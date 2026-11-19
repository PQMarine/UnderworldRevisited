using System.Collections.Generic;
using System.Linq;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE PITS OF CARNAGE (UWArenaRulesUw2, UW2 plan G6 part 4): in_arena and mpos, do_teleport's running test, babl_hack
    /// 2's layout of the fighters and Jospur's debt, place_pitfighter's rolls and record, arena_player_runs,
    /// arena_opponent_runs and death_check's pit part, on the castle level's creatures as stand-ins.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2Arena(CheckContext pOContext)
        {
            Check.Group("UW2 arena: in_arena, running away, the fight's set-up, the fighters, the pit record");

            // in_arena: the centre, a near corner, an axis, the far corners of quarters 0 and 2, quarter 1's edge.
            string lsIn = string.Join(" ", new[] { (31, 31), (34, 34), (34, 31), (46, 46), (47, 47), (15, 15), (17, 17), (47, 27) }
                .Select(p => UWArenaRulesUw2.InArena(p.Item1, p.Item2)));

            Check.Str("in_arena: centre, 3/3 off, on an axis, 15/15 and 16/16 off (quarter 0), -16 and -14 (quarter 2), +16/-4 (quarter 1)",
                lsIn, "False True False True False False True True");

            // do_teleport's test: the same quarter, another quarter, another level, out of the arena, not in the pits.
            UWPlayerFile lOFile = UWPlayerRecordUw2.NewWalker();
            string lsNotInPits = UWArenaRulesUw2.TeleportRuns(lOFile, 2, 1, 31, 31, 34, 34).ToString();
            lOFile.Record[UWPlayerRecordUw2.PitsByteOffset] |= UWPlayerRecordUw2.PitsBit;
            string lsRuns = UWArenaRulesUw2.TeleportRuns(lOFile, 1, 1, 36, 36, 34, 34) + " " + UWArenaRulesUw2.TeleportRuns(lOFile, 1, 1, 24, 36, 34, 34) + " "
                + UWArenaRulesUw2.TeleportRuns(lOFile, 2, 1, 36, 36, 34, 34) + " " + UWArenaRulesUw2.TeleportRuns(lOFile, 1, 1, 31, 31, 34, 34);

            Check.Str("teleport in the pits: same quarter, another quarter, another level, out of the arena; not in the pits", lsRuns + " | " + lsNotInPits,
                "False True True True | False");

            // babl_hack 2: three towards corner 0; five towards corner 2 with the second place failing.
            UWPlayerFile lOFight = UWPlayerRecordUw2.NewWalker();
            List<string> lOPlaced = new List<string>();
            int liNext = 10;
            UWArenaRulesUw2.InArenaOnArrival = false;
            int liThree = UWArenaRulesUw2.SetUpFight(lOFight, 1, 0, 3, (p, x, y) => { lOPlaced.Add(x + "/" + y); return liNext++; });
            string lsThree = liThree + " " + string.Join(",", lOPlaced) + " | " + string.Join(",", Enumerable.Range(0, 6).Select(i => UWPlayerRecordUw2.PitFighter(lOFight, i)))
                + " debt " + UWPlayerRecordUw2.GetQuest(lOFight, UWArenaRulesUw2.JospurDebtQuest) + " " + UWArenaRulesUw2.InArenaOnArrival;

            Check.Str("babl_hack 2 (power 1, corner 0, three): placed, the spots, pit_fighters 0-4, the debt, in the arena on arrival",
                lsThree, "3 35/35,35/36,36/35 | 12,11,10,0,0,0 debt 12 True");

            UWPlayerFile lOFive = UWPlayerRecordUw2.NewWalker();
            List<string> lOFiveSpots = new List<string>();
            int liCall = 0;
            int liFive = UWArenaRulesUw2.SetUpFight(lOFive, 2, 2, 5, (p, x, y) => { lOFiveSpots.Add(p + "@" + x + "/" + y); liCall++; return liCall == 2 ? 0 : 20 + liCall; });
            string lsFive = liFive + " " + string.Join(",", lOFiveSpots) + " | " + string.Join(",", Enumerable.Range(0, 6).Select(i => lOFive.Record[UWPlayerRecordUw2.PitFightersOffset + i]))
                + " debt " + UWPlayerRecordUw2.GetQuest(lOFive, UWArenaRulesUw2.JospurDebtQuest);

            Check.Str("babl_hack 2 (power 2, corner 2, five, the second place failing): the strong one first, the triangle, the sixth byte, debt for four",
                lsFive, "4 99@25/27,2@27/27,2@27/26,2@26/27,2@27/25 | 25,24,23,0,0,21 debt 20");

            UWArenaRulesUw2.InArenaOnArrival = false;

            // place_pitfighter's rolls from scripted rand()s.
            string fRoll(int piPower, params int[] piRolls)
            {
                int liAt = 0;
                int liItem = UWArenaRulesUw2.RollFighter(piPower, () => piRolls[liAt++], out bool lbStrong);

                return "0x" + liItem.ToString("X") + " " + lbStrong;
            }

            Check.Str("place_pitfighter's kind: 0x79 strong turned 0x7B, a 0x77 not strong, 0x78 strong, a great troll",
                fRoll(1, 1, 1, 0, 1) + " | " + fRoll(1, 0, 2, 2) + " | " + fRoll(0x63, 1, 0, 0, 4) + " | " + fRoll(0x63, 1, 1, 0, 0),
                "0x7B False | 0x77 False | 0x78 True | 0x5C False");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2 arena on data", "no second game here: skipped");

                return;
            }

            DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);
            UWLevel lOLevel = lOImport.Levels[0];
            List<UWNpc> lOActive = lOLevel.ActiveMobiles.Select(i => lOLevel.Masterlist[i] as UWNpc).Where(n => n != null && UWObjectIds.IsCreature(n.ID)).Take(3).ToList();
            int[] liIndexes = lOActive.Select(n => lOLevel.Masterlist.IndexOf(n)).ToArray();

            // The pit fighter's record.
            UWNpc lOFighter = lOActive[2];
            UWArenaRulesUw2.MakePitFighter(lOFighter, 40, 41, true);
            UWCritterRecord lOFighterRecord = new UWCritterRecord(lOFighter);

            Check.Str("place_pitfighter's record: whoami, home, strong, temporary, attitude, goal/target, loner",
                lOFighter.NPCwhoami + " " + lOFighterRecord.HomeX + "/" + lOFighterRecord.HomeY + " " + lOFighterRecord.IsStrong + " " + lOFighter.NPCSpawned + " "
                + lOFighterRecord.Attitude + " " + lOFighterRecord.Goal + "/" + lOFighterRecord.GTarg + " " + lOFighterRecord.AttitudeLocked,
                "102 40/41 True True 0 5/1 True");

            // arena_player_runs: places 1 and 2 listed; the first stands down and talks, the other leaves the list.
            UWPlayerFile lORun = UWPlayerRecordUw2.NewWalker();
            lORun.Record[UWPlayerRecordUw2.PitsByteOffset] |= UWPlayerRecordUw2.PitsBit;
            lORun.Record[UWPlayerRecordUw2.PitFightersOffset + 1] = (byte)liIndexes[0];
            lORun.Record[UWPlayerRecordUw2.PitFightersOffset + 2] = (byte)liIndexes[1];
            UWPlayerRecordUw2.SetQuest(lORun, UWArenaRulesUw2.PitRecordQuest, 3);
            UWPlayerRecordUw2.SetQuest(lORun, UWArenaRulesUw2.JospurDebtQuest, 12);
            List<UWNpc> lOTalked = new List<UWNpc>();
            UWArenaRulesUw2.RunningAway = false;
            UWArenaRulesUw2.PlayerRuns(lORun, lOLevel, n => lOTalked.Add(n));
            string lsRun = (lOTalked.Count == 1 && lOTalked[0] == lOActive[0]) + " " + string.Join(",", lOActive.Take(2).Select(n => new UWCritterRecord(n).Attitude + "/" + new UWCritterRecord(n).Goal))
                + " | " + string.Join(",", Enumerable.Range(0, 5).Select(i => UWPlayerRecordUw2.PitFighter(lORun, i))) + " " + UWPlayerRecordUw2.IsInPits(lORun) + " "
                + UWPlayerRecordUw2.GetQuest(lORun, UWArenaRulesUw2.PitRecordQuest) + " " + UWPlayerRecordUw2.GetQuest(lORun, UWArenaRulesUw2.JospurDebtQuest)
                + " | " + UWArenaRulesUw2.TakeRunningAway() + UWArenaRulesUw2.TakeRunningAway();

            Check.Str("the player runs: the first fighter talks, both stand down (1/1) | the list, in the pits, record, debt | babl_hack 1 twice",
                lsRun, "True 1/1,1/1 | 0,0,0,0,0 False 0 0 | 10");

            // arena_opponent_runs and death_check's pit part.
            UWPlayerFile lOWin = UWPlayerRecordUw2.NewWalker();
            lOWin.Record[UWPlayerRecordUw2.PitsByteOffset] |= UWPlayerRecordUw2.PitsBit;
            lOWin.Record[UWPlayerRecordUw2.PitFightersOffset + 0] = (byte)liIndexes[0];
            lOWin.Record[UWPlayerRecordUw2.PitFightersOffset + 1] = (byte)liIndexes[1];
            lOWin.Record[UWPlayerRecordUw2.PitFightersOffset + 2] = (byte)liIndexes[2];
            UWPlayerRecordUw2.SetQuest(lOWin, UWArenaRulesUw2.PitRecordQuest, 2);
            UWArenaRulesUw2.OpponentRuns(lOWin, lOActive[0], liIndexes[0]);
            string lsOpponent = UWPlayerRecordUw2.GetQuest(lOWin, UWArenaRulesUw2.PitRecordQuest) + " " + UWPlayerRecordUw2.XClock(lOWin, UWArenaRulesUw2.PitKillsClock) + " "
                + new UWCritterRecord(lOActive[0]).Goal + " " + UWPlayerRecordUw2.PitFighter(lOWin, 0);

            FakeTrapHost lOTraps = new FakeTrapHost(lOImport, 0);
            FakeScheduleHost lOHost = new FakeScheduleHost { World = lOTraps.Fake, Traps = lOTraps, Properties = lOImport.ObjectClassProperties, LevelNumber = 1, Record = lOWin };
            UWNpc lODying = lOActive[1];
            int liWhoAmI = lODying.NPCwhoami;
            lODying.NPCwhoami = 0;
            UWSpecialDeathsUw2.Check(new UWCreatureRef(lODying, new UWCritterRecord(lODying), new UWTilePos(0, 0), false, liIndexes[1]), 1, lOHost, out System.Action lfNone);
            lODying.NPCwhoami = (byte)liWhoAmI;
            string lsDeath = UWPlayerRecordUw2.GetQuest(lOWin, UWArenaRulesUw2.PitRecordQuest) + " " + UWPlayerRecordUw2.GetQuest(lOWin, 24) + " "
                + UWPlayerRecordUw2.PitFighter(lOWin, 1) + " " + UWPlayerRecordUw2.IsInPits(lOWin) + " " + UWPlayerRecordUw2.XClock(lOWin, UWArenaRulesUw2.PitKillsClock);

            Check.Str("an opponent runs (record 2 -> 3, best 3, goal 6, off the list) | one dies in the fight (record 4, quest 24, off the list, one still listed: in the pits, best 4)",
                lsOpponent + " | " + lsDeath, "3 3 6 0 | 4 1 0 True 4");
        }
    }
}
