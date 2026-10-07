using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// What the player sets up outside the game data (2026-09-17): for now the folder of the GOG
/// installation chosen in the setup screen. Kept in persistentDataPath/settings.json, because a
/// build never writes the settings asset back - the same reason as UWGraphicsDetail. Further
/// entries of the menu bar (controls, text, colours) are to land here too.
/// </summary>
public static class UWUserSettings
{
    [Serializable]
    private class Data
    {
        public string GogInstallPath = string.Empty;

        /// <summary>Changed keys, one line per binding: "map|action|index|path" (see
        /// UWKeyBindings).</summary>
        public List<string> KeyBindings = new List<string>();

        /// <summary>Colour help (see UWColourVision): mode, strength, brightness, contrast.</summary>
        public int ColourMode;

        public float ColourStrength = 1f;

        public float ColourBrightness = 1f;

        public float ColourContrast = 1f;

        /// <summary>Hatch the health flask while poisoned, so it is not only the colour that
        /// tells (see UWFlasks).</summary>
        public bool MarkPoison;

        /// <summary>The player deals the class's bonus points at character creation himself
        /// instead of the dice (UWCharacterGeneration.ManualAttributes). Off by default.</summary>
        public bool ManualAttributes;

        /// <summary>The help window as the player left it (per user, 2026-10-01): open or not,
        /// the tab (UWHelpWindow.TabEnum as a number, 3 = Spells) and how many tiles the map
        /// tab shows across.</summary>
        public bool HelpOpen;

        public int HelpTab = 3;

        public int HelpMapSpan = 20;

        /// <summary>The modern scheme's minimap (UWModernMinimap, per user 2026-10-03): its zoom
        /// as screen pixels per map pixel at the default HUD scale (0 = not chosen yet), and
        /// whether it turns with the view - north up by default.</summary>
        public int MinimapZoom;

        public bool MinimapTurns;

        /// <summary>The modern UI's size chosen in the game menu, in percent of the automatic
        /// one, 0 = 100 (UWModernHud.PixelScale).</summary>
        public int ModernUiPercent;

        /// <summary>The modern conversation's text size in percent, 0 = 100 (UWModernConversation,
        /// its + and - in the history's corner).</summary>
        public int ConversationTextPercent;

        /// <summary>The modern HUD's own layout (UWModernLayout): the moved and sized parts.</summary>
        public string ModernLayout;

        /// <summary>The modern minimap switched off (the layout editor's Minimap button).</summary>
        public bool MinimapHidden;

        /// <summary>The modern pointer is free and the view turns only while the right button is
        /// held, instead of the button toggling (UWModernPointer; toggling by default, per user
        /// 2026-10-03).</summary>
        public bool ModernPointerHold;

        /// <summary>The mouse look's speed in the modern scheme as a factor on the tuned
        /// default, 0.05 to 2 (per user, 2026-10-06: "Die Maussteuerung ist schon sehr
        /// direkt", so adjustable). The stick keeps its own rate.</summary>
        public float MouseLookSpeed = 0.5f;

        /// <summary>The modern character panel stays out on its Character tab without freeing the
        /// pointer, the minimap in its head (UWModernPanel, per user 2026-10-03).</summary>
        public bool ModernPanelPinned;

        /// <summary>The modern rune panel stays out (UWModernRunePanel, per user 2026-10-04).</summary>
        public bool ModernRunesPinned;

        /// <summary>The control scheme last chosen (Shift+F2), as UWControlScheme.SchemeEnum; -1 =
        /// not chosen yet, the scene's default (per user, 2026-10-04: kept over loading and noclip).</summary>
        public int ControlScheme = -1;

        /// <summary>Seconds between two steps while an easy movement key or arrow is held
        /// down - see UWEasyMovement.DefaultRepeatSeconds.</summary>
        public float EasyMovementInterval = UWDataImport.UWData.UWEasyMovement.DefaultRepeatSeconds;

        /// <summary>The two company logos, the animated title and the intro at startup. Off
        /// goes straight to the main menu, which is what one wants while testing
        /// (per user, 2026-09-22).</summary>
        public bool ShowOpeningSequence = true;

        /// <summary>The game keeps running while another window has the focus (per user,
        /// 2026-09-28: wanted for idle tests beside the original, "everyone should decide for
        /// themselves"). Off by default, as Unity has it.</summary>
        public bool RunInBackground;

        /// <summary>The classic screen at 4:3 - see UWDisplayAspect. True, because the settings
        /// asset ships with it on, so a file written before this entry existed keeps the
        /// picture it had.</summary>
        public bool DisplayAs4By3 = true;

        /// <summary>The world drawn at the original's resolution - see UWWorldResolution. Off by
        /// default: the full screen resolution, as before.</summary>
        public bool OriginalWorldResolution;

        /// <summary>Its multiple of the original's resolution, 1 to 4 (UWWorldResolution.Factor).</summary>
        public int OriginalWorldResolutionFactor = 1;

        /// <summary>The palette renderer's ambient occlusion in shade steps, 0 = off
        /// (UWPaletteEffects).</summary>
        public float PaletteAmbientOcclusion;

        /// <summary>The palette renderer's light sources (UWPaletteEffects); off by default.</summary>
        public bool PaletteLightSources;

        /// <summary>The palette renderer's glow (lava, the light sources' flames); off by default.</summary>
        public bool PaletteGlow;

        /// <summary>The palette renderer's grime along the walls in shade steps, 0 = off.</summary>
        public float PaletteGrime;

        /// <summary>The grime's colour (UWGrimeTint.ToneEnum): 0 none, 1 dirt, 2 moss, 3 soot.</summary>
        public int PaletteGrimeTone = 1;

        /// <summary>The palette renderer's ground shadows in shade steps, 0 = off.</summary>
        public float PaletteGroundShadows;

        /// <summary>The palette renderer's hollows (joints darker) in shade steps, 0 = off.</summary>
        public float PaletteCavity;

        /// <summary>The palette renderer's depth (parallax and self shadow), 0 = off.</summary>
        public float PaletteDepth;


        /// <summary>Window or borderless fullscreen (UWDisplayMode.ModeEnum): 0 window, 1 borderless
        /// fullscreen; -1 not chosen yet, then a build starts borderless at the desktop's size.</summary>
        public int DisplayMode = -1;

        /// <summary>The resolution the game renders at; 0 = the desktop's.</summary>
        public int ResolutionWidth;

        public int ResolutionHeight;

        /// <summary>The head bob while walking and the weapon's jitter with it - see UWHeadBobRules:
        /// 0 off, 1 the original's steps, 2 smoothed (per user, 2026-09-23, first a plain on/off
        /// switch). Original by default; off for players who get sick from it.</summary>
        public int HeadBobMode = 1;

        /// <summary>How strong the Smooth head bob is, 0.1 to 1 of the original's amplitude (per
        /// user, 2026-09-23: a slider beside Smooth, none for Off and Original).</summary>
        public float HeadBobStrength = 1f;

        /// <summary>The weapon's jitter, its own three-way switch since 2026-09-23 (per user: "Original
        /// is too tiring for me, but without it something is missing"): 0 off, 1 the original's
        /// jumps, 2 smoothed. Until then the head bob switch turned it off with the bob.</summary>
        public int WeaponJitterMode = 1;

        /// <summary>How strong the Smooth weapon jitter is, 0.1 to 1 of the original's range.</summary>
        public float WeaponJitterStrength = 1f;

        /// <summary>The player's motion on the core (2026-10-06, per user: "per Schalter
        /// einstellbar, wer es nah am Original haben moechte kann das waehlen"): 0 Original - one
        /// motion frame per rendered frame with the ticks that passed, whole units, as UW.EXE
        /// runs it; 1 Smooth - fixed steps of the original's frame length with the view drawn
        /// between them (UWPlayerMovement). Smooth by default.</summary>
        public int MotionSmoothing = 1;

        /// <summary>The picture of the motion (2026-10-06, per user: "soll jeder selbst einstellen"):
        /// 0 even - the view drawn between the simulation steps at the monitor's rate; 1 stepped
        /// - the view set once per step, the 15-20 pictures a second of the original on DOSBox at
        /// fixed cycles. Even by default.</summary>
        public int MotionViewStepped;

        /// <summary>The frame length of the Original motion in PIT ticks - how often UW.EXE ran
        /// its motion frame on the machine one measures against (2026-10-06, per user: DOSBox at
        /// 30000 cycles looks smoother than our 16): 4, 8, 12 or 16 ticks = 64, 32, 21 or 16
        /// frames a second. 8 by default - the user matched 32 fps against his DOSBox by eye.</summary>
        public int OriginalFrameTicks = 8;

        /// <summary>The Smooth motion's ramp (2026-10-06, per user: a long start and stop work
        /// against a sensitive stomach): the speed changes by the original's step per this many
        /// ticks - 16 = 0.56 s to the run (the original at 16 fps), 8 = 0.31 s (at 32 fps, the
        /// user's DOSBox), 4 = 0.16 s, 0 = at once. 8 by default.</summary>
        public int SmoothRampTicks = 8;

        /// <summary>Music and sound, on or off and how loud - see UWSoundOptions. The values
        /// here are the ones the settings asset starts with, so a file written before this
        /// entry existed reads as the old behaviour.</summary>
        public bool MusicEnabled = true;

        public float MusicVolume = 0.6f;

        public bool SoundEnabled = true;

        public float SoundVolume = 0.8f;
    }

    private const string FileName = "settings.json";

    private static Data msData;

    public static string GogInstallPath
    {
        get { return fGet().GogInstallPath; }
        set
        {
            fGet().GogInstallPath = value ?? string.Empty;
            Save();
        }
    }

    public static List<string> KeyBindings
    {
        get
        {
            Data lOData = fGet();

            if (lOData.KeyBindings == null)
                lOData.KeyBindings = new List<string>();

            return lOData.KeyBindings;
        }
    }

    public static int ColourMode
    {
        get { return fGet().ColourMode; }
        set { fGet().ColourMode = value; }
    }

    public static float ColourStrength
    {
        get { return fGet().ColourStrength; }
        set { fGet().ColourStrength = value; }
    }

    public static float ColourBrightness
    {
        get { return fGet().ColourBrightness; }
        set { fGet().ColourBrightness = value; }
    }

    public static float ColourContrast
    {
        get { return fGet().ColourContrast; }
        set { fGet().ColourContrast = value; }
    }

    public static bool MarkPoison
    {
        get { return fGet().MarkPoison; }
        set { fGet().MarkPoison = value; }
    }

    public static bool ManualAttributes
    {
        get { return fGet().ManualAttributes; }
        set { fGet().ManualAttributes = value; }
    }

    public static bool HelpOpen
    {
        get { return fGet().HelpOpen; }
        set { fGet().HelpOpen = value; }
    }

    public static int HelpTab
    {
        get { return fGet().HelpTab; }
        set { fGet().HelpTab = value; }
    }

    public static int HelpMapSpan
    {
        get { return fGet().HelpMapSpan; }
        set { fGet().HelpMapSpan = value; }
    }

    public static int MinimapZoom
    {
        get { return fGet().MinimapZoom; }
        set { fGet().MinimapZoom = value; }
    }

    public static bool MinimapTurns
    {
        get { return fGet().MinimapTurns; }
        set { fGet().MinimapTurns = value; }
    }

    public static int ModernUiPercent
    {
        get { return fGet().ModernUiPercent; }
        set { fGet().ModernUiPercent = value; }
    }

    public static bool MinimapHidden
    {
        get { return fGet().MinimapHidden; }
        set { fGet().MinimapHidden = value; }
    }

    public static string ModernLayout
    {
        get { return fGet().ModernLayout; }
        set { fGet().ModernLayout = value; }
    }

    public static int ConversationTextPercent
    {
        get { return fGet().ConversationTextPercent; }
        set { fGet().ConversationTextPercent = value; }
    }

    public static bool ModernPointerHold
    {
        get { return fGet().ModernPointerHold; }
        set { fGet().ModernPointerHold = value; }
    }

    /// <summary>The mouse look's speed factor, clamped; UWPlayerLook reads it every frame.</summary>
    public static float MouseLookSpeed
    {
        get { return Mathf.Clamp(fGet().MouseLookSpeed <= 0f ? 1f : fGet().MouseLookSpeed, MinMouseLookSpeed, MaxMouseLookSpeed); }
        set { fGet().MouseLookSpeed = Mathf.Clamp(value, MinMouseLookSpeed, MaxMouseLookSpeed); }
    }

    public const float MinMouseLookSpeed = 0.05f;

    public const float MaxMouseLookSpeed = 2f;

    /// <summary>The slider's and the arrows' step: five per cent.</summary>
    public const float MouseLookSpeedStep = 0.05f;

    public static bool ModernPanelPinned
    {
        get { return fGet().ModernPanelPinned; }
        set { fGet().ModernPanelPinned = value; }
    }

    public static bool ModernRunesPinned
    {
        get { return fGet().ModernRunesPinned; }
        set { fGet().ModernRunesPinned = value; }
    }

    public static int ControlScheme
    {
        get { return fGet().ControlScheme; }
        set { fGet().ControlScheme = value; }
    }

    /// <summary>Seconds between two steps of the easy movement while the key or arrow stays
    /// down (UWEasyMovement). Settings written by an older build have a zero here, which is
    /// read as the default.</summary>
    public static float EasyMovementInterval
    {
        get
        {
            float lfValue = fGet().EasyMovementInterval;

            if (lfValue <= 0f)
                return UWDataImport.UWData.UWEasyMovement.DefaultRepeatSeconds;

            return Mathf.Clamp(lfValue, UWDataImport.UWData.UWEasyMovement.MinRepeatSeconds,
                UWDataImport.UWData.UWEasyMovement.MaxRepeatSeconds);
        }

        set
        {
            fGet().EasyMovementInterval = Mathf.Clamp(value,
                UWDataImport.UWData.UWEasyMovement.MinRepeatSeconds,
                UWDataImport.UWData.UWEasyMovement.MaxRepeatSeconds);
        }
    }

    /// <summary>The logos, the title and the intro at startup - see the field.</summary>
    public static bool ShowOpeningSequence
    {
        get { return fGet().ShowOpeningSequence; }
        set { fGet().ShowOpeningSequence = value; }
    }

    /// <summary>Keep running without the focus - see the field. Setting it applies at once.</summary>
    public static bool RunInBackground
    {
        get { return fGet().RunInBackground; }
        set
        {
            fGet().RunInBackground = value;
            Application.runInBackground = value;
        }
    }

    /// <summary>Puts the stored choice into effect when the game starts.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void fApplyRunInBackground()
    {
        Application.runInBackground = fGet().RunInBackground;
    }

    /// <summary>Window or borderless fullscreen, -1 not chosen yet - see UWDisplayMode.</summary>
    public static int DisplayMode
    {
        get { return fGet().DisplayMode; }
        set { fGet().DisplayMode = value; }
    }

    public static int ResolutionWidth
    {
        get { return fGet().ResolutionWidth; }
        set { fGet().ResolutionWidth = value; }
    }

    public static int ResolutionHeight
    {
        get { return fGet().ResolutionHeight; }
        set { fGet().ResolutionHeight = value; }
    }

    /// <summary>The 4:3 switch. Everything that reads or changes it goes through
    /// UWDisplayAspect, which also puts it into the live settings.</summary>
    public static bool DisplayAs4By3
    {
        get { return fGet().DisplayAs4By3; }
        set { fGet().DisplayAs4By3 = value; }
    }

    /// <summary>The world at the original's resolution. Everything that reads or changes it goes
    /// through UWWorldResolution.</summary>
    public static bool OriginalWorldResolution
    {
        get { return fGet().OriginalWorldResolution; }
        set { fGet().OriginalWorldResolution = value; }
    }

    /// <summary>The palette renderer's ambient occlusion in shade steps, 0 = off - see UWPaletteEffects.</summary>
    public static float PaletteAmbientOcclusion
    {
        get { return fGet().PaletteAmbientOcclusion; }
        set { fGet().PaletteAmbientOcclusion = value; }
    }

    /// <summary>The palette renderer's grime along the walls in shade steps, 0 = off - see UWPaletteEffects.</summary>
    public static float PaletteGrime
    {
        get { return fGet().PaletteGrime; }
        set { fGet().PaletteGrime = value; }
    }

    /// <summary>The grime's colour - see UWGrimeTint.ToneEnum.</summary>
    public static int PaletteGrimeTone
    {
        get { return fGet().PaletteGrimeTone; }
        set { fGet().PaletteGrimeTone = value; }
    }

    /// <summary>The palette renderer's ground shadows in shade steps, 0 = off - see UWPaletteEffects.</summary>
    public static float PaletteGroundShadows
    {
        get { return fGet().PaletteGroundShadows; }
        set { fGet().PaletteGroundShadows = value; }
    }

    /// <summary>The palette renderer's hollows in shade steps, 0 = off - see UWPaletteEffects.</summary>
    public static float PaletteCavity
    {
        get { return fGet().PaletteCavity; }
        set { fGet().PaletteCavity = value; }
    }

    /// <summary>The palette renderer's depth (parallax and self shadow), 0 = off - see UWPaletteEffects.</summary>
    public static float PaletteDepth
    {
        get { return fGet().PaletteDepth; }
        set { fGet().PaletteDepth = value; }
    }

    /// <summary>The palette renderer's glow - see UWPaletteEffects.</summary>
    public static bool PaletteGlow
    {
        get { return fGet().PaletteGlow; }
        set { fGet().PaletteGlow = value; }
    }

    /// <summary>The palette renderer's light sources - see UWPaletteEffects.</summary>
    public static bool PaletteLightSources
    {
        get { return fGet().PaletteLightSources; }
        set { fGet().PaletteLightSources = value; }
    }

    /// <summary>The multiple of the original's resolution - see UWWorldResolution.Factor.</summary>
    public static int OriginalWorldResolutionFactor
    {
        get { return fGet().OriginalWorldResolutionFactor; }
        set { fGet().OriginalWorldResolutionFactor = value; }
    }

    /// <summary>The head bob setting (UWHeadBobRules.ModeEnum); read every frame. An unknown
    /// number from the file reads as Original.</summary>
    public static UWDataImport.UWData.UWHeadBobRules.ModeEnum HeadBobMode
    {
        get
        {
            int liMode = fGet().HeadBobMode;

            return liMode >= 0 && liMode <= (int)UWDataImport.UWData.UWHeadBobRules.ModeEnum.Smooth
                ? (UWDataImport.UWData.UWHeadBobRules.ModeEnum)liMode : UWDataImport.UWData.UWHeadBobRules.ModeEnum.Original;
        }

        set { fGet().HeadBobMode = (int)value; }
    }

    /// <summary>The Smooth head bob's strength, clamped to 0.1 - 1; Original always has the full one.</summary>
    public static float HeadBobStrength
    {
        get { return Mathf.Clamp(fGet().HeadBobStrength, MinHeadBobStrength, 1f); }
        set { fGet().HeadBobStrength = Mathf.Clamp(value, MinHeadBobStrength, 1f); }
    }

    /// <summary>Below this the Smooth bob is not worth having - Off is the switch for none.</summary>
    public const float MinHeadBobStrength = 0.1f;

    /// <summary>The weapon jitter setting, like HeadBobMode.</summary>
    public static UWDataImport.UWData.UWHeadBobRules.ModeEnum WeaponJitterMode
    {
        get
        {
            int liMode = fGet().WeaponJitterMode;

            return liMode >= 0 && liMode <= (int)UWDataImport.UWData.UWHeadBobRules.ModeEnum.Smooth
                ? (UWDataImport.UWData.UWHeadBobRules.ModeEnum)liMode : UWDataImport.UWData.UWHeadBobRules.ModeEnum.Original;
        }

        set { fGet().WeaponJitterMode = (int)value; }
    }

    /// <summary>Whether the player's motion runs in the precision mode (true, Smooth) or as the
    /// original's arithmetic call by call at its frame length (false, Original).</summary>
    public static bool MotionSmooth
    {
        get { return fGet().MotionSmoothing != 0; }
        set { fGet().MotionSmoothing = value ? 1 : 0; }
    }

    /// <summary>Whether the view is set once per simulation step (true, the original's picture
    /// rate) or drawn between the steps (false, even).</summary>
    public static bool MotionViewStepped
    {
        get { return fGet().MotionViewStepped != 0; }
        set { fGet().MotionViewStepped = value ? 1 : 0; }
    }

    /// <summary>The Original motion's frame length in ticks, one of OriginalFrameChoices.</summary>
    public static int OriginalFrameTicks
    {
        get
        {
            int liTicks = fGet().OriginalFrameTicks;

            foreach (int liChoice in OriginalFrameChoices)
            {
                if (liChoice == liTicks)
                    return liTicks;
            }

            return 8;
        }

        set { fGet().OriginalFrameTicks = value; }
    }

    /// <summary>4, 8, 12, 16 ticks: 64, 32, 21, 16 frames a second.</summary>
    public static readonly int[] OriginalFrameChoices = { 4, 8, 12, 16 };

    /// <summary>The Smooth ramp's frame in ticks, one of SmoothRampChoices.</summary>
    public static int SmoothRampTicks
    {
        get
        {
            int liTicks = fGet().SmoothRampTicks;

            foreach (int liChoice in SmoothRampChoices)
            {
                if (liChoice == liTicks)
                    return liTicks;
            }

            return 8;
        }

        set { fGet().SmoothRampTicks = value; }
    }

    /// <summary>16, 8, 4, 0 ticks: 0.6 s, 0.3 s, 0.15 s, at once.</summary>
    public static readonly int[] SmoothRampChoices = { 16, 8, 4, 0 };

    public static readonly string[] SmoothRampLabels = { "0.6 s", "0.3 s", "0.15 s", "At once" };

    /// <summary>The Smooth weapon jitter's strength, clamped like HeadBobStrength.</summary>
    public static float WeaponJitterStrength
    {
        get { return Mathf.Clamp(fGet().WeaponJitterStrength, MinHeadBobStrength, 1f); }
        set { fGet().WeaponJitterStrength = Mathf.Clamp(value, MinHeadBobStrength, 1f); }
    }

    /// <summary>Music and sound. Everything that changes them goes through UWSoundOptions,
    /// which also puts them into the live settings.</summary>
    public static bool MusicEnabled
    {
        get { return fGet().MusicEnabled; }
        set { fGet().MusicEnabled = value; }
    }

    public static bool SoundEnabled
    {
        get { return fGet().SoundEnabled; }
        set { fGet().SoundEnabled = value; }
    }

    public static float MusicVolume
    {
        get { return Mathf.Clamp01(fGet().MusicVolume); }
        set { fGet().MusicVolume = Mathf.Clamp01(value); }
    }

    public static float SoundVolume
    {
        get { return Mathf.Clamp01(fGet().SoundVolume); }
        set { fGet().SoundVolume = Mathf.Clamp01(value); }
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(fGetPath(), JsonUtility.ToJson(fGet(), true));
        }
        catch (Exception lOError)
        {
            Debug.LogWarning("Settings not saved: " + lOError.Message);
        }
    }

    private static Data fGet()
    {
        if (msData != null)
            return msData;

        try
        {
            string lsPath = fGetPath();

            if (File.Exists(lsPath))
                msData = JsonUtility.FromJson<Data>(File.ReadAllText(lsPath));
        }
        catch (Exception lOError)
        {
            Debug.LogWarning("Settings not read: " + lOError.Message);
        }

        if (msData == null)
            msData = new Data();

        return msData;
    }

    private static string fGetPath()
    {
        return Path.Combine(Application.persistentDataPath, FileName);
    }
}
