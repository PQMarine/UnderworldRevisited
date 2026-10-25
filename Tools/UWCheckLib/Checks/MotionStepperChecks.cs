using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.7 (2026-10-18): UWMotionStepper, the host's tick accumulator and the two
    /// loops of the motion switch. Pinned: Original runs whole steps of its frame length, the same
    /// number at any frame rate (Docs/DEVIATIONS.md, Rules), each after the host kept its
    /// interpolation state; a long frame is capped at MaxFrameTicks plus a step; a held clock runs
    /// nothing; Smooth runs one frame per rendered frame in 1/256 ticks with the remainder carried
    /// and cut past MaxFrameTicks; a jump pressed in a frame without a step waits for the next one
    /// and takes exactly one frame. With the game data the real motion walks through the stepper:
    /// the same place at 32, 64 and 128 frames a second, and a pressed jump leaves the ground.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>piFrames rendered frames of 1/piFps seconds; returns the frames the motion ran.</summary>
        private static int fAdvance(UWMotionStepper pOStepper, int piFps, int piFrames, bool pbSmooth, int piStep,
            UWPlayerMotion.Command peHeld, IUWMotionStepTarget pOTarget)
        {
            int liRan = 0;
            float lfSeconds = 1f / piFps;

            for (int liAt = 0; liAt < piFrames; liAt++)
                liRan += pOStepper.Advance(lfSeconds, pbSmooth, piStep, peHeld, pOTarget);

            return liRan;
        }

        private static void fCheckMotionStepper()
        {
            Check.Group("Motion stepper (UWMotionStepper - cleanup step 4.7)");

            fCheckStepperOriginal();
            fCheckStepperSmooth();
            fCheckStepperJump();
        }

        private static void fCheckStepperOriginal()
        {
            // The same steps at any frame rate: one second at 32, 64 and 128 fps (exact in float).
            foreach (int liFps in new[] { 32, 64, 128 })
            {
                UWMotionStepper lOStepper = new UWMotionStepper();
                RecordingStepTarget lOTarget = new RecordingStepTarget();

                fExpectInt("original: one second at " + liFps + " fps runs 32 steps of 8 ticks",
                    fAdvance(lOStepper, liFps, liFps, false, 8, UWPlayerMotion.Command.Walk, lOTarget), 32);
                fExpectInt("original: ... 256 ticks in all at " + liFps + " fps", lOTarget.TotalDt, 256);
            }

            // And within a step at the usual rates.
            foreach (int liFps in new[] { 30, 60, 144, 240 })
            {
                UWMotionStepper lOStepper = new UWMotionStepper();
                RecordingStepTarget lOTarget = new RecordingStepTarget();
                int liSteps = fAdvance(lOStepper, liFps, liFps * 4, false, 16, UWPlayerMotion.Command.Walk, lOTarget);

                fExpectBool("original: four seconds at " + liFps + " fps run 64 steps of 16 ticks (within one)",
                    Math.Abs(liSteps - 64) <= 1, true);
            }

            UWMotionStepper lOOrder = new UWMotionStepper();
            RecordingStepTarget lOOrderTarget = new RecordingStepTarget();

            lOOrder.Advance(1f / 16f, false, 4, UWPlayerMotion.Command.Walk, lOOrderTarget);
            fExpectBool("original: a 1/16 s frame runs four 4-tick steps, each after the host kept its view state",
                string.Join(",", lOOrderTarget.Calls) == "before,frame,before,frame,before,frame,before,frame", true);
            fExpectInt("original: ... all with the held walk", lOOrderTarget.Count(UWPlayerMotion.Command.Walk), 4);

            UWMotionStepper lOFew = new UWMotionStepper();
            RecordingStepTarget lOFewTarget = new RecordingStepTarget();

            fExpectInt("original: a 1/60 s frame runs no 16-tick step", lOFew.Advance(1f / 60f, false, 16,
                UWPlayerMotion.Command.Walk, lOFewTarget), 0);
            fExpectBool("original: ... and keeps the 4.27 ticks", Math.Abs(lOFew.Remainder - (256f / 60f)) < 0.001f, true);
            fExpectBool("original: ... the view drawn that far towards the next step",
                Math.Abs(lOFew.Alpha(16) - ((256f / 60f) / 16f)) < 0.0001f, true);

            UWMotionStepper lOLong = new UWMotionStepper();
            RecordingStepTarget lOLongTarget = new RecordingStepTarget();

            fExpectInt("original: a one-second hitch runs at most (0x40 + 8) / 8 = 9 steps",
                lOLong.Advance(1f, false, 8, UWPlayerMotion.Command.Walk, lOLongTarget), 9);
            fExpectBool("original: ... and drops the rest", lOLong.Remainder == 0f, true);

            UWMotionStepper lOHeld = new UWMotionStepper();
            RecordingStepTarget lOHeldTarget = new RecordingStepTarget();

            lOHeld.Advance(1f / 60f, false, 8, UWPlayerMotion.Command.Walk, lOHeldTarget);

            float lfBefore = lOHeld.Remainder;

            fExpectInt("original: a held clock runs nothing", lOHeld.Advance(0f, false, 8, UWPlayerMotion.Command.Walk, lOHeldTarget), 0);
            fExpectBool("original: ... and owes nothing new", lOHeld.Remainder == lfBefore, true);

            lOHeld.ResetTime();
            fExpectBool("original: a placement clears what was owed", lOHeld.Remainder == 0f, true);

            fExpectInt("step: Original takes the chosen frame length", UWMotionStepper.StepTicks(false, 4, 12), 12);
            fExpectInt("step: Smooth takes its own step", UWMotionStepper.StepTicks(true, 4, 12), 4);
            fExpectInt("step: ... at least one tick", UWMotionStepper.StepTicks(true, 0, 12), 1);
            fExpectInt("step: ... at most MaxFrameTicks", UWMotionStepper.StepTicks(true, 100, 12), UWPlayerMotion.MaxFrameTicks);
        }

        private static void fCheckStepperSmooth()
        {
            UWMotionStepper lOStepper = new UWMotionStepper();
            RecordingStepTarget lOTarget = new RecordingStepTarget();

            fExpectInt("smooth: a rendered frame runs one motion frame", lOStepper.Advance(1f / 60f, true, 4,
                UWPlayerMotion.Command.Walk, lOTarget), 1);
            fExpectInt("smooth: ... of 1/60 s in 1/256 ticks, floored", lOTarget.Dts[0], (int)Math.Floor((256f / 60f) * 256f));
            fExpectBool("smooth: ... without a step's view state", lOTarget.Calls.Contains("before"), false);

            fAdvance(lOStepper, 60, 59, true, 4, UWPlayerMotion.Command.Walk, lOTarget);
            fExpectInt("smooth: one second at 60 fps runs 60 frames", lOTarget.Dts.Count, 60);
            fExpectBool("smooth: ... 256 ticks of 1/256 in all, the remainder carried (within 2)",
                Math.Abs(lOTarget.TotalDt - (256 * UWMotionParams.FineTicksPerTick)) <= 2, true);

            UWMotionStepper lO144 = new UWMotionStepper();
            RecordingStepTarget lO144Target = new RecordingStepTarget();

            fAdvance(lO144, 144, 144, true, 4, UWPlayerMotion.Command.Walk, lO144Target);
            fExpectBool("smooth: ... and the same at 144 fps (within 2)",
                Math.Abs(lO144Target.TotalDt - (256 * UWMotionParams.FineTicksPerTick)) <= 2, true);

            UWMotionStepper lOLong = new UWMotionStepper();
            RecordingStepTarget lOLongTarget = new RecordingStepTarget();

            lOLong.Advance(1f, true, 4, UWPlayerMotion.Command.Walk, lOLongTarget);
            fExpectInt("smooth: a one-second hitch is cut to MaxFrameTicks", lOLongTarget.Dts[0],
                UWPlayerMotion.MaxFrameTicks * UWMotionParams.FineTicksPerTick);
            fExpectBool("smooth: ... and drops the rest", lOLong.Remainder == 0f, true);

            UWMotionStepper lOHeld = new UWMotionStepper();
            RecordingStepTarget lOHeldTarget = new RecordingStepTarget();

            fExpectInt("smooth: a held clock runs nothing", lOHeld.Advance(0f, true, 4, UWPlayerMotion.Command.Walk, lOHeldTarget), 0);
        }

        private static void fCheckStepperJump()
        {
            UWMotionStepper lOStepper = new UWMotionStepper();
            RecordingStepTarget lOTarget = new RecordingStepTarget();

            // 16-tick steps at 60 fps: the first three frames run none.
            lOStepper.PressJump(UWPlayerMotion.Command.Jump);
            fAdvance(lOStepper, 60, 3, false, 16, UWPlayerMotion.Command.Walk, lOTarget);
            fExpectInt("jump: pressed in frames without a step it waits", (int)lOStepper.PendingJump, (int)UWPlayerMotion.Command.Jump);

            lOStepper.Advance(0f, false, 16, UWPlayerMotion.Command.Walk, lOTarget);
            fExpectInt("jump: ... a held clock keeps it", (int)lOStepper.PendingJump, (int)UWPlayerMotion.Command.Jump);

            fAdvance(lOStepper, 60, 8, false, 16, UWPlayerMotion.Command.Walk, lOTarget);
            fExpectBool("jump: the next step takes it", lOTarget.Commands.Count >= 2 && lOTarget.Commands[0] == UWPlayerMotion.Command.Jump, true);
            fExpectInt("jump: ... in place of the walk for exactly one frame", lOTarget.Count(UWPlayerMotion.Command.Jump), 1);
            fExpectInt("jump: ... the walk goes on after it", (int)lOTarget.Commands[1], (int)UWPlayerMotion.Command.Walk);
            fExpectInt("jump: ... and it is gone", (int)lOStepper.PendingJump, (int)UWPlayerMotion.Command.None);

            UWMotionStepper lOMany = new UWMotionStepper();
            RecordingStepTarget lOManyTarget = new RecordingStepTarget();

            lOMany.PressJump(UWPlayerMotion.Command.StandingJump);
            lOMany.Advance(1f / 16f, false, 4, UWPlayerMotion.Command.None, lOManyTarget);
            fExpectInt("jump: of four steps in one frame only the first jumps", lOManyTarget.Count(UWPlayerMotion.Command.StandingJump), 1);
            fExpectInt("jump: ... the first", (int)lOManyTarget.Commands[0], (int)UWPlayerMotion.Command.StandingJump);

            UWMotionStepper lOSmooth = new UWMotionStepper();
            RecordingStepTarget lOSmoothTarget = new RecordingStepTarget();

            lOSmooth.PressJump(UWPlayerMotion.Command.Jump);
            fAdvance(lOSmooth, 60, 2, true, 4, UWPlayerMotion.Command.Walk, lOSmoothTarget);
            fExpectBool("jump: Smooth takes it with the next frame, then walks",
                lOSmoothTarget.Commands[0] == UWPlayerMotion.Command.Jump && lOSmoothTarget.Commands[1] == UWPlayerMotion.Command.Walk, true);

            UWMotionStepper lODropped = new UWMotionStepper();

            lODropped.PressJump(UWPlayerMotion.Command.Jump);
            lODropped.DropPendingJump();
            fExpectInt("jump: a window taking the controls drops it", (int)lODropped.PendingJump, (int)UWPlayerMotion.Command.None);
        }

        /// <summary>The real motion through the stepper (needs COMOBJ.DAT).</summary>
        private static void fCheckMotionStepperWalk(DataImport pOData)
        {
            Check.Group("Motion stepper with the motion (cleanup step 4.7)");

            if (pOData == null || pOData.CommonObjectProperties == null)
            {
                fFail("motion stepper", "no COMOBJ.DAT");

                return;
            }

            UWRandom.UseSeed(31);

            int liFirstY = -1;
            bool lbSame = true;

            foreach (int liFps in new[] { 32, 64, 128 })
            {
                UWPlayerMotion lOPlayer = fMakePlayer(new FakeMotionWorld(), pOData, 8, 4);
                UWMotionStepper lOStepper = new UWMotionStepper();
                MotionStepTarget lOTarget = new MotionStepTarget(lOPlayer);

                lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                lOPlayer.Walk = UWPlayerMotion.WalkRun;
                fAdvance(lOStepper, liFps, liFps, false, 8, UWPlayerMotion.Command.Walk, lOTarget);

                if (liFirstY < 0)
                    liFirstY = lOPlayer.Params.Y;
                else if (lOPlayer.Params.Y != liFirstY)
                    lbSame = false;
            }

            fExpectBool("walk: a second's run in Original ends on the same unit at 32, 64 and 128 fps", lbSame, true);
            fExpectBool("walk: ... and has moved", liFirstY > (4 << 8) + 0x80, true);

            UWPlayerMotion lOJumper = fMakePlayer(new FakeMotionWorld(), pOData, 8, 8);
            UWMotionStepper lOJumpStepper = new UWMotionStepper();
            MotionStepTarget lOJumpTarget = new MotionStepTarget(lOJumper);

            lOJumpStepper.PressJump(UWPlayerMotion.Command.StandingJump);
            fAdvance(lOJumpStepper, 60, 1, false, 16, UWPlayerMotion.Command.None, lOJumpTarget);
            fExpectBool("jump: still on the ground in a frame without a step", lOJumper.IsAirborne, false);
            fAdvance(lOJumpStepper, 60, 3, false, 16, UWPlayerMotion.Command.None, lOJumpTarget);
            fExpectBool("jump: ... off it after the next step", lOJumper.IsAirborne || lOJumper.Params.Vz > 0, true);
        }
    }
}
