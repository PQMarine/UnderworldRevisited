using System;
using System.Collections.Generic;
using System.IO;

namespace UWDataImport.Install
{
    /// <summary>
    /// THE GAME'S PDFS FOR THE HELP'S MANUAL TAB (per user, 2026-11-05: the Steam version brings its extras along,
    /// show them in the help): the manual, the reference card, the map, the first game's Memoirs of Cabirus, the
    /// second's A Safe Passage Through Britannia and the cluebook, each of the running game. GOG lays its manual
    /// beside game.gog (Ultima_Underworld-Manual.pdf, Ultima_Underworld_II-Manual.pdf), Steam all of them in its
    /// Extras folder (Ultima_Underworld_I_Manual.pdf, Ultima_Underworld_II_Cluebook.pdf, ...); GOG's goodies
    /// downloaded into the installation are found the same way. The kind and the game are read from the file's
    /// name. Engine-free; UWManual opens what is found.
    /// </summary>
    public static class UWGameDocuments
    {
        public enum KindEnum
        {
            Manual,
            ReferenceCard,
            Map,
            Memoirs,
            SafePassage,
            Cluebook
        }

        /// <summary>The words of a file name that tell its kind, in the order the help shows the kinds.</summary>
        private static readonly (KindEnum Kind, string Word)[] msWords =
        {
            (KindEnum.Manual, "manual"),
            (KindEnum.ReferenceCard, "reference"),
            (KindEnum.Map, "map"),
            (KindEnum.Memoirs, "memoirs"),
            (KindEnum.SafePassage, "safe_passage"),
            (KindEnum.Cluebook, "cluebook"),
        };

        /// <summary>The first game's manual as GOG names it, preferred among its manuals.</summary>
        public const string GogManualName = "Ultima_Underworld-Manual.pdf";

        /// <summary>Where a Steam installation keeps the PDFs.</summary>
        public const string ExtrasFolder = "Extras";

        /// <summary>One document found: its kind and file.</summary>
        public readonly struct Document
        {
            public readonly KindEnum Kind;

            public readonly string Path;

            public Document(KindEnum peKind, string psPath)
            {
                Kind = peKind;
                Path = psPath;
            }

            /// <summary>The cluebook holds the solutions: the help warns before it opens it the first time.</summary>
            public bool IsSpoiler => Kind == KindEnum.Cluebook;
        }

        /// <summary>
        /// A PDF's kind and game by its name, false for none of ours. The second game's names carry "Underworld_II"
        /// or "Underworld_2" (spaces or hyphens as well); every other is the first's. A name with "map" counts only
        /// when no other kind's word is in it.
        /// </summary>
        public static bool Classify(string psFileName, out KindEnum peKind, out int piGame)
        {
            peKind = KindEnum.Manual;
            piGame = 1;

            if (string.IsNullOrEmpty(psFileName) || !psFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return false;

            string lsName = psFileName.ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

            if (lsName.IndexOf("underworld", StringComparison.Ordinal) < 0)
                return false;

            piGame = lsName.Contains("underworld_ii") || lsName.Contains("underworld_2") ? 2 : 1;

            foreach ((KindEnum leKind, string lsWord) in msWords)
            {
                if (leKind == KindEnum.Map || !lsName.Contains(lsWord))
                    continue;

                peKind = leKind;

                return true;
            }

            if (lsName.Contains("_map"))
            {
                peKind = KindEnum.Map;

                return true;
            }

            return false;
        }

        /// <summary>
        /// The game's documents in the folders given (each folder's Extras looked in right after it, then its other
        /// subfolders: GOG's goodies come as ZIPs whose PDFs lie in a folder of their own, such as
        /// Ultima_Underworld_1_2_cluebook, and are found where they were unpacked), one of each kind - the first
        /// found, the folders in their order; among the first game's manuals GOG's name first.
        /// In the help's order: manual, reference card, map, the game's own book, cluebook.
        /// </summary>
        public static List<Document> Find(IEnumerable<string> pOFolders, int piGame)
        {
            Dictionary<KindEnum, string> lOFound = new Dictionary<KindEnum, string>();
            List<string> lOFolders = new List<string>();

            if (pOFolders != null)
            {
                foreach (string lsFolder in pOFolders)
                {
                    if (string.IsNullOrEmpty(lsFolder))
                        continue;

                    lOFolders.Add(lsFolder);
                    lOFolders.Add(System.IO.Path.Combine(lsFolder, ExtrasFolder));

                    try
                    {
                        if (Directory.Exists(lsFolder))
                        {
                            string[] lsSubfolders = Directory.GetDirectories(lsFolder);

                            Array.Sort(lsSubfolders, StringComparer.OrdinalIgnoreCase);
                            lOFolders.AddRange(lsSubfolders);
                        }
                    }
                    catch (Exception)
                    {
                        // Subfolders that cannot be listed are left out.
                    }
                }
            }

            foreach (string lsFolder in lOFolders)
            {
                string[] lsFiles;

                try
                {
                    if (!Directory.Exists(lsFolder))
                        continue;

                    lsFiles = Directory.GetFiles(lsFolder);
                }
                catch (Exception)
                {
                    // A folder that cannot be read holds nothing.
                    continue;
                }

                Array.Sort(lsFiles, StringComparer.OrdinalIgnoreCase);

                foreach (string lsFile in lsFiles)
                {
                    string lsName = System.IO.Path.GetFileName(lsFile);

                    if (!Classify(lsName, out KindEnum leKind, out int liGame) || liGame != piGame)
                        continue;

                    bool lbGogManual = leKind == KindEnum.Manual && string.Equals(lsName, GogManualName, StringComparison.OrdinalIgnoreCase);

                    if (!lOFound.ContainsKey(leKind) || (lbGogManual && !string.Equals(System.IO.Path.GetFileName(lOFound[leKind]), GogManualName,
                        StringComparison.OrdinalIgnoreCase) && fSameFolder(lOFound[leKind], lsFile)))
                        lOFound[leKind] = lsFile;
                }
            }

            List<Document> lODocuments = new List<Document>();

            foreach ((KindEnum leKind, string _) in msWords)
            {
                if (lOFound.TryGetValue(leKind, out string lsPath))
                    lODocuments.Add(new Document(leKind, lsPath));
            }

            return lODocuments;
        }

        private static bool fSameFolder(string psA, string psB)
        {
            return string.Equals(System.IO.Path.GetDirectoryName(psA), System.IO.Path.GetDirectoryName(psB), StringComparison.OrdinalIgnoreCase);
        }
    }
}
