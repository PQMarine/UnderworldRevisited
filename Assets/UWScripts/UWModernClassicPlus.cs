using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE CLASSIC MODULAR PRESET (the enum keeps its old name ClassicPlus): the original's pieces
/// freed as elements over the whole screen. Its layout is the one the user laid out in the layout
/// editor from the first draft (which put the pieces at the original's places, 2026-10-10) and
/// saved, taken over as the preset on 2026-10-11 (per user: "take it over as the preset"):
///
///   - the stone shelf along the bottom from the left edge to the stats panel in the bottom right
///     corner, the message scroll (80 %, five lines) on it at the left, the active spells, the
///     compass on its disc and the power gem in the middle, the rune hollow and the flasks at the
///     right;
///   - the rune tablet at the left edge and the character page at the right edge, both a little
///     above the middle; the heading strip at the top in the middle;
///   - the compass and the rune hollow with their outlines, no generated backs; the conversation
///     in the original's look at its default place, at the top in the middle.
///
/// Kept as the own layout's text (UWHudLayout), so it keeps its sense at another resolution or
/// aspect like the own layout does. The shelf is the one part that adapts beyond that: as wide as
/// the screen leaves beside the stats panel (on the user's 16:9 screen the 493 they had set).
/// </summary>
public static class UWModernClassicPlus
{
    public static bool IsActive => UWModernLayout.Preset == UWModernLayout.PresetEnum.ClassicPlus;

    /// <summary>The places, as the own layout keeps them (UWUserSettings.ModernLayout).</summary>
    public const string Layout =
        "2:0.5:1:0:-4:100;3:0.5:0:-57.3:40:100;4:0:0:0:6:80;10:0.5:0:-0.12:5.9:100;11:0.5:0:45.42:5.73:100;12:0:0:0:0:100;"
        + "14:1:0:-100.3:5.7:100;15:1:0:-75.4:6:100;16:1:0:-127.6:5.7:100;17:0:0.5:-3.3:-35:100;18:1:0:0:0:100;19:1:0.5:-0.13:36:100;";

    /// <summary>Lines of text the message scroll holds.</summary>
    public const int ScrollLines = 5;

    /// <summary>The stone shelf's width in its own pixels: the screen beside the stats panel.</summary>
    public static int StoneShelfWidth => Mathf.Max(UWHudArt.StoneShelfMinWidth, UWModernHud.StoneShelfMaxWidth - UWTextures.PanelWidth);

    /// <summary>The compass disc's outline: on in Classic Modular, else the own layout's choice.</summary>
    public static bool CompassOutline => IsActive || UWUserSettings.ModernCompassOutline;

    /// <summary>The rune hollow's outline: on in Classic Modular, else the own layout's choice.</summary>
    public static bool RuneHollowOutline => IsActive || UWUserSettings.ModernRuneHollowOutline;

    /// <summary>The conversation's outline: on in Classic Modular, else the own layout's choice.</summary>
    public static bool ConversationOutline => IsActive || !UWUserSettings.ModernConversationNoOutline;
}
