using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// TIME PASSING FOR THE CREATURES (2026-10-15, reference audit round 2): the level pass of
    /// CRITTIME.C (update_all_critters_whilst_player_snoozes, up_crit) on a level change and after
    /// a night - healing half way to the average, the transient bits, the record written so the
    /// mind sees it at once (B16) - and the five-minute block's yearly_checkup.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckCreatureTimePass(DataImport pOData)
        {
            Check.Group("Creature time pass (CRITTIME.C up_crit)");

            // up_crit: (hp + avghit) / 2 below the average, never above it.
            fExpectInt("heal: avghit 20, hp 18 -> 19", UWLevelExit.HealedHitPoints(18, 20), 19);
            fExpectInt("heal: avghit 20, hp 2 -> 11", UWLevelExit.HealedHitPoints(2, 20), 11);
            fExpectInt("heal: avghit 30, hp 28 -> 29", UWLevelExit.HealedHitPoints(28, 30), 29);
            fExpectInt("heal: at the average nothing", UWLevelExit.HealedHitPoints(20, 20), 20);
            fExpectInt("heal: above the average nothing", UWLevelExit.HealedHitPoints(25, 20), 25);
            fExpectInt("mood: hostile counts -1", UWLevelExit.MoodOf(0), -1);
            fExpectInt("mood: friendly counts +1", UWLevelExit.MoodOf(3), 1);
            fExpectInt("mood: upset and mellow count nothing", UWLevelExit.MoodOf(1) + UWLevelExit.MoodOf(2), 0);
            fExpectInt("mood: clamped at hostile", UWLevelExit.ShiftedAttitude(1, -3), 0);
            fExpectInt("mood: clamped at friendly", UWLevelExit.ShiftedAttitude(2, 4), 3);

            UWLevel lOLevel = pOData != null && pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;

            if (lOLevel == null)
            {
                fExpectBool("time pass: level 1 loaded", false, true);
                return;
            }

            UWTileQueries lOQueries = new UWTileQueries(lOLevel, pOData);
            UWWorldEdit lOEdit = new UWWorldEdit(lOQueries);

            if (!fFindMovableCreature(lOQueries, out UWNpc lONpc, out UWTilePos lOAway, out UWObjectClassProperties.Critter lOStats))
            {
                fExpectBool("time pass: a creature with a free home post on level 1", false, true);
                return;
            }

            UWNpc lOTemporary = fFindOtherCreature(lOQueries, lONpc);

            UWCritterRecord lORecord = new UWCritterRecord(lONpc);
            int liHomeX = lORecord.HomeX;
            int liHomeY = lORecord.HomeY;

            // Pushed off its post, wounded, an ally that had seen the player and made a stand.
            lOEdit.Move(lONpc, lOAway.X, lOAway.Y);
            lORecord.TileX = lOAway.X;
            lORecord.TileY = lOAway.Y;
            lORecord.HitPoints = 1;
            lORecord.IsAlly = true;
            lORecord.Fed = true;
            lORecord.TargetConfirmed = true;
            lORecord.HeardSomething = true;
            lORecord.MadeStand = true;
            lORecord.Relentless = true;
            lORecord.SpellSlot = 2;
            lORecord.Goal = UWNpc.GoalWantToTalk;

            if (lOTemporary != null)
                lOTemporary.NPCSpawned = true;

            RecordingTimeHost lOHost = new RecordingTimeHost(lOEdit);

            UWLevelExit.PassTime(lOEdit, lOHost, out int liRemoved);

            // A fresh record over the same bytes: what a creature built on re-entry reads.
            UWCritterRecord lOAfter = new UWCritterRecord(lONpc);

            fExpectBool("time pass: sent home in the record's word 0x16", lOAfter.TileX == liHomeX && lOAfter.TileY == liHomeY, true);
            fExpectBool("time pass: ... and in the home tile's list", lOLevel.GetTile(liHomeX, liHomeY).ObjectsInTile.Contains(lONpc), true);
            fExpectBool("time pass: ... and out of the other's", lOLevel.GetTile(lOAway.X, lOAway.Y).ObjectsInTile.Contains(lONpc), false);
            fExpectInt("time pass: healed half way in the record", lOAfter.HitPoints, (1 + lOStats.Vitality) / 2);
            fExpectInt("time pass: ... and in the data the save writes", lONpc.HitPoints, (1 + lOStats.Vitality) / 2);
            fExpectBool("time pass: the ally bit cleared in the record", lOAfter.IsAlly, false);
            fExpectBool("time pass: ... and in the data", lONpc.NPCIsAlly, false);
            fExpectBool("time pass: fed cleared", lOAfter.Fed, false);
            fExpectBool("time pass: bits 0, 1, 4, 5 cleared",
                lOAfter.TargetConfirmed || lOAfter.HeardSomething || lOAfter.MadeStand || lOAfter.Relentless, false);
            fExpectInt("time pass: the spell slot (bits 2-3) kept", lOAfter.SpellSlot, 2);
            fExpectInt("time pass: the goal kept (goal 10 too)", lOAfter.Goal, UWNpc.GoalWantToTalk);
            fExpectInt("time pass: the record's attitude is the data's", lOAfter.Attitude, lONpc.NPCAttitude);
            fExpectBool("time pass: the host was told", lOHost.Changed.Contains(lONpc), true);

            if (lOTemporary != null)
            {
                fExpectInt("time pass: the temporary creature removed", liRemoved, 1);
                fExpectBool("time pass: ... out of every tile list", lOQueries.FindTileHolding(lOTemporary) == null, true);
                fExpectBool("time pass: ... through the host", lOHost.Removed.Contains(lOTemporary), true);
            }
        }

        /// <summary>yearly_checkup (CRITTIME.C) from the 24th player tick (PLAYTIME.C).</summary>
        private static void fCheckYearlyCheckup(DataImport pOData)
        {
            Check.Group("Five-minute checkup (CRITTIME.C yearly_checkup)");

            FakeVitalsHost lOVitalsHost = new FakeVitalsHost();
            UWPlayerVitals lOVitals = lOVitalsHost.Watch(new UWPlayerVitals(lOVitalsHost));

            lOVitals.Initialise(null, UWPlayerVitals.StartValues.Default);

            for (int liTick = 0; liTick < UWPlayerTick.FiveMinuteTicks - 1; liTick++)
                lOVitals.RunGameTick(0, 0);

            fExpectInt("checkup: nothing for 23 ticks", lOVitalsHost.FiveMinuteBlocks, 0);
            lOVitals.RunGameTick(0, 0);
            fExpectInt("checkup: the level's share on the 24th", lOVitalsHost.FiveMinuteBlocks, 1);

            UWLevel lOLevel = pOData != null && pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;

            if (lOLevel == null)
            {
                fExpectBool("checkup: level 1 loaded", false, true);
                return;
            }

            UWTileQueries lOQueries = new UWTileQueries(lOLevel, pOData);
            UWNpc lONpc = fFindOtherCreature(lOQueries, null);
            UWCritterRecord lORecord = new UWCritterRecord(lONpc);

            try
            {
                // rand() % 2 == 1 fed; rand() % 4 == 1 keeps the ally bit.
                lORecord.IsAlly = true;
                lORecord.Fed = false;
                UWRandom.Use(new ConstantRandom(1));
                UWLevelExit.YearlyCheckup(lOQueries);
                fExpectBool("checkup: a 1 feeds it", lORecord.Fed, true);
                fExpectBool("checkup: ... and a 1 of four keeps the ally", lORecord.IsAlly, true);

                UWRandom.Use(new ConstantRandom(3));
                UWLevelExit.YearlyCheckup(lOQueries);
                fExpectBool("checkup: a 3 of four clears the ally bit", lORecord.IsAlly, false);
                fExpectBool("checkup: ... in the data the save writes too", lONpc.NPCIsAlly, false);

                UWRandom.Use(new ConstantRandom(0));
                UWLevelExit.YearlyCheckup(lOQueries);
                fExpectBool("checkup: a 0 leaves it unfed", lORecord.Fed, false);

                // The same block's wandering monsters: (rand() & 3) == 0.
                fExpectBool("wandering monsters: a 0 of four calls them", UWRespawnRules.RollOpportunity(), true);
                UWRandom.Use(new ConstantRandom(1));
                fExpectBool("wandering monsters: ... a 1 does not", UWRespawnRules.RollOpportunity(), false);
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>A25: a dying creature captured before a save or a level change stays in its
        /// tile list with 0 hit points (critter_ai keeps it until frame 3).</summary>
        private static void fCheckDyingCreatureKept(DataImport pOData)
        {
            Check.Group("A dying creature stays in the data (AI.C critter_ai)");

            UWLevel lOLevel = pOData != null && pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;

            if (lOLevel == null)
            {
                fExpectBool("dying: level 1 loaded", false, true);
                return;
            }

            UWTileQueries lOQueries = new UWTileQueries(lOLevel, pOData);
            UWNpc lONpc = fFindOtherCreature(lOQueries, null);
            UWTile lOTile = lOQueries.FindTileHolding(lONpc);

            UWWorldCapture.CaptureCritter(lOQueries, new UWCritterRecord(lONpc), true, 20, false);

            fExpectBool("dying: still in its tile list", lOTile != null && lOTile.ObjectsInTile.Contains(lONpc), true);
            fExpectInt("dying: with 0 hit points", lONpc.HitPoints, 0);
        }

        /// <summary>damage_critter (AI.C) with from 0 - the ward rune, a damage trap, the lava:
        /// the tally grows, but no attacker, no kin alarm, no music; a kill is nobody's.</summary>
        private static void fCheckDamageWithoutAttacker()
        {
            Check.Group("Damage without an attacker (AI.C damage_critter, who 0)");

            FakeCritterHost lOHost = new FakeCritterHost();
            UWCritterRecord lORecord = fMakeRecord(5, 5, 0, 2);
            UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(3, 20, 5, 4), 5, lOHost);

            lOHost.ClockValue = 1000;
            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lOBrain.OnDamaged(lOHost, 0, 3);

            fExpectInt("no attacker: the tally grows", lORecord.DamageTaken, 3);
            fExpectInt("no attacker: ... the hit points fall", lORecord.HitPoints, 17);
            fExpectInt("no attacker: no attacker recorded", lORecord.Attacker, 0);
            fExpectInt("no attacker: no kin alarm", lOHost.Alarm.Kind, UWCritterAlarm.NoKind);
            fExpectBool("no attacker: no combat music", lOHost.HasEvent("music"), false);

            lOBrain.OnDamaged(lOHost, 0, 30);
            fExpectBool("no attacker: it can still die", lOHost.HasEvent("death"), true);
        }

        /// <summary>critter_ai (AI.C:1133-1135): goals 3 and 11 skip the animation machine, so a
        /// flutterer that dies never reaches frame 3 - no removal, however long it runs; a further
        /// blow starts no second death (2026-10-17, audit round 2 B11, per user in the original).
        /// A goal-8 creature beside it finishes as before.</summary>
        private static void fCheckEndlessDying()
        {
            Check.Group("Endless dying of goals 3 and 11 (AI.C critter_ai)");

            fExpectBool("goal 3 skips the animation machine", UWCritterRules.SkipsAnimationMachine(3), true);
            fExpectBool("goal 11 too", UWCritterRules.SkipsAnimationMachine(11), true);
            fExpectBool("goal 8 does not", UWCritterRules.SkipsAnimationMachine(8), false);

            foreach (int liGoal in new[] { 11, 8 })
            {
                FakeCritterHost lOHost = new FakeCritterHost { DefaultRoll = 1 };
                UWCritterRecord lORecord = fMakeRecord(5, 5, liGoal, 2, -1, -1, 3);
                UWCritterBrain lOBrain = fMakeBrain(lORecord, fMakeRow(), 5, lOHost);

                lOHost.PlacePlayer((9 << 3) + 4, (9 << 3) + 4);
                lOBrain.OnDamaged(lOHost, 0, 10);

                bool lbRemoved = false;

                for (int liAt = 0; liAt < 20 && !lbRemoved; liAt++)
                    lbRemoved = lOBrain.Update(lOHost).Removed;

                string lsWho = "goal " + liGoal;

                fExpectInt(lsWho + ": dying with 0 hit points", lORecord.HitPoints, 0);
                fExpectBool(lsWho + ": removed within 20 updates", lbRemoved, liGoal == 8);

                if (liGoal != 11)
                    continue;

                fExpectInt(lsWho + ": still the death animation", lORecord.Animation, UWCritterBrain.AnimDying);
                fExpectInt(lsWho + ": goal unchanged", lORecord.Goal, 11);

                lOHost.Events.Clear();
                lOBrain.OnDamaged(lOHost, 0, 5);
                fExpectBool(lsWho + ": a further blow starts no second death", lOHost.HasEvent("death"), false);
            }
        }

        /// <summary>CRITTIME.C set_creatures_to/from_saved_game (2026-10-17, audit round 2 C7):
        /// the kin alarm goes into PLAYER.DAT 0xBA-0xC1 with its time as a game clock value and
        /// comes back on loading as old as it was, against a critter clock that restarted at 0.</summary>
        private static void fCheckKinAlarmSaved()
        {
            Check.Group("Kin alarm in the save (CRITTIME.C set_creatures_to/from_saved_game)");

            string lsFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "UWSelfCheck-alarm-" + Guid.NewGuid().ToString("N"));

            try
            {
                System.IO.Directory.CreateDirectory(lsFolder);

                byte[] lyPlain = UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter { Name = "Selfcheck" });

                fExpectInt("fresh character: no victim", UWPlayerData.FromPlain(lyPlain).AlarmIndex, 0);
                fExpectInt("fresh character: kind 0xFF", UWPlayerData.FromPlain(lyPlain).AlarmKind, UWCritterAlarm.NoKind);

                System.IO.File.WriteAllBytes(System.IO.Path.Combine(lsFolder, "PLAYER.DAT"), UWPlayerData.Encrypt(lyPlain, 0x2A));

                // The blow 0x80 ticks before saving, on a critter clock at 5000 and a game clock at 90000.
                UWCritterAlarm lOAlarm = new UWCritterAlarm();

                lOAlarm.Record(37, 4, 12, 50, 2, 5000 - 0x80);

                UWPlayerData.SavedState lOState = new UWPlayerData.SavedState { ClockValue = 90000 };

                lOAlarm.SaveInto(lOState, 5000, 90000);

                string lsError = UWPlayerData.WritePosition(lsFolder, 1, 32 << 8, 2 << 8, 96 * 8, 0, lOState);

                fExpectBool("the writer accepts the scratch save", lsError == null, true);

                UWPlayerData lOLoaded = new UWPlayerData(lsFolder);

                fExpectInt("0xBA: the victim", lOLoaded.AlarmIndex, 37);
                fExpectInt("0xBB: its kind", lOLoaded.AlarmKind, 4);
                fExpectInt("0xBC: the game clock at the blow", (int)lOLoaded.AlarmTime, 90000 - 0x80);
                fExpectInt("0xC0: the tile x", lOLoaded.AlarmTileX, 12);
                fExpectInt("0xC1: the tile y", lOLoaded.AlarmTileY, 50);

                // Loaded on a fresh critter clock: the same age, so fresh while under 0x180 more ticks.
                UWCritterAlarm lORestored = new UWCritterAlarm();

                lORestored.RestoreFrom(lOLoaded, 0, 3);

                fExpectInt("restored: the victim", lORestored.Index, 37);
                fExpectInt("restored: the floor level from the caller", lORestored.FloorLevel, 3);
                fExpectBool("restored: fresh right after loading", lORestored.IsFresh(0), true);
                fExpectBool("restored: fresh up to the window's end", lORestored.IsFresh(0x17F), true);
                fExpectBool("restored: stale after it", lORestored.IsFresh(0x180), false);

                // A level change before saving: victim and kind cleared, time and tile left.
                UWPlayerData.SavedState lOCleared = new UWPlayerData.SavedState { ClockValue = 95000 };

                new UWCritterAlarm().SaveInto(lOCleared, 0, 95000);
                UWPlayerData.WritePosition(lsFolder, 1, 32 << 8, 2 << 8, 96 * 8, 0, lOCleared);
                lOLoaded = new UWPlayerData(lsFolder);

                fExpectInt("level change: no victim", lOLoaded.AlarmIndex, 0);
                fExpectInt("level change: kind 0xFF", lOLoaded.AlarmKind, UWCritterAlarm.NoKind);
                fExpectInt("level change: the stale time stays", (int)lOLoaded.AlarmTime, 90000 - 0x80);

                lORestored.RestoreFrom(lOLoaded, 0, 3);
                fExpectBool("level change: nothing restored", lORestored.IsFresh(0), false);
            }
            finally
            {
                try
                {
                    System.IO.Directory.Delete(lsFolder, true);
                }
                catch (System.IO.IOException)
                {
                }
            }
        }

        /// <summary>A creature with stats, not a loner, below whose post an open tile with no
        /// other creature lies, and an open tile next to the post to push it to.</summary>
        private static bool fFindMovableCreature(UWTileQueries pOQueries, out UWNpc pONpc, out UWTilePos pOAway,
            out UWObjectClassProperties.Critter pOStats)
        {
            pONpc = null;
            pOAway = default;
            pOStats = default;

            foreach (UWTile lOTile in pOQueries.Level.TileData)
            {
                if (lOTile == null || lOTile.ObjectsInTile == null)
                    continue;

                foreach (UWObject lOObject in lOTile.ObjectsInTile)
                {
                    UWNpc lONpc = lOObject as UWNpc;

                    if (lONpc == null || lONpc.NPCSpawned
                        || !pOQueries.Data.ObjectClassProperties.TryGetCritter(lONpc.ID, out UWObjectClassProperties.Critter lOStats)
                        || lOStats.Vitality < 4)
                        continue;

                    UWCritterRecord lORecord = new UWCritterRecord(lONpc);
                    UWTile lOHome = pOQueries.GetTile(lORecord.HomeX, lORecord.HomeY);

                    if (lORecord.AttitudeLocked || lOHome == null || lOHome.TileType != UWTile.TileTypeEnum.open
                        || lORecord.TileX != lORecord.HomeX || lORecord.TileY != lORecord.HomeY)
                        continue;

                    bool lbAlone = true;

                    foreach (UWObject lOOther in lOHome.ObjectsInTile)
                        lbAlone &= lOOther == lONpc || !(lOOther is UWNpc);

                    UWTile lONext = pOQueries.GetTile(lORecord.HomeX + 1, lORecord.HomeY);

                    if (!lbAlone || lONext == null || lONext.TileType != UWTile.TileTypeEnum.open)
                        continue;

                    pONpc = lONpc;
                    pOAway = new UWTilePos(lORecord.HomeX + 1, lORecord.HomeY);
                    pOStats = lOStats;

                    return true;
                }
            }

            return false;
        }

        private static UWNpc fFindOtherCreature(UWTileQueries pOQueries, UWNpc pONot)
        {
            foreach (UWTile lOTile in pOQueries.Level.TileData)
            {
                if (lOTile == null || lOTile.ObjectsInTile == null)
                    continue;

                foreach (UWObject lOObject in lOTile.ObjectsInTile)
                {
                    if (lOObject is UWNpc lONpc && lONpc != pONot)
                        return lONpc;
                }
            }

            return null;
        }
    }
}
