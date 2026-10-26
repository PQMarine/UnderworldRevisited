using UnityEngine;
using UWDataImport;
using UWDataImport.Audio;
using UWDataImport.UWData;

/// <summary>
/// The sound of the game in Unity: two emulated AdLib chips (UWOpl2), one for the music
/// (UWAdlibMusicDevice), one for the sound effects (UWEffectSynth), or the music on General MIDI
/// or the MT-32 (UWMusicDeviceSelector), mixed in Unity's audio thread via OnAudioFilterRead.
///
/// Cleanup step 6.6 (2026-10-24): UWAudioEngine became this class and the engine-free
/// UWAudioMixCore, which holds the devices, the effects and the mix. What stays here is what
/// needs Unity: the AudioSource the filter hangs on, the audio thread, the lock, the settings
/// read every frame, the device switch from the menu and the paths of the soundfont and the ROMs.
///
/// THE LOCK: whatever comes from the game thread - a sound effect, a new piece - goes into the
/// core under mOLock, and OnAudioFilterRead mixes under it; nothing on the audio thread
/// allocates. Making a device (a soundfont or ROMs loaded) happens outside it.
///
/// Volumes and switches come from the settings (UWSettings), read anew every frame into one
/// UWAudioSettings snapshot, so they can be adjusted in the Inspector.
/// </summary>
public class UWAudioMixer : MonoBehaviour
{
    public static UWAudioMixer Instance { get; private set; }

    /// <summary>The device the music plays on now: the one the settings ask for, unless it
    /// could not start - then the AdLib.</summary>
    public UWMusicDeviceId MusicDevice => mOCore != null ? mOCore.MusicDevice : UWMusicDeviceId.AdLib;

    /// <summary>The soundfont of the General MIDI music, cut from FluidR3_GM by
    /// Tools/UWSoundFontTrim.</summary>
    public static string GeneralMidiSoundFontPath =>
        System.IO.Path.Combine(Application.streamingAssetsPath, "UWMusic", "UWGeneralMidi.sf2");

    public static bool IsGeneralMidiAvailable => System.IO.File.Exists(GeneralMidiSoundFontPath);

    /// <summary>Where the player's MT-32 ROMs are looked for: the folder chosen in the settings,
    /// else "MT32" beside settings.json.</summary>
    public static string Mt32RomFolder =>
        !string.IsNullOrEmpty(UWUserSettings.Mt32RomPath) ? UWUserSettings.Mt32RomPath
            : System.IO.Path.Combine(Application.persistentDataPath, "MT32");

    /// <summary>The MT-32 ROMs in Mt32RomFolder, looked for again when the folder or its contents
    /// changed; null when there are none, Mt32Problem then says why.</summary>
    public static UWMt32MusicDriver.RomSet Mt32Roms => UWMusicDeviceSelector.GetMt32Roms(Mt32RomFolder);

    public static string Mt32Problem => UWMusicDeviceSelector.GetMt32Problem(Mt32RomFolder);

    private readonly object mOLock = new object();

    private UWAudioMixCore mOCore;

    private int miRequestedDevice = -1;

    private volatile bool mbReady;

    /// <summary>The settings the audio thread reads, replaced (never changed) by the game
    /// thread.</summary>
    private volatile UWAudioSettings mOSettings = UWAudioSettings.Initial;

    /// <summary>The settings now (see UWAudioSettings).</summary>
    public UWAudioSettings Settings => mOSettings;

    /// <summary>Is a piece playing? One that has played to the end no longer counts.</summary>
    public bool IsMusicPlaying
    {
        get
        {
            if (mOCore == null)
                return false;

            lock (mOLock)
            {
                return mOCore.IsMusicPlaying;
            }
        }
    }

    public bool IsReady => mbReady;

    /// <summary>Creates the sound engine if it does not exist yet.</summary>
    public static UWAudioMixer Ensure(DataImport pOData)
    {
        if (Instance != null)
            return Instance;

        GameObject lOObject = new GameObject("UW Sound");

        // SURVIVES THE SCENE RESTART. Loading and starting a new character rebuild the
        // scene; in the original the music simply keeps playing through that - the
        // intro music until its end, then a random level theme follows (per user,
        // 2026-09-11). The timbres are pure data and apply to every scene.
        DontDestroyOnLoad(lOObject);

        Instance = lOObject.AddComponent<UWAudioMixer>();
        Instance.fInitialise(pOData);

        return Instance;
    }

    private void fInitialise(DataImport pOData)
    {
        UWAdlibBank lOBank = pOData != null && pOData.Sound != null ? pOData.Sound.AdlibBank : null;

        if (lOBank == null || !lOBank.IsLoaded)
        {
            Debug.LogWarning("UWAudioMixer: UW.AD not found - no sound.");
            return;
        }

        int liOutputRate = AudioSettings.outputSampleRate;

        if (liOutputRate <= 0)
            liOutputRate = 48000;

        mOCore = new UWAudioMixCore(lOBank, liOutputRate, GeneralMidiSoundFontPath, () => Mt32RomFolder,
            psMessage => Debug.Log("UWAudioMixer: " + psMessage),
            psMessage => Debug.LogWarning("UWAudioMixer: " + psMessage));

        fReadSettings();

        // A silent loop, so the source plays and the filter gets called.
        AudioClip lOSilence = AudioClip.Create("UW Silence", liOutputRate, 1, liOutputRate, false);
        lOSilence.SetData(new float[liOutputRate], 0);

        AudioSource lOSource = gameObject.AddComponent<AudioSource>();
        lOSource.clip = lOSilence;
        lOSource.loop = true;
        lOSource.playOnAwake = false;
        lOSource.spatialBlend = 0f;
        lOSource.volume = 1f;

        UWAudioListenerGuard.Ensure(null);

        // The device from the settings before the first piece can start, so the intro does not
        // begin on the AdLib for a moment and start again on it (per user, 2026-10-08).
        miRequestedDevice = (int)mOSettings.MusicDevice;
        fSelectDevice(mOSettings.MusicDevice);

        mbReady = true;
        lOSource.Play();
    }

    private Interaction mOInteraction;

    private void Update()
    {
        fReadSettings();

        if (mOInteraction == null)
            mOInteraction = UWScene.Interaction;

        // A device chosen in the menu takes over at once: the piece playing starts again on it.
        int liWanted = (int)mOSettings.MusicDevice;

        if (mbReady && liWanted != miRequestedDevice)
        {
            miRequestedDevice = liWanted;

            bool lbWasPlaying = IsMusicPlaying;

            if (fSelectDevice((UWMusicDeviceId)liWanted) && lbWasPlaying)
                UWAudioService.ReplayMusic();
        }

        UWAudioService.RefreshMusic(mOInteraction != null && mOInteraction.IsCombatModeActive);
    }

    /// <summary>Switches the music to a device, the AdLib if that one cannot start; whatever was
    /// playing stops. True when the device changed. The device is made outside the lock.</summary>
    private bool fSelectDevice(UWMusicDeviceId peWanted)
    {
        UWMusicDeviceId leDevice = mOCore.ResolveDevice(peWanted);

        lock (mOLock)
        {
            return mOCore.SwitchDevice(leDevice);
        }
    }

    private void fReadSettings()
    {
        // The saved values themselves (UWSoundOptions, settings.json) - until cleanup step 8.8
        // (2026-10-24) a live copy of them in UWSettings, filled here at the latest.
        float lfMusic = UWSoundOptions.MusicEnabled ? UWSoundOptions.MusicVolume : 0f;
        float lfEffects = UWSoundOptions.SoundEnabled ? UWSoundOptions.SoundVolume : 0f;

        mOSettings = mOSettings.With(lfMusic, lfEffects, (UWMusicDeviceId)UWUserSettings.MusicDevice);
    }

    // ------------------------------------------------------------------
    // Game thread
    // ------------------------------------------------------------------

    /// <summary>Plays a theme's piece from the start (UWAudioMixCore.SelectPiece chooses the file
    /// for the device, outside the lock); without one the music stops.</summary>
    public void PlayPiece(UWSound pOSound, string psNumber)
    {
        if (!mbReady)
            return;

        UWXmiVersion leVersion;
        UWXmi.Sequence lOSequence = mOCore.SelectPiece(pOSound, psNumber, out leVersion);

        if (lOSequence == null)
        {
            StopMusic();
            return;
        }

        lock (mOLock)
        {
            mOCore.PlayMusic(lOSequence, false, leVersion);
        }
    }

    public void StopMusic()
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            mOCore.StopMusic();
        }
    }

    /// <summary>Starts a sound effect from SOUNDS.DAT (UWEffectSynth.Play); none while the
    /// sound is off.</summary>
    public void PlayEffect(UWSound.Effect pOEffect, int piVelocityOffset)
    {
        if (!mbReady || mOSettings.EffectGain <= 0f)
            return;

        lock (mOLock)
        {
            mOCore.PlayEffect(pOEffect, piVelocityOffset);
        }
    }

    /// <summary>Starts an effect and holds it (UWEffectSynth.StartHeld): the voice, or -1.</summary>
    public int StartHeldEffect(UWSound.Effect pOEffect, int piVelocityOffset)
    {
        if (!mbReady || mOSettings.EffectGain <= 0f)
            return -1;

        lock (mOLock)
        {
            return mOCore.StartHeldEffect(pOEffect, piVelocityOffset);
        }
    }

    /// <summary>Whether a held effect is still sounding - see StartHeldEffect.</summary>
    public bool IsHeldEffectPlaying(int piVoice)
    {
        if (mOCore == null)
            return false;

        lock (mOLock)
        {
            return mOCore.IsHeldEffectPlaying(piVoice);
        }
    }

    /// <summary>Ends a held effect - see StartHeldEffect.</summary>
    public void StopHeldEffect(int piVoice)
    {
        if (mOCore == null)
            return;

        lock (mOLock)
        {
            mOCore.StopHeldEffect(piVoice);
        }
    }

    /// <summary>See UWAudioMixCore.BeginInstrument.</summary>
    public void BeginInstrument(int piProgram)
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            mOCore.BeginInstrument(piProgram);
        }
    }

    public void PlayInstrumentNote(int piNote)
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            mOCore.PlayInstrumentNote(piNote);
        }
    }

    public void StopInstrumentNote(int piNote)
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            mOCore.StopInstrumentNote(piNote);
        }
    }

    /// <summary>See UWAudioMixCore.EndInstrument.</summary>
    public void EndInstrument()
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            mOCore.EndInstrument();
        }
    }

    // ------------------------------------------------------------------
    // Audio thread
    // ------------------------------------------------------------------

    private void OnAudioFilterRead(float[] pfData, int piChannels)
    {
        if (!mbReady || piChannels <= 0)
            return;

        UWAudioSettings lOSettings = mOSettings;

        lock (mOLock)
        {
            mOCore.Mix(pfData, piChannels, lOSettings.MusicGain, lOSettings.EffectGain);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        mbReady = false;

        if (mOCore == null)
            return;

        lock (mOLock)
        {
            mOCore.Dispose();
        }
    }
}
