using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The conversations of CNV.ARK: as readable bytecode (the editor's conversation dump), and
    /// run without the game (the editor's conversation run) - the cross-check for the virtual
    /// machine (UWConversationVM): if the instruction set, jump targets and calling convention
    /// are right, readable dialogue comes out; if something is wrong, gibberish, an error or an
    /// endless loop - all three are noticeable at once.
    /// </summary>
    public static partial class Program
    {
        /// <summary>Answers before a single run is aborted, so that a conversation with a return
        /// loop does not run forever.</summary>
        private const int ConversationChoiceLimit = 12;

        /// <summary>The same for the run over every conversation.</summary>
        private const int ConversationAllChoiceLimit = 40;

        private static int fConversation(DumpContext pOContext, List<string> pOArgs)
        {
            UWConversations lOConversations = pOContext.Data.Conversations;

            if (lOConversations == null || !lOConversations.IsLoaded)
            {
                Console.Error.WriteLine("CNV.ARK not loaded.");

                return 4;
            }

            string lsRun = fTakeOption(pOArgs, "--run");
            string lsOut = fTakeOption(pOArgs, "--out");
            string lsWhich = lsRun ?? (pOArgs.Count > 0 ? pOArgs[0] : "all");
            bool lbAll = string.Equals(lsWhich, "all", StringComparison.OrdinalIgnoreCase);
            int liSlot = 0;

            if (!lbAll && (!fParse(lsWhich, out liSlot) || liSlot < 0 || liSlot >= lOConversations.SlotCount))
            {
                Console.Error.WriteLine("Which conversation? A slot from 0 to " + (lOConversations.SlotCount - 1) + ", or all.");

                return 1;
            }

            if (lsRun != null)
                return lbAll ? fRunAllConversations(pOContext.Data) : fRunConversation(pOContext.Data, liSlot);

            return lbAll ? fDisassembleAll(pOContext.Data, lsOut) : fDisassembleOne(pOContext.Data, liSlot);
        }

        /// <summary>One conversation's bytecode with its name and string block.</summary>
        private static int fDisassembleOne(DataImport pOData, int piSlot)
        {
            UWConversations.Conversation lOConversation = pOData.Conversations.GetConversation(piSlot);

            if (lOConversation == null)
            {
                Console.Error.WriteLine("Slot " + piSlot + " holds no conversation.");

                return 4;
            }

            Console.WriteLine(string.Format("--- Slot {0}, name '{1}', string block 0x{2:X4} ---",
                piSlot, fPartnerName(pOData, piSlot), lOConversation.StringBlock));
            Console.Write(UWConversationDisassembler.Disassemble(lOConversation, pOData.Strings));

            return 0;
        }

        /// <summary>
        /// The overview of every conversation - doubles as the test of the parser: the names of
        /// the built-in functions must match uw-formats.txt 7.6.1 (babl_menu, babl_fmenu, print,
        /// get_quest, set_quest, ...), and each conversation's string block the blocks from
        /// 0x0E01 upwards. With --out every conversation's bytecode goes to conv_NNN.txt there
        /// (the original's content: never into the repository).
        /// </summary>
        private static int fDisassembleAll(DataImport pOData, string psOut)
        {
            UWConversations lOConversations = pOData.Conversations;
            int liUsed = 0;
            int liCodeWords = 0;
            Dictionary<string, int> lOFunctionUse = new Dictionary<string, int>();

            if (psOut != null)
                Directory.CreateDirectory(psOut);

            for (int liSlot = 0; liSlot < lOConversations.SlotCount; liSlot++)
            {
                UWConversations.Conversation lOConversation = lOConversations.GetConversation(liSlot);

                if (lOConversation == null)
                    continue;

                liUsed++;
                liCodeWords += lOConversation.Code.Length;

                foreach (UWConversations.Import lOImport in lOConversation.Imports)
                {
                    if (!lOImport.IsFunction)
                        continue;

                    lOFunctionUse.TryGetValue(lOImport.Name, out int liCount);
                    lOFunctionUse[lOImport.Name] = liCount + 1;
                }

                if (psOut != null)
                    File.WriteAllText(Path.Combine(psOut, string.Format("conv_{0:000}.txt", liSlot)),
                        UWConversationDisassembler.Disassemble(lOConversation, pOData.Strings));
            }

            Console.WriteLine(string.Format("{0} slots in CNV.ARK, {1} of them used, {2} words of code in total",
                lOConversations.SlotCount, liUsed, liCodeWords));
            Console.WriteLine();
            Console.WriteLine("--- built-in functions, by frequency ---");

            // Most used first; equal counts by name, so the list is the same on every run.
            List<KeyValuePair<string, int>> lOSorted = new List<KeyValuePair<string, int>>(lOFunctionUse);
            lOSorted.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));

            foreach (KeyValuePair<string, int> lOPair in lOSorted)
                Console.WriteLine(string.Format("  {0,-28} in {1} conversations", lOPair.Key, lOPair.Value));

            if (psOut != null)
            {
                Console.WriteLine();
                Console.WriteLine("Full disassembly in " + psOut);
            }

            return 0;
        }

        /// <summary>Runs every existing conversation, answering 1 every time, and counts how
        /// they end: errors grouped by message, and the functions no host implements here.</summary>
        private static int fRunAllConversations(DataImport pOData)
        {
            int liTotal = 0;
            int liFinished = 0;
            Dictionary<string, int> lOErrors = new Dictionary<string, int>();
            Dictionary<string, int> lOUnknown = new Dictionary<string, int>();

            for (int liSlot = 1; liSlot < pOData.Conversations.SlotCount; liSlot++)
            {
                UWConversations.Conversation lOConversation = pOData.Conversations.GetConversation(liSlot);

                if (lOConversation == null || lOConversation.Code == null || lOConversation.Code.Length == 0)
                    continue;

                liTotal++;

                RecordingHost lOHost = new RecordingHost(new StringBuilder());
                UWConversationVM lOVm = new UWConversationVM(lOConversation, lOHost,
                    piIndex => fConversationString(pOData, lOConversation.StringBlock, piIndex));

                lOVm.SetImportedGlobal("play_name", 1);
                lOVm.SetImportedGlobal("npc_whoami", liSlot);
                lOVm.SetImportedGlobal("npc_attitude", 2);

                int liChoices = 0;

                while (true)
                {
                    UWConversationVM.RunState leState = lOVm.Run();

                    if (leState == UWConversationVM.RunState.Finished)
                    {
                        liFinished++;
                        break;
                    }

                    if (leState == UWConversationVM.RunState.Failed)
                    {
                        // Numbers stripped from the message, so that similar errors are grouped.
                        fCount(lOErrors, System.Text.RegularExpressions.Regex.Replace(lOVm.Error, "[0-9]+", "N"));
                        break;
                    }

                    if (leState != UWConversationVM.RunState.AwaitingChoice)
                        break;

                    liChoices++;

                    if (liChoices > ConversationAllChoiceLimit)
                    {
                        fCount(lOErrors, "too many choices");
                        break;
                    }

                    lOVm.SupplyChoice(1);
                }

                foreach (string lsName in lOHost.UnknownCalls)
                    fCount(lOUnknown, lsName);
            }

            Console.WriteLine(string.Format("{0} of {1} conversations run through to the end.", liFinished, liTotal));

            if (lOErrors.Count > 0)
            {
                Console.WriteLine("Errors:");

                foreach (KeyValuePair<string, int> lOPair in lOErrors)
                    Console.WriteLine(string.Format("  {0}x  {1}", lOPair.Value, lOPair.Key));
            }

            Console.WriteLine("Functions called but not implemented by this run's host:");

            foreach (KeyValuePair<string, int> lOPair in lOUnknown)
                Console.WriteLine(string.Format("  {0,-24} {1}x", lOPair.Key, lOPair.Value));

            return 0;
        }

        private static void fCount(Dictionary<string, int> pOCounts, string psKey)
        {
            pOCounts.TryGetValue(psKey, out int liCount);
            pOCounts[psKey] = liCount + 1;
        }

        /// <summary>One conversation as a transcript: what the NPC says, the choices, the
        /// quest flags it sets and the functions it calls.</summary>
        private static int fRunConversation(DataImport pOData, int piSlot)
        {
            StringBuilder lOOut = new StringBuilder();
            UWConversations.Conversation lOConversation = pOData.Conversations.GetConversation(piSlot);

            lOOut.AppendLine("=== Conversation " + piSlot + " (" + fPartnerName(pOData, piSlot) + ")");

            if (lOConversation == null)
            {
                lOOut.AppendLine("  no program");
                Console.Write(lOOut.ToString());

                return 0;
            }

            RecordingHost lOHost = new RecordingHost(lOOut);
            UWConversationVM lOVm = new UWConversationVM(lOConversation, lOHost,
                piIndex => fConversationString(pOData, lOConversation.StringBlock, piIndex));

            // A few game globals, so that placeholders like @GS name have something to show.
            lOVm.SetImportedGlobal("play_name", 1);
            lOVm.SetImportedGlobal("play_level", 5);
            lOVm.SetImportedGlobal("npc_whoami", piSlot);
            lOVm.SetImportedGlobal("npc_attitude", 2);
            lOVm.SetImportedGlobal("npc_talkedto", 0);
            lOVm.SetImportedGlobal("dungeon_level", 1);

            int liChoices = 0;

            while (true)
            {
                UWConversationVM.RunState leState = lOVm.Run();

                if (leState == UWConversationVM.RunState.Finished)
                {
                    lOOut.AppendLine("  [End]");
                    break;
                }

                if (leState == UWConversationVM.RunState.Failed)
                {
                    lOOut.AppendLine("  [ERROR] " + lOVm.Error);
                    break;
                }

                if (leState != UWConversationVM.RunState.AwaitingChoice)
                    break;

                for (int liAt = 0; liAt < lOVm.PendingChoices.Count; liAt++)
                    lOOut.AppendLine(string.Format("    {0}) {1}", liAt + 1, fCleanLine(lOVm.PendingChoices[liAt])));

                liChoices++;

                if (liChoices > ConversationChoiceLimit)
                {
                    lOOut.AppendLine("  [aborted after " + ConversationChoiceLimit + " answers]");
                    break;
                }

                lOOut.AppendLine("  -> choose 1");

                lOVm.SupplyChoice(1);
            }

            lOOut.AppendLine();
            Console.Write(lOOut.ToString());

            return 0;
        }

        private static string fConversationString(DataImport pOData, int piBlock, int piIndex)
        {
            try
            {
                if (!pOData.Strings.Blocks.ContainsKey(piBlock))
                    return string.Empty;

                UWStringBlock lOBlock = pOData.Strings.Blocks[piBlock];

                // The same offset by one as everywhere in this project: our parsed string blocks
                // lie one entry above the game's numbering. Without it Hagbard speaks a line too
                // early, and his greeting appears as the player's first answer option.
                int liIndex = piIndex + 1;

                if (liIndex < 0 || liIndex >= lOBlock.Strings.Count)
                    return string.Empty;

                return lOBlock.Strings[liIndex].TrimEnd('\r', '\n');
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string fPartnerName(DataImport pOData, int piSlot)
        {
            try
            {
                return pOData.Strings.Blocks[UWConversations.PartnerNameStringBlock]
                    .Strings[UWConversations.GetPartnerNameIndex(piSlot)].TrimEnd('\r', '\n');
            }
            catch
            {
                return "?";
            }
        }

        private static string fCleanLine(string psText)
        {
            return psText == null ? string.Empty : psText.Replace("\n", " ").Replace("\r", string.Empty);
        }

        /// <summary>The host of a run without the game: records what is said and answers every
        /// question with a default (quest flags 0, random 1, unknown functions 0).</summary>
        private sealed class RecordingHost : UWConversationVM.IHost
        {
            private readonly StringBuilder mOOut;

            /// <summary>Names of the functions this host does not know, in the order first met.</summary>
            public readonly List<string> UnknownCalls = new List<string>();

            public RecordingHost(StringBuilder pOOut)
            {
                mOOut = pOOut;
            }

            public void Say(string psText)
            {
                mOOut.AppendLine("  NPC: " + fCleanLine(psText));
            }

            public void Print(string psText)
            {
                mOOut.AppendLine("  --  " + fCleanLine(psText));
            }

            public int GetQuest(int piFlag)
            {
                return 0;
            }

            public void SetQuest(int piFlag, int piValue)
            {
                mOOut.AppendLine(string.Format("  [Quest {0} := {1}]", piFlag, piValue));
            }

            public int Random(int piMaximum)
            {
                return 1;
            }

            public int CallUnknown(string psName, int piFunctionId, IReadOnlyList<int> pOArguments,
                IReadOnlyList<int> pOAddresses, UWConversationVM pOVm)
            {
                StringBuilder lOArguments = new StringBuilder();

                foreach (int liArgument in pOArguments)
                    lOArguments.Append(liArgument).Append(' ');

                if (!UnknownCalls.Contains(psName))
                    UnknownCalls.Add(psName);

                mOOut.AppendLine(string.Format("  [{0}({1})]", psName, lOArguments.ToString().Trim()));

                return 0;
            }
        }
    }
}
