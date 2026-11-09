using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE EDITOR'S PACKAGES (.uwmod, the Underworld Editor's mod format 2; Docs/MOD-SUPPORT-PLAN.md, 2026-11-09): the
    /// delta (COPY, ADD, BLOCK over a second-game archive, and what it refuses), the start square moved by a
    /// delta on the player's own program, and the game files with packages applied to a made-up extraction - a
    /// good one, a conflicting one, one for another release, one with a forbidden path, one with a damaged delta.
    /// The deltas are made here with the simplest encoder (the common start and end copied, the middle added).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckModGameFiles(DataImport pOData)
        {
            Check.Group("Mods: the editor's packages (UWModDelta, UWModGameFiles)");

            // The delta itself.
            byte[] lyOriginal = Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog.");
            byte[] lyEdited = Encoding.ASCII.GetBytes("The quick red fox leaps over the lazy dog!");

            Check.Str("delta: copy, add, copy gives the edited file", Encoding.ASCII.GetString(UWModDelta.Apply(lyOriginal, fTestDelta(lyOriginal, lyEdited))),
                Encoding.ASCII.GetString(lyEdited));
            Check.Str("delta: refused for an original of another length", fDeltaError(new byte[10], fTestDelta(lyOriginal, lyEdited)),
                "made from a file of 44 bytes, not 10");

            byte[] lyCut = fTestDelta(lyOriginal, lyEdited);

            Array.Resize(ref lyCut, lyCut.Length - 3);
            Check.Str("delta: refused where it ends early", fDeltaError(lyOriginal, lyCut), "the delta ends early");

            List<byte> lOOutside = new List<byte>(Encoding.ASCII.GetBytes("UWD2"));

            fVarint(lOOutside, (ulong)lyOriginal.Length);
            fVarint(lOOutside, 4);
            lOOutside.Add(0x01);
            fVarint(lOOutside, fZigzag(lyOriginal.Length - 2));
            fVarint(lOOutside, 4);
            lOOutside.Add(0x00);
            Check.Str("delta: refused where it copies from outside the original", fDeltaError(lyOriginal, lOOutside.ToArray()), "copies from outside the original");

            // BLOCK: a second-game archive of a plain block and a packed one; the packed one changed.
            byte[] lyBlock0 = Encoding.ASCII.GetBytes("plain block, left as it is");
            byte[] lyBlock1 = new byte[600];

            for (int liAt = 0; liAt < lyBlock1.Length; liAt++)
                lyBlock1[liAt] = (byte)("ABCDEFGH"[(liAt / 7) % 8]);

            byte[] lyBlock1Edited = (byte[])lyBlock1.Clone();

            lyBlock1Edited[300] = (byte)'z';
            lyBlock1Edited[301] = (byte)'z';

            byte[] lyArchive = fTestArchive(lyBlock0, lyBlock1, out int liHeader);
            byte[] lyArchiveEdited = fTestArchive(lyBlock0, lyBlock1Edited, out _);
            List<byte> lOBlockDelta = new List<byte>(Encoding.ASCII.GetBytes("UWD2"));

            fVarint(lOBlockDelta, (ulong)lyArchive.Length);
            fVarint(lOBlockDelta, (ulong)lyArchiveEdited.Length);
            // The header changed (block 1's stored length), the plain block copied, block 1 by a nested delta.
            lOBlockDelta.Add(0x02);
            fVarint(lOBlockDelta, (ulong)liHeader);
            lOBlockDelta.AddRange(new ArraySegment<byte>(lyArchiveEdited, 0, liHeader));
            lOBlockDelta.Add(0x01);
            fVarint(lOBlockDelta, fZigzag(liHeader));
            fVarint(lOBlockDelta, (ulong)lyBlock0.Length);
            lOBlockDelta.Add(0x03);
            fVarint(lOBlockDelta, 1);
            lOBlockDelta.Add(0x01);
            lOBlockDelta.AddRange(fTestDelta(lyBlock1, lyBlock1Edited, true));
            lOBlockDelta.Add(0x00);

            byte[] lyBlockResult = null;
            string lsBlockError = null;

            try
            {
                lyBlockResult = UWModDelta.Apply(lyArchive, lOBlockDelta.ToArray());
            }
            catch (InvalidDataException lOError)
            {
                lsBlockError = lOError.Message;
            }

            Check.Str("delta: BLOCK unpacks a second-game block, changes it and packs it again",
                lsBlockError ?? (UWModGameFiles.Sha256(lyBlockResult) == UWModGameFiles.Sha256(lyArchiveEdited)).ToString(), "True");

            // The start square moved by a delta on the player's own program.
            string lsProgram = pOData != null ? Path.Combine(Path.GetDirectoryName(pOData.DataPath.TrimEnd('\\', '/')), "UW.EXE") : null;

            if (lsProgram != null && File.Exists(lsProgram))
            {
                byte[] lyProgram = File.ReadAllBytes(lsProgram);
                byte[] lyMoved = (byte[])lyProgram.Clone();
                int liFound = -1;

                for (int liAt = 0; liAt + 13 <= lyMoved.Length && liFound < 0; liAt++)
                {
                    if (lyMoved[liAt] == 0x6A && lyMoved[liAt + 1] == 0x01 && lyMoved[liAt + 2] == 0x6A && lyMoved[liAt + 4] == 0x6A
                        && lyMoved[liAt + 6] == 0x9A && lyMoved[liAt + 7] == 0xD1 && lyMoved[liAt + 8] == 0x06 && lyMoved[liAt + 9] == 0x48
                        && lyMoved[liAt + 10] == 0x00)
                        liFound = liAt;
                }

                if (liFound >= 0)
                {
                    lyMoved[liFound + 3] = 33;
                    lyMoved[liFound + 5] = 7;
                }

                UWStartSquare.TryRead(UWModDelta.Apply(lyProgram, fTestDelta(lyProgram, lyMoved)), out int liX, out int liY);
                Check.Str("start square: a delta on the player's UW.EXE moves it (as the editor's Mark as start)", liX + "," + liY, "7,33");
            }
            else
                Check.Fail("start square from a delta", "no UW.EXE beside the data");

            // A save's record of its mods against the ones in force (UWModSaveRecord, 2026-11-09).
            List<UWModSaveRecord.Package> lOSaved = UWModSaveRecord.Parse(new[]
            {
                UWModSaveRecord.Line("my.uw1.mod", "1.0.0", "My UW1 mod"), UWModSaveRecord.Line("a.kept", "2", "Kept"), "broken line"
            });
            List<UWModSaveRecord.Package> lONow = UWModSaveRecord.Parse(new[]
            {
                UWModSaveRecord.Line("a.kept", "2", "Kept"), UWModSaveRecord.Line("my.uw1.mod", "1.1.0", "My UW1 mod"),
                UWModSaveRecord.Line("Conversation test.mod", "1.0.0", "Conversation\ttest")
            });
            bool lbDiffers = UWModSaveRecord.Compare(lOSaved, lONow, out List<UWModSaveRecord.Package> lOMissing, out List<UWModSaveRecord.Package> lONew);

            Check.Str("save's mods: another version is missing and new, an id with spaces kept, a broken line left out",
                lbDiffers + " | " + string.Join(", ", lOMissing.ConvertAll(pOPackage => pOPackage.Shown)) + " | " + string.Join(", ", lONew.ConvertAll(pOPackage => pOPackage.Shown))
                + " | " + UWModSaveRecord.Compare(lOSaved, lOSaved, out _, out _),
                "True | My UW1 mod 1.0.0 | My UW1 mod 1.1.0, Conversation test 1.0.0 | False");

            // A piece a mod changed plays on the device, not from the recording (2026-11-09).
            List<string> lsMusic = new List<string> { "DATA/LEV.ARK", "SOUND/AW06.XMI", "sound/uw01.xmi" };

            Check.Str("music: a piece whose AW or UW file a mod changed, by its number",
                UWDataImport.Audio.UWMusicRecordings.IsChangedByMods(lsMusic, "01") + " " + UWDataImport.Audio.UWMusicRecordings.IsChangedByMods(lsMusic, "06")
                + " " + UWDataImport.Audio.UWMusicRecordings.IsChangedByMods(lsMusic, "02") + " " + UWDataImport.Audio.UWMusicRecordings.IsChangedByMods(null, "01"),
                "True True False False");

            // The game files: a made-up extraction and five packages.
            string lsRoot = Path.Combine(Path.GetTempPath(), "uwselfcheck-gamefiles-" + Guid.NewGuid().ToString("N"));

            try
            {
                string lsExtraction = Path.Combine(lsRoot, "GameData", "UW1");
                string lsMods = Path.Combine(lsRoot, "Mods");
                byte[] lyLevel = Encoding.ASCII.GetBytes("LEVEL ARCHIVE: tiles, objects and the rest of the original world");
                byte[] lyLevelEdited = Encoding.ASCII.GetBytes("LEVEL ARCHIVE: tiles, objects MOVED and the rest of the original world");
                byte[] lyObjects = Encoding.ASCII.GetBytes("OBJECT TABLE of the original");
                byte[] lySpeech = Encoding.ASCII.GetBytes("a speech of the mod's own");

                Directory.CreateDirectory(Path.Combine(lsExtraction, "DATA"));
                Directory.CreateDirectory(Path.Combine(lsExtraction, "SOUND"));
                Directory.CreateDirectory(lsMods);
                File.WriteAllBytes(Path.Combine(lsExtraction, "UW.EXE"), Encoding.ASCII.GetBytes("program"));
                File.WriteAllBytes(Path.Combine(lsExtraction, "DATA", "LEV.ARK"), lyLevel);
                File.WriteAllBytes(Path.Combine(lsExtraction, "DATA", "OBJECTS.DAT"), lyObjects);
                File.WriteAllText(Path.Combine(lsExtraction, UWModGameFiles.ExtractionMarker), "image 123");

                byte[] lyOther = Encoding.ASCII.GetBytes("OBJECT TABLE of another release");

                fTestPackage(Path.Combine(lsMods, "a.uwmod"), "a.good", ("DATA/LEV.ARK", lyLevel, lyLevelEdited, false), ("SOUND/05.VOC", null, lySpeech, false));
                fTestPackage(Path.Combine(lsMods, "b.uwmod"), "b.conflict", ("DATA/LEV.ARK", lyLevel, Encoding.ASCII.GetBytes("LEVEL ARCHIVE: something else"), false));
                fTestPackage(Path.Combine(lsMods, "c.uwmod"), "c.release", ("DATA/OBJECTS.DAT", lyOther, Encoding.ASCII.GetBytes("OBJECT TABLE changed"), false));
                fTestPackage(Path.Combine(lsMods, "d.uwmod"), "d.path", ("UW.CFG", lyObjects, lyObjects, false));
                fTestPackage(Path.Combine(lsMods, "e.uwmod"), "e.damaged", ("DATA/OBJECTS.DAT", lyObjects, Encoding.ASCII.GetBytes("OBJECT TABLE damaged"), true));

                List<UWModCatalog.Entry> lOPackages = new List<UWModCatalog.Entry>(UWModCatalog.Scan(lsMods).SetFor("uw1").Mods);
                string lsCopy = lsExtraction + "-mods";
                UWModGameFiles lOFiles = UWModGameFiles.Build(lsExtraction, lsCopy, lOPackages, "uw1");
                string lsRefused = string.Join(" | ", lOFiles.Refused);

                Check.Str("game files: the good package applied, its files listed, the copy read", string.Join(",", lOFiles.Applied) + " "
                    + string.Join(",", lOFiles.Changed) + " " + (lOFiles.Root == lsCopy) + " " + (lOFiles.Problem ?? "-"), "a.good 1.0.0 DATA/LEV.ARK,SOUND/05.VOC True -");
                Check.Bool("game files: refused - the conflict, another release, a forbidden path, a damaged delta (" + lOFiles.Refused.Count + ")",
                    lOFiles.Refused.Count == 4 && lsRefused.Contains("b.conflict: DATA/LEV.ARK was changed by a.good before")
                    && lsRefused.Contains("c.release: DATA/OBJECTS.DAT is not the file the mod was made for")
                    && lsRefused.Contains("d.path: UW.CFG: not a file a mod may change") && lsRefused.Contains("e.damaged: deltas/DATA/OBJECTS.DAT.uwd is damaged"), true);
                Check.Str("game files: the copy holds the result, the added file and the untouched rest; the extraction stays as it was",
                    (UWModGameFiles.Sha256(File.ReadAllBytes(Path.Combine(lsCopy, "DATA", "LEV.ARK"))) == UWModGameFiles.Sha256(lyLevelEdited)) + " "
                    + File.Exists(Path.Combine(lsCopy, "SOUND", "05.VOC")) + " " + File.Exists(Path.Combine(lsCopy, "UW.EXE")) + " "
                    + (UWModGameFiles.Sha256(File.ReadAllBytes(Path.Combine(lsExtraction, "DATA", "LEV.ARK"))) == UWModGameFiles.Sha256(lyLevel)) + " "
                    + File.Exists(Path.Combine(lsExtraction, "SOUND", "05.VOC")), "True True True True False");

                File.WriteAllText(Path.Combine(lsCopy, "kept.txt"), "made by the first build");
                UWModGameFiles.Build(lsExtraction, lsCopy, lOPackages, "uw1");
                Check.Bool("game files: the same packages keep the copy", File.Exists(Path.Combine(lsCopy, "kept.txt")), true);

                UWModGameFiles lONone = UWModGameFiles.Build(lsExtraction, lsCopy, lOPackages.FindAll(pOEntry => pOEntry.Id != "a.good" && pOEntry.Id != "b.conflict"), "uw1");

                Check.Str("game files: with none applied the extraction itself is read", (lONone.Root == lsExtraction) + " " + lONone.Refused.Count, "True 3");

                fTestPackage(Path.Combine(lsMods, "f.uwmod"), "f.adds", ("SOUND/05.VOC", null, lySpeech, false));
                Directory.Delete(lsCopy, true);

                UWModGameFiles lOTwice = UWModGameFiles.Build(lsExtraction, lsCopy, new List<UWModCatalog.Entry>(UWModCatalog.Scan(lsMods).SetFor("uw1").Mods), "uw1");

                Check.Bool("game files: a file two packages add - the second is refused",
                    string.Join(" | ", lOTwice.Refused).Contains("f.adds: SOUND/05.VOC was added by a.good before - " + UWModGameFiles.ConflictMark), true);

                // The Mods window's conflicts, from the order and the file lists alone (UWModCatalog.ConflictOf).
                UWModCatalog lOOrdered = UWModCatalog.Scan(lsMods);
                UWModCatalog.Conflict lOB = lOOrdered.ConflictOf("uw1", "b.conflict");
                UWModCatalog.Conflict lOF = lOOrdered.ConflictOf("uw1", "f.adds");

                Check.Str("conflicts at once: the later package gives way, with the earlier one and the shared files",
                    (lOB == null ? "none" : lOB.With.Id + " " + string.Join(",", lOB.Files)) + " | " + (lOF == null ? "none" : lOF.With.Id + " " + string.Join(",", lOF.Files))
                    + " | " + (lOOrdered.ConflictOf("uw1", "a.good") == null) + " " + (lOOrdered.ConflictOf("uw1", "c.release") == null),
                    "a.good DATA/LEV.ARK | a.good SOUND/05.VOC | True True");
            }
            finally
            {
                try
                {
                    Directory.Delete(lsRoot, true);
                }
                catch (IOException)
                {
                }
            }
        }

        /// <summary>A package of format 2 with the given changes (path, original or null for an added file, result,
        /// whether to damage the delta after hashing it).</summary>
        private static void fTestPackage(string psZip, string psId, params (string Path, byte[] Base, byte[] Target, bool Damaged)[] paFiles)
        {
            StringBuilder lOJson = new StringBuilder();

            lOJson.Append("{ \"format\": 2, \"game\": \"uw1\", \"id\": \"").Append(psId).Append("\", \"name\": \"").Append(psId)
                .Append("\", \"version\": \"1.0.0\", \"authors\": [\"Test\"], \"files\": [");

            using (ZipArchive lOZip = ZipFile.Open(psZip, ZipArchiveMode.Create))
            {
                for (int liAt = 0; liAt < paFiles.Length; liAt++)
                {
                    (string Path, byte[] Base, byte[] Target, bool Damaged) lFile = paFiles[liAt];
                    byte[] lyDelta = fTestDelta(lFile.Base ?? new byte[0], lFile.Target);
                    string lsDeltaPath = "deltas/" + lFile.Path + ".uwd";
                    string lsDeltaSha = UWModGameFiles.Sha256(lyDelta);

                    if (lFile.Damaged)
                        lyDelta[lyDelta.Length - 2] ^= 0xFF;

                    using (Stream lOEntry = lOZip.CreateEntry(lsDeltaPath).Open())
                        lOEntry.Write(lyDelta, 0, lyDelta.Length);

                    lOJson.Append(liAt > 0 ? ", " : string.Empty).Append("{ \"path\": \"").Append(lFile.Path).Append("\", \"base\": ")
                        .Append(lFile.Base == null ? "null" : "{ \"sha256\": \"" + UWModGameFiles.Sha256(lFile.Base) + "\", \"size\": " + lFile.Base.Length + " }")
                        .Append(", \"target\": { \"sha256\": \"").Append(UWModGameFiles.Sha256(lFile.Target)).Append("\", \"size\": ").Append(lFile.Target.Length)
                        .Append(" }, \"delta\": { \"path\": \"").Append(lsDeltaPath).Append("\", \"sha256\": \"").Append(lsDeltaSha).Append("\" } }");
                }

                lOJson.Append("] }");
                fZipText(lOZip, UWModManifest.FileName, lOJson.ToString());
            }
        }

        /// <summary>The simplest delta: the common start and end copied, the middle added (pbNested: no header, for a BLOCK).</summary>
        private static byte[] fTestDelta(byte[] pyOriginal, byte[] pyEdited, bool pbNested = false)
        {
            int liStart = 0;

            while (liStart < pyOriginal.Length && liStart < pyEdited.Length && pyOriginal[liStart] == pyEdited[liStart])
                liStart++;

            int liEnd = 0;

            while (liEnd < pyOriginal.Length - liStart && liEnd < pyEdited.Length - liStart
                && pyOriginal[pyOriginal.Length - 1 - liEnd] == pyEdited[pyEdited.Length - 1 - liEnd])
                liEnd++;

            List<byte> lODelta = new List<byte>();

            if (!pbNested)
            {
                lODelta.AddRange(Encoding.ASCII.GetBytes("UWD2"));
                fVarint(lODelta, (ulong)pyOriginal.Length);
                fVarint(lODelta, (ulong)pyEdited.Length);
            }

            if (liStart > 0)
            {
                lODelta.Add(0x01);
                fVarint(lODelta, 0);
                fVarint(lODelta, (ulong)liStart);
            }

            int liMiddle = pyEdited.Length - liStart - liEnd;

            if (liMiddle > 0)
            {
                lODelta.Add(0x02);
                fVarint(lODelta, (ulong)liMiddle);
                lODelta.AddRange(new ArraySegment<byte>(pyEdited, liStart, liMiddle));
            }

            if (liEnd > 0)
            {
                lODelta.Add(0x01);
                fVarint(lODelta, fZigzag(pyOriginal.Length - liEnd - liStart));
                fVarint(lODelta, (ulong)liEnd);
            }

            lODelta.Add(0x00);

            return lODelta.ToArray();
        }

        /// <summary>A second-game archive of two blocks: block 0 plain, block 1 packed (length prefix and LZSS).</summary>
        private static byte[] fTestArchive(byte[] pyBlock0, byte[] pyBlock1, out int piHeader)
        {
            byte[] lyStream = UWLzss.Encode(pyBlock1);
            byte[] lyStored1 = new byte[lyStream.Length + 4];

            BitConverter.GetBytes(pyBlock1.Length).CopyTo(lyStored1, 0);
            lyStream.CopyTo(lyStored1, 4);
            piHeader = 6 + (16 * 2);

            byte[] lyArchive = new byte[piHeader + pyBlock0.Length + lyStored1.Length];

            lyArchive[0] = 2;
            BitConverter.GetBytes(piHeader).CopyTo(lyArchive, 6);
            BitConverter.GetBytes(piHeader + pyBlock0.Length).CopyTo(lyArchive, 10);
            BitConverter.GetBytes(0).CopyTo(lyArchive, 14);
            BitConverter.GetBytes(UWArk.CompressFlag | UWArk.CompressedFlag).CopyTo(lyArchive, 18);
            BitConverter.GetBytes(pyBlock0.Length).CopyTo(lyArchive, 22);
            BitConverter.GetBytes(lyStored1.Length).CopyTo(lyArchive, 26);
            BitConverter.GetBytes(pyBlock0.Length).CopyTo(lyArchive, 30);
            BitConverter.GetBytes(lyStored1.Length).CopyTo(lyArchive, 34);
            pyBlock0.CopyTo(lyArchive, piHeader);
            lyStored1.CopyTo(lyArchive, piHeader + pyBlock0.Length);

            return lyArchive;
        }

        private static string fDeltaError(byte[] pyOriginal, byte[] pyDelta)
        {
            try
            {
                UWModDelta.Apply(pyOriginal, pyDelta);

                return "applied";
            }
            catch (InvalidDataException lOError)
            {
                return lOError.Message;
            }
        }

        private static ulong fZigzag(long piValue)
        {
            return ((ulong)piValue << 1) ^ (ulong)(piValue >> 63);
        }

        private static void fVarint(List<byte> pOOut, ulong piValue)
        {
            while (piValue >= 0x80)
            {
                pOOut.Add((byte)(piValue | 0x80));
                piValue >>= 7;
            }

            pOOut.Add((byte)piValue);
        }
    }
}
