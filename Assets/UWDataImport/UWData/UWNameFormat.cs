using System;
using System.Collections.Generic;
using System.Globalization;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE NAME FORMAT OF A TRANSLATED NAME (translation wave 2 for German, generalised in wave 4,
	/// 2026-10-26): how a game-text pack writes an object's name (block 4, and the names built from
	/// in blocks 1, 7 and 10) so that a grammar can put it in a case, a number and after an article.
	///
	///   &lt;gender&gt;[&lt;article&gt;]:&lt;singular&gt;[|&lt;oblique&gt;][|&lt;case&gt;=&lt;form&gt;]...[&amp;&lt;plural&gt;][&amp;&lt;key&gt;=&lt;form&gt;]...
	///
	///   gender   m, f, n, c (common: the Dutch and Scandinavian "de/en" words) or p (a plural only);
	///            capitals allowed. German knows m f n p.
	///   article  none: the indefinite one; d the definite one; o none at all (as in wave 2).
	///   oblique  a segment after "|" WITHOUT "=": the singular in every case but the nominative where
	///            no named form is given (wave 2's German weak masculine: "m:Drache|Drachen").
	///   case     a segment after "|" WITH a case key and "=": the singular in that case
	///            ("|gen=Dolches|dat=Dolch"). The keys: nom gen dat acc ins loc voc abl par ess tra ine
	///            ela ill ade all abe com (CaseKeys).
	///   plural   the first segment after "&amp;" without "=": the plural (wave 2's "&amp;Dolche").
	///   key=form after "&amp;": a CLDR plural category (zero one two few many other: the form after
	///            a count of that category, "&amp;few=kinżały&amp;many=kinżałów") or a case key (the plural
	///            in that case, uncounted: "&amp;dat=Stiefeln").
	/// A "_" reads as a blank. A segment "x=y" whose x is no known key is plain text, so a name may
	/// hold "=" - the format of wave 2 ("m:Dolch&amp;Dolche", "md:Ring der Demut", "m:Drache|Drachen&amp;Drachen")
	/// reads exactly as it did. A text without the prefix is no name in the format (null): the
	/// grammars read it by the English rules.
	///
	/// THE FORM A GRAMMAR ASKS FOR: a case - its named form, else (not the nominative) the oblique,
	/// else the singular (SingularIn); a count's category - its named form, else for "one" the
	/// singular (one is the singular-like category: ru 21, fr 0), else the plural, else the singular
	/// (PluralFor); an uncounted plural in a case - its named form, else the plural (PluralIn).
	/// </summary>
	public sealed class UWNameFormat
	{
		/// <summary>The gender letters of the format.</summary>
		public const string GenderLetters = "mfncp";

		public const char Indefinite = 'i';

		public const char Definite = 'd';

		public const char NoArticle = 'o';

		/// <summary>The case keys a name may give a form for, and a language's header may name.</summary>
		public static readonly string[] CaseKeys =
		{
			"nom", "gen", "dat", "acc", "ins", "loc", "voc", "abl", "par", "ess", "tra", "ine", "ela", "ill", "ade", "all", "abe", "com"
		};

		public const string Nominative = "nom";

		/// <summary>The gender letter, lower case: m f n c p.</summary>
		public string Gender;

		/// <summary>i (indefinite), d (definite) or o (none).</summary>
		public char Article;

		public string Singular;

		/// <summary>The singular outside the nominative where no case form is named; null if none.</summary>
		public string Oblique;

		/// <summary>The plain plural; null when the name gives none.</summary>
		public string Plural;

		/// <summary>The named singular forms by case key, in the order written.</summary>
		public readonly List<KeyValuePair<string, string>> CaseForms = new List<KeyValuePair<string, string>>();

		/// <summary>The named plural forms by category or case key, in the order written.</summary>
		public readonly List<KeyValuePair<string, string>> PluralForms = new List<KeyValuePair<string, string>>();

		/// <summary>What reads oddly (a second oblique or plain plural, an empty form) - validate-tsv's
		/// findings; the name still reads.</summary>
		public readonly List<string> Problems = new List<string>();

		public bool IsPluralOnly => Gender == "p";

		/// <summary>A name in the format, or null for a text without the prefix (or with a gender not in
		/// psGenders, the letters a language knows).</summary>
		public static UWNameFormat Parse(string psRaw, string psGenders = GenderLetters)
		{
			if (string.IsNullOrEmpty(psRaw))
				return null;

			int liColon = psRaw.IndexOf(':');

			if (liColon < 1 || liColon > 2)
				return null;

			char lcGender = char.ToLowerInvariant(psRaw[0]);
			char lcArticle = liColon == 2 ? char.ToLowerInvariant(psRaw[1]) : Indefinite;

			if ((psGenders ?? GenderLetters).IndexOf(lcGender) < 0 || (liColon == 2 && lcArticle != Definite && lcArticle != NoArticle))
				return null;

			UWNameFormat lOName = new UWNameFormat { Gender = lcGender.ToString(), Article = lcArticle };
			string[] lsPlurals = psRaw.Substring(liColon + 1).Replace('_', ' ').Split('&');
			string[] lsCases = lsPlurals[0].Split('|');

			lOName.Singular = lsCases[0].Trim();

			for (int liAt = 1; liAt < lsCases.Length; liAt++)
			{
				string lsSegment = lsCases[liAt].Trim();
				string lsKey = fKey(lsSegment, false);

				if (lsKey != null)
					fNamed(lOName, lOName.CaseForms, lsKey, lsSegment.Substring(lsKey.Length + 1).Trim(), "|");
				else if (lOName.Oblique == null && lsSegment.Length > 0)
					lOName.Oblique = lsSegment;
				else
					lOName.Problems.Add(lsSegment.Length == 0 ? "an empty form after |" : "a second form without a case key after |: " + lsSegment);
			}

			for (int liAt = 1; liAt < lsPlurals.Length; liAt++)
			{
				string lsSegment = lsPlurals[liAt].Trim();
				string lsKey = fKey(lsSegment, true);

				if (lsKey != null)
					fNamed(lOName, lOName.PluralForms, lsKey, lsSegment.Substring(lsKey.Length + 1).Trim(), "&");
				else if (lOName.Plural == null && lsSegment.Length > 0)
					lOName.Plural = lsSegment;
				else
					lOName.Problems.Add(lsSegment.Length == 0 ? "an empty plural after &" : "a second plural without a key after &: " + lsSegment);
			}

			return lOName;
		}

		/// <summary>The key of a "key=form" segment if it is one the place takes (a case key; after "&amp;"
		/// a plural category too), else null.</summary>
		private static string fKey(string psSegment, bool pbPlural)
		{
			int liEquals = psSegment.IndexOf('=');

			if (liEquals < 1)
				return null;

			string lsKey = psSegment.Substring(0, liEquals).Trim();

			if (Array.IndexOf(CaseKeys, lsKey) >= 0 || (pbPlural && UWPluralRules.IsCategory(lsKey)))
				return psSegment.Substring(0, liEquals);

			return null;
		}

		private static void fNamed(UWNameFormat pOName, List<KeyValuePair<string, string>> pOForms, string psKey, string psForm, string psSeparator)
		{
			string lsKey = psKey.Trim();

			if (psForm.Length == 0)
			{
				pOName.Problems.Add("an empty form for " + lsKey + " after " + psSeparator);

				return;
			}

			foreach (KeyValuePair<string, string> lOPair in pOForms)
			{
				if (lOPair.Key == lsKey)
				{
					pOName.Problems.Add(lsKey + " given twice after " + psSeparator);

					return;
				}
			}

			pOForms.Add(new KeyValuePair<string, string>(lsKey, psForm));
		}

		private static string fFind(List<KeyValuePair<string, string>> pOForms, string psKey)
		{
			foreach (KeyValuePair<string, string> lOPair in pOForms)
			{
				if (lOPair.Key == psKey)
					return lOPair.Value;
			}

			return null;
		}

		/// <summary>The singular in a case: the named form, else (outside the nominative) the oblique,
		/// else the singular.</summary>
		public string SingularIn(string psCaseKey)
		{
			string lsForm = psCaseKey != null ? fFind(CaseForms, psCaseKey) : null;

			if (lsForm != null)
				return lsForm;

			return psCaseKey != null && psCaseKey != Nominative && Oblique != null ? Oblique : Singular;
		}

		/// <summary>The form after a count of a CLDR category: the named form, else for "one" the
		/// singular, else the plural, else the singular.</summary>
		public string PluralFor(string psCategory)
		{
			string lsForm = psCategory != null ? fFind(PluralForms, psCategory) : null;

			if (lsForm != null)
				return lsForm;

			if (psCategory == UWPluralRules.One && !IsPluralOnly)
				return Singular;

			return Plural ?? Singular;
		}

		/// <summary>The uncounted plural in a case: the named form, else the plural, else the singular.</summary>
		public string PluralIn(string psCaseKey)
		{
			string lsForm = psCaseKey != null ? fFind(PluralForms, psCaseKey) : null;

			return lsForm ?? Plural ?? Singular;
		}

		/// <summary>What is wrong with the name for a language: its problems, a gender it does not know
		/// (psGenders), a case key it does not use (psCases, null for any), a plural category it never
		/// takes (psPluralCode, null for any). Null when nothing.</summary>
		public string Check(string psGenders, string[] psCases, string psPluralCode)
		{
			List<string> lsFound = new List<string>(Problems);

			if (Singular.Length == 0)
				lsFound.Add("no name after the prefix");

			if (psGenders != null && psGenders.IndexOf(Gender[0]) < 0)
				lsFound.Add("the gender " + Gender + " is none of the language's (" + psGenders + ")");

			foreach (KeyValuePair<string, string> lOPair in CaseForms)
			{
				if (psCases != null && Array.IndexOf(psCases, lOPair.Key) < 0)
					lsFound.Add("the case " + lOPair.Key + " is none the language asks for (" + string.Join(" ", psCases) + ")");
			}

			foreach (KeyValuePair<string, string> lOPair in PluralForms)
			{
				if (UWPluralRules.IsCategory(lOPair.Key))
				{
					if (psPluralCode != null && Array.IndexOf(UWPluralRules.CategoriesOf(psPluralCode), lOPair.Key) < 0)
						lsFound.Add("the plural category " + lOPair.Key + " is none of " + psPluralCode + "'s ("
							+ string.Join(" ", UWPluralRules.CategoriesOf(psPluralCode)) + ")");
				}
				else if (psCases != null && Array.IndexOf(psCases, lOPair.Key) < 0)
					lsFound.Add("the case " + lOPair.Key + " is none the language asks for (" + string.Join(" ", psCases) + ")");
			}

			return lsFound.Count == 0 ? null : string.Join("; ", lsFound);
		}

		/// <summary>The case key of the grammar's case roles in a language with the usual names:
		/// nom, acc, dat.</summary>
		public static string CaseKeyOf(UWGrammarCase peCase)
		{
			switch (peCase)
			{
				case UWGrammarCase.Accusative:
					return "acc";
				case UWGrammarCase.Dative:
					return "dat";
				default:
					return Nominative;
			}
		}

		/// <summary>A count as the look writes it, invariant.</summary>
		public static string Number(int piCount)
		{
			return piCount.ToString(CultureInfo.InvariantCulture);
		}
	}
}
