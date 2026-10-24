using UnityEngine;

/// <summary>
/// THE MODERN SCHEME'S COLOURS, in one place (cleanup 5.1, 2026-10-19). Until then every modern
/// part typed its own copy - the gold in ten files, the parchment text in ten, the outline in
/// nine, the dim text, the rule, the light hover text - so a change of shade meant hunting the
/// literals through a dozen files. The values are exactly those literals: nothing changes on
/// screen.
///
/// HudText is UWModernHud's own text colour, a cooler grey than the parts' parchment Text. It
/// stays different on purpose and is its own entry (decision 15 of the cleanup plan, DECIDED
/// 2026-10-19 per user: keep it).
///
/// Unity colours (floats) and not an engine-free table: the modern scheme stays a uGUI renderer
/// (decision 26), and the float values do not all fall on whole bytes, so a UWColor32 copy would
/// not be the same colour. Colours only one part uses stay in that part.
/// </summary>
public static class UWModernPalette
{
    /// <summary>The gold of headings, frames, highlights and the hovered heading strip.</summary>
    public static readonly Color Gold = new Color(0.925f, 0.77f, 0.44f, 1f);

    /// <summary>The parchment text of the modern parts (panels, bags' titles, menus, question box,
    /// minimap, layout editor, pad hints).</summary>
    public static readonly Color Text = new Color(0.94f, 0.87f, 0.71f);

    /// <summary>The dimmed text: secondary rows, disabled menu entries.</summary>
    public static readonly Color Dim = new Color(0.78f, 0.71f, 0.57f);

    /// <summary>The leather-brown rule between sections of the panels.</summary>
    public static readonly Color Rule = new Color(0.47f, 0.28f, 0.12f, 1f);

    /// <summary>The light text: a hovered answer or button, the badges' numbers.</summary>
    public static readonly Color LightText = new Color(1f, 0.92f, 0.69f, 1f);

    /// <summary>UWModernHud's own text colour (the messages, the heading strip, the game menu),
    /// also the starting colour of the texts the bags and the original character page create.
    /// Decision 15: kept.</summary>
    public static readonly Color HudText = new Color(0.86f, 0.86f, 0.84f, 1f);

    /// <summary>The dark brown outline around the parts' text.</summary>
    public static readonly Color Outline = new Color(0.16f, 0.09f, 0.04f, 0.9f);

    /// <summary>The bronze accent: the load bar, the scroll thumbs, the game menu's accent.</summary>
    public static readonly Color Accent = new Color(0.80f, 0.64f, 0.34f, 1f);

    /// <summary>The greyed text of what cannot be cast now.</summary>
    public static readonly Color Grey = new Color(0.55f, 0.50f, 0.42f);

    /// <summary>The pale blue of rune sequences and castable spells.</summary>
    public static readonly Color RunesText = new Color(0.77f, 0.84f, 1f);

    /// <summary>The dark fill of the bags' count badges and the minimap's buttons.</summary>
    public static readonly Color BadgeFill = new Color(0.18f, 0.094f, 0.035f, 1f);

    /// <summary>The dark fill behind a typed number or answer (the split box, the conversation's
    /// typed answer, the question box's input).</summary>
    public static readonly Color InputFill = new Color(0.10f, 0.06f, 0.03f, 0.85f);

    /// <summary>A palette colour with another alpha (hover washes, fading names).</summary>
    public static Color WithAlpha(Color pOColour, float pfAlpha)
    {
        pOColour.a = pfAlpha;
        return pOColour;
    }
}
