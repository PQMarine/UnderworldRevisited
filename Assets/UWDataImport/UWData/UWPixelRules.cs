using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// NAMED COLOUR RULES of the HUD art builders (cleanup 9.9, 2026-10-25, CLEANUP-PLAN 8.4): what
	/// counts as black, as the frame's backdrop, as rust, chain or wood. Each rule was worked out by
	/// eye on the original's pictures with the user and is frozen by the art hashes
	/// (Pins/hud-art-hashes.txt).
	///
	/// THE THREE BLACKS are named, NOT unified - each is right for its picture: the compass cross's
	/// outline is pure 0/0/0 among stone greys from 28 up (PureBlackSum), the frame's and the
	/// conversation's black edges are the palette's darkest (EdgeBlackSum), the paperdoll's outline
	/// is a dark brown at places (OutlineBlackSum). A sum of the three channels below the limit.
	/// </summary>
	public static class UWPixelRules
	{
		/// <summary>The compass cross's outline (CompassCrossMask, CompassTipKeeps).</summary>
		public const int PureBlackSum = 10;

		/// <summary>The frame's and the conversation's black (Classic Wide's hole, the conversation's
		/// edges and outline).</summary>
		public const int EdgeBlackSum = 16;

		/// <summary>The paperdoll's outline (ComposePaperdoll, FreeOnPage) and the stone ledge's holes.</summary>
		public const int OutlineBlackSum = 30;

		/// <summary>The brightness of a colour (Rec. 601 weights), 0 to 255.</summary>
		public static float Luma(UWColor32 pOColour)
		{
			return (0.299f * pOColour.R) + (0.587f * pOColour.G) + (0.114f * pOColour.B);
		}

		/// <summary>The three channels added, 0 to 765.</summary>
		public static int Sum(UWColor32 pOColour)
		{
			return pOColour.R + pOColour.G + pOColour.B;
		}

		/// <summary>The same colour, alpha included.</summary>
		public static bool Same(UWColor32 pOA, UWColor32 pOB)
		{
			return pOA.R == pOB.R && pOA.G == pOB.G && pOA.B == pOB.B && pOA.A == pOB.A;
		}

		/// <summary>The same colour, alpha left out (a picture against the page it was painted on).</summary>
		public static bool SameRgb(UWColor32 pOA, UWColor32 pOB)
		{
			return pOA.R == pOB.R && pOA.G == pOB.G && pOA.B == pOB.B;
		}

		/// <summary>The squared colour difference of two pixels (the texture synthesis' cost).</summary>
		public static long Distance(UWColor32 pOA, UWColor32 pOB)
		{
			int liR = pOA.R - pOB.R;
			int liG = pOA.G - pOB.G;
			int liB = pOA.B - pOB.B;

			return (liR * liR) + (liG * liG) + (liB * liB);
		}

		/// <summary>Visible and pure black (PureBlackSum).</summary>
		public static bool IsPureBlack(UWColor32 pOColour)
		{
			return pOColour.A > 0 && Sum(pOColour) < PureBlackSum;
		}

		/// <summary>The frame's black (EdgeBlackSum), alpha left out.</summary>
		public static bool IsEdgeBlack(UWColor32 pOColour)
		{
			return Sum(pOColour) < EdgeBlackSum;
		}

		/// <summary>The paperdoll's outline black (OutlineBlackSum), alpha left out.</summary>
		public static bool IsOutlineBlack(UWColor32 pOColour)
		{
			return Sum(pOColour) < OutlineBlackSum;
		}

		/// <summary>Visible and not the frame's black: a coloured pixel an outline goes round.</summary>
		public static bool IsColoured(UWColor32 pOColour)
		{
			return pOColour.A > 0 && Sum(pOColour) >= EdgeBlackSum;
		}

		/// <summary>The original's backdrop behind the flasks and the gem: transparent, black, and
		/// the dark blue-grey (28,28,36) the panels dither with.</summary>
		public static bool IsBackdrop(UWColor32 pOColour)
		{
			return pOColour.A == 0 || (pOColour.R == pOColour.G && pOColour.R <= 28 && pOColour.B - pOColour.R <= 8);
		}

		/// <summary>A rust mark on the frame's stone or bar: red well over blue and over green.</summary>
		public static bool IsRust(UWColor32 pOColour)
		{
			return pOColour.R > pOColour.B + 12 && pOColour.R > pOColour.G + 4;
		}

		/// <summary>The conversation's upper rollers' chain (per user, 2026-10-10: "the upper ones
		/// hang on a chain"): its cool links - bluish grey, the top ones blue-green (5C6C70, 405458,
		/// 38484C; per user, the same day, they were missing) - blue over red, green not under red by
		/// more than 20; not the frame's neutral grey stone beside it (per user: a pixel too many).</summary>
		public static bool IsChain(UWColor32 pOColour)
		{
			return pOColour.A > 0 && pOColour.B > pOColour.R && pOColour.G >= pOColour.R && pOColour.G - pOColour.R <= 20
				&& pOColour.R + pOColour.G + pOColour.B > 100;
		}

		/// <summary>The conversation rollers' wood: warm - red over blue by more than 10, not under
		/// green, not black (per user, 2026-10-10: a stricter 30 left holes, the dark browns 24/18/0C
		/// and 24/14/0C).</summary>
		public static bool IsWood(UWColor32 pOColour)
		{
			return pOColour.A > 0 && pOColour.R - pOColour.B > 10 && pOColour.R >= pOColour.G && pOColour.R + pOColour.G + pOColour.B > 40;
		}
	}
}
