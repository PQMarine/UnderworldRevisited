using System;
using System.Collections.Generic;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// EVERY COMPOSED HUD PICTURE (UWHudArt, UWBackdropArt) over a grid of sizes and options,
        /// hashed, against Pins/hud-art-hashes.txt. The user confirmed these pictures pixel by
        /// pixel (the cloud leather, the freed paperdoll, the stone shelf seams, the Classic Wide
        /// cuts, the compass disc), so the split of UWHudArt into builder classes (cleanup 9.11) had
        /// to keep every hash. The hashes are of our composed pictures, not of the game's art. The
        /// builders' Compose cores on synthetic pictures: the HudArtCompose group.
        /// </summary>
        private static void fCheckHudArt(CheckContext pOContext)
        {
            Check.Group("HUD art hashes (UWHudArt and UWBackdropArt over a grid of sizes)");

            DataImport lOData = pOContext.Data;
            UWTextures lOTextures = lOData.Textures;
            SortedDictionary<string, string> lONow = new SortedDictionary<string, string>(StringComparer.Ordinal);

            void fAdd(string psName, UWPicture pOPicture, string psExtra = null)
            {
                lONow[psName] = pOPicture == null
                    ? "null"
                    : string.Format("{0}x{1} {2:X8}{3}", pOPicture.Width, pOPicture.Height, fCrc32(pOPicture), psExtra != null ? " " + psExtra : "");
            }

            void fAddMask(string psName, bool[] pbMask)
            {
                lONow[psName] = pbMask == null ? "null" : string.Format("{0} {1:X8}", pbMask.Length, fCrc32(pbMask));
            }

            void fTry(string psName, Action pOBuild)
            {
                try
                {
                    pOBuild();
                }
                catch (Exception lOException)
                {
                    lONow[psName] = "<threw " + lOException.GetType().Name + ">";
                }
            }

            (int W, int H)[] lOLeatherSizes = { (1, 1), (8, 8), (9, 9), (20, 12), (44, 24), (85, 41), (100, 60), (160, 41), (236, 80), (300, 200), (320, 24) };

            foreach ((int liWidth, int liHeight) in lOLeatherSizes)
                for (int liVariant = 0; liVariant < 3; liVariant++)
                {
                    string lsName = string.Format("leather: {0}x{1} v{2}", liWidth, liHeight, liVariant);

                    fTry(lsName, () => fAdd(lsName, UWHudArt.BuildLeather(lOTextures, liWidth, liHeight, liVariant)));
                }

            foreach ((int liWidth, int liHeight) in new[] { (20, 12), (85, 41), (236, 80) })
            {
                string lsName = string.Format("leather tab: {0}x{1}", liWidth, liHeight);

                fTry(lsName, () => fAdd(lsName, UWHudArt.BuildLeatherTab(lOTextures, liWidth, liHeight)));
            }

            foreach ((int liWidth, int liOpenHalf) in new[] { (44, 46), (120, 60), (200, 46), (320, 100) })
            {
                string lsName = string.Format("heading strip: {0} open {1}", liWidth, liOpenHalf);

                fTry(lsName, () => fAdd(lsName, UWHudArt.BuildHeadingStrip(lOTextures, liWidth, liOpenHalf)));
            }

            foreach (int liWidth in new[] { 120, 320 })
            {
                string lsName = "heading strip: " + liWidth + " brand centred";

                fTry(lsName, () => fAdd(lsName, UWHudArt.BuildHeadingStripWithBrandAt(lOTextures, liWidth, UWHudArt.BrandCentredLeft(liWidth))));
            }

            fTry("slot: circle", () => fAdd("slot: circle", UWHudArt.BuildSlotCircle(lOTextures)));
            fTry("slot: ring plain", () => fAdd("slot: ring plain", UWHudArt.BuildSlotRing(lOTextures)));
            fTry("paperdoll: page", () => fAdd("paperdoll: page", UWHudArt.BuildPaperdollPage(lOTextures)));
            fTry("paperdoll: marks plain", () => fAdd("paperdoll: marks plain", UWHudArt.BuildPaperdollMarks(lOTextures)));

            foreach (UWBackdropArt.ColourEnum leColour in Enum.GetValues(typeof(UWBackdropArt.ColourEnum)))
            {
                UWBackdropArt.ColourEnum leAt = leColour;

                fTry("slot: ring " + leAt, () => fAdd("slot: ring " + leAt, UWHudArt.BuildSlotRing(lOTextures, UWBackdropArt.Ramp(lOTextures, leAt))));
                fTry("paperdoll: marks " + leAt, () => fAdd("paperdoll: marks " + leAt, UWHudArt.BuildPaperdollMarks(lOTextures, UWBackdropArt.Ramp(lOTextures, leAt))));
                fTry("backdrop: ramp " + leAt, () =>
                {
                    List<UWColor32> lORamp = UWBackdropArt.Ramp(lOTextures, leAt);
                    UWPicture lOStrip = new UWPicture(Math.Max(1, lORamp.Count), 1);

                    for (int liAt = 0; liAt < lORamp.Count; liAt++)
                        lOStrip.Set(liAt, 0, lORamp[liAt]);

                    fAdd("backdrop: ramp " + leAt, lOStrip);
                });
            }

            List<UWTexture> lOBodies = lOTextures.GetTexturesByType(UWTexture.TextureTypes.BODIES);

            for (int liBody = 0; lOBodies != null && liBody < lOBodies.Count; liBody++)
            {
                int liAt = liBody;
                string lsName = "paperdoll: figure body " + liAt;

                fTry(lsName, () =>
                {
                    UWPicture lOFigure = UWHudArt.ComposePaperdoll(lOTextures, liAt, new List<UWPicture>(), new List<(int X, int Y)>(), out int liX, out int liY);

                    fAdd(lsName, lOFigure, string.Format("at {0},{1}", liX, liY));
                });

                if (liAt < 2)
                {
                    string lsFree = "paperdoll: freed body " + liAt;

                    fTry(lsFree, () => fAdd(lsFree, UWHudArt.FreeOnPage(lOTextures, UWPicture.From(lOBodies[liAt]), UWHudArt.PaperdollBodyX, UWHudArt.PaperdollBodyY)));
                    fTry(lsFree + " all pieces", () => fAdd(lsFree + " all pieces",
                        UWHudArt.FreeOnPage(lOTextures, UWPicture.From(lOBodies[liAt]), UWHudArt.PaperdollBodyX, UWHudArt.PaperdollBodyY, false)));
                }
            }

            foreach (bool lbApart in new[] { false, true })
            {
                string lsName = "shelf: gem " + (lbApart ? "apart" : "on");

                fTry(lsName, () =>
                {
                    UWPicture lOShelf = UWHudArt.BuildShelf(lOTextures, out UWShelfBuilder.ShelfLayout lOLayout, lbApart);

                    fAdd(lsName, lOShelf, string.Format("flasks {0},{1} {2},{3} gem {4},{5}", lOLayout.HealthFlaskX, lOLayout.HealthFlaskY,
                        lOLayout.ManaFlaskX, lOLayout.ManaFlaskY, lOLayout.GemX, lOLayout.GemY));
                });
            }

            fTry("shelf: gem stand", () =>
            {
                UWPicture lOStand = UWHudArt.BuildGemStand(lOTextures, out int liGemX, out int liGemY);

                fAdd("shelf: gem stand", lOStand, string.Format("gem {0},{1}", liGemX, liGemY));
            });

            foreach (int liWidth in new[] { 1, 24, 25, 80, 81, 150, 250 })
            {
                string lsName = "stone shelf: " + liWidth;

                fTry(lsName, () => fAdd(lsName, UWHudArt.BuildStoneShelf(lOTextures, liWidth)));
            }

            foreach (int liExtra in new[] { 0, 1, 2, 63, 100, 201, 480 })
            {
                string lsName = "classic wide: extra " + liExtra;

                fTry(lsName, () =>
                {
                    UWClassicWideBuilder.ClassicWideFrame lOFrame = UWHudArt.BuildClassicWideFrame(lOTextures, liExtra);

                    fAdd(lsName, lOFrame.Picture, string.Format("extra {0} left {1} hole {2},{3} {4}x{5}", lOFrame.Extra, lOFrame.Left,
                        lOFrame.Hole.X, lOFrame.Hole.Y, lOFrame.Hole.Width, lOFrame.Hole.Height));
                });
            }

            fTry("conversation: header", () => fAdd("conversation: header", UWHudArt.BuildConversationHeader(lOTextures)));
            fTry("conversation: header padded", () => fAdd("conversation: header padded", UWHudArt.PadAndOutline(UWHudArt.BuildConversationHeader(lOTextures), false)));
            fTry("conversation: header outlined", () => fAdd("conversation: header outlined", UWHudArt.PadAndOutline(UWHudArt.BuildConversationHeader(lOTextures), true)));

            foreach (int liRows in new[] { 29, 89, 120 })
                foreach (bool lbKnobs in new[] { false, true })
                {
                    string lsName = string.Format("conversation: parchment {0} rows{1}", liRows, lbKnobs ? " knobs" : "");

                    fTry(lsName, () => fAdd(lsName, UWHudArt.BuildConversationParchment(lOTextures, liRows, lbKnobs)));
                }

            foreach (bool lbOutline in new[] { false, true })
            {
                string lsSuffix = lbOutline ? " outlined" : "";

                fTry("rune hollow:" + lsSuffix, () => fAdd("rune hollow:" + lsSuffix, UWHudArt.BuildRuneHollow(lOTextures, lbOutline)));
                fTry("compass: disc" + lsSuffix, () => fAdd("compass: disc" + lsSuffix, UWHudArt.BuildCompassDisc(lOTextures, lbOutline)));
            }

            List<UWTexture> lOCompass = lOTextures.GetTexturesByType(UWTexture.TextureTypes.COMPASS);

            for (int liPicture = 0; lOCompass != null && liPicture < lOCompass.Count && liPicture < 20; liPicture++)
            {
                int liAt = liPicture;
                string lsName = "compass: " + (liAt < 4 ? "cross mask " : "tip keeps ") + liAt;

                fTry(lsName, () =>
                {
                    UWColor32[] lOPixels = lOCompass[liAt].GetUWColor32();
                    bool[] lbKeep;

                    if (liAt < 4)
                        lbKeep = UWHudArt.CompassCrossMask(lOPixels, lOCompass[liAt].Width, lOCompass[liAt].Height);
                    else
                    {
                        lbKeep = new bool[lOPixels.Length];

                        for (int liPixel = 0; liPixel < lOPixels.Length; liPixel++)
                            lbKeep[liPixel] = UWHudArt.CompassTipKeeps(lOPixels[liPixel]);
                    }

                    fAddMask(lsName, lbKeep);
                });
            }

            fTry("flasks: mask", () => fAddMask("flasks: mask", UWHudArt.BuildFlaskMask(lOData.Flasks)));
            fTry("flasks: free mask", () => fAddMask("flasks: free mask", UWHudArt.BuildFreeFlaskMask(lOData.Flasks)));
            fTry("flasks: gem mask", () => fAddMask("flasks: gem mask", UWHudArt.BuildGemMask(lOData.PowerGem)));

            foreach (UWBackdropArt.ShapeEnum leShape in new[] { UWBackdropArt.ShapeEnum.Oval, UWBackdropArt.ShapeEnum.Rect })
                foreach (UWBackdropArt.ColourEnum leColour in Enum.GetValues(typeof(UWBackdropArt.ColourEnum)))
                    foreach (UWBackdropArt.PatternEnum lePattern in Enum.GetValues(typeof(UWBackdropArt.PatternEnum)))
                        foreach (bool lbBorder in new[] { false, true })
                        {
                            UWBackdropArt.ShapeEnum leShapeAt = leShape;
                            UWBackdropArt.ColourEnum leColourAt = leColour;
                            UWBackdropArt.PatternEnum lePatternAt = lePattern;
                            bool lbBorderAt = lbBorder;
                            string lsName = string.Format("backdrop: {0} {1} {2}{3}", leShapeAt, leColourAt, lePatternAt, lbBorderAt ? " border" : "");

                            fTry(lsName, () => fAdd(lsName, UWBackdropArt.Build(lOTextures, 100, 60, leShapeAt, leColourAt, lePatternAt, lbBorderAt)));
                        }

            fTry("backdrop: none", () => fAdd("backdrop: none", UWBackdropArt.Build(lOTextures, 100, 60, UWBackdropArt.ShapeEnum.None,
                UWBackdropArt.ColourEnum.Leather, UWBackdropArt.PatternEnum.Clouds, false)));

            // The pure functions, as sequences.
            fTry("pure: classic wide shift", () =>
            {
                List<int> liShifts = new List<int>();

                foreach (int liExtra in new[] { 0, 63, 480 })
                    for (int liY = 0; liY < 200; liY += 7)
                        for (int liX = 0; liX < 320; liX += 3)
                            liShifts.Add(UWHudArt.ClassicWideShiftX(liX, liY, liExtra));

                lONow["pure: classic wide shift"] = string.Format("{0} {1:X8}", liShifts.Count, fCrc32(liShifts.ToArray()));
            });

            fTry("pure: page column to main", () =>
            {
                int[] liColumns = new int[UWHudArt.PaperdollPageWidth];

                for (int liAt = 0; liAt < liColumns.Length; liAt++)
                    liColumns[liAt] = UWHudArt.PageColumnToMain(liAt);

                lONow["pure: page column to main"] = string.Format("{0} {1:X8}", liColumns.Length, fCrc32(liColumns));
            });

            fTry("pure: compass disc arrows", () =>
            {
                List<int> liArrows = new List<int>();

                for (int liY = 0; liY < 37; liY++)
                    for (int liX = 0; liX < 65; liX++)
                        liArrows.Add(UWHudArt.CompassDiscArrowAt(liX, liY));

                lONow["pure: compass disc arrows"] = string.Format("{0} {1:X8}", liArrows.Count, fCrc32(liArrows.ToArray()));
            });

            // The frame picture is decoded once and shared by every builder (cleanup 9.15): after all
            // of them it is still MAIN.BYT as the file has it, and the same picture each call.
            UWTexture lOMain = lOTextures.GetTextureByType(UWTexture.TextureTypes.MAIN, 0);

            Check.Bool("art: the kept frame picture is MAIN.BYT unchanged after every builder, and kept",
                fCrc32(UWMainFrame.Picture(lOTextures)) == fCrc32(UWPicture.From(lOMain))
                && ReferenceEquals(UWMainFrame.Picture(lOTextures), UWMainFrame.Picture(lOTextures)), true);

            fComparePins(pOContext, "hud-art-hashes.txt", "art", lONow, psName =>
            {
                int liAt = psName.IndexOf(':');

                return liAt > 0 ? psName.Substring(0, liAt) : psName;
            });
        }
    }
}
