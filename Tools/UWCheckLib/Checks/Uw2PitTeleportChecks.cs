using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE FALL INTO THE PIT ON UW2 LEVEL 3 (per user 2026-11-22, SAVE3: from 51/27 west into the pit 50/27 onto its
        /// teleport trigger 0x1A0; the original teleports without fall damage). The run in Original and in Modern: in
        /// which frame the move trigger is met and in which the fall damage comes, walking in at half the run.
        /// </summary>
        private static void fCheckUw2PitTeleport(CheckContext pOContext)
        {
            Check.Group("The fall into the pit on level 3 (UW2)");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("pit teleport", "no second game here: skipped");

                return;
            }

            DataImport lOImport = new DataImport(lsData, null, null, UWGameProfiles.Uw2);
            UWGameSession lOBefore = UWGameSession.Current;

            UWGameSession.MakeCurrent(lOImport.Session);

            try
            {
                foreach (bool lbModern in new[] { false, true })
                {
                    LevelMotionWorld lOLevel = new LevelMotionWorld(lOImport.Levels[2], lOImport.Terrain, 1);
                    UWPlayerMotion lOMotion = new UWPlayerMotion(lOLevel, lOImport.CommonObjectProperties);

                    lOMotion.PlaceInTile(51, 27);
                    lOMotion.PlaceAt((51 << 8) + 96, (27 << 8) + 72, lOMotion.Params.Z, 0xC000);
                    lOMotion.Precise = lbModern;
                    lOMotion.FineTicks = lbModern;
                    lOMotion.Modern = lbModern;
                    lOMotion.RampFrameTicks = lbModern ? 0 : 8;
                    lOMotion.CameraYaw = 0xC000;

                    List<string> lOTrace = new List<string>();
                    int liTriggerFrame = -1;
                    int liDamageFrame = -1;

                    for (int liAt = 0; liAt < 3000 && (liTriggerFrame < 0 || liDamageFrame < 0 || liAt < liTriggerFrame + 40); liAt++)
                    {
                        lOMotion.MotionCommand = UWPlayerMotion.Command.Walk;
                        lOMotion.Walk = liTriggerFrame < 0 ? UWPlayerMotion.WalkRun / 2 : 0;

                        bool lbResult = lbModern ? lOMotion.Frame((liAt & 1) == 0 ? ModernFrameA : ModernFrameB) : lOMotion.Frame(8);

                        if (liTriggerFrame < 0 && lOLevel.Triggers.Count > 0)
                        {
                            liTriggerFrame = liAt;
                            lOTrace.Add("trigger@" + liAt + " z" + lOMotion.Params.Z);
                        }

                        if (lbResult && lOMotion.Last.FallDamage > 0)
                        {
                            if (liDamageFrame < 0)
                                liDamageFrame = liAt;

                            lOTrace.Add("damage " + lOMotion.Last.FallDamage + "@" + liAt + " z" + lOMotion.Params.Z);
                        }
                    }

                    // No fall damage before the trigger (the host's frame drops a landing's damage in the trigger's step and
                    // holds the player still through the teleport's fade). Original meets it at its top in the air, as the
                    // original's set_targz aims; Modern on touching down (UWCharacterMotor's landing look).
                    bool lbOk = liTriggerFrame >= 0 && (liDamageFrame < 0 || liDamageFrame >= liTriggerFrame) && lOMotion.TileX == 50;

                    Check.Bool("pit teleport: " + (lbModern ? "Modern" : "Original") + " meets the trigger before any fall damage ("
                        + string.Join(", ", lOTrace) + ")", lbOk, true);
                }
            }
            finally
            {
                UWGameSession.MakeCurrent(lOBefore);
            }
        }
    }
}
