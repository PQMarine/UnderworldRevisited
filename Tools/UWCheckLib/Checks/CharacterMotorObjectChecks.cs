using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE CHARACTER MOTOR'S OBJECTS (Docs/MOTOR-PLAN.md section 5.1, M3, 2026-10-27): each kind of
    /// object on a small map, with the COMOBJ entries of the player's installation - a closed door
    /// stops the body at its box and the reach of one eighth, an open one (raised by 0x18) lets it
    /// through, a box is slid along, a bridge carries the body over a drop, a creature blocks and
    /// is pushed, a move trigger fires when walked through and not when passed at a distance, and a
    /// step up that would lift the head into a raised gate is not taken.
    /// </summary>
    public static partial class SelfChecks
    {
        private const int MotorDoorClosed = 0x140;

        private const int MotorDoorOpen = 0x148;

        private const int MotorBridge = 0x164;

        private const int MotorRat = 0x40;

        private const int MotorGate = 0x16D;

        private static void fCheckCharacterMotorObjects(DataImport pOData)
        {
            Check.Group("Character motor: objects, doors, creatures and triggers (M3)");

            UWCommonObjectProperties lOProps = pOData.CommonObjectProperties;
            double lfGap = UWCharacterMotor.ObjectPad + UWCharacterMotor.Skin;

            // A closed door in the middle of tile 10/15 (eighth 4/4, its z the floor's): the body
            // stops where its box (radius 3) and the reach of one eighth meet.
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            lOWorld.AddBody(10, 15, new UWMotionBody { Index = 50, ItemId = MotorDoorClosed, XPos = 4, YPos = 4, ZPos = 16 });

            UWCharacterMotor lOMotor = fMotorObjectRun(lOWorld, lOProps, 8.5, 15.5625, 0.0125, 0.0, 300);
            double lfDoorWest = 10.0 + (4.5 / 8.0) - (3.5 / 8.0);

            Check.Near("motor objects: a closed door stops the body at its box and the reach", lOMotor.X, lfDoorWest - lfGap, 1.0e-9);
            Check.Int("motor objects: ... and a door is not pushed (no mass)", lOWorld.Pushes, 0);

            // The same door open: its z raised by 0x18, above the head.
            lOWorld = new FakeMotionWorld();
            lOWorld.AddBody(10, 15, new UWMotionBody { Index = 50, ItemId = MotorDoorOpen, XPos = 4, YPos = 4, ZPos = 16 + 0x18 });
            lOMotor = fMotorObjectRun(lOWorld, lOProps, 8.5, 15.5625, 0.0125, 0.0, 300);
            Check.Bool("motor objects: an open door lets the body through", lOMotor.X > 11.5 && !lOMotor.Touched, true);

            // The closed door met at 30 degrees: slid along with the part along it kept.
            lOWorld = new FakeMotionWorld();
            lOWorld.AddBody(10, 15, new UWMotionBody { Index = 50, ItemId = MotorDoorClosed, XPos = 4, YPos = 4, ZPos = 16 });
            lOMotor = fMotorObjectRun(lOWorld, lOProps, 9.0, 15.3, 0.0125 * Math.Cos(Math.PI / 6.0), 0.0125 * Math.Sin(Math.PI / 6.0), 60);
            Check.Near("motor objects: a door met at an angle is slid along, at its box", lOMotor.X, lfDoorWest - lfGap, 1.0e-9);
            Check.Near("motor objects: ... the part along it kept", lOMotor.Y, 15.3 + (60 * MotorFrameTicks * 0.0125 * Math.Sin(Math.PI / 6.0)), 1.0e-9);

            // A bridge (a 3D model over the whole tile, two coarse z high) over a drop of two levels.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(12, 10, 12, 20, 1, 0, 0);
            lOWorld.AddBody(12, 15, new UWMotionBody { Index = 51, ItemId = MotorBridge, XPos = 4, YPos = 4, ZPos = 16 });

            double lfMaxZ;
            bool lbAirborne;
            double lfLanded;

            lOMotor = fMotorRun(lOWorld, 10.5, 15.5, 0.0125, 0.0, false, 0.0, 0.0, 300, out lfMaxZ, out lbAirborne, out lfLanded, lOProps);
            Check.Bool("motor objects: a bridge carries the body over a drop, on the ground all the way", !lbAirborne && lOMotor.X > 14.0 && lOMotor.Zf == 2 * 0x40 && lfMaxZ == 18 * 8, true);

            // A creature (radius 2, one eighth smaller for the player): blocks and is pushed.
            lOWorld = new FakeMotionWorld();
            lOWorld.AddBody(10, 15, new UWMotionBody { Index = 52, ItemId = MotorRat, XPos = 4, YPos = 4, ZPos = 16, IsMobile = true, IsCreature = true });
            lOMotor = fMotorObjectRun(lOWorld, lOProps, 8.5, 15.5625, 0.0125, 0.0, 300);
            Check.Near("motor objects: a creature blocks with its box one eighth smaller", lOMotor.X, 10.0 + (4.5 / 8.0) - (1.5 / 8.0) - lfGap, 1.0e-9);
            Check.Bool("motor objects: ... and is pushed along the motion (heading east)", lOWorld.Pushes > 0 && lOWorld.Log.Contains("push 52 heading 16384 vz 0"), true);

            // A move trigger walked through fires; passed half a tile beside it, it does not.
            lOWorld = new FakeMotionWorld();
            lOWorld.AddBody(10, 15, new UWMotionBody { Index = 53, ItemId = UWObjectMechanics.MoveTriggerId, XPos = 4, YPos = 4, ZPos = 16 });
            fMotorObjectRun(lOWorld, lOProps, 8.5, 15.5625, 0.0125, 0.0, 300);
            Check.Bool("motor objects: a move trigger walked through fires", lOWorld.Log.Contains("trigger 53"), true);

            lOWorld = new FakeMotionWorld();
            lOWorld.AddBody(10, 15, new UWMotionBody { Index = 53, ItemId = UWObjectMechanics.MoveTriggerId, XPos = 4, YPos = 4, ZPos = 16 });
            fMotorObjectRun(lOWorld, lOProps, 8.5, 16.1, 0.0125, 0.0, 300);
            Check.Bool("motor objects: ... and passed half a tile beside it does not", lOWorld.Log.Contains("trigger 53"), false);

            // A step up of one level into tile 11 under a raised gate whose bottom is above the head
            // now but would not be after the step: the step is not taken, the body stays clear of it.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(11, 10, 11, 20, 1, 3, 0);
            lOWorld.AddBody(11, 15, new UWMotionBody { Index = 54, ItemId = MotorGate, XPos = 4, YPos = 4, ZPos = 40 });
            lOMotor = fMotorObjectRun(lOWorld, lOProps, 9.5, 15.5625, 0.0125, 0.0, 300);
            Check.Bool("motor objects: no step up into a raised gate", lOMotor.X < 11.0 + (4.5 / 8.0) - (3.5 / 8.0) && lOMotor.Clearance() >= 0.0, true);
        }

        /// <summary>The motor with the objects placed at (pfX, pfY) and walked at (pfVx, pfVy) tiles a
        /// tick for piFrames frames of 144 a second.</summary>
        private static UWCharacterMotor fMotorObjectRun(FakeMotionWorld pOWorld, UWCommonObjectProperties pOProps, double pfX, double pfY,
            double pfVx, double pfVy, int piFrames)
        {
            double lfMaxZ;
            bool lbAirborne;
            double lfLanded;

            return fMotorRun(pOWorld, pfX, pfY, pfVx, pfVy, false, 0.0, 0.0, piFrames, out lfMaxZ, out lbAirborne, out lfLanded, pOProps);
        }
    }
}
