using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE WINDOWS' ROWS SIZED BY THEIR TEXT (cleanup 8b.1, 2026-10-24; decided per user the same day):
	/// a label column, a row of choices and the setup bar's items take the room their text needs in the
	/// language in force, so a longer language does not clip. The pseudo-language of step 8.5 showed the
	/// fixed widths running out: the Game window's Scheme label ran into "Original", the bar items into
	/// each other, Motion's Strength labels and the Graphics view-angle label over their sliders.
	///
	/// THE RULE: the widths the windows were laid out with stay the least a piece gets - in English
	/// nothing moves - and a text wider than that widens its piece by what it lacks plus a margin. The
	/// widths are layout units (720 lines high, UWGuiTheme); the text widths come in measured with the
	/// font in use (UWKitRows), so the rule itself needs no engine.
	/// </summary>
	public static class UWRowSizing
	{
		/// <summary>Between a label's text and what stands right of it.</summary>
		public const float Margin = 10f;

		/// <summary>Left and right of a text inside a button, a bar item or a choice (the list rows'
		/// padding, UWGuiTheme.SetupListPadding).</summary>
		public const float Padding = 10f;

		/// <summary>The width a button or a bar item gets: the one it was laid out with, or its text and
		/// the padding on both sides where that is more.</summary>
		public static float Fit(float pfTextWidth, float pfNominal)
		{
			return Math.Max(pfNominal, pfTextWidth + (2f * Padding));
		}

		/// <summary>How far a label column has to grow for a text of pfTextWidth: its text and the margin
		/// less the room it was laid out with, never below 0.</summary>
		public static float LabelShift(float pfTextWidth, float pfRoom)
		{
			return Math.Max(0f, pfTextWidth + Margin - pfRoom);
		}

		/// <summary>
		/// The widths of a row of choices over pfArea, written into paWidths (as long as the texts): an
		/// even share each while every text with its padding fits its share - the look the windows were
		/// laid out with; otherwise each choice its text and padding and what is left shared out evenly;
		/// where even that does not fit, each choice its text and padding and the row runs over its area.
		/// Returns the row's whole width.
		/// </summary>
		public static float SegmentWidths(float pfArea, float[] paTextWidths, float[] paWidths)
		{
			int liCount = paTextWidths.Length;

			if (liCount == 0)
				return 0f;

			float lfShare = pfArea / liCount;
			float lfNeeded = 0f;
			bool lbFits = true;

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				float lfNeed = paTextWidths[liAt] + (2f * Padding);

				lfNeeded += lfNeed;

				if (lfNeed > lfShare)
					lbFits = false;
			}

			if (lbFits)
			{
				for (int liAt = 0; liAt < liCount; liAt++)
					paWidths[liAt] = lfShare;

				return pfArea;
			}

			float lfSpare = Math.Max(0f, (pfArea - lfNeeded) / liCount);
			float lfTotal = 0f;

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				paWidths[liAt] = paTextWidths[liAt] + (2f * Padding) + lfSpare;
				lfTotal += paWidths[liAt];
			}

			return lfTotal;
		}

		/// <summary>
		/// NAMED SEGMENTS SIZED BY THEIR NAMES (cleanup 8b.4, 2026-10-24; the layout editor's Preset row,
		/// per user 2026-10-11: "Classic Modular" did not fit a third): each its name and an even share of
		/// what is left of pfArea, written into paWidths - the widths the IMGUI form gave them. Where the
		/// names do not fit at all, each keeps its name and the row runs over its area instead of
		/// squeezing them (the window widens to SpreadNeeded beforehand). Returns the row's width.
		/// </summary>
		public static float SpreadWidths(float pfArea, float[] paTextWidths, float[] paWidths)
		{
			int liCount = paTextWidths.Length;

			if (liCount == 0)
				return 0f;

			float lfTexts = 0f;

			for (int liAt = 0; liAt < liCount; liAt++)
				lfTexts += paTextWidths[liAt];

			float lfSpare = Math.Max(0f, (pfArea - lfTexts) / liCount);
			float lfTotal = 0f;

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				paWidths[liAt] = paTextWidths[liAt] + lfSpare;
				lfTotal += paWidths[liAt];
			}

			return lfTotal;
		}

		/// <summary>The room named segments want (SpreadWidths): their names and a margin each - the
		/// English Preset row keeps its laid-out width with it, a longer language widens the window.</summary>
		public static float SpreadNeeded(float[] paTextWidths)
		{
			float lfNeeded = 0f;

			foreach (float lfText in paTextWidths)
				lfNeeded += lfText + Margin;

			return lfNeeded;
		}
	}
}
