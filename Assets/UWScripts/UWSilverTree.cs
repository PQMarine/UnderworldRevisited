using UnityEngine;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// The silver seed and the silver tree of Ultima Underworld 1: the Unity side. The rules
/// (where the seed takes root, the stored level, the messages) live in UWSilverTreeRules
/// since P3 of the engine separation; here the tree and the seed are spawned.
/// </summary>
public static class UWSilverTree
{
    public const int SeedObjectId = UWSilverTreeRules.SeedObjectId;

    public const int TreeObjectId = UWSilverTreeRules.TreeObjectId;

    /// <summary>Dungeon level (1-based) of the planted tree, 0 for none - see UWSilverTreeRules.</summary>
    public static int TreeLevel
    {
        get { return UWSilverTreeRules.TreeLevel; }
        set { UWSilverTreeRules.TreeLevel = value; }
    }

    public static bool IsSoil(int piFloorTexture)
    {
        return UWSilverTreeRules.IsSoil(piFloorTexture);
    }

    /// <summary>
    /// Uses a silver seed. Returns true if the seed is used up (planted or vanished); the
    /// caller removes it then. The message is added in any case.
    /// </summary>
    public static bool TryPlant(UWLevelLoader pOLoader, Transform pOViewer, Interaction pOInteraction)
    {
        if (pOLoader == null || pOLoader.CurrentLevel == null || pOViewer == null)
            return false;

        if (pOLoader.CurrentLevelIndex == UWEndgame.VoidLevelIndex)
        {
            fMessage(pOInteraction, UWSilverTreeRules.VanishesMessage);
            return true;
        }

        Vector3 lOForward = pOViewer.forward;
        lOForward.y = 0f;

        if (lOForward.sqrMagnitude < 0.0001f)
            lOForward = Vector3.forward;

        Vector3 lOSpot = pOViewer.position + (lOForward.normalized * UWSilverTreeRules.PlantDistanceTiles * UWWorldScale.TileSize);
        UWTilePos lOTile = pOLoader.WorldPositionToTile(lOSpot);

        // AS plant_seed MAKES IT (read in the decompilation 2026-10-11, after the user compared
        // our SAVE4 with the original's SAVE3): CreateObj's fresh object (UWObjectFactory: quality
        // 0x28, the quantity bit with a quantity of 1 for the tree's stack class), its tenacious
        // bit (word 0 bit 13, our DoorDirection: the spawner reads it as "protected") set, and
        // the animation started on it (the frame in the owner field, UWLevelLoader.fBuildObject).
        // The quantity is named here so that the saved tree stays what SAVE3 showed whatever
        // COMOBJ says.
        // can_place for the tree at the spot in eighths, at the tile's floor, not a flier,
        // range 0 (plant_seed, SKILLS.C) - furniture, boulders, creatures and the player count.
        UWTile lOGround = pOLoader.CurrentLevel.GetTile(lOTile.X, lOTile.Y);
        int liX8 = (lOTile.X << 3) + UWTileQueries.WorldToEighthFloor(lOSpot.x);
        int liY8 = (lOTile.Y << 3) + UWTileQueries.WorldToEighthFloor(lOSpot.z);

        if (!UWSilverTreeRules.CanTakeRoot(pOLoader.CurrentLevel, lOTile)
            || !pOLoader.ObjectFitsAt(TreeObjectId, liX8, liY8, UWSilverTreeRules.PlantZ(lOGround), false, 0)
            || !pOLoader.SpawnObjectById(TreeObjectId, lOTile.X, lOTile.Y, 1, UWObjectFactory.DefaultQuality,
                new Vector3(lOSpot.x, pOLoader.GetFloorHeightAt(lOSpot), lOSpot.z), UWWorldEdit.Arrival.Created,
                pOTree => { pOTree.DoorDirection = true; }))
        {
            fMessage(pOInteraction, UWSilverTreeRules.NoRootsMessage);
            return false;
        }

        TreeLevel = pOLoader.CurrentLevelIndex + 1;

        fMessage(pOInteraction, UWSilverTreeRules.PlantedMessage);

        return true;
    }

    /// <summary>
    /// The player reaches for the tree: it withers, a seed goes to the hand (backpack if the
    /// hand is full) and the stored level is cleared. Returns false if this is not a tree.
    /// </summary>
    public static bool TryTakeTree(UWLevelLoader pOLoader, UWObject pOTree, UWInventory pOInventory, Interaction pOInteraction)
    {
        if (pOLoader == null || pOTree == null || pOTree.ID != TreeObjectId)
            return false;

        if (!pOLoader.RemoveObjectFromWorld(pOTree))
            return false;

        fMessage(pOInteraction, UWSilverTreeRules.WithersMessage);

        TreeLevel = 0;

        UWObject lOSeed = fCreateSeed(pOLoader);

        // THE MODERN SCHEME puts what it picks up into the bags (UWWorldPickup.Take), the
        // seed as well - it stayed on the pointer (per user, 2026-10-07). Only without room it
        // goes onto the pointer, as in the original; else the backpack, else the feet
        // (UWInventoryModel.GiveToPlayer, one behaviour for the four gifts since 2026-10-12;
        // until then a full hand and backpack lost the seed).
        if (lOSeed != null && pOInventory != null)
            pOInventory.GiveToPlayer(lOSeed, UWControlScheme.Ui.Policy.PickupGoesToBags);

        return true;
    }

    private static UWObject fCreateSeed(UWLevelLoader pOLoader)
    {
        UWDataImport.DataImport lOData = pOLoader.UWDataImporter;

        if (lOData == null)
            return null;

        // CreateObj's fresh object with its picture (UWObjectFactory), as place_new makes the
        // seed (UseObj, USEITEMS.C).
        UWObject lOSeed = UWObjectFactory.Create(SeedObjectId, lOData);

        if (lOSeed == null)
            return null;

        lOSeed.WasSpawned = true;

        // THE SEED IS TENACIOUS, as the planted tree is: UseObj (UnderworldExhumed obj/USEITEMS.C,
        // read 2026-10-15) sets word 0 bit 13 (SET_DOORDIR) on the new seed, so no culling and no
        // water or lava takes it when it is dropped (UWObjectLimitRules.IsTenacious). Until
        // 2026-10-15 ours made a plain object (reference audit round 2, A42).
        lOSeed.DoorDirection = true;

        return lOSeed;
    }

    private static void fMessage(Interaction pOInteraction, int piMessage)
    {
        if (pOInteraction != null)
            pOInteraction.AddGeneralMessage(piMessage);
    }
}
