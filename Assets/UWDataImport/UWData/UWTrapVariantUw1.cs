namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FIRST GAME'S TRAP STEPS (phase 0 step 2, 2026-10-28): what UWFishing and UWTrapRules did
	/// at these steps until then, moved here unchanged.
	/// </summary>
	public sealed class UWTrapVariantUw1 : IUWTrapVariant
	{
		/// <summary>A bite when a roll of 0 to 4 gives 0; the tracking skill plays no part.</summary>
		public const int BiteOneIn = 5;

		public bool Bites(int piTrackSkill)
		{
			return UWRandom.Next(BiteOneIn) == 0;
		}

		public bool SwapsLock(int piTrapOwner)
		{
			return true;
		}
	}
}
