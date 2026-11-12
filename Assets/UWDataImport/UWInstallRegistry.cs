using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace UWDataImport.Install
{
    /// <summary>
    /// WHERE THE STORES SAY THEY PUT THE GAME (2026-11-12, per user: GOG Galaxy into its own library D:\GOG Galaxy and the
    /// EA app into D:\EA Games, neither found until then). Both write the folder into the registry, whatever folder the
    /// player chose:
    ///
    ///   GOG (Galaxy and the offline installer)   HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\1207662463 (the first game) and
    ///                                            ...\1207662473 (the second), value "path": the folder holding game.gog
    ///   EA app                                   HKLM\SOFTWARE\WOW6432Node\Electronic Arts\Ultima Underworld 1 (EA) and
    ///                                            ...\Ultima Underworld 2 (EA), value "Install Dir"; the image is
    ///                                            DATA\game.bin there, with the saves in DATA\UNDEROM1 (its DOSBox mounts
    ///                                            DATA as C:), so DATA is the folder to look in
    ///
    /// Read by RegGetValueW through P/Invoke: the engine-free library targets .NET Standard 2.1, which has no
    /// Microsoft.Win32.Registry. Windows only; anything that fails gives no folder. Steam is found by its library list
    /// (UWGogInstall.GetSteamLibraries), not here.
    /// </summary>
    public static class UWInstallRegistry
    {
        /// <summary>GOG's product ids of the two games.</summary>
        public static readonly string[] GogProductIds = { "1207662463", "1207662473" };

        /// <summary>The EA app's keys of the two games under Electronic Arts.</summary>
        public static readonly string[] EaKeys = { "Ultima Underworld 1 (EA)", "Ultima Underworld 2 (EA)" };

        /// <summary>The folder under the EA installation that holds the image and the saves.</summary>
        public const string EaDataFolder = "DATA";

        private static readonly UIntPtr HKEY_LOCAL_MACHINE = new UIntPtr(0x80000002u);

        /// <summary>RRF_RT_REG_SZ.</summary>
        private const uint RegistryStringsOnly = 0x00000002;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegGetValueW")]
        private static extern int fRegGetValue(UIntPtr phKey, string psSubKey, string psValue, uint piFlags, IntPtr pType,
            StringBuilder pOData, ref uint piSize);

        /// <summary>The folders the registry names for one game (piGame 0 the first, 1 the second): GOG's, then the EA
        /// app's DATA folder. Empty off Windows or without entries.</summary>
        public static List<string> FoldersOf(int piGame)
        {
            List<string> lOFolders = new List<string>();

            if (piGame < 0 || piGame > 1 || !fIsWindows())
                return lOFolders;

            fAdd(lOFolders, fRead(@"SOFTWARE\WOW6432Node\GOG.com\Games\" + GogProductIds[piGame], "path")
                ?? fRead(@"SOFTWARE\GOG.com\Games\" + GogProductIds[piGame], "path"), null);
            fAdd(lOFolders, fRead(@"SOFTWARE\WOW6432Node\Electronic Arts\" + EaKeys[piGame], "Install Dir")
                ?? fRead(@"SOFTWARE\Electronic Arts\" + EaKeys[piGame], "Install Dir"), EaDataFolder);

            return lOFolders;
        }

        private static void fAdd(List<string> pOFolders, string psFolder, string psBelow)
        {
            if (string.IsNullOrWhiteSpace(psFolder))
                return;

            string lsFolder = psFolder.Trim().TrimEnd('\\', '/');

            if (psBelow != null)
                lsFolder = Path.Combine(lsFolder, psBelow);

            if (!pOFolders.Contains(lsFolder))
                pOFolders.Add(lsFolder);
        }

        private static bool fIsWindows()
        {
            try
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A string value under HKEY_LOCAL_MACHINE, null when it is not there or cannot be read.</summary>
        private static string fRead(string psSubKey, string psValue)
        {
            try
            {
                uint liSize = 0;

                if (fRegGetValue(HKEY_LOCAL_MACHINE, psSubKey, psValue, RegistryStringsOnly, IntPtr.Zero, null, ref liSize) != 0 || liSize == 0)
                    return null;

                StringBuilder lOData = new StringBuilder((int)(liSize / 2) + 1);

                if (fRegGetValue(HKEY_LOCAL_MACHINE, psSubKey, psValue, RegistryStringsOnly, IntPtr.Zero, lOData, ref liSize) != 0)
                    return null;

                return lOData.ToString();
            }
            catch (Exception)
            {
                // No advapi32 (not Windows), no entry point, anything else: no folder.
                return null;
            }
        }
    }
}
