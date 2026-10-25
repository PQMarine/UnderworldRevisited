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
		/// with pbArticle - the "&amp;" and "_" conventions resolved by UWObjectDescriptionFormatter.
		/// The fallback when the name is missing or empty.
		/// </summary>
		public static string ObjectName(DataImport pOData, int piObjectId, bool pbPlural = false,
			bool pbArticle = false, string psFallback = "")
		{
			string lsRaw = RawObjectName(pOData, piObjectId, null);

			if (lsRaw == null)
				return psFallback;

			string lsName = pbArticle
				? UWObjectDescriptionFormatter.FormatItemName(lsRaw, pbPlural)
				: UWObjectDescriptionFormatter.FormatBareItemName(lsRaw, pbPlural);

			return string.IsNullOrEmpty(lsName) ? psFallback : lsName;
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
