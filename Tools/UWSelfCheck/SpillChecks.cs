using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 2.6 (2026-10-12): the spill partition (UWSpillRules), the debris roll
    /// (UWObjectDamageRules) and the bag a fire blast leaves as it is when the culling test
    /// spares it (UWObjectLimitRules.CullingTest, shared with the motion core). Pinned over
    /// the rule itself and over real containers of the fresh levels - a chest with a lock and
    /// contents, a sack, a creature with a pack.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckSpill(DataImport pOData)
        {
            Check.Group("Spill (UWSpillRules, the debris roll, the bag the culling test spares)");

            fCheckSpillPartitionRule();
            fCheckSpillOnRealContainers(pOData);
            fCheckDebrisRoll();
            fCheckBagSpared(pOData.CommonObjectProperties);
        }

        // ------------------------------------------------- the rule itself

        private static void fCheckSpillPartitionRule()
        {
            const int liSword = 0x02;

            UWObject lOLocked = new UWObject((ushort)UWObjectMechanics.LockObjectId) { Flags = 1 };
            UWObject lOOpen = new UWObject((ushort)UWObjectMechanics.LockObjectId) { Flags = 0 };
            UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.UseTriggerId);
            UWObject lOPickupTrigger = new UWObject((ushort)(UWObjectIds.FirstTrigger + 1));
            UWObject lOThing = new UWObject(liSword);
            List<UWObject> lOChain = new List<UWObject> { lOThing, null, lOTrigger, lOOpen, lOPickupTrigger };

            fExpectBool("spill: a locked lock refuses the use",
                UWSpillRules.FindLockedLock(new List<UWObject> { lOThing, lOLocked }) == lOLocked, true);
            fExpectBool("spill: an unlocked lock does not (checkLock's 'not locked')",
                UWSpillRules.FindLockedLock(lOChain) == null, true);
            fExpectBool("spill: no lock, no refusal", UWSpillRules.FindLockedLock(new List<UWObject> { lOThing }) == null, true);
            fExpectBool("spill: only the first lock counts - open first, locked second opens (checkLock's Obj_InList)",
                UWSpillRules.FindLockedLock(new List<UWObject> { lOThing, lOOpen, lOLocked }) == null, true);
            fExpectBool("spill: ... locked first refuses",
                UWSpillRules.FindLockedLock(new List<UWObject> { lOLocked, lOOpen }) == lOLocked, true);

            // Decision 25 (per user on the original, 2026-10-12): drop_link_chain puts the whole
            // chain down - lock and triggers too.
            List<UWObject> lOSpilled = UWSpillRules.Partition(lOChain);

            fExpectInt("spill: the whole chain goes down, a null link skipped", lOSpilled.Count, 4);
            fExpectBool("spill: ... in chain order: thing, use trigger, lock, pick-up trigger",
                lOSpilled[0] == lOThing && lOSpilled[1] == lOTrigger && lOSpilled[2] == lOOpen
                && lOSpilled[3] == lOPickupTrigger, true);
            fExpectInt("spill: an empty chain partitions to nothing", UWSpillRules.Partition(null).Count, 0);

            List<UWObject> lOUseTriggers = UWSpillRules.UseTriggers(lOChain);

            fExpectBool("spill: a smashed container's use trigger is found (damage_object fires it first)",
                lOUseTriggers.Count == 1 && lOUseTriggers[0] == lOTrigger, true);

            fExpectBool("spill: 'is empty' when the chain was empty and the player used it",
                UWSpillRules.AnnouncesEmpty(0, true), true);
            fExpectBool("spill: ... silent when smashed", UWSpillRules.AnnouncesEmpty(0, false), false);
            fExpectBool("spill: ... silent when something came out", UWSpillRules.AnnouncesEmpty(1, true), false);

            // remove_object (DAMAGE.C): a smashed chest or barrel loses ONE lock and is used; the
            // nightstand is not used, its chain is freed (2026-10-12).
            UWObject lOSecond = new UWObject((ushort)UWObjectMechanics.LockObjectId) { Flags = 1 };
            List<UWObject> lOTwoLocks = new List<UWObject> { lOThing, lOOpen, lOSecond };

            fExpectBool("smash: the first lock is taken out", UWSpillRules.TakeFirstLock(lOTwoLocks) == lOOpen, true);
            fExpectBool("smash: ... a second, locked one still refuses",
                UWSpillRules.FindLockedLock(lOTwoLocks) == lOSecond && lOTwoLocks.Count == 2, true);
            fExpectBool("smash: no lock, nothing taken", UWSpillRules.TakeFirstLock(new List<UWObject> { lOThing }) == null, true);
            fExpectBool("smash: a chest is used", UWObjectDamageRules.IsUsedWhenDestroyed(UWObjectMechanics.ChestObjectId), true);
            fExpectBool("smash: a barrel is used", UWObjectDamageRules.IsUsedWhenDestroyed(UWObjectMechanics.BarrelObjectId), true);
            fExpectBool("smash: a nightstand is not", UWObjectDamageRules.IsUsedWhenDestroyed(UWObjectMechanics.NightstandObjectId), false);
        }

        // ------------------------------------------------- real containers

        /// <summary>The first chest with a lock and something else in it, the first bag with
        /// contents and the first creature with a pack, over the nine fresh levels; and a count
        /// of the containers that hold a trigger or a lock at all - the cases where ours
        /// (lock and triggers stay) and the original's drop_link_chain (the whole chain goes
        /// down) would differ.</summary>
        private static void fCheckSpillOnRealContainers(DataImport pOData)
        {
            UWObject lOChest = null;
            UWObject lOBag = null;
            UWNpc lOCreature = null;
            string lsChestAt = "";
            string lsBagAt = "";
            string lsCreatureAt = "";
            int liWithTrigger = 0;
            int liWithLock = 0;
            int liWithUseTrigger = 0;
            string lsWhere = "";

            for (int liLevel = 0; pOData.Levels != null && liLevel < pOData.Levels.Count; liLevel++)
            {
                UWLevel lOLevel = pOData.Levels[liLevel];

                if (lOLevel == null || lOLevel.TileData == null)
                    continue;

                foreach (UWTile lOTile in lOLevel.TileData)
                {
                    if (lOTile == null || lOTile.ObjectsInTile == null)
                        continue;

                    foreach (UWObject lOObject in lOTile.ObjectsInTile)
                    {
                        if (lOObject == null)
                            continue;

                        bool lbWorldContainer = UWObjectMechanics.IsWorldContainer(lOObject.ID);
                        bool lbBag = UWObjectDamageRules.SpillsWhenDestroyed(lOObject.ID);
                        UWNpc lONpc = lOObject as UWNpc;

                        if (!lbWorldContainer && !lbBag && lONpc == null)
                            continue;

                        lOObject.EnsureContentsLoaded(lOLevel.Masterlist);

                        bool lbHasLock = false;
                        bool lbHasTrigger = false;
                        bool lbHasUseTrigger = false;
                        int liOthers = 0;

                        foreach (UWObject lOItem in lOObject.Contents)
                        {
                            if (lOItem == null)
                                continue;

                            if (lOItem.ID == UWObjectMechanics.LockObjectId)
                                lbHasLock = true;
                            else if (UWObjectIds.IsTrigger(lOItem.ID))
                            {
                                lbHasTrigger = true;
                                lbHasUseTrigger |= lOItem.ID == UWObjectMechanics.UseTriggerId;
                            }
                            else
                                liOthers++;
                        }

                        string lsAt = string.Format("level {0} at {1}/{2}", liLevel + 1, lOObject.TileX, lOObject.TileY);

                        if (lONpc == null)
                        {
                            if (lbHasTrigger)
                            {
                                liWithTrigger++;
                                lsWhere += " trigger in 0x" + lOObject.ID.ToString("X") + " " + lsAt + ";";
                            }
                            if (lbHasUseTrigger)
                                liWithUseTrigger++;
                            if (lbHasLock)
                            {
                                liWithLock++;
                                lsWhere += " lock in 0x" + lOObject.ID.ToString("X") + " " + lsAt + ";";
                            }
                        }

                        if (lONpc != null && lOCreature == null && liOthers > 0)
                        {
                            lOCreature = lONpc;
                            lsCreatureAt = lsAt;
                        }
                        else if (lOObject.ID == UWObjectMechanics.ChestObjectId && lOChest == null && lbHasLock && liOthers > 0)
                        {
                            lOChest = lOObject;
                            lsChestAt = lsAt;
                        }
                        else if (lbBag && lOBag == null && liOthers > 0 && !lbHasTrigger && !lbHasLock)
                        {
                            lOBag = lOObject;
                            lsBagAt = lsAt;
                        }
                    }
                }
            }

            fExpectBool("spill: a chest with a lock and contents exists in the fresh levels", lOChest != null, true);

            if (lOChest != null)
            {
                List<UWObject> lOSpilled = UWSpillRules.Partition(lOChest.Contents);
                int liLocks = 0;

                foreach (UWObject lOItem in lOSpilled)
                    if (lOItem.ID == UWObjectMechanics.LockObjectId)
                        liLocks++;

                fExpectInt("spill: the chest (" + lsChestAt + ") puts its lock down with the rest", liLocks, 1);
                fExpectBool("spill: ... everything of its chain goes down (" + lOSpilled.Count + " objects)",
                    lOSpilled.Count == lOChest.Contents.Count, true);
                fExpectBool("spill: ... a locked one refuses, an unlocked one spills",
                    (UWSpillRules.FindLockedLock(lOChest.Contents) != null) == fChestIsLocked(lOChest), true);
            }

            fExpectBool("spill: a bag with contents exists in the fresh levels", lOBag != null, true);

            if (lOBag != null)
            {
                List<UWObject> lOSpilled = UWSpillRules.Partition(lOBag.Contents);

                fExpectInt("spill: the bag (" + lsBagAt + ", id 0x" + lOBag.ID.ToString("X") + ") spills everything",
                    lOSpilled.Count, lOBag.Contents.Count);
            }

            fExpectBool("spill: a creature with a pack exists in the fresh levels", lOCreature != null, true);

            if (lOCreature != null)
            {
                List<UWObject> lOSpilled = UWSpillRules.Partition(lOCreature.Contents);

                fExpectInt("spill: the creature (" + lsCreatureAt + ") drops its whole pack",
                    lOSpilled.Count, lOCreature.Contents.Count);
                fExpectBool("spill: ... which is not empty", lOSpilled.Count > 0, true);
            }

            fPass("spill: world containers and bags holding a lock / a trigger / a use trigger",
                liWithLock + " / " + liWithTrigger + " / " + liWithUseTrigger + " over the nine levels:" + lsWhere);
        }

        private static bool fChestIsLocked(UWObject pOChest)
        {
            foreach (UWObject lOItem in pOChest.Contents)
                if (lOItem != null && lOItem.ID == UWObjectMechanics.LockObjectId && UWObjectMechanics.IsLockLocked(lOItem))
                    return true;

            return false;
        }

        // ------------------------------------------------- debris

        private static void fCheckDebrisRoll()
        {
            bool lbFirst = false;
            bool lbSecond = false;
            bool lbOther = false;

            for (int liRun = 0; liRun < 2000; liRun++)
            {
                int liId = UWObjectDamageRules.RollDebrisObjectId();

                if (liId == UWObjectDamageRules.FirstDebrisObjectId)
                    lbFirst = true;
                else if (liId == UWObjectDamageRules.FirstDebrisObjectId + 1)
                    lbSecond = true;
                else
                    lbOther = true;
            }

            fExpectBool("debris: the roll gives 0xD5", lbFirst, true);
            fExpectBool("debris: ... and 0xD6", lbSecond, true);
            fExpectBool("debris: ... and nothing else", lbOther, false);
            fExpectBool("debris: both piles count as debris",
                UWObjectDamageRules.IsDebris(0xD5) && UWObjectDamageRules.IsDebris(0xD6) && !UWObjectDamageRules.IsDebris(0xD7), true);
            fExpectInt("debris: the pile keeps the worn quality 0", UWObjectDamageRules.DebrisQuality, 0);
            fExpectInt("debris: a destroyed bag is tested with the liquids' ten",
                UWObjectDamageRules.BagCullingRangeArgument, UWLiquidCulling.RangeBase);
        }

        // ------------------------------------------------- the bag under fire

        /// <summary>remove_object's Obj_Elem_Fate(10): a bag with word 0 bit 13, a bag that is
        /// valuable enough itself and a bag holding something that is are left as they are; a
        /// bag of low priority with nothing valuable in it spills. The test is the motion core's
        /// (CullingTest) - its draws are counted, so the motion's sequence is pinned as well.</summary>
        private static void fCheckBagSpared(UWCommonObjectProperties pOProperties)
        {
            const int liSack = 0x80;
            const int liRuns = 300;

            if (pOProperties == null)
            {
                fFail("bag: COMOBJ.DAT loaded", "no common object properties");

                return;
            }

            UWCommonObjectProperties.Entry lOSackEntry;
            pOProperties.TryGet(liSack, out lOSackEntry);

            int liCheapId = fFindIdWithPriority(pOProperties, 0, UWLiquidCulling.RangeBase);
            int liHighId = fFindIdWithPriority(pOProperties, UWLiquidCulling.RangeBase + UWLiquidCulling.RangeDice, 15);
            int liMiddleId = fFindIdWithPriority(pOProperties, UWLiquidCulling.RangeBase + 1, UWLiquidCulling.RangeBase + 1);

            fExpectBool("bag: a cheap thing (priority up to 10) exists in COMOBJ", liCheapId >= 0, true);
            fExpectBool("bag: a thing of priority 11 exists in COMOBJ (the emeralds' class)", liMiddleId >= 0, true);
            fExpectBool("bag: a thing above every range (priority 13 or more) exists in COMOBJ", liHighId >= 0, true);

            if (liCheapId < 0 || liMiddleId < 0 || liHighId < 0)
                return;

            // The sack itself: always culled when its priority is ten or below, never when it
            // tops every range - the expectation follows the table, the roll decides nothing.
            bool lbSackAlwaysGoes = UWLiquidCulling.AlwaysSwallows(lOSackEntry.CullingPriority);
            bool lbSackNeverGoes = UWLiquidCulling.NeverSwallows(lOSackEntry.CullingPriority);

            fExpectBool("bag: the sack's own priority (" + lOSackEntry.CullingPriority + ") decides outright",
                lbSackAlwaysGoes || lbSackNeverGoes, true);

            UWObject lOEmpty = fBag(liSack);
            fExpectInt("bag: an empty sack is spared " + (lbSackNeverGoes ? liRuns : 0) + " times of " + liRuns,
                fCountSpared(lOEmpty, pOProperties, liRuns), lbSackNeverGoes ? liRuns : 0);

            UWObject lOProtected = fBag(liSack);
            lOProtected.DoorDirection = true;
            fExpectInt("bag: word 0 bit 13 on the sack spares it every time", fCountSpared(lOProtected, pOProperties, liRuns), liRuns);

            UWObject lOWithProtected = fBag(liSack, new UWObject((ushort)liCheapId) { DoorDirection = true });
            fExpectInt("bag: ... and bit 13 on something inside", fCountSpared(lOWithProtected, pOProperties, liRuns), liRuns);

            UWObject lOWithValuable = fBag(liSack, new UWObject((ushort)liHighId));
            fExpectInt("bag: a valuable thing inside (0x" + liHighId.ToString("X") + ") spares it every time",
                fCountSpared(lOWithValuable, pOProperties, liRuns), liRuns);

            UWObject lOWithStack = fBag(liSack, new UWObject((ushort)liMiddleId) { HasQuantity = true, Quantity = 5 });
            fExpectInt("bag: five of priority 11 inside (11 + 2 beats 12) spare it every time",
                fCountSpared(lOWithStack, pOProperties, liRuns), liRuns);

            UWObject lOWithCheap = fBag(liSack, new UWObject((ushort)liCheapId));
            fExpectInt("bag: a cheap thing inside (0x" + liCheapId.ToString("X") + ") spares nothing",
                fCountSpared(lOWithCheap, pOProperties, liRuns), lbSackNeverGoes ? liRuns : 0);

            // One of priority 11 alone is spared at the ranges 10 only - about a third.
            if (!lbSackNeverGoes)
            {
                UWObject lOWithOne = fBag(liSack, new UWObject((ushort)liMiddleId));
                int liSpared = fCountSpared(lOWithOne, pOProperties, 3000);

                fExpectBool("bag: one of priority 11 inside is spared about a third of the time (" + liSpared + " of 3000)",
                    liSpared > 700 && liSpared < 1300, true);
            }

            // THE DRAWS, as the motion core makes them: one for the range; a condemned object
            // draws the second roll as well, a spared one does not.
            int liDraws = 0;
            System.Func<int> lfCounting = () => { liDraws++; return 0; };

            UWObjectLimitRules.CullingTest(lOProtected, UWLiquidCulling.RangeBase, pOProperties, null, lfCounting);
            fExpectInt("bag: a spared object draws once (the range)", liDraws, 1);
            liDraws = 0;
            UWObjectLimitRules.CullingTest(lOWithCheap, UWLiquidCulling.RangeBase, pOProperties, null, lfCounting);
            fExpectInt("bag: a condemned object draws twice (range, then the second roll)", liDraws, lbSackNeverGoes ? 1 : 2);
            liDraws = 0;
            fExpectBool("bag: a draw of 0 gives the range 10, which the second roll of 0 is below (culled)",
                UWObjectLimitRules.CullingTest(new UWObject((ushort)liCheapId), UWLiquidCulling.RangeBase, pOProperties, null, lfCounting), true);
            fExpectBool("bag: at the range 12 (a draw of 0x7FFF) a priority of 13 still stays",
                UWObjectLimitRules.IsTenacious(new UWObject((ushort)liHighId), UWLiquidCulling.RangeBase + 2, pOProperties), true);
            fExpectBool("bag: ... and 11 does not",
                UWObjectLimitRules.IsTenacious(new UWObject((ushort)liMiddleId), UWLiquidCulling.RangeBase + 2, pOProperties), false);
            fExpectBool("bag: ... but 11 does at the range 10",
                UWObjectLimitRules.IsTenacious(new UWObject((ushort)liMiddleId), UWLiquidCulling.RangeBase, pOProperties), true);
        }

        private static UWObject fBag(int piId, params UWObject[] pOContents)
        {
            UWObject lOBag = new UWObject((ushort)piId) { Quantity = 1 };
            List<UWObject> lOMaster = new List<UWObject> { new UWObject(0) };

            lOMaster.AddRange(pOContents);
            lOBag.EnsureContentsLoaded(lOMaster);

            return lOBag;
        }

        private static int fCountSpared(UWObject pOBag, UWCommonObjectProperties pOProperties, int piRuns)
        {
            int liSpared = 0;

            for (int liRun = 0; liRun < piRuns; liRun++)
                if (UWObjectDamageRules.DestroyedBagIsSpared(pOBag, pOProperties, null))
                    liSpared++;

            return liSpared;
        }

        /// <summary>The first object id whose culling priority lies in the range, or -1.</summary>
        private static int fFindIdWithPriority(UWCommonObjectProperties pOProperties, int piLow, int piHigh)
        {
            for (int liId = 0; liId < 0x200; liId++)
            {
                UWCommonObjectProperties.Entry lOEntry;

                if (pOProperties.TryGet(liId, out lOEntry) && lOEntry.CullingPriority >= piLow && lOEntry.CullingPriority <= piHigh)
                    return liId;
            }

            return -1;
        }
    }
}
