using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 2.10 (2026-10-14): the dispatch tables that replaced the if-chains - the trap
    /// kinds and a_do actions (UWTrapRules.Traps, DoActions), the variable-trap operations, the
    /// conversation functions (UWConversationSession.Functions), the item-use rows
    /// (UWItemUse.UseTable) and the food rows (UWFoodTable) - walked over every id, quality and
    /// name the game can hand them. Each one is either handled or listed here as deliberately
    /// unhandled with the reason; anything else fails. Where the game data says which ids occur
    /// (the levels' traps, CNV.ARK's imports), the data is walked too.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckDispatchCoverage(DataImport pOData)
        {
            Check.Group("Dispatch coverage (trap, a_do, conversation, item-use and food tables)");

            fCheckTrapKindCoverage(pOData);
            fCheckDoActionCoverage(pOData);
            fCheckVariableOperationCoverage(pOData);
            fCheckConversationFunctionCoverage(pOData);
            fCheckItemUseCoverage(pOData);
            fCheckFoodCoverage(pOData);
        }

        // ------------------------------------------------------------------ traps

        /// <summary>The trap ids UseTrap (TRIGGER.C) has no case for: the original does nothing
        /// there and follows the link, ours logs NOT BUILT and follows the link too. No level holds
        /// one of them (checked below).</summary>
        private static readonly HashSet<int> msUnhandledTrapKinds = new HashSet<int>
        {
            0x18F, 0x191, 0x192, 0x193, 0x194, 0x195, 0x196, 0x197,
            0x198, 0x199, 0x19A, 0x19B, 0x19C, 0x19D, 0x19E, 0x19F,
        };

        private static void fCheckTrapKindCoverage(DataImport pOData)
        {
            UWTrapTable lOTraps = UWTrapRules.Traps;
            List<string> lOGaps = new List<string>();
            int liHandled = 0;

            for (int liId = UWTrapTable.FirstId; liId < UWTrapTable.FirstId + UWTrapTable.KindCount; liId++)
            {
                bool lbHandled = lOTraps.IsHandled(liId);
                bool lbDeliberate = msUnhandledTrapKinds.Contains(liId);

                if (lbHandled)
                    liHandled++;

                if (lbHandled == lbDeliberate)
                    lOGaps.Add(string.Format("0x{0:X3} {1} ({2})", liId, UWText.ObjectName(pOData, liId),
                        lbHandled ? "handled but listed as unhandled" : "neither handled nor listed"));
            }

            if (lOGaps.Count == 0)
                fPass("dispatch: every trap id 0x180-0x19F handled or listed",
                    liHandled + " handled, " + msUnhandledTrapKinds.Count + " without a case in UseTrap");
            else
                fFail("dispatch: every trap id 0x180-0x19F handled or listed", string.Join("; ", lOGaps));

            fExpectBool("dispatch: a trigger id is not a trap kind", lOTraps.IsHandled(UWObjectIds.FirstTrigger), false);
            fExpectBool("dispatch: a non-trap id is not a trap kind (no id & 0x1F alias)",
                lOTraps.IsHandled(UWObjectMechanics.DoTrapId - 0x100), false);

            // The shipped levels: every trap there is of a handled kind.
            List<string> lOFound = new List<string>();
            int liTraps = 0;

            foreach (UWObject lOTrap in fLevelTraps(pOData))
            {
                liTraps++;

                if (!lOTraps.IsHandled(lOTrap.ID) && !lOFound.Contains("0x" + lOTrap.ID.ToString("X3")))
                    lOFound.Add("0x" + lOTrap.ID.ToString("X3"));
            }

            if (lOFound.Count == 0)
                fPass("dispatch: every trap in the nine levels is of a handled kind", liTraps + " traps");
            else
                fFail("dispatch: every trap in the nine levels is of a handled kind", "unhandled: " + string.Join(", ", lOFound));
        }

        /// <summary>The qualities do_trap_hack (TRIGGER.C) has a case for: 2 crystal ball, 3 and 4
        /// eight-position switch, 5 a crime, 0x18 bullfrog, 0x28 emerald, 0x29 exploding book, 0x2A
        /// talking door, 0x32 the conversation trap, 0x39 Arial, 0x3C-0x3E quake, 0x3F the end.
        /// Every other quality is deliberately unhandled: the original returns 2 and does nothing,
        /// ours logs NOT BUILT; the chain goes on in both.</summary>
        private static readonly int[] miOriginalDoActions =
        {
            0x02, 0x03, 0x04, 0x05, 0x18, 0x28, 0x29, 0x2A, 0x32, 0x39, 0x3C, 0x3D, 0x3E, 0x3F,
        };

        private static void fCheckDoActionCoverage(DataImport pOData)
        {
            UWDoActionTable lOActions = UWTrapRules.DoActions;
            HashSet<int> lOOriginal = new HashSet<int>(miOriginalDoActions);
            List<string> lOGaps = new List<string>();

            for (int liQuality = 0; liQuality < UWDoActionTable.ActionCount; liQuality++)
            {
                if (lOActions.IsHandled(liQuality) != lOOriginal.Contains(liQuality))
                    lOGaps.Add(string.Format("0x{0:X2} {1}", liQuality,
                        lOActions.IsHandled(liQuality) ? "handled, no case in do_trap_hack" : "a case in do_trap_hack, not handled"));
            }

            if (lOGaps.Count == 0)
                fPass("dispatch: every a_do quality 0-63 as do_trap_hack has it",
                    lOOriginal.Count + " handled, " + (UWDoActionTable.ActionCount - lOOriginal.Count) + " without a case");
            else
                fFail("dispatch: every a_do quality 0-63 as do_trap_hack has it", string.Join("; ", lOGaps));

            List<string> lOFound = new List<string>();
            int liDoTraps = 0;

            foreach (UWObject lOTrap in fLevelTraps(pOData))
            {
                if (lOTrap.ID != UWObjectMechanics.DoTrapId)
                    continue;

                liDoTraps++;

                if (!lOActions.IsHandled(lOTrap.Quality) && !lOFound.Contains(lOTrap.Quality.ToString()))
                    lOFound.Add(lOTrap.Quality.ToString());
            }

            if (lOFound.Count == 0)
                fPass("dispatch: every a_do trap in the nine levels has a handled quality", liDoTraps + " a_do traps");
            else
                fFail("dispatch: every a_do trap in the nine levels has a handled quality", "unhandled qualities: " + string.Join(", ", lOFound));
        }

        /// <summary>The heading is three bits; UseTrap has cases 0-6. Heading 7 is deliberately
        /// unknown (ours logs "unknown operation" and leaves the variable); no level holds one.</summary>
        private static void fCheckVariableOperationCoverage(DataImport pOData)
        {
            fExpectInt("dispatch: seven variable-trap operations", UWVariableTrapOperations.Count, 7);

            int liResult;

            fExpectBool("dispatch: variable-trap heading 7 is unknown",
                UWVariableTrapOperations.TryApply(7, 3, 1, out liResult) || UWVariableTrapOperations.NameOf(7) != "?", false);

            int liSetTraps = 0;
            int liUnknown = 0;

            foreach (UWObject lOTrap in fLevelTraps(pOData))
            {
                if (lOTrap.ID != UWObjectMechanics.SetVariableTrapId || lOTrap.ZPos == 0)
                    continue;

                liSetTraps++;

                if (lOTrap.Heading >= UWVariableTrapOperations.Count)
                    liUnknown++;
            }

            fExpectInt("dispatch: set-variable traps in the nine levels with an unknown operation (of " + liSetTraps + ")", liUnknown, 0);
        }

        /// <summary>Every trap record of the nine levels in a slot that is in use. A slot on the
        /// free lists keeps whatever its last record was: LEV.ARK has records with ids 0x191-0x19D
        /// in free slots (the same slots on most levels), which nothing links to and the game never
        /// runs.</summary>
        private static IEnumerable<UWObject> fLevelTraps(DataImport pOData)
        {
            if (pOData.Levels == null)
                yield break;

            foreach (UWLevel lOLevel in pOData.Levels)
            {
                if (lOLevel == null || lOLevel.Masterlist == null)
                    continue;

                HashSet<int> lOFree = new HashSet<int>(lOLevel.FreeMobileSlots ?? new List<int>());

                lOFree.UnionWith(lOLevel.FreeStaticSlots ?? new List<int>());

                for (int liSlot = 0; liSlot < lOLevel.Masterlist.Count; liSlot++)
                {
                    UWObject lOObject = lOLevel.Masterlist[liSlot];

                    if (lOObject != null && !lOFree.Contains(liSlot)
                        && lOObject.ID >= UWTrapTable.FirstId && lOObject.ID < UWObjectIds.FirstTrigger)
                        yield return lOObject;
                }
            }
        }

        // ------------------------------------------------------------------ conversations

        /// <summary>The eight built-ins UWConversationVM.fCallImported serves by id itself.</summary>
        private static readonly Dictionary<string, int> mOVmBuiltIns = new Dictionary<string, int>
        {
            { "babl_menu", 0x00 }, { "babl_fmenu", 0x01 }, { "print", 0x02 }, { "babl_ask", 0x03 },
            { "random", 0x05 }, { "say", 0x0D }, { "get_quest", 0x0F }, { "set_quest", 0x10 },
        };

        /// <summary>Imports that no CALLI of CNV.ARK reaches, so ours has no function for them:
        /// the original binds each (bab_fun in load_script, BABL.C, and Converse, CONVERSE.C), the
        /// compiler imported them, nothing calls them. A call would land in MissingFunctions and
        /// answer 0. Listed so that a conversation that does call one shows up as a gap.</summary>
        private static readonly Dictionary<string, string> mOUncalledImports = new Dictionary<string, string>
        {
            { "add_to_npc_inv", "inventory, never called" },
            { "append", "string built-in, never called" },
            { "copy", "string built-in, never called" },
            { "end_barter", "trade, never called" },
            { "find", "array search built-in, never called" },
            { "pause", "screen pause, never called" },
            { "plural", "string built-in, never called" },
            { "respond", "the import RESPOND_OP prints through; never called as a function" },
            { "val", "string built-in, never called" },
            { "x_obj_pos", "object position, never called" },
        };

        private static void fCheckConversationFunctionCoverage(DataImport pOData)
        {
            UWConversations lOConversations = pOData.Conversations;

            if (lOConversations == null || !lOConversations.IsLoaded)
            {
                fFail("dispatch: conversations", "CNV.ARK is not loaded");

                return;
            }

            UWConversationFunctionTable lOTable = UWConversationSession.Functions;
            SortedDictionary<string, HashSet<int>> lOIdsByName = new SortedDictionary<string, HashSet<int>>(System.StringComparer.Ordinal);
            HashSet<string> lOCalled = new HashSet<string>(System.StringComparer.Ordinal);
            List<string> lOUnimportedCalls = new List<string>();
            int liConversations = 0;
            int liCalls = 0;

            for (int liSlot = 0; liSlot < lOConversations.SlotCount; liSlot++)
            {
                if (!lOConversations.HasConversation(liSlot))
                    continue;

                UWConversations.Conversation lOConversation = lOConversations.GetConversation(liSlot);

                if (lOConversation == null)
                    continue;

                liConversations++;

                Dictionary<int, string> lONameById = new Dictionary<int, string>();

                foreach (UWConversations.Import lOImport in lOConversation.Imports)
                {
                    if (!lOImport.IsFunction)
                        continue;

                    if (!lOIdsByName.TryGetValue(lOImport.Name, out HashSet<int> lOIds))
                        lOIdsByName[lOImport.Name] = lOIds = new HashSet<int>();

                    lOIds.Add(lOImport.IdOrAddress);
                    lONameById[lOImport.IdOrAddress] = lOImport.Name;
                }

                // Every CALLI of the program, walked as the disassembler walks it.
                ushort[] liCode = lOConversation.Code;

                for (int liAt = 0; liCode != null && liAt < liCode.Length; liAt++)
                {
                    int liWord = liCode[liAt];

                    if (liWord > (int)UWConversationDisassembler.Opcode.OPNEG
                        || !UWConversationDisassembler.HasImmediateOperand((UWConversationDisassembler.Opcode)liWord)
                        || liAt + 1 >= liCode.Length)
                        continue;

                    int liOperand = liCode[++liAt];

                    if (liWord != (int)UWConversationDisassembler.Opcode.CALLI)
                        continue;

                    liCalls++;

                    if (lONameById.TryGetValue(liOperand, out string lsName))
                        lOCalled.Add(lsName);
                    else
                        lOUnimportedCalls.Add(liSlot + ":" + liOperand);
                }
            }

            fPass("dispatch: conversations walked", liConversations + " conversations, " + liCalls + " calls, "
                + lOIdsByName.Count + " imported function names");

            if (lOUnimportedCalls.Count == 0)
                fPass("dispatch: every CALLI names an imported function", "none without");
            else
                fFail("dispatch: every CALLI names an imported function", string.Join(", ", lOUnimportedCalls));

            // gronk_door is looked up by name since 2026-10-14; it was tested by id 0x25 before.
            bool lbGronkConsistent = lOIdsByName.TryGetValue("gronk_door", out HashSet<int> lOGronkIds)
                && lOGronkIds.Count == 1 && lOGronkIds.Contains(UWConversationSession.GronkDoorFunction);

            foreach (KeyValuePair<string, HashSet<int>> lOPair in lOIdsByName)
                if (lOPair.Key != "gronk_door" && lOPair.Value.Contains(UWConversationSession.GronkDoorFunction))
                    lbGronkConsistent = false;

            fExpectBool("dispatch: gronk_door is imported as 0x25 everywhere, and 0x25 is nothing else", lbGronkConsistent, true);

            // The VM's built-ins go by id: their names must carry those ids everywhere.
            List<string> lOBuiltInGaps = new List<string>();

            foreach (KeyValuePair<string, int> lOPair in mOVmBuiltIns)
            {
                if (lOIdsByName.TryGetValue(lOPair.Key, out HashSet<int> lOIds) && (lOIds.Count != 1 || !lOIds.Contains(lOPair.Value)))
                    lOBuiltInGaps.Add(lOPair.Key);

                foreach (KeyValuePair<string, HashSet<int>> lOOther in lOIdsByName)
                    if (lOOther.Key != lOPair.Key && lOOther.Value.Contains(lOPair.Value))
                        lOBuiltInGaps.Add(lOOther.Key + " under " + lOPair.Value);
            }

            if (lOBuiltInGaps.Count == 0)
                fPass("dispatch: the VM's eight built-ins keep their ids in every conversation", string.Join(" ", mOVmBuiltIns.Keys));
            else
                fFail("dispatch: the VM's eight built-ins keep their ids in every conversation", string.Join(", ", lOBuiltInGaps));

            // Every called name: a built-in, a table entry, or listed.
            List<string> lOGaps = new List<string>();
            List<string> lOUncalled = new List<string>();

            foreach (string lsName in lOIdsByName.Keys)
            {
                bool lbServed = mOVmBuiltIns.ContainsKey(lsName) || lOTable.Contains(lsName);

                if (!lOCalled.Contains(lsName))
                {
                    if (!lbServed)
                        lOUncalled.Add(lsName);

                    continue;
                }

                if (!lbServed)
                    lOGaps.Add(lsName);
            }

            if (lOGaps.Count == 0)
                fPass("dispatch: every called conversation function is served", lOCalled.Count + " names called");
            else
                fFail("dispatch: every called conversation function is served", "missing: " + string.Join(", ", lOGaps));

            List<string> lOUnlisted = new List<string>();

            foreach (string lsName in lOUncalled)
                if (!mOUncalledImports.ContainsKey(lsName))
                    lOUnlisted.Add(lsName);

            if (lOUnlisted.Count == 0)
                fPass("dispatch: imported but never called, listed", lOUncalled.Count == 0 ? "none" : string.Join(" ", lOUncalled));
            else
                fFail("dispatch: imported but never called, listed", "not listed: " + string.Join(", ", lOUnlisted));

            // Table entries no conversation imports (kept from the reference's list; harmless).
            List<string> lOUnused = new List<string>();

            foreach (string lsName in lOTable.Names)
                if (!lOIdsByName.ContainsKey(lsName))
                    lOUnused.Add(lsName);

            fPass("dispatch: table entries no conversation imports", lOUnused.Count == 0 ? "none" : string.Join(" ", lOUnused));
        }

        // ------------------------------------------------------------------ item use and food

        private const int CoverageMandolinId = 291;

        private const int CoverageFluteId = 292;

        /// <summary>A host that only answers the instrument question - the instruments are the
        /// Unity side's (UWInstrumentPlayer.IsInstrument, mandolin 291 and flute 292).</summary>
        private sealed class CoverageUseHost : IUWItemUseHost
        {
            public void ConsumeUsedItem() { }
            public void AddGeneralMessage(int piIndex) { }
            public void AddMessage(string psMessage) { }
            public void CursePlayer(int piDice) { }
            public int PlayerDamageTypeProof { get { return 0; } }
            public void FlashWindow(int piPaletteColour, float pfSeconds) { }
            public void PlayCutscene(int piCutscene) { }
            public bool CastSpellFromObject(int piSpellIndex) { return false; }
            public bool CastSpellByClass(int piMajorClass, int piMinorClass) { return false; }
            public bool IsWandCoolingDown { get { return false; } }
            public void StartWandCooldown() { }
            public void PlayMagicItemRefused() { }
            public bool TrySleep() { return false; }
            public void SleepPassedOut() { }
            public bool TryPlantSeed() { return false; }
            public bool IsInstrument(int piObjectId) { return piObjectId == CoverageMandolinId || piObjectId == CoverageFluteId; }
            public bool PlayInstrument(UWObject pOItem) { return false; }
            public bool TryFish() { return false; }
        }

        /// <summary>The row of the use table a plain item of this id lands on, or "none".</summary>
        private static string fUseRowOf(DataImport pOData, UWObject pOItem)
        {
            UWItemUseTable lOTable = UWItemUse.UseTable;
            int liRow = lOTable.FindRow(new UWItemUseTable.Use(pOItem, pOData, null, null, new CoverageUseHost()));

            return liRow < 0 ? "none" : lOTable.Rows[liRow].Name;
        }

        private static void fCheckItemUseCoverage(DataImport pOData)
        {
            // The rows that are chosen by id: each id must reach its own row.
            (string Name, int Id)[] lOById =
            {
                ("bedroll", UWItemUse.BedrollObjectId),
                ("instrument", CoverageMandolinId),
                ("instrument", CoverageFluteId),
                ("silver seed", UWItemUse.SilverSeedObjectId),
                ("leeches", UWItemUse.LeechObjectId),
                ("burning incense", UWItemUse.BurningIncenseObjectId),
                ("exploding book", UWExplodingBook.BookObjectId),
                ("fishing pole", UWItemUse.FishingPoleObjectId),
            };

            foreach ((string lsName, int liId) in lOById)
                Check.Str("dispatch: use of 0x" + liId.ToString("X3") + " reaches its row", fUseRowOf(pOData, new UWObject((ushort)liId)), lsName);

            // The rows chosen by a property, each with an example.
            UWObject lOScroll = new UWObject(0x130) { IsEnchanted = true };
            UWObject lOWand = new UWObject(0x98) { HasQuantity = true, IsEnchanted = true, Quantity = 0x211 };

            lOWand.Flags |= UWObjectMechanics.SpellFormatFlag;

            Check.Str("dispatch: use of a red potion", fUseRowOf(pOData, new UWObject(0xBB)), "potion or spell scroll");
            Check.Str("dispatch: use of an enchanted scroll", fUseRowOf(pOData, lOScroll), "potion or spell scroll");
            Check.Str("dispatch: use of a charged wand", fUseRowOf(pOData, lOWand), "charged item");
            Check.Str("dispatch: use of an unlit lantern", fUseRowOf(pOData, new UWObject(0x90)), "light source");

            // Every row is reached by something above or below.
            HashSet<string> lOReached = new HashSet<string> { "potion or spell scroll", "charged item", "light source" };
            List<string> lOWrong = new List<string>();
            int liNone = 0;

            foreach ((string lsName, int liId) in lOById)
                lOReached.Add(lsName);

            // Every plain id: food goes to the food row, a light source to the light row, the rest
            // to its id row or to none (deliberately: the use mode or a target takes it then).
            for (int liId = 0; liId < 0x200; liId++)
            {
                UWObject lOItem = new UWObject((ushort)liId);
                string lsRow = fUseRowOf(pOData, lOItem);
                string lsExpected;

                if (UWItemUse.IsPotion(lOItem))
                    lsExpected = "potion or spell scroll";
                else if (lOItem.GetCategory() == UWObject.ObjectCategoryEnum.Comestibles || UWItemUse.IsEatenOnUse(liId))
                    lsExpected = "food";
                else
                {
                    lsExpected = "none";

                    foreach ((string lsName, int liById) in lOById)
                        if (liById == liId)
                            lsExpected = lsName;

                    if (lsExpected == "none" && lOItem.GetCategory() == UWObject.ObjectCategoryEnum.LightSources)
                        lsExpected = "light source";
                }

                if (lsRow != lsExpected)
                    lOWrong.Add(string.Format("0x{0:X3} {1} (expected {2})", liId, lsRow, lsExpected));

                if (lsRow == "none")
                    liNone++;

                lOReached.Add(lsRow);
            }

            if (lOWrong.Count == 0)
                fPass("dispatch: every plain id 0-0x1FF lands on its row", (0x200 - liNone) + " on a row, " + liNone + " on none (use mode or target)");
            else
                fFail("dispatch: every plain id 0-0x1FF lands on its row", string.Join("; ", lOWrong));

            List<string> lOUnreached = new List<string>();

            foreach (UWItemUseTable.Row lORow in UWItemUse.UseTable.Rows)
                if (!lOReached.Contains(lORow.Name))
                    lOUnreached.Add(lORow.Name);

            if (lOUnreached.Count == 0)
                fPass("dispatch: every use row is reachable", UWItemUse.UseTable.Rows.Count + " rows");
            else
                fFail("dispatch: every use row is reachable", "unreached: " + string.Join(", ", lOUnreached));
        }

        /// <summary>Food rows outside the comestibles without their own nutrition: eaten only when
        /// dropped on the head (UWItemUse.TryEat); used from the pack the candle lights and the
        /// leeches bite.</summary>
        private static readonly HashSet<int> msHeadDropOnlyFood = new HashSet<int> { UWObjectIds.Candle, UWItemUse.LeechObjectId };

        private static void fCheckFoodCoverage(DataImport pOData)
        {
            // The sixteen comestibles: a potion (its own path), a row, or plain food with a table
            // value.
            List<string> lOGaps = new List<string>();
            int liPotions = 0;
            int liRows = 0;
            int liPlain = 0;

            for (int liId = 0xB0; liId <= 0xBF; liId++)
            {
                UWFoodTable.Row lORow;

                if (UWItemUse.IsPotion(new UWObject((ushort)liId)))
                    liPotions++;
                else if (UWFoodTable.TryGet(liId, out lORow))
                    liRows++;
                else if (pOData.ObjectClassProperties != null && pOData.ObjectClassProperties.GetFoodNutrition(liId) != 0xFF)
                    liPlain++;
                else
                    lOGaps.Add("0x" + liId.ToString("X2"));
            }

            if (lOGaps.Count == 0)
                fPass("dispatch: every comestible 0xB0-0xBF is a potion, a row or plain food",
                    liPotions + " potions, " + liRows + " rows, " + liPlain + " plain");
            else
                fFail("dispatch: every comestible 0xB0-0xBF is a potion, a row or plain food", string.Join(", ", lOGaps));

            fExpectBool("dispatch: no food row for a potion",
                UWFoodTable.TryGet(0xBB, out _) || UWFoodTable.TryGet(0xBC, out _), false);

            // Rows outside the comestibles: eaten when used (own nutrition) or by the head drop only.
            List<string> lOStray = new List<string>();
            int liOutside = 0;

            foreach (UWFoodTable.Row lORow in UWFoodTable.Rows)
            {
                if ((lORow.Id >> 4) == 0xB)
                    continue;

                liOutside++;

                if (lORow.HasOwnNutrition == msHeadDropOnlyFood.Contains(lORow.Id))
                    lOStray.Add("0x" + lORow.Id.ToString("X3"));
            }

            if (lOStray.Count == 0)
                fPass("dispatch: every food row outside the comestibles is eaten on use or by the head drop", liOutside + " rows");
            else
                fFail("dispatch: every food row outside the comestibles is eaten on use or by the head drop", string.Join(", ", lOStray));

            fExpectBool("dispatch: the four eaten on use are 0xCE, 0xCF, 0xD9 and the stew",
                UWItemUse.IsEatenOnUse(0xCE) && UWItemUse.IsEatenOnUse(0xCF) && UWItemUse.IsEatenOnUse(0xD9)
                && UWItemUse.IsEatenOnUse(UWObjectIds.RotwormStew) && !UWItemUse.IsEatenOnUse(UWObjectIds.Candle)
                && !UWItemUse.IsEatenOnUse(UWItemUse.LeechObjectId), true);
        }
    }
}
