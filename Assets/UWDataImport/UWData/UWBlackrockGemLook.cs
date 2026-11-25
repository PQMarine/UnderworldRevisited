namespace UWDataImport.UWData
{
	/// <summary>
	/// THE LARGE BLACKROCK GEM'S COLOURS (do_rect for model 0x1E, uw2 3d/DRAWOBJ.C:381; per user 2026-11-16: in the original
	/// the gem changes its colour and the unlocked facets glow). The model's eight facets take colour registers 0, 1, 2
	/// and 0xC to 0x10 (facet n in that order), its body register 3. A facet whose gem is used (QB_GEMS_USED bit n) glows
	/// 0x4D, the facet the gem shows (vars[6] &amp; 7) is 0x4F, the others are dark 0x52; the body pulses 0x53 to 0x56 and
	/// back, a step every 64 ticks of the 1/256 s game clock. Once every gem is used (0xFF) the facets blink between
	/// 0x4C and 0x4F every 128 ticks, alternating, and the body is 0x50.
	/// </summary>
	public static class UWBlackrockGemLook
	{
		/// <summary>The large blackrock gem's model.</summary>
		public const int Model = 0x1E;

		public const int BodyRegister = 3;

		public const int FacetCount = 8;

		public const int UsedFacet = 0x4D;

		public const int ShownFacet = 0x4F;

		public const int DarkFacet = 0x52;

		public const int FirstBodyColour = 0x53;

		public const int AllUsedBody = 0x50;

		public const int AllUsedFacet = 0x4C;

		public const int AllUsed = 0xFF;

		/// <summary>The facet a colour register paints, -1 for none: 0, 1, 2, then 0xC to 0x10 for facets 3 to 7.</summary>
		public static int FacetOf(int piRegister)
		{
			if (piRegister >= 0 && piRegister <= 2)
				return piRegister;

			if (piRegister >= 0xC && piRegister <= 0x10)
				return piRegister - 0xC + 3;

			return -1;
		}

		/// <summary>The palette colour of a register, or -1 when the gem does not set it (the model's own record then).
		/// piGemsUsed the quest byte, piShownFacet vars[6], piGameTime the 1/256 s clock.</summary>
		public static int Colour(int piRegister, int piGemsUsed, int piShownFacet, long plGameTime)
		{
			bool lbAllUsed = (piGemsUsed & 0xFF) == AllUsed;

			if (piRegister == BodyRegister)
			{
				if (lbAllUsed)
					return AllUsedBody;

				// The clock step of every colour animated by the clock (do_rect's flag 0x80, UWMoongateLook).
				return FirstBodyColour + UWMoongateLook.ClockStep(plGameTime);
			}

			int liFacet = FacetOf(piRegister);

			if (liFacet < 0)
				return -1;

			if (lbAllUsed)
				return (((liFacet + (int)((plGameTime >> 7) & 1)) & 1) * 3) + AllUsedFacet;

			if (((1 << liFacet) & piGemsUsed) != 0)
				return UsedFacet;

			return (piShownFacet & 7) == liFacet ? ShownFacet : DarkFacet;
		}
	}
}
