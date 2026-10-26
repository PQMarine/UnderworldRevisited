using System.Collections;
using System.IO;
using UnityEngine;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// Plays the intro: picture track, speech and subtitles, driven by the
/// original script CS000.N00 - and every other cutscene, the logos and the victory screens.
///
/// THE SCRIPT ITSELF is interpreted by UWCutsceneRunner (engine-free since cleanup step 8.7,
/// 2026-10-24; until then fRun and its helpers stood here): the segment model, the frame
/// numbers, the holds, the fades, the lines. This component drives it once per frame with the
/// unscaled delta and carries out what it asks for - the picture, the speech, the subtitle.
///
/// SPLIT THE SAME DAY into four: this component keeps the public surface, the Inspector values,
/// Start/Update/LateUpdate and their order, the input lock and every pointer visibility write;
/// UWCutsceneView draws (full screen, the view window, the leather frame), UWSplashSequence
/// holds the opening (logos, title, the intro when it is due) and UWVictoryScreens the two
/// pictures after the end sequence.
/// </summary>
public class UWIntroPlayer : MonoBehaviour
{
    [Header("Intro")]
    [SerializeField]
    [Tooltip("Play automatically when the scene starts.")]
    private bool mbPlayOnStart;

    [Header("Splash screens")]
    [SerializeField]
    [Tooltip("Display time per logo in seconds. Not from the data - in the original it depends on a busy-wait loop and thus on CPU speed.")]
    private float mfSplashSeconds = 4f;

    [SerializeField]
    [Tooltip("Black pause between the logos in seconds.")]
    private float mfSplashGapSeconds = 0.6f;

    [SerializeField]
    [Tooltip("How long the animated title stays before it ends by itself, in seconds. Ours, not the original's: its control file would run about twelve seconds (per user, 2026-09-22: four, like a logo).")]
    private float mfTitleSeconds = 4f;

    [Header("Intro")]
    [SerializeField]
    [Tooltip("Frame rate if an animation file does not specify its own.")]
    private int miFallbackFrameRate = 6;

    [SerializeField]
    [Tooltip("Seconds per character for lines without a speech recording.")]
    private float mfSecondsPerCharacter = 0.06f;

    [SerializeField]
    [Tooltip("Minimum duration of a line without a speech recording, in seconds.")]
    private float mfMinimumLineDuration = 1.5f;

    [SerializeField]
    [Tooltip("How long the subtitle stays up after the recording ends, in seconds.")]
    private float mfSubtitleHold = 0.3f;

    [SerializeField]
    [Tooltip("Height of the subtitle above the bottom screen edge, in original pixels (320x200).")]
    private float mfSubtitleBottomOffset = 8f;

    /// <summary>
    /// Which cutscene is running. Zero is the intro; Garamon's dreams are 24
    /// to 33 (see UWSleep).
    ///
    /// THE FILE NAMES ARE OCTAL, not decimal: 24 becomes CS030, 33 becomes CS041. The
    /// conversion is done by UWCutscene.FileName (fBuildFileName until 2026-10-24), which
    /// already existed for the open-file commands in the script. Checked in the data
    /// (2026-09-16): CS030 to CS037, CS040 and CS041 are all there, so the two dream stages the
    /// random roll can reach beyond CS037 do exist - an earlier note that only eight dream files
    /// exist was wrong.
    /// </summary>
    private int miCutscene;

    private DataImport mOData;

    private UWSoundPlayer mOSoundPlayer;

    private UWCutsceneScript mOScript;

    private Coroutine mOPlayback;

    /// <summary>The canvas and the pictures (cleanup step 8.7).</summary>
    private readonly UWCutsceneView mOView;

    /// <summary>The logos, the title, and when the opening is due (cleanup step 8.7).</summary>
    private readonly UWSplashSequence mOSplash;

    /// <summary>WIN1 and WIN2 after the end sequence (cleanup step 8.7).</summary>
    private readonly UWVictoryScreens mOVictory;

    /// <summary>The interpreter of the running cutscene, null while none runs (the logos and
    /// the victory screens have none).</summary>
    private UWCutsceneRunner mORunner;

    /// <summary>The runner's tuning, refreshed from the Inspector fields before every step.
    /// </summary>
    private readonly UWCutsceneRunner.Timing mOTiming = new UWCutsceneRunner.Timing();

    public UWIntroPlayer()
    {
        mOView = new UWCutsceneView(this);
        mOSplash = new UWSplashSequence(this, mOView);
        mOVictory = new UWVictoryScreens(this, mOView);
    }

    /// <summary>The control script of the running sequence.</summary>
    private string fGetScriptFile()
    {
        return UWCutsceneRunner.GetScriptFile(miCutscene);
    }

    /// <summary>What a sequence starts with before the first open-file comes (see
    /// UWCutsceneRunner.GetFirstAnimationFile).</summary>
    private string fGetFirstAnimationFile()
    {
        return UWCutsceneRunner.GetFirstAnimationFile(miCutscene);
    }

    /// <summary>
    /// Plays any cutscene - for the dreams while sleeping.
    ///
    /// The script is discarded so that the intro's script does not stay loaded.
    /// </summary>
    public void PlayCutscene(int piCutscene)
    {
        Stop();

        // Cutscenes 1 to 3 (the end sequence is 1) bring their own music, theme 4 - see
        // UWCutsceneRunner.GetMusicTheme.
        int liTheme = UWCutsceneRunner.GetMusicTheme(piCutscene);

        if (liTheme >= 0)
            UWAudioService.ChangeTheme(liTheme);

        if (miCutscene != piCutscene)
        {
            miCutscene = piCutscene;
            mOScript = null;
        }

        Play();
    }

    /// <summary>As above; pOOnFinished runs when the sequence ends or is cancelled with Escape
    /// - also immediately if it does not start at all. For the end sequence,
    /// after which the victory screens come.</summary>
    public void PlayCutscene(int piCutscene, System.Action pOOnFinished)
    {
        PlayCutscene(piCutscene);

        if (IsPlaying)
            mOOnFinished = pOOnFinished;
        else if (pOOnFinished != null)
            pOOnFinished();
    }

    /// <summary>What runs after the regular end. Stop alone - e.g. when another sequence
    /// starts - discards it.</summary>
    private System.Action mOOnFinished;

    /// <summary>Ends the playback and then invokes the callback.</summary>
    private void fFinish()
    {
        System.Action lOOnFinished = mOOnFinished;

        miFinishFrame = Time.frameCount;

        Stop();

        if (lOOnFinished != null)
            lOOnFinished();
    }

    /// <summary>Are the victory screens showing? Then continue only by key or click.
    /// </summary>
    private bool mbShowingVictory;

    /// <summary>
    /// The two victory screens after UW.EXE EndGameFunctions_ovr143_1186: WIN1.BYT until a
    /// key press or click, then WIN2.BYT with the character's stats, again until a
    /// key press. Both with palette 7 (see UWTextures).
    /// </summary>
    public void PlayVictoryScreens(System.Action pOOnFinished)
    {
        Stop();

        if (!fEnsureData())
        {
            if (pOOnFinished != null)
                pOOnFinished();

            return;
        }

        mOView.EnsureCanvas();
        fSetInputLock(true);
        UWPointer.IsShown = false;

        mbShowingVictory = true;
        mOOnFinished = pOOnFinished;
        mOPlayback = StartCoroutine(mOVictory.Run());
    }

    public bool IsPlaying => mOPlayback != null;

    /// <summary>The frame a sequence last ended in (fFinish).</summary>
    private int miFinishFrame = -1;

    /// <summary>
    /// Whether the game's keys must stay silent: a sequence plays, or one ended in this very
    /// frame - the Escape that cancelled a dream also opened the modern game menu, as the game's
    /// key routing (UWGameUI) ran after the cancel in the same frame (per user, 2026-10-13).
    /// </summary>
    public bool HoldsKeys => IsPlaying || miFinishFrame == Time.frameCount;

#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
    private float mfNextLinuxCursorHide;
#endif

    private void Start()
    {
        // What the opening owes at this start - after character creation, loading, the
        // switch in the bar: UWSplashSequence.OnStart.
        mOSplash.OnStart();
    }

    /// <summary>Is an opening still owed that has not started yet? The main menu asks, so
    /// that it does not put itself in front of it (UWMainMenu.fAutoShow).</summary>
    public bool IsOpeningPending
    {
        get { return mOSplash.IsOpeningPending; }
    }

    /// <summary>
    /// The two company logos before the game.
    ///
    /// Order and pictures come from the original: first PRES1.BYT ("origin
    /// presents"), then PRES2.BYT ("a blue sky prod. game"), both with palette 5,
    /// then the animated title. The DISPLAY TIME, however, is stored nowhere - in the
    /// original a busy-wait loop counts there, which depends on CPU speed.
    /// In DOSBox with high cycles the pictures are therefore gone before you recognise them
    /// (per user, 2026-09-01). Two seconds per logo are chosen, not measured.
    ///
    /// THE TITLE IS CUTSCENE 9, built 2026-09-22 - the file name is octal, so CS011, and
    /// it is the lettering "Ultima Underworld - the stygian abyss" over the stone, ten
    /// frames at five per second. Its control file fades in at frame 1, repeats the segment
    /// from frame 7 seven times at frame 8 and ends at frame 9; our player already handles
    /// all three commands. Until then we went from the second logo straight to the intro.
    /// </summary>
    public void PlaySplashScreens()
    {
        Stop();

        if (!fPrepare())
            return;

        // As for every sequence (see Play): no other input, and the cursor is off - set
        // AFTER the lock, whose setter restores the cursor itself (per user, 2026-09-22: "hide
        // the mouse cursor as with the animated title"). The click that started it does not
        // count.
        fSetInputLock(true);
        UWPointer.IsShown = false;
        mbAdvanceRequested = false;
        miPlayStartFrame = Time.frameCount;

        mOPlayback = StartCoroutine(mOSplash.Run());

        // Escape during a logo ends it the same way the title ends - fFinish calls this.
        mOOnFinished = mOSplash.AfterSplashScreens;
    }

    /// <summary>Whether a key or a pad button was pressed this frame - the logos and the
    /// victory screens move on by it.</summary>
    internal static bool WasSkipPressed()
    {
        // Any gamepad button skips as well (UWGamepad).
        return (UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.anyKey.wasPressedThisFrame)
            || UWGamepad.AnyPressed(false);
    }

    /// <summary>The click that advances a running playback and Esc that ends it. The debug keys
    /// that started the intro and the splash screens (F3, F4) are gone since 2026-09-26 (per
    /// user: not needed any more).</summary>
    private void Update()
    {
#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
        // LINUX: the pointer stayed visible over the logos (per user, 2026-10-09, on real
        // hardware after the same in the VM): hidden before the window is fully up, the player
        // loses it, and setting the same value again does nothing. So while a sequence plays it
        // is shown and hidden again within one frame, twice a second; Windows never needed it
        // (UWPointer.ReassertHidden since cleanup step 7.7B, where the Cursor is written).
        if (IsPlaying && Time.unscaledTime >= mfNextLinuxCursorHide)
        {
            mfNextLinuxCursorHide = Time.unscaledTime + 0.5f;
            UWPointer.ReassertHidden();
        }
#endif

        // The title's time, then the opening or the intro once the data is there - in this
        // order, where it stood before the split.
        mOSplash.Update();

        if (IsPlaying)
            mOView.HideSubtitleWhenDue();

        // THE CLICK IS COLLECTED HERE, not in the coroutine. wasPressedThisFrame
        // only holds for one frame, and the wait steps of the sequence do not necessarily
        // run in that same frame - that is how the click got lost (per user, 2026-09-07).
        if (IsPlaying)
        {
            if (UWPointer.HasMouse
                && (UWPointer.LeftPressed || UWPointer.RightPressed)
                && Time.frameCount != miPlayStartFrame)
            {
                // A cutscene's click goes to its interpreter; the logos' and the victory
                // screens' to mbAdvanceRequested.
                if (mORunner != null)
                    mORunner.RequestAdvance();
                else
                    mbAdvanceRequested = true;
            }
        }

        // The victory screens take every key themselves (UWVictoryScreens.fWaitForKeyOrClick).
        if (UnityEngine.InputSystem.Keyboard.current == null || mbShowingVictory)
            return;

        // ESC CANCELS IMMEDIATELY, as in the original (reference: uimanager_mainmenu calls
        // cutsplayer.StopCutscene on Escape, whether in the menu or in the game). What is meant
        // to come afterwards - the victory screens for the end sequence -, still comes.
        if (IsPlaying && UWBack.Takes(UWBackOwner.Cutscene))
        {
            fFinish();

            return;
        }
    }

    public void Play()
    {
        Stop();

        // The click that started the sequence must not skip it: the right click that confirms
        // the repair question starts the anvil sequence, and the same frame's click then tore
        // through all of it, which has no text line to clear the flag (per user, 2026-09-17:
        // "far too fast").
        mbAdvanceRequested = false;
        miPlayStartFrame = Time.frameCount;

        // Before fPrepare: it opens the first animation file, which already needs the mode for
        // its viewport crop.
        mOView.InViewport = UWCutsceneRunner.PlaysInViewport(miCutscene);

        if (!fPrepare())
            return;

        mOView.PlacePicture(mOView.InViewport);

        // DURING A SEQUENCE ALL OTHER INPUT IS OFF. The conversation uses the same
        // switch: it stops look, movement and interaction (see
        // UWControlScheme.IsUIModalOpen and the checks in UWPlayerLook, UWPlayerMovement
        // and Interaction). Without it the character would keep walking during a dream, and the
        // click to advance would have struck on the side as well.
        fSetInputLock(true);

        // The cursor is off meanwhile - there is nothing to click. Set AFTER the lock:
        // its setter restores the cursor state itself.
        UWPointer.IsShown = false;

        fSyncTiming();
        mORunner = new UWCutsceneRunner(miCutscene, mOScript, mOView.Animation, mOData.Strings, new CutsceneHost(this), mOTiming);
        mOPlayback = StartCoroutine(fRunCutscene());
    }

    /// <summary>
    /// The cutscene's clock: the interpreter runs up to its first wait at once, as the coroutine
    /// it replaced did when it started, then once per frame after Update (where the click and
    /// Escape are collected) with the unscaled delta - the same moments the old coroutine's
    /// waits were asked at.
    /// </summary>
    private IEnumerator fRunCutscene()
    {
        bool lbRunning = mORunner.Start();

        while (lbRunning)
        {
            yield return null;

            fSyncTiming();
            lbRunning = mORunner.Tick(Time.unscaledDeltaTime);
        }

        fFinish();
    }

    /// <summary>The Inspector values the interpreter reads, live as before.</summary>
    private void fSyncTiming()
    {
        mOTiming.FallbackFrameRate = miFallbackFrameRate;
        mOTiming.SecondsPerCharacter = mfSecondsPerCharacter;
        mOTiming.MinimumLineDuration = mfMinimumLineDuration;
    }

    /// <summary>What the interpreter asks for, carried out on the picture track, the speech
    /// player and the subtitle.</summary>
    private sealed class CutsceneHost : UWCutsceneRunner.IHost
    {
        private readonly UWIntroPlayer mOPlayer;

        public CutsceneHost(UWIntroPlayer pOPlayer)
        {
            mOPlayer = pOPlayer;
        }

        public UWCutscene OpenAnimation(string psFileName)
        {
            return mOPlayer.mOView.OpenAnimation(psFileName);
        }

        public void ShowFrame(int piFrame)
        {
            mOPlayer.mOView.ShowFrame(piFrame);
        }

        public void SetBrightness(float pfBrightness)
        {
            mOPlayer.mOView.SetBrightness(pfBrightness);
        }

        public float StartSpeech(int piVoice)
        {
            // File names are two digits with a leading zero.
            string lsClipName = piVoice.ToString("00");
            AudioClip lOClip = mOPlayer.mOSoundPlayer.GetClip(lsClipName);

            if (lOClip == null)
            {
                Debug.LogWarning("UWIntroPlayer: recording '" + lsClipName + "' missing, line plays silent.");

                return -1f;
            }

            // ONE RECORDING AT A TIME (per user, 2026-10-24: clicking on through the intro laid the
            // recordings over each other). The player plays them as one-shots, so a click that
            // starts the next line before the last one ended let both run; the original has one
            // channel for its digitised speech, and the next line replaces the last.
            mOPlayer.mOSoundPlayer.StopSpeech();
            mOPlayer.mOSoundPlayer.Play(lsClipName);

            return lOClip.length;
        }

        public void ShowLine(int piStringIndex, string psSubtitle, int piColourIndex, float pfSeconds)
        {
            // The subtitle disappears with the end of the recording, not only when the
            // next line replaces it. Before, it stayed up across the cut and
            // then sat under the next picture - which looks as if the wrong
            // text belonged to it (noticed by user, 2026-08-29).
            mOPlayer.mOView.ShowLine(psSubtitle, piColourIndex, Time.unscaledTime + pfSeconds + mOPlayer.mfSubtitleHold);
        }

        // The "Chime" sound in the intro (reference: cutsplayer, SoundEffectKlang).
        public void PlayChime()
        {
            UWAudioService.PlayEffect(UWSoundEffectId.Chime);
        }
    }

    /// <summary>Frame in which Play was called - its click does not count, see Play.</summary>
    private int miPlayStartFrame = -1;

    public void Stop()
    {
        mOOnFinished = null;
        mbShowingVictory = false;
        mOSplash.OnStop();

        if (mOPlayback != null)
        {
            StopCoroutine(mOPlayback);
            mOPlayback = null;
        }

        mORunner = null;

        mOView.ShowSubtitle(null, Color.white);

        // A spoken line stops with the sequence - otherwise it kept playing after Escape
        // (per user, 2026-09-15).
        if (mOSoundPlayer != null)
            mOSoundPlayer.StopSpeech();

        mOView.HidePicture();

        fSetInputLock(false);

        UWPointer.IsShown = true;
    }

    /// <summary>Stops all input that does not belong to the sequence.</summary>
    private void fSetInputLock(bool pbLocked)
    {
        if (mOControlScheme == null)
            mOControlScheme = UWScene.ControlScheme;

        if (mOControlScheme != null)
            mOControlScheme.SetUiModal("cutscene", pbLocked);
    }

    private UWControlScheme mOControlScheme;

    /// <summary>A click during the logos or the victory screens (both buttons count); a
    /// cutscene's click goes to its interpreter instead (UWCutsceneRunner.RequestAdvance).
    /// </summary>
    private bool mbAdvanceRequested;

    private bool fEnsureData()
    {
        if (mOData == null)
        {
            UWLevelLoader lOLoader = UWScene.LevelLoader;
            mOData = lOLoader == null ? null : lOLoader.UWDataImporter;
        }

        if (mOData == null)
            Debug.LogWarning("UWIntroPlayer: no game data found.");

        return mOData != null;
    }

    private bool fPrepare()
    {
        if (!fEnsureData())
            return false;

        if (mOSoundPlayer == null)
        {
            mOSoundPlayer = GetComponent<UWSoundPlayer>();

            if (mOSoundPlayer == null)
                mOSoundPlayer = gameObject.AddComponent<UWSoundPlayer>();
        }

        if (mOScript == null)
        {
            mOScript = new UWCutsceneScript(Path.Combine(fGetCutsPath(), fGetScriptFile()));

            if (!mOScript.IsLoaded)
            {
                Debug.LogWarning("UWIntroPlayer: " + fGetScriptFile() + " not found.");
                return false;
            }
        }

        mOView.EnsureCanvas();
        mOView.OpenAnimation(fGetFirstAnimationFile());

        return true;
    }

    private string fGetCutsPath()
    {
        string lsTrimmed = mOData.DataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return Path.Combine(Path.GetDirectoryName(lsTrimmed), "CUTS");
    }

    /// <summary>The black backdrop and the leather frame follow the picture - see
    /// UWCutsceneView.LateUpdate. After the coroutines and before drawing.</summary>
    private void LateUpdate()
    {
        mOView.LateUpdate();
    }

    // ------------------------------------------------- for the parts (cleanup step 8.7)

    /// <summary>The game data, null until a sequence first needed it.</summary>
    internal DataImport Data => mOData;

    /// <summary>The CUTS folder next to DATA.</summary>
    internal string CutsPath => fGetCutsPath();

    internal float SubtitleBottomOffset => mfSubtitleBottomOffset;

    internal float SplashSeconds => mfSplashSeconds;

    internal float SplashGapSeconds => mfSplashGapSeconds;

    internal float TitleSeconds => mfTitleSeconds;

    internal bool PlayOnStart => mbPlayOnStart;

    /// <summary>See mbAdvanceRequested.</summary>
    internal bool AdvanceRequested
    {
        get { return mbAdvanceRequested; }
        set { mbAdvanceRequested = value; }
    }

    /// <summary>The regular end of a sequence: Stop, then what was to follow (fFinish).</summary>
    internal void Finish()
    {
        fFinish();
    }

    /// <summary>The logos are done and the title is about to start in their place: forget the
    /// logos' coroutine without stopping it, so the title's Stop does not end the routine that
    /// starts it.</summary>
    internal void ReleasePlayback()
    {
        mOPlayback = null;
    }

    [ContextMenu("Play splash screens")]
    private void fPlaySplashFromMenu()
    {
        PlaySplashScreens();
    }

    [ContextMenu("Play intro")]
    private void fPlayFromMenu()
    {
        Play();
    }
}
