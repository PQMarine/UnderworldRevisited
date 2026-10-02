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
/// </summary>
public static class UWHelpContent
{
    /// <summary>Our note per spell number (UWRunicMagic.AllSpells) for the spells the manual
    /// names. A spell missing here is one the manual does not name.</summary>
    public static readonly Dictionary<int, string> ManualSpellNotes = new Dictionary<int, string>
    {
        // First circle
        { 0, "Lights up the surroundings for a while." },
        { 1, "Protects against blows like a full suit of armour, for a while." },
        { 2, "Shoots a magic missile at a target." },
        { 3, "Conjures a meal." },
        { 4, "Muffles your steps for a short time, so creatures notice you less." },

        // Second circle
        { 7, "Lets you drift down gently for a short time." },
        { 8, "Heals light wounds." },
        { 9, "Tells you of creatures nearby that you cannot see." },
        { 10, "May make a foe lose heart and run." },
        { 11, "Lays a ward that tells you when something disturbs it." },

        // Third circle
        { 12, "You move faster than your foes, for a while." },
        { 13, "Makes you harder to see for a short time." },
        { 14, "Lets you see in the dark, for a while." },
        { 15, "Hurls a lightning bolt at a target." },
        { 16, "Spikes a door shut." },

        // Fourth circle
        { 19, "Heals serious wounds." },
        { 20, "Lifts you straight up into the air for a short time." },
        { 21, "Poisons a foe." },
        { 22, "Gives some protection from fire for a short time." },
        { 23, "Disarms a trap you aim at." },

        // Fifth circle
        { 24, "Hurls a fireball at a target." },
        { 26, "Tells you what magic an object holds." },
        { 27, "Makes you proof against missiles, for a while." },
        { 28, "Unlocks a locked door or chest." },
        { 29, "Cures poison." },

        // Sixth circle
        { 30, "Heals you completely." },
        { 32, "Takes you to your moonstone at once." },
        { 33, "Holds a foe in place." },
        { 34, "Bright light for a long time." },
        { 35, "Lets you take and use a single object from afar, for a while." },

        // Seventh circle
        { 36, "Lets you fly for a while, then glide to the ground." },
        { 37, "Turns a creature to fight on your side." },
        { 39, "Makes you nearly invisible, for a while." },
        { 40, "Makes the foes around you stagger as if drunk." },
        { 41, "Reveals hidden objects and secret ways around you." },

        // Eighth circle
        { 42, "Greatly hardens you against damage, for a while." },
        { 43, "Makes the ground quake and the rocks burst." },
        { 44, "Lets you see the world from above, for a while." },
        { 45, "Rains flaming missiles on the area." },
        { 46, "Stops time for everyone but you, for a while." }
    };

    /// <summary>The group mantras the manual names, in its order, with our note.</summary>
    public static readonly KeyValuePair<int, string>[] ManualMantras =
    {
        new KeyValuePair<int, string>(UWDataImport.UWData.UWShrineRules.FirstSpecialMantra + 3, "Improves some of your combat skills."),
        new KeyValuePair<int, string>(UWDataImport.UWData.UWShrineRules.FirstSpecialMantra + 4, "Improves some of your magic skills."),
        new KeyValuePair<int, string>(UWDataImport.UWData.UWShrineRules.FirstSpecialMantra + 5, "Improves some of your other skills.")
    };

    /// <summary>Our note for the two quest mantras (UWShrineRules.CupOfWonderMantra and
    /// KeyOfTruthMantra).</summary>
    public const string CupOfWonderNote = "Tells you where the Cup of Wonder lies.";

    public const string KeyOfTruthNote = "Gives you the Key of Truth.";
}
