using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UWDataImport.UWData;

namespace UWDataImport.Install
{
    /// <summary>
    /// The game data of the supported version: Ultima Underworld 1 and 2 from GOG.com.
    ///
    /// In the GOG installation the data is not on disk: `UNDEROM1\DATA` holds only UW.CFG.
    /// Everything lives in `game.gog`, an ISO 9660 CD image (2048-byte sectors) that the
    /// bundled DOSBox mounts as drive D:. UW1 is in its folder `UW` (CRIT, CUTS, DATA, SOUND,
    /// UW.EXE), UW2 in `UW2` (the same folders and UW2.EXE). The save games are on disk in
    /// `UNDEROM1\SAVE1` to `SAVE4`, UW2's in `UNDEROM2` (the image's UW2.BAT looks for it).
    ///
    /// This class finds the image, extracts one game's folders once into a cache folder of its
    /// own with the same layout as an installed game (CRIT, CUTS, DATA, SOUND and the program
    /// side by side), and checks that the program is the supported build - the model import reads
    /// it at fixed offsets. WHICH GAME is the profile's (IUWGameProfile: the image folder, the
    /// program with its size and hash, the cache and save folders; UW2 plan phase 0 step 4,
    /// 2026-10-28 - the first game's only until then). No Unity references, see UWDataImport.asmdef.
    /// </summary>
    public static class UWGogInstall
    {
        /// <summary>File name of the CD image in a GOG installation.</summary>
        public const string ImageFileName = "game.gog";

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

        /// <summary>The folder GOG's current installers lay over drive C: in DOSBox (2026-10-29):
        /// dosboxULTIMAn_single.conf mounts the installation as C: and then this folder over it
        /// (`mount C "..\cloud_saves" -t overlay`), so the DOS game writes its saves to
        /// cloud_saves/UNDEROMn and reads them there first.</summary>
        public const string CloudSavesFolderName = "cloud_saves";

        /// <summary>The installation folders of the second game's own GOG product, beside the first's.</summary>
        private static readonly string[] msSecondGameFolders = { "Ultima Underworld 2", "Ultima Underworld II" };

        /// <summary>
        /// The folder holding the game's SAVE1 to SAVE4: in the image's installation when it is the
        /// game's (it has the game's UNDEROM1 or UNDEROM2), and for the second game also in its own
        /// GOG product's installation beside the first's ("Ultima Underworld 2"; GOG sells one product
        /// per game, each with the whole 1+2 CD as game.gog, but each starts only its own game). Null
        /// when there is none.
        ///
        /// WHERE IN AN INSTALLATION (2026-10-29, the user's fresh installs of GOG's current installers):
        /// cloud_saves/UNDEROMn when the installation has a cloud_saves folder - that is where DOSBox
        /// writes and reads first, the installer's UNDEROMn holds no saves then; UNDEROMn itself in an
        /// older installation without it, as until then.
        /// </summary>
        public static string GetSavegameFolder(string psImagePath, IUWGameProfile pOGame)
        {
            return GetSavegameFolder(psImagePath, pOGame, SaveFolderModeEnum.Automatic);
        }

        /// <summary>
        /// WHERE THE SAVES GO (per user, 2026-10-29: "Automatisch + Schalter"). The user's second install
        /// started UW2 through a DOSBox Staging of his own whose configuration comments the overlay out, so
        /// DOS wrote to UNDEROM2 although cloud_saves existed - and GOG's own configuration of the same
        /// install mounts it. Which DOSBox starts the game cannot be read from the files, so AUTOMATIC
        /// follows the saves: the folder holding the newest SAVE1 to SAVE4 (its PLAYER.DAT) wins; without
        /// any save, cloud_saves when the installation has it and GOG's configuration (dosbox*_single.conf)
        /// mounts it as an overlay, else UNDEROMn. The other two modes take one folder whatever is there.
        /// </summary>
        public enum SaveFolderModeEnum
        {
            Automatic,
            CloudSaves,
            Installation
        }

        /// <summary>The stored mode (settings.json's SaveFolder: empty, "CloudSaves", "Installation").</summary>
        public static SaveFolderModeEnum ParseSaveFolderMode(string psValue)
        {
            return !string.IsNullOrEmpty(psValue) && Enum.TryParse(psValue, true, out SaveFolderModeEnum leMode) ? leMode : SaveFolderModeEnum.Automatic;
        }

        /// <summary>The save games' folder by the mode (see SaveFolderModeEnum); the rest as above.</summary>
        public static string GetSavegameFolder(string psImagePath, IUWGameProfile pOGame, SaveFolderModeEnum peMode)
        {
            try
            {
                string lsInstall = Path.GetDirectoryName(psImagePath);
                string lsFound = fSavegameFolderIn(lsInstall, pOGame, peMode);

                if (lsFound != null || pOGame.Game == UWGame.Uw1)
                    return lsFound;

                DirectoryInfo lOParent = lsInstall == null ? null : Directory.GetParent(lsInstall);

                foreach (string lsName in msSecondGameFolders)
                {
                    lsFound = lOParent == null ? null : fSavegameFolderIn(Path.Combine(lOParent.FullName, lsName), pOGame, peMode);

                    if (lsFound != null)
                        return lsFound;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The game's save folder in one installation, or null when the installation is not
        /// the game's (no UNDEROM1 or UNDEROM2 of its own).</summary>
        private static string fSavegameFolderIn(string psInstall, IUWGameProfile pOGame, SaveFolderModeEnum peMode)
        {
            if (string.IsNullOrEmpty(psInstall) || !Directory.Exists(Path.Combine(psInstall, pOGame.SaveFolder)))
                return null;

            string lsOwn = Path.Combine(psInstall, pOGame.SaveFolder);
            string lsCloud = Path.Combine(psInstall, CloudSavesFolderName, pOGame.SaveFolder);

            if (peMode == SaveFolderModeEnum.Installation)
                return lsOwn;

            if (peMode == SaveFolderModeEnum.CloudSaves)
                return lsCloud;

            DateTime ldOwn = fNewestSave(lsOwn);
            DateTime ldCloud = fNewestSave(lsCloud);

            if (ldOwn != DateTime.MinValue || ldCloud != DateTime.MinValue)
                return ldCloud > ldOwn ? lsCloud : lsOwn;

            return Directory.Exists(Path.Combine(psInstall, CloudSavesFolderName)) && MountsCloudSaves(psInstall) ? lsCloud : lsOwn;
        }

        /// <summary>The newest PLAYER.DAT of SAVE1 to SAVE4 in a save folder (SAVE0 is the running game's
        /// working copy); MinValue for none.</summary>
        private static DateTime fNewestSave(string psFolder)
        {
            DateTime ldNewest = DateTime.MinValue;

            for (int liSlot = 1; liSlot <= 4; liSlot++)
            {
                string lsFile = Path.Combine(psFolder, "SAVE" + liSlot, "PLAYER.DAT");

                if (File.Exists(lsFile) && File.GetLastWriteTimeUtc(lsFile) > ldNewest)
                    ldNewest = File.GetLastWriteTimeUtc(lsFile);
            }

            return ldNewest;
        }

        /// <summary>Whether GOG's configuration in the installation (dosbox*_single.conf) mounts the
        /// cloud_saves overlay: a line, not a comment, that mounts cloud_saves with -t overlay.</summary>
        public static bool MountsCloudSaves(string psInstall)
        {
            try
            {
                foreach (string lsConf in Directory.GetFiles(psInstall, "dosbox*_single.conf"))
                {
                    foreach (string lsLine in File.ReadAllLines(lsConf))
                    {
                        string lsTrim = lsLine.Trim();

                        if (lsTrim.StartsWith("#", StringComparison.Ordinal))
                            continue;

                        string lsLower = lsTrim.ToLowerInvariant();

                        if (lsLower.StartsWith("mount ", StringComparison.Ordinal) && lsLower.Contains(CloudSavesFolderName) && lsLower.Contains("-t overlay"))
                            return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// One file of the game's DATA folder in the image (UW\DATA, UW2\DATA) read straight from
        /// the image, or null when there is no such file or the image cannot be read. For the
        /// self-check, whose extracted cache may lack a file the image has (the FONT*.SYS, found
        /// 2026-10-26).
        /// </summary>
        public static byte[] ReadDataFile(string psImagePath, IUWGameProfile pOGame, string psFileName)
        {
            try
            {
                using (FileStream lOStream = new FileStream(psImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    IsoReader lOReader = new IsoReader(lOStream);
                    IsoEntry lOUw = lOReader.Find(lOReader.Root, pOGame.ImageFolder);
                    IsoEntry lOData = lOUw != null && lOUw.IsDirectory ? lOReader.Find(lOUw, "DATA") : null;
                    IsoEntry lOFile = lOData != null && lOData.IsDirectory ? lOReader.Find(lOData, psFileName) : null;

                    return lOFile != null && !lOFile.IsDirectory ? lOReader.Read(lOFile) : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether the image holds the game: its folder with the supported build of its program. A
        /// game the image does not hold is not offered (UW2 plan section 4.1). False when the image
        /// cannot be read.
        /// </summary>
        public static bool HasGame(string psImagePath, IUWGameProfile pOGame)
        {
            try
            {
                using (FileStream lOStream = new FileStream(psImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    IsoReader lOReader = new IsoReader(lOStream);
                    IsoEntry lOFolder = lOReader.Find(lOReader.Root, pOGame.ImageFolder);
                    IsoEntry lOExe = lOFolder != null && lOFolder.IsDirectory ? lOReader.Find(lOFolder, pOGame.Executable) : null;

                    return lOExe != null && !lOExe.IsDirectory && lOExe.Size == pOGame.ExecutableSize
                        && fIsSupportedExe(lOReader.Read(lOExe), pOGame);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Makes sure the game's data of the image is extracted below psCacheRoot, into the game's
        /// own cache folder (UW1, UW2), and returns the path of its DATA folder, or null with
        /// psError set. An existing extraction of the same image (size and time stamp) is reused.
        /// </summary>
        public static string EnsureExtracted(string psImagePath, string psCacheRoot, IUWGameProfile pOGame, out string psError)
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

                string lsTarget = Path.Combine(psCacheRoot, pOGame.CacheFolder);
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
                    IsoEntry lOUw = lOReader.Find(lOReader.Root, pOGame.ImageFolder);

                    if (lOUw == null || !lOUw.IsDirectory)
                    {
                        psError = "The image has no " + pOGame.ImageFolder + " folder - is this the GOG version of " + pOGame.Title + "?";
                        return null;
                    }

                    IsoEntry lOExe = lOReader.Find(lOUw, pOGame.Executable);

                    if (lOExe == null || lOExe.IsDirectory)
                    {
                        psError = pOGame.Executable + " is missing in the image.";
                        return null;
                    }

                    byte[] lyExe = lOReader.Read(lOExe);

                    if (!fIsSupportedExe(lyExe, pOGame))
                    {
                        psError = "Unsupported " + pOGame.Executable + " (" + lyExe.Length + " bytes). Only the GOG version of " + pOGame.Title + " is supported.";
                        return null;
                    }

                    File.WriteAllBytes(Path.Combine(lsTemporary, pOGame.Executable), lyExe);

                    foreach (string lsFolder in msFoldersToExtract)
                    {
                        IsoEntry lODirectory = lOReader.Find(lOUw, lsFolder);

                        if (lODirectory == null || !lODirectory.IsDirectory)
                        {
                            psError = "Folder " + pOGame.ImageFolder + "\\" + lsFolder + " is missing in the image.";
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

        /// <summary>Is this the supported build of the game's program (size and SHA-1)?</summary>
        public static bool IsSupportedExe(string psExePath, IUWGameProfile pOGame)
        {
            try
            {
                return File.Exists(psExePath) && fIsSupportedExe(File.ReadAllBytes(psExePath), pOGame);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool fIsSupportedExe(byte[] pyExe, IUWGameProfile pOGame)
        {
            if (pyExe == null || pyExe.Length != pOGame.ExecutableSize)
                return false;

            using (SHA1 lOSha = SHA1.Create())
            {
                byte[] lyHash = lOSha.ComputeHash(pyExe);
                StringBuilder lOHex = new StringBuilder(lyHash.Length * 2);

                foreach (byte lyByte in lyHash)
                    lOHex.Append(lyByte.ToString("x2"));

                return lOHex.ToString() == pOGame.ExecutableSha1;
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
