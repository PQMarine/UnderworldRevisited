using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class Program
    {
        /// <summary>
        /// pictures (UW2 plan phase 3 step 1, 2026-10-29): a picture file of the data folder as PNGs,
        /// one per picture, to measure the interface against - a .GR file by name (COMPASS.GR, OPTB.GR)
        /// in palette 0 or the one given with --palette, or with BYT the second game's BYT.ARK screens
        /// in their own palettes. Each PNG is named after the file and the picture's number in hex.
        /// </summary>
        private static int fDumpPictures(DumpContext pOContext, List<string> pOArgs)
        {
            string lsOut = fTakeOption(pOArgs, "--out");
            string lsPalette = fTakeOption(pOArgs, "--palette");

            if (pOArgs.Count == 0 || lsOut == null)
            {
                Console.Error.WriteLine("pictures <NAME.GR|BYT> --out dir [--palette n]");

                return 1;
            }

            string lsName = pOArgs[0].ToUpperInvariant();
            UWPalettes lOPalettes = new UWPalettes(pOContext.DataPath);
            int liPalette = lsPalette != null ? int.Parse(lsPalette) : 0;
            List<KeyValuePair<string, UWTexture>> lOPictures = new List<KeyValuePair<string, UWTexture>>();

            Directory.CreateDirectory(lsOut);

            // The first game's panels (PANELS.GR has no picture header) and its .BYT screens: as the game reads
            // them, in their own palettes (2026-11-08, for a language's own pictures - see picture-template).
            UWTexture.TextureTypes leGameType = default;
            bool lbFromGame = pOContext.Game.Game == UWGame.Uw1 && lsPalette == null
                && UWPicturePack.TryParse(Path.GetFileNameWithoutExtension(lsName) + (lsName.EndsWith(".BYT") ? ".png" : "_00.png"), out leGameType, out int _)
                && (leGameType == UWTexture.TextureTypes.PANELS || UWPicturePack.IsScreen(leGameType));

            if (lbFromGame)
            {
                List<UWTexture> lOList = pOContext.Data.Textures.GetTexturesByType(leGameType);

                for (int liAt = 0; lOList != null && liAt < lOList.Count; liAt++)
                {
                    UWTexture lOPicture = pOContext.Data.Textures.GetOriginalTextureByType(leGameType, liAt);

                    lOPictures.Add(new KeyValuePair<string, UWTexture>(Path.GetFileNameWithoutExtension(UWPicturePack.FileName(leGameType, liAt)), lOPicture));
                }
            }
            else if (lsName == "BYT")
            {
                UWTextures lOTextures = new UWTextures(pOContext.DataPath, lOPalettes, pOContext.Game.Game);

                foreach (KeyValuePair<UWScreensUw2.ScreenEnum, UWTexture.TextureTypes> lOPair in UWTextures.Uw2ScreenTypes)
                {
                    UWTexture lOScreen = lOTextures.GetTextureByType(lOPair.Value, 0);

                    if (lOScreen != null)
                        lOPictures.Add(new KeyValuePair<string, UWTexture>(string.Format("BYT_{0:X2}_{1}", (int)lOPair.Key, lOPair.Key), lOScreen));
                }
            }
            else
            {
                string lsFile = Path.Combine(pOContext.DataPath, lsName);

                if (!File.Exists(lsFile))
                {
                    Console.Error.WriteLine("No " + lsFile + ".");

                    return 2;
                }

                List<UWTexture> lOList = UWTextures.ReadImageFile(File.ReadAllBytes(lsFile), lsName, lOPalettes, liPalette, UWTexture.TextureTypes.OBJECTS);

                for (int liAt = 0; liAt < lOList.Count; liAt++)
                    lOPictures.Add(new KeyValuePair<string, UWTexture>(string.Format("{0}_{1:X2}", Path.GetFileNameWithoutExtension(lsName), liAt), lOList[liAt]));
            }

            foreach (KeyValuePair<string, UWTexture> lOPair in lOPictures)
            {
                UWTexture lOPicture = lOPair.Value;

                if (lOPicture == null || lOPicture.Width <= 0 || lOPicture.Height <= 0 || lOPicture.PaletteIndices == null)
                {
                    Console.WriteLine(lOPair.Key + ": empty");

                    continue;
                }

                UWColor32[] lOColours = lOPicture.GetUWColor32();
                byte[] lyRgba = new byte[lOPicture.Width * lOPicture.Height * 4];

                for (int liPixel = 0; liPixel < lOColours.Length && liPixel < lOPicture.Width * lOPicture.Height; liPixel++)
                {
                    lyRgba[(liPixel * 4) + 0] = lOColours[liPixel].R;
                    lyRgba[(liPixel * 4) + 1] = lOColours[liPixel].G;
                    lyRgba[(liPixel * 4) + 2] = lOColours[liPixel].B;
                    lyRgba[(liPixel * 4) + 3] = lOColours[liPixel].A;
                }

                PngWriter.Write(Path.Combine(lsOut, lOPair.Key + ".png"), lOPicture.Width, lOPicture.Height, lyRgba, false);
                Console.WriteLine(lOPair.Key + ": " + lOPicture.Width + " x " + lOPicture.Height);
            }

            return 0;
        }

        /// <summary>
        /// uwdump weapon-frames [path] --out dir [--both-hands] (GitHub issue 16, 2026-11-09: the painter of the
        /// widescreen weapons found the pictures' hex names sorted out of order by the file manager - Windows
        /// puts WEAPONS_0A before WEAPONS_01): the first game's WEAPONS.GR sorted as the game reads it
        /// (UWWeaponAnimations, UWWeaponAnimation) - a folder per weapon, the files numbered in the order of
        /// the moves (01 to 28: three attacks of 4 wind-up and 5 swing pictures, then the ready pose), each
        /// still named after its picture (WEAPONS_xx), which is what a pack names. Only the right hand: the
        /// left mirrors it (--both-hands writes it too). The 1x1 placeholders are left out.
        /// </summary>
        private static int fWeaponFrames(DumpContext pOContext, List<string> pOArgs)
        {
            string lsOut = fTakeOption(pOArgs, "--out");
            bool lbBoth = pOArgs.Remove("--both-hands");

            if (lsOut == null)
            {
                Console.Error.WriteLine("weapon-frames --out dir [--both-hands]");

                return 1;
            }

            if (pOContext.Game.Game != UWGame.Uw1)
            {
                Console.Error.WriteLine("weapon-frames knows the first game's WEAPONS.GR only.");

                return 2;
            }

            string lsFile = Path.Combine(pOContext.DataPath, "WEAPONS.GR");
            List<UWTexture> lOList = UWTextures.ReadImageFile(File.ReadAllBytes(lsFile), "WEAPONS.GR", new UWPalettes(pOContext.DataPath), 0,
                UWTexture.TextureTypes.WEAPONS);
            string[] lsWeapons = { "1 Sword", "2 Axe", "3 Mace", "4 Fist" };
            string[] lsAttacks = { "slash", "bash", "thrust" };
            int liWritten = 0;
            int liEmpty = 0;

            for (int liRecord = 0; liRecord < (lbBoth ? 8 : 4); liRecord++)
            {
                string lsHand = liRecord < 4 ? "Right hand" : "Left hand (mirrors the right)";
                string lsFolder = Path.Combine(lsOut, lsHand, lsWeapons[liRecord % 4]);

                Directory.CreateDirectory(lsFolder);

                for (int liAt = 0; liAt < 28; liAt++)
                {
                    int liIndex = (liRecord * 28) + liAt;
                    UWTexture lOPicture = liIndex < lOList.Count ? lOList[liIndex] : null;

                    if (lOPicture == null || lOPicture.Width <= 1 || lOPicture.Height <= 1 || lOPicture.PaletteIndices == null)
                    {
                        liEmpty++;
                        continue;
                    }

                    string lsMove = liAt == 27 ? "ready"
                        : lsAttacks[liAt / 9] + (liAt % 9 < 4 ? " wind-up " + ((liAt % 9) + 1) : " swing " + ((liAt % 9) - 3));
                    string lsName = string.Format("{0:00} {1} - WEAPONS_{2:X2}.png", liAt + 1, lsMove, liIndex);

                    fWriteRgbaPng(Path.Combine(lsFolder, lsName), lOPicture);
                    liWritten++;
                }
            }

            Console.WriteLine(liWritten + " pictures written to " + lsOut + ", " + liEmpty + " empty placeholders left out.");

            return 0;
        }

        private static void fWriteRgbaPng(string psPath, UWTexture pOPicture)
        {
            UWColor32[] lOColours = pOPicture.GetUWColor32();
            byte[] lyRgba = new byte[pOPicture.Width * pOPicture.Height * 4];

            for (int liPixel = 0; liPixel < lOColours.Length && liPixel < pOPicture.Width * pOPicture.Height; liPixel++)
            {
                lyRgba[(liPixel * 4) + 0] = lOColours[liPixel].R;
                lyRgba[(liPixel * 4) + 1] = lOColours[liPixel].G;
                lyRgba[(liPixel * 4) + 2] = lOColours[liPixel].B;
                lyRgba[(liPixel * 4) + 3] = lOColours[liPixel].A;
            }

            PngWriter.Write(psPath, pOPicture.Width, pOPicture.Height, lyRgba, false);
        }

        /// <summary>
        /// uwdump picture-template [path] --out dir: the pictures with English painted into them, as a language's
        /// own pictures are named (Languages/&lt;code&gt;/pictures, UWPicturePack): the options panel's buttons and
        /// headings, the stats page, the map, the main menu's captions and the congratulations at the end - from
        /// the player's own game, in the game's palettes. Paint over them, keep the size, and put those you
        /// changed into the language's pictures folder; the game takes the nearest palette colour for each
        /// pixel. The words the game writes anew from the texts (picture.*) need no picture.
        /// </summary>
        private static int fPictureTemplate(DumpContext pOContext, List<string> pOArgs)
        {
            string lsOut = fTakeOption(pOArgs, "--out");

            if (lsOut == null)
            {
                Console.Error.WriteLine("picture-template --out dir");

                return 1;
            }

            UWPictureWords lOWords = pOContext.Data.Textures.Words;

            if (lOWords == null)
            {
                Console.Error.WriteLine("No pictures to translate for this game.");

                return 2;
            }

            Directory.CreateDirectory(lsOut);

            int liCount = 0;

            foreach ((UWTexture.TextureTypes Type, int Index) lPicture in lOWords.TranslatablePictures)
            {
                UWTexture lOPicture = pOContext.Data.Textures.GetOriginalTextureByType(lPicture.Type, lPicture.Index);

                if (lOPicture == null || lOPicture.Width <= 0 || lOPicture.Height <= 0)
                    continue;

                UWColor32[] lOColours = lOPicture.GetUWColor32();
                byte[] lyRgba = new byte[lOPicture.Width * lOPicture.Height * 4];

                for (int liPixel = 0; liPixel < lOColours.Length && liPixel < lOPicture.Width * lOPicture.Height; liPixel++)
                {
                    lyRgba[(liPixel * 4) + 0] = lOColours[liPixel].R;
                    lyRgba[(liPixel * 4) + 1] = lOColours[liPixel].G;
                    lyRgba[(liPixel * 4) + 2] = lOColours[liPixel].B;
                    lyRgba[(liPixel * 4) + 3] = lOColours[liPixel].A;
                }

                PngWriter.Write(Path.Combine(lsOut, UWPicturePack.FileName(lPicture.Type, lPicture.Index)), lOPicture.Width, lOPicture.Height, lyRgba, false);
                liCount++;
            }

            Console.WriteLine(liCount + " pictures written to " + lsOut + " - from your own game, not to be published as they are; put the ones"
                + " you painted into Languages/<code>/" + UWPicturePack.FolderName);

            return 0;
        }

        /// <summary>The checks of a language's own pictures (validate-tsv): every file names a picture of the game,
        /// in its size. Warnings and errors counted into the caller's.</summary>
        private static void CheckPictureFolder(DumpContext pOContext, string psFolder, ref int piErrors, ref int piWarnings)
        {
            if (psFolder == null || !Directory.Exists(psFolder))
                return;

            int liChecked = 0;

            foreach (string lsFile in Directory.GetFiles(psFolder, "*.png"))
            {
                string lsName = Path.GetFileName(lsFile);

                if (!UWPicturePack.TryParse(lsName, out UWTexture.TextureTypes leType, out int liIndex))
                {
                    Console.WriteLine("warning: picture " + lsName + ": names no picture of the game (NAME_NN.png for a .GR picture, NAME.png for a .BYT screen) - not used");
                    piWarnings++;
                    continue;
                }

                UWTexture lOPicture = null;

                try
                {
                    lOPicture = pOContext.Data.Textures.GetOriginalTextureByType(leType, liIndex);
                }
                catch
                {
                    lOPicture = null;
                }

                if (lOPicture == null)
                {
                    Console.WriteLine("warning: picture " + lsName + ": the game has no such picture - not used");
                    piWarnings++;
                    continue;
                }

                if (!UWPicturePack.TryReadPngSize(File.ReadAllBytes(lsFile), out int liWidth, out int liHeight))
                {
                    Console.WriteLine("error: picture " + lsName + ": no PNG");
                    piErrors++;
                    continue;
                }

                liChecked++;

                if (liWidth != lOPicture.Width || liHeight != lOPicture.Height)
                {
                    Console.WriteLine("error: picture " + lsName + ": " + liWidth + " x " + liHeight + ", the original is " + lOPicture.Width + " x "
                        + lOPicture.Height + " - the game keeps the original");
                    piErrors++;
                }
            }

            Console.WriteLine("pictures of " + psFolder + ": " + liChecked + " checked");
        }
    }
}
