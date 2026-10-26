namespace UWDataImport.UWData
{
	/// <summary>
	/// The skill check of the original - a single routine on which everything in the whole game
	/// depends: hitting, picking locks, detecting traps, haggling, drowning.
	///
	/// The calculation is
	///
	///     score = skill - target value + random(0..30)
	///
	/// and the result falls into four bands:
	///
	///     up to  2   critical failure
	///     up to 15   failure
	///     up to 28   success
	///     above      critical success
	///
	/// So the die carries more weight than the skill: between hopeless and
	/// certain there is only a difference of about 30 points. This matches the user's
	/// observation that a mage misses very often, but not always.
	///
	/// Origin: from the disassembled original, see UnderworldGodot
	/// (hankmorgan, MIT), src/player/playerdatskills.cs. The formula was taken over, not the
	/// code.
	/// </summary>
	public static class UWSkillCheck
	{
		public enum ResultEnum
		{
			CriticalFailure = -1,
			Failure = 0,
			Success = 1,
			CriticalSuccess = 2
		}

		/// <summary>Range of the random component. The roll goes from 0 to 30.</summary>
		public const int RollRange = 31;

		/// <summary>The upper edges of the bands: a score up to 2 is a critical failure, up to 15
		/// a failure, up to 28 a success, above it a critical success.</summary>
		public const int CriticalFailureAtMost = 2;

		public const int FailureAtMost = 0xF;

		public const int SuccessAtMost = 0x1C;

		/// <summary>The roll's range and the bands are the rule set's (UWRulesCombat.SkillCheck,
		/// combat.skillCheck.*; cleanup phase 10, 2026-10-25) - the consts above are their
		/// defaults.</summary>
		public static ResultEnum Check(int piSkillValue, int piTargetValue)
		{
			int liScore = piSkillValue - piTargetValue + UWRandom.Next(0, UWRules.Current.Combat.SkillCheck.RollRange);

			return GetResult(liScore);
		}

		/// <summary>The band classification kept separate, so that it can be tested without randomness.</summary>
		public static ResultEnum GetResult(int piScore)
		{
			UWRulesCombat.SkillCheckRules lORules = UWRules.Current.Combat.SkillCheck;

			if (piScore <= lORules.CriticalFailureAtMost)
				return ResultEnum.CriticalFailure;

			if (piScore <= lORules.FailureAtMost)
				return ResultEnum.Failure;

			if (piScore <= lORules.SuccessAtMost)
				return ResultEnum.Success;

			return ResultEnum.CriticalSuccess;
		}

		/// <summary>Whether the check counts as passed. A failure and a critical
		/// failure differ only in their consequences, not in the outcome.</summary>
		public static bool IsSuccess(ResultEnum peResult)
		{
			return peResult == ResultEnum.Success || peResult == ResultEnum.CriticalSuccess;
		}
	}
}
