using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN SCHEME'S CHARACTER PANEL (idea and look per user on a mockup, 2026-10-03): a panel
/// at the right edge of the screen.
///
///   - Closed only its HANDLE shows, a tab of leather that is one piece with the panel (it lies
///     over the panel's left frame, whose lines it turns into, UWModernHudArt.BuildLeatherTab).
///   - With the pointer free (the right button, UWModernPointer) and near the handle the panel
///     peeks out a little; a click on the handle or the peeking edge opens it, a click on the
///     handle closes it again. C opens and closes it (UWModernBags), Tab opens it on the Help tab
///     (the help window, drawn inside - UWHelpWindow); Escape closes it after the bags. None of
///     them touches the pointer (per user, 2026-10-04).
///   - Two tabs on top. CHARACTER: name, class and level; the original's inventory page with the
///     paperdoll - body and armour as UWCharacter draws them, the hands, shoulders and rings
///     with their things - whose slots take and give things like the bags (UWModernBags does
///     the dragging, TryGetEquipSlotAt); the attributes, vitality, mana and experience; the twenty
///     skills and the points to spend. HELP: the help window. THE BACKPACK stays with the bags
///     (per user, 2026-10-04 - until then it moved in under the paperdoll on the Character tab,
///     and the bag windows stacking beside the panel made the panel hard to place).
///   - The Character tab is only as tall as it needs (per user, 2026-10-03: at small UI sizes the
///     stats need not reach down to the bottom); the Help tab takes the screen's height. Where the
///     screen is too low for it (large UI sizes) its content SCROLLS with the wheel, clipped at
///     the frame, a thin bar showing where.
///   - SLEEP AND TRACK (per user, 2026-10-04: F10 and F9 have no other place in the modern
///     scheme): two round buttons left and right of the paperdoll's feet, the bedroll's and the
///     leather boots' pictures alone (a slot circle and a ring did not suit the page); a left click sleeps (as the bedroll and F10 do, also
///     without one) or reads the tracks (as F9); the pointer over one names it.
///   - PINNED (the pin on the tab row - UWUserSettings.ModernPanelPinned, per user 2026-10-03: at
///     half the UI size there is room to have the character in view all the time): the panel
///     stays out on its Character tab without the handle, the minimap in its head when the
///     screen has the rows (UWModernMinimap, TryGetMinimapArea). Tab widens it to the help for a
///     while; Tab or Escape go back to the Character tab.
/// </summary>
public class UWModernPanel : UWModernDockedPanel
{
    public enum TabEnum
    {
        Character,
        Help
    }

    public static UWModernPanel Instance { get; private set; }

    public TabEnum Tab => meTab;

    /// <summary>Pinned: always out on the Character tab (UWUserSettings.ModernPanelPinned).</summary>
    public static bool Pinned => UWUserSettings.ModernPanelPinned;

    /// <summary>Whether Escape has something to close here: an open panel, a pinned one only on
    /// the Help tab.</summary>
    public override bool IsClosable => mbOpen && (!Pinned || meTab == TabEnum.Help);

    /// <summary>At the right edge, its handle at its left (UWModernDockedPanel).</summary>
    protected override bool DockedLeft => false;

    protected override UWModernLayout.ElementEnum Element => UWModernLayout.ElementEnum.CharacterPanel;

    protected override bool IsPinned => Pinned;

    /// <summary>The layout in original pixels (the docked panel's measures are UWModernDockedPanel's).</summary>
    private const int CharacterWidth = 96;

    private const int BottomMargin = 4;

    /// <summary>The minimap in the pinned panel's head: square, as wide as the panel's inside,
    /// and the name two rows below it.</summary>
    private const int HeadSize = CharacterWidth - UWModernHudArt.LeatherLeft - UWModernHudArt.LeatherRight;

    private const int HeadRows = UWModernHudArt.LeatherTop + HeadSize + 2 - NameTop;

    /// <summary>The Character tab below the page, as fLayoutCharacter lays it out: a gap and a
    /// rule, the two rows of attributes, a rule and the skills' heading.</summary>
    private const int BelowPageRows = 3 + 3 + 26 + 3 + 8;

    private const int NameTop = 4;

    private const int PageTop = 15;

    private const int SkillRows = 10;

    private const float SkillRowHeight = 6f;

    /// <summary>Where the original draws the armour (UWPaperdollLayout), in MAIN.BYT
    /// pixels, rows from the top; the body's place the freed figure brings itself.</summary>
    private static readonly Vector2Int msHelmetAt = new Vector2Int(UWPaperdollLayout.HelmetX, UWPaperdollLayout.HelmetY);

    private static readonly Vector2Int msGlovesAt = new Vector2Int(UWPaperdollLayout.GlovesX, UWPaperdollLayout.GlovesY);

    private static readonly Vector2Int msLegsAt = new Vector2Int(UWPaperdollLayout.LegsX, UWPaperdollLayout.LegsY);

    private static readonly Vector2Int msChestAt = new Vector2Int(UWPaperdollLayout.ChestX, UWPaperdollLayout.ChestY);

    private static readonly Vector2Int msBootsAt = new Vector2Int(UWPaperdollLayout.BootsX, UWPaperdollLayout.BootsY);

    private UWCharacter mOCharacter;

    private UWHelpWindow mOHelp;

    private RectTransform mOContent;

    /// <summary>The Character tab's clipped view (RectMask2D) - its content scrolls in it.</summary>
    private RectTransform mOViewport;

    private Image mOScrollBar;

    /// <summary>How far the Character tab is scrolled down, original rows.</summary>
    private float mfScroll;

    private Rect mOViewRect;

    private const float ScrollStep = 18f;

    private Text mOName;

    private Text mOClass;

    private RawImage mOPage;

    private RawImage[] mOArmour;

    private RawImage[] mOIcons;

    /// <summary>A stack's count at the bottom right of its hand, shoulder or ring slot, as the
    /// classic panel and the bags show it.</summary>
    private Text[] mOCounts;

    /// <summary>The slots' counts by the inventory page's one rule (UWInventoryView).</summary>
    private readonly UWInventoryView mOView = new UWInventoryView();

    private Image[] mORules;

    private Text[] mOStatLabels;

    private Text[] mOStatValues;

    private Text mOSkillsHeading;

    private Text mOSkillPoints;

    private Text[] mOSkillNames;

    private Text[] mOSkillValues;

    /// <summary>What the Character tab says (UWCharacterSheet, readable).</summary>
    private readonly UWCharacterSheet mOSheet = new UWCharacterSheet(UWCharacterSheet.SpellingEnum.Readable);

    /// <summary>The figure without its copper (UWHudArt.ComposePaperdoll) for a back of the panel's
    /// own, where it lies on MAIN.BYT, stamped with the body, the armour and the colour help it was
    /// made from - UWVersionedTexture.</summary>
    private readonly UWVersionedTexture mOFreeBody = new UWVersionedTexture();

    private Vector2Int mOFreeBodyAt;

    private Texture2D mOHandleTexture;

    /// <summary>The page, built for the backs' and colours' version with the handle, tab and pin
    /// (fEnsureArt) - its stamp says whether they are current.</summary>
    private readonly UWVersionedTexture mOPageArt = new UWVersionedTexture();

    /// <summary>Sleep (left of the feet) and Track (right of them): the slot circles, the pictures,
    /// their places on the screen and the name shown under the pointer.</summary>
    private RawImage[] mOFootSlots;

    private RawImage[] mOFootIcons;

    private Text mOFootTip;

    private Texture2D mOSlotTexture;

    private readonly Rect[] mOFootRects = new Rect[2];

    private static readonly string[] msFootNames = { "Sleep", "Track" };

    /// <summary>The names shown over the two, by their ids in the text table (UWText, cleanup step
    /// 8.5, 2026-10-24); msFootNames stays the objects' names.</summary>
    private static readonly string[] msFootTipIds = { "hud.panel.sleep", "hud.panel.track" };

    /// <summary>The bedroll and the leather boots (OBJECTS.GR).</summary>
    private static readonly int[] miFootObjects = { 289, 41 };

    /// <summary>On the page: the buttons' top row and their distance from the page's sides.</summary>
    private const int FootButtonTop = 60;

    /// <summary>Sleep sits two further right, off the page's frame (per user, 2026-10-04).</summary>
    private const int SleepExtraInset = 2;

    private const int FootButtonInset = 3;

    private TabEnum meTab = TabEnum.Character;

    /// <summary>The panel's current width - it glides towards its target as how far it is out
    /// (UWModernDockedPanel.mfShown) does.</summary>
    private float mfWidth;

    /// <summary>Pinned with the rows for the minimap in the head.</summary>
    private bool mbHead;

    // Screen rectangles (pixels, bottom-left origin) of the last layout, for the clicks (the
    // panel's, the handle's, the pin's and the tabs' are UWModernDockedPanel's).
    private Rect mOHeadRect;

    private Rect mOPageRect;

    private Rect mOHelpArea;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        fDestroyTextures();
    }

    private void Start()
    {
        mOUi = GetComponent<UWGameUI>();
        mOCharacter = GetComponent<UWCharacter>();
        mOHelp = GetComponent<UWHelpWindow>();
        mOScheme = GetComponentInParent<UWControlScheme>();
    }

    private void fDestroyTextures()
    {
        fDestroyChromeArt();
        mOPageArt.Release();

        if (mOHandleTexture != null)
            Destroy(mOHandleTexture);

        mOHandleTexture = null;

        mOFreeBody.Release();

        if (mOSlotTexture != null)
            Destroy(mOSlotTexture);

        mOSlotTexture = null;

        mOLowestRows.Clear();
    }

    /// <summary>Whether the panel holds the help (its Help tab takes Tab and draws the help): in the
    /// modern scheme, but not in Classic Wide, where the frame's panel is the original's and the
    /// help window takes Tab itself (per user, 2026-10-21: Tab did nothing there).</summary>
    public static bool HoldsHelp => UWModernUi.IsActive && Instance != null && !UWModernLayout.IsClassic;

    protected override bool fIsShown()
    {
        // In a conversation as well, for the trade (UWModernConversation).
        // Not in Classic Wide: the frame's panel is the original's (UWModernClassicFrame).
        return UWModernUi.IsActive && UWModernUi.IsShowingOrTalking
            && mOUi != null && mOUi.mOUWData != null && mOCharacter != null && !UWModernLayout.IsClassic;
    }

    private DataImport fData()
    {
        return mOUi != null ? mOUi.mOUWData : null;
    }

    // ------------------------------------------------- For the bags and the help

    /// <summary>The panel's left edge with its handle, in screen pixels: the bags keep left of it.</summary>
    public float LeftEdge { get; private set; } = float.MaxValue;

    /// <summary>The open panel on the screen with its tab row, empty while it is shut - the
    /// minimap hides under it, the bags keep beside it.</summary>
    public Rect OpenRect => fIsShown() && !fHiddenForPage() && mbOpen && mfShown > 0.5f && mOPanelRect.width > 0f
        ? new Rect(mOPanelRect.x, mOPanelRect.y, mOPanelRect.width, mOPanelRect.height + ((TabHeaderHeight - 2) * miScale))
        : Rect.zero;

    /// <summary>Whether the Character tab is out, its paperdoll taking things.</summary>
    private bool fIsPageOut()
    {
        return fIsShown() && !fHiddenForPage() && mbOpen && meTab == TabEnum.Character && mfShown > mfWidth * 0.5f;
    }

    /// <summary>With the original's character page (UWModernInventoryPage) this panel only shows
    /// the help (per user, 2026-10-10: the original panels instead of ours).</summary>
    private bool fHiddenForPage()
    {
        return UWModernLayout.IsCharacterPageShown && !(mbOpen && meTab == TabEnum.Help);
    }

    /// <summary>The minimap's place in the pinned panel's head (screen pixels, bottom-left
    /// origin) - while the Character tab is out at its own width and the screen has the rows.</summary>
    public bool TryGetMinimapArea(out Rect pOArea)
    {
        pOArea = mOHeadRect;

        return fIsShown() && mOHeadRect.width > 0f;
    }

    /// <summary>The help's area inside the open panel (screen pixels, bottom-left origin) - only
    /// while the Help tab is fully out.</summary>
    public bool TryGetHelpArea(out Rect pOArea)
    {
        pOArea = mOHelpArea;

        return fIsShown() && mbOpen && meTab == TabEnum.Help && mfShown >= mfWidth - 1f && mOHelpArea.width > 0f;
    }

    /// <summary>The equipment slot of the paperdoll under a screen point, by the original's table
    /// of the inventory page (UWClickRules.InventorySpotAt) - only while the Character tab is out.</summary>
    public bool TryGetEquipSlotAt(Vector2 pOPointer, out UWArmorItemMap.BodySlot peSlot)
    {
        peSlot = UWArmorItemMap.BodySlot.Helmet;

        if (!fIsPageOut() || !mOPageRect.Contains(pOPointer) || !mOViewRect.Contains(pOPointer))
            return false;

        RectInt lOPage = UWModernHudArt.PaperdollPage;
        int liX = UWModernHudArt.PageColumnToMain(Mathf.FloorToInt((pOPointer.x - mOPageRect.xMin) / miScale));
        int liRow = lOPage.y + Mathf.FloorToInt((mOPageRect.yMax - pOPointer.y) / miScale);

        // The table counts y from the bottom of the 320 x 200 screen.
        UWArmorItemMap.BodySlot? leSlot = UWClickRules.BodySlotOf(UWClickRules.InventorySpotAt(liX, 199 - liRow));

        if (!leSlot.HasValue)
            return false;

        peSlot = leSlot.Value;
        return true;
    }

    // ------------------------------------------------- Opening and closing

    public void Open(TabEnum peTab)
    {
        mbOpen = true;
        fSetTab(peTab);
    }

    /// <summary>Closes the panel; a thing on the pointer goes back first (UWModernBags) unless the
    /// bags stay open and keep it. A pinned panel goes back to its Character tab.</summary>
    public override void Close()
    {
        if (!mbOpen)
            return;

        if (Pinned)
        {
            if (meTab == TabEnum.Character)
                return;

            fSetTab(TabEnum.Character);
            return;
        }

        UWModernBags lOBags = UWModernBags.Instance;

        if (lOBags != null && !lOBags.IsOpen && !lOBags.ReturnCursorItem())
            return;

        mbOpen = false;

        if (meTab == TabEnum.Help && mOHelp != null)
            mOHelp.SetOpenFromPanel(false);
    }

    private void fSetTab(TabEnum peTab)
    {
        meTab = peTab;

        if (mOHelp != null)
            mOHelp.SetOpenFromPanel(mbOpen && peTab == TabEnum.Help);
    }

    /// <summary>Pins or unpins the panel by its pin; unpinned it stays out, now as an opened panel.</summary>
    protected override void fSetPinnedSetting(bool pbPinned)
    {
        UWUserSettings.ModernPanelPinned = pbPinned;
        UWUserSettings.Save();
    }

    /// <summary>The handle or the closed panel's edge opens it on the Character tab.</summary>
    protected override void fOpenByClick()
    {
        Open(TabEnum.Character);
    }

    // ------------------------------------------------- Input

    private PanelKeys mOKeys;

    /// <summary>The panel's hotkey (UWHotkeys): Tab opens and closes its Help tab.</summary>
    private sealed class PanelKeys : UWHotkeyHost
    {
        private readonly UWModernPanel mOPanel;

        public PanelKeys(UWModernPanel pOPanel)
        {
            mOPanel = pOPanel;
        }

        protected override UWControls Controls => mOPanel.mOScheme != null ? mOPanel.mOScheme.Controls : null;

        public override bool Gate(UWHotkeyGate peGate, UWHotkeyRow pORow)
        {
            switch (peGate)
            {
                case UWHotkeyGate.MenuOpen: return UWModernUi.IsMenuOpen;
                case UWHotkeyGate.KeysLocked: return UWControls.IsTextEntryActive;
                case UWHotkeyGate.HelpKeys: return UWHelpWindow.BlocksGameKeys;
                case UWHotkeyGate.MapVisible: return mOPanel.mOUi.Map.IsMapVisible;
                case UWHotkeyGate.SetupPanel: return UWSetupMenu.HasOpenPanel;
                default: return false;
            }
        }

        public override bool Run(UWHotkeyHandler peHandler)
        {
            if (peHandler != UWHotkeyHandler.TogglePanelHelp)
                return false;

            if (mOPanel.mbOpen && mOPanel.meTab == TabEnum.Help)
                mOPanel.Close();
            else
                mOPanel.Open(TabEnum.Help);

            return true;
        }
    }

    private void Update()
    {
        if (!fIsShown())
        {
            if (mbOpen && !UWModernUi.IsActive)
                fGiveUp();

            mbPeek = false;
            return;
        }

        // Pinned: out on the Character tab. The help is left alone -
        // restored open with a save game, the check below turns to it.
        if (Pinned && !mbOpen && !UWModernLayout.IsCharacterPageShown)
        {
            mbOpen = true;
            meTab = TabEnum.Character;
        }

        if (UWModernUi.IsMenuOpen || UWControls.IsTextEntryActive || UWHelpWindow.BlocksGameKeys || mOUi.Map.IsMapVisible)
            return;

        UWControls lOControls = mOScheme.Controls;

        if (lOControls == null)
            return;

        // TAB: the Help tab, not with Alt (switching programs) or Shift - as the help window,
        // and not under a setup-menu panel (the Game-folder dialog, UWSetupMenu).
        // Performed, not pressed: the pad's View opens the help when HELD (UWControls). The row is
        // UWHotkeys' since cleanup step 7.5 (its gates repeat the return above).
        if (mOKeys == null)
            mOKeys = new PanelKeys(this);

        UWHotkeys.Route(UWHotkeySite.ModernPanel, mOKeys);

        // The help opened elsewhere (restored with a save game): the panel shows it.
        if (UWHelpLayout.IsOpen && !(mbOpen && meTab == TabEnum.Help))
            Open(TabEnum.Help);

        mbPeek = false;

        if (!UWPointer.HasMouse || !mOScheme.IsPointerFree)
            return;

        Vector2 lOPointer = UWPointer.Position;

        fUpdatePeek(lOPointer);

        // The wheel scrolls the Character tab where it does not fit.
        float lfWheel = UWPointer.Scroll.y;

        if (mbOpen && meTab == TabEnum.Character && Mathf.Abs(lfWheel) > 0.01f && mOViewRect.Contains(lOPointer))
            mfScroll -= Mathf.Sign(lfWheel) * ScrollStep;

        if (!lOControls.Player.CursorDrag.WasPressedThisFrame() || UWModernPointer.PanelsHeld)
            return;

        // The pin, the handle, the closed panel's edge (UWModernDockedPanel).
        if (fClickChrome(lOPointer))
            return;

        // Sleep and Track beside the paperdoll's feet - not with a thing on the pointer.
        for (int liFoot = 0; mbOpen && meTab == TabEnum.Character && liFoot < mOFootRects.Length; liFoot++)
        {
            if (!mOFootRects[liFoot].Contains(lOPointer) || !mOViewRect.Contains(lOPointer)
                || (mOUi.mOInventory != null && mOUi.mOInventory.CursorItem != null))
                continue;

            if (liFoot == 0 && UWGameActions.CanCamp)
                mOUi.Actions.Camp();
            else if (liFoot == 1 && mOUi.Runes != null)
                mOUi.Actions.Track();

            return;
        }

        int liTab = mbOpen ? fTabAt(lOPointer) : -1;

        if (liTab >= 0)
            fSetTab((TabEnum)liTab);
    }

    /// <summary>Leaving the modern scheme: the pointer goes back, the help stays as it is.</summary>
    private void fGiveUp()
    {
        mbOpen = false;
    }

    // ------------------------------------------------- Drawing

    private void LateUpdate()
    {
        if (!fIsShown() || fHiddenForPage())
        {
            if (mOCanvas != null)
                mOCanvas.enabled = false;

            LeftEdge = float.MaxValue;
            mOPanelRect = Rect.zero;
            mOHandleRect = Rect.zero;
            mOPinRect = Rect.zero;
            mOHeadRect = Rect.zero;
            mOHelpArea = Rect.zero;
            mOViewRect = Rect.zero;
            return;
        }

        if (mOCanvas == null)
            fBuild();

        mOCanvas.enabled = true;
        // Its own size (UWModernLayout - docked, never moved).
        miScale = UWModernUi.PixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.CharacterPanel);

        fEnsureArt();

        float liScale = miScale;
        float lfHelpWidth = Mathf.Round(Mathf.Min(UWHelpLayout.MaxWidthPerHeight * Screen.height,
            Screen.width * UWHelpLayout.OverlayShare) / liScale) * liScale;
        float lfTargetWidth = meTab == TabEnum.Help ? Mathf.Max(lfHelpWidth, CharacterWidth * liScale) : CharacterWidth * liScale;
        // Moved in the layout editor it is a free window: no handle, no peeking, no sliding.
        bool lbFree = UWModernLayout.IsPlaced(UWModernLayout.ElementEnum.CharacterPanel);
        float lfTargetShown = fTargetShown(lfTargetWidth, lbFree);
        float lfStep = fSlideStep();
        bool lbPinned = Pinned;

        // The rows: the Help tab the screen's, the Character tab what it needs - pinned with the
        // minimap in its head when that fits too.
        int liRoom = Mathf.Max(60, Mathf.FloorToInt(Screen.height / liScale) - TopMargin - BottomMargin);

        // NO MINIMAP IN THE HEAD any more (per user, 2026-10-04: it went there by itself with the
        // panel pinned; with the layout editor the minimap is placed freely instead).
        mbHead = false;

        int liTargetRows = meTab == TabEnum.Help ? liRoom : Mathf.Min(liRoom, fCharacterRows(mbHead));

        if (mfWidth <= 0f)
            mfWidth = lfTargetWidth;

        if (mfRows <= 0f)
            mfRows = liTargetRows;

        mfWidth = fGlide(mfWidth, lfTargetWidth, lfStep, 1f);
        mfShown = lbFree ? lfTargetShown : fGlide(mfShown, lfTargetShown, lfStep, 1f);
        mfRows = fGlide(mfRows, liTargetRows, lfStep, 0.5f);

        // Whole original pixels, so the leather stays sharp; hung from the top.
        int liWidth = Mathf.Max(CharacterWidth, Mathf.RoundToInt(mfWidth / liScale));
        int liHeight = Mathf.Max(60, Mathf.RoundToInt(mfRows));
        float lfLeft = Mathf.Round(Screen.width - mfShown);
        float lfTop = Mathf.Round(Screen.height - (TopMargin * liScale));
        float lfBottom = lfTop - (liHeight * liScale);
        float lfTabRise = (TabHeaderHeight - 2) * liScale;

        if (lbFree)
        {
            // Where it was put (UWModernLayout), its tab row above it kept on the screen.
            Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.CharacterPanel,
                new Rect(Screen.width - (liWidth * liScale), lfBottom, liWidth * liScale, (liHeight * liScale) + lfTabRise));

            lfLeft = lOPlaced.x;
            lfBottom = lOPlaced.y;
            lfTop = lOPlaced.yMax - lfTabRise;
        }

        mOPanelRect = lbFree && !mbOpen ? Rect.zero : new Rect(lfLeft, lfBottom, liWidth * liScale, liHeight * liScale);

        // The leather.
        fLayoutBack(liWidth, liHeight, lfLeft, lfBottom);

        // The handle, out of the panel's left edge and over its frame there - not when pinned.
        float lfHandleBottom = Screen.height - ((HandleTop + HandleHeight) * liScale);
        Rect lOHandle = new Rect(lfLeft - (HandleWidth * liScale), lfHandleBottom, HandleWidth * liScale, HandleHeight * liScale);

        fLayoutHandle(lOHandle, lbPinned, lbFree);
        // A free window leaves the right edge to the others (the bags, the spells, the conversation).
        LeftEdge = lbFree ? float.MaxValue : lbPinned ? lfLeft : lOHandle.xMin;

        // For the layout editor: the panel as far as it is out, with its handle.
        if (lbFree)
        {
            if (mbOpen)
                UWModernLayout.Report(UWModernLayout.ElementEnum.CharacterPanel,
                    new Rect(lfLeft, lfBottom, liWidth * liScale, (lfTop + lfTabRise) - lfBottom));
        }
        else
        {
            UWModernLayout.Report(UWModernLayout.ElementEnum.CharacterPanel, mfShown > 0.5f
                ? Rect.MinMaxRect(Mathf.Min(lfLeft, mOHandleRect.width > 0f ? mOHandleRect.xMin : lfLeft), lfBottom, Screen.width, lfTop)
                : lOHandle);
        }

        // The tab headers over the top edge, once the panel is out, and the pin at the right end of
        // the row (UWModernDockedPanel).
        fLayoutTabRow(lfLeft, lfTop, liWidth, (int)meTab, lbPinned);

        // The help's area, inside the frame.
        mOHelpArea = new Rect(lfLeft + (ContentPad * liScale), lfBottom + (ContentPad * liScale),
            (liWidth - (2 * ContentPad)) * liScale, (liHeight - (2 * ContentPad)) * liScale);

        bool lbCharacter = meTab == TabEnum.Character && mfShown > 0.5f;

        // The minimap's place in the head, once the panel has its own width again.
        bool lbHead = lbCharacter && mbOpen && mbHead && Mathf.Abs(mfWidth - (CharacterWidth * liScale)) < 1f;

        Rect lOHeadPlace = new Rect(lfLeft + (UWModernHudArt.LeatherLeft * liScale),
            lfTop - ((UWModernHudArt.LeatherTop + HeadSize) * liScale), HeadSize * liScale, HeadSize * liScale);

        mOHeadRect = lbHead ? lOHeadPlace : Rect.zero;

        mOViewport.gameObject.SetActive(lbCharacter);

        if (lbCharacter)
        {
            // The clipped view inside the frame, below the head; the content scrolls in it.
            float lfViewTop = lfTop - ((mbHead ? HeadRows : UWModernHudArt.LeatherTop) * liScale);
            float lfViewBottom = lfBottom + (UWModernHudArt.LeatherBottom * liScale);
            int liNeeded = fCharacterRows(mbHead);
            float lfMaxScroll = Mathf.Max(0f, liNeeded - liHeight);

            mfScroll = Mathf.Clamp(mfScroll, 0f, lfMaxScroll);
            mOViewRect = new Rect(lfLeft + (UWModernHudArt.LeatherLeft * liScale), lfViewBottom,
                (liWidth - UWModernHudArt.LeatherLeft - UWModernHudArt.LeatherRight) * liScale, Mathf.Max(0f, lfViewTop - lfViewBottom));
            UWUiKit.SetRect(mOViewport, mOViewRect.x, mOViewRect.y, mOViewRect.width, mOViewRect.height);

            // The content keeps screen coordinates inside the view.
            mOContent.anchoredPosition = -mOViewRect.position;

            fLayoutCharacter(lfLeft, lfTop - ((mbHead ? HeadRows : 0) * liScale) + (mfScroll * liScale), liWidth);

            // The bar: where the view lies in the whole tab.
            mOScrollBar.enabled = lfMaxScroll > 0f;

            if (lfMaxScroll > 0f)
            {
                float lfBar = mOViewRect.height * (liHeight / (float)liNeeded);
                float lfBarTop = mOViewRect.yMax - ((mfScroll / liNeeded) * mOViewRect.height);

                UWUiKit.SetRect(mOScrollBar.rectTransform, mOViewRect.xMax - (1.5f * liScale), lfBarTop - lfBar, Mathf.Max(2f, liScale * 0.75f), lfBar);
            }
        }
        else
        {
            mOPageRect = Rect.zero;
            mOViewRect = Rect.zero;
            mOScrollBar.enabled = false;
            mOFootRects[0] = Rect.zero;
            mOFootRects[1] = Rect.zero;
            mOFootTip.enabled = false;
        }
    }

    /// <summary>The rows the Character tab needs, the frame included - with the minimap's head
    /// or without.</summary>
    private static int fCharacterRows(bool pbHead)
    {
        return (pbHead ? HeadRows : 0) + PageTop + UWModernHudArt.PaperdollPage.height
            + BelowPageRows + Mathf.CeilToInt(SkillRows * SkillRowHeight) + UWModernHudArt.LeatherBottom + 1;
    }

    /// <summary>What the name was last fitted for - see fLayoutCharacter.</summary>
    private string msNameFitKey;

    /// <summary>The width the name may take beside the class, in whole pixels.</summary>
    private float fNameRoom(float pfInner, float pfScale)
    {
        float lfClass = string.IsNullOrEmpty(mOClass.text) ? 0f : mOClass.preferredWidth + (2f * pfScale);

        return Mathf.Max(0f, Mathf.Floor(pfInner - lfClass));
    }

    /// <summary>Makes the name smaller, down to piSmallest, until it fits beside the class;
    /// whether it does.</summary>
    private bool fShrinkNameToFit(float pfInner, int piSmallest, float pfScale)
    {
        float lfNameRoom = fNameRoom(pfInner, pfScale);

        while (mOName.fontSize > piSmallest && mOName.preferredWidth > lfNameRoom)
            mOName.fontSize--;

        return mOName.preferredWidth <= lfNameRoom;
    }

    /// <summary>The Character tab, laid out from the panel's top-left corner.</summary>
    private void fLayoutCharacter(float pfLeft, float pfTop, int piWidth)
    {
        float liScale = miScale;
        DataImport lOData = fData();
        UWPlayerData lOPlayer = lOData.InitialPlayer;
        UWCharacter lOCharacter = mOCharacter;
        int liFont = Mathf.Max(10, Mathf.RoundToInt(4f * liScale));
        int liSmall = Mathf.Max(9, Mathf.RoundToInt(3.4f * liScale));
        float lfInner = (piWidth - (2 * ContentPad)) * liScale;
        float lfX = pfLeft + (ContentPad * liScale);

        // What the tab says: UWCharacterSheet, readable (title case, "level N", "a / b"; cleanup
        // 5.9, 2026-10-20).
        UWCharacterSheet lOSheet = mOSheet.Bind(lOData.Strings, lOPlayer, lOCharacter.Vitals);

        // Name, class and level - one line, the name on the left, the class on the right.
        string lsClassName = lOSheet.ClassName;
        string lsClassLine = lOSheet.ClassWithLevel;
        mOClass.fontSize = liSmall;
        UWUiKit.SetRect(mOClass.rectTransform, lfX, pfTop - ((NameTop + 9) * liScale), lfInner, 9 * liScale);

        string lsName = lOSheet.Name;
        int liLargest = Mathf.Max(12, Mathf.RoundToInt(6f * liScale));

        // A name of up to 29 characters (since 2026-10-12, UWPlayerData.MaxNameLength) may not fit
        // beside the class (per user, 2026-10-13: below 100 % interface scale the class covered
        // the name). The name gets smaller, down to the class line's size; still too wide, the
        // class gives way first - without the word "level", then without the class name, the
        // number stays - and only then is the name cut with "..." (per user the same day).
        // FITTED ONCE PER CHANGE, in whole pixels (per user the same day: at one window size the
        // name flipped between cut and whole every frame - the measurement sat on the edge).
        string lsFitKey = lsName + "|" + lsClassLine + "|" + Mathf.RoundToInt(lfInner) + "|" + liSmall + "|" + liLargest;

        if (lsFitKey != msNameFitKey)
        {
            msNameFitKey = lsFitKey;
            mOName.text = lsName;

            string lsLevel = lOSheet.LevelText;

            foreach (string lsClass in new[] { lsClassLine, lsClassName + " " + lsLevel, lsLevel })
            {
                mOClass.text = lsClass;
                mOName.fontSize = liLargest;

                if (fShrinkNameToFit(lfInner, liSmall, liScale))
                    break;
            }

            float lfNameRoom = fNameRoom(lfInner, liScale);

            for (int liKeep = lsName.Length - 1; liKeep > 0 && mOName.preferredWidth > lfNameRoom; liKeep--)
                mOName.text = lsName.Substring(0, liKeep).TrimEnd() + "...";
        }

        UWUiKit.SetRect(mOName.rectTransform, lfX, pfTop - ((NameTop + 9) * liScale), lfInner, 9 * liScale);

        // The page, centred - as wide as the backpack below it, which it docks onto.
        RectInt lOPage = UWModernHudArt.PaperdollPage;
        float lfPageLeft = pfLeft + (((piWidth - UWModernHudArt.PaperdollPageWidth) / 2) * liScale);
        float lfPageTop = pfTop - (PageTop * liScale);

        mOPageRect = new Rect(lfPageLeft, lfPageTop - (lOPage.height * liScale), UWModernHudArt.PaperdollPageWidth * liScale,
            lOPage.height * liScale);
        mOPage.texture = mOPageArt.Texture;
        mOPage.enabled = true;
        UWUiKit.SetRect(mOPage.rectTransform, mOPageRect.x, mOPageRect.y, mOPageRect.width, mOPageRect.height);

        // Body and armour in the classic order (UWGameUI.fBuildCanvas), put together as one picture
        // and freed from the page they were painted on. On a back of the panel's own (per user,
        // 2026-10-09: it stood in a box of copper, and helmets carried the page with them) - and
        // since 2026-10-10 on the leather as well: the body picture is a rectangle of the page with
        // the two ring holes at its edges, and narrowed to the original's proportion they stood
        // beside the page's own, twice on each side (per user).
        {
            // Built anew when the body, the armour (UWPaperdollView.LookVersion) or the colour
            // help changed (per user 2026-10-19: the paperdoll did not follow the colour filter;
            // the key was the pieces alone, built as a string every frame).
            UWArtStamp lOFigureStamp = UWVersionedTexture.Colours(lOCharacter.CharIndex, lOCharacter.Paperdoll.LookVersion);

            if (!mOFreeBody.IsCurrent(lOFigureStamp))
            {
                UWTextures lOTextures = fData().Textures;
                UWTexture[] lOPieces =
                {
                    lOCharacter.Paperdoll.HelmetSource, lOCharacter.Paperdoll.GlovesSource, lOCharacter.Paperdoll.LegsSource, lOCharacter.Paperdoll.ChestSource, lOCharacter.Paperdoll.BootsSource
                };

                mOFreeBody.Set(lOFigureStamp, UWModernHudArt.BuildFreeFigure(lOTextures, lOCharacter.CharIndex, lOPieces,
                    new[] { msHelmetAt, msGlovesAt, msLegsAt + new Vector2Int(0, lOCharacter.Paperdoll.LegsDrop), msChestAt, msBootsAt }, mOUi.TextureFilterMode,
                    out mOFreeBodyAt));
            }

            fPlaceOnPage(mOArmour[0], mOFreeBody.Texture, mOFreeBodyAt);

            for (int liPart = 1; liPart < mOArmour.Length; liPart++)
                mOArmour[liPart].enabled = false;
        }

        // The things in the hands, on the shoulders and on the fingers.
        UWInventory lOInventory = mOUi.mOInventory;

        mOView.Refresh(lOInventory != null ? lOInventory.Model : null, mOUi.mOUWData);

        for (int liSlot = 0; liSlot < UWPaperdollLayout.IconSlots.Length; liSlot++)
        {
            (UWArmorItemMap.BodySlot leSlot, float lfSlotX, float lfSlotY, float lfSize) = UWPaperdollLayout.IconSlots[liSlot];
            UWObject lOItem = lOInventory != null ? lOInventory.GetEquipped(leSlot) : null;
            UWTexture lOSource = lOItem != null ? lOItem.Icon ?? lOItem.Texture : null;
            RawImage lOIcon = mOIcons[liSlot];

            if (lOSource == null)
            {
                lOIcon.enabled = false;
                mOCounts[liSlot].enabled = false;
                continue;
            }

            Texture2D lOTexture = fGetIcon(lOSource);
            float lfCentreX = lfSlotX + (lfSize * 0.5f) - lOPage.x + UWModernHudArt.PaperdollShift;
            float lfCentreRow = lfSlotY + (lfSize * 0.5f) - lOPage.y - (lfSize < 16f ? UWPaperdollLayout.RingIconYOffset : 0f);

            lOIcon.texture = lOTexture;
            lOIcon.enabled = true;
            UWUiKit.SetRect(lOIcon.rectTransform, Mathf.Round(mOPageRect.xMin + ((lfCentreX - (lOTexture.width * UWModernHudArt.PixelAspectX * 0.5f)) * liScale)),
                Mathf.Round(mOPageRect.yMax - ((lfCentreRow + (lOTexture.height * 0.5f)) * liScale)),
                lOTexture.width * UWModernHudArt.PixelAspectX * liScale, lOTexture.height * liScale);

            // THE STACK'S COUNT at the slot's bottom right (per user, 2026-10-20: the modern
            // Character tab showed none in the hands, the classic panel and the original page do).
            UWInventoryView.Slot lOSlot = mOView.Hands[liSlot];
            Text lOCount = mOCounts[liSlot];

            lOCount.enabled = lOSlot.ShowsCount;

            if (lOSlot.ShowsCount)
            {
                float lfRight = lfSlotX + Mathf.Max(lfSize, 16f) - lOPage.x + UWModernHudArt.PaperdollShift;
                float lfBottomRow = lfSlotY + Mathf.Max(lfSize, 16f) - lOPage.y;

                lOCount.text = lOSlot.Count.ToString();
                lOCount.fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * liScale));
                UWUiKit.SetRect(lOCount.rectTransform, mOPageRect.xMin + (lfRight * liScale) - (40f * liScale) + liScale,
                    mOPageRect.yMax - (lfBottomRow * liScale) - liScale, 40f * liScale, 10f * liScale);
            }
        }

        fLayoutFootButtons();

        // A rule, the attributes, a rule, the skills - right below the page; the backpack stays
        // with the bags.
        float lfY = mOPageRect.yMin - (3 * liScale);

        fSetRule(mORules[0], lfX, lfY, lfInner);
        lfY -= 3 * liScale;

        float lfColumn = lfInner / 3f;

        for (int liStat = 0; liStat < mOStatLabels.Length; liStat++)
        {
            float lfStatX = lfX + ((liStat % 3) * lfColumn);
            float lfStatY = lfY - ((liStat / 3) * 12 * liScale);

            // The label read on every pass as the values are (a language switched in the game shows at
            // once, translation step 3R; Text keeps an unchanged string as it is).
            mOStatLabels[liStat].text = UWCharacterSheet.ValueLabel(liStat);
            mOStatLabels[liStat].fontSize = liSmall;
            UWUiKit.SetRect(mOStatLabels[liStat].rectTransform, lfStatX, lfStatY - (5 * liScale), lfColumn, 5 * liScale);

            mOStatValues[liStat].text = lOSheet.Value(liStat);
            mOStatValues[liStat].fontSize = liFont;
            UWUiKit.SetRect(mOStatValues[liStat].rectTransform, lfStatX, lfStatY - (11 * liScale), lfColumn, 6 * liScale);
        }

        lfY -= 26 * liScale;
        fSetRule(mORules[1], lfX, lfY, lfInner);
        lfY -= 3 * liScale;

        mOSkillsHeading.text = UWText.T("hud.panel.skills");
        mOSkillsHeading.fontSize = liFont;
        UWUiKit.SetRect(mOSkillsHeading.rectTransform, lfX, lfY - (7 * liScale), lfInner, 7 * liScale);

        mOSkillPoints.text = lOSheet.SkillPointsText;
        mOSkillPoints.enabled = lOSheet.SkillPoints > 0;
        mOSkillPoints.fontSize = liSmall;
        UWUiKit.SetRect(mOSkillPoints.rectTransform, lfX, lfY - (7 * liScale), lfInner, 7 * liScale);

        lfY -= 8 * liScale;

        // Attack and Defense first, then the eighteen skills - the order of the classic list.
        float lfSkillColumn = (lfInner - (2 * liScale)) / 2f;

        for (int liEntry = 0; liEntry < mOSkillNames.Length; liEntry++)
        {
            float lfSkillX = lfX + ((liEntry / SkillRows) * (lfSkillColumn + (2 * liScale)));
            float lfSkillY = lfY - (((liEntry % SkillRows) + 1) * SkillRowHeight * liScale);

            if (string.IsNullOrEmpty(mOSkillNames[liEntry].text))
                mOSkillNames[liEntry].text = lOSheet.SkillName(liEntry);

            mOSkillNames[liEntry].fontSize = liSmall;
            UWUiKit.SetRect(mOSkillNames[liEntry].rectTransform, lfSkillX, lfSkillY, lfSkillColumn - (4 * liScale), SkillRowHeight * liScale);

            mOSkillValues[liEntry].text = lOSheet.SkillValue(liEntry);
            mOSkillValues[liEntry].fontSize = liSmall;
            UWUiKit.SetRect(mOSkillValues[liEntry].rectTransform, lfSkillX, lfSkillY, lfSkillColumn - (4 * liScale), SkillRowHeight * liScale);
        }
    }

    /// <summary>Sleep left of the feet, Track right of them, on the page's lower corners.</summary>
    private void fLayoutFootButtons()
    {
        float liScale = miScale;
        float lfSlot = UWModernHudArt.SlotSize * liScale;
        float lfTop = mOPageRect.yMax - ((FootButtonTop + UWModernHudArt.SlotSize) * liScale);
        Vector2 lOPointer = UWPointer.HasMouse ? UWPointer.Position : new Vector2(-1f, -1f);
        int liHovered = -1;

        for (int liFoot = 0; liFoot < 2; liFoot++)
        {
            float lfX = liFoot == 0
                ? mOPageRect.xMin + ((FootButtonInset + SleepExtraInset) * liScale)
                : mOPageRect.xMax - ((FootButtonInset + UWModernHudArt.SlotSize) * liScale);
            Rect lORect = new Rect(lfX, lfTop, lfSlot, lfSlot);

            mOFootRects[liFoot] = lORect;
            // Only the picture (per user, 2026-10-04: neither the slot circle nor a ring suited the
            // page); the name shows under the pointer.
            mOFootSlots[liFoot].enabled = false;

            UWTexture lOSource = fData().Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, miFootObjects[liFoot]);
            RawImage lOIcon = mOFootIcons[liFoot];

            lOIcon.enabled = lOSource != null;

            if (lOSource != null)
            {
                Texture2D lOTexture = fGetIcon(lOSource);

                // Standing on one line: each picture's lowest visible row on the same baseline
                // (per user, 2026-10-04: the boots sat higher than the bedroll).
                lOIcon.texture = lOTexture;
                UWUiKit.SetRect(lOIcon.rectTransform, lORect.x + ((UWModernHudArt.SlotSize - (lOTexture.width * UWModernHudArt.PixelAspectX)) * 0.5f * liScale),
                    lORect.y + ((FootBaseline - fLowestVisibleRow(lOTexture)) * liScale),
                    lOTexture.width * UWModernHudArt.PixelAspectX * liScale, lOTexture.height * liScale);
            }

            if (UWModernPointer.IsFree && lORect.Contains(lOPointer) && mOViewRect.Contains(lOPointer))
                liHovered = liFoot;
        }

        // The name over the button under the pointer.
        mOFootTip.enabled = liHovered >= 0;

        if (liHovered >= 0)
        {
            Rect lORect = mOFootRects[liHovered];

            mOFootTip.text = UWText.T(msFootTipIds[liHovered]);
            mOFootTip.fontSize = Mathf.Max(9, Mathf.RoundToInt(3.8f * liScale));
            UWUiKit.SetRect(mOFootTip.rectTransform, lORect.x - (10 * liScale), lORect.yMax, lORect.width + (20 * liScale), 7 * liScale);
        }
    }

    /// <summary>The row above the button's bottom that the foot pictures stand on.</summary>
    private const int FootBaseline = 3;

    private readonly Dictionary<Texture2D, int> mOLowestRows = new Dictionary<Texture2D, int>();

    /// <summary>The lowest row of a picture with a visible pixel, counted from its bottom.</summary>
    private int fLowestVisibleRow(Texture2D pOTexture)
    {
        if (mOLowestRows.TryGetValue(pOTexture, out int liRow))
            return liRow;

        liRow = 0;

        if (pOTexture.isReadable)
        {
            Color32[] lyPixels = pOTexture.GetPixels32();
            bool lbFound = false;

            for (int liY = 0; liY < pOTexture.height && !lbFound; liY++)
            {
                for (int liX = 0; liX < pOTexture.width; liX++)
                {
                    if (lyPixels[(liY * pOTexture.width) + liX].a > 0)
                    {
                        liRow = liY;
                        lbFound = true;
                        break;
                    }
                }
            }
        }

        mOLowestRows[pOTexture] = liRow;
        return liRow;
    }

    /// <summary>
    /// THE FIGURE AT THE ORIGINAL'S PROPORTION (per user, 2026-10-09: next to the original with its
    /// 4:3 fix ours looked "chubbier" - the modern scheme draws square pixels, the original's are 1.2
    /// times taller than wide on the screen it was made for). The same proportion without changing
    /// the panel's height: the figure and its armour are drawn 1/1.2 as wide, around the figure's
    /// middle (FigureAxisX); the page, its circles and the things in them stay as they are.
    /// </summary>
    private const float FigureWidthFactor = UWModernHudArt.PixelAspectX;

    /// <summary>The figure's middle on MAIN.BYT (the body picture's, 260 + 36 / 2).</summary>
    private const float FigureAxisX = 278f;

    /// <summary>A picture of the paperdoll's figure at its place in MAIN.BYT pixels, narrowed to the
    /// original's proportion (FigureWidthFactor); an empty slot's placeholder (1 x 1) is not
    /// shown.</summary>
    private void fPlaceOnPage(RawImage pOImage, Texture2D pOTexture, Vector2Int pOAt)
    {
        if (pOTexture == null || pOTexture.width <= 1)
        {
            pOImage.enabled = false;
            return;
        }

        RectInt lOPage = UWModernHudArt.PaperdollPage;
        float liScale = miScale;

        pOImage.texture = pOTexture;
        pOImage.enabled = true;
        float lfLeft = FigureAxisX + ((pOAt.x - FigureAxisX) * FigureWidthFactor);

        UWUiKit.SetRect(pOImage.rectTransform, mOPageRect.xMin + ((lfLeft - lOPage.x + UWModernHudArt.PaperdollShift) * liScale),
            mOPageRect.yMax - ((pOAt.y - lOPage.y + pOTexture.height) * liScale),
            pOTexture.width * liScale * FigureWidthFactor, pOTexture.height * liScale);
    }

    private void fSetRule(Image pORule, float pfX, float pfY, float pfWidth)
    {
        UWUiKit.SetRect(pORule.rectTransform, pfX, pfY, pfWidth, Mathf.Max(2f, miScale * 0.75f));
    }

    private Texture2D fGetIcon(UWTexture pOSource)
    {
        return UWIconCache.Texture(pOSource, mOUi.TextureFilterMode);
    }

    private void fEnsureArt()
    {
        UWArtStamp lOStamp = UWVersionedTexture.Backs();

        if (mOPageArt.IsCurrent(lOStamp))
            return;

        fDestroyTextures();

        UWTextures lOTextures = fData().Textures;
        FilterMode leFilter = mOUi.TextureFilterMode;

        mOHandleTexture = UWModernHudArt.BuildPartLeatherTab(UWModernLayout.ElementEnum.CharacterPanel, lOTextures, HandleWidth + UWModernHudArt.LeatherLeft, HandleHeight, leFilter);
        mOPageArt.Set(lOStamp, UWModernHudArt.BuildPartPaperdollPage(UWModernLayout.ElementEnum.CharacterPanel, lOTextures, leFilter));
        mOSlotTexture = UWModernActionBar.BuildRing(128, 60f, 6f);

        // The tab headers, the pin and its icon (UWModernDockedPanel).
        fBuildChromeArt(lOTextures, leFilter);

        mOHandle.texture = mOHandleTexture;
    }

    /// <summary>A pushpin pointing down: a gold head with a dark rim on a grey needle - also the
    /// rune panel's (UWModernRunePanel).</summary>
    internal static Texture2D BuildPinIcon(int piSize)
    {
        Color32[] lyPixels = new Color32[piSize * piSize];
        Vector2 lOHead = new Vector2(0.5f, 0.66f);
        Vector2 lONeedleTop = new Vector2(0.5f, 0.5f);
        Vector2 lONeedleTip = new Vector2(0.5f, 0.06f);
        Color lOGold = UWModernPalette.Gold;
        Color lOShine = new Color(1f, 0.93f, 0.72f, 1f);
        Color lORim = new Color(0.16f, 0.09f, 0.04f, 1f);
        Color lOSteel = new Color(0.8f, 0.79f, 0.76f, 1f);
        float lfPixel = 1f / piSize;

        for (int y = 0; y < piSize; y++)
        {
            for (int x = 0; x < piSize; x++)
            {
                Vector2 lOAt = new Vector2((x + 0.5f) / piSize, (y + 0.5f) / piSize);

                // Distances in pixels, smoothed over one.
                float lfHead = (Vector2.Distance(lOAt, lOHead) - 0.3f) / lfPixel;
                float lfNeedle = (fDistanceToSegment(lOAt, lONeedleTop, lONeedleTip) - 0.045f) / lfPixel;
                Color lOColour = new Color(0f, 0f, 0f, 0f);

                // The needle with its rim, the head over it.
                lOColour = fOver(lOColour, lORim, Mathf.Clamp01(0.5f - (lfNeedle - 3f)));
                lOColour = fOver(lOColour, lOSteel, Mathf.Clamp01(0.5f - lfNeedle));
                lOColour = fOver(lOColour, lORim, Mathf.Clamp01(0.5f - (lfHead - 3f)));

                Color lOFill = Color.Lerp(lOShine, lOGold, Mathf.Clamp01(Vector2.Distance(lOAt, lOHead + new Vector2(-0.08f, 0.08f)) / 0.28f));

                lOColour = fOver(lOColour, lOFill, Mathf.Clamp01(0.5f - lfHead));
                lyPixels[(y * piSize) + x] = lOColour;
            }
        }

        return UWTextureFactory.FromColours(lyPixels, piSize, piSize, FilterMode.Bilinear, TextureWrapMode.Clamp, "UWModernPanel pin");
    }

    private static Color fOver(Color pOBelow, Color pOAbove, float pfCover)
    {
        float lfAlpha = pfCover + (pOBelow.a * (1f - pfCover));

        if (lfAlpha <= 0f)
            return new Color(0f, 0f, 0f, 0f);

        Color lOColour = ((pOAbove * pfCover) + (pOBelow * pOBelow.a * (1f - pfCover))) / lfAlpha;

        lOColour.a = lfAlpha;
        return lOColour;
    }

    private static float fDistanceToSegment(Vector2 pOAt, Vector2 pOA, Vector2 pOB)
    {
        Vector2 lOLine = pOB - pOA;
        float lfAlong = Mathf.Clamp01(Vector2.Dot(pOAt - pOA, lOLine) / lOLine.sqrMagnitude);

        return Vector2.Distance(pOAt, pOA + (lOLine * lfAlong));
    }

    // ------------------------------------------------- Building

    private void fBuild()
    {
        mOCanvas = UWUiKit.CreateOverlayCanvas(transform, "Modern character panel", UWUiSorting.ModernCharacterPanel);

        mORoot = (RectTransform)mOCanvas.transform;

        // The tab headers, the pin, the leather and the handle (UWModernDockedPanel).
        fBuildChrome(mORoot, new[] { "hud.panel.tab.character", "hud.panel.tab.help" }, "hud.panel.handle");
        mOHandle.enabled = true;

        mOViewport = UWUiKit.CreateClipArea(mORoot, "Character view");
        mOContent = UWUiKit.CreateGroup(mOViewport, "Character");

        mOScrollBar = UWUiKit.CreateImage(mORoot, "Scroll bar", UWModernPalette.WithAlpha(UWModernPalette.Accent, 0.85f));
        mOScrollBar.enabled = false;

        mOName = UWUiKit.CreateText(mOContent, "Name", TextAnchor.MiddleLeft, UWModernPalette.Text, mOUi);
        mOClass = UWUiKit.CreateText(mOContent, "Class", TextAnchor.MiddleRight, UWModernPalette.Dim, mOUi);

        mOPage = UWUiKit.CreateRawImage(mOContent, "Paperdoll page");
        UWPixelArtUI.Apply(mOPage);

        mOFootSlots = new RawImage[2];
        mOFootIcons = new RawImage[2];

        for (int liFoot = 0; liFoot < 2; liFoot++)
        {
            mOFootSlots[liFoot] = UWUiKit.CreateRawImage(mOContent, msFootNames[liFoot]);
            mOFootIcons[liFoot] = UWUiKit.CreateRawImage(mOContent, msFootNames[liFoot] + " picture");
            UWIconPalette.ApplySmooth(mOFootIcons[liFoot]);
        }

        mOFootTip = UWUiKit.CreateText(mORoot, "Foot button name", TextAnchor.LowerCenter, UWModernPalette.Text, mOUi);

        mOArmour = new RawImage[6];

        string[] lsArmour = { "Body", "Helmet", "Gloves", "Legs", "Chest", "Boots" };

        for (int liPart = 0; liPart < mOArmour.Length; liPart++)
        {
            mOArmour[liPart] = UWUiKit.CreateRawImage(mOContent, lsArmour[liPart]);
            UWPixelArtUI.Apply(mOArmour[liPart]);
        }

        mOIcons = new RawImage[UWPaperdollLayout.IconSlots.Length];

        for (int liSlot = 0; liSlot < mOIcons.Length; liSlot++)
        {
            mOIcons[liSlot] = UWUiKit.CreateRawImage(mOContent, UWPaperdollLayout.IconSlots[liSlot].Slot.ToString());
            UWIconPalette.ApplySmooth(mOIcons[liSlot]);
        }

        // After the icons, so the counts lie over them.
        mOCounts = new Text[UWPaperdollLayout.IconSlots.Length];

        for (int liSlot = 0; liSlot < mOCounts.Length; liSlot++)
        {
            mOCounts[liSlot] = UWUiKit.CreateText(mOContent, "Count " + UWPaperdollLayout.IconSlots[liSlot].Slot, TextAnchor.LowerRight,
                UWModernPalette.HudText, mOUi);
            mOCounts[liSlot].enabled = false;
        }

        mORules = new Image[2];

        for (int liRule = 0; liRule < mORules.Length; liRule++)
            mORules[liRule] = UWUiKit.CreateImage(mOContent, "Rule", UWModernPalette.Rule);

        string[] lsStats = UWCharacterSheet.ValueLabels;

        mOStatLabels = new Text[lsStats.Length];
        mOStatValues = new Text[lsStats.Length];

        for (int liStat = 0; liStat < lsStats.Length; liStat++)
        {
            mOStatLabels[liStat] = UWUiKit.CreateText(mOContent, lsStats[liStat], TextAnchor.LowerLeft, UWModernPalette.Dim, mOUi);
            mOStatLabels[liStat].text = UWCharacterSheet.ValueLabel(liStat);

            mOStatValues[liStat] = UWUiKit.CreateText(mOContent, lsStats[liStat] + " value", TextAnchor.UpperLeft, UWModernPalette.Text, mOUi);
        }

        mOSkillsHeading = UWUiKit.CreateText(mOContent, "Skills", TextAnchor.MiddleLeft, UWModernPalette.Text, mOUi);
        mOSkillsHeading.text = UWText.T("hud.panel.skills");
        mOSkillPoints = UWUiKit.CreateText(mOContent, "Skill points", TextAnchor.MiddleRight, UWModernPalette.Dim, mOUi);

        int liSkills = UWCharacterSheet.SkillEntries;

        mOSkillNames = new Text[liSkills];
        mOSkillValues = new Text[liSkills];

        for (int liEntry = 0; liEntry < liSkills; liEntry++)
        {
            mOSkillNames[liEntry] = UWUiKit.CreateText(mOContent, "Skill", TextAnchor.MiddleLeft, UWModernPalette.Text, mOUi);
            mOSkillValues[liEntry] = UWUiKit.CreateText(mOContent, "Skill value", TextAnchor.MiddleRight, UWModernPalette.Text, mOUi);
        }
    }
}
