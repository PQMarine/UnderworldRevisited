using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE HELP WINDOW (feature wish of the user, 2026-09-24; layout decided 2026-09-25): Tab (F1 until 2026-09-26) opens
/// it beside the picture - the classic frame goes to 4:3 and slides left (UWHelpLayout), the help
/// takes the freed width. Seven tabs: the character's STATS, a live cut-out of the MAP (both added
/// 2026-09-26), free NOTES of the player (kept per save game, UWHelpNotes), the known SPELLS, the
/// known MANTRAS, the game's MANUAL (the GOG PDF, rendered by UWManual, with a reading mode
/// over the whole picture) and the CONTROLS of the scheme in force (UWHelpControls, 2026-10-04).
///
/// THE LOOK is modern and readable (per user), the same as the setup bar: IMGUI with the built-in
/// font until phase 8b, because the scene has no EventSystem on purpose (see UWItemDrag) and IMGUI
/// brought the text area and the scroll list along; the kit brings its own since 8b.3.
///
/// Not in a screen menu: those are centred canvases of their own that do not follow the frame, so
/// the help closes when one opens. A CONVERSATION keeps it (per user, 2026-09-26: it closed when a
/// conversation started): the conversation's frame slides with the game frame
/// (UWClassicConversationRenderer.Tick) and leaves the keys to the help while it has them.
///
/// THE TABS ARE CLASSES (cleanup 8.6, 2026-10-24): each tab draws itself (UWHelpTab and its seven
/// kinds); the window keeps the frame - where the help lies in each scheme and preset, the
/// scaling, the tab row, the reading mode's state and hold - and draws the active tab into it.
///
/// ON THE KIT (cleanup 8b.3, 2026-10-24; phase 8b, decision 26): the window, its tab row, the seven
/// tabs and the reading mode are drawn on the uGUI kit (UWKitRows, its own canvas
/// UWUiSorting.HelpWindow) in LateUpdate, in the same place, clip and scale as the IMGUI form (fPlace
/// served both); the IMGUI form drew in OnGUI only under the side-by-side switch until it went with
/// the switch in cleanup 8b.4. The notes type into a text area of the kit
/// (UWTextEntry, UWTypedKeys); the clicks come from the Input System through the modern root's gate
/// (UWModernUi.WindowTakesPointer), the pad's pointer pressing the real button as before.
/// </summary>
public class UWHelpWindow : MonoBehaviour
{
    private const float CanvasHeight = 200f;

    /// <summary>Seven tabs do not fit in one row beside the 4:3 frame - two rows of four (of three
    /// until the Controls tab came, 2026-10-04).</summary>
    private const int TabsPerRow = 4;

    /// <summary>Whether the player types in the notes - then the game's keys stay silent (see
    /// UWTextEntryState.IsTextEntryActive).</summary>
    public static bool IsTyping { get; private set; }

    /// <summary>The tabs in the order of their saved numbers (UWHelpTabId), made in Awake.</summary>
    private UWHelpTab[] mOTabs;

    private UWHelpTab mOActive;

    /// <summary>The manual's tab, which also draws the reading mode.</summary>
    private UWHelpManualTab mOManual;

    private UWHelpContext mOContext;

    /// <summary>The kit's pieces on the help's own canvas, and the flow that lays the tabs out on
    /// them (cleanup 8b.3).</summary>
    private UWKitRows mOKit;

    private UWHelpKitFlow mOKitFlow;

    /// <summary>
    /// THE HELP KEEPS ITS STATE (per user, 2026-10-01): open or closed, the tab and the map
    /// tab's zoom are stored in the settings (UWUserSettings) and come back with the next game
    /// and the next start. Only the player's own Tab counts as the wish - a screen menu that
    /// pushes the help aside does not change it, and once the menu is gone (and no intro or
    /// cutscene plays) the help comes back.
    /// </summary>
    private static bool msbReopenPending;

    /// <summary>Called by the level loader instead of closing the help: open at once when the
    /// player left it open and nothing covers the picture, otherwise closed and reopened later.
    /// </summary>
    public static void RestoreAfterLoad()
    {
        if (UWUserSettings.HelpOpen && !fIsCovered())
        {
            UWHelpLayout.OpenAtOnce();
            msbReopenPending = false;
        }
        else
        {
            UWHelpLayout.CloseAtOnce();
            msbReopenPending = UWUserSettings.HelpOpen;
        }
    }

    /// <summary>A screen menu, the intro or a cutscene takes the picture.</summary>
    private static bool fIsCovered()
    {
        UWIntroPlayer lOIntro = UWScene.IntroPlayer;

        return UWScreenUi.IsScreenMenuOpen || (lOIntro != null && lOIntro.IsPlaying);
    }

    private void Awake()
    {
        mOManual = new UWHelpManualTab();
        mOTabs = new UWHelpTab[]
        {
            new UWHelpStatsTab(), new UWHelpMapTab(), new UWHelpNotesTab(), new UWHelpSpellsTab(),
            new UWHelpMantrasTab(), mOManual, new UWHelpControlsTab()
        };
        mOContext = new UWHelpContext(this);
        mOKit = new UWKitRows(transform, UWUiSorting.HelpWindow);
        mOKitFlow = new UWHelpKitFlow(mOKit);

        // Each tab at its saved number - a wrong order would mix up the settings file's tab.
        for (int liAt = 0; liAt < mOTabs.Length; liAt++)
        {
            if ((int)mOTabs[liAt].Id != liAt)
                Debug.LogError("UWHelpWindow: the tab " + mOTabs[liAt].TitleId + " stands at " + liAt);
        }

        mOActive = mOTabs[(int)UWHelpTabIds.FromSaved(UWUserSettings.HelpTab)];
    }

    // ------------------------------------------------- The manual's reading mode

    /// <summary>The name of the modal hold while reading (UWControlScheme.HoldUiModal).</summary>
    private const string ReadingHold = "help reading";

    /// <summary>
    /// THE READING MODE (per user, 2026-09-26): a click on the page lays the manual over the
    /// whole picture at screen height, two pages side by side like the open booklet where the
    /// width allows. Beside the frame the page is only good for leafing through below about
    /// 1080 lines (probe render the same day). While it is open the game hears neither keys
    /// nor clicks.
    /// </summary>
    public static bool IsReading { get; private set; }

    /// <summary>The help has the keyboard - typing in the notes or reading the manual (see
    /// UWTextEntryState.IsTextEntryActive).</summary>
    public static bool BlocksGameKeys => IsTyping || IsReading;

    /// <summary>Where the help lies on the screen (pixels, top left), for keeping the game's
    /// clicks off it.</summary>
    private Rect mOPanelOnScreen;

    private void Update()
    {
        UWHelpLayout.Tick(Time.unscaledDeltaTime);

        // A screen menu takes the whole picture - the help steps aside. A conversation does not.
        // A CUTSCENE TOO (per user, 2026-10-02: the help still covered a cutscene): it steps
        // aside without changing the wish and comes back afterwards.
        if (UWHelpLayout.IsOpen && fIsCovered())
        {
            fSetOpen(false, false);
            msbReopenPending = UWUserSettings.HelpOpen;
        }
        else if (msbReopenPending && !fIsCovered())
        {
            msbReopenPending = false;

            if (UWUserSettings.HelpOpen && !UWHelpLayout.IsOpen)
                fSetOpen(true, false);
        }

        // The game's buttons stay off the help: while reading everywhere, otherwise over the
        // panel (it lies over the picture on 4:3 screens and in the modern scheme).
        fUpdateSwallow();

        UWMouseButtons.IsBlockedByHelp = IsReading || msbSwallowUntilReleased
            || (UWHelpLayout.Blend > 0f && fIsMouseOverPanel());

        if (BlocksGameKeys)
            return;

        // TAB, NOT WITH ALT (per user, 2026-09-26): Alt+Tab switches to another program and must
        // not open or close anything on the way. Nor with Shift - Shift+Tab is left alone.
        // In the modern scheme the character panel takes Tab and opens on its Help tab
        // (UWModernPanel, per user 2026-10-03); the help is drawn inside it. Not in Classic Wide,
        // which has no such panel (UWModernPanel.HoldsHelp). Not under a setup-menu panel either -
        // the Game-folder dialog took Tab as help (Linux first run, 2026-10-06). The row is
        // UWHotkeys' since cleanup step 7.5.
        if (mOKeys == null)
            mOKeys = new HelpKeys(this);

        if (mOKeys.Scheme != null && mOKeys.Scheme.Controls != null)
            UWHotkeys.Route(UWHotkeySite.Help, mOKeys);
    }

    private HelpKeys mOKeys;

    /// <summary>The help's hotkey (UWHotkeys): Tab opens and closes it.</summary>
    private sealed class HelpKeys : UWHotkeyHost
    {
        private readonly UWHelpWindow mOHelp;

        public HelpKeys(UWHelpWindow pOHelp)
        {
            mOHelp = pOHelp;
        }

        public UWControlScheme Scheme => UWScene.ControlScheme;

        protected override UWControls Controls => Scheme != null ? Scheme.Controls : null;

        public override bool Gate(UWHotkeyGate peGate, UWHotkeyRow pORow)
        {
            switch (peGate)
            {
                case UWHotkeyGate.HelpKeys: return BlocksGameKeys;
                case UWHotkeyGate.PanelHoldsHelp: return Scheme != null && UWModernPanel.HoldsHelp;
                case UWHotkeyGate.KeysLocked: return UWControls.IsTextEntryActive;
                case UWHotkeyGate.SetupPanel: return UWSetupMenu.HasOpenPanel;
                case UWHotkeyGate.HelpCovered: return fIsCovered();
                default: return false;
            }
        }

        public override bool Run(UWHotkeyHandler peHandler)
        {
            if (peHandler != UWHotkeyHandler.ToggleHelp)
                return false;

            mOHelp.fSetOpen(!UWHelpLayout.IsOpen, true);
            return true;
        }
    }

    /// <summary>Turns to the map tab - for a click on the modern scheme's minimap
    /// (UWModernMinimap), which opens the panel's Help tab on it.</summary>
    internal void ShowMapTab()
    {
        fShow(mOTabs[(int)UWHelpTabId.Map]);
    }

    /// <summary>Turns to a tab - its list back at the top - and stores it as the one to come back
    /// to.</summary>
    private void fShow(UWHelpTab pOTab)
    {
        // The tab left behind types no more (the kit's notes area lets go, cleanup 8b.3).
        if (mOActive != pOTab)
            mOActive.OnHidden();

        if (mOKit != null)
            mOKit.LetGoOfArea();

        mOActive = pOTab;
        pOTab.OnShown();

        int liTab = (int)pOTab.Id;

        if (UWUserSettings.HelpTab != liTab)
        {
            UWUserSettings.HelpTab = liTab;
            UWUserSettings.Save();
        }
    }

    /// <summary>The notes' Tab while typing closes the help - the player's own Tab.</summary>
    internal void CloseByPlayer()
    {
        fSetOpen(false, true);
    }

    /// <summary>The modern character panel opens or closes the help with its Help tab - the
    /// player's own choice, stored as the wish like Tab.</summary>
    internal void SetOpenFromPanel(bool pbOpen)
    {
        if (UWHelpLayout.IsOpen != pbOpen || UWUserSettings.HelpOpen != pbOpen)
            fSetOpen(pbOpen, true);
    }

    /// <summary>pbByPlayer: the player's own Tab - stored as the wish.</summary>
    private void fSetOpen(bool pbOpen, bool pbByPlayer)
    {
        UWHelpLayout.SetOpen(pbOpen);

        if (pbByPlayer && UWUserSettings.HelpOpen != pbOpen)
        {
            UWUserSettings.HelpOpen = pbOpen;
            msbReopenPending = false;
            UWUserSettings.Save();
        }

        if (!pbOpen)
        {
            IsTyping = false;
            SetReading(false);

            // The kit's notes let go at once, not after the slide (cleanup 8b.3).
            mOActive.OnHidden();

            if (mOKit != null)
                mOKit.LetGoOfArea();
        }
    }

    /// <summary>The reading mode was closed by a click whose buttons are still down: the game
    /// stays shut out (mouse and modal hold) until every button is up, so the release does
    /// not use, drop or walk anything (per user, 2026-09-26).</summary>
    private static bool msbSwallowUntilReleased;

    internal static void SetReading(bool pbReading, bool pbByClick = false)
    {
        if (IsReading == pbReading)
            return;

        IsReading = pbReading;

        if (!pbReading && pbByClick)
        {
            msbSwallowUntilReleased = true;
            return;
        }

        fSetReadingHold(pbReading);
    }

    private static void fSetReadingHold(bool pbHold)
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;

        if (lOScheme != null)
            lOScheme.SetUiModal(ReadingHold, pbHold);
    }

    /// <summary>Ends the swallowing once no mouse button is held any more.</summary>
    private static void fUpdateSwallow()
    {
        if (!msbSwallowUntilReleased)
            return;

        if (UWPointer.HasMouse && (UWPointer.LeftHeld || UWPointer.RightHeld
            || UWPointer.MiddleHeld))
            return;

        msbSwallowUntilReleased = false;

        if (!IsReading)
            fSetReadingHold(false);
    }

    private bool fIsMouseOverPanel()
    {
        if (!UWPointer.HasMouse)
            return false;

        Vector2 lOAt = UWPointer.Position;

        return mOPanelOnScreen.Contains(new Vector2(lOAt.x, Screen.height - lOAt.y));
    }

    private void OnDisable()
    {
        IsTyping = false;
        SetReading(false);

        if (msbSwallowUntilReleased)
        {
            msbSwallowUntilReleased = false;
            fSetReadingHold(false);
        }

        UWMouseButtons.IsBlockedByHelp = false;

        // The kit's canvas goes dark with the window (cleanup 8b.3).
        fHideKit();
    }

    private void OnDestroy()
    {
        if (mOKit != null)
        {
            mOKit.LetGoOfArea();
            mOKit.Dispose();
        }
    }

    // ------------------------------------------------- Drawing

    /// <summary>
    /// WHERE THE HELP LIES THIS FRAME, for both forms (the IMGUI form's numbers, moved out of OnGUI in
    /// cleanup 8b.3, 2026-10-24): in the windows' layout units (UWGuiTheme, 720 lines) the left edge it is
    /// cut off at (pfClipX - the sliding classic frame's right edge), its area and its top and height
    /// (the modern panel's inner area on its Help tab). False while the modern panel is closed or still
    /// sliding - then nothing is drawn.
    /// </summary>
    private bool fPlace(out float pfClipX, out float pfAreaX, out float pfAreaWidth, out float pfAreaTop, out float pfAreaHeight)
    {
        float lfUnit = Screen.height / CanvasHeight;
        float lfCanvasWidth = Screen.width / lfUnit;

        pfClipX = 0f;
        pfAreaX = 0f;
        pfAreaWidth = 0f;

        UWHelpLayout.OpenLayout(lfCanvasWidth, out float _, out float lfLeft, out float lfWidth);

        // In the modern scheme there is no classic frame to slide: the help lies over the picture.
        UWControlScheme lOScheme = UWScene.ControlScheme;
        bool lbBesideFrame = UWHelpLayout.FitsBeside(lfCanvasWidth);

        // THE MODERN CHARACTER PANEL holds the help on its Help tab (UWModernPanel): drawn into
        // the panel's inner area, and not at all while the panel is closed or still sliding.
        float lfScale = UWGuiTheme.Scale;

        pfAreaTop = 0f;
        pfAreaHeight = UWGuiTheme.ReferenceHeight;

        if (lOScheme != null && UWControlScheme.Ui.Policy.HelpOverPicture)
        {
            lfWidth = Mathf.Min(UWHelpLayout.MaxWidthPerHeight * CanvasHeight, lfCanvasWidth * UWHelpLayout.OverlayShare);
            lfLeft = lfCanvasWidth - lfWidth;
            lbBesideFrame = false;

            // Classic Wide has no panel for it: the help lies right of the frame, which closes up
            // to leave it room (UWModernClassicFrame.HelpBeside, per user 2026-10-21), or over the
            // picture's right side where the screen is too narrow for that.
            if (UWModernClassicFrame.HelpBeside)
            {
                lfLeft = UWModernClassicFrame.RightEdge / lfUnit;
                lfWidth = Mathf.Min(lfCanvasWidth - lfLeft, UWHelpLayout.MaxWidthPerHeight * CanvasHeight);
            }

            if (UWModernPanel.HoldsHelp)
            {
                if (!UWModernPanel.Instance.TryGetHelpArea(out Rect lOInPanel))
                    return false;

                lfLeft = lOInPanel.x / lfUnit;
                lfWidth = lOInPanel.width / lfUnit;
                pfAreaTop = (Screen.height - lOInPanel.yMax) / lfScale;
                pfAreaHeight = lOInPanel.height / lfScale;
            }
        }

        // Beside the sliding frame the help lies BEHIND it: cut off at the frame's right edge,
        // so the frame uncovers it while sliding left (per user, 2026-09-25). IMGUI is drawn over
        // every canvas, hence the clip instead of a draw order (the kit's canvas lies over the
        // frame's as well, UWUiSorting.HelpWindow).
        float lfClipLeft = lfLeft;

        if (lbBesideFrame)
            lfClipLeft = Mathf.Clamp(UWHelpLayout.FrameRightEdge(lfCanvasWidth, UWClassicScreen.HorizontalPixelFactor),
                lfLeft, lfLeft + lfWidth);

        pfClipX = lfClipLeft * lfUnit / lfScale;
        pfAreaX = lfLeft * lfUnit / lfScale;
        pfAreaWidth = lfWidth * lfUnit / lfScale;

        return true;
    }

    /// <summary>The help is not drawn: nothing typed or read, no panel to keep the clicks off.</summary>
    private void fNotDrawn()
    {
        IsTyping = false;
        mOPanelOnScreen = Rect.zero;
        SetReading(false);
    }

    /// <summary>
    /// THE HELP ON THE KIT (cleanup 8b.3, 2026-10-24; phase 8b): after Update decided whether it is
    /// open and where the frame stands, the window, its tab row and the active tab are drawn on the kit
    /// (UWKitRows on its own canvas, UWUiSorting.HelpWindow), laid out as the IMGUI form in OnGUI was
    /// (gone since cleanup 8b.4) - the same place in every scheme and preset, the same clip at the
    /// sliding frame's edge, the same scaling, faded with the slide. The tabs draw through DrawKit
    /// (UWHelpKitFlow); the reading mode over everything after them.
    /// </summary>
    private void LateUpdate()
    {
        if (mOKit == null)
            return;

        if (UWHelpLayout.Blend <= 0f || Screen.height <= 0
            || !fPlace(out float lfClipX, out float lfAreaX, out float lfAreaWidth, out float lfAreaTop, out float lfAreaHeight))
        {
            fHideKit();
            fNotDrawn();
            return;
        }

        float lfScale = UWGuiTheme.Scale;

        mOKit.Begin(true, false, false, false);

        // The setup bar's windows lie over the help (its canvas over this one): while the bar
        // captures the pointer its clicks are its own, as the in-game menus under it stand back.
        // So are the modern game menu's while it is open (on the kit over this canvas since 8b.4,
        // UWUiSorting.ModernGameMenu): with both on IMGUI the first window to use the click had it,
        // wherever they overlapped.
        if (UWSetupMenu.IsCapturingInput || UWModernUi.IsMenuOpen)
            mOKit.DropPointer();

        mOKit.SetAlpha(UWHelpLayout.Blend);

        mOPanelOnScreen = new Rect(lfClipX * lfScale, lfAreaTop * lfScale, (lfAreaX + lfAreaWidth - lfClipX) * lfScale,
            lfAreaHeight * lfScale);
        mOContext.Scale = lfScale;
        mOContext.ClipPixels = lfClipX * lfScale;

        // While reading, the overlay has the mouse - the panel below must not react to it.
        mOKit.Enabled = !IsReading;

        mOKit.BeginClipArea(new Rect(lfClipX, lfAreaTop, lfAreaX + lfAreaWidth - lfClipX, lfAreaHeight));
        // The window's ground in the theme in force (cleanup phase 8d; Plain: the overlay's fill).
        mOKit.Ground(new Rect(lfAreaX, lfAreaTop, lfAreaWidth, lfAreaHeight), UWWindowGroundEnum.Overlay);

        Rect lOInner = new Rect(lfAreaX + 12f, lfAreaTop + 12f, lfAreaWidth - 24f, lfAreaHeight - 24f);

        mOKit.BeginClipArea(lOInner);

        float lfTop = fDrawKitTabs(lOInner) + 10f;
        Rect lOBody = new Rect(lOInner.x, lfTop, lOInner.width, Mathf.Max(0f, lOInner.yMax - lfTop));
        UWHelpTab lOTab = mOActive;

        // Only the notes type: another tab takes the keys from their area.
        if (lOTab.Id != UWHelpTabId.Notes)
            mOKit.LetGoOfArea();

        if (lOTab.Scrolls)
        {
            // The tab's list in a scroll list as IMGUI's scroll view: its height from the last frame,
            // the bar's room taken from the list's width while it is taller than the room.
            float lfContent = lOTab.KitContentHeight;
            bool lbBar = lfContent > lOBody.height;

            lOTab.Scroll = mOKit.BeginScroll(lOBody, lOTab.Scroll, new Rect(0f, 0f, lOBody.width, lfContent));
            mOKitFlow.Begin(0f, 0f, lOBody.width - (lbBar ? UWKitRows.ScrollBarRoom : 0f), lOBody.height);
            lOTab.DrawKit(mOContext, mOKitFlow);
            lOTab.KitContentHeight = mOKitFlow.Y;
            mOKit.EndScroll();
        }
        else
        {
            mOKitFlow.Begin(lOBody.x, lOBody.y, lOBody.width, lOBody.yMax);
            lOTab.DrawKit(mOContext, mOKitFlow);
        }

        // CapturesKeys is the notes while typing and the manual while it is read large; the
        // reading counts on its own (IsReading, set the moment it opens or closes), so IsTyping
        // stays the typing alone.
        IsTyping = lOTab.CapturesKeys && !IsReading;

        mOKit.EndClipArea();
        mOKit.EndClipArea();

        mOKit.Enabled = true;

        if (IsReading)
            mOManual.DrawReadingKit(mOContext, mOKit);

        mOKit.End();
    }

    /// <summary>The kit's canvas goes dark and its text area lets go.</summary>
    private void fHideKit()
    {
        if (mOKit == null)
            return;

        mOKit.LetGoOfArea();
        mOKit.Begin(false, false, false, false);
    }

    /// <summary>
    /// The tab row on the kit: two rows of four (three in the second), the current tab in gold on the
    /// lit face, the others lighting under the pointer - IMGUI's buttons, which shared each row's width
    /// evenly. SIZED BY TEXT (decided per user 2026-10-24): each row shares its width by UWRowSizing
    /// (the even shares while every title fits its share), and a row whose titles do not fit side by
    /// side at all breaks into more rows, so a longer language or a narrow help does not clip them.
    /// Returns the row's bottom.
    /// </summary>
    private float fDrawKitTabs(Rect pOInner)
    {
        UWKitRows.HelpLook lOLook = UWHelpKitFlow.ButtonLook(UWGuiTheme.HelpTabSize);
        float lfY = pOInner.y;

        for (int liRowStart = 0; liRowStart < mOTabs.Length; liRowStart += TabsPerRow)
        {
            int liEnd = Mathf.Min(liRowStart + TabsPerRow, mOTabs.Length);
            int liAt = liRowStart;

            while (liAt < liEnd)
            {
                int liTake = liEnd - liAt;

                while (liTake > 1 && fTabsWidth(liAt, liTake, lOLook) > pOInner.width)
                    liTake--;

                float[] laTexts = new float[liTake];
                float[] laWidths = new float[liTake];

                for (int liTab = 0; liTab < liTake; liTab++)
                    laTexts[liTab] = mOKit.HelpTextWidth(UWText.T(mOTabs[liAt + liTab].TitleId), lOLook);

                UWRowSizing.SegmentWidths(pOInner.width, laTexts, laWidths);

                float lfX = pOInner.x;

                for (int liTab = 0; liTab < liTake; liTab++)
                {
                    UWHelpTab lOTab = mOTabs[liAt + liTab];
                    bool lbCurrent = lOTab == mOActive;
                    UWKitRows.HelpLook lOTabLook = lOLook;

                    lOTabLook.Colour = lbCurrent ? UWGuiTheme.Accent : UWGuiTheme.Text;

                    if (mOKit.HelpButton(new Rect(lfX, lfY, laWidths[liTab], TabHeight), UWText.T(lOTab.TitleId), lOTabLook,
                        lbCurrent ? UWGuiTheme.Hover : UWGuiTheme.Panel, UWGuiTheme.Hover, lbCurrent ? UWGuiTheme.Accent : UWGuiTheme.Text))
                    {
                        mOKit.LetGoOfArea();
                        fShow(lOTab);
                    }

                    lfX += laWidths[liTab];
                }

                lfY += TabHeight;
                liAt += liTake;
            }
        }

        return lfY;
    }

    /// <summary>The width piCount tabs from piFirst need side by side: their titles and padding.</summary>
    private float fTabsWidth(int piFirst, int piCount, in UWKitRows.HelpLook pOLook)
    {
        float lfWidth = 0f;

        for (int liTab = piFirst; liTab < piFirst + piCount; liTab++)
            lfWidth += mOKit.HelpTextWidth(UWText.T(mOTabs[liTab].TitleId), pOLook) + (2f * UWRowSizing.Padding);

        return lfWidth;
    }

    /// <summary>A tab button's height.</summary>
    private const float TabHeight = 28f;
}
