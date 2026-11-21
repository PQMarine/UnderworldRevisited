using System;
using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE LARGE BLACKROCK GEM'S FACETS CAN BE REACHED (per user, 2026-11-16: "In die leuchtende Facette kann ich nicht
    /// treten"): on level 5 the player walks from 28/37 north towards the gem at 28/40 (fine 3/3, COMOBJ radius 5) in
    /// each motion mode; the south facet's move trigger (28/39, fine 2/7) must be hit before the gem stops him.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fGemFacetWalk(DataImport pOImport)
        {
            UWLevel lOLevel = pOImport.Levels[4];
            UWObject lOFacet = lOLevel.GetTile(28, 39).ObjectsInTile.Find(o => o != null && o.ID == UWObjectMechanics.MoveTriggerId);
            int liFacet = lOLevel.Masterlist.IndexOf(lOFacet);

            foreach (int liMode in new[] { 0, 1, 2 })
            foreach (bool lbNorth in new[] { false, true })
            {
                LevelMotionWorld lOWorld = new LevelMotionWorld(lOLevel, pOImport.Terrain, 5);
                UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOImport.CommonObjectProperties);

                lOPlayer.PlaceInTile(28, lbNorth ? 43 : 37);
                lOPlayer.CameraYaw = lbNorth ? 0x8000 : 0;
                lOPlayer.MotionYaw = lbNorth ? 0x8000 : 0;
                lOPlayer.Precise = liMode > 0;
                lOPlayer.FineTicks = liMode > 0;
                lOPlayer.Modern = liMode == 2;
                lOPlayer.RampFrameTicks = 0;
                lOPlayer.TerrainWordAt = (piTx, piTy) =>
                {
                    UWTile lOTile = lOLevel.GetTile(piTx, piTy);

                    return lOTile != null ? pOImport.Terrain.Uw2.Word(lOTile.TextureFloor) : 0;
                };

                int liRate = liMode == 0 ? 32 : 60;
                double lfFurthest = lbNorth ? 99.0 : 0.0;

                for (int liFrame = 0; liFrame < 4 * liRate; liFrame++)
                {
                    lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                    lOPlayer.Walk = UWPlayerMotion.WalkRun;
                    lOPlayer.Frame(liMode == 0 ? 8 : (liFrame & 1) == 0 ? ModernFrameA : ModernFrameB);
                    lfFurthest = lbNorth ? Math.Min(lfFurthest, lOPlayer.FineY / 256.0) : Math.Max(lfFurthest, lOPlayer.FineY / 256.0);
                }

                string lsMode = liMode == 2 ? "Modern" : liMode == 1 ? "Smooth" : "Original";

                Check.Str("uw2 gem: level 5, " + lsMode + (lbNorth ? ": walking south at the gem hits a north facet's trigger (furthest y " : ": walking north at the gem hits the south facet's trigger (furthest y ")
                    + lfFurthest.ToString("F2") + ", triggers " + string.Join(",", lOWorld.Triggers) + ")",
                    (lbNorth ? lOWorld.Triggers.Exists(i => lOLevel.Masterlist[i].TileY == 40 && lOLevel.Masterlist[i].YPos == 6) : lOWorld.Triggers.Contains(liFacet)).ToString(), "True");
            }

            fWallTriggerWalk(pOImport);
            fFrazniumFieldWalk(pOImport);
            fBenchClimbWalk(pOImport);
        }

        /// <summary>
        /// UP ONTO THE BENCH WITH A RUNNING JUMP (per user 2026-11-20, UW2 SAVE2 on level 4: Modern could not get onto the
        /// bench in front, Original and Smooth could; one must jump, and must be on it to get on behind it). The bench 0x150
        /// (height 9, radius 2) stands on 54/10 at fine 4/5 and fills the one tile wide passage. The player runs south from
        /// SAVE2's spot (54/11), jumps a little way on and must come onto the bench (north of y 10.6) in each motion mode.
        /// Modern ran up to full speed sooner, met the bench's side with its feet still below the top, and the motor
        /// dropped the speed into it for the rest of the jump.
        /// </summary>
        private static void fBenchClimbWalk(DataImport pOImport)
        {
            UWLevel lOLevel = pOImport.Levels[3];
            List<string> lOResults = new List<string>();
            List<string> lODetail = new List<string>();

            foreach (int liMode in new[] { 0, 1, 2 })
            foreach (int liYaw in new[] { 33323, 0x8000, 0x7800, 0x8800 })
            {
                LevelMotionWorld lOWorld = new LevelMotionWorld(lOLevel, pOImport.Terrain, 5);
                UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOImport.CommonObjectProperties);
                int liRate = liMode == 0 ? 32 : 60;
                double lfFurthest = 99.0;
                bool lbJumped = false;

                lOPlayer.PlaceAt((54 << 8) + 102, (11 << 8) + 159, 504, liYaw);
                lOPlayer.Precise = liMode > 0;
                lOPlayer.FineTicks = liMode > 0;
                lOPlayer.Modern = liMode == 2;
                lOPlayer.RampFrameTicks = 0;
                lOPlayer.TerrainWordAt = (piTx, piTy) =>
                {
                    UWTile lOTile = lOLevel.GetTile(piTx, piTy);

                    return lOTile != null ? pOImport.Terrain.Uw2.Word(lOTile.TextureFloor) : 0;
                };

                for (int liFrame = 0; liFrame < 4 * liRate; liFrame++)
                {
                    // A running jump a little way on, as one climbs it (per user: one has to jump).
                    bool lbJumpNow = !lbJumped && lOPlayer.FineY / 256.0 < 11.50;

                    lbJumped |= lbJumpNow;
                    lOPlayer.MotionCommand = lbJumpNow ? UWPlayerMotion.Command.Jump : UWPlayerMotion.Command.Walk;
                    lOPlayer.Walk = UWPlayerMotion.WalkRun;
                    lOPlayer.Frame(liMode == 0 ? 8 : (liFrame & 1) == 0 ? ModernFrameA : ModernFrameB);
                    lfFurthest = Math.Min(lfFurthest, lOPlayer.FineY / 256.0);

                }

                lOResults.Add(lfFurthest < 10.6 ? "1" : "0");
                lODetail.Add(lfFurthest.ToString("F2"));
            }

            Check.Str("uw2 level 4: a running jump south from 54/11 comes onto the bench on 54/10 (Original, Smooth, Modern; SAVE2's heading, south, a little either way; furthest y "
                + string.Join(" ", lODetail) + ")", string.Join("", lOResults), "111111111111");
        }

        /// <summary>
        /// THE FRAZNIUM FORCE FIELD LETS THE WEARER THROUGH IN EACH MOTION MODE (per user 2026-11-20, SAVE2: Modern stood
        /// at the field, Original stuttered briefly and passed, as the original does). Level 16: the field 0x16D at 31/33
        /// fine 3/3, move triggers that lower it (owner 1) at the centres of 30/33 and 32/33 and that raise it for the
        /// wearer (owner 0) at fine 0 and 6 of 31/33 (hack 11, check_fraznium). The player walks west from 32/33; the
        /// trap's effect is played here (the field to z 0x7F or 0) and the host's pacer too (a trigger once a quarter
        /// second); he must reach 30/33.
        /// </summary>
        private static void fFrazniumFieldWalk(DataImport pOImport)
        {
            UWLevel lOLevel = pOImport.Levels[15];
            UWObject lOField = lOLevel.GetTile(31, 33).ObjectsInTile.Find(o => o != null && o.ID == UWTrapRulesUw2.ForceFieldId);
            List<string> lOResults = new List<string>();

            if (lOField == null)
            {
                Check.Fail("uw2 level 16: the fraznium force field", "no field at 31/33");
                return;
            }

            int liFieldZ = lOField.ZPos;

            foreach (int liMode in new[] { 0, 1, 2 })
            foreach (int liStart in new[] { 0, 1, 2 })
            {
                LevelMotionWorld lOWorld = new LevelMotionWorld(lOLevel, pOImport.Terrain, 5);
                UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOImport.CommonObjectProperties);
                int liRate = liMode == 0 ? 32 : 60;
                List<int> lOReported = new List<int>();
                Dictionary<int, long> lOFiredUnit = new Dictionary<int, long>();
                double lfClock = 0.0;

                lOField.ZPos = liFieldZ;
                lOWorld.OnTrigger = piIndex => lOReported.Add(piIndex);

                // SAVE2's spot (fine 174/154 in 32/33, heading 48593) and two more headings from there.
                lOPlayer.PlaceAt((32 << 8) + 174, (33 << 8) + 154, 768, liStart == 0 ? 48593 : liStart == 1 ? 0xC000 : 0xB800);
                lOPlayer.Precise = liMode > 0;
                lOPlayer.FineTicks = liMode > 0;
                lOPlayer.Modern = liMode == 2;
                lOPlayer.RampFrameTicks = 0;
                lOPlayer.TerrainWordAt = (piTx, piTy) =>
                {
                    UWTile lOTile = lOLevel.GetTile(piTx, piTy);

                    return lOTile != null ? pOImport.Terrain.Uw2.Word(lOTile.TextureFloor) : 0;
                };

                for (int liFrame = 0; liFrame < 4 * liRate && lOPlayer.TileX > 30; liFrame++)
                {
                    int liTicks = liMode == 0 ? 8 : (liFrame & 1) == 0 ? ModernFrameA : ModernFrameB;

                    lOReported.Clear();
                    lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                    lOPlayer.Walk = UWPlayerMotion.WalkRun;
                    lOPlayer.Frame(liTicks);
                    lfClock += liMode == 0 ? liTicks : liTicks / 256.0;

                    // The host after the frame: the reported triggers in order, each once a clock unit (UWTriggerPacer,
                    // 16 PIT ticks), the trap's effect on the field.
                    long llUnit = (long)(lfClock / UWTriggerPacer.SpacingTicks);

                    foreach (int liIndex in lOReported)
                    {
                        if (lOFiredUnit.TryGetValue(liIndex, out long llLast) && llLast >= llUnit)
                            continue;

                        lOFiredUnit[liIndex] = llUnit;
                        lOField.ZPos = lOLevel.Masterlist[liIndex].TileX == 31 ? 0x7F : 0;
                    }
                }

                lOResults.Add(lOPlayer.TileX <= 30 ? "1" : "0");
            }

            lOField.ZPos = liFieldZ;

            Check.Str("uw2 level 16: wearing fraznium, walking west from 32/33 passes the force field at 31/33 (Original, Smooth, Modern; from SAVE2's spot at its heading, due west, a little south of west)",
                string.Join("", lOResults), "111111111");
        }

        /// <summary>
        /// A MOVE TRIGGER INSIDE A WALL (per user 2026-11-17: on level 5 the way down to level 4 fired only now and then in
        /// Modern): at the dead end 44/33 the trigger lies in the solid tile 44/32 at fine 3/6, two eighths behind the wall
        /// face; walking south from 44/35 into the wall must fire it in each motion mode.
        /// </summary>
        private static void fWallTriggerWalk(DataImport pOImport)
        {
            UWLevel lOLevel = pOImport.Levels[4];
            UWObject lOTrigger = lOLevel.GetTile(44, 32).ObjectsInTile.Find(o => o != null && o.ID == UWObjectMechanics.MoveTriggerId);
            int liTrigger = lOLevel.Masterlist.IndexOf(lOTrigger);
            List<string> lOResults = new List<string>();

            foreach (int liMode in new[] { 0, 1, 2 })
            foreach (int liYaw in new[] { 0x8000, 0x7800, 0x8800 })
            {
                LevelMotionWorld lOWorld = new LevelMotionWorld(lOLevel, pOImport.Terrain, 5);
                UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOImport.CommonObjectProperties);

                lOPlayer.PlaceInTile(44, 35);
                lOPlayer.CameraYaw = liYaw;
                lOPlayer.MotionYaw = liYaw;
                lOPlayer.Precise = liMode > 0;
                lOPlayer.FineTicks = liMode > 0;
                lOPlayer.Modern = liMode == 2;
                lOPlayer.RampFrameTicks = 0;
                lOPlayer.TerrainWordAt = (piTx, piTy) =>
                {
                    UWTile lOTile = lOLevel.GetTile(piTx, piTy);

                    return lOTile != null ? pOImport.Terrain.Uw2.Word(lOTile.TextureFloor) : 0;
                };

                int liRate = liMode == 0 ? 32 : 60;

                for (int liFrame = 0; liFrame < 4 * liRate; liFrame++)
                {
                    lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                    lOPlayer.Walk = UWPlayerMotion.WalkRun;
                    lOPlayer.Frame(liMode == 0 ? 8 : (liFrame & 1) == 0 ? ModernFrameA : ModernFrameB);
                }

                lOResults.Add(lOWorld.Triggers.Contains(liTrigger) ? "1" : "0");
            }

            Check.Str("uw2 level 5: walking south into the dead end 44/33 fires the trigger in the wall 44/32 (Original, Smooth, Modern; straight, a little left, a little right)",
                string.Join("", lOResults), "111111111");
        }
    }
}
