using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The sound of the game: two emulated AdLib chips (UWOpl2), one for the music
/// (UWAdlibMusicDriver with UWXmiSequencer), one for the sound effects (UWTvfxVoice), mixed
/// in Unity's audio thread via OnAudioFilterRead.
///
/// The chips run at their 49716 Hz; conversion to the output rate is linear. The
/// sequencer and the sixtieth-of-a-second ticks of the effect engine run along in the audio
/// thread, so that a hitch in the frame does not make the music stutter. Whatever comes from
/// the game thread - a sound effect, a new piece - goes in under a lock.
///
/// Volumes and switches come from the settings (UWSettings), read anew every frame,
/// so they can be adjusted in the Inspector.
///
/// THE MUSIC DEVICE (2026-10-08, per user: "everything shall work out of the box"): the music can
/// instead play its MT-32 version (UW*.XMI) on General MIDI - UWGmMusicDriver with the soundfont
/// in StreamingAssets/UWMusic - which renders stereo at the output rate in blocks of
/// MidiBlockFrames, the sequencer advanced block by block - or on the MT-32 itself,
/// UWMt32MusicDriver (Munt's libmt32emu) with the player's own ROMs from Mt32RomFolder, rendered
/// the same way. The sound effects stay on the AdLib chip. A device that cannot start (no
/// soundfont, no ROMs, no library) leaves the AdLib playing.
/// </summary>
public class UWAudioEngine : MonoBehaviour
{
    public static UWAudioEngine Instance { get; private set; }

    /// <summary>The values of UWUserSettings.MusicDevice.</summary>
    public enum MusicDeviceEnum
    {
        AdLib,
        GeneralMidi,
        Mt32
    }

    /// <summary>The device the music plays on now: the one the settings ask for, unless it
    /// could not start - then the AdLib.</summary>
    public MusicDeviceEnum MusicDevice { get; private set; } = MusicDeviceEnum.AdLib;

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

    private static string msMt32CheckedFor;
    private static UWMt32MusicDriver.RomSet msOMt32Roms;
    private static string msMt32Problem;

    /// <summary>The MT-32 ROMs in Mt32RomFolder, looked for again when the folder or its contents
    /// changed; null when there are none, Mt32Problem then says why.</summary>
    public static UWMt32MusicDriver.RomSet Mt32Roms
    {
        get
        {
            string lsFolder = Mt32RomFolder;
            string lsKey = lsFolder + "|" + (System.IO.Directory.Exists(lsFolder)
                ? System.IO.Directory.GetLastWriteTimeUtc(lsFolder).Ticks.ToString() : "-");

            if (lsKey == msMt32CheckedFor)
                return msOMt32Roms;

            msMt32CheckedFor = lsKey;
            msOMt32Roms = null;
            msMt32Problem = null;

            try
            {
                msOMt32Roms = UWMt32MusicDriver.FindRoms(lsFolder);

                if (msOMt32Roms == null)
                    msMt32Problem = "no MT-32 or CM-32L ROMs in that folder";
            }
            catch (System.Exception lOError)
            {
                msMt32Problem = "the MT-32 emulation is missing on this system (" + lOError.GetType().Name + ")";
            }

            return msOMt32Roms;
        }
    }

    public static string Mt32Problem
    {
        get
        {
            _ = Mt32Roms;

            return msMt32Problem;
        }
    }

    /// <summary>How many frames the MIDI synthesizers render at once; the sequencer's events land
    /// on these steps (64 frames, 1.3 ms at 48 kHz).</summary>
    private const int MidiBlockFrames = 64;

    /// <summary>The General MIDI music's level beside the AdLib's, set by ear.</summary>
    private const float GeneralMidiGain = 1f;

    /// <summary>The MT-32's level beside the AdLib's, set by ear.</summary>
    private const float Mt32Gain = 1f;

    private UWGmMusicDriver mOGmDriver;
    private bool mbGmFailed;
    private UWMt32MusicDriver mOMt32Driver;
    private IUWMidiDriver mISequencerDriver;
    private readonly float[] mfMidiLeft = new float[MidiBlockFrames];
    private readonly float[] mfMidiRight = new float[MidiBlockFrames];
    private int miMidiBlockAt = MidiBlockFrames;
    private int miOutputRate = 48000;
    private int miRequestedDevice = -1;

    private const double TicksPerSecond = 60.0;

    private readonly object mOLock = new object();

    private UWOpl2 mOMusicChip;
    private UWOpl2 mOEffectChip;
    private UWAdlibBank mOBank;
    private UWAdlibMusicDriver mODriver;
    private UWXmiSequencer mOSequencer;
    private readonly UWTvfxVoice[] mOVoices = new UWTvfxVoice[UWOpl2.ChannelCount];

    private double mfRatio = 1.0;
    private double mfSourcePosition;
    private float mfPrevious;
    private float mfCurrent;
    private double mfTickAccumulator;

    private volatile bool mbReady;

    private float mfMusicGain;
    private float mfEffectGain;

    /// <summary>Is a piece playing? One that has played to the end no longer counts.</summary>
    public bool IsMusicPlaying
    {
        get
        {
            lock (mOLock)
            {
                return mOSequencer != null && !mOSequencer.IsFinished;
            }
        }
    }

    public bool IsReady => mbReady;

    /// <summary>Creates the sound engine if it does not exist yet.</summary>
    public static UWAudioEngine Ensure(DataImport pOData)
    {
        if (Instance != null)
            return Instance;

        GameObject lOObject = new GameObject("UW Sound");

        // SURVIVES THE SCENE RESTART. Loading and starting a new character rebuild the
        // scene; in the original the music simply keeps playing through that - the
        // intro music until its end, then a random level theme follows (per user,
        // 2026-09-11). The timbres are pure data and apply to every scene.
        DontDestroyOnLoad(lOObject);

        Instance = lOObject.AddComponent<UWAudioEngine>();
        Instance.fInitialise(pOData);

        return Instance;
    }

    private void fInitialise(DataImport pOData)
    {
        mOBank = pOData != null && pOData.Sound != null ? pOData.Sound.AdlibBank : null;

        if (mOBank == null || !mOBank.IsLoaded)
        {
            Debug.LogWarning("UWAudioEngine: UW.AD not found - no sound.");
            return;
        }

        mOMusicChip = new UWOpl2();
        mOEffectChip = new UWOpl2();
        mOEffectChip.WriteRegister(0x01, 0x20);
        mODriver = new UWAdlibMusicDriver(mOMusicChip, mOBank);

        for (int liAt = 0; liAt < mOVoices.Length; liAt++)
            mOVoices[liAt] = new UWTvfxVoice(liAt);

        int liOutputRate = AudioSettings.outputSampleRate;

        if (liOutputRate <= 0)
            liOutputRate = 48000;

        miOutputRate = liOutputRate;
        mfRatio = (double)UWOpl2.SampleRate / liOutputRate;
        mfSourcePosition = 1.0;

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

        fEnsureListener();

        // The device from the settings before the first piece can start, so the intro does not
        // begin on the AdLib for a moment and start again on it (per user, 2026-10-08).
        miRequestedDevice = UWUserSettings.MusicDevice;
        fSelectDevice((MusicDeviceEnum)miRequestedDevice);

        mbReady = true;
        lOSource.Play();
    }

    /// <summary>Without an AudioListener Unity stays silent (see UWSoundPlayer).</summary>
    private static void fEnsureListener()
    {
        foreach (AudioListener lOExisting in FindObjectsByType<AudioListener>())
        {
            if (lOExisting.enabled && lOExisting.gameObject.activeInHierarchy)
                return;
        }

        Camera lOCamera = Camera.main;

        if (lOCamera != null)
            lOCamera.gameObject.AddComponent<AudioListener>();
    }

    private Interaction mOInteraction;

    private void Update()
    {
        fReadSettings();

        if (mOInteraction == null)
            mOInteraction = UWScene.Interaction;

        // A device chosen in the menu takes over at once: the piece playing starts again on it.
        int liWanted = UWUserSettings.MusicDevice;

        if (mbReady && liWanted != miRequestedDevice)
        {
            miRequestedDevice = liWanted;

            bool lbWasPlaying = IsMusicPlaying;

            if (fSelectDevice((MusicDeviceEnum)liWanted) && lbWasPlaying)
                UWMusic.Replay();
        }

        UWMusic.Refresh(mOInteraction != null && mOInteraction.IsCombatModeActive);
    }

    /// <summary>Switches the music to a device, the AdLib if that one cannot start; whatever was
    /// playing stops. True when the device changed.</summary>
    private bool fSelectDevice(MusicDeviceEnum peWanted)
    {
        MusicDeviceEnum leDevice = MusicDeviceEnum.AdLib;

        if (peWanted == MusicDeviceEnum.GeneralMidi && fEnsureGmDriver())
            leDevice = MusicDeviceEnum.GeneralMidi;
        else if (peWanted == MusicDeviceEnum.Mt32 && fEnsureMt32Driver())
            leDevice = MusicDeviceEnum.Mt32;

        if (leDevice == MusicDevice)
            return false;

        lock (mOLock)
        {
            mOSequencer = null;
            mODriver.AllNotesOff();

            if (mOGmDriver != null)
                mOGmDriver.AllNotesOff();

            if (mOMt32Driver != null)
                mOMt32Driver.Reset();

            MusicDevice = leDevice;
        }

        return true;
    }

    /// <summary>The MT-32 emulation, made once from the ROMs found (Mt32Roms). Asked again on
    /// every switch to the MT-32, so ROMs put into the folder later are taken.</summary>
    private bool fEnsureMt32Driver()
    {
        if (mOMt32Driver != null)
            return true;

        string lsError;
        UWMt32MusicDriver lODriver = UWMt32MusicDriver.TryCreate(Mt32RomFolder, miOutputRate, out lsError);

        if (lODriver == null)
        {
            Debug.LogWarning("UWAudioEngine: MT-32 not available: " + lsError);

            return false;
        }

        Debug.Log("UWAudioEngine: MT-32 emulation with " + lODriver.Description);
        mOMt32Driver = lODriver;

        return true;
    }

    /// <summary>The synthesizer that renders the music now, or null on the AdLib.</summary>
    private IUWRenderingMidiDriver fRenderingDriver =>
        MusicDevice == MusicDeviceEnum.GeneralMidi ? (IUWRenderingMidiDriver)mOGmDriver
            : MusicDevice == MusicDeviceEnum.Mt32 ? mOMt32Driver : null;

    private bool fEnsureGmDriver()
    {
        if (mOGmDriver != null)
            return true;

        if (mbGmFailed)
            return false;

        try
        {
            mOGmDriver = new UWGmMusicDriver(GeneralMidiSoundFontPath, miOutputRate);
        }
        catch (System.Exception lOError)
        {
            mbGmFailed = true;
            Debug.LogWarning("UWAudioEngine: General MIDI not available (" + GeneralMidiSoundFontPath + "): " + lOError.Message);

            return false;
        }

        return true;
    }

    /// <summary>The driver the instruments and the MT-32 version of the music sound on.</summary>
    private IUWMidiDriver fMidiDriver => (IUWMidiDriver)fRenderingDriver ?? mODriver;

    private void fReadSettings()
    {
        // The saved values reach the live settings here at the latest, even if no menu has
        // been open yet (UWSoundOptions).
        UWSoundOptions.EnsureLoaded();

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        float lfMusic = lOSettings == null ? 0.6f : (lOSettings.MusicEnabled ? lOSettings.MusicVolume : 0f);
        float lfEffects = lOSettings == null ? 0.8f : (lOSettings.SoundEnabled ? lOSettings.SoundVolume : 0f);

        mfMusicGain = Mathf.Clamp01(lfMusic);
        mfEffectGain = Mathf.Clamp01(lfEffects);
    }

    // ------------------------------------------------------------------
    // Game thread
    // ------------------------------------------------------------------

    /// <summary>Plays an XMI sequence from the start; whatever was playing stops. pbAdlibVersion:
    /// the sequence is an AdLib one (AW*.XMI) and plays on the chip whatever the device; else it
    /// is the MT-32 version and plays on the music device.</summary>
    public void PlayMusic(UWXmi.Sequence pOSequence, bool pbLoop, bool pbAdlibVersion = true)
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            mODriver.AllNotesOff();
            mODriver.Reset();

            if (mOGmDriver != null)
            {
                mOGmDriver.AllNotesOff();
                mOGmDriver.Reset();
            }

            if (mOMt32Driver != null)
                mOMt32Driver.Reset();

            mISequencerDriver = pbAdlibVersion ? mODriver : fMidiDriver;

            int liRate = mISequencerDriver == mODriver ? UWOpl2.SampleRate : miOutputRate;

            mOSequencer = pOSequence != null ? new UWXmiSequencer(pOSequence, liRate) { Loop = pbLoop } : null;
        }
    }

    public void StopMusic()
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            mOSequencer = null;
            mODriver.AllNotesOff();

            if (mOGmDriver != null)
                mOGmDriver.AllNotesOff();

            if (mOMt32Driver != null)
                mOMt32Driver.AllNotesOff();
        }
    }

    /// <summary>
    /// Starts a sound effect from SOUNDS.DAT. piVelocityOffset is the offset to the
    /// entry's velocity, as supplied by the distance calculation (see
    /// UWSoundEffects). If all nine voices are busy, the sound is dropped - the original
    /// does it the same way.
    /// </summary>
    public void PlayEffect(UWSound.Effect pOEffect, int piVelocityOffset)
    {
        if (!mbReady || mfEffectGain <= 0f)
            return;

        UWAdlibBank.TvfxPatch lOPatch = mOBank.GetEffect(pOEffect.Patch);

        if (lOPatch == null)
            return;

        byte lyScale = UWTvfxVoice.ComputeVolumeScale(pOEffect.Velocity, piVelocityOffset);

        lock (mOLock)
        {
            foreach (UWTvfxVoice lOVoice in mOVoices)
            {
                if (lOVoice.Phase != UWTvfxVoice.PhaseEnum.Idle)
                    continue;

                lOVoice.StartKeyOn(lOPatch, pOEffect.LifetimeTicks, lyScale);
                return;
            }
        }
    }

    /// <summary>
    /// Starts an effect and HOLDS it: the voice is not released until StopHeldEffect, and it
    /// gets no lifetime, so it only ends through its own program. That is how the original
    /// keeps the water sound going while one is in the water.
    ///
    /// Returns the voice, or -1 when none was free. The caller keeps the number and asks
    /// IsHeldEffectPlaying whether the program has ended in the meantime - the original
    /// starts it afresh as well.
    /// </summary>
    public int StartHeldEffect(UWSound.Effect pOEffect, int piVelocityOffset)
    {
        if (!mbReady || mfEffectGain <= 0f)
            return -1;

        UWAdlibBank.TvfxPatch lOPatch = mOBank.GetEffect(pOEffect.Patch);

        if (lOPatch == null)
            return -1;

        byte lyScale = UWTvfxVoice.ComputeVolumeScale(pOEffect.Velocity, piVelocityOffset);

        lock (mOLock)
        {
            for (int liAt = 0; liAt < mOVoices.Length; liAt++)
            {
                if (mOVoices[liAt].Phase != UWTvfxVoice.PhaseEnum.Idle)
                    continue;

                mOVoices[liAt].StartKeyOn(lOPatch, -1, lyScale);

                return liAt;
            }
        }

        return -1;
    }

    /// <summary>Whether a held effect is still sounding - see StartHeldEffect.</summary>
    public bool IsHeldEffectPlaying(int piVoice)
    {
        if (piVoice < 0 || piVoice >= mOVoices.Length)
            return false;

        lock (mOLock)
        {
            return mOVoices[piVoice].Phase != UWTvfxVoice.PhaseEnum.Idle;
        }
    }

    /// <summary>Ends a held effect - see StartHeldEffect.</summary>
    public void StopHeldEffect(int piVoice)
    {
        if (piVoice < 0 || piVoice >= mOVoices.Length)
            return;

        lock (mOLock)
        {
            mOVoices[piVoice].Release();
        }
    }

    /// <summary>MIDI channel for instruments - free alongside the music. UW.EXE obtains
    /// a free channel from the driver for this (PlayMusicalInstrument_seg014_1195).</summary>
    private const int InstrumentChannel = 14;

    /// <summary>
    /// Prepares the instrument channel like UW.EXE: timbre, all controllers reset,
    /// volume and expression full, centre pan. Sounds through the music chip and
    /// thus at the music volume - as in the original, which uses the same driver.
    /// </summary>
    public void BeginInstrument(int piProgram)
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            fMidiDriver.Handle(0xB0 | InstrumentChannel, 0x72, 0);
            fMidiDriver.Handle(0xC0 | InstrumentChannel, piProgram & 0x7F, 0);
            fMidiDriver.Handle(0xB0 | InstrumentChannel, 0x79, 0);
            fMidiDriver.Handle(0xB0 | InstrumentChannel, 7, 0x7F);
            fMidiDriver.Handle(0xB0 | InstrumentChannel, 11, 0x7F);
            fMidiDriver.Handle(0xB0 | InstrumentChannel, 10, 0x40);
        }
    }

    /// <summary>Strikes a note (velocity 127, like UW.EXE).</summary>
    public void PlayInstrumentNote(int piNote)
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            fMidiDriver.Handle(0x90 | InstrumentChannel, piNote & 0x7F, 0x7F);
        }
    }

    public void StopInstrumentNote(int piNote)
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            fMidiDriver.Handle(0x80 | InstrumentChannel, piNote & 0x7F, 0);
        }
    }

    /// <summary>The instrument is put down. On the MT-32 the part it borrowed goes back to the
    /// music (UWMt32MusicDriver.ReleaseLockedChannel); the other drivers have a channel of
    /// their own for it.</summary>
    public void EndInstrument()
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            if (mOMt32Driver != null)
                mOMt32Driver.ReleaseLockedChannel();
        }
    }

    // ------------------------------------------------------------------
    // Audio thread
    // ------------------------------------------------------------------

    private void OnAudioFilterRead(float[] pfData, int piChannels)
    {
        if (!mbReady || piChannels <= 0)
            return;

        float lfMusic = mfMusicGain;
        float lfEffects = mfEffectGain;

        lock (mOLock)
        {
            int liFrames = pfData.Length / piChannels;

            for (int liFrame = 0; liFrame < liFrames; liFrame++)
            {
                while (mfSourcePosition >= 1.0)
                {
                    mfPrevious = mfCurrent;
                    mfCurrent = fNextSourceSample(lfMusic, lfEffects);
                    mfSourcePosition -= 1.0;
                }

                float lfValue = mfPrevious + ((mfCurrent - mfPrevious) * (float)mfSourcePosition);
                mfSourcePosition += mfRatio;

                int liBase = liFrame * piChannels;

                for (int liChannel = 0; liChannel < piChannels; liChannel++)
                    pfData[liBase + liChannel] += lfValue;
            }

            IUWRenderingMidiDriver lISynth = fRenderingDriver;

            if (lISynth != null)
                fMixMidi(lISynth, pfData, piChannels, liFrames,
                    lfMusic * (MusicDevice == MusicDeviceEnum.Mt32 ? Mt32Gain : GeneralMidiGain));
        }
    }

    /// <summary>The General MIDI or MT-32 music, stereo at the output rate: each block of
    /// MidiBlockFrames first advances the sequencer, then renders.</summary>
    private void fMixMidi(IUWRenderingMidiDriver pISynth, float[] pfData, int piChannels, int piFrames, float pfGain)
    {
        for (int liFrame = 0; liFrame < piFrames; liFrame++)
        {
            if (miMidiBlockAt >= MidiBlockFrames)
            {
                if (mOSequencer != null && mISequencerDriver == pISynth)
                    mOSequencer.Advance(MidiBlockFrames, pISynth);

                pISynth.Render(mfMidiLeft, mfMidiRight, MidiBlockFrames);
                miMidiBlockAt = 0;
            }

            float lfLeft = mfMidiLeft[miMidiBlockAt] * pfGain;
            float lfRight = mfMidiRight[miMidiBlockAt] * pfGain;
            int liBase = liFrame * piChannels;

            miMidiBlockAt++;

            if (piChannels == 1)
            {
                pfData[liBase] += (lfLeft + lfRight) * 0.5f;
            }
            else
            {
                pfData[liBase] += lfLeft;
                pfData[liBase + 1] += lfRight;
            }
        }
    }

    /// <summary>One sample at 49716 Hz: advance the sequencer and effect ticks, run both
    /// chips, mix.</summary>
    private float fNextSourceSample(float pfMusic, float pfEffects)
    {
        if (mOSequencer != null && mISequencerDriver == mODriver)
            mOSequencer.Advance(1, mODriver);

        mfTickAccumulator += TicksPerSecond / UWOpl2.SampleRate;

        if (mfTickAccumulator >= 1.0)
        {
            mfTickAccumulator -= 1.0;

            foreach (UWTvfxVoice lOVoice in mOVoices)
            {
                if (lOVoice.Phase == UWTvfxVoice.PhaseEnum.Idle)
                    continue;

                lOVoice.ServiceTick();
                lOVoice.EmitRegisters(mOEffectChip);
            }
        }

        int liMusic = mOMusicChip.GenerateSample();
        int liEffects = mOEffectChip.GenerateSample();

        return ((liMusic * pfMusic) + (liEffects * pfEffects)) / 32768f;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        mbReady = false;

        lock (mOLock)
        {
            if (mOMt32Driver != null)
            {
                mOMt32Driver.Dispose();
                mOMt32Driver = null;
            }
        }
    }
}
