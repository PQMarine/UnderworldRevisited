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

        Vector3 lOSpot = pOViewer.position + (lOForward.normalized * UWSilverTreeRules.PlantDistanceTiles * UWLevelMeshBuilder.TileSpacing);
        UWTilePos lOTile = pOLoader.WorldPositionToTile(lOSpot);

        if (!UWSilverTreeRules.CanTakeRoot(pOLoader.CurrentLevel, lOTile)
            || !pOLoader.SpawnObjectById(TreeObjectId, lOTile.X, lOTile.Y, 0, -1,
                new Vector3(lOSpot.x, pOLoader.GetFloorHeightAt(lOSpot), lOSpot.z)))
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

        if (lOSeed != null && pOInventory != null)
        {
            // THE MODERN SCHEME puts what it picks up into the bags (Interaction.fTryPickUp), the
            // seed as well - it stayed on the pointer (per user, 2026-10-07). Only without room it
            // goes onto the pointer, as in the original.
            UWControlScheme lOScheme = UWScene.ControlScheme;
            bool lbModern = lOScheme != null && lOScheme.Current == UWControlScheme.SchemeEnum.Modern;

            if (lbModern && pOInventory.TryStoreAnywhere(lOSeed))
                return true;

            if (pOInventory.CursorItem == null)
                pOInventory.BeginDragFromExternal(lOSeed);
            else
                pOInventory.TryAddToBackpack(lOSeed);
        }

        return true;
    }

    private static UWObject fCreateSeed(UWLevelLoader pOLoader)
    {
        UWDataImport.DataImport lOData = pOLoader.UWDataImporter;

        if (lOData == null)
            return null;

        UWObject lOSeed = new UWObject((ushort)SeedObjectId);

        // A freshly created object (UW.EXE PrepareNewObjectProps): quality 0x28, quantity 1
        // if its quantity class asks for it.
        lOSeed.Quality = 0x28;

        UWCommonObjectProperties.Entry lOEntry;

        if (lOData.CommonObjectProperties != null
            && lOData.CommonObjectProperties.TryGet(SeedObjectId, out lOEntry) && lOEntry.StartsWithQuantity)
        {
            lOSeed.HasQuantity = true;
            lOSeed.Quantity = 1;
        }

        try
        {
            lOSeed.Texture = lOData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, SeedObjectId);
        }
        catch
        {
            return null;
        }

        lOSeed.WasSpawned = true;

        return lOSeed;
    }

    private static void fMessage(Interaction pOInteraction, int piMessage)
    {
        if (pOInteraction != null)
            pOInteraction.AddGeneralMessage(piMessage);
    }
}
