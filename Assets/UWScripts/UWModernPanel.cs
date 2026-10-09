using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
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
public class UWModernPanel : MonoBehaviour
{
    public enum TabEnum
    {
        Character,
        Help
    }

    public static UWModernPanel Instance { get; private set; }

    public bool IsOpen => mbOpen;

    public TabEnum Tab => meTab;

    /// <summary>Pinned: always out on the Character tab (UWUserSettings.ModernPanelPinned).</summary>
    public static bool Pinned => UWUserSettings.ModernPanelPinned;

    /// <summary>Whether Escape has something to close here: an open panel, a pinned one only on
    /// the Help tab.</summary>
    public bool IsClosable => mbOpen && (!Pinned || meTab == TabEnum.Help);


    /// <summary>The layout in original pixels.</summary>
    private const int CharacterWidth = 96;

    private const int TopMargin = 14;

    private const int BottomMargin = 4;

    private const int HandleWidth = 9;

    private const int HandleHeight = 40;

    /// <summary>Below the minimap (UWModernMinimap: 4 margin + 96) and a gap of 4.</summary>
    private const int HandleTop = 104;

    /// <summary>How far the panel peeks out while the pointer is near the handle.</summary>
    private const int PeekWidth = 15;

    /// <summary>How near (original pixels left of the handle) counts as near.</summary>
    private const int PeekReach = 14;

    private const int TabHeaderWidth = 30;

    private const int TabHeaderHeight = 9;

    /// <summary>The pin at the right end of the tab row.</summary>
    private const int PinWidth = 11;

    /// <summary>The minimap in the pinned panel's head: square, as wide as the panel's inside,
    /// and the name two rows below it.</summary>
    private const int HeadSize = CharacterWidth - UWModernHudArt.LeatherLeft - UWModernHudArt.LeatherRight;

    private const int HeadRows = UWModernHudArt.LeatherTop + HeadSize + 2 - NameTop;

    /// <summary>The Character tab below the page, as fLayoutCharacter lays it out: a gap and a
    /// rule, the two rows of attributes, a rule and the skills' heading.</summary>
    private const int BelowPageRows = 3 + 3 + 26 + 3 + 8;

    private const int ContentPad = 6;

    private const int NameTop = 4;

    private const int PageTop = 15;

    /// <summary>How fast the panel slides: the share of the way left that is covered per second
    /// (exponentially).</summary>
    private const float SlideRate = 14f;

    private const int SkillRows = 10;

    private const float SkillRowHeight = 6f;

    /// <summary>Where the original draws the body and the armour (UWGameUI's paperdoll offsets),
    /// in MAIN.BYT pixels, rows from the top.</summary>
    private static readonly Vector2Int msBodyAt = new Vector2Int(260, 11);

    private static readonly Vector2Int msHelmetAt = new Vector2Int(267, 10);

    private static readonly Vector2Int msGlovesAt = new Vector2Int(261, 42);

    private static readonly Vector2Int msLegsAt = new Vector2Int(268, 24);

    private static readonly Vector2Int msChestAt = new Vector2Int(262, 22);

    private static readonly Vector2Int msBootsAt = new Vector2Int(266, 66);

    /// <summary>The six slots that show a thing's own icon, as the classic page places them
    /// (UWHudInventory.BuildEquipmentSlots): top-left corner and size; the rings' 4 x 4 carries a
    /// 16 x 16 icon centred, 0.8 up.</summary>
    private static readonly (UWArmorItemMap.BodySlot Slot, float X, float Y, float Size)[] msIconSlots =
    {
        (UWArmorItemMap.BodySlot.LeftRing, 291.03f, 56.9f, 4f),
        (UWArmorItemMap.BodySlot.RightRing, 260f, 56.9f, 4f),
        (UWArmorItemMap.BodySlot.RightHandSlot, 242f, 34.82566f, 16f),
        (UWArmorItemMap.BodySlot.LeftHandSlot, 296f, 34.826f, 16f),
        (UWArmorItemMap.BodySlot.LeftOffHandSlot, 294f, 12.792999f, 16f),
        (UWArmorItemMap.BodySlot.RightOffHandSlot, 244.9f, 12.793f, 16f),
    };

    private const float RingIconYOffset = 0.8f;

    private static readonly Color msText = new Color(0.94f, 0.87f, 0.71f);

    private static readonly Color msDim = new Color(0.78f, 0.71f, 0.57f);

    private static readonly Color msRule = new Color(0.47f, 0.28f, 0.12f, 1f);

    private UWGameUI mOUi;

    private UWCharacter mOCharacter;

    private UWControlScheme mOScheme;

    private UWHelpWindow mOHelp;

    private Font mOFont;

    private Canvas mOCanvas;

    private RectTransform mORoot;

    private RawImage mOBack;

    private RawImage mOHandle;

    private RawImage mOPin;

    private RawImage mOPinIcon;

    private Text mOHandleText;

    private RawImage[] mOTabBacks;

    private Text[] mOTabTexts;

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

    private Image[] mORules;

    private Text[] mOStatLabels;

    private Text[] mOStatValues;

    private Text mOSkillsHeading;

    private Text mOSkillPoints;

    private Text[] mOSkillNames;

    private Text[] mOSkillValues;

    private Texture2D mOBackTexture;

    private Vector2Int mOBackSize;

    /// <summary>The figure without its copper (UWHudArt.ComposePaperdoll) for a back of the panel's
    /// own, where it lies on MAIN.BYT, and the body and armour it was made from.</summary>
    private Texture2D mOFreeBodyTexture;

    private Vector2Int mOFreeBodyAt;

    private string msFreeBodyKey;

    /// <summary>The backs' and colours' version the leather was built with (UWModernBacks.ArtVersion).</summary>
    private int miBackArtVersion = -1;

    private Texture2D mOHandleTexture;

    private Texture2D mOTabTexture;

    private Texture2D mOPageTexture;

    private Texture2D mOPinTexture;

    private Texture2D mOPinIconTexture;

    /// <summary>Sleep (left of the feet) and Track (right of them): the slot circles, the pictures,
    /// their places on the screen and the name shown under the pointer.</summary>
    private RawImage[] mOFootSlots;

    private RawImage[] mOFootIcons;

    private Text mOFootTip;

    private Texture2D mOSlotTexture;

    private readonly Rect[] mOFootRects = new Rect[2];

    private static readonly string[] msFootNames = { "Sleep", "Track" };

    /// <summary>The bedroll and the leather boots (OBJECTS.GR).</summary>
    private static readonly int[] miFootObjects = { 289, 41 };

    /// <summary>On the page: the buttons' top row and their distance from the page's sides.</summary>
    private const int FootButtonTop = 60;

    /// <summary>Sleep sits two further right, off the page's frame (per user, 2026-10-04).</summary>
    private const int SleepExtraInset = 2;

    private const int FootButtonInset = 3;

    private int miArtVersion = -1;

    private readonly Dictionary<UWTexture, Texture2D> mOIconTextures = new Dictionary<UWTexture, Texture2D>();

    private bool mbOpen;

    private bool mbPeek;

    private TabEnum meTab = TabEnum.Character;

    /// <summary>How many screen pixels of the panel are out now, and its current width - both
    /// glide towards their targets.</summary>
    private float mfShown;

    private float mfWidth;

    /// <summary>The panel's height in original rows, gliding as the width.</summary>
    private float mfRows;

    /// <summary>Pinned with the rows for the minimap in the head.</summary>
    private bool mbHead;

    private float miScale = 1f;

    // Screen rectangles (pixels, bottom-left origin) of the last layout, for the clicks.
    private Rect mOPanelRect;

    private Rect mOHandleRect;

    private Rect mOPinRect;

    private Rect mOHeadRect;

    private readonly Rect[] mOTabRects = new Rect[2];

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
        mOFont = Resources.Load<Font>("Fonts/LexendExa");
    }

    private void fDestroyTextures()
    {
        foreach (Texture2D lOTexture in new[] { mOBackTexture, mOHandleTexture, mOTabTexture, mOPageTexture, mOPinTexture })
        {
            if (lOTexture != null)
                Destroy(lOTexture);
        }

        mOBackTexture = null;
        mOHandleTexture = null;
        mOTabTexture = null;
        mOPageTexture = null;
        mOPinTexture = null;

        if (mOFreeBodyTexture != null)
            Destroy(mOFreeBodyTexture);

        mOFreeBodyTexture = null;

        if (mOPinIconTexture != null)
            Destroy(mOPinIconTexture);

        mOPinIconTexture = null;

        if (mOSlotTexture != null)
            Destroy(mOSlotTexture);

        mOSlotTexture = null;

        foreach (Texture2D lOIcon in mOIconTextures.Values)
            Destroy(lOIcon);

        mOIconTextures.Clear();
        mOLowestRows.Clear();
    }

    private bool fIsModern()
    {
        return mOScheme != null && mOScheme.Current == UWControlScheme.SchemeEnum.Modern;
    }

    private bool fIsShown()
    {
        // In a conversation as well, for the trade (UWModernConversation).
        return fIsModern() && UWModernHud.Instance != null && (UWModernHud.Instance.IsShowing || UWModernHud.Instance.IsTalking)
            && mOUi != null && mOUi.mOUWData != null && mOCharacter != null;
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
    public Rect OpenRect => fIsShown() && mbOpen && mfShown > 0.5f && mOPanelRect.width > 0f
        ? new Rect(mOPanelRect.x, mOPanelRect.y, mOPanelRect.width, mOPanelRect.height + ((TabHeaderHeight - 2) * miScale))
        : Rect.zero;

    /// <summary>Whether the Character tab is out, its paperdoll taking things.</summary>
    private bool fIsPageOut()
    {
        return fIsShown() && mbOpen && meTab == TabEnum.Character && mfShown > mfWidth * 0.5f;
    }

    /// <summary>Whether a screen point lies on the panel or its handle - nothing goes into the
    /// world through it.</summary>
    public bool Contains(Vector2 pOPointer)
    {
        return fIsShown() && (mOPanelRect.Contains(pOPointer) || mOHandleRect.Contains(pOPointer)
            || mOTabRects[0].Contains(pOPointer) || mOTabRects[1].Contains(pOPointer) || mOPinRect.Contains(pOPointer));
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
        UWArmorItemMap.BodySlot? leSlot = UWHudInventory.GetSlotOfSpot(UWClickRules.InventorySpotAt(liX, 199 - liRow));

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
    public void Close()
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
    private static void fSetPinned(bool pbPinned)
    {
        UWUserSettings.ModernPanelPinned = pbPinned;
        UWUserSettings.Save();
    }

    // ------------------------------------------------- Input

    private void Update()
    {
        if (!fIsShown())
        {
            if (mbOpen && !fIsModern())
                fGiveUp();

            mbPeek = false;
            return;
        }

        // Pinned: out on the Character tab. The help is left alone -
        // restored open with a save game, the check below turns to it.
        if (Pinned && !mbOpen)
        {
            mbOpen = true;
            meTab = TabEnum.Character;
        }

        UWModernHud lOHud = UWModernHud.Instance;

        if (lOHud.IsOpen || UWControls.IsTextEntryActive || UWHelpWindow.BlocksGameKeys || mOUi.IsMapVisible)
            return;

        UWControls lOControls = mOScheme.Controls;

        if (lOControls == null)
            return;

        // TAB: the Help tab, not with Alt (switching programs) or Shift - as the help window,
        // and not under a setup-menu panel (the Game-folder dialog, UWSetupMenu).
        // Performed, not pressed: the pad's View opens the help when HELD (UWControls).
        if (lOControls.Player.ToggleHelp.WasPerformedThisFrame() && !UWControls.IsShiftHeld && !UWControls.IsAltHeld
            && !UWSetupMenu.HasOpenPanel)
        {
            if (mbOpen && meTab == TabEnum.Help)
                Close();
            else
                Open(TabEnum.Help);
        }

        // The help opened elsewhere (restored with a save game): the panel shows it.
        if (UWHelpLayout.IsOpen && !(mbOpen && meTab == TabEnum.Help))
            Open(TabEnum.Help);

        Mouse lOMouse = Mouse.current;

        mbPeek = false;

        if (lOMouse == null || !mOScheme.IsPointerFree)
            return;

        Vector2 lOPointer = lOMouse.position.ReadValue();
        Rect lONear = new Rect(mOHandleRect.xMin - (PeekReach * miScale), mOHandleRect.yMin - (4 * miScale),
            mOHandleRect.width + (PeekReach * miScale), mOHandleRect.height + (8 * miScale));

        mbPeek = !mbOpen && !UWModernLayout.IsPlaced(UWModernLayout.ElementEnum.CharacterPanel)
            && (lONear.Contains(lOPointer) || mOPanelRect.Contains(lOPointer));

        // The wheel scrolls the Character tab where it does not fit.
        float lfWheel = lOMouse.scroll.ReadValue().y;

        if (mbOpen && meTab == TabEnum.Character && Mathf.Abs(lfWheel) > 0.01f && mOViewRect.Contains(lOPointer))
            mfScroll -= Mathf.Sign(lfWheel) * ScrollStep;

        if (!lOControls.Player.CursorDrag.WasPressedThisFrame())
            return;

        if (mOPinRect.Contains(lOPointer))
        {
            fSetPinned(!Pinned);
            return;
        }

        if (mOHandleRect.Contains(lOPointer))
        {
            if (mbOpen)
                Close();
            else
                Open(TabEnum.Character);

            return;
        }

        if (!mbOpen && mOPanelRect.Contains(lOPointer))
        {
            Open(TabEnum.Character);
            return;
        }

        // Sleep and Track beside the paperdoll's feet - not with a thing on the pointer.
        for (int liFoot = 0; mbOpen && meTab == TabEnum.Character && liFoot < mOFootRects.Length; liFoot++)
        {
            if (!mOFootRects[liFoot].Contains(lOPointer) || !mOViewRect.Contains(lOPointer)
                || (mOUi.mOInventory != null && mOUi.mOInventory.CursorItem != null))
                continue;

            if (liFoot == 0 && UWScene.ItemDrag != null)
                UWScene.ItemDrag.TrySleep();
            else if (liFoot == 1 && mOUi.Runes != null)
                mOUi.Runes.TrackByKey();

            return;
        }

        for (int liTab = 0; mbOpen && liTab < mOTabRects.Length; liTab++)
        {
            if (mOTabRects[liTab].Contains(lOPointer))
                fSetTab((TabEnum)liTab);
        }
    }

    /// <summary>Leaving the modern scheme: the pointer goes back, the help stays as it is.</summary>
    private void fGiveUp()
    {
        mbOpen = false;
    }

    // ------------------------------------------------- Drawing

    private void LateUpdate()
    {
        if (!fIsShown())
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
        miScale = UWModernHud.PixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.CharacterPanel);

        fEnsureArt();

        float liScale = miScale;
        float lfHelpWidth = Mathf.Round(Mathf.Min(UWHelpLayout.MaxWidthPerHeight * Screen.height,
            Screen.width * UWHelpLayout.OverlayShare) / liScale) * liScale;
        float lfTargetWidth = meTab == TabEnum.Help ? Mathf.Max(lfHelpWidth, CharacterWidth * liScale) : CharacterWidth * liScale;
        // Moved in the layout editor it is a free window: no handle, no peeking, no sliding.
        bool lbFree = UWModernLayout.IsPlaced(UWModernLayout.ElementEnum.CharacterPanel);
        float lfTargetShown = mbOpen ? lfTargetWidth : mbPeek && !lbFree ? PeekWidth * liScale : 0f;
        float lfStep = 1f - Mathf.Exp(-SlideRate * Time.unscaledDeltaTime);
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

        mfWidth = Mathf.Abs(mfWidth - lfTargetWidth) < 1f ? lfTargetWidth : Mathf.Lerp(mfWidth, lfTargetWidth, lfStep);
        mfShown = lbFree || Mathf.Abs(mfShown - lfTargetShown) < 1f ? lfTargetShown : Mathf.Lerp(mfShown, lfTargetShown, lfStep);
        mfRows = Mathf.Abs(mfRows - liTargetRows) < 0.5f ? liTargetRows : Mathf.Lerp(mfRows, liTargetRows, lfStep);

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
        if (mOBackTexture == null || mOBackSize.x != liWidth || mOBackSize.y != liHeight || miBackArtVersion != UWModernBacks.ArtVersion)
        {
            if (mOBackTexture != null)
                Destroy(mOBackTexture);

            mOBackTexture = UWModernHudArt.BuildPartLeather(UWModernLayout.ElementEnum.CharacterPanel, fData().Textures, liWidth, liHeight,
                mOUi.TextureFilterMode);
            mOBackSize = new Vector2Int(liWidth, liHeight);
            miBackArtVersion = UWModernBacks.ArtVersion;
            mOBack.texture = mOBackTexture;
        }

        mOBack.enabled = mfShown > 0.5f;
        fSetRect(mOBack.rectTransform, lfLeft, lfBottom, liWidth * liScale, liHeight * liScale);

        // The handle, out of the panel's left edge and over its frame there - not when pinned.
        float lfHandleBottom = Screen.height - ((HandleTop + HandleHeight) * liScale);
        Rect lOHandle = new Rect(lfLeft - (HandleWidth * liScale), lfHandleBottom, HandleWidth * liScale, HandleHeight * liScale);

        mOHandleRect = lbPinned || lbFree ? Rect.zero : lOHandle;
        mOHandle.enabled = !lbPinned && !lbFree;
        mOHandleText.enabled = !lbPinned && !lbFree;
        // A free window leaves the right edge to the others (the bags, the spells, the conversation).
        LeftEdge = lbFree ? float.MaxValue : lbPinned ? lfLeft : lOHandle.xMin;
        fSetRect(mOHandle.rectTransform, lOHandle.x, lOHandle.y,
            (HandleWidth + UWModernHudArt.LeatherLeft) * liScale, lOHandle.height);

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

        RectTransform lOLabel = mOHandleText.rectTransform;
        lOLabel.pivot = new Vector2(0.5f, 0.5f);
        lOLabel.anchoredPosition = lOHandle.center;
        lOLabel.sizeDelta = new Vector2(lOHandle.height, lOHandle.width);
        lOLabel.localRotation = Quaternion.Euler(0f, 0f, 90f);
        mOHandleText.fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * liScale));

        // The tab headers over the top edge, once the panel is out.
        bool lbOut = mbOpen && mfShown > PeekWidth * liScale;

        for (int liTab = 0; liTab < mOTabBacks.Length; liTab++)
        {
            Rect lOTab = new Rect(lfLeft + ((ContentPad + (liTab * (TabHeaderWidth + 2))) * liScale), lfTop - (2 * liScale),
                TabHeaderWidth * liScale, TabHeaderHeight * liScale);

            mOTabRects[liTab] = lbOut ? lOTab : Rect.zero;
            mOTabBacks[liTab].enabled = lbOut;
            mOTabTexts[liTab].enabled = lbOut;
            mOTabBacks[liTab].color = (int)meTab == liTab ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f);
            mOTabTexts[liTab].color = (int)meTab == liTab ? msText : msDim;
            mOTabTexts[liTab].fontSize = Mathf.Max(9, Mathf.RoundToInt(4.2f * liScale));
            fSetRect(mOTabBacks[liTab].rectTransform, lOTab.x, lOTab.y, lOTab.width, lOTab.height);
            fSetRect(mOTabTexts[liTab].rectTransform, lOTab.x, lOTab.y + liScale, lOTab.width, lOTab.height);
        }

        // The pin at the right end of the row: upright and bright when pinned, tilted otherwise.
        Rect lOPin = new Rect(lfLeft + ((liWidth - ContentPad - PinWidth) * liScale), lfTop - (2 * liScale),
            PinWidth * liScale, TabHeaderHeight * liScale);

        mOPinRect = lbOut ? lOPin : Rect.zero;
        mOPin.enabled = lbOut;
        mOPinIcon.enabled = lbOut;
        mOPin.color = lbPinned ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f);
        mOPinIcon.color = lbPinned ? Color.white : new Color(0.78f, 0.78f, 0.78f, 1f);
        fSetRect(mOPin.rectTransform, lOPin.x, lOPin.y, lOPin.width, lOPin.height);

        RectTransform lOPinIcon = mOPinIcon.rectTransform;
        lOPinIcon.pivot = new Vector2(0.5f, 0.5f);
        lOPinIcon.anchoredPosition = lOPin.center + new Vector2(0f, 0.5f * liScale);
        lOPinIcon.sizeDelta = new Vector2(7f * liScale, 7f * liScale);
        lOPinIcon.localRotation = Quaternion.Euler(0f, 0f, lbPinned ? 0f : 35f);

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
            fSetRect(mOViewport, mOViewRect.x, mOViewRect.y, mOViewRect.width, mOViewRect.height);

            // The content keeps screen coordinates inside the view.
            mOContent.anchoredPosition = -mOViewRect.position;

            fLayoutCharacter(lfLeft, lfTop - ((mbHead ? HeadRows : 0) * liScale) + (mfScroll * liScale), liWidth);

            // The bar: where the view lies in the whole tab.
            mOScrollBar.enabled = lfMaxScroll > 0f;

            if (lfMaxScroll > 0f)
            {
                float lfBar = mOViewRect.height * (liHeight / (float)liNeeded);
                float lfBarTop = mOViewRect.yMax - ((mfScroll / liNeeded) * mOViewRect.height);

                fSetRect(mOScrollBar.rectTransform, mOViewRect.xMax - (1.5f * liScale), lfBarTop - lfBar, Mathf.Max(2f, liScale * 0.75f), lfBar);
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

        // Name, class and level.
        mOName.text = lOPlayer != null && !string.IsNullOrEmpty(lOPlayer.Name) ? lOPlayer.Name : "Avatar";
        mOName.fontSize = Mathf.Max(12, Mathf.RoundToInt(6f * liScale));
        fSetRect(mOName.rectTransform, lfX, pfTop - ((NameTop + 9) * liScale), lfInner, 9 * liScale);

        mOClass.text = UWHelpWindow.fClassName(lOData, lOPlayer) + ", level " + lOCharacter.Level;
        mOClass.fontSize = liSmall;
        fSetRect(mOClass.rectTransform, lfX, pfTop - ((NameTop + 9) * liScale), lfInner, 9 * liScale);

        // The page, centred - as wide as the backpack below it, which it docks onto.
        RectInt lOPage = UWModernHudArt.PaperdollPage;
        float lfPageLeft = pfLeft + (((piWidth - UWModernHudArt.PaperdollPageWidth) / 2) * liScale);
        float lfPageTop = pfTop - (PageTop * liScale);

        mOPageRect = new Rect(lfPageLeft, lfPageTop - (lOPage.height * liScale), UWModernHudArt.PaperdollPageWidth * liScale,
            lOPage.height * liScale);
        mOPage.texture = mOPageTexture;
        mOPage.enabled = true;
        fSetRect(mOPage.rectTransform, mOPageRect.x, mOPageRect.y, mOPageRect.width, mOPageRect.height);

        // Body and armour in the classic order (UWGameUI.fBuildCanvas).
        // On a back of the panel's own the figure loses the copper it was painted on (per user,
        // 2026-10-09: it stood in a box of copper, and helmets carried the page with them): body
        // and armour put together as one picture and freed from the page.
        if (UWModernBacks.Get(UWModernLayout.ElementEnum.CharacterPanel).Shape != UWDataImport.UWData.UWBackdropArt.ShapeEnum.None)
        {
            UWTextures lOTextures = fData().Textures;
            UWTexture[] lOPieces =
            {
                lOCharacter.HelmetSource, lOCharacter.GlovesSource, lOCharacter.LegsSource, lOCharacter.ChestSource, lOCharacter.BootsSource
            };
            string lsKey = lOCharacter.CharIndex.ToString();

            foreach (UWTexture lOPiece in lOPieces)
                lsKey += "/" + (lOPiece != null ? lOPiece.GetHashCode() : 0);

            if (mOFreeBodyTexture == null || msFreeBodyKey != lsKey)
            {
                if (mOFreeBodyTexture != null)
                    Destroy(mOFreeBodyTexture);

                mOFreeBodyTexture = UWModernHudArt.BuildFreeFigure(lOTextures, lOCharacter.CharIndex, lOPieces,
                    new[] { msHelmetAt, msGlovesAt, msLegsAt + new Vector2Int(0, lOCharacter.LegsDrop), msChestAt, msBootsAt }, mOUi.TextureFilterMode,
                    out mOFreeBodyAt);
                msFreeBodyKey = lsKey;
            }

            fPlaceOnPage(mOArmour[0], mOFreeBodyTexture, mOFreeBodyAt);

            for (int liPart = 1; liPart < mOArmour.Length; liPart++)
                mOArmour[liPart].enabled = false;
        }
        else
        {
            fPlaceOnPage(mOArmour[0], lOCharacter.CharTexture, msBodyAt);
            fPlaceOnPage(mOArmour[1], lOCharacter.HelmetTexture, msHelmetAt);
            fPlaceOnPage(mOArmour[2], lOCharacter.GlovesTexture, msGlovesAt);
            fPlaceOnPage(mOArmour[3], lOCharacter.LegsArmorTexture, msLegsAt);
            fPlaceOnPage(mOArmour[4], lOCharacter.ChestArmorTexture, msChestAt);
            fPlaceOnPage(mOArmour[5], lOCharacter.BootsTexture, msBootsAt);
        }

        // The things in the hands, on the shoulders and on the fingers.
        UWInventory lOInventory = mOUi.mOInventory;

        for (int liSlot = 0; liSlot < msIconSlots.Length; liSlot++)
        {
            (UWArmorItemMap.BodySlot leSlot, float lfSlotX, float lfSlotY, float lfSize) = msIconSlots[liSlot];
            UWObject lOItem = lOInventory != null ? lOInventory.GetEquipped(leSlot) : null;
            UWTexture lOSource = lOItem != null ? lOItem.Icon ?? lOItem.Texture : null;
            RawImage lOIcon = mOIcons[liSlot];

            if (lOSource == null)
            {
                lOIcon.enabled = false;
                continue;
            }

            Texture2D lOTexture = fGetIcon(lOSource);
            float lfCentreX = lfSlotX + (lfSize * 0.5f) - lOPage.x + UWModernHudArt.PaperdollShift;
            float lfCentreRow = lfSlotY + (lfSize * 0.5f) - lOPage.y - (lfSize < 16f ? RingIconYOffset : 0f);

            lOIcon.texture = lOTexture;
            lOIcon.enabled = true;
            fSetRect(lOIcon.rectTransform, Mathf.Round(mOPageRect.xMin + ((lfCentreX - (lOTexture.width * UWModernHudArt.PixelAspectX * 0.5f)) * liScale)),
                Mathf.Round(mOPageRect.yMax - ((lfCentreRow + (lOTexture.height * 0.5f)) * liScale)),
                lOTexture.width * UWModernHudArt.PixelAspectX * liScale, lOTexture.height * liScale);
        }

        fLayoutFootButtons();

        // A rule, the attributes, a rule, the skills - right below the page; the backpack stays
        // with the bags.
        float lfY = mOPageRect.yMin - (3 * liScale);

        fSetRule(mORules[0], lfX, lfY, lfInner);
        lfY -= 3 * liScale;

        string[] lsValues =
        {
            lOCharacter.Strength.ToString(), lOCharacter.Dexterity.ToString(), lOCharacter.Intelligence.ToString(),
            Mathf.RoundToInt(lOCharacter.CurrentHP) + " / " + Mathf.RoundToInt(lOCharacter.MaxHP),
            Mathf.RoundToInt(lOCharacter.CurrentMana) + " / " + Mathf.RoundToInt(lOCharacter.MaxMana),
            (lOCharacter.Experience / 10).ToString()
        };

        float lfColumn = lfInner / 3f;

        for (int liStat = 0; liStat < mOStatLabels.Length; liStat++)
        {
            float lfStatX = lfX + ((liStat % 3) * lfColumn);
            float lfStatY = lfY - ((liStat / 3) * 12 * liScale);

            mOStatLabels[liStat].fontSize = liSmall;
            fSetRect(mOStatLabels[liStat].rectTransform, lfStatX, lfStatY - (5 * liScale), lfColumn, 5 * liScale);

            mOStatValues[liStat].text = lsValues[liStat];
            mOStatValues[liStat].fontSize = liFont;
            fSetRect(mOStatValues[liStat].rectTransform, lfStatX, lfStatY - (11 * liScale), lfColumn, 6 * liScale);
        }

        lfY -= 26 * liScale;
        fSetRule(mORules[1], lfX, lfY, lfInner);
        lfY -= 3 * liScale;

        mOSkillsHeading.fontSize = liFont;
        fSetRect(mOSkillsHeading.rectTransform, lfX, lfY - (7 * liScale), lfInner, 7 * liScale);

        int liPoints = lOCharacter.SkillPoints;

        mOSkillPoints.text = liPoints == 1 ? "1 point to spend" : liPoints + " points to spend";
        mOSkillPoints.enabled = liPoints > 0;
        mOSkillPoints.fontSize = liSmall;
        fSetRect(mOSkillPoints.rectTransform, lfX, lfY - (7 * liScale), lfInner, 7 * liScale);

        lfY -= 8 * liScale;

        // Attack and Defense first, then the eighteen skills - the order of the classic list.
        float lfSkillColumn = (lfInner - (2 * liScale)) / 2f;

        for (int liEntry = 0; liEntry < mOSkillNames.Length; liEntry++)
        {
            int liValue = liEntry == 0 ? lOCharacter.Attack
                : liEntry == 1 ? lOCharacter.Defence
                : lOCharacter.GetSkill((UWPlayerData.Skill)(liEntry - 2));
            float lfSkillX = lfX + ((liEntry / SkillRows) * (lfSkillColumn + (2 * liScale)));
            float lfSkillY = lfY - (((liEntry % SkillRows) + 1) * SkillRowHeight * liScale);

            if (string.IsNullOrEmpty(mOSkillNames[liEntry].text))
                mOSkillNames[liEntry].text = UWHelpWindow.fSkillName(lOData, liEntry);

            mOSkillNames[liEntry].fontSize = liSmall;
            fSetRect(mOSkillNames[liEntry].rectTransform, lfSkillX, lfSkillY, lfSkillColumn - (4 * liScale), SkillRowHeight * liScale);

            mOSkillValues[liEntry].text = liValue.ToString();
            mOSkillValues[liEntry].fontSize = liSmall;
            fSetRect(mOSkillValues[liEntry].rectTransform, lfSkillX, lfSkillY, lfSkillColumn - (4 * liScale), SkillRowHeight * liScale);
        }
    }

    /// <summary>Sleep left of the feet, Track right of them, on the page's lower corners.</summary>
    private void fLayoutFootButtons()
    {
        float liScale = miScale;
        float lfSlot = UWModernHudArt.SlotSize * liScale;
        float lfTop = mOPageRect.yMax - ((FootButtonTop + UWModernHudArt.SlotSize) * liScale);
        Vector2 lOPointer = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);
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
                fSetRect(lOIcon.rectTransform, lORect.x + ((UWModernHudArt.SlotSize - (lOTexture.width * UWModernHudArt.PixelAspectX)) * 0.5f * liScale),
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

            mOFootTip.text = msFootNames[liHovered];
            mOFootTip.fontSize = Mathf.Max(9, Mathf.RoundToInt(3.8f * liScale));
            fSetRect(mOFootTip.rectTransform, lORect.x - (10 * liScale), lORect.yMax, lORect.width + (20 * liScale), 7 * liScale);
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

        fSetRect(pOImage.rectTransform, mOPageRect.xMin + ((lfLeft - lOPage.x + UWModernHudArt.PaperdollShift) * liScale),
            mOPageRect.yMax - ((pOAt.y - lOPage.y + pOTexture.height) * liScale),
            pOTexture.width * liScale * FigureWidthFactor, pOTexture.height * liScale);
    }

    private void fSetRule(Image pORule, float pfX, float pfY, float pfWidth)
    {
        fSetRect(pORule.rectTransform, pfX, pfY, pfWidth, Mathf.Max(2f, miScale * 0.75f));
    }

    private Texture2D fGetIcon(UWTexture pOSource)
    {
        if (!mOIconTextures.TryGetValue(pOSource, out Texture2D lOIcon) || lOIcon == null)
        {
            lOIcon = UWIconTextureBuilder.Build(pOSource, mOUi.TextureFilterMode);
            mOIconTextures[pOSource] = lOIcon;
        }

        return lOIcon;
    }

    private void fEnsureArt()
    {
        if (miArtVersion == UWModernBacks.ArtVersion && mOPageTexture != null)
            return;

        miArtVersion = UWModernBacks.ArtVersion;
        fDestroyTextures();

        UWTextures lOTextures = fData().Textures;
        FilterMode leFilter = mOUi.TextureFilterMode;

        mOHandleTexture = UWModernHudArt.BuildPartLeatherTab(UWModernLayout.ElementEnum.CharacterPanel, lOTextures, HandleWidth + UWModernHudArt.LeatherLeft, HandleHeight, leFilter);
        mOTabTexture = UWModernHudArt.BuildPartLeather(UWModernLayout.ElementEnum.CharacterPanel, lOTextures, TabHeaderWidth, TabHeaderHeight, leFilter);
        mOPageTexture = UWModernHudArt.BuildPartPaperdollPage(UWModernLayout.ElementEnum.CharacterPanel, lOTextures, leFilter);
        mOPinTexture = UWModernHudArt.BuildPartLeather(UWModernLayout.ElementEnum.CharacterPanel, lOTextures, PinWidth, TabHeaderHeight, leFilter);
        mOPinIconTexture = BuildPinIcon(48);
        mOSlotTexture = UWModernActionBar.BuildRing(128, 60f, 6f);

        mOHandle.texture = mOHandleTexture;
        mOPin.texture = mOPinTexture;
        mOPinIcon.texture = mOPinIconTexture;

        foreach (RawImage lOTab in mOTabBacks)
            lOTab.texture = mOTabTexture;

        mOBackSize = Vector2Int.zero;
    }

    /// <summary>A pushpin pointing down: a gold head with a dark rim on a grey needle - also the
    /// rune panel's (UWModernRunePanel).</summary>
    internal static Texture2D BuildPinIcon(int piSize)
    {
        Color32[] lyPixels = new Color32[piSize * piSize];
        Vector2 lOHead = new Vector2(0.5f, 0.66f);
        Vector2 lONeedleTop = new Vector2(0.5f, 0.5f);
        Vector2 lONeedleTip = new Vector2(0.5f, 0.06f);
        Color lOGold = new Color(0.925f, 0.77f, 0.44f, 1f);
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

        Texture2D lOTexture = new Texture2D(piSize, piSize, TextureFormat.RGBA32, false);
        lOTexture.name = "UWModernPanel pin";
        lOTexture.filterMode = FilterMode.Bilinear;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(lyPixels);
        lOTexture.Apply();

        return lOTexture;
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

    private static void fSetRect(RectTransform pORect, float pfX, float pfY, float pfWidth, float pfHeight)
    {
        pORect.anchoredPosition = new Vector2(pfX, pfY);
        pORect.sizeDelta = new Vector2(pfWidth, pfHeight);
    }

    // ------------------------------------------------- Building

    private void fBuild()
    {
        GameObject lORoot = new GameObject("Modern character panel", typeof(Canvas), typeof(CanvasScaler));
        lORoot.transform.SetParent(transform, false);

        mOCanvas = lORoot.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mOCanvas.sortingOrder = 41;

        CanvasScaler lOScaler = lORoot.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        lOScaler.scaleFactor = 1f;

        mORoot = (RectTransform)lORoot.transform;

        mOTabBacks = new RawImage[2];
        mOTabTexts = new Text[2];

        string[] lsTabs = { "Character", "Help" };

        for (int liTab = 0; liTab < 2; liTab++)
        {
            mOTabBacks[liTab] = fCreateRawImage(mORoot, "Tab " + lsTabs[liTab]);
            UWPixelArtUI.Apply(mOTabBacks[liTab]);
            mOTabTexts[liTab] = fCreateText(mORoot, lsTabs[liTab], TextAnchor.MiddleCenter, msText);
            mOTabTexts[liTab].text = lsTabs[liTab];
        }

        mOPin = fCreateRawImage(mORoot, "Pin");
        UWPixelArtUI.Apply(mOPin);
        mOPinIcon = fCreateRawImage(mORoot, "Pin icon");

        mOBack = fCreateRawImage(mORoot, "Leather");
        mOHandle = fCreateRawImage(mORoot, "Handle");
        UWPixelArtUI.Apply(mOBack);
        UWPixelArtUI.Apply(mOHandle);
        mOHandle.enabled = true;
        mOHandleText = fCreateText(mORoot, "Handle label", TextAnchor.MiddleCenter, msText);
        mOHandleText.text = "Character";

        GameObject lOViewport = new GameObject("Character view", typeof(RectTransform), typeof(RectMask2D));
        lOViewport.transform.SetParent(mORoot, false);
        mOViewport = (RectTransform)lOViewport.transform;
        mOViewport.anchorMin = Vector2.zero;
        mOViewport.anchorMax = Vector2.zero;
        mOViewport.pivot = Vector2.zero;

        GameObject lOContent = new GameObject("Character", typeof(RectTransform));
        lOContent.transform.SetParent(mOViewport, false);
        mOContent = (RectTransform)lOContent.transform;
        mOContent.anchorMin = Vector2.zero;
        mOContent.anchorMax = Vector2.zero;
        mOContent.pivot = Vector2.zero;

        GameObject lOBar = new GameObject("Scroll bar", typeof(RectTransform), typeof(Image));
        lOBar.transform.SetParent(mORoot, false);
        RectTransform lOBarRect = (RectTransform)lOBar.transform;
        lOBarRect.anchorMin = Vector2.zero;
        lOBarRect.anchorMax = Vector2.zero;
        lOBarRect.pivot = Vector2.zero;
        mOScrollBar = lOBar.GetComponent<Image>();
        mOScrollBar.color = new Color(0.80f, 0.64f, 0.34f, 0.85f);
        mOScrollBar.raycastTarget = false;
        mOScrollBar.enabled = false;

        mOName = fCreateText(mOContent, "Name", TextAnchor.MiddleLeft, msText);
        mOClass = fCreateText(mOContent, "Class", TextAnchor.MiddleRight, msDim);

        mOPage = fCreateRawImage(mOContent, "Paperdoll page");
        UWPixelArtUI.Apply(mOPage);

        mOFootSlots = new RawImage[2];
        mOFootIcons = new RawImage[2];

        for (int liFoot = 0; liFoot < 2; liFoot++)
        {
            mOFootSlots[liFoot] = fCreateRawImage(mOContent, msFootNames[liFoot]);
            mOFootIcons[liFoot] = fCreateRawImage(mOContent, msFootNames[liFoot] + " picture");
            UWIconPalette.ApplySmooth(mOFootIcons[liFoot]);
        }

        mOFootTip = fCreateText(mORoot, "Foot button name", TextAnchor.LowerCenter, msText);

        mOArmour = new RawImage[6];

        string[] lsArmour = { "Body", "Helmet", "Gloves", "Legs", "Chest", "Boots" };

        for (int liPart = 0; liPart < mOArmour.Length; liPart++)
        {
            mOArmour[liPart] = fCreateRawImage(mOContent, lsArmour[liPart]);
            UWPixelArtUI.Apply(mOArmour[liPart]);
        }

        mOIcons = new RawImage[msIconSlots.Length];

        for (int liSlot = 0; liSlot < mOIcons.Length; liSlot++)
        {
            mOIcons[liSlot] = fCreateRawImage(mOContent, msIconSlots[liSlot].Slot.ToString());
            UWIconPalette.ApplySmooth(mOIcons[liSlot]);
        }

        mORules = new Image[2];

        for (int liRule = 0; liRule < mORules.Length; liRule++)
        {
            GameObject lORule = new GameObject("Rule", typeof(RectTransform), typeof(Image));
            lORule.transform.SetParent(mOContent, false);

            RectTransform lORect = (RectTransform)lORule.transform;
            lORect.anchorMin = Vector2.zero;
            lORect.anchorMax = Vector2.zero;
            lORect.pivot = Vector2.zero;

            mORules[liRule] = lORule.GetComponent<Image>();
            mORules[liRule].color = msRule;
            mORules[liRule].raycastTarget = false;
        }

        string[] lsStats = { "Strength", "Dexterity", "Intelligence", "Vitality", "Mana", "Experience" };

        mOStatLabels = new Text[lsStats.Length];
        mOStatValues = new Text[lsStats.Length];

        for (int liStat = 0; liStat < lsStats.Length; liStat++)
        {
            mOStatLabels[liStat] = fCreateText(mOContent, lsStats[liStat], TextAnchor.LowerLeft, msDim);
            mOStatLabels[liStat].text = lsStats[liStat];
            mOStatValues[liStat] = fCreateText(mOContent, lsStats[liStat] + " value", TextAnchor.UpperLeft, msText);
        }

        mOSkillsHeading = fCreateText(mOContent, "Skills", TextAnchor.MiddleLeft, msText);
        mOSkillsHeading.text = "Skills";
        mOSkillPoints = fCreateText(mOContent, "Skill points", TextAnchor.MiddleRight, msDim);

        int liSkills = 2 + (int)UWPlayerData.Skill.Swimming + 1;

        mOSkillNames = new Text[liSkills];
        mOSkillValues = new Text[liSkills];

        for (int liEntry = 0; liEntry < liSkills; liEntry++)
        {
            mOSkillNames[liEntry] = fCreateText(mOContent, "Skill", TextAnchor.MiddleLeft, msText);
            mOSkillValues[liEntry] = fCreateText(mOContent, "Skill value", TextAnchor.MiddleRight, msText);
        }
    }

    private static RawImage fCreateRawImage(Transform pOParent, string psName)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        RawImage lOImage = lOObject.GetComponent<RawImage>();
        lOImage.raycastTarget = false;
        lOImage.enabled = false;

        return lOImage;
    }

    private Text fCreateText(Transform pOParent, string psName, TextAnchor peAlignment, Color pOColour)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Text), typeof(Outline));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        Text lOText = lOObject.GetComponent<Text>();
        lOText.font = mOFont != null ? mOFont : UWInterfaceFont.Font;
        lOText.alignment = peAlignment;
        lOText.color = pOColour;
        lOText.raycastTarget = false;
        lOText.horizontalOverflow = HorizontalWrapMode.Overflow;
        lOText.verticalOverflow = VerticalWrapMode.Overflow;

        Outline lOOutline = lOObject.GetComponent<Outline>();
        lOOutline.effectColor = new Color(0.16f, 0.09f, 0.04f, 0.9f);
        lOOutline.effectDistance = new Vector2(1.5f, -1.5f);

        return lOText;
    }
}
