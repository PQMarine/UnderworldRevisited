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
        }
    }
}
