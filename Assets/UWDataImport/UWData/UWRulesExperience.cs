namespace UWDataImport.UWData
{
	/// <summary>
	/// THE EXPERIENCE RULES of the rule set (experience.*; cleanup phase 10, 2026-10-25): what a
	/// level and a skill point cost, the gate at the end of a career, what a kill yields, the maximum
	/// hit points a level brings and the chance of a trainer's extra point. Experience counts in the
	/// file's tenths, as the panel shows a tenth of it. Each default is the const of the class that
	/// applies it; see UWRules.
	/// </summary>
	public sealed class UWRulesExperience
	{
		[UWRule(typeof(UWExperience), nameof(UWExperience.PointsPerLevelUnit),
			"Experience (in tenths) per point of the level table.", Minimum = 1)]
		public int PointsPerLevelUnit = UWExperience.PointsPerLevelUnit;

		[UWRule(typeof(UWExperience), nameof(UWExperience.myLevelUpAt),
			"Points of the level table each level needs, levels 1 to 16 (16 numbers).", Minimum = 0)]
		public int[] LevelUpAt = (int[])UWExperience.myLevelUpAt.Clone();

		[UWRule(typeof(UWExperience), nameof(UWExperience.ExperiencePerSkillPoint),
			"Experience (in tenths) per skill point, independent of the level.", Minimum = 1)]
		public int PerSkillPoint = UWExperience.ExperiencePerSkillPoint;

		[UWRule(typeof(UWExperience), nameof(UWExperience.AwardCap),
			"Whoever holds more experience (in tenths) than this gains nothing any more.")]
		public int AwardCap = UWExperience.AwardCap;

		public readonly KillRules Kill = new KillRules();

		public readonly MaxHealthRules MaxHealth = new MaxHealthRules();

		public readonly SkillRaiseRules SkillRaise = new SkillRaiseRules();

		/// <summary>experience.kill.*: a slain creature yields factor * its table value plus two dice
		/// of that value (UWCritterCombat.GetKillExperience).</summary>
		public sealed class KillRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.ExperienceFactor),
				"A kill yields this times the creature's experience value, plus two dice of it.")]
			public int Factor = UWCritterRules.ExperienceFactor;
		}

		/// <summary>experience.maxHealth.*: the maximum hit points, base + strength * level / divisor
		/// (UWExperience.GetMaximumHealth).</summary>
		public sealed class MaxHealthRules
		{
			[UWRule(typeof(UWExperience), nameof(UWExperience.MaximumHealthBase),
				"Maximum hit points before strength and level.")]
			public int Base = UWExperience.MaximumHealthBase;

			[UWRule(typeof(UWExperience), nameof(UWExperience.MaximumHealthDivisor),
				"Maximum hit points: strength times level, divided by this.", Minimum = 1)]
			public int StrengthLevelDivisor = UWExperience.MaximumHealthDivisor;
		}

		/// <summary>experience.skillRaise.*: a raised skill that lags behind its attribute gets one
		/// more point when a roll below this range's value falls under the gap
		/// (UWPlayerVitals.TryIncreaseSkill).</summary>
		public sealed class SkillRaiseRules
		{
			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.msSkillRollRange),
				"The extra point's roll range per governing attribute: Strength, Dexterity, Intelligence (3 numbers).",
				Minimum = 1)]
			public int[] ChanceRange = (int[])UWPlayerVitals.msSkillRollRange.Clone();
		}
	}
}
