using System;
using System.IO;

namespace UWDataImport.UWData
{
	/// <summary>The grammatical case a name is put in (German needs it; English ignores it).</summary>
	public enum UWGrammarCase
	{
		Nominative,
		Accusative,
		Dative
	}

	/// <summary>
	/// THE GRAMMAR OF A LANGUAGE (translation wave 2, layer 2 of the plan in Todo.md, "Translations";
	/// 2026-10-26): how the sentences the game assembles from parts are built - an object's name from
	/// its block 4 entry (article, plural, the words before it), the look sentence with its count,
	/// condition word, "magical" or "cursed", enchantment, charges and owner, the creature's look with
	/// its attitude and name, identify_inv's name for a conversation, a conversation partner's name.
	///
	/// THE WORDS come from the game-text pack (the names of block 4, the condition and attitude words of
	/// block 5, the owners of block 1, the names of block 7); THE SENTENCE FRAMES and the words the
	/// original keeps in UW.EXE ("You see {0}.", " belonging to{0}", "magical") are UWText ids of the
	/// port's own texts (UWTextEnglish.Grammar), so the language's Languages/&lt;code&gt;.tsv gives them.
	/// THE GRAMMAR is chosen by the game-text pack's language code (UWGameData.Grammar, UWGrammar.For):
	/// English without a pack, so the English stays exactly as it was (UWGrammarEnglish is the code
	/// that built the sentences until then, moved); German for a "de" pack (UWGrammarGerman, its name
	/// format described there); every other language the generic grammar (UWGrammarGeneric,
	/// translation wave 4) with the rules of a small header in its own Languages/&lt;code&gt;.tsv - by
	/// default the English rules without articles, names in UWNameFormat.
	/// </summary>
	public interface IUWGrammar
	{
		/// <summary>The language code the grammar is for ("en", "de").</summary>
		string Code { get; }

		/// <summary>The letters beyond printable ASCII the text entries take in this language - the
		/// ones the classic fonts can draw (UWFontGlyphBuilder). Empty for English.</summary>
		string TypeableLetters { get; }

		/// <summary>THE LANGUAGE'S ALPHABET (translation 5.1, 2026-10-26): the letter keys of the
		/// gamepad's letter grid in their order, upper case - A to Z for English and every language
		/// whose file does not name its own (grammar.rule.alphabet, read for every language).</summary>
		string Alphabet { get; }

		/// <summary>The alphabet in lower case, letter for letter (grammar.rule.alphabet.lower, or each
		/// letter's char.ToLowerInvariant).</summary>
		string AlphabetLower { get; }

		/// <summary>Whether the language's names in block 4 carry a gender (uwdump validate-tsv warns
		/// about a name without one).</summary>
		bool NamesHaveGender { get; }

		/// <summary>What is wrong with a name of block 4 in this language's format, or null.</summary>
		string CheckName(string psText);

		/// <summary>A name as a word: bare, plural, with its article, in a case - "torch", "torches",
		/// "a torch". psRaw is the block 4 entry (or another name in the same format).</summary>
		string Name(string psRaw, bool pbPlural, bool pbArticle, UWGrammarCase peCase);

		/// <summary>The plural where the original adds an "s" to the bare name (identify_inv for a
		/// name without a plural form, the ammunition's "arrows"). psRaw may be null.</summary>
		string AddedPlural(string psRaw);

		/// <summary>A name of block 7 (a conversation partner, the look's creature name) as shown.</summary>
		string PersonName(string psRaw);

		/// <summary>An item as the look names it: one piece with its article, or the count and the
		/// plural; the words before the name in the original's order - the condition word, "cursed",
		/// "magical" (each may be missing).</summary>
		string ItemPhrase(string psRaw, int piCount, string psCondition, bool pbCursed, bool pbMagical);

		/// <summary>A door or a piece of scenery as the look names it, its condition word (or null)
		/// before the name.</summary>
		string ScenePhrase(string psRaw, string psCondition);

		/// <summary>A wall or floor (block 10) as the look names it.</summary>
		string ChunkPhrase(string psText);

		/// <summary>A name that is used as it stands (a talisman's, block 1).</summary>
		string LiteralPhrase(string psText);

		/// <summary>The look sentence around a phrase: "You see {0}.".</summary>
		string LookSentence(string psPhrase);

		/// <summary>What follows an enchanted item's name: " of " the enchantment (psName, may be
		/// null) and the charges (piCharges, -1 for none shown).</summary>
		string EnchantmentPart(string psName, int piCharges);

		/// <summary>What follows an owned thing's name: " belonging to" and the owner (block 1).</summary>
		string OwnerPart(string psOwner);

		/// <summary>A creature's whole look sentence from its attitude word (or null), its kind (the
		/// block 4 entry, or null) and its name (block 7, or null); null when there is neither kind
		/// nor name.</summary>
		string CreatureLook(string psAttitude, string psKind, string psName);

		/// <summary>identify_inv's name for a conversation: the count or the article (when asked for),
		/// "magical" or "cursed", the name - without the enchantment's part.</summary>
		string IdentifyName(string psRaw, int piCount, bool pbMagical, bool pbCursed, bool pbArticle);

		// ------------------------------------------------- The message lines (translation step 3A)

		/// <summary>A known thing as a message's frame takes it ("The {0} is locked.", "You read the
		/// {0}..."): English the bare singular, the frame writes the "the"; German with the definite
		/// article in the case ("die Truhe", "den Dolch", "dem Dolch").</summary>
		string TheName(string psRaw, UWGrammarCase peCase);

		/// <summary>The player's own thing as a frame takes it ("Your {0} was damaged."): English the
		/// bare singular, the frame writes the "Your"; German with the possessive in the case ("dein
		/// Dolch", "deine Fackel", "deinen Dolch").</summary>
		string YourName(string psRaw, UWGrammarCase peCase);

		/// <summary>Whether the name takes its verb in the plural: English a bare name ending in "s"
		/// ("Your leggings were damaged.", as UW.EXE decides it); German a plural-only name (p:).</summary>
		bool TakesPluralVerb(string psRaw);

		/// <summary>A message line as the language writes it: English as it stands (the original's
		/// lines keep their quirks: "a goblin is angered by your action."); German with a capital
		/// first letter, so a frame may start with its name ("{0} ist verschlossen.").</summary>
		string Sentence(string psText);

		/// <summary>The modern interface's name of a thing on a label (the bags' tooltip): the count
		/// before the plural, "Cursed" before the name, the first letter a capital ("Torch", "3
		/// Torches", "Cursed sword"); German nominative without article, the word inflected.</summary>
		string ItemLabel(string psRaw, int piCount, bool pbCursed);

		/// <summary>A name used as it stands (a talisman's, block 1) on a label, first letter a capital.</summary>
		string LiteralLabel(string psText);

		// ------------------------------------------------- What a frame's select chooses by (wave 4)

		/// <summary>The gender letter of a name in the language's name format (UWNameFormat: m f n c p),
		/// null for a name without one (every English name) - a frame's {n,select,...} chooses by it
		/// (UWNamePhrase).</summary>
		string GenderOf(string psRaw);

		/// <summary>The case key (UWNameFormat.CaseKeys) the language puts a name in where the code asks
		/// for a case role: "nom", "acc", "dat" in English and German; a language without its own grammar
		/// names its own in the header (UWGrammarGeneric: grammar.rule.case.*).</summary>
		string CaseKey(UWGrammarCase peCase);
	}

	/// <summary>The grammars and which one a language code takes.</summary>
	public static class UWGrammar
	{
		/// <summary>The English, the default and the grammar of every language without its own.</summary>
		public static readonly IUWGrammar English = new UWGrammarEnglish();

		public static readonly IUWGrammar German = new UWGrammarGerman();

		/// <summary>The grammar of a language code: German for "de" and its regions ("de-AT", "de_CH"),
		/// English for "en" and its regions, an empty code and null; every other code the generic
		/// grammar of its header (UWGrammarGeneric, translation wave 4), one per code.</summary>
		public static IUWGrammar For(string psCode)
		{
			if (string.IsNullOrEmpty(psCode) || psCode.Trim().Length == 0)
				return English;

			string lsCode = psCode.Trim();

			if (fIsLanguage(lsCode, UWGrammarGerman.LanguageCode))
				return German;

			if (fIsLanguage(lsCode, English.Code))
				return English;

			lock (msGeneric)
			{
				string lsKey = lsCode.ToLowerInvariant();

				if (!msGeneric.TryGetValue(lsKey, out IUWGrammar lIGrammar))
				{
					lIGrammar = new UWGrammarGeneric(lsCode);
					msGeneric[lsKey] = lIGrammar;
				}

				return lIGrammar;
			}
		}

		/// <summary>The generic grammars made so far, by code (looked up, never listed).</summary>
		private static readonly System.Collections.Generic.Dictionary<string, IUWGrammar> msGeneric =
			new System.Collections.Generic.Dictionary<string, IUWGrammar>();

		/// <summary>Whether a code is the two-letter language or one of its regions ("de", "de-AT", "de_CH").</summary>
		private static bool fIsLanguage(string psCode, string psLanguage)
		{
			return string.Equals(psCode, psLanguage, StringComparison.OrdinalIgnoreCase)
				|| (psCode.Length > 2 && psCode.StartsWith(psLanguage, StringComparison.OrdinalIgnoreCase) && (psCode[2] == '-' || psCode[2] == '_'));
		}

		/// <summary>The language code of a game-text pack's file: "de" for Languages/de.strings.tsv; null
		/// for a file that does not end like a pack's.</summary>
		public static string CodeOfPackFile(string psPath)
		{
			if (string.IsNullOrEmpty(psPath))
				return null;

			string lsName = Path.GetFileName(psPath);

			return lsName.EndsWith(UWGameTextPack.FileSuffix, StringComparison.OrdinalIgnoreCase)
				? lsName.Substring(0, lsName.Length - UWGameTextPack.FileSuffix.Length)
				: null;
		}

		/// <summary>The grammar the data's names are written for (its game-text pack's), English
		/// without data.</summary>
		public static IUWGrammar Of(DataImport pOData)
		{
			return pOData != null && pOData.Grammar != null ? pOData.Grammar : English;
		}
	}
}
