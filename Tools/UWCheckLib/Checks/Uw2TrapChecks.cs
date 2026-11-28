using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE SECOND GAME'S TRIGGERS AND TRAP CHAINS (UWTrapRulesUw2, G3 part H1, 2026-11-13): the trigger modes against
        /// OBJECTS.DAT, the setter test, do_math_op, the numbered variables and the variable check; on the game's own
        /// levels a one-shot set-variable chain and a variable check with its damage trap behind it.
        /// </summary>
        private static void fCheckUw2Traps(CheckContext pOContext)
        {
            Check.Group("The second game's triggers and trap chains (G3 part H1)");

            Check.Str("uw2 traps: the mode of 0x1A0-0x1AC (and the same for 0x1B0-0x1BC)",
                string.Join(" ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(0, 13), i => UWTrapRulesUw2.ModeOf(0x1A0 + i).ToString("X")))
                + " | " + UWTrapRulesUw2.ModeOf(0x1B4).ToString("X"),
                "0 2 4 5 7 F 6 E B A 8 9 C | 7");
            Check.Str("uw2 traps: triggers are minor 2 and 3, traps minor 0 and 1",
                UWTrapRulesUw2.IsTrigger(0x1A0) + " " + UWTrapRulesUw2.IsTrigger(0x1BF) + " " + UWTrapRulesUw2.IsTrigger(0x19F) + " "
                + UWTrapRulesUw2.IsTrap(0x180) + " " + UWTrapRulesUw2.IsTrap(0x1A0),
                "True True False True False");

            // UseTrigger's setter test: the player bit 11, a creature bit 12, a thing bit 9 (the first game lets a thing
            // through unless the trigger is enchanted without bit 11).
            UWObject lOPlayerOnly = new UWObject(0x1A0) { Flags = 4 };
            UWObject lOThingToo = new UWObject(0x1A0) { Flags = 5 };
            UWObject lOCreatureOnly = new UWObject(0x1A0) { Flags = 8 };

            Check.Str("uw2 traps: who sets off flags 4, 5 and 8 (player creature thing), and the first game's thing for flags 4",
                fSetters(lOPlayerOnly) + " " + fSetters(lOThingToo) + " " + fSetters(lOCreatureOnly) + " "
                + UWTrapRules.MaySetOff(lOPlayerOnly, UWTrapRules.Setter.Thing),
                "100 101 010 True");

            Check.Str("uw2 traps: do_math_op's eight ops on 6 and 3, and 9 for an unknown op",
                UWTrapRulesUw2.DoMathOp(6, 0, 3) + " " + UWTrapRulesUw2.DoMathOp(6, 1, 3) + " " + UWTrapRulesUw2.DoMathOp(6, 2, 3) + " "
                + UWTrapRulesUw2.DoMathOp(6, 3, 3) + " " + UWTrapRulesUw2.DoMathOp(6, 4, 3) + " " + UWTrapRulesUw2.DoMathOp(6, 5, 3) + " "
                + UWTrapRulesUw2.DoMathOp(6, 6, 3) + " " + UWTrapRulesUw2.DoMathOp(6, 7, 3) + " " + UWTrapRulesUw2.DoMathOp(3, 7, 3) + " "
                + UWTrapRulesUw2.DoMathOp(9, 8, 3),
                "9 3 3 2 7 5 48 0 4 9");

            UWPlayerFile lOFile = UWPlayerRecordUw2.NewCharacter(new UWPlayerData.NewCharacter { Name = "Selfcheck" });

            // A game variable keeps a byte; a quest bit is set by right > 0, toggled by op 5, left by op 1; a quest byte
            // and an X clock take the op.
            UWTrapRulesUw2.SetNumbered(lOFile, 5, 2, 300);
            UWTrapRulesUw2.SetNumbered(lOFile, 0x105, 2, 3);
            int liQuestSet = UWPlayerRecordUw2.GetNumbered(lOFile, 0x105);
            UWTrapRulesUw2.SetNumbered(lOFile, 0x105, 1, 0);
            int liQuestKept = UWPlayerRecordUw2.GetNumbered(lOFile, 0x105);
            UWTrapRulesUw2.SetNumbered(lOFile, 0x105, 5, 0);
            UWTrapRulesUw2.SetNumbered(lOFile, 0x181, 2, 7);
            UWTrapRulesUw2.SetNumbered(lOFile, 0x181, 0, 5);
            UWTrapRulesUw2.SetNumbered(lOFile, 0x19E, 4, 0x11);

            Check.Str("uw2 traps: numbered variables - var 5 := 300 keeps 44; quest 5 set, kept by op 1, toggled; quest byte 1 7 + 5; X clock 14 | 0x11",
                UWPlayerRecordUw2.GetNumbered(lOFile, 5) + " " + liQuestSet + liQuestKept + UWPlayerRecordUw2.GetNumbered(lOFile, 0x105) + " "
                + UWPlayerRecordUw2.GetNumbered(lOFile, 0x181) + " " + UWPlayerRecordUw2.GetNumbered(lOFile, 0x19E),
                "44 110 12 17");

            // The set trap's variable and value, the check trap's range and expectation.
            UWObject lOSet = new UWObject((ushort)UWObjectMechanics.SetVariableTrapId) { ZPos = 17, XPos = 3, Quality = 0, Owner = 1, Heading = 2 };
            UWObject lOCheck = new UWObject((ushort)UWObjectMechanics.CheckVariableTrapId) { ZPos = 10, XPos = 0, Heading = 2, Quality = 0, Owner = 13, YPos = 6 };

            UWTrapRulesUw2.SetNumbered(lOFile, 10, 2, 1);
            UWTrapRulesUw2.SetNumbered(lOFile, 11, 2, 5);
            UWTrapRulesUw2.SetNumbered(lOFile, 12, 2, 6);

            Check.Str("uw2 traps: a set trap of z 17 fine x 3 names X clock 1 (0x191) with 1; a check of vars 10-12 packs 1 5 6 = 0x6E against (13 << 3) | 6",
                "0x" + UWTrapRulesUw2.SetVariableIndex(lOSet).ToString("X") + " " + UWTrapRulesUw2.SetVariableValue(lOSet) + " "
                + UWTrapRulesUw2.CheckVariableValue(lOCheck, lOFile) + " " + UWTrapRulesUw2.CheckVariableExpected(lOCheck) + " "
                + UWTrapRulesUw2.CheckVariableHolds(lOCheck, lOFile),
                "0x191 1 110 110 True");

            lOCheck.XPos = 4;

            Check.Str("uw2 traps: fine x bit 2 sums the range instead (1 + 5 + 6)", UWTrapRulesUw2.CheckVariableValue(lOCheck, lOFile).ToString(), "12");

            Check.Str("uw2 traps: the text index (quality * 32 + owner & 31, ours + 1) and the chain's end (door only with an owner)",
                UWTrapRulesUw2.TextIndex(new UWObject((ushort)UWObjectMechanics.TextStringTrapId) { Quality = 2, Owner = 0x24 }) + " "
                + UWTrapRulesUw2.EndsChain(new UWObject((ushort)UWObjectMechanics.DoorTrapId) { Owner = 0 }) + " "
                + UWTrapRulesUw2.EndsChain(new UWObject((ushort)UWObjectMechanics.DoorTrapId) { Owner = 3 }) + " "
                + UWTrapRulesUw2.EndsChain(new UWObject((ushort)UWObjectMechanics.DeleteObjectTrapId)),
                "69 False True True");

            IUWGameProfile lOUw2 = UWGameProfiles.Uw2;

            Check.Str("uw2 traps: the profile's tables - built kinds (H1a, H1b, H3: the jump), the general hacks (H6: 2)",
                lOUw2.TrapKinds.IsHandled(UWTeleportUw2.TrapId) + " " + lOUw2.TrapKinds.IsHandled(UWObjectMechanics.DamageTrapId) + " "
                + lOUw2.TrapKinds.IsHandled(UWObjectMechanics.ChangeTerrainTrapId) + " " + lOUw2.TrapKinds.IsHandled(UWObjectMechanics.SpellTrapId) + " "
                + lOUw2.TrapKinds.IsHandled(UWObjectMechanics.InventoryTrapId) + " " + lOUw2.TrapKinds.IsHandled(0x192) + " "
                + lOUw2.DoActions.IsHandled(2) + " " + lOUw2.Traps.SwapsLock(0) + " " + lOUw2.Traps.SwapsLock(1),
                "True True True True True True True False True");

            // H1b: what a trap as the caster casts, ObjWorn, and change_terrain's filter.
            Check.Str("uw2 traps: a spell trap casts 5/3, 11/9 (Tremor), 13/5, 14/4, 6/1 - not 0/0, 4/1, 9/1, 10/1, 11/2",
                UWTrapRulesUw2.SpellTrapCasts(5, 3) + " " + UWTrapRulesUw2.SpellTrapCasts(11, 9) + " " + UWTrapRulesUw2.SpellTrapCasts(13, 5) + " "
                + UWTrapRulesUw2.SpellTrapCasts(14, 4) + " " + UWTrapRulesUw2.SpellTrapCasts(6, 1) + " | " + UWTrapRulesUw2.SpellTrapCasts(0, 0) + " "
                + UWTrapRulesUw2.SpellTrapCasts(4, 1) + " " + UWTrapRulesUw2.SpellTrapCasts(9, 1) + " " + UWTrapRulesUw2.SpellTrapCasts(10, 1) + " "
                + UWTrapRulesUw2.SpellTrapCasts(11, 2),
                "True True True True True | False False False False False");
            Check.Str("uw2 traps: ObjWorn - armour slot 0, ring 9, a shield 0x3B in hand 7 (right-handed) or 8 (lefty), a sword there not, slot 11 not",
                UWTrapRulesUw2.ObjWorn(0x20, 0, false) + " " + UWTrapRulesUw2.ObjWorn(0x38, 9, false) + " " + UWTrapRulesUw2.ObjWorn(0x3B, 7, false) + " "
                + UWTrapRulesUw2.ObjWorn(0x3B, 8, true) + " " + UWTrapRulesUw2.ObjWorn(0x3B, 8, false) + " " + UWTrapRulesUw2.ObjWorn(0x04, 7, false) + " "
                + UWTrapRulesUw2.ObjWorn(0x20, 11, false),
                "True True True True False False False");

            Check.Str("uw2 traps: filter - on the square anything but traps and doors; from a neighbour nothing without a radius, nor off the old floor when it sinks",
                UWTrapRulesUw2.TerrainMoves(new UWObject(0x95) { ZPos = 0 }, 5, 5, 5, 5, 2, 5, null) + " "
                + UWTrapRulesUw2.TerrainMoves(new UWObject(0x1A0), 5, 5, 5, 5, 2, 5, null) + " "
                + UWTrapRulesUw2.TerrainMoves(new UWObject(0x140), 5, 5, 5, 5, 2, 5, null) + " "
                + UWTrapRulesUw2.TerrainMoves(new UWObject(0x95) { XPos = 7, YPos = 3, ZPos = 16 }, 4, 5, 5, 5, 2, 5, null) + " "
                + UWTrapRulesUw2.TerrainMoves(new UWObject(0x95) { XPos = 7, YPos = 3, ZPos = 8 }, 4, 5, 5, 5, 2, 1, null),
                "True False False False False");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2 trap chains", "no second game here: skipped");

                return;
            }

            byte[] lyObjects = File.ReadAllBytes(Path.Combine(lsData, "OBJECTS.DAT"));
            string lsModes = string.Empty;
            string lsOurs = string.Empty;

            for (int liAt = 0; liAt < UWTrapRulesUw2.ModeTableLength; liAt++)
            {
                lsModes += lyObjects[UWTrapRulesUw2.ModeTableOffset + liAt].ToString("X");
                lsOurs += UWTrapRulesUw2.ModeOf(0x1A0 + liAt).ToString("X");
            }

            Check.Str("uw2 traps: the mode table is OBJECTS.DAT's (trap_init at 0xD92)", lsOurs, lsModes);

            DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);

            UWSaveCodec.StartNewCharacterUw2(lOImport.Session, new UWPlayerData.NewCharacter { Name = "Selfcheck" });

            // Level 3, 15/53 and 16/53: two one-shot move triggers (flags 4) aim at one set-variable trap (flags 2) that
            // sets X clock 1, the castle's plot, to 1. Set off once, the trap and both triggers are gone.
            FakeTrapHost lOHost = new FakeTrapHost(lOImport, 2);

            lOHost.Fake.SessionOf = lOImport.Session;

            UWObject lOFirst = fFindTrigger(lOImport.Levels[2], 15, 53, 0x1A0);
            UWObject lOSecond = fFindTrigger(lOImport.Levels[2], 16, 53, 0x1A0);
            int liClockBefore = UWPlayerRecordUw2.XClock(lOImport.Session.PlayerFileUw2, 1);
            bool lbRan = UWTrapRulesUw2.UseTrigger(null, lOFirst, UWTrapRulesUw2.MoveMode, lOHost);

            Check.Str("uw2 chain: level 3 15/53 sets X clock 1 from 0 to 1 and takes trap and both triggers away",
                lbRan + " " + liClockBefore + " " + UWPlayerRecordUw2.XClock(lOImport.Session.PlayerFileUw2, 1) + " "
                + (fFindTrigger(lOImport.Levels[2], 15, 53, 0x1A0) == null) + " " + (fFindTrigger(lOImport.Levels[2], 16, 53, 0x1A0) == null)
                + " " + (lOSecond != null),
                "True 0 1 True True True");

            // Level 52, 40/45: a repeatable move trigger to a sum check of variable 0xF4 against 1, then a poison trap of 8.
            // Variable 0: the check fails, and with nothing after its link the chain stops; set to 1, the player is poisoned.
            FakeTrapHost lOPits = new FakeTrapHost(lOImport, 51);

            lOPits.Fake.SessionOf = lOImport.Session;

            UWObject lOPoisonTrigger = fFindTrigger(lOImport.Levels[51], 40, 45, 0x1A0);

            UWTrapRulesUw2.UseTrigger(null, lOPoisonTrigger, UWTrapRulesUw2.MoveMode, lOPits);
            int liPoisonBefore = lOPits.Fake.Body.Numbers.Poison;
            UWTrapRulesUw2.SetNumbered(lOImport.Session.PlayerFileUw2, 0xF4, 2, 1);
            UWTrapRulesUw2.UseTrigger(null, lOPoisonTrigger, UWTrapRulesUw2.MoveMode, lOPits);

            // filter with COMOBJ's radius: a thing on the west neighbour reaches over the edge when fine x + radius is 8.
            int liRadiusId = 0x95;
            int liRadius = lOImport.CommonObjectProperties.TryGet(liRadiusId, out UWCommonObjectProperties.Entry lORadiusEntry) ? lORadiusEntry.Radius : 0;

            Check.Str("uw2 traps: filter with the radius of 0x95 - fine x 8 - r reaches over from the west, 7 - r does not",
                (liRadius > 0) + " " + UWTrapRulesUw2.TerrainMoves(new UWObject((ushort)liRadiusId) { XPos = (ushort)(8 - liRadius), ZPos = 16 }, 4, 5, 5, 5, 2, 5, lOImport.CommonObjectProperties)
                + " " + UWTrapRulesUw2.TerrainMoves(new UWObject((ushort)liRadiusId) { XPos = (ushort)(7 - liRadius), ZPos = 16 }, 4, 5, 5, 5, 2, 5, lOImport.CommonObjectProperties),
                "True True False");

            // H1b on the game's levels. Level 25, 31/6: a use trigger to a change terrain trap at 26/2 (height 12, a type
            // from its heading and quality) and a text trap behind it.
            FakeTrapHost lOIce = new FakeTrapHost(lOImport, 24);

            lOIce.Fake.SessionOf = lOImport.Session;

            UWObject lOIceTrigger = fFindTriggerDeep(lOImport.Levels[24], 31, 6, 0x1A2);
            UWObject lOIceTrap = UWObjectMechanics.GetLinkedObject(lOIceTrigger, lOImport.Levels[24].Masterlist);
            int liIceType = (lOIceTrap.Heading << 1) + (lOIceTrap.Quality & 1);

            UWTrapRulesUw2.UseTrigger(null, lOIceTrigger, UWTrapRulesUw2.UseMode, lOIce);

            UWTile lOIceTile = lOImport.Levels[24].GetTile(26, 2);

            Check.Str("uw2 chain: level 25 31/6 (use) sets 26/2 to height 12 and its type, keeps floor and wall (15, 63), then speaks",
                (lOIceTile.FloorHeight >> 4) + " " + ((int)lOIceTile.TileType == liIceType) + " " + (lOIceTrap.Quality >> 1) + " " + lOIceTrap.Owner + " "
                + lOIce.Fake.Shown.Log.Exists(l => l.StartsWith("message You hear the faint")),
                "12 True 15 63 True");

            // Level 1, 28/31: a use trigger to a door trap of quality 3 (toggle) and owner 0 - the door moves, its lock stays.
            FakeTrapHost lOCastle = new FakeTrapHost(lOImport, 0);

            lOCastle.Fake.SessionOf = lOImport.Session;

            UWObject lODoorData = UWTrapRules.FindDoorInTile(29, 31, lOImport.Levels[0]);

            if (lODoorData != null)
                lOCastle.Fake.DoorList.Add(new FakeDoor(lOCastle.Fake.Log, lODoorData, true));

            UWTrapRulesUw2.UseTrigger(null, fFindTriggerDeep(lOImport.Levels[0], 28, 31, 0x1A2), UWTrapRulesUw2.UseMode, lOCastle);

            Check.Str("uw2 chain: level 1 28/31 (use) toggles the door at 29/31 and leaves its lock (owner 0)",
                (lODoorData != null) + " " + lOCastle.Fake.Log.Exists(l => l.Contains("opens")) + " " + lOCastle.Fake.Log.Exists(l => l.Contains("lock replaced")),
                "True True False");

            // Level 13, 33/37: an unlock trigger to a delete trap on 33/37 whose victim is a damage trap there - the trap
            // goes with the hidden use trigger aiming at it (trap_obj_del -> delete_trap).
            FakeTrapHost lOTower = new FakeTrapHost(lOImport, 12);

            lOTower.Fake.SessionOf = lOImport.Session;

            UWObject lOUnlock = fFindTriggerDeep(lOImport.Levels[12], 33, 37, 0x1A8);
            UWObject lODelete = UWObjectMechanics.GetLinkedObject(lOUnlock, lOImport.Levels[12].Masterlist);
            UWObject lOVictim = UWObjectMechanics.GetLinkedObject(lODelete, lOImport.Levels[12].Masterlist);
            bool lbVictimThere = lOImport.Levels[12].GetTile(33, 37).ObjectsInTile.Contains(lOVictim);

            UWTrapRulesUw2.UseTrigger(null, lOUnlock, UWTrapRulesUw2.UnlockMode, lOTower);

            Check.Str("uw2 chain: level 13 33/37 (unlock) deletes the damage trap there and the hidden use trigger aiming at it",
                "0x" + lOVictim.ID.ToString("X") + " " + lbVictimThere + " " + lOImport.Levels[12].GetTile(33, 37).ObjectsInTile.Contains(lOVictim) + " "
                + (fFindTrigger(lOImport.Levels[12], 33, 37, 0x1B2) == null),
                "0x180 True False True");

            Check.Str("uw2 chain: level 52 40/45 poisons only once variable 0xF4 is 1; the trigger stays (flags 6); a thing cannot set it off",
                liPoisonBefore + " " + lOPits.Fake.Body.Numbers.Poison + " " + (fFindTrigger(lOImport.Levels[51], 40, 45, 0x1A0) != null) + " "
                + UWTrapRulesUw2.MaySetOff(lOPoisonTrigger, UWTrapRules.Setter.Thing),
                "0 8 True False");

            fCheckUw2TrapSearch(lsData);
            fCheckUw2NewKinds(lsData);
            fCheckUw2TriggerSites(lsData);
            fCheckUw2Wandering(lsData);
            fCheckUw2Hacks(lsData);
            fCheckUw2WorldHacks(lsData);
            fCheckUw2LastHacks(lsData);
            fCheckUw2Use(lsData);
            fCheckUw2Djinn(lsData);
            fCheckUw2NewCharacterSave();
        }

        /// <summary>
        /// A new character saved with one thing carried (2026-11-14, per user: "Save Game Failed" with a honeycomb):
        /// the new file's workspace holds the empty record 0, the write takes the honeycomb and reads back; a workspace
        /// of the header alone (made before the fix) is padded rather than refused.
        /// </summary>
        private static void fCheckUw2NewCharacterSave()
        {
            UWPlayerFile lOFile = UWPlayerRecordUw2.NewCharacter(new UWPlayerData.NewCharacter { Name = "Selfcheck", Strength = 20 });
            UWObject[] lOEquipment = new UWObject[UWInventoryWorkspace.EquipmentSlotCount];
            UWObject[] lOBackpack = new UWObject[UWInventoryWorkspace.BackpackSlotCount];

            lOBackpack[0] = new UWObject(0xBA) { Quality = 40 };

            string lsNew = UWInventoryWorkspace.WriteInto(lOFile, lOEquipment, lOBackpack, null, null, 3);
            UWInventoryWorkspace lOBack = UWInventoryWorkspace.Read(UWPlayerFile.Read(lOFile.ToBytes(), UWGame.Uw2));

            UWPlayerFile lOShort = UWPlayerFile.Create(UWGame.Uw2, (byte[])lOFile.Record.Clone(), 1, new byte[UWPlayerFile.WorkspaceHeaderSize]);
            string lsShort = UWInventoryWorkspace.WriteInto(lOShort, lOEquipment, lOBackpack, null, null, 3);

            Check.Str("uw2 new character: a fresh file's workspace, the save with a honeycomb, read back; a header-only workspace padded",
                lOFile.Workspace.Length + " " + (lsNew ?? "none") + " " + (lOBack.Backpack[0] != null ? lOBack.Backpack[0].ID.ToString("X") : "-") + " " + (lsShort ?? "none"),
                UWPlayerRecordUw2.NewWorkspaceSize + 8 + " none BA none");
        }

        /// <summary>
        /// do_filanium (2026-11-14): level 26's mud at 58/15 is water on texture 0xC1; the basilisk oil of level 59 (a
        /// colorless potion at 53/41, spell 0xD/3 cast on use) moves the djinn clock from 0 to 2 there, never back from 3;
        /// the same potion on other water, and a potion of another spell in the mud, do nothing.
        /// </summary>
        private static void fCheckUw2Djinn(string psData)
        {
            DataImport lOImport = new DataImport(psData, null, null, UWGameProfiles.Uw2);

            UWSaveCodec.StartNewCharacterUw2(lOImport.Session, new UWPlayerData.NewCharacter { Name = "Selfcheck" });

            UWPlayerFile lOFile = lOImport.Session.PlayerFileUw2;
            UWTile lOMud = lOImport.Levels[25].GetTile(58, 15);
            UWTile lOWater = null;

            for (int liTile = 0; liTile < 64 * 64 && lOWater == null; liTile++)
            {
                UWTile lOAt = lOImport.Levels[25].GetTile(liTile & 63, liTile >> 6);

                if (lOAt != null && lOAt.TextureFloor != UWDjinnRulesUw2.MudTexture && lOImport.Terrain.IsWaterFloor(lOAt.TextureFloor))
                    lOWater = lOAt;
            }

            UWObject lOOil = lOImport.Levels[58].GetTile(53, 41).ObjectsInTile.Find(o => o != null && o.ID == 0xE6);
            UWObject lOOther = new UWObject(0xE6) { HasQuantity = lOOil != null && lOOil.HasQuantity, IsEnchanted = lOOil != null && lOOil.IsEnchanted,
                Flags = lOOil != null ? lOOil.Flags : (ushort)0, Quantity = 0x244 };

            UWPlayerRecordUw2.SetXClock(lOFile, UWPlayerRecordUw2.DjinnClock, 0);

            bool lbOtherWater = UWDjinnRulesUw2.OilInMud(lOOil, lOWater != null ? lOWater.TextureFloor : -1, null, lOFile);
            bool lbOtherSpell = UWDjinnRulesUw2.OilInMud(lOOther, lOMud.TextureFloor, null, lOFile);
            int liBefore = UWPlayerRecordUw2.XClock(lOFile, UWPlayerRecordUw2.DjinnClock);
            bool lbOil = UWDjinnRulesUw2.OilInMud(lOOil, lOMud.TextureFloor, null, lOFile);
            int liAfter = UWPlayerRecordUw2.XClock(lOFile, UWPlayerRecordUw2.DjinnClock);

            UWPlayerRecordUw2.SetXClock(lOFile, UWPlayerRecordUw2.DjinnClock, 3);
            UWDjinnRulesUw2.OilInMud(lOOil, lOMud.TextureFloor, null, lOFile);

            Check.Str("uw2 djinn: level 26 58/15 is mud water (0xC1); the oil (link 0x243) moves 0 -> 2 there, not on other water, not a potion of 0x244, never back from 3",
                (lOMud.TextureFloor == UWDjinnRulesUw2.MudTexture) + " " + lOImport.Terrain.IsWaterFloor(lOMud.TextureFloor) + " " + (lOOil != null ? lOOil.Quantity.ToString("X") : "none")
                + " " + lbOtherWater + " " + lbOtherSpell + " " + liBefore + " " + lbOil + " " + liAfter + " " + UWPlayerRecordUw2.XClock(lOFile, UWPlayerRecordUw2.DjinnClock),
                "True True 243 False False 0 True 2 3");

            // Put down in the mud the oil sinks (obj_deal, Obj_Punt): its 0x243 is a special property, no pile
            // (chkTenacious counts is_quant without LINK_SPECIAL); until 2026-11-14 ours read it as 0x243 pieces.
            UWTileQueries lOQueries = new UWTileQueries(lOImport.Levels[25], lOImport);
            bool lbAlways = true;

            for (int liTry = 0; liTry < 20; liTry++)
                lbAlways &= lOQueries.TileSwallows(lOMud, lOOil);

            Check.Str("uw2 djinn: the oil put down in the mud sinks every time (a special property is no pile)", lbAlways.ToString(), "True");

            // remove_object's bottle: before 5 the daemon rends; from 5 elsewhere wasted; at the sigil (level 69, 21-22/52-53)
            // absorbed - clock 6, castle 0xD -> 0xE, quests[26] bit 1; the castle left alone when it is not at 0xD. Level 69
            // holds a bottle at 3/20, and it can break (its quality class is not 3).
            UWPlayerRecordUw2.SetXClock(lOFile, UWPlayerRecordUw2.DjinnClock, 4);
            UWDjinnRulesUw2.BottleOutcome leEarly = UWDjinnRulesUw2.BottleBroken(UWDjinnRulesUw2.BottleId, lOFile, 69, 21, 52);
            UWPlayerRecordUw2.SetXClock(lOFile, UWPlayerRecordUw2.DjinnClock, 5);
            UWDjinnRulesUw2.BottleOutcome leElsewhere = UWDjinnRulesUw2.BottleBroken(UWDjinnRulesUw2.BottleId, lOFile, 69, 23, 52);
            UWDjinnRulesUw2.BottleOutcome leOther = UWDjinnRulesUw2.BottleBroken(0xE6, lOFile, 69, 21, 52);
            int liAfterMiss = UWPlayerRecordUw2.XClock(lOFile, UWPlayerRecordUw2.DjinnClock);

            UWPlayerRecordUw2.SetXClock(lOFile, UWTrapRulesUw2.CastleXClock, 0xD);
            UWDjinnRulesUw2.BottleOutcome leSigil = UWDjinnRulesUw2.BottleBroken(UWDjinnRulesUw2.BottleId, lOFile, 69, 22, 53);
            string lsAbsorbed = UWPlayerRecordUw2.XClock(lOFile, UWPlayerRecordUw2.DjinnClock) + " " + UWPlayerRecordUw2.XClock(lOFile, UWTrapRulesUw2.CastleXClock)
                + " " + UWPlayerRecordUw2.GetQuest(lOFile, UWDjinnRulesUw2.DaemonHeldQuest);

            UWPlayerRecordUw2.SetXClock(lOFile, UWPlayerRecordUw2.DjinnClock, 5);
            UWPlayerRecordUw2.SetXClock(lOFile, UWTrapRulesUw2.CastleXClock, 0xB);
            UWDjinnRulesUw2.BottleBroken(UWDjinnRulesUw2.BottleId, lOFile, 69, 21, 53);

            UWObject lOBottle = lOImport.Levels[68].Masterlist.Find(o => o != null && o.ID == UWDjinnRulesUw2.BottleId && o.TileX == 3 && o.TileY == 20);
            bool lbBreaks = lOImport.CommonObjectProperties.TryGet(UWDjinnRulesUw2.BottleId, out UWCommonObjectProperties.Entry lOBottleEntry)
                && lOBottleEntry.QualityClass != UWObjectDamageRules.ImmuneQualityClass;

            Check.Str("uw2 djinn: the bottle before 5 rends, from 5 elsewhere wasted, another thing nothing; at the sigil 6, castle E, quest bit; castle B stays; level 69 3/20 holds one, breakable",
                leEarly + " " + leElsewhere + " " + leOther + " " + liAfterMiss + " " + leSigil + " " + lsAbsorbed + " "
                + UWPlayerRecordUw2.XClock(lOFile, UWTrapRulesUw2.CastleXClock) + " " + (lOBottle != null) + " " + lbBreaks,
                "Rends Wasted NotTheBottle 5 Absorbed 6 14 1 11 True True");
        }

        /// <summary>
        /// G4 PART A (2026-11-13): UseRect on the wall's controls - a switch of the game's own levels flips, the lever and
        /// the switch picture step their flags - and the second game's locks: UseLockpickOn's Picklock + 1, the fumble that
        /// breaks the pick unless Dexterity saves it, "failed" for no Picklock on an unlocked lock, and UseKeyOn's "already
        /// open" on an open chest.
        /// </summary>
        private static void fCheckUw2Use(string psData)
        {
            DataImport lOImport = new DataImport(psData, null, null, UWGameProfiles.Uw2);

            UWSaveCodec.StartNewCharacterUw2(lOImport.Session, new UWPlayerData.NewCharacter { Name = "Selfcheck" });

            UWLevel lOLevel = lOImport.Levels[0];
            FakeTrapHost lOHost = fUw2Host(lOImport, 0);
            UWObject lOSwitch = null;

            for (int liY = 0; liY < 64 && lOSwitch == null; liY++)
            {
                for (int liX = 0; liX < 64 && lOSwitch == null; liX++)
                {
                    UWTile lOTile = lOLevel.GetTile(liX, liY);

                    if (lOTile != null && lOTile.ObjectsInTile != null)
                        lOSwitch = lOTile.ObjectsInTile.Find(o => o != null && (o.ID & 0x1F0) == 0x170);
                }
            }

            int liBefore = lOSwitch != null ? lOSwitch.ID : -1;
            bool lbUsed = UWUseRulesUw2.UseWallControl(lOSwitch, lOHost);
            UWObject lOLever = new UWObject((ushort)UWUseRulesUw2.LeverId) { Flags = 7 };
            UWObject lOPicture = new UWObject((ushort)UWUseRulesUw2.SwitchPictureId) { Flags = 2 };

            UWUseRulesUw2.UseWallControl(lOLever, lOHost);
            UWUseRulesUw2.UseWallControl(lOPicture, lOHost);

            Check.Str("uw2 use: a switch of level 1 flips (index ^ 8); the lever 7 -> 0, the switch picture 2 -> 3; a chest is no control",
                (lOSwitch != null) + " " + lbUsed + " " + (lOSwitch != null && lOSwitch.ID == (liBefore ^ 8)) + " " + lOLever.Flags + " " + lOPicture.Flags
                + " " + UWUseRulesUw2.UseWallControl(new UWObject(0x15D), lOHost),
                "True True True 0 3 False");

            // The locks: a chest with a lock of key 7 in its chain.
            UWObject lOChest = new UWObject((ushort)UWObjectMechanics.ChestObjectId);
            lOChest.EnsureContentsLoaded(null);

            System.Func<int, int, bool, UWObject> lFLock = (piZ, piFlags, pbAdd) =>
            {
                lOChest.Contents.Clear();

                UWObject lOLock = new UWObject((ushort)UWObjectMechanics.LockObjectId) { Quantity = 7, Flags = (ushort)piFlags, ZPos = piZ };

                if (pbAdd)
                    lOChest.Contents.Add(lOLock);

                return lOLock;
            };

            UWObject lOLocked = lFLock(2, 3, true);

            UWRandom.Use(new FixedRandomSource(false));
            UWLockRules.PickResult lOFumble = UWLockRules.PickLockSecondGame(null, lOChest, lOLocked, 0, 10);
            UWRandom.Use(new FixedRandomSource(true));
            UWLockRules.PickResult lOPicked = UWLockRules.PickLockSecondGame(null, lOChest, lOLocked, 30, 10);

            UWObject lOHard = lFLock(0xE, 3, true);
            UWLockRules.PickResult lOHard30 = UWLockRules.PickLockSecondGame(null, lOChest, lOHard, 30, 10);
            UWLockRules.PickResult lOHard31 = UWLockRules.PickLockSecondGame(null, lOChest, lOHard, 31, 10);
            UWRandom.Use(null);

            Check.Str("uw2 locks: the lowest roll fumbles and breaks the pick (0x86), the highest picks (0x87); a hard lock wants Picklock 31",
                lOFumble.Broke + " " + lOFumble.MessageIndex + " " + lOPicked.Unlocked + " " + lOPicked.MessageIndex + " "
                + lOHard30.Unlocked + " " + lOHard31.Unlocked,
                "True " + UWLockRules.PickBrokeStringUw2 + " True " + UWLockRules.PickSucceededStringUw2 + " False True");

            // A fumble with the dexterity roll on the highest number keeps the pick: a lock of 15 always fails its pick,
            // the roll of 0 skill is a critical failure only on the lowest number - so the same lowest source for both.
            UWObject lOUnpickable = lFLock(0xF, 1, true);

            UWRandom.Use(new FixedRandomSource(false));
            UWLockRules.PickResult lOClumsy = UWLockRules.PickLockSecondGame(null, lOChest, lOUnpickable, 0, 0);
            UWLockRules.PickResult lOAgile = UWLockRules.PickLockSecondGame(null, lOChest, lOUnpickable, 0, 60);
            UWRandom.Use(null);

            UWObject lOOpenLock = lFLock(2, 2, true);
            UWLockRules.PickResult lONoSkill = UWLockRules.PickLockSecondGame(null, lOChest, lOOpenLock, 0, 10);
            UWLockRules.PickResult lOSkill = UWLockRules.PickLockSecondGame(null, lOChest, lOOpenLock, 5, 10);
            UWLockRules.PickResult lONone = UWLockRules.PickLockSecondGame(null, lOChest, null, 5, 10);

            Check.Str("uw2 locks: on a fumble Dexterity 60 saves the pick (failed), 0 not; no Picklock on an unlocked lock fails, with it 'not locked'; no lock",
                lOClumsy.Broke + " " + lOAgile.Broke + " " + lOAgile.MessageIndex + " " + lONoSkill.MessageIndex + " " + lOSkill.MessageIndex + " " + lONone.MessageIndex,
                "True False " + UWLockRules.PickFailedStringUw2 + " " + UWLockRules.PickFailedStringUw2 + " " + UWLockRules.NotLockedStringUw2 + " " + UWLockRules.NoLockStringUw2);

            // THE LOCK IN THE CHAIN (per user 2026-11-13): level 3's portcullis at 45/56 holds a door trap first and its lock
            // behind it; it reads locked. Across all levels, the doors whose lock is not their first object.
            UWObject lOPortcullis = fFindTrigger(lOImport.Levels[2], 45, 56, 0x146);
            UWLockState lOPortLock = new UWLockState();

            lOPortLock.EnsureResolved(lOPortcullis, lOImport.Levels[2].Masterlist);

            Check.Str("uw2 locks: level 3's portcullis at 45/56 (a door trap before its lock) is locked; doors with the lock not first, all levels",
                lOPortLock.HasLock + " " + lOPortLock.IsLocked + " " + fDoorsWithLockBehind(lOImport),
                "True True 16");

            UWObject lOKey = new UWObject(0x100) { Owner = 7 };
            UWObject lOKeyLock = lFLock(2, 1, true);
            int liUnlock = UWLockRules.ToggleWithKey(null, lOChest, lOKeyLock, UWLockRules.IsOpenForKey(lOChest), lOKey);
            bool lbGone = !lOChest.Contents.Contains(lOKeyLock);
            // An open container of class 0x80 (odd, below index 0xC); the chest 0x15D is furniture, not a container.
            UWObject lOOpenChest = new UWObject(0x83);
            UWObject lOKept = lFLock(2, 2, true);
            int liOpen = UWLockRules.ToggleWithKey(null, lOChest, lOKept, UWLockRules.IsOpenForKey(lOOpenChest), lOKey);

            Check.Str("uw2 locks: the fitting key unlocks (line 6) and the lock goes; on an open container 'That is already open.' (7); open doors 0x148, not 0x140",
                liUnlock + " " + lbGone + " " + liOpen + " " + UWLockRules.IsOpenForKey(new UWObject(0x148)) + " " + UWLockRules.IsOpenForKey(new UWObject(0x140)),
                "6 True 7 True False");
        }

        /// <summary>
        /// G3 PART H6c on a fresh world: bottles turned coins, a cure potion spoiled into a poison trap, Bliy Skup's crystals
        /// carried to the middle, and the q*bert pyramid of level 69 - a colour learnt sends the player to it, a plate takes
        /// the sequence's colours in turn.
        /// </summary>
        private static void fCheckUw2LastHacks(string psData)
        {
            DataImport lOImport = new DataImport(psData, null, null, UWGameProfiles.Uw2);

            UWSaveCodec.StartNewCharacterUw2(lOImport.Session, new UWPlayerData.NewCharacter { Name = "Selfcheck" });

            UWPlayerFile lOFile = lOImport.Session.PlayerFileUw2;
            UWLevel lOLevel52 = lOImport.Levels[51];
            FakeTrapHost lOTombs = fUw2Host(lOImport, 51);
            UWObject lOAim = new UWObject(0x1A0) { Quality = 30, Owner = 47 };
            UWObject lOBottle = new UWObject((ushort)UWTrapRulesUw2.BottleId) { TileX = 30, TileY = 47 };
            UWObject lOPotion = new UWObject((ushort)UWTrapRulesUw2.RedPotionId) { TileX = 30, TileY = 47, HasQuantity = true, Quantity = 0x200 | 0x1F, Flags = 4 };

            lOLevel52.GetTile(30, 47).ObjectsInTile.Add(lOBottle);
            lOLevel52.GetTile(30, 47).ObjectsInTile.Add(lOPotion);
            lOTombs.World.Session.Game.DoActions.Run(new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 33 }, lOAim, null, lOTombs);
            lOTombs.World.Session.Game.DoActions.Run(new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 38 }, lOAim, null, lOTombs);

            UWObject lOPoison = lOPotion.Contents != null && lOPotion.Contents.Count == 1 ? lOPotion.Contents[0] : null;

            Check.Str("uw2 last hacks: a bottle becomes a coin (33); a red cure potion a green one holding a poison trap of 1 to 8 (38)",
                "0x" + lOBottle.ID.ToString("X") + " 0x" + lOPotion.ID.ToString("X") + " " + lOPotion.HasQuantity + " "
                + (lOPoison != null && lOPoison.ID == UWObjectMechanics.DamageTrapId && lOPoison.Owner == 1 && lOPoison.Quality >= 1 && lOPoison.Quality <= 8),
                "0xA0 0xE4 False True");

            // Bliy Skup's chamber (level 33): the thing 0xAE on 58/4, crystals of quality 2 on 57/4 and 59/4, his death set.
            UWLevel lOLevel33 = lOImport.Levels[32];
            FakeTrapHost lOTalorus = fUw2Host(lOImport, 32);

            lOTalorus.Fake.RemovesFromTiles = true;
            lOLevel33.GetTile(0x3A, 4).ObjectsInTile.Add(new UWObject(0xAE) { TileX = 0x3A, TileY = 4 });
            lOLevel33.GetTile(0x39, 4).ObjectsInTile.Add(new UWObject(0xA1) { TileX = 0x39, TileY = 4, Quality = 2 });
            lOLevel33.GetTile(0x3B, 4).ObjectsInTile.Add(new UWObject(0xA1) { TileX = 0x3B, TileY = 4, Quality = 6 });

            lOTalorus.World.Session.Game.DoActions.Run(new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 25 }, null, null, lOTalorus);
            string lsAlive = lOTalorus.Fake.Spawned.Count.ToString();

            UWPlayerRecordUw2.SetQuest(lOFile, UWTrapRulesUw2.BliySkupQuest, 1);
            lOTalorus.World.Session.Game.DoActions.Run(new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 25 }, null, null, lOTalorus);

            Check.Str("uw2 last hacks: Bliy Skup alive nothing; dead, both crystals go to 58/3 and the quest flag clears",
                lsAlive + " " + string.Join("|", lOTalorus.Fake.Log.FindAll(l => l.StartsWith("spawn 161"))) + " "
                + UWPlayerRecordUw2.GetQuest(lOFile, UWTrapRulesUw2.BliySkupQuest),
                "0 spawn 161 on 58/3|spawn 161 on 58/3 0");

            // The q*bert pyramid (level 69): colour 3 learnt -> to 49/51 of level 69 falling; the pyramid's squares start at 6,
            // the sequence's first colour, so a plate on 47/53 steps it to the next learnt one (3), and after another square
            // and back round to 6.
            UWLevel lOLevel69 = lOImport.Levels[68];
            FakeTrapHost lOVoid = fUw2Host(lOImport, 68);

            lOVoid.World.Session.Game.DoActions.Run(new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 32, Owner = 3 }, null, null, lOVoid);

            UWTile lOFirst = lOLevel69.GetTile(47, 53);
            UWTile lOSecond = lOLevel69.GetTile(48, 53);
            int liBefore = UWTrapRulesUw2.FloorSlot(lOFirst);

            fQbertPress(lOVoid, lOFirst);

            int liFirstColour = UWTrapRulesUw2.FloorSlot(lOFirst);

            fQbertPress(lOVoid, lOSecond);
            fQbertPress(lOVoid, lOFirst);

            Check.Str("uw2 last hacks: q*bert - colour 3 learnt sends the player to the pyramid; a plate on a square of 6 steps it to 3, then round to 6",
                string.Join("|", lOVoid.Fake.Log.FindAll(l => l.StartsWith("teleport2"))) + " " + liBefore + " "
                + liFirstColour + " " + UWTrapRulesUw2.FloorSlot(lOFirst),
                "teleport2 69 49/51 falls 6 3 6");
        }

        /// <summary>How many doors of all levels hold their lock behind another object (checkLock finds it in the chain).</summary>
        private static int fDoorsWithLockBehind(DataImport pOImport)
        {
            int liCount = 0;

            foreach (UWLevel lOLevel in pOImport.Levels)
            {
                if (lOLevel == null || lOLevel.Masterlist == null)
                    continue;

                foreach (UWObject lODoor in UWDoorRules.FindDoorsWithLock(lOLevel))
                {
                    if (lODoor.Contents[0] == null || lODoor.Contents[0].ID != UWObjectMechanics.LockObjectId)
                        liCount++;
                }
            }

            return liCount;
        }

        /// <summary>A q*bert plate pressed on the square (do_qbert with owner 0x3F through a plate's host).</summary>
        private static void fQbertPress(FakeTrapHost pOHost, UWTile pOTile)
        {
            UWTrapHostAsPlayer lOPlate = new UWTrapHostAsPlayer(pOHost) { PlateTile = pOTile };

            pOHost.World.Session.Game.DoActions.Run(new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 32, Owner = 0x3F }, null, null, lOPlate);
        }

        /// <summary>
        /// G3 PART H6b: the blackrock gem's facets and draw, its travel from level 5's move trigger at 28/39 (the facet
        /// shown takes the player to its world, another bounces him back), a standing wave and a flipped switch on made-up
        /// traps.
        /// </summary>
        private static void fCheckUw2WorldHacks(string psData)
        {
            Check.Str("uw2 world hacks: the gem's facets around 0xE3/0x143 (south 0 on the axis and 1 beside, west 6 and 7, north 3 on the axis and 2 beside)",
                UWTrapRulesUw2.GemFacet(0xE3, 0x14B) + " " + UWTrapRulesUw2.GemFacet(0xEB, 0x144) + " " + UWTrapRulesUw2.GemFacet(0xDB, 0x144) + " "
                + UWTrapRulesUw2.GemFacet(0xE3 - 1, 0x14B) + " " + UWTrapRulesUw2.GemFacet(0xE3, 0x13B) + " " + UWTrapRulesUw2.GemFacet(0xEB, 0x142),
                "0 1 6 7 3 2");
            Check.Str("uw2 world hacks: the gem's draw early in the plot (one facet): facet 0, or 8 when 0 is shown already",
                UWTrapRulesUw2.GemRotate(1, 0, 0) + " " + UWTrapRulesUw2.GemRotate(0, 0, 0), "0 8");

            DataImport lOImport = new DataImport(psData, null, null, UWGameProfiles.Uw2);

            UWSaveCodec.StartNewCharacterUw2(lOImport.Session, new UWPlayerData.NewCharacter { Name = "Selfcheck" });

            UWPlayerFile lOFile = lOImport.Session.PlayerFileUw2;
            UWLevel lOLevel5 = lOImport.Levels[4];
            FakeTrapHost lOFive = fUw2Host(lOImport, 4);
            UWObject lOGem = fFindTrigger(lOLevel5, 28, 39, 0x1A0);

            lOFive.Fake.Body.Floor = UWSpot.FromEighths(0xE3, 0x14B, 0);
            UWTrapRulesUw2.SetNumbered(lOFile, UWTrapRulesUw2.GemFacetVariable, 2, 3);
            UWTrapRulesUw2.UseTrigger(null, lOGem, UWTrapRulesUw2.ChainMode, lOFive);

            bool lbBounced = lOFive.Fake.Shown.Log.Contains("general " + UWTrapRulesUw2.GemOpaqueString) && !lOFive.Fake.Log.Exists(l => l.StartsWith("teleport2"));

            UWTrapRulesUw2.SetNumbered(lOFile, UWTrapRulesUw2.GemFacetVariable, 2, 0);
            UWTrapRulesUw2.UseTrigger(null, lOGem, UWTrapRulesUw2.ChainMode, lOFive);

            Check.Str("uw2 world hacks: level 5's gem - at facet 0 with facet 3 shown bounced back; with 0 shown to level 9 at 33/32, world 0 visited",
                lbBounced + " " + string.Join("|", lOFive.Fake.Log.FindAll(l => l.StartsWith("teleport2"))) + " "
                + (UWPlayerRecordUw2.GetNumbered(lOFile, UWTrapRulesUw2.FirstQuestByte + UWTrapRulesUw2.WorldsVisitedQuestByte) & 1),
                "True teleport2 9 33/32 1");

            // A standing wave from 30/30 of level 1: phase 2 lifts the square two south by 2 over the target's height.
            UWLevel lOLevel1 = lOImport.Levels[0];
            FakeTrapHost lOOne = fUw2Host(lOImport, 0);
            int liBase = lOLevel1.GetTile(30, 30).FloorHeight >> 4;
            UWObject lOWave = new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 12, Owner = 2 };

            lOOne.World.Session.Game.DoActions.Run(lOWave, new UWObject(0x1A0) { Quality = 30, Owner = 30 }, null, lOOne);

            // A switch put on 31/31: state 1 flips a first-state switch, then not again; state 4 flips it back.
            UWObject lOSwitch = new UWObject(0x170) { TileX = 31, TileY = 31 };
            bool lbFirst = UWTrapRulesUw2.FlipSwitch(lOSwitch, 1, lOOne);
            bool lbAgain = UWTrapRulesUw2.FlipSwitch(lOSwitch, 1, lOOne);
            int liFlipped = lOSwitch.ID;
            bool lbBack = UWTrapRulesUw2.FlipSwitch(lOSwitch, 4, lOOne);

            Check.Str("uw2 world hacks: a standing wave at phase 2 (two south +2, the phase now 3); flip_switch 1, 1 again, then 4",
                ((lOLevel1.GetTile(30, 28).FloorHeight >> 4) - liBase) + " " + lOWave.Owner + " | " + lbFirst + " " + lbAgain + " 0x"
                + liFlipped.ToString("X") + " " + lbBack + " 0x" + lOSwitch.ID.ToString("X"),
                "2 3 | True False 0x178 True 0x170");
        }

        /// <summary>
        /// G3 PART H6 on a fresh world: level 3's look trigger at 44/55 brings out a hidden switch (hack 39), level 1's
        /// scheduled trigger at 25/40 sets a wall picture's owner (hack 28), and the toggle of an object's height (21/22)
        /// and the texture cycle (14) on made-up traps.
        /// </summary>
        private static void fCheckUw2Hacks(string psData)
        {
            DataImport lOImport = new DataImport(psData, null, null, UWGameProfiles.Uw2);

            UWSaveCodec.StartNewCharacterUw2(lOImport.Session, new UWPlayerData.NewCharacter { Name = "Selfcheck" });

            // Level 3, 44/55: look trigger -> text -> hack 39 owner 0 on a switch.
            UWLevel lOLevel3 = lOImport.Levels[2];
            FakeTrapHost lOThree = fUw2Host(lOImport, 2);
            UWObject lOLook = fFindTriggerDeep(lOLevel3, 44, 55, 0x1A3);
            UWObject lOText = UWObjectMechanics.GetLinkedObject(lOLook, lOLevel3.Masterlist);
            UWObject lOHack = UWObjectMechanics.GetLinkedObject(lOText, lOLevel3.Masterlist);
            UWObject lOSwitch = UWObjectMechanics.GetLinkedObject(lOHack, lOLevel3.Masterlist);
            bool lbHiddenBefore = lOSwitch.IsHidden;

            UWTrapRulesUw2.UseTrigger(null, lOLook, UWTrapRulesUw2.ChainMode, lOThree);

            Check.Str("uw2 hacks: level 3's look trigger at 44/55 speaks and shows the hidden switch (hack 39)",
                "0x" + lOHack.ID.ToString("X") + " q" + lOHack.Quality + " " + lbHiddenBefore + " " + lOSwitch.IsHidden + " "
                + lOThree.Fake.Shown.Log.Exists(l => l.StartsWith("message ")),
                "0x183 q39 True False True");

            // Level 1, 25/40: a scheduled trigger -> hack 28 owner 44 on a wall picture (special tmap obj).
            UWLevel lOLevel1 = lOImport.Levels[0];
            FakeTrapHost lOOne = fUw2Host(lOImport, 0);
            UWObject lOScheduled = null;

            foreach (UWObject lOAt in lOLevel1.GetTile(25, 40).ObjectsInTile)
            {
                UWObject lOTrap = lOAt != null && lOAt.ID == 0x1AC ? UWObjectMechanics.GetLinkedObject(lOAt, lOLevel1.Masterlist) : null;

                if (lOTrap != null && lOTrap.ID == UWObjectMechanics.DoTrapId && lOTrap.Quality == 28)
                    lOScheduled = lOAt;
            }

            UWObject lOPicture = lOScheduled != null
                ? UWObjectMechanics.GetLinkedObject(UWObjectMechanics.GetLinkedObject(lOScheduled, lOLevel1.Masterlist), lOLevel1.Masterlist) : null;

            if (lOScheduled != null)
                UWTrapRulesUw2.UseTrigger(null, lOScheduled, UWTrapRulesUw2.ChainMode, lOOne);

            Check.Str("uw2 hacks: level 1's scheduled trigger at 25/40 sets the wall picture's owner to 44 (hack 28)",
                (lOPicture != null ? "0x" + lOPicture.ID.ToString("X") + " " + lOPicture.Owner : "none"), "0x16E 44");

            // Made-up traps on level 1's lists: hack 22 lifts an object at or below the trap's height by owner, hack 14 steps a floor.
            UWObject lOThing = new UWObject(0x95) { ZPos = 10 };
            int liIndex = 1000;
            UWObject lOWas = lOLevel1.Masterlist[liIndex];

            lOLevel1.Masterlist[liIndex] = lOThing;

            UWObject lORaise = new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 22, Owner = 5, ZPos = 20, Quantity = (ushort)liIndex };

            lOOne.World.Session.Game.DoActions.Run(lORaise, null, null, lOOne);
            int liFirst = lOThing.ZPos;

            lOOne.World.Session.Game.DoActions.Run(lORaise, null, null, lOOne);
            lOLevel1.Masterlist[liIndex] = lOWas;

            UWTile lOCycle = lOLevel1.GetTile(30, 30);
            int liFloor = UWTrapRulesUw2.FloorSlot(lOCycle);
            UWObject lOCycleTrap = new UWObject((ushort)UWObjectMechanics.DoTrapId) { Quality = 14, Owner = (ushort)liFloor, ZPos = liFloor + 1 };
            UWObject lOAim = new UWObject(0x1A0) { Quality = 30, Owner = 30 };

            lOOne.World.Session.Game.DoActions.Run(lOCycleTrap, lOAim, null, lOOne);
            int liStepped = UWTrapRulesUw2.FloorSlot(lOCycle);

            lOOne.World.Session.Game.DoActions.Run(lOCycleTrap, lOAim, null, lOOne);

            Check.Str("uw2 hacks: hack 22 takes a low object to z + owner (25), then, above, to z (20); hack 14 steps a floor on and round",
                liFirst + " " + lOThing.ZPos + " " + (liStepped == liFloor + 1) + " " + (UWTrapRulesUw2.FloorSlot(lOCycle) == liFloor),
                "25 20 True True");
        }

        /// <summary>
        /// G3 PART H5 on a fresh world: the random castle monster's draw, and the wandering monsters - every create-object
        /// trap without flags whose template is a mobile record (DoWanderingMonsters) fires at its square, the template and
        /// the copy temporary; an adventurer template makes a castle monster, hostile, the template an adventurer again.
        /// </summary>
        private static void fCheckUw2Wandering(string psData)
        {
            DataImport lOImport = new DataImport(psData, null, null, UWGameProfiles.Uw2);

            UWSaveCodec.StartNewCharacterUw2(lOImport.Session, new UWPlayerData.NewCharacter { Name = "Selfcheck" });

            bool lbDrawsFit = true;

            for (int liDraw = 0; liDraw < 50; liDraw++)
            {
                int liKind = UWTrapRulesUw2.RollCastleMonster(1, 0, 1, lOImport.ObjectClassProperties);

                lbDrawsFit &= liKind == -1 || liKind == 0x40 || liKind == 0x41;
            }

            int liLater = UWTrapRulesUw2.RollCastleMonster(5, 8, 10, lOImport.ObjectClassProperties);

            Check.Str("uw2 wandering: castle monsters on level 1 at plot 0 and character level 1 are 0x40 or 0x41; on level 5 at plot 8 a creature",
                lbDrawsFit + " " + (liLater >= 0x40 && liLater < 0x80), "True True");

            string lsPlain = null;
            string lsCastle = null;

            for (int liLevel = 0; liLevel < lOImport.Levels.Count && (lsPlain == null || lsCastle == null); liLevel++)
            {
                UWLevel lOLevel = lOImport.Levels[liLevel];

                if (lOLevel == null || lOLevel.TileData == null)
                    continue;

                foreach (UWTile lOTile in lOLevel.TileData)
                {
                    foreach (UWObject lOTrap in new System.Collections.Generic.List<UWObject>(lOTile.ObjectsInTile))
                    {
                        if (lOTrap == null || lOTrap.ID != UWObjectMechanics.CreateObjectTrapId || (lOTrap.Flags & 0xF) != 0
                            || lOTrap.Quantity <= 0 || lOTrap.Quantity >= UWLevArkLayout.MobileCount)
                            continue;

                        UWObject lOTemplate = UWObjectMechanics.GetLinkedObject(lOTrap, lOLevel.Masterlist);
                        bool lbAdventurer = lOTemplate != null && lOTemplate.ID == UWTrapRulesUw2.AdventurerTemplateId;

                        if (lOTemplate == null || (lbAdventurer ? lsCastle != null : lsPlain != null))
                            continue;

                        FakeTrapHost lOHost = fUw2Host(lOImport, liLevel);

                        lOHost.AlwaysFireCreateObjectTraps = true;

                        bool lbFired = UWTrapRulesUw2.FireWanderingTrap(lOTrap, lOTile.X, lOTile.Z, lOHost);
                        UWObject lOCopy = lOHost.Fake.Spawned.Count > 0 ? lOHost.Fake.Spawned[0] : null;
                        string lsResult = lbFired + " " + ((UWNpc)lOTemplate).NPCSpawned + " " + (lOCopy is UWNpc lOCopyNpc && lOCopyNpc.NPCSpawned);

                        if (lbAdventurer)
                            lsCastle = lsResult + " " + (lOTemplate.ID == UWTrapRulesUw2.AdventurerTemplateId) + " "
                                + (lOCopy != null && lOCopy.ID >= 0x40 && lOCopy.ID < 0x7F) + " "
                                + (lOCopy is UWNpc lOHostile && lOHostile.NPCAttitude == UWNpc.AttitudeHostile);
                        else
                            lsPlain = lsResult + " " + (lOCopy != null && lOCopy.ID == lOTemplate.ID);
                    }
                }
            }

            Check.Str("uw2 wandering: a plain wandering trap fires, its template and the copy temporary, the copy its kind",
                lsPlain ?? "none in the levels", "True True True True");
            Check.Str("uw2 wandering: an adventurer template makes a hostile temporary castle monster, the template an adventurer again",
                lsCastle ?? "none in the levels", "True True True True True True");
        }

        /// <summary>
        /// G3 PART H4 on a fresh world: an enter trigger (level 2, 35/30) going off when the player steps onto its square, a
        /// plate (level 26, 58/15) pressed by a heavy thing put on it and flipping its state, and level 3's timer at 8/21
        /// stepping its oscillator while the player is near and not when far.
        /// </summary>
        private static void fCheckUw2TriggerSites(string psData)
        {
            DataImport lOImport = new DataImport(psData, null, null, UWGameProfiles.Uw2);

            UWSaveCodec.StartNewCharacterUw2(lOImport.Session, new UWPlayerData.NewCharacter { Name = "Selfcheck" });

            UWPlayerFile lOFile = lOImport.Session.PlayerFileUw2;

            // Level 2, 35/30: the enter trigger sets variable 52 to 2 (set trap q0 o2 z52 h2).
            FakeTrapHost lOTwo = fUw2Host(lOImport, 1);
            UWLevel lOLevel2 = lOImport.Levels[1];
            int liBefore = UWPlayerRecordUw2.GetNumbered(lOFile, 52);

            lOTwo.Fake.Body.Feet = new UWSpot(new UWTilePos(35, 30), 3, 3, lOLevel2.GetTile(35, 30).FloorHeight >> 1);
            UWTrapRulesUw2.MoverMoved(lOLevel2.GetTile(34, 30), 0, lOLevel2.GetTile(35, 30), lOLevel2.GetTile(35, 30).FloorHeight >> 1, lOTwo);

            Check.Str("uw2 sites: stepping onto level 2's 35/30 sets off its enter trigger (variable 52 from 0 to 2)",
                liBefore + " " + UWPlayerRecordUw2.GetNumbered(lOFile, 52), "0 2");

            // Level 25 (per user 2026-11-22, SAVE3): the drain at 28/30 sends the player to 28/34, whose enter trigger sends
            // whoever STEPS onto it back to 28/29. Put there by the teleport (player_setup) only 28/30's exit plates go off.
            FakeTrapHost lODrainStep = fUw2Host(lOImport, 24);
            UWLevel lOLevel25 = lOImport.Levels[24];

            lODrainStep.Fake.Body.Feet = new UWSpot(new UWTilePos(28, 34), 3, 3, lOLevel25.GetTile(28, 34).FloorHeight >> 1);
            UWTrapRulesUw2.MoverMoved(lOLevel25.GetTile(28, 33), 0, lOLevel25.GetTile(28, 34), lOLevel25.GetTile(28, 34).FloorHeight >> 1, lODrainStep);

            FakeTrapHost lODrainArrive = fUw2Host(lOImport, 24);

            lODrainArrive.Fake.Body.Feet = new UWSpot(new UWTilePos(28, 34), 3, 3, lOLevel25.GetTile(28, 34).FloorHeight >> 1);
            UWTrapRulesUw2.PlayerSetUp(lOLevel25.GetTile(28, 30), lOLevel25.GetTile(28, 30).FloorHeight >> 1, lODrainArrive);

            Check.Str("uw2 sites: level 25's 28/34 sends back who steps onto it, not who is teleported there",
                string.Join(",", lODrainStep.Fake.Log.FindAll(s => s.StartsWith("teleport2"))) + " | "
                + string.Join(",", lODrainArrive.Fake.Log.FindAll(s => s.StartsWith("teleport2"))), "teleport2 0 28/29 | ");

            // Level 3, 15/53: the enter trigger sits behind a move trigger, so the original never asks it (check_pplate); with
            // the fix (deviations.everyEnterExitTrigger) it sets variable 52 to 1, Miranda's meeting place.
            FakeTrapHost lOThree = fUw2Host(lOImport, 2);
            UWLevel lOLevel3Enter = lOImport.Levels[2];
            UWRulesDeviations lODeviations = lOImport.Session.Rules.Deviations;
            bool lbFixBefore = lODeviations.EveryEnterExitTrigger;
            string lsEnter = (lOThree.World.Session == lOImport.Session).ToString();

            foreach (bool lbFix in new[] { false, true })
            {
                lODeviations.EveryEnterExitTrigger = lbFix;
                UWPlayerRecordUw2.SetNumbered(lOFile, 52, 0);
                lOThree.Fake.Body.Feet = new UWSpot(new UWTilePos(15, 53), 3, 3, lOLevel3Enter.GetTile(15, 53).FloorHeight >> 1);
                UWTrapRulesUw2.MoverMoved(lOLevel3Enter.GetTile(14, 53), 0, lOLevel3Enter.GetTile(15, 53), lOLevel3Enter.GetTile(15, 53).FloorHeight >> 1, lOThree);
                lsEnter += " " + UWPlayerRecordUw2.GetNumbered(lOFile, 52);
            }

            lODeviations.EveryEnterExitTrigger = lbFixBefore;

            Check.Str("uw2 sites: level 3's enter trigger at 15/53 behind a move trigger - dead as in the original, with the fix it sets variable 52 to 1",
                lsEnter, "True 0 1");

            // Level 26, 58/15: a heavy plate (fine y bit 2 set: more than 84) at its height; a thing heavier than that put on it
            // presses it - its chain runs as by the player and the plate's state flips to pressed.
            FakeTrapHost lOIce = fUw2Host(lOImport, 25);
            UWLevel lOLevel26 = lOImport.Levels[25];
            UWTile lOPlateTile = lOLevel26.GetTile(58, 15);
            UWObject lOPlate = fFindTrigger(lOLevel26, 58, 15, 0x1A4);
            int liHeavy = fFirstHeavyId(lOImport.CommonObjectProperties, (lOPlate.YPos & 4) != 0 ? UWTrapRulesUw2.HeavyPlate : UWTrapRulesUw2.LightPlate);
            int liPressedBefore = lOPlate.YPos & 1;
            bool lbNothingYet = !UWTrapRulesUw2.CheckPlates(lOPlateTile, lOPlate.ZPos, UWTrapRulesUw2.PressureMode, lOIce) || (lOPlate.YPos & 1) == liPressedBefore;
            int liLogBefore = UWTrapLog.Entries.Count;

            lOPlateTile.ObjectsInTile.Add(new UWObject((ushort)liHeavy) { ZPos = lOPlate.ZPos, TileX = 58, TileY = 15 });
            UWTrapRulesUw2.CheckPlates(lOPlateTile, lOPlate.ZPos, UWTrapRulesUw2.EnterMode, lOIce);

            Check.Str("uw2 sites: level 26's heavy plate at 58/15 (fine y bit 2) - empty nothing happens; a thing over 84 arriving presses it and it is pressed now",
                (liHeavy > 0) + " " + ((lOPlate.YPos & 4) != 0) + " " + liPressedBefore + " " + lbNothingYet + " " + (lOPlate.YPos & 1) + " "
                + (UWTrapRulesUw2.WeightOn(lOPlateTile, lOPlate.ZPos, lOIce) > UWTrapRulesUw2.HeavyPlate),
                "True True 0 True 1 True");

            // Taking the thing away again releases it.
            lOPlateTile.ObjectsInTile.RemoveAll(o => o.ID == liHeavy && o.TileX == 58 && o.TileY == 15 && o.ZPos == lOPlate.ZPos);
            UWTrapRulesUw2.CheckPlates(lOPlateTile, lOPlate.ZPos, UWTrapRulesUw2.ReleaseMode, lOIce);

            Check.Str("uw2 sites: ... and taking it off releases the plate", (lOPlate.YPos & 1).ToString(), "0");

            // Level 3, 8/21: the timer trigger (z 0x20 -> every 0x21 steps) steps the oscillator at 9/23 with the player near,
            // not with him far away.
            FakeTrapHost lOCastle = fUw2Host(lOImport, 2);
            UWLevel lOLevel3 = lOImport.Levels[2];
            UWObject lOTimer = fFindTriggerDeep(lOLevel3, 8, 21, 0x1A9);
            bool lbListed = lOLevel3.Timers.Contains(lOLevel3.Masterlist.IndexOf(lOTimer));
            int liStart = lOLevel3.GetTile(9, 23).FloorHeight >> 4;
            int liTick = UWTrapRulesUw2.TickTimers(0, lOTimer.ZPos + 1, new UWTilePos(50, 50), UWSpot.Nowhere.Tile, lOCastle);
            int liFar = lOLevel3.GetTile(9, 23).FloorHeight >> 4;

            UWTrapRulesUw2.TickTimers(liTick, lOTimer.ZPos + 1, new UWTilePos(9, 23), UWSpot.Nowhere.Tile, lOCastle);

            Check.Str("uw2 sites: level 3's timer at 8/21 is in the level's list; far away nothing, near it the oscillator steps",
                lbListed + " " + (liFar == liStart) + " " + ((lOLevel3.GetTile(9, 23).FloorHeight >> 4) != liStart),
                "True True True");

            // The player standing on the oscillating square goes with its floor (raise_up / put_down for ThePlayer, per user
            // 2026-11-16: a still player fell through the rising floor); three squares away nothing.
            FakeTrapHost lOStand = fUw2Host(lOImport, 2);
            int liStandBefore = lOLevel3.GetTile(9, 23).FloorHeight >> 4;
            UWSpot lOOnIt = new UWSpot(new UWTilePos(9, 23), 3, 3, liStandBefore << 3);

            lOStand.Fake.Body.Floor = lOOnIt;
            lOStand.Fake.Body.Feet = lOOnIt;
            UWTrapRulesUw2.TickTimers(0, lOTimer.ZPos + 1, new UWTilePos(9, 23), UWSpot.Nowhere.Tile, lOStand);

            int liAfter = lOLevel3.GetTile(9, 23).FloorHeight >> 4;
            string lsOnIt = string.Join(",", lOStand.Fake.Body.FloorMoves);
            FakeTrapHost lOAway = fUw2Host(lOImport, 2);
            UWSpot lOThreeOff = new UWSpot(new UWTilePos(12, 23), 3, 3, liAfter << 3);

            lOAway.Fake.Body.Floor = lOThreeOff;
            lOAway.Fake.Body.Feet = lOThreeOff;
            UWTrapRulesUw2.TickTimers(0, lOTimer.ZPos + 1, new UWTilePos(9, 23), UWSpot.Nowhere.Tile, lOAway);

            Check.Str("uw2 sites: the player on level 3's oscillating 9/23 is told the floor's move (old new zpos, no step of one); three squares off not",
                (liAfter != liStandBefore) + " " + lsOnIt.Split(',')[0] + " | " + lOAway.Fake.Body.FloorMoves.Count,
                "True " + (liStandBefore << 3) + " " + (liAfter << 3) + " False | 0");
        }

        /// <summary>The first object id whose COMOBJ mass is above piOver, or -1.</summary>
        private static int fFirstHeavyId(UWCommonObjectProperties pOCommon, int piOver)
        {
            for (int liId = 0; liId < 0x180; liId++)
            {
                if (pOCommon.TryGet(liId, out UWCommonObjectProperties.Entry lOEntry) && lOEntry.MassTenthStones > piOver)
                    return liId;
            }

            return -1;
        }

        /// <summary>
        /// G3 PART H3, the second game's own kinds: pure parts first (experience, proximity, the change fields), then on a
        /// fresh world the shipped chains of an oscillator (level 3, 8/21), a pit (3, 57/56), the bridge remover (34,
        /// 30/26), a change-from pair (5, 18/34), a sound (18, 54/50) and a jump (33, 39/13), each set off along its chain.
        /// </summary>
        private static void fCheckUw2NewKinds(string psData)
        {
            Check.Str("uw2 kinds: experience of quality 44 owner 4, 32/0, 33/9 (doubled), 0/0",
                UWTrapRulesUw2.ExperienceOf(new UWObject(0x191) { Quality = 44, Owner = 4 }) + " "
                + UWTrapRulesUw2.ExperienceOf(new UWObject(0x191) { Quality = 32, Owner = 0 }) + " "
                + UWTrapRulesUw2.ExperienceOf(new UWObject(0x191) { Quality = 33, Owner = 9 }) + " "
                + UWTrapRulesUw2.ExperienceOf(new UWObject(0x191) { Quality = 0, Owner = 0 }),
                "100 0 18 -256");

            UWObject lONear = new UWObject(0x196) { Quality = 2, Owner = 2, ZPos = 50, XPos = 1, YPos = 0 };

            Check.Str("uw2 kinds: proximity 2 by 2 from 10/10 - 11/11 above (fine x lifts the upper bound), 13/10 outside, 11/11 below, 9/10 outside, nowhere",
                UWTrapRulesUw2.ProximityHolds(lONear, 10, 10, new UWSpot(new UWTilePos(11, 11), 0, 0, 60)) + " "
                + UWTrapRulesUw2.ProximityHolds(lONear, 10, 10, new UWSpot(new UWTilePos(13, 10), 0, 0, 60)) + " "
                + UWTrapRulesUw2.ProximityHolds(lONear, 10, 10, new UWSpot(new UWTilePos(11, 11), 0, 0, 40)) + " "
                + UWTrapRulesUw2.ProximityHolds(lONear, 10, 10, new UWSpot(new UWTilePos(9, 10), 0, 0, 60)) + " "
                + UWTrapRulesUw2.ProximityHolds(lONear, 10, 10, UWSpot.Nowhere),
                "True False False False False");

            Check.Str("uw2 kinds: a change trap's fields (wall, floor = heading + zpos bit 4 / 2, type, height = zpos & 15)",
                string.Join(",", UWTrapRulesUw2.ChangeFields(new UWObject(0x194) { Quality = 48, Heading = 4, ZPos = 127, Owner = 10 })),
                "48,12,10,15");

            if (psData == null)
                return;

            DataImport lOImport = new DataImport(psData, null, null, UWGameProfiles.Uw2);

            UWSaveCodec.StartNewCharacterUw2(lOImport.Session, new UWPlayerData.NewCharacter { Name = "Selfcheck" });

            // Level 3, 8/21: a timer trigger to an oscillator raising 9/23 within 4 to 12, a step each time.
            FakeTrapHost lOCastle = fUw2Host(lOImport, 2);
            UWTile lOOsc = lOImport.Levels[2].GetTile(9, 23);
            UWObject lOOscTrigger = fFindTriggerDeep(lOImport.Levels[2], 8, 21, 0x1A9);
            int liOscBefore = lOOsc.FloorHeight >> 4;

            UWTrapRulesUw2.UseTrigger(null, lOOscTrigger, UWTrapRulesUw2.ChainMode, lOCastle);

            int liOscAfter = lOOsc.FloorHeight >> 4;

            for (int liStep = 0; liStep < 12; liStep++)
                UWTrapRulesUw2.UseTrigger(null, lOOscTrigger, UWTrapRulesUw2.ChainMode, lOCastle);

            Check.Str("uw2 kinds: level 3's oscillator at 9/23 steps the height up by one, turns at 12 and comes back down to 4 or above",
                (liOscAfter == liOscBefore + 1 || liOscBefore >= 12) + " " + ((lOOsc.FloorHeight >> 4) >= 4 && (lOOsc.FloorHeight >> 4) <= 12),
                "True True");

            // Level 3, 57/56: a pressure trigger to a pit on its own square.
            UWTile lOPit = lOImport.Levels[2].GetTile(57, 56);
            UWObject lOPitTrap = UWObjectMechanics.GetLinkedObject(fFindTriggerDeep(lOImport.Levels[2], 57, 56, 0x1B4), lOImport.Levels[2].Masterlist);
            int liPitBefore = lOPit.FloorHeight >> 4;
            int liPitExpected = liPitBefore == 0 ? lOPitTrap.XPos + ((lOPitTrap.YPos & 7) << 3) : 0;

            UWTrapRulesUw2.UseTrigger(null, fFindTriggerDeep(lOImport.Levels[2], 57, 56, 0x1B4), UWTrapRulesUw2.ChainMode, lOCastle);

            Check.Str("uw2 kinds: level 3's pit at 57/56 digs a raised square to 0 (or fills one at 0) with its floor",
                ((lOPit.FloorHeight >> 4) == liPitExpected) + " " + (UWTrapRulesUw2.FloorSlot(lOPit) == (liPitBefore == 0 ? lOPitTrap.Owner : lOPitTrap.Quality)),
                "True True");

            // Level 34: the move trigger held by the proximity trap at 30/33 lays six bridges (heading 4, z 70, flags 1)
            // from 30/32 south, the one at 30/26 takes them away again.
            FakeTrapHost lOTalorus = fUw2Host(lOImport, 33);
            UWLevel lOLevel34 = lOImport.Levels[33];

            UWTrapRulesUw2.UseTrigger(null, fFindTriggerDeep(lOLevel34, 30, 33, 0x1A0), UWTrapRulesUw2.ChainMode, lOTalorus);

            string lsLaid = string.Join(" ", System.Linq.Enumerable.Select(lOTalorus.Fake.Spawned,
                o => o.ID.ToString("X") + "@" + o.XPos + o.YPos + "h" + o.Heading + "z" + o.ZPos + "f" + o.Flags));

            foreach (string lsLine in lOTalorus.Fake.Log.FindAll(l => l.StartsWith("spawn 356 on ")))
            {
                string[] lsAt = lsLine.Substring("spawn 356 on ".Length).Split('/');
                UWObject lOBridge = lOTalorus.Fake.Spawned[lOTalorus.Fake.Log.FindAll(l => l.StartsWith("spawn 356 on ")).IndexOf(lsLine)];

                lOBridge.TileX = int.Parse(lsAt[0]);
                lOBridge.TileY = int.Parse(lsAt[1]);
                lOLevel34.GetTile(lOBridge.TileX, lOBridge.TileY).ObjectsInTile.Add(lOBridge);
            }

            int liBridgesLaid = fCountBridges(lOLevel34, 30, 27, 32, 4, 70);

            UWTrapRulesUw2.UseTrigger(null, fFindTriggerDeep(lOLevel34, 30, 26, 0x1A0), UWTrapRulesUw2.ChainMode, lOTalorus);

            Check.Str("uw2 kinds: level 34's bridge traps lay six bridges at fine 4/4 from 30/32 south, and take them away",
                lsLaid + " | " + liBridgesLaid + " " + fCountBridges(lOLevel34, 30, 27, 32, 4, 70),
                "164@44h4z70f1 164@44h4z70f1 164@44h4z70f1 164@44h4z70f1 164@44h4z70f1 164@44h4z70f1 | 6 0");

            // Level 5, 18/34: the change-from pair at 19/32 - squares of wall 0 and floor 0 take wall 48 and floor 12.
            FakeTrapHost lOFive = fUw2Host(lOImport, 4);
            UWLevel lOLevel5 = lOImport.Levels[4];
            UWObject lOFrom = UWObjectMechanics.GetLinkedObject(fFindTriggerDeep(lOLevel5, 18, 34, 0x1AC), lOLevel5.Masterlist);
            UWTile lOMatch = fFirstTile(lOLevel5, 19, 32, 7, 7, t => UWTrapRulesUw2.WallSlot(t) == 0 && UWTrapRulesUw2.FloorSlot(t) == 0);

            UWTrapRulesUw2.UseTrigger(null, fFindTriggerDeep(lOLevel5, 18, 34, 0x1AC), UWTrapRulesUw2.ChainMode, lOFive);

            Check.Str("uw2 kinds: level 5's change-from at 19/32 turns a square of wall 0, floor 0 into wall 48, floor 12",
                "0x" + lOFrom.ID.ToString("X") + " " + (lOMatch != null) + " "
                + (lOMatch != null ? UWTrapRulesUw2.WallSlot(lOMatch) + "," + UWTrapRulesUw2.FloorSlot(lOMatch) : "-"),
                "0x193 True 48,12");

            // Level 18, 54/50: an enter trigger to a special effect of type 2 - sound 0x64 + 1. Level 33, 39/13: a jump.
            FakeTrapHost lOEighteen = fUw2Host(lOImport, 17);

            UWTrapRulesUw2.UseTrigger(null, fFindTriggerDeep(lOImport.Levels[17], 54, 50, 0x1A6), UWTrapRulesUw2.ChainMode, lOEighteen);

            FakeTrapHost lOThirtyThree = fUw2Host(lOImport, 32);

            UWTrapRulesUw2.UseTrigger(null, fFindTrigger(lOImport.Levels[32], 39, 13, 0x1A0), UWTrapRulesUw2.ChainMode, lOThirtyThree);

            Check.Str("uw2 kinds: level 18's effect plays sound 0x65; level 33's jump trap jumps the player (force 24, no stop, from speed 3 * 0x2F)",
                lOEighteen.Fake.Shown.Log.Contains("sound " + 0x65) + " " + string.Join("|", lOThirtyThree.Fake.Body.Jumps),
                "True 24 False 3");
        }

        /// <summary>A trap host on the second game's level piIndex with the check's session.</summary>
        private static FakeTrapHost fUw2Host(DataImport pOImport, int piIndex)
        {
            FakeTrapHost lOHost = new FakeTrapHost(pOImport, piIndex);

            lOHost.Fake.SessionOf = pOImport.Session;

            return lOHost;
        }

        /// <summary>Bridges of that heading and height on piX, piFromY..piToY.</summary>
        private static int fCountBridges(UWLevel pOLevel, int piX, int piFromY, int piToY, int piHeading, int piZ)
        {
            int liCount = 0;

            for (int liY = piFromY; liY <= piToY; liY++)
                liCount += pOLevel.GetTile(piX, liY).ObjectsInTile.FindAll(o => o != null && o.ID == UWTrapRulesUw2.BridgeId
                    && o.Heading == piHeading && o.ZPos == piZ).Count;

            return liCount;
        }

        /// <summary>The first tile of the area that pFMatch accepts, or null.</summary>
        private static UWTile fFirstTile(UWLevel pOLevel, int piX, int piY, int piWidth, int piDepth, System.Func<UWTile, bool> pFMatch)
        {
            for (int liX = piX; liX < piX + piWidth; liX++)
            {
                for (int liY = piY; liY < piY + piDepth; liY++)
                {
                    UWTile lOTile = pOLevel.GetTile(liX, liY);

                    if (lOTile != null && pFMatch(lOTile))
                        return lOTile;
                }
            }

            return null;
        }

        /// <summary>
        /// G3 PART H2 on a fresh world: level 13's trapped doors at 33/37 and 34/37 each hold a hidden use trigger (0x1B2,
        /// the kind the search finds) aiming at a damage trap on their square, and an unlock trigger (0x1A8) whose delete
        /// trap removes that damage trap. Disarmed with a sure skill, the trap leaves its square and the hidden trigger the
        /// door; unlocked (checkTrap 0xB), the other door's damage trap goes the same way.
        /// </summary>
        private static void fCheckUw2TrapSearch(string psData)
        {
            DataImport lOImport = new DataImport(psData, null, null, UWGameProfiles.Uw2);

            UWSaveCodec.StartNewCharacterUw2(lOImport.Session, new UWPlayerData.NewCharacter { Name = "Selfcheck" });

            UWLevel lOLevel = lOImport.Levels[12];
            FakeTrapHost lOHost = new FakeTrapHost(lOImport, 12);

            lOHost.Fake.SessionOf = lOImport.Session;

            UWObject lODoor = fFindHolder(lOLevel, 34, 37, 0x1B2);
            UWObject lOHidden = fFindTriggerDeep(lOLevel, 34, 37, 0x1B2);
            UWObject lOTrap = UWObjectMechanics.GetLinkedObject(lOHidden, lOLevel.Masterlist);
            bool lbFound = UWObjectSpellRulesUw2.TryFindTrap(lODoor, lOLevel.Masterlist, out UWObject lOFoundTrigger, out _);
            UWSkillCheck.ResultEnum? leDetect = UWTrapRulesUw2.DetectTrap(lODoor, lOLevel.Masterlist, 100, 13);
            UWSkillCheck.ResultEnum? leNone = UWTrapRulesUw2.DetectTrap(new UWObject(0x95), lOLevel.Masterlist, 100, 13);
            UWSkillCheck.ResultEnum? leRemoved = UWTrapRulesUw2.RemoveTrap(lODoor, 100, lOHost);

            Check.Str("uw2 search: level 13's door at 34/37 holds the hidden trigger; a sure Search finds it, a plain thing has none; a sure disarm takes the damage trap off 34/37 and the trigger off the door, and says so",
                (lODoor != null) + " " + (lbFound && lOFoundTrigger == lOHidden) + " " + (leDetect.HasValue && UWSkillCheck.IsSuccess(leDetect.Value)) + " "
                + !leNone.HasValue + " " + (leRemoved.HasValue && UWSkillCheck.IsSuccess(leRemoved.Value)) + " "
                + lOLevel.GetTile(34, 37).ObjectsInTile.Contains(lOTrap) + " " + UWObjectSpellRulesUw2.TryFindTrap(lODoor, lOLevel.Masterlist, out _, out _) + " "
                + lOHost.Fake.Shown.Log.Exists(l => l.StartsWith("message The ") && l.Contains("disarmed")),
                "True True True True True False False True");

            UWObject lOOtherDoor = fFindHolder(lOLevel, 33, 37, 0x1A8);
            UWObject lOOtherTrap = UWObjectMechanics.GetLinkedObject(fFindTriggerDeep(lOLevel, 33, 37, 0x1B2), lOLevel.Masterlist);
            bool lbOtherThere = lOLevel.GetTile(33, 37).ObjectsInTile.Contains(lOOtherTrap);

            UWTrapRulesUw2.CheckTrap(lOOtherDoor, UWTrapRulesUw2.UnlockMode, 33, 37, lOHost);

            Check.Str("uw2 search: unlocking the door at 33/37 (checkTrap 0xB) runs its unlock trigger, whose delete trap takes the damage trap off the square",
                (lOOtherDoor != null) + " " + lbOtherThere + " " + lOLevel.GetTile(33, 37).ObjectsInTile.Contains(lOOtherTrap),
                "True True False");
        }

        /// <summary>The thing in the tile whose chain holds an object of this id (three deep), or null.</summary>
        private static UWObject fFindHolder(UWLevel pOLevel, int piTileX, int piTileY, int piId)
        {
            UWTile lOTile = pOLevel != null ? pOLevel.GetTile(piTileX, piTileY) : null;

            if (lOTile == null || lOTile.ObjectsInTile == null)
                return null;

            foreach (UWObject lOObject in lOTile.ObjectsInTile)
            {
                if (lOObject != null && lOObject.ID != piId && fFindHeld(lOObject, piId, pOLevel.Masterlist, 0) != null)
                    return lOObject;
            }

            return null;
        }

        private static string fSetters(UWObject pOTrigger)
        {
            return (UWTrapRulesUw2.MaySetOff(pOTrigger, UWTrapRules.Setter.Player) ? "1" : "0")
                + (UWTrapRulesUw2.MaySetOff(pOTrigger, UWTrapRules.Setter.Creature) ? "1" : "0")
                + (UWTrapRulesUw2.MaySetOff(pOTrigger, UWTrapRules.Setter.Thing) ? "1" : "0");
        }

        /// <summary>The first object of this id in the tile or held by something there (a switch's or a lock's trigger),
        /// three deep, or null.</summary>
        private static UWObject fFindTriggerDeep(UWLevel pOLevel, int piTileX, int piTileY, int piId)
        {
            UWTile lOTile = pOLevel != null ? pOLevel.GetTile(piTileX, piTileY) : null;

            if (lOTile == null || lOTile.ObjectsInTile == null)
                return null;

            foreach (UWObject lOObject in lOTile.ObjectsInTile)
            {
                UWObject lOFound = fFindHeld(lOObject, piId, pOLevel.Masterlist, 0);

                if (lOFound != null)
                    return lOFound;
            }

            return null;
        }

        private static UWObject fFindHeld(UWObject pOObject, int piId, System.Collections.Generic.List<UWObject> pOMaster, int piDepth)
        {
            if (pOObject == null || piDepth > 3)
                return null;

            if (pOObject.ID == piId)
                return pOObject;

            if (pOObject.HasQuantity || pOObject.Quantity == 0 || pOObject.Quantity >= pOMaster.Count)
                return null;

            for (UWObject lOHeld = pOMaster[pOObject.Quantity]; lOHeld != null; lOHeld = lOHeld.Link != 0 && lOHeld.Link < pOMaster.Count ? pOMaster[lOHeld.Link] : null)
            {
                UWObject lOFound = fFindHeld(lOHeld, piId, pOMaster, piDepth + 1);

                if (lOFound != null)
                    return lOFound;

                if (lOHeld.Link == 0)
                    break;
            }

            return null;
        }

        /// <summary>The first object of this id lying in the tile, or null.</summary>
        private static UWObject fFindTrigger(UWLevel pOLevel, int piTileX, int piTileY, int piId)
        {
            UWTile lOTile = pOLevel != null ? pOLevel.GetTile(piTileX, piTileY) : null;

            return lOTile != null && lOTile.ObjectsInTile != null ? lOTile.ObjectsInTile.Find(o => o != null && o.ID == piId) : null;
        }
    }
}
