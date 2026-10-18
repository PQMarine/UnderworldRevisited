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

		/// <summary>A CREATURE'S MISSILE starts the combat music like its blow: damage_critter
		/// (critter/AI.C) finds the shooter in the missile's last_hit and picks MUSIC_DANGER or
		/// MUSIC_COMBAT for the player, damage or not (2026-10-12; ours played nothing).</summary>
		public static bool StartsCombatMusic(bool pbFromCreature)
		{
			return pbFromCreature;
		}

		/// <summary>A missile with an attacker shakes the view (COMBAT.C missile_thwack -> do_damage,
		/// C5, 2026-10-12): a creature's or the player's own; one without (fromwho -1, a trap's)
		/// does not.</summary>
		public static bool ShakesPlayer(bool pbFromCreature, bool pbPlayersOwn)
		{
			return pbFromCreature || pbPlayersOwn;
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
