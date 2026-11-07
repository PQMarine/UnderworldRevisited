using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class Program
    {
        /// <summary>
        /// palette (GitHub issue 16, 2026-11-07: an artist painting weapon frames asked for the game's palette to
        /// paint and palettize with): the palettes of PALS.DAT from the player's own game folder as a GIMP palette
        /// (.gpl, also read by Krita, Aseprite and others) and an Adobe Color Table (.act, 768 bytes, 256 times
        /// R G B). The colours are the ones the pictures command writes into its PNGs (UWPalette: the file's six
        /// bits shifted up by two), so a colour picked from a PNG is in the palette exactly. Index 0 is the
        /// pictures' transparent colour. Palette 0 unless a number or "all" is given; the files are named
        /// after the game and the palette's number.
        /// </summary>
        private static int fDumpPalette(DumpContext pOContext, List<string> pOArgs)
        {
            string lsOut = fTakeOption(pOArgs, "--out");

            if (lsOut == null)
            {
                Console.Error.WriteLine("palette [n|all] --out dir");

                return 1;
            }

            UWPalettes lOPalettes = new UWPalettes(pOContext.DataPath);
            int liCount = fPaletteCount(pOContext.DataPath);
            List<int> lOWanted = new List<int>();

            if (pOArgs.Count == 0)
                lOWanted.Add(0);
            else if (pOArgs[0].Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                for (int liAt = 0; liAt < liCount; liAt++)
                    lOWanted.Add(liAt);
            }
            else if (int.TryParse(pOArgs[0], out int liOne) && liOne >= 0 && liOne < liCount)
                lOWanted.Add(liOne);
            else
            {
                Console.Error.WriteLine("No palette " + pOArgs[0] + " - 0 to " + (liCount - 1) + " or all.");

                return 1;
            }

            Directory.CreateDirectory(lsOut);

            string lsGame = pOContext.Game.Game == UWGame.Uw2 ? "UW2" : "UW1";

            foreach (int liPalette in lOWanted)
            {
                string lsBase = Path.Combine(lsOut, lsGame + "_palette_" + liPalette);
                byte[] lyAct = new byte[256 * 3];
                StringBuilder lOGpl = new StringBuilder();

                lOGpl.Append("GIMP Palette\n");
                lOGpl.Append("Name: Ultima Underworld " + (lsGame == "UW2" ? "II " : string.Empty) + "palette " + liPalette + "\n");
                lOGpl.Append("Columns: 16\n");
                lOGpl.Append("# From PALS.DAT by uwdump (Underworld Revisited); index 0 is the pictures' transparent colour.\n");

                for (int liIndex = 0; liIndex < 256; liIndex++)
                {
                    byte[] lyRgb = lOPalettes.GetMainPaletteRGB(liPalette, liIndex);

                    lyAct[(liIndex * 3) + 0] = lyRgb[0];
                    lyAct[(liIndex * 3) + 1] = lyRgb[1];
                    lyAct[(liIndex * 3) + 2] = lyRgb[2];
                    lOGpl.AppendFormat("{0,3} {1,3} {2,3}\tIndex {3}\n", lyRgb[0], lyRgb[1], lyRgb[2], liIndex);
                }

                File.WriteAllText(lsBase + ".gpl", lOGpl.ToString(), new UTF8Encoding(false));
                File.WriteAllBytes(lsBase + ".act", lyAct);
                Console.WriteLine(lsBase + ".gpl, .act");
            }

            return 0;
        }

        /// <summary>How many palettes PALS.DAT holds, 768 bytes each.</summary>
        private static int fPaletteCount(string psDataPath)
        {
            return (int)(new FileInfo(Path.Combine(psDataPath, "PALS.DAT")).Length / 768);
        }
    }
}
