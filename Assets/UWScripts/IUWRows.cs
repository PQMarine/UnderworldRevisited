using System;
using UnityEngine;

/// <summary>How a line of text looks (IUWRows.Info; UWRowInfoEnum until cleanup 8b.4, 2026-10-24,
/// when the IMGUI drawer went).</summary>
public enum UWRowInfoEnum
{
    /// <summary>A row's own text, as its label.</summary>
    Row,

    /// <summary>The larger text of a dialog's title.</summary>
    Heading,

    /// <summary>A dim note: a hint, a notice.</summary>
    Dim,

    /// <summary>A good state in the accent colour.</summary>
    Accent,

    /// <summary>A failure.</summary>
    Error
}

/// <summary>
/// WHICH WINDOW A ROW DRAWER DRAWS (UWGuiRows.LookEnum until cleanup 8b.4, 2026-10-24): the same row
/// kinds in each window's own type - the setup bar's 16 points, the layout editor's 15 with its
/// smaller hints, the modern game menu in the modern font.
/// </summary>
public enum UWRowLookEnum
{
    /// <summary>The setup bar, its dialogs, the first-run screen.</summary>
    Setup,

    /// <summary>The layout editor's window (UWModernLayoutEditor).</summary>
    LayoutEditor,

    /// <summary>The modern scheme's game menu (UWModernGameMenu): the modern font, the setup rows'
    /// 16 points for its name field; its titles and buttons in the help pieces' looks.</summary>
    GameMenu,

    /// <summary>The gamepad's letter grid (UWLetterGrid): the IMGUI skin's own font (LegacyRuntime),
    /// its keys and hints in the help pieces' looks.</summary>
    LetterGrid
}

/// <summary>
/// THE ROWS OF THE PORT'S WINDOWS AS ONE VOCABULARY (cleanup 8b.1, 2026-10-24): what UWGuiRows draws
/// with IMGUI and UWKitRows on the uGUI kit - a window's frame, its title, hints and lines of text,
/// switches, choices, slider rows with their arrows and value, steppers, rebinding rows, buttons, list
/// rows, a scrolled area, a drag handle, the setup bar and its items, a text field, a separator. The
/// page drawer (UWSetupPageDrawer) and the dialogs' frames talk to this, so a window moves from IMGUI
/// to the kit by the drawer it is handed, fed by the same page tables (phase 8b's rule).
///
/// FED WITH PLAIN VALUES like UWGuiRows: rectangles in the windows' layout units (720 lines high,
/// UWGuiTheme.ReferenceHeight), texts, values; a press comes back as the result. Each drawer keeps its
/// own way of taking clicks and keys: IMGUI its events (UWImguiInput, UWImguiKeyNav), the kit the
/// Input System through the modern root's gate (UWModernUi.WindowTakesPointer, UWKitKeyNav).
///
/// SIZED BY TEXT (decided per user 2026-10-24): the kit sizes label columns, choices and bar items by
/// their text (UWRowSizing), the IMGUI form kept its fixed widths - SizesByText says which, and the
/// measures below give the page drawer and the dialogs what they need for it.
///
/// ONE DRAWER LEFT (cleanup 8b.4, 2026-10-24): with the last window on the kit UWGuiRows, the IMGUI
/// form, went with the side-by-side switch; UWKitRows is the one implementation (SizesByText always
/// on). The vocabulary stays: the page tables, their drawer and the windows talk to it, not to the kit.
/// </summary>
public interface IUWRows
{
    /// <summary>Whether the rows drawn from here on take input; off they are drawn greyed.</summary>
    bool Enabled { get; set; }

    /// <summary>Label columns, choices, buttons and bar items grow with their text (the kit), or keep
    /// the widths they were laid out with (the IMGUI form).</summary>
    bool SizesByText { get; }

    /// <summary>A text's width in one of the looks of a line, in layout units.</summary>
    float TextWidth(string psText, UWRowInfoEnum peLook);

    /// <summary>A text's height in one of the looks of a line, wrapped at pfWidth where the look wraps
    /// (cleanup 8b.4, 2026-10-24: the layout editor's hints grow with their text on the kit).</summary>
    float TextHeight(string psText, UWRowInfoEnum peLook, float pfWidth);

    /// <summary>A switch's width with its text: the box and the text beside it.</summary>
    float ToggleWidth(string psText);

    /// <summary>The width a button or a bar item with this text gets where pfNominal was laid out:
    /// pfNominal in the IMGUI form, at least the text and its padding on the kit (UWRowSizing.Fit).</summary>
    float FitWidth(string psText, float pfNominal);

    // ------------------------------------------------- Frames

    Rect Dialog(float pfScreenWidth, float pfMaxWidth, float pfTop, float pfHeight);

    void Frame(Rect pORect);

    void Title(Rect pORect, string psText);

    bool Done(Rect pODialog);

    float Hint(float pfLeft, float pfTop, float pfWidth, string psText);

    void Separator(Rect pORect);

    void Picture(Rect pORect, Texture pOTexture);

    Vector2 BeginScroll(Rect pOView, Vector2 pOScroll, Rect pOContent);

    void EndScroll();

    Vector2 Drag(Rect pOHandle, Vector2 pOAt, Vector2 pOMin, Vector2 pOMax);

    // ------------------------------------------------- Lines

    void Label(Rect pORect, string psText);

    float LabelWidth(string psText);

    void Info(Rect pORect, string psText, UWRowInfoEnum peLook);

    // ------------------------------------------------- Buttons and rows

    bool Action(Rect pORect, string psText);

    bool Item(Rect pORect, string psText, bool pbCurrent = false);

    bool Toggle(Rect pORect, bool pbValue, string psText);

    /// <summary>Choices side by side; bit n of piEnabled off greys choice n, bit n of piTextOnly on draws
    /// it as a plain text (cleanup 8d.3: the Graphics window's Custom) - lit in the accent colour while
    /// current, dim otherwise, no press and no step of the arrow keys.</summary>
    int Choices(Rect pOArea, string[] psChoices, int piCurrent, float pfGap = 0f, bool pbFaced = false, int piEnabled = -1, int piTextOnly = 0);

    int ChoiceRow(Rect pORow, in UWChoiceColumns pOColumns, string psLabel, string[] psChoices, int piCurrent, int piTextOnly = 0);

    float Slider(Rect pORow, in UWSliderColumns pOColumns, string psLabel, float pfValue, float pfMin, float pfMax,
        float pfStep, bool pbClamp, Func<float, string> pFFormat);

    int Stepper(Rect pORow, float pfLabelWidth, float pfButtonWidth, string psLabel, string psValue, bool pbCanDown, bool pbCanUp);

    bool Rebind(Rect pORow, string psLabel, string psKey, Texture2D pOGlyph, bool pbPad);

    // ------------------------------------------------- The layout editor's rows (cleanup 8b.4)

    /// <summary>Named segments, each as wide as its name needs and the rest shared out, the chosen one
    /// lit; returns the chosen one, a newly pressed one if any (the editor's Preset row).</summary>
    int Segments(Rect pORect, int piChosen, string[] psNames);

    /// <summary>Segments sharing the rectangle, the current one lit; returns the one chosen now (the
    /// current one when none was pressed) - the editor's choice rows.</summary>
    int Toolbar(Rect pORect, int piCurrent, string[] psChoices);

    /// <summary>Choices shown as samples of themselves (the backs' colours and patterns); returns the
    /// sample pressed (not the chosen one), or -1.</summary>
    int Samples(float pfX, float pfY, float pfEach, float pfHeight, Texture2D[] paPictures, int piChosen, bool pbEnabled);

    // ------------------------------------------------- The bar and the first-run screen

    void Bar(Rect pORect);

    bool BarItem(Rect pORect, string psText, bool pbOpen);

    /// <summary>The darkest ground (the folder list's), flat in every theme.</summary>
    void Backdrop(Rect pORect);

    /// <summary>The first-run screen's ground over the whole screen: the darkest ground's colour, or
    /// a carved theme's stone (cleanup phase 8d).</summary>
    void ScreenGround(Rect pORect);

    void BigTitle(Rect pORect, string psText);

    // ------------------------------------------------- Typing

    /// <summary>A line to type in, named psName for EnterIn and EscapeIn; returns its text. piMaxLength
    /// above 0 limits it.</summary>
    string TextField(Rect pORect, string psName, string psText, int piMaxLength = 0);

    /// <summary>Enter in the text field named psName this pass: true (and taken).</summary>
    bool EnterIn(string psName);

    /// <summary>Escape in the text field named psName this pass: true (and taken).</summary>
    bool EscapeIn(string psName);
}
