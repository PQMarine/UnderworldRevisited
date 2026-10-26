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
		/// integers, not Math.Sqrt (GetSquareRoot_seg019_F3F, cSqRt, which runs
		/// SquareRoot_seg019_A78; UnderworldExhumed IMATH.ASM).
		///
		/// The starting value depends on the magnitude, by the highest byte that is not zero:
		/// below 256 it starts with 4 and computes in eight bits, below 0x10000 with 64 in
		/// sixteen, below 0x1000000 with 0x400, above that with 0x4000 (0xFFFF from 0x40000000
		/// on), the upper two dividing the 32-bit value by a 16-bit guess. Five rounds do not
		/// always arrive at the root rounded down: just below a square the guess still swings
		/// one above it (3 gives 2, 8 gives 3, 255 gives 16; 120 values of 0 to 0xFFFF). The
		/// reference's disassembly notes the same (UnderWorldSqrt.sqrt_vanilla).
		///
		/// The halving keeps the carry of the sum (add, then rcr), so the sum never wraps.
		///
		/// THE TWO UPPER BRANCHES since cleanup phase 10 (2026-10-26), for the creature brain's
		/// distances in eighths (up to 2 * 512 squared): until then only values up to 0xFFFF
		/// were taken, the darkening's largest being 28800, and the brain had a root of its own
		/// rounded down (UWCritterBrain.IntSqrt).
		/// </summary>
		public static int Sqrt(int piValue)
		{
			if (piValue <= 0)
				return 0;

			uint luValue = (uint)piValue;

			// From 0x10000 on: the 32-bit value over a 16-bit guess.
			if ((luValue >> 16) != 0)
			{
				uint luGuess = (luValue >> 24) == 0 ? 0x400u : ((luValue >> 16) < 0x4000 ? 0x4000u : 0xFFFFu);

				for (int liRound = 0; liRound < 5; liRound++)
					luGuess = (luGuess + (luValue / luGuess)) >> 1;

				return (int)(luGuess & 0xFFFF);
			}

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

		/// <summary>
		/// The original's arcsine of 0 to 1.0 in 128 steps, as an angle of 0x10000 to the turn
		/// (seg063 +0x762, 129 words): floor(asin(i / 128) * 0x8000 / pi). Kept as the numbers
		/// themselves so that a mirror needs no floating point; the self-check compares them with
		/// the formula and, where the installation has it, with UW.EXE.
		/// </summary>
		public static readonly short[] ArcSineTable =
		{
			0, 81, 162, 244, 326, 407, 489, 570, 652, 733, 815, 897, 979, 1061, 1143, 1225,
			1307, 1389, 1471, 1554, 1636, 1719, 1801, 1884, 1967, 2050, 2133, 2216, 2300, 2383, 2467, 2551,
			2635, 2719, 2804, 2888, 2973, 3058, 3143, 3229, 3315, 3400, 3487, 3573, 3660, 3747, 3834, 3921,
			4009, 4097, 4185, 4274, 4363, 4452, 4542, 4632, 4723, 4813, 4905, 4996, 5088, 5181, 5274, 5367,
			5461, 5555, 5650, 5745, 5841, 5938, 6035, 6132, 6231, 6330, 6429, 6529, 6630, 6732, 6834, 6937,
			7041, 7146, 7252, 7358, 7466, 7574, 7684, 7795, 7906, 8019, 8133, 8248, 8365, 8483, 8602, 8723,
			8845, 8969, 9095, 9223, 9352, 9484, 9617, 9753, 9892, 10033, 10177, 10324, 10474, 10628, 10785, 10946,
			11112, 11283, 11460, 11642, 11831, 12028, 12233, 12449, 12676, 12918, 13177, 13459, 13769, 14121, 14537, 15079,
			16384
		};

		/// <summary>The arccosine beside it (seg063 +0x864, 129 words): floor(acos(i / 128) *
		/// 0x8000 / pi).</summary>
		public static readonly short[] ArcCosineTable =
		{
			16384, 16302, 16221, 16139, 16057, 15976, 15894, 15813, 15731, 15650, 15568, 15486, 15404, 15322, 15240, 15158,
			15076, 14994, 14912, 14829, 14747, 14664, 14582, 14499, 14416, 14333, 14250, 14167, 14083, 14000, 13916, 13832,
			13748, 13664, 13579, 13495, 13410, 13325, 13240, 13154, 13068, 12983, 12896, 12810, 12723, 12636, 12549, 12462,
			12374, 12286, 12198, 12109, 12020, 11931, 11841, 11751, 11660, 11570, 11478, 11387, 11295, 11202, 11109, 11016,
			10922, 10828, 10733, 10638, 10542, 10445, 10348, 10251, 10152, 10053, 9954, 9854, 9753, 9651, 9549, 9446,
			9342, 9237, 9131, 9025, 8917, 8809, 8699, 8588, 8477, 8364, 8250, 8135, 8018, 7900, 7781, 7660,
			7538, 7414, 7288, 7160, 7031, 6899, 6766, 6630, 6491, 6350, 6206, 6059, 5909, 5755, 5598, 5437,
			5271, 5100, 4923, 4741, 4552, 4355, 4150, 3934, 3707, 3465, 3206, 2924, 2614, 2262, 1846, 1304,
			0
		};

		/// <summary>The largest sine the arcsine branch takes, sin 45 degrees in 1.15.</summary>
		public const int ArcSineLimit = 0x5A82;

		/// <summary>
		/// The original's table arctangent (MaybeGetTangent_seg019_EFB, cAtan2, which runs
		/// MaybeTangent_seg019_BD4; UnderworldExhumed IMATH.ASM): the angle of a unit vector given
		/// as its sine and cosine in 1.15 (0x7FFF is 1.0, -0x8000 is -1.0), 0 to 0xFFFF for the
		/// full turn, 0 along the cosine, 0x4000 along the sine.
		///
		/// Where the sine is small (up to sin 45 degrees either way) the arcsine of the sine,
		/// mirrored into the other half when the cosine is negative; otherwise the arccosine of the
		/// cosine, with the sine's sign. Each table entry is looked up by the high byte of the
		/// magnitude and interpolated linearly by the low byte, in 16-bit arithmetic.
		/// </summary>
		public static int Atan2(int piSine, int piCosine)
		{
			int liSine = (short)piSine;
			int liCosine = (short)piCosine;
			int liAngle;

			if (liSine <= ArcSineLimit && liSine >= -ArcSineLimit)
			{
				liAngle = fLookUp(ArcSineTable, liSine < 0 ? -liSine : liSine);

				if (liSine < 0)
					liAngle = -liAngle;

				if (liCosine < 0)
					liAngle = 0x8000 - liAngle;
			}
			else
			{
				liAngle = fLookUp(ArcCosineTable, liCosine < 0 ? -liCosine : liCosine);

				if (liCosine < 0)
					liAngle = 0x8000 - liAngle;

				if (liSine < 0)
					liAngle = -liAngle;
			}

			return liAngle & 0xFFFF;
		}

		/// <summary>A table entry for a magnitude of 0 to 0x8000: the entry of the high byte, plus
		/// the difference to the next one times the low byte, shifted down by eight (the original's
		/// imul, taking bits 8 to 23) - all of it a signed 16-bit word.</summary>
		private static int fLookUp(short[] psTable, int piMagnitude)
		{
			int liHigh = piMagnitude >> 8;
			int liLow = piMagnitude & 0xFF;
			int liBase = psTable[liHigh];

			// 0x8000 itself reads the word after the table, but always times a low byte of zero.
			if (liLow == 0)
				return liBase;

			int liStep = (short)(psTable[liHigh + 1] - liBase);

			return (short)(((liStep * liLow) >> 8) + liBase);
		}
	}
}
