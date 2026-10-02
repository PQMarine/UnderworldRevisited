using System;
using System.Collections.Generic;
using System.IO;

namespace UWDataImport
{
    /// <summary>
    /// The logic of the game folder dialog, engine-free (2026-09-17): the installations found in
    /// the usual folders, the current folder with its subfolders (or the drives), and whether
    /// the current folder can be used. The host only draws it (Unity: UWSetupMenu).
    /// </summary>
    public class UWGameFolderBrowser
    {
        public const string ImageFileName = "game.gog";

        /// <summary>Installations with a game.gog among the candidates, in their order.</summary>
        public List<string> Found { get; } = new List<string>();

        /// <summary>The folder being looked at; empty means the list of drives.</summary>
        public string CurrentPath { get; private set; } = string.Empty;

        /// <summary>Subfolders of CurrentPath, or the drive roots.</summary>
        public List<string> Entries { get; } = new List<string>();

        public string Status { get; private set; } = string.Empty;

        public bool StatusIsError { get; private set; }

        public bool IsDriveList => string.IsNullOrEmpty(CurrentPath);

        public bool CanUseCurrent => HasImage(CurrentPath);

        /// <summary>Looks through the candidates and starts at the remembered folder, the first
        /// installation found, or the drives.</summary>
        public void Open(IEnumerable<string> pOCandidates, string psRemembered)
        {
            Found.Clear();

            if (pOCandidates != null)
            {
                foreach (string lsCandidate in pOCandidates)
                {
                    if (HasImage(lsCandidate))
                        Found.Add(lsCandidate);
                }
            }

            if (!string.IsNullOrEmpty(psRemembered) && Directory.Exists(psRemembered))
                Browse(psRemembered);
            else
                Browse(Found.Count > 0 ? Found[0] : string.Empty);

            if (Found.Count == 0 && string.IsNullOrEmpty(psRemembered))
                SetStatus("No GOG installation was found in the usual folders. Please choose it.", false);
        }

        /// <summary>Goes to a folder; an empty or missing path shows the drives.</summary>
        public void Browse(string psPath)
        {
            Entries.Clear();

            if (string.IsNullOrEmpty(psPath) || !Directory.Exists(psPath))
            {
                CurrentPath = string.Empty;

                Entries.AddRange(GetDrives());

                fUpdateStatus();

                return;
            }

            CurrentPath = Path.GetFullPath(psPath);

            try
            {
                string[] lsFolders = Directory.GetDirectories(CurrentPath);

                Array.Sort(lsFolders, StringComparer.OrdinalIgnoreCase);

                foreach (string lsFolder in lsFolders)
                {
                    if (fIsVisible(lsFolder))
                        Entries.Add(lsFolder);
                }
            }
            catch (Exception lOError)
            {
                SetStatus("This folder cannot be read: " + lOError.Message, true);

                return;
            }

            fUpdateStatus();
        }

        /// <summary>One folder up; from a drive root to the list of drives. Trimming the backslash
        /// off "C:\" left "C:", which Windows reads as the current folder of that drive - so Up
        /// never reached the drives (per user, 2026-09-17).</summary>
        public void Up()
        {
            if (IsDriveList)
                return;

            DirectoryInfo lOParent = new DirectoryInfo(CurrentPath).Parent;

            Browse(lOParent == null ? string.Empty : lOParent.FullName);
        }

        /// <summary>The ready drives, for the drive list and a row of drive buttons.</summary>
        public List<string> GetDrives()
        {
            List<string> lODrives = new List<string>();

            try
            {
                foreach (DriveInfo lODrive in DriveInfo.GetDrives())
                {
                    if (lODrive.IsReady)
                        lODrives.Add(lODrive.RootDirectory.FullName);
                }
            }
            catch (Exception)
            {
                // None to show.
            }

            return lODrives;
        }

        /// <summary>The name to show for an entry: the whole root for a drive, else the folder
        /// name, marked when it holds the image.</summary>
        public string GetDisplayName(string psEntry)
        {
            string lsName = IsDriveList ? psEntry : Path.GetFileName(psEntry);

            return HasImage(psEntry) ? lsName + "    (" + ImageFileName + ")" : lsName;
        }

        public void SetStatus(string psText, bool pbError)
        {
            Status = psText ?? string.Empty;
            StatusIsError = pbError;
        }

        public static bool HasImage(string psFolder)
        {
            try
            {
                return !string.IsNullOrEmpty(psFolder) && File.Exists(Path.Combine(psFolder, ImageFileName));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void fUpdateStatus()
        {
            if (CanUseCurrent)
                SetStatus(ImageFileName + " found - this folder can be used.", false);
            else if (!IsDriveList)
                SetStatus("No " + ImageFileName + " in this folder.", false);
            else
                SetStatus("Choose the drive and folder of your GOG installation.", false);
        }

        /// <summary>No hidden or system folders, and none starting with $ or a dot.</summary>
        private static bool fIsVisible(string psFolder)
        {
            string lsName = Path.GetFileName(psFolder);

            if (lsName.StartsWith("$") || lsName.StartsWith("."))
                return false;

            try
            {
                return (File.GetAttributes(psFolder) & (FileAttributes.Hidden | FileAttributes.System)) == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
