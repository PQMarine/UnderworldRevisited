using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.6 (2026-10-18): the flasks' shown levels and bubbling moved out of
    /// UWCharacter into UWFlaskGauge. Pinned: the first pass shows the real level at once, then one
    /// step per eighth of a second towards it, no bubbling while a level moves or the
    /// resurrection's fill runs, and the shown fill half a step into its band.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckFlaskGauge()
        {
            Check.Group("Flask levels (UWFlaskGauge - cleanup step 4.6)");

            UWFlaskGauge lOGauge = new UWFlaskGauge();

            fExpectInt("flasks: nothing shown before the first pass", lOGauge.ShownHealthLevel, -1);

            fExpectBool("flasks: the first pass shows the real level at once (a load does not fill up)",
                lOGauge.Advance(0.01f, 30, 30, 10, 30, false) && lOGauge.ShownHealthLevel == UWFlasks.FillLevels
                && lOGauge.ShownManaLevel == 4, true);

            lOGauge.Advance(0.01f, 15, 30, 10, 30, false);
            fExpectInt("flasks: a hit does not jump the level", lOGauge.ShownHealthLevel, UWFlasks.FillLevels);

            lOGauge.Advance(UWFlaskAnimation.StepSeconds, 15, 30, 10, 30, false);
            fExpectInt("flasks: one step per eighth of a second", lOGauge.ShownHealthLevel, UWFlasks.FillLevels - 1);

            fExpectInt("flasks: a moving health flask does not bubble", lOGauge.HealthBubbleFrame, -1);

            lOGauge.Advance(UWFlaskAnimation.StepSeconds * 10f, 15, 30, 10, 30, false);
            fExpectInt("flasks: and it arrives at half", lOGauge.ShownHealthLevel, 6);

            UWFlaskGauge lOFilling = new UWFlaskGauge();
            bool lbBubbled = false;

            lOFilling.Advance(0.01f, 15, 30, 15, 30, true);

            for (int liStep = 0; liStep < 400; liStep++)
            {
                lOFilling.Advance(UWFlaskAnimation.StepSeconds, 15, 30, 15, 30, true);
                lbBubbled |= lOFilling.HealthBubbleFrame >= 0 || lOFilling.ManaBubbleFrame >= 0;
            }

            fExpectBool("flasks: nothing bubbles while the resurrection fills them", lbBubbled, false);

            fExpectBool("flasks: the shown fill is half a step into its band",
                UWFlaskGauge.GetShownFill(6) == 6.5f / UWFlasks.FillLevels, true);
        }
    }
}
