using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MODERN HUD'S FRAMES AND PIECES, cut from the original graphics (look decided per user on
	/// mockups, 2026-10-03, see Todo "Modern control scheme"), engine-free (moved out of
	/// UWModernHudArt 2026-10-05, per user: reusable for UW2 or another engine) - since cleanup 9.11
	/// (2026-10-25) A FACADE over one builder class per piece under HudArt/: UWLeatherBuilder (the
	/// leather and the heading strip), UWSlotBuilder, UWPaperdollBuilder (page, marks, figure),
	/// UWPageFreer, UWShelfBuilder (the flasks' shelf, the flask and gem masks), UWStoneShelfBuilder,
	/// UWFrameCutBuilder (the gem's stand, the rune hollow), UWClassicWideBuilder,
	/// UWConversationArtBuilder and UWCompassDiscBuilder (the modern message scroll's paper,
	/// UWScrollPaperBuilder since cleanup 9.16, is called by UWModernScroll directly). Each has Build
	/// methods that read the game's pictures and Compose cores that work on pictures given, so the
	/// cores run on synthetic input (the self-check's HudArtCompose group). The pixel work they share
	/// is UWPixelOps, UWPixelRules, UWMask, UWMaskText, UWNoise, UWSeededRandom and the grow
	/// algorithm UWTextureSynth (cleanup 9.13 and 9.14), their hand-made masks
	/// UWHudMasks; the frame's places they crop are UWMainFrame's, their by-eye thresholds
	/// UWHudTuning's.
	///
	/// Everything is composed from the palette colours (rows top down, as the files store them)
	/// and keyed there; the engine turns the pictures into textures, with the colour help applied.
	/// The members here keep the names the callers know.
	/// </summary>
	public static class UWHudArt
	{
		// ------------------------------------------------- The leather and the heading strip (UWLeatherBuilder)

		public const int LeatherLeft = UWLeatherBuilder.LeatherLeft;

		public const int LeatherRight = UWLeatherBuilder.LeatherRight;

		public const int LeatherTop = UWLeatherBuilder.LeatherTop;

		public const int LeatherBottom = UWLeatherBuilder.LeatherBottom;

		public const int StripHeight = UWLeatherBuilder.StripHeight;

		public const int StripClosedWidth = UWLeatherBuilder.StripClosedWidth;

		public const int StripMinOpenHalf = UWLeatherBuilder.StripMinOpenHalf;

		public const int BrandMargin = UWLeatherBuilder.BrandMargin;

		public const int BrandTop = UWLeatherBuilder.BrandTop;

		public const int BrandWidth = UWLeatherBuilder.BrandWidth;

		public const int EyesLeftInBrand = UWLeatherBuilder.EyesLeftInBrand;

		public const int EyesTopInBrand = UWLeatherBuilder.EyesTopInBrand;

		public static UWPicture BuildLeather(UWTextures pOTextures, int piWidth, int piHeight, int piVariant = 0)
		{
			return UWLeatherBuilder.Build(pOTextures, piWidth, piHeight, piVariant);
		}

		public static UWPicture BuildLeatherTab(UWTextures pOTextures, int piWidth, int piHeight)
		{
			return UWLeatherBuilder.BuildTab(pOTextures, piWidth, piHeight);
		}

		public static int BrandLeft(int piWidth, int piOpenHalf)
		{
			return UWLeatherBuilder.BrandLeft(piWidth, piOpenHalf);
		}

		public static int BrandCentredLeft(int piWidth)
		{
			return UWLeatherBuilder.BrandCentredLeft(piWidth);
		}

		public static UWPicture BuildHeadingStrip(UWTextures pOTextures, int piWidth, int piOpenHalf, int piClosedWidth = StripClosedWidth)
		{
			return UWLeatherBuilder.BuildHeadingStrip(pOTextures, piWidth, piOpenHalf, piClosedWidth);
		}

		public static UWPicture BuildHeadingStripWithBrandAt(UWTextures pOTextures, int piWidth, int piBrandLeft)
		{
			return UWLeatherBuilder.BuildHeadingStripWithBrandAt(pOTextures, piWidth, piBrandLeft);
		}

		// ------------------------------------------------- The slot circle (UWSlotBuilder)

		public const int SlotSize = UWSlotBuilder.SlotSize;

		public static UWPicture BuildSlotCircle(UWTextures pOTextures)
		{
			return UWSlotBuilder.BuildCircle(pOTextures);
		}

		public static UWPicture BuildSlotRing(UWTextures pOTextures, List<UWColor32> pOCarve = null)
		{
			return UWSlotBuilder.BuildRing(pOTextures, pOCarve);
		}

		// ------------------------------------------------- The page freer (UWPageFreer)

		public static UWPicture FreeOnPage(UWTextures pOTextures, UWPicture pOBody, int piMainX, int piMainY, bool pbLargestOnly = true)
		{
			return UWPageFreer.Free(pOTextures, pOBody, piMainX, piMainY, pbLargestOnly);
		}

		// ------------------------------------------------- The paperdoll page and figure (UWPaperdollBuilder)

		public static readonly UWRectInt PaperdollPage = UWPaperdollBuilder.PaperdollPage;

		public const int PaperdollPageWidth = UWPaperdollBuilder.PaperdollPageWidth;

		public const int PaperdollShift = UWPaperdollBuilder.PaperdollShift;

		public const int PaperdollBodyX = UWPaperdollBuilder.PaperdollBodyX;

		public const int PaperdollBodyY = UWPaperdollBuilder.PaperdollBodyY;

		public static UWPicture BuildPaperdollPage(UWTextures pOTextures)
		{
			return UWPaperdollBuilder.BuildPage(pOTextures);
		}

		public static int PageColumnToMain(int piColumn)
		{
			return UWPaperdollBuilder.PageColumnToMain(piColumn);
		}

		public static UWPicture BuildPaperdollMarks(UWTextures pOTextures, List<UWColor32> pOCarve = null)
		{
			return UWPaperdollBuilder.BuildMarks(pOTextures, pOCarve);
		}

		public static UWPicture ComposePaperdoll(UWTextures pOTextures, int piBody, IList<UWPicture> pOArmour, IList<(int X, int Y)> pOAt,
			out int piOriginX, out int piOriginY)
		{
			return UWPaperdollBuilder.BuildFigure(pOTextures, piBody, pOArmour, pOAt, out piOriginX, out piOriginY);
		}

		// ------------------------------------------------- The flasks' shelf and masks (UWShelfBuilder)

		public static UWPicture BuildShelf(UWTextures pOTextures, out UWShelfBuilder.ShelfLayout pOLayout, bool pbGemApart = false)
		{
			return UWShelfBuilder.Build(pOTextures, out pOLayout, pbGemApart);
		}

		public static bool[] BuildFlaskMask(UWFlasks pOFlasks)
		{
			return UWShelfBuilder.BuildFlaskMask(pOFlasks);
		}

		public static bool[] BuildFreeFlaskMask(UWFlasks pOFlasks)
		{
			return UWShelfBuilder.BuildFreeFlaskMask(pOFlasks);
		}

		public static bool[] BuildGemMask(UWPowerGem pOGem)
		{
			return UWShelfBuilder.BuildGemMask(pOGem);
		}

		// ------------------------------------------------- The frame cuts (UWFrameCutBuilder)

		public const int GemStandX = UWFrameCutBuilder.GemStandX;

		public const int GemStandY = UWFrameCutBuilder.GemStandY;

		public const int RuneHollowX = UWFrameCutBuilder.RuneHollowX;

		public const int RuneHollowY = UWFrameCutBuilder.RuneHollowY;

		public const int RuneHollowRuneX = UWFrameCutBuilder.RuneHollowRuneX;

		public const int RuneHollowRuneY = UWFrameCutBuilder.RuneHollowRuneY;

		public const int RuneHollowPitch = UWFrameCutBuilder.RuneHollowPitch;

		public static UWPicture BuildGemStand(UWTextures pOTextures, out int piGemX, out int piGemY)
		{
			return UWFrameCutBuilder.BuildGemStand(pOTextures, out piGemX, out piGemY);
		}

		public static UWPicture BuildRuneHollow(UWTextures pOTextures, bool pbOutline)
		{
			return UWFrameCutBuilder.BuildRuneHollow(pOTextures, pbOutline);
		}

		// ------------------------------------------------- The stone shelf (UWStoneShelfBuilder)

		public const int StoneShelfMinWidth = UWStoneShelfBuilder.StoneShelfMinWidth;

		public static UWPicture BuildStoneShelf(UWTextures pOTextures, int piWidth)
		{
			return UWStoneShelfBuilder.Build(pOTextures, piWidth);
		}

		// ------------------------------------------------- Classic Wide (UWClassicWideBuilder)

		public const int ClassicWideTopRows = UWClassicWideBuilder.ClassicWideTopRows;

		public const int ClassicWideLedgeTop = UWClassicWideBuilder.ClassicWideLedgeTop;

		public const int ClassicWideScrollTop = UWClassicWideBuilder.ClassicWideScrollTop;

		public static readonly UWRectInt ClassicWideHole = UWClassicWideBuilder.ClassicWideHole;

		public const int ClassicWideCameraLeft = UWClassicWideBuilder.ClassicWideCameraLeft;

		public const int ClassicWideCameraRight = UWClassicWideBuilder.ClassicWideCameraRight;

		public const int ClassicWideCameraTop = UWClassicWideBuilder.ClassicWideCameraTop;

		public const int ClassicWideCameraBottom = UWClassicWideBuilder.ClassicWideCameraBottom;

		public static int ClassicWideShiftX(int piX, int piY, int piExtra)
		{
			return UWClassicWideBuilder.ClassicWideShiftX(piX, piY, piExtra);
		}

		public static int ClassicWideCut(int piRow, int piWhich)
		{
			return UWClassicWideBuilder.ClassicWideCut(piRow, piWhich);
		}

		public static UWClassicWideBuilder.ClassicWideFrame BuildClassicWideFrame(UWTextures pOTextures, int piExtra)
		{
			return UWClassicWideBuilder.Build(pOTextures, piExtra);
		}

		// ------------------------------------------------- The conversation's pieces (UWConversationArtBuilder)

		public static readonly UWRectInt ConversationHeader = UWConversationArtBuilder.ConversationHeader;

		public static readonly UWRectInt ConversationParchment = UWConversationArtBuilder.ConversationParchment;

		public static UWPicture BuildConversationHeader(UWTextures pOTextures)
		{
			return UWConversationArtBuilder.BuildHeader(pOTextures);
		}

		public static UWPicture BuildConversationParchment(UWTextures pOTextures, int piRows, bool pbKnobRollers = false)
		{
			return UWConversationArtBuilder.BuildParchment(pOTextures, piRows, pbKnobRollers);
		}

		public static UWPicture PadAndOutline(UWPicture pOPicture, bool pbOutline)
		{
			return UWConversationArtBuilder.PadAndOutline(pOPicture, pbOutline);
		}

		// ------------------------------------------------- The compass disc and cross (UWCompassDiscBuilder)

		public const int CompassDiscX = UWCompassDiscBuilder.CompassDiscX;

		public const int CompassDiscY = UWCompassDiscBuilder.CompassDiscY;

		public static UWPicture BuildCompassDisc(UWTextures pOTextures, bool pbOutline)
		{
			return UWCompassDiscBuilder.Build(pOTextures, pbOutline);
		}

		public static int CompassDiscArrowAt(int piX, int piY)
		{
			return UWCompassDiscBuilder.CompassDiscArrowAt(piX, piY);
		}

		public static bool[] CompassCrossMask(UWColor32[] pyPixels, int piWidth, int piHeight)
		{
			return UWCompassDiscBuilder.CompassCrossMask(pyPixels, piWidth, piHeight);
		}

		public static bool CompassTipKeeps(UWColor32 pOPixel)
		{
			return UWCompassDiscBuilder.CompassTipKeeps(pOPixel);
		}
	}
}
