namespace UWDataImport.UWData
{
	/// <summary>
	/// ONE BUTTON, TWO MEANINGS (cleanup step 7.3, 2026-10-25): tapped it does one thing, held for
	/// HoldSeconds another. Until then three places kept their own timer, adding the frame times up
	/// while the button stayed down - the modern scheme's E (UWModernWorldInput: tapped the default use,
	/// held the direct use, 0.4 s), the pad's panel button (UWModernBags: tapped the character panel,
	/// held every bag, 0.5 s) and the pad's cast trigger (UWModernRunePanel: tapped the cast, held the
	/// rune panel, 0.35 s). The numbers stay each place's; what differs in the way they end is the
	/// Policy, named by what each place did:
	///
	///   - EndsOnHold: the hold ends the press - the release afterwards does nothing more (the pad's
	///     panel and cast); otherwise the press goes on until the release, which then does nothing
	///     either (E: its hold ring is gone once the hold acted).
	///   - TapNeedsRelease: only a release in this frame taps (E: a press lost while a panel held the
	///     world taps nothing); otherwise the button no longer down taps, released or not (the pad's
	///     panel and cast).
	///
	/// Time comes in as a parameter (seconds of the caller's clock): the hold is due HoldSeconds after
	/// the press by that clock - nothing here reads the engine or counts frames.
	/// </summary>
	public sealed class UWHoldTap
	{
		/// <summary>What one of the places does differently (see the class comment).</summary>
		public sealed class Policy
		{
			public bool EndsOnHold;

			public bool TapNeedsRelease;
		}

		/// <summary>The modern scheme's E.</summary>
		public static readonly Policy UseKey = new Policy { EndsOnHold = false, TapNeedsRelease = true };

		/// <summary>The pad's panel button and cast trigger.</summary>
		public static readonly Policy PadButton = new Policy { EndsOnHold = true, TapNeedsRelease = false };

		/// <summary>What an Update found: nothing yet, the hold is due, or a tap.</summary>
		public enum ResultEnum
		{
			None,
			Hold,
			Tap
		}

		/// <summary>The button has to be held this long for the hold (seconds).</summary>
		public readonly float HoldSeconds;

		private readonly Policy mOPolicy;

		private float mfPressedAt;

		private float mfLastNow;

		public UWHoldTap(float pfHoldSeconds, Policy pOPolicy)
		{
			HoldSeconds = pfHoldSeconds;
			mOPolicy = pOPolicy ?? PadButton;
		}

		/// <summary>A press is being followed (between Press and its end).</summary>
		public bool IsPressed { get; private set; }

		/// <summary>The hold of this press has acted (EndsOnHold ends the press with it instead).</summary>
		public bool HoldDone { get; private set; }

		/// <summary>How long the button has been held by the last Update's clock, -1 without a press.</summary>
		public float HeldSeconds => IsPressed ? mfLastNow - mfPressedAt : -1f;

		/// <summary>The button went down now: a new press.</summary>
		public void Press(float pfNow)
		{
			IsPressed = true;
			HoldDone = false;
			mfPressedAt = pfNow;
			mfLastNow = pfNow;
		}

		/// <summary>The press is gone without a tap or a hold (taken elsewhere, the pointer left).</summary>
		public void Cancel()
		{
			IsPressed = false;
		}

		/// <summary>
		/// Once a frame after the press frame: pbDown - the button is still down; pbReleased - it went up
		/// now. Hold once HoldSeconds have passed (once per press); Tap when the button is up before
		/// that, as the Policy says; the press ends with the tap or without one when the button is up.
		/// </summary>
		public ResultEnum Update(bool pbDown, bool pbReleased, float pfNow)
		{
			if (!IsPressed)
				return ResultEnum.None;

			mfLastNow = pfNow;

			if (pbDown)
			{
				if (HoldDone || pfNow - mfPressedAt < HoldSeconds)
					return ResultEnum.None;

				HoldDone = true;

				if (mOPolicy.EndsOnHold)
					IsPressed = false;

				return ResultEnum.Hold;
			}

			IsPressed = false;

			if (HoldDone || (mOPolicy.TapNeedsRelease && !pbReleased))
				return ResultEnum.None;

			return ResultEnum.Tap;
		}
	}
}
