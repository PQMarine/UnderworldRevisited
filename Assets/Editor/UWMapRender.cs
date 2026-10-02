using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Renders a top view of every level as PNG and highlights searched objects in colour.
/// Meant for finding items again in the original.
///
/// Orientation: tile 0/0 is at the bottom left, X to the right, Y upwards - as in the
/// tile numbering of the dumps. A grid line runs every eight tiles, so that
/// coordinates can be counted off.
/// </summary>
public static class UWMapRender
{
    private const int PixelsPerTile = 8;

    private const int TilesPerAxis = 64;

    /// <summary>a_teleport trap - in the original the transition between levels.</summary>
    private const int TeleportTrapId = 0x0181;

    private const string OutputDirectory = "Assets/UWExportedStrings/Maps";

    /// <summary>Objects that are highlighted. To search for a particular thing, enter it
    /// here - currently only the battle axe.</summary>
    private static readonly int[] msHighlightIds = new int[] { 1 };

    private static readonly Color32 msSolid = new Color32(18, 18, 22, 255);
    private static readonly Color32 msDoor = new Color32(80, 140, 255, 255);
    private static readonly Color32 msTeleport = new Color32(60, 220, 120, 255);
    private static readonly Color32 msHighlight = new Color32(255, 60, 40, 255);
    private static readonly Color32 msGrid = new Color32(0, 0, 0, 60);

    [MenuItem("Underworld Revisited/Diagnostics/Render maps")]
    public static void Render()
    {
        UnderworldRevisited.UWSettings lOSettings = AssetDatabase.LoadAssetAtPath<UnderworldRevisited.UWSettings>("Assets/Resources/UWSettings.asset");
        DataImport lOData = new DataImport(lOSettings.DataPath);

        Directory.CreateDirectory(OutputDirectory);

        System.Text.StringBuilder lOOut = new System.Text.StringBuilder();

        for (int liLevel = 0; liLevel < lOData.Levels.Count; liLevel++)
        {
            List<string> lOFound = new List<string>();

            Texture2D lOMap = fRenderLevel(lOData.Levels[liLevel], lOFound);

            string lsPath = string.Format("{0}/Level{1:00}.png", OutputDirectory, liLevel + 1);

            File.WriteAllBytes(lsPath, lOMap.EncodeToPNG());

            Object.DestroyImmediate(lOMap);

            lOOut.AppendLine(string.Format("Level {0}: {1}", liLevel + 1,
                lOFound.Count == 0 ? "nothing highlighted" : string.Join(", ", lOFound)));
        }

        AssetDatabase.Refresh();

        Debug.Log("UWMAP\n" + lOOut.ToString());
    }

    private static Texture2D fRenderLevel(UWLevel pOLevel, List<string> pOFound)
    {
        int liSize = TilesPerAxis * PixelsPerTile;

        Texture2D lOMap = new Texture2D(liSize, liSize, TextureFormat.RGBA32, false);
        Color32[] lOPixels = new Color32[liSize * liSize];

        for (int liTileY = 0; liTileY < TilesPerAxis; liTileY++)
        {
            for (int liTileX = 0; liTileX < TilesPerAxis; liTileX++)
            {
                UWTile lOTile = pOLevel.TileData[(liTileY * TilesPerAxis) + liTileX];

                Color32 lOColour = fGetTileColour(lOTile);

                foreach (UWObject lOObject in lOTile.ObjectsInTile)
                {
                    if (lOObject == null)
                        continue;

                    if (lOObject.GetCategory() == UWObject.ObjectCategoryEnum.Doors)
                        lOColour = msDoor;

                    if (lOObject.ID == TeleportTrapId)
                        lOColour = msTeleport;
                }

                foreach (UWObject lOObject in lOTile.ObjectsInTile)
                {
                    if (lOObject == null || System.Array.IndexOf(msHighlightIds, (int)lOObject.ID) < 0)
                        continue;

                    lOColour = msHighlight;

                    pOFound.Add(string.Format("{0} at {1}/{2}", lOObject.ID, liTileX, liTileY));
                }

                fFillTile(lOPixels, liSize, liTileX, liTileY, lOColour);
            }
        }

        fDrawGrid(lOPixels, liSize);

        lOMap.SetPixels32(lOPixels);
        lOMap.Apply(false, false);

        return lOMap;
    }

    /// <summary>Walkable light, solid dark; brightness follows floor height, so that
    /// different heights within the level can be told apart.</summary>
    private static Color32 fGetTileColour(UWTile pOTile)
    {
        if (pOTile.TileType == UWTile.TileTypeEnum.solid)
            return msSolid;

        float lfHeight = Mathf.Clamp01(pOTile.FloorHeight / 192f);

        byte lyShade = (byte)(90 + (lfHeight * 130f));

        // Colour diagonals and slopes slightly differently, so the shape stays recognisable.
        bool lbSpecial = pOTile.TileType != UWTile.TileTypeEnum.open;

        return lbSpecial
            ? new Color32((byte)(lyShade * 0.8f), lyShade, (byte)(lyShade * 0.7f), 255)
            : new Color32(lyShade, lyShade, (byte)(lyShade * 0.92f), 255);
    }

    private static void fFillTile(Color32[] pOPixels, int piSize, int piTileX, int piTileY, Color32 pOColour)
    {
        // Unity texture rows start at the bottom left, so tile 0/0 lands at the bottom left
        // without flipping.
        int liBaseX = piTileX * PixelsPerTile;
        int liBaseY = piTileY * PixelsPerTile;

        for (int y = 0; y < PixelsPerTile; y++)
        {
            for (int x = 0; x < PixelsPerTile; x++)
                pOPixels[((liBaseY + y) * piSize) + liBaseX + x] = pOColour;
        }
    }

    private static void fDrawGrid(Color32[] pOPixels, int piSize)
    {
        for (int liTile = 0; liTile <= TilesPerAxis; liTile += 8)
        {
            int liPixel = Mathf.Min(piSize - 1, liTile * PixelsPerTile);

            for (int i = 0; i < piSize; i++)
            {
                pOPixels[(i * piSize) + liPixel] = fBlend(pOPixels[(i * piSize) + liPixel], msGrid);
                pOPixels[(liPixel * piSize) + i] = fBlend(pOPixels[(liPixel * piSize) + i], msGrid);
            }
        }
    }

    private static Color32 fBlend(Color32 pOBase, Color32 pOOver)
    {
        float lfAlpha = pOOver.a / 255f;

        return new Color32(
            (byte)((pOBase.r * (1f - lfAlpha)) + (pOOver.r * lfAlpha)),
            (byte)((pOBase.g * (1f - lfAlpha)) + (pOOver.g * lfAlpha)),
            (byte)((pOBase.b * (1f - lfAlpha)) + (pOOver.b * lfAlpha)),
            255);
    }
}
