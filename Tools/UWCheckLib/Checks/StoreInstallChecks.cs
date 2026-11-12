using System.IO;
using UWDataImport;
using UWDataImport.Install;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// THE EA APP AND THE STORES' REGISTRY (2026-11-12, per user: the EA release in D:\EA Games, GOG Galaxy in a library
        /// of its own): the EA layout on a made-up folder - the image as DATA\game.bin with the saves in DATA\UNDEROM1, found
        /// from the installation folder and from DATA; and the folders the registry names come first in the search.
        /// </summary>
        private static void fCheckStoreInstalls()
        {
            Check.Group("Store installations (EA app, registry)");

            string lsRoot = Path.Combine(Path.GetTempPath(), "UWCheckEa_" + System.Guid.NewGuid().ToString("N"));
            string lsInstall = Path.Combine(lsRoot, "Ultima Underworld 1");
            string lsData = Path.Combine(lsInstall, UWInstallRegistry.EaDataFolder);
            IUWGameProfile lOUw1 = UWGameProfiles.Uw1;

            try
            {
                Directory.CreateDirectory(Path.Combine(lsData, lOUw1.SaveFolder, "SAVE1"));
                File.WriteAllBytes(Path.Combine(lsData, UWGogInstall.SteamImageFileName), new byte[0]);

                string lsFromInstall = UWGogInstall.FindImage(lsInstall);
                string lsFromData = UWGogInstall.FindImage(lsData);

                Check.Str("ea: DATA\\game.bin found from the installation folder and from DATA",
                    fRelative(lsRoot, lsFromInstall) + " " + fRelative(lsRoot, lsFromData),
                    "Ultima Underworld 1/DATA/game.bin Ultima Underworld 1/DATA/game.bin");
                Check.Str("ea: the saves in DATA\\UNDEROM1, where its DOSBox mounts DATA as C:",
                    fRelative(lsRoot, UWGogInstall.GetSavegameFolder(lsFromInstall, lOUw1)), "Ultima Underworld 1/DATA/UNDEROM1");

                // THE GAME WINDOW'S LIST (2026-11-12): a GOG, a Steam and the EA installation beside each other, found once
                // each from any folder that leads to them, by store; a folder without the game's UNDEROMn is none of the
                // game's; the installation a save folder belongs to, cloud_saves stepped over.
                string lsGog = Path.Combine(lsRoot, "Ultima Underworld");
                string lsSteam = Path.Combine(lsRoot, "steamapps", "common", "Ultima Underworld I");
                string lsBare = Path.Combine(lsRoot, "Bare");

                Directory.CreateDirectory(Path.Combine(lsGog, lOUw1.SaveFolder));
                File.WriteAllBytes(Path.Combine(lsGog, UWGogInstall.ImageFileName), new byte[0]);
                Directory.CreateDirectory(Path.Combine(lsSteam, lOUw1.SaveFolder));
                File.WriteAllBytes(Path.Combine(lsSteam, UWGogInstall.SteamImageFileName), new byte[0]);
                Directory.CreateDirectory(lsBare);
                File.WriteAllBytes(Path.Combine(lsBare, UWGogInstall.ImageFileName), new byte[0]);

                System.Collections.Generic.List<UWInstallations.Installation> lOFound = UWInstallations.Find(lOUw1, new[]
                {
                    lsGog, Path.Combine(lsGog, lOUw1.SaveFolder), lsInstall, lsData, lsSteam + "\\", lsBare, Path.Combine(lsRoot, "Nowhere"), null
                });
                string lsList = string.Join(" ", lOFound.ConvertAll(pOAt => pOAt.Store + ":" + fRelative(lsRoot, pOAt.Folder)));

                Check.Str("installations: GOG, the EA app, Steam once each by store, a bare image and a missing folder none",
                    lsList, "Gog:Ultima Underworld EaApp:Ultima Underworld 1/DATA Steam:steamapps/common/Ultima Underworld I");
                Check.Str("installations: none of the second game's (no UNDEROM2); the installation of a save folder, cloud_saves stepped over",
                    UWInstallations.Find(UWGameProfiles.Uw2, new[] { lsGog, lsInstall, lsSteam }).Count + " "
                    + fRelative(lsRoot, UWInstallations.InstallationOfSaves(Path.Combine(lsGog, UWGogInstall.CloudSavesFolderName, lOUw1.SaveFolder))) + " "
                    + fRelative(lsRoot, UWInstallations.InstallationOfSaves(Path.Combine(lsData, lOUw1.SaveFolder))) + " "
                    + fRelative(lsRoot, UWInstallations.InstallationOfSaves(null)),
                    "0 Ultima Underworld Ultima Underworld 1/DATA none");
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

            // Whatever this machine's registry holds: its folders stand before the usual ones of each game.
            string[] lsCandidates = UWGogInstall.GetCandidateFolders(false);
            int liUsualFirst = System.Array.IndexOf(lsCandidates, @"C:\GOG Games\Ultima Underworld");
            int liUsualSecond = System.Array.IndexOf(lsCandidates, @"C:\GOG Games\Ultima Underworld 2");
            bool lbOrder = liUsualFirst >= 0 && liUsualSecond > liUsualFirst;

            foreach (string lsFolder in UWInstallRegistry.FoldersOf(0))
                lbOrder &= System.Array.IndexOf(lsCandidates, lsFolder) >= 0 && System.Array.IndexOf(lsCandidates, lsFolder) < liUsualFirst;

            foreach (string lsFolder in UWInstallRegistry.FoldersOf(1))
            {
                int liAt = System.Array.IndexOf(lsCandidates, lsFolder);

                lbOrder &= liAt > liUsualFirst && liAt < liUsualSecond;
            }

            Check.Bool("registry: GOG's and the EA app's folders before the usual ones, each game's in its group", lbOrder, true);

            // What the Game window would list on this machine, with no folder chosen (the search's folders alone).
            foreach (IUWGameProfile lOGame in new[] { lOUw1, UWGameProfiles.Uw2 })
            {
                System.Collections.Generic.List<UWInstallations.Installation> lOHere = UWInstallations.Find(lOGame, lsCandidates);

                Check.Pass("installations here of " + lOGame.Game, lOHere.Count == 0 ? "none"
                    : string.Join(" | ", lOHere.ConvertAll(pOAt => pOAt.Store + " " + pOAt.Folder)));
            }
            System.Console.WriteLine("         (this machine's registry: " + string.Join(", ", UWInstallRegistry.FoldersOf(0)) + " | "
                + string.Join(", ", UWInstallRegistry.FoldersOf(1)) + ")");
        }
    }
}
