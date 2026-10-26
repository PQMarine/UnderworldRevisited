using System.IO;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// TRANSLATION 5.1 (2026-10-26): the gamepad letter grid's keys per letter set (UWLetterGridLayout,
    /// drawn and driven by UWLetterGrid). English keeps exactly the old grid and its moves; German gets
    /// a row of ä ö ü ß above the digits; more than ten letters take more rows; a language's alphabet
    /// (grammar.rule.alphabet, the made-up "xc" of Tools/Fixtures/i18n with the Cyrillic letters) stands
    /// for A to Z, and the text fields take its letters - a save keeps only Latin-1 (UWTextLine.IsStorable).
    /// </summary>
    public static partial class SelfChecks
    {
        private const string EnglishGrid = "ABCDEFGHIJ/KLMNOPQRST/UVWXYZ'-.,/1234567890/[5x2]";

        private static void fCheckLetterGrid()
        {
            Check.Group("The gamepad letter grid per letter set (UWLetterGridLayout - translation 5.1)");

            try
            {
                fCheckLetterGridEnglish();
                fCheckLetterGridGerman();
                fCheckLetterGridAlphabet();
            }
            finally
            {
                UWText.UseEnglish();
                UWTextLine.UseEnglish();
            }
        }

        /// <summary>A cursor's walk: each step "dx,dy" from row 0, column 0, the cells it passes as "row:column".</summary>
        private static string fGridWalk(UWLetterGridLayout pOLayout, int piRow, int piColumn, params int[] piSteps)
        {
            StringBuilder lOWalk = new StringBuilder();
            int liWanted = piColumn;

            for (int liAt = 0; liAt + 1 < piSteps.Length; liAt += 2)
            {
                pOLayout.Move(ref piRow, ref piColumn, ref liWanted, piSteps[liAt], piSteps[liAt + 1]);
                lOWalk.Append(lOWalk.Length > 0 ? " " : string.Empty).Append(piRow).Append(':').Append(piColumn);
            }

            return lOWalk.ToString();
        }

        private static void fCheckLetterGridEnglish()
        {
            UWTextLine.UseEnglish();

            UWLetterGridLayout lOLayout = UWLetterGridLayout.Current;

            Check.Str("English: the old grid, upper and lower case", lOLayout.Describe(true) + " " + lOLayout.Describe(false),
                EnglishGrid + " abcdefghij/klmnopqrst/uvwxyz'-.,/1234567890/[5x2]");
            Check.Str("English: rows, letter rows, the bottom row", lOLayout.Rows + " " + lOLayout.LetterRows + " " + lOLayout.BottomRow, "5 0 4");
            Check.Str("English: the grammar's alphabet is A to Z (the text table's value)", UWGrammar.English.Alphabet + " " + UWGrammar.German.Alphabet
                + " " + UWGrammar.English.AlphabetLower, UWGrammarEnglish.LatinAlphabet + " " + UWGrammarEnglish.LatinAlphabet + " abcdefghijklmnopqrstuvwxyz");

            // The old moves: sideways wraps over ten, by two on the bottom row; down keeps the column, the
            // bottom row its even one, and up from there stays on it; the rows wrap.
            Check.Str("English: the old moves", fGridWalk(lOLayout, 0, 7, 0, 1, 0, 1, 0, 1, 0, 1, 0, -1, 1, 0, -1, 0, -1, 0, 0, 1, 1, 0, 0, 1),
                "1:7 2:7 3:7 4:6 3:6 3:7 3:6 3:5 4:4 4:6 0:6");
            Check.Str("English: wrapping at the edges", fGridWalk(lOLayout, 0, 0, -1, 0, 1, 0, 0, -1, 1, 0, -1, 0, -1, 0),
                "0:9 0:0 4:0 4:2 4:0 4:8");
        }

        private static void fCheckLetterGridGerman()
        {
            UWTextLine.UseLanguage(UWGrammar.German);

            UWLetterGridLayout lOLayout = UWLetterGridLayout.Current;

            Check.Str("German: the umlauts and the sharp s in a row above the digits", lOLayout.Describe(true) + " " + lOLayout.Describe(false),
                "ABCDEFGHIJ/KLMNOPQRST/UVWXYZ'-.,/ÄÖÜß/1234567890/[5x2] abcdefghij/klmnopqrst/uvwxyz'-.,/äöüß/1234567890/[5x2]");
            Check.Str("German: rows, letter rows; the fields take the letters", lOLayout.Rows + " " + lOLayout.LetterRows + " " + UWTextLine.LanguageLetters
                + " " + UWTextLine.IsTypeableInLanguage('ß') + " " + UWTextLine.IsTypeableAndStorable('Ä'), "6 1 äöüÄÖÜß True True");

            // Down from the 8th column into the short row takes its last key and goes on in the 8th; sideways
            // wraps within its four keys.
            Check.Str("German: through the short row and within it", fGridWalk(lOLayout, 2, 7, 0, 1, 0, 1, 0, -1, 1, 0, -1, 0, 0, 1),
                "3:3 4:7 3:3 3:0 3:3 4:3");
            UWTextLine.UseEnglish();

            Check.Str("German: back to English, a new layout, the old grid again", ReferenceEquals(lOLayout, UWLetterGridLayout.Current) + " "
                + UWLetterGridLayout.Current.Describe(true), "False " + EnglishGrid);

            // More than ten letters: rows of ten, the last one short.
            UWLetterGridLayout lOMany = UWLetterGridLayout.Build(UWGrammarEnglish.LatinAlphabet, null, "àáâãåæçèéêëìÀ");

            Check.Str("twelve letters: two rows, the capitals where the set has them", lOMany.Describe(true) + " " + lOMany.Rows + " " + lOMany.LetterRows,
                "ABCDEFGHIJ/KLMNOPQRST/UVWXYZ'-.,/Àáâãåæçèéê/ëì/1234567890/[5x2] 7 2");
        }

        private static void fCheckLetterGridAlphabet()
        {
            string lsPath = fFixture("xc.tsv");

            Check.Str("alphabet: the header read", UWGrammarEnglish.ParseAlphabet(" a b c ") + " " + UWGrammarEnglish.ParseAlphabet("none") + " "
                + UWGrammarEnglish.ParseAlphabetLower("x y", "AB") + " " + UWGrammarEnglish.ParseAlphabetLower("x", "AB") + " " + UWGrammarEnglish.ParseAlphabet("AB1A"),
                "abc " + UWGrammarEnglish.LatinAlphabet + " xy ab AB");

            if (lsPath == null || !File.Exists(lsPath))
            {
                Check.Pass("alphabet: the fixture", "skipped - no source tree");

                return;
            }

            UWTextLanguageFile lOXc = UWTextLanguageFile.Parse(File.ReadAllText(lsPath, Encoding.UTF8));

            UWText.UseLanguage("xc", lOXc);
            UWTextLine.UseLanguage(UWGrammar.For("xc"));

            UWLetterGridLayout lOLayout = UWLetterGridLayout.Current;

            Check.Str("xc: 33 letters and the signs in four rows, no row of other letters", lOLayout.Describe(true) + " " + lOLayout.Rows + " " + lOLayout.LetterRows,
                "АБВГДЕЁЖЗИ/ЙКЛМНОПРСТ/УФХЦЧШЩЪЫЬ/ЭЮЯ'-.,/1234567890/[5x2] 6 0");
            Check.Str("xc: the lower case derived", lOLayout.Describe(false).Substring(0, 10), "абвгдеёжзи");

            string lsNote;

            Check.Str("xc: the fields take its letters, a save does not keep them", UWTextLine.IsTypeableInLanguage('ж') + " " + UWTextLine.IsTypeableInLanguage('Ж')
                + " " + UWTextLine.IsTypeableInLanguage('ä') + " " + UWTextLine.IsTypeableAndStorable('ж') + " " + UWTextLine.IsStorable('ä') + " "
                + UWMapNoteRules.TryAppend("AB", 'ж', 0, psText => psText.Length, out lsNote) + " " + UWTextLine.IsTypeableInLanguage('Q'),
                "True True False False True False True");
            Check.Str("xc: the short row's moves", fGridWalk(lOLayout, 2, 8, 0, 1, 1, 0, 0, 1, 0, -1, 0, -1),
                "3:6 3:0 4:0 3:0 2:0");
            Check.Str("xc: the header check names the letters the classic fonts lack", string.Join("; ", UWGrammarGeneric.CheckHeader(psId =>
                lOXc.Texts.TryGetValue(psId, out string lsText) ? lsText : null)), "grammar.rule.alphabet: the classic fonts cannot draw 66 of its letters ("
                + "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя) - the language needs a font of its own");
        }
    }
}
