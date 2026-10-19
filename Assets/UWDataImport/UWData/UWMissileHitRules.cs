namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT A MISSILE'S HIT DOES, in numbers: the hit maths of MissileAttackHit_seg022_14BF
	/// (missile_newhit -> missile_thwack -> do_damage, UnderworldExhumed motion/OBJPHYS.C and
	/// combat/COMBAT.C). Cleanup step 3.5 (2026-10-17) moved them here out of UWProjectileFlight,
	/// so the self-check can pin them (group MissileHit); the flight keeps the picture, UWMissiles
	/// carries the answers out in the game - sounds, damage, flash, impact. Every rule below was
	/// the flight's own expression before; nothing changed in the move.
	/// </summary>
	public static class UWMissileHitRules
	{
		/// <summary>The first missile objects: major class 0, minor class 1 - the only ones that
		/// hurt what they hit (ObjectUse class 0 minor 1 -> seg029_29EE_AD).</summary>
		public const int FirstMissileObjectId = 0x10;

		public const int LastMissileObjectId = 0x1F;

		/// <summary>Whether a flying thing of this id hurts what it hits: a missile object (arrow,
		/// bolt, sling stone, spell missile), not a thrown sword or a barrel knocked loose. One rule
		/// for the hit and for the throw's typed damage (UWPlayerThrow), which carried its own copy
		/// of the range until 2026-10-17.</summary>
		public static bool HurtsWhatItHits(int piItemId)
		{
			return piItemId >= FirstMissileObjectId && piItemId <= LastMissileObjectId;
		}

		/// <summary>A fireball or lightning bolt detonates at rest: its picture belongs to that, not
		/// to the hit (UWObjectDamageRules.TryGetBlast).</summary>
		public static bool Detonates(int piItemId)
		{
			return UWObjectDamageRules.TryGetBlast(piItemId, out int _, out int _, out int _);
		}

		/// <summary>The missile's damage after the armour of the part it struck (do_damage labels
		/// A04-A10 through UWCritterCombat.SubtractArmour) - for a creature and for an object.</summary>
		public static int DamageAfterArmour(int piDamage, int piArmour)
		{
			return UWCritterCombat.SubtractArmour(piDamage, piArmour);
		}

		/// <summary>TWO HIT SOUNDS (2026-10-12; ours played none): missile_thwack plays the hit
		/// effect on the struck mobile, then do_damage plays it again louder by the damage BEFORE
		/// the armour (UnderworldExhumed combat/COMBAT.C). This is that second volume - the blows'
		/// own (UWAttackResolution.HitVolume, cleanup step 4.9, 2026-10-18).</summary>
		public static int HitSoundVolume(int piDamageBeforeArmour)
		{
			return UWAttackResolution.HitVolume(piDamageBeforeArmour);
		}

		/// <summary>NO TRACE WHEN THE ARMOUR SWALLOWS IT: do_damage (COMBAT.C) returns before the
		/// blood or the flash when the damage after the armour is 0, as the blows already do. A
		/// resistance does not count: it is asked later, in damage_item, so a ghost struck by acid
		/// still flashes (per user's question, 2026-10-13). Ours showed it always until then. The
		/// player's own hit report to the eyes follows the same test.</summary>
		public static bool LeavesTrace(int piDamageAfterArmour)
		{
			return piDamageAfterArmour > 0;
		}

		/// <summary>The missile's box against the player's body: pickloc of the missile from its z
		/// to its z plus its COMOBJ height (UWArmourProtection.PickBodyPart, which may draw from
		/// UWRandom in the unclear middle).</summary>
		public static int PickPlayerBodyPart(int piPlayerFeet, int piPlayerBodyHeight, int piMissileZ, int piMissileHeight)
		{
			return UWArmourProtection.PickBodyPart(piPlayerFeet, piPlayerFeet + piPlayerBodyHeight, piMissileZ,
				piMissileZ + piMissileHeight);
		}

		/// <summary>The owner byte (last_hit, record byte 0x12) of the player's missile: his mobile
		/// index. 0 is nobody's (a trap's), above 1 a creature's index.</summary>
		public const int PlayerOwner = 1;

		/// <summary>
		/// WHOM A MISSILE'S HIT IS CHARGED TO: its owner byte, as damage_critter takes who from the
		/// missile's last_hit (UnderworldExhumed critter/AI.C:1474-1476) - the player, a creature's
		/// index, or nobody (UWBlow.Nobody) for a trap's, whose owner missile_fire leaves 0
		/// (combat/MISSILE.C:245-300). The one rule for every flight since 2026-10-19 (the user's
		/// decision "as the original"): until then a launched flight was charged to its launching
		/// creature or else the player, so a trap's missile counted as the player's.
		/// </summary>
		public static int AttackerOf(int piOwner)
		{
			if (piOwner == PlayerOwner)
				return UWCritterBrain.PlayerIndex;

			return piOwner > PlayerOwner ? piOwner : UWBlow.Nobody;
		}

		/// <summary>
		/// WALKING INTO A MISSILE HURTS, WHOSEVER IT IS: do_objhit uses a struck object whose COMOBJ
		/// usable bit is set (byte 6 bit 1, UWCommonObjectProperties.UsedWhenThrown - the missiles
		/// 0x10-0x17) on the mover, and UseObj runs missile_newhit for a missile object with the
		/// mover as what it hit (UnderworldExhumed motion/OBJPHYS.C:141-200, obj/USEITEMS.C:90-92),
		/// charged to the missile's owner (AttackerOf). Since 2026-10-19 (the user's decision "as the
		/// original") for every such flight the player walks into; his own alone until then.
		/// </summary>
		public static bool HurtsWhoWalksIntoIt(int piItemId, bool pbUsable)
		{
			return pbUsable && HurtsWhatItHits(piItemId);
		}

		/// <summary>The eighths beyond the two radii the player may be from a missile and still be
		/// touching it (InTouchReach).</summary>
		public const int TouchSlackEighths = 1;

		/// <summary>
		/// HOW OFTEN A MISSILE THE PLAYER PRESSES AGAINST STRIKES HIM: in the original on every
		/// motion step that meets it (do_objhit, UnderworldExhumed motion/OBJPHYS.C:141-200), and the
		/// player's motion runs once per drawn frame - against a missile held by Freeze Time he dies
		/// fast (per user on the original, 2026-10-19: the arrow stays as it is and the damage is not
		/// small). Ours strikes at a fixed rate instead, at most once per this many PIT ticks while the
		/// motion meets the missile: 8, 1/32 s, the rate of the lava's rolls, which is what the
		/// original reaches in the user's DOSBox (Docs/DEVIATIONS.md, Rules: nothing hangs on the
		/// frame rate). Without the freeze the push sends the missile off after the first hit anyway.
		/// Until that day ours struck once per touch.
		/// </summary>
		public const int WalkInHitIntervalTicks = 8;

		/// <summary>A meeting strikes when no walk-in hit of this missile came within the interval
		/// (piLastHit -1: none yet).</summary>
		public static bool WalkInHitDue(long plNow, long plLastHit)
		{
			return plLastHit < 0 || plNow - plLastHit >= WalkInHitIntervalTicks;
		}

		/// <summary>
		/// THE PLAYER STILL TOUCHES THE MISSILE: his box (his COMOBJ radius, not the scan's shrunk
		/// one) and the missile's within one eighth more across, their heights overlapping. Since
		/// 2026-10-19 (evening) it only keeps the contact at the LAUNCH from striking until he is out of
		/// this reach; a walk-in strikes on every meeting at the fixed rate WalkInHitIntervalTicks, as
		/// the original on every step (per user on the original the same evening). Before, once per
		/// touch; a distance of 24 world units between the pictures until that morning.
		/// </summary>
		public static bool InTouchReach(int piPlayerX8, int piPlayerY8, int piPlayerFeet, int piPlayerRadius, int piPlayerHeight,
			int piMissileX8, int piMissileY8, int piMissileZ, int piMissileRadius, int piMissileHeight)
		{
			int liReach = piPlayerRadius + piMissileRadius + TouchSlackEighths;
			int liDx = piPlayerX8 - piMissileX8;
			int liDy = piPlayerY8 - piMissileY8;

			if (liDx > liReach || -liDx > liReach || liDy > liReach || -liDy > liReach)
				return false;

			// A missile of height 0 is a point one unit high (the scan's record, CreateColllisionRecord).
			int liTop = piMissileZ + (piMissileHeight > 0 ? piMissileHeight : 1);

			return liTop >= piPlayerFeet && piMissileZ <= piPlayerFeet + piPlayerHeight;
		}

		/// <summary>The player's own missile (AttackerOf is the player): his impact sound, his hits
		/// reported to the eyes (2026-10-19, by the owner byte).</summary>
		public static bool IsPlayersOwn(int piOwner)
		{
			return piOwner == PlayerOwner;
		}

		/// <summary>
		/// WHO THE COMBAT MUSIC HEARS A MISSILE'S HIT ON THE PLAYER FROM: damage_critter
		/// (UnderworldExhumed critter/AI.C:1458-1515) takes who from the missile's last_hit. A
		/// CREATURE'S (who above 1) starts the music like its blow - MUSIC_DANGER or MUSIC_COMBAT for
		/// the player, damage or not (2026-10-12; ours played nothing). THE PLAYER'S OWN (who 1) runs
		/// the first branch, as for a creature he hits: MUSIC_FOE_HURT or MUSIC_COMBAT by his own hit
		/// points (per user on the original, 2026-10-19: walking into his own arrow during Freeze
		/// Time started the combat music; ours kept it silent). A trap's (who 0) plays nothing.
		/// </summary>
		public static UWDamageMusic MusicFor(int piOwner)
		{
			if (piOwner == PlayerOwner)
				return UWDamageMusic.Player;

			return piOwner > PlayerOwner ? UWDamageMusic.Creature : UWDamageMusic.None;
		}

		/// <summary>THE FLASH AT THE MISSILE'S HEIGHT on a struck object: missile_thwack sets hitz to
		/// the missile's z plus half its height and do_damage puts the flash there - for a door no
		/// lower than its own z + 2 (COMBAT.C). Ours put it at the object's own position until
		/// 2026-10-13 (per user).</summary>
		public static int ObjectFlashZ(int piMissileZ, int piMissileHeight, bool pbDoor, int piObjectZ)
		{
			int liHitZ = piMissileZ + (piMissileHeight / 2);

			if (pbDoor && piObjectZ > liHitZ)
				liHitZ = piObjectZ + 2;

			return liHitZ;
		}

		/// <summary>The orb rock: thrown, it does nothing to what it strikes but Tybal's orb
		/// (UWItemTargetUse.SmashOrb).</summary>
		public static bool IsOrbRock(int piItemId)
		{
			return piItemId == UWObjectMechanics.OrbRockObjectId;
		}

		/// <summary>The orb rock thrown at Tybal's orb smashes it.</summary>
		public static bool SmashesOrb(int piMissileId, int piTargetId)
		{
			return IsOrbRock(piMissileId) && piTargetId == UWObjectMechanics.TyballOrbObjectId;
		}
	}
}
