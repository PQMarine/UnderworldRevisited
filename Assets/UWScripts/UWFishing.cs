using UnityEngine;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// The fishing pole (0x12B), used from the pack (per user, 2026-09-18: "Die Angel kann man bei
/// uns noch nicht benutzen"). Rules from the reference (fishingpole.use), UW.EXE not read:
///
///   The spot 11/8 tile ahead in the view direction must lie on a tile that is not solid,
///   whose floor is water in TERRAIN.DAT, and whose floor is at most two height steps above
///   the floor under the player - otherwise "You cannot fish there.  Perhaps somewhere else."
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

    /// <summary>Block 1 in our export numbering.</summary>
    private const int CatchMessage = 100;

    private const int NoLuckMessage = 101;

    private const int CannotFishMessage = 102;

    private const int GetsAwayMessage = 103;

    /// <summary>Distance of the fishing spot: 0x0B eighths of a tile, as for the silver seed.</summary>
    private const float FishingDistanceTiles = 11f / 8f;

    /// <summary>The water may lie at most this many height steps above the floor under the player
    /// (player zpos >> 3 against floor height - 2).</summary>
    private const int MaxStepsAbovePlayer = 2;

    /// <summary>One floor height step in our height units (eight zpos steps).</summary>
    private const float HeightStep = 8f * UWObjectSpawner.HeightScale;

    /// <summary>uw1: a bite when a roll of 0 to 4 gives 0.</summary>
    private const int BiteOneIn = 5;

    /// <summary>Always true - the pole is used even when there is nothing to fish.</summary>
    public static bool TryFish(UWLevelLoader pOLoader, Transform pOViewer, UWInventory pOInventory,
        Interaction pOInteraction)
    {
        if (pOLoader == null || pOLoader.CurrentLevel == null || pOViewer == null)
            return true;

        Vector3 lOForward = pOViewer.forward;
        lOForward.y = 0f;

        if (lOForward.sqrMagnitude < 0.0001f)
            lOForward = Vector3.forward;

        Vector3 lOSpot = pOViewer.position + (lOForward.normalized * FishingDistanceTiles * UWLevelMeshBuilder.TileSpacing);
        UWTilePos lOTile = pOLoader.WorldPositionToTile(lOSpot);

        if (!fIsFishingWater(pOLoader, lOTile, pOViewer.position))
        {
            fMessage(pOInteraction, CannotFishMessage);
            return true;
        }

        if (UWRandom.Next(BiteOneIn) != 0)
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

        // Into the hand (SpawnObjectInHand); if something is already held there, into the backpack.
        if (pOInventory.CursorItem == null)
            pOInventory.BeginDragFromExternal(lOFish);
        else if (!pOInventory.TryAddToBackpack(lOFish))
        {
            fMessage(pOInteraction, GetsAwayMessage);
            return true;
        }

        fMessage(pOInteraction, CatchMessage);

        return true;
    }

    private static bool fIsFishingWater(UWLevelLoader pOLoader, UWTilePos pOTile, Vector3 pOPlayerPosition)
    {
        if (pOTile.X < 0 || pOTile.Y < 0 || pOTile.X >= UWLevelMeshBuilder.TilesPerAxis || pOTile.Y >= UWLevelMeshBuilder.TilesPerAxis)
            return false;

        UWTile lOTile = pOLoader.CurrentLevel.TileData[(pOTile.Y * UWLevelMeshBuilder.TilesPerAxis) + pOTile.X];

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
            return false;

        if (!pOLoader.TileQueries.IsWaterTile(lOTile))
            return false;

        // In whole height steps, as the original compares them.
        int liPlayerSteps = Mathf.FloorToInt(pOLoader.GetFloorHeightAt(pOPlayerPosition) / HeightStep);
        int liWaterSteps = Mathf.FloorToInt(lOTile.FloorHeight / HeightStep);

        return liPlayerSteps >= liWaterSteps - MaxStepsAbovePlayer;
    }

    private static UWObject fCreateFish(UWLevelLoader pOLoader)
    {
        UWDataImport.DataImport lOData = pOLoader.UWDataImporter;

        if (lOData == null)
            return null;

        UWObject lOFish = new UWObject((ushort)FishObjectId);

        // A freshly created object (UW.EXE PrepareNewObjectProps): quality 0x28, quantity 1
        // if its quantity class asks for it.
        lOFish.Quality = 0x28;

        UWCommonObjectProperties.Entry lOEntry;

        if (lOData.CommonObjectProperties != null
            && lOData.CommonObjectProperties.TryGet(FishObjectId, out lOEntry) && lOEntry.StartsWithQuantity)
        {
            lOFish.HasQuantity = true;
            lOFish.Quantity = 1;
        }

        try
        {
            lOFish.Texture = lOData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, FishObjectId);
        }
        catch
        {
            return null;
        }

        lOFish.WasSpawned = true;

        return lOFish;
    }

    private static void fMessage(Interaction pOInteraction, int piMessage)
    {
        if (pOInteraction != null)
            pOInteraction.AddGeneralMessage(piMessage);
    }
}
