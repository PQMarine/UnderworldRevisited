namespace UWDataImport.UWData
{
	public static partial class UWTextEnglish
	{
		/// <summary>
		/// The grammar's sentence frames and the words the original keeps in UW.EXE (translation wave 2,
		/// 2026-10-26; IUWGrammar): the look sentence, the owner's and the enchantment's parts, the
		/// charges, "named", "magical" and "cursed", the bones' adventurer. The English is what the code
		/// wrote until then, blanks included; a language gives its own in Languages/&lt;code&gt;.tsv (the
		/// German grammar inflects the two words: write them uninflected, "magisch", "verflucht").
		/// </summary>
		private static void fGrammar()
		{
			fAdd("grammar.look.you_see", "You see {0}.");
			fAdd("grammar.look.named", "{0} named {1}");
			fAdd("grammar.look.owner", " belonging to{0}");
			fAdd("grammar.look.of", " of {0}");
			fAdd("grammar.look.charges.none", " with no full charges");
			fAdd("grammar.look.charges.one", " with 1 full charge");
			fAdd("grammar.look.charges.many", " with {0} full charges");

			fAdd("grammar.word.magical", "magical");
			fAdd("grammar.word.cursed", "cursed");

			fAdd("grammar.bones.adventurer", "an adventurer");

			// THE PLURAL RULE (translation wave 4): the language code whose CLDR cardinal rule the
			// frames' {n,plural,...} and a name's per-category plurals take (UWPluralRules); empty: the
			// language's own code (auto).
			fAdd("grammar.rule.plural", "auto");

			// THE HEADER OF A LANGUAGE WITHOUT A GRAMMAR CLASS OF ITS OWN (translation wave 4,
			// UWGrammarGeneric, which describes each): every code but English and German. The English
			// values are the defaults - no genders, no articles, the words before the noun. English and
			// German do not read them.
			fAdd("grammar.rule.genders", "none");
			fAdd("grammar.rule.cases", "nom acc dat");
			fAdd("grammar.rule.case.subject", "nom");
			fAdd("grammar.rule.case.object", "acc");
			fAdd("grammar.rule.case.place", "dat");
			fAdd("grammar.rule.case.look", "acc");
			fAdd("grammar.rule.capital", "no");
			fAdd("grammar.rule.letters", "none");

			// THE ALPHABET (translation 5.1, 2026-10-26; IUWGrammar.Alphabet): the gamepad letter grid's
			// letter keys in order, upper case, and their lower case ("auto": char.ToLowerInvariant) -
			// read for every language, English and German too; the typed fields take its letters.
			fAdd("grammar.rule.alphabet", UWGrammarEnglish.LatinAlphabet);
			fAdd("grammar.rule.alphabet.lower", UWGrammarEnglish.AutoValue);

			// THE LANGUAGE'S FONT AND INPUT METHOD (translation wave 6, 2026-10-26; UWLanguageFont): the
			// fonts that draw what the port's own fonts lack (a file in Languages, os:<family> for an
			// installed one, a list in order; none) and whether the text entries use the system's input
			// method (yes/no) - read for every language.
			fAdd("grammar.rule.font", UWGrammarGeneric.NoneValue);
			fAdd("grammar.rule.ime", "no");

			// {0} the noun phrase (its gender, case and count for select and plural).
			fAdd("grammar.article.indefinite", "{0}");
			fAdd("grammar.article.definite", "{0}");
			fAdd("grammar.article.your", "{0}");

			// {0} a word (a condition, attitude, "magical", "cursed"), {1} the noun phrase.
			fAdd("grammar.phrase.words", "{0} {1}");

			// {0} the count, {1} the noun phrase in the count's plural.
			fAdd("grammar.phrase.count", "{0} {1}");
		}
	}
}
