using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A PIECE OF THE GAME'S TEXT USED IN A FRAME (translation wave 4, 2026-10-26): a string of
	/// STRINGS.PAK (block 1's "You have fully repaired the ", the taste's " tasted great.") passed to
	/// a frame as one of its arguments. Its text may hold SLOTS - {n} for the frame's argument n,
	/// select and plural as in a frame - which UWMessageFormat fills with the frame's own arguments,
	/// so a language can put the name inside the fragment ("Halb geflickt hast du {1}"). Only a
	/// fragment is read for slots: a name, a number or anything the player typed is put in as it
	/// stands, braces and all. Without a brace in it (every English string: STRINGS.PAK has none) it
	/// is its text, as the plain string was before.
	/// </summary>
	public sealed class UWTextFragment
	{
		public readonly string Text;

		public UWTextFragment(string psText)
		{
			Text = psText ?? string.Empty;
		}

		/// <summary>Whether the text holds a slot (a brace) at all.</summary>
		public bool HasSlots => Text.IndexOf('{') >= 0;

		public override string ToString()
		{
			return Text;
		}
	}

	/// <summary>
	/// A NAME PASSED TO A FRAME (translation wave 4, 2026-10-26): the name as the grammar wrote it
	/// (Text - what the frame shows), with what a frame's select and plural choose by: its gender
	/// (the name format's letter m, f, n, c or p; null for a name without one - English), the case it
	/// is in (a case key of UWNameFormat: "nom", "acc", "dat" ...; null where it does not matter) and
	/// its count (1 for a single thing, -1 for a plural without one). {0,select,f{...}other{...}} tries "gender.case", then
	/// "gender", then "other"; {0,plural,...} takes the count. Shown, it is its text, so English
	/// frames that only write {0} read as before.
	/// </summary>
	public sealed class UWNamePhrase
	{
		public readonly string Text;

		public readonly string Gender;

		public readonly string Case;

		public readonly int Count;

		public UWNamePhrase(string psText, string psGender, string psCase = null, int piCount = 1)
		{
			Text = psText ?? string.Empty;
			Gender = string.IsNullOrEmpty(psGender) ? null : psGender;
			Case = string.IsNullOrEmpty(psCase) ? null : psCase;
			Count = piCount;
		}

		/// <summary>The keys a select tries for it, the most special first ("other" is tried last by
		/// the select itself).</summary>
		public IEnumerable<string> SelectKeys()
		{
			if (Gender == null)
				yield break;

			if (Case != null)
				yield return Gender + "." + Case;

			yield return Gender;
		}

		public override string ToString()
		{
			return Text;
		}
	}

	/// <summary>
	/// THE FRAMES' FORMAT (translation wave 4, 2026-10-26): string.Format's {0} {1} as UWText.F always
	/// had them, plus a SUBSET OF ICU MessageFormat that translators know, for the frames of
	/// Languages/&lt;code&gt;.tsv and the fragments of &lt;code&gt;.strings.tsv alike:
	///
	///   {n}                              argument n (a fragment's slots filled - UWTextFragment)
	///   {n,select,KEY{...}...other{...}} by argument n: a name (UWNamePhrase) by "gender.case", then its
	///                                    gender (m f n c p), then other; any other argument by its text
	///   {n,plural,=N{...}CAT{...}...other{...}}
	///                                    by the number of argument n (a whole number, a name's count, or
	///                                    a text that reads as one): first an exact =N, then the CLDR
	///                                    category of the language (zero one two few many other,
	///                                    UWPluralRules); inside, # is the number
	///   '{' '}' '#'                      a brace or a # as a character
	/// "other" is required; a branch may hold one more select or plural (one level of nesting) and
	/// any {n}. At the top level of a text {{ and }} stay string.Format's literal braces; inside a
	/// branch a { always opens a placeholder.
	///
	/// THE ENGLISH IS BYTE-EXACT: a text without select or plural whose fragments hold no brace goes
	/// to string.Format exactly as before (NeedsEngine); the English texts and STRINGS.PAK have
	/// neither. Deterministic: no culture beyond what string.Format used, # written in the
	/// invariant culture, branches chosen by their keys in the order written.
	/// </summary>
	public static class UWMessageFormat
	{
		/// <summary>How deep fragments may fill fragments (a fragment naming itself stops here).</summary>
		public const int MaxFragmentDepth = 2;

		/// <summary>How deep select and plural may nest - the outer one and one inside it.</summary>
		public const int MaxNesting = 2;

		private static readonly Regex msExtended = new Regex(@"\{\s*[0-9]+\s*,\s*(select|plural)\s*,", RegexOptions.CultureInvariant);

		/// <summary>Whether a text uses select or plural.</summary>
		public static bool IsExtended(string psPattern)
		{
			return psPattern != null && psPattern.IndexOf(',') >= 0 && msExtended.IsMatch(psPattern);
		}

		/// <summary>Whether a text with these arguments needs more than string.Format: select or plural
		/// in it, or a fragment argument with slots.</summary>
		public static bool NeedsEngine(string psPattern, object[] paArgs)
		{
			if (IsExtended(psPattern))
				return true;

			if (paArgs != null)
			{
				foreach (object lOArg in paArgs)
				{
					if (lOArg is UWTextFragment lOFragment && lOFragment.HasSlots)
						return true;
				}
			}

			return false;
		}

		/// <summary>The text with its arguments filled in; string.Format where that is all it needs.
		/// Throws FormatException for a text it cannot read, as string.Format does.</summary>
		public static string Format(string psPattern, object[] paArgs, string psPluralCode)
		{
			if (!NeedsEngine(psPattern, paArgs))
				return string.Format(psPattern, paArgs);

			return Render(psPattern, paArgs, psPluralCode);
		}

		/// <summary>The text formatted by this reader whatever it holds - for a text string.Format can
		/// read too, the same result (the self-check MessageFormat compares every text of the table).</summary>
		public static string Render(string psPattern, object[] paArgs, string psPluralCode)
		{
			Context lOContext = new Context { Args = paArgs ?? new object[0], Code = psPluralCode };
			StringBuilder lOText = new StringBuilder(psPattern.Length + 32);
			int liAt = 0;

			fPattern(psPattern, ref liAt, lOText, lOContext, false, null, 0, 0);

			return lOText.ToString();
		}

		// ------------------------------------------------- Checking a text

		/// <summary>What Check found.</summary>
		public sealed class Problem
		{
			public bool IsError;

			public string Message;

			public override string ToString()
			{
				return (IsError ? "error: " : "warning: ") + Message;
			}
		}

		/// <summary>
		/// What is wrong with a text: braces that do not pair, a placeholder that is no number, a
		/// select or plural without "other", nesting deeper than one level, a select or plural key
		/// that is no word (a plural's: no CLDR category or =N), an argument at or beyond piArguments
		/// (when known, else -1), and - as a warning - a plural category the language
		/// (psPluralCode, when known) never takes. Empty when the text is fine.
		/// </summary>
		public static List<Problem> Check(string psPattern, int piArguments = -1, string psPluralCode = null)
		{
			Context lOContext = new Context
			{
				Analyse = true,
				Problems = new List<Problem>(),
				Indices = new SortedSet<int>(),
				ArgumentCount = piArguments,
				Code = psPluralCode
			};

			if (string.IsNullOrEmpty(psPattern))
				return lOContext.Problems;

			int liAt = 0;

			try
			{
				fPattern(psPattern, ref liAt, new StringBuilder(), lOContext, false, null, 0, 0);
			}
			catch (FormatException lOError)
			{
				lOContext.Problems.Add(new Problem { IsError = true, Message = lOError.Message });
			}

			return lOContext.Problems;
		}

		/// <summary>The argument numbers a text uses, in branches too, sorted; what can be read of a
		/// text that has a mistake.</summary>
		public static SortedSet<int> Arguments(string psPattern)
		{
			Context lOContext = new Context { Analyse = true, Problems = new List<Problem>(), Indices = new SortedSet<int>(), ArgumentCount = -1 };

			if (string.IsNullOrEmpty(psPattern))
				return lOContext.Indices;

			int liAt = 0;

			try
			{
				fPattern(psPattern, ref liAt, new StringBuilder(), lOContext, false, null, 0, 0);
			}
			catch (FormatException)
			{
			}

			return lOContext.Indices;
		}

		// ------------------------------------------------- The reader

		private sealed class Context
		{
			public object[] Args;

			public string Code;

			public bool Analyse;

			public List<Problem> Problems;

			public SortedSet<int> Indices;

			public int ArgumentCount;
		}

		private static void fAdd(Context pOContext, bool pbError, string psMessage)
		{
			foreach (Problem lOProblem in pOContext.Problems)
			{
				if (lOProblem.Message == psMessage)
					return;
			}

			pOContext.Problems.Add(new Problem { IsError = pbError, Message = psMessage });
		}

		/// <summary>Reads text up to the end (the top level) or to the "}" that closes a branch (not
		/// taken), writing what it reads. psNumber is the innermost plural's number as # shows it.</summary>
		private static void fPattern(string psText, ref int piAt, StringBuilder pOOut, Context pOContext, bool pbBranch,
			string psNumber, int piNesting, int piDepth)
		{
			while (piAt < psText.Length)
			{
				char lcChar = psText[piAt];

				if (lcChar == '\'' && piAt + 2 < psText.Length && "{}#".IndexOf(psText[piAt + 1]) >= 0 && psText[piAt + 2] == '\'')
				{
					pOOut.Append(psText[piAt + 1]);
					piAt += 3;

					continue;
				}

				if (lcChar == '{')
				{
					if (!pbBranch && piAt + 1 < psText.Length && psText[piAt + 1] == '{')
					{
						pOOut.Append('{');
						piAt += 2;

						continue;
					}

					fPlaceholder(psText, ref piAt, pOOut, pOContext, psNumber, piNesting, piDepth);

					continue;
				}

				if (lcChar == '}')
				{
					if (pbBranch)
						return;

					if (piAt + 1 < psText.Length && psText[piAt + 1] == '}')
					{
						pOOut.Append('}');
						piAt += 2;

						continue;
					}

					throw new FormatException("a } that closes nothing at " + piAt);
				}

				if (lcChar == '#' && pbBranch && psNumber != null)
				{
					pOOut.Append(psNumber);
					piAt++;

					continue;
				}

				pOOut.Append(lcChar);
				piAt++;
			}

			if (pbBranch)
				throw new FormatException("a branch is not closed with }");
		}

		private static void fSkipBlanks(string psText, ref int piAt)
		{
			while (piAt < psText.Length && char.IsWhiteSpace(psText[piAt]))
				piAt++;
		}

		/// <summary>A placeholder from its "{" on: {n}, {n,select,...}, {n,plural,...}, or string.Format's
		/// {n,alignment:format}.</summary>
		private static void fPlaceholder(string psText, ref int piAt, StringBuilder pOOut, Context pOContext, string psNumber,
			int piNesting, int piDepth)
		{
			int liStart = piAt;

			piAt++;
			fSkipBlanks(psText, ref piAt);

			int liIndex = 0;
			int liDigits = piAt;

			while (piAt < psText.Length && psText[piAt] >= '0' && psText[piAt] <= '9')
			{
				liIndex = liIndex * 10 + (psText[piAt] - '0');
				piAt++;

				if (liIndex > 1000000)
					throw new FormatException("an argument number too large at " + liStart);
			}

			if (piAt == liDigits)
				throw new FormatException("a { not followed by an argument number at " + liStart);

			fSkipBlanks(psText, ref piAt);

			if (piAt >= psText.Length)
				throw new FormatException("a placeholder not closed at " + liStart);

			fUse(pOContext, liIndex);

			if (psText[piAt] == '}')
			{
				piAt++;
				pOOut.Append(fArgument(pOContext, liIndex, piDepth));

				return;
			}

			if (psText[piAt] == ',')
			{
				int liAfterComma = piAt + 1;
				int liWord = liAfterComma;

				fSkipBlanks(psText, ref liWord);

				int liWordEnd = liWord;

				while (liWordEnd < psText.Length && char.IsLetter(psText[liWordEnd]))
					liWordEnd++;

				string lsKind = psText.Substring(liWord, liWordEnd - liWord);
				int liNext = liWordEnd;

				fSkipBlanks(psText, ref liNext);

				if ((lsKind == "select" || lsKind == "plural") && liNext < psText.Length && psText[liNext] == ',')
				{
					piAt = liNext + 1;
					fChoice(psText, ref piAt, pOOut, pOContext, liIndex, lsKind == "plural", psNumber, piNesting + 1, piDepth, liStart);

					return;
				}
			}

			if (psText[piAt] == ',' || psText[piAt] == ':')
			{
				// string.Format's alignment and format, handed to it as they stand.
				int liClose = psText.IndexOf('}', piAt);

				if (liClose < 0)
					throw new FormatException("a placeholder not closed at " + liStart);

				string lsSpec = psText.Substring(piAt, liClose - piAt);

				piAt = liClose + 1;

				if (!pOContext.Analyse)
					pOOut.Append(string.Format("{0" + lsSpec + "}", fRaw(pOContext, liIndex)));

				return;
			}

			throw new FormatException("a placeholder that is neither {n}, select nor plural at " + liStart);
		}

		/// <summary>Records an argument number (analysis) or checks it exists (formatting).</summary>
		private static void fUse(Context pOContext, int piIndex)
		{
			if (pOContext.Analyse)
			{
				pOContext.Indices.Add(piIndex);

				if (pOContext.ArgumentCount >= 0 && piIndex >= pOContext.ArgumentCount)
					fAdd(pOContext, true, "{" + piIndex + "} names an argument the frame does not have (it has " + pOContext.ArgumentCount + ")");

				return;
			}

			if (piIndex >= pOContext.Args.Length)
				throw new FormatException("{" + piIndex + "}: there are only " + pOContext.Args.Length + " arguments");
		}

		private static object fRaw(Context pOContext, int piIndex)
		{
			return pOContext.Analyse ? null : pOContext.Args[piIndex];
		}

		/// <summary>An argument as shown: a fragment with its slots filled (as deep as MaxFragmentDepth),
		/// anything else as string.Format shows it.</summary>
		private static string fArgument(Context pOContext, int piIndex, int piDepth)
		{
			if (pOContext.Analyse)
				return string.Empty;

			object lOArg = pOContext.Args[piIndex];

			if (lOArg is UWTextFragment lOFragment && lOFragment.HasSlots && piDepth < MaxFragmentDepth)
			{
				StringBuilder lOText = new StringBuilder();
				int liAt = 0;

				fPattern(lOFragment.Text, ref liAt, lOText, pOContext, false, null, 0, piDepth + 1);

				return lOText.ToString();
			}

			return string.Format(CultureInfo.CurrentCulture, "{0}", lOArg);
		}

		/// <summary>The branches of a select or plural after its second comma, up to and with its
		/// closing "}"; the chosen one written.</summary>
		private static void fChoice(string psText, ref int piAt, StringBuilder pOOut, Context pOContext, int piIndex, bool pbPlural,
			string psNumber, int piNesting, int piDepth, int piStart)
		{
			if (pOContext.Analyse && piNesting > MaxNesting)
				fAdd(pOContext, true, "select or plural nested more than one level deep at " + piStart);

			object lOArg = fRaw(pOContext, piIndex);
			long llNumber = 0;
			bool lbNumber = false;
			string lsNumberText = null;

			if (pbPlural && !pOContext.Analyse)
			{
				lbNumber = fNumber(lOArg, out llNumber);
				lsNumberText = lbNumber ? llNumber.ToString(CultureInfo.InvariantCulture) : string.Format(CultureInfo.CurrentCulture, "{0}", lOArg).Trim();
			}

			string lsInnerNumber = pbPlural ? lsNumberText ?? string.Empty : psNumber;
			List<string> lsKeys = new List<string>();
			List<string> lsTexts = new List<string>();

			while (true)
			{
				fSkipBlanks(psText, ref piAt);

				if (piAt >= psText.Length)
					throw new FormatException((pbPlural ? "a plural" : "a select") + " not closed at " + piStart);

				if (psText[piAt] == '}')
				{
					piAt++;

					break;
				}

				int liKey = piAt;

				while (piAt < psText.Length && psText[piAt] != '{' && psText[piAt] != '}' && !char.IsWhiteSpace(psText[piAt]))
					piAt++;

				string lsKey = psText.Substring(liKey, piAt - liKey);

				fSkipBlanks(psText, ref piAt);

				if (lsKey.Length == 0 || piAt >= psText.Length || psText[piAt] != '{')
					throw new FormatException((pbPlural ? "a plural" : "a select") + " key without its {...} at " + liKey);

				if (pOContext.Analyse)
					fCheckKey(pOContext, lsKey, pbPlural, liKey);

				piAt++;

				StringBuilder lOBranch = new StringBuilder();

				fPattern(psText, ref piAt, lOBranch, pOContext, true, lsInnerNumber, piNesting, piDepth);
				piAt++;

				lsKeys.Add(lsKey);
				lsTexts.Add(lOBranch.ToString());
			}

			if (pOContext.Analyse)
			{
				if (!lsKeys.Contains(UWPluralRules.Other))
					fAdd(pOContext, true, (pbPlural ? "a plural" : "a select") + " without its other{...} at " + piStart);

				return;
			}

			int liChosen = -1;

			if (pbPlural)
			{
				if (lbNumber)
					liChosen = lsKeys.IndexOf("=" + llNumber.ToString(CultureInfo.InvariantCulture));

				if (liChosen < 0 && lbNumber)
					liChosen = lsKeys.IndexOf(UWPluralRules.Category(pOContext.Code, llNumber));
			}
			else if (lOArg is UWNamePhrase lOPhrase)
			{
				foreach (string lsTry in lOPhrase.SelectKeys())
				{
					liChosen = lsKeys.IndexOf(lsTry);

					if (liChosen >= 0)
						break;
				}
			}
			else
			{
				liChosen = lsKeys.IndexOf(string.Format(CultureInfo.CurrentCulture, "{0}", lOArg));
			}

			if (liChosen < 0)
				liChosen = lsKeys.IndexOf(UWPluralRules.Other);

			if (liChosen >= 0)
				pOOut.Append(lsTexts[liChosen]);
		}

		/// <summary>A select key is a word ([A-Za-z0-9_.-]); a plural key a CLDR category or =N, and a
		/// category the language takes (a warning).</summary>
		private static void fCheckKey(Context pOContext, string psKey, bool pbPlural, int piAt)
		{
			if (pbPlural)
			{
				if (psKey[0] == '=')
				{
					if (psKey.Length < 2 || !long.TryParse(psKey.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out _))
						fAdd(pOContext, true, "plural key " + psKey + " is no =number at " + piAt);

					return;
				}

				if (!UWPluralRules.IsCategory(psKey))
				{
					fAdd(pOContext, true, "plural key " + psKey + " is no CLDR category (zero, one, two, few, many, other) at " + piAt);

					return;
				}

				if (pOContext.Code != null && Array.IndexOf(UWPluralRules.CategoriesOf(pOContext.Code), psKey) < 0)
					fAdd(pOContext, false, "plural key " + psKey + ": the language " + pOContext.Code + " never takes it (it has "
						+ string.Join(", ", UWPluralRules.CategoriesOf(pOContext.Code)) + ")");

				return;
			}

			foreach (char lcChar in psKey)
			{
				if (!(lcChar >= 'a' && lcChar <= 'z') && !(lcChar >= 'A' && lcChar <= 'Z') && !(lcChar >= '0' && lcChar <= '9')
					&& lcChar != '_' && lcChar != '.' && lcChar != '-')
				{
					fAdd(pOContext, true, "select key " + psKey + " is no word at " + piAt);

					return;
				}
			}
		}

		/// <summary>The number of a plural's argument: a whole number, a name's count, or a text that
		/// reads as one (the level-up's " 5").</summary>
		private static bool fNumber(object pOArg, out long plNumber)
		{
			plNumber = 0;

			switch (pOArg)
			{
				case int liValue:
					plNumber = liValue;
					return true;
				case long llValue:
					plNumber = llValue;
					return true;
				case short liShort:
					plNumber = liShort;
					return true;
				case byte lbByte:
					plNumber = lbByte;
					return true;
				case uint luValue:
					plNumber = luValue;
					return true;
				case UWNamePhrase lOPhrase:
					plNumber = lOPhrase.Count;
					return lOPhrase.Count >= 0;
				case string lsText:
					return long.TryParse(lsText.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out plNumber);
				default:
					return false;
			}
		}
	}
}
