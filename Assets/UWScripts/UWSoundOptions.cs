using UnityEngine;

/// <summary>
/// The sound settings that the player changes and that have to survive the program: music and
/// sound on or off, and the two volumes.
///
/// THE ORIGINAL HAS ONLY ON AND OFF, in the options panel of the button bar. The volumes are
/// ours (per user, 2026-09-21, when the Sound entry was added to the menu bar of the main
/// screen) - an AdLib card was set up outside the game, so the game had nothing to offer there.
///
/// WHY A CLASS OF ITS OWN: everything that CHANGES a value goes through here - the in-game
/// options panel (UWHudOptions), the Sound menu of UWSetupMenu and a loaded save - so that each
/// change is saved at once and switching the music off stops it. The values live in
/// UWUserSettings (settings.json) and nowhere else since cleanup step 8.8 (2026-10-24); until then
/// they were copied into UWSettings as well, a live copy for the Inspector that UWAudioMixer read,
/// and the mixer asks here now.
/// </summary>
public static class UWSoundOptions
{
    /// <summary>The steps the volume sliders snap to, and what their arrows move by. Five
    /// percent, so a round value can be hit (as with the easy movement interval).</summary>
    public const float VolumeStep = UWDataImport.UWData.UWUserSettingsTable.VolumeStep;

    public static bool MusicEnabled
    {
        get
        {
            return UWUserSettings.MusicEnabled;
        }

        set
        {
            UWUserSettings.MusicEnabled = value;
            UWUserSettings.Save();

            if (!value)
                UWAudioService.StopMusic();
        }
    }

    public static bool SoundEnabled
    {
        get
        {
            return UWUserSettings.SoundEnabled;
        }

        set
        {
            UWUserSettings.SoundEnabled = value;
            UWUserSettings.Save();
        }
    }

    // THE VOLUMES ARE SAVED ONLY WHEN THEY CHANGE (found in cleanup 8.2, fixed per user
    // 2026-10-24): the sound dialog assigns its sliders' values on every OnGUI pass, and each
    // assignment wrote settings.json, several times a frame while the dialog was open.
    public static float MusicVolume
    {
        get
        {
            return UWUserSettings.MusicVolume;
        }

        set
        {
            float lfVolume = Mathf.Clamp01(value);

            // Only a change is saved: the sound dialog assigns its sliders on every pass.
            if (lfVolume == UWUserSettings.MusicVolume)
                return;

            UWUserSettings.MusicVolume = lfVolume;
            UWUserSettings.Save();
        }
    }

    public static float SoundVolume
    {
        get
        {
            return UWUserSettings.SoundVolume;
        }

        set
        {
            float lfVolume = Mathf.Clamp01(value);

            // Only a change is saved: the sound dialog assigns its sliders on every pass.
            if (lfVolume == UWUserSettings.SoundVolume)
                return;

            UWUserSettings.SoundVolume = lfVolume;
            UWUserSettings.Save();
        }
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
        UWUserSettings.MusicEnabled = pbMusic;
        UWUserSettings.SoundEnabled = pbSound;
        UWUserSettings.Save();

        if (!pbMusic)
            UWAudioService.StopMusic();
    }
}
