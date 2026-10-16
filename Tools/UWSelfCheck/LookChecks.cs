using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Reference audit round 2 (2026-10-15): the look texts against the original's LookAt,
    /// do_of and CritterLook (UnderworldExhumed obj/LOOK.C) - the charges of the charged form
    /// only, the creature whose name is in lower case, the owner's "belonging to".
    /// </summary>
    public static partial class Program
    {
        private static void fCheckLook(DataImport pOData)
        {
            Check.Group("Look texts (UWItemDescriptions, LookAt and CritterLook)");

            fCheckLookCharges(pOData);
            fCheckLookCreatures(pOData);
            fCheckLookOwner(pOData);
            fCheckLookBooks(pOData);
        }

        /// <summary>checkSpell with how 0 (A75): a thing in the world with a charged spell casts it
        /// and spends a charge; a wand the player touches does not; the uncharged form does not.
        /// The shipped sceptre (level 5, 59/54) carries Ally, which does nothing with a thing as
        /// the caster.</summary>
        private static void fCheckWorldObjectSpell(DataImport pOData)
        {
            Check.Group("World objects with a charged spell (UWItemSpellRules.TryCastFromWorldObject)");

            List<UWObject> lOMaster;
            UWObject lOSceptre = fThingWithSpell(0xAA, 0x4, 0x25, 5, out lOMaster);

            fExpectBool("thing spell: the sceptre casts", UWItemSpellRules.TryCastFromWorldObject(lOSceptre, lOMaster, true,
                out int liMajor, out int liMinor), true);
            fExpectInt("thing spell: ... rune spell 37's class", liMajor, UWRunicMagic.AllSpells[0x25].MajorClass);
            fExpectBool("thing spell: ... which does nothing with a thing as the caster (Ally)",
                UWTrapRules.SpellTrapCastsFor(liMajor, false, false), false);
            fExpectInt("thing spell: ... and spends a charge, 5 to 4", lOMaster[1].Quality, 4);

            UWObject lOWand = fThingWithSpell(0x98, 0x4, 0x25, 5, out lOMaster);

            fExpectBool("thing spell: a wand the player touches casts nothing",
                UWItemSpellRules.TryCastFromWorldObject(lOWand, lOMaster, true, out liMajor, out liMinor), false);
            fExpectInt("thing spell: ... and keeps its charges", lOMaster[1].Quality, 5);
            fExpectBool("thing spell: ... another user would cast",
                UWItemSpellRules.TryCastFromWorldObject(lOWand, lOMaster, false, out liMajor, out liMinor), true);

            UWObject lOPlain = fThingWithSpell(0xAA, 0x0, 0x25, 5, out lOMaster);

            fExpectBool("thing spell: the uncharged form casts nothing",
                UWItemSpellRules.TryCastFromWorldObject(lOPlain, lOMaster, true, out liMajor, out liMinor), false);

            UWObject lOClass = fThingWithSpell(0xAA, 0x4, 0x45, 3, out lOMaster);

            fExpectBool("thing spell: an item class spell (link 0x45) casts", UWItemSpellRules.TryCastFromWorldObject(lOClass, lOMaster,
                true, out liMajor, out liMinor) && liMajor == 13 && liMinor == 5, true);
        }

        /// <summary>A thing whose chain holds one spell object with these flags, link and quality.</summary>
        private static UWObject fThingWithSpell(int piId, int piFlags, int piLink, int piCharges, out List<UWObject> pOMaster)
        {
            UWObject lOThing = new UWObject((ushort)piId) { Quantity = 1 };
            UWObject lOSpell = new UWObject((ushort)UWEnchantment.SpellObjectId)
            {
                Flags = (ushort)piFlags,
                Quantity = (ushort)piLink,
                Quality = (ushort)piCharges
            };

            pOMaster = new List<UWObject> { new UWObject(0), lOSpell };

            return lOThing;
        }

        /// <summary>UseBook and BookLook: ID_FLAG10 plays cutscene 0x100 + the text number (the map
        /// piece, level 7 24/62, text 8: CS410), a text number from 0x100 on reads without "You
        /// read the" (the recipe).</summary>
        private static void fCheckLookBooks(DataImport pOData)
        {
            UWObject lOPiece = new UWObject(0x138) { HasQuantity = true, Quantity = 0x208, Flags = 0x2 };
            UWObject lOPlain = new UWObject(0x138) { HasQuantity = true, Quantity = 0x208 };
            UWObject lORecipe = new UWObject(0x13E) { HasQuantity = true, Quantity = 0x301 };

            fExpectInt("book: the map piece plays cutscene 0x108", UWObjectMechanics.GetBookCutscene(lOPiece), 0x108);
            Check.Str("book: ... from CS410.N01", UWCutscene.FileName(UWObjectMechanics.GetBookCutscene(lOPiece), 1), "CS410.N01");
            fExpectInt("book: the same scroll without the flag plays none", UWObjectMechanics.GetBookCutscene(lOPlain), -1);
            fExpectBool("book: ... and reads with the header", UWObjectMechanics.ReadsWithHeader(lOPlain), true);
            fExpectBool("book: the recipe reads without it", UWObjectMechanics.ReadsWithHeader(lORecipe), false);
            fExpectBool("book: ... and stays the recipe", UWStewRules.IsRecipe(lORecipe), true);
            lORecipe.Flags = 0x2;
            fExpectBool("book: a flagged recipe plays its cutscene instead", UWStewRules.IsRecipe(lORecipe), false);
            Check.Str("cutscene: the intro is CS000.N01", UWCutscene.FileName(0, 1), "CS000.N01");

            // The one flagged scroll UW1 places (B13): level 7 (index 6), 24/62, text 8. The list
            // holds four more flagged readables in free slots (tile 0/0), never placed.
            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > 6 ? pOData.Levels[6] : null;
            int liText = -1;

            if (lOLevel != null && lOLevel.Masterlist != null)
            {
                foreach (UWObject lOObject in lOLevel.Masterlist)
                {
                    if (lOObject != null && lOObject.TileX == 24 && lOObject.TileY == 62
                        && lOObject.GetCategory() == UWObject.ObjectCategoryEnum.BooksAndScrolls
                        && UWObjectMechanics.GetBookCutscene(lOObject) >= 0)
                        liText = UWObjectMechanics.GetBookTextNumber(lOObject);
                }
            }

            fExpectInt("book: the flagged scroll on level 7 at 24/62 has text 8", liText, 8);
        }

        private const int LookWandId = 0x98;

        /// <summary>do_of: " with N full charges" only when the chained spell object carries
        /// ID_FLAG11 (Flags bit 2), never for a curse (major 9).</summary>
        private static void fCheckLookCharges(DataImport pOData)
        {
            fExpectBool("charges: the charged form shows its charges",
                fDescribeWand(pOData, 0x4, 0x05, 5).Contains("with 5 full charges"), true);
            fExpectBool("charges: the plain form shows none (ID_FLAG11 clear)",
                fDescribeWand(pOData, 0x0, 0x25, 5).Contains("charge"), false);
            fExpectBool("charges: a curse shows none",
                fDescribeWand(pOData, 0x0, 0x90, 5).Contains("charge"), false);
            fExpectBool("charges: ... and reads cursed",
                fDescribeWand(pOData, 0x0, 0x90, 5).Contains("cursed"), true);
        }

        /// <summary>A wand whose chain holds one spell object with these flags, value and quality,
        /// looked at with the full Lore result.</summary>
        private static string fDescribeWand(DataImport pOData, int piFlags, int piValue, int piCharges)
        {
            UWObject lOWand = new UWObject(LookWandId) { Quantity = 1 };
            UWObject lOSpell = new UWObject((ushort)UWEnchantment.SpellObjectId)
            {
                Flags = (ushort)piFlags,
                Quantity = (ushort)piValue,
                Quality = (ushort)piCharges
            };
            List<UWObject> lOList = new List<UWObject> { new UWObject(0), lOSpell };

            return UWItemDescriptions.DescribeItem(lOWand, pOData, 0, lOList, UWLoreCheck.ResultNamed) ?? string.Empty;
        }

        /// <summary>CritterLook: a lower-case name replaces the kind, an upper-case one follows it
        /// with "named"; from whoami 0xF0 (but 0xFF) no attitude word.</summary>
        private static void fCheckLookCreatures(DataImport pOData)
        {
            int liLower = fFindWhoami(pOData, "head bandit");
            int liUpper = -1;

            for (int liWho = 1; liWho < 0xF0 && liUpper < 0; liWho++)
            {
                string lsName = fPartnerName(pOData, liWho);

                if (lsName.Length > 1 && lsName[0] >= 'A' && lsName[0] <= 'Z' && lsName.IndexOf('_') < 0)
                    liUpper = liWho;
            }

            fExpectBool("creature: block 7 holds the head bandit and an upper-case name", liLower > 0 && liUpper > 0, true);

            if (liLower <= 0 || liUpper <= 0)
                return;

            string lsUpper = fPartnerName(pOData, liUpper);

            Check.Str("creature: a lower-case name replaces the kind",
                UWItemDescriptions.DescribeCreature(fCreature(liLower, 0), "an_outcast", pOData), "You see a hostile head bandit.");
            Check.Str("creature: an upper-case name follows the kind",
                UWItemDescriptions.DescribeCreature(fCreature(liUpper, 2), "an_outcast", pOData),
                "You see a mellow outcast named " + lsUpper + ".");
            Check.Str("creature: no name, the kind after the attitude",
                UWItemDescriptions.DescribeCreature(fCreature(0, 3), "a_mountainman&mountainmen", pOData),
                "You see a friendly mountainman.");
            // A whoami from 0xF0 with a kind (latent, N2): no attitude word; the lower-case name
            // "a_bolt of lightning" replaces the kind and loses its article (fix_name_string with
            // article 0, as the kind is there).
            Check.Str("creature: whoami 0xF0 is the bolt of lightning", fPartnerName(pOData, 0xF0), "a_bolt of lightning");
            Check.Str("creature: from whoami 0xF0 no attitude word, the lower-case name without article",
                UWItemDescriptions.DescribeCreature(fCreature(0xF0, 0), "an_outcast", pOData), "You see bolt of lightning.");
            Check.Str("creature: without a kind the name stands alone",
                UWItemDescriptions.DescribeCreature(fCreature(liUpper, 0), "", pOData), "You see " + lsUpper + ".");
            fExpectBool("creature: neither kind nor name gives nothing",
                UWItemDescriptions.DescribeCreature(fCreature(0, 0), "", pOData) == null, true);
        }

        private static UWNpc fCreature(int piWho, int piAttitude)
        {
            return new UWNpc(0x40) { NPCwhoami = (byte)piWho, NPCAttitude = (byte)piAttitude };
        }

        private static string fPartnerName(DataImport pOData, int piWho)
        {
            return UWText.Block(pOData, UWConversations.PartnerNameStringBlock, UWConversations.GetPartnerNameIndex(piWho)).Trim();
        }

        private static int fFindWhoami(DataImport pOData, string psName)
        {
            for (int liWho = 1; liWho < 0x100; liWho++)
            {
                if (fPartnerName(pOData, liWho) == psName)
                    return liWho;
            }

            return -1;
        }

        /// <summary>LookAt: "belonging to" for an owner field above 0 whose race is at most 0x1B -
        /// 0x20 is "a denizen of the area" (race 0), race 28 is not named.</summary>
        private static void fCheckLookOwner(DataImport pOData)
        {
            int liOwnable = -1;

            for (int liId = 0; liId < 0x100 && liOwnable < 0; liId++)
            {
                if (pOData.CommonObjectProperties != null
                    && pOData.CommonObjectProperties.TryGet(liId, out UWCommonObjectProperties.Entry lOEntry) && lOEntry.CanHaveOwner)
                    liOwnable = liId;
            }

            fExpectBool("owner: COMOBJ has an ownable item", liOwnable >= 0, true);

            if (liOwnable < 0)
                return;

            string lsDenizen = UWText.General(pOData, UWItemDescriptions.FirstOwnerNameMessage);

            Check.Str("owner: 0x20 belongs to race 0", UWItemDescriptions.DescribeOwner(
                new UWObject((ushort)liOwnable) { Owner = 0x20 }, pOData), " belonging to" + lsDenizen);
            fExpectBool("owner: ... a denizen of the area", lsDenizen.Contains("denizen"), true);
            Check.Str("owner: race 0x1B is named", UWItemDescriptions.DescribeOwner(
                new UWObject((ushort)liOwnable) { Owner = 0x1B }, pOData),
                " belonging to" + UWText.General(pOData, UWItemDescriptions.FirstOwnerNameMessage + 0x1B));
            Check.Str("owner: race 28 is not", UWItemDescriptions.DescribeOwner(
                new UWObject((ushort)liOwnable) { Owner = 28 }, pOData), string.Empty);
            Check.Str("owner: 0 is nobody", UWItemDescriptions.DescribeOwner(
                new UWObject((ushort)liOwnable) { Owner = 0 }, pOData), string.Empty);
        }
    }
}
