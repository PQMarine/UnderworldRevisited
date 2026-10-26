using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// TRANSLATION WAVE 2 (2026-10-26): the German grammar (UWGrammarGerman) on made-up names - every
    /// string here is invented, nothing of the game: the three genders, a plural-only name, the
    /// definite and the missing article, a weak masculine's oblique form, the nominative, accusative
    /// and dative, counts and plurals, condition words, "magisch" and "verflucht" inflected, a word
    /// ending in e, the creature with its attitude and a name that follows or replaces its kind, the
    /// owner, the enchantment and its charges, identify_inv's name, and a name left in the English
    /// format read by the English rules; the name format's check (validate-tsv's warning) and the
    /// grammar a language code takes. The frames come from a made-up de.tsv put in force for the
    /// group. With the data: a made-up de.strings.tsv over the player's strings - the grammar chosen by
    /// the file's code, and the look, the stack, the owner, the creature, the bones, the wall,
    /// identify_inv and the partner's name through the game's own entry points.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>The frames of the made-up German port texts (Languages/de.tsv's grammar ids).</summary>
        private const string GermanFrames =
            "grammar.look.you_see\tDu siehst {0}.\n"
            + "grammar.look.named\t{0} namens {1}\n"
            + "grammar.look.owner\t, Eigentum{0}\n"
            + "grammar.look.of\t (Zauber: {0})\n"
            + "grammar.look.charges.none\t ohne volle Ladungen\n"
            + "grammar.look.charges.one\t mit 1 voller Ladung\n"
            + "grammar.look.charges.many\t mit {0} vollen Ladungen\n"
            + "grammar.word.magical\tmagisch\n"
            + "grammar.word.cursed\tverflucht\n"
            + "grammar.bones.adventurer\teinem Abenteurer\n";

        private const string Ae = "ä";

        private const string Ue = "ü";

        private static void fCheckGrammar()
        {
            Check.Group("German grammar (UWGrammarGerman, made-up names - translation wave 2)");

            UWTextLanguageFile lOFrames = UWTextLanguageFile.Parse(GermanFrames);

            Check.Int("frames: the made-up de.tsv reads whole", lOFrames.Texts.Count + lOFrames.Problems.Count * 100, 10);

            try
            {
                UWText.UseLanguage("de", lOFrames);

                fCheckGrammarChoice();
                fCheckGrammarNames();
                fCheckGrammarLook();
                fCheckGrammarCreatures();
                fCheckGrammarIdentify();
            }
            finally
            {
                UWText.UseEnglish();
            }
        }

        private static void fCheckGrammarChoice()
        {
            Check.Str("choice: de is German", UWGrammar.For("de").Code, "de");
            Check.Str("choice: a region of it too", UWGrammar.For("DE-at").Code + UWGrammar.For("de_CH").Code, "dede");
            // Since translation wave 4 a code without a class of its own takes the generic grammar under
            // its own code (UWGrammarGeneric); en and nothing stay English.
            Check.Str("choice: deu and xx are generic, en and nothing English", UWGrammar.For("deu").Code + "|" + UWGrammar.For("en").Code + "|"
                + UWGrammar.For("xx").Code + "|" + UWGrammar.For(null).Code, "deu|en|xx|en");
            Check.Str("choice: the code of a pack's file", UWGrammar.CodeOfPackFile("Languages/de.strings.tsv") + "|"
                + UWGrammar.CodeOfPackFile("de.tsv"), "de|");
            Check.Str("letters: German types the umlauts and the sharp s", fCodes(UWGrammar.German.TypeableLetters), "E4 F6 FC C4 D6 DC DF");
            Check.Str("letters: English none", UWGrammar.English.TypeableLetters, string.Empty);

            IUWGrammar lIGerman = UWGrammar.German;

            Check.Str("format: a name with gender and plural is fine", lIGerman.CheckName("m:Wumpel&Wumpels") ?? "fine", "fine");
            Check.Bool("format: without a prefix it warns", lIGerman.CheckName("Wumpel") != null && lIGerman.CheckName("x:Wumpel") != null, true);
            Check.Bool("format: an empty name or plural warns", lIGerman.CheckName("m:") != null && lIGerman.CheckName("m:Wumpel&") != null, true);
            Check.Bool("format: English has no format to check", UWGrammar.English.CheckName("Wumpel") == null, true);
        }

        private static void fCheckGrammarNames()
        {
            IUWGrammar lIGerman = UWGrammar.German;

            Check.Str("name: bare, plural, with article", lIGerman.Name("m:Wumpel&Wumpels", false, false, UWGrammarCase.Nominative) + "|"
                + lIGerman.Name("m:Wumpel&Wumpels", true, false, UWGrammarCase.Nominative) + "|"
                + lIGerman.Name("m:Wumpel&Wumpels", false, true, UWGrammarCase.Nominative), "Wumpel|Wumpels|ein Wumpel");
            Check.Str("name: the articles by gender, accusative", lIGerman.Name("m:Wumpel", false, true, UWGrammarCase.Accusative) + "|"
                + lIGerman.Name("f:Zirbe", false, true, UWGrammarCase.Accusative) + "|"
                + lIGerman.Name("n:Glimmding", false, true, UWGrammarCase.Accusative), "einen Wumpel|eine Zirbe|ein Glimmding");
            Check.Str("name: dative", lIGerman.Name("m:Wumpel", false, true, UWGrammarCase.Dative) + "|"
                + lIGerman.Name("f:Zirbe", false, true, UWGrammarCase.Dative), "einem Wumpel|einer Zirbe");
            Check.Str("name: a weak masculine's oblique form", lIGerman.Name("m:Murkel|Murkeln&Murkeln", false, true, UWGrammarCase.Nominative) + "|"
                + lIGerman.Name("m:Murkel|Murkeln&Murkeln", false, true, UWGrammarCase.Accusative), "ein Murkel|einen Murkeln");
            Check.Str("name: definite and none", lIGerman.Name("md:Oberwumpel der Tiefe", false, true, UWGrammarCase.Accusative) + "|"
                + lIGerman.Name("fo:Schmiere", false, true, UWGrammarCase.Accusative) + "|"
                + lIGerman.Name("md:Oberwumpel&Oberwumpel", true, true, UWGrammarCase.Dative), "den Oberwumpel der Tiefe|Schmiere|den Oberwumpel");
            Check.Str("name: a plural-only name, capital letters, a blank as _", lIGerman.Name("P:Klunker_bunt", false, true, UWGrammarCase.Accusative),
                "Klunker bunt");
            Check.Str("name: the added plural", lIGerman.AddedPlural("m:Wumpel&Wumpels") + "|" + lIGerman.AddedPlural("f:Zirbe") + "|"
                + lIGerman.AddedPlural("an_axe") + "|" + lIGerman.AddedPlural(null), "Wumpels|Zirbe|axes|s");
            Check.Str("name: a person's name loses its prefix", lIGerman.PersonName("m:Obergrummler") + "|" + lIGerman.PersonName("Knurz"),
                "Obergrummler|Knurz");
            Check.Str("name: one left English reads as English", lIGerman.Name("a_wumpel&wumpels", false, true, UWGrammarCase.Accusative) + "|"
                + lIGerman.Name("an_ogle&ogles", true, false, UWGrammarCase.Dative), "a wumpel|ogles");
            Check.Str("inflect: the last word, an e not doubled, no letter no ending", UWGrammarGerman.Inflect("etwas zerknautscht", "en") + "|"
                + UWGrammarGerman.Inflect("m" + Ue + "de", "en") + "|" + UWGrammarGerman.Inflect("m" + Ue + "de", "e") + "|"
                + UWGrammarGerman.Inflect("Stufe 3", "en"), "etwas zerknautschten|m" + Ue + "den|m" + Ue + "de|Stufe 3");
        }

        private static void fCheckGrammarLook()
        {
            IUWGrammar lIGerman = UWGrammar.German;

            Check.Str("look: masculine with a condition, accusative", lIGerman.LookSentence(
                lIGerman.ItemPhrase("m:Wumpel&Wumpels", 1, "etwas zerknautscht", false, false)), "Du siehst einen etwas zerknautschten Wumpel.");
            Check.Str("look: feminine", lIGerman.ItemPhrase("f:Zirbe&Zirben", 1, "stark verbeult", false, false), "eine stark verbeulte Zirbe");
            Check.Str("look: neuter, magical", lIGerman.ItemPhrase("n:Glimmding&Glimmdinge", 1, "angeknackst", false, true),
                "ein angeknackstes magisches Glimmding");
            Check.Str("look: cursed after the condition word", lIGerman.ItemPhrase("f:Zirbe&Zirben", 1, "z" + Ae + "h", true, false),
                "eine z" + Ae + "he verfluchte Zirbe");
            Check.Str("look: a stack, the count and the plural", lIGerman.ItemPhrase("m:Wumpel&Wumpels", 3, "etwas zerknautscht", false, false),
                "3 etwas zerknautschte Wumpels");
            Check.Str("look: a stack without a plural form", lIGerman.ItemPhrase("f:Zirbe", 12, null, false, false), "12 Zirbe");
            Check.Str("look: plural only, no article", lIGerman.ItemPhrase("p:Klunker", 1, "angeknackst", false, false), "angeknackste Klunker");
            Check.Str("look: no article, the strong ending", lIGerman.ItemPhrase("fo:Schmiere", 1, "z" + Ae + "h", false, false), "z" + Ae + "he Schmiere");
            Check.Str("look: definite, the weak ending", lIGerman.ItemPhrase("nd:Ur-Glimmding", 1, "angeknackst", false, false),
                "das angeknackste Ur-Glimmding");
            Check.Str("look: a weak masculine in the accusative", lIGerman.ItemPhrase("m:Murkel|Murkeln&Murkeln", 1, "grob", false, false),
                "einen groben Murkeln");
            Check.Str("look: a door with its condition word", lIGerman.ScenePhrase("f:Klapptuer&Klapptueren", "stark verbeult"),
                "eine stark verbeulte Klapptuer");
            Check.Str("look: a wall with and without the format", lIGerman.ChunkPhrase("f:Glitzerwand") + "|" + lIGerman.ChunkPhrase("eine Glitzerwand"),
                "eine Glitzerwand|eine Glitzerwand");
            Check.Str("look: a talisman's name, definite", lIGerman.LiteralPhrase("md:Ring der Probe") + "|" + lIGerman.LiteralPhrase("die Probe"),
                "den Ring der Probe|die Probe");
            Check.Str("look: the owner and the enchantment with its charges", lIGerman.EnchantmentPart("Funkelei", 3)
                + lIGerman.OwnerPart(" eines Grummlers"), " (Zauber: Funkelei) mit 3 vollen Ladungen, Eigentum eines Grummlers");
            Check.Str("look: one and no charge", lIGerman.EnchantmentPart(null, 1) + "|" + lIGerman.EnchantmentPart(null, 0),
                " mit 1 voller Ladung| ohne volle Ladungen");
            Check.Str("look: a name left English follows the English rules", lIGerman.ItemPhrase("a_wumpel&wumpels", 1, "etwas zerknautscht", false, false),
                UWGrammar.English.ItemPhrase("a_wumpel&wumpels", 1, "etwas zerknautscht", false, false));
            Check.Str("look: ... its plural too", lIGerman.ItemPhrase("a_wumpel&wumpels", 2, null, false, true), "2 magisch wumpels");
        }

        private static void fCheckGrammarCreatures()
        {
            IUWGrammar lIGerman = UWGrammar.German;

            Check.Str("creature: attitude and kind", lIGerman.CreatureLook("grummelig", "m:Grummler&Grummler", null),
                "Du siehst einen grummeligen Grummler.");
            Check.Str("creature: feminine kind", lIGerman.CreatureLook("freundlich", "f:Zirpe&Zirpen", null), "Du siehst eine freundliche Zirpe.");
            Check.Str("creature: a name without prefix follows the kind", lIGerman.CreatureLook("grummelig", "m:Grummler", "Knurz"),
                "Du siehst einen grummeligen Grummler namens Knurz.");
            Check.Str("creature: a name with prefix replaces the kind", lIGerman.CreatureLook("grummelig", "m:Grummler", "m:Obergrummler"),
                "Du siehst einen grummeligen Obergrummler.");
            Check.Str("creature: no kind, the name alone with its article", lIGerman.CreatureLook(null, null, "md:Schlunz der Leere") + "|"
                + lIGerman.CreatureLook("grummelig", null, "Knurz"), "Du siehst den Schlunz der Leere.|Du siehst Knurz.");
            Check.Str("creature: no attitude word", lIGerman.CreatureLook(null, "n:Glimmwesen", null), "Du siehst ein Glimmwesen.");
            Check.Bool("creature: neither kind nor name", lIGerman.CreatureLook("grummelig", null, null) == null, true);
            Check.Str("creature: a kind left English follows the English rules",
                lIGerman.CreatureLook("grummelig", "an_ogle&ogles", "Knurz"), "Du siehst a grummelig ogle namens Knurz.");
            Check.Str("creature: an English name in lower case too", lIGerman.CreatureLook("grummelig", "m:Grummler", "head ogle"),
                "Du siehst a grummelig head ogle.");
        }

        private static void fCheckGrammarIdentify()
        {
            IUWGrammar lIGerman = UWGrammar.German;

            Check.Str("identify: magical with and without article, nominative", lIGerman.IdentifyName("n:Glimmding&Glimmdinge", 1, true, false, true) + "|"
                + lIGerman.IdentifyName("n:Glimmding&Glimmdinge", 1, true, false, false), "ein magisches Glimmding|magisches Glimmding");
            Check.Str("identify: cursed masculine", lIGerman.IdentifyName("m:Wumpel", 1, false, true, true), "ein verfluchter Wumpel");
            Check.Str("identify: a pile", lIGerman.IdentifyName("n:Glimmding&Glimmdinge", 3, true, false, true), "3 magische Glimmdinge");
            Check.Str("identify: plain", lIGerman.IdentifyName("f:Zirbe", 1, false, false, true) + "|" + lIGerman.IdentifyName("f:Zirbe", 0, false, false, false),
                "eine Zirbe|Zirbe");
            Check.Str("identify: English left as it is", lIGerman.IdentifyName("an_ogle", 2, false, false, true),
                UWGrammar.English.IdentifyName("an_ogle", 2, false, false, true));
        }

        // ------------------------------------------------- With the data: a made-up German pack

        private static void fCheckGrammarData(DataImport pOData)
        {
            Check.Group("German grammar on the data (a made-up de.strings.tsv - translation wave 2)");

            if (pOData == null || pOData.CommonObjectProperties == null)
            {
                Check.Fail("grammar data: data", "not loaded");

                return;
            }

            // Three ownable things with a condition word at quality 30, and their words' lines in block 5.
            List<int> liIds = new List<int>();
            HashSet<int> liWords = new HashSet<int>();

            for (int liId = 0; liId < 0x40 && liIds.Count < 3; liId++)
            {
                if (!pOData.CommonObjectProperties.TryGet(liId, out UWCommonObjectProperties.Entry lOEntry) || !lOEntry.CanHaveOwner
                    || UWItemDescriptions.GetItemConditionWord(new UWObject((ushort)liId) { Quality = 30 }, pOData) == null)
                    continue;

                liIds.Add(liId);
                liWords.Add(UWCombat.GetConditionStringIndex(lOEntry.QualityType, 30, lOEntry.QualityClass) + 1);
            }

            Check.Int("grammar data: three ownable things with a condition word", liIds.Count, 3);

            if (liIds.Count < 3)
                return;

            const int liKind = 0x41;
            const int liOwnerRace = 6;
            const int liBones = 0xC2;
            string lsFolder = Path.Combine(Path.GetTempPath(), "UWSelfCheck-grammar-" + Environment.ProcessId);
            StringBuilder lOPack = new StringBuilder("# made-up names, nothing of the game\n");

            lOPack.Append("4\t").Append(liIds[0] + 1).Append("\tm:Wumpel&Wumpels\n");
            lOPack.Append("4\t").Append(liIds[1] + 1).Append("\tf:Zirbe&Zirben\n");
            lOPack.Append("4\t").Append(liIds[2] + 1).Append("\tn:Glimmding&Glimmdinge\n");
            lOPack.Append("4\t").Append(liKind + 1).Append("\tm:Grummler&Grummler\n");
            lOPack.Append("1\t").Append(UWItemDescriptions.FirstOwnerNameMessage + liOwnerRace).Append("\t eines Grummlers\n");
            lOPack.Append("1\t").Append(0x16 + 1).Append("\tDas stammt wohl von \n");
            lOPack.Append("5\t97\tgrummelig\n");
            lOPack.Append("10\t1\tf:Glitzerwand\n");
            lOPack.Append("7\t").Append(UWConversations.GetPartnerNameIndex(1)).Append("\tm:Obergrummler\n");

            foreach (int liWord in liWords)
                lOPack.Append("5\t").Append(liWord).Append("\tetwas zerknautscht\n");

            try
            {
                Directory.CreateDirectory(lsFolder);

                string lsGerman = Path.Combine(lsFolder, "de.strings.tsv");
                string lsOther = Path.Combine(lsFolder, "xx.strings.tsv");

                File.WriteAllText(lsGerman, lOPack.ToString(), new UTF8Encoding(false));
                File.WriteAllText(lsOther, lOPack.ToString(), new UTF8Encoding(false));

                Check.Str("grammar data: the pack's code chooses it", new UWGameData(pOData.DataPath, lsGerman).Grammar.Code + "|"
                    + new UWGameData(pOData.DataPath, lsOther).Grammar.Code + "|" + new UWGameData(pOData.DataPath).Grammar.Code + "|"
                    + new UWGameData(pOData.DataPath, lsGerman + ".missing").Grammar.Code, "de|xx|en|en");

                DataImport lOGerman = new DataImport(pOData.DataPath, null, lsGerman);

                UWText.UseLanguage("de", UWTextLanguageFile.Parse(GermanFrames));

                UWObject lOWumpel = new UWObject((ushort)liIds[0]) { Quality = 30, Owner = liOwnerRace };
                UWObject lOZirben = new UWObject((ushort)liIds[1]) { Quality = 30, HasQuantity = true, Quantity = 3 };
                UWObject lOGlimm = new UWObject((ushort)liIds[2]) { Quality = 30 };

                Check.Str("grammar data: the look, owned", UWItemDescriptions.DescribeItem(lOWumpel, lOGerman, 0, null, UWLoreCheck.ResultUnknown),
                    "Du siehst einen etwas zerknautschten Wumpel, Eigentum eines Grummlers.");
                Check.Str("grammar data: a stack", UWItemDescriptions.DescribeItem(lOZirben, lOGerman, 0, null, UWLoreCheck.ResultUnknown),
                    "Du siehst 3 etwas zerknautschte Zirben.");
                Check.Str("grammar data: neuter", UWItemDescriptions.DescribeItem(lOGlimm, lOGerman, 0, null, UWLoreCheck.ResultUnknown),
                    "Du siehst ein etwas zerknautschtes Glimmding.");
                Check.Str("grammar data: the scenery look", UWItemDescriptions.DescribeScenery(UWText.RawObjectName(lOGerman, liIds[1]),
                    "etwas zerknautscht", null, lOGerman), "Du siehst eine etwas zerknautschte Zirbe.");
                Check.Str("grammar data: the creature", UWItemDescriptions.DescribeCreature(
                    new UWNpc(liKind) { NPCwhoami = 0, NPCAttitude = 0 }, UWText.RawObjectName(lOGerman, liKind), lOGerman),
                    "Du siehst einen grummeligen Grummler.");
                Check.Str("grammar data: the bones, dative", UWItemDescriptions.SpecialLookLine(
                    new UWObject(liBones) { Owner = liKind - 0x40, Quantity = 1 }, lOGerman), "Das stammt wohl von einem Grummler.");
                Check.Str("grammar data: the wall", UWItemDescriptions.DescribeChunk(UWText.Block(lOGerman, 10, 1), lOGerman),
                    "Du siehst eine Glitzerwand.");
                Check.Str("grammar data: identify_inv", UWItemDescriptions.IdentifyName(lOGlimm, lOGerman, null, true, 0) + "|"
                    + UWItemDescriptions.IdentifyName(lOZirben, lOGerman, null, true, 0), "ein Glimmding|3 Zirben");
                Check.Str("grammar data: the names", UWText.ObjectName(lOGerman, liIds[0]) + "|" + UWText.ObjectName(lOGerman, liIds[1], true)
                    + "|" + UWText.ObjectName(lOGerman, liIds[2], false, true) + "|" + UWText.ObjectNameAddedPlural(lOGerman, liIds[0]),
                    "Wumpel|Zirben|ein Glimmding|Wumpels");
                Check.Str("grammar data: the partner's name", UWText.PartnerName(lOGerman, UWConversations.GetPartnerNameIndex(1)), "Obergrummler");

                // validate-tsv: the German format warned about, the English left alone.
                UWGameTextPack lOMixed = UWGameTextPack.Parse("4\t" + (liIds[0] + 1) + "\tWumpel\n4\t" + (liIds[1] + 1) + "\tf:Zirbe&\n4\t"
                    + (liIds[2] + 1) + "\tn:Glimmding&Glimmdinge\n");
                UWStrings lOEnglish = new UWStrings(File.ReadAllBytes(Path.Combine(pOData.DataPath, "STRINGS.PAK")));
                List<string> lsFound = new List<string>();

                foreach (UWGameTextPack.Finding lOFinding in lOMixed.Validate(lOEnglish, UWGrammar.German))
                    lsFound.Add((lOFinding.IsError ? "E" : "W") + lOFinding.Line);

                Check.Str("validate: German warns about a name without prefix and an empty plural", string.Join(",", lsFound), "W1,W2");
                Check.Int("validate: English does not", lOMixed.Validate(lOEnglish, UWGrammar.English).Count + lOMixed.Validate(lOEnglish).Count, 0);
            }
            catch (Exception lOError)
            {
                Check.Fail("grammar data: a German pack", lOError.GetType().Name + ": " + lOError.Message);
            }
            finally
            {
                UWText.UseEnglish();

                try
                {
                    Directory.Delete(lsFolder, true);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
