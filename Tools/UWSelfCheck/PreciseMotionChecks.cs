using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE PRECISION MODE of the player's motion (UWMotionParams.Precise, the host's Smooth motion,
    /// 2026-10-06): the core carries the sub-fine fraction and the per-call rules are rates per
    /// original frame, so small steps add up exactly. Checked here: the flag off leaves the
    /// original's call-by-call results alone; with it on the speeds are symmetric and the
    /// step size does not matter (2, 4, 8 and 16 ticks reach the same place), the ramp takes
    /// the original's time whatever the step, the view's displacement per step is even, and
    /// the rules the user confirmed - the ledge at a creep and a run, the fall's damage, the
    /// levitation stop, the slow fall's cap, the water edge, the jump apex - hold.
    /// </summary>
    public static partial class Program
    {
        private static UWPlayerMotion fMakePrecisePlayer(FakeMotionWorld pOWorld, DataImport pOData, int piTileX, int piTileY, bool pbPrecise)
        {
            UWPlayerMotion lOPlayer = fMakePlayer(pOWorld, pOData, piTileX, piTileY);

            lOPlayer.Precise = pbPrecise;

            return lOPlayer;
        }

        /// <summary>Runs piTicks of a held command in steps of piStep ticks.</summary>
        private static void fRunTicks(UWPlayerMotion pOPlayer, UWPlayerMotion.Command peCommand, int piWalk, int piTicks, int piStep)
        {
            for (int liAt = 0; liAt < piTicks; liAt += piStep)
            {
                pOPlayer.MotionCommand = peCommand;
                pOPlayer.Walk = piWalk;
                pOPlayer.Frame(piStep);
            }
        }

        /// <summary>The fine distance a run along the heading covers in piTicks after the ramp,
        /// along the axis the heading points at (+-), with the carried fraction.</summary>
        private static float fRunDistance(FakeMotionWorld pOWorld, DataImport pOData, int piHeading, UWPlayerMotion.Command peCommand,
            int piTicks, int piStep, bool pbPrecise, out float pfSpread)
        {
            UWPlayerMotion lOPlayer = fMakePrecisePlayer(pOWorld, pOData, 16, 16, pbPrecise);

            lOPlayer.CameraYaw = piHeading;

            // The ramp first: two original frames' worth beyond the longest ramp.
            fRunTicks(lOPlayer, peCommand, UWPlayerMotion.WalkRun, 256, piStep);

            // The axis the motion runs along: the heading's, turned a quarter for the slides.
            bool lbSlide = peCommand == UWPlayerMotion.Command.SlideLeft || peCommand == UWPlayerMotion.Command.SlideRight;
            bool lbHeadingX = piHeading == 0x4000 || piHeading == 0xC000;
            bool lbAlongX = lbSlide ? !lbHeadingX : lbHeadingX;
            float lfStart = lbAlongX ? lOPlayer.FineX : lOPlayer.FineY;
            float lfLast = lfStart;
            float lfMin = float.MaxValue;
            float lfMax = float.MinValue;

            for (int liAt = 0; liAt < piTicks; liAt += piStep)
            {
                lOPlayer.MotionCommand = peCommand;
                lOPlayer.Walk = UWPlayerMotion.WalkRun;
                lOPlayer.Frame(piStep);

                float lfNow = lbAlongX ? lOPlayer.FineX : lOPlayer.FineY;
                float lfMoved = Math.Abs(lfNow - lfLast);

                lfLast = lfNow;
                lfMin = Math.Min(lfMin, lfMoved);
                lfMax = Math.Max(lfMax, lfMoved);
            }

            pfSpread = lfMax - lfMin;

            return Math.Abs(lfLast - lfStart);
        }

        private static void fCheckPreciseMotion(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Precise motion (the Smooth mode's precision core)");

            if (pOData == null || pOData.CommonObjectProperties == null)
            {
                fFail("precise motion", "no COMOBJ.DAT");

                return;
            }

            UWRandom.UseSeed(47);

            FakeMotionWorld lOWorld = new FakeMotionWorld();

            // ------------------------------------------------- the speeds: the original drops fractions, the precision mode does not
            float lfSpread;
            float lfPlusY = fRunDistance(lOWorld, pOData, 0x0000, UWPlayerMotion.Command.Walk, 256, 4, false, out lfSpread);
            float lfMinusY = fRunDistance(lOWorld, pOData, 0x8000, UWPlayerMotion.Command.Walk, 256, 4, false, out lfSpread);

            fExpectBool("precise: without the flag a 4-tick run along -y is faster than +y (the dropped fraction)", lfMinusY > lfPlusY + 4f, true);

            // 822 / 65536 tile a tick = 822 / 256 fine a tick: 821.9 fine in 256 ticks.
            const float RunPerTick = 822f / 256f;

            lfPlusY = fRunDistance(lOWorld, pOData, 0x0000, UWPlayerMotion.Command.Walk, 256, 4, true, out lfSpread);
            lfMinusY = fRunDistance(lOWorld, pOData, 0x8000, UWPlayerMotion.Command.Walk, 256, 4, true, out lfSpread);
            fExpectBool("precise: a run along +y covers 822 / 256 fine a tick within one per cent", Math.Abs(lfPlusY - (RunPerTick * 256f)) < RunPerTick * 2.56f, true);
            fExpectBool("precise: ... and -y the same distance within 2 fine", Math.Abs(lfPlusY - lfMinusY) <= 2f, true);
            fExpectBool("precise: ... evenly, the step's displacement spread below one fine", lfSpread < 1f, true);

            float lfPlusX = fRunDistance(lOWorld, pOData, 0x0000, UWPlayerMotion.Command.SlideRight, 256, 4, true, out lfSpread);
            float lfMinusX = fRunDistance(lOWorld, pOData, 0x0000, UWPlayerMotion.Command.SlideLeft, 256, 4, true, out lfSpread);
            fExpectBool("precise: a slide covers 0xEB / 256 fine a tick within one per cent", Math.Abs(lfPlusX - (0xEB / 256f * 256f)) < 0xEB / 100f, true);
            fExpectBool("precise: ... both ways alike within 2 fine", Math.Abs(lfPlusX - lfMinusX) <= 2f, true);

            // Swimming at three tenths: 246 / 256 = 0.96 fine a tick - the DOSBox cycles=max stall.
            FakeMotionWorld lOPool = new FakeMotionWorld();
            lOPool.Paint(1, 1, 30, 30, 1, 2, 1);

            UWPlayerMotion lOSwimmer = fMakePrecisePlayer(lOPool, pOData, 16, 16, false);
            fRunTicks(lOSwimmer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 64, 1);
            float lfStart = lOSwimmer.FineY;
            fRunTicks(lOSwimmer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 128, 1);
            fExpectInt("precise: without the flag a swimmer at one tick a call does not move (0.96 fine truncated)", (int)(lOSwimmer.FineY - lfStart), 0);

            lOSwimmer = fMakePrecisePlayer(lOPool, pOData, 16, 16, true);
            fRunTicks(lOSwimmer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 64, 1);
            lfStart = lOSwimmer.FineY;
            fRunTicks(lOSwimmer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 128, 1);
            fExpectBool("precise: ... with it he swims 246 / 256 fine a tick", Math.Abs(lOSwimmer.FineY - lfStart - (246f / 256f * 128f)) < 3f, true);

            // ------------------------------------------------- the step size does not matter
            float lfAt2 = fRunDistance(lOWorld, pOData, 0x0000, UWPlayerMotion.Command.Walk, 256, 2, true, out lfSpread);
            float lfAt8 = fRunDistance(lOWorld, pOData, 0x0000, UWPlayerMotion.Command.Walk, 256, 8, true, out lfSpread);
            float lfAt16 = fRunDistance(lOWorld, pOData, 0x0000, UWPlayerMotion.Command.Walk, 256, 16, true, out lfSpread);
            fExpectBool("precise: steps of 2, 4, 8 and 16 ticks cover the same distance within 2 fine",
                Math.Abs(lfAt2 - lfPlusY) <= 2f && Math.Abs(lfAt8 - lfPlusY) <= 2f && Math.Abs(lfAt16 - lfPlusY) <= 2f, true);

            // ------------------------------------------------- the ramp is the original's per second
            UWPlayerMotion lOPlayer = fMakePrecisePlayer(lOWorld, pOData, 16, 16, true);
            fRunTicks(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 64, 4);
            fExpectInt("precise: after 64 ticks (four original frames) the run is at 4 * 0x60", lOPlayer.Params.Speed, 4 * 0x60);
            fRunTicks(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 96, 4);
            fExpectInt("precise: ... and at 822 after ten", lOPlayer.Params.Speed, 822);

            UWPlayerMotion lOCoarse = fMakePrecisePlayer(lOWorld, pOData, 16, 16, true);
            fRunTicks(lOCoarse, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 64, 16);
            fExpectInt("precise: the same ramp in 16-tick steps", lOCoarse.Params.Speed, 4 * 0x60);

            lOPlayer = fMakePrecisePlayer(lOWorld, pOData, 16, 16, false);
            fRunTicks(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 64, 4);
            fExpectInt("precise: without the flag the ramp is per call (16 calls reach the run)", lOPlayer.Params.Speed, 822);

            // The Smooth ramp's own frame (RampFrameTicks): 8 reaches the run in 80 ticks, 0 at once.
            lOPlayer = fMakePrecisePlayer(lOWorld, pOData, 16, 16, true);
            lOPlayer.RampFrameTicks = 8;
            fRunTicks(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 64, 4);
            fExpectInt("precise: a ramp frame of 8 ticks is at 8 * 0x60 after 64 ticks", lOPlayer.Params.Speed, 8 * 0x60);
            fRunTicks(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 16, 4);
            fExpectInt("precise: ... and at the run after 80", lOPlayer.Params.Speed, 822);
            lOPlayer = fMakePrecisePlayer(lOWorld, pOData, 16, 16, true);
            lOPlayer.RampFrameTicks = 0;
            fRunTicks(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 4, 4);
            fExpectInt("precise: a ramp frame of 0 is the run at once", lOPlayer.Params.Speed, 822);
            fRunTicks(lOPlayer, UWPlayerMotion.Command.None, 0, 4, 4);
            fExpectInt("precise: ... and the stop at once", lOPlayer.Params.Speed, 0);

            // ------------------------------------------------- the rules
            FakeMotionWorld lOLedge = new FakeMotionWorld();
            lOLedge.Paint(12, 1, 30, 30, 1, 0, 0);

            lOPlayer = fMakePrecisePlayer(lOLedge, pOData, 10, 8, true);
            lOPlayer.CameraYaw = 0x4000;
            fRunTicks(lOPlayer, UWPlayerMotion.Command.Walk, 0x18, 1600, 4);
            fExpectBool("precise: a creep holds at the two-step ledge", lOPlayer.Params.X < (12 << 8) + 0x48 && lOPlayer.Params.Gravity == 0, true);

            lOPlayer = fMakePrecisePlayer(lOLedge, pOData, 10, 8, true);
            lOPlayer.CameraYaw = 0x4000;
            fRunTicks(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 400, 4);

            int liFrames = 0;
            bool lbSound = false;

            for (; liFrames < 400 && (lOPlayer.Params.Gravity != 0 || lOPlayer.IsAirborne); liFrames++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.None;
                lOPlayer.Frame(4);
                lbSound |= lOPlayer.Last.LandingSound;
            }

            fExpectBool("precise: a run goes over it and lands", liFrames < 400 && lOPlayer.Params.Z == 0, true);

            FakeMotionWorld lOHigh = new FakeMotionWorld();
            lOHigh.Paint(1, 1, 30, 30, 1, 10, 0);
            lOHigh.Paint(12, 1, 30, 30, 1, 0, 0);
            lOPlayer = fMakePrecisePlayer(lOHigh, pOData, 10, 8, true);
            lOPlayer.CameraYaw = 0x4000;
            fRunTicks(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 400, 4);

            int liDamage = 0;

            for (liFrames = 0; liFrames < 600 && (lOPlayer.Params.Gravity != 0 || lOPlayer.IsAirborne); liFrames++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.None;
                lOPlayer.Frame(4);
                liDamage = Math.Max(liDamage, lOPlayer.Last.FallDamage);
            }

            fExpectBool("precise: a ten-step fall hurts", liDamage > 3, true);

            lOPlayer = fMakePrecisePlayer(lOLedge, pOData, 10, 8, true);
            lOPlayer.CameraYaw = 0x4000;
            lOPlayer.Abilities = UWPlayerMotion.AbilityLevitate;
            fRunTicks(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 400, 4);
            fExpectBool("precise: levitating off the ledge hovers", lOPlayer.Params.X >= (12 << 8) && lOPlayer.Params.Gravity == 0 && lOPlayer.Params.Z == 0x80, true);
            fRunTicks(lOPlayer, UWPlayerMotion.Command.FlyUp, 0, 32, 4);
            fExpectBool("precise: fly up rises", lOPlayer.Params.Vz > 0, true);
            fRunTicks(lOPlayer, UWPlayerMotion.Command.None, 0, 256, 4);
            fExpectInt("precise: letting go stops within a second", lOPlayer.Params.Vz, 0);

            int liHeld = lOPlayer.Params.Z;
            fRunTicks(lOPlayer, UWPlayerMotion.Command.None, 0, 256, 4);
            fExpectInt("precise: ... and holds the height", lOPlayer.Params.Z, liHeld);

            lOPlayer = fMakePrecisePlayer(lOLedge, pOData, 10, 8, true);
            lOPlayer.CameraYaw = 0x4000;
            lOPlayer.Abilities = UWPlayerMotion.AbilitySlowFall;
            fRunTicks(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 400, 4);

            bool lbCapped = true;

            for (liFrames = 0; liFrames < 400 && lOPlayer.Params.Gravity != 0; liFrames++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.None;
                lOPlayer.Frame(4);

                if (lOPlayer.Params.Vz < -0x5E - 16)
                    lbCapped = false;
            }

            fExpectBool("precise: slow fall stays at the cap", lbCapped, true);
            fExpectInt("precise: ... and takes the momentum", lOPlayer.Params.Speed, 0);

            FakeMotionWorld lOShore = new FakeMotionWorld();
            lOShore.Paint(12, 1, 30, 30, 1, 2, 1);
            lOPlayer = fMakePrecisePlayer(lOShore, pOData, 10, 8, true);
            lOPlayer.CameraYaw = 0x4000;
            fRunTicks(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 640, 4);
            fExpectInt("precise: walking into water swims", (int)lOPlayer.CurrentState, (int)UWPlayerMotion.State.Swimming);

            // ------------------------------------------------- the jump apex: the original's 16-tick arc
            lOPlayer = fMakePrecisePlayer(lOWorld, pOData, 16, 16, false);
            lOPlayer.MotionCommand = UWPlayerMotion.Command.Jump;
            lOPlayer.Frame(16);

            int liMaxZ;
            int liApexDamage;
            fRunUntilSettled(lOPlayer, 60, out liMaxZ, out lbSound, out liApexDamage);

            UWPlayerMotion lOFine = fMakePrecisePlayer(lOWorld, pOData, 16, 16, true);
            lOFine.MotionCommand = UWPlayerMotion.Command.Jump;
            lOFine.Frame(2);

            int liFineMax = lOFine.Params.Z;

            for (liFrames = 0; liFrames < 2000 && (lOFine.Params.Gravity != 0 || lOFine.IsAirborne); liFrames++)
            {
                lOFine.MotionCommand = UWPlayerMotion.Command.None;
                lOFine.Frame(2);
                liFineMax = Math.Max(liFineMax, lOFine.Params.Z);
            }

            fExpectBool("precise: the 2-tick jump peaks where the 16-tick one does (within 6 fine z; measured 290 = 290)", Math.Abs(liFineMax - liMaxZ) <= 6, true);
            fExpectInt("precise: ... and lands on its floor", lOFine.Params.Z, 0x80);

            fCheckFineTicks(pOData, liMaxZ);
        }

        /// <summary>A player in the precision mode with fine ticks (1/256 tick a unit).</summary>
        private static UWPlayerMotion fMakeFinePlayer(FakeMotionWorld pOWorld, DataImport pOData, int piTileX, int piTileY)
        {
            UWPlayerMotion lOPlayer = fMakePrecisePlayer(pOWorld, pOData, piTileX, piTileY, true);

            lOPlayer.FineTicks = true;
            lOPlayer.RampFrameTicks = 8;

            return lOPlayer;
        }

        /// <summary>Frames of a frame time in fine ticks (alternating two values for a frame time
        /// that is no whole number) until piTicks whole ticks have passed.</summary>
        private static void fRunFine(UWPlayerMotion pOPlayer, UWPlayerMotion.Command peCommand, int piWalk, int piTicks, int piFineDtA, int piFineDtB)
        {
            long liLeft = (long)piTicks * UWMotionParams.FineTicksPerTick;

            for (int liAt = 0; liLeft > 0; liAt++)
            {
                // The last frame takes what is left, so the budget is exact.
                int liDt = (int)Math.Min(liLeft, (liAt & 1) == 0 ? piFineDtA : piFineDtB);

                pOPlayer.MotionCommand = peCommand;
                pOPlayer.Walk = piWalk;
                pOPlayer.Frame(liDt);
                liLeft -= liDt;
            }
        }

        /// <summary>
        /// THE SECOND STAGE (FineTicks): one call per rendered frame with the frame's real time.
        /// The same places as with 4-tick steps whatever the frame rate - 60 fps (1092.27 fine
        /// ticks, so 1092 and 1093 alternating), 144 (455) and 240 (273) -, the ramp per second,
        /// an even view, and the rules: the ledge at a creep and a run, the fall's damage, the
        /// levitation stop, the slow fall, water, the jump apex of the original.
        /// </summary>
        private static void fCheckFineTicks(DataImport pOData, int piApex16)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();

            // The reference: the precision mode in 4-tick steps, 256 ticks of running after the ramp.
            UWPlayerMotion lOReference = fMakePrecisePlayer(lOWorld, pOData, 16, 16, true);
            lOReference.RampFrameTicks = 8;
            fRunTicks(lOReference, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 256, 4);
            float lfStart = lOReference.FineY;
            fRunTicks(lOReference, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 256, 4);
            float lfReference = lOReference.FineY - lfStart;

            int[,] liRates = { { 1092, 1093 }, { 455, 455 }, { 273, 273 }, { 4096, 4096 } };
            string[] lsRates = { "60 fps", "144 fps", "240 fps", "16 fps" };

            for (int liAt = 0; liAt < lsRates.Length; liAt++)
            {
                UWPlayerMotion lOPlayer = fMakeFinePlayer(lOWorld, pOData, 16, 16);

                fRunFine(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 256, liRates[liAt, 0], liRates[liAt, 1]);

                float lfFrom = lOPlayer.FineY;
                float lfLast = lfFrom;
                float lfMin = float.MaxValue;
                float lfMax = float.MinValue;
                long liLeft = 256L * UWMotionParams.FineTicksPerTick;

                for (int liFrame = 0; liLeft > 0; liFrame++)
                {
                    int liDt = (int)Math.Min(liLeft, (liFrame & 1) == 0 ? liRates[liAt, 0] : liRates[liAt, 1]);

                    lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                    lOPlayer.Walk = UWPlayerMotion.WalkRun;
                    lOPlayer.Frame(liDt);
                    liLeft -= liDt;

                    float lfMoved = (lOPlayer.FineY - lfLast) / liDt;

                    lfLast = lOPlayer.FineY;
                    lfMin = Math.Min(lfMin, lfMoved);
                    lfMax = Math.Max(lfMax, lfMoved);
                }

                fExpectBool("fine ticks: a run at " + lsRates[liAt] + " covers the 4-tick distance within 2 fine", Math.Abs((lOPlayer.FineY - lfFrom) - lfReference) <= 2f, true);
                fExpectBool("fine ticks: ... at an even speed per fine tick", (lfMax - lfMin) * liRates[liAt, 0] < 1f, true);
            }

            // The ramp per second with the fraction carried: 8 * 0x60 after 64 ticks at 60 fps.
            UWPlayerMotion lORamp = fMakeFinePlayer(lOWorld, pOData, 16, 16);
            fRunFine(lORamp, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 64, 1092, 1093);
            fExpectBool("fine ticks: the ramp is at 8 * 0x60 after 64 ticks at 60 fps (within 2)", Math.Abs(lORamp.Params.Speed - (8 * 0x60)) <= 2, true);
            fRunFine(lORamp, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 32, 1092, 1093);
            fExpectInt("fine ticks: ... and at the run after 96", lORamp.Params.Speed, 822);

            // The rules at 60 fps.
            FakeMotionWorld lOLedge = new FakeMotionWorld();
            lOLedge.Paint(12, 1, 30, 30, 1, 0, 0);

            UWPlayerMotion lOCreep = fMakeFinePlayer(lOLedge, pOData, 10, 8);
            lOCreep.CameraYaw = 0x4000;
            fRunFine(lOCreep, UWPlayerMotion.Command.Walk, 0x18, 1600, 1092, 1093);
            fExpectBool("fine ticks: a creep holds at the two-step ledge", lOCreep.Params.X < (12 << 8) + 0x48 && lOCreep.Params.Gravity == 0, true);

            UWPlayerMotion lORun = fMakeFinePlayer(lOLedge, pOData, 10, 8);
            lORun.CameraYaw = 0x4000;
            fRunFine(lORun, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 400, 1092, 1093);

            int liFrames = 0;

            for (; liFrames < 2000 && (lORun.Params.Gravity != 0 || lORun.IsAirborne); liFrames++)
            {
                lORun.MotionCommand = UWPlayerMotion.Command.None;
                lORun.Frame(1092);
            }

            fExpectBool("fine ticks: a run goes over it and lands on floor 0", liFrames < 2000 && lORun.Params.Z == 0, true);

            FakeMotionWorld lOHigh = new FakeMotionWorld();
            lOHigh.Paint(1, 1, 30, 30, 1, 10, 0);
            lOHigh.Paint(12, 1, 30, 30, 1, 0, 0);
            UWPlayerMotion lOFall = fMakeFinePlayer(lOHigh, pOData, 10, 8);
            lOFall.CameraYaw = 0x4000;
            fRunFine(lOFall, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 400, 1092, 1093);

            int liDamage = 0;

            for (liFrames = 0; liFrames < 3000 && (lOFall.Params.Gravity != 0 || lOFall.IsAirborne); liFrames++)
            {
                lOFall.MotionCommand = UWPlayerMotion.Command.None;
                lOFall.Frame(1092);
                liDamage = Math.Max(liDamage, lOFall.Last.FallDamage);
            }

            fExpectBool("fine ticks: a ten-step fall hurts", liDamage > 3, true);

            UWPlayerMotion lOHover = fMakeFinePlayer(lOLedge, pOData, 10, 8);
            lOHover.CameraYaw = 0x4000;
            lOHover.Abilities = UWPlayerMotion.AbilityLevitate;
            fRunFine(lOHover, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 400, 1092, 1093);
            fExpectBool("fine ticks: levitating off the ledge hovers at its height", lOHover.Params.X >= (12 << 8) && lOHover.Params.Gravity == 0 && lOHover.Params.Z == 0x80, true);
            fRunFine(lOHover, UWPlayerMotion.Command.FlyUp, 0, 32, 1092, 1093);
            fExpectBool("fine ticks: fly up rises", lOHover.Params.Vz > 0, true);
            fRunFine(lOHover, UWPlayerMotion.Command.None, 0, 256, 1092, 1093);
            fExpectInt("fine ticks: letting go stops within a second", lOHover.Params.Vz, 0);

            UWPlayerMotion lOSlow = fMakeFinePlayer(lOLedge, pOData, 10, 8);
            lOSlow.CameraYaw = 0x4000;
            lOSlow.Abilities = UWPlayerMotion.AbilitySlowFall;
            fRunFine(lOSlow, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 400, 1092, 1093);

            bool lbCapped = true;

            for (liFrames = 0; liFrames < 2000 && lOSlow.Params.Gravity != 0; liFrames++)
            {
                lOSlow.MotionCommand = UWPlayerMotion.Command.None;
                lOSlow.Frame(1092);

                if (lOSlow.Params.Vz < -0x5E - 16)
                    lbCapped = false;
            }

            fExpectBool("fine ticks: slow fall stays at the cap", lbCapped, true);
            fExpectInt("fine ticks: ... and takes the momentum", lOSlow.Params.Speed, 0);

            FakeMotionWorld lOShore = new FakeMotionWorld();
            lOShore.Paint(12, 1, 30, 30, 1, 2, 1);
            UWPlayerMotion lOSwim = fMakeFinePlayer(lOShore, pOData, 10, 8);
            lOSwim.CameraYaw = 0x4000;
            fRunFine(lOSwim, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 640, 1092, 1093);
            fExpectInt("fine ticks: walking into water swims", (int)lOSwim.CurrentState, (int)UWPlayerMotion.State.Swimming);

            UWPlayerMotion lOJump = fMakeFinePlayer(lOWorld, pOData, 16, 16);
            lOJump.MotionCommand = UWPlayerMotion.Command.Jump;
            lOJump.Frame(1092);

            int liApex = lOJump.Params.Z;

            for (liFrames = 0; liFrames < 2000 && (lOJump.Params.Gravity != 0 || lOJump.IsAirborne); liFrames++)
            {
                lOJump.MotionCommand = UWPlayerMotion.Command.None;
                lOJump.Frame(1092);
                liApex = Math.Max(liApex, lOJump.Params.Z);
            }

            fExpectBool("fine ticks: the jump at 60 fps peaks where the original's does (within 6 fine z)", Math.Abs(liApex - piApex16) <= 6, true);
            fExpectInt("fine ticks: ... and lands on its floor", lOJump.Params.Z, 0x80);
        }
    }
}
