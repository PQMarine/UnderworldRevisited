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

    /// <summary>The second game's theme logic (UW2 plan phase 5 part A), on the same output.</summary>
    private static readonly UWMusicSelectorUw2 msOSelectorUw2 = new UWMusicSelectorUw2(msOOutput);

    /// <summary>Is the sound the second game's? Its themes follow UWMusicSelectorUw2; the first game's requests
    /// (UW1 numbers, from code both games share) are left out then.</summary>
    public static bool IsUw2 => mOData != null && mOData.Sound != null && mOData.Sound.Game == UWGame.Uw2;

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
        piEffect = fRemapUw2(piEffect);

        UWAudioMixer lOMixer = UWAudioMixer.Instance;

        if (lOMixer == null || mOData == null || mOData.Sound == null)
            return;

        // THE SECOND GAME'S RECORDINGS FROM EFFECT 100 (found 2026-11-15, per user in the original: the effect trap on
        // level 72 laughs): play_effect takes an effect above 0x63 without a SOUNDS.DAT entry, at full volume in the
        // middle, and digi_fx_play plays SOUND/UWnn.VOC (nn = effect - 100) with the priority 0x7D00 (uw2
        // sound/SOUND.C). Ours dropped every such effect at the table's end.
        if (piEffect >= UWSoundsDat.FirstUwVocEffect && fPlayUwVoc(lOMixer, piEffect))
            return;

        if (piEffect < 0 || piEffect >= mOData.Sound.Effects.Count)
            return;

        if (!fPlayDigital(lOMixer, piEffect, piVelocityOffset))
            lOMixer.PlayEffect(mOData.Sound.Effects[piEffect], piVelocityOffset);
    }

    /// <summary>play_effect's two aliases (uw2 SOUND.C:1095; UW2 plan phase 5 part B): 0x5A plays as effect 1, 0x5B as
    /// effect 2 (on the speech card 1 as notes, which ours does not tell apart).</summary>
    private static int fRemapUw2(int piEffect)
    {
        if (!IsUw2)
            return piEffect;

        return piEffect == UWSoundsDat.AliasOfFirst ? 1 : piEffect == UWSoundsDat.AliasOfSecond ? 2 : piEffect;
    }

    /// <summary>digi_fx_play's priority for the UWnn recordings.</summary>
    private const int UwVocPriority = 0x7D00;

    /// <summary>A UWnn.VOC recording at full volume (the second game only; GetEffectVoc gives nothing for the first).</summary>
    private static bool fPlayUwVoc(UWAudioMixer pOMixer, int piEffect)
    {
        UWDataImport.Audio.UWVoc lOVoc = mOData.Sound.GetEffectVoc(piEffect);

        if (lOVoc == null || !lOVoc.IsLoaded)
            return false;

        pOMixer.PlayDigitalEffect(lOVoc.Samples, lOVoc.SampleRate, 1f, UwVocPriority);

        return true;
    }

    /// <summary>The volume at which the original's channel plays a recording to the full.</summary>
    private const int FullVolume = 0x7F;

    /// <summary>
    /// play_effect tries the one digital channel first (uw2 sound/SOUND.C:1117): an effect whose SOUNDS.DAT entry
    /// has the flag plays its SPnn.VOC there, when its priority (the entry's plus the volume) is at least the
    /// playing one's. False falls back to the note on the AdLib, as the original does. The first game has no flag.
    /// </summary>
    private static bool fPlayDigital(UWAudioMixer pOMixer, int piEffect, int piVelocityOffset)
    {
        UWSoundsDat lOTable = mOData.Sound.SoundsDat;

        if (lOTable == null || piEffect >= lOTable.Effects.Count || lOTable.Effects[piEffect].Digital == 0)
            return false;

        UWDataImport.Audio.UWVoc lOVoc = mOData.Sound.GetEffectVoc(piEffect);

        if (lOVoc == null || !lOVoc.IsLoaded)
            return false;

        int liVolume = Mathf.Clamp(lOTable.Effects[piEffect].Volume + piVelocityOffset, 0, FullVolume);

        return pOMixer.PlayDigitalEffect(lOVoc.Samples, lOVoc.SampleRate, liVolume / (float)FullVolume, lOTable.Effects[piEffect].Priority + liVolume);
    }

    /// <summary>At a position in the world, with distance attenuation relative to the player. In the second game a
    /// recording on the digital channel keeps its volume to the player's place while it plays, from this point or from
    /// pOFollow while that lives (FollowDigitalSource).</summary>
    public static void PlayEffectAt(int piEffect, Vector3 pOWorldPosition, int piVolumeDelta = 0, Transform pOFollow = null)
    {
        piEffect = fRemapUw2(piEffect);

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

        UWAudioMixer lOMixer = UWAudioMixer.Instance;
        int liTokenBefore = lOMixer != null ? lOMixer.DigitalToken : 0;

        PlayEffect(piEffect, liVolume - liBase);

        // update_digi_playback (uw2 sound/SOUND.C:612): digi_src type 1 (play_effect's point) or 2 (the missile
        // play_effect_on_mobile_src follows) - the first game re-reads nothing.
        if (IsUw2 && lOMixer != null && lOMixer.DigitalToken != liTokenBefore)
        {
            msiFollowToken = lOMixer.DigitalToken;
            msiFollowEffect = piEffect;
            msiFollowDelta = piVolumeDelta;
            msOFollowPoint = pOWorldPosition;
            msOFollowTransform = pOFollow;
        }
    }

    private static int msiFollowToken = -1;

    private static int msiFollowEffect;

    private static int msiFollowDelta;

    private static Vector3 msOFollowPoint;

    private static Transform msOFollowTransform;

    /// <summary>
    /// THE DIGITAL SOUND FOLLOWS THE PLAYER'S PLACE (sound audit point 4, built 2026-11-18): the second game re-reads
    /// the digital channel's volume every pass of its loop (update_digi_playback with sound_move) - from the point the
    /// sound came from, or from the missile that launched it - so walking away from a creaking door makes it fainter
    /// while it plays. Called by the mixer every frame; nothing once another recording has the channel. Stereo, which
    /// sound_move works out as well, stays out (UWSoundAttenuation).
    /// </summary>
    public static void FollowDigitalSource()
    {
        UWAudioMixer lOMixer = UWAudioMixer.Instance;

        if (lOMixer == null || msiFollowToken < 0 || mOData == null || mOData.Sound == null || !IsUw2)
            return;

        if (msiFollowToken != lOMixer.DigitalToken)
        {
            msiFollowToken = -1;
            msOFollowTransform = null;

            return;
        }

        if (msOFollowTransform != null)
            msOFollowPoint = msOFollowTransform.position;

        UWSoundsDat lOTable = mOData.Sound.SoundsDat;
        Vector3 lOListener;

        if (lOTable == null || msiFollowEffect >= lOTable.Effects.Count || msiFollowEffect >= mOData.Sound.Effects.Count
            || !fListenerPosition(out lOListener))
            return;

        float lfDx = (msOFollowPoint.x - lOListener.x) / UWWorldScale.TileSize * UnitsPerTile;
        float lfDz = (msOFollowPoint.z - lOListener.z) / UWWorldScale.TileSize * UnitsPerTile;
        int liDistance = Mathf.FloorToInt(Mathf.Sqrt((lfDx * lfDx) + (lfDz * lfDz)));
        int liBase = mOData.Sound.Effects[msiFollowEffect].Velocity;
        int liVolume = UWSoundAttenuation.GetVolume(liDistance, liBase, msiFollowDelta);

        // Out of reach the card plays on at nothing (sound_move gives 0), as the original's.
        int liDigital = liVolume < 0 ? 0 : Mathf.Clamp(lOTable.Effects[msiFollowEffect].Volume + (liVolume - liBase), 0, FullVolume);

        if (!lOMixer.SetDigitalVolume(msiFollowToken, liDigital / (float)FullVolume))
            msiFollowToken = -1;
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

    public static int CurrentTheme => IsUw2 ? msOSelectorUw2.CurrentTheme : msOSelector.CurrentTheme;

    public static int NewTheme => IsUw2 ? msOSelectorUw2.NewTheme : msOSelector.NewTheme;

    /// <summary>Is a piece currently playing?</summary>
    public static bool IsMusicPlaying => msOOutput.IsPlaying;

    /// <summary>Request a theme. Not possible during the fanfare.</summary>
    public static void ChangeTheme(int piTheme)
    {
        if (IsUw2)
            return;

        msOSelector.ChangeTheme(piTheme);
    }

    /// <summary>The weapon is drawn - see UWMusicSelector.OnWeaponDrawn.</summary>
    public static void OnWeaponDrawn()
    {
        if (IsUw2)
            return;

        msOSelector.OnWeaponDrawn();
    }

    /// <summary>Combat mode left - see UWMusicSelector.OnCombatModeLeft.</summary>
    public static void OnCombatModeLeft()
    {
        if (IsUw2)
            return;

        msOSelector.OnCombatModeLeft();
    }

    /// <summary>The piece playing ends, then a level theme - see UWMusicSelector.LetPieceEnd.</summary>
    public static void LetPieceEnd()
    {
        if (IsUw2)
            return;

        msOSelector.LetPieceEnd();
    }

    /// <summary>One of the three level themes, at random.</summary>
    public static void PickLevelTheme()
    {
        if (IsUw2)
            return;

        msOSelector.PickLevelTheme();
    }

    /// <summary>After map, conversation or weapon put away: Armed with a drawn weapon, otherwise
    /// a level theme.</summary>
    public static void ResumeAfterInterruption(bool pbWeaponDrawn)
    {
        if (IsUw2)
            return;

        msOSelector.ResumeAfterInterruption(pbWeaponDrawn);
    }

    /// <summary>A blow has been struck - the combat theme keeps playing.</summary>
    public static void MarkCombat()
    {
        if (IsUw2)
            msOSelectorUw2.MarkCombat(Time.time);
        else
            msOSelector.MarkCombat(Time.time);
    }

    /// <summary>The player was hit: if still doing well, the undecided combat theme,
    /// otherwise the losing one (reference: damage.cs), the winning one when his own missile hit
    /// him (pbByPlayer, UWMusicSelector.PlayerHurtTheme).</summary>
    public static void OnPlayerDamaged(int piCurrentHealth, int piMaxHealth, bool pbByPlayer)
    {
        if (IsUw2)
            return;

        msOSelector.OnPlayerDamaged(piCurrentHealth, piMaxHealth, Time.time, pbByPlayer);
    }

    /// <summary>Every frame, from UWAudioMixer.Update: reconcile the request with what is
    /// playing (reference: RefreshMusic).</summary>
    public static void RefreshMusic(bool pbWeaponDrawn)
    {
        // Without a world nothing starts by itself until a theme has been asked for: at the start
        // the selector took the silence for a piece that had ended and played a level theme for
        // the second until the main menu asked for its own (per user, 2026-10-08).
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (IsUw2)
        {
            fRefreshUw2();
            return;
        }

        if (lOLoader != null && !lOLoader.HasWorld && msOSelector.CurrentTheme == 0 && msOSelector.NewTheme == 0)
            return;

        msOSelector.Refresh(pbWeaponDrawn, Time.time);
    }

    // ------------------------------------------------------------------
    // The second game's music (UWMusicSelectorUw2)
    // ------------------------------------------------------------------

    /// <summary>The music was off on the last frame (turn_music: switched on again, it starts anew).</summary>
    private static bool mbUw2MusicOff;

    /// <summary>The level the player is on, 1-based (PlayerLevel), for the world of the walking themes.</summary>
    private static int fUw2Level
    {
        get
        {
            UWLevelLoader lOLoader = UWScene.LevelLoader;

            return lOLoader != null && UWWalkUw2.IsWalking ? lOLoader.CurrentLevelIndex + 1 : 1;
        }
    }

    /// <summary>player->drawn: the fight command chosen.</summary>
    private static bool fUw2Drawn => UWWalkUw2View.Current != null && UWGameScreenUw2.Command == UWGameScreenUw2.CommandEnum.Fight;

    /// <summary>
    /// EVERY FRAME, THE SECOND GAME (UW2 plan phase 5 part A): with the music off nothing changes (music_on), switched
    /// on it starts again (turn_music). The screens that loop on their own - the menu and the creation (MAINMENU.C:297,
    /// CHARGEN.C:467), a conversation (CONVERSE.C:335), the map (AUTOMAP.C:818) - let a piece over start again,
    /// loop_music_maybe; the walk keeps the right theme, change_music_maybe (INTERACT.C:115, from the main loop).
    /// </summary>
    private static void fRefreshUw2()
    {
        if (!UWSoundOptions.MusicEnabled)
        {
            mbUw2MusicOff = true;
            return;
        }

        if (mbUw2MusicOff)
        {
            mbUw2MusicOff = false;

            if (msOSelectorUw2.CurrentTheme != 0 || UWWalkUw2View.Current != null)
            {
                msOSelectorUw2.TurnOn(fUw2Level);
                return;
            }
        }

        // A cutscene keeps the music with change_music_maybe (anm_sound, CUTS.C), off the game screen.
        if (UWScene.IntroPlayer != null && UWScene.IntroPlayer.IsPlaying)
        {
            msOSelectorUw2.ChangeMusicMaybe(false, false, fUw2Level, Time.time);

            return;
        }

        bool lbLooping = UWMainMenu.IsOpen || UWConversationScreenUw2.IsOpen || UWMapScreenUw2.IsOpen || UWWalkUw2View.Current == null;

        if (lbLooping)
            msOSelectorUw2.LoopMusicMaybe();
        else
            msOSelectorUw2.ChangeMusicMaybe(fUw2Drawn, true, fUw2Level, Time.time);
    }

    /// <summary>set_new_music then change_music_maybe at once, as the screens ask (the menu's theme, MAINMENU.C:147;
    /// the armed theme on leaving the map, AUTOMAP.C:233).</summary>
    public static void Uw2RequestNow(int piTheme)
    {
        if (!IsUw2)
            return;

        msOSelectorUw2.SetNewMusic(piTheme);
        fUw2ChangeNow();
    }

    /// <summary>set_new_music: a theme asked for, taken by the next change_music_maybe.</summary>
    public static void Uw2Request(int piTheme)
    {
        if (IsUw2)
            msOSelectorUw2.SetNewMusic(piTheme);
    }

    /// <summary>set_random_walking_music (UWMusicSelectorUw2.PickFirst, PickRandom, PickKeep); pbNow runs
    /// change_music_maybe at once, as on entering a conversation, the map or sleep (CONVERSE.C:172, AUTOMAP.C:102,
    /// SKILLS.C:452).</summary>
    public static void Uw2Walking(int piPick, bool pbNow = false)
    {
        if (!IsUw2)
            return;

        msOSelectorUw2.SetRandomWalkingMusic(piPick, fUw2Level);

        if (pbNow)
            fUw2ChangeNow();
    }

    /// <summary>The weapon drawn or sheathed (pick_fightmode, punt_fightmode; UWGameScreenUw2.Command).</summary>
    public static void Uw2Drawn(bool pbDrawn)
    {
        if (!IsUw2)
            return;

        if (pbDrawn)
            msOSelectorUw2.OnWeaponDrawn();
        else
            msOSelectorUw2.OnWeaponSheathed(fUw2Level);
    }

    /// <summary>load_new_music: a theme at once, past the victory theme (the death theme, SKILLS.C:843).</summary>
    public static void Uw2Load(int piTheme)
    {
        if (IsUw2 && UWSoundOptions.MusicEnabled)
            msOSelectorUw2.LoadNewMusic(piTheme);
    }

    /// <summary>damage_critter with the player hit by someone (AI.C:1522; UW2 plan phase 5 part B): the danger theme
    /// below a quarter of the hit points, else the combat theme, and the combat timer.</summary>
    public static void Uw2PlayerDamaged(int piCurrentHealth, int piMaxHealth)
    {
        if (!IsUw2)
            return;

        msOSelectorUw2.SetNewMusic(UWMusicSelectorUw2.PlayerDamagedTheme(piCurrentHealth, piMaxHealth));
        msOSelectorUw2.MarkCombat(Time.time);
    }

    /// <summary>The second game's theme playing (get_current_music).</summary>
    public static int Uw2CurrentTheme => msOSelectorUw2.CurrentTheme;

    private static void fUw2ChangeNow()
    {
        if (UWSoundOptions.MusicEnabled)
            msOSelectorUw2.ChangeMusicMaybe(fUw2Drawn, !UWMainMenu.IsOpen && !UWConversationScreenUw2.IsOpen && !UWMapScreenUw2.IsOpen,
                fUw2Level, Time.time);
    }

    /// <summary>The music device was switched while a piece played (UWAudioMixer): that theme
    /// starts again on the new one.</summary>
    public static void ReplayMusic()
    {
        int liTheme = CurrentTheme;

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

            // The second game's pieces in the log, one line a piece (its test list compares them with the original).
            if (Data != null && Data.Sound != null && Data.Sound.Game == UWGame.Uw2)
                Debug.Log("[Music] Theme " + piTheme + " (" + UWSoundsDat.MusicName(UWGame.Uw2, piTheme, false) + ").");

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
