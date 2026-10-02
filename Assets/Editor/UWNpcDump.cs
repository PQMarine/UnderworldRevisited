using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// Lists all creatures of a level with their behaviour fields. Groundwork for
/// NPC behaviour: first see what actually occurs in the data.
///
/// uw-formats.txt gives only three values for npc_goal (5 and 9 attack, 6 flee) and for
/// npc_attitude four (0 hostile, 1 upset, 2 mellow, 3 friendly) - so the distribution
/// in the real data has to show what else there is.
///
/// Besides the creatures of level 1, the dump also lists all bridges on levels 1 to 4, the
/// goal distribution over all levels (with how many of them have a conversation slot), and the doors of level 1
/// with their sub-tile positions.
/// </summary>
public static class UWNpcDump
{
    private const int LevelIndex = 0;

    private const int BridgeObjectId = 356;

    [MenuItem("Underworld Revisited/Diagnostics/Dump NPCs")]
    public static void Dump()
    {
        UnderworldRevisited.UWSettings lOSettings = AssetDatabase.LoadAssetAtPath<UnderworldRevisited.UWSettings>("Assets/Resources/UWSettings.asset");
        DataImport lOData = new DataImport(lOSettings.DataPath);
        UWLevel lOLevel = lOData.Levels[LevelIndex];
        StringBuilder lOOut = new StringBuilder();

        Dictionary<int, int> lOGoals = new Dictionary<int, int>();
        Dictionary<int, int> lOAttitudes = new Dictionary<int, int>();

        int liCount = 0;

        for (int liLevel = 0; liLevel < 4; liLevel++)
        {
            UWLevel lOOther = lOData.Levels[liLevel];

            for (int liTile = 0; liTile < lOOther.TileData.Length; liTile++)
            {
                foreach (UWObject lOObject in lOOther.TileData[liTile].ObjectsInTile)
                {
                    if (lOObject == null || lOObject.ID != BridgeObjectId)
                        continue;

                    UWTile lOT = lOOther.TileData[liTile];

                    lOOut.AppendLine(string.Format("BRIDGE level {0} tile {1}/{2} type={3} floor={4} Z={5} cat={6}",
                        liLevel + 1, liTile % 64, liTile / 64, lOT.TileType, lOT.FloorHeight,
                        lOObject.ZPos, lOObject.GetCategory()));
                }
            }
        }

        for (int liTile = 0; liTile < lOLevel.TileData.Length; liTile++)
        {
            foreach (UWObject lOObject in lOLevel.TileData[liTile].ObjectsInTile)
            {
                UWNpc lONpc = lOObject as UWNpc;

                if (lONpc == null)
                    continue;

                liCount++;

                fCount(lOGoals, lONpc.NPCGoal);
                fCount(lOAttitudes, lONpc.NPCAttitude);

                lOOut.AppendLine(string.Format(
                    "{0,2}/{1,-2} Id={2,-3} \"{3}\" Goal={4} GoalObj={5} Attitude={6} Level={7} HP={8} Home={9}/{10} Heading={11} Hunger={12} WhoAmI={13} TalkedTo={14}",
                    liTile % 64, liTile / 64, lONpc.ID, fSafe(lOData, lONpc.ID + 1),
                    lONpc.NPCGoal, lONpc.NPCGTarg, lONpc.NPCAttitude, lONpc.NPCLevel, lONpc.NPC_HP,
                    lONpc.NPCXHome, lONpc.NPCYHome, lONpc.NPCHeading, lONpc.NPCHunger,
                    lONpc.NPCwhoami, lONpc.NPCTalkedTo));
            }
        }

        lOOut.AppendLine("--- " + liCount + " creatures on level " + (LevelIndex + 1));
        lOOut.AppendLine("Goals:     " + fFormat(lOGoals));
        lOOut.AppendLine("Attitude:  " + fFormat(lOAttitudes));

        lOOut.AppendLine("--- Goal versus creature type and conversation slot, all levels");

        Dictionary<int, int> lOWithSlot = new Dictionary<int, int>();
        Dictionary<int, int> lOTotal = new Dictionary<int, int>();
        Dictionary<int, string> lOExamples = new Dictionary<int, string>();

        for (int liLevel = 0; liLevel < lOData.Levels.Count; liLevel++)
        {
            UWLevel lOAny = lOData.Levels[liLevel];

            for (int liTile = 0; liTile < lOAny.TileData.Length; liTile++)
            {
                foreach (UWObject lOObject in lOAny.TileData[liTile].ObjectsInTile)
                {
                    UWNpc lOOne = lOObject as UWNpc;

                    if (lOOne == null)
                        continue;

                    fCount(lOTotal, lOOne.NPCGoal);

                    if (lOOne.NPCwhoami != 0)
                        fCount(lOWithSlot, lOOne.NPCGoal);

                    string lsName = fSafe(lOData, lOOne.ID + 1);

                    if (!lOExamples.ContainsKey(lOOne.NPCGoal))
                        lOExamples[lOOne.NPCGoal] = string.Empty;

                    if (!lOExamples[lOOne.NPCGoal].Contains(lsName) && lOExamples[lOOne.NPCGoal].Length < 90)
                        lOExamples[lOOne.NPCGoal] += lsName + ", ";
                }
            }
        }

        List<int> lOGoalList = new List<int>(lOTotal.Keys);
        lOGoalList.Sort();

        foreach (int liGoal in lOGoalList)
        {
            int liSlots;
            lOWithSlot.TryGetValue(liGoal, out liSlots);

            lOOut.AppendLine(string.Format("  Goal {0,-3}: {1,4} creatures, {2,3} of them with conversation slot   {3}",
                liGoal, lOTotal[liGoal], liSlots, lOExamples[liGoal]));
        }

        lOOut.AppendLine("--- Doors on level 1: id, sub-tile position, heading");

        Dictionary<string, int> lOPositions = new Dictionary<string, int>();

        for (int liTile = 0; liTile < lOLevel.TileData.Length; liTile++)
        {
            foreach (UWObject lOObject in lOLevel.TileData[liTile].ObjectsInTile)
            {
                if (lOObject == null || lOObject.GetCategory() != UWObject.ObjectCategoryEnum.Doors)
                    continue;

                lOOut.AppendLine(string.Format("  {0,2}/{1,-2} Id={2,-3} \"{3}\" XY={4}/{5} Heading={6} Flags={7}",
                    liTile % 64, liTile / 64, lOObject.ID, fSafe(lOData, lOObject.ID + 1),
                    lOObject.XPos, lOObject.YPos, lOObject.Heading, lOObject.Flags));

                string lsKey = lOObject.XPos + "/" + lOObject.YPos;

                fCount2(lOPositions, lsKey);
            }
        }

        lOOut.AppendLine("  Frequency of sub-tile positions:");

        foreach (KeyValuePair<string, int> lOPair in lOPositions)
            lOOut.AppendLine("    " + lOPair.Key + ": " + lOPair.Value);

        Debug.Log("UWNPC\n" + lOOut.ToString());
    }

    private static void fCount2(Dictionary<string, int> pOCounts, string psValue)
    {
        int liExisting;

        pOCounts.TryGetValue(psValue, out liExisting);
        pOCounts[psValue] = liExisting + 1;
    }

    private static void fCount(Dictionary<int, int> pOCounts, int piValue)
    {
        int liExisting;

        pOCounts.TryGetValue(piValue, out liExisting);
        pOCounts[piValue] = liExisting + 1;
    }

    private static string fFormat(Dictionary<int, int> pOCounts)
    {
        StringBuilder lOOut = new StringBuilder();

        List<int> lOKeys = new List<int>(pOCounts.Keys);
        lOKeys.Sort();

        foreach (int liKey in lOKeys)
            lOOut.Append(liKey + ":" + pOCounts[liKey] + "  ");

        return lOOut.ToString();
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
