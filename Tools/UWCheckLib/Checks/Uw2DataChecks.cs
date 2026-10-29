using System.Collections.Generic;
using System.IO;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S DATA THROUGH WHAT IS GENERIC ALREADY (Docs/UW2-PLAN.md phase 0 step 6,
    /// 2026-10-28): UW2's DATA folder, extracted from the same game.gog as the first game's
    /// (CheckContext.Uw2DataPath), read by the readers that know no game - the strings of
    /// STRINGS.PAK, the palettes, every .GR file's pictures (UWTextures.ReadImageFile) and the
    /// archives with their LZSS blocks (UWArk). What is not generic is said, not read: the
    /// critters' pages have a format of their own (AS.AN, CR.AN, PG.MP, CRxx.y beside UW1's
    /// ASSOC.ANM and CRxxPAGE.Nyy), and the first game's critter reader must find nothing there.
    /// Skipped without the second game (no image, or an image without it).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2Data(CheckContext pOContext)
        {
            Check.Group("The second game's data, read where the readers are generic (UW2 plan, phase 0 step 6)");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2", "no second game here (a DATA folder given directly, or an image without UW2): skipped");

                return;
            }

            UWStrings lOStrings = new UWStrings(File.ReadAllBytes(Path.Combine(lsData, "STRINGS.PAK")));
            int liStrings = 0;

            foreach (UWStringBlock lOBlock in lOStrings.Blocks.Values)
                liStrings += lOBlock.Strings.Count;

            Check.Str("uw2: STRINGS.PAK's blocks and strings", lOStrings.Blocks.Count + " " + liStrings, "136 13319");
            Check.Str("uw2: the first object names of block 4 (an object's name at its id + 1, as in the first game)",
                string.Join("|", lOStrings.Blocks[4].Strings.GetRange(0, 4)), "|a_hand axe|a_battle axe|an_axe");

            UWPalettes lOPalettes = new UWPalettes(lsData);
            int liPalettes = 0;

            while (fHasPalette(lOPalettes, liPalettes))
                liPalettes++;

            Check.Int("uw2: PALS.DAT's palettes (8448 bytes of 768)", liPalettes, 11);

            int liFiles = 0;
            int liPictures = 0;
            int liAbsent = 0;
            List<string> lOBad = new List<string>();

            // PANELS.GR is not generic: its pictures have no header (the first game reads its own with
            // fImportPanels, the second's are of another size) - phase 3 reads it.
            foreach (string lsFile in Directory.GetFiles(lsData, "*.GR"))
            {
                string lsName = Path.GetFileName(lsFile);

                if (lsName.Equals("PANELS.GR", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                byte[] lyFile = File.ReadAllBytes(lsFile);
                List<UWTexture> lOPictures = UWTextures.ReadImageFile(lyFile, lsName, lOPalettes, 0, UWTexture.TextureTypes.OBJECTS);

                liFiles++;

                for (int liAt = 0; liAt < lOPictures.Count; liAt++)
                {
                    // An entry whose offset is the file's end is absent (WEAP.GR's last 18, GENHEAD.GR's 4).
                    if (System.BitConverter.ToInt32(lyFile, 3 + (4 * liAt)) >= lyFile.Length)
                    {
                        liAbsent++;

                        continue;
                    }

                    UWTexture lOPicture = lOPictures[liAt];

                    liPictures++;

                    if (lOPicture.Width <= 0 || lOPicture.Height <= 0 || lOPicture.PaletteIndices == null
                        || lOPicture.PaletteIndices.Length < lOPicture.Width * lOPicture.Height)
                        lOBad.Add(lsName + "#" + liAt);
                }
            }

            Check.Str("uw2: every .GR file's pictures read but PANELS.GR's (files, pictures, absent entries)", liFiles + " " + liPictures + " " + liAbsent, "34 1717 28");
            Check.Str("uw2: ... each with a size and its pixels", string.Join(" ", lOBad), string.Empty);

            // LEV.ARK: 80 levels of four blocks (map, textures, automap, notes); CNV.ARK: 102 conversations
            // of 320 slots; BYT.ARK: 11 screens, 3 and 10 empty; SCD.ARK never compressed (FORMATS.md).
            Dictionary<string, string> lOExpected = new Dictionary<string, string>
            {
                { "LEV.ARK", "320 75 " }, { "CNV.ARK", "320 102 " }, { "BYT.ARK", "11 9 " }, { "SCD.ARK", "16 0 " }
            };

            foreach (string lsArchive in lOExpected.Keys)
            {
                UWArk lOArk = UWArk.Read(Path.Combine(lsData, lsArchive), UWArk.LayoutEnum.Uw2);
                int liCompressed = 0;
                List<string> lOWrong = new List<string>();

                for (int liBlock = 0; liBlock < lOArk.BlockCount; liBlock++)
                {
                    if (!lOArk.IsCompressed(liBlock))
                        continue;

                    byte[] lyBlock = lOArk.ReadBlock(liBlock);

                    if (lyBlock.Length == 0)
                        continue;

                    liCompressed++;

                    if (lyBlock.Length != lOArk.DeclaredLength(liBlock))
                        lOWrong.Add(liBlock + ":" + lyBlock.Length + "/" + lOArk.DeclaredLength(liBlock));
                }

                Check.Str("uw2: " + lsArchive + "'s blocks and compressed blocks, each decoding to its declared length",
                    lOArk.BlockCount + " " + liCompressed + " " + string.Join(" ", lOWrong), lOExpected[lsArchive]);
            }

            string lsCrit = Path.Combine(Path.GetDirectoryName(lsData), "CRIT");

            Check.Bool("uw2: the critters' own files are there (AS.AN, CR.AN, PG.MP), not the first game's ASSOC.ANM",
                File.Exists(Path.Combine(lsCrit, "AS.AN")) && File.Exists(Path.Combine(lsCrit, "CR.AN")) && File.Exists(Path.Combine(lsCrit, "PG.MP"))
                && !File.Exists(Path.Combine(lsCrit, "ASSOC.ANM")), true);
            Check.Bool("uw2: ... so the first game's critter reader finds nothing there (phase 1 reads them)", new UWCritterAnimations(lsData).IsLoaded, false);
        }

        private static bool fHasPalette(UWPalettes pOPalettes, int piIndex)
        {
            try
            {
                return pOPalettes.GetPalette(piIndex) != null;
            }
            catch (System.ArgumentOutOfRangeException)
            {
                return false;
            }
        }
    }
}
