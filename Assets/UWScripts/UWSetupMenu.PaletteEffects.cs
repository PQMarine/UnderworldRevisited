using UnityEngine;
using UWDataImport.UWData;


/// <summary>
/// THE PALETTE EFFECTS DIALOG (per user, 2026-10-07): the palette renderer's own effects, each with
/// its strength and a line on what it does (UWPaletteEffects). Opened from the Graphics menu;
/// they take effect in the Low detail level, the palette path. Kept compact - one slider and one
/// short line each, Done beside the title - so all of them fit on the 720 lines the menus are
/// laid out for. MOVABLE by its title row (per user, the same day: to find a spot where the effect
/// shows while the sliders move); the place is kept while the program runs.
/// </summary>
public partial class UWSetupMenu
{
    /// <summary>The dialog's top left corner in the menu's units; NaN until first drawn (then
    /// centred under the bar). Dragged by its title row (UWGuiRows.Drag).</summary>
    private Vector2 mOPaletteDialogAt = new Vector2(float.NaN, float.NaN);

    private static readonly string[] msPresetIds = { "setup.palette.preset.off", "setup.palette.preset.subtle", "setup.palette.preset.full", "setup.palette.preset.own" };

    /// <summary>An effect's row: the label 220 wide, the slider from 230 over the row less 330, the
    /// value in the last 90; no arrows.</summary>
    private static readonly UWSliderColumns EffectColumns = new UWSliderColumns(0f, 220f, 230f, 330f, 0f, 90f, 90f);

    /// <summary>The effects' strengths move in half shade steps. The settings table keeps no limits
    /// for them; UWPaletteEffects keeps their maxima.</summary>
    private const float EffectStep = 0.5f;

    private void fOpenPaletteEffectsDialog()
    {
        fOpenDialog(DialogEnum.PaletteEffects);
    }

    /// <summary>
    /// On the kit since cleanup 8b.2 (2026-10-24), dragged by its title row as before. Sized by its
    /// text there: as wide and as tall as its rows need, the title's column with its text (the drag
    /// line moves along), Done as wide as its text with its right edge kept; unchanged in the IMGUI
    /// form and in English.
    /// </summary>
    private void fDrawPaletteEffectsDialog(float pfWidth)
    {
        float lfDialogWidth = Mathf.Min(fMaxWidth(720f, mOPassPages.InnerNeeded(mOPaletteEffectsPage)), pfWidth - 40f);
        float lfDialogHeight = fHeight(660f, 10f);

        if (float.IsNaN(mOPaletteDialogAt.x))
            mOPaletteDialogAt = new Vector2((pfWidth - lfDialogWidth) / 2f, BarHeight + 8f);

        Vector2 lOMin = new Vector2(100f - lfDialogWidth, BarHeight);
        Vector2 lOMax = new Vector2(pfWidth - 100f, UWGuiTheme.ReferenceHeight - 44f);

        // Moved while the left button holds its title row, kept on the screen with its title row
        // reachable.
        mOPaletteDialogAt = mOPassRows.Drag(new Rect(mOPaletteDialogAt.x, mOPaletteDialogAt.y, lfDialogWidth - 140f, 44f), mOPaletteDialogAt,
            lOMin, lOMax);

        // What the bar's items offered to show is not this window's to follow.
        mOKitRows.FocusToReveal(out Rect _);

        Rect lODialog = new Rect(mOPaletteDialogAt.x, mOPaletteDialogAt.y, lfDialogWidth, lfDialogHeight);

        mOPassRows.Frame(lODialog);

        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfTop = lODialog.y + 14f;
        string lsTitle = UWText.T("setup.palette.title");
        string lsDone = UWText.T("setup.done");
        float lfTitleShift = mOPassRows.SizesByText ? UWRowSizing.LabelShift(mOPassRows.TextWidth(lsTitle, UWRowInfoEnum.Heading), 230f) : 0f;
        float lfDoneExtra = mOPassRows.FitWidth(lsDone, 110f) - 110f;

        mOPassRows.Title(new Rect(lfLeft, lfTop, 220f + lfTitleShift, 26f), lsTitle);
        mOPassRows.Info(new Rect(lfLeft + 230f + lfTitleShift, lfTop + 4f, lfInner - 370f - lfTitleShift - lfDoneExtra, 22f), UWText.T("setup.palette.drag"),
            UWRowInfoEnum.Dim);

        if (mOPassRows.Action(new Rect(lODialog.xMax - 130f - lfDoneExtra, lfTop - 2f, 110f + lfDoneExtra, 30f), lsDone))
            meOpenDialog = DialogEnum.None;

        lfTop += 32f;

        // The hint, the presets, the effects, the grime's tones, Glow and Light sources
        // (fBuildPaletteEffectsPage).
        fContentEnds(lODialog, mOPassPages.Draw(mOPaletteEffectsPage, lfLeft, lfTop, lfInner));

        // THE VIEW FOLLOWS THE FOCUS (cleanup 8d.4, per user 2026-10-25): the window does not scroll,
        // but it can be dragged partly off the screen - an entry the arrow keys put the focus on there
        // moves the window by the least that shows it (below the bar, on the screen), within the drag's
        // limits; the place is kept as a drag's.
        if (mOKitRows.FocusToReveal(out Rect lOFocus))
        {
            Vector2 lOShift = new Vector2(UWKeyNavRules.RevealShift(lOFocus.xMin, lOFocus.xMax, 0f, pfWidth),
                UWKeyNavRules.RevealShift(lOFocus.yMin, lOFocus.yMax, BarHeight, UWGuiTheme.ReferenceHeight));

            mOPaletteDialogAt = new Vector2(Mathf.Clamp(mOPaletteDialogAt.x + lOShift.x, lOMin.x, lOMax.x),
                Mathf.Clamp(mOPaletteDialogAt.y + lOShift.y, lOMin.y, lOMax.y));
        }
    }
}
