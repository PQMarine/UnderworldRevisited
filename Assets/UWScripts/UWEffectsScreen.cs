using System;
using System.Collections.Generic;
using UnityEngine;
using UWDataImport.Presentation;
using UWDataImport.UWData;
using UnderworldRevisited;

/// <summary>
/// THE REMASTERED EFFECTS WINDOW (per user, 2026-09-17): every Remastered effect on its own, the
/// viewer's own values of the detail level Custom (UWGraphicsDetail.LevelEnum.VeryHigh). Changes
/// apply at once: the values go to UWGraphicsDetail.CustomValues and from there into the settings,
/// which the Remastered renderer reads every frame; the grime along the walls and its colour are the
/// setting both render modes share (UWUserSettings). Closing it saves both.
///
/// REWORKED 2026-10-07 (per user: "Bau es in den Remastered Dialog ein. Dann mit Scrolling. Den
/// Remastered Dialog auch verschiebbar machen. On Off und Done nach oben."): the grime and its tone are
/// rows here too; ALL ON / OFF and DONE stand in the title row; the rows scroll when there are more
/// than fit; and the window can be dragged by its title row.
///
/// ON THE KIT, A WINDOW OF THE SETUP BAR (cleanup 8d.3, 2026-10-25, per user): until then one marbled
/// 320x200 picture in the original's colours (the stone of OPTBTNS.GR, its ON, OFF and DONE buttons, a
/// built-in-font lettering over it) in a frame of its own - over the main menu's, the game's, or a canvas
/// of its own at order 500 - that no longer fitted beside the themed windows. Now it is drawn by the
/// setup bar's kit drawer (UWSetupMenu, DialogEnum.RemasteredEffects) in the theme in force like the
/// bar's other windows: the title row with "All:", On, Off and Done, the rows below in a scrolled area,
/// the label and value columns sized by their text, F10 and the arrows stepping through it with the bar,
/// the pad's pointer pressing and dragging as the mouse. The bar opens it from the Graphics window's
/// "Remastered effects..." and from the classic options' Very High (UWSetupMenu.OpenEffectsWindow); the
/// classic options keep no screen of their own any more. Escape, the pad's B and Start close it first
/// (the back chain's SetupEffects row).
///
/// THE ROWS: the Row list below is the table, as before - label, kind, range, reading and writing,
/// ALL ON / OFF - and the window draws it as a setup page (UWSetupPage, built once from it) through the
/// bar's page drawer, the tables' shape unchanged: a Slider row a slider with its value in two decimals
/// (three below 0.5), a Steps row a slider in whole steps, a Toggle row a switch, the tonemapping and the
/// grime's colour a row of choices. UWEffectsRowsCheck holds it against the picture's record (Describe).
/// </summary>
public sealed class UWEffectsScreen
{
    private enum KindEnum
    {
        Toggle,
        Slider,
        Steps,
        Tonemapping,
        GrimeTone
    }

    private class Row
    {
        /// <summary>The text id of its label (read when the page is built).</summary>
        public string LabelId;
        public KindEnum Kind;
        public float Min;
        public float Max;
        public Func<UWGraphicsDetail.EffectValues, float> Get;
        public Action<UWGraphicsDetail.EffectValues, float> Set;

        /// <summary>For a row outside the effect values (the grime): what ALL ON and ALL OFF set
        /// (NaN: they leave it).</summary>
        public float AllOn = float.NaN;
        public float AllOff = float.NaN;
    }

    private readonly List<Row> mORows = new List<Row>();

    /// <summary>The page the window draws, a row for each of mORows in the same order; built anew when
    /// the language changes (its labels and choices are taken when it is built).</summary>
    private UWSetupPage mOPage;

    /// <summary>The language mOPage was built in (UWText.LanguageVersion).</summary>
    private int miPageLanguage;

    /// <summary>The values the rows read and write: the viewer's own (Describe swaps in its probe).</summary>
    private Func<UWGraphicsDetail.EffectValues> mFValues = () => UWGraphicsDetail.CustomValues;

    public bool IsOpen { get; private set; }

    public UWEffectsScreen()
    {
        fAddToggle("effects.torch_and_fire_lights", v => v.TorchLights, (v, b) => v.TorchLights = b);
        fAddToggle("effects.torch_shadows", v => v.TorchShadows, (v, b) => v.TorchShadows = b);
        fAddRow("effects.shadows_at_once", KindEnum.Steps, 0f, 8f, v => v.MaxShadowLights, (v, f) => v.MaxShadowLights = Mathf.RoundToInt(f));
        fAddToggle("effects.lava_lights", v => v.LavaLights, (v, b) => v.LavaLights = b);
        fAddRow("effects.light_brightness", KindEnum.Slider, 0f, 3f, v => v.LightBrightness, (v, f) => v.LightBrightness = f);
        fAddRow("effects.relief", KindEnum.Slider, 0f, 4f, v => v.NormalStrength, (v, f) => v.NormalStrength = f);
        fAddRow("effects.shine", KindEnum.Slider, 0f, 2f, v => v.SpecularStrength, (v, f) => v.SpecularStrength = f);
        fAddRow("effects.wall_depth", KindEnum.Slider, 0f, 0.2f, v => v.ParallaxDepth, (v, f) => v.ParallaxDepth = f);
        fAddRow("effects.relief_shadows", KindEnum.Slider, 0f, 1f, v => v.SelfShadow, (v, f) => v.SelfShadow = f);
        fAddRow("effects.dark_joints", KindEnum.Slider, 0f, 1f, v => v.Cavity, (v, f) => v.Cavity = f);
        fAddRow("effects.lava_glow", KindEnum.Slider, 0f, 4f, v => v.EmissiveStrength, (v, f) => v.EmissiveStrength = f);
        fAddRow("effects.bloom", KindEnum.Slider, 0f, 3f, v => v.BloomIntensity, (v, f) => v.BloomIntensity = f);
        fAddToggle("effects.ambient_occlusion", v => v.AmbientOcclusion, (v, b) => v.AmbientOcclusion = b);
        fAddRow("effects.tonemapping", KindEnum.Tonemapping, 0f, 2f, v => (float)v.Tonemapping,
            (v, f) => v.Tonemapping = (UWSettings.RemasterTonemappingEnum)Mathf.RoundToInt(f));
        fAddRow("effects.sprite_glow", KindEnum.Slider, 0f, 3f, v => v.SpriteGlow, (v, f) => v.SpriteGlow = f);
        fAddRow("effects.fire_flicker", KindEnum.Slider, 0f, 0.8f, v => v.FireFlicker, (v, f) => v.FireFlicker = f);
        fAddRow("effects.sparks", KindEnum.Slider, 0f, 3f, v => v.FireSparks, (v, f) => v.FireSparks = f);
        fAddRow("effects.smoke", KindEnum.Slider, 0f, 1f, v => v.FireSmoke, (v, f) => v.FireSmoke = f);
        fAddRow("effects.dust", KindEnum.Slider, 0f, 3f, v => v.Dust, (v, f) => v.Dust = f);
        fAddRow("effects.ground_shadows", KindEnum.Slider, 0f, 1f, v => v.GroundShadow, (v, f) => v.GroundShadow = f);

        // THE GRIME, shared with the palette path (UWGrime.hlsl): its setting lives in the user's
        // settings, not in the effect values; saved when the window closes.
        fAddRow("effects.grime_along_the_walls", KindEnum.Slider, 0f, UWPaletteEffects.MaxGrime,
            v => UWUserSettings.PaletteGrime, (v, f) => UWUserSettings.PaletteGrime = Mathf.Round(f * 2f) / 2f);
        mORows[mORows.Count - 1].AllOn = 3f;
        mORows[mORows.Count - 1].AllOff = 0f;
        fAddRow("effects.grime_colour", KindEnum.GrimeTone, 0f, UWGrimeTint.ToneCount - 1,
            v => UWUserSettings.PaletteGrimeTone, (v, f) => UWUserSettings.PaletteGrimeTone = Mathf.RoundToInt(f));

        mOPage = fBuildPage();
        miPageLanguage = UWText.LanguageVersion;
    }

    private void fAddRow(string psLabelId, KindEnum peKind, float pfMin, float pfMax,
        Func<UWGraphicsDetail.EffectValues, float> pOGet, Action<UWGraphicsDetail.EffectValues, float> pOSet)
    {
        mORows.Add(new Row { LabelId = psLabelId, Kind = peKind, Min = pfMin, Max = pfMax, Get = pOGet, Set = pOSet });
    }

    private void fAddToggle(string psLabelId, Func<UWGraphicsDetail.EffectValues, bool> pOGet,
        Action<UWGraphicsDetail.EffectValues, bool> pOSet)
    {
        fAddRow(psLabelId, KindEnum.Toggle, 0f, 1f, v => pOGet(v) ? 1f : 0f, (v, f) => pOSet(v, f >= 0.5f));
    }

    // ------------------------------------------------- The rows as a page

    /// <summary>A row's height and the step down to the next: 22 rows fit under the title row on the 720
    /// lines with the window under the bar.</summary>
    private const float RowHeight = 26f;

    private const float RowAdvance = 27f;

    /// <summary>A slider row: the label 200 wide, the slider from 210 over the row less 300, the value in
    /// the last 80; no arrows (as the palette effects' rows). The label and value columns grow by their
    /// text (UWSetupPageDrawer).</summary>
    private static readonly UWSliderColumns SliderColumns = new UWSliderColumns(0f, 200f, 210f, 300f, 0f, 80f, 80f);

    /// <summary>A choice row: the label 200 wide, the choices from 210 to the row's right edge.</summary>
    private static readonly UWChoiceColumns ChoiceColumns = new UWChoiceColumns(0f, 200f, 210f, 210f);

    private static readonly Func<float, string> msWhole = pfValue => Mathf.RoundToInt(pfValue).ToString();

    private static readonly Func<float, string> msTwoDecimals =
        pfValue => pfValue.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static readonly Func<float, string> msThreeDecimals =
        pfValue => pfValue.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The table as a setup page, one row each in the same order. What a row of the picture did, the
    /// page's row does: a slider row sets the value along its bar - unsnapped, the whole steps rounded
    /// (the bar's Steps), the grime in half steps (its writing rounds so, the slider snaps alike) - and
    /// writes only a value that moved; a switch flips; a choice sets its value (the picture stepped
    /// round them by clicks). Each change goes through the row's own writing and then
    /// UWGraphicsDetail.ApplyCustom, as the picture's fChanged did.
    /// </summary>
    private UWSetupPage fBuildPage()
    {
        UWSetupPage lOPage = new UWSetupPage("effects");

        foreach (Row lORow in mORows)
        {
            Row lOThis = lORow;
            UWSetupRow lOPageRow = new UWSetupRow { Id = "effects.row", Text = UWText.T(lORow.LabelId), Height = RowHeight, Advance = RowAdvance, OnChange = fChanged };

            switch (lORow.Kind)
            {
                case KindEnum.Toggle:
                    lOPageRow.Kind = UWSetupRow.KindEnum.Toggle;
                    lOPageRow.GetOn = () => lOThis.Get(mFValues()) >= 0.5f;
                    lOPageRow.SetOn = pbOn => lOThis.Set(mFValues(), pbOn ? 1f : 0f);
                    break;

                case KindEnum.Tonemapping:
                case KindEnum.GrimeTone:
                    lOPageRow.Kind = UWSetupRow.KindEnum.ChoiceRow;
                    lOPageRow.ChoiceColumns = ChoiceColumns;
                    lOPageRow.Choices = new string[(int)lORow.Max + 1];

                    for (int liAt = 0; liAt < lOPageRow.Choices.Length; liAt++)
                        lOPageRow.Choices[liAt] = fValueText(lORow, liAt);

                    lOPageRow.GetIndex = () => Mathf.RoundToInt(lOThis.Get(mFValues()));
                    lOPageRow.Choose = piValue => lOThis.Set(mFValues(), piValue);
                    break;

                default:
                    lOPageRow.Kind = UWSetupRow.KindEnum.Slider;
                    lOPageRow.SliderColumns = SliderColumns;
                    lOPageRow.Min = lORow.Min;
                    lOPageRow.Max = lORow.Max;
                    lOPageRow.Step = lORow.Kind == KindEnum.Steps ? 1f : (float.IsNaN(lORow.AllOn) ? 0f : 0.5f);
                    lOPageRow.FormatWith = lORow.Kind == KindEnum.Steps ? msWhole : (lORow.Max < 0.5f ? msThreeDecimals : msTwoDecimals);
                    lOPageRow.Get = () => lOThis.Get(mFValues());
                    lOPageRow.Set = pfValue => lOThis.Set(mFValues(), pfValue);
                    break;
            }

            lOPage.Add(lOPageRow);
        }

        return lOPage;
    }

    private static void fChanged()
    {
        UWGraphicsDetail.ApplyCustom();
    }

    /// <summary>A row's value as the picture wrote it: the switch's On and Off, the choices' names.</summary>
    private static string fValueText(Row pORow, float pfValue)
    {
        switch (pORow.Kind)
        {
            case KindEnum.Toggle:
                return pfValue >= 0.5f ? UWText.T("effects.on") : UWText.T("effects.off");

            case KindEnum.Tonemapping:
                return fTonemappingName(Mathf.RoundToInt(pfValue));

            case KindEnum.GrimeTone:
                return fToneName(Mathf.RoundToInt(pfValue));

            case KindEnum.Steps:
                return msWhole(pfValue);

            default:
                return pORow.Max < 0.5f ? msThreeDecimals(pfValue) : msTwoDecimals(pfValue);
        }
    }

    private static string fToneName(int piValue)
    {
        switch (piValue)
        {
            case 1: return UWText.T("effects.tone.dirt");
            case 2: return UWText.T("effects.tone.moss");
            case 3: return UWText.T("effects.tone.soot");
            default: return UWText.T("effects.tone.none");
        }
    }

    private static string fTonemappingName(int piValue)
    {
        switch (piValue)
        {
            case 1: return UWText.T("effects.tonemapping.neutral");
            case 2: return "ACES";
            default: return UWText.T("effects.tonemapping.off");
        }
    }

    /// <summary>ALL ON (the shipped values) or ALL OFF (MEDIUM's): every row from the preset, the grime
    /// to its own values, its colour left as it is.</summary>
    private void fAll(bool pbOn, UWGraphicsDetail.EffectValues pOValues, UWGraphicsDetail.EffectValues pOShipped)
    {
        UWGraphicsDetail.EffectValues lOPreset = pbOn ? pOShipped : pOShipped.AllOff();

        foreach (Row lORow in mORows)
        {
            float lfOwn = pbOn ? lORow.AllOn : lORow.AllOff;

            if (!float.IsNaN(lfOwn))
                lORow.Set(pOValues, lfOwn);
            else if (lORow.Kind != KindEnum.GrimeTone)
                lORow.Set(pOValues, lORow.Get(lOPreset));
        }
    }

    // ------------------------------------------------- Open and close

    /// <summary>The press that opened the window (the classic options act on the press) is not one of
    /// its own: its pieces take no pointer until the button is up again.</summary>
    private bool mbWaitRelease;

    public void Open()
    {
        if (!IsOpen)
            mOScroll = Vector2.zero;

        IsOpen = true;
        mbWaitRelease = true;
    }

    /// <summary>Saves the values and the grime's setting (the values took effect on every change).</summary>
    public void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;

        UWGraphicsDetail.Save();
        UWUserSettings.Save();
    }

    // ------------------------------------------------- The window

    /// <summary>The window's width as laid out; more where its texts need it.</summary>
    private const float WindowWidth = 600f;

    /// <summary>The title row's height: the drag handle, and where the rows start below the top.</summary>
    private const float TitleRow = 44f;

    private const float RowsTop = 50f;

    private const float BottomMargin = 16f;

    /// <summary>The window keeps at least the title row and two rows when it is dragged down.</summary>
    private const float MinHeight = RowsTop + (2f * RowAdvance) + BottomMargin;

    /// <summary>On and Off as wide as laid out, more for a longer text.</summary>
    private const float ButtonWidth = 70f;

    /// <summary>The window's top-left corner in the bar's layout units; NaN until first drawn (then centred
    /// under the bar). Kept while the program runs, as the palette dialog's.</summary>
    private static Vector2 msOAt = new Vector2(float.NaN, float.NaN);

    private Vector2 mOScroll;

    /// <summary>
    /// The window on the bar's kit drawer, after the bar (UWSetupMenu.fDrawOpenDialog): the title row -
    /// the title, then "All:" with On and Off, Done at the right edge -, the rows below it in a scrolled
    /// area. Dragged by its title row left of "All:", kept on the screen under the bar (pfTop); as tall
    /// as its rows need, shorter where the screen ends below it - then the rows scroll (the wheel, the
    /// bar at the right). Returns true when Done was pressed.
    /// </summary>
    public bool Draw(UWKitRows pORows, UWSetupPageDrawer pOPages, float pfScreenWidth, float pfTop)
    {
        // The opening press: withheld until the button is up (it may lie over a row of the window).
        if (mbWaitRelease && !UWPointer.LeftHeld && !UWPointer.LeftReleased)
            mbWaitRelease = false;

        pORows.PointerWithheld = mbWaitRelease;

        // A language switched while the game runs (translation step 3R): the rows' texts anew.
        if (miPageLanguage != UWText.LanguageVersion)
        {
            miPageLanguage = UWText.LanguageVersion;
            mOPage = fBuildPage();
        }

        UWGraphicsDetail.EffectValues lOValues = mFValues();
        string lsTitle = UWText.T("effects.title");
        string lsAll = UWText.T("effects.all");
        string lsOn = UWText.T("effects.on");
        string lsOff = UWText.T("effects.off");
        string lsDone = UWText.T("setup.done");
        float lfTitle = pORows.TextWidth(lsTitle, UWRowInfoEnum.Heading);
        float lfAll = pORows.LabelWidth(lsAll);
        float lfOn = pORows.FitWidth(lsOn, ButtonWidth);
        float lfOff = pORows.FitWidth(lsOff, ButtonWidth);
        float lfDone = pORows.FitWidth(lsDone, 110f);

        // As wide as laid out, or as the rows (20 left, 36 right: the scroll bar) or the title row need.
        float lfHeader = 20f + lfTitle + 30f + lfAll + 8f + lfOn + 6f + lfOff + 20f + lfDone + 20f;
        float lfWidth = Mathf.Min(Mathf.Max(WindowWidth, Mathf.Max(pOPages.InnerNeeded(mOPage) + 56f, lfHeader)), pfScreenWidth - 40f);
        float lfContent = fContentHeight();
        float lfWanted = RowsTop + lfContent + BottomMargin;

        if (float.IsNaN(msOAt.x))
            msOAt = new Vector2((pfScreenWidth - lfWidth) / 2f, pfTop + 8f);

        Vector2 lOMax = new Vector2(Mathf.Max(0f, pfScreenWidth - lfWidth), Mathf.Max(pfTop, UWGuiTheme.ReferenceHeight - 8f - MinHeight));

        msOAt = new Vector2(Mathf.Clamp(msOAt.x, 0f, lOMax.x), Mathf.Clamp(msOAt.y, pfTop, lOMax.y));

        // The title row's pieces from the window's left edge: Done 20 from the right, Off 20 before it,
        // On 6 before that, "All:" 8 before On.
        float lfDoneAt = lfWidth - 20f - lfDone;
        float lfOffAt = lfDoneAt - 20f - lfOff;
        float lfOnAt = lfOffAt - 6f - lfOn;
        float lfAllAt = lfOnAt - 8f - lfAll;

        // Moved while the left button holds the title row left of "All:", kept on the screen.
        msOAt = pORows.Drag(new Rect(msOAt.x, msOAt.y, Mathf.Max(0f, lfAllAt - 10f), TitleRow), msOAt, new Vector2(0f, pfTop), lOMax);

        float lfHeight = Mathf.Clamp(lfWanted, MinHeight, UWGuiTheme.ReferenceHeight - 8f - msOAt.y);
        Rect lODialog = new Rect(msOAt.x, msOAt.y, lfWidth, lfHeight);
        float lfTop = lODialog.y + 14f;
        bool lbDone = false;

        pORows.Frame(lODialog);
        pORows.Title(new Rect(lODialog.x + 20f, lfTop, lfTitle + 10f, 26f), lsTitle);

        if (lOValues != null)
        {
            pORows.Label(new Rect(lODialog.x + lfAllAt, lfTop, lfAll + 4f, 26f), lsAll);

            if (pORows.Action(new Rect(lODialog.x + lfOnAt, lfTop - 2f, lfOn, 30f), lsOn))
                fPressAll(true, lOValues);

            if (pORows.Action(new Rect(lODialog.x + lfOffAt, lfTop - 2f, lfOff, 30f), lsOff))
                fPressAll(false, lOValues);
        }

        if (pORows.Action(new Rect(lODialog.x + lfDoneAt, lfTop - 2f, lfDone, 30f), lsDone))
            lbDone = true;

        if (lOValues != null)
        {
            Rect lOView = new Rect(lODialog.x, lODialog.y + RowsTop, lODialog.width, lODialog.height - RowsTop - BottomMargin);

            mOScroll = pORows.BeginScroll(lOView, mOScroll, new Rect(0f, 0f, lOView.width - 20f, Mathf.Max(lOView.height, lfContent)));

            // The rows in the view's own coordinates, 20 from its left edge, clear of the scroll bar.
            pOPages.Draw(mOPage, 20f, 0f, lODialog.width - 56f);

            pORows.EndScroll();
        }

        pORows.PointerWithheld = false;

        return lbDone;
    }

    /// <summary>The rows' height: each its step down (none is a hint or drawn by itself).</summary>
    private float fContentHeight()
    {
        float lfHeight = 0f;

        foreach (UWSetupRow lORow in mOPage.Rows)
            lfHeight += lORow.Advance;

        return lfHeight;
    }

    private void fPressAll(bool pbOn, UWGraphicsDetail.EffectValues pOValues)
    {
        UWGraphicsDetail.EffectValues lOShipped = UWGraphicsDetail.ShippedValues;

        if (lOShipped == null)
            return;

        fAll(pbOn, pOValues, lOShipped);
        fChanged();
    }

    // ------------------------------------------------- The record (cleanup 8d.3, 2026-10-25)

    /// <summary>
    /// THE ROWS AS A RECORD (cleanup 8d.3, 2026-10-25), the oracle of the window's move onto the kit
    /// (UWEffectsRowsCheck): per row its label, kind and range, the text shown and the value and field a
    /// change leaves - a slider set at the fractions 0, 1, 33, 44 and 89 of 89 (the picture's bar
    /// pixels) as the kit's slider sets it (snapped by the row's step, written when it moved), a switch
    /// pressed twice, a choice row stepped round to the next choice - and ALL ON and ALL OFF, on
    /// pOShipped and the user settings in force (the check swaps in its own; nothing is saved). Recorded
    /// from the 320x200 picture's own bar and click code before the move (Tools/Fixtures/effects).
    /// </summary>
    public static string Describe(UWGraphicsDetail.EffectValues pOShipped)
    {
        UWEffectsScreen lOScreen = new UWEffectsScreen();
        System.Text.StringBuilder lOText = new System.Text.StringBuilder();
        UWGraphicsDetail.EffectValues lOValues = null;

        lOScreen.mFValues = () => lOValues;

        for (int liRow = 0; liRow < lOScreen.mORows.Count; liRow++)
        {
            Row lORow = lOScreen.mORows[liRow];
            UWSetupRow lOPageRow = lOScreen.mOPage.Rows[liRow];

            lOValues = pOShipped.Copy();

            lOText.Append(lOPageRow.Text).Append(" | ").Append(fKindName(lOPageRow)).Append(" | ")
                .Append(fNumber(lOPageRow.Kind == UWSetupRow.KindEnum.Slider ? lOPageRow.Min : 0f)).Append(" to ")
                .Append(fNumber(lOPageRow.Kind == UWSetupRow.KindEnum.Slider ? lOPageRow.Max
                    : (lOPageRow.Kind == UWSetupRow.KindEnum.Toggle ? 1f : lOPageRow.Choices.Length - 1)))
                .Append('\n');

            switch (lOPageRow.Kind)
            {
                case UWSetupRow.KindEnum.Slider:
                    foreach (int liPixel in DescribePixels)
                    {
                        string lsBefore = DescribeValues(lOValues);
                        float lfBefore = lOPageRow.Get();
                        float lfAfter = lOPageRow.Min + (liPixel / 89f * (lOPageRow.Max - lOPageRow.Min));

                        if (lOPageRow.Step > 0f)
                            lfAfter = Mathf.Round(lfAfter / lOPageRow.Step) * lOPageRow.Step;

                        if (UWSetupPageDrawer.IsChanged(lOPageRow.Format, lfBefore, lfAfter))
                        {
                            lOPageRow.Set(lfAfter);
                            lOPageRow.OnChange();
                        }

                        fDescribeChange(lOText, "  bar " + liPixel + "/89", lsBefore, lOValues, lOPageRow.FormatWith(lOPageRow.Get()));
                    }

                    break;

                case UWSetupRow.KindEnum.Toggle:
                    for (int liPress = 0; liPress < 2; liPress++)
                    {
                        string lsBefore = DescribeValues(lOValues);

                        lOPageRow.SetOn(!lOPageRow.GetOn());
                        lOPageRow.OnChange();
                        fDescribeChange(lOText, "  press", lsBefore, lOValues, fValueText(lORow, lOPageRow.GetOn() ? 1f : 0f));
                    }

                    break;

                default:
                    for (int liPress = 0; liPress < lOPageRow.Choices.Length; liPress++)
                    {
                        string lsBefore = DescribeValues(lOValues);

                        lOPageRow.Choose((lOPageRow.GetIndex() + 1) % lOPageRow.Choices.Length);
                        lOPageRow.OnChange();
                        fDescribeChange(lOText, "  press", lsBefore, lOValues, lOPageRow.Choices[lOPageRow.GetIndex()]);
                    }

                    break;
            }
        }

        // ALL ON from everything off (and the two rows ALL OFF leaves moved), ALL OFF from the shipped.
        foreach (bool lbOn in new[] { true, false })
        {
            lOValues = lbOn ? pOShipped.AllOff() : pOShipped.Copy();

            if (lbOn)
            {
                lOValues.MaxShadowLights = 1;
                lOValues.LightBrightness = 2.5f;
            }

            string lsBefore = DescribeValues(lOValues);

            lOScreen.fAll(lbOn, lOValues, pOShipped);
            fDescribeChange(lOText, lbOn ? "All on" : "All off", lsBefore, lOValues, null);
        }

        return lOText.ToString();
    }

    /// <summary>The fractions of 89 the record sets the sliders at (the picture's bar pixels).</summary>
    private static readonly int[] DescribePixels = { 0, 1, 33, 44, 89 };

    /// <summary>The kind as the record names it (the picture's kinds: whole steps by the row's step 1).</summary>
    private static string fKindName(UWSetupRow pORow)
    {
        switch (pORow.Kind)
        {
            case UWSetupRow.KindEnum.Toggle: return "switch";
            case UWSetupRow.KindEnum.Slider: return pORow.Step == 1f ? "whole steps" : "slider";
            default: return "choices";
        }
    }

    private static string fNumber(float pfValue)
    {
        return pfValue.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The values a row can change, as "name=value" in field order, the grime's two settings
    /// last.</summary>
    private static string DescribeValues(UWGraphicsDetail.EffectValues pOValues)
    {
        System.Text.StringBuilder lOText = new System.Text.StringBuilder();

        foreach (System.Reflection.FieldInfo lOField in typeof(UWGraphicsDetail.EffectValues).GetFields())
        {
            object lOValue = lOField.GetValue(pOValues);

            lOText.Append(lOField.Name).Append('=').Append(lOValue is float lfValue ? fNumber(lfValue) : lOValue.ToString()).Append(';');
        }

        lOText.Append("PaletteGrime=").Append(fNumber(UWUserSettings.PaletteGrime)).Append(';');
        lOText.Append("PaletteGrimeTone=").Append(UWUserSettings.PaletteGrimeTone).Append(';');

        return lOText.ToString();
    }

    /// <summary>One line of the record: what changed against psBefore, and the text shown.</summary>
    private static void fDescribeChange(System.Text.StringBuilder pOText, string psWhat, string psBefore,
        UWGraphicsDetail.EffectValues pOValues, string psShown)
    {
        string[] lsBefore = psBefore.Split(';');
        string[] lsAfter = DescribeValues(pOValues).Split(';');
        List<string> lsChanged = new List<string>();

        for (int liAt = 0; liAt < lsAfter.Length && liAt < lsBefore.Length; liAt++)
        {
            if (lsAfter[liAt] != lsBefore[liAt])
                lsChanged.Add(lsAfter[liAt]);
        }

        pOText.Append(psWhat).Append(" -> ").Append(lsChanged.Count == 0 ? "no change" : string.Join(", ", lsChanged));

        if (psShown != null)
            pOText.Append(" | shows \"").Append(psShown).Append('"');

        pOText.Append('\n');
    }
}
