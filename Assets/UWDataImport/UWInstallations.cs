using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UWDataImport.UWData;

namespace UWDataImport.Install
{
    /// <summary>
    /// THE INSTALLATIONS FOUND, BY STORE (2026-11-12, per user: the Game window lists what was found - GOG, Steam, the EA
    /// app, a folder of one's own - with the save folder the port takes). Engine-free, from the folders the search walks
    /// (UWGogInstall.GetCandidateFolders) and any the player chose.
    ///
    /// The store is read from the files: game.gog is GOG's image, game.bin Steam's, game.bin in a folder DATA the EA
    /// app's (it keeps the image, DOSBox and UNDEROMn there). GOG Galaxy and GOG's offline installer cannot be told apart:
    /// their files are the same (goggame-galaxyFileList.ini in both) and the registry names only the last one installed;
    /// both are GOG.
    /// </summary>
    public static class UWInstallations
    {
        public enum StoreEnum
        {
            Gog,
            Steam,
            EaApp,
            Other
        }

        /// <summary>One installation of a game: the folder holding the image and the game's UNDEROMn.</summary>
        public sealed class Installation
        {
            public StoreEnum Store;

            public string Folder;

            public string Image;
        }

        /// <summary>The store an image comes from by its name and folder; Other for none.</summary>
        public static StoreEnum StoreOf(string psImage)
        {
            if (string.IsNullOrEmpty(psImage))
                return StoreEnum.Other;

            string lsName = Path.GetFileName(psImage);

            if (string.Equals(lsName, UWGogInstall.ImageFileName, StringComparison.OrdinalIgnoreCase))
                return StoreEnum.Gog;

            if (!string.Equals(lsName, UWGogInstall.SteamImageFileName, StringComparison.OrdinalIgnoreCase))
                return StoreEnum.Other;

            string lsFolder = Path.GetFileName(Path.GetDirectoryName(psImage) ?? string.Empty);

            return string.Equals(lsFolder, UWInstallRegistry.EaDataFolder, StringComparison.OrdinalIgnoreCase) ? StoreEnum.EaApp : StoreEnum.Steam;
        }

        /// <summary>
        /// The installations of pOGame among the folders, in their order, each once: a folder counts when it leads to an
        /// image (UWGogInstall.FindImage) whose folder holds the game's own save folder (UNDEROM1, UNDEROM2) - the
        /// installation of that game's product, where its saves are.
        /// </summary>
        public static List<Installation> Find(IUWGameProfile pOGame, IEnumerable<string> pOFolders)
        {
            List<Installation> lOFound = new List<Installation>();

            if (pOGame == null || pOFolders == null)
                return lOFound;

            foreach (string lsCandidate in pOFolders)
            {
                string lsImage = UWGogInstall.FindImage(lsCandidate);
                string lsFolder = lsImage == null ? null : Path.GetDirectoryName(lsImage);

                if (lsFolder == null || !fHasSaveFolder(lsFolder, pOGame) || IndexOf(lOFound, lsFolder) >= 0)
                    continue;

                lOFound.Add(new Installation { Store = StoreOf(lsImage), Folder = lsFolder, Image = lsImage });
            }

            return lOFound;
        }

        /// <summary>Where in the list an installation folder is, -1 when it is not there.</summary>
        public static int IndexOf(List<Installation> pOList, string psFolder)
        {
            if (pOList == null || string.IsNullOrEmpty(psFolder))
                return -1;

            for (int liAt = 0; liAt < pOList.Count; liAt++)
            {
                if (SameFolder(pOList[liAt].Folder, psFolder))
                    return liAt;
            }

            return -1;
        }

        /// <summary>Two paths name one folder: compared whole, without a trailing separator, ignoring case off Linux.</summary>
        public static bool SameFolder(string psOne, string psOther)
        {
            if (string.IsNullOrEmpty(psOne) || string.IsNullOrEmpty(psOther))
                return false;

            try
            {
                string lsOne = Path.GetFullPath(psOne).TrimEnd('\\', '/');
                string lsOther = Path.GetFullPath(psOther).TrimEnd('\\', '/');

                return string.Equals(lsOne, lsOther, fIsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The installation a save folder belongs to: UNDEROMn's parent, cloud_saves/UNDEROMn's parent's parent.
        /// Null for none.</summary>
        public static string InstallationOfSaves(string psSaveFolder)
        {
            if (string.IsNullOrEmpty(psSaveFolder))
                return null;

            try
            {
                DirectoryInfo lOParent = Directory.GetParent(psSaveFolder.TrimEnd('\\', '/'));

                if (lOParent != null && lOParent.Parent != null
                    && string.Equals(lOParent.Name, UWGogInstall.CloudSavesFolderName, StringComparison.OrdinalIgnoreCase))
                    lOParent = lOParent.Parent;

                return lOParent == null ? null : lOParent.FullName;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool fHasSaveFolder(string psFolder, IUWGameProfile pOGame)
        {
            try
            {
                return Directory.Exists(Path.Combine(psFolder, pOGame.SaveFolder));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool fIsLinux()
        {
            try
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
