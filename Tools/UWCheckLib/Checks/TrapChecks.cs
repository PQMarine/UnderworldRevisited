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
    public static partial class SelfChecks
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
            fCheckDroppedTrapFound(pOData);
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
        /// <summary>
        /// A THING CARRIED AND DROPPED keeps links that count in the save's records (2026-10-22, per
        /// user: the poisoned potion taken from the pack and put down asked no trap question): its
        /// resolved chain decides, the level record its stale link points at must not. Both forms,
        /// the trap straight under the potion and a trigger with the trap behind it.
        /// </summary>
        private static void fCheckDroppedTrapFound(DataImport pOData)
        {
            List<UWObject> lOSave = new List<UWObject>
            {
                null,
                new UWObject(0xBB) { HasQuantity = false, Quantity = 2 },
                new UWObject((ushort)UWObjectMechanics.DamageTrapId) { Quality = 10, Owner = 1 }
            };

            UWObject lOPotion = lOSave[1];

            lOPotion.EnsureContentsLoaded(lOSave);

            // The level: at the potion's stale link 2 lies something else entirely.
            List<UWObject> lOLevel = new List<UWObject> { null, new UWObject(0x80), new UWObject(0x95) };

            bool lbFound = UWObjectMechanics.TryFindDisarmableTrap(lOPotion, lOLevel, out UWObject lOTrap, out UWObject lOTrigger);

            fExpectBool("dropped trap: a carried potion's trap is found through its resolved chain", lbFound, true);
            fExpectBool("dropped trap: ... it is the potion's damage trap, no trigger",
                lOTrap == lOSave[2] && lOTrigger == null, true);

            List<UWObject> lOSaveTriggered = new List<UWObject>
            {
                null,
                new UWObject(0xBB) { HasQuantity = false, Quantity = 2 },
                new UWObject((ushort)UWObjectMechanics.MoveTriggerId) { HasQuantity = false, Quantity = 3 },
                new UWObject((ushort)UWObjectMechanics.DamageTrapId) { Quality = 4 }
            };

            foreach (UWObject lORecord in lOSaveTriggered)
                lORecord?.EnsureContentsLoaded(lOSaveTriggered);

            lbFound = UWObjectMechanics.TryFindDisarmableTrap(lOSaveTriggered[1], lOLevel, out lOTrap, out lOTrigger);

            fExpectBool("dropped trap: behind a trigger, through the trigger's resolved chain",
                lbFound && lOTrap == lOSaveTriggered[3] && lOTrigger == lOSaveTriggered[2], true);
        }

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
    }
}
