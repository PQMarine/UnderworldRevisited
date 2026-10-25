using System;
using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE HUD ART BUILDERS' COMPOSE CORES ON SYNTHETIC PICTURES (cleanup 9.12, 2026-10-25), no
        /// game data: every builder under UWData/HudArt composes from a made-up frame picture (a hashed
        /// pattern without black, its view's hole black), and the checks hold what the builders promise
        /// whatever the pictures - the sizes, the alpha (opaque, or keyed only where the masks say), the
        /// outlines black and only around what is there, the same bytes twice, the inputs untouched -
        /// plus the shared helpers (UWMask, UWMaskText, UWPixelOps, UWSeededRandom, UWNoise) and the
        /// hand-made masks against their places. The pictures of the real data are pinned by the HudArt
        /// group (Pins/hud-art-hashes.txt).
        /// </summary>
        private static void fCheckHudArtCompose()
        {
            Check.Group("HUD art builders on synthetic pictures (the Compose cores and the pixel helpers)");

            fCheckArtHelpers();
            fCheckArtMasks();

            UWPicture lOMain = fArtPattern(UWClassicLayout.Screen.Width, UWClassicLayout.Screen.Height, 1);

            UWPixelOps.Fill(lOMain, UWClassicLayout.ClassicWide.Hole, UWPixelOps.Transparent);

            uint luMainBefore = fCrc32(lOMain);

            fCheckArtLeather();
            fCheckArtSlotAndPage(lOMain);
            fCheckArtPageFreer(lOMain);
            fCheckArtShelves(lOMain);
            fCheckArtClassicWide(lOMain);
            fCheckArtConversation(lOMain);
            fCheckArtCompass(lOMain);
            fCheckArtScrollPaper(lOMain);

            Check.Bool("compose: the frame picture given is not changed", fCrc32(lOMain) == luMainBefore, true);
        }

        /// <summary>A made-up picture: every pixel's colour hashed from its place, opaque, never near
        /// black (each channel 20 or more).</summary>
        private static UWPicture fArtPattern(int piWidth, int piHeight, int piSeed)
        {
            UWPicture lOPicture = new UWPicture(piWidth, piHeight);

            for (int y = 0; y < piHeight; y++)
            {
                for (int x = 0; x < piWidth; x++)
                {
                    uint luHash = unchecked((uint)((x * 73856093) ^ (y * 19349663) ^ (piSeed * 83492791)));

                    luHash ^= luHash >> 13;
                    luHash = unchecked(luHash * 0x5bd1e995u);
                    luHash ^= luHash >> 15;

                    lOPicture.Set(x, y, new UWColor32((byte)(40 + (luHash & 0x7F)), (byte)(30 + ((luHash >> 8) & 0x7F)),
                        (byte)(20 + ((luHash >> 16) & 0x7F)), 255));
                }
            }

            return lOPicture;
        }

        /// <summary>Whether every pixel's alpha is 0 or 255.</summary>
        private static bool fArtAlphaKeyed(UWPicture pOPicture)
        {
            foreach (UWColor32 lOColour in pOPicture.Pixels)
            {
                if (lOColour.A != 0 && lOColour.A != 255)
                    return false;
            }

            return true;
        }

        private static int fArtVisible(UWPicture pOPicture)
        {
            int liCount = 0;

            foreach (UWColor32 lOColour in pOPicture.Pixels)
                liCount += lOColour.A > 0 ? 1 : 0;

            return liCount;
        }

        private static string fArtSize(UWPicture pOPicture)
        {
            return pOPicture == null ? "null" : pOPicture.Width + "x" + pOPicture.Height;
        }

        /// <summary>Whether every pixel of pOAfter that differs from the padded pOBefore (one pixel of
        /// room all round) is opaque black - an outline adds nothing else.</summary>
        private static bool fArtOnlyBlackAdded(UWPicture pOBefore, UWPicture pOAfter, int piLeft, int piTop)
        {
            for (int y = 0; y < pOAfter.Height; y++)
            {
                for (int x = 0; x < pOAfter.Width; x++)
                {
                    int liX = x - piLeft;
                    int liY = y - piTop;
                    UWColor32 lOWas = liX >= 0 && liY >= 0 && liX < pOBefore.Width && liY < pOBefore.Height ? pOBefore.Get(liX, liY) : UWPixelOps.Transparent;
                    UWColor32 lONow = pOAfter.Get(x, y);

                    if (lOWas.A == 0 && lONow.A == 0)
                        continue;

                    if (!UWPixelRules.Same(lOWas, lONow) && !UWPixelRules.Same(lONow, UWPixelOps.Black))
                        return false;
                }
            }

            return true;
        }

        // ------------------------------------------------- The helpers

        private static void fCheckArtHelpers()
        {
            UWSeededRandom lORandom = new UWSeededRandom(1u);

            Check.Seq("helpers: UWSeededRandom is xorshift32 (13, 17, 5)", new[] { (int)lORandom.Next(), (int)lORandom.Next(), (int)lORandom.Next() },
                new[] { 270369, 67634689, unchecked((int)2647435461u) });
            Check.Int("helpers: UWSeededRandom.Below is the remainder", new UWSeededRandom(1u).Below(1000), 270369 % 1000);

            bool lbNoise = true;

            for (int liAt = 0; liAt < 500; liAt++)
            {
                float lfHash = UWNoise.Hash(liAt * 7, liAt * 13 - 900, liAt % 5);
                float lfClouds = UWNoise.Clouds(liAt * 11, liAt * 3, 0);

                lbNoise &= lfHash >= 0f && lfHash < 1f && lfClouds >= 0f && lfClouds <= 1f
                    && lfHash == UWNoise.Hash(liAt * 7, liAt * 13 - 900, liAt % 5) && lfClouds == UWNoise.Clouds(liAt * 11, liAt * 3, 0);
            }

            Check.Bool("helpers: UWNoise in 0 to 1 and the same for the same point", lbNoise, true);

            // Costs 5 7 5 2 9 2 offered as candidates 0 to 5: the cheapest three, of equal costs the
            // one found first ahead; the pick one of them by the sequence.
            long[] llBest = new long[UWTextureSynth.Best];
            int[] liBest = new int[UWTextureSynth.Best];
            int liFound = 0;
            long[] llOffered = { 5, 7, 5, 2, 9, 2 };

            for (int liAt = 0; liAt < llOffered.Length; liAt++)
                UWTextureSynth.PickBest3(llBest, liBest, ref liFound, llOffered[liAt], liAt);

            Check.Seq("helpers: UWTextureSynth.PickBest3 keeps the three cheapest, ties in the order found",
                new[] { liBest[0], liBest[1], liBest[2], liFound }, new[] { 3, 5, 0, 6 });
            Check.Int("helpers: UWTextureSynth.PickOne by the sequence", UWTextureSynth.PickOne(liBest, liFound, new UWSeededRandom(1u)),
                liBest[270369 % 3]);

            // A pixel against itself costs nothing (all 24 neighbours compared); against its neighbour
            // something; with nothing known or nothing to draw from it is no match.
            UWPicture lOSynth = fArtPattern(7, 7, 9);
            UWMask lOAll = new UWMask(7, 7);
            UWMask lONone = new UWMask(7, 7);

            for (int liAt = 0; liAt < lOAll.Bits.Length; liAt++)
                lOAll.Bits[liAt] = true;

            bool lbSelf = UWTextureSynth.MatchCost(lOSynth, lOAll, 3, 3, 0, 7, lOSynth, lOAll, 0, 3, 3, 24, out long llSelf);
            bool lbOther = UWTextureSynth.MatchCost(lOSynth, lOAll, 3, 3, 0, 7, lOSynth, lOAll, 0, 4, 3, 4, out long llOther);

            Check.Bool("helpers: UWTextureSynth.MatchCost (itself 0, a neighbour more, nothing known or usable no match)",
                lbSelf && llSelf == 0 && lbOther && llOther > 0
                && !UWTextureSynth.MatchCost(lOSynth, lONone, 3, 3, 0, 7, lOSynth, lOAll, 0, 3, 3, 1, out _)
                && !UWTextureSynth.MatchCost(lOSynth, lOAll, 3, 3, 0, 7, lOSynth, lONone, 0, 3, 3, 1, out _), true);

            // A wall across a 7 x 5 mask: the flood from the left stays left of it.
            UWMask lOWall = new UWMask(7, 5);

            for (int y = 0; y < 5; y++)
                lOWall[3, y] = true;

            UWMask lOFlood = UWMask.Flood(7, 5, new[] { 0 }, (piX, piY) => !lOWall[piX, piY]);
            int liFlooded = 0;

            foreach (bool lbBit in lOFlood.Bits)
                liFlooded += lbBit ? 1 : 0;

            Check.Bool("helpers: UWMask.Flood stops at the wall (15 pixels, none right of it)", liFlooded == 15 && !lOFlood[4, 2] && lOFlood[2, 4], true);

            // Two pieces of 3 (first found wins) and one of 4 diagonally joined (8-connected only).
            UWMask lOPieces = new UWMask(6, 4);

            foreach ((int liX, int liY) in new[] { (0, 0), (1, 0), (2, 0), (5, 0), (5, 1), (5, 2), (0, 2), (1, 3), (2, 2), (3, 3) })
                lOPieces[liX, liY] = true;

            UWMask lOLargest4 = lOPieces.Largest(false);
            UWMask lOLargest8 = lOPieces.Largest(true);

            Check.Bool("helpers: UWMask.Largest - of equal pieces the first, 8-connected joins diagonals",
                lOLargest4[0, 0] && lOLargest4[2, 0] && !lOLargest4[5, 0] && lOLargest8[0, 2] && lOLargest8[3, 3] && !lOLargest8[0, 0], true);
            Check.Int("helpers: UWMask.CountAround", lOPieces.CountAround(1, 1), 5);

            UWMask lOFull = new UWMask(4, 4);

            for (int liAt = 0; liAt < lOFull.Bits.Length; liAt++)
                lOFull.Bits[liAt] = true;

            UWMask lOEroded = lOFull.Eroded();

            Check.Bool("helpers: UWMask.Eroded keeps the inside only", lOEroded[1, 1] && lOEroded[2, 2] && !lOEroded[0, 1] && !lOEroded[3, 3], true);

            Check.Bool("helpers: UWMaskText refuses a ragged row, a stray character, a wrong place",
                fArtThrows(() => new UWMaskText(new[] { "..", "." }, ".X", new UWRectInt(0, 0, 2, 2)))
                && fArtThrows(() => new UWMaskText(new[] { "..", ".Y" }, ".X", new UWRectInt(0, 0, 2, 2)))
                && fArtThrows(() => new UWMaskText(new[] { "..", ".X" }, ".X", new UWRectInt(0, 0, 3, 2))), true);

            UWMaskText lOText = new UWMaskText(new[] { ".X.", "XFX" }, ".XF", new UWRectInt(5, 6, 3, 2));
            UWMask lOXs = lOText.Where('X');

            Check.Bool("helpers: UWMaskText reads its rows", lOText.Is(1, 1, 'F') && lOText.At(0, 1) == 'X' && lOXs[1, 0] && !lOXs[1, 1]
                && lOText.Place.X == 5, true);

            UWPicture lODot = new UWPicture(5, 5);

            lODot.Set(2, 2, new UWColor32(200, 100, 50, 255));

            UWPicture lOShape = UWPixelOps.Copy(lODot);

            UWPixelOps.Outline(lODot, (piX, piY) => lOShape.Get(piX, piY).A == 0,
                (piX, piY) => piX >= 0 && piY >= 0 && piX < 5 && piY < 5 && lOShape.Get(piX, piY).A != 0, UWPixelOps.Black);
            Check.Bool("helpers: UWPixelOps.Outline rings the dot in black, nothing beyond", fArtVisible(lODot) == 9
                && UWPixelRules.Same(lODot.Get(1, 1), UWPixelOps.Black) && lODot.Get(0, 0).A == 0, true);

            UWPicture lOPadded = UWPixelOps.Pad(lOShape, 1, 2, 3, 4);

            Check.Bool("helpers: UWPixelOps.Pad", fArtSize(lOPadded) == "9x11" && lOPadded.Get(3, 4).A == 255, true);

            UWPicture lOTiled = UWPixelOps.MirrorTile(fArtPattern(3, 2, 7), 9, 4, true, true);

            Check.Bool("helpers: UWPixelOps.MirrorTile flips every second copy",
                UWPixelRules.Same(lOTiled.Get(3, 0), lOTiled.Get(2, 0)) && UWPixelRules.Same(lOTiled.Get(6, 0), lOTiled.Get(0, 0))
                && UWPixelRules.Same(lOTiled.Get(0, 2), lOTiled.Get(0, 1)), true);
            Check.Bool("helpers: the three blacks named in order (pure, edge, outline)",
                UWPixelRules.PureBlackSum < UWPixelRules.EdgeBlackSum && UWPixelRules.EdgeBlackSum < UWPixelRules.OutlineBlackSum, true);
        }

        private static bool fArtThrows(Action pOAction)
        {
            try
            {
                pOAction();
            }
            catch (ArgumentException)
            {
                return true;
            }

            return false;
        }

        private static void fCheckArtMasks()
        {
            UWRectInt lOScreen = new UWRectInt(0, 0, UWClassicLayout.Screen.Width, UWClassicLayout.Screen.Height);
            List<string> lOProblems = new List<string>();

            foreach ((string lsName, UWMaskText lOMask) in new[] { ("gargoyle", UWHudMasks.GargoyleHead), ("gem stand", UWHudMasks.GemStand),
                ("compass disc", UWHudMasks.CompassDisc) })
            {
                UWRectInt lOPlace = lOMask.Place;

                if (lOMask.Width != lOPlace.Width || lOMask.Height != lOPlace.Height || lOPlace.X < lOScreen.X || lOPlace.Y < lOScreen.Y
                    || lOPlace.X + lOPlace.Width > lOScreen.Width || lOPlace.Y + lOPlace.Height > lOScreen.Height)
                    lOProblems.Add(lsName);
            }

            Check.Str("masks: each the size of its place, inside MAIN.BYT", string.Join(", ", lOProblems), string.Empty);
        }

        // ------------------------------------------------- The builders

        private static void fCheckArtLeather()
        {
            UWPicture lOSource = fArtPattern(83, 41, 2);
            uint luBefore = fCrc32(lOSource);
            UWPicture lOLeather = UWLeatherBuilder.Compose(lOSource, 100, 60, 1);
            UWPicture lOSmall = UWLeatherBuilder.Compose(lOSource, 1, 1, 0);
            UWPicture lOTab = UWLeatherBuilder.ComposeTab(lOSource, 20, 12);
            UWPicture lOClosed = UWLeatherBuilder.ComposeHeadingStrip(lOSource, null, 200, 60);
            UWPicture lOStrip = UWLeatherBuilder.ComposeHeadingStrip(lOSource, fArtPattern(24, 18, 3), 200, 60);

            Check.Str("leather: sizes (any size; at least the frame and a pixel; the tab; the strip)",
                fArtSize(lOLeather) + " " + fArtSize(lOSmall) + " " + fArtSize(lOTab) + " " + fArtSize(lOStrip),
                "100x60 8x8 20x12 200x" + UWLeatherBuilder.StripHeight);
            Check.Bool("leather: opaque, its frame the source's corners", fArtVisible(lOLeather) == 100 * 60
                && UWPixelRules.Same(lOLeather.Get(0, 0), lOSource.Get(0, 0)) && UWPixelRules.Same(lOLeather.Get(99, 59), lOSource.Get(82, 40)), true);
            Check.Bool("leather: the inside only the source's shades (UWLeatherBuilder.CloudColours)",
                fArtOnlyShades(lOLeather, UWLeatherBuilder.CloudColours(lOSource)), true);
            Check.Bool("leather: the same bytes twice, the source untouched",
                fCrc32(lOLeather) == fCrc32(UWLeatherBuilder.Compose(lOSource, 100, 60, 1)) && fCrc32(lOSource) == luBefore, true);

            int liDiffers = 0;

            for (int liAt = 0; liAt < lOStrip.Pixels.Length; liAt++)
                liDiffers += UWPixelRules.Same(lOStrip.Pixels[liAt], lOClosed.Pixels[liAt]) ? 0 : 1;

            Check.Bool("leather: the gargoyle's head only on the strip's inside, at most its mask's pixels",
                liDiffers > 0 && liDiffers <= 24 * 18 && fArtStripFrameKept(lOStrip, lOClosed), true);
        }

        private static bool fArtOnlyShades(UWPicture pOLeather, List<UWColor32> pOShades)
        {
            for (int y = UWLeatherBuilder.LeatherTop; y < pOLeather.Height - UWLeatherBuilder.LeatherBottom; y++)
            {
                for (int x = UWLeatherBuilder.LeatherLeft; x < pOLeather.Width - UWLeatherBuilder.LeatherRight; x++)
                {
                    UWColor32 lOColour = pOLeather.Get(x, y);

                    if (!pOShades.Exists(lOShade => UWPixelRules.Same(lOShade, lOColour)))
                        return false;
                }
            }

            return true;
        }

        private static bool fArtStripFrameKept(UWPicture pOStrip, UWPicture pOClosed)
        {
            for (int y = 0; y < pOStrip.Height; y++)
            {
                for (int x = 0; x < pOStrip.Width; x++)
                {
                    bool lbInside = x >= UWLeatherBuilder.LeatherLeft && y >= UWLeatherBuilder.LeatherTop
                        && x < pOStrip.Width - UWLeatherBuilder.LeatherRight && y < pOStrip.Height - UWLeatherBuilder.LeatherBottom;

                    if (!lbInside && !UWPixelRules.Same(pOStrip.Get(x, y), pOClosed.Get(x, y)))
                        return false;
                }
            }

            return true;
        }

        private static void fCheckArtSlotAndPage(UWPicture pOMain)
        {
            UWPicture lOCircle = UWSlotBuilder.ComposeCircle(pOMain);
            UWPicture lORing = UWSlotBuilder.ComposeRing(pOMain);
            List<UWColor32> lOCarve = new List<UWColor32> { new UWColor32(10, 20, 30, 255), new UWColor32(200, 210, 220, 255) };
            UWPicture lOCarved = UWSlotBuilder.ComposeRing(pOMain, lOCarve);
            int liMiddle = UWSlotBuilder.SlotSize / 2;
            bool lbCarved = true;

            foreach (UWColor32 lOColour in lOCarved.Pixels)
                lbCarved &= lOColour.A == 0 || UWPixelRules.Same(lOColour, UWHudTuning.Slot.Hollow) || lOCarve.Exists(lOShade => UWPixelRules.Same(lOShade, lOColour));

            Check.Bool("slot: the circle cut round (corners transparent, the middle the frame's)", fArtSize(lOCircle) == "17x17"
                && lOCircle.Get(0, 0).A == 0 && UWPixelRules.Same(lOCircle.Get(liMiddle, liMiddle),
                    pOMain.Get(UWMainFrame.SlotCircle.X + liMiddle, UWMainFrame.SlotCircle.Y + liMiddle)), true);
            Check.Bool("slot: the ring's middle is the hollow, carved only in the back's shades",
                UWPixelRules.Same(lORing.Get(liMiddle, liMiddle), UWHudTuning.Slot.Hollow) && lORing.Get(0, 0).A == 0 && lbCarved, true);

            UWPicture lOPage = UWPaperdollBuilder.ComposePage(pOMain);
            UWPicture lOMarks = UWPaperdollBuilder.ComposeMarks(pOMain, lOCarve);
            bool lbColumns = true;

            for (int liColumn = 0; liColumn < UWPaperdollBuilder.PaperdollPageWidth; liColumn++)
            {
                int liMain = UWPaperdollBuilder.PageColumnToMain(liColumn);

                lbColumns &= liMain >= UWPaperdollBuilder.PaperdollPage.X && liMain < UWPaperdollBuilder.PaperdollPage.X + UWPaperdollBuilder.PaperdollPage.Width
                    && UWPixelRules.Same(lOPage.Get(liColumn, 5), pOMain.Get(liMain, UWPaperdollBuilder.PaperdollPage.Y + 5));
            }

            Check.Bool("paperdoll page: widened to 86, every column a column of the page", fArtSize(lOPage) == UWPaperdollBuilder.PaperdollPageWidth
                + "x" + UWPaperdollBuilder.PaperdollPage.Height && lbColumns, true);
            Check.Bool("paperdoll marks: the page's size, keyed, the hollows in the large circles", fArtSize(lOMarks) == fArtSize(lOPage)
                && UWPixelRules.Same(lOMarks.Get((int)UWMainFrame.PageCircles[0, 0] + UWPaperdollBuilder.PaperdollShift, (int)UWMainFrame.PageCircles[0, 1]),
                    UWHudTuning.Slot.Hollow), true);
        }

        private static void fCheckArtPageFreer(UWPicture pOMain)
        {
            // A square in a black ring painted on a piece of the page: the page around it goes, the
            // ring and the square stay.
            int liX0 = UWPaperdollBuilder.PaperdollBodyX;
            int liY0 = UWPaperdollBuilder.PaperdollBodyY;
            UWPicture lOBody = pOMain.Crop(new UWRectInt(liX0, liY0, 30, 30));

            UWPixelOps.Fill(lOBody, new UWRectInt(8, 8, 14, 14), UWPixelOps.Black);
            UWPixelOps.Fill(lOBody, new UWRectInt(9, 9, 12, 12), new UWColor32(200, 60, 60, 255));

            UWPicture lOFreed = UWPageFreer.Free(pOMain, lOBody, liX0, liY0);

            Check.Bool("page freer: the page goes, the outlined square stays", fArtVisible(lOFreed) == 14 * 14 && lOFreed.Get(0, 0).A == 0
                && UWPixelRules.Same(lOFreed.Get(15, 15), new UWColor32(200, 60, 60, 255)) && UWPixelRules.Same(lOFreed.Get(8, 8), UWPixelOps.Black), true);

            UWPicture lOFigure = UWPaperdollBuilder.ComposeFigure(pOMain, lOBody, 0, null, 0, 0, new List<UWPicture>(), new List<(int X, int Y)>(),
                out int liFigureX, out int liFigureY);

            Check.Bool("paperdoll figure: without a silhouette the body freed by its colours, at the body's place",
                fArtVisible(lOFigure) == 14 * 14 && liFigureX == liX0 && liFigureY == liY0, true);
        }

        private static void fCheckArtShelves(UWPicture pOMain)
        {
            UWPicture lOShelf = UWShelfBuilder.Compose(pOMain, out UWShelfBuilder.ShelfLayout lOLayout, false);
            UWPicture lOApart = UWShelfBuilder.Compose(pOMain, out UWShelfBuilder.ShelfLayout lOApartLayout, true);

            Check.Bool("shelf: its layout its size; the gem on it, or apart (no gem place)", fArtSize(lOShelf) == lOLayout.Width + "x" + lOLayout.Height
                && lOLayout.Height == UWMainFrame.ShelfBottom - UWMainFrame.ShelfTop && lOLayout.GemX >= 0 && lOApartLayout.GemX == -1
                && fArtSize(lOApart) == lOApartLayout.Width + "x" + lOApartLayout.Height && fArtAlphaKeyed(lOShelf), true);

            // A picture with a backdrop border: the keep mask is its inside.
            UWPicture lOFlask = fArtPattern(10, 12, 4);

            for (int liAt = 0; liAt < lOFlask.Pixels.Length; liAt++)
            {
                if (liAt % 10 == 0 || liAt % 10 == 9 || liAt < 10 || liAt >= 110)
                    lOFlask.Pixels[liAt] = UWPixelOps.Transparent;
            }

            bool[] lbKeep = UWShelfBuilder.ComposeKeepMask(new[] { lOFlask });
            int liKept = 0;

            foreach (bool lbBit in lbKeep)
                liKept += lbBit ? 1 : 0;

            Check.Bool("shelf masks: the keep mask is what the border's backdrop does not reach", liKept == 8 * 10 && !lbKeep[0] && lbKeep[11], true);

            UWPicture lOStand = UWFrameCutBuilder.ComposeGemStand(pOMain, out int liGemX, out int liGemY);
            bool lbStand = fArtSize(lOStand) == UWHudMasks.GemStand.Width + "x" + UWHudMasks.GemStand.Height;

            for (int y = 0; lbStand && y < lOStand.Height; y++)
            {
                for (int x = 0; x < lOStand.Width; x++)
                    lbStand &= lOStand.Get(x, y).A == (UWHudMasks.GemStand.Is(x, y, 'X') ? 255 : 0);
            }

            Check.Bool("gem stand: opaque exactly on its mask, the gem inside", lbStand && liGemX >= 0 && liGemY >= 0 && liGemX < lOStand.Width, true);

            UWPicture lOHollow = UWFrameCutBuilder.ComposeRuneHollow(pOMain, false);
            UWPicture lOFramed = UWFrameCutBuilder.ComposeRuneHollow(pOMain, true);

            Check.Bool("rune hollow: opaque; the outline a black pixel all round", fArtVisible(lOHollow) == lOHollow.Pixels.Length
                && lOFramed.Width == lOHollow.Width + 2 && lOFramed.Height == lOHollow.Height + 2 && fArtOnlyBlackAdded(lOHollow, lOFramed, 1, 1)
                && UWPixelRules.Same(lOFramed.Get(0, 0), UWPixelOps.Black), true);

            UWPicture lONarrow = UWStoneShelfBuilder.Compose(pOMain, 1);
            UWPicture lOStone = UWStoneShelfBuilder.Compose(pOMain, 100);
            UWPicture lOWider = UWStoneShelfBuilder.Compose(pOMain, 150);
            bool lbGrows = true;

            for (int y = 0; y < lOStone.Height; y++)
            {
                for (int x = 0; x < 40; x++)
                    lbGrows &= UWPixelRules.Same(lOStone.Get(x, y), lOWider.Get(x, y));
            }

            Check.Bool("stone shelf: at least its narrowest, the ledge's rows, opaque, the same twice",
                fArtSize(lONarrow) == UWStoneShelfBuilder.StoneShelfMinWidth + "x" + UWMainFrame.StoneLedge.Height
                && fArtSize(lOStone) == "100x" + UWMainFrame.StoneLedge.Height && fArtVisible(lOStone) == lOStone.Pixels.Length
                && fCrc32(lOStone) == fCrc32(UWStoneShelfBuilder.Compose(pOMain, 100)), true);
            Check.Bool("stone shelf: a wider shelf only grows on the right (its first 40 columns the same)", lbGrows, true);
        }

        private static void fCheckArtClassicWide(UWPicture pOMain)
        {
            List<string> lOProblems = new List<string>();

            foreach (int liExtra in new[] { 0, 33, 120 })
            {
                UWClassicWideBuilder.ClassicWideFrame lOFrame = UWClassicWideBuilder.Compose(pOMain, null, liExtra);
                UWPicture lOWide = lOFrame.Picture;
                int liHoleWidth = UWClassicLayout.ClassicWide.Hole.Width + liExtra;
                int liClear = lOWide.Pixels.Length - fArtVisible(lOWide);

                if (fArtSize(lOWide) != (UWClassicLayout.Screen.Width + liExtra) + "x" + UWClassicLayout.Screen.Height || !fArtAlphaKeyed(lOWide))
                    lOProblems.Add(liExtra + ": size or alpha");

                if (liClear != liHoleWidth * UWClassicLayout.ClassicWide.Hole.Height || lOFrame.Hole.Width != liHoleWidth
                    || lOWide.Get(lOFrame.Hole.X, lOFrame.Hole.Y).A != 0 || lOWide.Get(lOFrame.Hole.X + liHoleWidth - 1, lOFrame.Hole.Y + lOFrame.Hole.Height - 1).A != 0)
                    lOProblems.Add(liExtra + ": the hole");

                // Every original pixel where ClassicWideShiftX puts it (rows of each band, outside the hole).
                foreach ((int liX, int liY) in new[] { (10, 5), (150, 5), (300, 5), (20, 60), (310, 100), (60, 150), (150, 150), (290, 150), (50, 190), (200, 190) })
                {
                    UWColor32 lOWas = pOMain.Get(liX, liY);
                    UWColor32 lONow = lOWide.Get(liX + UWClassicWideBuilder.ClassicWideShiftX(liX, liY, liExtra), liY);

                    if (!UWPixelRules.SameRgb(lOWas, lONow))
                        lOProblems.Add(liExtra + ": pixel " + liX + "," + liY);
                }

                if (fCrc32(lOWide) != fCrc32(UWClassicWideBuilder.Compose(pOMain, null, liExtra).Picture))
                    lOProblems.Add(liExtra + ": not the same twice");
            }

            Check.Str("classic wide: size, keyed alpha, the hole exactly the view's widened, every pixel shifted, the same twice",
                string.Join("; ", lOProblems), string.Empty);
        }

        private static void fCheckArtConversation(UWPicture pOMain)
        {
            UWPicture lOConv = fArtPattern(UWClassicLayout.Screen.Width, UWClassicLayout.Screen.Height, 5);
            uint luBefore = fCrc32(lOConv);
            UWPicture lOHeader = UWConversationArtBuilder.ComposeHeader(lOConv);
            UWPicture lOParchment = UWConversationArtBuilder.ComposeParchment(lOConv, null, 89);
            UWPicture lOAnswers = UWConversationArtBuilder.ComposeParchment(lOConv, pOMain, 40, true);
            UWPicture lOLow = UWConversationArtBuilder.ComposeParchment(lOConv, null, 3);

            Check.Bool("conversation: sizes (the header, the parchment at its rows, at least its rims), keyed alpha, CONV.BYT untouched",
                fArtSize(lOHeader) == UWConversationArtBuilder.ConversationHeader.Width + "x" + UWConversationArtBuilder.ConversationHeader.Height
                && fArtSize(lOParchment) == UWConversationArtBuilder.ConversationParchment.Width + "x89" && lOAnswers.Height == 40
                && lOLow.Height == 29 && fArtAlphaKeyed(lOHeader) && fArtAlphaKeyed(lOParchment) && fArtAlphaKeyed(lOAnswers)
                && fCrc32(lOConv) == luBefore, true);

            UWPicture lOOff = UWConversationArtBuilder.PadAndOutline(lOParchment, false);
            UWPicture lOOn = UWConversationArtBuilder.PadAndOutline(lOParchment, true);

            Check.Bool("conversation: PadAndOutline a pixel larger; off takes away only, on adds only black",
                lOOn.Width == lOParchment.Width + 2 && lOOff.Height == lOParchment.Height + 2 && fArtVisible(lOOff) <= fArtVisible(lOParchment)
                && fArtOnlyBlackAdded(lOParchment, lOOn, 1, 1), true);
        }

        private static void fCheckArtCompass(UWPicture pOMain)
        {
            UWPicture lODisc = UWCompassDiscBuilder.Compose(pOMain, null, false);
            UWPicture lOOutlined = UWCompassDiscBuilder.Compose(pOMain, null, true);
            UWMaskText lOMask = UWHudMasks.CompassDisc;
            int liLeft = UWMainFrame.CompassMaskX - UWCompassDiscBuilder.CompassDiscX;
            bool lbMask = fArtSize(lODisc) == (lOMask.Width + 2) + "x" + (lOMask.Height + 1);

            for (int y = 0; lbMask && y < lOMask.Height; y++)
            {
                for (int x = 0; x < lOMask.Width; x++)
                    lbMask &= lODisc.Get(liLeft + x, y).A == (lOMask.Is(x, y, '.') ? 0 : 255);
            }

            Check.Bool("compass disc: opaque exactly on its mask, a pixel of room; the outline only black, the same twice",
                lbMask && fArtOnlyBlackAdded(lODisc, lOOutlined, 0, 0) && fArtVisible(lOOutlined) > fArtVisible(lODisc)
                && fCrc32(lODisc) == fCrc32(UWCompassDiscBuilder.Compose(pOMain, null, false)), true);

            bool lbArrows = true;

            for (int y = 0; y < lODisc.Height; y++)
            {
                for (int x = 0; x < lODisc.Width; x++)
                {
                    int liArrow = UWCompassDiscBuilder.CompassDiscArrowAt(x, y);

                    lbArrows &= liArrow >= -1 && liArrow < UWClickRules.EasyArrows.Length;
                }
            }

            Check.Bool("compass disc: the arrows' map only knows the original's arrows", lbArrows, true);

            // A warm square in a pure black frame and a smaller warm blob apart, on grey stone.
            UWColor32[] lyPixels = new UWColor32[12 * 8];
            UWColor32 lOWarm = new UWColor32(200, 150, 60, 255);

            for (int liAt = 0; liAt < lyPixels.Length; liAt++)
                lyPixels[liAt] = new UWColor32(90, 90, 100, 255);

            for (int y = 1; y <= 5; y++)
            {
                for (int x = 1; x <= 5; x++)
                    lyPixels[(y * 12) + x] = x == 1 || y == 1 || x == 5 || y == 5 ? new UWColor32(0, 0, 0, 255) : lOWarm;
            }

            lyPixels[(2 * 12) + 9] = lOWarm;
            lyPixels[(2 * 12) + 10] = lOWarm;

            bool[] lbCross = UWCompassDiscBuilder.CompassCrossMask(lyPixels, 12, 8);
            int liCross = 0;

            foreach (bool lbBit in lbCross)
                liCross += lbBit ? 1 : 0;

            Check.Bool("compass cross: the largest warm piece and the pure black around it, not the stone nor the smaller piece",
                liCross == 25 && lbCross[(3 * 12) + 3] && lbCross[(1 * 12) + 1] && !lbCross[(2 * 12) + 9] && !lbCross[0], true);
            Check.Bool("compass tips: red and pure black kept, stone not", UWCompassDiscBuilder.CompassTipKeeps(new UWColor32(220, 40, 40, 255))
                && UWCompassDiscBuilder.CompassTipKeeps(new UWColor32(0, 0, 0, 255)) && !UWCompassDiscBuilder.CompassTipKeeps(new UWColor32(90, 90, 100, 255)), true);
        }

        private static void fCheckArtScrollPaper(UWPicture pOMain)
        {
            // An edge strip 4 x 29 with one transparent pixel; the frame's dark ground at one place.
            UWPicture lOEdge = fArtPattern(4, 29, 6);
            UWPicture lOFrame = UWPixelOps.Copy(pOMain);
            int liTop = UWScrollPaperBuilder.PaperTop - UWScrollPaperBuilder.SourceTop;

            lOEdge.Set(1, 0, UWPixelOps.Transparent);
            lOFrame.Set(200, UWScrollPaperBuilder.SourceTop, new UWColor32(0, 0, 4, 255));

            uint luEdge = fCrc32(lOEdge);
            UWPicture lOPlain = UWScrollPaperBuilder.Compose(lOFrame, null, null, 0);
            UWPicture lOEdged = UWScrollPaperBuilder.Compose(lOFrame, lOEdge, lOEdge, 0);
            UWPicture lOTaller = UWScrollPaperBuilder.Compose(lOFrame, lOEdge, lOEdge, 14);
            UWPicture lOLower = UWScrollPaperBuilder.Compose(lOFrame, null, null, -7);
            bool lbPaper = true;

            for (int y = liTop; y < liTop + 30; y++)
            {
                for (int x = UWScrollPaperBuilder.PaperLeft; x < UWScrollPaperBuilder.PaperLeft + UWScrollPaperBuilder.PaperWidth; x++)
                    lbPaper &= UWPixelRules.Same(lOPlain.Get(x, y), UWScrollPaperBuilder.Paper);
            }

            Check.Bool("scroll paper: the frame's rows, the paper filled, the ground alone transparent, the same twice",
                fArtSize(lOPlain) == UWClassicLayout.Screen.Width + "x" + UWScrollPaperBuilder.SourceRows && lbPaper
                && fArtVisible(lOPlain) == lOPlain.Pixels.Length - 1 && lOPlain.Get(200, 0).A == 0
                && UWPixelRules.Same(lOPlain.Get(20, 0), lOFrame.Get(20, UWScrollPaperBuilder.SourceTop))
                && fCrc32(lOPlain) == fCrc32(UWScrollPaperBuilder.Compose(lOFrame, null, null, 0)), true);

            // Taller or lower by rows: the bottom rows are the frame's bottom rows, the edges grow with
            // the paper; an edge's transparent pixel leaves what lies under it.
            bool lbBottom = true;

            for (int x = 0; x < lOPlain.Width; x++)
            {
                lbBottom &= UWPixelRules.Same(lOTaller.Get(x, lOTaller.Height - 1), lOEdged.Get(x, lOEdged.Height - 1))
                    && UWPixelRules.Same(lOLower.Get(x, lOLower.Height - 1), lOPlain.Get(x, lOPlain.Height - 1));
            }

            Check.Bool("scroll paper: taller and lower by rows, the bottom kept; the edges laid on, their gaps what lay under them; the edge untouched",
                lOTaller.Height == UWScrollPaperBuilder.SourceRows + 14 && lOLower.Height == UWScrollPaperBuilder.SourceRows - 7 && lbBottom
                && UWPixelRules.Same(lOEdged.Get(11, liTop), UWPixelOps.Opaque(lOEdge.Get(0, 0)))
                && UWPixelRules.Same(lOEdged.Get(12, liTop), lOPlain.Get(12, liTop))
                && UWPixelRules.Same(lOTaller.Get(306 + 3, liTop + 28 + 14), UWPixelOps.Opaque(lOEdge.Get(3, 28)))
                && fCrc32(lOEdge) == luEdge, true);

            // Classic Wide's frame: the paper as much wider.
            UWPicture lOWide = UWClassicWideBuilder.Compose(pOMain, null, 33).Picture;
            UWPicture lOWidePaper = UWScrollPaperBuilder.Compose(lOWide, null, null, 0);

            Check.Bool("scroll paper: from Classic Wide's frame as wide, the paper as much wider",
                lOWidePaper.Width == UWClassicLayout.Screen.Width + 33
                && UWPixelRules.Same(lOWidePaper.Get(UWScrollPaperBuilder.PaperLeft + UWScrollPaperBuilder.PaperWidth + 32, liTop), UWScrollPaperBuilder.Paper)
                && !UWPixelRules.Same(lOWidePaper.Get(UWScrollPaperBuilder.PaperLeft + UWScrollPaperBuilder.PaperWidth + 33, liTop), UWScrollPaperBuilder.Paper),
                true);
        }
    }
}
