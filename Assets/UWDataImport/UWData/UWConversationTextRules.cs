namespace UWDataImport.UWData
{
	/// <summary>
	/// The conversation function "contains" as UW.EXE has it (Contains_ovr093_ED4, read 2026-10-01
	/// while listing Lakshi's keywords, per user: "align it with the original"). Ours found either
	/// string anywhere inside the other, so a typed "kn" or "knightly" counted as "knight".
	///
	///   - the SECOND argument (the typed text, [bx-2]) is searched for the FIRST (the keyword,
	///     [bx-4]); both are lowercased first (seg039_3495_85A: class 4 of the character table,
	///     A-Z, plus 0x20; in the original a string with an @ variable is a copy and keeps its case,
	///     ours lowercases it as well);
	///   - a hit counts only as a WHOLE WORD: the character before it is the start of the text, a
	///     blank (class 1: space, tab to carriage return) or a punctuation mark (class 0x40), and
	///     the one after it the end of the text, a blank or a punctuation mark - digits and letters
	///     are no boundary (the table at dseg 1E01);
	///   - after a hit that is no whole word the search goes on behind it, a keyword's length
	///     further.
	/// </summary>
	public static class UWConversationTextRules
	{
		public static bool ContainsWord(string psText, string psKeyword)
		{
			if (string.IsNullOrEmpty(psText) || string.IsNullOrEmpty(psKeyword))
				return false;

			string lsText = fLower(psText);
			string lsKeyword = fLower(psKeyword);
			int liFrom = 0;

			while (liFrom <= lsText.Length)
			{
				int liAt = lsText.IndexOf(lsKeyword, liFrom, System.StringComparison.Ordinal);

				if (liAt < 0)
					return false;

				int liEnd = liAt + lsKeyword.Length;

				if ((liAt == 0 || fIsBoundary(lsText[liAt - 1]))
					&& (liEnd >= lsText.Length || fIsBoundary(lsText[liEnd])))
					return true;

				liFrom = liEnd;
			}

			return false;
		}

		/// <summary>Only A-Z, as the table marks them; everything else stays.</summary>
		private static string fLower(string psText)
		{
			char[] laChars = psText.ToCharArray();

			for (int liAt = 0; liAt < laChars.Length; liAt++)
			{
				if (laChars[liAt] >= 'A' && laChars[liAt] <= 'Z')
					laChars[liAt] = (char)(laChars[liAt] + 0x20);
			}

			return new string(laChars);
		}

		/// <summary>Class 1 (blanks) or 0x40 (punctuation) of the table at dseg 1E01.</summary>
		private static bool fIsBoundary(char pcChar)
		{
			if (pcChar == ' ' || (pcChar >= '\t' && pcChar <= '\r'))
				return true;

			return (pcChar >= '!' && pcChar <= '/') || (pcChar >= ':' && pcChar <= '@')
				|| (pcChar >= '[' && pcChar <= '`') || (pcChar >= '{' && pcChar <= '~');
		}
	}
}
