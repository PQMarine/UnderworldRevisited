using UWDataImport.UWData;

namespace UWDataImport.Presentation
{
	/// <summary>
	/// THE HUD ART'S HAND-MADE MASKS with the places they lie on (cleanup 9.9, 2026-10-25,
	/// CLEANUP-PLAN 8.4): rows top down, one character per pixel of the crop, checked against its
	/// size when made (UWMaskText); the rows stay pinned in Pins/rule-constants.txt. Only the masks
	/// are in the project, never the pictures (original art).
	/// </summary>
	public static class UWHudMasks
	{
		/// <summary>
		/// The gargoyle's head's own pixels in MAIN.BYT (X; UWClassicLayout.Frame.GargoyleHead, its
		/// eyes at 130-133 and 142-145, rows 4 and 5), without what belongs to the frame: the dark
		/// and blue-grey band beside the jaw, the frame's wooden bar below it except the chin, the
		/// corners (per user, 2026-10-04: dark patches beside the teeth). Worked out by a flood from
		/// the crop's sides and bottom through dark and blue pixels, checked on a render.
		/// </summary>
		private static readonly string[] msGargoyleHead =
		{
			".XXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXX.",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			".XXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXX.",
			"..XXXXXXXXXXXXXXXXXXXX..",
			"..XXXXXXXXXXXXXXXXXXXX..",
			"..XXXXXXXXXXXXXXXXXXXX..",
			"...XXXXXXXXXXXXXXXXXX...",
			"...XXXXXXXXXXXXXXXXXX...",
			"...XXXXXXXXXXXXXXXXXX...",
			"..XXXXXXXXXXXXXXXXXXXX..",
			"........XXXXXXXX........",
			"........................"
		};

		public static readonly UWMaskText GargoyleHead = new UWMaskText(msGargoyleHead, ".X", UWClassicLayout.Frame.GargoyleHead);

		/// <summary>
		/// The second game's head (UWClassicLayoutUw2.GargoyleHead; UW2 plan part U4d step 1, 2026-11-23): set by hand from
		/// a luma map of the crop, as the frame's near-black outline (palette index 1) has gaps a flood leaks through. The
		/// head's sides step in below the brow (rows 9 to 17) where the frame's stone bar and its moss lie beside the jaw;
		/// the top row keeps the horns and leaves the black between them out. The frame's moss over the head's left brow (rows 3
		/// to 8) comes along: cut out, the brow showed a notch (per user 2026-11-23). Checked on a render.
		/// </summary>
		private static readonly string[] msGargoyleHeadUw2 =
		{
			"XXX....XXXXXXXXXX....XXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXX",
			".XXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXX.",
			"..XXXXXXXXXXXXXXXXXXXX..",
			"..XXXXXXXXXXXXXXXXXXXX..",
			"..XXXXXXXXXXXXXXXXXXXX..",
			".XXXXXXXXXXXXXXXXXXXXXX.",
			"..XXXXXXXXXXXXXXXXXXXX..",
			"...XXXXXXXXXXXXXXXXXX..."
		};

		public static readonly UWMaskText GargoyleHeadUw2 = new UWMaskText(msGargoyleHeadUw2, ".X", UWClassicLayoutUw2.GargoyleHead.Place);

		/// <summary>
		/// The second game's compass recess (UWClassicLayoutUw2.CompassRecess; UW2 plan stage U6 step 1, 2026-11-23): each
		/// row from its dark outline's left pixel to its right, read from a luma map of the frame - the raised rim's top
		/// corners bevelled (rows 145 to 149), straight below. The outline comes along.
		/// </summary>
		private static readonly string[] msCompassRecessUw2 =
		{
			"......XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX......",
			".....XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.....",
			"....XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX...",
			"...XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX..",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
		};

		public static readonly UWMaskText CompassRecessUw2 = new UWMaskText(msCompassRecessUw2, ".X", UWClassicLayoutUw2.CompassRecess.Place);

		/// <summary>The head's mask for the data's game.</summary>
		public static UWMaskText GargoyleHeadOf(UWTextures pOTextures)
		{
			return pOTextures != null && pOTextures.Game == UWGame.Uw2 ? GargoyleHeadUw2 : GargoyleHead;
		}

		/// <summary>
		/// The power gem's and its collar's own pixels in MAIN.BYT (X; from the dome's top, row 139,
		/// down to the collar's lower edge, row 157), painted by the user in Paint.NET on 2026-10-10
		/// (the collar is hexagonal, per user; its grey is the ledge's around it, so no rule told them
		/// apart).
		/// </summary>
		private static readonly string[] msGemStand =
		{
			"..............XXXXXXXXXX.............",
			".........XXXXXXXXXXXXXXXXXXX.........",
			"......XXXXXXXXXXXXXXXXXXXXXXXXX......",
			".....XXXXXXXXXXXXXXXXXXXXXXXXXXX.....",
			"....XXXXXXXXXXXXXXXXXXXXXXXXXXXXX....",
			"...XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX...",
			"..XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX..",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX..",
			"...XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX....",
			".....XXXXXXXXXXXXXXXXXXXXXXXXXX......",
			".......XXXXXXXXXXXXXXXXXXXXXX........",
			"........XXXXXXXXXXXXXXXXXXXX........."
		};

		public static readonly UWMaskText GemStand = new UWMaskText(msGemStand, ".X",
			UWMainFrame.GemStand);

		/// <summary>
		/// The compass's stone disc's own pixels (X and F) in MAIN.BYT, from the cross's top (row 131)
		/// to the pedestal's foot (row 166), painted by the user in Paint.NET on 2026-10-10 over a
		/// 1:1 picture of the original compass; F marks where the user retouched the cross and the
		/// disc's slate rim away to bare granite (per user, the same day: "the stone disc with the
		/// compass needle retouched away"). The retouched picture is not in the project: the F
		/// pixels are grown anew from the disc's own granite (UWCompassDiscBuilder).
		/// </summary>
		private static readonly string[] msCompassDisc =
		{
			"...............................................................",
			"...............................................................",
			"...............................................................",
			"...............................................................",
			"...............................................................",
			"...................XXXXXFFFFFFFFFFFFFFXXXXXX...................",
			"............XXXXFFFFXXXFFFFFFFFFFFFFFFXFXXXXFFXXXXX............",
			"........XXXXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXFXXXX........",
			"....XXFXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXFXX....",
			"..XXXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXXX..",
			".XXXXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXXXX.",
			"XXXXXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXXXXX",
			"XXXXXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXXXXX",
			"XXXXXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXXXXXX",
			"XXXXXXFFFXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXXXXXX",
			"XXXXXXXXXFXFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFXFFFXXXXXXXXX",
			"XXXXXXXXXXXXXFFFFFFFXXXFFFFFFFFFFFFFFFFXFXXFFFFXXXXFXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXFXXXXXFFFFFFFFFFFFFFFXXXXXXFXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXFFFFFFFFFFFFFFFXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXFFFFFFFFFFFFFFFFXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXFFFFFFFFFFFFFFXFXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXFFFFFFFFFFFFFFXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXFXFFFFFFFFFFFXXXXXXXXXXXXXXXXXXXXXXXXX",
			"XXXXXXXXXXXXXXXXXXXXXXXXXFXFFFFFFFFFFFXXXXXXXXXXXXXXXXXXXXXXXXX",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXFFFFFFFXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXFFFFXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			".XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			"..XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.",
			"...XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX...",
			".....XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX.....",
			".........XXXXX........XXXXXXXXXXXXXXXXXXX........XXXXX.........",
			"........................XXXXXXXXXXXXXXX........................"
		};

		public static readonly UWMaskText CompassDisc = new UWMaskText(msCompassDisc, ".XF",
			UWMainFrame.CompassDisc);
	}
}
