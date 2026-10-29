namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FIRST GAME'S PLAYER STEPS (phase 0 step 2, 2026-10-28): what UWExperience.Change,
	/// UWSleepRules.Sleep and UWTrapDisarming did at these steps until then, moved here unchanged.
	/// </summary>
	public sealed class UWPlayerVariantUw1 : IUWPlayerVariant
	{
		/// <summary>The trap checks' target, finding and disarming alike (reference:
		/// trapdisarming.DetectTrapTrigger and DoTrapSkillCheck).</summary>
		public const int TrapCheckDifficulty = 8;

		/// <summary>Only a loss still works above the cap (the original tests the amount first and
		/// subtracts before this comparison); a gain then counts in full.</summary>
		public UWExperienceGateEnum ExperienceGate(UWRulesExperience pORules, int piExperience, int piGain)
		{
			return piExperience > pORules.AwardCap ? UWExperienceGateEnum.Ignore : UWExperienceGateEnum.Count;
		}

		/// <summary>On a dungeon level too shallow for one's own level there is less.</summary>
		public int ExperienceGain(int piGain, int piDungeonLevel, int piCharacterLevel)
		{
			if ((piDungeonLevel << 1) + 2 < piCharacterLevel)
				return 1 + (piGain / 2);

			return piGain;
		}

		/// <summary>Not in the Void (label D44 of the sleep: refused like water or lava).</summary>
		public bool MaySleepAt(UWRules pORules, int piDungeonLevel)
		{
			return piDungeonLevel != UWLevelIds.Number(pORules.Levels.Void);
		}

		public int TrapDetectTarget()
		{
			return TrapCheckDifficulty;
		}

		public int TrapDisarmTarget()
		{
			return TrapCheckDifficulty;
		}
	}
}
