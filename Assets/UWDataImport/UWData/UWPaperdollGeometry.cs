using UWDataImport.Presentation;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PAPERDOLL PAGE'S MEASURES PER GAME (UW2 plan, "The modern interface for UW2: one interface, two hosts",
	/// part U4b step 2, 2026-11-23): where the page lies on the game's 320 x 200 screen, where the body and the
	/// armour are drawn on it, the six places that show a worn thing's own picture, and which place a point of
	/// the page belongs to. The modern character panel reads all of it from here, so the same window shows the
	/// first game's leather page and the second game's marble page.
	///
	/// THE FIRST GAME's numbers are the tables that were there before (UWPaperdollLayout, UWPaperdollBuilder's
	/// page with its widening, UWClickRules.InventorySpotAt). THE SECOND GAME's come from InvDisplay
	/// (UWClassicLayoutUw2.Inventory: the pictures' places and the hit rectangles, y counted up from the screen's
	/// bottom) and the panel's place (UWClassicLayoutUw2.Panel).
	///
	/// Coordinates are the game's screen pixels, rows from the top, the top-left corner of a piece.
	///
	/// THE WHOLE PAGE (UW2 plan stage U5 step 2, 2026-11-23): the backpack's eight places, the weight left, the open
	/// bag's back, its icon and the two arrows, and which spot of the page a point is (SpotAt, the spots of the first
	/// game's table for both) - for the modern scheme's original character page (UWModernInventoryPage). The first
	/// game's numbers are UWClickRules' table and the places that page had as constants; the second game's are
	/// InvDisplay's displays 12 to 22, OpenTheBag's back and the weight's string_to_screen.
	/// </summary>
	public sealed class UWPaperdollGeometry
	{
		/// <summary>The armour pieces in the order the panel hands them over.</summary>
		public const int Helmet = 0;

		public const int Gloves = 1;

		public const int Legs = 2;

		public const int Chest = 3;

		public const int Boots = 4;

		public const int ArmourPieces = 5;

		public bool IsSecondGame { get; private set; }

		/// <summary>The page above the backpack on the game's screen.</summary>
		public UWRectInt Page { get; private set; }

		/// <summary>The page's width as the panel shows it (the first game's is widened to the backpack window's).</summary>
		public int PageWidth { get; private set; }

		/// <summary>How far the widening moves what lies on the page.</summary>
		public int Shift { get; private set; }

		public int BodyX { get; private set; }

		public int BodyY { get; private set; }

		/// <summary>The armour's places, by Helmet to Boots.</summary>
		public int[] ArmourX { get; private set; }

		public int[] ArmourY { get; private set; }

		/// <summary>The order the pieces are drawn in, first to last.</summary>
		public int[] ArmourOrder { get; private set; }

		/// <summary>The figure's middle, round which the panel narrows it to the original's proportion.</summary>
		public float FigureAxisX { get; private set; }

		/// <summary>The rings, hands and shoulders, in UWPaperdollLayout.IconSlots' order of places.</summary>
		public UWPaperdollLayout.IconSlot[] IconSlots { get; private set; }

		/// <summary>The small step up a ring's picture takes (the first game's 4 x 4 ring places).</summary>
		public float RingIconYOffset { get; private set; }

		/// <summary>The page row the panel's Sleep and Track buttons begin at.</summary>
		public int FootButtonTop { get; private set; }

		// ------------------------------------------------- The whole page (stage U5 step 2)

		/// <summary>Where the whole page (PANELS.GR 0, the backpack included) lies on the screen.</summary>
		public int PanelLeft { get; private set; }

		public int PanelTop { get; private set; }

		/// <summary>The backpack's places, 0 to 7 in rows from the top left: the box a click takes (rows from the top,
		/// both ends in) and the middle a thing's picture is centred on.</summary>
		public UWRectInt[] BackpackBoxes { get; private set; }

		public float[] BackpackCentreX { get; private set; }

		public float[] BackpackCentreRow { get; private set; }

		/// <summary>The weight left: the middle of its number, and its colour (a colour of palette 0 by its index, or
		/// the fixed colour below 0).</summary>
		public float WeightCentreX { get; private set; }

		public float WeightCentreRow { get; private set; }

		public int WeightColourIndex { get; private set; }

		public UWColor32 FixedWeightColour { get; private set; }

		/// <summary>The open container's back (INV.GR), its top-left corner.</summary>
		public int BagBackPicture { get; private set; }

		public int BagBackLeft { get; private set; }

		public int BagBackTop { get; private set; }

		/// <summary>The open container's own picture: its left and the row below its bottom.</summary>
		public int BagIconLeft { get; private set; }

		public int BagIconBottom { get; private set; }

		/// <summary>The arrows (BUTTONS.GR): the up one raises the offset; the row below their bottom.</summary>
		public int UpArrowPicture { get; private set; }

		public int DownArrowPicture { get; private set; }

		public int ArrowBottom { get; private set; }

		/// <summary>An arrow's left edge, from the two pictures' widths: the first game's lie by their right edges at
		/// 314, the up one 3 left of the down one; the second game's at InvDisplay 21 and 22.</summary>
		public int ArrowLeft(bool pbUp, int piUpWidth, int piDownWidth)
		{
			if (IsSecondGame)
				return UWClassicLayoutUw2.Inventory.PictureX[pbUp ? UWClassicLayoutUw2.Inventory.UpArrowDisplay : UWClassicLayoutUw2.Inventory.DownArrowDisplay];

			return pbUp ? Uw1ArrowsRight - piDownWidth - Uw1ArrowGap - piUpWidth : Uw1ArrowsRight - piDownWidth;
		}

		/// <summary>The weight's colour from the game's palettes.</summary>
		public UWColor32 WeightColour(UWPalettes pOPalettes)
		{
			if (WeightColourIndex < 0)
				return FixedWeightColour;

			UWPalette lOPalette = pOPalettes != null ? pOPalettes.GetPalette(0) : null;

			if (lOPalette == null)
				return FixedWeightColour;

			UWColor32 lOColour = lOPalette.GetUWColor(WeightColourIndex);

			lOColour.A = 255;

			return lOColour;
		}

		/// <summary>
		/// THE SPOT OF THE PAGE AT A POINT of the screen (rows from the top), None for none: the first game's table
		/// (UWClickRules.InventorySpotAt), the second game's FindInventoryHit over displays 1 to 22 - the first whose
		/// rectangle holds it - each display named by the first game's spot: the paperdoll's by their slot, 12 to 19
		/// the backpack, 20 the open bag, 21 the up arrow (the left one), 22 the down arrow.
		/// </summary>
		public UWClickRules.InventorySpot SpotAt(int piX, int piRow)
		{
			int liY = UWClassicLayout.Screen.Height - 1 - piRow;

			if (!IsSecondGame)
				return UWClickRules.InventorySpotAt(piX, liY);

			for (int liDisplay = 1; liDisplay <= UWClassicLayoutUw2.Inventory.DownArrowDisplay; liDisplay++)
			{
				if (piX >= UWClassicLayoutUw2.Inventory.HitLeft[liDisplay] && piX <= UWClassicLayoutUw2.Inventory.HitRight[liDisplay]
					&& liY >= UWClassicLayoutUw2.Inventory.HitBottom[liDisplay] && liY <= UWClassicLayoutUw2.Inventory.HitTop[liDisplay])
					return fUw2Spot(liDisplay);
			}

			return UWClickRules.InventorySpot.None;
		}

		/// <summary>A second-game display as the first game's spot.</summary>
		private static UWClickRules.InventorySpot fUw2Spot(int piDisplay)
		{
			if (piDisplay >= Uw2FirstBackpackDisplay && piDisplay < Uw2FirstBackpackDisplay + UWInventoryModel.BackpackSize)
				return UWClickRules.InventorySpot.Backpack0 + (piDisplay - Uw2FirstBackpackDisplay);

			if (piDisplay == UWClassicLayoutUw2.Inventory.OpenBagDisplay)
				return UWClickRules.InventorySpot.ContainerIcon;

			if (piDisplay == UWClassicLayoutUw2.Inventory.UpArrowDisplay)
				return UWClickRules.InventorySpot.ScrollLeft;

			if (piDisplay == UWClassicLayoutUw2.Inventory.DownArrowDisplay)
				return UWClickRules.InventorySpot.ScrollRight;

			UWArmorItemMap.BodySlot? lePlace = UWInventoryModel.FromSavegameSlot(UWClassicLayoutUw2.Inventory.DisplayToSlot[piDisplay]);

			for (UWClickRules.InventorySpot leSpot = UWClickRules.InventorySpot.Helmet; leSpot <= UWClickRules.InventorySpot.RingRight; leSpot++)
			{
				if (lePlace.HasValue && UWClickRules.BodySlotOf(leSpot) == lePlace)
					return leSpot;
			}

			return UWClickRules.InventorySpot.None;
		}

		/// <summary>The screen column a column of the shown page stands for.</summary>
		public int ColumnToScreen(int piColumn)
		{
			if (!IsSecondGame)
				return UWPaperdollBuilder.PageColumnToMain(piColumn);

			return Page.X + System.Math.Min(System.Math.Max(piColumn, 0), Page.Width - 1);
		}

		/// <summary>The paperdoll place at a point of the screen (rows from the top), null for none.</summary>
		public UWArmorItemMap.BodySlot? SlotAt(int piX, int piRow)
		{
			// Both tables count y up from the bottom of the 320 x 200 screen.
			int liY = UWClassicLayout.Screen.Height - 1 - piRow;

			if (!IsSecondGame)
				return UWClickRules.BodySlotOf(UWClickRules.InventorySpotAt(piX, liY));

			// FindInventoryHit: the first display whose rectangle holds the point; 1 to 11 are the paperdoll's.
			for (int liDisplay = 1; liDisplay < UWClassicLayoutUw2.Inventory.FirstItemDisplay + IconSlotCount; liDisplay++)
			{
				if (piX >= UWClassicLayoutUw2.Inventory.HitLeft[liDisplay] && piX <= UWClassicLayoutUw2.Inventory.HitRight[liDisplay]
					&& liY >= UWClassicLayoutUw2.Inventory.HitBottom[liDisplay] && liY <= UWClassicLayoutUw2.Inventory.HitTop[liDisplay])
					return UWInventoryModel.FromSavegameSlot(UWClassicLayoutUw2.Inventory.DisplayToSlot[liDisplay]);
			}

			return null;
		}

		private const int IconSlotCount = 6;

		/// <summary>The measures for the data's game.</summary>
		public static UWPaperdollGeometry Of(UWTextures pOTextures)
		{
			return pOTextures != null && pOTextures.Game == UWGame.Uw2 ? Uw2 : Uw1;
		}

		public static readonly UWPaperdollGeometry Uw1 = new UWPaperdollGeometry
		{
			Page = UWClassicLayout.Paperdoll.Page,
			PageWidth = UWPaperdollBuilder.PaperdollPageWidth,
			Shift = UWPaperdollBuilder.PaperdollShift,
			BodyX = UWPaperdollLayout.BodyX,
			BodyY = UWPaperdollLayout.BodyY,
			ArmourX = new[] { UWPaperdollLayout.HelmetX, UWPaperdollLayout.GlovesX, UWPaperdollLayout.LegsX, UWPaperdollLayout.ChestX, UWPaperdollLayout.BootsX },
			ArmourY = new[] { UWPaperdollLayout.HelmetY, UWPaperdollLayout.GlovesY, UWPaperdollLayout.LegsY, UWPaperdollLayout.ChestY, UWPaperdollLayout.BootsY },
			ArmourOrder = new[] { Helmet, Gloves, Legs, Chest, Boots },
			FigureAxisX = Uw1FigureAxisX,
			IconSlots = UWPaperdollLayout.IconSlots,
			RingIconYOffset = UWPaperdollLayout.RingIconYOffset,
			FootButtonTop = Uw1FootButtonTop,
			PanelLeft = UWClassicLayout.Panel.Left,
			PanelTop = UWClassicLayout.Panel.Top,
			BackpackBoxes = fUw1BackpackBoxes(),
			BackpackCentreX = fUw1BackpackCentres(true),
			BackpackCentreRow = fUw1BackpackCentres(false),
			WeightCentreX = UWClassicLayout.Backpack.WeightCentreX,
			WeightCentreRow = UWClassicLayout.Backpack.WeightCentreY,
			WeightColourIndex = -1,
			FixedWeightColour = UWClassicLayout.Backpack.WeightColour,
			BagBackPicture = Uw1BagBackPicture,
			BagBackLeft = Uw1BagBackLeft,
			BagBackTop = Uw1BagBackTop,
			BagIconLeft = Uw1BagIconLeft,
			BagIconBottom = Uw1BagBottom,
			UpArrowPicture = Uw1UpArrowPicture,
			DownArrowPicture = Uw1UpArrowPicture + 1,
			ArrowBottom = Uw1BagBottom
		};

		/// <summary>The first game's container view as the classic frame draws it (UWHudInventory), the places the
		/// modern original page had as constants: the back INV.GR 6 at 236/80, the icon from x 241 ending above row
		/// 80, the arrows BUTTONS.GR 27 and 28 ending at x 314 and above row 80, 3 apart.</summary>
		private const int Uw1BagBackPicture = 6;

		private const int Uw1BagBackLeft = 236;

		private const int Uw1BagBackTop = 80;

		private const int Uw1BagIconLeft = 241;

		private const int Uw1BagBottom = 80;

		private const int Uw1UpArrowPicture = 27;

		private const int Uw1ArrowsRight = 314;

		private const int Uw1ArrowGap = 3;

		/// <summary>The first game's backpack boxes: UWClickRules' table areas, y turned into rows.</summary>
		private static UWRectInt[] fUw1BackpackBoxes()
		{
			UWRectInt[] lOBoxes = new UWRectInt[UWInventoryModel.BackpackSize];

			for (int liSlot = 0; liSlot < lOBoxes.Length; liSlot++)
			{
				UWClickRules.Area lOArea = UWClickRules.BackpackArea(liSlot);

				lOBoxes[liSlot] = new UWRectInt(lOArea.X0, UWClassicLayout.Screen.Height - 1 - lOArea.Y1, lOArea.X1 + 1 - lOArea.X0, lOArea.Y1 + 1 - lOArea.Y0);
			}

			return lOBoxes;
		}

		/// <summary>The first game's pictures' middles: the areas' middles, the odd one a pixel left
		/// (UWClassicLayout.Backpack.OddSlot) - as the modern original page centred them.</summary>
		private static float[] fUw1BackpackCentres(bool pbX)
		{
			float[] lfCentres = new float[UWInventoryModel.BackpackSize];

			for (int liSlot = 0; liSlot < lfCentres.Length; liSlot++)
			{
				UWClickRules.Area lOArea = UWClickRules.BackpackArea(liSlot);
				float lfOdd = liSlot == UWClassicLayout.Backpack.OddSlot ? 1f : 0f;

				lfCentres[liSlot] = pbX ? ((lOArea.X0 + lOArea.X1 + 1) * 0.5f) - lfOdd : (UWClassicLayout.Screen.Height - 0.5f) - ((lOArea.Y0 + lOArea.Y1) * 0.5f);
			}

			return lfCentres;
		}

		/// <summary>The figure's middle on MAIN.BYT (the body picture's, 260 + 36 / 2).</summary>
		private const float Uw1FigureAxisX = 278f;

		private const int Uw1FootButtonTop = 60;

		// ------------------------------------------------- The second game

		/// <summary>The rows of the inventory page (PANELS.GR 0) above the backpack: down to the light line over
		/// the pack's circles, which closes the page as its bottom edge.</summary>
		[UWClassicLayout.Measured("UW2 PANELS.GR 0")]
		public const int Uw2PageRows = 71;

		/// <summary>The buttons' top row on the second game's shorter page.</summary>
		[UWClassicLayout.UserTuned("2026-11-23, set by eye on a rendering")]
		public const int Uw2FootButtonTop = 53;

		/// <summary>InvDisplay's displays of the armour, by Helmet to Boots (DISPLAY_TO_SLOT: display 2 the helmet,
		/// 4 the gloves, 1 the leggings, 3 the chest, 5 the boots).</summary>
		private static readonly int[] msUw2ArmourDisplays = { 2, 4, 1, 3, 5 };

		public static readonly UWPaperdollGeometry Uw2 = fBuildUw2();

		private static int fUw2X(int piDisplay)
		{
			return UWClassicLayoutUw2.Inventory.PictureX[piDisplay];
		}

		private static int fUw2Y(int piDisplay)
		{
			return UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Inventory.PictureY[piDisplay];
		}

		private static UWPaperdollGeometry fBuildUw2()
		{
			int[] liX = new int[ArmourPieces];
			int[] liY = new int[ArmourPieces];

			for (int liPiece = 0; liPiece < ArmourPieces; liPiece++)
			{
				liX[liPiece] = fUw2X(msUw2ArmourDisplays[liPiece]);
				liY[liPiece] = fUw2Y(msUw2ArmourDisplays[liPiece]);
			}

			// ArmorSlots' drawing order (leggings, chest, boots, gloves, helmet) as pieces.
			int[] liOrder = new int[ArmourPieces];

			for (int liAt = 0; liAt < ArmourPieces; liAt++)
				liOrder[liAt] = System.Array.IndexOf(msUw2ArmourDisplays, UWClassicLayoutUw2.Inventory.ArmourDisplays[liAt]);

			// The six places with a thing's own picture, in the first game's order of places, each found by its
			// display's slot. All are 16 x 16 here, the rings too.
			UWPaperdollLayout.IconSlot[] lOSlots = new UWPaperdollLayout.IconSlot[UWPaperdollLayout.IconSlots.Length];

			for (int liSlot = 0; liSlot < lOSlots.Length; liSlot++)
			{
				UWArmorItemMap.BodySlot lePlace = UWPaperdollLayout.IconSlots[liSlot].Slot;

				for (int liDisplay = UWClassicLayoutUw2.Inventory.FirstItemDisplay; liDisplay < UWClassicLayoutUw2.Inventory.FirstItemDisplay + IconSlotCount; liDisplay++)
				{
					if (UWInventoryModel.FromSavegameSlot(UWClassicLayoutUw2.Inventory.DisplayToSlot[liDisplay]) == lePlace)
						lOSlots[liSlot] = new UWPaperdollLayout.IconSlot(lePlace, fUw2X(liDisplay), fUw2Y(liDisplay), UWClassicLayoutUw2.Inventory.PictureWidth[liDisplay]);
				}
			}

			// The backpack: the hit rectangles as boxes (rows from the top), the pictures' middles from their places.
			UWRectInt[] lOBoxes = new UWRectInt[UWInventoryModel.BackpackSize];
			float[] lfCentreX = new float[UWInventoryModel.BackpackSize];
			float[] lfCentreRow = new float[UWInventoryModel.BackpackSize];

			for (int liSlot = 0; liSlot < lOBoxes.Length; liSlot++)
			{
				int liDisplay = Uw2FirstBackpackDisplay + liSlot;

				lOBoxes[liSlot] = new UWRectInt(UWClassicLayoutUw2.Inventory.HitLeft[liDisplay],
					UWClassicLayout.Screen.Height - 1 - UWClassicLayoutUw2.Inventory.HitTop[liDisplay],
					UWClassicLayoutUw2.Inventory.HitRight[liDisplay] + 1 - UWClassicLayoutUw2.Inventory.HitLeft[liDisplay],
					UWClassicLayoutUw2.Inventory.HitTop[liDisplay] + 1 - UWClassicLayoutUw2.Inventory.HitBottom[liDisplay]);
				lfCentreX[liSlot] = fUw2X(liDisplay) + (UWClassicLayoutUw2.Inventory.PictureWidth[liDisplay] * 0.5f);
				lfCentreRow[liSlot] = fUw2Y(liDisplay) + (UWClassicLayoutUw2.Inventory.PictureHeight[liDisplay] * 0.5f);
			}

			return new UWPaperdollGeometry
			{
				IsSecondGame = true,
				Page = new UWRectInt(UWClassicLayoutUw2.Panel.Left, UWClassicLayoutUw2.Panel.Top, Uw2PageWidth, Uw2PageRows),
				PageWidth = Uw2PageWidth,
				Shift = 0,
				BodyX = fUw2X(0),
				BodyY = fUw2Y(0),
				ArmourX = liX,
				ArmourY = liY,
				ArmourOrder = liOrder,
				FigureAxisX = fUw2X(0) + (UWClassicLayoutUw2.Inventory.PictureWidth[0] * 0.5f),
				IconSlots = lOSlots,
				RingIconYOffset = 0f,
				FootButtonTop = Uw2FootButtonTop,
				PanelLeft = UWClassicLayoutUw2.Panel.Left,
				PanelTop = UWClassicLayoutUw2.Panel.Top,
				BackpackBoxes = lOBoxes,
				BackpackCentreX = lfCentreX,
				BackpackCentreRow = lfCentreRow,
				WeightCentreX = UWClassicLayoutUw2.Inventory.WeightCentreX,
				WeightCentreRow = UWClassicLayoutUw2.Inventory.WeightTop + (Uw2WeightFontRows * 0.5f),
				WeightColourIndex = UWClassicLayoutUw2.Inventory.WeightColour,
				FixedWeightColour = UWClassicLayout.Backpack.WeightColour,
				BagBackPicture = UWClassicLayoutUw2.Inventory.OpenBagBackPicture,
				BagBackLeft = UWClassicLayoutUw2.Inventory.OpenBagBackLeft,
				BagBackTop = UWClassicLayoutUw2.Inventory.OpenBagBackTop,
				BagIconLeft = fUw2X(UWClassicLayoutUw2.Inventory.OpenBagDisplay),
				BagIconBottom = fUw2Y(UWClassicLayoutUw2.Inventory.OpenBagDisplay) + UWClassicLayoutUw2.Inventory.PictureHeight[UWClassicLayoutUw2.Inventory.OpenBagDisplay],
				UpArrowPicture = UWClassicLayoutUw2.Inventory.UpArrowPicture,
				DownArrowPicture = UWClassicLayoutUw2.Inventory.DownArrowPicture,
				ArrowBottom = fUw2Y(UWClassicLayoutUw2.Inventory.UpArrowDisplay) + UWClassicLayoutUw2.Inventory.PictureHeight[UWClassicLayoutUw2.Inventory.UpArrowDisplay]
			};
		}

		/// <summary>The backpack's displays, 12 to 19 (DISPLAY_TO_SLOT: slots 11 to 18).</summary>
		private const int Uw2FirstBackpackDisplay = 12;

		/// <summary>The weight's font (FONT5X6P, the second game's classic page draws it in UWFonts' Normal): its rows,
		/// for the number's middle under string_to_screen's top.</summary>
		[UWClassicLayout.Measured("UW2 FONT5X6P.SYS, its height")]
		public const int Uw2WeightFontRows = 6;

		/// <summary>The inventory page's width (PANELS.GR 0).</summary>
		[UWClassicLayout.Measured("UW2 PANELS.GR 0")]
		public const int Uw2PageWidth = 79;
	}
}
