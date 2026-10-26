namespace UWDataImport.UWData
{
	/// <summary>
	/// Small arithmetic the rules and the art builders share (cleanup phase 1, 2026-10-11).
	/// Until then five classes each kept a private clamp with the same body. Rounding is NOT
	/// here: UWRounding names the three ways a fraction becomes a whole number (cleanup phase
	/// 10), and UWUnits.RoundToInt is its halves-to-even for the world conversions.
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
