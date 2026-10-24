using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// WHERE THE THING ON THE CLASSIC POINTER LANDS (cleanup step 5.15, 2026-10-22: out of UWItemDrag):
/// when the held button is let go or a sticky click comes, TryPlaceAt tries the trade areas
/// (UWClassicTradeDrag), the open container's icon (one level up), the scroll arrows (they only
/// scroll), the paperdoll (the head eats first), the open container, the backpack (a container
/// there takes it inside) and the view window (drop or throw); what it refuses stays on the
/// pointer and UWItemDrag lets it stick.
/// </summary>
public sealed class UWClassicDrop
{
    /// <summary>The player camera UWItemDrag sits on - the drop and the throw go from it.</summary>
    private readonly Transform mOCamera;

    private readonly UWInventory mOInventory;

    private readonly UWGameUI mOGameUi;

    private readonly Interaction mOInteraction;

    private readonly UWControlScheme mOControlScheme;

    private readonly UWClassicTradeDrag mOTrade;

    /// <summary>The item uses' shared parts - the game data and the host of the eating.</summary>
    private readonly UWItemUseHost.Services mOServices;

    public UWClassicDrop(Transform pOCamera, UWInventory pOInventory, UWGameUI pOGameUi, Interaction pOInteraction,
        UWControlScheme pOControlScheme, UWClassicTradeDrag pOTrade, UWItemUseHost.Services pOServices)
    {
        mOCamera = pOCamera;
        mOInventory = pOInventory;
        mOGameUi = pOGameUi;
        mOInteraction = pOInteraction;
        mOControlScheme = pOControlScheme;
        mOTrade = pOTrade;
        mOServices = pOServices;
    }

    /// <summary>
    /// Places the item from the pointer and extinguishes a burning light source in the process
    /// if it does not end up in a hand slot.
    ///
    /// Only there may one burn (per user, checked in the original, 2026-09-05) - putting it into the
    /// backpack or a container puts it out.
    ///
    /// Returns false if fTryPlaceAtCore found no valid action; the item then stays stuck to
    /// the pointer (see UWItemDrag.fEndHeldDrag).
    /// </summary>
    public bool TryPlaceAt(Vector2 pOMousePos)
    {
        if (!fTryPlaceAtCore(pOMousePos))
            return false;

        // PUTTING IT OUT IS THE MODEL'S JOB (UWInventoryModel.ExtinguishOutsideHand), which does it
        // in every place that takes an item. Here it used to happen for EVERY successful
        // placement, and that also caught the trade area of a conversation - where the original
        // lets a light keep burning, even so far that it lands burning on the floor when the
        // conversation ends with it in a slot (per user on the original, 2026-09-18).
        mOInventory.ApplyCarriedLight();

        return true;
    }

    /// <summary>Tries to place CursorItem at the given screen position (also
    /// as a swap if the target is occupied). Returns true only for an actually
    /// valid action - a recognised target that does not fit the item (e.g.
    /// non-armour on an armour zone) counts as invalid and returns false, the
    /// item then stays stuck to the pointer (see UWItemDrag.fEndHeldDrag).</summary>
    private bool fTryPlaceAtCore(Vector2 pOMousePos)
    {
        // The trade areas of a conversation first (UWClassicTradeDrag.TryPlace).
        UWClassicTradeDrag.PlaceEnum leTrade = mOTrade.TryPlace(mOInventory, pOMousePos);

        if (leTrade != UWClassicTradeDrag.PlaceEnum.NotHere)
            return leTrade == UWClassicTradeDrag.PlaceEnum.Placed;

        // During a held/sticky drag the container icon is a drop target like
        // any other - puts the item into the first free backpack slot (confirmed by
        // user). Without an item on the pointer a simple click on it closes the
        // container instead (see UWItemDrag.fReleasePending) - the two cases are mutually
        // exclusive, since TryPlaceAt is only ever called with a set CursorItem.
        if (mOGameUi.Inventory.IsContainerOpenIconUnderMouse(pOMousePos))
        {
            // ONE LEVEL UP, as closing goes: from a container opened inside another the item goes
            // into that one, not into the pack (per user, 2026-09-30). It then takes it like a
            // drop onto the closed container, with the same refusals. The rule is
            // UWInventoryView.PutOneLevelUp, shared with the modern original page (cleanup step 5.8).
            UWInventoryView.PutOutcomeEnum leUp = UWInventoryView.PutOneLevelUp(mOInventory.Model);

            if (leUp == UWInventoryView.PutOutcomeEnum.Placed)
                return true;

            if (leUp == UWInventoryView.PutOutcomeEnum.Refused)
            {
                ReportContainerRejection();
                return false;
            }

            // Original wording (confirmed by user): general message 259, ItemFitsSlot's 0x102.
            mOInteraction?.AddGeneralMessage(UWInventoryView.NoPlaceMessage);
            return false;
        }

        // The scroll arrows also work during a held/sticky drag -
        // they only scroll the view and are not a drop target themselves (the item stays stuck
        // to the pointer).
        if (mOGameUi.Inventory.IsContainerScrollUpUnderMouse(pOMousePos))
        {
            mOInventory.ScrollContainer(UWInventoryView.ScrollDeltaOf(UWInventoryView.ControlEnum.ScrollUp));
            return false;
        }

        if (mOGameUi.Inventory.IsContainerScrollDownUnderMouse(pOMousePos))
        {
            mOInventory.ScrollContainer(UWInventoryView.ScrollDeltaOf(UWInventoryView.ControlEnum.ScrollDown));
            return false;
        }

        UWArmorItemMap.BodySlot? leHitZone = mOGameUi.Inventory.GetArmorHitZoneUnderMouse(pOMousePos);

        if (leHitZone.HasValue)
        {
            if (leHitZone.Value == UWArmorItemMap.BodySlot.Helmet && TryEatOnHead())
                return true;

            if (mOInventory.DropCursorItemInEquip(leHitZone.Value))
                return true;

            // The rune bag held there refuses anything but a rune stone with its message.
            ReportContainerRejection();
            return false;
        }

        // Exactly on top of the backpack (see UWItemDrag.fTryStartPending) - only one of the two
        // returns a hit at all, depending on GetBackpackSlotUnderMouse/OpenContainer.
        int liContainerSlot = mOGameUi.Inventory.GetContainerSlotUnderMouse(pOMousePos);

        if (liContainerSlot >= 0)
        {
            if (mOInventory.DropCursorItemInContainer(liContainerSlot))
                return true;

            ReportContainerRejection();
            return false;
        }

        int liBackpackSlot = mOGameUi.Inventory.GetBackpackSlotUnderMouse(pOMousePos);

        if (liBackpackSlot >= 0)
        {
            // If the drop attempt hits a backpack slot that itself contains a container,
            // the item goes into its contents instead of displacing the container
            // (confirmed by user) - the container does not have to be opened for this.
            // UWInventoryView.DropOnBackpackSlot, the rule the modern bags take too (cleanup 5.8).
            UWInventoryView.PutOutcomeEnum leDrop = UWInventoryView.DropOnBackpackSlot(mOInventory.Model, liBackpackSlot);

            if (leDrop == UWInventoryView.PutOutcomeEnum.Refused)
                ReportContainerRejection();

            return leDrop == UWInventoryView.PutOutcomeEnum.Placed;
        }

        // In a conversation the parchment is there, not the world - nothing falls behind it.
        if (mOControlScheme != null && mOControlScheme.IsConversationOpen)
            return false;

        if (mOGameUi.ClassicScreen.IsScreenPositionInGameArea(pOMousePos))
            return fTryDropOrThrowInWorld(pOMousePos);

        return false;
    }

    /// <summary>Original: release/sticky right click in the 3D view window drops or throws the
    /// item - see UWPlayerThrow, which follows DropOrThrowByPlayer_seg025_355 of UW.EXE. A failed
    /// drop does NOT cancel the drag (see TryPlaceAt), the item stays at the pointer.
    /// UWItemDrag sits on Camera.main, so its transform is the player camera itself.</summary>
    private bool fTryDropOrThrowInWorld(Vector2 pOMousePos)
    {
        if (mOLevelLoader == null)
            mOLevelLoader = UWScene.LevelLoader;

        return UWPlayerThrow.TryDropOrThrow(pOMousePos, mOCamera, mOInventory, mOLevelLoader, mOGameUi,
            mOInteraction);
    }

    private UWLevelLoader mOLevelLoader;

    /// <summary>
    /// WHAT LANDS ON THE HEAD IS EATEN. In the original the routine that decides about a drop
    /// on a paperdoll slot calls Eat for slot 0 (MaybeContainerLogic_ovr121_B4B). The user
    /// checked it on 2026-09-21: leeches give "You manage to finish eating the leeches.
    /// Barely.", a potion "You quaff the potion in one gulp.", a wine bottle "You are unable to
    /// open the wine bottle."
    ///
    /// IT IS NOT THE ORDINARY USE PATH, which was the first reading and was wrong: USED, the
    /// leeches bite and suck the poison out, and the user confirmed that this effect stays away
    /// when they go on the head - they are simply eaten. So the drop takes UWItemUse.TryEat.
    ///
    /// A HELMET still goes onto the head: TryEat says no to it, and then the drop continues as
    /// before into the slot.
    /// </summary>
    public bool TryEatOnHead()
    {
        UWObject lOItem = mOInventory != null ? mOInventory.CursorItem : null;

        if (lOItem == null)
            return false;

        UWCharacter lOCharacter = UWScene.Character;

        // Eaten off the CURSOR (UWItemUseHost.ConsumptionEnum.FromCursor).
        if (!UWItemUse.TryEat(lOItem, mOServices.Data,
            lOCharacter != null ? lOCharacter.Vitals : null,
            new UWItemUseHost(mOServices, lOItem, UWItemUseHost.ConsumptionEnum.FromCursor, UWDragSource.None)))
            return false;

        // Whatever is left of it stays on the cursor - a stack that gave up one piece, a
        // bottle that is now empty. Only what the rules consumed is gone already.
        return true;
    }

    /// <summary>Says why a container refused the item - the sentences are
    /// UWItemMessages.ContainerRejection, shared with the modern bags.</summary>
    public void ReportContainerRejection()
    {
        if (mOInteraction == null || mOInventory == null)
            return;

        string lsMessage = UWItemMessages.ContainerRejection(mOInventory.LastContainerRejection,
            mOInventory.LastContainerRejectionContainer, mOServices.Data);

        if (lsMessage != null)
            mOInteraction.AddMessage(lsMessage);
    }
}
