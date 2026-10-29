using System.IO;
using UWDataImport;
using UWDataImport.Install;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Finds the game data for the command line tools, exactly as the game does it
    /// (see UWSettings.DataPath): a DATA folder as it is, a GOG installation folder or
    /// game.gog itself is extracted into a cache once and reused afterwards. Without a path
    /// the tools search the same list as the game (UWGogInstall.GetCandidateFolders: the
    /// environment variable UW_DATA, then the common installation folders).
    ///
    /// The cache is the tools' own (the temp folder), not the game's (persistentDataPath): the
    /// tools do not know the player's company and product names, and a tool run never touches
    /// the game's folders. UWGogInstall locks each cache while it is being filled.
    ///
    /// PER GAME (UW2 plan phase 0 step 6, 2026-10-28): the image holds both games, each extracted
    /// into a cache folder of its own (UW1, UW2). Without a game the first; a DATA folder given
    /// directly is taken as it is, for whichever game it is.
    /// </summary>
    public static class DataPath
    {
        /// <summary>The file by which a data folder is recognised.</summary>
        public const string MarkerFile = "LEV.ARK";

        /// <summary>Returns the first game's DATA folder, or null with psError set.</summary>
        public static string Resolve(string psPath, out string psError)
        {
            return Resolve(psPath, UWGameProfiles.Uw1, out psError);
        }

        /// <summary>Returns the game's DATA folder, or null with psError set.</summary>
        public static string Resolve(string psPath, IUWGameProfile pOGame, out string psError)
        {
            psError = null;

            if (!string.IsNullOrEmpty(psPath) && File.Exists(Path.Combine(psPath, MarkerFile)))
                return psPath;

            string lsImage = UWGogInstall.FindImage(psPath);

            if (lsImage == null)
            {
                psError = "Neither a DATA folder with " + MarkerFile + " nor a game.gog in: " + psPath;

                return null;
            }

            string lsCacheRoot = Path.Combine(Path.GetTempPath(), "UnderworldRevisited");
            string lsData = UWGogInstall.EnsureExtracted(lsImage, lsCacheRoot, pOGame, out string lsExtractError);

            if (lsData == null)
                psError = lsExtractError ?? "Could not extract the image.";

            return lsData;
        }

        /// <summary>The data of the first candidate that holds the first game (UW_DATA, then the
        /// common installation folders), or null with psError set.</summary>
        public static string Find(out string psError)
        {
            return Find(UWGameProfiles.Uw1, out psError);
        }

        /// <summary>The data of the first candidate that holds the game: a DATA folder (the first
        /// game's only, as UW_DATA named it before there was a second) or an image holding it.</summary>
        public static string Find(IUWGameProfile pOGame, out string psError)
        {
            string[] lsCandidates = UWGogInstall.GetCandidateFolders(System.OperatingSystem.IsLinux());

            foreach (string lsCandidate in lsCandidates)
            {
                bool lbDataFolder = pOGame == UWGameProfiles.Uw1 && File.Exists(Path.Combine(lsCandidate, MarkerFile));
                string lsImage = lbDataFolder ? null : UWGogInstall.FindImage(lsCandidate);

                // The first game's image is taken as before, an unsupported build then failing in Resolve
                // with its reason; another game's must hold it, or the next candidate is tried.
                if (!lbDataFolder && (lsImage == null || (pOGame != UWGameProfiles.Uw1 && !UWGogInstall.HasGame(lsImage, pOGame))))
                    continue;

                return Resolve(lsCandidate, pOGame, out psError);
            }

            psError = "No game data found. Pass the path, or set UW_DATA to the DATA folder, the GOG"
                + " installation folder or game.gog. Searched: " + string.Join("; ", lsCandidates);

            return null;
        }

        /// <summary>
        /// UW.EXE belongs to the installation, not to DATA. The game looks in the data folder
        /// first and then one level up (UWSettings.ExePath); the extracted GOG cache has it
        /// above DATA. Null if there is none. The first game's program unless pOGame names another.
        /// </summary>
        public static string FindExe(string psDataPath, IUWGameProfile pOGame = null)
        {
            if (string.IsNullOrEmpty(psDataPath))
                return null;

            string lsExecutable = (pOGame ?? UWGameProfiles.Uw1).Executable;
            string lsInData = Path.Combine(psDataPath, lsExecutable);

            if (File.Exists(lsInData))
                return lsInData;

            DirectoryInfo lOParent = Directory.GetParent(psDataPath);
            string lsAbove = lOParent == null ? null : Path.Combine(lOParent.FullName, lsExecutable);

            return lsAbove != null && File.Exists(lsAbove) ? lsAbove : null;
        }

        /// <summary>The folder next to DATA that holds the cutscenes or the sound files.
        /// Null if it does not exist.</summary>
        public static string FindSiblingFolder(string psDataPath, string psName)
        {
            DirectoryInfo lOParent = string.IsNullOrEmpty(psDataPath) ? null : Directory.GetParent(psDataPath);
            string lsFolder = lOParent == null ? null : Path.Combine(lOParent.FullName, psName);

            return lsFolder != null && Directory.Exists(lsFolder) ? lsFolder : null;
        }
    }
}
