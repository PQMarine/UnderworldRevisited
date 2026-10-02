using System.Text;
using UnityEditor;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Lists all objects in a square area around a tile. For the question
/// "what is actually there" when something is visible in the game that the tile itself
/// does not explain.
/// </summary>
public static class UWAreaDump
{
    private const int LevelIndex = 3;

    private const int CentreX = 35;

    private const int CentreY = 51;

    private const int Radius = 4;

    [MenuItem("Underworld Revisited/Diagnostics/Dump surroundings")]
    public static void Dump()
    {
        UnderworldRevisited.UWSettings lOSettings = AssetDatabase.LoadAssetAtPath<UnderworldRevisited.UWSettings>("Assets/Resources/UWSettings.asset");
        DataImport lOData = new DataImport(lOSettings.DataPath);
        UWLevel lOLevel = lOData.Levels[LevelIndex];
        StringBuilder lOOut = new StringBuilder();

        for (int liY = CentreY - Radius; liY <= CentreY + Radius; liY++)
        {
            for (int liX = CentreX - Radius; liX <= CentreX + Radius; liX++)
            {
                if (liX < 0 || liY < 0 || liX > 63 || liY > 63)
                    continue;

                UWTile lOTile = lOLevel.TileData[(liY * 64) + liX];

                if (lOTile.ObjectsInTile.Count == 0)
                    continue;

                foreach (UWObject lOObject in lOTile.ObjectsInTile)
                {
                    if (lOObject == null)
                        continue;

                    lOOut.AppendLine(string.Format("{0}/{1}: Id={2} \"{3}\" Cat={4} Texture={5} Z={6}",
                        liX, liY, lOObject.ID, fSafe(lOData, lOObject.ID + 1), lOObject.GetCategory(),
                        lOObject.Texture == null ? "null" : lOObject.Texture.TextureType + ":" + lOObject.Texture.Index,
                        lOObject.ZPos));
                }
            }
        }

        Debug.Log("UWAREA\n" + lOOut.ToString());
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
