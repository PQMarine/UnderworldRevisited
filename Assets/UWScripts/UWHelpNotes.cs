using System.IO;
using UnityEngine;

/// <summary>
/// What the help window keeps with a save game (decided per user, 2026-09-24): a readable file
/// of our own in the save folder, SAVEn/UWR.json, which the original never reads. It holds the
/// player's free notes (the help's third tab) and, since 2026-10-03, the modern action bar
/// (UWModernActionBar) and the modern bags' open containers with their numbers (UWModernBags) -
/// each piece as its path in the inventory (UWInventoryPaths) and its object number, taken at
/// saving.
///
/// ROBUST AGAINST A SAVE IT HAS NOT SEEN (per user, 2026-10-03: saved over with the original,
/// anything may have changed): the file keeps a stamp of the PLAYER.DAT written with it. When the
/// save's PLAYER.DAT is another one (IsStale), or an entry's piece is not the one recorded, the
/// entry is looked for by its object number among the carried things and kept only when exactly
/// one fits (UWInventoryPaths.ResolveChecked); anything unreadable is dropped. The notes stay.
/// Known limit, accepted then: saving over the slot in the original leaves our file at its old
/// state.
///
/// Loaded with a save game (UWLevelLoader), written after every successful save
/// (UWSavegameWriter); a new character starts with empty notes.
/// </summary>
public static class UWHelpNotes
{
    public const string FileName = "UWR.json";

    /// <summary>The notes as the player wrote them.</summary>
    public static string Notes { get; set; } = string.Empty;

    /// <summary>The action bar as the save game holds it - one path per slot, empty for an empty
    /// slot (UWModernActionBar.CaptureSlots, resolved after loading).</summary>
    public static string[] ActionBarPaths { get; set; } = new string[0];

    /// <summary>The open bags as the save game holds them, in the order they opened, and their
    /// number badges.</summary>
    public static string[] OpenBagPaths { get; set; } = new string[0];

    public static int[] OpenBagNumbers { get; set; } = new int[0];

    /// <summary>The object number of each entry, -1 where none was recorded (older files).</summary>
    public static int[] ActionBarIds { get; set; } = new int[0];

    public static int[] OpenBagIds { get; set; } = new int[0];

    /// <summary>The save game's PLAYER.DAT is not the one this file was written with - the
    /// original (or something else) saved over the slot since.</summary>
    public static bool IsStale { get; private set; }

    private const string PlayerFile = "PLAYER.DAT";

    /// <summary>Counts the loads and clears, so the action bar knows when to read the paths again.</summary>
    public static int LoadCount { get; private set; }

    [System.Serializable]
    private class Data
    {
        public string Notes = string.Empty;

        public string[] ActionBar = new string[0];

        public string[] OpenBags = new string[0];

        public int[] OpenBagNumbers = new int[0];

        public int[] ActionBarIds = new int[0];

        public int[] OpenBagIds = new int[0];

        /// <summary>Length and FNV-1a hash of the PLAYER.DAT written with this file.</summary>
        public string SaveStamp = string.Empty;
    }

    /// <summary>The empty state - also what a broken file leaves.</summary>
    private static void fReset()
    {
        Notes = string.Empty;
        ActionBarPaths = new string[0];
        OpenBagPaths = new string[0];
        OpenBagNumbers = new int[0];
        ActionBarIds = new int[0];
        OpenBagIds = new int[0];
        IsStale = false;
    }

    /// <summary>A fingerprint of a save folder's PLAYER.DAT, empty when it cannot be read.</summary>
    private static string fStamp(string psFolder)
    {
        try
        {
            byte[] lyData = File.ReadAllBytes(Path.Combine(psFolder, PlayerFile));
            ulong luHash = 14695981039346656037UL;

            foreach (byte lyByte in lyData)
            {
                luHash ^= lyByte;
                luHash *= 1099511628211UL;
            }

            return lyData.Length + ":" + luHash.ToString("X16");
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Reads the file of a save folder; a folder without one gives empty notes.</summary>
    public static void LoadFrom(string psFolder)
    {
        fReset();
        LoadCount++;

        if (string.IsNullOrEmpty(psFolder))
            return;

        string lsPath = Path.Combine(psFolder, FileName);

        if (!File.Exists(lsPath))
            return;

        try
        {
            Data lOData = JsonUtility.FromJson<Data>(File.ReadAllText(lsPath));

            if (lOData == null)
                return;

            Notes = lOData.Notes ?? string.Empty;
            ActionBarPaths = lOData.ActionBar ?? new string[0];
            OpenBagPaths = lOData.OpenBags ?? new string[0];
            OpenBagNumbers = lOData.OpenBagNumbers ?? new int[0];
            ActionBarIds = lOData.ActionBarIds ?? new int[0];
            OpenBagIds = lOData.OpenBagIds ?? new int[0];
            IsStale = string.IsNullOrEmpty(lOData.SaveStamp) || lOData.SaveStamp != fStamp(psFolder);

            if (IsStale)
                Debug.Log("[Help] " + lsPath + " is older than the save game: its action bar and open bags are checked piece by piece.");
        }
        catch (System.Exception lOError)
        {
            // A broken file: the notes it may have held are lost, the game goes on without them.
            fReset();
            Debug.LogWarning("[Help] " + lsPath + " could not be read: " + lOError.Message);
        }
    }

    public static void SaveTo(string psFolder)
    {
        if (string.IsNullOrEmpty(psFolder) || !Directory.Exists(psFolder))
            return;

        // The bar and the open bags as they stand now - their pieces' places at this moment.
        UWModernActionBar.CaptureSlots();
        UWModernBags.CaptureOpenBags();

        try
        {
            File.WriteAllText(Path.Combine(psFolder, FileName), JsonUtility.ToJson(new Data
            {
                Notes = Notes ?? string.Empty,
                ActionBar = ActionBarPaths ?? new string[0],
                OpenBags = OpenBagPaths ?? new string[0],
                OpenBagNumbers = OpenBagNumbers ?? new int[0],
                ActionBarIds = ActionBarIds ?? new int[0],
                OpenBagIds = OpenBagIds ?? new int[0],
                SaveStamp = fStamp(psFolder)
            }, true));

            // What is written is what the save holds now.
            IsStale = false;
        }
        catch (System.Exception lOError)
        {
            Debug.LogWarning("[Help] notes could not be saved: " + lOError.Message);
        }
    }

    public static void Clear()
    {
        fReset();
        LoadCount++;
    }

    /// <summary>The recorded object number of entry piAt of an array, -1 if there is none.</summary>
    public static int IdAt(int[] piIds, int piAt)
    {
        return piIds != null && piAt >= 0 && piAt < piIds.Length ? piIds[piAt] : -1;
    }
}
