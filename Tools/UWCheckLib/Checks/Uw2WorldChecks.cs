using System.Collections.Generic;
using System.IO;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S WORLD IN A SESSION (Docs/UW2-PLAN.md phase 2 step 2, 2026-10-29): UWSaveCodec
    /// loads the data folder's LEV.ARK - and each DOS save's (--uw2-saves) - into a session of the
    /// second profile, and each level is held to the block it came from, read apart from UWLevel: the
    /// tiles' textures against UWLevelBlock's indices and UWTextureMapUw2's numbers (walls and floors
    /// straight into the 64 entries, the ceiling entry 15), the objects in the tiles against a walk of
    /// the block's own chains, the free and active lists, the overlays and timers inside the block.
    /// The shipped file's 42 levels and its automaps and notes of levels 1, 74 and 79 (14, 51 and 30
    /// notes) are Underworld Exhumed's measurements (docs/FORMATS.md "LEV.ARK"); where a new game
    /// starts is UWGameProfileUw2's (MAINMENU.C, player_setup).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckUw2World(CheckContext pOContext)
        {
            Check.Group("The second game's world in a session (UW2 plan, phase 2 step 2)");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2 world", "no second game here: skipped");

                return;
            }

            UWGameData lOData = new UWGameData(lsData, null, UWGameProfiles.Uw2);
            UWGameSession lOSession = UWSaveCodec.Load(lOData, null);
            UWSessionWorld lOWorld = lOSession.World;

            Check.Str("world: the data folder's levels (80 places, 42 with a map block), the archive kept, no character yet",
                lOWorld.Levels.Count + " " + lOWorld.Levels.FindAll(l => l != null).Count + " " + (lOWorld.Ark != null) + " " + (lOSession.Player == null),
                "80 42 True True");

            Check.Str("world: the data folder's levels, each held to its block (problems)", fUw2LevelProblems(lOWorld), string.Empty);

            int liLevelOne = 0;
            int liAll = 0;

            foreach (UWLevel lOLevel in lOWorld.Levels.FindAll(l => l != null))
            {
                foreach (UWTile lOTile in lOLevel.TileData)
                {
                    liAll += lOTile.ObjectsInTile.Count;
                    liLevelOne += lOLevel.LevelNumber == 1 ? lOTile.ObjectsInTile.Count : 0;
                }
            }

            Check.Str("world: the objects lying in the tiles, level 1 and all levels (as the chains walked above give them)", liLevelOne + " " + liAll, "507 8162");

            List<string> lsAutomaps = new List<string>();
            List<string> lsNotes = new List<string>();

            for (int liLevel = 0; liLevel < UWLevArkFile.Uw2LevelCount; liLevel++)
            {
                UWLevel lOLevel = lOWorld.Levels[liLevel];

                if (lOLevel != null && lOLevel.AutomapTiles != null)
                    lsAutomaps.Add((liLevel + 1).ToString());

                if (lOWorld.GetMapNotes(liLevel).Count > 0)
                    lsNotes.Add((liLevel + 1) + ":" + lOWorld.GetMapNotes(liLevel).Count);
            }

            Check.Str("world: the shipped automaps on built levels and the notes (level:count; UE: notes 14, 51, 30 on levels 1, 74, 79)",
                string.Join(" ", lsAutomaps) + " / " + string.Join(" ", lsNotes), "1 / 1:14 74:51 79:30");
            Check.Str("world: the automaps of levels 74 and 79, which have no map block, kept in the archive (blocks 233, 238)",
                lOWorld.Ark.ReadBlock(UWLevArkFile.Uw2.AutomapBlock(73)).Length + " " + lOWorld.Ark.ReadBlock(UWLevArkFile.Uw2.AutomapBlock(78)).Length
                + " " + (lOWorld.Levels[73] == null) + " " + (lOWorld.Levels[78] == null), "4096 4096 True True");

            UWLevel lOFirst = lOWorld.Levels[UWGameProfileUw2.NewGameLevel - 1];
            UWTile lOStart = lOFirst.GetTile(UWGameProfileUw2.NewGameTileX, UWGameProfileUw2.NewGameTileY);

            int liStart = UWGameProfileUw2.NewGameTileX + (UWGameProfileUw2.NewGameTileY * UWWorldScale.TilesPerAxis);
            UWLevelBlock lOStartBlock = UWLevelBlock.Read(lOWorld.Ark.ReadBlock(0), UWLevArkFile.Uw2);

            Check.Str("world: a new game's start, level 1 tile 0x13/0x30 - open (type 1), its floor height as the block has it",
                (int)lOStart.TileType + " " + (lOStart.FloorHeight >> 4), "1 " + lOStartBlock.FloorHeight(liStart));

            fCheckUw2WalkRules(lOData, lOWorld);
            fCheckUw2Teleports(lOWorld);

            fCheckUw2SaveRoundTrip(lOData, null, "DATA");

            if (pOContext.Uw2SavesPath == null)
                return;

            foreach (string lsSave in Directory.GetDirectories(pOContext.Uw2SavesPath, "SAVE?"))
            {
                if (!File.Exists(Path.Combine(lsSave, "LEV.ARK")))
                    continue;

                UWSessionWorld lOSaved = UWSaveCodec.Load(lOData, lsSave).World;
                int liAutomaps = 0;
                int liNotes = 0;

                for (int liLevel = 0; liLevel < UWLevArkFile.Uw2LevelCount; liLevel++)
                {
                    liAutomaps += lOSaved.Levels[liLevel] != null && lOSaved.Levels[liLevel].AutomapTiles != null ? 1 : 0;
                    liNotes += lOSaved.GetMapNotes(liLevel).Count;
                }

                Check.Str("world: " + Path.GetFileName(lsSave) + "'s levels, each held to its block (problems)", fUw2LevelProblems(lOSaved), string.Empty);
                fCheckUw2SaveRoundTrip(lOData, lsSave, Path.GetFileName(lsSave));
                fCheckUw2Character(lOData, lsSave);
                Check.Pass("world: " + Path.GetFileName(lsSave) + "'s levels with a map block, automaps, notes",
                    lOSaved.Levels.FindAll(l => l != null).Count + " " + liAutomaps + " " + liNotes);
            }
        }

        /// <summary>
        /// WHAT THE WALK SHOWS (phase 2 step 3): the level light (DL.DAT: level 1's byte 14 is a minimum
        /// of 4 where the tile's light bit is clear, none where it is set - load_dl, change_GrSq), the
        /// unlit levels 65 to 72 but 68 (process_grid), the door pictures after the 64 textures, and every
        /// creature of level 1 with a picture in its stored sequence from the front, found in its pages.
        /// </summary>
        private static void fCheckUw2WalkRules(UWGameData pOData, UWSessionWorld pOWorld)
        {
            UWDarknessUw2 lODark = new UWDarknessUw2(File.ReadAllBytes(Path.Combine(pOData.DataPath, UWDarknessUw2.FileName)));

            Check.Str("walk: level 1's light (bit clear, bit set), and level 2's (its byte, its two tiles)",
                lODark.TileMinimum(0, false) + " " + lODark.TileMinimum(0, true) + " / " + lODark.Raw(1) + ": "
                + lODark.TileMinimum(1, false) + " " + lODark.TileMinimum(1, true),
                "4 -1 / " + lODark.Raw(1) + ": " + fExpectedTileMinimum(lODark.Raw(1), false) + " " + fExpectedTileMinimum(lODark.Raw(1), true));
            Check.Str("walk: the unlit levels among 64, 65, 68, 72, 73",
                UWDarknessUw2.IsUnlitLevel(64) + " " + UWDarknessUw2.IsUnlitLevel(65) + " " + UWDarknessUw2.IsUnlitLevel(68) + " "
                + UWDarknessUw2.IsUnlitLevel(72) + " " + UWDarknessUw2.IsUnlitLevel(73), "False True False True False");

            UWLevel lOFirst = pOWorld.Levels[0];
            UWTextureMapUw2 lOMap = lOFirst.TextureMapUw2;
            bool lbDoors = lOFirst.TextureInfos.Length == UWLevel.Uw2TextureInfoCount;

            for (int liDoor = 0; lbDoors && liDoor < UWTextureMapUw2.DoorCount; liDoor++)
                lbDoors &= lOFirst.TextureInfos[UWTextureMapUw2.TextureCount + liDoor] == lOMap.Doors[liDoor];

            Check.Bool("walk: level 1's table holds its 64 textures and then its six door pictures", lbDoors, true);

            // The translucent colours (the walk's third test: pink shadows). Asked inside a session of
            // the second game, as the walk asks them; the first game's answers before and after.
            bool lbUw1Before = UWTransparencyTables.TryGetFadeTable(0xFD, out _);
            UWGameSession lOKept = UWGameSession.Current;
            UWGameSession.MakeCurrent(new UWGameSession(pOData, 1));
            bool lbShadow = UWTransparencyTables.TryGetFadeTable(0xFD, out UWTransparencyTables.FadeTable leShadow);
            bool lbBelow = UWTransparencyTables.TryGetFadeTable(0xF8, out _);
            bool lbAbove = UWTransparencyTables.TryGetFadeTable(0xFE, out _);
            bool lbLava = UWPaletteRotation.IsLavaIndex(20) || UWPaletteRotation.IsWaterIndex(50);
            UWGameSession.MakeCurrent(lOKept);

            // The automap byte (step 4): a door over lava written by each game's order - the first game's
            // terrain low and kind high (0x61 on an open tile), the second game's the other way (0x91).
            lOFirst.EnsureAutomap();
            lOFirst.AutomapTiles[UWGameProfileUw2.NewGameTileX + (UWGameProfileUw2.NewGameTileY * UWWorldScale.TilesPerAxis)] = 0;
            lOFirst.MarkTileVisited(UWGameProfileUw2.NewGameTileX, UWGameProfileUw2.NewGameTileY, UWLevel.MapDisplayDoor | UWLevel.MapDisplayLava);
            int liWritten = lOFirst.AutomapTiles[UWGameProfileUw2.NewGameTileX + (UWGameProfileUw2.NewGameTileY * UWWorldScale.TilesPerAxis)];

            Check.Str("walk: the automap byte of a door over lava on an open tile, UW2's order (and UW1's Compose), ice as UW2's terrain 3",
                liWritten.ToString("X2") + " " + UWAutomapCode.Compose(1, UWAutomapCode.KindDoor, 2, UWGame.Uw1).ToString("X2") + " "
                + UWAutomapCode.Compose(1, 0, 3, UWGame.Uw2).ToString("X2") + " " + UWAutomapCode.Terrain(liWritten, UWGame.Uw2) + "/" + UWAutomapCode.Kind(liWritten, UWGame.Uw2),
                "91 61 C1 2/1");

            Check.Str("walk: XFER.DAT's five tables; 0xFD is table 4 (the shadow), 0xF8 and 0xFE none; no first-game lava or water; the first game's 0xFD none",
                pOData.TransparencyTables.IsLoaded + " " + lbShadow + " " + (int)leShadow + " " + lbBelow + " " + lbAbove + " " + lbLava + " " + lbUw1Before,
                "True True 4 False False False False");

            UWCritterArtUw2 lOArt = pOData.CritterArtUw2;
            int liCreatures = 0;
            int liShown = 0;
            List<string> lsMissing = new List<string>();

            foreach (UWTile lOTile in lOFirst.TileData)
            {
                foreach (UWObject lOObject in lOTile.ObjectsInTile)
                {
                    if (!(lOObject is UWNpc lONpc) || !UWObjectIds.IsCreature(lOObject.ID))
                        continue;

                    liCreatures++;

                    int liType = lOObject.ID - UWObjectIds.FirstCreature;
                    int liPicture = lOArt.PictureFor(liType, UWCritterArtUw2.SequenceOf(lONpc), UWCritterAnimations.FrontView, UWCritterArtUw2.FrameOf(lONpc));

                    if (liPicture == UWCritterArtUw2.NoPicture)
                        continue;

                    if (lOArt.GetFrame(lOArt.MapOf(liType), liPicture) != null)
                        liShown++;
                    else if (lsMissing.Count < 5)
                        lsMissing.Add(lOObject.ID.ToString("X") + ":" + liPicture);
                }
            }

            Check.Str("walk: level 1's creatures, those with a front picture in their stored sequence, pictures not in their pages",
                liCreatures + " " + liShown + " " + string.Join(" ", lsMissing), liCreatures + " " + liCreatures + " ");
        }

        /// <summary>
        /// THE TELEPORTS (step 5): every move trigger in a tile whose trap is a teleport, decoded as
        /// UseTrap does (UWTeleportUw2) - how many, how many to another level, to the same, falling,
        /// with a heading, stopping the map - and every target held to the world: a level that exists, a
        /// tile on the map that is not solid (find_good_x_and_y starts there).
        /// </summary>
        private static void fCheckUw2Teleports(UWSessionWorld pOWorld)
        {
            int liAll = 0;
            int liOther = 0;
            int liSame = 0;
            int liFalls = 0;
            int liHeadings = 0;
            int liMapOff = 0;
            List<string> lsBad = new List<string>();

            foreach (UWLevel lOLevel in pOWorld.Levels.FindAll(l => l != null))
            {
                foreach (UWTile lOTile in lOLevel.TileData)
                {
                    foreach (UWObject lOTrigger in lOTile.ObjectsInTile)
                    {
                        if (lOTrigger.ID != UWObjectMechanics.MoveTriggerId)
                            continue;

                        int liLink = lOTrigger.Quantity;
                        UWObject lOTrap = liLink > 0 && liLink < lOLevel.Masterlist.Count ? lOLevel.Masterlist[liLink] : null;

                        if (lOTrap == null || lOTrap.ID != UWTeleportUw2.TrapId)
                            continue;

                        UWTeleportUw2.Target lOTarget = UWTeleportUw2.Decode(lOTrap);
                        int liLevel = UWTeleportUw2.LevelOf(lOTarget, lOLevel.LevelNumber);
                        UWLevel lOTo = liLevel >= 1 && liLevel <= pOWorld.Levels.Count ? pOWorld.Levels[liLevel - 1] : null;
                        UWTile lOAt = lOTo != null ? lOTo.GetTile(lOTarget.TileX, lOTarget.TileY) : null;

                        liAll++;
                        liOther += liLevel != lOLevel.LevelNumber ? 1 : 0;
                        liSame += liLevel == lOLevel.LevelNumber ? 1 : 0;
                        liFalls += lOTarget.Falls ? 1 : 0;
                        liHeadings += lOTarget.HasHeading ? 1 : 0;
                        liMapOff += lOTarget.AutomapOff ? 1 : 0;

                        if ((lOAt == null || lOAt.TileType == UWTile.TileTypeEnum.solid) && lsBad.Count < 6)
                            lsBad.Add(lOLevel.LevelNumber + ":" + lOTile.X + "/" + lOTile.Z + "->" + liLevel + ":" + lOTarget.TileX + "/" + lOTarget.TileY);
                    }
                }
            }

            Check.Str("teleports: move triggers to a teleport, to another level, the same, falling, with a heading, stopping the map",
                liAll + " " + liOther + " " + liSame + " " + liFalls + " " + liHeadings + " " + liMapOff, "298 190 108 109 140 37");

            // One target lies in rock as shipped: find_good_x_and_y takes the player to a free tile beside it.
            Check.Str("teleports: targets on a missing level or a solid tile (level:from->level:to; the shipped one in rock)",
                string.Join(" ", lsBad), "25:53/41->25:55/41");
            Check.Str("teleports: a trap's decoding (east heading, falling: fine x 3, heading 2; level 0 is the current)",
                fDescribe(UWTeleportUw2.Decode(new UWObject(UWTeleportUw2.TrapId) { XPos = 3, Heading = 2, ZPos = 0, Quality = 5, Owner = 6 }))
                + " " + UWTeleportUw2.LevelOf(UWTeleportUw2.Decode(new UWObject(UWTeleportUw2.TrapId) { ZPos = 0 }), 7),
                "0 5/6 True True 4000 False 7");
        }

        private static string fDescribe(UWTeleportUw2.Target pOTarget)
        {
            return pOTarget.Level + " " + pOTarget.TileX + "/" + pOTarget.TileY + " " + pOTarget.Falls + " " + pOTarget.HasHeading + " "
                + pOTarget.Heading.ToString("X4") + " " + pOTarget.AutomapOff;
        }

        /// <summary>
        /// THE CHARACTER ON THE SCREEN (phase 3 step 2): a DOS save's record and workspace as the walk's
        /// screen reads them (UWPlayerRecordUw2) - name, class, body, sex and hand, the attributes, vitality
        /// and mana with their maxima, the level, the weight left in stones, poison, and the objects of the
        /// nineteen Inventory[] slots by id (the walk's paperdoll, belt and backpack). Pinned per save as
        /// read on 2026-10-29; a save the user writes anew changes them.
        /// </summary>
        private static void fCheckUw2Character(UWGameData pOData, string psSave)
        {
            string lsPlayer = Path.Combine(psSave, UWSaveCodec.PlayerFileName);

            if (!File.Exists(lsPlayer))
                return;

            UWPlayerFile lOFile = UWPlayerFile.Read(File.ReadAllBytes(lsPlayer), UWGame.Uw2);
            List<string> lsSlots = new List<string>();

            for (int liSlot = 0; liSlot < UWPlayerFile.UsedSlotCount; liSlot++)
            {
                int liId = UWPlayerRecordUw2.SlotItemId(lOFile, liSlot);

                lsSlots.Add(liId < 0 ? "-" : liId.ToString("X") + (UWPlayerRecordUw2.SlotCount(lOFile, liSlot) > 1 ? "x" + UWPlayerRecordUw2.SlotCount(lOFile, liSlot) : string.Empty));
            }

            string lsGot = string.Format("{0} c{1} b{2} {3} {4} | {5} {6} {7} | {8}/{9} {10}/{11} L{12} W{13} {14} | {15}",
                lOFile.Name, UWPlayerRecordUw2.CharacterClass(lOFile), UWPlayerRecordUw2.Body(lOFile),
                UWPlayerRecordUw2.IsFemale(lOFile) ? "f" : "m", UWPlayerRecordUw2.IsLeftHanded(lOFile) ? "left" : "right",
                UWPlayerRecordUw2.Strength(lOFile), UWPlayerRecordUw2.Dexterity(lOFile), UWPlayerRecordUw2.Intelligence(lOFile),
                UWPlayerRecordUw2.Vitality(lOFile), UWPlayerRecordUw2.MaxVitality(lOFile), UWPlayerRecordUw2.Mana(lOFile),
                UWPlayerRecordUw2.MaxMana(lOFile), UWPlayerRecordUw2.CharacterLevel(lOFile), UWPlayerRecordUw2.WeightLeft(lOFile) / 10,
                UWPlayerRecordUw2.IsPoisoned(lOFile) ? "poisoned" : "well", string.Join(" ", lsSlots));

            Check.Str("character: " + Path.GetFileName(psSave) + " - name, class, body, sex, hand | str dex int | vitality, mana, level, weight left | slots",
                lsGot, fExpectedCharacter(Path.GetFileName(psSave)) ?? lsGot);

            // Step 3 part A: the workspace as objects (UWInventoryWorkspace) gives the same ids in the
            // same places, and the second game's places take each worn piece only where it is worn
            // (IUWInventoryVariant over OBJECTS.DAT's armour categories).
            UWInventoryWorkspace lOWorkspace = UWInventoryWorkspace.Read(lOFile);
            int liWithoutPicture = 0;

            lOWorkspace.AssignPictures(pOData.Textures);

            for (int liRecord = 1; liRecord < lOWorkspace.Records.Count; liRecord++)
            {
                if (lOWorkspace.Records[liRecord].ID <= UWObjectAppearance.LastPicturedObjectId && lOWorkspace.Records[liRecord].Texture == null)
                    liWithoutPicture++;
            }

            // Every carried object can be drawn once it is put down (per user, 2026-10-29: they vanished).
            Check.Str("inventory: " + Path.GetFileName(psSave) + " - carried objects without a picture after AssignPictures (of all)",
                liWithoutPicture + " of " + (lOWorkspace.Records.Count - 1), "0 of " + (lOWorkspace.Records.Count - 1));
            List<string> lsObjects = new List<string>();
            UWObjectClassProperties lOClasses = pOData.ObjectClassProperties;
            IUWInventoryVariant lOVariant = UWGameProfiles.Uw2.Inventory;
            UWArmorItemMap.BodySlot[] leArmour = { UWArmorItemMap.BodySlot.Helmet, UWArmorItemMap.BodySlot.Chest, UWArmorItemMap.BodySlot.Gloves,
                UWArmorItemMap.BodySlot.Legs, UWArmorItemMap.BodySlot.Boots, UWArmorItemMap.BodySlot.RightRing, UWArmorItemMap.BodySlot.LeftRing };
            int liWrongFits = 0;

            for (int liSlot = 0; liSlot < UWInventoryWorkspace.EquipmentSlotCount + UWInventoryWorkspace.BackpackSlotCount; liSlot++)
            {
                UWObject lOItem = liSlot < UWInventoryWorkspace.EquipmentSlotCount ? lOWorkspace.Equipment[liSlot]
                    : lOWorkspace.Backpack[liSlot - UWInventoryWorkspace.EquipmentSlotCount];

                lsObjects.Add(lOItem == null ? "-" : lOItem.ID.ToString("X"));

                if (lOItem == null || liSlot >= UWInventoryWorkspace.EquipmentSlotCount)
                    continue;

                UWArmorItemMap.BodySlot leOwn = UWInventoryModel.FromSavegameSlot(liSlot);

                foreach (UWArmorItemMap.BodySlot leSlot in leArmour)
                {
                    bool lbFits = lOVariant.FitsBodySlot(lOItem, leSlot, UWArmorItemMap.BodySlot.LeftHandSlot, lOClasses);
                    bool lbOwn = leSlot == leOwn || (System.Array.IndexOf(leArmour, leOwn) >= 5 && System.Array.IndexOf(leArmour, leSlot) >= 5);

                    if (lbFits != (lbOwn && System.Array.IndexOf(leArmour, leOwn) >= 0))
                        liWrongFits++;
                }
            }

            // Step 3 part D: the workspace written back from its objects (UWInventoryWorkspace.WriteInto)
            // and read again holds the same objects in the same places, as many records, every container
            // with as many things inside; the player object's link points at record 1.
            UWPlayerFile lOCopy = UWPlayerFile.Read(File.ReadAllBytes(lsPlayer), UWGame.Uw2);
            UWInventoryWorkspace lOBefore = UWInventoryWorkspace.Read(lOCopy);
            string lsWriteError = UWInventoryWorkspace.WriteInto(lOCopy, lOBefore.Equipment, lOBefore.Backpack, null, lOBefore.Records,
                UWPlayerRecordUw2.Weight(lOCopy));
            UWPlayerFile lOAgainFile = UWPlayerFile.Read(lOCopy.ToBytes(), UWGame.Uw2);
            UWInventoryWorkspace lOAfter = UWInventoryWorkspace.Read(lOAgainFile);

            Check.Str("inventory: " + Path.GetFileName(psSave) + " - written back and read again: error, records before and after, places and contents alike, the link to record 1",
                (lsWriteError ?? "none") + " " + (lOBefore.Records.Count - 1) + " " + (lOAfter.Records.Count - 1) + " "
                + (fPlacesOf(lOBefore) == fPlacesOf(lOAfter)) + " " + (lOAgainFile.InventoryHead == 1),
                "none " + (lOBefore.Records.Count - 1) + " " + (lOBefore.Records.Count - 1) + " True True");

            string lsRunes = fRunesOf(lOFile);

            // Both DOS saves: every rune, Vas Ort Ylem on the shelf, no spell active (read 2026-10-29).
            Check.Str("runes: " + Path.GetFileName(psSave) + " - the bag's runes | the shelf (count) | the active spells' words",
                lsRunes, fExpectedCharacter(Path.GetFileName(psSave)) != null
                    ? "0 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 | 21 14 23 (3) | " : lsRunes);

            string lsPinned = fExpectedCharacter(Path.GetFileName(psSave));
            string lsGotObjects = string.Join(" ", lsObjects) + " | " + liWrongFits;

            Check.Str("inventory: " + Path.GetFileName(psSave) + " - the workspace's objects by place, worn pieces fitting elsewhere or not where worn",
                lsGotObjects, lsPinned != null ? fIdsOnly(lsPinned.Substring(lsPinned.LastIndexOf('|') + 2)) + " | 0" : lsGotObjects);
        }

        /// <summary>The runes a DOS save's record holds (step 3 part C2): the bag's runes by number, the
        /// shelf's three and their count, the active spells' words.</summary>
        private static string fRunesOf(UWPlayerFile pOFile)
        {
            List<string> lsRunes = new List<string>();
            List<string> lsSpells = new List<string>();

            for (int liRune = 0; liRune < UWPlayerRecordUw2.RuneCount; liRune++)
            {
                if (UWPlayerRecordUw2.HasRune(pOFile, liRune))
                    lsRunes.Add(liRune.ToString());
            }

            for (int liAt = 0; liAt < UWPlayerRecordUw2.ActiveSpellCount(pOFile) && liAt < 3; liAt++)
                lsSpells.Add(UWPlayerRecordUw2.ActiveSpell(pOFile, liAt).ToString("X"));

            return string.Join(" ", lsRunes) + " | " + UWPlayerRecordUw2.ShelfRune(pOFile, 0) + " " + UWPlayerRecordUw2.ShelfRune(pOFile, 1) + " "
                + UWPlayerRecordUw2.ShelfRune(pOFile, 2) + " (" + UWPlayerRecordUw2.ShelfCount(pOFile) + ") | " + string.Join(" ", lsSpells);
        }

        /// <summary>Every place's object id with the ids inside it, depth first, for a comparison.</summary>
        private static string fPlacesOf(UWInventoryWorkspace pOWorkspace)
        {
            System.Text.StringBuilder lOText = new System.Text.StringBuilder();

            foreach (UWObject lOItem in pOWorkspace.Equipment)
                fAppendTree(lOText, lOItem);

            foreach (UWObject lOItem in pOWorkspace.Backpack)
                fAppendTree(lOText, lOItem);

            return lOText.ToString();
        }

        private static void fAppendTree(System.Text.StringBuilder pOText, UWObject pOItem)
        {
            if (pOItem == null)
            {
                pOText.Append("- ");

                return;
            }

            // A count only where the field is one: a container's holds the link to its first thing, which
            // the writer renumbers.
            pOText.Append(pOItem.ID.ToString("X")).Append('/').Append(pOItem.HasQuantity ? pOItem.Quantity : -1).Append('/')
                .Append(pOItem.Quality).Append(' ');

            if (pOItem.Contents == null)
                return;

            pOText.Append("( ");

            foreach (UWObject lOInner in pOItem.Contents)
                fAppendTree(pOText, lOInner);

            pOText.Append(") ");
        }

        /// <summary>A pin's slots without their counts.</summary>
        private static string fIdsOnly(string psSlots)
        {
            List<string> lsIds = new List<string>();

            foreach (string lsSlot in psSlots.Split(' '))
                lsIds.Add(lsSlot.Split('x')[0]);

            return string.Join(" ", lsIds);
        }

        /// <summary>The pins of fCheckUw2Character by save, null for a save not pinned.</summary>
        private static string fExpectedCharacter(string psSave)
        {
            switch (psSave.ToUpperInvariant())
            {
                case "SAVE1":
                    return "Henrietta c3 b3 f left | 25 21 14 | 99/105 12/36 L15 W19 well | 2D 20 27 24 2A 9B 99 D 3B 38 39 8C 82 8F 13A 80 9A B5 -";
                case "SAVE2":
                    return "Henrietta c3 b3 f left | 25 21 14 | 105/105 21/21 L15 W26 well | 2D 20 27 24 2A 9B 99 D 3B 38 39 8C 82 8F 13A 80 - - -";
                default:
                    return null;
            }
        }

        /// <summary>
        /// THE WALK'S SAVE (step 6): a world loaded (from DATA or a DOS save) and saved unchanged into a
        /// scratch folder with the position its PLAYER.DAT holds - every level block, overlays and timers
        /// included, must decode as it did, every automap and every page's notes too, PLAYER.DAT must come
        /// back byte for byte (a DOS save's) or as a walker named Avatar on the start tile (DATA's), and the
        /// saved folder must load again with the same levels.
        /// </summary>
        private static void fCheckUw2SaveRoundTrip(UWGameData pOData, string psSave, string psLabel)
        {
            string lsScratch = Path.Combine(Path.GetTempPath(), "UWCheckUw2Save_" + System.Guid.NewGuid().ToString("N"));

            try
            {
                UWGameSession lOSession = UWSaveCodec.Load(pOData, psSave);
                UWPlayerFile lOPlayer = lOSession.PlayerFileUw2;
                byte[] lyPlayerBefore = lOPlayer != null ? lOPlayer.ToBytes() : null;
                int liX = lOPlayer != null ? UWPlayerRecordUw2.PositionX(lOPlayer) : (UWGameProfileUw2.NewGameTileX << 8) + 0x80;
                int liY = lOPlayer != null ? UWPlayerRecordUw2.PositionY(lOPlayer) : (UWGameProfileUw2.NewGameTileY << 8) + 0x80;
                int liZ = lOPlayer != null ? UWPlayerRecordUw2.ZPosition(lOPlayer) : 0;
                int liHeading = lOPlayer != null ? UWPlayerRecordUw2.Heading(lOPlayer) : 0;
                int liLevel = lOPlayer != null ? UWPlayerRecordUw2.Level(lOPlayer) - 1 : 0;
                UWArk lOBefore = UWArk.Read(lOSession.World.RawLevelData, UWArk.LayoutEnum.Uw2);

                string lsError = UWSaveCodec.SaveUw2(lOSession, lsScratch, "round trip", liLevel, liX, liY, liZ, liHeading);
                UWArk lOAfter = UWArk.Read(Path.Combine(lsScratch, "LEV.ARK"), UWArk.LayoutEnum.Uw2);
                List<string> lsChanged = new List<string>();

                for (int liBlock = 0; liBlock < lOBefore.BlockCount && lsChanged.Count < 6; liBlock++)
                {
                    if (!fSameBytes(lOBefore.ReadBlock(liBlock), lOAfter.ReadBlock(liBlock)))
                        lsChanged.Add(liBlock.ToString());
                }

                byte[] lyPlayerAfter = File.ReadAllBytes(Path.Combine(lsScratch, UWSaveCodec.PlayerFileName));
                UWPlayerFile lOWritten = UWPlayerFile.Read(lyPlayerAfter, UWGame.Uw2);
                string lsPlayer = lyPlayerBefore != null
                    ? (fSameBytes(lyPlayerBefore, lyPlayerAfter) ? "same" : "changed")
                    : lOWritten.Name + " " + lOWritten.KeyFitsName + " " + (UWPlayerRecordUw2.PositionX(lOWritten) >> 8) + "/" + (UWPlayerRecordUw2.PositionY(lOWritten) >> 8)
                        + " " + UWPlayerRecordUw2.Level(lOWritten);

                UWGameSession lOAgain = UWSaveCodec.Load(pOData, lsScratch);

                Check.Str("save: " + psLabel + " saved unchanged - error, blocks that decode otherwise, PLAYER.DAT, levels loaded again, problems",
                    (lsError ?? "none") + " | " + string.Join(" ", lsChanged) + " | " + lsPlayer + " | "
                    + lOAgain.World.Levels.FindAll(l => l != null).Count + " | " + fUw2LevelProblems(lOAgain.World),
                    "none |  | " + (lyPlayerBefore != null ? "same" : "Avatar True 19/48 1") + " | 42 | ");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(lsScratch))
                        Directory.Delete(lsScratch, true);
                }
                catch (System.Exception)
                {
                }
            }
        }

        /// <summary>DL.DAT's rule written out for the check: light = byte % 10, a minimum where byte &gt;= 10
        /// and the bit is clear or byte &lt; 10 and the bit is set, none elsewhere; a light of 0 keeps
        /// the level's own (0 or none).</summary>
        private static int fExpectedTileMinimum(int piByte, bool pbBit)
        {
            int liLight = piByte % 10;
            bool lbHigh = piByte >= 10;

            if (liLight == 0)
                return lbHigh ? 0 : -1;

            return (lbHigh && !pbBit) || (!lbHigh && pbBit) ? liLight : -1;
        }

        /// <summary>Every level of a world against its block; the problems found, at most a few.</summary>
        private static string fUw2LevelProblems(UWSessionWorld pOWorld)
        {
            UWLevArkFile lOFile = UWLevArkFile.Uw2;
            List<string> lsProblems = new List<string>();

            for (int liLevel = 0; liLevel < lOFile.LevelCount && lsProblems.Count < 6; liLevel++)
            {
                UWLevel lOLevel = pOWorld.Levels[liLevel];
                byte[] lyBlock = pOWorld.Ark.ReadBlock(lOFile.MapBlock(liLevel));

                if (lOLevel == null)
                {
                    if (lyBlock.Length >= lOFile.BlockSize)
                        lsProblems.Add((liLevel + 1) + " not built");

                    continue;
                }

                UWLevelBlock lOBlock = UWLevelBlock.Read(lyBlock, lOFile);
                UWTextureMapUw2 lOMap = UWTextureMapUw2.Read(pOWorld.Ark.ReadBlock(lOFile.TextureMapBlock(liLevel)));
                int liTextures = 0;
                int liInTiles = 0;
                int liChained = 0;

                for (int liTile = 0; liTile < UWLevArkLayout.TileCount; liTile++)
                {
                    UWTile lOTile = lOLevel.TileData[liTile];
                    int liWall = lOMap != null ? lOMap.Textures[lOBlock.WallTexture(liTile)] : lOBlock.WallTexture(liTile);
                    int liFloor = lOMap != null ? lOMap.Textures[lOBlock.FloorTexture(liTile)] : lOBlock.FloorTexture(liTile);
                    int liCeiling = lOMap != null ? lOMap.Textures[UWTile.Uw2CeilingSlot] : UWTile.Uw2CeilingSlot;

                    liTextures += lOTile.TextureWall != liWall || lOTile.TextureFloor != liFloor || lOTile.TextureCeiling != liCeiling ? 1 : 0;
                    liInTiles += lOTile.ObjectsInTile.Count;

                    for (int liObject = lOBlock.FirstObject(liTile), liGuard = 0; liObject != 0 && liGuard < UWLevArkLayout.ObjectCount; liGuard++)
                    {
                        liChained++;
                        liObject = UWBinary.ReadU16(lyBlock, fRecordOffset(liObject) + 4) >> 6;
                    }
                }

                if (liTextures != 0)
                    lsProblems.Add((liLevel + 1) + " textures " + liTextures);

                if (liInTiles + lOLevel.InFlight.Count != liChained)
                    lsProblems.Add((liLevel + 1) + " objects " + liInTiles + "+" + lOLevel.InFlight.Count + "/" + liChained);

                if (!fSameList(lOLevel.FreeMobileSlots, lOBlock.MobileFreeList) || !fSameList(lOLevel.FreeStaticSlots, lOBlock.StaticFreeList)
                    || !fSameList(lOLevel.ActiveMobiles, lOBlock.ActiveMobiles))
                    lsProblems.Add((liLevel + 1) + " lists");

                if (lOLevel.Animations.Entries.Count != lOBlock.Overlays.Entries.Count || !fSameList(lOLevel.Timers, lOBlock.Timers))
                    lsProblems.Add((liLevel + 1) + " overlays or timers");

                if (lOLevel.Masterlist.Count != UWLevArkLayout.ObjectCount || lOLevel.LevelNumber != liLevel + 1 || !lOLevel.IsUw2)
                    lsProblems.Add((liLevel + 1) + " shape");
            }

            return string.Join("; ", lsProblems);
        }

        private static int fRecordOffset(int piObject)
        {
            return UWLevArkLayout.IsMobileSlot(piObject)
                ? UWLevArkLayout.MobileBase + (piObject * UWLevArkLayout.MobileRecordSize)
                : UWLevArkLayout.MobileBase + (UWLevArkLayout.MobileCount * UWLevArkLayout.MobileRecordSize)
                    + ((piObject - UWLevArkLayout.MobileCount) * UWLevArkLayout.StaticRecordSize);
        }

        private static bool fSameList(IReadOnlyList<int> pOFirst, IReadOnlyList<int> pOSecond)
        {
            if (pOFirst.Count != pOSecond.Count)
                return false;

            for (int liAt = 0; liAt < pOFirst.Count; liAt++)
            {
                if (pOFirst[liAt] != pOSecond[liAt])
                    return false;
            }

            return true;
        }
    }
}
