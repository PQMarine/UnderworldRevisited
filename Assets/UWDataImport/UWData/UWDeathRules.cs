namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT BECOMES OF THE PLAYER AT ZERO HIT POINTS (player_is_dead, UnderworldExhumed
	/// game/SKILLS.C, with do_resurrect beside it; UW.EXE DeathAndResurrection_ovr143_1610 and
	/// Resurrection_ovr143_1521). Engine-free since cleanup step 4.3 (2026-10-17): the decision and
	/// the numbers were inline in UWDeath until then, which polled the hit points every frame; the
	/// vitals now raise UWPlayerVitals.Died on the crossing and UWDeath acts on it.
	///
	///   Talisman counter (quest 36) is 0     vitality 4, no death (UW.EXE checks PLAYER.DAT 0x6D).
	///   Silver tree planted, not on level 9  resurrection at the tree.
	///   Otherwise                            final death.
	/// </summary>
	public static class UWDeathRules
	{
		public enum Outcome
		{
			/// <summary>The last talisman is gone: the player cannot die and keeps 4 hit points.</summary>
			Spared,

			/// <summary>Death, then back at the silver tree.</summary>
			Resurrect,

			/// <summary>The end (real_death(1)).</summary>
			Final
		}

		/// <summary>Vitality set when the talisman counter is 0 instead of dying.</summary>
		public const int SparedVitality = 4;

		/// <summary>Death costs an eighth of the experience (player_get_exp(-(exp >> 3))).</summary>
		public const int ExperienceLossShift = 3;

		/// <summary>Up to this maximum the resurrection restores all of it (avghit > 8, max_mana > 8).</summary>
		public const int FullRestoreLimit = 8;

		/// <summary>
		/// The decision: piTalismans is quest flag 36 (the talismans still to destroy), piTreeLevel
		/// the silver tree's level, 1-based and 0 for none (player->tree), piLevelIndex the level the
		/// player died on, 0-based, piLevelCount the levels there are. A tree level beyond them
		/// counts as none, as UWDeath has always had it.
		/// </summary>
		public static Outcome Decide(int piTalismans, int piTreeLevel, int piLevelIndex, int piLevelCount)
		{
			if (piTalismans == 0)
				return Outcome.Spared;

			if (piTreeLevel > 0 && piTreeLevel <= piLevelCount && piLevelIndex != UWRules.Current.Levels.Void)
				return Outcome.Resurrect;

			return Outcome.Final;
		}

		/// <summary>The experience a death takes.</summary>
		public static int ExperienceLoss(int piExperience)
		{
			return piExperience >> ExperienceLossShift;
		}

		/// <summary>The hit points after the resurrection: the maximum less 2 and rand() * 3 / 0x8000
		/// (0 to 2), all of it up to 8 (do_resurrect). Draws only above 8.</summary>
		public static int ResurrectionHitPoints(int piMaxHitPoints)
		{
			return piMaxHitPoints > FullRestoreLimit ? piMaxHitPoints - 2 - UWRandom.Next(3) : piMaxHitPoints;
		}

		/// <summary>The mana after the resurrection: the maximum less an eighth and 2, all of it up
		/// to 8 (do_resurrect).</summary>
		public static int ResurrectionMana(int piMaxMana)
		{
			return piMaxMana > FullRestoreLimit ? piMaxMana - (2 + (piMaxMana / 8)) : piMaxMana;
		}
	}
}
