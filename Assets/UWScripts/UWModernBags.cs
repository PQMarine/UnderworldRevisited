using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN SCHEME'S BAGS (stage 2a of the modern controls; look and handling decided per user
/// on mockups, 2026-10-03 - WoW-like bags, no all-in-one bag):
///
///   - bottom right the 8 backpack slots of the original, ALWAYS shown, 4 x 2 on the leather,
///     the load in the title and as a bar above them;
///   - B (or Escape) opens and closes them - only the windows: the pointer is the right
///     button's alone (UWModernPointer, per user 2026-10-04: with the pinned character panel the
///     old C and B, which freed the pointer as well, had become the same key); C is the
///     character panel's (UWModernPanel);
///   - a container in a slot opens its own window above the backpack on the right button,
///     several at once, stacked upwards in the order they opened and then in columns to the left
///     (a bag inside another one is appended as well - the player chooses the order; per user,
///     2026-10-03, after trying it directly above its parent); its slot shows the open picture,
///     as the original's open container does;
///   - WHICH WINDOW BELONGS TO WHICH CONTAINER (per user on a mockup, 2026-10-03): a number
///     badge in the window's title and on the container's slot (the lowest free number when it
///     opens); the pointer over a window or an open container's slot frames the window, rings
///     the slot and joins them with a line; a window grows out of its slot when it opens;
///   - the window's badge is its CLOSE BUTTON (per user, 2026-10-03): under the pointer its
///     number turns into an x, a left click closes the window and the bags inside it - not
///     the right button, which uses, and a click beside it would close a bag by accident;
///   - the LEFT button takes a thing and puts it down - held and dragged, or click and click;
///     onto a slot, a container (it goes inside), a bag window, or into the world (low on the
///     screen dropped in front, higher thrown - the original's rule, UWPlayerThrow);
///   - the RIGHT button opens a thing's CONTEXT MENU (per user, 2026-10-04 - before it used at
///     once, and Shift with it looked): use, open, equip or take off, look, pick up, split,
///     cancel (UWModernBags.ContextMenu.cs); a container opens or closes at once instead, Shift
///     with it gives its menu; food and drink put down on the paperdoll's head
///     are eaten, as on the classic page;
///   - hovering shows the name and the weight;
///   - THE USE MODE (per user, 2026-10-04: the lantern could not be filled): the right button on
///     a thing that is applied to something else (oil flask, key, lockpick, pole ... -
///     UWItemDrag.NeedsUseTarget) asks "Use ... on what?" and hangs its picture on the pointer;
///     the next left click applies it - onto a carried thing in a slot, or onto the world under
///     the pointer (UWItemDrag.ApplyItemModern). The right button, Escape, locking the pointer or
///     closing the bags let it go. The anvil in the world comes the same way (BeginUse).
///
/// CLICKING THE BACKPACK with the pointer free and the bags shut opens them, as B does - on a
/// container its window as well (per user, 2026-10-03). THE OPEN CONTAINERS, their order and
/// numbers are kept with the save game (UWR.json through UWHelpNotes, as inventory paths) and
/// are open again after loading.
///
/// WITH THE CHARACTER PANEL (UWModernPanel, 2026-10-03) the bags work while it is open as well,
/// but the bag windows, their numbers and the open pictures show only while the bags are open
/// (per user: with the panel open B and Escape did not seem to close them); the paperdoll's slots
/// on its Character tab take part in the dragging, the right button and the tooltip like any
/// slot. Nothing goes into the world through the panel. THE BACKPACK ALWAYS STAYS WITH THE BAGS
/// (per user, 2026-10-04 - until then it moved into the panel under the paperdoll, and the bag
/// windows stacking beside the panel made the panel hard to place in the layout editor).
///
/// The work is the inventory model's (UWInventoryModel: the pointer item, stacking, combining,
/// capacity); this class only lays it out and maps the clicks. SHIFT AND THE LEFT BUTTON on a
/// stack split it (UWModernBags.StackSplit.cs). IN A CONVERSATION the bags stay for the trade, its
/// areas are slots of theirs (UWModernBags.Trade.cs, UWModernConversation).
/// </summary>
public partial class UWModernBags : MonoBehaviour
{
    /// <summary>The one bag view of the scene, for UWGameUI's Escape.</summary>
    public static UWModernBags Instance { get; private set; }

    /// <summary>Whether the bag windows are open.</summary>
    public bool IsOpen => mbOpen;


    private const int Columns = 4;

    private const int MinRows = 2;

    private const int MaxRows = 6;

    /// <summary>The layout in original pixels: the slot pitch is the original's 19.</summary>
    private const int SlotGap = 2;

    private const int SidePad = 6;

    private const int TitleRows = 12;

    private const int BottomPad = 6;

    private const int ScreenMargin = 4;

    private const int WindowGap = 4;

    private const int LoadBarRows = 2;

    /// <summary>Screen pixels the pointer may move before a press counts as a drag.</summary>
    private const float ClickMoveThreshold = 6f;

    /// <summary>The containers 128 to 139 have an open picture (bit 0), see UWHudInventory.</summary>
    private const int LastContainerWithOpenVariant = 139;

    /// <summary>Weapons the right button puts into the hand: the melee ones and the bows,
    /// crossbows and slings - not the ammunition and spell missiles between (0x10 to 0x17).</summary>
    private const int LastMeleeWeaponId = 0x0F;

    private const int FirstLauncherId = 0x18;

    private const int LastLauncherId = 0x1F;

    /// <summary>String block 1, our numbering - as UWItemDrag reports a refusing container.</summary>
    private const int RunesOnlyMessage = 248;

    private const int DoesNotFitMessage = 249;

    private sealed class Slot
    {
        public RawImage Circle;

        public RawImage Icon;

        public Text Count;

        public Rect ScreenRect;

        public UWTexture ShownTexture;

        /// <summary>The ring while the pointer is on this slot's open container or its window.</summary>
        public RawImage Ring;

        /// <summary>The number badge of an open container lying here.</summary>
        public RawImage Badge;

        public Text BadgeText;
    }

    private sealed class Window
    {
        public RectTransform Root;

        public RawImage Back;

        public Texture2D BackTexture;

        public Vector2Int BackSize;

        public int BackVersion = -1;

        public Text Title;

        public Text Weight;

        public RawImage Badge;

        public Text BadgeText;

        /// <summary>Where the badge - the close button - is on the screen.</summary>
        public Rect BadgeRect;

        /// <summary>The frame while the pointer is on the window or its container's slot.</summary>
        public Image[] Frame;

        /// <summary>Fades the window in while it grows out of its slot.</summary>
        public CanvasGroup Group;

        public readonly List<Slot> Slots = new List<Slot>();

        public Rect ScreenRect;

        /// <summary>Null for the backpack.</summary>
        public UWObject Container;

        /// <summary>The contents index of the first slot (scrolled rows times four).</summary>
        public int FirstIndex;

        /// <summary>The arrows in the right margin while rows lie hidden above or below
        /// (fShowScrollArrows).</summary>
        public RawImage ScrollUp;

        public RawImage ScrollDown;

        public int SlotCount;
    }

    private UWGameUI mOUi;

    private UWInventory mOInventory;

    private Interaction mOInteraction;

    private UWControlScheme mOScheme;


    private Font mOFont;

    private Canvas mOCanvas;

    private RectTransform mORoot;

    private readonly List<Window> mOWindows = new List<Window>();

    private Image mOLoadBack;

    private Image mOLoadFill;

    private RawImage mOCursorIcon;

    private Text mOCursorCount;

    private Image mOTooltipBack;

    private Text mOTooltip;

    private Texture2D mOSlotTexture;

    private int miArtVersion = -1;

    private readonly Dictionary<UWTexture, Texture2D> mOIcons = new Dictionary<UWTexture, Texture2D>();

    private float miScale = 1f;

    private bool mbOpen;

    /// <summary>The containers whose windows are open, bottom first.</summary>
    private readonly List<UWObject> mOOpenBags = new List<UWObject>();

    /// <summary>The scrolled rows of each open bag.</summary>
    private readonly Dictionary<UWObject, int> mOScroll = new Dictionary<UWObject, int>();

    /// <summary>The number badge of each open bag.</summary>
    private readonly Dictionary<UWObject, int> mOBagNumbers = new Dictionary<UWObject, int>();

    /// <summary>When each bag opened (unscaled time), for its window growing out of the slot.</summary>
    private readonly Dictionary<UWObject, float> mOOpenedAt = new Dictionary<UWObject, float>();

    /// <summary>Where each open bag's slot is on the screen this frame, if it is visible.</summary>
    private readonly Dictionary<UWObject, Rect> mOSourceRects = new Dictionary<UWObject, Rect>();

    /// <summary>How long a window takes to grow out of its slot.</summary>
    private const float OpenSeconds = 0.25f;

    private static readonly Color msGold = new Color(0.925f, 0.77f, 0.44f, 1f);

    private static readonly Color msBadgeFill = new Color(0.18f, 0.094f, 0.035f, 1f);

    private Texture2D mODiscTexture;

    /// <summary>The scroll arrow, pointing up (drawn flipped for down).</summary>
    private Texture2D mOArrowTexture;

    /// <summary>The badge under the pointer, lighter, as a button.</summary>
    private Texture2D mODiscHoverTexture;

    private static readonly Color msBadgeHoverFill = new Color(0.45f, 0.26f, 0.09f, 1f);

    private const string CloseMark = "\u00D7";

    private Texture2D mORingTexture;

    private Image mOHoverLine;

    /// <summary>A press that took a thing and may still turn into a drag.</summary>
    private bool mbPressDrag;

    private Vector2 mOPressStart;

    private Window mOPressWindow;

    private int miPressSlot = -1;

    /// <summary>Where the thing on the pointer came from, to put it back when the bags close.</summary>
    private UWObject mOOriginContainer;

    private int miOriginIndex = -1;

    private bool mbOriginBackpack;

    /// <summary>The load the open bags were last read from (UWHelpNotes.LoadCount), and the
    /// inventory load they were resolved against.</summary>
    private int miLoadCount = -1;

    private int miModelLoadCount = -1;

    private UWInventoryModel mOResolvedModel;

    /// <summary>The thing in the use mode, applied by the next left click; null for none.</summary>
    private UWObject mOUseItem;

    public bool IsUsing => mOUseItem != null;

    /// <summary>The anvil's use mode freed the pointer and opened the bags itself: both go back
    /// when it ends.</summary>
    private bool mbUseFreedPointer;

    private bool mbUseOpenedBags;

    /// <summary>The paperdoll slot the thing on the pointer came from.</summary>
    private UWArmorItemMap.BodySlot? mOOriginEquip;

    /// <summary>The press took its thing from a paperdoll slot (mePressEquip), not a window.</summary>
    private bool mbPressEquip;

    private UWArmorItemMap.BodySlot mePressEquip;

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
        mOInteraction = GetComponent<Interaction>();
        mOScheme = GetComponentInParent<UWControlScheme>();
        mOFont = Resources.Load<Font>("Fonts/LexendExa");
    }

    private void fDestroyTextures()
    {
        if (mOSlotTexture != null)
            Destroy(mOSlotTexture);

        if (mODiscTexture != null)
            Destroy(mODiscTexture);

        if (mOArrowTexture != null)
            Destroy(mOArrowTexture);

        if (mODiscHoverTexture != null)
            Destroy(mODiscHoverTexture);

        if (mORingTexture != null)
            Destroy(mORingTexture);

        foreach (Window lOWindow in mOWindows)
        {
            if (lOWindow.BackTexture != null)
                Destroy(lOWindow.BackTexture);
        }

        foreach (Texture2D lOIcon in mOIcons.Values)
            Destroy(lOIcon);

        mOIcons.Clear();
        fDestroySplitTexture();
        fDestroyMenuTexture();
    }

    private UWInventory fInventory()
    {
        if (mOInventory == null)
        {
            mOInventory = GetComponent<UWInventory>();

            if (mOInventory == null && Camera.main != null)
                mOInventory = Camera.main.GetComponent<UWInventory>();
        }

        return mOInventory;
    }

    private DataImport fData()
    {
        return mOUi != null ? mOUi.mOUWData : null;
    }

    private bool fIsModern()
    {
        return mOScheme != null && mOScheme.Current == UWControlScheme.SchemeEnum.Modern;
    }

    /// <summary>Shown where the modern HUD is shown (not in a conversation, a screen menu or a
    /// cutscene).</summary>
    private bool fIsShown()
    {
        return fIsModern() && UWModernHud.Instance != null && (UWModernHud.Instance.IsShowing || UWModernHud.Instance.IsTalking)
            && fData() != null && fInventory() != null;
    }

    /// <summary>Opens the bag windows (the modern conversation, for the trade).</summary>
    public void Open()
    {
        fOpen();
    }

    /// <summary>The left edge of the bag windows shown, in screen pixels (the screen's width
    /// when none) - the modern conversation keeps left of them.</summary>
    public float LeftEdge { get; private set; } = float.MaxValue;

    // ------------------------------------------------- Opening and closing

    /// <summary>B: the bag windows open or shut; the pointer stays as it is.</summary>
    private void fToggle()
    {
        if (mbOpen)
            Close();
        else
            fOpen();
    }

    /// <summary>How long the gamepad's Y is held to open every bag - a little longer than the
    /// use's hold (Interaction.ModernUseHoldSeconds, 0.4 s; per user, 2026-10-07).</summary>
    private const float PadOpenAllSeconds = 0.5f;

    /// <summary>Seconds the gamepad's Y is held, -1 when it is not.</summary>
    private float mfPadPanelHeld = -1f;

    /// <summary>
    /// THE GAMEPAD'S PANEL BUTTON, d-pad down since 2026-10-07 (Y before; UWGamepad): TAPPED, on the release, the character panel - the bags alone
    /// while the panel is pinned, where it had nothing to do (per user's test, 2026-10-07); HELD,
    /// every carried bag opens (fOpenAllBags, per user the same day).
    /// </summary>
    private void fUpdatePadPanelKey(InputAction pOKey)
    {
        // Not while the pad's pointer scrolls with the d-pad (UWGamepadPointer.Takes).
        if (pOKey.WasPressedThisFrame())
        {
            mfPadPanelHeld = UWGamepad.IsFromGamepad(pOKey) && !UWGamepadPointer.Takes(pOKey) ? 0f : -1f;
            return;
        }

        if (mfPadPanelHeld < 0f)
            return;

        if (pOKey.IsPressed())
        {
            mfPadPanelHeld += Time.unscaledDeltaTime;

            if (mfPadPanelHeld >= PadOpenAllSeconds)
            {
                mfPadPanelHeld = -1f;
                fOpenAllBags();
            }

            return;
        }

        mfPadPanelHeld = -1f;

        if (UWModernPanel.Pinned)
            fToggle();
        else
            fTogglePanel();
    }

    /// <summary>Every carried bag opens, in the backpack's order with a bag's inner bags after it -
    /// not the rune bag, whose shelf comes with the spells (as the click on the shut backpack
    /// leaves it). Passes, because an inner bag is found once its parent's contents are loaded
    /// (fToggleBag).</summary>
    private void fOpenAllBags()
    {
        UWInventory lOInventory = fInventory();

        if (lOInventory == null)
            return;

        if (!mbOpen)
            fOpen();

        List<UWObject> lONew = new List<UWObject>();

        for (int liPass = 0; liPass < 8; liPass++)
        {
            lONew.Clear();

            foreach (UWObject lOItem in lOInventory.Model.EnumerateAll())
            {
                if (lOItem != null && lOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers
                    && lOItem.ID != UWObjectMechanics.RuneBagId && !mOOpenBags.Contains(lOItem) && !lONew.Contains(lOItem))
                    lONew.Add(lOItem);
            }

            if (lONew.Count == 0)
                return;

            foreach (UWObject lOBag in lONew)
            {
                if (!mOOpenBags.Contains(lOBag))
                    fToggleBag(lOBag);
            }
        }
    }

    /// <summary>C: the character panel slides out or in (on its Character tab); the pointer stays
    /// as it is, and a pinned panel has nothing to do (per user, 2026-10-04).</summary>
    private void fTogglePanel()
    {
        UWModernPanel lOPanel = fPanel();

        if (lOPanel == null || UWModernPanel.Pinned)
            return;

        if (lOPanel.IsOpen && lOPanel.Tab == UWModernPanel.TabEnum.Character)
            lOPanel.Close();
        else
            lOPanel.Open(UWModernPanel.TabEnum.Character);
    }

    private void fOpen()
    {
        mbOpen = true;
    }

    /// <summary>Shuts the bag windows. What hangs on the pointer goes back where it came from, else
    /// wherever there is room (DropCursorItemAnywhere); if there is none it stays, and so do the
    /// bags (false).</summary>
    public bool Close()
    {
        if (!mbOpen)
            return true;

        if (!fReturnCursorItem())
        {
            if (mOInteraction != null)
                mOInteraction.AddMessage("There is no room for it in your pack.");

            return false;
        }

        fGiveUpPointer();

        return true;
    }

    private void fGiveUpPointer()
    {
        mbOpen = false;
        mbPressDrag = false;
    }

    private bool fReturnCursorItem()
    {
        UWInventory lOInventory = fInventory();

        if (lOInventory == null || lOInventory.CursorItem == null)
            return true;

        bool lbBack = false;

        if (fReturnToSplitOrigin(lOInventory))
            lbBack = true;
        else if (mOOriginEquip.HasValue && lOInventory.GetEquipped(mOOriginEquip.Value) == null)
            lbBack = lOInventory.DropCursorItemInEquip(mOOriginEquip.Value);
        else if (mbOriginBackpack && miOriginIndex >= 0 && lOInventory.Backpack[miOriginIndex] == null)
            lbBack = lOInventory.DropCursorItemInBackpack(miOriginIndex);
        else if (mOOriginContainer != null && fIsCarried(mOOriginContainer))
            lbBack = lOInventory.InsertCursorItemInContainer(mOOriginContainer, miOriginIndex);

        if (!lbBack && lOInventory.CursorItem != null)
            lOInventory.DropCursorItemAnywhere();

        fForgetOrigin();
        lOInventory.ApplyCarriedLight();

        return lOInventory.CursorItem == null;
    }

    private void fForgetOrigin()
    {
        mOSplitOrigin = null;
        mOOriginContainer = null;
        miOriginIndex = -1;
        mbOriginBackpack = false;
        mOOriginEquip = null;
    }

    /// <summary>For the character panel closing while the bags are shut: what hangs on the
    /// pointer goes back where it came from, else wherever there is room; false if it stays.</summary>
    public bool ReturnCursorItem()
    {
        return fReturnCursorItem();
    }

    private static UWModernPanel fPanel()
    {
        return UWModernPanel.Instance;
    }

    /// <summary>The bags take clicks while their pointer is out or the character panel is open.</summary>
    private bool fIsActive()
    {
        return mbOpen || (fPanel() != null && fPanel().IsOpen);
    }

    /// <summary>The paperdoll slot under the pointer, if the panel shows one there.</summary>
    private static bool fPanelSlotAt(Vector2 pOPointer, out UWArmorItemMap.BodySlot peSlot)
    {
        peSlot = UWArmorItemMap.BodySlot.Helmet;

        return fPanel() != null && fPanel().TryGetEquipSlotAt(pOPointer, out peSlot);
    }

    // ------------------------------------------------- Input

    private void Update()
    {
        if (!fIsShown())
        {
            // Leaving the modern scheme puts the pointer item back and gives the pointer up; a
            // thing with no room stays on it, the classic drag takes it over (UWItemDrag).
            if (mbOpen && !fIsModern() && !Close())
                fGiveUpPointer();

            fEndSplit();
            fCloseMenu();
            return;
        }

        UWModernHud lOHud = UWModernHud.Instance;

        // The question box has the mouse (UWModernQuestion).
        if (UWModernQuestion.IsBlocking)
            return;

        // The split box has the keys and the clicks while it is open (StackSplit).
        if (mOSplitItem != null)
        {
            if (lOHud.IsOpen || !UWModernPointer.IsFree || mOScheme.Controls == null || (mOUi != null && mOUi.IsMapVisible))
                fEndSplit();
            else
                fUpdateSplit(mOScheme.Controls);

            return;
        }

        // The context menu likewise (ContextMenu.cs).
        if (mOMenuItem != null)
        {
            if (lOHud.IsOpen || !UWModernPointer.IsFree || mOScheme.Controls == null || (mOUi != null && mOUi.IsMapVisible))
                fCloseMenu();
            else
                fUpdateMenu(mOScheme.Controls);

            return;
        }

        if (lOHud.IsOpen || UWControls.IsTextEntryActive || (mOUi != null && mOUi.IsMapVisible))
            return;

        UWControls lOControls = mOScheme.Controls;

        if (lOControls == null)
            return;

        fResolveOpenBags();

        // IN A CONVERSATION THE PANEL STAYS (per user, 2026-10-08: the d-pad's down, which steps the
        // answers there, closed it) - the conversation opened it on its Character tab for the trade
        // (UWModernConversation.fBegin); neither the pad nor C toggles it meanwhile.
        bool lbTalking = UWConversationScreen.IsAnyOpen;

        // The gamepad's panel button has its own tap and hold (fUpdatePadPanelKey); C acts on the press.
        if (!lbTalking)
            fUpdatePadPanelKey(lOControls.Player.ToggleInventory);
        else
            mfPadPanelHeld = -1f;

        if (lOControls.Player.ToggleInventory.WasPressedThisFrame())
        {
            if (!UWGamepad.IsFromGamepad(lOControls.Player.ToggleInventory) && !lbTalking)
                fTogglePanel();
        }
        else if (lOControls.Player.ModernBags.WasPressedThisFrame())
            fToggle();
        else if (!fIsActive() && UWModernPointer.IsFree && Mouse.current != null
            && (lOControls.Player.CursorDrag.WasPressedThisFrame()
                || (lOControls.Player.Interact.WasPressedThisFrame() && UWModernPointer.SwitchedFrame != Time.frameCount)))
        {
            // The click that opens the bags does nothing else - the same click handled below as
            // well closed the container it had just opened (per user, 2026-10-04).
            fClickShutBackpack(Mouse.current.position.ReadValue());
            return;
        }

        if (!fIsActive())
        {
            if (mOUseItem != null)
                CancelUse();

            return;
        }

        fPruneBags();

        // The right button locked the pointer for a look around: the windows stay, the mouse is
        // the view's until it switches back.
        if (!UWModernPointer.IsFree)
        {
            mbPressDrag = false;
            return;
        }

        Mouse lOMouse = Mouse.current;

        if (lOMouse == null)
            return;

        Vector2 lOPointer = lOMouse.position.ReadValue();
        UWInventory lOInventory = fInventory();

        float lfWheel = lOMouse.scroll.ReadValue().y;

        if (Mathf.Abs(lfWheel) > 0.01f)
            fScroll(lOPointer, lfWheel > 0f ? -1 : 1);

        if (lOControls.Player.CursorDrag.WasPressedThisFrame())
            fLeftPressed(lOPointer, lOInventory);
        else if (lOControls.Player.CursorDrag.WasReleasedThisFrame() && !mbTradePress)
            fLeftReleased(lOPointer, lOInventory);

        fUpdateTradePress(lOControls, lOPointer, lOInventory);

        if (lOControls.Player.Interact.WasPressedThisFrame() && mOUseItem != null
            && UWModernPointer.SwitchedFrame != Time.frameCount)
            CancelUse();
        else if (lOControls.Player.Interact.WasPressedThisFrame() && lOInventory.CursorItem == null
            && UWModernPointer.SwitchedFrame != Time.frameCount)
            fRightPressed(lOPointer, lOInventory);
    }

    private void fLeftPressed(Vector2 pOPointer, UWInventory pOInventory)
    {
        if (pOInventory.CursorItem != null)
        {
            fPlace(pOPointer, pOInventory);
            return;
        }

        if (mOUseItem != null)
        {
            fApplyUse(pOPointer, pOInventory);
            return;
        }

        if (fFindCloseBadge(pOPointer, out Window lOClosing))
        {
            fCloseBag(lOClosing.Container);
            return;
        }

        // A conversation's trade slot (Trade.cs).
        if (fTradePressed(pOPointer))
            return;

        // A spell waiting for an item (Name Enchantment, UWHudRunes.fAfterModernCast) takes the
        // thing clicked.
        if (mOInteraction != null && mOInteraction.IsItemTargetSpellPending)
        {
            UWObject lOTarget = null;

            if (fPanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leTargetSlot))
                lOTarget = pOInventory.GetEquipped(leTargetSlot);
            else if (fFindSlot(pOPointer, out Window lOTargetWindow, out int liTargetSlot))
                lOTarget = fGetItem(lOTargetWindow, liTargetSlot);

            if (lOTarget != null)
            {
                mOInteraction.ResolveTargetSpellOnItem(lOTarget);
                return;
            }
        }

        if (fPanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leEquip))
        {
            UWObject lOEquipped = pOInventory.GetEquipped(leEquip);

            if (lOEquipped == null)
                return;

            if (fTryBeginSplit(lOEquipped, null, -1, leEquip, new Rect(pOPointer, Vector2.zero)))
                return;

            fTakeEquipped(leEquip, pOInventory);

            mbPressDrag = pOInventory.CursorItem != null;
            mOPressStart = pOPointer;
            mOPressWindow = null;
            miPressSlot = -1;
            mbPressEquip = true;
            mePressEquip = leEquip;
            return;
        }

        if (!fFindSlot(pOPointer, out Window lOWindow, out int liSlot))
            return;

        UWObject lOItem = fGetItem(lOWindow, liSlot);

        if (lOItem == null)
            return;

        if (fTryBeginSplit(lOItem, lOWindow, liSlot, UWArmorItemMap.BodySlot.Helmet, lOWindow.Slots[liSlot].ScreenRect))
            return;

        fTake(lOWindow, liSlot, pOInventory);

        mbPressDrag = pOInventory.CursorItem != null;
        mOPressStart = pOPointer;
        mOPressWindow = lOWindow;
        miPressSlot = liSlot;
        mbPressEquip = false;
    }

    /// <summary>Takes the thing in a paperdoll slot onto the pointer and remembers where from.</summary>
    private void fTakeEquipped(UWArmorItemMap.BodySlot peSlot, UWInventory pOInventory)
    {
        fForgetOrigin();
        pOInventory.BeginDragFromEquip(peSlot);
        mOOriginEquip = peSlot;
        fPruneBags();
    }

    /// <summary>A press that took a thing ends: on the same slot without moving it stays on
    /// the pointer (click and click), anywhere else it is put down there.</summary>
    private void fLeftReleased(Vector2 pOPointer, UWInventory pOInventory)
    {
        if (!mbPressDrag)
            return;

        mbPressDrag = false;

        if (pOInventory.CursorItem == null)
            return;

        bool lbSameSlot = mbPressEquip
            ? fPanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leEquip) && leEquip == mePressEquip
            : fFindSlot(pOPointer, out Window lOWindow, out int liSlot) && lOWindow == mOPressWindow && liSlot == miPressSlot;

        if (lbSameSlot || Vector2.Distance(pOPointer, mOPressStart) < ClickMoveThreshold)
            return;

        fPlace(pOPointer, pOInventory);
    }

    /// <summary>The right button on a thing in a bag window or on the paperdoll: its context menu
    /// (ContextMenu.cs) - a CONTAINER opens or closes at once instead, the rune bag opens the rune
    /// panel (per user, 2026-10-04: the most frequent use by far); Shift with it gives the menu.</summary>
    private void fRightPressed(Vector2 pOPointer, UWInventory pOInventory)
    {
        if (mOInteraction == null)
            return;

        // In a conversation a trade slot looks (Trade.cs); one's own things keep their menu,
        // without the uses (ContextMenu.cs).
        if (fTradeLook(pOPointer))
            return;

        if (fPanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leEquip))
        {
            UWObject lOWorn = pOInventory.GetEquipped(leEquip);

            if (lOWorn != null && !fOpenContainerAtOnce(lOWorn))
                fOpenMenu(lOWorn, null, -1, leEquip, pOPointer);

            return;
        }

        if (!fFindSlot(pOPointer, out Window lOWindow, out int liSlot))
            return;

        UWObject lOItem = fGetItem(lOWindow, liSlot);

        if (lOItem != null && !fOpenContainerAtOnce(lOItem))
            fOpenMenu(lOItem, lOWindow, liSlot, UWArmorItemMap.BodySlot.Helmet, pOPointer);
    }

    /// <summary>A container without Shift: open or close it (the rune bag: the rune panel); true if
    /// that was it.</summary>
    private bool fOpenContainerAtOnce(UWObject pOItem)
    {
        if (UWControls.IsShiftHeld || pOItem.GetCategory() != UWObject.ObjectCategoryEnum.Containers)
            return false;

        if (pOItem.ID == UWObjectMechanics.RuneBagId)
        {
            if (UWModernRunePanel.Instance != null)
                UWModernRunePanel.Instance.Open(UWModernRunePanel.TabEnum.Runes);
        }
        else
        {
            fToggleBag(pOItem);
        }

        return true;
    }

    /// <summary>The use mode: the thing waits for the left click that says what it goes onto.
    /// From the world (the anvil) the bags open and the pointer frees, as its target is a carried
    /// thing - the one exception to the pointer being the right button's (per user, 2026-10-04:
    /// with the pointer locked the anvil did nothing, and the right button to free it cancelled);
    /// both go back when the mode ends.</summary>
    public void BeginUse(UWObject pOItem, bool pbFromWorld)
    {
        if (pOItem == null)
            return;

        mbUseOpenedBags = false;
        mbUseFreedPointer = false;

        if (pbFromWorld && !mbOpen)
        {
            fOpen();
            mbUseOpenedBags = true;
        }

        if (pbFromWorld && !UWModernPointer.IsFree)
        {
            UWModernPointer.Free();
            mbUseFreedPointer = true;
        }

        mOUseItem = pOItem;

        UWItemDrag lODrag = UWScene.ItemDrag;

        if (lODrag != null)
            lODrag.ReportUsePrompt(pOItem);
    }

    public void CancelUse()
    {
        mOUseItem = null;

        // What the anvil opened and freed goes back (the flags first: locking cancels again).
        bool lbLock = mbUseFreedPointer && !UWModernPointer.HoldToLook;
        bool lbClose = mbUseOpenedBags && mbOpen;

        mbUseFreedPointer = false;
        mbUseOpenedBags = false;

        if (lbClose)
            Close();

        // A question follows (the anvil's repair): the pointer locks only after its answer. Locked
        // now and freed by the question box the next moment, Windows kept the pointer invisible
        // (per user, 2026-10-04).
        if (lbLock && mOInteraction != null && mOInteraction.IsAskingYesNo)
            UWModernQuestion.LockWhenAnswered();
        else if (lbLock)
            UWModernPointer.Lock();
    }

    /// <summary>The click of the use mode: onto the thing in a slot, onto nothing on another window
    /// of the modern UI, else onto the world under the pointer. The mode ends either way, as the
    /// classic one does.</summary>
    private void fApplyUse(Vector2 pOPointer, UWInventory pOInventory)
    {
        UWObject lOUse = mOUseItem;
        UWItemDrag lODrag = UWScene.ItemDrag;
        UWObject lOTarget = null;

        if (fPanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leSlot))
            lOTarget = pOInventory.GetEquipped(leSlot);
        else if (fFindSlot(pOPointer, out Window lOWindow, out int liSlot))
            lOTarget = fGetItem(lOWindow, liSlot);

        bool lbOverUi = UWModernPointer.IsOverUi(pOPointer);

        // The mode ends right after the thing acted (what the anvil opened goes back - after it
        // asked its question, see CancelUse). Its own flag first, so the thing is not used twice.
        mOUseItem = null;

        if (lODrag != null && lOTarget != null)
            lODrag.ApplyItemModern(lOUse, lOTarget, null);
        else if (lODrag != null && !lbOverUi)
        {
            lODrag.ApplyItemModern(lOUse, null, pOPointer);
            pOInventory.ApplyCarriedLight();
        }

        CancelUse();
    }

    /// <summary>Q over the interface (Interaction.fUpdateModernActions): looks at the thing in
    /// the character panel's slot or the bag slot under the pointer, as the menu's Look does.
    /// False when there is none.</summary>
    public bool TryLookAt(Vector2 pOPointer)
    {
        UWInventory lOInventory = fInventory();
        UWObject lOItem = null;

        if (lOInventory != null && fPanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leSlot))
            lOItem = lOInventory.GetEquipped(leSlot);
        else if (fFindSlot(pOPointer, out Window lOWindow, out int liSlot))
            lOItem = fGetItem(lOWindow, liSlot);

        if (lOItem == null)
            return false;

        fLook(lOItem);
        return true;
    }

    /// <summary>
    /// E tapped over the interface (Interaction.fUpdateModernUseKey, per user 2026-10-07): the
    /// thing in the slot under the pointer does what makes sense for it, the menu's first entry -
    /// a container opens or closes (the rune bag: the rune panel), a thing to wear in a bag is put
    /// on, anything else is used as the menu's Use uses it (with a target: the use mode). Worn
    /// armour and a thing without a use stay as they are, silently. In a conversation nothing is
    /// used, as in the menu. False when no slot of the bags or the panel is under the pointer.
    /// </summary>
    public bool TryUseAt(Vector2 pOPointer)
    {
        UWInventory lOInventory = fInventory();

        if (lOInventory == null || lOInventory.CursorItem != null || mOUseItem != null)
            return false;

        UWObject lOItem;
        Window lOWindow = null;
        int liSlot = -1;
        bool lbEquipped = false;

        if (fPanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leEquip))
        {
            lOItem = lOInventory.GetEquipped(leEquip);
            lbEquipped = true;
        }
        else if (fFindSlot(pOPointer, out lOWindow, out liSlot))
            lOItem = fGetItem(lOWindow, liSlot);
        else
            return false;

        if (lOItem == null)
            return true;

        if (lOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
        {
            if (lOItem.ID != UWObjectMechanics.RuneBagId)
                fToggleBag(lOItem);
            else if (UWModernRunePanel.Instance != null && !fIsTalking())
                UWModernRunePanel.Instance.Open(UWModernRunePanel.TabEnum.Runes);

            return true;
        }

        if (fIsTalking())
            return true;

        bool lbWearable = fTryGetEquipSlot(lOItem, lOInventory, out UWArmorItemMap.BodySlot leTarget);

        if (lbWearable)
        {
            if (!lbEquipped)
                fEquip(lOWindow, liSlot, leTarget, lOInventory);

            return true;
        }

        UWItemDrag lODrag = UWScene.ItemDrag;

        if (lODrag != null && lODrag.UseCarriedItem(lOItem))
            return true;

        if (lODrag != null && lODrag.NeedsUseTarget(lOItem))
            BeginUse(lOItem, false);

        return true;
    }

    /// <summary>Looks at a carried thing as the menu's Look does - for the action bar's slots.</summary>
    public void LookAt(UWObject pOItem)
    {
        if (pOItem != null)
            fLook(pOItem);
    }

    private void fLook(UWObject pOItem)
    {
        try
        {
            // In a conversation the look replaces the answers for a moment (the messages are
            // hidden there), as the classic screen shows it.
            if (fIsTalking())
            {
                fShowTalkText(mOInteraction.DescribeInventoryItem(pOItem));
                return;
            }
        }
        catch
        {
            return;
        }

        if (mOInteraction.TryReadBook(pOItem, true))
            return;

        try
        {
            mOInteraction.AddMessage(mOInteraction.DescribeInventoryItem(pOItem));
        }
        catch
        {
            // As UWItemDrag: a missing string is no message, not an error.
        }
    }

    private void fScroll(Vector2 pOPointer, int piRows)
    {
        foreach (Window lOWindow in mOWindows)
        {
            if (lOWindow.Container == null || !lOWindow.Root.gameObject.activeSelf
                || !lOWindow.ScreenRect.Contains(pOPointer))
                continue;

            mOScroll.TryGetValue(lOWindow.Container, out int liRows);
            mOScroll[lOWindow.Container] = Mathf.Max(0, liRows + piRows);
            return;
        }
    }

    // ------------------------------------------------- The work

    private UWObject fGetItem(Window pOWindow, int piSlot)
    {
        UWInventory lOInventory = fInventory();

        if (pOWindow.Container == null)
            return piSlot >= 0 && piSlot < lOInventory.Backpack.Length ? lOInventory.Backpack[piSlot] : null;

        List<UWObject> lOContents = pOWindow.Container.Contents;
        int liIndex = pOWindow.FirstIndex + piSlot;

        return lOContents != null && liIndex >= 0 && liIndex < lOContents.Count ? lOContents[liIndex] : null;
    }

    /// <summary>Takes the thing in a slot onto the pointer and remembers where from.</summary>
    private void fTake(Window pOWindow, int piSlot, UWInventory pOInventory)
    {
        fForgetOrigin();

        if (pOWindow.Container == null)
        {
            pOInventory.BeginDragFromBackpack(piSlot);
            mbOriginBackpack = true;
            miOriginIndex = piSlot;
        }
        else
        {
            pOInventory.BeginDragFromContainerItem(pOWindow.Container, pOWindow.FirstIndex + piSlot);
            mOOriginContainer = pOWindow.Container;
            miOriginIndex = pOWindow.FirstIndex + piSlot;
        }

        fPruneBags();
    }

    /// <summary>
    /// Puts the thing on the pointer down where the pointer is: a backpack slot (a container
    /// there takes it inside, as in the original), a bag slot (the model stacks, combines,
    /// swaps or appends), a window's free space, or - outside the windows - the world.
    /// </summary>
    private void fPlace(Vector2 pOPointer, UWInventory pOInventory)
    {
        bool lbPlaced;

        // A conversation's trade slot (Trade.cs).
        if (fTryPlaceInTrade(pOPointer, pOInventory, out bool lbTraded))
        {
            if (lbTraded)
            {
                fForgetOrigin();
                pOInventory.ApplyCarriedLight();
                fPruneBags();
            }

            return;
        }

        if (fPanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leEquip))
        {
            // What lands on the head is eaten, as on the classic page (UWItemDrag, per user
            // 2026-10-04); a stack is refused (one at a time) and stays on the pointer, its origin with it.
            UWItemDrag lODrag = UWScene.ItemDrag;

            if (leEquip == UWArmorItemMap.BodySlot.Helmet && lODrag != null && lODrag.TryConsumeCursorItemOnHead())
            {
                if (pOInventory.CursorItem == null)
                    fForgetOrigin();

                return;
            }

            lbPlaced = pOInventory.DropCursorItemInEquip(leEquip);

            if (!lbPlaced)
                fReportRejection(pOInventory);
        }
        else if (fFindSlot(pOPointer, out Window lOWindow, out int liSlot))
        {
            if (lOWindow.Container == null)
            {
                UWObject lOThere = pOInventory.Backpack[liSlot];

                lbPlaced = lOThere != null && lOThere.GetCategory() == UWObject.ObjectCategoryEnum.Containers
                    ? pOInventory.DropCursorItemIntoContainerItem(lOThere)
                    : pOInventory.DropCursorItemInBackpack(liSlot);
            }
            else
            {
                lbPlaced = pOInventory.DropCursorItemInContainerAt(lOWindow.Container, lOWindow.FirstIndex + liSlot);
            }

            if (!lbPlaced)
                fReportRejection(pOInventory);
        }
        else if (fFindWindow(pOPointer, out Window lOOnWindow))
        {
            lOWindow = lOOnWindow;

            lbPlaced = lOWindow.Container == null
                ? pOInventory.DropCursorItemInFirstFreeBackpackSlot()
                : pOInventory.DropCursorItemInContainerAt(lOWindow.Container,
                    lOWindow.Container.Contents != null ? lOWindow.Container.Contents.Count : 0);

            if (!lbPlaced)
                fReportRejection(pOInventory);
        }
        else if (UWModernActionBar.Instance != null && UWModernActionBar.Instance.TryGetSlotAt(pOPointer, out int liBarSlot))
        {
            // Onto the action bar: the slot is bound to the piece, which goes back to its place
            // (per user, 2026-10-03: a slot holds the single piece, not a copy).
            UWModernActionBar.Instance.TryBind(liBarSlot, pOInventory.CursorItem);
            fReturnCursorItem();
            return;
        }
        else if ((fPanel() != null && fPanel().Contains(pOPointer)) || fIsTalking()
            || (UWModernRunePanel.Instance != null && UWModernRunePanel.Instance.Contains(pOPointer))
            || (UWModernMinimap.Instance != null && UWModernMinimap.Instance.Contains(pOPointer)))
        {
            // Nothing goes into the world through the panels or the minimap, nor in a conversation.
            lbPlaced = false;
        }
        else
        {
            UWPlayerThrow.GetPointerInFullScreenView(pOPointer, out int liX, out int liY);

            Camera lOCamera = Camera.main;

            lbPlaced = lOCamera != null && UWPlayerThrow.TryDropOrThrowAtView(liX, liY, lOCamera.transform,
                pOInventory, UWScene.LevelLoader, mOInteraction);
        }

        if (!lbPlaced)
            return;

        // A swap leaves the displaced thing on the pointer; its origin is unknown then.
        fForgetOrigin();
        pOInventory.ApplyCarriedLight();
        fPruneBags();
    }

    private void fReportRejection(UWInventory pOInventory)
    {
        if (mOInteraction == null)
            return;

        switch (pOInventory.LastContainerRejection)
        {
            case UWContainerCapacity.ResultEnum.RunesOnly:
                mOInteraction.AddMessage(mOInteraction.GetGeneralMessage(RunesOnlyMessage));
                break;

            case UWContainerCapacity.ResultEnum.WrongType:
                mOInteraction.AddMessage(mOInteraction.GetGeneralMessage(DoesNotFitMessage));
                break;

            case UWContainerCapacity.ResultEnum.TooHeavy:
                UWObject lOContainer = pOInventory.LastContainerRejectionContainer;

                if (lOContainer != null)
                    mOInteraction.AddMessage("The " + UWItemDescriptions.GetBareName(lOContainer.ID, fData()) + " is too full.");
                break;
        }
    }

    /// <summary>Where the right button puts a piece on: armour on its place, a ring on a free
    /// finger, a weapon into the weapon hand. Nothing else.</summary>
    private static bool fTryGetEquipSlot(UWObject pOItem, UWInventory pOInventory, out UWArmorItemMap.BodySlot peSlot)
    {
        peSlot = UWArmorItemMap.BodySlot.RightHandSlot;

        if (UWArmorItemMap.TryGet(pOItem.ID, out UWArmorItemMap.Entry lOEntry))
        {
            peSlot = lOEntry.Slot;
            return true;
        }

        if (UWArmorItemMap.Fits(pOItem, UWArmorItemMap.BodySlot.LeftRing))
        {
            peSlot = pOInventory.GetEquipped(UWArmorItemMap.BodySlot.LeftRing) == null
                || pOInventory.GetEquipped(UWArmorItemMap.BodySlot.RightRing) != null
                ? UWArmorItemMap.BodySlot.LeftRing
                : UWArmorItemMap.BodySlot.RightRing;
            return true;
        }

        if (pOItem.ID <= LastMeleeWeaponId || (pOItem.ID >= FirstLauncherId && pOItem.ID <= LastLauncherId))
        {
            peSlot = pOInventory.MainHandSlot;
            return true;
        }

        return false;
    }

    /// <summary>Puts a piece on through the pointer, as a drag onto the paperdoll would: what
    /// was worn there goes back to the piece's place; if that refuses it, it stays on the
    /// pointer.</summary>
    private void fEquip(Window pOWindow, int piSlot, UWArmorItemMap.BodySlot peSlot, UWInventory pOInventory)
    {
        fTake(pOWindow, piSlot, pOInventory);

        if (pOInventory.CursorItem == null)
            return;

        if (!pOInventory.DropCursorItemInEquip(peSlot))
        {
            fReturnCursorItem();
            return;
        }

        if (pOInventory.CursorItem != null)
            fReturnCursorItem();
        else
            fForgetOrigin();

        pOInventory.ApplyCarriedLight();
    }

    /// <summary>
    /// Puts a carried piece on wherever it lies - the backpack, a bag or another slot - as the
    /// right button does from a window: through the pointer, what was worn there goes back to
    /// the piece's place (for the action bar's weapons, UWModernActionBar). False with a thing
    /// already on the pointer or a piece not carried.
    /// </summary>
    internal bool EquipCarried(UWObject pOItem, UWArmorItemMap.BodySlot peSlot)
    {
        UWInventory lOInventory = fInventory();

        if (lOInventory == null || pOItem == null || lOInventory.CursorItem != null)
            return false;

        fForgetOrigin();

        int liBackpack = System.Array.IndexOf(lOInventory.Backpack, pOItem);

        if (liBackpack >= 0)
        {
            lOInventory.BeginDragFromBackpack(liBackpack);
            mbOriginBackpack = true;
            miOriginIndex = liBackpack;
        }
        else if (fFindEquipped(lOInventory, pOItem, out UWArmorItemMap.BodySlot leFrom))
        {
            if (leFrom == peSlot)
                return true;

            lOInventory.BeginDragFromEquip(leFrom);
            mOOriginEquip = leFrom;
        }
        else if (fFindHolder(lOInventory, pOItem, out UWObject lOHolder))
        {
            int liIndex = lOHolder.Contents.IndexOf(pOItem);

            lOInventory.BeginDragFromContainerItem(lOHolder, liIndex);
            mOOriginContainer = lOHolder;
            miOriginIndex = liIndex;
        }
        else
        {
            return false;
        }

        fPruneBags();

        if (lOInventory.CursorItem == null)
            return false;

        if (!lOInventory.DropCursorItemInEquip(peSlot) || lOInventory.CursorItem != null)
            fReturnCursorItem();
        else
            fForgetOrigin();

        lOInventory.ApplyCarriedLight();

        return true;
    }

    private static bool fFindEquipped(UWInventory pOInventory, UWObject pOItem, out UWArmorItemMap.BodySlot peSlot)
    {
        UWObject[] lOSlots = pOInventory.EquipSlots;

        for (int liSlot = 0; liSlot < lOSlots.Length; liSlot++)
        {
            if (lOSlots[liSlot] == pOItem)
            {
                peSlot = (UWArmorItemMap.BodySlot)liSlot;
                return true;
            }
        }

        peSlot = UWArmorItemMap.BodySlot.Helmet;
        return false;
    }

    /// <summary>The carried container a piece lies in directly.</summary>
    private static bool fFindHolder(UWInventory pOInventory, UWObject pOItem, out UWObject pOHolder)
    {
        foreach (UWObject lOItem in pOInventory.Model.EnumerateAll())
        {
            if (lOItem.Contents != null && lOItem.Contents.Contains(pOItem))
            {
                pOHolder = lOItem;
                return true;
            }
        }

        pOHolder = null;
        return false;
    }

    /// <summary>A click on the backpack while the bags are shut: they open as with B, and a
    /// container clicked opens its window (not the rune bag, whose shelf comes with the spells).
    /// The click does nothing else.</summary>
    private void fClickShutBackpack(Vector2 pOPointer)
    {
        if (!fFindWindow(pOPointer, out Window lOWindow) || lOWindow.Container != null)
            return;

        fOpen();

        if (!fFindSlot(pOPointer, out Window lOSlotWindow, out int liSlot) || lOSlotWindow != lOWindow)
            return;

        UWObject lOItem = fGetItem(lOWindow, liSlot);

        if (lOItem != null && lOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers
            && lOItem.ID != UWObjectMechanics.RuneBagId && !mOOpenBags.Contains(lOItem))
            fToggleBag(lOItem);
    }

    /// <summary>The open bags into UWHelpNotes for saving (UWHelpNotes.SaveTo): their paths in the
    /// order they opened, and their numbers. Not yet resolved since the last load: what was read
    /// stays.</summary>
    public static void CaptureOpenBags()
    {
        UWModernBags lOBags = Instance;

        if (lOBags == null || lOBags.fInventory() == null || lOBags.miLoadCount != UWHelpNotes.LoadCount)
            return;

        UWInventoryModel lOModel = lOBags.fInventory().Model;
        List<string> lOPaths = new List<string>();
        List<int> lONumbers = new List<int>();
        List<int> lOIds = new List<int>();

        foreach (UWObject lOBag in lOBags.mOOpenBags)
        {
            string lsPath = UWInventoryPaths.PathOf(lOModel, lOBag);

            if (string.IsNullOrEmpty(lsPath))
                continue;

            lOPaths.Add(lsPath);
            lONumbers.Add(lOBags.mOBagNumbers.TryGetValue(lOBag, out int liNumber) ? liNumber : 0);
            lOIds.Add(lOBag.ID);
        }

        UWHelpNotes.OpenBagPaths = lOPaths.ToArray();
        UWHelpNotes.OpenBagNumbers = lONumbers.ToArray();
        UWHelpNotes.OpenBagIds = lOIds.ToArray();
    }

    /// <summary>After a load or a new character: the open bags read back once - for a save game
    /// only after its inventory has come in. They show when the bags open.</summary>
    private void fResolveOpenBags()
    {
        UWInventory lOInventory = fInventory();

        if (miLoadCount == UWHelpNotes.LoadCount || lOInventory == null)
            return;

        UWInventoryModel lOModel = lOInventory.Model;
        string[] lsPaths = UWHelpNotes.OpenBagPaths ?? new string[0];

        if (lsPaths.Length > 0 && lOModel == mOResolvedModel && lOModel.LoadCount == miModelLoadCount)
            return;

        miLoadCount = UWHelpNotes.LoadCount;
        mOResolvedModel = lOModel;
        miModelLoadCount = lOModel.LoadCount;

        mOOpenBags.Clear();
        mOScroll.Clear();
        mOBagNumbers.Clear();
        mOOpenedAt.Clear();

        UWLevelLoader lOLoader = UWScene.LevelLoader;
        List<UWObject> lOMasterlist = lOLoader != null && lOLoader.CurrentLevel != null ? lOLoader.CurrentLevel.Masterlist : null;
        int[] liNumbers = UWHelpNotes.OpenBagNumbers ?? new int[0];

        for (int liAt = 0; liAt < lsPaths.Length; liAt++)
        {
            UWObject lOBag = UWInventoryPaths.ResolveChecked(lOModel, lsPaths[liAt],
                UWHelpNotes.IdAt(UWHelpNotes.OpenBagIds, liAt), UWHelpNotes.IsStale, lOMasterlist);

            if (lOBag == null || lOBag.GetCategory() != UWObject.ObjectCategoryEnum.Containers || mOOpenBags.Contains(lOBag))
                continue;

            if (lOBag.Contents == null && lOMasterlist != null)
                lOBag.EnsureContentsLoaded(lOMasterlist);

            if (lOBag.Contents == null)
                continue;

            // A sensible number not taken yet, else the lowest free one.
            int liNumber = liAt < liNumbers.Length && liNumbers[liAt] > 0 && liNumbers[liAt] <= 99
                && !mOBagNumbers.ContainsValue(liNumbers[liAt])
                ? liNumbers[liAt] : fFreeNumber();

            mOOpenBags.Add(lOBag);
            mOScroll[lOBag] = 0;
            mOBagNumbers[lOBag] = liNumber;
        }
    }

    // ------------------------------------------------- The open bags

    private void fToggleBag(UWObject pOContainer)
    {
        if (mOOpenBags.Contains(pOContainer))
        {
            fCloseBag(pOContainer);
            return;
        }

        DataImport lOData = fData();
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (pOContainer.Contents == null && lOLoader != null && lOLoader.CurrentLevel != null)
            pOContainer.EnsureContentsLoaded(lOLoader.CurrentLevel.Masterlist);

        if (pOContainer.Contents == null || lOData == null)
            return;

        // From the character panel with the bags shut: they open with it, or nothing would show.
        if (!mbOpen)
            fOpen();

        mOOpenBags.Add(pOContainer);
        mOScroll[pOContainer] = 0;
        mOBagNumbers[pOContainer] = fFreeNumber();
        mOOpenedAt[pOContainer] = Time.unscaledTime;
    }

    /// <summary>The lowest number no open bag carries.</summary>
    private int fFreeNumber()
    {
        int liNumber = 1;

        while (mOBagNumbers.ContainsValue(liNumber))
            liNumber++;

        return liNumber;
    }

    private void fForgetBag(int piAt)
    {
        UWObject lOBag = mOOpenBags[piAt];

        mOOpenBags.RemoveAt(piAt);
        mOScroll.Remove(lOBag);
        mOBagNumbers.Remove(lOBag);
        mOOpenedAt.Remove(lOBag);
    }

    /// <summary>Closes a bag and every open bag inside it.</summary>
    private void fCloseBag(UWObject pOContainer)
    {
        for (int liAt = mOOpenBags.Count - 1; liAt >= 0; liAt--)
        {
            if (mOOpenBags[liAt] == pOContainer || fIsInside(mOOpenBags[liAt], pOContainer))
                fForgetBag(liAt);
        }
    }

    /// <summary>Closes the windows of bags that are no longer carried (taken onto the pointer,
    /// dropped, used up).</summary>
    private void fPruneBags()
    {
        for (int liAt = mOOpenBags.Count - 1; liAt >= 0; liAt--)
        {
            if (!fIsCarried(mOOpenBags[liAt]))
                fForgetBag(liAt);
        }
    }

    private bool fIsCarried(UWObject pOItem)
    {
        UWInventory lOInventory = fInventory();

        if (lOInventory == null)
            return false;

        foreach (UWObject lOItem in lOInventory.Model.EnumerateAll())
        {
            if (lOItem == pOItem)
                return true;
        }

        return false;
    }

    private static bool fIsInside(UWObject pOItem, UWObject pOContainer)
    {
        return fContains(pOContainer, pOItem, 0);
    }

    private static bool fContains(UWObject pOContainer, UWObject pOItem, int piDepth)
    {
        if (pOContainer?.Contents == null || piDepth > 16)
            return false;

        foreach (UWObject lOInside in pOContainer.Contents)
        {
            if (lOInside == pOItem || fContains(lOInside, pOItem, piDepth + 1))
                return true;
        }

        return false;
    }

    // ------------------------------------------------- Hit tests

    /// <summary>The middle of the backpack's first slot on the screen (pixels, bottom-left origin) -
    /// where the freed pointer appears (UWModernPointer). False while the bags are not laid out.</summary>
    public bool TryGetFirstBackpackSlot(out Vector2 pOCentre)
    {
        foreach (Window lOWindow in mOWindows)
        {
            if (lOWindow.Container != null || !lOWindow.Root.gameObject.activeSelf || lOWindow.SlotCount <= 0)
                continue;

            Rect lORect = lOWindow.Slots[0].ScreenRect;

            if (lORect.width <= 0f)
                break;

            pOCentre = lORect.center;
            return true;
        }

        pOCentre = Vector2.zero;
        return false;
    }

    private bool fFindSlot(Vector2 pOPointer, out Window pOWindow, out int piSlot)
    {
        foreach (Window lOWindow in mOWindows)
        {
            if (!lOWindow.Root.gameObject.activeSelf)
                continue;

            for (int liSlot = 0; liSlot < lOWindow.SlotCount; liSlot++)
            {
                if (lOWindow.Slots[liSlot].ScreenRect.Contains(pOPointer))
                {
                    pOWindow = lOWindow;
                    piSlot = liSlot;
                    return true;
                }
            }
        }

        pOWindow = null;
        piSlot = -1;
        return false;
    }

    /// <summary>The close button of a bag window under the pointer - only with nothing on the
    /// pointer; with a thing there a click on the window puts it in.</summary>
    private bool fFindCloseBadge(Vector2 pOPointer, out Window pOWindow)
    {
        UWInventory lOInventory = fInventory();

        if (lOInventory != null && lOInventory.CursorItem == null)
        {
            foreach (Window lOWindow in mOWindows)
            {
                if (lOWindow.Container != null && lOWindow.Root.gameObject.activeSelf && lOWindow.BadgeRect.Contains(pOPointer))
                {
                    pOWindow = lOWindow;
                    return true;
                }
            }
        }

        pOWindow = null;
        return false;
    }

    /// <summary>Whether a bag window (or the backpack) lies under a screen point - the minimap
    /// below leaves such clicks to the bags.</summary>
    public bool IsOverWindow(Vector2 pOPointer)
    {
        return fIsShown() && (fFindWindow(pOPointer, out Window _) || (IsSplitting && mOSplitRect.Contains(pOPointer))
            || (IsMenuOpen && mOMenuRect.Contains(pOPointer)));
    }

    private bool fFindWindow(Vector2 pOPointer, out Window pOWindow)
    {
        foreach (Window lOWindow in mOWindows)
        {
            if (lOWindow.Root.gameObject.activeSelf && lOWindow.ScreenRect.Contains(pOPointer))
            {
                pOWindow = lOWindow;
                return true;
            }
        }

        pOWindow = null;
        return false;
    }

    // ------------------------------------------------- Drawing

    private void LateUpdate()
    {
        if (!fIsShown())
        {
            if (mOCanvas != null)
                mOCanvas.enabled = false;

            LeftEdge = float.MaxValue;
            return;
        }

        if (mOCanvas == null)
            fBuild();

        mOCanvas.enabled = true;

        // Left of the docked character panel and its handle (UWModernPanel). Where the backpack
        // and the bag windows go is worked out engine-free in UWBagPlacement (since 2026-10-05).
        UWModernPanel lOPanel = fPanel();

        // Its own size and place (UWModernLayout): the backpack, the bag windows stacking from it.
        miScale = UWModernHud.PixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.Bags);

        fEnsureArt();

        UWInventory lOInventory = fInventory();
        DataImport lOData = fData();

        mOSourceRects.Clear();

        // The backpack, always.
        Window lOPack = fGetWindow(0);
        lOPack.Container = null;
        lOPack.FirstIndex = 0;

        float lfEdge = lOPanel != null ? Mathf.Min(Screen.width, lOPanel.LeftEdge) : Screen.width;
        float lfRight = lfEdge - (ScreenMargin * UWModernHud.PixelScale);
        float lfBottom = ScreenMargin * UWModernHud.PixelScale;
        bool lbFree = UWModernLayout.IsPlaced(UWModernLayout.ElementEnum.Bags);

        // The backpack with its load bar above it.
        float lfPackWidth = fWindowWidth() * miScale;
        float lfPackHeight = (fWindowRows(2) + LoadBarRows + 2) * miScale;
        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Bags,
            new Rect(lfRight - lfPackWidth, lfBottom, lfPackWidth, lfPackHeight));

        // The open bags' rows (their scrolling kept within them) and heights, in their order.
        List<UWObject> lOShownBags = new List<UWObject>();
        List<int> liBagRows = new List<int>();
        List<int> liBagHeights = new List<int>();

        if (mbOpen)
        {
            foreach (UWObject lOBag in mOOpenBags)
            {
                int liCount = lOBag.Contents != null ? lOBag.Contents.Count : 0;
                int liNeeded = (liCount / Columns) + 1;
                int liRows = Mathf.Clamp(liNeeded, MinRows, MaxRows);

                mOScroll.TryGetValue(lOBag, out int liScrolled);
                liScrolled = Mathf.Clamp(liScrolled, 0, Mathf.Max(0, liNeeded - liRows));
                mOScroll[lOBag] = liScrolled;

                lOShownBags.Add(lOBag);
                liBagRows.Add(liRows);
                liBagHeights.Add(fWindowRows(liRows));
            }
        }

        // The backpack pushed out left of an open character panel lying on it, the bag windows
        // stacked from it around the panel, the minimap and the action bar (UWBagPlacement).
        UWModernActionBar lOBar = UWModernActionBar.Instance;
        UWBagPlacement.Result lOPlace = UWBagPlacement.Place(new UWBagPlacement.Input
        {
            ScreenWidth = Screen.width,
            ScreenHeight = Screen.height,
            Scale = miScale,
            PixelScale = UWModernHud.PixelScale,
            Pack = lOPlaced.ToUW(),
            PackPlaced = lbFree,
            PackWindowHeight = fWindowRows(2),
            WindowWidth = fWindowWidth(),
            BagHeights = liBagHeights.ToArray(),
            Panel = (lOPanel != null ? lOPanel.OpenRect : Rect.zero).ToUW(),
            Minimap = (UWModernMinimap.Instance != null ? UWModernMinimap.Instance.ScreenRect : Rect.zero).ToUW(),
            ActionBar = (lOBar != null ? lOBar.ScreenRect : Rect.zero).ToUW(),
            ScreenMargin = ScreenMargin,
            WindowGap = WindowGap,
            LoadBarRows = LoadBarRows
        });

        lOPlaced = lOPlace.Pack.ToUnity();
        lfRight = lOPlaced.xMax;
        lfBottom = lOPlaced.y;
        UWModernLayout.Report(UWModernLayout.ElementEnum.Bags, lOPlaced);

        int liCarried = lOInventory.GetCarriedTenthStones(lOData.CommonObjectProperties);
        int liMax = lOData.InitialPlayer != null ? lOData.InitialPlayer.MaxWeight : 0;
        string lsLoad = UWInventoryWeight.Format(liCarried) + " / " + UWInventoryWeight.Format(liMax) + " st";

        fLayoutWindow(lOPack, 2, lfRight, lfBottom, -1, "Backpack", lsLoad);

        mOLoadBack.enabled = true;
        mOLoadFill.enabled = true;

        // The load bar over the backpack's inner width.
        float lfBarLeft = lOPack.ScreenRect.xMin + ((UWModernHudArt.LeatherLeft + 1) * miScale);
        float lfBarWidth = lOPack.ScreenRect.width - ((UWModernHudArt.LeatherLeft + UWModernHudArt.LeatherRight + 2) * miScale);
        float lfBarY = lOPack.ScreenRect.yMax + (2 * miScale);
        float lfLoad = liMax > 0 ? Mathf.Clamp01(liCarried / (float)liMax) : 0f;

        fSetRect(mOLoadBack.rectTransform, lfBarLeft, lfBarY, lfBarWidth, LoadBarRows * miScale);
        fSetRect(mOLoadFill.rectTransform, lfBarLeft, lfBarY, lfBarWidth * lfLoad, LoadBarRows * miScale);

        // The open bags where UWBagPlacement put them.
        int liWindow = 1;

        for (int liBag = 0; liBag < lOShownBags.Count; liBag++)
        {
            UWObject lOBag = lOShownBags[liBag];
            Window lOWindow = fGetWindow(liWindow++);

            mOScroll.TryGetValue(lOBag, out int liScrolled);
            lOWindow.Container = lOBag;
            lOWindow.FirstIndex = liScrolled * Columns;

            mOBagNumbers.TryGetValue(lOBag, out int liNumber);

            UWBagPlacement.Spot lOSpot = lOPlace.Bags[liBag];

            fLayoutWindow(lOWindow, liBagRows[liBag], lOSpot.Right, lOSpot.Bottom, liNumber, fTitle(lOBag), fBagWeight(lOBag, lOData));
            fAnimateOpening(lOWindow, lOBag);
        }

        for (int liAt = liWindow; liAt < mOWindows.Count; liAt++)
            mOWindows[liAt].Root.gameObject.SetActive(false);

        float lfLeftEdge = Screen.width;

        foreach (Window lOShown in mOWindows)
        {
            if (lOShown.Root.gameObject.activeSelf && lOShown.ScreenRect.width > 0f)
                lfLeftEdge = Mathf.Min(lfLeftEdge, lOShown.ScreenRect.xMin);
        }

        LeftEdge = lfLeftEdge;

        fUpdateHover();
        fUpdateCursorIcon(lOInventory);
        fUpdateTooltip(lOInventory, lOData);
        fLayoutSplit();
        fLayoutMenu();
    }

    private static int fWindowRows(int piRows)
    {
        return TitleRows + (piRows * UWModernHudArt.SlotSize) + ((piRows - 1) * SlotGap) + BottomPad;
    }

    private static int fWindowWidth()
    {
        return (2 * SidePad) + (Columns * UWModernHudArt.SlotSize) + ((Columns - 1) * SlotGap);
    }

    private static void fSetRect(RectTransform pORect, float pfX, float pfY, float pfWidth, float pfHeight)
    {
        pORect.anchoredPosition = new Vector2(pfX, pfY);
        pORect.sizeDelta = new Vector2(pfWidth, pfHeight);
    }

    /// <summary>Lays a window out with its bottom right corner at the given screen point and
    /// fills its slots.</summary>
    private void fLayoutWindow(Window pOWindow, int piRows, float pfRight, float pfBottom, int piNumber, string psTitle,
        string psWeight)
    {
        int liWidth = fWindowWidth();
        int liHeight = fWindowRows(piRows);
        float liScale = miScale;

        pOWindow.Root.gameObject.SetActive(true);

        float lfLeft = pfRight - (liWidth * liScale);

        fSetRect(pOWindow.Root, lfLeft, pfBottom, liWidth * liScale, liHeight * liScale);
        pOWindow.Root.localScale = Vector3.one;
        pOWindow.Group.alpha = 1f;
        pOWindow.ScreenRect = new Rect(lfLeft, pfBottom, liWidth * liScale, liHeight * liScale);

        if (pOWindow.BackTexture == null || pOWindow.BackSize.x != liWidth || pOWindow.BackSize.y != liHeight
            || pOWindow.BackVersion != miArtVersion)
        {
            if (pOWindow.BackTexture != null)
                Destroy(pOWindow.BackTexture);

            pOWindow.BackTexture = UWModernHudArt.BuildLeather(fData().Textures, liWidth, liHeight, mOUi.TextureFilterMode);
            pOWindow.BackSize = new Vector2Int(liWidth, liHeight);
            pOWindow.BackVersion = miArtVersion;
            pOWindow.Back.texture = pOWindow.BackTexture;
        }

        int liTitleSize = Mathf.Max(10, Mathf.RoundToInt(4.5f * liScale));

        // The number badge left of the title, which moves over for it.
        bool lbBadge = piNumber > 0;
        float lfTitleLeft = (lbBadge ? SidePad + TitleBadgeShift : SidePad) * liScale;

        fShowBadge(pOWindow.Badge, pOWindow.BadgeText, lbBadge ? piNumber : -1,
            (SidePad + 3f) * liScale, (liHeight - 6f) * liScale, TitleBadgeSize * liScale);

        // One original pixel more around it to hit.
        float lfHit = (TitleBadgeSize + 2f) * liScale;

        pOWindow.BadgeRect = lbBadge
            ? new Rect(lfLeft + ((SidePad + 3f) * liScale) - (lfHit * 0.5f), pfBottom + ((liHeight - 6f) * liScale) - (lfHit * 0.5f), lfHit, lfHit)
            : Rect.zero;

        pOWindow.Title.fontSize = liTitleSize;
        pOWindow.Title.text = psTitle;
        fSetRect(pOWindow.Title.rectTransform, lfTitleLeft, (liHeight - TitleRows) * liScale,
            (liWidth * liScale) - lfTitleLeft - (SidePad * liScale), (TitleRows - 2) * liScale);

        pOWindow.Weight.fontSize = Mathf.Max(9, Mathf.RoundToInt(3.6f * liScale));
        pOWindow.Weight.text = psWeight;
        fSetRect(pOWindow.Weight.rectTransform, SidePad * liScale, (liHeight - TitleRows) * liScale, (liWidth - (2 * SidePad)) * liScale, (TitleRows - 2) * liScale);

        int liSlots = piRows * Columns;

        while (pOWindow.Slots.Count < liSlots)
            pOWindow.Slots.Add(fCreateSlot(pOWindow.Root));

        pOWindow.SlotCount = liSlots;

        for (int liSlot = 0; liSlot < pOWindow.Slots.Count; liSlot++)
        {
            Slot lOSlot = pOWindow.Slots[liSlot];
            bool lbUsed = liSlot < liSlots;

            lOSlot.Circle.gameObject.SetActive(lbUsed);

            if (!lbUsed)
                continue;

            int liColumn = liSlot % Columns;
            int liRow = liSlot / Columns;
            float lfX = (SidePad + (liColumn * (UWModernHudArt.SlotSize + SlotGap))) * liScale;
            float lfY = (liHeight - TitleRows - ((liRow + 1) * UWModernHudArt.SlotSize) - (liRow * SlotGap)) * liScale;

            fSetRect(lOSlot.Circle.rectTransform, lfX, lfY, UWModernHudArt.SlotSize * liScale, UWModernHudArt.SlotSize * liScale);
            lOSlot.Circle.texture = mOSlotTexture;
            lOSlot.ScreenRect = new Rect(lfLeft + lfX, pfBottom + lfY, UWModernHudArt.SlotSize * liScale, UWModernHudArt.SlotSize * liScale);

            fShowItem(lOSlot, fGetItem(pOWindow, liSlot));
        }

        fShowScrollArrows(pOWindow, piRows, liWidth, liHeight);

        float lfLine = Mathf.Max(2f, miScale * 0.75f);

        fSetFrame(pOWindow.Frame, -lfLine, -lfLine, (liWidth * liScale) + (2f * lfLine), (liHeight * liScale) + (2f * lfLine), lfLine);
    }

    /// <summary>The scroll arrow in original pixels: five wide, three high.</summary>
    private const int ArrowWidth = 5;

    private const int ArrowHeight = 3;

    /// <summary>
    /// THE SCROLL ARROWS (per user, 2026-10-08: show when a bag can be scrolled): in the right margin
    /// beside the top row while rows lie hidden above, beside the bottom row while more follows
    /// below - the same reckoning as the layout's (a spare empty row at the end, fLayout).
    /// </summary>
    private void fShowScrollArrows(Window pOWindow, int piRows, int piWidth, int piHeight)
    {
        int liScrolled = pOWindow.FirstIndex / Columns;
        int liNeeded = pOWindow.Container != null && pOWindow.Container.Contents != null
            ? (pOWindow.Container.Contents.Count / Columns) + 1
            : 0;
        bool lbUp = pOWindow.Container != null && liScrolled > 0;
        bool lbDown = pOWindow.Container != null && liScrolled < liNeeded - piRows;

        if (mOArrowTexture == null)
            mOArrowTexture = fBuildArrow();

        float liScale = miScale;
        float lfX = (piWidth - SidePad + ((SidePad - ArrowWidth) * 0.5f)) * liScale;
        float lfTopY = (piHeight - TitleRows - ArrowHeight) * liScale;
        float lfBottomY = BottomPad * liScale;

        pOWindow.ScrollUp.texture = mOArrowTexture;
        pOWindow.ScrollUp.enabled = lbUp;
        fSetRect(pOWindow.ScrollUp.rectTransform, lfX, lfTopY, ArrowWidth * liScale, ArrowHeight * liScale);

        pOWindow.ScrollDown.texture = mOArrowTexture;
        pOWindow.ScrollDown.enabled = lbDown;
        fSetRect(pOWindow.ScrollDown.rectTransform, lfX, lfBottomY, ArrowWidth * liScale, ArrowHeight * liScale);
    }

    /// <summary>A gold triangle pointing up, five by three pixels, drawn sharp.</summary>
    private static Texture2D fBuildArrow()
    {
        Texture2D lOTexture = new Texture2D(ArrowWidth, ArrowHeight, TextureFormat.RGBA32, false);
        lOTexture.name = "UWModernBags scroll arrow";
        lOTexture.filterMode = FilterMode.Point;
        lOTexture.wrapMode = TextureWrapMode.Clamp;

        Color32[] lyPixels = new Color32[ArrowWidth * ArrowHeight];
        Color32 lOGold = msGold;

        // Row 0 is the bottom: five wide, then three, then the tip.
        for (int y = 0; y < ArrowHeight; y++)
        {
            for (int x = 0; x < ArrowWidth; x++)
                lyPixels[(y * ArrowWidth) + x] = Mathf.Abs(x - (ArrowWidth / 2)) <= (ArrowHeight - 1 - y) ? lOGold : new Color32(0, 0, 0, 0);
        }

        lOTexture.SetPixels32(lyPixels);
        lOTexture.Apply();

        return lOTexture;
    }

    /// <summary>Badge sizes and places, in original pixels.</summary>
    private const float TitleBadgeSize = 6f;

    private const float TitleBadgeShift = 8f;

    private const float SlotBadgeSize = 6.5f;

    /// <summary>A number badge centred on a point, or hidden for a number below 1.</summary>
    private void fShowBadge(RawImage pOBadge, Text pOText, int piNumber, float pfCentreX, float pfCentreY, float pfSize)
    {
        bool lbShow = piNumber > 0;

        pOBadge.enabled = lbShow;
        pOText.enabled = lbShow;

        if (!lbShow)
            return;

        pOBadge.texture = mODiscTexture;
        fSetRect(pOBadge.rectTransform, pfCentreX - (pfSize * 0.5f), pfCentreY - (pfSize * 0.5f), pfSize, pfSize);

        pOText.text = piNumber.ToString();
        pOText.fontSize = Mathf.Max(9, Mathf.RoundToInt(pfSize * 0.66f));
        fSetRect(pOText.rectTransform, pfCentreX - (pfSize * 0.5f), pfCentreY - (pfSize * 0.5f), pfSize, pfSize);
    }

    /// <summary>The four lines of a frame around a rectangle.</summary>
    private static void fSetFrame(Image[] pOFrame, float pfX, float pfY, float pfWidth, float pfHeight, float pfLine)
    {
        fSetRect(pOFrame[0].rectTransform, pfX, pfY, pfWidth, pfLine);
        fSetRect(pOFrame[1].rectTransform, pfX, pfY + pfHeight - pfLine, pfWidth, pfLine);
        fSetRect(pOFrame[2].rectTransform, pfX, pfY, pfLine, pfHeight);
        fSetRect(pOFrame[3].rectTransform, pfX + pfWidth - pfLine, pfY, pfLine, pfHeight);
    }

    /// <summary>
    /// The window grows out of its container's slot when it opens: from the slot's rectangle to
    /// its own over OpenSeconds, easing out, fading in from a third. Without a visible slot it is
    /// simply there.
    /// </summary>
    private void fAnimateOpening(Window pOWindow, UWObject pOBag)
    {
        if (!mOOpenedAt.TryGetValue(pOBag, out float lfOpened) || !mOSourceRects.TryGetValue(pOBag, out Rect lOFrom))
            return;

        float lfTime = (Time.unscaledTime - lfOpened) / OpenSeconds;

        if (lfTime >= 1f)
        {
            mOOpenedAt.Remove(pOBag);
            return;
        }

        float lfEase = 1f - Mathf.Pow(1f - Mathf.Clamp01(lfTime), 3f);
        Rect lOTo = pOWindow.ScreenRect;

        float lfX = Mathf.Lerp(lOFrom.xMin, lOTo.xMin, lfEase);
        float lfY = Mathf.Lerp(lOFrom.yMin, lOTo.yMin, lfEase);
        float lfWidth = Mathf.Lerp(lOFrom.width, lOTo.width, lfEase);
        float lfHeight = Mathf.Lerp(lOFrom.height, lOTo.height, lfEase);

        pOWindow.Root.anchoredPosition = new Vector2(lfX, lfY);
        pOWindow.Root.localScale = new Vector3(lfWidth / lOTo.width, lfHeight / lOTo.height, 1f);
        pOWindow.Group.alpha = Mathf.Min(1f, 0.35f + lfTime);
    }

    /// <summary>
    /// The pointer on a bag window or on the slot of an open bag: the window gets its frame, the
    /// slot its ring, and a line joins them (3 of the look).
    /// </summary>
    private void fUpdateHover()
    {
        UWObject lOHovered = null;

        if (mbOpen && UWModernPointer.IsFree && Mouse.current != null)
        {
            Vector2 lOPointer = Mouse.current.position.ReadValue();

            if (fFindSlot(lOPointer, out Window lOSlotWindow, out int liSlot))
            {
                UWObject lOItem = fGetItem(lOSlotWindow, liSlot);

                if (lOItem != null && mOOpenBags.Contains(lOItem))
                    lOHovered = lOItem;
            }

            if (lOHovered == null && fFindWindow(lOPointer, out Window lOWindow) && lOWindow.Container != null)
                lOHovered = lOWindow.Container;
        }

        Window lOHoveredWindow = null;
        Window lOCloseWindow = null;

        if (mbOpen && UWModernPointer.IsFree && Mouse.current != null)
            fFindCloseBadge(Mouse.current.position.ReadValue(), out lOCloseWindow);

        if (lOCloseWindow != null)
        {
            lOCloseWindow.Badge.texture = mODiscHoverTexture;
            lOCloseWindow.BadgeText.text = CloseMark;
        }

        foreach (Window lOWindow in mOWindows)
        {
            bool lbMine = lOHovered != null && lOWindow.Container == lOHovered && lOWindow.Root.gameObject.activeSelf;

            foreach (Image lOLine in lOWindow.Frame)
                lOLine.enabled = lbMine;

            if (lbMine)
                lOHoveredWindow = lOWindow;

            for (int liSlot = 0; liSlot < lOWindow.Slots.Count; liSlot++)
            {
                lOWindow.Slots[liSlot].Ring.enabled = lOHovered != null && liSlot < lOWindow.SlotCount
                    && fGetItem(lOWindow, liSlot) == lOHovered;
            }
        }

        if (lOHoveredWindow == null || !mOSourceRects.TryGetValue(lOHovered, out Rect lOSource))
        {
            mOHoverLine.enabled = false;
            return;
        }

        // From the ring's edge to the nearest point of the window's frame.
        Rect lOTarget = lOHoveredWindow.ScreenRect;
        Vector2 lOCentre = lOSource.center;
        Vector2 lONearest = new Vector2(Mathf.Clamp(lOCentre.x, lOTarget.xMin, lOTarget.xMax),
            Mathf.Clamp(lOCentre.y, lOTarget.yMin, lOTarget.yMax));
        Vector2 lODirection = lONearest - lOCentre;
        float lfRadius = (lOSource.width * 0.5f) + miScale;
        float lfLength = lODirection.magnitude - lfRadius;

        if (lfLength <= 1f)
        {
            mOHoverLine.enabled = false;
            return;
        }

        lODirection.Normalize();

        RectTransform lORect = mOHoverLine.rectTransform;
        lORect.anchoredPosition = lOCentre + (lODirection * lfRadius);
        lORect.sizeDelta = new Vector2(lfLength, Mathf.Max(2f, miScale * 0.75f));
        lORect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(lODirection.y, lODirection.x) * Mathf.Rad2Deg);
        mOHoverLine.enabled = true;
    }

    private void fShowItem(Slot pOSlot, UWObject pOItem)
    {
        UWTexture lOSource = null;

        if (pOItem != null)
        {
            lOSource = pOItem.Icon ?? pOItem.Texture;

            // An open bag shows its open picture, as the original's open container does.
            if (mbOpen && mOOpenBags.Contains(pOItem) && pOItem.ID <= LastContainerWithOpenVariant)
                lOSource = fData().Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, pOItem.ID | 0x1) ?? lOSource;
        }

        float liScale = miScale;
        int liNumber = -1;

        if (mbOpen && pOItem != null && mOBagNumbers.TryGetValue(pOItem, out int liOpenNumber))
        {
            liNumber = liOpenNumber;
            mOSourceRects[pOItem] = pOSlot.ScreenRect;
        }

        fShowBadge(pOSlot.Badge, pOSlot.BadgeText, liNumber, 2.5f * liScale,
            (UWModernHudArt.SlotSize - 2.5f) * liScale, SlotBadgeSize * liScale);

        float lfRing = (UWModernHudArt.SlotSize + 2) * liScale;

        pOSlot.Ring.texture = mORingTexture;
        fSetRect(pOSlot.Ring.rectTransform, -liScale, -liScale, lfRing, lfRing);

        if (lOSource == null)
        {
            pOSlot.Icon.enabled = false;
            pOSlot.Count.enabled = false;
            pOSlot.ShownTexture = null;
            return;
        }

        Texture2D lOIcon = fGetIcon(lOSource);

        pOSlot.Icon.texture = lOIcon;
        pOSlot.Icon.enabled = true;
        pOSlot.ShownTexture = lOSource;

        fSetRect(pOSlot.Icon.rectTransform, ((UWModernHudArt.SlotSize - lOIcon.width) / 2) * liScale,
            ((UWModernHudArt.SlotSize - lOIcon.height) / 2) * liScale, lOIcon.width * liScale, lOIcon.height * liScale);

        int liCount = UWItemDescriptions.GetStackCount(pOItem);

        pOSlot.Count.enabled = liCount > 1;

        if (liCount > 1)
        {
            pOSlot.Count.text = liCount.ToString();
            pOSlot.Count.fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * liScale));
            fSetRect(pOSlot.Count.rectTransform, 0f, -liScale, (UWModernHudArt.SlotSize + 1) * liScale, UWModernHudArt.SlotSize * liScale * 0.6f);
        }
    }

    private Texture2D fGetIcon(UWTexture pOSource)
    {
        if (!mOIcons.TryGetValue(pOSource, out Texture2D lOIcon) || lOIcon == null)
        {
            lOIcon = UWIconTextureBuilder.Build(pOSource, mOUi.TextureFilterMode);
            mOIcons[pOSource] = lOIcon;
        }

        return lOIcon;
    }

    private string fTitle(UWObject pOItem)
    {
        string lsName = UWItemDescriptions.GetBareName(pOItem.ID, fData());

        return string.IsNullOrEmpty(lsName) ? string.Empty : char.ToUpperInvariant(lsName[0]) + lsName.Substring(1);
    }

    /// <summary>What a bag holds against what it takes; a bag without a limit (quiver, rune
    /// bag) shows only its contents.</summary>
    private static string fBagWeight(UWObject pOBag, DataImport pOData)
    {
        int liHeld = UWInventoryWeight.GetTotalTenthStones(pOBag.Contents, pOData.CommonObjectProperties);

        if (pOData.ObjectClassProperties != null
            && pOData.ObjectClassProperties.TryGetContainer(pOBag.ID, out UWObjectClassProperties.Container lOEntry)
            && lOEntry.CapacityTenthStones > 0)
            return UWInventoryWeight.Format(liHeld) + " / " + UWInventoryWeight.Format(lOEntry.CapacityTenthStones) + " st";

        return UWInventoryWeight.Format(liHeld) + " st";
    }

    private void fUpdateCursorIcon(UWInventory pOInventory)
    {
        UWObject lOItem = fIsActive() ? pOInventory.CursorItem ?? mOUseItem : null;
        UWTexture lOSource = lOItem != null ? lOItem.Icon ?? lOItem.Texture : null;

        if (lOSource == null || Mouse.current == null)
        {
            mOCursorIcon.enabled = false;
            mOCursorCount.enabled = false;
            fShowSystemPointer(true);
            return;
        }

        Texture2D lOIcon = fGetIcon(lOSource);
        Vector2 lOPointer = Mouse.current.position.ReadValue();
        float liScale = miScale;

        mOCursorIcon.texture = lOIcon;
        mOCursorIcon.enabled = true;

        // THE THING IS THE POINTER (2026-10-06, per user: with a fast mouse the thing trailed the
        // free pointer). The system pointer is drawn by the OS at scan-out with the newest
        // position, our icon a frame later from the position the frame sampled - two pointers
        // can never agree under a fast hand. So while something hangs on the free pointer the
        // system pointer is hidden and the thing alone shows where one points, as in the
        // original, where the picked-up thing replaces the cursor. Restored once the hand is empty.
        fShowSystemPointer(false);
        fSetRect(mOCursorIcon.rectTransform, lOPointer.x - (lOIcon.width * liScale * 0.5f), lOPointer.y - (lOIcon.height * liScale * 0.5f),
            lOIcon.width * liScale, lOIcon.height * liScale);

        int liCount = UWItemDescriptions.GetStackCount(lOItem);

        mOCursorCount.enabled = liCount > 1;

        if (liCount > 1)
        {
            mOCursorCount.text = liCount.ToString();
            mOCursorCount.fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * liScale));
            fSetRect(mOCursorCount.rectTransform, lOPointer.x - (lOIcon.width * liScale * 0.5f), lOPointer.y - (lOIcon.height * liScale * 0.5f) - (2 * liScale),
                (lOIcon.width + 2) * liScale, 6 * liScale);
        }
    }

    /// <summary>Whether this class hid the system pointer for the thing on it.</summary>
    private bool mbHidSystemPointer;

    /// <summary>Hides the system pointer behind the thing on the free pointer, and shows it
    /// again when the hand is empty - only if this class hid it, and only while the pointer is
    /// free (locked, UWControlScheme keeps it hidden anyway).</summary>
    private void fShowSystemPointer(bool pbShow)
    {
        if (!pbShow)
        {
            if (UWModernPointer.IsFree && Cursor.visible)
            {
                Cursor.visible = false;
                mbHidSystemPointer = true;
            }

            return;
        }

        if (mbHidSystemPointer)
        {
            mbHidSystemPointer = false;

            if (UWModernPointer.IsFree)
                Cursor.visible = true;
        }
    }

    /// <summary>The name and the weight of the thing under the pointer.</summary>
    private void fUpdateTooltip(UWInventory pOInventory, DataImport pOData)
    {
        UWObject lOItem = null;
        Vector2 lOPointer = Vector2.zero;

        if (fIsActive() && UWModernPointer.IsFree && pOInventory.CursorItem == null && Mouse.current != null)
        {
            lOPointer = Mouse.current.position.ReadValue();

            if (fPanelSlotAt(lOPointer, out UWArmorItemMap.BodySlot leEquip))
                lOItem = pOInventory.GetEquipped(leEquip);
            else if (fFindSlot(lOPointer, out Window lOWindow, out int liSlot))
                lOItem = fGetItem(lOWindow, liSlot);
        }

        if (lOItem == null)
        {
            mOTooltip.enabled = false;
            mOTooltipBack.enabled = false;
            return;
        }

        int liCount = UWItemDescriptions.GetStackCount(lOItem);
        string lsRaw = pOData.GetObjectDescription(lOItem.ID + 1);
        string lsName = string.IsNullOrEmpty(lsRaw) ? string.Empty
            : UWObjectDescriptionFormatter.FormatBareItemName(lsRaw, liCount > 1);

        if (lsName.Length > 0)
            lsName = char.ToUpperInvariant(lsName[0]) + lsName.Substring(1);

        string lsEffect = fKnownEffect(lOItem, pOData, ref lsName);

        if (liCount > 1)
            lsName = liCount + " " + lsName;

        string lsWeight = UWInventoryWeight.Format(UWInventoryWeight.GetItemTenthStones(lOItem, pOData.CommonObjectProperties)) + " st";

        int liFontSize = UWModernHud.FontSize;
        int liLines = string.IsNullOrEmpty(lsEffect) ? 2 : 3;

        mOTooltip.fontSize = liFontSize;
        mOTooltip.text = lsName + (string.IsNullOrEmpty(lsEffect) ? string.Empty : "\n" + lsEffect) + "\n" + lsWeight;

        float lfWidth = Mathf.Max(liFontSize * 6f, mOTooltip.preferredWidth + liFontSize);
        float lfHeight = liFontSize * (liLines + 1f);
        float lfX = Mathf.Clamp(lOPointer.x - lfWidth, 0f, Screen.width - lfWidth);
        float lfY = Mathf.Clamp(lOPointer.y + (4 * miScale), 0f, Screen.height - lfHeight);

        fSetRect(mOTooltipBack.rectTransform, lfX, lfY, lfWidth, lfHeight);
        fSetRect(mOTooltip.rectTransform, lfX + (liFontSize * 0.5f), lfY, lfWidth - liFontSize, lfHeight);

        mOTooltip.enabled = true;
        mOTooltipBack.enabled = true;
    }

    /// <summary>
    /// What is KNOWN of a thing (per user, 2026-10-04: an identified thing shows its full name, its
    /// magic on a line of its own): only with the full Lore result already in the thing - from an
    /// earlier look or Name Enchantment (UWLoreCheck: bits 0-1 of its heading, level 3); the
    /// tooltip rolls nothing. A talisman takes its own name, a cursed thing the word before its
    /// name; the enchantment ("of Very Great Damage") and a wand's charges are the second line,
    /// as the look words them (UWItemDescriptions.DescribeItem). Empty when nothing more is known.
    /// </summary>
    private string fKnownEffect(UWObject pOItem, DataImport pOData, ref string psName)
    {
        if (!UWLoreCheck.CanBeIdentified(pOItem, pOData.CommonObjectProperties)
            || (pOItem.Heading & 0x3) != UWLoreCheck.ResultNamed)
            return string.Empty;

        string lsTalisman = UWTalismans.GetName(pOItem.ID, pOData.Strings);

        if (!string.IsNullOrEmpty(lsTalisman))
        {
            psName = char.ToUpperInvariant(lsTalisman[0]) + lsTalisman.Substring(1);
            return string.Empty;
        }

        UWLevelLoader lOLoader = UWScene.LevelLoader;
        List<UWObject> lOObjects = pOData.IsSavegame && pOData.InitialPlayer != null ? pOData.InitialPlayer.InventoryRecords
            : lOLoader != null && lOLoader.CurrentLevel != null ? lOLoader.CurrentLevel.Masterlist : null;
        UWEnchantment.Result lOEnchantment = UWEnchantment.Get(pOItem, lOObjects, pOData.Strings);
        string lsEffect = string.Empty;

        if (lOEnchantment.IsCursed && psName.Length > 0)
            psName = "Cursed " + char.ToLowerInvariant(psName[0]) + psName.Substring(1);
        else if (!string.IsNullOrEmpty(lOEnchantment.Name))
            lsEffect = "of " + lOEnchantment.Name;

        if (lOEnchantment.Charges >= 0)
        {
            string lsCharges = lOEnchantment.Charges == 0 ? "with no full charges"
                : lOEnchantment.Charges == 1 ? "with 1 full charge"
                : "with " + lOEnchantment.Charges + " full charges";

            lsEffect = lsEffect.Length > 0 ? lsEffect + " " + lsCharges : lsCharges;
        }

        return lsEffect;
    }

    private void fEnsureArt()
    {
        if (miArtVersion == UWColourVision.Version && mOSlotTexture != null)
            return;

        miArtVersion = UWColourVision.Version;

        if (mOSlotTexture != null)
            Destroy(mOSlotTexture);

        mOSlotTexture = UWModernHudArt.BuildSlotCircle(fData().Textures, mOUi.TextureFilterMode);

        if (mODiscTexture == null)
            mODiscTexture = fBuildCircle(64, 29f, 3.5f, msBadgeFill, msGold);

        if (mODiscHoverTexture == null)
            mODiscHoverTexture = fBuildCircle(64, 29f, 3.5f, msBadgeHoverFill, msGold);

        if (mORingTexture == null)
            mORingTexture = fBuildCircle(128, 60f, 6f, Color.clear, msGold);
    }

    /// <summary>A smooth disc with a rim, for the number badges and the hover ring (the fill
    /// clear for the ring).</summary>
    private static Texture2D fBuildCircle(int piSize, float pfRadius, float pfRim, Color pOFill, Color pORim)
    {
        Color32[] lyPixels = new Color32[piSize * piSize];
        float lfCentre = (piSize - 1) * 0.5f;

        for (int y = 0; y < piSize; y++)
        {
            for (int x = 0; x < piSize; x++)
            {
                float lfDistance = Mathf.Sqrt(((x - lfCentre) * (x - lfCentre)) + ((y - lfCentre) * (y - lfCentre)));
                float lfOutside = Mathf.Clamp01(lfDistance - pfRadius + 0.5f);
                float lfInRim = Mathf.Clamp01(lfDistance - (pfRadius - pfRim) + 0.5f);
                Color lOColour = Color.Lerp(pOFill, pORim, lfInRim);

                lOColour.a *= 1f - lfOutside;
                lyPixels[(y * piSize) + x] = lOColour;
            }
        }

        Texture2D lOTexture = new Texture2D(piSize, piSize, TextureFormat.RGBA32, false);
        lOTexture.name = "UWModernBags circle";
        lOTexture.filterMode = FilterMode.Bilinear;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(lyPixels);
        lOTexture.Apply();

        return lOTexture;
    }

    // ------------------------------------------------- Building

    private void fBuild()
    {
        GameObject lORoot = new GameObject("Modern bags", typeof(Canvas), typeof(CanvasScaler));
        lORoot.transform.SetParent(transform, false);

        mOCanvas = lORoot.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the character panel (41), whose backpack it draws.
        mOCanvas.sortingOrder = 42;

        CanvasScaler lOScaler = lORoot.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        lOScaler.scaleFactor = 1f;

        mORoot = (RectTransform)lORoot.transform;

        mOLoadBack = fCreateImage(mORoot, "Load", new Color(0.24f, 0.20f, 0.16f, 0.9f));
        mOLoadFill = fCreateImage(mORoot, "Load fill", new Color(0.80f, 0.64f, 0.34f, 1f));

        mOHoverLine = fCreateImage(mORoot, "Hover line", msGold);
        mOHoverLine.rectTransform.pivot = new Vector2(0f, 0.5f);
        mOHoverLine.enabled = false;

        mOCursorIcon = fCreateRawImage(mORoot, "Pointer item");
        UWIconPalette.ApplySmooth(mOCursorIcon);
        mOCursorCount = fCreateText(mORoot, "Pointer count", TextAnchor.LowerRight);

        mOTooltipBack = fCreateImage(mORoot, "Tooltip", new Color(0.05f, 0.05f, 0.06f, 0.88f));
        mOTooltip = fCreateText(mORoot, "Tooltip text", TextAnchor.MiddleLeft);
    }

    private Window fGetWindow(int piIndex)
    {
        while (mOWindows.Count <= piIndex)
        {
            Window lOWindow = new Window();
            GameObject lOObject = new GameObject("Bag " + mOWindows.Count, typeof(RectTransform));

            lOObject.transform.SetParent(mORoot, false);

            lOWindow.Root = (RectTransform)lOObject.transform;
            lOWindow.Root.anchorMin = Vector2.zero;
            lOWindow.Root.anchorMax = Vector2.zero;
            lOWindow.Root.pivot = Vector2.zero;

            lOWindow.Back = fCreateRawImage(lOWindow.Root, "Leather");
            lOWindow.Back.enabled = true;
            UWPixelArtUI.Apply(lOWindow.Back);
            lOWindow.Back.rectTransform.anchorMin = Vector2.zero;
            lOWindow.Back.rectTransform.anchorMax = Vector2.one;
            lOWindow.Back.rectTransform.offsetMin = Vector2.zero;
            lOWindow.Back.rectTransform.offsetMax = Vector2.zero;

            lOWindow.Title = fCreateText(lOWindow.Root, "Title", TextAnchor.MiddleLeft);
            lOWindow.Title.color = new Color(0.94f, 0.87f, 0.71f);
            lOWindow.Weight = fCreateText(lOWindow.Root, "Weight", TextAnchor.MiddleRight);
            lOWindow.Weight.color = new Color(0.90f, 0.84f, 0.69f);

            lOWindow.ScrollUp = fCreateRawImage(lOWindow.Root, "Scroll up");
            lOWindow.ScrollDown = fCreateRawImage(lOWindow.Root, "Scroll down");
            lOWindow.ScrollDown.uvRect = new Rect(0f, 1f, 1f, -1f);

            lOWindow.Badge = fCreateRawImage(lOWindow.Root, "Badge");
            lOWindow.BadgeText = fCreateText(lOWindow.Root, "Badge number", TextAnchor.MiddleCenter);
            lOWindow.BadgeText.color = new Color(1f, 0.92f, 0.69f);

            lOWindow.Frame = new Image[4];

            for (int liLine = 0; liLine < lOWindow.Frame.Length; liLine++)
            {
                lOWindow.Frame[liLine] = fCreateImage(lOWindow.Root, "Frame", msGold);
                lOWindow.Frame[liLine].enabled = false;
            }

            lOWindow.Group = lOObject.AddComponent<CanvasGroup>();
            lOWindow.Group.interactable = false;
            lOWindow.Group.blocksRaycasts = false;

            mOWindows.Add(lOWindow);

            // The hover line, the pointer item and the tooltip stay on top of every window.
            mOHoverLine.transform.SetAsLastSibling();
            mOCursorIcon.transform.SetAsLastSibling();
            mOCursorCount.transform.SetAsLastSibling();
            mOTooltipBack.transform.SetAsLastSibling();
            mOTooltip.transform.SetAsLastSibling();
        }

        return mOWindows[piIndex];
    }

    private Slot fCreateSlot(RectTransform pOParent)
    {
        Slot lOSlot = new Slot();

        lOSlot.Circle = fCreateRawImage(pOParent, "Slot");
        lOSlot.Circle.enabled = true;
        UWPixelArtUI.Apply(lOSlot.Circle);

        lOSlot.Icon = fCreateRawImage(lOSlot.Circle.rectTransform, "Icon");
        UWIconPalette.ApplySmooth(lOSlot.Icon);

        lOSlot.Count = fCreateText(lOSlot.Circle.rectTransform, "Count", TextAnchor.LowerRight);
        lOSlot.Count.color = Color.white;

        lOSlot.Ring = fCreateRawImage(lOSlot.Circle.rectTransform, "Ring");
        lOSlot.Badge = fCreateRawImage(lOSlot.Circle.rectTransform, "Badge");
        lOSlot.BadgeText = fCreateText(lOSlot.Circle.rectTransform, "Badge number", TextAnchor.MiddleCenter);
        lOSlot.BadgeText.color = new Color(1f, 0.92f, 0.69f);

        return lOSlot;
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

    private static Image fCreateImage(Transform pOParent, string psName, Color pOColour)
    {
        GameObject lOObject = new GameObject(psName, typeof(RectTransform), typeof(Image));
        lOObject.transform.SetParent(pOParent, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = Vector2.zero;
        lORect.anchorMax = Vector2.zero;
        lORect.pivot = Vector2.zero;

        Image lOImage = lOObject.GetComponent<Image>();
        lOImage.color = pOColour;
        lOImage.raycastTarget = false;

        return lOImage;
    }

    private Text fCreateText(Transform pOParent, string psName, TextAnchor peAlignment)
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
        lOText.color = new Color(0.86f, 0.86f, 0.84f);
        lOText.raycastTarget = false;
        lOText.horizontalOverflow = HorizontalWrapMode.Overflow;
        lOText.verticalOverflow = VerticalWrapMode.Overflow;

        Outline lOOutline = lOObject.GetComponent<Outline>();
        lOOutline.effectColor = new Color(0.16f, 0.09f, 0.04f, 0.9f);
        lOOutline.effectDistance = new Vector2(1.5f, -1.5f);

        return lOText;
    }
}
