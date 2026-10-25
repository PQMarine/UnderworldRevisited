using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 3.4 (2026-10-17): the world's sequencing on the game's own clock - the scheduler
    /// (order, pausing, the conversation wait, the per-frame steps, the player ticks), the trigger
    /// pacer (the same hit rate at 60 and 144 frames a second) and the launch into the void (the
    /// sequence's order, its step timing in both modes, the guard against a second launch).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckWorldScheduler()
        {
            Check.Group("World scheduler (UWWorldScheduler: order, pausing, conversation wait, player ticks)");

            // ORDER: by due tick, equal ticks in the order asked for.
            UWWorldScheduler lOSchedule = new UWWorldScheduler();
            List<string> lsRan = new List<string>();

            lOSchedule.After(2, () => lsRan.Add("A"));
            lOSchedule.After(1, () => lsRan.Add("B"));
            lOSchedule.After(1, () => lsRan.Add("C"));
            lOSchedule.AfterTicks(UWWorldScheduler.TicksPerUnit, () => lsRan.Add("D"));
            lOSchedule.Advance(UWWorldScheduler.TicksPerUnit - 1, true);
            Check.Str("scheduler: nothing before its tick", string.Concat(lsRan), "");
            lOSchedule.Advance(1, true);
            Check.Str("scheduler: equal ticks in the order asked for", string.Concat(lsRan), "BCD");
            lOSchedule.Advance(3 * UWWorldScheduler.TicksPerUnit, true);
            Check.Str("scheduler: then the later one", string.Concat(lsRan), "BCDA");
            Check.Int("scheduler: the clock counts ticks", (int)lOSchedule.Now, 4 * UWWorldScheduler.TicksPerUnit);
            Check.Int("scheduler: and units of 16", (int)lOSchedule.NowUnits, 4);

            // One advance runs several due actions earliest first, even when asked for the other way.
            lsRan.Clear();
            lOSchedule.After(3, () => lsRan.Add("late"));
            lOSchedule.After(1, () => lsRan.Add("early"));
            lOSchedule.Advance(10 * UWWorldScheduler.TicksPerUnit, true);
            Check.Str("scheduler: a long frame runs them earliest first", string.Join(",", lsRan), "early,late");

            // PAUSING: a held clock is no running frame - no time passes, nothing runs.
            lsRan.Clear();
            long llBefore = lOSchedule.Now;

            lOSchedule.After(0, () => lsRan.Add("x"));
            lOSchedule.Advance(500, false);
            Check.Bool("scheduler: a held frame runs nothing", lsRan.Count == 0, true);
            Check.Bool("scheduler: and passes no time", lOSchedule.Now == llBefore, true);
            lOSchedule.Advance(0, true);
            Check.Str("scheduler: After(0) runs on the next running advance", string.Concat(lsRan), "x");

            // What an action asks for waits for a later advance, even After(0).
            lsRan.Clear();
            lOSchedule.After(0, () =>
            {
                lsRan.Add("first");
                lOSchedule.After(0, () => lsRan.Add("second"));
            });
            lOSchedule.Advance(1, true);
            Check.Str("scheduler: asked for while running - not in the same advance", string.Join(",", lsRan), "first");
            lOSchedule.Advance(1, true);
            Check.Str("scheduler: but on the next", string.Join(",", lsRan), "first,second");

            // AFTER THE CONVERSATION. Opened at once (the usual case: Begin is synchronous): the frames
            // it is open are no running advances; the action comes on the first running one.
            lsRan.Clear();
            lOSchedule.AfterConversation(() => lsRan.Add("burial"));
            lOSchedule.Advance(0, false);
            lOSchedule.Advance(0, false);
            Check.Bool("conversation wait: nothing while it is open", lsRan.Count == 0, true);
            lOSchedule.Advance(4, true);
            Check.Str("conversation wait: the first running frame after it", string.Concat(lsRan), "burial");

            // Not yet open: the first frame only arms the wait; a conversation opening on the next
            // frame is still waited for.
            lsRan.Clear();
            lOSchedule.AfterConversation(() => lsRan.Add("burial"));
            lOSchedule.Advance(4, true);
            Check.Bool("conversation wait: not on the frame it was asked for", lsRan.Count == 0, true);
            lOSchedule.Advance(0, false);
            lOSchedule.Advance(0, false);
            Check.Bool("conversation wait: a conversation opened a frame later holds it", lsRan.Count == 0, true);
            lOSchedule.Advance(4, true);
            Check.Str("conversation wait: then it runs", string.Concat(lsRan), "burial");

            // Never opened: it runs on the second running frame, as the coroutine looked after one.
            lsRan.Clear();
            lOSchedule.AfterConversation(() => lsRan.Add("burial"));
            lOSchedule.Advance(4, true);
            lOSchedule.Advance(4, true);
            Check.Str("conversation wait: without a conversation one frame later", string.Concat(lsRan), "burial");
            Check.Int("scheduler: nothing left pending", lOSchedule.Pending, 0);

            // PER-FRAME STEPS: every running advance, held frames skipped, until the step says stop.
            int liCalls = 0;

            lOSchedule.EveryAdvance(() => ++liCalls < 3);
            lOSchedule.Advance(0, true);
            lOSchedule.Advance(0, false);
            lOSchedule.Advance(1, true);
            Check.Int("per-frame step: running frames only, a 0-tick frame included", liCalls, 2);
            lOSchedule.Advance(1, true);
            lOSchedule.Advance(1, true);
            Check.Int("per-frame step: stops when it returns false", liCalls, 3);
            Check.Int("per-frame step: and is gone", lOSchedule.RunningSteps, 0);

            lOSchedule.After(1, () => lsRan.Add("dropped"));
            lOSchedule.Clear();
            lOSchedule.Advance(100, true);
            Check.Bool("scheduler: Clear drops everything", lOSchedule.Pending == 0 && !lsRan.Contains("dropped"), true);

            // One per session.
            UWGameSession lOFirst = new UWGameSession(null, 1);
            UWGameSession lOSecond = new UWGameSession(null, 1);

            Check.Bool("scheduler: each session has its own", !ReferenceEquals(lOFirst.Schedule, lOSecond.Schedule)
                && !ReferenceEquals(lOFirst.Endgame, lOSecond.Endgame), true);
        }

        private static void fCheckTriggerPacer()
        {
            Check.Group("Trigger pacer (UWTriggerPacer: once per clock unit, the same rate at any frame rate)");

            Check.Int("pacer: the spacing is one unit, 16 PIT ticks (16/256 s)", UWTriggerPacer.SpacingTicks, 16);
            Check.Bool("pacer: not again within the unit", UWTriggerPacer.IsSpaced(0, 15), false);
            Check.Bool("pacer: again in the next unit", UWTriggerPacer.IsSpaced(0, 16), true);
            Check.Bool("pacer: the unit, not 16 ticks since", UWTriggerPacer.IsSpaced(15, 16), true);
            Check.Bool("pacer: within the later unit", UWTriggerPacer.IsSpaced(16, 31), false);
            Check.Bool("pacer: a clock that started afresh", UWTriggerPacer.IsSpaced(100, 5), true);

            // THE HIT RATE: a mover standing in a damage trigger's reach for ten seconds, the trigger
            // queued every frame. Frames feed the clock as UWCritterDriver does (whole PIT ticks of
            // the frame time, the rest carried).
            int li60 = fPacedFirings(60.0, 10.0);
            int li144 = fPacedFirings(144.0, 10.0);
            int li30 = fPacedFirings(30.0, 10.0);

            Check.Int("pacer: 60 fps fire 16 a second", li60, 160);
            Check.Int("pacer: 144 fps the same", li144, 160);
            Check.Int("pacer: 30 fps the same", li30, 160);
            Check.Int("pacer: below 16 fps once a frame", fPacedFirings(10.0, 10.0), 100);

            // Keys: the trigger by reference and the mover.
            UWTriggerPacer lOPacer = new UWTriggerPacer(2);
            object lOTrigger = new object();
            object lOOther = new object();

            lOPacer.MarkFired(lOTrigger, 5, 0);
            Check.Bool("pacer: the same mover waits", lOPacer.IsReady(lOTrigger, 5, 8), false);
            Check.Bool("pacer: another mover does not", lOPacer.IsReady(lOTrigger, 6, 8), true);
            Check.Bool("pacer: another trigger does not", lOPacer.IsReady(lOOther, 5, 8), true);

            lOPacer.MarkFired(lOOther, 5, 0);
            lOPacer.MarkFired(lOOther, 6, 0);
            lOPacer.BeginBatch();
            Check.Int("pacer: a batch forgets above its limit", lOPacer.Count, 0);
        }

        /// <summary>How often a trigger queued every frame fires in pdSeconds at pdFps.</summary>
        private static int fPacedFirings(double pdFps, double pdSeconds)
        {
            UWWorldScheduler lOClock = new UWWorldScheduler();
            UWTriggerPacer lOPacer = new UWTriggerPacer(64);
            object lOTrigger = new object();
            double ldRemainder = 0.0;
            int liFrames = (int)Math.Round(pdFps * pdSeconds);
            int liFired = 0;

            for (int liFrame = 0; liFrame < liFrames; liFrame++)
            {
                if (lOPacer.IsReady(lOTrigger, 1, lOClock.Now))
                {
                    lOPacer.MarkFired(lOTrigger, 1, lOClock.Now);
                    liFired++;
                }

                ldRemainder += UWPlayerTick.PitTicksPerSecond / pdFps;

                int liTicks = (int)Math.Floor(ldRemainder + 1e-9);

                ldRemainder -= liTicks;
                lOClock.Advance(liTicks, true);
            }

            return liFired;
        }

        private static void fCheckEndgameSequence()
        {
            Check.Group("Endgame sequence (UWEndgameSequence, UWMoongateFlight: the launch into the void)");

            // THE ORDER at the start, then the journey and the last message. The rending sound is
            // the impact's, not the launch's, since the victory check starts it (2026-10-18).
            int liSavedCounter = UWQuestFlags.Get(UWEndgameRules.TalismanQuestFlag);

            UWQuestFlags.Set(UWEndgameRules.TalismanQuestFlag, 0);

            UWWorldScheduler lOSchedule = new UWWorldScheduler();
            UWEndgameSequence lOSequence = new UWEndgameSequence();
            LoggingEndgameHost lOHost = new LoggingEndgameHost { Clock = lOSchedule };
            const float lfStep = 1f / 15f;

            Check.Bool("launch: starts", lOSequence.Launch(lOHost.World, lOHost, lOSchedule, lfStep), true);
            Check.Str("launch: at the start", string.Join("; ", lOHost.Log),
                "spawn 15A 32/32 2C0 PutDown; general 280; camera");
            Check.Int("launch: the counter stays 0 during the flight", UWQuestFlags.Get(UWEndgameRules.TalismanQuestFlag), 0);
            Check.Int("launch: step 0 shown at once", lOHost.Poses.Count, 1);
            Check.Bool("launch: running", lOSequence.IsLaunching, true);
            Check.Bool("launch: a second one while it runs does nothing",
                lOSequence.Launch(lOHost.World, lOHost, lOSchedule, lfStep), false);

            // The launch's own frame, then 60 fps: the flight takes 64 step lengths of game time.
            long llStart = -1;
            int liFrames = 0;
            double ldRemainder = 0.0;

            while (lOSequence.IsLaunching && liFrames < 10000)
            {
                ldRemainder += UWPlayerTick.PitTicksPerSecond / 60.0;

                int liTicks = (int)Math.Floor(ldRemainder);

                ldRemainder -= liTicks;
                lOSchedule.Advance(liTicks, true);

                if (llStart < 0)
                    llStart = lOSchedule.Now;

                liFrames++;

                // A menu in between: the flight stands.
                if (liFrames == 20)
                {
                    int liShown = lOHost.Poses.Count;

                    lOSchedule.Advance(0, false);
                    lOSchedule.Advance(0, false);
                    Check.Int("launch: a held clock holds the flight", lOHost.Poses.Count, liShown);
                }
            }

            double ldSeconds = (lOHost.TravelledAt - llStart) / (double)UWPlayerTick.PitTicksPerSecond;

            Check.Near("launch: 64 steps of 1/15 s on the game clock", ldSeconds, 64.0 / 15.0, 1.0 / 60.0);
            Check.Int("launch: every step shown once at 60 fps", lOHost.Poses.Count, UWMoongateFlight.Steps);
            Check.Str("launch: then the camera ends, the journey, the last message",
                string.Join("; ", lOHost.Log.GetRange(3, lOHost.Log.Count - 3)), "end camera; travel 8 27/23; general 281");
            Check.Int("launch: the counter 0xFF after the journey (check_victory)", UWQuestFlags.Get(UWEndgameRules.TalismanQuestFlag),
                UWEndgameRules.TalismansAfterVictory);
            Check.Bool("launch: no victory check due at 0xFF", UWEndgameRules.AllTalismansGone(), false);

            // THE TALISMANS IN THE VOLCANO count down; the last one only brings the counter to 0,
            // which the victory check watches (mob_to_static, check_victory).
            int liLeft;

            UWQuestFlags.Set(UWEndgameRules.TalismanQuestFlag, 2);
            Check.Str("talisman: one of two burns", UWEndgameRules.DropTalismanInLava(310, UWEndgameRules.VolcanoLevelIndex,
                32, 32, true, true, out liLeft).ToString(), "Flame");
            Check.Int("talisman: ... one left", liLeft, 1);
            Check.Bool("talisman: ... no victory yet", UWEndgameRules.AllTalismansGone(), false);
            Check.Str("talisman: the last one", UWEndgameRules.DropTalismanInLava(10, UWEndgameRules.VolcanoLevelIndex,
                33, 31, true, true, out liLeft).ToString(), "Last");
            Check.Int("talisman: ... the counter at 0", UWQuestFlags.Get(UWEndgameRules.TalismanQuestFlag), 0);
            Check.Bool("talisman: ... the victory check is due", UWEndgameRules.AllTalismansGone(), true);
            Check.Str("talisman: one more at 0 wraps to 0xFF, a flame", UWEndgameRules.DropTalismanInLava(54,
                UWEndgameRules.VolcanoLevelIndex, 32, 32, true, true, out liLeft).ToString(), "Flame");
            Check.Int("talisman: ... the counter 0xFF", UWQuestFlags.Get(UWEndgameRules.TalismanQuestFlag), 0xFF);
            UWQuestFlags.Set(UWEndgameRules.TalismanQuestFlag, 0);
            Check.Bool("launch: no longer running", lOSequence.IsLaunching, false);
            Check.Int("launch: its step is gone", lOSchedule.RunningSteps, 0);

            // STEP LENGTH 0: one step per running frame, the launch's own frame showing step 0.
            UWWorldScheduler lOFrames = new UWWorldScheduler();
            UWEndgameSequence lOPerFrame = new UWEndgameSequence();
            LoggingEndgameHost lOFrameHost = new LoggingEndgameHost { Clock = lOFrames };
            int liAdvances = 0;

            lOPerFrame.Launch(lOFrameHost.World, lOFrameHost, lOFrames, 0f);

            while (lOPerFrame.IsLaunching && liAdvances < 1000)
            {
                lOFrames.Advance(1, true);
                liAdvances++;
            }

            Check.Int("per frame: the journey on the 65th advance (frames 0 to 63 show the steps)", liAdvances, UWMoongateFlight.Steps + 1);
            Check.Int("per frame: every step once", lOFrameHost.Poses.Count, UWMoongateFlight.Steps);

            // Without a camera the flight still takes its time.
            UWWorldScheduler lOBlind = new UWWorldScheduler();
            UWEndgameSequence lOBlindSequence = new UWEndgameSequence();
            LoggingEndgameHost lOBlindHost = new LoggingEndgameHost { Clock = lOBlind, HasCamera = false };

            lOBlindSequence.Launch(lOBlindHost.World, lOBlindHost, lOBlind, 0.1f);
            lOBlind.Advance(0, true);

            for (int liTick = 0; liTick < 1637 && lOBlindSequence.IsLaunching; liTick++)
                lOBlind.Advance(1, true);

            Check.Bool("no camera: still flying after 6.39 s", lOBlindSequence.IsLaunching, true);
            lOBlind.Advance(2, true);
            Check.Bool("no camera: arrives at 6.4 s", lOBlindSequence.IsLaunching, false);
            Check.Int("no camera: no pose and no camera end", lOBlindHost.Poses.Count, 0);
            Check.Bool("no camera: still the journey", lOBlindHost.Log.Contains("travel 8 27/23"), true);

            // THE FLIGHT'S POSES: step 0 at the start, the path shrinking to the gate.
            UWMoongateFlight lOFlight = UWMoongateFlight.From(new UWVector3(32 * 64f + 256f, 40f, 32 * 64f), 270f, 32, 32);
            UWMoongateFlight.Pose lOFirst = lOFlight.PoseAt(0);
            UWMoongateFlight.Pose lOLast = lOFlight.PoseAt(UWMoongateFlight.Steps - 1);

            Check.Near("flight: step 0 at the camera (x)", lOFirst.Position.X, 32 * 64f + 256f, 0.01);
            Check.Near("flight: step 0 at the camera (z)", lOFirst.Position.Z, 32 * 64f, 0.01);
            Check.Near("flight: looking at the gate (east of it, so west)", lOFirst.Yaw, 270.0, 0.01);
            Check.Near("flight: no roll at step 0", lOFirst.Roll, 0.0, 0.0001);
            Check.Near("flight: the last step a 64th of the way out", Math.Sqrt(Math.Pow(lOLast.Position.X - 32 * 64f, 2)
                + Math.Pow(lOLast.Position.Z - 32 * 64f, 2)), 256.0 / 64.0, 0.01);
            Check.Near("flight: sinking two fine units a step", lOLast.Position.Y, 40.0 - (63 * 0.5), 0.001);
            Check.Near("flight: rolling 0x800 a step", lOFlight.PoseAt(1).Roll, 11.25, 0.0001);
            Check.Near("flight: circling 0xCCB a step", lOFlight.PoseAt(1).Yaw - lOFirst.Yaw, 0xCCB * 360.0 / 65536.0, 0.001);
            Check.Int("flight: step at 1/15 s after 17 ticks", UWMoongateFlight.StepAt(lfStep, 17, 0), 0);
            Check.Int("flight: and after 18", UWMoongateFlight.StepAt(lfStep, 18, 0), 1);
            Check.Int("flight: step length 0 counts frames", UWMoongateFlight.StepAt(0f, 5000, 7), 7);

            UWQuestFlags.Set(UWEndgameRules.TalismanQuestFlag, liSavedCounter);
        }
    }
}
