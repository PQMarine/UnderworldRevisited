using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.5 (2026-10-17): UWVoidRules. Pinned: EtherealVoidSpecialEffects_seg008_150's
    /// one-in-32 roll, the drain by the hit points, the quake's eleven in twelve and the compass, in
    /// UW.EXE's order of draws; and the ticks at 32 a second on the game's PIT ticks at any frame
    /// length.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckVoidRules()
        {
            Check.Group("The Void (UWVoidRules - cleanup step 4.5)");

            UWVoidRules lOFast = new UWVoidRules();
            UWVoidRules lOSlow = new UWVoidRules();
            int liFast = 0;
            int liSlow = 0;

            for (int liFrame = 0; liFrame < 128; liFrame++)
                liFast += lOFast.Advance(2, 32f);

            for (int liFrame = 0; liFrame < 16; liFrame++)
                liSlow += lOSlow.Advance(16, 32f);

            fExpectBool("void: a second of game time is 32 ticks, at 144 frames as at 16",
                liFast == 32 && liSlow == 32, true);
            fExpectInt("void: a rate of zero runs none", new UWVoidRules().Advance(256, 0f), 0);

            fExpectVoidTick("void: an effect above 100 hit points drains 0 to 5, quakes 15 + 0..29, turns the compass",
                150, new[] { 0, 4, 5, 7, 9 }, true, 4, 22, 9, "32768 6 12 30 16");
            fExpectVoidTick("void: above 20 a one-in-four roll, then 0 to 2", 30, new[] { 0, 0, 2, 1, 3, 5 }, true, 2, 18, 5,
                "32768 32768 3 12 30 16");
            fExpectVoidTick("void: at 1 hit point no drain, one in twelve no quake", 1, new[] { 0, 0, 4 }, true, 0, 0, 4,
                "32768 12 16");
            fExpectVoidTick("void: 31 of 32 ticks bring nothing", 150, new[] { 1 }, false, 0, 0, 0, "32768");
        }

        private static void fExpectVoidTick(string psName, int piHitPoints, int[] piDraws, bool pbEffect, int piDrain,
            int piQuake, int piCompass, string psRanges)
        {
            RecordingRandom lORandom = new RecordingRandom(piDraws);

            UWRandom.Use(lORandom);

            try
            {
                bool lbEffect = UWVoidRules.Tick(piHitPoints, out UWVoidRules.Effect lOEffect);

                fExpectBool(psName, lbEffect == pbEffect && lOEffect.Drain == piDrain && lOEffect.QuakeTicks == piQuake
                    && lOEffect.CompassStep == piCompass, true);
                Check.Str(psName + " (the draws)", string.Join(" ", lORandom.Ranges), psRanges);
            }
            finally
            {
                UWRandom.Use(null);
            }
        }
    }
}
