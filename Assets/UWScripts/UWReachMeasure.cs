using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE HAND'S REACH, MEASURED FOR BOTH GAMES (InPickRange and BlockingTerrain, uw1 and uw2 ui/INTERACT.C; the rules are
/// UWReachRules): the player and the thing on the tile grid, the distance and height window first, then the terrain
/// between. One home since 2026-11-26 (second cleanup plan, duplication audit G10): the first game measured in
/// Interaction.fReach, the second took and aimed with the range alone and used along a ray of fixed length, without the
/// terrain between, so it reached over water and up walls the original refuses.
///
/// The level is read here for the terrain test: a tile's floor height, its terrain class (0 normal, 1 water, 2 lava;
/// the second game's TERRAIN(t) is its floor texture's class in TERRAIN.DAT, uw2 ui/INTERACT.C:46) and the highest
/// bridge's z.
/// </summary>
public static class UWReachMeasure
{
    public enum Result
    {
        Reached,
        OutOfRange,
        Blocked
    }

    /// <summary>The player's feet (UWPlayerThrow.GetFeet) to the thing's grid spot. piSwimLowering: the second game's
    /// swimmer reaches lower (UWReachRules.SwimLowering); 0 in the first.</summary>
    public static Result Measure(UWLevelLoader pOLoader, Vector3 pOFeet, int piTargetX, int piTargetY, int piTargetZ,
        int piDistanceSquared, bool pbUsingPole, int piSwimLowering, bool pbTerrain = true)
    {
        return Measure(pOLoader, UWTileQueries.WorldToEighths(pOFeet.x), UWTileQueries.WorldToEighths(pOFeet.z),
            UWUnits.WorldToZPos(pOFeet.y), piTargetX, piTargetY, piTargetZ, piDistanceSquared, pbUsingPole, piSwimLowering, pbTerrain);
    }

    /// <summary>Positions in eighths of a tile, heights in zpos steps.</summary>
    public static Result Measure(UWLevelLoader pOLoader, int piPlayerX, int piPlayerY, int piPlayerZ,
        int piTargetX, int piTargetY, int piTargetZ, int piDistanceSquared, bool pbUsingPole, int piSwimLowering, bool pbTerrain = true)
    {
        // The swimmer's lowering moves the height window only (dz -= swim_count >> 3), not the line the terrain is
        // walked on.
        if (!UWReachRules.CanReach(piPlayerX, piPlayerY, piPlayerZ - piSwimLowering, piTargetX, piTargetY, piTargetZ,
            piDistanceSquared, pbUsingPole))
            return Result.OutOfRange;

        if (!pbTerrain)
            return Result.Reached;

        return IsBlockedByTerrain(pOLoader, piPlayerX, piPlayerY, piPlayerZ, piTargetX, piTargetY, piTargetZ,
            piDistanceSquared, pbUsingPole) ? Result.Blocked : Result.Reached;
    }

    /// <summary>BlockingTerrain over the loader's level; false without one.</summary>
    public static bool IsBlockedByTerrain(UWLevelLoader pOLoader, int piPlayerX, int piPlayerY, int piPlayerZ,
        int piTargetX, int piTargetY, int piTargetZ, int piDistanceSquared, bool pbUsingPole)
    {
        UWLevel lOLevel = pOLoader != null ? pOLoader.CurrentLevel : null;

        if (lOLevel == null)
            return false;

        UWTerrain lOTerrain = pOLoader.UWDataImporter != null ? pOLoader.UWDataImporter.Terrain : null;

        return UWReachRules.IsBlockedByTerrain(piPlayerX >> 3, piPlayerY >> 3, piPlayerZ,
            piTargetX >> 3, piTargetY >> 3, piTargetZ, piDistanceSquared, pbUsingPole,
            (piX, piY) =>
            {
                UWTile lOTile = lOLevel.GetTile(piX, piY);

                return lOTile != null ? (lOTile.FloorHeight >> 4) & 0xF : 0;
            },
            (piX, piY) => TerrainClassAt(lOTerrain, lOLevel.GetTile(piX, piY)),
            (piX, piY) =>
            {
                UWTile lOTile = lOLevel.GetTile(piX, piY);
                int liBridge = -1;

                if (lOTile != null && lOTile.ObjectsInTile != null)
                {
                    foreach (UWObject lOObject in lOTile.ObjectsInTile)
                    {
                        if (lOObject != null && lOObject.ID == UWObjectIds.Bridge && lOObject.ZPos > liBridge)
                            liBridge = lOObject.ZPos;
                    }
                }

                return liBridge;
            });
    }

    /// <summary>A tile's terrain class for the reach: 0 normal, 1 water, 2 lava (3 ice in the second game) - the second
    /// game's floor texture class in TERRAIN.DAT, the first game's floor terrain's high nibble.</summary>
    public static int TerrainClassAt(UWTerrain pOTerrain, UWTile pOTile)
    {
        if (pOTile == null || pOTerrain == null)
            return 0;

        if (pOTerrain.Uw2 != null)
            return pOTerrain.Uw2.Class(pOTile.TextureFloor);

        return ((int)pOTerrain.GetFloorTerrain(pOTile.TextureFloor) & 0xFF) >> 4;
    }

    /// <summary>
    /// WHERE THE THING IS, as InPickRange measures it: a creature where it walks (its node), anything else by its record
    /// - tile, fine place and height (a wall decal stands on the wall in the world, but the original reckons with the
    /// spot it is stored on; a door's node stands at its hinge, so its record is the measure too). Something without
    /// data by its position.
    /// </summary>
    public static void TargetGrid(UWEntityInfo pOTarget, out int piX, out int piY, out int piZ)
    {
        UWObject lOData = pOTarget != null ? pOTarget.ObjectData : null;

        if (lOData != null && UWObjectIds.Major(lOData.ID) != UWObjectIds.CreatureMajorClass)
        {
            TargetGrid(lOData, out piX, out piY, out piZ);

            return;
        }

        Vector3 lOAt = pOTarget != null ? pOTarget.transform.position : Vector3.zero;

        piX = UWTileQueries.WorldToEighths(lOAt.x);
        piY = UWTileQueries.WorldToEighths(lOAt.z);
        piZ = UWUnits.WorldToZPos(lOAt.y);
    }

    /// <summary>A thing by its record: tile, fine place and height.</summary>
    public static void TargetGrid(UWObject pOData, out int piX, out int piY, out int piZ)
    {
        piX = UWReachRules.ToEighths(pOData.TileX, pOData.XPos);
        piY = UWReachRules.ToEighths(pOData.TileY, pOData.YPos);
        piZ = pOData.ZPos;
    }

    /// <summary>The second game's swimmer reaches lower (InPickRange, uw2 ui/INTERACT.C:154-155): the swim counter's eighth
    /// once it passes 0x50. The first game has no such term.</summary>
    public static int SwimLoweringUw2()
    {
        UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

        return lOTerrain != null ? UWReachRules.SwimLowering(lOTerrain.SwimCounter) : 0;
    }
}
