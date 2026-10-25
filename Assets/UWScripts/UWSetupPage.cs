using System;
using System.Collections.Generic;
using UWDataImport.UWData;

/// <summary>A slider row's columns, measured from the row (all in layout units): the label at
/// LabelLeft, LabelWidth wide (no label when the row is given none); the slider at SliderLeft, as
/// wide as the row less SliderLess; the two arrows (none when ArrowsAt is 0) the first ArrowsAt
/// from the row's right edge, the second 30 further right; the value ValueAt from the right edge,
/// ValueWidth wide. Widths "less" than the row so each is one subtraction, as the dialogs wrote it.
/// (Moved out of UWGuiRows in cleanup step 8.4, 2026-10-24: the page tables keep them, and the
/// tables hold no type of the IMGUI drawer.)</summary>
public readonly struct UWSliderColumns
{
    public readonly float LabelLeft;

    public readonly float LabelWidth;

    public readonly float SliderLeft;

    public readonly float SliderLess;

    public readonly float ArrowsAt;

    public readonly float ValueAt;

    public readonly float ValueWidth;

    public UWSliderColumns(float pfLabelLeft, float pfLabelWidth, float pfSliderLeft, float pfSliderLess,
        float pfArrowsAt, float pfValueAt, float pfValueWidth)
    {
        LabelLeft = pfLabelLeft;
        LabelWidth = pfLabelWidth;
        SliderLeft = pfSliderLeft;
        SliderLess = pfSliderLess;
        ArrowsAt = pfArrowsAt;
        ValueAt = pfValueAt;
        ValueWidth = pfValueWidth;
    }
}

/// <summary>A choice row's columns, measured from the row: the label at LabelLeft, LabelWidth wide;
/// the choices from ChoicesLeft over the row's width less ChoicesLess, shared out evenly.</summary>
public readonly struct UWChoiceColumns
{
    public readonly float LabelLeft;

    public readonly float LabelWidth;

    public readonly float ChoicesLeft;

    public readonly float ChoicesLess;

    public UWChoiceColumns(float pfLabelLeft, float pfLabelWidth, float pfChoicesLeft, float pfChoicesLess)
    {
        LabelLeft = pfLabelLeft;
        LabelWidth = pfLabelWidth;
        ChoicesLeft = pfChoicesLeft;
        ChoicesLess = pfChoicesLess;
    }
}

/// <summary>
/// ONE ROW OF A SETUP DIALOG'S PAGE (cleanup step 8.4, 2026-10-24; Docs/CLEANUP-PLAN.md 8.7): what
/// it is, where it stands, what it shows and what a change does. A row that is a setting names its
/// entry of the settings table (Def) and takes its label, hint, choice texts, format and conditions
/// from there unless it says otherwise; a row the table does not hold (the colour help's live
/// values, the display mode, a tab, a button) brings its own reading and writing as delegates.
///
/// NO IMGUI TYPE HERE (decision 26, phase 8b): the places are plain numbers in the dialogs' layout
/// units, the texts plain strings with the row's stable Id for the text table (step 8.5). The one
/// drawer (UWSetupPageDrawer) turns the rows into UWGuiRows calls; phase 8b swaps the drawer
/// (since cleanup 8b.4 the kit's, UWKitRows, alone).
/// </summary>
public sealed class UWSetupRow
{
    public enum KindEnum
    {
        /// <summary>A dialog's title.</summary>
        Title,

        /// <summary>A dim explanation, as tall as its wrapped lines (at least MinHeight).</summary>
        Hint,

        /// <summary>A thin line between groups.</summary>
        Separator,

        /// <summary>Room and nothing else (Advance).</summary>
        Space,

        Toggle,

        /// <summary>Choices side by side without a label of their own.</summary>
        Choices,

        /// <summary>A label and the choices beside it (ChoiceColumns).</summary>
        ChoiceRow,

        /// <summary>A label, a slider, perhaps its arrows, and the value (SliderColumns).</summary>
        Slider,

        /// <summary>A button with a face.</summary>
        Action,

        /// <summary>A list row that lights up under the pointer, in the accent colour while current.</summary>
        Item,

        /// <summary>A line of text in one of the looks.</summary>
        Info,

        /// <summary>Drawn by the dialog itself (Draw): a list, a preview, a line with a button.</summary>
        Custom
    }

    /// <summary>How an Info line looks.</summary>
    public enum LookEnum
    {
        Row,
        Heading,
        Dim,
        Accent,
        Error
    }

    public KindEnum Kind;

    /// <summary>The row's stable id ("colours.title"); its own text's id in the text table is
    /// "setup." and this id ("setup.colours.title", step 8.5).</summary>
    public string Id;

    /// <summary>The setting the row shows, or null.</summary>
    public UWSettingDef Def;

    /// <summary>The row's text where it is not its setting's label or hint, taken from the text
    /// table (UWText.T) when the page is built - once, after the language is chosen at startup.</summary>
    public string Text;

    /// <summary>A text worked out on each pass (the Display hint, the palette hint by the level).</summary>
    public Func<string> TextOf;

    /// <summary>Two spaces before the label (a slider row under another, "  Strength").</summary>
    public bool Indent;

    /// <summary>The choice texts where they are not the setting's.</summary>
    public string[] Choices;

    public LookEnum Look;

    // ------------------------------------------------- Where it stands

    /// <summary>From the page's left edge.</summary>
    public float X;

    /// <summary>Its width when above 0, else the page's inner width less WidthLess.</summary>
    public float Width;

    public float WidthLess;

    public float Height = 26f;

    /// <summary>Down to the next row (a Hint adds its own height to this).</summary>
    public float Advance;

    /// <summary>A Hint's least height.</summary>
    public float MinHeight;

    /// <summary>Between the choices of a Choices row.</summary>
    public float Gap;

    /// <summary>Choices as buttons with a face, not as list rows.</summary>
    public bool Faced;

    /// <summary>A press on the current choice counts too (the colour modes: "sets it again").</summary>
    public bool PressCurrent;

    public UWSliderColumns SliderColumns;

    public UWChoiceColumns ChoiceColumns;

    // ------------------------------------------------- A slider's limits

    /// <summary>NaN: the setting's.</summary>
    public float Min = float.NaN;

    public float Max = float.NaN;

    /// <summary>A maximum worked out on each pass (the factor's 3 while 4x is locked).</summary>
    public Func<float> MaxOf;

    /// <summary>NaN: the setting's; 0: not snapped.</summary>
    public float Step = float.NaN;

    /// <summary>The result kept within the limits (else an arrow may step past them for a pass and
    /// the setting's own clamp takes it back, as the rows always did).</summary>
    public bool Clamp;

    /// <summary>None: the setting's format.</summary>
    public UWSettingFormat Format;

    /// <summary>A text of the row's own for the value (the deadzones with the stick's deflection).</summary>
    public Func<float, string> FormatWith;

    // ------------------------------------------------- Conditions

    /// <summary>Takes input only while this holds; null: the setting's EnabledWhen.</summary>
    public Func<bool> EnabledWhen;

    /// <summary>There only while this holds; null: the setting's VisibleWhen.</summary>
    public Func<bool> VisibleWhen;

    /// <summary>A Choices row's choices that take input, a bit each (null: all).</summary>
    public Func<int> EnabledChoices;

    /// <summary>A choice row's choices drawn as a plain text, a bit each (0: none): lit while current,
    /// dim otherwise, never pressed (cleanup 8d.3: the Graphics window's Custom level).</summary>
    public int TextOnlyChoices;

    // ------------------------------------------------- Reading and writing where it is no plain setting

    public Func<float> Get;

    public Action<float> Set;

    public Func<int> GetIndex;

    public Action<int> Choose;

    public Func<bool> GetOn;

    public Action<bool> SetOn;

    /// <summary>An Item is the current one.</summary>
    public Func<bool> IsCurrent;

    /// <summary>An Action's or an Item's press.</summary>
    public Action Press;

    /// <summary>What else a change through this row does, after the setting's own OnChange.</summary>
    public Action OnChange;

    /// <summary>A Custom row: draws at (left, top) over the inner width and returns where the next row
    /// stands (its own sum of steps, so a list of entries adds them one by one as before).</summary>
    public Func<float, float, float, float> Draw;
}

/// <summary>
/// A SETUP DIALOG AS A PAGE OBJECT (cleanup step 8.4, 2026-10-24): its rows top down, in the order
/// they are drawn - the arrow keys (UWKitKeyNav) step through them in that order. The dialog keeps
/// only its frame and what hangs from the frame's bottom edge (a list down to it, Back to default,
/// Done); everything that flows from the title down is a row here.
/// </summary>
public sealed class UWSetupPage
{
    public readonly string Id;

    public readonly List<UWSetupRow> Rows = new List<UWSetupRow>();

    public UWSetupPage(string psId)
    {
        Id = psId;
    }

    public UWSetupPage Add(UWSetupRow pORow)
    {
        Rows.Add(pORow);

        return this;
    }

    /// <summary>Labels the window draws itself in the page's label column, each with the width it was
    /// laid out with (the Display window's "Resolution" before its list): on the kit the column grows
    /// for them as for the page's own labels, so both line up (cleanup 8b.2, 2026-10-24).</summary>
    public readonly List<KeyValuePair<string, float>> ColumnLabels = new List<KeyValuePair<string, float>>();

    public UWSetupPage AddColumnLabel(string psText, float pfWidth)
    {
        ColumnLabels.Add(new KeyValuePair<string, float>(psText, pfWidth));

        return this;
    }
}
