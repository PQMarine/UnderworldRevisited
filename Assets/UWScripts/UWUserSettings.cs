using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UWDataImport.Presentation;
using UWDataImport.UWData;
using Defs = UWDataImport.UWData.UWUserSettingsTable;

/// <summary>
/// What the player sets up outside the game data (2026-09-17): for now the folder of the GOG
/// installation chosen in the setup screen. Kept in persistentDataPath/settings.json, because a
/// build never writes the settings asset back - the same reason as UWGraphicsDetail. Further
/// entries of the menu bar (controls, text, colours) are to land here too.
///
/// THE FACADE (cleanup step 8.3, 2026-10-24): the data is UWUserData, engine-free and the file
/// format itself; how a stored value reads - the clamps, the legacy quirks of older files, "not
/// chosen yet" - is an entry of UWUserSettingsTable each. This class loads and saves the file
/// with JsonUtility, holds the one instance and keeps every property the callers use, one line
/// each, plus the few with an effect of their own: GogInstallPath saves at once, RunInBackground
/// applies at once, ShowsPadHints asks the pad, and the layout editor's snapshot.
/// </summary>
public static class UWUserSettings
{
    private const string FileName = "settings.json";

    /// <summary>The settings read from the file (or the defaults); named as UWSettingsFixtureCheck
    /// expects it.</summary>
    private static UWUserData msData;

    /// <summary>The settings themselves, for the rows that read and write them through the table
    /// (UWSetupPageDrawer, the layout editor's inspector; cleanup step 8.4, 2026-10-24).</summary>
    internal static UWUserData Data => fGet();

    /// <summary>
    /// THE TABLE'S OnChange ENTRIES THAT NEED THE ENGINE (cleanup step 8.4, 2026-10-24): a change of
    /// the sticks' swap or a deadzone through a row puts the bindings anew (UWControls), as the
    /// gamepad tab did after each of them; hung on here, where the engine meets the table.
    /// </summary>
    static UWUserSettings()
    {
        Defs.GamepadSwapSticks.OnChange = fApplyKeyBindings;
        Defs.LeftStickDeadzone.OnChange = fApplyKeyBindings;
        Defs.RightStickDeadzone.OnChange = fApplyKeyBindings;
        // The windows' theme and ground take effect at once, an open window included (the Colours
        // window itself; cleanup phase 8d): the kit's drawers read the theme anew on their next pass.
        Defs.WindowTheme.OnChange = fThemeChanged;
        Defs.WindowBackground.OnChange = fThemeChanged;
    }

    private static void fThemeChanged(UWUserData pOData)
    {
        UWGuiTheme.Changed();
    }

    private static void fApplyKeyBindings(UWUserData pOData)
    {
        UWControls.ApplyKeyBindings();
    }

    public static string GogInstallPath
    {
        get { return Defs.GogInstallPath.Read(fGet()); }
        set
        {
            Defs.GogInstallPath.Write(fGet(), value);
            Save();
        }
    }

    public static List<string> KeyBindings => Defs.KeyBindings.Read(fGet());

    public static int ColourMode { get => Defs.ColourMode.Read(fGet()); set => Defs.ColourMode.Write(fGet(), value); }

    public static float ColourStrength { get => Defs.ColourStrength.Read(fGet()); set => Defs.ColourStrength.Write(fGet(), value); }

    public static float ColourBrightness { get => Defs.ColourBrightness.Read(fGet()); set => Defs.ColourBrightness.Write(fGet(), value); }

    public static float ColourContrast { get => Defs.ColourContrast.Read(fGet()); set => Defs.ColourContrast.Write(fGet(), value); }

    public static bool MarkPoison { get => Defs.MarkPoison.Read(fGet()); set => Defs.MarkPoison.Write(fGet(), value); }

    public static bool ManualAttributes { get => Defs.ManualAttributes.Read(fGet()); set => Defs.ManualAttributes.Write(fGet(), value); }

    public static bool HelpOpen { get => Defs.HelpOpen.Read(fGet()); set => Defs.HelpOpen.Write(fGet(), value); }

    public static int HelpTab { get => Defs.HelpTab.Read(fGet()); set => Defs.HelpTab.Write(fGet(), value); }

    public static int HelpMapSpan { get => Defs.HelpMapSpan.Read(fGet()); set => Defs.HelpMapSpan.Write(fGet(), value); }

    public static int MinimapZoom { get => Defs.MinimapZoom.Read(fGet()); set => Defs.MinimapZoom.Write(fGet(), value); }

    public static bool MinimapTurns { get => Defs.MinimapTurns.Read(fGet()); set => Defs.MinimapTurns.Write(fGet(), value); }

    public static int ModernUiPercent { get => Defs.ModernUiPercent.Read(fGet()); set => Defs.ModernUiPercent.Write(fGet(), value); }

    public static bool MinimapHidden { get => Defs.MinimapHidden.Read(fGet()); set => Defs.MinimapHidden.Write(fGet(), value); }

    public static bool ActionBarHidden { get => Defs.ActionBarHidden.Read(fGet()); set => Defs.ActionBarHidden.Write(fGet(), value); }

    public static bool ModernCompass { get => Defs.ModernCompass.Read(fGet()); set => Defs.ModernCompass.Write(fGet(), value); }

    public static bool ModernCompassDisc { get => Defs.ModernCompassDisc.Read(fGet()); set => Defs.ModernCompassDisc.Write(fGet(), value); }

    public static bool ModernCompassOutline { get => Defs.ModernCompassOutline.Read(fGet()); set => Defs.ModernCompassOutline.Write(fGet(), value); }

    public static string ModernBacks { get => Defs.ModernBacks.Read(fGet()); set => Defs.ModernBacks.Write(fGet(), value); }

    public static bool ModernScroll { get => Defs.ModernScroll.Read(fGet()); set => Defs.ModernScroll.Write(fGet(), value); }

    public static int ModernScrollLines { get => Defs.ModernScrollLines.Read(fGet()); set => Defs.ModernScrollLines.Write(fGet(), value); }

    public static int ModernMessageLines { get => Defs.ModernMessageLines.Read(fGet()); set => Defs.ModernMessageLines.Write(fGet(), value); }

    public static int InterfaceFont { get => Defs.InterfaceFont.Read(fGet()); set => Defs.InterfaceFont.Write(fGet(), value); }

    public static bool ModernGemApart { get => Defs.ModernGemApart.Read(fGet()); set => Defs.ModernGemApart.Write(fGet(), value); }

    public static bool ModernStoneShelf { get => Defs.ModernStoneShelf.Read(fGet()); set => Defs.ModernStoneShelf.Write(fGet(), value); }

    public static int ModernStoneShelfWidth { get => Defs.ModernStoneShelfWidth.Read(fGet()); set => Defs.ModernStoneShelfWidth.Write(fGet(), value); }

    public static bool ModernFlasksApart { get => Defs.ModernFlasksApart.Read(fGet()); set => Defs.ModernFlasksApart.Write(fGet(), value); }

    public static bool ModernRuneHollow { get => Defs.ModernRuneHollow.Read(fGet()); set => Defs.ModernRuneHollow.Write(fGet(), value); }

    public static bool ModernRuneHollowOutline { get => Defs.ModernRuneHollowOutline.Read(fGet()); set => Defs.ModernRuneHollowOutline.Write(fGet(), value); }

    public static bool ModernRuneTablet { get => Defs.ModernRuneTablet.Read(fGet()); set => Defs.ModernRuneTablet.Write(fGet(), value); }

    public static bool ModernStatsPanel { get => Defs.ModernStatsPanel.Read(fGet()); set => Defs.ModernStatsPanel.Write(fGet(), value); }

    public static bool ModernCharacterPage { get => Defs.ModernCharacterPage.Read(fGet()); set => Defs.ModernCharacterPage.Write(fGet(), value); }

    public static bool ModernConversationOriginal { get => Defs.ModernConversationOriginal.Read(fGet()); set => Defs.ModernConversationOriginal.Write(fGet(), value); }

    public static bool ModernConversationNoOutline { get => Defs.ModernConversationNoOutline.Read(fGet()); set => Defs.ModernConversationNoOutline.Write(fGet(), value); }

    public static string ModernLayout { get => Defs.ModernLayout.Read(fGet()); set => Defs.ModernLayout.Write(fGet(), value); }

    /// <summary>The interface preset; -1 in the file reads as Custom or Modern (the table's entry).</summary>
    public static int InterfacePreset { get => Defs.InterfacePreset.Read(fGet()); set => Defs.InterfacePreset.Write(fGet(), value); }

    public static int ConversationTextPercent { get => Defs.ConversationTextPercent.Read(fGet()); set => Defs.ConversationTextPercent.Write(fGet(), value); }

    public static bool ModernPointerHold { get => Defs.ModernPointerHold.Read(fGet()); set => Defs.ModernPointerHold.Write(fGet(), value); }

    /// <summary>The mouse look's speed factor, clamped; UWPlayerLook reads it every frame.</summary>
    public static float MouseLookSpeed { get => Defs.MouseLookSpeed.Read(fGet()); set => Defs.MouseLookSpeed.Write(fGet(), value); }

    public static bool MouseInvertY { get => Defs.MouseInvertY.Read(fGet()); set => Defs.MouseInvertY.Write(fGet(), value); }

    public static bool WalkSlowToggle { get => Defs.WalkSlowToggle.Read(fGet()); set => Defs.WalkSlowToggle.Write(fGet(), value); }

    public const float MinMouseLookSpeed = Defs.MinMouseLookSpeed;

    public const float MaxMouseLookSpeed = Defs.MaxMouseLookSpeed;

    /// <summary>The slider's and the arrows' step: five per cent.</summary>
    public const float MouseLookSpeedStep = Defs.MouseLookSpeedStep;

    public static int GamepadButtonStyle { get => Defs.GamepadButtonStyle.Read(fGet()); set => Defs.GamepadButtonStyle.Write(fGet(), value); }

    /// <summary>The stick's look speed factor, clamped; UWPlayerLook reads it every frame.</summary>
    public static float StickLookSpeed { get => Defs.StickLookSpeed.Read(fGet()); set => Defs.StickLookSpeed.Write(fGet(), value); }

    public static bool GamepadSwapSticks { get => Defs.GamepadSwapSticks.Read(fGet()); set => Defs.GamepadSwapSticks.Write(fGet(), value); }

    public static bool StickInvertY { get => Defs.StickInvertY.Read(fGet()); set => Defs.StickInvertY.Write(fGet(), value); }

    public const float MaxStickDeadzone = Defs.MaxStickDeadzone;

    public static bool GamepadButtonHints { get => Defs.GamepadButtonHints.Read(fGet()); set => Defs.GamepadButtonHints.Write(fGet(), value); }

    /// <summary>The hints show now: the pad was used last and they are switched on.</summary>
    public static bool ShowsPadHints => UWGamepad.IsActive && GamepadButtonHints;

    public static float LeftStickDeadzone { get => Defs.LeftStickDeadzone.Read(fGet()); set => Defs.LeftStickDeadzone.Write(fGet(), value); }

    public static float RightStickDeadzone { get => Defs.RightStickDeadzone.Read(fGet()); set => Defs.RightStickDeadzone.Write(fGet(), value); }

    public const float MinStickLookSpeed = Defs.MinStickLookSpeed;

    public const float MaxStickLookSpeed = Defs.MaxStickLookSpeed;

    public static bool ModernPanelPinned { get => Defs.ModernPanelPinned.Read(fGet()); set => Defs.ModernPanelPinned.Write(fGet(), value); }

    public static bool ModernRunesPinned { get => Defs.ModernRunesPinned.Read(fGet()); set => Defs.ModernRunesPinned.Write(fGet(), value); }

    public static int ControlScheme { get => Defs.ControlScheme.Read(fGet()); set => Defs.ControlScheme.Write(fGet(), value); }

    /// <summary>Seconds between two steps of the easy movement while the key or arrow stays
    /// down (UWEasyMovement). Settings written by an older build have a zero here, which is
    /// read as the default.</summary>
    public static float EasyMovementInterval { get => Defs.EasyMovementInterval.Read(fGet()); set => Defs.EasyMovementInterval.Write(fGet(), value); }

    /// <summary>The logos, the title and the intro at startup - see the field.</summary>
    public static bool ShowOpeningSequence { get => Defs.ShowOpeningSequence.Read(fGet()); set => Defs.ShowOpeningSequence.Write(fGet(), value); }

    /// <summary>Keep running without the focus - see the field. Setting it applies at once.</summary>
    public static bool RunInBackground
    {
        get { return Defs.RunInBackground.Read(fGet()); }
        set
        {
            Defs.RunInBackground.Write(fGet(), value);
            Application.runInBackground = value;
        }
    }

    /// <summary>Puts the stored choice into effect when the game starts.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void fApplyRunInBackground()
    {
        Application.runInBackground = RunInBackground;
    }

    /// <summary>Window or borderless fullscreen, -1 not chosen yet - see UWDisplayMode.</summary>
    public static int DisplayMode { get => Defs.DisplayMode.Read(fGet()); set => Defs.DisplayMode.Write(fGet(), value); }

    public static int ResolutionWidth { get => Defs.ResolutionWidth.Read(fGet()); set => Defs.ResolutionWidth.Write(fGet(), value); }

    public static int ResolutionHeight { get => Defs.ResolutionHeight.Read(fGet()); set => Defs.ResolutionHeight.Write(fGet(), value); }

    /// <summary>The 4:3 switch - kept for the settings.json field only (always on since
    /// 2026-10-10, see UWDisplayAspect); nothing reads or changes it.</summary>
    public static bool DisplayAs4By3 { get => Defs.DisplayAs4By3.Read(fGet()); set => Defs.DisplayAs4By3.Write(fGet(), value); }

    public static bool ClassicWidescreen { get => Defs.ClassicWidescreen.Read(fGet()); set => Defs.ClassicWidescreen.Write(fGet(), value); }

    public static float ViewAngle { get => Defs.ViewAngle.Read(fGet()); set => Defs.ViewAngle.Write(fGet(), value); }

    /// <summary>The world at the original's resolution. Everything that reads or changes it goes
    /// through UWWorldResolution.</summary>
    public static bool OriginalWorldResolution { get => Defs.OriginalWorldResolution.Read(fGet()); set => Defs.OriginalWorldResolution.Write(fGet(), value); }

    /// <summary>The palette renderer's ambient occlusion in shade steps, 0 = off - see UWPaletteEffects.</summary>
    public static float PaletteAmbientOcclusion { get => Defs.PaletteAmbientOcclusion.Read(fGet()); set => Defs.PaletteAmbientOcclusion.Write(fGet(), value); }

    /// <summary>The palette renderer's grime along the walls in shade steps, 0 = off - see UWPaletteEffects.</summary>
    public static float PaletteGrime { get => Defs.PaletteGrime.Read(fGet()); set => Defs.PaletteGrime.Write(fGet(), value); }

    /// <summary>The grime's colour - see UWGrimeTint.ToneEnum.</summary>
    public static int PaletteGrimeTone { get => Defs.PaletteGrimeTone.Read(fGet()); set => Defs.PaletteGrimeTone.Write(fGet(), value); }

    /// <summary>The palette renderer's ground shadows in shade steps, 0 = off - see UWPaletteEffects.</summary>
    public static float PaletteGroundShadows { get => Defs.PaletteGroundShadows.Read(fGet()); set => Defs.PaletteGroundShadows.Write(fGet(), value); }

    /// <summary>The palette renderer's hollows in shade steps, 0 = off - see UWPaletteEffects.</summary>
    public static float PaletteCavity { get => Defs.PaletteCavity.Read(fGet()); set => Defs.PaletteCavity.Write(fGet(), value); }

    /// <summary>The palette renderer's depth (parallax and self shadow), 0 = off - see UWPaletteEffects.</summary>
    public static float PaletteDepth { get => Defs.PaletteDepth.Read(fGet()); set => Defs.PaletteDepth.Write(fGet(), value); }

    /// <summary>The palette renderer's glow - see UWPaletteEffects.</summary>
    public static bool PaletteGlow { get => Defs.PaletteGlow.Read(fGet()); set => Defs.PaletteGlow.Write(fGet(), value); }

    /// <summary>The palette renderer's light sources - see UWPaletteEffects.</summary>
    public static bool PaletteLightSources { get => Defs.PaletteLightSources.Read(fGet()); set => Defs.PaletteLightSources.Write(fGet(), value); }

    /// <summary>The multiple of the original's resolution - see UWWorldResolution.Factor.</summary>
    public static int OriginalWorldResolutionFactor { get => Defs.OriginalWorldResolutionFactor.Read(fGet()); set => Defs.OriginalWorldResolutionFactor.Write(fGet(), value); }

    /// <summary>The head bob setting (UWHeadBobRules.ModeEnum); read every frame. An unknown
    /// number from the file reads as Original.</summary>
    public static UWHeadBobRules.ModeEnum HeadBobMode
    {
        get => (UWHeadBobRules.ModeEnum)Defs.HeadBobMode.Read(fGet());
        set => Defs.HeadBobMode.Write(fGet(), (int)value);
    }

    /// <summary>The Smooth head bob's strength, clamped to 0.1 - 1; Original always has the full one.</summary>
    public static float HeadBobStrength { get => Defs.HeadBobStrength.Read(fGet()); set => Defs.HeadBobStrength.Write(fGet(), value); }

    /// <summary>Below this the Smooth bob is not worth having - Off is the switch for none.</summary>
    public const float MinHeadBobStrength = Defs.MinHeadBobStrength;

    /// <summary>The weapon jitter setting, like HeadBobMode.</summary>
    public static UWHeadBobRules.ModeEnum WeaponJitterMode
    {
        get => (UWHeadBobRules.ModeEnum)Defs.WeaponJitterMode.Read(fGet());
        set => Defs.WeaponJitterMode.Write(fGet(), (int)value);
    }

    /// <summary>Whether the player's motion runs in the precision mode (true, Smooth) or as the
    /// original's arithmetic call by call at its frame length (false, Original).</summary>
    public static bool MotionSmooth { get => Defs.MotionSmoothing.ReadOn(fGet()); set => Defs.MotionSmoothing.WriteOn(fGet(), value); }

    /// <summary>The motion mode as its number: 0 Original, 1 Smooth, 2 Modern (another stored number
    /// reads as Smooth).</summary>
    public static int MotionMode { get => Defs.MotionSmoothing.Read(fGet()); set => Defs.MotionSmoothing.Write(fGet(), value); }

    /// <summary>Whether the player's motion is Modern, the character motor (the precision mode's
    /// frame on the motor; MotionSmooth is true for it as well).</summary>
    public static bool MotionModern => Defs.MotionSmoothing.Read(fGet()) == Defs.MotionModern;

    /// <summary>Modern's ramp in ticks, one of SmoothRampChoices (0 at once by default).</summary>
    public static int ModernRampTicks { get => Defs.ModernRampTicks.Read(fGet()); set => Defs.ModernRampTicks.Write(fGet(), value); }

    /// <summary>Whether the view is set once per simulation step (true, the original's picture
    /// rate) or drawn between the steps (false, even).</summary>
    public static bool MotionViewStepped { get => Defs.MotionViewStepped.ReadOn(fGet()); set => Defs.MotionViewStepped.WriteOn(fGet(), value); }

    /// <summary>The Original motion's frame length in ticks, one of OriginalFrameChoices.</summary>
    public static int OriginalFrameTicks { get => Defs.OriginalFrameTicks.Read(fGet()); set => Defs.OriginalFrameTicks.Write(fGet(), value); }

    /// <summary>4, 8, 12, 16 ticks: 64, 32, 21, 16 frames a second.</summary>
    public static readonly int[] OriginalFrameChoices = Defs.OriginalFrameChoices;

    /// <summary>The Smooth ramp's frame in ticks, one of SmoothRampChoices.</summary>
    public static int SmoothRampTicks { get => Defs.SmoothRampTicks.Read(fGet()); set => Defs.SmoothRampTicks.Write(fGet(), value); }

    /// <summary>16, 8, 4, 0 ticks: 0.6 s, 0.3 s, 0.15 s, at once.</summary>
    public static readonly int[] SmoothRampChoices = Defs.SmoothRampChoices;

    /// <summary>The Smooth weapon jitter's strength, clamped like HeadBobStrength.</summary>
    public static float WeaponJitterStrength { get => Defs.WeaponJitterStrength.Read(fGet()); set => Defs.WeaponJitterStrength.Write(fGet(), value); }

    /// <summary>The swimmer's view (UWSwimViewRules), like HeadBobMode.</summary>
    public static UWHeadBobRules.ModeEnum SwimViewMode
    {
        get => (UWHeadBobRules.ModeEnum)Defs.SwimViewMode.Read(fGet());
        set => Defs.SwimViewMode.Write(fGet(), (int)value);
    }

    /// <summary>The Smooth swimming view's strength, clamped like HeadBobStrength.</summary>
    public static float SwimViewStrength { get => Defs.SwimViewStrength.Read(fGet()); set => Defs.SwimViewStrength.Write(fGet(), value); }

    /// <summary>Music and sound. Everything that changes them goes through UWSoundOptions,
    /// which also puts them into the live settings.</summary>
    public static bool MusicEnabled { get => Defs.MusicEnabled.Read(fGet()); set => Defs.MusicEnabled.Write(fGet(), value); }

    public static bool SoundEnabled { get => Defs.SoundEnabled.Read(fGet()); set => Defs.SoundEnabled.Write(fGet(), value); }

    public static float MusicVolume { get => Defs.MusicVolume.Read(fGet()); set => Defs.MusicVolume.Write(fGet(), value); }

    public static float SoundVolume { get => Defs.SoundVolume.Read(fGet()); set => Defs.SoundVolume.Write(fGet(), value); }

    public static int MusicDevice { get => Defs.MusicDevice.Read(fGet()); set => Defs.MusicDevice.Write(fGet(), value); }

    public static string Mt32RomPath { get => Defs.Mt32RomPath.Read(fGet()); set => Defs.Mt32RomPath.Write(fGet(), value); }

    /// <summary>The player's recordings of the music in place of the device (UWMusicRecordings).</summary>
    public static bool MusicRecordings { get => Defs.MusicRecordings.Read(fGet()); set => Defs.MusicRecordings.Write(fGet(), value); }

    /// <summary>The debug panel's folded sections (UWDebugFolds' text; cleanup 8c).</summary>
    public static string DebugFolded { get => Defs.DebugFolded.Read(fGet()); set => Defs.DebugFolded.Write(fGet(), value); }

    /// <summary>The game's own pointer: screen pixels per mouse count, clamped (UWPointer reads it every
    /// frame it is captured; cleanup step 7.7B).</summary>
    public static float PointerSpeed { get => Defs.PointerSpeed.Read(fGet()); set => Defs.PointerSpeed.Write(fGet(), value); }

    /// <summary>The look of the game's own windows (UWGuiTheme reads it and is told of a change, here
    /// as through the Colours window's row; cleanup phase 8d).</summary>
    public static UWWindowThemeEnum WindowTheme
    {
        get => (UWWindowThemeEnum)Defs.WindowTheme.Read(fGet());
        set
        {
            Defs.WindowTheme.Write(fGet(), (int)value);
            UWGuiTheme.Changed();
        }
    }

    /// <summary>The Stone theme's ground behind the windows (as WindowTheme; cleanup phase 8d).</summary>
    public static UWWindowBackgroundEnum WindowBackground
    {
        get => (UWWindowBackgroundEnum)Defs.WindowBackground.Read(fGet());
        set
        {
            Defs.WindowBackground.Write(fGet(), (int)value);
            UWGuiTheme.Changed();
        }
    }

    /// <summary>The language code chosen in the Game window, empty for English; UWLanguage reads it
    /// at startup, so a change takes effect at the next start.</summary>
    public static string Language { get => Defs.Language.Read(fGet()); set => Defs.Language.Write(fGet(), value); }

    /// <summary>
    /// THE LAYOUT EDITOR'S SESSION (per user, 2026-10-11: "the layout is saved only when one leaves
    /// the editor and confirms the save" - so a preset can be a starting point and one can still go
    /// back to one's own): while true nothing is written to the file; the changes live in memory and
    /// the editor writes them, or puts the snapshot back, when it is left (UWModernLayoutEditor).
    /// </summary>
    public static bool SaveSuspended { get; set; }

    /// <summary>The layout's settings at one moment (TakeLayoutSnapshot).</summary>
    public sealed class LayoutSnapshot
    {
        internal object[] Values;
    }

    /// <summary>Everything the layout editor sets: the parts' places and sizes, their backs, the
    /// UI size, every part's switches and looks, the font - the table's Layout entries (until
    /// cleanup step 8.3 a list of field names here, read by reflection).</summary>
    private static UWSettingDef[] msLayoutDefs;

    private static UWSettingDef[] fLayoutDefs()
    {
        if (msLayoutDefs == null)
            msLayoutDefs = Defs.InStore(UWSettingStore.Layout);

        return msLayoutDefs;
    }

    public static LayoutSnapshot TakeLayoutSnapshot()
    {
        UWSettingDef[] lODefs = fLayoutDefs();
        object[] lOValues = new object[lODefs.Length];

        for (int liAt = 0; liAt < lODefs.Length; liAt++)
            lOValues[liAt] = lODefs[liAt].RawOf(fGet());

        return new LayoutSnapshot { Values = lOValues };
    }

    public static void RestoreLayout(LayoutSnapshot pOSnapshot)
    {
        if (pOSnapshot == null)
            return;

        UWSettingDef[] lODefs = fLayoutDefs();

        for (int liAt = 0; liAt < lODefs.Length && liAt < pOSnapshot.Values.Length; liAt++)
            lODefs[liAt].SetRaw(fGet(), pOSnapshot.Values[liAt]);
    }

    /// <summary>Whether the layout's settings are what the snapshot holds.</summary>
    public static bool LayoutEquals(LayoutSnapshot pOSnapshot)
    {
        if (pOSnapshot == null)
            return true;

        UWSettingDef[] lODefs = fLayoutDefs();

        for (int liAt = 0; liAt < lODefs.Length && liAt < pOSnapshot.Values.Length; liAt++)
        {
            if (!Equals(lODefs[liAt].RawOf(fGet()), pOSnapshot.Values[liAt]))
                return false;
        }

        return true;
    }

    public static void Save()
    {
        if (SaveSuspended)
            return;

        try
        {
            File.WriteAllText(fGetPath(), JsonUtility.ToJson(fGet(), true));
        }
        catch (Exception lOError)
        {
            Debug.LogWarning("Settings not saved: " + lOError.Message);
        }
    }

    private static UWUserData fGet()
    {
        if (msData != null)
            return msData;

        try
        {
            string lsPath = fGetPath();

            if (File.Exists(lsPath))
                msData = JsonUtility.FromJson<UWUserData>(File.ReadAllText(lsPath));
        }
        catch (Exception lOError)
        {
            Debug.LogWarning("Settings not read: " + lOError.Message);
        }

        if (msData == null)
            msData = new UWUserData();

        return msData;
    }

    private static string fGetPath()
    {
        return PathBeside(FileName);
    }

    /// <summary>A file in the folder of settings.json - the MT32 folder's parent, and the parent of
    /// the game's folder (UWGameFolder: UW1/rules.json, UW1/Music).</summary>
    public static string PathBeside(string psFileName)
    {
        return Path.Combine(Application.persistentDataPath, psFileName);
    }
}
