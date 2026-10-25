using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE COUNT TYPED FOR "Move how many?" (cleanup step 5.13, 2026-10-22): the digit buffer, its
	/// maximum, the d-pad's stepping with the one repeat rule, the commit that clamps and the cancel.
	/// Until then the classic count prompt (UWItemDrag) and the modern split box (UWModernBags.
	/// StackSplit) each kept their own buffer, first-key flag and repeat timer - the repeat alike to
	/// the hundredth, the rest a little different, and each difference is a user observation that
	/// stays: hence the Policy, which names them.
	///
	/// WHAT IS THE SAME: the number starts at 1; the first digit typed replaces it, the next ones
	/// append; Backspace takes one away; the d-pad's held direction steps the number once at once,
	/// again after FirstRepeatSeconds and then every RepeatSeconds, and by FastStep once the direction
	/// has been held FastAfterSeconds (per user, 2026-10-07 for the prompt, 2026-10-08 for the box);
	/// the commit takes at least 1 and at most the maximum.
	///
	/// WHAT DIFFERS (the policy):
	///   - WrapsAtEnds: the classic prompt goes round from the maximum to 1 and back, a step by ten
	///     stopping at the end first (per user, 2026-10-07); the modern box stops at the ends.
	///   - ClampsWhileTyping: the modern box turns a typed number above the stack into the stack at
	///     once; the classic prompt shows what was typed and clamps on the commit.
	///   - StepStartsNewNumber: in the modern box a digit typed after a step replaces the stepped
	///     number; in the classic prompt it appends to it.
	///   - ZeroCancels: the modern box's Enter with 0 or nothing typed lets the box go; the classic
	///     prompt's Enter takes 1 for a typed 0 and cancels only for nothing typed.
	///   - The classic prompt's preset 1 disappears on the FIRST key press, whichever key (per user,
	///     2026-09-01) - that is FirstKeyPressed, which the prompt calls on any key; the box clears
	///     it with the first digit only, so it never calls it.
	///
	/// Time comes in as a parameter (seconds of an unscaled clock); nothing here reads the engine.
	/// </summary>
	public sealed class UWCountInput
	{
		/// <summary>The held direction steps again after this (seconds).</summary>
		public const float FirstRepeatSeconds = 0.35f;

		/// <summary>And then every this (seconds).</summary>
		public const float RepeatSeconds = 0.07f;

		/// <summary>Held this long, the direction steps by FastStep.</summary>
		public const float FastAfterSeconds = 2f;

		public const int FastStep = 10;

		/// <summary>The number every prompt starts with.</summary>
		public const string Preset = "1";

		/// <summary>What one of the two prompts does differently (see the class comment).</summary>
		public sealed class Policy
		{
			public bool WrapsAtEnds;

			public bool ClampsWhileTyping;

			public bool StepStartsNewNumber;

			public bool ZeroCancels;
		}

		/// <summary>The classic "Move how many?" in the message scroll.</summary>
		public static readonly Policy ClassicPrompt = new Policy
		{
			WrapsAtEnds = true,
			ClampsWhileTyping = false,
			StepStartsNewNumber = false,
			ZeroCancels = false,
		};

		/// <summary>The modern bags' split box.</summary>
		public static readonly Policy ModernSplitBox = new Policy
		{
			WrapsAtEnds = false,
			ClampsWhileTyping = true,
			StepStartsNewNumber = true,
			ZeroCancels = true,
		};

		private readonly Policy mOPolicy;

		/// <summary>The d-pad's held direction and its repeat (UWRepeat since cleanup step 7.3,
		/// 2026-10-25; three fields here until then): a step on the push, again after
		/// FirstRepeatSeconds, then RepeatSeconds after each step.</summary>
		private readonly UWRepeat mOPadRepeat = new UWRepeat(FirstRepeatSeconds, RepeatSeconds, UWRepeat.RuleEnum.FromNow);

		public UWCountInput(Policy pOPolicy)
		{
			mOPolicy = pOPolicy ?? ClassicPrompt;
		}

		/// <summary>A prompt is running (between Begin and Commit or Cancel).</summary>
		public bool IsOpen { get; private set; }

		/// <summary>The most that can be taken - the stack's count.</summary>
		public int Maximum { get; set; }

		/// <summary>The digits as typed, possibly none at all.</summary>
		public string Text { get; private set; } = string.Empty;

		/// <summary>Whether the next digit replaces the number instead of appending to it: true
		/// after Begin (the preset 1) and, in the modern box, after a step.</summary>
		public bool IsFirstKey { get; private set; }

		/// <summary>The number typed; 0 when nothing is typed or the digits make no number.</summary>
		public int Value => int.TryParse(Text, out int liValue) ? liValue : 0;

		/// <summary>Whether the digits make a number at all (a typed 0 does, nothing does not).</summary>
		public bool HasTypedNumber => int.TryParse(Text, out _);

		/// <summary>The count the last Commit took, clamped; 0 after a Cancel.</summary>
		public int TakenCount { get; private set; }

		/// <summary>Opens the prompt on a stack of piMaximum with the preset 1.</summary>
		public void Begin(int piMaximum)
		{
			IsOpen = true;
			Maximum = piMaximum;
			Text = Preset;
			IsFirstKey = true;
			TakenCount = 0;
			mOPadRepeat.Reset();
		}

		/// <summary>The classic prompt's rule: on the first key press, whichever key, the preset goes
		/// - if it was not a digit, the field stays empty (per user, 2026-09-01).</summary>
		public void FirstKeyPressed()
		{
			if (!IsFirstKey)
				return;

			Text = string.Empty;
			IsFirstKey = false;
		}

		/// <summary>A digit typed: the first replaces the number, the next append; the modern box
		/// clamps to the maximum at once.</summary>
		public void TypeDigit(int piDigit)
		{
			if (IsFirstKey)
				Text = string.Empty;

			IsFirstKey = false;
			Text += piDigit;

			if (mOPolicy.ClampsWhileTyping && Value > Maximum)
				Text = Maximum.ToString();
		}

		/// <summary>Backspace: the last digit goes; nothing happens on an empty field.</summary>
		public void Backspace()
		{
			if (Text.Length == 0)
				return;

			Text = Text.Substring(0, Text.Length - 1);
			IsFirstKey = false;
		}

		/// <summary>
		/// The number stepped by piStep (the d-pad, the box's buttons and wheel, the arrow keys):
		/// round from the end to the start where the policy wraps, else held at the ends.
		///
		/// THE CLASSIC ROUND (per user, 2026-10-07): past the maximum the number goes to 1, below 1
		/// to the maximum - but a step by ten stops at the end first, so that a fast run does not
		/// skip it. A field without a number counts as 1 for the step.
		///
		/// THE MODERN CLAMP: the number stays between 1 and the maximum; a field without a number
		/// counts as 0, so one step up from nothing is 1.
		/// </summary>
		public void Step(int piStep)
		{
			if (mOPolicy.WrapsAtEnds)
			{
				int liNow = Value;
				int liValue = HasTypedNumber ? liNow : 1;

				liValue += piStep;

				// Round from the end to the start - a step by ten stops at the end first.
				if (liValue > Maximum)
					liValue = Math.Abs(piStep) > 1 && liNow < Maximum ? Maximum : 1;
				else if (liValue < 1)
					liValue = Math.Abs(piStep) > 1 && liNow > 1 ? 1 : Maximum;

				Text = liValue.ToString();
			}
			else
			{
				Text = Math.Max(1, Math.Min(Maximum, Value + piStep)).ToString();
			}

			IsFirstKey = mOPolicy.StepStartsNewNumber;
		}

		/// <summary>
		/// THE HELD DIRECTION of the d-pad (or the stick), once a frame with the direction pressed now
		/// (-1, 0, 1) and the time: steps at once when the direction changes, again after
		/// FirstRepeatSeconds, then every RepeatSeconds; by FastStep once the direction has been held
		/// FastAfterSeconds. Returns true when the number stepped this call.
		/// </summary>
		public bool HoldDirection(int piDirection, float pfNow)
		{
			if (!mOPadRepeat.Hold(piDirection, pfNow))
				return false;

			Step(piDirection * (pfNow - mOPadRepeat.PressedAt > FastAfterSeconds ? FastStep : 1));

			return true;
		}

		/// <summary>Takes piCount, held between 1 and the maximum, and closes the prompt. Returns the
		/// count taken.</summary>
		public int Commit(int piCount)
		{
			TakenCount = Math.Max(1, Math.Min(Maximum, piCount));
			IsOpen = false;
			mOPadRepeat.Reset();

			return TakenCount;
		}

		/// <summary>Closes the prompt without taking anything.</summary>
		public void Cancel()
		{
			TakenCount = 0;
			IsOpen = false;
			mOPadRepeat.Reset();
		}

		/// <summary>Enter: the typed number is taken (true), or the prompt is cancelled (false) - for
		/// nothing typed, and in the modern box for a typed 0 as well (ZeroCancels).</summary>
		public bool Enter()
		{
			if (!HasTypedNumber || (mOPolicy.ZeroCancels && Value <= 0))
			{
				Cancel();
				return false;
			}

			Commit(Value);

			return true;
		}
	}
}
