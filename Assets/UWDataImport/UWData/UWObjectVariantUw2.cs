using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S ITEM STEPS: the valuable a critter carries, from Underworld Exhumed's uw2
	/// obj/TREASURE.C (a conversation rolls the partner's inventory before its script runs, per user
	/// 2026-10-31: the refused roll left the trade without goods). The light and the lock come with the
	/// game's rules in phase 4 of Docs/UW2-PLAN.md.
	/// </summary>
	public sealed class UWObjectVariantUw2 : IUWObjectVariant
	{
		private const int LevelsPerWorld = 8;

		private const int WorldStep = 3;

		private const int FirstWorldSpan = 37;

		private const int FirstWorldShift = 30;

		/// <summary>A roll of this type becomes the first: the coin, never the gold coin.</summary>
		private const int RolledAway = 1;

		public bool LightUsedUp(int piQuality)
		{
			throw new NotSupportedException("The second game's lights come in phase 4 of Docs/UW2-PLAN.md.");
		}

		/// <summary>rand() % (37 - world * 3) - (30 - world * 3), at least 0 and never 1; the world is the
		/// level's (TREASURE.C:103-107).</summary>
		public int RollValuableType(int piDungeonLevel)
		{
			int liWorld = ((piDungeonLevel - 1) / LevelsPerWorld) * WorldStep;
			int liType = UWRandom.Next(0, FirstWorldSpan - liWorld) - (FirstWorldShift - liWorld);

			return liType < 0 || liType == RolledAway ? 0 : liType;
		}

		public UWSkillCheck.ResultEnum PickRoll(int piPicklock, int piLockHeight)
		{
			throw new NotSupportedException("The second game's locks come in phase 4 of Docs/UW2-PLAN.md.");
		}
	}
}
