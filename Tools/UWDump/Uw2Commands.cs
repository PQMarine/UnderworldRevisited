using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class Program
    {
        /// <summary>
        /// info for the second game (UW2 plan phase 0 step 6, 2026-10-28): what the generic readers
        /// find in its DATA folder - the strings, the palettes, the .GR files and the archives with
        /// their blocks. The game data as a whole (DataImport) reads the first game's formats, so the
        /// other commands wait for phase 1.
        /// </summary>
        private static int fPrintInfoUw2(DumpContext pOContext)
        {
            string lsData = pOContext.DataPath;

            Console.WriteLine("Game        : " + pOContext.Game.Title);
            Console.WriteLine("Data folder : " + lsData);

            UWStrings lOStrings = new UWStrings(File.ReadAllBytes(Path.Combine(lsData, "STRINGS.PAK")));
            int liStrings = 0;

            foreach (UWStringBlock lOBlock in lOStrings.Blocks.Values)
                liStrings += lOBlock.Strings.Count;

            Console.WriteLine("String blocks: " + lOStrings.Blocks.Count + ", " + liStrings + " strings");

            long liPalettes = new FileInfo(Path.Combine(lsData, "PALS.DAT")).Length / 768;

            Console.WriteLine("Palettes    : " + liPalettes);

            UWPalettes lOPalettes = new UWPalettes(lsData);
            List<string> lOFiles = new List<string>(Directory.GetFiles(lsData, "*.GR"));

            lOFiles.Sort(StringComparer.OrdinalIgnoreCase);
            Console.WriteLine("Pictures    :");

            foreach (string lsFile in lOFiles)
            {
                string lsName = Path.GetFileName(lsFile);

                // No headers in PANELS.GR (phase 3 reads it).
                if (lsName.Equals("PANELS.GR", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("  " + lsName.PadRight(14) + " own format, not read yet");

                    continue;
                }

                byte[] lyFile = File.ReadAllBytes(lsFile);
                List<UWTexture> lOPictures = UWTextures.ReadImageFile(lyFile, lsName, lOPalettes, 0, UWTexture.TextureTypes.OBJECTS);
                int liAbsent = 0;

                for (int liAt = 0; liAt < lOPictures.Count; liAt++)
                {
                    if (BitConverter.ToInt32(lyFile, 3 + (4 * liAt)) >= lyFile.Length)
                        liAbsent++;
                }

                Console.WriteLine("  " + lsName.PadRight(14) + string.Format("{0,4} pictures", lOPictures.Count - liAbsent)
                    + (liAbsent > 0 ? ", " + liAbsent + " absent" : string.Empty));
            }

            Console.WriteLine("Archives    :");

            foreach (string lsArchive in new[] { "LEV.ARK", "CNV.ARK", "BYT.ARK", "SCD.ARK" })
            {
                string lsPath = Path.Combine(lsData, lsArchive);

                if (!File.Exists(lsPath))
                    continue;

                UWArk lOArk = UWArk.Read(lsPath, UWArk.LayoutEnum.Uw2);
                int liPresent = 0;
                int liCompressed = 0;

                for (int liBlock = 0; liBlock < lOArk.BlockCount; liBlock++)
                {
                    if (lOArk.ReadBlock(liBlock).Length == 0)
                        continue;

                    liPresent++;

                    if (lOArk.IsCompressed(liBlock))
                        liCompressed++;
                }

                Console.WriteLine("  " + lsArchive.PadRight(14) + string.Format("{0,4} blocks, {1,4} present, {2,4} compressed",
                    lOArk.BlockCount, liPresent, liCompressed));
            }

            Console.WriteLine("Critters    : own format (AS.AN, CR.AN, PG.MP), not read yet");

            return 0;
        }

        /// <summary>The commands that read the second game so far (phase 1 step 6): the format readers'
        /// survey, the strings, a level's map and objects, the maps as pictures, the conversations.</summary>
        private static readonly List<string> msUw2Commands = new List<string> { "info", "strings", "find", "level", "maprender", "conv", "pictures", "palette", "traps",
            "strings-template", "validate-tsv", "strings-checksums" };

        /// <summary>A command on the second game's data, without DataImport.</summary>
        private static int fRunUw2(string psName, DumpContext pOContext, List<string> pOArgs)
        {
            pOContext.Strings = new UWStrings(File.ReadAllBytes(Path.Combine(pOContext.DataPath, "STRINGS.PAK")));

            // The second game's pack (Languages/UW2/<code>.strings.tsv) over its strings, as the game lays it
            // (2026-11-25): strings, find and conv show the translation.
            if (pOContext.StringsPath != null)
            {
                if (!File.Exists(pOContext.StringsPath))
                {
                    Console.Error.WriteLine("No game-text pack " + pOContext.StringsPath + ".");

                    return 2;
                }

                Console.OutputEncoding = new System.Text.UTF8Encoding(false);
                fLoadOwnTexts(pOContext.StringsPath);

                UWGameTextPack lOPack = UWGameTextPack.Load(pOContext.StringsPath, pOContext.Strings);

                if (lOPack != null)
                    Console.Error.WriteLine("Game-text pack " + pOContext.StringsPath + ": " + lOPack.Applied + " strings, " + lOPack.Mismatched
                        + " left English (their checksum names other English), " + lOPack.Problems.Count + " problems.");
            }

            switch (psName)
            {
                case "strings-template": return fStringsTemplate(pOContext, pOArgs);
                case "validate-tsv": return fValidateTsv(pOContext, pOArgs);
                case "strings-checksums": return fStringsChecksums(pOContext, pOArgs);
                case "strings": return fPrintStrings(pOContext, pOArgs);
                case "find": return fFindString(pOContext, pOArgs);
                case "level": return fPrintLevelUw2(pOContext, pOArgs);
                case "maprender": return fRenderMapsUw2(pOContext, pOArgs);
                case "conv": return fConversationUw2(pOContext, pOArgs);
                case "pictures": return fDumpPictures(pOContext, pOArgs);
                case "palette": return fDumpPalette(pOContext, pOArgs);
                case "traps": return fPrintTrapsUw2(pOContext, pOArgs);
                default: return fPrintInfoUw2(pOContext);
            }
        }

        /// <summary>The objects in a tile: its chain from the tile's first object through each record's
        /// next link (word 2, bits 6-15), as item id and index.</summary>
        private static List<KeyValuePair<int, int>> fTileObjectsUw2(byte[] pyBlock, UWLevelBlock pOBlock, int piTile)
        {
            List<KeyValuePair<int, int>> lOObjects = new List<KeyValuePair<int, int>>();
            int liIndex = pOBlock.FirstObject(piTile);

            for (int liGuard = 0; liIndex > 0 && liIndex < UWLevArkLayout.ObjectCount && liGuard < UWLevArkLayout.ObjectCount; liGuard++)
            {
                int liAt = UWLevArkLayout.ObjectOffset(liIndex);

                lOObjects.Add(new KeyValuePair<int, int>(UWBinary.ReadU16(pyBlock, liAt) & 0x1FF, liIndex));
                liIndex = UWBinary.ReadU16(pyBlock, liAt + 4) >> 6;
            }

            return lOObjects;
        }

        /// <summary>level &lt;1-80&gt; [x y radius]: the tile map as the first game's command prints it, then
        /// the objects of each tile with their names (string block 4, id + 1).</summary>
        private static int fPrintLevelUw2(DumpContext pOContext, List<string> pOArgs)
        {
            UWLevArkFile lOFile = pOContext.Game.LevArk;

            if (pOArgs.Count < 1 || !fParse(pOArgs[0], out int liLevel) || liLevel < 1 || liLevel > lOFile.LevelCount)
            {
                Console.Error.WriteLine("Which level, 1 to " + lOFile.LevelCount + "? For example: uwdump level --game uw2 1");

                return 1;
            }

            UWArk lOArk = UWArk.Read(Path.Combine(pOContext.SavePath ?? pOContext.DataPath, "LEV.ARK"), pOContext.Game.ArchiveLayout);
            byte[] lyBlock = lOArk.ReadBlock(lOFile.MapBlock(liLevel - 1));
            UWLevelBlock lOBlock = UWLevelBlock.Read(lyBlock, lOFile);

            if (lOBlock == null)
            {
                Console.Error.WriteLine("Level " + liLevel + " is not in the archive.");

                return 4;
            }

            int liFloorAt = pOArgs.IndexOf("--floor");

            if (liFloorAt > 0 && liFloorAt + 1 < pOArgs.Count && fParse(pOArgs[liFloorAt + 1], out int liFloorId))
            {
                // --floor id: every tile whose floor shows that texture (the level's texture map, TxmID).
                UWTextureMapUw2 lOMap = UWTextureMapUw2.Read(lOArk.ReadBlock(lOFile.TextureMapBlock(liLevel - 1)));

                for (int liTile = 0; liTile < 64 * 64; liTile++)
                {
                    if (lOBlock.TileType(liTile) != 0 && lOMap != null && lOMap.Textures[lOBlock.FloorTexture(liTile)] == liFloorId)
                        Console.WriteLine(string.Format("{0,2}/{1,2}  type {2}  floor height {3}", liTile & 63, liTile >> 6, lOBlock.TileType(liTile), lOBlock.FloorHeight(liTile)));
                }

                return 0;
            }

            int liCentreX = pOArgs.Count > 2 ? fArgument(pOArgs, 1, 32) : 32;
            int liCentreY = pOArgs.Count > 2 ? fArgument(pOArgs, 2, 32) : 32;
            int liRadius = fArgument(pOArgs, 3, 31);
            int liFromX = Math.Max(0, liCentreX - liRadius);
            int liToX = Math.Min(63, liCentreX + liRadius);
            int liFromY = Math.Max(0, liCentreY - liRadius);
            int liToY = Math.Min(63, liCentreY + liRadius);

            for (int liY = liToY; liY >= liFromY; liY--)
            {
                System.Text.StringBuilder lOLine = new System.Text.StringBuilder(liY.ToString("D2") + " ");

                for (int liX = liFromX; liX <= liToX; liX++)
                    lOLine.Append(fTileCharacterUw2(lOBlock, liX + (64 * liY)));

                Console.WriteLine(lOLine.ToString());
            }

            Console.WriteLine();

            List<string> lsNames = pOContext.Strings.Blocks.TryGetValue(4, out UWStringBlock lONames) ? lONames.Strings : new List<string>();

            for (int liY = liToY; liY >= liFromY; liY--)
            {
                for (int liX = liFromX; liX <= liToX; liX++)
                {
                    foreach (KeyValuePair<int, int> lOObject in fTileObjectsUw2(lyBlock, lOBlock, liX + (64 * liY)))
                    {
                        string lsName = lOObject.Key + 1 < lsNames.Count ? fOneLine(lsNames[lOObject.Key + 1]) : "?";

                        int liAt = UWLevArkLayout.ObjectOffset(lOObject.Value);

                        Console.WriteLine(string.Format("{0,2}/{1,2}  {2,4}  {3}  (object {4}, words {5:X4} {6:X4} {7:X4} {8:X4}{9})", liX, liY, lOObject.Key, lsName, lOObject.Value,
                            UWBinary.ReadU16(lyBlock, liAt), UWBinary.ReadU16(lyBlock, liAt + 2), UWBinary.ReadU16(lyBlock, liAt + 4), UWBinary.ReadU16(lyBlock, liAt + 6),
                            lOObject.Value < 256 ? ", whoami " + lyBlock[liAt + 0x1A] : string.Empty));

                        fPrintContentsUw2(lyBlock, lsNames, lOObject.Value, "      ", 0);
                    }
                }
            }

            return 0;
        }

        /// <summary>traps --game uw2 [level] [--id 0xNNN]: every trigger lying in a tile or held by an object there
        /// (a lock, a switch), with its target square and the chain of traps behind it (each trap's link, as UseTrap
        /// follows it; a condition trap's else branch is the linked object's next), then a count of every trap kind
        /// reached and where it is first met. With --id only the chains holding that trap kind (G3, 2026-11-13).</summary>
        private static int fPrintTrapsUw2(DumpContext pOContext, List<string> pOArgs)
        {
            UWLevArkFile lOFile = pOContext.Game.LevArk;
            UWArk lOArk = UWArk.Read(Path.Combine(pOContext.SavePath ?? pOContext.DataPath, "LEV.ARK"), pOContext.Game.ArchiveLayout);
            List<string> lsNames = pOContext.Strings.Blocks.TryGetValue(4, out UWStringBlock lONames) ? lONames.Strings : new List<string>();
            int liOnly = 0;
            int liWanted = -1;

            for (int liArg = 0; liArg < pOArgs.Count; liArg++)
            {
                if (pOArgs[liArg] == "--id" && liArg + 1 < pOArgs.Count)
                    fParse(pOArgs[++liArg], out liWanted);
                else if (fParse(pOArgs[liArg], out int liLevelArg))
                    liOnly = liLevelArg;
            }

            SortedDictionary<int, int> lOCounts = new SortedDictionary<int, int>();
            Dictionary<int, List<string>> lOSites = new Dictionary<int, List<string>>();

            for (int liLevel = 1; liLevel <= lOFile.LevelCount; liLevel++)
            {
                if (liOnly > 0 && liLevel != liOnly)
                    continue;

                byte[] lyBlock;

                try
                {
                    lyBlock = lOArk.ReadBlock(lOFile.MapBlock(liLevel - 1));
                }
                catch
                {
                    continue;
                }

                UWLevelBlock lOBlock = lyBlock != null ? UWLevelBlock.Read(lyBlock, lOFile) : null;

                if (lOBlock == null)
                    continue;

                for (int liTile = 0; liTile < 64 * 64; liTile++)
                {
                    foreach (KeyValuePair<int, int> lOObject in fTileObjectsUw2(lyBlock, lOBlock, liTile))
                        fTrapsUnder(lyBlock, lsNames, liLevel, liTile & 63, liTile >> 6, lOObject.Value, 0, liWanted, lOCounts, lOSites);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Trap kinds reached from triggers:");

            foreach (KeyValuePair<int, int> lOCount in lOCounts)
            {
                string lsName = lOCount.Key + 1 < lsNames.Count ? fOneLine(lsNames[lOCount.Key + 1]) : "?";

                Console.WriteLine(string.Format("  0x{0:X3} {1,-24} {2,4}   first at {3}", lOCount.Key, lsName, lOCount.Value,
                    string.Join(", ", lOSites[lOCount.Key])));
            }

            return 0;
        }

        /// <summary>A tile object, and what it holds, searched for triggers (four deep).</summary>
        private static void fTrapsUnder(byte[] pyBlock, List<string> pONames, int piLevel, int piX, int piY, int piObject, int piDepth,
            int piWanted, SortedDictionary<int, int> pOCounts, Dictionary<int, List<string>> pOSites)
        {
            int liAt = UWLevArkLayout.ObjectOffset(piObject);

            if (piDepth > 3 || liAt + 8 > pyBlock.Length)
                return;

            int liWord0 = UWBinary.ReadU16(pyBlock, liAt);
            int liId = liWord0 & 0x1FF;

            if (liId >= 0x1A0 && liId < 0x1C0)
            {
                fPrintTriggerChain(pyBlock, pONames, piLevel, piX, piY, piObject, piWanted, pOCounts, pOSites);

                return;
            }

            if ((liWord0 & 0x8000) != 0)
                return;

            int liNext = UWBinary.ReadU16(pyBlock, liAt + 6) >> 6;

            for (int liSteps = 0; liNext > 0 && liSteps < 64; liSteps++)
            {
                int liItemAt = UWLevArkLayout.ObjectOffset(liNext);

                if (liItemAt + 8 > pyBlock.Length)
                    return;

                fTrapsUnder(pyBlock, pONames, piLevel, piX, piY, liNext, piDepth + 1, piWanted, pOCounts, pOSites);
                liNext = UWBinary.ReadU16(pyBlock, liItemAt + 4) >> 6;
            }
        }

        /// <summary>One trigger and its chain, on one line each step.</summary>
        private static void fPrintTriggerChain(byte[] pyBlock, List<string> pONames, int piLevel, int piX, int piY, int piTrigger,
            int piWanted, SortedDictionary<int, int> pOCounts, Dictionary<int, List<string>> pOSites)
        {
            List<string> lsSteps = new List<string>();
            List<int> liKinds = new List<int>();
            int liAt = UWLevArkLayout.ObjectOffset(piTrigger);
            int liTrigger = UWBinary.ReadU16(pyBlock, liAt) & 0x1FF;
            int liTargetX = UWBinary.ReadU16(pyBlock, liAt + 4) & 0x3F;
            int liTargetY = UWBinary.ReadU16(pyBlock, liAt + 6) & 0x3F;
            int liLink = UWBinary.ReadU16(pyBlock, liAt + 6) >> 6;
            HashSet<int> lOSeen = new HashSet<int>();

            for (int liSteps = 0; liLink > 0 && liLink < UWLevArkLayout.ObjectCount && liSteps < 12 && lOSeen.Add(liLink); liSteps++)
            {
                int liTrapAt = UWLevArkLayout.ObjectOffset(liLink);

                if (liTrapAt + 8 > pyBlock.Length)
                    break;

                int liWord0 = UWBinary.ReadU16(pyBlock, liTrapAt);
                int liWord1 = UWBinary.ReadU16(pyBlock, liTrapAt + 2);
                int liId = liWord0 & 0x1FF;

                if ((liId >> 6) != 6)
                {
                    lsSteps.Add(string.Format("[{0} {1}]", liId, liId + 1 < pONames.Count ? fOneLine(pONames[liId + 1]) : "?"));

                    break;
                }

                lsSteps.Add(string.Format("0x{0:X3}(q{1} o{2} z{3} h{4} fx{5} fy{6} #{7})", liId, UWBinary.ReadU16(pyBlock, liTrapAt + 4) & 0x3F,
                    UWBinary.ReadU16(pyBlock, liTrapAt + 6) & 0x3F, liWord1 & 0x7F, (liWord1 >> 7) & 7, (liWord1 >> 13) & 7, (liWord1 >> 10) & 7, liLink));

                if (liId >= 0x1A0)
                    break;

                liKinds.Add(liId);
                liLink = UWBinary.ReadU16(pyBlock, liTrapAt + 6) >> 6;
            }

            if (piWanted >= 0 && !liKinds.Contains(piWanted))
                return;

            foreach (int liKind in liKinds)
            {
                pOCounts[liKind] = pOCounts.TryGetValue(liKind, out int liCount) ? liCount + 1 : 1;

                if (!pOSites.TryGetValue(liKind, out List<string> lsSites))
                    pOSites[liKind] = lsSites = new List<string>();

                if (lsSites.Count < 3)
                    lsSites.Add(string.Format("{0}:{1}/{2}", piLevel, piX, piY));
            }

            Console.WriteLine(string.Format("L{0,2} {1,2}/{2,2}  0x{3:X3} flags {4} -> {5}/{6}  {7}", piLevel, piX, piY, liTrigger,
                (UWBinary.ReadU16(pyBlock, liAt) >> 9) & 0xF, liTargetX, liTargetY, string.Join(" > ", lsSteps)));
        }

        /// <summary>What an object holds (its quantity bit clear and a link in word 3): the chain along the
        /// next links of word 2, each with its words, indented, and what those hold in turn (a wand's spell, a
        /// bag's things) - at most four deep and 64 to a chain.</summary>
        private static void fPrintContentsUw2(byte[] pyBlock, List<string> pONames, int piObject, string psIndent, int piDepth)
        {
            int liAt = UWLevArkLayout.ObjectOffset(piObject);

            if (piDepth > 3 || liAt + 8 > pyBlock.Length || (UWBinary.ReadU16(pyBlock, liAt) & 0x8000) != 0)
                return;

            int liNext = UWBinary.ReadU16(pyBlock, liAt + 6) >> 6;

            for (int liSteps = 0; liNext > 0 && liSteps < 64; liSteps++)
            {
                int liItemAt = UWLevArkLayout.ObjectOffset(liNext);

                if (liItemAt + 8 > pyBlock.Length)
                    return;

                int liId = UWBinary.ReadU16(pyBlock, liItemAt) & 0x1FF;
                string lsName = liId + 1 < pONames.Count ? fOneLine(pONames[liId + 1]) : "?";

                Console.WriteLine(string.Format("{0}holds {1,4}  {2}  (object {3}, words {4:X4} {5:X4} {6:X4} {7:X4})", psIndent, liId, lsName, liNext,
                    UWBinary.ReadU16(pyBlock, liItemAt), UWBinary.ReadU16(pyBlock, liItemAt + 2), UWBinary.ReadU16(pyBlock, liItemAt + 4),
                    UWBinary.ReadU16(pyBlock, liItemAt + 6)));

                fPrintContentsUw2(pyBlock, pONames, liNext, psIndent + "  ", piDepth + 1);
                liNext = UWBinary.ReadU16(pyBlock, liItemAt + 4) >> 6;
            }
        }

        /// <summary>The first game's characters: # solid, / a diagonal, s a slope, . an open floor at
        /// height 0, else its height as a hex digit.</summary>
        private static char fTileCharacterUw2(UWLevelBlock pOBlock, int piTile)
        {
            int liType = pOBlock.TileType(piTile);

            if (liType == 0)
                return '#';

            if (liType >= 2 && liType <= 5)
                return '/';

            if (liType >= 6)
                return 's';

            int liHeight = pOBlock.FloorHeight(piTile);

            return liHeight == 0 ? '.' : "0123456789abcdef"[liHeight];
        }

        /// <summary>maprender --game uw2 --out dir [--highlight id ...]: every level the archive holds as
        /// Level01.png to Level80.png in the first game's colours (walkable light by height, doors blue),
        /// and the floor's terrain class: water blue, lava red, ice pale.</summary>
        private static int fRenderMapsUw2(DumpContext pOContext, List<string> pOArgs)
        {
            string lsOut = fTakeOption(pOArgs, "--out");
            List<int> lOHighlight = new List<int>();
            int liAt = pOArgs.FindIndex(a => string.Equals(a, "--highlight", StringComparison.OrdinalIgnoreCase));

            if (liAt >= 0)
            {
                pOArgs.RemoveAt(liAt);

                while (liAt < pOArgs.Count && fParse(pOArgs[liAt], out int liId))
                {
                    lOHighlight.Add(liId);
                    pOArgs.RemoveAt(liAt);
                }
            }

            if (lsOut == null)
            {
                Console.Error.WriteLine("Where to? For example: uwdump maprender --game uw2 --out maps2");

                return 1;
            }

            Directory.CreateDirectory(lsOut);

            UWLevArkFile lOFile = pOContext.Game.LevArk;
            UWArk lOArk = UWArk.Read(Path.Combine(pOContext.SavePath ?? pOContext.DataPath, "LEV.ARK"), pOContext.Game.ArchiveLayout);
            UWTerrainUw2 lOTerrain = new UWTerrainUw2(File.ReadAllBytes(Path.Combine(pOContext.DataPath, "TERRAIN.DAT")));
            int liSize = 64 * MapPixelsPerTile;

            for (int liLevel = 0; liLevel < lOFile.LevelCount; liLevel++)
            {
                byte[] lyBlock = lOArk.ReadBlock(lOFile.MapBlock(liLevel));
                UWLevelBlock lOBlock = UWLevelBlock.Read(lyBlock, lOFile);

                if (lOBlock == null)
                    continue;

                UWTextureMapUw2 lOMap = UWTextureMapUw2.Read(lOArk.ReadBlock(lOFile.TextureMapBlock(liLevel)));
                Rgba[] lOPixels = new Rgba[liSize * liSize];
                List<string> lOFound = new List<string>();

                for (int liTile = 0; liTile < UWLevArkLayout.TileCount; liTile++)
                {
                    Rgba lOColour = fTileColourUw2(lOBlock, lOMap, lOTerrain, liTile);

                    foreach (KeyValuePair<int, int> lOObject in fTileObjectsUw2(lyBlock, lOBlock, liTile))
                    {
                        if (!lOHighlight.Contains(lOObject.Key))
                            continue;

                        lOColour = msMapHighlight;
                        lOFound.Add(string.Format("{0} at {1}/{2}", lOObject.Key, liTile % 64, liTile / 64));
                    }

                    fFill(lOPixels, liSize, liTile % 64, liTile / 64, MapPixelsPerTile, lOColour);
                }

                fDrawGrid(lOPixels, liSize);
                PngWriter.Write(Path.Combine(lsOut, string.Format("Level{0:00}.png", liLevel + 1)), liSize, liSize, fToBytes(lOPixels), true);
                Console.WriteLine(string.Format("Level {0}: {1}", liLevel + 1, lOFound.Count == 0 ? "nothing highlighted" : string.Join(", ", lOFound)));
            }

            return 0;
        }

        private static Rgba fTileColourUw2(UWLevelBlock pOBlock, UWTextureMapUw2 pOMap, UWTerrainUw2 pOTerrain, int piTile)
        {
            int liType = pOBlock.TileType(piTile);

            if (liType == 0)
                return msMapSolid;

            if (pOBlock.HasDoor(piTile))
                return msMapDoor;

            int liClass = pOMap == null ? 0 : pOTerrain.Class(pOMap.Textures[pOBlock.FloorTexture(piTile)]);

            switch (liClass)
            {
                case UWTerrainUw2.ClassWater: return new Rgba(50, 90, 200, 255);
                case UWTerrainUw2.ClassLava: return new Rgba(210, 70, 30, 255);
                case UWTerrainUw2.ClassIce: return new Rgba(190, 225, 240, 255);
            }

            byte lyShade = (byte)(90 + (Math.Clamp(pOBlock.FloorHeight(piTile) * 16 / 192f, 0f, 1f) * (double)130f));

            return liType != 1
                ? new Rgba((byte)(lyShade * (double)0.8f), lyShade, (byte)(lyShade * (double)0.7f), 255)
                : new Rgba(lyShade, lyShade, (byte)(lyShade * (double)0.92f), 255);
        }

        /// <summary>conv --game uw2 &lt;slot|all&gt;: a conversation's bytecode with its strings, the first
        /// game's disassembler on the second game's block (the same format); all: one line each.</summary>
        private static int fConversationUw2(DumpContext pOContext, List<string> pOArgs)
        {
            UWArk lOArk = UWArk.Read(Path.Combine(pOContext.DataPath, "CNV.ARK"), pOContext.Game.ArchiveLayout);
            string lsWhich = pOArgs.Count > 0 ? pOArgs[0] : "all";

            if (!string.Equals(lsWhich, "all", StringComparison.OrdinalIgnoreCase))
            {
                if (!fParse(lsWhich, out int liSlot) || liSlot < 0 || liSlot >= lOArk.BlockCount || lOArk.ReadBlock(liSlot).Length == 0)
                {
                    Console.Error.WriteLine("Slot " + lsWhich + " holds no conversation (0 to " + (lOArk.BlockCount - 1) + ").");

                    return 4;
                }

                UWConversations.Conversation lOConversation = UWConversations.Parse(lOArk.ReadBlock(liSlot), 0, liSlot);

                Console.WriteLine(string.Format("--- Slot {0}, string block 0x{1:X4} ---", liSlot, lOConversation.StringBlock));
                Console.Write(UWConversationDisassembler.Disassemble(lOConversation, pOContext.Strings));

                return 0;
            }

            int liUsed = 0;
            int liWords = 0;

            for (int liSlot = 0; liSlot < lOArk.BlockCount; liSlot++)
            {
                byte[] lyBlock = lOArk.ReadBlock(liSlot);

                if (lyBlock.Length == 0)
                    continue;

                UWConversations.Conversation lOConversation = UWConversations.Parse(lyBlock, 0, liSlot);

                liUsed++;
                liWords += lOConversation.Code.Length;
                Console.WriteLine(string.Format("{0,3}  string block 0x{1:X4}  {2,5} words  {3,3} imports", liSlot, lOConversation.StringBlock,
                    lOConversation.Code.Length, lOConversation.Imports.Count));
            }

            Console.WriteLine(string.Format("{0} slots, {1} conversations, {2} code words.", lOArk.BlockCount, liUsed, liWords));

            return 0;
        }
    }
}
