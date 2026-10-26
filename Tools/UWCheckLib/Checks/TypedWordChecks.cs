using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// TRANSLATION WAVE 2 (2026-10-26): the typed words in a language beyond ASCII. The text entries'
    /// filter takes the language's letters (UWTextLine.LanguageLetters, German: the umlauts and the
    /// sharp s) and only then; a map note upper-cases them and writes the sharp s as SS; a note and a
    /// save name keep them as their Latin-1 byte and read them back; the conversation's compare and
    /// contains fold the umlauts' case and take the German quotation marks for punctuation. English
    /// (no letters) types, stores and compares as before. Data-free; made-up words only.
    /// </summary>
    public static partial class SelfChecks
    {
        private const char AUmlaut = 'ä';

        private const char CapitalAUmlaut = 'Ä';

        private const char SharpS = 'ß';

        private static void fCheckTypedWords()
        {
            Check.Group("Typed words beyond ASCII (UWTextLine.LanguageLetters, compare and contains - translation wave 2)");

            string lsBefore = UWTextLine.LanguageLetters;

            try
            {
                fCheckTypedFilter();
                fCheckTypedMapNotes();
                fCheckTypedStorage();
                fCheckTypedCompare();
            }
            finally
            {
                UWTextLine.LanguageLetters = lsBefore;
            }
        }

        private static void fCheckTypedFilter()
        {
            UWTextLine lOLine = new UWTextLine();

            UWTextLine.LanguageLetters = UWGrammar.English.TypeableLetters;

            Check.Bool("filter: English takes no umlaut", UWTextLine.IsTypeableInLanguage(AUmlaut) || lOLine.Takes(AUmlaut), false);
            Check.Bool("filter: ... and every printable ASCII character", UWTextLine.IsTypeableInLanguage(' ') && UWTextLine.IsTypeableInLanguage('~')
                && !UWTextLine.IsTypeableInLanguage('\u007f'), true);

            UWTextLine.LanguageLetters = UWGrammar.German.TypeableLetters;

            Check.Bool("filter: German takes the umlauts and the sharp s", UWTextLine.IsTypeableInLanguage(AUmlaut)
                && UWTextLine.IsTypeableInLanguage(CapitalAUmlaut) && UWTextLine.IsTypeableInLanguage(SharpS), true);
            Check.Bool("filter: ... not other accents", UWTextLine.IsTypeableInLanguage('é'), false);
            Check.Bool("filter: the ASCII filter stays ASCII", UWTextLine.IsTypeable(AUmlaut), false);

            lOLine.Set(string.Empty, 10);
            lOLine.InsertText("Gr" + AUmlaut + SharpS + "eé");

            Check.Str("filter: the kit's line (the modern save name) takes them", fCodes(lOLine.Text), "47 72 E4 DF 65");
        }

        private static void fCheckTypedMapNotes()
        {
            System.Func<string, int> lfWidth = psText => psText.Length * 4;
            string lsText;

            UWTextLine.LanguageLetters = UWGrammar.English.TypeableLetters;

            Check.Bool("map notes: English refuses an umlaut", UWMapNoteRules.TryAppend("A", AUmlaut, 10, lfWidth, out lsText), false);

            UWTextLine.LanguageLetters = UWGrammar.German.TypeableLetters;

            Check.Bool("map notes: German takes it, upper-cased", UWMapNoteRules.TryAppend("A", AUmlaut, 10, lfWidth, out lsText)
                && lsText == "A" + CapitalAUmlaut, true);
            Check.Bool("map notes: the sharp s becomes SS", UWMapNoteRules.TryAppend("A", SharpS, 10, lfWidth, out lsText) && lsText == "ASS", true);
            Check.Bool("map notes: ... refused whole at the length limit",
                UWMapNoteRules.TryAppend(new string('A', 45), SharpS, 0, psText => 0, out lsText), false);
        }

        private static void fCheckTypedStorage()
        {
            Check.Str("save name: an umlaut and the sharp s as their Latin-1 byte", fByteCodes(UWSavegameStore.EncodeDescription("Gr" + AUmlaut + SharpS + "e €1")),
                "47 72 E4 DF 65 20 31");
            Check.Str("save name: English as before", fByteCodes(UWSavegameStore.EncodeDescription("Save 1~")), "53 61 76 65 20 31 7E");

            // The character's name (translation 5.1): its entry takes the language's Latin-1 letters, PLAYER.DAT
            // keeps them as their byte and reads them back; what a save cannot keep stays the old underscore.
            byte[] lyName = UWPlayerData.BuildNewCharacter(new UWPlayerData.NewCharacter { Name = "J" + AUmlaut + "rж~" });

            Check.Str("character name: the umlaut as its Latin-1 byte, read back", fByteCodes(new[] { lyName[0], lyName[1], lyName[2], lyName[3], lyName[4], lyName[5] })
                + " " + UWPlayerData.FromPlain(lyName).Name, "4A E4 72 5F 7E 00 J" + AUmlaut + "r_~");

            byte[] lyFile = new byte[2 + 54];

            lyFile[2] = 0xC4;
            lyFile[3] = (byte)'P';
            lyFile[2 + 0x32] = 40;

            List<UWLevel.MapNote> lORead = UWLevel.ParseMapNotes(lyFile, 2, lyFile.Length);

            Check.Str("map note block: a note starting with A-umlaut reads", lORead.Count == 1 ? fCodes(lORead[0].Text) : "none", "C4 50");
            Check.Bool("map note block: ... and goes back byte for byte", lORead.Count == 1 && UWSaveCodec.BuildMapNoteBlock(lORead)[0] == 0xC4, true);

            lyFile[2] = 0xD7;

            Check.Int("map note block: a sign above 0x7E still ends the notes", UWLevel.ParseMapNotes(lyFile, 2, lyFile.Length).Count, 0);
        }

        private static void fCheckTypedCompare()
        {
            string lsGruen = "grün";
            string lsGruenCapital = "GRÜN";

            Check.Bool("compare: the umlaut's case folds", UWConversationTextRules.SameText(lsGruenCapital, lsGruen), true);
            Check.Bool("compare: English as before", UWConversationTextRules.SameText("Rawstag", "RAWSTAG")
                && !UWConversationTextRules.SameText("Rawstag", "Rawstag!"), true);
            Check.Bool("compare: the sharp s is no SS", UWConversationTextRules.SameText("Stra" + SharpS + "e", "STRASSE"), false);
            Check.Bool("contains: an umlaut word in capitals", UWConversationTextRules.ContainsWord("Ich suche " + lsGruenCapital + "ES Gras", lsGruen + "es"), true);
            Check.Bool("contains: German quotation marks are punctuation",
                UWConversationTextRules.ContainsWord("„" + lsGruen + "“", lsGruen), true);
            Check.Bool("contains: an umlaut is a letter, no boundary", UWConversationTextRules.ContainsWord(lsGruen + "ä", lsGruen), false);
        }

        private static string fByteCodes(byte[] pyBytes)
        {
            List<string> lsCodes = new List<string>();

            foreach (byte lyByte in pyBytes)
                lsCodes.Add(lyByte.ToString("X2"));

            return string.Join(" ", lsCodes);
        }
    }
}
