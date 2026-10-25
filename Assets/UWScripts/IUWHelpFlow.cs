using System.Collections.Generic;
using UnityEngine;

/// <summary>The looks of a line of the help (UWGuiTheme.Help): the running text, the dim notes, a
/// tab's title and a heading inside it.</summary>
public enum UWHelpLookEnum
{
    Text = 0,
    Dim = 1,
    Heading = 2,
    SubHeading = 3
}

/// <summary>
/// THE HELP TABS' LISTS AS ONE VOCABULARY (cleanup 8b.3, 2026-10-24; phase 8b): the Stats, Spells,
/// Mantras and Controls tabs lay their lists out top to bottom - lines in the help's looks, room, a
/// label with its value, a fold-out, a spell's row of rune pictures, a row of keys or of pad glyphs -
/// through this. UWHelpImguiFlow drew them with the GUILayout calls the tabs made until then, unchanged
/// (the side-by-side form, gone in cleanup 8b.4); UWHelpKitFlow lays them out on the kit (UWKitRows) with
/// the same sizes, margins and room. So each tab says once what it shows and in which order, and the move
/// to the kit changed the drawer, not the tab - phase 8b's rule for the setup pages (IUWRows), here for
/// the help's GUILayout lists.
/// </summary>
public interface IUWHelpFlow
{
    /// <summary>A line in one of the help's looks, wrapped at the list's width.</summary>
    void Line(string psText, UWHelpLookEnum peLook);

    /// <summary>Room between entries, in layout units.</summary>
    void Space(float pfUnits);

    /// <summary>A label on the left, its value in the accent colour on the right.</summary>
    void StatRow(string psLabel, string psValue);

    /// <summary>A fold-out's button over the list's width; true when pressed.</summary>
    bool Fold(string psText);

    /// <summary>A spell's row: its rune pictures, then its text; true when pressed while pbClickable.</summary>
    bool RuneRow(IReadOnlyList<Texture2D> pOIcons, string psText, bool pbClickable);

    /// <summary>A Controls row: the keys in the keys' column (already in their accent markup), what they
    /// do beside them.</summary>
    void KeyRow(string psKeys, string psText);

    /// <summary>A Controls row with gamepad glyphs in the keys' column, the text centred on them.</summary>
    void GlyphRow(IReadOnlyList<Texture2D> pOGlyphs, string psText);
}

/// <summary>The help's shared measures and markup, for both forms of its lists.</summary>
public static class UWHelpFlowText
{
    /// <summary>How large a rune picture stands in the spell list (help units, 720 lines).</summary>
    public const float RuneIconSize = 22f;

    /// <summary>Between the rune pictures and the spell's text.</summary>
    public const float RuneTextGap = 8f;

    /// <summary>The keys' column of the Controls tab.</summary>
    public const float ControlsKeyWidth = 130f;

    /// <summary>A gamepad glyph there: Kenney's 16 pixels twice over (UWGlyphs).</summary>
    public const float ControlsGlyphSize = 32f;

    /// <summary>A stat row's value in the accent colour.</summary>
    public static string Accent(string psValue)
    {
        return "<color=#" + UWGuiTheme.AccentHex + ">" + psValue + "</color>";
    }

    /// <summary>A Controls row's keys: bold, in the accent colour.</summary>
    public static string Keys(string psKeys)
    {
        return "<color=#" + UWGuiTheme.AccentHex + "><b>" + psKeys + "</b></color>";
    }
}
