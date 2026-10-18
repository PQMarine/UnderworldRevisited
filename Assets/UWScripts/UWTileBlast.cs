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
/// WHO IS STRUCK: the tile's object list in its order (UWAreaWalk.DamageSquare, damage_square in
/// UnderworldExhumed SPELLS.C) - the creatures among them by their record tile, whatever their
/// height or place within the tile - and the player at its head when he stands there. Since
/// cleanup step 4.0 (2026-10-18, decision 11 of Docs/CLEANUP-PLAN.md); until then the creatures
/// were found with a physics sphere around the middle of the tile's floor (0.75 of a tile) and put
/// on the tile their transform stood on, struck before the objects and the player last.
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
    /// <param name="piAttacker">Whom the struck creatures hold responsible, the blast's launcher: a
    /// creature's index (the struck creature's own too), UWCritterBrain.PlayerIndex, or
    /// UWBlow.NoBlastAttacker (-1) for nobody.</param>
    public static void Strike(UWLevelLoader pOLoader, UWTilePos pOTile, int piDiceCount, int piDiceRange,
        int piDamageType, int piAttacker)
    {
        if (pOLoader == null || pOLoader.CurrentLevel == null)
            return;

        UWTile lOTile = pOLoader.CurrentLevel.GetTile(pOTile.X, pOTile.Y);

        // NO TILE TEST: damage_square (UnderworldExhumed SPELLS.C) strikes the list of whatever
        // square it is handed, a wall square too - Flame Wind's cross reaches into the walls next
        // to its stroke (cleanup 4.0 follow-up, 2026-10-18, decided by the user: as the original).
        // What lies there: traps and triggers, every one with the protection bit (ShrugsOff), and
        // eight doors and one item of LEV.ARK without it (self-check WallSquares). Until then
        // ours left a solid square alone.
        if (lOTile == null)
            return;

        HashSet<UWDamageable> lODone = new HashSet<UWDamageable>();
        UWWorld lOWorld = UWWorld.Of(pOLoader);
        UWCommonObjectProperties lOCommon = pOLoader.UWDataImporter != null
            ? pOLoader.UWDataImporter.CommonObjectProperties : null;

        // WHAT LIES THERE NOW: a creature this blast kills drops its remains at once in ours,
        // while the original lets it die over its death animation first - they must not burn
        // in the same blast. DamageSquare walks the list as it was before the first blow.
        UWAreaWalk.DamageSquare(lOTile,
            () => fStrikePlayer(lOWorld, pOLoader, pOTile, piDiceCount, piDiceRange, piDamageType, piAttacker),
            pOObject =>
            {
                if (pOObject is UWNpc)
                    fStrikeCreature(lOWorld, pOLoader, pOObject, piDiceCount, piDiceRange, piDamageType, piAttacker,
                        lODone);
                else
                    fStrikeObject(lOWorld, pOLoader, pOTile, lOCommon, pOObject, piDiceCount, piDiceRange,
                        piDamageType, piAttacker, lODone);
            });
    }

    /// <summary>A creature of the tile's list: its body takes the blow unless it is dying.</summary>
    private static void fStrikeCreature(UWWorld pOWorld, UWLevelLoader pOLoader, UWObject pONpc, int piDiceCount,
        int piDiceRange, int piDamageType, int piAttacker, HashSet<UWDamageable> pODone)
    {
        pOLoader.TryGetEntity(pONpc, out UWEntityInfo lOEntity);

        UWCritter lOCritter = lOEntity != null ? lOEntity.GetComponentInParent<UWCritter>() : null;
        UWDamageable lODamageable = lOCritter != null ? lOCritter.GetComponent<UWDamageable>() : null;

        if (lODamageable == null || lODamageable.IsDestroyed || !pODone.Add(lODamageable))
            return;

        // The blast's launcher is charged, the creature itself too when it is caught in its own
        // blast, and nobody for a blast nobody launched (UWBlow.Blast, cleanup step 4.10). The roll
        // comes before the blow; naming the attacker draws nothing.
        pOWorld.Damage(UWWorld.TargetOf(lOCritter, lODamageable),
            UWBlow.Blast(UWRandom.RollDice(piDiceCount, piDiceRange), piDamageType, piAttacker));
    }

    /// <summary>Any other object of the tile's list: a door or a chest takes the blow, a loose
    /// object is worn down.</summary>
    private static void fStrikeObject(UWWorld pOWorld, UWLevelLoader pOLoader, UWTilePos pOTile,
        UWCommonObjectProperties pOCommon, UWObject pOObject, int piDiceCount, int piDiceRange, int piDamageType,
        int piAttacker, HashSet<UWDamageable> pODone)
    {
        pOLoader.TryGetEntity(pOObject, out UWEntityInfo lOEntity);

        UWDamageable lODamageable = lOEntity != null ? lOEntity.GetComponentInParent<UWDamageable>() : null;

        // WORD 0 BIT 13 PROTECTS (DamageObjectAndDoors) - a door too, whatever its hit points.
        if (pOObject.DoorDirection)
            return;

        int liRoll = UWRandom.RollDice(piDiceCount, piDiceRange);

        // A DOOR OR A CHEST has hit points and its own ending (UWDoor, UWContainerDamage).
        if (lODamageable != null)
        {
            if (lODamageable.IsDestroyed || !pODone.Add(lODamageable))
                return;

            pOWorld.Damage(UWWorld.TargetOf(lODamageable),
                UWBlow.Blast(liRoll, piDamageType, piAttacker));

            return;
        }

        UWCommonObjectProperties.Entry lOEntry = default;

        if (pOCommon == null || !pOCommon.TryGet(pOObject.ID, out lOEntry))
            return;

        int liDamage = UWDamageTypes.Scale(lOEntry.Resistances, liRoll, piDamageType);

        // A DOOR WITHOUT A BODY - one in a wall square, which the spawner leaves unbuilt: its
        // record takes damage_object and remove_object (UWDoorRules.WearRecord, BurstOpenRecord)
        // and stays a door; it must not wear down into debris like a loose object.
        if (UWObjectIds.IsDoor(pOObject.ID))
        {
            if (UWDoorRules.WearRecord(pOObject, liDamage, lOEntry.QualityClass)
                && UWDoorRules.BurstOpenRecord(pOLoader.CurrentLevel, pOObject))
                fPlayDoorSound(pOTile, pOObject);

            return;
        }

        if (!UWObjectDamageRules.Wear(pOObject.Quality, liDamage, lOEntry.QualityClass, false,
                out int liQuality))
        {
            pOObject.Quality = (ushort)liQuality;

            return;
        }

        pOObject.Quality = 0;

        fDestroy(pOLoader, pOTile, pOObject, lOEntity, piDamageType);
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

    /// <summary>OpenDoor's sound where the door is (play_effect 0x14 for a portcullis, 0xB for
    /// any other door) - for a bodiless door the blast broke; a door with a body plays it from its
    /// mover.</summary>
    private static void fPlayDoorSound(UWTilePos pOTile, UWObject pODoor)
    {
        UWSoundEffects.PlayAt((pODoor.ID & 7) == PortcullisInClass ? UWSoundEffects.Portcullis : UWSoundEffects.Door,
            UWObjectSpawner.GetObjectWorldPosition(pOTile.X, pOTile.Y, pODoor));
    }

    /// <summary>The portcullis among the doors (OpenDoor's inclass 6).</summary>
    private const int PortcullisInClass = 6;

    private static void fStrikePlayer(UWWorld pOWorld, UWLevelLoader pOLoader, UWTilePos pOTile, int piDiceCount,
        int piDiceRange, int piDamageType, int piAttacker)
    {
        UWCharacter lOPlayer = UWScene.Character;

        if (lOPlayer == null || pOLoader.WorldPositionToTile(lOPlayer.transform.position) != pOTile)
            return;

        // Plain damage of the blast's type, no armour, no halving (UWDamageRequest.Blast, cleanup
        // step 4.2, 2026-10-17); the player keeps no attacker.
        pOWorld.Damage(UWDamageTarget.ThePlayer,
            UWBlow.ToPlayer(UWDamageRequest.Blast(UWRandom.RollDice(piDiceCount, piDiceRange), piDamageType)));
    }
}
