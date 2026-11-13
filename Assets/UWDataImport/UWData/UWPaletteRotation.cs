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
			return fFirstGame && piPaletteIndex >= LavaFirstIndex && piPaletteIndex <= LavaLastIndex;
		}

		public static bool IsWaterIndex(int piPaletteIndex)
		{
			return fFirstGame && piPaletteIndex >= WaterFirstIndex && piPaletteIndex <= WaterLastIndex;
		}

		/// <summary>
		/// LAVA AND WATER ARE THE FIRST GAME'S (UW2 plan phase 2 step 3, the second test of the walk,
		/// 2026-10-29: UW2's walls glittered orange in Remastered - its colours 16 to 23 are no lava): what
		/// glows and shines as lava or water (UWLookupTables.MaterialLookup, UWRemasterLights) asks these two,
		/// and nothing does in a second game's session. WHAT ROTATES is IsUpIndex and IsDownIndex, by game.
		/// </summary>
		private static bool fFirstGame => UWGameSession.Current.Game.Game == UWGame.Uw1;

		// ------------------------------------------------- What rotates, by game

		/// <summary>
		/// THE SECOND GAME'S BANKS (cycle_colors, Underworld Exhumed uw2 gfx/COLCYCLE.C:49; UW2 plan G2, built
		/// 2026-11-13 for the runes, per user: the flam and tym runes move in the original): rotate_bank(3, 5, 1)
		/// and rotate_bank(8, 3, 1) move 3 to 7 and 8 to 10 one place up, rotate_bank(0xE0..0xEC, 4, 0) the four
		/// banks of four at 0xE0 to 0xEF one place down - the first game's shape (a five and a three going up as
		/// its lava, four fours going down as its water) at other places of the palette. It rotates whenever the
		/// time byte's bit 6 changes, every 64 ticks, the first game's four steps a second.
		/// </summary>
		public const int SecondGameUpFirstIndex = 3;

		public const int SecondGameDownFirstIndex = 0xE0;

		private static int fUpFirst => fFirstGame ? LavaFirstIndex : SecondGameUpFirstIndex;

		private static int fDownFirst => fFirstGame ? WaterFirstIndex : SecondGameDownFirstIndex;

		/// <summary>An index of the banks that travel UPWARDS: the first game's lava, the second game's 3 to 10.</summary>
		public static bool IsUpIndex(int piPaletteIndex)
		{
			int liFirst = fUpFirst;

			return piPaletteIndex >= liFirst && piPaletteIndex < liFirst + LavaGroupOneLength + LavaGroupTwoLength;
		}

		/// <summary>An index of the banks that travel DOWNWARDS: the first game's water, the second game's 0xE0 to 0xEF.</summary>
		public static bool IsDownIndex(int piPaletteIndex)
		{
			int liFirst = fDownFirst;

			return piPaletteIndex >= liFirst && piPaletteIndex < liFirst + (4 * WaterGroupLength);
		}

		public static bool IsRotatingIndex(int piPaletteIndex)
		{
			return IsUpIndex(piPaletteIndex) || IsDownIndex(piPaletteIndex);
		}

		/// <summary>
		/// Which palette index is visible at position piPaletteIndex in step piStep.
		/// Indices outside the two ranges stay unchanged.
		/// </summary>
		public static int GetSourceIndex(int piPaletteIndex, int piStep)
		{
			if (IsUpIndex(piPaletteIndex))
			{
				int liUpFirst = fUpFirst;
				bool lbFirstGroup = piPaletteIndex < liUpFirst + LavaGroupOneLength;

				int liGroupStart = lbFirstGroup
					? liUpFirst
					: liUpFirst + LavaGroupOneLength;

				int liGroupLength = lbFirstGroup ? LavaGroupOneLength : LavaGroupTwoLength;

				// Upwards: the colour at this position came from further down.
				return liGroupStart + fWrap(piPaletteIndex - liGroupStart - piStep, liGroupLength);
			}

			if (IsDownIndex(piPaletteIndex))
			{
				int liDownFirst = fDownFirst;
				int liOffset = piPaletteIndex - liDownFirst;
				int liGroupStart = liDownFirst + ((liOffset / WaterGroupLength) * WaterGroupLength);

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
