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
/// </summary>
public class UWAudioEngine : MonoBehaviour
{
    public static UWAudioEngine Instance { get; private set; }

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

        UWMusic.Refresh(mOInteraction != null && mOInteraction.IsCombatModeActive);
    }

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

    /// <summary>Plays an XMI sequence from the start; whatever was playing stops.</summary>
    public void PlayMusic(UWXmi.Sequence pOSequence, bool pbLoop)
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            mODriver.AllNotesOff();
            mODriver.Reset();
            mOSequencer = pOSequence != null ? new UWXmiSequencer(pOSequence, UWOpl2.SampleRate) { Loop = pbLoop } : null;
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
            mODriver.Handle(0xB0 | InstrumentChannel, 0x72, 0);
            mODriver.Handle(0xC0 | InstrumentChannel, piProgram & 0x7F, 0);
            mODriver.Handle(0xB0 | InstrumentChannel, 0x79, 0);
            mODriver.Handle(0xB0 | InstrumentChannel, 7, 0x7F);
            mODriver.Handle(0xB0 | InstrumentChannel, 11, 0x7F);
            mODriver.Handle(0xB0 | InstrumentChannel, 10, 0x40);
        }
    }

    /// <summary>Strikes a note (velocity 127, like UW.EXE).</summary>
    public void PlayInstrumentNote(int piNote)
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            mODriver.Handle(0x90 | InstrumentChannel, piNote & 0x7F, 0x7F);
        }
    }

    public void StopInstrumentNote(int piNote)
    {
        if (!mbReady)
            return;

        lock (mOLock)
        {
            mODriver.Handle(0x80 | InstrumentChannel, piNote & 0x7F, 0);
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
        }
    }

    /// <summary>One sample at 49716 Hz: advance the sequencer and effect ticks, run both
    /// chips, mix.</summary>
    private float fNextSourceSample(float pfMusic, float pfEffects)
    {
        if (mOSequencer != null)
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
    }
}
