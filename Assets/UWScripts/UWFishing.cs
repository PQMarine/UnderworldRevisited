using UnityEngine;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// The fishing pole (0x12B), used from the pack (per user, 2026-09-18: "Die Angel kann man bei
/// uns noch nicht benutzen"). Rules from the reference (fishingpole.use), checked against go_fish
/// (UnderworldExhumed event/WORLDEV.C) on 2026-10-15:
///
///   The spot 11/8 tile ahead in the view direction must lie on a tile that is not solid,
///   whose floor is water in TERRAIN.DAT, and whose floor is not above the floor under the
///   player (UWFishingRules; until 2026-10-15 up to two steps above, the reference's rule) -
///   otherwise "You cannot fish there.  Perhaps somewhere else."
///   Then a bite with a chance of one in five (the reference notes that uw1 rolls 0 to 4 and
///   bites on 0; it uses the uw2 rule with the tracking skill itself). On a bite the fish (0xB6)
///   goes to the hand with "You catch a lovely fish." if the player can carry it, otherwise
///   "You feel a nibble, but the fish gets away."; without a bite "No luck this time."
///
/// The pole is never used up. From the ground it does nothing (the reference returns early).
/// </summary>
public static class UWFishing
{
    public const int FishingPoleObjectId = UWItemUse.FishingPoleObjectId;

    private const int FishObjectId = 0xB6;

    /// <summary>What UseMagic writes onto the caught fish after place_new: 0x3F (USEITEMS.C).</summary>
    private const int CaughtFishQuality = UWObjectIds.MaxQuality;

    /// <summary>Block 1 in our export numbering.</summary>
    private const int CatchMessage = 100;

    private const int NoLuckMessage = 101;

    private const int CannotFishMessage = 102;

    private const int GetsAwayMessage = 103;

    /// <summary>Distance of the fishing spot: 0x0B eighths of a tile, as for the silver seed.</summary>
    private const float FishingDistanceTiles = 11f / 8f;

    /// <summary>One floor height step in our height units (eight zpos steps).</summary>
    private const float HeightStep = 8f * UWWorldScale.ZPosStep;

    /// <summary>Always true - the pole is used even when there is nothing to fish.</summary>
    public static bool TryFish(UWLevelLoader pOLoader, Transform pOViewer, UWInventory pOInventory,
        Interaction pOInteraction, int piTrackSkill)
    {
        if (pOLoader == null || pOLoader.CurrentLevel == null || pOViewer == null)
            return true;

        Vector3 lOForward = pOViewer.forward;
        lOForward.y = 0f;

        if (lOForward.sqrMagnitude < 0.0001f)
            lOForward = Vector3.forward;

        Vector3 lOSpot = pOViewer.position + (lOForward.normalized * FishingDistanceTiles * UWWorldScale.TileSize);
        UWTilePos lOTile = pOLoader.WorldPositionToTile(lOSpot);

        if (!fIsFishingWater(pOLoader, lOTile, pOViewer.position))
        {
            fMessage(pOInteraction, CannotFishMessage);
            return true;
        }

        // The bite is the game's (IUWTrapVariant; the first game: one in five).
        if (!UWGameSession.Current.Game.Traps.Bites(piTrackSkill))
        {
            fMessage(pOInteraction, NoLuckMessage);
            return true;
        }

        UWObject lOFish = fCreateFish(pOLoader);

        if (lOFish == null || pOInventory == null || !pOInventory.CanCarry(lOFish))
        {
            fMessage(pOInteraction, GetsAwayMessage);
            return true;
        }

        // The pointer, as place_new puts it; else the backpack, else the feet
        // (UWInventoryModel.GiveToPlayer, one behaviour for the four gifts since 2026-10-12;
        // until then a full backpack let the fish get away a second time).
        pOInventory.GiveToPlayer(lOFish, false);

        fMessage(pOInteraction, CatchMessage);

        return true;
    }

    private static bool fIsFishingWater(UWLevelLoader pOLoader, UWTilePos pOTile, Vector3 pOPlayerPosition)
    {
        UWTile lOTile = pOLoader.CurrentLevel.GetTile(pOTile.X, pOTile.Y);

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
            return false;

        if (!pOLoader.TileQueries.IsWaterTile(lOTile))
            return false;

        // In whole height steps, as the original compares them; the water may not lie above
        // the player (UWFishingRules, go_fish, 2026-10-15).
        int liPlayerSteps = Mathf.FloorToInt(pOLoader.GetFloorHeightAt(pOPlayerPosition) / HeightStep);
        int liWaterSteps = Mathf.FloorToInt(lOTile.FloorHeight / HeightStep);

        return UWFishingRules.WaterLowEnough(liPlayerSteps, liWaterSteps);
    }

    private static UWObject fCreateFish(UWLevelLoader pOLoader)
    {
        UWDataImport.DataImport lOData = pOLoader.UWDataImporter;

        if (lOData == null)
            return null;

        // CreateObj's fresh object with its picture (UWObjectFactory), and then the catch's
        // own quality: UseMagic sets 0x3F on the fish place_new made (UnderworldExhumed
        // USEITEMS.C, read 2026-10-12). Until 2026-10-12 it kept CreateObj's 40.
        UWObject lOFish = UWObjectFactory.Create(FishObjectId, lOData, UWObjectFactory.Unset, CaughtFishQuality);

        if (lOFish == null)
            return null;

        lOFish.WasSpawned = true;

        return lOFish;
    }

    private static void fMessage(Interaction pOInteraction, int piMessage)
    {
        if (pOInteraction != null)
            pOInteraction.AddGeneralMessage(piMessage);
    }
}
