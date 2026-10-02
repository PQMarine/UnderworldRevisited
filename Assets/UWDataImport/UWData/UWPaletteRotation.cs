namespace UWDataImport.UWData
{
	/// <summary>
	/// The palette rotation the original uses to move lava and water (uw-formats.txt
	/// 3.1.4). What is animated there is not the texture but palette 0: certain
	/// colour ranges travel through the palette, and every texture that uses these indices
	/// moves along with them.
	///
	/// Literally from the documentation:
	///   Indices 16 to 23 - lava/fire. The first five rotate together, the
	///   next three together. The colours travel UPWARDS, i.e. from low to
	///   high indices.
	///   Indices 48 to 63 - water. Four groups of four each, each group on its own. The
	///   colours travel DOWNWARDS, from high to low indices.
	///
	/// This class only converts indices; it knows neither Unity nor textures.
	/// </summary>
	public static class UWPaletteRotation
	{
		public const int LavaFirstIndex = 16;

		public const int LavaLastIndex = 23;

		/// <summary>First lava group: indices 16 to 20.</summary>
		public const int LavaGroupOneLength = 5;

		/// <summary>Second lava group: indices 21 to 23.</summary>
		public const int LavaGroupTwoLength = 3;

		public const int WaterFirstIndex = 48;

		public const int WaterLastIndex = 63;

		public const int WaterGroupLength = 4;

		/// <summary>After this many steps the lava is back to how it started - the
		/// least common multiple of the two group lengths 5 and 3.</summary>
		public const int LavaCycleLength = 15;

		/// <summary>All water groups are four long; after four steps the cycle is
		/// closed.</summary>
		public const int WaterCycleLength = 4;

		public static bool IsLavaIndex(int piPaletteIndex)
		{
			return piPaletteIndex >= LavaFirstIndex && piPaletteIndex <= LavaLastIndex;
		}

		public static bool IsWaterIndex(int piPaletteIndex)
		{
			return piPaletteIndex >= WaterFirstIndex && piPaletteIndex <= WaterLastIndex;
		}

		public static bool IsRotatingIndex(int piPaletteIndex)
		{
			return IsLavaIndex(piPaletteIndex) || IsWaterIndex(piPaletteIndex);
		}

		/// <summary>
		/// Which palette index is visible at position piPaletteIndex in step piStep.
		/// Indices outside the two ranges stay unchanged.
		/// </summary>
		public static int GetSourceIndex(int piPaletteIndex, int piStep)
		{
			if (IsLavaIndex(piPaletteIndex))
			{
				bool lbFirstGroup = piPaletteIndex < LavaFirstIndex + LavaGroupOneLength;

				int liGroupStart = lbFirstGroup
					? LavaFirstIndex
					: LavaFirstIndex + LavaGroupOneLength;

				int liGroupLength = lbFirstGroup ? LavaGroupOneLength : LavaGroupTwoLength;

				// Upwards: the colour at this position came from further down.
				return liGroupStart + fWrap(piPaletteIndex - liGroupStart - piStep, liGroupLength);
			}

			if (IsWaterIndex(piPaletteIndex))
			{
				int liOffset = piPaletteIndex - WaterFirstIndex;
				int liGroupStart = WaterFirstIndex + ((liOffset / WaterGroupLength) * WaterGroupLength);

				// Downwards: the colour at this position came from further up.
				return liGroupStart + fWrap(piPaletteIndex - liGroupStart + piStep, WaterGroupLength);
			}

			return piPaletteIndex;
		}

		/// <summary>
		/// How many distinct states a texture that uses these ranges has. Neither
		/// of the two ranges used means no state change (returns 1).
		/// </summary>
		public static int GetCycleLength(bool pbUsesLava, bool pbUsesWater)
		{
			if (pbUsesLava && pbUsesWater)
				return LavaCycleLength * WaterCycleLength / fGreatestCommonDivisor(LavaCycleLength, WaterCycleLength);

			if (pbUsesLava)
				return LavaCycleLength;

			if (pbUsesWater)
				return WaterCycleLength;

			return 1;
		}

		private static int fWrap(int piValue, int piLength)
		{
			int liResult = piValue % piLength;

			return liResult < 0 ? liResult + piLength : liResult;
		}

		private static int fGreatestCommonDivisor(int piLeft, int piRight)
		{
			while (piRight != 0)
			{
				int liTemp = piRight;
				piRight = piLeft % piRight;
				piLeft = liTemp;
			}

			return piLeft;
		}
	}
}
