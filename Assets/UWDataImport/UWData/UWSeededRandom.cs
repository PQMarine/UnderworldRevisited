namespace UWDataImport.Presentation
{
	/// <summary>
	/// A SMALL FIXED RANDOM SEQUENCE (xorshift32: 13, 17, 5), the same on every run and engine, for
	/// the HUD art's texture synthesis (cleanup 9.9, 2026-10-25, CLEANUP-PLAN 8.4: three builders
	/// carried the same function). Each builder names its own seed; a picture is the same only as
	/// long as its draws come in the same order from the same seed - Classic Wide's bands share one
	/// sequence, so the instance is passed on, not a copy.
	///
	/// Not the game's random source (UWRandom, the rules') and not the original's (UWOriginalRandom).
	/// </summary>
	public sealed class UWSeededRandom
	{
		private uint muState;

		public UWSeededRandom(uint puSeed)
		{
			muState = puSeed;
		}

		/// <summary>The next value of the sequence.</summary>
		public uint Next()
		{
			muState ^= muState << 13;
			muState ^= muState >> 17;
			muState ^= muState << 5;

			return muState;
		}

		/// <summary>The next value below piCount (the remainder; piCount must be over 0).</summary>
		public int Below(int piCount)
		{
			return (int)(Next() % (uint)piCount);
		}
	}
}
