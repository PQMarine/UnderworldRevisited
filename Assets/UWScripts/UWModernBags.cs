using UnityEngine;
using UnityEngine.InputSystem;
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
///     number turns into an x, a click closes the window and the bags inside it - the left
///     button, and since 2026-10-20 the right one too (per user: a container opens on the right
///     button, so it should close on it as well; the right button on its slot toggled already);
///   - the LEFT button takes a thing and puts it down - held and dragged, or click and click;
///     onto a slot, a container (it goes inside), a bag window, or into the world (low on the
///     screen dropped in front, higher thrown - the original's rule, UWPlayerThrow);
///   - the RIGHT button opens a thing's CONTEXT MENU (per user, 2026-10-04 - before it used at
///     once, and Shift with it looked): use, open, equip or take off, look, pick up, split,
///     cancel (UWModernBagMenu); a container opens or closes at once instead, Shift
///     with it gives its menu; food and drink put down on the paperdoll's head
///     are eaten, as on the classic page;
///   - hovering shows the name and the weight;
///   - THE USE MODE (per user, 2026-10-04: the lantern could not be filled): the right button on
///     a thing that is applied to something else (oil flask, key, lockpick, pole ... -
///     UWItemTargetRules.Classify) asks "Use ... on what?" and hangs its picture on the pointer;
///     the next left click applies it - onto a carried thing in a slot, or onto the world under
///     the pointer (UWItemTargetUse.Apply). The right button, Escape, locking the pointer or
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
/// stack split it (UWModernBagSplit). IN A CONVERSATION the bags stay for the trade, its
/// areas are slots of theirs (UWModernBagTrade, UWModernConversation).
///
/// SPLIT IN CLEANUP 5.17 (2026-10-23; 2,686 lines and three partial files of 1,147 until then):
/// this component keeps the bags' state (open or shut), the routing of the keys and clicks in a
/// fixed order (Update) and the layout in a fixed order (LateUpdate), and the members the rest of
/// the game asks. The pieces are classes of their own, each as it was: which bags are open
/// (UWModernOpenBags), the windows and the original page's stand-in (UWModernBagPages), the canvas
/// and the placement (UWModernBagView), the thing on the pointer and the use mode
/// (UWModernBagDrag), the clicks (UWModernBagClicks), the context menu (UWModernBagMenu), the
/// split box (UWModernBagSplit) and the trade (UWModernBagTrade). Escape and the pad's B close
/// the open bags through UWModernUi's closable chain, of which this component is a link.
/// </summary>
public class UWModernBags : MonoBehaviour, IUWModernClosable
{
    /// <summary>The one bag view of the scene, for UWGameUI's Escape.</summary>
    public static UWModernBags Instance { get; private set; }

    /// <summary>Whether the bag windows are open.</summary>
    public bool IsOpen => mbOpen;

    private UWGameUI mOUi;

    private UWInventory mOInventory;

    private Interaction mOInteraction;

    private UWControlScheme mOScheme;

    private bool mbOpen;

    // ------------------------------------------------- The pieces (made in Awake)

    /// <summary>Which bags are open, their numbers, scrolling and persistence.</summary>
    internal UWModernOpenBags OpenBags { get; private set; }

    /// <summary>The windows, the original page's stand-in and the hit tests.</summary>
    internal UWModernBagPages Pages { get; private set; }

    /// <summary>The canvas, where the windows go, the hover and the tooltip.</summary>
    internal UWModernBagView View { get; private set; }

    /// <summary>The thing on the pointer, where it came from and the use mode.</summary>
    internal UWModernBagDrag Drag { get; private set; }

    /// <summary>The presses: take and put, click and click, the right button.</summary>
    internal UWModernBagClicks Clicks { get; private set; }

    /// <summary>The context menu.</summary>
    internal UWModernBagMenu Menu { get; private set; }

    /// <summary>The split box.</summary>
    internal UWModernBagSplit Split { get; private set; }

    /// <summary>The trade slots of a conversation.</summary>
    internal UWModernBagTrade Trade { get; private set; }

    /// <summary>The scene's UI, interaction and control scheme, found in Start - for the pieces.</summary>
    internal UWGameUI Ui => mOUi;

    internal Interaction Interaction => mOInteraction;

    internal UWInventory Inventory => fInventory();

    internal DataImport Data => fData();

    private void Awake()
    {
        Instance = this;

        OpenBags = new UWModernOpenBags(this);
        Pages = new UWModernBagPages(this);
        View = new UWModernBagView(this);
        Drag = new UWModernBagDrag(this);
        Clicks = new UWModernBagClicks(this);
        Menu = new UWModernBagMenu(this);
        Split = new UWModernBagSplit(this);
        Trade = new UWModernBagTrade(this);
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
    }

    private void fDestroyTextures()
    {
        View.Release();
        Pages.Release();
        Split.Release();
        Menu.Release();
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

    /// <summary>Shown where the modern HUD is shown (not in a conversation, a screen menu or a
    /// cutscene).</summary>
    private bool fIsShown()
    {
        return UWModernUi.IsActive && UWModernUi.IsShowingOrTalking
            && fData() != null && fInventory() != null;
    }

    /// <summary>The left edge of the bag windows shown, in screen pixels (the screen's width
    /// when none) - the modern conversation keeps left of them.</summary>
    public float LeftEdge { get; internal set; } = float.MaxValue;

    // ------------------------------------------------- Opening and closing

    /// <summary>Opens the bag windows (the modern conversation, for the trade; B, a click on the
    /// shut backpack, a bag opened from the panel, the anvil's use mode).</summary>
    public void Open()
    {
        // With the original's character page the bag windows stay shut (per user, 2026-10-10).
        if (UWModernBagPages.IsPageShown)
            return;

        mbOpen = true;
    }

    /// <summary>B: the bag windows open or shut; the pointer stays as it is.</summary>
    private void fToggle()
    {
        if (mbOpen)
            Close();
        else
            Open();
    }

    /// <summary>How long the gamepad's Y is held to open every bag - a little longer than the
    /// use's hold (Interaction.ModernUseHoldSeconds, 0.4 s; per user, 2026-10-07).</summary>
    private const float PadOpenAllSeconds = 0.5f;

    /// <summary>Seconds the gamepad's Y is held, -1 when it is not.</summary>
    private float mfPadPanelHeld = -1f;

    /// <summary>
    /// THE GAMEPAD'S PANEL BUTTON, d-pad down since 2026-10-07 (Y before; UWGamepad): TAPPED, on the release, the character panel - the bags alone
    /// while the panel is pinned, where it had nothing to do (per user's test, 2026-10-07); HELD,
    /// every carried bag opens (UWModernOpenBags.OpenAll, per user the same day).
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
                OpenBags.OpenAll();
            }

            return;
        }

        mfPadPanelHeld = -1f;

        if (UWModernPanel.Pinned)
            fToggle();
        else
            fTogglePanel();
    }

    /// <summary>C: the character panel slides out or in (on its Character tab); the pointer stays
    /// as it is, and a pinned panel has nothing to do (per user, 2026-10-04).</summary>
    private void fTogglePanel()
    {
        UWModernPanel lOPanel = UWModernPanel.Instance;

        // The original's character page is always out (UWModernInventoryPage).
        if (lOPanel == null || UWModernPanel.Pinned || UWModernBagPages.IsPageShown)
            return;

        if (lOPanel.IsOpen && lOPanel.Tab == UWModernPanel.TabEnum.Character)
            lOPanel.Close();
        else
            lOPanel.Open(UWModernPanel.TabEnum.Character);
    }

    /// <summary>Shuts the bag windows. What hangs on the pointer goes back where it came from, else
    /// wherever there is room (DropCursorItemAnywhere); if there is none it stays, and so do the
    /// bags (false).</summary>
    public bool Close()
    {
        if (!mbOpen)
            return true;

        if (!Drag.ReturnCursorItem())
        {
            if (mOInteraction != null)
                mOInteraction.AddGeneralMessage(UWStringIds.NoPlaceToPutMessage);

            return false;
        }

        fGiveUpPointer();

        return true;
    }

    private void fGiveUpPointer()
    {
        mbOpen = false;
        Clicks.EndPress();
    }

    /// <summary>The open bags are a link of UWModernUi's closable chain: Escape and the pad's B
    /// close them (Close) while they are open.</summary>
    bool IUWModernClosable.IsClosable => IsOpen;

    void IUWModernClosable.Close()
    {
        Close();
    }

    /// <summary>For the character panel closing while the bags are shut: what hangs on the
    /// pointer goes back where it came from, else wherever there is room; false if it stays.</summary>
    public bool ReturnCursorItem()
    {
        return Drag.ReturnCursorItem();
    }

    /// <summary>The bags take clicks while their pointer is out, the character panel is open or
    /// the original's character page shows (UWModernInventoryPage).</summary>
    internal bool IsActive => mbOpen || (UWModernPanel.Instance != null && UWModernPanel.Instance.IsOpen) || UWModernBagPages.IsPageShown;

    // ------------------------------------------------- Input

    private void Update()
    {
        if (!fIsShown())
        {
            // Leaving the modern scheme puts the pointer item back and gives the pointer up; a
            // thing with no room stays on it, the classic drag takes it over (UWItemDrag).
            if (mbOpen && !UWModernUi.IsActive && !Close())
                fGiveUpPointer();

            Split.End();
            Menu.Close();
            return;
        }

        // The question box has the mouse (UWModernQuestion).
        if (UWModernQuestion.IsBlocking)
            return;

        // The split box has the keys and the clicks while it is open (UWModernBagSplit).
        if (Split.IsOpen)
        {
            if (UWModernUi.IsMenuOpen || !UWModernPointer.IsFree || mOScheme.Controls == null || (mOUi != null && mOUi.Map.IsMapVisible))
                Split.End();
            else
                Split.Update(mOScheme.Controls);

            return;
        }

        // The context menu likewise (UWModernBagMenu).
        if (Menu.IsOpen)
        {
            if (UWModernUi.IsMenuOpen || !UWModernPointer.IsFree || mOScheme.Controls == null || (mOUi != null && mOUi.Map.IsMapVisible))
                Menu.Close();
            else
                Menu.Update(mOScheme.Controls);

            return;
        }

        if (UWModernUi.IsMenuOpen || UWControls.IsTextEntryActive || (mOUi != null && mOUi.Map.IsMapVisible))
            return;

        UWControls lOControls = mOScheme.Controls;

        if (lOControls == null)
            return;

        OpenBags.Resolve();

        // IN A CONVERSATION THE PANEL STAYS (per user, 2026-10-08: the d-pad's down, which steps the
        // answers there, closed it) - the conversation opened it on its Character tab for the trade
        // (UWModernConversation.fBegin); neither the pad nor C toggles it meanwhile.
        bool lbTalking = UWConversationController.IsAnyOpen;

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
        else if (!IsActive && UWModernPointer.IsFree && Mouse.current != null
            && (lOControls.Player.CursorDrag.WasPressedThisFrame()
                || (lOControls.Player.Interact.WasPressedThisFrame() && UWModernPointer.SwitchedFrame != Time.frameCount)))
        {
            // The click that opens the bags does nothing else - the same click handled below as
            // well closed the container it had just opened (per user, 2026-10-04).
            Clicks.ClickShutBackpack(Mouse.current.position.ReadValue());
            return;
        }

        if (!IsActive)
        {
            if (Drag.IsUsing)
                CancelUse();

            return;
        }

        OpenBags.Prune();

        // The right button locked the pointer for a look around: the windows stay, the mouse is
        // the view's until it switches back.
        if (!UWModernPointer.IsFree)
        {
            Clicks.EndPress();
            return;
        }

        Mouse lOMouse = Mouse.current;

        if (lOMouse == null)
            return;

        Vector2 lOPointer = lOMouse.position.ReadValue();
        UWInventory lOInventory = fInventory();

        float lfWheel = lOMouse.scroll.ReadValue().y;

        if (Mathf.Abs(lfWheel) > 0.01f)
            Pages.Scroll(lOPointer, lfWheel > 0f ? -1 : 1);

        if (lOControls.Player.CursorDrag.WasPressedThisFrame())
            Clicks.LeftPressed(lOPointer, lOInventory);
        else if (lOControls.Player.CursorDrag.WasReleasedThisFrame() && !Trade.IsPressing)
            Clicks.LeftReleased(lOPointer, lOInventory);

        Trade.UpdatePress(lOControls, lOPointer, lOInventory);

        if (lOControls.Player.Interact.WasPressedThisFrame() && Drag.IsUsing
            && UWModernPointer.SwitchedFrame != Time.frameCount)
            CancelUse();
        else if (lOControls.Player.Interact.WasPressedThisFrame() && lOInventory.CursorItem == null
            && UWModernPointer.SwitchedFrame != Time.frameCount)
            Clicks.RightPressed(lOPointer, lOInventory);
    }

    // ------------------------------------------------- Drawing

    /// <summary>THE ORDER THE BAGS ARE LAID OUT IN every frame, as one LateUpdate did it until
    /// cleanup 5.17: the windows (or the original page's stand-in), the hover, the thing on the
    /// pointer, the tooltip, the split box, the menu - the last two over everything and the
    /// tooltip hidden under them.</summary>
    private void LateUpdate()
    {
        if (!fIsShown())
        {
            View.Hide();
            return;
        }

        View.Layout();

        UWInventory lOInventory = fInventory();

        View.UpdateHover();
        Drag.LayoutPointerItem(lOInventory);
        View.UpdateTooltip(lOInventory, fData());
        Split.Layout();
        Menu.Layout();
    }

    // ------------------------------------------------- What the rest of the game asks

    /// <summary>Whether the bags' use mode waits for its target (UWModernBagDrag).</summary>
    public bool IsUsing => Drag.IsUsing;

    /// <summary>The use mode (UWModernBagDrag.BeginUse): the anvil from the world, a carried thing
    /// applied to something else (UWCarriedItemUse).</summary>
    public void BeginUse(UWObject pOItem, bool pbFromWorld)
    {
        Drag.BeginUse(pOItem, pbFromWorld);
    }

    /// <summary>The use mode ends (UWModernBagDrag.CancelUse).</summary>
    public void CancelUse()
    {
        Drag.CancelUse();
    }

    /// <summary>Q over the interface (UWModernBagClicks.TryLookAt).</summary>
    public bool TryLookAt(Vector2 pOPointer)
    {
        return Clicks.TryLookAt(pOPointer);
    }

    /// <summary>E tapped over the interface (UWModernBagClicks.TryUseAt).</summary>
    public bool TryUseAt(Vector2 pOPointer)
    {
        return Clicks.TryUseAt(pOPointer);
    }

    /// <summary>Looks at a carried thing as the menu's Look does - for the action bar's slots.</summary>
    public void LookAt(UWObject pOItem)
    {
        if (pOItem != null)
            Clicks.Look(pOItem);
    }

    /// <summary>Puts a carried piece on wherever it lies (UWModernBagDrag.EquipCarried) - for the
    /// action bar's weapons.</summary>
    internal bool EquipCarried(UWObject pOItem, UWArmorItemMap.BodySlot peSlot)
    {
        return Drag.EquipCarried(pOItem, peSlot);
    }

    /// <summary>The menu of a thing in the world (UWModernBagMenu.OpenWorld).</summary>
    public void OpenWorldMenu(UWEntityInfo pOTarget, UWEntityInfo pOPickableBehind, bool pbCreature, bool pbPickable, Vector2 pOAt)
    {
        Menu.OpenWorld(pOTarget, pOPickableBehind, pbCreature, pbPickable, pOAt);
    }

    /// <summary>A thing taken from the world by the free pointer's drag (UWModernBagClicks.BeginWorldDrag).</summary>
    public void BeginWorldDrag(Vector2 pOStart)
    {
        Clicks.BeginWorldDrag(pOStart);
    }

    /// <summary>The middle of the backpack's first slot on the screen (UWModernBagPages) - where the
    /// freed pointer appears (UWModernPointer).</summary>
    public bool TryGetFirstBackpackSlot(out Vector2 pOCentre)
    {
        return Pages.TryGetFirstBackpackSlot(out pOCentre);
    }

    /// <summary>Whether a bag window (or the backpack) lies under a screen point - the minimap
    /// below leaves such clicks to the bags.</summary>
    public bool IsOverWindow(Vector2 pOPointer)
    {
        return fIsShown() && (Pages.FindWindow(pOPointer, out UWModernBagPages.Window _) || (IsSplitting && Split.ScreenRect.Contains(pOPointer))
            || (IsMenuOpen && Menu.ScreenRect.Contains(pOPointer)));
    }

    /// <summary>The open bags into UWHelpNotes for saving (UWModernOpenBags.Capture).</summary>
    public static void CaptureOpenBags()
    {
        UWModernBags lOBags = Instance;

        if (lOBags == null)
            return;

        lOBags.OpenBags.Capture();
    }

    /// <summary>The menu has the keys and the mouse (UWTextEntryState.IsTextEntryActive, UWModernPointer) -
    /// also in the frame it closed, so its click and its right button do nothing more.</summary>
    public static bool IsMenuOpen => Instance != null && Instance.Menu.HasInput;

    /// <summary>Whether the split box has the keys (UWTextEntryState.IsTextEntryActive) - also in the frame
    /// it closed, so its Escape or Enter does nothing more.</summary>
    public static bool IsSplitting => Instance != null && Instance.Split.HasInput;
}
