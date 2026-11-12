using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UWDataImport.UWData;

namespace UWDataImport.Install
{
    /// <summary>
    /// The game data of the supported version: Ultima Underworld 1 and 2 from GOG.com, or the same
    /// from Steam.
    ///
    /// STEAM (per user, 2026-11-05: EA brought both games to Steam that day): each game in its own
    /// folder under steamapps/common, "Ultima Underworld I" and "Ultima Underworld II", each with the
    /// same CD image as GOG's under the name game.bin (byte for byte GOG's game.gog of the first game,
    /// MD5 1be5d90a96c4b9d495152ee38ca572ff), the old DOSBox 0.74-2 and its own UNDEROM1 or UNDEROM2
    /// for the saves, without cloud_saves (its configuration comments the overlay out). The libraries
    /// are found by Steam's libraryfolders.vdf (GetSteamLibraries).
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

        /// <summary>The same image's name in a Steam installation.</summary>
        public const string SteamImageFileName = "game.bin";

        /// <summary>The image's names, GOG's first.</summary>
        public static readonly string[] ImageFileNames = { ImageFileName, SteamImageFileName };

        /// <summary>The image in a folder by either name, GOG's first; null when there is none.</summary>
        public static string ImageIn(string psFolder)
        {
            if (string.IsNullOrEmpty(psFolder))
                return null;

            try
            {
                foreach (string lsName in ImageFileNames)
                {
                    string lsPath = Path.Combine(psFolder, lsName);

                    if (File.Exists(lsPath))
                        return lsPath;
                }
            }
            catch (Exception)
            {
                // A folder that cannot be read holds no image.
            }

            return null;
        }

        private const string MarkerFileName = "extracted.txt";
        private const int MarkerVersion = 1;

        private static readonly string[] msFoldersToExtract = { "CRIT", "CUTS", "DATA", "SOUND" };

        /// <summary>
        /// Finds the image for a path that is either the image itself or the GOG or Steam installation
        /// folder (or its UNDEROM1 subfolder), or the EA app's installation, which keeps it in DATA
        /// (2026-11-12, UWInstallRegistry). Null if there is none.
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

                string lsInFolder = ImageIn(lsTrimmed) ?? ImageIn(Path.Combine(lsTrimmed, UWInstallRegistry.EaDataFolder));

                if (lsInFolder != null)
                    return lsInFolder;

                DirectoryInfo lOParent = Directory.GetParent(lsTrimmed);

                return lOParent == null ? null : ImageIn(lOParent.FullName);
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
        ///
        /// STEAM (2026-11-05): after GOG's folders of the first game the Steam libraries' "Ultima Underworld
        /// I", at the end their "Ultima Underworld II" - GOG's first, as before.
        /// </summary>
        public static string[] GetCandidateFolders(bool pbLinux)
        {
            string[] lsGog = fGogCandidateFolders(pbLinux, out int liFirstGame);
            List<string> lOAll = new List<string>();
            List<string> lOLibraries = GetSteamLibraries(pbLinux);

            for (int liAt = 0; liAt < liFirstGame; liAt++)
                lOAll.Add(lsGog[liAt]);

            foreach (string lsLibrary in lOLibraries)
                lOAll.Add(SteamGameFolder(lsLibrary, 0));

            for (int liAt = liFirstGame; liAt < lsGog.Length; liAt++)
                lOAll.Add(lsGog[liAt]);

            foreach (string lsLibrary in lOLibraries)
                lOAll.Add(SteamGameFolder(lsLibrary, 1));

            return lOAll.ToArray();
        }

        /// <summary>The two games' folders under a Steam library's steamapps/common.</summary>
        public static readonly string[] SteamGameFolders = { "Ultima Underworld I", "Ultima Underworld II" };

        /// <summary>A game's folder in a Steam library (piGame 0 the first game, 1 the second).</summary>
        public static string SteamGameFolder(string psLibrary, int piGame)
        {
            return Path.Combine(psLibrary, "steamapps", "common", SteamGameFolders[piGame]);
        }

        /// <summary>
        /// The Steam libraries to look in: each Steam folder itself and every library its
        /// steamapps/libraryfolders.vdf lists ("path" entries), without repeats. Steam's folder by its usual
        /// places - on Windows Program Files (x86) and Program Files and, as Steam may live on any drive, a
        /// Steam and a SteamLibrary folder at each fixed drive's root; on Linux ~/.steam/steam,
        /// ~/.local/share/Steam and the Flatpak's.
        /// </summary>
        public static List<string> GetSteamLibraries(bool pbLinux)
        {
            List<string> lORoots = new List<string>();

            try
            {
                if (pbLinux)
                {
                    string lsHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                    if (string.IsNullOrEmpty(lsHome))
                        lsHome = "/root";

                    lORoots.Add(Path.Combine(lsHome, ".steam/steam"));
                    lORoots.Add(Path.Combine(lsHome, ".local/share/Steam"));
                    lORoots.Add(Path.Combine(lsHome, ".var/app/com.valvesoftware.Steam/.local/share/Steam"));
                }
                else
                {
                    string lsX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    string lsPlain = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

                    lORoots.Add(Path.Combine(string.IsNullOrEmpty(lsX86) ? @"C:\Program Files (x86)" : lsX86, "Steam"));

                    if (!string.IsNullOrEmpty(lsPlain))
                        lORoots.Add(Path.Combine(lsPlain, "Steam"));

                    foreach (DriveInfo lODrive in DriveInfo.GetDrives())
                    {
                        if (lODrive.DriveType != DriveType.Fixed)
                            continue;

                        lORoots.Add(Path.Combine(lODrive.RootDirectory.FullName, "Steam"));
                        lORoots.Add(Path.Combine(lODrive.RootDirectory.FullName, "SteamLibrary"));
                    }
                }
            }
            catch (Exception)
            {
                // Whatever could be listed is looked in.
            }

            List<string> lOLibraries = new List<string>();

            foreach (string lsRoot in lORoots)
            {
                fAddLibrary(lOLibraries, lsRoot, pbLinux);

                try
                {
                    string lsVdf = Path.Combine(lsRoot, "steamapps", "libraryfolders.vdf");

                    if (!File.Exists(lsVdf))
                        continue;

                    foreach (string lsLibrary in ParseLibraryFolders(File.ReadAllText(lsVdf)))
                        fAddLibrary(lOLibraries, lsLibrary, pbLinux);
                }
                catch (Exception)
                {
                    // An unreadable list adds nothing.
                }
            }

            return lOLibraries;
        }

        private static void fAddLibrary(List<string> pOLibraries, string psLibrary, bool pbLinux)
        {
            if (string.IsNullOrEmpty(psLibrary))
                return;

            string lsTrimmed = psLibrary.TrimEnd('\\', '/');
            StringComparison leCompare = pbLinux ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            foreach (string lsKnown in pOLibraries)
            {
                if (string.Equals(lsKnown, lsTrimmed, leCompare))
                    return;
            }

            pOLibraries.Add(lsTrimmed);
        }

        /// <summary>
        /// The libraries a libraryfolders.vdf names: every "path" key's value, its escaped backslashes
        /// made single (Steam writes "D:\\SteamLibrary"). Older files listed them under number keys
        /// ("1" "D:\\SteamLibrary"); those values are taken as well when they look like a folder.
        /// </summary>
        public static List<string> ParseLibraryFolders(string psVdf)
        {
            List<string> lOPaths = new List<string>();

            if (string.IsNullOrEmpty(psVdf))
                return lOPaths;

            foreach (string lsRaw in psVdf.Split('\n'))
            {
                List<string> lOTokens = fQuotedTokens(lsRaw);

                if (lOTokens.Count != 2)
                    continue;

                string lsKey = lOTokens[0];
                string lsValue = lOTokens[1].Replace("\\\\", "\\");
                bool lbNumbered = lsKey.Length > 0 && char.IsDigit(lsKey[0]) && (lsValue.Contains("\\") || lsValue.StartsWith("/", StringComparison.Ordinal));

                if (string.Equals(lsKey, "path", StringComparison.OrdinalIgnoreCase) || lbNumbered)
                    lOPaths.Add(lsValue);
            }

            return lOPaths;
        }

        /// <summary>The quoted strings of a line, an escaped quote kept inside.</summary>
        private static List<string> fQuotedTokens(string psLine)
        {
            List<string> lOTokens = new List<string>();
            StringBuilder lOToken = null;

            for (int liAt = 0; liAt < psLine.Length; liAt++)
            {
                char lcAt = psLine[liAt];

                if (lOToken == null)
                {
                    if (lcAt == '"')
                        lOToken = new StringBuilder();

                    continue;
                }

                if (lcAt == '\\' && liAt + 1 < psLine.Length)
                {
                    lOToken.Append(lcAt).Append(psLine[++liAt]);

                    continue;
                }

                if (lcAt == '"')
                {
                    lOTokens.Add(lOToken.ToString());
                    lOToken = null;

                    continue;
                }

                lOToken.Append(lcAt);
            }

            return lOTokens;
        }

        /// <summary>GOG's folders as before; piFirstGame says how many of them are the first game's (the rest
        /// are fWithSecondGame's).</summary>
        private static string[] fGogCandidateFolders(bool pbLinux, out int piFirstGame)
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
                piFirstGame = lOFolders.Count;

                return fWithSecondGame(lOFolders);
            }

            // WHERE THE STORES SAY (2026-11-12): GOG's and the EA app's registry entries of the first game come before the
            // usual folders, those of the second before the second's (UWInstallRegistry).
            lOFolders.AddRange(UWInstallRegistry.FoldersOf(0));
            lOFolders.Add(@"C:\GOG Games\Ultima Underworld");
            lOFolders.Add(@"D:\GOG Games\Ultima Underworld");
            lOFolders.Add(@"C:\Program Files (x86)\GOG Galaxy\Games\Ultima Underworld");
            lOFolders.Add(@"C:\Program Files (x86)\GOG.com\Ultima Underworld");
            lOFolders.Add(@"C:\Ultima Underworld");
            lOFolders.Add(@"D:\Ultima Underworld");
            piFirstGame = lOFolders.Count;

            List<string> lOAll = new List<string>(fWithSecondGame(lOFolders));

            lOAll.InsertRange(piFirstGame, UWInstallRegistry.FoldersOf(1));

            return lOAll.ToArray();
        }

        /// <summary>The list, then the same folders for the second game's own GOG product: every image
        /// holds both games, so one who owns only the second is found too, and the first game's folders
        /// still come first.</summary>
        private static string[] fWithSecondGame(List<string> pOFolders)
        {
            List<string> lOAll = new List<string>(pOFolders);

            foreach (string lsFolder in pOFolders)
            {
                if (!lsFolder.EndsWith(msGameFolders[0], StringComparison.OrdinalIgnoreCase))
                    continue;

                string lsAbove = lsFolder.Substring(0, lsFolder.Length - msGameFolders[0].Length);

                for (int liAt = 1; liAt < msGameFolders.Length; liAt++)
                    lOAll.Add(lsAbove + msGameFolders[liAt]);
            }

            return lOAll.ToArray();
        }

        /// <summary>The folder GOG's current installers lay over drive C: in DOSBox (2026-10-29):
        /// dosboxULTIMAn_single.conf mounts the installation as C: and then this folder over it
        /// (`mount C "..\cloud_saves" -t overlay`), so the DOS game writes its saves to
        /// cloud_saves/UNDEROMn and reads them there first.</summary>
        public const string CloudSavesFolderName = "cloud_saves";

        /// <summary>The installation folders of the two games' own GOG products, the first's first, and
        /// Steam's first game ("Ultima Underworld I", 2026-11-05; its second is "Ultima Underworld II" as
        /// GOG's): the saves of one game are looked for beside the other's installation.</summary>
        private static readonly string[] msGameFolders = { "Ultima Underworld", "Ultima Underworld 2", "Ultima Underworld II", "Ultima Underworld I" };

        /// <summary>
        /// The folder holding the game's SAVE1 to SAVE4: in the image's installation when it is the
        /// game's (it has the game's UNDEROM1 or UNDEROM2), else in the game's own GOG product's
        /// installation beside it ("Ultima Underworld 2"; GOG sells one product per game, each with the
        /// whole 1+2 CD as game.gog, but each starts only its own game). Null when there is none.
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
        /// follows where the original last ran: SAVE0, its working copy, which it writes on every start and
        /// this port never does - the folder with the newer SAVE0 wins. Without a SAVE0, cloud_saves when the
        /// installation has it and GOG's configuration (dosbox*_single.conf) mounts it as an overlay, else
        /// the folder holding the newest SAVE1 to SAVE4. INSTALLATION takes UNDEROMn whatever is there.
        ///
        /// CLOUD_SAVES IS READ AS DOSBOX'S OVERLAY (2026-11-12, per user: a GOG Galaxy install restored the old
        /// cloud saves SAVE1 and SAVE2 into UNDEROM1 and the original's into cloud_saves, and Galaxy syncs both,
        /// each local slot and the whole cloud_saves folder): a slot is read from cloud_saves when it is
        /// there, else from the installation's UNDEROMn below, as DOSBox shows the original the two folders
        /// laid over each other; every save is written to cloud_saves (SlotFolderToRead, OverlayBaseOf). The
        /// automatic choice of cloud_saves therefore no longer hides the older slots of UNDEROMn.
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

                if (lsFound != null)
                    return lsFound;

                DirectoryInfo lOParent = lsInstall == null ? null : Directory.GetParent(lsInstall);

                foreach (string lsName in msGameFolders)
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

        /// <summary>
        /// THE GAMES IN TWO FOLDERS (per user, 2026-10-31): whoever changes between the original and this
        /// port starts each game from its own installation, wherever that lies, though either image holds
        /// both. So the saves are looked for in the folder chosen for the game first (psOwnFolder, may be
        /// empty), then as above, then in the other folders given.
        /// </summary>
        public static string GetSavegameFolder(string psImagePath, IUWGameProfile pOGame, SaveFolderModeEnum peMode, string psOwnFolder, IEnumerable<string> pOOtherFolders)
        {
            string lsOwnImage = FindImage(psOwnFolder);
            string lsFound = lsOwnImage == null ? null : fSavegameFolderIn(Path.GetDirectoryName(lsOwnImage), pOGame, peMode);

            if (lsFound == null)
                lsFound = GetSavegameFolder(psImagePath, pOGame, peMode);

            if (lsFound != null || pOOtherFolders == null)
                return lsFound;

            foreach (string lsFolder in pOOtherFolders)
            {
                lsFound = fSavegameFolderIn(lsFolder, pOGame, peMode);

                if (lsFound != null)
                    return lsFound;
            }

            return null;
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

            DateTime ldOwnCopy = fWorkingCopy(lsOwn);
            DateTime ldCloudCopy = fWorkingCopy(lsCloud);

            if (ldOwnCopy != DateTime.MinValue || ldCloudCopy != DateTime.MinValue)
                return ldCloudCopy > ldOwnCopy ? lsCloud : lsOwn;

            if (Directory.Exists(Path.Combine(psInstall, CloudSavesFolderName)) && MountsCloudSaves(psInstall))
                return lsCloud;

            DateTime ldOwn = fNewestSave(lsOwn);
            DateTime ldCloud = fNewestSave(lsCloud);

            return ldCloud > ldOwn ? lsCloud : lsOwn;
        }

        /// <summary>The PLAYER.DAT of SAVE0, the original's working copy, written on every start of a game and
        /// never by this port; MinValue for none.</summary>
        private static DateTime fWorkingCopy(string psFolder)
        {
            string lsFile = Path.Combine(psFolder, "SAVE0", "PLAYER.DAT");

            return File.Exists(lsFile) ? File.GetLastWriteTimeUtc(lsFile) : DateTime.MinValue;
        }

        /// <summary>The file that marks a used slot (UWSavegameSlots).</summary>
        public const string SlotMarkerFile = "LEV.ARK";

        /// <summary>The installation's own UNDEROMn below a save folder cloud_saves/UNDEROMn - what DOSBox's
        /// overlay lays cloud_saves over -, or null when the folder is no cloud_saves one or the installation
        /// has no such folder.</summary>
        public static string OverlayBaseOf(string psSaveFolder)
        {
            try
            {
                if (string.IsNullOrEmpty(psSaveFolder))
                    return null;

                string lsFolder = psSaveFolder.TrimEnd('\\', '/');
                DirectoryInfo lOCloud = Directory.GetParent(lsFolder);

                if (lOCloud == null || lOCloud.Parent == null
                    || !string.Equals(lOCloud.Name, CloudSavesFolderName, StringComparison.OrdinalIgnoreCase))
                    return null;

                string lsBase = Path.Combine(lOCloud.Parent.FullName, Path.GetFileName(lsFolder));

                return Directory.Exists(lsBase) ? lsBase : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The folder slot piSlot is read from: SAVEn of the save folder, or, when that is cloud_saves
        /// and holds no save there, the installation's SAVEn below it if that holds one (OverlayBaseOf). Saves
        /// are always written to the save folder's own SAVEn.</summary>
        public static string SlotFolderToRead(string psSaveFolder, int piSlot)
        {
            if (string.IsNullOrEmpty(psSaveFolder))
                return null;

            string lsOwn = Path.Combine(psSaveFolder, "SAVE" + piSlot);

            if (File.Exists(Path.Combine(lsOwn, SlotMarkerFile)))
                return lsOwn;

            string lsBase = OverlayBaseOf(psSaveFolder);

            if (lsBase == null)
                return lsOwn;

            string lsBelow = Path.Combine(lsBase, "SAVE" + piSlot);

            return File.Exists(Path.Combine(lsBelow, SlotMarkerFile)) ? lsBelow : lsOwn;
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
