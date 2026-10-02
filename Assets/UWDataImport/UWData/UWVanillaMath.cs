namespace UWDataImport.UWData
{
	/// <summary>
	/// Arithmetic of the original that differs from today's.
	///
	/// Needed wherever what counts is not the mathematically correct result but
	/// exactly what the game computed in 1992 - one step off is visible when
	/// darkening.
	/// </summary>
	public static class UWVanillaMath
	{
		/// <summary>
		/// The original's square root - a Newton iteration with five rounds in
		/// 16-bit integers, not Math.Sqrt.
		///
		/// The starting value depends on the magnitude: below 256 it starts with 4 and
		/// computes in eight bits, above that with 64 in sixteen. Both are stated like this in
		/// the reference's disassembly (UnderWorldSqrt.sqrt_vanilla, with segment labels from the
		/// original), and it explicitly notes that the results may differ from those of
		/// today's square root functions.
		///
		/// Only the branches for values up to 0xFFFF are implemented. Larger values do not
		/// occur when darkening: the largest value appearing there is 28800.
		/// </summary>
		public static int Sqrt(int piValue)
		{
			if (piValue <= 0)
				return 0;

			int liBx = piValue & 0xFFFF;

			// Below 256 the original continues in eight bits, with 4 as the starting value.
			if ((liBx >> 8) == 0)
			{
				int liCl = 4;

				for (int liRound = 0; liRound < 5; liRound++)
				{
					int liAl = (liBx / liCl) & 0xFF;

					liCl = ((liCl + liAl) & 0xFF) >> 1;

					// A divisor of 0 would divide by zero in the next round.
					// In the original this cannot happen because bl is non-zero.
					if (liCl == 0)
						return 0;
				}

				return liCl;
			}

			int liCx = 0x40;

			for (int liRound = 0; liRound < 5; liRound++)
			{
				int liAx = (liBx / liCx) & 0xFFFF;

				liCx = ((liCx + liAx) & 0xFFFF) >> 1;

				if (liCx == 0)
					return 0;
			}

			return liCx;
		}
	}
}
