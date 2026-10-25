using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MODERN MESSAGE SCROLL'S PAPER (MAIN.BYT, SCRLEDGE.GR; cleanup 9.16, 2026-10-25: moved
	/// engine-free out of UWModernScroll.fBuildPaper, a builder like the others). Build reads the
	/// pictures, Compose works on the frame picture and the two edge pictures given; UWModernScroll
	/// keeps the texture, the text and where the scroll goes, and the colour help is applied when the
	/// picture is uploaded (UWUploadPixels), as for the other builders' pictures.
	///
	/// The frame picture (MAIN.BYT) only draws the scroll's OUTLINE - the wooden rollers at both
	/// ends with their knobs, the rims above and below; the paper between them the game fills with
	/// one colour, #8C6854 (UWHudMessageLog). So the picture here is the frame's rows 167-199 over
	/// its whole width, the frame's dark ground around the ends left transparent, the paper filled -
	/// and TALLER on request (per user, 2026-10-09: "extending makes sense"): a row of the rollers'
	/// plain middle (183) is repeated, and the rolling edges (SCRLEDGE.GR, 4x29 at x 11 and 306) get
	/// the same rows in their middle.
	/// </summary>
	public static class UWScrollPaperBuilder
	{
		/// <summary>The frame picture's rows of the scroll, the rim's top to the bottom. Row 166 above
		/// it belongs to the frame hanging over the scroll - the view's wooden bar and the compass's
		/// pedestal (per user, 2026-10-09: "at the top edge some of the UI still shows").</summary>
		public const int SourceTop = 167;

		public const int SourceRows = 33;

		/// <summary>Where the pedestal's outline still lies on the rim's top row (x 131 to 145): the rim
		/// there is taken from the same row this far to the left.</summary>
		private const int PedestalLeft = 131;

		private const int PedestalRight = 145;

		private const int PedestalPatch = 15;

		/// <summary>The upper rods' knobs (rows 167 to 175, x 0-8 and 310-319) lie under the shelves'
		/// dithered shadow; they are drawn as the lower knobs, this many rows down, which are clean
		/// and of the same shape.</summary>
		private const int UpperKnobLast = 175;

		private const int KnobLeftEnd = 8;

		private const int KnobRightStart = 310;

		private const int LowerKnobOffset = 22;

		/// <summary>The frame's dark ground around the scroll's ends (0/0/4) - not black, which outlines
		/// the rods' knobs (per user: the scroll was not freed cleanly; the ground stood as dark boxes
		/// around the knobs while their black outline, palette index 0, was taken for transparent).</summary>
		private static bool fIsGround(UWColor32 pOColour)
		{
			return pOColour.R == 0 && pOColour.G == 0 && pOColour.B == 4;
		}

		/// <summary>The row of the plain middle that is repeated to make it taller.</summary>
		private const int RepeatRow = 183;

		/// <summary>The paper (the classic box): from x 15, rows 169-198.</summary>
		public const int PaperLeft = 15;

		public const int PaperTop = 169;

		public const int PaperWidth = 291;

		private const int PaperRows = 30;

		private const int EdgeLeftX = 11;

		private const int EdgeRightX = 306;

		/// <summary>The edge strips' row repeated for a taller scroll - their middle.</summary>
		private const int EdgeRepeatRow = 14;

		/// <summary>The paper's colour, as the game fills the classic box (UWHudMessageLog).</summary>
		public static readonly UWColor32 Paper = new UWColor32(0x8C, 0x68, 0x54, 255);

		/// <summary>
		/// The scroll's picture, piExtra rows taller than the frame's (fewer for a negative one), the
		/// edges at their frame piEdgeFrame (UWScrollEdgeRules). pOWideFrame: CLASSIC WIDE's widened
		/// frame picture, whose own bottom rows are taken - the right knobs, the right edge and the
		/// pedestal's patch lie where its parts moved; null: MAIN.BYT itself. Null when MAIN.BYT is
		/// missing or not the frame's size.
		/// </summary>
		public static UWPicture Build(UWTextures pOTextures, UWPicture pOWideFrame, int piExtra, int piEdgeFrame)
		{
			UWPicture lOFrame = pOWideFrame;

			if (lOFrame == null)
			{
				UWTexture lOMain;

				try
				{
					lOMain = pOTextures.GetTextureByType(UWTexture.TextureTypes.MAIN, 0);
				}
				catch
				{
					return null;
				}

				if (lOMain == null || lOMain.Width != UWClassicLayout.Screen.Width || lOMain.Height < SourceTop + SourceRows)
					return null;

				lOFrame = UWMainFrame.Picture(pOTextures);
			}

			return Compose(lOFrame, fEdge(pOTextures, UWScrollEdgeRules.GetMainLeftImage(piEdgeFrame)),
				fEdge(pOTextures, UWScrollEdgeRules.GetMainRightImage(piEdgeFrame)), piExtra);
		}

		/// <summary>An edge picture (SCRLEDGE.GR), null when it is missing or empty.</summary>
		private static UWPicture fEdge(UWTextures pOTextures, int piImage)
		{
			UWTexture lOEdge;

			try
			{
				lOEdge = pOTextures.GetTextureByType(UWTexture.TextureTypes.SCRLEDGE, piImage);
			}
			catch
			{
				return null;
			}

			if (lOEdge == null || lOEdge.Width <= 0 || lOEdge.Height <= 0)
				return null;

			return UWPicture.From(lOEdge);
		}

		/// <summary>
		/// The scroll from the frame picture (MAIN.BYT or Classic Wide's, 320 or more columns wide)
		/// and the left and right edge pictures (null: none laid on): the frame's rows 167-199 over the
		/// full width with the middle row repeated, the ground transparent, everything else opaque,
		/// the paper filled, the edge strips laid on, their middle row repeated as well.
		/// </summary>
		public static UWPicture Compose(UWPicture pOFrame, UWPicture pOLeftEdge, UWPicture pORightEdge, int piExtra)
		{
			int liWidth = pOFrame.Width;
			int liWide = liWidth - UWClassicLayout.Screen.Width;
			int liShift = liWide / 2;
			int liHeight = SourceRows + piExtra;
			UWPicture lOOut = new UWPicture(liWidth, liHeight);

			for (int liRow = 0; liRow < liHeight; liRow++)
			{
				int liSource = fSourceRow(liRow, SourceTop, RepeatRow, piExtra);

				for (int liX = 0; liX < liWidth; liX++)
				{
					int liFrom = liSource == SourceTop && liX >= PedestalLeft + liShift && liX <= PedestalRight + liShift ? liX - PedestalPatch : liX;
					int liFromRow = liSource <= UpperKnobLast && (liX <= KnobLeftEnd || liX >= KnobRightStart + liWide) ? liSource + LowerKnobOffset : liSource;
					UWColor32 lOColour = pOFrame.Get(liFrom, liFromRow);

					if (fIsGround(lOColour))
						continue;

					lOOut.Set(liX, liRow, UWPixelOps.Opaque(lOColour));
				}
			}

			for (int liRow = PaperTop - SourceTop; liRow < PaperTop - SourceTop + PaperRows + piExtra; liRow++)
			{
				for (int liX = PaperLeft; liX < PaperLeft + PaperWidth + liWide; liX++)
					lOOut.Set(liX, liRow, Paper);
			}

			fLayEdge(lOOut, pOLeftEdge, EdgeLeftX, piExtra);
			fLayEdge(lOOut, pORightEdge, EdgeRightX + liWide, piExtra);

			return lOOut;
		}

		/// <summary>Output row to source row: up to the repeated row as they are, then that row for
		/// the extra ones, then the rest shifted.</summary>
		private static int fSourceRow(int piRow, int piTop, int piRepeat, int piExtra)
		{
			int liRepeatAt = piRepeat - piTop;

			if (piRow <= liRepeatAt)
				return piTop + piRow;

			if (piRow <= liRepeatAt + piExtra)
				return piRepeat;

			return piTop + piRow - piExtra;
		}

		/// <summary>Lays an edge strip's visible pixels, opaque, at column piX from the paper's top row,
		/// its middle row repeated piExtra times; what would fall outside the picture is left out.</summary>
		private static void fLayEdge(UWPicture pOOut, UWPicture pOEdge, int piX, int piExtra)
		{
			if (pOEdge == null)
				return;

			int liTop = PaperTop - SourceTop;

			for (int liRow = 0; liRow < pOEdge.Height + piExtra; liRow++)
			{
				int liSource = fSourceRow(liRow, 0, Math.Min(EdgeRepeatRow, pOEdge.Height - 1), piExtra);

				for (int liX = 0; liX < pOEdge.Width; liX++)
				{
					UWColor32 lOPixel = pOEdge.Get(liX, liSource);

					if (lOPixel.A == 0)
						continue;

					// As a run through the rows, as the paper was always laid (a column past the right
					// edge would continue in the next row).
					int liTarget = ((liTop + liRow) * pOOut.Width) + piX + liX;

					if (liTarget >= 0 && liTarget < pOOut.Pixels.Length)
						pOOut.Pixels[liTarget] = UWPixelOps.Opaque(lOPixel);
				}
			}
		}
	}
}
