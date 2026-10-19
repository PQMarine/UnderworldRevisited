using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// 2026-10-19: UWShakeRounds, the screen shake's rounds as PLAYMOVE.C runs them - the counter
    /// tested before its decrement, the byte wrapping in the last round (a strong jolt), the
    /// combat shake by 8 at most 3, the tremor by 10 at most 8, an idle channel at 1. Per user on
    /// the original: a trap's arrow doing 3 (level 0) shook the view.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckShakeRounds()
        {
            Check.Group("Screen shake rounds (UWShakeRounds - PLAYMOVE.C set_effect)");

            UWShakeRounds lORounds = new UWShakeRounds();

            fExpectInt("shake: nothing running, no round", lORounds.Round(), 0);

            // Level 0: set_effect(0x20, 0) - the jolt alone.
            lORounds.Start(UWShakeRounds.CombatChannel, 0);
            fExpectInt("shake: level 0 is one jolt at 1 + 3", lORounds.Round(), 4);
            fExpectInt("shake: ... and then nothing", lORounds.Round(), 0);

            // Level 1: five quiet rounds (counter 4 to 0 after the decrement, by 8 = 0), then the jolt.
            lORounds.Start(UWShakeRounds.CombatChannel, 5);
            string lsRounds = "";

            for (int liAt = 0; liAt < 8; liAt++)
                lsRounds += lORounds.Round() + " ";

            Check.Str("shake: level 1 is five weak rounds and the jolt", lsRounds.Trim(), "1 1 1 1 1 4 0 0");

            // Level 3: counter 15 - 14..8 give 1, 7..0 give 0, then the jolt.
            lORounds.Start(UWShakeRounds.CombatChannel, 15);
            int liFirst = lORounds.Round();
            int liCount = 1;

            while (lORounds.AnyRunning && liCount < 100)
            {
                lORounds.Round();
                liCount++;
            }

            fExpectInt("shake: level 3 starts at 1 + 14 / 8", liFirst, 2);
            fExpectInt("shake: ... and lasts 16 rounds, the last the jolt", liCount, 16);

            // The tremor (0x28, the Tremor spell): 39 / 10 = 3 plus the idle combat channel's 1.
            lORounds.Start(UWShakeRounds.TremorChannel, 0x28);
            fExpectInt("shake: a tremor of 0x28 starts at 3 + 1", lORounds.Round(), 4);

            // Both: the combat shake adds its phase to the tremor's amp.
            lORounds.Start(UWShakeRounds.CombatChannel, 15);
            fExpectInt("shake: tremor 38 / 10 and combat 14 / 8 add up", lORounds.Round(), 3 + 1);

            // The tremor's own last round: 255 / 10 capped at 8, plus the idle 1.
            UWShakeRounds lOTremor = new UWShakeRounds();

            lOTremor.Start(UWShakeRounds.TremorChannel, 1);
            fExpectInt("shake: tremor counter 1 - first round 0 / 10 + 1", lOTremor.Round(), 1);
            fExpectInt("shake: ... its last round the jolt, 8 + 1", lOTremor.Round(), 9);
            fExpectInt("shake: ... then nothing", lOTremor.Round(), 0);
        }
    }
}
