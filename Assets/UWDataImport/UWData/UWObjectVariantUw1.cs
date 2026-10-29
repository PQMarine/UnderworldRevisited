namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FIRST GAME'S ITEM STEPS (phase 0 step 2, 2026-10-28): what UWItemUse, UWCritterLoot and
	/// UWLockRules did at these steps until then, moved here unchanged.
	/// </summary>
	public sealed class UWObjectVariantUw1 : IUWObjectVariant
	{
		/// <summary>A light burned down to quality zero.</summary>
		public bool LightUsedUp(int piQuality)
		{
			return piQuality == 0;
		}

		/// <summary>A roll of 0 to 39 - 3 * level, less 33 - 3 * level, at least 0: the deeper the
		/// level, the dearer the valuable.</summary>
		public int RollValuableType(int piDungeonLevel)
		{
			int liSpan = 40 - (piDungeonLevel * 3);
			int liShift = 33 - (piDungeonLevel * 3);

			if (liSpan < 1)
				liSpan = 1;

			int liType = UWRandom.Next(0, liSpan) - liShift;

			if (liType < 0)
				liType = 0;

			return liType;
		}

		public UWSkillCheck.ResultEnum PickRoll(int piPicklock, int piLockHeight)
		{
			return UWSkillCheck.Check(piPicklock, piLockHeight * 3);
		}
	}
}
