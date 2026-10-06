using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UWDataImport
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

                if (File.Exists(lsMarker) && File.ReadAllText(lsMarker) == lsExpected
                    && File.Exists(Path.Combine(Path.Combine(lsTarget, "DATA"), "LEV.ARK")))
                {
                    return Path.Combine(lsTarget, "DATA");
                }

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
