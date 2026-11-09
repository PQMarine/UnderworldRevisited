using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A SMALL JSON READER for the files mods bring (mod.json of ours and of the editor's .uwmod packages;
	/// 2026-11-09, the mod folder's first step). Objects come out as Dictionary&lt;string, object&gt; (the
	/// keys as written, the last of a repeated key), arrays as List&lt;object&gt;, numbers as double, then
	/// string, bool and null. A byte order mark is skipped; an error names the line. The rule set keeps its
	/// own reader (UWRulesJson), which reports a rule file's mistakes key by key.
	/// </summary>
	public static class UWJsonReader
	{
		/// <summary>The value of a JSON text; throws FormatException where it is not JSON.</summary>
		public static object Parse(string psText)
		{
			Reader lOReader = new Reader(psText ?? string.Empty);

			return lOReader.ReadDocument();
		}

		/// <summary>The object of a JSON text, or null with the reason where it is not JSON or not an object.</summary>
		public static Dictionary<string, object> TryParseObject(string psText, out string psError)
		{
			psError = null;

			try
			{
				if (Parse(psText) is Dictionary<string, object> lOObject)
					return lOObject;

				psError = "not a JSON object";
			}
			catch (FormatException lOError)
			{
				psError = lOError.Message;
			}

			return null;
		}

		// ------------------------------------------------- Reading values out

		public static string GetString(Dictionary<string, object> pOObject, string psKey)
		{
			return pOObject != null && pOObject.TryGetValue(psKey, out object lOValue) ? lOValue as string : null;
		}

		public static List<object> GetList(Dictionary<string, object> pOObject, string psKey)
		{
			return pOObject != null && pOObject.TryGetValue(psKey, out object lOValue) ? lOValue as List<object> : null;
		}

		public static Dictionary<string, object> GetObject(Dictionary<string, object> pOObject, string psKey)
		{
			return pOObject != null && pOObject.TryGetValue(psKey, out object lOValue) ? lOValue as Dictionary<string, object> : null;
		}

		/// <summary>A number, or pdDefault where there is none.</summary>
		public static double GetNumber(Dictionary<string, object> pOObject, string psKey, double pdDefault)
		{
			return pOObject != null && pOObject.TryGetValue(psKey, out object lOValue) && lOValue is double ldValue ? ldValue : pdDefault;
		}

		/// <summary>The strings of a list (others left out), or of a single string; empty where there is none.</summary>
		public static List<string> GetStrings(Dictionary<string, object> pOObject, string psKey)
		{
			List<string> lsResult = new List<string>();

			if (pOObject == null || !pOObject.TryGetValue(psKey, out object lOValue))
				return lsResult;

			if (lOValue is string lsOne)
				lsResult.Add(lsOne);
			else if (lOValue is List<object> lOList)
			{
				foreach (object lOItem in lOList)
				{
					if (lOItem is string lsItem)
						lsResult.Add(lsItem);
				}
			}

			return lsResult;
		}

		// ------------------------------------------------- The reader

		private sealed class Reader
		{
			private readonly string msText;

			private int miAt;

			public Reader(string psText)
			{
				msText = psText;
			}

			public object ReadDocument()
			{
				if (miAt < msText.Length && msText[miAt] == '﻿')
					miAt++;

				fSkip();

				object lOValue = fValue();

				fSkip();

				if (miAt < msText.Length)
					throw fError("unexpected text after the value");

				return lOValue;
			}

			private object fValue()
			{
				fSkip();

				if (miAt >= msText.Length)
					throw fError("unexpected end");

				char lcChar = msText[miAt];

				switch (lcChar)
				{
					case '{':
						return fObject();

					case '[':
						return fArray();

					case '"':
						return fString();

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

				if (lcChar == '-' || (lcChar >= '0' && lcChar <= '9'))
					return fNumber();

				throw fError("unexpected '" + lcChar + "'");
			}

			private Dictionary<string, object> fObject()
			{
				miAt++;

				Dictionary<string, object> lOObject = new Dictionary<string, object>();

				fSkip();

				if (fPeek() == '}')
				{
					miAt++;

					return lOObject;
				}

				while (true)
				{
					fSkip();

					if (fPeek() != '"')
						throw fError("expected a key in double quotes");

					string lsKey = fString();

					fSkip();

					if (fPeek() != ':')
						throw fError("expected ':'");

					miAt++;
					lOObject[lsKey] = fValue();
					fSkip();

					char lcNext = fPeek();

					miAt++;

					if (lcNext == '}')
						return lOObject;

					if (lcNext != ',')
						throw fError("expected ',' or '}'");
				}
			}

			private List<object> fArray()
			{
				miAt++;

				List<object> lOList = new List<object>();

				fSkip();

				if (fPeek() == ']')
				{
					miAt++;

					return lOList;
				}

				while (true)
				{
					lOList.Add(fValue());
					fSkip();

					char lcNext = fPeek();

					miAt++;

					if (lcNext == ']')
						return lOList;

					if (lcNext != ',')
						throw fError("expected ',' or ']'");
				}
			}

			private string fString()
			{
				miAt++;

				StringBuilder lOText = new StringBuilder();

				while (true)
				{
					if (miAt >= msText.Length)
						throw fError("a string without its closing quote");

					char lcChar = msText[miAt++];

					if (lcChar == '"')
						return lOText.ToString();

					if (lcChar != '\\')
					{
						lOText.Append(lcChar);
						continue;
					}

					if (miAt >= msText.Length)
						throw fError("a string without its closing quote");

					char lcEscape = msText[miAt++];

					switch (lcEscape)
					{
						case '"': lOText.Append('"'); break;
						case '\\': lOText.Append('\\'); break;
						case '/': lOText.Append('/'); break;
						case 'b': lOText.Append('\b'); break;
						case 'f': lOText.Append('\f'); break;
						case 'n': lOText.Append('\n'); break;
						case 'r': lOText.Append('\r'); break;
						case 't': lOText.Append('\t'); break;

						case 'u':
							if (miAt + 4 > msText.Length
								|| !int.TryParse(msText.Substring(miAt, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int liCode))
								throw fError("a bad \\u escape");

							lOText.Append((char)liCode);
							miAt += 4;
							break;

						default:
							throw fError("a bad escape \\" + lcEscape);
					}
				}
			}

			private double fNumber()
			{
				int liStart = miAt;

				while (miAt < msText.Length && "+-0123456789.eE".IndexOf(msText[miAt]) >= 0)
					miAt++;

				if (!double.TryParse(msText.Substring(liStart, miAt - liStart), NumberStyles.Float, CultureInfo.InvariantCulture, out double ldValue))
					throw fError("a bad number");

				return ldValue;
			}

			private void fWord(string psWord)
			{
				if (string.CompareOrdinal(msText, miAt, psWord, 0, psWord.Length) != 0)
					throw fError("expected " + psWord);

				miAt += psWord.Length;
			}

			private void fSkip()
			{
				while (miAt < msText.Length && char.IsWhiteSpace(msText[miAt]))
					miAt++;
			}

			private char fPeek()
			{
				return miAt < msText.Length ? msText[miAt] : '\0';
			}

			private FormatException fError(string psWhat)
			{
				int liLine = 1;

				for (int i = 0; i < miAt && i < msText.Length; i++)
				{
					if (msText[i] == '\n')
						liLine++;
				}

				return new FormatException("line " + liLine + ": " + psWhat);
			}
		}
	}
}
