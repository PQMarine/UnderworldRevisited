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
        private static readonly List<string> msUw2Commands = new List<string> { "info", "strings", "find", "level", "maprender", "conv", "pictures" };

        /// <summary>A command on the second game's data, without DataImport.</summary>
        private static int fRunUw2(string psName, DumpContext pOContext, List<string> pOArgs)
        {
            pOContext.Strings = new UWStrings(File.ReadAllBytes(Path.Combine(pOContext.DataPath, "STRINGS.PAK")));

            switch (psName)
            {
                case "strings": return fPrintStrings(pOContext, pOArgs);
                case "find": return fFindString(pOContext, pOArgs);
                case "level": return fPrintLevelUw2(pOContext, pOArgs);
                case "maprender": return fRenderMapsUw2(pOContext, pOArgs);
                case "conv": return fConversationUw2(pOContext, pOArgs);
                case "pictures": return fDumpPictures(pOContext, pOArgs);
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

            UWArk lOArk = UWArk.Read(Path.Combine(pOContext.DataPath, "LEV.ARK"), pOContext.Game.ArchiveLayout);
            byte[] lyBlock = lOArk.ReadBlock(lOFile.MapBlock(liLevel - 1));
            UWLevelBlock lOBlock = UWLevelBlock.Read(lyBlock, lOFile);

            if (lOBlock == null)
            {
                Console.Error.WriteLine("Level " + liLevel + " is not in the archive.");

                return 4;
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

                        Console.WriteLine(string.Format("{0,2}/{1,2}  {2,4}  {3}  (object {4})", liX, liY, lOObject.Key, lsName, lOObject.Value));
                    }
                }
            }

            return 0;
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
            UWArk lOArk = UWArk.Read(Path.Combine(pOContext.DataPath, "LEV.ARK"), pOContext.Game.ArchiveLayout);
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
