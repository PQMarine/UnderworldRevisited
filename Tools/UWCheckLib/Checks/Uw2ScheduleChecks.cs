using System.Collections.Generic;
using System.IO;
using System.Linq;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S SCHEDULER (G6 part 1, 2026-11-16): UWScheduleRulesUw2 against uw2 event/SCHEDULE.C and
    /// SCDEVENT.C - the clocks and the rows due, once rows, the level byte, event 10's conditional rows, the day's wrap,
    /// the migration queue, deltatotheta and player_looking; with the second game's data, block 15's goblin rule (quest 60
    /// and XC_CHANGED turn race 6 on level 10 hostile, goal 5 on the player).
    /// </summary>
    public static partial class SelfChecks
    {
        private sealed class FakeScheduleHost : IUWScheduleHost
        {
            public UWPlayerFile Record { get; set; } = UWPlayerRecordUw2.NewWalker();

            public int LevelNumber { get; set; } = 10;

            public IUWWorld World { get; set; }

            public IUWTrapHost Traps { get; set; }

            public UWObjectClassProperties Properties { get; set; }

            public UWObject TalkingTo { get; set; }

            public int PlayerX8 { get; set; }

            public int PlayerY8 { get; set; }

            public int PlayerFacingEighth { get; set; }
        }

        private static UWScheduleUw2.Row fRow(int piTime, int piLevel, int piEvent, int piOnce, params int[] piParams)
        {
            UWScheduleUw2.Row lORow = new UWScheduleUw2.Row { Time = piTime, Level = piLevel, Event = piEvent, Once = piOnce };

            for (int liAt = 0; liAt < piParams.Length; liAt++)
                lORow.Parameters[liAt] = (byte)piParams[liAt];

            return lORow;
        }

        private static UWScheduleBookUw2 fBook(int piBlock, params UWScheduleUw2.Row[] pORows)
        {
            UWScheduleBookUw2 lOBook = new UWScheduleBookUw2();
            UWScheduleUw2 lOBlock = new UWScheduleUw2 { BlockNumber = piBlock };

            lOBlock.Rows.AddRange(pORows);
            lOBook.Blocks[piBlock] = lOBlock;

            if (piBlock != 0)
                lOBook.Blocks[0] = new UWScheduleUw2();

            return lOBook;
        }

        private static string fQuests(UWPlayerFile pOFile, params int[] piQuests)
        {
            return string.Join("", piQuests.Select(q => UWPlayerRecordUw2.GetQuest(pOFile, q)));
        }

        private static void fCheckUw2Schedules(CheckContext pOContext)
        {
            Check.Group("UW2 schedules: the clocks, the rows due, once rows, the level byte, event 10, the wrap, migrations");

            // Block 1 (the castle plot): quest bits 1..4 at times 1, 3, 5, 9; the second a once row; a row for level 3 only,
            // one for any level, one for world 1 (0xF7, levels 9-16).
            FakeScheduleHost lOHost = new FakeScheduleHost { World = new FakeWorld() };
            UWScheduleBookUw2 lOBook = fBook(1,
                fRow(1, 10, UWScheduleRulesUw2.EventQuestBit, 0, 1, 1),
                fRow(3, 10, UWScheduleRulesUw2.EventQuestBit, 1, 2, 1),
                fRow(3, 3, UWScheduleRulesUw2.EventQuestBit, 0, 5, 1),
                fRow(5, 0xFF, UWScheduleRulesUw2.EventQuestBit, 0, 3, 1),
                fRow(9, 0xF7, UWScheduleRulesUw2.EventQuestBit, 0, 4, 1));

            UWPlayerRecordUw2.SetXClock(lOHost.Record, 1, 4);
            UWScheduleRulesUw2.SetAllClocks(lOBook, lOHost);
            string lsAt4 = fQuests(lOHost.Record, 1, 2, 3, 4, 5) + " rows " + lOBook.Blocks[1].Rows.Count + " next " + lOBook.Blocks[1].ClockNextRow[10];

            UWPlayerRecordUw2.SetXClock(lOHost.Record, 1, 9);
            UWScheduleRulesUw2.SetAllClocks(lOBook, lOHost);
            string lsAt9 = fQuests(lOHost.Record, 1, 2, 3, 4, 5) + " next " + lOBook.Blocks[1].ClockNextRow[10] + " time " + lOBook.Blocks[1].ClockTime[10];

            Check.Str("clock 4 runs times 1 and 3 for level 10 (the once row deleted, level 3's not), clock 9 the rest; the clock and next row per level",
                lsAt4 + " | " + lsAt9, "11000 rows 4 next 2 | 11110 next 4 time 9");

            // The same clock again runs nothing; another level's clock is its own (level 11 catches up on any-level and world rows).
            UWPlayerRecordUw2.SetQuest(lOHost.Record, 3, 0);
            UWScheduleRulesUw2.SetAllClocks(lOBook, lOHost);
            string lsAgain = fQuests(lOHost.Record, 3);
            lOHost.LevelNumber = 11;
            UWPlayerRecordUw2.SetQuest(lOHost.Record, 1, 0);
            UWScheduleRulesUw2.SetAllClocks(lOBook, lOHost);
            Check.Str("the same clock runs nothing again; level 11 catches up on the any-level and world rows only",
                lsAgain + " " + fQuests(lOHost.Record, 1, 3), "0 01");

            // Event 10: variable 5 == 2 runs the disabled rows after it up to the next test; a repeating row is disabled
            // again, a once row deleted; block 15 rescans from row 0 every time.
            FakeScheduleHost lOTestHost = new FakeScheduleHost { World = new FakeWorld() };
            UWScheduleBookUw2 lOTests = fBook(15,
                fRow(0, 0xFF, UWScheduleRulesUw2.EventTest, 0, 5, 0, 1, 0, 0, 2, 0),
                fRow(0, 0xFF, -UWScheduleRulesUw2.EventQuestBit, 0, 10, 1),
                fRow(0, 0xFF, -UWScheduleRulesUw2.EventQuestBit, 1, 11, 1),
                fRow(0, 0xFF, -UWScheduleRulesUw2.EventTest, 0, 6, 0, 1, 0, 0, 1, 0),
                fRow(0, 0xFF, -UWScheduleRulesUw2.EventQuestBit, 0, 12, 1));

            UWPlayerRecordUw2.SetXClock(lOTestHost.Record, 15, 1);
            UWScheduleRulesUw2.SetAllClocks(lOTests, lOTestHost);
            string lsFailed = fQuests(lOTestHost.Record, 10, 11, 12);
            UWPlayerRecordUw2.SetNumbered(lOTestHost.Record, 5, 2);
            UWPlayerRecordUw2.SetXClock(lOTestHost.Record, 15, 2);
            UWScheduleRulesUw2.SetAllClocks(lOTests, lOTestHost);
            Check.Str("event 10: nothing while variable 5 is not 2; then the rows after it to the next test, the repeating one disabled again, the once one gone",
                lsFailed + " " + fQuests(lOTestHost.Record, 10, 11, 12) + " " + string.Join(",", lOTests.Blocks[15].Rows.Select(r => r.Event)),
                "000 110 10,-4,-10,-4");

            // Block 0 wraps at 72: from 70 to 2 it runs to 71 first, then from 0.
            FakeScheduleHost lODayHost = new FakeScheduleHost { World = new FakeWorld() };
            UWScheduleBookUw2 lODay = fBook(0,
                fRow(1, 0xFF, UWScheduleRulesUw2.EventQuestBit, 0, 20, 1),
                fRow(71, 0xFF, UWScheduleRulesUw2.EventQuestBit, 0, 21, 1));

            lODay.Blocks[0].ClockTime[10] = 70;
            lODay.Blocks[0].ClockNextRow[10] = 1;
            lODayHost.Record.Record[UWPlayerRecordUw2.XClocksOffset] = 2;
            UWScheduleRulesUw2.StepDay(lODay, lODayHost);
            Check.Str("the day wraps: 70 -> 2 runs 71's row, then 1's", fQuests(lODayHost.Record, 20, 21) + " time " + lODay.Blocks[0].ClockTime[10], "11 time 2");

            // A teleport that cannot happen with a retry queues a once copy for the day clock plus the delay, put into block 0.
            UWScheduleBookUw2 lOMove = fBook(1);
            UWScheduleUw2.Row lOTeleport = fRow(0, 0xFF, UWScheduleRulesUw2.EventTeleport, 0, 5, 5, 0, 0x99, 0, 0, 0, 4);
            UWScheduleRulesUw2.Migrate(lOMove, lOTeleport);
            lOMove.Migrations[0].Time = 7;
            UWScheduleRulesUw2.DoMigrations(lOMove);
            Check.Str("migration: the queued row into block 0 at its time, the queue empty", lOMove.Blocks[0].Rows.Count + " " + lOMove.Blocks[0].Rows[0].Time
                + " " + lOMove.Migrations.Count, "1 7 0");

            // deltatotheta: 0 along +y, clockwise towards +x; player_looking within eight squares and one eighth.
            string lsTheta = string.Join(" ", new[] { (0, 8), (8, 8), (8, 0), (8, -8), (0, -8), (-8, -8), (-8, 0), (-8, 8) }
                .Select(d => UWScheduleRulesUw2.DeltaToTheta((sbyte)d.Item1, (sbyte)d.Item2)));
            FakeScheduleHost lOEye = new FakeScheduleHost { PlayerX8 = (20 << 3) + 3, PlayerY8 = (20 << 3) + 3, PlayerFacingEighth = 0 };
            string lsLooking = UWScheduleRulesUw2.PlayerLooking((20 << 3) + 3, (24 << 3) + 3, lOEye) + " " + UWScheduleRulesUw2.PlayerLooking((20 << 3) + 3, (16 << 3) + 3, lOEye)
                + " " + UWScheduleRulesUw2.PlayerLooking((20 << 3) + 3, (29 << 3) + 3, lOEye);
            Check.Str("deltatotheta's eight headings; player_looking ahead, behind, nine squares ahead", lsTheta + " | " + lsLooking,
                "0 1 2 3 4 5 6 7 | True False False");

            fCheckUw2GoblinRule(pOContext);
        }

        /// <summary>The shipped SCD.ARK: block 15 rows 0-2 for world 1 (quest 60 = variable 0x13C) turn every goblin (race 6,
        /// no loner) of level 10 hostile with goal 5 on the player once XC_CHANGED moves.</summary>
        private static void fCheckUw2GoblinRule(CheckContext pOContext)
        {
            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2 goblin rule", "no second game here: skipped");

                return;
            }

            UWGameData lOData = new UWGameData(lsData, null, UWGameProfiles.Uw2);
            UWSessionWorld lOWorld = UWSaveCodec.Load(lOData, null).World;
            UWLevel lOLevel = lOWorld.Levels[9];
            string lsScd = Path.Combine(lOData.DataPath, UWSaveCodec.ScheduleFileName);

            if (lOLevel == null || !File.Exists(lsScd))
            {
                Check.Fail("uw2 goblin rule", "no level 10 or no SCD.ARK");

                return;
            }

            UWScheduleBookUw2 lOBook = UWScheduleBookUw2.Read(UWArk.Read(lsScd, UWArk.LayoutEnum.Uw2));

            Check.Bool("SCD.ARK unchanged comes back byte for byte", lOBook.ToBytes().SequenceEqual(File.ReadAllBytes(lsScd)), true);

            FakeScheduleHost lOHost = new FakeScheduleHost { World = new FakeWorld { Level = lOLevel }, Properties = lOData.ObjectClassProperties };
            List<UWNpc> lOGoblins = UWScheduleRulesUw2.Select(lOLevel, UWScheduleRulesUw2.ByRace | (6 << 8), lOData.ObjectClassProperties);

            UWScheduleRulesUw2.SetAllClocks(lOBook, lOHost);
            string lsBefore = string.Join(",", lOGoblins.Select(g => new UWCritterRecord(g).Attitude).Distinct());

            UWPlayerRecordUw2.SetQuest(lOHost.Record, 60, 1);
            UWPlayerRecordUw2.SetXClock(lOHost.Record, UWScheduleRulesUw2.ChangedClock, 1);
            UWScheduleRulesUw2.SetAllClocks(lOBook, lOHost);
            string lsAfter = string.Join(",", lOGoblins.Select(g => new UWCritterRecord(g).Attitude + "/" + new UWCritterRecord(g).Goal + "/" + new UWCritterRecord(g).GTarg).Distinct());

            Check.Str("block 15: the six goblins of level 10 friendly or wary before; quest 60 and XC_CHANGED: all hostile, goal 5 on the player",
                lOGoblins.Count + " " + lsBefore + " -> " + lsAfter, "6 1,2 -> 0/5/1");

            // Sched_Save writes only a changed block, and Sched_Load put its number into the header: the empty block 2
            // keeps the file's 0, block 15 now says 15.
            UWScheduleBookUw2 lOSaved = UWScheduleBookUw2.Read(UWArk.Read(lOBook.ToBytes(), UWArk.LayoutEnum.Uw2));

            Check.Str("a save numbers only the changed blocks (block 2, block 15)",
                lOSaved.Blocks[2].BlockNumber + " " + lOSaved.Blocks[15].BlockNumber, "0 15");
        }
    }
}
