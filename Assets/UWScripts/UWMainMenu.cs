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
/// </summary>
public class UWMainMenu : MonoBehaviour
{
    private const int SortingOrder = 95;

    /// <summary>Where the four buttons sit (the reference's scene, divided by four).</summary>
    private static readonly Vector2Int[] miButtonPositions =
    {
        new Vector2Int(98, 81), new Vector2Int(81, 104), new Vector2Int(72, 128), new Vector2Int(85, 153)
    };

    private const int ButtonCount = 4;

    private const int IntroductionButton = 0;

    private const int CreateCharacterButton = 1;

    private const int AcknowledgementsButton = 2;

    private const int JourneyOnwardButton = 3;

    private const int IntroductionCutscene = 0;

    private const int AcknowledgementsCutscene = 0xA;

    /// <summary>The list of savegames, centred below the lettering.</summary>
    private const int SaveListTop = 84;

    private const int SaveListPitch = 20;

    /// <summary>Palette of the screen and the two text colours of the list (the reference:
    /// PaletteIndexSaveGameSelected 0xA2, ...UnSelected 0xAA).</summary>
    private const int ScreenPalette = 2;

    private const int SelectedColourIndex = 0xA2;

    private const int UnselectedColourIndex = 0xAA;

    private enum Mode
    {
        Hidden,
        Buttons,
        SaveList,
        Cutscene,
        CharacterCreation
    }

    /// <summary>Is the menu up - or something it has called?</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>Whether the menu bar may fold out now: only over the buttons and the save list,
    /// not during a cutscene or character creation (see UWSetupMenu).</summary>
    public static bool AcceptsMenuBar { get; private set; }

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
    private UWControlScheme mOControls;
    private Color32 mOSelectedColour;
    private Color32 mOUnselectedColour;

    public void Init(DataImport pOData)
    {
        // Static, so it survives the scene restart: after "Journey Onward" it would otherwise
        // still be open, and the game would keep the cross pointer and locked keys.
        IsOpen = false;

        mOData = pOData;
        miStartFrame = Time.frameCount;
        mbAutoShowDone = false;

        fReadColours();
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
        if (mOCanvas == null || miColourVersion == UWColourVision.Version)
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

        AcceptsMenuBar = IsOpen && (meMode == Mode.Buttons || meMode == Mode.SaveList);

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
                if (UWCharacterCreationScreen.IsStartingNewGame)
                {
                    meMode = Mode.Hidden;
                    IsOpen = false;
                    return;
                }

                if (mOCharacterCreation == null || !mOCharacterCreation.IsOpen)
                    fShowButtons();
                return;

            case Mode.Buttons:
                fUpdateButtons();
                return;

            case Mode.SaveList:
                fUpdateSaveList();
                return;
        }
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
        // UWIntroPlayer.fAfterSplashScreens) is there so that the game never shows before the
        // menu. Now the menu stands.
        UWScreenUi.HideLoadingCover();

        // The menu plays "Introduction" (the reference: main.cs, LoadXMI(IntroTheme)).
        UWMusic.ChangeTheme(UWMusic.IntroTheme);
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

        UWMusic.PickLevelTheme();

        if (mOCanvas != null)
            mOCanvas.gameObject.SetActive(false);

        fSetModal(false);
        UWGameClock.ReleaseAll();
    }

    private void fShowButtons()
    {
        fEnsureCanvas();

        UWSetupMenu.EnsureForMainMenu(mOData, mOFrame);

        meMode = Mode.Buttons;
        IsOpen = true;
        miHover = -1;

        mOCanvas.gameObject.SetActive(true);

        for (int liAt = 0; liAt < ButtonCount; liAt++)
        {
            UWScreenUi.SetTexture(mOButtons[liAt], mOButtonOff[liAt]);
            mOButtons[liAt].gameObject.SetActive(true);
        }

        foreach (RawImage lOLabel in mOSaveLabels)
            lOLabel.gameObject.SetActive(false);

        fSetModal(true);
        UWGameClock.Hold(UWGameClock.MainMenuHold);
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
        fStepByPad(mOButtons, ButtonCount);

        int liHover = -1;

        for (int liAt = 0; liAt < ButtonCount; liAt++)
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
        Keyboard lOKeyboard = Keyboard.current;

        // B as Escape (UWScreenUi.WasPadBackPressed).
        if ((lOKeyboard != null && lOKeyboard.escapeKey.wasPressedThisFrame) || UWScreenUi.WasPadBackPressed())
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
            Cursor.visible = true;
    }

    private void fEnsureCanvas()
    {
        if (mOCanvas != null)
            return;

        mOCanvas = UWScreenUi.CreateCanvas(transform, "UW Main Menu", SortingOrder, out mOFrame);

        FilterMode leFilter = UnderworldRevisited.UWSettings.Instance != null
            ? UnderworldRevisited.UWSettings.Instance.TextureFilterMode : FilterMode.Point;

        mOBackground = UWScreenUi.CreateRawImage(mOFrame, "Title Screen", 0, 0);
        UWScreenUi.SetTexture(mOBackground, UWScreenUi.BuildTexture(mOData.Textures, UWTexture.TextureTypes.OPSCR, 0, leFilter));

        for (int liAt = 0; liAt < ButtonCount; liAt++)
        {
            mOButtonOff[liAt] = UWScreenUi.BuildTexture(mOData.Textures, UWTexture.TextureTypes.OPBTN, liAt * 2, leFilter);
            mOButtonOn[liAt] = UWScreenUi.BuildTexture(mOData.Textures, UWTexture.TextureTypes.OPBTN, (liAt * 2) + 1, leFilter);

            mOButtons[liAt] = UWScreenUi.CreateRawImage(mOFrame, "Button " + (liAt + 1),
                miButtonPositions[liAt].x, miButtonPositions[liAt].y);
        }

        for (int liSlot = 0; liSlot < UWSavegameSlots.SlotCount; liSlot++)
        {
            mOSaveLabels[liSlot] = UWScreenUi.CreateRawImage(mOFrame, "Savegame " + (liSlot + 1),
                0, SaveListTop + (liSlot * SaveListPitch));
            mOSaveLabels[liSlot].gameObject.SetActive(false);
        }

        mOCanvas.gameObject.SetActive(false);
    }
}
