using System.Collections.Generic;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// TRANSLATION WAVE 4 (2026-10-26): the CLDR plural table (UWPluralRules) against CLDR's own
    /// integer samples of plurals.xml (read 2026-10-26): every sample number of every category of
    /// every code of the table (en de nl sv fi no nb nn da hu tr fr pt pt-PT es it ca ru uk pl cs sk
    /// ja zh ko) gives that category; the categories a language takes; a region and an unknown code.
    /// Data-free.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>CLDR's integer samples per rule: "category: numbers; ...", ranges a~b.</summary>
        private static readonly string[][] msPluralSamples =
        {
            new[] { "en de nl sv fi it ca pt-PT", "one: 1", "other: 0, 2~16, 100, 1000, 10000, 100000" },
            new[] { "no nb nn da hu tr", "one: 1", "other: 0, 2~16, 100, 1000, 10000, 100000, 1000000" },
            new[] { "fr pt", "one: 0, 1", "many: 1000000, 2000000", "other: 2~17, 100, 1000, 10000, 100000" },
            new[] { "es it ca pt-PT", "one: 1", "many: 1000000, 2000000, 6000000", "other: 0, 2~16, 100, 1000, 10000, 100000" },
            new[] { "ru uk", "one: 1, 21, 31, 41, 51, 61, 71, 81, 101, 1001", "few: 2~4, 22~24, 32~34, 42~44, 52~54, 62, 102, 1002",
                "many: 0, 5~19, 100, 1000, 10000, 100000, 1000000" },
            new[] { "pl", "one: 1", "few: 2~4, 22~24, 32~34, 42~44, 52~54, 62, 102, 1002", "many: 0, 5~19, 21, 100, 1000, 10000, 100000, 1000000" },
            new[] { "cs sk", "one: 1", "few: 2~4", "other: 0, 5~19, 100, 1000, 10000, 100000, 1000000" },
            new[] { "ja zh ko", "other: 0~15, 100, 1000, 10000, 100000, 1000000" }
        };

        private static void fCheckPluralRules()
        {
            Check.Group("Plural categories (UWPluralRules, CLDR's integer samples - translation wave 4)");

            StringBuilder lOWrong = new StringBuilder();
            int liSamples = 0;

            foreach (string[] lsRow in msPluralSamples)
            {
                foreach (string lsCode in lsRow[0].Split(' '))
                {
                    for (int liAt = 1; liAt < lsRow.Length; liAt++)
                    {
                        int liColon = lsRow[liAt].IndexOf(':');
                        string lsCategory = lsRow[liAt].Substring(0, liColon);

                        foreach (long llNumber in fPluralNumbers(lsRow[liAt].Substring(liColon + 1)))
                        {
                            liSamples++;

                            string lsGot = UWPluralRules.Category(lsCode, llNumber);

                            if (lsGot != lsCategory)
                                lOWrong.Append(lsCode).Append(' ').Append(llNumber).Append(": ").Append(lsGot).Append(" not ").Append(lsCategory).Append("; ");
                        }
                    }
                }
            }

            Check.Str("plural: every CLDR integer sample in its category (" + liSamples + " samples over " + UWPluralRules.Codes.Length + " codes)",
                lOWrong.ToString(), string.Empty);
            Check.Str("plural: the categories a language takes", string.Join(" ", UWPluralRules.CategoriesOf("en")) + "|"
                + string.Join(" ", UWPluralRules.CategoriesOf("fr")) + "|" + string.Join(" ", UWPluralRules.CategoriesOf("ru")) + "|"
                + string.Join(" ", UWPluralRules.CategoriesOf("cs")) + "|" + string.Join(" ", UWPluralRules.CategoriesOf("ja")),
                "one other|one many other|one few many other|one few other|other");
            Check.Str("plural: a region takes its language, pt-PT its own row, an unknown code the English", UWPluralRules.Category("ru-RU", 3) + "|"
                + UWPluralRules.Category("de_AT", 0) + "|" + UWPluralRules.Category("pt-BR", 0) + "|" + UWPluralRules.Category("pt_PT", 0) + "|"
                + UWPluralRules.Category("xx", 1) + "|" + UWPluralRules.Category(null, 2) + "|" + UWPluralRules.Category("PL", 5),
                "few|other|one|other|one|other|many");
            Check.Str("plural: a negative count as its absolute value", UWPluralRules.Category("ru", -21) + "|" + UWPluralRules.Category("en", -1),
                "one|one");
        }

        /// <summary>The numbers of a sample list "2~4, 22, 100".</summary>
        private static List<long> fPluralNumbers(string psList)
        {
            List<long> lONumbers = new List<long>();

            foreach (string lsPart in psList.Split(','))
            {
                string lsItem = lsPart.Trim();
                int liTilde = lsItem.IndexOf('~');

                if (liTilde < 0)
                {
                    lONumbers.Add(long.Parse(lsItem, System.Globalization.CultureInfo.InvariantCulture));

                    continue;
                }

                long llFrom = long.Parse(lsItem.Substring(0, liTilde), System.Globalization.CultureInfo.InvariantCulture);
                long llTo = long.Parse(lsItem.Substring(liTilde + 1), System.Globalization.CultureInfo.InvariantCulture);

                for (long llNumber = llFrom; llNumber <= llTo; llNumber++)
                    lONumbers.Add(llNumber);
            }

            return lONumbers;
        }
    }
}
