namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SAFE STRING LOOKUPS over STRINGS.PAK (DataImport.Strings).
	///
	/// The raw accessors of DataImport (GetGeneralMessage, GetObjectDescription, ...) index the
	/// parsed blocks directly and throw on a missing block or entry. Every place that showed a
	/// string therefore wrapped them in its own try/catch and trimmed the trailing CR/LF that
	/// the parsed strings carry - twelve times until 2026-10-11, two of them without the trim.
	/// Here it is written once: a missing block, a missing or empty entry and missing data all
	/// give the fallback (empty unless the caller says otherwise), and the result is trimmed.
	///
	/// THE "+1" CONVENTION lives here too: the parsed blocks sit one entry higher than the
	/// original's numbering (see UWObjectMechanics.GetTextTrapStringIndex), so an object's name
	/// is block 4 at its id plus one. ObjectName and RawObjectName add that one; the message
	/// indices of block 1 and 9 are passed as the project numbers them (the index of the string
	/// export, already parsed).
	///
	/// THE PORT'S OWN TEXTS (T, F, the language file) are the other half of this class, in
	/// UWText.Ui.cs (cleanup step 8.5, 2026-10-24).
	/// </summary>
	public static partial class UWText
	{
		/// <summary>General game messages, the feedback not tied to a level.</summary>
		public const int GeneralBlock = 1;

		/// <summary>Object names, one per object id at id + 1, with the "&amp;" plural and the
		/// "_" article of UWObjectDescriptionFormatter.</summary>
		public const int ObjectNameBlock = 4;

		/// <summary>Level messages (the a_text string traps) and, below 64, global texts.</summary>
		public const int LevelMessageBlock = 9;

		/// <summary>A line of any block, trimmed of CR/LF; the fallback when the block or the
		/// entry is missing or the entry is empty.</summary>
		public static string Block(UWStrings pOStrings, int piBlock, int piIndex, string psFallback = "")
		{
			if (pOStrings == null || pOStrings.Blocks == null || piIndex < 0)
				return psFallback;

			UWStringBlock lOBlock;

			if (!pOStrings.Blocks.TryGetValue(piBlock, out lOBlock) || lOBlock == null
				|| lOBlock.Strings == null || piIndex >= lOBlock.Strings.Count)
				return psFallback;

			string lsText = lOBlock.Strings[piIndex];

			if (lsText == null)
				return psFallback;

			lsText = lsText.TrimEnd('\r', '\n');

			return lsText.Length == 0 ? psFallback : lsText;
		}

		public static string Block(DataImport pOData, int piBlock, int piIndex, string psFallback = "")
		{
			return Block(pOData != null ? pOData.Strings : null, piBlock, piIndex, psFallback);
		}

		/// <summary>A general message (block 1) by the project's numbering.</summary>
		public static string General(DataImport pOData, int piIndex, string psFallback = "")
		{
			return Block(pOData, GeneralBlock, piIndex, psFallback);
		}

		/// <summary>A level message (block 9) by the project's numbering - see
		/// UWObjectMechanics.GetTextTrapStringIndex for how a trap's index is formed.</summary>
		public static string LevelMessage(DataImport pOData, int piIndex, string psFallback = "")
		{
			return Block(pOData, LevelMessageBlock, piIndex, psFallback);
		}

		/// <summary>The raw block 4 entry of an object, "a_torch&amp;torches", with the +1 applied -
		/// for the places that insert a condition word before formatting
		/// (UWItemDescriptions.InsertCondition).</summary>
		public static string RawObjectName(DataImport pOData, int piObjectId, string psFallback = "")
		{
			return Block(pOData, ObjectNameBlock, piObjectId + 1, psFallback);
		}

		/// <summary>
		/// The name of an object as a word: "torch", or "torches" with pbPlural, or "a torch"
		/// with pbArticle - the name format resolved by the language's grammar (IUWGrammar.Name; in
		/// English the "&amp;" and "_" of UWObjectDescriptionFormatter), in a case where the language
		/// has them. The fallback when the name is missing or empty.
		/// </summary>
		public static string ObjectName(DataImport pOData, int piObjectId, bool pbPlural = false,
			bool pbArticle = false, string psFallback = "", UWGrammarCase peCase = UWGrammarCase.Nominative)
		{
			string lsRaw = RawObjectName(pOData, piObjectId, null);

			if (lsRaw == null)
				return psFallback;

			string lsName = UWGrammar.Of(pOData).Name(lsRaw, pbPlural, pbArticle, peCase);

			return string.IsNullOrEmpty(lsName) ? psFallback : lsName;
		}

		/// <summary>The plural where the original adds an "s" to the bare name - "arrows" (the reference's
		/// GetSimpleObjectNameUW plus "s"); the language's plural in a language with a grammar of its own
		/// (IUWGrammar.AddedPlural).</summary>
		public static string ObjectNameAddedPlural(DataImport pOData, int piObjectId)
		{
			return UWGrammar.Of(pOData).AddedPlural(RawObjectName(pOData, piObjectId, null));
		}

		/// <summary>An object's name as a message's frame takes it (IUWGrammar.TheName: English the bare
		/// singular for "The {0} is locked.", German with the definite article in the case); the
		/// fallback when the name is missing or empty (translation step 3A).</summary>
		public static string TheObjectName(DataImport pOData, int piObjectId, UWGrammarCase peCase, string psFallback = "")
		{
			string lsRaw = RawObjectName(pOData, piObjectId, null);

			if (lsRaw == null)
				return psFallback;

			string lsName = UWGrammar.Of(pOData).TheName(lsRaw, peCase);

			return string.IsNullOrEmpty(lsName) ? psFallback : lsName;
		}

		/// <summary>The player's own object as a frame takes it (IUWGrammar.YourName: English the bare
		/// singular for "Your {0} was damaged.", German "dein Dolch"); the fallback as TheObjectName.</summary>
		public static string YourObjectName(DataImport pOData, int piObjectId, UWGrammarCase peCase, string psFallback = "")
		{
			string lsRaw = RawObjectName(pOData, piObjectId, null);

			if (lsRaw == null)
				return psFallback;

			string lsName = UWGrammar.Of(pOData).YourName(lsRaw, peCase);

			return string.IsNullOrEmpty(lsName) ? psFallback : lsName;
		}

		/// <summary>TheObjectName as a frame's argument that a select can choose by: the name with its
		/// gender and case (UWNamePhrase, translation wave 4) - shown, the same text.</summary>
		public static UWNamePhrase TheObject(DataImport pOData, int piObjectId, UWGrammarCase peCase, string psFallback = "")
		{
			return fPhrase(pOData, piObjectId, TheObjectName(pOData, piObjectId, peCase, psFallback), peCase, 1);
		}

		/// <summary>YourObjectName as a frame's argument with gender and case (UWNamePhrase).</summary>
		public static UWNamePhrase YourObject(DataImport pOData, int piObjectId, UWGrammarCase peCase, string psFallback = "")
		{
			return fPhrase(pOData, piObjectId, YourObjectName(pOData, piObjectId, peCase, psFallback), peCase, 1);
		}

		/// <summary>ObjectName as a frame's argument with gender and case (UWNamePhrase); a plural
		/// without a count has none (piCount -1: a plural takes "other").</summary>
		public static UWNamePhrase ObjectPhrase(DataImport pOData, int piObjectId, bool pbPlural, bool pbArticle,
			UWGrammarCase peCase = UWGrammarCase.Nominative)
		{
			return fPhrase(pOData, piObjectId, ObjectName(pOData, piObjectId, pbPlural, pbArticle, string.Empty, peCase), peCase, pbPlural ? -1 : 1);
		}

		/// <summary>ObjectNameAddedPlural as a frame's argument (UWNamePhrase, no count).</summary>
		public static UWNamePhrase ObjectAddedPlural(DataImport pOData, int piObjectId)
		{
			return fPhrase(pOData, piObjectId, ObjectNameAddedPlural(pOData, piObjectId), UWGrammarCase.Nominative, -1);
		}

		private static UWNamePhrase fPhrase(DataImport pOData, int piObjectId, string psText, UWGrammarCase peCase, int piCount)
		{
			IUWGrammar lIGrammar = UWGrammar.Of(pOData);

			return new UWNamePhrase(psText, lIGrammar.GenderOf(RawObjectName(pOData, piObjectId, null)), lIGrammar.CaseKey(peCase), piCount);
		}

		/// <summary>An object's name on the modern interface's label (the bags' tooltip):
		/// IUWGrammar.ItemLabel - "Torch", "3 Torches", "Cursed sword".</summary>
		public static string ObjectLabel(DataImport pOData, int piObjectId, int piCount, bool pbCursed)
		{
			return UWGrammar.Of(pOData).ItemLabel(RawObjectName(pOData, piObjectId, null), piCount, pbCursed);
		}

		/// <summary>A message line from its frame (F) as the language of the data's names writes a
		/// sentence (IUWGrammar.Sentence: German starts it with a capital, English leaves it).</summary>
		public static string Sentence(DataImport pOData, string psId, params object[] paArgs)
		{
			return UWGrammar.Of(pOData).Sentence(F(psId, paArgs));
		}

		/// <summary>A conversation partner's name (block 7) as shown - IUWGrammar.PersonName; the
		/// fallback when it is missing or empty.</summary>
		public static string PartnerName(DataImport pOData, int piIndex, string psFallback = "")
		{
			string lsRaw = Block(pOData, UWConversations.PartnerNameStringBlock, piIndex, null);

			return lsRaw == null ? psFallback : UWGrammar.Of(pOData).PersonName(lsRaw);
		}

		/// <summary>The first letter upper-case, the rest as it is - a name as a title or a
		/// heading ("Wisp", "Map case"). Null and empty stay as they are.</summary>
		public static string Capitalised(string psText)
		{
			if (string.IsNullOrEmpty(psText))
				return psText;

			return char.ToUpperInvariant(psText[0]) + psText.Substring(1);
		}
	}
}
