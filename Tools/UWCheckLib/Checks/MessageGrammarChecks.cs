using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// TRANSLATION STEP 3A (2026-10-26): the message lines UW.EXE assembles around a name, in German
    /// on made-up names - every German string here is invented (frames and names alike), nothing of
    /// the game. The grammar's forms for them (IUWGrammar.TheName with the definite article in three
    /// cases, YourName with "dein", TakesPluralVerb, Sentence's capital, the bags' label with its
    /// inflected "verflucht", a talisman's label) and their English forms; with the data, a made-up
    /// de.strings.tsv over the player's strings and made-up German frames: every moved sentence
    /// through the game's own entry point (UWItemMessages, UWEquipmentWear, UWTrapDisarming,
    /// UWRepairRules, UWPlayerVitals, UWShrineRules, UWText.ObjectLabel).
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>The made-up German frames of the message lines (Languages/de.tsv's msg ids).</summary>
        private const string GermanMessageFrames =
            "msg.item.cannot_use_on\t{0} lässt sich dafür nicht nehmen.\n"
            + "msg.item.too_full\t{0} ist zu voll.\n"
            + "msg.item.locked\t{0} ist zu.\n"
            + "msg.item.empty\t{0} ist leer.\n"
            + "msg.item.unnamed\tDing\n"
            + "msg.item.read\tDu liest {0}...\n"
            + "msg.item.taste\t{0}{1}\n"
            + "msg.item.no_ammunition\tDu hast keine {0} mehr.\n"
            + "msg.item.angered\t{0}{1}\n"
            + "msg.wear.damaged\t{0} wurde verbeult.\n"
            + "msg.wear.damaged_plural\t{0} wurden verbeult.\n"
            + "msg.wear.destroyed\t{0} ging kaputt.\n"
            + "msg.wear.destroyed_plural\t{0} gingen kaputt.\n"
            + "msg.trap.disarmed\t{0} an {1} ist entschärft.\n"
            + "msg.trap.set_off\tDu hast {0} ausgelöst\n"
            + "msg.trap.failed\tDas klappt nicht.\n"
            + "msg.repair.result\t{0}{1}.\n"
            + "msg.level_up\t{0}{1} erreicht.\n"
            + "msg.shrine.prompt\tSprich: \n"
            + "msg.shrine.list_comma\t, \n"
            + "msg.shrine.list_and\t und \n"
            + "msg.shrine.cup_and\t und \n"
            + "msg.shrine.cup_very_near\tganz nah\n"
            + "prompt.use_on_what\tWorauf {0} anwenden?\n"
            + "grammar.word.cursed\tverflucht\n";

        private static void fCheckMessageGrammar()
        {
            Check.Group("German message lines (made-up names - translation step 3A)");

            UWTextLanguageFile lOFrames = UWTextLanguageFile.Parse(GermanMessageFrames);

            Check.Int("frames: the made-up msg ids read whole", lOFrames.Texts.Count + lOFrames.Problems.Count * 100, 25);

            IUWGrammar lIGerman = UWGrammar.German;
            IUWGrammar lIEnglish = UWGrammar.English;

            try
            {
                UWText.UseLanguage("de", lOFrames);

                Check.Str("the name: definite by gender, nominative", lIGerman.TheName("m:Wumpel&Wumpels", UWGrammarCase.Nominative) + "|"
                    + lIGerman.TheName("f:Zirbe", UWGrammarCase.Nominative) + "|" + lIGerman.TheName("n:Glimmding", UWGrammarCase.Nominative) + "|"
                    + lIGerman.TheName("p:Klunker", UWGrammarCase.Nominative), "der Wumpel|die Zirbe|das Glimmding|die Klunker");
                Check.Str("the name: accusative and dative", lIGerman.TheName("m:Wumpel", UWGrammarCase.Accusative) + "|"
                    + lIGerman.TheName("m:Wumpel", UWGrammarCase.Dative) + "|" + lIGerman.TheName("f:Zirbe", UWGrammarCase.Dative) + "|"
                    + lIGerman.TheName("p:Klunker", UWGrammarCase.Dative), "den Wumpel|dem Wumpel|der Zirbe|den Klunker");
                Check.Str("the name: a weak masculine, the look's articles d and o", lIGerman.TheName("m:Murkel|Murkeln&Murkeln", UWGrammarCase.Accusative)
                    + "|" + lIGerman.TheName("md:Oberwumpel der Tiefe", UWGrammarCase.Dative) + "|" + lIGerman.TheName("fo:Schmiere", UWGrammarCase.Accusative),
                    "den Murkeln|dem Oberwumpel der Tiefe|die Schmiere");
                Check.Str("the name: adjectives inside it take the weak ending (step 3D)", lIGerman.TheName("m:roter Wumpel|roten Wumpel&rote Wumpels", UWGrammarCase.Nominative)
                    + "|" + lIGerman.TheName("m:roter Wumpel|roten Wumpel", UWGrammarCase.Dative) + "|" + lIGerman.TheName("n:glänzendes Glimmding", UWGrammarCase.Accusative)
                    + "|" + lIGerman.TheName("n:kleines rostiges Glimmding", UWGrammarCase.Dative) + "|" + lIGerman.TheName("f:zerbrochene Zirbe", UWGrammarCase.Dative)
                    + "|" + lIGerman.TheName("m:leuchtend blauer Wumpel", UWGrammarCase.Nominative),
                    "der rote Wumpel|dem roten Wumpel|das glänzende Glimmding|dem kleinen rostigen Glimmding|der zerbrochenen Zirbe|der leuchtend blaue Wumpel");
                Check.Str("the name: no adjective before the noun, or an article or preposition there, stays", lIGerman.TheName("n:Paar Klunker", UWGrammarCase.Dative)
                    + "|" + lIGerman.TheName("f:an eine Wand gekettete Zirbe", UWGrammarCase.Dative) + "|" + lIGerman.TheName("p:rote Klunker", UWGrammarCase.Dative)
                    + "|" + lIGerman.YourName("n:glänzendes Glimmding", UWGrammarCase.Nominative),
                    "dem Paar Klunker|der an eine Wand gekettete Zirbe|den roten Klunker|dein glänzendes Glimmding");
                Check.Str("the name: one left English is the English bare name", lIGerman.TheName("a_wumpel&wumpels", UWGrammarCase.Dative), "wumpel");
                Check.Str("yours: by gender and case", lIGerman.YourName("m:Wumpel", UWGrammarCase.Nominative) + "|"
                    + lIGerman.YourName("f:Zirbe", UWGrammarCase.Nominative) + "|" + lIGerman.YourName("n:Glimmding", UWGrammarCase.Nominative) + "|"
                    + lIGerman.YourName("p:Klunker", UWGrammarCase.Nominative) + "|" + lIGerman.YourName("m:Wumpel", UWGrammarCase.Accusative) + "|"
                    + lIGerman.YourName("f:Zirbe", UWGrammarCase.Dative), "dein Wumpel|deine Zirbe|dein Glimmding|deine Klunker|deinen Wumpel|deiner Zirbe");
                Check.Str("plural verb: a plural-only name, not the others; English by the s",
                    lIGerman.TakesPluralVerb("p:Klunker") + "|" + lIGerman.TakesPluralVerb("m:Wumpels") + "|" + lIGerman.TakesPluralVerb("some_wumpels")
                    + "|" + lIEnglish.TakesPluralVerb("a_wumpel&wumpels") + "|" + lIEnglish.TakesPluralVerb(null), "True|False|True|False|False");
                Check.Str("sentence: German starts with a capital, an umlaut too; English stays", lIGerman.Sentence("der Wumpel.") + "|"
                    + lIGerman.Sentence("äh.") + "|" + lIEnglish.Sentence("a wumpel grumbles."), "Der Wumpel.|Äh.|a wumpel grumbles.");
                Check.Str("sentence: German folds blanks in a row, English keeps them (step 3D)", lIGerman.Sentence("du hast Stufe  5 erreicht.\nSo   weit.")
                    + "|" + lIEnglish.Sentence("level  5"), "Du hast Stufe 5 erreicht.\nSo weit.|level  5");
                Check.Str("label: nominative, no article, inflected, a capital", lIGerman.ItemLabel("m:Wumpel&Wumpels", 1, false) + "|"
                    + lIGerman.ItemLabel("m:Wumpel&Wumpels", 1, true) + "|" + lIGerman.ItemLabel("f:Zirbe", 1, true) + "|"
                    + lIGerman.ItemLabel("n:Glimmding", 1, true) + "|" + lIGerman.ItemLabel("m:Wumpel&Wumpels", 3, true),
                    "Wumpel|Verfluchter Wumpel|Verfluchte Zirbe|Verfluchtes Glimmding|3 verfluchte Wumpels");
                Check.Str("label: English as the tooltip had it", lIEnglish.ItemLabel("a_wumpel&wumpels", 1, false) + "|"
                    + lIEnglish.ItemLabel("a_wumpel&wumpels", 3, false) + "|" + lIEnglish.ItemLabel("a_wumpel&wumpels", 2, true) + "|"
                    + lIEnglish.ItemLabel(null, 2, false), "Wumpel|3 Wumpels|2 Cursed wumpels|2 ");
                Check.Str("label: a talisman's name, the format and without", lIGerman.LiteralLabel("md:Ring der Probe") + "|"
                    + lIGerman.LiteralLabel("die Probe") + "|" + lIEnglish.LiteralLabel("the probe"), "Der Ring der Probe|Die Probe|The probe");
                Check.Str("English: the bare name for the frames", lIEnglish.TheName("a_wumpel&wumpels", UWGrammarCase.Dative) + "|"
                    + lIEnglish.YourName("an_ogle&ogles", UWGrammarCase.Accusative), "wumpel|ogle");
            }
            finally
            {
                UWText.UseEnglish();
            }

            Check.Str("English: the frames as the code wrote them", UWText.F("msg.item.locked", "chest") + "|" + UWText.F("msg.wear.damaged_plural", "leggings")
                + "|" + UWText.F("msg.trap.disarmed", "damage trap", "chest") + "|" + UWText.F("msg.item.read", "scroll"),
                "The chest is locked.|Your leggings were damaged.|The damage trap on the chest was successfully dearmed.|You read the scroll...");
        }

        // ------------------------------------------------- With the data: a made-up German pack

        private static void fCheckMessageGrammarData(DataImport pOData)
        {
            Check.Group("German message lines on the data (a made-up de.strings.tsv - translation step 3A)");

            if (pOData == null || pOData.Strings == null)
            {
                Check.Fail("message data: data", "not loaded");

                return;
            }

            const int liWumpel = 0x00;
            const int liZirbe = 0x01;
            const int liGlimm = 0x02;
            const int liKlunker = 0x03;
            const int liMurkel = 0x04;
            const int liTrap = 0x180;
            const int liKind = 0x41;
            string lsFolder = Path.Combine(Path.GetTempPath(), "UWSelfCheck-messages-" + Environment.ProcessId);
            StringBuilder lOPack = new StringBuilder("# made-up names and lines, nothing of the game\n");

            lOPack.Append("4\t").Append(liWumpel + 1).Append("\tm:Wumpel&Wumpels\n");
            lOPack.Append("4\t").Append(liZirbe + 1).Append("\tf:Zirbe&Zirben\n");
            lOPack.Append("4\t").Append(liGlimm + 1).Append("\tn:Glimmding&Glimmdinge\n");
            lOPack.Append("4\t").Append(liKlunker + 1).Append("\tp:Klunker\n");
            lOPack.Append("4\t").Append(liMurkel + 1).Append("\tm:Murkel|Murkeln&Murkeln\n");
            lOPack.Append("4\t").Append(liTrap + 1).Append("\tf:Schnappfalle&Schnappfallen\n");
            lOPack.Append("4\t").Append(liKind + 1).Append("\tm:Grummler&Grummler\n");
            lOPack.Append("1\t").Append(UWItemMessages.FirstTasteMessage + 4).Append("\t schmeckte süß.\n");
            lOPack.Append("1\t").Append(UWCritterRules.TheftMessageBase).Append("\t grollt dir.\n");
            lOPack.Append("1\t148\tDu hast Stufe\n");
            lOPack.Append("1\t217\tEs wird \n1\t220\tleicht\n1\t218\t sein, \n1\t219\t zu flicken. Versuchen?\n");
            lOPack.Append("1\t145\tHalb geflickt hast du \n");
            lOPack.Append("1\t30\tDu wirst besser in \n");
            lOPack.Append("1\t36\tder Kelch ist \n");

            try
            {
                Directory.CreateDirectory(lsFolder);

                string lsGerman = Path.Combine(lsFolder, "de.strings.tsv");

                File.WriteAllText(lsGerman, lOPack.ToString(), new UTF8Encoding(false));

                DataImport lOGerman = new DataImport(pOData.DataPath, null, lsGerman);

                UWText.UseLanguage("de", UWTextLanguageFile.Parse(GermanMessageFrames));

                Check.Str("message data: locked, too full, empty, cannot be used", UWItemMessages.IsLocked(lOGerman, liZirbe) + "|"
                    + UWItemMessages.IsTooFull(lOGerman, liWumpel) + "|" + UWItemMessages.IsEmpty(lOGerman, liGlimm) + "|"
                    + UWItemMessages.CannotBeUsedOnThat(lOGerman, liKlunker),
                    "Die Zirbe ist zu.|Der Wumpel ist zu voll.|Das Glimmding ist leer.|Die Klunker lässt sich dafür nicht nehmen.");
                Check.Str("message data: read and use on, accusative", UWItemMessages.ReadHeader(lOGerman, liMurkel) + "|"
                    + UWItemMessages.UseOnWhat(lOGerman, liWumpel), "Du liest den Murkeln...|Worauf den Wumpel anwenden?");
                Check.Str("message data: the taste", UWItemMessages.Taste(lOGerman, liGlimm, 4), "Das Glimmding schmeckte süß.");
                Check.Str("message data: no ammunition, the plural", UWItemMessages.NoAmmunition(lOGerman, liWumpel), "Du hast keine Wumpels mehr.");
                Check.Str("message data: the theft, a capital", UWItemMessages.Angered(lOGerman, liKind, 0), "Ein Grummler grollt dir.");
                Check.Str("message data: worn equipment, yours, the plural verb", UWEquipmentWear.Message(lOGerman, liZirbe, false) + "|"
                    + UWEquipmentWear.Message(lOGerman, liWumpel, true) + "|" + UWEquipmentWear.Message(lOGerman, liKlunker, false),
                    "Deine Zirbe wurde verbeult.|Dein Wumpel ging kaputt.|Deine Klunker wurden verbeult.");
                Check.Str("message data: disarmed (dative), set off (accusative), failed", UWTrapDisarming.DisarmedMessage(lOGerman, liTrap, liWumpel) + "|"
                    + UWTrapDisarming.SetOffMessage(lOGerman, liTrap) + "|" + UWTrapDisarming.FailedMessage,
                    "Die Schnappfalle an dem Wumpel ist entschärft.|Du hast die Schnappfalle ausgelöst|Das klappt nicht.");
                Check.Str("message data: the anvil", UWRepairRules.EstimateQuestion(lOGerman, liZirbe, 0) + "|"
                    + UWRepairRules.ResultMessage(lOGerman, UWRepairRules.OutcomeEnum.Partially, liWumpel),
                    "Es wird leicht sein, die Zirbe zu flicken. Versuchen?|Halb geflickt hast du den Wumpel.");
                Check.Str("message data: the level-up, words after the number", UWPlayerVitals.LevelUpMessage(lOGerman, 2) + "|"
                    + UWPlayerVitals.LevelUpMessage(lOGerman, 12), "Du hast Stufe 2 erreicht.|Du hast Stufe12 erreicht.");
                Check.Str("message data: the shrine", UWShrineRules.GetAdvancedLine(lOGerman, new List<string> { "Hieb", "Stich", "Wurf" }) + "|"
                    + UWShrineRules.GetCupLine(lOGerman, -1, false, 0, true) + "|" + UWShrineRules.Prompt,
                    "Du wirst besser in Hieb, Stich und Wurf.|Der Kelch ist ganz nah.|Sprich: ");
                Check.Str("message data: the bags' label", UWText.ObjectLabel(lOGerman, liWumpel, 3, true) + "|" + UWText.ObjectLabel(lOGerman, liZirbe, 1, true),
                    "3 verfluchte Wumpels|Verfluchte Zirbe");

                UWText.UseEnglish();

                Check.Str("message data: the English frames over the German names", UWItemMessages.IsLocked(lOGerman, liZirbe), "The die Zirbe is locked.");
                Check.Str("message data: English data stays English", UWItemMessages.IsLocked(pOData, liZirbe),
                    "The " + UWText.ObjectName(pOData, liZirbe) + " is locked.");
            }
            catch (Exception lOError)
            {
                Check.Fail("message data: a German pack", lOError.GetType().Name + ": " + lOError.Message);
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
