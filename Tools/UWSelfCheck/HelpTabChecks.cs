using System;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 8.6 (2026-10-24): the help window's tabs became classes, each carrying its
    /// saved number (UWHelpTabId). settings.json keeps the open tab as that number (HelpTab), so
    /// the numbers are pinned here: Stats 0 to Controls 6, a fresh file on Spells, a number out of
    /// range back on Spells as the window's Awake did. Data-free.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckHelpTabIds()
        {
            Check.Group("Help tab ids (UWHelpTabId - cleanup step 8.6)");

            string[] lsNames = { "Stats", "Map", "Notes", "Spells", "Mantras", "Manual", "Controls" };

            Check.Int("ids: seven tabs", Enum.GetValues(typeof(UWHelpTabId)).Length, lsNames.Length);
            Check.Int("ids: the count", UWHelpTabIds.Count, lsNames.Length);

            for (int liAt = 0; liAt < lsNames.Length; liAt++)
                Check.Str("ids: saved number " + liAt, ((UWHelpTabId)liAt).ToString(), lsNames[liAt]);

            Check.Int("default: a fresh settings file opens the Spells tab", new UWUserData().HelpTab, (int)UWHelpTabId.Spells);
            Check.Int("default: the window's default is the file's", (int)UWHelpTabIds.Default, new UWUserData().HelpTab);

            for (int liSaved = 0; liSaved < UWHelpTabIds.Count; liSaved++)
                Check.Int("saved: " + liSaved + " read back", (int)UWHelpTabIds.FromSaved(liSaved), liSaved);

            Check.Int("saved: -1 keeps Spells", (int)UWHelpTabIds.FromSaved(-1), (int)UWHelpTabId.Spells);
            Check.Int("saved: 7 keeps Spells", (int)UWHelpTabIds.FromSaved(7), (int)UWHelpTabId.Spells);
            Check.Int("saved: 99 keeps Spells", (int)UWHelpTabIds.FromSaved(99), (int)UWHelpTabId.Spells);
        }
    }
}
