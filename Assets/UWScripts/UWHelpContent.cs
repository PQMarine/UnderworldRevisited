using System.Collections.Generic;

/// <summary>
/// What the help window says about spells and mantras beyond what the game data holds.
///
/// THE SPELLS: the tab shows first the forty rune spells the game's manual names (five per
/// circle), then - in a panel closed by default, for whoever wants to find them out alone -
/// the eight it does not name (decided per user, 2026-09-24). Names and runes come from the
/// game data at runtime (UWRunicMagic); the short notes below are OUR OWN WORDS, not the
/// manual's text, which is not ours to ship. The eight unnamed spells get no note: what some
/// of them do is not settled in our code yet, and the panel is meant for finding out.
///
/// THE MANTRAS: SUMM RA, MU AHM and OM CAH are the three the manual names; the others come
/// in the closed panel. Words and skill names come from the game data (UWShrineRules).
///
/// THE NOTES ARE TEXT IDS since cleanup step 8.5 (2026-10-24): the words themselves are in the
/// text table (UWTextEnglish.Help.cs), the tabs show them through UWText.T.
/// </summary>
public static class UWHelpContent
{
    /// <summary>Our note's text id per spell number (UWRunicMagic.AllSpells) for the spells the
    /// manual names. A spell missing here is one the manual does not name.</summary>
    public static readonly Dictionary<int, string> ManualSpellNotes = new Dictionary<int, string>
    {
        // First circle
        { 0, "help.spells.note.0" },
        { 1, "help.spells.note.1" },
        { 2, "help.spells.note.2" },
        { 3, "help.spells.note.3" },
        { 4, "help.spells.note.4" },

        // Second circle
        { 7, "help.spells.note.7" },
        { 8, "help.spells.note.8" },
        { 9, "help.spells.note.9" },
        { 10, "help.spells.note.10" },
        { 11, "help.spells.note.11" },

        // Third circle
        { 12, "help.spells.note.12" },
        { 13, "help.spells.note.13" },
        { 14, "help.spells.note.14" },
        { 15, "help.spells.note.15" },
        { 16, "help.spells.note.16" },

        // Fourth circle
        { 19, "help.spells.note.19" },
        { 20, "help.spells.note.20" },
        { 21, "help.spells.note.21" },
        { 22, "help.spells.note.22" },
        { 23, "help.spells.note.23" },

        // Fifth circle
        { 24, "help.spells.note.24" },
        { 26, "help.spells.note.26" },
        { 27, "help.spells.note.27" },
        { 28, "help.spells.note.28" },
        { 29, "help.spells.note.29" },

        // Sixth circle
        { 30, "help.spells.note.30" },
        { 32, "help.spells.note.32" },
        { 33, "help.spells.note.33" },
        { 34, "help.spells.note.34" },
        { 35, "help.spells.note.35" },

        // Seventh circle
        { 36, "help.spells.note.36" },
        { 37, "help.spells.note.37" },
        { 39, "help.spells.note.39" },
        { 40, "help.spells.note.40" },
        { 41, "help.spells.note.41" },

        // Eighth circle
        { 42, "help.spells.note.42" },
        { 43, "help.spells.note.43" },
        { 44, "help.spells.note.44" },
        { 45, "help.spells.note.45" },
        { 46, "help.spells.note.46" }
    };

    /// <summary>The group mantras the manual names, in its order, with our note's text id.</summary>
    public static readonly KeyValuePair<int, string>[] ManualMantras =
    {
        new KeyValuePair<int, string>(UWDataImport.UWData.UWShrineRules.FirstSpecialMantra + 3, "help.mantras.note.combat"),
        new KeyValuePair<int, string>(UWDataImport.UWData.UWShrineRules.FirstSpecialMantra + 4, "help.mantras.note.magic"),
        new KeyValuePair<int, string>(UWDataImport.UWData.UWShrineRules.FirstSpecialMantra + 5, "help.mantras.note.other")
    };

    /// <summary>Our note's text id for the two quest mantras (UWShrineRules.CupOfWonderMantra and
    /// KeyOfTruthMantra).</summary>
    public const string CupOfWonderNote = "help.mantras.note.cup_of_wonder";

    public const string KeyOfTruthNote = "help.mantras.note.key_of_truth";
}
