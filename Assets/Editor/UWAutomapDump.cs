using System.Text;
using UnityEditor;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Writes the automap of the configured savegame as an image and counts which values
/// occur in it.
///
/// Test of the interpretation from uw-formats.txt 4.7: low nibble tile type, high nibble the
/// display kind. For comparison with a screenshot the user took in the original
/// (2026-09-03).
/// </summary>
public static class UWAutomapDump
{
    private const int TilesPerAxis = 64;

    private const int Scale = 6;

    [MenuItem("Underworld Revisited/Diagnostics/Write automap")]
    public static void Dump()
    {
        UnderworldRevisited.UWSettings lOSettings = AssetDatabase.LoadAssetAtPath<UnderworldRevisited.UWSettings>("Assets/Resources/UWSettings.asset");
        DataImport lOData = new DataImport(lOSettings.DataPath, lOSettings.DiagnosticSavegamePath);

        StringBuilder lOOut = new StringBuilder();

        lOOut.AppendLine("Savegame: " + lOSettings.DiagnosticSavegamePath);

        UWLevel lOLevel = lOData.Levels[0];

        if (lOLevel.AutomapTiles == null)
        {
            Debug.LogError("UWAUTOMAP: no automap in this LEV.ARK");
            return;
        }

        int[] liLowCount = new int[16];
        int[] liHighCount = new int[16];

        foreach (byte lyTile in lOLevel.AutomapTiles)
        {
            liLowCount[lyTile & 0xF]++;
            liHighCount[(lyTile >> 4) & 0xF]++;
        }

        lOOut.AppendLine();
        lOOut.AppendLine("Low nibble (tile type):");

        for (int liAt = 0; liAt < 16; liAt++)
        {
            if (liLowCount[liAt] > 0)
                lOOut.AppendLine(string.Format("  {0,2} = {1}", liAt, liLowCount[liAt]));
        }

        lOOut.AppendLine();
        lOOut.AppendLine("High nibble (display):");

        for (int liAt = 0; liAt < 16; liAt++)
        {
            if (liHighCount[liAt] > 0)
                lOOut.AppendLine(string.Format("  {0,2} = {1}", liAt, liHighCount[liAt]));
        }

        Debug.Log("UWAUTOMAP" + System.Environment.NewLine + lOOut.ToString());

        fWriteImage(lOLevel);
    }

    private static void fWriteImage(UWLevel pOLevel)
    {
        int liSize = TilesPerAxis * Scale;

        Color32[] lOPixels = new Color32[liSize * liSize];

        for (int liTileY = 0; liTileY < TilesPerAxis; liTileY++)
        {
            for (int liTileX = 0; liTileX < TilesPerAxis; liTileX++)
            {
                byte lyTile = pOLevel.AutomapTiles[(liTileY * TilesPerAxis) + liTileX];

                Color32 lOColour = fGetColour(lyTile);

                for (int liY = 0; liY < Scale; liY++)
                {
                    for (int liX = 0; liX < Scale; liX++)
                    {
                        lOPixels[(((liTileY * Scale) + liY) * liSize) + (liTileX * Scale) + liX] = lOColour;
                    }
                }
            }
        }

        Texture2D lOTexture = new Texture2D(liSize, liSize, TextureFormat.RGBA32, false);
        lOTexture.SetPixels32(lOPixels);
        lOTexture.Apply(false, false);

        System.IO.File.WriteAllBytes("Assets/UWExportedStrings/Automap.png", lOTexture.EncodeToPNG());

        Object.DestroyImmediate(lOTexture);

        AssetDatabase.Refresh();
    }

    /// <summary>Colours for comparison only, not those of the original.
    ///
    /// The display values in UW1 are DIFFERENT from those described in uw-formats.txt - that
    /// lists the Underworld 2 version. In UW1: 1 water, 2 lava, 4 door, 9 and 10
    /// bridge, 12 stairs (per the reference, automaptileinfo).</summary>
    private static Color32 fGetColour(byte pyTile)
    {
        int liType = pyTile & 0xF;
        int liDisplay = (pyTile >> 4) & 0xF;

        if (liType > 9)
            return new Color32(0, 0, 0, 255);

        if (liDisplay == 1)
            return new Color32(70, 90, 220, 255);

        if (liDisplay == 2)
            return new Color32(220, 90, 40, 255);

        if (liDisplay == 4)
            return new Color32(200, 60, 60, 255);

        if (liDisplay == 12)
            return new Color32(80, 220, 120, 255);

        if (liDisplay == 9 || liDisplay == 10)
            return new Color32(180, 140, 60, 255);

        return liType == 0
            ? new Color32(60, 50, 45, 255)
            : new Color32(200, 175, 140, 255);
    }
}
