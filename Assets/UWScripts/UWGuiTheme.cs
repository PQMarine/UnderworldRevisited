using UnityEngine;
using UWDataImport.UWData;

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
///
/// THEMES (cleanup phase 8d, 2026-10-25, per user): the fixed palette became a theme table, engine-free
/// (UWWindowThemes): Plain - this look, value for value, and the default - and Stone, the application
/// icon's carved stone with its torchlight and the runes' ember. The palette's names below read the
/// theme in force (Current); a carved theme's window grounds are pictures (PicturesOf, made by
/// UWThemeGround) that the kit lays under the windows (UWKitRows.Ground). The debug panel keeps Plain.
/// </summary>
public static class UWGuiTheme
{
    /// <summary>The height the windows are laid out on; they scale with the screen's height.</summary>
    public const float ReferenceHeight = 720f;

    // ------------------------------------------------- The theme (cleanup phase 8d, 2026-10-25)

    /// <summary>
    /// ONE THEME'S COLOURS AS UNITY COLOURS (cleanup phase 8d, 2026-10-25): an entry of the engine-free
    /// theme table (UWWindowThemes) turned into Color once, so the drawers read plain fields each pass
    /// and allocate nothing. The roles are the palette this class held as fixed values until 8d.
    /// </summary>
    public sealed class Palette
    {
        public readonly UWWindowTheme Theme;

        /// <summary>The darkest ground: the first-run screen, text fields, the folder list, the help's
        /// notes and map, the game menu's name field.</summary>
        public readonly Color Background;

        /// <summary>The setup bar and the layout editor's title row.</summary>
        public readonly Color Bar;

        /// <summary>The dialogs and menus of the bar, the editor's window, the help's tabs, the game
        /// menu's buttons.</summary>
        public readonly Color Panel;

        /// <summary>A row or tab under the pointer, an open bar item, a button's face.</summary>
        public readonly Color Hover;

        /// <summary>A button's face under the pointer.</summary>
        public readonly Color ButtonHover;

        /// <summary>A thin line between groups inside a menu.</summary>
        public readonly Color Separator;

        /// <summary>The text colour (Plain: the modern interface's own, UWModernPalette.HudText).</summary>
        public readonly Color Text;

        public readonly Color DimText;

        /// <summary>The accent (Plain: the modern interface's own, UWModernPalette.Accent).</summary>
        public readonly Color Accent;

        /// <summary>Accent for rich text.</summary>
        public readonly string AccentHex;

        /// <summary>The help window's and the modern game menu's ground, a little see-through.</summary>
        public readonly Color Overlay;

        /// <summary>The shade over the picture while the manual is read.</summary>
        public readonly Color ReadingShade;

        /// <summary>A status line that reports a failure (the folder dialog).</summary>
        public readonly Color Error;

        /// <summary>A slider's thumb at rest (UWKitRows' ThumbColour until 8d).</summary>
        public readonly Color Thumb;

        /// <summary>The arrow keys' frame (UWKitKeyNav.FocusColour until 8d) and the glow outside it.</summary>
        public readonly Color Focus;

        public readonly Color FocusGlow;

        /// <summary>The letter grid's panel, keys and focused key (UWLetterGrid's own until 8d) and hints.</summary>
        public readonly Color LetterGridPanel;

        public readonly Color LetterGridKey;

        public readonly Color LetterGridFocus;

        public readonly Color LetterGridHint;

        /// <summary>A carved ground's lit and shaded edges.</summary>
        public readonly Color EdgeLight;

        public readonly Color EdgeShadow;

        public Palette(UWWindowTheme pOTheme)
        {
            Theme = pOTheme;
            Background = fColour(pOTheme.Background);
            Bar = fColour(pOTheme.Bar);
            Panel = fColour(pOTheme.Panel);
            Hover = fColour(pOTheme.Hover);
            ButtonHover = fColour(pOTheme.ButtonHover);
            Separator = fColour(pOTheme.Separator);
            Text = fColour(pOTheme.Text);
            DimText = fColour(pOTheme.DimText);
            Accent = fColour(pOTheme.Accent);
            AccentHex = pOTheme.AccentHex;
            Overlay = fColour(pOTheme.Overlay);
            ReadingShade = fColour(pOTheme.ReadingShade);
            Error = fColour(pOTheme.Error);
            Thumb = fColour(pOTheme.Thumb);
            Focus = fColour(pOTheme.Focus);
            FocusGlow = fColour(pOTheme.FocusGlow);
            LetterGridPanel = fColour(pOTheme.LetterGridPanel);
            LetterGridKey = fColour(pOTheme.LetterGridKey);
            LetterGridFocus = fColour(pOTheme.LetterGridFocus);
            LetterGridHint = fColour(pOTheme.LetterGridHint);
            EdgeLight = fColour(pOTheme.EdgeLight);
            EdgeShadow = fColour(pOTheme.EdgeShadow);
        }

        /// <summary>The ground is the stone's picture with its edges, not a flat colour.</summary>
        public bool IsCarved => Theme.IsCarved;

        /// <summary>A ground's colour: a flat theme's fill, a carved theme's opacity (its alpha).</summary>
        public Color GroundColour(UWWindowGroundEnum peGround)
        {
            switch (peGround)
            {
                case UWWindowGroundEnum.Bar: return Bar;
                case UWWindowGroundEnum.Overlay: return Overlay;
                case UWWindowGroundEnum.Screen: return Background;
                case UWWindowGroundEnum.LetterGrid: return LetterGridPanel;
                default: return Panel;
            }
        }

        private static Color fColour(UWThemeColour pOColour)
        {
            return new Color(pOColour.R, pOColour.G, pOColour.B, pOColour.A);
        }
    }

    /// <summary>
    /// TODAY'S LOOK, whatever is chosen: the default theme, and the debug panel's always (UWDebugPanel,
    /// UWDebugTools: it stays plain, decided per user 2026-10-25).
    /// </summary>
    public static readonly Palette Plain = new Palette(UWWindowThemes.Plain);

    /// <summary>Every theme's palette, in the table's order.</summary>
    private static readonly Palette[] msPalettes = fPalettes();

    private static Palette msCurrent;

    private static bool mbGroundKnown;

    private static UWWindowBackgroundEnum meGround;

    private static Palette[] fPalettes()
    {
        Palette[] lOPalettes = new Palette[UWWindowThemes.All.Length];

        for (int liAt = 0; liAt < lOPalettes.Length; liAt++)
            lOPalettes[liAt] = UWWindowThemes.All[liAt] == UWWindowThemes.Plain ? Plain : new Palette(UWWindowThemes.All[liAt]);

        return lOPalettes;
    }

    /// <summary>
    /// THE THEME IN FORCE (UWUserSettings.WindowTheme), read once and kept until the setting changes
    /// (Changed, the setting's OnChange) - the windows ask for it many times a pass. The kit's drawers
    /// take it at the start of each pass (UWKitRows.Begin), so a switch shows at once, in the window
    /// that is open too.
    /// </summary>
    public static Palette Current
    {
        get
        {
            if (msCurrent == null)
                msCurrent = msPalettes[System.Array.IndexOf(UWWindowThemes.All, UWWindowThemes.Get(UWUserSettings.WindowTheme))];

            return msCurrent;
        }
    }

    /// <summary>What a carved theme lays under the windows (UWUserSettings.WindowBackground), kept as
    /// Current is.</summary>
    public static UWWindowBackgroundEnum Ground
    {
        get
        {
            if (!mbGroundKnown)
            {
                meGround = UWUserSettings.WindowBackground;
                mbGroundKnown = true;
            }

            return meGround;
        }
    }

    /// <summary>The theme or its ground changed: both are read again at their next use.</summary>
    public static void Changed()
    {
        msCurrent = null;
        mbGroundKnown = false;
    }

    // ------------------------------------------------- The palette of the theme in force

    // The names the windows have always read (cleanup 8.1); since 8d each is the current theme's.

    public static Color Background => Current.Background;

    public static Color Bar => Current.Bar;

    public static Color Panel => Current.Panel;

    public static Color Hover => Current.Hover;

    public static Color ButtonHover => Current.ButtonHover;

    public static Color Separator => Current.Separator;

    public static Color Text => Current.Text;

    public static Color DimText => Current.DimText;

    public static Color Accent => Current.Accent;

    public static string AccentHex => Current.AccentHex;

    public static Color Overlay => Current.Overlay;

    public static Color ReadingShade => Current.ReadingShade;

    public static Color Error => Current.Error;

    // ------------------------------------------------- The carved grounds' pictures

    /// <summary>
    /// THE PICTURES OF A CARVED GROUND (cleanup phase 8d): made by the engine-free generator
    /// (UWThemeGround) and uploaded once per theme on first use - their sizes do not follow the
    /// window: the gradient and the light are stretched over it, the clouds repeat at one texel per
    /// layout unit and the grain at one texel per screen pixel. Kept for the session, nothing per frame.
    /// </summary>
    public sealed class GroundPictures
    {
        public Texture2D Gradient;

        public Texture2D Clouds;

        public Texture2D Light;

        public Texture2D Grain;
    }

    private static readonly GroundPictures[] msPictures = new GroundPictures[UWWindowThemes.All.Length];

    /// <summary>The carved ground's pictures of a palette's theme, made on the first call (null for a
    /// flat theme).</summary>
    public static GroundPictures PicturesOf(Palette pOPalette)
    {
        UWStoneLook lOLook = pOPalette.Theme.Stone;

        if (lOLook == null)
            return null;

        int liAt = System.Array.IndexOf(UWWindowThemes.All, pOPalette.Theme);
        GroundPictures lOPictures = msPictures[liAt];

        if (lOPictures != null && lOPictures.Gradient != null && lOPictures.Clouds != null && lOPictures.Light != null && lOPictures.Grain != null)
            return lOPictures;

        string lsName = "UWGuiTheme " + pOPalette.Theme.Id + " ";

        lOPictures = new GroundPictures
        {
            Gradient = fUpload(UWThemeGround.Gradient(lOLook, UWThemeGround.GradientSize), FilterMode.Bilinear, TextureWrapMode.Clamp, lsName + "gradient"),
            Clouds = fUpload(UWThemeGround.Clouds(lOLook, UWThemeGround.CloudTileSize), FilterMode.Bilinear, TextureWrapMode.Repeat, lsName + "clouds"),
            Light = fUpload(UWThemeGround.Light(lOLook, UWThemeGround.LightSize), FilterMode.Bilinear, TextureWrapMode.Clamp, lsName + "light"),
            Grain = fUpload(UWThemeGround.Grain(lOLook, UWThemeGround.GrainSize), FilterMode.Point, TextureWrapMode.Repeat, lsName + "grain")
        };
        msPictures[liAt] = lOPictures;

        return lOPictures;
    }

    private static Texture2D fUpload(UWPicture pOPicture, FilterMode peFilter, TextureWrapMode peWrap, string psName)
    {
        // No colour help: the windows' colours never followed it (they are not the game's pictures).
        return UWTextureFactory.Upload(UWUploadPixels.FromColours(pOPicture.Pixels, pOPicture.Width, pOPicture.Height, null,
            UWUploadPixels.TransparentEnum.Keep), pOPicture.Width, pOPicture.Height, peFilter, peWrap, psName);
    }

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

    /// <summary>The hint's colour, the current theme's (a fixed value until cleanup phase 8d).</summary>
    public static Color LetterGridHint => Current.LetterGridHint;
}
