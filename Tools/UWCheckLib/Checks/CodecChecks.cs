using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The codecs of the cleanup's phase 2 (UWObjectRecordCodec, UWNpcRecordCodec,
    /// UWLevArkLayout, UWLevArkCodec; Docs/CLEANUP-PLAN.md step "Codecs", 2026-10-12): the
    /// layout numbers against the original's level.h, the flag bits against object.h (B13,
    /// decision 4), and encode(decode(record)) identity on every record of DATA and of every
    /// save under --saves - plus the LOADED objects written back through the codecs against
    /// the file's bytes, which is what the save round trip of UWLevelWriter rests on.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckCodecs(CheckContext pOContext)
        {
            Check.Group("Codecs (UWObjectRecordCodec, UWNpcRecordCodec, UWLevArkLayout, UWLevArkCodec)");

            fCheckLevArkLayout();
            fCheckObjectRecordBits();

            if (pOContext.Data != null)
            {
                fCheckCodecsOnWorld(pOContext.Data, pOContext.DataPath, "DATA");
                fCheckCodecsOnInventory(Path.Combine(pOContext.DataPath, "PLAYER.DAT"), "DATA");
            }

            if (string.IsNullOrEmpty(pOContext.SavesPath))
            {
                Check.Pass("codecs: saves", "none given (--saves <folder> checks every SAVE* in it)");

                return;
            }

            foreach (string lsSave in Directory.GetDirectories(pOContext.SavesPath))
            {
                if (!File.Exists(Path.Combine(lsSave, "LEV.ARK")) || !File.Exists(Path.Combine(lsSave, "PLAYER.DAT")))
                    continue;

                string lsName = Path.GetFileName(lsSave);
                DataImport lOData;

                try
                {
                    lOData = new DataImport(pOContext.DataPath, lsSave);
                }
                catch (Exception lOException)
                {
                    Check.Fail("codecs: " + lsName + " loads", lOException.Message);

                    continue;
                }

                fCheckCodecsOnWorld(lOData, lsSave, lsName);
                fCheckCodecsOnInventory(Path.Combine(lsSave, "PLAYER.DAT"), lsName);
            }
        }

        // ------------------------------------------------- the layout, level.h

        private static void fCheckLevArkLayout()
        {
            Check.Int("layout: a level block is 0x7C08 bytes (LEVEL_SIZE)", UWLevArkLayout.BlockSize, 0x7C08);
            Check.Int("layout: the tile map fills 0x4000 bytes, the mobile objects start there",
                UWLevArkLayout.TileMapSize, UWLevArkLayout.MobileBase);
            Check.Int("layout: 256 mobile records of 27 bytes end at the static ones, 0x5B00",
                UWLevArkLayout.MobileBase + (UWLevArkLayout.MobileCount * UWLevArkLayout.MobileRecordSize), UWLevArkLayout.StaticBase);
            Check.Int("layout: 768 static records of 8 bytes end at the mobile free list, 0x7300",
                UWLevArkLayout.MobileBase + UWLevArkLayout.ObjectListSize, UWLevArkLayout.MobileFreeListOffset);
            Check.Int("layout: the mobile free list's 254 entries end at the static one, 0x74FC",
                UWLevArkLayout.MobileFreeListOffset + (UWLevArkLayout.MobileFreeListCapacity * 2), UWLevArkLayout.StaticFreeListOffset);
            Check.Int("layout: the static free list's 768 entries end at the active list, 0x7AFC",
                UWLevArkLayout.StaticFreeListOffset + (UWLevArkLayout.StaticFreeListCapacity * 2), UWLevArkLayout.ActiveMobileListOffset);
            Check.Int("layout: the active list's 260 bytes end at nactive, 0x7C00",
                UWLevArkLayout.ActiveMobileListOffset + UWLevArkLayout.ActiveMobileListCapacity, UWLevArkLayout.ActiveMobileCountOffset);
            Check.Seq("layout: nactive, nmobfree, nstaticfree, magic at 0x7C00, 02, 04, 06",
                new[] { UWLevArkLayout.ActiveMobileCountOffset, UWLevArkLayout.MobileFreeTopOffset, UWLevArkLayout.StaticFreeTopOffset, UWLevArkLayout.MagicOffset },
                new[] { 0x7C00, 0x7C02, 0x7C04, 0x7C06 });
            Check.Int("layout: the magic word ends the block", UWLevArkLayout.MagicOffset + 2, UWLevArkLayout.BlockSize);
            Check.Int("layout: the player's record is object 1 at 0x401B", UWLevArkLayout.PlayerSlotOffset, 0x401B);
            Check.Int("layout: object 256 is the first static record", UWLevArkLayout.ObjectOffset(UWLevArkLayout.MobileCount), UWLevArkLayout.StaticBase);
            Check.Int("layout: object 1023 is the last one before the free lists",
                UWLevArkLayout.ObjectOffset(UWLevArkLayout.ObjectCount - 1) + UWLevArkLayout.StaticRecordSize, UWLevArkLayout.MobileFreeListOffset);
            Check.Seq("layout: the five kinds of blocks start at 0, 9, 18, 27, 36 (LEVARK_*)",
                new[] { UWLevArkLayout.MapBlock(0), UWLevArkLayout.AnimationBlock(0), UWLevArkLayout.TextureMapBlock(0), UWLevArkLayout.AutomapBlock(0), UWLevArkLayout.MapNoteBlock(0) },
                new[] { 0, 9, 18, 27, 36 });
            Check.Int("layout: 36 + 99 note blocks are the file's 135", UWLevArkLayout.DeclaredBlockCount, 135);

            // The active list's two operations, active_critter and free_critter.
            List<int> lOActive = new List<int> { 5, 9, 12, 20 };

            UWLevArkCodec.RemoveActiveMobile(lOActive, 9);
            Check.Seq("layout: free_critter moves the last entry into the hole", lOActive.ToArray(), new[] { 5, 20, 12 });
            UWLevArkCodec.RemoveActiveMobile(lOActive, 12);
            Check.Seq("layout: ... and simply shortens the list when the last one goes", lOActive.ToArray(), new[] { 5, 20 });
            Check.Bool("layout: a slot not in the list is reported", UWLevArkCodec.RemoveActiveMobile(lOActive, 77), false);
            UWLevArkCodec.AddActiveMobile(lOActive, 9);
            UWLevArkCodec.AddActiveMobile(lOActive, 9);
            Check.Seq("layout: active_critter appends, once", lOActive.ToArray(), new[] { 5, 20, 9 });

            // A free list written and read: the pointer is the top entry's index, -1 when empty.
            byte[] lyBlock = new byte[UWLevArkLayout.BlockSize];

            UWLevArkCodec.WriteMobileFreeList(lyBlock, 0, new List<int> { 250, 7, 3 });
            Check.Int("layout: nmobfree is the index of the top entry", (short)UWBinary.Read16(lyBlock, UWLevArkLayout.MobileFreeTopOffset), 2);
            Check.Seq("layout: the mobile free list reads back in storage order, the top last",
                UWLevArkCodec.ReadMobileFreeList(lyBlock, 0).ToArray(), new[] { 250, 7, 3 });
            UWLevArkCodec.WriteStaticFreeList(lyBlock, 0, new List<int>());
            Check.Int("layout: an empty free list stores -1", (short)UWBinary.Read16(lyBlock, UWLevArkLayout.StaticFreeTopOffset), -1);
            Check.Int("layout: ... and reads back empty", UWLevArkCodec.ReadStaticFreeList(lyBlock, 0).Count, 0);
        }

        // ------------------------------------------------- the record bits, object.h

        private static void fCheckObjectRecordBits()
        {
            byte[] lyRecord = new byte[UWObjectRecordCodec.RecordSize];
            byte[] lyAgain = new byte[UWObjectRecordCodec.RecordSize];

            UWBinary.Write16(lyRecord, 0, 0x1E00 | 0x123);
            UWBinary.Write16(lyRecord, 2, (5 << 13) | (6 << 10) | (3 << 7) | 0x7F);
            UWBinary.Write16(lyRecord, 4, (0x3FF << 6) | 0x3F);
            UWBinary.Write16(lyRecord, 6, (0x2AA << 6) | 0x15);

            UWObject lOObject = new UWObject((ushort)UWObjectRecordCodec.ReadId(lyRecord, 0));

            UWObjectRecordCodec.Read(lyRecord, 0, lOObject);

            Check.Int("record: the id is the nine low bits (ID_ITEM)", lOObject.ID, 0x123);
            Check.Int("record: ID_FLAGS 0x1E00 reads as FOUR bits (OBJ_FLAGS)", lOObject.Flags, 0xF);
            Check.Bool("record: ID_ENCHANT 0x1000 is bit 3 of them, IsEnchanted", lOObject.IsEnchanted, true);
            Check.Seq("record: xpos, ypos, heading, zpos from word 1",
                new[] { (int)lOObject.XPos, (int)lOObject.YPos, (int)lOObject.Heading, lOObject.ZPos }, new[] { 5, 6, 3, 0x7F });
            Check.Seq("record: link and quality from word 2, quantity and owner from word 3",
                new[] { (int)lOObject.Link, (int)lOObject.Quality, (int)lOObject.Quantity, (int)lOObject.Owner }, new[] { 0x3FF, 0x3F, 0x2AA, 0x15 });
            Check.Bool("record: doordir, invisible and is_quant are clear here",
                lOObject.DoorDirection || lOObject.IsHidden || lOObject.HasQuantity, false);

            UWObjectRecordCodec.Write(lyAgain, 0, lOObject);
            Check.Bool("record: encode(decode(record)) is the record", fSameBytes(lyAgain, lyRecord, out _), true);

            lOObject.IsEnchanted = false;
            Check.Int("record: clearing IsEnchanted clears bit 3 of Flags, nothing else", lOObject.Flags, 0x7);
            lOObject.Flags = 8;
            Check.Bool("record: Flags 8 is enchanted - one store, not two", lOObject.IsEnchanted, true);

            // B13: the chain-removal trap counts its flags down through SET_FLAGS
            // (trigger_obj_del); a count that fell from 8 to 7 came back as 15 after a save
            // until 2026-10-12, because the writer re-added the stale enchanted bool to three bits.
            lOObject.Flags = 7;
            UWObjectRecordCodec.Write(lyAgain, 0, lOObject);
            Check.Int("record: SET_FLAGS writes all four bits - 7 stays 7 (B13)", (UWBinary.Read16(lyAgain, 0) >> 9) & 0x1F, 7);
            lOObject.Flags = 0xF;
            UWObjectRecordCodec.Write(lyAgain, 0, lOObject);
            Check.Int("record: ... and 15 stays 15", (UWBinary.Read16(lyAgain, 0) >> 9) & 0x1F, 0xF);

            UWBinary.Write16(lyRecord, 0, 0xE000 | 0x1FF);
            UWObjectRecordCodec.Read(lyRecord, 0, lOObject);
            Check.Bool("record: bit 13 doordir, 14 invisible, 15 is_quant",
                lOObject.DoorDirection && lOObject.IsHidden && lOObject.HasQuantity && lOObject.Flags == 0, true);
            Check.Bool("record: ReadIsQuantity sees bit 15 in the bytes", UWObjectRecordCodec.ReadIsQuantity(lyRecord, 0), true);
            Check.Int("record: ReadLink and ReadQuantityOrLink see words 2 and 3",
                UWObjectRecordCodec.ReadLink(lyRecord, 0) + UWObjectRecordCodec.ReadQuantityOrLink(lyRecord, 0), 0x3FF + 0x2AA);

            // The writer's three overrides: id, link and the last field.
            UWObjectRecordCodec.Write(lyAgain, 0, lOObject, 0x80, 0x155, 0x3AA);
            Check.Seq("record: the writer's id, link and quantity overrides land in their fields",
                new[] { UWObjectRecordCodec.ReadId(lyAgain, 0), UWObjectRecordCodec.ReadLink(lyAgain, 0), UWObjectRecordCodec.ReadQuantityOrLink(lyAgain, 0) },
                new[] { 0x80, 0x155, 0x3AA });
            UWObjectRecordCodec.ClearId(lyAgain, 0);
            Check.Int("record: ClearId frees the slot's number and keeps the rest", UWBinary.Read16(lyAgain, 0), 0xE000);

            // The nineteen creature bytes: parse, write back, and the parsed fields.
            byte[] lyNpc = new byte[UWNpcRecordCodec.ByteCount];

            for (int liAt = 0; liAt < lyNpc.Length; liAt++)
                lyNpc[liAt] = (byte)(0x11 * (liAt + 1));

            UWNpc lONpc = new UWNpc(0x40);

            UWNpcRecordCodec.Read(lyNpc, 0, lONpc);
            Check.Int("npc: hit points are byte 0x08 as it is", lONpc.HitPoints, 0x11);
            Check.Seq("npc: goal and gtarg from word 0x0B",
                new[] { (int)lONpc.NPCGoal, (int)lONpc.NPCGTarg }, new[] { 0x44 & 0xF, (0x5544 >> 4) & 0xFF });
            Check.Seq("npc: backup goal and attitude from word 0x0D",
                new[] { (int)lONpc.NPCLevel, (int)lONpc.NPCAttitude }, new[] { 0x66 & 0xF, 0x7766 >> 14 });
            Check.Bool("npc: the spawned, no-healing, loot and talked-to bits of word 0x0D",
                lONpc.NPCSpawned == ((0x7766 & 0x100) != 0) && lONpc.NPCNoHealing == ((0x7766 & 0x200) != 0)
                && lONpc.NPCLootSpawned == ((0x7766 & 0x1000) != 0) && lONpc.NPCTalkedTo == ((0x7766 & 0x2000) != 0), true);
            int liHome = UWBinary.Read16(lyNpc, 14);

            Check.Seq("npc: the current tile from word 0x16", new[] { (int)lONpc.NPCXHome, (int)lONpc.NPCYHome },
                new[] { liHome >> 10, (liHome >> 4) & 0x3F });
            Check.Seq("npc: facing residual, hunger byte, whoami from 0x18, 0x19, 0x1A",
                new[] { (int)lONpc.NPCHeading, (int)lONpc.NPCHunger, (int)lONpc.NPCwhoami }, new[] { lyNpc[16] & 0x1F, lyNpc[17] & 0x7F, (int)lyNpc[18] });
            Check.Bool("npc: the ally bit is bit 6 of byte 0x19", lONpc.NPCIsAlly, (lyNpc[17] & 0x40) != 0);
            Check.Bool("npc: attitude locked is bit 7 of byte 0x0A", lONpc.NPCAttitudeLocked, (lyNpc[2] & 0x80) != 0);

            byte[] lyNpcAgain = new byte[UWNpcRecordCodec.ByteCount];

            UWNpcRecordCodec.Write(lyNpcAgain, 0, lONpc);
            Check.Bool("npc: encode(decode(bytes)) is the bytes", fSameBytes(lyNpcAgain, lyNpc, out _), true);

            lONpc.HitPoints = 0x77;
            lONpc.NPCGoal = 5;
            lONpc.NPCAttitude = 0;
            UWNpcRecordCodec.Write(lyNpcAgain, 0, lONpc);
            Check.Seq("npc: a changed field lands in its bits and the neighbours stay",
                new[] { (int)lyNpcAgain[0], UWBinary.Read16(lyNpcAgain, 3), UWBinary.Read16(lyNpcAgain, 5) },
                new[] { 0x77, (0x5544 & ~0xF) | 5, 0x7766 & 0x3FFF });
        }

        // ------------------------------------------------- real records

        /// <summary>Every object record of the nine levels: decoded and encoded again, and
        /// the loaded model encoded, both against the file's bytes; then the three lists.</summary>
        private static void fCheckCodecsOnWorld(DataImport pOData, string psFolder, string psLabel)
        {
            byte[] lyArk = File.ReadAllBytes(Path.Combine(psFolder, "LEV.ARK"));
            List<uint> lOOffsets = UWLevArkCodec.ReadBlockOffsets(lyArk);

            Check.Int("codecs: " + psLabel + " LEV.ARK declares 135 blocks", lOOffsets.Count, UWLevArkLayout.DeclaredBlockCount);

            int liMagic = 0;
            int liRecordMismatch = 0;
            int liNpcMismatch = 0;
            int liModelMismatch = 0;
            int liListMismatch = 0;
            int liOrderMismatch = 0;
            byte[] lyBuffer = new byte[UWLevArkLayout.MobileRecordSize];

            for (int liLevel = 0; liLevel < UWLevArkLayout.LevelBlockCount; liLevel++)
            {
                int liStart = (int)lOOffsets[UWLevArkLayout.MapBlock(liLevel)];
                UWLevel lOLevel = pOData.Levels != null && liLevel < pOData.Levels.Count ? pOData.Levels[liLevel] : null;

                if (liStart == 0 || liStart + UWLevArkLayout.BlockSize > lyArk.Length || lOLevel == null || lOLevel.Masterlist == null)
                {
                    Check.Fail("codecs: " + psLabel + " level " + (liLevel + 1) + " has a block and a loaded level", "missing");

                    continue;
                }

                if (UWBinary.Read16(lyArk, liStart + UWLevArkLayout.MagicOffset) == UWLevArkLayout.Magic)
                    liMagic++;

                for (int liIndex = 0; liIndex < UWLevArkLayout.ObjectCount; liIndex++)
                {
                    int liAt = liStart + UWLevArkLayout.ObjectOffset(liIndex);
                    bool lbMobile = UWLevArkLayout.IsMobileSlot(liIndex);
                    ushort lyId = (ushort)UWObjectRecordCodec.ReadId(lyArk, liAt);
                    UWObject lOFresh = lbMobile ? new UWNpc(lyId) : new UWObject(lyId);

                    UWObjectRecordCodec.Read(lyArk, liAt, lOFresh);
                    UWObjectRecordCodec.Write(lyBuffer, 0, lOFresh);

                    if (!fSameSpan(lyBuffer, 0, lyArk, liAt, UWObjectRecordCodec.RecordSize))
                        liRecordMismatch++;

                    if (lbMobile)
                    {
                        UWNpcRecordCodec.Read(lyArk, liAt + UWObjectRecordCodec.RecordSize, (UWNpc)lOFresh);
                        UWNpcRecordCodec.Write(lyBuffer, UWObjectRecordCodec.RecordSize, (UWNpc)lOFresh);

                        if (!fSameSpan(lyBuffer, UWObjectRecordCodec.RecordSize, lyArk, liAt + UWObjectRecordCodec.RecordSize, UWNpcRecordCodec.ByteCount))
                            liNpcMismatch++;
                    }

                    UWObject lOLoaded = liIndex < lOLevel.Masterlist.Count ? lOLevel.Masterlist[liIndex] : null;

                    if (lOLoaded == null)
                    {
                        liModelMismatch++;

                        continue;
                    }

                    UWObjectRecordCodec.Write(lyBuffer, 0, lOLoaded);

                    if (lbMobile && lOLoaded is UWNpc lONpc)
                        UWNpcRecordCodec.Write(lyBuffer, UWObjectRecordCodec.RecordSize, lONpc);

                    int liLength = lbMobile ? UWLevArkLayout.MobileRecordSize : UWLevArkLayout.StaticRecordSize;

                    if (!fSameSpan(lyBuffer, 0, lyArk, liAt, liLength))
                        liModelMismatch++;
                }

                // The three lists through the codec: read, written onto a copy, identical.
                byte[] lyCopy = new byte[UWLevArkLayout.BlockSize];

                Array.Copy(lyArk, liStart, lyCopy, 0, lyCopy.Length);

                List<int> lOMobileFree = UWLevArkCodec.ReadMobileFreeList(lyArk, liStart);
                List<int> lOStaticFree = UWLevArkCodec.ReadStaticFreeList(lyArk, liStart);
                List<int> lOActive = UWLevArkCodec.ReadActiveMobiles(lyArk, liStart);

                UWLevArkCodec.WriteMobileFreeList(lyCopy, 0, lOMobileFree);
                UWLevArkCodec.WriteStaticFreeList(lyCopy, 0, lOStaticFree);
                UWLevArkCodec.WriteActiveMobiles(lyCopy, 0, lOActive);

                if (!fSameSpan(lyCopy, 0, lyArk, liStart, UWLevArkLayout.BlockSize))
                    liListMismatch++;

                // The loaded level's own lists agree with the codec's reading.
                for (int liAt = 0; liAt < lOActive.Count; liAt++)
                {
                    if (lOLevel.GetActiveMobileOrder(lOActive[liAt]) != liAt)
                        liOrderMismatch++;
                }

                foreach (int liSlot in lOStaticFree)
                {
                    if (!lOLevel.IsStaticSlotFree(liSlot))
                        liOrderMismatch++;
                }
            }

            Check.Int("codecs: " + psLabel + ": every level block ends with the magic word", liMagic, UWLevArkLayout.LevelBlockCount);
            Check.Int("codecs: " + psLabel + ": 9 x 1024 object records decode and encode identically", liRecordMismatch, 0);
            Check.Int("codecs: " + psLabel + ": 9 x 256 creature records decode and encode identically", liNpcMismatch, 0);
            Check.Int("codecs: " + psLabel + ": the loaded objects write back the file's bytes", liModelMismatch, 0);
            Check.Int("codecs: " + psLabel + ": the free lists and the active list read and write identically", liListMismatch, 0);
            Check.Int("codecs: " + psLabel + ": the loaded level's lists agree with the codec's", liOrderMismatch, 0);
        }

        /// <summary>The inventory records of PLAYER.DAT: the same eight bytes as a level's.</summary>
        private static void fCheckCodecsOnInventory(string psPlayerDat, string psLabel)
        {
            byte[] lyRaw = File.ReadAllBytes(psPlayerDat);
            byte[] lyPlain = UWPlayerData.IsPlainTemplate(lyRaw) ? lyRaw : UWPlayerData.Decrypt(lyRaw);
            byte[] lyBuffer = new byte[UWObjectRecordCodec.RecordSize];
            int liRecords = 0;
            int liMismatch = 0;

            for (int liAt = UWPlayerData.InventoryRecordOffset; liAt + UWPlayerData.InventoryRecordSize <= lyPlain.Length; liAt += UWPlayerData.InventoryRecordSize)
            {
                UWObject lOObject = new UWObject((ushort)UWObjectRecordCodec.ReadId(lyPlain, liAt));

                UWObjectRecordCodec.Read(lyPlain, liAt, lOObject);
                UWObjectRecordCodec.Write(lyBuffer, 0, lOObject);
                liRecords++;

                if (!fSameSpan(lyBuffer, 0, lyPlain, liAt, UWObjectRecordCodec.RecordSize))
                    liMismatch++;
            }

            Check.Int("codecs: " + psLabel + " PLAYER.DAT: " + liRecords + " inventory records decode and encode identically", liMismatch, 0);
        }

        private static bool fSameSpan(byte[] pyLeft, int piLeftAt, byte[] pyRight, int piRightAt, int piLength)
        {
            for (int liAt = 0; liAt < piLength; liAt++)
            {
                if (pyLeft[piLeftAt + liAt] != pyRight[piRightAt + liAt])
                    return false;
            }

            return true;
        }
    }
}
