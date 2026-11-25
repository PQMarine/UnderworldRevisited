namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STATS PAGE PER GAME (UW2 plan, "The modern interface for UW2: one interface, two hosts", stage U5 step 1,
	/// 2026-11-23): where the lines of PANELS.GR 2 stand, in which colours, and which strip of the page pages the skill
	/// list. Both games' page is PANELS.GR 2 with the labels STR to EXP in the picture and the same six values, seven
	/// pixels apart; what differs is the page's size, the lines' places, the colours, the list's paging strip and the
	/// list itself (the second game's begins with the skill points, UWCharacterSheet). The modern scheme's stats page
	/// (UWModernStatsPage) reads the running game's from here.
	///
	/// Coordinates are the page's pixels, x from its left, rows from its top; a row is a line's top. A right edge is
	/// where the line ends, as the original's string_to_screen(right - string_width(...)).
	///
	/// THE FIRST GAME'S numbers are UWClassicLayout.Stats (measured on the picture, the edges by eye, the name set
	/// by the user's eye onto the grid), the same the classic page of UWHudPanel draws with. THE SECOND GAME'S come
	/// from RedispStat (uw2 STATS.C) and the panel's mouse region (uw2 INVPANEL.C), the numbers the classic page of
	/// UWGameScreenUw2 draws and clicks with (UWClassicLayoutUw2.Statistics, Panel).
	/// </summary>
	public sealed class UWStatsPageGeometry
	{
		/// <summary>The rows' pitch, for both games (the first game's picture grid, the second game's sp_att and
		/// sp_skill step of 7).</summary>
		public const int RowHeight = 7;

		/// <summary>The skill list's rows on the page, for both games.</summary>
		public const int SkillRows = 6;

		public bool IsSecondGame { get; private set; }

		/// <summary>The page's picture, PANELS.GR 2 (UWTextures' panel sizes).</summary>
		public int PageWidth { get; private set; }

		public int PageHeight { get; private set; }

		/// <summary>The name, centred on this x.</summary>
		public int NameTop { get; private set; }

		public float NameCentreX { get; private set; }

		/// <summary>The class from the left, the level to the right, on one row.</summary>
		public int ClassTop { get; private set; }

		/// <summary>The left edge of the class and the skill names.</summary>
		public int LabelLeft { get; private set; }

		/// <summary>The right edge of the level, the six values and the skills' values.</summary>
		public int ValueRight { get; private set; }

		/// <summary>The first of the six values (strength), the others RowHeight below each other.</summary>
		public int FirstValueTop { get; private set; }

		/// <summary>The skill list's first row, the others RowHeight below each other.</summary>
		public int FirstSkillTop { get; private set; }

		/// <summary>The paging strip: a click from PagingLeft to before PagingRight, from PagingTop to before
		/// PagingBottom pages the list, left of PagingSplit back, from it on forward.</summary>
		public int PagingLeft { get; private set; }

		public int PagingRight { get; private set; }

		public int PagingTop { get; private set; }

		public int PagingBottom { get; private set; }

		public int PagingSplit { get; private set; }

		/// <summary>The header's and the values' colour, and the skill list's: a colour of palette 0 by its index, or
		/// the fixed colour where the index is below 0.</summary>
		public int ValueColourIndex { get; private set; }

		public int SkillColourIndex { get; private set; }

		public UWColor32 FixedValueColour { get; private set; }

		public UWColor32 FixedSkillColour { get; private set; }

		/// <summary>Every pixel of the page opaque (the second game's panel pages, as UWMainFrame.Picture draws its
		/// frame: its near black (0, 0, 4) is no clear colour); the first game's page keeps its clear pixels.</summary>
		public bool IsPageOpaque { get; private set; }

		/// <summary>A value's row (0 strength ... 5 experience) and a skill row's.</summary>
		public int ValueTop(int piRow)
		{
			return FirstValueTop + (piRow * RowHeight);
		}

		public int SkillTop(int piRow)
		{
			return FirstSkillTop + (piRow * RowHeight);
		}

		/// <summary>Which way a click on the page pages the list: -1 back, 1 forward, 0 off the strip.</summary>
		public int PagingStepAt(float pfX, float pfRow)
		{
			if (pfX < PagingLeft || pfX >= PagingRight || pfRow < PagingTop || pfRow >= PagingBottom)
				return 0;

			return pfX < PagingSplit ? -1 : 1;
		}

		/// <summary>The header's and the values' colour, or the skill list's, from the game's palettes.</summary>
		public UWColor32 ValueColour(UWPalettes pOPalettes)
		{
			return fColour(pOPalettes, ValueColourIndex, FixedValueColour);
		}

		public UWColor32 SkillColour(UWPalettes pOPalettes)
		{
			return fColour(pOPalettes, SkillColourIndex, FixedSkillColour);
		}

		private static UWColor32 fColour(UWPalettes pOPalettes, int piIndex, UWColor32 pOFixed)
		{
			if (piIndex < 0)
				return pOFixed;

			UWPalette lOPalette = pOPalettes != null ? pOPalettes.GetPalette(0) : null;

			if (lOPalette == null)
				return pOFixed;

			UWColor32 lOColour = lOPalette.GetUWColor(piIndex);

			lOColour.A = 255;

			return lOColour;
		}

		/// <summary>The measures for the data's game.</summary>
		public static UWStatsPageGeometry Of(UWTextures pOTextures)
		{
			return pOTextures != null && pOTextures.Game == UWGame.Uw2 ? Uw2 : Uw1;
		}

		public static readonly UWStatsPageGeometry Uw1 = new UWStatsPageGeometry
		{
			IsSecondGame = false,
			PageWidth = UWTextures.PanelWidth,
			PageHeight = UWTextures.PanelHeight,
			NameTop = UWClassicLayout.Stats.NameRow,
			NameCentreX = UWTextures.PanelWidth * 0.5f,
			ClassTop = UWClassicLayout.Stats.ClassRow,
			LabelLeft = UWClassicLayout.Stats.LabelLeft,
			ValueRight = UWClassicLayout.Stats.ValueRight,
			FirstValueTop = UWClassicLayout.Stats.FirstRow,
			FirstSkillTop = UWClassicLayout.Stats.SkillFirstRow,
			PagingLeft = UWClassicLayout.Stats.PageBack.X,
			PagingRight = UWClassicLayout.Stats.PageForward.X + UWClassicLayout.Stats.PageForward.Width,
			PagingTop = UWClassicLayout.Stats.PageBack.Y,
			PagingBottom = UWClassicLayout.Stats.PageBack.Y + UWClassicLayout.Stats.PageBack.Height,
			PagingSplit = UWClassicLayout.Stats.PageForward.X,
			ValueColourIndex = -1,
			SkillColourIndex = -1,
			FixedValueColour = UWClassicLayout.Stats.ValueColour,
			FixedSkillColour = UWClassicLayout.Stats.SkillColour,
			IsPageOpaque = false
		};

		/// <summary>A row of the second game's screen, y counted up, as a row of its page (show at y 0xC0).</summary>
		private static int fUw2Row(int piScreenY)
		{
			return (UWClassicLayout.Screen.Height - 1 - piScreenY) - UWClassicLayoutUw2.Panel.Top;
		}

		/// <summary>A column of the second game's screen as a column of its page (show at x 0xEB).</summary>
		private static int fUw2Column(int piScreenX)
		{
			return piScreenX - UWClassicLayoutUw2.Panel.Left;
		}

		/// <summary>
		/// The second game (uw2 STATS.C RedispStat): the name centred in 0x48 from x 0xF0 at y 0xBD; the class from
		/// 0xF0 and the level to 0x135 at 0xB6; strength (2 - 0) * 7 + 0xA1 down to the experience at 0x8C; the six
		/// skill rows from 0x84 down, all of them string tops counted up. The paging strip is mous_in_stat's: the
		/// panel's mouse region (0xF0, 0x51) to 0x13B, its bottom ten rows, split at 0x26 from its left. The colours
		/// 0xC4 and 0xC9.
		/// </summary>
		public static readonly UWStatsPageGeometry Uw2 = new UWStatsPageGeometry
		{
			IsSecondGame = true,
			PageWidth = UWTextures.Uw2PanelWidth,
			PageHeight = UWTextures.Uw2PanelHeight,
			NameTop = fUw2Row(UWClassicLayoutUw2.Statistics.NameY),
			NameCentreX = fUw2Column(UWClassicLayoutUw2.Statistics.Left) + (UWClassicLayoutUw2.Statistics.NameWidth * 0.5f),
			ClassTop = fUw2Row(UWClassicLayoutUw2.Statistics.ClassY),
			LabelLeft = fUw2Column(UWClassicLayoutUw2.Statistics.Left),
			ValueRight = fUw2Column(UWClassicLayoutUw2.Statistics.Right),
			FirstValueTop = fUw2Row((2 * UWClassicLayoutUw2.Statistics.RowStep) + UWClassicLayoutUw2.Statistics.AttributeBaseY),
			FirstSkillTop = fUw2Row(UWClassicLayoutUw2.Statistics.SkillY),
			PagingLeft = fUw2Column(UWClassicLayoutUw2.Statistics.Left),
			PagingRight = fUw2Column(UWClassicLayoutUw2.Statistics.AreaRight) + 1,
			PagingTop = fUw2Row(UWClassicLayoutUw2.Statistics.AreaBottomY + UWClassicLayoutUw2.Statistics.ScrollStrip - 1),
			PagingBottom = fUw2Row(UWClassicLayoutUw2.Statistics.AreaBottomY) + 1,
			PagingSplit = fUw2Column(UWClassicLayoutUw2.Statistics.Left) + UWClassicLayoutUw2.Statistics.ScrollSplit,
			ValueColourIndex = UWClassicLayoutUw2.Statistics.HeaderColour,
			SkillColourIndex = UWClassicLayoutUw2.Statistics.SkillColour,
			FixedValueColour = UWClassicLayout.Stats.ValueColour,
			FixedSkillColour = UWClassicLayout.Stats.SkillColour,
			IsPageOpaque = true
		};
	}
}
