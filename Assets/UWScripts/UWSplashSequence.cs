using System.Collections;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE OPENING (cleanup step 8.7, 2026-10-24: out of UWIntroPlayer): the company logos, the
/// animated title and the intro - when they are due at startup and after character creation,
/// and the logos themselves. Owned by UWIntroPlayer, whose Start and Update call OnStart and
/// Update at the places the code stood before, so the startup order is the one it was (review
/// F 5.4); the input lock and the pointer stay with UWIntroPlayer. Moved as it was.
/// </summary>
public sealed class UWSplashSequence
{
    /// <summary>The animated title, CS011 - the file name is octal.</summary>
    private const int TitleCutscene = 9;

    private readonly UWIntroPlayer mOPlayer;

    private readonly UWCutsceneView mOView;

    /// <summary>The intro is due as soon as the level loader has its data.</summary>
    private bool mbPlayWhenDataReady;

    /// <summary>The same for the opening at startup - logos and title.</summary>
    private bool mbSplashWhenDataReady;

    /// <summary>Is the animated title of the opening running, and when does it end by itself?
    /// See UWIntroPlayer.mfTitleSeconds.</summary>
    private bool mbTitleRunning;

    private float mfTitleEndsAt;

    /// <summary>Is the running opening the one at program start? Only then the main menu
    /// follows it, and only then AfterSplashScreens puts the black cover up for the step
    /// between the two - played at any other time it goes back to where it was instead.</summary>
    private bool mbOpeningAtStartup;

    public UWSplashSequence(UWIntroPlayer pOPlayer, UWCutsceneView pOView)
    {
        mOPlayer = pOPlayer;
        mOView = pOView;
    }

    /// <summary>Is an opening still owed that has not started yet? The main menu asks, so
    /// that it does not put itself in front of it (UWMainMenu.fAutoShow).</summary>
    public bool IsOpeningPending
    {
        get { return mbSplashWhenDataReady || mbPlayWhenDataReady; }
    }

    /// <summary>UWIntroPlayer.Start: what the opening owes at this start.</summary>
    public void OnStart()
    {
        // AFTER CHARACTER CREATION the intro comes, as in the original - and nothing
        // else, no logos. The scene has just restarted; the game data may
        // not be there yet, hence only in the next frame (see Update).
        if (UWCharacterCreationScreen.TakeIntroRequest())
        {
            mbPlayWhenDataReady = true;
            return;
        }

        // NOT AFTER LOADING. Loading goes through a scene restart (see
        // UWSavegameSlots), and everything starts up again - including the company logos
        // and the intro. Whoever picks a savegame in the game does not want to see them.
        if (UWSavegameSlots.IsRestoring)
            return;

        // NOT AFTER CHARACTER CREATION EITHER (seen on the Linux VM, 2026-10-06, with the
        // opening still switched on): the new game restarts the scene the same way, and the
        // logos and the title came up over the running game, the move cursor drawn on top. The
        // flag survives the restart (UWCharacterCreationScreen.IsStartingNewGame).
        if (UWCharacterCreationScreen.IsStartingNewGame)
            return;

        // THE WHOLE OPENING CAN BE SWITCHED OFF (per user, 2026-09-22): logos, title and
        // intro, straight to the main menu. The switch is in the Game menu of the bar
        // (UWSetupMenu) and lives in UWUserSettings.
        if (!UWUserSettings.ShowOpeningSequence)
            return;

        // THE WHOLE OPENING RUNS HERE, logos and title. Until 2026-09-22 a serialized
        // switch stood in front of it which defaulted to OFF ("they get in the way during
        // testing") - and since this component is created at runtime by Interaction, that
        // default was the only value there ever was, so at startup nothing of it ran and
        // only a debug key (removed 2026-09-26) showed it. Switching it off is now the player's
        // business, one line up.
        //
        // NOT RIGHT HERE, THOUGH (per user, 2026-09-22: "I do not see the splash screens at
        // startup, although the menu says they are on"): at this moment the level loader
        // usually has no data yet, fPrepare fails and the whole opening was swallowed with
        // a warning in the log. So the same as for the intro after character creation - the
        // wish is noted and Update starts it as soon as the data is there.
        mbSplashWhenDataReady = true;
    }

    /// <summary>UWIntroPlayer.Update, before anything else of it but the Linux pointer: the
    /// title's time, and the opening or the intro once the data is there.</summary>
    public void Update()
    {
        // The title's time is up: the regular end, so the main menu follows as after the last
        // frame.
        if (mbTitleRunning && mOPlayer.IsPlaying && Time.unscaledTime >= mfTitleEndsAt)
        {
            mbTitleRunning = false;
            mOPlayer.Finish();
        }

        if (mbSplashWhenDataReady && !mOPlayer.IsPlaying && fIsDataReady())
        {
            mbSplashWhenDataReady = false;
            mbOpeningAtStartup = true;
            mOPlayer.PlaySplashScreens();
        }

        if (mbPlayWhenDataReady && !mOPlayer.IsPlaying && fIsDataReady())
        {
            mbPlayWhenDataReady = false;
            mOPlayer.Play();

            // The black cover of character creation can go: the intro starts black.
            // Even if it does not start - then straight into the game.
            UWScreenUi.HideLoadingCover();
        }
    }

    /// <summary>UWIntroPlayer.Stop: the title's own end is off.</summary>
    public void OnStop()
    {
        mbTitleRunning = false;
    }

    private bool fIsDataReady()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        return lOLoader != null && lOLoader.UWDataImporter != null;
    }

    /// <summary>The logos, then the title - see UWIntroPlayer.PlaySplashScreens, which starts
    /// it.</summary>
    public IEnumerator Run()
    {
        yield return fShowSplash(UWTexture.TextureTypes.PRES1);
        yield return fShowSplash(UWTexture.TextureTypes.PRES2);

        mOPlayer.ReleasePlayback();

        // Should a logo have failed to draw, the cover of the start would still be up and the
        // title would play invisibly beneath it. The title fades in from black anyway.
        UWScreenUi.HideLoadingCover();

        // The animated title between the logos and whatever follows - see the class comment
        // of PlaySplashScreens. When it has run it carries on by itself.
        mOPlayer.PlayCutscene(TitleCutscene, AfterSplashScreens);

        // ... and after mfTitleSeconds at the latest, which Update watches.
        if (mOPlayer.IsPlaying)
        {
            mbTitleRunning = true;
            mfTitleEndsAt = Time.unscaledTime + Mathf.Max(0f, mOPlayer.TitleSeconds);
        }
    }

    public void AfterSplashScreens()
    {
        bool lbAtStartup = mbOpeningAtStartup;

        mbOpeningAtStartup = false;

        if (mOPlayer.PlayOnStart)
        {
            mOPlayer.Play();
            return;
        }

        // THE GAME MUST NOT FLASH UP between the title and the main menu (per user,
        // 2026-09-22). The menu only appears in its own Update, a frame later - so black goes up
        // right here, in the same frame the picture goes, and UWMainMenu.Show takes it down.
        if (lbAtStartup && UWMainMenu.WillAutoShow)
            UWScreenUi.ShowLoadingCover();
    }

    /// <summary>Show a logo, then the black pause. A key press skips
    /// the current picture - company logos you cannot click away are an
    /// imposition, and the original lets you cancel them too.</summary>
    private IEnumerator fShowSplash(UWTexture.TextureTypes peType)
    {
        // Wait one frame before checking for a key press: otherwise exactly the
        // key that started the playback counts immediately as a skip and the logo would be
        // gone again in the same frame.
        yield return null;

        if (!mOView.DrawFullScreenBitmap(peType))
            yield break;

        // The picture track is tinted with mfBrightness, which the interpreter (UWCutsceneRunner,
        // fRun until 2026-10-24) sets to 0 when the script fades in from black. Without the
        // reset the logos would come out black (per user, 2026-09-01).
        mOView.SetBrightness(1f);

        mOView.ShowPicture();

        // The black cover of the start (UWLevelLoader.Start) steps aside the moment the logo
        // stands - in the same frame, so the game never shows in between.
        UWScreenUi.HideLoadingCover();

        float lfUntil = Time.unscaledTime + Mathf.Max(0f, mOPlayer.SplashSeconds);
        bool lbSkipped = false;

        // A KEY OR A CLICK moves on to the next picture (per user, 2026-09-22: "a click should
        // advance"). The click is collected in Update, see mbAdvanceRequested.
        while (Time.unscaledTime < lfUntil)
        {
            if (UWIntroPlayer.WasSkipPressed() || mOPlayer.AdvanceRequested)
            {
                mOPlayer.AdvanceRequested = false;
                lbSkipped = true;

                break;
            }

            yield return null;
        }

        // The pause stays black instead of revealing the game: the picture is
        // cleared and stays visible.
        mOView.FillBlack();

        // Whoever clicked the logo away wants the next one, not a black pause first.
        if (lbSkipped)
            yield break;

        lfUntil = Time.unscaledTime + Mathf.Max(0f, mOPlayer.SplashGapSeconds);

        while (Time.unscaledTime < lfUntil)
        {
            if (UWIntroPlayer.WasSkipPressed() || mOPlayer.AdvanceRequested)
            {
                mOPlayer.AdvanceRequested = false;

                break;
            }

            yield return null;
        }
    }
}
