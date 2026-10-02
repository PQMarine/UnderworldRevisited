using System.IO;
using UWDataImport;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Finds the game data for the command line tools, exactly as the game does it
    /// (see UWSettings.DataPath): a DATA folder as it is, a GOG installation folder or
    /// game.gog itself is extracted into a cache once and reused afterwards.
    /// </summary>
    public static class DataPath
    {
        /// <summary>The file by which a data folder is recognised.</summary>
        public const string MarkerFile = "LEV.ARK";

        /// <summary>Returns the DATA folder, or null with psError set.</summary>
        public static string Resolve(string psPath, out string psError)
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
            string lsData = UWGogInstall.EnsureExtracted(lsImage, lsCacheRoot, out string lsExtractError);

            if (lsData == null)
                psError = lsExtractError ?? "Could not extract the image.";

            return lsData;
        }

        /// <summary>
        /// UW.EXE belongs to the installation, not to DATA. The game looks in the data folder
        /// first and then one level up (UWSettings.ExePath); the extracted GOG cache has it
        /// above DATA. Null if there is none.
        /// </summary>
        public static string FindExe(string psDataPath)
        {
            if (string.IsNullOrEmpty(psDataPath))
                return null;

            string lsInData = Path.Combine(psDataPath, "UW.EXE");

            if (File.Exists(lsInData))
                return lsInData;

            DirectoryInfo lOParent = Directory.GetParent(psDataPath);
            string lsAbove = lOParent == null ? null : Path.Combine(lOParent.FullName, "UW.EXE");

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
