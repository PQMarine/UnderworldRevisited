using System.IO;
using UnityEditor;
using UWDataImport.UWData;

/// <summary>
/// The editor's way to the port's languages (cleanup step 8.5, 2026-10-24; UWLanguage):
///
///   - Underworld Revisited / Text / Pseudo language: the built-in test language at the next Play -
///     every text of the port in brackets and about 40 % longer, so a text the table does not hold
///     shows without brackets and the places where a longer language clips show at once. Kept in
///     EditorPrefs until switched off; the command line and UR_LANGUAGE still come first.
///   - Underworld Revisited / Text / Write the English texts: the whole table as a language file
///     (id, TAB, text), the template a translator starts from. Written where one chooses, never
///     into the project. Since translation wave 6 the same bytes as `uwdump text-template`
///     (UWTextTemplate), which needs no Unity and also updates an older translation (--from).
/// </summary>
public static class UWLanguageMenu
{
    private const string PseudoItem = "Underworld Revisited/Text/Pseudo language (long test texts)";

    [MenuItem(PseudoItem)]
    private static void fTogglePseudo()
    {
        bool lbOn = EditorPrefs.GetString(UWLanguage.EditorPrefsKey, string.Empty) == UWText.PseudoLanguage;

        EditorPrefs.SetString(UWLanguage.EditorPrefsKey, lbOn ? string.Empty : UWText.PseudoLanguage);
    }

    [MenuItem(PseudoItem, true)]
    private static bool fTogglePseudoCheck()
    {
        Menu.SetChecked(PseudoItem, EditorPrefs.GetString(UWLanguage.EditorPrefsKey, string.Empty) == UWText.PseudoLanguage);

        return true;
    }

    [MenuItem("Underworld Revisited/Text/Write the English texts...")]
    private static void fWriteEnglish()
    {
        string lsPath = EditorUtility.SaveFilePanel("The English texts as a language file", string.Empty, "en.tsv", "tsv");

        if (string.IsNullOrEmpty(lsPath))
            return;

        // The same bytes as uwdump text-template (UWTextTemplate, translation wave 6).
        File.WriteAllText(lsPath, UWTextTemplate.English(), new System.Text.UTF8Encoding(false));
        EditorUtility.RevealInFinder(lsPath);
    }
}
