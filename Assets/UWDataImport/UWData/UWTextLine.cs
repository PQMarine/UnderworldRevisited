namespace UWDataImport.UWData
{
	/// <summary>
	/// THE LINE OF A TEXT FIELD ON THE KIT (cleanup 8b.1, 2026-10-24): the text, the cursor in it and
	/// the most characters it takes - what IMGUI's TextField kept for the setup bar's folder path and the
	/// modern game menu's save name until the windows moved to the kit (UWKitRows.TextField). The
	/// characters come from UWTextEntry (the text events, and the keys themselves where none arrive, as
	/// on Linux - UWTypedKeys); Backspace, Delete, the arrows, Home and End move and edit here. Enter and
	/// Escape are the field's owner's to read. Engine-free, so the rules have self-checks.
	/// </summary>
	public sealed class UWTextLine
	{
		/// <summary>The text typed so far.</summary>
		public string Text { get; private set; } = string.Empty;

		/// <summary>Where the next character goes: 0 before the first, Text.Length after the last.</summary>
		public int Caret { get; private set; }

		/// <summary>The most characters the line takes; 0 for no limit.</summary>
		public int MaxLength { get; private set; }

		/// <summary>THE ONE FILTER of typed text: printable ASCII, 0x20 to 0x7E, no control codes
		/// (UWTextEntry's since cleanup 5.11; here since 8b.1, so the engine-free line keeps it too).</summary>
		public static bool IsTypeable(char pcChar)
		{
			return pcChar >= 0x20 && pcChar <= 0x7E;
		}

		/// <summary>Starts the line on a text, the cursor after its last character; a text longer than
		/// the limit is cut to it, as IMGUI's TextField with a length did.</summary>
		public void Set(string psText, int piMaxLength)
		{
			MaxLength = piMaxLength < 0 ? 0 : piMaxLength;

			string lsText = psText ?? string.Empty;

			if (MaxLength > 0 && lsText.Length > MaxLength)
				lsText = lsText.Substring(0, MaxLength);

			Text = lsText;
			Caret = lsText.Length;
		}

		/// <summary>A typed character at the cursor; false when the filter or the limit refuses it.</summary>
		public bool Insert(char pcChar)
		{
			if (!Takes(pcChar) || (MaxLength > 0 && Text.Length >= MaxLength))
				return false;

			Text = Text.Insert(Caret, pcChar.ToString());
			Caret++;

			return true;
		}

		/// <summary>The character before the cursor goes; false at the start.</summary>
		public bool Backspace()
		{
			if (Caret <= 0)
				return false;

			Text = Text.Remove(Caret - 1, 1);
			Caret--;

			return true;
		}

		/// <summary>The character after the cursor goes; false at the end.</summary>
		public bool Delete()
		{
			if (Caret >= Text.Length)
				return false;

			Text = Text.Remove(Caret, 1);

			return true;
		}

		public void Left()
		{
			if (Caret > 0)
				Caret--;
		}

		public void Right()
		{
			if (Caret < Text.Length)
				Caret++;
		}

		public void Home()
		{
			Caret = 0;
		}

		public void End()
		{
			Caret = Text.Length;
		}

		// ------------------------------------------------- Any text, pasted text (cleanup 8b.2)

		/// <summary>
		/// ANY TEXT BUT CONTROL CODES (cleanup 8b.2, 2026-10-24): the folder path takes what a path may
		/// hold - "C:\Users\Jürgen" - as IMGUI's field did; the other lines keep the one filter (the
		/// original's font knows ASCII only). Set by the field's owner before typing starts.
		/// </summary>
		public bool AnyText { get; set; }

		/// <summary>Whether the line takes a character: the one filter, or with AnyText every character
		/// that is no control code.</summary>
		public bool Takes(char pcChar)
		{
			return AnyText ? !char.IsControl(pcChar) : IsTypeable(pcChar);
		}

		/// <summary>A pasted text at the cursor, character by character through the line's filter and
		/// limit (a line break and whatever else it refuses drop out, as IMGUI's field took a paste);
		/// returns how many characters went in.</summary>
		public int InsertText(string psText)
		{
			int liCount = 0;

			if (psText == null)
				return 0;

			foreach (char lcChar in psText)
			{
				if (Insert(lcChar))
					liCount++;
			}

			return liCount;
		}
	}
}
