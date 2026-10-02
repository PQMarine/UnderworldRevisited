using System.Text;
using UnityEditor;
using UnityEngine;
using UWDataImport;

/// <summary>
/// Searches all string blocks for a text and reports block and number.
///
/// Meant for cases where a name shows up in the game that we cannot map to the data,
/// such as the "Sword of Justice", whose object record in the save game carries no
/// enchantment.
/// </summary>
public static class UWStringSearch
{
    /// <summary>What to search for. Case-insensitive.</summary>
    private const string SearchTerm = "experience level";

    /// <summary>Instead of searching, dump a range of one block. -1 switches this off.</summary>
    // static readonly instead of const: as a constant the compiler considers the branch below
    // unreachable and warns. The switch stays the same.
    private static readonly int DumpBlock = -1;

    private const int DumpFirst = 255;

    private const int DumpLast = 290;

    [MenuItem("Underworld Revisited/Diagnostics/Search Text")]
    public static void Search()
    {
        UnderworldRevisited.UWSettings lOSettings = AssetDatabase.LoadAssetAtPath<UnderworldRevisited.UWSettings>("Assets/Resources/UWSettings.asset");
        DataImport lOData = new DataImport(lOSettings.DataPath);

        StringBuilder lOOut = new StringBuilder();

        if (DumpBlock >= 0)
        {
            for (int liString = DumpFirst; liString <= DumpLast; liString++)
            {
                try
                {
                    lOOut.AppendLine(string.Format("  {0,3}: {1}", liString,
                        lOData.Strings.Blocks[DumpBlock].Strings[liString].Trim()));
                }
                catch
                {
                }
            }

            Debug.Log("UWSTRINGS " + lOOut.ToString());

            return;
        }

        int liHits = 0;

        foreach (System.Collections.Generic.KeyValuePair<int, UWDataImport.UWData.UWStringBlock> lOBlock in lOData.Strings.Blocks)
        {
            for (int liString = 0; liString < lOBlock.Value.Strings.Count; liString++)
            {
                string lsText = lOBlock.Value.Strings[liString];

                if (string.IsNullOrEmpty(lsText))
                    continue;

                if (lsText.IndexOf(SearchTerm, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                liHits++;

                lOOut.AppendLine(string.Format("Block {0} (0x{0:X4}) Number {1}: {2}",
                    lOBlock.Key, liString, lsText.Trim()));

                if (liHits >= 40)
                {
                    lOOut.AppendLine("  ... stopped at 40 hits");

                    Debug.Log("UWSTRINGS\n" + lOOut.ToString());

                    return;
                }
            }
        }

        if (liHits == 0)
            lOOut.AppendLine("Nothing found.");

        Debug.Log("UWSTRINGS\n" + lOOut.ToString());
    }
}
