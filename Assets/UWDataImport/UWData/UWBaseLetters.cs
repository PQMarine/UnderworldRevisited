using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PLAIN LETTERS OF A CHARACTER THE FONT LACKS (per user, 2026-11-01, on a player's report from a
	/// French translation: "ete" for the word with its two accents came out as a lone t). The classic fonts
	/// hold ASCII and the German letters; a language's font (grammar.rule.font) still goes first, this is
	/// the last resort before nothing: the letter without its accent, the two letters of a ligature, the
	/// plain form of typographic punctuation. Latin-1 and Latin Extended-A; a table, not String.Normalize,
	/// so that it reads the same on every platform and globalisation setting.
	/// </summary>
	public static class UWBaseLetters
	{
		private const int FirstCode = 0xA0;

		/// <summary>The base letter from U+00A0 to U+017F, a full stop for none.</summary>
		private const string Letters =
			"................................AAAAAA.CEEEEIIII.NOOOOO..UUUUY.."
			+ "aaaaaa.ceeeeiiii.nooooo..uuuuy.yAaAaAaCcCcCcCcDd..EeEeEeEeEeGgGg"
			+ "GgGgHh..IiIiIiIiI...JjKk.LlLlLl....NnNnNn...OoOoOo..RrRrRrSsSsSs"
			+ "SsTtTt..UuUuUuUuUuUuWwYyYZzZzZz.";

		private static readonly Dictionary<int, string> msOthers = new Dictionary<int, string>
		{
			{ 0x00A0, " " }, { 0x2009, " " }, { 0x202F, " " },
			{ 0x00AB, "\"" }, { 0x00BB, "\"" }, { 0x201C, "\"" }, { 0x201D, "\"" }, { 0x201E, "\"" },
			{ 0x2018, "'" }, { 0x2019, "'" }, { 0x201A, "'" }, { 0x2039, "'" }, { 0x203A, "'" },
			{ 0x2013, "-" }, { 0x2014, "-" }, { 0x2026, "..." },
			{ 0x00C6, "AE" }, { 0x00E6, "ae" }, { 0x0152, "OE" }, { 0x0153, "oe" }, { 0x0132, "IJ" }, { 0x0133, "ij" },
			{ 0x00DF, "ss" }, { 0x00DE, "Th" }, { 0x00FE, "th" },
			{ 0x00D0, "D" }, { 0x00F0, "d" }, { 0x0110, "D" }, { 0x0111, "d" }, { 0x00D8, "O" }, { 0x00F8, "o" },
			{ 0x0126, "H" }, { 0x0127, "h" }, { 0x0131, "i" }, { 0x013F, "L" }, { 0x0140, "l" }, { 0x0141, "L" }, { 0x0142, "l" },
			{ 0x014A, "N" }, { 0x014B, "n" }, { 0x0166, "T" }, { 0x0167, "t" }, { 0x017F, "s" }
		};

		/// <summary>What a typed character comes to under an entry's filter: itself where the filter takes it,
		/// else its plain letters (per user, 2026-11-01: an accent key and e gave nothing, the system composes
		/// the letter with its accent and the filter refused that), null when nothing passes.</summary>
		public static string Typed(char pcTyped, System.Func<char, bool> pFTakes)
		{
			if (pFTakes(pcTyped))
				return pcTyped.ToString();

			string lsLetters = Of(pcTyped);

			if (lsLetters == null)
				return null;

			foreach (char lcLetter in lsLetters)
			{
				if (!pFTakes(lcLetter))
					return null;
			}

			return lsLetters;
		}

		/// <summary>The plain characters that stand for piCode, null when none do.</summary>
		public static string Of(int piCode)
		{
			if (msOthers.TryGetValue(piCode, out string lsOther))
				return lsOther;

			int liAt = piCode - FirstCode;

			if (liAt < 0 || liAt >= Letters.Length || Letters[liAt] == '.')
				return null;

			return Letters[liAt].ToString();
		}
	}
}
