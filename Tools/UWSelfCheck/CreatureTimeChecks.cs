using System;
using System.Collections.Generic;
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
    public static partial class Program
    {
        /// <summary>A host that records what the pass asked of the scene.</summary>
        private sealed class RecordingTimeHost : IUWCreatureTimeHost
        {
            public readonly UWWorldEdit Edit;

            public readonly List<UWNpc> Removed = new List<UWNpc>();

            public readonly List<UWNpc> Changed = new List<UWNpc>();

            public RecordingTimeHost(UWWorldEdit pOEdit)
            {
                Edit = pOEdit;
            }

            public void RemoveCreature(UWNpc pONpc)
            {
                Removed.Add(pONpc);
                UWLevelExit.RemoveFromLevel(Edit, pONpc);
            }

            public void CreatureChanged(UWNpc pONpc)
            {
                Changed.Add(pONpc);
            }
        }

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
            UWPlayerVitals lOVitals = new UWPlayerVitals(lOVitalsHost);

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

            UWWorldCapture.CaptureCritter(lOQueries, lONpc, true, true, 0, 20, 0, 5, false, 0f, 0f, 0f);

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
            UWCritterBrain lOBrain = new UWCritterBrain(lORecord, fMakeRow(3, 20, 5, 4), 5);

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
