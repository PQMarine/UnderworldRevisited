using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The inventory-side rules the cleanup touches first (Docs/CLEANUP-PLAN.md, phase 1 step
    /// "self-check additions"): the hand's reach, the items applied to things, the locks, the
    /// inventory model's carrying, splitting, consuming and putting away, and eating. Pinned
    /// here before UWPickupRules, UWItemTargetRules and UWDoorRules take these over, so the
    /// move cannot change a number.
    /// </summary>
    public static partial class Program
    {
        // ------------------------------------------------- reach

        /// <summary>UWReachRules against the user's measurement in the original (save 4, level
        /// 2, 2026-09-18): eleven eighths reach the sign, twelve do not.</summary>
        private static void fCheckReach()
        {
            Check.Group("Reach (UWReachRules, the hand on the tile grid)");

            int liPlayerX = UWReachRules.ToEighths(36, 4);
            int liPlayerY = UWReachRules.ToEighths(16, 4);
            int liSignX = UWReachRules.ToEighths(37, 7);
            int liSignY = liPlayerY + 1;
            int liHand = UWReachRules.UseDistanceSquared;

            Check.Int("reach: eighths of a tile", UWReachRules.ToEighths(36, 4), 292);
            Check.Bool("reach: the sign eleven eighths away is used (122 <= 144)",
                UWReachRules.CanReach(liPlayerX, liPlayerY, 40, liSignX, liSignY, 40, liHand, false), true);
            Check.Bool("reach: one step back, twelve eighths, is refused (145 > 144)",
                UWReachRules.CanReach(liPlayerX - 1, liPlayerY, 40, liSignX, liSignY, 40, liHand, false), false);
            Check.Bool("reach: 24 zpos steps above the feet still count",
                UWReachRules.CanReach(0, 0, 40, 0, 0, 64, liHand, false), true);
            Check.Bool("reach: 25 above do not",
                UWReachRules.CanReach(0, 0, 40, 0, 0, 65, liHand, false), false);
            Check.Bool("reach: 12 below still count",
                UWReachRules.CanReach(0, 0, 40, 0, 0, 28, liHand, false), true);
            Check.Bool("reach: 13 below do not",
                UWReachRules.CanReach(0, 0, 40, 0, 0, 27, liHand, false), false);
            Check.Bool("reach: the pole reaches twenty eighths",
                UWReachRules.CanReach(0, 0, 40, 20, 0, 40, UWReachRules.PoleDistanceSquared, true), true);
            Check.Bool("reach: ... and twice the height window",
                UWReachRules.CanReach(0, 0, 40, 0, 0, 88, UWReachRules.PoleDistanceSquared, true), true);
            Check.Bool("reach: twenty-one eighths are beyond the pole",
                UWReachRules.CanReach(0, 0, 40, 21, 0, 40, UWReachRules.PoleDistanceSquared, true), false);
            Check.Bool("reach: Telekinesis reaches anything",
                UWReachRules.CanReach(0, 0, 0, 500, 500, 127, UWReachRules.UnlimitedDistance, false), true);
            Check.Int("reach: the pole beats Telekinesis",
                UWReachRules.GetDistanceSquared(true, true), UWReachRules.PoleDistanceSquared);
            Check.Int("reach: Telekinesis alone is unlimited",
                UWReachRules.GetDistanceSquared(false, true), UWReachRules.UnlimitedDistance);
            Check.Int("reach: taking reaches as far as the bare hand",
                UWReachRules.GetPickupDistanceSquared(false), UWReachRules.UseDistanceSquared);
            Check.Int("reach: ... unless Telekinesis",
                UWReachRules.GetPickupDistanceSquared(true), UWReachRules.UnlimitedDistance);

            // A swimmer reaches as anyone (InPickRange has no swimming term in UW1, A52,
            // 2026-10-15): the call takes no swim counter any more, and 23 below stays out of the
            // window of 12.
            Check.Bool("reach: 23 below the feet are out of reach, swimming or not",
                UWReachRules.CanReach(0, 0, 40, 0, 0, 17, liHand, false), false);

            // A close look (player_3dlook's 0x48, A46): a window's picture from eight eighths,
            // not from nine.
            Check.Bool("look: a window eight eighths away shows its picture",
                UWReachRules.CanReach(0, 0, 40, 8, 0, 40, UWReachRules.LookDetailDistanceSquared, false), true);
            Check.Bool("look: ... nine eighths away does not",
                UWReachRules.CanReach(0, 0, 40, 9, 0, 40, UWReachRules.LookDetailDistanceSquared, false), false);
        }

        // ------------------------------------------------- items applied to things

        /// <summary>UWItemApplications: the oil flask, the spike, the rock hammer and the orb rock,
        /// with the message numbers of string block 1 (our numbering).</summary>
        private static void fCheckItemApplications(DataImport pOData)
        {
            Check.Group("Item applications (UWItemApplications: oil, spike, hammer, orb rock)");

            UWObject lOWood = new UWObject((ushort)UWObjectMechanics.FirstWoodObjectId) { Quality = 17 };
            UWItemApplications.Result lOResult = UWItemApplications.ApplyOil(lOWood, pOData);

            Check.Bool("oil: wood is handled", lOResult.Handled, true);
            Check.Int("oil: wood becomes a torch", lOWood.ID, UWObjectMechanics.TorchObjectId);
            Check.Int("oil: ... which keeps the wood's quality (UseOilOn)", lOWood.Quality, 17);
            Check.Int("oil: ... with message 182", lOResult.MessageIndex, 182);

            // The seed from the tree carries word 0 bit 13 (UseObj, A42), and no liquid takes such
            // a thing, nor a bag holding it (chkTenacious, 2026-10-15).
            UWTileQueries lOLiquid = new UWTileQueries(null, pOData);
            UWObject lOSeed = new UWObject((ushort)UWSilverTreeRules.SeedObjectId) { DoorDirection = true };
            UWObject lOSeedBag = fBag(0x80, lOSeed);
            bool lbSeedSank = false;

            for (int liAt = 0; liAt < 200; liAt++)
                lbSeedSank |= lOLiquid.SinksInLiquid(lOSeed) || lOLiquid.SinksInLiquid(lOSeedBag);

            Check.Bool("liquid: a thing with word 0 bit 13 never sinks, nor a bag holding it", lbSeedSank, false);

            // go_fish: the water may not lie above the player (A49, 2026-10-15).
            Check.Bool("fishing: water a step below the player", UWFishingRules.WaterLowEnough(3, 2), true);
            Check.Bool("fishing: water level with the player", UWFishingRules.WaterLowEnough(3, 3), true);
            Check.Bool("fishing: water a step above the player is refused", UWFishingRules.WaterLowEnough(3, 4), false);
            Check.Bool("oil: ... and the flask is used up", lOResult.ItemUsed, true);

            UWObject lOLitTorch = new UWObject((ushort)(UWObjectMechanics.TorchObjectId + UWObjectMechanics.LitLightOffset));
            lOResult = UWItemApplications.ApplyOil(lOLitTorch, pOData);

            Check.Bool("oil: a burning torch refuses, message 183", lOResult.Handled && lOResult.MessageIndex == 183, true);
            Check.Bool("oil: ... and keeps the flask", lOResult.ItemUsed, false);

            UWObject lOTorch = new UWObject((ushort)UWObjectMechanics.TorchObjectId) { Quality = 20 };
            lOResult = UWItemApplications.ApplyOil(lOTorch, pOData);

            Check.Int("oil: an unlit torch of 20 fills by 32", lOTorch.Quality, 52);
            Check.Int("oil: ... with message 184", lOResult.MessageIndex, 184);
            Check.Bool("oil: ... the flask used up", lOResult.ItemUsed && lOResult.TargetChanged, true);

            lOTorch.Quality = (ushort)UWObjectMechanics.FullLightQuality;
            lOResult = UWItemApplications.ApplyOil(lOTorch, pOData);

            Check.Int("oil: a full torch says so, message 185", lOResult.MessageIndex, 185);
            Check.Bool("oil: ... and keeps the flask", lOResult.ItemUsed, false);

            UWObject lOLantern = new UWObject((ushort)UWObjectMechanics.LanternObjectId) { Quality = 40 };
            lOResult = UWItemApplications.ApplyOil(lOLantern, pOData);

            Check.Int("oil: a lantern of 40 fills to 63, not beyond", lOLantern.Quality, UWObjectMechanics.FullLightQuality);
            Check.Int("oil: ... with the lantern's message 180", lOResult.MessageIndex, 180);
            Check.Bool("oil: a dagger is not handled", UWItemApplications.ApplyOil(new UWObject(1), pOData).Handled, false);

            UWObject lODoor = new UWObject((ushort)UWObjectIds.FirstClosedDoor);
            lOResult = UWItemApplications.SpikeDoor(lODoor, false);

            Check.Int("spike: an open door only takes a closed one, message 129", lOResult.MessageIndex, 129);
            Check.Bool("spike: ... and keeps the spike", lOResult.ItemUsed, false);

            lOResult = UWItemApplications.SpikeDoor(lODoor, true);

            Check.Int("spike: a closed door is wedged, message 130", lOResult.MessageIndex, 130);
            Check.Bool("spike: ... the spike used up", lOResult.ItemUsed, true);
            Check.Bool("spike: ... and the door spiked", UWObjectMechanics.IsDoorSpiked(lODoor), true);
            Check.Int("spike: a fresh spike takes 31", UWObjectMechanics.GetDoorSpikeStrength(lODoor), 31);
            Check.Bool("spike: a blow of 10 goes to the spike", UWItemApplications.TryDamageDoorSpike(lODoor, true, 10), true);
            Check.Int("spike: ... which keeps 21", UWObjectMechanics.GetDoorSpikeStrength(lODoor), 21);
            Check.Bool("spike: an open door's spike takes nothing", UWItemApplications.TryDamageDoorSpike(lODoor, false, 10), false);
            Check.Bool("spike: a blow of 21 breaks it", UWItemApplications.TryDamageDoorSpike(lODoor, true, 21), true);
            // damage_object keeps owner bit 0 when the strength reaches 0 (owner 1): still
            // spiked to look at and to creatures, but the blows go to the door again.
            Check.Bool("spike: ... and the door still reads spiked", UWObjectMechanics.IsDoorSpiked(lODoor), true);
            Check.Int("spike: ... owner 1, strength 0", lODoor.Owner, 1);
            Check.Bool("spike: the next blow hits the door", UWItemApplications.TryDamageDoorSpike(lODoor, true, 5), false);
            Check.Int("spike: ... and leaves the owner at 1", lODoor.Owner, 1);

            // The spike takes the blow after the quality class shift (damage_object): a class 2
            // door turns a blow of 12 into 3; class 3 takes nothing at all.
            UWHealth lODoorHealth = new UWHealth();
            lODoorHealth.Initialise(40, 0, 0, 0, 0, -1, 2);
            Check.Int("spike: a class 2 door passes a quarter of the blow on", lODoorHealth.ScaleDamage(12, UWDamageTypes.Physical), 3);
            lODoorHealth.Initialise(40, 0, 0, 0, 0, -1, 3);
            Check.Bool("spike: a class 3 door takes no blow at all", lODoorHealth.CanTakeDamage, false);

            // UseRockHammerOn (USEITEMS.C): rand(0..1) + (0x155 - id) + 1 pieces, each id + 1 or
            // id + 2, a sling-stone pile of 3..8 above the small boulder (2026-10-15).
            fCheckBrokenBoulder(UWObjectIds.LargeBoulderA, 3, 4);
            fCheckBrokenBoulder(UWObjectIds.LargeBoulderB, 2, 3);
            fCheckBrokenBoulder(UWObjectIds.MediumBoulder, 1, 2);
            fCheckBrokenBoulder(UWObjectIds.SmallBoulder, 0, 1);

            UWItemApplications.RockResult lORock = UWItemApplications.BreakRock(UWObjectIds.FirstBoulder);

            Check.Int("hammer: a boulder breaks with message 136", lORock.MessageIndex, 136);
            Check.Int("hammer: anything else has no effect", UWItemApplications.BreakRock(100).MessageIndex, UWStringIds.NoEffectMessage);
            Check.Bool("hammer: ... breaks nothing", UWItemApplications.BreakRock(100).Broken, false);
            Check.Int("hammer: ... and spawns nothing", UWItemApplications.BreakRock(100).Pieces.Length, 0);

            lOResult = UWItemApplications.UseOrbRock(new UWObject((ushort)UWObjectMechanics.TyballOrbObjectId));

            Check.Int("orb rock: on the orb, message 134", lOResult.MessageIndex, 134);
            Check.Bool("orb rock: ... both are gone", lOResult.ItemUsed && lOResult.TargetChanged, true);

            lOResult = UWItemApplications.UseOrbRock(new UWObject(1));

            Check.Int("orb rock: on anything else no effect", lOResult.MessageIndex, UWStringIds.NoEffectMessage);
            Check.Bool("orb rock: ... and the rock survives", lOResult.ItemUsed, false);
        }

        /// <summary>A boulder broken 400 times: the piece count stays within its range and reaches
        /// both ends, every piece is one or two sizes smaller, and a stone pile holds 3 to 8.</summary>
        private static void fCheckBrokenBoulder(int piBoulder, int piFewest, int piMost)
        {
            bool lbInRange = true;
            bool lbPiecesFit = true;
            bool lbSawFewest = false;
            bool lbSawMost = false;

            for (int liAt = 0; liAt < 400; liAt++)
            {
                UWItemApplications.RockResult lORock = UWItemApplications.BreakRock(piBoulder);
                int liCount = lORock.Pieces.Length;

                lbInRange &= lORock.Broken && liCount >= piFewest && liCount <= piMost;
                lbSawFewest |= liCount == piFewest;
                lbSawMost |= liCount == piMost;

                foreach (UWItemApplications.RockPiece lOPiece in lORock.Pieces)
                {
                    if (lOPiece.ObjectId == UWObjectMechanics.SlingStoneObjectId)
                        lbPiecesFit &= piBoulder >= UWObjectIds.MediumBoulder && lOPiece.Quantity >= 3 && lOPiece.Quantity <= 8;
                    else
                        lbPiecesFit &= (lOPiece.ObjectId == piBoulder + 1 || lOPiece.ObjectId == piBoulder + 2)
                            && lOPiece.ObjectId <= UWObjectIds.SmallBoulder && lOPiece.Quantity == 0;
                }
            }

            string lsName = "hammer: boulder 0x" + piBoulder.ToString("X");

            Check.Bool(lsName + " breaks into " + piFewest + " to " + piMost + " pieces", lbInRange, true);
            Check.Bool(lsName + " ... both ends occur", lbSawFewest && lbSawMost, true);
            Check.Bool(lsName + " ... each one or two sizes smaller, stones 3 to 8", lbPiecesFit, true);
        }

        // ------------------------------------------------- locks

        /// <summary>A door with a lock in its chain, resolved into a lock state.</summary>
        private static UWLockState fMakeDoorLock(int piLockId, int piDifficulty, bool pbLocked, bool pbKeep, out UWObject pODoor)
        {
            pODoor = new UWObject((ushort)UWObjectIds.FirstClosedDoor);
            pODoor.EnsureContentsLoaded(null);

            UWObject lOLock = new UWObject((ushort)UWObjectMechanics.LockObjectId)
            {
                Quantity = (ushort)piLockId,
                ZPos = piDifficulty,
                Flags = (ushort)((pbLocked ? 1 : 0) | (pbKeep ? 2 : 0))
            };

            pODoor.Contents.Add(lOLock);

            UWLockState lOState = new UWLockState();

            lOState.EnsureResolved(pODoor, new List<UWObject>());

            return lOState;
        }

        private static UWObject fMakeKey(int piKeyId)
        {
            return new UWObject(0x100) { Owner = (ushort)piKeyId };
        }

        /// <summary>UWLockRules: the key, the pick and the Unlock spell on a door's lock and on a
        /// chest's, with the keep bit deciding whether the lock stays.</summary>
        private static void fCheckLockRules()
        {
            Check.Group("Locks (UWLockRules: key, lockpick, Unlock spell, the keep bit)");

            UWLockState lOState = fMakeDoorLock(5, 2, true, false, out UWObject lODoor);

            Check.Bool("locks: a locked lock reads locked", lOState.HasLock && lOState.IsLocked, true);
            Check.Int("locks: the fitting key unlocks it",
                (int)UWLockRules.Unlock(lOState, null, null, false, 5), (int)UWLockRules.Outcome.Unlocked);
            Check.Bool("locks: ... without the keep bit the lock is gone", lOState.HasLock, false);
            Check.Int("locks: a door without a lock answers NoLock",
                (int)UWLockRules.Unlock(lOState, null, null, false, 5), (int)UWLockRules.Outcome.NoLock);

            lOState = fMakeDoorLock(5, 2, true, true, out lODoor);

            Check.Int("locks: the wrong key fails",
                (int)UWLockRules.Unlock(lOState, null, null, false, 6), (int)UWLockRules.Outcome.Failed);
            Check.Bool("locks: ... and the lock stays locked", lOState.IsLocked, true);
            Check.Int("locks: the fitting key unlocks a kept lock",
                (int)UWLockRules.Unlock(lOState, null, null, false, 5), (int)UWLockRules.Outcome.Unlocked);
            Check.Bool("locks: ... which stays on the door, unlocked", lOState.HasLock && !lOState.IsLocked, true);
            Check.Int("locks: the key on an open door does nothing",
                (int)UWLockRules.Unlock(lOState, null, null, true, 5), (int)UWLockRules.Outcome.NotLocked);
            Check.Int("locks: the key on a closed unlocked door locks it again",
                (int)UWLockRules.Unlock(lOState, null, null, false, 5), (int)UWLockRules.Outcome.Locked);
            Check.Bool("locks: ... locked", lOState.IsLocked, true);
            Check.Int("locks: a wrong key cannot lock",
                (int)UWLockRules.Unlock(fMakeDoorLock(5, 2, false, true, out lODoor), null, null, false, 6), (int)UWLockRules.Outcome.Failed);
            Check.Int("locks: a lock of id 0 takes no key",
                (int)UWLockRules.Unlock(fMakeDoorLock(0, 2, true, true, out lODoor), null, null, false, 0), (int)UWLockRules.Outcome.Failed);

            Check.Int("locks: a non-key is no key for ToggleWithKey",
                UWLockRules.ToggleWithKey(fMakeDoorLock(5, 2, true, true, out lODoor), null, null, false, new UWObject(1)), -1);
            Check.Bool("locks: ToggleWithKey with a key gives a message line",
                UWLockRules.ToggleWithKey(fMakeDoorLock(5, 2, true, true, out lODoor), null, null, false, fMakeKey(5)) >= 0, true);

            // Picking: difficulty 15 never, 14 only from skill 31, else a skill check against three
            // times the difficulty.
            UWLockRules.PickResult lOPick = UWLockRules.PickLock(fMakeDoorLock(5, 15, true, true, out lODoor), null, null, 200);

            Check.Bool("pick: difficulty 15 cannot be picked", lOPick.Unlocked, false);
            Check.Int("pick: ... message 121", lOPick.MessageIndex, 121);
            Check.Bool("pick: difficulty 14 needs skill 31",
                UWLockRules.PickLock(fMakeDoorLock(5, 14, true, true, out lODoor), null, null, 30).Unlocked, false);

            UWRandom.Use(new FixedRandomSource(false));
            lOPick = UWLockRules.PickLock(fMakeDoorLock(5, 2, true, true, out lODoor), null, null, 50);
            Check.Bool("pick: skill 50 against difficulty 2 (score 44 on the lowest roll) opens it", lOPick.Unlocked, true);
            Check.Int("pick: ... message 122", lOPick.MessageIndex, 122);
            Check.Bool("pick: skill 6 against difficulty 2 (score 0 on the lowest roll) fails",
                UWLockRules.PickLock(fMakeDoorLock(5, 2, true, true, out lODoor), null, null, 6).Unlocked, false);
            UWRandom.Use(new FixedRandomSource(true));
            Check.Bool("pick: ... but on the highest roll (score 30) it opens",
                UWLockRules.PickLock(fMakeDoorLock(5, 2, true, true, out lODoor), null, null, 6).Unlocked, true);
            UWRandom.Use(null);

            lOPick = UWLockRules.PickLock(fMakeDoorLock(5, 2, false, true, out lODoor), null, null, 50);
            Check.Int("pick: an unlocked lock answers 123", lOPick.MessageIndex, UWLockRules.NotLockedMessage);
            Check.Int("pick: no lock answers 4", UWLockRules.PickLock(new UWLockState(), null, null, 50).MessageIndex, 4);

            UWRandom.Use(new FixedRandomSource(false));
            Check.Int("spell: Unlock picks with skill 45, message 271",
                UWLockRules.CastUnlock(fMakeDoorLock(5, 2, true, true, out lODoor), null, null), 271);
            Check.Int("spell: ... and has no effect on difficulty 15",
                UWLockRules.CastUnlock(fMakeDoorLock(5, 15, true, true, out lODoor), null, null), UWStringIds.SpellNoEffectMessage);
            UWRandom.Use(null);

            // A chest: the lock lies in its contents and goes with the keep bit cleared.
            UWObject lOChest = new UWObject((ushort)UWObjectMechanics.ChestObjectId);
            lOChest.EnsureContentsLoaded(null);

            UWObject lOChestLock = new UWObject((ushort)UWObjectMechanics.LockObjectId) { Quantity = 7, Flags = 1 };
            lOChest.Contents.Add(lOChestLock);

            Check.Bool("chest: the lock is found in the chain",
                UWLockRules.FindContainerLock(lOChest, new List<UWObject>()) == lOChestLock, true);
            Check.Int("chest: the fitting key unlocks",
                (int)UWLockRules.Unlock(null, lOChest, lOChestLock, false, 7), (int)UWLockRules.Outcome.Unlocked);
            Check.Bool("chest: ... and the lock without its keep bit is gone from the chest", lOChest.Contents.Contains(lOChestLock), false);

            lOChestLock = new UWObject((ushort)UWObjectMechanics.LockObjectId) { Quantity = 7, Flags = 3 };
            lOChest.Contents.Add(lOChestLock);
            UWLockRules.Unlock(null, lOChest, lOChestLock, false, 7);
            Check.Bool("chest: a kept lock stays, unlocked",
                lOChest.Contents.Contains(lOChestLock) && !UWObjectMechanics.IsLockLocked(lOChestLock), true);
        }

        // ------------------------------------------------- the inventory model

        private static UWObject fStack(int piId, int piCount)
        {
            return new UWObject((ushort)piId) { HasQuantity = true, Quantity = (ushort)piCount };
        }

        /// <summary>UWInventoryModel: carrying with a count, splitting a stack to the pointer,
        /// consuming one piece, removing, and the modern put-away.</summary>
        private static void fCheckInventoryModel(DataImport pOData)
        {
            Check.Group("Inventory model (CanCarry, TakeFromStack, TryConsumeOne, RemoveItem, DropCursorItemAnywhere)");

            UWCommonObjectProperties lOProperties = pOData.CommonObjectProperties;

            if (lOProperties == null || pOData.InitialPlayer == null)
            {
                Check.Fail("inventory model", "no COMOBJ.DAT or no player");

                return;
            }

            // The template PLAYER.DAT carries no capacity (0x4C is 0 there); a character with 400
            // tenth-stones stands in for the check. The runner reloads the game state afterwards.
            byte[] lyPlain = (byte[])pOData.InitialPlayer.PlainBytes.Clone();

            UWBinary.Write16(lyPlain, 0x4C, 400);
            pOData.ReplaceInitialPlayer(UWPlayerData.FromPlain(lyPlain));

            UWInventoryModel lOModel = new UWInventoryModel(() => pOData, () => null);
            int liStone = UWObjectMechanics.SlingStoneObjectId;
            int liCapacity = pOData.InitialPlayer.MaxWeight - lOModel.GetCarriedTenthStones(lOProperties);
            UWCommonObjectProperties.Entry lOEntry;

            Check.Bool("carry: the template player can carry something", liCapacity > 0, true);
            Check.Bool("carry: the sling stone has a mass", lOProperties.TryGet(liStone, out lOEntry) && lOEntry.MassTenthStones > 0, true);

            int liMass = lOEntry.MassTenthStones;
            int liTooMany = (liCapacity / liMass) + 2;
            UWObject lOHeap = fStack(liStone, liTooMany);

            Check.Bool("carry: a heap heavier than the capacity is refused", lOModel.CanCarry(lOHeap), false);
            Check.Bool("carry: one stone of it is not", lOModel.CanCarry(lOHeap, 1), true);
            Check.Bool("carry: a count above the stack weighs the whole stack",
                lOModel.CanCarry(lOHeap, liTooMany + 5), false);
            Check.Bool("carry: exactly the capacity fits", lOModel.CanCarry(fStack(liStone, liCapacity / liMass)), true);
            Check.Bool("carry: nothing is refused without an item", lOModel.CanCarry(null), true);

            // ItemWeight (INVDATA.C): mass times the raw count, so a count of 0 weighs nothing
            // (C10, decided 2026-10-15); a special link weighs one piece.
            Check.Int("weight: a stack of three stones weighs three", UWInventoryWeight.GetItemTenthStones(fStack(liStone, 3),
                lOProperties), 3 * liMass);
            Check.Int("weight: a stack with a count of 0 weighs nothing", UWInventoryWeight.GetItemTenthStones(fStack(liStone, 0),
                lOProperties), 0);
            Check.Int("weight: a special link weighs one piece", UWInventoryWeight.GetItemTenthStones(
                fStack(liStone, UWStackRules.SpecialPropertyThreshold + 5), lOProperties), liMass);

            // Splitting: part of the stack goes to the pointer, the stack keeps the rest.
            UWObject lOFive = fStack(liStone, 5);
            lOModel.Backpack[0] = lOFive;

            Check.Bool("split: two of five go to the pointer", lOModel.TakeFromStack(lOFive, 2), true);
            Check.Int("split: ... the pointer holds two", lOModel.CursorItem != null ? UWStackRules.Count(lOModel.CursorItem) : -1, 2);
            Check.Int("split: ... three stay", UWStackRules.Count(lOFive), 3);
            Check.Bool("split: ... the stack stays in its place", lOModel.Backpack[0] == lOFive, true);
            lOModel.TakeCursorItem();
            Check.Bool("split: the whole stack does not split", lOModel.TakeFromStack(lOFive, 3), false);
            Check.Bool("split: nor more than it has", lOModel.TakeFromStack(lOFive, 4), false);
            Check.Bool("split: nor a count of zero", lOModel.TakeFromStack(lOFive, 0), false);

            UWObject lOEnchanted = new UWObject(0x20) { Quantity = 719 };
            Check.Bool("split: a special property is one piece and never splits", lOModel.TakeFromStack(lOEnchanted, 1), false);

            // Consuming: a stack loses one, a single piece leaves its place, a special property goes whole.
            Check.Bool("consume: one of three", lOModel.TryConsumeOne(lOFive), true);
            Check.Int("consume: ... two stay", UWStackRules.Count(lOFive), 2);
            lOFive.Quantity = 1;
            Check.Bool("consume: the last piece leaves the backpack", lOModel.TryConsumeOne(lOFive), true);
            Check.Bool("consume: ... slot empty", lOModel.Backpack[0] == null, true);
            lOModel.Backpack[1] = lOEnchanted;
            Check.Bool("consume: enchanted armour goes whole", lOModel.TryConsumeOne(lOEnchanted), true);
            Check.Bool("consume: ... slot empty, the link field untouched", lOModel.Backpack[1] == null && lOEnchanted.Quantity == 719, true);
            Check.Bool("consume: nothing happens to a thing that is not carried", lOModel.TryConsumeOne(new UWObject(1)), false);

            UWObject lODagger = new UWObject(1);
            lOModel.Backpack[2] = lODagger;
            Check.Bool("remove: a backpack item", lOModel.RemoveItem(lODagger), true);
            Check.Bool("remove: ... gone", lOModel.Backpack[2] == null, true);
            Check.Bool("remove: not twice", lOModel.RemoveItem(lODagger), false);

            // The modern put-away: onto a matching stack, else a free place.
            UWModelReset(lOModel);
            lOModel.Backpack[3] = fStack(liStone, 3);
            lOModel.BeginDragFromExternal(fStack(liStone, 2));
            Check.Bool("put away: two stones join the three", lOModel.DropCursorItemAnywhere(), true);
            Check.Int("put away: ... five", UWStackRules.Count(lOModel.Backpack[3]), 5);
            Check.Bool("put away: ... the pointer is empty", lOModel.CursorItem == null, true);

            lOModel.Backpack[3].Quantity = (ushort)UWStackRules.MaxStackCount;
            lOModel.BeginDragFromExternal(fStack(liStone, 2));
            Check.Bool("put away: a full stack of 511 does not take two more", lOModel.DropCursorItemAnywhere(), true);
            Check.Int("put away: ... the stack stays at 511", UWStackRules.Count(lOModel.Backpack[3]), UWStackRules.MaxStackCount);
            Check.Bool("put away: ... the two lie in the first free place", lOModel.Backpack[0] != null && UWStackRules.Count(lOModel.Backpack[0]) == 2, true);

            UWModelReset(lOModel);

            for (int liSlot = 0; liSlot < lOModel.Backpack.Length; liSlot++)
                lOModel.Backpack[liSlot] = new UWObject(1);

            Check.Bool("put away: a full backpack without bags takes nothing", lOModel.TryStoreAnywhere(new UWObject(2)), false);
            Check.Bool("put away: ... and the pointer is empty again", lOModel.CursorItem == null, true);
            Check.Bool("put away: a free place takes the thing", fFreeSlot(lOModel, 4) && lOModel.TryStoreAnywhere(lODagger), true);
            Check.Bool("put away: ... in that place", lOModel.Backpack[4] == lODagger, true);

            fCheckCombining(pOData, lOModel);
            fCheckDropOnEquippedBag(lOModel);
            fCheckNestedBags(pOData, lOModel);
        }

        /// <summary>ItemFitsSlot's loop over the open bags (A43, 2026-10-15): a box inside a full
        /// sack takes nothing more, though the box itself has room - "The box is too full.".</summary>
        private static void fCheckNestedBags(DataImport pOData, UWInventoryModel pOModel)
        {
            int liOuter = -1;
            int liInner = -1;
            int liOuterCapacity = 0;

            for (int liId = 0x80; liId <= 0x8F; liId++)
            {
                if (liId == UWObjectMechanics.RuneBagId || pOData.ObjectClassProperties == null
                    || !pOData.ObjectClassProperties.TryGetContainer(liId, out UWObjectClassProperties.Container lOEntry))
                    continue;

                if (liOuter < 0 && lOEntry.CapacityTenthStones > 0 && lOEntry.ObjectMask == UWObjectClassProperties.ContainerMaskAll)
                {
                    liOuter = liId;
                    liOuterCapacity = lOEntry.CapacityTenthStones;
                }
                else if (liInner < 0 && liOuter >= 0 && lOEntry.ObjectMask == UWObjectClassProperties.ContainerMaskAll)
                {
                    liInner = liId;
                }
            }

            UWCommonObjectProperties lOProperties = pOData.CommonObjectProperties;
            int liStone = UWObjectMechanics.SlingStoneObjectId;
            int liStoneMass = lOProperties != null && lOProperties.TryGet(liStone, out UWCommonObjectProperties.Entry lOStone)
                ? lOStone.MassTenthStones : 0;

            fExpectBool("nested bags: two plain containers and a stone with a mass", liOuter >= 0 && liInner >= 0 && liStoneMass > 0, true);

            if (liOuter < 0 || liInner < 0 || liStoneMass <= 0)
                return;

            UWModelReset(pOModel);

            UWObject lOInner = fBag(liInner);
            int liInnerWeight = UWInventoryWeight.GetItemTenthStones(lOInner, lOProperties);
            int liFill = (liOuterCapacity - liInnerWeight) / liStoneMass;
            UWObject lOOuter = fBag(liOuter);

            lOOuter.Contents.Add(lOInner);
            lOOuter.Contents.Add(fStack(liStone, liFill));

            pOModel.Backpack[0] = lOOuter;
            pOModel.BeginDragFromExternal(fStack(liStone, 1));

            Check.Bool("nested bags: the full sack refuses a stone dropped into the box inside it",
                pOModel.DropCursorItemInContainerAt(lOInner, 0), false);
            Check.Bool("nested bags: ... naming the box", pOModel.LastContainerRejection == UWContainerCapacity.ResultEnum.TooHeavy
                && pOModel.LastContainerRejectionContainer == lOInner, true);
            Check.Bool("nested bags: ... dropped onto the box in the sack's slot too",
                pOModel.DropCursorItemInContainerAt(lOOuter, 0), false);

            lOOuter.Contents.RemoveAt(1);

            Check.Bool("nested bags: with the sack emptied the box takes it", pOModel.DropCursorItemInContainerAt(lOInner, 0), true);

            UWModelReset(pOModel);
        }

        /// <summary>AddToOccupiedSlot (A36, 2026-10-15): a bag on a shoulder or in a hand takes
        /// what is dropped onto it; putting a piece ON (the modern equip) swaps instead.</summary>
        private static void fCheckDropOnEquippedBag(UWInventoryModel pOModel)
        {
            UWModelReset(pOModel);

            UWObject lOSack = fBag(0x80);
            UWObject lOApple = new UWObject(0xB4);

            pOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.RightOffHandSlot] = lOSack;
            pOModel.BeginDragFromExternal(lOApple);

            Check.Bool("bag on a shoulder: the apple goes in",
                pOModel.DropCursorItemInEquip(UWArmorItemMap.BodySlot.RightOffHandSlot), true);
            Check.Bool("bag on a shoulder: ... the sack stays, the pointer is empty, the apple inside",
                pOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.RightOffHandSlot] == lOSack && pOModel.CursorItem == null
                && lOSack.Contents.Contains(lOApple), true);

            UWObject lOTorch = new UWObject((ushort)UWObjectMechanics.TorchObjectId);

            pOModel.BeginDragFromExternal(lOTorch);
            pOModel.DropCursorItemInEquip(UWArmorItemMap.BodySlot.RightOffHandSlot, false);

            Check.Bool("bag on a shoulder: putting a piece on swaps it", pOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.RightOffHandSlot]
                == lOTorch && pOModel.CursorItem == lOSack, true);

            pOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.RightOffHandSlot] = null;
            UWModelReset(pOModel);

            // The open bag's icon when it is the outermost (A44): a full pack refuses at once, even
            // with a matching stack in it; with a place free a stack in a hand takes it too.
            int liStone = UWObjectMechanics.SlingStoneObjectId;
            UWObject lOPackStones = fStack(liStone, 3);

            for (int liSlot = 0; liSlot < pOModel.Backpack.Length; liSlot++)
                pOModel.Backpack[liSlot] = new UWObject(1);

            pOModel.Backpack[0] = lOPackStones;
            pOModel.BeginDragFromExternal(fStack(liStone, 2));

            Check.Bool("bag icon: a full pack refuses, matching stack or not", pOModel.DropCursorItemInFirstFreeBackpackSlot(), false);
            Check.Int("bag icon: ... the stack keeps its three", UWStackRules.Count(lOPackStones), 3);

            pOModel.Backpack[5] = null;
            pOModel.Backpack[0] = null;

            UWObject lOHandStones = fStack(liStone, 4);

            pOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.LeftOffHandSlot] = lOHandStones;

            Check.Bool("bag icon: with a place free the stack in a hand takes them", pOModel.DropCursorItemInFirstFreeBackpackSlot(), true);
            Check.Int("bag icon: ... six in the hand", UWStackRules.Count(lOHandStones), 6);

            pOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.LeftOffHandSlot] = null;
            UWModelReset(pOModel);
        }

        /// <summary>AddToOccupiedSlot's combining (A37, 2026-10-15): a NEW object of quality 40, the
        /// pointer item's place taken by it when that is used up, a stack or a thing holding
        /// something refused.</summary>
        private static void fCheckCombining(DataImport pOData, UWInventoryModel pOModel)
        {
            UWObjectCombining.Rule lOBoth = default;
            UWObjectCombining.Rule lOKeeps = default;
            bool lbBoth = false;
            bool lbKeeps = false;

            foreach (UWObjectCombining.Rule lORule in pOData.ObjectCombining.Rules)
            {
                if (!lbBoth && lORule.FirstIsConsumed && lORule.SecondIsConsumed)
                {
                    lOBoth = lORule;
                    lbBoth = true;
                }

                if (!lbKeeps && lORule.FirstIsConsumed != lORule.SecondIsConsumed)
                {
                    lOKeeps = lORule;
                    lbKeeps = true;
                }
            }

            Check.Bool("combine: CMB.DAT has a rule using up both and one keeping one", lbBoth && lbKeeps, true);

            if (!lbBoth || !lbKeeps)
                return;

            UWModelReset(pOModel);

            UWObject lOFirst = new UWObject((ushort)lOBoth.FirstObjectId) { Quality = 9 };
            pOModel.Backpack[1] = new UWObject((ushort)lOBoth.SecondObjectId) { Quality = 9 };
            pOModel.BeginDragFromExternal(lOFirst);

            Check.Bool("combine: both used up", pOModel.DropCursorItemInBackpack(1), true);
            Check.Bool("combine: ... the result is a new object on the pointer", pOModel.CursorItem != null
                && pOModel.CursorItem != lOFirst && pOModel.CursorItem.ID == lOBoth.ResultObjectId, true);
            Check.Int("combine: ... of CreateObj's quality 40", pOModel.CursorItem != null ? pOModel.CursorItem.Quality : -1, 40);
            Check.Bool("combine: ... and the place is empty", pOModel.Backpack[1] == null, true);

            // The rule keeping one side: the kept one at the pointer, the used-up one in the place,
            // so the result takes that place.
            UWModelReset(pOModel);

            int liKept = lOKeeps.FirstIsConsumed ? lOKeeps.SecondObjectId : lOKeeps.FirstObjectId;
            int liUsed = lOKeeps.FirstIsConsumed ? lOKeeps.FirstObjectId : lOKeeps.SecondObjectId;
            UWObject lOKeptItem = new UWObject((ushort)liKept);

            pOModel.Backpack[2] = new UWObject((ushort)liUsed);
            pOModel.BeginDragFromExternal(lOKeptItem);

            Check.Bool("combine: one kept", pOModel.DropCursorItemInBackpack(2), true);
            Check.Bool("combine: ... the kept one stays at the pointer", pOModel.CursorItem == lOKeptItem, true);
            Check.Bool("combine: ... the result takes the used-up one's place", pOModel.Backpack[2] != null
                && pOModel.Backpack[2].ID == lOKeeps.ResultObjectId, true);

            // A stack of two is refused (ObjsBeCombinable) - the drop swaps instead.
            UWModelReset(pOModel);

            UWObject lOStack = new UWObject((ushort)liUsed) { HasQuantity = true, Quantity = 2 };
            UWObject lOKept = new UWObject((ushort)liKept);

            pOModel.Backpack[2] = lOStack;
            pOModel.BeginDragFromExternal(lOKept);
            pOModel.DropCursorItemInBackpack(2);

            Check.Bool("combine: a stack of two is refused, the two swap", pOModel.Backpack[2] == lOKept
                && pOModel.CursorItem == lOStack && UWStackRules.Count(lOStack) == 2, true);

            UWModelReset(pOModel);
        }

        private static void UWModelReset(UWInventoryModel pOModel)
        {
            for (int liSlot = 0; liSlot < pOModel.Backpack.Length; liSlot++)
                pOModel.Backpack[liSlot] = null;

            pOModel.TakeCursorItem();
        }

        private static bool fFreeSlot(UWInventoryModel pOModel, int piSlot)
        {
            pOModel.Backpack[piSlot] = null;

            return true;
        }

        // ------------------------------------------------- eating

        /// <summary>UWItemUse.TryEat: a stack is not eaten (message 120, read back from the
        /// original 2026-09-21), a single piece is.</summary>
        private static void fCheckEating(DataImport pOData)
        {
            Check.Group("Eating (UWItemUse.TryEat: a stack is refused, a piece is eaten)");

            FakeVitalsHost lOVitalsHost = new FakeVitalsHost();
            UWPlayerVitals lOVitals = fWoundedVitals(lOVitalsHost);
            BookHost lOHost = new BookHost();
            UWObject lOFish = fStack(MeatObjectId, 5);

            Check.Bool("eat: a stack of five is handled", UWItemUse.TryEat(lOFish, pOData, lOVitals, lOHost), true);
            Check.Bool("eat: ... with 'You can only use those individually' (120)", lOHost.GeneralMessages.Contains(120), true);
            Check.Bool("eat: ... nothing consumed", lOHost.Consumed, false);
            Check.Int("eat: ... the stack untouched", UWStackRules.Count(lOFish), 5);

            lOHost = new BookHost();
            int liHungerBefore = lOVitals.Hunger;

            Check.Bool("eat: a single piece of meat is eaten", UWItemUse.TryEat(fStack(MeatObjectId, 1), pOData, lOVitals, lOHost), true);
            Check.Bool("eat: ... and consumed or refused as too full, never silent",
                lOHost.Consumed || lOHost.Messages.Count > 0 || lOHost.GeneralMessages.Count > 0, true);
            Check.Bool("eat: ... the hunger did not drop", lOVitals.Hunger >= liHungerBefore, true);
            Check.Bool("eat: a helmet is not eaten", UWItemUse.TryEat(new UWObject(0x21), pOData, lOVitals, new BookHost()), false);
        }

        // ------------------------------------------------- doors

        /// <summary>
        /// UWDoorRules (cleanup step 2.5, 2026-10-12) against UnderworldExhumed: UseRect and
        /// checkLock for the use, OpenDoor/CloseDoor/ToggleDoor for gronk_door and the a_door
        /// trap (TRAP_DOOR), DoClosingDoors for the night, remove_object for the breaking blow -
        /// and the lock resolution on the real level 1, which has several doors with a lock.
        /// </summary>
        private static void fCheckDoorRules(DataImport pOData)
        {
            Check.Group("Doors (UWDoorRules: use, gronk_door, door trap, night, burst, the lock on level 1)");

            // Using a door (UseRect): the lock first, then the spike - for a creature only.
            Check.Int("use: an open door closes, whatever lock or spike",
                (int)UWDoorRules.Use(false, true, true, false, false), (int)UWDoorRules.UseOutcome.Toggle);
            Check.Int("use: a closed plain door opens",
                (int)UWDoorRules.Use(true, false, false, true, false), (int)UWDoorRules.UseOutcome.Toggle);
            Check.Int("use: a locked door refuses the player",
                (int)UWDoorRules.Use(true, false, true, true, false), (int)UWDoorRules.UseOutcome.Locked);
            Check.Int("use: ... and a creature",
                (int)UWDoorRules.Use(true, false, true, false, true), (int)UWDoorRules.UseOutcome.Locked);
            Check.Int("use: the modern scheme's key in the backpack unlocks and opens",
                (int)UWDoorRules.Use(true, false, true, true, true), (int)UWDoorRules.UseOutcome.UnlockAndOpen);
            Check.Int("use: a spiked door keeps a creature out",
                (int)UWDoorRules.Use(true, true, false, false, false), (int)UWDoorRules.UseOutcome.Spiked);
            Check.Int("use: ... not the player (UseRect: who == ThePlayer || !(owner & 1))",
                (int)UWDoorRules.Use(true, true, false, true, false), (int)UWDoorRules.UseOutcome.Toggle);
            Check.Int("use: a locked AND spiked door says locked first",
                (int)UWDoorRules.Use(true, true, true, false, false), (int)UWDoorRules.UseOutcome.Locked);

            // The three entry points decide on the heading.
            Check.Bool("open: acts on a closed door", UWDoorRules.OpenActs(true), true);
            Check.Bool("open: turns a closing door round", UWDoorRules.OpenActs(true), true);
            Check.Bool("open: leaves an open or opening door alone", UWDoorRules.OpenActs(false), false);
            Check.Bool("close: acts on an open door", UWDoorRules.CloseActs(false), true);
            Check.Bool("close: leaves a closed or closing door alone", UWDoorRules.CloseActs(true), false);
            Check.Bool("toggle: a door at rest always moves", UWDoorRules.ToggleActs(true, false) && UWDoorRules.ToggleActs(false, false), true);
            Check.Bool("toggle: an opening door is turned round (0x1CF counts as open)", UWDoorRules.ToggleActs(false, true), true);
            Check.Bool("toggle: a closing door goes on closing", UWDoorRules.ToggleActs(true, true), false);

            // gronk_door (B10: the talking door closes itself and opens again at once).
            Check.Bool("gronk: open on a door still swinging shut turns it round",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorOpen, true, true), true);
            Check.Bool("gronk: open on a door opening does nothing",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorOpen, false, true), false);
            Check.Bool("gronk: open on a closed door at rest",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorOpen, true, false), true);
            Check.Bool("gronk: close on a closing door does nothing",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorClose, true, true), false);
            Check.Bool("gronk: close on an open door at rest",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorClose, false, false), true);
            Check.Bool("gronk: toggle at rest",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorToggle, false, false), true);
            Check.Bool("gronk: toggle on a closing door goes on closing",
                UWDoorRules.GronkTogglesDoor(UWConversationSession.GronkDoorToggle, true, true), false);
            Check.Bool("gronk: an unknown mode does nothing", UWDoorRules.GronkTogglesDoor(7, true, false), false);

            // The a_door trap: at rest the lock is replaced and the quality decides; in motion the
            // lock stays and only a swing against the wish is turned round.
            UWDoorRules.TrapOutcome lOTrap = UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapOpenAction, true, false);

            Check.Bool("trap: open on a closed door at rest replaces the lock", lOTrap.ReplaceLock, true);
            Check.Bool("trap: ... and opens it", lOTrap.Toggle, true);
            Check.Bool("trap: open on an open door leaves the leaf",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapOpenAction, false, false).Toggle, false);
            Check.Bool("trap: ... but still replaces the lock",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapOpenAction, false, false).ReplaceLock, true);
            Check.Bool("trap: close on an open door closes it",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapCloseAction, false, false).Toggle, true);
            Check.Bool("trap: close on a closed door leaves it",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapCloseAction, true, false).Toggle, false);
            Check.Bool("trap: toggle at rest always moves",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapToggleAction, true, false).Toggle
                && UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapToggleAction, false, false).Toggle, true);
            Check.Bool("trap: quality 0 replaces the lock and leaves the leaf",
                UWDoorRules.DoorTrap(0, true, false).ReplaceLock && !UWDoorRules.DoorTrap(0, true, false).Toggle, true);
            Check.Bool("trap: a moving door keeps its lock",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapCloseAction, true, true).ReplaceLock, false);
            Check.Bool("trap: close on a closing grate does nothing (Drog's grate, level 1 15/33)",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapCloseAction, true, true).Toggle, false);
            Check.Bool("trap: close on an opening door turns it round",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapCloseAction, false, true).Toggle, true);
            Check.Bool("trap: open on a closing door turns it round",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapOpenAction, true, true).Toggle, true);
            Check.Bool("trap: open on an opening door does nothing",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapOpenAction, false, true).Toggle, false);
            Check.Bool("trap: toggle turns a moving door round either way",
                UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapToggleAction, true, true).Toggle
                && UWDoorRules.DoorTrap(UWObjectMechanics.DoorTrapToggleAction, false, true).Toggle, true);

            // The night (DoClosingDoors): open at rest, no tile door bit, roll below the chance.
            Check.Bool("night: an open door at rest is a candidate", UWDoorRules.IsNightCandidate(true, false, false), true);
            Check.Bool("night: a closed door is not", UWDoorRules.IsNightCandidate(false, false, false), false);
            Check.Bool("night: a moving door is not (an animation object then)", UWDoorRules.IsNightCandidate(true, true, false), false);
            Check.Bool("night: the tile's door bit spares it", UWDoorRules.IsNightCandidate(true, false, true), false);
            Check.Bool("night: roll 2 of chance 3 closes", UWDoorRules.NightCloses(true, false, false, 2, 3), true);
            Check.Bool("night: roll 3 does not", UWDoorRules.NightCloses(true, false, false, 3, 3), false);
            Check.Bool("night: the raised portcullis is a door object of the open range (B11)",
                UWObjectIds.IsDoor(UWObjectIds.OpenPortcullis) && UWObjectIds.IsOpenDoor(UWObjectIds.OpenPortcullis), true);

            // The breaking blow (remove_object) and the spike OpenDoor takes off.
            Check.Bool("burst: a closed door springs open", UWDoorRules.BurstOpens(true), true);
            Check.Bool("burst: an open door only loses its lock", UWDoorRules.BurstOpens(false), false);

            UWObject lOSpiked = new UWObject((ushort)UWObjectIds.FirstClosedDoor) { Owner = UWObjectMechanics.FreshDoorSpikeOwner };

            UWDoorRules.ClearSpike(lOSpiked);
            Check.Bool("spike: OpenDoor takes the bit off", UWObjectMechanics.IsDoorSpiked(lOSpiked), false);
            Check.Int("spike: ... the strength bits above it stay (owner & 0xFFFE)", lOSpiked.Owner, UWObjectMechanics.FreshDoorSpikeOwner - 1);

            // The lock resolution, synthetic and on the real level 1.
            UWLockState lOSynthetic = fMakeDoorLock(5, 2, true, true, out UWObject lOSyntheticDoor);

            Check.Bool("lock: a synthetic door's lock resolves locked",
                UWDoorRules.ResolveLock(lOSyntheticDoor, new List<UWObject>()).IsLocked && lOSynthetic.LockId == 5, true);
            Check.Bool("lock: a door without a chain has none",
                UWDoorRules.ResolveLock(new UWObject((ushort)UWObjectIds.FirstClosedDoor), new List<UWObject>()).HasLock, false);

            UWLevel lOLevel = pOData != null && pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;
            List<UWObject> lODoors = UWDoorRules.FindDoorsWithLock(lOLevel);

            Check.Bool("lock: level 1 has doors with a lock", lODoors.Count > 0, true);

            bool lbAllResolve = lODoors.Count > 0;
            int liLocked = 0;

            foreach (UWObject lODoor in lODoors)
            {
                UWLockState lOState = UWDoorRules.ResolveLock(lODoor, lOLevel.Masterlist);
                UWObject lOLock = lODoor.Contents[0];

                lbAllResolve &= lOState.IsResolved && lOState.HasLock && lOState.LockObject == lOLock
                    && lOState.IsLocked == UWObjectMechanics.IsLockLocked(lOLock)
                    && lOState.LockId == UWObjectMechanics.GetLockId(lOLock)
                    && lOState.LockDifficulty == lOLock.ZPos;

                if (lOState.IsLocked)
                    liLocked++;
            }

            Check.Bool("lock: every one of them resolves to its first chain object (" + lODoors.Count + " doors, "
                + liLocked + " locked)", lbAllResolve, true);
        }
    }
}
