using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE TEXT OF A MULTI-LINE FIELD ON THE KIT (cleanup 8b.3, 2026-10-24): the help's notes, which
	/// IMGUI's TextArea kept until the help window moved to the kit (UWKitRows.TextArea). The text, the
	/// cursor in it, line breaks ('\n'), typing, pasting, Backspace and Delete, the cursor left and right
	/// and to any place (MoveTo). Where the lines wrap on the screen is the kit's to measure with the font
	/// in use, so the moves up and down and to a line's start or end, and a click into the text, hand the
	/// target place in through MoveTo. Engine-free, so the rules have self-checks.
	/// </summary>
	public sealed class UWTextArea
	{
		/// <summary>The text typed so far.</summary>
		public string Text { get; private set; } = string.Empty;

		/// <summary>Where the next character goes: 0 before the first, Text.Length after the last.</summary>
		public int Caret { get; private set; }

		/// <summary>
		/// THE NOTES' FILTER: every character but a control code and half of a surrogate pair. IMGUI's
		/// TextArea took accents and umlauts too, and the notes are written to the save's UWR.json as
		/// UTF-8 (UWHelpNotes), so they keep them; the one-line fields keep their ASCII filter
		/// (UWTextLine.IsTypeable). Line breaks come from Enter (NewLine), never from a typed character.
		/// </summary>
		public static bool IsTypeable(char pcChar)
		{
			return !char.IsControl(pcChar) && !char.IsSurrogate(pcChar);
		}

		/// <summary>Starts the area on a text, the cursor after its last character. The text is taken as
		/// it is - notes loaded from a save stay byte for byte what they were until the player types.</summary>
		public void Set(string psText)
		{
			Text = psText ?? string.Empty;
			Caret = Text.Length;
		}

		/// <summary>A typed character at the cursor; false when the filter refuses it.</summary>
		public bool Insert(char pcChar)
		{
			if (!IsTypeable(pcChar))
				return false;

			fInsert(pcChar.ToString());

			return true;
		}

		/// <summary>A line break at the cursor (Enter).</summary>
		public void NewLine()
		{
			fInsert("\n");
		}

		/// <summary>A pasted text at the cursor: line breaks kept (a carriage return and line feed as
		/// one), every other character through the filter. Returns how many characters went in.</summary>
		public int Paste(string psText)
		{
			string lsText = fNormalise(psText);
			StringBuilder lOKept = new StringBuilder(lsText.Length);

			foreach (char lcChar in lsText)
			{
				if (lcChar == '\n' || IsTypeable(lcChar))
					lOKept.Append(lcChar);
			}

			if (lOKept.Length > 0)
				fInsert(lOKept.ToString());

			return lOKept.Length;
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

		/// <summary>The cursor to a place, kept inside the text.</summary>
		public void MoveTo(int piAt)
		{
			Caret = piAt < 0 ? 0 : (piAt > Text.Length ? Text.Length : piAt);
		}

		private void fInsert(string psText)
		{
			Text = Text.Insert(Caret, psText);
			Caret += psText.Length;
		}

		private static string fNormalise(string psText)
		{
			if (string.IsNullOrEmpty(psText))
				return string.Empty;

			return psText.Replace("\r\n", "\n").Replace('\r', '\n');
		}
	}
}
