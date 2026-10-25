using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.13 (2026-10-22): the count typed for "Move how many?" and the modern split box
    /// read one UWCountInput - the typing, Backspace, the maximum, the d-pad's repeat (0.35 s, then
    /// every 0.07 s, by ten after 2 s), the commit's clamp and the cancel; and what the two prompts
    /// do differently (wrap or clamp at the ends, clamp while typing, a digit after a step, Enter on
    /// 0), each as the user saw it. Data-free.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckCountInput()
        {
            Check.Group("Count input (UWCountInput - cleanup step 5.13)");

            fCheckCountTyping();
            fCheckCountStepping();
            fCheckCountRepeat();
            fCheckCountCommit();
        }

        private static void fCheckCountTyping()
        {
            UWCountInput lOClassic = new UWCountInput(UWCountInput.ClassicPrompt);

            lOClassic.Begin(13);
            Check.Bool("typing: open after Begin", lOClassic.IsOpen, true);
            Check.Str("typing: the preset 1", lOClassic.Text, "1");
            Check.Int("typing: the preset as a number", lOClassic.Value, 1);
            Check.Bool("typing: the first key is pending", lOClassic.IsFirstKey, true);
            Check.Int("typing: the maximum", lOClassic.Maximum, 13);

            lOClassic.TypeDigit(7);
            Check.Str("typing: the first digit replaces the 1", lOClassic.Text, "7");
            lOClassic.TypeDigit(5);
            Check.Str("typing: the next digit appends", lOClassic.Text, "75");
            Check.Int("typing: 75 is read", lOClassic.Value, 75);
            Check.Bool("typing: the classic prompt shows more than the stack", lOClassic.Value > lOClassic.Maximum, true);

            lOClassic.Backspace();
            Check.Str("backspace: the last digit goes", lOClassic.Text, "7");
            lOClassic.Backspace();
            Check.Str("backspace: down to nothing", lOClassic.Text, string.Empty);
            lOClassic.Backspace();
            Check.Str("backspace: nothing stays nothing", lOClassic.Text, string.Empty);
            Check.Int("backspace: nothing reads as 0", lOClassic.Value, 0);
            Check.Bool("backspace: nothing is no number", lOClassic.HasTypedNumber, false);

            // The classic rule: the preset goes on the first key press, whichever key.
            lOClassic.Begin(13);
            lOClassic.FirstKeyPressed();
            Check.Str("first key: a key that is no digit empties the field", lOClassic.Text, string.Empty);
            Check.Bool("first key: and the preset is gone", lOClassic.IsFirstKey, false);
            lOClassic.TypeDigit(4);
            Check.Str("first key: the digit after it appends to nothing", lOClassic.Text, "4");
            lOClassic.FirstKeyPressed();
            Check.Str("first key: a later key changes nothing", lOClassic.Text, "4");

            lOClassic.Begin(13);
            lOClassic.Backspace();
            Check.Str("first key: backspace on the preset takes the 1", lOClassic.Text, string.Empty);

            // The modern box clamps while typing and never clears on a key that is no digit.
            UWCountInput lOModern = new UWCountInput(UWCountInput.ModernSplitBox);

            lOModern.Begin(13);
            lOModern.TypeDigit(9);
            Check.Str("modern typing: 9 of 13", lOModern.Text, "9");
            lOModern.TypeDigit(9);
            Check.Str("modern typing: 99 becomes the stack at once", lOModern.Text, "13");
            lOModern.TypeDigit(0);
            Check.Str("modern typing: 130 becomes the stack again", lOModern.Text, "13");
            lOModern.Backspace();
            Check.Str("modern backspace: 1", lOModern.Text, "1");
            Check.Bool("modern backspace: the next digit appends", lOModern.IsFirstKey, false);
            lOModern.TypeDigit(2);
            Check.Str("modern typing: 12", lOModern.Text, "12");
        }

        private static void fCheckCountStepping()
        {
            UWCountInput lOClassic = new UWCountInput(UWCountInput.ClassicPrompt);

            lOClassic.Begin(5);
            lOClassic.Step(1);
            Check.Str("wrap: 1 up is 2", lOClassic.Text, "2");
            Check.Bool("wrap: a digit after a step appends (classic)", lOClassic.IsFirstKey, false);
            lOClassic.Step(1);
            lOClassic.Step(1);
            lOClassic.Step(1);
            Check.Str("wrap: up to the maximum", lOClassic.Text, "5");
            lOClassic.Step(1);
            Check.Str("wrap: past the maximum round to 1", lOClassic.Text, "1");
            lOClassic.Step(-1);
            Check.Str("wrap: below 1 round to the maximum", lOClassic.Text, "5");

            lOClassic.Begin(13);
            lOClassic.Step(UWCountInput.FastStep);
            Check.Str("wrap: by ten from 1 is 11", lOClassic.Text, "11");
            lOClassic.Step(UWCountInput.FastStep);
            Check.Str("wrap: by ten past the end stops at the end first", lOClassic.Text, "13");
            lOClassic.Step(UWCountInput.FastStep);
            Check.Str("wrap: by ten from the end rounds to 1", lOClassic.Text, "1");
            lOClassic.Step(-UWCountInput.FastStep);
            Check.Str("wrap: by ten down from 1 rounds to the maximum", lOClassic.Text, "13");
            lOClassic.Step(-UWCountInput.FastStep);
            Check.Str("wrap: by ten down from 13 is 3", lOClassic.Text, "3");
            lOClassic.Step(-UWCountInput.FastStep);
            Check.Str("wrap: by ten down past 1 stops at 1 first", lOClassic.Text, "1");

            lOClassic.Begin(13);
            lOClassic.FirstKeyPressed();
            lOClassic.Step(1);
            Check.Str("wrap: an empty field counts as 1, so up is 2", lOClassic.Text, "2");

            UWCountInput lOModern = new UWCountInput(UWCountInput.ModernSplitBox);

            lOModern.Begin(5);
            lOModern.Step(-1);
            Check.Str("clamp: 1 down stays 1", lOModern.Text, "1");
            lOModern.Step(1);
            Check.Str("clamp: 1 up is 2", lOModern.Text, "2");
            Check.Bool("clamp: a digit after a step replaces (modern)", lOModern.IsFirstKey, true);
            lOModern.TypeDigit(4);
            Check.Str("clamp: and does replace", lOModern.Text, "4");
            lOModern.Step(UWCountInput.FastStep);
            Check.Str("clamp: by ten past the end stops at the end", lOModern.Text, "5");
            lOModern.Step(1);
            Check.Str("clamp: and stays there", lOModern.Text, "5");
            lOModern.Step(-UWCountInput.FastStep);
            Check.Str("clamp: by ten below 1 stops at 1", lOModern.Text, "1");

            lOModern.Begin(5);
            lOModern.TypeDigit(3);
            lOModern.Backspace();
            lOModern.Step(1);
            Check.Str("clamp: an empty field counts as 0, so up is 1", lOModern.Text, "1");
        }

        private static void fCheckCountRepeat()
        {
            Check.Bool("repeat: the first repeat after 0.35 s", UWCountInput.FirstRepeatSeconds == 0.35f, true);
            Check.Bool("repeat: then every 0.07 s", UWCountInput.RepeatSeconds == 0.07f, true);
            Check.Bool("repeat: by ten after 2 s", UWCountInput.FastAfterSeconds == 2f, true);
            Check.Int("repeat: the fast step is ten", UWCountInput.FastStep, 10);

            UWCountInput lOInput = new UWCountInput(UWCountInput.ClassicPrompt);

            lOInput.Begin(100);
            Check.Bool("repeat: nothing held, nothing steps", lOInput.HoldDirection(0, 10f), false);
            Check.Bool("repeat: the press steps at once", lOInput.HoldDirection(1, 10f), true);
            Check.Str("repeat: to 2", lOInput.Text, "2");
            Check.Bool("repeat: not again at 0.1 s", lOInput.HoldDirection(1, 10.1f), false);
            Check.Bool("repeat: not yet at 0.34 s", lOInput.HoldDirection(1, 10.34f), false);
            Check.Bool("repeat: again at 0.36 s", lOInput.HoldDirection(1, 10.36f), true);
            Check.Str("repeat: to 3", lOInput.Text, "3");
            Check.Bool("repeat: not at 0.42 s", lOInput.HoldDirection(1, 10.42f), false);
            Check.Bool("repeat: again at 0.44 s", lOInput.HoldDirection(1, 10.44f), true);
            Check.Str("repeat: to 4", lOInput.Text, "4");
            Check.Bool("repeat: and at 0.52 s", lOInput.HoldDirection(1, 10.52f), true);
            Check.Str("repeat: to 5, still by one", lOInput.Text, "5");
            Check.Bool("repeat: at 2.01 s held", lOInput.HoldDirection(1, 12.01f), true);
            Check.Str("repeat: by ten once held two seconds", lOInput.Text, "15");

            // Letting go and pressing again starts over: at once, then the long wait.
            Check.Bool("repeat: released", lOInput.HoldDirection(0, 12.1f), false);
            Check.Bool("repeat: pressed again steps at once", lOInput.HoldDirection(1, 12.2f), true);
            Check.Str("repeat: by one again after a release", lOInput.Text, "16");
            Check.Bool("repeat: and waits 0.35 s again", lOInput.HoldDirection(1, 12.5f), false);

            // The other direction is a new press as well.
            Check.Bool("repeat: the other direction steps at once", lOInput.HoldDirection(-1, 12.51f), true);
            Check.Str("repeat: down to 15", lOInput.Text, "15");
            Check.Bool("repeat: and waits", lOInput.HoldDirection(-1, 12.6f), false);
        }

        private static void fCheckCountCommit()
        {
            UWCountInput lOClassic = new UWCountInput(UWCountInput.ClassicPrompt);

            lOClassic.Begin(13);
            Check.Int("commit: 1 is 1", lOClassic.Commit(1), 1);
            Check.Bool("commit: closes the prompt", lOClassic.IsOpen, false);
            Check.Int("commit: the count taken is kept", lOClassic.TakenCount, 1);

            lOClassic.Begin(13);
            Check.Int("commit: 999 is clamped to the stack", lOClassic.Commit(999), 13);
            lOClassic.Begin(13);
            Check.Int("commit: 0 is clamped to 1", lOClassic.Commit(0), 1);
            lOClassic.Begin(13);
            Check.Int("commit: the maximum is the maximum", lOClassic.Commit(13), 13);

            lOClassic.Begin(13);
            lOClassic.TypeDigit(5);
            lOClassic.Cancel();
            Check.Bool("cancel: closes the prompt", lOClassic.IsOpen, false);
            Check.Int("cancel: takes nothing", lOClassic.TakenCount, 0);
            Check.Str("cancel: the typed text stays readable", lOClassic.Text, "5");

            // Enter in the classic prompt: the typed number, a 0 as 1, nothing cancels.
            lOClassic.Begin(13);
            lOClassic.TypeDigit(4);
            Check.Bool("enter: takes the typed number", lOClassic.Enter(), true);
            Check.Int("enter: 4", lOClassic.TakenCount, 4);
            lOClassic.Begin(13);
            lOClassic.TypeDigit(0);
            Check.Bool("enter: a typed 0 takes (classic)", lOClassic.Enter(), true);
            Check.Int("enter: as 1", lOClassic.TakenCount, 1);
            lOClassic.Begin(13);
            lOClassic.FirstKeyPressed();
            Check.Bool("enter: nothing typed cancels", lOClassic.Enter(), false);
            Check.Bool("enter: and the prompt is closed", lOClassic.IsOpen, false);
            lOClassic.Begin(13);
            lOClassic.TypeDigit(9);
            lOClassic.TypeDigit(9);
            Check.Bool("enter: 99 takes", lOClassic.Enter(), true);
            Check.Int("enter: 99 clamped to the stack", lOClassic.TakenCount, 13);
            Check.Int("enter: the typed number stays readable for the prompt line", lOClassic.Value, 99);

            // Enter in the modern box: a 0 lets the box go.
            UWCountInput lOModern = new UWCountInput(UWCountInput.ModernSplitBox);

            lOModern.Begin(13);
            lOModern.TypeDigit(0);
            Check.Bool("enter: a typed 0 cancels (modern)", lOModern.Enter(), false);
            lOModern.Begin(13);
            lOModern.TypeDigit(3);
            lOModern.Backspace();
            Check.Bool("enter: nothing typed cancels (modern)", lOModern.Enter(), false);
            lOModern.Begin(13);
            lOModern.TypeDigit(6);
            Check.Bool("enter: 6 takes (modern)", lOModern.Enter(), true);
            Check.Int("enter: 6", lOModern.TakenCount, 6);
        }
    }
}
