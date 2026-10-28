using System;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE CHARACTER MOTOR'S GEOMETRY (Docs/MOTOR-PLAN.md section 5.1, M1, 2026-10-27): each kind
    /// of contact on a small map against a hand-computed end - a wall met head-on, a long wall of
    /// many tiles slid along without a seam, a corner, a block's edge passed at the exact distance,
    /// a diagonal, a corridor, a step and a floor too high. Each walk is cut into many small steps,
    /// as the precision mode's frames, and once more into few, which must end at the same place.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckCharacterMotor()
        {
            Check.Group("Character motor: walls, corners and diagonals (M1)");

            double lfGap = UWCharacterMotor.DefaultRadius + UWCharacterMotor.Skin;

            // A wall met head-on: the body stops at its radius and the skin, nothing sideways.
            FakeMotionWorld lOWorld = new FakeMotionWorld();

            lOWorld.Paint(10, 1, 10, 30, 0, 2, 0);

            UWCharacterMotor lOMotor = fMotorWalk(lOWorld, 8.5, 15.5, 2.0, 0.0, 200);

            Check.Near("motor: head-on, stops at the wall", lOMotor.X, 10.0 - lfGap, 1.0e-9);
            Check.Near("motor: head-on, nothing sideways", lOMotor.Y, 15.5, 1.0e-12);

            // A wall of thirty tiles, met at 30 degrees: the slide keeps the whole part along it -
            // a seam between two tiles would take some away - at every step count.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(1, 20, 30, 20, 0, 2, 0);

            double lfCos = Math.Cos(Math.PI / 6.0);
            double lfSin = Math.Sin(Math.PI / 6.0);

            lOMotor = fMotorWalk(lOWorld, 5.5, 19.5, 10.0 * lfCos, 10.0 * lfSin, 1000);
            Check.Near("motor: along a long wall, at the wall", lOMotor.Y, 20.0 - lfGap, 1.0e-9);
            Check.Near("motor: along a long wall, no seam takes speed", lOMotor.X, 5.5 + (10.0 * lfCos), 1.0e-9);
            Check.Near("motor: along a long wall, few steps end the same", fMotorWalk(lOWorld, 5.5, 19.5, 10.0 * lfCos, 10.0 * lfSin, 7).X, lOMotor.X, 1.0e-9);

            // A corner: the body ends in it, at the gap from both walls.
            lOWorld.Paint(10, 1, 10, 19, 0, 2, 0);
            lOMotor = fMotorWalk(lOWorld, 8.5, 18.5, 3.0, 3.0, 300);
            Check.Near("motor: a corner, at the east wall", lOMotor.X, 10.0 - lfGap, 1.0e-9);
            Check.Near("motor: a corner, at the north wall", lOMotor.Y, 20.0 - lfGap, 1.0e-9);

            // A block's edge passed exactly at the gap: the corner of the block is not met again.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(15, 15, 15, 15, 0, 2, 0);
            lOMotor = fMotorWalk(lOWorld, 13.0, 16.0 + lfGap, 4.0, 0.0, 500);
            Check.Near("motor: past a block's edge at the gap", lOMotor.X, 17.0, 1.0e-6);

            // Into the block's corner a little off its diagonal: round it, slide along its south
            // side with the whole part along it, and on past it.
            lOMotor = fMotorWalk(lOWorld, 14.0, 13.9, 3.0, 3.0, 600);
            Check.Bool("motor: round a block's corner and on past it", lOMotor.X > 16.9 && lOMotor.Y > 15.5, true);

            // A diagonal wall of ten tiles (closed to the north-west), walked into due north: the
            // body slides north-east along it at the gap, the part along it kept.
            lOWorld = new FakeMotionWorld();

            for (int liK = 5; liK <= 20; liK++)
            {
                lOWorld.Paint(liK, liK, liK, liK, 2, 2, 0);
                lOWorld.Paint(liK, liK + 1, liK, liK + 1, 0, 2, 0);
            }

            lOMotor = fMotorWalk(lOWorld, 10.5, 8.5, 0.0, 5.0, 500);

            double lfRoot = Math.Sqrt(0.5);
            double lfToContact = (((10.5 - 8.5) * lfRoot) - lfGap) / lfRoot;
            double lfAlong = (5.0 - lfToContact) * lfRoot;

            Check.Near("motor: along a diagonal, at the gap", (lOMotor.X - lOMotor.Y) * lfRoot, lfGap, 1.0e-9);
            Check.Near("motor: along a diagonal, the part along it kept", lOMotor.X, 10.5 + (lfAlong * lfRoot), 1.0e-9);
            Check.Near("motor: along a diagonal, few steps end the same", fMotorWalk(lOWorld, 10.5, 8.5, 0.0, 5.0, 3).X, lOMotor.X, 1.0e-9);

            // A straight wall running into a diagonal at 135 degrees (the core's stair and jump
            // there): walked along the wall, pressing into it a little, the body turns up the
            // diagonal without a halt. The wall's top at y = 4 up to x = 10, the diagonal (closed
            // to the south-east) from 10/4 on.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(1, 3, 10, 3, 0, 2, 0);

            for (int liK = 10; liK <= 20; liK++)
            {
                lOWorld.Paint(liK, liK - 6, liK, liK - 6, 5, 2, 0);
                lOWorld.Paint(liK + 1, liK - 6, liK + 1, liK - 6, 0, 2, 0);
            }

            lOMotor = fMotorWalk(lOWorld, 6.5, 4.5, 6.0, -0.3, 600);
            Check.Near("motor: from a straight wall up the diagonal, at the gap", ((lOMotor.Y - lOMotor.X) + 6.0) * lfRoot, lfGap, 1.0e-9);
            Check.Bool("motor: from a straight wall up the diagonal, on along it", lOMotor.X > 11.0 && lOMotor.Y > 5.0, true);

            // A corridor one tile wide, walked 10 degrees off its axis: along it, never held.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(19, 3, 19, 28, 0, 2, 0);
            lOWorld.Paint(21, 3, 21, 28, 0, 2, 0);
            lOMotor = fMotorWalk(lOWorld, 20.5, 5.5, 10.0 * Math.Sin(Math.PI / 18.0), 10.0 * Math.Cos(Math.PI / 18.0), 800);
            Check.Near("motor: a corridor, at its east side", lOMotor.X, 21.0 - lfGap, 1.0e-9);
            Check.Near("motor: a corridor, the part along it kept", lOMotor.Y, 5.5 + (10.0 * Math.Cos(Math.PI / 18.0)), 1.0e-9);

            // A step of one level is walked onto; a floor two levels above it is a wall.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(6, 25, 7, 25, 1, 3, 0);
            lOWorld.Paint(8, 25, 8, 25, 1, 5, 0);
            lOMotor = fMotorWalk(lOWorld, 5.5, 25.5, 4.0, 0.0, 400);
            Check.Near("motor: up a step, stopped by a floor too high", lOMotor.X, 8.0 - lfGap, 1.0e-9);
            Check.Int("motor: up a step, on its floor", lOMotor.Z, 3 * 0x40);

            // Half over a ledge the body still stands on it (a lower floor beside, no wall forms).
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(10, 10, 10, 10, 1, 4, 0);
            lOWorld.Paint(11, 10, 11, 10, 1, 3, 0);
            lOMotor = fMotorWalk(lOWorld, 11.5, 10.5, -0.6, 0.0, 60);
            Check.Int("motor: half over a ledge, on the higher floor", lOMotor.Z, 4 * 0x40);
            Check.Bool("motor: half over a ledge, no wall it is in", lOMotor.Clearance() >= 0.0, true);
        }

        /// <summary>The motor placed at (pfX, pfY) and walked (pfDx, pfDy) tiles in piSteps equal steps.</summary>
        private static UWCharacterMotor fMotorWalk(FakeMotionWorld pOWorld, double pfX, double pfY, double pfDx, double pfDy, int piSteps)
        {
            UWCharacterMotor lOMotor = new UWCharacterMotor(pOWorld);

            lOMotor.PlaceOnFloor(pfX, pfY);

            for (int liStep = 0; liStep < piSteps; liStep++)
                lOMotor.Move(pfDx / piSteps, pfDy / piSteps);

            return lOMotor;
        }
    }
}
