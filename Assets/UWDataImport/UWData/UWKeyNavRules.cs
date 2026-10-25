using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ARROW KEYS' RULES IN THE KIT'S WINDOWS (cleanup 8d.4, 2026-10-25, per user): what Left and
	/// Right do to a slider with the focus, and how far a scrolled area or a window moves so the entry
	/// with the focus shows whole. The kit (UWKitRows, UWKitKeyNav) reads the keys and draws; the
	/// arithmetic is here, in layout units, so the self-check holds it without the engine.
	///
	/// A SLIDER BY THE KEYS: Left and Right move it by its row's own step - the volumes' five per cent,
	/// the grime's half steps, Shadows at once whole steps, whatever the row snaps to - and land on that
	/// step's grid as the bar's own drag lands (the value rounded to a multiple of the step, as
	/// UWKitRows.Slider snaps it), kept within the slider's ends. A row without a step of its own (the
	/// colours, most Remastered effects: the bar sets any value) moves by about a twentieth of its range,
	/// rounded down to 1, 2 or 5 times a power of ten so the values read round (0.05 over 0 to 1, 0.1
	/// over 0 to 3, 0.2 over 0 to 4, 0.01 over 0 to 0.2).
	/// </summary>
	public static class UWKeyNavRules
	{
		/// <summary>A slider without a step of its own crosses its range in about this many key steps.</summary>
		private const float StepsPerRange = 20f;

		/// <summary>What a key moves a slider by: the row's step, or for a row without one about
		/// 1/StepsPerRange of the range as 1, 2 or 5 times a power of ten (0 for an empty range).</summary>
		public static float SliderKeyStep(float pfStep, float pfMin, float pfMax)
		{
			if (pfStep > 0f)
				return pfStep;

			float lfRange = Math.Abs(pfMax - pfMin);

			if (!(lfRange > 0f))
				return 0f;

			// A hair over the share, so a share that is itself round (1/20 = 0.05) is not lost to the
			// float's last bit.
			double ldWanted = lfRange / StepsPerRange * 1.00001;
			double ldPower = Math.Pow(10.0, Math.Floor(Math.Log10(ldWanted)));
			double ldStep = ldPower * 5.0 <= ldWanted ? ldPower * 5.0 : (ldPower * 2.0 <= ldWanted ? ldPower * 2.0 : ldPower);

			return (float)ldStep;
		}

		/// <summary>The value a key moves a slider to, piDirection -1 (Left) or 1 (Right): one key step
		/// (SliderKeyStep) on, on the step's grid as the bar snaps (the multiple of the step nearest to
		/// it, rounded as Mathf.Round), within pfMin and pfMax. An off-grid value moves to the grid point
		/// beyond it in that direction.</summary>
		public static float SliderKeyValue(float pfValue, int piDirection, float pfStep, float pfMin, float pfMax)
		{
			float lfStep = SliderKeyStep(pfStep, pfMin, pfMax);

			if (piDirection == 0 || !(lfStep > 0f))
				return pfValue;

			float lfAt = (pfValue + (Math.Sign(piDirection) * lfStep)) / lfStep;
			float lfValue = (float)Math.Round(lfAt) * lfStep;
			float lfLow = Math.Min(pfMin, pfMax);
			float lfHigh = Math.Max(pfMin, pfMax);

			return lfValue < lfLow ? lfLow : (lfValue > lfHigh ? lfHigh : lfValue);
		}

		/// <summary>
		/// How far a stretch pfStart..pfEnd has to move to lie within pfLow..pfHigh: 0 where it does
		/// already, else the least move - negative up (or left), positive down (or right); a stretch
		/// longer than the room moves its start to pfLow. A window dragged partly off the screen
		/// (the palette effects') moves by it so the focused row shows.
		/// </summary>
		public static float RevealShift(float pfStart, float pfEnd, float pfLow, float pfHigh)
		{
			if (pfEnd - pfStart >= pfHigh - pfLow || pfStart < pfLow)
				return pfLow - pfStart;

			if (pfEnd > pfHigh)
				return pfHigh - pfEnd;

			return 0f;
		}

		/// <summary>
		/// The scroll position at which a scrolled area pfView high shows its inside's stretch
		/// pfTop..pfBottom whole (the inside's own coordinates, pfScroll the position now): unchanged
		/// where it shows, else moved by the least, a stretch taller than the view to its top - and kept
		/// within 0 and pfRoom (how far the inside can scroll).
		/// </summary>
		public static float RevealScroll(float pfScroll, float pfView, float pfTop, float pfBottom, float pfRoom)
		{
			float lfScroll = pfScroll - RevealShift(pfTop - pfScroll, pfBottom - pfScroll, 0f, pfView);
			float lfRoom = Math.Max(0f, pfRoom);

			return lfScroll < 0f ? 0f : (lfScroll > lfRoom ? lfRoom : lfScroll);
		}
	}
}
