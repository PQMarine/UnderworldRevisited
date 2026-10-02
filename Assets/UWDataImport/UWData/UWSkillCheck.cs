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

		public static ResultEnum Check(int piSkillValue, int piTargetValue)
		{
			int liScore = piSkillValue - piTargetValue + UWRandom.Next(0, RollRange);

			return GetResult(liScore);
		}

		/// <summary>The band classification kept separate, so that it can be tested without randomness.</summary>
		public static ResultEnum GetResult(int piScore)
		{
			if (piScore <= 2)
				return ResultEnum.CriticalFailure;

			if (piScore <= 0xF)
				return ResultEnum.Failure;

			if (piScore <= 0x1C)
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
