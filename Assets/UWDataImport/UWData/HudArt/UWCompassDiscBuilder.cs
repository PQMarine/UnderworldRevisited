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
			if (pOTextures != null && pOTextures.Game == UWGame.Uw2)
				return BuildUw2(pOTextures, pbOutline);

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

		// ------------------------------------------------- The second game's recess and compass (UW2 plan stage U6 step 1)

		/// <summary>The room round the second game's disc for its outline, a pixel each way.</summary>
		public const int Uw2DiscPad = 1;

		/// <summary>
		/// THE SECOND GAME'S DISC: the middle recess of the marble ledge (UWClassicLayoutUw2.CompassRecess) cut from the
		/// frame by its mask (UWHudMasks.CompassRecessUw2), every pixel opaque (the frame's near black is no clear colour
		/// there, UWMainFrame.Picture), with its cross, gem and needle baked in - the compass's whole picture covers them
		/// (UWCompassGeometry.IsFreedOnDisc false). A pixel of room each way; pbOutline: black there, as the first game's.
		/// </summary>
		public static UWPicture BuildUw2(UWTextures pOTextures, bool pbOutline)
		{
			return ComposeUw2(UWMainFrame.Picture(pOTextures), pbOutline);
		}

		public static UWPicture ComposeUw2(UWPicture pOMain, bool pbOutline)
		{
			UWMaskText lOMask = UWHudMasks.CompassRecessUw2;
			UWPicture lORecess = pOMain.Crop(lOMask.Place);

			UWPixelOps.MakeOpaque(lORecess);
			UWPixelOps.Clear(lORecess, lOMask.Where('.'));

			UWPicture lOOut = UWPixelOps.Pad(lORecess, Uw2DiscPad, Uw2DiscPad, Uw2DiscPad, Uw2DiscPad);

			if (!pbOutline)
				return lOOut;

			UWPicture lOShape = UWPixelOps.Copy(lOOut);

			UWPixelOps.Outline(lOOut, (piX, piY) => lOShape.Get(piX, piY).A == 0,
				(piX, piY) => piX >= 0 && piY >= 0 && piX < lOShape.Width && piY < lOShape.Height && lOShape.Get(piX, piY).A != 0, UWPixelOps.Black);

			return lOOut;
		}

		/// <summary>The second game's compass pictures: the four faces (COMPASS.GR 0 to 3, by step & 3) and the sixteen
		/// needles (4 to 19, by step).</summary>
		private const int Uw2Faces = 4;

		/// <summary>Where a second-game compass picture lies on the screen (rows from the top): the face at 0x5E/0x33,
		/// a needle at nedl_x + 0x78, nedl_y + 0x2B (UWClassicLayoutUw2.Compass, as UWGameScreenUw2.fUpdateCompass).</summary>
		private static void fUw2PictureAt(int piPicture, out int piX, out int piY)
		{
			if (piPicture < Uw2Faces)
			{
				piX = UWClassicLayoutUw2.Compass.Left;
				piY = UWClassicLayoutUw2.Compass.Top;

				return;
			}

			int liStep = piPicture - UWClassicLayoutUw2.Compass.FirstNeedle;

			piX = UWClassicLayoutUw2.Compass.NeedleBaseX + UWClassicLayoutUw2.Compass.NeedleX[liStep];
			piY = UWClassicLayout.Screen.Height - 1 - (UWClassicLayoutUw2.Compass.NeedleBaseY + UWClassicLayoutUw2.Compass.NeedleY[liStep]);
		}

		private static UWTexture fUw2Picture(UWTextures pOTextures, int piPicture)
		{
			try
			{
				UWTexture lOPicture = pOTextures != null ? pOTextures.GetTextureByType(UWTexture.TextureTypes.COMPASS, piPicture) : null;

				return lOPicture != null && lOPicture.Width > 0 && lOPicture.Height > 0 ? lOPicture : null;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>The box all four faces and sixteen needles take together on the screen, so every step's picture has
		/// one size and place (as the first game's UWHudCompass.BuildComposite); empty without the pictures.</summary>
		public static UWRectInt Uw2CompassBox(UWTextures pOTextures)
		{
			int liLeft = int.MaxValue, liTop = int.MaxValue, liRight = int.MinValue, liBottom = int.MinValue;

			for (int liPicture = 0; liPicture < UWClassicLayoutUw2.Compass.FirstNeedle + UWClassicLayoutUw2.Compass.NeedleX.Length; liPicture++)
			{
				UWTexture lOPicture = fUw2Picture(pOTextures, liPicture);

				if (lOPicture == null)
					return new UWRectInt(UWClassicLayoutUw2.Compass.Left, UWClassicLayoutUw2.Compass.Top, 0, 0);

				fUw2PictureAt(liPicture, out int liX, out int liY);
				liLeft = Math.Min(liLeft, liX);
				liTop = Math.Min(liTop, liY);
				liRight = Math.Max(liRight, liX + lOPicture.Width);
				liBottom = Math.Max(liBottom, liY + lOPicture.Height);
			}

			return new UWRectInt(liLeft, liTop, liRight - liLeft, liBottom - liTop);
		}

		/// <summary>
		/// THE SECOND GAME'S COMPASS OF A STEP over the box of all its pictures (Uw2CompassBox): the face of step & 3 and
		/// the step's needle, both opaque as the classic screen draws them; pbFreed: the cross and the needle's tip alone,
		/// by the first game's rules (CompassCrossMask, CompassTipKeeps) on the pictures' opaque colours. Null without the
		/// pictures.
		/// </summary>
		public static UWPicture ComposeUw2Compass(UWTextures pOTextures, int piStep, bool pbFreed)
		{
			UWRectInt lOBox = Uw2CompassBox(pOTextures);

			if (lOBox.Width <= 0 || lOBox.Height <= 0)
				return null;

			int liNeedles = UWClassicLayoutUw2.Compass.NeedleX.Length;
			int liStep = ((piStep % liNeedles) + liNeedles) % liNeedles;
			UWPicture lOOut = new UWPicture(lOBox.Width, lOBox.Height);

			foreach (int liPicture in new[] { liStep & (Uw2Faces - 1), UWClassicLayoutUw2.Compass.FirstNeedle + liStep })
			{
				UWPicture lOPicture = UWPicture.FromOpaque(fUw2Picture(pOTextures, liPicture));
				bool[] lbKeep = null;

				if (pbFreed)
				{
					if (liPicture < Uw2Faces)
						lbKeep = CompassCrossMask(lOPicture.Pixels, lOPicture.Width, lOPicture.Height);
					else
					{
						lbKeep = new bool[lOPicture.Pixels.Length];

						for (int liAt = 0; liAt < lbKeep.Length; liAt++)
							lbKeep[liAt] = CompassTipKeeps(lOPicture.Pixels[liAt]);
					}

					for (int liAt = 0; liAt < lbKeep.Length; liAt++)
					{
						if (!lbKeep[liAt])
							lOPicture.Pixels[liAt] = UWPixelOps.Transparent;
					}
				}

				fUw2PictureAt(liPicture, out int liX, out int liY);
				lOOut.Overlay(lOPicture, liX - lOBox.X, liY - lOBox.Y);
			}

			return lOOut;
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
