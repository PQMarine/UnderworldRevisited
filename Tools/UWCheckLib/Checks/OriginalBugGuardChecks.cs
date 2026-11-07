using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// BUGS OF THE ORIGINAL THAT OURS ALREADY AVOIDS (bug plan D and E1, 2026-11-03): guards, so
    /// that a later change cannot bring them back.
    ///
    /// D, THE DISK VERSION'S INVENTORY BUG: there a full level could cost carried objects on a level
    /// change. Ours keeps the inventory apart; arriving, it takes its numbers and culls only far,
    /// worthless world objects to make room (UWLevelChangeRules.Arrive, UWObjectLimitRules.
    /// MakeRoomForInventory), as the CD version does.
    ///
    /// E1, THE STACK SPLIT LEAK (UnderworldExhumed issue 9): the original cuts a floor stack for
    /// "Move how many?" before it weighs the part, and on "too heavy" the new record of the cut
    /// stays allocated and unreachable - "Problems in object list" on loading. Ours weighs first
    /// (UWWorldPickup.Take: UWPickupRules.Check before UWInventoryModel.TakeFromStack), and the
    /// level writer numbers world objects from the reachable ones and the file's free list, so a
    /// split never leaves a slot that is neither free nor reachable.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckOriginalBugGuards(CheckContext pOContext)
        {
            Check.Group("Bugs of the original already avoided (bug plan D, E1)");

            DataImport lOData = pOContext.Data;
            UWLevel lOLevel = lOData.Levels != null && lOData.Levels.Count > 1 ? lOData.Levels[1] : null;

            if (lOLevel == null || lOLevel.TileData == null || lOData.CommonObjectProperties == null)
            {
                Check.Fail("bug guards", "no level 2 or no COMOBJ.DAT");

                return;
            }

            // E2 first: it builds blocks from the level as loaded, which D and E1 then change.
            fCheckLeakedCopyTidy(pOContext, lOLevel);
            fCheckDebrisSpell(lOData.CommonObjectProperties);
            fCheckFullLevelInventoryGuard(lOData, lOLevel);
            fCheckStackSplitGuard(pOContext, lOData, lOLevel);
        }

        /// <summary>
        /// E2: an artificial leak in the block of level 2 as the original leaves one - a static slot off
        /// the free list holding a copy of a stack lying in a tile, itself in no tile - and an unknown
        /// orphan (the same slot holding no stack). With deviations.tidyLeakedObjects the writer frees
        /// the copy on top of the free list and the balance is whole again; without it the block stays
        /// as it was; the unknown orphan stays either way, with a warning.
        /// </summary>
        private static void fCheckLeakedCopyTidy(CheckContext pOContext, UWLevel pOLevel)
        {
            string lsArk = pOContext.DataPath != null ? Path.Combine(pOContext.DataPath, "LEV.ARK") : null;

            if (lsArk == null || !File.Exists(lsArk) || UWRules.Current == null)
            {
                Check.Fail("bug guard E2", "no LEV.ARK or no rules");

                return;
            }

            byte[] lyArk = File.ReadAllBytes(lsArk);
            uint luOffset = BitConverter.ToUInt32(lyArk, 2 + (UWLevArkFile.Uw1.MapBlock(pOLevel.LevelNumber - 1) * 4));
            byte[] lyClean = new byte[UWLevArkFile.Uw1.BlockSize];

            Array.Copy(lyArk, (int)luOffset, lyClean, 0, lyClean.Length);

            // A stack lying in a tile to copy, and the top of the static free list to leak into.
            HashSet<UWObject> lOInTiles = new HashSet<UWObject>();

            foreach (UWTile lOTile in pOLevel.TileData)
            {
                if (lOTile != null && lOTile.ObjectsInTile != null)
                    lOInTiles.UnionWith(lOTile.ObjectsInTile);
            }

            int liStack = -1;

            for (int liIndex = UWLevArkLayout.MobileCount; liIndex < UWLevArkLayout.ObjectCount && liStack < 0; liIndex++)
            {
                int liAt = UWLevArkLayout.ObjectOffset(liIndex);
                int liCount = UWObjectRecordCodec.ReadQuantityOrLink(lyClean, liAt);

                if (UWObjectRecordCodec.ReadIsQuantity(lyClean, liAt) && liCount > 1 && liCount < UWStackRules.SpecialPropertyThreshold
                    && pOLevel.Masterlist != null && liIndex < pOLevel.Masterlist.Count && lOInTiles.Contains(pOLevel.Masterlist[liIndex]))
                    liStack = liIndex;
            }

            List<int> lOFree = UWLevArkCodec.ReadStaticFreeList(lyClean, 0);

            if (liStack < 0 || lOFree.Count == 0)
            {
                Check.Fail("bug guard E2", "level 2 has no stack in a tile or no free static slot");

                return;
            }

            int liLeak = lOFree[lOFree.Count - 1];
            int liLeakAt = UWLevArkLayout.ObjectOffset(liLeak);
            byte[] lyLeaked = (byte[])lyClean.Clone();

            Array.Copy(lyLeaked, UWLevArkLayout.ObjectOffset(liStack), lyLeaked, liLeakAt, UWLevArkLayout.StaticRecordSize);
            UWBinary.Write16(lyLeaked, liLeakAt + 4, UWBinary.Read16(lyLeaked, liLeakAt + 4) & 0x3F);
            lOFree.RemoveAt(lOFree.Count - 1);
            UWLevArkCodec.WriteStaticFreeList(lyLeaked, 0, lOFree);

            List<int> lOUnknown;

            Check.Str("guard E2: the leaked copy of stack " + liStack + " in slot " + liLeak + " is found, nothing else",
                string.Join(",", UWLevelWriter.FindLeakedCopies(lyLeaked, null, out lOUnknown)) + " / " + string.Join(",", lOUnknown),
                liLeak + " / ");
            Check.Int("guard E2: ... the leaked block is one short of the balance", fSlotBalance(lyLeaked), fSlotBalance(lyClean) - 1);

            UWRulesDeviations lODeviations = UWRules.Current.Deviations;
            bool lbBefore = lODeviations.TidyLeakedObjects;

            try
            {
                lODeviations.TidyLeakedObjects = true;

                byte[] lyTidied = UWLevelWriter.BuildBlock(pOLevel, lyLeaked);
                List<int> lOFreeAfter = UWLevArkCodec.ReadStaticFreeList(lyTidied, 0);

                Check.Int("guard E2: saved with the fix, the balance is whole", fSlotBalance(lyTidied), fSlotBalance(lyClean));
                Check.Int("guard E2: ... the copy's slot on top of the free list",
                    lOFreeAfter.Count > 0 ? lOFreeAfter[lOFreeAfter.Count - 1] : -1, liLeak);

                lODeviations.TidyLeakedObjects = false;
                Check.Bool("guard E2: saved without it, the block stays as the original wrote it",
                    fSameBytes(UWLevelWriter.BuildBlock(pOLevel, lyLeaked), lyLeaked), true);

                // An orphan that holds no stack: left alone and reported, with the fix on.
                byte[] lyUnknown = (byte[])lyLeaked.Clone();

                UWBinary.Write16(lyUnknown, liLeakAt, UWBinary.Read16(lyUnknown, liLeakAt) & ~UWObjectRecordCodec.IsQuantityBit);
                UWBinary.Write16(lyUnknown, liLeakAt + 6, 0);
                lODeviations.TidyLeakedObjects = true;
                UWLevelWriter.Warnings.Clear();

                byte[] lyKept = UWLevelWriter.BuildBlock(pOLevel, lyUnknown);

                Check.Bool("guard E2: an orphan that holds no stack stays, with a warning",
                    fSameBytes(lyKept, lyUnknown) && UWLevelWriter.Warnings.Exists(psLine => psLine.Contains("neither free nor reachable")), true);
                UWLevelWriter.Warnings.Clear();
            }
            finally
            {
                lODeviations.TidyLeakedObjects = lbBefore;
            }
        }

        /// <summary>
        /// THE DEBRIS THAT KEEPS A POTION'S SPELL (UWObjectDamageRules.DebrisLosesSpellApplied,
        /// 2026-11-07): a potion as the levels carry it (the quantity bit, flags 12 with the enchanted
        /// bit, 512 plus rune spell 30 in the quantity field) becomes a fresh pile, as the blast and a
        /// smashed thing leave one, and a pile in place, as a thing in flight becomes one. With
        /// deviations.debrisLosesSpell no pile carries a spell; without it both carry the potion's
        /// spell, uncharged, as the original's SET_ITEM keeps it. A pile of something without the
        /// quantity bit (a wand) keeps nothing either way.
        /// </summary>
        private static void fCheckDebrisSpell(UWCommonObjectProperties pOCommon)
        {
            if (UWRules.Current == null)
            {
                Check.Fail("debris spell", "no rules");

                return;
            }

            const int Potion = 0xBB;
            const int Wand = 0x98;
            const int SpellField = 512 + 30;
            int liDebris = UWObjectDamageRules.FirstDebrisObjectId;

            UWObject fPotion()
            {
                UWObject lOPotion = UWObjectFactory.Create(Potion, pOCommon);

                lOPotion.HasQuantity = true;
                lOPotion.Quantity = SpellField;
                lOPotion.Flags = 12;

                return lOPotion;
            }

            string fSpellOf(UWObject pOPile)
            {
                UWObject lOSpell = UWObjectMechanics.FindSpellObject(pOPile, null);

                return lOSpell != null ? "spell " + (lOSpell.Quantity & 0x3F) : "none";
            }

            UWRulesDeviations lODeviations = UWRules.Current.Deviations;
            bool lbBefore = lODeviations.DebrisLosesSpell;

            try
            {
                Check.Str("debris spell: the potion itself carries rune spell 30", fSpellOf(fPotion()), "spell 30");

                foreach (bool lbFix in new[] { false, true })
                {
                    lODeviations.DebrisLosesSpell = lbFix;

                    string lsExpected = lbFix ? "none" : "spell 30";
                    string lsWhich = lbFix ? "with the fix" : "as the original";
                    UWObject lOFresh = UWObjectFactory.Create(liDebris, pOCommon);

                    UWObjectDamageRules.CarryIntoPile(fPotion(), lOFresh, pOCommon);
                    Check.Str("debris spell " + lsWhich + ": the fresh pile of a blast", fSpellOf(lOFresh), lsExpected);

                    UWObject lOInPlace = fPotion();

                    lOInPlace.ID = (ushort)liDebris;
                    UWObjectDamageRules.DropCarriedSpell(lOInPlace, pOCommon);
                    Check.Str("debris spell " + lsWhich + ": the pile a thing in flight becomes", fSpellOf(lOInPlace), lsExpected);

                    UWObject lOFreshPile = UWObjectFactory.Create(liDebris, pOCommon);

                    Check.Bool("debris spell " + lsWhich + ": ... the dropped spell leaves a plain pile's quantity",
                        !lbFix || (lOInPlace.HasQuantity == lOFreshPile.HasQuantity && lOInPlace.Quantity == lOFreshPile.Quantity
                            && !lOInPlace.IsEnchanted), true);

                    UWObject lOWand = UWObjectFactory.Create(Wand, pOCommon);
                    UWObject lOWandPile = UWObjectFactory.Create(liDebris, pOCommon);

                    lOWand.HasQuantity = false;
                    lOWand.Quantity = 5;
                    UWObjectDamageRules.CarryIntoPile(lOWand, lOWandPile, pOCommon);
                    Check.Str("debris spell " + lsWhich + ": a wand's pile keeps nothing", fSpellOf(lOWandPile), "none");
                }
            }
            finally
            {
                lODeviations.DebrisLosesSpell = lbBefore;
            }
        }

        /// <summary>
        /// D: level 2 filled to the last static slot, far junk and far keys among the filling, near
        /// junk around the player; a large inventory (a sack of twenty and thirty loose things, keys
        /// among them) arrives. No carried object goes, nothing near the player goes, no key goes,
        /// nothing that holds against the highest culling range goes, and afterwards level and
        /// inventory fit the static slots.
        /// </summary>
        private static void fCheckFullLevelInventoryGuard(DataImport pOData, UWLevel pOLevel)
        {
            UWCommonObjectProperties lOProperties = pOData.CommonObjectProperties;
            UWWorldEdit lOEdit = new UWWorldEdit(new UWTileQueries(pOLevel, pOData));
            int liHighestRange = UWObjectLimitRules.CullRange + UWLiquidCulling.RangeDice - 1;
            int liJunk = fFindCullableId(lOProperties, liHighestRange);
            UWTilePos lOPlayer = fFirstOpenTile(pOLevel, 32, 32);

            if (liJunk < 0 || !lOPlayer.IsOnMap)
            {
                Check.Fail("bug guard D", "no cullable object id or no open tile on level 2");

                return;
            }

            Check.Bool("guard D: a key holds against the highest culling range",
                UWObjectLimitRules.IsTenacious(new UWObject(0x100), liHighestRange, lOProperties), true);

            // The level to its last static slot: near junk first, then far junk with a key every 25th.
            int liMobile;
            int liStatic;

            UWObjectLimitRules.CountUsage(pOLevel, out liMobile, out liStatic);

            List<UWObject> lONear = new List<UWObject>();
            List<UWObject> lOFarKeys = new List<UWObject>();
            int liToFill = UWObjectLimitRules.StaticSlots - liStatic;
            int liPlaced = 0;

            for (int liY = 0; liY < UWWorldScale.TilesPerAxis && liPlaced < liToFill; liY++)
            {
                for (int liX = 0; liX < UWWorldScale.TilesPerAxis && liPlaced < liToFill; liX++)
                {
                    UWTile lOTile = pOLevel.GetTile(liX, liY);

                    if (lOTile == null || lOTile.TileType != UWTile.TileTypeEnum.open)
                        continue;

                    bool lbFar = UWObjectLimitRules.TileQualifies(liX, liY, lOPlayer.X, lOPlayer.Y, UWObjectLimitRules.CullRange);

                    for (int liPer = 0; liPer < 4 && liPlaced < liToFill; liPer++)
                    {
                        bool lbKey = lbFar && liPlaced % 25 == 24;
                        UWObject lOFill = UWObjectFactory.Create(lbKey ? 0x100 : liJunk, lOProperties);

                        if (lOEdit.Place(lOFill, liX, liY, 3, 3, 0, false, UWWorldEdit.Arrival.Created)
                            != UWWorldEdit.PlaceResult.Placed)
                            continue;

                        liPlaced++;

                        if (!lbFar)
                            lONear.Add(lOFill);
                        else if (lbKey)
                            lOFarKeys.Add(lOFill);
                    }
                }
            }

            UWObjectLimitRules.CountUsage(pOLevel, out liMobile, out liStatic);
            Check.Int("guard D: level 2 holds an object in every static slot", liStatic, UWObjectLimitRules.StaticSlots);

            // The inventory: a sack of twenty, thirty loose things, every tenth a key.
            UWObject lOSack = UWObjectFactory.Create(UWObjectIds.FirstContainer, lOProperties);

            lOSack.HasQuantity = false;
            lOSack.Quantity = 0;
            lOSack.EnsureContentsLoaded(pOLevel.Masterlist);

            for (int liAt = 0; liAt < 20; liAt++)
                lOSack.Contents.Add(UWObjectFactory.Create(1 + (liAt % 2), lOProperties));

            List<UWObject> lOChain = new List<UWObject> { lOSack };

            for (int liAt = 0; liAt < 30; liAt++)
                lOChain.Add(UWObjectFactory.Create(liAt % 10 == 9 ? 0x100 : 1, lOProperties));

            HashSet<UWObject> lOCarried = new HashSet<UWObject>(lOChain);

            lOCarried.UnionWith(lOSack.Contents);

            int liCarried = UWObjectLimitRules.CountObjects(lOChain);
            List<UWObject> lORemoved = new List<UWObject>();
            List<UWTilePos> lORemovedAt = new List<UWTilePos>();

            lOEdit.ObjectRemoved += pOObject =>
            {
                lORemoved.Add(pOObject);
                lORemovedAt.Add(new UWTilePos(pOObject.TileX, pOObject.TileY));
            };

            int liCulled = UWLevelChangeRules.Arrive(lOEdit, lOChain, liCarried, lOPlayer.X, lOPlayer.Y);

            Check.Bool("guard D: arriving with " + liCarried + " carried objects in a full level culls (" + liCulled + ")",
                liCulled > 0 && liCulled == lORemoved.Count, true);
            Check.Bool("guard D: ... no carried object among them", lORemoved.TrueForAll(pOObject => !lOCarried.Contains(pOObject)), true);
            Check.Bool("guard D: ... the inventory whole, the sack with its twenty",
                lOChain.Count == 31 && lOSack.Contents.Count == 20 && UWObjectLimitRules.CountObjects(lOChain) == liCarried, true);

            bool lbFarOnly = true;

            for (int liAt = 0; liAt < lORemovedAt.Count; liAt++)
                lbFarOnly &= UWObjectLimitRules.TileQualifies(lORemovedAt[liAt].X, lORemovedAt[liAt].Y, lOPlayer.X, lOPlayer.Y,
                    UWObjectLimitRules.CullRange);

            Check.Bool("guard D: ... only from tiles beyond the culling distance", lbFarOnly, true);
            Check.Bool("guard D: ... nothing near the player", lONear.TrueForAll(pOObject => !lORemoved.Contains(pOObject)), true);
            Check.Bool("guard D: ... no key of the level (" + lOFarKeys.Count + " far ones)",
                lOFarKeys.Count > 0 && lOFarKeys.TrueForAll(pOObject => !lORemoved.Contains(pOObject)), true);
            Check.Bool("guard D: ... nothing that holds against the highest culling range",
                lORemoved.TrueForAll(pOObject => !UWObjectLimitRules.IsTenacious(pOObject, liHighestRange, lOProperties)), true);

            UWObjectLimitRules.CountUsage(pOLevel, out liMobile, out liStatic);
            Check.Bool("guard D: ... until level and inventory fit (" + liStatic + " + " + liCarried + ")",
                liStatic + liCarried <= UWObjectLimitRules.StaticSlots, true);

            UWLevelChangeRules.Leave(lOEdit, lOChain);
        }

        /// <summary>
        /// E1: a floor stack of sling stones on level 2, the player able to carry a few. Taking more
        /// than he can is refused before anything is cut, and the saved block stays the same; taking
        /// one cuts the stack, keeps the count, and the saved block holds no slot that is neither
        /// free nor reachable - nor after the piece is put down elsewhere.
        /// </summary>
        private static void fCheckStackSplitGuard(CheckContext pOContext, DataImport pOData, UWLevel pOLevel)
        {
            string lsArk = pOContext.DataPath != null ? Path.Combine(pOContext.DataPath, "LEV.ARK") : null;

            if (lsArk == null || !File.Exists(lsArk) || pOData.InitialPlayer == null)
            {
                Check.Fail("bug guard E1", "no LEV.ARK or no player");

                return;
            }

            UWCommonObjectProperties lOProperties = pOData.CommonObjectProperties;
            byte[] lyArk = File.ReadAllBytes(lsArk);
            uint luOffset = BitConverter.ToUInt32(lyArk, 2 + (UWLevArkFile.Uw1.MapBlock(pOLevel.LevelNumber - 1) * 4));
            byte[] lyOriginal = new byte[UWLevArkFile.Uw1.BlockSize];

            Array.Copy(lyArk, (int)luOffset, lyOriginal, 0, lyOriginal.Length);

            // A character with 400 tenth-stones, as in the pickup check; the runner reloads afterwards.
            byte[] lyPlain = (byte[])pOData.InitialPlayer.PlainBytes.Clone();

            UWBinary.Write16(lyPlain, 0x4C, 400);
            pOData.ReplaceInitialPlayer(UWPlayerData.FromPlain(lyPlain));

            UWInventoryModel lOModel = new UWInventoryModel(() => pOData, () => null);
            int liStone = UWObjectMechanics.SlingStoneObjectId;
            int liCapacity = pOData.InitialPlayer.MaxWeight - lOModel.GetCarriedTenthStones(lOProperties);
            UWCommonObjectProperties.Entry lOEntry;

            if (!lOProperties.TryGet(liStone, out lOEntry) || lOEntry.MassTenthStones <= 0 || liCapacity <= 0)
            {
                Check.Fail("bug guard E1", "no sling stone mass or no capacity");

                return;
            }

            UWWorldEdit lOEdit = new UWWorldEdit(new UWTileQueries(pOLevel, pOData));
            UWTilePos lOAt = fFirstOpenTile(pOLevel, 32, 32);
            UWTilePos lOElsewhere = fFirstOpenTile(pOLevel, lOAt.X + 1, lOAt.Y);
            int liStack = (liCapacity / lOEntry.MassTenthStones) + 3;
            UWObject lOStack = UWObjectFactory.Create(liStone, lOProperties);

            lOStack.HasQuantity = true;
            lOStack.Quantity = (ushort)liStack;

            if (!lOAt.IsOnMap || !lOElsewhere.IsOnMap
                || lOEdit.Place(lOStack, lOAt.X, lOAt.Y, 3, 3, 0, false,
                    UWWorldEdit.Arrival.Created) != UWWorldEdit.PlaceResult.Placed)
            {
                Check.Fail("bug guard E1", "the stack found no open tile on level 2");

                return;
            }

            UWLevel lOCarriedBefore = UWCarriedSlots.Level;

            UWCarriedSlots.Level = pOLevel;

            try
            {
                byte[] lyBefore = UWLevelWriter.BuildBlock(pOLevel, lyOriginal);
                int liBalance = fSlotBalance(lyBefore);
                int liReach = fReachableSlots(lyBefore);

                // Too heavy: refused by the weight check, which comes before any cut.
                int liTooMany = liStack - 1;

                Check.Int("guard E1: " + liTooMany + " of " + liStack + " stones near the weight limit are too heavy",
                    (int)UWPickupRules.Check(lOStack, liTooMany, true, lOModel).Outcome, (int)UWPickupRules.Outcome.TooHeavy);
                Check.Int("guard E1: ... the stack untouched", lOStack.Quantity, liStack);
                Check.Bool("guard E1: ... and the saved block is the same as before",
                    fSameBytes(UWLevelWriter.BuildBlock(pOLevel, lyOriginal), lyBefore), true);

                // One stone fits: the cut keeps the count, and no slot leaks.
                Check.Bool("guard E1: one stone can be taken", UWPickupRules.Check(lOStack, 1, true, lOModel).CanTake, true);
                Check.Bool("guard E1: ... cut from the stack", lOModel.TakeFromStack(lOStack, 1), true);
                Check.Int("guard E1: ... stack and pointer hold the whole count",
                    lOStack.Quantity + (lOModel.CursorItem != null ? lOModel.CursorItem.Quantity : 0), liStack);

                byte[] lyCut = UWLevelWriter.BuildBlock(pOLevel, lyOriginal);

                Check.Int("guard E1: ... the saved block: reachable plus free slots as before", fSlotBalance(lyCut), liBalance);
                Check.Int("guard E1: ... the same objects reachable (the piece is on the pointer)", fReachableSlots(lyCut), liReach);

                // Put down on the next tile: one slot more reachable, one less free, the balance held.
                UWObject lOPiece = lOModel.CursorItem;

                Check.Bool("guard E1: the piece put down elsewhere",
                    lOPiece != null && lOEdit.Place(lOPiece, lOElsewhere.X, lOElsewhere.Y, 3, 3,
                        0, false, UWWorldEdit.Arrival.PutDown)
                    == UWWorldEdit.PlaceResult.Placed, true);

                byte[] lyDown = UWLevelWriter.BuildBlock(pOLevel, lyOriginal);

                Check.Int("guard E1: ... the saved block: one more reachable", fReachableSlots(lyDown), liReach + 1);
                Check.Int("guard E1: ... reachable plus free slots as before", fSlotBalance(lyDown), liBalance);

                lOEdit.Remove(lOPiece);
                lOEdit.Remove(lOStack);
            }
            finally
            {
                UWCarriedSlots.Level = lOCarriedBefore;
            }
        }

        /// <summary>An object id the culling test can take even at its highest range, for filler.</summary>
        private static int fFindCullableId(UWCommonObjectProperties pOProperties, int piHighestRange)
        {
            for (int liId = 0xB0; liId < 0x140; liId++)
            {
                UWCommonObjectProperties.Entry lOEntry;

                if (!pOProperties.TryGet(liId, out lOEntry))
                    continue;

                if (!UWObjectLimitRules.IsTenacious(new UWObject((ushort)liId), piHighestRange, pOProperties))
                    return liId;
            }

            return -1;
        }

        /// <summary>The first open tile from the given one on, row by row; off the map when none.</summary>
        private static UWTilePos fFirstOpenTile(UWLevel pOLevel, int piFromX, int piFromY)
        {
            for (int liAt = (piFromY * UWWorldScale.TilesPerAxis) + piFromX; liAt < UWLevArkLayout.TileCount; liAt++)
            {
                int liX = liAt % UWWorldScale.TilesPerAxis;
                int liY = liAt / UWWorldScale.TilesPerAxis;
                UWTile lOTile = pOLevel.GetTile(liX, liY);

                if (lOTile != null && lOTile.TileType == UWTile.TileTypeEnum.open)
                    return new UWTilePos(liX, liY);
            }

            return new UWTilePos(-1, -1);
        }

        /// <summary>Slots a block reaches from its tiles - chains and, without the quantity bit,
        /// the special link - as the original's object list check walks them.</summary>
        private static int fReachableSlots(byte[] pyBlock)
        {
            HashSet<int> lOReach = new HashSet<int>();
            Stack<int> lOStack = new Stack<int>();

            for (int liTile = 0; liTile < UWLevArkLayout.TileCount; liTile++)
            {
                int liHead = (int)(BitConverter.ToUInt32(pyBlock, liTile * UWLevArkLayout.TileRecordSize)
                    >> UWLevArkLayout.TileFirstObjectShift);

                if (liHead != 0)
                    lOStack.Push(liHead);
            }

            while (lOStack.Count > 0)
            {
                int liIndex = lOStack.Pop();

                if (liIndex <= 0 || liIndex >= UWLevArkLayout.ObjectCount || !lOReach.Add(liIndex))
                    continue;

                int liOffset = UWLevArkLayout.ObjectOffset(liIndex);
                int liNext = UWObjectRecordCodec.ReadLink(pyBlock, liOffset);
                int liSpecial = UWObjectRecordCodec.ReadQuantityOrLink(pyBlock, liOffset);

                if (liNext != 0)
                    lOStack.Push(liNext);

                if (!UWObjectRecordCodec.ReadIsQuantity(pyBlock, liOffset) && liSpecial != 0)
                    lOStack.Push(liSpecial);
            }

            return lOReach.Count;
        }

        /// <summary>Reachable slots plus both free lists: a leak lowers it, a slot both free and
        /// reachable raises it.</summary>
        private static int fSlotBalance(byte[] pyBlock)
        {
            return fReachableSlots(pyBlock) + UWLevArkCodec.ReadMobileFreeList(pyBlock, 0).Count
                + UWLevArkCodec.ReadStaticFreeList(pyBlock, 0).Count;
        }
    }
}
