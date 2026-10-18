using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The trap rules (UWTrapRules) against the original's UseTrigger, UseTrap and the hack traps
    /// (UnderworldExhumed TRIGGER.C, WORLDEV.C), on the shipped levels where a trap of the kind
    /// exists and on records made here where none does - the fixes of the reference audit of
    /// 2026-10-12. A recording host stands in for the game. The levels are changed; the runner
    /// reloads the world afterwards.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckTraps(DataImport pOData)
        {
            Check.Group("Traps (UWTrapRules against TRIGGER.C and WORLDEV.C)");

            if (pOData.Levels == null || pOData.Levels.Count < 9)
            {
                Check.Fail("traps", "the nine levels are not loaded");

                return;
            }

            fCheckPitTrap(pOData);
            fCheckWardRune(pOData);
            fCheckTriggerSetters(pOData);
            fCheckTileHeightCarries(pOData);
            fCheckPoisonTrap(pOData);
            fCheckSpellTrapLine(pOData);
            fCheckSpellTrapCaster(pOData);

            fCheckInventoryTrap(pOData);
            fCheckConversationTrap(pOData);
            fCheckTerrainBounds(pOData);
            fCheckTrapInDoorRemoved(pOData);
            fCheckChainGoesOn(pOData);
            fCheckEightPositionSwitch(pOData);
            fCheckTextTrapStrings(pOData);
            fCheckSwitchPassesOn(pOData);
            fCheckQuakeAndBook(pOData);

            Check.Group("Traps on the world (cleanup step 3.6)");

            fCheckQuakeLength(pOData);
            fCheckDoorTrapOnWorld(pOData);
            fCheckCrowdingOnWorld(pOData);
            fCheckSilentFiring(pOData);

            Check.Group("Traps (UWTrapRules against TRIGGER.C and WORLDEV.C), continued");

            // A42: RemoveTrap names the trap by get_name - a poisoning damage trap too.
            Check.Str("disarm: a poisoning damage trap is a damage trap",
                UWTrapDisarming.GetTrapName(new UWObject((ushort)UWObjectMechanics.DamageTrapId) { Owner = 1 }, pOData),
                UWText.ObjectName(pOData, UWObjectMechanics.DamageTrapId));

            // A29: rand() * 0x3F / 0x8000 below the quality creates nothing.
            fExpectBool("create roll: quality 0 always creates",
                !UWTrapRules.CreateObjectRollFails(0, 0) && !UWTrapRules.CreateObjectRollFails(0x7FFF, 0), true);
            fExpectBool("create roll: quality 62 creates only on the top draw",
                !UWTrapRules.CreateObjectRollFails(0x7FFF, 62) && UWTrapRules.CreateObjectRollFails(0x7FFF - 0x210, 62), true);
            fExpectBool("create roll: quality 63 never creates", UWTrapRules.CreateObjectRollFails(0x7FFF, 63), true);

            // A20: UseRect clicks for minor class 3 alone.
            fExpectBool("switch click: the buttons 0x170 and 0x17F click, lever, door, texture object and bridge do not",
                UWTrapRules.ClicksWhenUsed(0x170) && UWTrapRules.ClicksWhenUsed(0x17F) && !UWTrapRules.ClicksWhenUsed(0x161)
                && !UWTrapRules.ClicksWhenUsed(0x140) && !UWTrapRules.ClicksWhenUsed(0x16E) && !UWTrapRules.ClicksWhenUsed(0x164),
                true);
        }

        /// <summary>A30: FindObj with how 4 - paperdoll, backpack, then contents; never the cursor;
        /// only the first find is weighed against z. A bag in the pack holds a single crystal
        /// splinter and then a stack of five; the trap asks for two.</summary>
        private static void fCheckInventoryTrap(DataImport pOData)
        {
            const int Splinter = 0x111;

            List<UWObject> lOBagList = new List<UWObject>
            {
                null,
                new UWObject((ushort)UWObjectIds.FirstContainer) { HasQuantity = false, Quantity = 2 },
                new UWObject((ushort)Splinter) { HasQuantity = true, Quantity = 1, Link = 3 },
                new UWObject((ushort)Splinter) { HasQuantity = true, Quantity = 5 }
            };

            UWObject lOBag = lOBagList[1];

            lOBag.EnsureContentsLoaded(lOBagList);

            UWInventoryModel lOModel = new UWInventoryModel(() => pOData, () => null);

            lOModel.Backpack[0] = lOBag;

            fExpectBool("inventory trap: the first splinter in the bag is found",
                UWTrapRules.FindCarriedLikeFindObj(lOModel, Splinter, null) == lOBagList[2], true);

            // The trap (quality 8, owner 17: 0x111) with z 2, linked to a damage trap of 5.
            List<UWObject> lOMaster = pOData.Levels[6].Masterlist;
            UWObject lODamage = new UWObject((ushort)UWObjectMechanics.DamageTrapId) { Quality = 5 };

            lOMaster.Add(lODamage);

            UWObject lOTrap = new UWObject((ushort)UWObjectMechanics.InventoryTrapId)
            {
                Quality = 8, Owner = 17, ZPos = 2, Quantity = (ushort)(lOMaster.Count - 1)
            };

            lOMaster.Add(lOTrap);

            UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.MoveTriggerId)
            {
                Quantity = (ushort)(lOMaster.Count - 1), Flags = 6, Quality = 21, Owner = 41
            };

            FakeTrapHost lOHost = new FakeTrapHost(pOData, 6) { Inventory = lOModel };

            UWTrapRules.FireTraps(lOTrigger, null, lOHost);
            fExpectInt("inventory trap: one splinter first, two asked - the chain ends", lOHost.PlayerDamage, 0);

            lOTrap.ZPos = 1;
            UWTrapRules.FireTraps(lOTrigger, null, lOHost);
            fExpectInt("inventory trap: one asked - the chain goes on", lOHost.PlayerDamage, 5);

            lOModel.BeginDragFromBackpack(0);
            lOHost.PlayerDamage = 0;
            UWTrapRules.FireTraps(lOTrigger, null, lOHost);
            fExpectInt("inventory trap: the bag on the cursor is not searched", lOHost.PlayerDamage, 0);

            lOMaster.RemoveRange(lOMaster.Count - 2, 2);
        }

        /// <summary>A34: do_trap_hack 0x32 - gronk_whoami(0xD8) over the active mobiles finds the
        /// troll guard of level 7 in slot 251; a waiting one (goal 7) is sent home (goal 1) and
        /// talked to.</summary>
        private static void fCheckConversationTrap(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[6];
            UWNpc lOTroll = UWTrapRules.FindActiveByWhoAmI(lOLevel, UWTrapRules.ConversationTrapWhoAmI);

            fExpectInt("conversation trap: the first active critter with conversation 216 is slot 251",
                lOTroll != null ? lOLevel.Masterlist.IndexOf(lOTroll) : -1, 251);

            if (lOTroll == null)
                return;

            UWCritterRecord lORecord = new UWCritterRecord(lOTroll);

            fExpectInt("conversation trap: the troll waits (goal 7)", lORecord.Goal, 7);

            FakeTrapHost lOHost = new FakeTrapHost(pOData, 6);
            UWObject lOTrap = new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 0x32 };

            UWTrapRules.SetOffDisarmableTrap(lOTrap, null, null, 24, 57, lOHost);
            fExpectBool("conversation trap: the troll is talked to", lOHost.TalkedTo == lOTroll, true);
            fExpectInt("conversation trap: ... and goes home (goal 1)", lORecord.Goal, 1);
        }

        /// <summary>A40, A57: change_terrain applies a height up to 0xD and a floor below 0xB - the
        /// bullfrog field of level 4 stops at 13; a change terrain trap with height 14 leaves the
        /// height, its floor 10 is written.</summary>
        private static void fCheckTerrainBounds(DataImport pOData)
        {
            UWLevel lOFrogLevel = pOData.Levels[UWObjectMechanics.BullfrogLevelIndex];
            int[] liSaved =
            {
                UWGameVariables.Get(UWObjectMechanics.BullfrogVarX), UWGameVariables.Get(UWObjectMechanics.BullfrogVarY),
                UWGameVariables.Get(UWObjectMechanics.BullfrogVarRetries)
            };

            int liCentre = UWObjectMechanics.BullfrogGridOrigin + 3;
            UWTile lOCentre = lOFrogLevel.GetTile(liCentre, liCentre);
            UWTile lOBeside = lOFrogLevel.GetTile(liCentre - 1, liCentre);

            UWGameVariables.Set(UWObjectMechanics.BullfrogVarX, 3);
            UWGameVariables.Set(UWObjectMechanics.BullfrogVarY, 3);
            UWGameVariables.Set(UWObjectMechanics.BullfrogVarRetries, 10);
            lOCentre.FloorHeight = 13 << 4;
            lOBeside.FloorHeight = 12 << 4;

            UWTrapRules.FireBullfrog(0, new FakeTrapHost(pOData, UWObjectMechanics.BullfrogLevelIndex));

            fExpectInt("bullfrog: a tile at 13 does not rise", lOCentre.FloorHeight >> 4, 13);
            fExpectInt("bullfrog: its neighbour at 12 rises to 13", lOBeside.FloorHeight >> 4, 13);

            UWGameVariables.Set(UWObjectMechanics.BullfrogVarX, liSaved[0]);
            UWGameVariables.Set(UWObjectMechanics.BullfrogVarY, liSaved[1]);
            UWGameVariables.Set(UWObjectMechanics.BullfrogVarRetries, liSaved[2]);

            UWLevel lOLevel = pOData.Levels[0];

            if (!fFindWorldEditTiles(new UWTileQueries(lOLevel, pOData), out UWTilePos lOOpen, out UWTilePos _, out UWTilePos _))
                return;

            UWTile lOTile = lOLevel.GetTile(lOOpen.X, lOOpen.Y);
            int liHeightBefore = lOTile.FloorHeight >> 4;
            List<UWObject> lOMaster = lOLevel.Masterlist;

            // Type open (quality bit 0, heading 0), floor 10 (quality 21 >> 1), height 14, wall unchanged.
            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.ChangeTerrainTrapId) { Quality = 21, Owner = 63, ZPos = 14 << 3 });

            UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.MoveTriggerId)
            {
                Quantity = (ushort)(lOMaster.Count - 1), Flags = 6, Quality = (ushort)lOOpen.X, Owner = (ushort)lOOpen.Y
            };

            UWTrapRules.FireTraps(lOTrigger, null, new FakeTrapHost(pOData, 0));
            lOMaster.RemoveAt(lOMaster.Count - 1);

            fExpectInt("change terrain: height 14 is not applied", lOTile.FloorHeight >> 4, liHeightBefore);
            fExpectInt("change terrain: floor 10 is", (int)((lOTile.RawTileData >> 10) & 0xF), 10);
        }

        /// <summary>A50: delete_trap finds the trap with Obj_Find, deep - the one-shot trigger of
        /// level 7 at 4/38 aims at door trap 672 inside door 735 at 7/35; after it ran, trap and
        /// lock template are out of the door's chain and their slots free.</summary>
        private static void fCheckTrapInDoorRemoved(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[6];
            List<UWObject> lOMaster = lOLevel.Masterlist;
            UWObject lOTrap = lOMaster.Count > 735 ? lOMaster[672] : null;
            UWObject lODoor = lOTrap != null ? lOMaster[735] : null;
            UWObject lOTrigger = lOLevel.GetTile(4, 38)?.ObjectsInTile?.Find(pOAt => pOAt.Quantity == 672
                && pOAt.ID >= UWObjectIds.FirstTrigger && pOAt.ID <= UWObjectIds.LastTrigger);

            if (lOTrap == null || lOTrap.ID != UWObjectMechanics.DoorTrapId || lOTrigger == null)
            {
                Check.Fail("trap in a door", "level 7: door trap 672 or its trigger at 4/38 not found");

                return;
            }

            lODoor.EnsureContentsLoaded(lOMaster);

            int liLock = lOTrap.Quantity;

            fExpectBool("trap in a door: door 735 holds trap 672", lODoor.Contents.Contains(lOTrap), true);

            UWTrapRules.RemoveOneShotTrap(lOTrigger, lOTrap, lOLevel);

            fExpectBool("trap in a door: the trap is out of the door's chain", lODoor.Contents.Contains(lOTrap), false);
            fExpectBool("trap in a door: trap and lock template are on the free list",
                lOLevel.IsStaticSlotFree(672) && (liLock == 0 || lOLevel.IsStaticSlotFree(liLock)), true);
        }

        /// <summary>A54: UseTrap follows the link whatever the kind did - a combination trap (no case
        /// in UW1) and a failing teleport (level 99) pass on to a damage trap; a tell trap ends the
        /// chain.</summary>
        private static void fCheckChainGoesOn(DataImport pOData)
        {
            List<UWObject> lOMaster = pOData.Levels[0].Masterlist;
            int liBase = lOMaster.Count;

            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.DamageTrapId) { Quality = 3 });
            lOMaster.Add(new UWObject(0x18F) { Quantity = (ushort)liBase });
            lOMaster.Add(new UWObject((ushort)UWTrapRules.TeleportTrapId) { ZPos = 99, Quantity = (ushort)liBase });
            lOMaster.Add(new UWObject((ushort)UWTrapRules.TellTrapId) { Quality = 0x3F, Quantity = (ushort)liBase });

            int[] liDamage = new int[3];

            for (int liAt = 0; liAt < 3; liAt++)
            {
                FakeTrapHost lOHost = new FakeTrapHost(pOData, 0);
                UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.MoveTriggerId)
                {
                    Quantity = (ushort)(liBase + 1 + liAt), Flags = 6, Quality = 1, Owner = 1
                };

                UWTrapRules.FireTraps(lOTrigger, null, lOHost);
                liDamage[liAt] = lOHost.PlayerDamage;
            }

            lOMaster.RemoveRange(liBase, 4);

            fExpectInt("chain: a combination trap passes on", liDamage[0], 3);
            fExpectInt("chain: a teleport that fails passes on", liDamage[1], 3);
            fExpectInt("chain: a tell trap ends it", liDamage[2], 0);
        }

        /// <summary>A55: eight_pos_switch - height = trap z + lever flags * 8; quality 3 sets the
        /// target tile to height / 8 below 0x68 (z 16, position 2: 4, the user's 64 world units),
        /// quality 4 the z of the trap's linked object.</summary>
        private static void fCheckEightPositionSwitch(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[0];

            if (!fFindWorldEditTiles(new UWTileQueries(lOLevel, pOData), out UWTilePos lOOpen, out UWTilePos _, out UWTilePos _))
                return;

            List<UWObject> lOMaster = lOLevel.Masterlist;
            int liBase = lOMaster.Count;
            UWObject lOMoved = new UWObject(0x80);
            UWObject lOLever = new UWObject(0x161) { Flags = 2 };

            // The moved object borrows slot 300 - the link has nine bits.
            UWObject lOHeld = lOMaster[300];

            lOMaster[300] = lOMoved;
            lOMaster.Add(null);
            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 3, ZPos = 16 });
            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 4, ZPos = 16, Quantity = 300 });
            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 3, ZPos = 0x60 });

            UWTile lOTile = lOLevel.GetTile(lOOpen.X, lOOpen.Y);
            int[] liHeights = new int[3];

            for (int liAt = 0; liAt < 3; liAt++)
            {
                UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.UseTriggerId)
                {
                    Quantity = (ushort)(liBase + 1 + liAt), Flags = 6, Quality = (ushort)lOOpen.X, Owner = (ushort)lOOpen.Y
                };

                lOTile.FloorHeight = 1 << 4;
                UWTrapRules.FireTraps(lOTrigger, lOLever, new FakeTrapHost(pOData, 0));
                liHeights[liAt] = lOTile.FloorHeight >> 4;
            }

            lOMaster.RemoveRange(liBase, 4);
            lOMaster[300] = lOHeld;

            fExpectInt("eight-position switch: z 16, position 2 -> height 4", liHeights[0], 4);
            fExpectInt("eight-position switch: quality 4 leaves the tile", liHeights[1], 1);
            fExpectInt("eight-position switch: ... and sets the linked object's z to 32", lOMoved.ZPos, 32);
            fExpectInt("eight-position switch: 0x60 + 16 is not below 0x68 - the tile stays", liHeights[2], 1);
        }

        /// <summary>A56: TRAP_TEXT_STRING reads string quality &lt;&lt; 5 | owner of block 9. On every
        /// text trap of the nine levels that is the 64 * level + owner the port used before, and
        /// level 1's two give 2 (the orb at 58/13) and 4 (the double door at 31/1).</summary>
        private static void fCheckTextTrapStrings(DataImport pOData)
        {
            int liTraps = 0;
            int liDiffering = 0;

            for (int liLevel = 0; liLevel < 9; liLevel++)
            {
                UWLevel lOLevel = pOData.Levels[liLevel];

                for (int liAt = 0; liAt < lOLevel.Masterlist.Count; liAt++)
                {
                    UWObject lOObject = lOLevel.Masterlist[liAt];

                    // Slot 469 of five levels is a leftover record on the free list.
                    if (lOObject == null || lOObject.ID != UWObjectMechanics.TextStringTrapId || lOLevel.IsStaticSlotFree(liAt))
                        continue;

                    liTraps++;

                    if (UWObjectMechanics.GetTextTrapStringIndex(lOObject) != (64 * liLevel) + lOObject.Owner + 1)
                        liDiffering++;
                }
            }

            fExpectBool("text trap: the levels hold text traps", liTraps > 0, true);
            fExpectInt("text trap: quality << 5 | owner is 64 * level + owner on every one", liDiffering, 0);
        }

        /// <summary>A58: a button that is on hands the use to the next trigger of its chain
        /// (UseTrigger). A button with two use triggers: turned on it fires the second, turned off
        /// the first. On the shipped levels no button's trigger has a successor.</summary>
        private static void fCheckSwitchPassesOn(DataImport pOData)
        {
            int liWithSuccessor = 0;

            for (int liLevel = 0; liLevel < 9; liLevel++)
            {
                UWLevel lOLevel = pOData.Levels[liLevel];

                foreach (UWTile lOTile in lOLevel.TileData)
                {
                    foreach (UWObject lOObject in lOTile?.ObjectsInTile ?? new List<UWObject>())
                    {
                        UWObject lOLinked = lOObject != null && UWTrapRules.ClicksWhenUsed(lOObject.ID)
                            ? UWObjectMechanics.GetLinkedObject(lOObject, lOLevel.Masterlist) : null;

                        if (lOLinked != null && lOLinked.ID == UWObjectMechanics.UseTriggerId && lOLinked.Link != 0)
                            liWithSuccessor++;
                    }
                }
            }

            fExpectInt("switch: no shipped button's use trigger has a successor", liWithSuccessor, 0);

            List<UWObject> lOMaster = pOData.Levels[0].Masterlist;
            int liBase = lOMaster.Count;

            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.DamageTrapId) { Quality = 3 });
            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.DamageTrapId) { Quality = 7 });
            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.UseTriggerId)
            {
                Quantity = (ushort)liBase, Link = (ushort)(liBase + 3), Flags = 6, Quality = 1, Owner = 1
            });
            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.UseTriggerId)
            {
                Quantity = (ushort)(liBase + 1), Flags = 6, Quality = 1, Owner = 1
            });

            FakeTrapHost lOOff = new FakeTrapHost(pOData, 0);
            FakeTrapHost lOOn = new FakeTrapHost(pOData, 0);

            UWTrapRules.TryFireSwitch(new UWObject(0x170) { Quantity = (ushort)(liBase + 2) }, lOOff);
            UWTrapRules.TryFireSwitch(new UWObject(0x178) { Quantity = (ushort)(liBase + 2) }, lOOn);

            lOMaster.RemoveRange(liBase, 4);

            fExpectInt("switch: pressed from off it is on - the second trigger fires", lOOff.PlayerDamage, 7);
            fExpectInt("switch: pressed from on it is off - the first trigger fires", lOOn.PlayerDamage, 3);
        }

        /// <summary>The quake's shake is always 0x1E, whatever the owner field says (a named variant
        /// in UWTrapRules since cleanup step 3.6 - Tremor passes its own length); the bounce takes
        /// the owner.</summary>
        private static void fCheckQuakeLength(DataImport pOData)
        {
            UWObject lOQuake = new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 0x3E, Owner = 5 };
            FakeTrapHost lOHost = new FakeTrapHost(pOData, 0);

            UWTrapRules.SetOffDisarmableTrap(lOQuake, null, null, 0, 0, lOHost);

            fExpectBool("quake: the shake is 0x1E whatever the owner (5)", lOHost.Fake.Shown.Log.Contains("shake 30"), true);
            fExpectInt("quake: the player is thrown up", lOHost.Bounces, 1);
        }

        /// <summary>
        /// The a_door trap as a rule over the world's doors (UWDoorRules.DoorTrap; on the trap host
        /// until cleanup step 3.6, 2026-10-17): at rest the lock is replaced, then the leaf acts by
        /// the quality; a moving door keeps its lock and turns only against the trap's wish; the one
        /// who set the chain off opens it.
        /// </summary>
        private static void fCheckDoorTrapOnWorld(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[0];
            List<UWObject> lOMaster = lOLevel.Masterlist;
            int liBase = lOMaster.Count;
            UWTile lOTile = lOLevel.GetTile(1, 1);
            UWObject lODoorData = new UWObject(0x140) { TileX = 1, TileY = 1 };

            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.DoorTrapId) { Quantity = (ushort)(liBase + 1) });
            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.LockObjectId));
            lOTile.ObjectsInTile.Add(lODoorData);

            try
            {
                UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.UseTriggerId)
                {
                    Quantity = (ushort)liBase, Flags = 6, Quality = 1, Owner = 1
                };

                // Close on an open door at rest: the lock is replaced, then it closes.
                lOMaster[liBase].Quality = (ushort)UWObjectMechanics.DoorTrapCloseAction;

                FakeTrapHost lOHost = new FakeTrapHost(pOData, 0);
                FakeDoor lODoor = new FakeDoor(lOHost.Fake.Log, lODoorData, false);

                lOHost.Fake.DoorList.Add(lODoor);
                UWTrapRules.FireTraps(lOTrigger, null, lOHost);

                fExpectInt("door trap: at rest the lock is replaced, then the door closes",
                    fIndexOfSequence(lOHost.Fake.Log, "door 1/1 lock replaced", "door 1/1 closes"), 0);

                // Open, set off by a creature: it opens the door.
                lOMaster[liBase].Quality = (ushort)UWObjectMechanics.DoorTrapOpenAction;
                lOHost = new FakeTrapHost(pOData, 0) { HasTriggeringCreature = true };
                lOHost.Fake.DoorList.Add(new FakeDoor(lOHost.Fake.Log, lODoorData, true));
                UWTrapRules.FireTraps(lOTrigger, null, lOHost);

                fExpectBool("door trap: a creature's chain opens the door as the creature",
                    lOHost.Fake.Log.Contains("door 1/1 opens") && lOHost.Fake.Log.Contains("door 1/1 moved by Creature"), true);

                // Close on a door swinging shut: the lock stays, nothing moves.
                lOMaster[liBase].Quality = (ushort)UWObjectMechanics.DoorTrapCloseAction;
                lOHost = new FakeTrapHost(pOData, 0);
                lOHost.Fake.DoorList.Add(new FakeDoor(lOHost.Fake.Log, lODoorData, true, true));
                UWTrapRules.FireTraps(lOTrigger, null, lOHost);

                fExpectBool("door trap: a closing door keeps its lock and goes on closing",
                    lOHost.Fake.Log.Exists(psLine => psLine.StartsWith("door 1/1")), false);

                // Open on a door swinging shut: it is turned round, by the player.
                lOMaster[liBase].Quality = (ushort)UWObjectMechanics.DoorTrapOpenAction;
                lOHost = new FakeTrapHost(pOData, 0);
                lOHost.Fake.DoorList.Add(new FakeDoor(lOHost.Fake.Log, lODoorData, true, true));
                UWTrapRules.FireTraps(lOTrigger, null, lOHost);

                fExpectBool("door trap: a closing door is turned round against the trap's wish, no new lock",
                    lOHost.Fake.Log.Contains("door 1/1 moved by Player") && !lOHost.Fake.Log.Contains("door 1/1 lock replaced"),
                    true);
            }
            finally
            {
                lOTile.ObjectsInTile.Remove(lODoorData);
                lOMaster.RemoveRange(liBase, 2);
            }
        }

        /// <summary>A creation trap whose template is a creature creates nothing while one made
        /// during play stands within four back and five forward (the world's creature search by
        /// the body's tile).</summary>
        private static void fCheckCrowdingOnWorld(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[0];
            List<UWObject> lOMaster = lOLevel.Masterlist;
            int liBase = lOMaster.Count;
            UWNpc lOTemplate = new UWNpc((ushort)(UWObjectClassProperties.CritterFirstId + 1)) { TileX = 20, TileY = 20 };

            lOMaster.Add(new UWObject((ushort)UWObjectMechanics.CreateObjectTrapId) { Quantity = (ushort)(liBase + 1) });
            lOMaster.Add(lOTemplate);

            try
            {
                FakeTrapHost lOHost = new FakeTrapHost(pOData, 0) { AlwaysFireCreateObjectTraps = true };

                lOHost.Fake.Creatures.Add(fFakeCreature(1, 25, 25, 25, 25, pbSpawned: true));
                UWTrapRules.FireTrapInTile(lOMaster[liBase], 21, 21, lOHost);

                fExpectInt("crowding: a spawned creature five tiles on keeps the trap from creating", lOHost.Spawned.Count, 0);

                lOHost = new FakeTrapHost(pOData, 0) { AlwaysFireCreateObjectTraps = true };
                lOHost.Fake.Creatures.Add(fFakeCreature(1, 26, 26, 26, 26, pbSpawned: true));
                UWTrapRules.FireTrapInTile(lOMaster[liBase], 21, 21, lOHost);

                fExpectInt("crowding: six tiles on it creates, at the template's spot",
                    lOHost.Fake.Log.FindAll(psLine => psLine.StartsWith("spawn")).Count, 1);
            }
            finally
            {
                lOMaster.RemoveRange(liBase, 2);
            }
        }

        /// <summary>A firing without a message channel (the respawn) is the silent scope: a text
        /// trap says nothing, the world still plays.</summary>
        private static void fCheckSilentFiring(DataImport pOData)
        {
            FakeTrapHost lOHost = new FakeTrapHost(pOData, 0);

            fExpectBool("silent firing: a host's world shows messages", lOHost.World.Presentation.ShowsMessages, true);

            IUWWorld lOSilent = UWScopedWorld.With(lOHost.World, UWSilentPresentation.Of(lOHost.World.Presentation));

            fExpectBool("silent firing: the scoped world shows none", lOSilent.Presentation.ShowsMessages, false);
            fExpectBool("silent firing: ... and is the same level", ReferenceEquals(lOSilent.Level, lOHost.World.Level), true);
            fExpectBool("silent firing: scoping it again keeps one layer",
                UWScopedWorld.With(lOSilent, lOSilent.Presentation) == lOSilent, true);
        }

        /// <summary>A59: the quake hack acts only when the player set it off; the exploding book
        /// hack finds the book on the player's tile, inside a bag lying there.</summary>
        private static void fCheckQuakeAndBook(DataImport pOData)
        {
            UWObject lOQuake = new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 0x3E };
            FakeTrapHost lOByCreature = new FakeTrapHost(pOData, 0) { HasTriggeringCreature = true };
            FakeTrapHost lOByPlayer = new FakeTrapHost(pOData, 0);

            UWTrapRules.SetOffDisarmableTrap(lOQuake, null, null, 0, 0, lOByCreature);
            UWTrapRules.SetOffDisarmableTrap(lOQuake, null, null, 0, 0, lOByPlayer);

            fExpectInt("quake: a creature shakes nothing", lOByCreature.Shakes + lOByCreature.Bounces, 0);
            fExpectInt("quake: the player gets shake and bounce", lOByPlayer.Shakes + lOByPlayer.Bounces, 2);

            UWLevel lOLevel = pOData.Levels[0];

            if (!fFindWorldEditTiles(new UWTileQueries(lOLevel, pOData), out UWTilePos lOOpen, out UWTilePos _, out UWTilePos _))
                return;

            List<UWObject> lOBagList = new List<UWObject>
            {
                null,
                new UWObject((ushort)UWObjectIds.FirstContainer) { HasQuantity = false, Quantity = 2 },
                new UWObject((ushort)UWExplodingBook.BookObjectId)
            };

            UWObject lOBag = lOBagList[1];

            lOBag.EnsureContentsLoaded(lOBagList);

            UWTile lOTile = lOLevel.GetTile(lOOpen.X, lOOpen.Y);

            lOTile.ObjectsInTile.Add(lOBag);

            FakeTrapHost lOHost = new FakeTrapHost(pOData, 0) { PlayerTile = lOOpen };
            bool lbQuest = UWQuestFlags.Get(UWExplodingBook.QuestFlag) != 0;

            UWTrapRules.SetOffDisarmableTrap(new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 0x29 }, null, null,
                0, 0, lOHost);

            lOTile.ObjectsInTile.Remove(lOBag);
            UWQuestFlags.Set(UWExplodingBook.QuestFlag, lbQuest ? 1 : 0);

            fExpectInt("exploding book: the book in a bag on the player's tile explodes", lOHost.Curses, 1);
            fExpectInt("exploding book: ... and is gone from the bag", lOBag.Contents.Count, 0);
        }

        /// <summary>A53: do_spell with the trap as the caster - classes 0-3, 7, 9, 10 and 11 do
        /// nothing, a heal goes to the player who set it off, the rest is cast.</summary>
        private static void fCheckSpellTrapCaster(DataImport pOData)
        {
            int[] liCasts = new int[15];
            List<UWObject> lOMaster = pOData.Levels[0].Masterlist;

            for (int liClass = 0; liClass < liCasts.Length; liClass++)
            {
                FakeTrapHost lOHost = new FakeTrapHost(pOData, 0);
                UWObject lOTrap = new UWObject((ushort)UWObjectMechanics.SpellTrapId) { Quality = (ushort)liClass, Owner = 1 };

                lOMaster.Add(lOTrap);

                // A repeatable trigger (flags bit 1) - nothing is cleaned up.
                UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.MoveTriggerId)
                {
                    Quantity = (ushort)(lOMaster.Count - 1), Flags = 6, Quality = 1, Owner = 1
                };

                UWTrapRules.FireTraps(lOTrigger, null, lOHost);
                lOMaster.RemoveAt(lOMaster.Count - 1);
                liCasts[liClass] = lOHost.SpellsCast.Count;
            }

            fExpectBool("spell trap caster: classes 0-3, 7, 9, 10, 11 do nothing, the rest is cast",
                string.Join(",", liCasts) == "0,0,0,0,1,1,1,0,1,0,0,0,1,1,1", true);
            fExpectBool("spell trap caster: a creature that sets off a heal is not healed by the player's spell",
                UWTrapRules.SpellTrapCastsFor(4, true, false), false);
        }

        /// <summary>A11: the spell trap of level 5 (slot 562 at 10/45, fireball 5/3) fires flat along
        /// its heading from its height. Its trigger 561 at 14/45 names 10/45; the corridor 10..13/45
        /// is open, and the ball at z 51 flies within the body of a player standing on the floor of
        /// 14/45 (z 32, height 23) - so it still hits him there when he stands in line.</summary>
        private static void fCheckSpellTrapLine(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[4];
            UWObject lOTrap = lOLevel.Masterlist.Count > 562 ? lOLevel.Masterlist[562] : null;
            UWObject lOTrigger = lOTrap != null ? lOLevel.Masterlist[561] : null;

            if (lOTrap == null || lOTrap.ID != UWObjectMechanics.SpellTrapId || lOTrigger == null
                || lOTrigger.ID != UWObjectMechanics.MoveTriggerId)
            {
                Check.Fail("spell trap", "level 5 slots 561/562 are not the trigger and the spell trap");

                return;
            }

            fExpectInt("spell trap: the fireball flies east (heading byte 64)", UWTrapRules.SpellTrapMissileHeading(lOTrap), 64);
            fExpectInt("spell trap: the trigger names the trap's tile", (lOTrigger.Quality << 8) | lOTrigger.Owner, (10 << 8) | 45);

            bool lbOpen = true;

            for (int liX = 10; liX <= 14; liX++)
                lbOpen &= lOLevel.GetTile(liX, 45).TileType == UWTile.TileTypeEnum.open;

            fExpectBool("spell trap: the corridor to 14/45 is open", lbOpen, true);

            int liFloorZ = (lOLevel.GetTile(14, 45).FloorHeight >> 4) << 3;
            UWCommonObjectProperties.Entry lOPlayer;
            int liPlayerHeight = pOData.CommonObjectProperties.TryGet(UWObjectMechanics.AdventurerObjectId, out lOPlayer)
                ? lOPlayer.Height : 0;

            fExpectBool("spell trap: the ball flies within a standing player's body on 14/45",
                lOTrap.ZPos > liFloorZ && lOTrap.ZPos < liFloorZ + liPlayerHeight, true);
        }


        /// <summary>A10: whack_thing poisons only past check_res(player, 1, 0x10) - the poison
        /// needles of level 3 (damage trap 828 at 1/10, quality 4, owner 1).</summary>
        private static void fCheckPoisonTrap(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[2];
            UWObject lOTrap = lOLevel.Masterlist.Count > 828 ? lOLevel.Masterlist[828] : null;

            if (lOTrap == null || lOTrap.ID != UWObjectMechanics.DamageTrapId || lOTrap.Owner == 0)
            {
                Check.Fail("poison trap", "level 3 slot 828 is no poison trap");

                return;
            }

            FakeTrapHost lOHost = new FakeTrapHost(pOData, 2);

            UWTrapRules.SetOffDisarmableTrap(lOTrap, null, null, 1, 10, lOHost);
            fExpectInt("poison trap: poisons with its quality", lOHost.PlayerPoison, lOTrap.Quality);

            lOHost = new FakeTrapHost(pOData, 2) { PlayerDamageTypeProof = UWDamageTypes.Poison };

            UWTrapRules.SetOffDisarmableTrap(lOTrap, null, null, 1, 10, lOHost);
            fExpectInt("poison trap: a poison resistance keeps it off", lOHost.PlayerPoison, 0);
            fExpectInt("poison trap: ... and does no damage either", lOHost.PlayerDamage, 0);

            UWPlayerVitals lOVitals = new UWPlayerVitals(new FakeVitalsHost());

            lOVitals.Initialise(null, UWPlayerVitals.StartValues.Default);

            lOVitals.ApplyPoison(20);
            fExpectInt("poison: the field keeps four bits (20 -> 4)", lOVitals.Poison, 4);
        }

        /// <summary>A4, A13: cast_trap_spell lays trigger and trap at the head of the tile's list,
        /// any tile, with their numbers; the trigger is a creature's only, one-shot, and the first
        /// creature takes both out again with their slots.</summary>
        /// <summary>UseTrigger (TRIGGER.C) asks who set a trigger off for every kind: the player
        /// uses, looks and picks up, so every use, look and pick-up trigger the shipped levels can
        /// reach - from a tile, along the chains and the special links - must let him
        /// (UWTrapRules.MaySetOff, asked since 2026-10-13: nothing may change). Records no tile
        /// reaches are left over (two on level 9 at 0/0) and do not count.</summary>
        private static void fCheckTriggerSetters(DataImport pOData)
        {
            int liCount = 0;
            int liRefused = 0;

            for (int liLevel = 0; liLevel < 9; liLevel++)
            {
                UWLevel lOLevel = pOData.Levels[liLevel];
                List<UWObject> lOMaster = lOLevel.Masterlist;
                HashSet<UWObject> lOSeen = new HashSet<UWObject>();
                Stack<UWObject> lOOpen = new Stack<UWObject>();

                foreach (UWTile lOTile in lOLevel.TileData)
                    if (lOTile != null && lOTile.ObjectsInTile != null)
                        foreach (UWObject lOObject in lOTile.ObjectsInTile)
                            lOOpen.Push(lOObject);

                while (lOOpen.Count > 0)
                {
                    UWObject lOObject = lOOpen.Pop();

                    if (lOObject == null || !lOSeen.Add(lOObject))
                        continue;

                    UWObject lONext = lOObject.GetNextObjectInLine(lOMaster);
                    UWObject lOLinked = UWObjectMechanics.GetLinkedObject(lOObject, lOMaster);

                    if (lONext != null)
                        lOOpen.Push(lONext);

                    if (lOLinked != null)
                        lOOpen.Push(lOLinked);

                    if (lOObject.ID != UWObjectMechanics.UseTriggerId && lOObject.ID != UWObjectMechanics.LookTriggerId
                        && lOObject.ID != UWObjectMechanics.PickUpTriggerId)
                        continue;

                    liCount++;

                    if (!UWTrapRules.MaySetOff(lOObject, UWTrapRules.Setter.Player))
                        liRefused++;
                }
            }

            fExpectBool("trigger setters: use, look and pick-up triggers reached", liCount > 0, true);
            fExpectInt("trigger setters: every one lets the player set it off", liRefused, 0);
        }

        /// <summary>change_terrain (WORLDEV.C): a rising tile lifts what lies below the new floor,
        /// a sinking one takes what rests on the old floor; a trigger stays, and a thing above the
        /// old floor stays when the tile sinks (UWTrapRules.SetTileHeight, 2026-10-13).</summary>
        private static void fCheckTileHeightCarries(DataImport pOData)
        {
            UWTile lOTile = new UWTile(0, (4u << 4) | 1u, pOData.Levels[0].TextureInfos);
            UWObject lOCoin = new UWObject(0xA0) { ZPos = 4 << 3 };
            UWObject lOHigh = new UWObject(0xA0) { ZPos = (4 << 3) + 5 };
            UWObject lOTrigger = new UWObject((ushort)UWObjectMechanics.MoveTriggerId) { ZPos = 0 };
            FakeTrapHost lOHost = new FakeTrapHost(pOData, 0);

            lOTile.ObjectsInTile.Add(lOCoin);
            lOTile.ObjectsInTile.Add(lOHigh);
            lOTile.ObjectsInTile.Add(lOTrigger);

            UWTrapRules.SetTileHeight(lOTile, 6, lOHost);
            fExpectInt("tile height: rising lifts the coin onto the new floor", lOCoin.ZPos, 6 << 3);
            fExpectInt("tile height: rising lifts what lies below the new floor", lOHigh.ZPos, 6 << 3);
            fExpectInt("tile height: a trigger stays", lOTrigger.ZPos, 0);

            lOHigh.ZPos = (6 << 3) + 5;
            UWTrapRules.SetTileHeight(lOTile, 3, lOHost);
            fExpectInt("tile height: sinking takes what rests on the old floor", lOCoin.ZPos, 3 << 3);
            fExpectInt("tile height: sinking leaves what lies above it", lOHigh.ZPos, (6 << 3) + 5);
            fExpectInt("tile height: the pictures follow (three moves)", lOHost.HeightMoves, 3);
            fExpectInt("tile height: the floor itself", lOTile.FloorHeight >> 4, 3);
        }

        private static void fCheckWardRune(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[0];
            UWTileQueries lOQueries = new UWTileQueries(lOLevel, pOData);
            UWWorldEdit lOEdit = new UWWorldEdit(lOQueries);

            if (!fFindWorldEditTiles(lOQueries, out UWTilePos lOOpen, out UWTilePos _, out UWTilePos lOSolid))
            {
                Check.Fail("ward rune", "level 1 has no open tile");

                return;
            }

            string lsPlaced = pOData.GetGeneralMessage(277) ?? string.Empty;

            fExpectBool("ward rune: message 277 is the placement", lsPlaced.StartsWith("The Rune of Warding is placed"), true);

            UWTile lOTile = lOLevel.GetTile(lOOpen.X, lOOpen.Y);
            int liFreeBefore = lOLevel.FreeStaticSlots.Count;

            fExpectBool("ward rune: laid on an open tile", UWTrapRules.CastTrapSpell(lOEdit, pOData.CommonObjectProperties,
                lOOpen.X, lOOpen.Y, UWObjectMechanics.WardTrapId), true);

            UWObject lOTrap = lOTile.ObjectsInTile.Count > 1 ? lOTile.ObjectsInTile[0] : null;
            UWObject lOTrigger = lOTile.ObjectsInTile.Count > 1 ? lOTile.ObjectsInTile[1] : null;

            if (lOTrap == null || lOTrap.ID != UWObjectMechanics.WardTrapId || lOTrigger == null
                || lOTrigger.ID != UWObjectMechanics.MoveTriggerId)
            {
                Check.Fail("ward rune", "the trap and the trigger are not the head of the tile's list");

                return;
            }

            fExpectInt("ward rune: two static slots taken", lOLevel.FreeStaticSlots.Count, liFreeBefore - 2);
            fExpectInt("ward rune: the trigger links the trap by number", lOTrigger.Quantity, lOLevel.Masterlist.IndexOf(lOTrap));
            fExpectInt("ward rune: trigger flags 8", lOTrigger.Flags, 8);
            fExpectBool("ward rune: invisible, door bit and quantity bit", lOTrigger.IsHidden && lOTrigger.DoorDirection
                && lOTrigger.HasQuantity && lOTrap.IsHidden && lOTrap.DoorDirection && lOTrap.HasQuantity, true);
            fExpectInt("ward rune: the trigger aims at its own tile", (lOTrigger.Quality << 8) | lOTrigger.Owner,
                (lOOpen.X << 8) | lOOpen.Y);
            fExpectInt("ward rune: trap flags 1 (one trigger)", lOTrap.Flags, 1);
            fExpectInt("ward rune: trap quality 0x3F", lOTrap.Quality, 0x3F);
            fExpectInt("ward rune: on the floor", lOTrigger.ZPos, (lOTile.FloorHeight >> 4) << 3);
            fExpectBool("ward rune: the player never sets it off", UWTrapRules.MaySetOff(lOTrigger, UWTrapRules.Setter.Player), false);
            fExpectBool("ward rune: a thing never sets it off", UWTrapRules.MaySetOff(lOTrigger, UWTrapRules.Setter.Thing), false);

            // An older save of ours laid it with flags 0 on both; reading the level repairs it.
            lOTrigger.Flags = 0;
            lOTrap.Flags = 0;
            fExpectInt("ward rune: an old save's rune is repaired once", UWTrapRules.RepairOldWardRunes(lOLevel), 1);
            fExpectBool("ward rune: ... a creature sets it off again, the trap counts one",
                UWTrapRules.MaySetOff(lOTrigger, UWTrapRules.Setter.Creature) && lOTrap.Flags == 1, true);
            fExpectInt("ward rune: ... and a second read finds nothing more", UWTrapRules.RepairOldWardRunes(lOLevel), 0);
            fExpectBool("ward rune: a creature sets it off", UWTrapRules.MaySetOff(lOTrigger, UWTrapRules.Setter.Creature), true);

            FakeTrapHost lOHost = new FakeTrapHost(pOData, 0) { HasTriggeringCreature = true, PlayerCastingSkill = 10 };

            UWTrapRules.TryFireMoveTrigger(lOTrigger, lOHost);

            fExpectInt("ward rune: the creature is hit once", lOHost.WardHits, 1);
            fExpectBool("ward rune: trigger and trap are gone from the tile",
                lOTile.ObjectsInTile.Contains(lOTrap) || lOTile.ObjectsInTile.Contains(lOTrigger), false);
            fExpectInt("ward rune: both slots are free again", lOLevel.FreeStaticSlots.Count, liFreeBefore);

            UWTile lOWall = lOLevel.GetTile(lOSolid.X, lOSolid.Y);

            fExpectBool("ward rune: laid in a wall too", UWTrapRules.CastTrapSpell(lOEdit, pOData.CommonObjectProperties,
                lOSolid.X, lOSolid.Y, UWObjectMechanics.WardTrapId), true);
            fExpectBool("ward rune: ... in the wall's list", lOWall.ObjectsInTile.Count > 1
                && lOWall.ObjectsInTile[0].ID == UWObjectMechanics.WardTrapId, true);
        }

        /// <summary>A trigger in the tile lists of a level whose special link is the given slot.</summary>
        private static UWObject fFindTriggerOf(UWLevel pOLevel, int piTrapIndex)
        {
            foreach (UWTile lOTile in pOLevel.TileData)
            {
                if (lOTile == null || lOTile.ObjectsInTile == null)
                    continue;

                foreach (UWObject lOObject in lOTile.ObjectsInTile)
                {
                    if (lOObject != null && lOObject.ID >= UWObjectIds.FirstTrigger && lOObject.ID <= UWObjectIds.LastTrigger
                        && lOObject.Quantity == piTrapIndex)
                        return lOObject;
                }
            }

            return null;
        }

        /// <summary>A1: UseTrap has no case for TRAP_PIT. The one pit trap of the game, level 3 slot
        /// 583 at 6/4, behind the use trigger 995 in the chain of the check trap at 5/5 - its tile
        /// stays the solid wall of height 15 it is.</summary>
        private static void fCheckPitTrap(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels[2];
            UWObject lOPit = lOLevel.Masterlist.Count > 995 ? lOLevel.Masterlist[583] : null;
            UWObject lOTrigger = lOPit != null ? lOLevel.Masterlist[995] : null;

            if (lOPit == null || lOPit.ID != UWObjectMechanics.PitTrapId || lOTrigger == null || lOTrigger.Quantity != 583)
            {
                Check.Fail("pit trap", "level 3 slot 583 or its trigger 995 not found");

                return;
            }

            UWTile lOTile = lOLevel.GetTile(6, 4);
            FakeTrapHost lOHost = new FakeTrapHost(pOData, 2);

            fExpectBool("pit trap: the chain runs", UWTrapRules.FireTraps(lOTrigger, null, lOHost), true);
            fExpectBool("pit trap: level 3, 6/4 stays solid", lOTile.TileType == UWTile.TileTypeEnum.solid, true);
            fExpectInt("pit trap: ... at height 15", lOTile.FloorHeight >> 4, 15);
            fExpectInt("pit trap: nothing is rebuilt", lOHost.Rebuilds, 0);
        }

        /// <summary>
        /// The game around one firing of the trap rules, recording what the rules asked of it.
        /// The player stands on 0/0 with no inventory unless a check gives him one.
        ///
        /// SINCE CLEANUP STEP 3.6 (2026-10-17) the world of the firing is a FakeWorld (WorldChecks.cs)
        /// over the real level: the player's numbers are real vitals, the messages, shakes, blows,
        /// spawns and removals its logs. The counters below read those logs, so the checks still ask
        /// what they asked before.
        /// </summary>
        private sealed class FakeTrapHost : IUWTrapHost
        {
            public readonly FakeWorld Fake;

            public FakeTrapHost(DataImport pOData, int piLevelIndex)
            {
                Fake = new FakeWorld
                {
                    Data = pOData,
                    Level = pOData.Levels[piLevelIndex],
                    LevelIndex = piLevelIndex,
                    LevelCount = pOData.Levels.Count,
                    RemovesFromTiles = true
                };

                Fake.Body.Floor = new UWSpot(new UWTilePos(0, 0), 0, 0, 0);
                Fake.Body.Numbers = fTrapPlayer(-1);
            }

            /// <summary>A player with this casting skill (-1: the default).</summary>
            private static UWPlayerVitals fTrapPlayer(int piCastingSkill)
            {
                UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

                lOStart.CastingSkill = piCastingSkill;

                UWPlayerVitals lOVitals = new UWPlayerVitals(new FakeVitalsHost());

                lOVitals.Initialise(null, lOStart);

                return lOVitals;
            }

            public IUWWorld World => Fake;

            public UWTrapFiring Firing => new UWTrapFiring(HasTriggeringCreature, HasTriggeringThing);

            public bool HasTriggeringCreature { get; set; }

            public bool HasTriggeringThing { get; set; }

            public bool AlwaysFireCreateObjectTraps { get; set; }

            // ------------------------------------------------- What the check sets on the player

            public UWTilePos PlayerTile
            {
                get { return Fake.Body.Floor.Tile; }
                set { Fake.Body.Floor = new UWSpot(value, 0, 0, 0); }
            }

            public UWInventoryModel Inventory
            {
                get { return Fake.Body.Carried; }
                set { Fake.Body.Carried = value; }
            }

            public int PlayerCastingSkill
            {
                get { return Fake.Body.Numbers.GetSkill(UWPlayerData.Skill.Casting); }
                set { Fake.Body.Numbers = fTrapPlayer(value); }
            }

            public int PlayerDamageTypeProof
            {
                get { return Fake.Body.Proof; }
                set { Fake.Body.Proof = value; }
            }

            // ------------------------------------------------- What the check reads

            /// <summary>The untyped damage the player took (the trap's UWBlow on ThePlayer); setting
            /// it to 0 forgets the blows so far.</summary>
            public int PlayerDamage
            {
                get
                {
                    int liSum = 0;

                    foreach (string lsLine in Fake.Blows)
                    {
                        if (lsLine.StartsWith("player takes "))
                            liSum += int.Parse(lsLine.Substring("player takes ".Length).Split(' ')[0]);
                    }

                    return liSum;
                }
                set
                {
                    if (value == 0)
                        Fake.Blows.RemoveAll(psLine => psLine.StartsWith("player takes "));
                }
            }

            public int PlayerPoison => Fake.Body.Numbers.Poison;

            public int Curses => Fake.Blows.FindAll(psLine => psLine.StartsWith("player cursed")).Count;

            public int Shakes => Fake.Shown.Log.FindAll(psLine => psLine.StartsWith("shake")).Count;

            public int Bounces => Fake.Body.Bounces;

            /// <summary>The lines for the scroll: a composed one as it is, a line of string block 1
            /// as "#n".</summary>
            public List<string> Messages
            {
                get
                {
                    List<string> lOLines = new List<string>();

                    foreach (string lsLine in Fake.Shown.Log)
                    {
                        if (lsLine.StartsWith("message "))
                            lOLines.Add(lsLine.Substring("message ".Length));
                        else if (lsLine.StartsWith("general "))
                            lOLines.Add("#" + lsLine.Substring("general ".Length));
                    }

                    return lOLines;
                }
            }

            public List<UWObject> Spawned => Fake.Spawned;

            public List<UWObject> Removed => Fake.Removed;

            // ------------------------------------------------- The firing's own

            public void AdvanceLever(UWObject pOSwitchObject) { }

            public int Toggles;

            public void ToggleSwitchVisual(UWObject pOSwitchObject) { Toggles++; }

            public int Rebuilds;

            public void RebuildGeometry() { Rebuilds++; }

            public void OnObjectHeightChanged(UWObject pOObject, int piOldZPos) { HeightMoves++; }

            public void MoveCreaturesWithFloor(int piTileX, int piTileY, int piOldZPos, int piNewZPos) { }

            public int HeightMoves;

            public void SpawnPendingObjectsInTile(int piTileX, int piTileY) { }

            public void StrikeTriggeringThing(int piDamage) { }

            public int CreatureDamage;

            public void DamageTriggeringCreature(int piDamage) { CreatureDamage += piDamage; }

            public int WardHits;

            public void ApplyWardDamage(int piDamage, int piWardMessage, int piFirstDirectionMessage) { WardHits++; }

            public bool FireArrowTrap(UWObject pOTrap, int piTileX, int piTileY) => true;

            public readonly List<int> SpellsCast = new List<int>();

            public bool FireSpellTrap(UWObject pOTrap, int piTileX, int piTileY, int piMajorClass, int piMinorClass)
            {
                SpellsCast.Add((piMajorClass << 4) | piMinorClass);

                return true;
            }

            public bool FireCameraTrap(UWObject pOTrap, int piTileX, int piTileY) => true;

            public int AngerRaceNearPlayer(int piOwner, int piTilesBack, int piTilesForward) => 0;

            public bool BeginConversation(int piSlot) => true;

            public UWObject TalkedTo;

            public bool TalkTo(UWObject pONpcData)
            {
                TalkedTo = pONpcData;

                return true;
            }

            public bool PlayVictory() => true;
        }
    }
}
