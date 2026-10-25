using System;
using System.Diagnostics;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// What a check group may ask for: the data folder, the loaded data (loaded once, by the
    /// "Load" group or on first use), a fresh world after a group has changed one, a level by
    /// index. Everything a group used to fetch for itself.
    /// </summary>
    public sealed class CheckContext
    {
        public string DataPath { get; }

        /// <summary>A folder of save games (SAVE1 ...) for the round-trip check, or null.</summary>
        public string SavesPath;

        /// <summary>--record: the pin checks write their files instead of comparing.</summary>
        public bool Record;

        private DataImport mOData;
        private bool mbLoadTried;
        private string msLoadError;

        public CheckContext(string psDataPath)
        {
            DataPath = psDataPath;
        }

        /// <summary>--root: the project root the checks of the source tree and the pins use,
        /// instead of the folder with Assets above the working directory.</summary>
        public static string RootOverride;

        /// <summary>The project root (the folder with Assets, Tools and Docs), or null when there
        /// is no source tree here - a published build.</summary>
        public static string ProjectRoot
        {
            get { return RootOverride ?? UWRuleIndexScan.FindProjectRoot(); }
        }

        /// <summary>The data, or null when the folder could not be loaded (or no folder was given).</summary>
        public DataImport Data
        {
            get
            {
                if (!mbLoadTried)
                    TryLoad(out _);

                return mOData;
            }
        }

        public bool HasData
        {
            get { return Data != null; }
        }

        /// <summary>Why the load failed, for the "Load" group's report.</summary>
        public string LoadError
        {
            get { return msLoadError; }
        }

        /// <summary>Loads once; later calls return the first result.</summary>
        public bool TryLoad(out long plMilliseconds)
        {
            plMilliseconds = 0;

            if (mbLoadTried)
                return mOData != null;

            mbLoadTried = true;

            if (string.IsNullOrEmpty(DataPath))
            {
                msLoadError = "no data folder";

                return false;
            }

            try
            {
                Stopwatch lOWatch = Stopwatch.StartNew();

                mOData = new DataImport(DataPath);
                lOWatch.Stop();

                // As the game does at every start: the loaded game is the current session, so the
                // statics that forward to it read and write this world's session.
                UWGameSession.MakeCurrent(mOData.Session);
                plMilliseconds = lOWatch.ElapsedMilliseconds;

                return true;
            }
            catch (Exception lOException)
            {
                msLoadError = lOException.Message;

                return false;
            }
        }

        /// <summary>
        /// Reloads the game state (LEV.ARK, PLAYER.DAT, BGLOBALS.DAT) in place, as starting a
        /// new game does. The runner calls it after every group that declares MutatesData, so a
        /// creature placed by one group is not counted by the next. Textures, strings and tables
        /// stay as loaded.
        /// </summary>
        public void FreshData()
        {
            if (mOData != null)
            {
                mOData.LoadGame(null);
                UWGameSession.MakeCurrent(mOData.Session);
            }
        }

        /// <summary>The level with that 0-based index, or null when the data or the level is missing.</summary>
        public UWLevel Level(int piIndex)
        {
            DataImport lOData = Data;

            return lOData != null && lOData.Levels != null && lOData.Levels.Count > piIndex && piIndex >= 0
                ? lOData.Levels[piIndex]
                : null;
        }
    }
}
