using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport.UWData;
using Window = UWModernBagPages.Window;

/// <summary>
/// WHAT HANGS ON THE MODERN POINTER (cleanup 5.17, 2026-10-23: out of UWModernBags, as it was):
///
///   - THE THING TAKEN: from a slot of a bag window, the backpack or the paperdoll onto the
///     pointer, remembering where from (UWCursorOrigin), and put down where the pointer is - a
///     backpack slot (a container there takes it inside, as in the original), a bag slot (the
///     model stacks, combines, swaps or appends), a window's free space, the action bar, or -
///     outside the windows - the world (low on the screen dropped in front, higher thrown - the
///     original's rule, UWPlayerThrow); food and drink put down on the paperdoll's head are
///     eaten, as on the classic page. What hangs on the pointer goes back where it came from
///     when the bags close (UWInventoryModel.ReturnCursorItem). Putting a piece on through the
///     pointer (Equip, EquipCarried) is the same take and drop.
///   - THE USE MODE (per user, 2026-10-04: the lantern could not be filled): the right button on
///     a thing that is applied to something else (oil flask, key, lockpick, pole ... -
///     UWItemTargetRules.Classify) asks "Use ... on what?" and hangs its picture on the pointer;
///     the next left click applies it - onto a carried thing in a slot, or onto the world under
///     the pointer (UWItemTargetUse.Apply). The right button, Escape, locking the pointer or
///     closing the bags let it go. The anvil in the world comes the same way (BeginUse).
///   - THE PICTURE on the free pointer: the thing's icon and its count. The system pointer is
///     hidden behind it by UWPointerCursor (ShowSystemPointerForBags), the one owner of that
///     hiding since cleanup 5.17.
///
/// The work is the inventory model's (UWInventoryModel: the pointer item, stacking, combining,
/// capacity); this class only maps the take and the drop.
/// </summary>
internal sealed class UWModernBagDrag
{
    public UWModernBagDrag(UWModernBags pOBags)
    {
        mOBags = pOBags;
    }

    private readonly UWModernBags mOBags;

    /// <summary>Where the thing on the pointer came from, to put it back when the bags close - a
    /// backpack place, a container's place, a paperdoll slot or the stack a part was split from
    /// (UWCursorOrigin, read by UWInventoryModel.ReturnCursorItem; cleanup step 5.15, 2026-10-22:
    /// until then five fields here and in StackSplit).</summary>
    private UWCursorOrigin mOOrigin;

    /// <summary>The thing in the use mode, applied by the next left click; null for none.</summary>
    private UWObject mOUseItem;

    internal bool IsUsing => mOUseItem != null;

    /// <summary>The anvil's use mode freed the pointer and opened the bags itself: both go back
    /// when it ends.</summary>
    private bool mbUseFreedPointer;

    private bool mbUseOpenedBags;

    private RawImage mOCursorIcon;

    private Text mOCursorCount;

    // ------------------------------------------------- Where it came from

    internal bool ReturnCursorItem()
    {
        UWInventory lOInventory = mOBags.Inventory;

        if (lOInventory == null || lOInventory.CursorItem == null)
            return true;

        // The model's one way back (UWInventoryModel.ReturnCursorItem, OrAnywhere): the origin
        // while its place is free or its container or stack still carried, else wherever there is
        // room, then the carried light.
        bool lbEmpty = lOInventory.ReturnCursorItem(mOOrigin, UWInventoryModel.ReturnEnum.OrAnywhere);

        ForgetOrigin();

        return lbEmpty;
    }

    internal void ForgetOrigin()
    {
        mOOrigin = UWCursorOrigin.None;
    }

    /// <summary>The stack a part was taken from (the split box), for putting it back.</summary>
    internal void TookFromStack(UWObject pOStack)
    {
        mOOrigin = UWCursorOrigin.Stack(pOStack);
    }

    // ------------------------------------------------- Taking and putting down

    /// <summary>Takes the thing in a slot onto the pointer and remembers where from.</summary>
    internal void Take(Window pOWindow, int piSlot, UWInventory pOInventory)
    {
        ForgetOrigin();

        if (pOWindow.Container == null)
        {
            pOInventory.BeginDragFromBackpack(piSlot);
            mOOrigin = UWCursorOrigin.Backpack(piSlot);
        }
        else
        {
            pOInventory.BeginDragFromContainerItem(pOWindow.Container, pOWindow.FirstIndex + piSlot);
            mOOrigin = UWCursorOrigin.Container(pOWindow.Container, pOWindow.FirstIndex + piSlot);
        }

        mOBags.OpenBags.Prune();
    }

    /// <summary>Takes the thing in a paperdoll slot onto the pointer and remembers where from.</summary>
    internal void TakeEquipped(UWArmorItemMap.BodySlot peSlot, UWInventory pOInventory)
    {
        ForgetOrigin();
        pOInventory.BeginDragFromEquip(peSlot);
        mOOrigin = UWCursorOrigin.Equip(peSlot);
        mOBags.OpenBags.Prune();
    }

    /// <summary>
    /// Puts the thing on the pointer down where the pointer is: a backpack slot (a container
    /// there takes it inside, as in the original), a bag slot (the model stacks, combines,
    /// swaps or appends), a window's free space, or - outside the windows - the world.
    /// </summary>
    internal void Place(Vector2 pOPointer, UWInventory pOInventory)
    {
        bool lbPlaced;
        UWModernBagPages lOPages = mOBags.Pages;

        // A conversation's trade slot (UWModernBagTrade).
        if (mOBags.Trade.TryPlace(pOPointer, pOInventory, out bool lbTraded))
        {
            if (lbTraded)
            {
                ForgetOrigin();
                pOInventory.ApplyCarriedLight();
                mOBags.OpenBags.Prune();
            }

            return;
        }

        if (UWModernBagPages.PanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leEquip))
        {
            // What lands on the head is eaten, as on the classic page (UWItemDrag, per user
            // 2026-10-04); a stack is refused (one at a time) and stays on the pointer, its origin with it.
            UWItemDrag lODrag = UWScene.ItemDrag;

            if (leEquip == UWArmorItemMap.BodySlot.Helmet && lODrag != null && lODrag.TryConsumeCursorItemOnHead())
            {
                if (pOInventory.CursorItem == null)
                    ForgetOrigin();

                return;
            }

            lbPlaced = pOInventory.DropCursorItemInEquip(leEquip);

            if (!lbPlaced)
                ReportRejection(pOInventory);
        }
        else if (lOPages.FindSlot(pOPointer, out Window lOWindow, out int liSlot))
        {
            if (lOWindow.Container == null)
            {
                // A container lying there takes it - the classic frame's rule (cleanup step 5.8).
                lbPlaced = UWInventoryView.DropOnBackpackSlot(pOInventory.Model, liSlot) == UWInventoryView.PutOutcomeEnum.Placed;
            }
            else
            {
                lbPlaced = pOInventory.DropCursorItemInContainerAt(lOWindow.Container, lOWindow.FirstIndex + liSlot);
            }

            if (!lbPlaced)
                ReportRejection(pOInventory);
        }
        else if (lOPages.FindWindow(pOPointer, out Window lOOnWindow))
        {
            lOWindow = lOOnWindow;

            lbPlaced = lOWindow.Container == null
                ? pOInventory.DropCursorItemInFirstFreeBackpackSlot()
                : pOInventory.DropCursorItemInContainerAt(lOWindow.Container,
                    lOWindow.Container.Contents != null ? lOWindow.Container.Contents.Count : 0);

            if (!lbPlaced)
                ReportRejection(pOInventory);
        }
        else if (UWModernActionBar.Instance != null && UWModernActionBar.Instance.TryGetSlotAt(pOPointer, out int liBarSlot))
        {
            // Onto the action bar: the slot is bound to the piece, which goes back to its place
            // (per user, 2026-10-03: a slot holds the single piece, not a copy).
            UWModernActionBar.Instance.TryBind(liBarSlot, pOInventory.CursorItem);
            ReturnCursorItem();
            return;
        }
        else if ((UWModernPanel.Instance != null && UWModernPanel.Instance.Contains(pOPointer)) || UWModernBagTrade.IsTalking
            || (UWModernBagPages.IsPageShown && UWModernInventoryPage.Instance.Contains(pOPointer))
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
                pOInventory, UWScene.LevelLoader, mOBags.Interaction);
        }

        if (!lbPlaced)
            return;

        // A swap leaves the displaced thing on the pointer; its origin is unknown then.
        ForgetOrigin();
        pOInventory.ApplyCarriedLight();
        mOBags.OpenBags.Prune();
    }

    /// <summary>Says why a container refused the item - the sentences are
    /// UWItemMessages.ContainerRejection, shared with UWItemDrag.</summary>
    internal void ReportRejection(UWInventory pOInventory)
    {
        if (mOBags.Interaction == null)
            return;

        string lsMessage = UWItemMessages.ContainerRejection(pOInventory.LastContainerRejection,
            pOInventory.LastContainerRejectionContainer, mOBags.Data);

        if (lsMessage != null)
            mOBags.Interaction.AddMessage(lsMessage);
    }

    // ------------------------------------------------- Putting a piece on

    /// <summary>Where the right button puts a piece on: armour on its place, a ring on a free
    /// finger, a weapon into the weapon hand. Nothing else.</summary>
    internal static bool TryGetEquipSlot(UWObject pOItem, UWInventory pOInventory, out UWArmorItemMap.BodySlot peSlot)
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

        // Weapons the right button puts into the hand: the melee ones and the bows, crossbows
        // and slings - not the ammunition and spell missiles between (UWObjectIds.IsHandWeapon).
        if (UWObjectIds.IsHandWeapon(pOItem.ID))
        {
            peSlot = pOInventory.MainHandSlot;
            return true;
        }

        return false;
    }

    /// <summary>Puts a piece on through the pointer, as a drag onto the paperdoll would: what
    /// was worn there goes back to the piece's place; if that refuses it, it stays on the
    /// pointer.</summary>
    internal void Equip(Window pOWindow, int piSlot, UWArmorItemMap.BodySlot peSlot, UWInventory pOInventory)
    {
        Take(pOWindow, piSlot, pOInventory);

        if (pOInventory.CursorItem == null)
            return;

        // Putting the piece ON: a bag in the slot is swapped, not filled (the drop fills it).
        if (!pOInventory.DropCursorItemInEquip(peSlot, false))
        {
            ReturnCursorItem();
            return;
        }

        if (pOInventory.CursorItem != null)
            ReturnCursorItem();
        else
            ForgetOrigin();

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
        UWInventory lOInventory = mOBags.Inventory;

        if (lOInventory == null || pOItem == null || lOInventory.CursorItem != null)
            return false;

        ForgetOrigin();

        int liBackpack = System.Array.IndexOf(lOInventory.Backpack, pOItem);

        if (liBackpack >= 0)
        {
            lOInventory.BeginDragFromBackpack(liBackpack);
            mOOrigin = UWCursorOrigin.Backpack(liBackpack);
        }
        else if (fFindEquipped(lOInventory, pOItem, out UWArmorItemMap.BodySlot leFrom))
        {
            if (leFrom == peSlot)
                return true;

            lOInventory.BeginDragFromEquip(leFrom);
            mOOrigin = UWCursorOrigin.Equip(leFrom);
        }
        else if (fFindHolder(lOInventory, pOItem, out UWObject lOHolder))
        {
            int liIndex = lOHolder.Contents.IndexOf(pOItem);

            lOInventory.BeginDragFromContainerItem(lOHolder, liIndex);
            mOOrigin = UWCursorOrigin.Container(lOHolder, liIndex);
        }
        else
        {
            return false;
        }

        mOBags.OpenBags.Prune();

        if (lOInventory.CursorItem == null)
            return false;

        if (!lOInventory.DropCursorItemInEquip(peSlot, false) || lOInventory.CursorItem != null)
            ReturnCursorItem();
        else
            ForgetOrigin();

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

    // ------------------------------------------------- The use mode

    /// <summary>The use mode: the thing waits for the left click that says what it goes onto.
    /// From the world (the anvil) the bags open and the pointer frees, as its target is a carried
    /// thing - the one exception to the pointer being the right button's (per user, 2026-10-04:
    /// with the pointer locked the anvil did nothing, and the right button to free it cancelled);
    /// both go back when the mode ends.</summary>
    internal void BeginUse(UWObject pOItem, bool pbFromWorld)
    {
        if (pOItem == null)
            return;

        mbUseOpenedBags = false;
        mbUseFreedPointer = false;

        if (pbFromWorld && !mOBags.IsOpen)
        {
            mOBags.Open();
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

    internal void CancelUse()
    {
        mOUseItem = null;

        // What the anvil opened and freed goes back (the flags first: locking cancels again).
        bool lbLock = mbUseFreedPointer && !UWModernPointer.HoldToLook;
        bool lbClose = mbUseOpenedBags && mOBags.IsOpen;

        mbUseFreedPointer = false;
        mbUseOpenedBags = false;

        if (lbClose)
            mOBags.Close();

        // A question follows (the anvil's repair): the pointer locks only after its answer. Locked
        // now and freed by the question box the next moment, Windows kept the pointer invisible
        // (per user, 2026-10-04).
        if (lbLock && mOBags.Interaction != null && mOBags.Interaction.IsAskingYesNo)
            UWModernQuestion.LockWhenAnswered();
        else if (lbLock)
            UWModernPointer.Lock();
    }

    /// <summary>The click of the use mode: onto the thing in a slot, onto nothing on another window
    /// of the modern UI, else onto the world under the pointer. The mode ends either way, as the
    /// classic one does.</summary>
    internal void ApplyUse(Vector2 pOPointer, UWInventory pOInventory)
    {
        UWObject lOUse = mOUseItem;
        UWItemDrag lODrag = UWScene.ItemDrag;
        UWObject lOTarget = null;

        if (UWModernBagPages.PanelSlotAt(pOPointer, out UWArmorItemMap.BodySlot leSlot))
            lOTarget = pOInventory.GetEquipped(leSlot);
        else if (mOBags.Pages.FindSlot(pOPointer, out Window lOWindow, out int liSlot))
            lOTarget = mOBags.Pages.GetItem(lOWindow, liSlot);

        bool lbOverUi = UWModernUi.IsOverUi(pOPointer);

        // The mode ends right after the thing acted (what the anvil opened goes back - after it
        // asked its question, see CancelUse). Its own flag first, so the thing is not used twice.
        mOUseItem = null;

        // The pole on a carried thing keeps the mode (UWItemTargetRules.KeepsUseMode, 2026-10-15).
        bool lbKeep = false;

        if (lODrag != null && lOTarget != null)
            lbKeep = UWItemTargetUse.Apply(mOBags.Interaction, lOUse, lOTarget, null);
        else if (lODrag != null && !lbOverUi)
        {
            UWItemTargetUse.Apply(mOBags.Interaction, lOUse, null, pOPointer);
            pOInventory.ApplyCarriedLight();
        }

        if (lbKeep)
        {
            mOUseItem = lOUse;
            return;
        }

        CancelUse();
    }

    // ------------------------------------------------- The picture on the pointer

    /// <summary>The thing's icon and count, made on the bags' canvas between the hover line and
    /// the tooltip (UWModernBagView).</summary>
    internal void BuildPointerItem(RectTransform pORoot)
    {
        mOCursorIcon = UWUiKit.CreateRawImage(pORoot, "Pointer item");
        UWIconPalette.ApplySmooth(mOCursorIcon);
        mOCursorCount = UWUiKit.CreateText(pORoot, "Pointer count", TextAnchor.LowerRight, UWModernPalette.HudText, mOBags.Ui);
    }

    internal void KeepPointerItemOnTop()
    {
        mOCursorIcon.transform.SetAsLastSibling();
        mOCursorCount.transform.SetAsLastSibling();
    }

    internal void LayoutPointerItem(UWInventory pOInventory)
    {
        UWObject lOItem = mOBags.IsActive ? pOInventory.CursorItem ?? mOUseItem : null;
        UWTexture lOSource = lOItem != null ? lOItem.Icon ?? lOItem.Texture : null;

        if (lOSource == null || Mouse.current == null)
        {
            mOCursorIcon.enabled = false;
            mOCursorCount.enabled = false;
            mOBags.Ui.PointerCursor.ShowSystemPointerForBags(true);
            return;
        }

        Texture2D lOIcon = UWIconCache.Texture(lOSource, mOBags.Ui.TextureFilterMode);
        Vector2 lOPointer = Mouse.current.position.ReadValue();
        float liScale = mOBags.View.Scale;

        mOCursorIcon.texture = lOIcon;
        mOCursorIcon.enabled = true;

        // THE THING IS THE POINTER (2026-10-06, per user: with a fast mouse the thing trailed the
        // free pointer). The system pointer is drawn by the OS at scan-out with the newest
        // position, our icon a frame later from the position the frame sampled - two pointers
        // can never agree under a fast hand. So while something hangs on the free pointer the
        // system pointer is hidden and the thing alone shows where one points, as in the
        // original, where the picked-up thing replaces the cursor. Restored once the hand is empty.
        mOBags.Ui.PointerCursor.ShowSystemPointerForBags(false);
        UWUiKit.SetRect(mOCursorIcon.rectTransform, lOPointer.x - (lOIcon.width * UWModernHudArt.PixelAspectX * liScale * 0.5f),
            lOPointer.y - (lOIcon.height * liScale * 0.5f), lOIcon.width * UWModernHudArt.PixelAspectX * liScale, lOIcon.height * liScale);

        int liCount = UWStackRules.CountAtLeastOne(lOItem);

        mOCursorCount.enabled = liCount > 1;

        if (liCount > 1)
        {
            mOCursorCount.text = liCount.ToString();
            mOCursorCount.fontSize = Mathf.Max(9, Mathf.RoundToInt(4f * liScale));
            UWUiKit.SetRect(mOCursorCount.rectTransform, lOPointer.x - (lOIcon.width * UWModernHudArt.PixelAspectX * liScale * 0.5f),
                lOPointer.y - (lOIcon.height * liScale * 0.5f) - (2 * liScale), ((lOIcon.width * UWModernHudArt.PixelAspectX) + 2) * liScale, 6 * liScale);
        }
    }
}
