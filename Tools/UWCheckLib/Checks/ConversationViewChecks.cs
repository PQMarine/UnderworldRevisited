using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.12 (2026-10-20): what the conversation shows moved out of the classic screen
    /// (UWConversationScreen) into the engine-free UWConversationView and UWConversationLayout, and
    /// the modern conversation escapes rich text (UWRichText, B31). Pinned with the real font where
    /// the data has it: the
    /// parchment's capacity, Ketchaval's wrap and indent (per user with screenshots of the original,
    /// 2026-09-30), [MORE] after twelve lines of one text, the pause at every "\m" and "\p", the count
    /// starting over per text, the modern policy that never pauses, the vine edges' step on a clear
    /// and on a scroll, the answers' numbering and click rows, the typed answer's limits, the look
    /// text's two seconds, the trade slots' hit test and the portraits' choice.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckConversationView(DataImport pOData)
        {
            Check.Group("Conversation view (UWConversationView, UWConversationLayout, UWRichText - cleanup step 5.12)");

            UWFont lOFont = pOData.Fonts != null ? pOData.Fonts.Get(UWFonts.FontType.Normal) : null;
            bool lbRealFont = lOFont != null && lOFont.IsLoaded;

            // The GOG archive the check unpacks carries no FONT*.SYS: then the checks run on a
            // stand-in of font5x6p's height, every character 4 pixels wide, and the pixel measures
            // of Ketchaval's lines are left out.
            if (!lbRealFont)
                lOFont = fStandInFont();

            if (lOFont == null)
            {
                Check.Fail("view: a font", "neither FONT5X6P.SYS nor the stand-in");
                return;
            }

            fCheckKetchavalWrap(pOData, lOFont, lbRealFont);

            fExpectInt("layout: the parchment holds thirteen six-pixel lines", UWConversationLayout.HistoryCapacity(lOFont), 13);

            fCheckConversationMore(lOFont);
            fCheckConversationAnswers(lOFont);
            fCheckConversationTyping(lOFont);
            fCheckConversationLookAndTrade();
        }

        /// <summary>A font file of font5x6p's height (6 rows, one byte each), 128 characters 4 pixels
        /// wide and a 2-pixel space, written to a temporary file and read back.</summary>
        private static UWFont fStandInFont()
        {
            const int liHeight = 6;
            const int liCount = 128;
            byte[] lyData = new byte[12 + (liCount * (liHeight + 1))];

            lyData[0] = 1;
            lyData[2] = liHeight;
            lyData[4] = 2;
            lyData[6] = liHeight;
            lyData[8] = 1;
            lyData[10] = 4;

            for (int liAt = 0; liAt < liCount; liAt++)
                lyData[12 + (liAt * (liHeight + 1)) + liHeight] = 4;

            string lsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "uwselfcheck-standin-" + System.Guid.NewGuid().ToString("N") + ".sys");

            try
            {
                System.IO.File.WriteAllBytes(lsPath, lyData);

                UWFont lOFont = new UWFont(lsPath);

                return lOFont.IsLoaded ? lOFont : null;
            }
            finally
            {
                try
                {
                    System.IO.File.Delete(lsPath);
                }
                catch (System.IO.IOException)
                {
                    // A temporary file left behind is harmless.
                }
            }
        }

        /// <summary>Ketchaval's speech (level 2): the line break after "the Gray tribe." ends the line
        /// and indents " Years ago," by its space; in font5x6p "the last holding of the noble
        /// Goblins," stays on one 159-pixel line.</summary>
        private static void fCheckKetchavalWrap(DataImport pOData, UWFont pOFont, bool pbRealFont)
        {
            string lsText = null;

            if (pOData.Strings != null && pOData.Strings.Blocks != null)
            {
                foreach (KeyValuePair<int, UWStringBlock> lOPair in pOData.Strings.Blocks)
                {
                    if (lOPair.Value == null || lOPair.Value.Strings == null)
                        continue;

                    foreach (string lsString in lOPair.Value.Strings)
                    {
                        if (lsString != null && lsString.Contains("Gray tribe.") && lsString.Contains("Years ago"))
                        {
                            lsText = lsString;
                            break;
                        }
                    }

                    if (lsText != null)
                        break;
                }
            }

            if (lsText == null)
            {
                Check.Fail("Ketchaval: the speech", "not found in the strings");
                return;
            }

            List<string> lOLines = UWConversationLayout.WrapWithBreaks(pOFont, lsText.Trim(), UWConversationLayout.HistoryWidth);
            int liTribe = lOLines.FindIndex(lsLine => lsLine.EndsWith("tribe."));

            Check.Bool("Ketchaval: the speech breaks its line after \"tribe.\"", lsText.Contains("tribe.\n Years ago,"), true);
            Check.Bool("Ketchaval: a line ends at \"tribe.\"", liTribe >= 0, true);
            Check.Bool("Ketchaval: the next starts \" Years ago,\" with its space",
                liTribe >= 0 && liTribe + 1 < lOLines.Count && lOLines[liTribe + 1].StartsWith(" Years ago,"), true);

            if (!pbRealFont)
            {
                Check.Pass("Ketchaval: the 159-pixel line", "FONT5X6P.SYS not in this data folder, not measured");
                return;
            }

            int liGoblins = lOLines.FindIndex(lsLine => lsLine.EndsWith("the last holding of the noble Goblins,"));

            Check.Bool("Ketchaval: \"the last holding of the noble Goblins,\" ends a line", liGoblins >= 0, true);
            fExpectInt("Ketchaval: ... which is 159 pixels wide",
                liGoblins >= 0 ? pOFont.MeasureText(lOLines[liGoblins], 0, UWTextLayout.CharacterSpacing) : -1, 159);

            int liWidest = 0;

            foreach (string lsLine in lOLines)
                liWidest = System.Math.Max(liWidest, pOFont.MeasureText(lsLine, 0, UWTextLayout.CharacterSpacing));

            Check.Bool("Ketchaval: no line wider than the parchment's 159", liWidest <= UWConversationLayout.HistoryWidth, true);
        }

        /// <summary>A text of so many words that it wraps to many lines.</summary>
        private static string fLongText(int piWords)
        {
            List<string> lOWords = new List<string>();

            for (int liAt = 0; liAt < piWords; liAt++)
                lOWords.Add("word" + liAt);

            return string.Join(" ", lOWords);
        }

        private static UWConversationView fViewOf(params UWConversationSession.Line[] pyLines)
        {
            return UWConversationView.Preview(pyLines, null, "Partner", "Player", UWTexture.TextureTypes.GENHEAD, 0, 0);
        }

        private static void fCheckConversationMore(UWFont pOFont)
        {
            UWConversationView.Policy lyClassic = UWConversationView.Policy.For(pOFont, false);
            UWConversationView.Policy lyModern = UWConversationView.Policy.For(pOFont, true);
            List<string> lOVisible = new List<string>();
            List<UWConversationLayout.RoleEnum> lORoles = new List<UWConversationLayout.RoleEnum>();

            Check.Bool("policy: classic pauses for [MORE]", lyClassic.PauseForMore, true);
            Check.Bool("policy: modern never does", lyModern.PauseForMore, false);
            fExpectInt("policy: both wrap to the parchment", lyModern.WrapWidth, UWConversationLayout.HistoryWidth);

            // One text of twenty lines.
            string lsLong = fLongTextOfLines(pOFont, 20);
            List<string> lOWrapped = UWConversationLayout.WrapWithBreaks(pOFont, lsLong, UWConversationLayout.HistoryWidth);

            fExpectInt("[MORE]: the test text wraps to twenty lines", lOWrapped.Count, 20);

            UWConversationView lOView = fViewOf(new UWConversationSession.Line { IsNpc = true, Text = lsLong });

            lOView.Refresh(pOFont, lyClassic);
            lOView.VisibleHistory(lyClassic, lOVisible, lORoles);
            Check.Bool("[MORE]: pending after twelve lines of one text", lOView.MorePending, true);
            fExpectInt("[MORE]: twelve lines and [MORE] under them", lOVisible.Count, 13);
            Check.Str("[MORE]: the twelfth line above it", lOVisible[11], lOWrapped[11]);
            Check.Str("[MORE]: the last row is the marker", lOVisible[12], UWConversationView.MoreMarker);
            Check.Bool("[MORE]: in the interface's colour role", lORoles[12] == UWConversationLayout.RoleEnum.Ui, true);
            Check.Bool("[MORE]: the lines in the partner's", lORoles[0] == UWConversationLayout.RoleEnum.Npc, true);

            lOView.ContinueAfterMore();
            lOView.Refresh(pOFont, lyClassic);
            lOView.VisibleHistory(lyClassic, lOVisible, lORoles);
            Check.Bool("[MORE]: the click brings the rest", lOView.MorePending, false);
            fExpectInt("[MORE]: ... the parchment full", lOVisible.Count, 13);
            Check.Str("[MORE]: ... the oldest pushed out at the top", lOVisible[0], lOWrapped[7]);
            Check.Str("[MORE]: ... the newest at the bottom", lOVisible[12], lOWrapped[19]);

            // The modern history scrolls: the same text arrives whole.
            UWConversationView lOModern = fViewOf(new UWConversationSession.Line { IsNpc = true, Text = lsLong });

            lOModern.Refresh(pOFont, lyModern);
            Check.Bool("modern: no [MORE]", lOModern.MorePending, false);
            lOModern.VisibleHistory(lyModern, lOVisible, lORoles);
            Check.Str("modern: everything at once (the modern history wraps the entries itself)", lOVisible[12], lOWrapped[19]);

            // "\m" and "\p" pause.
            UWConversationView lOPaged = fViewOf(new UWConversationSession.Line { IsNpc = true, Text = "First part.\\m Second part.\\p Third." });

            lOPaged.Refresh(pOFont, lyClassic);
            lOPaged.VisibleHistory(lyClassic, lOVisible, lORoles);
            Check.Str("\\m: the text pauses after its first part", string.Join("|", lOVisible), "First part.|" + UWConversationView.MoreMarker);
            lOPaged.ContinueAfterMore();
            lOPaged.Refresh(pOFont, lyClassic);
            lOPaged.VisibleHistory(lyClassic, lOVisible, lORoles);
            Check.Str("\\p: ... and after its second", string.Join("|", lOVisible), "First part.|Second part.|" + UWConversationView.MoreMarker);
            lOPaged.ContinueAfterMore();
            lOPaged.Refresh(pOFont, lyClassic);
            lOPaged.VisibleHistory(lyClassic, lOVisible, lORoles);
            Check.Str("\\m: the last part without a pause", string.Join("|", lOVisible), "First part.|Second part.|Third.");
            Check.Bool("\\m: nothing pending at the end", lOPaged.MorePending, false);

            // The limit counts per text: two texts of eight lines each never pause.
            string lsEight = fLongTextOfLines(pOFont, 8);
            UWConversationView lOTwo = fViewOf(
                new UWConversationSession.Line { IsNpc = true, Text = lsEight },
                new UWConversationSession.Line { IsPlayer = true, Text = lsEight });

            lOTwo.Refresh(pOFont, lyClassic);
            Check.Bool("[MORE]: counted per text - two of eight lines do not pause", lOTwo.MorePending, false);
            lOTwo.VisibleHistory(lyClassic, lOVisible, lORoles);
            fExpectInt("[MORE]: ... the parchment shows the last thirteen", lOVisible.Count, 13);
            Check.Bool("[MORE]: ... the player's lines in the player's role", lORoles[12] == UWConversationLayout.RoleEnum.Player, true);

            // Roles and colours.
            Check.Bool("roles: narrator", UWConversationLayout.RoleOf(new UWConversationSession.Line { Text = "x" })
                == UWConversationLayout.RoleEnum.Narrator, true);
            Check.Bool("colours: the partner's is CONV.BYT's sample text",
                UWConversationLayout.ClassicColour(UWConversationLayout.RoleEnum.Npc).Equals(UWClassicLayout.Conversation.NpcText), true);
            Check.Bool("colours: the player's reddish brown",
                UWConversationLayout.ClassicColour(UWConversationLayout.RoleEnum.Player).Equals(UWClassicLayout.Conversation.PlayerText), true);

            // The vine edges: one step on a clear, one per line scrolled.
            UWConversationView lOEdges = new UWConversationView();
            int liStart = lOEdges.EdgeFrame;

            lOEdges.ResetHistory();
            fExpectInt("edges: a cleared parchment is one step", lOEdges.EdgeFrame,
                UWScrollEdgeRules.Advance(liStart, UWScrollEdgeRules.ConversationFrames));

            int liAfterClear = lOEdges.EdgeFrame;

            Check.Bool("edges: the first lines are no scroll", lOEdges.RollEdges(new[] { "a", "b" }), false);
            Check.Bool("edges: a line added below is no scroll", lOEdges.RollEdges(new[] { "a", "b", "c" }), false);
            Check.Bool("edges: the top line pushed out is a scroll", lOEdges.RollEdges(new[] { "b", "c", "d" }), true);
            fExpectInt("edges: ... by one step", lOEdges.EdgeFrame,
                UWScrollEdgeRules.Advance(liAfterClear, UWScrollEdgeRules.ConversationFrames, 1));

            // The modern history's text: page marks gone, rich text escaped (B31).
            Check.Str("modern: page marks dropped", UWConversationLayout.WithoutPageBreaks("Hello\\m world\\p\r\n"), "Hello world");
            Check.Str("B31: angle brackets escaped", UWRichText.Escape("a<b>c</color>"), "a\u2039b\u203Ac\u2039/color\u203A");
            Check.Str("B31: plain text unchanged", UWRichText.Escape("Hello"), "Hello");
            Check.Str("B31: null is empty", UWRichText.Escape(null), string.Empty);
        }

        /// <summary>A text that wraps to exactly piLines lines on the parchment.</summary>
        private static string fLongTextOfLines(UWFont pOFont, int piLines)
        {
            for (int liWords = 1; liWords < 400; liWords++)
            {
                string lsText = fLongText(liWords);

                if (UWConversationLayout.WrapWithBreaks(pOFont, lsText, UWConversationLayout.HistoryWidth).Count == piLines
                    && UWConversationLayout.WrapWithBreaks(pOFont, fLongText(liWords + 1), UWConversationLayout.HistoryWidth).Count > piLines)
                    return lsText;
            }

            return string.Empty;
        }

        private static void fCheckConversationAnswers(UWFont pOFont)
        {
            UWConversationView lOView = UWConversationView.Preview(null,
                new[] { "Don't mention it.", fLongText(20), "Farewell.", fLongText(20) }, null, null, UWTexture.TextureTypes.GENHEAD, 0, 0);
            List<string> lOLines = new List<string>();
            List<int> lOFirst = new List<int>();

            lOView.ChoiceLines(pOFont, lOLines, lOFirst);

            Check.Str("answers: numbered with a period", lOLines[0], "1. Don't mention it.");
            Check.Bool("answers: the long one wraps", lOFirst[2] - lOFirst[1] >= 2, true);
            Check.Str("answers: the third starts its line", lOLines[lOFirst[2]], "3. Farewell.");

            UWConversationLayout.ChoiceRows(lOFirst, 1, out int liTop, out int liCount);
            fExpectInt("answers: the wrapped answer's rows start on its line", liTop, lOFirst[1]);
            fExpectInt("answers: ... and reach to the next answer", liCount, lOFirst[2] - lOFirst[1]);

            UWConversationLayout.ChoiceRows(lOFirst, 3, out liTop, out liCount);
            fExpectInt("answers: the last answer has one row, however it wraps (as the classic screen built it)", liCount, 1);
            Check.Bool("answers: ... though it wraps here", lOLines.Count - lOFirst[3] >= 2, true);

            fExpectInt("answers: 1 to 4 classic", UWConversationView.ChoiceKeys(false), 4);
            fExpectInt("answers: 1 to 9 modern", UWConversationView.ChoiceKeys(true), 9);
        }

        private static void fCheckConversationTyping(UWFont pOFont)
        {
            UWConversationView lOView = new UWConversationView();

            lOView.BeginTyping();
            Check.Bool("typing: open", lOView.IsTyping, true);
            Check.Str("typing: the prompt and the cursor", lOView.TypedAnswerLine, ">_");

            lOView.TryType('a', pOFont);
            lOView.TryType('<', pOFont);
            Check.Str("typing: what is typed", lOView.TypedAnswerLine, ">a<_");
            Check.Bool("typing: Backspace takes one", lOView.Backspace(), true);
            Check.Str("typing: ... the last", lOView.TypedAnswer, "a");

            int liTaken = 0;

            while (liTaken < 200 && lOView.TryType('W', pOFont))
                liTaken++;

            int liLimit = UWConversationLayout.ChoicesWidth - pOFont.MeasureText(UWConversationLayout.TypedAnswerPrompt, 0, UWTextLayout.CharacterSpacing)
                - UWConversationLayout.TypedAnswerMargin;

            Check.Bool("typing: wdialog stops at the scroll less the prompt and 20 pixels (or at 50)",
                pOFont.MeasureText(lOView.TypedAnswer, 0, UWTextLayout.CharacterSpacing) >= liLimit
                || lOView.TypedAnswer.Length == UWConversationLayout.TypedAnswerMaxLength, true);
            Check.Bool("typing: ... and not a character earlier",
                pOFont.MeasureText(lOView.TypedAnswer.Substring(0, lOView.TypedAnswer.Length - 1), 0, UWTextLayout.CharacterSpacing) < liLimit, true);

            UWConversationView lONoFont = new UWConversationView();

            lONoFont.BeginTyping();

            for (int liAt = 0; liAt < 80; liAt++)
                lONoFont.TryType('i', null);

            fExpectInt("typing: at most 50 characters (babl_ask)", lONoFont.TypedAnswer.Length, UWConversationLayout.TypedAnswerMaxLength);

            lOView.EndTyping();
            Check.Bool("typing: closed", lOView.IsTyping, false);
            lOView.BeginTyping();
            Check.Str("typing: a new question starts empty", lOView.TypedAnswer, string.Empty);
        }

        private static void fCheckConversationLookAndTrade()
        {
            UWConversationView lOView = new UWConversationView();

            Check.Bool("look: none at first", lOView.LookText == null, true);
            lOView.ShowLookText("You see a sword.", 10f);
            Check.Str("look: shown", lOView.LookText, "You see a sword.");
            Check.Bool("look: still after 1.9 s", lOView.IsLookTextOver(11.9f), false);
            Check.Bool("look: over after 2 s", lOView.IsLookTextOver(12f), true);
            Check.Bool("look: ... but shown until the controller ends it", lOView.IsLookTextShown, true);
            lOView.EndLookText();
            Check.Bool("look: ended", lOView.LookText == null, true);

            // The trade slots: 17 by 17 from the original's tables, the crosses no button.
            fExpectInt("trade: the partner's first slot", UWConversationLayout.TradeSlotAt(true, 91f, 11f), 0);
            fExpectInt("trade: ... its far corner", UWConversationLayout.TradeSlotAt(true, 107.9f, 27.9f), 0);
            fExpectInt("trade: ... and no further", UWConversationLayout.TradeSlotAt(true, 108f, 11f), -1);
            fExpectInt("trade: the partner's lower right slot on row 29", UWConversationLayout.TradeSlotAt(true, 112f, 29f), 3);
            fExpectInt("trade: the player's first slot", UWConversationLayout.TradeSlotAt(false, 148f, 11f), 0);
            fExpectInt("trade: a cross is no button", UWConversationLayout.TradeSlotAt(true, 88f, 18f), -1);
            Check.Str("trade: a 16 x 16 picture fills the circle", UWConversationLayout.TradeItemPlace((91, 11), 16, 16).ToString(), "(91, 11)");
            Check.Str("trade: a smaller one is centred", UWConversationLayout.TradeItemPlace((91, 11), 8, 10).ToString(), "(95, 14)");
            fExpectInt("trade: no trade, no version", lOView.TradeVersion, -1);
            Check.Bool("trade: no trade, an empty slot", lOView.TradeSlotOf(true, 0).Item == null && !lOView.TradeSlotOf(true, 0).Marked, true);

            // The portraits.
            UWConversationLayout.PartnerPortrait(5, null, out UWTexture.TextureTypes leType, out int liIndex);
            Check.Bool("portrait: a named character from CHARHEAD", leType == UWTexture.TextureTypes.CHARHEAD && liIndex == 4, true);
            UWConversationLayout.PartnerPortrait(29, null, out leType, out liIndex);
            Check.Bool("portrait: the others from GENHEAD", leType == UWTexture.TextureTypes.GENHEAD && liIndex == 0, true);
            fExpectInt("portrait: no player, the first head", UWConversationLayout.PlayerPortrait(null), 0);

            // The names run to the screen's edge.
            fExpectInt("names: the partner's room", UWConversationLayout.NameRoom(UWClassicLayout.Conversation.NpcNameLeft), 272);
        }
    }
}
