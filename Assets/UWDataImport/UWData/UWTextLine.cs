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

		/// <summary>
		/// THE LANGUAGE'S LETTERS BEYOND ASCII (translation wave 2, 2026-10-26): the ones the text
		/// entries take besides printable ASCII - the grammar's TypeableLetters of the language in force
		/// (UWLanguage sets it at startup; German: a-, o-, u-umlaut, their capitals, the sharp s, which
		/// the classic fonts draw, UWFontGlyphBuilder). Empty in English, so English types as before.
		/// </summary>
		public static string LanguageLetters { get; set; } = string.Empty;

		/// <summary>One of the language's letters beyond ASCII (LanguageLetters).</summary>
		public static bool IsLanguageLetter(char pcChar)
		{
			return pcChar > 0x7E && !string.IsNullOrEmpty(LanguageLetters) && LanguageLetters.IndexOf(pcChar) >= 0;
		}

		/// <summary>The filter of the texts that may hold the language's letters - the conversation's
		/// answer, the mantra, the map note, the save name: printable ASCII and LanguageLetters, and with
		/// the input method (ImeOn) every character it commits.</summary>
		public static bool IsTypeableInLanguage(char pcChar)
		{
			return IsTypeable(pcChar) || IsLanguageLetter(pcChar) || (ImeOn && IsImeCharacter(pcChar));
		}

		/// <summary>
		/// THE SYSTEM'S INPUT METHOD (translation wave 6, 2026-10-26; grammar.rule.ime, UWLanguageFont):
		/// set with the language. While it is on, the text entries turn the input method on (UWTextEntry)
		/// and take every character it commits beyond ASCII (IsImeCharacter). Off in English and German.
		/// </summary>
		public static bool ImeOn { get; set; }

		/// <summary>A character an input method commits that a line takes: beyond ASCII, no control code,
		/// not half of a pair of UTF-16 surrogates (a character beyond the first 65536 - no font of the port
		/// would draw it).</summary>
		public static bool IsImeCharacter(char pcChar)
		{
			return pcChar > 0x7E && !char.IsControl(pcChar) && !char.IsSurrogate(pcChar);
		}

		/// <summary>
		/// THE LANGUAGE'S ALPHABET (translation 5.1, 2026-10-26; IUWGrammar.Alphabet): the gamepad letter
		/// grid's letter keys in order, upper case, and their lower case letter for letter (UWLetterGridLayout).
		/// A to Z in English and wherever the language's file names none. Set with UseLanguage.
		/// </summary>
		public static string Alphabet { get; private set; } = UWGrammarEnglish.LatinAlphabet;

		public static string AlphabetLower { get; private set; } = UWGrammarEnglish.LatinAlphabet.ToLowerInvariant();

		/// <summary>The language in force for the text entries (UWLanguage): its alphabet, and as
		/// LanguageLetters its TypeableLetters together with every letter of its alphabet beyond ASCII in
		/// both cases - so the fields take whatever the letter grid offers - and its input method (ImeOn).
		/// English: A to Z, no letters, no input method.</summary>
		public static void UseLanguage(IUWGrammar pIGrammar)
		{
			if (pIGrammar == null)
			{
				UseEnglish();

				return;
			}

			string lsUpper = pIGrammar.Alphabet;
			string lsLower = pIGrammar.AlphabetLower;
			System.Text.StringBuilder lOLetters = new System.Text.StringBuilder(pIGrammar.TypeableLetters ?? string.Empty);

			foreach (char lcChar in lsUpper + lsLower)
			{
				if (lcChar > 0x7E && lOLetters.ToString().IndexOf(lcChar) < 0)
					lOLetters.Append(lcChar);
			}

			Alphabet = lsUpper;
			AlphabetLower = lsLower;
			LanguageLetters = lOLetters.ToString();

			// The input method of the language whose texts are in force (UWText - grammar.rule.ime).
			ImeOn = UWLanguageFont.ImeWanted;
		}

		/// <summary>English for the text entries: A to Z, no letters beyond ASCII, no input method.</summary>
		public static void UseEnglish()
		{
			Alphabet = UWGrammarEnglish.LatinAlphabet;
			AlphabetLower = UWGrammarEnglish.LatinAlphabet.ToLowerInvariant();
			LanguageLetters = string.Empty;
			ImeOn = false;
		}

		/// <summary>
		/// WHAT A SAVE KEEPS (translation 5.1): printable ASCII and the Latin-1 letters (UWLevel.IsLatin1Letter),
		/// as their byte - the character's name in PLAYER.DAT, a map note, a save game's name. A letter of an
		/// alphabet beyond Latin-1 (Greek, Cyrillic) is typed into a conversation's answer or a mantra but
		/// cannot be kept there: the name's entry and the map note refuse it, the save name leaves it out.
		/// </summary>
		public static bool IsStorable(char pcChar)
		{
			return IsTypeable(pcChar) || UWLevel.IsLatin1Letter(pcChar);
		}

		/// <summary>The filter of a text the save keeps (the character's name): the language's (IsTypeableInLanguage)
		/// as far as the save can keep it (IsStorable).</summary>
		public static bool IsTypeableAndStorable(char pcChar)
		{
			return IsTypeableInLanguage(pcChar) && IsStorable(pcChar);
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

		/// <summary>Whether the line takes a character: the one filter with the language's letters
		/// (IsTypeableInLanguage, since translation wave 2 - the modern save name), or with AnyText every
		/// character that is no control code.</summary>
		public bool Takes(char pcChar)
		{
			return AnyText ? !char.IsControl(pcChar) : IsTypeableInLanguage(pcChar);
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
