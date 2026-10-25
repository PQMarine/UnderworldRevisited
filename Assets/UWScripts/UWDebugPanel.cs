using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE DEBUG PANEL ON THE KIT (cleanup 8c, 2026-10-25; decision 26): the F1 overlay's report and the
/// debug tools' tab, drawn by UWKitRows on a canvas of its own (UWUiSorting.DebugPanel) in LateUpdate.
/// Until then the overlay was one white IMGUI text block on a dark box and the tools two IMGUI boxes;
/// the debug display is needed more often again for UW2, so it became clearer (per user, 2026-10-25):
///
///   - TWO TABS, Report and Tools: the report is the F1 overlay (Shift+F1), the tools the spell list
///     and the jump field of Shift+F10 and Shift+F7 (UWDebugTools draws its tab here).
///   - THE REPORT'S SECTIONS (UWDebugReport, engine-free) under a heading each, a label column and a
///     value column aligned, the game variables and the creature record's numbers as a grid; two
///     columns of sections side by side when the window is wide, one when narrow (UWDebugColumns), the
///     panel never wider than two thirds of the window nor taller than most of it - a scroll bar when
///     the sections are taller.
///   - A HEADING FOLDS its section and unfolds it again (a click, the pad pointer's A as well); the
///     folded sections are kept in the settings (UWUserSettings.DebugFolded, UWDebugFolds' text),
///     saved only when a heading is clicked.
///   - A VALUE THAT CHANGED LIGHTS UP for about a second (UWDebugFlash, by the clock): a trap that
///     fires, a creature's goal or last event.
///
/// THE LOOK is the kit windows' (UWGuiTheme's palette, the interface font) on a dark ground the world
/// shows through; the layout is on UWGuiTheme's 720 lines and scales with the window as the other kit
/// windows do. The texts are English literals - a developer tool, not translated.
/// </summary>
public sealed class UWDebugPanel
{
    /// <summary>What a click on the tab row asks of the panel's owner.</summary>
    public enum ActionEnum
    {
        None,

        /// <summary>The Report tab: the report shown, the tools closed.</summary>
        ShowReport,

        /// <summary>The Tools tab: the tools opened.</summary>
        ShowTools
    }

    // ------------------------------------------------- Measures, in UWGuiTheme's layout units

    private const float Margin = 8f;

    private const float Pad = 8f;

    private const float TabHeight = 24f;

    private const float TabPadding = 14f;

    private const float TabGap = 6f;

    private const float HeadingHeight = 22f;

    private const float SectionGap = 6f;

    /// <summary>One column of sections; two side by side when the window is wide.</summary>
    private const float ColumnWidth = 380f;

    private const float MinColumnWidth = 240f;

    private const float ColumnGap = 12f;

    /// <summary>Inside a section, left and right of its rows.</summary>
    private const float Inset = 6f;

    /// <summary>Between the label column and the values.</summary>
    private const float LabelGap = 10f;

    /// <summary>The label column takes at most this share of the column.</summary>
    private const float MaxLabelShare = 0.45f;

    /// <summary>Between a grid cell's label and its value, and between two cells.</summary>
    private const float CellInnerGap = 5f;

    private const float CellGap = 12f;

    /// <summary>The panel takes at most these shares of the window, so the view stays in sight.</summary>
    private const float MaxWidthShare = 0.66f;

    private const float MaxHeightShare = 0.85f;

    /// <summary>The tools' tab: the old spell box's size, a little wider for the columns.</summary>
    private const float ToolsWidth = 600f;

    private const float ToolsHeight = 440f;

    public const int TextSize = 13;

    private const int HeadingSize = 14;

    private const int SubSize = 12;

    private const int TabSize = 14;

    // ------------------------------------------------- Colours

    /// <summary>The panel's ground: dark and see-through, so the world shows.</summary>
    private static readonly Color Ground = new Color(0.04f, 0.04f, 0.05f, 0.72f);

    private static readonly Color HeadingGround = new Color(0.14f, 0.14f, 0.16f, 0.85f);

    private static readonly Color HeadingHover = new Color(0.22f, 0.22f, 0.26f, 0.92f);

    private static readonly Color TabGround = new Color(0.09f, 0.09f, 0.11f, 0.85f);

    private static readonly Color LabelColour = new Color(0.70f, 0.70f, 0.72f, 1f);

    /// <summary>The light of a changed value at full strength (the accent, half see-through).</summary>
    private const float FlashAlpha = 0.45f;

    // ------------------------------------------------- State

    private readonly UWKitRows mOKit;

    private readonly UWDebugFlash mOFlash = new UWDebugFlash();

    /// <summary>The folded sections' text as last read or written, and its ids.</summary>
    private string msFolded;

    private HashSet<string> mOFolded;

    private Vector2 mOScroll;

    private bool mbReportShown;

    /// <summary>The panel stands on the screen this frame.</summary>
    public bool IsShown { get; private set; }

    /// <summary>Where the panel stands, in screen pixels with y up (as the mouse reports it), for
    /// the block of the game's buttons over it.</summary>
    public Rect ScreenRect { get; private set; }

    public UWDebugPanel(Transform pOOwner)
    {
        mOKit = new UWKitRows(pOOwner, UWUiSorting.DebugPanel);
    }

    /// <summary>The text field's typing ends with the panel (the owner's OnDestroy).</summary>
    public void Dispose()
    {
        mOKit.Dispose();
    }

    private static UWKitRows.HelpLook fLook(int piSize, Color pOColour, TextAnchor peAnchor = TextAnchor.UpperLeft, bool pbWraps = false, bool pbBold = false)
    {
        return new UWKitRows.HelpLook { Size = piSize, Colour = pOColour, Anchor = peAnchor, Wraps = pbWraps, Bold = pbBold };
    }

    private static Color fEmphasisColour(UWDebugEmphasis peEmphasis)
    {
        switch (peEmphasis)
        {
            case UWDebugEmphasis.Dim: return UWGuiTheme.DimText;
            case UWDebugEmphasis.Warning: return UWGuiTheme.Error;
            case UWDebugEmphasis.Accent: return UWGuiTheme.Accent;
            default: return UWGuiTheme.Text;
        }
    }

    // ------------------------------------------------- The frame

    /// <summary>
    /// Draws the panel for this frame: not shown, the canvas goes dark; with pOTools the tools' tab
    /// (UWDebugTools.DrawTab), else pOReport's sections. Returns what a click on the tab row asks for.
    /// </summary>
    public ActionEnum Draw(bool pbShown, UWDebugReport pOReport, UWDebugTools pOTools)
    {
        mOKit.Begin(pbShown, false, false, false);
        IsShown = pbShown;

        if (!pbShown)
        {
            mOFlash.Reset();
            mbReportShown = false;
            ScreenRect = default;

            return ActionEnum.None;
        }

        if (mOFolded == null || msFolded != UWUserSettings.DebugFolded)
        {
            msFolded = UWUserSettings.DebugFolded;
            mOFolded = UWDebugFolds.Parse(msFolded);
        }

        bool lbTools = pOTools != null || pOReport == null;
        float lfAvailableWidth = Mathf.Max(MinColumnWidth + (2f * Pad), (mOKit.Width * MaxWidthShare) - Margin);
        float lfAvailableHeight = (UWGuiTheme.ReferenceHeight * MaxHeightShare) - Margin;
        float lfTop = Margin + Pad + TabHeight + TabGap;
        Rect lOPanel;
        ActionEnum leAction;

        if (lbTools)
        {
            float lfWidth = Mathf.Min(ToolsWidth, mOKit.Width - (2f * Margin) - (2f * Pad));
            float lfHeight = Mathf.Min(ToolsHeight, lfAvailableHeight - (lfTop - Margin) - Pad);

            lOPanel = new Rect(Margin, Margin, lfWidth + (2f * Pad), (lfTop - Margin) + lfHeight + Pad);
            mOKit.Fill(lOPanel, Ground);
            leAction = fTabs(lOPanel, true, "Shift+F7 jump   Shift+F10 spells   Esc closes");

            if (pOTools != null)
                pOTools.DrawTab(mOKit, new Rect(Margin + Pad, lfTop, lfWidth, lfHeight));

            mOFlash.Reset();
            mbReportShown = false;
        }
        else
        {
            if (!mbReportShown)
                mOFlash.Reset();

            mbReportShown = true;
            mOFlash.Note(pOReport, Time.unscaledTime);
            lOPanel = fReport(pOReport, lfAvailableWidth, lfAvailableHeight, lfTop, out leAction);
        }

        mOKit.End();

        float lfScale = UWGuiTheme.Scale;

        ScreenRect = new Rect(lOPanel.x * lfScale, Screen.height - (lOPanel.yMax * lfScale), lOPanel.width * lfScale, lOPanel.height * lfScale);

        return leAction;
    }

    /// <summary>The tab row along the panel's top, and the hint on its right.</summary>
    private ActionEnum fTabs(Rect pOPanel, bool pbTools, string psHint)
    {
        UWKitRows.HelpLook lOLook = fLook(TabSize, UWGuiTheme.Text, TextAnchor.MiddleCenter);
        float lfX = pOPanel.x + Pad;
        float lfY = pOPanel.y + Pad;
        ActionEnum leAction = ActionEnum.None;

        if (fTab(ref lfX, lfY, "Report", !pbTools, lOLook) && pbTools)
            leAction = ActionEnum.ShowReport;

        lfX += 2f;

        if (fTab(ref lfX, lfY, "Tools", pbTools, lOLook) && !pbTools)
            leAction = ActionEnum.ShowTools;

        UWKitRows.HelpLook lOHint = fLook(SubSize, UWGuiTheme.DimText, TextAnchor.MiddleRight);
        float lfRight = pOPanel.xMax - Pad;

        mOKit.HelpText(new Rect(lfX + 8f, lfY, Mathf.Max(0f, lfRight - lfX - 8f), TabHeight), psHint, lOHint);

        return leAction;
    }

    private bool fTab(ref float pfX, float pfY, string psText, bool pbActive, UWKitRows.HelpLook pOLook)
    {
        float lfWidth = mOKit.HelpTextWidth(psText, pOLook) + (2f * TabPadding);
        UWKitRows.HelpLook lOLook = pOLook;

        lOLook.Colour = pbActive ? UWGuiTheme.Accent : UWGuiTheme.DimText;

        bool lbClicked = mOKit.HelpButton(new Rect(pfX, pfY, lfWidth, TabHeight), psText, lOLook,
            pbActive ? HeadingGround : TabGround, HeadingHover, UWGuiTheme.Text);

        pfX += lfWidth;

        return lbClicked;
    }

    // ------------------------------------------------- The report

    /// <summary>The measures of one section in a column: its height, its label column, its grid.</summary>
    private struct SectionLayout
    {
        public float Height;

        public float LabelWidth;

        public float CellWidth;

        public int CellsPerRow;
    }

    private float LineHeight => mOKit.HelpTextHeight("Ay", fLook(TextSize, UWGuiTheme.Text), 1000f) + 2f;

    private Rect fReport(UWDebugReport pOReport, float pfAvailableWidth, float pfAvailableHeight, float pfTop, out ActionEnum peAction)
    {
        int liCount = pOReport.Sections.Count;
        float lfColumn = Mathf.Max(MinColumnWidth, Mathf.Min(ColumnWidth, pfAvailableWidth - (2f * Pad) - UWKitRows.ScrollBarRoom));
        int liColumns = pOReport.IsCritterTrace ? 1 : UWDebugColumns.Count(pfAvailableWidth - (2f * Pad) - UWKitRows.ScrollBarRoom, lfColumn, ColumnGap);
        SectionLayout[] laLayouts = new SectionLayout[liCount];
        float[] laHeights = new float[liCount];
        int[] laColumnOf = new int[liCount];

        for (int liAt = 0; liAt < liCount; liAt++)
        {
            laLayouts[liAt] = fMeasure(pOReport.Sections[liAt], lfColumn);
            laHeights[liAt] = laLayouts[liAt].Height;
        }

        float lfContent = Mathf.Max(LineHeight, UWDebugColumns.Split(laHeights, liColumns, laColumnOf) - SectionGap);
        float lfViewHeight = Mathf.Min(lfContent, pfAvailableHeight - (pfTop - Margin) - Pad);
        bool lbScrolls = lfContent > lfViewHeight + 0.5f;
        float lfContentWidth = (liColumns * lfColumn) + ((liColumns - 1) * ColumnGap);
        float lfViewWidth = lfContentWidth + (lbScrolls ? UWKitRows.ScrollBarRoom + 4f : 0f);
        Rect lOPanel = new Rect(Margin, Margin, lfViewWidth + (2f * Pad), (pfTop - Margin) + lfViewHeight + Pad);

        mOKit.Fill(lOPanel, Ground);
        peAction = fTabs(lOPanel, false, pOReport.Footer);

        Rect lOView = new Rect(Margin + Pad, pfTop, lfViewWidth, lfViewHeight);

        mOScroll = mOKit.BeginScroll(lOView, mOScroll, new Rect(0f, 0f, lfViewWidth, lfContent));

        float[] laY = new float[2];

        for (int liAt = 0; liAt < liCount; liAt++)
        {
            int liColumn = laColumnOf[liAt];
            float lfX = liColumn * (lfColumn + ColumnGap);

            fDrawSection(pOReport.Sections[liAt], laLayouts[liAt], lfX, laY[liColumn], lfColumn);
            laY[liColumn] += laLayouts[liAt].Height;
        }

        mOKit.EndScroll();

        return lOPanel;
    }

    private bool fIsFolded(UWDebugSection pOSection)
    {
        return mOFolded.Contains(pOSection.Id);
    }

    /// <summary>A section's measures in a column of pfWidth: the heading, and unfolded its rows - the
    /// label column as wide as its longest label (at most MaxLabelShare), a value wrapped below its
    /// first line when it is longer than the value column, the cells in as many per line as fit.</summary>
    private SectionLayout fMeasure(UWDebugSection pOSection, float pfWidth)
    {
        SectionLayout lOLayout = new SectionLayout { Height = HeadingHeight + SectionGap, CellsPerRow = 1 };

        if (fIsFolded(pOSection))
            return lOLayout;

        UWKitRows.HelpLook lOText = fLook(TextSize, UWGuiTheme.Text);
        float lfInner = pfWidth - (2f * Inset);
        float lfLabel = 0f;
        float lfCell = 0f;

        foreach (UWDebugRow lORow in pOSection.Rows)
        {
            if (lORow.Is(UWDebugRowFlags.Cell))
                lfCell = Mathf.Max(lfCell, mOKit.HelpTextWidth(lORow.Label, lOText) + CellInnerGap + mOKit.HelpTextWidth(lORow.Value, lOText) + CellGap);
            else if (!lORow.Is(UWDebugRowFlags.Sub))
                lfLabel = Mathf.Max(lfLabel, mOKit.HelpTextWidth(lORow.Label, lOText));
        }

        lOLayout.LabelWidth = lfLabel > 0f ? Mathf.Min(lfLabel + LabelGap, lfInner * MaxLabelShare) : 0f;
        lOLayout.CellsPerRow = lfCell > 0f ? Mathf.Max(1, Mathf.FloorToInt(lfInner / lfCell)) : 1;
        lOLayout.CellWidth = lfInner / lOLayout.CellsPerRow;

        float lfLine = LineHeight;
        float lfHeight = 2f;
        int liCellsInLine = 0;

        foreach (UWDebugRow lORow in pOSection.Rows)
        {
            if (lORow.Is(UWDebugRowFlags.Cell))
            {
                if (liCellsInLine == 0)
                    lfHeight += lfLine;

                liCellsInLine = (liCellsInLine + 1) % lOLayout.CellsPerRow;

                continue;
            }

            liCellsInLine = 0;
            lfHeight += fRowHeight(lORow, lOLayout, lfInner);
        }

        lOLayout.Height += lfHeight + 2f;

        return lOLayout;
    }

    private float fValueWidth(UWDebugRow pORow, SectionLayout pOLayout, float pfInner)
    {
        return string.IsNullOrEmpty(pORow.Label) ? pfInner : pfInner - pOLayout.LabelWidth;
    }

    private float fRowHeight(UWDebugRow pORow, SectionLayout pOLayout, float pfInner)
    {
        if (pORow.Is(UWDebugRowFlags.Sub))
            return LineHeight + 2f;

        float lfValue = mOKit.HelpTextHeight(pORow.Value, fLook(TextSize, UWGuiTheme.Text, TextAnchor.UpperLeft, true), fValueWidth(pORow, pOLayout, pfInner));

        return Mathf.Max(LineHeight, lfValue + 2f);
    }

    /// <summary>A section at (pfX, pfY) of the content: its heading, which folds it, and unfolded its
    /// rows, sub headings and grid.</summary>
    private void fDrawSection(UWDebugSection pOSection, SectionLayout pOLayout, float pfX, float pfY, float pfWidth)
    {
        bool lbFolded = fIsFolded(pOSection);
        UWKitRows.HelpLook lOHeading = fLook(HeadingSize, UWGuiTheme.Accent, TextAnchor.MiddleLeft, false, true);

        // "+" and "-" rather than arrows: the interface font has them for certain.
        if (mOKit.HelpButton(new Rect(pfX, pfY, pfWidth, HeadingHeight), (lbFolded ? "  +  " : "  -  ") + pOSection.Title, lOHeading,
            HeadingGround, HeadingHover, UWGuiTheme.Text))
            fToggleFold(pOSection.Id);

        if (lbFolded)
            return;

        UWKitRows.HelpLook lOLabel = fLook(TextSize, LabelColour);
        UWKitRows.HelpLook lOValue = fLook(TextSize, UWGuiTheme.Text, TextAnchor.UpperLeft, true);
        UWKitRows.HelpLook lOSub = fLook(SubSize, UWGuiTheme.DimText, TextAnchor.LowerLeft, false, true);
        float lfInner = pfWidth - (2f * Inset);
        float lfLeft = pfX + Inset;
        float lfY = pfY + HeadingHeight + 2f;
        float lfLine = LineHeight;
        float lfNow = Time.unscaledTime;
        int liCell = 0;

        foreach (UWDebugRow lORow in pOSection.Rows)
        {
            float lfFlash = mOFlash.Strength(pOSection, lORow, lfNow);

            lOValue.Colour = fEmphasisColour(lORow.Emphasis);

            if (lORow.Is(UWDebugRowFlags.Cell))
            {
                // A grid cell: its label, its value right after it.
                float lfCellX = lfLeft + (liCell * pOLayout.CellWidth);
                float lfLabelWidth = mOKit.HelpTextWidth(lORow.Label, lOLabel);
                Rect lOValueRect = new Rect(lfCellX + lfLabelWidth + CellInnerGap, lfY, Mathf.Max(1f, pOLayout.CellWidth - lfLabelWidth - CellInnerGap), lfLine);

                mOKit.HelpText(new Rect(lfCellX, lfY, lfLabelWidth + 1f, lfLine), lORow.Label, lOLabel);
                fFlash(lOValueRect, mOKit.HelpTextWidth(lORow.Value, lOValue), lfFlash);
                lOValue.Wraps = false;
                mOKit.HelpText(lOValueRect, lORow.Value, lOValue);
                lOValue.Wraps = true;

                liCell++;

                if (liCell >= pOLayout.CellsPerRow)
                {
                    liCell = 0;
                    lfY += lfLine;
                }

                continue;
            }

            if (liCell > 0)
            {
                liCell = 0;
                lfY += lfLine;
            }

            float lfHeight = fRowHeight(lORow, pOLayout, lfInner);

            if (lORow.Is(UWDebugRowFlags.Sub))
            {
                mOKit.HelpText(new Rect(lfLeft, lfY, lfInner, lfHeight), lORow.Label, lOSub);
                lfY += lfHeight;

                continue;
            }

            float lfValueWidth = fValueWidth(lORow, pOLayout, lfInner);
            Rect lORect = new Rect(lfLeft + lfInner - lfValueWidth, lfY, lfValueWidth, lfHeight);

            if (!string.IsNullOrEmpty(lORow.Label))
                mOKit.HelpText(new Rect(lfLeft, lfY, pOLayout.LabelWidth - 2f, lfLine), lORow.Label, lOLabel);

            fFlash(lORect, Mathf.Min(lfValueWidth, mOKit.HelpTextWidth(lORow.Value, lOValue)), lfFlash);
            mOKit.HelpText(lORect, lORow.Value, lOValue);
            lfY += lfHeight;
        }
    }

    /// <summary>The light behind a changed value, as wide as the value's text.</summary>
    private void fFlash(Rect pORect, float pfTextWidth, float pfStrength)
    {
        if (pfStrength <= 0.01f)
            return;

        Color lOAccent = UWGuiTheme.Accent;

        mOKit.Fill(new Rect(pORect.x - 2f, pORect.y, Mathf.Max(6f, pfTextWidth + 4f), pORect.height),
            new Color(lOAccent.r, lOAccent.g, lOAccent.b, FlashAlpha * pfStrength));
    }

    /// <summary>Folds or unfolds a section and keeps it in the settings - saved now, on the change only.</summary>
    private void fToggleFold(string psId)
    {
        msFolded = UWDebugFolds.Toggle(msFolded, psId);
        mOFolded = UWDebugFolds.Parse(msFolded);
        UWUserSettings.DebugFolded = msFolded;
        UWUserSettings.Save();
    }
}
