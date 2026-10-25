using System;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE ONE DRAWER OF THE SETUP PAGES (cleanup step 8.4, 2026-10-24): walks a page's rows top down
/// and turns each into its UWGuiRows call, reading the value from the row's setting (or the row's
/// own Get) and writing a change back. Until then each dialog of the setup bar wrote this out per
/// row: read, draw, compare, write, Save, by hand some fifty times.
///
/// THE RULES IT KEEPS, as the dialogs had them:
///   - A row whose setting or own rule greys it is drawn greyed, and the rows after it are drawn
///     as before (the drawer switches input back on, as every dialog did after such a row).
///   - A plain setting is written through the table and the file saved at once; a row with its
///     own writing (UWSoundOptions, UWColourVision, UWWorldResolution) saves as that writing does,
///     never a second time.
///   - A slider counts as changed when the value shown moves by its format's step (a whole per
///     cent, a whole millisecond, a whole degree or multiple), or at all for the two-decimal and
///     shade-step rows - so the file is written once a shown step changes, not every frame.
///   - Then the setting's OnChange (the engine's effect, UWUserSettings hangs it on) and the row's.
///
/// ONE DRAWER FOR BOTH FORMS (cleanup 8b.1, 2026-10-24): it draws through IUWRows, the IMGUI form
/// (UWGuiRows) or the kit (UWKitRows) - the same rows, order, values and presses either way. On the
/// kit the page's LABEL COLUMN IS SIZED BY ITS TEXT (fLabelShift): where a label does not fit the
/// width it was laid out with, the column grows and everything right of it moves along. Since cleanup
/// 8b.4 the kit is the only form (the IMGUI one went with the side-by-side switch); the notes below on
/// "the IMGUI form" say what its fixed widths were.
/// </summary>
public sealed class UWSetupPageDrawer
{
    private readonly IUWRows mORows;

    public UWSetupPageDrawer(IUWRows pORows)
    {
        mORows = pORows;
    }

    /// <summary>Draws the page's rows from (pfLeft, pfTop) over pfInner; returns where the next row
    /// would stand.</summary>
    public float Draw(UWSetupPage pOPage, float pfLeft, float pfTop, float pfInner)
    {
        UWUserData lOData = UWUserSettings.Data;
        float lfTop = pfTop;
        float lfShift = fLabelShift(pOPage, lOData);

        foreach (UWSetupRow lORow in pOPage.Rows)
        {
            if (!fIsVisible(lORow, lOData))
                continue;

            bool lbGreys = lORow.EnabledWhen != null || (lORow.Def != null && lORow.Def.EnabledWhen != null);

            if (lbGreys)
                mORows.Enabled = lORow.EnabledWhen != null ? lORow.EnabledWhen() : lORow.Def.IsEnabled(lOData);

            lfTop = fDrawRow(lORow, lOData, pfLeft, lfTop, pfInner, lfShift);

            if (lbGreys)
                mORows.Enabled = true;
        }

        return lfTop;
    }

    // ------------------------------------------------- The label column sized by its text

    /// <summary>
    /// THE LABEL COLUMN SIZED BY ITS TEXT (cleanup 8b.1, 2026-10-24; decided per user the same day,
    /// after the pseudo-language showed labels running into their choices and sliders): how far the
    /// page's label column grows so its widest label fits with a margin (UWRowSizing.LabelShift) - 0
    /// in the IMGUI form, which keeps its fixed widths, and 0 wherever every label fits, so English
    /// stays as it was laid out. The column's members: a choice row's and a slider row's label, and a
    /// switch or a line of a fixed width with a row beside it on the same line (no Advance of its own:
    /// the Sound page's switches beside their volumes, its device heading beside the choices).
    /// </summary>
    private float fLabelShift(UWSetupPage pOPage, UWUserData pOData)
    {
        if (!mORows.SizesByText)
            return 0f;

        float lfShift = 0f;

        foreach (UWSetupRow lORow in pOPage.Rows)
        {
            if (!fIsVisible(lORow, pOData))
                continue;

            float lfText;
            float lfRoom;

            switch (lORow.Kind)
            {
                case UWSetupRow.KindEnum.ChoiceRow:
                    lfText = mORows.LabelWidth(fLabel(lORow));
                    lfRoom = lORow.ChoiceColumns.LabelWidth;
                    break;

                case UWSetupRow.KindEnum.Slider when lORow.SliderColumns.LabelWidth > 0f:
                    lfText = mORows.LabelWidth(fLabel(lORow));
                    lfRoom = lORow.SliderColumns.LabelWidth;
                    break;

                case UWSetupRow.KindEnum.Toggle when fBesideNext(lORow):
                    lfText = mORows.ToggleWidth(fToggleText(lORow));
                    lfRoom = lORow.Width;
                    break;

                case UWSetupRow.KindEnum.Info when fBesideNext(lORow):
                    lfText = mORows.TextWidth(fLabel(lORow), fLook(lORow.Look));
                    lfRoom = lORow.Width;
                    break;

                // A button or a list row with one beside it (the Controls window's first tab, cleanup
                // 8b.2): as wide as its text and padding need, the rows right of it move along.
                case UWSetupRow.KindEnum.Action when fBesideNext(lORow):
                case UWSetupRow.KindEnum.Item when fBesideNext(lORow):
                    lfShift = Math.Max(lfShift, mORows.FitWidth(fLabel(lORow), lORow.Width) - lORow.Width);
                    continue;

                default:
                    continue;
            }

            lfShift = Math.Max(lfShift, UWRowSizing.LabelShift(lfText, lfRoom));
        }

        // The labels the window draws itself in the column (cleanup 8b.2).
        foreach (System.Collections.Generic.KeyValuePair<string, float> lOLabel in pOPage.ColumnLabels)
            lfShift = Math.Max(lfShift, UWRowSizing.LabelShift(mORows.LabelWidth(lOLabel.Key), lOLabel.Value));

        return lfShift;
    }

    /// <summary>A row of a fixed width at the page's left edge with the next row beside it on the
    /// same line.</summary>
    private static bool fBesideNext(UWSetupRow pORow)
    {
        return pORow.Width > 0f && pORow.X <= 0f && pORow.Advance == 0f;
    }

    /// <summary>Where a row stands with the label column grown by pfShift: a member of the column
    /// grows with it, a row right of it (X above 0) moves along and keeps its right edge.</summary>
    private static void fShifted(UWSetupRow pORow, float pfShift, ref float pfX, ref float pfWidth, ref float pfWidthLess)
    {
        if (pORow.X > 0f)
        {
            pfX += pfShift;

            if (pfWidth <= 0f)
                pfWidthLess += pfShift;
        }
        else if ((pORow.Kind == UWSetupRow.KindEnum.Toggle || pORow.Kind == UWSetupRow.KindEnum.Info
                     || pORow.Kind == UWSetupRow.KindEnum.Action || pORow.Kind == UWSetupRow.KindEnum.Item) && fBesideNext(pORow))
        {
            pfWidth += pfShift;
        }
    }

    /// <summary>A choice row's columns with the label column grown by pfShift (the choices keep the
    /// row's right edge).</summary>
    private static UWChoiceColumns fShifted(in UWChoiceColumns pOColumns, float pfShift)
    {
        return new UWChoiceColumns(pOColumns.LabelLeft, pOColumns.LabelWidth + pfShift, pOColumns.ChoicesLeft + pfShift,
            pOColumns.ChoicesLess + pfShift);
    }

    /// <summary>A slider row's columns with the label column grown by pfShift (the slider keeps its
    /// right end, the arrows and the value their places from the right edge).</summary>
    private static UWSliderColumns fShifted(in UWSliderColumns pOColumns, float pfShift)
    {
        if (pOColumns.LabelWidth <= 0f)
            return pOColumns;

        return new UWSliderColumns(pOColumns.LabelLeft, pOColumns.LabelWidth + pfShift, pOColumns.SliderLeft + pfShift,
            pOColumns.SliderLess + pfShift, pOColumns.ArrowsAt, pOColumns.ValueAt, pOColumns.ValueWidth);
    }

    private static bool fIsVisible(UWSetupRow pORow, UWUserData pOData)
    {
        if (pORow.VisibleWhen != null)
            return pORow.VisibleWhen();

        return pORow.Def == null || pORow.Def.IsVisible(pOData);
    }

    /// <summary>One row; returns where the next one stands. Each row moves down by its own sum, in
    /// the order the dialogs added them, so every place comes out as before to the last bit.</summary>
    private float fDrawRow(UWSetupRow pORow, UWUserData pOData, float pfLeft, float pfTop, float pfInner, float pfShift)
    {
        float lfRowX = pORow.X;
        float lfRowWidth = pORow.Width;
        float lfRowLess = pORow.WidthLess;

        // Only where the label column grew (the kit, a label too long for its width); otherwise every
        // place comes out of the same expressions as before.
        if (pfShift > 0f)
            fShifted(pORow, pfShift, ref lfRowX, ref lfRowWidth, ref lfRowLess);

        float lfX = pfLeft + lfRowX;
        float lfWidth = lfRowWidth > 0f ? lfRowWidth : pfInner - lfRowLess;
        Rect lORect = new Rect(lfX, pfTop, lfWidth, pORow.Height);

        switch (pORow.Kind)
        {
            case UWSetupRow.KindEnum.Title:
                mORows.Title(lORect, fLabel(pORow));
                break;

            case UWSetupRow.KindEnum.Hint:
            {
                string lsHint = pORow.TextOf != null ? pORow.TextOf() : (pORow.Text ?? pORow.Def.Hint);

                return pfTop + (Mathf.Max(pORow.MinHeight, mORows.Hint(lfX, pfTop, lfWidth, lsHint)) + pORow.Advance);
            }

            case UWSetupRow.KindEnum.Separator:
                mORows.Separator(lORect);
                break;

            case UWSetupRow.KindEnum.Space:
                break;

            case UWSetupRow.KindEnum.Toggle:
                fToggle(pORow, pOData, lORect);
                break;

            case UWSetupRow.KindEnum.Choices:
            case UWSetupRow.KindEnum.ChoiceRow:
                fChoices(pORow, pOData, lORect, pfShift > 0f ? fShifted(pORow.ChoiceColumns, pfShift) : pORow.ChoiceColumns);
                break;

            case UWSetupRow.KindEnum.Slider:
                fSlider(pORow, pOData, lORect, pfShift > 0f ? fShifted(pORow.SliderColumns, pfShift) : pORow.SliderColumns);
                break;

            // A button or list row of a fixed width is as wide as its text needs on the kit (cleanup
            // 8b.2: FitWidth, the width laid out in the IMGUI form).
            case UWSetupRow.KindEnum.Action:
                if (mORows.Action(fFitted(pORow, lORect), fLabel(pORow)))
                    pORow.Press();
                break;

            case UWSetupRow.KindEnum.Item:
                if (mORows.Item(fFitted(pORow, lORect), fLabel(pORow), pORow.IsCurrent != null && pORow.IsCurrent()))
                    pORow.Press();
                break;

            case UWSetupRow.KindEnum.Info:
                mORows.Info(lORect, fLabel(pORow), fLook(pORow.Look));
                break;

            case UWSetupRow.KindEnum.Custom:
                return pORow.Draw(pfLeft, pfTop, pfInner);
        }

        return pfTop + pORow.Advance;
    }

    private Rect fFitted(UWSetupRow pORow, Rect pORect)
    {
        return pORow.Width > 0f ? new Rect(pORect.x, pORect.y, mORows.FitWidth(fLabel(pORow), pORect.width), pORect.height) : pORect;
    }

    private static string fLabel(UWSetupRow pORow)
    {
        string lsText = pORow.TextOf != null ? pORow.TextOf() : (pORow.Text ?? (pORow.Def != null ? pORow.Def.Label : null));

        return pORow.Indent ? "  " + lsText : lsText;
    }

    /// <summary>The switch's box and its text two spaces apart, as every toggle of the dialogs.</summary>
    private static string fToggleText(UWSetupRow pORow)
    {
        return "  " + fLabel(pORow);
    }

    private void fToggle(UWSetupRow pORow, UWUserData pOData, Rect pORect)
    {
        bool lbBefore = pORow.GetOn != null ? pORow.GetOn() : UWUserSettingsTable.ReadSwitch(pORow.Def, pOData);

        bool lbAfter = mORows.Toggle(pORect, lbBefore, fToggleText(pORow));

        if (lbAfter == lbBefore)
            return;

        if (pORow.SetOn != null)
            pORow.SetOn(lbAfter);
        else
        {
            UWUserSettingsTable.WriteSwitch(pORow.Def, pOData, lbAfter);
            UWUserSettings.Save();
        }

        fChanged(pORow, pOData);
    }

    private void fChoices(UWSetupRow pORow, UWUserData pOData, Rect pORect, in UWChoiceColumns pOColumns)
    {
        string[] lsChoices = pORow.Choices ?? pORow.Def.ChoiceLabels;
        int liCurrent = pORow.GetIndex != null ? pORow.GetIndex() : fIndexOf(pORow.Def, pOData);
        int liPressed = pORow.Kind == UWSetupRow.KindEnum.ChoiceRow
            ? mORows.ChoiceRow(pORect, pOColumns, fLabel(pORow), lsChoices, liCurrent, pORow.TextOnlyChoices)
            : mORows.Choices(pORect, lsChoices, liCurrent, pORow.Gap, pORow.Faced, pORow.EnabledChoices != null ? pORow.EnabledChoices() : -1,
                pORow.TextOnlyChoices);

        if (liPressed < 0 || (liPressed == liCurrent && !pORow.PressCurrent))
            return;

        if (pORow.Choose != null)
            pORow.Choose(liPressed);
        else
        {
            if (pORow.Def is UWIntSettingDef lOInt && lOInt.Kind != UWSettingKind.Bool)
                lOInt.Write(pOData, lOInt.ValueAt(liPressed));
            else
                UWUserSettingsTable.WriteSwitch(pORow.Def, pOData, (liPressed == 1) != pORow.Def.Invert);

            UWUserSettings.Save();
        }

        fChanged(pORow, pOData);
    }

    /// <summary>The choice a setting's row shows: a switch's place, else the value's among the
    /// setting's choices.</summary>
    private static int fIndexOf(UWSettingDef pODef, UWUserData pOData)
    {
        if (pODef.Kind == UWSettingKind.Bool)
            return UWUserSettingsTable.SwitchIndex(pODef, pOData);

        return ((UWIntSettingDef)pODef).IndexOf(pOData);
    }

    private void fSlider(UWSetupRow pORow, UWUserData pOData, Rect pORect, in UWSliderColumns pOColumns)
    {
        UWFloatSettingDef lODef = pORow.Def as UWFloatSettingDef;
        float lfBefore = pORow.Get != null ? pORow.Get() : lODef.Read(pOData);
        float lfMin = !float.IsNaN(pORow.Min) ? pORow.Min : lODef.Min;
        float lfMax = pORow.MaxOf != null ? pORow.MaxOf() : (!float.IsNaN(pORow.Max) ? pORow.Max : lODef.Max);
        float lfStep = !float.IsNaN(pORow.Step) ? pORow.Step : lODef.Step;
        UWSettingFormat leFormat = pORow.Format != UWSettingFormat.None ? pORow.Format : (pORow.Def != null ? pORow.Def.Format : UWSettingFormat.None);
        // No label where the columns leave it none (a volume beside its switch, the factor under its).
        string lsLabel = pOColumns.LabelWidth > 0f ? fLabel(pORow) : null;
        Func<float, string> lFFormat = pORow.FormatWith ?? FormatOf(leFormat);

        float lfAfter = mORows.Slider(pORect, mORows.SizesByText ? fValueSized(pORow, pOColumns, lFFormat, lfMin, lfMax, lfBefore) : pOColumns,
            lsLabel, lfBefore, lfMin, lfMax, lfStep, pORow.Clamp, lFFormat);

        if (!IsChanged(leFormat, lfBefore, lfAfter))
            return;

        if (pORow.Set != null)
            pORow.Set(lfAfter);
        else
        {
            lODef.Write(pOData, lfAfter);
            UWUserSettings.Save();
        }

        fChanged(pORow, pOData);
    }

    private static void fChanged(UWSetupRow pORow, UWUserData pOData)
    {
        if (pORow.Def != null && pORow.Def.OnChange != null)
            pORow.Def.OnChange(pOData);

        if (pORow.OnChange != null)
            pORow.OnChange();
    }

    private static UWRowInfoEnum fLook(UWSetupRow.LookEnum peLook)
    {
        switch (peLook)
        {
            case UWSetupRow.LookEnum.Heading: return UWRowInfoEnum.Heading;
            case UWSetupRow.LookEnum.Dim: return UWRowInfoEnum.Dim;
            case UWSetupRow.LookEnum.Accent: return UWRowInfoEnum.Accent;
            case UWSetupRow.LookEnum.Error: return UWRowInfoEnum.Error;
            default: return UWRowInfoEnum.Row;
        }
    }

    // ------------------------------------------------- Formats

    private static readonly Func<float, string> msPercent = pfValue => Mathf.RoundToInt(pfValue * 100f) + " %";

    private static readonly Func<float, string> msTwoDecimals =
        pfValue => pfValue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly Func<float, string> msMilliseconds = pfValue => Mathf.RoundToInt(pfValue * 1000f) + " ms";

    private static readonly Func<float, string> msDegrees = pfValue => Mathf.RoundToInt(pfValue) + "°";

    private static readonly Func<float, string> msFactor = pfValue => Mathf.RoundToInt(pfValue) + "x";

    private static readonly Func<float, string> msShadeSteps = pfValue => pfValue <= 0f ? UWText.T("setup.format.steps_off") : UWText.F("setup.format.steps", pfValue.ToString("0.#"));


    private static readonly Func<float, string> msWholePercent = pfValue => Mathf.RoundToInt(pfValue) + " %";

    private static readonly Func<float, string> msNumber = pfValue => Mathf.RoundToInt(pfValue).ToString();

    /// <summary>How a format writes a value (the dialogs' own texts).</summary>
    public static Func<float, string> FormatOf(UWSettingFormat peFormat)
    {
        switch (peFormat)
        {
            case UWSettingFormat.Percent: return msPercent;
            case UWSettingFormat.TwoDecimals: return msTwoDecimals;
            case UWSettingFormat.Milliseconds: return msMilliseconds;
            case UWSettingFormat.Degrees: return msDegrees;
            case UWSettingFormat.Factor: return msFactor;
            case UWSettingFormat.ShadeSteps: return msShadeSteps;
            case UWSettingFormat.WholePercent: return msWholePercent;
            case UWSettingFormat.Number: return msNumber;
            default: return pfValue => string.Empty;
        }
    }

    /// <summary>Whether a slider moved by a step its format shows.</summary>
    public static bool IsChanged(UWSettingFormat peFormat, float pfBefore, float pfAfter)
    {
        switch (peFormat)
        {
            case UWSettingFormat.Percent:
                return Mathf.RoundToInt(pfAfter * 100f) != Mathf.RoundToInt(pfBefore * 100f);

            case UWSettingFormat.Milliseconds:
                return Mathf.RoundToInt(pfAfter * 1000f) != Mathf.RoundToInt(pfBefore * 1000f);

            case UWSettingFormat.Degrees:
            case UWSettingFormat.Factor:
            case UWSettingFormat.WholePercent:
            case UWSettingFormat.Number:
                return Mathf.RoundToInt(pfAfter) != Mathf.RoundToInt(pfBefore);

            default:
                return !Mathf.Approximately(pfAfter, pfBefore);
        }
    }

    // ------------------------------------------------- The windows sized by their text (cleanup 8b.2)

    /// <summary>The widest value each slider row has shown in this language - it only grows, so the
    /// row does not jump while a value with fewer digits is dragged past.</summary>
    private readonly System.Collections.Generic.Dictionary<UWSetupRow, float> mOValueWidths =
        new System.Collections.Generic.Dictionary<UWSetupRow, float>();

    private int miValueLanguage = -1;

    /// <summary>
    /// A SLIDER'S VALUE COLUMN SIZED BY ITS TEXT (cleanup 8b.2, 2026-10-24; the pseudo-language's
    /// "[0,5 steeps]" ran out of the palette rows' 90): on the kit the value column is as wide as the
    /// widest of the row's least, greatest and present value (and any it showed before), growing to
    /// the left - the arrows and the slider's right end move along.
    /// </summary>
    private UWSliderColumns fValueSized(UWSetupRow pORow, in UWSliderColumns pOColumns, Func<float, string> pFFormat, float pfMin, float pfMax,
        float pfValue)
    {
        if (miValueLanguage != UWText.LanguageVersion)
        {
            miValueLanguage = UWText.LanguageVersion;
            mOValueWidths.Clear();
        }

        mOValueWidths.TryGetValue(pORow, out float lfWidest);

        lfWidest = Math.Max(lfWidest, Math.Max(mORows.LabelWidth(pFFormat(pfMin)), Math.Max(mORows.LabelWidth(pFFormat(pfMax)), mORows.LabelWidth(pFFormat(pfValue)))));
        mOValueWidths[pORow] = lfWidest;

        return WithValueWidth(pOColumns, lfWidest);
    }

    /// <summary>How far the page's label column grows on the kit (0 in the IMGUI form) - for the labels
    /// the window draws itself in it (UWSetupPage.ColumnLabels) and what stands right of them.</summary>
    public float LabelShift(UWSetupPage pOPage)
    {
        return fLabelShift(pOPage, UWUserSettings.Data);
    }

    /// <summary>A slider row's columns with the value column at least pfWidth wide: what it lacks is
    /// taken from the left, the arrows and the slider's right end move along (the value keeps its
    /// right edge).</summary>
    public static UWSliderColumns WithValueWidth(in UWSliderColumns pOColumns, float pfWidth)
    {
        float lfExtra = pfWidth - pOColumns.ValueWidth;

        if (lfExtra <= 0f)
            return pOColumns;

        return new UWSliderColumns(pOColumns.LabelLeft, pOColumns.LabelWidth, pOColumns.SliderLeft, pOColumns.SliderLess + lfExtra,
            pOColumns.ArrowsAt > 0f ? pOColumns.ArrowsAt + lfExtra : 0f, pOColumns.ValueAt + lfExtra, pOColumns.ValueWidth + lfExtra);
    }

    /// <summary>A slider row's columns with its label column grown to the label's text where the
    /// drawer sizes by text (a row the window draws itself, the view angle's) - unchanged in the
    /// IMGUI form.</summary>
    public static UWSliderColumns WithLabel(IUWRows pORows, in UWSliderColumns pOColumns, string psLabel)
    {
        if (!pORows.SizesByText || pOColumns.LabelWidth <= 0f)
            return pOColumns;

        float lfShift = UWRowSizing.LabelShift(pORows.LabelWidth(psLabel), pOColumns.LabelWidth);

        return lfShift > 0f ? fShifted(pOColumns, lfShift) : pOColumns;
    }

    /// <summary>
    /// THE INNER WIDTH A PAGE NEEDS (cleanup 8b.2, 2026-10-24): on the kit, the inner width at which
    /// every row of the page shows its text whole - a full-width switch or line, a row of choices with
    /// their texts and padding beside its label column (grown as Draw grows it); 0 in the IMGUI form,
    /// which keeps its widths. A window narrower than this grows by the rest where the screen allows
    /// (UWSetupMenu.fMaxWidth): the pseudo-language ran the Colours modes and the Controls switches out
    /// of their windows. In English every window's own width is more than its page needs.
    /// </summary>
    public float InnerNeeded(UWSetupPage pOPage)
    {
        if (!mORows.SizesByText)
            return 0f;

        UWUserData lOData = UWUserSettings.Data;
        float lfShift = fLabelShift(pOPage, lOData);
        float lfNeeded = 0f;

        foreach (UWSetupRow lORow in pOPage.Rows)
        {
            if (!fIsVisible(lORow, lOData) || fBesideNext(lORow))
                continue;

            // Where the row starts and what it leaves free at the page's right edge (fShifted).
            float lfLeft = lORow.X > 0f ? lORow.X + lfShift : lORow.X;
            float lfLess = lORow.WidthLess + (lORow.X > 0f ? lfShift : 0f);
            float lfContent;

            switch (lORow.Kind)
            {
                case UWSetupRow.KindEnum.Toggle:
                    lfContent = mORows.ToggleWidth(fToggleText(lORow));
                    break;

                case UWSetupRow.KindEnum.Info when lORow.Look == UWSetupRow.LookEnum.Row || lORow.Look == UWSetupRow.LookEnum.Heading:
                    lfContent = mORows.TextWidth(fLabel(lORow), fLook(lORow.Look));
                    break;

                case UWSetupRow.KindEnum.Choices:
                    lfContent = fChoicesWidth(lORow.Choices ?? lORow.Def.ChoiceLabels, lORow.Gap);
                    break;

                case UWSetupRow.KindEnum.ChoiceRow:
                    // The choices over the row less the (grown) columns before them.
                    lfContent = fChoicesWidth(lORow.Choices ?? lORow.Def.ChoiceLabels, 0f) + lORow.ChoiceColumns.ChoicesLess + lfShift;
                    break;

                default:
                    continue;
            }

            // A row of a fixed width keeps it; its content runs from its left edge.
            lfNeeded = Math.Max(lfNeeded, lORow.Width > 0f ? lfLeft + Math.Max(lORow.Width, lfContent) : lfContent + lfLess);
        }

        return lfNeeded;
    }

    /// <summary>A row of choices as the kit lays it out where the texts do not fit their shares: each
    /// its text, the gap and the padding on both sides (UWRowSizing.SegmentWidths).</summary>
    private float fChoicesWidth(string[] psChoices, float pfGap)
    {
        float lfWidth = 0f;

        foreach (string lsChoice in psChoices)
            lfWidth += mORows.LabelWidth(lsChoice) + pfGap + (2f * UWRowSizing.Padding);

        return lfWidth;
    }
}
