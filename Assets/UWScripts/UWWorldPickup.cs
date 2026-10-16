using UWDataImport.UWData;

/// <summary>
/// TAKING A THING FROM THE WORLD - the one path behind the classic drag (UWItemDrag), the
/// modern E and the free pointer's drag (Interaction) and the world menu's Pick up
/// (UWModernBags) (cleanup step 2.3, 2026-10-12; until then Interaction.TakeOntoPointer,
/// Interaction.fTryPickUp and UWItemDrag.fTakeFromSource each carried the reach, the weight,
/// the creature test and the theft-moonstone-removal sequence, the last one three times).
///
/// THE ORDER is the classic drag's, which follows the original's player_3dget (ui/INTERACT.C):
/// the checks of UWPickupRules (creature, reach, the weight of what is taken), then the theft
/// (player_grabbed), the moonstone (the player's moonstone field) and the removal from the
/// world (release_3d, which fires the pick-up trigger afterwards - UWInventory.RemoveWorldObject).
/// The modern scheme puts the thing into the pack instead of onto the pointer; a pack without
/// room keeps it in the world before anyone is angered.
/// </summary>
public static class UWWorldPickup
{
    /// <summary>Where the thing goes.</summary>
    public enum Destination
    {
        /// <summary>Onto the pointer (the classic drag, the free pointer, the world menu).</summary>
        Pointer,

        /// <summary>Into the pack, onto a stack or into a bag that takes it (the modern E;
        /// UWInventoryModel.TryStoreAnywhere).</summary>
        Pack
    }

    /// <summary>The count that takes a stack whole.</summary>
    public const int WholeStack = 0;

    /// <summary>
    /// Takes piCount pieces (WholeStack for all) of a thing in the world to peDestination. True
    /// when it was taken; a refusal prints its message. pbReachChecked: the caller measured the
    /// reach already - the classic drag does it at the press, before the count prompt, as the
    /// original asks InPickRange before AskHowMany.
    /// </summary>
    public static bool Take(Interaction pOInteraction, UWEntityInfo pOEntity, int piCount, Destination peDestination,
        bool pbReachChecked = false)
    {
        UWInventory lOInventory = pOInteraction != null ? pOInteraction.Inventory : null;

        if (pOInteraction == null || pOEntity == null || pOEntity.ObjectData == null || !pOEntity.CanBePickedUp
            || lOInventory == null)
            return false;

        if (peDestination == Destination.Pointer && lOInventory.CursorItem != null)
            return false;

        UWObject lOItem = pOEntity.ObjectData;
        bool lbReachOk = pbReachChecked || pOInteraction.CanReachForPickup(pOEntity);
        UWPickupRules.Result lOCheck = UWPickupRules.Check(lOItem, piCount, lbReachOk, lOInventory.Model);

        if (!lOCheck.CanTake)
        {
            if (lOCheck.MessageIndex >= 0)
                pOInteraction.AddGeneralMessage(lOCheck.MessageIndex);

            return false;
        }

        // Onto a stack, the backpack, or into a bag that takes it (per user 2026-10-03: a full
        // backpack used to stop every pickup).
        if (peDestination == Destination.Pack && !lOInventory.TryStoreAnywhere(lOItem))
        {
            pOInteraction.AddGeneralMessage(UWStringIds.NoPlaceToPutMessage);
            return false;
        }

        // PICKING UP IS THE THEFT. The original flags it the moment the object leaves its tile
        // (player_3dget calls player_grabbed before release_3d), not only when it lands in the
        // backpack (per user, 2026-09-16: with the classic drag nobody got angry).
        ReportTheft(pOInteraction, lOItem);

        // Taking the moonstone (or what holds it) from the world: nobody knows where it is any
        // more (UWMoonstoneRules, Pickup_seg024_A9E).
        UWMoonstoneRules.OnTakenFromWorld(lOItem);

        if (peDestination == Destination.Pack)
        {
            lOInventory.RemoveWorldObject(pOEntity);
            return true;
        }

        // Part of a stack is only counted off, the stack stays where it is.
        if (piCount > 0 && piCount < UWStackRules.CountAtLeastOne(lOItem) && lOInventory.TakeFromStack(lOItem, piCount))
            return true;

        lOInventory.BeginDragFromWorld(pOEntity);

        return lOInventory.CursorItem != null;
    }

    /// <summary>
    /// Picking up someone else's property - ovr104_E4C (341516ff), read 2026-09-23.
    ///
    /// WHO OWNS SOMETHING is stored in the item's owner field, six bits, when its COMOBJ entry
    /// allows an owner: a RACE NUMBER in the lower five bits, not a person, and bit 5 that also
    /// wakes the creatures with a locked attitude. Everyone within the area - seven tiles back and
    /// eight forward around the item (UWCritterRules.TheftAreaBack/Forward) - who minds it
    /// and sees it loses goodwill and says so - UWTriggerSystem.AngerRaceAround with
    /// UWCritterRules.MindsTheft, the knights' exception included.
    ///
    /// AFTERWARDS IT BELONGS TO NOBODY - whether anyone saw it or not, but only when owner
    /// &amp; 0x1F is at most 0x1B (UWCritterRules.ClearsOwnerAfterTheft); the top bits of the
    /// byte stay, and above that limit the owner stays too. Until 2026-09-23 ours cleared it
    /// always, did not mind owners above 28 at all, and left the knights' items owned.
    /// </summary>
    public static void ReportTheft(Interaction pOInteraction, UWObject pOItem)
    {
        UWLevelLoader lOLevelLoader = UWScene.LevelLoader;
        UWDataImport.DataImport lOData = lOLevelLoader != null ? lOLevelLoader.UWDataImporter : null;

        if (pOItem == null || lOLevelLoader == null || lOData == null || lOData.CommonObjectProperties == null)
            return;

        UWCommonObjectProperties.Entry lOEntry;

        if (!lOData.CommonObjectProperties.TryGet(pOItem.ID, out lOEntry) || !lOEntry.CanHaveOwner)
            return;

        int liOwner = pOItem.Owner;

        if (liOwner == 0)
            return;

        UWTriggerSystem.AngerRaceAround(liOwner, pOItem,
            new UWTilePos(pOItem.TileX, pOItem.TileY), lOLevelLoader, pOInteraction);

        if (UWCritterRules.ClearsOwnerAfterTheft(liOwner))
            pOItem.Owner = 0;
    }
}
