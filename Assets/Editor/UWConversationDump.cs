using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Writes all conversations from CNV.ARK out as readable bytecode and prints an
/// overview to the console.
///
/// The overview doubles as the test of the parser: the names of the built-in
/// functions must match those in uw-formats.txt 7.6.1 (babl_menu, babl_fmenu,
/// print, get_quest, set_quest, ...), and each conversation's string block must match the
/// blocks from 0x0E01 upwards. If both hold, header and import table are parsed correctly.
/// </summary>
public static class UWConversationDump
{
    private const string OutputDirectory = "Assets/UWExportedStrings/Conversations";

    [MenuItem("Underworld Revisited/Diagnostics/Disassemble conversations")]
    public static void Dump()
    {
        UnderworldRevisited.UWSettings lOSettings = AssetDatabase.LoadAssetAtPath<UnderworldRevisited.UWSettings>("Assets/Resources/UWSettings.asset");
        DataImport lOData = new DataImport(lOSettings.DataPath);
        UWConversations lOConversations = lOData.Conversations;

        if (!lOConversations.IsLoaded)
        {
            Debug.LogError("UWCNV CNV.ARK not loaded");
            return;
        }

        Directory.CreateDirectory(OutputDirectory);

        StringBuilder lOOut = new StringBuilder();
        int liUsed = 0;
        int liCodeWords = 0;

        System.Collections.Generic.Dictionary<string, int> lOFunctionUse =
            new System.Collections.Generic.Dictionary<string, int>();

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

            File.WriteAllText(string.Format("{0}/conv_{1:000}.txt", OutputDirectory, liSlot),
                UWConversationDisassembler.Disassemble(lOConversation, lOData.Strings));
        }

        lOOut.AppendLine(string.Format("{0} slots in CNV.ARK, {1} of them used, {2} words of code in total",
            lOConversations.SlotCount, liUsed, liCodeWords));

        lOOut.AppendLine();
        lOOut.AppendLine("--- built-in functions, by frequency ---");

        System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>> lOSorted =
            new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(lOFunctionUse);
        lOSorted.Sort((a, b) => b.Value.CompareTo(a.Value));

        foreach (var lOPair in lOSorted)
            lOOut.AppendLine(string.Format("  {0,-28} in {1} conversations", lOPair.Key, lOPair.Value));

        // One conversation in detail, as a sample.
        for (int liSlot = 1; liSlot < lOConversations.SlotCount; liSlot++)
        {
            UWConversations.Conversation lOSample = lOConversations.GetConversation(liSlot);

            if (lOSample == null || lOSample.Code.Length < 40)
                continue;

            lOOut.AppendLine();
            lOOut.AppendLine(string.Format("--- Sample: slot {0}, name '{1}', string block 0x{2:X4} ---",
                liSlot, fPartnerName(lOData, liSlot), lOSample.StringBlock));

            string lsCode = UWConversationDisassembler.Disassemble(lOSample, lOData.Strings);
            string[] lsLines = lsCode.Split('\n');

            for (int liLine = 0; liLine < lsLines.Length && liLine < 40; liLine++)
                lOOut.AppendLine("  " + lsLines[liLine].TrimEnd('\r'));

            break;
        }

        lOOut.AppendLine();
        lOOut.AppendLine("Full disassembly in " + OutputDirectory);

        Debug.Log("UWCNV\n" + lOOut.ToString());
        AssetDatabase.Refresh();
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
}
