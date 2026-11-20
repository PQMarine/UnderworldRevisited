using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// WALKING THROUGH PEACEFUL PEOPLE (UWCreaturePass, the aid of 2026-11-19 for the second game's castle): who counts
    /// as peaceful (the conversation's own test), where the aid works (only the second game's level of the rule set),
    /// the overlap that keeps a pair passing, and the player's movement in Smooth (the core) and Modern (the motor): a
    /// creature in the way blocks without the pass test, and with it the player walks through and pushes nobody.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckCreaturePass(DataImport pOData)
        {
            Check.Group("Walking through peaceful people (UW2 castle aid)");

            // Who is peaceful: UWClickRules.IsAgainstThePlayer, the test the conversation asks too.
            Check.Bool("pass: mellow and wandering is peaceful", UWCreaturePass.IsPeaceful(8, 0, 2, false), true);
            Check.Bool("pass: upset but not after the player is peaceful", UWCreaturePass.IsPeaceful(8, 0, 1, false), true);
            Check.Bool("pass: attacking the player is not", UWCreaturePass.IsPeaceful(5, 1, 1, false), false);
            Check.Bool("pass: withdrawing from the player is not", UWCreaturePass.IsPeaceful(6, 1, 2, false), false);
            Check.Bool("pass: attacking someone else is", UWCreaturePass.IsPeaceful(5, 7, 2, false), true);
            Check.Bool("pass: hostile is not", UWCreaturePass.IsPeaceful(8, 0, UWNpc.AttitudeHostile, false), false);
            Check.Bool("pass: an ally is", UWCreaturePass.IsPeaceful(5, 1, UWNpc.AttitudeHostile, true), true);
            Check.Bool("pass: the conversation's test agrees (a hostile one answers no)",
                UWClickRules.CreatureAnswers(136, 8, 0, UWNpc.AttitudeHostile, false), false);

            // Where: the second game's level of the rule set, nowhere in the first game.
            Check.Int("pass: the rule set's level is the castle", new UWRules().Levels.PassPeacefulUw2, 0);
            Check.Bool("pass: works on the second game's castle", UWCreaturePass.WorksOn(UWGame.Uw2, 0, 0), true);
            Check.Bool("pass: ... not on its second level", UWCreaturePass.WorksOn(UWGame.Uw2, 1, 0), false);
            Check.Bool("pass: ... not in the first game", UWCreaturePass.WorksOn(UWGame.Uw1, 0, 0), false);

            // The overlap that keeps a pair passing: the player radius 2, a creature radius 2 (one eighth smaller).
            Check.Bool("pass: overlap at the same spot", UWCreaturePass.Overlap(100, 100, 2, 100, 100, 2), true);
            Check.Bool("pass: overlap three eighths apart", UWCreaturePass.Overlap(100, 100, 2, 103, 100, 2), true);
            Check.Bool("pass: apart at four eighths", UWCreaturePass.Overlap(100, 100, 2, 104, 100, 2), false);

            foreach (bool lbModern in new[] { false, true })
            {
                string lsMode = lbModern ? "Modern" : "Smooth";
                int liPushes;
                bool lbAsked;

                double lfBlocked = fPassRun(pOData, lbModern, false, out liPushes, out lbAsked);

                Check.Bool("pass: " + lsMode + " - a creature in the way blocks without the test (x " + lfBlocked.ToString("F2") + ")",
                    lfBlocked < 10.6, true);

                double lfPassed = fPassRun(pOData, lbModern, true, out liPushes, out lbAsked);

                Check.Bool("pass: " + lsMode + " - with the test the player walks through (x " + lfPassed.ToString("F2") + ")",
                    lfPassed > 11.5, true);
                Check.Int("pass: " + lsMode + " - ... and pushes nobody", liPushes, 0);
                Check.Bool("pass: " + lsMode + " - ... the test is asked as the player, mover 1", lbAsked, true);
            }
        }

        /// <summary>The player walks east from tile 6/15 toward a creature in the middle of tile 10/15 for 300 frames; the
        /// x reached in tiles, with or without a pass test that lets that creature through.</summary>
        private static double fPassRun(DataImport pOData, bool pbModern, bool pbPass, out int piPushes, out bool pbAskedAsPlayer)
        {
            FakeMotionWorld lOWorld = new FakeMotionWorld();

            lOWorld.AddBody(10, 15, new UWMotionBody { Index = 52, ItemId = MotorRat, XPos = 4, YPos = 4, ZPos = 16, IsMobile = true, IsCreature = true });

            UWPlayerMotion lOPlayer = new UWPlayerMotion(lOWorld, pOData.CommonObjectProperties);
            bool lbAsked = false;

            lOPlayer.PlaceInTile(6, 15);
            lOPlayer.Precise = true;
            lOPlayer.FineTicks = true;
            lOPlayer.Modern = pbModern;
            lOPlayer.RampFrameTicks = 0;
            lOPlayer.CameraYaw = 0x4000;
            lOPlayer.MotionYaw = 0x4000;

            if (pbPass)
            {
                lOPlayer.PassesThrough = (piMover, pOBody) =>
                {
                    if (piMover == 1)
                        lbAsked = true;

                    return piMover == 1 && pOBody.Index == 52;
                };
            }

            for (int liFrame = 0; liFrame < 300; liFrame++)
            {
                lOPlayer.MotionCommand = UWPlayerMotion.Command.Walk;
                lOPlayer.Walk = UWPlayerMotion.WalkRun;
                lOPlayer.Frame((liFrame & 1) == 0 ? ModernFrameA : ModernFrameB);
            }

            piPushes = lOWorld.Pushes;
            pbAskedAsPlayer = lbAsked;

            return lOPlayer.FineX / 256.0;
        }
    }
}
