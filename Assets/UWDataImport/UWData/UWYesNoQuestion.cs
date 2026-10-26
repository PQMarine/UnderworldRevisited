using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A YES/NO QUESTION IN THE MESSAGE SCROLL (cleanup step 5.13, 2026-10-22) - repairing on the
	/// anvil and the trap found on a look ask the same way (per user from the original, 2026-09-17
	/// on repairing, 2026-09-30: "it must work exactly like there" for the trap): "Yes" is preset
	/// and shown behind the question; Y (and J) and N only switch the shown answer; Enter confirms
	/// the shown one; Escape answers no; in the classic scheme a RIGHT click confirms "Yes" and a
	/// LEFT click answers "No", the pad's A yes and B no; the modern scheme answers in its own box
	/// with OK and Cancel. The question and its answer stay in the log.
	///
	/// This is the state and the rule: what is shown, which inputs end it and what the answer is
	/// when both came in one frame (no). Which key, button or pad press means yes or no, the frame
	/// the question was asked in (whose click chose the item) and the hidden pointer are the
	/// controller's (UWPromptController); until this step everything was in Interaction.
	/// </summary>
	public sealed class UWYesNoQuestion
	{
		/// <summary>The words behind the question - the original's (msg.question.yes, .no).</summary>
		public static string YesWord => UWText.T("msg.question.yes");

		public static string NoWord => UWText.T("msg.question.no");

		private Action<bool> mFAnswered;

		/// <summary>A question is standing.</summary>
		public bool IsOpen { get; private set; }

		/// <summary>The question without its answer, so the line can be rewritten with "No".
		/// Null while none has been asked.</summary>
		public string Question { get; private set; }

		/// <summary>The answer shown behind the question - Yes until N switches it; Enter confirms
		/// it.</summary>
		public bool ShownAnswer { get; private set; } = true;

		/// <summary>The answer the last question ended with.</summary>
		public bool LastAnswer { get; private set; }

		/// <summary>The line in the scroll: the question and the shown answer.</summary>
		public string PromptText => PromptTextFor(ShownAnswer);

		/// <summary>The line with this answer - "Yes" while the question stands, the answer given
		/// once it ended.</summary>
		public string PromptTextFor(bool pbYes)
		{
			return Question + (pbYes ? YesWord : NoWord);
		}

		/// <summary>Asks: "Yes" preset; pFAnswered gets the answer when the question ends.</summary>
		public void Ask(string psQuestion, Action<bool> pFAnswered)
		{
			IsOpen = true;
			Question = psQuestion;
			ShownAnswer = true;
			mFAnswered = pFAnswered;
		}

		/// <summary>Y, J or N: only the shown answer changes.</summary>
		public void ShowAnswer(bool pbYes)
		{
			ShownAnswer = pbYes;
		}

		/// <summary>
		/// The inputs of one frame that mean yes and no (whichever keys, buttons or pad presses the
		/// controller read as such): nothing ends the question (false); both in one frame answer
		/// no; else the one that came in. True when the question ended.
		/// </summary>
		public bool TryAnswer(bool pbYes, bool pbNo)
		{
			if (!DecideAnswer(pbYes, pbNo, out bool lbAnswer))
				return false;

			Answer(lbAnswer);

			return true;
		}

		/// <summary>The rule alone, for a controller that writes the line before it answers: nothing
		/// in (false, no answer); both in one frame: no; else the one that came in.</summary>
		public static bool DecideAnswer(bool pbYes, bool pbNo, out bool pbAnswer)
		{
			pbAnswer = pbYes && !pbNo;

			return pbYes || pbNo;
		}

		/// <summary>Ends the question with this answer and hands it to the asker. The question is
		/// closed before the asker runs, so it may ask again at once.</summary>
		public void Answer(bool pbYes)
		{
			IsOpen = false;
			LastAnswer = pbYes;

			Action<bool> lFAnswered = mFAnswered;

			mFAnswered = null;

			if (lFAnswered != null)
				lFAnswered(pbYes);
		}
	}
}
