using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE STONE SHELF as a deco panel, quilted and grown from the ledge (MAIN.BYT; cleanup 9.11,
	/// 2026-10-25: a builder of its own, UWHudArt forwards to it). Build reads MAIN.BYT, Compose works
	/// on the frame picture given.
	/// </summary>
	public static class UWStoneShelfBuilder
	{
		// ------------------------------------------------- The stone shelf (MAIN.BYT)

		/// <summary>The narrowest stone shelf: its two ends and a piece.</summary>
		public const int StoneShelfMinWidth = 24;

		/// <summary>The seeds of the pieces' sequence and of the band's (UWSeededRandom).</summary>
		private const uint StonePieceSeed = 0x2545F491u;

		private const uint StoneBandSeed = 0x9E3779B9u;

		/// <summary>
		/// THE STONE SHELF AS A DECO PANEL (per user, 2026-10-09: "to bring over some of the
		/// original's look we need deco panels - the stone shelf freely placed and sized, without a
		/// function; one can put the power gem or the flasks on it as one likes"). The first try
		/// tiled the 12 columns between the flasks and repeated the same patterns too often (per
		/// user); the second put random pieces of the whole ledge side by side, and the user marked
		/// where they met (2026-10-10): the top rows' dither broke (two dark or two lit pixels side by
		/// side) and the brightness jumped. Now:
		///   - the WHOLE LEDGE under the flasks is the source (81 columns), the flasks' places filled
		///     with the plain middle's stone: their bluish glass, the gold feet, the frame's wood and,
		///     on the top surface (rows 150 to 156), the feet's black outline;
		///   - the shelf is put together from PIECES of it, 10 to 18 columns long, each laid 3 columns
		///     over the shelf so far ("image quilting"): of all pieces (also mirrored) that keep the
		///     dither's checkerboard (source and shelf column of the same parity) one that matches
		///     the overlap closely is picked by a fixed seeded sequence (not the one just used), and
		///     the seam runs through the overlap along the path of the least difference, row by row;
		///   - the ledge's right end (6 columns) at the right, joined the same way, and mirrored at
		///     the left. The same shelf every time; a wider one only grows on the right.
		/// No flood from the border: the rows are all stone, with the original's dithered top edge.
		/// </summary>
		public static UWPicture Build(UWTextures pOTextures, int piWidth)
		{
			return Compose(UWMainFrame.Picture(pOTextures), piWidth);
		}

		public static UWPicture Compose(UWPicture pOMain, int piWidth)
		{
			UWPicture lOLedge = pOMain.Crop(UWMainFrame.StoneLedge);
			int liSourceWidth = lOLedge.Width;
			int liRows = lOLedge.Height;
			UWMask lOStone = new UWMask(liSourceWidth, liRows);

			for (int y = 0; y < liRows; y++)
			{
				for (int x = 0; x < liSourceWidth; x++)
				{
					UWColor32 lOColour = lOLedge.Get(x, y);
					int liSum = UWPixelRules.Sum(lOColour);
					bool lbHole = liSum < UWPixelRules.OutlineBlackSum
						? y >= UWHudTuning.StoneShelf.HoleTop && y <= UWHudTuning.StoneShelf.HoleBottom
						: lOColour.B > lOColour.R + UWHudTuning.StoneShelf.HoleBlueOverRed || Math.Abs(lOColour.R - lOColour.G) > UWHudTuning.StoneShelf.HoleSpread
							|| Math.Abs(lOColour.G - lOColour.B) > UWHudTuning.StoneShelf.HoleSpread;

					// The back edge's dark dither shade (28/28/36) is bluish too, but stone.
					lOStone[x, y] = !lbHole || (y < UWHudTuning.StoneShelf.DitherShadeRows && liSum >= UWPixelRules.OutlineBlackSum
						&& liSum < UWHudTuning.StoneShelf.DitherShadeSum);

					if (!lbHole)
						continue;

					int liFrom = UWMainFrame.StoneMiddleX
						+ (((x - UWMainFrame.StoneMiddleX) % UWMainFrame.StoneMiddleWidth) + UWMainFrame.StoneMiddleWidth) % UWMainFrame.StoneMiddleWidth;

					lOLedge.Set(x, y, lOLedge.Get(liFrom, y));
				}
			}

			// All opaque: the frame picture's black (palette index 0) comes transparent, and on the
			// stone it is the original's dark specks.
			UWPixelOps.MakeOpaque(lOLedge);

			int liWidth = Math.Max(StoneShelfMinWidth, piWidth);
			UWPicture lOShelf = new UWPicture(liWidth, liRows);
			UWSeededRandom lORandom = new UWSeededRandom(StonePieceSeed);

			// The left end: the ledge's right end mirrored (the ledge is 81 columns wide, so the
			// mirrored columns keep their parity).
			for (int x = 0; x < UWMainFrame.StoneCapWidth; x++)
			{
				for (int y = 0; y < liRows; y++)
					lOShelf.Set(x, y, lOLedge.Get(liSourceWidth - 1 - x, y));
			}

			int liFilled = UWMainFrame.StoneCapWidth;
			int liEnd = liWidth - UWMainFrame.StoneCapWidth;
			int liLastStart = -UWHudTuning.StoneShelf.PieceMin;
			bool lbLastMirrored = false;
			List<int> lOCandidates = new List<int>();
			List<long> lOCosts = new List<long>();

			while (liFilled < liEnd)
			{
				int liLength = UWHudTuning.StoneShelf.PieceMin + lORandom.Below(UWHudTuning.StoneShelf.PieceRange);
				int liAt = liFilled - UWHudTuning.StoneShelf.Overlap;

				lOCandidates.Clear();
				lOCosts.Clear();

				for (int liStart = UWMainFrame.StoneCapWidth; liStart + liLength <= liSourceWidth - UWMainFrame.StoneCapWidth; liStart++)
				{
					for (int liMirror = 0; liMirror < 2; liMirror++)
					{
						bool lbMirrored = liMirror == 1;
						int liFirst = lbMirrored ? liStart + liLength - 1 : liStart;

						if (((liFirst - liAt) & 1) != 0
							|| (lbMirrored == lbLastMirrored && Math.Abs(liStart - liLastStart) < UWHudTuning.StoneShelf.PieceMin))
							continue;

						long llCost = 0;

						for (int k = 0; k < UWHudTuning.StoneShelf.Overlap; k++)
						{
							int liSource = lbMirrored ? liFirst - k : liFirst + k;

							for (int y = 0; y < liRows; y++)
								llCost += UWPixelRules.Distance(lOShelf.Get(liAt + k, y), lOLedge.Get(liSource, y));
						}

						lOCandidates.Add((liStart * 2) + liMirror);
						lOCosts.Add(llCost);
					}
				}

				// Any of the close matches, so the same overlap does not always call the same piece.
				long llBest = long.MaxValue;

				foreach (long llCost in lOCosts)
					llBest = Math.Min(llBest, llCost);

				long llLimit = llBest + (llBest / UWHudTuning.StoneShelf.CloseMatchDivisor);
				int liClose = 0;

				foreach (long llCost in lOCosts)
					liClose += llCost <= llLimit ? 1 : 0;

				int liPick = lORandom.Below(liClose);
				int liChosen = 0;

				for (int i = 0; i < lOCosts.Count; i++)
				{
					if (lOCosts[i] <= llLimit && liPick-- == 0)
					{
						liChosen = lOCandidates[i];
						break;
					}
				}

				liLastStart = liChosen / 2;
				lbLastMirrored = (liChosen & 1) != 0;

				fQuiltPiece(lOShelf, lOLedge, liAt, lbLastMirrored ? liLastStart + liLength - 1 : liLastStart,
					lbLastMirrored ? -1 : 1, liLength);
				liFilled = liAt + liLength;
			}

			// The right end as the ledge has it, with the overlap before it; for a shelf whose width
			// has the other parity than the ledge's the ledge's last column is left off.
			int liCapFirst = liSourceWidth - UWMainFrame.StoneCapWidth - UWHudTuning.StoneShelf.Overlap - ((liSourceWidth - liWidth) & 1);

			fQuiltPiece(lOShelf, lOLedge, liEnd - UWHudTuning.StoneShelf.Overlap, liCapFirst, 1, UWHudTuning.StoneShelf.Overlap + UWMainFrame.StoneCapWidth);
			fSynthesiseBand(lOShelf, lOLedge, lOStone);

			return lOShelf;
		}

		/// <summary>
		/// Grows the upper band between the shelf's ends anew pixel by pixel (texture synthesis by
		/// neighbourhood): column by column from the left, each column from the bottom up, every
		/// pixel takes the ledge pixel of the same row (and column parity, for the dither) whose
		/// surroundings - the pixels already in place within two columns and rows - match best,
		/// one of the three best by a fixed seeded sequence of its own. So the band joins the
		/// pieces below and the ends seamlessly, recombines the little stone there is without a
		/// period, and a wider shelf still only grows on the right. Only the ledge's REAL stone
		/// (pOStone) is drawn from and compared with: the first version also took the flasks'
		/// places, filled with copies of the middle, and so grew the middle's pattern again and
		/// again (per user, 2026-10-10: "in the upper part one clearly sees the pattern repeat").
		/// </summary>
		private static void fSynthesiseBand(UWPicture pOShelf, UWPicture pOLedge, UWMask pOStone)
		{
			int liWidth = pOShelf.Width;
			int liRows = pOShelf.Height;
			int liSourceWidth = pOLedge.Width;
			UWMask lOKnown = new UWMask(liWidth, liRows);
			UWSeededRandom lORandom = new UWSeededRandom(StoneBandSeed);
			int[] liColumns = new int[Math.Max(0, liSourceWidth - 4)];

			for (int x = 0; x < liWidth; x++)
			{
				for (int y = 0; y < liRows; y++)
					lOKnown[x, y] = y >= UWMainFrame.StoneBandRows || x < UWMainFrame.StoneCapWidth || x >= liWidth - UWMainFrame.StoneCapWidth;
			}

			// The ledge's columns but the two at each side (their surroundings would leave it); at least
			// four pixels compared.
			for (int liAt = 0; liAt < liColumns.Length; liAt++)
				liColumns[liAt] = 2 + liAt;

			UWTextureSynth.GrowFrom(pOShelf, lOKnown, UWMainFrame.StoneCapWidth, liWidth - UWMainFrame.StoneCapWidth, 0, UWMainFrame.StoneBandRows,
				0, liRows, pOLedge, pOStone, 0, liColumns, 4, lORandom);
		}

		/// <summary>Lays piLength source columns (from piFirst, in piStep direction) onto the shelf
		/// at piAt; in the first UWHudTuning.StoneShelf.Overlap columns it takes the new pixels only
		/// from the path of
		/// the least difference on (one column per row, moving at most one column from row to
		/// row).</summary>
		private static void fQuiltPiece(UWPicture pOShelf, UWPicture pOLedge, int piAt, int piFirst, int piStep, int piLength)
		{
			int liRows = pOShelf.Height;
			long[,] llPath = new long[liRows, UWHudTuning.StoneShelf.Overlap];

			for (int y = 0; y < liRows; y++)
			{
				for (int k = 0; k < UWHudTuning.StoneShelf.Overlap; k++)
				{
					long llAbove = 0;

					if (y > 0)
					{
						llAbove = llPath[y - 1, k];

						if (k > 0)
							llAbove = Math.Min(llAbove, llPath[y - 1, k - 1]);

						if (k < UWHudTuning.StoneShelf.Overlap - 1)
							llAbove = Math.Min(llAbove, llPath[y - 1, k + 1]);
					}

					llPath[y, k] = llAbove + UWPixelRules.Distance(pOShelf.Get(piAt + k, y), pOLedge.Get(piFirst + (k * piStep), y));
				}
			}

			int liSeam = 0;

			for (int k = 1; k < UWHudTuning.StoneShelf.Overlap; k++)
			{
				if (llPath[liRows - 1, k] < llPath[liRows - 1, liSeam])
					liSeam = k;
			}

			for (int y = liRows - 1; y >= 0; y--)
			{
				for (int k = liSeam; k < piLength && piAt + k < pOShelf.Width; k++)
					pOShelf.Set(piAt + k, y, pOLedge.Get(piFirst + (k * piStep), y));

				if (y == 0)
					break;

				int liNext = liSeam;

				if (liSeam > 0 && llPath[y - 1, liSeam - 1] < llPath[y - 1, liNext])
					liNext = liSeam - 1;

				if (liSeam < UWHudTuning.StoneShelf.Overlap - 1 && llPath[y - 1, liSeam + 1] < llPath[y - 1, liNext])
					liNext = liSeam + 1;

				liSeam = liNext;
			}
		}
	}
}
