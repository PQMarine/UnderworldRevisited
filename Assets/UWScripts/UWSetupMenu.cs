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
/// the original's pictures nor its fonts exist. Drawn with IMGUI, because the scene has no
/// EventSystem on purpose (see UWItemDrag) and IMGUI brings text field and scroll list along.
///
/// OVER THE MAIN MENU (phase 2): the same bar folds out when the mouse reaches the top edge, with
/// the game folder and the four detail levels; VERY HIGH opens UWEffectsScreen in the main menu's
/// frame. Meanwhile UWMainMenu ignores the mouse (IsCapturingInput). Controls, text and colour
/// settings follow (Todo.md).
/// </summary>
public partial class UWSetupMenu : MonoBehaviour
{
    private const float ReferenceHeight = 720f;

    private const float BarHeight = 30f;

    private static readonly Color BackgroundColour = new Color(0.05f, 0.05f, 0.06f, 1f);

    private static readonly Color BarColour = new Color(0.11f, 0.11f, 0.13f, 1f);

    private static readonly Color PanelColour = new Color(0.14f, 0.14f, 0.16f, 1f);

    private static readonly Color HoverColour = new Color(0.22f, 0.22f, 0.26f, 1f);

    /// <summary>A thin line between groups inside a menu.</summary>
    private static readonly Color SeparatorColour = new Color(0.30f, 0.30f, 0.34f, 1f);

    private static readonly Color TextColour = new Color(0.86f, 0.86f, 0.84f, 1f);

    private static readonly Color DimTextColour = new Color(0.55f, 0.55f, 0.55f, 1f);

    private static readonly Color AccentColour = new Color(0.80f, 0.64f, 0.34f, 1f);

    private enum MenuEnum
    {
        None,
        Game,
        Graphics,
        Controls
    }

    /// <summary>From this height at the top of the screen (reference units) the bar folds out
    /// over the main menu.</summary>
    private const float RevealZone = 6f;

    private static UWSetupMenu msMainMenuInstance;

    /// <summary>The bar, a menu or a dialog has the mouse - the main menu must not react to
    /// clicks meanwhile (UWMainMenu).</summary>
    public static bool IsCapturingInput { get; private set; }

    /// <summary>One of the bar's menus or dialogs is open - the in-game menus under it stand
    /// back then (UWModernHud, UWHudOptions).</summary>
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

        if (lOModern != null && lOModern.ShowsMenuPage)
            return true;

        UWGameUI lOGameUi = UWScene.GameUi;

        return lOGameUi != null && lOGameUi.IsOptionsOpen;
    }

    private DataImport mOData;

    private RectTransform mOFrame;

    private UWEffectsScreen mOEffects;

    /// <summary>The effects screen's own frame in the game (fChooseLevel), on a canvas above the
    /// game's interfaces.</summary>
    private RectTransform mOEffectsFrame;

    private const int EffectsCanvasOrder = 500;

    private bool mbBarShown;

    private GUIStyle mOCurrentItem;

    private GUIStyle mOListLabel;

    private GUIStyle mOToggle;

    private bool mbFirstRun;

    private MenuEnum meOpenMenu = MenuEnum.None;

    private bool mbFolderDialogOpen;

    private bool mbControlsDialogOpen;

    private bool mbColoursDialogOpen;

    private bool mbSoundDialogOpen;

    private bool mbMotionDialogOpen;

    private bool mbDisplayDialogOpen;

    private readonly UWGameFolderBrowser mOBrowser = new UWGameFolderBrowser();

    private List<string> mODrives = new List<string>();

    private string msTypedPath = string.Empty;

    private Vector2 mOScroll;

    private bool mbApplyPending;

    private GUIStyle mOLabel;

    private GUIStyle mODimLabel;

    private GUIStyle mOTitle;

    private GUIStyle mOBarItem;

    private GUIStyle mOListItem;

    private GUIStyle mOButton;

    private GUIStyle mOTextField;

    private Font mOFont;

    private readonly Dictionary<Color, Texture2D> mOTextures = new Dictionary<Color, Texture2D>();

    /// <summary>The bar over the real main menu (phase 2): hidden until the mouse reaches the top
    /// edge, with the game folder and the graphics detail. Called by UWMainMenu whenever its
    /// buttons show; creates the bar once per scene.</summary>
    public static void EnsureForMainMenu(DataImport pOData, RectTransform pOFrame)
    {
        if (msMainMenuInstance != null)
            return;

        GameObject lOObject = new GameObject("Menu Bar");

        msMainMenuInstance = lOObject.AddComponent<UWSetupMenu>();
        msMainMenuInstance.mOData = pOData;
        msMainMenuInstance.mOFrame = pOFrame;
    }

    /// <summary>
    /// THE BAR EXISTS IN THE GAME TOO (2026-10-06, per user: it was not visible in either scheme).
    /// Loading a save or starting a character reloads the scene, which destroyed the "Menu Bar"
    /// object made for the main menu, and nothing made it again - the bar only ever came back
    /// with the main menu. The level loader asks for it when a level is up; without the main
    /// menu's frame the effects screen of the Very high detail is not offered (fChooseLevel).
    /// </summary>
    public static void EnsureInGame(DataImport pOData)
    {
        EnsureForMainMenu(pOData, null);
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

        fClearFlaskPreview();
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
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

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

    /// <summary>Shows or hides the bar by the mouse, drives the effects screen, and tells the
    /// main menu whether it may use the mouse.</summary>
    private void fUpdateOverMainMenu()
    {
        if (mOEffects != null && mOEffects.IsOpen)
        {
            mOEffects.Update();
            IsCapturingInput = true;

            return;
        }

        bool lbInGameMenu = fInGameMenuOpen();

        if (!UWMainMenu.AcceptsMenuBar && !lbInGameMenu)
        {
            mbBarShown = false;
            meOpenMenu = MenuEnum.None;
            mbFolderDialogOpen = false;
            mbControlsDialogOpen = false;
            mbDisplayDialogOpen = false;
            mbColoursDialogOpen = false;
            mbMotionDialogOpen = false;
            mbPaletteEffectsDialogOpen = false;
            mbSoundDialogOpen = false;
            IsCapturingInput = false;
            HasOpenPanel = false;

            return;
        }

        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;
        float lfScale = Screen.height / ReferenceHeight;
        float lfMouseY = lOMouse != null ? (Screen.height - lOMouse.position.ReadValue().y) / lfScale : ReferenceHeight;

        bool lbSomethingOpen = meOpenMenu != MenuEnum.None || mbFolderDialogOpen || mbControlsDialogOpen
            || mbColoursDialogOpen || mbSoundDialogOpen || mbDisplayDialogOpen || mbMotionDialogOpen
            || mbPaletteEffectsDialogOpen;

        if (lfMouseY <= RevealZone || lbInGameMenu)
            mbBarShown = true;
        else if (!lbSomethingOpen && lfMouseY > BarHeight)
            mbBarShown = false;

        IsCapturingInput = lbSomethingOpen || (mbBarShown && lfMouseY <= BarHeight);
        HasOpenPanel = lbSomethingOpen;
    }

    private void fChooseLevel(UWGraphicsDetail.LevelEnum peLevel)
    {
        meOpenMenu = MenuEnum.None;
        mbBarShown = false;

        UWGraphicsDetail.SetLevel(peLevel);

        if (peLevel != UWGraphicsDetail.LevelEnum.VeryHigh || mOData == null)
            return;

        // IN THE GAME the bar has no frame of its own (EnsureInGame), and the Very high entry did
        // nothing there (per user, 2026-10-07). The game's 320x200 frame is hidden in the modern
        // scheme (per user the same day: the panel stayed invisible), so the effects screen gets a
        // canvas of its own, the 320x200 frame in the middle and no backdrop - the game stays
        // visible around it.
        RectTransform lOFrame = mOFrame;

        if (lOFrame == null)
        {
            if (mOEffectsFrame == null)
            {
                Canvas lOCanvas = UWScreenUi.CreateCanvas(transform, "Effects Screen Canvas", EffectsCanvasOrder, out mOEffectsFrame);
                Transform lOBackdrop = lOCanvas.transform.Find("Backdrop");

                if (lOBackdrop != null)
                    lOBackdrop.gameObject.SetActive(false);

                mOEffects = null;
            }

            lOFrame = mOEffectsFrame;
        }

        if (mOEffects == null)
            mOEffects = new UWEffectsScreen(mOData, lOFrame);

        mOEffects.Open();
    }

    // ------------------------------------------------- Colours

    private void fOpenColoursDialog()
    {
        mbColoursDialogOpen = true;
        mbSoundDialogOpen = false;
        mbDisplayDialogOpen = false;
        mbMotionDialogOpen = false;
        mbPaletteEffectsDialogOpen = false;
        meOpenMenu = MenuEnum.None;
    }

    /// <summary>
    /// The colour help (phase 5, per user, 2026-09-17): which deficiency to correct for, how
    /// strongly, plus brightness and contrast, and the hatched flask while poisoned. Everything
    /// takes effect at once - the palette table is rebuilt, the render pipeline follows the same
    /// matrix (see UWColourVision).
    /// </summary>
    private void fDrawColoursDialog(float pfWidth)
    {
        float lfDialogWidth = Mathf.Min(680f, pfWidth - 40f);
        Rect lODialog = new Rect((pfWidth - lfDialogWidth) / 2f, 150f, lfDialogWidth, 420f);

        fFill(lODialog, PanelColour);

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 16f;

        GUI.Label(new Rect(lfLeft, lfTop, lfInner, 26f), "Colours", mOLabel);
        lfTop += 28f;

        lfTop += fDrawHint(lfLeft, lfTop, lfInner,
            "Pulls apart the colours that are hard to tell apart, for the whole game. It is the usual daltonisation: what the eye loses is put back into the channels it can see.") + 8f;

        string[] lsModes = { "Off", "Red (protanopia)", "Green (deuteranopia)", "Blue (tritanopia)" };
        int liMode = (int)UWColourVision.Mode;

        for (int liAt = 0; liAt < lsModes.Length; liAt++)
        {
            if (GUI.Button(new Rect(lfLeft + (liAt * 160f), lfTop, 155f, 28f), lsModes[liAt],
                liAt == liMode ? mOCurrentItem : mOButton))
                fSetColours(liAt, UWColourVision.Strength, UWColourVision.Brightness, UWColourVision.Contrast);
        }

        lfTop += 40f;

        float lfStrength = fColourSlider(lfLeft, lfTop, lfInner, "Strength", UWColourVision.Strength, 0f, 1f);
        lfTop += 34f;

        float lfBrightness = fColourSlider(lfLeft, lfTop, lfInner, "Brightness", UWColourVision.Brightness, 0.5f, 2f);
        lfTop += 34f;

        float lfContrast = fColourSlider(lfLeft, lfTop, lfInner, "Contrast", UWColourVision.Contrast, 0.5f, 2f);
        lfTop += 40f;

        if (!Mathf.Approximately(lfStrength, UWColourVision.Strength)
            || !Mathf.Approximately(lfBrightness, UWColourVision.Brightness)
            || !Mathf.Approximately(lfContrast, UWColourVision.Contrast))
            fSetColours(liMode, lfStrength, lfBrightness, lfContrast);

        bool lbMark = GUI.Toggle(new Rect(lfLeft, lfTop, lfInner, 26f), UWUserSettings.MarkPoison,
            "  Hatch the health flask while poisoned", mOToggle);

        if (lbMark != UWUserSettings.MarkPoison)
        {
            UWUserSettings.MarkPoison = lbMark;
            UWUserSettings.Save();
        }

        lfTop += 30f;

        fDrawHint(lfLeft, lfTop, lfInner,
            "The flask is red normally and green while poisoned - the one pair a red-green deficiency cannot tell apart.");

        // Bottom edge level with the Done button (per user, 2026-09-17).
        fDrawFlaskPreview(lfLeft, lODialog.yMax - 16f - 20f - (33f * 2f));

        if (GUI.Button(new Rect(lODialog.xMax - 130f, lODialog.yMax - 46f, 110f, 30f), "Done", mOButton))
            mbColoursDialogOpen = false;
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

        string[] lsNames = { "Health", "Poisoned", "Mana" };

        for (int liAt = 0; liAt < mOFlaskPreview.Length; liAt++)
        {
            Texture2D lOTexture = mOFlaskPreview[liAt];

            if (lOTexture == null)
                continue;

            float lfLeft = pfLeft + (liAt * 80f);

            GUI.DrawTexture(new Rect(lfLeft, pfTop, lOTexture.width * 2f, lOTexture.height * 2f), lOTexture);
            GUI.Label(new Rect(lfLeft, pfTop + (lOTexture.height * 2f) + 2f, 76f, 22f), lsNames[liAt], mODimLabel);
        }
    }

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

    private float fColourSlider(float pfLeft, float pfTop, float pfWidth, string psLabel,
        float pfValue, float pfMin, float pfMax)
    {
        GUI.Label(new Rect(pfLeft, pfTop, 140f, 26f), psLabel, mOListLabel);

        float lfResult = GUI.HorizontalSlider(new Rect(pfLeft + 150f, pfTop + 8f, pfWidth - 230f, 20f),
            pfValue, pfMin, pfMax);

        GUI.Label(new Rect(pfLeft + pfWidth - 70f, pfTop, 70f, 26f),
            lfResult.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), mOListLabel);

        return lfResult;
    }

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
        mbControlsDialogOpen = true;
        mbSoundDialogOpen = false;
        mbDisplayDialogOpen = false;
        mbMotionDialogOpen = false;
        mbPaletteEffectsDialogOpen = false;
        meOpenMenu = MenuEnum.None;
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
                ? "This key is also used for \"" + lOConflict.Label + "\"."
                : string.Empty;
        });
    }

    /// <summary>
    /// A dim explanatory text, as tall as its wrapped lines need; returns that height. In a fixed
    /// rect the style centres the lines on it, so a text that wraps to more lines than the rect
    /// holds runs up into the heading (per user, 2026-09-30: the hint of the key dialog).
    /// </summary>
    private float fDrawHint(float pfLeft, float pfTop, float pfWidth, string psText)
    {
        GUIContent lOContent = new GUIContent(psText);
        float lfHeight = mODimLabel.CalcHeight(lOContent, pfWidth);

        GUI.Label(new Rect(pfLeft, pfTop, pfWidth, lfHeight), lOContent, mODimLabel);

        return lfHeight;
    }

    private void fDrawControlsDialog(float pfWidth)
    {
        float lfDialogWidth = Mathf.Min(680f, pfWidth - 40f);
        Rect lODialog = new Rect((pfWidth - lfDialogWidth) / 2f, 90f, lfDialogWidth, 540f);

        fFill(lODialog, PanelColour);

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 16f;

        GUI.Label(new Rect(lfLeft, lfTop, lfInner, 26f), "Keyboard and mouse", mOLabel);
        lfTop += 28f;

        lfTop += fDrawHint(lfLeft, lfTop, lfInner,
            "Click an entry to change it, then press the new key or mouse button. Escape keeps the old one. The pointer movement itself is fixed, and the interface panels always use the physical mouse buttons. A gamepad follows later.") + 12f;

        // The easy movement repeats while a key or one of the three arrows under the compass
        // is held. The original's pace is too fast to aim with (per user, 2026-09-21: "one
        // overshoots the target very easily"), and whether that is the game or the DOSBox
        // cycles is open - so it is a setting here.
        float lfStep = UWDataImport.UWData.UWEasyMovement.RepeatStepSeconds;
        float lfBefore = UWUserSettings.EasyMovementInterval;

        GUI.Label(new Rect(lfLeft, lfTop, 220f, 26f), "Easy movement interval", mOListLabel);

        // The slider snaps to the same ten milliseconds as the two arrows beside it, so a round
        // value can be hit at all (per user, 2026-09-21: dragging never landed on 250).
        float lfInterval = GUI.HorizontalSlider(new Rect(lfLeft + 230f, lfTop + 8f, lfInner - 400f, 20f),
            lfBefore,
            UWDataImport.UWData.UWEasyMovement.MinRepeatSeconds,
            UWDataImport.UWData.UWEasyMovement.MaxRepeatSeconds);

        lfInterval = Mathf.Round(lfInterval / lfStep) * lfStep;

        if (GUI.Button(new Rect(lfLeft + lfInner - 160f, lfTop, 26f, 26f), "<", mOButton))
            lfInterval = lfBefore - lfStep;

        if (GUI.Button(new Rect(lfLeft + lfInner - 130f, lfTop, 26f, 26f), ">", mOButton))
            lfInterval = lfBefore + lfStep;

        GUI.Label(new Rect(lfLeft + lfInner - 95f, lfTop, 95f, 26f),
            Mathf.RoundToInt(lfInterval * 1000f) + " ms", mOListLabel);

        if (Mathf.RoundToInt(lfInterval * 1000f) != Mathf.RoundToInt(lfBefore * 1000f))
        {
            UWUserSettings.EasyMovementInterval = lfInterval;
            UWUserSettings.Save();
        }

        lfTop += 30f;

        // THE MOUSE LOOK'S SPEED of the modern scheme (per user, 2026-10-06: "sehr direkt"),
        // a factor on the tuned default in steps of five per cent, the same row as above.
        float lfSpeedStep = UWUserSettings.MouseLookSpeedStep;
        float lfSpeedBefore = UWUserSettings.MouseLookSpeed;

        GUI.Label(new Rect(lfLeft, lfTop, 220f, 26f), "Mouse look speed (modern)", mOListLabel);

        float lfSpeed = GUI.HorizontalSlider(new Rect(lfLeft + 230f, lfTop + 8f, lfInner - 400f, 20f),
            lfSpeedBefore, UWUserSettings.MinMouseLookSpeed, UWUserSettings.MaxMouseLookSpeed);

        lfSpeed = Mathf.Round(lfSpeed / lfSpeedStep) * lfSpeedStep;

        if (GUI.Button(new Rect(lfLeft + lfInner - 160f, lfTop, 26f, 26f), "<", mOButton))
            lfSpeed = lfSpeedBefore - lfSpeedStep;

        if (GUI.Button(new Rect(lfLeft + lfInner - 130f, lfTop, 26f, 26f), ">", mOButton))
            lfSpeed = lfSpeedBefore + lfSpeedStep;

        GUI.Label(new Rect(lfLeft + lfInner - 95f, lfTop, 95f, 26f),
            Mathf.RoundToInt(lfSpeed * 100f) + " %", mOListLabel);

        if (Mathf.RoundToInt(lfSpeed * 100f) != Mathf.RoundToInt(lfSpeedBefore * 100f))
        {
            UWUserSettings.MouseLookSpeed = lfSpeed;
            UWUserSettings.Save();
        }

        lfTop += 30f;

        Rect lOListRect = new Rect(lfLeft, lfTop, lfInner, lODialog.yMax - 90f - lfTop);
        Rect lOContent = new Rect(0f, 0f, lOListRect.width - 20f, UWKeyBindings.Entries.Count * 28f);

        mOControlsScroll = GUI.BeginScrollView(lOListRect, mOControlsScroll, lOContent);

        for (int liAt = 0; liAt < UWKeyBindings.Entries.Count; liAt++)
        {
            UWKeyBindings.Entry lOEntry = UWKeyBindings.Entries[liAt];
            float lfRowTop = liAt * 28f;

            GUI.Label(new Rect(6f, lfRowTop, lOContent.width - 180f, 26f), lOEntry.Label, mOListLabel);

            string lsKey = mOListeningFor == lOEntry ? "Press a key or button..." : UWKeyBindings.GetKeyName(lOEntry);

            if (GUI.Button(new Rect(lOContent.width - 170f, lfRowTop, 160f, 26f), lsKey, mOButton)
                && mOListeningFor == null)
                fListenFor(lOEntry);
        }

        GUI.EndScrollView();

        GUI.Label(new Rect(lfLeft, lODialog.yMax - 82f, lfInner, 22f), msBindingNotice, mODimLabel);

        float lfButtonTop = lODialog.yMax - 46f;

        GUI.enabled = UWKeyBindings.HasChanges;

        if (GUI.Button(new Rect(lfLeft, lfButtonTop, 180f, 30f), "Back to default", mOButton))
        {
            UWKeyBindings.ResetAll();
            msBindingNotice = string.Empty;
        }

        GUI.enabled = true;

        if (GUI.Button(new Rect(lODialog.xMax - 130f, lfButtonTop, 110f, 30f), "Done", mOButton))
        {
            mbControlsDialogOpen = false;
            mOListeningFor = null;
        }
    }

    // ------------------------------------------------- Folder dialog

    private void fOpenFolderDialog()
    {
        mbFolderDialogOpen = true;
        meOpenMenu = MenuEnum.None;

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
            mOBrowser.SetStatus(UWSettings.LastGogError ?? "The game data could not be read from this folder.", true);

            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ------------------------------------------------- Drawing

    private void OnGUI()
    {
        fEnsureStyles();

        float lfScale = Screen.height / ReferenceHeight;
        float lfWidth = Screen.width / lfScale;

        GUI.matrix = Matrix4x4.Scale(new Vector3(lfScale, lfScale, 1f));

        // Over the main menu only while it shows its buttons or the save list, and only when
        // folded out; the effects screen covers everything by itself.
        if (!mbFirstRun && (!mbBarShown || (!UWMainMenu.AcceptsMenuBar && !fInGameMenuOpen()) || (mOEffects != null && mOEffects.IsOpen)))
            return;

        if (mbFirstRun)
        {
            fFill(new Rect(0f, 0f, lfWidth, ReferenceHeight), BackgroundColour);

            GUI.Label(new Rect(0f, 150f, lfWidth, 50f), "Underworld Revisited", mOTitle);
        }

        fDrawBar(lfWidth);

        if (mbFolderDialogOpen)
            fDrawFolderDialog(lfWidth);

        if (mbControlsDialogOpen)
            fDrawControlsDialog(lfWidth);

        if (mbColoursDialogOpen)
            fDrawColoursDialog(lfWidth);

        if (mbSoundDialogOpen)
            fDrawSoundDialog(lfWidth);

        if (mbMotionDialogOpen)
            fDrawMotionDialog(lfWidth);

        if (mbDisplayDialogOpen)
            fDrawDisplayDialog(lfWidth);

        if (mbPaletteEffectsDialogOpen)
            fDrawPaletteEffectsDialog(lfWidth);

        if (meOpenMenu == MenuEnum.Game)
            fDrawGameMenu();

        if (meOpenMenu == MenuEnum.Graphics)
            fDrawGraphicsMenu();
    }

    private void fDrawBar(float pfWidth)
    {
        fFill(new Rect(0f, 0f, pfWidth, BarHeight), BarColour);

        Rect lOGame = new Rect(8f, 0f, 70f, BarHeight);

        if (fBarItem(lOGame, "Game", meOpenMenu == MenuEnum.Game))
            meOpenMenu = meOpenMenu == MenuEnum.Game ? MenuEnum.None : MenuEnum.Game;

        // Graphics needs the game data - not on the first-run screen. Controls do not, but there
        // is nothing to play yet either.
        if (mbFirstRun)
            return;

        // THE MOTION SECOND, right after Game (per user, 2026-10-06: "eine sehr wichtige
        // Einstellung"): the player's motion and the picture of it, with the explanations.
        if (fBarItem(new Rect(lOGame.xMax, 0f, 90f, BarHeight), "Motion", mbMotionDialogOpen))
        {
            mbMotionDialogOpen = !mbMotionDialogOpen;

            if (mbMotionDialogOpen)
            {
                meOpenMenu = MenuEnum.None;
                mbControlsDialogOpen = false;
                mbColoursDialogOpen = false;
                mbDisplayDialogOpen = false;
                mbSoundDialogOpen = false;
            }
        }

        if (fBarItem(new Rect(lOGame.xMax + 90f, 0f, 90f, BarHeight), "Graphics", meOpenMenu == MenuEnum.Graphics))
            meOpenMenu = meOpenMenu == MenuEnum.Graphics ? MenuEnum.None : MenuEnum.Graphics;

        // Beside Graphics: window or fullscreen and the resolution (per user, 2026-09-26).
        if (fBarItem(new Rect(lOGame.xMax + 180f, 0f, 90f, BarHeight), "Display", mbDisplayDialogOpen))
        {
            mbDisplayDialogOpen = !mbDisplayDialogOpen;

            if (mbDisplayDialogOpen)
            {
                meOpenMenu = MenuEnum.None;
                mbControlsDialogOpen = false;
                mbColoursDialogOpen = false;
                mbSoundDialogOpen = false;
                mbMotionDialogOpen = false;
                mbPaletteEffectsDialogOpen = false;
            }
        }

        if (fBarItem(new Rect(lOGame.xMax + 270f, 0f, 90f, BarHeight), "Controls", mbControlsDialogOpen))
        {
            if (mbControlsDialogOpen)
                mbControlsDialogOpen = false;
            else
                fOpenControlsDialog();
        }

        if (fBarItem(new Rect(lOGame.xMax + 360f, 0f, 90f, BarHeight), "Sound", mbSoundDialogOpen))
        {
            mbSoundDialogOpen = !mbSoundDialogOpen;

            if (mbSoundDialogOpen)
            {
                meOpenMenu = MenuEnum.None;
                mbControlsDialogOpen = false;
                mbColoursDialogOpen = false;
                mbDisplayDialogOpen = false;
                mbMotionDialogOpen = false;
                mbPaletteEffectsDialogOpen = false;
            }
        }

        if (fBarItem(new Rect(lOGame.xMax + 450f, 0f, 90f, BarHeight), "Colours", mbColoursDialogOpen))
        {
            if (mbColoursDialogOpen)
                mbColoursDialogOpen = false;
            else
                fOpenColoursDialog();
        }
    }

    // ------------------------------------------------- Sound

    /// <summary>
    /// Music and sound (per user, 2026-09-21). THE ORIGINAL HAS ONLY ON AND OFF, in the
    /// options panel of the button bar - an AdLib card was set up outside the game, so there
    /// was nothing else to offer. THE VOLUMES ARE OURS. The two switches are the same ones as
    /// in the game, both go through UWSoundOptions, so the panel in the game and this dialog
    /// always show the same thing and it survives a restart.
    ///
    /// THE DETAIL LEVELS of the original's panel are deliberately not repeated here: ours mean
    /// something else and sit under Graphics (per user, same day).
    /// </summary>
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
    private void fDrawMotionDialog(float pfWidth)
    {
        float lfDialogWidth = Mathf.Min(680f, pfWidth - 40f);
        Rect lODialog = new Rect((pfWidth - lfDialogWidth) / 2f, 30f, lfDialogWidth, 680f);

        fFill(lODialog, PanelColour);

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 16f;

        // The row helpers measure from a panel's left edge plus 8 and its width less 116.
        Rect lORows = new Rect(lfLeft - 8f, 0f, lfInner + 16f, 0f);

        GUI.Label(new Rect(lfLeft, lfTop, lfInner, 26f), "Motion", mOLabel);
        lfTop += 28f;

        lfTop += fDrawHint(lfLeft, lfTop, lfInner,
            "How you move. The game uses the movement code of the original. Here you choose how often it calculates and how the picture follows.") + 10f;

        bool lbSmooth = fTwoWayRow(lORows, lfTop, "Motion", UWUserSettings.MotionSmooth, "Original", "Smooth");

        if (lbSmooth != UWUserSettings.MotionSmooth)
        {
            UWUserSettings.MotionSmooth = lbSmooth;
            UWUserSettings.Save();
        }

        lfTop += 30f;
        lfTop += fDrawHint(lfLeft, lfTop, lfInner,
            "Original: calculates like the original, including its rounding. Smooth: the same rules without the rounding. Smooth moves the same at every frame rate and may be easier on the stomach.") + 10f;

        // THE SMOOTH RAMP (per user, 2026-10-06: "die Verzoegerung beim Anlaufen und Abbremsen
        // wirken dagegen"): how long starting and stopping take in Smooth. Greyed under Original
        // (per user: a setting without effect is greyed), as the fps row is under Smooth.
        GUI.enabled = UWUserSettings.MotionSmooth;
        GUI.Label(new Rect(lfLeft, lfTop, 100f, 26f), "Response", mOListLabel);

        int[] liRamps = UWUserSettings.SmoothRampChoices;
        float lfRampWidth = (lORows.width - 116f) / liRamps.Length;

        for (int liAt = 0; liAt < liRamps.Length; liAt++)
        {
            bool lbCurrent = UWUserSettings.SmoothRampTicks == liRamps[liAt];

            if (GUI.Button(new Rect(lORows.x + 108f + (liAt * lfRampWidth), lfTop, lfRampWidth, 26f), UWUserSettings.SmoothRampLabels[liAt],
                lbCurrent ? mOCurrentItem : mOListItem) && !lbCurrent)
            {
                UWUserSettings.SmoothRampTicks = liRamps[liAt];
                UWUserSettings.Save();
            }
        }

        lfTop += 30f;
        lfTop += fDrawHint(lfLeft, lfTop, lfInner,
            "How long it takes to get going and to stop in Smooth. 0.6 s is the original on a slow PC, 0.3 s on the PC that DOSBox at 30000 cycles stands for. Shorter feels more direct. Original uses its own frame rate for this.") + 10f;
        GUI.enabled = true;

        // The picture belongs to Original: Smooth is always even (greyed otherwise).
        GUI.enabled = !UWUserSettings.MotionSmooth;

        bool lbStepped = fTwoWayRow(lORows, lfTop, "Picture", UWUserSettings.MotionViewStepped, "Even", "Stepped");

        if (lbStepped != UWUserSettings.MotionViewStepped)
        {
            UWUserSettings.MotionViewStepped = lbStepped;
            UWUserSettings.Save();
        }

        lfTop += 30f;
        lfTop += fDrawHint(lfLeft, lfTop, lfInner,
            "Only for Original. Even: the picture moves a little with every screen refresh. Stepped: the picture only moves when the game has calculated, like on an old PC. Smooth is always even.") + 10f;
        GUI.enabled = true;

        GUI.enabled = !UWUserSettings.MotionSmooth;
        GUI.Label(new Rect(lfLeft, lfTop, 100f, 26f), "Original fps", mOListLabel);

        int[] liChoices = UWUserSettings.OriginalFrameChoices;
        float lfChoiceWidth = (lORows.width - 116f) / liChoices.Length;

        for (int liAt = 0; liAt < liChoices.Length; liAt++)
        {
            bool lbCurrent = UWUserSettings.OriginalFrameTicks == liChoices[liAt];
            string lsLabel = (256 / liChoices[liAt]).ToString();

            if (GUI.Button(new Rect(lORows.x + 108f + (liAt * lfChoiceWidth), lfTop, lfChoiceWidth, 26f), lsLabel,
                lbCurrent ? mOCurrentItem : mOListItem) && !lbCurrent)
            {
                UWUserSettings.OriginalFrameTicks = liChoices[liAt];
                UWUserSettings.Save();
            }
        }

        lfTop += 30f;
        lfTop += fDrawHint(lfLeft, lfTop, lfInner,
            "How often per second the Original mode calculates. More is not faster. The original rounds down after every calculation, so with 64 you move a bit slower when swimming or walking backwards, and you reach full speed sooner. With 16 it is the other way round. 32 is like the original in DOSBox at 30000 cycles. Smooth does not use this setting.") + 12f;
        GUI.enabled = true;

        fFill(new Rect(lfLeft, lfTop, lfInner, 1f), SeparatorColour);
        lfTop += 8f;

        // THE HEAD BOB AND THE WEAPON'S JITTER (UWHeadBobRules), three ways each (per user,
        // 2026-09-23): Off for everyone who cannot take it, Original, Smooth - ours - with a
        // strength slider under it.
        UWHeadBobRules.ModeEnum leBob = fMotionModeRow(lORows, lfTop, "Head bob", UWUserSettings.HeadBobMode);

        if (leBob != UWUserSettings.HeadBobMode)
        {
            UWUserSettings.HeadBobMode = leBob;
            UWUserSettings.Save();
        }

        lfTop += 30f;

        if (UWUserSettings.HeadBobMode == UWHeadBobRules.ModeEnum.Smooth)
        {
            float lfBobStrength = fMotionStrengthRow(lORows, lfTop, UWUserSettings.HeadBobStrength);

            if (lfBobStrength != UWUserSettings.HeadBobStrength)
            {
                UWUserSettings.HeadBobStrength = lfBobStrength;
                UWUserSettings.Save();
            }

            lfTop += 30f;
        }

        UWHeadBobRules.ModeEnum leJitter = fMotionModeRow(lORows, lfTop, "Weapon", UWUserSettings.WeaponJitterMode);

        if (leJitter != UWUserSettings.WeaponJitterMode)
        {
            UWUserSettings.WeaponJitterMode = leJitter;
            UWUserSettings.Save();
        }

        lfTop += 30f;

        if (UWUserSettings.WeaponJitterMode == UWHeadBobRules.ModeEnum.Smooth)
        {
            float lfJitterStrength = fMotionStrengthRow(lORows, lfTop, UWUserSettings.WeaponJitterStrength);

            if (lfJitterStrength != UWUserSettings.WeaponJitterStrength)
            {
                UWUserSettings.WeaponJitterStrength = lfJitterStrength;
                UWUserSettings.Save();
            }

            lfTop += 30f;
        }

        lfTop += fDrawHint(lfLeft, lfTop, lfInner,
            "The bobbing of the view and the weapon while walking: like the original, smoothed with your own strength, or off if it makes you uneasy.");

        if (GUI.Button(new Rect(lODialog.xMax - 130f, lODialog.yMax - 46f, 110f, 30f), "Done", mOButton))
            mbMotionDialogOpen = false;
            mbPaletteEffectsDialogOpen = false;
    }

    // ------------------------------------------------- Display

    private Vector2 mODisplayScroll;

    /// <summary>
    /// Window or borderless fullscreen and the resolution (per user, 2026-09-26) - see
    /// UWDisplayMode. A click switches at once; the choice is kept for the next start.
    /// </summary>
    private void fDrawDisplayDialog(float pfWidth)
    {
        float lfDialogWidth = Mathf.Min(560f, pfWidth - 40f);
        Rect lODialog = new Rect((pfWidth - lfDialogWidth) / 2f, 110f, lfDialogWidth, 470f);

        fFill(lODialog, PanelColour);

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 16f;

        GUI.Label(new Rect(lfLeft, lfTop, lfInner, 26f), "Display", mOLabel);
        lfTop += 28f;

        lfTop += Mathf.Max(40f, fDrawHint(lfLeft, lfTop, lfInner, Application.isEditor
                ? "In the editor the Game view decides the size - the choice is kept and takes effect in the built game."
                : "Takes effect at once and is kept for the next start.")) + 4f;

        // In the editor the saved choice, in the game what is on screen (Alt+Enter changes it too).
        UWDisplayMode.ModeEnum leMode = Application.isEditor ? UWDisplayMode.SavedMode : UWDisplayMode.CurrentMode;
        Vector2Int lOSize = Application.isEditor ? UWDisplayMode.SavedResolution : UWDisplayMode.CurrentResolution;

        GUI.Label(new Rect(lfLeft, lfTop, 110f, 28f), "Mode", mOListLabel);

        string[] lsModes = { "Window", "Borderless fullscreen" };
        float lfModeWidth = (lfInner - 110f) / lsModes.Length;

        for (int liMode = 0; liMode < lsModes.Length; liMode++)
        {
            Rect lOButton = new Rect(lfLeft + 110f + (liMode * lfModeWidth), lfTop, lfModeWidth, 28f);

            if (GUI.Button(lOButton, lsModes[liMode], liMode == (int)leMode ? mOCurrentItem : mOListItem)
                && liMode != (int)leMode)
                UWDisplayMode.Apply((UWDisplayMode.ModeEnum)liMode, fSizeForMode((UWDisplayMode.ModeEnum)liMode, lOSize));
        }

        lfTop += 40f;

        GUI.Label(new Rect(lfLeft, lfTop, 110f, 28f), "Resolution", mOListLabel);

        List<Vector2Int> lOSizes = UWDisplayMode.GetResolutions(leMode);
        Vector2Int lODesktop = UWDisplayMode.Desktop;
        Rect lOList = new Rect(lfLeft + 110f, lfTop, lfInner - 110f, lODialog.yMax - 62f - lfTop);

        mODisplayScroll = GUI.BeginScrollView(lOList, mODisplayScroll,
            new Rect(0f, 0f, lOList.width - 20f, lOSizes.Count * 28f));

        for (int liAt = 0; liAt < lOSizes.Count; liAt++)
        {
            Vector2Int lOEntry = lOSizes[liAt];
            string lsText = lOEntry.x + " x " + lOEntry.y + (lOEntry == lODesktop ? "   (desktop)" : string.Empty);

            if (GUI.Button(new Rect(0f, liAt * 28f, lOList.width - 20f, 28f), lsText,
                    lOEntry == lOSize ? mOCurrentItem : mOListItem)
                && lOEntry != lOSize)
                UWDisplayMode.Apply(leMode, lOEntry);
        }

        GUI.EndScrollView();

        if (GUI.Button(new Rect(lODialog.xMax - 130f, lODialog.yMax - 46f, 110f, 30f), "Done", mOButton))
            mbDisplayDialogOpen = false;
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

    private void fDrawSoundDialog(float pfWidth)
    {
        float lfDialogWidth = Mathf.Min(680f, pfWidth - 40f);
        Rect lODialog = new Rect((pfWidth - lfDialogWidth) / 2f, 150f, lfDialogWidth, 280f);

        fFill(lODialog, PanelColour);

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 16f;

        GUI.Label(new Rect(lfLeft, lfTop, lfInner, 26f), "Sound", mOLabel);
        lfTop += 28f;

        lfTop += fDrawHint(lfLeft, lfTop, lfInner,
            "Music and sound come from the emulated AdLib card, as in 1992. The original only knew on and off, and the same two switches sit in the options panel in the game; the volumes are ours. The two switches belong to the save game, as in the original - a save loaded here brings the state it was saved with, the volumes stay.") + 8f;

        bool lbMusic = GUI.Toggle(new Rect(lfLeft, lfTop, 220f, 26f), UWSoundOptions.MusicEnabled,
            "  Music", mOToggle);

        if (lbMusic != UWSoundOptions.MusicEnabled)
            UWSoundOptions.MusicEnabled = lbMusic;

        GUI.enabled = lbMusic;
        UWSoundOptions.MusicVolume = fVolumeSlider(lfLeft + 230f, lfTop, lfInner - 230f, UWSoundOptions.MusicVolume);
        GUI.enabled = true;

        lfTop += 38f;

        bool lbSound = GUI.Toggle(new Rect(lfLeft, lfTop, 220f, 26f), UWSoundOptions.SoundEnabled,
            "  Sound effects", mOToggle);

        if (lbSound != UWSoundOptions.SoundEnabled)
            UWSoundOptions.SoundEnabled = lbSound;

        GUI.enabled = lbSound;
        UWSoundOptions.SoundVolume = fVolumeSlider(lfLeft + 230f, lfTop, lfInner - 230f, UWSoundOptions.SoundVolume);
        GUI.enabled = true;

        lfTop += 44f;

        fDrawHint(lfLeft, lfTop, lfInner,
            "The General MIDI and MT-32 versions of the music are not built - they would need a soundfont or an MT-32 emulation.");

        if (GUI.Button(new Rect(lODialog.xMax - 130f, lODialog.yMax - 46f, 110f, 30f), "Done", mOButton))
            mbSoundDialogOpen = false;
    }

    /// <summary>One volume: a slider that snaps to the same five percent its two arrows move
    /// by, and the value in percent beside it. Returns what it now stands at.</summary>
    private float fVolumeSlider(float pfLeft, float pfTop, float pfWidth, float pfValue)
    {
        float lfStep = UWSoundOptions.VolumeStep;

        float lfResult = GUI.HorizontalSlider(new Rect(pfLeft, pfTop + 8f, pfWidth - 170f, 20f),
            pfValue, 0f, 1f);

        lfResult = Mathf.Round(lfResult / lfStep) * lfStep;

        if (GUI.Button(new Rect(pfLeft + pfWidth - 160f, pfTop, 26f, 26f), "<", mOButton))
            lfResult = pfValue - lfStep;

        if (GUI.Button(new Rect(pfLeft + pfWidth - 130f, pfTop, 26f, 26f), ">", mOButton))
            lfResult = pfValue + lfStep;

        lfResult = Mathf.Clamp01(lfResult);

        GUI.Label(new Rect(pfLeft + pfWidth - 95f, pfTop, 95f, 26f),
            Mathf.RoundToInt(lfResult * 100f) + " %", mOListLabel);

        return Mathf.RoundToInt(lfResult * 100f) == Mathf.RoundToInt(pfValue * 100f) ? pfValue : lfResult;
    }

    /// <summary>A row of the Graphics menu with a label and the three buttons Off, Original
    /// and Smooth, the current one in the accent colour. Returns the mode it now stands at.
    /// </summary>
    private UWHeadBobRules.ModeEnum fMotionModeRow(Rect pOPanel, float pfTop, string psLabel, UWHeadBobRules.ModeEnum peMode)
    {
        GUI.Label(new Rect(pOPanel.x + 8f, pfTop, 100f, 26f), psLabel, mOListLabel);

        string[] lsModes = { "Off", "Original", "Smooth" };
        float lfButtonWidth = (pOPanel.width - 116f) / lsModes.Length;
        UWHeadBobRules.ModeEnum leResult = peMode;

        for (int liMode = 0; liMode < lsModes.Length; liMode++)
        {
            Rect lOButton = new Rect(pOPanel.x + 108f + (liMode * lfButtonWidth), pfTop, lfButtonWidth, 26f);

            if (GUI.Button(lOButton, lsModes[liMode], liMode == (int)peMode ? mOCurrentItem : mOListItem))
                leResult = (UWHeadBobRules.ModeEnum)liMode;
        }

        return leResult;
    }

    /// <summary>The strength row under a Smooth setting: ten to a hundred per cent in steps of
    /// five like the volumes, so the file is written only when a step changes and not on every
    /// frame of the drag. Returns the value it now stands at, the one given when unchanged.
    /// </summary>
    private float fMotionStrengthRow(Rect pOPanel, float pfTop, float pfStrength)
    {
        GUI.Label(new Rect(pOPanel.x + 8f, pfTop, 100f, 26f), "  Strength", mOListLabel);

        float lfWanted = GUI.HorizontalSlider(new Rect(pOPanel.x + 108f, pfTop + 8f, pOPanel.width - 170f, 20f),
            pfStrength, UWUserSettings.MinHeadBobStrength, 1f);

        lfWanted = Mathf.Clamp(Mathf.Round(lfWanted / HeadBobStrengthStep) * HeadBobStrengthStep,
            UWUserSettings.MinHeadBobStrength, 1f);

        GUI.Label(new Rect(pOPanel.x + pOPanel.width - 56f, pfTop, 56f, 26f),
            Mathf.RoundToInt(lfWanted * 100f) + " %", mOListLabel);

        return Mathf.RoundToInt(lfWanted * 100f) != Mathf.RoundToInt(pfStrength * 100f) ? lfWanted : pfStrength;
    }

    /// <summary>The multiple of the original's resolution, 1x to 4x (UWWorldResolution.Factor), on
    /// a slider in whole steps. While 4x is locked the slider ends at 3x and a line says why.
    /// </summary>
    private void fWorldResolutionFactorRow(Rect pOPanel, float pfTop, bool pbMaxLocked)
    {
        int liMax = pbMaxLocked ? UWWorldResolution.MaxFactor - 1 : UWWorldResolution.MaxFactor;
        int liShown = Mathf.Min(UWWorldResolution.Factor, liMax);

        // No label of its own (per user, 2026-10-07): under the switch it is clear what it sets.
        float lfWanted = GUI.HorizontalSlider(new Rect(pOPanel.x + 32f, pfTop + 8f, pOPanel.width - 94f, 20f),
            liShown, 1f, liMax);

        int liWanted = Mathf.Clamp(Mathf.RoundToInt(lfWanted), 1, liMax);

        GUI.Label(new Rect(pOPanel.x + pOPanel.width - 56f, pfTop, 56f, 26f), liWanted + "x", mOListLabel);

        if (liWanted != liShown)
            UWWorldResolution.Factor = liWanted;

        if (pbMaxLocked)
            GUI.Label(new Rect(pOPanel.x + 8f, pfTop + 30f, pOPanel.width - 16f, 26f),
                "  4x needs a window 1600 pixels tall", mOListLabel);
    }

    /// <summary>The step of the Smooth head bob's strength slider.</summary>
    private const float HeadBobStrengthStep = 0.05f;

    /// <summary>The four detail levels of the options panel, the current one in the accent
    /// colour (see UWGraphicsDetail).</summary>
    private void fDrawGraphicsMenu()
    {
        string[] lsItems =
        {
            "Low - original palette",
            "Medium - Remastered, no effects",
            "High - Remastered, all effects",
            "Very high - own effects..."
        };

        // The detail levels, and below them, set apart, the 4:3 switch. The motion rows that
        // followed (head bob, weapon jitter, the motion modes) moved to the Motion dialog of the
        // bar on 2026-10-06 (per user: they need an explanation, "ein eigener Menuepunkt").
        const float AspectRowHeight = 36f;
        const float WorldResolutionRowHeight = 30f;

        // The multiple's slider under the switch while it is on, and a line on the locked 4x
        // while the window is too small for it.
        bool lbResolutionOn = UWWorldResolution.Enabled;
        bool lbMaxLocked = !UWWorldResolution.IsMaxFactorAllowed;
        float lfFactorRows = lbResolutionOn ? WorldResolutionRowHeight * (lbMaxLocked ? 2f : 1f) : 0f;

        // Under the levels the palette renderer's own effects (UWPaletteEffects), a dialog.
        const float PaletteEffectsRowHeight = 30f;

        Rect lOPanel = new Rect(168f, BarHeight, 300f,
            8f + (lsItems.Length * 30f) + PaletteEffectsRowHeight + AspectRowHeight + WorldResolutionRowHeight + lfFactorRows);

        fFill(lOPanel, PanelColour);

        int liCurrent = (int)UWGraphicsDetail.CurrentLevel;

        for (int liAt = 0; liAt < lsItems.Length; liAt++)
        {
            Rect lOItem = new Rect(lOPanel.x, lOPanel.y + 4f + (liAt * 30f), lOPanel.width, 30f);

            if (GUI.Button(lOItem, lsItems[liAt], liAt == liCurrent ? mOCurrentItem : mOListItem))
            {
                fChooseLevel((UWGraphicsDetail.LevelEnum)liAt);

                return;
            }
        }

        if (GUI.Button(new Rect(lOPanel.x, lOPanel.y + 4f + (lsItems.Length * 30f), lOPanel.width, 30f),
            "Palette effects...", mOListItem))
        {
            fOpenPaletteEffectsDialog();

            return;
        }

        // THE 4:3 SWITCH lives here and not under Game (per user, 2026-09-22): it changes how
        // the picture looks, like the detail levels above it, and takes effect at once - the
        // game camera is set up anew every frame and every 320x200 frame follows it in
        // LateUpdate (UWPixelAspectFrame). The Game menu keeps what concerns the program.
        float lfAspectTop = lOPanel.y + 8f + (lsItems.Length * 30f) + PaletteEffectsRowHeight;

        fFill(new Rect(lOPanel.x + 8f, lfAspectTop, lOPanel.width - 16f, 1f), SeparatorColour);

        bool lbAspect = GUI.Toggle(new Rect(lOPanel.x + 8f, lfAspectTop + 6f, lOPanel.width - 16f, 26f),
            UWDisplayAspect.Enabled, "  Classic screen at 4:3", mOToggle);

        if (lbAspect != UWDisplayAspect.Enabled)
            UWDisplayAspect.Enabled = lbAspect;

        // The world at the original's resolution (UWWorldResolution), asked for on Reddit
        // (2026-10-07): it takes effect at once as well, the interface stays sharp.
        bool lbOriginalResolution = GUI.Toggle(
            new Rect(lOPanel.x + 8f, lfAspectTop + 6f + WorldResolutionRowHeight, lOPanel.width - 16f, 26f),
            UWWorldResolution.Enabled, "  World at the original's resolution", mOToggle);

        if (lbOriginalResolution != UWWorldResolution.Enabled)
            UWWorldResolution.Enabled = lbOriginalResolution;

        if (lbResolutionOn)
            fWorldResolutionFactorRow(lOPanel, lfAspectTop + 6f + (2f * WorldResolutionRowHeight), lbMaxLocked);

        fCloseMenuOnOutsideClick(lOPanel);
    }

    /// <summary>A row with two choices in the style of fMotionModeRow; returns true for the
    /// second one.</summary>
    private bool fTwoWayRow(Rect pOPanel, float pfTop, string psLabel, bool pbSecond, string psFirst, string psSecond)
    {
        GUI.Label(new Rect(pOPanel.x + 8f, pfTop, 100f, 26f), psLabel, mOListLabel);

        float lfButtonWidth = (pOPanel.width - 116f) / 2f;
        bool lbResult = pbSecond;

        if (GUI.Button(new Rect(pOPanel.x + 108f, pfTop, lfButtonWidth, 26f), psFirst, !pbSecond ? mOCurrentItem : mOListItem))
            lbResult = false;

        if (GUI.Button(new Rect(pOPanel.x + 108f + lfButtonWidth, pfTop, lfButtonWidth, 26f), psSecond, pbSecond ? mOCurrentItem : mOListItem))
            lbResult = true;

        return lbResult;
    }

    private void fCloseMenuOnOutsideClick(Rect pOPanel)
    {
        if (Event.current.type == EventType.MouseDown && !pOPanel.Contains(Event.current.mousePosition)
            && Event.current.mousePosition.y > BarHeight)
        {
            meOpenMenu = MenuEnum.None;
            Event.current.Use();
        }
    }

    private void fDrawGameMenu()
    {
        Rect lOPanel = new Rect(8f, BarHeight, 260f, 162f);

        fFill(lOPanel, PanelColour);

        // The game folder only from the main menu: in the game (the bar over the in-game menus
        // since 2026-10-06) a new folder would mean a new game.
        GUI.enabled = UWMainMenu.AcceptsMenuBar;

        if (GUI.Button(new Rect(lOPanel.x, lOPanel.y + 4f, lOPanel.width, 30f), "Game folder...", mOListItem))
            fOpenFolderDialog();

        GUI.enabled = true;

        // THE WHOLE OPENING AT STARTUP: the two company logos, the animated title and the
        // intro. Off it goes straight to the main menu, which is what one wants while testing
        // (per user, 2026-09-22). It takes effect at the next start.
        bool lbOpening = GUI.Toggle(new Rect(lOPanel.x + 8f, lOPanel.y + 38f, lOPanel.width - 16f, 26f),
            UWUserSettings.ShowOpeningSequence, "  Logos and intro at startup", mOToggle);

        if (lbOpening != UWUserSettings.ShowOpeningSequence)
        {
            UWUserSettings.ShowOpeningSequence = lbOpening;
            UWUserSettings.Save();
        }

        // KEEP RUNNING WITHOUT THE FOCUS (per user, 2026-09-28: for idle tests beside the
        // original, but everyone should decide for themselves). Takes effect at once.
        bool lbBackground = GUI.Toggle(new Rect(lOPanel.x + 8f, lOPanel.y + 68f, lOPanel.width - 16f, 26f),
            UWUserSettings.RunInBackground, "  Keep running in the background", mOToggle);

        if (lbBackground != UWUserSettings.RunInBackground)
        {
            UWUserSettings.RunInBackground = lbBackground;
            UWUserSettings.Save();
        }

        // DEAL THE ATTRIBUTE POINTS AT CHARACTER CREATION ONESELF (per user, 2026-10-01: for
        // those who do not like their values to depend on luck; off by default). Takes effect at
        // the next character creation.
        bool lbManual = GUI.Toggle(new Rect(lOPanel.x + 8f, lOPanel.y + 98f, lOPanel.width - 16f, 26f),
            UWUserSettings.ManualAttributes, "  Choose attributes at creation", mOToggle);

        if (lbManual != UWUserSettings.ManualAttributes)
        {
            UWUserSettings.ManualAttributes = lbManual;
            UWUserSettings.Save();
        }

        if (GUI.Button(new Rect(lOPanel.x, lOPanel.y + 128f, lOPanel.width, 30f), "Quit", mOListItem))
            fQuit();

        // A click anywhere else closes the menu.
        if (Event.current.type == EventType.MouseDown && !lOPanel.Contains(Event.current.mousePosition)
            && Event.current.mousePosition.y > BarHeight)
        {
            meOpenMenu = MenuEnum.None;
            Event.current.Use();
        }
    }

    private void fDrawFolderDialog(float pfWidth)
    {
        float lfDialogWidth = Mathf.Min(760f, pfWidth - 40f);
        Rect lODialog = new Rect((pfWidth - lfDialogWidth) / 2f, 210f, lfDialogWidth, 480f);

        fFill(lODialog, PanelColour);

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 16f;

        GUI.Label(new Rect(lfLeft, lfTop, lfInner, 26f), "Game folder", mOLabel);
        lfTop += 28f;

        lfTop += Mathf.Max(22f, fDrawHint(lfLeft, lfTop, lfInner,
            "Choose the folder of your GOG installation of Ultima Underworld - the one that contains game.gog.")) + 8f;

        foreach (string lsFound in mOBrowser.Found)
        {
            if (GUI.Button(new Rect(lfLeft, lfTop, lfInner, 26f), "Found:  " + lsFound, mOListItem))
                fBrowse(lsFound);

            lfTop += 28f;
        }

        // A button per drive, so another drive is one click away (per user, 2026-09-17).
        GUI.Label(new Rect(lfLeft, lfTop, 60f, 26f), "Drives:", mODimLabel);

        float lfDriveLeft = lfLeft + 64f;

        foreach (string lsDrive in mODrives)
        {
            // The label: a Windows letter, or on Linux "/", "Home" and the mount's name - the
            // button as wide as its label needs (UWGameFolderBrowser.GetDriveLabel).
            string lsLabel = UWGameFolderBrowser.GetDriveLabel(lsDrive);
            float lfButtonWidth = Mathf.Max(44f, 16f + (lsLabel.Length * 9f));

            if (lfDriveLeft + lfButtonWidth > lfLeft + lfInner)
                break;

            if (GUI.Button(new Rect(lfDriveLeft, lfTop, lfButtonWidth, 26f), lsLabel, mOButton))
                fBrowse(lsDrive);

            lfDriveLeft += lfButtonWidth + 6f;
        }

        lfTop += 32f;

        // The path: typed or pasted, Enter goes there.
        if (GUI.Button(new Rect(lfLeft, lfTop, 50f, 26f), "Up", mOButton))
        {
            mOBrowser.Up();
            fAfterBrowse();
        }

        GUI.SetNextControlName("PathField");
        msTypedPath = GUI.TextField(new Rect(lfLeft + 56f, lfTop, lfInner - 56f, 26f), msTypedPath ?? string.Empty, mOTextField);

        if (Event.current.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == "PathField"
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
        {
            fBrowse(msTypedPath);
            Event.current.Use();
        }

        lfTop += 32f;

        float lfListBottom = lODialog.yMax - 90f;
        Rect lOListRect = new Rect(lfLeft, lfTop, lfInner, lfListBottom - lfTop);

        fFill(lOListRect, BackgroundColour);

        Rect lOContent = new Rect(0f, 0f, lOListRect.width - 20f, Mathf.Max(lOListRect.height, mOBrowser.Entries.Count * 24f));

        mOScroll = GUI.BeginScrollView(lOListRect, mOScroll, lOContent);

        string lsGoTo = null;

        for (int liAt = 0; liAt < mOBrowser.Entries.Count; liAt++)
        {
            string lsEntry = mOBrowser.Entries[liAt];

            if (GUI.Button(new Rect(0f, liAt * 24f, lOContent.width, 24f), mOBrowser.GetDisplayName(lsEntry), mOListItem))
                lsGoTo = lsEntry;
        }

        GUI.EndScrollView();

        if (lsGoTo != null)
            fBrowse(lsGoTo);

        GUIStyle lOStatusStyle = new GUIStyle(mODimLabel);
        lOStatusStyle.normal.textColor = mOBrowser.StatusIsError ? new Color(0.9f, 0.4f, 0.35f) : (mOBrowser.CanUseCurrent ? AccentColour : DimTextColour);

        GUI.Label(new Rect(lfLeft, lODialog.yMax - 82f, lfInner, 22f),
            mbApplyPending ? "Preparing the game data..." : mOBrowser.Status, lOStatusStyle);

        float lfButtonTop = lODialog.yMax - 46f;

        GUI.enabled = mOBrowser.CanUseCurrent && !mbApplyPending;

        if (GUI.Button(new Rect(lODialog.xMax - 200f, lfButtonTop, 180f, 30f), "Use this folder", mOButton))
            mbApplyPending = true;

        GUI.enabled = true;

        string lsCancel = mbFirstRun ? "Quit" : "Cancel";

        if (GUI.Button(new Rect(lODialog.xMax - 320f, lfButtonTop, 110f, 30f), lsCancel, mOButton))
        {
            if (mbFirstRun)
                fQuit();
            else
                mbFolderDialogOpen = false;
        }
    }

    private bool fBarItem(Rect pORect, string psText, bool pbOpen)
    {
        if (pbOpen)
            fFill(pORect, HoverColour);

        return GUI.Button(pORect, psText, mOBarItem);
    }

    private void fFill(Rect pORect, Color pOColour)
    {
        GUI.DrawTexture(pORect, fTexture(pOColour));
    }

    private Texture2D fTexture(Color pOColour)
    {
        Texture2D lOTexture;

        if (mOTextures.TryGetValue(pOColour, out lOTexture) && lOTexture != null)
            return lOTexture;

        lOTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        lOTexture.name = "UWSetupMenu";
        lOTexture.SetPixel(0, 0, pOColour);
        lOTexture.Apply();

        mOTextures[pOColour] = lOTexture;

        return lOTexture;
    }

    private void fEnsureStyles()
    {
        if (mOLabel != null)
            return;

        mOFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        mOLabel = new GUIStyle { font = mOFont, fontSize = 20, alignment = TextAnchor.MiddleLeft };
        mOLabel.normal.textColor = TextColour;

        mODimLabel = new GUIStyle(mOLabel) { fontSize = 15, wordWrap = true };
        mODimLabel.normal.textColor = DimTextColour;

        mOTitle = new GUIStyle(mOLabel) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
        mOTitle.normal.textColor = AccentColour;

        mOBarItem = new GUIStyle(mOLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        mOBarItem.hover.background = fTexture(HoverColour);
        mOBarItem.hover.textColor = TextColour;

        mOListItem = new GUIStyle(mOLabel) { fontSize = 16, padding = new RectOffset(10, 10, 0, 0) };
        mOListItem.hover.background = fTexture(HoverColour);
        mOListItem.hover.textColor = TextColour;

        mOListLabel = new GUIStyle(mOLabel) { fontSize = 16 };

        mOToggle = new GUIStyle(GUI.skin.toggle) { font = mOFont, fontSize = 16 };
        mOToggle.normal.textColor = TextColour;
        mOToggle.onNormal.textColor = TextColour;
        mOToggle.hover.textColor = TextColour;
        mOToggle.onHover.textColor = TextColour;

        mOCurrentItem = new GUIStyle(mOListItem);
        mOCurrentItem.normal.textColor = AccentColour;
        mOCurrentItem.hover.textColor = AccentColour;

        mOButton = new GUIStyle(mOLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        mOButton.normal.background = fTexture(HoverColour);
        mOButton.hover.background = fTexture(new Color(0.30f, 0.30f, 0.35f, 1f));
        mOButton.hover.textColor = TextColour;
        mOButton.active.background = fTexture(AccentColour);
        mOButton.active.textColor = BackgroundColour;

        mOTextField = new GUIStyle(mOLabel) { fontSize = 16, padding = new RectOffset(8, 8, 0, 0), clipping = TextClipping.Clip };
        mOTextField.normal.background = fTexture(BackgroundColour);
        mOTextField.focused.background = fTexture(BackgroundColour);
        mOTextField.focused.textColor = TextColour;
        mOTextField.hover.textColor = TextColour;
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
