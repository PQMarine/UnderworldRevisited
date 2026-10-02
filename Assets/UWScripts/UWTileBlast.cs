using System.Collections.Generic;
using UnityEngine;
using UnderworldRevisited.Build;
using UWDataImport.UWData;

/// <summary>
/// Everything on one tile takes a blow of its own - the explosion of a fireball or lightning
/// bolt and every tile of Sheet Lightning and Flame Wind (UW.EXE: DamageObjectsInTile_seg038_3307_182A,
/// the rules in UWObjectDamageRules).
///
/// THREE KINDS OF THING ON THE TILE, each with its own roll:
/// - CREATURES, and doors and chests with hit points: their UWDamageable takes the damage
///   (resistances and quality class inside), no armour. The creature a missile has just hit is
///   not spared - ours spared it until 2026-09-27, which is why summoned creatures lasted far
///   longer than in the original (per user).
/// - THE PLAYER when he stands on the tile: the game keeps him in his tile's object list
///   (PlacePlayerInTile_seg008_6D1), so he takes the blast like a creature (per user, 2026-09-27:
///   strafing in front of a wall in the original, he took damage now and then when a fireball
///   struck the wall on his tile).
/// - LOOSE OBJECTS - weapons, loot, blood stains, debris: worn down, and at quality 0 they
///   become a pile of debris, with fire sometimes under a cloud of smoke.
///
/// Creatures are found by where they stand, because they walk and the tile lists are not kept
/// up with them; everything else by the tile's object list.
/// </summary>
public static class UWTileBlast
{
    /// <summary>
    /// Strikes the tile.
    /// </summary>
    /// <param name="piAttacker">Whom the struck creatures should hold responsible: a creature's
    /// index, UWCritterBrain.PlayerIndex, or -1 for nobody.</param>
    /// <param name="pOReportTo">The player's own blast reports its hits (gargoyle eyes); null
    /// for a creature's.</param>
    public static void Strike(UWLevelLoader pOLoader, UWTilePos pOTile, int piDiceCount, int piDiceRange,
        int piDamageType, int piAttacker, Interaction pOReportTo)
    {
        if (pOLoader == null || pOLoader.CurrentLevel == null)
            return;

        UWTile lOTile = pOLoader.CurrentLevel.GetTile(pOTile.X, pOTile.Y);

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
            return;

        HashSet<UWDamageable> lODone = new HashSet<UWDamageable>();

        // WHAT LIES THERE NOW: a creature this blast kills drops its remains at once in ours,
        // while the original lets it die over its death animation first - they must not burn
        // in the same blast.
        UWObject[] lOLying = lOTile.ObjectsInTile != null ? lOTile.ObjectsInTile.ToArray() : new UWObject[0];

        fStrikeCreatures(pOLoader, pOTile, lOTile, piDiceCount, piDiceRange, piDamageType, piAttacker,
            pOReportTo, lODone);
        fStrikeObjects(pOLoader, pOTile, lOTile, lOLying, piDiceCount, piDiceRange, piDamageType, pOReportTo, lODone);
        fStrikePlayer(pOLoader, pOTile, piDiceCount, piDiceRange, piDamageType);
    }

    private static void fStrikeCreatures(UWLevelLoader pOLoader, UWTilePos pOTile, UWTile pOTileData,
        int piDiceCount, int piDiceRange, int piDamageType, int piAttacker, Interaction pOReportTo,
        HashSet<UWDamageable> pODone)
    {
        Vector3 lOCentre = new Vector3(pOTile.X * UWLevelMeshBuilder.TileSpacing, pOTileData.FloorHeight,
            pOTile.Y * UWLevelMeshBuilder.TileSpacing);

        // Half the diagonal of a tile and a little more, so nothing at the edge slips through;
        // the tile number then decides.
        Collider[] lOHits = Physics.OverlapSphere(lOCentre, UWLevelMeshBuilder.TileSpacing * 0.75f);

        foreach (Collider lOCollider in lOHits)
        {
            UWCritter lOCritter = lOCollider.GetComponentInParent<UWCritter>();
            UWDamageable lODamageable = lOCritter != null ? lOCritter.GetComponent<UWDamageable>() : null;

            if (lODamageable == null || lODamageable.IsDestroyed || !pODone.Add(lODamageable))
                continue;

            if (pOLoader.WorldPositionToTile(lODamageable.transform.position) != pOTile)
                continue;

            if (piAttacker >= 0 && piAttacker != lOCritter.Index)
                lOCritter.AnnounceAttacker(piAttacker);

            lODamageable.ApplyDamage(UWRandom.RollDice(piDiceCount, piDiceRange), piDamageType);

            if (pOReportTo != null)
                pOReportTo.ReportSpellHit(lOCollider.GetComponentInParent<UWEntityInfo>(), lODamageable,
                    lOCollider.bounds.center);
        }
    }

    private static void fStrikeObjects(UWLevelLoader pOLoader, UWTilePos pOTile, UWTile pOTileData, UWObject[] pOLying,
        int piDiceCount, int piDiceRange, int piDamageType, Interaction pOReportTo, HashSet<UWDamageable> pODone)
    {
        if (pOTileData.ObjectsInTile == null || pOLying.Length == 0)
            return;

        UWCommonObjectProperties lOCommon = pOLoader.UWDataImporter != null
            ? pOLoader.UWDataImporter.CommonObjectProperties : null;

        // Through the snapshot: a destroyed object leaves the list and debris joins it.
        foreach (UWObject lOObject in pOLying)
        {
            if (lOObject == null || lOObject is UWNpc || !pOTileData.ObjectsInTile.Contains(lOObject))
                continue;

            pOLoader.TryGetEntity(lOObject, out UWEntityInfo lOEntity);

            UWDamageable lODamageable = lOEntity != null ? lOEntity.GetComponentInParent<UWDamageable>() : null;

            // WORD 0 BIT 13 PROTECTS (DamageObjectAndDoors) - a door too, whatever its hit points.
            if (lOObject.DoorDirection)
                continue;

            int liRoll = UWRandom.RollDice(piDiceCount, piDiceRange);

            // A DOOR OR A CHEST has hit points and its own ending (UWDoorDamage, UWContainerDamage).
            if (lODamageable != null)
            {
                if (lODamageable.IsDestroyed || !pODone.Add(lODamageable))
                    continue;

                lODamageable.ApplyDamage(liRoll, piDamageType);

                if (pOReportTo != null)
                    pOReportTo.ReportSpellHit(lOEntity, null, lOEntity.transform.position);

                continue;
            }

            UWCommonObjectProperties.Entry lOEntry = default;

            if (lOCommon == null || !lOCommon.TryGet(lOObject.ID, out lOEntry))
                continue;

            int liDamage = UWDamageTypes.Scale(lOEntry.Resistances, liRoll, piDamageType);

            if (!UWObjectDamageRules.Wear(lOObject.Quality, liDamage, lOEntry.QualityClass, false,
                    out int liQuality))
            {
                lOObject.Quality = (ushort)liQuality;

                continue;
            }

            lOObject.Quality = 0;

            fDestroy(pOLoader, pOTile, lOObject, lOEntity, piDamageType);
        }
    }

    /// <summary>What DamageObject_Debris does with a destroyed loose object.</summary>
    private static void fDestroy(UWLevelLoader pOLoader, UWTilePos pOTile, UWObject pOObject,
        UWEntityInfo pOEntity, int piDamageType)
    {
        Vector3 lOPosition = pOEntity != null
            ? pOEntity.transform.position
            : new Vector3(pOTile.X * UWLevelMeshBuilder.TileSpacing, 0f, pOTile.Y * UWLevelMeshBuilder.TileSpacing);

        // A BAG OR BOX lets its contents fall onto the tile first.
        if (pOEntity != null && UWObjectDamageRules.SpillsWhenDestroyed(pOObject.ID) && UWScene.Interaction != null)
            UWScene.Interaction.SpillWorldContainer(pOEntity, true, false);

        UWObjectDamageRules.Remains lORemains = UWObjectDamageRules.DecideRemains(pOObject.ID, piDamageType);

        // THE SMOKE STANDS WHERE THE OBJECT LAY: SpawnClass7Object copies its sub-position and,
        // with height factor 0, its zpos. Placed like any animated object there - the object's
        // data position plus the animated objects' height correction. It sat a little too high
        // on the entity's own position (per user, 2026-09-27).
        if (lORemains == UWObjectDamageRules.Remains.DebrisWithSmoke)
            pOLoader.SpawnEffectAt(UWObjectDamageRules.SmokeObjectId,
                UWObjectSpawner.GetObjectWorldPosition(pOTile.X, pOTile.Y, pOObject)
                    + new Vector3(0f, UWObjectSpawner.AnimatedHeightOffset, 0f),
                UWObjectDamageRules.RollSmokeSeconds());

        if (!pOLoader.RemoveObjectFromWorld(pOObject) && pOEntity != null)
        {
            pOLoader.ForgetObjectData(pOObject);
            Object.Destroy(pOEntity.gameObject);
        }

        if (lORemains == UWObjectDamageRules.Remains.Gone)
            return;

        // The object BECOMES the debris in the original, quality 0, contents gone; ours puts a
        // new pile on the same spot, as a smashed chest does (UWContainerDamage).
        pOLoader.SpawnObjectById(UWObjectDamageRules.RollDebrisObjectId(), pOTile.X, pOTile.Y, 0, 0,
            new Vector3(lOPosition.x, 0f, lOPosition.z));
    }

    private static void fStrikePlayer(UWLevelLoader pOLoader, UWTilePos pOTile, int piDiceCount, int piDiceRange,
        int piDamageType)
    {
        UWCharacter lOPlayer = UWScene.Character;

        if (lOPlayer == null || pOLoader.WorldPositionToTile(lOPlayer.transform.position) != pOTile)
            return;

        lOPlayer.ApplyDamage(UWRandom.RollDice(piDiceCount, piDiceRange), piDamageType);
    }
}
