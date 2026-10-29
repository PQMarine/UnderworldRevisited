using System.Collections.Generic;
using System.IO;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S LEV.ARK READ BY OUR READERS (Docs/UW2-PLAN.md phase 1 step 2, 2026-10-28):
    /// UWArk, UWLevArkFile.Uw2, UWLevelBlock, UWTextureMapUw2, UWTerrainUw2 and UWAutomapCode on the
    /// shipped file, held to what Underworld Exhumed measured on the same file (docs/FORMATS.md
    /// "LEV.ARK", "The automap and map notes", "TERRAIN.DAT"): 42 level and 42 texture blocks, every
    /// level block 0x7E08 long, every texture block 0x86; automaps of levels 1, 74 and 79 in blocks
    /// 160, 233 and 238, their notes in 240, 313 and 318 (14, 51 and 30 notes); stairs on ice on level
    /// 79's map; the floors' terrain words. Our own counts of the lists, overlays and timers are
    /// pinned as read, with their ranges checked.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2Levels(CheckContext pOContext)
        {
            Check.Group("The second game's levels in LEV.ARK (UW2 plan, phase 1 step 2)");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2 levels", "no second game here: skipped");

                return;
            }

            UWLevArkFile lOFile = UWGameProfiles.Uw2.LevArk;
            UWArk lOArk = UWArk.Read(Path.Combine(lsData, "LEV.ARK"), UWGameProfiles.Uw2.ArchiveLayout);
            UWTerrainUw2 lOTerrain = new UWTerrainUw2(File.ReadAllBytes(Path.Combine(lsData, "TERRAIN.DAT")));

            Check.Int("uw2 levels: LEV.ARK declares the game's blocks", lOArk.BlockCount, lOFile.DeclaredBlockCount);

            int liLevels = 0;
            int liTextureMaps = 0;
            int liOverlays = 0;
            int liTimers = 0;
            List<string> lOBad = new List<string>();
            SortedSet<int> lOFloorWords = new SortedSet<int>();

            for (int liLevel = 0; liLevel < lOFile.LevelCount; liLevel++)
            {
                byte[] lyMap = lOArk.ReadBlock(lOFile.MapBlock(liLevel));
                byte[] lyTextures = lOArk.ReadBlock(lOFile.TextureMapBlock(liLevel));

                if (lyTextures.Length > 0)
                {
                    liTextureMaps++;

                    if (lyTextures.Length != lOFile.TextureMapSize)
                        lOBad.Add("texture map " + (liLevel + 1) + " is " + lyTextures.Length);
                }

                if (lyMap.Length == 0)
                    continue;

                liLevels++;

                if (lyMap.Length != lOFile.BlockSize)
                {
                    lOBad.Add("level " + (liLevel + 1) + " is " + lyMap.Length);

                    continue;
                }

                UWLevelBlock lOBlock = UWLevelBlock.Read(lyMap, lOFile);

                if (!lOBlock.HasMagic)
                    lOBad.Add("level " + (liLevel + 1) + " has no magic word");

                foreach (int liSlot in lOBlock.MobileFreeList)
                {
                    if (liSlot < UWLevArkLayout.FirstFreeMobileSlot || liSlot >= UWLevArkLayout.MobileCount)
                        lOBad.Add("level " + (liLevel + 1) + " frees mobile " + liSlot);
                }

                foreach (int liSlot in lOBlock.StaticFreeList)
                {
                    if (liSlot < UWLevArkLayout.MobileCount || liSlot >= UWLevArkLayout.ObjectCount)
                        lOBad.Add("level " + (liLevel + 1) + " frees static " + liSlot);
                }

                UWAnimationOverlay lOOverlays = lOBlock.Overlays;

                liOverlays += lOOverlays.Entries.Count;
                liTimers += lOBlock.Timers.Count;

                foreach (UWAnimationOverlay.Entry lOEntry in lOOverlays.Entries)
                {
                    if (lOEntry.ObjectIndex >= UWLevArkLayout.ObjectCount || lOEntry.TileX > 63 || lOEntry.TileY > 63)
                        lOBad.Add("level " + (liLevel + 1) + " overlay of object " + lOEntry.ObjectIndex);
                }

                // The floors' terrain: a floor index names an entry of the texture map (TxmID), the entry a
                // T64.TR texture, the texture a TERRAIN.DAT word.
                UWTextureMapUw2 lOMap = UWTextureMapUw2.Read(lyTextures);

                for (int liTile = 0; lOMap != null && liTile < UWLevArkLayout.TileCount; liTile++)
                {
                    if (lOBlock.TileType(liTile) == 0)
                        continue;

                    int liWord = lOTerrain.Word(lOMap.Textures[lOBlock.FloorTexture(liTile)]);

                    if (liWord != 0)
                        lOFloorWords.Add(liWord);
                }
            }

            Check.Str("uw2 levels: level and texture blocks (UE measured 42 and 42)", liLevels + " " + liTextureMaps, "42 42");
            Check.Str("uw2 levels: every level block 0x7E08 with the magic word, every texture map 0x86, the free lists in range",
                string.Join("; ", lOBad), string.Empty);
            Check.Str("uw2 levels: animation overlays and timers over all levels (as read)", liOverlays + " " + liTimers, "169 109");

            // UE's list is of the class and flow bits (3-7); a floor with only a mark in bits 0-2 is listed apart:
            // 0x05, a "blackness" texture (bits 0-2 = 5) on a floor.
            List<string> lsClassWords = new List<string>();
            List<string> lsMarkWords = new List<string>();

            foreach (int liWord in lOFloorWords)
                ((liWord & 0xF8) != 0 ? lsClassWords : lsMarkWords).Add("0x" + liWord.ToString("X2"));

            Check.Str("uw2 levels: the floors' terrain words with class or flow bits (UE: 0x40, 0x48, 0x50, 0x58, 0x60, 0x80, 0xC0, 0xE8, 0xF8)",
                string.Join(" ", lsClassWords), "0x40 0x48 0x50 0x58 0x60 0x80 0xC0 0xE8 0xF8");
            Check.Str("uw2 levels: ... and with a mark only (as read)", string.Join(" ", lsMarkWords), "0x05");

            // The automaps and notes the shipped file has: levels 1, 74, 79.
            List<string> lsAutomaps = new List<string>();
            List<string> lsNotes = new List<string>();

            for (int liLevel = 0; liLevel < lOFile.LevelCount; liLevel++)
            {
                int liAutomapBlock = lOFile.AutomapBlock(liLevel);
                int liNoteBlock = lOFile.MapNoteBlock(liLevel);

                if (lOArk.ReadBlock(liAutomapBlock).Length > 0)
                    lsAutomaps.Add(liAutomapBlock + ":" + lOArk.ReadBlock(liAutomapBlock).Length);

                if (lOArk.ReadBlock(liNoteBlock).Length > 0)
                    lsNotes.Add(liNoteBlock + ":" + (lOArk.ReadBlock(liNoteBlock).Length / UWLevArkLayout.MapNoteRecordSize));
            }

            Check.Str("uw2 levels: automap blocks (block:length) of levels 1, 74, 79", string.Join(" ", lsAutomaps), "160:4096 233:4096 238:4096");
            Check.Str("uw2 levels: note blocks (block:notes), UE 14, 51 and 30", string.Join(" ", lsNotes), "240:14 313:51 318:30");

            byte[] lyAutomap79 = lOArk.ReadBlock(lOFile.AutomapBlock(78));
            bool lbStairsOnIce = false;

            foreach (byte lyByte in lyAutomap79)
            {
                if (UWAutomapCode.Kind(lyByte, UWGame.Uw2) == UWAutomapCode.KindStairs && UWAutomapCode.Terrain(lyByte, UWGame.Uw2) == UWTerrainUw2.ClassIce)
                    lbStairsOnIce = true;
            }

            Check.Bool("uw2 levels: level 79's automap has stairs on ice (UE: a byte 0xF_), read the second game's way", lbStairsOnIce, true);
            Check.Str("automap: one byte read each game's way (0x9B: type, kind, terrain)",
                UWAutomapCode.TileType(0x9B) + " " + UWAutomapCode.Kind(0x9B, UWGame.Uw1) + " " + UWAutomapCode.Terrain(0x9B, UWGame.Uw1) + " "
                + UWAutomapCode.Kind(0x9B, UWGame.Uw2) + " " + UWAutomapCode.Terrain(0x9B, UWGame.Uw2), "11 2 1 1 2");
        }
    }
}
