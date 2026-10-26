using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// WHICH LANGUAGE THE PORT SHOWS (cleanup step 8.5, 2026-10-24; the game's own texts since
/// translation wave 1, 2026-10-26): chosen at startup, before any window or panel is built, so a
/// text taken when a window is built is the language's as well; since translation step 3R
/// (2026-10-26) the Game window's Language row switches it at once (Switch).
///
/// The language is asked for, first that wins:
///   - the command line: -language de
///   - the environment variable UR_LANGUAGE=de
///   - in the editor, the pseudo-language switched on in the menu Underworld Revisited / Text
///     (UWLanguageMenu, kept in EditorPrefs)
///   - the Game window's Language row (UWUserSettings.Language); chosen there it takes effect at
///     once, also over the command line, which wins again at the next start.
/// "en" or nothing is English. "pseudo" is the built-in test language (UWText.UsePseudo): every
/// text of the port bracketed and about 40 % longer, to see a text that does not come through the
/// table and where a longer language clips - no file needed.
///
/// ONE CODE, TWO FILES in Languages beside the game (beside the executable; in the editor the
/// project folder), either or both:
///   - Languages/&lt;code&gt;.tsv: the port's own texts (UWText, the format described there);
///   - Languages/&lt;code&gt;.strings.tsv: the game's own texts, laid over the player's English
///     STRINGS.PAK when the data is loaded (UWGameTextPack, UWLevelLoader; GameTextFile).
/// A missing file, or a missing line in one, keeps the English.
/// </summary>
public static class UWLanguage
{
    public const string CommandLineSwitch = "-language";

    public const string EnvironmentVariable = "UR_LANGUAGE";

    /// <summary>The editor's choice (UWLanguageMenu).</summary>
    public const string EditorPrefsKey = "UnderworldRevisited.Language";

    /// <summary>The language files' folder beside the game.</summary>
    public const string FolderName = "Languages";

    /// <summary>English, the code that needs no file.</summary>
    public const string EnglishCode = "en";

    /// <summary>The code in force since startup: empty for English, else the code (the pseudo-language
    /// included).</summary>
    public static string Current { get; private set; } = string.Empty;

    /// <summary>The game-text pack of the language in force (Languages/&lt;code&gt;.strings.tsv), or null
    /// when it has none - the data is then loaded with the English strings.</summary>
    public static string GameTextFile { get; private set; }

    /// <summary>The folder the game runs from: beside the executable in a build (dataPath is its
    /// _Data folder), the project folder in the editor.</summary>
    public static string GameFolder => Path.GetDirectoryName(Application.dataPath);

    /// <summary>The file a language code's own texts are read from.</summary>
    public static string FileOf(string psCode)
    {
        return Path.Combine(GameFolder, FolderName, psCode + ".tsv");
    }

    /// <summary>The file a language code's game texts are read from.</summary>
    public static string GameTextFileOf(string psCode)
    {
        return Path.Combine(GameFolder, FolderName, psCode + UWGameTextPack.FileSuffix);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void fChooseAtStartup()
    {
        Apply(Requested());
    }

    /// <summary>The code asked for (see the class), or empty for English.</summary>
    public static string Requested()
    {
        string[] lsArgs = Environment.GetCommandLineArgs();

        for (int liAt = 0; liAt + 1 < lsArgs.Length; liAt++)
        {
            if (lsArgs[liAt] == CommandLineSwitch)
                return lsArgs[liAt + 1];
        }

        string lsCode = Environment.GetEnvironmentVariable(EnvironmentVariable);

        if (!string.IsNullOrEmpty(lsCode))
            return lsCode;

#if UNITY_EDITOR
        lsCode = UnityEditor.EditorPrefs.GetString(EditorPrefsKey, string.Empty);

        if (!string.IsNullOrEmpty(lsCode))
            return lsCode;
#endif

        return UWUserSettings.Language;
    }

    /// <summary>Whether a text is a language code: letters, digits, - and _ (a name, never a path).</summary>
    public static bool IsCode(string psCode)
    {
        if (string.IsNullOrEmpty(psCode))
            return false;

        foreach (char lcChar in psCode)
        {
            if (!char.IsLetterOrDigit(lcChar) && lcChar != '-' && lcChar != '_')
                return false;
        }

        return true;
    }

    /// <summary>
    /// The languages found beside the game, sorted: every code with a Languages/&lt;code&gt;.tsv or a
    /// Languages/&lt;code&gt;.strings.tsv - the Game window's Language row lists them after English.
    /// English itself (an en.tsv written as a template) is not listed; an unreadable folder lists
    /// nothing.
    /// </summary>
    public static List<string> Available()
    {
        SortedSet<string> lOCodes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            string lsFolder = Path.Combine(GameFolder, FolderName);

            if (Directory.Exists(lsFolder))
            {
                foreach (string lsFile in Directory.GetFiles(lsFolder, "*.tsv"))
                {
                    string lsName = Path.GetFileName(lsFile);
                    string lsCode = lsName.EndsWith(UWGameTextPack.FileSuffix, StringComparison.OrdinalIgnoreCase)
                        ? lsName.Substring(0, lsName.Length - UWGameTextPack.FileSuffix.Length)
                        : lsName.Substring(0, lsName.Length - ".tsv".Length);

                    if (IsCode(lsCode) && !string.Equals(lsCode, EnglishCode, StringComparison.OrdinalIgnoreCase))
                        lOCodes.Add(lsCode);
                }
            }
        }
        catch (Exception lOError)
        {
            Debug.LogWarning("UWLanguage: the languages could not be listed: " + lOError.Message);
        }

        return new List<string>(lOCodes);
    }

    /// <summary>Puts a language in force; English for an empty, unknown or unreadable one. The
    /// port's own texts change at once; the game texts follow the next time the data is loaded
    /// (GameTextFile) - or at once through Switch, which calls this. The language's own fonts
    /// (grammar.rule.font, UWFallbackFont - translation wave 6) follow its texts at once.</summary>
    public static void Apply(string psCode)
    {
        fApplyTexts(psCode);
        UWFallbackFont.Apply();
    }

    private static void fApplyTexts(string psCode)
    {
        string lsCode = (psCode ?? string.Empty).Trim();

        GameTextFile = null;
        Current = string.Empty;

        UWTextLine.UseEnglish();

        if (lsCode.Length == 0 || lsCode == EnglishCode)
        {
            UWText.UseEnglish();

            return;
        }

        if (lsCode == UWText.PseudoLanguage)
        {
            UWText.UsePseudo();
            Current = lsCode;
            Debug.Log("UWLanguage: the pseudo-language (" + UWText.TextIds.Count + " texts, lengthened and in brackets)");

            return;
        }

        if (!IsCode(lsCode))
        {
            Debug.LogWarning("UWLanguage: \"" + lsCode + "\" is no language code - English");
            UWText.UseEnglish();

            return;
        }

        string lsPath = FileOf(lsCode);
        string lsGameText = GameTextFileOf(lsCode);
        bool lbGameText = File.Exists(lsGameText);

        if (!File.Exists(lsPath) && !lbGameText)
        {
            Debug.LogWarning("UWLanguage: neither " + lsPath + " nor " + lsGameText + " - English");
            UWText.UseEnglish();

            return;
        }

        Current = lsCode;

        // The letters the text entries take beyond ASCII: the language's grammar's (translation wave 2;
        // German: the umlauts and the sharp s; a language without a class of its own: its header's) and,
        // since translation 5.1, the letter grid's alphabet with its letters (UWTextLine.UseLanguage).
        UWTextLine.UseLanguage(UWGrammar.For(lsCode));

        if (lbGameText)
        {
            GameTextFile = lsGameText;
            Debug.Log("UWLanguage: " + lsCode + ", the game's texts from " + lsGameText + " when the data is loaded");
        }

        if (!File.Exists(lsPath))
        {
            // Only the game's texts: the port's own stay English (and with them the header's alphabet).
            UWText.UseEnglish();
            UWTextLine.UseLanguage(UWGrammar.For(lsCode));

            return;
        }

        UWTextLanguageFile lOFile = UWText.LoadLanguage(lsCode, lsPath);

        if (lOFile == null)
        {
            Debug.LogWarning("UWLanguage: " + lsPath + " could not be read - English");

            return;
        }

        // A language without a grammar class of its own names its letters in its file's header
        // (grammar.rule.letters, UWGrammarGeneric - translation wave 4) and every language may name its
        // alphabet (grammar.rule.alphabet, translation 5.1): taken again now it is in force.
        UWTextLine.UseLanguage(UWGrammar.For(lsCode));

        Debug.Log("UWLanguage: " + lsCode + " from " + lsPath + ", " + lOFile.Texts.Count + " of " + UWText.TextIds.Count
            + " texts, " + lOFile.Problems.Count + " lines skipped");

        for (int liAt = 0; liAt < lOFile.Problems.Count && liAt < 20; liAt++)
            Debug.LogWarning("UWLanguage: " + lOFile.Problems[liAt]);
    }

    /// <summary>
    /// ANOTHER LANGUAGE AT ONCE (translation step 3R, per user 2026-10-26: the Game window's Language
    /// row takes effect without a restart). Apply puts the port's own texts and the typed letters in
    /// force; the game's texts of the data loaded so far are laid anew over a fresh copy of the
    /// English strings (UWGameData.UseGameText: in place, the grammar chosen again), and the change is
    /// counted (UWText.LanguageVersion) so every window and HUD part that keeps a text takes it anew.
    /// Lines already in the message scroll stay as they were written; a conversation that is open
    /// keeps the lines it has taken (its next ones are the new language's).
    /// </summary>
    public static void Switch(string psCode)
    {
        string lsBefore = Current;

        Apply(psCode);

        DataImport lOData = UWLevelLoader.SharedData;

        if (lOData != null && lOData.GameData.GameTextFile != GameTextFile)
        {
            if (lOData.GameData.UseGameText(GameTextFile, out UWGameTextPack lOPack))
                LogGameText(lOPack, GameTextFile);
            else
                Debug.LogWarning("UWLanguage: STRINGS.PAK could not be read again - the game's texts stay as they were");
        }

        UWText.MarkGameTextChanged();

        Debug.Log("UWLanguage: switched from " + (lsBefore.Length == 0 ? EnglishCode : lsBefore) + " to "
            + (Current.Length == 0 ? EnglishCode : Current) + " while the game runs");
    }

    /// <summary>What the data loader reports after laying a game-text pack over the strings.</summary>
    public static void LogGameText(UWGameTextPack pOPack, string psFile)
    {
        if (string.IsNullOrEmpty(psFile))
            return;

        if (pOPack == null)
        {
            Debug.LogWarning("UWLanguage: " + psFile + " could not be read - the game's texts stay English");

            return;
        }

        Debug.Log("UWLanguage: the game's texts, " + pOPack.Applied + " strings from " + psFile + ", " + pOPack.EmptyRows
            + " left English, " + pOPack.Problems.Count + " lines skipped");

        for (int liAt = 0; liAt < pOPack.Problems.Count && liAt < 20; liAt++)
            Debug.LogWarning("UWLanguage: " + pOPack.Problems[liAt]);
    }
}
