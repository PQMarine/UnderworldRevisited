using System;
using UWDataImport.Presentation;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT EACH SETTING IS TO THE ONE WHO CHANGES IT (cleanup step 8.4, 2026-10-24): the label, the
	/// hint and the choice texts of its row, how its number is shown, when the row is greyed or left
	/// out. Until then every dialog of the setup bar and the layout editor's inspector wrote these
	/// out by hand beside their rows; now the page tables (UWSetupPage) and the inspector's element
	/// lists take them from the entries.
	///
	/// THE TEXTS ARE IDS since step 8.5 (2026-10-24): a stable id each ("setting." and the key,
	/// ".hint", ".choice." and a short name), the English words in the text table
	/// (UWTextEnglish.Settings.cs, the words the rows showed, unchanged - a toggle's leading spaces
	/// are the drawer's, not the text's); the def's Label, Hint and ChoiceLabels read them through
	/// UWText in the language in force.
	///
	/// Only the settings a row of the setup bar or the inspector shows are described; the others
	/// (paths, the help window's state, the pinned panels) keep empty texts.
	/// </summary>
	public static partial class UWUserSettingsTable
	{
		static UWUserSettingsTable()
		{
			fDescribeSetup();
			fDescribeMotion();
			fDescribeLayout();
		}

		/// <summary>A switch: a bool setting, or one kept as 0 or 1 (MotionSmoothing).</summary>
		public static bool ReadSwitch(UWSettingDef pODef, UWUserData pOData)
		{
			if (pODef is UWBoolSettingDef lOBool)
				return lOBool.Read(pOData);

			return ((UWIntSettingDef)pODef).ReadOn(pOData);
		}

		public static void WriteSwitch(UWSettingDef pODef, UWUserData pOData, bool pbOn)
		{
			if (pODef is UWBoolSettingDef lOBool)
				lOBool.Write(pOData, pbOn);
			else
				((UWIntSettingDef)pODef).WriteOn(pOData, pbOn);
		}

		/// <summary>The choice a switch's row shows: 1 for on, 0 for off - the other way round where
		/// the entry is inverted.</summary>
		public static int SwitchIndex(UWSettingDef pODef, UWUserData pOData)
		{
			return ReadSwitch(pODef, pOData) != pODef.Invert ? 1 : 0;
		}

		// ------------------------------------------------- Describing

		/// <summary>The label's id, and the hint's where the row has one.</summary>
		private static void fText(UWSettingDef pODef, bool pbHint = false)
		{
			pODef.LabelId = "setting." + pODef.Key;

			if (pbHint)
				pODef.HintId = "setting." + pODef.Key + ".hint";
		}

		/// <summary>The choice texts' ids; paIds are the short names the ids end with.</summary>
		private static void fChoices(UWSettingDef pODef, string[] paIds)
		{
			pODef.ChoiceIds = new string[paIds.Length];

			for (int liAt = 0; liAt < paIds.Length; liAt++)
				pODef.ChoiceIds[liAt] = "setting." + pODef.Key + ".choice." + paIds[liAt];
		}

		private static readonly string[] msOffOn = { "off", "on" };

		private static readonly string[] msModes = { "off", "original", "smooth" };

		private static void fDescribeSetup()
		{
			// The game.
			fText(GogInstallPath);
			fText(ControlScheme);
			fChoices(ControlScheme, new[] { "original", "modern" });
			fText(ShowOpeningSequence);
			fText(RunInBackground);
			fText(ManualAttributes);
			// The language: its choices are the files found beside the game, listed by the Game window
			// (UWLanguage.Available), not a fixed set.
			fText(Language, true);

			// The colours.
			fText(ColourMode);
			fChoices(ColourMode, new[] { "off", "red", "green", "blue" });

			// The windows' theme and the Stone theme's ground (cleanup phase 8d, 2026-10-25), in the
			// Colours window; the ground is greyed while the theme is Plain, which has none.
			fText(WindowTheme, true);
			fChoices(WindowTheme, new[] { "plain", "stone" });
			fText(WindowBackground);
			fChoices(WindowBackground, new[] { "gradient", "clouds" });
			WindowBackground.EnabledWhen = d => WindowTheme.Read(d) != (int)UWWindowThemeEnum.Plain;
			fText(ColourStrength);
			ColourStrength.Format = UWSettingFormat.TwoDecimals;
			fText(ColourBrightness);
			ColourBrightness.Format = UWSettingFormat.TwoDecimals;
			fText(ColourContrast);
			ColourContrast.Format = UWSettingFormat.TwoDecimals;
			fText(MarkPoison, true);

			// The controls, keyboard and mouse.
			fText(EasyMovementInterval);
			EasyMovementInterval.Format = UWSettingFormat.Milliseconds;
			fText(MouseLookSpeed);
			MouseLookSpeed.Format = UWSettingFormat.Percent;
			fText(MouseInvertY);
			fText(PointerSpeed, true);
			PointerSpeed.Format = UWSettingFormat.Percent;
			fText(WalkSlowToggle);

			// The gamepad.
			fText(GamepadButtonStyle);
			fChoices(GamepadButtonStyle, new[] { "xbox", "playstation", "nintendo", "steamdeck" });
			fText(StickLookSpeed);
			StickLookSpeed.Format = UWSettingFormat.Percent;
			fText(GamepadSwapSticks);
			fText(StickInvertY);
			fText(GamepadButtonHints);
			fText(LeftStickDeadzone);
			LeftStickDeadzone.Format = UWSettingFormat.Percent;
			fText(RightStickDeadzone);
			RightStickDeadzone.Format = UWSettingFormat.Percent;

			// The display.
			fText(DisplayMode);
			fChoices(DisplayMode, new[] { "window", "borderless" });

			// The sound: a volume is greyed while its switch is off.
			fText(MusicEnabled);
			fText(MusicVolume);
			MusicVolume.Format = UWSettingFormat.Percent;
			MusicVolume.EnabledWhen = d => MusicEnabled.Read(d);
			fText(SoundEnabled);
			fText(SoundVolume);
			SoundVolume.Format = UWSettingFormat.Percent;
			SoundVolume.EnabledWhen = d => SoundEnabled.Read(d);
			fText(MusicDevice, true);
			fChoices(MusicDevice, new[] { "adlib", "gm", "mt32" });
			fText(MusicRecordings, true);

			// The graphics: the factor's slider only while the world is at the original's resolution.
			fText(ClassicWidescreen);
			fText(ViewAngle);
			ViewAngle.Format = UWSettingFormat.Degrees;
			fText(OriginalWorldResolution);
			fText(OriginalWorldResolutionFactor);
			OriginalWorldResolutionFactor.Format = UWSettingFormat.Factor;
			OriginalWorldResolutionFactor.VisibleWhen = d => OriginalWorldResolution.Read(d);

			// The palette effects.
			fText(PaletteAmbientOcclusion, true);
			fText(PaletteGrime, true);
			fText(PaletteGrimeTone);
			fChoices(PaletteGrimeTone, new[] { "none", "dirt", "moss", "soot" });
			fText(PaletteDepth, true);
			fText(PaletteCavity, true);
			fText(PaletteGroundShadows, true);
			fText(PaletteGlow);
			fText(PaletteLightSources, true);

			foreach (UWFloatSettingDef lODef in new[] { PaletteAmbientOcclusion, PaletteGrime, PaletteDepth, PaletteCavity, PaletteGroundShadows })
				lODef.Format = UWSettingFormat.ShadeSteps;
		}

		/// <summary>
		/// THE MOTION DIALOG'S SETTINGS, with the explanations the user asked for (2026-10-06: they
		/// "need an explanation in any case"). The ramp belongs to Smooth and Modern - each its own
		/// value, the row of the mode in force shown (Smooth's greyed under Original) -, the picture
		/// and the frame rate to Original: each is greyed under the other modes (per user: a setting
		/// without effect is greyed). A strength row is there only while its mode is Smooth.
		/// </summary>
		private static void fDescribeMotion()
		{
			fText(MotionSmoothing, true);
			fChoices(MotionSmoothing, new[] { "original", "smooth", "modern" });

			fText(SmoothRampTicks, true);
			fChoices(SmoothRampTicks, new[] { "16", "8", "4", "0" });
			SmoothRampTicks.EnabledWhen = d => MotionSmoothing.Read(d) == MotionSmooth;
			SmoothRampTicks.VisibleWhen = d => MotionSmoothing.Read(d) != MotionModern;

			fText(ModernRampTicks, true);
			fChoices(ModernRampTicks, new[] { "16", "8", "4", "0" });
			ModernRampTicks.VisibleWhen = d => MotionSmoothing.Read(d) == MotionModern;

			fText(MotionViewStepped, true);
			fChoices(MotionViewStepped, new[] { "even", "stepped" });
			MotionViewStepped.EnabledWhen = d => !MotionSmoothing.ReadOn(d);

			// 256 ticks a second over each frame length: 64, 32, 21, 16.
			string[] lsFrames = Array.ConvertAll(OriginalFrameChoices, piTicks => (256 / piTicks).ToString());

			fText(OriginalFrameTicks, true);
			fChoices(OriginalFrameTicks, lsFrames);
			OriginalFrameTicks.EnabledWhen = d => !MotionSmoothing.ReadOn(d);

			fMode(HeadBobMode, HeadBobStrength);
			fMode(WeaponJitterMode, WeaponJitterStrength);
			fMode(SwimViewMode, SwimViewStrength);
		}

		/// <summary>An Off/Original/Smooth row and the strength row under it, there only in Smooth.</summary>
		private static void fMode(UWIntSettingDef pOMode, UWFloatSettingDef pOStrength)
		{
			fText(pOMode);
			fChoices(pOMode, msModes);

			fText(pOStrength);
			pOStrength.Format = UWSettingFormat.Percent;
			pOStrength.VisibleWhen = d => pOMode.Read(d) == (int)UWHeadBobRules.ModeEnum.Smooth;
		}

		/// <summary>
		/// THE LAYOUT EDITOR'S SETTINGS (its inspector, UWModernLayoutEditor): a switch is a row of
		/// two named segments (per user, 2026-10-09: "like a property grid"). The stored "hidden"
		/// and "no outline" switches show as Shown On / Off and Outline On / Off; a part's own Shown
		/// row reads On first, so those entries are inverted.
		/// </summary>
		private static void fDescribeLayout()
		{
			fText(ModernUiPercent);
			ModernUiPercent.Format = UWSettingFormat.WholePercent;
			fText(InterfaceFont);
			fChoices(InterfaceFont, new[] { "modern", "original" });

			fText(MinimapHidden);
			fChoices(MinimapHidden, new[] { "on", "off" });
			fText(ActionBarHidden);
			fChoices(ActionBarHidden, new[] { "on", "off" });
			fText(MinimapTurns);
			fChoices(MinimapTurns, new[] { "up", "turns" });

			fShown(ModernCompass);
			fText(ModernCompassDisc);
			fChoices(ModernCompassDisc, new[] { "cross", "disc" });
			fText(ModernCompassOutline);
			fChoices(ModernCompassOutline, msOffOn);

			fText(ModernGemApart);
			fChoices(ModernGemApart, new[] { "shelf", "apart" });
			fText(ModernFlasksApart);
			fChoices(ModernFlasksApart, new[] { "shelf", "apart" });

			fText(ModernConversationOriginal);
			fChoices(ModernConversationOriginal, new[] { "modern", "original" });
			fText(ModernConversationNoOutline);
			fChoices(ModernConversationNoOutline, new[] { "on", "off" });
			fText(ConversationTextPercent);
			ConversationTextPercent.Format = UWSettingFormat.WholePercent;

			fShown(ModernCharacterPage);
			fShown(ModernStatsPanel);
			fShown(ModernRuneTablet);
			fShown(ModernRuneHollow);
			fText(ModernRuneHollowOutline);
			fChoices(ModernRuneHollowOutline, msOffOn);

			fShown(ModernStoneShelf);
			fText(ModernStoneShelfWidth);
			ModernStoneShelfWidth.Format = UWSettingFormat.Number;

			fText(ModernScroll);
			fChoices(ModernScroll, new[] { "modern", "scroll" });
			fText(ModernScrollLines);
			ModernScrollLines.Format = UWSettingFormat.Number;
			fText(ModernMessageLines);
			ModernMessageLines.Format = UWSettingFormat.Number;
		}

		/// <summary>A part shown or not, Shown On / Off - true is the first choice.</summary>
		private static void fShown(UWBoolSettingDef pODef)
		{
			fText(pODef);
			fChoices(pODef, new[] { "on", "off" });
			pODef.Invert = true;
		}
	}
}
