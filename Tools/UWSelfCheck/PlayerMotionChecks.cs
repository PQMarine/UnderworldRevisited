using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE PLAYER ON THE MOTION CORE (stage 3 of the motion rework, 2026-10-05): frames of
    /// UWPlayerMotion on the small map with the real COMOBJ. The placement and its z table, the
    /// walk's acceleration to the 822 of a run, the stop, the turn, the back and the slides, the
    /// jump and its landing, the height cuts of a high jump, the standing jump, the ledge held at
    /// a creep and walked off at a run, the fall and its impact, water and the swim speed, the
    /// water edge, water walking, lava, levitation off a ledge with the fly commands, slow fall,
    /// the slope snap, the quake bounce, the motion weight under load, the camera follow at a
    /// wall.
    /// </summary>
    public static partial class Program
    {
        /// <summary>A player on the small map at a tile centre, facing +y.</summary>
        private static UWPlayerMotion fMakePlayer(FakeMotionWorld pOWorld, DataImport pOData, int piTileX, int piTileY)
        {
            UWPlayerMotion lOPlayer = new UWPlayerMotion(pOWorld, pOData.CommonObjectProperties);

            lOPlayer.PlaceInTile(piTileX, piTileY);

            return lOPlayer;
        }

        /// <summary>Frames of 16 ticks with one command held.</summary>
        private static void fRunFrames(UWPlayerMotion pOPlayer, UWPlayerMotion.Command peCommand, int piWalk, int piFrames)
        {
            for (int liAt = 0; liAt < piFrames; liAt++)
            {
                pOPlayer.MotionCommand = peCommand;
                pOPlayer.Walk = piWalk;
                pOPlayer.Frame(16);
            }
        }

        /// <summary>Frames without a command until the player stands on the ground again with no
        /// vertical motion, or the limit is reached. Returns the frames taken, -1 at the limit;
        /// piMaxZ gets the highest fine z seen.</summary>
        private static int fRunUntilSettled(UWPlayerMotion pOPlayer, int piLimit, out int piMaxZ, out bool pbSoundHeard, out int piDamage)
        {
            piMaxZ = pOPlayer.Params.Z;
            pbSoundHeard = false;
            piDamage = 0;

            for (int liAt = 0; liAt < piLimit; liAt++)
            {
                pOPlayer.MotionCommand = UWPlayerMotion.Command.None;
                pOPlayer.Frame(16);

                if (pOPlayer.Params.Z > piMaxZ)
                    piMaxZ = pOPlayer.Params.Z;

                if (pOPlayer.Last.LandingSound)
                    pbSoundHeard = true;

                if (pOPlayer.Last.FallDamage > piDamage)
                    piDamage = pOPlayer.Last.FallDamage;

                if (pOPlayer.Params.Gravity == 0 && pOPlayer.Params.Vz == 0 && !pOPlayer.IsAirborne && pOPlayer.Params.Speed == 0)
                    return liAt + 1;
            }

            return -1;
        }

        private static void fCheckPlayerMotion(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Player motion (the engine-free core, stage 3)");

            if (pOData == null || pOData.CommonObjectProperties == null)
            {
                fFail("player motion", "no COMOBJ.DAT");

                return;
            }

            UWRandom.UseSeed(31);

            // ------------------------------------------------- the placement
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWPlayerMotion lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);

            fExpectInt("player: placed at the tile centre x", lOPlayer.Params.X, (8 << 8) + 0x80);
            fExpectInt("player: ... y", lOPlayer.Params.Y, (8 << 8) + 0x80);
            fExpectInt("player: ... the table's z for floor 2", lOPlayer.Params.Z, 0x80);
            fExpectInt("player: ... stands on the ground", lOPlayer.Params.Contact, UWMotionTables.ContactGround);
            fExpectInt("player: ... in the normal state", (int)lOPlayer.CurrentState, (int)UWPlayerMotion.State.Normal);
            fExpectInt("player: ... with the full forward speed", lOPlayer.ForwardSpeed, 0x3AC);
            fExpectInt("player: the z table ends in zeros (floors 14 and 15)", UWPlayerMotion.PlaceZByFloor[14] + UWPlayerMotion.PlaceZByFloor[15], 0);
            fExpectInt("player: ... and climbs by 0x40 (floor 13)", UWPlayerMotion.PlaceZByFloor[13], 0x340);

            fExpectBool("player: nothing to do runs no frame", lOPlayer.Frame(16) && lOPlayer.Frame(16), false);

            // ------------------------------------------------- the walk
            int liY0 = lOPlayer.Params.Y;

            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 1);
            fExpectInt("player: the first frame of a run accelerates by the motion weight", lOPlayer.Params.Speed, 0x60);
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 12);
            fExpectInt("player: ... and settles at (0x70 >> 2) * 0x3AC / 32 = 822", lOPlayer.Params.Speed, 822);
            fExpectBool("player: ... moving along +y", lOPlayer.Params.Y > liY0, true);
            fExpectInt("player: ... the heading is the camera's", lOPlayer.MotionYaw, lOPlayer.CameraYaw);

            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkSlow, 12);
            fExpectInt("player: the slow walk settles at 352", lOPlayer.Params.Speed, 352);

            fRunFrames(lOPlayer, UWPlayerMotion.Command.None, 0, 3);
            fExpectInt("player: the stop takes the speed down by 0x60 a frame", lOPlayer.Params.Speed, 352 - (3 * 0x60));
            fRunFrames(lOPlayer, UWPlayerMotion.Command.None, 0, 2);
            fExpectInt("player: ... to zero", lOPlayer.Params.Speed, 0);

            // ------------------------------------------------- the turn
            int liYaw0 = lOPlayer.CameraYaw;

            lOPlayer.TurnInput = UWPlayerMotion.TurnInputStep;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, 0, 1);
            fExpectInt("player: the turn key turns the camera by (16 * 15) * 22 / 4 a frame", (lOPlayer.CameraYaw - liYaw0) & 0xFFFF, 1320);
            lOPlayer.TurnInput = 0;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.None, 0, 1);

            // ------------------------------------------------- back and the slides
            lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);
            liY0 = lOPlayer.Params.Y;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Back, 0, 6);
            fExpectInt("player: back targets 0xBC", lOPlayer.Params.Speed, 0xBC);
            fExpectBool("player: ... along -y", lOPlayer.Params.Y < liY0, true);
            fExpectInt("player: ... the camera still faces +y", lOPlayer.CameraYaw, 0);

            lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);
            int liX0 = lOPlayer.Params.X;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.SlideRight, 0, 6);
            fExpectInt("player: a slide targets 0xEB", lOPlayer.Params.Speed, 0xEB);
            fExpectBool("player: ... right is +x", lOPlayer.Params.X > liX0, true);
            lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);
            fRunFrames(lOPlayer, UWPlayerMotion.Command.SlideLeft, 0, 6);
            fExpectBool("player: ... left is -x", lOPlayer.Params.X < liX0, true);

            // ------------------------------------------------- the jump
            lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Jump, 0, 1);
            fExpectInt("player: the jump takes off at 0x263 under gravity -4", lOPlayer.Params.Gravity, -4);
            fExpectBool("player: ... and is airborne after the first frame", lOPlayer.IsAirborne, true);

            int liMaxZ;
            bool lbSound;
            int liDamage;
            int liFrames = fRunUntilSettled(lOPlayer, 60, out liMaxZ, out lbSound, out liDamage);

            fExpectBool("player: ... comes down again", liFrames > 0, true);
            fExpectBool("player: ... having risen", liMaxZ > 0x80, true);
            fExpectInt("player: ... onto the floor it left", lOPlayer.Params.Z, 0x80);
            fExpectInt("player: ... unhurt", liDamage, 0);
            fExpectBool("player: ... and heard", lbSound, true);

            lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);
            lOPlayer.Abilities = UWPlayerMotion.AbilityLeap;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Jump, 0, 1);
            fExpectInt("player: Leap jumps under gravity -2", lOPlayer.Params.Gravity, -2);

            int liLeapMax;
            fRunUntilSettled(lOPlayer, 120, out liLeapMax, out lbSound, out liDamage);
            fExpectBool("player: ... and higher", liLeapMax > liMaxZ, true);

            lOWorld.Paint(20, 4, 24, 12, 1, 11, 0);
            lOPlayer = fMakePlayer(lOWorld, pOData, 22, 8);
            fExpectInt("player: placed on floor 11 at 0x2C0", lOPlayer.Params.Z, 0x2C0);
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Jump, 0, 1);
            lOWorld.Paint(20, 4, 24, 12, 1, 12, 0);
            lOPlayer = fMakePlayer(lOWorld, pOData, 22, 8);
            fExpectInt("player: placed on floor 12 at 0x300", lOPlayer.Params.Z, 0x300);

            // The cut is made before the core's first integration adds gravity * dt: read the
            // vertical speed right after the command by a frame of one tick.
            lOPlayer.MotionCommand = UWPlayerMotion.Command.Jump;
            lOPlayer.Frame(1);
            fExpectInt("player: a jump above 0x2C0 takes off at 0x263 * 5 / 6 * 2 / 3 less one tick of gravity", lOPlayer.Params.Vz, ((0x263 * 5 / 6) * 2 / 3) - 4);
            lOWorld.Paint(20, 4, 24, 12, 1, 10, 0);
            lOPlayer = fMakePlayer(lOWorld, pOData, 22, 8);
            lOPlayer.MotionCommand = UWPlayerMotion.Command.Jump;
            lOPlayer.Frame(1);
            fExpectInt("player: a jump at 0x280 takes off at the full 0x263", lOPlayer.Params.Vz, 0x263 - 4);
            lOWorld.Paint(20, 4, 24, 12, 1, 2, 0);

            lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);
            fRunFrames(lOPlayer, UWPlayerMotion.Command.StandingJump, 0, 1);
            fExpectBool("player: the standing jump from rest takes off", lOPlayer.Params.Gravity == -4, true);
            fExpectInt("player: ... at half the forward speed", lOPlayer.Params.Speed, 0x3AC / 2);
            liY0 = lOPlayer.Params.Y;
            fRunUntilSettled(lOPlayer, 60, out liMaxZ, out lbSound, out liDamage);
            fExpectBool("player: ... and lands farther along +y", lOPlayer.Params.Y > liY0, true);

            lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 3);
            fRunFrames(lOPlayer, UWPlayerMotion.Command.StandingJump, 0, 1);
            fExpectInt("player: the standing jump while moving does nothing", lOPlayer.Params.Gravity, 0);

            // ------------------------------------------------- the ledge
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(12, 1, 30, 30, 1, 0, 0);
            lOPlayer = fMakePlayer(lOWorld, pOData, 10, 8);
            lOPlayer.CameraYaw = 0x4000;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, 0x18, 80);
            // The box leans over the edge until its back corners leave the upper tile (the
            // flags 0x1000 come with the whole box over the lower floor), two eighths past it.
            fExpectBool("player: a creep (176 < 282) holds at a two-step ledge", lOPlayer.Params.X < (12 << 8) + 0x48 && lOPlayer.Params.Gravity == 0, true);
            fExpectInt("player: ... on floor 2", lOPlayer.Params.Z, 0x80);

            lOPlayer = fMakePlayer(lOWorld, pOData, 10, 8);
            lOPlayer.CameraYaw = 0x4000;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 12);
            fExpectBool("player: a run walks off it", lOPlayer.Params.X >= (12 << 8), true);
            liFrames = fRunUntilSettled(lOPlayer, 60, out liMaxZ, out lbSound, out liDamage);
            fExpectBool("player: ... and lands", liFrames > 0, true);
            fExpectInt("player: ... on floor 0", lOPlayer.Params.Z, 0);

            lOWorld.Paint(12, 1, 30, 30, 1, 3, 0);
            lOPlayer = fMakePlayer(lOWorld, pOData, 10, 8);
            lOPlayer.CameraYaw = 0x4000;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, 0x18, 40);
            fExpectBool("player: a one-step rise is climbed at a creep", lOPlayer.Params.X >= (12 << 8), true);
            fExpectInt("player: ... onto floor 3", lOPlayer.Params.Z, 0xC0);

            // ------------------------------------------------- a fall that hurts
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(1, 1, 30, 30, 1, 10, 0);
            lOWorld.Paint(12, 1, 30, 30, 1, 0, 0);
            lOPlayer = fMakePlayer(lOWorld, pOData, 10, 8);
            lOPlayer.CameraYaw = 0x4000;
            lOPlayer.AcrobatSkill = 0;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 12);
            liFrames = fRunUntilSettled(lOPlayer, 120, out liMaxZ, out lbSound, out liDamage);
            fExpectBool("player: a ten-step fall lands", liFrames > 0, true);
            fExpectBool("player: ... hurts", liDamage > 3, true);
            fExpectBool("player: ... and is heard", lbSound, true);

            // ------------------------------------------------- water
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(12, 1, 30, 30, 1, 2, 1);
            lOPlayer = fMakePlayer(lOWorld, pOData, 10, 8);
            lOPlayer.CameraYaw = 0x4000;

            bool lbEntered = false;
            bool lbEdge = false;

            for (int liAt = 0; liAt < 40; liAt++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                lOPlayer.Walk = UWPlayerMotion.WalkRun;
                lOPlayer.Frame(16);

                if (lOPlayer.Last.EnteredWater)
                    lbEntered = true;

                if (lOPlayer.Params.Contact == 0x20)
                    lbEdge = true;
            }

            fExpectBool("player: walking into water enters it", lbEntered, true);
            fExpectBool("player: ... through the edge state first", lbEdge, true);
            fExpectInt("player: ... swimming", (int)lOPlayer.CurrentState, (int)UWPlayerMotion.State.Swimming);
            fExpectInt("player: ... at three tenths", lOPlayer.ForwardSpeed, 0x3AC * 3 / 10);
            fExpectInt("player: ... the counter at 0x60", lOPlayer.SwimCounter, 0x60);
            fExpectInt("player: ... the surface bit 1", lOPlayer.SurfaceBits, 1);
            fExpectInt("player: ... on the water", lOPlayer.Params.Contact, UWMotionTables.ContactWater);

            lOPlayer = fMakePlayer(lOWorld, pOData, 10, 8);
            lOPlayer.CameraYaw = 0x4000;
            lOPlayer.Abilities = UWPlayerMotion.AbilityWaterWalk;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 40);
            fExpectInt("player: water walking keeps the normal state over water", (int)lOPlayer.CurrentState, (int)UWPlayerMotion.State.Normal);
            fExpectInt("player: ... and the counter at 0", lOPlayer.SwimCounter, 0);

            // ------------------------------------------------- lava
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(12, 1, 30, 30, 1, 2, 2);
            lOPlayer = fMakePlayer(lOWorld, pOData, 10, 8);
            lOPlayer.CameraYaw = 0x4000;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 40);
            fExpectInt("player: lava is state 2", (int)lOPlayer.CurrentState, (int)UWPlayerMotion.State.Lava);
            fExpectInt("player: ... at half speed", lOPlayer.ForwardSpeed, 0x3AC * 5 / 10);
            fExpectInt("player: ... surface bit 2", lOPlayer.SurfaceBits, 2);

            // ------------------------------------------------- levitation
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(12, 1, 30, 30, 1, 0, 0);
            lOPlayer = fMakePlayer(lOWorld, pOData, 10, 8);
            lOPlayer.CameraYaw = 0x4000;
            lOPlayer.Abilities = UWPlayerMotion.AbilityLevitate;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 14);
            fExpectBool("player: levitating off a ledge does not fall", lOPlayer.Params.X >= (12 << 8) && lOPlayer.Params.Gravity == 0, true);
            fExpectInt("player: ... stays at its height", lOPlayer.Params.Z, 0x80);
            fExpectInt("player: ... in the levitating state", (int)lOPlayer.CurrentState, (int)UWPlayerMotion.State.Levitating);
            fExpectInt("player: ... at one tenth", lOPlayer.ForwardSpeed, 0x3AC / 10);
            fExpectInt("player: ... surface bit 8", lOPlayer.SurfaceBits, 8);

            int liZ0 = lOPlayer.Params.Z;

            fRunFrames(lOPlayer, UWPlayerMotion.Command.FlyUp, 0, 4);
            fExpectBool("player: fly up rises", lOPlayer.Params.Z > liZ0, true);
            fExpectBool("player: ... at a vertical speed", lOPlayer.Params.Vz > 0, true);

            // Letting go of E: the decay of four fifths a frame stops the rise within a few
            // frames and holds the height (per user in the original, 2026-10-06).
            fRunFrames(lOPlayer, UWPlayerMotion.Command.None, 0, 12);
            fExpectInt("player: ... letting go stops the rise", lOPlayer.Params.Vz, 0);
            liZ0 = lOPlayer.Params.Z;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.None, 0, 10);
            fExpectInt("player: ... and holds the height", lOPlayer.Params.Z, liZ0);
            fRunFrames(lOPlayer, UWPlayerMotion.Command.FlyDown, 0, 4);
            fExpectBool("player: fly down sinks", lOPlayer.Params.Z < liZ0, true);
            fRunFrames(lOPlayer, UWPlayerMotion.Command.FlyDown, 0, 40);
            fExpectInt("player: ... down to the floor", lOPlayer.Params.Z, 0);
            fExpectInt("player: ... where he stands again", (int)lOPlayer.CurrentState, (int)UWPlayerMotion.State.Normal);

            lOPlayer = fMakePlayer(lOWorld, pOData, 10, 8);
            lOPlayer.CameraYaw = 0x4000;
            lOPlayer.Abilities = UWPlayerMotion.AbilitySlowFall;

            for (int liAt = 0; liAt < 60 && !lOPlayer.IsAirborne; liAt++)
                fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 1);

            fExpectBool("player: slow fall leaves the ledge airborne", lOPlayer.IsAirborne, true);
            fExpectInt("player: ... in state 6", (int)lOPlayer.CurrentState, (int)UWPlayerMotion.State.SlowFalling);
            fExpectInt("player: ... at two tenths", lOPlayer.ForwardSpeed, 0x3AC * 2 / 10);
            fExpectInt("player: ... gravity on, nothing integrated yet in the frame that left the ledge", lOPlayer.Params.Vz, 0);
            fExpectInt("player: ... the speed still whole (the cap bites from -0x5E on)", lOPlayer.Params.Speed, 822);

            // THE AIRBORNE PART OF ProcessPlayerTileState RUNS EVERY FRAME (label D8, the early
            // exit of an unchanged state lands on it): the fall is capped at -0x5E and the speed
            // halved frame after frame until nothing is left - one sinks straight down.
            lOPlayer.MotionCommand = UWPlayerMotion.Command.None;
            lOPlayer.Frame(16);
            fExpectInt("player: ... the next frame falls at gravity * dt", lOPlayer.Params.Vz, -64);
            lOPlayer.Frame(16);
            fExpectInt("player: ... and is capped at -0x5E from then on", lOPlayer.Params.Vz, -0x5E);
            fExpectInt("player: ... with the speed halved", lOPlayer.Params.Speed, 822 / 2);
            fRunFrames(lOPlayer, UWPlayerMotion.Command.None, 0, 6);
            fExpectInt("player: ... still capped six frames on", lOPlayer.Params.Vz, -0x5E);
            fExpectInt("player: ... and the momentum gone", lOPlayer.Params.Speed, 0);

            // ------------------------------------------------- the slope snap
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(12, 8, 12, 8, 6, 2, 0);
            lOPlayer = fMakePlayer(lOWorld, pOData, 10, 8);
            lOPlayer.CameraYaw = 0x4000;

            int liZInSlope = -1;

            for (int liAt = 0; liAt < 40 && liZInSlope < 0; liAt++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                lOPlayer.Walk = UWPlayerMotion.WalkRun;
                lOPlayer.Frame(16);

                if (lOPlayer.TileX == 12 && (lOPlayer.Params.X & 0xFF) > 0x60)
                    liZInSlope = lOPlayer.Params.Z;
            }

            fExpectInt("player: on a slope rising +y at mid tile the z is floor * 64 + 0x80 / 4", liZInSlope, 0x80 + (0x80 >> 2));
            lOPlayer = fMakePlayer(lOWorld, pOData, 12, 8);
            fExpectInt("player: placed on a slope the table's z gets 0x20", lOPlayer.Params.Z, 0x80 + 0x20);

            // ------------------------------------------------- the quake, the weight, the wall
            lOWorld = new FakeMotionWorld();
            lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);
            lOPlayer.Bounce(4);
            fExpectInt("player: the quake bounce sets 4 * 0x2F / 4", lOPlayer.Params.Vz, 0x2F);
            fExpectInt("player: ... under gravity -2", lOPlayer.Params.Gravity, -2);
            liFrames = fRunUntilSettled(lOPlayer, 60, out liMaxZ, out lbSound, out liDamage);
            fExpectBool("player: ... and comes down", liFrames > 0, true);

            lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);
            lOPlayer.CarriedWeight = 60;
            lOPlayer.MaxWeight = 100;
            lOPlayer.ReapplyState();
            fExpectInt("player: a load above half the capacity lowers the weight to 0x60 - 60 * 0x60 / 200", lOPlayer.MotionWeight, 0x60 - 28);
            lOPlayer.CarriedWeight = 50;
            lOPlayer.ReapplyState();
            fExpectInt("player: ... half the capacity keeps 0x60", lOPlayer.MotionWeight, 0x60);

            lOPlayer = fMakePlayer(lOWorld, pOData, 2, 8);
            lOPlayer.CameraYaw = 0xC000;
            fRunFrames(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 20);
            fExpectBool("player: running west into the border wall stops at it", lOPlayer.Params.X >= (1 << 8) && lOPlayer.Params.Speed == 0, true);
            fExpectInt("player: ... the camera still faces west", lOPlayer.CameraYaw, 0xC000);

            lOPlayer = fMakePlayer(lOWorld, pOData, 2, 8);
            lOPlayer.CameraYaw = 0xC000 + 0x1000;

            bool lbDeflected = false;

            for (int liAt = 0; liAt < 30; liAt++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                lOPlayer.Walk = UWPlayerMotion.WalkRun;
                lOPlayer.Frame(16);

                if (lOPlayer.Last.Deflected)
                    lbDeflected = true;
            }

            fExpectBool("player: running at the wall askew slides along it", lbDeflected && lOPlayer.Params.Y > (8 << 8) + 0x80, true);
            fExpectBool("player: ... and the camera has followed towards the wall's line", lOPlayer.CameraYaw != 0xD000, true);
        }
    }
}
