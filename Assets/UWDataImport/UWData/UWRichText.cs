namespace UWDataImport.UWData
{
	/// <summary>
	/// TEXT THAT GOES INTO RICH TEXT (cleanup step 5.12, 2026-10-20): a front end that reads tags in
	/// angle brackets (Unity's Text with rich text on) must not read them in what the player typed
	/// or the game says - a typed answer "&lt;b&gt;" or a name with "&lt;" would break the markup around
	/// it, and with it the layout (B31 of the cleanup plan: the modern conversation escaped nothing).
	/// The brackets become their single-guillemet lookalikes, as UWTextLabel has done for the modern
	/// font since its start (the names of save games could contain some). The one rule for both.
	/// </summary>
	public static class UWRichText
	{
		/// <summary>The text with no character a rich-text parser reads as markup.</summary>
		public static string Escape(string psText)
		{
			if (string.IsNullOrEmpty(psText) || (psText.IndexOf('<') < 0 && psText.IndexOf('>') < 0))
				return psText ?? string.Empty;

			return psText.Replace('<', '\u2039').Replace('>', '\u203A');
		}
	}
}
