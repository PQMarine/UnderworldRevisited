using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE CLASSIC PANEL'S CLICKS THAT DO NOT BECOME A DRAG (cleanup step 5.15, 2026-10-22: out of
/// UWItemDrag, which decides on the release which click it was - see
/// UWItemDrag.fReleasePending): what a press hit on the panel (SourceAt), the look (Look), the use
/// (Use: the trade's mark, the rune bag's shelf, opening a container, the combat toggle in a
/// conversation, else the chain every scheme shares, UWCarriedItemUse), the combat toggle on the
/// weapon hand, and a pending target spell discharged onto a carried thing (TryResolveTargetSpell).
/// </summary>
public sealed class UWClassicPanelClick
{
    /// <summary>The drag - the host of the classic panel's use (UWCarriedItemUse).</summary>
    private readonly UWItemDrag mODrag;

    private readonly UWInventory mOInventory;

    private readonly UWGameUI mOGameUi;

    private readonly Interaction mOInteraction;

    private readonly UWClassicTradeDrag mOTrade;

    /// <summary>The item uses' shared parts - the game data.</summary>
    private readonly UWItemUseHost.Services mOServices;

    public UWClassicPanelClick(UWItemDrag pODrag, UWInventory pOInventory, UWGameUI pOGameUi, Interaction pOInteraction,
        UWClassicTradeDrag pOTrade, UWItemUseHost.Services pOServices)
    {
        mODrag = pODrag;
        mOInventory = pOInventory;
        mOGameUi = pOGameUi;
        mOInteraction = pOInteraction;
        mOTrade = pOTrade;
        mOServices = pOServices;
    }

    /// <summary>
    /// What a press at pOMousePos hit on the classic panel - the container icon, a scroll arrow
    /// (both only for a use click, pbUseClick), a place of the open container, of the backpack or
    /// the paperdoll; None for nothing, an empty place, or the 3D view (UWItemDrag.fTryStartPending
    /// asks it after the trade areas).
    /// </summary>
    public UWDragSource SourceAt(Vector2 pOMousePos, bool pbUseClick)
    {
        // Original: the container icon (above left of the container panel, only visible with
        // an open container) closes on left click - no dragging possible, so
        // only checked for the left button at all.
        if (pbUseClick && mOGameUi.Inventory.IsContainerOpenIconUnderMouse(pOMousePos))
            return UWDragSource.ContainerIcon;

        // Original: the scroll arrows scroll the container window by a whole row on left click
        // (confirmed by user) - no dragging possible either, left
        // button only. The UpArrow moves the entire content UP (confirmed by user) -
        // the current second row moves up to the first, the ones behind it (further back in the
        // container) become visible; so it increases the scroll offset rather than decreasing
        // it (the reverse for the DownArrow). The step is UWInventoryView's, shared with the
        // modern original page (cleanup step 5.8, 2026-10-20).
        if (pbUseClick && mOGameUi.Inventory.IsContainerScrollUpUnderMouse(pOMousePos))
            return UWDragSource.ScrollArrow(UWInventoryView.ScrollDeltaOf(UWInventoryView.ControlEnum.ScrollUp));

        if (pbUseClick && mOGameUi.Inventory.IsContainerScrollDownUnderMouse(pOMousePos))
            return UWDragSource.ScrollArrow(UWInventoryView.ScrollDeltaOf(UWInventoryView.ControlEnum.ScrollDown));

        // If a container is open, its panel lies exactly on top of the backpack (see
        // UWGameUI.GetBackpackSlotUnderMouse, which then returns -1 itself) - so check this
        // first.
        int liContainerSlot = mOGameUi.Inventory.GetContainerSlotUnderMouse(pOMousePos);

        if (liContainerSlot >= 0)
            return mOGameUi.Inventory.GetContainerItem(liContainerSlot) == null
                ? UWDragSource.None
                : UWDragSource.Container(liContainerSlot);

        int liSlot = mOGameUi.Inventory.GetBackpackSlotUnderMouse(pOMousePos);

        if (liSlot >= 0)
            return mOGameUi.Inventory.GetBackpackItem(liSlot) == null ? UWDragSource.None : UWDragSource.Backpack(liSlot);

        // Nothing hit in the backpack - instead check whether an equipped
        // piece is being dragged off the body (unequipping). Fixed hit zones (see
        // UWGameUI.mArmorHitZones), not the texture-dependent armour images.
        UWArmorItemMap.BodySlot? leZone = mOGameUi.Inventory.GetArmorHitZoneUnderMouse(pOMousePos);

        // The EMPTY weapon hand also counts as a click target - unarmed combat
        // starts there (per user, 2026-08-30). Without this exception an
        // empty body slot was never registered, and the click went nowhere.
        bool lbEmptyWeaponHand = leZone.HasValue && leZone.Value == mOInventory.MainHandSlot;

        // Neither backpack nor paper doll hit. The 3D view is Interaction's: it reads the
        // right button once (UWWorldPointer) and calls TryBeginWorldDrag when a pickable
        // thing is dragged - until 2026-09-18 a second raycast and a second click
        // threshold for the same button lived here.
        if (!leZone.HasValue || (!lbEmptyWeaponHand && mOInventory.GetEquipped(leZone.Value) == null))
            return UWDragSource.None;

        return UWDragSource.Equip(leZone.Value);
    }

    /// <summary>
    /// A pending target spell, discharged onto the inventory.
    ///
    /// THIS IS HOW THE ORIGINAL BEHAVES (per user, 2026-09-10): a click into the inventory - with
    /// any button - consumes the spell, even if it hits nothing valid. And the
    /// click still does what it otherwise does: whoever has the wand in a container and
    /// hits its close button with the left button loses the spell AND closes the
    /// container.
    ///
    /// That is exactly the return value: true means "the click may continue".
    ///
    /// A projectile spell does not get through here (IsItemTargetSpellPending) - for it
    /// the inventory stays silent as before.
    /// </summary>
    public bool TryResolveTargetSpell(UWControls pOControls)
    {
        if (!mOInteraction.IsItemTargetSpellPending)
            return false;

        if (!UWMouseButtons.LeftPressed
            && !pOControls.Player.Interact.WasPressedThisFrame())
            return false;

        Vector2 lOMousePos = UWDragIcon.PointerPosition(mOGameUi);

        // In the view window Interaction does the targeting itself - there the right button counts, and the
        // left one walks.
        if (mOGameUi.ClassicScreen.IsScreenPositionInGameArea(lOMousePos))
            return false;

        UWObject lOItem = UWClassicUseMode.CarriedItemAt(mOGameUi, mOInventory, lOMousePos);

        // B17's one rule with the classic policy (UWPendingSpellState.InventoryPolicy.Classic,
        // cleanup step 5.14, 2026-10-22): on no item the spell is lost.
        UWPendingSpellState.InventoryClickOutcome leOutcome = mOInteraction.ResolveTargetSpellInInventory(lOItem,
            UWPendingSpellState.InventoryPolicy.Classic);

        // If the spell found a target, the click is consumed by it - otherwise it would
        // also look at or pick up the item right away.
        return leOutcome == UWPendingSpellState.InventoryClickOutcome.Lost;
    }

    /// <summary>The use click on the pending source (UWItemDrag.fReleasePending): pOSource is the
    /// press's source, pOPendingItem the thing still in it.</summary>
    public void Use(UWDragSource pOSource, UWObject pOPendingItem)
    {
        UWObject lOPendingItem = pOPendingItem;
        UWConversationTrade lOPendingTrade = pOSource.Kind == UWDragSource.KindEnum.PlayerTrade ? mOTrade.Trade : null;

        // A plain left click on an item in the player's trade area marks it for the
        // deal, exactly like its cross (per user on the original, 2026-09-18).
        if (lOPendingTrade != null)
            lOPendingTrade.TogglePlayerSelected(pOSource.Index);
        // The rune bag is a container, but does not behave like one:
        // instead of a container window the panel flips to the rune shelf
        // (per user, 2026-09-03). That is why this check comes BEFORE the
        // container check.
        else if (lOPendingItem != null && lOPendingItem.ID == UWObjectMechanics.RuneBagId)
        {
            // Flipping to the rune shelf is a use, and in a conversation the left
            // click uses nothing (per user, 2026-09-18) - it must not fall through to
            // the container branch either, the bag is a container by category.
            if (mOTrade.Trade == null)
                mOGameUi.Panel.ToggleRunePanel();
        }
        else if (lOPendingItem != null && lOPendingItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
            mOInventory.TryOpenContainer(lOPendingItem);
        // In a conversation the left click does not use anything (per user, 2026-09-11);
        // opening containers and dragging still work - trading needs that. THE ONE
        // EXCEPTION is the combat mode from the weapon hand: the original flips it at
        // once, the Fight icon lights up, and the weapon is drawn only once the
        // conversation has returned to the game loop (per user on the original,
        // 2026-09-18). UWCharacter holds the weapon state machine while a conversation
        // is open, so the draw starts when the parchment is gone, as there.
        else if (mOTrade.Trade != null)
        {
            ToggleCombatFromClick(pOSource);
        }
        // The rest is the chain every scheme shares (UWCarriedItemUse): the map opens on
        // the LEFT click (the right click still shows its text), a book is read, what
        // works at once is used, a key or tool starts the use mode - and a click that used
        // nothing toggles the combat mode on the weapon hand.
        else
            UWCarriedItemUse.Use(mODrag, lOPendingItem, UWCarriedItemUse.Context.ClassicPanel);
    }

    /// <summary>Opens the overview map of the current level.</summary>
    public void OpenMap()
    {
        if (mOGameUi == null)
            return;

        // The level the character is currently standing on.
        mOGameUi.Map.ShowMap(mOGameUi.Map.CurrentMapLevelIndex);
    }

    /// <summary>Like a world look target, but for a backpack/equipment/container/
    /// world item that is still in its source slot (a simple click never
    /// removed it). The message comes from Interaction.DescribeInventoryItem, which reads
    /// the name with +1 on the object ID, since Strings.Blocks[4] is indexed shifted by
    /// one relative to the object ID (see UWArmorItemMap - verified at tile (24,10) on level 1).
    /// For a world object whose object data yields no description, the finished one is taken from
    /// UWEntityInfo.Description instead (the same one Interaction.cs uses for normal
    /// world look targets) - the raw original string,
    /// UWObjectDescriptionFormatter builds the "You see ..." message from it (see there for
    /// the "_"/"&amp;" structure).</summary>
    public void Look(UWObject pOItem)
    {
        if (mOInteraction == null)
            return;

        UWObject lOItem = pOItem;

        if (mOServices.Data == null || lOItem == null)
            return;

        // IN A CONVERSATION the look goes onto the parchment (UWClassicTradeDrag.TryLook).
        if (mOTrade.TryLook(lOItem))
            return;

        // A text scroll is also read on right click (per user, 2026-09-03).
        // The look message is not lost - it is the first line of what is
        // read. Scrolls without text (picture parchment, map) fall through and are
        // only described, as before.
        if (mOInteraction.TryReadBook(lOItem, true))
            return;

        try
        {
            mOInteraction.AddItemLookMessages(lOItem);
        }
        catch
        {
            // Like fGetDescription in UWObjectSpawner: a missing string block entry is not an
            // error, simply no message.
        }
    }

    /// <summary>Original: simple left click (no drag) on the hand slot that is currently
    /// the active combat hand (UWInventory.MainHandSlot) - toggles combat mode, exactly
    /// like Interaction.ToggleCombatMode, but only if the hand is empty or holds
    /// a weapon. Every other left click (other slot, backpack, world) has
    /// no effect here.</summary>
    public void ToggleCombatFromClick(UWDragSource pOSource)
    {
        if (mOInteraction == null || !pOSource.IsEquip(mOInventory.MainHandSlot))
            return;

        UWObject lOItem = mOInventory.GetEquipped(pOSource.Slot);

        // An empty hand also starts combat mode - unarmed then (per user,
        // 2026-08-30). Before, a click on the empty hand had no effect, so unarmed combat
        // was not reachable at all.
        if (lOItem != null && lOItem.GetCategory() != UWObject.ObjectCategoryEnum.WeaponsAndMissiles)
            return;

        mOInteraction.ToggleCombatMode();
    }

    /// <summary>Right click on the empty weapon hand: unarmed combat mode. Returns false
    /// if something is there or another slot was meant - then it is looked at.</summary>
    public bool TryToggleCombatFromEmptyHand(UWDragSource pOSource)
    {
        if (mOInteraction == null || !pOSource.IsEquip(mOInventory.MainHandSlot))
            return false;

        if (mOInventory.GetEquipped(pOSource.Slot) != null)
            return false;

        mOInteraction.ToggleCombatMode();

        return true;
    }
}
