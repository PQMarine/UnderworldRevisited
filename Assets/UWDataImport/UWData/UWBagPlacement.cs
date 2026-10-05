using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE THE MODERN BAGS GO, engine-free (moved out of UWModernBags.LateUpdate 2026-10-05, per
	/// user: reusable for UW2 or another engine, and the rule that needed the most rounds of
	/// fixes). Screen pixels from the bottom left; sizes in original pixels times Scale.
	///
	///   - The BACKPACK where the layout put it (UWHudLayout), pushed out left of an open
	///     character panel lying on it (per user, 2026-10-04: the bags lay over the panel).
	///   - The open bag windows STACKED UPWARDS above its load bar, a new column to the left when
	///     the screen ends - a column over the action bar starts above it (per user, 2026-10-03).
	///     A backpack moved into the upper half stacks them DOWNWARDS; one in the left half starts
	///     new columns to the right.
	///   - A window that would lie on the open character panel or the minimap goes left of it, and
	///     the rest of its column with it; one that would lie on the minimap with no room below or
	///     above starts a new column (per user, 2026-10-04).
	/// </summary>
	public static class UWBagPlacement
	{
		/// <summary>The room an upward column keeps free at the screen's top, original pixels.</summary>
		private const int TopRoom = 24;

		public struct Input
		{
			public float ScreenWidth;

			public float ScreenHeight;

			/// <summary>The bags' own scale: the UI size times their percent.</summary>
			public float Scale;

			/// <summary>The UI size alone: the margin beside the panel and the minimap.</summary>
			public float PixelScale;

			/// <summary>The backpack with its load bar, where the layout put it.</summary>
			public UWRect Pack;

			/// <summary>Moved in the layout editor: then the stacking follows its half of the screen.</summary>
			public bool PackPlaced;

			/// <summary>The backpack window alone, without its load bar, original pixels.</summary>
			public int PackWindowHeight;

			/// <summary>Every bag window's width, original pixels.</summary>
			public int WindowWidth;

			/// <summary>The open bag windows' heights in their order, original pixels.</summary>
			public int[] BagHeights;

			/// <summary>The open character panel, the minimap and the action bar - width 0 for none.</summary>
			public UWRect Panel;

			public UWRect Minimap;

			public UWRect ActionBar;

			public int ScreenMargin;

			public int WindowGap;

			public int LoadBarRows;
		}

		/// <summary>A bag window's place: its bottom right corner.</summary>
		public struct Spot
		{
			public float Right;

			public float Bottom;
		}

		public struct Result
		{
			/// <summary>The backpack with its load bar, after the push.</summary>
			public UWRect Pack;

			/// <summary>The open bag windows, in the order of Input.BagHeights.</summary>
			public Spot[] Bags;
		}

		public static Result Place(Input pOIn)
		{
			float lfScale = pOIn.Scale;
			UWRect lOPlaced = pOIn.Pack;

			if (pOIn.Panel.Width > 0f && pOIn.Panel.Overlaps(lOPlaced))
				lOPlaced.X = Math.Max(0f, pOIn.Panel.XMin - (pOIn.ScreenMargin * pOIn.PixelScale) - lOPlaced.Width);

			float lfRight = lOPlaced.XMax;
			float lfBottom = lOPlaced.Y;
			float lfWidth = pOIn.WindowWidth * lfScale;
			UWRect lOPackWindow = new UWRect(lfRight - lfWidth, lfBottom, lfWidth, pOIn.PackWindowHeight * lfScale);
			float lfBarY = lOPackWindow.YMax + (2 * lfScale);

			int[] liHeights = pOIn.BagHeights ?? new int[0];
			Spot[] lOSpots = new Spot[liHeights.Length];

			float lfColumnRight = lfRight;
			float lfColumnWidth = lOPackWindow.Width;
			float lfFirstY = fColumnBottom(pOIn, lfColumnRight, lfColumnWidth, lfBarY + ((pOIn.LoadBarRows + pOIn.WindowGap) * lfScale));
			float lfY = lfFirstY;
			float lfColumnStart = lfFirstY;
			float lfTop = pOIn.ScreenHeight - (TopRoom * lfScale);

			bool lbDown = pOIn.PackPlaced && lOPackWindow.CenterY > pOIn.ScreenHeight * 0.5f;
			float lfColumnStep = (lfColumnWidth + (pOIn.WindowGap * lfScale))
				* (pOIn.PackPlaced && lOPackWindow.CenterX < pOIn.ScreenWidth * 0.5f ? -1f : 1f);

			if (lbDown)
			{
				float lfDownStart = lOPackWindow.YMin - (pOIn.WindowGap * lfScale);

				lfY = lfDownStart;

				for (int liBag = 0; liBag < liHeights.Length; liBag++)
				{
					float lfHeight = liHeights[liBag] * lfScale;

					// Below the screen's edge, or over the minimap: a new column.
					if (lfY < lfDownStart - 1f && (lfY - lfHeight < pOIn.ScreenMargin * lfScale
						|| fWindowOverlaps(pOIn, pOIn.Minimap, lfColumnRight, lfY - lfHeight, lfHeight)))
					{
						lfColumnRight -= lfColumnStep;
						lfY = lfDownStart;
					}

					lfColumnRight = fBeside(pOIn, pOIn.Panel, lfColumnRight, lfY - lfHeight, lfHeight);
					lfColumnRight = fBeside(pOIn, pOIn.Minimap, lfColumnRight, lfY - lfHeight, lfHeight);
					lOSpots[liBag] = new Spot { Right = lfColumnRight, Bottom = lfY - lfHeight };

					lfY -= lfHeight + (pOIn.WindowGap * lfScale);
				}
			}
			else
			{
				for (int liBag = 0; liBag < liHeights.Length; liBag++)
				{
					float lfHeight = liHeights[liBag] * lfScale;

					// Over the top, or over the minimap: a new column (moved left of the minimap the
					// column was set off oddly).
					if (lfY > lfColumnStart + 1f && (lfY + lfHeight > lfTop || fWindowOverlaps(pOIn, pOIn.Minimap, lfColumnRight, lfY, lfHeight)))
					{
						lfColumnRight -= lfColumnStep;
						lfY = fColumnBottom(pOIn, lfColumnRight, lfColumnWidth, lfBottom);
						lfColumnStart = lfY;
					}

					lfColumnRight = fBeside(pOIn, pOIn.Panel, lfColumnRight, lfY, lfHeight);
					lfColumnRight = fBeside(pOIn, pOIn.Minimap, lfColumnRight, lfY, lfHeight);
					lOSpots[liBag] = new Spot { Right = lfColumnRight, Bottom = lfY };

					lfY += lfHeight + (pOIn.WindowGap * lfScale);
				}
			}

			return new Result { Pack = lOPlaced, Bags = lOSpots };
		}

		/// <summary>A bag window that would lie on the rect goes left of it.</summary>
		private static float fBeside(Input pOIn, UWRect pORect, float pfRight, float pfBottom, float pfHeight)
		{
			return fWindowOverlaps(pOIn, pORect, pfRight, pfBottom, pfHeight)
				? Math.Min(pfRight, pORect.XMin - (pOIn.ScreenMargin * pOIn.PixelScale))
				: pfRight;
		}

		/// <summary>Whether a bag window at this place would lie on the rect.</summary>
		private static bool fWindowOverlaps(Input pOIn, UWRect pORect, float pfRight, float pfBottom, float pfHeight)
		{
			UWRect lOWindow = new UWRect(pfRight - (pOIn.WindowWidth * pOIn.Scale), pfBottom, pOIn.WindowWidth * pOIn.Scale, pfHeight);

			return pORect.Width > 0f && pORect.Overlaps(lOWindow);
		}

		/// <summary>Where a column of bag windows starts: at pfDefault, or above the action bar
		/// when the column reaches over it.</summary>
		private static float fColumnBottom(Input pOIn, float pfRight, float pfWidth, float pfDefault)
		{
			UWRect lOBar = pOIn.ActionBar;

			if (lOBar.Width <= 0f)
				return pfDefault;

			bool lbOver = pfRight > lOBar.XMin && pfRight - pfWidth < lOBar.XMax;

			return lbOver ? Math.Max(pfDefault, lOBar.YMax + (pOIn.WindowGap * pOIn.Scale)) : pfDefault;
		}
	}
}
