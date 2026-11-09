using System;
using System.IO;
using UnityEngine;

/// <summary>
/// WHERE THE PORT KEEPS ITS OWN FILES (per user, 2026-11-09: many would like a "portable" version with
/// everything in one folder): settings.json, graphics.json, the extracted game data (GameData), the
/// game's folders (UW1, UW2: rules.json, Music), the MT-32 ROMs and the mods.
///
///   - NORMALLY Unity's persistentDataPath (Windows: AppData\LocalLow\Underworld Revisited\...).
///   - PORTABLE: the folder UserData beside the program, when a file portable.txt or a folder UserData lies
///     beside it, or the game was started with -portable (a marker file, as Dolphin's portable.txt and
///     PCSX2's portable.ini, so a double click keeps it). Where that folder cannot be written (a copy under
///     Program Files) the normal folder is used, with a warning in the log.
///   - Never in the editor: the project is no program folder.
///
/// What stays outside either way: the saves, in the game's installation (UNDEROM1, UNDEROM2), so they stay
/// those of the DOS game; and Unity's Player.log, which Unity opens before any of our code runs.
/// Paths the player chose (the installations, the MT-32 ROMs) are kept relative to the program while
/// portable and on the same drive, so a stick with the game on it can change its letter (StorePath,
/// ResolvePath).
/// </summary>
public static class UWUserFolder
{
    public const string MarkerName = "portable.txt";

    public const string PortableFolderName = "UserData";

    public const string PortableArgument = "-portable";

    private static string msPath;

    /// <summary>The folder the port's own files go to.</summary>
    public static string Path
    {
        get
        {
            if (msPath == null)
                fResolve();

            return msPath;
        }
    }

    /// <summary>The files lie beside the program (UserData).</summary>
    public static bool IsPortable { get; private set; }

    /// <summary>Why the portable folder could not be used, null where it could or was not asked for.</summary>
    public static string Problem { get; private set; }

    /// <summary>The folder the program lies in; null in the editor.</summary>
    public static string ProgramFolder => Application.isEditor ? null : System.IO.Path.GetDirectoryName(Application.dataPath);

    /// <summary>The portable folder beside the program; null in the editor.</summary>
    public static string PortableFolder => ProgramFolder != null ? System.IO.Path.Combine(ProgramFolder, PortableFolderName) : null;

    /// <summary>Asked for: the marker file, the folder, or the start argument.</summary>
    public static bool WantsPortable
    {
        get
        {
            string lsProgram = ProgramFolder;

            if (lsProgram == null)
                return false;

            if (File.Exists(System.IO.Path.Combine(lsProgram, MarkerName)) || Directory.Exists(PortableFolder))
                return true;

            foreach (string lsArgument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(lsArgument, PortableArgument, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    private static void fResolve()
    {
        msPath = Application.persistentDataPath;
        IsPortable = false;
        Problem = null;

        if (!WantsPortable)
            return;

        string lsFolder = PortableFolder;

        try
        {
            Directory.CreateDirectory(lsFolder);

            // Writable: a probe written and taken away again.
            string lsProbe = System.IO.Path.Combine(lsFolder, ".write-probe");

            File.WriteAllText(lsProbe, "ok");
            File.Delete(lsProbe);

            msPath = lsFolder;
            IsPortable = true;
        }
        catch (Exception lOError) when (lOError is IOException || lOError is UnauthorizedAccessException)
        {
            Problem = lOError.Message;
            Debug.LogWarning("[Portable] " + lsFolder + " cannot be written (" + lOError.Message + ") - using " + msPath);
        }
    }

    /// <summary>A file or folder in the port's folder.</summary>
    public static string Combine(string psName)
    {
        return System.IO.Path.Combine(Path, psName);
    }

    // ------------------------------------------------- Paths the player chose

    /// <summary>A path as it is kept in settings.json: relative to the program while portable and on the
    /// program's drive, else as it is.</summary>
    public static string StorePath(string psPath)
    {
        string lsProgram = ProgramFolder;

        if (!IsPortable || string.IsNullOrEmpty(psPath) || lsProgram == null || !System.IO.Path.IsPathRooted(psPath))
            return psPath;

        try
        {
            string lsFull = System.IO.Path.GetFullPath(psPath);

            if (!string.Equals(System.IO.Path.GetPathRoot(lsFull), System.IO.Path.GetPathRoot(lsProgram), StringComparison.OrdinalIgnoreCase))
                return psPath;

            return System.IO.Path.GetRelativePath(lsProgram, lsFull);
        }
        catch (Exception lOError) when (lOError is ArgumentException || lOError is NotSupportedException || lOError is PathTooLongException)
        {
            return psPath;
        }
    }

    /// <summary>A path read from settings.json: a relative one is the program's (StorePath).</summary>
    public static string ResolvePath(string psPath)
    {
        string lsProgram = ProgramFolder;

        // The program's folder first: in the editor there is none, and a path with characters no path
        // may hold (a settings fixture's) throws in IsPathRooted on Unity's Mono.
        if (lsProgram == null || string.IsNullOrEmpty(psPath))
            return psPath;

        try
        {
            if (System.IO.Path.IsPathRooted(psPath))
                return psPath;

            return System.IO.Path.GetFullPath(System.IO.Path.Combine(lsProgram, psPath));
        }
        catch (Exception lOError) when (lOError is ArgumentException || lOError is NotSupportedException || lOError is PathTooLongException)
        {
            return psPath;
        }
    }

    // ------------------------------------------------- Making a copy portable

    /// <summary>
    /// THIS COPY PORTABLE (the Game window's button): writes portable.txt beside the program and copies the
    /// port's files there - the settings, the game folders, the ROMs and the mods; not the extracted game
    /// data, which is made again. Nothing that is already there is overwritten. In force at the next start.
    /// Null where it worked, else why not.
    /// </summary>
    public static string MakePortable()
    {
        string lsProgram = ProgramFolder;

        if (lsProgram == null)
            return "not in the editor";

        string lsTarget = PortableFolder;
        string lsSource = Path;

        try
        {
            Directory.CreateDirectory(lsTarget);
            // What the file means and how to go back (per user, 2026-11-09: the folder alone keeps it
            // portable, so deleting this file is not enough).
            File.WriteAllText(System.IO.Path.Combine(lsProgram, MarkerName),
                "Underworld Revisited keeps its settings, its extracted game data and the mods in the folder\n"
                + "UserData beside the program while this file OR that folder is here.\n"
                + "\n"
                + "To go back to the normal user folder, delete this file and the folder UserData - or, to keep\n"
                + "your settings, first copy what is in UserData to the normal folder:\n"
                + "\n"
                + "    " + Application.persistentDataPath + "\n"
                + "\n"
                + "The save games are not in UserData: they stay in the game's installation (UNDEROM1, UNDEROM2).\n");

            foreach (string lsFile in new[] { "settings.json", "graphics.json" })
            {
                string lsFrom = System.IO.Path.Combine(lsSource, lsFile);
                string lsTo = System.IO.Path.Combine(lsTarget, lsFile);

                if (File.Exists(lsFrom) && !File.Exists(lsTo))
                    File.Copy(lsFrom, lsTo);
            }

            foreach (string lsFolder in new[] { "UW1", "UW2", "MT32", UWMods.FolderName })
                fCopyFolder(System.IO.Path.Combine(lsSource, lsFolder), System.IO.Path.Combine(lsTarget, lsFolder));

            // THE REST OF THIS SESSION WRITES THERE TOO (a Steam player's report, 2026-11-09: a scheme chosen after
            // Make portable went into the old folder, and the next start read the copy made before the choice -
            // every setting changed after the button was lost so). The files read at the start stay as they were.
            msPath = lsTarget;
            IsPortable = true;
            Problem = null;

            return null;
        }
        catch (Exception lOError) when (lOError is IOException || lOError is UnauthorizedAccessException)
        {
            return lOError.Message;
        }
    }

    private static void fCopyFolder(string psFrom, string psTo)
    {
        if (!Directory.Exists(psFrom))
            return;

        Directory.CreateDirectory(psTo);

        foreach (string lsFile in Directory.GetFiles(psFrom))
        {
            string lsTo = System.IO.Path.Combine(psTo, System.IO.Path.GetFileName(lsFile));

            if (!File.Exists(lsTo))
                File.Copy(lsFile, lsTo);
        }

        foreach (string lsFolder in Directory.GetDirectories(psFrom))
            fCopyFolder(lsFolder, System.IO.Path.Combine(psTo, System.IO.Path.GetFileName(lsFolder)));
    }
}
