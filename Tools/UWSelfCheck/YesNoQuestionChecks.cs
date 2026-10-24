using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.13 (2026-10-22): the yes/no question of the message scroll (the anvil's repair,
    /// the trap found on a look) as UWYesNoQuestion - "Yes" preset, Y/J and N only switch the shown
    /// answer, the inputs of a frame end it (both in one frame: no), the asker gets the answer once
    /// the question is closed. Data-free.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckYesNoQuestion()
        {
            Check.Group("Yes/no question (UWYesNoQuestion - cleanup step 5.13)");

            UWYesNoQuestion lOQuestion = new UWYesNoQuestion();
            List<bool> lOAnswers = new List<bool>();

            Check.Bool("yes/no: closed before a question", lOQuestion.IsOpen, false);

            lOQuestion.Ask("Attempt repair? ", lOAnswers.Add);
            Check.Bool("yes/no: open once asked", lOQuestion.IsOpen, true);
            Check.Str("yes/no: the question", lOQuestion.Question, "Attempt repair? ");
            Check.Bool("yes/no: Yes is preset", lOQuestion.ShownAnswer, true);
            Check.Str("yes/no: the line shows the question and Yes", lOQuestion.PromptText, "Attempt repair? Yes");

            lOQuestion.ShowAnswer(false);
            Check.Str("yes/no: N switches the line to No", lOQuestion.PromptText, "Attempt repair? No");
            Check.Bool("yes/no: and the question still stands", lOQuestion.IsOpen, true);
            lOQuestion.ShowAnswer(true);
            Check.Str("yes/no: Y switches it back", lOQuestion.PromptText, "Attempt repair? Yes");
            Check.Str("yes/no: the line for No", lOQuestion.PromptTextFor(false), "Attempt repair? No");

            Check.Bool("yes/no: the rule: nothing in, no answer", UWYesNoQuestion.DecideAnswer(false, false, out bool lbDecided), false);
            Check.Bool("yes/no: the rule: yes in is yes", UWYesNoQuestion.DecideAnswer(true, false, out lbDecided) && lbDecided, true);
            Check.Bool("yes/no: the rule: no in is no", UWYesNoQuestion.DecideAnswer(false, true, out lbDecided) && !lbDecided, true);
            Check.Bool("yes/no: the rule: both in is no", UWYesNoQuestion.DecideAnswer(true, true, out lbDecided) && !lbDecided, true);

            Check.Bool("yes/no: no input, no answer", lOQuestion.TryAnswer(false, false), false);
            Check.Bool("yes/no: still open", lOQuestion.IsOpen, true);
            Check.Int("yes/no: the asker heard nothing", lOAnswers.Count, 0);

            Check.Bool("yes/no: yes ends it", lOQuestion.TryAnswer(true, false), true);
            Check.Bool("yes/no: closed", lOQuestion.IsOpen, false);
            Check.Int("yes/no: the asker heard once", lOAnswers.Count, 1);
            Check.Bool("yes/no: and heard yes", lOAnswers[0], true);
            Check.Bool("yes/no: the last answer", lOQuestion.LastAnswer, true);

            lOQuestion.Ask("Attempt repair? ", lOAnswers.Add);
            Check.Bool("yes/no: no ends it", lOQuestion.TryAnswer(false, true), true);
            Check.Bool("yes/no: heard no", lOAnswers[1], false);

            lOQuestion.Ask("Attempt repair? ", lOAnswers.Add);
            Check.Bool("yes/no: yes and no in one frame end it", lOQuestion.TryAnswer(true, true), true);
            Check.Bool("yes/no: as no", lOAnswers[2], false);

            // Enter confirms the shown answer - the controller passes it as the inputs.
            lOQuestion.Ask("Attempt repair? ", lOAnswers.Add);
            lOQuestion.ShowAnswer(false);
            lOQuestion.TryAnswer(lOQuestion.ShownAnswer, !lOQuestion.ShownAnswer);
            Check.Bool("yes/no: Enter on a shown No answers no", lOAnswers[3], false);

            // The modern box answers straight.
            lOQuestion.Ask("Attempt repair? ", lOAnswers.Add);
            lOQuestion.Answer(true);
            Check.Bool("yes/no: the box's OK answers yes", lOAnswers[4], true);
            Check.Bool("yes/no: closed by the box", lOQuestion.IsOpen, false);

            // The asker is forgotten with the answer: an answer without a question reaches nobody.
            lOQuestion.Answer(false);
            Check.Int("yes/no: an answer without a question reaches nobody", lOAnswers.Count, 5);

            // A new question goes to its own asker, and the question is closed before he runs.
            bool lbWasOpenInAsker = true;
            UWYesNoQuestion lONext = new UWYesNoQuestion();

            lONext.Ask("Trap. Disarm? ", pbYes => lbWasOpenInAsker = lONext.IsOpen);
            lONext.TryAnswer(true, false);
            Check.Bool("yes/no: closed before the asker runs", lbWasOpenInAsker, false);

            // Asking again from the asker works - the new question stands afterwards.
            lONext.Ask("First? ", pbYes => lONext.Ask("Second? ", null));
            lONext.TryAnswer(false, true);
            Check.Bool("yes/no: the asker may ask again at once", lONext.IsOpen, true);
            Check.Str("yes/no: and the new question stands", lONext.Question, "Second? ");
            Check.Bool("yes/no: with Yes preset again", lONext.ShownAnswer, true);
            Check.Bool("yes/no: an asker of nothing is fine", lONext.TryAnswer(true, false), true);
        }
    }
}
