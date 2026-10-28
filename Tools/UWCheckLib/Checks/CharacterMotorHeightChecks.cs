using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE CHARACTER MOTOR'S HEIGHTS (Docs/MOTOR-PLAN.md sections 5.1 and 5.4, M2, 2026-10-27): the
    /// jump envelope measured against the original core in the same run - the apex of a standing
    /// jump against the original's (whole 8-tick steps), the reach of a running jump against the
    /// precision mode's at the same speed, with and without Leap - and the heights by hand: a slow
    /// player held at a ledge, a fast one falling off it and landing, a step down without a fall,
    /// a jump onto a ledge three levels up and not four, a slope, the ceiling. Frames of 144 a
    /// second (455 fine ticks), as the game runs.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>A frame of 144 a second in ticks.</summary>
        private const double MotorFrameTicks = 455.0 / 256.0;

        private static void fCheckCharacterMotorHeights(DataImport pOData)
        {
            Check.Group("Character motor: heights, ledges and jumps (M2)");

            fCheckMotorJumpEnvelope(pOData, false);
            fCheckMotorJumpEnvelope(pOData, true);

            double lfJumpVz = UWPlayerMotion.JumpSpeed - UWPlayerMotion.JumpEulerOffset;

            // A drop of two levels: a creep holds with the centre at the edge, a run falls and lands.
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            lOWorld.Paint(12, 10, 20, 20, 1, 0, 0);

            double lfMaxZ;
            bool lbAirborne;
            double lfLanded;
            UWCharacterMotor lOMotor = fMotorRun(lOWorld, 9.5, 15.5, 0.004, 0.0, true, 0.0, 0.0, 400, out lfMaxZ, out lbAirborne, out lfLanded);

            Check.Bool("motor heights: a creep holds at a ledge of two levels, on the ground", lOMotor.Grounded && !lbAirborne, true);
            Check.Near("motor heights: ... the centre at the edge", lOMotor.X, 12.0 - UWCharacterMotor.Skin, 1.0e-9);
            Check.Near("motor heights: ... at its height", lOMotor.Zf, 2 * 0x40, 1.0e-9);

            lOMotor = fMotorRun(lOWorld, 9.5, 15.5, 0.0125, 0.0, false, 0.0, 0.0, 400, out lfMaxZ, out lbAirborne, out lfLanded);
            Check.Bool("motor heights: a run falls off it and lands below", lbAirborne && lOMotor.Grounded && lOMotor.Zf == 0.0 && lfLanded < 0.0, true);

            // A step down of one level: followed on the ground, no fall.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(12, 10, 20, 20, 1, 1, 0);
            lOMotor = fMotorRun(lOWorld, 9.5, 15.5, 0.0125, 0.0, false, 0.0, 0.0, 400, out lfMaxZ, out lbAirborne, out lfLanded);
            Check.Bool("motor heights: a step down of one level, on the ground all the way", !lbAirborne && lOMotor.Zf == 0x40, true);

            // A running jump at a ledge three levels up lands on it; four levels up it does not.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(14, 10, 20, 20, 1, 5, 0);
            lOMotor = fMotorRun(lOWorld, 12.5, 15.5, 0.0125, 0.0, false, lfJumpVz, UWPlayerMotion.JumpGravity, 400, out lfMaxZ, out lbAirborne, out lfLanded);
            Check.Bool("motor heights: a running jump lands on a ledge three levels up", lOMotor.Grounded && lOMotor.Zf == 5 * 0x40 && lOMotor.X > 14.0 - UWCharacterMotor.DefaultRadius, true);

            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(14, 10, 20, 20, 1, 6, 0);
            lOMotor = fMotorRun(lOWorld, 12.5, 15.5, 0.0125, 0.0, false, lfJumpVz, UWPlayerMotion.JumpGravity, 400, out lfMaxZ, out lbAirborne, out lfLanded);
            Check.Bool("motor heights: ... and not on one four levels up", lOMotor.Grounded && lOMotor.Zf == 2 * 0x40 && lOMotor.X < 14.0 - UWCharacterMotor.DefaultRadius, true);

            // A slope rising north from floor 2 to the floor 3 beyond it: its height at the centre,
            // walked up on the ground.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(10, 15, 10, 15, 6, 2, 0);
            lOWorld.Paint(10, 16, 10, 20, 1, 3, 0);
            lOMotor = new UWCharacterMotor(lOWorld);
            lOMotor.PlaceOnFloor(10.5, 15.5);
            Check.Near("motor heights: a slope's height at the centre (floor * 0x40 + across >> 2)", lOMotor.Zf, (2 * 0x40) + (128 >> 2), 1.0e-9);

            lOMotor = fMotorRun(lOWorld, 10.5, 14.5, 0.0, 0.0125, false, 0.0, 0.0, 300, out lfMaxZ, out lbAirborne, out lfLanded);
            Check.Bool("motor heights: up the slope on the ground onto the floor above", !lbAirborne && lOMotor.Zf == 3 * 0x40 && lOMotor.Y > 17.0, true);

            // The ceiling: a jump from floor 12 stops with the head at coarse 0x80.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(5, 5, 8, 8, 1, 12, 0);
            lOMotor = fMotorRun(lOWorld, 6.5, 6.5, 0.0, 0.0, false, lfJumpVz, UWPlayerMotion.JumpGravity, 400, out lfMaxZ, out lbAirborne, out lfLanded);
            Check.Near("motor heights: a jump under the ceiling stops with the head at it", lfMaxZ, (UWCharacterMotor.CeilingZ - UWCharacterMotor.DefaultHeight) * 8, 1.0e-9);
            Check.Bool("motor heights: ... and lands back on its floor", lOMotor.Grounded && lOMotor.Zf == 12 * 0x40, true);
        }

        /// <summary>The motor's jump against the core's: the standing apex against the original's
        /// (8-tick steps) within 6 fine z, as the precision mode is held to; the running reach
        /// against the precision mode's at 144 frames a second, per unit of speed, within 1.5 per cent.</summary>
        private static void fCheckMotorJumpEnvelope(DataImport pOData, bool pbLeap)
        {
            string lsName = pbLeap ? "with Leap" : "without Leap";
            int liGravity = pbLeap ? UWPlayerMotion.LeapGravity : UWPlayerMotion.JumpGravity;
            double lfJumpVz = UWPlayerMotion.JumpSpeed - UWPlayerMotion.JumpEulerOffset;
            FakeMotionWorld lOWorld = new FakeMotionWorld();

            // The original's standing apex.
            UWPlayerMotion lOCore = fMakePrecisePlayer(lOWorld, pOData, 4, 16, false);

            if (pbLeap)
                lOCore.Abilities |= UWPlayerMotion.AbilityLeap;

            int liZ0 = lOCore.Params.Z;
            int liApex = fCoreJump(lOCore, 8);

            // The precision mode's running reach: the run at its full speed first.
            UWPlayerMotion lOSmooth = fMakeFinePlayer(lOWorld, pOData, 2, 16);

            lOSmooth.PlaceAt((2 << 8) + 128, (16 << 8) + 128, lOSmooth.Params.Z, 0x4000);

            if (pbLeap)
                lOSmooth.Abilities |= UWPlayerMotion.AbilityLeap;

            for (int liAt = 0; liAt < 120; liAt++)
            {
                lOSmooth.MotionCommand = UWPlayerMotion.Command.Walk;
                lOSmooth.Walk = UWPlayerMotion.WalkRun;
                lOSmooth.Frame(455);
            }

            int liSpeed = lOSmooth.Params.Speed;
            int liX0 = lOSmooth.Params.X;

            fCoreJump(lOSmooth, 455);

            double lfSmoothReach = (lOSmooth.Params.X - liX0) / (double)liSpeed;

            // The motor: the same take-off and gravity, the arc exact.
            double lfMaxZ;
            bool lbAirborne;
            double lfLanded;
            UWCharacterMotor lOMotor = fMotorRun(lOWorld, 4.5, 16.5, 0.0, 0.0, false, lfJumpVz, liGravity, 2000, out lfMaxZ, out lbAirborne, out lfLanded);

            Check.Bool("motor jump " + lsName + ": the standing apex within 6 fine z of the original's (" + (liApex - liZ0) + ", motor "
                + (lfMaxZ - lOMotor.Zf).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ")", Math.Abs((lfMaxZ - lOMotor.Zf) - (liApex - liZ0)) <= 6.0, true);

            lOMotor = fMotorRun(lOWorld, 2.5, 16.5, liSpeed / 65536.0, 0.0, false, lfJumpVz, liGravity, 2000, out lfMaxZ, out lbAirborne, out lfLanded);

            double lfMotorReach = (lOMotor.X - 2.5) * 256.0 / liSpeed;

            Check.Bool("motor jump " + lsName + ": the running reach within 1.5 per cent of the precision mode's ("
                + (lfSmoothReach * liSpeed).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " fine, motor "
                + (lfMotorReach * liSpeed).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + ")", Math.Abs(lfMotorReach / lfSmoothReach - 1.0) <= 0.015, true);
        }

        /// <summary>The core's jump from where it stands, frames of piDt until it is down again; the
        /// apex in fine z.</summary>
        private static int fCoreJump(UWPlayerMotion pOPlayer, int piDt)
        {
            pOPlayer.MotionCommand = UWPlayerMotion.Command.Jump;
            pOPlayer.Frame(piDt);

            int liApex = pOPlayer.Params.Z;

            for (int liAt = 0; liAt < 3000 && (pOPlayer.Params.Gravity != 0 || pOPlayer.IsAirborne || pOPlayer.Params.Vz != 0); liAt++)
            {
                pOPlayer.MotionCommand = UWPlayerMotion.Command.None;
                pOPlayer.Frame(piDt);
                liApex = Math.Max(liApex, pOPlayer.Params.Z);
            }

            return liApex;
        }

        /// <summary>The motor placed on the floor at (pfX, pfY), given a velocity in tiles a tick
        /// (held on the ground, kept in the air) and, with a vertical speed, a jump at once; then
        /// frames of 144 a second, ending early once a jump is down again. The highest feet, whether
        /// it left the ground, the speed of its last landing.</summary>
        private static UWCharacterMotor fMotorRun(FakeMotionWorld pOWorld, double pfX, double pfY, double pfVx, double pfVy, bool pbHold,
            double pfJumpVz, double pfGravity, int piFrames, out double pfMaxZ, out bool pbAirborne, out double pfLanded,
            UWCommonObjectProperties pOProps = null)
        {
            UWCharacterMotor lOMotor = new UWCharacterMotor(pOWorld, pOProps);

            lOMotor.PlaceOnFloor(pfX, pfY);
            lOMotor.HoldAtLedges = pbHold;
            lOMotor.Vx = pfVx;
            lOMotor.Vy = pfVy;
            pfMaxZ = lOMotor.Zf;
            pbAirborne = false;
            pfLanded = 0.0;

            if (pfJumpVz != 0.0)
                lOMotor.Jump(pfJumpVz, pfGravity);

            for (int liAt = 0; liAt < piFrames; liAt++)
            {
                lOMotor.Advance(MotorFrameTicks);
                pfMaxZ = Math.Max(pfMaxZ, lOMotor.Zf);

                if (!lOMotor.Grounded)
                    pbAirborne = true;

                if (lOMotor.LandedVz != 0.0)
                    pfLanded = lOMotor.LandedVz;

                if (pfJumpVz != 0.0 && lOMotor.Grounded)
                    break;
            }

            return lOMotor;
        }
    }
}
