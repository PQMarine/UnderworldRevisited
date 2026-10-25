using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// The game's one door to the sound: the effects, the music themes, the instruments.
/// Cleanup step 6.6 (2026-10-24): absorbs UWSoundEffects and UWMusic, which each held their own
/// reference to the game data and to UWAudioEngine (now UWAudioMixer). The effect numbers are
/// engine-free in UWSoundEffectId, the themes in UWMusicSelector.
///
/// THE MUSIC: which theme plays when is engine-free in UWMusicSelector since 2026-09-18 (P3 of
/// the engine separation); this class hands the selector the game time and plays the pieces
/// through UWAudioMixer, which picks the file for the device (UWAudioMixCore.SelectPiece).
///
/// DISTANCE as in the original (seg014_8AE): distance in eighths of a tile, full volume
/// up to 8, nothing from 48, linear in between. Stereo panning is dropped - the AdLib is
/// mono, and it was in 1992 too.
///
/// WHERE ONE HEARS FROM: Camera.main, the camera on the player's body (fListenerPosition, the one
/// place that decides it). Not UWViewpoint.Current: that one follows a detached camera, and
/// during Roaming Sight the sounds would then be measured from the flying eye instead of from
/// the figure (2026-10-24, kept as it was - a change would be heard).
/// </summary>
public static class UWAudioService
{
    private static DataImport mOData;

    private static readonly UWMusicOutput msOOutput = new UWMusicOutput();

    private static readonly UWMusicSelector msOSelector = new UWMusicSelector(msOOutput);

    /// <summary>At the start of a scene: the sound engine (made once, it survives the scene),
    /// the theme logic and the effects get the game data. If a piece from the previous scene is
    /// still playing - the sound survives the restart (see UWAudioMixer.Ensure) -, it stays,
    /// theme included.</summary>
    public static void Init(DataImport pOData)
    {
        UWAudioMixer.Ensure(pOData);

        msOOutput.Data = pOData;
        msOSelector.Reset();

        mOData = pOData;
    }

    // ------------------------------------------------------------------
    // Effects
    // ------------------------------------------------------------------

    /// <summary>The distance counts in eighths of a tile (see UWSoundAttenuation).</summary>
    private const float UnitsPerTile = 8f;

    /// <summary>Without a position, with an offset to the entry's velocity.</summary>
    public static void PlayEffect(int piEffect, int piVelocityOffset = 0)
    {
        UWAudioMixer lOMixer = UWAudioMixer.Instance;

        if (lOMixer == null || mOData == null || mOData.Sound == null)
            return;

        if (piEffect < 0 || piEffect >= mOData.Sound.Effects.Count)
            return;

        lOMixer.PlayEffect(mOData.Sound.Effects[piEffect], piVelocityOffset);
    }

    /// <summary>At a position in the world, with distance attenuation relative to the player.</summary>
    public static void PlayEffectAt(int piEffect, Vector3 pOWorldPosition, int piVolumeDelta = 0)
    {
        if (mOData == null || mOData.Sound == null || piEffect < 0 || piEffect >= mOData.Sound.Effects.Count)
            return;

        Vector3 lOListener;

        if (!fListenerPosition(out lOListener))
        {
            PlayEffect(piEffect, piVolumeDelta);
            return;
        }

        float lfDx = (pOWorldPosition.x - lOListener.x) / UWWorldScale.TileSize * UnitsPerTile;
        float lfDz = (pOWorldPosition.z - lOListener.z) / UWWorldScale.TileSize * UnitsPerTile;
        int liDistance = Mathf.FloorToInt(Mathf.Sqrt((lfDx * lfDx) + (lfDz * lfDz)));

        // The attenuation is the original's (UWSoundAttenuation, P3 of the engine separation).
        int liBase = mOData.Sound.Effects[piEffect].Velocity;
        int liVolume = UWSoundAttenuation.GetVolume(liDistance, liBase, piVolumeDelta);

        if (liVolume < 0)
            return;

        PlayEffect(piEffect, liVolume - liBase);
    }

    /// <summary>Where the sounds are heard from - see the class comment. False without a main
    /// camera (the tools' spectator): the sound then plays at full volume.</summary>
    private static bool fListenerPosition(out Vector3 pOPosition)
    {
        Camera lOCamera = Camera.main;

        pOPosition = lOCamera != null ? lOCamera.transform.position : Vector3.zero;

        return lOCamera != null;
    }

    /// <summary>
    /// THE RUSHING OF THE WATER: while the player is in water, the original keeps effect 0
    /// going and ends it when he leaves - SurfaceFootsteps_seg034_2F89_713 plays it in place
    /// of the footsteps while PLAYER.DAT byte 0xB8 bit 0 is set, keeps the handle in
    /// dseg_5c99_77E and gives it back after 0x1800 counts, which starts it afresh. That is
    /// what one hears as a continuous rushing (per user in the original, 2026-09-21).
    ///
    /// Called every frame from UWPlayerTerrain: it starts the sound, starts it again when the
    /// program has ended, and cuts it off on leaving the water.
    /// </summary>
    public static void UpdateWaterLoop(bool pbInWater)
    {
        UWAudioMixer lOMixer = UWAudioMixer.Instance;

        if (lOMixer == null || mOData == null || mOData.Sound == null
            || UWSoundEffectId.Water >= mOData.Sound.Effects.Count)
            return;

        if (!pbInWater)
        {
            if (miWaterVoice >= 0)
            {
                lOMixer.StopHeldEffect(miWaterVoice);
                miWaterVoice = -1;
            }

            return;
        }

        if (miWaterVoice >= 0 && lOMixer.IsHeldEffectPlaying(miWaterVoice))
            return;

        miWaterVoice = lOMixer.StartHeldEffect(mOData.Sound.Effects[UWSoundEffectId.Water], 0);
    }

    /// <summary>The voice of the water rushing, or -1 - see UpdateWaterLoop.</summary>
    private static int miWaterVoice = -1;

    // ------------------------------------------------------------------
    // Music
    // ------------------------------------------------------------------

    public static int CurrentTheme => msOSelector.CurrentTheme;

    public static int NewTheme => msOSelector.NewTheme;

    /// <summary>Is a piece currently playing?</summary>
    public static bool IsMusicPlaying => msOOutput.IsPlaying;

    /// <summary>Request a theme. Not possible during the fanfare.</summary>
    public static void ChangeTheme(int piTheme)
    {
        msOSelector.ChangeTheme(piTheme);
    }

    /// <summary>The weapon is drawn - see UWMusicSelector.OnWeaponDrawn.</summary>
    public static void OnWeaponDrawn()
    {
        msOSelector.OnWeaponDrawn();
    }

    /// <summary>Combat mode left - see UWMusicSelector.OnCombatModeLeft.</summary>
    public static void OnCombatModeLeft()
    {
        msOSelector.OnCombatModeLeft();
    }

    /// <summary>One of the three level themes, at random.</summary>
    public static void PickLevelTheme()
    {
        msOSelector.PickLevelTheme();
    }

    /// <summary>After map, conversation or weapon put away: Armed with a drawn weapon, otherwise
    /// a level theme.</summary>
    public static void ResumeAfterInterruption(bool pbWeaponDrawn)
    {
        msOSelector.ResumeAfterInterruption(pbWeaponDrawn);
    }

    /// <summary>A blow has been struck - the combat theme keeps playing.</summary>
    public static void MarkCombat()
    {
        msOSelector.MarkCombat(Time.time);
    }

    /// <summary>The player was hit: if still doing well, the undecided combat theme,
    /// otherwise the losing one (reference: damage.cs), the winning one when his own missile hit
    /// him (pbByPlayer, UWMusicSelector.PlayerHurtTheme).</summary>
    public static void OnPlayerDamaged(float pfCurrentHealth, float pfMaxHealth, bool pbByPlayer)
    {
        msOSelector.OnPlayerDamaged(pfCurrentHealth, pfMaxHealth, Time.time, pbByPlayer);
    }

    /// <summary>Every frame, from UWAudioMixer.Update: reconcile the request with what is
    /// playing (reference: RefreshMusic).</summary>
    public static void RefreshMusic(bool pbWeaponDrawn)
    {
        // Without a world nothing starts by itself until a theme has been asked for: at the start
        // the selector took the silence for a piece that had ended and played a level theme for
        // the second until the main menu asked for its own (per user, 2026-10-08).
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader != null && !lOLoader.HasWorld && msOSelector.CurrentTheme == 0 && msOSelector.NewTheme == 0)
            return;

        msOSelector.Refresh(pbWeaponDrawn, Time.time);
    }

    /// <summary>The music device was switched while a piece played (UWAudioMixer): that theme
    /// starts again on the new one.</summary>
    public static void ReplayMusic()
    {
        int liTheme = msOSelector.CurrentTheme;

        if (msOOutput.IsReady && liTheme > 0)
            msOOutput.Play(liTheme);
    }

    /// <summary>The music switched off (UWSoundOptions).</summary>
    public static void StopMusic()
    {
        msOOutput.Stop();
    }

    /// <summary>The pieces: the theme number in octal as the file number, played through the
    /// mixer, which takes the version for the device.</summary>
    private sealed class UWMusicOutput : IUWMusicOutput
    {
        public DataImport Data;

        public bool IsReady => UWAudioMixer.Instance != null && UWAudioMixer.Instance.IsReady && Data != null;

        public bool IsPlaying => UWAudioMixer.Instance != null && UWAudioMixer.Instance.IsMusicPlaying;

        public void Play(int piTheme)
        {
            UWAudioMixer lOMixer = UWAudioMixer.Instance;

            if (lOMixer == null)
                return;

            lOMixer.PlayPiece(Data != null ? Data.Sound : null, UWMusicSelector.GetFileNumber(piTheme));
        }

        public void Stop()
        {
            if (UWAudioMixer.Instance != null)
                UWAudioMixer.Instance.StopMusic();
        }
    }

    // ------------------------------------------------------------------
    // Instruments (UWInstrumentPlayer)
    // ------------------------------------------------------------------

    /// <summary>See UWAudioMixCore.BeginInstrument.</summary>
    public static void BeginInstrument(int piProgram)
    {
        if (UWAudioMixer.Instance != null)
            UWAudioMixer.Instance.BeginInstrument(piProgram);
    }

    public static void PlayInstrumentNote(int piNote)
    {
        if (UWAudioMixer.Instance != null)
            UWAudioMixer.Instance.PlayInstrumentNote(piNote);
    }

    public static void StopInstrumentNote(int piNote)
    {
        if (UWAudioMixer.Instance != null)
            UWAudioMixer.Instance.StopInstrumentNote(piNote);
    }

    /// <summary>See UWAudioMixCore.EndInstrument.</summary>
    public static void EndInstrument()
    {
        if (UWAudioMixer.Instance != null)
            UWAudioMixer.Instance.EndInstrument();
    }
}
