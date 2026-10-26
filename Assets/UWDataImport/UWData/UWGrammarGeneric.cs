using System;
using System.Collections.Generic;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE GRAMMAR OF A LANGUAGE WITHOUT A CLASS OF ITS OWN (translation wave 4, 2026-10-26): every
	/// code but English and German - a French, Russian, Japanese or Chinese pack starts without code.
	/// Its rules are a small HEADER of ids in the language's own Languages/&lt;code&gt;.tsv (UWText, read
	/// live, so the switch of language takes them at once); the English values are the defaults and
	/// leave a name as bare as English leaves a name without article:
	///
	///   grammar.rule.genders       the gender letters the names use ("m f", "m f n"; none)
	///   grammar.rule.cases         the case keys the names may name forms for ("nom gen dat acc ins loc")
	///   grammar.rule.case.subject  the case key of a subject (nom)
	///   grammar.rule.case.object   the case key where the code asks for an object - "You read {0}" (acc)
	///   grammar.rule.case.place    the case key after "on" - "the trap on {1}" (dat)
	///   grammar.rule.case.look     the case key of the thing in the look sentence (acc)
	///   grammar.rule.plural        a code whose CLDR plural rule to take (auto: the language's)
	///   grammar.rule.capital       yes: a message line starts with a capital (no)
	///   grammar.rule.letters       the letters beyond ASCII the text entries take (none)
	///   grammar.rule.alphabet      the letter grid's letters in order, upper case (A-Z; translation 5.1,
	///                              read for every language - UWGrammarEnglish.Alphabet)
	///   grammar.rule.alphabet.lower  their lower case, letter for letter (auto: char.ToLowerInvariant)
	///   grammar.rule.font          the fonts that draw what the port's fonts lack (none; translation wave 6,
	///                              read for every language - UWLanguageFont); with one the letters beyond
	///                              the classic fonts are typeable and drawn
	///   grammar.rule.ime           yes: the text entries use the system's input method (no; UWLanguageFont)
	///   grammar.article.indefinite the indefinite article around the noun phrase {0} ("{0}": none;
	///                              "{0,select,f{une {0}}other{un {0}}}")
	///   grammar.article.definite   the definite one, the same way ("{0,select,f{la {0}}other{le {0}}}")
	///   grammar.article.your       the possessive of the player's own thing ("{0}")
	///   grammar.phrase.words       a word before or after the noun: {0} the word, {1} the noun phrase
	///                              ("{0} {1}"; "{1} {0}" puts it after; "{0}{1}" without a blank)
	///   grammar.phrase.count       a count: {0} the number, {1} the phrase in the count's plural ("{0} {1}")
	/// The {0} of the article and word frames is the name (UWNamePhrase: gender, case, count), so they
	/// may select on its gender and case and count by plural. The WORDS (the condition words of block 5,
	/// the attitude words, grammar.word.magical and .cursed) are fragments with {0} the name: a word
	/// may agree ("{0,select,f{usée}other{usé}}"). The NAMES are in UWNameFormat (gender prefix, named
	/// cases, per-category plurals); a name without the prefix reads by the English rules, as in German.
	/// The look's frames (grammar.look.*) and the messages' (msg.*) are the language's as for every
	/// language. English and German keep their classes and do not read the header.
	/// </summary>
	public sealed class UWGrammarGeneric : UWGrammarEnglish
	{
		private readonly string msCode;

		public UWGrammarGeneric(string psCode)
		{
			msCode = psCode;
		}

		public override string Code => msCode;

		/// <summary>The value "none" of a header list.</summary>
		public const string NoneValue = "none";

		// ------------------------------------------------- The header

		private static string fRule(string psId)
		{
			return UWText.T(psId).Trim();
		}

		/// <summary>The header's gender letters, empty for none.</summary>
		public static string Genders
		{
			get
			{
				StringBuilder lOLetters = new StringBuilder();

				foreach (char lcChar in fRule("grammar.rule.genders").ToLowerInvariant())
				{
					if (UWNameFormat.GenderLetters.IndexOf(lcChar) >= 0 && lOLetters.ToString().IndexOf(lcChar) < 0)
						lOLetters.Append(lcChar);
				}

				return lOLetters.ToString();
			}
		}

		/// <summary>The case keys the names may name forms for: the header's list and the four roles.</summary>
		public static string[] Cases
		{
			get
			{
				List<string> lsKeys = new List<string>();

				foreach (string lsKey in (fRule("grammar.rule.cases") + " " + fRule("grammar.rule.case.subject") + " " + fRule("grammar.rule.case.object")
					+ " " + fRule("grammar.rule.case.place") + " " + fRule("grammar.rule.case.look")).Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
				{
					if (lsKey != NoneValue && !lsKeys.Contains(lsKey))
						lsKeys.Add(lsKey);
				}

				return lsKeys.ToArray();
			}
		}

		private static string fCaseOf(string psId)
		{
			string lsKey = fRule(psId);

			return lsKey.Length == 0 ? UWNameFormat.Nominative : lsKey;
		}

		private static string LookCase => fCaseOf("grammar.rule.case.look");

		private static string SubjectCase => fCaseOf("grammar.rule.case.subject");

		public override string CaseKey(UWGrammarCase peCase)
		{
			switch (peCase)
			{
				case UWGrammarCase.Accusative:
					return fCaseOf("grammar.rule.case.object");
				case UWGrammarCase.Dative:
					return fCaseOf("grammar.rule.case.place");
				default:
					return SubjectCase;
			}
		}

		/// <summary>The letters beyond ASCII of the header that the classic fonts draw.</summary>
		public override string TypeableLetters
		{
			get
			{
				StringBuilder lOLetters = new StringBuilder();
				string lsLetters = fRule("grammar.rule.letters");

				if (lsLetters == NoneValue)
					return string.Empty;

				// With a font of its own (UWLanguageFont, translation wave 6) the language draws them all.
				bool lbOwnFont = UWLanguageFont.HasFont;

				foreach (char lcChar in lsLetters)
				{
					if (lcChar > '~' && (lbOwnFont || Array.IndexOf(UWFontGlyphBuilder.Characters, lcChar) >= 0) && lOLetters.ToString().IndexOf(lcChar) < 0)
						lOLetters.Append(lcChar);
				}

				return lOLetters.ToString();
			}
		}

		public override bool NamesHaveGender => Genders.Length > 0;

		private string PluralCode => UWText.PluralCode;

		/// <summary>The header's ids, for the checks.</summary>
		public static readonly string[] HeaderIds =
		{
			"grammar.rule.genders", "grammar.rule.cases", "grammar.rule.case.subject", "grammar.rule.case.object", "grammar.rule.case.place",
			"grammar.rule.case.look", "grammar.rule.plural", "grammar.rule.capital", "grammar.rule.letters", "grammar.rule.alphabet",
			"grammar.rule.alphabet.lower", UWLanguageFont.FontRuleId, UWLanguageFont.ImeRuleId, "grammar.article.indefinite",
			"grammar.article.definite", "grammar.article.your", "grammar.phrase.words", "grammar.phrase.count"
		};

		/// <summary>What is wrong with a header (pText gives an id's text in the language, or null for
		/// one it leaves English): genders that are no letters of the format, a case key the format does
		/// not know, capital neither yes nor no, letters the classic fonts cannot draw (a warning-worthy
		/// note: they are left out), a plural rule that names no code; select and plural of the frames
		/// are the language file's own check. Empty when fine.</summary>
		public static List<string> CheckHeader(Func<string, string> pText)
		{
			List<string> lsFound = new List<string>();
			Func<string, string> lOText = psId => (pText(psId) ?? UWText.English(psId) ?? string.Empty).Trim();

			foreach (string lsPart in lOText("grammar.rule.genders").Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
			{
				if (lsPart != NoneValue && (lsPart.Length != 1 || UWNameFormat.GenderLetters.IndexOf(char.ToLowerInvariant(lsPart[0])) < 0))
					lsFound.Add("grammar.rule.genders: " + lsPart + " is no gender letter (m f n c p)");
			}

			foreach (string lsId in new[] { "grammar.rule.cases", "grammar.rule.case.subject", "grammar.rule.case.object", "grammar.rule.case.place", "grammar.rule.case.look" })
			{
				foreach (string lsKey in lOText(lsId).Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
				{
					if (lsKey != NoneValue && Array.IndexOf(UWNameFormat.CaseKeys, lsKey) < 0)
						lsFound.Add(lsId + ": " + lsKey + " is no case key (" + string.Join(" ", UWNameFormat.CaseKeys) + ")");
				}
			}

			string lsCapital = lOText("grammar.rule.capital");

			if (lsCapital != "yes" && lsCapital != "no")
				lsFound.Add("grammar.rule.capital: " + lsCapital + " is neither yes nor no");

			string lsPlural = lOText("grammar.rule.plural");

			if (lsPlural != UWText.AutoRule && (lsPlural.Length < 2 || lsPlural.IndexOf(' ') >= 0))
				lsFound.Add("grammar.rule.plural: " + lsPlural + " is no language code (or auto)");

			// A language with a font of its own (grammar.rule.font, translation wave 6) draws the letters the
			// classic fonts lack from it: no letter is left out, no font is missing.
			bool lbOwnFont = UWLanguageFont.Parse(lOText(UWLanguageFont.FontRuleId), null).Count > 0;

			lsFound.AddRange(UWLanguageFont.CheckHeader(pText));

			string lsLetters = lOText("grammar.rule.letters");

			foreach (char lcChar in lsLetters == NoneValue || lbOwnFont ? string.Empty : lsLetters)
			{
				if (lcChar > '~' && Array.IndexOf(UWFontGlyphBuilder.Characters, lcChar) < 0)
					lsFound.Add("grammar.rule.letters: the classic fonts cannot draw " + lcChar + " (U+" + ((int)lcChar).ToString("X4") + ") - left out");
			}

			// THE ALPHABET (translation 5.1): letters only, the lower case letter for letter; letters the
			// classic fonts cannot draw are one note - such a language needs a font of its own.
			string lsAlphabet = lOText("grammar.rule.alphabet");
			string lsUpper = ParseAlphabet(lsAlphabet);

			foreach (char lcChar in lsAlphabet == NoneValue || lsAlphabet == AutoValue ? string.Empty : lsAlphabet)
			{
				if (!char.IsWhiteSpace(lcChar) && !char.IsLetter(lcChar))
					lsFound.Add("grammar.rule.alphabet: " + lcChar + " is no letter - left out");
			}

			string lsLowerRule = lOText("grammar.rule.alphabet.lower");
			int liLower = 0;

			foreach (char lcChar in lsLowerRule == AutoValue ? string.Empty : lsLowerRule)
			{
				if (char.IsLetter(lcChar))
					liLower++;
			}

			if (liLower > 0 && liLower != lsUpper.Length)
				lsFound.Add("grammar.rule.alphabet.lower: " + liLower + " letters for " + lsUpper.Length + " - derived from the upper case instead");

			List<char> lOUndrawn = new List<char>();

			foreach (char lcChar in lsUpper + ParseAlphabetLower(lsLowerRule, lsUpper))
			{
				if (lcChar > '~' && Array.IndexOf(UWFontGlyphBuilder.Characters, lcChar) < 0 && !lOUndrawn.Contains(lcChar))
					lOUndrawn.Add(lcChar);
			}

			if (lOUndrawn.Count > 0 && !lbOwnFont)
				lsFound.Add("grammar.rule.alphabet: the classic fonts cannot draw " + lOUndrawn.Count + " of its letters (" + new string(lOUndrawn.ToArray())
					+ ") - the language needs a font of its own");

			return lsFound;
		}

		// ------------------------------------------------- The name format

		private static UWNameFormat fParse(string psRaw)
		{
			return UWNameFormat.Parse(psRaw);
		}

		public override string CheckName(string psText)
		{
			UWNameFormat lOName = fParse(psText);

			if (lOName == null)
				return NamesHaveGender ? "no gender prefix (" + string.Join(":, ", Genders.ToCharArray()) + ": as in m:name&names) - the name reads by the English rules" : null;

			return lOName.Check(NamesHaveGender ? Genders : UWNameFormat.GenderLetters, Cases, PluralCode);
		}

		public override string GenderOf(string psRaw)
		{
			UWNameFormat lOName = fParse(psRaw);

			return lOName == null ? base.GenderOf(psRaw) : lOName.Gender;
		}

		/// <summary>The noun in a case: the singular's form (a plural-only name its plural's).</summary>
		private static string fNounIn(UWNameFormat pOName, string psCase)
		{
			return pOName.IsPluralOnly ? pOName.PluralIn(psCase) : pOName.SingularIn(psCase);
		}

		/// <summary>The words joined to the noun phrase, the last word nearest (grammar.phrase.words),
		/// each word a fragment with {0} the name so far.</summary>
		private string fWithWords(UWNameFormat pOName, string psNoun, List<string> psWords, string psCase, int piCount)
		{
			string lsPhrase = psNoun;

			for (int liAt = psWords.Count - 1; liAt >= 0; liAt--)
			{
				string lsWord = fWord(psWords[liAt], new UWNamePhrase(lsPhrase, pOName.Gender, psCase, piCount));

				lsPhrase = UWText.F("grammar.phrase.words", lsWord, new UWNamePhrase(lsPhrase, pOName.Gender, psCase, piCount));
			}

			return lsPhrase;
		}

		/// <summary>A word (block 5's, grammar.word.*) as a fragment with {0} the name; as it stands
		/// when it does not read.</summary>
		private string fWord(string psWord, UWNamePhrase pOName)
		{
			if (psWord == null || psWord.IndexOf('{') < 0)
				return psWord;

			try
			{
				return UWMessageFormat.Format(psWord, new object[] { pOName }, PluralCode);
			}
			catch (FormatException)
			{
				return psWord;
			}
		}

		/// <summary>The phrase with the name's own article (i: the indefinite frame, d: the definite one,
		/// o: none).</summary>
		private static string fArticle(UWNameFormat pOName, string psPhrase, string psCase, int piCount)
		{
			UWNamePhrase lOPhrase = new UWNamePhrase(psPhrase, pOName.Gender, psCase, piCount);

			if (pOName.Article == UWNameFormat.Definite)
				return UWText.F("grammar.article.definite", lOPhrase);

			if (pOName.Article == UWNameFormat.NoArticle)
				return psPhrase;

			return UWText.F("grammar.article.indefinite", lOPhrase);
		}

		/// <summary>A count, the words and the plural of the count's category (grammar.phrase.count).</summary>
		private string fCounted(UWNameFormat pOName, int piCount, List<string> psWords, string psCase)
		{
			string lsNoun = pOName.PluralFor(UWPluralRules.Category(PluralCode, piCount));
			string lsPhrase = fWithWords(pOName, lsNoun, psWords, psCase, piCount);

			return UWText.F("grammar.phrase.count", piCount, new UWNamePhrase(lsPhrase, pOName.Gender, psCase, piCount));
		}

		/// <summary>One thing with its words and its article, or a count.</summary>
		private string fPhrase(UWNameFormat pOName, int piCount, List<string> psWords, string psCase, bool pbArticle)
		{
			if (piCount > 1)
				return fCounted(pOName, piCount, psWords, psCase);

			string lsPhrase = fWithWords(pOName, fNounIn(pOName, psCase), psWords, psCase, 1);

			return pbArticle ? fArticle(pOName, lsPhrase, psCase, 1) : lsPhrase;
		}

		private static List<string> fWords(params string[] psWords)
		{
			List<string> lsWords = new List<string>();

			foreach (string lsWord in psWords)
			{
				if (!string.IsNullOrEmpty(lsWord))
					lsWords.Add(lsWord);
			}

			return lsWords;
		}

		// ------------------------------------------------- Names

		public override string Name(string psRaw, bool pbPlural, bool pbArticle, UWGrammarCase peCase)
		{
			UWNameFormat lOName = fParse(psRaw);

			if (lOName == null)
				return base.Name(psRaw, pbPlural, pbArticle, peCase);

			string lsCase = CaseKey(peCase);

			if (!pbPlural)
				return pbArticle ? fArticle(lOName, fNounIn(lOName, lsCase), lsCase, 1) : fNounIn(lOName, lsCase);

			string lsPlural = lOName.PluralIn(lsCase);

			// The indefinite plural has no article; the definite one has.
			return pbArticle && lOName.Article == UWNameFormat.Definite
				? UWText.F("grammar.article.definite", new UWNamePhrase(lsPlural, lOName.Gender, lsCase, -1))
				: lsPlural;
		}

		public override string AddedPlural(string psRaw)
		{
			UWNameFormat lOName = fParse(psRaw);

			return lOName == null ? base.AddedPlural(psRaw) : lOName.Plural ?? lOName.Singular;
		}

		public override string PersonName(string psRaw)
		{
			UWNameFormat lOName = fParse(psRaw);

			return lOName == null ? psRaw : lOName.Singular;
		}

		// ------------------------------------------------- The look

		public override string ItemPhrase(string psRaw, int piCount, string psCondition, bool pbCursed, bool pbMagical)
		{
			UWNameFormat lOName = fParse(psRaw);

			if (lOName == null)
				return base.ItemPhrase(psRaw, piCount, psCondition, pbCursed, pbMagical);

			// The original's order of the words: condition, cursed, magical (magical nearest the noun).
			return fPhrase(lOName, piCount, fWords(psCondition, pbCursed ? CursedWord : null, pbMagical ? MagicalWord : null), LookCase, true);
		}

		public override string ScenePhrase(string psRaw, string psCondition)
		{
			UWNameFormat lOName = fParse(psRaw);

			return lOName == null ? base.ScenePhrase(psRaw, psCondition) : fPhrase(lOName, 1, fWords(psCondition), LookCase, true);
		}

		public override string ChunkPhrase(string psText)
		{
			UWNameFormat lOName = fParse(psText);

			return lOName == null ? base.ChunkPhrase(psText) : fPhrase(lOName, 1, fWords(), LookCase, true);
		}

		public override string LiteralPhrase(string psText)
		{
			UWNameFormat lOName = fParse(psText);

			return lOName == null ? base.LiteralPhrase(psText) : fPhrase(lOName, 1, fWords(), LookCase, true);
		}

		/// <summary>As German: a kind or name left English reads by the English rules; a block 7 name with
		/// the prefix replaces the kind, one without follows it (grammar.look.named); the attitude word
		/// shows only with a kind.</summary>
		public override string CreatureLook(string psAttitude, string psKind, string psName)
		{
			if (psKind == null && psName == null)
				return null;

			UWNameFormat lOKind = psKind != null ? fParse(psKind) : null;
			UWNameFormat lOName = psName != null ? fParse(psName) : null;

			if ((psKind != null && lOKind == null) || (psName != null && lOName == null && char.IsLower(psName[0])))
				return base.CreatureLook(psAttitude, psKind, psName);

			List<string> lsWords = fWords(psKind != null ? psAttitude : null);
			string lsPhrase;

			if (lOName != null)
				lsPhrase = fPhrase(lOName, 1, lsWords, LookCase, true);
			else if (psName != null)
				lsPhrase = lOKind != null
					? UWText.F("grammar.look.named", fPhrase(lOKind, 1, lsWords, LookCase, true), psName.Replace('_', ' '))
					: psName.Replace('_', ' ');
			else
				lsPhrase = fPhrase(lOKind, 1, lsWords, LookCase, true);

			return LookSentence(lsPhrase);
		}

		public override string IdentifyName(string psRaw, int piCount, bool pbMagical, bool pbCursed, bool pbArticle)
		{
			UWNameFormat lOName = fParse(psRaw);

			if (lOName == null)
				return base.IdentifyName(psRaw, piCount, pbMagical, pbCursed, pbArticle);

			return fPhrase(lOName, piCount, fWords(pbMagical ? MagicalWord : pbCursed ? CursedWord : null), SubjectCase, pbArticle);
		}

		// ------------------------------------------------- The message lines

		public override string TheName(string psRaw, UWGrammarCase peCase)
		{
			UWNameFormat lOName = fParse(psRaw);

			if (lOName == null)
				return base.TheName(psRaw, peCase);

			string lsCase = CaseKey(peCase);

			return UWText.F("grammar.article.definite", new UWNamePhrase(fNounIn(lOName, lsCase), lOName.Gender, lsCase, lOName.IsPluralOnly ? -1 : 1));
		}

		public override string YourName(string psRaw, UWGrammarCase peCase)
		{
			UWNameFormat lOName = fParse(psRaw);

			if (lOName == null)
				return base.YourName(psRaw, peCase);

			string lsCase = CaseKey(peCase);

			return UWText.F("grammar.article.your", new UWNamePhrase(fNounIn(lOName, lsCase), lOName.Gender, lsCase, lOName.IsPluralOnly ? -1 : 1));
		}

		public override bool TakesPluralVerb(string psRaw)
		{
			UWNameFormat lOName = fParse(psRaw);

			return lOName == null ? base.TakesPluralVerb(psRaw) : lOName.IsPluralOnly;
		}

		/// <summary>Two or more blanks in a row become one (the level-up's number, as in German); a
		/// capital first letter where the header says so.</summary>
		public override string Sentence(string psText)
		{
			string lsText = psText;

			if (lsText != null)
			{
				while (lsText.IndexOf("  ", StringComparison.Ordinal) >= 0)
					lsText = lsText.Replace("  ", " ");
			}

			return fRule("grammar.rule.capital") == "yes" ? UWText.Capitalised(lsText) : lsText;
		}

		public override string ItemLabel(string psRaw, int piCount, bool pbCursed)
		{
			UWNameFormat lOName = fParse(psRaw);

			if (lOName == null)
				return base.ItemLabel(psRaw, piCount, pbCursed);

			return UWText.Capitalised(fPhrase(lOName, piCount, fWords(pbCursed ? CursedWord : null), SubjectCase, false));
		}

		public override string LiteralLabel(string psText)
		{
			UWNameFormat lOName = fParse(psText);

			return lOName == null ? base.LiteralLabel(psText) : UWText.Capitalised(fPhrase(lOName, 1, fWords(), SubjectCase, true));
		}
	}
}
