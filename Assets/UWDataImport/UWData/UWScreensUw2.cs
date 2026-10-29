using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S FULL SCREENS (UW2 plan phase 0 step 5, 2026-10-28): BYT.ARK holds them as
	/// 320 x 200 screens of palette indices, 64000 bytes each, compressed (Underworld Exhumed,
	/// docs/FORMATS.md, "Pictures"). Phase 0 shows the main menu only: block 5 with palette 2 of
	/// PALS.DAT (MAINMENU.C:150-160 reads the block and the palette, then draws the OPBTN buttons over
	/// it - the buttons come with the screens of phase 3).
	///
	/// EVERY SCREEN (phase 1 step 4, 2026-10-29): the blocks as their callers name them, each with the
	/// palette it is shown with (FORMATS.md's table: the automap 1, character creation 3, the main menu
	/// 2, the Origin logo 5, the Looking Glass logo 6, the first end screen 7; the conversation screen,
	/// the game screen and the second end screen in the palette of the moment - the game's own, 0).
	/// Blocks 3 and 10 are empty.
	/// </summary>
	public static class UWScreensUw2
	{
		public const string ArchiveName = "BYT.ARK";

		public const int ScreenWidth = 320;

		public const int ScreenHeight = 200;

		public const int MainMenuBlock = 5;

		public const int MainMenuPalette = 2;

		/// <summary>The palette of the moment: the game's own.</summary>
		public const int CurrentPalette = 0;

		public enum ScreenEnum
		{
			Automap = 0,
			CharacterCreation = 1,
			Conversation = 2,
			GameScreen = 4,
			MainMenu = 5,
			OriginLogo = 6,
			LookingGlassLogo = 7,
			EndOfGame = 8,
			EndOfGameSecond = 9
		}

		/// <summary>The palette a screen is shown with.</summary>
		public static int PaletteOf(ScreenEnum peScreen)
		{
			switch (peScreen)
			{
				case ScreenEnum.Automap: return 1;
				case ScreenEnum.CharacterCreation: return 3;
				case ScreenEnum.MainMenu: return MainMenuPalette;
				case ScreenEnum.OriginLogo: return 5;
				case ScreenEnum.LookingGlassLogo: return 6;
				case ScreenEnum.EndOfGame: return 7;
				default: return CurrentPalette;
			}
		}

		/// <summary>A screen of the data folder psDataPath in its palette, opaque; null when a file is
		/// missing or the block is not a full screen.</summary>
		public static UWPicture Screen(string psDataPath, ScreenEnum peScreen)
		{
			string lsArchive = Path.Combine(psDataPath ?? string.Empty, ArchiveName);

			if (!File.Exists(lsArchive))
				return null;

			byte[] lyIndices = UWArk.Read(lsArchive, UWArk.LayoutEnum.Uw2).ReadBlock((int)peScreen);

			if (lyIndices.Length < ScreenWidth * ScreenHeight)
				return null;

			return FromIndices(lyIndices, new UWPalettes(psDataPath).GetPalette(PaletteOf(peScreen)));
		}

		/// <summary>The main menu screen of the data folder psDataPath, opaque; null when a file is
		/// missing or the block is not a full screen.</summary>
		public static UWPicture MainMenu(string psDataPath)
		{
			return Screen(psDataPath, ScreenEnum.MainMenu);
		}

		/// <summary>A screen of palette indices in colours, every pixel opaque: a full screen has no
		/// transparent colour.</summary>
		public static UWPicture FromIndices(byte[] pyIndices, UWPalette pOPalette)
		{
			UWPicture lOPicture = new UWPicture(ScreenWidth, ScreenHeight);

			for (int liAt = 0; liAt < ScreenWidth * ScreenHeight; liAt++)
			{
				UWColor32 lOColour = pOPalette.GetUWColor(pyIndices[liAt]);

				lOPicture.Pixels[liAt] = new UWColor32(lOColour.R, lOColour.G, lOColour.B, 255);
			}

			return lOPicture;
		}
	}
}
