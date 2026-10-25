namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FRAME PICTURE'S PIECES THE HUD ART CROPS (cleanup 9.10, 2026-10-25, CLEANUP-PLAN 8.4):
	/// MAIN.BYT itself and the places in it that only the art builders read - the slot circle, the
	/// flasks' shelf, the gem's stand, the stone ledge, the message scroll's rollers, the compass
	/// disc's crop, Classic Wide's synthesis sources, the paperdoll page's circles, the modern backs'
	/// colour sources. The places other code needs too are UWClassicLayout's; this table is its
	/// sibling for the builders and tags its entries the same way.
	///
	/// Coordinates are MAIN.BYT's pixels, x from the left and y from the TOP.
	/// </summary>
	public static class UWMainFrame
	{
		/// <summary>
		/// The frame picture, MAIN.BYT, as a picture - DECODED ONCE PER MAIN.BYT AND KEPT (cleanup
		/// 9.15, 2026-10-25; until then anew for every piece, 14 times per rebuild of the modern HUD
		/// and on every Classic Wide resize). Shared, so NOT TO BE WRITTEN TO: the builders only read
		/// it (the self-check's HudArtCompose group holds that no Compose core changes the frame
		/// picture given, the HudArt group that the kept one still is MAIN.BYT). Its colours are the
		/// palette's, which do not change after loading - the colour help is applied when a picture
		/// is uploaded (UWUploadPixels) - so only other game data (another MAIN.BYT texture) decodes
		/// anew.
		/// </summary>
		public static UWPicture Picture(UWTextures pOTextures)
		{
			UWTexture lOMain = pOTextures.GetTextureByType(UWTexture.TextureTypes.MAIN, 0);

			lock (msOLock)
			{
				if (msOPicture == null || msOSource != lOMain)
				{
					msOPicture = UWPicture.From(lOMain);
					msOSource = lOMain;
				}

				return msOPicture;
			}
		}

		/// <summary>The MAIN.BYT texture the kept picture was decoded from, and the picture.</summary>
		private static readonly object msOLock = new object();

		private static UWTexture msOSource;

		private static UWPicture msOPicture;

		// ------------------------------------------------- The slot circle

		/// <summary>The top right circle of the backpack panel: its fill (106,61,17) is closest to
		/// the leather (92,52,13); the top left one lies in the panel's highlight. 17 x 17 - the
		/// 18th column is already the panel's frame.</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public static readonly UWRectInt SlotCircle = new UWRectInt(298, 81, 17, 17);

		// ------------------------------------------------- The flasks' shelf

		/// <summary>The shelf's first row: the flasks' (UWClassicLayout.Flasks).</summary>
		[UWClassicLayout.Derived("UWClassicLayout.Flasks.Top")]
		public const int ShelfTop = UWClassicLayout.Flasks.Top;

		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int ShelfBottom = 166;

		/// <summary>The shelf with the vitality flask (drawn at 248,125), up to the mana flask.</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int ShelfLeftX0 = 237;

		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int ShelfLeftX1 = 272;

		/// <summary>The shelf with the mana flask (drawn at 284,125).</summary>
		[UWClassicLayout.Derived("UWClassicLayout.Flasks.ManaLeft")]
		public const int ShelfRightX0 = UWClassicLayout.Flasks.ManaLeft;

		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int ShelfRightX1 = 318;

		/// <summary>The rows of the 12 columns between the flasks that carry the chain ring.</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int ChainRows = 15;

		/// <summary>The power gem's size where MAIN.BYT draws it (UWClassicLayout.PowerGem); its
		/// pedestal is the 6 rows below it.</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int GemWidth = 31;

		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int GemHeight = 12;

		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int PedestalRows = 6;

		/// <summary>Above this row of the shelf the back edge was dithered against the black: the
		/// loose dark dots left there are dirt without it.</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int ShelfDitherRows = 26;

		// ------------------------------------------------- The gem's stand

		/// <summary>The gem with its hexagonal stone collar, from the dome's top (row 139) down to the
		/// collar's lower edge (row 157); the ledge below it is the stone shelf's
		/// (UWHudMasks.GemStand holds its own pixels).</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int GemStandX = 1;

		[UWClassicLayout.Derived("UWClassicLayout.PowerGem.Top")]
		public const int GemStandY = UWClassicLayout.PowerGem.Top;

		[UWClassicLayout.UserTuned("2026-10-10, painted by the user")]
		public static readonly UWRectInt GemStand = new UWRectInt(GemStandX, GemStandY, 37, 19);

		// ------------------------------------------------- The stone ledge

		/// <summary>The whole ledge under the flasks (x 237 to 317), from where its top is solid
		/// stone (row 147; above it the back edge dithers into the black) to the frame's bottom.</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public static readonly UWRectInt StoneLedge = new UWRectInt(237, 147, 81, 19);

		/// <summary>The plain middle between the flasks within the ledge (x 272 to 283), the stone the
		/// flasks' places are filled with.</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int StoneMiddleX = 272 - 237;

		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int StoneMiddleWidth = 12;

		/// <summary>The ledge's ends kept at both sides of every stone shelf: its right end, mirrored
		/// at the left (the left end lies under the frame's wood).</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int StoneCapWidth = 6;

		/// <summary>The upper band of the ledge (the back edge's dither and the dark slope, rows 147
		/// to 157, down to the flasks' feet): there the flasks stood over most of the ledge, so its
		/// real stone is only the left end, the middle between the flasks and the right end, and
		/// pieces of it repeat whatever they are.</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int StoneBandRows = 11;

		// ------------------------------------------------- The message scroll's rollers

		/// <summary>The message scroll's lower rollers, knob to rod (x 1 to 10 and 310 to 319), and
		/// the rows from the one above their top outline to the one under the bottom one (189 to
		/// 197); their top outline (190) lies on the conversation parchment roller's top row.</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int KnobRollerLeftX = 1;

		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int KnobRollerRightX = 310;

		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int KnobRollerTop = 189;

		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int KnobRollerRows = 9;

		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int KnobRollerOutlineTop = 190;

		/// <summary>The message scroll's upper rollers with the chain each hangs on, chain top to
		/// roller bottom (rows 167 to 174; the same columns).</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int KnobUpperTop = 167;

		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int KnobUpperRows = 8;

		// ------------------------------------------------- The compass disc

		/// <summary>The disc's crop, from the cross's top (row 131) to the pedestal's foot (row 166),
		/// the disc's left to its right edge (UWHudMasks.CompassDisc holds its own pixels).</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int CompassMaskX = 107;

		[UWClassicLayout.UserTuned("2026-10-10, painted by the user")]
		public static readonly UWRectInt CompassDisc = new UWRectInt(CompassMaskX, UWClassicLayout.Compass.Top, 63, 36);

		/// <summary>The last row of the disc's top: below it the pedestal's lighter front.</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int CompassDiscTopRow = 156;

		// ------------------------------------------------- Classic Wide's synthesis sources

		/// <summary>Each band's plain source columns (top bar, view, ledge, scroll), as ranges
		/// (first, end): the bar x 72 to 82 and 194 to 203, the left stone x 56 to 103, the paper x
		/// 40 to 109 and 170 to 229.</summary>
		[UWClassicLayout.UserTuned("2026-10-10")]
		public static readonly int[][] ClassicWideSources =
		{
			new[] { 72, 83, 194, 204 },
			new int[0],
			new[] { 56, 104 },
			new[] { 40, 110, 170, 230 }
		};

		// ------------------------------------------------- The paperdoll page

		/// <summary>Page columns from which a column is doubled in the widened page: the leather just
		/// inside the left border (x 239 and 240) and inside the right one (x 310 and 311).</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int PaperdollWidenLeft = 3;

		[UWClassicLayout.Measured("MAIN.BYT")]
		public const int PaperdollWidenRight = 74;

		/// <summary>The page's circles (centre x, y in the page, before the widening, the radius of
		/// their dark line and how far around it a pixel may stand out): shoulders, hands, the two
		/// ring holes (narrow - the sheen crosses the left one).</summary>
		[UWClassicLayout.Measured("MAIN.BYT")]
		public static readonly float[,] PageCircles =
		{
			{ 16.5f, 13.5f, 7.5f, 1.8f }, { 65.5f, 13.5f, 7.5f, 1.8f }, { 13.5f, 35.5f, 7.5f, 1.8f }, { 67.5f, 35.5f, 7.5f, 1.8f },
			{ 25.5f, 51.5f, 1.8f, 0.9f }, { 56.5f, 51.5f, 1.8f, 0.9f }
		};

		// ------------------------------------------------- The modern backs' colours (UWBackdropArt)

		/// <summary>Marble: the frame's blue-grey slate beside the classic compass.</summary>
		[UWClassicLayout.UserTuned("2026-10-09")]
		public static readonly UWRectInt MarbleSource = new UWRectInt(82, 135, 22, 22);

		/// <summary>Stone: the plain grey of the classic compass's pedestal, its top without the light
		/// rim.</summary>
		[UWClassicLayout.UserTuned("2026-10-09")]
		public static readonly UWRectInt StoneSource = new UWRectInt(104, 136, 68, 18);

		/// <summary>Bronze: the character panel's copper.</summary>
		[UWClassicLayout.UserTuned("2026-10-09")]
		public static readonly UWRectInt BronzeSource = new UWRectInt(240, 12, 74, 100);
	}
}
