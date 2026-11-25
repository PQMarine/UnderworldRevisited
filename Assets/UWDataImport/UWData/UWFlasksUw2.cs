using System.Collections.Generic;
using UWDataImport.Presentation;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S FLASK AS ONE PICTURE (2026-11-23, for the modern interface's host and the colour page's
	/// preview; per user: the preview's mana flask was wrong on the second game - it went through UWFlasks, which
	/// is the first game's layout of FLASKS.GR). The classic screen draws a flask as set_flask does (uw2
	/// ui/PANELS.C:115): the empty flask and over it a slice for each twelfth filled, each slice's top at its
	/// liquid_y row. Here the same in the picture's palette indices, so it goes through the colour help as every
	/// other picture does.
	///
	/// THE FLASK STANDS FREE: the black round it (the colour of the empty flask's top-left pixel, reached from the
	/// picture's edges) becomes index 0, and the slices, which carry the same black, do not draw there. The strip of
	/// stone under the stand stays as the flask's foot.
	///
	/// THE HATCH while poisoned (UWUserSettings.MarkPoison, the colour help's switch for a red-green deficiency)
	/// is the first game's: every third diagonal of the liquid in UWFlasks' hatch colour.
	/// </summary>
	public static class UWFlasksUw2
	{
		/// <summary>The flask at piLevel twelfths of the liquid whose first slice is piFirst (UWClassicLayoutUw2.Flasks:
		/// HealthFirst, PoisonFirst, ManaFirst), null without the pictures; piBubbleFrame a bubble picture over the liquid's
		/// top (UWFlaskGauge, 1 to 11) or -1.</summary>
		public static UWTexture Compose(UWTextures pOTextures, int piFirst, int piLevel, bool pbHatch, int piBubbleFrame = -1)
		{
			UWTexture lOEmpty = fPicture(pOTextures, UWClassicLayoutUw2.Flasks.Empty);

			if (lOEmpty == null)
				return null;

			int liWidth = lOEmpty.Width;
			int liHeight = lOEmpty.Height;
			byte[] lyFlask = (byte[])lOEmpty.PaletteIndices.Clone();
			bool[] lbCleared = fClearBackdrop(lyFlask, liWidth, liHeight);
			int liLevel = System.Math.Max(0, System.Math.Min(UWClassicLayoutUw2.Flasks.Levels, piLevel));

			for (int liSlice = 0; liSlice < liLevel; liSlice++)
			{
				UWTexture lOSlice = fPicture(pOTextures, piFirst + liSlice);

				if (lOSlice == null)
					continue;

				int liTop = SliceTop(liSlice);

				for (int liRow = 0; liRow < lOSlice.Height; liRow++)
				{
					int liTo = liTop + liRow;

					if (liTo < 0 || liTo >= liHeight)
						continue;

					for (int liColumn = 0; liColumn < lOSlice.Width && liColumn < liWidth; liColumn++)
					{
						int liFrom = (liRow * lOSlice.Width) + liColumn;
						int liAt = (liTo * liWidth) + liColumn;

						if (liFrom >= lOSlice.PaletteIndices.Length || lOSlice.PaletteIndices[liFrom] == 0 || lbCleared[liAt])
							continue;

						lyFlask[liAt] = pbHatch && IsHatched(liColumn, liTo) ? UWFlasks.HatchColour : lOSlice.PaletteIndices[liFrom];
					}
				}
			}

			if (piBubbleFrame > 0 && liLevel > 0)
				fDrawBubbles(pOTextures, lyFlask, lbCleared, liWidth, liHeight, piFirst, liLevel, piBubbleFrame);

			UWTexture lOFlask = new UWTexture(lOEmpty);

			lOFlask.ReplaceIndices(0, lyFlask);

			return lOFlask;
		}

		/// <summary>adjust_flasks' bubbles at the liquid's top (UWClassicLayoutUw2.Flasks.BubbleOffset): the bubble picture
		/// opaque over BubbleRows rows, the glass picture's same rows drawn transparent over it; the black round the flask
		/// stays clear.</summary>
		private static void fDrawBubbles(UWTextures pOTextures, byte[] pyFlask, bool[] pbCleared, int piWidth, int piHeight, int piFirst,
			int piLevel, int piFrame)
		{
			UWTexture lOBubble = fPicture(pOTextures, piFirst + UWClassicLayoutUw2.Flasks.BubbleOffset + piFrame);
			UWTexture lOGlass = fPicture(pOTextures, UWClassicLayoutUw2.Flasks.BubbleGlass);
			int liTop = SliceTop(piLevel - 1);

			for (int liRow = 0; liRow < UWClassicLayoutUw2.Flasks.BubbleRows; liRow++)
			{
				int liTo = liTop + liRow;

				if (liTo < 0 || liTo >= piHeight)
					continue;

				for (int liColumn = 0; liColumn < UWClassicLayoutUw2.Flasks.BubbleWidth && liColumn < piWidth; liColumn++)
				{
					int liAt = (liTo * piWidth) + liColumn;

					if (pbCleared[liAt])
						continue;

					if (lOBubble != null && liRow < lOBubble.Height && liColumn < lOBubble.Width)
						pyFlask[liAt] = lOBubble.PaletteIndices[(liRow * lOBubble.Width) + liColumn];

					if (lOGlass != null && liTo < lOGlass.Height && liColumn < lOGlass.Width)
					{
						byte lyGlass = lOGlass.PaletteIndices[(liTo * lOGlass.Width) + liColumn];

						if (lyGlass != 0)
							pyFlask[liAt] = lyGlass;
					}
				}
			}
		}

		/// <summary>A slice's top row under the empty flask's top: liquid_y counts up from the screen's bottom.</summary>
		public static int SliceTop(int piSlice)
		{
			return (UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Flasks.LiquidY[piSlice + 1]) - UWClassicLayoutUw2.Flasks.Top;
		}

		/// <summary>Whether the hatch lies on a pixel of the flask (its column and its row from the flask's top).</summary>
		public static bool IsHatched(int piColumn, int piRow)
		{
			return (piColumn + piRow) % UWFlasks.HatchSpacing == 0;
		}

		/// <summary>
		/// A slice for the classic screen, which draws the slices one by one: the slice as it is, with the hatch on
		/// its liquid. The liquid is what a slice draws inside the glass - where the empty flask is not the black
		/// round it.
		/// </summary>
		public static UWTexture HatchedSlice(UWTextures pOTextures, int piFirst, int piSlice)
		{
			UWTexture lOEmpty = fPicture(pOTextures, UWClassicLayoutUw2.Flasks.Empty);
			UWTexture lOSlice = fPicture(pOTextures, piFirst + piSlice);

			if (lOEmpty == null || lOSlice == null)
				return lOSlice;

			int liWidth = lOEmpty.Width;
			int liHeight = lOEmpty.Height;
			bool[] lbCleared = fClearBackdrop((byte[])lOEmpty.PaletteIndices.Clone(), liWidth, liHeight);
			byte[] lySlice = (byte[])lOSlice.PaletteIndices.Clone();
			int liTop = SliceTop(piSlice);

			for (int liRow = 0; liRow < lOSlice.Height; liRow++)
			{
				int liTo = liTop + liRow;

				if (liTo < 0 || liTo >= liHeight)
					continue;

				for (int liColumn = 0; liColumn < lOSlice.Width && liColumn < liWidth; liColumn++)
				{
					int liFrom = (liRow * lOSlice.Width) + liColumn;

					if (liFrom < lySlice.Length && lySlice[liFrom] != 0 && !lbCleared[(liTo * liWidth) + liColumn] && IsHatched(liColumn, liTo))
						lySlice[liFrom] = UWFlasks.HatchColour;
				}
			}

			UWTexture lOHatched = new UWTexture(lOSlice);

			lOHatched.ReplaceIndices(0, lySlice);

			return lOHatched;
		}

		/// <summary>A composed flask as upload bytes (RGBA, the bottom row first) through the colour help's filter,
		/// index 0 clear.</summary>
		public static byte[] ToRgba(UWTexture pOFlask, UWColourFilter pOFilter)
		{
			byte[] lyRgba = UWUploadPixels.FromTexture(pOFlask, pOFilter);
			byte[] lyIndices = pOFlask.PaletteIndices;
			int liWidth = pOFlask.Width;
			int liHeight = pOFlask.Height;

			for (int liRow = 0; liRow < liHeight; liRow++)
			{
				for (int liColumn = 0; liColumn < liWidth; liColumn++)
				{
					int liIndex = (liRow * liWidth) + liColumn;
					bool lbClear = liIndex < lyIndices.Length && lyIndices[liIndex] == 0;

					lyRgba[((((liHeight - 1 - liRow) * liWidth) + liColumn) * UWUploadPixels.BytesPerPixel) + 3] = lbClear ? (byte)0 : (byte)255;
				}
			}

			return lyRgba;
		}

		private static UWTexture fPicture(UWTextures pOTextures, int piId)
		{
			if (pOTextures == null)
				return null;

			try
			{
				UWTexture lOSource = pOTextures.GetTextureByType(UWTexture.TextureTypes.FLASKS, piId);

				return lOSource != null && lOSource.PaletteIndices != null && lOSource.Width > 0 && lOSource.Height > 0
					&& lOSource.PaletteIndices.Length >= lOSource.Width * lOSource.Height ? lOSource : null;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>Floods the black round the flask from the picture's edges and sets it to index 0; the answer says
		/// which pixels went, rows from the top.</summary>
		private static bool[] fClearBackdrop(byte[] pyIndices, int piWidth, int piHeight)
		{
			bool[] lbSeen = new bool[piWidth * piHeight];
			byte lyBack = pyIndices[0];
			Stack<int> lOOpen = new Stack<int>();

			for (int liColumn = 0; liColumn < piWidth; liColumn++)
			{
				lOOpen.Push(liColumn);
				lOOpen.Push(((piHeight - 1) * piWidth) + liColumn);
			}

			for (int liRow = 0; liRow < piHeight; liRow++)
			{
				lOOpen.Push(liRow * piWidth);
				lOOpen.Push((liRow * piWidth) + piWidth - 1);
			}

			while (lOOpen.Count > 0)
			{
				int liAt = lOOpen.Pop();

				if (lbSeen[liAt] || pyIndices[liAt] != lyBack)
					continue;

				lbSeen[liAt] = true;

				int liRow = liAt / piWidth;
				int liColumn = liAt % piWidth;

				if (liColumn > 0) lOOpen.Push(liAt - 1);
				if (liColumn < piWidth - 1) lOOpen.Push(liAt + 1);
				if (liRow > 0) lOOpen.Push(liAt - piWidth);
				if (liRow < piHeight - 1) lOOpen.Push(liAt + piWidth);
			}

			for (int liAt = 0; liAt < lbSeen.Length; liAt++)
			{
				if (lbSeen[liAt])
					pyIndices[liAt] = 0;
			}

			return lbSeen;
		}
	}
}
