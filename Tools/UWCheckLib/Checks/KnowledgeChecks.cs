using System;
using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// WHAT THE CHARACTER LEARNS (UWKnowledge, UWKnowledgeSources; GitHub issue 19, 2026-11-08): every source
    /// string really names what it teaches in the player's data (a mantra its word, a spell its runes, the
    /// flags their word), and the rules - FANLO only after Gurstang's word, Curse half known until An and
    /// Sanct are in the bag, INSAHN's pieces in the order seen, NO never learned, the lines for UWR.json
    /// read back as they were written.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckKnowledge(DataImport pOData)
        {
            Check.Group("Knowledge of mantras and spells (UWKnowledge - GitHub issue 19)");

            if (pOData == null || pOData.Strings == null || pOData.InitialPlayer == null)
            {
                Check.Fail("knowledge", "no strings or no player");

                return;
            }

            fCheckKnowledgeSources(pOData);
            fCheckKnowledgeRules(pOData);
        }

        private static string fKnowledgeString(DataImport pOData, int piBlock, int piIndex)
        {
            return pOData.Strings.Blocks.TryGetValue(piBlock, out UWStringBlock lOBlock) && piIndex < lOBlock.Strings.Count
                ? lOBlock.Strings[piIndex] : string.Empty;
        }

        /// <summary>Every source string names what it teaches.</summary>
        private static void fCheckKnowledgeSources(DataImport pOData)
        {
            List<string> lOWrong = new List<string>();
            int liChecked = 0;
            int[] liBlocks = { 3, 8, 3586, 3608, 3609, 3730, 3731, 3732, 3733, 3801, 3898 };

            foreach (int liBlock in liBlocks)
            {
                if (!pOData.Strings.Blocks.TryGetValue(liBlock, out UWStringBlock lOBlock))
                    continue;

                for (int liIndex = 0; liIndex < lOBlock.Strings.Count; liIndex++)
                {
                    foreach (UWKnowledgeSources.Source lOSource in UWKnowledgeSources.Of(liBlock, liIndex))
                    {
                        string lsText = lOBlock.Strings[liIndex];
                        string lsWord;

                        switch (lOSource.Kind)
                        {
                            case UWKnowledgeSources.KindEnum.Mantra:
                                lsWord = UWShrineRules.GetMantra(lOSource.Id, pOData).Trim();
                                break;

                            case UWKnowledgeSources.KindEnum.Spell:
                                lsWord = UWRunicMagic.DescribeRunes(UWRunicMagic.AllSpells[lOSource.Id].RuneSequence);

                                // Runes may stand apart ("Vas, Ort and Grav", the door 3609:16): each one in order.
                                if (lsText.IndexOf(lsWord, StringComparison.OrdinalIgnoreCase) < 0 && fRunesInOrder(lsText, lsWord))
                                    lsWord = string.Empty;
                                break;

                            case UWKnowledgeSources.KindEnum.HalfSpell:
                                lsWord = lOSource.Id == UWKnowledgeSources.SummonMonsterSpell ? "Kal"
                                    : lOSource.Id == UWKnowledgeSources.ThickSkinSpell ? "Remove Bet from a Resist Blows" : "Sheet Lightning";
                                break;

                            default:
                                lsWord = lOSource.Flag == UWKnowledgeSources.FolanaeFlag ? "Folanae" : "Curse";

                                // Murgo's mime teaches the word his line before it names (UWLizardWords).
                                if (lOSource.Flag.StartsWith(UWLizardWords.FlagPrefix))
                                {
                                    lsWord = lOSource.Flag.Substring(UWLizardWords.FlagPrefix.Length);
                                    lsText = liIndex == UWLizardWords.NameGuessedIndex ? lOBlock.Strings[UWLizardWords.NameIndex]
                                        : liIndex > 0 ? lOBlock.Strings[liIndex - 1] : string.Empty;
                                }
                                break;
                        }

                        liChecked++;

                        if (lsText.IndexOf(lsWord, StringComparison.OrdinalIgnoreCase) < 0)
                            lOWrong.Add(liBlock + ":" + liIndex + " lacks \"" + lsWord + "\"");
                    }
                }
            }

            Check.Str("knowledge: every source string names what it teaches (" + liChecked + " sources)", string.Join(", ", lOWrong), string.Empty);
            Check.Bool("knowledge: ... all 71 found in the data (49, Murgo's 21 lizard words and his name guessed)", liChecked == 71, true);
        }

        /// <summary>Every rune of psRunes ("Vas Ort Grav") stands in psText, in that order.</summary>
        private static bool fRunesInOrder(string psText, string psRunes)
        {
            int liAt = 0;

            foreach (string lsRune in psRunes.Split(' '))
            {
                liAt = psText.IndexOf(lsRune, liAt, StringComparison.Ordinal);

                if (liAt < 0)
                    return false;

                liAt += lsRune.Length;
            }

            return true;
        }

        private static void fCheckKnowledgeRules(DataImport pOData)
        {
            UWKnowledge lOKnown = new UWKnowledge();

            // FANLO: the book before Gurstang teaches nothing, after him it does.
            lOKnown.SawString(3, 172, null);
            bool lbBefore = lOKnown.KnowsMantra(UWShrineRules.KeyOfTruthMantra, out UWKnowledge.Entry _);

            lOKnown.SawString(3801, 8, "Gurstang");
            lOKnown.SawString(3, 172, null);
            lOKnown.KnowsMantra(UWShrineRules.KeyOfTruthMantra, out UWKnowledge.Entry lOFanlo);

            Check.Str("knowledge: FANLO's book before Gurstang, then after him", lbBefore + " " + lOFanlo.Source, "False Book");

            // His other way to the word: "I, too, seek the Key" (3801:9).
            UWKnowledge lOOther = new UWKnowledge();

            lOOther.SawString(3801, 9, "Gurstang");
            lOOther.SawString(3, 172, null);
            Check.Bool("knowledge: FANLO's book after Gurstang's other line", lOOther.KnowsMantra(UWShrineRules.KeyOfTruthMantra, out UWKnowledge.Entry _), true);

            // The help names what FANLO does only once it was chanted.
            bool lbBeforeChant = lOOther.ChantedFanlo;

            lOOther.Chanted(UWShrineRules.KeyOfTruthMantra);
            Check.Str("knowledge: FANLO chanted, before and after", lbBeforeChant + " " + lOOther.ChantedFanlo + " " + UWKnowledge.Import(lOOther.Export()).ChantedFanlo, "False True True");

            // A knight's line, by name.
            lOKnown.SawString(3731, 35, "Sir Dorna");
            lOKnown.KnowsMantra(4, out UWKnowledge.Entry lOGar);
            Check.Str("knowledge: GAR told by a knight", lOGar.Source + " " + lOGar.Who, "Talk Sir Dorna");

            // Curse: half known when read, known with An and Sanct in the bag.
            UWPlayerData lOBag = UWPlayerData.FromPlain((byte[])pOData.InitialPlayer.PlainBytes.Clone());

            lOBag.ClearRunes();
            lOKnown.SawString(3, 112, null);

            UWKnowledge.SpellStateEnum leNoRunes = lOKnown.SpellState(UWKnowledgeSources.CurseSpell, lOBag, out UWKnowledge.Entry _);

            lOBag.TryLearnRuneStone(UWPlayerData.FirstRuneObjectId + (int)UWPlayerData.Rune.An);
            lOBag.TryLearnRuneStone(UWPlayerData.FirstRuneObjectId + (int)UWPlayerData.Rune.Sanct);

            Check.Str("knowledge: Curse read without its runes, then with An and Sanct",
                leNoRunes + " " + lOKnown.SpellState(UWKnowledgeSources.CurseSpell, lOBag, out UWKnowledge.Entry _), "Half Known");

            // Summon Monster's riddle, then cast.
            lOKnown.SawString(3, 171, null);
            UWKnowledge.SpellStateEnum leRiddle = lOKnown.SpellState(UWKnowledgeSources.SummonMonsterSpell, null, out UWKnowledge.Entry _);

            lOKnown.Cast(UWKnowledgeSources.SummonMonsterSpell);
            Check.Str("knowledge: Summon Monster's riddle, then cast",
                leRiddle + " " + lOKnown.SpellState(UWKnowledgeSources.SummonMonsterSpell, null, out UWKnowledge.Entry _), "Half Known");

            // INSAHN's pieces in the order seen, each once; NO and an unknown word never learned.
            lOKnown.SawCutscene(0x0D);
            lOKnown.SawCutscene(0x0B);
            lOKnown.SawCutscene(0x0D);
            lOKnown.SawCutscene(0x10);
            lOKnown.Chanted(UWShrineRules.EmptyMantra);
            lOKnown.Chanted(-1);

            Check.Str("knowledge: INSAHN's pieces as seen (hn, in, hn again, another cutscene); NO and a wrong word chanted",
                string.Join(" ", lOKnown.Pieces()) + " " + lOKnown.KnowsMantra(UWShrineRules.EmptyMantra, out UWKnowledge.Entry _), "HN IN False");

            // THE LIZARD WORDS (GitHub issue 20): Murgo's mime teaches a word; the lines show its meaning as the
            // setting says - whole words only, the apostrophe part of a word, the slip "isila" read as isili.
            UWKnowledge lOLizard = new UWKnowledge();

            lOLizard.SawString(UWLizardWords.MurgoBlock, 52, "Murgo");
            lOLizard.SawString(UWLizardWords.MurgoBlock, 84, "Murgo");

            Func<string, string> lFMeaning = psId => UWTextEnglish.Table.TryGetValue(psId, out string lsMeaning) ? lsMeaning : psId;
            string lsLearned = UWLizardWords.Translate("Bica, sor'click. Sseth.", lOWord => lOLizard.HasFlag(lOWord.Flag), lFMeaning);
            string lsAll = UWLizardWords.Translate("Tosa thes'click 'click-iriass? 'Click. Isili sel'a isila kri'kla.", lOWord => true, lFMeaning);

            Check.Str("lizard words: learned from Murgo's mimes (sseth, bica), the rest bare", lsLearned, "Bica [hello, goodbye], sor'click. Sseth [yes].");
            Check.Str("lizard words: all - whole words, apostrophes inside, the slip isila",
                lsAll, "Tosa [you] thes'click [hate] 'click-iriass [Sir Cabirus]? 'Click [no]. Isili [I] sel'a [give] isila [I] kri'kla [lurker].");
            Check.Str("lizard words: no word, the line itself", UWLizardWords.Translate("Sorry, this is Thesis.", lOWord => true, lFMeaning),
                "Sorry, this is Thesis.");

            // Every word has its meaning, and every lizard word of the six conversations is in the list.
            System.Collections.Generic.List<string> lsMissing = new System.Collections.Generic.List<string>();

            foreach (UWLizardWords.Word lOWord in UWLizardWords.Words)
            {
                if (!UWTextEnglish.Table.ContainsKey(lOWord.TextId))
                    lsMissing.Add(lOWord.TextId);
            }

            Check.Str("lizard words: every meaning a text of the port", string.Join(", ", lsMissing), string.Empty);

            int liTranslated = 0;

            foreach (int liSlot in UWLizardWords.ConversationSlots)
            {
                if (!pOData.Strings.Blocks.TryGetValue(3584 + liSlot, out UWStringBlock lOConversation))
                    continue;

                foreach (string lsLine in lOConversation.Strings)
                {
                    if (UWLizardWords.Translate(lsLine, lOWord => true, lFMeaning) != lsLine)
                        liTranslated++;
                }
            }

            Check.Bool("lizard words: the six conversations have lines with words (" + liTranslated + ")", liTranslated > 100, true);

            // The lines for UWR.json read back as written.
            string[] lsLines = lOKnown.Export();
            string[] lsAgain = UWKnowledge.Import(lsLines).Export();

            Check.Str("knowledge: the save's lines read back as written (" + lsLines.Length + " lines)",
                string.Join("|", lsAgain), string.Join("|", lsLines));
        }
    }
}
