using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE MODS WINDOW (per user, 2026-11-09; Docs/MOD-SUPPORT-PLAN.md phase 0): a window of the bar of its own,
/// which took over the Game window's mods line. Left every mod in the folder Mods in its load order, each with
/// its switch and Up and Down; right the chosen mod - its name, version and kind, its authors, credit and
/// licence, whether it is loaded and why not, and its readme (README.md or README.txt, else mod.json's
/// description). Below the buttons for the folder and the log. The order and the switches are written at once
/// to the folder's order.json (UWModOrder), which another program may write as well; they take effect at the
/// next start or load, when the catalog is scanned and the editor's packages are applied (UWMods).
/// </summary>
public partial class UWSetupMenu
{
    /// <summary>The mod whose details stand on the right (its id), null for the first.</summary>
    private string msModsChosen;

    private Vector2 mOModsListScroll;

    private Vector2 mOModsTextScroll;

    /// <summary>The chosen mod's readme, read once when it is chosen (a zip is opened for it).</summary>
    private string msModsReadmeOf;

    private string msModsReadme;

    /// <summary>msModsReadme is rich text (a README.md turned by UWMarkdownLite).</summary>
    private bool mbModsReadmeRich;

    /// <summary>Why order.json could not be written, shown until the window closes; null when it was.</summary>
    private string msModsAnswer;

    private const float ModsRow = 32f;

    private void fDrawModsDialog(float pfWidth)
    {
        IUWRows lORows = mOPassRows;
        UWModCatalog lOCatalog = UWMods.Fresh();
        float lfTop = mfBarBottom + ScreenMargin + 10f;
        // THE WHOLE SCREEN below the bar (per user, 2026-11-09: a folder used a lot fills the list).
        float lfHeight = UWGuiTheme.ReferenceHeight - ScreenMargin - lfTop;
        Rect lODialog = lORows.Dialog(pfWidth, pfWidth, lfTop, lfHeight);
        float lfLeft = lODialog.x + 20f;
        float lfInner = lODialog.width - 40f;
        float lfY = lODialog.y + 14f;

        lORows.Title(new Rect(lfLeft, lfY, lfInner, 28f), UWText.T("setup.mods.title"));
        lfY += 38f;

        float lfBottom = lODialog.yMax - 96f;
        float lfListWidth = Mathf.Floor(lfInner * 0.5f);
        float lfDetailsLeft = lfLeft + lfListWidth + 20f;
        float lfDetailsWidth = lfInner - lfListWidth - 20f;
        List<UWModCatalog.Entry> lOAll = lOCatalog.All;
        List<UWModCatalog.Broken> lOBroken = lOCatalog.BrokenMods;

        if (lOAll.Count == 0 && lOBroken.Count == 0)
            lORows.Hint(lfLeft, lfY, lfInner, UWText.T("setup.mods.none"));
        else
        {
            // The chosen one: a mod by its id, a broken one by its place; else the first of the list.
            UWModCatalog.Entry lOChosen = lOAll.Find(pOEntry => pOEntry.Id == msModsChosen);
            UWModCatalog.Broken lOChosenBroken = lOChosen == null ? lOBroken.Find(pOBroken => BrokenKey + pOBroken.Location == msModsChosen) : null;

            if (lOChosen == null && lOChosenBroken == null)
            {
                if (lOAll.Count > 0)
                    lOChosen = lOAll[0];
                else
                    lOChosenBroken = lOBroken[0];
            }

            fDrawModsList(lORows, lOCatalog, lOChosen, lOChosenBroken, new Rect(lfLeft, lfY, lfListWidth, lfBottom - lfY));

            Rect lODetails = new Rect(lfDetailsLeft, lfY, lfDetailsWidth, lfBottom - lfY);

            if (lOChosen != null)
                fDrawModDetails(lORows, lOCatalog, lOChosen, lODetails);
            else
                fDrawBrokenDetails(lORows, lOCatalog, lOChosenBroken, lODetails);
        }

        // Below: when the changes count, a write that failed, the folder and the log.
        float lfLine = lODialog.yMax - 88f;

        lORows.Info(new Rect(lfLeft, lfLine, lfInner, 24f), msModsAnswer ?? UWText.T("setup.mods.next_start"),
            msModsAnswer != null ? UWRowInfoEnum.Error : UWRowInfoEnum.Dim);

        string lsMods = UWText.T("setup.game.open_mods");
        float lfButton = lORows.FitWidth(lsMods, 120f);
        float lfButtonTop = lODialog.yMax - 46f;

        if (lORows.Action(new Rect(lfLeft, lfButtonTop, lfButton, 30f), lsMods))
            fOpenFolder(UWMods.Folder);

        if (!string.IsNullOrEmpty(Application.consoleLogPath))
        {
            string lsLog = UWText.T("setup.game.open_log");

            if (lORows.Action(new Rect(lfLeft + lfButton + 10f, lfButtonTop, lORows.FitWidth(lsLog, 120f), 30f), lsLog))
                fOpenFolder(System.IO.Path.GetDirectoryName(Application.consoleLogPath));
        }

        if (lORows.Done(lODialog))
        {
            meOpenDialog = DialogEnum.None;
            msModsAnswer = null;
        }
    }

    /// <summary>The list's mark of the games a mod is for: "I", "II", "I / II" (none named: both).</summary>
    private static string fGamesMark(UWModManifest pOManifest)
    {
        bool lbFirst = pOManifest.IsFor("uw1");
        bool lbSecond = pOManifest.IsFor("uw2");

        return lbFirst && lbSecond ? "I / II" : lbSecond ? "II" : lbFirst ? "I" : "-";
    }

    /// <summary>msModsChosen of a broken entry: this before its place (no id has it).</summary>
    private const string BrokenKey = "!";

    /// <summary>
    /// A mod's state for the list's colour and the details' line (per user, 2026-11-09, one look for both): loaded;
    /// switched off (dim); for the other game only (dim, it does not load in this one); not loaded with why (red) -
    /// the catalog's reason, or the game files' for an editor's package they refused at the last start.
    /// </summary>
    private static string fModState(UWModCatalog pOCatalog, UWModCatalog.Entry pOEntry, out UWRowInfoEnum peLook)
    {
        if (pOCatalog.IsDisabled(pOEntry.Id))
        {
            peLook = UWRowInfoEnum.Dim;

            return UWText.T("setup.mods.state_off");
        }

        string lsWhy = pOCatalog.ReasonOf(pOEntry.Id);

        // A conflict in the current order, at once (per user, 2026-11-09): the other package's name, the shared files.
        UWModCatalog.Conflict lOConflict = lsWhy == null ? pOCatalog.ConflictOf(UWMods.GameId(UnderworldRevisited.UWSettings.Game), pOEntry.Id) : null;

        if (lOConflict != null)
        {
            peLook = UWRowInfoEnum.Error;

            return UWText.F("setup.mods.conflict", lOConflict.With.Manifest.Name ?? lOConflict.With.Id, string.Join(", ", lOConflict.Files));
        }

        // What only applying shows, from the last start; its conflicts are left out - the order may have changed since.
        UWModGameFiles lOFiles = UWMods.GameFiles(UnderworldRevisited.UWSettings.Game);

        if (lsWhy == null && lOFiles != null)
        {
            string lsRefused = lOFiles.Refused.Find(psLine => psLine.StartsWith(pOEntry.Id + ": ", System.StringComparison.Ordinal)
                && !psLine.EndsWith(UWModGameFiles.ConflictMark, System.StringComparison.Ordinal));

            if (lsRefused != null)
                lsWhy = lsRefused.Substring(pOEntry.Id.Length + 2);
        }

        if (lsWhy != null)
        {
            peLook = UWRowInfoEnum.Error;

            return UWText.F("setup.mods.state_refused", lsWhy);
        }

        if (!pOEntry.Manifest.IsFor(UWMods.GameId(UnderworldRevisited.UWSettings.Game)))
        {
            List<string> lsTitles = new List<string>();

            foreach (string lsGame in pOEntry.Manifest.Games)
                lsTitles.Add(string.Equals(lsGame, "uw2", System.StringComparison.OrdinalIgnoreCase) ? UWGameProfiles.Uw2.Title
                    : string.Equals(lsGame, "uw1", System.StringComparison.OrdinalIgnoreCase) ? UWGameProfiles.Uw1.Title : lsGame);

            peLook = UWRowInfoEnum.Dim;

            return UWText.F("setup.mods.other_game", string.Join(", ", lsTitles));
        }

        peLook = UWRowInfoEnum.Accent;

        return UWText.T("setup.mods.state_loaded");
    }

    /// <summary>The list: per mod its switch, its name in its state's colour (a click chooses it) and Up and Down;
    /// then what is no mod, in red, chosen the same way. Scrolled where it does not fit.</summary>
    private void fDrawModsList(IUWRows pORows, UWModCatalog pOCatalog, UWModCatalog.Entry pOChosen, UWModCatalog.Broken pOChosenBroken, Rect pOView)
    {
        List<UWModCatalog.Entry> lOAll = pOCatalog.All;
        List<UWModCatalog.Broken> lOBroken = pOCatalog.BrokenMods;
        float lfContent = ((lOAll.Count + lOBroken.Count) * ModsRow) + 4f;
        bool lbBar = lfContent > pOView.height;
        float lfWidth = pOView.width - (lbBar ? ScrollBarRoom : 0f);
        string lsUp = UWText.T("setup.mods.up");
        string lsDown = UWText.T("setup.mods.down");
        float lfUp = pORows.FitWidth(lsUp, 54f);
        float lfDown = pORows.FitWidth(lsDown, 54f);
        float lfToggle = 30f;
        float lfGames = pORows.FitWidth("I / II", 44f);
        float lfName = lfWidth - lfToggle - lfGames - lfUp - lfDown - 24f;

        mOModsListScroll = pORows.BeginScroll(pOView, mOModsListScroll, new Rect(0f, 0f, lfWidth, Mathf.Max(pOView.height, lfContent)));

        for (int liAt = 0; liAt < lOAll.Count; liAt++)
        {
            UWModCatalog.Entry lOEntry = lOAll[liAt];
            float lfY = liAt * ModsRow;

            // THE CHOSEN ROW'S GROUND, from the switch to Down (per user, 2026-11-09: its Up and Down found at a glance).
            if (pOChosen != null && lOEntry.Id == pOChosen.Id)
                pORows.RowHighlight(new Rect(0f, lfY, lfWidth, ModsRow));
            bool lbOn = !pOCatalog.IsDisabled(lOEntry.Id);
            bool lbWanted = pORows.Toggle(new Rect(0f, lfY + 2f, lfToggle - 4f, 26f), lbOn, string.Empty);

            if (lbWanted != lbOn)
                fWriteModOrder(pOCatalog, -1, -1, lOEntry.Id, !lbWanted);

            fModState(pOCatalog, lOEntry, out UWRowInfoEnum leLook);

            if (pORows.ItemTinted(new Rect(lfToggle, lfY + 2f, lfName, 26f), lOEntry.Manifest.Name ?? lOEntry.Id,
                    pOChosen != null && lOEntry.Id == pOChosen.Id, leLook == UWRowInfoEnum.Accent ? UWRowInfoEnum.Row : leLook))
                msModsChosen = lOEntry.Id;

            bool lbEnabled = pORows.Enabled;

            pORows.Enabled = lbEnabled && liAt > 0;

            // THE GAMES it is for (per user, 2026-11-09): I, II, or I / II for both (a mod naming none is for both).
            pORows.Info(new Rect(lfToggle + lfName + 8f, lfY + 2f, lfGames, 26f), fGamesMark(lOEntry.Manifest), UWRowInfoEnum.Dim);

            if (pORows.Action(new Rect(lfToggle + lfName + lfGames + 16f, lfY + 2f, lfUp, 26f), lsUp))
                fWriteModOrder(pOCatalog, liAt, liAt - 1, null, false);

            pORows.Enabled = lbEnabled && liAt < lOAll.Count - 1;

            if (pORows.Action(new Rect(lfToggle + lfName + lfGames + 20f + lfUp, lfY + 2f, lfDown, 26f), lsDown))
                fWriteModOrder(pOCatalog, liAt, liAt + 1, null, false);

            pORows.Enabled = lbEnabled;
        }

        for (int liAt = 0; liAt < lOBroken.Count; liAt++)
        {
            UWModCatalog.Broken lOItem = lOBroken[liAt];

            if (lOItem == pOChosenBroken)
                pORows.RowHighlight(new Rect(0f, (lOAll.Count + liAt) * ModsRow, lfWidth, ModsRow));

            if (pORows.ItemTinted(new Rect(lfToggle, ((lOAll.Count + liAt) * ModsRow) + 2f, lfName, 26f), lOItem.Name, lOItem == pOChosenBroken, UWRowInfoEnum.Error))
                msModsChosen = BrokenKey + lOItem.Location;
        }

        pORows.EndScroll();
    }

    /// <summary>The chosen mod: name, what it is, by whom, its credit and licence, whether it is loaded, and its
    /// readme below in a scroll view of its own.</summary>
    private void fDrawModDetails(IUWRows pORows, UWModCatalog pOCatalog, UWModCatalog.Entry pOEntry, Rect pOArea)
    {
        UWModManifest lOManifest = pOEntry.Manifest;
        float lfY = pOArea.y;

        pORows.Info(new Rect(pOArea.x, lfY, pOArea.width, 26f), lOManifest.Name ?? pOEntry.Id, UWRowInfoEnum.Heading);
        lfY += 28f;

        string lsKind = lOManifest.IsEditorPackage ? UWText.T("setup.mods.kind_uwmod")
            : string.IsNullOrEmpty(lOManifest.Kind) ? string.Empty : lOManifest.Kind;
        string lsWhat = pOEntry.Id + "  " + UWText.F("setup.mods.version", lOManifest.Version ?? "?") + (lsKind.Length > 0 ? "  " + lsKind : string.Empty);

        pORows.Info(new Rect(pOArea.x, lfY, pOArea.width, 24f), lsWhat, UWRowInfoEnum.Dim);
        lfY += 24f;

        if (lOManifest.Authors.Count > 0)
        {
            pORows.Info(new Rect(pOArea.x, lfY, pOArea.width, 24f), UWText.F("setup.mods.by", string.Join(", ", lOManifest.Authors)), UWRowInfoEnum.Row);
            lfY += 24f;
        }

        if (!string.IsNullOrEmpty(lOManifest.Credit))
        {
            pORows.Info(new Rect(pOArea.x, lfY, pOArea.width, 24f), lOManifest.Credit, UWRowInfoEnum.Row);
            lfY += 24f;
        }

        if (!string.IsNullOrEmpty(lOManifest.Licence))
        {
            pORows.Info(new Rect(pOArea.x, lfY, pOArea.width, 24f), UWText.F("setup.mods.licence", lOManifest.Licence), UWRowInfoEnum.Dim);
            lfY += 24f;
        }

        // Where it lies, then loaded, switched off, for the other game, or why not (fModState).
        pORows.Info(new Rect(pOArea.x, lfY, pOArea.width, 24f), UWText.F("setup.mods.where", pOCatalog.WhereInFolder(pOEntry.Source.Location)), UWRowInfoEnum.Dim);
        lfY += 24f;

        string lsState = fModState(pOCatalog, pOEntry, out UWRowInfoEnum leLook);
        float lfStateHeight = Mathf.Max(24f, pORows.TextHeight(lsState, leLook, pOArea.width));

        // ITS WHOLE HEIGHT (per user, 2026-11-09: a three-line conflict lay over the line above): the red and dim looks
        // wrap and centre in their rectangle, so a rectangle of one line let them grow upwards out of it.
        pORows.Info(new Rect(pOArea.x, lfY, pOArea.width, lfStateHeight), lsState, leLook);
        lfY += lfStateHeight + 6f;
        pORows.Separator(new Rect(pOArea.x, lfY, pOArea.width, 2f));
        lfY += 8f;

        // The readme, read once per choice; a README.md drawn as the little of Markdown it uses (UWMarkdownLite),
        // from the top of its view (per user, 2026-11-09: as a row's text it stood in the middle of its height).
        if (msModsReadmeOf != pOEntry.Id)
        {
            msModsReadmeOf = pOEntry.Id;

            string lsReadme = pOEntry.Readme(out bool lbMarkdown);

            mbModsReadmeRich = lbMarkdown;
            msModsReadme = lsReadme == null ? UWText.T("setup.mods.no_readme")
                : lbMarkdown ? UWMarkdownLite.ToRichText(lsReadme, UWGuiTheme.AccentHex) : lsReadme;
            mOModsTextScroll = Vector2.zero;
        }

        Rect lOView = new Rect(pOArea.x, lfY, pOArea.width, pOArea.yMax - lfY);
        float lfTextWidth = pOArea.width - ScrollBarRoom;
        float lfText = pORows.ParagraphHeight(msModsReadme, lfTextWidth, mbModsReadmeRich) + 8f;

        mOModsTextScroll = pORows.BeginScroll(lOView, mOModsTextScroll, new Rect(0f, 0f, lfTextWidth, Mathf.Max(lOView.height, lfText)));
        pORows.Paragraph(0f, 0f, lfTextWidth, msModsReadme, mbModsReadmeRich);
        pORows.EndScroll();
    }

    /// <summary>What is no mod: its name, where it lies, what is wrong, and how to mend it.</summary>
    private void fDrawBrokenDetails(IUWRows pORows, UWModCatalog pOCatalog, UWModCatalog.Broken pOBroken, Rect pOArea)
    {
        float lfY = pOArea.y;

        pORows.Info(new Rect(pOArea.x, lfY, pOArea.width, 26f), pOBroken.Name, UWRowInfoEnum.Heading);
        lfY += 28f;
        pORows.Info(new Rect(pOArea.x, lfY, pOArea.width, 24f), UWText.F("setup.mods.where", pOCatalog.WhereInFolder(pOBroken.Location)), UWRowInfoEnum.Dim);
        lfY += 24f;

        string lsState = UWText.F("setup.mods.state_refused", pOBroken.Why);

        float lfStateHeight = Mathf.Max(24f, pORows.TextHeight(lsState, UWRowInfoEnum.Error, pOArea.width));

        pORows.Info(new Rect(pOArea.x, lfY, pOArea.width, lfStateHeight), lsState, UWRowInfoEnum.Error);
        lfY += lfStateHeight + 6f;
        pORows.Separator(new Rect(pOArea.x, lfY, pOArea.width, 2f));
        lfY += 8f;

        string lsFix;

        switch (pOBroken.Kind)
        {
            case UWModCatalog.BrokenKind.NoManifest: lsFix = UWText.T("setup.mods.fix_no_manifest"); break;
            case UWModCatalog.BrokenKind.BadManifest: lsFix = UWText.T("setup.mods.fix_bad_manifest"); break;
            case UWModCatalog.BrokenKind.NoId: lsFix = UWText.T("setup.mods.fix_no_id"); break;
            default: lsFix = UWText.T("setup.mods.fix_bad_zip"); break;
        }

        pORows.Paragraph(pOArea.x, lfY, pOArea.width, lsFix, false);
    }

    /// <summary>
    /// Writes order.json after a change: piFrom moved to piTo (Up, Down), or psSwitched switched off or on. Every
    /// mod of the list goes into the order as it stands, so the file holds the whole order the window showed.
    /// </summary>
    private void fWriteModOrder(UWModCatalog pOCatalog, int piFrom, int piTo, string psSwitched, bool pbOff)
    {
        UWModOrder lOOrder = new UWModOrder();

        foreach (UWModCatalog.Entry lOEntry in pOCatalog.All)
            lOOrder.Order.Add(lOEntry.Id);

        // Ids order.json names whose mods are not there now (taken out for a while) keep their place at the end.
        foreach (string lsId in pOCatalog.Order.Order)
        {
            if (!lOOrder.Order.Contains(lsId))
                lOOrder.Order.Add(lsId);
        }

        foreach (string lsId in pOCatalog.Order.Disabled)
            lOOrder.Disabled.Add(lsId);

        if (piFrom >= 0 && piTo >= 0 && piFrom < pOCatalog.All.Count && piTo < pOCatalog.All.Count)
        {
            string lsMoved = lOOrder.Order[piFrom];

            lOOrder.Order[piFrom] = lOOrder.Order[piTo];
            lOOrder.Order[piTo] = lsMoved;
        }

        if (psSwitched != null)
        {
            if (pbOff)
                lOOrder.Disabled.Add(psSwitched);
            else
                lOOrder.Disabled.Remove(psSwitched);
        }

        string lsError = lOOrder.Write(UWMods.Folder);

        msModsAnswer = lsError != null ? UWText.F("setup.mods.write_failed", lsError) : null;
        Debug.Log("[Mods] " + (lsError == null ? UWModOrder.FileName + " written: " + string.Join(", ", lOOrder.Order)
            + (lOOrder.Disabled.Count > 0 ? "; off: " + string.Join(", ", lOOrder.Disabled) : string.Empty) : UWModOrder.FileName + " not written: " + lsError));
        UWMods.Rescan();
    }
}
