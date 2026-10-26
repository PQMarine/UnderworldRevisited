using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Translation wave 1 (2026-10-26): the game's own texts beyond ASCII and the game-text packs
    /// (UWGameTextPack, layer 1). Data-free: the string decoder (UWStringNode, UWStringBlock) on a
    /// STRINGS.PAK built here with a byte above 0x7F; a pack's lines, escapes and the template's
    /// format. With the data: the made-up pack of Tools/Fixtures/i18n laid over the English (the
    /// strings it names change, no other; UWGameData lays it as the game does), its findings, the
    /// template read back as the English, and a conversation run on translated strings - the VM's
    /// placeholders filled in as in English. Every string of a fixture is made up; the English is
    /// only compared, never written out.
    /// </summary>
    public static partial class SelfChecks
    {
        private const string GameTextFixture = "Tools/Fixtures/i18n/made-up.strings.tsv";

        private static void fCheckGameText()
        {
            Check.Group("Game texts (STRINGS.PAK beyond ASCII, game-text packs - translation wave 1)");

            fCheckGameTextDecoder();
            fCheckGameTextParse();
        }

        private static void fCheckGameTextData(DataImport pOData)
        {
            Check.Group("Game-text packs on the English strings (translation wave 1)");

            if (pOData == null)
            {
                Check.Fail("game texts: data", "not loaded");

                return;
            }

            string lsPak = Path.Combine(pOData.DataPath, "STRINGS.PAK");

            fCheckGameTextFixture(pOData.DataPath, lsPak);
            fCheckGameTextTemplate(lsPak);
            fCheckGameTextConversation(pOData, lsPak);
        }

        // ------------------------------------------------- A pack's lines

        /// <summary>The lines of a pack: a byte order mark, CR LF, comments, a line of blanks, an empty
        /// text with and without its TAB, the escapes, the game's own backslash codes, bad lines.</summary>
        private static void fCheckGameTextParse()
        {
            string lsBom = ((char)0xFEFF).ToString();
            string lsFile = lsBom + "# a comment\r\n"
                + "1\t2\tplain text \r\n"
                + "   \t \r\n"
                + "1\t3\t\r\n"
                + "1\t4\n"
                + "1\t5\tline\\nbreak\\ttab\\\\n back\\mslash \\0 end\\\n"
                + "1\t6\ttext\twith a TAB\n"
                + "no tabs here\n"
                + "1\tx\tbad index\n"
                + " 2\t+7\tblanks and a sign\n";

            UWGameTextPack lOPack = UWGameTextPack.Parse(lsFile);

            Check.Int("pack: rows with a text", lOPack.Rows.Count, 4);
            Check.Int("pack: empty texts stay English (with and without the TAB)", lOPack.EmptyRows, 2);
            Check.Str("pack: lines skipped", string.Join(" | ", lOPack.Problems),
                "line 8: not block, TAB, index, TAB, text | line 9: block or index is no number");

            if (lOPack.Rows.Count == 4)
            {
                Check.Str("pack: a text keeps its blanks, the CR goes", lOPack.Rows[0].Text, "plain text ");
                Check.Str("pack: the escapes read, the game's codes stay", fCodes(lOPack.Rows[1].Text),
                    fCodes("line\nbreak\ttab\\n back\\mslash \\0 end\\"));
                Check.Str("pack: a TAB in the text is the text's", lOPack.Rows[2].Text, "text\twith a TAB");
                Check.Str("pack: numbers as int.Parse reads them", lOPack.Rows[3].Key + " line " + lOPack.Rows[3].Line, "2:7 line 10");
            }

            // Escape and Unescape: lossless, and the game's codes written as the fork's files have them.
            string[] lsTexts = { "a\nb", "\\m and \\p", "\\n literal", "\\\\", "end\\", "tab\there\r\n", string.Empty };
            StringBuilder lOBad = new StringBuilder();

            foreach (string lsText in lsTexts)
            {
                if (UWGameTextPack.Unescape(UWGameTextPack.Escape(lsText)) != lsText)
                    lOBad.Append(fCodes(lsText)).Append("; ");
            }

            Check.Str("pack: escaping is lossless", lOBad.ToString(), string.Empty);
            Check.Str("pack: a code stays one backslash", UWGameTextPack.Escape("\\mHi\n\\0"), "\\mHi\\n\\0");

            // The VM's placeholders as fGetSubstitutedString reads them, in any order.
            Check.Str("pack: placeholders", UWGameTextPack.PlaceholderSignature("@SS2 x @GI3C2 @SI4 @PS-1 @GS8SI2 @ss1 @GS 8 @"),
                "@GI3C2,@GS8SI2,@PS-1,@SI4,@SS2");
            Check.Str("pack: an SI right after a placeholder belongs to it", UWGameTextPack.PlaceholderSignature("@SS15SIE"), "@SS15SI");
            Check.Str("pack: codes", UWGameTextPack.CodeSignature("\\4a\\0 \\m %d %2$s"), "%2$s,%d,\\0,\\4,\\m");
        }

        // ------------------------------------------------- The made-up pack on the English

        private static void fCheckGameTextFixture(string psDataPath, string psPak)
        {
            string lsRoot = CheckContext.ProjectRoot;

            if (lsRoot == null)
            {
                Check.Pass("pack fixture", "outside the source tree, skipped");

                return;
            }

            string lsFixture = Path.Combine(lsRoot, GameTextFixture.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(lsFixture))
            {
                Check.Fail("pack fixture", "no " + GameTextFixture);

                return;
            }

            UWStrings lOEnglish = new UWStrings(File.ReadAllBytes(psPak));
            UWStrings lOLaid = new UWStrings(File.ReadAllBytes(psPak));
            UWGameTextPack lOPack = UWGameTextPack.Parse(File.ReadAllText(lsFixture, Encoding.UTF8));

            // Its findings, by line - the made-up mistakes, each where it was put.
            List<string> lsFound = new List<string>();

            foreach (UWGameTextPack.Finding lOFinding in lOPack.Validate(lOEnglish))
                lsFound.Add((lOFinding.IsError ? "E" : "W") + lOFinding.Line + (lOFinding.Key.Length > 0 ? " " + lOFinding.Key : string.Empty));

            Check.Str("fixture: findings", string.Join(", ", lsFound),
                "E11 3586:34, E13 3586:113, W14 3:3, W14 3:3, E15, E16, E17 99999:1, E18 7:0, E19 7:1");

            int liApplied = lOPack.Apply(lOLaid);

            Check.Int("fixture: strings laid over (the later 7:1 twice)", liApplied, 11);
            Check.Str("fixture: what the game reports", string.Join(" | ", lOPack.Problems),
                "line 15: not block, TAB, index, TAB, text | line 16: block or index is no number"
                + " | line 17: no string 99999:1 | line 18: no string 7:0 | line 19: 7:1 again, the later text counts");

            // Each named string holds the pack's last text for it; every other string is the English.
            Dictionary<string, string> lOWanted = new Dictionary<string, string>();

            foreach (UWGameTextPack.Row lORow in lOPack.Rows)
            {
                if (lOEnglish.Blocks.TryGetValue(lORow.Block, out UWStringBlock lOBlock) && lORow.Index >= 1 && lORow.Index < lOBlock.Strings.Count)
                    lOWanted[lORow.Key] = lORow.Text;
            }

            int liSame = 0;
            StringBuilder lODiffer = new StringBuilder();

            foreach (KeyValuePair<int, UWStringBlock> lOPair in lOEnglish.Blocks)
            {
                List<string> lsBefore = lOPair.Value.Strings;
                List<string> lsAfter = lOLaid.Blocks[lOPair.Key].Strings;

                for (int liIndex = 0; liIndex < lsBefore.Count; liIndex++)
                {
                    string lsKey = lOPair.Key + ":" + liIndex;

                    if (lOWanted.TryGetValue(lsKey, out string lsText))
                    {
                        if (lsAfter[liIndex] != lsText)
                            lODiffer.Append(lsKey).Append(" not the pack's; ");
                    }
                    else if (lsAfter[liIndex] != lsBefore[liIndex])
                        lODiffer.Append(lsKey).Append(" changed; ");
                    else
                        liSame++;
                }
            }

            Check.Str("fixture: the named strings the pack's, no other changed", lODiffer.ToString(), string.Empty);
            Check.Pass("fixture: strings left English", liSame.ToString());

            string lsUmlauts = lOLaid.Blocks[7].Strings[1];

            Check.Bool("fixture: the later 7:1 counts, its umlauts kept", lsUmlauts.IndexOf((char)0xE4) > 0 && lsUmlauts.EndsWith("\n"), true);
            Check.Str("fixture: the save message's codes and line break", fCodes(lOLaid.Blocks[1].Strings[164]).Substring(0, 5) + " .. "
                + fCodes(lOLaid.Blocks[1].Strings[164].Substring(lOLaid.Blocks[1].Strings[164].Length - 3)), "5C 34 .. 0A 5C 30");

            // The game's way: UWGameData lays the pack over the strings as it reads them.
            try
            {
                UWGameData lOGame = new UWGameData(psDataPath, lsFixture);

                Check.Bool("fixture: UWGameData lays it over", lOGame.GameText != null && lOGame.GameText.Applied == 11
                    && lOGame.Strings.Blocks[10].Strings[1] == lOLaid.Blocks[10].Strings[1]
                    && lOGame.Strings.Blocks[4].Strings[145] == lOEnglish.Blocks[4].Strings[145], true);
                Check.Bool("fixture: an object name through UWText", UWText.Block(lOGame.Strings, UWText.ObjectNameBlock, 146) == lOLaid.Blocks[4].Strings[146], true);
                Check.Bool("fixture: a pack that is not there keeps the English",
                    new UWGameData(psDataPath, lsFixture + ".missing").GameText == null, true);
            }
            catch (Exception lOError)
            {
                Check.Fail("fixture: UWGameData with a pack", lOError.Message);
            }
        }

        // ------------------------------------------------- The template

        /// <summary>The template of the player's STRINGS.PAK read back is the English, string for
        /// string; with empty texts it changes nothing. Its rows are counted, never written out.</summary>
        private static void fCheckGameTextTemplate(string psPak)
        {
            UWStrings lOEnglish = new UWStrings(File.ReadAllBytes(psPak));
            int liNonEmpty = 0;

            foreach (UWStringBlock lOBlock in lOEnglish.Blocks.Values)
            {
                for (int liIndex = 1; liIndex < lOBlock.Strings.Count; liIndex++)
                {
                    if (!string.IsNullOrEmpty(lOBlock.Strings[liIndex]))
                        liNonEmpty++;
                }
            }

            foreach (bool lbEmpty in new[] { false, true })
            {
                string lsName = lbEmpty ? "template with empty texts" : "template";
                UWStrings lOBack = new UWStrings(File.ReadAllBytes(psPak));
                UWGameTextPack lOPack = UWGameTextPack.Parse(UWGameTextPack.Template(lOEnglish, "xx", lbEmpty));

                lOPack.Apply(lOBack);

                Check.Int(lsName + ": rows", lbEmpty ? lOPack.EmptyRows : lOPack.Rows.Count, liNonEmpty);
                Check.Int(lsName + ": nothing skipped, no finding", lOPack.Problems.Count + lOPack.Validate(lOEnglish).Count, 0);
                Check.Int(lsName + ": read back, strings that differ from the English", fDiffering(lOEnglish, lOBack), 0);
            }
        }

        private static int fDiffering(UWStrings pOLeft, UWStrings pORight)
        {
            int liDiffering = 0;

            foreach (KeyValuePair<int, UWStringBlock> lOPair in pOLeft.Blocks)
            {
                List<string> lsLeft = lOPair.Value.Strings;
                List<string> lsRight = pORight.Blocks[lOPair.Key].Strings;

                for (int liIndex = 0; liIndex < lsLeft.Count; liIndex++)
                {
                    if (liIndex >= lsRight.Count || lsLeft[liIndex] != lsRight[liIndex])
                        liDiffering++;
                }
            }

            return liDiffering;
        }

        // ------------------------------------------------- The conversation VM on translated strings

        /// <summary>
        /// A conversation (slot 2) run twice, answering 1 each time: on the English and on a pack that
        /// changes every a, o, u of its string block to an umlaut (an invented "translation" that keeps
        /// every placeholder, since they are capitals and digits). What the VM says, prints and offers
        /// on the translation must be the English run's words with the same change - the placeholders
        /// filled in alike, the umlauts kept, the run as long.
        /// </summary>
        private static void fCheckGameTextConversation(DataImport pOData, string psPak)
        {
            const int liSlot = 2;
            UWConversations.Conversation lOConversation = pOData.Conversations != null ? pOData.Conversations.GetConversation(liSlot) : null;

            if (lOConversation == null)
            {
                Check.Fail("conversation: slot " + liSlot, "no conversation");

                return;
            }

            UWStrings lOEnglish = new UWStrings(File.ReadAllBytes(psPak));
            UWStrings lOTranslated = new UWStrings(File.ReadAllBytes(psPak));
            StringBuilder lOPackText = new StringBuilder();
            List<string> lsBlock = lOEnglish.Blocks[lOConversation.StringBlock].Strings;

            for (int liIndex = 1; liIndex < lsBlock.Count; liIndex++)
            {
                if (!string.IsNullOrEmpty(lsBlock[liIndex]))
                    lOPackText.Append(lOConversation.StringBlock).Append('\t').Append(liIndex).Append('\t')
                        .Append(UWGameTextPack.Escape(fUmlauts(lsBlock[liIndex]))).Append('\n');
            }

            UWGameTextPack lOPack = UWGameTextPack.Parse(lOPackText.ToString());

            lOPack.Apply(lOTranslated);

            Check.Int("conversation: the pack's findings", lOPack.Validate(lOEnglish).Count, 0);

            List<string> lsEnglish = fRunConversation(lOConversation, lOEnglish);
            List<string> lsTranslated = fRunConversation(lOConversation, lOTranslated);
            StringBuilder lODiffer = new StringBuilder();
            int liPlaceholders = 0;

            for (int liAt = 0; liAt < lsEnglish.Count && liAt < lsTranslated.Count; liAt++)
            {
                if (fUmlauts(lsEnglish[liAt]) != lsTranslated[liAt] && lODiffer.Length == 0)
                    lODiffer.Append("line ").Append(liAt).Append(" differs");
            }

            foreach (string lsText in lsBlock)
                liPlaceholders += UWGameTextPack.PlaceholderSignature(lsText).Length > 0 ? 1 : 0;

            Check.Int("conversation: as many lines translated as in English", lsTranslated.Count, lsEnglish.Count);
            Check.Str("conversation: each line the English one translated", lODiffer.ToString(), string.Empty);
            Check.Bool("conversation: the run has umlauts and the block placeholders", lsTranslated.Exists(psLine => psLine.IndexOf((char)0xE4) >= 0)
                && liPlaceholders > 0 && lsEnglish.Count > 10, true);
        }

        /// <summary>The invented translation: a o u become a-umlaut o-umlaut u-umlaut.</summary>
        private static string fUmlauts(string psText)
        {
            return psText == null ? null : psText.Replace('a', (char)0xE4).Replace('o', (char)0xF6).Replace('u', (char)0xFC);
        }

        /// <summary>What a run says (S), prints (P) and offers (C) and how it ends (E), answering 1 up to 40
        /// times - the tags without a, o, u, so the invented translation leaves them alone.</summary>
        private static List<string> fRunConversation(UWConversations.Conversation pOConversation, UWStrings pOStrings)
        {
            GameTextHost lOHost = new GameTextHost();
            List<string> lsBlock = pOStrings.Blocks[pOConversation.StringBlock].Strings;
            UWConversationVM lOVm = new UWConversationVM(pOConversation, lOHost,
                piIndex => piIndex + 1 >= 0 && piIndex + 1 < lsBlock.Count ? lsBlock[piIndex + 1].TrimEnd('\r', '\n') : string.Empty);

            lOVm.SetImportedGlobal("play_name", 1);
            lOVm.SetImportedGlobal("npc_attitude", 2);

            for (int liChoices = 0; liChoices <= 40; liChoices++)
            {
                UWConversationVM.RunState leState = lOVm.Run();

                if (leState != UWConversationVM.RunState.AwaitingChoice)
                {
                    lOHost.Lines.Add("E " + (int)leState);

                    break;
                }

                foreach (string lsChoice in lOVm.PendingChoices)
                    lOHost.Lines.Add("C " + lsChoice);

                lOVm.SupplyChoice(1);
            }

            return lOHost.Lines;
        }

        /// <summary>Records what the VM says and prints; answers every question with its default.</summary>
        private sealed class GameTextHost : UWConversationVM.IHost
        {
            public readonly List<string> Lines = new List<string>();

            public void Say(string psText) { Lines.Add("S " + psText); }

            public void Print(string psText) { Lines.Add("P " + psText); }

            public int GetQuest(int piFlag) { return 0; }

            public void SetQuest(int piFlag, int piValue) { }

            public int Random(int piMaximum) { return 1; }

            public int CallUnknown(string psName, int piFunctionId, IReadOnlyList<int> pOArguments,
                IReadOnlyList<int> pOAddresses, UWConversationVM pOVm)
            {
                return 0;
            }
        }

        /// <summary>
        /// A STRINGS.PAK of three leaves under two inner nodes - 0xE4 (a-umlaut in Latin-1), 'x' and
        /// the terminator '|' - and one block 77 with the strings a-umlaut x and x a-umlaut a-umlaut. The
        /// decoder starts at the last node, the root, and walks left on a 0 bit and right on a 1 bit
        /// until a leaf. Until 2026-10-26 the leaf's byte was read signed and 0xE4 came out as
        /// U+FFE4.
        /// </summary>
        private static void fCheckGameTextDecoder()
        {
            // Nodes: symbol, parent, left, right (255 = none). 0 a-umlaut, 1 'x', 2 '|', 3 inner (x | |),
            // 4 the root (a-umlaut | inner). Codes: a-umlaut 0, x 10, | 11.
            uint[] lyNodes =
            {
                fNode(0xE4, 4, 255, 255),
                fNode('x', 3, 255, 255),
                fNode('|', 3, 255, 255),
                fNode(0, 4, 1, 2),
                fNode(0, 255, 0, 3)
            };

            // "a-umlaut x |" = 0 10 11 -> 01011000; "x a-umlaut a-umlaut |" = 10 0 0 11 -> 10001100.
            byte[] lyFirst = { 0x58 };
            byte[] lySecond = { 0x8C };

            List<byte> lyFile = new List<byte>();

            lyFile.AddRange(BitConverter.GetBytes((ushort)lyNodes.Length));

            foreach (uint luNode in lyNodes)
                lyFile.AddRange(BitConverter.GetBytes(luNode));

            lyFile.AddRange(BitConverter.GetBytes((ushort)1));

            int liBlockOffset = lyFile.Count + 6;

            lyFile.AddRange(BitConverter.GetBytes((short)77));
            lyFile.AddRange(BitConverter.GetBytes(liBlockOffset));

            // The block: the count, an offset per string (from the end of the offsets), the bits.
            lyFile.AddRange(BitConverter.GetBytes((short)2));
            lyFile.AddRange(BitConverter.GetBytes((ushort)0));
            lyFile.AddRange(BitConverter.GetBytes((ushort)lyFirst.Length));
            lyFile.AddRange(lyFirst);
            lyFile.AddRange(lySecond);

            UWStrings lOStrings;

            try
            {
                lOStrings = new UWStrings(lyFile.ToArray());
            }
            catch (Exception lOError)
            {
                Check.Fail("decoder: a STRINGS.PAK with a byte above 0x7F reads", lOError.Message);

                return;
            }

            UWStringBlock lOBlock;
            bool lbFound = lOStrings.Blocks.TryGetValue(77, out lOBlock);

            Check.Bool("decoder: the made-up block is there", lbFound, true);

            if (!lbFound)
                return;

            Check.Int("decoder: slot zero and two strings", lOBlock.Strings.Count, 3);
            Check.Str("decoder: byte 0xE4 reads as U+00E4", fCodes(lOBlock.Strings.Count > 1 ? lOBlock.Strings[1] : null), "E4 78");
            Check.Str("decoder: the second string", fCodes(lOBlock.Strings.Count > 2 ? lOBlock.Strings[2] : null), "78 E4 E4");
            Check.Int("decoder: a leaf's symbol is unsigned", new UWStringNode(fNode(0xFF, 0, 255, 255)).CharSymbol, 0xFF);
        }

        private static uint fNode(int piSymbol, int piParent, int piLeft, int piRight)
        {
            return (uint)(piSymbol & 0xFF) | (uint)(piParent & 0xFF) << 8 | (uint)(piLeft & 0xFF) << 16 | (uint)(piRight & 0xFF) << 24;
        }

        /// <summary>A text as its characters' codes in hex, "E4 78"; "null" for none.</summary>
        private static string fCodes(string psText)
        {
            if (psText == null)
                return "null";

            List<string> lsCodes = new List<string>();

            foreach (char lcChar in psText)
                lsCodes.Add(((int)lcChar).ToString("X2"));

            return string.Join(" ", lsCodes);
        }
    }
}
