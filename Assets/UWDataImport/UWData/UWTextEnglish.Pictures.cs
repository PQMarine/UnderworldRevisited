namespace UWDataImport.UWData
{
	public static partial class UWTextEnglish
	{
		/// <summary>
		/// THE WORDS PAINTED INTO THE ORIGINAL'S PICTURES (UWPictureWords, Docs/PICTURE-WORDS-PLAN.md,
		/// 2026-11-08): the options panel's buttons and headings, as the pictures spell them. A language whose
		/// text differs gets the picture's lettering painted over and its text written in FONT4X5P's capitals,
		/// broken at blanks to fit the button (a "\n" breaks where the translator wants). English keeps the
		/// pictures as they are. Upper case is made by the game.
		/// </summary>
		private static void fPictures()
		{
			fAdd("picture.options.save_game", "SAVE GAME");
			fAdd("picture.options.restore_game", "RESTORE GAME");
			fAdd("picture.options.music", "MUSIC");
			fAdd("picture.options.sound", "SOUND");
			fAdd("picture.options.detail", "DETAIL");
			fAdd("picture.options.return_to_game", "RETURN TO GAME");
			fAdd("picture.options.quit_game", "QUIT GAME");
			fAdd("picture.options.on", "ON");
			fAdd("picture.options.off", "OFF");
			fAdd("picture.options.cancel", "CANCEL");
			fAdd("picture.options.done", "DONE");
			fAdd("picture.options.low", "LOW");
			fAdd("picture.options.medium", "MEDIUM");
			fAdd("picture.options.high", "HIGH");
			fAdd("picture.options.very_high", "VERY HIGH");
			fAdd("picture.options.yes", "YES");
			fAdd("picture.options.no", "NO");
			fAdd("picture.options.save_title", "SAVE GAME:");
			fAdd("picture.options.restore_title", "RESTORE GAME:");
			fAdd("picture.options.quit_title", "QUIT GAME:");
			fAdd("picture.options.are_you_sure", "ARE YOU SURE?");
			fAdd("picture.options.music_is_on", "MUSIC IS ON.");
			fAdd("picture.options.music_is_off", "MUSIC IS OFF.");
			fAdd("picture.options.sound_is_on", "SOUND IS ON.");
			// The original's picture has no full stop here.
			fAdd("picture.options.sound_is_off", "SOUND IS OFF");
			fAdd("picture.options.turn_music", "TURN MUSIC:");
			fAdd("picture.options.turn_sound", "TURN SOUND:");
			fAdd("picture.options.detail_is_low", "DETAIL LEVEL IS: LOW");
			fAdd("picture.options.detail_is_medium", "DETAIL LEVEL IS: MEDIUM");
			fAdd("picture.options.detail_is_high", "DETAIL LEVEL IS: HIGH");
			fAdd("picture.options.detail_is_very_high", "DETAIL LEVEL IS: VERY HIGH");
			fAdd("picture.options.set_new_level", "SET NEW LEVEL TO:");

			// The stats page's labels (PANELS.GR 2) and the map's words (BLNKMAP.BYT). "Level" keeps its case;
			// the level's number stands beside it.
			fAdd("picture.stats.str", "STR");
			fAdd("picture.stats.dex", "DEX");
			fAdd("picture.stats.int", "INT");
			fAdd("picture.stats.vit", "VIT");
			fAdd("picture.stats.mana", "MANA");
			fAdd("picture.stats.exp", "EXP");
			fAdd("picture.map.level", "Level");
			fAdd("picture.map.erase", "ERASE");
			fAdd("picture.map.close", "CLOSE");

			// The main menu's captions (OPBTN.GR, a decorative script): a language without pictures of its own
			// gets them written in the save list's style (UWMainMenu.fSetButton). Not upper case.
			fAdd("picture.menu.introduction", "Introduction");
			fAdd("picture.menu.create_character", "Create Character");
			fAdd("picture.menu.acknowledgements", "Acknowledgements");
			fAdd("picture.menu.journey_onward", "Journey Onward");
		}
	}
}
