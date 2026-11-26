namespace UWDataImport.UWData
{
	/// <summary>
	/// Original raw descriptions (string block 4, see DataImport.GetObjectDescription)
	/// encode article and singular/plural directly in the string instead of as a finished
	/// sentence - confirmed by the user's knowledge:
	/// - "&amp;" separates singular (left) from plural (right), e.g. "a_torch&amp;torches".
	/// - "_" separates article (left) from the item name (right) and becomes a
	///   space, e.g. "a_hand axe" -> "a hand axe".
	///
	/// According to the user there are still exceptions to this scheme - deliberately not
	/// handled until a concrete case turns up.
	///
	/// THE ENGLISH NAME FORMAT only (translation wave 2, 2026-10-26): the names go through the
	/// language's grammar (IUWGrammar; UWText.ObjectName), whose English rules call these; the "You
	/// see" frame (FormatLookMessage until then) is the grammar's, IUWGrammar.LookSentence.
	/// </summary>
	public static class UWObjectDescriptionFormatter
	{
		/// <summary>The resolution of "&amp;" and "_" - the name with its article.</summary>
		public static string FormatItemName(string psRawDescription, bool pbPlural = false)
		{
			if (string.IsNullOrEmpty(psRawDescription))
				return string.Empty;

			return fApplyArticle(fChosenForm(psRawDescription, pbPlural));
		}

		/// <summary>
		/// fix_name_string's choice (UnderworldExhumed ui/GAMESTRN.C, both games): the singular before the "&amp;" or the
		/// plural after it - and a plural of a name without "&amp;" is the whole string with an "s" (per user 2026-11-26 on
		/// the second game's original: "You see 10 arrows.", ours said "10 arrow"; "an_arrow" has no plural form).
		/// </summary>
		private static string fChosenForm(string psRaw, bool pbPlural)
		{
			int liAmpersandIndex = psRaw.IndexOf('&');

			if (liAmpersandIndex >= 0)
				return pbPlural ? psRaw.Substring(liAmpersandIndex + 1) : psRaw.Substring(0, liAmpersandIndex);

			return pbPlural ? psRaw + "s" : psRaw;
		}

		/// <summary>
		/// Resolves the "_" and determines the article ANEW, based on the word that follows.
		///
		/// The article is in the string, but it only works as long as nothing comes
		/// in between. As soon as a condition word is inserted, it no longer fits: with
		/// "sturdy" in front, "an_open door" becomes "an sturdy open door", the original writes
		/// "a sturdy open door" (per user, 2026-09-04). The reference does the same, see
		/// look.GetArticle: it picks the article for the quality word if there is one, otherwise
		/// for the name.
		///
		/// Only "a" and "an" are touched. If something else is there, such as "the", it stays.
		/// </summary>
		private static string fApplyArticle(string psText)
		{
			int liSeparator = psText.IndexOf('_');

			if (liSeparator < 0)
				return psText;

			string lsArticle = psText.Substring(0, liSeparator);
			string lsRest = psText.Substring(liSeparator + 1);

			if (!string.Equals(lsArticle, "a", System.StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(lsArticle, "an", System.StringComparison.OrdinalIgnoreCase))
				return lsArticle + " " + lsRest;

			return (StartsWithVowel(lsRest) ? "an " : "a ") + lsRest;
		}

		/// <summary>Does the text start with a vowel? The reference checks exactly these five
		/// letters too, without special cases.</summary>
		public static bool StartsWithVowel(string psText)
		{
			if (string.IsNullOrEmpty(psText))
				return false;

			switch (char.ToUpperInvariant(psText[0]))
			{
				case 'A':
				case 'E':
				case 'I':
				case 'O':
				case 'U':
					return true;

				default:
					return false;
			}
		}

		/// <summary>Only the bare item name WITHOUT article - the part after the "_",
		/// e.g. "a_pole" -&gt; "pole". For messages that bring their own article,
		/// such as "The pole cannot be used on that." (see UWItemDrag). Descriptions without
		/// "_" (e.g. "leather leggings") stay unchanged.</summary>
		public static string FormatBareItemName(string psRawDescription, bool pbPlural = false)
		{
			string lsName = FormatItemName(psRawDescription, pbPlural);

			// THE ARTICLE OF THE FORM CHOSEN (per user 2026-11-26: a stack of moldy bread under the crosshair read "Of bread"):
			// a plural after the "&amp;" has no article, "loaves of bread" was cut at its first blank because the singular
			// before it had a "_".
			int liSpaceIndex = string.IsNullOrEmpty(psRawDescription) ? -1 : fChosenForm(psRawDescription, pbPlural).IndexOf('_');

			if (liSpaceIndex < 0)
				return lsName;

			int liCut = lsName.IndexOf(' ');
			return liCut < 0 ? lsName : lsName.Substring(liCut + 1);
		}
	}
}
