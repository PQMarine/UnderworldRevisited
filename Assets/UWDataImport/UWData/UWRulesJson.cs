using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE RULE SET AS A FILE (cleanup phase 10, 2026-10-25): rules.json holds flat dotted keys and
	/// only the ones to change -
	///
	///     { "magic.manaPerCircle": 2, "combat.skillCheck.rollRange": 41 }
	///
	/// Apply takes such a text onto a UWRules; Write gives the whole set back, with a one-line
	/// meaning above each key when asked (the template of `uwdump rules-template`).
	///
	/// FORGIVING WHERE IT CAN BE: an unknown key, a value of the wrong kind or below the rule's
	/// minimum is reported and skipped, the other keys still count; a file that is no JSON at all is
	/// reported and nothing of it is taken. Comments (// to the end of the line, /* ... */) and a
	/// comma after the last entry are accepted, so the template is a valid file as it stands.
	///
	/// Own parser, no engine and no library: whole numbers, true and false, and lists of whole
	/// numbers are all a rule holds, which also keeps the format simple enough for a C++ port to
	/// read or to generate a header from.
	/// </summary>
	public static class UWRulesJson
	{
		/// <summary>The player's file, beside settings.json (the host knows that folder).</summary>
		public const string FileName = "rules.json";

		// ------------------------------------------------- Reading

		/// <summary>Applies the keys of psJson to pORules; psSource names the text in the report.</summary>
		public static UWRulesReport Apply(UWRules pORules, string psJson, string psSource = FileName)
		{
			UWRulesReport lOReport = new UWRulesReport { Source = psSource, Found = true };
			List<KeyValuePair<string, object>> lOPairs;

			try
			{
				lOPairs = new Parser(psJson ?? string.Empty).ReadObject();
			}
			catch (FormatException lOError)
			{
				lOReport.Error = lOError.Message;

				return lOReport;
			}

			HashSet<string> lOSeen = new HashSet<string>(StringComparer.Ordinal);

			foreach (KeyValuePair<string, object> lOPair in lOPairs)
			{
				UWRuleEntry lOEntry = UWRules.Find(lOPair.Key);

				if (lOEntry == null)
				{
					lOReport.Problems.Add(fUnknown(lOPair.Key));

					continue;
				}

				if (!lOSeen.Add(lOPair.Key))
					lOReport.Problems.Add(lOPair.Key + ": given twice, the last one counts");

				string lsWhy = fTake(pORules, lOEntry, lOPair.Value);

				if (lsWhy != null)
					lOReport.Problems.Add(lOPair.Key + ": " + lsWhy + " - the default stays");
				else if (!lOReport.Applied.Contains(lOPair.Key))
					lOReport.Applied.Add(lOPair.Key);
			}

			return lOReport;
		}

		/// <summary>Applies the file at psPath to pORules. No file (or no path) is no error: the
		/// report says so (Found false) and the rules stay as they are.</summary>
		public static UWRulesReport ApplyFile(UWRules pORules, string psPath)
		{
			if (string.IsNullOrEmpty(psPath) || !File.Exists(psPath))
				return new UWRulesReport { Source = psPath, Found = false };

			string lsText;

			try
			{
				lsText = File.ReadAllText(psPath);
			}
			catch (Exception lOError)
			{
				return new UWRulesReport { Source = psPath, Found = true, Error = "not readable: " + lOError.Message };
			}

			return Apply(pORules, lsText, psPath);
		}

		private static string fUnknown(string psKey)
		{
			foreach (UWRuleEntry lOEntry in UWRules.Entries)
			{
				if (string.Equals(lOEntry.Key, psKey, StringComparison.OrdinalIgnoreCase))
					return psKey + ": unknown key (did you mean " + lOEntry.Key + "?)";
			}

			return psKey + ": unknown key";
		}

		/// <summary>Sets one rule, or says why not (null when taken).</summary>
		private static string fTake(UWRules pORules, UWRuleEntry pOEntry, object pOValue)
		{
			if (pOEntry.ValueType == typeof(bool))
			{
				if (!(pOValue is bool lbValue))
					return "wants true or false";

				pOEntry.SetValue(pORules, lbValue);

				return null;
			}

			if (pOEntry.ValueType == typeof(int))
			{
				string lsWhy = fToInt(pOValue, pOEntry.Minimum, pOEntry.Maximum, out int liValue);

				if (lsWhy != null)
					return lsWhy;

				pOEntry.SetValue(pORules, liValue);

				return null;
			}

			if (pOEntry.ValueType == typeof(int[]))
			{
				int[] liDefault = (int[])pOEntry.GetValue(pORules);

				if (!(pOValue is List<object> lOList))
					return "wants a list of " + liDefault.Length + " whole numbers";

				if (lOList.Count != liDefault.Length)
					return "wants " + liDefault.Length + " numbers, not " + lOList.Count;

				int[] liTable = new int[lOList.Count];

				for (int liAt = 0; liAt < lOList.Count; liAt++)
				{
					string lsWhy = fToInt(lOList[liAt], pOEntry.Minimum, pOEntry.Maximum, out liTable[liAt]);

					if (lsWhy != null)
						return "entry " + liAt + " " + lsWhy;
				}

				pOEntry.SetValue(pORules, liTable);

				return null;
			}

			return "has a kind of value the file cannot set";
		}

		private static string fToInt(object pOValue, int piMinimum, int piMaximum, out int piValue)
		{
			piValue = 0;

			if (pOValue is NotWhole)
				return "wants a whole number";

			if (!(pOValue is long llValue))
				return "wants a whole number";

			if (llValue < int.MinValue || llValue > int.MaxValue)
				return "is out of range";

			if (llValue < piMinimum)
				return "must be at least " + piMinimum.ToString(CultureInfo.InvariantCulture);

			if (llValue > piMaximum)
				return "must be at most " + piMaximum.ToString(CultureInfo.InvariantCulture);

			piValue = (int)llValue;

			return null;
		}

		// ------------------------------------------------- Writing

		/// <summary>
		/// The whole set, one key per line in ordinal order, LF line ends. With pbMeanings every key
		/// gets its one-line meaning as a comment above it and the file a header - the template a
		/// player copies the lines he wants from; without, it is the plain JSON the self-check pins
		/// (Pins/rules-original.json).
		/// </summary>
		public static string Write(UWRules pORules, bool pbMeanings)
		{
			StringBuilder lOText = new StringBuilder();
			IReadOnlyList<UWRuleEntry> lOEntries = UWRules.Entries;

			if (pbMeanings)
			{
				lOText.Append("// The rule set of Underworld Revisited at the original's values (uwdump rules-template).\n");
				lOText.Append("// To change a rule, put its line into rules.json beside settings.json; a key left out\n");
				lOText.Append("// keeps the original's value. The file is read when a game starts or is loaded.\n");
			}

			lOText.Append("{\n");

			for (int liAt = 0; liAt < lOEntries.Count; liAt++)
			{
				UWRuleEntry lOEntry = lOEntries[liAt];

				if (pbMeanings)
					lOText.Append("  // ").Append(lOEntry.Meaning).Append('\n');

				lOText.Append("  \"").Append(lOEntry.Key).Append("\": ").Append(fFormat(lOEntry.GetValue(pORules)));

				if (liAt < lOEntries.Count - 1)
					lOText.Append(',');

				lOText.Append('\n');
			}

			return lOText.Append("}\n").ToString();
		}

		private static string fFormat(object pOValue)
		{
			if (pOValue is bool lbValue)
				return lbValue ? "true" : "false";

			if (pOValue is int[] liTable)
			{
				StringBuilder lOText = new StringBuilder("[");

				for (int liAt = 0; liAt < liTable.Length; liAt++)
				{
					if (liAt > 0)
						lOText.Append(", ");

					lOText.Append(liTable[liAt].ToString(CultureInfo.InvariantCulture));
				}

				return lOText.Append(']').ToString();
			}

			return ((int)pOValue).ToString(CultureInfo.InvariantCulture);
		}

		// ------------------------------------------------- The parser

		/// <summary>A number with a fraction or an exponent - read, but no rule takes it.</summary>
		private sealed class NotWhole
		{
		}

		/// <summary>
		/// One JSON object of the shape a rule file has. Values come out as long (a whole number),
		/// NotWhole, bool, string, null or List&lt;object&gt;; a nested object is read and handed on
		/// as a dictionary only so that it can be reported as the wrong kind. Errors name the line.
		/// </summary>
		private sealed class Parser
		{
			private readonly string msText;

			private int miAt;

			public Parser(string psText)
			{
				msText = psText;
			}

			public List<KeyValuePair<string, object>> ReadObject()
			{
				fSkip();

				// A byte order mark is no error.
				if (miAt < msText.Length && msText[miAt] == '﻿')
				{
					miAt++;
					fSkip();
				}

				List<KeyValuePair<string, object>> lOPairs = fObject();

				fSkip();

				if (miAt < msText.Length)
					throw fError("unexpected text after the closing }");

				return lOPairs;
			}

			private List<KeyValuePair<string, object>> fObject()
			{
				fExpect('{');

				List<KeyValuePair<string, object>> lOPairs = new List<KeyValuePair<string, object>>();

				while (true)
				{
					fSkip();

					if (fPeek() == '}')
					{
						miAt++;

						return lOPairs;
					}

					if (fPeek() != '"')
						throw fError("expected a key in double quotes or }");

					string lsKey = fString();

					fSkip();
					fExpect(':');
					fSkip();

					lOPairs.Add(new KeyValuePair<string, object>(lsKey, fValue()));

					fSkip();

					if (fPeek() == ',')
					{
						miAt++;

						continue;
					}

					if (fPeek() != '}')
						throw fError("expected , or } after the value of " + lsKey);
				}
			}

			private object fValue()
			{
				char lcAt = fPeek();

				switch (lcAt)
				{
					case '"':
						return fString();

					case '[':
						return fList();

					case '{':
						return fObject();

					case 't':
						fWord("true");

						return true;

					case 'f':
						fWord("false");

						return false;

					case 'n':
						fWord("null");

						return null;
				}

				if (lcAt == '-' || (lcAt >= '0' && lcAt <= '9'))
					return fNumber();

				throw fError("expected a value");
			}

			private List<object> fList()
			{
				fExpect('[');

				List<object> lOItems = new List<object>();

				while (true)
				{
					fSkip();

					if (fPeek() == ']')
					{
						miAt++;

						return lOItems;
					}

					lOItems.Add(fValue());
					fSkip();

					if (fPeek() == ',')
					{
						miAt++;

						continue;
					}

					if (fPeek() != ']')
						throw fError("expected , or ] in a list");
				}
			}

			private object fNumber()
			{
				int liStart = miAt;

				if (fPeek() == '-')
					miAt++;

				while (miAt < msText.Length && char.IsDigit(msText[miAt]))
					miAt++;

				bool lbWhole = true;

				while (miAt < msText.Length && (msText[miAt] == '.' || msText[miAt] == 'e' || msText[miAt] == 'E'
					|| msText[miAt] == '+' || msText[miAt] == '-' || char.IsDigit(msText[miAt])))
				{
					lbWhole = false;
					miAt++;
				}

				string lsNumber = msText.Substring(liStart, miAt - liStart);

				if (lsNumber == "-")
					throw fError("expected a number");

				if (!lbWhole)
					return new NotWhole();

				// Too long for a long is out of any rule's range as well.
				return long.TryParse(lsNumber, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long llValue)
					? llValue
					: (object)(lsNumber[0] == '-' ? long.MinValue : long.MaxValue);
			}

			private string fString()
			{
				fExpect('"');

				StringBuilder lOText = new StringBuilder();

				while (true)
				{
					if (miAt >= msText.Length || msText[miAt] == '\n')
						throw fError("a text in double quotes is not closed");

					char lcAt = msText[miAt++];

					if (lcAt == '"')
						return lOText.ToString();

					if (lcAt != '\\')
					{
						lOText.Append(lcAt);

						continue;
					}

					if (miAt >= msText.Length)
						throw fError("a text in double quotes is not closed");

					char lcEscaped = msText[miAt++];

					switch (lcEscaped)
					{
						case 'n': lOText.Append('\n'); break;
						case 't': lOText.Append('\t'); break;
						case 'r': lOText.Append('\r'); break;
						case 'b': lOText.Append('\b'); break;
						case 'f': lOText.Append('\f'); break;

						case 'u':
							if (miAt + 4 > msText.Length
								|| !int.TryParse(msText.Substring(miAt, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int liCode))
								throw fError("a \\u escape wants four hexadecimal digits");

							lOText.Append((char)liCode);
							miAt += 4;
							break;

						default: lOText.Append(lcEscaped); break;
					}
				}
			}

			private void fWord(string psWord)
			{
				if (string.CompareOrdinal(msText, miAt, psWord, 0, psWord.Length) != 0)
					throw fError("expected a value");

				miAt += psWord.Length;
			}

			private void fExpect(char pcWanted)
			{
				if (fPeek() != pcWanted)
					throw fError("expected " + pcWanted);

				miAt++;
			}

			private char fPeek()
			{
				return miAt < msText.Length ? msText[miAt] : '\0';
			}

			/// <summary>White space and comments.</summary>
			private void fSkip()
			{
				while (miAt < msText.Length)
				{
					char lcAt = msText[miAt];

					if (char.IsWhiteSpace(lcAt))
					{
						miAt++;

						continue;
					}

					if (lcAt == '/' && miAt + 1 < msText.Length && msText[miAt + 1] == '/')
					{
						while (miAt < msText.Length && msText[miAt] != '\n')
							miAt++;

						continue;
					}

					if (lcAt == '/' && miAt + 1 < msText.Length && msText[miAt + 1] == '*')
					{
						int liEnd = msText.IndexOf("*/", miAt + 2, StringComparison.Ordinal);

						if (liEnd < 0)
							throw fError("a /* comment is not closed");

						miAt = liEnd + 2;

						continue;
					}

					return;
				}
			}

			private FormatException fError(string psWhat)
			{
				int liLine = 1;

				for (int liAt = 0; liAt < miAt && liAt < msText.Length; liAt++)
				{
					if (msText[liAt] == '\n')
						liLine++;
				}

				return new FormatException(miAt >= msText.Length
					? "the file ends too early (" + psWhat + ")"
					: "line " + liLine.ToString(CultureInfo.InvariantCulture) + ": " + psWhat);
			}
		}
	}

	/// <summary>What applying a rules.json found (UWRulesJson.Apply, UWGameSession.RulesReport).</summary>
	public sealed class UWRulesReport
	{
		/// <summary>The file's path, or what the text was called.</summary>
		public string Source;

		/// <summary>False: there was no file, the original's rules stand.</summary>
		public bool Found;

		/// <summary>Why nothing of the file was taken (not readable, no JSON); null when it was read.</summary>
		public string Error;

		/// <summary>The keys taken, in the file's order.</summary>
		public readonly List<string> Applied = new List<string>();

		/// <summary>The keys skipped or doubtful, each with the reason, in the file's order.</summary>
		public readonly List<string> Problems = new List<string>();

		public bool HasProblems => Error != null || Problems.Count > 0;

		/// <summary>One line for the log and the debug panel.</summary>
		public string Summary
		{
			get
			{
				if (!Found)
					return "the original's rules (no " + UWRulesJson.FileName + ")";

				if (Error != null)
					return UWRulesJson.FileName + " not used, the original's rules: " + Error;

				string lsText = UWRulesJson.FileName + ": " + Applied.Count + (Applied.Count == 1 ? " rule" : " rules") + " taken";

				return Problems.Count == 0 ? lsText : lsText + ", " + Problems.Count + (Problems.Count == 1 ? " problem" : " problems");
			}
		}
	}
}
