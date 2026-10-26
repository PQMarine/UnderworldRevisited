using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE HUD ART'S BY-EYE THRESHOLDS (cleanup 9.10, 2026-10-25, CLEANUP-PLAN 8.4): the limits
	/// that decide which pixel of the original's pictures stays, goes or takes which shade - each
	/// found on renders with the user and frozen by the art hashes (Pins/hud-art-hashes.txt), so a
	/// change here is a change of a confirmed picture. The places in MAIN.BYT are UWMainFrame's,
	/// what counts as black, rust, wood or chain UWPixelRules'; the texture synthesis' own numbers
	/// (its reach, its cost scale, its three best) are the grow algorithm's (UWTextureSynth, cleanup
	/// 9.14), the fewest pixels compared each builder's call.
	/// </summary>
	public static class UWHudTuning
	{
		/// <summary>The cloud leather (per user, 2026-10-04, variant C).</summary>
		public static class Leather
		{
			/// <summary>The noise range spread over the shades, and the grain.</summary>
			public const float CloudLow = 0.2f;

			public const float CloudHigh = 0.8f;

			public const float CloudGrain = 0.08f;

			/// <summary>The extra dark shade below the calm centre's: the leather's most frequent colour
			/// darker than the centre's darkest but at least this share of its luma (not the near-black
			/// of the scratches).</summary>
			public const float DarkShadeShare = 0.6f;

			/// <summary>The gargoyle's head's pixels brighter than this are not laid on the strip.</summary>
			public const float GargoyleMaxLuma = 240f;
		}

		/// <summary>The slot circle and its freed ring (per user, 2026-10-09).</summary>
		public static class Slot
		{
			/// <summary>The circle cut round: its corners beyond this radius transparent.</summary>
			public const float CircleRadius = 9f;

			/// <summary>The ring's inner edge: inside it the hollow.</summary>
			public const float RingInnerRadius = 5.5f;

			/// <summary>The ring's pixels are darker or lighter than the leather between them.</summary>
			public const float RingDarkLuma = 60f;

			public const float RingLightLuma = 76f;

			/// <summary>The shadow inside a freed slot (and the page's large circles): black, a fifth
			/// opaque.</summary>
			public static readonly UWColor32 Hollow = new UWColor32(0, 0, 0, 52);
		}

		/// <summary>The paperdoll page's freed circles (per user, 2026-10-09).</summary>
		public static class PaperdollMarks
		{
			/// <summary>A circle's pixel stands out from the median of its neighbourhood (this far each
			/// way) by more than this luma.</summary>
			public const float StandOut = 9f;

			public const int MedianReach = 3;

			/// <summary>Circles of this radius and more get the hollow inside.</summary>
			public const float HollowMinRadius = 3f;
		}

		/// <summary>The figure freed from the page (per user, 2026-10-09; UWHudArt.FreeOnPage's
		/// steps).</summary>
		public static class PageFreer
		{
			/// <summary>Step 1: the steps from the page into what does not equal it, and the luma those
			/// pixels need (the legs are outlined in dark brown at places).</summary>
			public const int NearSteps = 2;

			public const float NearLuma = 50f;

			/// <summary>Step 2: the shadow - darker than the page by more than this, at most this many
			/// steps.</summary>
			public const float DarkerBy = 6f;

			public const int ShadowSteps = 6;

			/// <summary>Step 4: an enclosed strip of background - every pixel the page or darker than it
			/// by more than StripBelowPage, at most StripMaxPixels, at most StripMaxThickness wide or
			/// high.</summary>
			public const float StripBelowPage = 20f;

			public const int StripMaxPixels = 40;

			public const int StripMaxThickness = 3;

			/// <summary>Step 5: a speck goes with this many of its eight neighbours gone (a black one
			/// with BlackSpeckGone).</summary>
			public const int SpeckGone = 5;

			public const int BlackSpeckGone = 6;
		}

		/// <summary>The flasks' shelf and the free flasks (per user, 2026-10-09 and 2026-10-10).</summary>
		public static class Shelf
		{
			/// <summary>The frame's wood at the shelf's left end: red over green by more than this.</summary>
			public const int WoodRedOverGreen = 30;

			/// <summary>A loose dither dot above UWMainFrame.ShelfDitherRows: darker than this sum.</summary>
			public const int DitherDotSum = 150;

			/// <summary>Free columns on each side of the gem in the widened middle.</summary>
			public const int GemMargin = 3;

			/// <summary>The gem's pedestal ends 2 rows below the flask pictures (chosen per user from
			/// three heights).</summary>
			public const int GemBottom = 35;

			/// <summary>The flask pictures' rows from which down the shelf's stone shows beside and
			/// under the flask's foot, and how close its channels lie (grey).</summary>
			public const int FlaskStoneTop = 24;

			public const int FlaskGreySpread = 16;
		}

		/// <summary>The stone shelf (per user, 2026-10-09 and 2026-10-10).</summary>
		public static class StoneShelf
		{
			/// <summary>The ledge's holes (the flasks' places): black from row HoleTop to HoleBottom
			/// (the feet's outline on the top surface), else bluish (blue over red by more than
			/// HoleBlueOverRed) or coloured (two channels apart by more than HoleSpread).</summary>
			public const int HoleTop = 3;

			public const int HoleBottom = 9;

			public const int HoleBlueOverRed = 6;

			public const int HoleSpread = 14;

			/// <summary>The back edge's dark dither shade (28/28/36) is bluish too, but stone: in the
			/// top DitherShadeRows rows, below this sum.</summary>
			public const int DitherShadeRows = 3;

			public const int DitherShadeSum = 120;

			/// <summary>The pieces' lengths: PieceMin to PieceMin + PieceRange - 1 columns.</summary>
			public const int PieceMin = 10;

			public const int PieceRange = 9;

			/// <summary>The columns a new piece lies over the shelf so far; the seam runs through them
			/// along the path of the least difference.</summary>
			public const int Overlap = 3;

			/// <summary>A piece matches closely when its cost is within the best's share 1 / this.</summary>
			public const int CloseMatchDivisor = 4;
		}

		/// <summary>Classic Wide's grown columns (per user, 2026-10-10).</summary>
		public static class ClassicWide
		{
			/// <summary>A source row with more than this many tenths of rust is a wooden edge: all of it
			/// counts.</summary>
			public const int WoodRowTenths = 6;
		}

		/// <summary>The compass disc and the freed cross (per user, 2026-10-09 and 2026-10-10).</summary>
		public static class Compass
		{
			/// <summary>The darkest granite the F pixels draw from (the sum of the channels); the
			/// outline and the rim are darker.</summary>
			public const int GraniteMin = 150;

			/// <summary>An F pixel draws from granite at most this many rows away.</summary>
			public const int SourceRows = 6;

			/// <summary>The cross's warm pixels: red minus blue over this.</summary>
			public const int CrossWarm = 25;

			/// <summary>A needle tip's red: red over TipRed and over green by more than TipRedOverGreen.</summary>
			public const int TipRed = 100;

			public const int TipRedOverGreen = 50;
		}
	}
}
