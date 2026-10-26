using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// EVERY LOOK SENTENCE THE GAME CAN BUILD from the player's data (translation wave 2,
    /// 2026-10-26): the names of block 4 in their four forms, the item look over every quality,
    /// count, owner and detail, the enchantments (the item as carrier and a chained spell with and
    /// without charges), identify_inv's name, the creature look over every kind, whoami and
    /// attitude, the scenery and door look with every condition word and owner, the walls and
    /// floors of block 10, SpecialLook's key and bones lines, and every object the nine levels
    /// hold looked at as it lies. Built from the engine-free entry points the game calls
    /// (UWItemDescriptions, UWText.ObjectName), so a change in the sentence building shows here.
    ///
    /// SINCE TRANSLATION STEP 3A (2026-10-26) also the other sentences the code builds around a
    /// game name or from pieces of the strings: the item messages (cannot be used, too full, locked,
    /// empty, use on what, read, taste, worn equipment, no ammunition, the theft that angers), the
    /// trap disarming, the anvil's estimate and outcome, the level-up line, the shrine's lines
    /// (advance, INSAHN's direction, the prompt) and the fixed lines of UW.EXE the engine-free code
    /// prints - recorded before they moved into the grammar and the text ids.
    ///
    /// The self-check pins a hash per family (Pins/look-strings.txt), so English stays byte-exact
    /// while the grammar moves (IUWGrammar); `uwdump looks` prints the lines - with --strings and a
    /// game-text pack the same sentences in that language. The lines hold the original's text:
    /// printed for the one who runs it, never written into the project.
    /// </summary>
    internal static class UWLookCatalog
    {
        /// <summary>One sentence: its family, what was looked at, the text.</summary>
        public struct Line
        {
            public string Family;

            public string Key;

            public string Text;
        }

        /// <summary>The families in the order they are built.</summary>
        public static readonly string[] Families =
        {
            "name", "quality", "count", "owner", "detail", "enchant", "charges", "identify",
            "creature", "scenery", "chunk", "special", "world",
            "message", "trap", "repair", "levelup", "shrine", "fixed", "label"
        };

        private const int ObjectCount = 512;

        private const int CreatureFirst = 0x40;

        private const int CreatureLast = 0x7F;

        /// <summary>Every line of every family, in a fixed order.</summary>
        public static List<Line> Build(DataImport pOData)
        {
            List<Line> lOLines = new List<Line>();

            if (pOData == null)
                return lOLines;

            fNames(pOData, lOLines);
            fQualities(pOData, lOLines);
            fCounts(pOData, lOLines);
            fOwners(pOData, lOLines);
            fDetails(pOData, lOLines);
            fEnchantments(pOData, lOLines);
            fCharges(pOData, lOLines);
            fIdentify(pOData, lOLines);
            fCreatures(pOData, lOLines);
            fScenery(pOData, lOLines);
            fChunks(pOData, lOLines);
            fSpecial(pOData, lOLines);
            fWorld(pOData, lOLines);
            fMessages(pOData, lOLines);
            fTrapMessages(pOData, lOLines);
            fRepair(pOData, lOLines);
            fLevelUp(pOData, lOLines);
            fShrine(pOData, lOLines);
            fFixed(pOData, lOLines);
            fLabels(pOData, lOLines);

            return lOLines;
        }

        /// <summary>Per family: "count sha256" over its lines (key TAB text, LF each), the first 16
        /// hex digits of the hash.</summary>
        public static SortedDictionary<string, string> Hashes(List<Line> pOLines)
        {
            Dictionary<string, StringBuilder> lOTexts = new Dictionary<string, StringBuilder>();
            Dictionary<string, int> lOCounts = new Dictionary<string, int>();

            foreach (Line lOLine in pOLines)
            {
                if (!lOTexts.TryGetValue(lOLine.Family, out StringBuilder lOText))
                {
                    lOText = new StringBuilder();
                    lOTexts[lOLine.Family] = lOText;
                    lOCounts[lOLine.Family] = 0;
                }

                lOText.Append(lOLine.Key).Append('\t').Append(lOLine.Text ?? "(null)").Append('\n');
                lOCounts[lOLine.Family]++;
            }

            SortedDictionary<string, string> lOHashes = new SortedDictionary<string, string>(StringComparer.Ordinal);

            using (SHA256 lOSha = SHA256.Create())
            {
                foreach (KeyValuePair<string, StringBuilder> lOPair in lOTexts)
                {
                    byte[] lyHash = lOSha.ComputeHash(Encoding.UTF8.GetBytes(lOPair.Value.ToString()));
                    StringBuilder lOHex = new StringBuilder();

                    for (int liAt = 0; liAt < 8; liAt++)
                        lOHex.Append(lyHash[liAt].ToString("x2", CultureInfo.InvariantCulture));

                    lOHashes[lOPair.Key] = lOCounts[lOPair.Key].ToString(CultureInfo.InvariantCulture) + " " + lOHex;
                }
            }

            return lOHashes;
        }

        private static void fAdd(List<Line> pOLines, string psFamily, string psKey, string psText)
        {
            pOLines.Add(new Line { Family = psFamily, Key = psKey, Text = psText });
        }

        private static string fId(int piId)
        {
            return "0x" + piId.ToString("X3", CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------- Names

        /// <summary>UWText.ObjectName in its four forms, for every block 4 entry.</summary>
        private static void fNames(DataImport pOData, List<Line> pOLines)
        {
            for (int liId = 0; liId < ObjectCount; liId++)
            {
                fAdd(pOLines, "name", fId(liId) + " bare", UWText.ObjectName(pOData, liId));
                fAdd(pOLines, "name", fId(liId) + " plural", UWText.ObjectName(pOData, liId, true));
                fAdd(pOLines, "name", fId(liId) + " article", UWText.ObjectName(pOData, liId, false, true));
                fAdd(pOLines, "name", fId(liId) + " article plural", UWText.ObjectName(pOData, liId, true, true));
            }
        }

        // ------------------------------------------------- The item look

        /// <summary>Every object at every quality, one piece, as it lies (detail 1).</summary>
        private static void fQualities(DataImport pOData, List<Line> pOLines)
        {
            for (int liId = 0; liId < ObjectCount; liId++)
            {
                for (int liQuality = 0; liQuality < 64; liQuality++)
                {
                    UWObject lOItem = new UWObject((ushort)liId) { Quality = (ushort)liQuality };

                    fAdd(pOLines, "quality", fId(liId) + " q" + liQuality,
                        UWItemDescriptions.DescribeItem(lOItem, pOData, 0, null, UWLoreCheck.ResultUnknown));
                }
            }
        }

        private static readonly int[] msCounts = { 0, 1, 2, 5, 13, 511 };

        private static readonly int[] msSomeQualities = { 0, 12, 25, 40, 63 };

        /// <summary>Every object as a pile of 0 to 511 at a few qualities.</summary>
        private static void fCounts(DataImport pOData, List<Line> pOLines)
        {
            for (int liId = 0; liId < ObjectCount; liId++)
            {
                foreach (int liCount in msCounts)
                {
                    foreach (int liQuality in msSomeQualities)
                    {
                        UWObject lOItem = new UWObject((ushort)liId) { HasQuantity = true, Quantity = (ushort)liCount, Quality = (ushort)liQuality };

                        fAdd(pOLines, "count", fId(liId) + " n" + liCount + " q" + liQuality,
                            UWItemDescriptions.DescribeItem(lOItem, pOData, 0, null, UWLoreCheck.ResultUnknown));
                    }
                }
            }
        }

        /// <summary>Every object with every owner field, one piece and a pile.</summary>
        private static void fOwners(DataImport pOData, List<Line> pOLines)
        {
            for (int liId = 0; liId < ObjectCount; liId++)
            {
                for (int liOwner = 0; liOwner < 64; liOwner++)
                {
                    UWObject lOItem = new UWObject((ushort)liId) { Quality = 40, Owner = (ushort)liOwner };
                    UWObject lOPile = new UWObject((ushort)liId) { Quality = 40, Owner = (ushort)liOwner, HasQuantity = true, Quantity = 3 };

                    fAdd(pOLines, "owner", fId(liId) + " o" + liOwner,
                        UWItemDescriptions.DescribeItem(lOItem, pOData, 0, null, UWLoreCheck.ResultUnknown));
                    fAdd(pOLines, "owner", fId(liId) + " o" + liOwner + " n3",
                        UWItemDescriptions.DescribeItem(lOPile, pOData, 0, null, UWLoreCheck.ResultUnknown));
                }
            }
        }

        /// <summary>Every object at the detail levels 0 to 3 (the talismans name themselves at 3).</summary>
        private static void fDetails(DataImport pOData, List<Line> pOLines)
        {
            for (int liId = 0; liId < ObjectCount; liId++)
            {
                for (int liDetail = 0; liDetail <= UWLoreCheck.ResultNamed; liDetail++)
                {
                    UWObject lOItem = new UWObject((ushort)liId) { Quality = 40 };

                    fAdd(pOLines, "detail", fId(liId) + " d" + liDetail,
                        UWItemDescriptions.DescribeItem(lOItem, pOData, 0, null, liDetail));
                }
            }
        }

        /// <summary>The objects an enchantment is shown on: weapons, armour, rings, wands, a few more.</summary>
        private static IEnumerable<int> fEnchantable()
        {
            for (int liId = 0x00; liId <= 0x0F; liId++)
                yield return liId;

            for (int liId = 0x20; liId <= 0x3F; liId++)
                yield return liId;

            foreach (int liId in new[] { 0x98, 0x99, 0x9A, 0x9B, 0xAA, 0x12, 0x17, 0x80, 0xB4, 0x138 })
                yield return liId;
        }

        /// <summary>An item that carries its own enchantment (the quantity field, every value) in
        /// both forms (Flags bit 2), at the detail levels 1 to 3, one piece and a pile.</summary>
        private static void fEnchantments(DataImport pOData, List<Line> pOLines)
        {
            foreach (int liId in fEnchantable())
            {
                for (int liValue = 0; liValue < 0x200; liValue++)
                {
                    foreach (int liFlags in new[] { 0x8, 0xC })
                    {
                        for (int liDetail = UWLoreCheck.ResultUnknown; liDetail <= UWLoreCheck.ResultNamed; liDetail++)
                        {
                            UWObject lOItem = new UWObject((ushort)liId)
                            {
                                HasQuantity = true, Quantity = (ushort)liValue, Flags = (ushort)liFlags, Quality = 30
                            };

                            fAdd(pOLines, "enchant", fId(liId) + " v" + liValue + " f" + liFlags + " d" + liDetail,
                                UWItemDescriptions.DescribeItem(lOItem, pOData, 0, null, liDetail));
                        }
                    }
                }
            }
        }

        /// <summary>A chained spell object (the wands): its value, both forms, charges 0, 1 and 7.</summary>
        private static void fCharges(DataImport pOData, List<Line> pOLines)
        {
            foreach (int liId in new[] { 0x98, 0x99, 0x04, 0x37 })
            {
                for (int liValue = 0; liValue < 0x200; liValue += 3)
                {
                    foreach (int liFlags in new[] { 0x0, 0x4 })
                    {
                        foreach (int liCharges in new[] { 0, 1, 7 })
                        {
                            List<UWObject> lOList = fSpellChain(liFlags, liValue, liCharges);
                            UWObject lOItem = new UWObject((ushort)liId) { Quantity = 1, Quality = 40 };

                            fAdd(pOLines, "charges", fId(liId) + " v" + liValue + " f" + liFlags + " c" + liCharges,
                                UWItemDescriptions.DescribeItem(lOItem, pOData, 0, lOList, UWLoreCheck.ResultNamed));
                        }
                    }
                }
            }
        }

        private static List<UWObject> fSpellChain(int piFlags, int piValue, int piCharges)
        {
            UWObject lOSpell = new UWObject((ushort)UWEnchantment.SpellObjectId)
            {
                Flags = (ushort)piFlags,
                Quantity = (ushort)piValue,
                Quality = (ushort)piCharges
            };

            return new List<UWObject> { new UWObject(0), lOSpell };
        }

        /// <summary>identify_inv's name: every object, counts, article or not, detail 0 to 3; and the
        /// enchanted objects magical, cursed and named.</summary>
        private static void fIdentify(DataImport pOData, List<Line> pOLines)
        {
            for (int liId = 0; liId < ObjectCount; liId++)
            {
                foreach (int liCount in new[] { 0, 1, 2, 7 })
                {
                    foreach (bool lbArticle in new[] { false, true })
                    {
                        UWObject lOItem = new UWObject((ushort)liId) { HasQuantity = liCount > 0, Quantity = (ushort)liCount, Quality = 40 };

                        fAdd(pOLines, "identify", fId(liId) + " n" + liCount + (lbArticle ? " a" : " -"),
                            UWItemDescriptions.IdentifyName(lOItem, pOData, null, lbArticle, 0));
                    }
                }
            }

            foreach (int liId in fEnchantable())
            {
                foreach (int liValue in new[] { 0x05, 0x25, 0x90, 0x91, 0x123, 0x1F0 })
                {
                    foreach (int liCount in new[] { 1, 3 })
                    {
                        foreach (bool lbArticle in new[] { false, true })
                        {
                            for (int liDetail = 0; liDetail <= UWLoreCheck.ResultNamed; liDetail++)
                            {
                                UWObject lOItem = new UWObject((ushort)liId) { Quantity = (ushort)liCount, Quality = 40 };

                                fAdd(pOLines, "identify", fId(liId) + " v" + liValue + " n" + liCount + (lbArticle ? " a" : " -") + " d" + liDetail,
                                    UWItemDescriptions.IdentifyName(lOItem, pOData, fSpellChain(0x4, liValue, 3), lbArticle, liDetail));
                            }
                        }
                    }
                }
            }
        }

        // ------------------------------------------------- Creatures, scenery, walls

        /// <summary>CritterLook: every creature kind (and none) with every whoami and attitude.</summary>
        private static void fCreatures(DataImport pOData, List<Line> pOLines)
        {
            for (int liId = CreatureFirst - 1; liId <= CreatureLast; liId++)
            {
                string lsRaw = liId < CreatureFirst ? string.Empty : UWText.RawObjectName(pOData, liId);

                for (int liWho = 0; liWho < 0x100; liWho++)
                {
                    for (int liAttitude = 0; liAttitude <= UWNpc.MaxAttitude; liAttitude++)
                    {
                        UWNpc lONpc = new UWNpc((ushort)Math.Max(liId, CreatureFirst)) { NPCwhoami = (byte)liWho, NPCAttitude = (byte)liAttitude };

                        fAdd(pOLines, "creature", fId(liId) + " w" + liWho + " a" + liAttitude,
                            UWItemDescriptions.DescribeCreature(lONpc, lsRaw, pOData));
                    }
                }
            }
        }

        private static readonly int[] msSomeOwners = { 0, 0x20, 1, 6, 0x1B, 0x1C, 0x3F };

        /// <summary>The scenery and door look: every object name with every condition word the game
        /// has (none included) and a few owners.</summary>
        private static void fScenery(DataImport pOData, List<Line> pOLines)
        {
            List<string> lsWords = fConditionWords(pOData);

            for (int liId = 0; liId < ObjectCount; liId++)
            {
                string lsRaw = UWText.RawObjectName(pOData, liId, null);

                if (lsRaw == null)
                    continue;

                for (int liWord = -1; liWord < lsWords.Count; liWord++)
                {
                    foreach (int liOwner in msSomeOwners)
                    {
                        UWObject lOItem = new UWObject((ushort)liId) { Owner = (ushort)liOwner };

                        fAdd(pOLines, "scenery", fId(liId) + " c" + liWord + " o" + liOwner,
                            UWItemDescriptions.DescribeScenery(lsRaw, liWord < 0 ? null : lsWords[liWord], lOItem, pOData));
                    }
                }

                fAdd(pOLines, "scenery", fId(liId) + " no item", UWItemDescriptions.DescribeScenery(lsRaw, null, null, pOData));
            }
        }

        /// <summary>The condition words the game shows, each once, in the order first met: every
        /// object's quality from 0 to 63.</summary>
        private static List<string> fConditionWords(DataImport pOData)
        {
            List<string> lsWords = new List<string>();
            HashSet<string> lOSeen = new HashSet<string>(StringComparer.Ordinal);

            for (int liId = 0; liId < ObjectCount; liId++)
            {
                for (int liQuality = 0; liQuality < 64; liQuality++)
                {
                    string lsWord = UWItemDescriptions.GetItemConditionWord(new UWObject((ushort)liId) { Quality = (ushort)liQuality }, pOData);

                    if (!string.IsNullOrEmpty(lsWord) && lOSeen.Add(lsWord))
                        lsWords.Add(lsWord);
                }
            }

            return lsWords;
        }

        /// <summary>The walls and floors: every line of block 10.</summary>
        private static void fChunks(DataImport pOData, List<Line> pOLines)
        {
            if (pOData.Strings == null || !pOData.Strings.Blocks.TryGetValue(10, out UWStringBlock lOBlock) || lOBlock == null)
                return;

            for (int liIndex = 0; liIndex < lOBlock.Strings.Count; liIndex++)
            {
                string lsText = UWText.Block(pOData, 10, liIndex, null);

                if (lsText != null)
                    fAdd(pOLines, "chunk", "10:" + liIndex, UWItemDescriptions.DescribeChunk(lsText, pOData));
            }
        }

        /// <summary>SpecialLook: the keys and the bones with every owner field, one and more.</summary>
        private static void fSpecial(DataImport pOData, List<Line> pOLines)
        {
            for (int liId = 0xC0; liId <= 0x10F; liId++)
            {
                for (int liOwner = 0; liOwner < 64; liOwner++)
                {
                    foreach (int liCount in new[] { 1, 2 })
                    {
                        UWObject lOItem = new UWObject((ushort)liId) { Owner = (ushort)liOwner, Quantity = (ushort)liCount };

                        fAdd(pOLines, "special", fId(liId) + " o" + liOwner + " n" + liCount,
                            UWItemDescriptions.SpecialLookLine(lOItem, pOData));
                    }
                }
            }
        }

        // ------------------------------------------------- The levels as they lie

        /// <summary>Every object of the nine levels: the item look at detail 1 and 3, identify_inv's
        /// name, the scenery look with its owner, SpecialLook; a creature's look.</summary>
        private static void fWorld(DataImport pOData, List<Line> pOLines)
        {
            if (pOData.Levels == null)
                return;

            for (int liLevel = 0; liLevel < pOData.Levels.Count; liLevel++)
            {
                UWLevel lOLevel = pOData.Levels[liLevel];

                if (lOLevel == null || lOLevel.Masterlist == null)
                    continue;

                for (int liAt = 0; liAt < lOLevel.Masterlist.Count; liAt++)
                {
                    UWObject lOObject = lOLevel.Masterlist[liAt];

                    if (lOObject == null || (lOObject.ID == 0 && liAt == 0))
                        continue;

                    string lsKey = (liLevel + 1) + ":" + liAt + " " + fId(lOObject.ID);

                    // The mobile slots hold items as well; a creature is one of the creature ids.
                    if (lOObject is UWNpc lONpc && lOObject.ID >= CreatureFirst && lOObject.ID <= CreatureLast)
                    {
                        fAdd(pOLines, "world", lsKey + " creature",
                            UWItemDescriptions.DescribeCreature(lONpc, UWText.RawObjectName(pOData, lOObject.ID), pOData));

                        continue;
                    }

                    fAdd(pOLines, "world", lsKey + " d1",
                        UWItemDescriptions.DescribeItem(lOObject, pOData, 0, lOLevel.Masterlist, UWLoreCheck.ResultUnknown));
                    fAdd(pOLines, "world", lsKey + " d3",
                        UWItemDescriptions.DescribeItem(lOObject, pOData, 0, lOLevel.Masterlist, UWLoreCheck.ResultNamed));
                    fAdd(pOLines, "world", lsKey + " identify",
                        UWItemDescriptions.IdentifyName(lOObject, pOData, lOLevel.Masterlist, true, UWLoreCheck.ResultNamed));
                    fAdd(pOLines, "world", lsKey + " scenery",
                        UWItemDescriptions.DescribeScenery(UWText.RawObjectName(pOData, lOObject.ID), null, lOObject, pOData));
                    fAdd(pOLines, "world", lsKey + " special", UWItemDescriptions.SpecialLookLine(lOObject, pOData));
                }
            }
        }

        // ------------------------------------------------- The sentences around a name (step 3A)

        /// <summary>Every object in the item messages: cannot be used on that, too full, locked,
        /// empty, use on what, the read header, the five tastes, worn equipment damaged and
        /// destroyed, no ammunition; a creature's theft line at every attitude.</summary>
        private static void fMessages(DataImport pOData, List<Line> pOLines)
        {
            for (int liId = 0; liId < ObjectCount; liId++)
            {
                string lsId = fId(liId);

                fAdd(pOLines, "message", lsId + " cannot use", UWItemMessages.CannotBeUsedOnThat(pOData, liId));
                fAdd(pOLines, "message", lsId + " too full", UWItemMessages.IsTooFull(pOData, liId));
                fAdd(pOLines, "message", lsId + " locked", UWItemMessages.IsLocked(pOData, liId));
                fAdd(pOLines, "message", lsId + " empty", UWItemMessages.IsEmpty(pOData, liId));
                fAdd(pOLines, "message", lsId + " use on", UWItemMessages.UseOnWhat(pOData, liId));
                fAdd(pOLines, "message", lsId + " read", UWItemMessages.ReadHeader(pOData, liId));

                for (int liStep = 0; liStep <= UWItemUse.LastTasteStep; liStep++)
                    fAdd(pOLines, "message", lsId + " taste " + liStep, UWItemMessages.Taste(pOData, liId, liStep));

                fAdd(pOLines, "message", lsId + " damaged", UWEquipmentWear.Message(pOData, liId, false));
                fAdd(pOLines, "message", lsId + " destroyed", UWEquipmentWear.Message(pOData, liId, true));
                fAdd(pOLines, "message", lsId + " no ammunition", UWItemMessages.NoAmmunition(pOData, liId));
            }

            for (int liId = CreatureFirst; liId <= CreatureLast; liId++)
            {
                for (int liAttitude = 0; liAttitude <= UWNpc.MaxAttitude; liAttitude++)
                    fAdd(pOLines, "message", fId(liId) + " angered a" + liAttitude, UWItemMessages.Angered(pOData, liId, liAttitude));
            }
        }

        /// <summary>Every trap object disarmed on every object, and set off.</summary>
        private static void fTrapMessages(DataImport pOData, List<Line> pOLines)
        {
            for (int liTrap = 0x180; liTrap <= 0x1BF; liTrap++)
            {
                fAdd(pOLines, "trap", fId(liTrap) + " set off", UWTrapDisarming.SetOffMessage(pOData, liTrap));

                for (int liTarget = 0; liTarget < ObjectCount; liTarget++)
                    fAdd(pOLines, "trap", fId(liTrap) + " on " + fId(liTarget), UWTrapDisarming.DisarmedMessage(pOData, liTrap, liTarget));
            }

            fAdd(pOLines, "trap", "failed", UWTrapDisarming.FailedMessage);
        }

        /// <summary>The anvil: every object's estimate with each difficulty word, every outcome.</summary>
        private static void fRepair(DataImport pOData, List<Line> pOLines)
        {
            for (int liId = 0; liId < ObjectCount; liId++)
            {
                for (int liWord = 0; liWord < UWRepairRules.DifficultyWordCount; liWord++)
                    fAdd(pOLines, "repair", fId(liId) + " estimate " + liWord, UWRepairRules.EstimateQuestion(pOData, liId, liWord));

                foreach (UWRepairRules.OutcomeEnum leOutcome in Enum.GetValues(typeof(UWRepairRules.OutcomeEnum)))
                    fAdd(pOLines, "repair", fId(liId) + " " + leOutcome, UWRepairRules.ResultMessage(pOData, leOutcome, liId));
            }
        }

        /// <summary>The level-up line for the levels 0 to 20.</summary>
        private static void fLevelUp(DataImport pOData, List<Line> pOLines)
        {
            for (int liLevel = 0; liLevel <= 20; liLevel++)
                fAdd(pOLines, "levelup", "level " + liLevel, UWPlayerVitals.LevelUpMessage(pOData, liLevel));
        }

        /// <summary>The shrine: the advance of every skill alone and in lists of two to four,
        /// INSAHN's line in every form, the prompt.</summary>
        private static void fShrine(DataImport pOData, List<Line> pOLines)
        {
            for (int liSkill = 0; liSkill < UWPlayerVitals.SkillNumberCount; liSkill++)
            {
                fAdd(pOLines, "shrine", "greatly " + liSkill, UWShrineRules.GetAdvancedGreatlyLine(pOData, liSkill));

                for (int liCount = 1; liCount <= 4; liCount++)
                {
                    List<string> lsNames = new List<string>();

                    for (int liAt = 0; liAt < liCount; liAt++)
                        lsNames.Add(UWCharacterText.SkillName(pOData.Strings, (liSkill + liAt) % UWPlayerVitals.SkillNumberCount));

                    fAdd(pOLines, "shrine", "advanced " + liSkill + " x" + liCount, UWShrineRules.GetAdvancedLine(pOData, lsNames));
                }
            }

            for (int liCardinal = -1; liCardinal < 8; liCardinal++)
            {
                for (int liDifference = -8; liDifference <= 8; liDifference++)
                {
                    foreach (bool lbOther in new[] { false, true })
                    {
                        foreach (bool lbNear in new[] { false, true })
                        {
                            fAdd(pOLines, "shrine", "cup " + liCardinal + " " + liDifference + (lbOther ? " other" : " same") + (lbNear ? " near" : string.Empty),
                                UWShrineRules.GetCupLine(pOData, liCardinal, lbOther, liDifference, lbNear));
                        }
                    }
                }
            }

            fAdd(pOLines, "shrine", "prompt", UWShrineRules.Prompt);
        }

        /// <summary>The modern bags' tooltip name: every object one, two and five, cursed or not; the
        /// talismans' names.</summary>
        private static void fLabels(DataImport pOData, List<Line> pOLines)
        {
            for (int liId = 0; liId < ObjectCount; liId++)
            {
                foreach (int liCount in new[] { 1, 2, 5 })
                {
                    foreach (bool lbCursed in new[] { false, true })
                    {
                        fAdd(pOLines, "label", fId(liId) + " n" + liCount + (lbCursed ? " cursed" : string.Empty),
                            UWText.ObjectLabel(pOData, liId, liCount, lbCursed));
                    }
                }

                string lsTalisman = UWTalismans.GetName(liId, pOData.Strings);

                if (!string.IsNullOrEmpty(lsTalisman))
                    fAdd(pOLines, "label", fId(liId) + " talisman", UWGrammar.Of(pOData).LiteralLabel(lsTalisman));
            }
        }

        /// <summary>The fixed lines of UW.EXE (and a few of the port's) the engine-free code shows.</summary>
        private static void fFixed(DataImport pOData, List<Line> pOLines)
        {
            fAdd(pOLines, "fixed", "exploding book", UWExplodingBook.Message);
            fAdd(pOLines, "fixed", "not a spell", UWSpellCasting.NotASpellText);
            fAdd(pOLines, "fixed", "rune missing", UWSpellCasting.RuneMissingText);
            fAdd(pOLines, "fixed", "yes", UWYesNoQuestion.YesWord);
            fAdd(pOLines, "fixed", "no", UWYesNoQuestion.NoWord);
            fAdd(pOLines, "fixed", "unnamed item", UWItemMessages.UnnamedItem);
        }
    }
}
