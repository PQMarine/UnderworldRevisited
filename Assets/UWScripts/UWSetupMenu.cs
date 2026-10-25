using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnderworldRevisited;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// The menu bar at the top edge and its dialogs (per user, 2026-09-17).
///
/// FIRST RUN: when no game data is found, the game no longer stays on a black screen. It shows
/// this bar over a plain background and opens the game folder dialog; once a folder with a
/// usable game.gog is chosen, it is remembered (UWUserSettings) and the scene starts again -
/// now with the company logos and the real main menu.
///
/// THE LOOK is deliberately plain and modern: at this point there is no game data, so neither
/// the original's pictures nor its fonts exist. Drawn with IMGUI until phase 8b, because the scene
/// has no EventSystem on purpose (see UWItemDrag) and IMGUI brought text field and scroll list
/// along; on the uGUI kit (UWKitRows) since cleanup 8b.1-8b.2, which brings its own.
///
/// OVER THE MAIN MENU (phase 2): the same bar folds out when the mouse reaches the top edge, with
/// the game folder and the detail levels; the Remastered effects (UWEffectsScreen) are a window of the
/// bar since cleanup 8d.3. Meanwhile UWMainMenu ignores the mouse (IsCapturingInput). Controls, text
/// and colour settings follow (Todo.md).
/// </summary>
public partial class UWSetupMenu : MonoBehaviour
{
    private const float BarHeight = 30f;

    /// <summary>
    /// THE BAR'S DIALOGS IN ONE STATE (cleanup 8.1, 2026-10-24): one dialog at a time, so one value
    /// instead of a flag per dialog. Until 2026-10-11 Controls and Colours could be open at once
    /// and the Motion item left the palette dialog standing (fCloseDialogs); the Display and Sound
    /// items still left the folder dialog open under them until this enum.
    ///
    /// GAME AND GRAPHICS ARE WINDOWS TOO (per user, 2026-10-24, cleanup step 8.4): until then the
    /// bar's two drop-down menus, which could lie over an open dialog and closed on a click outside
    /// them; now they open, close and step like the other items' dialogs, one at a time.
    /// </summary>
    private enum DialogEnum
    {
        None,
        Folder,
        Controls,
        Colours,
        Sound,
        Motion,
        Display,
        PaletteEffects,
        Game,
        Graphics,

        /// <summary>The Remastered effects (UWEffectsScreen): a window of the bar since cleanup 8d.3
        /// (2026-10-25), until then a 320x200 picture of its own over everything.</summary>
        RemasteredEffects
    }

    /// <summary>From this height at the top of the screen (reference units) the bar folds out
    /// over the main menu.</summary>
    private const float RevealZone = 6f;

    private static UWSetupMenu msMainMenuInstance;

    /// <summary>The bar, a menu or a dialog has the mouse - the main menu must not react to
    /// clicks meanwhile (UWMainMenu).</summary>
    public static bool IsCapturingInput { get; private set; }

    /// <summary>The bar's Remastered effects window is up - UWBack: Escape, the pad's B and Start close
    /// it first (the chain's SetupEffects row), the bar's keyboard navigation's Escape waits for it.</summary>
    public static bool IsEffectsScreenOpen => msMainMenuInstance != null
        && msMainMenuInstance.meOpenDialog == DialogEnum.RemasteredEffects;

    /// <summary>
    /// THE BAR AND ITS WINDOWS ON THE KIT (cleanup 8b.1, 2026-10-24; phase 8b): the bar - with the
    /// first-run screen's ground and title - and the Sound window are drawn on the uGUI kit
    /// (UWKitRows) in LateUpdate, fed by the same page tables through a page drawer of their own.
    ///
    /// EVERY WINDOW ON THE KIT since cleanup 8b.2 (2026-10-24): Game, Motion, Graphics, Display,
    /// Controls, Colours, Palette effects, the folder dialog and the first-run screen followed Sound.
    /// Since cleanup 8b.4 the IMGUI form is gone (UWGuiRows, UWImguiKeyNav, the side-by-side switch):
    /// the bar has no OnGUI any more.
    ///
    /// THE ARROW KEYS STEER THE BAR AND ITS DIALOGS (per user 2026-10-12: a fallback for when the
    /// mouse cannot click them, as on Linux in a downscaled fullscreen; the kit's UWKitKeyNav since
    /// 8b.1). F10 over the main menu or an in-game menu shows the bar with the focus on its first
    /// item; the arrows step, Enter or Space presses, Escape or F10 again leaves.
    /// </summary>
    private UWKitRows mOKitRows;

    private UWSetupPageDrawer mOKitPages;

    /// <summary>
    /// THE DRAWER THE WINDOWS DRAW THROUGH (cleanup 8b.2, 2026-10-24): every window of the bar - its
    /// page and the rows it draws itself (the key lists, the flask preview, the folder's list and
    /// path, the view angle) - draws through these two, which served the kit's and the IMGUI form in
    /// turn; since cleanup 8b.4 the kit's (mOKitRows, mOKitPages), set in Awake.
    /// </summary>
    private IUWRows mOPassRows;

    private UWSetupPageDrawer mOPassPages;

    /// <summary>The bar's keyboard navigation is on - the modern game menu leaves the keys to it.</summary>
    public static bool KeyNavActive { get; private set; }

    /// <summary>The bar's keyboard navigation is on, asked now (KeyNavActive is put once a frame in
    /// Update) - UWBack: the bar is drawn over the game menu and the classic options, so its Escape
    /// is theirs no more (cleanup 7, wave 2b).</summary>
    public static bool IsKeyNavOn => msMainMenuInstance != null && msMainMenuInstance.mOKitRows != null
        && msMainMenuInstance.fKeyNavIsActive;

    /// <summary>One of the bar's menus or dialogs is open - the in-game menus under it stand
    /// back then (UWModernGameMenu, UWHudOptions).</summary>
    public static bool HasOpenPanel { get; private set; }

    /// <summary>
    /// THE BAR IN THE GAME (per user, 2026-10-06: "dann muss man nicht immer ins Hauptmenue"):
    /// while the modern scheme's game menu (Escape) or the classic options panel is open, the
    /// bar stays folded out over it, with everything it offers. Outside those, as before: only
    /// over the main menu, and folded out by the mouse at the top.
    /// </summary>
    private static bool fInGameMenuOpen()
    {
        UWModernHud lOModern = UWModernHud.Instance;

        if (lOModern != null && lOModern.Menu.ShowsMenuPage)
            return true;

        UWGameUI lOGameUi = UWScene.GameUi;

        return lOGameUi != null && lOGameUi.Classic.IsOptionsOpen;
    }

    private DataImport mOData;

    /// <summary>The Remastered effects window (cleanup 8d.3: the one, the bar's, whoever opens it).</summary>
    private readonly UWEffectsScreen mOEffects = new UWEffectsScreen();

    private bool mbBarShown;

    private bool mbFirstRun;

    private DialogEnum meOpenDialog = DialogEnum.None;

    private readonly UWGameFolderBrowser mOBrowser = new UWGameFolderBrowser();

    private List<string> mODrives = new List<string>();

    private string msTypedPath = string.Empty;

    private Vector2 mOScroll;

    private bool mbApplyPending;

    /// <summary>The bar over the real main menu (phase 2): hidden until the mouse reaches the top
    /// edge, with the game folder and the graphics detail. Called by UWMainMenu whenever its
    /// buttons show; creates the bar once per scene. (The main menu's frame it took until cleanup 8d.3
    /// was the effects screen's.)</summary>
    public static void EnsureForMainMenu(DataImport pOData)
    {
        if (msMainMenuInstance != null)
            return;

        GameObject lOObject = new GameObject("Menu Bar");

        msMainMenuInstance = lOObject.AddComponent<UWSetupMenu>();
        msMainMenuInstance.mOData = pOData;
    }

    /// <summary>
    /// THE BAR EXISTS IN THE GAME TOO (2026-10-06, per user: it was not visible in either scheme).
    /// Loading a save or starting a character reloads the scene, which destroyed the "Menu Bar"
    /// object made for the main menu, and nothing made it again - the bar only ever came back
    /// with the main menu. The level loader asks for it when a level is up.
    /// </summary>
    public static void EnsureInGame(DataImport pOData)
    {
        EnsureForMainMenu(pOData);
    }

    private void Awake()
    {
        mOKitRows = new UWKitRows(transform, UWUiSorting.KitWindows);
        // The bar's one text field is the folder path: any character a path may hold (cleanup 8b.2).
        mOKitRows.TypesAnyText = true;
        mOKitPages = new UWSetupPageDrawer(mOKitRows);
        mOPassRows = mOKitRows;
        mOPassPages = mOKitPages;
        fBuildPages();
    }

    private void OnDestroy()
    {
        if (msMainMenuInstance == this || mbFirstRun)
        {
            if (msMainMenuInstance == this)
                msMainMenuInstance = null;

            IsCapturingInput = false;
            HasOpenPanel = false;
        }

        // The effects window's values are saved when it closes - with the scene too.
        mOEffects.Close();

        fClearFlaskPreview();

        if (mOKitRows != null)
            mOKitRows.Dispose();
    }

    /// <summary>The first-run screen: no game data found.</summary>
    public static void ShowFirstRun()
    {
        GameObject lOObject = new GameObject("Setup Menu");
        UWSetupMenu lOMenu = lOObject.AddComponent<UWSetupMenu>();

        lOMenu.mbFirstRun = true;
        lOMenu.fOpenFolderDialog();
    }

    private void Update()
    {
        if (mbFirstRun)
        {
            // The pointer shows, out of any look mode (UWPointer writes the Cursor only on a change;
            // no game runs, so the system pointer is free).
            UWPointer.IsShown = true;
            UWPointer.IsLookLocked = false;

            // The folder dialog is a panel too: Tab must not open the help over it (seen on
            // Linux, 2026-10-06).
            IsCapturingInput = true;
            HasOpenPanel = true;
        }
        else
        {
            fUpdateOverMainMenu();
        }

        if (!mbApplyPending)
            return;

        // One frame after the click, so "Preparing the game data..." is on screen while the
        // image is extracted.
        mbApplyPending = false;
        fApplyFolder();
    }

    // ------------------------------------------------- Over the main menu

    /// <summary>Shows or hides the bar by the mouse, closes the effects window by the back keys, and
    /// tells the main menu whether it may use the mouse.</summary>
    private void fUpdateOverMainMenu()
    {
        bool lbInGameMenu = fInGameMenuOpen();

        if (!UWMainMenu.AcceptsMenuBar && !lbInGameMenu)
        {
            mbBarShown = false;
            meOpenDialog = DialogEnum.None;
            IsCapturingInput = false;
            HasOpenPanel = false;
            fLeaveKeyNav();

            return;
        }

        // THE EFFECTS WINDOW TAKES THE BACK KEYS FIRST (the chain's SetupEffects row): Escape, the pad's
        // B and Start close it, also over the game menu or the classic options, and its Escape is not the
        // keyboard navigation's (cleanup 7, wave 2b; since cleanup 8d.3 the one effects window, the bar's).
        if (meOpenDialog == DialogEnum.RemasteredEffects && UWBack.Takes(UWBackOwner.SetupEffects))
            meOpenDialog = DialogEnum.None;

        UnityEngine.InputSystem.Keyboard lOKeyboard = UnityEngine.InputSystem.Keyboard.current;

        if (lOKeyboard != null && lOKeyboard.f10Key.wasPressedThisFrame && !UWControls.IsShiftHeld)
        {
            if (fKeyNavIsActive)
                fLeaveKeyNav();
            else
            {
                mbBarShown = true;
                mOKitRows.KeyNav.Activate();
            }
        }
        else if (lOKeyboard != null && fKeyNavIsActive && UWBack.Takes(UWBackOwner.SetupKeyNav))
            fLeaveKeyNav();

        KeyNavActive = fKeyNavIsActive;

        // The pointer as the Input System reports it - right on Linux in a downscaled fullscreen
        // too, where IMGUI's was not (per user, 2026-10-12).
        float lfScale = UWGuiTheme.Scale;
        float lfMouseY = UWPointer.HasMouse ? (Screen.height - UWPointer.Position.y) / lfScale : UWGuiTheme.ReferenceHeight;

        bool lbSomethingOpen = meOpenDialog != DialogEnum.None;

        if (lfMouseY <= RevealZone || lbInGameMenu)
            mbBarShown = true;
        else if (!lbSomethingOpen && lfMouseY > BarHeight)
            mbBarShown = false;

        if (fKeyNavIsActive)
            mbBarShown = true;

        IsCapturingInput = lbSomethingOpen || fKeyNavIsActive || (mbBarShown && lfMouseY <= BarHeight);
        HasOpenPanel = lbSomethingOpen;
    }

    /// <summary>The bar's keyboard navigation is on.</summary>
    private bool fKeyNavIsActive => mOKitRows.KeyNav.IsActive;

    /// <summary>Leaves the keyboard navigation: the focus goes, the bar hides again unless the
    /// mouse holds it; an open dialog stays (Escape inside it is its own).</summary>
    private void fLeaveKeyNav()
    {
        mOKitRows.KeyNav.Reset();
        KeyNavActive = false;
    }

    /// <summary>
    /// A detail level of the Graphics window: Low, Medium or High, at once; the window stays, as a
    /// choice in any other dialog. CUSTOM (VeryHigh, the viewer's own values) is no choice there since
    /// cleanup 8d.3 (per user, 2026-10-25), only a text lit while it is in force: "Remastered
    /// effects..." beside "Palette effects..." sets it and opens the effects window.
    /// </summary>
    private void fChooseLevel(UWGraphicsDetail.LevelEnum peLevel)
    {
        // The one path with the classic options' detail screen (cleanup step 8.8, 2026-10-24).
        UWGraphicsDetail.Choose(peLevel, null, null);
    }

    /// <summary>The Graphics window's "Remastered effects...": the level Custom (VeryHigh; the shipped
    /// values become the viewer's own the first time, UWGraphicsDetail.SetLevel) and the effects window
    /// in place of the Graphics window - what Very high did until cleanup 8d.3.</summary>
    private void fOpenRemasteredEffects()
    {
        UWGraphicsDetail.Choose(UWGraphicsDetail.LevelEnum.VeryHigh, null, fOpenEffectsWindow);
    }

    /// <summary>
    /// THE CLASSIC OPTIONS' VERY HIGH (cleanup 8d.3, per user 2026-10-25): the options set the level
    /// and open the bar's effects window, the bar with it - they keep no effects screen of their own any
    /// more. Nothing without the bar (it exists in the game, EnsureInGame).
    /// </summary>
    public static void OpenEffectsWindow()
    {
        if (msMainMenuInstance != null)
            msMainMenuInstance.fOpenEffectsWindow();
    }

    /// <summary>The effects window as the bar's one open window, the bar shown over it.</summary>
    private void fOpenEffectsWindow()
    {
        fOpenDialog(DialogEnum.RemasteredEffects);
        mOEffects.Open();
        mbBarShown = true;
    }

    /// <summary>The effects window saves when it is no longer the open window, whatever closed it: Done,
    /// the back keys, another window of the bar, the bar going with the in-game menus.</summary>
    private void fCloseEffectsWhenGone()
    {
        if (mOEffects.IsOpen && meOpenDialog != DialogEnum.RemasteredEffects)
            mOEffects.Close();
    }

    /// <summary>The effects window (UWEffectsScreen.Draw) under the bar; Done closes it.</summary>
    private void fDrawRemasteredEffects(float pfWidth)
    {
        if (mOEffects.Draw(mOKitRows, mOKitPages, pfWidth, BarHeight))
            meOpenDialog = DialogEnum.None;
    }

    // ------------------------------------------------- Colours

    private void fOpenColoursDialog()
    {
        fOpenDialog(DialogEnum.Colours);
    }

    /// <summary>Every dialog of the bar shut, so that the one opened next is the only one (until
    /// 2026-10-11 Controls and Colours could be open at once, and the Motion item left the palette
    /// dialog standing).</summary>
    private void fCloseDialogs()
    {
        meOpenDialog = DialogEnum.None;
    }

    /// <summary>Opens the dialog as the only one.</summary>
    private void fOpenDialog(DialogEnum peDialog)
    {
        fCloseDialogs();
        meOpenDialog = peDialog;
    }

    /// <summary>A bar item's click: its dialog closes when it is open, else opens as the only one.
    /// </summary>
    private void fToggleDialog(DialogEnum peDialog)
    {
        if (meOpenDialog == peDialog)
            meOpenDialog = DialogEnum.None;
        else
            fOpenDialog(peDialog);
    }

    /// <summary>The colour rows: the label 140 wide, the slider from 150 over the row less 230, the
    /// value with two decimals in the last 70; no arrows. The limits are the dialog's own (0 to 1,
    /// 0.5 to 2): the settings table keeps none for the colour entries.</summary>
    private static readonly UWSliderColumns ColourColumns = new UWSliderColumns(0f, 140f, 150f, 230f, 0f, 70f, 70f);

    /// <summary>
    /// The colour help (phase 5, per user, 2026-09-17): which deficiency to correct for, how
    /// strongly, plus brightness and contrast, and the hatched flask while poisoned. Everything
    /// takes effect at once - the palette table is rebuilt, the render pipeline follows the same
    /// matrix (see UWColourVision). The modes' texts are the settings table's (ColourMode).
    /// </summary>
    private void fDrawColoursDialog(float pfWidth)
    {
        // On the kit (cleanup 8b.2) as wide as its rows need - the four modes' texts in the pseudo-
        // language ran out of it - and as tall, the flask preview's 102 and a gap under the rows.
        Rect lODialog = mOPassRows.Dialog(pfWidth, fMaxWidth(680f, mOPassPages.InnerNeeded(mOColoursPage)), 150f, fHeight(420f, 110f));

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 16f;

        // The modes, the strength, brightness and contrast, the hatch switch and its hint: the
        // page (fBuildColoursPage); a change takes effect at once through UWColourVision.
        fContentEnds(lODialog, mOPassPages.Draw(mOColoursPage, lfLeft, lfTop, lfInner));

        // Bottom edge level with the Done button (per user, 2026-09-17).
        fDrawFlaskPreview(lfLeft, lODialog.yMax - 16f - 20f - (33f * 2f));

        if (mOPassRows.Done(lODialog))
            meOpenDialog = DialogEnum.None;
    }

    /// <summary>
    /// The three flasks as they will look in the game (per user, 2026-09-17): health, health
    /// while poisoned - hatched when that switch is on - and mana. They are built through the
    /// same path as the pictures in the game (UWScreenUi.BuildTexture), so they carry the
    /// current correction.
    /// </summary>
    private void fDrawFlaskPreview(float pfLeft, float pfTop)
    {
        if (mOData == null || mOData.Flasks == null)
            return;

        bool lbHatch = UWUserSettings.MarkPoison;

        if (mOFlaskPreview == null || miPreviewVersion != UWColourVision.Version || mbPreviewHatch != lbHatch)
        {
            fClearFlaskPreview();

            FilterMode leFilter = UWSettings.Instance != null
                ? UWSettings.Instance.TextureFilterMode : FilterMode.Point;

            mOFlaskPreview = new Texture2D[3];
            // Index 3 is the panel stone around the flask - cut away, so it stands on the dark
            // dialog (per user, 2026-09-17).
            mOFlaskPreview[0] = UWScreenUi.BuildTexture(mOData.Flasks.GetFlask(UWFlasks.FlaskTypeEnum.Red, 1f), leFilter, true, FlaskBackgroundIndex);
            mOFlaskPreview[1] = UWScreenUi.BuildTexture(mOData.Flasks.GetFlask(UWFlasks.FlaskTypeEnum.Green, 1f, lbHatch), leFilter, true, FlaskBackgroundIndex);
            mOFlaskPreview[2] = UWScreenUi.BuildTexture(mOData.Flasks.GetFlask(UWFlasks.FlaskTypeEnum.Blue, 1f), leFilter, true, FlaskBackgroundIndex);

            miPreviewVersion = UWColourVision.Version;
            mbPreviewHatch = lbHatch;
        }

        string[] lsNames = msFlaskNameIds;

        // On the kit the names as wide as the widest needs, the flasks as far apart (cleanup 8b.2: a
        // longer name wrapped below its line); 76 wide every 80 as laid out.
        float lfShift = 0f;

        if (mOPassRows.SizesByText)
        {
            foreach (string lsName in lsNames)
                lfShift = Mathf.Max(lfShift, UWRowSizing.LabelShift(mOPassRows.TextWidth(UWText.T(lsName), UWRowInfoEnum.Dim), 76f));
        }

        for (int liAt = 0; liAt < mOFlaskPreview.Length; liAt++)
        {
            Texture2D lOTexture = mOFlaskPreview[liAt];

            if (lOTexture == null)
                continue;

            float lfLeft = pfLeft + (liAt * (80f + lfShift));

            mOPassRows.Picture(new Rect(lfLeft, pfTop, lOTexture.width * 2f, lOTexture.height * 2f), lOTexture);
            mOPassRows.Info(new Rect(lfLeft, pfTop + (lOTexture.height * 2f) + 2f, 76f + lfShift, 22f), UWText.T(lsNames[liAt]), UWRowInfoEnum.Dim);
        }
    }

    private static readonly string[] msFlaskNameIds = { "setup.colours.flask.health", "setup.colours.flask.poisoned", "setup.colours.flask.mana" };

    private void fClearFlaskPreview()
    {
        if (mOFlaskPreview == null)
            return;

        foreach (Texture2D lOTexture in mOFlaskPreview)
        {
            if (lOTexture != null)
                Destroy(lOTexture);
        }

        mOFlaskPreview = null;
    }

    /// <summary>The colour of the panel behind the flask pictures.</summary>
    private const int FlaskBackgroundIndex = 3;

    private Texture2D[] mOFlaskPreview;

    private int miPreviewVersion = -1;

    private bool mbPreviewHatch;

    private void fSetColours(int piMode, float pfStrength, float pfBrightness, float pfContrast)
    {
        UWColourVision.Set((UWDataImport.UWData.UWColourFilter.ModeEnum)piMode, pfStrength, pfBrightness, pfContrast);
    }

    // ------------------------------------------------- Controls

    /// <summary>Which key is being waited for, or null (phase 3).</summary>
    private UWKeyBindings.Entry mOListeningFor;

    private string msBindingNotice = string.Empty;

    private Vector2 mOControlsScroll;

    private void fOpenControlsDialog()
    {
        fOpenDialog(DialogEnum.Controls);
        mOListeningFor = null;
        msBindingNotice = string.Empty;
    }

    /// <summary>Waits for the next key; Escape keeps the old one. A key already used elsewhere is
    /// accepted, with a note saying where it also sits.</summary>
    private void fListenFor(UWKeyBindings.Entry pOEntry)
    {
        mOListeningFor = pOEntry;
        msBindingNotice = string.Empty;

        UWKeyBindings.Listen(psPath =>
        {
            mOListeningFor = null;

            if (psPath == null)
                return;

            UWKeyBindings.Entry lOConflict = UWKeyBindings.FindConflict(pOEntry, psPath);

            UWKeyBindings.Set(pOEntry, psPath);

            msBindingNotice = lOConflict != null
                ? UWText.F("setup.controls.conflict_key", lOConflict.Label)
                : string.Empty;
        });
    }

    /// <summary>The Controls dialog's slider rows (the easy movement, the mouse's and the stick's look
    /// speed): the label 220 wide, the slider from 230 over the row less 400, the arrows 160 and the
    /// value 95 from the right edge.</summary>
    private static readonly UWSliderColumns ControlColumns = new UWSliderColumns(0f, 220f, 230f, 400f, 160f, 95f, 95f);

    /// <summary>
    /// The inner width the Controls window needs on the kit (cleanup 8b.2, 2026-10-24): both tabs'
    /// pages and both lists of keys - each name beside its button, the button as wide as "Press a key
    /// or button..." - so the window keeps one width when the tab changes or a key is awaited; 0 in the
    /// IMGUI form. A list's names have its width less 20 (the scroll bar) less 180 (the button) on the
    /// keyboard tab, less 220 (the glyph too) on the gamepad tab.
    /// </summary>
    private float fControlsInnerNeeded()
    {
        IUWRows lORows = mOPassRows;

        if (!lORows.SizesByText)
            return 0f;

        float lfNeeded = Mathf.Max(mOPassPages.InnerNeeded(mOControlsTabsPage),
            Mathf.Max(mOPassPages.InnerNeeded(mOKeyboardPage), mOPassPages.InnerNeeded(mOGamepadPage)));
        float lfKey = lORows.FitWidth(UWText.T("setup.controls.press_key"), 160f) - 160f;
        float lfButton = lORows.FitWidth(UWText.T("setup.gamepad.press_button"), 160f) - 160f;

        foreach (UWKeyBindings.Entry lOEntry in UWKeyBindings.Entries)
            lfNeeded = Mathf.Max(lfNeeded, lORows.LabelWidth(lOEntry.Label) + 200f + lfKey);

        foreach (UWKeyBindings.Entry lOEntry in UWKeyBindings.PadEntries)
            lfNeeded = Mathf.Max(lfNeeded, lORows.LabelWidth(lOEntry.Label) + 240f + lfButton);

        return lfNeeded;
    }

    /// <summary>
    /// THE CONTROLS WINDOW AS TALL AS THE SCREEN ALLOWS (cleanup 8d.4, per user 2026-10-25: more of the
    /// keys to bind at once): from 8 under the bar to 8 above the screen's bottom edge, as the palette
    /// and the Remastered effects windows stand (90 to 630 until then). The layout is always 720 lines
    /// high (UWGuiTheme.ReferenceHeight), so a small window shows the same rows smaller; the lists of
    /// keys take what the window gained and scroll inside as before.
    /// </summary>
    private const float ControlsTop = BarHeight + 8f;

    private const float ControlsHeight = UWGuiTheme.ReferenceHeight - 8f - ControlsTop;

    private void fDrawControlsDialog(float pfWidth)
    {
        Rect lODialog = mOPassRows.Dialog(pfWidth, fMaxWidth(680f, fControlsInnerNeeded()), ControlsTop, ControlsHeight);

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 16f;

        // The two tabs (fBuildControlsTabsPage), then the tab's rows down to its list of keys.
        lfTop = mOPassPages.Draw(mOControlsTabsPage, lfLeft, lfTop, lfInner);

        if (mbControlsGamepadTab)
        {
            fDrawGamepadTab(lODialog, lfLeft, lfInner, lfTop);
            return;
        }

        lfTop = mOPassPages.Draw(mOKeyboardPage, lfLeft, lfTop, lfInner);

        Rect lOListRect = new Rect(lfLeft, lfTop, lfInner, lODialog.yMax - 90f - lfTop);
        Rect lOContent = new Rect(0f, 0f, lOListRect.width - 20f, UWKeyBindings.Entries.Count * 28f);

        mOControlsScroll = mOPassRows.BeginScroll(lOListRect, mOControlsScroll, lOContent);

        for (int liAt = 0; liAt < UWKeyBindings.Entries.Count; liAt++)
        {
            UWKeyBindings.Entry lOEntry = UWKeyBindings.Entries[liAt];
            string lsKey = mOListeningFor == lOEntry ? UWText.T("setup.controls.press_key") : UWBindingInfo.Name(lOEntry);

            if (mOPassRows.Rebind(new Rect(0f, liAt * 28f, lOContent.width, 28f), lOEntry.Label, lsKey, null, false)
                && mOListeningFor == null)
                fListenFor(lOEntry);
        }

        mOPassRows.EndScroll();

        mOPassRows.Info(new Rect(lfLeft, lODialog.yMax - 82f, lfInner, 22f), msBindingNotice, UWRowInfoEnum.Dim);

        float lfButtonTop = lODialog.yMax - 46f;

        mOPassRows.Enabled = UWKeyBindings.HasChanges;

        string lsDefault = UWText.T("setup.controls.back_to_default");

        if (mOPassRows.Action(new Rect(lfLeft, lfButtonTop, mOPassRows.FitWidth(lsDefault, 180f), 30f), lsDefault))
        {
            UWKeyBindings.ResetAll();
            msBindingNotice = string.Empty;
        }

        mOPassRows.Enabled = true;

        if (mOPassRows.Done(lODialog))
        {
            meOpenDialog = DialogEnum.None;
            mOListeningFor = null;
        }
    }

    // ------------------------------------------------- Folder dialog

    private void fOpenFolderDialog()
    {
        fOpenDialog(DialogEnum.Folder);

        mODrives = mOBrowser.GetDrives();
        mOBrowser.Open(UWSettings.GetCandidateGogPaths(), UWUserSettings.GogInstallPath);
        fAfterBrowse();
    }

    private void fBrowse(string psPath)
    {
        mOBrowser.Browse(psPath);
        fAfterBrowse();
    }

    private void fAfterBrowse()
    {
        msTypedPath = mOBrowser.CurrentPath;
        mOScroll = Vector2.zero;
    }

    /// <summary>Remembers the folder and checks that the data really comes out of it; then the
    /// scene starts again with the real launch.</summary>
    private void fApplyFolder()
    {
        UWUserSettings.GogInstallPath = mOBrowser.CurrentPath;

        string lsData = UWSettings.AutoDetectDataPath();

        if (lsData == null)
        {
            mOBrowser.SetStatus(UWSettings.LastGogError ?? UWText.T("setup.folder.no_data"), true);

            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ------------------------------------------------- Drawing

    /// <summary>The bar is on the screen: over the main menu only while it shows its buttons or the
    /// save list, over the in-game menus, and only when folded out - over the effects window too since
    /// cleanup 8d.3 (until then a picture of its own over everything, the bar hidden). Always on the
    /// first-run screen.</summary>
    private bool fBarDrawn()
    {
        return mbFirstRun || (mbBarShown && (UWMainMenu.AcceptsMenuBar || fInGameMenuOpen()));
    }

    /// <summary>
    /// THE KIT'S FRAME (cleanup 8b.1, 2026-10-24): after Update decided what shows, the bar - on the
    /// first-run screen with its ground and title - and the open window on the kit, in the order IMGUI
    /// drew them, the kit's navigation counting their entries in that order. The arrow keys leave the
    /// debug tools' tab alone (UWDebugTools.IsTyping; its fields are on the debug panel's own kit drawer
    /// since cleanup 8c - until 8b.4 any IMGUI field with the keyboard, GUIUtility.keyboardControl).
    /// </summary>
    private void LateUpdate()
    {
        fCloseEffectsWhenGone();

        bool lbShown = fBarDrawn();

        // The arrow keys only after F10 (or in the first-run screen, which has nothing else).
        mOKitRows.Begin(lbShown, mOKitRows.KeyNav.IsActive || mbFirstRun, mbFirstRun, UWDebugTools.IsTyping);

        if (!lbShown)
            return;

        float lfWidth = mOKitRows.Width;

        if (mbFirstRun)
            fDrawFirstRunGround(mOKitRows, lfWidth);

        fDrawBar(mOKitRows, lfWidth);
        fDrawOpenDialog(lfWidth);

        mOKitRows.End();
    }

    /// <summary>The open window, through the windows' drawer.</summary>
    private void fDrawOpenDialog(float pfWidth)
    {
        switch (meOpenDialog)
        {
            case DialogEnum.Folder: fDrawFolderDialog(pfWidth); break;
            case DialogEnum.Controls: fDrawControlsDialog(pfWidth); break;
            case DialogEnum.Colours: fDrawColoursDialog(pfWidth); break;
            case DialogEnum.Sound: fDrawSoundDialog(pfWidth); break;
            case DialogEnum.Motion: fDrawMotionDialog(pfWidth); break;
            case DialogEnum.Display: fDrawDisplayDialog(pfWidth); break;
            case DialogEnum.PaletteEffects: fDrawPaletteEffectsDialog(pfWidth); break;
            case DialogEnum.Game: fDrawGameDialog(pfWidth); break;
            case DialogEnum.Graphics: fDrawGraphicsDialog(pfWidth); break;
            case DialogEnum.RemasteredEffects: fDrawRemasteredEffects(pfWidth); break;
        }
    }

    // ------------------------------------------------- A window sized by its rows on the kit

    /// <summary>The window whose rows ended at mfContentHeight below its top in its last pass.</summary>
    private DialogEnum meContentOf = DialogEnum.None;

    private float mfContentHeight;

    /// <summary>
    /// THE WINDOWS GROW WITH THEIR TEXT ON THE KIT (cleanup 8b.2, 2026-10-24; the rule of phase 8b that
    /// the pseudo-language must not clip): a window is as wide as laid out (pfMaxWidth), or wider by what
    /// its rows need (pfInnerNeeded, UWSetupPageDrawer.InnerNeeded; the 20 margins on both sides added)
    /// where the screen allows. The IMGUI form and English need nothing more.
    /// </summary>
    private static float fMaxWidth(float pfMaxWidth, float pfInnerNeeded)
    {
        return Mathf.Max(pfMaxWidth, pfInnerNeeded + 40f);
    }

    /// <summary>A window's height: as laid out, or on the kit down to where its rows ended in its last
    /// pass and pfBelow under them (what hangs from its bottom edge: Done, the flask preview) - the
    /// pseudo-language's longer hints pushed Sound's MT-32 line onto Done. One pass late, as the rows
    /// are measured while they are drawn.</summary>
    private float fHeight(float pfHeight, float pfBelow)
    {
        if (!mOPassRows.SizesByText || meContentOf != meOpenDialog)
            return pfHeight;

        return Mathf.Max(pfHeight, mfContentHeight + pfBelow);
    }

    /// <summary>Where the open window's rows ended (pfBottom), for fHeight in its next pass.</summary>
    private void fContentEnds(Rect pODialog, float pfBottom)
    {
        meContentOf = meOpenDialog;
        mfContentHeight = pfBottom - pODialog.y;
    }

    /// <summary>The first-run screen's ground and title, under the bar.</summary>
    private static void fDrawFirstRunGround(IUWRows pORows, float pfWidth)
    {
        pORows.ScreenGround(new Rect(0f, 0f, pfWidth, UWGuiTheme.ReferenceHeight));

        pORows.BigTitle(new Rect(0f, 150f, pfWidth, 50f), "Underworld Revisited");
    }

    /// <summary>
    /// The bar's items left to right from 8: Game 70 wide, the others 90 each - on the kit as wide
    /// as their text needs where that is more (IUWRows.FitWidth, 2026-10-24: the pseudo-language ran
    /// them into each other), each following the one before.
    /// </summary>
    private void fDrawBar(IUWRows pORows, float pfWidth)
    {
        pORows.Bar(new Rect(0f, 0f, pfWidth, BarHeight));

        string lsGame = UWText.T("setup.bar.game");
        Rect lOGame = new Rect(8f, 0f, pORows.FitWidth(lsGame, 70f), BarHeight);

        if (pORows.BarItem(lOGame, lsGame, meOpenDialog == DialogEnum.Game))
            fToggleDialog(DialogEnum.Game);

        // Graphics needs the game data - not on the first-run screen. Controls do not, but there
        // is nothing to play yet either.
        if (mbFirstRun)
            return;

        float lfLeft = lOGame.xMax;

        // THE MOTION SECOND, right after Game (per user, 2026-10-06: "eine sehr wichtige
        // Einstellung"): the player's motion and the picture of it, with the explanations.
        if (fBarItem(pORows, ref lfLeft, "setup.bar.motion", meOpenDialog == DialogEnum.Motion))
            fToggleDialog(DialogEnum.Motion);

        if (fBarItem(pORows, ref lfLeft, "setup.bar.graphics", meOpenDialog == DialogEnum.Graphics))
            fToggleDialog(DialogEnum.Graphics);

        // Beside Graphics: window or fullscreen and the resolution (per user, 2026-09-26).
        if (fBarItem(pORows, ref lfLeft, "setup.bar.display", meOpenDialog == DialogEnum.Display))
            fToggleDialog(DialogEnum.Display);

        // Closed from the bar, a key being waited for stays awaited (as before; Done drops it).
        if (fBarItem(pORows, ref lfLeft, "setup.bar.controls", meOpenDialog == DialogEnum.Controls))
        {
            if (meOpenDialog == DialogEnum.Controls)
                meOpenDialog = DialogEnum.None;
            else
                fOpenControlsDialog();
        }

        if (fBarItem(pORows, ref lfLeft, "setup.bar.sound", meOpenDialog == DialogEnum.Sound))
            fToggleDialog(DialogEnum.Sound);

        if (fBarItem(pORows, ref lfLeft, "setup.bar.colours", meOpenDialog == DialogEnum.Colours))
            fToggleDialog(DialogEnum.Colours);
    }

    /// <summary>One of the bar's items after Game, at pfLeft, 90 wide (more for a longer text on
    /// the kit); pfLeft moves to its right edge.</summary>
    private static bool fBarItem(IUWRows pORows, ref float pfLeft, string psTextId, bool pbOpen)
    {
        string lsText = UWText.T(psTextId);
        Rect lORect = new Rect(pfLeft, 0f, pORows.FitWidth(lsText, 90f), BarHeight);

        pfLeft = lORect.xMax;

        return pORows.BarItem(lORect, lsText, pbOpen);
    }

    // ------------------------------------------------- Motion

    /// <summary>
    /// THE MOTION DIALOG (2026-10-06, per user: the motion switches "brauchen auf jeden Fall eine
    /// Erklaerung", "ein eigener Menuepunkt"). The player's motion is UW.EXE's own code in its
    /// units (UWPlayerMotion on UWMotionCore); these rows choose how often it runs and how the
    /// picture follows it, and say what each choice does - above all that in the Original mode
    /// more frames a second do NOT move faster but a little slower, because every call rounds
    /// down (the user: "Mir ist nicht sofort klar, dass mehr fps weniger Bewegung bedeutet").
    /// The head bob and the weapon's jitter moved here from the Graphics menu the same day.
    /// Everything is read every frame and takes effect at once.
    /// </summary>
    private Vector2 mOMotionScroll;

    /// <summary>The motion dialog's content height of the last frame, for its scroll view.</summary>
    private float mfMotionContentHeight;

    /// <summary>The motion rows, measured from 8 left of the text and 16 wider (the panel the row
    /// helpers measured from until cleanup 8.4): the label at 8, 100 wide, the choices from 108 over
    /// the panel less 116 (fMotionChoice: the two-way rows, the modes, the ramp and fps rows).</summary>
    private static readonly UWChoiceColumns MotionChoiceColumns = new UWChoiceColumns(8f, 100f, 108f, 116f);

    /// <summary>The strength row under a Smooth mode: the label at 8, 100 wide, the slider from 108
    /// over the panel less 170, the value in the last 56; no arrows.</summary>
    private static readonly UWSliderColumns StrengthColumns = new UWSliderColumns(8f, 100f, 108f, 170f, 0f, 56f, 56f);

    private void fDrawMotionDialog(float pfWidth)
    {
        // On the kit as wide as its rows need (cleanup 8b.2); they stand 56 short of its width.
        Rect lODialog = mOPassRows.Dialog(pfWidth, fMaxWidth(680f, mOPassPages.InnerNeeded(mOMotionPage) + 16f), 30f, 680f);

        // THE CONTENT SCROLLS above the Done button (2026-10-15, when the swimming row came in
        // under the head bob and the weapon): with all three on Smooth and their sliders it no
        // longer fits the dialog. Coordinates inside are the view's own; the scroll bar shows
        // only when it is needed.
        Rect lOView = new Rect(lODialog.x, lODialog.y, lODialog.width, lODialog.height - 56f);

        mOMotionScroll = mOPassRows.BeginScroll(lOView, mOMotionScroll,
            new Rect(0f, 0f, lOView.width - 20f, Mathf.Max(lOView.height, mfMotionContentHeight)));

        // The rows (fBuildMotionPage) in the view's own coordinates, 20 from its left edge.
        float lfTop = mOPassPages.Draw(mOMotionPage, 20f, 16f, lODialog.width - 56f);

        mfMotionContentHeight = lfTop + 10f;

        mOPassRows.EndScroll();

        if (mOPassRows.Done(lODialog))
        {
            meOpenDialog = DialogEnum.None;
        }
    }

    // ------------------------------------------------- Display

    private Vector2 mODisplayScroll;

    /// <summary>The mode row: the label 110 wide, the two modes over the rest.</summary>
    private static readonly UWChoiceColumns DisplayModeColumns = new UWChoiceColumns(0f, 110f, 110f, 110f);

    /// <summary>
    /// Window or borderless fullscreen and the resolution (per user, 2026-09-26) - see
    /// UWDisplayMode. A click switches at once; the choice is kept for the next start.
    /// On the kit (cleanup 8b.2) as wide as its rows need, "Resolution" in the label column of "Mode"
    /// (UWSetupPage.ColumnLabels), the list beside it.
    /// </summary>
    private void fDrawDisplayDialog(float pfWidth)
    {
        Rect lODialog = mOPassRows.Dialog(pfWidth, fMaxWidth(560f, mOPassPages.InnerNeeded(mODisplayPage)), 110f, 470f);

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 16f;

        // In the editor the saved choice, in the game what is on screen (Alt+Enter changes it too);
        // read once, before the rows (fBuildDisplayPage), as the list below shows the same.
        meShownMode = Application.isEditor ? UWDisplayMode.SavedMode : UWDisplayMode.CurrentMode;
        mOShownSize = Application.isEditor ? UWDisplayMode.SavedResolution : UWDisplayMode.CurrentResolution;

        UWDisplayMode.ModeEnum leMode = meShownMode;
        Vector2Int lOSize = mOShownSize;

        lfTop = mOPassPages.Draw(mODisplayPage, lfLeft, lfTop, lfInner);

        float lfShift = mOPassPages.LabelShift(mODisplayPage);

        mOPassRows.Label(new Rect(lfLeft, lfTop, 110f + lfShift, 28f), UWText.T("setup.display.resolution"));

        List<Vector2Int> lOSizes = UWDisplayMode.GetResolutions(leMode);
        Vector2Int lODesktop = UWDisplayMode.Desktop;
        Rect lOList = new Rect(lfLeft + 110f + lfShift, lfTop, lfInner - 110f - lfShift, lODialog.yMax - 62f - lfTop);

        mODisplayScroll = mOPassRows.BeginScroll(lOList, mODisplayScroll,
            new Rect(0f, 0f, lOList.width - 20f, lOSizes.Count * 28f));

        for (int liAt = 0; liAt < lOSizes.Count; liAt++)
        {
            Vector2Int lOEntry = lOSizes[liAt];
            string lsText = lOEntry.x + " x " + lOEntry.y + (lOEntry == lODesktop ? "   " + UWText.T("setup.display.desktop") : string.Empty);

            if (mOPassRows.Item(new Rect(0f, liAt * 28f, lOList.width - 20f, 28f), lsText, lOEntry == lOSize)
                && lOEntry != lOSize)
                UWDisplayMode.Apply(leMode, lOEntry);
        }

        mOPassRows.EndScroll();

        if (mOPassRows.Done(lODialog))
            meOpenDialog = DialogEnum.None;
    }

    /// <summary>The size to keep when the mode changes. A window as large as the desktop would
    /// hide its title bar under the task bar, so from fullscreen at the desktop's size a window
    /// starts at the largest offered size that leaves a margin.</summary>
    private static Vector2Int fSizeForMode(UWDisplayMode.ModeEnum peMode, Vector2Int pOSize)
    {
        Vector2Int lODesktop = UWDisplayMode.Desktop;

        if (peMode != UWDisplayMode.ModeEnum.Window || pOSize != lODesktop)
            return pOSize;

        foreach (Vector2Int lOEntry in UWDisplayMode.GetResolutions(peMode))
        {
            if (lOEntry.x <= lODesktop.x * 0.85f && lOEntry.y <= lODesktop.y * 0.85f)
                return lOEntry;
        }

        return pOSize;
    }

    // ------------------------------------------------- Sound

    /// <summary>A volume row right of its switch: no label of its own, the slider over the row less
    /// 170, the arrows 160 and the value 95 from its right edge.</summary>
    private static readonly UWSliderColumns VolumeColumns = new UWSliderColumns(0f, 0f, 0f, 170f, 160f, 95f, 95f);

    /// <summary>
    /// Music and sound (per user, 2026-09-21). THE ORIGINAL HAS ONLY ON AND OFF, in the
    /// options panel of the button bar - an AdLib card was set up outside the game, so there
    /// was nothing else to offer. THE VOLUMES ARE OURS. The two switches are the same ones as
    /// in the game, both go through UWSoundOptions, so the panel in the game and this dialog
    /// always show the same thing and it survives a restart.
    ///
    /// THE DETAIL LEVELS of the original's panel are deliberately not repeated here: ours mean
    /// something else and sit under Graphics (per user, same day).
    ///
    /// ON THE KIT since cleanup 8b.1 (2026-10-24): drawn by the windows' drawer (until 8b.4 also the
    /// IMGUI one in the side-by-side form) - the same page. On the kit as wide and as tall as
    /// its rows need (cleanup 8b.2: the pseudo-language's hints pushed the MT-32 line onto Done).
    /// </summary>
    private void fDrawSoundDialog(float pfWidth)
    {
        Rect lODialog = mOPassRows.Dialog(pfWidth, fMaxWidth(680f, mOPassPages.InnerNeeded(mOSoundPage)), 150f, fHeight(450f, 54f));

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 16f;

        // The switches with their volumes, the music device, its hint and the MT-32 line
        // (fBuildSoundPage); the line is 26 high.
        fContentEnds(lODialog, mOPassPages.Draw(mOSoundPage, lfLeft, lfTop, lfInner) + 26f);

        if (mOPassRows.Done(lODialog))
            meOpenDialog = DialogEnum.None;
    }

    /// <summary>The factor row under the resolution switch: no label, the slider from 32 over the
    /// panel less 94, the value in the last 56.</summary>
    private static readonly UWSliderColumns FactorColumns = new UWSliderColumns(0f, 0f, 32f, 94f, 0f, 56f, 56f);

    private static readonly System.Func<float, string> msFactor = pfValue => Mathf.RoundToInt(pfValue) + "x";

    /// <summary>The view angle's row: the label at 8, 50 wide, the slider from 58 over the panel less
    /// 186, the value 122 from the right edge, 66 wide - "Orig." takes the last 56.</summary>
    private static readonly UWSliderColumns ViewAngleColumns = new UWSliderColumns(8f, 50f, 58f, 186f, 0f, 122f, 66f);

    private static readonly System.Func<float, string> msDegrees = pfValue => Mathf.RoundToInt(pfValue) + "°";

    private static readonly System.Func<float, string> msOriginalAngle = pfValue => UWText.T("setup.graphics.view_original");

    /// <summary>The multiple of the original's resolution, 1x to 4x (UWWorldResolution.Factor), on
    /// a slider in whole steps. While 4x is locked the slider ends at 3x and a line says why. The
    /// limits are UWWorldResolution's: the settings table keeps none for the factor.
    /// </summary>
    private void fWorldResolutionFactorRow(Rect pOPanel, float pfTop, bool pbMaxLocked)
    {
        int liMax = pbMaxLocked ? UWWorldResolution.MaxFactor - 1 : UWWorldResolution.MaxFactor;
        int liShown = Mathf.Min(UWWorldResolution.Factor, liMax);

        // No label of its own (per user, 2026-10-07): under the switch it is clear what it sets.
        float lfWanted = mOPassRows.Slider(new Rect(pOPanel.x, pfTop, pOPanel.width, 26f), FactorColumns, null,
            liShown, 1f, liMax, 1f, true, msFactor);

        int liWanted = Mathf.RoundToInt(lfWanted);

        if (liWanted != liShown)
            UWWorldResolution.Factor = liWanted;

        if (pbMaxLocked)
            mOPassRows.Label(new Rect(pOPanel.x + 8f, pfTop + 30f, pOPanel.width - 16f, 26f), "  " + UWText.T("setup.graphics.factor_locked"));
    }

    /// <summary>
    /// THE VIEW ANGLE (UWViewAngle; per user, 2026-10-10: a slider, one for both schemes): the
    /// horizontal angle of the 3D view in whole degrees, and "Orig." back to each scheme's own. At
    /// the original's the slider stands at the angle shown now and the value reads "Original".
    /// The limits are UWViewAngle's (the settings table keeps none for the angle).
    ///
    /// SIZED BY ITS TEXT ON THE KIT (cleanup 8b.2, 2026-10-24; the pseudo-language ran the label over
    /// the slider and "Original" under "Orig."): the label column, the value column (wide enough for
    /// "Original" and the widest angle) and "Orig." take what their texts need, the slider gives it up.
    /// </summary>
    private void fViewAngleRow(Rect pOPanel, float pfTop)
    {
        IUWRows lORows = mOPassRows;
        bool lbOriginal = UWViewAngle.IsOriginal;
        float lfShown = Mathf.Clamp(lbOriginal ? UWViewAngle.HorizontalOf(Camera.main) : UWViewAngle.Horizontal, UWViewAngle.Min, UWViewAngle.Max);
        string lsLabel = UWText.T("setting.ViewAngle");
        string lsOrig = UWText.T("setup.graphics.view_orig");
        // "Orig." is a list row, its text from the padding at its left: in English it fits its 48 with
        // a little room on the right, which FitWidth's padding on both sides would not leave.
        float lfOrigExtra = lORows.SizesByText ? Mathf.Max(0f, lORows.LabelWidth(lsOrig) + UWRowSizing.Padding + 2f - 48f) : 0f;
        UWSliderColumns lOColumns = ViewAngleColumns;

        if (lORows.SizesByText)
        {
            lOColumns = UWSetupPageDrawer.WithValueWidth(UWSetupPageDrawer.WithLabel(lORows, lOColumns, lsLabel),
                Mathf.Max(lORows.LabelWidth(msOriginalAngle(0f)), lORows.LabelWidth(msDegrees(UWViewAngle.Max))));

            // "Orig." grows to the left: the value and the slider's right end move along.
            lOColumns = new UWSliderColumns(lOColumns.LabelLeft, lOColumns.LabelWidth, lOColumns.SliderLeft, lOColumns.SliderLess + lfOrigExtra,
                0f, lOColumns.ValueAt + lfOrigExtra, lOColumns.ValueWidth);
        }

        float lfWanted = lORows.Slider(new Rect(pOPanel.x, pfTop, pOPanel.width, 26f), lOColumns, lsLabel,
            lfShown, UWViewAngle.Min, UWViewAngle.Max, 1f, false, lbOriginal ? msOriginalAngle : msDegrees);
        int liWanted = Mathf.RoundToInt(lfWanted);

        if (liWanted != Mathf.RoundToInt(lfShown))
            UWViewAngle.Horizontal = liWanted;

        lORows.Enabled = !lbOriginal;

        if (lORows.Item(new Rect(pOPanel.x + pOPanel.width - 56f - lfOrigExtra, pfTop, 48f + lfOrigExtra, 26f), lsOrig))
            UWViewAngle.Horizontal = 0f;

        lORows.Enabled = true;
    }
    // ------------------------------------------------- Graphics

    /// <summary>
    /// THE GRAPHICS WINDOW (per user, 2026-10-24; until then a drop-down menu of the bar): the four
    /// detail levels (see UWGraphicsDetail) as a row of choices, the palette renderer's own effects
    /// (a dialog of their own), and set apart the classic screen's widescreen, the view angle and
    /// the world at the original's resolution - the rows of fBuildGraphicsPage. The motion rows that
    /// followed in the menu (head bob, weapon jitter, the motion modes) moved to the Motion dialog
    /// of the bar on 2026-10-06 (per user: they need an explanation, "ein eigener Menuepunkt").
    /// On the kit since cleanup 8b.2 (2026-10-24), as wide and as tall as its rows need.
    /// </summary>
    private void fDrawGraphicsDialog(float pfWidth)
    {
        Rect lODialog = mOPassRows.Dialog(pfWidth, fMaxWidth(560f, mOPassPages.InnerNeeded(mOGraphicsPage)), 110f, fHeight(400f, 54f));

        fContentEnds(lODialog, mOPassPages.Draw(mOGraphicsPage, lODialog.x + 20f, lODialog.y + 16f, lODialog.width - 40f));

        if (mOPassRows.Done(lODialog))
            meOpenDialog = DialogEnum.None;
    }

    // ------------------------------------------------- Game

    /// <summary>
    /// THE GAME WINDOW (per user, 2026-10-24; until then a drop-down menu of the bar): the game
    /// folder, the scheme, the three switches of the program (fBuildGamePage), and Quit in the
    /// bottom left corner where the other dialogs keep Back to default - at once, as the menu's
    /// item quit. On the kit since cleanup 8b.2 (2026-10-24), as wide and as tall as its rows need,
    /// Quit as wide as its text.
    /// </summary>
    private void fDrawGameDialog(float pfWidth)
    {
        Rect lODialog = mOPassRows.Dialog(pfWidth, fMaxWidth(560f, mOPassPages.InnerNeeded(mOGamePage)), 150f, fHeight(280f, 54f));
        float lfLeft = lODialog.x + 20f;

        fContentEnds(lODialog, mOPassPages.Draw(mOGamePage, lfLeft, lODialog.y + 16f, lODialog.width - 40f));

        string lsQuit = UWText.T("setup.game.quit");

        if (mOPassRows.Action(new Rect(lfLeft, lODialog.yMax - 46f, mOPassRows.FitWidth(lsQuit, 180f), 30f), lsQuit))
            fQuit();

        if (mOPassRows.Done(lODialog))
            meOpenDialog = DialogEnum.None;
    }

    /// <summary>The scheme in force: the scene's in the game, else the one kept (-1: not chosen
    /// yet, none marked).</summary>
    private static int fSchemeIndex()
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;

        return lOScheme != null ? (int)lOScheme.Current : UWUserSettings.ControlScheme;
    }

    /// <summary>The same as Shift+F2 in the game (UWControlScheme.Choose), else kept for the next
    /// start.</summary>
    private static void fChooseScheme(int piScheme)
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;

        if (lOScheme != null)
            lOScheme.Choose((UWControlScheme.SchemeEnum)piScheme);
        else
        {
            UWUserSettings.ControlScheme = piScheme;
            UWUserSettings.Save();
        }
    }

    /// <summary>
    /// The game folder dialog, also the first-run screen's. On the kit since cleanup 8b.2 (2026-10-24):
    /// the path is typed into the kit's text field (UWTextEntry and UWTypedKeys, so typing works on
    /// Linux too; Enter goes there, the game's keys stay silent meanwhile), the status in its colours;
    /// Up, the drives' label and the two buttons as wide as their texts, the buttons' right edges kept.
    /// </summary>
    private void fDrawFolderDialog(float pfWidth)
    {
        Rect lODialog = mOPassRows.Dialog(pfWidth, fMaxWidth(760f, mOPassPages.InnerNeeded(mOFolderPage)), 210f, 480f);

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 16f;

        // The title, the hint, the folders found, the drives and the path (fBuildFolderPage).
        lfTop = mOPassPages.Draw(mOFolderPage, lfLeft, lfTop, lfInner);

        float lfListBottom = lODialog.yMax - 90f;
        Rect lOListRect = new Rect(lfLeft, lfTop, lfInner, lfListBottom - lfTop);

        mOPassRows.Backdrop(lOListRect);

        Rect lOContent = new Rect(0f, 0f, lOListRect.width - 20f, Mathf.Max(lOListRect.height, mOBrowser.Entries.Count * 24f));

        mOScroll = mOPassRows.BeginScroll(lOListRect, mOScroll, lOContent);

        string lsGoTo = null;

        for (int liAt = 0; liAt < mOBrowser.Entries.Count; liAt++)
        {
            string lsEntry = mOBrowser.Entries[liAt];

            if (mOPassRows.Item(new Rect(0f, liAt * 24f, lOContent.width, 24f), mOBrowser.GetDisplayName(lsEntry)))
                lsGoTo = lsEntry;
        }

        mOPassRows.EndScroll();

        if (lsGoTo != null)
            fBrowse(lsGoTo);

        UWRowInfoEnum leStatus = mOBrowser.StatusIsError ? UWRowInfoEnum.Error
            : (mOBrowser.CanUseCurrent ? UWRowInfoEnum.Accent : UWRowInfoEnum.Dim);

        mOPassRows.Info(new Rect(lfLeft, lODialog.yMax - 82f, lfInner, 22f),
            mbApplyPending ? UWText.T("setup.folder.preparing") : mOBrowser.Status, leStatus);

        float lfButtonTop = lODialog.yMax - 46f;
        string lsUse = UWText.T("setup.folder.use");
        float lfUseExtra = mOPassRows.FitWidth(lsUse, 180f) - 180f;

        mOPassRows.Enabled = mOBrowser.CanUseCurrent && !mbApplyPending;

        if (mOPassRows.Action(new Rect(lODialog.xMax - 200f - lfUseExtra, lfButtonTop, 180f + lfUseExtra, 30f), lsUse))
            mbApplyPending = true;

        mOPassRows.Enabled = true;

        string lsCancel = mbFirstRun ? UWText.T("setup.game.quit") : UWText.T("setup.folder.cancel");
        float lfCancelExtra = mOPassRows.FitWidth(lsCancel, 110f) - 110f;

        if (mOPassRows.Action(new Rect(lODialog.xMax - 320f - lfUseExtra - lfCancelExtra, lfButtonTop, 110f + lfCancelExtra, 30f), lsCancel))
        {
            if (mbFirstRun)
                fQuit();
            else
                meOpenDialog = DialogEnum.None;
        }
    }

    /// <summary>The folder dialog's path field, by name for Enter (IUWRows.EnterIn).</summary>
    private const string PathField = "PathField";

    /// <summary>The folders a search found, one list item each; a click browses there.</summary>
    private float fDrawFoundFolders(float pfLeft, float pfTop, float pfInner)
    {
        float lfTop = pfTop;

        foreach (string lsFound in mOBrowser.Found)
        {
            if (mOPassRows.Item(new Rect(pfLeft, lfTop, pfInner, 26f), UWText.F("setup.folder.found", lsFound)))
                fBrowse(lsFound);

            lfTop += 28f;
        }

        return lfTop;
    }

    /// <summary>A button per drive, so another drive is one click away (per user, 2026-09-17). On the
    /// kit "Drives:" as wide as its text and the buttons after it (cleanup 8b.2: the pseudo-language
    /// wrapped it onto the found folder above); the buttons are sized by their labels as before.</summary>
    private float fDrawDrives(float pfLeft, float pfTop, float pfInner)
    {
        float lfTop = pfTop;
        string lsDrives = UWText.T("setup.folder.drives");
        float lfShift = mOPassRows.SizesByText ? UWRowSizing.LabelShift(mOPassRows.TextWidth(lsDrives, UWRowInfoEnum.Dim), 64f) : 0f;

        mOPassRows.Info(new Rect(pfLeft, lfTop, 60f + lfShift, 26f), lsDrives, UWRowInfoEnum.Dim);

        float lfDriveLeft = pfLeft + 64f + lfShift;

        foreach (string lsDrive in mODrives)
        {
            // The label: a Windows letter, or on Linux "/", "Home" and the mount's name - the
            // button as wide as its label needs (UWGameFolderBrowser.GetDriveLabel).
            string lsLabel = UWGameFolderBrowser.GetDriveLabel(lsDrive);
            float lfButtonWidth = Mathf.Max(44f, 16f + (lsLabel.Length * 9f));

            if (lfDriveLeft + lfButtonWidth > pfLeft + pfInner)
                break;

            if (mOPassRows.Action(new Rect(lfDriveLeft, lfTop, lfButtonWidth, 26f), lsLabel))
                fBrowse(lsDrive);

            lfDriveLeft += lfButtonWidth + 6f;
        }

        lfTop += 32f;

        return lfTop;
    }

    /// <summary>The path: typed or pasted, Enter goes there; Up beside it.</summary>
    private float fDrawPathRow(float pfLeft, float pfTop, float pfInner)
    {
        float lfTop = pfTop;
        string lsUp = UWText.T("setup.folder.up");
        float lfUpExtra = mOPassRows.FitWidth(lsUp, 50f) - 50f;

        // Up as wide as its text on the kit, the field after it (cleanup 8b.2).
        if (mOPassRows.Action(new Rect(pfLeft, lfTop, 50f + lfUpExtra, 26f), lsUp))
        {
            mOBrowser.Up();
            fAfterBrowse();
        }

        msTypedPath = mOPassRows.TextField(new Rect(pfLeft + 56f + lfUpExtra, lfTop, pfInner - 56f - lfUpExtra, 26f), PathField, msTypedPath ?? string.Empty);

        if (mOPassRows.EnterIn(PathField))
            fBrowse(msTypedPath);

        lfTop += 32f;

        return lfTop;
    }

    private static void fQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
