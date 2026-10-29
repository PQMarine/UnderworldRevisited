using System.Collections.Generic;
using System.IO;
using System.Linq;
using UWDataImport.Audio;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S DATA LOADS (Docs/UW2-PLAN.md phase 2 step 1, 2026-10-29): UWGameData built for
    /// the second profile from its DATA folder, each part that differs per game held to the files -
    /// the 256 T64.TR textures for walls and floors, the .GR files by type with UW2's three own names,
    /// PANELS.GR's 79 x 112 panels, the BYT.ARK screens under the first game's screen types in their
    /// palettes, POWER.GR's two pulse pictures, WEAP.DAT's eight records with WEAP.GR's pictures,
    /// TERRAIN.DAT's water and lava classes, CNV.ARK's 102 conversations, SOUNDS.DAT's 49 effects with
    /// a VOC for each digital one, the 17 themes in both music versions, and the creature art: every
    /// type's critter map, every sequence's frames found in the page the renderer would take and
    /// decoded to their size. The counts were measured on the shipped files (2026-10-29; Python over
    /// the same bytes for the .GR counts, WEAP.DAT and T64.TR).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2GameData(CheckContext pOContext)
        {
            Check.Group("The second game's data loads (UW2 plan, phase 2 step 1)");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2 game data", "no second game here: skipped");

                return;
            }

            UWGameData lOData = new UWGameData(lsData, null, UWGameProfiles.Uw2);

            // The textures: one T64.TR serves walls and floors.
            List<UWTexture> lOWalls = lOData.Textures.GetTexturesByType(UWTexture.TextureTypes.WALL);
            List<UWTexture> lOFloors = lOData.Textures.GetTexturesByType(UWTexture.TextureTypes.FLOOR);

            Check.Str("textures: T64.TR's textures (of 264 entries, the 8 at the file's end absent), walls and floors the same list, each 64 x 64",
                lOWalls.Count + " " + ReferenceEquals(lOWalls, lOFloors) + " " + lOWalls.Count(t => t.Width == 64 && t.Height == 64 && t.PaletteIndices.Length == 4096)
                + " " + lOWalls[255].Index, "256 True 256 255");

            // The .GR files by type.
            string lsCounts = string.Join(" ", lOData.Textures.ImageFiles.OrderBy(p => p.Key.ToString()).Select(p => p.Key + ":" + p.Value.Count));

            Check.Str("images: every .GR file by type and its picture count (WEAP.GR as WEAPONS, GEMPT and GHED the second game's own)", lsCounts,
                "ANIMO:64 ARMOR_F:66 ARMOR_M:66 BODIES:10 BUTTONS:108 CHAINS:4 CHARHEAD:168 CHRBTNS:27 COMPASS:20 CONVERSE:6 CURSORS:19 DOORS:13 "
                + "DRAGONS:36 EYES:10 FLASKS:77 GEMPT:17 GENHEAD:64 GHED:64 HEADS:10 INV:7 LFTI:12 OBJECTS:464 OPBTN:8 OPTB:4 OPTBTNS:16 "
                + "PANELS:4 POWER:11 QUESTION:1 SCRLEDGE:22 SPELLS:30 THRREDWIN:2 TMFLAT:16 TMOBJ:54 VIEWS:1 WEAPONS:248");

            List<UWTexture> lOPanels = lOData.Textures.GetTexturesByType(UWTexture.TextureTypes.PANELS);

            Check.Str("panels: PANELS.GR's three panels and the narrow side (79 x 112, 3 x 112)",
                string.Join(" ", lOPanels.Select(t => t.Width + "x" + t.Height)), "79x112 79x112 79x112 3x112");

            // The screens from BYT.ARK.
            List<string> lsScreens = new List<string>();

            foreach (KeyValuePair<UWScreensUw2.ScreenEnum, UWTexture.TextureTypes> lOPair in UWTextures.Uw2ScreenTypes)
            {
                UWTexture lOScreen = lOData.Textures.BitmapFiles.TryGetValue(lOPair.Value, out UWTexture lOFound) ? lOFound : null;

                if (lOScreen == null || lOScreen.PaletteIndices.Length != UWScreensUw2.ScreenWidth * UWScreensUw2.ScreenHeight
                    || lOScreen.MainPaletteIndex != UWScreensUw2.PaletteOf(lOPair.Key))
                    lsScreens.Add(lOPair.Value.ToString());
            }

            Check.Str("screens: BYT.ARK's nine screens under the first game's screen types, 320 x 200, each in its palette",
                lOData.Textures.BitmapFiles.Count + " " + string.Join(" ", lsScreens), "9 ");

            Check.Str("power gem: unpowered, seven charging pictures, the pulse (POWER.GR 11: two)",
                lOData.PowerGem.Powering.Count + " " + lOData.PowerGem.FullyPowered.Count, "7 2");

            // The weapon pictures.
            UWWeaponAnimations lOWeapons = lOData.WeaponAnimations;
            List<string> lsRecords = new List<string>();

            foreach (UWWeaponAnimations.Uw2Record lORecord in lOWeapons.Uw2Records)
            {
                lsRecords.Add(lORecord.PictureCount + "/" + lORecord.Pictures.Count + ":" + string.Join(",", lORecord.FrameTotals));
            }

            Check.Str("weapons: WEAP.DAT's eight records (pictures stored / taken from WEAP.GR : the three swings' frame totals)",
                string.Join(" ", lsRecords), "31/31:9,9,7 31/31:9,9,7 31/31:9,9,7 31/31:7,7,7 31/31:9,9,7 31/31:9,9,7 31/31:9,9,7 31/31:7,7,7");
            Check.Str("weapons: record 0's first steps and places (0x20 0x21 0x22 0x43; 41,102), the left hand's pictures from WEAP.GR 124 on, no first-game swing",
                lOWeapons.Uw2Records[0].Steps[0].ToString("X2") + " " + lOWeapons.Uw2Records[0].Steps[1].ToString("X2") + " "
                + lOWeapons.Uw2Records[0].Steps[2].ToString("X2") + " " + lOWeapons.Uw2Records[0].Steps[3].ToString("X2") + "; "
                + lOWeapons.Uw2Records[0].X[0] + "," + lOWeapons.Uw2Records[0].Y[0] + " " + lOWeapons.Uw2Records[4].Pictures[0].Index + " "
                + (lOWeapons.GetWeaponAnimation(WeaponTypes.RightHandSword) == null), "20 21 22 43; 41,102 124 True");
            Check.Bool("weapons: WEAP.CM's two colour sets", lOData.MiscDataFiles.WeaponAttackPalettes.IsLoaded, true);

            // The terrain.
            int liWater = 0;
            int liLava = 0;
            int liAgree = 0;

            for (int liTexture = 0; liTexture < UWTerrainUw2.TextureCount; liTexture++)
            {
                int liClass = lOData.Terrain.Uw2.Class(liTexture);

                liWater += lOData.Terrain.IsWaterFloor(liTexture) ? 1 : 0;
                liLava += lOData.Terrain.IsLavaFloor(liTexture) ? 1 : 0;
                liAgree += (lOData.Terrain.IsWaterFloor(liTexture) == (liClass == UWTerrainUw2.ClassWater))
                    && (lOData.Terrain.IsLavaFloor(liTexture) == (liClass == UWTerrainUw2.ClassLava)) ? 1 : 0;
            }

            Check.Str("terrain: TERRAIN.DAT's water (13) and lava (3) textures served as Water and Lava, each texture agreeing with its class",
                liWater + " " + liLava + " " + liAgree, "13 3 256");

            // The conversations.
            int liConversations = 0;
            List<string> lsBad = new List<string>();

            for (int liSlot = 0; liSlot < lOData.Conversations.SlotCount; liSlot++)
            {
                if (!lOData.Conversations.HasConversation(liSlot))
                    continue;

                liConversations++;

                UWConversations.Conversation lOConversation = lOData.Conversations.GetConversation(liSlot);

                if (lOConversation == null || !lOData.Strings.Blocks.ContainsKey(lOConversation.StringBlock) || lOConversation.Code.Length == 0)
                    lsBad.Add(liSlot.ToString());
            }

            Check.Str("conversations: CNV.ARK's conversations through UWArk, each with code and its string block",
                liConversations + " " + string.Join(" ", lsBad), "102 ");

            // The sound.
            UWSound lOSound = lOData.Sound;
            List<string> lsNoVoc = new List<string>();
            int liDigital = 0;

            for (int liEffect = 0; liEffect < lOSound.SoundsDat.Effects.Count; liEffect++)
            {
                if (lOSound.SoundsDat.Effects[liEffect].Digital == 0)
                    continue;

                liDigital++;

                UWVoc lOVoc = lOSound.GetEffectVoc(liEffect);

                if (lOVoc == null || !lOVoc.IsLoaded)
                    lsNoVoc.Add(liEffect.ToString());
            }

            Check.Str("sound: SOUNDS.DAT's effects (eight-byte entries), the digital ones, each one's VOC loads",
                lOSound.Effects.Count + " " + liDigital + " " + string.Join(" ", lsNoVoc), "49 29 ");

            int liThemes = 0;

            for (int liTheme = 0; liTheme < 64; liTheme++)
            {
                UWXmi lORoland = lOSound.GetMusicTheme(liTheme, true);
                UWXmi lOOther = lOSound.GetMusicTheme(liTheme, false);

                liThemes += lORoland != null && lORoland.IsLoaded && lOOther != null && lOOther.IsLoaded ? 1 : 0;
            }

            Check.Int("sound: the themes that load in both versions (UWRnn and UWAnn)", liThemes, 17);

            // The creature art.
            fCheckUw2CritterArt(lOData.CritterArtUw2);

            fCheckUw2GameScreen(lOData.Textures.GetTextureByType(UWTexture.TextureTypes.MAIN, 0));
            fCheckUw2Furniture(lOData.Textures);
            fCheckUw2Creation(lOData);
            fCheckUw2ContainerMasks(lOData.ObjectClassProperties);

            Check.Bool("critters: the first game's ASSOC.ANM reader finds nothing in the second game's CRIT", lOData.CritterAnimations.IsLoaded, false);

            // The sprite records' scales (the size comparison of 2026-10-29): UW2.EXE's object records
            // 0x20/0x28 and the critter record 0x18/0x18 at file 0x5EF2C on; UW.EXE's four records 4/5
            // at 0x554E8 on (the words at +0x0A and +0x0C of each 0x0E-byte record).
            string lsUw2Exe = Path.Combine(Path.GetDirectoryName(lsData), UWGameProfiles.Uw2.Executable);

            if (File.Exists(lsUw2Exe))
            {
                byte[] lyExe = File.ReadAllBytes(lsUw2Exe);
                string lsScales = string.Empty;

                for (int liRecord = 0; liRecord < 4; liRecord++)
                {
                    int liAt = 0x5EF2A + (liRecord * 0x0E);

                    lsScales += UWBinary.ReadU16(lyExe, liAt + 0x0A).ToString("X2") + "/" + UWBinary.ReadU16(lyExe, liAt + 0x0C).ToString("X2") + " ";
                }

                Check.Str("critters: UW2.EXE's sprite records' scales (objects, critters, objects, objects) and the creature pixel (0x18 / 0x20)",
                    lsScales + UWCritterArtUw2.CreaturePixelScale.ToString(System.Globalization.CultureInfo.InvariantCulture), "20/28 18/18 20/28 20/28 0.75");
            }
        }

        /// <summary>
        /// THE GAME SCREEN'S WINDOW (phase 3 step 1): BYT.ARK block 4 paints the 3D view's place in
        /// index 0, edged by index 1 (the palette's (0, 0, 4), which the first game's art uses as its
        /// transparency marker - here an opaque colour, UWGameScreenUw2). The view of place_3d_view
        /// (16/16 as drawn, 208 x 128; see UWClassicLayoutUw2.Frame.ViewWindowTop) covers the rest of the
        /// window and 22 pixels of the frame - the gargoyle's teeth in its first two rows (x 110 to 129,
        /// rows 16 and 17), which 3DWIN.GR 0 draws over the view in the original too - and leaves eight of
        /// the window's black beside and under it (x 224, rows 141-143, and row 144), measured 2026-10-29.
        /// </summary>
        private static void fCheckUw2GameScreen(UWTexture pOScreen)
        {
            if (pOScreen == null || pOScreen.PaletteIndices == null)
            {
                Check.Fail("screen: BYT.ARK's game screen (block 4) as MAIN", "absent");

                return;
            }

            byte[] lyPixels = pOScreen.PaletteIndices;
            int liWidth = pOScreen.Width;
            int liLeft = UWClassicLayoutUw2.Frame.ViewWindowLeft;
            int liTop = UWClassicLayoutUw2.Frame.ViewWindowTop;
            int liRight = liLeft + UWClassicLayoutUw2.Frame.ViewWindowWidth - 1;
            int liBottom = liTop + UWClassicLayoutUw2.Frame.ViewWindowHeight - 1;
            int liInside = 0;
            int liBlackOutside = 0;

            for (int liRow = liTop - 1; liRow <= liBottom + 1; liRow++)
            {
                for (int liColumn = liLeft - 1; liColumn <= liRight + 1; liColumn++)
                {
                    byte lyPixel = lyPixels[(liRow * liWidth) + liColumn];
                    bool lbInside = liRow >= liTop && liRow <= liBottom && liColumn >= liLeft && liColumn <= liRight;

                    if (lbInside && lyPixel > 1)
                        liInside++;
                    else if (!lbInside && lyPixel == 0 && liRow != liTop - 1)
                        liBlackOutside++;
                }
            }

            Check.Str("screen: the game screen's pixels in the view other than its black and edge, its black beside or under the view",
                liInside + " " + liBlackOutside, "22 8");
        }

        /// <summary>
        /// THE SCREEN'S FURNITURE (phase 3 step 1 part B): the pictures UWGameScreenUw2 places, at the
        /// sizes UE's calls give them, and the screen's places for the scroll and the command bar left
        /// in index 1 for the game to fill (the paper, the bar's picture).
        /// </summary>
        private static void fCheckUw2Furniture(UWTextures pOTextures)
        {
            string lsSizes = string.Empty;

            foreach (KeyValuePair<UWTexture.TextureTypes, int> lOPicture in new[]
            {
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.COMPASS, 0),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.COMPASS, UWClassicLayoutUw2.Compass.FirstNeedle),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.POWER, 0),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.EYES, 0),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.SCRLEDGE, UWClassicLayoutUw2.MessageLog.EdgeLeftFirst),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.SCRLEDGE, UWClassicLayoutUw2.MessageLog.EdgeRightFirst),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.OPTB, 0),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.OPTB, 1),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.FLASKS, UWClassicLayoutUw2.Flasks.Empty),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.FLASKS, UWClassicLayoutUw2.Flasks.HealthFirst),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.PANELS, 0),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.BODIES, 0),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.ARMOR_M, 0),
                new KeyValuePair<UWTexture.TextureTypes, int>(UWTexture.TextureTypes.OBJECTS, 0x8C)
            })
            {
                UWTexture lOTexture = pOTextures.GetTextureByType(lOPicture.Key, lOPicture.Value);

                lsSizes += (lOTexture != null ? lOTexture.Width + "x" + lOTexture.Height : "none") + " ";
            }

            // OPTB.GR's bar (0x4D x 0x1D in setup_icon_buttons' call), the compass face (change_sprite
            // 0x34 x 0x10), the scroll's edges 4 wide, drawn 0x1C of their rows.
            // Step 2: the empty flask (set_flask's 0x2057) and a slice, the panel's page (show's 0x4F x
            // 0x70), the body (InvDisplay[0]'s 0x24 x 0x45), an armour piece (InvDisplay[3]'s 0x21 x 0x2C),
            // an object (the key ring).
            Check.Str("screen: the furniture's pictures (compass face, first needle, gem, eyes, scroll edges, command bar plain and lit, "
                + "empty flask, flask slice, panel page, body, armour, object)",
                lsSizes.Trim(), "52x16 10x4 14x5 20x3 4x29 4x29 77x29 77x29 24x33 24x4 79x112 36x69 33x44 16x16");

            UWTexture lOScreen = pOTextures.GetTextureByType(UWTexture.TextureTypes.MAIN, 0);

            if (lOScreen == null || lOScreen.PaletteIndices == null)
                return;

            // The inventory page as init_scrgr shows it over the screen: the screen holds it already.
            UWTexture lOPage = pOTextures.GetTextureByType(UWTexture.TextureTypes.PANELS, 0);
            int liDiffering = 0;

            for (int liRow = 0; lOPage != null && liRow < lOPage.Height; liRow++)
            {
                for (int liColumn = 0; liColumn < lOPage.Width; liColumn++)
                {
                    if (lOPage.PaletteIndices[(liRow * lOPage.Width) + liColumn]
                        != lOScreen.PaletteIndices[((UWClassicLayoutUw2.Panel.Top + liRow) * lOScreen.Width) + UWClassicLayoutUw2.Panel.Left + liColumn])
                        liDiffering++;
                }
            }

            Check.Int("screen: the inventory page (PANELS.GR 0) against the screen's panel at 235/7 (differing pixels)", liDiffering, 0);

            // Step 2 part B: the statistics page and the chain's four pictures of the slide; the last
            // stays drawn after it, against the chain the screen shows at rest.
            UWTexture lOStats = pOTextures.GetTextureByType(UWTexture.TextureTypes.PANELS, 2);
            string lsChains = string.Empty;

            for (int liChain = 0; liChain < 4; liChain++)
            {
                UWTexture lOChain = pOTextures.GetTextureByType(UWTexture.TextureTypes.CHAINS, liChain);

                lsChains += (lOChain != null ? lOChain.Width + "x" + lOChain.Height : "none") + " ";
            }

            UWTexture lOLast = pOTextures.GetTextureByType(UWTexture.TextureTypes.CHAINS, 3);
            int liChainDiffering = 0;

            for (int liRow = 0; lOLast != null && liRow < lOLast.Height; liRow++)
            {
                for (int liColumn = 0; liColumn < lOLast.Width; liColumn++)
                {
                    byte lyPixel = lOLast.PaletteIndices[(liRow * lOLast.Width) + liColumn];

                    if (lyPixel != 0 && lyPixel != lOScreen.PaletteIndices[((UWClassicLayoutUw2.PanelSlide.ChainTop + liRow) * lOScreen.Width)
                        + UWClassicLayoutUw2.PanelSlide.ChainLeft + liColumn])
                        liChainDiffering++;
                }
            }

            // Step 2 part C: 3DWIN.GR 0's two rows are the screen's view rows 16 and 17 from x 110 (its
            // index 0 left out), and a weapon picture holds WEAP.CM's sixteen indices (the ready sword,
            // right hand).
            UWTexture lOTeeth = pOTextures.GetTextureByType(UWTexture.TextureTypes.THRREDWIN, 0);
            int liTeethDiffering = 0;
            int liTeethLeft = UWClassicLayoutUw2.Frame.ViewWindowLeft + UWClassicLayoutUw2.ViewOverlay.TeethX;
            int liTeethTop = UWClassicLayoutUw2.Frame.ViewWindowTop + UWClassicLayoutUw2.ViewOverlay.TopY - UWClassicLayoutUw2.ViewOverlay.TeethY;

            for (int liRow = 0; lOTeeth != null && liRow < lOTeeth.Height; liRow++)
            {
                for (int liColumn = 0; liColumn < lOTeeth.Width; liColumn++)
                {
                    byte lyPixel = lOTeeth.PaletteIndices[(liRow * lOTeeth.Width) + liColumn];

                    if (lyPixel != 0 && lyPixel != lOScreen.PaletteIndices[((liTeethTop + liRow) * lOScreen.Width) + liTeethLeft + liColumn])
                        liTeethDiffering++;
                }
            }

            UWTexture lOSword = pOTextures.GetTextureByType(UWTexture.TextureTypes.WEAPONS, UWClassicLayoutUw2.Weapon.PicturesPerHand + 27);
            int liHighest = 0;

            for (int liAt = 0; lOSword != null && liAt < lOSword.Width * lOSword.Height; liAt++)
                liHighest = System.Math.Max(liHighest, lOSword.PaletteIndices[liAt]);

            Check.Str("screen: the teeth (3DWIN.GR 0) against the view's first two rows (differing), the ready sword's size and highest index",
                (lOTeeth != null ? lOTeeth.Width + "x" + lOTeeth.Height : "none") + " " + liTeethDiffering + " "
                + (lOSword != null ? lOSword.Width + "x" + lOSword.Height : "none") + " " + liHighest, "20x2 0 10x31 15");

            Check.Str("screen: the statistics page's picture, the chain's four pictures, the last chain against the screen (differing pixels)",
                (lOStats != null ? lOStats.Width + "x" + lOStats.Height : "none") + " " + lsChains + liChainDiffering, "79x112 15x32 15x32 15x32 15x32 0");

            // The scroll's last row (198) is painted in the paper already, but for its two ends (x 16 and
            // 223, a darker brown: the paper's corners); the rest is left in index 1.
            Check.Str("screen: the scroll's place in the screen other than index 1 or the paper, the command bar's other than index 1 (pixels)",
                fNotIndex(lOScreen, UWClassicLayoutUw2.MessageLog.Left, UWClassicLayoutUw2.MessageLog.Top,
                    UWClassicLayoutUw2.MessageLog.Width, UWClassicLayoutUw2.MessageLog.Height, 1, (byte)UWClassicLayoutUw2.MessageLog.PaperColour) + " "
                + fNotIndex(lOScreen, UWClassicLayoutUw2.Commands.Left, UWClassicLayoutUw2.Commands.Top, 77, 29, 1, 1),
                "2 0");

            // The options panel (phase 3 step 4): the open bar, a group's unlit and lit pictures, the
            // message pictures, as UWClassicLayoutUw2.Options cuts them.
            string lsOptions = string.Empty;

            foreach (int liPicture in new[] { UWClassicLayoutUw2.Options.OpenBarPicture, UWClassicLayoutUw2.Options.FirstUnlitPicture,
                UWClassicLayoutUw2.Options.FirstLitPicture + 4, 13, UWClassicLayoutUw2.Options.SoundTitlePicture, 15 })
            {
                UWTexture lOPicture = pOTextures.GetTextureByType(UWTexture.TextureTypes.OPTBTNS, liPicture);

                lsOptions += (lOPicture != null ? lOPicture.Width + "x" + lOPicture.Height : "none") + " ";
            }

            Check.Str("screen: the options' open bar, a group's unlit and lit pictures, the three message pictures (OPTBTNS.GR 2, 3, 12, 13, 14, 15)",
                lsOptions.Trim(), "77x29 80x116 80x116 80x65 80x65 80x65");
        }

        /// <summary>
        /// THE SECOND GAME'S CREATION (phase 3 step 5): its button pictures (the lit ones beside them), the
        /// first game's model on the second game's CHRGEN.DAT and SKILLS.DAT - the questions' answers and a
        /// fighter's base attributes and first skill choice.
        /// </summary>
        private static void fCheckUw2Creation(UWGameData pOData)
        {
            string lsSizes = string.Empty;

            foreach (int liPicture in new[] { 0, 1, 2, 3, 4, 6, 7, 0x11, 0x16 })
            {
                UWTexture lOPicture = pOData.Textures.GetTextureByType(UWTexture.TextureTypes.CHRBTNS, liPicture);

                lsSizes += (lOPicture != null ? lOPicture.Width + "x" + lOPicture.Height : "none") + " ";
            }

            Check.Str("creation: CHRBTNS.GR 0, 1, 2, 3, 4, 6, 7, 0x11, 0x16", lsSizes.Trim(),
                "67x16 67x16 67x16 35x35 35x35 145x16 35x35 31x68 27x67");

            UWCharacterGeneration lOGeneration = new UWCharacterGeneration(pOData.Strings, pOData.MiscDataFiles);
            string lsTrace = lOGeneration.IsLoaded + " |";

            lOGeneration.Begin();
            lsTrace += lOGeneration.QuestionText + " " + string.Join("/", lOGeneration.Choices) + " |";
            lOGeneration.Choose(0);
            lsTrace += string.Join("/", lOGeneration.Choices) + " |";
            lOGeneration.Choose(0);
            lsTrace += lOGeneration.Choices.Count + " " + lOGeneration.Choices[0] + " |";

            pOData.MiscDataFiles.SkillChoices.TryGetBaseAttributes(0, out int liStrength, out int liDexterity, out int liIntelligence, out int liBonus);
            lsTrace += liStrength + " " + liDexterity + " " + liIntelligence + " " + liBonus;

            Check.Str("creation: loaded | sex | hands | classes | the fighter's base and bonus", lsTrace,
                "True |Choose character sex: Male/Female |Right/Left |8 Fighter |20 16 12 12");

            // Part B: a new character's PLAYER.DAT from fixed choices.
            int[] liSkills = new int[UWPlayerData.SkillCount];

            liSkills[1] = 8;
            UWPlayerFile lONew = UWPlayerRecordUw2.NewCharacter(new UWPlayerData.NewCharacter
            {
                Name = "Tester", CharacterClass = 5, Body = 3, IsFemale = true, IsLeftHanded = true, Difficulty = 1,
                Strength = 25, Dexterity = 15, Intelligence = 14, Attack = 9, Defense = 7, Skills = liSkills, MaxVitality = 35, MaxMana = 3
            });
            UWPlayerFile lOBack = UWPlayerFile.Read(lONew.ToBytes(), UWGame.Uw2);

            Check.Str("creation: the player object's bytes | the new file read back: name, class, body, female, lefty, attributes, "
                + "vitality (workspace), maximum, mana, level, weight limit, skills 0-3, clock, easy, links",
                System.BitConverter.ToString(lOBack.Workspace, 0, UWPlayerRecordUw2.PlayerObjectSize) + " | " + lOBack.Name + " "
                + UWPlayerRecordUw2.CharacterClass(lOBack) + " " + UWPlayerRecordUw2.Body(lOBack) + " " + UWPlayerRecordUw2.IsFemale(lOBack) + " "
                + UWPlayerRecordUw2.IsLeftHanded(lOBack) + " " + UWPlayerRecordUw2.Strength(lOBack) + "/" + UWPlayerRecordUw2.Dexterity(lOBack) + "/"
                + UWPlayerRecordUw2.Intelligence(lOBack) + " " + UWPlayerRecordUw2.Vitality(lOBack) + " " + UWPlayerRecordUw2.MaxVitality(lOBack) + " "
                + UWPlayerRecordUw2.Mana(lOBack) + " " + UWPlayerRecordUw2.CharacterLevel(lOBack) + " " + UWPlayerRecordUw2.MaxWeight(lOBack) + " "
                + UWPlayerRecordUw2.Skill(lOBack, 0) + "," + UWPlayerRecordUw2.Skill(lOBack, 1) + "," + UWPlayerRecordUw2.Skill(lOBack, 2) + ","
                + UWPlayerRecordUw2.Skill(lOBack, 3) + " " + UWBinary.Read32(lOBack.Record, UWPlayerRecordUw2.GameClockOffset).ToString("X") + " "
                + lOBack.Record[UWPlayerRecordUw2.EasyOffset] + " " + (UWBinary.ReadU16(lOBack.Workspace, UWInventoryWorkspace.PlayerLinkOffset) >> 6)
                + " " + lOBack.CountWord + " " + lOBack.Workspace.Length,
                "7F-20-00-6C-00-00-00-00-23-00-00-00-00-00-00-00-00-00-00-00-00-01-00-4F-00-00-FD | Tester 5 3 True True 25/15/14 35 35 3 1 625 9,7,0,8 465000 1 0 1 91");
        }

        /// <summary>
        /// THE CREATION'S ARRANGEMENT (UWChargenLayoutUw2, drawopt and mousopt): columns, rows, spacing,
        /// the question's and the first row's tops for the sex, the classes, the portraits and the name,
        /// the places of two answers and the answers under a few points.
        /// </summary>
        private static void fCheckUw2ChargenLayout()
        {
            string fArranged(UWChargenLayoutUw2.Arrangement pOArrangement)
            {
                return pOArrangement.Columns + "x" + pOArrangement.Rows + " " + pOArrangement.Spacing + " " + pOArrangement.QuestionY + " " + pOArrangement.FirstRowY;
            }

            UWChargenLayoutUw2.Arrangement lOSex = UWChargenLayoutUw2.Arrange(2, 67, 16, true);
            UWChargenLayoutUw2.Arrangement lOClasses = UWChargenLayoutUw2.Arrange(8, 67, 16, true);
            UWChargenLayoutUw2.Arrangement lOPortraits = UWChargenLayoutUw2.Arrange(5, 35, 35, false);
            UWChargenLayoutUw2.Arrangement lOName = UWChargenLayoutUw2.Arrange(0, 1, 16, true);

            Check.Str("creation: sex, classes, portraits, name (columns x rows, spacing, question top, first row top)",
                fArranged(lOSex) + " | " + fArranged(lOClasses) + " | " + fArranged(lOPortraits) + " | " + fArranged(lOName),
                "1x2 46 127 107 | 1x8 46 187 167 | 1x5 62 195 195 | 1x0 79 107 87");

            UWChargenLayoutUw2.Place(lOClasses, 7, out int liX, out int liY);
            UWChargenLayoutUw2.Place(lOPortraits, 4, out int liPortraitX, out int liPortraitY);

            Check.Str("creation: the last class's and the last portrait's place",
                liX + "," + liY + " " + liPortraitX + "," + liPortraitY, "206,27 222,39");

            Check.Str("creation: the sex answer under (206,107) (272,92) (273,107) (206,91) (206,87) (205,107) (206,108)",
                UWChargenLayoutUw2.AnswerAt(lOSex, 206, 107) + " " + UWChargenLayoutUw2.AnswerAt(lOSex, 272, 92) + " "
                + UWChargenLayoutUw2.AnswerAt(lOSex, 273, 107) + " " + UWChargenLayoutUw2.AnswerAt(lOSex, 206, 91) + " "
                + UWChargenLayoutUw2.AnswerAt(lOSex, 206, 87) + " " + UWChargenLayoutUw2.AnswerAt(lOSex, 205, 107) + " "
                + UWChargenLayoutUw2.AnswerAt(lOSex, 206, 108), "0 0 -1 -1 1 -1 -1");
        }

        /// <summary>
        /// THE OPTIONS PANEL'S MODEL (UWOptionsPanelUw2, phase 3 step 4) through a walk of its groups: the
        /// group, the lit button and the message row after each step, and what a press asks the game to do.
        /// </summary>
        private static void fCheckUw2OptionsPanel()
        {
            UWOptionsPanelUw2 lOPanel = new UWOptionsPanelUw2 { DetailLevel = 2, MusicOn = true, SoundOn = true };
            System.Text.StringBuilder lOTrace = new System.Text.StringBuilder();

            void lfStep(string psWhat)
            {
                lOTrace.Append(psWhat).Append(':').Append(lOPanel.Group).Append(',').Append(lOPanel.Highlight).Append(',').Append(lOPanel.MessageRow).Append(' ');
            }

            void lfPress(int piButton)
            {
                UWOptionsPanelUw2.Outcome lOOutcome = lOPanel.Press(piButton);

                lOTrace.Append(lOOutcome.Action).Append(lOOutcome.Value).Append(' ');
                lfStep("p" + piButton);
            }

            lOPanel.Open(UWOptionsPanelUw2.GroupEnum.Game);
            lfStep("open");
            lfPress(4);
            lfPress(5);
            lfPress(6);
            lOPanel.Move(-1);
            lfStep("up");
            lfPress(5);
            lOPanel.Move(1);
            lfStep("down");
            lfPress(3);

            Check.Str("options: game, detail (High), Very High, back, up, quit, down, no",
                lOTrace.ToString().Trim(),
                "open:Game,0,-1 None0 p4:Detail,4,2 SetDetail3 p5:Detail,5,3 None0 p6:Game,0,-1 up:Game,6,-1 None0 p5:Quit,3,-1 down:Quit,2,-1 Close0 p3:Quit,3,-1");

            lOTrace.Clear();
            lOPanel.Open(UWOptionsPanelUw2.GroupEnum.Game);
            lfPress(1);
            lOPanel.Move(-1);
            lfStep("up");
            lfPress(3);
            lOPanel.OpenMusicSound(true);
            lfStep("sound");
            lfPress(3);
            lfPress(4);
            lfPress(0);
            lfPress(2);

            Check.Str("options: restore, up, slot 3, the sound page, off, done, save, slot 2",
                lOTrace.ToString().Trim(),
                "ListSaves0 p1:File,1,1 up:File,5,1 Restore3 p3:File,3,1 sound:MusicSound,2,3 SetSound0 p3:MusicSound,3,2 None0 p4:Game,0,-1 ListSaves0 p0:File,3,-1 Save2 p2:File,2,-1");

            Check.Str("options: the button under (0, 0), (0, 111), (0, 112), (81, 0), (-1, 0) from the click area's corner",
                UWOptionsPanelUw2.ButtonAt(0, 0) + " " + UWOptionsPanelUw2.ButtonAt(0, 111) + " " + UWOptionsPanelUw2.ButtonAt(0, 112) + " "
                + UWOptionsPanelUw2.ButtonAt(81, 0) + " " + UWOptionsPanelUw2.ButtonAt(-1, 0), "6 0 -1 -1 -1");
        }

        private static int fNotIndex(UWTexture pOScreen, int piLeft, int piTop, int piWidth, int piHeight, byte pyIndex, byte pyAlso)
        {
            int liOther = 0;

            for (int liRow = piTop; liRow < piTop + piHeight && liRow < pOScreen.Height; liRow++)
            {
                for (int liColumn = piLeft; liColumn < piLeft + piWidth && liColumn < pOScreen.Width; liColumn++)
                {
                    byte lyPixel = pOScreen.PaletteIndices[(liRow * pOScreen.Width) + liColumn];

                    if (lyPixel != pyIndex && lyPixel != pyAlso)
                        liOther++;
                }
            }

            return liOther;
        }

        /// <summary>
        /// THE CONTAINERS' MASKS (phase 3 step 3 part C) through the second game's variant over its own
        /// OBJECTS.DAT: the key ring (0x8C) takes a key and the key ring, not a scroll; the quiver takes an
        /// arrow and a wand, not the plain stone; the rune bag a runestone; the food mask an apple, not a
        /// bottle of ale. Each container's mask is printed as the data has it.
        /// </summary>
        private static void fCheckUw2ContainerMasks(UWObjectClassProperties pOClasses)
        {
            IUWInventoryVariant lOVariant = UWGameProfiles.Uw2.Inventory;
            List<string> lsMasks = new List<string>();
            int liKeyRing = -1, liQuiver = -1, liRunes = -1, liFood = -1;

            for (int liId = 0x80; liId <= 0x8F; liId++)
            {
                if (!pOClasses.TryGetContainer(liId, out UWObjectClassProperties.Container lOEntry))
                    continue;

                lsMasks.Add(liId.ToString("X") + ":" + lOEntry.ObjectMask.ToString("X"));

                if (lOEntry.ObjectMask == UWInventoryVariantUw2.KeysMask && liKeyRing < 0)
                    liKeyRing = lOEntry.ObjectMask;
                if (lOEntry.ObjectMask == UWInventoryVariantUw2.MissilesMask)
                    liQuiver = lOEntry.ObjectMask;
                if (lOEntry.ObjectMask == UWInventoryVariantUw2.RunesMask)
                    liRunes = lOEntry.ObjectMask;
                if (lOEntry.ObjectMask == UWInventoryVariantUw2.FoodMask)
                    liFood = lOEntry.ObjectMask;
            }

            System.Func<int, int, string> lFTakes = (piMask, piId) =>
                lOVariant.MatchesContainerMask(piMask, new UWObject((ushort)piId), pOClasses) ? "1" : "0";

            Check.Str("containers: the masks by id, then key ring (key, key ring, scroll), quiver (arrow, wand, stone), rune bag (rune), food (apple, ale)",
                string.Join(" ", lsMasks) + " | " + lFTakes(liKeyRing, 0x101) + lFTakes(liKeyRing, 0x8C) + lFTakes(liKeyRing, 0x13A) + " "
                + lFTakes(liQuiver, 0x12) + lFTakes(liQuiver, 0x98) + lFTakes(liQuiver, 0x13) + " " + lFTakes(liRunes, 0xE8) + " "
                + lFTakes(liFood, 0xB0) + lFTakes(liFood, 0xBB),
                "80:FFFF 81:FFFF 82:FFFF 83:FFFF 84:FFFF 85:FFFF 86:FFFF 87:FFFF 88:202 89:202 8A:FFFF 8B:FFFF 8C:204 8D:201 8E:203 8F:200 | 110 110 1 10");
        }

        private static void fCheckUw2CritterArt(UWCritterArtUw2 pOArt)
        {
            Check.Bool("critters: AS.AN, CR.AN and PG.MP read whole", pOArt != null && pOArt.IsLoaded, true);

            if (pOArt == null || !pOArt.IsLoaded)
                return;

            SortedSet<int> lOMaps = new SortedSet<int>();
            int liTypes = 0;

            for (int liType = 0; liType < UWCritterArtUw2.TypeCount; liType++)
            {
                int liMap = pOArt.MapOf(liType);

                if (liMap == UWCritterArtUw2.NoMap)
                    continue;

                liTypes++;
                lOMaps.Add(liMap);
            }

            Check.Str("critters: the creature types with a critter map (all but one), the maps they use (all 32)",
                liTypes + " " + lOMaps.Count, "63 32");

            int liFrames = 0;
            int liSequences = 0;
            int liLengthsAgree = 0;
            SortedSet<int> lOFormats = new SortedSet<int>();
            List<string> lsMissing = new List<string>();

            foreach (int liMap in lOMaps)
            {
                for (int liSequence = 0; liSequence < UWCritterArtUw2.SequenceCount; liSequence++)
                {
                    int liLength = pOArt.SequenceLength(liMap, liSequence);

                    if (liLength == 0)
                        continue;

                    liSequences++;

                    bool lbSame = true;

                    for (int liAngle = 0; liAngle < UWCritterArtUw2.AngleCount; liAngle++)
                    {
                        byte[] lySteps = pOArt.SequenceBytes(liMap, liSequence, liAngle);

                        lbSame &= lySteps[UWCritterArtUw2.SequenceLengthByte] == liLength;

                        for (int liStep = 0; liStep < liLength && liStep < UWCritterArtUw2.SequenceLengthByte; liStep++)
                        {
                            UWCritterArtUw2.Frame lOFrame = pOArt.GetFrame(liMap, lySteps[liStep]);

                            if (lOFrame == null || lOFrame.Pixels == null || lOFrame.Pixels.Length != lOFrame.Width * lOFrame.Height)
                            {
                                if (lsMissing.Count < 8)
                                    lsMissing.Add(liMap + "/" + liSequence + "/" + liAngle + "/" + lySteps[liStep]);

                                continue;
                            }

                            liFrames++;
                            lOFormats.Add(lOFrame.Format);
                        }
                    }

                    liLengthsAgree += lbSame ? 1 : 0;
                }
            }

            Check.Str("critters: every used map's sequences, each step's frame in the page PG.MP names, decoded to its size (missing: map/sequence/angle/frame)",
                string.Join(" ", lsMissing), string.Empty);
            Check.Str("critters: the sequences with frames (256), those whose eight angles all carry the count in byte 7 (248), the steps drawn, the formats (6 and 8)",
                liSequences + " " + liLengthsAgree + " " + liFrames + " " + string.Join(",", lOFormats),
                "256 248 10544 6,8");
            Check.Str("critters: the page names (map 0o21 page 3, map 0 page 0)",
                UWCritterArtUw2.PageFileName(0x11, 3) + " " + UWCritterArtUw2.PageFileName(0, 0), "CR21.03 CR00.00");
        }
    }
}
