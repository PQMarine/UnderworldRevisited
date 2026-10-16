using System;
using System.Collections.Generic;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// One group of expectations: a name for the command line, tags to select groups by, whether
    /// it needs the game data at all, whether it changes the loaded world (the runner reloads the
    /// game state afterwards), and the body.
    ///
    /// Tags in use: env (depends on the installation, not on a rule), meta (the source tree),
    /// original (pins a number of UW.EXE; a mod validator skips these), and one per domain.
    /// </summary>
    public sealed class CheckEntry
    {
        public string Name;
        public string[] Tags = Array.Empty<string>();
        public bool NeedsData;
        public bool MutatesData;
        public Action<CheckContext> Run;

        public bool HasTag(string psTag)
        {
            foreach (string lsTag in Tags)
                if (string.Equals(lsTag, psTag, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }
    }

    /// <summary>An ordered list of groups; the order is the order they run in.</summary>
    public sealed class CheckCatalog
    {
        public readonly List<CheckEntry> Entries = new List<CheckEntry>();

        public CheckEntry Add(string psName, Action<CheckContext> pORun, bool pbNeedsData = false, params string[] psTags)
        {
            CheckEntry lOEntry = new CheckEntry { Name = psName, Run = pORun, NeedsData = pbNeedsData, Tags = psTags ?? Array.Empty<string>() };

            Entries.Add(lOEntry);

            return lOEntry;
        }

        public CheckEntry Find(string psName)
        {
            foreach (CheckEntry lOEntry in Entries)
                if (string.Equals(lOEntry.Name, psName, StringComparison.OrdinalIgnoreCase))
                    return lOEntry;

            return null;
        }
    }
}
