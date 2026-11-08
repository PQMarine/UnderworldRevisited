using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A LANGUAGE'S OWN FONT AND ITS INPUT METHOD (translation wave 6, 2026-10-26): two ids of the
	/// header in Languages/&lt;code&gt;.tsv, read for every language (English and German too, whose
	/// files name none), live, so the switch of language takes them at once.
	///
	///   grammar.rule.font   the fonts the port draws a character with that its own fonts lack - "none",
	///                       or a list separated by commas, in order:
	///                         zh-TW.otf          a font file in the folder Languages beside the game
	///                                            (.ttf, .otf or .ttc - of a collection its first face),
	///                                            a name, never a path
	///                         os:Microsoft JhengHei   a font installed on the system, by family name
	///                       e.g. "zh-TW.otf, os:Microsoft JhengHei, os:Noto Sans CJK TC". The classic
	///                       pixel fonts ask the entries in order for each character they lack (the
	///                       first entry found that has it draws it - UWFallbackGlyphs); the modern
	///                       interface's fonts get the os: entries as their fallback list (Unity's
	///                       dynamic fonts can only fall back to installed fonts, not to a file).
	///   grammar.rule.ime    yes: the text entries turn on the system's input method (IME) and take
	///                       every character it commits (Chinese, Japanese, Korean); no (the default).
	///   grammar.rule.classic.font   modern: choosing the language switches the classic texts to the
	///                       modern font once (the key F still switches back), and the original pieces
	///                       of the modern scheme follow the key F too - an ideograph in six pixel rows
	///                       is not readable (measured with the made-up Japanese xj, 2026-11-08);
	///                       original (the default): as before.
	///   grammar.rule.classic.bold   yes: in the modern font the classic texts draw their Chinese, Japanese
	///                       and Korean characters bold - a regular CJK face has thin strokes that fade on
	///                       the original's stone and parchment in its grey and brown (character creation
	///                       with the made-up xj, 2026-11-08); the Latin letters and digits stay as they
	///                       are. no (the default).
	///
	/// Without a font the classic fonts draw ASCII and the German letters (UWFontGlyphBuilder) and the
	/// modern ones what Lexend Exa and Liberation Sans have, then whatever Unity finds on the system.
	/// </summary>
	public static class UWLanguageFont
	{
		/// <summary>The header id of the fonts.</summary>
		public const string FontRuleId = "grammar.rule.font";

		/// <summary>The header id of the input method.</summary>
		public const string ImeRuleId = "grammar.rule.ime";

		/// <summary>The header id of the classic texts' font (original or modern).</summary>
		public const string ClassicFontRuleId = "grammar.rule.classic.font";

		public const string ClassicFontOriginal = "original";

		public const string ClassicFontModern = "modern";

		/// <summary>The header id of the bold CJK characters in the classic texts' modern font.</summary>
		public const string ClassicBoldRuleId = "grammar.rule.classic.bold";

		/// <summary>What an entry for an installed font starts with.</summary>
		public const string OsPrefix = "os:";

		/// <summary>The font files a language may name.</summary>
		public static readonly string[] FileExtensions = { ".ttf", ".otf", ".ttc" };

		/// <summary>One entry of grammar.rule.font: a file in Languages, or an installed family.</summary>
		public sealed class Source
		{
			/// <summary>True: Name is a family of the system's fonts; false: a file name in Languages.</summary>
			public bool IsOs;

			public string Name;

			public override string ToString()
			{
				return IsOs ? OsPrefix + Name : Name;
			}
		}

		/// <summary>The fonts of the language in force (its grammar.rule.font), in order; empty for none.</summary>
		public static List<Source> Sources => Parse(UWText.T(FontRuleId), null);

		/// <summary>Whether the language in force names a font of its own.</summary>
		public static bool HasFont => Sources.Count > 0;

		/// <summary>Whether the language in force wants the input method (grammar.rule.ime yes).</summary>
		public static bool ImeWanted => string.Equals(UWText.T(ImeRuleId).Trim(), "yes", StringComparison.OrdinalIgnoreCase);

		/// <summary>Whether the language in force wants its classic texts in the modern font
		/// (grammar.rule.classic.font modern).</summary>
		public static bool ModernClassicFontWanted => string.Equals(UWText.T(ClassicFontRuleId).Trim(), ClassicFontModern, StringComparison.OrdinalIgnoreCase);

		/// <summary>Whether the language in force wants its CJK characters bold in the classic texts' modern
		/// font (grammar.rule.classic.bold yes).</summary>
		public static bool ClassicBoldWanted => string.Equals(UWText.T(ClassicBoldRuleId).Trim(), "yes", StringComparison.OrdinalIgnoreCase);

		/// <summary>A Chinese, Japanese or Korean character (UWTextLayout.IsBreakAnywhere, the Hangul
		/// syllables and the Hangul letters) - what grammar.rule.classic.bold makes bold.</summary>
		public static bool IsCjk(char pcChar)
		{
			return UWTextLayout.IsBreakAnywhere(pcChar) || (pcChar >= '\uAC00' && pcChar <= '\uD7AF') || (pcChar >= '\u1100' && pcChar <= '\u11FF');
		}

		/// <summary>The rule's entries in order (see the class); an entry that does not read is left out
		/// and, with pOProblems, named there.</summary>
		public static List<Source> Parse(string psRule, List<string> pOProblems)
		{
			List<Source> lOSources = new List<Source>();
			string lsRule = (psRule ?? string.Empty).Trim();

			if (lsRule.Length == 0 || lsRule == UWGrammarGeneric.NoneValue)
				return lOSources;

			foreach (string lsPart in lsRule.Split(','))
			{
				string lsEntry = lsPart.Trim();

				if (lsEntry.Length == 0)
					continue;

				if (lsEntry.StartsWith(OsPrefix, StringComparison.OrdinalIgnoreCase))
				{
					string lsFamily = lsEntry.Substring(OsPrefix.Length).Trim();

					if (lsFamily.Length == 0)
						pOProblems?.Add(FontRuleId + ": \"" + lsEntry + "\" names no font family - left out");
					else
						lOSources.Add(new Source { IsOs = true, Name = lsFamily });

					continue;
				}

				string lsWhy = FileProblem(lsEntry);

				if (lsWhy != null)
					pOProblems?.Add(FontRuleId + ": \"" + lsEntry + "\" " + lsWhy + " - left out");
				else
					lOSources.Add(new Source { IsOs = false, Name = lsEntry });
			}

			return lOSources;
		}

		/// <summary>Why a file entry cannot be one, or null: a name only (no folder, no ".."), ending in
		/// one of FileExtensions.</summary>
		public static string FileProblem(string psName)
		{
			if (psName.IndexOf('/') >= 0 || psName.IndexOf('\\') >= 0 || psName.IndexOf(':') >= 0 || psName.Contains(".."))
				return "is no file name in Languages (no folder; an installed font is written os:<family>)";

			foreach (string lsExtension in FileExtensions)
			{
				if (psName.Length > lsExtension.Length && psName.EndsWith(lsExtension, StringComparison.OrdinalIgnoreCase))
					return null;
			}

			return "is no font file (" + string.Join(" ", FileExtensions) + ")";
		}

		/// <summary>What is wrong with the four ids (pText gives an id's text in the language, or null for
		/// one it leaves English) - part of UWGrammarGeneric.CheckHeader. Empty when fine.</summary>
		public static List<string> CheckHeader(Func<string, string> pText)
		{
			List<string> lsFound = new List<string>();
			string lsFont = pText(FontRuleId) ?? UWText.English(FontRuleId) ?? string.Empty;
			string lsIme = (pText(ImeRuleId) ?? UWText.English(ImeRuleId) ?? string.Empty).Trim();
			string lsClassic = (pText(ClassicFontRuleId) ?? UWText.English(ClassicFontRuleId) ?? string.Empty).Trim();
			string lsBold = (pText(ClassicBoldRuleId) ?? UWText.English(ClassicBoldRuleId) ?? string.Empty).Trim();

			Parse(lsFont, lsFound);

			if (!string.Equals(lsIme, "yes", StringComparison.OrdinalIgnoreCase) && !string.Equals(lsIme, "no", StringComparison.OrdinalIgnoreCase))
				lsFound.Add(ImeRuleId + ": " + lsIme + " is neither yes nor no");

			if (!string.Equals(lsClassic, ClassicFontOriginal, StringComparison.OrdinalIgnoreCase)
				&& !string.Equals(lsClassic, ClassicFontModern, StringComparison.OrdinalIgnoreCase))
				lsFound.Add(ClassicFontRuleId + ": " + lsClassic + " is neither " + ClassicFontOriginal + " nor " + ClassicFontModern);

			if (!string.Equals(lsBold, "yes", StringComparison.OrdinalIgnoreCase) && !string.Equals(lsBold, "no", StringComparison.OrdinalIgnoreCase))
				lsFound.Add(ClassicBoldRuleId + ": " + lsBold + " is neither yes nor no");

			return lsFound;
		}
	}
}
