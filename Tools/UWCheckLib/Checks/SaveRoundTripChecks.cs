using System;
using System.Collections.Generic;
using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE SAVE ROUND TRIP: the level writer, given a level as loaded and the block it came
        /// from, must produce that block again byte for byte; the whole LEV.ARK written into a
        /// copy must equal the file read; PLAYER.DAT survives decrypt and encrypt; BGLOBALS.DAT
        /// survives load and serialise. Over the nine DATA levels always, and over every save
        /// folder under --saves when given. No such check existed before 2026-10-11, and every
        /// step of the cleanup that touches the data layer leans on it.
        ///
        /// Not covered yet: PLAYER.DAT through UWPlayerData.WritePosition with the same state,
        /// because the SavedState is only built on the Unity side today (phase 2 of the plan).
        /// </summary>
        private static void fCheckSaveRoundTrip(CheckContext pOContext)
        {
            Check.Group("Save round trip (UWLevelWriter, UWPlayerData, UWConversationGlobals)");

            fCheckWorldRoundTrip(pOContext.Data, pOContext.DataPath, "DATA");

            byte[] lyTemplate = File.ReadAllBytes(Path.Combine(pOContext.DataPath, "PLAYER.DAT"));

            Check.Bool("round trip: DATA\\PLAYER.DAT is the plain template", UWPlayerData.IsPlainTemplate(lyTemplate), true);
            Check.Bool("round trip: the template decrypts after encrypting",
                fSameBytes(UWPlayerData.Decrypt(UWPlayerData.Encrypt(lyTemplate, 0x2A)), lyTemplate, out _), true);
            Check.Bool("round trip: the template parses", UWPlayerData.FromPlain(lyTemplate) != null, true);

            if (string.IsNullOrEmpty(pOContext.SavesPath))
            {
                Check.Pass("round trip: saves", "none given (--saves <folder> checks every SAVE* in it)");

                return;
            }

            int liSaves = 0;

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
                    Check.Fail("round trip: " + lsName + " loads", lOException.Message);

                    continue;
                }

                liSaves++;
                fCheckWorldRoundTrip(lOData, lsSave, lsName);

                byte[] lyRaw = File.ReadAllBytes(Path.Combine(lsSave, "PLAYER.DAT"));
                byte[] lyAgain = UWPlayerData.Encrypt(UWPlayerData.Decrypt(lyRaw), lyRaw[0]);

                Check.Bool("round trip: " + lsName + " PLAYER.DAT decrypt then encrypt", fSameBytes(lyAgain, lyRaw, out _), true);

                string lsGlobals = Path.Combine(lsSave, "BGLOBALS.DAT");

                if (File.Exists(lsGlobals) && lOData.ConversationGlobals != null)
                {
                    byte[] lyFile = File.ReadAllBytes(lsGlobals);
                    int liFirst;
                    bool lbSame = fSameBytes(lOData.ConversationGlobals.Serialize(), lyFile, out liFirst);

                    if (lbSame)
                        Check.Pass("round trip: " + lsName + " BGLOBALS.DAT serialises identically", lyFile.Length + " bytes");
                    else
                        Check.Fail("round trip: " + lsName + " BGLOBALS.DAT serialises identically", "differs at " + liFirst);
                }
            }

            Check.Bool("round trip: at least one save folder found under " + pOContext.SavesPath, liSaves > 0, true);
        }

        /// <summary>The nine level blocks through BuildBlock, then the whole file through
        /// SaveMapData into a scratch copy of the folder.</summary>
        private static void fCheckWorldRoundTrip(DataImport pOData, string psFolder, string psLabel)
        {
            byte[] lyArk = File.ReadAllBytes(Path.Combine(psFolder, "LEV.ARK"));
            int liCount = lyArk[0] | (lyArk[1] << 8);

            for (int liLevel = 0; liLevel < 9 && liLevel < liCount; liLevel++)
            {
                uint luOffset = BitConverter.ToUInt32(lyArk, 2 + (liLevel * 4));
                string lsName = string.Format("round trip: {0} level {1} block through BuildBlock", psLabel, liLevel + 1);

                if (luOffset == 0 || luOffset + UWLevArkFile.Uw1.BlockSize > lyArk.Length || pOData.Levels == null || pOData.Levels.Count <= liLevel)
                {
                    Check.Fail(lsName, "no block or no level");

                    continue;
                }

                byte[] lyOriginal = new byte[UWLevArkFile.Uw1.BlockSize];

                Array.Copy(lyArk, (int)luOffset, lyOriginal, 0, lyOriginal.Length);

                UWLevelWriter.Warnings.Clear();

                byte[] lyBuilt = UWLevelWriter.BuildBlock(pOData.Levels[liLevel], lyOriginal);
                byte[] lyExpected = (byte[])lyOriginal.Clone();
                int liRepaired = fRepairActiveList(lyExpected, 0);
                int liFirst = -1;

                if (lyBuilt != null && fSameBytes(lyBuilt, lyExpected, out liFirst))
                    Check.Pass(lsName, "identical" + (liRepaired > 0 ? ", active list repaired by " + liRepaired : "")
                        + (UWLevelWriter.Warnings.Count > 0 ? ", " + UWLevelWriter.Warnings.Count + " warning(s)" : ""));
                else
                    Check.Fail(lsName, lyBuilt == null ? "no block built" : fDescribeDifference(lyBuilt, lyExpected, liFirst));
            }

            // The whole file: SaveMapData writes the loaded world over a copy of the folder.
            string lsScratch = Path.Combine(Path.GetTempPath(), "uwselfcheck-roundtrip", psLabel);

            try
            {
                if (Directory.Exists(lsScratch))
                    Directory.Delete(lsScratch, true);

                Directory.CreateDirectory(lsScratch);
                File.Copy(Path.Combine(psFolder, "LEV.ARK"), Path.Combine(lsScratch, "LEV.ARK"));

                string lsName = "round trip: " + psLabel + " LEV.ARK through SaveMapData";

                if (!pOData.SaveMapData(lsScratch))
                {
                    Check.Fail(lsName, "SaveMapData refused");
                }
                else
                {
                    byte[] lyWritten = File.ReadAllBytes(Path.Combine(lsScratch, "LEV.ARK"));
                    byte[] lyExpected = (byte[])lyArk.Clone();
                    int liFirst;

                    for (int liLevel = 0; liLevel < 9 && liLevel < liCount; liLevel++)
                    {
                        uint luOffset = BitConverter.ToUInt32(lyExpected, 2 + (liLevel * 4));

                        if (luOffset != 0 && luOffset + UWLevArkFile.Uw1.BlockSize <= lyExpected.Length)
                            fRepairActiveList(lyExpected, (int)luOffset);
                    }

                    if (fSameBytes(lyWritten, lyExpected, out liFirst))
                        Check.Pass(lsName, lyArk.Length + " bytes identical");
                    else
                        Check.Fail(lsName, fDescribeDifference(lyWritten, lyExpected, liFirst));
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(lsScratch))
                        Directory.Delete(lsScratch, true);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// What the writer must make of a level block's list of active critters, worked out
        /// from the bytes alone: in the original's data the list holds exactly the creatures
        /// standing in a tile (UWLevelWriter.fWriteActiveMobiles). Entries that hold none leave
        /// it (the last moving in), standing creatures it lacks are appended in tile order - our
        /// saves before 2026-10-12 left off creatures made during play. A non-creature in a mobile
        /// slot in a tile's list that is on the list is a thing in flight and stays where it is
        /// (UWMobileRecordCodec, 2026-10-19). Returns how many entries changed.
        /// </summary>
        private static int fRepairActiveList(byte[] pyData, int piBlock)
        {
            List<int> lOStanding = new List<int>();
            List<int> lOInTiles = new List<int>();

            for (int liTile = 0; liTile < UWLevArkLayout.TileCount; liTile++)
            {
                int liIndex = (int)(BitConverter.ToUInt32(pyData, piBlock + (liTile * UWLevArkLayout.TileRecordSize))
                    >> UWLevArkLayout.TileFirstObjectShift);
                int liGuard = 0;

                while (liIndex != 0 && liIndex < UWLevArkLayout.ObjectCount && liGuard++ < UWLevArkLayout.ObjectCount)
                {
                    int liAt = piBlock + UWLevArkLayout.MobileBase + (liIndex < UWLevArkLayout.MobileCount
                        ? liIndex * UWLevArkLayout.MobileRecordSize
                        : (UWLevArkLayout.MobileCount * UWLevArkLayout.MobileRecordSize)
                            + ((liIndex - UWLevArkLayout.MobileCount) * UWLevArkLayout.StaticRecordSize));
                    int liId = BitConverter.ToUInt16(pyData, liAt) & 0x1FF;

                    if (UWLevArkLayout.IsMobileSlot(liIndex) && UWObjectIds.IsCreature(liId) && !lOStanding.Contains(liIndex))
                        lOStanding.Add(liIndex);

                    if (UWLevArkLayout.IsMobileSlot(liIndex) && liId != 0 && !UWObjectIds.IsCreature(liId))
                        lOInTiles.Add(liIndex);

                    liIndex = BitConverter.ToUInt16(pyData, liAt + 4) >> 6;
                }
            }

            List<int> lOList = UWLevArkCodec.ReadActiveMobiles(pyData, piBlock);
            int liChanged = 0;

            foreach (int liListed in new List<int>(lOList))
            {
                if (!lOStanding.Contains(liListed) && !lOInTiles.Contains(liListed))
                {
                    UWLevArkCodec.RemoveActiveMobile(lOList, liListed);
                    liChanged++;
                }
            }

            foreach (int liIndex in lOStanding)
            {
                if (!lOList.Contains(liIndex))
                {
                    lOList.Add(liIndex);
                    liChanged++;
                }
            }

            if (liChanged > 0)
                UWLevArkCodec.WriteActiveMobiles(pyData, piBlock, lOList);

            return liChanged;
        }

        private static bool fSameBytes(byte[] pyLeft, byte[] pyRight, out int piFirstDifference)
        {
            piFirstDifference = -1;

            if (pyLeft == null || pyRight == null)
                return pyLeft == pyRight;

            int liCount = Math.Min(pyLeft.Length, pyRight.Length);

            for (int liAt = 0; liAt < liCount; liAt++)
            {
                if (pyLeft[liAt] != pyRight[liAt])
                {
                    piFirstDifference = liAt;

                    return false;
                }
            }

            if (pyLeft.Length != pyRight.Length)
            {
                piFirstDifference = liCount;

                return false;
            }

            return true;
        }

        private static string fDescribeDifference(byte[] pyBuilt, byte[] pyOriginal, int piFirst)
        {
            if (pyBuilt.Length != pyOriginal.Length)
                return string.Format("length {0}, expected {1}", pyBuilt.Length, pyOriginal.Length);

            int liDiffering = 0;

            for (int liAt = 0; liAt < pyBuilt.Length; liAt++)
                if (pyBuilt[liAt] != pyOriginal[liAt])
                    liDiffering++;

            return string.Format("{0} byte(s) differ, the first at 0x{1:X} (got {2:X2}, expected {3:X2})",
                liDiffering, piFirst, pyBuilt[piFirst], pyOriginal[piFirst]);
        }
    }
}
