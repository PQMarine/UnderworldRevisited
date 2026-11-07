using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S PLAYER STEPS (UW2 plan phase 4), from Underworld Exhumed's uw2 game/SKILLCHK.C, SKILLS.C and
	/// PLAYTIME.C. The trap targets depend on the world and wait for the objects' domain.
	/// </summary>
	public sealed class UWPlayerVariantUw2 : IUWPlayerVariant
	{
		/// <summary>player_get_exp's ceiling: a gain that would pass it is refused.</summary>
		public const int ExperienceCeiling = 0x7FFF0;

		private const int LevelsPerWorld = 8;

		/// <summary>duration_check's long cycle: every thirtieth step (the first game's every twenty-fourth).</summary>
		public const int CycleTicks = 30;

		public int LongCycleTicks => CycleTicks;

		public int LevelUpString => 162;

		public int ExplorationWeight(int piDungeonLevel) => (piDungeonLevel / LevelsPerWorld) + 1;

		public bool HasOrbLair => false;

		/// <summary>
		/// Past the ceiling nothing counts; with the total above the cap AFTER the gain it counts and gives skill
		/// points but no levels (SKILLCHK.C:88-96: exp += n, then "if (exp > 0x17700) return" before the level
		/// count). Until 2026-11-07 the total before the gain was tested (per user's question about the cap), so a
		/// gain crossing 96000 still gave level 16; in the original only a total of exactly 96000 reaches it.
		/// The original gives the skill points of a gain it then refuses at the ceiling; ours does not (52427.2
		/// points, never reached in play).
		/// </summary>
		public UWExperienceGateEnum ExperienceGate(UWRulesExperience pORules, int piExperience, int piGain)
		{
			if (piExperience + piGain > ExperienceCeiling)
				return UWExperienceGateEnum.Ignore;

			return piExperience + piGain > pORules.AwardCap ? UWExperienceGateEnum.CountWithoutLevels : UWExperienceGateEnum.Count;
		}

		/// <summary>Every gain halved, rounding at random, then halved again, plus one, in a world too early for
		/// the character (SKILLCHK.C:70-79).</summary>
		public int ExperienceGain(int piGain, int piDungeonLevel, int piCharacterLevel)
		{
			int liGain = piGain > 0 ? (piGain + UWRandom.Next(2)) / 2 : piGain;

			if ((((piDungeonLevel - 1) / LevelsPerWorld) * 2) + 2 < piCharacterLevel)
				return (liGain / 2) + 1;

			return liGain;
		}

		/// <summary>Everywhere; the Void's own rule, sleeping wakes the player from it, comes with the Void.</summary>
		public bool MaySleepAt(UWRules pORules, int piDungeonLevel)
		{
			return true;
		}

		private static bool fIsInPits => UWGameSession.Current != null && UWPlayerRecordUw2.IsInPits(UWGameSession.Current.PlayerFileUw2);

		public bool IsHeldAwake => fIsInPits;

		private const int BedrollLine = 0x17;

		private const int BedLine = 0x18;

		public int CampLine(int piHow)
		{
			return piHow == 1 ? BedrollLine : piHow == 2 ? BedLine : UWSleepRules.MakeCampMessage;
		}

		public bool PassingOutKills => fIsInPits;

		public bool HasGaramonDreams => false;

		public int TrapDetectTarget()
		{
			throw new NotSupportedException("The second game's trap targets come with its objects in phase 4 of Docs/UW2-PLAN.md.");
		}

		public int TrapDisarmTarget()
		{
			throw new NotSupportedException("The second game's trap targets come with its objects in phase 4 of Docs/UW2-PLAN.md.");
		}

		/// <summary>The world of the Scintillus Academy (levels 41 to 48).</summary>
		public const int AcademyWorld = 5;

		/// <summary>From this column on the Academy's eighth level gives mana again.</summary>
		public const int AcademyManaColumn = 25;

		/// <summary>restore_mana's test (uw2 combat/SPELLS.C:160): outside world 5, on its first level, or on its
		/// eighth at x 25 or more. A level unknown (0 or less) gives mana.</summary>
		public bool MayRestoreMana(int piDungeonLevel, int piTileX)
		{
			if (piDungeonLevel <= 0)
				return true;

			int liLevelInWorld = ((piDungeonLevel - 1) % LevelsPerWorld) + 1;

			return (piDungeonLevel - 1) / LevelsPerWorld != AcademyWorld || liLevelInWorld <= 1
				|| (liLevelInWorld >= LevelsPerWorld && piTileX >= AcademyManaColumn);
		}
	}
}
