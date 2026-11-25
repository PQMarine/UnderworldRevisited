using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 4.6 (2026-10-18): the flasks' shown levels and bubbling moved out of
    /// UWCharacter into UWFlaskGauge. Pinned: the first pass shows the real level at once, then one
    /// step per slow update (a quarter of a second, the original's in both games since 2026-11-25) towards it, no bubbling while the
    /// resurrection's fill runs, a bubbling of eleven pictures at levels 1 to 7 only, and the shown fill half a step into its band.
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
            fExpectInt("flasks: one step per slow update (64 ticks)", lOGauge.ShownHealthLevel, UWFlasks.FillLevels - 1);

            fExpectBool("flasks: a slow update is 64 ticks of the 1/256 s clock (update_screen)", UWFlaskAnimation.StepSeconds == 0.25f, true);

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

            // THE BUBBLES, the original's adjust_flasks (both games, 2026-11-25): eleven pictures one a step, then the plain
            // level and the bubbling over; nothing at levels 0 and 8 to 12, where it waits.
            UWFlaskAnimation lOBubbles = new UWFlaskAnimation();
            string lsFrames = "";

            lOBubbles.Start();

            for (int liStep = 0; liStep < 12; liStep++)
            {
                lOBubbles.Step(4);
                lsFrames += lOBubbles.FrameAt(4) + " ";
            }

            Check.Str("flasks: a bubbling shows pictures 1 to 11, then none", lsFrames.Trim(), "1 2 3 4 5 6 7 8 9 10 11 -1");
            fExpectBool("flasks: and it is over", lOBubbles.IsBubbling, false);

            lOBubbles.Start();
            lOBubbles.Step(8);
            fExpectBool("flasks: at level 8 it waits", lOBubbles.IsBubbling && lOBubbles.FrameAt(8) == -1, true);
            lOBubbles.Step(7);
            fExpectInt("flasks: and goes on at level 7", lOBubbles.FrameAt(7), 1);
            fExpectInt("flasks: a frame shows only at levels 1 to 7", lOBubbles.FrameAt(0), -1);

            fExpectBool("flasks: the shown fill is half a step into its band",
                UWFlaskGauge.GetShownFill(6) == 6.5f / UWFlasks.FillLevels, true);
        }
    }
}
