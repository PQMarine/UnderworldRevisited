using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PORT'S OWN TEXTS (cleanup step 8.5, 2026-10-24; Docs/CLEANUP-PLAN.md 8.7): every word the
	/// port itself shows - the setup bar and its dialogs, the help window, the layout editor, the key
	/// and button names, the effects screen, the letter grid, the modern game menu and the modern
	/// interface's own words - by a stable id ("setup.bar.motion", "help.tab.spells"). T(id) gives the
	/// text, F(id, ...) the text with its {0} {1} filled in. The game's own strings (STRINGS.PAK,
	/// conversations, object names) are not here: they stay in the player's data (the lookups above).
	///
	/// WHY IDS AND NOT THE ENGLISH AS THE KEY: a word like "Look" or "Open" means different things in
	/// different places and a language may need two words for it; an id names the place. The
	/// English text is the table built in here (UWTextEnglish, C# source, so it is compiled and
	/// checked with the code); it is the default and the fallback for every id.
	///
	/// A LANGUAGE FILE (layer 1 of the translation plan in Todo.md, "Translations") replaces texts id
	/// by id. Its format, kept small on purpose so a translator needs nothing but a text editor or a
	/// spreadsheet:
	///   - UTF-8 (a byte order mark is allowed), one text per line: the id, one TAB, the text.
	///   - Empty lines and lines starting with # are skipped.
	///   - In the text \n stands for a line break, \t for a TAB, \\ for a backslash.
	///   - An id the table does not know, a line without a TAB, and a text whose {0} {1} placeholders
	///     differ from the English one are reported and skipped - that id stays English.
	///   - An id the file does not hold stays English. Nothing in a file can make the game fail.
	/// The Unity side (UWLanguage) decides which file is read and when: once at startup, before any
	/// window is built, so texts taken when a window is built are the language's as well.
	///
	/// THE PSEUDO-LANGUAGE (UsePseudo) is a test language built from the English: every vowel doubled,
	/// the text in brackets and padded with ~ to at least 40 % more length. It shows at a glance a
	/// text that does not come through the table (no brackets) and where a longer language clips.
	/// </summary>
	public static partial class UWText
	{
		/// <summary>The name UsePseudo gives the language.</summary>
		public const string PseudoLanguage = "pseudo";

		/// <summary>The texts of the language in force, by id; null while English is in force.</summary>
		private static Dictionary<string, string> msLanguage;

		/// <summary>"English", PseudoLanguage, or the name a language file was loaded under.</summary>
		public static string LanguageName { get; private set; } = "English";

		/// <summary>Counts the changes of language, for whoever keeps texts it took (the setting
		/// rows' choice texts).</summary>
		public static int LanguageVersion { get; private set; }

		/// <summary>The text of an id: the language's where it has one, else the English. An id the
		/// table does not know comes back as itself, so a missing entry shows on screen instead of a
		/// blank (the self-check finds every such id in the code).</summary>
		public static string T(string psId)
		{
			if (psId == null)
				return string.Empty;

			if (msLanguage != null && msLanguage.TryGetValue(psId, out string lsText))
				return lsText;

			if (UWTextEnglish.Table.TryGetValue(psId, out lsText))
				return lsText;

			return psId;
		}

		/// <summary>The text of an id with its placeholders filled in (string.Format, as the code
		/// wrote it before). A language's text that does not format falls back to the English one;
		/// never throws.</summary>
		public static string F(string psId, params object[] paArgs)
		{
			string lsFormat = T(psId);

			try
			{
				return string.Format(lsFormat, paArgs);
			}
			catch (FormatException)
			{
			}

			if (UWTextEnglish.Table.TryGetValue(psId ?? string.Empty, out string lsEnglish))
			{
				try
				{
					return string.Format(lsEnglish, paArgs);
				}
				catch (FormatException)
				{
				}
			}

			return lsFormat;
		}

		/// <summary>Whether the table knows the id.</summary>
		public static bool HasText(string psId)
		{
			return psId != null && UWTextEnglish.Table.ContainsKey(psId);
		}

		/// <summary>The English text of an id, whatever language is in force (null if unknown).</summary>
		public static string English(string psId)
		{
			return psId != null && UWTextEnglish.Table.TryGetValue(psId, out string lsText) ? lsText : null;
		}

		/// <summary>Every id of the table, in the table's order.</summary>
		public static IReadOnlyList<string> TextIds => UWTextEnglish.Order;

		/// <summary>Ids the English table gives twice (the self-check wants none).</summary>
		public static IReadOnlyList<string> TextIdsGivenTwice => UWTextEnglish.Twice;

		// ------------------------------------------------- Choosing the language

		/// <summary>English again.</summary>
		public static void UseEnglish()
		{
			msLanguage = null;
			LanguageName = "English";
			LanguageVersion++;
		}

		/// <summary>The pseudo-language: every text lengthened and bracketed (see the class).</summary>
		public static void UsePseudo()
		{
			Dictionary<string, string> lOTexts = new Dictionary<string, string>(UWTextEnglish.Table.Count);

			foreach (KeyValuePair<string, string> lOPair in UWTextEnglish.Table)
				lOTexts[lOPair.Key] = Pseudo(lOPair.Value);

			msLanguage = lOTexts;
			LanguageName = PseudoLanguage;
			LanguageVersion++;
		}

		/// <summary>Puts a parsed language file in force under a name; its skipped lines stay English.</summary>
		public static void UseLanguage(string psName, UWTextLanguageFile pOFile)
		{
			if (pOFile == null)
			{
				UseEnglish();

				return;
			}

			msLanguage = new Dictionary<string, string>(pOFile.Texts);
			LanguageName = psName;
			LanguageVersion++;
		}

		/// <summary>Reads a language file and puts it in force; English stays when the file cannot be
		/// read. Returns the parsed file (null when unreadable) so the caller can log its problems.</summary>
		public static UWTextLanguageFile LoadLanguage(string psName, string psPath)
		{
			UWTextLanguageFile lOFile;

			try
			{
				lOFile = UWTextLanguageFile.Parse(File.ReadAllText(psPath, Encoding.UTF8));
			}
			catch (Exception)
			{
				UseEnglish();

				return null;
			}

			UseLanguage(psName, lOFile);

			return lOFile;
		}

		// ------------------------------------------------- The pseudo-language

		/// <summary>The pseudo form of a text: "[Saavee gaamee~~]". Placeholders ({0}), rich text tags
		/// (&lt;b&gt;) and line breaks stay as they are; at least 40 % longer than the English.</summary>
		public static string Pseudo(string psEnglish)
		{
			if (string.IsNullOrEmpty(psEnglish))
				return "[]";

			StringBuilder lOText = new StringBuilder(psEnglish.Length * 2 + 4);
			int liShown = 0;
			int liAt = 0;

			lOText.Append('[');

			while (liAt < psEnglish.Length)
			{
				char lcChar = psEnglish[liAt];
				int liSkip = fSkipMarkup(psEnglish, liAt);

				if (liSkip > liAt)
				{
					lOText.Append(psEnglish, liAt, liSkip - liAt);
					liAt = liSkip;

					continue;
				}

				lOText.Append(lcChar);
				liShown++;

				if ("aeiouAEIOU".IndexOf(lcChar) >= 0)
					lOText.Append(lcChar);

				liAt++;
			}

			// The brackets count; the doubled vowels too; pad the rest with ~.
			int liWanted = (liShown * 14 + 9) / 10 + 2;
			int liHave = lOText.Length + 1 - (psEnglish.Length - liShown);

			while (liHave < liWanted)
			{
				lOText.Append('~');
				liHave++;
			}

			lOText.Append(']');

			return lOText.ToString();
		}

		/// <summary>Where a placeholder ({0}, {1,5}, {0:0.0}) or a rich text tag (&lt;b&gt;,
		/// &lt;/color&gt;, &lt;color=#AABBCC&gt;) starting at the index ends; the index itself where
		/// none starts there.</summary>
		private static int fSkipMarkup(string psText, int piAt)
		{
			char lcChar = psText[piAt];

			if (lcChar == '{')
			{
				int liEnd = psText.IndexOf('}', piAt);

				return liEnd > piAt + 1 && char.IsDigit(psText[piAt + 1]) ? liEnd + 1 : piAt;
			}

			if (lcChar == '<')
			{
				int liEnd = psText.IndexOf('>', piAt);

				if (liEnd < 0)
					return piAt;

				string lsTag = psText.Substring(piAt + 1, liEnd - piAt - 1).TrimStart('/');

				foreach (string lsKnown in msRichTags)
				{
					if (lsTag == lsKnown || lsTag.StartsWith(lsKnown + "="))
						return liEnd + 1;
				}

				return piAt;
			}

			if (lcChar == '\n' || lcChar == '\r' || lcChar == '\t')
				return piAt + 1;

			return piAt;
		}

		private static readonly string[] msRichTags = { "b", "i", "color", "size" };

		// ------------------------------------------------- Placeholders

		/// <summary>How many arguments a format text takes: its highest {n} plus one (0 for none).
		/// Doubled braces ({{ }}) are no placeholder.</summary>
		public static int PlaceholderCount(string psFormat)
		{
			int liCount = 0;

			if (psFormat == null)
				return 0;

			for (int liAt = 0; liAt < psFormat.Length; liAt++)
			{
				if (psFormat[liAt] != '{')
					continue;

				if (liAt + 1 < psFormat.Length && psFormat[liAt + 1] == '{')
				{
					liAt++;

					continue;
				}

				int liNumber = 0;
				int liDigit = liAt + 1;

				while (liDigit < psFormat.Length && char.IsDigit(psFormat[liDigit]))
				{
					liNumber = liNumber * 10 + (psFormat[liDigit] - '0');
					liDigit++;
				}

				if (liDigit > liAt + 1)
					liCount = Math.Max(liCount, liNumber + 1);
			}

			return liCount;
		}

		/// <summary>The placeholders of a format text as a set, sorted ("0,1"): two texts that fill in
		/// the same arguments have the same set.</summary>
		public static string PlaceholderSet(string psFormat)
		{
			SortedSet<int> lOFound = new SortedSet<int>();

			if (psFormat != null)
			{
				for (int liAt = 0; liAt < psFormat.Length; liAt++)
				{
					if (psFormat[liAt] != '{')
						continue;

					if (liAt + 1 < psFormat.Length && psFormat[liAt + 1] == '{')
					{
						liAt++;

						continue;
					}

					int liNumber = 0;
					int liDigit = liAt + 1;

					while (liDigit < psFormat.Length && char.IsDigit(psFormat[liDigit]))
					{
						liNumber = liNumber * 10 + (psFormat[liDigit] - '0');
						liDigit++;
					}

					if (liDigit > liAt + 1)
						lOFound.Add(liNumber);
				}
			}

			return string.Join(",", lOFound);
		}
	}

	/// <summary>
	/// A LANGUAGE FILE, parsed (cleanup step 8.5, 2026-10-24): the texts it gives by id and what was
	/// wrong with the lines it skipped. The format is described at UWText. Write gives the same
	/// format back, so the English table written out is a template for a translator.
	/// </summary>
	public sealed class UWTextLanguageFile
	{
		public readonly Dictionary<string, string> Texts = new Dictionary<string, string>();

		/// <summary>One line per skipped or doubtful line: "line 12: unknown id x.y".</summary>
		public readonly List<string> Problems = new List<string>();

		public static UWTextLanguageFile Parse(string psContent)
		{
			UWTextLanguageFile lOFile = new UWTextLanguageFile();

			if (string.IsNullOrEmpty(psContent))
				return lOFile;

			string[] lsLines = psContent.TrimStart('\uFEFF').Split('\n');

			for (int liLine = 0; liLine < lsLines.Length; liLine++)
			{
				string lsLine = lsLines[liLine].TrimEnd('\r');

				if (lsLine.Length == 0 || lsLine[0] == '#')
					continue;

				int liTab = lsLine.IndexOf('\t');

				if (liTab <= 0)
				{
					lOFile.Problems.Add("line " + (liLine + 1) + ": no id and TAB");

					continue;
				}

				string lsId = lsLine.Substring(0, liTab).Trim();
				string lsText = Unescape(lsLine.Substring(liTab + 1));
				string lsEnglish = UWText.English(lsId);

				if (lsEnglish == null)
				{
					lOFile.Problems.Add("line " + (liLine + 1) + ": unknown id " + lsId);

					continue;
				}

				if (UWText.PlaceholderSet(lsText) != UWText.PlaceholderSet(lsEnglish))
				{
					lOFile.Problems.Add("line " + (liLine + 1) + ": " + lsId + " has other placeholders than the English");

					continue;
				}

				if (lOFile.Texts.ContainsKey(lsId))
					lOFile.Problems.Add("line " + (liLine + 1) + ": " + lsId + " again, the later text counts");

				lOFile.Texts[lsId] = lsText;
			}

			return lOFile;
		}

		/// <summary>Texts as file lines, in the given order of ids (ids without a text left out).</summary>
		public static string Write(IEnumerable<string> pOIds, Func<string, string> pOText)
		{
			StringBuilder lOFile = new StringBuilder();

			foreach (string lsId in pOIds)
			{
				string lsText = pOText(lsId);

				if (lsText != null)
					lOFile.Append(lsId).Append('\t').Append(Escape(lsText)).Append('\n');
			}

			return lOFile.ToString();
		}

		public static string Escape(string psText)
		{
			return psText.Replace("\\", "\\\\").Replace("\r", string.Empty).Replace("\n", "\\n").Replace("\t", "\\t");
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

				char lcNext = psText[++liAt];

				if (lcNext == 'n')
					lOText.Append('\n');
				else if (lcNext == 't')
					lOText.Append('\t');
				else if (lcNext == '\\')
					lOText.Append('\\');
				else
					lOText.Append('\\').Append(lcNext);
			}

			return lOText.ToString();
		}
	}
}
