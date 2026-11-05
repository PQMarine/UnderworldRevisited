using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The help's MANUAL tab: the game's manual (the GOG PDF, rendered by UWManual) page by page
/// beside the frame, and its READING MODE over the whole picture (DrawReadingKit). Moved out of
/// UWHelpWindow (cleanup 8.6, 2026-10-24); the window keeps whether the reading mode is open
/// (UWHelpWindow.IsReading), its modal hold and the swallowed closing click, and draws it over
/// everything after the panel. On the kit since cleanup 8b.3; the IMGUI form went in 8b.4.
///
/// THE DOCUMENTS' ROW (per user, 2026-11-05, the Steam version's extras): with more than one of the running
/// game's PDFs found (UWManual.Documents) a row of buttons above the arrows picks one - manual, reference card,
/// map, the game's own book, cluebook; each keeps its own page. The cluebook asks once before it opens
/// (UWUserSettings.CluebookConfirmed).
/// </summary>
public sealed class UWHelpManualTab : UWHelpTab
{
    /// <summary>The page shown, from zero - kept while the game runs, for the document shown; the others'
    /// pages in msPages by their file.</summary>
    private static int msPage;

    private static readonly System.Collections.Generic.Dictionary<string, int> msPages = new System.Collections.Generic.Dictionary<string, int>();

    /// <summary>The cluebook asked for and its question showing, its index in the documents (-1 none).</summary>
    private static int miAsking = -1;

    /// <summary>The documents' buttons' row height.</summary>
    private const float DocumentRowHeight = 26f;

    private const float DocumentGap = 4f;

    /// <summary>Room on either side of a document's name in its button.</summary>
    private const float DocumentPadding = 6f;

    private static string fDocumentName(UWDataImport.Install.UWGameDocuments.KindEnum peKind)
    {
        switch (peKind)
        {
            case UWDataImport.Install.UWGameDocuments.KindEnum.ReferenceCard:
                return UWText.T("help.manual.doc.reference");
            case UWDataImport.Install.UWGameDocuments.KindEnum.Map:
                return UWText.T("help.manual.doc.map");
            case UWDataImport.Install.UWGameDocuments.KindEnum.Memoirs:
                return UWText.T("help.manual.doc.memoirs");
            case UWDataImport.Install.UWGameDocuments.KindEnum.SafePassage:
                return UWText.T("help.manual.doc.passage");
            case UWDataImport.Install.UWGameDocuments.KindEnum.Cluebook:
                return UWText.T("help.manual.doc.cluebook");
            default:
                return UWText.T("help.manual.doc.manual");
        }
    }

    /// <summary>Another document: the page of the one shown kept, the chosen one's taken back.</summary>
    private static void fShow(int piIndex)
    {
        System.Collections.Generic.IReadOnlyList<UWDataImport.Install.UWGameDocuments.Document> lODocuments = UWManual.Documents;

        if (piIndex < 0 || piIndex >= lODocuments.Count || piIndex == UWManual.Selected)
            return;

        if (UWManual.Selected < lODocuments.Count)
            msPages[lODocuments[UWManual.Selected].Path] = msPage;

        UWManual.Select(piIndex);
        msPage = msPages.TryGetValue(lODocuments[piIndex].Path, out int liPage) ? liPage : 0;
    }

    /// <summary>The row of the documents' buttons, the one shown lit; nothing with one document. True while the
    /// cluebook's question stands in place of the page.</summary>
    private bool fDrawDocuments(UWHelpKitFlow pOFlow)
    {
        System.Collections.Generic.IReadOnlyList<UWDataImport.Install.UWGameDocuments.Document> lODocuments = UWManual.Documents;

        if (lODocuments.Count <= 1)
            return false;

        UWKitRows lOKit = pOFlow.Kit;
        UWKitRows.HelpLook lOLook = UWHelpKitFlow.ButtonLook(UWGuiTheme.HelpFoldoutSize);
        // EACH BUTTON AS WIDE AS ITS NAME NEEDS, in as many rows as that takes (per user, 2026-11-05: "Memoirs of
        // Cabirus" was cut at equal widths, and in the classic screen's narrow help every name was cut when the row
        // was shared out): the buttons go into a row while they fit, the next starts a new row, and each row's
        // room left is shared out evenly among its buttons.
        float lfWidthAll = pOFlow.Width;
        float[] lfNeeded = new float[lODocuments.Count];
        List<int> lORowStarts = new List<int> { 0 };
        float lfUsed = 0f;

        for (int liAt = 0; liAt < lODocuments.Count; liAt++)
        {
            lfNeeded[liAt] = Mathf.Min(lfWidthAll, lOKit.HelpTextWidth(fDocumentName(lODocuments[liAt].Kind), lOLook) + (2f * DocumentPadding));

            float lfWith = lfUsed + (lfUsed > 0f ? DocumentGap : 0f) + lfNeeded[liAt];

            if (lfUsed > 0f && lfWith > lfWidthAll)
            {
                lORowStarts.Add(liAt);
                lfUsed = lfNeeded[liAt];
            }
            else
                lfUsed = lfWith;
        }

        Rect[] lOButtons = new Rect[lODocuments.Count];

        for (int liRow = 0; liRow < lORowStarts.Count; liRow++)
        {
            int liFirst = lORowStarts[liRow];
            int liEnd = liRow + 1 < lORowStarts.Count ? lORowStarts[liRow + 1] : lODocuments.Count;
            Rect lORow = pOFlow.Take(DocumentRowHeight);
            float lfNames = 0f;

            if (liRow > 0)
                lORow.y += liRow * DocumentGap;

            for (int liAt = liFirst; liAt < liEnd; liAt++)
                lfNames += lfNeeded[liAt];

            float lfExtra = Mathf.Max(0f, (lORow.width - lfNames - ((liEnd - liFirst - 1) * DocumentGap)) / (liEnd - liFirst));
            float lfX = lORow.x;

            for (int liAt = liFirst; liAt < liEnd; liAt++)
            {
                lOButtons[liAt] = new Rect(lfX, lORow.y, lfNeeded[liAt] + lfExtra, DocumentRowHeight);
                lfX += lOButtons[liAt].width + DocumentGap;
            }
        }

        if (lORowStarts.Count > 1)
            pOFlow.Space((lORowStarts.Count - 1) * DocumentGap);

        for (int liAt = 0; liAt < lODocuments.Count; liAt++)
        {
            bool lbShown = liAt == UWManual.Selected && miAsking < 0;
            Rect lOButton = lOButtons[liAt];

            if (!lOKit.HelpButton(lOButton, fDocumentName(lODocuments[liAt].Kind), lOLook, lbShown ? UWGuiTheme.Hover : UWGuiTheme.Panel,
                UWGuiTheme.Hover, UWGuiTheme.Text))
                continue;

            if (lODocuments[liAt].IsSpoiler && !UWUserSettings.CluebookConfirmed)
                miAsking = liAt;
            else
            {
                miAsking = -1;
                fShow(liAt);
            }
        }

        pOFlow.Space(6f);

        if (miAsking < 0)
            return false;

        // The question in place of the page: open the cluebook (once, then never asked again), or not now.
        pOFlow.Line(UWText.T("help.manual.spoiler"), UWHelpLookEnum.Text);
        pOFlow.Space(6f);

        Rect lOAnswers = pOFlow.Take(DocumentRowHeight);
        float lfHalf = (lOAnswers.width - DocumentGap) / 2f;

        if (lOKit.HelpButton(new Rect(lOAnswers.x, lOAnswers.y, lfHalf, DocumentRowHeight), UWText.T("help.manual.spoiler_open"), lOLook,
            UWGuiTheme.Panel, UWGuiTheme.Hover, UWGuiTheme.Text))
        {
            UWUserSettings.CluebookConfirmed = true;
            fShow(miAsking);
            miAsking = -1;
        }
        else if (lOKit.HelpButton(new Rect(lOAnswers.x + lfHalf + DocumentGap, lOAnswers.y, lfHalf, DocumentRowHeight), UWText.T("help.manual.spoiler_cancel"),
            lOLook, UWGuiTheme.Panel, UWGuiTheme.Hover, UWGuiTheme.Text))
            miAsking = -1;

        return miAsking >= 0;
    }

    public override UWHelpTabId Id => UWHelpTabId.Manual;

    public override string TitleId => "help.tab.manual";

    /// <summary>While the manual is read large the game hears neither keys nor clicks.</summary>
    public override bool CapturesKeys => UWHelpWindow.IsReading;

    public override bool Scrolls => false;

    private static void fTurn(int piPages)
    {
        msPage = Mathf.Clamp(msPage + piPages, 0, Mathf.Max(0, UWManual.PageCount - 1));
    }

    private static float fPageWidth(int piPage, float pfHeight)
    {
        if (piPage < 0)
            return 0f;

        Vector2 lOSize = UWManual.GetPageSize(piPage);

        return lOSize.y > 0f ? pfHeight * lOSize.x / lOSize.y : 0f;
    }

    private static int fLastShown(int piLeft, int piRight)
    {
        return Mathf.Max(piLeft, piRight);
    }

    // ------------------------------------------------- On the kit (cleanup 8b.3, 2026-10-24)

    /// <summary>The page arrows' width and the row's height (the IMGUI form's 44 and 28).</summary>
    private const float ArrowWidth = 44f;

    private const float ArrowRowHeight = 28f;

    /// <summary>
    /// THE MANUAL ON THE KIT: the same rows as the IMGUI form - the arrows with "Page x of y" between them, the
    /// line about the reading mode, the page as large as the room allows at whole screen pixels - a
    /// press on the page opens the reading mode, the wheel turns. Without PDFium (the Linux build
    /// without it) or without the manual: its title and the reason.
    /// </summary>
    protected override void OnDrawKit(UWHelpKitFlow pOFlow)
    {
        if (fDrawDocuments(pOFlow))
            return;

        if (!UWManual.EnsureOpen())
        {
            pOFlow.Line(UWText.T("help.manual.title"), UWHelpLookEnum.Heading);
            pOFlow.Line(UWManual.Problem ?? UWText.T("help.manual.cannot_show"), UWHelpLookEnum.Dim);

            return;
        }

        int liCount = UWManual.PageCount;

        msPage = Mathf.Clamp(msPage, 0, Mathf.Max(0, liCount - 1));

        UWKitRows lOKit = pOFlow.Kit;
        UWKitRows.HelpLook lOArrow = UWHelpKitFlow.ButtonLook(UWGuiTheme.HelpFoldoutSize);
        Rect lORow = pOFlow.Take(ArrowRowHeight);

        if (lOKit.HelpButton(new Rect(lORow.x, lORow.y, ArrowWidth, ArrowRowHeight), "<", lOArrow, UWGuiTheme.Panel, UWGuiTheme.Hover, UWGuiTheme.Text))
            fTurn(-1);

        lOKit.HelpText(new Rect(lORow.x + ArrowWidth, lORow.y, Mathf.Max(0f, lORow.width - (2f * ArrowWidth)), ArrowRowHeight),
            UWText.F("help.manual.page", msPage + 1, liCount), UWHelpKitFlow.OneLine(UWHelpLookEnum.Text, TextAnchor.MiddleCenter));

        if (lOKit.HelpButton(new Rect(lORow.xMax - ArrowWidth, lORow.y, ArrowWidth, ArrowRowHeight), ">", lOArrow, UWGuiTheme.Panel, UWGuiTheme.Hover, UWGuiTheme.Text))
            fTurn(1);

        pOFlow.Line(UWText.T("help.manual.click_to_read"), UWHelpLookEnum.Dim);
        pOFlow.Space(6f);

        Rect lOSpace = pOFlow.Rest();

        if (lOSpace.width < 4f || lOSpace.height < 4f)
            return;

        // The page as large as the space allows, placed on whole screen pixels (the flow's units are
        // the canvas', from the screen's top-left corner).
        float lfScale = mOContext.Scale;
        Vector2 lOSize = UWManual.GetPageSize(msPage);
        float lfLeft = lOSpace.x * lfScale;
        float lfTop = lOSpace.y * lfScale;
        float lfRoomWidth = lOSpace.width * lfScale;
        float lfRoomHeight = lOSpace.height * lfScale;

        int liWidth = Mathf.FloorToInt(Mathf.Min(lfRoomWidth, lfRoomHeight * lOSize.x / lOSize.y));
        int liHeight = Mathf.RoundToInt(liWidth * lOSize.y / lOSize.x);

        if (liWidth < 16 || liHeight < 16)
            return;

        Rect lOOnScreen = new Rect(Mathf.Round(lfLeft + ((lfRoomWidth - liWidth) / 2f)), Mathf.Round(lfTop), liWidth, liHeight);
        Rect lOUnits = new Rect(lOOnScreen.x / lfScale, lOOnScreen.y / lfScale, liWidth / lfScale, liHeight / lfScale);
        Texture2D lOPage = UWManual.GetPage(msPage, liWidth, liHeight);

        if (lOPage != null)
            lOKit.Picture(lOUnits, lOPage);

        if (lOKit.PressedIn(lOUnits))
            UWHelpWindow.SetReading(true);
        else
        {
            float lfWheel = lOKit.WheelIn(lOSpace);

            if (lfWheel != 0f)
                fTurn(lfWheel < 0f ? 1 : -1);
        }
    }

    /// <summary>
    /// The manual over the whole picture. Page one is the cover and stands alone on the
    /// right; after it the even pages lie left and the odd ones right, as in the printed
    /// booklet. Two pages when both fit at screen height, one otherwise.
    ///
    /// THE READING MODE ON THE KIT: as the IMGUI form's DrawReading - the shade over the whole picture, one page or the
    /// spread at screen height on whole pixels, the arrows and the page line in the help's scale; the
    /// arrow keys, PageUp and PageDown, Space, Home and End turn (held keys repeat, as the system's
    /// repeat did for IMGUI's KeyDown), the wheel too; Escape, Tab (not with Alt) or a press on a page
    /// closes it, and the closing press is the help's (UWHelpWindow.SetReading's swallow).
    /// </summary>
    public void DrawReadingKit(UWHelpContext pOContext, UWKitRows pOKit)
    {
        mOContext = pOContext;

        int liCount = UWManual.PageCount;

        if (liCount <= 0)
        {
            UWHelpWindow.SetReading(false);
            return;
        }

        float lfWidth = Screen.width;
        float lfHeight = Screen.height;
        float lfMargin = Mathf.Round(lfHeight * 0.03f);
        float lfBar = Mathf.Round(lfHeight * 0.05f);
        float lfArrowZone = Mathf.Round(lfHeight * 0.08f);
        float lfPageHeight = lfHeight - (2f * lfMargin) - lfBar;
        float lfRoom = lfWidth - (2f * (lfMargin + lfArrowZone));

        // Which pages: the spread around the current page if two fit.
        int liLeft = (msPage % 2 == 1) ? msPage : msPage - 1;
        int liRight = liLeft + 1 < liCount ? liLeft + 1 : -1;
        float lfSpreadWidth = fPageWidth(liLeft, lfPageHeight) + fPageWidth(liRight, lfPageHeight);
        bool lbSpread = lfSpreadWidth <= lfRoom;

        if (!lbSpread)
        {
            liLeft = msPage;
            liRight = -1;
        }

        // Shrink when even a single page is too wide (a very narrow window).
        float lfShown = fPageWidth(liLeft, lfPageHeight) + fPageWidth(liRight, lfPageHeight);

        if (lfShown > lfRoom && lfShown > 0f)
        {
            lfPageHeight *= lfRoom / lfShown;
            lfShown = lfRoom;
        }

        // The kit's units are the help's scale: screen pixels divided by it.
        float lfScale = UWGuiTheme.Scale;
        float lfUnitsWide = lfWidth / lfScale;

        pOKit.Fill(new Rect(0f, 0f, lfUnitsWide, UWGuiTheme.ReferenceHeight), UWGuiTheme.ReadingShade);

        float lfX = Mathf.Round((lfWidth - lfShown) / 2f);
        float lfY = Mathf.Round(lfMargin + ((lfHeight - (2f * lfMargin) - lfBar - lfPageHeight) / 2f));
        bool lbClickedPage = false;

        foreach (int liPage in new[] { liLeft, liRight })
        {
            if (liPage < 0)
                continue;

            int liPageWidth = Mathf.RoundToInt(fPageWidth(liPage, lfPageHeight));
            int liPageHeight = Mathf.RoundToInt(lfPageHeight);
            Rect lOUnits = new Rect(lfX / lfScale, lfY / lfScale, liPageWidth / lfScale, liPageHeight / lfScale);
            Texture2D lOPage = UWManual.GetPage(liPage, liPageWidth, liPageHeight);

            if (lOPage != null)
                pOKit.Picture(lOUnits, lOPage);

            if (pOKit.PressedIn(lOUnits))
                lbClickedPage = true;

            lfX += liPageWidth;
        }

        int liStep = lbSpread ? 2 : 1;

        // Arrows, page number and hint in the help's own scale.
        float lfArrowWidth = lfArrowZone / lfScale;
        float lfArrowTop = (UWGuiTheme.ReferenceHeight / 2f) - 40f;
        UWKitRows.HelpLook lOArrow = UWHelpKitFlow.ButtonLook(UWGuiTheme.HelpArrowSize);

        if (msPage > 0 && pOKit.HelpButton(new Rect(lfMargin / lfScale, lfArrowTop, lfArrowWidth, 80f), "<", lOArrow,
            UWGuiTheme.Panel, UWGuiTheme.Hover, UWGuiTheme.Text))
            fTurn(-liStep);

        if (fLastShown(liLeft, liRight) < liCount - 1
            && pOKit.HelpButton(new Rect(lfUnitsWide - (lfMargin / lfScale) - lfArrowWidth, lfArrowTop, lfArrowWidth, 80f), ">", lOArrow,
                UWGuiTheme.Panel, UWGuiTheme.Hover, UWGuiTheme.Text))
            fTurn(liStep);

        string lsPages = liRight >= 0 && liLeft >= 0
            ? UWText.F("help.manual.pages", liLeft + 1, liRight + 1, liCount)
            : UWText.F("help.manual.page", Mathf.Max(liLeft, liRight) + 1, liCount);

        pOKit.HelpText(new Rect(0f, UWGuiTheme.ReferenceHeight - (lfBar / lfScale) - (lfMargin / lfScale), lfUnitsWide, lfBar / lfScale),
            lsPages + "      " + UWText.T("help.manual.reading_keys"), UWHelpKitFlow.OneLine(UWHelpLookEnum.Text, TextAnchor.MiddleCenter));

        // The keys and the wheel - the game hears none of them while reading.
        UnityEngine.InputSystem.Keyboard lOKeyboard = UnityEngine.InputSystem.Keyboard.current;

        if (lOKeyboard != null)
        {
            if (pOKit.KeyWithRepeat(UnityEngine.InputSystem.Key.LeftArrow) || pOKit.KeyWithRepeat(UnityEngine.InputSystem.Key.PageUp))
                fTurn(-liStep);

            if (pOKit.KeyWithRepeat(UnityEngine.InputSystem.Key.RightArrow) || pOKit.KeyWithRepeat(UnityEngine.InputSystem.Key.PageDown)
                || pOKit.KeyWithRepeat(UnityEngine.InputSystem.Key.Space))
                fTurn(liStep);

            if (lOKeyboard.homeKey.wasPressedThisFrame)
                msPage = 0;

            if (lOKeyboard.endKey.wasPressedThisFrame)
                msPage = liCount - 1;

            // Tab not with Alt: that is the switch to another program.
            if (UWBack.Takes(UWBackOwner.HelpReading) || (lOKeyboard.tabKey.wasPressedThisFrame && !lOKeyboard.altKey.isPressed))
            {
                UWHelpWindow.SetReading(false);
                return;
            }
        }

        float lfWheel = pOKit.WheelIn(new Rect(0f, 0f, lfUnitsWide, UWGuiTheme.ReferenceHeight));

        if (lfWheel != 0f)
            fTurn(lfWheel < 0f ? liStep : -liStep);
        else if (lbClickedPage)
        {
            // The click that closes is the help's: the game gets neither its release nor the
            // button held down after it (per user, 2026-09-26).
            UWHelpWindow.SetReading(false, true);
        }
    }
}
