using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UWDataImport;
using UnderworldRevisited;

/// <summary>
/// Writes ALL original strings (STRINGS.PAK, all blocks) into the project as a searchable
/// text file.
///
/// Stays in the project (not a throwaway dump): the exact original wording is needed
/// constantly in this project - messages like "The door is locked", "The key does not fit",
/// "Using the pole you trigger the switch" or "The cauldron is empty." used to be copied
/// down one by one while playing. Reading everything at once is faster and more
/// complete than provoking every message in the original - and the neighbouring lines
/// often reveal the remaining cases of the same mechanism right away.
///
/// Format: one line per string as "[Block:Index] Text", line breaks inside the string itself
/// are escaped as \n so every line stays greppable on its own.
///
/// Known blocks (see DataImport): 1 = general game messages, 3 = scroll/
/// book texts, 4 = object names (index = object ID + 1), 5 = object state descriptions,
/// 10 = wall/floor texture names, from 0x0E00 = conversations (one block per NPC).
/// </summary>
public static class UWStringExporter
{
    private const string OutDir = "Assets/UWExportedStrings";
    private const string OutFile = OutDir + "/AllStrings.txt";

    [MenuItem("UW/Export All Strings")]
    public static void Export()
    {
        string lsDataPath = UWSettings.Instance != null ? UWSettings.Instance.DataPath : null;

        if (!UWSettings.IsValidDataPath(lsDataPath))
            lsDataPath = UWSettings.AutoDetectDataPath();

        DataImport lOData = new DataImport(lsDataPath);

        List<int> lyBlockNumbers = new List<int>(lOData.Strings.Blocks.Keys);
        lyBlockNumbers.Sort();

        StringBuilder lOOut = new StringBuilder();
        int liTotal = 0;

        // Overview first - this shows without scrolling which blocks exist and
        // how large they are.
        lOOut.AppendLine("### Overview ###");

        foreach (int liBlock in lyBlockNumbers)
        {
            int liCount = lOData.Strings.Blocks[liBlock].Strings.Count;
            liTotal += liCount;
            lOOut.AppendLine($"Block {liBlock} (0x{liBlock:X4}): {liCount} Strings");
        }

        lOOut.AppendLine();
        lOOut.AppendLine($"### {lyBlockNumbers.Count} blocks, {liTotal} strings in total ###");
        lOOut.AppendLine();

        foreach (int liBlock in lyBlockNumbers)
        {
            List<string> lyStrings = lOData.Strings.Blocks[liBlock].Strings;

            lOOut.AppendLine();
            lOOut.AppendLine($"### Block {liBlock} (0x{liBlock:X4}) - {lyStrings.Count} Strings ###");

            for (int i = 0; i < lyStrings.Count; i++)
            {
                string lsLine = lyStrings[i] ?? string.Empty;
                lsLine = lsLine.Replace("\r", string.Empty).Replace("\n", "\\n");

                lOOut.AppendLine($"[{liBlock}:{i}] {lsLine}");
            }
        }

        Directory.CreateDirectory(OutDir);
        File.WriteAllText(OutFile, lOOut.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();

        Debug.Log($"{liTotal} strings from {lyBlockNumbers.Count} blocks exported to {OutFile}");
    }
}
