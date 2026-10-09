using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Plays the intro: picture track, speech and subtitles, driven by the
/// original script CS000.N00.
///
/// The script is a command list in which each entry carries a FRAME NUMBER - i.e. the
/// moment at which it is triggered. So the timing comes from the picture track (6 or 10
/// frames per second, noted in the header of each animation file), not from seconds. On
/// every open-file the count starts over, because a new file is then running.
///
/// SEGMENT MODEL (see fRun): the script is cut into segments at each frame-set, and the
/// frame number of the frame-set is the LENGTH of the segment. The segment's frame counter
/// drives both picture and speech; a line does not wait for the previous recording to end
/// (the older speech-driven model was removed on 2026-09-07).
///
/// If the picture track is missing - say because the CUTS folder is incomplete -, sound and text
/// still run, then timed by the same frame counter at the fallback frame rate.
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
    /// conversion is done by fBuildFileName, which already existed for the open-file commands
    /// in the script. Checked in the data (2026-09-16): CS030 to CS037, CS040 and CS041
    /// are all there, so the two dream stages the random roll can reach beyond CS037 do
    /// exist - an earlier note that only eight dream files exist was wrong.
    /// </summary>
    private int miCutscene;

    /// <summary>The string block of the subtitles is at 0x0C00 plus the sequence number.
    /// </summary>
    private int fGetSpeechBlock()
    {
        return UWSound.SpeechStringBlock + miCutscene;
    }

    /// <summary>The control script of the running sequence.</summary>
    private string fGetScriptFile()
    {
        return fBuildFileName(miCutscene, 0);
    }

    /// <summary>What a sequence starts with before the first open-file comes: according to the
    /// file list in uw-formats.txt the .N01 is the black screen.</summary>
    private string fGetFirstAnimationFile()
    {
        return fBuildFileName(miCutscene, 1);
    }

    /// <summary>
    /// Plays any cutscene - for the dreams while sleeping.
    ///
    /// The script is discarded so that the intro's script does not stay loaded.
    /// </summary>
    public void PlayCutscene(int piCutscene)
    {
        Stop();

        // Cutscenes 1 to 3 (the end sequence is 1) bring their own music: UW.EXE
        // PlayCutscene_ovr105_1402 calls LoadXMIFILE(4, 1) for them, theme 4 (AW04.XMI).
        // LoadXMIFILE keeps a piece that is already playing, and so does UWMusic.
        if (piCutscene >= FirstMusicCutscene && piCutscene <= LastMusicCutscene)
            UWMusic.ChangeTheme(CutsceneMusicTheme);

        if (miCutscene != piCutscene)
        {
            miCutscene = piCutscene;
            mOScript = null;
        }

        Play();
    }

    /// <summary>Cutscenes with their own music, and the theme they load - see PlayCutscene.
    /// </summary>
    private const int FirstMusicCutscene = 1;

    private const int LastMusicCutscene = 3;

    private const int CutsceneMusicTheme = 4;

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

        fEnsureCanvas();
        fSetInputLock(true);
        Cursor.visible = false;

        mbShowingVictory = true;
        mOOnFinished = pOOnFinished;
        mOPlayback = StartCoroutine(fRunVictoryScreens());
    }

    private IEnumerator fRunVictoryScreens()
    {
        mfBrightness = 1f;
        fApplyBrightness();

        if (fDrawFullScreenBitmap(UWTexture.TextureTypes.WIN1))
        {
            mOPictureImage.enabled = true;
            yield return fWaitForKeyOrClick();
        }

        if (fDrawFullScreenBitmap(UWTexture.TextureTypes.WIN2))
        {
            fDrawVictoryStats();
            mOPictureImage.enabled = true;
            yield return fWaitForKeyOrClick();
        }

        fFinish();
    }

    /// <summary>Waits for a key or a click. First wait one frame so that the
    /// click that led here does not count right away.</summary>
    private IEnumerator fWaitForKeyOrClick()
    {
        yield return null;

        mbAdvanceRequested = false;

        while (!mbAdvanceRequested && !fWasSkipPressed())
            yield return null;

        mbAdvanceRequested = false;
    }

    /// <summary>Height of the first line on WIN2, in pixels from the bottom (UW.EXE: di = 0xB4).
    /// </summary>
    private const int VictoryFirstLineY = 0xB4;

    /// <summary>Horizontal centre around which the four header lines are placed.</summary>
    private const int VictoryCentreX = 0xA0;

    /// <summary>Left column of the six attributes, right column, offset to the value.</summary>
    private const int VictoryAttributeLeftX = 0x50;

    private const int VictoryAttributeRightX = 0xBE;

    private const int VictoryAttributeValueOffset = 0x2D;

    /// <summary>The skills: three columns from 50 spaced 74 apart, value right-aligned at
    /// column plus 70.</summary>
    private const int VictorySkillFirstX = 0x32;

    private const int VictorySkillColumnWidth = 0x4A;

    private const int VictorySkillValueRight = 0x46;

    /// <summary>Font colour 0x5C (UW.EXE FontColour), with the palette of the pictures.</summary>
    private const int VictoryFontColour = 0x5C;

    private const int VictoryPalette = 7;

    /// <summary>
    /// The stats on WIN2 - line by line after UW.EXE VictoryScreen_ovr143_76F, font
    /// fontchar.sys:
    ///   Name                                                  centred
    ///   "A level " level " " class                            centred
    ///   "Banished the Slasher of Veils"                       centred
    ///   "after " days " days in the Abyss"                    centred
    ///   Str/Dex/Int left at 80, Vit/Mana/Exp right at 190, value 45 beside it, Exp divided by 10
    ///   (one line lower than the header lines)
    ///   the twenty skills in three columns, directly below the attributes
    /// Days = clock / 0x1C2000 / 12. In the original the lines count from the bottom; whether the height
    /// means the foot or the head of a line is not stated there. First built as foot - then
    /// the name sat a little too high (per user, 2026-09-14). Now as head, everything one
    /// font height lower (assumption, not exactly visible in the original because of DOSBox).
    /// </summary>
    private void fDrawVictoryStats()
    {
        UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.CharacterGeneration);
        UWCharacter lOCharacter = UWScene.Character;
        UWPlayerData lOPlayer = mOData.InitialPlayer;

        if (lOFont == null || !lOFont.IsLoaded || lOCharacter == null)
            return;

        Color32 lOColour = fGetPaletteColour(VictoryPalette, VictoryFontColour);
        int liHeight = lOFont.Height;
        int liY = VictoryFirstLineY;

        fDrawCentred(lOFont, lOPlayer != null ? lOPlayer.Name : string.Empty, liY, lOColour);

        liY -= liHeight;
        fDrawCentred(lOFont, fGetString(1, 0xBB) + lOCharacter.Level + " "
            + fGetString(2, 0x17 + (lOPlayer != null ? lOPlayer.CharacterClass : 0)), liY, lOColour);

        liY -= liHeight;
        fDrawCentred(lOFont, fGetString(1, 0xBC), liY, lOColour);

        int liDays = (int)((uint)lOCharacter.ClockValue / 0x1C2000u) / 12;

        liY -= liHeight;
        fDrawCentred(lOFont, fGetString(1, 0xBD) + liDays + fGetString(1, 0xBE), liY, lOColour);

        liY -= liHeight;

        int[] liValues =
        {
            lOCharacter.Strength,
            lOCharacter.Dexterity,
            lOCharacter.Intelligence,
            Mathf.RoundToInt(lOCharacter.MaxHP),
            Mathf.RoundToInt(lOCharacter.MaxMana),
            lOCharacter.Experience / 10
        };

        for (int liAt = 0; liAt < liValues.Length; liAt++)
        {
            int liX = liAt / 3 != 0 ? VictoryAttributeRightX : VictoryAttributeLeftX;
            int liRowY = liY - ((liAt % 3) * liHeight);

            fDrawText(lOFont, fGetString(2, 0x11 + liAt), liX, liRowY, lOColour);
            fDrawText(lOFont, liValues[liAt].ToString(), liX + VictoryAttributeValueOffset, liRowY, lOColour);
        }

        liY -= 2 * liHeight;

        for (int liAt = 0; liAt < UWCharacter.SkillNumberCount; liAt++)
        {
            if (liAt % 3 == 0)
                liY -= liHeight;

            int liX = ((liAt % 3) * VictorySkillColumnWidth) + VictorySkillFirstX;
            string lsValue = lOCharacter.GetSkillByNumber(liAt).ToString();

            fDrawText(lOFont, fGetString(2, 0x1F + liAt), liX, liY, lOColour);
            fDrawText(lOFont, lsValue,
                liX + VictorySkillValueRight - lOFont.MeasureText(lsValue, 0, UWFontRenderer.CharacterSpacing), liY, lOColour);
        }

        mOPictureTexture.SetPixels32(mOPictureBuffer);
        mOPictureTexture.Apply(false, false);
    }

    /// <summary>A string by game numbering - our export is one higher.</summary>
    private string fGetString(int piBlock, int piIndex)
    {
        try
        {
            return mOData.Strings.Blocks[piBlock].Strings[piIndex + 1].TrimEnd('\n');
        }
        catch
        {
            return string.Empty;
        }
    }

    private Color32 fGetPaletteColour(int piPalette, int piIndex)
    {
        try
        {
            byte[] lyRgb = mOData.Palettes.GetPalette(piPalette).GetRGB((byte)piIndex);

            return new Color32(lyRgb[0], lyRgb[1], lyRgb[2], 255);
        }
        catch
        {
            return new Color32(255, 255, 255, 255);
        }
    }

    private void fDrawCentred(UWFont pOFont, string psText, int piY, Color32 pOColour)
    {
        int liWidth = pOFont.MeasureText(psText, 0, UWFontRenderer.CharacterSpacing);

        fDrawText(pOFont, psText, VictoryCentreX - (liWidth / 2), piY, pOColour);
    }

    /// <summary>Writes text into the picture track; piX/piY is the top-left corner, counted from
    /// the bottom as in the original.</summary>
    private void fDrawText(UWFont pOFont, string psText, int piX, int piY, Color32 pOColour)
    {
        if (string.IsNullOrEmpty(psText) || mOPictureBuffer == null)
            return;

        int liCursor = piX;

        for (int liAt = 0; liAt < psText.Length; liAt++)
        {
            char lcChar = psText[liAt];

            if (lcChar == ' ')
            {
                liCursor += pOFont.SpaceWidth + UWFontRenderer.CharacterSpacing;
                continue;
            }

            if (!pOFont.TryGetGlyph(lcChar, out UWFont.Glyph lOGlyph))
                continue;

            for (int liRow = 0; liRow < pOFont.Height; liRow++)
            {
                int liTargetY = piY - liRow;

                if (liTargetY < 0 || liTargetY >= ScreenHeight)
                    continue;

                for (int liColumn = 0; liColumn < lOGlyph.Width; liColumn++)
                {
                    int liTargetX = liCursor + liColumn;

                    if (lOGlyph.Pixels[(liRow * lOGlyph.Width) + liColumn] == 0 || liTargetX < 0 || liTargetX >= ScreenWidth)
                        continue;

                    mOPictureBuffer[(liTargetY * ScreenWidth) + liTargetX] = pOColour;
                }
            }

            liCursor += lOGlyph.Width + UWFontRenderer.CharacterSpacing;
        }
    }

    /// <summary>Line width of cutscene text: UW.EXE CutsceneDisplayString_ovr105_3B6 breaks a
    /// line once it would exceed 0x140 (320) pixels, at most six lines.</summary>
    private const int SubtitleWidth = 320;

    private const int ScreenWidth = 320;

    private const int ScreenHeight = 200;

    private DataImport mOData;

    private UWSoundPlayer mOSoundPlayer;

    private UWCutsceneScript mOScript;

    private Canvas mOCanvas;

    private RawImage mOPictureImage;

    /// <summary>The centred 320x200 frame a FULL-SCREEN picture sits in, kept at the pixel
    /// proportion of UWSettings.DisplayAs4By3 by UWPixelAspectFrame - the same frame the game,
    /// the conversation and the menus use. Until 2026-09-22 a full-screen picture was stretched
    /// over the whole window instead, so on a wide screen the logos, the title and the intro
    /// came out neither 4:3 nor 16:10 (per user: "take the 4:3 fix into account").</summary>
    private RectTransform mOFullScreenFrame;

    /// <summary>Black behind a full-screen picture, so the bars beside the frame do not show
    /// the game. Follows the picture, see LateUpdate.</summary>
    private Image mOBackdrop;

    private RawImage mOSubtitleImage;

    private Texture2D mOPictureTexture;

    private Color32[] mOPictureBuffer;

    private UWCutscene mOAnimation;

    /// <summary>Last displayed frame number of the running file.</summary>
    private int miCurrentFrame;

    /// <summary>Brightness of the picture track, 0 dark to 1 bright - for the fades.</summary>
    private float mfBrightness = 1f;

    private Coroutine mOPlayback;

    /// <summary>Did the running line start a speech recording? Then its subtitle is not
    /// drawn, and command 0x0E waits for the recording to end (see fExecute).</summary>
    private bool mbSpeechPlaying;

    /// <summary>Time from which the subtitle is hidden.</summary>
    private float mfSubtitleUntil;

    /// <summary>Time at which the running recording ends - reference point for 0x0E.</summary>
    private float mfSpeechEndsAt;

    public bool IsPlaying => mOPlayback != null;

#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
    private float mfNextLinuxCursorHide;
#endif

    private void Start()
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

    /// <summary>The intro is due as soon as the level loader has its data.</summary>
    private bool mbPlayWhenDataReady;

    /// <summary>The same for the opening at startup - logos and title.</summary>
    private bool mbSplashWhenDataReady;

    /// <summary>Is the animated title of the opening running, and when does it end by itself?
    /// See mfTitleSeconds.</summary>
    private bool mbTitleRunning;

    private float mfTitleEndsAt;

    /// <summary>Is the running opening the one at program start? Only then the main menu
    /// follows it, and only then fAfterSplashScreens puts the black cover up for the step
    /// between the two - played at any other time it goes back to where it was instead.</summary>
    private bool mbOpeningAtStartup;

    /// <summary>Is an opening still owed that has not started yet? The main menu asks, so
    /// that it does not put itself in front of it (UWMainMenu.fAutoShow).</summary>
    public bool IsOpeningPending
    {
        get { return mbSplashWhenDataReady || mbPlayWhenDataReady; }
    }

    private bool fIsDataReady()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        return lOLoader != null && lOLoader.UWDataImporter != null;
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
        Cursor.visible = false;
        mbAdvanceRequested = false;
        miPlayStartFrame = Time.frameCount;

        mOPlayback = StartCoroutine(fRunSplashScreens());

        // Escape during a logo ends it the same way the title ends - fFinish calls this.
        mOOnFinished = fAfterSplashScreens;
    }

    private IEnumerator fRunSplashScreens()
    {
        yield return fShowSplash(UWTexture.TextureTypes.PRES1);
        yield return fShowSplash(UWTexture.TextureTypes.PRES2);

        mOPlayback = null;

        // Should a logo have failed to draw, the cover of the start would still be up and the
        // title would play invisibly beneath it. The title fades in from black anyway.
        UWScreenUi.HideLoadingCover();

        // The animated title between the logos and whatever follows - see the class comment
        // of PlaySplashScreens. When it has run it carries on by itself.
        PlayCutscene(TitleCutscene, fAfterSplashScreens);

        // ... and after mfTitleSeconds at the latest, which Update watches.
        if (IsPlaying)
        {
            mbTitleRunning = true;
            mfTitleEndsAt = Time.unscaledTime + Mathf.Max(0f, mfTitleSeconds);
        }
    }

    /// <summary>The animated title, CS011 - the file name is octal.</summary>
    private const int TitleCutscene = 9;

    private void fAfterSplashScreens()
    {
        bool lbAtStartup = mbOpeningAtStartup;

        mbOpeningAtStartup = false;

        if (mbPlayOnStart)
        {
            Play();
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

        if (!fDrawFullScreenBitmap(peType))
            yield break;

        // The picture track is tinted with mfBrightness, which fRun sets to 0 when the script
        // fades in from black. Without the reset the logos would come out black
        // (per user, 2026-09-01).
        mfBrightness = 1f;
        fApplyBrightness();

        mOPictureImage.enabled = true;

        // The black cover of the start (UWLevelLoader.Start) steps aside the moment the logo
        // stands - in the same frame, so the game never shows in between.
        UWScreenUi.HideLoadingCover();

        float lfUntil = Time.unscaledTime + Mathf.Max(0f, mfSplashSeconds);
        bool lbSkipped = false;

        // A KEY OR A CLICK moves on to the next picture (per user, 2026-09-22: "a click should
        // advance"). The click is collected in Update, see mbAdvanceRequested.
        while (Time.unscaledTime < lfUntil)
        {
            if (fWasSkipPressed() || mbAdvanceRequested)
            {
                mbAdvanceRequested = false;
                lbSkipped = true;

                break;
            }

            yield return null;
        }

        // The pause stays black instead of revealing the game: the picture is
        // cleared and stays visible.
        fFillBlack();

        // Whoever clicked the logo away wants the next one, not a black pause first.
        if (lbSkipped)
            yield break;

        lfUntil = Time.unscaledTime + Mathf.Max(0f, mfSplashGapSeconds);

        while (Time.unscaledTime < lfUntil)
        {
            if (fWasSkipPressed() || mbAdvanceRequested)
            {
                mbAdvanceRequested = false;

                break;
            }

            yield return null;
        }
    }

    /// <summary>Clears the picture track to black without hiding it.</summary>
    private void fFillBlack()
    {
        if (mOPictureBuffer == null || mOPictureTexture == null)
            return;

        for (int liPixel = 0; liPixel < mOPictureBuffer.Length; liPixel++)
            mOPictureBuffer[liPixel] = new Color32(0, 0, 0, 255);

        mOPictureTexture.SetPixels32(mOPictureBuffer);
        mOPictureTexture.Apply(false, false);
    }

    private static bool fWasSkipPressed()
    {
        // Any gamepad button skips as well (UWGamepad).
        return (UnityEngine.InputSystem.Keyboard.current != null
            && UnityEngine.InputSystem.Keyboard.current.anyKey.wasPressedThisFrame)
            || UWGamepad.AnyPressed(false);
    }

    /// <summary>
    /// Draws one of the full-screen pictures from the BYT files into the picture track. Row 0 of the
    /// source is at the top, Unity starts at the bottom - hence the flip.
    /// </summary>
    private bool fDrawFullScreenBitmap(UWTexture.TextureTypes peType)
    {
        fEnsureCanvas();
        fPlacePicture(false);

        if (mOData == null || mOPictureBuffer == null)
            return false;

        UWTexture lOSource;

        if (!mOData.Textures.BitmapFiles.TryGetValue(peType, out lOSource) || lOSource == null)
            return false;

        UWColor32[] lOColours = lOSource.GetUWColor32();

        if (lOColours == null)
            return false;

        for (int liPixel = 0; liPixel < mOPictureBuffer.Length; liPixel++)
            mOPictureBuffer[liPixel] = new Color32(0, 0, 0, 255);

        int liWidth = Mathf.Min(lOSource.Width, ScreenWidth);
        int liHeight = Mathf.Min(lOSource.Height, ScreenHeight);

        for (int liY = 0; liY < liHeight; liY++)
        {
            int liSourceRow = (lOSource.Height - 1 - liY) * lOSource.Width;
            int liTargetRow = liY * ScreenWidth;

            for (int liX = 0; liX < liWidth; liX++)
            {
                int liSource = liSourceRow + liX;

                if (liSource < 0 || liSource >= lOColours.Length)
                    continue;

                mOPictureBuffer[liTargetRow + liX] =
                    new Color32(lOColours[liSource].R, lOColours[liSource].G, lOColours[liSource].B, 255);
            }
        }

        mOPictureTexture.SetPixels32(mOPictureBuffer);
        mOPictureTexture.Apply(false, false);

        return true;
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
        // is shown and hidden again within one frame, twice a second; Windows never needed it.
        if (IsPlaying && Time.unscaledTime >= mfNextLinuxCursorHide)
        {
            mfNextLinuxCursorHide = Time.unscaledTime + 0.5f;
            Cursor.visible = true;
            Cursor.visible = false;
        }
#endif

        // The title's time is up: the regular end, so the main menu follows as after the last
        // frame.
        if (mbTitleRunning && IsPlaying && Time.unscaledTime >= mfTitleEndsAt)
        {
            mbTitleRunning = false;
            fFinish();
        }

        if (mbSplashWhenDataReady && !IsPlaying && fIsDataReady())
        {
            mbSplashWhenDataReady = false;
            mbOpeningAtStartup = true;
            PlaySplashScreens();
        }

        if (mbPlayWhenDataReady && !IsPlaying && fIsDataReady())
        {
            mbPlayWhenDataReady = false;
            Play();

            // The black cover of character creation can go: the intro starts black.
            // Even if it does not start - then straight into the game.
            UWScreenUi.HideLoadingCover();
        }

        if (IsPlaying && mOSubtitleImage != null && UWTextLabel.Get(mOSubtitleImage).IsShown && Time.unscaledTime >= mfSubtitleUntil)
            UWTextLabel.Hide(mOSubtitleImage);

        // THE CLICK IS COLLECTED HERE, not in the coroutine. wasPressedThisFrame
        // only holds for one frame, and the wait steps of the sequence do not necessarily
        // run in that same frame - that is how the click got lost (per user, 2026-09-07).
        if (IsPlaying)
        {
            UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

            if (lOMouse != null
                && (lOMouse.leftButton.wasPressedThisFrame || lOMouse.rightButton.wasPressedThisFrame)
                && Time.frameCount != miPlayStartFrame)
                mbAdvanceRequested = true;
        }

        // The victory screens take every key themselves (fWaitForKeyOrClick).
        if (UnityEngine.InputSystem.Keyboard.current == null || mbShowingVictory)
            return;

        // ESC CANCELS IMMEDIATELY, as in the original (reference: uimanager_mainmenu calls
        // cutsplayer.StopCutscene on Escape, whether in the menu or in the game). What is meant
        // to come afterwards - the victory screens for the end sequence -, still comes.
        if (IsPlaying && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
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
        mbInViewport = miCutscene >= FirstViewportCutscene;

        if (!fPrepare())
            return;

        fPlacePicture(mbInViewport);

        // DURING A SEQUENCE ALL OTHER INPUT IS OFF. The conversation uses the same
        // switch: it stops look, movement and interaction (see
        // UWControlScheme.IsUIModalOpen and the checks in UWPlayerLook, UWPlayerMovement
        // and Interaction). Without it the character would keep walking during a dream, and the
        // click to advance would have struck on the side as well.
        fSetInputLock(true);

        // The cursor is off meanwhile - there is nothing to click. Set AFTER the lock:
        // its setter restores the cursor state itself.
        Cursor.visible = false;

        mOPlayback = StartCoroutine(fRun());
    }

    /// <summary>Cutscenes from this number on play inside the 3D viewport, the game UI stays
    /// visible (reference cutsplayer: CutsceneNo >= 256 is not full screen, CutsSmall) - the
    /// death cutscenes 0x102 and 0x103 among them (per user, 2026-09-14).</summary>
    private const int FirstViewportCutscene = 0x100;

    /// <summary>Frame in which Play was called - its click does not count, see Play.</summary>
    private int miPlayStartFrame = -1;

    /// <summary>Does the running cutscene play inside the viewport? See fPlacePicture.</summary>
    private bool mbInViewport;

    /// <summary>
    /// Full screen or the 3D viewport.
    ///
    /// IN THE VIEWPORT the frames are handled like the window and gravestone pictures: their
    /// content sits somewhere on the 320x200 canvas (bottom right), so the painted block is
    /// cut out and placed where UWGameUI puts those pictures. Scaling the whole canvas into the
    /// viewport put the death cutscene too far down and right (per user, 2026-09-14). The block
    /// is the union of all frames of the animation file (see fPlaceViewportCrop).
    /// </summary>
    private void fPlacePicture(bool pbInViewport)
    {
        mbInViewport = pbInViewport;

        if (mOPictureImage == null)
            return;

        RectTransform lORect = (RectTransform)mOPictureImage.transform;

        // FULL SCREEN goes into the 320x200 frame and so follows the 4:3 switch; IN THE VIEWPORT
        // the picture hangs on the canvas itself, because fPlaceViewportCrop sets its anchors
        // in screen fractions.
        Transform lOParent = pbInViewport || mOFullScreenFrame == null
            ? mOCanvas.transform
            : mOFullScreenFrame;

        if (lORect.parent != lOParent)
        {
            lORect.SetParent(lOParent, false);

            // Stay beneath the subtitle.
            lORect.SetSiblingIndex(lOParent == mOCanvas.transform ? 2 : 0);
        }

        if (!pbInViewport)
        {
            lORect.anchorMin = Vector2.zero;
            lORect.anchorMax = Vector2.one;
            lORect.offsetMin = Vector2.zero;
            lORect.offsetMax = Vector2.zero;
            mOPictureImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }
    }

    /// <summary>Cuts the painted block of the given animation file out of the canvas and puts
    /// it at the window picture position in the viewport.</summary>
    private void fPlaceViewportCrop(string psPath)
    {
        if (!mbInViewport || mOPictureImage == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        UWGameUI lOUi = UWScene.GameUi;

        if (lOUi == null)
            return;

        RectInt lOBlock = fMeasurePaintedBlock(psPath);

        // ONE FIXED CANVAS OFFSET for all viewport cutscenes. Centring each file's own block
        // put the final death cutscene (CS403) 10 pixels too low - its painted area sits
        // elsewhere on the canvas than that of CS402. The offset is taken from CS402, whose
        // placement is confirmed (per user, 2026-09-14: "the sequence sits perfectly").
        Vector2 lOOffset = fGetViewportCanvasOffset(lOUi, psPath, lOBlock);
        Vector2 lOCorner = new Vector2(lOBlock.x, lOBlock.y) + lOOffset;

        // The final death cutscene sits 2 reference pixels LOWER than the CS402 offset gives.
        // UW.EXE PlayCutscene_ovr105_1402 passes the viewport rectangle with its top 9 rows higher
        // (row 10 instead of 19) and 11 rows taller (123 instead of 112) for 0x103; with the canvas
        // bottom-aligned into that rectangle, canvas row r lands on screen row r - 67 instead of
        // r - 69. Per user on 2026-09-14 first "2 pixels too low", then with -2 "4 pixels too
        // high" - the second measurement agrees with UW.EXE.
        if (Path.GetFileName(psPath).StartsWith(FinalDeathFilePrefix, System.StringComparison.OrdinalIgnoreCase))
            lOCorner.y += FinalDeathNudgeY;
        Rect lOScreen = lOUi.GetUiPictureScreenRect(lOCorner, new Vector2(lOBlock.width, lOBlock.height));

        // THE MODERN SCHEME hides the classic view window: the picture lies in the middle in a
        // leather frame at the UI's size, as the view's pictures do (UWModernHud, per user
        // 2026-10-04: the anvil's sequence was not in such a frame).
        mbModernFrame = fIsModern();

        if (mbModernFrame)
            lOScreen = fPlaceModernFrame(lOUi, lOBlock);

        RectTransform lORect = (RectTransform)mOPictureImage.transform;

        lORect.anchorMin = new Vector2(lOScreen.xMin / Screen.width, lOScreen.yMin / Screen.height);
        lORect.anchorMax = new Vector2(lOScreen.xMax / Screen.width, lOScreen.yMax / Screen.height);
        lORect.offsetMin = Vector2.zero;
        lORect.offsetMax = Vector2.zero;

        // The picture texture is built bottom-up (see fShowFrame).
        mOPictureImage.uvRect = new Rect((float)lOBlock.x / ScreenWidth,
            (float)(ScreenHeight - lOBlock.y - lOBlock.height) / ScreenHeight,
            (float)lOBlock.width / ScreenWidth, (float)lOBlock.height / ScreenHeight);
    }

    /// <summary>The modern scheme's frame around a viewport cutscene (fPlaceModernFrame).</summary>
    private RawImage mOModernFrame;

    private Texture2D mOModernFrameTexture;

    private bool mbModernFrame;

    private const int ModernFrameBorder = 3;

    private static bool fIsModern()
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;

        return lOScheme != null && lOScheme.Current == UWControlScheme.SchemeEnum.Modern;
    }

    /// <summary>The block's place in the middle of the screen at the modern UI's size, and the
    /// leather frame around it; returns the picture's screen rectangle.</summary>
    private Rect fPlaceModernFrame(UWGameUI pOUi, RectInt pOBlock)
    {
        float liScale = UWModernHud.PixelScale;
        float lfWidth = pOBlock.width * liScale;
        float lfHeight = pOBlock.height * liScale;
        Rect lOPicture = new Rect(Mathf.Round((Screen.width - lfWidth) * 0.5f), Mathf.Round((Screen.height * 0.55f) - (lfHeight * 0.5f)),
            lfWidth, lfHeight);

        int liFrameWidth = pOBlock.width + UWModernHudArt.LeatherLeft + UWModernHudArt.LeatherRight + (2 * ModernFrameBorder);
        int liFrameHeight = pOBlock.height + UWModernHudArt.LeatherTop + UWModernHudArt.LeatherBottom + (2 * ModernFrameBorder);

        if (mOModernFrame == null)
        {
            GameObject lOObject = new GameObject("Modern frame", typeof(RectTransform), typeof(RawImage));
            lOObject.transform.SetParent(mOCanvas.transform, false);
            mOModernFrame = lOObject.GetComponent<RawImage>();
            mOModernFrame.raycastTarget = false;
            UWPixelArtUI.Apply(mOModernFrame);
        }

        if (mOModernFrameTexture == null || mOModernFrameTexture.width != liFrameWidth || mOModernFrameTexture.height != liFrameHeight)
        {
            if (mOModernFrameTexture != null)
                Destroy(mOModernFrameTexture);

            mOModernFrameTexture = UWModernHudArt.BuildLeather(pOUi.mOUWData.Textures, liFrameWidth, liFrameHeight, pOUi.TextureFilterMode);
            mOModernFrame.texture = mOModernFrameTexture;
        }

        // Beneath the picture.
        mOModernFrame.transform.SetSiblingIndex(mOPictureImage.transform.GetSiblingIndex());

        Rect lOFrame = new Rect(lOPicture.x - ((UWModernHudArt.LeatherLeft + ModernFrameBorder) * liScale),
            lOPicture.y - ((UWModernHudArt.LeatherBottom + ModernFrameBorder) * liScale), liFrameWidth * liScale, liFrameHeight * liScale);
        RectTransform lORect = (RectTransform)mOModernFrame.transform;

        lORect.anchorMin = new Vector2(lOFrame.xMin / Screen.width, lOFrame.yMin / Screen.height);
        lORect.anchorMax = new Vector2(lOFrame.xMax / Screen.width, lOFrame.yMax / Screen.height);
        lORect.offsetMin = Vector2.zero;
        lORect.offsetMax = Vector2.zero;

        return lOPicture;
    }

    /// <summary>Files of the final death cutscene (0x103) and their vertical nudge in reference
    /// pixels, positive is down - see fPlaceViewportCrop.</summary>
    private const string FinalDeathFilePrefix = "CS403.";

    private const float FinalDeathNudgeY = 2f;

    /// <summary>The reference file for the canvas offset of viewport cutscenes (death with
    /// silver tree, placement confirmed).</summary>
    private const string ViewportReferenceFile = "CS402.N01";

    private static bool mbViewportOffsetKnown;

    private static Vector2 mOViewportOffset;

    /// <summary>UI position minus canvas position, derived once from the reference file placed
    /// like a window picture. Falls back to the given file itself if the reference is missing.
    /// </summary>
    private Vector2 fGetViewportCanvasOffset(UWGameUI pOUi, string psPath, RectInt pOBlock)
    {
        if (!mbViewportOffsetKnown)
        {
            string lsReference = Path.Combine(fGetCutsPath(), ViewportReferenceFile);
            RectInt lOReference = File.Exists(lsReference) ? fMeasurePaintedBlock(lsReference) : pOBlock;

            mOViewportOffset = pOUi.GetViewportPictureCorner(new Vector2(lOReference.width, lOReference.height))
                - new Vector2(lOReference.x, lOReference.y);
            mbViewportOffsetKnown = File.Exists(lsReference);
        }

        return mOViewportOffset;
    }

    /// <summary>Bounding box (top-down canvas pixels) of everything that differs from the
    /// top-left background colour in any frame of the file - the same background rule as
    /// UWGameUI.fMeasureWindowContent.</summary>
    private static RectInt fMeasurePaintedBlock(string psPath)
    {
        UWCutscene lOCutscene = new UWCutscene(psPath);
        int liMinX = lOCutscene.Width, liMinY = lOCutscene.Height, liMaxX = -1, liMaxY = -1;
        byte lyBackground = 0;

        for (int liFrame = 0; liFrame < lOCutscene.FrameCount; liFrame++)
        {
            byte[] lyPixels = lOCutscene.GetFrame(liFrame);

            if (liFrame == 0 && lyPixels.Length > 0)
                lyBackground = lyPixels[0];

            for (int liY = 0; liY < lOCutscene.Height; liY++)
            {
                for (int liX = 0; liX < lOCutscene.Width; liX++)
                {
                    if (lyPixels[(liY * lOCutscene.Width) + liX] == lyBackground)
                        continue;

                    if (liX < liMinX) liMinX = liX;
                    if (liX > liMaxX) liMaxX = liX;
                    if (liY < liMinY) liMinY = liY;
                    if (liY > liMaxY) liMaxY = liY;
                }
            }
        }

        if (liMaxX < liMinX || liMaxY < liMinY)
            return new RectInt(0, 0, lOCutscene.Width, lOCutscene.Height);

        return new RectInt(liMinX, liMinY, liMaxX - liMinX + 1, liMaxY - liMinY + 1);
    }

    public void Stop()
    {
        mOOnFinished = null;
        mbShowingVictory = false;
        mbTitleRunning = false;

        if (mOPlayback != null)
        {
            StopCoroutine(mOPlayback);
            mOPlayback = null;
        }

        fShowSubtitle(null, Color.white);

        // A spoken line stops with the sequence - otherwise it kept playing after Escape
        // (per user, 2026-09-15).
        if (mOSoundPlayer != null)
            mOSoundPlayer.StopSpeech();

        mbSpeechPlaying = false;

        if (mOPictureImage != null)
            mOPictureImage.enabled = false;

        fSetInputLock(false);

        Cursor.visible = true;
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

    /// <summary>
    /// Set as soon as a mouse click happened; cleared as soon as the next line starts.
    ///
    /// Both buttons count. It matters at the points where the sequence is WAITING
    /// for something - for the end of a spoken line or for a pause from the script. The
    /// rest of the sequence runs on as before; whoever wants to skip everything uses Escape.
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

        fEnsureCanvas();
        fOpenAnimation(fGetFirstAnimationFile());

        return true;
    }

    private string fGetCutsPath()
    {
        string lsTrimmed = mOData.DataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return Path.Combine(Path.GetDirectoryName(lsTrimmed), "CUTS");
    }

    private IEnumerator fRun()
    {
        // Start dark: the script's first command is a fade-in, which would otherwise
        // have no effect.
        // ONLY IF THE SCRIPT FADES IN AT ALL. The death cutscenes (CS402, CS403) have no fade-in
        // command - starting dark left them completely black (per user, 2026-09-14).
        mfBrightness = fScriptFadesIn() ? 0f : 1f;
        fApplyBrightness();
        mbSpeechPlaying = false;

        System.Collections.Generic.List<UWCutsceneScript.Entry> lOSegment =
            new System.Collections.Generic.List<UWCutsceneScript.Entry>();

        System.Collections.Generic.List<UWCutsceneScript.Entry> lOAfter =
            new System.Collections.Generic.List<UWCutsceneScript.Entry>();

        int liAt = 0;
        int liExtension = FirstAnimationExtension;
        bool lbFirstSegment = true;
        bool lbFileChanged = false;

        while (liAt < mOScript.Entries.Count)
        {
            // AFTER EACH SEGMENT the next animation file - unless the segment has
            // opened one itself. That is how the intro gets from CS000.N01 via N02 to N03,
            // without the script having to say so every time.
            if (!lbFirstSegment && !lbFileChanged)
            {
                liExtension++;
                fOpenAnimation(fBuildFileName(miCutscene, liExtension));
            }

            lbFirstSegment = false;
            lbFileChanged = false;

            lOSegment.Clear();
            lOAfter.Clear();

            int liLength = 0;
            bool lbHasLength = false;
            bool lbEnd = false;

            // The segment extends to the next frame-set. ITS FRAME NUMBER IS THE
            // LENGTH, not a point in time - see class comment.
            while (liAt < mOScript.Entries.Count)
            {
                UWCutsceneScript.Entry lOEntry = mOScript.Entries[liAt++];

                if (lOEntry.Command == UWCutsceneScript.Command.SetStaticFrame)
                {
                    liLength = lOEntry.Frame;
                    lbHasLength = true;

                    break;
                }

                if (lOEntry.Command == UWCutsceneScript.Command.EndCutscene)
                {
                    // An end command without a preceding frame-set sets the length
                    // itself - that is how the title screen keeps its palette animation going.
                    if (!lbHasLength && lOEntry.Frame > 0)
                    {
                        liLength = lOEntry.Frame;
                        lbHasLength = true;
                    }

                    lbEnd = true;

                    break;
                }

                lOSegment.Add(lOEntry);
            }

            // Commands with frame number 999 belong AFTER the segment.
            while (liAt < mOScript.Entries.Count
                && mOScript.Entries[liAt].Frame == AfterSegmentFrame)
                lOAfter.Add(mOScript.Entries[liAt++]);

            if (lbHasLength && liLength > 0)
            {
                float lfDelay = 1f / Mathf.Max(1, fGetFrameRate());

                // Does this segment speak or write? That decides what a click does - see
                // the frame loop below.
                bool lbHasLine = false;

                for (int liEntry = 0; liEntry < lOSegment.Count; liEntry++)
                {
                    if (lOSegment[liEntry].Command == UWCutsceneScript.Command.ShowText
                        || lOSegment[liEntry].Command == UWCutsceneScript.Command.TextAndPlay)
                        lbHasLine = true;
                }

                // Repeats left per repeat-segment entry of this segment (index -> count).
                Dictionary<int, int> lORepeats = new Dictionary<int, int>();

                // UP TO AND INCLUDING the length: on this number sits the open-file
                // that fetches the file for the next segment. In the script it is placed
                // directly before the frame-set and carries the same frame number.
                for (int liFrame = 0; liFrame <= liLength; liFrame++)
                {
                    bool lbRepeat = false;

                    for (int liEntry = 0; liEntry < lOSegment.Count; liEntry++)
                    {
                        if (lOSegment[liEntry].Frame != liFrame)
                            continue;

                        // REPEAT-SEGMENT (0x07): plays the segment from its start up to this
                        // frame again, argument times in total. The final death cutscene
                        // (CS403) repeats its end - the skulls' eyes flash several times in the
                        // original, once with us (per user, 2026-09-14). The reference leaves
                        // the command unimplemented; the exact semantics are ASSUMED.
                        if (lOSegment[liEntry].Command == UWCutsceneScript.Command.RepeatSegment)
                        {
                            if (!lORepeats.ContainsKey(liEntry))
                                lORepeats[liEntry] = Mathf.Max(0, lOSegment[liEntry].Arguments[0] - 1);

                            if (lORepeats[liEntry] > 0)
                            {
                                lORepeats[liEntry]--;
                                lbRepeat = true;
                                break;
                            }

                            continue;
                        }

                        if (lOSegment[liEntry].Command == UWCutsceneScript.Command.OpenFile)
                            lbFileChanged = true;

                        yield return fExecute(lOSegment[liEntry]);
                    }

                    if (lbRepeat)
                    {
                        miCurrentFrame = liFrame;
                        fShowFrame(miCurrentFrame);

                        liFrame = -1;

                        yield return new WaitForSecondsRealtime(lfDelay);
                        continue;
                    }

                    if (liFrame >= liLength)
                        break;

                    miCurrentFrame = liFrame;
                    fShowFrame(miCurrentFrame);

                    // A CLICK. In a segment WITH a line it skips the rest of it, and the
                    // next line clears the flag (fStartLine). In a segment WITHOUT one -
                    // the title, the anvil - nothing would ever clear it, so the frames
                    // would race past with no wait at all and the picture would be gone
                    // before it was seen (per user, 2026-09-22, about the title). There a
                    // click means what the viewer means by it: away with it.
                    if (mbAdvanceRequested)
                    {
                        if (!lbHasLine)
                        {
                            mbAdvanceRequested = false;

                            break;
                        }

                        continue;
                    }

                    yield return new WaitForSecondsRealtime(lfDelay);
                }
            }
            else
            {
                for (int liEntry = 0; liEntry < lOSegment.Count; liEntry++)
                {
                    if (lOSegment[liEntry].Command == UWCutsceneScript.Command.OpenFile)
                        lbFileChanged = true;

                    yield return fExecute(lOSegment[liEntry]);
                }
            }

            for (int liEntry = 0; liEntry < lOAfter.Count; liEntry++)
                yield return fExecute(lOAfter[liEntry]);

            if (lbEnd)
                break;
        }

        fFinish();
    }

    /// <summary>Does the running script contain a fade-in? See fRun.</summary>
    private bool fScriptFadesIn()
    {
        if (mOScript == null)
            return false;

        for (int liAt = 0; liAt < mOScript.Entries.Count; liAt++)
        {
            if (mOScript.Entries[liAt].Command == UWCutsceneScript.Command.FadeIn)
                return true;
        }

        return false;
    }

    /// <summary>The frame number with which a command belongs AFTER the segment.</summary>
    private const int AfterSegmentFrame = 999;

    /// <summary>The picture track starts with this extension: .N01.</summary>
    private const int FirstAnimationExtension = 1;

    /// <summary>
    /// A single script command.
    ///
    /// NOT INCLUDED: frame-set, which is the segment separator and is consumed in fRun,
    /// repeat-segment, which fRun also handles itself, and to-frame. The latter used to set
    /// the playing time here; in the segment model the segment length does that, and the
    /// reference has no case for to-frame at all.
    /// </summary>
    private IEnumerator fExecute(UWCutsceneScript.Entry pOEntry)
    {
        switch (pOEntry.Command)
        {
            case UWCutsceneScript.Command.OpenFile:
                fOpenAnimation(fBuildFileName(pOEntry.Arguments[0], pOEntry.Arguments[1]));
                break;

            case UWCutsceneScript.Command.FadeIn:
                yield return fFade(1f, pOEntry.Arguments[0]);
                break;

            case UWCutsceneScript.Command.FadeOut:
                yield return fFade(0f, pOEntry.Arguments[0]);
                break;

            case UWCutsceneScript.Command.TextAndPlay:
                fStartLine(pOEntry.Arguments[0], pOEntry.Arguments[1], pOEntry.Arguments[2]);
                break;

            case UWCutsceneScript.Command.ShowText:
                fStartLine(pOEntry.Arguments[0], pOEntry.Arguments[1], -1);
                break;

            // The "Chime" sound in the intro (reference: cutsplayer, SoundEffectKlang).
            case UWCutsceneScript.Command.Chime:
                UWSoundEffects.PlayAtAvatar(UWSoundEffects.Chime);
                break;

            // 0x0E HOLDS THE PICTURE at its frame. UW.EXE Cutscene_14_Wait_ovr105_5FA: with
            // speech output the hold lasts until the recording has ended (flag 0x80, cleared in
            // CutsceneDataProcessing_ovr105_869 once seg014_1DC5_1C78 reports the sound done) and
            // then argument 2 seconds more; without speech it lasts argument 1 seconds. The unit is
            // seconds: the loop compares (PIT timer - frame time) >> 8, and the timer counts 256 per
            // second (the frame delay is 0x100 / frame rate). In the intro the arguments are 3 1,
            // 5 1 and mostly 2 1.
            case UWCutsceneScript.Command.Unknown0E:
                if (mbSpeechPlaying)
                {
                    while (Time.unscaledTime < mfSpeechEndsAt && !mbAdvanceRequested)
                        yield return null;

                    yield return fWaitOrClick(pOEntry.Arguments[1]);
                }
                else
                    yield return fWaitOrClick(pOEntry.Arguments[0]);

                break;

            // Pause holds the picture for argument 1 SECONDS, the same hold as 0x0E
            // (UW.EXE Cutscene_3_Pause_ovr105_5CD). Until 2026-09-14 this was half seconds.
            case UWCutsceneScript.Command.Pause:
                yield return fWaitOrClick(pOEntry.Arguments[0]);
                break;
        }
    }

    // UNTIL 2026-09-07 THREE HELPER METHODS STOOD HERE: fWaitForPreviousLine, fPlaySegment
    // and fAdvanceTo. They belonged to a different playback model - there speech set
    // the timing, and the picture track ran alongside. The segment model in fRun no longer
    // needs them: the segment's frame counter drives both.

    /// <summary>The frame rate of the running file; it is in its header. Only if nothing
    /// usable is there does the fallback value from the Inspector apply.</summary>
    private int fGetFrameRate()
    {
        return mOAnimation != null && mOAnimation.FrameRate > 0
            ? mOAnimation.FrameRate
            : miFallbackFrameRate;
    }

    /// <summary>Waits, but can be cut short by a click.</summary>
    private IEnumerator fWaitOrClick(float pfSeconds)
    {
        float lfUntil = Time.unscaledTime + pfSeconds;

        while (Time.unscaledTime < lfUntil)
        {
            if (mbAdvanceRequested)
                yield break;

            yield return null;
        }
    }

    /// <summary>
    /// Fades the picture track to the target brightness. READ 2026-09-25 (the fade commands only
    /// store the rate; CutsceneDataProcessing hands it to the palette fades ovr114_2B9 in and
    /// ovr114_1B3 out): rate 0 switches at once; otherwise the palette runs through rate * 8
    /// equal steps, and each waits for at least 8 PIT ticks (256 a second) before it is set -
    /// so a fade takes rate / 4 seconds, in steps of 1/32 second, while the script stands still.
    /// Fading in shows 1/n to n/n of the palette, fading out (n-1)/n down to 0. Until then ours
    /// took 1 / rate seconds, smoothly - a rate-4 fade was four times too fast.
    /// </summary>
    private IEnumerator fFade(float pfTarget, int piRate)
    {
        if (piRate <= 0)
        {
            mfBrightness = pfTarget;
            fApplyBrightness();
            yield break;
        }

        int liSteps = piRate * FadeStepsPerRate;
        float lfStart = mfBrightness;
        float lfStepSeconds = FadeStepPitTicks / (float)UWDataImport.UWData.UWPlayerTick.PitTicksPerSecond;

        for (int liStep = 1; liStep <= liSteps; liStep++)
        {
            float lfWaited = 0f;

            while (lfWaited < lfStepSeconds)
            {
                yield return null;
                lfWaited += Time.unscaledDeltaTime;
            }

            mfBrightness = Mathf.Lerp(lfStart, pfTarget, liStep / (float)liSteps);
            fApplyBrightness();
        }
    }

    /// <summary>Palette steps per unit of fade rate (the rate is shifted left by three).</summary>
    private const int FadeStepsPerRate = 8;

    /// <summary>The PIT ticks each fade step waits at least.</summary>
    private const float FadeStepPitTicks = 8f;

    private void fApplyBrightness()
    {
        if (mOPictureImage != null)
            mOPictureImage.color = new Color(mfBrightness, mfBrightness, mfBrightness, 1f);
    }

    /// <summary>The numbers in the file name are OCTAL - see uw-formats.txt on the command
    /// open-file.</summary>
    private static string fBuildFileName(int piCutscene, int piPage)
    {
        return string.Format("CS{0}.N{1}",
            System.Convert.ToString(piCutscene, 8).PadLeft(3, '0'),
            System.Convert.ToString(piPage, 8).PadLeft(2, '0'));
    }

    private void fOpenAnimation(string psFileName)
    {
        try
        {
            string lsPath = Path.Combine(fGetCutsPath(), psFileName);

            if (!File.Exists(lsPath))
            {
                Debug.LogWarning("UWIntroPlayer: " + psFileName + " missing, picture track paused.");
                mOAnimation = null;
                return;
            }

            mOAnimation = new UWCutscene(lsPath);
            miCurrentFrame = -1;

            fPlaceViewportCrop(lsPath);

            fShowFrame(0);
        }
        catch (System.Exception lOError)
        {
            Debug.LogWarning("UWIntroPlayer: " + psFileName + " not readable - " + lOError.Message);
            mOAnimation = null;
        }
    }

    /// <summary>Shows a frame. If the number is outside the file, the last frame simply
    /// stays up - the counter keeps running anyway, because the script regularly
    /// counts past the end of the file and playback would otherwise get stuck.</summary>
    private void fShowFrame(int piFrame)
    {
        miCurrentFrame = piFrame;

        if (mOAnimation == null || mOPictureImage == null)
            return;

        if (piFrame < 0 || piFrame >= mOAnimation.FrameCount)
            return;

        byte[] lyPixels = mOAnimation.GetFrame(piFrame);

        for (int liY = 0; liY < mOAnimation.Height; liY++)
        {
            // The texture is built bottom-up, the picture top-down.
            int liSourceRow = liY * mOAnimation.Width;
            int liTargetRow = (mOAnimation.Height - 1 - liY) * mOAnimation.Width;

            for (int liX = 0; liX < mOAnimation.Width; liX++)
            {
                byte lyIndex = lyPixels[liSourceRow + liX];

                mOPictureBuffer[liTargetRow + liX] = new Color32(
                    mOAnimation.PaletteRgb[(lyIndex * 3) + 0],
                    mOAnimation.PaletteRgb[(lyIndex * 3) + 1],
                    mOAnimation.PaletteRgb[(lyIndex * 3) + 2],
                    255);
            }
        }

        mOPictureTexture.SetPixels32(mOPictureBuffer);
        mOPictureTexture.Apply();
        mOPictureImage.enabled = true;
    }

    /// <summary>Clears the click flag: a new line is running, from here only the
    /// NEXT click counts.
    ///
    /// THIS BELONGS IN fStartLine and not in the individual script branches. At first it was
    /// only in TextAndPlay - ShowText did not clear, the flag stayed set, and a
    /// single click then tore through the whole sequence (per user, 2026-09-07). Both
    /// commands start a line, so it applies to both.</summary>
    private void fStartLine(int piColour, int piStringIndex, int piVoiceNumber)
    {
        mbAdvanceRequested = false;

        string lsText = fGetLine(piStringIndex);

        mbSpeechPlaying = false;

        // Lines without a recording get a reading time based on text length, so they too are not
        // immediately overwritten by the next one.
        float lfDuration = Mathf.Max(mfMinimumLineDuration,
            (lsText == null ? 0 : lsText.Length) * mfSecondsPerCharacter);

        if (piVoiceNumber >= 0)
        {
            // File names are two digits with a leading zero.
            string lsClipName = piVoiceNumber.ToString("00");
            AudioClip lOClip = mOSoundPlayer.GetClip(lsClipName);

            if (lOClip == null)
            {
                Debug.LogWarning("UWIntroPlayer: recording '" + lsClipName + "' missing, line plays silent.");
            }
            else
            {
                mOSoundPlayer.Play(lsClipName);
                lfDuration = lOClip.length;
                mbSpeechPlaying = true;
            }
        }

        // NO SUBTITLE WHILE SPEECH PLAYS. UW.EXE Cutscene_13_TextAndSound_ovr105_719 only draws
        // the text when speech output is off; with speech it just queues the recording. In the
        // original intro no subtitles appear (per user, 2026-09-14). Show-text (0x00), as in the
        // end sequence, and a line whose recording is missing still show their text.
        fShowSubtitle(mbSpeechPlaying ? null : lsText, fGetColour(piColour));

        mfSpeechEndsAt = Time.unscaledTime + lfDuration;

        // The subtitle disappears with the end of the recording, not only when the
        // next line replaces it. Before, it stayed up across the cut and
        // then sat under the next picture - which looks as if the wrong
        // text belonged to it (noticed by user, 2026-08-29).
        mfSubtitleUntil = Time.unscaledTime + lfDuration + mfSubtitleHold;
    }

    private string fGetLine(int piStringIndex)
    {
        if (piStringIndex == UWCutsceneScript.NoText)
            return null;

        try
        {
            // The usual project +1: our parsed string blocks are one entry
            // above the game numbering (as with GetObjectDescription or the partner names
            // of the conversations). Without it the subtitle lags one line behind the
            // sound - checked by user in the original, 2026-08-29: with the sound "At last, you
            // are asleep." entry 0 was wrongly shown.
            //
            // Evidence in the data itself: entry 0 is a truncated fragment
            // ("s brought you starting..."), whose complete version is
            // entry 2. A clean block does not start mid-sentence.
            return UWFontRenderer.CleanText(
                mOData.Strings.Blocks[fGetSpeechBlock()].Strings[piStringIndex + 1]);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// The colour value from the script is a palette index - into the palette of the running
    /// animation file. Until 2026-09-14 palette 0 from PALS.DAT was used here: there 244 and
    /// 245 are almost black, and the lines of the Baron and his daughter in the end sequence stayed
    /// invisible (per user). In the files of CS000 and CS001, 240 is grey, 241 blue,
    /// 244 purple and 245 orange. Palette 0 remains the fallback when no file is open.
    /// </summary>
    private Color32 fGetColour(int piColourIndex)
    {
        if (mOAnimation != null && mOAnimation.PaletteRgb != null && piColourIndex >= 0 && piColourIndex < 256)
            return new Color32(mOAnimation.PaletteRgb[piColourIndex * 3], mOAnimation.PaletteRgb[(piColourIndex * 3) + 1],
                mOAnimation.PaletteRgb[(piColourIndex * 3) + 2], 255);

        try
        {
            byte[] lyRgb = mOData.Palettes.GetPalette(0).GetRGB((byte)piColourIndex);

            if (lyRgb[0] == 0 && lyRgb[1] == 0 && lyRgb[2] == 0)
                return new Color32(255, 255, 255, 255);

            return new Color32(lyRgb[0], lyRgb[1], lyRgb[2], 255);
        }
        catch
        {
            return new Color32(255, 255, 255, 255);
        }
    }

    private void fShowSubtitle(string psText, Color32 pOColour)
    {
        if (mOSubtitleImage == null)
            return;

        if (string.IsNullOrEmpty(psText))
        {
            UWTextLabel.Hide(mOSubtitleImage);
            return;
        }

        // FONTBIG.SYS: UW.EXE PlayCutscene_ovr105_1402 opens it before every cutscene and
        // switches back to font5x6p.sys afterwards. The end sequence text was too small with the
        // normal font (per user, 2026-09-14).
        UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.Big);
        List<string> lOLines = UWFontRenderer.WrapText(lOFont, psText, SubtitleWidth);
        Texture2D lOTexture = UWFontRenderer.RenderLines(lOFont, lOLines, SubtitleWidth, pOColour, FilterMode.Point);

        if (lOTexture == null)
        {
            UWTextLabel.Hide(mOSubtitleImage);
            return;
        }

        if (mOSubtitleImage.texture != null)
            Destroy(mOSubtitleImage.texture);

        UWTextLabel.Get(mOSubtitleImage).Set(lOFont, lOTexture, lOLines, new List<Color32> { pOColour }, true);
    }

    private void fEnsureCanvas()
    {
        if (mOCanvas != null)
            return;

        GameObject lOCanvasObject = new GameObject("UW Intro Canvas", typeof(Canvas), typeof(CanvasScaler));
        lOCanvasObject.transform.SetParent(transform, false);

        mOCanvas = lOCanvasObject.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mOCanvas.sortingOrder = 100;

        CanvasScaler lOScaler = lOCanvasObject.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        lOScaler.referenceResolution = new Vector2(ScreenWidth, ScreenHeight);
        lOScaler.matchWidthOrHeight = 1f;

        // Fitted whole into the window, bars over and under it when narrow (UWUiFit).
        lOCanvasObject.AddComponent<UWFitCanvas>();

        // The black backdrop first, then the frame for full-screen pictures - see mOFullScreenFrame.
        GameObject lOBackdropObject = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        lOBackdropObject.transform.SetParent(lOCanvasObject.transform, false);

        RectTransform lOBackdropRect = (RectTransform)lOBackdropObject.transform;
        lOBackdropRect.anchorMin = Vector2.zero;
        lOBackdropRect.anchorMax = Vector2.one;
        lOBackdropRect.offsetMin = Vector2.zero;
        lOBackdropRect.offsetMax = Vector2.zero;

        mOBackdrop = lOBackdropObject.GetComponent<Image>();
        mOBackdrop.color = Color.black;
        mOBackdrop.raycastTarget = false;
        mOBackdrop.enabled = false;

        GameObject lOFrameObject = new GameObject("Full screen frame", typeof(RectTransform));
        lOFrameObject.transform.SetParent(lOCanvasObject.transform, false);

        mOFullScreenFrame = (RectTransform)lOFrameObject.transform;
        mOFullScreenFrame.anchorMin = new Vector2(0.5f, 0.5f);
        mOFullScreenFrame.anchorMax = new Vector2(0.5f, 0.5f);
        mOFullScreenFrame.pivot = new Vector2(0.5f, 0.5f);
        mOFullScreenFrame.sizeDelta = new Vector2(ScreenWidth, ScreenHeight);
        mOFullScreenFrame.anchoredPosition = Vector2.zero;
        lOFrameObject.AddComponent<UWPixelAspectFrame>();

        // Picture track first, so the subtitle lies on top of it.
        mOPictureTexture = new Texture2D(ScreenWidth, ScreenHeight, TextureFormat.RGBA32, false);
        mOPictureTexture.name = "UWIntroPlayer.cs:988";
        mOPictureTexture.filterMode = FilterMode.Point;
        mOPictureTexture.wrapMode = TextureWrapMode.Clamp;
        mOPictureBuffer = new Color32[ScreenWidth * ScreenHeight];

        GameObject lOPictureObject = new GameObject("Picture", typeof(RectTransform), typeof(RawImage));
        lOPictureObject.transform.SetParent(lOCanvasObject.transform, false);

        RectTransform lOPictureRect = (RectTransform)lOPictureObject.transform;
        lOPictureRect.anchorMin = Vector2.zero;
        lOPictureRect.anchorMax = Vector2.one;
        lOPictureRect.offsetMin = Vector2.zero;
        lOPictureRect.offsetMax = Vector2.zero;

        mOPictureImage = lOPictureObject.GetComponent<RawImage>();
        mOPictureImage.texture = mOPictureTexture;
        mOPictureImage.raycastTarget = false;
        mOPictureImage.enabled = false;

        GameObject lOSubtitleObject = new GameObject("Subtitle", typeof(RectTransform), typeof(RawImage));
        lOSubtitleObject.transform.SetParent(lOCanvasObject.transform, false);

        RectTransform lORect = (RectTransform)lOSubtitleObject.transform;
        lORect.anchorMin = new Vector2(0.5f, 0f);
        lORect.anchorMax = new Vector2(0.5f, 0f);
        lORect.pivot = new Vector2(0.5f, 0f);
        lORect.anchoredPosition = new Vector2(0f, mfSubtitleBottomOffset);

        mOSubtitleImage = lOSubtitleObject.GetComponent<RawImage>();
        mOSubtitleImage.raycastTarget = false;
        mOSubtitleImage.enabled = false;
    }

    /// <summary>The black backdrop follows the picture: on behind every full-screen picture,
    /// off in the viewport, where the game around it belongs to the scene. After the
    /// coroutines and before drawing, so it never lags a frame.</summary>
    private void LateUpdate()
    {
        if (mOBackdrop == null || mOPictureImage == null)
            return;

        bool lbOn = mOPictureImage.enabled && !mbInViewport;

        if (mOBackdrop.enabled != lbOn)
            mOBackdrop.enabled = lbOn;

        if (mOModernFrame != null)
            mOModernFrame.enabled = mOPictureImage.enabled && mbInViewport && mbModernFrame;

        // WITH THE HELP OPEN THE PICTURE SLIDES LEFT with the game frame (per user, 2026-09-30:
        // a dream played while the help was open lay partly under it) - the same shift the
        // conversation's frame follows (UWConversationScreen.fFollowHelpLayout).
        if (mOFullScreenFrame != null)
            mOFullScreenFrame.anchoredPosition = new Vector2(UWHelpLayout.FrameShift(UWUiFit.CanvasWidth), 0f);
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
