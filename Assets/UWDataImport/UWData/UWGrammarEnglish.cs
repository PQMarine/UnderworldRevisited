using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ENGLISH GRAMMAR (translation wave 2, 2026-10-26): the sentence building of the look, of
	/// identify_inv and of the creature's look as UWItemDescriptions and UWObjectDescriptionFormatter
	/// had it until then, moved here unchanged - every English sentence stays byte for byte what it
	/// was (the self-check's look catalog, Pins/look-strings.txt). The names follow the original's
	/// block 4 format: "&amp;" between singular and plural, "_" after the article
	/// (UWObjectDescriptionFormatter); the article is chosen anew for the word that follows it.
	///
	/// The frames and the words of UW.EXE come through UWText (UWTextEnglish.Grammar): a language
	/// without a grammar of its own (every code but German) keeps these rules with its own frames.
	/// UWGrammarGerman builds on this class: a name without the German format falls back to these
	/// rules, so an entry a pack leaves English reads as English.
	/// </summary>
	public class UWGrammarEnglish : IUWGrammar
	{
		public virtual string Code => "en";

		public virtual string TypeableLetters => string.Empty;

		// ------------------------------------------------- The alphabet (translation 5.1)

		/// <summary>The letter grid's letters as they always were, and the default of every language.</summary>
		public const string LatinAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

		/// <summary>The value of the lower-case list that derives it from the upper case.</summary>
		public const string AutoValue = "auto";

		/// <summary>The header's alphabet (grammar.rule.alphabet) - read for EVERY language, English and
		/// German too, whose files may name one; the English text table's value is LatinAlphabet.</summary>
		public string Alphabet => ParseAlphabet(UWText.T("grammar.rule.alphabet"));

		public string AlphabetLower => ParseAlphabetLower(UWText.T("grammar.rule.alphabet.lower"), Alphabet);

		/// <summary>An alphabet as the header writes it: its letters in order, blanks between them
		/// allowed, each letter once, anything that is no letter left out; LatinAlphabet for an empty
		/// list, "none" or "auto".</summary>
		public static string ParseAlphabet(string psRule)
		{
			StringBuilder lOLetters = new StringBuilder();
			string lsRule = (psRule ?? string.Empty).Trim();

			if (lsRule != "none" && lsRule != AutoValue)
			{
				foreach (char lcChar in lsRule)
				{
					if (char.IsLetter(lcChar) && lOLetters.ToString().IndexOf(lcChar) < 0)
						lOLetters.Append(lcChar);
				}
			}

			return lOLetters.Length == 0 ? LatinAlphabet : lOLetters.ToString();
		}

		/// <summary>The lower-case alphabet: the header's list when it has a letter for each of the
		/// upper case's, else each upper-case letter's char.ToLowerInvariant.</summary>
		public static string ParseAlphabetLower(string psRule, string psUpper)
		{
			StringBuilder lOLetters = new StringBuilder();

			foreach (char lcChar in (psRule ?? string.Empty).Trim() == AutoValue ? string.Empty : psRule ?? string.Empty)
			{
				if (char.IsLetter(lcChar))
					lOLetters.Append(lcChar);
			}

			return lOLetters.Length == psUpper.Length ? lOLetters.ToString() : psUpper.ToLowerInvariant();
		}

		public virtual bool NamesHaveGender => false;

		public virtual string CheckName(string psText)
		{
			return null;
		}

		// ------------------------------------------------- The words of UW.EXE

		/// <summary>"magical", the word Lore's middle result puts before an enchanted name.</summary>
		protected static string MagicalWord => UWText.T("grammar.word.magical");

		/// <summary>"cursed", the word a curse puts before the name at the full detail.</summary>
		protected static string CursedWord => UWText.T("grammar.word.cursed");

		// ------------------------------------------------- Names

		/// <summary>UWText.ObjectName's resolution until 2026-10-26: FormatItemName with the article,
		/// FormatBareItemName without.</summary>
		public virtual string Name(string psRaw, bool pbPlural, bool pbArticle, UWGrammarCase peCase)
		{
			return pbArticle
				? UWObjectDescriptionFormatter.FormatItemName(psRaw, pbPlural)
				: UWObjectDescriptionFormatter.FormatBareItemName(psRaw, pbPlural);
		}

		/// <summary>The bare singular with an "s" (GetSimpleObjectNameUW plus "s" in the reference).</summary>
		public virtual string AddedPlural(string psRaw)
		{
			return (psRaw == null ? string.Empty : UWObjectDescriptionFormatter.FormatBareItemName(psRaw, false)) + "s";
		}

		public virtual string PersonName(string psRaw)
		{
			return psRaw;
		}

		// ------------------------------------------------- The look

		/// <summary>
		/// LookAt's name (UWItemDescriptions.DescribeItem until 2026-10-26). In the original the words
		/// before the name come in this order: condition, cursed, magical. They are inserted BEFORE the
		/// article is resolved so that the "a" can become "an". For a stack the number replaces the
		/// article - "a loaf of bread" becomes "11 loaves of bread" - and the words go between the count
		/// and the plural in the same order: LookAt (UnderworldExhumed obj/LOOK.C) writes the count, the
		/// quality word, the modifiers, then the plural ("5 day old ears of corn", per user on the
		/// original 2026-10-17). Without a plural form the article goes ("2 a dagger" would come out).
		/// </summary>
		public virtual string ItemPhrase(string psRaw, int piCount, string psCondition, bool pbCursed, bool pbMagical)
		{
			string lsRaw = psRaw;

			if (pbMagical)
				lsRaw = UWItemDescriptions.InsertCondition(lsRaw, MagicalWord);

			if (pbCursed)
				lsRaw = UWItemDescriptions.InsertCondition(lsRaw, CursedWord);

			if (!string.IsNullOrEmpty(psCondition))
				lsRaw = UWItemDescriptions.InsertCondition(lsRaw, psCondition);

			string lsName = UWObjectDescriptionFormatter.FormatItemName(lsRaw, piCount > 1);

			if (piCount <= 1)
				return lsName;

			string lsPlural = psRaw != null && psRaw.Contains("&")
				? UWObjectDescriptionFormatter.FormatItemName(psRaw, true)
				: UWObjectDescriptionFormatter.FormatBareItemName(psRaw, false);

			StringBuilder lOWords = new StringBuilder();

			if (!string.IsNullOrEmpty(psCondition))
				lOWords.Append(psCondition).Append(' ');

			if (pbCursed)
				lOWords.Append(CursedWord).Append(' ');

			if (pbMagical)
				lOWords.Append(MagicalWord).Append(' ');

			return piCount + " " + lOWords + lsPlural;
		}

		/// <summary>The door's condition word goes after the article ("a_door" becomes "a_sturdy door").</summary>
		public virtual string ScenePhrase(string psRaw, string psCondition)
		{
			string lsRaw = string.IsNullOrEmpty(psCondition) ? psRaw : UWItemDescriptions.InsertCondition(psRaw, psCondition);

			return UWObjectDescriptionFormatter.FormatItemName(lsRaw, false);
		}

		/// <summary>The texture names of block 10 carry their article ("a stone wall").</summary>
		public virtual string ChunkPhrase(string psText)
		{
			return UWObjectDescriptionFormatter.FormatItemName(psText, false);
		}

		public virtual string LiteralPhrase(string psText)
		{
			return psText;
		}

		public virtual string LookSentence(string psPhrase)
		{
			return UWText.F("grammar.look.you_see", psPhrase);
		}

		/// <summary>
		/// do_of (LOOK.C) at the full detail: " of &lt;enchantment&gt;" and the charges. The original
		/// ends with "with N full charges" - without "remaining", and for a single charge in the
		/// singular: "with 1 full charge" (both per user, 2026-09-05).
		/// </summary>
		public virtual string EnchantmentPart(string psName, int piCharges)
		{
			string lsSuffix = !string.IsNullOrEmpty(psName) ? UWText.F("grammar.look.of", psName) : string.Empty;

			if (piCharges >= 0)
				lsSuffix += piCharges == 0
					? UWText.T("grammar.look.charges.none")
					: piCharges == 1
						? UWText.T("grammar.look.charges.one")
						: UWText.F("grammar.look.charges.many", piCharges);

			return lsSuffix;
		}

		/// <summary>" belonging to" and the owner as block 1 has him, with his leading blank.</summary>
		public virtual string OwnerPart(string psOwner)
		{
			return UWText.F("grammar.look.owner", psOwner);
		}

		/// <summary>
		/// CritterLook (UnderworldExhumed obj/LOOK.C, read 2026-10-15; UWItemDescriptions until
		/// 2026-10-26): the attitude word with its own "a" or "an" (only with a kind), the kind without
		/// its article after the attitude word, with it otherwise, and the name. A NAME IN LOWER CASE
		/// REPLACES THE KIND ("You see a hostile head bandit."); one in upper case follows it with
		/// "named"; without a kind the name stands alone with its article.
		/// </summary>
		public virtual string CreatureLook(string psAttitude, string psKind, string psName)
		{
			if (psKind == null && psName == null)
				return null;

			bool lbUpperName = psName != null && psName[0] >= 'A' && psName[0] <= 'Z';
			string lsText = string.Empty;

			if (psKind != null && psAttitude != null)
				lsText += (UWObjectDescriptionFormatter.StartsWithVowel(psAttitude) ? "an " : "a ") + psAttitude + " ";

			if (psKind != null && (psName == null || lbUpperName))
				lsText += FixName(psKind, psAttitude == null);

			if (psName != null)
			{
				if (psKind != null && lbUpperName)
					lsText = UWText.F("grammar.look.named", lsText, FixName(psName, false));
				else
					lsText += FixName(psName, psKind == null);
			}

			return LookSentence(lsText);
		}

		/// <summary>fix_name_string (ui/GAMESTRN.C) for the singular: the plural after "&amp;" is
		/// cut, and the "_" after the article becomes a space (pbArticle) or takes the article
		/// with it.</summary>
		protected static string FixName(string psRaw, bool pbArticle)
		{
			string lsName = psRaw;
			int liPlural = lsName.IndexOf('&');

			if (liPlural >= 0)
				lsName = lsName.Substring(0, liPlural);

			int liSeparator = lsName.IndexOf('_');

			if (liSeparator < 0)
				return lsName;

			return pbArticle
				? lsName.Substring(0, liSeparator) + " " + lsName.Substring(liSeparator + 1)
				: lsName.Substring(liSeparator + 1);
		}

		/// <summary>
		/// conv_inv_name (CONVERSE.C, read 2026-10-15; UWItemDescriptions.IdentifyName until
		/// 2026-10-26): a pile of more than one starts with its count and takes the plural; otherwise
		/// the article only when the script asks for it. do_mods' "magical " decides the article: its
		/// 'm' takes "a ". A cursed thing is "cursed" before the article the name brings, as the
		/// original builds it ("cursed a crown"). get_name with fix_name_string: the plural after the
		/// "&amp;", or the singular with an "s".
		/// </summary>
		public virtual string IdentifyName(string psRaw, int piCount, bool pbMagical, bool pbCursed, bool pbArticle)
		{
			bool lbArticle = pbArticle;
			string lsName = string.Empty;

			if (pbMagical && lbArticle && piCount == 1)
			{
				lsName = "a ";
				lbArticle = false;
			}
			else if (piCount > 1)
			{
				lsName = piCount + " ";
				lbArticle = false;
			}

			if (pbMagical)
				lsName += MagicalWord + " ";
			else if (pbCursed)
				lsName += CursedWord + " ";

			if (!string.IsNullOrEmpty(psRaw))
				lsName += piCount > 1 && psRaw.IndexOf('&') < 0
					? AddedPlural(psRaw)
					: Name(psRaw, piCount > 1, lbArticle, UWGrammarCase.Nominative);

			return lsName;
		}

		// ------------------------------------------------- The message lines (translation step 3A)
		//
		// The sentences UW.EXE assembles around a name ("The ", the bare name, " is locked.") until
		// 2026-10-26 written in code (UWItemMessages, UWEquipmentWear, UWTrapDisarming, UWRepairRules,
		// ...): their frames are UWText ids now (UWTextEnglish.Messages) and the name comes from here,
		// the bare singular as the code took it (UWText.ObjectName).

		public virtual string TheName(string psRaw, UWGrammarCase peCase)
		{
			return Name(psRaw, false, false, peCase);
		}

		public virtual string YourName(string psRaw, UWGrammarCase peCase)
		{
			return Name(psRaw, false, false, peCase);
		}

		/// <summary>UWEquipmentWear until 2026-10-26: the bare name ending in "s" takes "were".</summary>
		public virtual bool TakesPluralVerb(string psRaw)
		{
			return psRaw != null && Name(psRaw, false, false, UWGrammarCase.Nominative).EndsWith("s");
		}

		public virtual string Sentence(string psText)
		{
			return psText;
		}

		/// <summary>The bags' tooltip (UWModernBagView until 2026-10-26): the bare name, plural for a
		/// pile, with a capital; a cursed thing "Cursed" and the name with its first letter small
		/// (the frame hud.item.cursed); the count before it all.</summary>
		public virtual string ItemLabel(string psRaw, int piCount, bool pbCursed)
		{
			string lsName = psRaw == null ? string.Empty : UWText.Capitalised(Name(psRaw, piCount > 1, false, UWGrammarCase.Nominative));

			if (pbCursed && lsName.Length > 0)
				lsName = UWText.F("hud.item.cursed", char.ToLowerInvariant(lsName[0]) + lsName.Substring(1));

			if (piCount > 1)
				lsName = piCount + " " + lsName;

			return lsName;
		}

		public virtual string LiteralLabel(string psText)
		{
			return UWText.Capitalised(psText);
		}

		// ------------------------------------------------- What a frame's select chooses by (wave 4)

		/// <summary>English names have no gender: a select takes its "other".</summary>
		public virtual string GenderOf(string psRaw)
		{
			return null;
		}

		public virtual string CaseKey(UWGrammarCase peCase)
		{
			return UWNameFormat.CaseKeyOf(peCase);
		}
	}
}
