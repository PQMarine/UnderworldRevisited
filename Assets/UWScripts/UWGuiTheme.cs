using UnityEngine;

/// <summary>
/// THE LOOK OF THE PORT'S IMGUI WINDOWS IN ONE PLACE (cleanup 8.1, 2026-10-24): the setup bar and
/// its dialogs (UWSetupMenu), the help window (UWHelpWindow), the layout editor's window
/// (UWModernLayoutEditor), the modern game menu (UWModernGameMenu) and the gamepad's letter grid
/// (UWLetterGrid). Until then every window kept its own copy of the palette (the text colour stood
/// in seven files), its own 1x1 texture cache, its own styles and its own GUI.matrix set-up on 720
/// lines.
///
/// EVERY WINDOW KEEPS ITS EXACT LOOK: the style families were the windows' former fEnsureStyles,
/// moved here value for value. They stayed families and not one set because their sizes differ on
/// purpose - the dialogs' 16 points against the editor's 15, the help's 15 with rich text - and
/// changing one would change a window.
///
/// ON THE KIT (phase 8b, 2026-10-24): the windows moved onto the uGUI kit (UWKitRows, UWUiKit), which
/// reads the palette, the scale and the families' sizes as the numbers below; with the last IMGUI
/// window gone (cleanup 8b.4) the GUIStyle families, the shared 1x1 textures and the GUI.matrix
/// set-up went too. The debug overlays (UWDebugOverlay, UWDebugTools) followed in cleanup 8c: their
/// panel (UWDebugPanel) draws in this palette on a see-through ground of its own.
/// </summary>
public static class UWGuiTheme
{
    /// <summary>The height the windows are laid out on; they scale with the screen's height.</summary>
    public const float ReferenceHeight = 720f;

    // ------------------------------------------------- The palette

    /// <summary>The darkest ground: the first-run screen, text fields, the folder list, the help's
    /// notes and map, the game menu's name field.</summary>
    public static readonly Color Background = new Color(0.05f, 0.05f, 0.06f, 1f);

    /// <summary>The setup bar and the layout editor's title row.</summary>
    public static readonly Color Bar = new Color(0.11f, 0.11f, 0.13f, 1f);

    /// <summary>The dialogs and menus of the bar, the editor's window, the help's tabs, the game
    /// menu's buttons.</summary>
    public static readonly Color Panel = new Color(0.14f, 0.14f, 0.16f, 1f);

    /// <summary>A row or tab under the pointer, an open bar item, a button's face.</summary>
    public static readonly Color Hover = new Color(0.22f, 0.22f, 0.26f, 1f);

    /// <summary>A button's face under the pointer.</summary>
    public static readonly Color ButtonHover = new Color(0.30f, 0.30f, 0.35f, 1f);

    /// <summary>A thin line between groups inside a menu.</summary>
    public static readonly Color Separator = new Color(0.30f, 0.30f, 0.34f, 1f);

    /// <summary>The text colour, the modern interface's own (the same value the windows wrote out).</summary>
    public static readonly Color Text = UWModernPalette.HudText;

    public static readonly Color DimText = new Color(0.55f, 0.55f, 0.55f, 1f);

    /// <summary>The accent, the modern interface's own (the same value the windows wrote out).</summary>
    public static readonly Color Accent = UWModernPalette.Accent;

    /// <summary>Accent for rich text.</summary>
    public const string AccentHex = "CCA357";

    /// <summary>The help window's and the modern game menu's ground, a little see-through.</summary>
    public static readonly Color Overlay = new Color(0.08f, 0.08f, 0.09f, 0.96f);

    /// <summary>The shade over the picture while the manual is read.</summary>
    public static readonly Color ReadingShade = new Color(0.03f, 0.03f, 0.04f, 0.94f);

    /// <summary>A status line that reports a failure (the folder dialog).</summary>
    public static readonly Color Error = new Color(0.9f, 0.4f, 0.35f);

    // ------------------------------------------------- Scale

    /// <summary>Screen pixels per layout unit: the windows are laid out on ReferenceHeight lines.</summary>
    public static float Scale => Screen.height / ReferenceHeight;

    // ------------------------------------------------- The setup family's measures, shared with the kit

    // The sizes and paddings of the setup bar's styles as numbers (cleanup 8b.1, 2026-10-24), so the
    // kit's drawer (UWKitRows) draws the windows that moved onto it in the same type as the IMGUI form
    // beside it did.

    /// <summary>A dialog's title, a heading line, the folder's path line.</summary>
    public const int SetupHeadingSize = 20;

    /// <summary>Rows, list rows, buttons, switches, bar items, text fields.</summary>
    public const int SetupTextSize = 16;

    /// <summary>The dim hints under the rows.</summary>
    public const int SetupHintSize = 15;

    /// <summary>The first-run screen's title.</summary>
    public const int SetupBigTitleSize = 40;

    /// <summary>Left and right of a list row's text.</summary>
    public const int SetupListPadding = 10;

    /// <summary>Left and right of a text field's text.</summary>
    public const int SetupFieldPadding = 8;

    // ------------------------------------------------- The help family's measures, shared with the kit

    // The sizes, margins and paddings of the help's styles as numbers (cleanup 8b.3, 2026-10-24), so the
    // kit's help (UWHelpKitFlow, UWKitRows' help pieces) draws in the same type as the IMGUI form did.

    /// <summary>The help's running text, its stat rows, the manual's page line.</summary>
    public const int HelpTextSize = 15;

    /// <summary>The dim lines under the entries.</summary>
    public const int HelpDimSize = 13;

    /// <summary>A tab's title (bold, in the accent colour).</summary>
    public const int HelpHeadingSize = 20;

    /// <summary>A heading inside a tab (bold, in the accent colour), with room above and below.</summary>
    public const int HelpSubHeadingSize = 16;

    public const int HelpSubHeadingMarginTop = 6;

    public const int HelpSubHeadingMarginBottom = 2;

    /// <summary>The tab buttons.</summary>
    public const int HelpTabSize = 14;

    /// <summary>The fold-outs and the manual's page arrows, with their padding.</summary>
    public const int HelpFoldoutSize = 13;

    public const int HelpFoldoutPaddingX = 10;

    public const int HelpFoldoutPaddingY = 6;

    /// <summary>The reading mode's arrows.</summary>
    public const int HelpArrowSize = 28;

    /// <summary>Round the notes' text.</summary>
    public const int HelpNotesPadding = 8;

    /// <summary>Right of each rune picture in the spell list.</summary>
    public const int HelpIconMarginRight = 3;

    // ------------------------------------------------- The layout editor's measures, shared with the kit

    // The sizes of the layout editor's styles as numbers (cleanup 8b.4, 2026-10-24), so the kit's drawer
    // (UWKitRows in the editor's look) draws the editor's window in the same type as its IMGUI form did.

    /// <summary>The window's rows: labels, values, buttons, segments, switches.</summary>
    public const int LayoutEditorTextSize = 15;

    /// <summary>The title row and the inspector's heading, in the accent colour.</summary>
    public const int LayoutEditorTitleSize = 16;

    /// <summary>The dim hints, wrapped from the top.</summary>
    public const int LayoutEditorHintSize = 13;

    // ------------------------------------------------- The game menu's measures, shared with the kit

    // The sizes of the game menu's styles as numbers (cleanup 8b.4, 2026-10-24), so the kit (UWKitRows in
    // the game menu's look, the modern font) draws the menu in the same type as its IMGUI form did.

    /// <summary>A page's title, bold in the accent colour.</summary>
    public const int GameMenuTitleSize = 22;

    /// <summary>The save name page's line above the field, wrapped.</summary>
    public const int GameMenuTextSize = 15;

    /// <summary>The buttons, the section heading and the name field.</summary>
    public const int GameMenuButtonSize = 16;

    // ------------------------------------------------- The letter grid's measures, shared with the kit

    // The sizes and the hint's colour of the letter grid's styles (cleanup 8b.4, 2026-10-24), so the kit
    // (UWKitRows in the letter grid's look, the skin's font) draws the grid as its IMGUI form did.

    /// <summary>A key's letter or name, bold, white - dark on the gold key with the focus.</summary>
    public const int LetterGridKeySize = 20;

    /// <summary>The hint line under the keys.</summary>
    public const int LetterGridHintSize = 14;

    public static readonly Color LetterGridHint = new Color(0.85f, 0.8f, 0.7f);
}
