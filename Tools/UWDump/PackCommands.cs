using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE PICTURE PACK TOOLS (Docs/WIDESCREEN-WEAPONS-PLAN.md section 5, step 4, 2026-11-09): weapon-guide shows
    /// where each weapon picture lies on a wide screen, pack-strip turns whole painted pictures into a pack of
    /// only the new pixels, pack-check reports what the game would make of a pack. All from the player's own
    /// installation; nothing of the original goes into a pack.
    /// </summary>
    public static partial class Program
    {
        /// <summary>The modern scheme's weapon view (UWModernWeaponView): the original's 172 x 112 view, its height
        /// 0.8 of the screen's, its pixels 1/1.2 as wide as high.</summary>
        private const int GuideViewWidth = 172;

        private const int GuideViewHeight = 112;

        private const double GuideWeaponScale = 0.8;

        private const double GuidePixelAspect = 1.0 / 1.2;

        private static readonly string[] msGuideWeapons = { "1 Sword", "2 Axe", "3 Mace", "4 Fist" };

        private static readonly string[] msGuideAttacks = { "slash", "bash", "thrust" };

        /// <summary>A picture's move as weapon-frames names it: "05 slash swing 1".</summary>
        private static string fMoveName(int piAt)
        {
            return string.Format("{0:00} {1}", piAt + 1, piAt == 27 ? "ready"
                : msGuideAttacks[piAt / 9] + (piAt % 9 < 4 ? " wind-up " + ((piAt % 9) + 1) : " swing " + ((piAt % 9) - 3)));
        }

        /// <summary>Half a screen's width in the pictures' pixels, for an aspect ratio, at the modern scheme's scale.</summary>
        private static int fHalfScreen(double pdAspect)
        {
            return (int)Math.Round(pdAspect * (GuideViewHeight / GuideWeaponScale) / GuidePixelAspect / 2.0);
        }

        /// <summary>
        /// A CUT EDGE (weapon-guide, pack-check): the picture's border on the original view's border with opaque pixels
        /// on it - there the original ends where the weapon or the arm goes on. A border inside the view is only the
        /// picture's tight crop, and the view's bottom is the screen's own in the modern scheme. piX, piY: the picture's
        /// place in the view (WEAPONS.DAT: X from the view's left, Y its top above the view's bottom).
        /// </summary>
        /// <summary>How close to the view's border a picture's edge counts as cut: one pixel - the sword's raised blade
        /// (WEAPONS_0E, 112 rows) starts one row below the view's top and ends flat there (per user, 2026-11-09).</summary>
        private const int GuideCutReach = 1;

        private static (bool Left, bool Right, bool Top) fCutEdges(UWTexture pOPicture, WeaponCoordinate pOPlace)
        {
            UWColor32[] lyPicture = pOPicture.GetUWColor32();
            int liTop = GuideViewHeight - pOPlace.Y;
            bool lbLeft = false, lbRight = false, lbTop = false;

            for (int y = 0; y < pOPicture.Height; y++)
            {
                lbLeft |= pOPlace.X <= GuideCutReach && lyPicture[y * pOPicture.Width].A != 0;
                lbRight |= pOPlace.X + pOPicture.Width >= GuideViewWidth - GuideCutReach && lyPicture[(y * pOPicture.Width) + pOPicture.Width - 1].A != 0;
            }

            for (int x = 0; x < pOPicture.Width; x++)
                lbTop |= liTop <= GuideCutReach && lyPicture[x].A != 0;

            return (lbLeft, lbRight, lbTop);
        }

        // ------------------------------------------------- weapon-guide

        /// <summary>The guides' labels: a tiny pixel font of its own (five rows, the letters the labels need).</summary>
        private static readonly Dictionary<char, string[]> msGuideGlyphs = new Dictionary<char, string[]>
        {
            ['1'] = new[] { ".#.", "##.", ".#.", ".#.", "###" },
            ['2'] = new[] { "##.", "..#", ".#.", "#..", "###" },
            ['3'] = new[] { "##.", "..#", ".#.", "..#", "##." },
            ['4'] = new[] { "#.#", "#.#", "###", "..#", "..#" },
            ['6'] = new[] { ".##", "#..", "###", "#.#", "###" },
            ['9'] = new[] { "###", "#.#", "###", "..#", "##." },
            [':'] = new[] { ".", "#", ".", "#", "." },
            [' '] = new[] { ".", ".", ".", ".", "." },
            ['C'] = new[] { ".##", "#..", "#..", "#..", ".##" },
            ['E'] = new[] { "###", "#..", "##.", "#..", "###" },
            ['I'] = new[] { "###", ".#.", ".#.", ".#.", "###" },
            ['T'] = new[] { "###", ".#.", ".#.", ".#.", ".#." },
            ['U'] = new[] { "#.#", "#.#", "#.#", "#.#", "###" },
            ['V'] = new[] { "#.#", "#.#", "#.#", "#.#", ".#." },
            ['W'] = new[] { "#...#", "#...#", "#.#.#", "#.#.#", ".#.#." },
        };

        /// <summary>
        /// uwdump weapon-guide [path] --out dir [--scale n]: every right-hand weapon picture at its place on a 21:9
        /// screen as the modern scheme draws it (raised, no sway), one PNG each in weapon-frames' folders and names,
        /// with the original's view (white), a 4:3 screen (grey), a 16:9 screen (yellow) and the canvas as 21:9 (red), each
        /// named in its colour at the top;
        /// a picture's cut edges - opaque pixels where it meets the view's edge, where the weapon or the arm goes on -
        /// in magenta.
        /// edges.txt lists each picture's place, its cut edges and how many pixels reach each screen's edge.
        /// </summary>
        private static int fWeaponGuide(DumpContext pOContext, List<string> pOArgs)
        {
            string lsOut = fTakeOption(pOArgs, "--out");
            string lsScale = fTakeOption(pOArgs, "--scale");
            int liScale = 2;

            if (lsOut == null || (lsScale != null && (!int.TryParse(lsScale, out liScale) || liScale < 1 || liScale > 8)))
            {
                Console.Error.WriteLine("weapon-guide --out dir [--scale 1-8]");

                return 1;
            }

            if (pOContext.Game.Game != UWGame.Uw1)
            {
                Console.Error.WriteLine("weapon-guide knows the first game's weapons only.");

                return 2;
            }

            List<WeaponCoordinate> lOPlaces = UWWeaponAnimations.ReadUw1Coordinates(pOContext.DataPath);
            int liHalf43 = fHalfScreen(4.0 / 3.0), liHalf169 = fHalfScreen(16.0 / 9.0), liHalf219 = fHalfScreen(21.0 / 9.0);
            int liWidth = liHalf219 * 2;
            int liHeight = (int)Math.Round(GuideViewHeight / GuideWeaponScale);
            int liCentre = liHalf219;
            int liViewLeft = liCentre - (GuideViewWidth / 2);
            int liViewTop = liHeight - GuideViewHeight;
            StringBuilder lOReport = new StringBuilder();

            lOReport.AppendLine("WEAPON PICTURES ON A WIDE SCREEN (uwdump weapon-guide)");
            lOReport.AppendLine();
            lOReport.AppendLine("The modern scheme draws the original's 172 x 112 view at 0.8 of the screen's height, its pixels 1/1.2 as wide");
            lOReport.AppendLine("as high. In the pictures' own pixels a screen is " + liHeight + " rows high and, from its middle to its edge,");
            lOReport.AppendLine("  4:3   " + liHalf43 + " pixels");
            lOReport.AppendLine("  16:9  " + liHalf169 + " pixels");
            lOReport.AppendLine("  21:9  " + liHalf219 + " pixels");
            lOReport.AppendLine("The original's view reaches " + (GuideViewWidth / 2) + " to each side; its top is " + liViewTop + " rows below the screen's top.");
            lOReport.AppendLine();
            lOReport.AppendLine("Per picture: its place on the screen (x from the left edge of a 21:9 screen, y from the top), its cut edges");
            lOReport.AppendLine("(opaque pixels where it meets the view's edge: there the original ends and the weapon or the arm goes on),");
            lOReport.AppendLine("and for a cut edge how many pixels it needs to reach the edge of a 16:9 and a 21:9 screen. The view's bottom");
            lOReport.AppendLine("is the screen's bottom, so nothing is cut there. Pictures without a cut edge need no extension.");
            lOReport.AppendLine();

            Directory.CreateDirectory(lsOut);

            int liWritten = 0, liCut = 0;

            for (int liRecord = 0; liRecord < 4; liRecord++)
            {
                string lsFolder = Path.Combine(lsOut, "Right hand", msGuideWeapons[liRecord]);

                Directory.CreateDirectory(lsFolder);
                lOReport.AppendLine(msGuideWeapons[liRecord].Substring(2).ToUpperInvariant());

                for (int liAt = 0; liAt < 28; liAt++)
                {
                    int liIndex = (liRecord * 28) + liAt;
                    UWTexture lOPicture = pOContext.Data.Textures.GetOriginalTextureByType(UWTexture.TextureTypes.WEAPONS, liIndex);

                    if (lOPicture == null || lOPicture.Width <= 1 || lOPicture.Height <= 1)
                        continue;

                    WeaponCoordinate lOPlace = lOPlaces[liIndex];
                    int liX = liViewLeft + lOPlace.X;
                    int liY = liViewTop + (GuideViewHeight - lOPlace.Y);
                    UWColor32[] lyPicture = lOPicture.GetUWColor32();
                    (bool lbLeft, bool lbRight, bool lbTop) = fCutEdges(lOPicture, lOPlace);

                    // The canvas: dark, the lines, the picture over them, its cut edges beside it.
                    byte[] lyRgba = new byte[liWidth * liHeight * 4];

                    void fPut(int x, int y, byte r, byte g, byte b)
                    {
                        if (x < 0 || y < 0 || x >= liWidth || y >= liHeight)
                            return;

                        int liAt4 = ((y * liWidth) + x) * 4;

                        lyRgba[liAt4] = r;
                        lyRgba[liAt4 + 1] = g;
                        lyRgba[liAt4 + 2] = b;
                        lyRgba[liAt4 + 3] = 255;
                    }

                    // A label in the pixel font, its top left at x, y.
                    void fText(int x, int y, string psText, byte r, byte g, byte b)
                    {
                        foreach (char lcChar in psText)
                        {
                            string[] lsRows = msGuideGlyphs[lcChar];

                            for (int liRow = 0; liRow < lsRows.Length; liRow++)
                            {
                                for (int liColumn = 0; liColumn < lsRows[liRow].Length; liColumn++)
                                {
                                    if (lsRows[liRow][liColumn] == '#')
                                        fPut(x + liColumn, y + liRow, r, g, b);
                                }
                            }

                            x += lsRows[0].Length + 1;
                        }
                    }

                    for (int y = 0; y < liHeight; y++)
                    {
                        for (int x = 0; x < liWidth; x++)
                            fPut(x, y, 28, 28, 32);
                    }

                    // The legend: each line's name in its colour, beside its left line, above the view.
                    fText(3, 3, "21:9", 200, 60, 60);
                    fText(liCentre - liHalf169 + 3, 3, "16:9", 220, 200, 40);
                    fText(liCentre - liHalf43 + 3, 3, "4:3", 150, 150, 150);
                    fText(liViewLeft + 3, liViewTop - 7, "VIEW", 240, 240, 240);

                    for (int y = 0; y < liHeight; y++)
                    {
                        fPut(liCentre - liHalf43, y, 110, 110, 110);
                        fPut(liCentre + liHalf43 - 1, y, 110, 110, 110);
                        fPut(liCentre - liHalf169, y, 220, 200, 40);
                        fPut(liCentre + liHalf169 - 1, y, 220, 200, 40);
                        fPut(0, y, 200, 60, 60);
                        fPut(liWidth - 1, y, 200, 60, 60);
                    }

                    for (int y = liViewTop; y < liHeight; y++)
                    {
                        fPut(liViewLeft, y, 240, 240, 240);
                        fPut(liViewLeft + GuideViewWidth - 1, y, 240, 240, 240);
                    }

                    for (int x = liViewLeft; x < liViewLeft + GuideViewWidth; x++)
                        fPut(x, liViewTop, 240, 240, 240);

                    // "CUT" beside the cut edge it names: inside the view at its bottom where the picture leaves room,
                    // else just outside the view's edge (a top edge: at the view's top over the picture).
                    // Free: no pixel of the weapon under the label's eleven by five.
                    bool fFree(int x, int y)
                    {
                        for (int liRow = y; liRow < y + 5; liRow++)
                        {
                            for (int liColumn = x - 1; liColumn < x + 12; liColumn++)
                            {
                                int liPx = liColumn - liX, liPy = liRow - liY;

                                if (liPx >= 0 && liPy >= 0 && liPx < lOPicture.Width && liPy < lOPicture.Height
                                    && lyPicture[(liPy * lOPicture.Width) + liPx].A != 0)
                                    return false;
                            }
                        }

                        return true;
                    }

                    if (lbLeft)
                        fText(fFree(liViewLeft + 3, liHeight - 8) ? liViewLeft + 3 : liViewLeft - 14, liHeight - 8, "CUT", 255, 0, 255);

                    if (lbRight)
                        fText(fFree(liViewLeft + GuideViewWidth - 14, liHeight - 8) ? liViewLeft + GuideViewWidth - 14 : liViewLeft + GuideViewWidth + 3,
                            liHeight - 8, "CUT", 255, 0, 255);

                    // A top edge: just below the view's top, right beside the weapon's top pixels (left of them where
                    // the weapon is in the way or the view ends).
                    if (lbTop)
                    {
                        int liFirst = lOPicture.Width, liLast = -1;

                        for (int x = 0; x < lOPicture.Width; x++)
                        {
                            if (lyPicture[x].A != 0)
                            {
                                liFirst = Math.Min(liFirst, x);
                                liLast = x;
                            }
                        }

                        int liRightOf = liX + liLast + 3, liLeftOf = liX + liFirst - 14;

                        fText(liRightOf + 11 <= liViewLeft + GuideViewWidth && fFree(liRightOf, liViewTop + 3) ? liRightOf : liLeftOf, liViewTop + 3, "CUT", 255, 0, 255);
                    }

                    for (int y = 0; y < lOPicture.Height; y++)
                    {
                        for (int x = 0; x < lOPicture.Width; x++)
                        {
                            UWColor32 lOColour = lyPicture[(y * lOPicture.Width) + x];

                            if (lOColour.A != 0)
                                fPut(liX + x, liY + y, lOColour.R, lOColour.G, lOColour.B);
                        }
                    }

                    for (int y = 0; y < lOPicture.Height; y++)
                    {
                        if (lbLeft && lyPicture[y * lOPicture.Width].A != 0)
                            fPut(liX - 1, liY + y, 255, 0, 255);

                        if (lbRight && lyPicture[(y * lOPicture.Width) + lOPicture.Width - 1].A != 0)
                            fPut(liX + lOPicture.Width, liY + y, 255, 0, 255);
                    }

                    for (int x = 0; x < lOPicture.Width; x++)
                    {
                        if (lbTop && lyPicture[x].A != 0)
                            fPut(liX + x, liY - 1, 255, 0, 255);
                    }

                    string lsMove = fMoveName(liAt);

                    PngWriter.Write(Path.Combine(lsFolder, string.Format("{0} - WEAPONS_{1:X2}.png", lsMove, liIndex)), liWidth * liScale, liHeight * liScale,
                        fScaleUp(lyRgba, liWidth, liHeight, liScale), false);
                    liWritten++;

                    List<string> lsCuts = new List<string>();

                    if (lbLeft)
                        lsCuts.Add("left (16:9 " + Math.Max(0, liX - (liCentre - liHalf169)) + ", 21:9 " + liX + ")");

                    if (lbRight)
                        lsCuts.Add("right (16:9 " + Math.Max(0, liCentre + liHalf169 - (liX + lOPicture.Width)) + ", 21:9 " + (liWidth - (liX + lOPicture.Width)) + ")");

                    if (lbTop)
                        lsCuts.Add("top (" + liY + " to the screen's top)");

                    if (lsCuts.Count > 0)
                        liCut++;

                    lOReport.AppendLine(string.Format("  WEAPONS_{0:X2}  {1,-20} {2,3} x {3,3} at {4,3},{5,3}  {6}", liIndex, lsMove.Substring(3), lOPicture.Width,
                        lOPicture.Height, liX, liY, lsCuts.Count > 0 ? "cut: " + string.Join(", ", lsCuts) : "whole"));
                }

                lOReport.AppendLine();
            }

            lOReport.AppendLine("The left hand (WEAPONS_70 to DF) mirrors the right; a pack that does not paint it gets the right hand's");
            lOReport.AppendLine("extension mirrored.");
            File.WriteAllText(Path.Combine(lsOut, "edges.txt"), lOReport.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine));
            Console.WriteLine(liWritten + " guides written to " + lsOut + " (" + liCut + " pictures with a cut edge), the list in edges.txt.");

            return 0;
        }

        private static byte[] fScaleUp(byte[] pyRgba, int piWidth, int piHeight, int piScale)
        {
            if (piScale == 1)
                return pyRgba;

            int liWidth = piWidth * piScale;
            byte[] lyOut = new byte[liWidth * piHeight * piScale * 4];

            for (int y = 0; y < piHeight * piScale; y++)
            {
                for (int x = 0; x < liWidth; x++)
                    Array.Copy(pyRgba, (((y / piScale) * piWidth) + (x / piScale)) * 4, lyOut, ((y * liWidth) + x) * 4, 4);
            }

            return lyOut;
        }

        // ------------------------------------------------- pack-strip

        private static readonly Regex msPictureName = new Regex(@"(?:^|[^0-9A-Za-z])WEAPONS_([0-9A-Fa-f]{1,3})\.png$|^([0-9A-Fa-f]{1,3})\.png$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// uwdump pack-strip [path] --from painted-dir --out pack-dir: whole painted weapon pictures in, a pack of only
        /// the new pixels out (decision 3). Each PNG under --from named with its picture (weapon-frames' "05 slash swing
        /// 1 - WEAPONS_04.png", pictures' "WEAPONS_04.png", or a pack's "004.png"); the original is found in it by its
        /// pixels, every pixel where the original has one is taken out, the rest cut to size and written to
        /// pictures/UW1/WEAPONS.GR/ with its place in extend.json (merged with one already there). A mod.json to fill
        /// in is written where none is. Warns where the original is not found or was painted over.
        /// </summary>
        private static int fPackStrip(DumpContext pOContext, List<string> pOArgs)
        {
            string lsFrom = fTakeOption(pOArgs, "--from");
            string lsOut = fTakeOption(pOArgs, "--out");

            if (lsFrom == null || lsOut == null)
            {
                Console.Error.WriteLine("pack-strip [game folder] --from painted-dir --out pack-dir");

                return 1;
            }

            if (pOContext.Game.Game != UWGame.Uw1)
            {
                Console.Error.WriteLine("pack-strip knows the first game's weapons only.");

                return 2;
            }

            if (!Directory.Exists(lsFrom))
            {
                Console.Error.WriteLine("No folder " + lsFrom);

                return 2;
            }

            string lsPictures = Path.Combine(lsOut, UWPictureExtensions.PicturesFolder, "UW1", "WEAPONS.GR");
            string lsExtend = Path.Combine(lsPictures, "extend.json");
            SortedDictionary<int, (int Left, int Top)> lOShifts = new SortedDictionary<int, (int Left, int Top)>();
            Dictionary<int, string> lOSeen = new Dictionary<int, string>();
            int liWritten = 0, liWarnings = 0, liUnchanged = 0;

            Directory.CreateDirectory(lsPictures);

            if (File.Exists(lsExtend))
            {
                UWPictureExtension.Shifts lOOld = UWPictureExtension.ParseShifts(File.ReadAllText(lsExtend), out string lsError);

                if (lOOld == null)
                {
                    Console.Error.WriteLine(lsExtend + ": " + lsError + " - fix or remove it first.");

                    return 2;
                }

                foreach (KeyValuePair<int, (int Left, int Top)> lOPair in lOOld.Pictures)
                    lOShifts[lOPair.Key] = lOPair.Value;
            }

            foreach (string lsFile in Directory.GetFiles(lsFrom, "*.png", SearchOption.AllDirectories).OrderBy(psFile => psFile, StringComparer.OrdinalIgnoreCase))
            {
                string lsName = Path.GetFileName(lsFile);
                Match lOMatch = msPictureName.Match(lsName);

                if (!lOMatch.Success)
                {
                    Console.WriteLine("  skipped " + lsName + ": no picture number in its name (WEAPONS_xx)");
                    continue;
                }

                int liIndex = int.Parse(lOMatch.Groups[1].Success ? lOMatch.Groups[1].Value : lOMatch.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                string lsShown = string.Format("WEAPONS_{0:X2} ({1})", liIndex, lsName);

                if (liIndex >= UWPictureExtensions.Uw1WeaponPictures)
                {
                    Console.WriteLine("  skipped " + lsShown + ": WEAPONS.GR has no such picture");
                    continue;
                }

                if (lOSeen.TryGetValue(liIndex, out string lsBefore))
                {
                    Console.WriteLine("  WARNING " + lsShown + " replaces " + lsBefore + " - two paintings of one picture");
                    liWarnings++;
                }

                lOSeen[liIndex] = lsName;

                UWTexture lOOriginal = pOContext.Data.Textures.GetOriginalTextureByType(UWTexture.TextureTypes.WEAPONS, liIndex);
                UWPngReader.Image lOImage = UWPngReader.Read(File.ReadAllBytes(lsFile), out string lsReadError);

                if (lOImage == null)
                {
                    Console.WriteLine("  WARNING " + lsShown + ": " + lsReadError);
                    liWarnings++;
                    continue;
                }

                if (!UWPictureExtension.TryFindOriginal(lOOriginal, lOImage.Pixels, lOImage.Width, lOImage.Height, out int liLeft, out int liTop, out int liChanged))
                {
                    Console.WriteLine("  WARNING " + lsShown + ": the original picture is not in it - is it the right number?");
                    liWarnings++;
                    continue;
                }

                if (liChanged > 0)
                {
                    Console.WriteLine("  WARNING " + lsShown + ": " + liChanged + " of the original's pixels painted over - the game keeps the original's there");
                    liWarnings++;
                }

                UWColor32[] lyNew = UWPictureExtension.Strip(lOOriginal, lOImage.Pixels, lOImage.Width, lOImage.Height, liLeft, liTop,
                    out int liNewWidth, out int liNewHeight, out int liNewLeft, out int liNewTop, out int liNewPixels);
                string lsTarget = Path.Combine(lsPictures, liIndex.ToString("X3") + ".png");

                // Nothing painted on it (most of weapon-frames' pictures need nothing): counted, not listed - but a
                // picture of an earlier run is taken out of the pack, with a line.
                if (lyNew == null)
                {
                    liUnchanged++;

                    if (File.Exists(lsTarget))
                    {
                        File.Delete(lsTarget);
                        Console.WriteLine("  " + lsShown + ": nothing new painted any more - taken out of the pack");
                    }

                    lOShifts.Remove(liIndex);
                    continue;
                }

                byte[] lyRgba = new byte[lyNew.Length * 4];

                for (int liAt = 0; liAt < lyNew.Length; liAt++)
                {
                    lyRgba[(liAt * 4) + 0] = lyNew[liAt].R;
                    lyRgba[(liAt * 4) + 1] = lyNew[liAt].G;
                    lyRgba[(liAt * 4) + 2] = lyNew[liAt].B;
                    lyRgba[(liAt * 4) + 3] = lyNew[liAt].A;
                }

                PngWriter.Write(lsTarget, liNewWidth, liNewHeight, lyRgba, false);
                lOShifts[liIndex] = (liNewLeft, liNewTop);
                liWritten++;
                Console.WriteLine(string.Format("  {0}: {1} new pixels, {2} x {3}, the original at {4},{5}", lsShown, liNewPixels, liNewWidth, liNewHeight,
                    liNewLeft, liNewTop));
            }

            StringBuilder lOJson = new StringBuilder();

            lOJson.Append("{\n  \"pictures\": {");

            int liEntry = 0;

            foreach (KeyValuePair<int, (int Left, int Top)> lOPair in lOShifts)
                lOJson.Append(liEntry++ == 0 ? "\n" : ",\n").Append("    \"").Append(lOPair.Key.ToString("X3")).Append("\": { \"left\": ")
                    .Append(lOPair.Value.Left).Append(", \"top\": ").Append(lOPair.Value.Top).Append(" }");

            lOJson.Append(liEntry > 0 ? "\n  }\n}\n" : "}\n}\n");
            File.WriteAllText(lsExtend, lOJson.ToString(), new UTF8Encoding(false));

            string lsManifest = Path.Combine(lsOut, UWModManifest.FileName);

            if (!File.Exists(lsManifest))
            {
                File.WriteAllText(lsManifest,
                    "{\n  \"id\": \"yourname.widescreen-weapons\",\n  \"name\": \"Widescreen Weapons\",\n  \"version\": \"1.0\",\n  \"author\": \"yourname\",\n"
                    + "  \"kind\": \"pictures\",\n  \"games\": [\"uw1\"],\n  \"credit\": \"Widescreen weapons by yourname\"\n}\n", new UTF8Encoding(false));
                Console.WriteLine("  wrote " + lsManifest + " - put your name and the mod's id in it");
            }

            Console.WriteLine(liWritten + " pictures written to " + lsPictures + (liUnchanged > 0 ? ", " + liUnchanged + " without anything new left out" : string.Empty)
                + ", " + liWarnings + " warnings. Check the pack with pack-check.");

            return 0;
        }

        // ------------------------------------------------- uwmod-check

        /// <summary>
        /// uwdump uwmod-check [path] --pack file.uwmod: whether an editor's package (the Underworld Editor's mod format 2)
        /// applies to this game as the port applies it (UWModGameFiles, into a temporary copy deleted afterwards):
        /// the files it changes, or why it is refused, and the start square of the result.
        /// </summary>
        private static int fUwmodCheck(DumpContext pOContext, List<string> pOArgs)
        {
            string lsPack = fTakeOption(pOArgs, "--pack");

            if (lsPack == null || !File.Exists(lsPack))
            {
                Console.Error.WriteLine("uwmod-check [game folder] --pack file.uwmod");

                return 1;
            }

            UWModSource lOSource = UWModSource.Zip(lsPack);
            string lsJson = lOSource?.ReadAllText(UWModManifest.FileName);
            UWModManifest lOManifest = lsJson != null ? UWModManifest.Parse(lsJson, out _) : null;

            if (lOManifest == null || !lOManifest.IsEditorPackage)
            {
                Console.Error.WriteLine(lsPack + " is no package of mod format 2 (a zip with a mod.json of \"format\": 2).");

                return 2;
            }

            string lsGame = pOContext.Game.Game == UWGame.Uw2 ? "uw2" : "uw1";
            string lsExtraction = Path.GetDirectoryName(pOContext.DataPath.TrimEnd('\\', '/'));
            string lsCopy = Path.Combine(Path.GetTempPath(), "uwdump-uwmod-" + Guid.NewGuid().ToString("N"));

            Console.WriteLine(lOManifest.Id + " " + (lOManifest.Version ?? "?") + " - " + lOManifest.Name + " (" + string.Join(", ", lOManifest.Games) + ")");

            try
            {
                UWModGameFiles lOFiles = UWModGameFiles.Build(lsExtraction, lsCopy,
                    new List<UWModCatalog.Entry> { new UWModCatalog.Entry { Source = lOSource, Manifest = lOManifest } }, lsGame);

                foreach (string lsRefused in lOFiles.Refused)
                    Console.WriteLine("  NOT APPLIED " + lsRefused);

                if (lOFiles.Problem != null)
                    Console.WriteLine("  PROBLEM " + lOFiles.Problem);

                if (lOFiles.Applied.Count == 0)
                    return 1;

                Console.WriteLine("  applies to this game; it changes " + string.Join(", ", lOFiles.Changed));

                string lsProgram = UWModGameFiles.FindFile(lOFiles.Root, pOContext.Game.Executable);

                if (lsProgram != null && UWStartSquare.TryRead(File.ReadAllBytes(lsProgram), out int liX, out int liY))
                    Console.WriteLine("  a new character starts at " + liX + "," + liY + " (the original's is " + UWStartSquare.OriginalX + "," + UWStartSquare.OriginalY + ")");

                return 0;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(lsCopy))
                        Directory.Delete(lsCopy, true);
                }
                catch (IOException)
                {
                }
            }
        }

        // ------------------------------------------------- pack-check

        /// <summary>
        /// uwdump pack-check [path] --pack dir-or-zip: what the game makes of a picture pack - its mod.json, the file
        /// names, each picture as the game composes it, colours outside the palette, pixels over the original's,
        /// half-clear pixels, a grAb that disagrees with extend.json, and which weapon pictures are painted. A report;
        /// nothing is changed. Exit code 1 where something keeps a picture or the pack from being used.
        /// </summary>
        private static int fPackCheck(DumpContext pOContext, List<string> pOArgs)
        {
            string lsPack = fTakeOption(pOArgs, "--pack");

            if (lsPack == null)
            {
                Console.Error.WriteLine("pack-check [game folder] --pack pack-dir-or-zip");

                return 1;
            }

            UWModSource lOSource = Directory.Exists(lsPack) ? UWModSource.Folder(lsPack) : File.Exists(lsPack) ? UWModSource.Zip(lsPack) : null;

            if (lOSource == null)
            {
                Console.Error.WriteLine("No pack folder or zip " + lsPack);

                return 2;
            }

            int liErrors = 0, liWarnings = 0;

            void fError(string psText)
            {
                Console.WriteLine("  ERROR   " + psText);
                liErrors++;
            }

            void fWarn(string psText)
            {
                Console.WriteLine("  WARNING " + psText);
                liWarnings++;
            }

            Console.WriteLine("mod.json");

            string lsJson = lOSource.ReadAllText(UWModManifest.FileName);
            string lsManifestError = null;
            UWModManifest lOManifest = lsJson != null ? UWModManifest.Parse(lsJson, out lsManifestError) : null;

            if (lsJson == null)
                fError("no mod.json - the game does not load the pack");
            else if (lOManifest == null)
                fError("mod.json: " + lsManifestError);
            else
            {
                if (!UWModManifest.IsValidId(lOManifest.Id))
                    fError("id \"" + lOManifest.Id + "\" is not valid: lower case letters, digits, - and ., like author.widescreen-weapons");

                if (!lOManifest.IsFor("uw1"))
                    fError("games does not name uw1 - the first game does not load it");

                if (lOManifest.Id != null && lOManifest.Id.StartsWith("yourname.", StringComparison.Ordinal))
                    fWarn("the id still starts with yourname. - put your own name in mod.json");

                if (string.IsNullOrEmpty(lOManifest.Credit))
                    fWarn("no credit - the line the game shows for the pack");

                if (!string.IsNullOrEmpty(lOManifest.Licence) && !lOSource.Has(lOManifest.Licence))
                    fWarn("licence names " + lOManifest.Licence + ", which is not in the pack");

                if (string.IsNullOrEmpty(lOManifest.Licence))
                    fWarn("no licence - say in mod.json's licence which file holds the terms players may use it under");

                Console.WriteLine("  " + lOManifest.Id + " " + (lOManifest.Version ?? "?") + " - " + (lOManifest.Name ?? "(no name)"));
            }

            if (lOManifest == null)
            {
                Console.WriteLine(liErrors + " errors, " + liWarnings + " warnings.");

                return 1;
            }

            UWModSet lOSet = new UWModSet(new List<UWModCatalog.Entry> { new UWModCatalog.Entry { Source = lOSource, Manifest = lOManifest } });
            UWPictureExtensions lOPictures = pOContext.Data.Textures.MakeExtensions(lOSet);
            string lsPrefix = UWPictureExtensions.PicturesFolder + "/UW1/";
            Dictionary<string, UWPictureExtension.Shifts> lOShiftsByFolder = new Dictionary<string, UWPictureExtension.Shifts>(StringComparer.OrdinalIgnoreCase);
            HashSet<int> lOPainted = new HashSet<int>();

            Console.WriteLine("pictures");

            foreach (string lsPath in lOSource.ListFiles().OrderBy(psPath => psPath, StringComparer.OrdinalIgnoreCase))
            {
                if (!lsPath.StartsWith(lsPrefix, StringComparison.OrdinalIgnoreCase) || !lsPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;

                string[] lsParts = lsPath.Substring(lsPrefix.Length).Split('/');

                if (lsParts.Length != 2 || !UWPictureExtensions.TryParseFile(lsParts[0], out UWTexture.TextureTypes leType)
                    || !UWPictureExtension.TryParseIndex(lsParts[1].Substring(0, lsParts[1].Length - 4), out int liIndex))
                    continue;

                string lsFolder = lsPrefix + lsParts[0];

                if (!lOShiftsByFolder.TryGetValue(lsFolder, out UWPictureExtension.Shifts lOShifts))
                {
                    string lsExtend = lOSource.ReadAllText(lsFolder + "/extend.json");

                    lOShifts = lsExtend == null ? UWPictureExtension.NoShifts : UWPictureExtension.ParseShifts(lsExtend, out _);
                    lOShiftsByFolder[lsFolder] = lOShifts;
                }

                UWPngReader.Image lOImage = UWPngReader.Read(lOSource.ReadAllBytes(lsPath), out string lsReadError);
                UWTexture lOOriginal = null;

                try
                {
                    lOOriginal = pOContext.Data.Textures.GetOriginalTextureByType(leType, liIndex);
                }
                catch (ArgumentOutOfRangeException)
                {
                }

                if (lOImage == null || lOOriginal == null || lOShifts == null)
                    continue;

                if (leType == UWTexture.TextureTypes.WEAPONS)
                    lOPainted.Add(liIndex);

                (int Left, int Top) lShift = lOShifts.For(liIndex, lOImage);
                string lsShown = lsParts[0] + "/" + lsParts[1];
                List<string> lsNotes = new List<string>();
                int liOutside = UWPictureExtension.CountOutsidePalette(lOOriginal, lOImage.Pixels);
                int liOverlap = UWPictureExtension.CountOverlap(lOOriginal, lOImage.Pixels, lOImage.Width, lOImage.Height, lShift.Left, lShift.Top);
                int liHalf = UWPictureExtension.CountHalfClear(lOImage.Pixels);

                if (liOutside > 0)
                    fWarn(lsShown + ": " + liOutside + " pixels in colours outside the palette - the game takes the nearest (uwdump palette)");

                if (liOverlap > 0)
                    fWarn(lsShown + ": " + liOverlap + " pixels over the original's - the game keeps the original's (pack-strip takes them out)");

                if (liHalf > 0)
                    fWarn(lsShown + ": " + liHalf + " half-clear pixels - below half drawn as none, the rest as whole");

                if (lOImage.HasGrab && lOShifts.Pictures.TryGetValue(liIndex, out (int Left, int Top) lEntry)
                    && (lEntry.Left != lOImage.GrabX || lEntry.Top != lOImage.GrabY))
                    fWarn(lsShown + ": its grAb says " + lOImage.GrabX + "," + lOImage.GrabY + ", extend.json " + lEntry.Left + "," + lEntry.Top
                        + " - extend.json wins");

                if (leType == UWTexture.TextureTypes.WEAPONS && liIndex >= UWPictureExtensions.Uw1LeftHand)
                    lsNotes.Add("left hand, painted itself");

                UWExtendedPicture lOMade = lOPictures.Get(leType, liIndex);

                if (lOMade != null)
                    Console.WriteLine(string.Format("  {0}: {1} x {2}, the original at {3},{4} - drawn {5} x {6}{7}", lsShown, lOImage.Width, lOImage.Height,
                        lShift.Left, lShift.Top, lOMade.Picture.Width, lOMade.Picture.Height, lsNotes.Count > 0 ? " (" + string.Join(", ", lsNotes) + ")" : string.Empty));
            }

            lOPictures.ComposeAll();

            foreach (string lsProblem in lOPictures.Problems)
                fError(lsProblem);

            foreach (KeyValuePair<string, UWPictureExtension.Shifts> lOPair in lOShiftsByFolder)
            {
                if (lOPair.Value == null)
                    fError(lOPair.Key + "/extend.json cannot be read - every picture of the folder is left out");
            }

            // How much of the right hand is painted (the left mirrors it): the pictures with a cut edge need it, the
            // whole ones may have it (a shield, a sleeve).
            List<WeaponCoordinate> lOPlaces = UWWeaponAnimations.ReadUw1Coordinates(pOContext.DataPath);
            List<string> lsMissing = new List<string>();
            int liCut = 0, liWholePainted = 0;

            for (int liIndex = 0; liIndex < UWPictureExtensions.Uw1LeftHand; liIndex++)
            {
                UWTexture lOOriginal = pOContext.Data.Textures.GetOriginalTextureByType(UWTexture.TextureTypes.WEAPONS, liIndex);

                if (lOOriginal == null || lOOriginal.Width <= 1 || lOOriginal.Height <= 1)
                    continue;

                (bool Left, bool Right, bool Top) lCuts = fCutEdges(lOOriginal, lOPlaces[liIndex]);

                if (!lCuts.Left && !lCuts.Right && !lCuts.Top)
                {
                    if (lOPainted.Contains(liIndex))
                        liWholePainted++;

                    continue;
                }

                liCut++;

                if (!lOPainted.Contains(liIndex))
                    lsMissing.Add(liIndex.ToString("X2"));
            }

            Console.WriteLine("weapons (right hand; weapon-guide's edges.txt shows the cut edges)");
            Console.WriteLine("  " + (liCut - lsMissing.Count) + " of the " + liCut + " pictures with a cut edge painted"
                + (lsMissing.Count > 0 ? "; not yet: " + string.Join(" ", lsMissing) : string.Empty));

            if (liWholePainted > 0)
                Console.WriteLine("  and " + liWholePainted + " pictures without one");
            Console.WriteLine(liErrors + " errors, " + liWarnings + " warnings.");

            return liErrors > 0 ? 1 : 0;
        }
    }
}
