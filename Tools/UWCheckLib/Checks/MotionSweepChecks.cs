using System;
using System.Collections.Generic;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE MOTION SWEEP (the motor plan's test bench, M0, Docs/MOTOR-PLAN.md section 5, 2026-10-27):
    /// many runs of the player's motion on the REAL levels, the way the game runs it - from random
    /// starts on open floor, with random headings and commands, in small steps at the frame
    /// lengths of 30, 60 and 144 frames a second - and each run compared between the movers. Two
    /// attempts at a modern feel failed because their checks ran synthetic layouts in whole
    /// frames; this is what a new mover has to pass before the user plays it.
    ///
    /// The movers: the original core in its Original mode (whole steps of 8 ticks, the default
    /// frame length) and in its Smooth mode (the precision mode, one step per rendered frame);
    /// the character motor joins in M1. A run is STUCK when the body moved less than a 32nd of a
    /// tile in its two seconds, INSIDE when it ends in rock (a solid tile, the closed half of a
    /// diagonal, a floor more than a level above its feet). Each run's start is printed so a case
    /// can be replayed.
    ///
    /// The self-check runs a short sweep (RegularRuns a level) and asserts what must hold of the
    /// original core; --sweep n runs n a level and prints the report.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>The runs per level of the regular self-check.</summary>
        private const int RegularRuns = 12;

        /// <summary>One run's length: two seconds of PIT ticks.</summary>
        private const int SweepRunTicks = 512;

        /// <summary>The frame rates a run is played at.</summary>
        private static readonly int[] SweepFrameRates = { 30, 60, 144 };

        /// <summary>The motor's restarts from a run's end at a wall: how many, how long (a quarter
        /// second), at which frame rates - down to the smallest steps.</summary>
        private const int SweepRestarts = 8;

        private const int SweepRestartTicks = 64;

        /// <summary>The straight approaches to each move trigger in the trigger pass (one in the
        /// regular self-check).</summary>
        private const int TriggerApproaches = 12;

        private static readonly int[] SweepRestartRates = { 30, 60, 144, 240, 500 };

        /// <summary>Less than this many fine units (1/256 tile) moved is stuck.</summary>
        private const int SweepStuckFine = 8;

        /// <summary>The commands a run holds: the walk, and the modern scheme's free walk at an offset
        /// from the view (back, the sides, the diagonals).</summary>
        private static readonly int[] SweepFreeOffsets = { -1, 0x8000, 0x4000, 0xC000, 0x2000, 0xE000 };

        /// <summary>Where a run starts and how it is driven.</summary>
        private struct SweepStart
        {
            public int Level;

            public int TileX;

            public int TileY;

            public int FineX;

            public int FineY;

            public int Heading;

            /// <summary>-1 the walk forward, else the free walk's offset.</summary>
            public int FreeOffset;

            public int FrameRate;

            public int Seed;

            /// <summary>The walk value held (WalkRun the run, less a creep that holds at ledges).</summary>
            public int Walk;

            /// <summary>The tick of the run at which the mover jumps (-1: none).</summary>
            public int JumpTick;

            public override string ToString()
            {
                return "L" + Level + " " + TileX + "/" + TileY + " fine " + FineX + "/" + FineY + " heading " + Heading.ToString("X4")
                    + (FreeOffset < 0 ? " walk" : " free " + FreeOffset.ToString("X4")) + " " + FrameRate + "fps"
                    + (Walk != UWPlayerMotion.WalkRun ? " creep " + Walk : string.Empty) + (JumpTick >= 0 ? " jump at " + JumpTick : string.Empty);
            }
        }

        /// <summary>Where a run ended.</summary>
        private struct SweepEnd
        {
            public int X;

            public int Y;

            public int Z;

            /// <summary>The farthest it got from the start, in fine units.</summary>
            public int Moved;

            public bool Inside;

            public int Triggers;

            /// <summary>The move triggers met, by master-list index.</summary>
            public List<int> TriggerList;
        }

        /// <summary>A mover under test: placed, then driven frame by frame.</summary>
        private abstract class SweepMover
        {
            public abstract string Name { get; }

            public abstract void Place(LevelMotionWorld pOWorld, DataImport pOData, SweepStart pOStart);

            /// <summary>One rendered frame of piFineDt (1/256 tick) under the run's command.</summary>
            public abstract void Frame(int piFineDt);

            public abstract int X { get; }

            public abstract int Y { get; }

            public abstract int Z { get; }
        }

        /// <summary>The original core, Original (whole steps of 8 ticks, the time between carried
        /// over) or Smooth (the precision mode, one step a frame) - as UWPlayerMovement drives it.</summary>
        private sealed class CoreSweepMover : SweepMover
        {
            private readonly bool mbSmooth;

            private UWPlayerMotion mOMotion;

            private int miFreeOffset;

            private int miCarried;

            private int miWalk;

            private int miJumpTick;

            private long miElapsed;

            public CoreSweepMover(bool pbSmooth)
            {
                mbSmooth = pbSmooth;
            }

            public override string Name => mbSmooth ? "Smooth" : "Original";

            public override void Place(LevelMotionWorld pOWorld, DataImport pOData, SweepStart pOStart)
            {
                mOMotion = new UWPlayerMotion(pOWorld, pOData.CommonObjectProperties);
                mOMotion.PlaceInTile(pOStart.TileX, pOStart.TileY);
                mOMotion.PlaceAt((pOStart.TileX << 8) + pOStart.FineX, (pOStart.TileY << 8) + pOStart.FineY, mOMotion.Params.Z, pOStart.Heading);
                mOMotion.Precise = mbSmooth;
                mOMotion.FineTicks = mbSmooth;
                mOMotion.RampFrameTicks = 8;
                miFreeOffset = pOStart.FreeOffset;
                miCarried = 0;
                miWalk = pOStart.Walk;
                miJumpTick = pOStart.JumpTick;
                miElapsed = 0;
            }

            public override void Frame(int piFineDt)
            {
                if (mbSmooth)
                {
                    fCommand();
                    mOMotion.Frame(Math.Min(piFineDt, UWPlayerMotion.MaxFrameTicks * UWMotionParams.FineTicksPerTick));
                    miElapsed += piFineDt;

                    return;
                }

                miCarried += piFineDt;

                while (miCarried >= 8 * UWMotionParams.FineTicksPerTick)
                {
                    miCarried -= 8 * UWMotionParams.FineTicksPerTick;
                    fCommand();
                    mOMotion.Frame(8);
                    miElapsed += 8 * UWMotionParams.FineTicksPerTick;
                }
            }

            private void fCommand()
            {
                mOMotion.Walk = miWalk;

                // The jump once its tick has come (the command reads it only on the ground).
                if (miJumpTick >= 0 && miElapsed >= (long)miJumpTick * UWMotionParams.FineTicksPerTick)
                {
                    mOMotion.MotionCommand = UWPlayerMotion.Command.Jump;
                    miJumpTick = -1;

                    return;
                }

                if (miFreeOffset < 0)
                    mOMotion.MotionCommand = UWPlayerMotion.Command.Walk;
                else
                {
                    mOMotion.MotionCommand = UWPlayerMotion.Command.Free;
                    mOMotion.FreeOffset = miFreeOffset;
                }
            }

            public override int X => mOMotion.Params.X;

            public override int Y => mOMotion.Params.Y;

            public override int Z => mOMotion.Params.Z;
        }

        /// <summary>The character motor (M1): the run's command as a velocity at once - forward the
        /// walk's speed, back and to the sides three quarters of it (decision 1), a free direction
        /// between them by its angle - moved by collide and slide each rendered frame.</summary>
        private sealed class MotorSweepMover : SweepMover
        {
            private UWCharacterMotor mOMotor;

            private double mfVx;

            private double mfVy;

            private int miFrame;

            private int miForward;

            private int miForwardSpeed;

            private double mfSpeed;

            private int miJumpTick;

            private long miElapsed;

            private double mfTakeOff;

            /// <summary>The frames in which the feet rose more than a step on the ground without a jump,
            /// the landings higher than the jump's arc and a step reach, the frames with the head above
            /// the ceiling: the run reached what the original cannot ("beyond").</summary>
            public int Climbs;

            public int HighLandings;

            public int Ceilings;

            /// <summary>The frames in which a body holding at ledges left the ground without a jump.</summary>
            public int SlowFalls;

            /// <summary>The run met a wall or an object: its path is no longer the straight line.</summary>
            public bool EverTouched;

            /// <summary>The run's direction of motion (a unit vector).</summary>
            public double DirX;

            public double DirY;

            /// <summary>The frames of the run in which the motor halted though neither a wall met head-on
            /// nor a corner held it, and the first of them (-1: none).</summary>
            public int FalseHalts;

            public int FirstHalt;

            public override string Name => "Modern";

            public UWCharacterMotor Motor => mOMotor;

            public override void Place(LevelMotionWorld pOWorld, DataImport pOData, SweepStart pOStart)
            {
                UWPlayerMotion lOShared = new UWPlayerMotion(pOWorld, pOData.CommonObjectProperties);

                miForwardSpeed = lOShared.ForwardSpeed;
                miForward = (short)((short)((UWPlayerMotion.WalkRun >> 2) * lOShared.ForwardSpeed) / 32);
                mOMotor = new UWCharacterMotor(pOWorld, pOData.CommonObjectProperties);
                mOMotor.PlaceOnFloor(pOStart.TileX + (pOStart.FineX / 256.0), pOStart.TileY + (pOStart.FineY / 256.0));
                Aim(pOStart.Heading, pOStart.FreeOffset, pOStart.Walk, pOStart.JumpTick);
            }

            /// <summary>A new command where the body stands: the heading, the free walk's offset (-1
            /// the walk), the walk value and the jump's tick (-1 none); the counts start anew.</summary>
            public void Aim(int piHeading, int piFreeOffset, int piWalk, int piJumpTick)
            {
                int liAngle = piFreeOffset < 0 ? 0 : Math.Abs((int)(short)(piFreeOffset & 0xFFFF));
                double lfYaw = ((piHeading + Math.Max(0, piFreeOffset)) & 0xFFFF) * (Math.PI * 2.0 / 65536.0);

                mfSpeed = miForward * ((double)piWalk / UWPlayerMotion.WalkRun) * (1.0 - (0.25 * Math.Min(liAngle, 0x4000) / 0x4000));

                // 1/65536 tile a tick, as the core's speed; x east by the sine, y north by the cosine.
                mfVx = Math.Sin(lfYaw) * mfSpeed / 65536.0;
                mfVy = Math.Cos(lfYaw) * mfSpeed / 65536.0;
                miJumpTick = piJumpTick;
                miElapsed = 0;
                miFrame = 0;
                FalseHalts = 0;
                FirstHalt = -1;
                Climbs = 0;
                HighLandings = 0;
                Ceilings = 0;
                SlowFalls = 0;
                EverTouched = false;
                DirX = Math.Sin(lfYaw);
                DirY = Math.Cos(lfYaw);
            }

            /// <summary>The highest a jump from the ground can lift the feet: the arc's apex (Vz squared
            /// over twice the gravity, in 256ths) and a step.</summary>
            private double fJumpReach()
            {
                double lfVz = UWPlayerMotion.JumpSpeed - UWPlayerMotion.JumpEulerOffset;

                return (lfVz * lfVz / (2.0 * -UWPlayerMotion.JumpGravity * 256.0)) + (mOMotor.Step * 8);
            }

            public override void Frame(int piFineDt)
            {
                double lfTicks = Math.Min(piFineDt, UWPlayerMotion.MaxFrameTicks * UWMotionParams.FineTicksPerTick) / (double)UWMotionParams.FineTicksPerTick;

                double lfX0 = mOMotor.X;
                double lfY0 = mOMotor.Y;
                double lfZ0 = mOMotor.Zf;
                bool lbGrounded = mOMotor.Grounded;
                bool lbJumped = false;

                // On the ground the command sets the velocity at once (decision 4) and the ledge rule
                // the hold (the player's callback: below three tenths of the forward speed); the jump
                // as UWPlayerMotion.fJump in the precision mode.
                if (lbGrounded)
                {
                    mOMotor.Vx = mfVx;
                    mOMotor.Vy = mfVy;
                    mOMotor.HoldAtLedges = mfSpeed * 10.0 < miForwardSpeed * 3.0;

                    if (miJumpTick >= 0 && miElapsed >= (long)miJumpTick * UWMotionParams.FineTicksPerTick)
                    {
                        double lfVz = UWPlayerMotion.JumpSpeed;

                        if (mOMotor.Z > 0x280)
                        {
                            lfVz = (short)(lfVz * 5) / 6;

                            if (mOMotor.Z > 0x2C0)
                                lfVz = (short)((int)lfVz << 1) / 3;
                        }

                        mOMotor.Jump(lfVz - UWPlayerMotion.JumpEulerOffset, UWPlayerMotion.JumpGravity);
                        mfTakeOff = mOMotor.Zf;
                        miJumpTick = -1;
                        lbJumped = true;
                    }
                }

                double lfDx = mOMotor.Vx * lfTicks;
                double lfDy = mOMotor.Vy * lfTicks;

                mOMotor.Advance(lfTicks);
                miElapsed += piFineDt;
                EverTouched |= mOMotor.Touched;

                if (lbGrounded && !lbJumped && mOMotor.Grounded && mOMotor.Zf - lfZ0 > (mOMotor.Step * 8) + 1.0e-6)
                    Climbs++;

                if (lbGrounded && !lbJumped && !mOMotor.Grounded && mOMotor.HoldAtLedges)
                    SlowFalls++;

                if (!lbGrounded && mOMotor.Grounded && mOMotor.Zf - mfTakeOff > fJumpReach() + 1.0e-6)
                    HighLandings++;

                if (mOMotor.Grounded)
                    mfTakeOff = mOMotor.Zf;

                if (!mOMotor.Grounded && mOMotor.Zf > lfZ0 + 1.0e-6 && mOMotor.Zf + (mOMotor.Height * 8) > (UWCharacterMotor.CeilingZ * 8) + 1.0e-6)
                    Ceilings++;

                double lfWant = Math.Sqrt((lfDx * lfDx) + (lfDy * lfDy));
                double lfGot = Math.Sqrt(((mOMotor.X - lfX0) * (mOMotor.X - lfX0)) + ((mOMotor.Y - lfY0) * (mOMotor.Y - lfY0)));

                if (lbGrounded && lfWant > 0.0 && lfGot < lfWant * 0.01 && !mOMotor.StepHeld && !fHeld(lfDx / lfWant, lfDy / lfWant))
                {
                    FalseHalts++;

                    if (FirstHalt < 0)
                        FirstHalt = miFrame;
                }

                miFrame++;
            }

            /// <summary>Whether the walls met hold the direction (pfDx, pfDy): no direction leaving every
            /// one of them alone keeps more than a 50th of the motion - a wall met head-on, a corner,
            /// a wedge the slide runs into. In the plane the best such direction is the motion itself
            /// or runs along one of the walls, so those are the candidates.</summary>
            private bool fHeld(double pfDx, double pfDy)
            {
                List<double> lOContacts = mOMotor.Contacts;

                if (fFree(pfDx, pfDy, pfDx, pfDy))
                    return false;

                for (int liI = 0; liI < lOContacts.Count; liI += 2)
                {
                    double lfTx = -lOContacts[liI + 1];
                    double lfTy = lOContacts[liI];

                    if (fFree(lfTx, lfTy, pfDx, pfDy) || fFree(-lfTx, -lfTy, pfDx, pfDy))
                        return false;
                }

                return true;
            }

            /// <summary>Whether the unit direction (pfUx, pfUy) runs into none of the walls met and keeps
            /// more than a 50th of the motion (pfDx, pfDy).</summary>
            private bool fFree(double pfUx, double pfUy, double pfDx, double pfDy)
            {
                List<double> lOContacts = mOMotor.Contacts;

                if ((pfUx * pfDx) + (pfUy * pfDy) <= 0.02)
                    return false;

                for (int liAt = 0; liAt < lOContacts.Count; liAt += 2)
                {
                    if ((pfUx * lOContacts[liAt]) + (pfUy * lOContacts[liAt + 1]) < -1.0e-6)
                        return false;
                }

                return true;
            }

            public override int X => (int)Math.Floor(mOMotor.X * 256.0);

            public override int Y => (int)Math.Floor(mOMotor.Y * 256.0);

            public override int Z => mOMotor.Z;
        }

        private static void fCheckMotionSweep(CheckContext pOContext)
        {
            Check.Group("Motion sweep on the real levels (the motor plan's test bench, M0)");

            DataImport lOData = pOContext.Data;

            if (lOData == null || lOData.Levels == null || lOData.Levels.Count == 0)
            {
                Check.Pass("motion sweep", "skipped - no levels");

                return;
            }

            int liRuns = pOContext.SweepRuns > 0 ? pOContext.SweepRuns : RegularRuns;
            MotorSweepMover lOMotor = new MotorSweepMover();
            SweepMover[] lOMovers = { new CoreSweepMover(false), new CoreSweepMover(true), lOMotor };
            Dictionary<string, int> lOStuck = new Dictionary<string, int>();
            Dictionary<string, int> lOInside = new Dictionary<string, int>();
            Dictionary<string, long> lOMoved = new Dictionary<string, long>();
            Dictionary<string, int> lOTriggers = new Dictionary<string, int>();
            List<string> lOExamples = new List<string>();
            List<string> lOMotorExamples = new List<string>();
            int liTotal = 0;
            int liCompared = 0;
            int liMotorStuck = 0;
            int liMotorOverlap = 0;
            int liMotorHalts = 0;
            int liRestarts = 0;
            int liMotorBeyond = 0;
            int liTriggerRuns = 0;
            int liTriggerMismatches = 0;
            int liTriggersCompared = 0;
            int liMotorJumpsLanded = 0;
            double lfWorstClearance = double.MaxValue;

            foreach (SweepMover lOMover in lOMovers)
            {
                lOStuck[lOMover.Name] = 0;
                lOInside[lOMover.Name] = 0;
                lOMoved[lOMover.Name] = 0;
                lOTriggers[lOMover.Name] = 0;
            }

            for (int liLevel = 0; liLevel < lOData.Levels.Count && liLevel < 9; liLevel++)
            {
                UWLevel lOLevel = lOData.Levels[liLevel];
                List<int> lOOpen = fSweepOpenTiles(lOLevel);

                if (lOOpen.Count == 0)
                    continue;

                LevelMotionWorld lOWorld = new LevelMotionWorld(lOLevel, lOData.Terrain, 1);
                Dictionary<int, int> lOTileOf = fSweepObjectTiles(lOLevel);
                UWOriginalRandom lORandom = new UWOriginalRandom(7919 + liLevel);
                UWOriginalRandom lORestartRandom = new UWOriginalRandom(104729 + liLevel);
                UWOriginalRandom lOVariantRandom = new UWOriginalRandom(15485863 + liLevel);
                UWOriginalRandom lOStartRandom = new UWOriginalRandom(32452843 + liLevel);

                for (int liRun = 0; liRun < liRuns; liRun++)
                {
                    int liTile = lOOpen[lORandom.Next() % lOOpen.Count];
                    SweepStart lOStart = new SweepStart
                    {
                        Level = liLevel + 1,
                        TileX = liTile & 0x3F,
                        TileY = liTile >> 6,
                        // Inside the tile by more than the player's box (radius 2 eighths), so the
                        // start does not overlap a neighbour's wall.
                        FineX = 64 + (lORandom.Next() % 129),
                        FineY = 64 + (lORandom.Next() % 129),
                        Heading = (lORandom.Next() * 2) & 0xFFFF,
                        FreeOffset = SweepFreeOffsets[lORandom.Next() % SweepFreeOffsets.Length],
                        FrameRate = SweepFrameRates[lORandom.Next() % SweepFrameRates.Length],
                        Seed = lORandom.Next(),
                        Walk = lOVariantRandom.Next() % 6 == 0 ? UWPlayerMotion.WalkRun / 4 : UWPlayerMotion.WalkRun,
                        JumpTick = lOVariantRandom.Next() % 3 == 0 ? lOVariantRandom.Next() % (SweepRunTicks / 2) : -1
                    };

                    // Not inside an object (a doorway's closed door, a barrel): the game never puts
                    // the player there, so such a start is drawn again elsewhere.
                    for (int liTry = 0; liTry < 20 && fSweepStartInObject(lOWorld, lOData, lOStart); liTry++)
                    {
                        liTile = lOOpen[lOStartRandom.Next() % lOOpen.Count];
                        lOStart.TileX = liTile & 0x3F;
                        lOStart.TileY = liTile >> 6;
                        lOStart.FineX = 64 + (lOStartRandom.Next() % 129);
                        lOStart.FineY = 64 + (lOStartRandom.Next() % 129);
                    }

                    SweepEnd[] lOEnds = new SweepEnd[lOMovers.Length];

                    double lfClearance = 0.0;

                    for (int liMover = 0; liMover < lOMovers.Length; liMover++)
                    {
                        lOEnds[liMover] = fSweepRun(lOMovers[liMover], lOWorld, lOData, lOStart);

                        if (lOMovers[liMover] == lOMotor)
                        {
                            lfClearance = lOMotor.Motor.Clearance();

                            if (lOMotor.FalseHalts > 0)
                            {
                                liMotorHalts++;

                                if (lOMotorExamples.Count < 40)
                                    lOMotorExamples.Add("halt    Modern " + lOMotor.FalseHalts + " frames from frame " + lOMotor.FirstHalt + "  " + lOStart);
                            }

                            if (lOMotor.Climbs + lOMotor.HighLandings + lOMotor.Ceilings + lOMotor.SlowFalls > 0)
                            {
                                liMotorBeyond++;

                                if (lOMotorExamples.Count < 40)
                                    lOMotorExamples.Add("beyond  Modern climbs " + lOMotor.Climbs + ", high landings " + lOMotor.HighLandings + ", ceilings " + lOMotor.Ceilings + ", slow falls " + lOMotor.SlowFalls + "  " + lOStart);
                            }

                            if (lOMotor.Motor.Grounded)
                                liMotorJumpsLanded += lOStart.JumpTick >= 0 ? 1 : 0;

                            // THE RESTARTS: from where the run ended - mostly at a wall - the motor
                            // sets off again in new directions, at frame rates up to the smallest
                            // steps, as a player turning at a wall and walking on.
                            if (lOMotor.Motor.Touched && lOMotor.Motor.Grounded)
                            {
                                double lfEndX = lOMotor.Motor.X;
                                double lfEndY = lOMotor.Motor.Y;
                                double lfEndZ = lOMotor.Motor.Zf;

                                for (int liAgain = 0; liAgain < SweepRestarts; liAgain++)
                                {
                                    int liHeading = (lORestartRandom.Next() * 2) & 0xFFFF;
                                    int liOffset = SweepFreeOffsets[lORestartRandom.Next() % SweepFreeOffsets.Length];
                                    int liRate = SweepRestartRates[lORestartRandom.Next() % SweepRestartRates.Length];
                                    int liDt = (UWMotionParams.FineTicksPerTick * 256) / liRate;

                                    lOMotor.Motor.Place(lfEndX, lfEndY, lfEndZ);
                                    lOMotor.Aim(liHeading, liOffset, UWPlayerMotion.WalkRun, -1);
                                    liRestarts++;

                                    for (int liTime = 0; liTime < SweepRestartTicks * UWMotionParams.FineTicksPerTick; liTime += liDt)
                                        lOMotor.Frame(liDt);

                                    if (lOMotor.FalseHalts > 0)
                                    {
                                        liMotorHalts++;

                                        if (lOMotorExamples.Count < 40)
                                            lOMotorExamples.Add("halt    Modern restart at " + lfEndX.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture) + "/" + lfEndY.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture)
                                                + " heading " + liHeading.ToString("X4") + (liOffset < 0 ? " walk" : " free " + liOffset.ToString("X4")) + " " + liRate + "fps, "
                                                + lOMotor.FalseHalts + " frames from frame " + lOMotor.FirstHalt + "  after " + lOStart);
                                    }

                                    if (lOMotor.Motor.Clearance() < -1.0 / 256.0)
                                    {
                                        liMotorOverlap++;

                                        if (lOMotorExamples.Count < 40)
                                            lOMotorExamples.Add("overlap Modern restart heading " + liHeading.ToString("X4") + " after " + lOStart);
                                    }
                                }
                            }
                        }
                    }

                    liTotal++;

                    for (int liMover = 0; liMover < lOMovers.Length; liMover++)
                    {
                        lOMoved[lOMovers[liMover].Name] += lOEnds[liMover].Moved;
                        lOTriggers[lOMovers[liMover].Name] += lOEnds[liMover].Triggers;

                        if (lOEnds[liMover].Moved < SweepStuckFine)
                            lOStuck[lOMovers[liMover].Name]++;

                        if (lOEnds[liMover].Inside)
                        {
                            lOInside[lOMovers[liMover].Name]++;

                            if (lOMotorExamples.Count < 40)
                                lOMotorExamples.Add("inside  " + lOMovers[liMover].Name + "  " + lOStart);
                        }
                    }

                    // THE TRIGGERS: where the motor walked a straight line and the precision mode ended
                    // on it, both took the same path, so on the stretch they share they must meet the
                    // same move triggers - up to the band where the core's square box reaches beyond
                    // the motor's round one.
                    if (lOStart.JumpTick < 0 && lOStart.Walk == UWPlayerMotion.WalkRun && !lOMotor.EverTouched)
                    {
                        int liTriggersMet;
                        int liMismatch = fSweepTriggerMismatch(lOLevel, lOTileOf, lOData, lOStart, lOMotor, lOEnds[1], lOEnds[2], out liTriggersMet);

                        liTriggersCompared += liTriggersMet;

                        if (liMismatch >= 0)
                        {
                            liTriggerRuns++;

                            if (liMismatch > 0)
                            {
                                liTriggerMismatches++;

                                if (lOMotorExamples.Count < 40)
                                    lOMotorExamples.Add("trigger Modern " + liMismatch + " mismatched on the shared line  " + lOStart);
                            }
                        }
                    }

                    // Smooth stuck where Original moved, or the reverse: the two modes of one core.
                    bool lbOriginalStuck = lOEnds[0].Moved < SweepStuckFine;
                    bool lbSmoothStuck = lOEnds[1].Moved < SweepStuckFine;

                    // The motor stuck where either mode of the core moved, or ending in a wall.
                    if (lOEnds[2].Moved < SweepStuckFine && (!lbOriginalStuck || !lbSmoothStuck))
                    {
                        liMotorStuck++;

                        if (lOMotorExamples.Count < 40)
                            lOMotorExamples.Add("stuck   Modern, the core moved  " + lOStart);
                    }

                    if (lfClearance < -1.0 / 256.0)
                    {
                        liMotorOverlap++;

                        if (lOMotorExamples.Count < 40)
                            lOMotorExamples.Add("overlap Modern " + lfClearance.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) + "  " + lOStart);
                    }

                    lfWorstClearance = Math.Min(lfWorstClearance, lfClearance);

                    if (lbOriginalStuck != lbSmoothStuck)
                    {
                        liCompared++;

                        if (lOExamples.Count < 40)
                            lOExamples.Add((lbSmoothStuck ? "stuck   Smooth, Original moved  " : "stuck   Original, Smooth moved  ") + lOStart);
                    }
                }
            }

            // THE KNOWN SPOTS (per user, 2026-10-27, SAVE3): where the modern attempts stuck, each
            // walked straight on at 60 frames a second; every mover's distance is reported.
            SweepStart[] lOKnown =
            {
                new SweepStart { Level = 3, TileX = 16, TileY = 50, FineX = 18, FineY = 96, Heading = 0xC19F, FreeOffset = -1, FrameRate = 60, Seed = 1, Walk = UWPlayerMotion.WalkRun, JumpTick = -1 },
                new SweepStart { Level = 3, TileX = 44, TileY = 52, FineX = 111, FineY = 235, Heading = 0x564B, FreeOffset = -1, FrameRate = 60, Seed = 2, Walk = UWPlayerMotion.WalkRun, JumpTick = -1 },
                new SweepStart { Level = 3, TileX = 3, TileY = 5, FineX = 127, FineY = 78, Heading = 0x7EA6, FreeOffset = -1, FrameRate = 60, Seed = 3, Walk = UWPlayerMotion.WalkRun, JumpTick = -1 }
            };
            StringBuilder lOKnownText = new StringBuilder();

            foreach (SweepStart lOKnownStart in lOKnown)
            {
                if (lOData.Levels.Count < lOKnownStart.Level)
                    continue;

                LevelMotionWorld lOKnownWorld = new LevelMotionWorld(lOData.Levels[lOKnownStart.Level - 1], lOData.Terrain, 1);

                lOKnownText.Append(lOKnownText.Length > 0 ? "; " : string.Empty).Append(lOKnownStart.TileX).Append('/').Append(lOKnownStart.TileY);

                foreach (SweepMover lOMover in lOMovers)
                    lOKnownText.Append(' ').Append(lOMover.Name).Append(' ')
                        .Append((fSweepRun(lOMover, lOKnownWorld, lOData, lOKnownStart).Moved / 256.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }

            Check.Pass("motion sweep: the known spots (tiles moved in two seconds)", lOKnownText.ToString());

            // THE TRIGGER PASS: every move trigger of every level approached in straight lines from
            // several headings and side offsets, the precision mode against the motor.
            int liPassRuns = 0;
            int liPassTriggers = 0;
            int liPassMismatches = 0;
            int liApproaches = pOContext.SweepRuns > 0 ? TriggerApproaches : 1;

            for (int liLevel = 0; liLevel < lOData.Levels.Count && liLevel < 9; liLevel++)
            {
                UWLevel lOLevel = lOData.Levels[liLevel];
                LevelMotionWorld lOWorld = new LevelMotionWorld(lOLevel, lOData.Terrain, 1);
                Dictionary<int, int> lOTileOf = fSweepObjectTiles(lOLevel);
                UWOriginalRandom lORandom = new UWOriginalRandom(49979687 + liLevel);
                CoreSweepMover lOCore = new CoreSweepMover(true);
                LevelMotionWorld lOCoreWorld = new LevelMotionWorld(lOLevel, lOData.Terrain, 1);
                LevelMotionWorld lOMotorWorld = new LevelMotionWorld(lOLevel, lOData.Terrain, 1);

                foreach (KeyValuePair<int, int> lOPair in lOTileOf)
                {
                    UWObject lOTrigger = lOLevel.Masterlist[lOPair.Key];

                    if (lOTrigger == null || lOTrigger.ID != UWObjectMechanics.MoveTriggerId)
                        continue;

                    double lfBx = (lOPair.Value & 0x3F) + ((lOTrigger.XPos + 0.5) / 8.0);
                    double lfBy = (lOPair.Value >> 6) + ((lOTrigger.YPos + 0.5) / 8.0);

                    for (int liAt = 0; liAt < liApproaches; liAt++)
                    {
                        int liHeading = (lORandom.Next() * 2) & 0xFFFF;
                        double lfOffset = (((lORandom.Next() % 1001) / 1000.0) - 0.5) * 0.7;
                        double lfYaw = liHeading * (Math.PI * 2.0 / 65536.0);
                        double lfDx = Math.Sin(lfYaw);
                        double lfDy = Math.Cos(lfYaw);
                        double lfSx = lfBx - (lfDx * 1.5) + (lfDy * lfOffset);
                        double lfSy = lfBy - (lfDy * 1.5) - (lfDx * lfOffset);
                        int liTx = (int)Math.Floor(lfSx);
                        int liTy = (int)Math.Floor(lfSy);
                        UWTile lOTile = lOLevel.GetTile(liTx, liTy);

                        if (lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
                            continue;

                        SweepStart lOStart = new SweepStart
                        {
                            Level = liLevel + 1,
                            TileX = liTx,
                            TileY = liTy,
                            FineX = (int)((lfSx - liTx) * 256.0),
                            FineY = (int)((lfSy - liTy) * 256.0),
                            Heading = liHeading,
                            FreeOffset = -1,
                            FrameRate = 144,
                            Seed = 1,
                            Walk = UWPlayerMotion.WalkRun,
                            JumpTick = -1
                        };

                        if (fSweepStartInObject(lOWorld, lOData, lOStart))
                            continue;

                        int liMet;
                        int liMismatch = fSweepTriggerApproach(lOLevel, lOTileOf, lOData, lOStart, lOCoreWorld, lOMotorWorld, lOCore, lOMotor, out liMet);

                        if (liMismatch < 0)
                            continue;

                        liPassRuns++;
                        liPassTriggers += liMet;

                        if (liMismatch > 0)
                        {
                            liPassMismatches++;

                            if (lOMotorExamples.Count < 40)
                                lOMotorExamples.Add("trigger Modern " + liMismatch + " mismatched approaching a move trigger  " + lOStart);
                        }
                    }
                }
            }

            string lsPass = liPassRuns + " straight approaches, " + liPassTriggers + " triggers on the shared line, mismatched " + liPassMismatches;

            Check.Pass("motion sweep: the trigger pass", lsPass);
            Check.Int("motion sweep: approaching each move trigger, the motor meets what the core meets", liPassMismatches, 0);

            StringBuilder lOSummary = new StringBuilder();

            lOSummary.Append(liTotal).Append(" runs");

            foreach (SweepMover lOMover in lOMovers)
                lOSummary.Append("; ").Append(lOMover.Name).Append(" stuck ").Append(lOStuck[lOMover.Name])
                    .Append(", inside ").Append(lOInside[lOMover.Name])
                    .Append(", ").Append((lOMoved[lOMover.Name] / (double)Math.Max(1, liTotal) / 256.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(" tiles a run, ").Append(lOTriggers[lOMover.Name]).Append(" triggers");

            lOSummary.Append("; the two modes disagree on ").Append(liCompared);
            lOSummary.Append("; Modern restarted at a wall ").Append(liRestarts).Append(" times");
            lOSummary.Append("; Modern stuck where the core moved ").Append(liMotorStuck)
                .Append(", halting without a head-on wall or a corner ").Append(liMotorHalts)
                .Append(", beyond the original's reach ").Append(liMotorBeyond)
                .Append(", triggers compared on ").Append(liTriggerRuns).Append(" straight runs (").Append(liTriggersCompared).Append(" triggers on the shared line), mismatched ").Append(liTriggerMismatches)
                .Append(", jump runs ended on the ground ").Append(liMotorJumpsLanded)
                .Append(", overlapping a wall ").Append(liMotorOverlap)
                .Append(" (worst clearance ").Append(lfWorstClearance.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)).Append(" tiles)");

            Check.Pass("motion sweep: the movers on the real levels", lOSummary.ToString());
            Check.Int("motion sweep: the motor never stuck where the core moved", liMotorStuck, 0);
            Check.Int("motion sweep: the motor never overlaps a wall", liMotorOverlap, 0);
            Check.Int("motion sweep: the motor halts only head-on or in a corner", liMotorHalts, 0);
            Check.Int("motion sweep: the motor meets the move triggers the core meets on the same straight path", liTriggerMismatches, 0);
            Check.Int("motion sweep: the motor climbs no more than a step, lands no higher than a jump reaches, keeps its head under the ceiling, holds a slow player at ledges", liMotorBeyond, 0);
            Check.Int("motion sweep: the motor never ends in rock", lOInside[lOMotor.Name], 0);

            if (pOContext.SweepRuns > 0)
            {
                Console.WriteLine();
                Console.WriteLine("MOTION SWEEP: " + lOSummary);
                Console.WriteLine("KNOWN SPOTS: " + lOKnownText);
                Console.WriteLine("TRIGGER PASS: " + lsPass);

                foreach (string lsExample in lOMotorExamples)
                    Console.WriteLine("  " + lsExample);

                foreach (string lsExample in lOExamples)
                    Console.WriteLine("  " + lsExample);

                Console.WriteLine();
            }
        }

        /// <summary>The move triggers on a straight path, the motor's against the precision mode's
        /// (-1: the core did not end on the motor's line, no comparison). On the shared stretch -
        /// up to where the core ended, less a margin - a trigger the motor met must lie within its
        /// reach (ObjectPad and a sampling tolerance) and the core must have met it too; one the
        /// core met that lies clearly within the motor's reach the motor must have met. Between the
        /// two lies the band where the core's square box reaches beyond the round body.</summary>
        private static int fSweepTriggerMismatch(UWLevel pOLevel, Dictionary<int, int> pOTileOf, DataImport pOData, SweepStart pOStart, MotorSweepMover pOMotor, SweepEnd pOCore, SweepEnd pOModern, out int piCompared)
        {
            piCompared = 0;

            const double lfMargin = 0.375;
            double lfX0 = pOStart.TileX + (pOStart.FineX / 256.0);
            double lfY0 = pOStart.TileY + (pOStart.FineY / 256.0);
            double lfCx = (pOCore.X / 256.0) - lfX0;
            double lfCy = (pOCore.Y / 256.0) - lfY0;
            double lfAlong = (lfCx * pOMotor.DirX) + (lfCy * pOMotor.DirY);
            double lfAcross = Math.Abs((lfCx * pOMotor.DirY) - (lfCy * pOMotor.DirX));

            if (lfAcross > 1.0 / 64.0 || lfAlong < 2.0 * lfMargin)
                return -1;

            return fSweepTriggerCompare(pOLevel, pOTileOf, pOData, lfX0, lfY0, pOMotor.DirX, pOMotor.DirY, lfAlong, pOCore.TriggerList, pOModern.TriggerList, out piCompared);
        }

        /// <summary>The comparison itself: the triggers the core and the motor met, both along the
        /// line from (pfX0, pfY0) in direction (pfDx, pfDy) for pfAlong tiles; only triggers nearest
        /// the line before pfAlong less a margin count.</summary>
        private static int fSweepTriggerCompare(UWLevel pOLevel, Dictionary<int, int> pOTileOf, DataImport pOData, double pfX0, double pfY0,
            double pfDx, double pfDy, double pfAlong, List<int> pOCoreList, List<int> pOModernList, out int piCompared)
        {
            const double lfTolerance = 1.0 / 32.0;
            const double lfMargin = 0.375;
            double lfX0 = pfX0;
            double lfY0 = pfY0;
            double lfAlong = pfAlong;
            double lfShared = lfAlong - lfMargin;
            List<int> lOAll = new List<int>(pOCoreList);

            piCompared = 0;

            foreach (int liIndex in pOModernList)
            {
                if (!lOAll.Contains(liIndex))
                    lOAll.Add(liIndex);
            }

            int liMismatch = 0;

            foreach (int liIndex in lOAll)
            {
                UWObject lOTrigger = pOLevel.Masterlist[liIndex];
                UWCommonObjectProperties.Entry lOEntry;
                int liTile;

                if (lOTrigger == null || !pOData.CommonObjectProperties.TryGet(lOTrigger.ID, out lOEntry) || !pOTileOf.TryGetValue(liIndex, out liTile))
                    continue;

                double lfHalf = ((lOEntry.Radius & 7) + 0.5) / 8.0;
                double lfBx = (liTile & 0x3F) + ((lOTrigger.XPos + 0.5) / 8.0);
                double lfBy = (liTile >> 6) + ((lOTrigger.YPos + 0.5) / 8.0);
                double lfNearest = double.MaxValue;
                double lfWhere = 0.0;

                // The box's distance to the shared stretch, sampled every 64th of a tile.
                for (double lfT = 0.0; lfT <= lfAlong + 1.0e-9; lfT += 1.0 / 64.0)
                {
                    double lfPx = lfX0 + (pfDx * lfT);
                    double lfPy = lfY0 + (pfDy * lfT);
                    double lfDx = Math.Max(0.0, Math.Abs(lfPx - lfBx) - lfHalf);
                    double lfDy = Math.Max(0.0, Math.Abs(lfPy - lfBy) - lfHalf);
                    double lfDist = Math.Sqrt((lfDx * lfDx) + (lfDy * lfDy));

                    if (lfDist < lfNearest)
                    {
                        lfNearest = lfDist;
                        lfWhere = lfT;
                    }
                }

                if (lfWhere > lfShared)
                    continue;

                piCompared++;

                bool lbCore = pOCoreList.Contains(liIndex);
                bool lbModern = pOModernList.Contains(liIndex);

                if (lbModern && (!lbCore || lfNearest > UWCharacterMotor.ObjectPad + lfTolerance))
                    liMismatch++;
                else if (lbCore && !lbModern && lfNearest < UWCharacterMotor.ObjectPad - lfTolerance)
                    liMismatch++;
            }

            return liMismatch;
        }

        /// <summary>One straight approach of the trigger pass: the precision mode and the motor
        /// side by side in worlds of their own, frame by frame at 144 a second, until the motor
        /// meets anything or the core leaves the line (a deflection); then the triggers each met,
        /// compared over the stretch both walked (-1: too short to compare).</summary>
        private static int fSweepTriggerApproach(UWLevel pOLevel, Dictionary<int, int> pOTileOf, DataImport pOData, SweepStart pOStart,
            LevelMotionWorld pOCoreWorld, LevelMotionWorld pOMotorWorld, CoreSweepMover pOCore, MotorSweepMover pOMotor, out int piCompared)
        {
            const double lfMargin = 0.375;
            double lfX0 = pOStart.TileX + (pOStart.FineX / 256.0);
            double lfY0 = pOStart.TileY + (pOStart.FineY / 256.0);
            int liFineDt = (UWMotionParams.FineTicksPerTick * 256) / pOStart.FrameRate;
            double lfCoreAlong = 0.0;
            double lfMotorAlong = 0.0;

            piCompared = 0;
            pOCoreWorld.Reset(pOStart.Seed);
            pOMotorWorld.Reset(pOStart.Seed);
            pOCore.Place(pOCoreWorld, pOData, pOStart);
            pOMotor.Place(pOMotorWorld, pOData, pOStart);

            for (int liFrame = 0; liFrame < 2 * pOStart.FrameRate; liFrame++)
            {
                pOCore.Frame(liFineDt);
                pOMotor.Frame(liFineDt);

                double lfCx = (pOCore.X / 256.0) - lfX0;
                double lfCy = (pOCore.Y / 256.0) - lfY0;

                if (pOMotor.EverTouched || Math.Abs((lfCx * pOMotor.DirY) - (lfCy * pOMotor.DirX)) > 1.0 / 64.0)
                    break;

                lfCoreAlong = (lfCx * pOMotor.DirX) + (lfCy * pOMotor.DirY);
                lfMotorAlong = ((pOMotor.Motor.X - lfX0) * pOMotor.DirX) + ((pOMotor.Motor.Y - lfY0) * pOMotor.DirY);
            }

            double lfShared = Math.Min(lfCoreAlong, lfMotorAlong);

            if (lfShared < 2.0 * lfMargin)
                return -1;

            // Only what each met up to the shared length counts: the triggers beyond it are cut by
            // the comparison's margin.
            return fSweepTriggerCompare(pOLevel, pOTileOf, pOData, lfX0, lfY0, pOMotor.DirX, pOMotor.DirY, lfShared,
                pOCoreWorld.Triggers, pOMotorWorld.Triggers, out piCompared);
        }

        /// <summary>The tile of every object lying in one, by master-list index (y * 64 + x).</summary>
        private static Dictionary<int, int> fSweepObjectTiles(UWLevel pOLevel)
        {
            Dictionary<UWObject, int> lOIndex = new Dictionary<UWObject, int>();
            Dictionary<int, int> lOTileOf = new Dictionary<int, int>();

            for (int liAt = 0; liAt < pOLevel.Masterlist.Count; liAt++)
            {
                if (pOLevel.Masterlist[liAt] != null && !lOIndex.ContainsKey(pOLevel.Masterlist[liAt]))
                    lOIndex[pOLevel.Masterlist[liAt]] = liAt;
            }

            for (int liY = 0; liY < 64; liY++)
            {
                for (int liX = 0; liX < 64; liX++)
                {
                    UWTile lOTile = pOLevel.GetTile(liX, liY);
                    int liIndex;

                    if (lOTile == null || lOTile.ObjectsInTile == null)
                        continue;

                    foreach (UWObject lOObject in lOTile.ObjectsInTile)
                    {
                        if (lOObject != null && lOIndex.TryGetValue(lOObject, out liIndex))
                            lOTileOf[liIndex] = (liY << 6) + liX;
                    }
                }
            }

            return lOTileOf;
        }

        /// <summary>Whether the body would start inside an object's box (or nearer to it than it keeps).</summary>
        private static bool fSweepStartInObject(LevelMotionWorld pOWorld, DataImport pOData, SweepStart pOStart)
        {
            UWCharacterMotor lOMotor = new UWCharacterMotor(pOWorld, pOData.CommonObjectProperties);

            lOMotor.PlaceOnFloor(pOStart.TileX + (pOStart.FineX / 256.0), pOStart.TileY + (pOStart.FineY / 256.0));

            return lOMotor.Clearance() < 0.0;
        }

        /// <summary>The level's open tiles (type 1), as y * 64 + x.</summary>
        private static List<int> fSweepOpenTiles(UWLevel pOLevel)
        {
            List<int> lOOpen = new List<int>();

            for (int liY = 1; liY < 63; liY++)
            {
                for (int liX = 1; liX < 63; liX++)
                {
                    UWTile lOTile = pOLevel.GetTile(liX, liY);

                    if (lOTile != null && lOTile.TileType == UWTile.TileTypeEnum.open)
                        lOOpen.Add((liY << 6) + liX);
                }
            }

            return lOOpen;
        }

        /// <summary>One run of one mover: two seconds of frames at the run's rate.</summary>
        private static SweepEnd fSweepRun(SweepMover pOMover, LevelMotionWorld pOWorld, DataImport pOData, SweepStart pOStart)
        {
            pOWorld.Reset(pOStart.Seed);
            pOMover.Place(pOWorld, pOData, pOStart);

            int liX0 = pOMover.X;
            int liY0 = pOMover.Y;
            int liFineDt = (UWMotionParams.FineTicksPerTick * 256) / pOStart.FrameRate;
            int liElapsed = 0;
            int liMoved = 0;

            while (liElapsed < SweepRunTicks * UWMotionParams.FineTicksPerTick)
            {
                pOMover.Frame(liFineDt);
                liElapsed += liFineDt;
                liMoved = Math.Max(liMoved, Math.Abs(pOMover.X - liX0) + Math.Abs(pOMover.Y - liY0));
            }

            return new SweepEnd
            {
                X = pOMover.X,
                Y = pOMover.Y,
                Z = pOMover.Z,
                Moved = liMoved,
                Inside = fSweepInside(pOWorld, pOMover.X, pOMover.Y, pOMover.Z),
                Triggers = pOWorld.Triggers.Count,
                TriggerList = new List<int>(pOWorld.Triggers)
            };
        }

        /// <summary>Whether a centre lies in rock: a solid tile, the closed half of a diagonal by more
        /// than a 32nd of a tile, or a floor more than a level above the feet.</summary>
        private static bool fSweepInside(LevelMotionWorld pOWorld, int piX, int piY, int piZ)
        {
            int liType;
            int liFloor;
            int liTerrain;

            if (!pOWorld.TryGetTile(piX >> 8, piY >> 8, out liType, out liFloor, out liTerrain) || liType == 0)
                return true;

            if (liFloor * 0x40 > piZ + 0x40)
                return true;

            int liU = piX & 0xFF;
            int liV = piY & 0xFF;
            const int liMargin = 8;

            switch (liType)
            {
                case 2: return liV > liU + liMargin;
                case 3: return liU + liV > 0x100 + liMargin;
                case 4: return liU + liV < 0x100 - liMargin;
                case 5: return liV < liU - liMargin;
                default: return false;
            }
        }
    }
}
