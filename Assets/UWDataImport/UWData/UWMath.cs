namespace UWDataImport.UWData
{
	/// <summary>
	/// Small arithmetic the rules and the art builders share (cleanup phase 1, 2026-10-11).
	/// Until then five classes each kept a private clamp with the same body. Rounding is NOT
	/// here on purpose: the rounding helpers (halves to even in UWUnits, the original's own in
	/// the motion core) are named for their rule and stay where they are.
	/// </summary>
	public static class UWMath
	{
		/// <summary>piValue held to piMin..piMax. With piMin above piMax the minimum wins, as
		/// the private copies did.</summary>
		public static int Clamp(int piValue, int piMin, int piMax)
		{
			return piValue < piMin ? piMin : (piValue > piMax ? piMax : piValue);
		}

		/// <summary>pfValue held to pfMin..pfMax. With pfMin above pfMax the minimum wins, as
		/// the private copies did.</summary>
		public static float Clamp(float pfValue, float pfMin, float pfMax)
		{
			return pfValue < pfMin ? pfMin : (pfValue > pfMax ? pfMax : pfValue);
		}
	}
}
