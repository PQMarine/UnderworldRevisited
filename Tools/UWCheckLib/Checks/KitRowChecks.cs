using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 8b.1 (2026-10-24): the two engine-free pieces under the kit's rows - the sizing by
    /// text (UWRowSizing: a piece keeps the width it was laid out with and grows only by what its text
    /// lacks, so English stays as it was and a longer language does not clip) and the text field's line
    /// (UWTextLine: the one filter, the limit, the cursor, Backspace and Delete); since cleanup 8d.4 the
    /// arrow keys' rules (UWKeyNavRules). Data-free.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckKitRows()
        {
            Check.Group("Kit rows (UWRowSizing, UWTextLine - cleanup step 8b.1)");

            fCheckRowSizing();
            fCheckTextLine();
            fCheckKeyNavRules();
        }

        /// <summary>Cleanup 8d.4 (2026-10-25): the arrow keys on a slider with the focus (its row's step,
        /// or a round share of the range for a row without one; the grid the bar snaps to; the ends) and
        /// the view following the focus (the least scroll or move that shows the focused row whole).</summary>
        private static void fCheckKeyNavRules()
        {
            Check.Group("Kit arrow keys (UWKeyNavRules - cleanup 8d.4)");

            Check.Near("key step: a row's own step is the step", UWKeyNavRules.SliderKeyStep(0.05f, 0f, 1f), 0.05, 0.000001);
            Check.Near("key step: whole steps stay whole", UWKeyNavRules.SliderKeyStep(1f, 0f, 8f), 1.0, 0.000001);
            Check.Near("key step: no step over 0 to 1 is 0.05", UWKeyNavRules.SliderKeyStep(0f, 0f, 1f), 0.05, 0.000001);
            Check.Near("key step: no step over 0.5 to 2 is 0.05", UWKeyNavRules.SliderKeyStep(0f, 0.5f, 2f), 0.05, 0.000001);
            Check.Near("key step: no step over 0 to 3 is 0.1", UWKeyNavRules.SliderKeyStep(0f, 0f, 3f), 0.1, 0.000001);
            Check.Near("key step: no step over 0 to 4 is 0.2", UWKeyNavRules.SliderKeyStep(0f, 0f, 4f), 0.2, 0.000001);
            Check.Near("key step: no step over 0 to 0.2 is 0.01", UWKeyNavRules.SliderKeyStep(0f, 0f, 0.2f), 0.01, 0.000001);
            Check.Near("key step: no step over 0 to 0.8 is 0.02", UWKeyNavRules.SliderKeyStep(0f, 0f, 0.8f), 0.02, 0.000001);
            Check.Near("key step: an empty range has none", UWKeyNavRules.SliderKeyStep(0f, 1f, 1f), 0.0, 0.000001);

            Check.Near("key value: a volume one step up", UWKeyNavRules.SliderKeyValue(0.5f, 1, 0.05f, 0f, 1f), 0.55, 0.00001);
            Check.Near("key value: off the grid up to the next step", UWKeyNavRules.SliderKeyValue(0.53f, 1, 0.05f, 0f, 1f), 0.6, 0.00001);
            Check.Near("key value: off the grid down to the step below", UWKeyNavRules.SliderKeyValue(0.53f, -1, 0.05f, 0f, 1f), 0.5, 0.00001);
            Check.Near("key value: the top end holds", UWKeyNavRules.SliderKeyValue(1f, 1, 0.05f, 0f, 1f), 1.0, 0.00001);
            Check.Near("key value: the grime's half step", UWKeyNavRules.SliderKeyValue(1.5f, 1, 0.5f, 0f, 3f), 2.0, 0.00001);
            Check.Near("key value: the bottom end holds", UWKeyNavRules.SliderKeyValue(0f, -1, 0.5f, 0f, 3f), 0.0, 0.00001);
            Check.Near("key value: whole steps down", UWKeyNavRules.SliderKeyValue(3f, -1, 1f, 0f, 8f), 2.0, 0.00001);
            Check.Near("key value: a colour without a step", UWKeyNavRules.SliderKeyValue(1f, 1, 0f, 0.5f, 2f), 1.05, 0.00001);
            Check.Near("key value: a dragged effect up onto the grid", UWKeyNavRules.SliderKeyValue(1.2345f, 1, 0f, 0f, 3f), 1.3, 0.00001);
            Check.Near("key value: and down onto it", UWKeyNavRules.SliderKeyValue(1.2345f, -1, 0f, 0f, 3f), 1.1, 0.00001);
            Check.Near("key value: no direction, no change", UWKeyNavRules.SliderKeyValue(0.53f, 0, 0.05f, 0f, 1f), 0.53, 0.00001);

            float lfValue = 0.1f;

            for (int liStep = 0; liStep < 18; liStep++)
                lfValue = UWKeyNavRules.SliderKeyValue(lfValue, 1, 0.05f, 0.1f, 1f);

            Check.Near("key value: eighteen steps from 0.1 land on 1 without drift", lfValue, 1.0, 0.000001);

            Check.Near("reveal: a row in view keeps the scroll", UWKeyNavRules.RevealScroll(0f, 100f, 20f, 46f, 300f), 0.0, 0.0001);
            Check.Near("reveal: a row below scrolls down to its bottom", UWKeyNavRules.RevealScroll(0f, 100f, 120f, 146f, 300f), 46.0, 0.0001);
            Check.Near("reveal: a row above scrolls up to its top", UWKeyNavRules.RevealScroll(80f, 100f, 20f, 46f, 300f), 20.0, 0.0001);
            Check.Near("reveal: a row taller than the view shows its top", UWKeyNavRules.RevealScroll(0f, 100f, 150f, 300f, 500f), 150.0, 0.0001);
            Check.Near("reveal: never beyond the room", UWKeyNavRules.RevealScroll(0f, 100f, 380f, 420f, 300f), 300.0, 0.0001);
            Check.Near("shift: a row in the room stays", UWKeyNavRules.RevealShift(100f, 126f, 30f, 720f), 0.0, 0.0001);
            Check.Near("shift: a row below the edge moves up", UWKeyNavRules.RevealShift(700f, 726f, 30f, 720f), -6.0, 0.0001);
            Check.Near("shift: a row above the edge moves down", UWKeyNavRules.RevealShift(10f, 36f, 30f, 720f), 20.0, 0.0001);
        }

        private static void fCheckRowSizing()
        {
            Check.Near("fit: a short text keeps the nominal width", UWRowSizing.Fit(40f, 90f), 90f, 0.0001);
            Check.Near("fit: a long text gets its width and the padding", UWRowSizing.Fit(120f, 90f), 140f, 0.0001);
            Check.Near("fit: exactly at the edge stays nominal", UWRowSizing.Fit(70f, 90f), 90f, 0.0001);

            Check.Near("label shift: a text with room moves nothing", UWRowSizing.LabelShift(150f, 220f), 0f, 0.0001);
            Check.Near("label shift: the margin counts", UWRowSizing.LabelShift(215f, 220f), 5f, 0.0001);
            Check.Near("label shift: a long text grows the column by what it lacks", UWRowSizing.LabelShift(270f, 220f), 60f, 0.0001);

            float[] laWidths = new float[3];
            float lfTotal = UWRowSizing.SegmentWidths(408f, new[] { 40f, 90f, 45f }, laWidths);

            Check.Near("segments: fitting texts keep the even share", laWidths[1], 136f, 0.0001);
            Check.Near("segments: and the row its area", lfTotal, 408f, 0.0001);

            lfTotal = UWRowSizing.SegmentWidths(408f, new[] { 60f, 140f, 70f }, laWidths);

            Check.Near("segments: a long text takes its own width", laWidths[1], 140f + 20f + 26f, 0.0001);
            Check.Near("segments: the short ones theirs and the same spare", laWidths[0], 60f + 20f + 26f, 0.0001);
            Check.Near("segments: the row still fills its area", lfTotal, 408f, 0.001);

            lfTotal = UWRowSizing.SegmentWidths(300f, new[] { 100f, 140f, 100f }, laWidths);

            Check.Near("segments: too long for the area, each its text and padding", laWidths[1], 160f, 0.0001);
            Check.Near("segments: and the row runs over", lfTotal, 400f, 0.0001);

            // The layout editor's named segments (cleanup 8b.4): each its name and the rest shared, as
            // the IMGUI form - 300 over names of 40, 90 and 110 leaves 20 each.
            lfTotal = UWRowSizing.SpreadWidths(300f, new[] { 40f, 90f, 110f }, laWidths);

            Check.Near("spread: each its name and an even share of the rest", laWidths[0], 60f, 0.0001);
            Check.Near("spread: the long one as well", laWidths[2], 130f, 0.0001);
            Check.Near("spread: the row fills its area", lfTotal, 300f, 0.0001);

            lfTotal = UWRowSizing.SpreadWidths(200f, new[] { 40f, 90f, 110f }, laWidths);

            Check.Near("spread: too narrow, each its name", laWidths[1], 90f, 0.0001);
            Check.Near("spread: and the row runs over", lfTotal, 240f, 0.0001);
            Check.Near("spread: the room wanted, names and a margin each", UWRowSizing.SpreadNeeded(new[] { 40f, 90f, 110f }), 270f, 0.0001);
        }

        private static void fCheckTextLine()
        {
            UWTextLine lOLine = new UWTextLine();

            lOLine.Set("C:\\GOG", 0);
            Check.Int("line: the cursor starts after the text", lOLine.Caret, 6);
            Check.Bool("line: a letter goes in", lOLine.Insert('x'), true);
            Check.Str("line: at the cursor", lOLine.Text, "C:\\GOGx");
            Check.Bool("line: a line break is refused", lOLine.Insert('\n'), false);
            Check.Bool("line: a character beyond ASCII is refused", lOLine.Insert('\u00e9'), false);

            lOLine.Home();
            lOLine.Right();
            Check.Bool("line: a character in the middle", lOLine.Insert('-'), true);
            Check.Str("line: goes where the cursor stands", lOLine.Text, "C-:\\GOGx");
            Check.Bool("line: Backspace takes the one before", lOLine.Backspace(), true);
            Check.Str("line: and nothing else", lOLine.Text, "C:\\GOGx");
            Check.Bool("line: Delete takes the one after", lOLine.Delete(), true);
            Check.Str("line: the colon went", lOLine.Text, "C\\GOGx");

            lOLine.Home();
            Check.Bool("line: Backspace at the start does nothing", lOLine.Backspace(), false);
            lOLine.End();
            Check.Bool("line: Delete at the end does nothing", lOLine.Delete(), false);

            lOLine.Set("A name much too long for thirty characters", 30);
            Check.Int("limit: a longer text is cut to it", lOLine.Text.Length, 30);
            Check.Bool("limit: no more characters", lOLine.Insert('z'), false);
            lOLine.Backspace();
            Check.Bool("limit: after one went, one goes in again", lOLine.Insert('z'), true);
            Check.Int("limit: still thirty", lOLine.Text.Length, 30);

            lOLine.Set(null, 0);
            Check.Str("line: nothing is an empty line", lOLine.Text, string.Empty);
            Check.Bool("filter: the space is typeable", UWTextLine.IsTypeable(' '), true);
            Check.Bool("filter: the tilde is typeable", UWTextLine.IsTypeable('~'), true);
            Check.Bool("filter: DEL is not", UWTextLine.IsTypeable('\u007f'), false);

            fCheckTextLinePaths();
        }

        /// <summary>Cleanup step 8b.2 (2026-10-24): the folder path on the kit takes any character that
        /// is no control code (a path may hold umlauts, as IMGUI's field took them), and a paste goes in
        /// at the cursor through the line's filter and limit.</summary>
        private static void fCheckTextLinePaths()
        {
            UWTextLine lOLine = new UWTextLine { AnyText = true };

            lOLine.Set("C:\\Users\\J", 0);
            Check.Bool("any text: an umlaut goes in", lOLine.Insert('\u00fc'), true);
            Check.Bool("any text: a line break is still refused", lOLine.Insert('\n'), false);
            Check.Bool("any text: DEL is still refused", lOLine.Insert('\u007f'), false);
            Check.Str("any text: the path", lOLine.Text, "C:\\Users\\J\u00fc");

            lOLine.Home();
            Check.Int("paste: what went in", lOLine.InsertText("D:\\Spiele\r\n"), 9);
            Check.Str("paste: at the cursor, the line break dropped", lOLine.Text, "D:\\SpieleC:\\Users\\J\u00fc");
            Check.Int("paste: the cursor after it", lOLine.Caret, 9);
            Check.Int("paste: nothing is nothing", lOLine.InsertText(null), 0);

            UWTextLine lOName = new UWTextLine();

            lOName.Set("Name", 6);
            Check.Int("paste: the one filter and the limit hold", lOName.InsertText("\u00e9ab9"), 2);
            Check.Str("paste: only what fits", lOName.Text, "Nameab");
            Check.Bool("one filter: the umlaut stays refused", new UWTextLine().Takes('\u00fc'), false);
        }
    }
}
