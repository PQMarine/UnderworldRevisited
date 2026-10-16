using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE ONE PLACE WHERE A LEVEL'S OBJECT LISTS CHANGE (cleanup step 2.12, 2026-10-12):
    /// UWWorldEdit on level 1 of the loaded world - a slot reserved and given back, an object
    /// placed, moved, forgotten and removed with the tile chain, the free lists and the events
    /// in step, the culling of a full level. The world is changed; the runner reloads it.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckWorldEdit(CheckContext pOContext)
        {
            Check.Group("World edit (UWWorldEdit: slots, tile chain, events)");

            DataImport lOData = pOContext.Data;
            UWLevel lOLevel = lOData.Levels != null && lOData.Levels.Count > 0 ? lOData.Levels[0] : null;

            if (lOLevel == null || lOLevel.TileData == null)
            {
                Check.Fail("world edit", "no level 1");

                return;
            }

            UWTileQueries lOQueries = new UWTileQueries(lOLevel, lOData);
            UWWorldEdit lOEdit = new UWWorldEdit(lOQueries);
            List<string> lOEvents = new List<string>();

            lOEdit.ObjectPlaced += (pOObject, piX, piY) => lOEvents.Add(string.Format("placed {0:X} {1}/{2}", pOObject.ID, piX, piY));
            lOEdit.ObjectRemoved += pOObject => lOEvents.Add(string.Format("removed {0:X}", pOObject.ID));
            lOEdit.ObjectMoved += (pOObject, piFromX, piFromY, piToX, piToY) =>
                lOEvents.Add(string.Format("moved {0:X} {1}/{2} {3}/{4}", pOObject.ID, piFromX, piFromY, piToX, piToY));
            lOEdit.LevelCleared += () => lOEvents.Add("cleared");

            UWTilePos lOFirst;
            UWTilePos lOSecond;
            UWTilePos lOSolid;

            if (!fFindWorldEditTiles(lOQueries, out lOFirst, out lOSecond, out lOSolid))
            {
                Check.Fail("world edit", "level 1 has no two dry open tiles");

                return;
            }

            fCheckWorldEditPlacement(lOData, lOLevel, lOEdit, lOEvents, lOFirst, lOSecond, lOSolid);
            fCheckWorldEditCreature(lOData, lOLevel, lOEdit, lOEvents, lOFirst);
            fCheckWorldEditFullLevel(lOData, lOLevel, lOEdit, lOEvents, lOFirst);
            fCheckWorldEditListsAgree(lOLevel);
            fCheckWorldEditEffects(lOData, lOLevel, lOEdit, lOEvents, lOFirst);
            fCheckLevelChangeOrder(lOData);
            fCheckWorldEditWipe(lOData, lOLevel, lOEdit, lOEvents, lOFirst);
        }

        /// <summary>Two dry open tiles of level 1 (no water, no lava) and a solid one.</summary>
        private static bool fFindWorldEditTiles(UWTileQueries pOQueries, out UWTilePos pOFirst, out UWTilePos pOSecond,
            out UWTilePos pOSolid)
        {
            List<UWTilePos> lOOpen = new List<UWTilePos>();

            pOSolid = new UWTilePos(0, 0);

            for (int liY = 0; liY < UWWorldScale.TilesPerAxis; liY++)
            {
                for (int liX = 0; liX < UWWorldScale.TilesPerAxis; liX++)
                {
                    UWTile lOTile = pOQueries.GetTile(liX, liY);

                    if (lOTile != null && lOTile.TileType == UWTile.TileTypeEnum.open && !pOQueries.IsDestructiveTile(lOTile))
                        lOOpen.Add(new UWTilePos(liX, liY));
                }
            }

            pOFirst = lOOpen.Count > 0 ? lOOpen[0] : new UWTilePos(0, 0);
            pOSecond = lOOpen.Count > 1 ? lOOpen[1] : new UWTilePos(0, 0);

            return lOOpen.Count > 1 && pOQueries.GetTile(0, 0).TileType == UWTile.TileTypeEnum.solid;
        }

        /// <summary>An item into a tile, on to another, off the map's lists again; a solid tile
        /// refuses and leaves the record as it was.</summary>
        private static void fCheckWorldEditPlacement(DataImport pOData, UWLevel pOLevel, UWWorldEdit pOEdit,
            List<string> pOEvents, UWTilePos pOFirst, UWTilePos pOSecond, UWTilePos pOSolid)
        {
            UWTile lOFirst = pOLevel.GetTile(pOFirst.X, pOFirst.Y);
            UWTile lOSecond = pOLevel.GetTile(pOSecond.X, pOSecond.Y);
            UWObject lODagger = UWObjectFactory.Create(1, pOData.CommonObjectProperties);
            int liBefore = lOFirst.ObjectsInTile.Count;

            pOEvents.Clear();

            Check.Bool("place: an item into a dry open tile", pOEdit.Place(lODagger, pOFirst.X, pOFirst.Y, 5, 2, 7, true)
                == UWWorldEdit.PlaceResult.Placed, true);
            Check.Bool("place: ... appended at the end of the tile's chain",
                lOFirst.ObjectsInTile.Count == liBefore + 1 && lOFirst.ObjectsInTile[liBefore] == lODagger, true);
            Check.Int("place: ... the record names the tile and the spot (x*1000 + y*100 + xpos*10 + ypos)",
                (lODagger.TileX * 1000) + (lODagger.TileY * 100) + (lODagger.XPos * 10) + lODagger.YPos,
                (pOFirst.X * 1000) + (pOFirst.Y * 100) + 52);
            Check.Int("place: ... and the height", lODagger.ZPos, 7);
            Check.Str("place: ... ObjectPlaced after it joined the list", string.Join("; ", pOEvents),
                string.Format("placed 1 {0}/{1}", pOFirst.X, pOFirst.Y));

            pOEvents.Clear();
            pOEdit.Move(lODagger, pOFirst.X, pOFirst.Y);
            Check.Int("move: within its tile nothing happens", pOEvents.Count, 0);

            pOEdit.Move(lODagger, pOSecond.X, pOSecond.Y);
            Check.Bool("move: out of the first tile's chain", lOFirst.ObjectsInTile.Contains(lODagger), false);
            Check.Bool("move: ... to the end of the second's",
                lOSecond.ObjectsInTile[lOSecond.ObjectsInTile.Count - 1] == lODagger, true);
            Check.Int("move: ... the record follows", (lODagger.TileX * 100) + lODagger.TileY, (pOSecond.X * 100) + pOSecond.Y);
            Check.Str("move: ... ObjectMoved names both tiles", string.Join("; ", pOEvents),
                string.Format("moved 1 {0}/{1} {2}/{3}", pOFirst.X, pOFirst.Y, pOSecond.X, pOSecond.Y));

            pOEvents.Clear();
            Check.Bool("remove: the item leaves the world", pOEdit.Remove(lODagger), true);
            Check.Bool("remove: ... out of the chain", lOSecond.ObjectsInTile.Contains(lODagger), false);
            Check.Str("remove: ... ObjectRemoved for the picture", string.Join("; ", pOEvents), "removed 1");
            Check.Bool("remove: a second time it is in no list", pOEdit.Remove(lODagger), false);
            Check.Bool("remove: ... unless the picture still held it", pOEdit.Remove(lODagger, true), true);

            UWObject lORock = UWObjectFactory.Create(1, pOData.CommonObjectProperties);

            lORock.TileX = 9;
            lORock.XPos = 1;
            pOEvents.Clear();
            Check.Bool("place: a solid tile refuses", pOEdit.Place(lORock, pOSolid.X, pOSolid.Y, 5, 5, 5, true)
                == UWWorldEdit.PlaceResult.Refused, true);
            Check.Int("place: ... the record is left as it was", (lORock.TileX * 10) + lORock.XPos, 91);
            Check.Int("place: ... and nothing is told", pOEvents.Count, 0);
        }

        /// <summary>A creature takes its mobile slot from the top of the free list when placed and
        /// gives it back on removal - onto the top again, out of the active list; Forget gives it
        /// back without a word to the picture.</summary>
        private static void fCheckWorldEditCreature(DataImport pOData, UWLevel pOLevel, UWWorldEdit pOEdit,
            List<string> pOEvents, UWTilePos pOAt)
        {
            int liTop = pOLevel.FreeMobileSlots.Count > 0 ? pOLevel.FreeMobileSlots[pOLevel.FreeMobileSlots.Count - 1] : -1;
            int liFree = pOLevel.FreeMobileSlots.Count;
            UWObject lOBat = UWObjectFactory.Create(0x40, pOData.CommonObjectProperties);

            Check.Bool("creature: placed", pOEdit.Place(lOBat, pOAt.X, pOAt.Y, 3, 3, 0, false)
                == UWWorldEdit.PlaceResult.Placed, true);
            Check.Int("creature: ... takes the top of the mobile free list", pOLevel.Masterlist.IndexOf(lOBat), liTop);
            Check.Int("creature: ... which leaves it", pOLevel.FreeMobileSlots.Count, liFree - 1);
            Check.Bool("creature: ... and joins the active list at the end",
                pOLevel.ActiveMobiles.Count > 0 && pOLevel.ActiveMobiles[pOLevel.ActiveMobiles.Count - 1] == liTop, true);

            pOEvents.Clear();
            Check.Bool("creature: removed", pOEdit.Remove(lOBat), true);
            Check.Bool("creature: ... its slot is the top of the free list again",
                pOLevel.FreeMobileSlots.Count == liFree && pOLevel.FreeMobileSlots[liFree - 1] == liTop, true);
            Check.Int("creature: ... and out of the active list", pOLevel.GetActiveMobileOrder(liTop), int.MaxValue);

            UWObject lOSecondBat = UWObjectFactory.Create(0x40, pOData.CommonObjectProperties);

            pOEdit.Place(lOSecondBat, pOAt.X, pOAt.Y, 3, 3, 0, false);
            Check.Int("creature: the next one takes the same slot", pOLevel.Masterlist.IndexOf(lOSecondBat), liTop);

            pOEvents.Clear();
            Check.Bool("forget: a dying creature leaves the list", pOEdit.Forget(lOSecondBat), true);
            Check.Int("forget: ... without a word to the picture", pOEvents.Count, 0);
            Check.Bool("forget: ... and gives its slot back",
                pOLevel.FreeMobileSlots.Count == liFree && pOLevel.FreeMobileSlots[liFree - 1] == liTop, true);
        }

        /// <summary>A level with no static slot left: GetFreeObject culls up to ten far objects
        /// and refuses the new one when that does not suffice.</summary>
        private static void fCheckWorldEditFullLevel(DataImport pOData, UWLevel pOLevel, UWWorldEdit pOEdit,
            List<string> pOEvents, UWTilePos pOAt)
        {
            int liMobile;
            int liStatic;

            UWObjectLimitRules.CountUsage(pOLevel, out liMobile, out liStatic);

            // The player carries what fills the level to the last slot: nothing to cull for.
            pOEdit.CarriedObjectCount = () => UWObjectLimitRules.StaticSlots - liStatic - 1;
            pOEdit.PlayerTile = () => pOAt;
            pOEvents.Clear();

            UWObject lOLast = UWObjectFactory.Create(1, pOData.CommonObjectProperties);

            Check.Bool("full: the last slot is taken without culling",
                pOEdit.Place(lOLast, pOAt.X, pOAt.Y, 3, 3, 0, false) == UWWorldEdit.PlaceResult.Placed, true);
            Check.Int("full: ... nothing culled (its own ObjectPlaced only)", pOEvents.Count, 1);

            // Now carrying as much as the level holds: every request culls, and it never suffices.
            pOEdit.CarriedObjectCount = () => UWObjectLimitRules.StaticSlots;
            pOEvents.Clear();

            UWObject lORefused = UWObjectFactory.Create(1, pOData.CommonObjectProperties);
            UWWorldEdit.PlaceResult leResult = pOEdit.Place(lORefused, pOAt.X, pOAt.Y, 3, 3, 0, false);
            int liCulled = pOEvents.FindAll(psEvent => psEvent.StartsWith("removed")).Count;

            Check.Bool("full: an object finds no slot and is refused", leResult == UWWorldEdit.PlaceResult.Refused, true);
            Check.Bool("full: ... after culling some far objects, ten at most (" + liCulled + ")",
                liCulled > 0 && liCulled <= UWObjectLimitRules.StaticCullLimit, true);
            Check.Bool("full: ... and only removals were told",
                pOEvents.TrueForAll(psEvent => psEvent.StartsWith("removed")), true);

            pOEdit.CarriedObjectCount = null;
            pOEdit.Remove(lOLast);
        }

        /// <summary>Decision 8: an effect takes the top of the static free list while it lives and
        /// counts against the static slots, as put_effect's CreateObj does; with no slot free there
        /// is no effect, and nothing is culled for one. Giving the slots back, the last first,
        /// leaves the free list as it was.</summary>
        private static void fCheckWorldEditEffects(DataImport pOData, UWLevel pOLevel, UWWorldEdit pOEdit,
            List<string> pOEvents, UWTilePos pOAt)
        {
            List<int> lOFreeBefore = new List<int>(pOLevel.FreeStaticSlots);
            int liTop = lOFreeBefore[lOFreeBefore.Count - 1];
            int liMobile;
            int liStatic;
            int liToken;

            UWObjectLimitRules.CountUsage(pOLevel, out liMobile, out liStatic);
            pOEvents.Clear();

            Check.Bool("effect: takes a slot", pOEdit.TryTakeEffectSlot(out liToken), true);
            Check.Bool("effect: ... the top of the static free list",
                pOLevel.FreeStaticSlots.Count == lOFreeBefore.Count - 1 && !pOLevel.IsStaticSlotFree(liTop), true);
            Check.Int("effect: ... held while it lives", pOEdit.EffectSlotsHeld, 1);

            int liCountAfter;

            UWObjectLimitRules.CountUsage(pOLevel, out liMobile, out liCountAfter);
            Check.Int("effect: ... in no tile list (the count of the level is the same)", liCountAfter, liStatic);
            Check.Int("effect: ... and nothing told to the view", pOEvents.Count, 0);

            pOEdit.ReleaseEffectSlot(liToken);
            Check.Bool("effect: ended, its slot is the top again",
                pOLevel.FreeStaticSlots.Count == lOFreeBefore.Count && pOLevel.FreeStaticSlots[lOFreeBefore.Count - 1] == liTop, true);
            Check.Int("effect: ... none held", pOEdit.EffectSlotsHeld, 0);

            pOEdit.ReleaseEffectSlot(liToken);
            Check.Int("effect: giving it back twice changes nothing", pOLevel.FreeStaticSlots.Count, lOFreeBefore.Count);

            // One static slot left: the first effect takes it, the second finds none and culls nothing.
            pOEdit.CarriedObjectCount = () => UWObjectLimitRules.StaticSlots - liStatic - 1;

            int liFirst;
            int liSecond;

            Check.Bool("effect: the last free slot", pOEdit.TryTakeEffectSlot(out liFirst), true);
            Check.Bool("effect: then there is none - no effect", pOEdit.TryTakeEffectSlot(out liSecond), false);
            Check.Int("effect: ... and nothing was culled for it", pOEvents.Count, 0);

            UWObject lOItem = UWObjectFactory.Create(1, pOData.CommonObjectProperties);

            pOEdit.PlayerTile = () => pOAt;

            UWWorldEdit.PlaceResult leResult = pOEdit.Place(lOItem, pOAt.X, pOAt.Y, 3, 3, 0, false);

            Check.Bool("effect: a running effect's slot counts for an object placed meanwhile (it culls)",
                pOEvents.Exists(psEvent => psEvent.StartsWith("removed")), true);

            if (leResult != UWWorldEdit.PlaceResult.Refused)
                pOEdit.Remove(lOItem);

            pOEdit.CarriedObjectCount = null;

            int liThird;

            pOEdit.TryTakeEffectSlot(out liThird);
            pOEdit.ReleaseAllEffectSlots();
            Check.Int("effect: all given back, none held", pOEdit.EffectSlotsHeld, 0);
            Check.Bool("effect: ... the static free list as it was before, in its order",
                string.Join(",", pOLevel.FreeStaticSlots) == string.Join(",", lOFreeBefore), true);
        }

        /// <summary>
        /// Decision 9, the original's GetLevel (GAMEWRAP.C): the level is there, the carried
        /// objects take their numbers in the order of their chain (a container before its
        /// contents), and only then is room made - culling only what the slots did not cover.
        /// Leaving gives back the effects' slots and then the carried ones, so the free list is
        /// what it was. On level 2 of the loaded world.
        /// </summary>
        private static void fCheckLevelChangeOrder(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels.Count > 1 ? pOData.Levels[1] : null;

            if (lOLevel == null || lOLevel.FreeStaticSlots == null || lOLevel.FreeStaticSlots.Count < 8)
            {
                Check.Fail("level change", "level 2 has no static free list to take from");

                return;
            }

            UWWorldEdit lOEdit = new UWWorldEdit(new UWTileQueries(lOLevel, pOData));
            UWObject lOSack = UWObjectFactory.Create(UWObjectIds.FirstContainer, pOData.CommonObjectProperties);

            lOSack.HasQuantity = false;
            lOSack.Quantity = 0;
            lOSack.EnsureContentsLoaded(lOLevel.Masterlist);
            lOSack.Contents.Add(UWObjectFactory.Create(1, pOData.CommonObjectProperties));
            lOSack.Contents.Add(UWObjectFactory.Create(2, pOData.CommonObjectProperties));

            UWObject lODagger = UWObjectFactory.Create(1, pOData.CommonObjectProperties);
            List<UWObject> lOChain = new List<UWObject> { lOSack, lODagger };
            int liCarried = UWObjectLimitRules.CountObjects(lOChain);
            List<int> lOFreeBefore = new List<int>(lOLevel.FreeStaticSlots);
            int liTop = lOFreeBefore.Count - 1;
            int liCulled = UWLevelChangeRules.Arrive(lOEdit, lOChain, liCarried, 32, 32);

            Check.Int("level change: four carried objects arrive in a level with room, nothing culled", liCulled, 0);
            Check.Seq("level change: ... numbered in chain order, the sack before its contents, from the top",
                new[] { lOSack.SlotIndex, lOSack.Contents[0].SlotIndex, lOSack.Contents[1].SlotIndex, lODagger.SlotIndex },
                new[] { lOFreeBefore[liTop], lOFreeBefore[liTop - 1], lOFreeBefore[liTop - 2], lOFreeBefore[liTop - 3] });

            int liToken;

            lOEdit.TryTakeEffectSlot(out liToken);
            UWLevelChangeRules.Leave(lOEdit, lOChain);

            List<int> lOFreeAfter = new List<int>(lOLevel.FreeStaticSlots);
            List<int> lOSortedBefore = new List<int>(lOFreeBefore);

            lOFreeAfter.Sort();
            lOSortedBefore.Sort();
            Check.Bool("level change: leaving gives back the effect's slot and the carried ones - the same slots free",
                string.Join(",", lOFreeAfter) == string.Join(",", lOSortedBefore), true);
            Check.Int("level change: ... the effect's first, so it lies below the four carried ones (ClearObjectList's order)",
                lOLevel.FreeStaticSlots[lOLevel.FreeStaticSlots.Count - 5], lOFreeBefore[liTop - 4]);
            Check.Int("level change: ... no number left on the carried objects",
                lOSack.SlotIndex + lOSack.Contents[0].SlotIndex + lOSack.Contents[1].SlotIndex + lODagger.SlotIndex, 0);

            // A full level: the inventory exceeds the free static slots by five.
            int liMobile;
            int liStatic;

            UWObjectLimitRules.CountUsage(lOLevel, out liMobile, out liStatic);

            bool lbNumberedFirst = true;
            int liRemoved = 0;

            lOEdit.ObjectRemoved += pOObject =>
            {
                if (liRemoved++ == 0)
                    lbNumberedFirst = lOChain.TrueForAll(pOCarried => pOCarried.SlotIndex > 0);
            };

            liCulled = UWLevelChangeRules.Arrive(lOEdit, lOChain, UWObjectLimitRules.StaticSlots - liStatic + 5, 32, 32);

            Check.Bool("level change: arriving in a full level culls (" + liCulled + ")", liCulled > 0 && liCulled == liRemoved, true);
            Check.Bool("level change: ... after the carried objects took their numbers", lbNumberedFirst, true);

            UWObjectLimitRules.CountUsage(lOLevel, out liMobile, out int liStaticAfter);
            Check.Bool("level change: ... until level and inventory fit (" + liStaticAfter + " in the level)",
                liStaticAfter + UWObjectLimitRules.StaticSlots - liStatic + 5 <= UWObjectLimitRules.StaticSlots, true);

            UWLevelChangeRules.Leave(lOEdit, lOChain);
        }

        /// <summary>Armageddon (WipeAll): every tile list empty and the object store as Map_ObjFix
        /// (OBJECTS.C) leaves it - every slot free, the highest on top, the active list and the
        /// animation list empty. Runs last: level 1 stays empty until the runner reloads.</summary>
        private static void fCheckWorldEditWipe(DataImport pOData, UWLevel pOLevel, UWWorldEdit pOEdit,
            List<string> pOEvents, UWTilePos pOAt)
        {
            pOEvents.Clear();
            pOEdit.WipeAll();

            int liLeft = 0;

            foreach (UWTile lOTile in pOLevel.TileData)
                liLeft += lOTile.ObjectsInTile.Count;

            Check.Int("wipe: no object left in any tile", liLeft, 0);
            Check.Str("wipe: ... LevelCleared once, nothing else", string.Join("; ", pOEvents), "cleared");
            Check.Int("wipe: every mobile slot from 2 is free", pOLevel.FreeMobileSlots.Count, 254);
            Check.Int("wipe: ... the highest on top", pOLevel.FreeMobileSlots[pOLevel.FreeMobileSlots.Count - 1], 255);
            Check.Int("wipe: every static slot is free", pOLevel.FreeStaticSlots.Count, 768);
            Check.Int("wipe: ... the highest on top", pOLevel.FreeStaticSlots[pOLevel.FreeStaticSlots.Count - 1], 1023);
            Check.Int("wipe: the active list is empty", pOLevel.ActiveMobiles.Count, 0);
            Check.Int("wipe: the animation list is empty", pOLevel.Animations.Entries.Count, 0);

            UWObject lOBat = UWObjectFactory.Create(0x40, pOData.CommonObjectProperties);

            pOEdit.Place(lOBat, pOAt.X, pOAt.Y, 3, 3, 0, false);
            Check.Int("wipe: a creature made afterwards takes slot 255", pOLevel.Masterlist.IndexOf(lOBat), 255);
            Check.Int("wipe: ... the first of the active list", pOLevel.GetActiveMobileOrder(255), 0);
        }

        /// <summary>No slot both free and in use: the mobile free list and the active list are
        /// disjoint, and no creature in a tile list sits on a free slot.</summary>
        private static void fCheckWorldEditListsAgree(UWLevel pOLevel)
        {
            HashSet<int> lOFree = new HashSet<int>(pOLevel.FreeMobileSlots);
            int liClashes = 0;

            foreach (int liActive in pOLevel.ActiveMobiles)
            {
                if (lOFree.Contains(liActive))
                    liClashes++;
            }

            foreach (UWTile lOTile in pOLevel.TileData)
            {
                foreach (UWObject lOObject in lOTile.ObjectsInTile)
                {
                    int liIndex = lOObject is UWNpc ? pOLevel.Masterlist.IndexOf(lOObject) : -1;

                    if (liIndex >= 0 && lOFree.Contains(liIndex))
                        liClashes++;
                }
            }

            Check.Int("lists: no mobile slot is free and in use at once", liClashes, 0);
        }
    }
}
