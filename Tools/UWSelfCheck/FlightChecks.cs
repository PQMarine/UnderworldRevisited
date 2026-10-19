using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THINGS IN FLIGHT IN THE LEVEL DATA (2026-10-19): a mobile non-creature on the active list and
    /// in a tile's list is a flight (move_mobile steps it, UnderworldExhumed critter/AI.C:1539-1567),
    /// read into the motion's record (UWMobileRecordCodec), taken out of its tile's list into
    /// UWLevel.InFlight and written back by UWLevelWriter as the original keeps it. Pinned with the
    /// bytes of the user's arrow shot during Freeze Time in the original (level 1, slot 246): the
    /// codec's fields and its round trip, a level block with that arrow poked into it (read as a
    /// flight, written back byte for byte, moved to the next tile at the head of its list), a flight
    /// begun in ours (a mobile slot, the active list, read back as a flight), the missile's base at
    /// the hit by its owner byte (missile_newhit), and the game's own nine levels hold no flight.
    /// </summary>
    public static partial class Program
    {
        /// <summary>The user's arrow, slot 246 of level 1 (2026-10-19): bytes 8..26.</summary>
        private static readonly byte[] msyUsersArrow =
        {
            0x1f, 0xf7, 0x01, 0x6f, 0x2b, 0xaf, 0x3a, 0x98, 0x03, 0x00, 0x01, 0x96, 0x81, 0x2c, 0xa0, 0xaf, 0x57, 0x80, 0x00
        };

        private const int ArrowId = 0x12;

        private static UWObject fUsersArrowObject()
        {
            return new UWObject(ArrowId) { XPos = 3, YPos = 5, ZPos = 115, Heading = 7, HasQuantity = true, Quantity = 1 };
        }

        private static void fCheckFlights(CheckContext pOContext)
        {
            Check.Group("Things in flight in the level data (UWMobileRecordCodec, UWLevel.InFlight, UWLevelWriter)");

            fCheckFlightCodec();
            fCheckFlightHitBase(pOContext.Data);

            DataImport lOData = pOContext.Data;
            int liLevels = 0;
            int liFlights = 0;

            for (int liAt = 0; lOData != null && lOData.Levels != null && liAt < lOData.Levels.Count; liAt++)
            {
                liLevels++;
                liFlights += lOData.Levels[liAt].InFlight.Count;
            }

            Check.Int("data: the game's nine levels hold no thing in flight", liLevels == 9 ? liFlights : -1, 0);

            fCheckFlightInBlock(pOContext);
        }

        private static void fCheckFlightCodec()
        {
            UWMobileRecord lORecord = UWMobileRecordCodec.Read(fUsersArrowObject(), msyUsersArrow, 43, 58);

            Check.Str("codec: the user's arrow - heading, speed, gravity, vertical speed, period, phase",
                string.Join(" ", lORecord.HeadingByte, lORecord.SpeedByte, lORecord.GravityBit, lORecord.VzField, lORecord.Period,
                    lORecord.Phase), "247 22 True 16 1 1");
            Check.Str("codec: ... fine x, y, z (tile 43/58, z 115 at eye height)",
                string.Join(" ", lORecord.FineX >> 8, lORecord.FineY >> 8, lORecord.FineZ >> 3), "43 58 115");
            Check.Str("codec: ... owner (the player's 1), hp, octant, eighths, static heading, collided",
                string.Join(" ", lORecord.Owner, lORecord.Hp, lORecord.Octant, lORecord.XPos, lORecord.YPos, lORecord.StaticHeading,
                    lORecord.CollidedWithMobile), "1 31 7 3 5 0 False");
            Check.Str("codec: written back over its own bytes, byte for byte",
                BitConverter.ToString(UWMobileRecordCodec.Write(lORecord, msyUsersArrow)), BitConverter.ToString(msyUsersArrow));

            // A flight begun in ours: no bytes of its own, the motion's fields alone.
            UWMobileRecord lOOurs = new UWMobileRecord
            {
                Object = fUsersArrowObject(), TileX = 10, TileY = 20, HeadingByte = 0x45, SpeedByte = 0x14, GravityBit = true,
                VzField = 18, Period = 1, Phase = 9, ContactIndex = 0, FineX = (10 << 8) + 0x4F, FineY = (20 << 8) + 0x2F,
                FineZ = 300, Owner = 1, Hp = 40, FineHeadingBits = 5, StaticHeading = 2, CollidedWithMobile = true
            };
            UWMobileRecord lOBack = UWMobileRecordCodec.Read(lOOurs.Object, UWMobileRecordCodec.Write(lOOurs, null), 10, 20);

            Check.Str("codec: a flight of ours written and read back",
                string.Join(" ", lOBack.HeadingByte, lOBack.SpeedByte, lOBack.GravityBit, lOBack.VzField, lOBack.Period, lOBack.Phase,
                    lOBack.FineX, lOBack.FineY, lOBack.FineZ, lOBack.Owner, lOBack.Hp, lOBack.FineHeadingBits, lOBack.StaticHeading,
                    lOBack.CollidedWithMobile),
                string.Join(" ", 0x45, 0x14, true, 18, 1, 9, (10 << 8) + 0x4F, (20 << 8) + 0x2F, 300, 1, 40, 5, 2, true));

            fExpectBool("in flight: a non-creature on the active list in a tile's list",
                UWMobileRecordCodec.IsInFlight(new UWNpc(ArrowId), true, true), true);
            fExpectBool("in flight: not off the active list (an item in a mobile slot lies still)",
                UWMobileRecordCodec.IsInFlight(new UWNpc(ArrowId), false, true), false);
            fExpectBool("in flight: not when in no tile's list (a free slot)",
                UWMobileRecordCodec.IsInFlight(new UWNpc(ArrowId), true, false), false);
            fExpectBool("in flight: never a creature", UWMobileRecordCodec.IsInFlight(new UWNpc(0x40), true, true), false);
        }

        /// <summary>missile_newhit (UnderworldExhumed motion/OBJPHYS.C:102-132): the table's damage,
        /// the player's own ammunition scaled by the Missile skill and its check; no damage for what
        /// is no missile object.</summary>
        private static void fCheckFlightHitBase(DataImport pOData)
        {
            if (pOData == null || pOData.ObjectProperties == null)
            {
                Check.Fail("hit base: object properties", "not loaded");

                return;
            }

            int liTable = pOData.ObjectProperties.GetRangedDamage(ArrowId);

            fExpectInt("hit base: a creature's arrow (owner 6) does the table's damage",
                UWPlayerAttack.GetMissileHitBaseDamage(pOData, ArrowId, 6, 30), liTable);
            fExpectInt("hit base: a trap's arrow (owner 0) the same",
                UWPlayerAttack.GetMissileHitBaseDamage(pOData, ArrowId, 0, 30), liTable);
            fExpectInt("hit base: a thrown sword is no missile",
                UWPlayerAttack.GetMissileHitBaseDamage(pOData, 0x04, 1, 30), UWAttackResolution.NoMissileDamage);

            UWRandom.UseSeed(7);

            int liScaled = UWPlayerAttack.GetThrownMissileBaseDamage(pOData, ArrowId, 30);

            UWRandom.UseSeed(7);

            fExpectInt("hit base: the player's own arrow (owner 1) scaled by his Missile skill and its check",
                UWPlayerAttack.GetMissileHitBaseDamage(pOData, ArrowId, 1, 30), liScaled);
            UWRandom.Use(null);
        }

        /// <summary>The user's arrow poked into level 1 of the game's LEV.ARK at tile 43/58: read as a
        /// flight, written back unchanged byte for byte; moved one tile east it goes to the head of
        /// the new tile's list and the old list is as it was; a flight of ours takes the top mobile
        /// slot and the active list.</summary>
        private static void fCheckFlightInBlock(CheckContext pOContext)
        {
            string lsArk = pOContext.DataPath != null ? Path.Combine(pOContext.DataPath, "LEV.ARK") : null;

            if (lsArk == null || !File.Exists(lsArk) || pOContext.Data == null)
            {
                Check.Fail("block: LEV.ARK", "not found");

                return;
            }

            byte[] lyArk = File.ReadAllBytes(lsArk);
            List<uint> lOOffsets = UWLevArkCodec.ReadBlockOffsets(lyArk);
            int liStart = (int)lOOffsets[UWLevArkLayout.MapBlock(0)];
            int liTileAt = liStart + (((58 * UWWorldScale.TilesPerAxis) + 43) * UWLevArkLayout.TileRecordSize);
            uint luTile = UWBinary.Read32(lyArk, liTileAt);
            int liOldHead = (int)(luTile >> UWLevArkLayout.TileFirstObjectShift);
            List<int> lOFree = UWLevArkCodec.ReadMobileFreeList(lyArk, liStart);
            int liSlot = lOFree[lOFree.Count - 1];

            lOFree.RemoveAt(lOFree.Count - 1);
            UWLevArkCodec.WriteMobileFreeList(lyArk, liStart, lOFree);

            List<int> lOActive = UWLevArkCodec.ReadActiveMobiles(lyArk, liStart);

            UWLevArkCodec.AddActiveMobile(lOActive, liSlot);
            UWLevArkCodec.WriteActiveMobiles(lyArk, liStart, lOActive);

            int liRecord = liStart + UWLevArkLayout.ObjectOffset(liSlot);

            UWObjectRecordCodec.Write(lyArk, liRecord, fUsersArrowObject(), ArrowId, liOldHead, 1);
            Array.Copy(msyUsersArrow, 0, lyArk, liRecord + UWObjectRecordCodec.RecordSize, msyUsersArrow.Length);
            UWBinary.Write32(lyArk, liTileAt, (int)((luTile & ((1u << UWLevArkLayout.TileFirstObjectShift) - 1))
                | ((uint)liSlot << UWLevArkLayout.TileFirstObjectShift)));

            byte[] lyBlock = new byte[UWLevArkLayout.BlockSize];

            Array.Copy(lyArk, liStart, lyBlock, 0, lyBlock.Length);

            UWLevel lOLevel = new UWLevel(1, lyArk, (uint)liStart, lOOffsets[UWLevArkLayout.TextureMapBlock(0)], pOContext.Data.Textures);
            UWTile lOTile = lOLevel.GetTile(43, 58);
            UWMobileRecord lORecord = lOLevel.InFlight.Count == 1 ? lOLevel.InFlight[0] : null;

            fExpectBool("block: the arrow is a thing in flight in its slot, out of its tile's list",
                lORecord != null && lORecord.HeadingByte == 0xF7 && lORecord.Owner == 1 && lORecord.ChainPosition == 0
                    && lORecord.Slot == liSlot
                    && !lOTile.ObjectsInTile.Contains(lORecord.Object), true);

            if (lORecord == null)
                return;

            Check.Str("block: written back unchanged, byte for byte",
                fFirstDifference(UWLevelWriter.BuildBlock(lOLevel, lyBlock), lyBlock), "none");

            // One step east into tile 44/58, as set_phys_data moves it: at the head of the new list.
            lORecord.FineX += 0x100;
            lORecord.TileX = 44;
            lORecord.ChainPosition = 0;
            lORecord.HeadingByte = 0x40;

            byte[] lyMoved = UWLevelWriter.BuildBlock(lOLevel, lyBlock);
            int liNewHead = (int)(UWBinary.Read32(lyMoved, ((58 * UWWorldScale.TilesPerAxis) + 44) * UWLevArkLayout.TileRecordSize)
                >> UWLevArkLayout.TileFirstObjectShift);
            int liOldTileHead = (int)(UWBinary.Read32(lyMoved, ((58 * UWWorldScale.TilesPerAxis) + 43) * UWLevArkLayout.TileRecordSize)
                >> UWLevArkLayout.TileFirstObjectShift);
            int liMovedRecord = UWLevArkLayout.ObjectOffset(liSlot);

            fExpectBool("block: moved, at the head of tile 44/58, the old tile's head as before the shot",
                liNewHead == liSlot && liOldTileHead == liOldHead, true);
            Check.Str("block: ... its heading byte, x and home tile written",
                string.Join(" ", lyMoved[liMovedRecord + 9], UWBinary.ReadU16(lyMoved, liMovedRecord + 0x0B) >> 8,
                    (UWBinary.ReadU16(lyMoved, liMovedRecord + 0x16) >> 10) & 0x3F), "64 44 44");
            fExpectBool("block: ... still on the active list",
                UWLevArkCodec.ReadActiveMobiles(lyMoved, 0).Contains(liSlot), true);

            // A flight begun in ours: a fresh arrow in tile 43/58, no slot yet.
            UWObject lOFresh = fUsersArrowObject();
            UWMobileRecord lOOurs = UWMobileRecordCodec.Read(lOFresh, null, 43, 58);

            lOOurs.HeadingByte = 0x20;
            lOOurs.SpeedByte = 0x14;
            lOOurs.GravityBit = true;
            lOOurs.VzField = 17;
            lOOurs.Period = 1;
            lOOurs.FineX = (43 << 8) + 0x6F;
            lOOurs.FineY = (58 << 8) + 0xAF;
            lOOurs.FineZ = 115 << 3;
            lOOurs.Owner = 1;
            lOOurs.Hp = 40;
            lOOurs.StaticHeading = 1;
            lOLevel.InFlight.Add(lOOurs);

            int liTop = UWLevArkCodec.ReadMobileFreeList(lyBlock, 0)[UWLevArkCodec.ReadMobileFreeList(lyBlock, 0).Count - 1];
            byte[] lyOurs = UWLevelWriter.BuildBlock(lOLevel, lyBlock);
            int liOursHead = (int)(UWBinary.Read32(lyOurs, ((58 * UWWorldScale.TilesPerAxis) + 43) * UWLevArkLayout.TileRecordSize)
                >> UWLevArkLayout.TileFirstObjectShift);

            fExpectBool("ours: the top mobile slot, at the head of its tile, on the active list",
                liOursHead == liTop && UWLevArkCodec.ReadActiveMobiles(lyOurs, 0).Contains(liTop), true);

            byte[] lyWholeArk = (byte[])lyArk.Clone();

            Array.Copy(lyOurs, 0, lyWholeArk, liStart, lyOurs.Length);

            UWLevel lOReread = new UWLevel(1, lyWholeArk, (uint)liStart, lOOffsets[UWLevArkLayout.TextureMapBlock(0)],
                pOContext.Data.Textures);
            UWMobileRecord lOReadBack = null;

            foreach (UWMobileRecord lOAt in lOReread.InFlight)
            {
                if (lOAt.HeadingByte == 0x20)
                    lOReadBack = lOAt;
            }

            fExpectBool("ours: read back as a flight with its heading, speed, owner and static heading",
                lOReread.InFlight.Count == 2 && lOReadBack != null && lOReadBack.SpeedByte == 0x14 && lOReadBack.GravityBit
                    && lOReadBack.VzField == 17 && lOReadBack.Owner == 1 && lOReadBack.StaticHeading == 1
                    && lOReadBack.FineZ == 115 << 3 && lOReadBack.Octant == 7, true);
        }

        private static string fFirstDifference(byte[] pyA, byte[] pyB)
        {
            for (int liAt = 0; liAt < Math.Min(pyA.Length, pyB.Length); liAt++)
            {
                if (pyA[liAt] != pyB[liAt])
                    return string.Format("at 0x{0:X4}: {1:X2} against {2:X2}", liAt, pyA[liAt], pyB[liAt]);
            }

            return pyA.Length == pyB.Length ? "none" : "length";
        }
    }
}
