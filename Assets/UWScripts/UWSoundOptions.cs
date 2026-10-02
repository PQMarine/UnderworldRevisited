using UnityEngine;

/// <summary>
/// The sound settings that the player changes and that have to survive the program: music and
/// sound on or off, and the two volumes.
///
/// THE ORIGINAL HAS ONLY ON AND OFF, in the options panel of the button bar. The volumes are
/// ours (per user, 2026-09-21, when the Sound entry was added to the menu bar of the main
/// screen) - an AdLib card was set up outside the game, so the game had nothing to offer there.
///
/// WHY A CLASS OF ITS OWN: the live values sit in UWSettings, a ScriptableObject, so that the
/// Inspector shows and can change them while playing. A build never writes that asset back,
/// which is why the lasting copy lives in UWUserSettings (settings.json) - the same split as
/// UWColourVision and UWGraphicsDetail. Everything that CHANGES a value goes through here:
/// the in-game options panel (UWHudOptions) and the Sound menu of UWSetupMenu.
/// </summary>
public static class UWSoundOptions
{
    /// <summary>The steps the volume sliders snap to, and what their arrows move by. Five
    /// percent, so a round value can be hit (as with the easy movement interval).</summary>
    public const float VolumeStep = 0.05f;

    public static bool MusicEnabled
    {
        get
        {
            fEnsureLoaded();

            return UWUserSettings.MusicEnabled;
        }

        set
        {
            fEnsureLoaded();

            UWUserSettings.MusicEnabled = value;
            fApplyAndSave();

            if (!value && UWAudioEngine.Instance != null)
                UWAudioEngine.Instance.StopMusic();
        }
    }

    public static bool SoundEnabled
    {
        get
        {
            fEnsureLoaded();

            return UWUserSettings.SoundEnabled;
        }

        set
        {
            fEnsureLoaded();

            UWUserSettings.SoundEnabled = value;
            fApplyAndSave();
        }
    }

    public static float MusicVolume
    {
        get
        {
            fEnsureLoaded();

            return UWUserSettings.MusicVolume;
        }

        set
        {
            fEnsureLoaded();

            UWUserSettings.MusicVolume = Mathf.Clamp01(value);
            fApplyAndSave();
        }
    }

    public static float SoundVolume
    {
        get
        {
            fEnsureLoaded();

            return UWUserSettings.SoundVolume;
        }

        set
        {
            fEnsureLoaded();

            UWUserSettings.SoundVolume = Mathf.Clamp01(value);
            fApplyAndSave();
        }
    }

    /// <summary>
    /// Puts what was saved into the live settings. Called by itself before every access, and
    /// by UWAudioEngine before it reads, so that a fresh start plays at the saved volume even
    /// if nobody has opened a menu.
    /// </summary>
    public static void EnsureLoaded()
    {
        fEnsureLoaded();
    }

    /// <summary>
    /// A LOADED SAVE BRINGS ITS OWN TWO SWITCHES. In the original they live in PLAYER.DAT
    /// 0xB5 and nowhere else, so switching the music off and saving leaves it off in that save
    /// while another one keeps what it was saved with (per user, tried in the original
    /// 2026-09-21); the loading routine hands both bits straight to the two toggles
    /// (UWPlayerData.SettingsOffset). We do the same and let it stand in settings.json as
    /// well, so the state one played with is also the one a fresh start begins with.
    ///
    /// THE VOLUMES STAY OURS and are not touched here - the original has no place for them.
    /// </summary>
    public static void ApplyFromSave(bool pbMusic, bool pbSound)
    {
        fEnsureLoaded();

        UWUserSettings.MusicEnabled = pbMusic;
        UWUserSettings.SoundEnabled = pbSound;

        fApplyAndSave();

        if (!pbMusic && UWAudioEngine.Instance != null)
            UWAudioEngine.Instance.StopMusic();
    }

    private static bool mbLoaded;

    private static void fEnsureLoaded()
    {
        if (mbLoaded)
            return;

        mbLoaded = true;

        fApply();
    }

    private static void fApplyAndSave()
    {
        fApply();
        UWUserSettings.Save();
    }

    private static void fApply()
    {
        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        if (lOSettings == null)
            return;

        lOSettings.MusicEnabled = UWUserSettings.MusicEnabled;
        lOSettings.SoundEnabled = UWUserSettings.SoundEnabled;
        lOSettings.MusicVolume = UWUserSettings.MusicVolume;
        lOSettings.SoundVolume = UWUserSettings.SoundVolume;
    }
}
