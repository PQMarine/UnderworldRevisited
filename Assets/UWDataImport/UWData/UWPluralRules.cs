using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PLURAL CATEGORIES OF A LANGUAGE (translation wave 4, 2026-10-26): which form a count takes,
	/// by the CLDR cardinal rules (unicode-org/cldr, common/supplemental/plurals.xml, read 2026-10-26)
	/// for whole numbers - the game counts only whole things, so the rules' fraction operands (v, f, t
	/// and the exponent e) are 0 and only i (the number) is left. Integer code, no culture, no table
	/// lookup by hash: the same answer on every platform and in every mirror.
	///
	/// THE CATEGORIES are CLDR's names: zero, one, two, few, many, other. The select/plural syntax of
	/// the frames ({0,plural,one{...}few{...}other{...}}, UWMessageFormat) and the name format's
	/// per-category plurals (&amp;few=..., UWNameFormat) choose by them.
	///
	///   en de nl sv fi (and every code not in the table)   one: 1                       other
	///   no nb nn da hu tr                                   one: 1                       other
	///   fr pt (Brazil)                                      one: 0, 1; many: n % 1000000 = 0 (n != 0); other
	///   es it ca pt-PT                                      one: 1;    many: n % 1000000 = 0 (n != 0); other
	///   ru uk                                               one: n%10=1, n%100!=11; few: n%10=2..4, n%100!=12..14; many: the rest
	///   pl                                                  one: 1; few: n%10=2..4, n%100!=12..14; many: the rest
	///   cs sk                                               one: 1; few: 2..4; other (many is for fractions only)
	///   ja zh ko                                            other
	/// A negative count takes the category of its absolute value. A code is matched by its language
	/// part ("pt-BR" is pt, "de_AT" is de), but "pt-PT" / "pt_PT" is its own row.
	/// </summary>
	public static class UWPluralRules
	{
		public const string Zero = "zero";

		public const string One = "one";

		public const string Two = "two";

		public const string Few = "few";

		public const string Many = "many";

		public const string Other = "other";

		/// <summary>Every category name in CLDR's order.</summary>
		public static readonly string[] AllCategories = { Zero, One, Two, Few, Many, Other };

		/// <summary>The language codes the table has a row for (every other code takes the English row).</summary>
		public static readonly string[] Codes =
		{
			"en", "de", "nl", "sv", "fi", "no", "nb", "nn", "da", "hu", "tr", "fr", "pt", "pt-PT", "es", "it", "ca",
			"ru", "uk", "pl", "cs", "sk", "ja", "zh", "ko"
		};

		private enum Rule
		{
			OneIsOne,
			ZeroOneMillion,
			OneMillion,
			EastSlavic,
			Polish,
			CzechSlovak,
			NoPlural
		}

		/// <summary>The row of a code: the language part lower-case, pt-PT apart.</summary>
		private static Rule fRule(string psCode)
		{
			string lsCode = (psCode ?? string.Empty).Trim().Replace('_', '-').ToLowerInvariant();

			if (lsCode == "pt-pt")
				return Rule.OneMillion;

			int liDash = lsCode.IndexOf('-');

			if (liDash >= 0)
				lsCode = lsCode.Substring(0, liDash);

			switch (lsCode)
			{
				case "fr":
				case "pt":
					return Rule.ZeroOneMillion;
				case "es":
				case "it":
				case "ca":
					return Rule.OneMillion;
				case "ru":
				case "uk":
					return Rule.EastSlavic;
				case "pl":
					return Rule.Polish;
				case "cs":
				case "sk":
					return Rule.CzechSlovak;
				case "ja":
				case "zh":
				case "ko":
					return Rule.NoPlural;
				default:
					return Rule.OneIsOne;
			}
		}

		/// <summary>The category of a count in a language.</summary>
		public static string Category(string psCode, long plCount)
		{
			long n = plCount < 0 ? -plCount : plCount;
			long liMod10 = n % 10;
			long liMod100 = n % 100;

			switch (fRule(psCode))
			{
				case Rule.ZeroOneMillion:
					return n <= 1 ? One : n % 1000000 == 0 ? Many : Other;
				case Rule.OneMillion:
					return n == 1 ? One : n != 0 && n % 1000000 == 0 ? Many : Other;
				case Rule.EastSlavic:
					if (liMod10 == 1 && liMod100 != 11)
						return One;

					return liMod10 >= 2 && liMod10 <= 4 && (liMod100 < 12 || liMod100 > 14) ? Few : Many;
				case Rule.Polish:
					if (n == 1)
						return One;

					return liMod10 >= 2 && liMod10 <= 4 && (liMod100 < 12 || liMod100 > 14) ? Few : Many;
				case Rule.CzechSlovak:
					return n == 1 ? One : n >= 2 && n <= 4 ? Few : Other;
				case Rule.NoPlural:
					return Other;
				default:
					return n == 1 ? One : Other;
			}
		}

		/// <summary>The categories a language's whole numbers can take, in CLDR's order (what a frame's
		/// plural or a name's per-category plural may name for it; "other" always).</summary>
		public static string[] CategoriesOf(string psCode)
		{
			switch (fRule(psCode))
			{
				case Rule.ZeroOneMillion:
				case Rule.OneMillion:
					return new[] { One, Many, Other };
				case Rule.EastSlavic:
				case Rule.Polish:
					return new[] { One, Few, Many, Other };
				case Rule.CzechSlovak:
					return new[] { One, Few, Other };
				case Rule.NoPlural:
					return new[] { Other };
				default:
					return new[] { One, Other };
			}
		}

		/// <summary>Whether a word is one of CLDR's category names.</summary>
		public static bool IsCategory(string psWord)
		{
			return Array.IndexOf(AllCategories, psWord) >= 0;
		}
	}
}
