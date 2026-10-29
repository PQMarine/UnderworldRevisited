using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The original's main menu: OPSCR.BYT with the four buttons from OPBTN.GR -
/// Introduction, Create Character, Acknowledgements, Journey Onward (the reference:
/// uimanager_mainmenu). Each button has two images, the second lights up under the
/// pointer.
///
/// WHEN IT APPEARS: at every launch after the company logos (see UWIntroPlayer), and after
/// the victory screens. Not after loading a save game or starting a new character - both
/// are scene reloads that come from this menu.
///
/// BEHIND IT there is no world at launch (UWLevelLoader.HasWorld): the last save game is no
/// longer loaded automatically, a game is started from here only (per user, 2026-09-14).
/// The static data is prepared meanwhile and reused by the game that is started.
///
///   Introduction      plays the introduction (CS000) and comes back
///   Create Character  UWCharacterCreationScreen
///   Acknowledgements  sequence 0xA (CS012), the acknowledgements
///   Journey Onward    the four savegame slots, a click loads
///
/// Escape does not close the menu - as in the original there is no world to return to.
///
/// SCREEN ONLY (UW2 plan phase 0 step 5, 2026-10-28): for a game the port cannot play yet
/// (IUWGameProfile.IsPlayable), the level loader hands over its main menu screen and nothing else -
/// no buttons, no music, no data behind it; the menu bar over it offers the Game window only, to
/// switch back (UWGameSwitch).
///
/// THE WALK MENU (UW2 plan phase 2 step 3, 2026-10-29): the second game's main menu with its own
/// screen and OPBTN.GR's buttons at the first game's places (Underworld Exhumed uw2 ui/MAINMENU.C:131,
/// the same four as uw1's) - three of them, as the original shows without a saved game. Create
/// Character opens the second game's creation (UWCharacterCreationScreenUw2, phase 3 step 5); the
/// introduction and the acknowledgements are the second game's cutscenes, which come later, so they do
/// not answer yet. No music (the second game's themes come with its sound), the menu bar offers the
/// Game window only.
///
/// ITS RULES (phase 3 step 5 part C, MAINMENU.C real_start, parse_start_input, parse_start_mouse,
/// do_journey; UWClassicLayoutUw2.MainMenu): one button is always lit - Create Character without a
/// save, Journey Onward with one; the cursor keys (and Ctrl+F, N, B, P) move the light, Home and Page Up
/// to the first, End and Page Down to the last, Enter presses; a mouse press lights what is under the
/// pointer while it is held and letting go on a button presses it - no light on a pointer only passing.
/// Journey Onward lists the saves found, only those, centred in FONT_BIG (lit colour 7, the rest 0xCD),
/// with the same keys, Escape and Alt+X back; the one chosen shows "You reenter the Underworld . . ." a
/// moment and starts the walk from it. Escape on the buttons does nothing, as there; Alt+X there would
/// end the program and is left out.
/// </summary>
public class UWMainMenu : MonoBehaviour
{
    /// <summary>Where the four buttons sit (the reference's scene, divided by four).</summary>
    private static readonly Vector2Int[] miButtonPositions =
        System.Array.ConvertAll(UWClassicLayout.MainMenu.Buttons, pyButton => new Vector2Int(pyButton.X, pyButton.Y));

    private const int ButtonCount = 4;

    private const int IntroductionButton = 0;

    private const int CreateCharacterButton = 1;

    private const int AcknowledgementsButton = 2;

    private const int JourneyOnwardButton = 3;

    private const int IntroductionCutscene = 0;

    private const int AcknowledgementsCutscene = 0xA;

    /// <summary>The list of savegames, centred below the lettering.</summary>
    private const int SaveListTop = UWClassicLayout.MainMenu.SaveListTop;

    private const int SaveListPitch = UWClassicLayout.MainMenu.SaveListPitch;

    /// <summary>Palette of the screen and the two text colours of the list (the reference:
    /// PaletteIndexSaveGameSelected 0xA2, ...UnSelected 0xAA).</summary>
    private const int ScreenPalette = UWClassicLayout.MainMenu.Palette;

    private const int SelectedColourIndex = UWClassicLayout.MainMenu.SelectedColourIndex;

    private const int UnselectedColourIndex = UWClassicLayout.MainMenu.UnselectedColourIndex;

    private enum Mode
    {
        Hidden,
        Buttons,
        SaveList,
        Cutscene,
        CharacterCreation,

        /// <summary>A screen without buttons (ShowScreenOnly).</summary>
        ScreenOnly
    }

    /// <summary>Is the menu up - or something it has called?</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>Whether the menu bar may fold out now: only over the buttons and the save list,
    /// not during a cutscene or character creation (see UWSetupMenu).</summary>
    public static bool AcceptsMenuBar { get; private set; }

    /// <summary>Whether the menu shows a screen only - the main menu of a game the port cannot play
    /// yet (ShowScreenOnly); the menu bar offers the Game window only then.</summary>
    public static bool IsScreenOnly { get; private set; }

    /// <summary>Whether the menu is the second game's walk menu (InitWalkMenu).</summary>
    public static bool IsWalkMenu { get; private set; }

    /// <summary>The buttons the walk menu shows: Journey Onward is left out, as the original does
    /// without a saved game.</summary>
    private const int WalkButtonCount = 3;

    private int ShownButtons => IsWalkMenu && !fAnyWalkSave() ? WalkButtonCount : ButtonCount;

    /// <summary>Whether the save folder holds a save of the running game (the walk menu offers Journey
    /// Onward then, as the original with a saved game; UW2 plan phase 2 step 6).</summary>
    private static bool fAnyWalkSave()
    {
        string lsRoot = UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;

        for (int liSlot = 1; liSlot <= UWSavegameSlots.SlotCount; liSlot++)
        {
            if (UWSavegameSlots.Exists(lsRoot, liSlot))
                return true;
        }

        return false;
    }

    /// <summary>Does the menu come up by itself on this launch? The same rule as in
    /// fAutoShow - for the music while loading (see UWLevelLoader.Start).</summary>
    public static bool WillAutoShow
    {
        get { return !UWSavegameSlots.IsRestoring && !UWCharacterCreationScreen.IsStartingNewGame; }
    }

    private DataImport mOData;
    private Canvas mOCanvas;
    private RectTransform mOFrame;
    private RawImage mOBackground;
    private readonly RawImage[] mOButtons = new RawImage[ButtonCount];
    private readonly Texture2D[] mOButtonOff = new Texture2D[ButtonCount];
    private readonly Texture2D[] mOButtonOn = new Texture2D[ButtonCount];
    private readonly RawImage[] mOSaveLabels = new RawImage[UWSavegameSlots.SlotCount];
    private readonly string[] msSaveNames = new string[UWSavegameSlots.SlotCount];
    private int miHover = -1;
    private Mode meMode = Mode.Hidden;
    private bool mbAutoShowDone;
    private int miStartFrame;
    private UWIntroPlayer mOIntro;
    private UWCharacterCreationScreen mOCharacterCreation;

    /// <summary>The second game's creation (UW2 plan phase 3 step 5), made by the walk menu when first asked.</summary>
    private UWCharacterCreationScreenUw2 mOCharacterCreationUw2;

    /// <summary>The second game's lit button or description (parse_start_input's cur), the press it
    /// follows, the slots its list shows, the description drawn lit, and the save being re-entered.</summary>
    private int miWalkLit;

    /// <summary>The buttons shown, counted once when they show (ShownButtons looks for saves on disk).</summary>
    private int miWalkShown;

    private bool mbWalkPress;

    private readonly List<int> mOWalkSlots = new List<int>();

    private int miWalkDrawnLit = -1;

    private RawImage mOReenter;

    private float mfReenterTime = -1f;

    private int miReenterSlot;

    /// <summary>How long "You reenter the Underworld . . ." stands before the walk loads - the original
    /// shows it while it restores.</summary>
    public const float ReenterSeconds = 1.5f;
    private UWControlScheme mOControls;
    private Color32 mOSelectedColour;
    private Color32 mOUnselectedColour;

    public void Init(DataImport pOData)
    {
        // Static, so it survives the scene restart: after "Journey Onward" it would otherwise
        // still be open, and the game would keep the cross pointer and locked keys.
        IsOpen = false;
        IsScreenOnly = false;
        IsWalkMenu = false;

        mOData = pOData;
        miStartFrame = Time.frameCount;
        mbAutoShowDone = false;

        fReadColours();
    }

    /// <summary>The second game's main menu over its data (see the class comment): shown at once.</summary>
    public void InitWalkMenu(DataImport pOData)
    {
        Init(pOData);

        IsWalkMenu = true;
        mbAutoShowDone = true;

        Show();
    }

    /// <summary>The two text colours of the save list, through the colour help (see
    /// UWColourVision).</summary>
    private void fReadColours()
    {
        if (mOData == null)
            return;

        mOSelectedColour = UWColourVision.Apply(UWScreenUi.GetColour(mOData.Palettes, ScreenPalette, SelectedColourIndex));
        mOUnselectedColour = UWColourVision.Apply(UWScreenUi.GetColour(mOData.Palettes, ScreenPalette, UnselectedColourIndex));

        miColourVersion = UWColourVision.Version;
    }

    /// <summary>Which state of the colour help the pictures were built from - they are built once
    /// and would otherwise keep their colours until the next start (per user, 2026-09-17).</summary>
    private int miColourVersion = -1;

    private void fRefreshColours()
    {
        // The screen-only picture came with the colour help of its start (no data to build it anew).
        if (mOCanvas == null || miColourVersion == UWColourVision.Version || meMode == Mode.ScreenOnly)
            return;

        fReadColours();

        FilterMode leFilter = UnderworldRevisited.UWSettings.Instance != null
            ? UnderworldRevisited.UWSettings.Instance.TextureFilterMode : FilterMode.Point;

        Texture2D lOBackground = UWScreenUi.BuildTexture(mOData.Textures, UWTexture.TextureTypes.OPSCR, 0, leFilter);

        if (lOBackground != null)
        {
            Destroy(mOBackground.texture);
            UWScreenUi.SetTexture(mOBackground, lOBackground);
        }

        for (int liAt = 0; liAt < ButtonCount; liAt++)
        {
            Destroy(mOButtonOff[liAt]);
            Destroy(mOButtonOn[liAt]);

            mOButtonOff[liAt] = UWScreenUi.BuildTexture(mOData.Textures, UWTexture.TextureTypes.OPBTN, liAt * 2, leFilter);
            mOButtonOn[liAt] = UWScreenUi.BuildTexture(mOData.Textures, UWTexture.TextureTypes.OPBTN, (liAt * 2) + 1, leFilter);

            UWScreenUi.SetTexture(mOButtons[liAt], liAt == miHover ? mOButtonOn[liAt] : mOButtonOff[liAt]);
        }

        if (meMode == Mode.SaveList)
        {
            for (int liSlot = 0; liSlot < UWSavegameSlots.SlotCount; liSlot++)
                fDrawSaveLabel(liSlot, liSlot == miHover);
        }
    }

    private void Update()
    {
        fRefreshColours();

        AcceptsMenuBar = IsOpen && (meMode == Mode.Buttons || meMode == Mode.SaveList || meMode == Mode.ScreenOnly);

        // The menu bar, one of its menus or dialogs has the mouse.
        if (AcceptsMenuBar && UWSetupMenu.IsCapturingInput)
            return;

        switch (meMode)
        {
            case Mode.Hidden:
                fAutoShow();
                return;

            case Mode.Cutscene:
                if (mOIntro == null || !mOIntro.IsPlaying)
                    fShowButtons();
                return;

            case Mode.CharacterCreation:
                // If character creation has started the game, the scene is already being rebuilt.
                // Opening again would mean timeScale 0 - and that survives the scene change:
                // black intro, no movement (per user, 2026-09-11).
                if (UWCharacterCreationScreen.IsStartingNewGame || UWCharacterCreationScreenUw2.IsStartingWalk)
                {
                    meMode = Mode.Hidden;
                    IsOpen = false;
                    return;
                }

                if (IsWalkMenu ? mOCharacterCreationUw2 == null || !mOCharacterCreationUw2.IsOpen
                    : mOCharacterCreation == null || !mOCharacterCreation.IsOpen)
                    fShowButtons();
                return;

            case Mode.Buttons:
                if (IsWalkMenu)
                    fUpdateWalkButtons();
                else
                    fUpdateButtons();
                return;

            case Mode.SaveList:
                if (IsWalkMenu)
                    fUpdateWalkSaveList();
                else
                    fUpdateSaveList();
                return;

            case Mode.ScreenOnly:
                return;
        }
    }

    /// <summary>
    /// Shows pOScreen (320 x 200, the main menu screen of a game the port cannot play yet) as the
    /// menu's background and nothing else; the bar over it can switch the game (UW2 plan phase 0
    /// step 5). Stays until the scene starts again.
    /// </summary>
    public void ShowScreenOnly(Texture2D pOScreen)
    {
        IsOpen = true;
        IsScreenOnly = true;
        mbAutoShowDone = true;
        meMode = Mode.ScreenOnly;

        mOCanvas = UWScreenUi.CreateCanvas(transform, "UW Main Menu", UWUiSorting.MainMenu, out mOFrame);
        mOBackground = UWUiKit.CreateRawImageAt(mOFrame, "Title Screen", 0, 0);
        UWScreenUi.SetTexture(mOBackground, pOScreen);
        mOCanvas.gameObject.SetActive(true);

        UWSetupMenu.EnsureForMainMenu(null);
        fSetModal(true);
        UWScreenUi.HideLoadingCover();
    }

    /// <summary>
    /// At launch, as soon as the logos are done. Waits two frames so that the start routines
    /// of the other components - the intro above all - have run first.
    /// </summary>
    private void fAutoShow()
    {
        if (mbAutoShowDone || mOData == null)
            return;

        if (Time.frameCount < miStartFrame + 2)
            return;

        if (UWSavegameSlots.IsRestoring || UWCharacterCreationScreen.IsStartingNewGame)
        {
            mbAutoShowDone = true;
            return;
        }

        if (mOIntro == null)
            mOIntro = UWScene.IntroPlayer;

        // ALSO WAIT WHILE THE OPENING IS ONLY OWED, not yet running: it waits for the game
        // data, and two frames after the start it is usually not there. Without this the
        // menu was already up when the logos began (per user, 2026-09-22).
        if (mOIntro != null && (mOIntro.IsPlaying || mOIntro.IsOpeningPending))
            return;

        mbAutoShowDone = true;
        Show();
    }

    public void Show()
    {
        if (mOData == null)
            return;

        fEnsureCanvas();
        fShowButtons();

        // The black cover of the start and of the step after the title (UWLevelLoader.Start,
        // UWSplashSequence.AfterSplashScreens) is there so that the game never shows before the
        // menu. Now the menu stands.
        UWScreenUi.HideLoadingCover();

        // The menu plays "Introduction" (the reference: main.cs, LoadXMI(IntroTheme)); the second
        // game's music is not read yet.
        if (!IsWalkMenu)
            UWAudioService.ChangeTheme(UWMusicSelector.IntroTheme);
    }

    /// <summary>After the victory screens (UWEndgame.PlayVictory). The world behind it is
    /// finished; the menu never returns to it.</summary>
    public void ShowAfterVictory()
    {
        mbAutoShowDone = true;

        Show();
    }

    public void Hide()
    {
        meMode = Mode.Hidden;
        IsOpen = false;

        UWAudioService.PickLevelTheme();

        if (mOCanvas != null)
            mOCanvas.gameObject.SetActive(false);

        fSetModal(false);
        UWGameClock.ReleaseAll();
    }

    private void fShowButtons()
    {
        fEnsureCanvas();

        UWSetupMenu.EnsureForMainMenu(IsWalkMenu ? null : mOData);

        meMode = Mode.Buttons;
        IsOpen = true;
        miHover = -1;

        mOCanvas.gameObject.SetActive(true);

        for (int liAt = 0; liAt < ButtonCount; liAt++)
        {
            UWScreenUi.SetTexture(mOButtons[liAt], mOButtonOff[liAt]);
            mOButtons[liAt].gameObject.SetActive(liAt < ShownButtons);
        }

        foreach (RawImage lOLabel in mOSaveLabels)
            lOLabel.gameObject.SetActive(false);

        fSetModal(true);
        UWGameClock.Hold(UWGameClock.MainMenuHold);

        // The second game's menu lights a button from the start (real_start's sel).
        if (IsWalkMenu)
        {
            mOReenter.gameObject.SetActive(false);
            mfReenterTime = -1f;
            mbWalkPress = false;
            miWalkShown = ShownButtons;
            miWalkLit = miWalkShown == ButtonCount ? UWClassicLayoutUw2.MainMenu.LitWithSaves : UWClassicLayoutUw2.MainMenu.LitWithoutSaves;
            fLightWalkButtons();
        }
    }

    /// <summary>The gamepad steps through the buttons and the save games (UWPadEntryStepper, per
    /// user 2026-10-08: as in the other menus); a new mode snaps the pointer onto its first.</summary>
    private readonly UWPadEntryStepper mOPadStepper = new UWPadEntryStepper();

    private readonly List<Rect> mOPadRects = new List<Rect>();

    private Mode meSteppedMode = Mode.Hidden;

    private void fStepByPad(RawImage[] pyEntries, int piCount)
    {
        mOPadRects.Clear();

        for (int liAt = 0; liAt < piCount; liAt++)
        {
            Rect lORect = UWScreenUi.ScreenRectOf(pyEntries[liAt]);

            if (lORect.width > 0f)
                mOPadRects.Add(lORect);
        }

        if (meSteppedMode != meMode)
        {
            meSteppedMode = meMode;
            mOPadStepper.Opened();
            return;
        }

        mOPadStepper.Update(mOPadRects);
    }

    private void fUpdateButtons()
    {
        fStepByPad(mOButtons, ShownButtons);

        int liHover = -1;

        for (int liAt = 0; liAt < ShownButtons; liAt++)
        {
            if (UWScreenUi.IsMouseOver(mOButtons[liAt]))
            {
                liHover = liAt;
                break;
            }
        }

        if (liHover != miHover)
        {
            miHover = liHover;

            for (int liAt = 0; liAt < ButtonCount; liAt++)
                UWScreenUi.SetTexture(mOButtons[liAt], liAt == miHover ? mOButtonOn[liAt] : mOButtonOff[liAt]);
        }

        if (miHover < 0 || !UWScreenUi.WasLeftClicked())
            return;

        switch (miHover)
        {
            case IntroductionButton:
                fPlayCutscene(IntroductionCutscene);
                break;

            case CreateCharacterButton:
                fOpenCharacterCreation();
                break;

            case AcknowledgementsButton:
                fPlayCutscene(AcknowledgementsCutscene);
                break;

            case JourneyOnwardButton:
                fShowSaveList();
                break;
        }
    }

    private void fPlayCutscene(int piCutscene)
    {
        if (mOIntro == null)
            mOIntro = UWScene.IntroPlayer;

        if (mOIntro == null)
            return;

        meMode = Mode.Cutscene;
        mOCanvas.gameObject.SetActive(false);

        // The cutscene locks input itself and runs on the unscaled clock; whatever world
        // there is stays frozen.
        UWGameClock.Hold(UWGameClock.MainMenuHold);
        fSetModal(false);

        mOIntro.PlayCutscene(piCutscene);

        if (!mOIntro.IsPlaying)
            fShowButtons();
    }

    private void fOpenCharacterCreation()
    {
        if (mOCharacterCreation == null)
            mOCharacterCreation = GetComponent<UWCharacterCreationScreen>();

        if (mOCharacterCreation == null)
            return;

        meMode = Mode.CharacterCreation;
        mOCharacterCreation.Show();

        if (!mOCharacterCreation.IsOpen)
            fShowButtons();
    }

    /// <summary>The second game's Create Character (UW2 plan phase 3 step 5): its own creation screen,
    /// whose "Yes" starts the walk.</summary>
    private void fOpenCharacterCreationUw2()
    {
        if (mOCharacterCreationUw2 == null)
        {
            mOCharacterCreationUw2 = GetComponent<UWCharacterCreationScreenUw2>();

            if (mOCharacterCreationUw2 == null)
                mOCharacterCreationUw2 = gameObject.AddComponent<UWCharacterCreationScreenUw2>();

            mOCharacterCreationUw2.Init(mOData);
        }

        meMode = Mode.CharacterCreation;
        mOCharacterCreationUw2.Show();

        if (!mOCharacterCreationUw2.IsOpen)
            fShowButtons();
    }

    // ------------------------------------------------------------------
    // The second game's menu (UW2 plan phase 3 step 5 part C)
    // ------------------------------------------------------------------

    private void fUpdateWalkButtons()
    {
        fStepByPad(mOButtons, miWalkShown);

        int liChosen = fWalkInput(miWalkShown, fWalkButtonAt, false, out bool _);

        fLightWalkButtons();

        if (liChosen == CreateCharacterButton)
            fOpenCharacterCreationUw2();
        else if (liChosen == JourneyOnwardButton)
            fShowWalkSaveList();
    }

    private void fLightWalkButtons()
    {
        for (int liAt = 0; liAt < ButtonCount; liAt++)
            UWScreenUi.SetTexture(mOButtons[liAt], liAt == miWalkLit ? mOButtonOn[liAt] : mOButtonOff[liAt]);
    }

    private int fWalkButtonAt()
    {
        for (int liAt = 0; liAt < miWalkShown; liAt++)
        {
            if (UWScreenUi.IsMouseOver(mOButtons[liAt]))
                return liAt;
        }

        return -1;
    }

    private int fWalkSaveAt()
    {
        for (int liAt = 0; liAt < mOWalkSlots.Count; liAt++)
        {
            if (UWScreenUi.IsMouseOver(mOSaveLabels[liAt]))
                return liAt;
        }

        return -1;
    }

    /// <summary>
    /// parse_start_input and parse_start_mouse over piCount entries: the light moved by the keys and kept
    /// inside them, a press followed (the entry under the pointer lit while held), and what was chosen -
    /// the entry let go on, or the lit one on Enter; -1 for none. pbBack: Escape or Alt+X in the list.
    /// </summary>
    private int fWalkInput(int piCount, System.Func<int> pFUnderPointer, bool pbList, out bool pbBack)
    {
        pbBack = false;

        Mouse lOMouse = Mouse.current;

        if (lOMouse != null && UWPointer.HasMouse)
        {
            if (!mbWalkPress && (lOMouse.leftButton.wasPressedThisFrame || lOMouse.rightButton.wasPressedThisFrame || lOMouse.middleButton.wasPressedThisFrame))
                mbWalkPress = true;

            if (mbWalkPress)
            {
                int liUnder = pFUnderPointer();

                if (liUnder >= 0)
                    miWalkLit = liUnder;

                if (lOMouse.leftButton.isPressed || lOMouse.rightButton.isPressed || lOMouse.middleButton.isPressed)
                    return -1;

                mbWalkPress = false;

                return liUnder;
            }
        }

        Keyboard lOKeys = Keyboard.current;

        if (lOKeys == null || piCount <= 0)
            return -1;

        bool lbCtrl = lOKeys.ctrlKey.isPressed;

        if (lOKeys.rightArrowKey.wasPressedThisFrame || lOKeys.downArrowKey.wasPressedThisFrame
            || (lbCtrl && (lOKeys.fKey.wasPressedThisFrame || lOKeys.nKey.wasPressedThisFrame)))
            miWalkLit++;
        else if (lOKeys.leftArrowKey.wasPressedThisFrame || lOKeys.upArrowKey.wasPressedThisFrame
            || (lbCtrl && (lOKeys.bKey.wasPressedThisFrame || lOKeys.pKey.wasPressedThisFrame)))
            miWalkLit--;
        else if (lOKeys.homeKey.wasPressedThisFrame || lOKeys.pageUpKey.wasPressedThisFrame)
            miWalkLit = 0;
        else if (lOKeys.endKey.wasPressedThisFrame || lOKeys.pageDownKey.wasPressedThisFrame)
            miWalkLit = piCount - 1;
        else if (UWTextEntry.EnterPressed)
            return Mathf.Clamp(miWalkLit, 0, piCount - 1);
        else if (pbList && (UWBack.Takes(UWBackOwner.MainMenuSaves) || (lOKeys.altKey.isPressed && lOKeys.xKey.wasPressedThisFrame)))
            pbBack = true;

        miWalkLit = Mathf.Clamp(miWalkLit, 0, piCount - 1);

        return -1;
    }

    /// <summary>do_journey: the saves found, their descriptions in order, the first lit.</summary>
    private void fShowWalkSaveList()
    {
        meMode = Mode.SaveList;
        mbWalkPress = false;
        miWalkLit = 0;
        miWalkDrawnLit = -1;

        for (int liAt = 0; liAt < ButtonCount; liAt++)
            mOButtons[liAt].gameObject.SetActive(false);

        string lsRoot = UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;

        mOWalkSlots.Clear();

        for (int liSlot = 1; liSlot <= UWSavegameSlots.SlotCount; liSlot++)
        {
            if (UWSavegameSlots.Exists(lsRoot, liSlot))
            {
                msSaveNames[mOWalkSlots.Count] = UWSavegameSlots.GetName(lsRoot, liSlot);
                mOWalkSlots.Add(liSlot);
            }
        }

        for (int liAt = 0; liAt < mOSaveLabels.Length; liAt++)
            mOSaveLabels[liAt].gameObject.SetActive(liAt < mOWalkSlots.Count);

        fDrawWalkSaveLabels();
    }

    /// <summary>draw_start_buttons with text: each description cut to 0x13E columns, centred on x 0xA0 at
    /// y 100 - i * 0x16, the lit one in colour 7, the others in 0xCD.</summary>
    private void fDrawWalkSaveLabels()
    {
        if (miWalkDrawnLit == miWalkLit)
            return;

        miWalkDrawnLit = miWalkLit;

        UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.Big);
        Color32 lOLit = UWColourVision.Apply(UWScreenUi.GetColour(mOData.Palettes, UWClassicLayoutUw2.MainMenu.Palette, UWClassicLayoutUw2.MainMenu.ListLitColour));
        Color32 lOPlain = UWColourVision.Apply(UWScreenUi.GetColour(mOData.Palettes, UWClassicLayoutUw2.MainMenu.Palette, UWClassicLayoutUw2.MainMenu.ListColour));

        for (int liAt = 0; liAt < mOWalkSlots.Count; liAt++)
        {
            string lsName = msSaveNames[liAt] ?? string.Empty;

            while (lsName.Length > 0 && fBigWidth(lOFont, lsName) > UWClassicLayoutUw2.MainMenu.ListWidth)
                lsName = lsName.Substring(0, lsName.Length - 1);

            int liWidth = fBigWidth(lOFont, lsName);

            UWScreenUi.Place(mOSaveLabels[liAt], UWClassicLayoutUw2.MainMenu.ListCentre - (liWidth / 2),
                UWScreenUi.ScreenHeight - 1 - (UWClassicLayoutUw2.MainMenu.ListTop - (liAt * UWClassicLayoutUw2.MainMenu.ListStep)));
            UWScreenUi.SetText(mOSaveLabels[liAt], lOFont, lsName, liAt == miWalkLit ? lOLit : lOPlain, liWidth + 1, false);
        }
    }

    private static int fBigWidth(UWFont pOFont, string psText)
    {
        return pOFont != null && !string.IsNullOrEmpty(psText) ? pOFont.MeasureText(psText, 0, UWFontRenderer.CharacterSpacing) : 0;
    }

    private void fUpdateWalkSaveList()
    {
        if (mfReenterTime >= 0f)
        {
            if (Time.unscaledTime - mfReenterTime < ReenterSeconds)
                return;

            string lsRoot = UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;

            mfReenterTime = -1f;
            meMode = Mode.Hidden;
            IsOpen = false;
            fSetModal(false);
            UWWalkUw2.Request(UWSavegameSlots.GetSlotPath(lsRoot, miReenterSlot));

            return;
        }

        fStepByPad(mOSaveLabels, mOWalkSlots.Count);

        int liChosen = fWalkInput(mOWalkSlots.Count, fWalkSaveAt, true, out bool lbBack);

        if (lbBack)
        {
            fShowButtons();

            return;
        }

        fDrawWalkSaveLabels();

        if (liChosen < 0)
            return;

        // do_journey: the screen alone again and the message, then the restore.
        miReenterSlot = mOWalkSlots[liChosen];

        foreach (RawImage lOLabel in mOSaveLabels)
            lOLabel.gameObject.SetActive(false);

        UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.Big);
        string lsReenter = UWText.Block(mOData, 1, UWClassicLayoutUw2.MainMenu.ReenterString + 1, string.Empty);
        int liWidth = fBigWidth(lOFont, lsReenter);

        mOReenter.gameObject.SetActive(true);
        UWScreenUi.Place(mOReenter, ((UWScreenUi.ScreenWidth - liWidth) / 2) + UWClassicLayoutUw2.MainMenu.ReenterShift,
            UWScreenUi.ScreenHeight - 1 - UWClassicLayoutUw2.MainMenu.ReenterY);
        UWScreenUi.SetText(mOReenter, lOFont, lsReenter,
            UWColourVision.Apply(UWScreenUi.GetColour(mOData.Palettes, UWClassicLayoutUw2.MainMenu.Palette, UWClassicLayoutUw2.MainMenu.ListLitColour)),
            liWidth + 1, false);

        mfReenterTime = Time.unscaledTime;
    }

    // ------------------------------------------------------------------
    // Journey Onward
    // ------------------------------------------------------------------

    private void fShowSaveList()
    {
        meMode = Mode.SaveList;
        miHover = -1;

        for (int liAt = 0; liAt < ButtonCount; liAt++)
            mOButtons[liAt].gameObject.SetActive(false);

        string lsRoot = UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;

        for (int liSlot = 0; liSlot < UWSavegameSlots.SlotCount; liSlot++)
        {
            msSaveNames[liSlot] = UWSavegameSlots.GetName(lsRoot, liSlot + 1);
            mOSaveLabels[liSlot].gameObject.SetActive(true);
            fDrawSaveLabel(liSlot, false);
        }
    }

    private void fDrawSaveLabel(int piSlot, bool pbSelected)
    {
        UWFont lOFont = mOData.Fonts.Get(UWFonts.FontType.Big);

        UWScreenUi.SetText(mOSaveLabels[piSlot], lOFont, msSaveNames[piSlot],
            pbSelected ? mOSelectedColour : mOUnselectedColour, UWScreenUi.ScreenWidth, true);
    }

    private void fUpdateSaveList()
    {
        // B as Escape (UWScreenUi.WasPadBackPressed until cleanup step 7.5; the chain, UWBack).
        if (UWBack.Takes(UWBackOwner.MainMenuSaves))
        {
            fShowButtons();
            return;
        }

        fStepByPad(mOSaveLabels, UWSavegameSlots.SlotCount);

        int liHover = -1;

        for (int liSlot = 0; liSlot < UWSavegameSlots.SlotCount; liSlot++)
        {
            if (UWScreenUi.IsMouseOver(mOSaveLabels[liSlot]))
            {
                liHover = liSlot;
                break;
            }
        }

        if (liHover != miHover)
        {
            int liOld = miHover;
            miHover = liHover;

            if (liOld >= 0)
                fDrawSaveLabel(liOld, false);

            if (miHover >= 0)
                fDrawSaveLabel(miHover, true);
        }

        if (miHover < 0 || !UWScreenUi.WasLeftClicked())
            return;

        string lsRoot = UnderworldRevisited.UWSettings.Instance != null ? UnderworldRevisited.UWSettings.Instance.SavegameRoot : null;

        // Time must run again before the restart - otherwise the scene starts frozen.
        UWGameClock.ReleaseAll();

        if (!UWSavegameSlots.Restore(lsRoot, miHover + 1))
            UWGameClock.Hold(UWGameClock.MainMenuHold);
    }

    // ------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------

    private void fSetModal(bool pbModal)
    {
        if (mOControls == null)
            mOControls = GetComponentInParent<UWControlScheme>();

        if (mOControls == null)
            mOControls = UWScene.ControlScheme;

        if (mOControls != null)
            mOControls.SetUiModal("main menu", pbModal);

        if (pbModal)
            UWPointer.IsShown = true;
    }

    private void fEnsureCanvas()
    {
        if (mOCanvas != null)
            return;

        mOCanvas = UWScreenUi.CreateCanvas(transform, "UW Main Menu", UWUiSorting.MainMenu, out mOFrame);

        FilterMode leFilter = UnderworldRevisited.UWSettings.Instance != null
            ? UnderworldRevisited.UWSettings.Instance.TextureFilterMode : FilterMode.Point;

        mOBackground = UWUiKit.CreateRawImageAt(mOFrame, "Title Screen", 0, 0);
        UWScreenUi.SetTexture(mOBackground, UWScreenUi.BuildTexture(mOData.Textures, UWTexture.TextureTypes.OPSCR, 0, leFilter));

        for (int liAt = 0; liAt < ButtonCount; liAt++)
        {
            mOButtonOff[liAt] = UWScreenUi.BuildTexture(mOData.Textures, UWTexture.TextureTypes.OPBTN, liAt * 2, leFilter);
            mOButtonOn[liAt] = UWScreenUi.BuildTexture(mOData.Textures, UWTexture.TextureTypes.OPBTN, (liAt * 2) + 1, leFilter);

            mOButtons[liAt] = UWUiKit.CreateRawImageAt(mOFrame, "Button " + (liAt + 1),
                miButtonPositions[liAt].x, miButtonPositions[liAt].y);
        }

        for (int liSlot = 0; liSlot < UWSavegameSlots.SlotCount; liSlot++)
        {
            mOSaveLabels[liSlot] = UWUiKit.CreateRawImageAt(mOFrame, "Savegame " + (liSlot + 1),
                0, SaveListTop + (liSlot * SaveListPitch));
            mOSaveLabels[liSlot].gameObject.SetActive(false);
        }

        mOReenter = UWUiKit.CreateRawImageAt(mOFrame, "Reenter", 0, 0);
        mOReenter.gameObject.SetActive(false);

        mOCanvas.gameObject.SetActive(false);
    }
}
