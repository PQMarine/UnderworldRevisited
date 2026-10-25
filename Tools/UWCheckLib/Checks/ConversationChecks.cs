using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The conversation's small rules: who answers, the patches, the keyword test and the saved
    /// base pointer of the virtual machine.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>UWClickRules.CreatureAnswers, see TalkTo_ovr100_0 (per user, 2026-10-01: Oradinar fleeing the
        /// player does not answer in the original).</summary>
        private static void fCheckTalkRefusal()
        {
            fExpectBool("talk: Oradinar withdrawing from the player, upset", UWClickRules.CreatureAnswers(136, 6, 1, 1, false), false);
            fExpectBool("talk: the same, mellow and wandering", UWClickRules.CreatureAnswers(136, 8, 0, 2, false), true);
            fExpectBool("talk: attacking someone else", UWClickRules.CreatureAnswers(136, 5, 7, 1, false), true);
            fExpectBool("talk: hostile", UWClickRules.CreatureAnswers(136, 8, 0, 0, false), false);
            fExpectBool("talk: hostile with goal 10", UWClickRules.CreatureAnswers(136, 10, 1, 0, false), true);
            fExpectBool("talk: an ally attacking the player still answers", UWClickRules.CreatureAnswers(136, 5, 1, 3, true), true);
            fExpectBool("talk: Rodrick always", UWClickRules.CreatureAnswers(0x8E, 5, 1, 0, false), true);
        }

        /// <summary>UWConversationPatches: the trolls' news counter (conversation 288) starts at 0
        /// and the array is written one word on - in the VM's copy only.</summary>
        private static void fCheckConversationPatches(DataImport pOData)
        {
            UWConversations.Conversation lOTrolls = pOData.Conversations != null ? pOData.Conversations.GetConversation(288) : null;

            if (lOTrolls == null || lOTrolls.Code == null || lOTrolls.Code.Length < 927)
            {
                fFail("conversation patch: the generic trolls (288)", "not loaded");
                return;
            }

            ushort[] lyRun = UWConversationPatches.GetCode(lOTrolls);

            fExpectInt("conversation patch: 869 is PUSHI 1 in the original", lOTrolls.Code[870], 1);
            fExpectInt("conversation patch: the counter starts at 0 in the VM", lyRun[870], 0);
            fExpectInt("conversation patch: 895 is PUSHI_EFF 1 in the original", lOTrolls.Code[896], 1);
            fExpectInt("conversation patch: the array is written from local 2 in the VM", lyRun[896], 2);
            fExpectInt("conversation patch: the none-left test stays at 0", lyRun[926], 0);
            fExpectBool("conversation patch: the loaded code is untouched", lyRun != lOTrolls.Code, true);

            // The whole routine, run: one conversation per question, as the troll ends it after
            // the news. Six different ones, then "no new news" with every flag cleared, then again.
            var lOBlock = pOData.Strings.Blocks[lOTrolls.StringBlock];
            int[] liMemory = new int[lOTrolls.MemorySlots];
            List<string> lOHeard = new List<string>();
            bool lbFailed = false;

            for (int liTalk = 0; liTalk < 8; liTalk++)
            {
                FakeScriptHost lOHost = new FakeScriptHost { RollDice = true };
                UWConversationVM lOVm = new UWConversationVM(lOTrolls, lOHost,
                    piIndex => piIndex + 1 >= 0 && piIndex + 1 < lOBlock.Strings.Count ? lOBlock.Strings[piIndex + 1].Trim() : string.Empty);

                lOVm.LoadMemory(liMemory);

                for (int liStep = 0; liStep < 20; liStep++)
                {
                    UWConversationVM.RunState leState = lOVm.Run();

                    if (leState == UWConversationVM.RunState.Failed)
                        lbFailed = true;

                    if (leState != UWConversationVM.RunState.AwaitingChoice)
                        break;

                    int liChoice = lOVm.PendingChoices.FindIndex(psChoice => psChoice.Contains("news"));

                    lOVm.SupplyChoice((liChoice < 0 ? lOVm.PendingChoices.Count - 1 : liChoice) + 1);
                }

                lOVm.SaveMemory(liMemory);
                lOHeard.Add(lOHost.Said.Count > 0 ? lOHost.Said[lOHost.Said.Count - 1] : string.Empty);
            }

            fExpectBool("troll news: no conversation runs into the endless loop", lbFailed, false);
            fExpectInt("troll news: six different news first", new HashSet<string>(lOHeard.GetRange(0, 6)).Count, 6);
            fExpectBool("troll news: then \"no new news\"", lOHeard[6].StartsWith("I have no new news"), true);
            fExpectBool("troll news: and it starts over", lOHeard[7].Length > 0 && !lOHeard[7].StartsWith("I have no new news"), true);
        }

        /// <summary>UWConversationTextRules.ContainsWord after Contains_ovr093_ED4: the keyword as a
        /// whole word of the typed text, case aside.</summary>
        private static void fCheckContainsWord()
        {
            fExpectBool("contains: the word itself", UWConversationTextRules.ContainsWord("rawstag", "rawstag"), true);
            fExpectBool("contains: any case", UWConversationTextRules.ContainsWord("Tell me of RAWSTAG!", "rawstag"), true);
            fExpectBool("contains: in a sentence", UWConversationTextRules.ContainsWord("the knight, please", "knight"), true);
            fExpectBool("contains: not a part of a word", UWConversationTextRules.ContainsWord("knightly", "knight"), false);
            fExpectBool("contains: not a part typed", UWConversationTextRules.ContainsWord("kn", "knight"), false);
            fExpectBool("contains: a later whole word after a part", UWConversationTextRules.ContainsWord("knights knight", "knight"), true);
            fExpectBool("contains: a digit is no boundary", UWConversationTextRules.ContainsWord("pit2", "pit"), false);
            fExpectBool("contains: empty text", UWConversationTextRules.ContainsWord("", "pit"), false);
        }

        /// <summary>UWConversationVM PUSHBP/POPBP (2026-09-29): the saved base pointer counts from
        /// the stack, as in UW.EXE. A called function that writes element 0 of a local array
        /// overwrites its caller's saved base pointer with -1 (the generic Gray Goblin's do_offer
        /// function); the caller's locals must stay in the stack, not fall onto play_hp.</summary>
        private static void fCheckConversationBasePointer()
        {
            Check.Group("Conversation base pointer");

            const int PlayHpAddress = 4;

            ushort[] lyCode =
            {
                0x22,               //  0 START
                0x13, 5,            //  1 CALL 5
                0x26,               //  3 EXIT_OP
                0x00,               //  4 NOP
                0x1A, 0x1C,         //  5 PUSHBP, SPTOBP - the caller
                0x16, 5, 0x1E,      //  7 PUSHI 5, ADDSP
                0x13, 20,           // 10 CALL 20
                0x17, 5,            // 12 PUSHI_EFF 5 - its fifth local
                0x16, 0, 0x20,      // 14 PUSHI 0, STO
                0x1D, 0x1B, 0x15,   // 17 BPTOSP, POPBP, RET
                0x1A, 0x1C,         // 20 PUSHBP, SPTOBP - the callee
                0x16, 3, 0x1E,      // 22 PUSHI 3, ADDSP
                0x16, 0, 0x17, 1,   // 25 PUSHI 0, PUSHI_EFF 1
                0x21,               // 29 OFFSET - element 0: the saved base pointer
                0x16, 1, 0x29,      // 30 PUSHI 1, OPNEG
                0x20,               // 33 STO
                0x1D, 0x1B, 0x15    // 34 BPTOSP, POPBP, RET
            };

            UWConversations.Conversation lOConversation = new UWConversations.Conversation
            {
                MemorySlots = 35,
                Code = lyCode
            };

            lOConversation.Imports.Add(new UWConversations.Import
            {
                Name = "play_hp",
                IdOrAddress = PlayHpAddress,
                ImportType = UWConversations.VariableImportType
            });

            UWConversationVM lOVm = new UWConversationVM(lOConversation, new FakeScriptHost(), null);

            lOVm.SetImportedGlobal("play_hp", 113);

            UWConversationVM.RunState leState = UWConversationVM.RunState.Running;

            for (int liStep = 0; liStep < 100 && leState == UWConversationVM.RunState.Running; liStep++)
                leState = lOVm.Run();

            fExpectBool("conversation: the hand-made program finishes", leState == UWConversationVM.RunState.Finished, true);
            fExpectInt("conversation: play_hp untouched after a callee wrote -1 on the saved base pointer",
                lOVm.GetImportedGlobal("play_hp"), 113);
        }
    }
}
