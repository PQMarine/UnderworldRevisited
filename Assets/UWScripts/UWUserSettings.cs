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
