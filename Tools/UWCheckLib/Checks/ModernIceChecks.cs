using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S ICE IN EVERY MOTION MODE (2026-11-14, per user: Original bounces off a wall on ice and keeps
    /// its momentum, Modern slid along the wall without turning). A run on ground into ice of the most slippery kind
    /// (floor bits 0, elasticity 15, as on the Ice Caverns' levels), at 30 degrees off a wall: after the hit the motion
    /// leaves the wall mirrored, at full speed, in Smooth (the core) and in Modern (the motor) alike.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckModernIce(CheckContext pOContext)
        {
            Check.Group("Ice in every motion mode (UW2)");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("ice modes", "no second game here: skipped");

                return;
            }

            DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);
            UWGameSession lOBefore = UWGameSession.Current;

            UWGameSession.MakeCurrent(lOImport.Session);

            try
            {
                foreach (bool lbModern in new[] { false, true })
                {
                    string lsMode = lbModern ? "Modern" : "Smooth";
                    string lsTrace;
                    int liYaw = fIceWallRun(lOImport, lbModern, out int liSpeed, out double lfMinDistance, out lsTrace);

                    Console.WriteLine("    [" + lsMode + "] " + lsTrace);
                    Check.Bool("ice modes: " + lsMode + " leaves the wall mirrored (motion yaw near 0xD555, got 0x" + liYaw.ToString("X") + ")",
                        Math.Abs((short)(liYaw - 0xD555)) < 0x300, true);
                    Check.Bool("ice modes: " + lsMode + " keeps the run's speed on elasticity 15 (got " + liSpeed + ")", liSpeed > 800, true);
                }

                fIceHeadOn(lOImport);
                fIceSave3Walk(lOImport);
                fIceSave3FlyUp(lOImport);
                fIceGripSpeeds(lOImport);
                fIceSlopeJump(lOImport);
                fGemFacetWalk(lOImport);
                fIceLevelSweep(lOImport, 24);
            }
            finally
            {
                UWGameSession.MakeCurrent(lOBefore);
            }
        }

        /// <summary>THE USER'S RECORDING (2026-11-14): from standing on glassy ice two tiles before a wall, the walk held
        /// straight at it. Smooth and Modern are traced side by side.</summary>
        private static void fIceHeadOn(DataImport pOImport)
        {
            foreach (int liMode in new[] { 0, 1, 2 })
            {
                bool lbModern = liMode == 2;
                FakeMotionWorld lOWorld = new FakeMotionWorld();

                lOWorld.Paint(2, 1, 19, 30, 1, 2, UWTerrainUw2.ClassIce);
                lOWorld.Paint(20, 1, 20, 30, 0, 2, 0);

                UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOImport.CommonObjectProperties);

                lOPlayer.PlaceInTile(18, 16);
                lOPlayer.TerrainWordAt = (piX, piY) => UWTerrainUw2.ClassIce << 6;
                lOPlayer.Precise = liMode > 0;
                lOPlayer.FineTicks = liMode > 0;
                lOPlayer.Modern = lbModern;
                lOPlayer.RampFrameTicks = 0;
                lOPlayer.CameraYaw = 0x4000;
                lOPlayer.MotionYaw = 0x4000;

                string lsTrace = string.Empty;

                for (int liFrame = 0; liFrame < 240; liFrame++)
                {
                    lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                    lOPlayer.Walk = UWPlayerMotion.WalkRun;
                    lOPlayer.Frame(liMode == 0 ? 8 : (liFrame & 1) == 0 ? ModernFrameA : ModernFrameB);

                    if ((liFrame % (liMode == 0 ? 2 : 8)) == 0)
                        lsTrace += string.Format("{0}:{1:F3} y{2:X} s{3} {4}|", liFrame, lOPlayer.FineX / 256f, lOPlayer.MotionYaw, lOPlayer.Params.Speed, lOPlayer.CurrentState);
                }

                if (Environment.GetEnvironmentVariable("UR_ICE_TRACE") != null)
                    Console.WriteLine("    [head-on " + (liMode == 2 ? "Modern" : liMode == 1 ? "Smooth" : "Original") + "] " + lsTrace);
            }
        }

        /// <summary>THE USER'S SAVE3 (2026-11-14): level 25, 14/17 fine 93/76, heading 43091, the walk held straight on
        /// for 18 seconds in Original, Smooth and Modern, without and with Fly (SAVE3 had Fly active, class 1 minor 5:
        /// the user's recording stuck at the wall). UR_ICE_TRACE prints the traces.</summary>
        private static void fIceSave3Walk(DataImport pOImport)
        {
            UWLevel lOLevel = pOImport.Levels[24];
            UWTerrainUw2 lOTerrain = pOImport.Terrain.Uw2;

            UWRulesDeviations lODeviations = pOImport.Session.Rules.Deviations;
            bool lbFixBefore = lODeviations.GroundedFlightBouncesOnIce;

            for (int liRun = 0; liRun < 9; liRun++)
            {
                int liMode = liRun % 3;
                bool lbFly = liRun >= 3;
                bool lbFixed = liRun < 6;

                lODeviations.GroundedFlightBouncesOnIce = lbFixed;
                LevelMotionWorld lOWorld = new LevelMotionWorld(lOLevel, pOImport.Terrain, 1);
                UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOImport.CommonObjectProperties);

                lOPlayer.PlaceInTile(14, 17);
                lOPlayer.PlaceAt((14 << 8) + 93, (17 << 8) + 76, lOPlayer.Params.Z, 43091);
                lOPlayer.TerrainWordAt = (piTx, piTy) =>
                {
                    UWTile lOTile = lOLevel.GetTile(piTx, piTy);

                    return lOTile != null ? lOTerrain.Word(lOTile.TextureFloor) : 0;
                };
                lOPlayer.Precise = liMode > 0;
                lOPlayer.FineTicks = liMode > 0;
                lOPlayer.Modern = liMode == 2;
                lOPlayer.RampFrameTicks = 0;
                lOPlayer.Abilities = lbFly ? UWPlayerMotion.AbilityFly : 0;

                string lsTrace = string.Empty;
                int liFrames = liMode == 0 ? 18 * 32 : 18 * 60;
                bool lbTurned = false;

                for (int liFrame = 0; liFrame < liFrames; liFrame++)
                {
                    lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                    lOPlayer.Walk = UWPlayerMotion.WalkRun;
                    lOPlayer.Frame(liMode == 0 ? 8 : (liFrame & 1) == 0 ? ModernFrameA : ModernFrameB);
                    lbTurned |= (lOPlayer.MotionYaw & 0xFFFF) != 43091;

                    if ((liFrame % (liMode == 0 ? 16 : 30)) == 0)
                        lsTrace += string.Format("{0:F1}s:{1:F2}/{2:F2} y{3:X} s{4} b{5}|", liFrame / (liMode == 0 ? 32.0 : 60.0), lOPlayer.FineX / 256f,
                            lOPlayer.FineY / 256f, lOPlayer.MotionYaw, lOPlayer.Params.Speed,
                            (lOTerrain.Word(lOLevel.GetTile((int)(lOPlayer.FineX / 256f), (int)(lOPlayer.FineY / 256f)).TextureFloor) >> 3) & 7);
                }

                string lsMode = (liMode == 2 ? "Modern" : liMode == 1 ? "Smooth" : "Original");

                if (Environment.GetEnvironmentVariable("UR_ICE_TRACE") != null)
                    Console.WriteLine("    [save3" + (lbFly ? lbFixed ? " fly fixed " : " fly original " : " ") + lsMode + "] " + lsTrace);

                // Without flight the ice bounces the walk off the first wall; under Fly or Levitate the original sets the
                // slide flag (PHYSICS.C:489, motionbits & 0x14 -> PN.flags 0x80): no bounce, head-on it stops at the wall.
                // Its bug fixed (per user 2026-11-14, deviations.GroundedFlightBouncesOnIce): on the ground the flight
                // changes nothing, the walk bounces.
                Check.Bool("ice modes: SAVE3's walk, " + lsMode + (!lbFly ? " bounces off the walls" : lbFixed
                    ? " under Fly with the bug fixed bounces as without it" : " under Fly as the original stops at the wall without a bounce"),
                    lbTurned, !lbFly || lbFixed);
            }

            lODeviations.GroundedFlightBouncesOnIce = lbFixBefore;
        }

        /// <summary>THE USER'S SAVE3 UNDER FLY (2026-11-14: "climbing with E kills very fast"): from SAVE3's spot the
        /// fly-up command held for 10 seconds in Original, Smooth and Modern. Summed: the fall damage the frames ask for.</summary>
        private static void fIceSave3FlyUp(DataImport pOImport)
        {
            UWLevel lOLevel = pOImport.Levels[24];
            UWTerrainUw2 lOTerrain = pOImport.Terrain.Uw2;

            UWRulesDeviations lODeviations = pOImport.Session.Rules.Deviations;
            bool lbFixBefore = lODeviations.GroundedFlightBouncesOnIce;

            for (int liRun = 0; liRun < 9; liRun++)
            {
                int liMode = liRun % 3;
                int liKind = liRun / 3;
                bool lbWalkFirst = liKind > 0;
                bool lbFixed = liKind == 2;

                lODeviations.GroundedFlightBouncesOnIce = liKind != 1;

                LevelMotionWorld lOWorld = new LevelMotionWorld(lOLevel, pOImport.Terrain, 1);
                UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOImport.CommonObjectProperties);

                lOPlayer.PlaceInTile(14, 17);
                lOPlayer.PlaceAt((14 << 8) + 93, (17 << 8) + 76, lOPlayer.Params.Z, 43091);
                lOPlayer.TerrainWordAt = (piTx, piTy) =>
                {
                    UWTile lOTile = lOLevel.GetTile(piTx, piTy);

                    return lOTile != null ? lOTerrain.Word(lOTile.TextureFloor) : 0;
                };
                lOPlayer.Precise = liMode > 0;
                lOPlayer.FineTicks = liMode > 0;
                lOPlayer.Modern = liMode == 2;
                lOPlayer.RampFrameTicks = 0;
                lOPlayer.Abilities = UWPlayerMotion.AbilityFly;

                string lsTrace = string.Empty;
                int liRate = liMode == 0 ? 32 : 60;
                int liWalk = lbWalkFirst ? 6 * liRate : 0;
                int liFrames = liWalk + (10 * liRate);
                int liDamage = 0;
                int liHits = 0;

                for (int liFrame = 0; liFrame < liFrames; liFrame++)
                {
                    lOPlayer.MotionCommand = liFrame < liWalk ? UWPlayerMotion.Command.Walk : UWPlayerMotion.Command.FlyUp;
                    lOPlayer.Walk = UWPlayerMotion.WalkRun;
                    lOPlayer.Frame(liMode == 0 ? 8 : (liFrame & 1) == 0 ? ModernFrameA : ModernFrameB);
                    liDamage += lOPlayer.Last.FallDamage;

                    if (lOPlayer.Last.FallDamage > 0)
                        liHits++;

                    if ((liFrame % (liMode == 0 ? 16 : 30)) == 0 || lOPlayer.Last.ImpactValue > 0)
                        lsTrace += string.Format("{0:F2}s:z{1} vz{2} g{3} iv{4} fd{5}|", liFrame / (liMode == 0 ? 32.0 : 60.0), lOPlayer.Params.Z,
                            lOPlayer.Params.Vz, lOPlayer.Params.Gravity, lOPlayer.Last.ImpactValue, lOPlayer.Last.FallDamage);
                }

                string lsMode = (liMode == 2 ? "Modern" : liMode == 1 ? "Smooth" : "Original");

                if (Environment.GetEnvironmentVariable("UR_ICE_TRACE") != null)
                    Console.WriteLine("    [save3 fly up " + liKind + " " + lsMode + "] damage " + liDamage + " in " + liHits + " frames: " + lsTrace);

                // Kind 0 rises from the spot, 2 walks first with the original's bug fixed: no damage. Kind 1 walks first as
                // the original (per user 2026-11-14 in the original: "rises only a minimal step and dies very fast"): stuck
                // at the wall the ice keeps the run's speed (munge_vectors at strength 0 or less, no head-on stop on ice),
                // each step adds it as impact (MOTION.C:399, value 3), and with a vertical speed it is doubled to 6, above 3
                // (PHYSICS.C:601-608), every frame. The core reproduces it; Modern's motor rises off the wall.
                if (liKind == 1)
                {
                    if (liMode == 0)
                        Check.Bool("ice modes: SAVE3's walk into the wall then fly up, Original as the original hurts every frame (got " + liDamage + " in " + liHits + ")",
                            liDamage > 1000, true);
                }
                else
                    Check.Bool("ice modes: SAVE3's fly up" + (lbFixed ? " after the walk with the bug fixed, " : " from the spot, ") + lsMode + " does no damage (got " + liDamage + ")",
                        liDamage == 0, true);
            }

            lODeviations.GroundedFlightBouncesOnIce = lbFixBefore;
        }

        /// <summary>THE ICE GRIP'S RUN (per user 2026-11-15: under Half the run sank to a walk): the run held for three
        /// seconds (checked after one, before the wall) from standing on SAVE3's ice, in Original and Modern, under each ice grip. Half keeps the run's speed
        /// on the straight as Full does (the original's munge_vectors loses a sixteenth a call; Half blends without
        /// the loss, UWIceGrip.Blend). UR_ICE_TRACE prints the speed every quarter second.</summary>
        private static void fIceGripSpeeds(DataImport pOImport)
        {
            UWLevel lOLevel = pOImport.Levels[24];
            UWTerrainUw2 lOTerrain = pOImport.Terrain.Uw2;
            Func<UWIceGripEnum> lOBefore = UWIceGrip.ModeSource;

            foreach (UWIceGripEnum leMode in new[] { UWIceGripEnum.Off, UWIceGripEnum.Half, UWIceGripEnum.Full })
            {
                UWIceGrip.ModeSource = () => leMode;

                foreach (bool lbModern in new[] { false, true })
                {
                    LevelMotionWorld lOWorld = new LevelMotionWorld(lOLevel, pOImport.Terrain, 1);
                    UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOImport.CommonObjectProperties);

                    lOPlayer.PlaceInTile(14, 17);
                    lOPlayer.PlaceAt((14 << 8) + 93, (17 << 8) + 76, lOPlayer.Params.Z, 43091);
                    lOPlayer.TerrainWordAt = (piTx, piTy) =>
                    {
                        UWTile lOTile = lOLevel.GetTile(piTx, piTy);

                        return lOTile != null ? lOTerrain.Word(lOTile.TextureFloor) : 0;
                    };
                    lOPlayer.Precise = lbModern;
                    lOPlayer.FineTicks = lbModern;
                    lOPlayer.Modern = lbModern;
                    lOPlayer.RampFrameTicks = 0;

                    string lsTrace = string.Empty;
                    int liRate = lbModern ? 60 : 32;
                    int liAtOne = 0;

                    for (int liFrame = 0; liFrame < 3 * liRate; liFrame++)
                    {
                        lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                        lOPlayer.Walk = UWPlayerMotion.WalkRun;
                        lOPlayer.Frame(lbModern ? (liFrame & 1) == 0 ? ModernFrameA : ModernFrameB : 8);

                        if (liFrame == liRate)
                            liAtOne = lOPlayer.Params.Speed;

                        if (liFrame % (liRate / 4) == 0)
                            lsTrace += string.Format("{0:F2}s s{1} st{2} fw{3}|", liFrame / (double)liRate, lOPlayer.Params.Speed, lOPlayer.CurrentState, lOPlayer.ForwardSpeed);
                    }

                    if (Environment.GetEnvironmentVariable("UR_ICE_TRACE") != null)
                        Console.WriteLine("    [grip " + leMode + (lbModern ? " Modern" : " Original") + "] " + lsTrace);

                    if (leMode != UWIceGripEnum.Off)
                        Check.Bool("ice grip: " + leMode + (lbModern ? ", Modern" : ", Original") + " keeps the run on ice after a second, before the wall (speed " + liAtOne + ")",
                            liAtOne >= 780, true);
                }
            }

            UWIceGrip.ModeSource = lOBefore;
        }

        /// <summary>LEVEL 33'S ICE SLOPE AND JUMP TRAP (2026-11-15, per user: the trap at 31/18 must throw the player over
        /// the block at 31/20, and the ice grip must not leave him stuck): from standing at 31/12 the slope's push takes
        /// him down under every ice grip (ice slopes stay the original's) in Original, Smooth and Modern; on entering
        /// 31/18 the jump trap there (0x192 quality 24, owner 0, heading 3: at least 141) as UWTrapRulesUw2.fFireJump
        /// calls it must fire, and the jump must carry him off the trap's tile (the halved speed kept: until then the
        /// next frame took the full speed again and he hit the block's face). UR_ICE_TRACE prints the run.</summary>
        private static void fIceSlopeJump(DataImport pOImport)
        {
            UWLevel lOLevel = pOImport.Levels[32];
            UWTerrainUw2 lOTerrain = pOImport.Terrain.Uw2;
            Func<UWIceGripEnum> lOBefore = UWIceGrip.ModeSource;

            foreach (UWIceGripEnum leGrip in new[] { UWIceGripEnum.Off, UWIceGripEnum.Half, UWIceGripEnum.Full })
            foreach (int liMode in new[] { 0, 1, 2 })
            {
                UWIceGrip.ModeSource = () => leGrip;
                LevelMotionWorld lOWorld = new LevelMotionWorld(lOLevel, pOImport.Terrain, 33);
                UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOImport.CommonObjectProperties);

                lOPlayer.PlaceInTile(31, 12);
                lOPlayer.TerrainWordAt = (piTx, piTy) =>
                {
                    UWTile lOTile = lOLevel.GetTile(piTx, piTy);

                    return lOTile != null ? lOTerrain.Word(lOTile.TextureFloor) : 0;
                };
                lOPlayer.Precise = liMode > 0;
                lOPlayer.FineTicks = liMode > 0;
                lOPlayer.Modern = liMode == 2;
                lOPlayer.RampFrameTicks = 0;

                string lsTrace = string.Empty;
                int liRate = liMode == 0 ? 32 : 60;
                bool lbFired = false;
                bool lbJumped = false;
                double lfMaxY = 0.0;

                for (int liFrame = 0; liFrame < 8 * liRate; liFrame++)
                {
                    lOPlayer.MotionCommand = UWPlayerMotion.Command.None;
                    lOPlayer.Frame(liMode == 0 ? 8 : (liFrame & 1) == 0 ? ModernFrameA : ModernFrameB);

                    int liTy = (int)(lOPlayer.FineY / 256f);
                    string lsJump = string.Empty;

                    if (!lbFired && liTy == 18)
                    {
                        int liVxy = Math.Abs(lOPlayer.Params.Vx) + Math.Abs(lOPlayer.Params.Vy);

                        lbFired = true;
                        lbJumped = lOPlayer.Jump(24, false, 3);
                        lsJump = " JUMP(vxy " + liVxy + ", speed " + lOPlayer.Params.Speed + ")=" + lbJumped;
                    }

                    lfMaxY = Math.Max(lfMaxY, lOPlayer.FineY / 256.0);

                    if ((liFrame % (liRate / 8)) == 0 || lsJump.Length > 0)
                        lsTrace += string.Format("{0:F2}s:{1:F2}/{2:F2} z{3} vz{4} g{5} s{6}{7}|", liFrame / (double)liRate, lOPlayer.FineX / 256f,
                            lOPlayer.FineY / 256f, lOPlayer.Params.Z, lOPlayer.Params.Vz, lOPlayer.Params.Gravity, lOPlayer.Params.Speed, lsJump);
                }

                string lsMode = leGrip + ", " + (liMode == 2 ? "Modern" : liMode == 1 ? "Smooth" : "Original");

                if (Environment.GetEnvironmentVariable("UR_ICE_TRACE") != null)
                    Console.WriteLine("    [slope jump " + lsMode + "] " + lsTrace);

                Check.Bool("ice slope: level 33, " + lsMode + ": slides to the jump trap at 31/18 and is thrown (furthest y " + lfMaxY.ToString("F2") + ")",
                    lbJumped && lfMaxY >= 19.5, true);
            }

            UWIceGrip.ModeSource = lOBefore;
        }

        /// <summary>THE ICE CAVERNS' OWN WALLS: every slippery ice tile (floor bits 0-6) beside a solid tile to one side,
        /// the player in its middle at the run's speed, heading at 30 and 60 degrees off that wall towards it, run
        /// on for 120 frames in Smooth and in Modern. Counted: where the motion ends heading away from the wall.</summary>
        private static void fIceLevelSweep(DataImport pOImport, int piLevel)
        {
            UWLevel lOLevel = pOImport.Levels[piLevel];
            UWTerrainUw2 lOTerrain = pOImport.Terrain.Uw2;
            int[] liDx = { 1, -1, 0, 0 };
            int[] liDy = { 0, 0, 1, -1 };
            int liCases = 0;
            int liSmoothAway = 0;
            int liModernAway = 0;
            string lsDiffer = string.Empty;

            for (int liY = 1; liY < 63; liY++)
            {
                for (int liX = 1; liX < 63; liX++)
                {
                    UWTile lOTile = lOLevel.GetTile(liX, liY);

                    if (lOTile == null || (int)lOTile.TileType != 1 || lOTerrain.Class(lOTile.TextureFloor) != UWTerrainUw2.ClassIce
                        || lOTerrain.Flow(lOTile.TextureFloor) == 7)
                        continue;

                    for (int liSide = 0; liSide < 4; liSide++)
                    {
                        UWTile lONext = lOLevel.GetTile(liX + liDx[liSide], liY + liDy[liSide]);

                        if (lONext == null || (int)lONext.TileType != 0)
                            continue;

                        // The wall's normal points back (-dx, -dy); the tangent turned a quarter.
                        double lfWall = Math.Atan2(liDx[liSide], liDy[liSide]);

                        foreach (double lfOff in new[] { 30.0, 60.0 })
                        {
                            foreach (int liTurn in new[] { -1, 1 })
                            {
                                double lfHeading = lfWall + (liTurn * (90.0 - lfOff) * Math.PI / 180.0);
                                int liHeading = (int)Math.Round(lfHeading * 65536.0 / (2.0 * Math.PI)) & 0xFFFF;
                                bool lbSmooth = fIceRunAway(pOImport, lOLevel, liX, liY, liHeading, liDx[liSide], liDy[liSide], false);
                                bool lbModern = fIceRunAway(pOImport, lOLevel, liX, liY, liHeading, liDx[liSide], liDy[liSide], true);

                                liCases++;
                                liSmoothAway += lbSmooth ? 1 : 0;
                                liModernAway += lbModern ? 1 : 0;

                                if (lbSmooth != lbModern && lsDiffer.Length < 400)
                                    lsDiffer += liX + "/" + liY + " h" + liHeading.ToString("X") + " side " + liSide + " off " + lfOff + (liTurn < 0 ? "L" : "R") + " S" + (lbSmooth ? 1 : 0) + "M" + (lbModern ? 1 : 0) + "; ";
                            }
                        }
                    }
                }
            }

            Console.WriteLine("    [sweep level " + (piLevel + 1) + "] " + liCases + " runs, away from the wall: Smooth " + liSmoothAway + ", Modern " + liModernAway + ". Differ: " + lsDiffer);
            Check.Bool("ice modes: level " + (piLevel + 1) + "'s ice walls, Modern turns away where Smooth does (Smooth " + liSmoothAway + ", Modern " + liModernAway + " of " + liCases + ")",
                liModernAway >= liSmoothAway - (liCases / 20), true);
        }

        /// <summary>One run of the sweep: true when the motion ends heading away from the wall (its part along the
        /// wall's direction pdX, pdY below zero).</summary>
        private static bool fIceRunAway(DataImport pOImport, UWLevel pOLevel, int piX, int piY, int piHeading, int piDx, int piDy, bool pbModern)
        {
            LevelMotionWorld lOWorld = new LevelMotionWorld(pOLevel, pOImport.Terrain, 1);
            UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOImport.CommonObjectProperties);
            UWTerrainUw2 lOTerrain = pOImport.Terrain.Uw2;

            lOPlayer.PlaceInTile(piX, piY);
            lOPlayer.TerrainWordAt = (piTx, piTy) =>
            {
                UWTile lOTile = pOLevel.GetTile(piTx, piTy);

                return lOTile != null ? lOTerrain.Word(lOTile.TextureFloor) : 0;
            };
            lOPlayer.Precise = true;
            lOPlayer.FineTicks = true;
            lOPlayer.Modern = pbModern;
            lOPlayer.RampFrameTicks = 0;
            lOPlayer.CameraYaw = piHeading;
            lOPlayer.MotionYaw = piHeading;

            // One frame to take the ice's state, then the run's speed set along the heading.
            lOPlayer.MotionCommand = UWPlayerMotion.Command.None;
            lOPlayer.Frame(ModernFrameA);
            lOPlayer.MotionYaw = piHeading;
            lOPlayer.Params.Heading = piHeading;
            lOPlayer.Params.Speed = 822;

            bool lbTrace = Environment.GetEnvironmentVariable("UR_ICE_TRACE") == piX + "/" + piY + "/" + piHeading.ToString("X");
            string lsTrace = string.Empty;

            for (int liFrame = 0; liFrame < 120; liFrame++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                lOPlayer.Walk = UWPlayerMotion.WalkRun;
                lOPlayer.Frame((liFrame & 1) == 0 ? ModernFrameA : ModernFrameB);

                if (lbTrace && (liFrame % 4) == 0)
                    lsTrace += string.Format("{0}:{1:F3}/{2:F3} y{3:X} s{4} {5}|", liFrame, lOPlayer.FineX / 256f, lOPlayer.FineY / 256f, lOPlayer.MotionYaw, lOPlayer.Params.Speed, lOPlayer.CurrentState);
            }

            if (lbTrace)
                Console.WriteLine("    [trace " + (pbModern ? "Modern" : "Smooth") + "] " + lsTrace);

            double lfYaw = (lOPlayer.MotionYaw & 0xFFFF) * 2.0 * Math.PI / 65536.0;

            return (Math.Sin(lfYaw) * piDx) + (Math.Cos(lfYaw) * piDy) < -0.05;
        }

        /// <summary>Ground to x 12, ice of floor bits 0 from 12 to 19, a wall at x 20 running north: a run from 6/4
        /// at heading 0x2AAB (60 degrees east of north, so 60 degrees off the wall), held throughout. Mirrored off the
        /// wall it leaves at 0xD555. Returns the motion yaw after 400 frames; the trace lists it every 16.</summary>
        private static int fIceWallRun(DataImport pOImport, bool pbModern, out int piSpeed, out double pfMinX, out string psTrace)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();

            lOWorld.Paint(12, 1, 19, 30, 1, 2, UWTerrainUw2.ClassIce);
            lOWorld.Paint(20, 1, 20, 30, 0, 2, 0);

            UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOImport.CommonObjectProperties);

            lOPlayer.PlaceInTile(6, 4);
            lOPlayer.TerrainWordAt = (piX, piY) => piX >= 12 && piX <= 19 ? UWTerrainUw2.ClassIce << 6 : 0;
            lOPlayer.Precise = true;
            lOPlayer.FineTicks = true;
            lOPlayer.Modern = pbModern;
            lOPlayer.RampFrameTicks = 0;
            lOPlayer.CameraYaw = 0x2AAB;
            lOPlayer.MotionYaw = 0x2AAB;

            psTrace = string.Empty;
            pfMinX = double.MaxValue;

            for (int liFrame = 0; liFrame < 400; liFrame++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                lOPlayer.Walk = UWPlayerMotion.WalkRun;
                lOPlayer.Frame((liFrame & 1) == 0 ? ModernFrameA : ModernFrameB);

                if ((liFrame & 15) == 0)
                    psTrace += string.Format("{0}:{1:F2}/{2:F2} y{3:X} s{4} {5}  ", liFrame, lOPlayer.FineX / 256f, lOPlayer.FineY / 256f,
                        lOPlayer.MotionYaw, lOPlayer.Params.Speed, lOPlayer.CurrentState);
            }

            piSpeed = lOPlayer.Params.Speed;

            return lOPlayer.MotionYaw & 0xFFFF;
        }
    }
}
