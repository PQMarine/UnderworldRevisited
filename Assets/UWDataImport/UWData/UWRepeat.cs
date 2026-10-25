namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ONE KEY REPEAT (cleanup step 7.3, 2026-10-25): a held key, arrow or stick direction acts once
	/// on its press, again after Delay and then every Every, as the system's key repeat does for typed
	/// text. Until then six places kept their own timer - the kit's arrow keys (UWKitKeyNav), the help's
	/// text area and reading mode (UWKitRows), the pad's letter grid (UWLetterGrid), the count of the
	/// classic prompt and the modern split box on the pad (UWCountInput), the pad pointer's wheel
	/// (UWGamepadPointer) and the easy movement (UWPlayerInput) -, each with its own numbers and its own
	/// way of setting the next repeat. The numbers stay each place's (the caller passes them); how the
	/// next repeat is set is the Rule, named by what each place did:
	///
	///   - FromNow: the next repeat Every after this one (the letter grid, the count, the easy movement);
	///   - Accumulate: Every after the last one's due time, however late this one came (the wheel);
	///   - AccumulateOrNow: as Accumulate, but a due time already past starts Every from now (the kit's
	///     arrow keys);
	///   - AccumulateOrCatchUp: as Accumulate, but a due time already past becomes now, so one more
	///     repeat follows in the next call (the text area and the reading mode).
	///
	/// Time comes in as a parameter (seconds of the caller's clock); nothing here reads the engine or
	/// counts calls. A key is any non-zero number the caller chooses (a key code, a direction); 0 is
	/// "nothing held".
	/// </summary>
	public sealed class UWRepeat
	{
		/// <summary>How the next repeat is set after one (see the class comment).</summary>
		public enum RuleEnum
		{
			FromNow,
			Accumulate,
			AccumulateOrNow,
			AccumulateOrCatchUp
		}

		/// <summary>The held key repeats this long after its press (seconds).</summary>
		public float Delay;

		/// <summary>And then every this long (seconds).</summary>
		public float Every;

		public readonly RuleEnum Rule;

		private float mfNext;

		public UWRepeat(float pfDelay, float pfEvery, RuleEnum peRule)
		{
			Delay = pfDelay;
			Every = pfEvery;
			Rule = peRule;
		}

		/// <summary>The key held now, 0 for none.</summary>
		public int Held { get; private set; }

		/// <summary>When the held key was pressed (the count's fast step after two seconds).</summary>
		public float PressedAt { get; private set; }

		/// <summary>Nothing held any more; the next press starts afresh.</summary>
		public void Reset()
		{
			Held = 0;
		}

		/// <summary>
		/// Once a call per key and frame: pbPressed - the key piKey went down now (it becomes the held one
		/// and acts at once); pbDown - it is still down. True when the key acts in this call: on its press
		/// and on every repeat. A call for another key than the held one does nothing (the help's text area
		/// asks one key after the other with one repeat); the held key let go is nothing held.
		/// </summary>
		public bool Tick(int piKey, bool pbPressed, bool pbDown, float pfNow)
		{
			if (pbPressed)
			{
				Held = piKey;
				PressedAt = pfNow;
				mfNext = pfNow + Delay;

				return true;
			}

			if (Held == 0 || Held != piKey)
				return false;

			if (!pbDown)
			{
				Held = 0;
				return false;
			}

			if (pfNow < mfNext)
				return false;

			switch (Rule)
			{
				case RuleEnum.FromNow:
					mfNext = pfNow + Every;
					break;
				case RuleEnum.Accumulate:
					mfNext += Every;
					break;
				case RuleEnum.AccumulateOrNow:
					mfNext += Every;

					if (mfNext < pfNow)
						mfNext = pfNow + Every;

					break;
				default:
					mfNext = mfNext + Every > pfNow ? mfNext + Every : pfNow;
					break;
			}

			return true;
		}

		/// <summary>
		/// The same for a place that holds one thing at a time and only sees what is held now (a
		/// direction, a command): piHeld is it, 0 for nothing. A change to another non-zero value is a
		/// new press; 0 lets go.
		/// </summary>
		public bool Hold(int piHeld, float pfNow)
		{
			if (piHeld == 0)
			{
				Held = 0;
				return false;
			}

			return Tick(piHeld, piHeld != Held, true, pfNow);
		}
	}
}
