using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE GERMAN LETTERS IN THE ORIGINAL FONTS (translations wave 1, 2026-10-26; decided by the user
	/// the same day: the classic interface gets new pixel glyphs for a-, o-, u-umlaut, their capitals
	/// and the sharp s in the style of each original font, built at run time - the player's FONT*.SYS
	/// stay untouched). The six fonts hold ASCII only (32 to 126 at most; FONT4X5P and FONTBUTN have no
	/// lowercase), so the new glyphs go beyond the font's own (UWFont.AddGlyph), under their Unicode
	/// code - which is their Latin-1 byte as well.
	///
	/// THE UMLAUTS are the font's own a o u A O U with two dots:
	///   - THE DOT is the ink of the font's full stop (one pixel in the small fonts, two by two in
	///     FONTBIG and FONTCHAR); a font whose full stop is empty (FONTBUTN) takes one row as wide as
	///     the stem of its I.
	///   - ONE BLANK ROW between dots and letter where the font has the room above the letter. Where it
	///     has not, the letter gives up rows that repeat the row above them (a straight stretch: the
	///     u's stems, the O's sides; the longest stretch first), at most a third of its rows, so the
	///     gap still fits. Else the dots sit right on the letter where there is room for them alone. A
	///     capital that reaches the font's top gives up the rows that differ least from a neighbour
	///     (repeated rows first, the middle one on a tie, the upper of two): for dots and blank row
	///     where a third of its rows suffice, else for the dots alone. The letter's foot (its
	///     baseline) stays where it is.
	///   - THE DOTS stand over the outer ends of the letter's top row, or of its top two rows when the
	///     top row is too narrow for two dots and a blank column between them; one column further in
	///     where there is room for that and two blank columns. Where the dots sit right on the letter,
	///     the placement that touches fewer of its pixels from above is taken - and where even that
	///     rests half the dots on it (the U's stems would only grow taller), a straight stretch may
	///     give up to half the letter's rows for the blank row after all.
	/// THE SHARP S is drawn on its own per font (msSharpS), on the height of the font's b and h, in
	/// its stroke: no font of the game has a letter it could be built from. The fonts without lowercase
	/// get none, as they draw no a, o or u either.
	///
	/// Nothing of the game's data is kept in the project: the self-check pins only the hashes of the
	/// results (Tools/UWSelfCheck/Pins/font-glyph-hashes.txt).
	/// </summary>
	public static class UWFontGlyphBuilder
	{
		/// <summary>The letters built, in the order of the previews and the pins.</summary>
		public static readonly char[] Characters = { '\u00E4', '\u00F6', '\u00FC', '\u00C4', '\u00D6', '\u00DC', '\u00DF' };

		/// <summary>The umlauts and the letters they are built from.</summary>
		private static readonly char[] msUmlauts = { '\u00E4', '\u00F6', '\u00FC', '\u00C4', '\u00D6', '\u00DC' };

		private static readonly char[] msBases = { 'a', 'o', 'u', 'A', 'O', 'U' };

		public const char SharpS = '\u00DF';

		/// <summary>
		/// THE SHARP S per font, drawn for the purpose (not from the game): '#' a pixel. Each as tall as
		/// its font and as wide as its b, the stem where the font's b and h have theirs, the top like
		/// their ascender (FONTBIG's flag, FONTCHAR's hook), the lower bow ending in a stroke that
		/// leaves the stem free; the italic one leans like the italic B.
		/// </summary>
		private static readonly Dictionary<UWFonts.FontType, string[]> msSharpS = new Dictionary<UWFonts.FontType, string[]>
		{
			{
				UWFonts.FontType.Normal, new[]
				{
					".##..",
					"#..#.",
					"#.#..",
					"#..#.",
					"#.##.",
					"....."
				}
			},
			{
				UWFonts.FontType.Italic, new[]
				{
					"..##.",
					".#..#",
					".#.#.",
					"#...#",
					"#.##.",
					".....",
					"....."
				}
			},
			{
				UWFonts.FontType.Big, new[]
				{
					".####....",
					"##..##...",
					"##...##..",
					"##...##..",
					"##..##...",
					"##.##....",
					"##..##...",
					"##...##..",
					"##....##.",
					"##....##.",
					"##.####..",
					".........",
					".........",
					".........",
					"........."
				}
			},
			{
				UWFonts.FontType.CharacterGeneration, new[]
				{
					"........",
					"..###...",
					".##..#..",
					".##..#..",
					".##.##..",
					".##..##.",
					".##..##.",
					"##.##...",
					"........",
					"........"
				}
			}
		};

		/// <summary>Adds the German letters to a loaded font (see the class comment); how many were
		/// added.</summary>
		public static int AddGerman(UWFont pOFont, UWFonts.FontType peType)
		{
			if (pOFont == null || !pOFont.IsLoaded)
				return 0;

			int liAdded = 0;

			for (int liAt = 0; liAt < msUmlauts.Length; liAt++)
			{
				if (TryBuildUmlaut(pOFont, msBases[liAt], out UWFont.Glyph lOGlyph) && pOFont.AddGlyph(msUmlauts[liAt], lOGlyph))
					liAdded++;
			}

			if (TryBuildSharpS(pOFont, peType, out UWFont.Glyph lOSharpS) && pOFont.AddGlyph(SharpS, lOSharpS))
				liAdded++;

			return liAdded;
		}

		/// <summary>The sharp s of this font (msSharpS), or false for a font without lowercase or
		/// whose height is not the drawing's.</summary>
		public static bool TryBuildSharpS(UWFont pOFont, UWFonts.FontType peType, out UWFont.Glyph pOGlyph)
		{
			pOGlyph = default;

			if (pOFont == null || !pOFont.IsLoaded || !fHasInk(pOFont, 'a') || !msSharpS.TryGetValue(peType, out string[] lsRows)
				|| lsRows.Length != pOFont.Height)
				return false;

			int liWidth = lsRows[0].Length;
			byte[] lyPixels = new byte[liWidth * pOFont.Height];

			for (int y = 0; y < lsRows.Length; y++)
			{
				if (lsRows[y].Length != liWidth)
					throw new InvalidOperationException("UWFontGlyphBuilder: the sharp s of " + peType + " has rows of different lengths");

				for (int x = 0; x < liWidth; x++)
					lyPixels[(y * liWidth) + x] = lsRows[y][x] == '#' ? (byte)1 : (byte)0;
			}

			pOGlyph = new UWFont.Glyph { Width = liWidth, Pixels = lyPixels };
			return true;
		}

		/// <summary>The umlaut over a letter of the font (see the class comment), or false when the
		/// font has no such letter.</summary>
		public static bool TryBuildUmlaut(UWFont pOFont, char pcBase, out UWFont.Glyph pOGlyph)
		{
			pOGlyph = default;

			if (pOFont == null || !pOFont.IsLoaded || !pOFont.TryGetGlyph(pcBase, out UWFont.Glyph lOBase) || !fHasInk(lOBase))
				return false;

			byte[][] lyRows = fRows(lOBase, pOFont.Height);
			int liTop = fTopRow(lyRows);
			int liBody = fBottomRow(lyRows) - liTop + 1;
			byte[][] lyDot = fDot(pOFont);
			int liDotHeight = lyDot.Length;
			byte[][] lyDropped;

			// The room above the letter: a blank row and the dots - rows the letter repeats may give
			// way for it -, else the dots alone; a capital at the font's top gives way rows that
			// differ least, for the blank row too where a third of its rows suffice.
			if (liTop >= liDotHeight + 1)
				return fCompose(lyRows, lOBase.Width, lyDot, 1, out pOGlyph) >= 0;

			if ((lyDropped = fDropRepeated(lyRows, liDotHeight + 1 - liTop, liBody / 3)) != null)
				return fCompose(lyDropped, lOBase.Width, lyDot, 1, out pOGlyph) >= 0;

			if (liTop >= liDotHeight)
				lyDropped = lyRows;
			else if (liDotHeight + 1 - liTop <= liBody / 3)
				return fCompose(fDropNearest(lyRows, liDotHeight + 1 - liTop, liBody / 3), lOBase.Width, lyDot, 1, out pOGlyph) >= 0;
			else
				lyDropped = fDropRepeated(lyRows, liDotHeight - liTop, liBody / 3) ?? fDropNearest(lyRows, liDotHeight - liTop, liBody / 3);

			int liTouches = fCompose(lyDropped, lOBase.Width, lyDot, 0, out pOGlyph);

			// Dots resting with half their foot or more on the letter merge with it (the U's stems
			// grow taller instead): then a straight stretch may give up to half the letter's rows
			// for the blank row.
			if (liTouches * 2 >= fFootPixels(lyDot) && (lyDropped = fDropRepeated(lyRows, liDotHeight + 1 - liTop, liBody / 2)) != null)
				fCompose(lyDropped, lOBase.Width, lyDot, 1, out pOGlyph);

			return true;
		}

		/// <summary>
		/// The letter's rows with the dots laid on, piGap blank rows above its top (as many as fit);
		/// how many of the dots' pixels rest right on the letter's top row (0 with a gap).
		/// </summary>
		private static int fCompose(byte[][] pyRows, int piBaseWidth, byte[][] pyDot, int piGap, out UWFont.Glyph pOGlyph)
		{
			int liHeight = pyRows.Length;
			int liTop = fTopRow(pyRows);
			int liDotHeight = pyDot.Length;
			int liDotWidth = pyDot[0].Length;
			int liDotTop = Math.Max(0, liTop - piGap - liDotHeight);

			// The dots' columns: over the top row, or the top two rows where the top row is too
			// narrow; sitting right on the letter, the placement touching fewer of its pixels.
			fExtent(pyRows[liTop], out int liLeft0, out int liRight0);

			int liLeft1 = liLeft0, liRight1 = liRight0;

			if (liTop + 1 < liHeight && fExtent(pyRows[liTop + 1], out int liNextLeft, out int liNextRight))
			{
				liLeft1 = Math.Min(liLeft0, liNextLeft);
				liRight1 = Math.Max(liRight0, liNextRight);
			}

			bool lbTopWide = liRight0 - liLeft0 + 1 >= (2 * liDotWidth) + 1;
			fPlaceDots(lbTopWide ? liLeft0 : liLeft1, lbTopWide ? liRight0 : liRight1, liDotWidth, out int liFirst, out int liSecond);

			int liTouches = 0;

			if (liDotTop + liDotHeight == liTop)
			{
				fPlaceDots(lbTopWide ? liLeft1 : liLeft0, lbTopWide ? liRight1 : liRight0, liDotWidth, out int liOtherFirst, out int liOtherSecond);

				liTouches = fTouches(pyRows[liTop], pyDot, liFirst, liSecond);

				int liOtherTouches = fTouches(pyRows[liTop], pyDot, liOtherFirst, liOtherSecond);

				if (liOtherTouches < liTouches)
				{
					liFirst = liOtherFirst;
					liSecond = liOtherSecond;
					liTouches = liOtherTouches;
				}
			}

			int liWidth = Math.Max(piBaseWidth, liSecond + liDotWidth);
			byte[] lyPixels = new byte[liWidth * liHeight];

			for (int y = 0; y < liHeight; y++)
			{
				for (int x = 0; x < piBaseWidth; x++)
					lyPixels[(y * liWidth) + x] = pyRows[y][x];
			}

			foreach (int liDotLeft in new[] { liFirst, liSecond })
			{
				for (int y = 0; y < liDotHeight; y++)
				{
					for (int x = 0; x < liDotWidth; x++)
					{
						if (pyDot[y][x] != 0 && liDotTop + y < liHeight && liDotLeft + x >= 0)
							lyPixels[((liDotTop + y) * liWidth) + liDotLeft + x] = 1;
					}
				}
			}

			pOGlyph = new UWFont.Glyph { Width = liWidth, Pixels = lyPixels };
			return liTouches;
		}

		/// <summary>The pixels of the dots' bottom row, both dots.</summary>
		private static int fFootPixels(byte[][] pyDot)
		{
			int liPixels = 0;

			foreach (byte lyPixel in pyDot[pyDot.Length - 1])
			{
				if (lyPixel != 0)
					liPixels += 2;
			}

			return liPixels;
		}

		/// <summary>The two dots' left columns over the columns piLeft to piRight: at the ends, one
		/// column in where two blank columns stay between them, at least one blank column apart.</summary>
		private static void fPlaceDots(int piLeft, int piRight, int piDotWidth, out int piFirst, out int piSecond)
		{
			piFirst = piLeft;
			piSecond = piRight - piDotWidth + 1;

			if (piRight - piLeft + 1 >= (2 * piDotWidth) + 4)
			{
				piFirst++;
				piSecond--;
			}

			if (piSecond - piFirst < piDotWidth + 1)
				piSecond = piFirst + piDotWidth + 1;
		}

		/// <summary>How many of the dots' bottom pixels stand right above a pixel of the row.</summary>
		private static int fTouches(byte[] pyRow, byte[][] pyDot, int piFirst, int piSecond)
		{
			int liTouches = 0;
			byte[] lyBottom = pyDot[pyDot.Length - 1];

			foreach (int liLeft in new[] { piFirst, piSecond })
			{
				for (int x = 0; x < lyBottom.Length; x++)
				{
					int liAt = liLeft + x;

					if (lyBottom[x] != 0 && liAt >= 0 && liAt < pyRow.Length && pyRow[liAt] != 0)
						liTouches++;
				}
			}

			return liTouches;
		}

		/// <summary>The font's dot (see the class comment): rows of its pixels, at least one.</summary>
		private static byte[][] fDot(UWFont pOFont)
		{
			if (pOFont.TryGetGlyph('.', out UWFont.Glyph lOStop) && fHasInk(lOStop))
			{
				byte[][] lyRows = fRows(lOStop, pOFont.Height);
				int liTop = fTopRow(lyRows);
				int liBottom = fBottomRow(lyRows);
				int liLeft = int.MaxValue, liRight = -1;

				for (int y = liTop; y <= liBottom; y++)
				{
					if (fExtent(lyRows[y], out int liRowLeft, out int liRowRight))
					{
						liLeft = Math.Min(liLeft, liRowLeft);
						liRight = Math.Max(liRight, liRowRight);
					}
				}

				byte[][] lyDot = new byte[liBottom - liTop + 1][];

				for (int y = 0; y < lyDot.Length; y++)
				{
					lyDot[y] = new byte[liRight - liLeft + 1];
					Array.Copy(lyRows[liTop + y], liLeft, lyDot[y], 0, lyDot[y].Length);
				}

				return lyDot;
			}

			// No full stop: one row as wide as the I's stem (its narrowest run of pixels).
			int liStem = int.MaxValue;

			if (pOFont.TryGetGlyph('I', out UWFont.Glyph lOI) && fHasInk(lOI))
			{
				foreach (byte[] lyRow in fRows(lOI, pOFont.Height))
				{
					int liRun = 0;

					for (int x = 0; x <= lyRow.Length; x++)
					{
						if (x < lyRow.Length && lyRow[x] != 0)
						{
							liRun++;
							continue;
						}

						if (liRun > 0)
							liStem = Math.Min(liStem, liRun);

						liRun = 0;
					}
				}
			}

			byte[] lyOne = new byte[liStem == int.MaxValue ? 1 : liStem];

			for (int x = 0; x < lyOne.Length; x++)
				lyOne[x] = 1;

			return new[] { lyOne };
		}

		/// <summary>The rows with piCount rows dropped that each repeat the row above them, or null when
		/// there are not that many (or more than piMost). A dropped row moves everything above it one
		/// row down; the most repeated stretch gives first.</summary>
		private static byte[][] fDropRepeated(byte[][] pyRows, int piCount, int piMost)
		{
			if (piCount > piMost)
				return null;

			byte[][] lyRows = pyRows;

			for (int liDrop = 0; liDrop < piCount; liDrop++)
			{
				int liTop = fTopRow(lyRows);
				int liBottom = fBottomRow(lyRows);
				int liBest = -1, liBestRun = 0;

				for (int y = liTop + 1; y <= liBottom; y++)
				{
					if (!fSame(lyRows[y], lyRows[y - 1]))
						continue;

					// The length of the stretch of equal rows this one belongs to.
					int liFrom = y - 1;
					int liTo = y;

					while (liFrom > liTop && fSame(lyRows[liFrom - 1], lyRows[y]))
						liFrom--;

					while (liTo < liBottom && fSame(lyRows[liTo + 1], lyRows[y]))
						liTo++;

					if (liTo - liFrom + 1 > liBestRun)
					{
						liBestRun = liTo - liFrom + 1;
						liBest = y;
					}
				}

				if (liBest < 0)
					return null;

				lyRows = fDropRow(lyRows, liBest);
			}

			return lyRows;
		}

		/// <summary>The rows with up to piCount rows dropped, each the inner row that differs least
		/// from a neighbour (repeated rows count as differing by nothing); the middle one on a tie,
		/// the upper of two equally near it. At most piMost.</summary>
		private static byte[][] fDropNearest(byte[][] pyRows, int piCount, int piMost)
		{
			byte[][] lyRows = pyRows;

			for (int liDrop = 0; liDrop < Math.Min(piCount, piMost); liDrop++)
			{
				int liTop = fTopRow(lyRows);
				int liBottom = fBottomRow(lyRows);
				int liBest = -1, liBestDifference = int.MaxValue, liBestDistance = int.MaxValue;

				for (int y = liTop + 1; y < liBottom; y++)
				{
					int liDifference = Math.Min(fDifference(lyRows[y], lyRows[y - 1]), fDifference(lyRows[y], lyRows[y + 1]));
					// Twice the distance to the middle, so a middle between two rows stays whole.
					int liDistance = Math.Abs((2 * y) - (liTop + liBottom));

					if (liDifference < liBestDifference || (liDifference == liBestDifference && liDistance < liBestDistance))
					{
						liBest = y;
						liBestDifference = liDifference;
						liBestDistance = liDistance;
					}
				}

				if (liBest < 0)
					break;

				lyRows = fDropRow(lyRows, liBest);
			}

			return lyRows;
		}

		/// <summary>The rows without row piRow: everything above it one row down, a blank row on top.</summary>
		private static byte[][] fDropRow(byte[][] pyRows, int piRow)
		{
			byte[][] lyRows = new byte[pyRows.Length][];

			lyRows[0] = new byte[pyRows[0].Length];

			for (int y = 1; y <= piRow; y++)
				lyRows[y] = pyRows[y - 1];

			for (int y = piRow + 1; y < pyRows.Length; y++)
				lyRows[y] = pyRows[y];

			return lyRows;
		}

		private static byte[][] fRows(UWFont.Glyph pOGlyph, int piHeight)
		{
			byte[][] lyRows = new byte[piHeight][];

			for (int y = 0; y < piHeight; y++)
			{
				lyRows[y] = new byte[pOGlyph.Width];
				Array.Copy(pOGlyph.Pixels, y * pOGlyph.Width, lyRows[y], 0, pOGlyph.Width);
			}

			return lyRows;
		}

		private static bool fHasInk(UWFont pOFont, char pcChar)
		{
			return pOFont.TryGetGlyph(pcChar, out UWFont.Glyph lOGlyph) && fHasInk(lOGlyph);
		}

		private static bool fHasInk(UWFont.Glyph pOGlyph)
		{
			if (pOGlyph.Width <= 0 || pOGlyph.Pixels == null)
				return false;

			foreach (byte lyPixel in pOGlyph.Pixels)
			{
				if (lyPixel != 0)
					return true;
			}

			return false;
		}

		private static int fTopRow(byte[][] pyRows)
		{
			for (int y = 0; y < pyRows.Length; y++)
			{
				if (fExtent(pyRows[y], out _, out _))
					return y;
			}

			return -1;
		}

		private static int fBottomRow(byte[][] pyRows)
		{
			for (int y = pyRows.Length - 1; y >= 0; y--)
			{
				if (fExtent(pyRows[y], out _, out _))
					return y;
			}

			return -1;
		}

		/// <summary>The first and last column with a pixel; false for a blank row.</summary>
		private static bool fExtent(byte[] pyRow, out int piLeft, out int piRight)
		{
			piLeft = -1;
			piRight = -1;

			for (int x = 0; x < pyRow.Length; x++)
			{
				if (pyRow[x] == 0)
					continue;

				if (piLeft < 0)
					piLeft = x;

				piRight = x;
			}

			return piLeft >= 0;
		}

		private static bool fSame(byte[] pyA, byte[] pyB)
		{
			return fDifference(pyA, pyB) == 0;
		}

		private static int fDifference(byte[] pyA, byte[] pyB)
		{
			int liDifference = 0;

			for (int x = 0; x < pyA.Length; x++)
			{
				if ((pyA[x] != 0) != (pyB[x] != 0))
					liDifference++;
			}

			return liDifference;
		}
	}
}
