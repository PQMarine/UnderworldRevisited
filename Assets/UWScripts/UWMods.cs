using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE MOD FOLDER ON THE UNITY SIDE (Docs/WIDESCREEN-WEAPONS-PLAN.md step 1, built 2026-11-09): the folder
/// "Mods" beside settings.json (UWUserFolder: beside the program when portable) - one for both games, for the Steam and the GOG game alike, untouched by
/// unpacking a new release (decided by the user 2026-11-07) - and its catalog (UWModCatalog), scanned
/// anew at each game start (UWLevelLoader) and when the Game window shows its line. Active when
/// installed: until the Mods window there is no switch; to compare with the original, take a mod out of
/// the folder. Read: the pictures (ApplyPictures) and the editor's .uwmod packages (PinGameFiles,
/// BuildGameFiles - their deltas applied to a copy of the game's files, which the game then reads).
/// </summary>
public static class UWMods
{
    /// <summary>The folder's name beside settings.json.</summary>
    public const string FolderName = "Mods";

    public static string Folder => UWUserFolder.Combine(FolderName);

    private static UWModCatalog msCatalog;

    private static float msfScanned = -100f;

    /// <summary>The catalog as last scanned (scanned now if never).</summary>
    public static UWModCatalog Catalog => msCatalog ?? Rescan();

    /// <summary>Scans the folder anew.</summary>
    public static UWModCatalog Rescan()
    {
        msCatalog = UWModCatalog.Scan(Folder);
        msfScanned = Time.unscaledTime;

        return msCatalog;
    }

    /// <summary>The catalog, scanned anew at most every two seconds (the Game window asks each frame).</summary>
    public static UWModCatalog Fresh()
    {
        return msCatalog == null || Time.unscaledTime - msfScanned > 2f ? Rescan() : msCatalog;
    }

    /// <summary>Scans and writes the catalog to the log: a line per mod, a warning per mod left out.</summary>
    public static UWModCatalog ScanAndLog()
    {
        UWModCatalog lOCatalog = Rescan();

        Debug.Log("[Mods] " + (lOCatalog.Loaded.Count == 0 ? "none" : lOCatalog.Loaded.Count + " loaded") + " in " + Folder);

        foreach (UWModCatalog.Entry lOEntry in lOCatalog.Loaded)
            Debug.Log("[Mods] " + lOEntry);

        foreach (string lsRefused in lOCatalog.Refused)
            Debug.LogWarning("[Mods] not loaded: " + lsRefused);

        foreach (string lsWarning in lOCatalog.Warnings)
            Debug.LogWarning("[Mods] " + lsWarning);

        return lOCatalog;
    }

    /// <summary>
    /// THE MODS' PICTURES (Docs/WIDESCREEN-WEAPONS-PLAN.md step 3, 2026-11-09): the catalog's mods for this game
    /// handed to the textures as their extended pictures (UWPictureExtensions), composed at once, with a log line
    /// of how many and by whom and a warning for each picture that cannot be used. Kept while the same mods are
    /// in force; made anew at every load where a mod lies in a folder (an artist sees a saved picture at the
    /// next load) or the set changed. After ScanAndLog, at each game start.
    /// </summary>
    public static void ApplyPictures(UWTextures pOTextures)
    {
        if (pOTextures == null)
            return;

        Pictures = pOTextures.Extensions;

        UWModSet lOSet = Catalog.SetFor(pOTextures.Game == UWGame.Uw2 ? "uw2" : "uw1");
        UWPictureExtensions lOOld = pOTextures.Extensions;

        if (lOOld != null && lOOld.Key == lOSet.Key && !lOOld.FromFolder)
            return;

        UWPictureExtensions lONew = lOSet.Mods.Count > 0 ? pOTextures.MakeExtensions(lOSet) : null;

        if (lONew != null && lONew.Count == 0)
            lONew = null;

        pOTextures.Extensions = lONew;
        Pictures = lONew;

        if (lONew == null)
            return;

        int liMade = lONew.ComposeAll();

        Debug.Log("[Mods] pictures: " + liMade + " made from " + lONew.Count + " (" + string.Join(", ", lONew.Credits) + ")");

        foreach (string lsProblem in lONew.Problems)
            Debug.LogWarning("[Mods] picture not used: " + lsProblem);
    }

    // ------------------------------------------------- The editor's packages (.uwmod)

    /// <summary>The .uwmod packages in force per game ("uw1", "uw2"), fixed at the game's start (PinGameFiles), so a
    /// mod dropped into the folder during play does not change the files under the running game.</summary>
    private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<UWModCatalog.Entry>> msPinned =
        new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<UWModCatalog.Entry>>();

    private static readonly System.Collections.Generic.Dictionary<string, string> msPinnedKey = new System.Collections.Generic.Dictionary<string, string>();

    /// <summary>The last result of applying them per game, for the log line, the Game window and the debug display.</summary>
    private static readonly System.Collections.Generic.Dictionary<string, UWModGameFiles> msGameFiles = new System.Collections.Generic.Dictionary<string, UWModGameFiles>();

    /// <summary>"uw1" or "uw2" - the catalog's and format 2's name of a game.</summary>
    public static string GameId(IUWGameProfile pOGame)
    {
        return pOGame != null && pOGame.Game == UWGame.Uw2 ? "uw2" : "uw1";
    }

    /// <summary>
    /// Fixes the .uwmod packages of the catalog (as last scanned) for a game: at each game start, before the data
    /// is loaded (UWLevelLoader). UWSettings.DataPath asks for them by PinnedKey and applies them (BuildGameFiles).
    /// </summary>
    public static void PinGameFiles(IUWGameProfile pOGame)
    {
        string lsGame = GameId(pOGame);
        System.Collections.Generic.List<UWModCatalog.Entry> lOPackages = new System.Collections.Generic.List<UWModCatalog.Entry>();
        System.Text.StringBuilder lOKey = new System.Text.StringBuilder();

        foreach (UWModCatalog.Entry lOEntry in Catalog.SetFor(lsGame).Mods)
        {
            if (!lOEntry.Manifest.IsEditorPackage)
                continue;

            lOPackages.Add(lOEntry);

            byte[] lyManifest = lOEntry.Source.ReadAllBytes(UWModManifest.FileName);

            lOKey.Append(lOEntry.Id).Append('@').Append(lOEntry.Manifest.Version ?? "?").Append(':')
                .Append(lyManifest != null ? UWModGameFiles.Sha256(lyManifest) : "?").Append(';');
        }

        msPinned[lsGame] = lOPackages;
        msPinnedKey[lsGame] = lOKey.ToString();
    }

    /// <summary>The key of the pinned packages of a game (empty for none) - part of UWSettings' data path key.</summary>
    public static string PinnedKey(IUWGameProfile pOGame)
    {
        return msPinnedKey.TryGetValue(GameId(pOGame), out string lsKey) ? lsKey : string.Empty;
    }

    /// <summary>
    /// The game folder to read: psExtraction (GameData/UW1) where no package is pinned or none applies, else the
    /// copy with them applied (GameData/UW1-mods). Logs what was applied and what not, and why.
    /// </summary>
    public static string BuildGameFiles(string psExtraction, IUWGameProfile pOGame)
    {
        string lsGame = GameId(pOGame);

        if (!msPinned.TryGetValue(lsGame, out System.Collections.Generic.List<UWModCatalog.Entry> lOPackages) || lOPackages.Count == 0)
        {
            msGameFiles.Remove(lsGame);

            return psExtraction;
        }

        UWModGameFiles lOResult = UWModGameFiles.Build(psExtraction, psExtraction.TrimEnd('/', '\\') + "-mods", lOPackages, lsGame);

        msGameFiles[lsGame] = lOResult;

        if (lOResult.Applied.Count > 0)
            Debug.Log("[Mods] game files: " + string.Join(", ", lOResult.Applied) + " applied - " + string.Join(", ", lOResult.Changed) + " in " + lOResult.Root);

        foreach (string lsRefused in lOResult.Refused)
            Debug.LogWarning("[Mods] not applied: " + lsRefused);

        if (lOResult.Problem != null)
            Debug.LogWarning("[Mods] " + lOResult.Problem);

        return lOResult.Root;
    }

    /// <summary>The record of the packages applied to a game's files now (UWModSaveRecord), for a save's UWR.json.</summary>
    public static string[] SaveRecord(IUWGameProfile pOGame)
    {
        System.Collections.Generic.List<string> lsLines = new System.Collections.Generic.List<string>();
        UWModGameFiles lOFiles = GameFiles(pOGame);

        if (lOFiles == null || !msPinned.TryGetValue(GameId(pOGame), out System.Collections.Generic.List<UWModCatalog.Entry> lOPackages))
            return lsLines.ToArray();

        foreach (UWModCatalog.Entry lOEntry in lOPackages)
        {
            if (lOFiles.Applied.Contains(lOEntry.Id + " " + (lOEntry.Manifest.Version ?? "?")))
                lsLines.Add(UWModSaveRecord.Line(lOEntry.Id, lOEntry.Manifest.Version ?? "?", lOEntry.Manifest.Name));
        }

        return lsLines.ToArray();
    }

    /// <summary>
    /// What to tell the player about a loaded save's mods (UWModSaveRecord.Compare): null where the save keeps no record
    /// (made before 0.19.0, or saved by the DOS game) or nothing differs, else the lines for the message window.
    /// </summary>
    public static System.Collections.Generic.List<string> SaveDifference(string[] psSaved, IUWGameProfile pOGame)
    {
        if (psSaved == null)
            return null;

        System.Collections.Generic.List<UWModSaveRecord.Package> lOSaved = UWModSaveRecord.Parse(psSaved);
        System.Collections.Generic.List<UWModSaveRecord.Package> lONow = UWModSaveRecord.Parse(SaveRecord(pOGame));

        if (!UWModSaveRecord.Compare(lOSaved, lONow, out System.Collections.Generic.List<UWModSaveRecord.Package> lOMissing,
                out System.Collections.Generic.List<UWModSaveRecord.Package> lONew))
            return null;

        System.Collections.Generic.List<string> lsLines = new System.Collections.Generic.List<string> { UWText.T("msg.mods.save_differs") };

        if (lOMissing.Count > 0)
            lsLines.Add(UWText.F("msg.mods.save_missing", string.Join(", ", lOMissing.ConvertAll(pOPackage => pOPackage.Shown))));

        if (lONew.Count > 0)
            lsLines.Add(UWText.F("msg.mods.save_new", string.Join(", ", lONew.ConvertAll(pOPackage => pOPackage.Shown))));

        foreach (string lsLine in lsLines)
            Debug.Log("[Mods] " + lsLine);

        return lsLines;
    }

    /// <summary>The last result of applying a game's packages, null where none were pinned.</summary>
    public static UWModGameFiles GameFiles(IUWGameProfile pOGame)
    {
        return msGameFiles.TryGetValue(GameId(pOGame), out UWModGameFiles lOResult) ? lOResult : null;
    }

    /// <summary>The pictures in force (ApplyPictures), null for none - for their credit in the debug display.</summary>
    public static UWPictureExtensions Pictures { get; private set; }
}
