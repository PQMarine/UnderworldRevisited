namespace UWDataImport.UWData
{
	public static partial class UWTextEnglish
	{
		/// <summary>The setting rows (cleanup step 8.5, 2026-10-24): label, hint and choices of each
		/// setting a row of the setup bar or the layout editor's inspector shows, by the ids
		/// UWUserSettingsTable gives them ("setting." and the key, ".hint", ".choice." and a short
		/// name). Moved here from UWUserSettingsTable.Rows.cs unchanged.</summary>
		private static void fSettings()
		{
			// The game.
			fAdd("setting.GogInstallPath", "Game folder");
			fAdd("setting.ControlScheme", "Scheme");
			fAdd("setting.ControlScheme.choice.original", "Original");
			fAdd("setting.ControlScheme.choice.modern", "Modern");
			fAdd("setting.ShowOpeningSequence", "Logos and intro at startup");
			fAdd("setting.RunInBackground", "Keep running in the background");
			fAdd("setting.ManualAttributes", "Choose attributes at creation");
			fAdd("setting.Language", "Language");
			fAdd("setting.Language.hint", "Takes effect at once; lines already in the message scroll stay as they were. A language is what lies in the Languages folder beside the game: xx.tsv for the port's own texts, xx.strings.tsv for the game's texts; what a file does not hold stays English.");
			fAdd("setting.Language.choice.english", "English");
			fAdd("setting.Game", "Game");
			fAdd("setting.Game.hint", "Which of the games in the installation runs; a switch starts it from its main menu, and only the main menu offers it. Ultima Underworld II offers a walk through its world so far: Create Character starts it.");
			fAdd("setting.SaveFolder", "Save games");
			fAdd("setting.SaveFolder.hint", "Where the save games of the GOG installation are, for both games. Automatic takes the folder holding the newest save - where DOSBox wrote last -, and without one cloud_saves when GOG's DOSBox configuration mounts it. A DOSBox of your own may write to the installation's folder instead.");
			fAdd("setting.SaveFolder.choice.automatic", "Automatic");
			fAdd("setting.SaveFolder.choice.cloudsaves", "cloud_saves");
			fAdd("setting.SaveFolder.choice.installation", "Installation folder");
			fAdd("setting.Game.choice.uw1", "Ultima Underworld");
			fAdd("setting.Game.choice.uw2", "Ultima Underworld II");

			// The colours.
			fAdd("setting.ColourMode", "Colour help");
			fAdd("setting.ColourMode.choice.off", "Off");
			fAdd("setting.ColourMode.choice.red", "Red (protanopia)");
			fAdd("setting.ColourMode.choice.green", "Green (deuteranopia)");
			fAdd("setting.ColourMode.choice.blue", "Blue (tritanopia)");
			fAdd("setting.ColourStrength", "Strength");
			fAdd("setting.ColourBrightness", "Brightness");
			fAdd("setting.ColourContrast", "Contrast");
			fAdd("setting.MarkPoison", "Hatch the health flask while poisoned");
			fAdd("setting.MarkPoison.hint", "The flask is red normally and green while poisoned - the one pair a red-green deficiency cannot tell apart.");

			// The windows' theme (cleanup phase 8d, 2026-10-25).
			fAdd("setting.WindowTheme", "Window theme");
			fAdd("setting.WindowTheme.hint", "The look of these windows: Plain as before, or Stone in the colours of the game's icon.");
			fAdd("setting.WindowTheme.choice.plain", "Plain");
			fAdd("setting.WindowTheme.choice.stone", "Stone");
			fAdd("setting.WindowBackground", "Background");
			fAdd("setting.WindowBackground.choice.gradient", "Gradient");
			fAdd("setting.WindowBackground.choice.clouds", "Clouds");

			// The controls, keyboard and mouse.
			fAdd("setting.EasyMovementInterval", "Easy movement interval");
			fAdd("setting.MouseLookSpeed", "Mouse look speed (modern)");
			fAdd("setting.MouseInvertY", "Invert looking up and down (modern)");
			fAdd("setting.PointerSpeed", "Pointer speed");
			fAdd("setting.PointerSpeed.hint", "In the game the mouse moves the game's own pointer: at 100 % one step of the mouse is one pixel on the screen, without acceleration. The menus use the system's pointer.");
			fAdd("setting.WalkSlowToggle", "Slow walk key switches on and off instead of being held (modern)");

			// The gamepad.
			fAdd("setting.GamepadButtonStyle", "Button names");
			fAdd("setting.GamepadButtonStyle.choice.xbox", "Xbox");
			fAdd("setting.GamepadButtonStyle.choice.playstation", "PlayStation");
			fAdd("setting.GamepadButtonStyle.choice.nintendo", "Nintendo");
			fAdd("setting.GamepadButtonStyle.choice.steamdeck", "Steam Deck");
			fAdd("setting.StickLookSpeed", "Stick look speed");
			fAdd("setting.GamepadSwapSticks", "Swap the sticks (left-handed)");
			fAdd("setting.StickInvertY", "Invert looking up and down");
			fAdd("setting.GamepadButtonHints", "Button hints in the game (crosshair, action bar)");
			fAdd("setting.LeftStickDeadzone", "Left stick deadzone");
			fAdd("setting.RightStickDeadzone", "Right stick deadzone");

			// The display.
			fAdd("setting.DisplayMode", "Mode");
			fAdd("setting.DisplayMode.choice.window", "Window");
			fAdd("setting.DisplayMode.choice.borderless", "Borderless fullscreen");

			// The sound.
			fAdd("setting.MusicEnabled", "Music");
			fAdd("setting.MusicVolume", "Music volume");
			fAdd("setting.SoundEnabled", "Sound effects");
			fAdd("setting.SoundVolume", "Sound volume");
			fAdd("setting.MusicDevice", "Music played by");
			fAdd("setting.MusicDevice.hint", "General MIDI: the game's MT-32 music on a soundfont cut from FluidR3 GM (Frank Wen, MIT), its instruments matched to the MT-32's by name - close, not the original's sound. MT-32: the same music on Munt's emulation of the Roland MT-32, as the game was written for it. Its ROMs belong to Roland and do not come with the game; put your own control and PCM ROM into the folder below.");
			fAdd("setting.MusicDevice.choice.adlib", "AdLib");
			fAdd("setting.MusicDevice.choice.gm", "General MIDI");
			fAdd("setting.MusicDevice.choice.mt32", "MT-32");
			fAdd("setting.MusicRecordings", "Music from recordings");
			fAdd("setting.MusicRecordings.hint", "Your own recordings of the music, one file per piece in the folder below (UW1/Music), play in place of the device above. Name each by the number of the game's piece: 01.ogg for the introduction, 02 to 04 the levels, 05 to 07 combat, 10 Armed, 11 Victory, 12 Death, 13 Fleeing, 15 Maps and Legends (Ogg Vorbis or WAV). A piece without a recording plays on the device.");

			// The graphics.
			fAdd("setting.ClassicWidescreen", "Classic screen widescreen");
			fAdd("setting.ViewAngle", "View");
			fAdd("setting.ViewGamma", "Gamma");
			fAdd("setting.ViewGamma.hint", "Lifts the dark tones of the 3D view, in both modes; the interface keeps its colours. 1.00 is the picture as drawn.");
			fAdd("setting.OriginalWorldResolution", "World at the original's resolution");
			fAdd("setting.OriginalWorldResolutionFactor", "Resolution factor");

			// The palette effects.
			fAdd("setting.PaletteAmbientOcclusion", "Ambient occlusion");
			fAdd("setting.PaletteAmbientOcclusion.hint", "Corners, joints and the floor along the walls get darker.");
			fAdd("setting.PaletteGrime", "Grime along the walls");
			fAdd("setting.PaletteGrime.hint", "Dirt where a wall or a step meets the floor hides the hard edge, in the palette's colour nearest the tone. Grime works in the Remastered mode too.");
			fAdd("setting.PaletteGrimeTone", "Grime colour");
			fAdd("setting.PaletteGrimeTone.choice.none", "No colour");
			fAdd("setting.PaletteGrimeTone.choice.dirt", "Dirt");
			fAdd("setting.PaletteGrimeTone.choice.moss", "Moss");
			fAdd("setting.PaletteGrimeTone.choice.soot", "Soot");
			fAdd("setting.PaletteDepth", "Depth");
			fAdd("setting.PaletteDepth.hint", "Stones stand out from their joints and shift against them as you move, with their shadow.");
			fAdd("setting.PaletteCavity", "Hollows");
			fAdd("setting.PaletteCavity.hint", "Joints and recesses get darker.");
			fAdd("setting.PaletteGroundShadows", "Ground shadows");
			fAdd("setting.PaletteGroundShadows.hint", "A dark patch under creatures and things, so they stand on the ground.");
			fAdd("setting.PaletteGlow", "Glow - lava and flames keep their full colour, in the dark too");
			fAdd("setting.PaletteLightSources", "Light sources");
			fAdd("setting.PaletteLightSources.hint", "Torches, candles, lanterns, fires, lava, glowing stones, mushrooms and orbs light their surroundings by the original's own light table, walls cast shadows, fire flickers. The original lights only from you, so lit places beyond your light show here, up to its farthest drawing distance.");

			// The Motion dialog (the explanations the user asked for, 2026-10-06).
			fAdd("setting.MotionSmoothing", "Motion");
			fAdd("setting.MotionSmoothing.hint", "Original: calculates like the original, including its rounding. Smooth: the same rules without the rounding. Smooth moves the same at every frame rate and may be easier on the stomach. Modern: you slide along walls and round corners, the view stays where you look, sideways and backwards are almost as fast as forwards. Jumps, steps and ledges stay as in the original.");
			fAdd("setting.MotionSmoothing.choice.original", "Original");
			fAdd("setting.MotionSmoothing.choice.smooth", "Smooth");
			fAdd("setting.MotionSmoothing.choice.modern", "Modern");
			fAdd("setting.SmoothRampTicks", "Response");
			fAdd("setting.SmoothRampTicks.hint", "How long it takes to get going and to stop in Smooth. 0.6 s is the original on a slow PC, 0.3 s on the PC that DOSBox at 30000 cycles stands for. Shorter feels more direct. Original uses its own frame rate for this.");
			fAdd("setting.SmoothRampTicks.choice.16", "0.6 s");
			fAdd("setting.SmoothRampTicks.choice.8", "0.3 s");
			fAdd("setting.SmoothRampTicks.choice.4", "0.15 s");
			fAdd("setting.SmoothRampTicks.choice.0", "At once");
			fAdd("setting.ModernRampTicks", "Response");
			fAdd("setting.ModernRampTicks.hint", "How long it takes to get going and to stop in Modern, a value of its own beside Smooth's.");
			fAdd("setting.ModernRampTicks.choice.16", "0.6 s");
			fAdd("setting.ModernRampTicks.choice.8", "0.3 s");
			fAdd("setting.ModernRampTicks.choice.4", "0.15 s");
			fAdd("setting.ModernRampTicks.choice.0", "At once");
			fAdd("setting.MotionViewStepped", "Picture");
			fAdd("setting.MotionViewStepped.hint", "Only for Original. Even: the picture moves a little with every screen refresh. Stepped: the picture only moves when the game has calculated, like on an old PC. Smooth is always even.");
			fAdd("setting.MotionViewStepped.choice.even", "Even");
			fAdd("setting.MotionViewStepped.choice.stepped", "Stepped");
			fAdd("setting.OriginalFrameTicks", "Original fps");
			fAdd("setting.OriginalFrameTicks.hint", "How often per second the Original mode calculates. More is not faster. The original rounds down after every calculation, so with 64 you move a bit slower when swimming or walking backwards, and you reach full speed sooner. With 16 it is the other way round. 32 is like the original in DOSBox at 30000 cycles. Smooth does not use this setting.");
			fAdd("setting.OriginalFrameTicks.choice.64", "64");
			fAdd("setting.OriginalFrameTicks.choice.32", "32");
			fAdd("setting.OriginalFrameTicks.choice.21", "21");
			fAdd("setting.OriginalFrameTicks.choice.16", "16");
			fAdd("setting.HeadBobMode", "Head bob");
			fAdd("setting.HeadBobMode.choice.off", "Off");
			fAdd("setting.HeadBobMode.choice.original", "Original");
			fAdd("setting.HeadBobMode.choice.smooth", "Smooth");
			fAdd("setting.HeadBobStrength", "Strength");
			fAdd("setting.WeaponJitterMode", "Weapon");
			fAdd("setting.WeaponJitterMode.choice.off", "Off");
			fAdd("setting.WeaponJitterMode.choice.original", "Original");
			fAdd("setting.WeaponJitterMode.choice.smooth", "Smooth");
			fAdd("setting.WeaponJitterStrength", "Strength");
			fAdd("setting.SwimViewMode", "Swimming");
			fAdd("setting.SwimViewMode.choice.off", "Off");
			fAdd("setting.SwimViewMode.choice.original", "Original");
			fAdd("setting.SwimViewMode.choice.smooth", "Smooth");
			fAdd("setting.SwimViewStrength", "Strength");

			// The layout editor's inspector.
			fAdd("setting.ModernUiPercent", "UI size");
			fAdd("setting.InterfaceFont", "Font");
			fAdd("setting.InterfaceFont.choice.modern", "Modern");
			fAdd("setting.InterfaceFont.choice.original", "Original");
			fAdd("setting.MinimapHidden", "Shown");
			fAdd("setting.MinimapHidden.choice.on", "On");
			fAdd("setting.MinimapHidden.choice.off", "Off");
			fAdd("setting.ActionBarHidden", "Shown");
			fAdd("setting.ActionBarHidden.choice.on", "On");
			fAdd("setting.ActionBarHidden.choice.off", "Off");
			fAdd("setting.MinimapTurns", "North");
			fAdd("setting.MinimapTurns.choice.up", "Up");
			fAdd("setting.MinimapTurns.choice.turns", "Turns");
			fAdd("setting.ModernCompass", "Shown");
			fAdd("setting.ModernCompass.choice.on", "On");
			fAdd("setting.ModernCompass.choice.off", "Off");
			fAdd("setting.ModernCompassDisc", "Look");
			fAdd("setting.ModernCompassDisc.choice.cross", "Cross");
			fAdd("setting.ModernCompassDisc.choice.disc", "Disc");
			fAdd("setting.ModernCompassOutline", "Outline");
			fAdd("setting.ModernCompassOutline.choice.off", "Off");
			fAdd("setting.ModernCompassOutline.choice.on", "On");
			fAdd("setting.ModernGemApart", "Power gem");
			fAdd("setting.ModernGemApart.choice.shelf", "Shelf");
			fAdd("setting.ModernGemApart.choice.apart", "Apart");
			fAdd("setting.ModernFlasksApart", "Flasks");
			fAdd("setting.ModernFlasksApart.choice.shelf", "Shelf");
			fAdd("setting.ModernFlasksApart.choice.apart", "Apart");
			fAdd("setting.ModernConversationOriginal", "Look");
			fAdd("setting.ModernConversationOriginal.choice.modern", "Modern");
			fAdd("setting.ModernConversationOriginal.choice.original", "Original");
			fAdd("setting.ModernConversationNoOutline", "Outline");
			fAdd("setting.ModernConversationNoOutline.choice.on", "On");
			fAdd("setting.ModernConversationNoOutline.choice.off", "Off");
			fAdd("setting.ConversationTextPercent", "Text");
			fAdd("setting.ModernCharacterPage", "Shown");
			fAdd("setting.ModernCharacterPage.choice.on", "On");
			fAdd("setting.ModernCharacterPage.choice.off", "Off");
			fAdd("setting.ModernStatsPanel", "Shown");
			fAdd("setting.ModernStatsPanel.choice.on", "On");
			fAdd("setting.ModernStatsPanel.choice.off", "Off");
			fAdd("setting.ModernRuneTablet", "Shown");
			fAdd("setting.ModernRuneTablet.choice.on", "On");
			fAdd("setting.ModernRuneTablet.choice.off", "Off");
			fAdd("setting.ModernRuneHollow", "Shown");
			fAdd("setting.ModernRuneHollow.choice.on", "On");
			fAdd("setting.ModernRuneHollow.choice.off", "Off");
			fAdd("setting.ModernRuneHollowOutline", "Outline");
			fAdd("setting.ModernRuneHollowOutline.choice.off", "Off");
			fAdd("setting.ModernRuneHollowOutline.choice.on", "On");
			fAdd("setting.ModernStoneShelf", "Shown");
			fAdd("setting.ModernStoneShelf.choice.on", "On");
			fAdd("setting.ModernStoneShelf.choice.off", "Off");
			fAdd("setting.ModernStoneShelfWidth", "Width");
			fAdd("setting.ModernScroll", "Style");
			fAdd("setting.ModernScroll.choice.modern", "Modern");
			fAdd("setting.ModernScroll.choice.scroll", "Scroll");
			fAdd("setting.ModernScrollLines", "Lines");
			fAdd("setting.ModernMessageLines", "Lines");

			// The inspector's Preset row (UWLayoutPresets.StartIds, the ids of step 8.4).
			fAdd("layout.preset.custom", "Custom");
			fAdd("layout.preset.modern", "Modern");
			fAdd("layout.preset.classicmodular", "Classic Modular");
		}
	}
}
