using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE SECOND GAME'S DATA THROUGH WHAT IS GENERIC ALREADY (Docs/UW2-PLAN.md phase 0 step 6,
    /// 2026-10-28): UW2's DATA folder, extracted from the same game.gog as the first game's
    /// (CheckContext.Uw2DataPath), read by the readers that know no game - the strings of
    /// STRINGS.PAK, the palettes, every .GR file's pictures (UWTextures.ReadImageFile) and the
    /// archives with their LZSS blocks (UWArk). What is not generic is said, not read: the
    /// critters' pages have a format of their own (AS.AN, CR.AN, PG.MP, CRxx.y beside UW1's
    /// ASSOC.ANM and CRxxPAGE.Nyy), and the first game's critter reader must find nothing there.
    /// Skipped without the second game (no image, or an image without it).
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>A level's bridges disguised as their floor (UWItemDescriptions.BridgeLooksLikeItsFloor) as "x/y", "-" for none.</summary>
        private static string fDisguisedBridges(DataImport pOData, int piLevel)
        {
            SortedSet<string> lOSquares = new SortedSet<string>();

            foreach (UWObject lOObject in pOData.Levels[piLevel].Masterlist)
            {
                if (lOObject != null && UWItemDescriptions.BridgeLooksLikeItsFloor(lOObject, pOData.Levels[piLevel]))
                    lOSquares.Add(lOObject.TileX + "/" + lOObject.TileY);
            }

            return lOSquares.Count == 0 ? "-" : string.Join(" ", lOSquares);
        }

        /// <summary>A level's bridges with flags of 2 and up as "flags:name", each name once.</summary>
        private static string fBridgeNames(DataImport pOData, int piLevel)
        {
            SortedSet<string> lONames = new SortedSet<string>();

            foreach (UWObject lOObject in pOData.Levels[piLevel].Masterlist)
            {
                if (lOObject == null || lOObject.ID != UWObjectIds.Bridge || lOObject.Flags < 2)
                    continue;

                int liString = UWItemDescriptions.BridgeTextureDescription(lOObject);

                lONames.Add(lOObject.Flags + ":" + (liString >= 0 ? pOData.GetTextureDescription(liString) : "-"));
            }

            return string.Join(", ", lONames);
        }

        private static void fCheckUw2Data(CheckContext pOContext)
        {
            Check.Group("The second game's data, read where the readers are generic (UW2 plan, phase 0 step 6)");

            string lsData = pOContext.Uw2DataPath;

            if (lsData == null)
            {
                Check.Pass("uw2", "no second game here (a DATA folder given directly, or an image without UW2): skipped");

                return;
            }

            UWStrings lOStrings = new UWStrings(File.ReadAllBytes(Path.Combine(lsData, "STRINGS.PAK")));
            int liStrings = 0;

            foreach (UWStringBlock lOBlock in lOStrings.Blocks.Values)
                liStrings += lOBlock.Strings.Count;

            Check.Str("uw2: STRINGS.PAK's blocks and strings", lOStrings.Blocks.Count + " " + liStrings, "136 13319");
            Check.Str("uw2: the first object names of block 4 (an object's name at its id + 1, as in the first game)",
                string.Join("|", lOStrings.Blocks[4].Strings.GetRange(0, 4)), "|a_hand axe|a_battle axe|an_axe");

            // A NAMELESS KIND BY ITS SEQUENCE (CritterLook, per user 2026-11-22 on the original: the Void's eye on level 69,
            // 0x7C with goal 11 and sequence 7, is "an eyeball"): the string the second game's rule names, the first game none.
            UWNpc lOEye = new UWNpc(0x7C) { NPCGoal = 11, RawNpcBytes = new byte[19] };
            lOEye.RawNpcBytes[UWNpcRecordCodec.AnimationByte] = 7;
            int liEyeString = new UWObjectVariantUw2().NamelessCreatureKindString(lOEye);
            UWNpc lOWandering = new UWNpc(0x7C) { NPCGoal = 2, RawNpcBytes = new byte[19] };

            Check.Str("uw2: the Void creature's kind by sequence (goal 11, sequence 7), other goals, the first game",
                lOStrings.Blocks[1].Strings[liEyeString] + " | " + new UWObjectVariantUw2().NamelessCreatureKindString(lOWandering)
                + " | " + new UWObjectVariantUw1().NamelessCreatureKindString(lOEye), "an_eyeball | -1 | -1");

            // NO TALISMANS IN THE SECOND GAME (per user 2026-11-22, SAVE2: an identified copper ring 0x37, the first game's
            // shield of Valor, said "You see You put the instrument away." - our block 1 string 267 there).
            Check.Str("uw2: talismans in the first game, none in the second; the second game's 0x37 and its block 1 string 267",
                new UWObjectVariantUw1().HasTalismans + " " + new UWObjectVariantUw2().HasTalismans + " | " + lOStrings.Blocks[4].Strings[0x37 + 1]
                + " | " + lOStrings.Blocks[1].Strings[267].Trim(), "True False | a_copper ring | You put the instrument away.");

            // A BRIDGE NAMED AFTER ITS FLOOR (per user 2026-11-22 on the original: the plank in SAVE2's shaft, UW2 level 49
            // tile 55/46, flags 8, is "a trapdoor"; ours said "a bridge"): the level's bridges with flags of 2 and up, each
            // by the texture it wears.
            // The first game's plate over the Wine of Compassion (level 6, 27/50, flags 2) stays "a marble floor".
            Check.Str("uw2: level 49's bridges with a floor texture, named after it (flags:name); uw1 level 6's",
                fBridgeNames(new DataImport(lsData, null, null, UWGameProfiles.Uw2), 48) + " | "
                + fBridgeNames(new DataImport(pOContext.DataPath, null, null, UWGameProfiles.Uw1), 5), "8:a trapdoor | 2:a marble floor, 8:a marble floor");

            // A BRIDGE DISGUISED AS ITS FLOOR has no crosshair label (per user 2026-11-22: the plate over the Wine of Compassion
            // gave itself away): the first game's level 6 squares with such a bridge, the second game's level 49 none.
            Check.Str("bridges in their own square's floor texture: uw1 level 6 | uw2 level 49",
                fDisguisedBridges(new DataImport(pOContext.DataPath, null, null, UWGameProfiles.Uw1), 5) + " | "
                + fDisguisedBridges(new DataImport(lsData, null, null, UWGameProfiles.Uw2), 48), "27/50 5/31 6/31 7/31 | -");

            // The crosshair's label for it (IUWGrammar.ChunkLabel, per user 2026-11-22: it said "Bridge"): the bare name without
            // its article, as UWText.ObjectName names a thing (the second game's HUD capitalises it).
            Check.Str("bare names of block 10 names: a trapdoor, an ankh, outer space",
                UWGrammar.English.ChunkLabel("a trapdoor") + " | " + UWGrammar.English.ChunkLabel("an ankh") + " | "
                + UWGrammar.English.ChunkLabel("outer space"), "trapdoor | ankh | outer space");

            UWPalettes lOPalettes = new UWPalettes(lsData);
            int liPalettes = 0;

            while (fHasPalette(lOPalettes, liPalettes))
                liPalettes++;

            Check.Int("uw2: PALS.DAT's palettes (8448 bytes of 768)", liPalettes, 11);

            // The portcullis's stripes (UWPortcullisLook, per user 2026-11-18): the first game's measured 106 to 110 are light
            // levels of its model colour 104; the same levels of the second game's grey 202 - not its slots 106 to 110.
            Check.Str("portcullis stripes: UW1 from 104 as measured, UW2 from 202 in its own light table",
                string.Join(",", UWPortcullisLook.StripeIndices(UWPortcullisLook.FirstGameBase, new UWLightLevels(pOContext.DataPath))) + " | "
                + string.Join(",", UWPortcullisLook.StripeIndices(202, new UWLightLevels(lsData))),
                "106,107,108,109,110 | 204,205,206,207,207");

            int liFiles = 0;
            int liPictures = 0;
            int liAbsent = 0;
            List<string> lOBad = new List<string>();

            // PANELS.GR is not generic: its pictures have no header (the first game reads its own with
            // fImportPanels, the second's are of another size) - phase 3 reads it.
            foreach (string lsFile in Directory.GetFiles(lsData, "*.GR"))
            {
                string lsName = Path.GetFileName(lsFile);

                if (lsName.Equals("PANELS.GR", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                byte[] lyFile = File.ReadAllBytes(lsFile);
                List<UWTexture> lOPictures = UWTextures.ReadImageFile(lyFile, lsName, lOPalettes, 0, UWTexture.TextureTypes.OBJECTS);

                liFiles++;

                for (int liAt = 0; liAt < lOPictures.Count; liAt++)
                {
                    // An entry whose offset is the file's end is absent (WEAP.GR's last 18, GENHEAD.GR's 4).
                    if (System.BitConverter.ToInt32(lyFile, 3 + (4 * liAt)) >= lyFile.Length)
                    {
                        liAbsent++;

                        continue;
                    }

                    UWTexture lOPicture = lOPictures[liAt];

                    liPictures++;

                    if (lOPicture.Width <= 0 || lOPicture.Height <= 0 || lOPicture.PaletteIndices == null
                        || lOPicture.PaletteIndices.Length < lOPicture.Width * lOPicture.Height)
                        lOBad.Add(lsName + "#" + liAt);
                }
            }

            Check.Str("uw2: every .GR file's pictures read but PANELS.GR's (files, pictures, absent entries)", liFiles + " " + liPictures + " " + liAbsent, "34 1717 28");
            Check.Str("uw2: ... each with a size and its pixels", string.Join(" ", lOBad), string.Empty);

            // Rogwump (whoami 147) has no head of his own: the load fails and the class's stands in.
            List<UWTexture> lOHeads = UWTextures.ReadImageFile(File.ReadAllBytes(Path.Combine(lsData, "CHARHEAD.GR")), "CHARHEAD.GR", lOPalettes, 0,
                UWTexture.TextureTypes.CHARHEAD);

            Check.Str("uw2: CHARHEAD.GR, Feridwyn's head is there, Rogwump's entry empty", lOHeads[142].IsEmptyEntry + " " + lOHeads[146].IsEmptyEntry, "False True");

            // LEV.ARK: 80 levels of four blocks (map, textures, automap, notes); CNV.ARK: 102 conversations
            // of 320 slots; BYT.ARK: 11 screens, 3 and 10 empty; SCD.ARK never compressed (FORMATS.md).
            Dictionary<string, string> lOExpected = new Dictionary<string, string>
            {
                { "LEV.ARK", "320 75 " }, { "CNV.ARK", "320 102 " }, { "BYT.ARK", "11 9 " }, { "SCD.ARK", "16 0 " }
            };

            foreach (string lsArchive in lOExpected.Keys)
            {
                UWArk lOArk = UWArk.Read(Path.Combine(lsData, lsArchive), UWArk.LayoutEnum.Uw2);
                int liCompressed = 0;
                List<string> lOWrong = new List<string>();

                for (int liBlock = 0; liBlock < lOArk.BlockCount; liBlock++)
                {
                    if (!lOArk.IsCompressed(liBlock))
                        continue;

                    byte[] lyBlock = lOArk.ReadBlock(liBlock);

                    if (lyBlock.Length == 0)
                        continue;

                    liCompressed++;

                    if (lyBlock.Length != lOArk.DeclaredLength(liBlock))
                        lOWrong.Add(liBlock + ":" + lyBlock.Length + "/" + lOArk.DeclaredLength(liBlock));
                }

                Check.Str("uw2: " + lsArchive + "'s blocks and compressed blocks, each decoding to its declared length",
                    lOArk.BlockCount + " " + liCompressed + " " + string.Join(" ", lOWrong), lOExpected[lsArchive]);
            }

            string lsCrit = Path.Combine(Path.GetDirectoryName(lsData), "CRIT");

            Check.Bool("uw2: the critters' own files are there (AS.AN, CR.AN, PG.MP), not the first game's ASSOC.ANM",
                File.Exists(Path.Combine(lsCrit, "AS.AN")) && File.Exists(Path.Combine(lsCrit, "CR.AN")) && File.Exists(Path.Combine(lsCrit, "PG.MP"))
                && !File.Exists(Path.Combine(lsCrit, "ASSOC.ANM")), true);
            Check.Bool("uw2: ... so the first game's critter reader finds nothing there (phase 1 reads them)", new UWCritterAnimations(lsData).IsLoaded, false);

            fCheckUw2Teachers(lsData);
        }

        /// <summary>The second game's teachers as the help reads them from the scripts (UWTrainersUw2), and a met teacher
        /// kept through UWR.json's lines (UWKnowledge).</summary>
        private static void fCheckUw2Teachers(string psData)
        {
            DataImport lOImport = new DataImport(psData, null, null, UWGameProfiles.Uw2);
            List<string> lsTeachers = new List<string>();

            foreach (KeyValuePair<int, List<int>> lOTeacher in UWTrainersUw2.All(lOImport.Conversations))
                lsTeachers.Add(lOTeacher.Key + ":" + string.Join(",", lOTeacher.Value));

            // Only reachable code (per user 2026-11-19): Dupre (132) has an unused sword lesson, 4,5 not 4,5,3.
            Check.Str("uw2 teachers: the conversations that advance a skill for a skill point (x_skills above 10000) and their skills",
                string.Join(" ", lsTeachers),
                "4:2 41:3 43:8 45:9,7 102:4,5 128:16,13,10,17 130:7,9 132:4,5 133:0,1 134:6,19,18 135:10,16,14 139:8,11 140:15 141:15 144:12 168:3,2");

            UWKnowledge lOKnown = new UWKnowledge();

            lOKnown.MetTeacher(133, 1, "Geoffrey");
            lOKnown.MetTeacher(133, 5, "Other");
            UWKnowledge lOBack = UWKnowledge.Import(lOKnown.Export());

            Check.Str("uw2 teachers: the first meeting kept, through UWR.json's lines",
                (lOBack.HasMetTeacher(133, out UWKnowledge.Met lOMet) ? lOMet.Level + " " + lOMet.Who : "none") + " " + lOBack.HasMetTeacher(4, out _),
                "1 Geoffrey False");
        }

        private static bool fHasPalette(UWPalettes pOPalettes, int piIndex)
        {
            try
            {
                return pOPalettes.GetPalette(piIndex) != null;
            }
            catch (System.ArgumentOutOfRangeException)
            {
                return false;
            }
        }
    }
}
