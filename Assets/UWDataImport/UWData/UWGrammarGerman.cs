using System.Collections.Generic;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE GERMAN GRAMMAR (translation wave 2, 2026-10-26): articles by gender and case, the words
	/// before a name inflected as adjectives, counts with the plural - from a name format the German
	/// game-text pack writes into block 4 (and wherever else a name is built from: the talismans of
	/// block 1, the names of block 7, the walls and floors of block 10).
	///
	/// THE NAME FORMAT:   &lt;gender&gt;[&lt;article&gt;]:&lt;singular&gt;[|&lt;oblique&gt;][&amp;&lt;plural&gt;]
	///   gender   m (der), f (die), n (das), p (a plural only, die: "p:Stiefel")
	///   article  none: the indefinite one ("einen Dolch"); d the definite one ("md:Ring der Demut":
	///            "den Ring der Demut"); o none at all ("fo:Säure": "Säure")
	///   oblique  the singular in the accusative and dative where it differs, a weak masculine
	///            ("m:Drache|Drachen&amp;Drachen": "einen Drachen", "ein Drache")
	///   plural   after "&amp;" ("m:Dolch&amp;Dolche"); without one the singular stands for it
	///   (since translation wave 4 read by UWNameFormat, which adds named forms German may use: a case
	///   "|dat=..." before the oblique form, a plural in a case "&amp;dat=..."; the gender c is not German)
	/// Examples: "m:Dolch&amp;Dolche", "f:Fackel&amp;Fackeln", "n:Seil&amp;Seile". The prefix's letters may be
	/// capitals; a "_" in the text reads as a blank. A NAME WITHOUT THE PREFIX is read by the English
	/// rules ("a_torch&amp;torches"), so an entry the pack leaves English reads as English inside the German
	/// frame; `uwdump validate-tsv` warns about a block 4 name without one.
	///
	/// THE WORDS BEFORE A NAME - the condition word of block 5, "verflucht", "magisch" (the frames'
	/// grammar.word.*), the creature's attitude word - are written uninflected ("etwas abgenutzt"); the
	/// phrase's last word takes the ending of the case, gender and article: "einen etwas abgenutzten
	/// Dolch", "eine stark beschädigte Fackel", "3 abgenutzte Dolche" (a count is no article: the strong
	/// ending). A word ending in e takes the ending without its own e ("müde": "einen müden").
	///
	/// THE CASES: the look sentence names its thing in the accusative ("Du siehst einen Dolch." - the
	/// frame grammar.look.you_see in de.tsv), identify_inv's name for a conversation in the nominative,
	/// the bones' "that of" in the dative ("von einem Goblin"). A creature's block 7 name WITH the
	/// prefix replaces its kind (the English rule for a name in lower case: "einen feindseligen
	/// Banditenhauptmann"); a name without it follows the kind ("einen gelassenen Ausgestoßenen namens
	/// Bragit", the frame grammar.look.named). The owner is block 1's text after the frame
	/// grammar.look.owner as it stands (", Eigentum eines grünen Goblins": the genitive is the
	/// translator's).
	///
	/// THE MESSAGE LINES (translation step 3A, 2026-10-26; the frames msg.* in de.tsv): a known thing
	/// with the definite article in the frame's case ("Die Truhe ist verschlossen.", "Du liest die
	/// Schriftrolle...", "... an dem Dolch"), the player's own with "dein" ("Dein Dolch wurde
	/// beschädigt."; a plural-only name takes the plural frame, "Deine Stiefel wurden ..."), every line
	/// starting with a capital so a frame may begin with its name; the bags' label in the nominative
	/// without article ("Verfluchter Dolch", "3 Dolche").
	/// </summary>
	public sealed class UWGrammarGerman : UWGrammarEnglish
	{
		public const string LanguageCode = "de";

		public override string Code => LanguageCode;

		/// <summary>a-, o-, u-umlaut, their capitals and the sharp s: the letters the classic fonts
		/// draw beyond ASCII (UWFontGlyphBuilder.Characters).</summary>
		public override string TypeableLetters => "äöüÄÖÜß";

		public override bool NamesHaveGender => true;

		/// <summary>The case the look sentence names its thing in.</summary>
		private const UWGrammarCase LookCase = UWGrammarCase.Accusative;

		// ------------------------------------------------- The name format

		private const int Masculine = 0;

		private const int Feminine = 1;

		private const int Neuter = 2;

		private const int PluralOnly = 3;

		private const char Indefinite = 'i';

		private const char Definite = 'd';

		private const char NoArticle = 'o';

		/// <summary>The gender letters German knows, in the order of the tables' columns.</summary>
		private const string GermanGenders = "mfnp";

		/// <summary>The case keys the German grammar asks a name for (UWNameFormat.CaseKeyOf).</summary>
		private static readonly string[] msCaseKeys = { "nom", "acc", "dat" };

		/// <summary>A name in the format, parsed (UWNameFormat since translation wave 4; the German
		/// reads only its genders m f n p).</summary>
		private sealed class Noun
		{
			public int Gender;

			public char Article;

			public string Singular;

			/// <summary>The whole name as the shared format read it: the named case and plural forms.</summary>
			public UWNameFormat Format;

			/// <summary>The singular in a case: a form named for it ("|dat=..."), else outside the
			/// nominative the oblique one ("m:Drache|Drachen"), else the singular.</summary>
			public string In(UWGrammarCase peCase)
			{
				return Format.SingularIn(UWNameFormat.CaseKeyOf(peCase));
			}

			/// <summary>The plural without a count, in a case where one is named ("&amp;dat=...").</summary>
			public string PluralIn(UWGrammarCase peCase)
			{
				return Format.PluralIn(UWNameFormat.CaseKeyOf(peCase));
			}
		}

		/// <summary>The name in the format, or null for a text without the prefix.</summary>
		private static Noun fParse(string psRaw)
		{
			UWNameFormat lOFormat = UWNameFormat.Parse(psRaw, GermanGenders);

			if (lOFormat == null)
				return null;

			return new Noun
			{
				Gender = GermanGenders.IndexOf(lOFormat.Gender[0]),
				Article = lOFormat.Article,
				Singular = lOFormat.Singular,
				Format = lOFormat
			};
		}

		public override string CheckName(string psText)
		{
			Noun lONoun = fParse(psText);

			if (lONoun == null)
				return "no gender prefix (m:, f:, n: or p:, as in m:Dolch&Dolche) - the name reads by the English rules";

			if (lONoun.Singular.Length == 0)
				return "no name after the prefix";

			if (psText.IndexOf('&') >= 0 && lONoun.Format.Plural == null && lONoun.Format.PluralForms.Count == 0)
				return "an empty plural after &";

			return lONoun.Format.Check(GermanGenders, msCaseKeys, LanguageCode);
		}

		/// <summary>The gender letter of a name in the format (m f n p), null for one without.</summary>
		public override string GenderOf(string psRaw)
		{
			Noun lONoun = fParse(psRaw);

			return lONoun == null ? base.GenderOf(psRaw) : lONoun.Format.Gender;
		}

		// ------------------------------------------------- Articles and endings, [case][gender]

		private static readonly string[][] msIndefinite =
		{
			new[] { "ein", "eine", "ein", "" },
			new[] { "einen", "eine", "ein", "" },
			new[] { "einem", "einer", "einem", "" }
		};

		private static readonly string[][] msDefinite =
		{
			new[] { "der", "die", "das", "die" },
			new[] { "den", "die", "das", "die" },
			new[] { "dem", "der", "dem", "den" }
		};

		/// <summary>After the indefinite article (the plural has none and takes the strong ending).</summary>
		private static readonly string[][] msMixed =
		{
			new[] { "er", "e", "es", "e" },
			new[] { "en", "e", "es", "e" },
			new[] { "en", "en", "en", "en" }
		};

		/// <summary>After the definite article.</summary>
		private static readonly string[][] msWeak =
		{
			new[] { "e", "e", "e", "en" },
			new[] { "en", "e", "e", "en" },
			new[] { "en", "en", "en", "en" }
		};

		/// <summary>Without an article, and after a count.</summary>
		private static readonly string[][] msStrong =
		{
			new[] { "er", "e", "es", "e" },
			new[] { "en", "e", "es", "e" },
			new[] { "em", "er", "em", "en" }
		};

		/// <summary>An uninflected word or phrase with the ending on its last word: "etwas abgenutzt" and
		/// "en" give "etwas abgenutzten", "müde" and "en" give "müden". A phrase that does not end in a
		/// letter stays as it is.</summary>
		public static string Inflect(string psWords, string psEnding)
		{
			if (string.IsNullOrEmpty(psWords) || string.IsNullOrEmpty(psEnding))
				return psWords;

			char lcLast = psWords[psWords.Length - 1];

			if (!char.IsLetter(lcLast))
				return psWords;

			return lcLast == 'e' && psEnding[0] == 'e' ? psWords + psEnding.Substring(1) : psWords + psEnding;
		}

		/// <summary>The words inflected alike, each with the ending, joined by blanks.</summary>
		private static string fAdjectives(List<string> pOWords, string psEnding)
		{
			StringBuilder lOText = new StringBuilder();

			foreach (string lsWord in pOWords)
			{
				if (string.IsNullOrEmpty(lsWord))
					continue;

				if (lOText.Length > 0)
					lOText.Append(' ');

				lOText.Append(Inflect(lsWord.Trim(), psEnding));
			}

			return lOText.ToString();
		}

		private static string fJoin(params string[] psParts)
		{
			StringBuilder lOText = new StringBuilder();

			foreach (string lsPart in psParts)
			{
				if (string.IsNullOrEmpty(lsPart))
					continue;

				if (lOText.Length > 0)
					lOText.Append(' ');

				lOText.Append(lsPart);
			}

			return lOText.ToString();
		}

		/// <summary>The noun as a phrase in a case: the article (when wanted and the name has one), the
		/// words inflected for it, the singular or its oblique form.</summary>
		private static string fNounPhrase(Noun pONoun, List<string> pOWords, UWGrammarCase peCase, bool pbArticle)
		{
			int liCase = (int)peCase;
			int liGender = pONoun.Gender;
			string lsArticle = string.Empty;
			string[][] lsEndings = msStrong;

			if (pbArticle && pONoun.Article == Definite)
			{
				lsArticle = msDefinite[liCase][liGender];
				lsEndings = msWeak;
			}
			else if (pbArticle && pONoun.Article == Indefinite && liGender != PluralOnly)
			{
				lsArticle = msIndefinite[liCase][liGender];
				lsEndings = msMixed;
			}

			string lsNoun = liGender == PluralOnly ? pONoun.PluralIn(peCase) : pONoun.In(peCase);

			return fJoin(lsArticle, pOWords != null ? fAdjectives(pOWords, lsEndings[liCase][liGender]) : null, lsNoun);
		}

		/// <summary>A count and the plural, the words with the strong plural ending: "3 abgenutzte Dolche"
		/// (the plural of the count's category, UWNameFormat.PluralFor - in German every count above
		/// one is "other", the plain plural).</summary>
		private static string fCountPhrase(Noun pONoun, int piCount, List<string> pOWords, UWGrammarCase peCase)
		{
			return fJoin(piCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
				fAdjectives(pOWords, msStrong[(int)peCase][PluralOnly]), pONoun.Format.PluralFor(UWPluralRules.Category(LanguageCode, piCount)));
		}

		private static List<string> fWords(params string[] psWords)
		{
			List<string> lOWords = new List<string>();

			foreach (string lsWord in psWords)
			{
				if (!string.IsNullOrEmpty(lsWord))
					lOWords.Add(lsWord);
			}

			return lOWords;
		}

		// ------------------------------------------------- Names

		public override string Name(string psRaw, bool pbPlural, bool pbArticle, UWGrammarCase peCase)
		{
			Noun lONoun = fParse(psRaw);

			if (lONoun == null)
				return base.Name(psRaw, pbPlural, pbArticle, peCase);

			if (pbPlural)
			{
				string lsPlural = lONoun.PluralIn(peCase);

				// The indefinite plural has no article; the definite one has.
				return pbArticle && lONoun.Article == Definite ? fJoin(msDefinite[(int)peCase][PluralOnly], lsPlural) : lsPlural;
			}

			return fNounPhrase(lONoun, null, peCase, pbArticle);
		}

		public override string AddedPlural(string psRaw)
		{
			Noun lONoun = fParse(psRaw);

			return lONoun == null ? base.AddedPlural(psRaw) : lONoun.Format.Plural ?? lONoun.Singular;
		}

		public override string PersonName(string psRaw)
		{
			Noun lONoun = fParse(psRaw);

			return lONoun == null ? psRaw : lONoun.Singular;
		}

		// ------------------------------------------------- The look

		public override string ItemPhrase(string psRaw, int piCount, string psCondition, bool pbCursed, bool pbMagical)
		{
			Noun lONoun = fParse(psRaw);

			if (lONoun == null)
				return base.ItemPhrase(psRaw, piCount, psCondition, pbCursed, pbMagical);

			// The original's order of the words: condition, cursed, magical.
			List<string> lOWords = fWords(psCondition, pbCursed ? CursedWord : null, pbMagical ? MagicalWord : null);

			return piCount > 1 ? fCountPhrase(lONoun, piCount, lOWords, LookCase) : fNounPhrase(lONoun, lOWords, LookCase, true);
		}

		public override string ScenePhrase(string psRaw, string psCondition)
		{
			Noun lONoun = fParse(psRaw);

			return lONoun == null ? base.ScenePhrase(psRaw, psCondition) : fNounPhrase(lONoun, fWords(psCondition), LookCase, true);
		}

		public override string ChunkPhrase(string psText)
		{
			Noun lONoun = fParse(psText);

			return lONoun == null ? base.ChunkPhrase(psText) : fNounPhrase(lONoun, null, LookCase, true);
		}

		public override string LiteralPhrase(string psText)
		{
			Noun lONoun = fParse(psText);

			return lONoun == null ? base.LiteralPhrase(psText) : fNounPhrase(lONoun, null, LookCase, true);
		}

		/// <summary>The creature: a kind or name left English reads by the English rules; a block 7 name
		/// with the prefix replaces the kind, one without follows it with "namens".</summary>
		public override string CreatureLook(string psAttitude, string psKind, string psName)
		{
			if (psKind == null && psName == null)
				return null;

			Noun lOKind = psKind != null ? fParse(psKind) : null;
			Noun lOName = psName != null ? fParse(psName) : null;

			if ((psKind != null && lOKind == null) || (psName != null && lOName == null && char.IsLower(psName[0])))
				return base.CreatureLook(psAttitude, psKind, psName);

			// The attitude word shows only with a kind (CritterLook).
			List<string> lOWords = fWords(psKind != null ? psAttitude : null);
			string lsPhrase;

			if (lOName != null)
				lsPhrase = fNounPhrase(lOName, lOWords, LookCase, true);
			else if (psName != null)
				lsPhrase = lOKind != null
					? UWText.F("grammar.look.named", fNounPhrase(lOKind, lOWords, LookCase, true), psName.Replace('_', ' '))
					: psName.Replace('_', ' ');
			else
				lsPhrase = fNounPhrase(lOKind, lOWords, LookCase, true);

			return LookSentence(lsPhrase);
		}

		// ------------------------------------------------- The message lines (translation step 3A)

		/// <summary>The possessive "dein" by case and gender, [case][gender] (the plural: "deine").</summary>
		private static readonly string[][] msYour =
		{
			new[] { "dein", "deine", "dein", "deine" },
			new[] { "deinen", "deine", "dein", "deine" },
			new[] { "deinem", "deiner", "deinem", "deinen" }
		};

		/// <summary>The noun in a case with a determiner before it: the singular or its oblique form, a
		/// plural-only name's plural.</summary>
		private static string fNounIn(Noun pONoun, UWGrammarCase peCase)
		{
			return pONoun.Gender == PluralOnly ? pONoun.PluralIn(peCase) : pONoun.In(peCase);
		}

		/// <summary>"die Truhe", "den Dolch", "dem Dolch", "die Stiefel": the definite article whatever
		/// article the name takes in the look (a name written with o: has none there, but "die Säure"
		/// here); an adjective written into the name takes the weak ending ("der rote Trank", "an dem
		/// glänzenden Schwert" - fWeakAdjectives).</summary>
		public override string TheName(string psRaw, UWGrammarCase peCase)
		{
			Noun lONoun = fParse(psRaw);

			return lONoun == null
				? base.TheName(psRaw, peCase)
				: fJoin(msDefinite[(int)peCase][lONoun.Gender], fWeakAdjectives(fNounIn(lONoun, peCase), lONoun.Gender, peCase));
		}

		/// <summary>Words that may stand before a name's noun but are no adjective to inflect.</summary>
		private static readonly HashSet<string> msNoAdjective = new HashSet<string>
		{
			"ein", "eine", "einer", "einem", "einen", "eines", "der", "die", "das", "dem", "den", "des",
			"an", "auf", "aus", "in", "mit", "von", "zu", "und"
		};

		/// <summary>
		/// THE ADJECTIVES INSIDE A NAME AFTER THE DEFINITE ARTICLE (step 3D, 2026-10-26). The name format
		/// writes them as the indefinite article wants them ("m:roter Trank|roten Trank", "n:glänzendes
		/// Schwert", "f:zerbrochene Axt"), so after "der", "das" and "dem" they read "der roter Trank", "an
		/// dem glänzendes Schwert". The words before the noun - the name's first word with a capital - that
		/// are in lower case lose their strong ending (masculine -er, neuter -es, feminine and plural -e)
		/// and take the weak one of the case: "der rote Trank", "an dem glänzenden Schwert", "an der
		/// zerbrochenen Axt", "die roten Ranken". A word without such an ending stays ("leuchtend", "grob",
		/// the weak "verschlossenen" of a "pd:" name); a name with an article or a preposition before its
		/// noun ("an eine Ziegelwand gekettete Prinzessin") stays as written.
		/// </summary>
		private static string fWeakAdjectives(string psNoun, int piGender, UWGrammarCase peCase)
		{
			if (string.IsNullOrEmpty(psNoun) || psNoun.IndexOf(' ') < 0)
				return psNoun;

			string[] lsWords = psNoun.Split(' ');
			int liHead = 0;

			while (liHead < lsWords.Length && (lsWords[liHead].Length == 0 || !char.IsUpper(lsWords[liHead][0])))
				liHead++;

			if (liHead == 0 || liHead >= lsWords.Length)
				return psNoun;

			for (int liAt = 0; liAt < liHead; liAt++)
			{
				string lsWord = lsWords[liAt];

				if (lsWord.Length == 0 || !char.IsLetter(lsWord[lsWord.Length - 1]) || msNoAdjective.Contains(lsWord))
					return psNoun;
			}

			string lsStrong = piGender == Masculine ? "er" : piGender == Neuter ? "es" : "e";
			string lsWeak = msWeak[(int)peCase][piGender];

			for (int liAt = 0; liAt < liHead; liAt++)
			{
				string lsWord = lsWords[liAt];

				if (lsWord.Length > lsStrong.Length + 1 && lsWord.EndsWith(lsStrong, System.StringComparison.Ordinal))
					lsWords[liAt] = lsWord.Substring(0, lsWord.Length - lsStrong.Length) + lsWeak;
			}

			return string.Join(" ", lsWords);
		}

		/// <summary>"dein Dolch", "deine Fackel", "deinen Dolch", "deine Stiefel".</summary>
		public override string YourName(string psRaw, UWGrammarCase peCase)
		{
			Noun lONoun = fParse(psRaw);

			return lONoun == null ? base.YourName(psRaw, peCase) : fJoin(msYour[(int)peCase][lONoun.Gender], fNounIn(lONoun, peCase));
		}

		public override bool TakesPluralVerb(string psRaw)
		{
			Noun lONoun = fParse(psRaw);

			return lONoun == null ? base.TakesPluralVerb(psRaw) : lONoun.Gender == PluralOnly;
		}

		/// <summary>The first letter a capital (umlauts included): "{0} ist verschlossen." with "die
		/// Truhe" gives "Die Truhe ist verschlossen.". Two or more blanks in a row become one (step 3D):
		/// the level-up's number comes as UW.EXE writes it, a blank before a single digit, so a piece
		/// ending in a blank and words after the number ("Du hast Erfahrungsstufe " + " 5" + " erreicht.")
		/// read right below level 10 and from it on.</summary>
		public override string Sentence(string psText)
		{
			string lsText = psText;

			if (lsText != null)
			{
				while (lsText.IndexOf("  ", System.StringComparison.Ordinal) >= 0)
					lsText = lsText.Replace("  ", " ");
			}

			return UWText.Capitalised(lsText);
		}

		/// <summary>"Fackel", "3 Fackeln", "Verfluchter Dolch", "3 verfluchte Dolche": nominative without
		/// article, the word inflected strong, the first letter a capital.</summary>
		public override string ItemLabel(string psRaw, int piCount, bool pbCursed)
		{
			Noun lONoun = fParse(psRaw);

			if (lONoun == null)
				return base.ItemLabel(psRaw, piCount, pbCursed);

			List<string> lOWords = fWords(pbCursed ? CursedWord : null);

			return UWText.Capitalised(piCount > 1
				? fCountPhrase(lONoun, piCount, lOWords, UWGrammarCase.Nominative)
				: fNounPhrase(lONoun, lOWords, UWGrammarCase.Nominative, false));
		}

		/// <summary>A talisman's name in the format, in the nominative with its article ("Das Schwert
		/// der Gerechtigkeit"); one without the format as it stands.</summary>
		public override string LiteralLabel(string psText)
		{
			Noun lONoun = fParse(psText);

			return lONoun == null ? base.LiteralLabel(psText) : UWText.Capitalised(fNounPhrase(lONoun, null, UWGrammarCase.Nominative, true));
		}

		/// <summary>identify_inv's name in the nominative: "ein magisches Schwert", "magisches Schwert",
		/// "3 Dolche".</summary>
		public override string IdentifyName(string psRaw, int piCount, bool pbMagical, bool pbCursed, bool pbArticle)
		{
			Noun lONoun = fParse(psRaw);

			if (lONoun == null)
				return base.IdentifyName(psRaw, piCount, pbMagical, pbCursed, pbArticle);

			List<string> lOWords = fWords(pbMagical ? MagicalWord : pbCursed ? CursedWord : null);

			return piCount > 1
				? fCountPhrase(lONoun, piCount, lOWords, UWGrammarCase.Nominative)
				: fNounPhrase(lONoun, lOWords, UWGrammarCase.Nominative, pbArticle);
		}
	}
}
