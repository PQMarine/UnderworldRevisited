using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE RUNE BAG'S PAGE PER GAME (UW2 plan, "The modern interface for UW2: one interface, two hosts", part U4c
	/// step 1, 2026-11-23): where each rune's picture lies on PANELS.GR 1, which rune or strip a point of the page
	/// takes. Both games' page is PANELS.GR 1, 79 x 112, and both games' mous_in_rune is the same arithmetic
	/// (UnderworldExhumed uw1 and uw2 RUNES.C): in the panel's mouse region, cells of 18 columns by 15 rows, the
	/// rune 20 - 4 * row + column with the rows counted up from 0x12 above the region's bottom, below that the
	/// clear strip. What differs is the picture, the runes' places on it, the mouse region and where the columns
	/// begin in it (the first game's from x 3, the second game's from 0). The classic frame (UWHudPanel,
	/// UWHudRunes) and the modern rune panel (UWModernRunePanel) read it from here; until this step it was
	/// UWRuneShelfModel's, with the first game's numbers alone.
	///
	/// Coordinates are the page's pixels, x from its left, rows from its top.
	///
	/// THE FIRST GAME's numbers are UWClassicLayout.RuneShelf (the grid user-tuned 2026-09-26, the region from
	/// BeginInventory). THE SECOND GAME's come from ShowRune and the panel's mouse region
	/// (UWClassicLayoutUw2.Runes, Statistics, Panel), the same numbers the classic page of UWGameScreenUw2 draws
	/// and clicks with.
	/// </summary>
	public sealed class UWRuneTabletGeometry
	{
		/// <summary>Runes to a row, for both games (ShowRune's rune &amp; 3, mous_in_rune's &lt;&lt; 2).</summary>
		public const int Columns = 4;

		/// <summary>The cells' and the pictures' pitch, for both games (mous_in_rune's 0x12 and 0xF).</summary>
		public const int PitchX = 0x12;

		public const int PitchY = 0xF;

		/// <summary>mous_in_rune's rows count from here above the region's bottom; below it lies the clear strip.</summary>
		public const int CellOriginY = 0x12;

		public bool IsSecondGame { get; private set; }

		/// <summary>The page's picture, PANELS.GR 1 (UWTextures' panel sizes).</summary>
		public int PageWidth { get; private set; }

		public int PageHeight { get; private set; }

		/// <summary>Rune 0's picture, its top-left corner.</summary>
		public int FirstSlotLeft { get; private set; }

		public int FirstSlotTop { get; private set; }

		/// <summary>The panel's mouse region on the page, both ends included.</summary>
		public int HitLeft { get; private set; }

		public int HitRight { get; private set; }

		public int HitTop { get; private set; }

		public int HitBottom { get; private set; }

		/// <summary>Where mous_in_rune's columns begin in the region.</summary>
		public int CellOriginX { get; private set; }

		/// <summary>The strip under the cells that clears the shelf, all of it clickable (tested per user in the
		/// first game's original), both edge rows in.</summary>
		public int ClearTop { get; private set; }

		public int ClearBottom { get; private set; }

		/// <summary>A rune's picture on the page, its top-left corner - where both schemes draw it.</summary>
		public int SlotLeft(int piRune)
		{
			return FirstSlotLeft + ((piRune % Columns) * PitchX);
		}

		public int SlotTop(int piRune)
		{
			return FirstSlotTop + ((piRune / Columns) * PitchY);
		}

		/// <summary>
		/// WHICH RUNE A CLICK ON THE PAGE TAKES, in both schemes (decision 27, 2026-10-20): x and row in the page's
		/// pixels; the rune when pfShown says it lies in the bag, else -1 - also off the region and on the clear
		/// strip (IsClearStrip, asked first by the callers).
		///
		/// As mous_in_rune computes it: x and y from the region's bottom-left corner, column (x - CellOriginX) / 18,
		/// row from the bottom (y - 0x12) / 15, the rune 20 - 4 * row + column. So the cells are gapless, 18 x 15
		/// each, and every point of the region above the clear strip has its rune. What follows from the original's
		/// integer arithmetic is kept: the division rounds towards zero (C, as C#), so in the first game the first
		/// column also takes x 0 to 2 of the region (21 pixels wide); the region's last columns give column 4,
		/// which is the first rune of the row below, and below the bottom row "rune 24", whose bag bit the original
		/// reads past the bag (never set), so nothing.
		/// </summary>
		public int RuneCellAt(float pfX, float pfRow, Func<int, bool> pfShown)
		{
			int liX = (int)Math.Floor(pfX);
			int liRow = (int)Math.Floor(pfRow);

			if (liX < HitLeft || liX > HitRight || liRow < HitTop || liRow > HitBottom)
				return -1;

			// The region's own coordinates, y upwards from its bottom as the original's mouse.
			int liRegionX = liX - HitLeft;
			int liRegionY = HitBottom - liRow;

			if (liRegionY < CellOriginY)
				return -1;

			int liColumn = (liRegionX - CellOriginX) / PitchX;
			int liRowFromBottom = (liRegionY - CellOriginY) / PitchY;
			int liBottomRowFirst = UWPlayerData.RuneCount - Columns;
			int liRune = liBottomRowFirst - (liRowFromBottom * Columns) + liColumn;

			if (liRune < 0 || liRune >= UWPlayerData.RuneCount)
				return -1;

			return pfShown != null && pfShown(liRune) ? liRune : -1;
		}

		/// <summary>The clear strip at the page's bottom, both edge rows in.</summary>
		public bool IsClearStrip(float pfRow)
		{
			return pfRow >= ClearTop && pfRow <= ClearBottom;
		}

		/// <summary>The measures for the data's game.</summary>
		public static UWRuneTabletGeometry Of(UWTextures pOTextures)
		{
			return pOTextures != null && pOTextures.Game == UWGame.Uw2 ? Uw2 : Uw1;
		}

		/// <summary>mous_in_rune's columns in the first game: (x - 3) / 18 (uw1 RUNES.C:127).</summary>
		private const int Uw1CellOriginX = 3;

		public static readonly UWRuneTabletGeometry Uw1 = new UWRuneTabletGeometry
		{
			IsSecondGame = false,
			PageWidth = UWTextures.PanelWidth,
			PageHeight = UWTextures.PanelHeight,
			FirstSlotLeft = UWClassicLayout.RuneShelf.SlotLeft,
			FirstSlotTop = UWClassicLayout.RuneShelf.SlotTop,
			HitLeft = UWClassicLayout.RuneShelf.HitLeft,
			HitRight = UWClassicLayout.RuneShelf.HitRight,
			HitTop = UWClassicLayout.RuneShelf.HitTop,
			HitBottom = UWClassicLayout.RuneShelf.HitBottom,
			CellOriginX = Uw1CellOriginX,
			ClearTop = UWClassicLayout.RuneShelf.ClearTop,
			ClearBottom = UWClassicLayout.RuneShelf.ClearBottom
		};

		/// <summary>The page's top row on the second game's screen, rows from the top (show at y 0xC0).</summary>
		private const int Uw2PageTop = UWClassicLayoutUw2.Panel.Top;

		/// <summary>
		/// The second game: rune 0 where ShowRune draws it (x 0xF1, top y 0xBC, uw2 RUNES.C:65), the region
		/// input_addmouse(0xF0, 0x51, 0x13B, 0xBE) of BeginInventory (uw2 INVPANEL.C:138), the columns from the
		/// region's left (x / 0x12, uw2 RUNES.C:104), the clear strip its rows y &lt; 0x12 (RUNES.C:101).
		/// </summary>
		public static readonly UWRuneTabletGeometry Uw2 = new UWRuneTabletGeometry
		{
			IsSecondGame = true,
			PageWidth = UWTextures.Uw2PanelWidth,
			PageHeight = UWTextures.Uw2PanelHeight,
			FirstSlotLeft = UWClassicLayoutUw2.Runes.Left - UWClassicLayoutUw2.Panel.Left,
			FirstSlotTop = (UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Runes.TopY) - Uw2PageTop,
			HitLeft = UWClassicLayoutUw2.Statistics.Left - UWClassicLayoutUw2.Panel.Left,
			HitRight = UWClassicLayoutUw2.Statistics.AreaRight - UWClassicLayoutUw2.Panel.Left,
			HitTop = (UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Statistics.AreaTopY) - Uw2PageTop,
			HitBottom = (UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Statistics.AreaBottomY) - Uw2PageTop,
			CellOriginX = 0,
			ClearTop = (UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Statistics.AreaBottomY) - Uw2PageTop - UWClassicLayoutUw2.Runes.ClearStrip + 1,
			ClearBottom = (UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Statistics.AreaBottomY) - Uw2PageTop
		};
	}
}
