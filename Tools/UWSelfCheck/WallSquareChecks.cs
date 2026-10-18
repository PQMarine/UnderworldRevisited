using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup 4.0 follow-up (2026-10-18, decided by the user: as the original): a blast strikes
    /// the list of a wall square too, as damage_square (UnderworldExhumed SPELLS.C) has no tile
    /// test. What lies in the wall squares of the nine levels must therefore take it as
    /// damage_object (DAMAGE.C) lets it: every trap and trigger shrugs it off by word 0 bit 13
    /// (UWObjectDamageRules.ShrugsOff), and what is left that can be harmed is known - eight doors
    /// without a body, none with a chain, and one loose item. A bodiless door's record wears and
    /// bursts open as damage_object and remove_object have it (UWDoorRules.WearRecord,
    /// BurstOpenRecord).
    /// </summary>
    public static partial class Program
    {
        private static void fCheckWallSquares(DataImport pOData)
        {
            Check.Group("Wall squares (damage_square without a tile test, damage_object's protection)");

            fCheckTrapsShrugOff(pOData);
            fCheckWallSquareContents(pOData);
            fCheckDoorRecordWear();
        }

        /// <summary>Every trap and trigger of the nine levels, in a wall square or not: protected,
        /// and the wear of the hardest blast leaves its quality.</summary>
        private static void fCheckTrapsShrugOff(DataImport pOData)
        {
            int liTraps = 0;
            int liTriggers = 0;
            int liHarmed = 0;
            string lsFirstHarmed = "";

            foreach (UWLevel lOLevel in pOData.Levels)
            {
                if (lOLevel == null || lOLevel.TileData == null)
                    continue;

                foreach (UWTile lOTile in lOLevel.TileData)
                {
                    if (lOTile == null || lOTile.ObjectsInTile == null)
                        continue;

                    foreach (UWObject lOObject in lOTile.ObjectsInTile)
                    {
                        if (lOObject == null || UWObjectIds.Major(lOObject.ID) != TrapMajorClass)
                            continue;

                        if (lOObject.ID < FirstTriggerId)
                            liTraps++;
                        else
                            liTriggers++;

                        pOData.CommonObjectProperties.TryGet(lOObject.ID, out UWCommonObjectProperties.Entry lOEntry);

                        bool lbHarmed = !UWObjectDamageRules.ShrugsOff(lOObject.DoorDirection, lOEntry.QualityClass)
                            || UWObjectDamageRules.Wear(lOObject.Quality, 255, lOEntry.QualityClass, lOObject.DoorDirection,
                                out int liQuality)
                            || liQuality != lOObject.Quality;

                        if (lbHarmed && liHarmed++ == 0)
                            lsFirstHarmed = string.Format(" (first: level {0} {1}/{2} id {3:X})", lOLevel.LevelNumber,
                                lOTile.X, lOTile.Z, lOObject.ID);
                    }
                }
            }

            Check.Int("traps: the nine levels hold 246", liTraps, 246);
            Check.Int("triggers: ... and 171", liTriggers, 171);
            Check.Int("traps and triggers: none takes harm from a blast" + lsFirstHarmed, liHarmed, 0);
        }

        /// <summary>The objects of the wall squares that damage_object would harm: the eight doors
        /// and the one item the coordinator found in LEV.ARK, no door with a chain (so the open
        /// event BurstOpenRecord leaves out has nothing to fire).</summary>
        private static void fCheckWallSquareContents(DataImport pOData)
        {
            List<string> lOHarmable = new List<string>();
            int liDoorsWithChain = 0;

            foreach (UWLevel lOLevel in pOData.Levels)
            {
                if (lOLevel == null || lOLevel.TileData == null)
                    continue;

                foreach (UWTile lOTile in lOLevel.TileData)
                {
                    if (lOTile == null || lOTile.TileType != UWTile.TileTypeEnum.solid || lOTile.ObjectsInTile == null)
                        continue;

                    foreach (UWObject lOObject in lOTile.ObjectsInTile)
                    {
                        pOData.CommonObjectProperties.TryGet(lOObject.ID, out UWCommonObjectProperties.Entry lOEntry);

                        if (UWObjectDamageRules.ShrugsOff(lOObject.DoorDirection, lOEntry.QualityClass))
                            continue;

                        lOHarmable.Add(string.Format("{0}:{1}/{2}:{3:X}", lOLevel.LevelNumber, lOTile.X, lOTile.Z, lOObject.ID));

                        if (UWObjectIds.IsDoor(lOObject.ID) && !lOObject.HasQuantity && lOObject.Quantity != 0)
                            liDoorsWithChain++;
                    }
                }
            }

            Check.Str("wall squares: what a blast can harm there", string.Join(", ", lOHarmable),
                "2:30/15:141, 2:30/57:140, 3:49/57:141, 5:30/57:CB, 7:42/10:147, 7:18/29:147, 7:26/35:147, "
                + "7:36/35:147, 7:26/41:147");
            Check.Int("wall squares: ... no door among them with a chain (no open event to fire)", liDoorsWithChain, 0);
        }

        /// <summary>damage_object and remove_object on a bodiless door's record.</summary>
        private static void fCheckDoorRecordWear()
        {
            const int SecretDoorClass = 2;

            UWObject lODoor = new UWObject(UWObjectIds.FirstClosedDoor + 7) { Quality = 40, ZPos = 16 };

            Check.Bool("door record: 35 halved twice (class 2) leaves it standing",
                UWDoorRules.WearRecord(lODoor, 35, SecretDoorClass), false);
            Check.Int("door record: ... 40 - 8", lODoor.Quality, 32);
            Check.Bool("door record: 3 halved twice is nothing", UWDoorRules.WearRecord(lODoor, 3, SecretDoorClass), false);
            Check.Int("door record: ... the quality stays", lODoor.Quality, 32);

            UWObject lOProtected = new UWObject(UWObjectIds.FirstClosedDoor) { Quality = 1, DoorDirection = true };

            Check.Bool("door record: bit 13 takes nothing", UWDoorRules.WearRecord(lOProtected, 255, 0), false);
            Check.Int("door record: ... quality kept", lOProtected.Quality, 1);
            Check.Bool("door record: class 3 takes nothing",
                UWDoorRules.WearRecord(new UWObject(UWObjectIds.FirstClosedDoor) { Quality = 1 }, 255, 3), false);

            // A closed door with a spike of strength 5 (owner bit 0, strength above it).
            UWObject lOSpiked = new UWObject(UWObjectIds.FirstClosedDoor) { Quality = 10, Owner = (5 << 1) | 1 };

            Check.Bool("door record: the spike takes the blow", UWDoorRules.WearRecord(lOSpiked, 20, 0), false);
            Check.Int("door record: ... the door's quality stays", lOSpiked.Quality, 10);

            Check.Bool("door record: worn to 0 it breaks", UWDoorRules.WearRecord(lODoor, 255, SecretDoorClass), true);
            Check.Int("door record: ... at quality 0", lODoor.Quality, 0);

            lODoor.Owner = 1;
            Check.Bool("door record: the broken closed door opens", UWDoorRules.BurstOpenRecord(null, lODoor), true);
            Check.Int("door record: ... the open secret door", lODoor.ID, UWObjectIds.OpenSecretDoor);
            Check.Int("door record: ... raised by 0x18", lODoor.ZPos, 16 + 0x18);
            Check.Int("door record: ... the spike bit off", lODoor.Owner & 1, 0);
            Check.Bool("door record: an open one does not open again", UWDoorRules.BurstOpenRecord(null, lODoor), false);
            Check.Int("door record: ... and stays open", lODoor.ID, UWObjectIds.OpenSecretDoor);
        }

        /// <summary>Traps and triggers: major class 6 (0x180-0x1BF), the triggers from 0x1A0.</summary>
        private const int TrapMajorClass = 6;

        private const int FirstTriggerId = 0x1A0;
    }
}
