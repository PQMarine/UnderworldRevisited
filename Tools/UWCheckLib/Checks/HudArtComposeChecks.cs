using System;
using System.Collections.Generic;
using UWDataImport.Presentation;
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
                ("compass disc", UWHudMasks.CompassDisc), ("uw2 gargoyle", UWHudMasks.GargoyleHeadUw2),
                ("uw2 compass recess", UWHudMasks.CompassRecessUw2) })
            {
                UWRectInt lOPlace = lOMask.Place;

                if (lOMask.Width != lOPlace.Width || lOMask.Height != lOPlace.Height || lOPlace.X < lOScreen.X || lOPlace.Y < lOScreen.Y
                    || lOPlace.X + lOPlace.Width > lOScreen.Width || lOPlace.Y + lOPlace.Height > lOScreen.Height)
                    lOProblems.Add(lsName);
            }

            Check.Str("masks: each the size of its place, inside MAIN.BYT", string.Join(", ", lOProblems), string.Empty);

            // The second game's head (UW2 plan part U4d step 1): the first game's size, so the strip's brand, its margins
            // and the eyes' place on it hold for both; its eyes (EYES at 0x6E, top 4) two columns in, as the first game's.
            Check.Bool("masks: the second game's head the first game's size",
                UWHudMasks.GargoyleHeadUw2.Width == UWHudMasks.GargoyleHead.Width && UWHudMasks.GargoyleHeadUw2.Height == UWHudMasks.GargoyleHead.Height
                && UWHudMasks.GargoyleHeadUw2.Width == UWLeatherBuilder.BrandWidth, true);
            Check.Bool("masks: the second game's eyes two columns into its head, at its row 4",
                UWClassicLayoutUw2.Eyes.Left - UWClassicLayoutUw2.GargoyleHead.Left == UWLeatherBuilder.EyesLeftInBrand
                && UWClassicLayoutUw2.Eyes.Top - UWClassicLayoutUw2.GargoyleHead.Top == 4, true);

            // THE SECOND GAME'S RECESSES (UW2 plan stage U6 step 1): the compass's recess bevelled at its top corners and
            // straight below, the face (0x5E/0x33, 0x34 x 0x10) and the gem (0x71/0x30) inside it; the hollow's three
            // runes (rune_x at 0x32, 16 x 16) inside the right recess.
            UWMaskText lORecess = UWHudMasks.CompassRecessUw2;
            int liFirstRow = 0;
            int liLastRow = 0;

            for (int liX = 0; liX < lORecess.Width; liX++)
            {
                liFirstRow += lORecess.Is(liX, 0, 'X') ? 1 : 0;
                liLastRow += lORecess.Is(liX, lORecess.Height - 1, 'X') ? 1 : 0;
            }

            Check.Str("masks: the second game's compass recess, its rim's top row and its straight bottom", liFirstRow + " " + liLastRow, "60 72");

            UWRectInt lOFace = new UWRectInt(UWClassicLayoutUw2.Compass.Left, UWClassicLayoutUw2.Compass.Top, 0x34, 0x10);
            UWRectInt lOGem = new UWRectInt(UWClassicLayoutUw2.PowerGem.Left, UWClassicLayoutUw2.PowerGem.Top, 1, 1);
            UWRectInt lOHollow = UWClassicLayoutUw2.RuneHollow.Place;

            Check.Bool("uw2 recesses: the face and the gem inside the compass's recess",
                fInside(lOFace, lORecess.Place) && fInside(lOGem, lORecess.Place), true);
            Check.Bool("uw2 recesses: the three runes inside the hollow, as the frame's rune_x",
                fInside(new UWRectInt(UWClassicLayoutUw2.Runes.ShelfX[0], 199 - UWClassicLayoutUw2.Runes.ShelfY, 16, 16), lOHollow)
                && fInside(new UWRectInt(UWClassicLayoutUw2.Runes.ShelfX[2], 199 - UWClassicLayoutUw2.Runes.ShelfY, 16, 16), lOHollow)
                && UWClassicLayoutUw2.RuneHollow.RuneLeft == UWClassicLayoutUw2.Runes.ShelfX[0]
                && UWClassicLayoutUw2.RuneHollow.RuneLeft + (2 * UWClassicLayoutUw2.RuneHollow.RunePitch) == UWClassicLayoutUw2.Runes.ShelfX[2], true);
            Check.Bool("uw2 recesses: the compass's geometry without arrows, with the gem, whole on the disc; the first game's as before",
                !UWCompassGeometry.Uw2.HasArrows && UWCompassGeometry.Uw2.HasGem && !UWCompassGeometry.Uw2.IsFreedOnDisc
                && UWCompassGeometry.Uw1.HasArrows && !UWCompassGeometry.Uw1.HasGem && UWCompassGeometry.Uw1.IsFreedOnDisc, true);

            UWCompassGeometry.Uw1.CrossOnDisc(null, out int liCrossX, out int liCrossY);

            Check.Str("uw1: the cross on the disc where the modern compass laid it",
                liCrossX + "," + liCrossY, (UWClassicLayout.Compass.Left - UWHudArt.CompassDiscX) + "," + (UWClassicLayout.Compass.Top - UWHudArt.CompassDiscY));
        }

        private static bool fInside(UWRectInt pOInner, UWRectInt pOOuter)
        {
            return pOInner.X >= pOOuter.X && pOInner.Y >= pOOuter.Y && pOInner.X + pOInner.Width <= pOOuter.X + pOOuter.Width
                && pOInner.Y + pOInner.Height <= pOOuter.Y + pOOuter.Height;
        }

        /// <summary>
        /// CLASSIC WIDE ON THE SECOND GAME'S FRAME (UW2 plan stage U7 step 1): the geometry per game (the first game's the
        /// table's numbers as before), the bands and cuts in order, the view's cuts at the hole's edges, every band's second
        /// cut left of the right block; widened, the right block moved whole and the left edge kept, the hole clear in the
        /// camera's area only, its black ring and the gargoyle's teeth opaque, the ledge's grown marble without the hollows'
        /// black lines, the same picture every time.
        /// </summary>
        private static void fCheckUw2ClassicWide(CheckContext pOContext)
        {
            Check.Group("Classic Wide on the second game's frame (UW2 plan stage U7 step 1)");

            UWClassicWideGeometry lOFirst = UWClassicWideGeometry.Of(null);
            Check.Bool("classic wide: the first game's geometry is the table's",
                ReferenceEquals(lOFirst, UWClassicWideGeometry.Uw1) && fSameCuts(lOFirst.Cuts, UWClassicLayout.ClassicWide.Cuts)
                && lOFirst.TopRows == UWClassicLayout.ClassicWide.TopRows && lOFirst.HoleBlack < 0 && lOFirst.CoversBakedNeedle, true);
            Check.Str("classic wide: the columns put in shared among the cuts (2 of 106 at 1, 4 of 106 at 1 to 3, 4 of 240)",
                UWClassicWideGeometry.Share(106, 1, 2) + " " + UWClassicWideGeometry.Share(106, 1, 4) + "/" + UWClassicWideGeometry.Share(106, 2, 4)
                + "/" + UWClassicWideGeometry.Share(106, 3, 4) + " " + UWClassicWideGeometry.Share(240, 1, 4), "53 26/53/79 60");

            UWClassicWideGeometry lOGeo = UWClassicWideGeometry.Uw2;
            bool lbOrder = 0 < lOGeo.TopRows && lOGeo.TopRows < lOGeo.LedgeTop && lOGeo.LedgeTop < lOGeo.ScrollTop
                && lOGeo.ScrollTop < UWClassicLayout.Screen.Height && lOGeo.Cuts.Length == 4 && lOGeo.Sources.Length == 4;

            for (int liBand = 0; liBand < 4; liBand++)
            {
                int[] liCuts = lOGeo.Cuts[liBand];

                lbOrder &= liCuts.Length >= 2 && 0 < liCuts[0] && liCuts[liCuts.Length - 1] < UWClassicLayoutUw2.Panel.Left;

                for (int liAt = 1; liAt < liCuts.Length; liAt++)
                    lbOrder &= liCuts[liAt - 1] < liCuts[liAt];
            }

            Check.Bool("classic wide uw2: bands and cuts ascend, every last cut left of the right block", lbOrder, true);
            Check.Str("classic wide uw2: the cuts a band (the ledge four, per user)",
                lOGeo.Cuts[0].Length + "" + lOGeo.Cuts[1].Length + lOGeo.Cuts[2].Length + lOGeo.Cuts[3].Length, "2242");
            Check.Str("classic wide: dragons, the weapon's row and the projection's centre row, first and second game",
                UWClassicWideGeometry.Uw1.HasDragons + " " + UWClassicWideGeometry.Uw1.WeaponRow + " " + UWClassicWideGeometry.Uw1.ProjectionCentreRow
                + " / " + lOGeo.HasDragons + " " + lOGeo.WeaponRow + " " + lOGeo.ProjectionCentreRow,
                "True 131 " + UWClassicLayout.Frame.ProjectionCentreRow + " / False 144 81.5");

            // The second game's command bar and flask area, the rules its classic screen and Classic Wide share (step 2).
            int liBar = UWClassicLayoutUw2.Commands.Left;
            int liBarTop = UWClassicLayoutUw2.Commands.Top;
            Check.Str("classic wide uw2: the command bar's icons (fight, use, talk, outside left, above)",
                UWClickRules.CommandIconAtUw2(liBar + 1, liBarTop + 1) + " " + UWClickRules.CommandIconAtUw2(liBar + 27, liBarTop + 1) + " "
                + UWClickRules.CommandIconAtUw2(liBar + 1, liBarTop + 16) + " " + UWClickRules.CommandIconAtUw2(UWClassicLayoutUw2.Commands.AreaLeft - 1, liBarTop + 1)
                + " " + UWClickRules.CommandIconAtUw2(liBar + 1, liBarTop - 2), "0 1 3 -1 -1");
            Check.Str("classic wide uw2: the flask area (chain above, chain below, vitality, mana, above the words)",
                UWClickRules.FlaskAtUw2(0x17, 11) + " " + UWClickRules.FlaskAtUw2(0x17, 5) + " " + UWClickRules.FlaskAtUw2(5, 10) + " "
                + UWClickRules.FlaskAtUw2(0x30, 10) + " " + UWClickRules.FlaskAtUw2(5, 0x1F), "Chain None Vitality Mana None");
            Check.Bool("classic wide uw2: the flask area's corners",
                UWClickRules.IsInFlasksUw2(UWClassicLayoutUw2.FlaskClicks.Left, UWClassicLayoutUw2.FlaskClicks.BottomY)
                && UWClickRules.IsInFlasksUw2(UWClassicLayoutUw2.FlaskClicks.Right, UWClassicLayoutUw2.FlaskClicks.TopY)
                && !UWClickRules.IsInFlasksUw2(UWClassicLayoutUw2.FlaskClicks.Left - 1, UWClassicLayoutUw2.FlaskClicks.BottomY), true);

            // THE SECOND GAME'S SLIDE (step 2b): twenty frames, the old page out eight columns a frame, the new one in from the
            // right, the flip on the eleventh, the chain's pictures 0 to 3 on frames 1, 4, 13 and 16; a frame more ends it.
            UWPanelTurn lOSlide = new UWPanelTurn { Slides = true };
            System.Text.StringBuilder lOOffsets = new System.Text.StringBuilder();
            System.Text.StringBuilder lOChains = new System.Text.StringBuilder();
            int liFlipAt = -1;
            int liFrames = 0;

            lOSlide.Begin(UWPanelTurn.SideEnum.Stats);

            for (int liGuard = 0; liGuard < 40; liGuard++)
            {
                UWPanelTurn.Frame lOStep = lOSlide.Advance();

                if (lOStep.Finished)
                    break;

                liFrames++;
                lOOffsets.Append(lOStep.Offset).Append(' ');

                if (lOStep.ChainFrame >= 0)
                    lOChains.Append(liFrames).Append(':').Append(lOStep.ChainFrame).Append(' ');

                if (lOStep.Flipped)
                    liFlipAt = liFrames;
            }

            Check.Str("classic wide uw2: the slide's frames, offsets, flip and chain pictures",
                liFrames + " | " + lOOffsets.ToString().Trim() + " | flip " + liFlipAt + " | " + lOChains.ToString().Trim() + " | " + lOSlide.Side + " " + lOSlide.IsTurning,
                "20 | 8 16 24 32 40 48 56 64 72 80 72 64 56 48 40 32 24 16 8 0 | flip 11 | 1:0 4:1 13:2 16:3 | Stats False");
            Check.Int("classic wide: the first game's turn still eight steps", new UWPanelTurn().StepCount, UWPanelTurn.Steps);

            Check.Bool("classic wide uw2: the view's cuts at the hole's edges, the hole the camera's area",
                lOGeo.Cuts[1][0] == lOGeo.Hole.X && lOGeo.Cuts[1][1] == lOGeo.Hole.X + lOGeo.Hole.Width
                && lOGeo.CameraLeft == lOGeo.Hole.X && lOGeo.CameraRight == lOGeo.Hole.X + lOGeo.Hole.Width
                && lOGeo.CameraTop == lOGeo.Hole.Y && lOGeo.CameraBottom == lOGeo.Hole.Y + lOGeo.Hole.Height, true);

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2", "no second game here: skipped");

                return;
            }

            UWTextures lOTextures = new UWTextures(lsData, new UWPalettes(lsData), UWGame.Uw2);
            Check.Bool("classic wide uw2: the second game's textures give its geometry", ReferenceEquals(UWClassicWideGeometry.Of(lOTextures), lOGeo), true);

            const int Extra = 106;
            UWPicture lOMain = UWMainFrame.Picture(lOTextures);
            UWClassicWideBuilder.ClassicWideFrame lOWide = UWClassicWideBuilder.Build(lOTextures, Extra);
            UWPicture lOPicture = lOWide.Picture;
            int liRightBlock = UWClassicLayoutUw2.Panel.Left - 9;
            int liBlockWrong = 0;
            int liLeftWrong = 0;
            int liClearOutside = 0;
            int liLeftEdge = lOMain.Width;

            foreach (int[] liBandCuts in lOGeo.Cuts)
                liLeftEdge = Math.Min(liLeftEdge, liBandCuts[0]);

            for (int liY = 0; liY < lOMain.Height; liY++)
            {
                for (int liX = liRightBlock; liX < lOMain.Width; liX++)
                    liBlockWrong += fSame(lOPicture.Get(liX + Extra, liY), lOMain.Get(liX, liY)) ? 0 : 1;

                for (int liX = 0; liX < liLeftEdge; liX++)
                    liLeftWrong += fSame(lOPicture.Get(liX, liY), lOMain.Get(liX, liY)) ? 0 : 1;

                for (int liX = 0; liX < lOPicture.Width; liX++)
                {
                    bool lbInCamera = liX >= lOGeo.CameraLeft && liX < lOGeo.CameraRight + Extra && liY >= lOGeo.CameraTop && liY < lOGeo.CameraBottom;

                    liClearOutside += !lbInCamera && lOPicture.Get(liX, liY).A == 0 ? 1 : 0;
                }
            }

            Check.Str("classic wide uw2: the widened picture's size", lOPicture.Width + "x" + lOPicture.Height, (320 + Extra) + "x200");
            Check.Int("classic wide uw2: the right block (panel, flasks, command bar) moved whole", liBlockWrong, 0);
            Check.Int("classic wide uw2: the left edge kept (left of every band's first cut)", liLeftWrong, 0);
            Check.Int("classic wide uw2: nothing clear outside the camera's area", liClearOutside, 0);

            // The hole's middle clear, its black ring (rows 15 and 144) opaque, the teeth over its first rows opaque.
            int liMiddleX = lOGeo.Hole.X + ((lOGeo.Hole.Width + Extra) / 2);
            int liTeeth = 0;

            for (int liX = UWClassicLayoutUw2.ViewOverlay.TeethX; liX < UWClassicLayoutUw2.ViewOverlay.TeethX + 20; liX++)
            {
                if (!lOGeo.IsHolePixel(lOMain.Get(liX, lOGeo.Hole.Y)))
                    liTeeth += lOPicture.Get(liX + (Extra / 2), lOGeo.Hole.Y).A != 0 ? 1 : -1000;
            }

            Check.Bool("classic wide uw2: the hole's middle clear, its black ring opaque",
                lOPicture.Get(liMiddleX, lOGeo.Hole.Y + (lOGeo.Hole.Height / 2)).A == 0
                && lOPicture.Get(liMiddleX, lOGeo.Hole.Y - 1).A != 0 && lOPicture.Get(liMiddleX, lOGeo.Hole.Y + lOGeo.Hole.Height).A != 0, true);
            Check.Bool("classic wide uw2: the gargoyle's teeth over the view opaque, moved with the middle", liTeeth > 0, true);

            // The ledge's grown marble (the columns put in at its four cuts) without the hollows' black top lines - below
            // the bevels' rows at the ends (black above them down to row 149); and each hollow where its share puts it.
            int liBlackGrown = 0;
            int[] liLedgeCuts = lOGeo.Cuts[2];

            for (int liCut = 0; liCut < liLedgeCuts.Length; liCut++)
            {
                int liBefore = UWClassicWideGeometry.Share(Extra, liCut, liLedgeCuts.Length);
                int liAfter = UWClassicWideGeometry.Share(Extra, liCut + 1, liLedgeCuts.Length);

                for (int liY = UWClassicLayoutUw2.SpellHollow.Top; liY < UWClassicLayoutUw2.Ledge.Top + UWClassicLayoutUw2.Ledge.Height; liY++)
                {
                    for (int liX = liBefore; liX < liAfter; liX++)
                        liBlackGrown += lOGeo.IsHolePixel(lOPicture.Get(liLedgeCuts[liCut] + liX, liY)) ? 1 : 0;
                }
            }

            Check.Int("classic wide uw2: the ledge's grown marble without black", liBlackGrown, 0);
            Check.Str("classic wide uw2: the spells' hollow, the compass recess and the rune hollow moved a quarter, half, three quarters",
                lOGeo.ShiftX(UWClassicLayoutUw2.SpellHollow.Left, UWClassicLayoutUw2.SpellHollow.Top, Extra) + " "
                + lOGeo.ShiftX(UWClassicLayoutUw2.CompassRecess.Left, UWClassicLayoutUw2.CompassRecess.Top, Extra) + " "
                + lOGeo.ShiftX(UWClassicLayoutUw2.RuneHollow.Left, UWClassicLayoutUw2.RuneHollow.Top, Extra), "26 53 79");
            Check.Bool("classic wide uw2: the spells' hollow moved whole in the picture",
                fSame(lOPicture.Get(UWClassicLayoutUw2.SpellHollow.Left + 26 + 5, UWClassicLayoutUw2.SpellHollow.Top + 5),
                    lOMain.Get(UWClassicLayoutUw2.SpellHollow.Left + 5, UWClassicLayoutUw2.SpellHollow.Top + 5)), true);
            Check.Bool("classic wide uw2: the same picture every time", fCrc32(lOPicture) == fCrc32(UWClassicWideBuilder.Build(lOTextures, Extra).Picture), true);
            Check.Str("classic wide uw2: unwidened, the frame's size and the hole the camera's area",
                UWClassicWideBuilder.Build(lOTextures, 0).Picture.Width + " " + lOWide.Hole.X + "/" + lOWide.Hole.Y + " " + lOWide.Hole.Width + "x" + lOWide.Hole.Height,
                "320 16/16 " + (208 + Extra) + "x128");
        }

        /// <summary>
        /// THE SECOND GAME'S CONVERSATION PIECES (UW2 plan stage U7 step 3a): the sizes; the top's portrait boxes black
        /// inside (not cleared through the edge), its corner clear; the parchment's paper opaque, the black round it
        /// clear, lower and taller ones keeping both rims; the answers' scroll at any width with both knobs opaque; the
        /// same pictures every time.
        /// </summary>
        private static void fCheckUw2ConversationPieces(CheckContext pOContext)
        {
            Check.Group("The second game's conversation pieces (UW2 plan stage U7 step 3a)");

            // THE LOOK PER GAME (step 3b): the first game's places as the constants the conversation kept before; the
            // second game's inside its pieces.
            UWConversationLook lOOne = UWConversationLook.Of(null);
            Check.Str("conversation look: the first game's places as before (block, parchment, answers, history, names, portraits, first slots)",
                lOOne.Width + "x" + lOOne.HeaderRows + " " + lOOne.ParchmentX + "/" + lOOne.ParchmentTop + "/" + lOOne.ParchmentRows + " "
                + lOOne.AnswersX + "/" + lOOne.AnswersTop + "/" + lOOne.AnswersRows + "/" + lOOne.AnswerTextTop + "/" + lOOne.AnswerBottom + " "
                + lOOne.TextX + "/" + lOOne.TextTop + "/" + lOOne.TextWidth + "/" + lOOne.TextRows + " "
                + lOOne.NameX[0] + "," + lOOne.NameX[1] + "/" + lOOne.NameWidth + " " + lOOne.PortraitX[0] + "," + lOOne.PortraitX[1] + "/" + lOOne.PortraitTop + " "
                + lOOne.Slots.Length + ":" + lOOne.Slots[0].X + "/" + lOOne.Slots[0].Y + "," + lOOne.Slots[4].X + "/" + lOOne.Slots[4].Y + " " + lOOne.SlotSize,
                "190x47 -1/47/89 -1/134/52/141/8 16/53/159/78 5,101/88 2,154/11 8:48/11,105/11 16");

            UWConversationLook lOTwo = UWConversationLook.Uw2;
            bool lbInside = lOTwo.TextX >= lOTwo.ParchmentX && lOTwo.TextX + lOTwo.TextWidth <= lOTwo.ParchmentX + lOTwo.Width
                && lOTwo.TextTop >= lOTwo.ParchmentTop && lOTwo.TextTop + lOTwo.TextRows <= lOTwo.ParchmentTop + lOTwo.ParchmentRows
                && lOTwo.AnswerTextX + lOTwo.AnswerTextWidth <= lOTwo.Width && lOTwo.AnswersTop == lOTwo.ParchmentTop + lOTwo.ParchmentRows;

            foreach ((int X, int Y) lySlot in lOTwo.Slots)
                lbInside &= lySlot.X >= 0 && lySlot.X + lOTwo.SlotSize <= lOTwo.Width && lySlot.Y + lOTwo.SlotSize <= lOTwo.HeaderRows;

            Check.Bool("conversation look: the second game's texts, answers and slots inside their pieces", lbInside, true);
            Check.Str("conversation look: the second game's slots (first, second, third of each side), names and portraits",
                lOTwo.Slots.Length + ": " + lOTwo.Slots[0].X + "/" + lOTwo.Slots[0].Y + " " + lOTwo.Slots[1].X + "/" + lOTwo.Slots[1].Y + " "
                + lOTwo.Slots[2].X + "/" + lOTwo.Slots[2].Y + " " + lOTwo.Slots[6].X + "/" + lOTwo.Slots[6].Y + " | "
                + lOTwo.NameX[0] + "/" + lOTwo.NameTop[0] + " " + lOTwo.NameX[1] + "/" + lOTwo.NameTop[1] + " " + lOTwo.NameRight[1] + " | "
                + lOTwo.PortraitX[0] + "," + lOTwo.PortraitX[1] + "/" + lOTwo.PortraitTop,
                "12: 75/13 94/13 75/31 124/13 | 71/69 72/1 True | 2,168/3");

            // The block in Classic Wide (step 3c, variant b): the parchment lower so the block fits the frame's 200 rows, the
            // history shorter by as much, the answers right under it at the block's height; nothing else changed.
            UWConversationLook lOWideLook = UWConversationLook.Uw2Wide;
            Check.Str("conversation look: the second game's Classic Wide block (parchment, history, answers, height, width)",
                lOWideLook.ParchmentTop + "+" + lOWideLook.ParchmentRows + " text " + lOWideLook.TextTop + "+" + lOWideLook.TextRows + " answers " + lOWideLook.AnswersTop
                + "+" + lOWideLook.AnswersRows + " text " + lOWideLook.AnswerTextTop + " | " + (lOWideLook.AnswersTop + lOWideLook.AnswersRows) + " rows, " + lOWideLook.Width + " wide | "
                + (lOWideLook.Slots.Length == lOTwo.Slots.Length && lOWideLook.AnswerTextWidth == lOTwo.AnswerTextWidth),
                "77+70 text 81+63 answers 147+52 text 150 | 199 rows, 234 wide | True");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2", "no second game here: skipped");

                return;
            }

            UWTextures lOTextures = new UWTextures(lsData, new UWPalettes(lsData), UWGame.Uw2);
            UWPicture lOHeader = UWConversationArtBuilder.BuildHeaderUw2(lOTextures);
            UWPicture lOParchment = UWConversationArtBuilder.BuildParchmentUw2(lOTextures, UWClassicLayoutUw2.Conversation.Parchment.Height);
            UWPicture lOLow = UWConversationArtBuilder.BuildParchmentUw2(lOTextures, 52);
            UWPicture lOAnswers = UWConversationArtBuilder.BuildAnswersUw2(lOTextures, lOHeader.Width);
            UWPicture lOWide = UWConversationArtBuilder.BuildAnswersUw2(lOTextures, 400);

            Check.Str("uw2 conversation: the pieces' sizes (top, parchment, lower, answers narrow and wide)",
                lOHeader.Width + "x" + lOHeader.Height + " " + lOParchment.Width + "x" + lOParchment.Height + " " + lOLow.Height + " "
                + lOAnswers.Width + "x" + lOAnswers.Height + " " + lOWide.Width, "234x77 206x85 52 234x33 400");

            // The portraits' boxes: black inside, opaque; the portraits are drawn there.
            int liNpc = UWClassicLayoutUw2.Conversation.NpcPortraitLeft;
            int liPlayer = UWClassicLayoutUw2.Conversation.PlayerPortraitLeft;
            int liBoxRow = 30;
            int liClearInHeader = 0;

            for (int y = 0; y < lOHeader.Height; y++)
                for (int x = 0; x < lOHeader.Width; x++)
                    liClearInHeader += lOHeader.Get(x, y).A == 0 ? 1 : 0;

            Check.Int("uw2 conversation: the top a closed rectangle, nothing clear (the black between the grids too)", liClearInHeader, 0);
            Check.Bool("uw2 conversation: both portrait boxes black and opaque inside",
                lOHeader.Get(liNpc + 20, liBoxRow).A != 0 && lOHeader.Get(liNpc + 20, liBoxRow).R + lOHeader.Get(liNpc + 20, liBoxRow).G < 16
                && lOHeader.Get(liPlayer + 20, liBoxRow).A != 0 && lOHeader.Get(liPlayer + 20, liBoxRow).R + lOHeader.Get(liPlayer + 20, liBoxRow).G < 16, true);

            UWColor32 lOPaper = lOParchment.Get(lOParchment.Width / 2, lOParchment.Height / 2);
            Check.Bool("uw2 conversation: the parchment's paper opaque, its top-left corner and top row's black clear",
                lOPaper.A != 0 && lOPaper.R > lOPaper.B && lOParchment.Get(0, 0).A == 0, true);

            int liRimWrong = 0;

            for (int y = 0; y < UWClassicLayoutUw2.Conversation.ParchmentRim; y++)
            {
                for (int x = 0; x < lOLow.Width; x++)
                {
                    liRimWrong += fSame(lOLow.Get(x, y), lOParchment.Get(x, y)) ? 0 : 1;
                    liRimWrong += fSame(lOLow.Get(x, lOLow.Height - 1 - y), lOParchment.Get(x, lOParchment.Height - 1 - y)) ? 0 : 1;
                }
            }

            Check.Int("uw2 conversation: a lower parchment keeps both rims", liRimWrong, 0);

            int liKnobsWrong = 0;

            foreach (UWPicture lOScroll in new[] { lOAnswers, lOWide })
            {
                for (int y = 0; y < lOScroll.Height; y++)
                {
                    for (int x = 0; x < UWClassicLayoutUw2.Conversation.AnswersNarrowFrom; x++)
                    {
                        UWPicture lOFull = UWConversationArtBuilder.BuildAnswersUw2(lOTextures, UWClassicLayoutUw2.Conversation.AnswersScroll.Width);

                        liKnobsWrong += fSame(lOScroll.Get(x, y), lOFull.Get(x, y)) ? 0 : 1;
                        liKnobsWrong += fSame(lOScroll.Get(lOScroll.Width - 1 - x, y), lOFull.Get(lOFull.Width - 1 - x, y)) ? 0 : 1;
                    }
                }
            }

            Check.Int("uw2 conversation: the answers' scroll keeps both ends at any width", liKnobsWrong, 0);
            Check.Bool("uw2 conversation: the same pieces every time",
                fCrc32(lOHeader) == fCrc32(UWConversationArtBuilder.BuildHeaderUw2(lOTextures))
                && fCrc32(lOParchment) == fCrc32(UWConversationArtBuilder.BuildParchmentUw2(lOTextures, UWClassicLayoutUw2.Conversation.Parchment.Height)), true);
        }

        private static bool fSameCuts(int[][] piRows, int[,] piTable)
        {
            if (piRows.Length != piTable.GetLength(0))
                return false;

            for (int liRow = 0; liRow < piRows.Length; liRow++)
            {
                if (piRows[liRow].Length != piTable.GetLength(1))
                    return false;

                for (int liAt = 0; liAt < piRows[liRow].Length; liAt++)
                {
                    if (piRows[liRow][liAt] != piTable[liRow, liAt])
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// THE SECOND GAME'S COMPASS AND HOLLOW from its files (UW2 plan stage U6 step 1): the box of its faces and needles,
        /// where it lies on the recess and the gem on it, the disc's and the hollow's sizes; each step's compass the box's
        /// size, the freed one with fewer pixels than the whole one.
        /// </summary>
        private static void fCheckUw2CompassPieces(CheckContext pOContext)
        {
            Check.Group("The second game's compass, gem and hollow (UW2 plan stage U6 step 1)");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2", "no second game here: skipped");

                return;
            }

            UWTextures lOTextures = new UWTextures(lsData, new UWPalettes(lsData), UWGame.Uw2);
            UWRectInt lOBox = UWCompassDiscBuilder.Uw2CompassBox(lOTextures);

            UWCompassGeometry.Uw2.CrossOnDisc(lOTextures, out int liCrossX, out int liCrossY);
            UWCompassGeometry.Uw2.GemOnCross(lOTextures, out int liGemX, out int liGemY);

            Check.Str("uw2: the faces' and needles' box (a needle a column left of the face)",
                lOBox.X + "," + lOBox.Y + " " + lOBox.Width + "x" + lOBox.Height, "93,148 53x16");
            Check.Str("uw2: the compass on the disc and the gem on the compass", liCrossX + "," + liCrossY + " " + liGemX + "," + liGemY, "10,4 20,3");

            UWPicture lODisc = UWCompassDiscBuilder.Build(lOTextures, true);
            UWPicture lOHollow = UWFrameCutBuilder.BuildRuneHollow(lOTextures, false);

            UWFrameCutBuilder.RuneHollowRunes(lOTextures, out int liRuneX, out int liRuneY, out int liPitch);
            Check.Str("uw2: the disc with its outline's room, the hollow, its runes",
                lODisc.Width + "x" + lODisc.Height + " " + lOHollow.Width + "x" + lOHollow.Height + " " + liRuneX + "," + liRuneY + "+" + liPitch,
                "74x23 50x18 5,1+14");

            int liWrong = 0;

            for (int liStep = 0; liStep < 16; liStep++)
            {
                UWPicture lOWhole = UWCompassDiscBuilder.ComposeUw2Compass(lOTextures, liStep, false);
                UWPicture lOFreed = UWCompassDiscBuilder.ComposeUw2Compass(lOTextures, liStep, true);

                if (lOWhole == null || lOFreed == null || lOWhole.Width != lOBox.Width || lOWhole.Height != lOBox.Height
                    || fOpaqueCount(lOFreed) == 0 || fOpaqueCount(lOFreed) >= fOpaqueCount(lOWhole))
                    liWrong++;
            }

            Check.Int("uw2: every step's compass the box's size, the freed one less than the whole", liWrong, 0);

            // THE LEDGE AS THE STONE SHELF (stage U6 step 2a): the ledge's rows, its two ends as they are, the same shelf
            // every time, a wider one the narrower one and more on the right - but for the right end and the two columns grown
            // against it (their surroundings take the end in).
            UWPicture lOMain = UWMainFrame.Picture(lOTextures);
            UWPicture lOLedge = lOMain.Crop(UWClassicLayoutUw2.Ledge.Place);
            UWPicture lONarrow = UWStoneShelfBuilder.Build(lOTextures, 1);
            UWPicture lOShelf = UWStoneShelfBuilder.Build(lOTextures, 140);
            UWPicture lOWider = UWStoneShelfBuilder.Build(lOTextures, 200);
            int liEndsWrong = 0;
            int liGrowsWrong = 0;

            for (int liY = 0; liY < lOLedge.Height; liY++)
            {
                for (int liX = 0; liX < UWClassicLayoutUw2.Ledge.LeftCap; liX++)
                    liEndsWrong += fSame(lOShelf.Get(liX, liY), UWPixelOps.Opaque(lOLedge.Get(liX, liY))) ? 0 : 1;

                for (int liX = 0; liX < UWClassicLayoutUw2.Ledge.RightCap; liX++)
                    liEndsWrong += fSame(lOShelf.Get(lOShelf.Width - 1 - liX, liY), UWPixelOps.Opaque(lOLedge.Get(lOLedge.Width - 1 - liX, liY))) ? 0 : 1;

                for (int liX = 0; liX < lOShelf.Width - UWClassicLayoutUw2.Ledge.RightCap - 2; liX++)
                    liGrowsWrong += fSame(lOShelf.Get(liX, liY), lOWider.Get(liX, liY)) ? 0 : 1;
            }

            Check.Str("uw2 ledge: the shelf's sizes (the narrowest its ends and four columns)",
                lONarrow.Width + "x" + lONarrow.Height + " " + lOShelf.Width + "x" + lOShelf.Height, UWStoneShelfBuilder.Uw2MinWidth + "x21 140x21");
            Check.Int("uw2 ledge: both ends as the ledge has them", liEndsWrong, 0);
            Check.Int("uw2 ledge: a wider shelf grows on the right", liGrowsWrong, 0);
            Check.Bool("uw2 ledge: the same shelf every time", fCrc32(lOShelf) == fCrc32(UWStoneShelfBuilder.Build(lOTextures, 140)), true);

            // THE SCROLL (stage U6 step 2b): knob to knob, the first game's rows; the paper in its colour from column 8, the
            // ground between the knobs clear, the knobs opaque; taller, wider and narrower by the first game's rules.
            UWPicture lOScroll = UWScrollPaperBuilder.Build(lOTextures, null, 0, 0, 0);
            UWPicture lOTaller = UWScrollPaperBuilder.Build(lOTextures, null, 12, 2, 0);
            UWPicture lOWideScroll = UWScrollPaperBuilder.Build(lOTextures, null, 0, 0, 64);
            UWPicture lONarrowScroll = UWScrollPaperBuilder.Build(lOTextures, null, 0, 0, -96);
            UWColor32 lOPaper = UWPixelOps.Opaque(new UWPalettes(lsData).GetPalette(0).GetUWColor(UWClassicLayoutUw2.MessageLog.PaperColour));
            Check.Str("uw2 scroll: its sizes, taller, wider, narrower",
                lOScroll.Width + "x" + lOScroll.Height + " " + lOTaller.Width + "x" + lOTaller.Height + " " + lOWideScroll.Width + " " + lONarrowScroll.Width,
                "224x33 224x45 288 128");
            Check.Bool("uw2 scroll: the paper in its colour from column 8 and row 2, the ground clear, the knob opaque",
                fSame(lOScroll.Get(UWScrollPaperBuilder.Uw2PaperLeft + 10, 12), lOPaper)
                && lOScroll.Get(2, 13).A == 0 && lOScroll.Get(1, 4).A != 0, true);
            // THE BACKS (stage U6 step 2d): six shades each from the second game's own frame, the leather and the bronze warm,
            // the marble bluish, the stone grey; the patterns its walls 209, 62, 88.
            string lsRamps = string.Empty;

            foreach (UWBackdropArt.ColourEnum leColour in new[] { UWBackdropArt.ColourEnum.Leather, UWBackdropArt.ColourEnum.Marble,
                UWBackdropArt.ColourEnum.Stone, UWBackdropArt.ColourEnum.Bronze })
            {
                List<UWColor32> lORamp = UWBackdropArt.Ramp(lOTextures, leColour);
                UWColor32 lOLight = lORamp.Count > 0 ? lORamp[lORamp.Count - 1] : default;
                string lsKind = lOLight.R > lOLight.B + 30 ? "warm" : lOLight.B > lOLight.R ? "blue" : "grey";

                lsRamps += leColour + " " + lORamp.Count + " " + lsKind + ";";
            }

            Check.Str("uw2 backs: the ramps from its own frame", lsRamps, "Leather 6 warm;Marble 6 blue;Stone 6 grey;Bronze 6 warm;");
            Check.Str("uw2 backs: the patterns' walls", string.Join(",", UWClassicLayoutUw2.BackSources.PatternWalls), "-1,209,62,88");
            Check.Str("uw2 scroll: the log's colours its own (font_color 0x76, spec_col)",
                string.Join(",", UWMessageLogModel.ColourIndicesOf(UWGame.Uw2)) + " " + string.Join(",", UWMessageLogModel.ColourIndicesOf(UWGame.Uw1)),
                "118,104,1,2,33,80,72 46,38,241,96,180,196,212");
        }

        private static int fOpaqueCount(UWPicture pOPicture)
        {
            int liCount = 0;

            foreach (UWColor32 lOPixel in pOPicture.Pixels)
                liCount += lOPixel.A != 0 ? 1 : 0;

            return liCount;
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
