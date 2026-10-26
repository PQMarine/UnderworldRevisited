using System;
using System.Collections.Generic;
using System.Linq;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Reference audit round 2 (2026-10-15), conversations and barter: the trade's rules against
    /// UnderworldExhumed BARTER.C, CONVERSE.C and BABLHACK.C, run on a hand-made partner through
    /// the conversation function table, the way the machine calls them.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>A trade with a partner whose chain holds the given ids (in that order), on
        /// level 1, without a player.</summary>
        private static UWConversationTrade fMakeTrade(CheckContext pOContext, IEnumerable<int> piIds,
            out UWNpc pONpc, out UWConversationSession pOSession)
        {
            pONpc = new UWNpc((ushort)UWObjectIds.FirstCreature) { NPCLootSpawned = true, NPCAttitude = UWNpc.AttitudeFriendly };
            pONpc.EnsureContentsLoaded(null);

            foreach (int liId in piIds)
                pONpc.Contents.Add(new UWObject((ushort)liId) { Quality = 40 });

            pOSession = new UWConversationSession(pOContext.Data, pONpc, 1, ConversationHostStub.Create(pOContext.Level(0)),
                UWGameSession.Current);

            return pOSession.Trade;
        }

        /// <summary>Calls a function of the table by name, as the machine does.</summary>
        private static int fCallTrade(UWConversationSession pOSession, string psName, int[] piArguments = null,
            int[] piAddresses = null, UWConversationVM pOVm = null)
        {
            return pOSession.CallUnknown(psName, 0, piArguments ?? Array.Empty<int>(),
                piAddresses ?? Array.Empty<int>(), pOVm);
        }

        /// <summary>Ids COMOBJ.DAT values above 0, of a minor class other than 0, from 0x80 on.</summary>
        private static List<int> fValuableIds(DataImport pOData, int piCount)
        {
            List<int> liIds = new List<int>();
            UWCommonObjectProperties.Entry lOEntry;

            for (int liId = 0x80; liId < 0x140 && liIds.Count < piCount; liId++)
                if (UWObjectIds.Minor(liId) != 0 && pOData.CommonObjectProperties.TryGet(liId, out lOEntry) && lOEntry.Value > 0)
                    liIds.Add(liId);

            return liIds;
        }

        /// <summary>The first id from piFrom on that COMOBJ.DAT values at 0, of a minor class other than 0.</summary>
        private static int fWorthlessId(DataImport pOData, int piFrom = 0x80)
        {
            UWCommonObjectProperties.Entry lOEntry;

            for (int liId = piFrom; liId < 0x180; liId++)
                if (UWObjectIds.Minor(liId) != 0 && pOData.CommonObjectProperties.TryGet(liId, out lOEntry) && lOEntry.Value == 0)
                    return liId;

            return -1;
        }

        /// <summary>The first id of minor class 0 with a value, from piFrom on.</summary>
        private static int fValuableMinorZeroId(DataImport pOData, int piFrom)
        {
            UWCommonObjectProperties.Entry lOEntry;

            for (int liId = piFrom; liId < 0x180; liId++)
                if (UWObjectIds.Minor(liId) == 0 && pOData.CommonObjectProperties.TryGet(liId, out lOEntry) && lOEntry.Value > 0)
                    return liId;

            return -1;
        }

        private static int[] fSlotIds(UWObject[] pOSlots)
        {
            return pOSlots.Select(pOItem => pOItem == null ? -1 : (int)pOItem.ID).ToArray();
        }

        /// <summary>A63 and A70: setup_to_barter's walk and the goods leaving the partner's chain.</summary>
        private static void fCheckBarterSetup(CheckContext pOContext)
        {
            Check.Group("Barter setup (setup_to_barter: minor class 0, 0x28 items, the roll, goods out of the chain)");

            DataImport lOData = pOContext.Data;
            List<int> liValuable = fValuableIds(lOData, 6);
            int liWorthless = fWorthlessId(lOData);
            int liBag = fValuableMinorZeroId(lOData, 0x80);
            int liOtherMinorZero = fValuableMinorZeroId(lOData, 0x00);

            if (pOContext.Level(0) == null || liValuable.Count < 6 || liWorthless < 0 || liBag < 0 || liOtherMinorZero < 0)
            {
                Check.Fail("barter setup", "no level or not enough kinds of objects");
                return;
            }

            // The first item of minor class 0 is held back, whatever its major class - a bag too.
            UWConversationTrade lOTrade = fMakeTrade(pOContext, new[] { liBag, liOtherMinorZero, liValuable[0] },
                out UWNpc lONpc, out UWConversationSession lOSession);

            fCallTrade(lOSession, "setup_to_barter");
            Check.Seq("setup: the bag (minor class 0) held back, the next minor-0 item laid out",
                fSlotIds(lOTrade.NpcItems), new[] { liOtherMinorZero, liValuable[0], -1, -1 });
            Check.Seq("setup: the laid-out goods leave the chain", lONpc.Contents.Select(pOItem => (int)pOItem.ID).ToArray(),
                new[] { liBag });

            fCallTrade(lOSession, "do_decline");
            Check.Seq("do_decline: the goods go back to the head of the chain, slot by slot",
                lONpc.Contents.Select(pOItem => (int)pOItem.ID).ToArray(), new[] { liValuable[0], liOtherMinorZero, liBag });

            // The roll only for an item the first two tests would lay out: the worthless one costs no
            // roll; the fifth valuable rolls 7 (taken) and gives the first back to the chain's head.
            lOTrade = fMakeTrade(pOContext, new[] { liValuable[0], liValuable[1], liValuable[2], liValuable[3], liWorthless, liValuable[4] },
                out lONpc, out lOSession);

            RecordingRandom lOScript = new RecordingRandom(7);

            UWRandom.Use(lOScript);

            try
            {
                fCallTrade(lOSession, "setup_to_barter");
            }
            finally
            {
                UWRandom.Use(null);
            }

            Check.Str("setup: one roll - none for the worthless item", string.Join(" ", lOScript.Ranges), "8");
            Check.Seq("setup: the fifth good replaced slot 0",
                fSlotIds(lOTrade.NpcItems), new[] { liValuable[4], liValuable[1], liValuable[2], liValuable[3] });
            Check.Seq("setup: the replaced good is back at the head of the chain",
                lONpc.Contents.Select(pOItem => (int)pOItem.ID).ToArray(), new[] { liValuable[0], liWorthless });

            // 0x28 items looked at: a good in place 40 is laid out, one in place 41 is not.
            List<int> liChain = Enumerable.Repeat(liWorthless, 39).ToList();

            liChain.Add(liValuable[5]);
            liChain.Add(liValuable[4]);

            lOTrade = fMakeTrade(pOContext, liChain, out lONpc, out lOSession);
            fCallTrade(lOSession, "setup_to_barter");
            Check.Seq("setup: the fortieth item is looked at, the forty-first is not",
                fSlotIds(lOTrade.NpcItems), new[] { liValuable[5], -1, -1, -1 });

            // An accepted demand: the unmarked goods go back, the marked one stays out of the chain.
            lOTrade = fMakeTrade(pOContext, new[] { liValuable[0], liValuable[1] }, out lONpc, out lOSession);
            fCallTrade(lOSession, "setup_to_barter");
            lOTrade.ToggleNpcSelected(1);
            lOTrade.Swap();
            Check.Seq("accepted deal: the unmarked good is back in the chain, the marked one is not",
                lONpc.Contents.Select(pOItem => (int)pOItem.ID).ToArray(), new[] { liValuable[0] });
            Check.Bool("accepted deal: the marked good lies on his side, bought", lOTrade.NpcBought[1] && lOTrade.NpcItems[1] != null, true);
        }

        /// <summary>
        /// Cleanup step 4.12c (2026-10-19, the user's decision "as the original"): the trade's first
        /// look into the partner rolls his loot INTO his chain as generate_inventory does - each piece
        /// at the head (Obj_Add, UnderworldExhumed obj/TREASURE.C:202-216, OBJECTS.C:267), the last
        /// rolled first, then what he carried; once. Until then the loot went to the tail in the
        /// rolled order. The roll itself is unchanged (the same draws from the same seed).
        /// SINCE 2026-10-19 (the user's decision "as the original") the conversation's START rolls
        /// it, before the script runs (Converse, conv/CONVERSE.C:329-330) - the first trade lookup
        /// until then; a lookup afterwards rolls nothing.
        /// </summary>
        private static void fCheckTradeLootRoll(CheckContext pOContext)
        {
            Check.Group("Trade loot roll (generate_inventory: at the head of the chain, once)");

            DataImport lOData = pOContext.Data;
            UWLevel lOLevel = pOContext.Level(0);
            const int Seed = 4121;

            if (lOLevel == null || lOData.ObjectClassProperties == null || lOData.CommonObjectProperties == null)
            {
                Check.Fail("trade loot roll", "no level or no tables");
                return;
            }

            // The first creature kind whose roll from the seed gives at least two pieces.
            int liKind = -1;
            List<int> liRolled = null;

            for (int liId = UWObjectIds.FirstCreature; liId < UWObjectIds.FirstCreature + 0x40 && liKind < 0; liId++)
            {
                if (!lOData.ObjectClassProperties.TryGetCritter(liId, out UWObjectClassProperties.Critter lORow))
                    continue;

                UWRandom.Use(new UWSessionRandom(Seed));

                List<UWCritterLoot.Drop> lODrops = UWCritterLoot.Generate(lORow, lOData.CommonObjectProperties,
                    lOLevel.LevelNumber, lOData.ObjectProperties);

                UWRandom.Use(null);

                if (lODrops.Count >= 2)
                {
                    liKind = liId;
                    liRolled = lODrops.Select(pODrop => pODrop.ObjectId).ToList();
                }
            }

            if (liKind < 0)
            {
                Check.Fail("trade loot roll", "no creature kind rolls two pieces");
                return;
            }

            int liCarriedA = fWorthlessId(lOData);
            int liCarriedB = fValuableIds(lOData, 1)[0];
            UWNpc lONpc = new UWNpc((ushort)liKind) { NPCLootSpawned = false, NPCAttitude = UWNpc.AttitudeFriendly };

            lONpc.EnsureContentsLoaded(null);
            lONpc.Contents.Add(new UWObject((ushort)liCarriedA) { Quality = 40 });
            lONpc.Contents.Add(new UWObject((ushort)liCarriedB) { Quality = 40 });

            // The conversation's start rolls the loot (CONVERSE.C:329-330), before any trade function.
            UWRandom.Use(new UWSessionRandom(Seed));
            UWConversationSession lOSession = new UWConversationSession(lOData, lONpc, 1, ConversationHostStub.Create(lOLevel),
                UWGameSession.Current);
            UWRandom.Use(null);

            List<int> liExpected = Enumerable.Reverse(liRolled).ToList();

            liExpected.Add(liCarriedA);
            liExpected.Add(liCarriedB);
            Check.Seq("trade roll: at the conversation's start, the loot at the head of the chain, the last rolled first, then what he carried",
                lONpc.Contents.Select(pOItem => (int)pOItem.ID).ToArray(), liExpected.ToArray());
            Check.Bool("trade roll: remembered", lONpc.NPCLootSpawned, true);

            // The first look into his chain (Resolve walks it) rolls nothing more.
            lOSession.Trade.Resolve(1);
            Check.Int("trade roll: once - the first trade lookup rolls nothing", lONpc.Contents.Count, liExpected.Count);

            UWConversationSession lOAgain = new UWConversationSession(lOData, lONpc, 1, ConversationHostStub.Create(lOLevel),
                UWGameSession.Current);

            Check.Int("trade roll: once - the next conversation rolls nothing", lONpc.Contents.Count, liExpected.Count);
        }

        /// <summary>The original's gold coin (ITEM_GOLD_COIN).</summary>
        private const int CheckGoldCoin = 0xA1;

        private static UWObject fCoins(int piCount, int piQuality = 40)
        {
            return new UWObject((ushort)CheckGoldCoin) { HasQuantity = true, Quantity = (ushort)piCount, Quality = (ushort)piQuality };
        }

        /// <summary>A53 and A57: what an accepted offer hands the partner (probablyTradeObjects,
        /// npc_inv_add).</summary>
        private static void fCheckBarterHandover(CheckContext pOContext)
        {
            Check.Group("Barter handover (probablyTradeObjects: refused goods stay, coins join his pile)");

            DataImport lOData = pOContext.Data;
            int liWorthless = fWorthlessId(lOData);
            List<int> liValuable = fValuableIds(lOData, 1);

            if (pOContext.Level(0) == null || liWorthless < 0 || liValuable.Count < 1)
            {
                Check.Fail("barter handover", "no level or not enough kinds of objects");
                return;
            }

            UWConversationTrade lOTrade = fMakeTrade(pOContext, new[] { liValuable[0] }, out UWNpc lONpc, out UWConversationSession lOSession);
            UWObject lOHisCoins = fCoins(5, 30);

            lONpc.Contents.Add(lOHisCoins);
            lOTrade.PutInPlayerSlot(0, fCoins(3, 50));
            lOTrade.PutInPlayerSlot(1, new UWObject((ushort)liWorthless) { Quality = 40 });
            lOTrade.Swap();

            Check.Int("handover: the paid coins join his pile", lOHisCoins.Quantity, 8);
            Check.Int("handover: his pile keeps its quality", lOHisCoins.Quality, 30);
            Check.Int("handover: no new pile in his chain", lONpc.Contents.Count, 2);
            Check.Bool("handover: the worthless good stays in the player's slot, marked",
                lOTrade.PlayerItems[1] != null && lOTrade.PlayerSelected[1], true);
            Check.Bool("handover: the coins' slot is empty", lOTrade.PlayerItems[0] == null, true);

            // A pile that would pass the stack limit stays a pile of its own, at the chain's head.
            lOTrade = fMakeTrade(pOContext, Array.Empty<int>(), out lONpc, out lOSession);
            lOHisCoins = fCoins(UWStackRules.MaxStackCount - 2);
            lONpc.Contents.Add(lOHisCoins);
            lOTrade.PutInPlayerSlot(0, fCoins(3));
            lOTrade.Swap();

            Check.Int("handover: over the limit the coins stay apart", lONpc.Contents.Count, 2);
            Check.Int("handover: ... at the head of his chain", lONpc.Contents[0].Quantity, 3);
        }

        /// <summary>A64: the inventory functions (conv_give_inv, give_ptr_npc, npc_inv_create,
        /// npc_barter_find).</summary>
        private static void fCheckTradeInventoryFunctions(CheckContext pOContext)
        {
            Check.Group("Trade inventory functions (give_to_npc, give_ptr_npc, do_inv_create, find_inv)");

            DataImport lOData = pOContext.Data;
            List<int> liValuable = fValuableIds(lOData, 3);
            UWConversations.Conversation lOConversation = lOData.Conversations != null ? lOData.Conversations.GetConversation(1) : null;

            if (pOContext.Level(0) == null || liValuable.Count < 3 || lOConversation == null)
            {
                Check.Fail("trade inventory functions", "no level, conversation or objects");
                return;
            }

            UWConversationVM lOVm = new UWConversationVM(lOConversation, new FakeScriptHost(), null);
            const int liArray = 300;

            // give_to_npc reads exactly count entries from the array's first word.
            UWConversationTrade lOTrade = fMakeTrade(pOContext, Array.Empty<int>(), out UWNpc lONpc, out UWConversationSession lOSession);
            UWObject lOFirst = new UWObject((ushort)liValuable[0]) { Quality = 40 };
            UWObject lOSecond = new UWObject((ushort)liValuable[1]) { Quality = 40 };

            lOTrade.PutInPlayerSlot(0, lOFirst);
            lOTrade.PutInPlayerSlot(1, lOSecond);

            lOVm.WriteMemory(liArray, 0);
            lOVm.WriteMemory(liArray + 1, lOTrade.HandleOf(lOFirst));
            Check.Int("give_to_npc: an entry naming nothing stops the gift",
                fCallTrade(lOSession, "give_to_npc", new[] { 1, 0 }, new[] { 0, liArray }, lOVm), 0);

            lOVm.WriteMemory(liArray, lOTrade.HandleOf(lOSecond));
            lOVm.WriteMemory(liArray + 1, lOTrade.HandleOf(lOSecond));
            Check.Int("give_to_npc: the same good twice is not two goods",
                fCallTrade(lOSession, "give_to_npc", new[] { 2, 0 }, new[] { 0, liArray }, lOVm), 0);

            lOVm.WriteMemory(liArray, lOTrade.HandleOf(lOFirst));
            Check.Int("give_to_npc: the first word names the good - given",
                fCallTrade(lOSession, "give_to_npc", new[] { 1, 0 }, new[] { 0, liArray }, lOVm), 1);
            Check.Bool("give_to_npc: ... it is in his chain, the other still in the slot",
                lONpc.Contents.Contains(lOFirst) && lOTrade.PlayerItems[1] == lOSecond, true);

            // give_ptr_npc takes from a marked slot only.
            lOTrade.TogglePlayerSelected(1);
            Check.Int("give_ptr_npc: an unmarked slot's good is not given",
                fCallTrade(lOSession, "give_ptr_npc", new[] { lOTrade.HandleOf(lOSecond), -1 }, null, lOVm), 0);
            lOTrade.TogglePlayerSelected(1);
            Check.Int("give_ptr_npc: a marked slot's good is given",
                fCallTrade(lOSession, "give_ptr_npc", new[] { lOTrade.HandleOf(lOSecond), -1 }, null, lOVm), 1);

            // do_inv_create joins a like pile and answers 0; a new pile of CreateObj's one.
            lOTrade = fMakeTrade(pOContext, Array.Empty<int>(), out lONpc, out lOSession);
            UWObject lOPile = fCoins(2);

            lONpc.Contents.Add(lOPile);
            Check.Int("do_inv_create: onto his coin pile - answers 0", fCallTrade(lOSession, "do_inv_create", new[] { CheckGoldCoin }, null, lOVm), 0);
            Check.Int("do_inv_create: ... the pile grew by one", lOPile.Quantity, 3);

            lONpc.Contents.Clear();
            Check.Bool("do_inv_create: without a pile it answers a handle",
                fCallTrade(lOSession, "do_inv_create", new[] { CheckGoldCoin }, null, lOVm) > 0, true);
            Check.Bool("do_inv_create: ... a pile of one, quality 63",
                lONpc.Contents.Count == 1 && lONpc.Contents[0].HasQuantity && lONpc.Contents[0].Quantity == 1
                && lONpc.Contents[0].Quality == 63, true);

            // find_inv on the partner looks into a bag.
            lOTrade = fMakeTrade(pOContext, Array.Empty<int>(), out lONpc, out lOSession);
            UWObject lOBag = new UWObject(0x80) { Quality = 40 };
            UWObject lOInside = new UWObject((ushort)liValuable[2]) { Quality = 40 };

            lOBag.EnsureContentsLoaded(null);
            lOBag.Contents.Add(lOInside);
            lONpc.Contents.Add(lOBag);
            Check.Int("find_inv: the partner's good inside a bag is found",
                fCallTrade(lOSession, "find_inv", new[] { liValuable[2], 0 }, null, lOVm), lOTrade.HandleOf(lOInside));
        }

        /// <summary>The level's critters in the active mobile list, in its order.</summary>
        private static List<UWNpc> fActiveCritters(UWLevel pOLevel)
        {
            List<UWNpc> lOCritters = new List<UWNpc>();

            foreach (int liIndex in pOLevel.ActiveMobiles)
                if (liIndex > 0 && liIndex < pOLevel.Masterlist.Count && pOLevel.Masterlist[liIndex] is UWNpc lONpc
                    && UWObjectIds.IsCreature(lONpc.ID))
                    lOCritters.Add(lONpc);

            return lOCritters;
        }

        /// <summary>A58 and A59: set_attitude and set_race_attitude (BABLHACK.C, gronk_whoami).</summary>
        private static void fCheckConversationAttitudes(CheckContext pOContext)
        {
            Check.Group("Conversation attitudes (set_attitude: the first active match; set_race_attitude: race, loner, box)");

            DataImport lOData = pOContext.Data;
            UWLevel lOLevel = pOContext.Level(0);

            if (lOLevel == null || lOLevel.ActiveMobiles == null)
            {
                Check.Fail("conversation attitudes", "no level");
                return;
            }

            List<UWNpc> lOCritters = fActiveCritters(lOLevel);
            Dictionary<UWNpc, int> lOSaved = lOCritters.ToDictionary(pONpc => pONpc, pONpc => new UWCritterRecord(pONpc).Attitude);

            try
            {
                // set_attitude reaches the first critter of the list with the whoami, no other.
                List<UWNpc> lONobodies = lOCritters.Where(pONpc => pONpc.NPCwhoami == 0).ToList();

                foreach (UWNpc lONpc in lONobodies)
                    new UWCritterRecord(lONpc).Attitude = 2;

                fMakeTrade(pOContext, Array.Empty<int>(), out UWNpc lOTalker, out UWConversationSession lOSession);
                fCallTrade(lOSession, "set_attitude", new[] { 0, 1 });

                Check.Int("set_attitude: one critter of whoami 0 changed",
                    lONobodies.Count(pONpc => new UWCritterRecord(pONpc).Attitude == 1), lONobodies.Count > 0 ? 1 : 0);
                Check.Bool("set_attitude: ... the first in the active list",
                    lONobodies.Count == 0 || new UWCritterRecord(lONobodies[0]).Attitude == 1, true);

                // set_race_attitude: a talker with kin of its own kind on the level.
                UWNpc lOKinTalker = lOCritters.FirstOrDefault(pONpc => lOCritters.Count(pOOther => pOOther.ID == pONpc.ID) >= 3);

                if (lOKinTalker == null)
                {
                    Check.Fail("set_race_attitude", "no kind with three critters on level 1");
                    return;
                }

                List<UWNpc> lOKin = lOCritters.Where(pONpc => pONpc.ID == lOKinTalker.ID).ToList();
                UWCritterRecord lOTalkerRecord = new UWCritterRecord(lOKinTalker);
                lOData.ObjectClassProperties.TryGetCritter(lOKinTalker.ID, out UWObjectClassProperties.Critter lOStats);
                UWConversationSession lOKinSession = new UWConversationSession(lOData, lOKinTalker, 1, ConversationHostStub.Create(lOLevel),
                    UWGameSession.Current);

                foreach (UWNpc lONpc in lOKin)
                    new UWCritterRecord(lONpc).Attitude = 2;

                fCallTrade(lOKinSession, "set_race_attitude", new[] { lOStats.GeneralType + 1, 0, 64 });
                Check.Bool("set_race_attitude: another race - nobody changes",
                    lOKin.All(pONpc => new UWCritterRecord(pONpc).Attitude == 2), true);

                fCallTrade(lOKinSession, "set_race_attitude", new[] { (int)lOStats.GeneralType, 0, 0 });
                Check.Bool("set_race_attitude: range 0 - the talker's tile alone",
                    lOKin.All(pONpc =>
                    {
                        UWCritterRecord lORecord = new UWCritterRecord(pONpc);
                        bool lbOnTile = lORecord.TileX == lOTalkerRecord.TileX && lORecord.TileY == lOTalkerRecord.TileY
                            && !lORecord.AttitudeLocked;

                        return lORecord.Attitude == (lbOnTile ? 0 : 2);
                    }), true);

                foreach (UWNpc lONpc in lOKin)
                    new UWCritterRecord(lONpc).Attitude = 2;

                fCallTrade(lOKinSession, "set_race_attitude", new[] { (int)lOStats.GeneralType, 1, 64 });
                Check.Bool("set_race_attitude: range 64 - all of the kind but the loners",
                    lOKin.All(pONpc => new UWCritterRecord(pONpc).Attitude == (new UWCritterRecord(pONpc).AttitudeLocked ? 2 : 1)), true);
            }
            finally
            {
                foreach (KeyValuePair<UWNpc, int> lOEntry in lOSaved)
                    new UWCritterRecord(lOEntry.Key).Attitude = lOEntry.Value;
            }
        }

        /// <summary>A66: a refused demand sets the object's goal and leaves the script's
        /// npc_attitude alone (do_demand, change_critter_goal).</summary>
        private static void fCheckRefusedDemand(CheckContext pOContext)
        {
            Check.Group("Refused demand (do_demand: goods back, the object attacks, npc_attitude untouched)");

            List<int> liValuable = fValuableIds(pOContext.Data, 2);
            UWConversationTrade lOTrade = fMakeTrade(pOContext, liValuable, out UWNpc lONpc, out UWConversationSession lOSession);

            if (lOTrade == null || liValuable.Count < 2)
            {
                Check.Fail("refused demand", "no trade or objects");
                return;
            }

            UWCritterRecord lORecord = new UWCritterRecord(lONpc);

            // In full health and more, so his score beats the absent player's.
            lONpc.HitPoints = 200;
            lORecord.Goal = 4;
            lORecord.BackupGoal = 0;
            fCallTrade(lOSession, "setup_to_barter");
            lOTrade.ToggleNpcSelected(0);
            lOTrade.ToggleNpcSelected(1);

            // No player here: his score of 1 never beats the partner's.
            Check.Int("demand: refused", fCallTrade(lOSession, "do_demand", new[] { 0, 0 }), 0);
            Check.Bool("demand: his goods are back in his chain", lONpc.Contents.Count == 2 && lOTrade.NpcItems.All(pOItem => pOItem == null), true);
            Check.Str("demand: the object attacks the player, the guard goal saved",
                lORecord.Goal + "/" + lORecord.GTarg + "/" + lORecord.BackupGoal, "5/1/4");

            lOSession.Abort();
            Check.Int("demand: npc_attitude untouched - the write-back keeps him friendly", lONpc.NPCAttitude, UWNpc.AttitudeFriendly);
        }

        /// <summary>A69: UW1 binds neither give_all_stuff nor do_input_wait (Converse's bab_fun list).</summary>
        private static void fCheckUw1Builtins()
        {
            Check.Group("UW1 built-ins (Converse binds no give_all_stuff, no do_input_wait)");

            UWConversationFunctionTable.Entry lOEntry;

            Check.Bool("built-ins: no give_all_stuff", UWConversationSession.Functions.TryGet("give_all_stuff", out lOEntry), false);
            Check.Bool("built-ins: no do_input_wait", UWConversationSession.Functions.TryGet("do_input_wait", out lOEntry), false);
            Check.Bool("built-ins: setup_to_barter is bound", UWConversationSession.Functions.TryGet("setup_to_barter", out lOEntry), true);
        }

        /// <summary>A67: the partner's name under the portrait (get_name).</summary>
        private static void fCheckPartnerName(CheckContext pOContext)
        {
            Check.Group("Partner name (get_name: an empty personal name shows nothing)");

            UWNpc lONameless = new UWNpc((ushort)UWObjectIds.FirstCreature) { NPCwhoami = 115, NPCAttitude = UWNpc.AttitudeFriendly };
            UWNpc lOGeneric = new UWNpc((ushort)UWObjectIds.FirstCreature) { NPCwhoami = 0, NPCAttitude = UWNpc.AttitudeFriendly };

            Check.Str("name: whoami 115, empty in block 7 - no name",
                new UWConversationSession(pOContext.Data, lONameless, 115, null, UWGameSession.Current).PartnerName, string.Empty);
            Check.Bool("name: whoami 0 - the object's name",
                !string.IsNullOrEmpty(new UWConversationSession(pOContext.Data, lOGeneric,
                    256 + (UWObjectIds.FirstCreature - UWObjectClassProperties.CritterFirstId), null, UWGameSession.Current).PartnerName),
                true);
        }

        /// <summary>A60: the write-back's goal goes through critter_set_goal (update_converse_data).</summary>
        private static void fCheckGoalWriteBack(CheckContext pOContext)
        {
            Check.Group("Goal write-back (change_critter_goal: a guard's goal saved)");

            UWNpc lONpc = new UWNpc((ushort)UWObjectIds.FirstCreature) { NPCAttitude = UWNpc.AttitudeFriendly, NPCGoal = 4, NPCGTarg = 1 };
            UWConversationSession lOSession = new UWConversationSession(pOContext.Data, lONpc, 1, ConversationHostStub.Create(pOContext.Level(0)),
                UWGameSession.Current);

            lOSession.Abort();

            UWCritterRecord lORecord = new UWCritterRecord(lONpc);

            Check.Str("write-back: a guard keeps goal 4 and saves it as its backup",
                lORecord.Goal + "/" + lORecord.GTarg + "/" + lORecord.BackupGoal, "4/1/4");
        }

        /// <summary>A55 and A65: who spoke last decides the closing pause; do_judgement is the
        /// player's line (Converse, play_say, npc_say).</summary>
        private static void fCheckClosingPause(CheckContext pOContext)
        {
            Check.Group("Closing pause (convo_say_flag: the player's line last, the partner not hostile)");

            UWConversationTrade lOTrade = fMakeTrade(pOContext, Array.Empty<int>(), out UWNpc lONpc, out UWConversationSession lOSession);

            if (lOTrade == null)
            {
                Check.Fail("closing pause", "no trade");
                return;
            }

            fCallTrade(lOSession, "do_judgement");
            Check.Bool("do_judgement: the appraisal is the player's line",
                lOSession.Lines.Count > 0 && lOSession.Lines[lOSession.Lines.Count - 1].IsPlayer, true);
            lOSession.Abort();
            Check.Bool("closing: the player spoke last - the timed pause", lOSession.EndsWithTimedPause, true);

            fMakeTrade(pOContext, Array.Empty<int>(), out lONpc, out lOSession);
            fCallTrade(lOSession, "do_judgement");
            lOSession.Say("Farewell.");
            lOSession.Abort();
            Check.Bool("closing: the partner spoke last - wait for a key", lOSession.EndsWithTimedPause, false);

            fMakeTrade(pOContext, Array.Empty<int>(), out lONpc, out lOSession);
            fCallTrade(lOSession, "do_judgement");
            lOSession.Print("He turns away.");
            lOSession.Abort();
            Check.Bool("closing: narration leaves the player's line the last one", lOSession.EndsWithTimedPause, true);
        }

        /// <summary>A54: the name identify_inv hands a conversation (conv_inv_name).</summary>
        private static void fCheckIdentifyName(CheckContext pOContext)
        {
            Check.Group("identify_inv name (conv_inv_name: no condition word, the article on request)");

            DataImport lOData = pOContext.Data;
            UWObject lOSword = new UWObject(0x05) { Quality = 10 };

            Check.Str("identify: Shak's 'this @SS5' - no article, no condition word",
                UWItemDescriptions.IdentifyName(lOSword, lOData, null, false, 0), "longsword");
            Check.Str("identify: with the article asked for",
                UWItemDescriptions.IdentifyName(lOSword, lOData, null, true, 0), "a longsword");
            Check.Str("identify: a pile - its count and the plural (an 's' where the name has none)",
                UWItemDescriptions.IdentifyName(fCoins(3), lOData, null, true, 0), "3 gold coins");
        }

        /// <summary>A62: a drop on one of the player's trade slots (ovr097_CDB, CombineToSlot_ovr097_D30).</summary>
        private static void fCheckTradeSlotDrop(CheckContext pOContext)
        {
            Check.Group("Trade slot drop (CombineToSlot: piles keep their quality, a bag takes nothing)");

            UWConversationTrade lOTrade = fMakeTrade(pOContext, Array.Empty<int>(), out UWNpc lONpc, out UWConversationSession lOSession);

            if (lOTrade == null)
            {
                Check.Fail("trade slot drop", "no trade");
                return;
            }

            UWObject lOPile = fCoins(4, 20);

            Check.Bool("drop: an empty slot takes it", lOTrade.DropOnPlayerSlot(0, lOPile) == null && lOTrade.PlayerItems[0] == lOPile, true);
            Check.Bool("drop: ... and marks it", lOTrade.PlayerSelected[0], true);

            lOTrade.TogglePlayerSelected(0);
            Check.Bool("drop: a like pile joins", lOTrade.DropOnPlayerSlot(0, fCoins(6, 60)) == null, true);
            Check.Int("drop: ... the counts add up", lOPile.Quantity, 10);
            Check.Int("drop: ... the slot's pile keeps its quality", lOPile.Quality, 20);
            Check.Bool("drop: ... and the slot is marked again", lOTrade.PlayerSelected[0], true);

            UWObject lOOther = new UWObject((ushort)(CheckGoldCoin - 1)) { HasQuantity = true, Quantity = 2, Quality = 40 };

            Check.Bool("drop: another item swaps", lOTrade.DropOnPlayerSlot(0, lOOther) == lOPile && lOTrade.PlayerItems[0] == lOOther, true);

            UWObject lOBag = new UWObject(0x80) { Quality = 40 };
            UWObject lOHeld = fCoins(1);

            lOTrade.DropOnPlayerSlot(1, lOBag);
            lOTrade.TogglePlayerSelected(1);
            Check.Bool("drop: a bag in the slot refuses - the held thing comes back", lOTrade.DropOnPlayerSlot(1, lOHeld) == lOHeld, true);
            Check.Bool("drop: ... the bag stays, marked", lOTrade.PlayerItems[1] == lOBag && lOTrade.PlayerSelected[1], true);
        }
    }
}
