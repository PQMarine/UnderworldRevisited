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
    /// move cannot change a number. The inventory model (InventoryModelChecks) and the doors
    /// (DoorRuleChecks) have files of their own since cleanup 9.2.
    /// </summary>
    public static partial class SelfChecks
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
            Check.Bool("fishing: water of height 0 is refused from above (unsigned 0 - 1)", UWFishingRules.WaterLowEnough(1, 0), false);
            Check.Bool("fishing: ... and swimming in it", UWFishingRules.WaterLowEnough(0, 0), false);
            Check.Bool("fishing: swimming in water of height 1 works", UWFishingRules.WaterLowEnough(1, 1), true);
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
    }
}
