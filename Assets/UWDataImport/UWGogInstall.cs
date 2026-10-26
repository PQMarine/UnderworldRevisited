using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UWDataImport.Install
{
    /// <summary>
    /// The game data of the supported version: Ultima Underworld 1 from GOG.com.
    ///
    /// In the GOG installation the data is not on disk: `UNDEROM1\DATA` holds only UW.CFG.
    /// Everything lives in `game.gog`, an ISO 9660 CD image (2048-byte sectors) that the
    /// bundled DOSBox mounts as drive D:. UW1 is in its folder `UW` (CRIT, CUTS, DATA, SOUND,
    /// UW.EXE), UW2 in `UW2`. The save games are on disk in `UNDEROM1\SAVE1` to `SAVE4`.
    ///
    /// This class finds the image, extracts the UW1 folders once into a cache folder with the
    /// same layout as an installed game (CRIT, CUTS, DATA, SOUND and UW.EXE side by side), and
    /// checks that UW.EXE is the supported build - the model import reads it at fixed offsets.
    /// No Unity references, see UWDataImport.asmdef.
    /// </summary>
    public static class UWGogInstall
    {
        /// <summary>File name of the CD image in a GOG installation.</summary>
        public const string ImageFileName = "game.gog";

        /// <summary>Size of the supported UW.EXE (GOG build).</summary>
        public const long SupportedExeSize = 547248;

        /// <summary>SHA-1 of the supported UW.EXE (GOG build).</summary>
        public const string SupportedExeSha1 = "226fed4dba9718a7c972de5451b520fcd16546c0";

        private const string CacheFolderName = "UW1";
        private const string MarkerFileName = "extracted.txt";
        private const int MarkerVersion = 1;

        private static readonly string[] msFoldersToExtract = { "CRIT", "CUTS", "DATA", "SOUND" };

        /// <summary>
        /// Finds game.gog for a path that is either the image itself or the GOG installation
        /// folder (or its UNDEROM1 subfolder). Null if there is none.
        /// </summary>
        public static string FindImage(string psPath)
        {
            if (string.IsNullOrEmpty(psPath))
                return null;

            try
            {
                string lsTrimmed = psPath.Trim().TrimEnd('\\', '/');

                if (File.Exists(lsTrimmed))
                    return lsTrimmed;

                if (!Directory.Exists(lsTrimmed))
                    return null;

                string lsInFolder = Path.Combine(lsTrimmed, ImageFileName);

                if (File.Exists(lsInFolder))
                    return lsInFolder;

                DirectoryInfo lOParent = Directory.GetParent(lsTrimmed);
                string lsAbove = lOParent == null ? null : Path.Combine(lOParent.FullName, ImageFileName);

                return lsAbove != null && File.Exists(lsAbove) ? lsAbove : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// The optional environment variable UW_DATA (cleanup 9.2, 2026-10-25): a DATA folder, a
        /// GOG installation folder or game.gog itself, for machines where the game is somewhere
        /// else than the common folders. Null when it is not set.
        /// </summary>
        public static string GetEnvironmentPath()
        {
            string lsValue;

            try
            {
                lsValue = Environment.GetEnvironmentVariable("UW_DATA");
            }
            catch (Exception)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(lsValue) ? null : lsValue.Trim().Trim('"');
        }

        /// <summary>
        /// Where the game is looked for when no path is given - the ONE list of the game and the
        /// tools (cleanup 9.2, 2026-10-25; it lived in UWSettings before): UW_DATA first when it is
        /// set, then the common GOG installation folders (the folder holding game.gog).
        ///
        /// ON LINUX (since the Linux build, 2026-10-05) the usual homes of a GOG game: the GOG
        /// installer's and Heroic's "GOG Games" folders, Lutris' "Games", and the Wine, Steam
        /// Proton and Lutris prefixes that hold the Windows installer's folder - game.gog is the
        /// CD image every GOG release of the game carries, however it was installed (innoextract
        /// included).
        /// </summary>
        public static string[] GetCandidateFolders(bool pbLinux)
        {
            List<string> lOFolders = new List<string>();
            string lsEnvironment = GetEnvironmentPath();

            if (lsEnvironment != null)
                lOFolders.Add(lsEnvironment);

            if (pbLinux)
            {
                string lsHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                if (string.IsNullOrEmpty(lsHome))
                    lsHome = "/root";

                lOFolders.Add(Path.Combine(lsHome, "GOG Games/Ultima Underworld"));
                lOFolders.Add(Path.Combine(lsHome, "GOG Games/Ultima Underworld 1+2"));
                lOFolders.Add(Path.Combine(lsHome, "Games/Ultima Underworld"));
                lOFolders.Add(Path.Combine(lsHome, "Games/Heroic/Ultima Underworld 1+2"));
                lOFolders.Add(Path.Combine(lsHome, "Games/gog/ultima-underworld-1-2"));
                lOFolders.Add(Path.Combine(lsHome, ".wine/drive_c/GOG Games/Ultima Underworld"));
                lOFolders.Add(Path.Combine(lsHome, ".wine/drive_c/Program Files (x86)/GOG Galaxy/Games/Ultima Underworld"));
                lOFolders.Add(Path.Combine(lsHome, ".steam/steam/steamapps/compatdata/pfx/drive_c/GOG Games/Ultima Underworld"));
                lOFolders.Add(Path.Combine(lsHome, ".local/share/Steam/steamapps/compatdata/pfx/drive_c/GOG Games/Ultima Underworld"));
                lOFolders.Add("/opt/GOG Games/Ultima Underworld");
                lOFolders.Add("/usr/local/games/Ultima Underworld");

                return lOFolders.ToArray();
            }

            lOFolders.Add(@"C:\GOG Games\Ultima Underworld");
            lOFolders.Add(@"D:\GOG Games\Ultima Underworld");
            lOFolders.Add(@"C:\Program Files (x86)\GOG Galaxy\Games\Ultima Underworld");
            lOFolders.Add(@"C:\Program Files (x86)\GOG.com\Ultima Underworld");
            lOFolders.Add(@"C:\Ultima Underworld");
            lOFolders.Add(@"D:\Ultima Underworld");

            return lOFolders.ToArray();
        }

        /// <summary>The folder holding SAVE1 to SAVE4 next to an image: the installation's
        /// UNDEROM1 folder. Null if it does not exist.</summary>
        public static string GetSavegameFolder(string psImagePath)
        {
            try
            {
                string lsInstall = Path.GetDirectoryName(psImagePath);
                string lsUnderom = lsInstall == null ? null : Path.Combine(lsInstall, "UNDEROM1");

                return lsUnderom != null && Directory.Exists(lsUnderom) ? lsUnderom : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Makes sure the UW1 data of the image is extracted below psCacheRoot and returns the
        /// path of its DATA folder, or null with psError set. An existing extraction of the
        /// same image (size and time stamp) is reused.
        /// </summary>
        public static string EnsureExtracted(string psImagePath, string psCacheRoot, out string psError)
        {
            psError = null;

            FileStream lOLock = null;

            try
            {
                FileInfo lOImage = new FileInfo(psImagePath);

                if (!lOImage.Exists)
                {
                    psError = "Image not found: " + psImagePath;
                    return null;
                }

                string lsTarget = Path.Combine(psCacheRoot, CacheFolderName);
                string lsMarker = Path.Combine(lsTarget, MarkerFileName);
                string lsExpected = fMarkerText(lOImage);

                if (fIsExtracted(lsTarget, lsMarker, lsExpected))
                    return Path.Combine(lsTarget, "DATA");

                // Two cold starts at once (two tools, or a tool run twice in parallel) would both
                // delete and refill the same .partial folder. The lock file beside it makes the
                // second one wait for the first and then take its result (cleanup 9.2,
                // 2026-10-25).
                Directory.CreateDirectory(psCacheRoot);
                lOLock = fTakeLock(lsTarget + ".lock", out psError);

                if (lOLock == null)
                    return null;

                // The one we waited for has finished it.
                if (fIsExtracted(lsTarget, lsMarker, lsExpected))
                    return Path.Combine(lsTarget, "DATA");

                string lsTemporary = lsTarget + ".partial";

                if (Directory.Exists(lsTemporary))
                    Directory.Delete(lsTemporary, true);

                Directory.CreateDirectory(lsTemporary);

                using (FileStream lOStream = new FileStream(psImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    IsoReader lOReader = new IsoReader(lOStream);
                    IsoEntry lOUw = lOReader.Find(lOReader.Root, "UW");

                    if (lOUw == null || !lOUw.IsDirectory)
                    {
                        psError = "The image has no UW folder - is this the GOG version of Ultima Underworld?";
                        return null;
                    }

                    IsoEntry lOExe = lOReader.Find(lOUw, "UW.EXE");

                    if (lOExe == null || lOExe.IsDirectory)
                    {
                        psError = "UW.EXE is missing in the image.";
                        return null;
                    }

                    byte[] lyExe = lOReader.Read(lOExe);

                    if (!fIsSupportedExe(lyExe))
                    {
                        psError = "Unsupported UW.EXE (" + lyExe.Length + " bytes). Only the GOG version of Ultima Underworld is supported.";
                        return null;
                    }

                    File.WriteAllBytes(Path.Combine(lsTemporary, "UW.EXE"), lyExe);

                    foreach (string lsFolder in msFoldersToExtract)
                    {
                        IsoEntry lODirectory = lOReader.Find(lOUw, lsFolder);

                        if (lODirectory == null || !lODirectory.IsDirectory)
                        {
                            psError = "Folder UW\\" + lsFolder + " is missing in the image.";
                            return null;
                        }

                        fExtractDirectory(lOReader, lODirectory, Path.Combine(lsTemporary, lsFolder));
                    }
                }

                if (Directory.Exists(lsTarget))
                    Directory.Delete(lsTarget, true);

                Directory.Move(lsTemporary, lsTarget);
                File.WriteAllText(lsMarker, lsExpected);

                return Path.Combine(lsTarget, "DATA");
            }
            catch (Exception lOException)
            {
                psError = "Extracting " + psImagePath + " failed: " + lOException.Message;
                return null;
            }
            finally
            {
                if (lOLock != null)
                    lOLock.Dispose();
            }
        }

        private static bool fIsExtracted(string psTarget, string psMarker, string psExpected)
        {
            return File.Exists(psMarker) && File.ReadAllText(psMarker) == psExpected
                && File.Exists(Path.Combine(Path.Combine(psTarget, "DATA"), "LEV.ARK"));
        }

        /// <summary>
        /// Creates the lock file, waiting while another process holds it. The file is created
        /// new (it fails while it exists) and deleted when it is closed; a lock left behind by a
        /// process that died is taken over once it is older than an extraction ever takes.
        /// Null with psError set when the other process does not finish in time.
        /// </summary>
        private static FileStream fTakeLock(string psLockPath, out string psError)
        {
            const int liWaitMilliseconds = 120000;
            const int liPollMilliseconds = 250;
            const int liStaleMinutes = 10;

            psError = null;

            DateTime lODeadline = DateTime.UtcNow.AddMilliseconds(liWaitMilliseconds);

            while (true)
            {
                try
                {
                    return new FileStream(psLockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                        1, FileOptions.DeleteOnClose);
                }
                catch (IOException)
                {
                    // Held by another process - or left behind by one that died.
                }
                catch (UnauthorizedAccessException)
                {
                    // Windows: the holder's file is being deleted right now.
                }

                try
                {
                    if (File.Exists(psLockPath)
                        && DateTime.UtcNow - File.GetLastWriteTimeUtc(psLockPath) > TimeSpan.FromMinutes(liStaleMinutes))
                    {
                        File.Delete(psLockPath);

                        continue;
                    }
                }
                catch (Exception)
                {
                    // Still held after all; wait as for any other holder.
                }

                if (DateTime.UtcNow > lODeadline)
                {
                    psError = "Another program is extracting the game data right now (" + psLockPath
                        + "). Try again when it has finished.";

                    return null;
                }

                System.Threading.Thread.Sleep(liPollMilliseconds);
            }
        }

        /// <summary>Is this the supported UW.EXE (size and SHA-1)?</summary>
        public static bool IsSupportedExe(string psExePath)
        {
            try
            {
                return File.Exists(psExePath) && fIsSupportedExe(File.ReadAllBytes(psExePath));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool fIsSupportedExe(byte[] pyExe)
        {
            if (pyExe == null || pyExe.Length != SupportedExeSize)
                return false;

            using (SHA1 lOSha = SHA1.Create())
            {
                byte[] lyHash = lOSha.ComputeHash(pyExe);
                StringBuilder lOHex = new StringBuilder(lyHash.Length * 2);

                foreach (byte lyByte in lyHash)
                    lOHex.Append(lyByte.ToString("x2"));

                return lOHex.ToString() == SupportedExeSha1;
            }
        }

        private static string fMarkerText(FileInfo pOImage)
        {
            return "version " + MarkerVersion + "\nsize " + pOImage.Length + "\ntime " + pOImage.LastWriteTimeUtc.Ticks + "\n";
        }

        private static void fExtractDirectory(IsoReader pOReader, IsoEntry pODirectory, string psTarget)
        {
            Directory.CreateDirectory(psTarget);

            foreach (IsoEntry lOEntry in pOReader.List(pODirectory))
            {
                // Upper case as DOS wrote them, whatever the image's directory says: the readers
                // name the files in upper case, and Linux compares the case.
                string lsPath = Path.Combine(psTarget, lOEntry.Name.ToUpperInvariant());

                if (lOEntry.IsDirectory)
                    fExtractDirectory(pOReader, lOEntry, lsPath);
                else
                    File.WriteAllBytes(lsPath, pOReader.Read(lOEntry));
            }
        }

        /// <summary>A file or directory of an ISO 9660 image.</summary>
        private sealed class IsoEntry
        {
            public string Name;
            public long Extent;
            public long Size;
            public bool IsDirectory;
        }

        /// <summary>
        /// A minimal ISO 9660 reader: primary volume descriptor at sector 16, directory records
        /// with the extent (sector) at +2, the data length at +10, the flags at +25 (bit 1 =
        /// directory) and the name at +33 (length at +32, file names end in ";1"). Plain
        /// 2048-byte sectors, as in game.gog.
        /// </summary>
        private sealed class IsoReader
        {
            private const int SectorSize = 2048;

            private readonly Stream mOStream;

            public readonly IsoEntry Root;

            public IsoReader(Stream pOStream)
            {
                mOStream = pOStream;

                byte[] lyDescriptor = fReadBytes(16L * SectorSize, SectorSize);

                if (lyDescriptor[0] != 1 || Encoding.ASCII.GetString(lyDescriptor, 1, 5) != "CD001")
                    throw new InvalidDataException("Not an ISO 9660 image.");

                Root = fParseRecord(lyDescriptor, 156);

                if (Root == null)
                    throw new InvalidDataException("ISO 9660 root directory missing.");
            }

            public List<IsoEntry> List(IsoEntry pODirectory)
            {
                List<IsoEntry> lOEntries = new List<IsoEntry>();
                byte[] lyData = Read(pODirectory);
                int liPos = 0;

                while (liPos < lyData.Length)
                {
                    int liLength = lyData[liPos];

                    if (liLength == 0)
                    {
                        // the rest of this sector is padding
                        liPos = ((liPos / SectorSize) + 1) * SectorSize;
                        continue;
                    }

                    IsoEntry lOEntry = fParseRecord(lyData, liPos);

                    if (lOEntry != null && lOEntry.Name != "." && lOEntry.Name != "..")
                        lOEntries.Add(lOEntry);

                    liPos += liLength;
                }

                return lOEntries;
            }

            public IsoEntry Find(IsoEntry pODirectory, string psName)
            {
                foreach (IsoEntry lOEntry in List(pODirectory))
                    if (string.Equals(lOEntry.Name, psName, StringComparison.OrdinalIgnoreCase))
                        return lOEntry;

                return null;
            }

            public byte[] Read(IsoEntry pOEntry)
            {
                return fReadBytes(pOEntry.Extent * SectorSize, (int)pOEntry.Size);
            }

            private static IsoEntry fParseRecord(byte[] pyData, int piPos)
            {
                if (piPos + 34 > pyData.Length)
                    return null;

                int liNameLength = pyData[piPos + 32];

                if (piPos + 33 + liNameLength > pyData.Length)
                    return null;

                string lsName;

                if (liNameLength == 1 && pyData[piPos + 33] == 0)
                    lsName = ".";
                else if (liNameLength == 1 && pyData[piPos + 33] == 1)
                    lsName = "..";
                else
                {
                    lsName = Encoding.ASCII.GetString(pyData, piPos + 33, liNameLength);
                    int liVersion = lsName.IndexOf(';');

                    if (liVersion >= 0)
                        lsName = lsName.Substring(0, liVersion);

                    // files without an extension end in a dot
                    lsName = lsName.TrimEnd('.');
                }

                return new IsoEntry
                {
                    Name = lsName,
                    Extent = BitConverter.ToUInt32(pyData, piPos + 2),
                    Size = BitConverter.ToUInt32(pyData, piPos + 10),
                    IsDirectory = (pyData[piPos + 25] & 2) != 0
                };
            }

            private byte[] fReadBytes(long piOffset, int piCount)
            {
                byte[] lyBuffer = new byte[piCount];
                mOStream.Seek(piOffset, SeekOrigin.Begin);
                int liRead = 0;

                while (liRead < piCount)
                {
                    int liChunk = mOStream.Read(lyBuffer, liRead, piCount - liRead);

                    if (liChunk <= 0)
                        throw new EndOfStreamException("Unexpected end of the image.");

                    liRead += liChunk;
                }

                return lyBuffer;
            }
        }
    }
}
