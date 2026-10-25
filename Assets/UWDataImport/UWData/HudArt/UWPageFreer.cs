using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A PICTURE LYING ON MAIN.BYT FREED FROM THE PAGE AROUND IT - the paperdoll's body when the
	/// character creation's silhouette is missing, the helmet that paints the page (cleanup 9.11,
	/// 2026-10-25: a builder of its own, UWHudArt.FreeOnPage forwards to it; the six steps split
	/// into local functions verbatim - their depths depend on the walk's order, frozen per
	/// CLEANUP-PLAN 11). Free(UWPicture main, ...) is the core on pictures alone.
	/// </summary>
	public static class UWPageFreer
	{
		// ------------------------------------------------- The page freer

		/// <summary>Not taken out (Free's state per pixel).</summary>
		private const int Kept = int.MinValue;

		/// <summary>
		/// A picture lying on MAIN.BYT at piMainX/Y freed from the page around it (per user,
		/// 2026-10-09, screenshot: on a back of its own the figure stood in a box of copper; BODIES.GR
		/// pictures are opaque, their background the page itself at UWPaperdollBuilder.PaperdollBodyX/Y
		/// - pixel for pixel, but for a drop shadow). The SHADOW the original paints along the figure is
		/// wider than first thought and its own colours (per user, the same day: "not quite clean yet"), so in steps:
		///   1. From the edges: every pixel that equals the page; from there two more steps into what
		///      does not (near the page) - but only into light pixels (luma 50 and more): the legs are
		///      outlined in dark brown at places, not black (per user: a piece of a leg went missing).
		///   2. The shadow: it falls to the right, so further steps go only LEFTWARD into it (or up
		///      and down within it), six at most, and only into pixels darker than the page there.
		///   No step enters a black pixel or one SANDWICHED between two black ones (a gap in the
		///   outline); the shadow also stays out of the outline's band (a pixel around every black
		///   one), so it does not slip through wider gaps into the figure.
		///   3. The shadow the band kept: a pixel darker than the page with the taken-out background
		///      on one side and the outline two away on the other, then one away (per user: shadow left
		///      at the shoulder); then any pixel with the outline on one side and the taken-out
		///      background on the opposite side.
		///   4. Enclosed strips of background, between the arms and the body and between the legs
		///      (per user), which no flood from outside reaches: 4-connected pieces between the
		///      outlines of at most 40 pixels and at most 3 wide or high, every pixel the page or
		///      darker than it by more than 20 (the armour's own dark patches are not that dark).
		///   5. Spurs and specks: a pixel with five or more of its eight neighbours gone (six for a
		///      black one) goes too, twice.
		///   6. Of what stays, only the largest 8-connected piece (unless pbLargestOnly is off - an
		///      armour piece may be two gloves or two boots; then the shadow may also start at the
		///      picture's edge, where a piece's painted shadow reaches it).
		/// The shirts and trousers are the page's own browns (per user), so where a gap between the
		/// legs is as wide and as dark as a leg, neither shape nor colour tells them apart.
		/// </summary>
		public static UWPicture Free(UWTextures pOTextures, UWPicture pOBody, int piMainX, int piMainY, bool pbLargestOnly = true)
		{
			return Free(UWMainFrame.Picture(pOTextures), pOBody, piMainX, piMainY, pbLargestOnly);
		}

		/// <summary>The same on the frame picture given (pOMain).</summary>
		public static UWPicture Free(UWPicture pOMain, UWPicture pOBody, int piMainX, int piMainY, bool pbLargestOnly = true)
		{
			UWPicture lOMain = pOMain;
			int liWidth = pOBody.Width;
			int liHeight = pOBody.Height;
			int[] liDepth = new int[liWidth * liHeight];
			bool[] lbBand = new bool[liWidth * liHeight];
			Queue<int> lOOpen = new Queue<int>();

			for (int liAt = 0; liAt < liDepth.Length; liAt++)
				liDepth[liAt] = Kept;

			bool fPage(int piX, int piY)
			{
				return UWPixelRules.SameRgb(pOBody.Get(piX, piY), lOMain.Get(piMainX + piX, piMainY + piY));
			}

			bool fBlack(int piX, int piY)
			{
				return UWPixelRules.IsOutlineBlack(pOBody.Get(piX, piY));
			}

			bool fDarker(int piX, int piY)
			{
				return UWPixelRules.Luma(pOBody.Get(piX, piY)) < UWPixelRules.Luma(lOMain.Get(piMainX + piX, piMainY + piY)) - UWHudTuning.PageFreer.DarkerBy;
			}

			bool fGone(int piX, int piY)
			{
				return piX < 0 || piY < 0 || piX >= liWidth || piY >= liHeight || liDepth[(piY * liWidth) + piX] != Kept;
			}

			bool fBlackAt(int piX, int piY)
			{
				return piX >= 0 && piY >= 0 && piX < liWidth && piY < liHeight && fBlack(piX, piY);
			}

			bool fSandwiched(int piX, int piY)
			{
				return (fBlackAt(piX + 1, piY) && fBlackAt(piX - 1, piY)) || (fBlackAt(piX, piY + 1) && fBlackAt(piX, piY - 1))
					|| (fBlackAt(piX + 1, piY + 1) && fBlackAt(piX - 1, piY - 1)) || (fBlackAt(piX + 1, piY - 1) && fBlackAt(piX - 1, piY + 1));
			}

			float fBelowPage(int piX, int piY)
			{
				return UWPixelRules.Luma(lOMain.Get(piMainX + piX, piMainY + piY)) - UWPixelRules.Luma(pOBody.Get(piX, piY));
			}

			// The fringe lists of steps 3 and 5.
			List<int> lOFringe = new List<int>();

			// The outline's band: a pixel around every black one.
			void fMarkBand()
			{
				for (int y = 0; y < liHeight; y++)
				{
					for (int x = 0; x < liWidth; x++)
					{
						if (!fBlack(x, y))
							continue;

						for (int liDy = -1; liDy <= 1; liDy++)
						{
							for (int liDx = -1; liDx <= 1; liDx++)
							{
								if (x + liDx >= 0 && y + liDy >= 0 && x + liDx < liWidth && y + liDy < liHeight)
									lbBand[((y + liDy) * liWidth) + x + liDx] = true;
							}
						}
					}
				}
			}

			void fPageAndShadow()
			{
				// 1 and 2: the page, the steps near it, the shadow.
				for (int y = 0; y < liHeight; y++)
				{
					for (int x = 0; x < liWidth; x++)
					{
						if (x != 0 && y != 0 && x != liWidth - 1 && y != liHeight - 1)
							continue;

						if (fPage(x, y))
						{
							liDepth[(y * liWidth) + x] = 0;
							lOOpen.Enqueue((y * liWidth) + x);
						}
						else if (!pbLargestOnly && !fBlack(x, y) && fDarker(x, y))
						{
							// An armour piece's shadow reaching its edge (a woman's helmet paints three
							// or four columns of it at its right).
							liDepth[(y * liWidth) + x] = -1;
							lOOpen.Enqueue((y * liWidth) + x);
						}
					}
				}

				while (lOOpen.Count > 0)
				{
					int liAt = lOOpen.Dequeue();
					int liX = liAt % liWidth;
					int liY = liAt / liWidth;
					int liFrom = liDepth[liAt];

					for (int liDir = 0; liDir < 4; liDir++)
					{
						int liDx = liDir == 0 ? 1 : liDir == 1 ? -1 : 0;
						int liDy = liDir == 2 ? 1 : liDir == 3 ? -1 : 0;
						int liNx = liX + liDx;
						int liNy = liY + liDy;

						if (fGone(liNx, liNy) || fBlack(liNx, liNy))
							continue;

						int liNext = (liNy * liWidth) + liNx;
						int liTo;

						if (liFrom == 0 && fPage(liNx, liNy))
							liTo = 0;
						else if (fSandwiched(liNx, liNy))
							continue;
						else if (liFrom >= 0 && liFrom < UWHudTuning.PageFreer.NearSteps && UWPixelRules.Luma(pOBody.Get(liNx, liNy)) >= UWHudTuning.PageFreer.NearLuma)
							liTo = liFrom + 1;
						else if (lbBand[liNext])
							continue;
						else if ((liDx == -1 || (liDx == 0 && liFrom < 0)) && Math.Abs(liFrom) < UWHudTuning.PageFreer.ShadowSteps && fDarker(liNx, liNy))
							liTo = -(Math.Abs(liFrom) + 1);
						else
							continue;

						liDepth[liNext] = liTo;
						lOOpen.Enqueue(liNext);
					}
				}
			}

			void fShadowKept()
			{
				// 3: the shadow the band kept (outline two away, then one away), then the fringe along the
				// outline.
				for (int liReach = 2; liReach >= 1; liReach--)
				{
					lOFringe.Clear();

					for (int y = 1; y < liHeight - 1; y++)
					{
						for (int x = 1; x < liWidth - 1; x++)
						{
							if (fGone(x, y) || fBlack(x, y) || !fDarker(x, y))
								continue;

							for (int liDir = 0; liDir < 4; liDir++)
							{
								int liDx = liDir == 0 ? 1 : liDir == 1 ? -1 : 0;
								int liDy = liDir == 2 ? 1 : liDir == 3 ? -1 : 0;
								int liOutX = x - (liDx * liReach);
								int liOutY = y - (liDy * liReach);
								bool lbBetweenFree = liReach == 1 || (!fBlackAt(x - liDx, y - liDy) && !fGone(x - liDx, y - liDy));

								if (fGone(x + liDx, y + liDy) && fBlackAt(liOutX, liOutY) && lbBetweenFree)
								{
									lOFringe.Add((y * liWidth) + x);
									break;
								}
							}
						}
					}

					foreach (int liAt in lOFringe)
						liDepth[liAt] = -9;
				}

				lOFringe.Clear();

				for (int y = 1; y < liHeight - 1; y++)
				{
					for (int x = 1; x < liWidth - 1; x++)
					{
						if (fGone(x, y) || fBlack(x, y))
							continue;

						if ((fBlack(x - 1, y) && fGone(x + 1, y)) || (fBlack(x + 1, y) && fGone(x - 1, y))
							|| (fBlack(x, y - 1) && fGone(x, y + 1)) || (fBlack(x, y + 1) && fGone(x, y - 1)))
							lOFringe.Add((y * liWidth) + x);
					}
				}

				foreach (int liAt in lOFringe)
					liDepth[liAt] = -9;
			}

			void fEnclosedStrips()
			{
				// 4: enclosed strips of background.
				bool[] lbSeen = new bool[liDepth.Length];
				List<int> lOStrip = new List<int>();

				for (int liStart = 0; liStart < liDepth.Length; liStart++)
				{
					if (lbSeen[liStart] || liDepth[liStart] != Kept || fBlack(liStart % liWidth, liStart / liWidth))
						continue;

					lOStrip.Clear();
					lbSeen[liStart] = true;
					lOOpen.Enqueue(liStart);

					int liMinX = int.MaxValue, liMaxX = int.MinValue, liMinY = int.MaxValue, liMaxY = int.MinValue;
					bool lbBackground = true;

					while (lOOpen.Count > 0)
					{
						int liAt = lOOpen.Dequeue();
						int liX = liAt % liWidth;
						int liY = liAt / liWidth;

						lOStrip.Add(liAt);
						liMinX = Math.Min(liMinX, liX);
						liMaxX = Math.Max(liMaxX, liX);
						liMinY = Math.Min(liMinY, liY);
						liMaxY = Math.Max(liMaxY, liY);
						lbBackground &= fPage(liX, liY) || fBelowPage(liX, liY) > UWHudTuning.PageFreer.StripBelowPage;

						for (int liDir = 0; liDir < 4; liDir++)
						{
							int liNx = liX + (liDir == 0 ? 1 : liDir == 1 ? -1 : 0);
							int liNy = liY + (liDir == 2 ? 1 : liDir == 3 ? -1 : 0);

							if (fGone(liNx, liNy) || fBlack(liNx, liNy) || lbSeen[(liNy * liWidth) + liNx])
								continue;

							lbSeen[(liNy * liWidth) + liNx] = true;
							lOOpen.Enqueue((liNy * liWidth) + liNx);
						}
					}

					bool lbNarrow = Math.Min(liMaxX - liMinX + 1, liMaxY - liMinY + 1) <= UWHudTuning.PageFreer.StripMaxThickness;

					if (lbBackground && lbNarrow && lOStrip.Count <= UWHudTuning.PageFreer.StripMaxPixels)
					{
						foreach (int liAt in lOStrip)
							liDepth[liAt] = -8;
					}
				}
			}

			void fSpecks()
			{
				// 5: spurs and specks.
				for (int liPass = 0; liPass < 2; liPass++)
				{
					lOFringe.Clear();

					for (int y = 0; y < liHeight; y++)
					{
						for (int x = 0; x < liWidth; x++)
						{
							if (fGone(x, y))
								continue;

							int liAround = 0;

							for (int liDy = -1; liDy <= 1; liDy++)
							{
								for (int liDx = -1; liDx <= 1; liDx++)
								{
									if ((liDx != 0 || liDy != 0) && fGone(x + liDx, y + liDy))
										liAround++;
								}
							}

							if (liAround >= (fBlack(x, y) ? UWHudTuning.PageFreer.BlackSpeckGone : UWHudTuning.PageFreer.SpeckGone))
								lOFringe.Add((y * liWidth) + x);
						}
					}

					foreach (int liAt in lOFringe)
						liDepth[liAt] = -9;
				}
			}

			UWPicture fLargestPiece()
			{
				// 6: the largest piece of what stays.
				int[] liPiece = new int[liDepth.Length];
				int liBest = 0;
				int liBestSize = 0;
				int liPieces = 0;

				for (int liStart = 0; liStart < liDepth.Length; liStart++)
				{
					if (liDepth[liStart] != Kept || liPiece[liStart] != 0)
						continue;

					int liSize = 0;

					liPieces++;
					liPiece[liStart] = liPieces;
					lOOpen.Enqueue(liStart);

					while (lOOpen.Count > 0)
					{
						int liAt = lOOpen.Dequeue();
						int liX = liAt % liWidth;
						int liY = liAt / liWidth;

						liSize++;

						for (int liDy = -1; liDy <= 1; liDy++)
						{
							for (int liDx = -1; liDx <= 1; liDx++)
							{
								int liNx = liX + liDx;
								int liNy = liY + liDy;

								if (liNx < 0 || liNy < 0 || liNx >= liWidth || liNy >= liHeight)
									continue;

								int liNext = (liNy * liWidth) + liNx;

								if (liDepth[liNext] != Kept || liPiece[liNext] != 0)
									continue;

								liPiece[liNext] = liPieces;
								lOOpen.Enqueue(liNext);
							}
						}
					}

					if (liSize > liBestSize)
					{
						liBestSize = liSize;
						liBest = liPieces;
					}
				}

				UWPicture lOOut = new UWPicture(liWidth, liHeight);

				for (int y = 0; y < liHeight; y++)
				{
					for (int x = 0; x < liWidth; x++)
					{
						int liPieceOf = liPiece[(y * liWidth) + x];

						if (liPieceOf != 0 && (liPieceOf == liBest || !pbLargestOnly))
							lOOut.Set(x, y, pOBody.Get(x, y));
					}
				}

				return lOOut;
			}

			fMarkBand();
			fPageAndShadow();
			fShadowKept();
			fEnclosedStrips();
			fSpecks();

			return fLargestPiece();
		}
	}
}
