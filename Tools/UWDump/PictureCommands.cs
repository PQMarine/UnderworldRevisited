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

            if (lsName == "BYT")
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
    }
}
