using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE PAPERDOLL PAGE, its freed circles and the whole figure without the page (MAIN.BYT,
	/// BODIES.GR, CHRBTNS.GR; cleanup 9.11, 2026-10-25: a builder of its own, UWHudArt forwards to
	/// it). The Build methods read the pictures, the Compose methods work on the pictures given -
	/// the frame picture, the body, the character creation's silhouette, the armour.
	/// </summary>
	public static class UWPaperdollBuilder
	{
		// ------------------------------------------------- The paperdoll page (MAIN.BYT)

		/// <summary>
		/// The original's inventory page above the backpack - MAIN.BYT x 236 to 317, rows 7 to 81:
		/// the leather with the circles of the shoulders, hands and rings, on which the paperdoll
		/// stands (the character panel of the modern scheme, look per user on a mockup, 2026-10-03).
		/// WIDENED BY FOUR to the backpack window's 86 (per user: then it docks onto it): the two
		/// leather columns inside each border are doubled (UWMainFrame.PaperdollWidenLeft/Right), so
		/// everything on the page sits PaperdollShift further right.
		/// </summary>
		public static UWPicture BuildPage(UWTextures pOTextures)
		{
			return ComposePage(UWMainFrame.Picture(pOTextures));
		}

		public static UWPicture ComposePage(UWPicture pOMain)
		{
			UWPicture lOPage = pOMain.Crop(PaperdollPage);
			UWPicture lOOut = new UWPicture(PaperdollPageWidth, lOPage.Height);

			for (int x = 0; x < PaperdollPageWidth; x++)
			{
				int liColumn = PageColumnToMain(x) - PaperdollPage.X;

				for (int y = 0; y < lOPage.Height; y++)
					lOOut.Set(x, y, lOPage.Get(liColumn, y));
			}

			return lOOut;
		}

		/// <summary>The page's place in MAIN.BYT (rows from the top).</summary>
		public static readonly UWRectInt PaperdollPage = UWClassicLayout.Paperdoll.Page;

		/// <summary>The widened page: as wide as the backpack window.</summary>
		public const int PaperdollPageWidth = 86;

		/// <summary>How far the left doubling moves what lies on the page.</summary>
		public const int PaperdollShift = 2;

		/// <summary>The MAIN.BYT column a column of the widened page shows.</summary>
		public static int PageColumnToMain(int piColumn)
		{
			int liColumn;

			if (piColumn < UWMainFrame.PaperdollWidenLeft + 2)
				liColumn = piColumn;
			else if (piColumn < UWMainFrame.PaperdollWidenLeft + 4)
				liColumn = piColumn - 2;
			else if (piColumn < UWMainFrame.PaperdollWidenRight + 4)
				liColumn = piColumn - PaperdollShift;
			else
				liColumn = piColumn - 4;

			return PaperdollPage.X + Math.Min(Math.Max(liColumn, 0), PaperdollPage.Width - 1);
		}

		/// <summary>
		/// THE PAPERDOLL PAGE WITHOUT ITS LEATHER (per user, 2026-10-09, with the slot rings): the
		/// widened page's size, transparent but for the circles of the shoulders, hands and rings -
		/// without the grey stone, the place where the original shows the weight (MAIN.BYT 305/62,
		/// UWGameUI.mOWeightLabelPosition; per user, the same day: "remove it too"). The circles
		/// (UWMainFrame.PageCircles) are their pixels that stand out from the leather around them
		/// (more than 9 of luma from the median of their 7 x 7 neighbourhood,
		/// UWHudTuning.PaperdollMarks) near their line; they are taken from the page BEFORE
		/// the widening and moved by PaperdollShift, so the right ones lose the doubled edge the
		/// widening gives them on the leather page. With pOCarve the circles are carved into the back
		/// (as UWSlotBuilder.BuildRing).
		/// </summary>
		public static UWPicture BuildMarks(UWTextures pOTextures, List<UWColor32> pOCarve = null)
		{
			return ComposeMarks(UWMainFrame.Picture(pOTextures), pOCarve);
		}

		public static UWPicture ComposeMarks(UWPicture pOMain, List<UWColor32> pOCarve = null)
		{
			UWPicture lOPage = pOMain.Crop(PaperdollPage);
			UWPicture lOOut = new UWPicture(PaperdollPageWidth, lOPage.Height);
			float[] lfLuma = new float[lOPage.Width * lOPage.Height];
			float[] lfWindow = new float[((2 * UWHudTuning.PaperdollMarks.MedianReach) + 1) * ((2 * UWHudTuning.PaperdollMarks.MedianReach) + 1)];

			for (int y = 0; y < lOPage.Height; y++)
			{
				for (int x = 0; x < lOPage.Width; x++)
					lfLuma[(y * lOPage.Width) + x] = UWPixelRules.Luma(lOPage.Get(x, y));
			}

			for (int y = 0; y < lOPage.Height; y++)
			{
				for (int x = 0; x < lOPage.Width; x++)
				{
					UWColor32 lOColour = lOPage.Get(x, y);
					bool lbKeep = false;

					for (int liCircle = 0; !lbKeep && liCircle < UWMainFrame.PageCircles.GetLength(0); liCircle++)
					{
						float lfX = x - UWMainFrame.PageCircles[liCircle, 0];
						float lfY = y - UWMainFrame.PageCircles[liCircle, 1];
						float lfRadius = UWMainFrame.PageCircles[liCircle, 2];
						float lfDistance = (float)Math.Sqrt((lfX * lfX) + (lfY * lfY));
						float lfBand = UWMainFrame.PageCircles[liCircle, 3];

						// The hollow inside the large circles, as in a freed slot.
						if (pOCarve != null && lfRadius > UWHudTuning.PaperdollMarks.HollowMinRadius && lfDistance < lfRadius - lfBand)
						{
							lOColour = UWHudTuning.Slot.Hollow;
							lbKeep = true;
							continue;
						}

						if (lfDistance < lfRadius - lfBand || lfDistance > lfRadius + lfBand)
							continue;

						int liCount = 0;

						for (int liDy = -UWHudTuning.PaperdollMarks.MedianReach; liDy <= UWHudTuning.PaperdollMarks.MedianReach; liDy++)
						{
							for (int liDx = -UWHudTuning.PaperdollMarks.MedianReach; liDx <= UWHudTuning.PaperdollMarks.MedianReach; liDx++)
							{
								int liX = Math.Min(Math.Max(x + liDx, 0), lOPage.Width - 1);
								int liY = Math.Min(Math.Max(y + liDy, 0), lOPage.Height - 1);

								lfWindow[liCount++] = lfLuma[(liY * lOPage.Width) + liX];
							}
						}

						Array.Sort(lfWindow, 0, liCount);

						float lfStandOut = lfLuma[(y * lOPage.Width) + x] - lfWindow[liCount / 2];

						lbKeep = Math.Abs(lfStandOut) > UWHudTuning.PaperdollMarks.StandOut;

						if (lbKeep && pOCarve != null && pOCarve.Count > 0)
							lOColour = lfStandOut < 0f ? pOCarve[0] : pOCarve[pOCarve.Count - 1];
					}

					if (lbKeep && x + PaperdollShift < lOOut.Width)
						lOOut.Set(x + PaperdollShift, y, lOColour);
				}
			}

			return lOOut;
		}

		/// <summary>Where the body picture lies on MAIN.BYT (UWModernPanel.msBodyAt).</summary>
		public const int PaperdollBodyX = UWClassicLayout.Paperdoll.BodyLeft;

		public const int PaperdollBodyY = UWClassicLayout.Paperdoll.BodyTop;

		/// <summary>The first body picture of CHRBTNS.GR, the character creation's (bodies in the
		/// order of BODIES.GR).</summary>
		private const int CreationBodyImage = 17;

		/// <summary>
		/// THE WHOLE FIGURE - body and armour - without the page it was painted on (per user,
		/// 2026-10-09). Freeing the paperdoll's body from the page by its colours did not come out
		/// clean - shirt and trousers are the page's own browns, the shadow is painted by hand (per
		/// user). The user's idea: THE CHARACTER CREATION SHOWS THE SAME FIGURES (CHRBTNS.GR 17 to
		/// 26) on colour index 0, without the outline and the shadow - the original's own
		/// silhouette. So:
		///   - The body: the pixels inside that silhouette, plus the black pixels of the paperdoll's
		///     picture touching it (the outline). The two lie apart by two columns and a row for the
		///     men, four and two for the women (found by brightness, the mean difference 7 to 9
		///     there against 14 and more one step off - the creation has its own palette).
		///   - The armour in the classic order (helmet, gloves, legs, chest, boots), drawn as it is -
		///     the pieces are transparent around them - but for THE HELMET: the women's helmets paint
		///     the page and its shadow over the long hair they hide (per user: "helmets with the
		///     background worked in"). The helmet is freed from the page by itself (UWPageFreer, its own
		///     transparent pixels taken as black, so they hold the flood like the outline); what stays
		///     is drawn, what goes takes the figure out there too. (Freeing the other pieces by colour
		///     took a leather boot's cuff, per user - leather and page share their browns.)
		/// The pieces are top-down pictures with their places on MAIN.BYT; piOriginX/Y is where the
		/// result lies on MAIN.BYT.
		/// </summary>
		public static UWPicture BuildFigure(UWTextures pOTextures, int piBody, IList<UWPicture> pOArmour, IList<(int X, int Y)> pOAt,
			out int piOriginX, out int piOriginY)
		{
			UWTexture lOBodySource = pOTextures.GetTextureByType(UWTexture.TextureTypes.BODIES, piBody);
			UWPicture lOBody = UWPicture.From(lOBodySource);
			UWTexture lOCreation = null;

			try
			{
				lOCreation = pOTextures.GetTextureByType(UWTexture.TextureTypes.CHRBTNS, CreationBodyImage + piBody);
			}
			catch
			{
				lOCreation = null;
			}

			byte[] lyIndices = lOCreation != null ? lOCreation.GetMainPaletteIndices() : null;

			return ComposeFigure(UWMainFrame.Picture(pOTextures), lOBody, piBody, lyIndices, lyIndices != null ? lOCreation.Width : 0,
				lyIndices != null ? lOCreation.Height : 0, pOArmour, pOAt, out piOriginX, out piOriginY);
		}

		/// <summary>The figure from the pictures given: the frame picture, the body (BODIES.GR piBody),
		/// the creation's silhouette as palette indices (pyCreation, null: none - the body is freed
		/// by its colours then), the armour and its places.</summary>
		public static UWPicture ComposeFigure(UWPicture pOMain, UWPicture pOBody, int piBody, byte[] pyCreation, int piCreationWidth,
			int piCreationHeight, IList<UWPicture> pOArmour, IList<(int X, int Y)> pOAt, out int piOriginX, out int piOriginY)
		{
			UWPicture lOMain = pOMain;
			UWPicture lOBody = pOBody;
			int liLeft = PaperdollBodyX;
			int liTop = PaperdollBodyY;
			int liRight = PaperdollBodyX + lOBody.Width;
			int liBottom = PaperdollBodyY + lOBody.Height;

			for (int liAt = 0; liAt < pOArmour.Count; liAt++)
			{
				if (pOArmour[liAt] == null)
					continue;

				liLeft = Math.Min(liLeft, pOAt[liAt].X);
				liTop = Math.Min(liTop, pOAt[liAt].Y);
				liRight = Math.Max(liRight, pOAt[liAt].X + pOArmour[liAt].Width);
				liBottom = Math.Max(liBottom, pOAt[liAt].Y + pOArmour[liAt].Height);
			}

			UWPicture lOFigure = new UWPicture(liRight - liLeft, liBottom - liTop);

			// The body inside the creation's silhouette, and its outline.
			UWMask lOInside = new UWMask(lOBody.Width, lOBody.Height);
			byte[] lyIndices = pyCreation;

			if (lyIndices != null)
			{
				int liShiftX = piBody < 5 ? 2 : 4;
				int liShiftY = piBody < 5 ? 1 : 2;

				for (int y = 0; y < piCreationHeight; y++)
				{
					for (int x = 0; x < piCreationWidth; x++)
					{
						int liX = x + liShiftX;
						int liY = y + liShiftY;

						if (lyIndices[(y * piCreationWidth) + x] != 0 && liX < lOBody.Width && liY < lOBody.Height)
							lOInside[liX, liY] = true;
					}
				}

				// The creation's figure is a pixel wider at places (per user: the women's thigh, bodies
				// 5, 7, 8, 9): a pixel at the silhouette's edge that is the page itself is not figure -
				// nor one with no outline before it whose colour the page has within two pixels (the
				// page's sheen moves its colours a little).
				bool fPageNear(int piX, int piY)
				{
					UWColor32 lOA = lOBody.Get(piX, piY);

					for (int liDy = -2; liDy <= 2; liDy++)
					{
						for (int liDx = -2; liDx <= 2; liDx++)
						{
							UWColor32 lOB = lOMain.Get(PaperdollBodyX + piX + liDx, PaperdollBodyY + piY + liDy);

							if (UWPixelRules.SameRgb(lOA, lOB))
								return true;
						}
					}

					return false;
				}

				bool fOpenSide(int piX, int piY)
				{
					for (int liDir = 0; liDir < 4; liDir++)
					{
						int liX = piX + (liDir == 0 ? 1 : liDir == 1 ? -1 : 0);
						int liY = piY + (liDir == 2 ? 1 : liDir == 3 ? -1 : 0);

						if (!lOInside.Contains(liX, liY))
							return true;

						if (!lOInside[liX, liY] && !UWPixelRules.IsOutlineBlack(lOBody.Get(liX, liY)))
							return true;
					}

					return false;
				}

				for (int liPass = 0; liPass < 2; liPass++)
				{
					List<int> lOOut = new List<int>();

					for (int y = 0; y < lOBody.Height; y++)
					{
						for (int x = 0; x < lOBody.Width; x++)
						{
							int liAt = (y * lOBody.Width) + x;

							if (!lOInside.Bits[liAt])
								continue;

							bool lbEdge = x == 0 || y == 0 || x == lOBody.Width - 1 || y == lOBody.Height - 1
								|| !lOInside.Bits[liAt - 1] || !lOInside.Bits[liAt + 1] || !lOInside.Bits[liAt - lOBody.Width]
								|| !lOInside.Bits[liAt + lOBody.Width];
							UWColor32 lOA = lOBody.Get(x, y);
							UWColor32 lOB = lOMain.Get(PaperdollBodyX + x, PaperdollBodyY + y);

							if (lbEdge && (UWPixelRules.SameRgb(lOA, lOB) || (fOpenSide(x, y) && fPageNear(x, y))))
								lOOut.Add(liAt);
						}
					}

					foreach (int liAt in lOOut)
						lOInside.Bits[liAt] = false;
				}
			}
			else
			{
				// Without the creation's picture: the body freed from the page by its colours.
				UWPicture lOFreed = UWPageFreer.Free(lOMain, lOBody, PaperdollBodyX, PaperdollBodyY);

				for (int liAt = 0; liAt < lOInside.Bits.Length; liAt++)
					lOInside.Bits[liAt] = lOFreed.Pixels[liAt].A > 0;
			}

			for (int y = 0; y < lOBody.Height; y++)
			{
				for (int x = 0; x < lOBody.Width; x++)
				{
					UWColor32 lOColour = lOBody.Get(x, y);
					bool lbKeep = lOInside[x, y] || (UWPixelRules.IsOutlineBlack(lOColour) && lOInside.AnyAround(x, y));

					if (lbKeep)
						lOFigure.Set(PaperdollBodyX - liLeft + x, PaperdollBodyY - liTop + y, lOColour);
				}
			}

			// The armour as it is; only the helmet freed from the page, and its page takes the figure
			// out.
			for (int liAt = 0; liAt < pOArmour.Count; liAt++)
			{
				UWPicture lOPiece = pOArmour[liAt];

				if (lOPiece == null)
					continue;

				int liX0 = pOAt[liAt].X;
				int liY0 = pOAt[liAt].Y;

				// Only the helmet paints page and shadow; the other pieces are transparent around
				// them already, and freeing them by colour took a leather boot's cuff (per user).
				if (liAt != 0)
				{
					lOFigure.Overlay(lOPiece, liX0 - liLeft, liY0 - liTop);

					continue;
				}

				// The piece's own transparent pixels count as outline (black): a helmet's face opening
				// filled with the page let the flood eat the brim around it.
				UWPicture lOOnPage = new UWPicture(lOPiece.Width, lOPiece.Height);

				for (int y = 0; y < lOPiece.Height; y++)
				{
					for (int x = 0; x < lOPiece.Width; x++)
						lOOnPage.Set(x, y, lOPiece.Get(x, y).A > 0 ? lOPiece.Get(x, y) : UWPixelOps.Black);
				}

				UWPicture lOFreed = UWPageFreer.Free(lOMain, lOOnPage, liX0, liY0, false);

				for (int y = 0; y < lOPiece.Height; y++)
				{
					for (int x = 0; x < lOPiece.Width; x++)
					{
						if (lOPiece.Get(x, y).A == 0)
							continue;

						if (lOFreed.Get(x, y).A > 0)
							lOFigure.Set(liX0 - liLeft + x, liY0 - liTop + y, lOPiece.Get(x, y));
						else
							lOFigure.Set(liX0 - liLeft + x, liY0 - liTop + y, UWPixelOps.Transparent);
					}
				}
			}

			piOriginX = liLeft;
			piOriginY = liTop;

			return lOFigure;
		}
	}
}
