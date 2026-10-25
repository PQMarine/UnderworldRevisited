using System.IO;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// WHICH LANGUAGE THE PORT'S OWN TEXTS SHOW IN (cleanup step 8.5, 2026-10-24): chosen once at
/// startup, before any window or panel is built, so a text taken when a window is built is the
/// language's as well (UWText; the game's own strings are not touched).
///
/// The language is asked for, first that wins:
///   - the command line: -language de
///   - the environment variable UR_LANGUAGE=de
///   - in the editor, the menu Underworld Revisited / Text (UWLanguageMenu, kept in EditorPrefs).
/// "en" or nothing is English. "pseudo" is the built-in test language (UWText.UsePseudo): every
/// text bracketed and about 40 % longer, to see a text that does not come through the table and
/// where a longer language clips - no file needed. Any other code reads Languages/&lt;code&gt;.tsv
/// beside the game (beside the executable; in the editor the project folder), the format
/// described at UWText; a missing file or a missing line keeps the English.
/// </summary>
public static class UWLanguage
{
    public const string CommandLineSwitch = "-language";

    public const string EnvironmentVariable = "UR_LANGUAGE";

    /// <summary>The editor's choice (UWLanguageMenu).</summary>
    public const string EditorPrefsKey = "UnderworldRevisited.Language";

    /// <summary>The language files' folder beside the game.</summary>
    public const string FolderName = "Languages";

    /// <summary>The folder the game runs from: beside the executable in a build (dataPath is its
    /// _Data folder), the project folder in the editor.</summary>
    public static string GameFolder => Path.GetDirectoryName(Application.dataPath);

    /// <summary>The file a language code is read from.</summary>
    public static string FileOf(string psCode)
    {
        return Path.Combine(GameFolder, FolderName, psCode + ".tsv");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void fChooseAtStartup()
    {
        Apply(Requested());
    }

    /// <summary>The code asked for (see the class), or empty for English.</summary>
    public static string Requested()
    {
        string[] lsArgs = System.Environment.GetCommandLineArgs();

        for (int liAt = 0; liAt + 1 < lsArgs.Length; liAt++)
        {
            if (lsArgs[liAt] == CommandLineSwitch)
                return lsArgs[liAt + 1];
        }

        string lsCode = System.Environment.GetEnvironmentVariable(EnvironmentVariable);

        if (!string.IsNullOrEmpty(lsCode))
            return lsCode;

#if UNITY_EDITOR
        return UnityEditor.EditorPrefs.GetString(EditorPrefsKey, string.Empty);
#else
        return string.Empty;
#endif
    }

    /// <summary>Puts a language in force; English for an empty, unknown or unreadable one.</summary>
    public static void Apply(string psCode)
    {
        string lsCode = (psCode ?? string.Empty).Trim();

        if (lsCode.Length == 0 || lsCode == "en")
        {
            UWText.UseEnglish();

            return;
        }

        if (lsCode == UWText.PseudoLanguage)
        {
            UWText.UsePseudo();
            Debug.Log("UWLanguage: the pseudo-language (" + UWText.TextIds.Count + " texts, lengthened and in brackets)");

            return;
        }

        // A code is a name, never a path.
        foreach (char lcChar in lsCode)
        {
            if (!char.IsLetterOrDigit(lcChar) && lcChar != '-' && lcChar != '_')
            {
                Debug.LogWarning("UWLanguage: \"" + lsCode + "\" is no language code - English");
                UWText.UseEnglish();

                return;
            }
        }

        string lsPath = FileOf(lsCode);

        if (!File.Exists(lsPath))
        {
            Debug.LogWarning("UWLanguage: no " + lsPath + " - English");
            UWText.UseEnglish();

            return;
        }

        UWTextLanguageFile lOFile = UWText.LoadLanguage(lsCode, lsPath);

        if (lOFile == null)
        {
            Debug.LogWarning("UWLanguage: " + lsPath + " could not be read - English");

            return;
        }

        Debug.Log("UWLanguage: " + lsCode + " from " + lsPath + ", " + lOFile.Texts.Count + " of " + UWText.TextIds.Count
            + " texts, " + lOFile.Problems.Count + " lines skipped");

        for (int liAt = 0; liAt < lOFile.Problems.Count && liAt < 20; liAt++)
            Debug.LogWarning("UWLanguage: " + lOFile.Problems[liAt]);
    }
}
