using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// CLASSIC WIDE: the original's frame pulled to a wider screen (MAIN.BYT, COMPASS.GR; cleanup
	/// 9.11, 2026-10-25: a builder of its own, UWHudArt forwards to it). Build reads the pictures,
	/// Compose works on the frame picture and the compass's disc picture given.
	/// </summary>
	public static class UWClassicWideBuilder
	{
		// ------------------------------------------------- The classic frame widened (Classic Wide)

		/// <summary>
		/// THE ORIGINAL'S FRAME (MAIN.BYT) PULLED TO A WIDER SCREEN, "Classic Wide" (per user,
		/// 2026-10-10, the name theirs): the picture cut at fixed places and columns put in, half of
		/// them left of the middle, half right, so the left command column, the right panel with
		/// the flasks, the gargoyle, the compass, the rune hollow and both side bars (the dragons
		/// wind round them, UWHudDragons) keep their look, and the view's hole, the ledge and the
		/// message scroll grow. The cuts, by band of rows:
		///   - the top bar (rows 0 to 18) at x 78 and 198: in the plain bar only, between the
		///     corner ornaments and the gargoyle's claws;
		///   - the view (rows 19 to 127): the hole, nothing to draw;
		///   - the ledge (rows 128 to 167) at x 100 (the left stone, right of the active spell
		///     icons' places, x 52 to 99, left of the compass base's outline at 104) and at x 173
		///     (between the compass base's outline and the rune hollow: the hollow, the right
		///     dragon's head lying over its right end and the bar move together - the first cut
		///     right of the hollow would have split the head, per user);
		///   - the message scroll (rows 168 to 199) at x 82 and 223, the dark paper being uniform.
		/// THE COLUMNS PUT IN ARE GROWN PIXEL BY PIXEL as the stone shelf's band (fSynthesiseBand):
		/// every pixel takes a pixel of the same row from the band's plain source (the left stone,
		/// x 56 to 103; the bar, x 72 to 82 and 194 to 203; the paper, x 40 to 109 and 170 to 229)
		/// whose surroundings match the pixels already in place best - one of the three best by a
		/// seeded sequence -, of the same column parity. The RUST MARKS are left out of the source
		/// (a whole wood row, the ledge's edges, stays): the first mockups copied blocks with their
		/// rust, and the eye caught every shape that came twice (per user, 2026-10-10: "the frame
		/// still has repeating spots - one sees them at once"); grown stone has no period and no
		/// shape to recognise. The picture's black is opaque (the screen is black there); only the
		/// hole is transparent.
		/// </summary>
		public sealed class ClassicWideFrame
		{
			/// <summary>The widened picture, (320 + Extra) x 200.</summary>
			public UWPicture Picture;

			/// <summary>The columns put in, and how many of them left of the middle part.</summary>
			public int Extra;

			public int Left;

			/// <summary>The view's hole in the widened picture.</summary>
			public UWRectInt Hole;
		}

		/// <summary>Where an original pixel column lies in a frame widened by piExtra columns (half of
		/// them left of the middle): unchanged left of its band's first cut, half of piExtra further
		/// between the cuts, piExtra further right of the second cut.</summary>
		public static int ClassicWideShiftX(int piX, int piY, int piExtra)
		{
			int liBand = piY < ClassicWideTopRows ? 0 : piY < ClassicWideLedgeTop ? 1 : piY < ClassicWideScrollTop ? 2 : 3;

			return piX < msClassicWideCuts[liBand, 0] ? 0 : piX < msClassicWideCuts[liBand, 1] ? piExtra / 2 : piExtra;
		}

		/// <summary>The bands' first rows (the top bar from row 0) and the view's hole
		/// (UWClassicLayout.ClassicWide).</summary>
		public const int ClassicWideTopRows = UWClassicLayout.ClassicWide.TopRows;

		public const int ClassicWideLedgeTop = UWClassicLayout.ClassicWide.LedgeTop;

		public const int ClassicWideScrollTop = UWClassicLayout.ClassicWide.ScrollTop;

		public static readonly UWRectInt ClassicWideHole = UWClassicLayout.ClassicWide.Hole;

		/// <summary>The camera's area in the classic scheme (UWGameUI: x 50 to 226, rows 17 to 138),
		/// the hole's flood bounded by it.</summary>
		public const int ClassicWideCameraLeft = UWClassicLayout.Frame.WindowHoleLeft;

		public const int ClassicWideCameraRight = UWClassicLayout.Frame.WindowHoleRight;

		public const int ClassicWideCameraTop = UWClassicLayout.Frame.WindowHoleTop;

		public const int ClassicWideCameraBottom = UWClassicLayout.Frame.WindowHoleBottom;

		/// <summary>The two cuts of each band (top bar, view, ledge, scroll): UWClassicLayout.ClassicWide.Cuts.</summary>
		private static readonly int[,] msClassicWideCuts = UWClassicLayout.ClassicWide.Cuts;

		/// <summary>A band's first (0) or second (1) cut, by a row from the top.</summary>
		public static int ClassicWideCut(int piRow, int piWhich)
		{
			int liBand = piRow < ClassicWideTopRows ? 0 : piRow < ClassicWideLedgeTop ? 1 : piRow < ClassicWideScrollTop ? 2 : 3;

			return msClassicWideCuts[liBand, piWhich];
		}

		/// <summary>The seed of the grown columns' sequence, one for all bands (UWSeededRandom).</summary>
		private const uint ClassicWideSeed = 0xC0FFEE11u;

		public static ClassicWideFrame Build(UWTextures pOTextures, int piExtra)
		{
			UWTexture lOCompassDisc = pOTextures.GetTextureByType(UWTexture.TextureTypes.COMPASS, 0);

			return Compose(UWMainFrame.Picture(pOTextures), lOCompassDisc != null ? UWPicture.From(lOCompassDisc) : null, piExtra);
		}

		/// <summary>The widened frame from the frame picture and the compass's disc picture
		/// (COMPASS.GR 0; null: the baked needle stays).</summary>
		public static ClassicWideFrame Compose(UWPicture pOMain, UWPicture pOCompassDisc, int piExtra)
		{
			UWPicture lOMain = pOMain;
			int liExtra = Math.Max(0, piExtra);
			int liLeft = liExtra / 2;
			int liWidth = lOMain.Width + liExtra;
			int liHeight = lOMain.Height;
			UWPicture lOOut = new UWPicture(liWidth, liHeight);
			UWMask lOKnown = new UWMask(liWidth, liHeight);
			int[] liBandTops = { 0, ClassicWideTopRows, ClassicWideLedgeTop, ClassicWideScrollTop, liHeight };
			UWSeededRandom lORandom = new UWSeededRandom(ClassicWideSeed);

			for (int liBand = 0; liBand < 4; liBand++)
			{
				int liY0 = liBandTops[liBand];
				int liY1 = liBandTops[liBand + 1];
				int liCutLeft = msClassicWideCuts[liBand, 0];
				int liCutRight = msClassicWideCuts[liBand, 1];

				for (int y = liY0; y < liY1; y++)
				{
					for (int x = 0; x < lOMain.Width; x++)
					{
						int liTo = x + (x < liCutLeft ? 0 : x < liCutRight ? liLeft : liExtra);

						lOOut.Set(liTo, y, lOMain.Get(x, y));
						lOKnown[liTo, y] = true;
					}
				}

				int[] liSources = UWMainFrame.ClassicWideSources[liBand];

				if (liSources.Length == 0)
					continue;

				fGrowClassicWideStrip(lOOut, lOKnown, lOMain, liCutLeft, liLeft, liY0, liY1, liSources, lORandom);
				fGrowClassicWideStrip(lOOut, lOKnown, lOMain, liCutRight + liLeft, liExtra - liLeft, liY0, liY1, liSources, lORandom);
			}

			// The compass without its needle: MAIN.BYT has the north needle baked in, which the
			// classic scheme covers with the disc pictures (COMPASS.GR 0 to 3, UWHudCompass); the
			// compass element lies on it with its own (per user's screenshot, 2026-10-10: "one sees
			// the baked needle").
			if (pOCompassDisc != null)
				lOOut.Overlay(pOCompassDisc,
					UWClassicLayout.Compass.Left + ClassicWideShiftX(UWClassicLayout.Compass.Left, UWClassicLayout.Compass.Top, liExtra),
					UWClassicLayout.Compass.Top);

			// The hole: the black reached from the view's middle through black, within the camera's
			// area (the classic scheme's, x 50 to 226 and rows 17 to 138, widened) - not the frame's
			// black outlines on the ledge, which a plain rect would take. All else opaque.
			UWRectInt lOHole = new UWRectInt(ClassicWideHole.X, ClassicWideHole.Y, ClassicWideHole.Width + liExtra, ClassicWideHole.Height);
			int liStart = ((lOHole.Y + (lOHole.Height / 2)) * liWidth) + lOHole.X + (lOHole.Width / 2);
			UWMask lOHoleMask = UWMask.Flood(liWidth, liHeight, new[] { liStart }, (piX, piY) =>
				piX >= ClassicWideCameraLeft && piX < ClassicWideCameraRight + liExtra && piY >= ClassicWideCameraTop && piY < ClassicWideCameraBottom
				&& UWPixelRules.IsEdgeBlack(lOOut.Get(piX, piY)));

			UWPixelOps.Clear(lOOut, lOHoleMask);
			UWPixelOps.MakeOpaque(lOOut, lOHoleMask);

			return new ClassicWideFrame { Picture = lOOut, Extra = liExtra, Left = liLeft, Hole = lOHole };
		}

		/// <summary>
		/// Grows piCount columns at piAt of the widened picture, rows piY0 to piY1, from the
		/// original's source ranges: column by column, each from the bottom up, every pixel the
		/// source pixel of the same row and column parity whose known surroundings (within two
		/// columns and rows, the rust left out) match best - one of the three best.
		/// </summary>
		private static void fGrowClassicWideStrip(UWPicture pOOut, UWMask pOKnown, UWPicture pOMain, int piAt, int piCount, int piY0, int piY1,
			int[] piSources, UWSeededRandom pORandom)
		{
			if (piCount <= 0)
				return;

			List<int> lOColumns = new List<int>();

			for (int liRange = 0; liRange < piSources.Length; liRange += 2)
			{
				for (int x = piSources[liRange]; x < piSources[liRange + 1]; x++)
					lOColumns.Add(x);
			}

			// The source's mask: the rust left out, except in rows that are mostly rust (the
			// ledge's wooden edges) - there everything counts.
			int liRows = piY1 - piY0;
			UWMask lOStone = new UWMask(pOMain.Width, liRows);

			for (int y = 0; y < liRows; y++)
			{
				int liRust = 0;

				foreach (int x in lOColumns)
					liRust += UWPixelRules.IsRust(pOMain.Get(x, piY0 + y)) ? 1 : 0;

				bool lbWoodRow = liRust * 10 > lOColumns.Count * UWHudTuning.ClassicWide.WoodRowTenths;

				foreach (int x in lOColumns)
					lOStone[x, y] = lbWoodRow || !UWPixelRules.IsRust(pOMain.Get(x, piY0 + y));
			}

			// Only the source's columns are set in the mask, so a neighbour outside the ranges is never
			// compared; at least four pixels compared.
			UWTextureSynth.GrowFrom(pOOut, pOKnown, piAt, piAt + piCount, piY0, piY1, piY0, piY1, pOMain, lOStone, piY0, lOColumns.ToArray(), 4,
				pORandom);
		}
	}
}
