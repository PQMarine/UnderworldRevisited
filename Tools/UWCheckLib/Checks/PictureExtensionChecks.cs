using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE MODS' EXTENDED PICTURES (Docs/WIDESCREEN-WEAPONS-PLAN.md step 2, 2026-11-09) on a made-up pack around the
    /// player's own weapon pictures: the PNG reader (every row filter, four-bit indexed with tRNS, grAb, what it
    /// refuses), the shift's precedence (extend.json's entry, grAb, the default), the original winning where both
    /// have a pixel, the left hand mirrored from the right, a later mod winning, broken files reported, the
    /// originals untouched.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckPictureExtension(DataImport pOData)
        {
            Check.Group("Mods: extended pictures (UWPictureExtensions, UWPictureExtension, UWPngReader)");

            UWTextures lOTextures = pOData?.Textures;

            if (lOTextures == null || lOTextures.Game != UWGame.Uw1)
            {
                Check.Fail("extended pictures", "no textures of the first game");

                return;
            }

            UWTexture.TextureTypes leWeapons = UWTexture.TextureTypes.WEAPONS;
            UWTexture lO0 = lOTextures.GetOriginalTextureByType(leWeapons, 0);
            UWTexture lO1 = lOTextures.GetOriginalTextureByType(leWeapons, 1);
            UWTexture lO2 = lOTextures.GetOriginalTextureByType(leWeapons, 2);
            UWTexture lO70 = lOTextures.GetOriginalTextureByType(leWeapons, 0x70);
            string lsBefore = fIndicesHash(lO0) + " " + fIndicesHash(lO70);
            UWColor32[] lyPalette = lO0.PaletteColours();
            int liClear = UWPictureExtension.TransparentIndex(lO0);

            Check.Bool("the weapon pictures' palette has a transparent colour", liClear >= 0, true);

            // A colour of the palette no lower index shares, so it maps back to itself.
            int liColour = -1;

            for (int liAt = 1; liAt < lyPalette.Length && liColour < 0; liAt++)
            {
                if (lyPalette[liAt].A == 0)
                    continue;

                bool lbUnique = true;

                for (int liLower = 0; liLower < liAt && lbUnique; liLower++)
                    lbUnique = !(lyPalette[liLower].R == lyPalette[liAt].R && lyPalette[liLower].G == lyPalette[liAt].G && lyPalette[liLower].B == lyPalette[liAt].B);

                if (lbUnique)
                    liColour = liAt;
            }

            UWColor32 lOC = lyPalette[liColour];
            string lsRoot = Path.Combine(Path.GetTempPath(), "uwselfcheck-pictures-" + Guid.NewGuid().ToString("N"));

            try
            {
                string lsWide = Path.Combine(lsRoot, "wide");
                string lsGr = Path.Combine(lsWide, "pictures", "UW1", "WEAPONS.GR");

                Directory.CreateDirectory(lsGr);
                Directory.CreateDirectory(Path.Combine(lsWide, "pictures", "UW1", "NOPE.GR"));
                File.WriteAllText(Path.Combine(lsWide, UWModManifest.FileName),
                    "{ \"id\": \"t.wide\", \"version\": \"1.0\", \"games\": [\"uw1\"], \"credit\": \"Wide test\" }", new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(lsGr, "extend.json"),
                    "{ \"default\": { \"left\": 2, \"top\": 1 }, \"pictures\": { \"002\": { \"left\": 1, \"top\": 0 } } }", new UTF8Encoding(false));

                // 000: RGBA, every row filter, all one colour; the default shift.
                int liW0 = lO0.Width + 5, liH0 = lO0.Height + 1;
                byte[] ly000 = fTestPng(liW0, liH0, 6, 8, fFill(liW0 * liH0, lOC), null, null, false, 0, 0, false);

                File.WriteAllBytes(Path.Combine(lsGr, "000.png"), ly000);

                // 001: four-bit indexed, index 0 clear by tRNS, a grAb of 3, 0 - over the default.
                int liW1 = lO1.Width + 3, liH1 = lO1.Height;
                byte[] lyPacked = new byte[((liW1 + 1) / 2) * liH1];

                for (int y = 0; y < liH1; y++)
                {
                    for (int x = 0; x < 3; x++)
                        lyPacked[(y * ((liW1 + 1) / 2)) + (x / 2)] |= (byte)(1 << (x % 2 == 0 ? 4 : 0));
                }

                File.WriteAllBytes(Path.Combine(lsGr, "001.png"), fTestPng(liW1, liH1, 3, 4, null, lyPacked,
                    new byte[] { 0, 0, 0, lOC.R, lOC.G, lOC.B }, true, 3, 0, false));

                // 002: a grAb of 5, 5, but its own entry in extend.json (1, 0) wins.
                int liW2 = lO2.Width + 1, liH2 = lO2.Height;

                File.WriteAllBytes(Path.Combine(lsGr, "002.png"), fTestPng(liW2, liH2, 6, 8, fFill(liW2 * liH2, lOC), null, null, true, 5, 5, false));
                File.WriteAllBytes(Path.Combine(lsGr, "003.png"), Encoding.ASCII.GetBytes("no picture at all"));
                File.WriteAllBytes(Path.Combine(lsWide, "pictures", "UW1", "NOPE.GR", "000.png"), ly000);

                // A later mod (zip) with 001 of its own, clear but for one pixel, no shift.
                using (ZipArchive lOZip = ZipFile.Open(Path.Combine(lsRoot, "later.zip"), ZipArchiveMode.Create))
                {
                    fZipText(lOZip, UWModManifest.FileName, "{ \"id\": \"u.later\", \"games\": [\"uw1\"] }");

                    UWColor32[] lyOne = fFill(lO1.Width * lO1.Height, new UWColor32(0, 0, 0, 0));

                    lyOne[0] = lOC;

                    using (Stream lOEntry = lOZip.CreateEntry("pictures/UW1/WEAPONS.GR/001.png").Open())
                    {
                        byte[] lyBytes = fTestPng(lO1.Width, lO1.Height, 6, 8, lyOne, null, null, false, 0, 0, false);

                        lOEntry.Write(lyBytes, 0, lyBytes.Length);
                    }
                }

                // The reader.
                UWPngReader.Image lOImage = UWPngReader.Read(ly000, out string lsError);

                Check.Str("png: RGBA with every row filter read back", lOImage == null ? lsError
                    : lOImage.Width + "x" + lOImage.Height + " " + fSame(lOImage.Pixels, fFill(liW0 * liH0, lOC)), liW0 + "x" + liH0 + " True");

                UWPngReader.Image lO001 = UWPngReader.Read(File.ReadAllBytes(Path.Combine(lsGr, "001.png")), out lsError);

                Check.Str("png: four-bit indexed with tRNS and grAb", lO001 == null ? lsError
                    : lO001.Pixels[0].A + " " + lO001.Pixels[2].R + " " + lO001.Pixels[3].A + " grAb " + lO001.HasGrab + " " + lO001.GrabX + "," + lO001.GrabY,
                    "255 " + lOC.R + " 0 grAb True 3,0");
                UWPngReader.Read(fTestPng(4, 4, 6, 8, fFill(16, lOC), null, null, false, 0, 0, true), out lsError);
                Check.Str("png: interlaced refused with a hint", lsError, "interlaced - save it without interlacing");
                UWPngReader.Read(Encoding.ASCII.GetBytes("no picture at all, not even close"), out lsError);
                Check.Str("png: not a PNG", lsError, "not a PNG");

                // The set.
                UWModCatalog lOCatalog = UWModCatalog.Scan(lsRoot);
                UWPictureExtensions lOSet = lOTextures.MakeExtensions(lOCatalog.SetFor("uw1"));

                Check.Str("set: pictures counted, credits in order", lOSet.Count + " " + string.Join("|", lOSet.Credits), "4 Wide test|u.later");

                UWExtendedPicture lOX0 = lOSet.Get(leWeapons, 0);

                Check.Str("000: the default shift, the size covering both",
                    lOX0 == null ? string.Join(" | ", lOSet.Problems) : lOX0.Picture.Width + "x" + lOX0.Picture.Height + " at " + lOX0.Left + "," + lOX0.Top + " " + lOX0.ModId,
                    liW0 + "x" + liH0 + " at 2,1 t.wide");
                Check.Str("000: the original's pixels win, the pack fills the rest", fComposeErrors(lOX0, lO0, lOC, true), "0 errors");

                UWExtendedPicture lOX1 = lOSet.Get(leWeapons, 1);

                Check.Str("001: the later mod wins, with its own shift",
                    lOX1 == null ? string.Join(" | ", lOSet.Problems) : lOX1.ModId + " at " + lOX1.Left + "," + lOX1.Top, "u.later at 0,0");

                UWExtendedPicture lOX2 = lOSet.Get(leWeapons, 2);

                Check.Str("002: extend.json's entry over the PNG's grAb", lOX2 == null ? string.Join(" | ", lOSet.Problems) : lOX2.Left + "," + lOX2.Top, "1,0");

                UWExtendedPicture lOX70 = lOSet.Get(leWeapons, 0x70);

                Check.Str("left hand: 070 is 000's extension mirrored around the left-hand original",
                    lOX70 == null ? string.Join(" | ", lOSet.Problems)
                    : lOX70.Mirrored + " " + lOX70.ModId + " " + lOX70.Picture.Width + "x" + lOX70.Picture.Height + " at " + lOX70.Left + "," + lOX70.Top,
                    "True t.wide " + liW0 + "x" + liH0 + " at " + (liW0 - 2 - lO0.Width) + "," + 1);
                Check.Str("left hand: the original's pixels win there too", fComposeErrors(lOX70, lO70, lOC, true), "0 errors");
                Check.Str("left hand: 071 mirrors the later mod's 001, a picture the pack lacks entirely has none",
                    (lOSet.Get(leWeapons, 0x71)?.ModId ?? "none") + " " + lOSet.Has(leWeapons, 0x74) + " " + (lOSet.Get(leWeapons, 4) == null), "u.later False True");

                bool lbNo003 = lOSet.Get(leWeapons, 3) == null;
                string lsProblems = string.Join(" | ", lOSet.Problems);

                Check.Str("broken: 003 not used and reported, a file of no picture reported",
                    lbNo003 + " " + lsProblems.Contains("WEAPONS.GR/003.png - not a PNG") + " "
                    + lsProblems.Contains("NOPE.GR/000.png is no picture"), "True True True");
                Check.Str("all at once: three painted, three mirrored, 003 and its twin not; a folder mod read anew at each load",
                    lOSet.ComposeAll() + " " + lOSet.FromFolder, "6 True");
                Check.Str("the weapon frames carry their kind and number (the view asks by them)",
                    lO0.TextureType + " " + lO0.Index + " " + lO70.TextureType + " " + lO70.Index.ToString("X2"), "WEAPONS 0 WEAPONS 70");
                // pack-strip and pack-check: the extended 000 as a whole painted picture, found, stripped, composed again.
                UWColor32[] lyPainted = lOX0.Picture.GetUWColor32();
                int liPw = lOX0.Picture.Width, liPh = lOX0.Picture.Height;
                bool lbFound = UWPictureExtension.TryFindOriginal(lO0, lyPainted, liPw, liPh, out int liFoundLeft, out int liFoundTop, out int liChanged);
                UWColor32[] lyStripped = UWPictureExtension.Strip(lO0, lyPainted, liPw, liPh, liFoundLeft, liFoundTop,
                    out int liSw, out int liSh, out int liSl, out int liSt, out int liNew);
                UWExtendedPicture lOAgain = UWPictureExtension.Compose(lO0, lyStripped, liSw, liSh, liSl, liSt, out _);
                int liOpaque = 0;

                foreach (byte lyIndex in lO0.PaletteIndices)
                {
                    if (lyPalette[lyIndex].A != 0)
                        liOpaque++;
                }

                Check.Str("strip: the original found where it lies, unchanged", lbFound + " " + liFoundLeft + "," + liFoundTop + " " + liChanged, "True 2,1 0");
                Check.Str("strip: only the new pixels kept, composed again the same picture",
                    liNew + " " + (lOAgain != null && fIndicesEqual(lOAgain.Picture, lOX0.Picture) && lOAgain.Left == lOX0.Left && lOAgain.Top == lOX0.Top),
                    (liPw * liPh - liOpaque) + " True");

                for (int liAt = 0, liDone = 0; liAt < lO0.PaletteIndices.Length && liDone < 3; liAt++)
                {
                    if (lyPalette[lO0.PaletteIndices[liAt]].A == 0)
                        continue;

                    lyPainted[((liAt / lO0.Width + 1) * liPw) + (liAt % lO0.Width) + 2] = new UWColor32(1, 2, 3, 255);
                    liDone++;
                }

                UWPictureExtension.TryFindOriginal(lO0, lyPainted, liPw, liPh, out liFoundLeft, out liFoundTop, out liChanged);
                Check.Str("strip: three of the original's pixels painted over are counted", liFoundLeft + "," + liFoundTop + " " + liChanged, "2,1 3");

                UWColor32[] lyFull = fFill(liW0 * liH0, lOC);

                Check.Str("check: the palette's colour is none outside, another is; the pixels over the original counted; half-clear",
                    UWPictureExtension.CountOutsidePalette(lO0, lyFull) + " " + (UWPictureExtension.CountOutsidePalette(lO0, new[] { new UWColor32(1, 2, 3, 255) }) == 1)
                    + " " + (UWPictureExtension.CountOverlap(lO0, lyFull, liW0, liH0, 2, 1) == liOpaque)
                    + " " + UWPictureExtension.CountHalfClear(new[] { new UWColor32(1, 1, 1, 0), new UWColor32(1, 1, 1, 90), new UWColor32(1, 1, 1, 255) }),
                    "0 True True 1");
                Check.Str("the originals untouched", fIndicesHash(lO0) + " " + fIndicesHash(lO70), lsBefore);
                Check.Bool("the textures hand out none while no set is given", lOTextures.Extensions == null && lOTextures.GetExtendedByType(leWeapons, 0) == null, true);
            }
            finally
            {
                try
                {
                    Directory.Delete(lsRoot, true);
                }
                catch (IOException)
                {
                }
            }
        }

        /// <summary>Pixels of the extended picture that break the rule: where the original has a pixel it must be the
        /// original's index, elsewhere the pack's colour (pbFilled: the pack is filled with it wherever it is clear).</summary>
        private static string fComposeErrors(UWExtendedPicture pOMade, UWTexture pOOriginal, UWColor32 pOColour, bool pbFilled)
        {
            if (pOMade == null)
                return "none made";

            UWColor32[] lyPalette = pOOriginal.PaletteColours();
            int liErrors = 0;

            for (int y = 0; y < pOMade.Picture.Height; y++)
            {
                for (int x = 0; x < pOMade.Picture.Width; x++)
                {
                    byte lyGot = pOMade.Picture.PaletteIndices[(y * pOMade.Picture.Width) + x];
                    int liOx = x - pOMade.Left, liOy = y - pOMade.Top;
                    bool lbInside = liOx >= 0 && liOy >= 0 && liOx < pOOriginal.Width && liOy < pOOriginal.Height;
                    byte lyOriginal = lbInside ? pOOriginal.PaletteIndices[(liOy * pOOriginal.Width) + liOx] : (byte)0;

                    if (lbInside && lyPalette[lyOriginal].A != 0)
                    {
                        if (lyGot != lyOriginal)
                            liErrors++;
                    }
                    else if (pbFilled)
                    {
                        UWColor32 lOGot = lyPalette[lyGot];

                        if (lOGot.A == 0 || lOGot.R != pOColour.R || lOGot.G != pOColour.G || lOGot.B != pOColour.B)
                            liErrors++;
                    }
                }
            }

            return liErrors + " errors";
        }

        private static bool fIndicesEqual(UWTexture pOA, UWTexture pOB)
        {
            if (pOA.Width != pOB.Width || pOA.Height != pOB.Height || pOA.PaletteIndices.Length != pOB.PaletteIndices.Length)
                return false;

            for (int liAt = 0; liAt < pOA.PaletteIndices.Length; liAt++)
            {
                if (pOA.PaletteIndices[liAt] != pOB.PaletteIndices[liAt])
                    return false;
            }

            return true;
        }

        private static string fIndicesHash(UWTexture pOPicture)
        {
            uint liHash = 2166136261u;

            foreach (byte lyByte in pOPicture.PaletteIndices)
                liHash = (liHash ^ lyByte) * 16777619u;

            return pOPicture.Width + "x" + pOPicture.Height + ":" + liHash.ToString("X8");
        }

        private static UWColor32[] fFill(int piCount, UWColor32 pOColour)
        {
            UWColor32[] lyPixels = new UWColor32[piCount];

            for (int liAt = 0; liAt < piCount; liAt++)
                lyPixels[liAt] = pOColour;

            return lyPixels;
        }

        private static bool fSame(UWColor32[] pyA, UWColor32[] pyB)
        {
            if (pyA.Length != pyB.Length)
                return false;

            for (int liAt = 0; liAt < pyA.Length; liAt++)
            {
                if (pyA[liAt].R != pyB[liAt].R || pyA[liAt].G != pyB[liAt].G || pyA[liAt].B != pyB[liAt].B || pyA[liAt].A != pyB[liAt].A)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// A PNG for the checks: RGBA (colour type 6, from pyPixels) or packed rows as given (pyRows, e.g. four-bit
        /// indexed), each row with filter (row number mod 5) so every filter is read back; PLTE, tRNS (index 0 clear)
        /// and grAb where asked. The CRCs are left 0 - the reader does not check them.
        /// </summary>
        private static byte[] fTestPng(int piWidth, int piHeight, int piColourType, int piDepth, UWColor32[] pyPixels, byte[] pyRows,
            byte[] pyPalette, bool pbGrab, int piGrabX, int piGrabY, bool pbInterlaced)
        {
            int liStep;
            int liStride;
            byte[] lyRows;

            if (pyPixels != null)
            {
                liStep = 4;
                liStride = piWidth * 4;
                lyRows = new byte[liStride * piHeight];

                for (int liAt = 0; liAt < pyPixels.Length; liAt++)
                {
                    lyRows[(liAt * 4) + 0] = pyPixels[liAt].R;
                    lyRows[(liAt * 4) + 1] = pyPixels[liAt].G;
                    lyRows[(liAt * 4) + 2] = pyPixels[liAt].B;
                    lyRows[(liAt * 4) + 3] = pyPixels[liAt].A;
                }
            }
            else
            {
                liStep = 1;
                liStride = pyRows.Length / piHeight;
                lyRows = pyRows;
            }

            byte[] lyCompressed;

            using (MemoryStream lOBuffer = new MemoryStream())
            {
                using (ZLibStream lOZlib = new ZLibStream(lOBuffer, CompressionLevel.Optimal, true))
                {
                    for (int y = 0; y < piHeight; y++)
                    {
                        int liFilter = y % 5;

                        lOZlib.WriteByte((byte)liFilter);

                        for (int x = 0; x < liStride; x++)
                        {
                            int liRaw = lyRows[(y * liStride) + x];
                            int liLeft = x >= liStep ? lyRows[(y * liStride) + x - liStep] : 0;
                            int liUp = y > 0 ? lyRows[((y - 1) * liStride) + x] : 0;
                            int liCorner = y > 0 && x >= liStep ? lyRows[((y - 1) * liStride) + x - liStep] : 0;
                            int liPredicted = liFilter == 1 ? liLeft : liFilter == 2 ? liUp : liFilter == 3 ? (liLeft + liUp) >> 1
                                : liFilter == 4 ? UWPngReader.Paeth(liLeft, liUp, liCorner) : 0;

                            lOZlib.WriteByte((byte)(liRaw - liPredicted));
                        }
                    }
                }

                lyCompressed = lOBuffer.ToArray();
            }

            using (MemoryStream lOPng = new MemoryStream())
            {
                lOPng.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);

                byte[] lyHeader = new byte[13];

                fBigEndian(lyHeader, 0, piWidth);
                fBigEndian(lyHeader, 4, piHeight);
                lyHeader[8] = (byte)piDepth;
                lyHeader[9] = (byte)piColourType;
                lyHeader[12] = (byte)(pbInterlaced ? 1 : 0);
                fChunk(lOPng, "IHDR", lyHeader);

                if (pyPalette != null)
                {
                    fChunk(lOPng, "PLTE", pyPalette);
                    fChunk(lOPng, "tRNS", new byte[] { 0 });
                }

                if (pbGrab)
                {
                    byte[] lyGrab = new byte[8];

                    fBigEndian(lyGrab, 0, piGrabX);
                    fBigEndian(lyGrab, 4, piGrabY);
                    fChunk(lOPng, "grAb", lyGrab);
                }

                fChunk(lOPng, "IDAT", lyCompressed);
                fChunk(lOPng, "IEND", new byte[0]);

                return lOPng.ToArray();
            }
        }

        private static void fChunk(Stream pOStream, string psType, byte[] pyData)
        {
            byte[] lyLength = new byte[4];

            fBigEndian(lyLength, 0, pyData.Length);
            pOStream.Write(lyLength, 0, 4);
            pOStream.Write(Encoding.ASCII.GetBytes(psType), 0, 4);
            pOStream.Write(pyData, 0, pyData.Length);
            pOStream.Write(new byte[4], 0, 4);
        }

        private static void fBigEndian(byte[] pyData, int piAt, int piValue)
        {
            pyData[piAt] = (byte)(piValue >> 24);
            pyData[piAt + 1] = (byte)(piValue >> 16);
            pyData[piAt + 2] = (byte)(piValue >> 8);
            pyData[piAt + 3] = (byte)piValue;
        }
    }
}
