using System;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Pictures the rules decide: the grime tint, the palette renderer's light sources, the
    /// scroll edges and the power gem.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>UWGrimeTint (2026-10-07): no tone leaves every index as it is; a tone maps the
        /// ordinary colours to ordinary colours - never to a rotating water or lava index, never to
        /// 0 or the top sixteen - and does change some.</summary>
        private static void fCheckGrimeTint(DataImport pOData)
        {
            UWPalette lOPalette = pOData.Palettes != null ? pOData.Palettes.GetPalette(0) : null;

            if (lOPalette == null)
                return;

            byte[] lyNone = UWGrimeTint.Build(lOPalette, UWGrimeTint.ToneEnum.None);
            bool lbIdentity = true;

            for (int liAt = 0; liAt < lyNone.Length; liAt++)
                lbIdentity &= lyNone[liAt] == liAt;

            fExpectBool("grime tint: none leaves every index", lbIdentity, true);

            byte[] lyDirt = UWGrimeTint.Build(lOPalette, UWGrimeTint.ToneEnum.Dirt);
            bool lbClean = true;
            int liChanged = 0;

            for (int liAt = 1; liAt < 240; liAt++)
            {
                if (UWPaletteRotation.IsRotatingIndex(liAt))
                    continue;

                int liTo = lyDirt[liAt];

                lbClean &= liTo >= 1 && liTo < 240 && !UWPaletteRotation.IsRotatingIndex(liTo);

                if (liTo != liAt)
                    liChanged++;
            }

            fExpectBool("grime tint: dirt stays on ordinary colours", lbClean, true);
            fExpectBool("grime tint: dirt changes colours (" + liChanged + ")", liChanged > 20, true);
        }

        /// <summary>UWPaletteLightMap, the palette renderer's light sources (2026-10-07): a source
        /// falls off by the SHADES.DAT row of its light level, and a solid tile between it and a
        /// point leaves the point dark. On level 1, a tile with a solid one east of it and an open
        /// one beyond.</summary>
        private static void fCheckPaletteLightMap(DataImport pOData)
        {
            UWLevel lOLevel = pOData.Levels != null && pOData.Levels.Count > 0 ? pOData.Levels[0] : null;

            if (lOLevel == null || pOData.Shades == null || !pOData.Shades.IsLoaded)
                return;

            UWPaletteLightMap lOMap = new UWPaletteLightMap();

            lOMap.SetLevel(lOLevel, pOData.Shades);

            byte[] lyRow = pOData.Shades.GetShadeTable(3);

            fExpectInt("light map: light level 3 at its source", (int)Math.Round(lOMap.LevelAt(3f, 0f)), lyRow[0]);
            fExpectInt("light map: light level 3 two tiles off", (int)Math.Round(lOMap.LevelAt(3f, 2f)), lyRow[2]);

            for (int liY = 1; liY < 63; liY++)
            {
                for (int liX = 1; liX < 61; liX++)
                {
                    if (lOLevel.GetTile(liX, liY).TileType != UWTile.TileTypeEnum.open
                        || lOLevel.GetTile(liX + 1, liY).TileType != UWTile.TileTypeEnum.solid
                        || lOLevel.GetTile(liX + 2, liY).TileType != UWTile.TileTypeEnum.open)
                        continue;

                    UWPaletteLightMap.Source[] lOSources =
                    {
                        new UWPaletteLightMap.Source { X = liX, Y = liY, LightLevel = 3f }
                    };

                    lOMap.BuildStatic(lOSources);
                    lOMap.Compose(lOSources);

                    int liPer = UWPaletteLightMap.TexelsPerTile;
                    int liCentre = (liY * liPer) + (liPer / 2);

                    fExpectBool(string.Format("light map: the source's own tile {0}/{1} lit", liX, liY),
                        lOMap.Map[(liCentre * UWPaletteLightMap.Size) + (liX * liPer) + (liPer / 2)] < UWPaletteLightMap.Dark, true);
                    fExpectBool(string.Format("light map: behind the wall at {0}/{1} dark", liX + 2, liY),
                        lOMap.Map[(liCentre * UWPaletteLightMap.Size) + ((liX + 2) * liPer) + (liPer / 2)] == UWPaletteLightMap.Dark, true);
                    return;
                }
            }

            fFail("light map", "no tile with a wall east of it on level 1");
        }

        /// <summary>
        /// THE ROLLING EDGES beside the text (UWScrollEdgeRules). Checks the four strips of
        /// SCRLEDGE.GR against the file, the two loop lengths, and how a scroll is counted.
        /// </summary>
        private static void fCheckScrollEdges(DataImport pOData)
        {
            Check.Group("Scroll edges");

            // Four strips, and they have to be there.
            fExpectInt("edges: the main screen starts at image 0",
                UWScrollEdgeRules.MainLeftFirstImage, 0);
            fExpectInt("edges: its right strip five later",
                UWScrollEdgeRules.MainRightFirstImage, 5);
            fExpectInt("edges: the main loop runs five frames", UWScrollEdgeRules.MainFrames, 5);
            fExpectInt("edges: the conversation starts at image 10",
                UWScrollEdgeRules.ConversationLeftFirstImage, 10);
            fExpectInt("edges: its right strip six later",
                UWScrollEdgeRules.ConversationRightFirstImage, 16);
            fExpectInt("edges: the conversation loop runs six frames",
                UWScrollEdgeRules.ConversationFrames, 6);

            // The two groups differ in height: 29 on the main screen, 27 in the conversation.
            int liMainHeight = fGetScrledgeHeight(pOData, UWScrollEdgeRules.MainLeftFirstImage);
            int liConvHeight = fGetScrledgeHeight(pOData, UWScrollEdgeRules.ConversationLeftFirstImage);

            fExpectInt("edges: the main strip is 29 pixels high", liMainHeight, 29);
            fExpectInt("edges: the conversation strip is 27", liConvHeight, 27);
            fExpectInt("edges: SCRLEDGE.GR holds 22 images",
                pOData.Textures.GetTexturesByType(UWTexture.TextureTypes.SCRLEDGE).Count, 22);

            // Both loops close: one step past the last frame is the first again.
            fExpectInt("edges: the main loop closes",
                UWScrollEdgeRules.GetMainLeftImage(UWScrollEdgeRules.MainFrames),
                UWScrollEdgeRules.MainLeftFirstImage);
            fExpectInt("edges: the conversation loop closes",
                UWScrollEdgeRules.GetConversationRightImage(UWScrollEdgeRules.ConversationFrames),
                UWScrollEdgeRules.ConversationRightFirstImage);
            fExpectInt("edges: four steps stay inside the main strip",
                UWScrollEdgeRules.GetMainRightImage(4), 9);

            // How we notice a scroll: how far the visible window has moved.
            string[] lsFull = { "a", "b", "c", "d", "e" };
            fExpectInt("edges: nothing moved is no scroll",
                UWScrollEdgeRules.GetScrollAmount(lsFull, lsFull), 0);
            fExpectInt("edges: one line out at the top is one scroll",
                UWScrollEdgeRules.GetScrollAmount(lsFull, new[] { "b", "c", "d", "e", "f" }), 1);
            fExpectInt("edges: three lines at once count three",
                UWScrollEdgeRules.GetScrollAmount(lsFull, new[] { "d", "e", "f", "g", "h" }), 3);
            fExpectInt("edges: a box still filling up does not scroll",
                UWScrollEdgeRules.GetScrollAmount(new[] { "a", "b" }, new[] { "a", "b", "c" }), 0);
            fExpectInt("edges: a cleared box does not turn the strip",
                UWScrollEdgeRules.GetScrollAmount(lsFull, new string[0]), 0);
        }

        private static int fGetScrledgeHeight(DataImport pOData, int piIndex)
        {
            UWTexture lOTexture = pOData.Textures.GetTextureByType(UWTexture.TextureTypes.SCRLEDGE, piIndex);

            return lOTexture == null ? 0 : lOTexture.Height;
        }

        /// <summary>
        /// The power gem while a blow is charged: one plus the charge over twelve, and with a
        /// RANGED weapon no charge at all - frame 9, the first green one, the moment the
        /// crosshair comes up (seg022_230E_1276 and 1294). Read on 2026-09-22 after the user
        /// had seen the sling jump from red to green in 2026-09-10.
        /// </summary>
        private static void fCheckPowerGem()
        {
            Check.Group("Power gem");

            fExpectInt("gem: an uncharged blow is the red frame 1", UWPowerGem.GetFrame(0), 1);
            fExpectInt("gem: the first yellow starts at twelve", UWPowerGem.GetFrame(12), 2);
            fExpectInt("gem: just before it is still red", UWPowerGem.GetFrame(11), 1);
            fExpectInt("gem: the last yellow is frame 8", UWPowerGem.GetFrame(84), 8);
            fExpectInt("gem: green begins at 96", UWPowerGem.GetFrame(96), 9);
            fExpectInt("gem: a full charge is green too", UWPowerGem.GetFrame(UWCombat.FullCharge), 9);

            fExpectInt("gem: a ranged weapon is red until the crosshair",
                UWPowerGem.GetRangedFrame(false), 1);
            fExpectInt("gem: and green from then on, with no yellow in between",
                UWPowerGem.GetRangedFrame(true), 9);
            fExpectInt("gem: which is the same frame a full charge reaches",
                UWPowerGem.GetRangedFrame(true), UWPowerGem.GetFrame(UWCombat.FullCharge));
        }
    }
}
