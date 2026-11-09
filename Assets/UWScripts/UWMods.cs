using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE MOD FOLDER ON THE UNITY SIDE (Docs/WIDESCREEN-WEAPONS-PLAN.md step 1, built 2026-11-09): the folder
/// "Mods" beside settings.json (UWUserFolder: beside the program when portable) - one for both games, for the Steam and the GOG game alike, untouched by
/// unpacking a new release (decided by the user 2026-11-07) - and its catalog (UWModCatalog), scanned
/// anew at each game start (UWLevelLoader) and when the Game window shows its line. Active when
/// installed: until the Mods window there is no switch; to compare with the original, take a mod out of
/// the folder. Nothing reads the mods yet - the pictures (step 2) and the editor's packages come next.
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

    /// <summary>One line for the debug display and the Game window: how many loaded, how many not.</summary>
    public static string Summary(UWModCatalog pOCatalog)
    {
        if (pOCatalog == null)
            return string.Empty;

        string lsLine = pOCatalog.Loaded.Count == 0 ? UWText.T("setup.game.mods_none") : UWText.F("setup.game.mods", pOCatalog.Loaded.Count);

        return pOCatalog.Refused.Count > 0 ? lsLine + " " + UWText.F("setup.game.mods_refused", pOCatalog.Refused.Count) : lsLine;
    }
}
