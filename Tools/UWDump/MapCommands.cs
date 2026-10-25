using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The pictures: a level's automap (the values and, with --png, an image) and a top view of
    /// every level with doors, teleports and searched objects in colour. The colours are for
    /// comparison only, not the original's. The editor's automap and map dumps moved here with
    /// the same colours and the same pixels (the PNGs are written bottom-up as Unity wrote them,
    /// so tile 0/0 is at the bottom left, X to the right, Y upwards - as in the tile numbering
    /// of the other commands).
    /// </summary>
    public static partial class Program
    {
        private const int AutomapPixelsPerTile = 6;

        private const int MapPixelsPerTile = 8;

        private struct Rgba
        {
            public byte R;
            public byte G;
            public byte B;
            public byte A;

            public Rgba(byte pyR, byte pyG, byte pyB, byte pyA)
            {
                R = pyR;
                G = pyG;
                B = pyB;
                A = pyA;
            }
        }

        private static readonly Rgba msMapSolid = new Rgba(18, 18, 22, 255);
        private static readonly Rgba msMapDoor = new Rgba(80, 140, 255, 255);
        private static readonly Rgba msMapTeleport = new Rgba(60, 220, 120, 255);
        private static readonly Rgba msMapHighlight = new Rgba(255, 60, 40, 255);
        private static readonly Rgba msMapGrid = new Rgba(0, 0, 0, 60);

        // ------------------------------------------------------------------ automap

        /// <summary>
        /// The automap of a level (1 by default): how often each value of the low nibble (tile
        /// type) and the high nibble (display kind) occurs - the test of the interpretation of
        /// uw-formats.txt 4.7 - and with --png the map as a picture. The pristine data holds an
        /// undiscovered map; --save reads a save game's.
        /// </summary>
        private static int fPrintAutomap(DumpContext pOContext, List<string> pOArgs)
        {
            string lsPng = fTakeOption(pOArgs, "--png");
            UWLevel lOLevel = fLevel(pOContext, fArgument(pOArgs, 0, 1));

            if (lOLevel == null)
                return 4;

            Console.WriteLine("Savegame: " + (pOContext.SavePath ?? "none, the pristine world"));
            Console.WriteLine("Level " + lOLevel.LevelNumber);

            if (lOLevel.AutomapTiles == null)
            {
                // DATA\LEV.ARK holds none; the original makes one when the level is entered.
                Console.Error.WriteLine("No automap for level " + lOLevel.LevelNumber
                    + " in this LEV.ARK - the pristine data has none; read a save game with --save <SAVEn folder>.");

                return 4;
            }

            int[] liLowCount = new int[16];
            int[] liHighCount = new int[16];

            foreach (byte lyTile in lOLevel.AutomapTiles)
            {
                liLowCount[lyTile & 0xF]++;
                liHighCount[(lyTile >> 4) & 0xF]++;
            }

            Console.WriteLine();
            Console.WriteLine("Low nibble (tile type):");

            for (int liAt = 0; liAt < 16; liAt++)
            {
                if (liLowCount[liAt] > 0)
                    Console.WriteLine(string.Format("  {0,2} = {1}", liAt, liLowCount[liAt]));
            }

            Console.WriteLine();
            Console.WriteLine("High nibble (display):");

            for (int liAt = 0; liAt < 16; liAt++)
            {
                if (liHighCount[liAt] > 0)
                    Console.WriteLine(string.Format("  {0,2} = {1}", liAt, liHighCount[liAt]));
            }

            if (lsPng != null)
            {
                fWriteAutomap(lOLevel, lsPng);
                Console.WriteLine();
                Console.WriteLine("Picture in " + lsPng);
            }

            return 0;
        }

        private static void fWriteAutomap(UWLevel pOLevel, string psPath)
        {
            const int liTiles = UWWorldScale.TilesPerAxis;
            int liSize = liTiles * AutomapPixelsPerTile;
            Rgba[] lOPixels = new Rgba[liSize * liSize];

            for (int liTileY = 0; liTileY < liTiles; liTileY++)
            {
                for (int liTileX = 0; liTileX < liTiles; liTileX++)
                {
                    Rgba lOColour = fAutomapColour(pOLevel.AutomapTiles[(liTileY * liTiles) + liTileX]);

                    fFill(lOPixels, liSize, liTileX, liTileY, AutomapPixelsPerTile, lOColour);
                }
            }

            PngWriter.Write(psPath, liSize, liSize, fToBytes(lOPixels), true);
        }

        /// <summary>Colours for comparison only, not those of the original.
        ///
        /// The display values in UW1 are DIFFERENT from those described in uw-formats.txt - that
        /// lists the Underworld 2 version. In UW1: 1 water, 2 lava, 4 door, 9 and 10 bridge,
        /// 12 stairs (per the reference, automaptileinfo).</summary>
        private static Rgba fAutomapColour(byte pyTile)
        {
            int liType = pyTile & 0xF;
            int liDisplay = (pyTile >> 4) & 0xF;

            if (liType > 9)
                return new Rgba(0, 0, 0, 255);

            if (liDisplay == 1)
                return new Rgba(70, 90, 220, 255);

            if (liDisplay == 2)
                return new Rgba(220, 90, 40, 255);

            if (liDisplay == 4)
                return new Rgba(200, 60, 60, 255);

            if (liDisplay == 12)
                return new Rgba(80, 220, 120, 255);

            if (liDisplay == 9 || liDisplay == 10)
                return new Rgba(180, 140, 60, 255);

            return liType == 0
                ? new Rgba(60, 50, 45, 255)
                : new Rgba(200, 175, 140, 255);
        }

        // ------------------------------------------------------------------ maps

        /// <summary>
        /// A top view of every level as Level01.png to Level09.png in --out: walkable light,
        /// solid dark, the brightness following the floor height; doors blue, teleport traps
        /// green, the --highlight ids red (tiles listed). A grid line every eight tiles, so that
        /// coordinates can be counted off. For finding things again in the original - and the
        /// pictures are the original's level layout: never into the repository.
        /// </summary>
        private static int fRenderMaps(DumpContext pOContext, List<string> pOArgs)
        {
            string lsOut = fTakeOption(pOArgs, "--out");
            List<int> lOHighlight = new List<int>();
            int liAt = pOArgs.FindIndex(a => string.Equals(a, "--highlight", StringComparison.OrdinalIgnoreCase));

            if (liAt >= 0)
            {
                pOArgs.RemoveAt(liAt);

                while (liAt < pOArgs.Count && !pOArgs[liAt].StartsWith("--", StringComparison.Ordinal))
                {
                    if (!fParse(pOArgs[liAt], out int liId))
                    {
                        Console.Error.WriteLine("Not an object id: " + pOArgs[liAt]);

                        return 1;
                    }

                    lOHighlight.Add(liId);
                    pOArgs.RemoveAt(liAt);
                }
            }

            if (lsOut == null)
            {
                Console.Error.WriteLine("Where to? For example: uwdump maprender <path> --out maps --highlight 1");

                return 1;
            }

            Directory.CreateDirectory(lsOut);

            foreach (UWLevel lOLevel in pOContext.Data.Levels)
            {
                if (lOLevel == null || lOLevel.TileData == null)
                    continue;

                List<string> lOFound = new List<string>();
                int liSize = UWWorldScale.TilesPerAxis * MapPixelsPerTile;
                Rgba[] lOPixels = fRenderLevel(lOLevel, lOHighlight, lOFound);

                PngWriter.Write(Path.Combine(lsOut, string.Format("Level{0:00}.png", lOLevel.LevelNumber)),
                    liSize, liSize, fToBytes(lOPixels), true);

                Console.WriteLine(string.Format("Level {0}: {1}", lOLevel.LevelNumber,
                    lOFound.Count == 0 ? "nothing highlighted" : string.Join(", ", lOFound)));
            }

            return 0;
        }

        private static Rgba[] fRenderLevel(UWLevel pOLevel, List<int> pOHighlight, List<string> pOFound)
        {
            const int liTiles = UWWorldScale.TilesPerAxis;
            int liSize = liTiles * MapPixelsPerTile;
            Rgba[] lOPixels = new Rgba[liSize * liSize];

            for (int liTileY = 0; liTileY < liTiles; liTileY++)
            {
                for (int liTileX = 0; liTileX < liTiles; liTileX++)
                {
                    UWTile lOTile = pOLevel.GetTile(liTileX, liTileY);
                    Rgba lOColour = fTileColour(lOTile);

                    foreach (UWObject lOObject in lOTile.ObjectsInTile)
                    {
                        if (lOObject == null)
                            continue;

                        if (lOObject.GetCategory() == UWObject.ObjectCategoryEnum.Doors)
                            lOColour = msMapDoor;

                        if (lOObject.ID == UWTrapRules.TeleportTrapId)
                            lOColour = msMapTeleport;
                    }

                    foreach (UWObject lOObject in lOTile.ObjectsInTile)
                    {
                        if (lOObject == null || !pOHighlight.Contains(lOObject.ID))
                            continue;

                        lOColour = msMapHighlight;

                        pOFound.Add(string.Format("{0} at {1}/{2}", lOObject.ID, liTileX, liTileY));
                    }

                    fFill(lOPixels, liSize, liTileX, liTileY, MapPixelsPerTile, lOColour);
                }
            }

            fDrawGrid(lOPixels, liSize);

            return lOPixels;
        }

        /// <summary>Walkable light, solid dark; brightness follows floor height, so that
        /// different heights within the level can be told apart. Diagonals and slopes slightly
        /// green, so the shape stays recognisable.
        ///
        /// The products are taken in double precision on the float factors: that is what the
        /// editor's version computed under Mono, and in single precision a shade like 220 * 0.7f
        /// rounds up to 154 where the editor's pictures have 153.</summary>
        private static Rgba fTileColour(UWTile pOTile)
        {
            if (pOTile.TileType == UWTile.TileTypeEnum.solid)
                return msMapSolid;

            float lfHeight = Math.Clamp(pOTile.FloorHeight / 192f, 0f, 1f);

            byte lyShade = (byte)(90 + (lfHeight * (double)130f));

            bool lbSpecial = pOTile.TileType != UWTile.TileTypeEnum.open;

            return lbSpecial
                ? new Rgba((byte)(lyShade * (double)0.8f), lyShade, (byte)(lyShade * (double)0.7f), 255)
                : new Rgba(lyShade, lyShade, (byte)(lyShade * (double)0.92f), 255);
        }

        /// <summary>One tile's square; the rows start at the bottom (tile 0/0 bottom left).</summary>
        private static void fFill(Rgba[] pOPixels, int piSize, int piTileX, int piTileY, int piPixelsPerTile, Rgba pOColour)
        {
            int liBaseX = piTileX * piPixelsPerTile;
            int liBaseY = piTileY * piPixelsPerTile;

            for (int liY = 0; liY < piPixelsPerTile; liY++)
            {
                for (int liX = 0; liX < piPixelsPerTile; liX++)
                    pOPixels[((liBaseY + liY) * piSize) + liBaseX + liX] = pOColour;
            }
        }

        private static void fDrawGrid(Rgba[] pOPixels, int piSize)
        {
            for (int liTile = 0; liTile <= UWWorldScale.TilesPerAxis; liTile += 8)
            {
                int liPixel = Math.Min(piSize - 1, liTile * MapPixelsPerTile);

                for (int liAt = 0; liAt < piSize; liAt++)
                {
                    pOPixels[(liAt * piSize) + liPixel] = fBlend(pOPixels[(liAt * piSize) + liPixel], msMapGrid);
                    pOPixels[(liPixel * piSize) + liAt] = fBlend(pOPixels[(liPixel * piSize) + liAt], msMapGrid);
                }
            }
        }

        /// <summary>The grid's colour over a pixel, in double precision as in fTileColour.</summary>
        private static Rgba fBlend(Rgba pOBase, Rgba pOOver)
        {
            double ldAlpha = pOOver.A / 255f;

            return new Rgba(
                (byte)((pOBase.R * (1.0 - ldAlpha)) + (pOOver.R * ldAlpha)),
                (byte)((pOBase.G * (1.0 - ldAlpha)) + (pOOver.G * ldAlpha)),
                (byte)((pOBase.B * (1.0 - ldAlpha)) + (pOOver.B * ldAlpha)),
                255);
        }

        private static byte[] fToBytes(Rgba[] pOPixels)
        {
            byte[] lyBytes = new byte[pOPixels.Length * 4];

            for (int liAt = 0; liAt < pOPixels.Length; liAt++)
            {
                lyBytes[(liAt * 4) + 0] = pOPixels[liAt].R;
                lyBytes[(liAt * 4) + 1] = pOPixels[liAt].G;
                lyBytes[(liAt * 4) + 2] = pOPixels[liAt].B;
                lyBytes[(liAt * 4) + 3] = pOPixels[liAt].A;
            }

            return lyBytes;
        }
    }
}
