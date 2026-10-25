using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.4 (2026-10-17): UWPlayerTerrainRules. Pinned: one swim counter (0x60 on
    /// entering, 0 on leaving, a loaded one kept), the swim check as sink_sink_sink (nothing at or
    /// below 0x50, the rise capped at 0x8C), and the lava on the game's PIT ticks - the same rolls
    /// at any frame length, the first on the step onto the lava, none in the boots, a long pause
    /// dropped (Docs/DEVIATIONS.md, Rules: no rule hangs on the frame rate).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckTerrainRules()
        {
            Check.Group("Player terrain (UWPlayerTerrainRules - cleanup step 4.4)");

            fCheckSwimCounter();
            fCheckLavaRolls();
        }

        private static void fCheckSwimCounter()
        {
            UWPlayerTerrainRules lORules = new UWPlayerTerrainRules();

            lORules.UpdateSwimming(true);
            fExpectInt("swim: entering the water sets 0x60 (water_set)", lORules.SwimCounter, 0x60);

            lORules.UpdateSwimming(false);
            fExpectInt("swim: leaving it sets 0", lORules.SwimCounter, 0);

            lORules.LoadSwimCounter(0x74);
            lORules.UpdateSwimming(true);
            fExpectInt("swim: a counter loaded while swimming is kept (the sunken view)", lORules.SwimCounter, 0x74);

            RecordingRandom lORandom = new RecordingRandom();
            UWPlayerTerrainRules lOShallow = new UWPlayerTerrainRules();

            lOShallow.LoadSwimCounter(0x50);
            UWRandom.Use(lORandom);

            try
            {
                fExpectInt("swim: at 0x50 the tick's check does nothing", lOShallow.SwimCheck(0, 32), 0);
                fExpectInt("swim: ... and draws nothing", lORandom.Ranges.Count, 0);
            }
            finally
            {
                UWRandom.Use(null);
            }

            UWRandom.UseSeed(31);

            bool lbCapped = true;
            bool lbDamageInRange = true;
            bool lbDrowned = false;

            for (int liTry = 0; liTry < 200; liTry++)
            {
                UWPlayerTerrainRules lODeep = new UWPlayerTerrainRules();

                lODeep.LoadSwimCounter(0x8C);

                int liDamage = lODeep.SwimCheck(0, 32);

                lbCapped &= lODeep.SwimCounter == 0x8C;
                lbDamageInRange &= liDamage == 0 || (liDamage >= 2 && liDamage <= 2 + (2 * 4));
                lbDrowned |= liDamage > 0;
            }

            fExpectBool("swim: at 0x8C a failed check no longer sinks the view", lbCapped, true);
            fExpectBool("swim: above 0x78 the damage is two dice of 3 to 5 sides, or none", lbDamageInRange, true);
            fExpectBool("swim: an unpractised, loaded swimmer drowns", lbDrowned, true);
        }

        /// <summary>The lava's rolls over 10 s of game time in frames of piFrameTicks.</summary>
        private static int fLavaRolls(int piFrameTicks, bool pbBoots)
        {
            UWPlayerTerrainRules lORules = new UWPlayerTerrainRules();
            RecordingRandom lORandom = new RecordingRandom();

            UWRandom.Use(lORandom);

            try
            {
                lORules.TickLava(0, true, pbBoots, 8, 0.2);

                for (int liTicks = 0; liTicks < 2560; liTicks += piFrameTicks)
                    lORules.TickLava(piFrameTicks, true, pbBoots, 8, 0.2);
            }
            finally
            {
                UWRandom.Use(null);
            }

            return lORandom.Ranges.Count;
        }

        private static void fCheckLavaRolls()
        {
            fExpectInt("lava: 1/32 s is 8 PIT ticks", UWPlayerTerrainRules.SecondsToTicks(1f / 32f), 8);
            fExpectInt("lava: 10 s at 144 frames (about 2 ticks a frame): the first roll and one every 8 ticks",
                fLavaRolls(2, false), 1 + (2560 / 8));
            fExpectInt("lava: ... the same at 60 frames (4 ticks)", fLavaRolls(4, false), 1 + (2560 / 8));
            fExpectInt("lava: ... and at an original frame of 16 ticks", fLavaRolls(16, false), 1 + (2560 / 8));
            fExpectInt("lava: the dragon skin boots: no roll at all", fLavaRolls(4, true), 0);

            UWPlayerTerrainRules lORules = new UWPlayerTerrainRules();
            RecordingRandom lORandom = new RecordingRandom();

            UWRandom.Use(lORandom);

            try
            {
                fExpectInt("lava: the step onto it rolls at once (a sure roll hits)", lORules.TickLava(0, true, false, 8, 1.0), 1);
                lORules.TickLava(1000, true, false, 8, 1.0);
                fExpectInt("lava: a pause of 1000 ticks rolls once, the backlog dropped", lORandom.Ranges.Count, 2);

                lORules.TickLava(4, false, false, 8, 1.0);
                lORules.TickLava(4, true, false, 8, 1.0);
                fExpectInt("lava: off it and back, the first roll comes at once again", lORandom.Ranges.Count, 3);
                fExpectInt("lava: never hits at chance 0", lORules.TickLava(64, true, false, 8, 0.0), 0);
            }
            finally
            {
                UWRandom.Use(null);
            }
        }
    }
}
