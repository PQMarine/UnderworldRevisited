using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Reads the original Ultima Underworld 1 data without a game engine.
    ///
    /// Everything it prints comes from the same layer the game itself loads from
    /// (Assets/UWDataImport), compiled here with the plain .NET SDK. The tool is useful in
    /// its own right - for looking things up while working on the port - and it is the
    /// standing proof that the layer needs no engine.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] psArgs)
        {
            if (psArgs.Length < 2)
            {
                fPrintUsage();

                return 1;
            }

            string lsCommand = psArgs[0].ToLowerInvariant();
            string lsDataPath = DataPath.Resolve(psArgs[1], out string lsError);

            if (lsDataPath == null)
            {
                Console.Error.WriteLine(lsError);

                return 2;
            }

            DataImport lOData;

            try
            {
                lOData = new DataImport(lsDataPath);
            }
            catch (Exception lOException)
            {
                Console.Error.WriteLine("Could not read the game data in " + lsDataPath + ": " + lOException.Message);

                return 3;
            }

            switch (lsCommand)
            {
                case "info":
                    fPrintInfo(lOData, lsDataPath);
                    return 0;

                case "strings":
                    return fPrintStrings(lOData, psArgs);

                case "find":
                    return fFindString(lOData, psArgs);

                case "level":
                    return fPrintLevel(lOData, psArgs);

                case "object":
                    return fPrintObject(lOData, psArgs);

                case "triggers":
                    return fPrintMoveTriggers(lOData);

                default:
                    fPrintUsage();
                    return 1;
            }
        }

        private static void fPrintUsage()
        {
            Console.WriteLine("uwdump - reads Ultima Underworld 1 data without a game engine.");
            Console.WriteLine();
            Console.WriteLine("  uwdump info    <path>                     what was found and loaded");
            Console.WriteLine("  uwdump strings <path> <block> [from] [n]  strings of a block");
            Console.WriteLine("  uwdump find    <path> <text>              search all string blocks");
            Console.WriteLine("  uwdump level   <path> <1-9> [x y radius]  tile map and what stands in it");
            Console.WriteLine("  uwdump object  <path> <id>                properties of an object type");
            Console.WriteLine("  uwdump triggers <path>                    every move trigger, its flags and traps");
            Console.WriteLine();
            Console.WriteLine("<path> is the DATA folder of an installation, or the GOG installation");
            Console.WriteLine("folder or game.gog itself - the image is then extracted into a cache once.");
        }

        private static void fPrintInfo(DataImport pOData, string psDataPath)
        {
            Console.WriteLine("Data folder : " + psDataPath);
            Console.WriteLine("Levels      : " + (pOData.Levels == null ? 0 : pOData.Levels.Count));
            Console.WriteLine("String blocks: " + (pOData.Strings == null ? 0 : pOData.Strings.Blocks.Count));

            if (pOData.Levels == null)
                return;

            foreach (UWLevel lOLevel in pOData.Levels)
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
        }

        private static int fPrintStrings(DataImport pOData, string[] psArgs)
        {
            if (psArgs.Length < 3 || !fParse(psArgs[2], out int liBlock))
            {
                Console.Error.WriteLine("Which block? For example: uwdump strings <path> 1");

                return 1;
            }

            if (!pOData.Strings.Blocks.TryGetValue(liBlock, out UWStringBlock lOBlock) || lOBlock.Strings == null)
            {
                Console.Error.WriteLine("Block " + liBlock + " does not exist.");

                return 4;
            }

            int liFrom = psArgs.Length > 3 && fParse(psArgs[3], out int liStart) ? liStart : 0;
            int liCount = psArgs.Length > 4 && fParse(psArgs[4], out int liHowMany) ? liHowMany : lOBlock.Strings.Count;

            for (int liAt = Math.Max(0, liFrom); liAt < lOBlock.Strings.Count && liAt < liFrom + liCount; liAt++)
                Console.WriteLine(string.Format("[{0}:{1}] {2}", liBlock, liAt, fOneLine(lOBlock.Strings[liAt])));

            return 0;
        }

        private static int fFindString(DataImport pOData, string[] psArgs)
        {
            if (psArgs.Length < 3)
            {
                Console.Error.WriteLine("What should be searched for? For example: uwdump find <path> lockpick");

                return 1;
            }

            string lsNeedle = psArgs[2];
            int liHits = 0;

            foreach (KeyValuePair<int, UWStringBlock> lOPair in pOData.Strings.Blocks)
            {
                List<string> lOStrings = lOPair.Value == null ? null : lOPair.Value.Strings;

                for (int liAt = 0; lOStrings != null && liAt < lOStrings.Count; liAt++)
                {
                    if (lOStrings[liAt] == null
                        || lOStrings[liAt].IndexOf(lsNeedle, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    Console.WriteLine(string.Format("[{0}:{1}] {2}", lOPair.Key, liAt, fOneLine(lOStrings[liAt])));
                    liHits++;
                }
            }

            Console.WriteLine(liHits + " hits.");

            return 0;
        }

        /// <summary>Tile map as in the save-editing tool: '#' solid, '.' open, '/' diagonal,
        /// 's' sloped, the digit is the floor height.</summary>
        /// <summary>Every a_move trigger of the nine levels: tile, the flags of word 0 bits 9-12
        /// (bit 1 = 0x400 repeatable, bit 2 = 0x800 the player sets it off, bit 3 = 0x1000 creatures
        /// and things do, see Trigger_ovr153_3B) and the chain of traps behind it.</summary>
        private static int fPrintMoveTriggers(DataImport pOData)
        {
            if (pOData.Levels == null)
                return 4;

            foreach (UWLevel lOLevel in pOData.Levels)
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

                        System.Text.StringBuilder lOChain = new System.Text.StringBuilder();
                        UWObject lOLink = UWObjectMechanics.GetLinkedObject(lOObject, lOLevel.Masterlist);

                        for (int liDepth = 0; lOLink != null && liDepth < 6; liDepth++)
                        {
                            lOChain.Append(string.Format(" -> {0:X3} {1} q{2}", lOLink.ID,
                                fOneLine(pOData.GetObjectDescription(lOLink.ID + 1)), lOLink.Quality));

                            if (((lOLink.ID >> 6) & 7) != 6)
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

        private static int fPrintLevel(DataImport pOData, string[] psArgs)
        {
            if (psArgs.Length < 3 || !fParse(psArgs[2], out int liLevel))
            {
                Console.Error.WriteLine("Which level? For example: uwdump level <path> 1");

                return 1;
            }

            UWLevel lOLevel = pOData.Levels == null ? null : pOData.Levels.Find(l => l != null && l.LevelNumber == liLevel);

            if (lOLevel == null || lOLevel.TileData == null)
            {
                Console.Error.WriteLine("Level " + liLevel + " does not exist.");

                return 4;
            }

            const int liTilesPerAxis = 64;

            int liCentreX = psArgs.Length > 4 && fParse(psArgs[3], out int liX) ? liX : 32;
            int liCentreY = psArgs.Length > 4 && fParse(psArgs[4], out int liY) ? liY : 32;
            int liRadius = psArgs.Length > 5 && fParse(psArgs[5], out int liR) ? liR : 31;

            int liFromX = Math.Max(0, liCentreX - liRadius);
            int liToX = Math.Min(liTilesPerAxis - 1, liCentreX + liRadius);
            int liFromY = Math.Max(0, liCentreY - liRadius);
            int liToY = Math.Min(liTilesPerAxis - 1, liCentreY + liRadius);

            // North is up, so the higher Y comes first.
            for (int liTileY = liToY; liTileY >= liFromY; liTileY--)
            {
                System.Text.StringBuilder lOLine = new System.Text.StringBuilder();

                lOLine.Append(liTileY.ToString("D2")).Append(' ');

                for (int liTileX = liFromX; liTileX <= liToX; liTileX++)
                    lOLine.Append(fTileCharacter(lOLevel.TileData[(liTileY * liTilesPerAxis) + liTileX]));

                Console.WriteLine(lOLine.ToString());
            }

            Console.WriteLine();

            for (int liTileY = liToY; liTileY >= liFromY; liTileY--)
            {
                for (int liTileX = liFromX; liTileX <= liToX; liTileX++)
                {
                    UWTile lOTile = lOLevel.TileData[(liTileY * liTilesPerAxis) + liTileX];

                    if (lOTile == null || lOTile.ObjectsInTile == null || lOTile.ObjectsInTile.Count == 0)
                        continue;

                    foreach (UWObject lOObject in lOTile.ObjectsInTile)
                    {
                        if (lOObject == null)
                            continue;

                        Console.WriteLine(string.Format("{0,2}/{1,2}  {2,4}  {3}",
                            liTileX, liTileY, lOObject.ID, fOneLine(pOData.GetObjectDescription(lOObject.ID + 1))));
                    }
                }
            }

            return 0;
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

        private static int fPrintObject(DataImport pOData, string[] psArgs)
        {
            if (psArgs.Length < 3 || !fParse(psArgs[2], out int liId))
            {
                Console.Error.WriteLine("Which object? For example: uwdump object <path> 347");

                return 1;
            }

            Console.WriteLine(string.Format("{0} (0x{0:X3})  {1}", liId, fOneLine(pOData.GetObjectDescription(liId + 1))));

            if (pOData.CommonObjectProperties != null
                && pOData.CommonObjectProperties.TryGet(liId, out UWCommonObjectProperties.Entry lOCommon))
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

            if (pOData.ObjectClassProperties != null
                && pOData.ObjectClassProperties.TryGetCritter(liId, out UWObjectClassProperties.Critter lOCritter)
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

        private static bool fParse(string psText, out int piValue)
        {
            if (psText != null && psText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(psText.Substring(2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out piValue);

            return int.TryParse(psText, out piValue);
        }

        /// <summary>The strings carry the original line breaks; for a list one line is enough.
        /// </summary>
        private static string fOneLine(string psText)
        {
            return psText == null ? string.Empty : psText.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
