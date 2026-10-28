using System;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// MODERN THROUGH THE PLAYER'S MOTION (Docs/MOTOR-PLAN.md M4, 2026-10-28): UWPlayerMotion with
    /// Modern set, frames of 60 a second as the host runs it, the motor doing the motion and the
    /// shared part the rest. The rules every mode is held to (PreciseMotionChecks' ledge rules: a
    /// creep holds at a ledge, a run goes over and lands, a ten-step fall hurts, levitation hovers
    /// and flies, slow fall at its cap, walking into water swims), then what is Modern's own: the
    /// slide along a wall without the view turning, back and the sides at three quarters of the
    /// forward speed, the jump's apex as the original's, a placement from outside taken over, the
    /// easy step asking the motor, a move trigger reaching the world.
    /// </summary>
    public static partial class SelfChecks
    {
        private const int ModernFrameA = 1092;

        private const int ModernFrameB = 1093;

        private static void fCheckModernMotion(DataImport pOData)
        {
            Check.Group("Modern motion through UWPlayerMotion (the character motor in the game's frame, M4)");

            fCheckLedgeRules(new MotionStepMode
            {
                Name = "modern",
                Make = (pOWorld, piTileX, piTileY) => fMakeModernPlayer(pOWorld, pOData, piTileX, piTileY),
                Run = (pOPlayer, peCommand, piWalk, piTicks) => fRunFine(pOPlayer, peCommand, piWalk, piTicks, ModernFrameA, ModernFrameB),
                FreeFrame = ModernFrameA,
                LandFrames = 2000,
                FallFrames = 3000,
                SlowFallFrames = 2000,
                LandsName = "a run goes over it and lands on floor 0",
                HoverName = "levitating off the ledge hovers at its height",
                HoldsHeight = false
            });

            // A wall at y 20, met at 30 degrees: the body slides along it, the view stays.
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            lOWorld.Paint(1, 20, 30, 20, 0, 2, 0);

            UWPlayerMotion lOPlayer = fMakeModernPlayer(lOWorld, pOData, 8, 18);
            int liYaw = 0x4000 - 0x1555;
            bool lbDeflected = false;

            lOPlayer.CameraYaw = liYaw;

            for (int liAt = 0; liAt < 240; liAt++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                lOPlayer.Walk = UWPlayerMotion.WalkRun;
                lOPlayer.Frame((liAt & 1) == 0 ? ModernFrameA : ModernFrameB);
                lbDeflected |= lOPlayer.Last.Deflected;
            }

            Check.Bool("modern: along a wall, the view stays where it looks", lOPlayer.CameraYaw == liYaw && !lbDeflected, true);
            Check.Bool("modern: ... at the wall and on along it", lOPlayer.FineY > (20 << 8) - 0x45 && lOPlayer.FineX > (12 << 8), true);

            // Back and the sides at three quarters of the walk forward (decision 1).
            double lfForward = fModernDistance(pOData, UWPlayerMotion.Command.Walk);

            Check.Near("modern: back at three quarters of forward", fModernDistance(pOData, UWPlayerMotion.Command.Back) / lfForward, 0.75, 0.01);
            Check.Near("modern: a side step at three quarters of forward", fModernDistance(pOData, UWPlayerMotion.Command.SlideRight) / lfForward, 0.75, 0.01);

            // The slow walk slows back and the sides alike; without a walk value (the classic
            // scheme's keys) they run at three quarters of the run.
            double lfSlow = fModernDistance(pOData, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkSlow);

            Check.Bool("modern: the slow walk is slower forward", lfSlow < lfForward * 0.6, true);
            Check.Near("modern: ... back at three quarters of it", fModernDistance(pOData, UWPlayerMotion.Command.Back, UWPlayerMotion.WalkSlow) / lfSlow, 0.75, 0.02);
            Check.Near("modern: ... a side step at three quarters of it", fModernDistance(pOData, UWPlayerMotion.Command.SlideLeft, UWPlayerMotion.WalkSlow) / lfSlow, 0.75, 0.02);
            Check.Near("modern: a side step without a walk value at three quarters of the run", fModernDistance(pOData, UWPlayerMotion.Command.SlideLeft, 0) / lfForward, 0.75, 0.01);

            // The jump from standing: its apex as the original's within 6 fine z, down on its floor.
            lOWorld = new FakeMotionWorld();
            lOPlayer = fMakeModernPlayer(lOWorld, pOData, 16, 16);
            lOPlayer.MotionCommand = UWPlayerMotion.Command.Jump;
            lOPlayer.Frame(ModernFrameA);

            int liApex = lOPlayer.Params.Z;
            int liDamage = 0;
            int liFrames;

            for (liFrames = 0; liFrames < 2000 && (lOPlayer.Params.Gravity != 0 || lOPlayer.IsAirborne); liFrames++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.None;
                lOPlayer.Frame(ModernFrameA);
                liApex = Math.Max(liApex, lOPlayer.Params.Z);
                liDamage = Math.Max(liDamage, lOPlayer.Last.FallDamage);
            }

            UWPlayerMotion lOOriginal = fMakePrecisePlayer(new FakeMotionWorld(), pOData, 16, 16, false);

            Check.Bool("modern: the jump's apex within 6 fine z of the original's", Math.Abs(liApex - fCoreJump(lOOriginal, 8)) <= 6, true);
            Check.Bool("modern: ... down on its floor without damage", lOPlayer.Params.Z == 0x80 && liDamage == 0, true);

            // A placement from outside (a teleport, a load) is where the next frame goes on from.
            lOPlayer.PlaceAt((20 << 8) + 0x80, (10 << 8) + 0x80, 0x80, 0);
            fRunFine(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 32, ModernFrameA, ModernFrameB);
            Check.Bool("modern: a placement from outside is taken over", lOPlayer.TileX == 20 && lOPlayer.Params.Y > (10 << 8) + 0x80, true);

            // The easy step asks the motor: half a tile in the open, refused into a wall.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(1, 20, 30, 20, 0, 2, 0);
            lOPlayer = fMakeModernPlayer(lOWorld, pOData, 8, 10);
            Check.Bool("modern: the easy step in the open", lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0) && lOPlayer.Params.Y == (10 << 8) + 0x80 + 0x80, true);
            lOPlayer.PlaceAt((8 << 8) + 0x80, (19 << 8) + 0x66, 0x80, 0);
            Check.Bool("modern: ... refused into a wall", lOPlayer.EasyStep(UWEasyMovement.CommandStepForward, 0), false);

            // A BRIDGE OVER WATER (per user, 2026-10-28, SAVE3 level 3 at 32/16): the bridge's top at the
            // height of the floor before it, one level over the water - walked across, never swum.
            lOWorld = new FakeMotionWorld();
            lOWorld.Paint(1, 1, 30, 30, 1, 12, 0);
            lOWorld.Paint(1, 16, 30, 17, 1, 11, 1);
            lOWorld.AddBody(16, 16, new UWMotionBody { Index = 61, ItemId = MotorBridge, XPos = 0, YPos = 0, ZPos = 94 });
            lOWorld.AddBody(16, 17, new UWMotionBody { Index = 62, ItemId = MotorBridge, XPos = 0, YPos = 0, ZPos = 94 });
            lOPlayer = fMakeModernPlayer(lOWorld, pOData, 16, 14);
            lOPlayer.CameraYaw = 0;

            bool lbSwam = false;

            for (int liAt = 0; liAt < 200; liAt++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                lOPlayer.Walk = UWPlayerMotion.WalkRun;
                lOPlayer.Frame((liAt & 1) == 0 ? ModernFrameA : ModernFrameB);
                lbSwam |= lOPlayer.CurrentState != UWPlayerMotion.State.Normal;

            }

            Check.Bool("modern: a bridge over water is walked across, not swum", !lbSwam && lOPlayer.TileY >= 18, true);

            // The same on the real level: SAVE3's spot (level 3, from 32/15 fine 95/133 north over the
            // bridge on 32/16 and 32/17).
            if (pOData.Levels != null && pOData.Levels.Count >= 3)
            {
                LevelMotionWorld lOLevel = new LevelMotionWorld(pOData.Levels[2], pOData.Terrain, 1);
                UWPlayerMotion lOReal = new UWPlayerMotion(lOLevel, pOData.CommonObjectProperties);

                lOReal.PlaceInTile(32, 15);
                lOReal.PlaceAt((32 << 8) + 95, (15 << 8) + 133, lOReal.Params.Z, 428);
                lOReal.Precise = true;
                lOReal.FineTicks = true;
                lOReal.Modern = true;
                lOReal.RampFrameTicks = 0;

                bool lbRealSwam = false;

                for (int liAt = 0; liAt < 200; liAt++)
                {
                    lOReal.MotionCommand = UWPlayerMotion.Command.Walk;
                    lOReal.Walk = UWPlayerMotion.WalkRun;
                    lOReal.Frame((liAt & 1) == 0 ? ModernFrameA : ModernFrameB);
                    lbRealSwam |= lOReal.CurrentState != UWPlayerMotion.State.Normal;
                }

                Check.Bool("modern: SAVE3's bridge on level 3 (32/16, 32/17) walked across, not swum", !lbRealSwam && lOReal.TileY >= 18, true);
            }

            // A move trigger walked through reaches the world.
            lOWorld = new FakeMotionWorld();
            lOWorld.AddBody(10, 8, new UWMotionBody { Index = 60, ItemId = UWObjectMechanics.MoveTriggerId, XPos = 4, YPos = 4, ZPos = 16 });
            lOPlayer = fMakeModernPlayer(lOWorld, pOData, 8, 8);
            lOPlayer.CameraYaw = 0x4000;
            fRunFine(lOPlayer, UWPlayerMotion.Command.Walk, UWPlayerMotion.WalkRun, 256, ModernFrameA, ModernFrameB);
            Check.Bool("modern: a move trigger walked through reaches the world", lOWorld.Log.Contains("trigger 60"), true);

            fCheckModernLedges(pOData);
            fCheckModernWallImpact(pOData);
        }

        /// <summary>
        /// THE WALL IN THE AIR HURTS (per user, 2026-10-28: "the damage when bouncing off the wall"
        /// was missing): a run and a jump into the face of a ledge too high, head-on and at 30 and 60
        /// degrees, the roll held low so the Acrobat check never softens it. The impact value, the
        /// damage and the landing sounds of Modern as the precision mode's (the core's bounce counts
        /// the whole speed at the hit, whatever the angle).
        /// </summary>
        private static void fCheckModernWallImpact(DataImport pOData)
        {
            UWRandom.Use(new FixedRandomSource(false));

            try
            {
                foreach (int liAngle in new[] { 0, 0x1555, 0x2AAA })
                {
                    string lsCore = fModernWallJump(pOData, liAngle, false);
                    string lsModern = fModernWallJump(pOData, liAngle, true);

                    Check.Str("modern: a jump into a wall at " + (liAngle * 360 / 0x10000) + " degrees hurts as the core's (" + lsCore + ")", lsModern, lsCore);
                }
            }
            finally
            {
                UWRandom.Use(null);
            }
        }

        /// <summary>A run east from 11/16, a jump at 12.5 into the face of a ledge four levels up at
        /// x 14, the heading turned by piAngle from straight at it; the highest impact value and damage
        /// and the number of landing sounds until the body is down again.</summary>
        private static string fModernWallJump(DataImport pOData, int piAngle, bool pbModern)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            lOWorld.Paint(14, 1, 30, 30, 1, 6, 0);

            UWPlayerMotion lOPlayer = fMakeFinePlayer(lOWorld, pOData, 8, 16);
            int liValue = 0;
            int liDamage = 0;
            int liSounds = 0;
            bool lbJumped = false;

            lOPlayer.Modern = pbModern;
            lOPlayer.RampFrameTicks = 0;
            lOPlayer.PlaceAt((11 << 8) + 128, (16 << 8) + 128, 0x80, 0x4000 - piAngle);

            for (int liAt = 0; liAt < 1200; liAt++)
            {
                bool lbNow = !lbJumped && lOPlayer.Params.X >= (12 << 8) + 128;

                lOPlayer.MotionCommand = lbNow ? UWPlayerMotion.Command.Jump : lbJumped ? UWPlayerMotion.Command.None : UWPlayerMotion.Command.Walk;
                lOPlayer.Walk = UWPlayerMotion.WalkRun;
                lbJumped |= lbNow;
                lOPlayer.Frame(ModernFrameA);
                liValue = Math.Max(liValue, lOPlayer.Last.ImpactValue);
                liDamage = Math.Max(liDamage, lOPlayer.Last.FallDamage);
                liSounds += lOPlayer.Last.LandingSound ? 1 : 0;

                if (lbJumped && liAt > 3 && lOPlayer.Params.Gravity == 0 && !lOPlayer.IsAirborne && lOPlayer.Params.Vz == 0)
                    break;
            }

            return "value " + liValue + ", damage " + liDamage + ", sounds " + liSounds;
        }

        /// <summary>
        /// THE LEDGES (per user, 2026-10-28: "where one bounces off in the original, Modern sets one
        /// onto the ledge"; then SAVE3 on level 1, a run north from 11/25 and a jump at the ledge of
        /// 11/28, "possible with Original, but clearly harder"). In the air the motor now loses the
        /// motion into a face and steps onto a floor only rising and more than the core's radius
        /// below its top. Checked as a grid against the precision mode - running jumps at ledges two
        /// to four levels up from 51 distances, standing jumps one to three levels up - and on the
        /// real spot against Original: Modern may not be easier there.
        /// </summary>
        private static void fCheckModernLedges(DataImport pOData)
        {
            int liRunMismatch = 0;
            int liStandMismatch = 0;
            StringBuilder lOWhere = new StringBuilder();

            for (int liLevels = 1; liLevels <= 4; liLevels++)
            {
                for (int liD = 0; liD <= 400; liD += 8)
                {
                    if (liLevels >= 2 && fModernLedgeJump(pOData, liLevels, liD, false) != fModernLedgeJump(pOData, liLevels, liD, true))
                    {
                        liRunMismatch++;
                        lOWhere.Append("run +").Append(liLevels).Append(" d").Append(liD).Append("; ");
                    }

                    // The standing jump's far end may differ by the arc's last per cent (the motor's arc
                    // is the continuous one of the precision mode's take-off, a little longer).
                    if (liLevels <= 3 && liD < 384 && fModernStandJump(pOData, liLevels, liD, false) != fModernStandJump(pOData, liLevels, liD, true))
                    {
                        liStandMismatch++;
                        lOWhere.Append("stand +").Append(liLevels).Append(" d").Append(liD).Append("; ");
                    }
                }
            }

            Check.Str("modern: running jumps at ledges land where the precision mode's land", lOWhere.ToString(), string.Empty);
            Check.Int("modern: ... and standing jumps", liStandMismatch + liRunMismatch, 0);

            if (pOData.Levels == null || pOData.Levels.Count < 1)
                return;

            int liOriginal = fModernSave3Jumps(pOData, 0);
            int liModern = fModernSave3Jumps(pOData, 2);

            Check.Bool("modern: SAVE3's run and jump on level 1 (11/25 to 11/28) no easier than Original (Original "
                + liOriginal + ", Modern " + liModern + " of 113 jump points)", liModern > 0 && liModern <= liOriginal + 2, true);
        }

        /// <summary>SAVE3's spot on level 1: from 11/25 fine 165/8 north at a run, the jump pressed at
        /// one of 113 points of the run-up (a 64th of a tile apart from y 25.25 on); how many land on
        /// the floor-5 ledge of 11/28. Mode 0 Original (8-tick frames), 2 Modern.</summary>
        private static int fModernSave3Jumps(DataImport pOData, int piMode)
        {
            int liWins = 0;

            for (int liJumpY = (25 << 8) + 64; liJumpY <= (27 << 8); liJumpY += 4)
            {
                LevelMotionWorld lOWorld = new LevelMotionWorld(pOData.Levels[0], pOData.Terrain, 1);
                UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOData.CommonObjectProperties);

                lOPlayer.PlaceInTile(11, 25);
                lOPlayer.PlaceAt((11 << 8) + 165, (25 << 8) + 8, lOPlayer.Params.Z, 232);
                lOPlayer.Precise = piMode > 0;
                lOPlayer.FineTicks = piMode > 0;
                lOPlayer.Modern = piMode == 2;
                lOPlayer.RampFrameTicks = 0;

                bool lbJumped = false;
                int liDt = piMode == 0 ? 8 : ModernFrameA;

                for (int liAt = 0; liAt < 1500; liAt++)
                {
                    bool lbNow = !lbJumped && lOPlayer.Params.Y >= liJumpY;

                    lOPlayer.MotionCommand = lbNow ? UWPlayerMotion.Command.Jump : lbJumped ? UWPlayerMotion.Command.None : UWPlayerMotion.Command.Walk;
                    lOPlayer.Walk = UWPlayerMotion.WalkRun;
                    lbJumped |= lbNow;
                    lOPlayer.Frame(liDt);

                    if (lbJumped && liAt > 3 && lOPlayer.Params.Gravity == 0 && !lOPlayer.IsAirborne && lOPlayer.Params.Vz == 0)
                        break;
                }

                if (lOPlayer.Params.Z >= 5 * 0x40 && lOPlayer.Params.Z < 0x400)
                    liWins++;
            }

            return liWins;
        }

        /// <summary>A standing jump (half the forward speed) east from piD fine units before touching
        /// the face at x 14 of a ledge piLevels up.</summary>
        private static string fModernStandJump(DataImport pOData, int piLevels, int piD, bool pbModern)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            lOWorld.Paint(14, 1, 30, 30, 1, 2 + piLevels, 0);

            UWPlayerMotion lOP = fMakeFinePlayer(lOWorld, pOData, 8, 16);
            lOP.Modern = pbModern;
            lOP.RampFrameTicks = 0;
            lOP.PlaceAt((14 << 8) - 64 - piD, (16 << 8) + 128, 0x80, 0x4000);
            lOP.MotionCommand = UWPlayerMotion.Command.StandingJump;
            lOP.Frame(ModernFrameA);

            for (int liAt = 0; liAt < 1200; liAt++)
            {
                lOP.MotionCommand = UWPlayerMotion.Command.None;
                lOP.Frame((liAt & 1) == 0 ? ModernFrameA : ModernFrameB);

                if (lOP.Params.Gravity == 0 && !lOP.IsAirborne && lOP.Params.Vz == 0)
                    break;
            }

            return lOP.Params.Z >= (2 + piLevels) * 0x40 && lOP.Params.Z < 0x400 ? "L" : "-";
        }

        /// <summary>Run east at the ledge (face at x 14) and jump when the centre is piD fine units from
        /// where it would touch the face; 'L' landed on the ledge, '-' not. The precision mode or Modern.</summary>
        private static string fModernLedgeJump(DataImport pOData, int piLevels, int piD, bool pbModern)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();
            lOWorld.Paint(14, 1, 30, 30, 1, 2 + piLevels, 0);

            UWPlayerMotion lOP = fMakeFinePlayer(lOWorld, pOData, 8, 16);
            lOP.Modern = pbModern;
            lOP.RampFrameTicks = 0;
            lOP.CameraYaw = 0x4000;

            // The core's box touches at x 14 - 2.5 eighths about, the motor at 14 - 0.25.
            int liJumpAt = (14 << 8) - 64 - piD;
            bool lbJumped = false;

            for (int liAt = 0; liAt < 1200; liAt++)
            {
                lOP.MotionCommand = !lbJumped && lOP.Params.X >= liJumpAt ? UWPlayerMotion.Command.Jump : UWPlayerMotion.Command.Walk;
                lOP.Walk = UWPlayerMotion.WalkRun;

                if (lOP.MotionCommand == UWPlayerMotion.Command.Jump)
                    lbJumped = true;

                lOP.Frame((liAt & 1) == 0 ? ModernFrameA : ModernFrameB);

                if (lbJumped && lOP.Params.Gravity == 0 && !lOP.IsAirborne && liAt > 5)
                    break;
            }

            return lOP.Params.Z >= (2 + piLevels) * 0x40 ? "L" : "-";
        }

        /// <summary>A player of Modern: the precision mode with fine ticks, the motor, at once.</summary>
        private static UWPlayerMotion fMakeModernPlayer(FakeMotionWorld pOWorld, DataImport pOData, int piTileX, int piTileY)
        {
            UWPlayerMotion lOPlayer = fMakeFinePlayer(pOWorld, pOData, piTileX, piTileY);

            lOPlayer.Modern = true;
            lOPlayer.RampFrameTicks = 0;

            return lOPlayer;
        }

        /// <summary>How far a command carries a Modern player in the open in 128 ticks at a walk value
        /// (the run by default), in fine units.</summary>
        private static double fModernDistance(DataImport pOData, UWPlayerMotion.Command peCommand, int piWalk = UWPlayerMotion.WalkRun)
        {
            UWPlayerMotion lOPlayer = fMakeModernPlayer(new FakeMotionWorld(), pOData, 16, 16);
            float lfX = lOPlayer.FineX;
            float lfY = lOPlayer.FineY;

            fRunFine(lOPlayer, peCommand, piWalk, 128, ModernFrameA, ModernFrameB);

            return Math.Sqrt(((lOPlayer.FineX - lfX) * (lOPlayer.FineX - lfX)) + ((lOPlayer.FineY - lfY) * (lOPlayer.FineY - lfY)));
        }
    }
}
