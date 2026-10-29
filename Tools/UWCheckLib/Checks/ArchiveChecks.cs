using System.Collections.Generic;
using System.IO;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE ARCHIVES OF BOTH GAMES (Docs/UW2-PLAN.md phase 1 step 1, 2026-10-28; decision 4): UWArk
    /// reads and writes each game's layout, and UWLzss encodes as the original did.
    ///
    /// - The compression on its own: encoded and decoded again gives the data back, for an empty
    ///   input, a byte, runs and a pseudo-random block.
    /// - A read and a write give every shipped archive back byte for byte: the first game's LEV.ARK
    ///   and CNV.ARK, the save games' LEV.ARK, the second game's LEV.ARK, CNV.ARK, BYT.ARK, SCD.ARK.
    /// - The encoder gives every shipped compressed stream of the second game back byte for byte.
    /// - Every block of the second game's archives written again through the writer (compression,
    ///   flags, room) gives the shipped file back: the original's rules reproduced, not only its bytes.
    /// - A grown block with the room flag gets its length plus 15%, a shrunk one keeps its room, a
    ///   block that compresses to no gain is stored plain.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckArchives(CheckContext pOContext)
        {
            Check.Group("The archives of both games (UW2 plan, phase 1 step 1)");

            fCheckLzssRoundTrips();
            fCheckArchiveRules();

            if (pOContext.DataPath != null)
            {
                foreach (string lsName in new[] { "LEV.ARK", "CNV.ARK" })
                    fCheckArchiveRoundTrip("UW1 " + lsName, Path.Combine(pOContext.DataPath, lsName), UWArk.LayoutEnum.Uw1, false);
            }

            if (pOContext.SavesPath != null && Directory.Exists(pOContext.SavesPath))
            {
                foreach (string lsSave in Directory.GetDirectories(pOContext.SavesPath, "SAVE?"))
                {
                    string lsFile = Path.Combine(lsSave, "LEV.ARK");

                    if (File.Exists(lsFile))
                        fCheckArchiveRoundTrip("UW1 " + Path.GetFileName(lsSave) + " LEV.ARK", lsFile, UWArk.LayoutEnum.Uw1, false);
                }
            }

            if (pOContext.Uw2DataPath != null)
            {
                foreach (string lsName in new[] { "LEV.ARK", "CNV.ARK", "BYT.ARK", "SCD.ARK" })
                    fCheckArchiveRoundTrip("UW2 " + lsName, Path.Combine(pOContext.Uw2DataPath, lsName), UWArk.LayoutEnum.Uw2, true);
            }

            // The second game's saves written by DOS's put_arc itself (--uw2-saves; phase 1 step 4): its room
            // and its order, reproduced by the writer.
            if (pOContext.Uw2SavesPath != null && Directory.Exists(pOContext.Uw2SavesPath))
            {
                foreach (string lsSave in Directory.GetDirectories(pOContext.Uw2SavesPath, "SAVE?"))
                {
                    foreach (string lsName in new[] { "LEV.ARK", "SCD.ARK" })
                        fCheckArchiveRoundTrip("UW2 " + Path.GetFileName(lsSave) + " " + lsName, Path.Combine(lsSave, lsName), UWArk.LayoutEnum.Uw2, true);
                }
            }
        }

        /// <summary>Encoded and decoded again, the data comes back.</summary>
        private static void fCheckLzssRoundTrips()
        {
            System.Random lORandom = new System.Random(1);
            byte[] lyNoise = new byte[20000];

            // Bytes from a small alphabet, so matches of every length occur.
            for (int liAt = 0; liAt < lyNoise.Length; liAt++)
                lyNoise[liAt] = (byte)(lORandom.Next(6) * 37);

            byte[] lyRun = new byte[5000];

            for (int liAt = 0; liAt < lyRun.Length; liAt++)
                lyRun[liAt] = (byte)(liAt % 7 == 0 ? 0x20 : 0x41);

            string lsBad = string.Empty;

            foreach (byte[] lyData in new[] { new byte[0], new byte[] { 0x55 }, new byte[18], lyRun, lyNoise })
            {
                byte[] lyStream = UWLzss.Encode(lyData);
                byte[] lyBack = UWLzss.Decode(lyStream, 0, lyStream.Length, lyData.Length);

                if (!fSameBytes(lyBack, lyData))
                    lsBad += lyData.Length + " ";
            }

            Check.Str("lzss: encoded and decoded again (0, 1, 18 zeros, 5000 of runs, 20000 of noise)", lsBad, string.Empty);
            Check.Bool("lzss: a run compresses, and the stream starts with the ring's spaces in mind (18 spaces: one reference)",
                UWLzss.Encode(lyRun).Length < lyRun.Length / 4 && UWLzss.Encode(System.Text.Encoding.ASCII.GetBytes(new string(' ', 18))).Length == 3, true);
        }

        /// <summary>The writer's rules (put_arc): room kept, room grown, plain where compression gains nothing.</summary>
        private static void fCheckArchiveRules()
        {
            UWArk lOArk = UWArk.Create(UWArk.LayoutEnum.Uw2, 3);
            byte[] lyRun = new byte[1000];

            for (int liAt = 0; liAt < lyRun.Length; liAt++)
                lyRun[liAt] = (byte)(liAt & 3);

            // A new block with room: compressed, its stored length plus 15%.
            lOArk.WriteBlock(0, lyRun, UWArk.CompressFlag | UWArk.RoomFlag);

            int liStored = lOArk.StoredLength(0);

            Check.Str("ark: a new block with room is compressed and gets 15% beyond its length",
                lOArk.IsCompressed(0) + " " + (lOArk.RoomOf(0) == liStored + (15 * liStored / 100)), "True True");

            // Smaller again: it keeps its room.
            int liRoom = lOArk.RoomOf(0);

            lOArk.WriteBlock(0, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            Check.Str("ark: a smaller block keeps its room and is stored plain (no gain from compressing 8 bytes)",
                (lOArk.RoomOf(0) == liRoom) + " " + lOArk.IsCompressed(0) + " " + lOArk.StoredLength(0), "True False 8");

            // Without the room flag a block of another length gets exactly its own.
            lOArk.WriteBlock(1, new byte[] { 9, 9 }, UWArk.CompressFlag);
            lOArk.WriteBlock(1, new byte[] { 1, 2, 3 });
            Check.Str("ark: without room a block of another length gets exactly its length", lOArk.RoomOf(1) + " " + lOArk.StoredLength(1), "3 3");

            // Read back from the bytes written: the same blocks, block 2 absent.
            UWArk lOBack = UWArk.Read(lOArk.ToBytes(), UWArk.LayoutEnum.Uw2);

            Check.Bool("ark: written and read back, the blocks are the same and the absent one absent",
                fSameBytes(lOBack.ReadBlock(0), new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }) && fSameBytes(lOBack.ReadBlock(1), new byte[] { 1, 2, 3 })
                && !lOBack.IsPresent(2) && lOBack.RoomOf(0) == liRoom, true);

            // Outgrowing its room, block 0 moves to the file's end, as put_arc appends it.
            byte[] lyBefore = lOArk.ToBytes();

            // Random bytes do not compress: stored plain, longer than the room.
            byte[] lyBig = new byte[liRoom + 50];

            new System.Random(2).NextBytes(lyBig);
            lOArk.WriteBlock(0, lyBig);

            byte[] lyAfter = lOArk.ToBytes();

            Check.Str("ark: a block that outgrows its room goes to the file's end (block 0 before block 1, then after)",
                (System.BitConverter.ToUInt32(lyBefore, 6) < System.BitConverter.ToUInt32(lyBefore, 10)) + " "
                + (System.BitConverter.ToUInt32(lyAfter, 6) > System.BitConverter.ToUInt32(lyAfter, 10)), "True True");

            UWArk lOFirst = UWArk.Create(UWArk.LayoutEnum.Uw1, 2);

            lOFirst.WriteBlock(1, new byte[] { 7, 8, 9 });

            byte[] lyFirst = lOFirst.ToBytes();

            Check.Str("ark: the first game's layout (count, offsets, then the blocks plain)", System.BitConverter.ToString(lyFirst), "02-00-00-00-00-00-0A-00-00-00-07-08-09");
        }

        /// <summary>A read and a write give the file back; for the second game also the encoder and the writer.</summary>
        private static void fCheckArchiveRoundTrip(string psLabel, string psPath, UWArk.LayoutEnum peLayout, bool pbRewrite)
        {
            if (!File.Exists(psPath))
                return;

            byte[] lyFile = File.ReadAllBytes(psPath);
            UWArk lOArk = UWArk.Read(lyFile, peLayout);

            Check.Bool("ark: " + psLabel + " read and written gives the file back (" + lOArk.BlockCount + " blocks)", fSameBytes(lOArk.ToBytes(), lyFile), true);

            if (!pbRewrite)
                return;

            int liStreams = 0;
            List<int> lOOther = new List<int>();

            for (int liBlock = 0; liBlock < lOArk.BlockCount; liBlock++)
            {
                byte[] lyStream = lOArk.StoredStream(liBlock);

                if (lyStream == null)
                    continue;

                liStreams++;

                if (!fSameBytes(UWLzss.Encode(lOArk.ReadBlock(liBlock)), lyStream))
                    lOOther.Add(liBlock);
            }

            Check.Str("ark: " + psLabel + "'s " + liStreams + " compressed streams encoded again as shipped", string.Join(" ", lOOther), string.Empty);

            UWArk lORewritten = UWArk.Read(lyFile, peLayout);

            for (int liBlock = 0; liBlock < lORewritten.BlockCount; liBlock++)
            {
                if (lORewritten.IsPresent(liBlock))
                    lORewritten.WriteBlock(liBlock, lORewritten.ReadBlock(liBlock));
            }

            Check.Bool("ark: " + psLabel + " with every block written again (compression, flags, room) gives the file back",
                fSameBytes(lORewritten.ToBytes(), lyFile), true);
        }
    }
}
