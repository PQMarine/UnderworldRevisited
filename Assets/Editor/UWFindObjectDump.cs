using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Searches all levels for specific object ids and names the tile and container.
///
/// Unlike UWMapRender it also looks inside containers - a bottle rarely lies around
/// in the open, but in a chest or a barrel. For reproducing tests in the
/// original: wine vs beer bottle for the water test.
/// </summary>
public static class UWFindObjectDump
{
    private const int TilesPerAxis = 64;

    /// <summary>What to search for. Enter other things here to search for them.</summary>
    private static readonly int[] msSearchIds = new int[] { 10, 54, 55, 147, 151, 174, 191, 287, 310 };

    [MenuItem("Underworld Revisited/Diagnostics/Find objects")]
    public static void Dump()
    {
        UnderworldRevisited.UWSettings lOSettings = AssetDatabase.LoadAssetAtPath<UnderworldRevisited.UWSettings>("Assets/Resources/UWSettings.asset");
        DataImport lOData = new DataImport(lOSettings.DataPath);

        StringBuilder lOOut = new StringBuilder();

        foreach (int liSearchId in msSearchIds)
        {
            lOOut.AppendLine(string.Format("--- {0} (0x{0:X3}): {1}", liSearchId, fSafe(lOData, liSearchId + 1)));

            int liFound = 0;

            for (int liLevel = 0; liLevel < lOData.Levels.Count; liLevel++)
            {
                UWLevel lOLevel = lOData.Levels[liLevel];

                for (int liTileY = 0; liTileY < TilesPerAxis; liTileY++)
                {
                    for (int liTileX = 0; liTileX < TilesPerAxis; liTileX++)
                    {
                        UWTile lOTile = lOLevel.TileData[(liTileY * TilesPerAxis) + liTileX];

                        foreach (UWObject lOObject in lOTile.ObjectsInTile)
                            liFound += fSearch(lOData, lOLevel, lOObject, liSearchId, liLevel + 1, liTileX, liTileY, "", lOOut);
                    }
                }
            }

            if (liFound == 0)
                lOOut.AppendLine("  nothing found");

            lOOut.AppendLine();
        }

        Debug.Log("UWFIND\n" + lOOut.ToString());
    }

    private static int fSearch(DataImport pOData, UWLevel pOLevel, UWObject pOObject, int piSearchId,
        int piLevel, int piTileX, int piTileY, string psPath, StringBuilder pOOut)
    {
        if (pOObject == null)
            return 0;

        int liFound = 0;

        if (pOObject.ID == piSearchId)
        {
            pOOut.AppendLine(string.Format("  Level {0} tile {1}/{2}{3}",
                piLevel, piTileX, piTileY, psPath));

            liFound++;
        }

        List<UWObject> lOContents = fGetContents(pOObject, pOLevel);

        if (lOContents == null)
            return liFound;

        string lsPath = psPath + " in " + fSafe(pOData, pOObject.ID + 1);

        foreach (UWObject lOChild in lOContents)
            liFound += fSearch(pOData, pOLevel, lOChild, piSearchId, piLevel, piTileX, piTileY, lsPath, pOOut);

        return liFound;
    }

    private static List<UWObject> fGetContents(UWObject pOObject, UWLevel pOLevel)
    {
        try
        {
            pOObject.EnsureContentsLoaded(pOLevel.Masterlist);

            return pOObject.Contents;
        }
        catch
        {
            return null;
        }
    }

    private static string fSafe(DataImport pOData, int piIndex)
    {
        try
        {
            return pOData.Strings.Blocks[4].Strings[piIndex].Trim();
        }
        catch
        {
            return "?";
        }
    }
}
