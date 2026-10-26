using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A GAME-TEXT PACK (translation wave 1, layer 1 of the plan in Todo.md, "Translations";
	/// 2026-10-26): the game's own strings of a language, laid over the English ones of the player's
	/// STRINGS.PAK right after they are read (UWGameData). An entry the pack does not hold stays
	/// English; the numbers stay as they are, so the conversations, the traps, the shrines and the
	/// cutscenes find their texts where they always did.
	///
	/// THE FILE, Languages/&lt;code&gt;.strings.tsv beside the game (UWLanguage; FileSuffix), is the
	/// format of the higesho fork's Localization/ja.tsv, so a translation made for it runs as it
	/// is:
	///   - UTF-8, a byte order mark allowed; one string per line: the block, a TAB, the index, a
	///     TAB, the text. Block and index in decimal, as the port numbers them: the index of the
	///     parsed list (UWStringBlock), where 1 is a block's first string and 0 the empty slot.
	///   - Empty lines, lines of blanks and lines starting with # are skipped.
	///   - An empty text keeps the English (the fork's validator refuses one; its loader would have
	///     blanked the string), and so does a line of block and index alone - an editor that trims
	///     the ends of lines takes the TAB before an empty text.
	///   - In the text \n is a line break - the one escape the fork knows. Here \t (a TAB), \r and
	///     \\ (a backslash) are read too; every other backslash stays, as the game's own codes need
	///     (\m, \p, \0 in the conversations and the save messages). The English holds no backslash
	///     before n, r, t or a backslash, so the two readers read every real file alike.
	///   - A line that is not block, index and text, or names a string the player's STRINGS.PAK
	///     does not have, is skipped and reported; two lines for one string: the later counts.
	/// Nothing in a pack can make the game fail.
	///
	/// THE ORIGINAL'S TEXT IS NEVER SHIPPED: a translator starts from a template made of their
	/// own STRINGS.PAK (Template, `uwdump strings-template`), and `uwdump validate-tsv` checks a
	/// pack against it (Validate: the conversation VM's placeholders, the game's backslash codes,
	/// the line breaks).
	///
	/// LAYER 2 (wave 2, 2026-10-26): the sentences assembled in code - the look with its article,
	/// plural, condition word and owner, the creature's look, identify_inv's name - are the grammar's
	/// of the pack's language (IUWGrammar, chosen by the file's code: UWGameData.Grammar), their
	/// frames UWText ids. The words the player types or the code compares - the conversations'
	/// compare and contains against typed answers, the shrine's mantras and the hints that name them
	/// - must still be translated consistently, or a quest cannot be solved in the language: `uwdump
	/// typed-words` lists them (UWConversationTextRules).
	/// </summary>
	public sealed class UWGameTextPack
	{
		/// <summary>The end of a game-text pack's name: de.strings.tsv beside de.tsv, the port's own
		/// texts of the same language.</summary>
		public const string FileSuffix = ".strings.tsv";

		/// <summary>One line with a text.</summary>
		public sealed class Row
		{
			/// <summary>The line in the file, from 1.</summary>
			public int Line;

			public int Block;

			public int Index;

			/// <summary>The text, escapes read.</summary>
			public string Text;

			public string Key => Block.ToString(CultureInfo.InvariantCulture) + ":" + Index.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>The lines with a text, in the file's order.</summary>
		public readonly List<Row> Rows = new List<Row>();

		/// <summary>Lines whose text is empty: those strings stay English.</summary>
		public int EmptyRows { get; private set; }

		/// <summary>One line per skipped line: "line 12: ...".</summary>
		public readonly List<string> Problems = new List<string>();

		/// <summary>How many strings the last Apply replaced.</summary>
		public int Applied { get; private set; }

		/// <summary>The file it was read from, or null.</summary>
		public string FilePath { get; private set; }

		/// <summary>The lines Parse skipped, for Validate.</summary>
		private readonly List<Finding> mOSkipped = new List<Finding>();

		// ------------------------------------------------- Reading

		public static UWGameTextPack Parse(string psContent)
		{
			UWGameTextPack lOPack = new UWGameTextPack();

			if (string.IsNullOrEmpty(psContent))
				return lOPack;

			string[] lsLines = psContent.Split('\n');

			for (int liLine = 0; liLine < lsLines.Length; liLine++)
			{
				// A byte order mark (U+FEFF) at the start of a line is skipped.
				string lsLine = lsLines[liLine].TrimEnd('\r').TrimStart((char)0xFEFF);

				if (string.IsNullOrWhiteSpace(lsLine) || lsLine[0] == '#')
					continue;

				string[] lsFields = lsLine.Split(new[] { '\t' }, 3);

				// Block and index alone: an empty text whose TAB an editor took off the line's end.
				if (lsFields.Length == 2)
					lsFields = new[] { lsFields[0], lsFields[1], string.Empty };

				if (lsFields.Length != 3)
				{
					lOPack.fSkip(liLine + 1, "not block, TAB, index, TAB, text");

					continue;
				}

				if (!int.TryParse(lsFields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int liBlock)
					|| !int.TryParse(lsFields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int liIndex))
				{
					lOPack.fSkip(liLine + 1, "block or index is no number");

					continue;
				}

				if (lsFields[2].Length == 0)
				{
					lOPack.EmptyRows++;

					continue;
				}

				lOPack.Rows.Add(new Row { Line = liLine + 1, Block = liBlock, Index = liIndex, Text = Unescape(lsFields[2]) });
			}

			return lOPack;
		}

		private void fSkip(int piLine, string psWhy)
		{
			Problems.Add("line " + piLine + ": " + psWhy);
			mOSkipped.Add(new Finding { Line = piLine, Key = string.Empty, IsError = true, Message = psWhy });
		}

		/// <summary>Lays the pack's texts over the strings; a row that names no string of theirs is
		/// reported and skipped. Returns how many strings were replaced.</summary>
		public int Apply(UWStrings pOStrings)
		{
			Applied = 0;

			if (pOStrings == null || pOStrings.Blocks == null)
				return 0;

			HashSet<string> lOSeen = new HashSet<string>();

			foreach (Row lORow in Rows)
			{
				if (!fHasString(pOStrings, lORow.Block, lORow.Index))
				{
					Problems.Add("line " + lORow.Line + ": no string " + lORow.Key);

					continue;
				}

				if (!lOSeen.Add(lORow.Key))
					Problems.Add("line " + lORow.Line + ": " + lORow.Key + " again, the later text counts");

				pOStrings.Blocks[lORow.Block].Strings[lORow.Index] = lORow.Text;
				Applied++;
			}

			return Applied;
		}

		/// <summary>Reads a pack and lays it over the strings. Null when the file cannot be read - the
		/// strings stay English then; never throws.</summary>
		public static UWGameTextPack Load(string psPath, UWStrings pOStrings)
		{
			UWGameTextPack lOPack;

			try
			{
				lOPack = Parse(File.ReadAllText(psPath, Encoding.UTF8));
			}
			catch (Exception)
			{
				return null;
			}

			lOPack.FilePath = psPath;
			lOPack.Apply(pOStrings);

			return lOPack;
		}

		private static bool fHasString(UWStrings pOStrings, int piBlock, int piIndex)
		{
			return pOStrings.Blocks.TryGetValue(piBlock, out UWStringBlock lOBlock) && lOBlock != null && lOBlock.Strings != null
				&& piIndex >= 1 && piIndex < lOBlock.Strings.Count;
		}

		// ------------------------------------------------- Escapes

		/// <summary>A text as a pack writes it: a line break as \n, a TAB as \t, a carriage return as
		/// \r; a backslash doubled only where it would otherwise read as one of these escapes, so the
		/// game's own codes (\m, \p, \0) stay as the fork's files have them.</summary>
		public static string Escape(string psText)
		{
			if (string.IsNullOrEmpty(psText))
				return string.Empty;

			StringBuilder lOText = new StringBuilder(psText.Length + 8);

			for (int liAt = 0; liAt < psText.Length; liAt++)
			{
				char lcChar = psText[liAt];

				if (lcChar == '\n')
					lOText.Append("\\n");
				else if (lcChar == '\r')
					lOText.Append("\\r");
				else if (lcChar == '\t')
					lOText.Append("\\t");
				else if (lcChar == '\\' && liAt + 1 < psText.Length && "nrt\\".IndexOf(psText[liAt + 1]) >= 0)
					lOText.Append("\\\\");
				else
					lOText.Append(lcChar);
			}

			return lOText.ToString();
		}

		public static string Unescape(string psText)
		{
			if (psText.IndexOf('\\') < 0)
				return psText;

			StringBuilder lOText = new StringBuilder(psText.Length);

			for (int liAt = 0; liAt < psText.Length; liAt++)
			{
				char lcChar = psText[liAt];

				if (lcChar != '\\' || liAt + 1 >= psText.Length)
				{
					lOText.Append(lcChar);

					continue;
				}

				char lcNext = psText[liAt + 1];

				if (lcNext == 'n')
					lOText.Append('\n');
				else if (lcNext == 'r')
					lOText.Append('\r');
				else if (lcNext == 't')
					lOText.Append('\t');
				else if (lcNext == '\\')
					lOText.Append('\\');
				else
				{
					// The game's own code: the backslash stays, the next character is read as usual.
					lOText.Append('\\');

					continue;
				}

				liAt++;
			}

			return lOText.ToString();
		}

		// ------------------------------------------------- The template

		/// <summary>
		/// A pack of every non-empty string of these strings - the player's own STRINGS.PAK - for a
		/// translator to start from: with the English as the text (a row left alone changes nothing),
		/// or with pbEmpty an empty text under the English as a comment. The original's text: written
		/// where the translator chooses, never shipped.
		/// </summary>
		public static string Template(UWStrings pOStrings, string psCode, bool pbEmpty)
		{
			StringBuilder lOText = new StringBuilder();
			List<int> lOBlocks = new List<int>(pOStrings.Blocks.Keys);

			lOText.Append("# Underworld Revisited, the game's texts (STRINGS.PAK) in the language ").Append(psCode)
				.Append(": Languages/").Append(psCode).Append(FileSuffix).Append(" beside the game.\n");
			lOText.Append("# One string per line: block TAB index TAB text, in decimal as the port numbers them (index 1 is a block's first string).\n");
			lOText.Append("# In the text \\n is a line break, \\t a TAB, \\\\ a backslash before n, r, t or another backslash;\n");
			lOText.Append("# every other backslash is the game's own code (\\m, \\p, \\0) and stays. Keep the placeholders (@SS1, @GI3C2).\n");
			lOText.Append("# An empty text keeps the English; lines starting with # are comments.\n");
			lOText.Append("# Made from the player's own STRINGS.PAK (uwdump strings-template): the original's text, not to be published as it is.\n");
			lOText.Append("# block\tindex\ttext\n");

			lOBlocks.Sort();

			foreach (int liBlock in lOBlocks)
			{
				List<string> lsStrings = pOStrings.Blocks[liBlock].Strings;

				for (int liIndex = 1; lsStrings != null && liIndex < lsStrings.Count; liIndex++)
				{
					string lsEnglish = lsStrings[liIndex];

					if (string.IsNullOrEmpty(lsEnglish))
						continue;

					string lsKey = liBlock.ToString(CultureInfo.InvariantCulture) + "\t" + liIndex.ToString(CultureInfo.InvariantCulture) + "\t";

					if (pbEmpty)
						lOText.Append("# ").Append(Escape(lsEnglish)).Append('\n').Append(lsKey).Append('\n');
					else
						lOText.Append(lsKey).Append(Escape(lsEnglish)).Append('\n');
				}
			}

			return lOText.ToString();
		}

		// ------------------------------------------------- Checking a pack

		/// <summary>What Validate found at a line.</summary>
		public sealed class Finding
		{
			public int Line;

			/// <summary>"block:index", empty for a line that is no row.</summary>
			public string Key;

			/// <summary>An error makes the string wrong in the game; a warning is worth a look.</summary>
			public bool IsError;

			public string Message;

			public override string ToString()
			{
				return (IsError ? "error" : "warning") + " line " + Line + (string.IsNullOrEmpty(Key) ? string.Empty : " (" + Key + ")")
					+ ": " + Message;
			}
		}

		/// <summary>
		/// THE CONVERSATION VM'S PLACEHOLDERS as UWConversationVM.fGetSubstitutedString reads them:
		/// @, the source G S or P, a type character, an optional minus and digits; then an optional SI
		/// with digits (a shift read from the stack - the VM takes the SI even without digits) and an
		/// optional C with digits (an array index). A translation must keep each of them whole, in any
		/// order: "@SS1" followed by "SIE" would turn into a shift and swallow the letters.
		/// </summary>
		private static readonly Regex msPlaceholder = new Regex(@"@[GSP][\s\S]-?[0-9]+(?:SI[0-9]*)?(?:C[0-9]+)?", RegexOptions.CultureInvariant);

		/// <summary>The game's backslash codes (\m, \p, \0, \4 ...) and printf-style formats, as the
		/// fork's validator compares them.</summary>
		private static readonly Regex msCode = new Regex(@"\\[A-Za-z0-9]|%[0-9]*[$]?[dfs]", RegexOptions.CultureInvariant);

		/// <summary>A text's placeholders, sorted ("@GS8,@SS1"): two texts that fill in the same ones
		/// have the same signature.</summary>
		public static string PlaceholderSignature(string psText)
		{
			return fSignature(msPlaceholder, psText);
		}

		/// <summary>A text's backslash codes and formats, sorted.</summary>
		public static string CodeSignature(string psText)
		{
			return fSignature(msCode, psText);
		}

		private static string fSignature(Regex pORegex, string psText)
		{
			List<string> lsFound = new List<string>();

			foreach (Match lOMatch in pORegex.Matches(psText ?? string.Empty))
				lsFound.Add(lOMatch.Value);

			lsFound.Sort(StringComparer.Ordinal);

			return string.Join(",", lsFound);
		}

		/// <summary>
		/// Checks the pack against the English strings (the player's STRINGS.PAK, no pack laid over):
		/// ERRORS - a line that is no row, a string the English does not have (index 0 included), a
		/// string given twice, an English string that is empty, the VM's placeholders or the game's
		/// codes not kept, a line break at the end lost or added; WARNINGS - another number of line
		/// breaks inside, a blank at the start or end lost or added, a control character of the
		/// English missing; with the language's grammar (pIGrammar, translation wave 2) an object name of
		/// block 4 its name format cannot read (German: no gender prefix - IUWGrammar.CheckName). The
		/// same English text is no finding (a template row left alone).
		///
		/// SLOTS, SELECT AND PLURAL (translation wave 4): a string that is a piece of a frame
		/// (UWTextFragments) is read as the frames are - braces that pair, "other" present, nesting one
		/// level, each {n} an argument of its frame (ERRORS), a plural category the language
		/// (psPluralCode) never takes (WARNING); a word of block 5 the same with {0} the name; a brace in
		/// any other string is shown as written (WARNING). A name of blocks 1, 7 and 10 written in the
		/// name format is checked like block 4's. With the language's own texts (pFrame, the frame text
		/// of an id or null - Languages/&lt;code&gt;.tsv beside the pack): a frame that leaves an argument of
		/// the English to its fragments ("{0}." for msg.repair.result) needs every fragment of it to name
		/// that argument, else the name is lost (ERROR at the fragment's row, or at line 0 for a
		/// fragment the pack leaves English).
		/// </summary>
		public List<Finding> Validate(UWStrings pOEnglish, IUWGrammar pIGrammar = null, Func<string, string> pFrame = null, string psPluralCode = null)
		{
			List<Finding> lOFound = new List<Finding>(mOSkipped);

			fValidateFrames(lOFound, pOEnglish, pFrame);

			HashSet<string> lOSeen = new HashSet<string>();

			foreach (Row lORow in Rows)
			{
				if (!fHasString(pOEnglish, lORow.Block, lORow.Index))
				{
					fFind(lOFound, lORow, true, "the English has no string " + lORow.Key);

					continue;
				}

				if (!lOSeen.Add(lORow.Key))
				{
					fFind(lOFound, lORow, true, "given twice (the later counts)");

					continue;
				}

				string lsEnglish = pOEnglish.Blocks[lORow.Block].Strings[lORow.Index] ?? string.Empty;
				string lsText = lORow.Text;

				if (lsEnglish.Length == 0)
				{
					fFind(lOFound, lORow, true, "the English string is empty");

					continue;
				}

				if (lsText == lsEnglish)
					continue;

				string lsWanted = PlaceholderSignature(lsEnglish);
				string lsGiven = PlaceholderSignature(lsText);

				if (lsWanted != lsGiven)
					fFind(lOFound, lORow, true, "placeholders [" + lsGiven + "], the English has [" + lsWanted + "]");

				lsWanted = CodeSignature(lsEnglish);
				lsGiven = CodeSignature(lsText);

				if (lsWanted != lsGiven)
					fFind(lOFound, lORow, true, "codes [" + lsGiven + "], the English has [" + lsWanted + "]");

				if (lsEnglish.EndsWith("\n", StringComparison.Ordinal) != lsText.EndsWith("\n", StringComparison.Ordinal))
					fFind(lOFound, lORow, true, lsEnglish.EndsWith("\n", StringComparison.Ordinal)
						? "the English ends with a line break (\\n), the text does not"
						: "the text ends with a line break (\\n), the English does not");
				else if (fCount(lsEnglish, '\n') != fCount(lsText, '\n'))
					fFind(lOFound, lORow, false, fCount(lsText, '\n') + " line breaks, the English has " + fCount(lsEnglish, '\n'));

				// A piece of a frame with slots places the frame's arguments itself (wave 4): its blanks at
				// the ends no longer join it to them.
				bool lbSlotted = lsText.IndexOf('{') >= 0 && UWTextFragments.Find(lORow.Block, lORow.Index) != null;

				if (!lbSlotted && fStartsBlank(lsEnglish) != fStartsBlank(lsText))
					fFind(lOFound, lORow, false, fStartsBlank(lsEnglish) ? "the English starts with a blank, the text does not" : "the text starts with a blank, the English does not");

				if (!lbSlotted && fEndsBlank(lsEnglish) != fEndsBlank(lsText))
					fFind(lOFound, lORow, false, fEndsBlank(lsEnglish) ? "the English ends with a blank, the text does not" : "the text ends with a blank, the English does not");

				foreach (char lcChar in lsEnglish)
				{
					if (lcChar < ' ' && lcChar != '\n' && lsText.IndexOf(lcChar) < 0)
					{
						fFind(lOFound, lORow, false, "the English's control character 0x" + ((int)lcChar).ToString("X2", CultureInfo.InvariantCulture) + " is missing");

						break;
					}
				}

				// The language's name format (translation wave 2): an object name of block 4 the grammar
				// cannot read - in German one without its gender prefix (IUWGrammar.CheckName).
				string lsNameProblem = pIGrammar != null && lORow.Block == UWText.ObjectNameBlock ? pIGrammar.CheckName(lsText) : null;

				// Since wave 4 a name of blocks 1, 7 and 10 written in the format too (one without the
				// prefix is fine there: most of those strings are no names).
				if (pIGrammar != null && lORow.Block != UWText.ObjectNameBlock && Array.IndexOf(msNameBlocks, lORow.Block) >= 0
					&& UWNameFormat.Parse(lsText) != null)
					lsNameProblem = pIGrammar.CheckName(lsText);

				if (lsNameProblem != null)
					fFind(lOFound, lORow, false, "name format (" + pIGrammar.Code + "): " + lsNameProblem);

				fValidateSlots(lOFound, lORow, psPluralCode ?? (pIGrammar != null ? pIGrammar.Code : null));
			}

			// By line, the findings of one line in the order found (OrderBy is stable).
			return new List<Finding>(System.Linq.Enumerable.OrderBy(lOFound, pOFinding => pOFinding.Line));
		}

		/// <summary>The blocks besides 4 whose strings a grammar may read as names (talismans in 1, the
		/// partners in 7, walls and floors in 10).</summary>
		private static readonly int[] msNameBlocks = { 1, 7, 10 };

		/// <summary>The block of the words a grammar puts beside a name (conditions, attitudes).</summary>
		private const int WordBlock = 5;

		/// <summary>A row's slots, select and plural: read as its frame reads them.</summary>
		private static void fValidateSlots(List<Finding> pOFound, Row pORow, string psPluralCode)
		{
			string lsText = pORow.Text;

			if (lsText.IndexOf('{') < 0 && lsText.IndexOf('}') < 0)
				return;

			UWTextFragments.Use lOUse = UWTextFragments.Find(pORow.Block, pORow.Index);
			int liArguments;
			string lsWhat;

			if (lOUse != null)
			{
				liArguments = UWText.PlaceholderCount(UWText.English(lOUse.Frame));
				lsWhat = "a piece of the frame " + lOUse.Frame + " (" + lOUse.Arguments + ")";
			}
			else if (pORow.Block == WordBlock)
			{
				liArguments = 1;
				lsWhat = "a word beside a name ({0} the name)";
			}
			else
			{
				fFind(pOFound, pORow, false, "a brace in a string that is no piece of a frame: the game shows it as written");

				return;
			}

			foreach (UWMessageFormat.Problem lOProblem in UWMessageFormat.Check(lsText, liArguments, psPluralCode))
				fFind(pOFound, pORow, lOProblem.IsError, "slots of " + lsWhat + ": " + lOProblem.Message);
		}

		/// <summary>A frame of the language's own texts that leaves an argument of the English to its
		/// fragments: every fragment of it must name the argument.</summary>
		private void fValidateFrames(List<Finding> pOFound, UWStrings pOEnglish, Func<string, string> pFrame)
		{
			if (pFrame == null)
				return;

			List<string> lsFrames = new List<string>();

			foreach (UWTextFragments.Use lOUse in UWTextFragments.Uses)
			{
				if (!lsFrames.Contains(lOUse.Frame))
					lsFrames.Add(lOUse.Frame);
			}

			foreach (string lsFrame in lsFrames)
			{
				string lsLeft = UWTextLanguageFile.LeftOut(lsFrame, pFrame(lsFrame));

				if (lsLeft.Length == 0)
					continue;

				foreach (UWTextFragments.Use lOUse in UWTextFragments.Of(lsFrame))
				{
					for (int liIndex = lOUse.First; liIndex <= lOUse.Last; liIndex++)
					{
						Row lORow = fLastRow(lOUse.Block, liIndex);
						string lsText = lORow != null ? lORow.Text : UWText.Block(pOEnglish, lOUse.Block, liIndex, null);

						if (lsText == null)
							continue;

						SortedSet<int> lONamed = UWMessageFormat.Arguments(lsText);

						foreach (string lsNumber in lsLeft.Split(','))
						{
							if (lONamed.Contains(int.Parse(lsNumber, CultureInfo.InvariantCulture)))
								continue;

							string lsKey = lOUse.Block.ToString(CultureInfo.InvariantCulture) + ":" + liIndex.ToString(CultureInfo.InvariantCulture);

							pOFound.Add(new Finding
							{
								Line = lORow != null ? lORow.Line : 0,
								Key = lsKey,
								IsError = true,
								Message = "the frame " + lsFrame + " leaves {" + lsNumber + "} to its pieces (" + lOUse.Arguments + "), this one does not name it"
									+ (lORow == null ? " (the pack leaves it English)" : string.Empty)
							});
						}
					}
				}
			}
		}

		/// <summary>The row that counts for a string (the later of two), or null.</summary>
		private Row fLastRow(int piBlock, int piIndex)
		{
			Row lOFound = null;

			foreach (Row lORow in Rows)
			{
				if (lORow.Block == piBlock && lORow.Index == piIndex)
					lOFound = lORow;
			}

			return lOFound;
		}

		private static void fFind(List<Finding> pOFound, Row pORow, bool pbError, string psMessage)
		{
			pOFound.Add(new Finding { Line = pORow.Line, Key = pORow.Key, IsError = pbError, Message = psMessage });
		}

		private static int fCount(string psText, char pcWanted)
		{
			int liCount = 0;

			foreach (char lcChar in psText)
			{
				if (lcChar == pcWanted)
					liCount++;
			}

			return liCount;
		}

		private static bool fStartsBlank(string psText)
		{
			return psText.Length > 0 && psText[0] == ' ';
		}

		private static bool fEndsBlank(string psText)
		{
			string lsText = psText.TrimEnd('\n');

			return lsText.Length > 0 && lsText[lsText.Length - 1] == ' ';
		}
	}
}
