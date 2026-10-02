namespace UWDataImport.UWData
{
	/// <summary>
	/// Experience and levelling up.
	///
	/// Source: the disassembly of UW.EXE - AwardKillEXP_seg022_1725 (83677) and
	/// EXPChange_seg037_48 (the levels, the skill points and the depth penalty). uw-formats.txt
	/// says nothing about the rules, only about the fields in the savegame.
	///
	/// THE SEQUENCE ON A KILL (AwardKillEXP, 83757-83810)
	///
	///   exp = 4 * base + 2d(base)          base = row word 0x28, ExperienceWhenKilled
	///   strong individual: times (24 + RNG(24)) / 16
	///   EXPChange(exp) as it is
	///   if one stands on a level that is too shallow for one's own level: 1 + exp / 2
	///
	/// Until 2026-09-20 the port took base + 2d(base) and then HALVED it with a random
	/// rounding, after the UnderworldGodot reading. The disassembly has neither: the kill
	/// hands 4 * base + the dice straight to EXPChange (deviation 50).
	///
	/// LEVELS
	///
	/// The experience is divided by 500, and the table says how many such points
	/// a level needs. It grows quickly: level 5 wants 4 points, level 10 already 24,
	/// level 15 a full 128. Level 16 is the end, and so is 96000 experience: above that
	/// nothing is awarded any more at all (see AwardCap).
	///
	/// SKILL POINTS run separately: one per full 3000 experience, independent
	/// of the level. On levelling up, one more is added per level.
	/// </summary>
	public static class UWExperience
	{
		/// <summary>Experience per point of the level-up table.</summary>
		public const int PointsPerLevelUnit = 500;

		/// <summary>Experience per skill point. In UW2 it is 1500.</summary>
		public const int ExperiencePerSkillPoint = 3000;

		/// <summary>
		/// THE END OF THE CAREER, and it is a GATE, not a ceiling: whoever already holds
		/// more than this gains nothing at all any more - no experience, no skill point, no
		/// level (EXPChange_seg037_48, lines 123406-123418: the 32-bit value is compared
		/// before anything is added, and the routine returns at once). 96000 tenths, so 9600
		/// points as the panel shows them.
		///
		/// Measured 2026-09-20: the user set SAVE4 to exactly 100000 tenths, killed a goblin
		/// in the ORIGINAL and saved over it - the value had not moved by a single tenth.
		/// Until then we added the gain and only stopped the levelling.
		/// </summary>
		public const int AwardCap = 0x17700;

		public const int MaximumLevel = 16;

		/// <summary>How many 500-experience points each level requires.</summary>
		private static readonly int[] myLevelUpAt =
			{ 0, 1, 2, 3, 4, 6, 8, 0xC, 0x10, 0x18, 0x20, 0x30, 0x40, 0x60, 0x80, 0xC0 };

		/// <summary>The mutable part - everything a gain can touch.</summary>
		public struct State
		{
			public int Experience;
			public int Level;
			public int SkillPoints;
			public int SkillPointsTotal;
		}

		/// <summary>What a slain creature yields: 4 * base + 2d(base), a strong individual
		/// (word 0x0D bit 10) times (24 + RNG(24)) / 16 - the formula is UWCritterCombat's.
		/// Hand the result to Change, there is no halving in between.</summary>
		public static int GetKillReward(UWObjectClassProperties.Critter pOCritter, bool pbStrong)
		{
			return UWCritterCombat.GetKillExperience(pOCritter.ExperienceWhenKilled, pbStrong);
		}

		/// <summary>
		/// The core without halving, like UW.EXE EXPChange_seg037_48 - this is how a conversation
		/// awards its experience (new_player_exp, see UWConversationSession). A negative value
		/// subtracts, down to zero at most. Returns the number of levels gained.
		/// </summary>
		public static int Change(ref State pOState, int piAmount, int piDungeonLevel)
		{
			if (piAmount < 0)
			{
				pOState.Experience = System.Math.Max(0, pOState.Experience + piAmount);
				return 0;
			}

			// The gate above: only a LOSS still works up here (the original tests the amount
			// first and subtracts before this comparison).
			if (pOState.Experience > AwardCap)
				return 0;

			if (piAmount == 0)
				return 0;

			int liGain = piAmount;

			// On a dungeon level too shallow for one's own level there is less.
			if ((piDungeonLevel << 1) + 2 < pOState.Level)
				liGain = 1 + (liGain / 2);

			int liNewTotalSkillPoints = (pOState.Experience + liGain) / ExperiencePerSkillPoint;

			if (pOState.SkillPointsTotal < liNewTotalSkillPoints)
			{
				pOState.SkillPoints += liNewTotalSkillPoints - pOState.SkillPointsTotal;
				pOState.SkillPointsTotal = liNewTotalSkillPoints;
			}

			// No ceiling here: the gate above is the only limit, so the last award may carry
			// the value past it (which is how a character ends up above 9600 points).
			pOState.Experience += liGain;

			int liLevelsGained = 0;
			int liPoints = pOState.Experience / PointsPerLevelUnit;

			while (pOState.Level + liLevelsGained < MaximumLevel
				&& pOState.Level + liLevelsGained < myLevelUpAt.Length
				&& myLevelUpAt[pOState.Level + liLevelsGained] <= liPoints)
			{
				liLevelsGained++;
			}

			if (liLevelsGained <= 0)
				return 0;

			pOState.Level += liLevelsGained;
			pOState.SkillPoints += liLevelsGained;

			return liLevelsGained;
		}

		/// <summary>The experience (in the file's tenths) at which the level after piLevel is
		/// reached - the same table Change climbs -, or -1 at the top. For the help window.
		/// </summary>
		public static int GetNextLevelExperience(int piLevel)
		{
			if (piLevel < 1 || piLevel >= MaximumLevel || piLevel >= myLevelUpAt.Length)
				return -1;

			return myLevelUpAt[piLevel] * PointsPerLevelUnit;
		}

		/// <summary>Maximum vitality after levelling up: thirty plus one fifth of
		/// strength times level.</summary>
		public static int GetMaximumHealth(int piStrength, int piLevel)
		{
			return 0x1E + ((piStrength * piLevel) / 5);
		}

		/// <summary>Maximum mana from mana skill and intelligence.</summary>
		public static int GetMaximumMana(int piManaSkill, int piIntelligence)
		{
			return ((piManaSkill + 1) * piIntelligence) >> 3;
		}
	}
}
