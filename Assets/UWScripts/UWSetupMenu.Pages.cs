using UnityEngine;
using UWDataImport.UWData;
using Defs = UWDataImport.UWData.UWUserSettingsTable;
using Kind = UWSetupRow.KindEnum;

/// <summary>
/// THE SETUP BAR'S DIALOGS AS PAGES (cleanup step 8.4, 2026-10-24; Docs/CLEANUP-PLAN.md 8.7): each
/// dialog's rows from its title down, as a list the one drawer walks (UWSetupPageDrawer). The places
/// and steps are the numbers the dialogs drew with; the texts, choices, formats and the greying of
/// a row come from its setting's entry (UWUserSettingsTable) where the row is a setting. What hangs
/// from a dialog's bottom edge - a list down to it, Back to default, the flask preview, Done - stays
/// with the dialog's frame (fDrawColoursDialog and the others).
///
/// Built once per bar (Awake): the rows' delegates read the bar's own state (the tab, the key being
/// awaited, the mode the Display dialog shows) when they are drawn.
/// </summary>
public partial class UWSetupMenu
{
    private UWSetupPage mOColoursPage;

    private UWSetupPage mOControlsTabsPage;

    private UWSetupPage mOKeyboardPage;

    private UWSetupPage mOGamepadPage;

    private UWSetupPage mOMotionPage;

    private UWSetupPage mODisplayPage;

    private UWSetupPage mOSoundPage;

    private UWSetupPage mOPaletteEffectsPage;

    private UWSetupPage mOFolderPage;

    private UWSetupPage mOGraphicsPage;

    private UWSetupPage mOGamePage;

    /// <summary>The mode and the size the Display dialog shows in this pass, read once before its
    /// rows as the dialog always did (in the editor the saved choice, in the game what is on screen).</summary>
    private UWDisplayMode.ModeEnum meShownMode;

    private Vector2Int mOShownSize;

    private void fBuildPages()
    {
        mOColoursPage = fBuildColoursPage();
        mOControlsTabsPage = fBuildControlsTabsPage();
        mOKeyboardPage = fBuildKeyboardPage();
        mOGamepadPage = fBuildGamepadPage();
        mOMotionPage = fBuildMotionPage();
        mODisplayPage = fBuildDisplayPage();
        mOSoundPage = fBuildSoundPage();
        mOPaletteEffectsPage = fBuildPaletteEffectsPage();
        mOFolderPage = fBuildFolderPage();
        mOGraphicsPage = fBuildGraphicsPage();
        mOGamePage = fBuildGamePage();
    }

    private static UWSetupRow fTitle(string psId, string psText)
    {
        return new UWSetupRow { Kind = Kind.Title, Id = psId, Text = psText, Advance = 28f };
    }

    private static UWSetupRow fHint(string psId, string psText, float pfGap)
    {
        return new UWSetupRow { Kind = Kind.Hint, Id = psId, Text = psText, Advance = pfGap };
    }

    /// <summary>The hint of a setting, greyed with its row.</summary>
    private static UWSetupRow fHint(UWSettingDef pODef, float pfGap)
    {
        return new UWSetupRow { Kind = Kind.Hint, Id = pODef.HintId, Def = pODef, Advance = pfGap };
    }

    /// <summary>A switch over the page's width.</summary>
    private static UWSetupRow fToggle(UWSettingDef pODef, float pfAdvance)
    {
        return new UWSetupRow { Kind = Kind.Toggle, Id = pODef.LabelId, Def = pODef, Advance = pfAdvance };
    }

    // ------------------------------------------------- Colours

    private UWSetupPage fBuildColoursPage()
    {
        return new UWSetupPage("colours")
            .Add(fTitle("colours.title", UWText.T("setup.colours.title")))
            .Add(fHint("colours.hint", UWText.T("setup.colours.hint"), 8f))
            // The four modes as buttons 155 wide every 160, the one in force in the accent colour; a
            // click on it sets it again.
            .Add(new UWSetupRow
            {
                Kind = Kind.Choices, Id = Defs.ColourMode.LabelId, Def = Defs.ColourMode, Width = 160f * 4f, Height = 28f, Gap = 5f,
                Faced = true, PressCurrent = true, Advance = 40f,
                GetIndex = () => (int)UWColourVision.Mode,
                Choose = piMode => fSetColours(piMode, UWColourVision.Strength, UWColourVision.Brightness, UWColourVision.Contrast)
            })
            .Add(fColourSlider(Defs.ColourStrength, 0f, 1f, 34f,
                pfValue => fSetColours((int)UWColourVision.Mode, pfValue, UWColourVision.Brightness, UWColourVision.Contrast), () => UWColourVision.Strength))
            .Add(fColourSlider(Defs.ColourBrightness, 0.5f, 2f, 34f,
                pfValue => fSetColours((int)UWColourVision.Mode, UWColourVision.Strength, pfValue, UWColourVision.Contrast), () => UWColourVision.Brightness))
            .Add(fColourSlider(Defs.ColourContrast, 0.5f, 2f, 40f,
                pfValue => fSetColours((int)UWColourVision.Mode, UWColourVision.Strength, UWColourVision.Brightness, pfValue), () => UWColourVision.Contrast))
            .Add(fToggle(Defs.MarkPoison, 30f))
            .Add(fHint(Defs.MarkPoison, 10f))
            // THE WINDOWS' THEME (cleanup phase 8d, per user 2026-10-25): Plain, the look of before, or
            // Stone in the application icon's colours, and the Stone theme's ground - greyed under
            // Plain (the entry's EnabledWhen). A choice is saved at once and shows at once, this
            // window included (the entries' OnChange, UWGuiTheme.Changed).
            .Add(new UWSetupRow { Kind = Kind.Separator, Id = "colours.separator", Height = 1f, Advance = 8f })
            .Add(new UWSetupRow { Kind = Kind.ChoiceRow, Id = Defs.WindowTheme.LabelId, Def = Defs.WindowTheme, ChoiceColumns = ThemeChoiceColumns, Advance = 30f })
            .Add(new UWSetupRow { Kind = Kind.ChoiceRow, Id = Defs.WindowBackground.LabelId, Def = Defs.WindowBackground, ChoiceColumns = ThemeChoiceColumns, Advance = 30f })
            .Add(fHint(Defs.WindowTheme, 0f));
    }

    /// <summary>The theme rows: the label in the sliders' column (140 wide), the choices from 150.</summary>
    private static readonly UWChoiceColumns ThemeChoiceColumns = new UWChoiceColumns(0f, 140f, 150f, 150f);

    /// <summary>A colour row: the live value of UWColourVision, set through it with the other two
    /// as they stand. The limits are the dialog's own (0 to 1, 0.5 to 2): the settings table keeps
    /// none for the colour entries.</summary>
    private static UWSetupRow fColourSlider(UWFloatSettingDef pODef, float pfMin, float pfMax, float pfAdvance,
        System.Action<float> pOSet, System.Func<float> pOGet)
    {
        return new UWSetupRow
        {
            Kind = Kind.Slider, Id = pODef.LabelId, Def = pODef, SliderColumns = ColourColumns, Min = pfMin, Max = pfMax, Step = 0f,
            Advance = pfAdvance, Get = pOGet, Set = pOSet
        };
    }

    // ------------------------------------------------- Controls

    /// <summary>TWO TABS (per user, 2026-10-07): the gamepad's buttons are bound on their own
    /// (UWSetupMenu.Gamepad.cs). Not switched while a key is awaited.</summary>
    private UWSetupPage fBuildControlsTabsPage()
    {
        return new UWSetupPage("controls.tabs")
            .Add(new UWSetupRow
            {
                Kind = Kind.Item, Id = "controls.tab.keyboard", Text = UWText.T("setup.controls.tab.keyboard"), Width = 200f, Height = 28f,
                EnabledWhen = () => mOListeningFor == null, IsCurrent = () => !mbControlsGamepadTab, Press = () => mbControlsGamepadTab = false
            })
            .Add(new UWSetupRow
            {
                Kind = Kind.Item, Id = "controls.tab.gamepad", Text = UWText.T("setup.controls.tab.gamepad"), X = 210f, Width = 140f, Height = 28f, Advance = 36f,
                EnabledWhen = () => mOListeningFor == null, IsCurrent = () => mbControlsGamepadTab, Press = () => mbControlsGamepadTab = true
            });
    }

    private UWSetupPage fBuildKeyboardPage()
    {
        return new UWSetupPage("controls.keyboard")
            .Add(fHint("controls.keyboard.hint", UWText.T("setup.controls.keyboard.hint"), 12f))
            // The easy movement repeats while a key or one of the three arrows under the compass
            // is held. The original's pace is too fast to aim with (per user, 2026-09-21: "one
            // overshoots the target very easily"), and whether that is the game or the DOSBox
            // cycles is open - so it is a setting here. The slider snaps to the same ten
            // milliseconds as the two arrows beside it, so a round value can be hit at all (per
            // user, 2026-09-21: dragging never landed on 250).
            .Add(new UWSetupRow { Kind = Kind.Slider, Id = Defs.EasyMovementInterval.LabelId, Def = Defs.EasyMovementInterval, SliderColumns = ControlColumns, Advance = 30f })
            // THE MOUSE LOOK'S SPEED of the modern scheme (per user, 2026-10-06: "sehr direkt"),
            // a factor on the tuned default in steps of five per cent, the same row as above.
            .Add(new UWSetupRow { Kind = Kind.Slider, Id = Defs.MouseLookSpeed.LabelId, Def = Defs.MouseLookSpeed, SliderColumns = ControlColumns, Advance = 30f })
            // THE MOUSE LOOK REVERSED up and down, as the gamepad tab offers for the stick (pull
            // request 3 by krispit-dev, 2026-10-13).
            .Add(fToggle(Defs.MouseInvertY, 30f))
            // THE POINTER'S SPEED in the running game, where the mouse moves the game's own pointer by
            // its counts without the system's acceleration (UWPointer, per user 2026-10-25): 25 to 400
            // per cent, 100 one count per screen pixel; the same row as the look speed, its hint below.
            .Add(new UWSetupRow { Kind = Kind.Slider, Id = Defs.PointerSpeed.LabelId, Def = Defs.PointerSpeed, SliderColumns = ControlColumns, Advance = 30f })
            .Add(fHint(Defs.PointerSpeed, 8f))
            // THE SLOW-WALK KEY held or switched (issue 4, 2026-10-21).
            .Add(fToggle(Defs.WalkSlowToggle, 30f));
    }

    private UWSetupPage fBuildGamepadPage()
    {
        return new UWSetupPage("controls.gamepad")
            .Add(fHint("controls.gamepad.hint", UWText.T("setup.controls.gamepad.hint"), 12f))
            // THE BUTTON NAMES: chosen here, never detected (per user: a PlayStation controller often
            // reports as an Xbox one through Steam or DS4Windows).
            .Add(new UWSetupRow
            {
                Kind = Kind.ChoiceRow, Id = Defs.GamepadButtonStyle.LabelId, Def = Defs.GamepadButtonStyle, ChoiceColumns = WideChoiceColumns,
                Advance = 32f, GetIndex = () => (int)UWGamepad.ButtonStyle
            })
            // THE STICK'S LOOK SPEED, the same row as the mouse's on the other tab (the same step of
            // five per cent, from the settings table since cleanup 8.2).
            .Add(new UWSetupRow { Kind = Kind.Slider, Id = Defs.StickLookSpeed.LabelId, Def = Defs.StickLookSpeed, SliderColumns = ControlColumns, Advance = 32f })
            // THE STICKS: swapped for left-handed players, the look reversed up and down. The
            // sticks' presses go with them where the player has not changed them (UWKeyBindings);
            // the swap puts the bindings anew (the entry's OnChange).
            .Add(new UWSetupRow { Kind = Kind.Toggle, Id = Defs.GamepadSwapSticks.LabelId, Def = Defs.GamepadSwapSticks, Width = 300f })
            .Add(new UWSetupRow { Kind = Kind.Toggle, Id = Defs.StickInvertY.LabelId, Def = Defs.StickInvertY, X = 310f, WidthLess = 310f, Advance = 32f })
            // THE BUTTON HINTS in the modern interface (per user, 2026-10-08).
            .Add(fToggle(Defs.GamepadButtonHints, 32f))
            // THE DEADZONES, one per physical stick (per user, 2026-10-08: sooner or later every
            // controller drifts), with the stick's deflection right now beside them - at rest that
            // is the drift, and the deadzone goes a little above it.
            .Add(fDeadzoneRow(Defs.LeftStickDeadzone, true))
            .Add(fDeadzoneRow(Defs.RightStickDeadzone, false))
            .Add(new UWSetupRow { Kind = Kind.Space, Advance = 4f });
    }

    // ------------------------------------------------- Motion

    /// <summary>The motion dialog's rows, inside its scroll view; the choice and strength rows
    /// measure from 8 left of the text and are 16 wider (the panel lORows of before).</summary>
    private UWSetupPage fBuildMotionPage()
    {
        return new UWSetupPage("motion")
            .Add(fTitle("motion.title", UWText.T("setup.motion.title")))
            .Add(fHint("motion.hint", UWText.T("setup.motion.hint"), 10f))
            .Add(fMotionChoice(Defs.MotionSmoothing))
            .Add(fHint(Defs.MotionSmoothing, 10f))
            // THE SMOOTH RAMP (per user, 2026-10-06: "die Verzoegerung beim Anlaufen und Abbremsen
            // wirken dagegen"): how long starting and stopping take in Smooth. Greyed under Original
            // (per user: a setting without effect is greyed), as the fps row is under Smooth.
            .Add(fMotionChoice(Defs.SmoothRampTicks))
            .Add(fHint(Defs.SmoothRampTicks, 10f))
            // The picture belongs to Original: Smooth is always even (greyed otherwise).
            .Add(fMotionChoice(Defs.MotionViewStepped))
            .Add(fHint(Defs.MotionViewStepped, 10f))
            .Add(fMotionChoice(Defs.OriginalFrameTicks))
            .Add(fHint(Defs.OriginalFrameTicks, 12f))
            .Add(new UWSetupRow { Kind = Kind.Separator, Id = "motion.separator", Height = 1f, Advance = 8f })
            // THE HEAD BOB AND THE WEAPON'S JITTER (UWHeadBobRules), three ways each (per user,
            // 2026-09-23): Off for everyone who cannot take it, Original, Smooth - ours - with a
            // strength slider under it.
            .Add(fMotionChoice(Defs.HeadBobMode))
            .Add(fStrengthRow(Defs.HeadBobStrength))
            .Add(fMotionChoice(Defs.WeaponJitterMode))
            .Add(fStrengthRow(Defs.WeaponJitterStrength))
            // THE SWIMMER'S VIEW (UWSwimViewRules), the same three ways (per user, 2026-10-15).
            .Add(fMotionChoice(Defs.SwimViewMode))
            .Add(fStrengthRow(Defs.SwimViewStrength))
            .Add(fHint("motion.bob.hint", UWText.T("setup.motion.bob.hint"), 0f));
    }

    /// <summary>A label and its choices (Original / Smooth, the ramp, the frame rate, the modes):
    /// the label at 8, 100 wide, the choices from 108 over the panel less 116.</summary>
    private static UWSetupRow fMotionChoice(UWSettingDef pODef)
    {
        return new UWSetupRow
        {
            Kind = Kind.ChoiceRow, Id = pODef.LabelId, Def = pODef, X = -8f, WidthLess = -16f, ChoiceColumns = MotionChoiceColumns, Advance = 30f
        };
    }

    /// <summary>The strength row under a Smooth setting: ten to a hundred per cent in steps of
    /// five like the volumes, so the file is written only when a step changes and not on every
    /// frame of the drag. The limits and the step are the setting's (0.1 to 1 by 0.05 for all
    /// three); the row is there only while its mode is Smooth (the entry's VisibleWhen).</summary>
    private static UWSetupRow fStrengthRow(UWFloatSettingDef pODef)
    {
        return new UWSetupRow
        {
            Kind = Kind.Slider, Id = pODef.LabelId, Def = pODef, X = -8f, WidthLess = -16f, SliderColumns = StrengthColumns,
            Indent = true, Clamp = true, Advance = 30f
        };
    }

    // ------------------------------------------------- Display

    private UWSetupPage fBuildDisplayPage()
    {
        return new UWSetupPage("display")
            .Add(fTitle("display.title", UWText.T("setup.display.title")))
            .Add(new UWSetupRow
            {
                Kind = Kind.Hint, Id = "display.hint", MinHeight = 40f, Advance = 4f,
                TextOf = () => Application.isEditor
                    ? UWText.T("setup.display.hint.editor")
                    : UWText.T("setup.display.hint")
            })
            .Add(new UWSetupRow
            {
                Kind = Kind.ChoiceRow, Id = Defs.DisplayMode.LabelId, Def = Defs.DisplayMode, ChoiceColumns = DisplayModeColumns, Height = 28f,
                Advance = 40f, GetIndex = () => (int)meShownMode,
                Choose = piMode => UWDisplayMode.Apply((UWDisplayMode.ModeEnum)piMode, fSizeForMode((UWDisplayMode.ModeEnum)piMode, mOShownSize))
            })
            // "Resolution" before the list the dialog draws, in the same column as "Mode".
            .AddColumnLabel(UWText.T("setup.display.resolution"), 110f);
    }

    // ------------------------------------------------- Sound

    private UWSetupPage fBuildSoundPage()
    {
        return new UWSetupPage("sound")
            .Add(fTitle("sound.title", UWText.T("setup.sound.title")))
            .Add(fHint("sound.hint", UWText.T("setup.sound.hint"), 8f))
            .Add(new UWSetupRow
            {
                Kind = Kind.Toggle, Id = Defs.MusicEnabled.LabelId, Def = Defs.MusicEnabled, Width = 220f,
                GetOn = () => UWSoundOptions.MusicEnabled, SetOn = pbOn => UWSoundOptions.MusicEnabled = pbOn
            })
            .Add(fVolumeRow(Defs.MusicVolume, 38f, () => UWSoundOptions.MusicVolume, pfValue => UWSoundOptions.MusicVolume = pfValue))
            .Add(new UWSetupRow
            {
                Kind = Kind.Toggle, Id = Defs.SoundEnabled.LabelId, Def = Defs.SoundEnabled, Width = 220f,
                GetOn = () => UWSoundOptions.SoundEnabled, SetOn = pbOn => UWSoundOptions.SoundEnabled = pbOn
            })
            .Add(fVolumeRow(Defs.SoundVolume, 44f, () => UWSoundOptions.SoundVolume, pfValue => UWSoundOptions.SoundVolume = pfValue))
            // THE MUSIC DEVICE (UWMusicDeviceId). General MIDI plays the MT-32 version on the
            // soundfont that ships with the game, the MT-32 on Munt's emulation with the player's
            // own ROMs. AdLib is always there; the other two are greyed without their sound.
            .Add(new UWSetupRow { Kind = Kind.Info, Id = Defs.MusicDevice.LabelId, Def = Defs.MusicDevice, Width = 220f, Look = UWSetupRow.LookEnum.Heading })
            .Add(new UWSetupRow
            {
                Kind = Kind.Choices, Id = Defs.MusicDevice.LabelId + ".choices", Def = Defs.MusicDevice, X = 230f, WidthLess = 230f, Gap = 4f, Advance = 36f,
                EnabledChoices = () => 1 | (UWAudioMixer.IsGeneralMidiAvailable ? 2 : 0) | (UWAudioMixer.Mt32Roms != null ? 4 : 0)
            })
            .Add(fHint(Defs.MusicDevice, 6f))
            .Add(new UWSetupRow { Kind = Kind.Custom, Id = "sound.mt32roms", Draw = fDrawRomLine });
    }

    /// <summary>One volume right of its switch, greyed while the switch is off (the entry's
    /// EnabledWhen): a slider that snaps to the same five per cent its two arrows move by (the
    /// setting's step, UWSoundOptions.VolumeStep), and the value in per cent beside it. Set
    /// through UWSoundOptions, which saves it once it changes.</summary>
    private static UWSetupRow fVolumeRow(UWFloatSettingDef pODef, float pfAdvance, System.Func<float> pOGet, System.Action<float> pOSet)
    {
        return new UWSetupRow
        {
            Kind = Kind.Slider, Id = pODef.LabelId, Def = pODef, X = 230f, WidthLess = 230f, SliderColumns = VolumeColumns, Clamp = true,
            Advance = pfAdvance, Get = pOGet, Set = pOSet
        };
    }

    /// <summary>The MT-32 ROMs found, or why none, and the button that opens their folder - 140 wide,
    /// on the kit as wide as its text needs (2026-10-24), the line left of it 10 short of it. Drawn
    /// through the drawer the Sound window is drawn with (the pass's, cleanup 8b.2).</summary>
    private float fDrawRomLine(float pfLeft, float pfTop, float pfInner)
    {
        IUWRows lORows = mOPassRows;
        UWDataImport.UWData.UWMt32MusicDriver.RomSet lORoms = UWAudioMixer.Mt32Roms;
        string lsFolder = UWAudioMixer.Mt32RomFolder;
        string lsRomState = lORoms != null ? UWText.F("setup.sound.mt32_roms", lORoms.Description) : UWText.F("setup.sound.mt32_problem", UWAudioMixer.Mt32Problem);
        string lsOpen = UWText.T("setup.sound.open_folder");
        float lfButton = lORows.FitWidth(lsOpen, 140f);

        lORows.Info(new Rect(pfLeft, pfTop, pfInner - (lfButton + 10f), 26f), lsRomState, UWRowInfoEnum.Heading);

        if (lORows.Action(new Rect(pfLeft + pfInner - lfButton, pfTop, lfButton, 26f), lsOpen))
        {
            try
            {
                System.IO.Directory.CreateDirectory(lsFolder);
                Application.OpenURL((lsFolder.StartsWith("/") ? "file://" : "file:///") + lsFolder.Replace('\\', '/'));
            }
            catch (System.Exception lOError)
            {
                Debug.LogWarning("MT-32 ROM folder not opened: " + lOError.Message);
            }
        }

        return pfTop;
    }

    // ------------------------------------------------- Palette effects

    /// <summary>The palette dialog's rows under its title row (which carries the drag handle and
    /// Done): the presets, the five effects in half shade steps up to UWPaletteEffects' maxima (the
    /// settings table keeps no limits for them), the grime's tones, Glow and Light sources.</summary>
    private UWSetupPage fBuildPaletteEffectsPage()
    {
        return new UWSetupPage("palette")
            .Add(new UWSetupRow
            {
                Kind = Kind.Hint, Id = "palette.hint", Advance = 10f,
                TextOf = () => UWGraphicsDetail.CurrentLevel == UWGraphicsDetail.LevelEnum.Low
                    ? UWText.T("setup.palette.hint")
                    : UWText.T("setup.palette.hint.not_low")
            })
            // THE PRESETS (UWPaletteEffects.ApplyPreset): one click sets every effect; Own lights up
            // when the sliders hold something else.
            .Add(new UWSetupRow
            {
                Kind = Kind.ChoiceRow, Id = "palette.preset", Text = UWText.T("setup.palette.preset"), Choices = System.Array.ConvertAll(msPresetIds, UWText.T),
                ChoiceColumns = WideChoiceColumns, Advance = 38f,
                GetIndex = () => (int)UWPaletteEffects.CurrentPreset, Choose = piPreset => UWPaletteEffects.ApplyPreset((UWPaletteEffects.PresetEnum)piPreset)
            })
            .Add(fEffectRow(Defs.PaletteAmbientOcclusion, UWPaletteEffects.MaxAmbientOcclusion))
            .Add(fHint(Defs.PaletteAmbientOcclusion, 10f))
            .Add(fEffectRow(Defs.PaletteGrime, UWPaletteEffects.MaxGrime))
            // The grime's colour (UWGrimeTint): four buttons, the current one in the accent colour.
            .Add(new UWSetupRow { Kind = Kind.Choices, Id = Defs.PaletteGrimeTone.LabelId, Def = Defs.PaletteGrimeTone, X = 230f, WidthLess = 230f, Advance = 28f })
            .Add(fHint(Defs.PaletteGrime, 10f))
            .Add(fEffectRow(Defs.PaletteDepth, UWPaletteEffects.MaxDepth))
            .Add(fHint(Defs.PaletteDepth, 10f))
            .Add(fEffectRow(Defs.PaletteCavity, UWPaletteEffects.MaxRelief))
            .Add(fHint(Defs.PaletteCavity, 10f))
            .Add(fEffectRow(Defs.PaletteGroundShadows, UWPaletteEffects.MaxGroundShadows))
            .Add(fHint(Defs.PaletteGroundShadows, 10f))
            .Add(fToggle(Defs.PaletteGlow, 32f))
            .Add(fToggle(Defs.PaletteLightSources, 28f))
            .Add(fHint(Defs.PaletteLightSources, 0f));
    }

    /// <summary>One effect: its name, a strength in shade steps from 0 to the maximum in half steps
    /// with the value beside it ("Off" at 0); stored and the file written once a step changes, not
    /// every frame.</summary>
    private static UWSetupRow fEffectRow(UWFloatSettingDef pODef, float pfMax)
    {
        return new UWSetupRow
        {
            Kind = Kind.Slider, Id = pODef.LabelId, Def = pODef, SliderColumns = EffectColumns, Min = 0f, Max = pfMax, Step = EffectStep,
            Clamp = true, Advance = 28f
        };
    }

    // ------------------------------------------------- Folder

    /// <summary>The folder dialog's rows from the title to the path: the folders found, a button per
    /// drive, Up and the path to type in. The list of the folder, the status and the buttons hang
    /// from the dialog's bottom edge (fDrawFolderDialog).</summary>
    private UWSetupPage fBuildFolderPage()
    {
        return new UWSetupPage("folder")
            .Add(fTitle("folder.title", UWText.T("setup.folder.title")))
            .Add(new UWSetupRow
            {
                Kind = Kind.Hint, Id = "folder.hint", MinHeight = 22f, Advance = 8f,
                Text = UWText.T("setup.folder.hint")
            })
            .Add(new UWSetupRow { Kind = Kind.Custom, Id = "folder.found", Draw = fDrawFoundFolders })
            .Add(new UWSetupRow { Kind = Kind.Custom, Id = "folder.drives", Draw = fDrawDrives })
            .Add(new UWSetupRow { Kind = Kind.Custom, Id = "folder.path", Draw = fDrawPathRow });
    }

    // ------------------------------------------------- Graphics

    /// <summary>The four detail levels, short, by LevelEnum; the hint under them says what each is (the
    /// drop-down menu's items said it in one line each: "Low - original palette" and so on). The last,
    /// VeryHigh, reads Custom since cleanup 8d.3 (per user, 2026-10-25): the viewer's own values.</summary>
    private static readonly string[] msDetailLevelIds = { "setup.graphics.detail.low", "setup.graphics.detail.medium", "setup.graphics.detail.high", "setup.graphics.detail.custom" };

    /// <summary>The Graphics window's detail row and the Game window's scheme row: the label 80
    /// wide, the choices over the rest (four of 110 for the levels).</summary>
    private static readonly UWChoiceColumns WindowChoiceColumns = new UWChoiceColumns(0f, 80f, 80f, 80f);

    /// <summary>The detail row's Custom (VeryHigh), bit 3: a text, no choice (UWSetupRow.TextOnlyChoices).</summary>
    private const int CustomLevelTextOnly = 1 << (int)UWGraphicsDetail.LevelEnum.VeryHigh;

    private UWSetupPage fBuildGraphicsPage()
    {
        return new UWSetupPage("graphics")
            .Add(fTitle("graphics.title", UWText.T("setup.graphics.title")))
            // THE DETAIL LEVELS of the options panel, the current one in the accent colour; a click on
            // the current one sets it again. CUSTOM (VeryHigh) is only a text since cleanup 8d.3 (per
            // user, 2026-10-25): lit while the viewer's own values are in force, dim otherwise, no
            // click - "Remastered effects..." below sets it and opens their window.
            .Add(new UWSetupRow
            {
                Kind = Kind.ChoiceRow, Id = "graphics.detail", Text = UWText.T("setup.graphics.detail"), Choices = System.Array.ConvertAll(msDetailLevelIds, UWText.T),
                ChoiceColumns = WindowChoiceColumns, TextOnlyChoices = CustomLevelTextOnly,
                PressCurrent = true, Advance = 34f,
                GetIndex = () => (int)UWGraphicsDetail.CurrentLevel, Choose = piLevel => fChooseLevel((UWGraphicsDetail.LevelEnum)piLevel)
            })
            .Add(fHint("graphics.detail.hint", UWText.T("setup.graphics.detail.hint"), 10f))
            // Under the levels the palette renderer's own effects (UWPaletteEffects), a dialog, and
            // beside it the Remastered ones (UWEffectsScreen, cleanup 8d.3): both as wide as their text,
            // the second moving along with the first.
            .Add(new UWSetupRow
            {
                Kind = Kind.Action, Id = "graphics.palette", Text = UWText.T("setup.graphics.palette"), Width = 200f, Height = 30f,
                Press = fOpenPaletteEffectsDialog
            })
            .Add(new UWSetupRow
            {
                Kind = Kind.Action, Id = "graphics.remastered", Text = UWText.T("setup.graphics.remastered"), X = 210f, Width = 200f, Height = 30f,
                Advance = 40f, Press = fOpenRemasteredEffects
            })
            .Add(new UWSetupRow { Kind = Kind.Separator, Id = "graphics.separator", Height = 1f, Advance = 8f })
            // THE 4:3 SWITCH lives here and not under Game (per user, 2026-09-22): it changes how
            // the picture looks, like the detail levels above it, and takes effect at once - the
            // game camera is set up anew every frame and every 320x200 frame follows it in
            // LateUpdate (UWPixelAspectFrame). The Game menu keeps what concerns the program. The
            // classic screen is always at 4:3 since 2026-10-10 (per user); the place is the classic
            // frame's widescreen now (UWClassicWide).
            .Add(fToggle(Defs.ClassicWidescreen, 30f))
            // The view angle's row under the widescreen switch (UWViewAngle), measured from 8 left
            // of the text as in the menu.
            .Add(new UWSetupRow { Kind = Kind.Custom, Id = Defs.ViewAngle.LabelId, Draw = fDrawViewAngle })
            // The world at the original's resolution (UWWorldResolution), asked for on Reddit
            // (2026-10-07): it takes effect at once as well, the interface stays sharp.
            .Add(new UWSetupRow
            {
                Kind = Kind.Toggle, Id = Defs.OriginalWorldResolution.LabelId, Def = Defs.OriginalWorldResolution, Advance = 30f,
                GetOn = () => UWWorldResolution.Enabled, SetOn = pbOn => UWWorldResolution.Enabled = pbOn
            })
            // The multiple's slider under the switch while it is on, and a line on the locked 4x
            // while the window is too small for it.
            .Add(new UWSetupRow { Kind = Kind.Custom, Id = Defs.OriginalWorldResolutionFactor.LabelId, Draw = fDrawResolutionFactor });
    }

    private float fDrawViewAngle(float pfLeft, float pfTop, float pfInner)
    {
        fViewAngleRow(new Rect(pfLeft - 8f, 0f, pfInner + 16f, 0f), pfTop);

        return pfTop + 30f;
    }

    private float fDrawResolutionFactor(float pfLeft, float pfTop, float pfInner)
    {
        if (!UWWorldResolution.Enabled)
            return pfTop;

        bool lbMaxLocked = !UWWorldResolution.IsMaxFactorAllowed;

        fWorldResolutionFactorRow(new Rect(pfLeft - 8f, 0f, pfInner + 16f, 0f), pfTop, lbMaxLocked);

        return pfTop + (lbMaxLocked ? 60f : 30f);
    }

    // ------------------------------------------------- Game

    private UWSetupPage fBuildGamePage()
    {
        return new UWSetupPage("game")
            .Add(fTitle("game.title", UWText.T("setup.game.title")))
            // The game folder only from the main menu (and on the first-run screen, where it is the
            // one way on): in the game (the bar over the in-game menus since 2026-10-06) a new folder
            // would mean a new game.
            .Add(new UWSetupRow
            {
                Kind = Kind.Action, Id = "game.folder", Text = UWText.T("setup.game.folder"), Width = 200f, Height = 30f, Advance = 40f,

                EnabledWhen = fFolderReachable, Press = fOpenFolderDialog
            })
            // THE CONTROL SCHEME WITHOUT A KEY (per user, 2026-10-09: a small keyboard whose top row
            // gives media keys unless Fn is held could not reach Shift+F2; under Game because the
            // scheme changes the whole interface, not only the controls) - the same as Shift+F2,
            // kept for the next start.
            .Add(new UWSetupRow
            {
                Kind = Kind.ChoiceRow, Id = Defs.ControlScheme.LabelId, Def = Defs.ControlScheme, ChoiceColumns = WindowChoiceColumns, Advance = 36f,
                GetIndex = fSchemeIndex, Choose = fChooseScheme
            })
            // THE WHOLE OPENING AT STARTUP: the two company logos, the animated title and the
            // intro. Off it goes straight to the main menu, which is what one wants while testing
            // (per user, 2026-09-22). It takes effect at the next start.
            .Add(fToggle(Defs.ShowOpeningSequence, 30f))
            // KEEP RUNNING WITHOUT THE FOCUS (per user, 2026-09-28: for idle tests beside the
            // original, but everyone should decide for themselves). Takes effect at once (the
            // facade's setter applies it).
            .Add(new UWSetupRow
            {
                Kind = Kind.Toggle, Id = Defs.RunInBackground.LabelId, Def = Defs.RunInBackground, Advance = 30f,
                GetOn = () => UWUserSettings.RunInBackground,
                SetOn = pbOn =>
                {
                    UWUserSettings.RunInBackground = pbOn;
                    UWUserSettings.Save();
                }
            })
            // DEAL THE ATTRIBUTE POINTS AT CHARACTER CREATION ONESELF (per user, 2026-10-01: for
            // those who do not like their values to depend on luck; off by default). Takes effect at
            // the next character creation.
            .Add(fToggle(Defs.ManualAttributes, 30f));
    }

    /// <summary>The game folder can be chosen: over the main menu, or on the first-run screen.</summary>
    private bool fFolderReachable()
    {
        return UWMainMenu.AcceptsMenuBar || mbFirstRun;
    }
}
