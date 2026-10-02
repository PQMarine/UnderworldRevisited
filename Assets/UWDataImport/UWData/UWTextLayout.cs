using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Text layout in the original fonts without any rendering: character and line spacing,
	/// cleaning up original strings and wrapping them at word boundaries. Engine-free since
	/// 2026-09-17 (P1 of the engine separation); UWFontRenderer on the Unity side renders the
	/// wrapped lines into textures.
	/// </summary>
	public static class UWTextLayout
	{
		/// <summary>
		/// Spacing between two characters in pixels.
		///
		/// Zero, not one. The characters carry no gap of their own, and the font is
		/// italic - the slant already separates them. An extra pixel makes the text
		/// wider than in the original (per user, 2026-09-03), so clearly that he read the
		/// 9674 of his save game as 9874: with the original spacing the 7 places
		/// its left column exactly into the gap that distinguishes a 6 from an 8.
		/// </summary>
		public const int CharacterSpacing = 0;

		/// <summary>
		/// Additional spacing between two lines.
		///
		/// Zero, for the same reason as the character spacing: the characters already bring
		/// their spacing with them. font5x6p is six pixels high, descenders as in g and y sit in the
		/// last of them.
		///
		/// The check: the message box is 30 pixels high and Interaction holds five
		/// messages - five times six gives exactly those 30 (2026-09-03).
		/// </summary>
		public const int LineSpacing = 0;

		/// <summary>
		/// Cleans up an original string for display.
		///
		/// The texts use typewriter quotation marks (`` and '') and contain
		/// two special characters whose exact meaning is not documented: '_' appears
		/// where the sentence has a space ("attacked_._._."), and '^' at the start of
		/// embedded quotations. Here they are treated as a space and as nothing respectively - an
		/// observation, not an evidenced rule.
		/// </summary>
		public static string CleanText(string psText)
		{
			if (string.IsNullOrEmpty(psText))
				return string.Empty;

			return psText
				.Replace("``", "\"")
				.Replace("''", "\"")
				.Replace("^", string.Empty)
				.Replace("_", " ")
				.TrimEnd('\r', '\n');
		}

		/// <summary>Wraps text at word boundaries to a maximum width.</summary>
		public static List<string> WrapText(UWFont pOFont, string psText, int piMaxWidth)
		{
			List<string> lOLines = new List<string>();

			if (pOFont == null || !pOFont.IsLoaded || string.IsNullOrEmpty(psText))
				return lOLines;

			string[] lsWords = psText.Split(' ');
			string lsCurrent = string.Empty;

			// Leading spaces belong to the line. Until 2026-09-03 they were dropped, because the
			// first word of a still empty line was taken over without a separator - with a
			// leading space, however, that first word is empty, and so the space vanished. Four
			// pixels of difference were enough to wrap a line one word too late (measured by the
			// user on the original: the Lakshi letter wraps after "your", ours only
			// after "performing").
			bool lbFirstWord = true;

			foreach (string lsWord in lsWords)
			{
				string lsCandidate = lbFirstWord ? lsWord : lsCurrent + " " + lsWord;

				lbFirstWord = false;

				if (pOFont.MeasureText(lsCandidate, 0, CharacterSpacing) > piMaxWidth && lsCurrent.Length > 0)
				{
					lOLines.Add(lsCurrent);
					lsCurrent = lsWord;
				}
				else
				{
					lsCurrent = lsCandidate;
				}
			}

			if (lsCurrent.Length > 0)
				lOLines.Add(lsCurrent);

			return lOLines;
		}
	}
}
