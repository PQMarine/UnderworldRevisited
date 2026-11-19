using System.Collections.Generic;
using System.Linq;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S PLOT DEATHS (UWSpecialDeathsUw2, UW2 plan G6 part 2): death_check's two modes on the castle's
    /// level 1 - the castle's people who cannot die and the guards called out from their post, Lady Tory after castle plot
    /// 8, a loner - and the named cases on a creature given their whoami: the hordling guard, Patterson's talk and castle
    /// clock, Krilner's yield, the Listener and Altara's dagger, Loth's liches, Mors Gotha in and out of Killorn Keep, the
    /// counted one, the bloodworms, race 0xB and the pit record's best.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2SpecialDeaths(CheckContext pOContext)
        {
            Check.Group("UW2 plot deaths: death_check, stop_and_talk, call_out_the_guards, genocide");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2 plot deaths", "no second game here: skipped");

                return;
            }

            DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);
            FakeTrapHost lOTraps = new FakeTrapHost(lOImport, 0);
            FakeWorld lOWorld = lOTraps.Fake;
            UWObjectClassProperties lOProps = lOImport.ObjectClassProperties;
            FakeScheduleHost lOHost = new FakeScheduleHost { World = lOWorld, Traps = lOTraps, Properties = lOProps, LevelNumber = 1 };
            UWLevel lOLevel = lOWorld.Level;
            List<UWNpc> lONpcs = lOLevel.Masterlist.OfType<UWNpc>().Where(n => UWObjectIds.IsCreature(n.ID)).ToList();

            // Creatures of the active list (gronk_race and genocide walk it), for the race cases.
            List<UWNpc> lOActive = lOLevel.ActiveMobiles.Select(i => lOLevel.Masterlist[i] as UWNpc).Where(n => n != null && UWObjectIds.IsCreature(n.ID)).ToList();

            UWCreatureRef fRef(UWNpc pONpc) => new UWCreatureRef(pONpc, new UWCritterRecord(pONpc), new UWTilePos(pONpc.TileX, pONpc.TileY), false);
            bool fRun(UWNpc pONpc, int piMode, out System.Action pfAfter) => UWSpecialDeathsUw2.Check(fRef(pONpc), piMode, lOHost, out pfAfter);

            // A. Charles (whoami 0x83): mode 0 refuses, a third of the table's hit points less one, and the ten scheduled
            // triggers of the guards' post 15/29 fire with their guard templates sent to his home, goal 1, temporary, strong.
            UWNpc lOCharles = lONpcs.First(n => n.NPCwhoami == 0x83);
            UWCritterRecord lOCharlesRecord = new UWCritterRecord(lOCharles);
            lOProps.TryGetCritter(lOCharles.ID, out UWObjectClassProperties.Critter lORow);
            UWObject lOPostTrigger = lOLevel.GetTile(UWSpecialDeathsUw2.GuardPostX, UWSpecialDeathsUw2.GuardPostY).ObjectsInTile.First(o => UWTrapRulesUw2.IsTrigger(o.ID));
            UWNpc lOTemplate = (UWNpc)UWObjectMechanics.GetLinkedObject(UWObjectMechanics.GetLinkedObject(lOPostTrigger, lOLevel.Masterlist), lOLevel.Masterlist);

            lOWorld.Log.Clear();
            bool lbCharles = fRun(lOCharles, 0, out System.Action lfNone);
            UWCritterRecord lOTemplateRecord = new UWCritterRecord(lOTemplate);

            Check.Str("a castle person: refused | hit points avghit/3-1 | spawns | the first template's home, goal, temporary, strong",
                lbCharles + " | " + (lOCharlesRecord.HitPoints == ((lORow.Vitality / 3 - 1) & 0xFF)) + " | "
                + lOWorld.Log.Count(l => l.StartsWith("spawn")) + " | " + lOTemplateRecord.HomeX + "/" + lOTemplateRecord.HomeY + " "
                + lOTemplateRecord.Goal + " " + lOTemplate.NPCSpawned + " " + lOTemplateRecord.IsStrong,
                "False | True | 10 | " + lOCharlesRecord.HomeX + "/" + lOCharlesRecord.HomeY + " 1 True True");

            // B. Lady Tory dies once the castle plot reached 8; a loner of the castle dies too.
            UWNpc lOTory = lONpcs.First(n => n.NPCwhoami == UWSpecialDeathsUw2.ToryWhoAmI);
            UWPlayerRecordUw2.SetXClock(lOHost.Record, 1, 7);
            bool lbToryAt7 = fRun(lOTory, 0, out lfNone);
            UWPlayerRecordUw2.SetXClock(lOHost.Record, 1, 8);
            bool lbToryAt8 = fRun(lOTory, 0, out lfNone);
            new UWCritterRecord(lOCharles).AttitudeLocked = true;
            bool lbLoner = fRun(lOCharles, 0, out lfNone);

            Check.Str("Lady Tory at castle plot 7, at 8; a loner of the castle", lbToryAt7 + " " + lbToryAt8 + " " + lbLoner, "False True True");

            // The rest on a creature of level 1 given the whoami (the rat).
            UWNpc lOSubject = lONpcs.First(n => n.NPCwhoami == 0);
            int liSubjectId = lOSubject.ID;
            UWCritterRecord lOSubjectRecord = new UWCritterRecord(lOSubject);

            // C. The guard 0x4B turns into a hordling the first time (0x5C hit points, goal 5, redrawn) and dies the second.
            lOSubject.NPCwhoami = UWSpecialDeathsUw2.HordlingGuardWhoAmI;
            lOWorld.Log.Clear();
            bool lbFirst = fRun(lOSubject, 0, out lfNone);
            string lsHordling = lbFirst + " 0x" + lOSubject.ID.ToString("X") + " " + lOSubjectRecord.HitPoints + " " + lOSubjectRecord.Goal + " "
                + lOWorld.Log.Contains("redraw " + UWSpecialDeathsUw2.HordlingId);
            bool lbSecond = fRun(lOSubject, 0, out lfNone);

            Check.Str("the guard 0x4B: refused as a hordling (id, hit points, goal, redrawn); then dies", lsHordling + " | " + lbSecond,
                "False 0x5E 92 5 True | True");

            lOSubject.ID = (ushort)liSubjectId;

            // D. Patterson at his death: he talks, the body waits; after the talk the castle plot goes to 0xD, to 0xE after
            // the djinn's sixth step, and never back.
            lOSubject.NPCwhoami = UWSpecialDeathsUw2.PattersonWhoAmI;
            string lsPatterson = "";

            foreach ((int liBefore, int liDjinn) in new[] { (9, 0), (9, 6), (15, 0) })
            {
                UWPlayerRecordUw2.SetXClock(lOHost.Record, 1, liBefore);
                UWPlayerRecordUw2.SetXClock(lOHost.Record, UWPlayerRecordUw2.DjinnClock, liDjinn);
                lOWorld.Log.Clear();
                bool lbDies = fRun(lOSubject, 1, out System.Action lfAfter);
                bool lbTalked = lOWorld.Log.Contains("talk " + lOSubject.ID) && lfAfter != null;

                lfAfter?.Invoke();
                lsPatterson += lbDies + " " + lbTalked + " " + UWPlayerRecordUw2.XClock(lOHost.Record, 1) + ", ";
            }

            Check.Str("Patterson at his death: dies, talks first; castle plot after the talk from 9, from 9 with the djinn at 6, from 15",
                lsPatterson, "True True 13, True True 14, True True 15, ");

            // E. Krilner yields once (50 tenths of experience, the talk, attitude 2 goal 8), the second time he dies; with
            // quest 28 at once.
            lOSubject.NPCwhoami = UWSpecialDeathsUw2.KrilnerWhoAmI;
            lOSubjectRecord.TileState = 0;
            int liExpBefore = lOWorld.Body.Numbers.Experience;
            bool lbYield = fRun(lOSubject, 0, out System.Action lfYield);
            string lsYield = lbYield + " " + (lfYield != null) + " " + (lOWorld.Body.Numbers.Experience > liExpBefore) + " "
                + lOSubjectRecord.Attitude + "/" + lOSubjectRecord.Goal + " " + lOWorld.Body.CombatModeLeft;
            bool lbAgain = fRun(lOSubject, 0, out lfNone);
            lOSubjectRecord.TileState = 0;
            UWPlayerRecordUw2.SetQuest(lOHost.Record, 28, 1);
            bool lbWithQuest = fRun(lOSubject, 0, out lfNone);

            Check.Str("Krilner: yields (talk, experience, 2/8, the fight left) | the second time | with quest 28",
                lsYield + " | " + lbAgain + " | " + lbWithQuest, "False True True 2/8 True | True | True");

            // F. The Listener: without Altara's dagger 1 hit point and goal 5; with it the dagger breaks, castle plot + 1, quest 11.
            lOSubject.NPCwhoami = UWSpecialDeathsUw2.ListenerWhoAmI;
            UWPlayerRecordUw2.SetXClock(lOHost.Record, 1, 9);
            bool lbNoDagger = fRun(lOSubject, 0, out lfNone);
            string lsNoDagger = lbNoDagger + " " + lOSubjectRecord.HitPoints + " " + lOSubjectRecord.Goal;
            int liBroken = 0;

            UWSpecialDeathsUw2.AltarasDaggerStrike = () => liBroken++;
            bool lbDagger;

            try
            {
                lbDagger = fRun(lOSubject, 0, out lfNone);
            }
            finally
            {
                UWSpecialDeathsUw2.AltarasDaggerStrike = null;
            }

            Check.Str("the Listener without the dagger (refused, hit points, goal) | with it (dies, broken, castle plot, quest 11)",
                lsNoDagger + " | " + lbDagger + " " + liBroken + " " + UWPlayerRecordUw2.XClock(lOHost.Record, 1) + " " + UWPlayerRecordUw2.GetQuest(lOHost.Record, 11),
                "False 1 5 | True 1 10 1");

            // G. Praecor Loth: every lich of the level dies (genocide 0x17), quest 7. Two creatures made liches.
            int liLichId = Enumerable.Range(0x40, 0x40).First(i => lOProps.TryGetCritter(i, out UWObjectClassProperties.Critter c) && c.GeneralType == UWSpecialDeathsUw2.LichRace);
            List<UWNpc> lOOthers = lOActive.Where(n => n != lOSubject).Take(2).ToList();
            List<int> liOtherIds = lOOthers.Select(n => (int)n.ID).ToList();

            lOOthers.ForEach(n => n.ID = (ushort)liLichId);
            lOSubject.NPCwhoami = UWSpecialDeathsUw2.LothWhoAmI;
            lOWorld.Log.Clear();
            fRun(lOSubject, 1, out lfNone);
            Check.Str("Loth: his liches die, quest 7", lOWorld.Log.Count(l => l == "kill " + liLichId) + " " + UWPlayerRecordUw2.GetQuest(lOHost.Record, 7), "2 1");

            for (int liAt = 0; liAt < lOOthers.Count; liAt++)
                lOOthers[liAt].ID = (ushort)liOtherIds[liAt];

            // H. Mors Gotha: outside Killorn Keep she talks and dies, quest 64 and XC_CHANGED after the talk; in Killorn Keep
            // (world 2) she talks and lives, after the crash (quest 54) she is taken away.
            lOSubject.NPCwhoami = UWSpecialDeathsUw2.GothaWhoAmI;
            int liChanged = UWPlayerRecordUw2.XClock(lOHost.Record, UWScheduleRulesUw2.ChangedClock);
            bool lbGotha = fRun(lOSubject, 0, out System.Action lfGotha);
            string lsBeforeTalk = UWPlayerRecordUw2.GetQuest(lOHost.Record, 64).ToString();
            lfGotha?.Invoke();
            string lsOutside = lbGotha + " " + lsBeforeTalk + UWPlayerRecordUw2.GetQuest(lOHost.Record, 64) + " "
                + (UWPlayerRecordUw2.XClock(lOHost.Record, UWScheduleRulesUw2.ChangedClock) - liChanged);
            lOHost.LevelNumber = 17;
            bool lbKillorn = fRun(lOSubject, 0, out System.Action lfKillorn);
            UWPlayerRecordUw2.SetQuest(lOHost.Record, 54, 1);
            lOWorld.Log.Clear();
            bool lbCrashed = fRun(lOSubject, 0, out lfNone);
            lOHost.LevelNumber = 1;

            Check.Str("Mors Gotha outside (dies, quest 64 before/after the talk, XC_CHANGED) | in Killorn Keep | after the crash",
                lsOutside + " | " + lbKillorn + " " + (lfKillorn != null) + " | " + lbCrashed + " " + lOWorld.Log.Contains("remove " + lOSubject.ID),
                "True 01 1 | False True | False True");

            // I. The counted one (0x3A): the second death sets quest 50, shakes the screen by 0x58 and parks the count at 0x18.
            lOSubject.NPCwhoami = UWSpecialDeathsUw2.CountedWhoAmI;
            lOWorld.Log.Clear();
            fRun(lOSubject, 1, out lfNone);
            string lsOnce = UWPlayerRecordUw2.GetQuest(lOHost.Record, 50).ToString();
            fRun(lOSubject, 1, out lfNone);

            Check.Str("the counted one: quest 50 after the first, after the second; the shake; the count",
                lsOnce + UWPlayerRecordUw2.GetQuest(lOHost.Record, 50) + " " + lOWorld.Shown.Log.Contains("shake 88") + " "
                + UWPlayerRecordUw2.GetQuest(lOHost.Record, UWSpecialDeathsUw2.CountedQuest), "01 True 24");

            // J. Bloodworms on level 4 count (quest byte 7), elsewhere not; the pit record's best goes into X clock 14.
            lOSubject.NPCwhoami = 0;
            lOSubject.ID = UWSpecialDeathsUw2.BloodwormId;
            fRun(lOSubject, 1, out lfNone);
            lOHost.LevelNumber = UWSpecialDeathsUw2.WormLevel;
            fRun(lOSubject, 1, out lfNone);
            fRun(lOSubject, 1, out lfNone);
            lOHost.LevelNumber = 1;
            lOSubject.ID = (ushort)liSubjectId;
            UWPlayerRecordUw2.SetQuest(lOHost.Record, UWSpecialDeathsUw2.PitRecordQuest, 5);
            fRun(lOSubject, 1, out lfNone);

            Check.Str("bloodworms killed on level 4 (one elsewhere, two there); the pit record 5 as the best",
                UWPlayerRecordUw2.GetQuest(lOHost.Record, UWSpecialDeathsUw2.WormsKilledQuest) + " " + UWPlayerRecordUw2.XClock(lOHost.Record, UWSpecialDeathsUw2.PitKillsClock),
                "2 5");

            // K. A creature of race 0xB dying turns the others of its race hostile (loners not).
            int liRaceId = Enumerable.Range(0x40, 0x40).First(i => lOProps.TryGetCritter(i, out UWObjectClassProperties.Critter c) && c.GeneralType == UWSpecialDeathsUw2.HostileRace);
            List<UWNpc> lOKin = lOActive.Where(n => n != lOSubject && !new UWCritterRecord(n).AttitudeLocked).Take(2).ToList();
            List<int> liKinIds = lOKin.Select(n => (int)n.ID).ToList();

            lOKin.ForEach(n => { n.ID = (ushort)liRaceId; new UWCritterRecord(n).Attitude = 3; });
            new UWCritterRecord(lOKin[1]).AttitudeLocked = true;
            lOSubject.ID = (ushort)liRaceId;
            fRun(lOSubject, 1, out lfNone);

            Check.Str("race 0xB: a kinsman turns hostile, a loner does not", string.Join(" ", lOKin.Select(n => new UWCritterRecord(n).Attitude)), "0 3");

            new UWCritterRecord(lOKin[1]).AttitudeLocked = false;

            for (int liAt = 0; liAt < lOKin.Count; liAt++)
                lOKin[liAt].ID = (ushort)liKinIds[liAt];

            lOSubject.ID = (ushort)liSubjectId;

            fCheckUw2PlayerDeath(lOLevel, lOProps, lONpcs);
        }

        /// <summary>
        /// G6 PART 3, THE PLAYER'S DEATH (UWDeathRulesUw2): jail for a killer of the castle (not a loner, level 1 only),
        /// the time it passes, pass_time, the escape test of do_teleport, the guards calmed with quest 112, Lord British's
        /// new home, the pit fighters standing down, and the blow's attacker becoming the player's last_hit.
        /// </summary>
        private static void fCheckUw2PlayerDeath(UWLevel pOLevel, UWObjectClassProperties pOProps, List<UWNpc> pONpcs)
        {
            UWNpc lOGuard = pONpcs.First(n => n.NPCwhoami == 0x81 && !new UWCritterRecord(n).AttitudeLocked);
            UWNpc lORat = pONpcs.First(n => n.NPCwhoami == 0);
            string lsDecide = UWDeathRulesUw2.Decide(1, lOGuard) + " " + UWDeathRulesUw2.Decide(1, lORat) + " " + UWDeathRulesUw2.Decide(2, lOGuard)
                + " " + UWDeathRulesUw2.Decide(9, lOGuard) + " " + UWDeathRulesUw2.Decide(1, null);
            new UWCritterRecord(lOGuard).AttitudeLocked = true;
            lsDecide += " " + UWDeathRulesUw2.Decide(1, lOGuard);
            new UWCritterRecord(lOGuard).AttitudeLocked = false;

            Check.Str("the player's death: a castle guard on level 1, a rat, the guard on level 2 and 9, nobody, the guard a loner",
                lsDecide, "Jail Final Final Gem Final Final");

            // The time in jail: (40 - minute % 20) minutes less 60..149 seconds; pass_time moves the clock and the day clock.
            UWPlayerFile lOFile = UWPlayerRecordUw2.NewWalker();
            string lsSeconds = UWDeathRulesUw2.JailSeconds(0, 0) + " " + UWDeathRulesUw2.JailSeconds(0, 89) + " " + UWDeathRulesUw2.JailSeconds(0x3C00 * 5, 0);
            UWBinary.Write32(lOFile.Record, UWPlayerRecordUw2.GameClockOffset, 0);
            UWDeathRulesUw2.PassTime(lOFile, 2340);

            Check.Str("jail time in seconds (minute 0 roll 0, roll 89, minute 5); pass_time 2340 s: the clock and the day clock",
                lsSeconds + " | " + UWPlayerRecordUw2.GameClock(lOFile) + " " + UWPlayerRecordUw2.XClock(lOFile, 0), "2340 2251 2040 | 599040 1");

            // do_teleport's escape: in the cell (42 and 43 of row 38, level 0 or 1) not, elsewhere quest 112 off and 124 on.
            UWPlayerRecordUw2.SetQuest(lOFile, UWDeathRulesUw2.JailedQuest, 1);
            string lsEscape = UWDeathRulesUw2.CheckEscape(lOFile, 1, 0x2A, 0x26) + " " + UWDeathRulesUw2.CheckEscape(lOFile, 0, 0x2B, 0x26) + " "
                + UWDeathRulesUw2.CheckEscape(lOFile, 2, 0x2A, 0x26) + " " + UWPlayerRecordUw2.GetQuest(lOFile, 112) + UWPlayerRecordUw2.GetQuest(lOFile, 124)
                + " " + UWDeathRulesUw2.CheckEscape(lOFile, 1, 0x20, 0x20);

            Check.Str("the escape: the cell, the cell's other square, the cell's square on level 2 (quests 112/124 after), then without quest 112",
                lsEscape, "False False True 01 False");

            // The guards calmed: every non-loner of race 0x1C attitude 2 and goal 1, quest 112.
            List<UWNpc> lOGuards = UWScheduleRulesUw2.Select(pOLevel, UWScheduleRulesUw2.ByRace | (UWDeathRulesUw2.GuardRace << 8), pOProps);
            lOGuards.ForEach(g => { new UWCritterRecord(g).Attitude = 0; new UWCritterRecord(g).Goal = 5; });
            UWPlayerFile lOJail = UWPlayerRecordUw2.NewWalker();
            UWDeathRulesUw2.CalmTheGuards(pOLevel, pOProps, lOJail);
            UWNpc lOBritish = UWDeathRulesUw2.FindBritish(pOLevel);
            UWDeathRulesUw2.SendBritishHome(lOBritish);
            UWCritterRecord lOBritishRecord = new UWCritterRecord(lOBritish);

            Check.Str("the guards calmed (how many, attitudes, goals), quest 112; Lord British's home and goal",
                (lOGuards.Count > 0) + " " + string.Join(",", lOGuards.Select(g => new UWCritterRecord(g).Attitude).Distinct()) + " "
                + string.Join(",", lOGuards.Select(g => new UWCritterRecord(g).Goal).Distinct()) + " " + UWPlayerRecordUw2.GetQuest(lOJail, 112)
                + " | " + lOBritishRecord.HomeX + "/" + lOBritishRecord.HomeY + " " + lOBritishRecord.Goal,
                "True 2 1 1 | 40/39 1");

            // The pits lost: the listed fighters stand down (attitude 1, no attacker, goal 1).
            UWPlayerFile lOPits = UWPlayerRecordUw2.NewWalker();
            UWCritterRecord lOFighter = new UWCritterRecord(lORat);
            int liRatIndex = pOLevel.Masterlist.IndexOf(lORat);
            lOPits.Record[UWPlayerRecordUw2.PitsByteOffset] |= UWPlayerRecordUw2.PitsBit;
            lOPits.Record[UWPlayerRecordUw2.PitFightersOffset + 2] = (byte)liRatIndex;
            lOFighter.Attitude = 0;
            lOFighter.Attacker = 1;
            lOFighter.Goal = 5;
            int liStood = UWDeathRulesUw2.PitFightersStandDown(pOLevel, lOPits);

            Check.Str("the pits lost: fighters found, attitude, attacker, goal", liStood + " " + lOFighter.Attitude + " " + lOFighter.Attacker + " " + lOFighter.Goal,
                "1 1 0 1");

            // The blow's attacker: a creature's blow on the player names it, a trap's does not.
            FakeWorld lOWorld = new FakeWorld();
            UWBlowRouting.Land(lOWorld, UWDamageTarget.ThePlayer, UWBlow.ToPlayer(UWDamageRequest.Plain(3, 0), 7));
            UWBlowRouting.Land(lOWorld, UWDamageTarget.ThePlayer, UWBlow.ToPlayer(UWDamageRequest.Plain(3, 0)));
            UWPlayerFile lOStruck = UWPlayerRecordUw2.NewWalker();
            UWDeathRulesUw2.SetPlayerLastHit(lOStruck, 7);

            Check.Str("the player struck: by creature 7, then by a trap (no attacker); last_hit kept in the player object",
                string.Join(",", lOWorld.Blows.Where(b => b.StartsWith("player struck"))) + " | " + UWDeathRulesUw2.PlayerLastHit(lOStruck),
                "player struck by 7 | 7");
        }
    }
}
