using System;
using System.Collections.Generic;
using System.Reflection;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.13 (2026-10-19): ICritterHost shrunk to the questions about the world around
    /// one creature and the events out of its mind. Pinned: the host's member count; the session as
    /// the brain's services (ICritterServices: the random source, the clock, the kin alarm and the
    /// path slots, read anew at every call so a level build's fresh clock and alarm reach it); a
    /// brain on the session draws from the session's source and never from its host; the goal
    /// trace as the brain's event, which may have no listener; the injected own height and
    /// ammunition velocities, the actor's from COMOBJ and OBJECTS.DAT (and the old fallbacks without
    /// a world).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckCreatureHost(DataImport pOData)
        {
            Check.Group("Creature host (cleanup step 4.13)");

            fCheckHostMembers();
            fCheckSessionServices();
            fCheckBrainOnSession();
            fCheckGoalEvent();
            fCheckInjectedConstants(pOData);
        }

        /// <summary>25 members: the level queries, the other objects, the movement, the doors and
        /// the events (32 until the step).</summary>
        private static void fCheckHostMembers()
        {
            int liCount = 0;

            foreach (MemberInfo lOMember in typeof(ICritterHost).GetMembers())
            {
                if (lOMember is PropertyInfo || (lOMember is MethodInfo lOMethod && !lOMethod.IsSpecialName))
                    liCount++;
            }

            fExpectInt("host: ICritterHost has 25 members", liCount, 25);
            fExpectBool("host: no random, clock, alarm, path slots, height, velocity or goal trace on it",
                typeof(ICritterHost).GetMember("Random").Length == 0 && typeof(ICritterHost).GetMember("Clock").Length == 0
                && typeof(ICritterHost).GetMember("Alarm").Length == 0 && typeof(ICritterHost).GetMember("PathSlots").Length == 0
                && typeof(ICritterHost).GetMember("OwnObjectHeight").Length == 0
                && typeof(ICritterHost).GetMember("MissileVelocity").Length == 0
                && typeof(ICritterHost).GetMember("OnGoalChanged").Length == 0, true);
        }

        /// <summary>The session's four services, read anew after ResetCritterServices.</summary>
        private static void fCheckSessionServices()
        {
            UWGameSession lOSession = new UWGameSession(null, 5);
            ICritterServices lIServices = lOSession;

            fExpectBool("session: the services are the session's own",
                ReferenceEquals(lIServices.Random, lOSession.Random) && ReferenceEquals(lIServices.Alarm, lOSession.CritterAlarm)
                && ReferenceEquals(lIServices.PathSlots, lOSession.CritterPaths) && lIServices.Clock == lOSession.CritterClock.Clock, true);

            lOSession.CritterClock.Advance(64, false);
            fExpectBool("session: the clock as it runs", lIServices.Clock == lOSession.CritterClock.Clock && lIServices.Clock > 0, true);

            UWCritterAlarm lOOld = lIServices.Alarm;

            lOSession.ResetCritterServices();
            fExpectBool("session: a level build's fresh alarm and clock reach the services",
                !ReferenceEquals(lIServices.Alarm, lOOld) && ReferenceEquals(lIServices.Alarm, lOSession.CritterAlarm)
                && lIServices.Clock == 0, true);
        }

        /// <summary>A brain on the session draws from the session's source (a scripted one in front
        /// of it here), its host's own queue untouched.</summary>
        private static void fCheckBrainOnSession()
        {
            UWGameSession lOBefore = UWGameSession.Current;
            UWGameSession lOSession = new UWGameSession(null, 9);
            ScriptedRolls lOScript = new ScriptedRolls();
            FakeCritterHost lOHost = new FakeCritterHost();

            UWGameSession.MakeCurrent(lOSession);
            lOSession.Random.Use(lOScript);

            try
            {
                UWCritterRecord lORecord = fMakeRecord(5, 5, 5, 0);
                UWCritterBrain lOBrain = new UWCritterBrain(lORecord, fMakeRow(), 5, lOSession, lOHost.OwnObjectHeight,
                    lOHost.MissileVelocities);

                // At 8 eighths the 1-in-4 roll starts the swing (fCheckMeleeRange): the session's
                // script answers 0, the host's queue would answer 1.
                lOHost.Rolls.Enqueue(1);
                lOHost.PlacePlayer(44 + 8, 44);
                lOBrain.Update(lOHost);

                fExpectInt("services: the session's script answered the swing roll (animation 1)", lORecord.Animation, 1);
                fExpectInt("services: the host's own queue is not drawn", lOHost.Rolls.Count, 1);
            }
            finally
            {
                lOSession.Random.Use(null);
                UWGameSession.MakeCurrent(lOBefore);
            }
        }

        /// <summary>The goal trace as the brain's event: raised with old and new goal and the
        /// target, and a brain without a listener changes its goal all the same.</summary>
        private static void fCheckGoalEvent()
        {
            FakeCritterHost lOHost = new FakeCritterHost { ClockValue = 1000 };
            UWCritterRecord lOVictim = fMakeRecord(5, 5, 0, 2);
            UWCritterBrain lOVictimBrain = fMakeBrain(lOVictim, fMakeRow(3, 20, 5, 4), 5, lOHost);
            List<string> lOHeard = new List<string>();

            lOHost.PlacePlayer((6 << 3) + 4, 44);
            lOVictimBrain.OnDamaged(lOHost, 1, 2);

            // The kin alarm of fCheckKinAlarm: kin three tiles away joins with goal 5.
            UWCritterRecord lOKin = fMakeRecord(8, 5, 0, 2);
            UWCritterBrain lOKinBrain = new UWCritterBrain(lOKin, fMakeRow(3, 20, 5, 4), 9, lOHost, lOHost.OwnObjectHeight,
                lOHost.MissileVelocities);

            lOKinBrain.GoalChanged += (piOld, piNew, piTarget) => lOHeard.Add(piOld + ">" + piNew + " at " + piTarget);
            lOHost.ClockValue = 1000 + 0x200;
            lOHost.DefaultRoll = 1;
            lOKinBrain.Update(lOHost);

            fExpectBool("goal event: the alarm's goal 5 is raised with its target",
                lOHeard.Count > 0 && lOHeard[0].StartsWith("0>5", StringComparison.Ordinal), true);
            fExpectBool("goal event: nothing of it reaches the host", lOHost.HasEvent("goal"), false);

            UWCritterRecord lOQuiet = fMakeRecord(8, 5, 0, 2);
            UWCritterBrain lOQuietBrain = new UWCritterBrain(lOQuiet, fMakeRow(3, 20, 5, 4), 9, lOHost, lOHost.OwnObjectHeight,
                lOHost.MissileVelocities);

            lOQuietBrain.Update(lOHost);
            fExpectInt("goal event: without a listener the goal changes all the same", lOQuiet.Goal, 5);
        }

        /// <summary>The own height and the velocities the actor hands its brain: COMOBJ's and
        /// OBJECTS.DAT's, the old fallbacks (0x20 and 15) without a world; the brain takes entry n's
        /// low four bits.</summary>
        private static void fCheckInjectedConstants(DataImport pOData)
        {
            int[] liTable = new int[UWObjectProperties.RangedObjectCount];

            for (int liAt = 0; liAt < liTable.Length; liAt++)
                liTable[liAt] = 30 + liAt;

            UWCritterBrain lOBrain = new UWCritterBrain(fMakeRecord(5, 5, 0, 2), fMakeRow(), 5, new FakeCritterHost(), 12, liTable);

            fExpectBool("constants: the brain's own height and entry n of its table, n's low four bits",
                lOBrain.OwnObjectHeight == 12 && lOBrain.MissileVelocity(3) == 33 && lOBrain.MissileVelocity(0x13) == 33, true);

            UWCreatureActor lONoWorld = new UWCreatureActor(null, fMakeRow(), true, null, new FakeCreatureView(), null,
                UWGameSession.Current);
            bool lbFallback = lONoWorld.Brain.OwnObjectHeight == 0x20;

            for (int liAt = 0; liAt < UWObjectProperties.RangedObjectCount; liAt++)
                lbFallback &= lONoWorld.Brain.MissileVelocity(liAt) == 15;

            fExpectBool("constants: without a world height 0x20 and velocity 15 (the old fallbacks)", lbFallback, true);

            if (pOData == null || pOData.Levels == null || pOData.Levels.Count == 0 || pOData.ObjectProperties == null
                || pOData.CommonObjectProperties == null || pOData.ObjectClassProperties == null)
            {
                fFail("constants: the actor's", "no data");

                return;
            }

            UWLevel lOLevel = pOData.Levels[0];
            UWNpc lONpc = null;
            UWObjectClassProperties.Critter lORow = default;

            for (int liIndex = 2; liIndex < lOLevel.Masterlist.Count && lONpc == null; liIndex++)
            {
                if (lOLevel.Masterlist[liIndex] is UWNpc lOCandidate && UWObjectIds.IsCreature(lOCandidate.ID)
                    && pOData.ObjectClassProperties.TryGetCritter(lOCandidate.ID, out lORow))
                    lONpc = lOCandidate;
            }

            if (lONpc == null)
            {
                fFail("constants: the actor's", "no creature on level 1");

                return;
            }

            UWCreatureActor lOActor = new UWCreatureActor(lONpc, lORow, true, fActorWorld(pOData, lOLevel), new FakeCreatureView(), null,
                UWGameSession.Current);
            bool lbData = pOData.CommonObjectProperties.TryGet(lONpc.ID, out UWCommonObjectProperties.Entry lOOwn)
                && lOActor.Brain.OwnObjectHeight == lOOwn.Height;

            for (int liAt = 0; liAt < UWObjectProperties.RangedObjectCount; liAt++)
                lbData &= lOActor.Brain.MissileVelocity(liAt)
                    == pOData.ObjectProperties.GetRangedSpeed(UWObjectMechanics.FirstAmmunitionObjectId + liAt);

            fExpectBool("constants: the actor's from COMOBJ and the ammunition table", lbData, true);
        }
    }
}
