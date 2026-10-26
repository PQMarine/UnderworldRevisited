using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE COMPASS ON ITS STONE DISC and the compass's cross freed (MAIN.BYT, COMPASS.GR; cleanup
	/// 9.11, 2026-10-25: a builder of its own, UWHudArt forwards to it). Build reads the pictures,
	/// Compose works on the frame picture and the disc picture given.
	/// </summary>
	public static class UWCompassDiscBuilder
	{
		// ------------------------------------------------- The compass on its stone disc (MAIN.BYT, COMPASS.GR)

		/// <summary>The compass on its stone disc: its picture's top left in MAIN.BYT (the cross's
		/// top, row 131, down to the pedestal's foot, row 166; the disc's left to its right edge),
		/// with a pixel of room left, right and below for the outline.</summary>
		public const int CompassDiscX = UWMainFrame.CompassMaskX - 1;

		public const int CompassDiscY = UWClassicLayout.Compass.Top;

		/// <summary>The rows of the disc picture whose stone is the disc's top (MAIN.BYT down to row
		/// 156): the F pixels draw from there only, not from the pedestal's lighter front.</summary>
		private const int CompassDiscTopRows = UWMainFrame.CompassDiscTopRow - CompassDiscY + 1;

		/// <summary>
		/// THE COMPASS ON ITS STONE DISC (Classic+, per user 2026-10-10: the compass freed with its
		/// stone disc, "then we can show the movement arrows on the compass's stone disc"): the
		/// frame's compass pedestal with the disc picture COMPASS.GR 0, freed by UWHudMasks.CompassDisc,
		/// the arrows on its front kept. The cross and the rim under it (F) are grown anew: on the
		/// top edge in the colour of the nearest outline pixel the disc kept, the rest from the
		/// disc's own granite, pixel by pixel - those first that have the most finished neighbours -
		/// each taking the granite pixel (X, on the disc's top, within six rows) whose surroundings
		/// within two pixels match best, one of the three best by a fixed seeded sequence. The
		/// turning cross (UWHudCompass.BuildComposite, freed) is laid on top of it per step.
		/// pbOutline: THE BLACK OUTLINE the original's disc has in the frame, which the mask leaves
		/// out - one pixel all round (per user, 2026-10-10, as an option).
		/// </summary>
		public static UWPicture Build(UWTextures pOTextures, bool pbOutline)
		{
			UWTexture lOStone = pOTextures.GetTextureByType(UWTexture.TextureTypes.COMPASS, 0);

			return Compose(UWMainFrame.Picture(pOTextures), lOStone != null ? UWPicture.From(lOStone) : null, pbOutline);
		}

		/// <summary>The disc from the frame picture and the disc picture (COMPASS.GR 0; null: the
		/// frame's own).</summary>
		public static UWPicture Compose(UWPicture pOMain, UWPicture pOStone, bool pbOutline)
		{
			UWMaskText lOMask = UWHudMasks.CompassDisc;
			int liWidth = lOMask.Width;
			int liHeight = lOMask.Height;
			UWPicture lODisc = pOMain.Crop(lOMask.Place);

			if (pOStone != null)
				lODisc.Overlay(pOStone, UWClassicLayout.Compass.Left - UWMainFrame.CompassMaskX, UWClassicLayout.Compass.Top - CompassDiscY);

			UWMask lOKnown = new UWMask(liWidth, liHeight);
			List<int> lOTodo = new List<int>();
			List<int> lOSources = new List<int>();

			for (int y = 0; y < liHeight; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					char lcMask = lOMask.At(x, y);
					UWColor32 lOColour = lODisc.Get(x, y);

					if (lcMask == '.')
					{
						lODisc.Set(x, y, UWPixelOps.Transparent);
						continue;
					}

					// Opaque: the frame picture's black (palette index 0) comes transparent.
					lODisc.Set(x, y, UWPixelOps.Opaque(lOColour));

					if (lcMask == 'F')
						lOTodo.Add((y * liWidth) + x);
					else
						lOKnown[x, y] = true;
				}
			}

			// The sources: the disc top's granite - not its dark outline at the top edge, not the
			// darker rim, else they spread into the middle as streaks.
			for (int y = 0; y < CompassDiscTopRows; y++)
			{
				for (int x = 0; x < liWidth; x++)
				{
					UWColor32 lOColour = lODisc.Get(x, y);

					if (lOMask.Is(x, y, 'X') && !fCompassDiscEdge(x, y) && UWPixelRules.Sum(lOColour) >= UWHudTuning.Compass.GraniteMin)
						lOSources.Add((y * liWidth) + x);
				}
			}

			// The top edge first: an F pixel with nothing above it takes the colour of the nearest
			// edge pixel the disc kept (its dark outline).
			foreach (int liAt in lOTodo.ToArray())
			{
				int liX = liAt % liWidth;
				int liY = liAt / liWidth;

				if (!fCompassDiscEdge(liX, liY))
					continue;

				int liNearest = -1;

				for (int liDistance = 1; liDistance < liWidth && liNearest < 0; liDistance++)
				{
					foreach (int liSide in new[] { -1, 1 })
					{
						int liSX = liX + (liSide * liDistance);

						for (int liSY = Math.Max(0, liY - 1); liSY <= Math.Min(liHeight - 1, liY + 1) && liNearest < 0; liSY++)
						{
							if (liSX >= 0 && liSX < liWidth && lOMask.Is(liSX, liSY, 'X') && fCompassDiscEdge(liSX, liSY))
								liNearest = (liSY * liWidth) + liSX;
						}
					}
				}

				if (liNearest >= 0)
					lODisc.Set(liX, liY, lODisc.Get(liNearest % liWidth, liNearest / liWidth));

				lOKnown[liX, liY] = true;
				lOTodo.Remove(liAt);
			}

			UWSeededRandom lORandom = new UWSeededRandom(CompassDiscSeed);
			long[] llBest = new long[UWTextureSynth.Best];
			int[] liBest = new int[UWTextureSynth.Best];

			while (lOTodo.Count > 0)
			{
				// The pixels with the most finished neighbours first, an onion from the edge in.
				int liMost = -1;

				foreach (int liAt in lOTodo)
					liMost = Math.Max(liMost, lOKnown.CountAround(liAt % liWidth, liAt / liWidth));

				List<int> lOBatch = lOTodo.FindAll(liAt => lOKnown.CountAround(liAt % liWidth, liAt / liWidth) == liMost);

				foreach (int liAt in lOBatch)
				{
					int liX = liAt % liWidth;
					int liY = liAt / liWidth;
					int liFound = 0;

					foreach (int liSource in lOSources)
					{
						int liSourceX = liSource % liWidth;
						int liSourceY = liSource / liWidth;

						if (Math.Abs(liSourceY - liY) > UWHudTuning.Compass.SourceRows)
							continue;

						// The disc against itself: both neighbourhoods only where the disc is finished; at
						// least three pixels compared.
						if (!UWTextureSynth.MatchCost(lODisc, lOKnown, liX, liY, 0, liHeight, lODisc, lOKnown, 0, liSourceX, liSourceY, 3,
							out long llCost))
							continue;

						UWTextureSynth.PickBest3(llBest, liBest, ref liFound, llCost, liSource);
					}

					if (liFound > 0)
					{
						int liPick = UWTextureSynth.PickOne(liBest, liFound, lORandom);

						lODisc.Set(liX, liY, lODisc.Get(liPick % liWidth, liPick / liWidth));
					}

					lOKnown[liX, liY] = true;
					lOTodo.Remove(liAt);
				}
			}

			// The room for the outline, and the outline: every empty pixel touching the disc, also
			// diagonally.
			UWPicture lOOut = UWPixelOps.Pad(lODisc, UWMainFrame.CompassMaskX - CompassDiscX, 0, 1, 1);

			if (!pbOutline)
				return lOOut;

			UWPicture lOShape = UWPixelOps.Copy(lOOut);

			UWPixelOps.Outline(lOOut, (piX, piY) => lOShape.Get(piX, piY).A == 0,
				(piX, piY) => piX >= 0 && piY >= 0 && piX < lOShape.Width && piY < lOShape.Height && lOShape.Get(piX, piY).A != 0, UWPixelOps.Black);

			return lOOut;
		}

		/// <summary>The seed of the F pixels' sequence (UWSeededRandom).</summary>
		private const uint CompassDiscSeed = 0x5BD1E995u;

		/// <summary>Whether a disc pixel lies on its top edge: nothing of the disc right above it.</summary>
		private static bool fCompassDiscEdge(int piX, int piY)
		{
			return !UWHudMasks.CompassDisc.Is(piX, piY, '.') && (piY == 0 || UWHudMasks.CompassDisc.Is(piX, piY - 1, '.'));
		}

		/// <summary>The easy-movement arrow at a pixel of the disc picture (rows from the top), -1
		/// for none - the original's own click areas (UWClickRules.EasyArrows, y counted from the
		/// bottom of the 200-row screen).</summary>
		public static int CompassDiscArrowAt(int piX, int piY)
		{
			int liX = CompassDiscX + piX;
			int liY = UWClassicLayout.Screen.Height - 1 - (CompassDiscY + piY);

			for (int liArrow = 0; liArrow < UWClickRules.EasyArrows.Length; liArrow++)
			{
				if (UWClickRules.EasyArrows[liArrow].Contains(liX, liY))
					return liArrow;
			}

			return -1;
		}

		// ------------------------------------------------- The compass's cross, freed (COMPASS.GR)

		/// <summary>
		/// THE COMPASS'S CROSS WITHOUT ITS STONE (per user, 2026-10-09: the compass shall stand free in
		/// the modern interface, "the compass has a black edge, except north, which has a red pixel").
		/// The four disc pictures (COMPASS.GR 0-3) are opaque 52x26 and carry the cross in four turns
		/// over the disc's slate rim and granite; what belongs to the cross is the LARGEST connected
		/// piece of WARM pixels (gold and brown: red minus blue over 25) and the PURE BLACK pixels
		/// touching it (the outline is 0/0/0; the stone's darkest greys are 28 to 48). Grey and blue
		/// stone drops out, also inside the rings of the turned crosses, and so do the rust spots on
		/// the rim, which do not touch the cross. Worked out on the user's pictures the same day.
		/// Pixels top-down, as UWTexture.GetUWColor32 gives them; true for the cross.
		/// </summary>
		public static bool[] CompassCrossMask(UWColor32[] pyPixels, int piWidth, int piHeight)
		{
			int liCount = piWidth * piHeight;
			UWMask lOWarm = new UWMask(piWidth, piHeight);

			for (int liAt = 0; liAt < liCount; liAt++)
				lOWarm.Bits[liAt] = pyPixels[liAt].A > 0 && pyPixels[liAt].R - pyPixels[liAt].B > UWHudTuning.Compass.CrossWarm;

			// The largest 4-connected piece (of pieces as large the first found) and the pure black
			// among its eight neighbours.
			UWMask lOBody = lOWarm.Largest(false);
			bool[] lbMask = new bool[liCount];

			for (int liY = 0; liY < piHeight; liY++)
			{
				for (int liX = 0; liX < piWidth; liX++)
				{
					int liAt = (liY * piWidth) + liX;

					lbMask[liAt] = lOBody.Bits[liAt] || (UWPixelRules.IsPureBlack(pyPixels[liAt]) && lOBody.AnyAround(liX, liY));
				}
			}

			return lbMask;
		}

		/// <summary>The needle tips (COMPASS.GR 4-19) are small opaque pictures with the disc's stone
		/// around the red point: only their red and their pure black belong to the tip.</summary>
		public static bool CompassTipKeeps(UWColor32 pOPixel)
		{
			return pOPixel.A > 0 && (UWPixelRules.IsPureBlack(pOPixel)
				|| (pOPixel.R > UWHudTuning.Compass.TipRed && pOPixel.R - pOPixel.G > UWHudTuning.Compass.TipRedOverGreen));
		}
	}
}
