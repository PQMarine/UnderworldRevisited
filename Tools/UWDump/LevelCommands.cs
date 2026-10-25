using System;
using System.Collections.Generic;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>The level commands: overview, tile map, single tiles, object types, triggers,
    /// where things lie, and the creatures.</summary>
    public static partial class Program
    {
        private static int fPrintInfo(DumpContext pOContext, List<string> pOArgs)
        {
            DataImport lOData = pOContext.Data;

            Console.WriteLine("Data folder : " + pOContext.DataPath);

            if (pOContext.SavePath != null)
                Console.WriteLine("Save game   : " + pOContext.SavePath);

            Console.WriteLine("Levels      : " + (lOData.Levels == null ? 0 : lOData.Levels.Count));
            Console.WriteLine("String blocks: " + (lOData.Strings == null ? 0 : lOData.Strings.Blocks.Count));

            if (lOData.Levels == null)
                return 0;

            foreach (UWLevel lOLevel in lOData.Levels)
            {
                if (lOLevel == null || lOLevel.TileData == null)
                    continue;

                int liObjects = 0;
                int liSolid = 0;

                foreach (UWTile lOTile in lOLevel.TileData)
                {
                    if (lOTile == null)
                        continue;

                    if (lOTile.TileType == UWTile.TileTypeEnum.solid)
                        liSolid++;

                    liObjects += lOTile.ObjectsInTile == null ? 0 : lOTile.ObjectsInTile.Count;
                }

                Console.WriteLine(string.Format("  Level {0}: {1,4} objects in tiles, {2,4} solid tiles, {3,3} map notes",
                    lOLevel.LevelNumber, liObjects, liSolid,
                    lOLevel.MapNotes == null ? 0 : lOLevel.MapNotes.Count));
            }

            return 0;
        }

        /// <summary>Every a_move trigger of the nine levels: tile, the flags of word 0 bits 9-12
        /// (bit 1 = 0x400 repeatable, bit 2 = 0x800 the player sets it off, bit 3 = 0x1000 creatures
        /// and things do, see Trigger_ovr153_3B) and the chain of traps behind it.</summary>
        private static int fPrintMoveTriggers(DumpContext pOContext, List<string> pOArgs)
        {
            DataImport lOData = pOContext.Data;

            if (lOData.Levels == null)
                return 4;

            foreach (UWLevel lOLevel in lOData.Levels)
            {
                if (lOLevel == null || lOLevel.TileData == null)
                    continue;

                for (int liAt = 0; liAt < lOLevel.TileData.Length; liAt++)
                {
                    UWTile lOTile = lOLevel.TileData[liAt];

                    if (lOTile == null || lOTile.ObjectsInTile == null)
                        continue;

                    foreach (UWObject lOObject in lOTile.ObjectsInTile)
                    {
                        if (lOObject == null || lOObject.ID != UWObjectMechanics.MoveTriggerId)
                            continue;

                        StringBuilder lOChain = new StringBuilder();
                        UWObject lOLink = UWObjectMechanics.GetLinkedObject(lOObject, lOLevel.Masterlist);

                        for (int liDepth = 0; lOLink != null && liDepth < 6; liDepth++)
                        {
                            lOChain.Append(string.Format(" -> {0:X3} {1} q{2}", lOLink.ID,
                                fOneLine(lOData.GetObjectDescription(lOLink.ID + 1)), lOLink.Quality));

                            if (UWObjectIds.Major(lOLink.ID) != UWObjectMechanics.TrapMajorClass)
                                break;

                            lOLink = UWObjectMechanics.GetLinkedObject(lOLink, lOLevel.Masterlist);
                        }

                        Console.WriteLine(string.Format("L{0} {1,2}/{2,2} flags {3} ({4}{5}{6}){7}", lOLevel.LevelNumber,
                            liAt % 64, liAt / 64, lOObject.Flags,
                            (lOObject.Flags & 2) != 0 ? "repeat " : "once ",
                            (lOObject.Flags & 4) != 0 ? "player " : "",
                            (lOObject.Flags & 8) != 0 ? "creatures" : "", lOChain));
                    }
                }
            }

            return 0;
        }

        /// <summary>Tile map as in the save-editing tool: '#' solid, '.' open, '/' diagonal,
        /// 's' sloped, the digit is the floor height. Then what stands in the tiles, with
        /// --detail every field the area dump of the editor showed (category, picture, height)
        /// and the object's position, flags, quality and owner.</summary>
        private static int fPrintLevel(DumpContext pOContext, List<string> pOArgs)
        {
            bool lbDetail = fTakeFlag(pOArgs, "--detail");

            if (pOArgs.Count < 1 || !fParse(pOArgs[0], out int liLevel))
            {
                Console.Error.WriteLine("Which level? For example: uwdump level <path> 1");

                return 1;
            }

            UWLevel lOLevel = fLevel(pOContext, liLevel);

            if (lOLevel == null)
                return 4;

            const int liTilesPerAxis = UWWorldScale.TilesPerAxis;

            int liCentreX = pOArgs.Count > 2 ? fArgument(pOArgs, 1, 32) : 32;
            int liCentreY = pOArgs.Count > 2 ? fArgument(pOArgs, 2, 32) : 32;
            int liRadius = fArgument(pOArgs, 3, 31);

            int liFromX = Math.Max(0, liCentreX - liRadius);
            int liToX = Math.Min(liTilesPerAxis - 1, liCentreX + liRadius);
            int liFromY = Math.Max(0, liCentreY - liRadius);
            int liToY = Math.Min(liTilesPerAxis - 1, liCentreY + liRadius);

            // North is up, so the higher Y comes first.
            for (int liTileY = liToY; liTileY >= liFromY; liTileY--)
            {
                StringBuilder lOLine = new StringBuilder();

                lOLine.Append(liTileY.ToString("D2")).Append(' ');

                for (int liTileX = liFromX; liTileX <= liToX; liTileX++)
                    lOLine.Append(fTileCharacter(lOLevel.GetTile(liTileX, liTileY)));

                Console.WriteLine(lOLine.ToString());
            }

            Console.WriteLine();

            for (int liTileY = liToY; liTileY >= liFromY; liTileY--)
            {
                for (int liTileX = liFromX; liTileX <= liToX; liTileX++)
                {
                    UWTile lOTile = lOLevel.GetTile(liTileX, liTileY);

                    if (lOTile == null || lOTile.ObjectsInTile == null || lOTile.ObjectsInTile.Count == 0)
                        continue;

                    foreach (UWObject lOObject in lOTile.ObjectsInTile)
                    {
                        if (lOObject == null)
                            continue;

                        Console.WriteLine(string.Format("{0,2}/{1,2}  {2,4}  {3}{4}",
                            liTileX, liTileY, lOObject.ID, fName(pOContext, lOObject.ID),
                            lbDetail ? "  " + fObjectDetail(lOObject) : string.Empty));
                    }
                }
            }

            return 0;
        }

        /// <summary>The fields of a placed object: category, picture, height in the tile,
        /// position in the tile, heading, flags, quality and owner.</summary>
        private static string fObjectDetail(UWObject pOObject)
        {
            return string.Format("Cat={0} Texture={1} Z={2} XY={3}/{4} Heading={5} Flags={6} Quality={7} Owner={8}",
                pOObject.GetCategory(),
                pOObject.Texture == null ? "null" : pOObject.Texture.TextureType + ":" + pOObject.Texture.Index,
                pOObject.ZPos, pOObject.XPos, pOObject.YPos, pOObject.Heading, pOObject.Flags,
                pOObject.Quality, pOObject.Owner);
        }

        private static char fTileCharacter(UWTile pOTile)
        {
            if (pOTile == null)
                return ' ';

            switch (pOTile.TileType)
            {
                case UWTile.TileTypeEnum.solid:
                    return '#';

                case UWTile.TileTypeEnum.diagonal_se:
                case UWTile.TileTypeEnum.diagonal_sw:
                case UWTile.TileTypeEnum.diagonal_ne:
                case UWTile.TileTypeEnum.diagonal_nw:
                    return '/';

                case UWTile.TileTypeEnum.open:
                    // The floor height counts in sixteenths of a tile height (UWTile.HeightLevel),
                    // so the level number is what is interesting, as a hex digit 0 to F.
                    return pOTile.FloorHeight == 0
                        ? '.'
                        : "0123456789abcdef"[Math.Min(15, pOTile.FloorHeight / (int)UWTile.HeightLevel)];

                default:
                    return 's';
            }
        }

        /// <summary>
        /// Tile type, heights, textures and objects of a tile and the tiles within the radius
        /// (the centre marked "->"), from the south-west corner row by row. For places where the
        /// built geometry does not match the original; the editor's tile geometry dump with the
        /// tile as an argument instead of a constant.
        /// </summary>
        private static int fPrintTile(DumpContext pOContext, List<string> pOArgs)
        {
            if (pOArgs.Count < 3 || !fParse(pOArgs[0], out int liLevel)
                || !fParse(pOArgs[1], out int liX) || !fParse(pOArgs[2], out int liY))
            {
                Console.Error.WriteLine("Which tile? For example: uwdump tile <path> 1 13 26 [radius]");

                return 1;
            }

            UWLevel lOLevel = fLevel(pOContext, liLevel);

            if (lOLevel == null)
                return 4;

            int liRadius = Math.Max(0, fArgument(pOArgs, 3, 0));

            Console.WriteLine("Level " + liLevel);

            for (int liTileY = liY - liRadius; liTileY <= liY + liRadius; liTileY++)
            {
                for (int liTileX = liX - liRadius; liTileX <= liX + liRadius; liTileX++)
                {
                    UWTile lOTile = lOLevel.GetTile(liTileX, liTileY);

                    if (lOTile == null)
                        continue;

                    Console.WriteLine(string.Format("{0} {1}/{2}: Type={3} Floor={4} Ceiling={5} Slope={6} WallTex={7} FloorTex={8} CeilingTex={9} Objects={10}",
                        liTileX == liX && liTileY == liY ? "->" : "  ", liTileX, liTileY, lOTile.TileType,
                        lOTile.FloorHeight, lOTile.CeilingHeight, lOTile.Slope, lOTile.TextureWall,
                        lOTile.TextureFloor, lOTile.TextureCeiling, lOTile.ObjectsInTile.Count));

                    foreach (UWObject lOObject in lOTile.ObjectsInTile)
                    {
                        if (lOObject == null)
                            continue;

                        Console.WriteLine(string.Format("       Object {0} \"{1}\" Cat={2} XY={3}/{4} Z={5} Flags={6} Quality={7} Owner={8}",
                            lOObject.ID, fName(pOContext, lOObject.ID), lOObject.GetCategory(),
                            lOObject.XPos, lOObject.YPos, lOObject.ZPos, lOObject.Flags, lOObject.Quality, lOObject.Owner));
                    }
                }
            }

            return 0;
        }

        private static int fPrintObject(DumpContext pOContext, List<string> pOArgs)
        {
            DataImport lOData = pOContext.Data;

            if (pOArgs.Count < 1 || !fParse(pOArgs[0], out int liId))
            {
                Console.Error.WriteLine("Which object? For example: uwdump object <path> 347");

                return 1;
            }

            Console.WriteLine(string.Format("{0} (0x{0:X3})  {1}", liId, fName(pOContext, liId)));

            if (lOData.CommonObjectProperties != null
                && lOData.CommonObjectProperties.TryGet(liId, out UWCommonObjectProperties.Entry lOCommon))
            {
                Console.WriteLine("  height " + lOCommon.Height + ", radius " + lOCommon.Radius
                    + ", mass " + lOCommon.MassTenthStones + " tenth stones, value " + lOCommon.Value);
                Console.WriteLine("  quality class " + lOCommon.QualityClass + " (damage is halved per class, 3 takes none)"
                    + ", quality type " + lOCommon.QualityType);
                Console.WriteLine("  can be picked up " + lOCommon.CanBePickedUp
                    + ", container " + lOCommon.IsContainer
                    + ", can have an owner " + lOCommon.CanHaveOwner
                    + ", 3D model " + lOCommon.Is3DModel);
            }

            if (lOData.ObjectClassProperties != null
                && lOData.ObjectClassProperties.TryGetCritter(liId, out UWObjectClassProperties.Critter lOCritter)
                && lOCritter.Vitality != 0)
            {
                Console.WriteLine("  critter: vitality " + lOCritter.Vitality
                    + ", attack power " + lOCritter.AttackPower
                    + ", defence power " + lOCritter.DefensePower
                    + ", armour " + lOCritter.Armour0 + "/" + lOCritter.Armour1 + "/" + lOCritter.Armour2 + "/" + lOCritter.Armour3
                    + ", general type " + lOCritter.GeneralType
                    + ", morale " + lOCritter.Morale);
            }

            return 0;
        }

        // ------------------------------------------------------------------ where

        /// <summary>
        /// Where objects of the given types lie on all levels (or one, --level), INSIDE
        /// CONTAINERS TOO - a bottle rarely lies around in the open, but in a chest, a barrel or
        /// a creature's inventory. Ids may be ranges (320-335 are the doors). --detail adds the
        /// object's fields and the tile's type and floor height (the editor's bridge and door
        /// lists). For finding things again in the original.
        /// </summary>
        private static int fPrintWhere(DumpContext pOContext, List<string> pOArgs)
        {
            bool lbDetail = fTakeFlag(pOArgs, "--detail");
            string lsLevel = fTakeOption(pOArgs, "--level");
            int liOnlyLevel = 0;

            if (lsLevel != null && !fParse(lsLevel, out liOnlyLevel))
            {
                Console.Error.WriteLine("--level wants a number from 1 to 9.");

                return 1;
            }

            List<int> lOIds = new List<int>();

            foreach (string lsArg in pOArgs)
            {
                int liDash = lsArg.IndexOf('-', 1);

                if (liDash > 0 && fParse(lsArg.Substring(0, liDash), out int liFrom) && fParse(lsArg.Substring(liDash + 1), out int liTo))
                {
                    for (int liId = liFrom; liId <= liTo; liId++)
                        lOIds.Add(liId);
                }
                else if (fParse(lsArg, out int liId))
                {
                    lOIds.Add(liId);
                }
                else
                {
                    Console.Error.WriteLine("Not an object id: " + lsArg);

                    return 1;
                }
            }

            if (lOIds.Count == 0)
            {
                Console.Error.WriteLine("Which objects? For example: uwdump where <path> 191 287 or 320-335 --level 1");

                return 1;
            }

            foreach (int liSearchId in lOIds)
            {
                Console.WriteLine(string.Format("--- {0} (0x{0:X3}): {1}", liSearchId, fName(pOContext, liSearchId)));

                int liFound = 0;

                foreach (UWLevel lOLevel in pOContext.Data.Levels)
                {
                    if (lOLevel == null || lOLevel.TileData == null || (liOnlyLevel != 0 && lOLevel.LevelNumber != liOnlyLevel))
                        continue;

                    for (int liTileY = 0; liTileY < UWWorldScale.TilesPerAxis; liTileY++)
                    {
                        for (int liTileX = 0; liTileX < UWWorldScale.TilesPerAxis; liTileX++)
                        {
                            UWTile lOTile = lOLevel.GetTile(liTileX, liTileY);

                            foreach (UWObject lOObject in lOTile.ObjectsInTile)
                                liFound += fWhere(pOContext, lOLevel, lOTile, lOObject, liSearchId, liTileX, liTileY, "", lbDetail);
                        }
                    }
                }

                if (liFound == 0)
                    Console.WriteLine("  nothing found");

                Console.WriteLine();
            }

            return 0;
        }

        private static int fWhere(DumpContext pOContext, UWLevel pOLevel, UWTile pOTile, UWObject pOObject, int piSearchId,
            int piTileX, int piTileY, string psPath, bool pbDetail)
        {
            if (pOObject == null)
                return 0;

            int liFound = 0;

            if (pOObject.ID == piSearchId)
            {
                Console.WriteLine(string.Format("  Level {0} tile {1}/{2}{3}{4}", pOLevel.LevelNumber, piTileX, piTileY, psPath,
                    pbDetail
                        ? string.Format("  {0} Tile={1} Floor={2}", fObjectDetail(pOObject), pOTile.TileType, pOTile.FloorHeight)
                        : string.Empty));

                liFound++;
            }

            List<UWObject> lOContents;

            try
            {
                pOObject.EnsureContentsLoaded(pOLevel.Masterlist);
                lOContents = pOObject.Contents;
            }
            catch
            {
                lOContents = null;
            }

            if (lOContents == null)
                return liFound;

            string lsPath = psPath + " in " + fName(pOContext, pOObject.ID);

            foreach (UWObject lOChild in lOContents)
                liFound += fWhere(pOContext, pOLevel, pOTile, lOChild, piSearchId, piTileX, piTileY, lsPath, pbDetail);

            return liFound;
        }

        // ------------------------------------------------------------------ creatures

        /// <summary>
        /// The creatures of one level or of all, with their behaviour fields (goal, goal target,
        /// attitude, level, hit points, home, heading, hunger, conversation slot, talked-to), the
        /// distribution of goals and attitudes, and the goals against creature type and
        /// conversation slot - what actually occurs in the data, where uw-formats.txt names only
        /// a few values. With --save a save game's creatures.
        /// </summary>
        private static int fPrintNpcs(DumpContext pOContext, List<string> pOArgs)
        {
            List<UWLevel> lOLevels = new List<UWLevel>();
            bool lbAll = pOArgs.Count == 0 || string.Equals(pOArgs[0], "all", StringComparison.OrdinalIgnoreCase);

            if (lbAll)
            {
                foreach (UWLevel lOLevel in pOContext.Data.Levels)
                {
                    if (lOLevel != null && lOLevel.TileData != null)
                        lOLevels.Add(lOLevel);
                }
            }
            else
            {
                if (!fParse(pOArgs[0], out int liLevel))
                {
                    Console.Error.WriteLine("Which level? For example: uwdump npcs <path> 1, or all");

                    return 1;
                }

                UWLevel lOLevel = fLevel(pOContext, liLevel);

                if (lOLevel == null)
                    return 4;

                lOLevels.Add(lOLevel);
            }

            foreach (UWLevel lOLevel in lOLevels)
            {
                Dictionary<int, int> lOGoals = new Dictionary<int, int>();
                Dictionary<int, int> lOAttitudes = new Dictionary<int, int>();
                int liCount = 0;

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

                        Console.WriteLine(string.Format(
                            "{0,2}/{1,-2} Id={2,-3} \"{3}\" Goal={4} GoalObj={5} Attitude={6} Level={7} HP={8} Home={9}/{10} Heading={11} Hunger={12} WhoAmI={13} TalkedTo={14}",
                            liTile % 64, liTile / 64, lONpc.ID, fName(pOContext, lONpc.ID),
                            lONpc.NPCGoal, lONpc.NPCGTarg, lONpc.NPCAttitude, lONpc.NPCLevel, lONpc.NPC_HP,
                            lONpc.NPCXHome, lONpc.NPCYHome, lONpc.NPCHeading, lONpc.NPCHunger,
                            lONpc.NPCwhoami, lONpc.NPCTalkedTo));
                    }
                }

                Console.WriteLine("--- " + liCount + " creatures on level " + lOLevel.LevelNumber);
                Console.WriteLine("Goals:     " + fFormatCounts(lOGoals));
                Console.WriteLine("Attitude:  " + fFormatCounts(lOAttitudes));
            }

            Console.WriteLine("--- Goal versus creature type and conversation slot, "
                + (lbAll ? "all levels" : "level " + lOLevels[0].LevelNumber));

            Dictionary<int, int> lOWithSlot = new Dictionary<int, int>();
            Dictionary<int, int> lOTotal = new Dictionary<int, int>();
            Dictionary<int, string> lOExamples = new Dictionary<int, string>();

            foreach (UWLevel lOLevel in lOLevels)
            {
                for (int liTile = 0; liTile < lOLevel.TileData.Length; liTile++)
                {
                    foreach (UWObject lOObject in lOLevel.TileData[liTile].ObjectsInTile)
                    {
                        UWNpc lOOne = lOObject as UWNpc;

                        if (lOOne == null)
                            continue;

                        fCount(lOTotal, lOOne.NPCGoal);

                        if (lOOne.NPCwhoami != 0)
                            fCount(lOWithSlot, lOOne.NPCGoal);

                        string lsName = fName(pOContext, lOOne.ID);

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
                lOWithSlot.TryGetValue(liGoal, out int liSlots);

                Console.WriteLine(string.Format("  Goal {0,-3}: {1,4} creatures, {2,3} of them with conversation slot   {3}",
                    liGoal, lOTotal[liGoal], liSlots, lOExamples[liGoal]));
            }

            return 0;
        }

        private static void fCount(Dictionary<int, int> pOCounts, int piValue)
        {
            pOCounts.TryGetValue(piValue, out int liExisting);
            pOCounts[piValue] = liExisting + 1;
        }

        private static string fFormatCounts(Dictionary<int, int> pOCounts)
        {
            StringBuilder lOOut = new StringBuilder();

            List<int> lOKeys = new List<int>(pOCounts.Keys);
            lOKeys.Sort();

            foreach (int liKey in lOKeys)
                lOOut.Append(liKey + ":" + pOCounts[liKey] + "  ");

            return lOOut.ToString();
        }
    }
}
