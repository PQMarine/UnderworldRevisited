using System.Text;
using UnityEditor;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Dumps tile type, heights, textures and objects of a tile together with the tiles around it
/// (two tiles to each side, three towards higher Y; the centre tile is marked "->"). Meant for
/// places where the built geometry does not match the original.
/// </summary>
public static class UWTileGeometryDump
{
    private const int LevelIndex = 0;

    private const int TileX = 13;

    private const int TileY = 26;

    [MenuItem("Underworld Revisited/Diagnostics/Dump tile geometry")]
    public static void Dump()
    {
        UnderworldRevisited.UWSettings lOSettings = AssetDatabase.LoadAssetAtPath<UnderworldRevisited.UWSettings>("Assets/Resources/UWSettings.asset");
        DataImport lOData = new DataImport(lOSettings.DataPath);
        UWLevel lOLevel = lOData.Levels[LevelIndex];
        StringBuilder lOOut = new StringBuilder();

        lOOut.AppendLine("Level " + (LevelIndex + 1));

        for (int liY = TileY - 2; liY <= TileY + 3; liY++)
        {
            for (int liX = TileX - 2; liX <= TileX + 2; liX++)
                fDescribe(lOOut, lOLevel, lOData, liX, liY, liX == TileX && liY == TileY);
        }

        Debug.Log("UWTILEGEO\n" + lOOut.ToString());
    }

    private static void fDescribe(StringBuilder pOOut, UWLevel pOLevel, DataImport pOData, int piX, int piY, bool pbCentre)
    {
        if (piX < 0 || piY < 0 || piX > 63 || piY > 63)
            return;

        UWTile lOTile = pOLevel.TileData[(piY * 64) + piX];

        pOOut.AppendLine(string.Format("{0} {1}/{2}: Type={3} Floor={4} Ceiling={5} Slope={6} WallTex={7} FloorTex={8} CeilingTex={9} Objects={10}",
            pbCentre ? "->" : "  ", piX, piY, lOTile.TileType, lOTile.FloorHeight, lOTile.CeilingHeight,
            lOTile.Slope, lOTile.TextureWall, lOTile.TextureFloor, lOTile.TextureCeiling,
            lOTile.ObjectsInTile.Count));


        foreach (UWObject lOObject in lOTile.ObjectsInTile)
        {
            if (lOObject == null)
                continue;

            pOOut.AppendLine(string.Format("       Object {0} \"{1}\" Cat={2} XY={3}/{4} Z={5} Flags={6} Quality={7} Owner={8}",
                lOObject.ID, fSafe(pOData, lOObject.ID + 1), lOObject.GetCategory(),
                lOObject.XPos, lOObject.YPos, lOObject.ZPos, lOObject.Flags, lOObject.Quality, lOObject.Owner));
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
