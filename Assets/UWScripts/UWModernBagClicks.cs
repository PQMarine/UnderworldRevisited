using UnityEngine;
using UWDataImport.UWData;
using Window = UWModernBagPages.Window;

/// <summary>
/// THE BAGS' CLICKS (cleanup 5.17, 2026-10-23: out of UWModernBags, as they were):
///
///   - the LEFT button takes a thing and puts it down - held and dragged, or click and click
///     (the press on a slot takes it; let go on the same slot without moving it stays on the
///     pointer, anywhere else it is put down there; the next click puts it down);
///   - the RIGHT button opens a thing's CONTEXT MENU (per user, 2026-10-04 - before it used at
///     once, and Shift with it looked; UWModernBagMenu); a container opens or closes at once
///     instead (the rune bag: the rune panel), Shift with it gives its menu; the right button
///     closes what it opened (a window's badge, the original page's container icon);
///   - a click on the backpack while the bags are shut opens them, as B does - on a container its
///     window as well (per user, 2026-10-03);
///   - Q and E over the interface look at and use the thing in the slot under the pointer
///     (TryLookAt, TryUseAt).
///
/// Shift and the left button on a stack split it (UWModernBagSplit); in a conversation a trade
/// slot is the trade's (UWModernBagTrade). What a take and a drop do is UWModernBagDrag's.
/// </summary>
internal sealed class UWModernBagClicks
{
    public UWModernBagClicks(UWModernBags pOBags)
    {
        mOBags = pOBags;
    }

    private readonly UWModernBags mOBags;

    /// <summary>Screen pixels the pointer may move before a press counts as a drag.</summary>
    internal const float ClickMoveThreshold = 6f;

    /// <summary>A press that took a thing and may still turn into a drag.</summary>
    private bool mbPressDrag;

    private Vector2 mOPressStart;

    private Window mOPressWindow;

    private int miPressSlot = -1;

    /// <summary>The press took its thing from a paperdoll slot (mePressEquip), not a window.</summary>
    private bool mbPressEquip;

    private UWArmorItemMap.BodySlot mePressEquip;

    /// <summary>No press goes on into a drag (the bags close, the pointer locks, a menu or the
    /// split box opens, the thing stays on the pointer until the next click).</summary>
    internal void EndPress()
    {
        mbPressDrag = false;
    }

    /// <summary>A press that took a thing (pbDrag: and may become a drag) from a window's slot, or
    /// from no slot at all (the world, a trade slot: window null, slot -1).</summary>
    internal void BeginPress(bool pbDrag, Vector2 pOStart, Window pOWindow, int piSlot)
    {
        mbPressDrag = pbDrag;
        mOPressStart = pOStart;
        mOPressWindow = pOWindow;
        miPressSlot = piSlot;
        mbPressEquip = false;
    }

    /// <summary>A thing taken from the world by the free pointer's drag (Interaction): the bags
    /// open, and it is put down where the button is let go (LeftReleased), as a drag in them.</summary>
    internal void BeginWorldDrag(Vector2 pOStart)
    {
        if (!mOBags.IsOpen)
            mOBags.Open();

        mOBags.Drag.ForgetOrigin();
        BeginPress(true, pOStart, null, -1);
    }

    internal void LeftPressed(Vector2 pOPointer, UWInventory pOInventory)
    {
        UWModernBagPages lOPages = mOBags.Pages;

        if (lOPages.PageControls(pOPointer, pOInventory))
            return;

        if (pOInventory.CursorItem != null)
        {
            mOBags.Drag.Place(pOPointer, pOInventory);
            return;
        }

        if (mOBags.Drag.IsUsing)
        {
            mOBags.Drag.ApplyUse(pOPointer, pOInventory);
            return;
        }

        if (lOPages.FindCloseBadge(pOPointer, out Window lOClosing))
        {
            mOBags.OpenBags.Close(lOClosing.Container);
            return;
        }

        // A conversation's trade slot (UWModernBagTrade).
        if (mOBags.Trade.Pressed(pOPointer))
            return;

        // A spell waiting for an item (Name Enchantment, UWHudRunes.fAfterModernCast) takes the
        // thing clicked - B17's one rule with the modern policy (UWPendingSpellState.
        // InventoryPolicy.Modern, cleanup step 5.14, 2026-10-22): on no item it keeps waiting.
        Interaction lOInteraction = mOBags.Interaction;

        if (lOInteraction != null && lOInteraction.IsItemTargetSpellPending)
        {
            UWObject lOTarget = null;

            if (UWModernBagPages.PanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leTargetSlot))
                lOTarget = pOInventory.GetEquipped(leTargetSlot);
            else if (lOPages.FindSlot(pOPointer, out Window lOTargetWindow, out int liTargetSlot))
                lOTarget = lOPages.GetItem(lOTargetWindow, liTargetSlot);

            if (lOInteraction.ResolveTargetSpellInInventory(lOTarget, UWPendingSpellState.InventoryPolicy.Modern)
                == UWPendingSpellState.InventoryClickOutcome.CastOnItem)
                return;
        }

        // Any other click waits while a spell waits (UWModernPointer.PanelsHeld); until 2026-10-13
        // the bags and the paperdoll still picked things up (per user).
        if (UWModernPointer.PanelsHeld)
            return;

        if (UWModernBagPages.PanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leEquip))
        {
            UWObject lOEquipped = pOInventory.GetEquipped(leEquip);

            if (lOEquipped == null)
                return;

            if (mOBags.Split.TryBegin(lOEquipped, null, -1, leEquip, new Rect(pOPointer, Vector2.zero)))
                return;

            mOBags.Drag.TakeEquipped(leEquip, pOInventory);

            mbPressDrag = pOInventory.CursorItem != null;
            mOPressStart = pOPointer;
            mOPressWindow = null;
            miPressSlot = -1;
            mbPressEquip = true;
            mePressEquip = leEquip;
            return;
        }

        if (!lOPages.FindSlot(pOPointer, out Window lOWindow, out int liSlot))
            return;

        UWObject lOItem = lOPages.GetItem(lOWindow, liSlot);

        if (lOItem == null)
            return;

        if (mOBags.Split.TryBegin(lOItem, lOWindow, liSlot, UWArmorItemMap.BodySlot.Helmet, lOPages.SlotRect(lOWindow, liSlot)))
            return;

        mOBags.Drag.Take(lOWindow, liSlot, pOInventory);

        BeginPress(pOInventory.CursorItem != null, pOPointer, lOWindow, liSlot);
    }

    /// <summary>A press that took a thing ends: on the same slot without moving it stays on
    /// the pointer (click and click), anywhere else it is put down there.</summary>
    internal void LeftReleased(Vector2 pOPointer, UWInventory pOInventory)
    {
        if (!mbPressDrag)
            return;

        mbPressDrag = false;

        if (pOInventory.CursorItem == null)
            return;

        bool lbSameSlot = mbPressEquip
            ? UWModernBagPages.PanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leEquip) && leEquip == mePressEquip
            : mOBags.Pages.FindSlot(pOPointer, out Window lOWindow, out int liSlot) && lOWindow == mOPressWindow && liSlot == miPressSlot;

        if (lbSameSlot || Vector2.Distance(pOPointer, mOPressStart) < ClickMoveThreshold)
            return;

        mOBags.Drag.Place(pOPointer, pOInventory);
    }

    /// <summary>The right button on a thing in a bag window or on the paperdoll: its context menu
    /// (UWModernBagMenu) - a CONTAINER opens or closes at once instead, the rune bag opens the rune
    /// panel (per user, 2026-10-04: the most frequent use by far); Shift with it gives the menu.</summary>
    internal void RightPressed(Vector2 pOPointer, UWInventory pOInventory)
    {
        if (mOBags.Interaction == null || UWModernPointer.PanelsHeld)
            return;

        // In a conversation a trade slot looks (UWModernBagTrade); one's own things keep their menu,
        // without the uses (UWModernBagMenu).
        if (mOBags.Trade.Look(pOPointer))
            return;

        if (fRightCloses(pOPointer, pOInventory))
            return;

        if (UWModernBagPages.PanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leEquip))
        {
            UWObject lOWorn = pOInventory.GetEquipped(leEquip);

            if (lOWorn != null && !fOpenContainerAtOnce(lOWorn))
                mOBags.Menu.Open(lOWorn, null, -1, leEquip, pOPointer);

            return;
        }

        if (!mOBags.Pages.FindSlot(pOPointer, out Window lOWindow, out int liSlot))
            return;

        UWObject lOItem = mOBags.Pages.GetItem(lOWindow, liSlot);

        if (lOItem != null && !fOpenContainerAtOnce(lOItem))
            mOBags.Menu.Open(lOItem, lOWindow, liSlot, UWArmorItemMap.BodySlot.Helmet, pOPointer);
    }

    /// <summary>
    /// THE RIGHT BUTTON CLOSES WHAT IT OPENED (per user, 2026-10-20: a container opens on the
    /// right button, but the bag window's badge and the original page's open container icon
    /// (Classic Wide, Classic Modular) closed it on the left one only, and one kept mixing them
    /// up). The badge closes its window, the icon closes the open container one level up - the
    /// left button keeps doing both. The pointer is empty here (RightPressed's caller), so the
    /// icon's "put the thing one level up" does not arise. True when the press was this.
    /// </summary>
    private bool fRightCloses(Vector2 pOPointer, UWInventory pOInventory)
    {
        if (mOBags.Pages.FindCloseBadge(pOPointer, out Window lOClosing))
        {
            mOBags.OpenBags.Close(lOClosing.Container);
            return true;
        }

        UWModernInventoryPage lOPage = UWModernInventoryPage.Instance;
        UWInventoryModel lOModel = pOInventory.Model;

        if (!UWModernBagPages.IsPageShown || lOModel == null || lOModel.OpenContainer == null || !lOPage.IsContainerIconShown
            || UWInventoryView.ControlOf(lOPage.SpotAt(pOPointer)) != UWInventoryView.ControlEnum.ContainerIcon)
            return false;

        lOModel.CloseContainer();
        return true;
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
            mOBags.OpenBags.Toggle(pOItem);
        }

        return true;
    }

    /// <summary>A click on the backpack while the bags are shut: they open as with B, and a
    /// container clicked opens its window (not the rune bag, whose shelf comes with the spells).
    /// The click does nothing else.</summary>
    internal void ClickShutBackpack(Vector2 pOPointer)
    {
        UWModernBagPages lOPages = mOBags.Pages;

        if (!lOPages.FindWindow(pOPointer, out Window lOWindow) || lOWindow.Container != null)
            return;

        mOBags.Open();

        if (!lOPages.FindSlot(pOPointer, out Window lOSlotWindow, out int liSlot) || lOSlotWindow != lOWindow)
            return;

        UWObject lOItem = lOPages.GetItem(lOWindow, liSlot);

        if (lOItem != null && lOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers
            && lOItem.ID != UWObjectMechanics.RuneBagId && !mOBags.OpenBags.Bags.Contains(lOItem))
            mOBags.OpenBags.Toggle(lOItem);
    }

    // ------------------------------------------------- Look and use over the interface

    /// <summary>Q over the interface (UWModernWorldInput.UpdateActions): looks at the thing in
    /// the character panel's slot or the bag slot under the pointer, as the menu's Look does.
    /// False when there is none.</summary>
    internal bool TryLookAt(Vector2 pOPointer)
    {
        UWInventory lOInventory = mOBags.Inventory;
        UWObject lOItem = null;

        if (lOInventory != null && UWModernBagPages.PanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leSlot))
            lOItem = lOInventory.GetEquipped(leSlot);
        else if (mOBags.Pages.FindSlot(pOPointer, out Window lOWindow, out int liSlot))
            lOItem = mOBags.Pages.GetItem(lOWindow, liSlot);

        if (lOItem == null)
            return false;

        Look(lOItem);
        return true;
    }

    /// <summary>
    /// E tapped over the interface (UWModernWorldInput.fUpdateModernUseKey, per user 2026-10-07): the
    /// thing in the slot under the pointer does what makes sense for it, the menu's first entry -
    /// a container opens or closes (the rune bag: the rune panel), a thing to wear in a bag is put
    /// on, anything else is used as the menu's Use uses it (with a target: the use mode). Worn
    /// armour and a thing without a use stay as they are, silently. In a conversation nothing is
    /// used, as in the menu. False when no slot of the bags or the panel is under the pointer.
    /// </summary>
    internal bool TryUseAt(Vector2 pOPointer)
    {
        UWInventory lOInventory = mOBags.Inventory;

        if (lOInventory == null || lOInventory.CursorItem != null || mOBags.Drag.IsUsing)
            return false;

        UWObject lOItem;
        Window lOWindow = null;
        int liSlot = -1;
        bool lbEquipped = false;

        if (UWModernBagPages.PanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leEquip))
        {
            lOItem = lOInventory.GetEquipped(leEquip);
            lbEquipped = true;
        }
        else if (mOBags.Pages.FindSlot(pOPointer, out lOWindow, out liSlot))
            lOItem = mOBags.Pages.GetItem(lOWindow, liSlot);
        else
            return false;

        if (lOItem == null)
            return true;

        if (lOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
        {
            if (lOItem.ID != UWObjectMechanics.RuneBagId)
                mOBags.OpenBags.Toggle(lOItem);
            else if (UWModernRunePanel.Instance != null && !UWModernBagTrade.IsTalking)
                UWModernRunePanel.Instance.Open(UWModernRunePanel.TabEnum.Runes);

            return true;
        }

        if (UWModernBagTrade.IsTalking)
            return true;

        bool lbWearable = UWModernBagDrag.TryGetEquipSlot(lOItem, lOInventory, out UWArmorItemMap.BodySlot leTarget);

        if (lbWearable)
        {
            if (!lbEquipped)
                mOBags.Drag.Equip(lOWindow, liSlot, leTarget, lOInventory);

            return true;
        }

        UWCarriedItemUse.Use(UWScene.ItemDrag, lOItem, UWCarriedItemUse.Context.ModernBags);

        return true;
    }

    internal void Look(UWObject pOItem)
    {
        Interaction lOInteraction = mOBags.Interaction;

        try
        {
            // In a conversation the look replaces the answers for a moment (the messages are
            // hidden there), as the classic screen shows it.
            if (UWModernBagTrade.IsTalking)
            {
                UWModernBagTrade.ShowTalkText(lOInteraction.DescribeInventoryItem(pOItem));
                return;
            }
        }
        catch
        {
            return;
        }

        if (lOInteraction.TryReadBook(pOItem, true))
            return;

        try
        {
            lOInteraction.AddItemLookMessages(pOItem);
        }
        catch
        {
            // As UWItemDrag: a missing string is no message, not an error.
        }
    }
}
