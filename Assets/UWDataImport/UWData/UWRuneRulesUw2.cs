namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S RUNE TRAPS (UW2 plan G1 part E2, 2026-11-13; Underworld Exhumed uw2 obj/USEITEMS.C UseRune,
	/// motion/OBJPHYS.C's UseObj of a usable object a body runs into, combat/SPELLS2.C creat_spell's cases 2 and 3).
	/// Rune of Flame and Rune of Stasis put a flam rune (0x19E) or a tym rune (0x19F) in front of the caster; the two are
	/// usable (COMOBJ byte 6 bit 1) and not solid, so a body that runs into one uses it (UWMotionCore.CollideObjects
	/// hands it to the world's UseOnMover) and walks on:
	///
	///   flam rune   3d4 + 4 of plain fire (type 8) on whoever touched it, nobody's blow; the rune turns into the
	///               explosion 0x1C2 with its flames (fireball_effect)
	///   tym rune    the player is paralysed for 4 to 19 (the counter the frame loop takes down once a second) and
	///               reads "You feel your limbs stiffen. You are unable to move."; a creature is held as by the spell
	///               (hit_critter_goal: goal 15 for 0x40 + 0..63 * 4, attitude 1, unless it resists); the rune goes
	/// </summary>
	public static class UWRuneRulesUw2
	{
		public const int FlameRuneId = 0x19E;

		public const int StasisRuneId = 0x19F;

		/// <summary>UseRune's rollem(3, 4) + 4.</summary>
		public const int FlameDice = 3;

		public const int FlameSides = 4;

		public const int FlameBonus = 4;

		/// <summary>damage_item's type 8: plain fire.</summary>
		public const int FlameDamageType = UWDamageTypes.PlainFire;

		/// <summary>"You feel your limbs stiffen.  You are unable to move." (block 1, 0x163 + 1).</summary>
		public const int StiffenString = 0x163 + 1;

		/// <summary>The explosion the flam rune turns into (ITEM_EXPLOSION_1C2).</summary>
		public const int ExplosionId = UWExplosionRules.ExplosionObjectId;

		/// <summary>creat_spell puts a rune at the caster's height + 12.</summary>
		public const int PlacedAbove = 12;

		public static bool IsRune(int piObjectId)
		{
			return piObjectId == FlameRuneId || piObjectId == StasisRuneId;
		}

		/// <summary>The player's paralysis: (rand() &amp; 0xF) + 4.</summary>
		public static int PlayerParalysis(int piRandom)
		{
			return (piRandom & 0xF) + 4;
		}

		/// <summary>A creature's hold: ((rand() &amp; 0x3F) &lt;&lt; 2) + 0x40.</summary>
		public static int CreatureHoldTime(int piRandom)
		{
			return ((piRandom & 0x3F) << 2) + 0x40;
		}

		/// <summary>
		/// UseRune: pOCreature touched pORune, or the player when it is null. The rune leaves the world either way.
		/// </summary>
		public static void Touch(UWObject pORune, UWObject pOCreature, IUWSpellHost pIHost)
		{
			if (pORune == null || pIHost == null || pIHost.World == null || !IsRune(pORune.ID))
				return;

			if (pORune.ID == FlameRuneId)
			{
				fFlame(pORune, pOCreature, pIHost);

				return;
			}

			int liRandom = UWRandom.Next(UWOriginalRandom.Range);

			if (pOCreature != null)
				UWTargetSpellRulesUw2.HitCritterGoal(pOCreature, pIHost, UWTargetSpellRulesUw2.HeldGoal, UWTargetSpellRulesUw2.UpsetAttitude,
					CreatureHoldTime(liRandom));
			else if (pIHost.HasPlayer && pIHost is IUWSpellHostUw2 lIPlayer)
			{
				UWPlayerRecordUw2.SetParalysis(lIPlayer.Record, PlayerParalysis(liRandom));
				pIHost.World.Presentation.AddGeneralMessage(StiffenString);
			}

			pIHost.World.Remove(pORune);
		}

		/// <summary>The flam rune: the burn on whoever touched it, then the explosion with its flames where the rune lay.</summary>
		private static void fFlame(UWObject pORune, UWObject pOCreature, IUWSpellHost pIHost)
		{
			int liDamage = UWRandom.RollDice(FlameDice, FlameSides) + FlameBonus;

			if (pOCreature != null)
			{
				UWLevel lOLevel = UWSpellCasting.LevelOf(pIHost);
				int liIndex = lOLevel != null ? UWAreaWalk.MobileIndexOf(lOLevel, pOCreature) : -1;

				if (liIndex >= 0)
					pIHost.World.Damage(UWDamageTarget.Creature(liIndex), UWBlow.Trap(liDamage, FlameDamageType));
			}
			else if (pIHost.HasPlayer)
				pIHost.World.Damage(UWDamageTarget.ThePlayer, UWBlow.ToPlayer(UWDamageRequest.Plain(liDamage, FlameDamageType)));

			UWTilePos lOTile = new UWTilePos(pORune.TileX, pORune.TileY);
			IUWPresentation lIPresentation = pIHost.World.Presentation;

			lIPresentation.ShowEffect(ExplosionId, new UWSpot(lOTile, pORune.XPos, pORune.YPos, pORune.ZPos),
				UWExplosionRules.LengthToSeconds(UWExplosionRules.ExplosionLengthTicks));

			foreach (UWExplosionRules.Flame lOFlame in UWExplosionRules.ScatterFlames(ExplosionId, pORune.XPos, pORune.YPos, pORune.ZPos))
				lIPresentation.ShowEffect(lOFlame.ObjectId, new UWSpot(lOTile, lOFlame.FineX, lOFlame.FineY, lOFlame.ZPos),
					UWExplosionRules.LengthToSeconds(lOFlame.LengthTicks), lOFlame.StartFrame);

			pIHost.World.Remove(pORune);
		}
	}
}
