using UWDataImport.Presentation;

namespace UWDataImport.UWData
{
	/// <summary>
	/// CLASSIC WIDE PER GAME (UW2 plan, "The modern interface for UW2: one interface, two hosts", stage U7 step 1,
	/// 2026-11-24): where the game's frame is cut to pull it to a wider screen (UWClassicWideBuilder), what grows the
	/// columns put in, and the view's hole.
	///
	/// FOUR BANDS OF ROWS, TWO CUTS OR MORE EACH: the columns put in are shared out evenly among a band's cuts (Share),
	/// left of its first cut the frame stays, right of its last it moves by all of them - with two cuts the middle by
	/// half; the columns put in are grown from the band's plain sources (none in the view's band, where they are the
	/// hole).
	///
	/// THE FIRST GAME: MAIN.BYT's numbers (UWClassicLayout.ClassicWide, UWMainFrame.ClassicWideSources) - the command
	/// column and the left bar stay, the compass with the middle, the panel and the flasks at the right; the hole is
	/// MAIN.BYT's clear pixels, and the compass's disc (COMPASS.GR 0) covers the needle baked into the frame.
	///
	/// THE SECOND GAME: BYT.ARK block 4 (UWClassicLayoutUw2.ClassicWide) - THE RIGHT BLOCK (the panel, the flasks and
	/// the command bar) MOVES WHOLE to the right edge in every band, the left part grows: the top bar either side of the
	/// gargoyle's claws, the view between its walls, the ledge between the spells' hollow and the compass recess and
	/// between the recess and the rune hollow AND at the ledge's two ends - four cuts, the hollows spread a quarter each
	/// (per user 2026-11-24, from three variants: the group of compass and hollows stays, the rune hollow goes most of
	/// the way towards the panel and the rune page) -, the scroll in its paper. The frame has no clear pixel: the hole is its
	/// black window within the camera's area (the near black the screen draws there), the gargoyle's teeth over the
	/// view's first rows staying.
	/// </summary>
	public sealed class UWClassicWideGeometry
	{
		public bool IsSecondGame { get; private set; }

		/// <summary>The bands' first rows: the top bar from row 0, then the view, the ledge, the scroll.</summary>
		public int TopRows { get; private set; }

		public int LedgeTop { get; private set; }

		public int ScrollTop { get; private set; }

		/// <summary>The cuts of each band (top bar, view, ledge, scroll), ascending, two or more.</summary>
		public int[][] Cuts { get; private set; }

		/// <summary>Each band's plain source columns as ranges (first, end); none in the view's band.</summary>
		public int[][] Sources { get; private set; }

		/// <summary>The view's hole in the unwidened frame.</summary>
		public UWRectInt Hole { get; private set; }

		/// <summary>The camera's area, the hole's flood bounded by it (right and bottom not included).</summary>
		public int CameraLeft { get; private set; }

		public int CameraTop { get; private set; }

		public int CameraRight { get; private set; }

		public int CameraBottom { get; private set; }

		/// <summary>The hole is the frame's black (the second game's opaque frame), not its clear pixels: a pixel
		/// whose colours add up to at most this; -1 for the clear pixels.</summary>
		public int HoleBlack { get; private set; }

		/// <summary>The compass's disc (COMPASS.GR 0) laid over the needle the frame has baked in (the first game's).</summary>
		public bool CoversBakedNeedle { get; private set; }

		/// <summary>The dragons wind round the frame's bars (the first game's; the second game's frame has none).</summary>
		public bool HasDragons { get; private set; }

		/// <summary>The row the view's straight ahead lies on (the projection's centre), from the top.</summary>
		public float ProjectionCentreRow { get; private set; }

		/// <summary>The row the drawn weapon's offsets count up from: the first game's wTopOffset row, the second game's
		/// view's bottom edge (its pictures' y counts up from the frame buffer's bottom).</summary>
		public int WeaponRow { get; private set; }

		/// <summary>The band of a row: 0 the top bar, 1 the view, 2 the ledge, 3 the scroll.</summary>
		public int Band(int piRow)
		{
			return piRow < TopRows ? 0 : piRow < LedgeTop ? 1 : piRow < ScrollTop ? 2 : 3;
		}

		/// <summary>A band's cut by its number (0 the first), by a row from the top.</summary>
		public int Cut(int piRow, int piWhich)
		{
			return Cuts[Band(piRow)][piWhich];
		}

		/// <summary>Where an original pixel column lies in a frame widened by piExtra columns: further by the columns put
		/// in at the cuts at or left of it (Share) - unchanged left of its band's first cut, piExtra further right of the
		/// last.</summary>
		public int ShiftX(int piX, int piY, int piExtra)
		{
			int[] liCuts = Cuts[Band(piY)];
			int liPassed = 0;

			while (liPassed < liCuts.Length && piX >= liCuts[liPassed])
				liPassed++;

			return Share(piExtra, liPassed, liCuts.Length);
		}

		/// <summary>
		/// THE WAY BACK (UW2 plan stage U8 step 2, 2026-11-24): the original's column of a column of the frame widened by
		/// piExtra columns, at a row from the top - -1 in the columns put in (they belong to no area of the original's click
		/// table). The columns put in at a band's cut k lie from cut k + Share(k) up to cut k + Share(k + 1); left of them the
		/// original's columns lie Share(k) further. With two cuts the first game's rule as it was (UWClassicGeometry).
		/// </summary>
		public int OriginalColumn(int piX, int piRow, int piExtra)
		{
			if (piExtra <= 0)
				return piX;

			int[] liCuts = Cuts[Band(piRow)];

			for (int liCut = 0; liCut < liCuts.Length; liCut++)
			{
				int liBefore = Share(piExtra, liCut, liCuts.Length);

				if (piX < liCuts[liCut] + liBefore)
					return piX - liBefore;

				if (piX < liCuts[liCut] + Share(piExtra, liCut + 1, liCuts.Length))
					return -1;
			}

			return piX - piExtra;
		}

		/// <summary>The columns put in at a band's first piCuts of piOf cuts together: piExtra shared evenly, the
		/// remainder to the later ones (two cuts: half of it, rounded down, at the first).</summary>
		public static int Share(int piExtra, int piCuts, int piOf)
		{
			return piExtra * piCuts / piOf;
		}

		/// <summary>Whether a pixel of the frame can be the view's hole: clear (the first game) or black enough (the
		/// second).</summary>
		public bool IsHolePixel(UWColor32 pOPixel)
		{
			return HoleBlack < 0 ? pOPixel.A == 0 : pOPixel.R + pOPixel.G + pOPixel.B <= HoleBlack;
		}

		public static UWClassicWideGeometry Of(UWTextures pOTextures)
		{
			return pOTextures != null && pOTextures.Game == UWGame.Uw2 ? Uw2 : Uw1;
		}

		/// <summary>The first game's table (two cuts a band) as rows.</summary>
		private static int[][] fRows(int[,] piTable)
		{
			int[][] liRows = new int[piTable.GetLength(0)][];

			for (int liRow = 0; liRow < liRows.Length; liRow++)
			{
				liRows[liRow] = new int[piTable.GetLength(1)];

				for (int liAt = 0; liAt < liRows[liRow].Length; liAt++)
					liRows[liRow][liAt] = piTable[liRow, liAt];
			}

			return liRows;
		}

		public static readonly UWClassicWideGeometry Uw1 = new UWClassicWideGeometry
		{
			IsSecondGame = false,
			TopRows = UWClassicLayout.ClassicWide.TopRows,
			LedgeTop = UWClassicLayout.ClassicWide.LedgeTop,
			ScrollTop = UWClassicLayout.ClassicWide.ScrollTop,
			Cuts = fRows(UWClassicLayout.ClassicWide.Cuts),
			Sources = UWMainFrame.ClassicWideSources,
			Hole = UWClassicLayout.ClassicWide.Hole,
			CameraLeft = UWClassicLayout.Frame.WindowHoleLeft,
			CameraTop = UWClassicLayout.Frame.WindowHoleTop,
			CameraRight = UWClassicLayout.Frame.WindowHoleRight,
			CameraBottom = UWClassicLayout.Frame.WindowHoleBottom,
			HoleBlack = -1,
			CoversBakedNeedle = true,
			HasDragons = true,
			ProjectionCentreRow = UWClassicLayout.Frame.ProjectionCentreRow,
			WeaponRow = UWClassicLayout.Frame.WeaponTop
		};

		public static readonly UWClassicWideGeometry Uw2 = new UWClassicWideGeometry
		{
			IsSecondGame = true,
			TopRows = UWClassicLayoutUw2.ClassicWide.TopRows,
			LedgeTop = UWClassicLayoutUw2.ClassicWide.LedgeTop,
			ScrollTop = UWClassicLayoutUw2.ClassicWide.ScrollTop,
			Cuts = UWClassicLayoutUw2.ClassicWide.Cuts,
			Sources = UWClassicLayoutUw2.ClassicWide.Sources,
			Hole = UWClassicLayoutUw2.ClassicWide.Hole,
			CameraLeft = UWClassicLayoutUw2.Frame.ViewWindowLeft,
			CameraTop = UWClassicLayoutUw2.Frame.ViewWindowTop,
			CameraRight = UWClassicLayoutUw2.Frame.ViewWindowLeft + UWClassicLayoutUw2.Frame.ViewWindowWidth,
			CameraBottom = UWClassicLayoutUw2.Frame.ViewWindowTop + UWClassicLayoutUw2.Frame.ViewWindowHeight,
			HoleBlack = UWClassicLayoutUw2.ClassicWide.HoleBlack,
			CoversBakedNeedle = false,
			HasDragons = false,
			ProjectionCentreRow = UWClassicLayoutUw2.Frame.ProjectionCentreRow,
			WeaponRow = UWClassicLayoutUw2.Frame.ViewWindowTop + UWClassicLayoutUw2.Frame.ViewWindowHeight
		};
	}
}
