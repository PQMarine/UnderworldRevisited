using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>-mods for one start (UWModFolderArgument, 2026-11-16): the folder a tool names instead of the user folder's Mods.</summary>
    public static partial class SelfChecks
    {
        private static void fCheckModFolderArgument()
        {
            Check.Group("-mods: another Mods folder for one start");

            string lsWork = System.IO.Path.GetFullPath("C:/Games/UR");

            Check.Str("none: the normal folder", UWModFolderArgument.Find(new[] { "UR.exe", "-portable" }, lsWork) ?? "null", "null");
            Check.Str("-mods <folder>, absolute", UWModFolderArgument.Find(new[] { "UR.exe", "-mods", "D:/Test/Mods" }, lsWork),
                System.IO.Path.GetFullPath("D:/Test/Mods"));
            Check.Str("-mods=<folder>, relative to the working directory", UWModFolderArgument.Find(new[] { "UR.exe", "-MODS=TryMods" }, lsWork),
                System.IO.Path.GetFullPath("C:/Games/UR/TryMods"));
            Check.Str("-mods without a folder (the next is an option): the normal folder",
                UWModFolderArgument.Find(new[] { "UR.exe", "-mods", "-portable" }, lsWork) ?? "null", "null");
        }
    }
}
