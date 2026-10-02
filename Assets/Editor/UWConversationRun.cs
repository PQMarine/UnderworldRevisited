using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Runs a conversation without the game and records what comes out.
///
/// This is the cross-check for the virtual machine (UWConversationVM): if the
/// instruction set, jump targets and calling convention are right, readable dialogue results. If
/// something is wrong, gibberish, an error or an endless loop comes out - all three are
/// noticeable immediately.
///
/// Every choice is answered with the first option, and after ChoiceLimit answers (40 when
/// CheckAll runs every conversation) the run is aborted, so that a conversation with a
/// return loop does not run forever.
/// </summary>
public static class UWConversationRun
{
    /// <summary>Conversation slots of the level 1 NPCs (from npc_whoami): three outcasts and
    /// a goblin.</summary>
    private static readonly int[] msSlots = new int[] { 19, 20, 66, 67 };

    /// <summary>If true, EVERY conversation in the game runs instead of the four examples,
    /// and only the error summary is output. This finds unimplemented instructions and
    /// stack errors that slip through with four examples.</summary>
    // static readonly instead of const: as a constant the compiler considers the other branch
    // unreachable and warns. The switch stays the same.
    private static readonly bool CheckAll = true;

    private const int ChoiceLimit = 12;

    [MenuItem("Underworld Revisited/Diagnostics/Run conversations")]
    public static void Run()
    {
        UnderworldRevisited.UWSettings lOSettings = AssetDatabase.LoadAssetAtPath<UnderworldRevisited.UWSettings>("Assets/Resources/UWSettings.asset");
        DataImport lOData = new DataImport(lOSettings.DataPath);
        StringBuilder lOOut = new StringBuilder();

        if (CheckAll)
        {
            fCheckAll(lOData, lOOut);
        }
        else
        {
            foreach (int liSlot in msSlots)
                fRunOne(lOData, liSlot, lOOut);
        }

        Debug.Log("UWCONVRUN\n" + lOOut.ToString());
    }

    /// <summary>Runs every existing conversation and counts how it ends.</summary>
    private static void fCheckAll(DataImport pOData, StringBuilder pOOut)
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

            StringBuilder lOTrash = new StringBuilder();
            Host lOHost = new Host(lOTrash);

            UWConversationVM lOVm = new UWConversationVM(lOConversation, lOHost,
                piIndex => fGetString(pOData, lOConversation.StringBlock, piIndex));

            lOVm.SetImportedGlobal("play_name", 1);
            lOVm.SetImportedGlobal("npc_whoami", liSlot);
            lOVm.SetImportedGlobal("npc_attitude", 2);

            int liChoices = 0;
            bool lbOk = false;

            while (true)
            {
                UWConversationVM.RunState leState = lOVm.Run();

                if (leState == UWConversationVM.RunState.Finished)
                {
                    lbOk = true;
                    break;
                }

                if (leState == UWConversationVM.RunState.Failed)
                {
                    string lsKey = lOVm.Error;

                    // Strip numbers from the message so that similar errors are grouped together.
                    lsKey = System.Text.RegularExpressions.Regex.Replace(lsKey, "[0-9]+", "N");

                    lOErrors.TryGetValue(lsKey, out int liCount);
                    lOErrors[lsKey] = liCount + 1;

                    break;
                }

                if (leState != UWConversationVM.RunState.AwaitingChoice)
                    break;

                liChoices++;

                if (liChoices > 40)
                {
                    lOErrors.TryGetValue("too many choices", out int liCount);
                    lOErrors["too many choices"] = liCount + 1;

                    break;
                }

                lOVm.SupplyChoice(1);
            }

            if (lbOk)
                liFinished++;

            foreach (string lsName in lOHost.UnknownCalls)
            {
                lOUnknown.TryGetValue(lsName, out int liCount);
                lOUnknown[lsName] = liCount + 1;
            }
        }

        pOOut.AppendLine(string.Format("{0} of {1} conversations run through to the end.", liFinished, liTotal));

        if (lOErrors.Count > 0)
        {
            pOOut.AppendLine("Errors:");

            foreach (KeyValuePair<string, int> lOPair in lOErrors)
                pOOut.AppendLine(string.Format("  {0}x  {1}", lOPair.Value, lOPair.Key));
        }

        pOOut.AppendLine("Functions called but not yet implemented:");

        foreach (KeyValuePair<string, int> lOPair in lOUnknown)
            pOOut.AppendLine(string.Format("  {0,-24} {1}x", lOPair.Key, lOPair.Value));
    }

    private static void fRunOne(DataImport pOData, int piSlot, StringBuilder pOOut)
    {
        UWConversations.Conversation lOConversation = pOData.Conversations.GetConversation(piSlot);

        pOOut.AppendLine("=== Conversation " + piSlot + " (" + fPartnerName(pOData, piSlot) + ")");

        if (lOConversation == null)
        {
            pOOut.AppendLine("  no program");
            return;
        }

        Host lOHost = new Host(pOOut);

        UWConversationVM lOVm = new UWConversationVM(lOConversation, lOHost,
            piIndex => fGetString(pOData, lOConversation.StringBlock, piIndex));

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
                pOOut.AppendLine("  [End]");
                break;
            }

            if (leState == UWConversationVM.RunState.Failed)
            {
                pOOut.AppendLine("  [ERROR] " + lOVm.Error);
                break;
            }

            if (leState != UWConversationVM.RunState.AwaitingChoice)
                break;

            for (int i = 0; i < lOVm.PendingChoices.Count; i++)
                pOOut.AppendLine(string.Format("    {0}) {1}", i + 1, fClean(lOVm.PendingChoices[i])));

            liChoices++;

            if (liChoices > ChoiceLimit)
            {
                pOOut.AppendLine("  [aborted after " + ChoiceLimit + " answers]");
                break;
            }

            pOOut.AppendLine("  -> choose 1");

            lOVm.SupplyChoice(1);
        }

        pOOut.AppendLine();
    }

    private static string fGetString(DataImport pOData, int piBlock, int piIndex)
    {
        try
        {
            if (!pOData.Strings.Blocks.ContainsKey(piBlock))
                return string.Empty;

            var lOBlock = pOData.Strings.Blocks[piBlock];

            // The same offset by one as everywhere in this project: our parsed
            // string blocks lie one entry above the game's numbering. Without it
            // Hagbard speaks a line too early, and his greeting appears as the player's
            // first answer option.
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

    private static string fClean(string psText)
    {
        return psText == null ? string.Empty : psText.Replace("\n", " ").Replace("\r", string.Empty);
    }

    /// <summary>The host for the test run: records output and answers with default values.</summary>
    private class Host : UWConversationVM.IHost
    {
        private readonly StringBuilder mOOut;

        /// <summary>Names of the functions this host does not know.</summary>
        public readonly List<string> UnknownCalls = new List<string>();

        public Host(StringBuilder pOOut)
        {
            mOOut = pOOut;
        }

        public void Say(string psText)
        {
            mOOut.AppendLine("  NPC: " + fClean(psText));
        }

        public void Print(string psText)
        {
            mOOut.AppendLine("  --  " + fClean(psText));
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
                lOArguments.Append(liArgument).Append(" ");

            if (!UnknownCalls.Contains(psName))
                UnknownCalls.Add(psName);

            mOOut.AppendLine(string.Format("  [{0}({1})]", psName, lOArguments.ToString().Trim()));

            return 0;
        }
    }
}
