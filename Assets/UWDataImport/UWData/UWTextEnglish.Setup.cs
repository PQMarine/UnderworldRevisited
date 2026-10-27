namespace UWDataImport.UWData
{
	public static partial class UWTextEnglish
	{
		/// <summary>The setup bar and its dialogs (cleanup step 8.5, 2026-10-24): the bar's items, each
		/// dialog's own texts in the order of its rows (the page rows' text ids are "setup." and the
		/// row's id), the key and button notices, the folder dialog and the status lines of
		/// UWGameFolderBrowser. The setting rows' own texts are setting.* (UWTextEnglish.Settings.cs).</summary>
		private static void fSetup()
		{
			// The bar.
			fAdd("setup.bar.game", "Game");
			fAdd("setup.bar.motion", "Motion");
			fAdd("setup.bar.graphics", "Graphics");
			fAdd("setup.bar.display", "Display");
			fAdd("setup.bar.controls", "Controls");
			fAdd("setup.bar.sound", "Sound");
			fAdd("setup.bar.colours", "Colours");
			fAdd("setup.done", "Done");

			// The values of the slider rows that are words.
			fAdd("setup.format.steps_off", "Off");
			fAdd("setup.format.steps", "{0} steps");

			// Game.
			fAdd("setup.game.title", "Game");
			fAdd("setup.game.folder", "Game folder...");
			fAdd("setup.game.quit", "Quit");

			// Motion.
			fAdd("setup.motion.title", "Motion");
			fAdd("setup.motion.hint",
				"How you move. The game uses the movement code of the original. Here you choose how often it calculates and how the picture follows.");
			fAdd("setup.motion.bob.hint",
				"The bobbing of the view and the weapon while walking, and the rocking of the view while swimming: like the original, smoothed with your own strength, or off if it makes you uneasy.");

			// Graphics.
			fAdd("setup.graphics.title", "Graphics");
			fAdd("setup.graphics.detail", "Detail");
			fAdd("setup.graphics.detail.low", "Low");
			fAdd("setup.graphics.detail.medium", "Medium");
			fAdd("setup.graphics.detail.high", "High");
			// VeryHigh, the viewer's own values: a text, no choice (cleanup 8d.3, per user 2026-10-25).
			fAdd("setup.graphics.detail.custom", "Custom");
			fAdd("setup.graphics.detail.hint",
				"Low: the original palette. Medium: Remastered, no effects. High: Remastered, all effects. Custom: your own effects, set under Remastered effects.");
			fAdd("setup.graphics.palette", "Palette effects...");
			fAdd("setup.graphics.remastered", "Remastered effects...");
			fAdd("setup.graphics.view_original", "Original");
			fAdd("setup.graphics.view_orig", "Orig.");
			fAdd("setup.graphics.factor_locked", "4x needs a window 1600 pixels tall");

			// Display.
			fAdd("setup.display.title", "Display");
			fAdd("setup.display.hint.editor",
				"In the editor the Game view decides the size - the choice is kept and takes effect in the built game.");
			fAdd("setup.display.hint", "Takes effect at once and is kept for the next start.");
			fAdd("setup.display.resolution", "Resolution");
			fAdd("setup.display.desktop", "(desktop)");

			// Controls, both tabs.
			fAdd("setup.controls.tab.keyboard", "Keyboard and mouse");
			fAdd("setup.controls.tab.gamepad", "Gamepad");
			fAdd("setup.controls.keyboard.hint",
				"Click an entry to change it, then press the new key or mouse button. Escape keeps the old one. The pointer movement itself is fixed, and the interface panels always use the physical mouse buttons. The gamepad has its own tab.");
			fAdd("setup.controls.press_key", "Press a key or button...");
			fAdd("setup.controls.conflict_key", "This key is also used for \"{0}\".");
			fAdd("setup.controls.back_to_default", "Back to default");
			fAdd("setup.controls.gamepad.hint",
				"Click an entry to change it, then press the new button on the gamepad. Escape on the keyboard keeps the old one. One stick walks, the other looks (in the classic scheme it turns and tilts) - swap them below.");
			fAdd("setup.gamepad.press_button", "Press a button...");
			fAdd("setup.gamepad.conflict_button", "This button is also used for \"{0}\".");
			fAdd("setup.gamepad.no_pad", "no pad");
			fAdd("setup.gamepad.stick_now", "now {0} %");

			// Sound.
			fAdd("setup.sound.title", "Sound");
			fAdd("setup.sound.hint",
				"Sound comes from the emulated AdLib card, as in 1992; the music can also play its MT-32 version on General MIDI. The original only knew on and off, and the same two switches sit in the options panel in the game; the volumes are ours. The two switches belong to the save game, as in the original - a save loaded here brings the state it was saved with, the volumes stay.");
			fAdd("setup.sound.mt32_roms", "MT-32 ROMs: {0}");
			fAdd("setup.sound.mt32_problem", "MT-32: {0}");
			fAdd("setup.sound.mt32_no_roms", "no MT-32 or CM-32L ROMs in that folder");
			fAdd("setup.sound.mt32_missing", "the MT-32 emulation is missing on this system ({0})");
			fAdd("setup.sound.open_folder", "Open folder");
			fAdd("setup.sound.recordings", "Recordings: {0} pieces");
			fAdd("setup.sound.recordings_none", "Recordings: none in this folder");

			// Colours.
			fAdd("setup.colours.title", "Colours");
			fAdd("setup.colours.hint",
				"Pulls apart the colours that are hard to tell apart, for the whole game. It is the usual daltonisation: what the eye loses is put back into the channels it can see.");
			fAdd("setup.colours.flask.health", "Health");
			fAdd("setup.colours.flask.poisoned", "Poisoned");
			fAdd("setup.colours.flask.mana", "Mana");

			// Palette effects.
			fAdd("setup.palette.title", "Palette effects");
			fAdd("setup.palette.drag", "drag here to move");
			fAdd("setup.palette.hint",
				"Our own effects for the original's palette (detail level Low). They only make a surface lighter or darker by the original's shade steps, so every pixel keeps a palette colour - but turned up they quickly take away the look of the original. 0 is off.");
			fAdd("setup.palette.hint.not_low",
				"Our own effects for the original's palette - they show in the detail level Low only, which is not chosen at the moment. 0 is off.");
			fAdd("setup.palette.preset", "Preset");
			fAdd("setup.palette.preset.off", "Off");
			fAdd("setup.palette.preset.subtle", "Subtle");
			fAdd("setup.palette.preset.full", "Full");
			fAdd("setup.palette.preset.own", "Own");

			// The game folder.
			fAdd("setup.folder.title", "Game folder");
			fAdd("setup.folder.hint", "Choose the folder of your GOG installation of Ultima Underworld - the one that contains game.gog.");
			fAdd("setup.folder.found", "Found:  {0}");
			fAdd("setup.folder.drives", "Drives:");
			fAdd("setup.folder.home", "Home");
			fAdd("setup.folder.up", "Up");
			fAdd("setup.folder.none_found", "No GOG installation was found in the usual folders. Please choose it.");
			fAdd("setup.folder.unreadable", "This folder cannot be read: {0}");
			fAdd("setup.folder.usable", "{0} found - this folder can be used.");
			fAdd("setup.folder.no_image", "No {0} in this folder.");
			fAdd("setup.folder.choose", "Choose the drive and folder of your GOG installation.");
			fAdd("setup.folder.preparing", "Preparing the game data...");
			fAdd("setup.folder.no_data", "The game data could not be read from this folder.");
			fAdd("setup.folder.use", "Use this folder");
			fAdd("setup.folder.cancel", "Cancel");
		}
	}
}
