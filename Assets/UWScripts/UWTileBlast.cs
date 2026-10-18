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
/// NO FLASH, NO BLOOD, NO EYES: damage_square (UnderworldExhumed SPELLS.C) hands each object
/// straight to damage_item; the hit effects and the gargoyle eyes belong to do_damage (COMBAT.C),
/// a blow's or a missile's direct hit. Until 2026-10-13 ours showed the hit effect on everything
/// the player's blast struck, at the object's own position (per user: a flash on the nightstand
/// whatever the fireball's height), and it took the effect slot the explosion needed.
///
/// Creatures are found by where they stand, because they walk and the tile lists are not kept
/// up with them; everything else by the tile's object list.
///
/// THE BLOWS LAND THROUGH THE WORLD since cleanup step 3.3 (2026-10-17): the creatures, the doors
/// and chests and the player each take a UWBlow.Blast through IUWWorld.Damage. The wear of the
/// loose objects without hit points stays here - no other site wears them (a missile or a blow
/// leaves a loose object alone).
/// </summary>
public static class UWTileBlast
{
    /// <summary>
    /// Strikes the tile.
    /// </summary>
    /// <param name="piAttacker">Whom the struck creatures should hold responsible: a creature's
    /// index, UWCritterBrain.PlayerIndex, or UWBlow.NoBlastAttacker (-1) for nobody.</param>
    public static void Strike(UWLevelLoader pOLoader, UWTilePos pOTile, int piDiceCount, int piDiceRange,
        int piDamageType, int piAttacker)
    {
        if (pOLoader == null || pOLoader.CurrentLevel == null)
            return;

        UWTile lOTile = pOLoader.CurrentLevel.GetTile(pOTile.X, pOTile.Y);

        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
            return;

        HashSet<UWDamageable> lODone = new HashSet<UWDamageable>();
        UWWorld lOWorld = UWWorld.Of(pOLoader);

        // WHAT LIES THERE NOW: a creature this blast kills drops its remains at once in ours,
        // while the original lets it die over its death animation first - they must not burn
        // in the same blast.
        UWObject[] lOLying = lOTile.ObjectsInTile != null ? lOTile.ObjectsInTile.ToArray() : new UWObject[0];

        fStrikeCreatures(lOWorld, pOLoader, pOTile, lOTile, piDiceCount, piDiceRange, piDamageType, piAttacker, lODone);
        fStrikeObjects(lOWorld, pOLoader, pOTile, lOTile, lOLying, piDiceCount, piDiceRange, piDamageType, piAttacker, lODone);
        fStrikePlayer(lOWorld, pOLoader, pOTile, piDiceCount, piDiceRange, piDamageType, piAttacker);
    }

    private static void fStrikeCreatures(UWWorld pOWorld, UWLevelLoader pOLoader, UWTilePos pOTile, UWTile pOTileData,
        int piDiceCount, int piDiceRange, int piDamageType, int piAttacker, HashSet<UWDamageable> pODone)
    {
        Vector3 lOCentre = new Vector3(UWUnits.TileCentreToWorldAxis(pOTile.X), pOTileData.FloorHeight,
            UWUnits.TileCentreToWorldAxis(pOTile.Y));

        // Half the diagonal of a tile and a little more, so nothing at the edge slips through;
        // the tile number then decides.
        Collider[] lOHits = Physics.OverlapSphere(lOCentre, UWWorldScale.TileSize * 0.75f);

        foreach (Collider lOCollider in lOHits)
        {
            UWCritter lOCritter = lOCollider.GetComponentInParent<UWCritter>();
            UWDamageable lODamageable = lOCritter != null ? lOCritter.GetComponent<UWDamageable>() : null;

            if (lODamageable == null || lODamageable.IsDestroyed || !pODone.Add(lODamageable))
                continue;

            if (pOLoader.WorldPositionToTile(lODamageable.transform.position) != pOTile)
                continue;

            // The attacker is told unless there is none or it is the creature itself (UWBlow.Blast).
            // The roll comes before the announcement now; the announcement draws nothing.
            pOWorld.Damage(UWWorld.TargetOf(lOCritter, lODamageable),
                UWBlow.Blast(UWRandom.RollDice(piDiceCount, piDiceRange), piDamageType, piAttacker, lOCritter.Index));
        }
    }

    private static void fStrikeObjects(UWWorld pOWorld, UWLevelLoader pOLoader, UWTilePos pOTile, UWTile pOTileData,
        UWObject[] pOLying, int piDiceCount, int piDiceRange, int piDamageType, int piAttacker, HashSet<UWDamageable> pODone)
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

            // A DOOR OR A CHEST has hit points and its own ending (UWDoor, UWContainerDamage).
            if (lODamageable != null)
            {
                if (lODamageable.IsDestroyed || !pODone.Add(lODamageable))
                    continue;

                pOWorld.Damage(UWWorld.TargetOf(lODamageable),
                    UWBlow.Blast(liRoll, piDamageType, piAttacker, UWBlow.NoCreature));

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

    /// <summary>What DamageObject_Debris (remove_object) does with a destroyed loose object -
    /// the rules in UWObjectDamageRules, the remove and the pile in UWDebris.</summary>
    private static void fDestroy(UWLevelLoader pOLoader, UWTilePos pOTile, UWObject pOObject,
        UWEntityInfo pOEntity, int piDamageType)
    {
        Vector3 lOPosition = pOEntity != null
            ? pOEntity.transform.position
            : new Vector3(UWUnits.TileCentreToWorldAxis(pOTile.X), 0f, UWUnits.TileCentreToWorldAxis(pOTile.Y));

        UWObjectDamageRules.Remains lORemains;

        if (UWObjectDamageRules.SpillsWhenDestroyed(pOObject.ID))
        {
            // A BAG OR BOX the culling test spares is LEFT AS IT IS, worn to quality 0 with its
            // contents (remove_object, DAMAGE.C: Obj_Elem_Fate 10; read 2026-10-12). Until then
            // ours spilled every destroyed bag.
            if (UWObjectDamageRules.DestroyedBagIsSpared(pOObject,
                    pOLoader.UWDataImporter != null ? pOLoader.UWDataImporter.CommonObjectProperties : null,
                    pOLoader.CurrentLevel.Masterlist))
                return;

            // Otherwise its contents fall onto the tile first, and NO SMOKE FOR A CONTAINER:
            // the smoke roll sits in the branch of the things that are no door, chest, barrel
            // or bag (per user on the original 2026-10-12, a smashed chest never smokes).
            if (pOEntity != null && UWScene.Interaction != null)
                UWScene.Interaction.SpillWorldContainer(pOEntity, true, false, true);

            lORemains = UWObjectDamageRules.Remains.Debris;
        }
        else
            lORemains = UWObjectDamageRules.DecideRemains(pOObject.ID, piDamageType);

        // THE SMOKE STANDS WHERE THE OBJECT LAY: SpawnClass7Object copies its sub-position and,
        // with height factor 0, its zpos. Placed like any animated object there - the object's
        // data position plus the animated objects' height correction. It sat a little too high
        // on the entity's own position (per user, 2026-09-27).
        if (lORemains == UWObjectDamageRules.Remains.DebrisWithSmoke)
            pOLoader.SpawnEffectAt(UWObjectDamageRules.SmokeObjectId,
                UWObjectSpawner.GetObjectWorldPosition(pOTile.X, pOTile.Y, pOObject)
                    + new Vector3(0f, UWObjectSpawner.AnimatedHeightOffset, 0f),
                UWObjectDamageRules.RollSmokeSeconds());

        if (lORemains == UWObjectDamageRules.Remains.Gone)
            UWDebris.Remove(pOLoader, pOEntity, pOObject);
        else
            UWDebris.ReplaceWithDebris(pOLoader, pOEntity, pOObject, pOTile, lOPosition);
    }

    private static void fStrikePlayer(UWWorld pOWorld, UWLevelLoader pOLoader, UWTilePos pOTile, int piDiceCount,
        int piDiceRange, int piDamageType, int piAttacker)
    {
        UWCharacter lOPlayer = UWScene.Character;

        if (lOPlayer == null || pOLoader.WorldPositionToTile(lOPlayer.transform.position) != pOTile)
            return;

        pOWorld.Damage(UWDamageTarget.ThePlayer,
            UWBlow.Blast(UWRandom.RollDice(piDiceCount, piDiceRange), piDamageType, piAttacker, UWBlow.NoCreature));
    }
}
