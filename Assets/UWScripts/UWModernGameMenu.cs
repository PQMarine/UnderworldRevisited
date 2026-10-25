using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN SCHEME'S GAME MENU (Escape, F1, the Ctrl shortcuts - routed by
/// UWGameUI.fCheckModernKeys) does what the original's options do, through the same work:
/// the save, load and quit flows of UWSaveFlow (cleanup 5.18, 2026-10-23: one flow for both
/// menus), UWSoundOptions, UWGraphicsDetail. It refuses where they refuse
/// (UWHudOptions.RefusesToOpen) and stops time as they do.
///
/// OUT OF UWModernHud since cleanup 5.16 (2026-10-22), unchanged in what it does: a plain class the
/// HUD owns and drives - the pad's stepping first in the HUD's Update (StepByPad), the pages in the
/// HUD's OnGUI - so it keeps the HUD component's place among the IMGUI windows (the setup bar,
/// UWSetupMenu, lies over it) and its holds' timing. It reads the scene's UI, interaction and
/// control scheme through the HUD, which finds them in its Start, as the menu's own fields did.
///
/// ON THE KIT since cleanup 8b.4 (2026-10-24): the pages are drawn by UWKitRows in the HUD's
/// LateUpdate (see "The menu on the kit" below), on a canvas of their own under the setup bar's
/// (UWUiSorting); the IMGUI form in the HUD's OnGUI went at the end of phase 8b.
/// </summary>
public class UWModernGameMenu
{
    public enum PageEnum
    {
        Closed,
        Main,
        Save,
        SaveName,
        Load,
        Detail,

        /// <summary>The layout editor (UWModernLayoutEditor) - no page of this menu is drawn.</summary>
        Layout
    }

    public PageEnum Page => mePage;

    /// <summary>Whether a save game name is being typed - the game's keys stay silent then
    /// (UWTextEntryState.IsTextEntryActive).</summary>
    public static bool IsTypingName => UWModernHud.Instance != null && UWModernHud.Instance.Menu != null
        && UWModernHud.Instance.Menu.mePage == PageEnum.SaveName;

    public bool IsOpen => mePage != PageEnum.Closed;

    /// <summary>A page of the menu is drawn (not the layout editor) - the menu bar of the setup
    /// shows itself over it then (UWSetupMenu, per user 2026-10-06).</summary>
    public bool ShowsMenuPage => mePage != PageEnum.Closed && mePage != PageEnum.Layout;

    private const string MenuModalHold = "modern menu";

    private const string MenuClockHold = "modern menu";

    /// <summary>The HUD that owns the menu; the scene's parts below are its (found in its Start).</summary>
    private readonly UWModernHud mOHud;

    private UWGameUI mOUi => mOHud.Ui;

    private Interaction mOInteraction => mOHud.Interaction;

    private UWControlScheme mOScheme => mOHud.Scheme;

    private PageEnum mePage = PageEnum.Closed;

    private int miSaveSlot;

    private string msSaveName = string.Empty;

    private bool mbFocusName;

    public UWModernGameMenu(UWModernHud pOHud)
    {
        mOHud = pOHud;
    }

    /// <summary>Escape and F1: open the menu, or close it - from a sub page back to the main one.</summary>
    public void ToggleMenu()
    {
        // From the layout editor only through its save question (UWModernLayoutEditor.RequestLeave).
        if (mePage == PageEnum.Layout && UWModernLayoutEditor.Instance != null)
        {
            UWModernLayoutEditor.Instance.RequestLeave(() => mePage = PageEnum.Main);
            return;
        }

        if (mePage == PageEnum.Closed)
            OpenMenu(PageEnum.Main);
        else if (mePage == PageEnum.Main)
            CloseMenu();
        else
            mePage = PageEnum.Main;
    }

    /// <summary>Opens the menu on a page - refused as the original's options refuse (partway
    /// through an action, between worlds).</summary>
    public void OpenMenu(PageEnum pePage)
    {
        if (pePage == PageEnum.Closed)
        {
            CloseMenu();
            return;
        }

        if (mePage == PageEnum.Closed)
        {
            if (mOUi != null && mOUi.Options != null && mOUi.Options.RefusesToOpen())
                return;

            if (mOScheme != null)
                mOScheme.HoldUiModal(MenuModalHold);

            UWGameClock.Hold(MenuClockHold);
        }

        mePage = pePage;
    }

    public void CloseMenu()
    {
        if (mOKit != null)
            mOKit.KeyNav.Reset();

        mePage = PageEnum.Closed;
        ReleaseHolds();
    }

    /// <summary>The kit's name field lets go with the HUD (its OnDestroy).</summary>
    public void Dispose()
    {
        if (mOKit != null)
            mOKit.Dispose();
    }

    public void ReleaseHolds()
    {
        if (mOScheme != null)
            mOScheme.ReleaseUiModal(MenuModalHold);

        UWGameClock.Release(MenuClockHold);
    }

    /// <summary>The name trimmed - the modern menu's rule (the classic panel saves it as typed);
    /// the menu closes between the write and the report (UWSaveFlow).</summary>
    private void fSave()
    {
        string lsError = UWSaveFlow.Write(miSaveSlot, msSaveName.Trim());

        CloseMenu();
        UWSaveFlow.ReportSave(mOInteraction, mOUi != null ? mOUi.mOUWData : null, lsError, false);
    }

    /// <summary>An empty slot says so and leaves the menu up; a full one closes the menu and
    /// loads (UWSaveFlow).</summary>
    private void fLoad(int piSlot)
    {
        if (!UWSaveFlow.Exists(piSlot))
        {
            UWSaveFlow.ReportNoSaveGame(mOInteraction);

            return;
        }

        CloseMenu();
        UWSaveFlow.Restore(mOInteraction, piSlot);
    }

    private void fQuitToMainMenu()
    {
        CloseMenu();
        UWSaveFlow.QuitToMainMenu();
    }

    // ------------------------------------------------- The gamepad in the menu

    /// <summary>The gamepad steps through the buttons (UWPadEntryStepper, per user 2026-10-08:
    /// as in the context menu).</summary>
    private readonly UWPadEntryStepper mOMenuStepper = new UWPadEntryStepper();

    /// <summary>The page the arrow keys' entries were counted on (the kit's UWKitKeyNav since cleanup
    /// 8b.4, UWImguiKeyNav until then).</summary>
    private PageEnum meKeyNavPage = PageEnum.Closed;

    /// <summary>The buttons' screen rectangles of the last drawn frame, and those being drawn.</summary>
    private readonly List<Rect> mOEntryRects = new List<Rect>();

    private readonly List<Rect> mOEntryRectsDrawing = new List<Rect>();

    /// <summary>The menu's area and scale while it draws, for the buttons' screen places.</summary>
    private Vector2 mOAreaAt;

    private float mfMenuScale = 1f;

    private PageEnum meSteppedPage = PageEnum.Closed;

    /// <summary>Once a frame while the menu is up: a new page (or the menu opened) snaps the pointer
    /// onto its first button when the pad was used last.</summary>
    public void StepByPad()
    {
        if (!IsOpen || mePage == PageEnum.Layout || UWSetupMenu.HasOpenPanel)
        {
            meSteppedPage = PageEnum.Closed;
            return;
        }

        if (meSteppedPage != mePage)
        {
            meSteppedPage = mePage;
            mOMenuStepper.Opened();
            return;
        }

        mOMenuStepper.Update(mOEntryRects);
    }

    /// <summary>The detail levels' names, by LevelEnum (cleanup step 8.5, 2026-10-24: until then
    /// the enum's own names). VeryHigh reads Custom since cleanup 8d.3, as in the Graphics window.</summary>
    private static readonly string[] msDetailIds = { "menu.detail.low", "menu.detail.medium", "menu.detail.high", "menu.detail.custom" };

    // ================================================= The menu on the kit (cleanup 8b.4, 2026-10-24)
    //
    // THE MENU ON THE KIT (phase 8b, decision 26): every page drawn by UWKitRows in the game menu's
    // look (UWRowLookEnum.GameMenu, the modern font) on a canvas of its own (UWUiSorting.ModernGameMenu)
    // in the HUD's LateUpdate - the panel, its area and the pages laid out as GUILayout laid out the
    // IMGUI form: a title 40 high, a button 6 below the last and 38 high over the area's width, the
    // presets 4 below and 30 high set in by 28, Quit at the area's foot, the save name's line as tall as
    // its wrapped text and the field 36 high. The buttons are the arrow keys' entries as before
    // (UWKitKeyNav), their screen rectangles the pad's steps (UWPadEntryStepper); the save name types
    // into the kit's field (UWTextEntry, UWTypedKeys: on Linux too), 30 characters, Enter saves and
    // Escape goes back wherever the keyboard is, as before.

    /// <summary>The menu's drawer on the kit, made on its first frame.</summary>
    private UWKitRows mOKit;

    /// <summary>The save name's field on the kit.</summary>
    private const string NameField = "UWModernSaveName";

    /// <summary>The menu's area: the panel (380 by 560, 90 from the top) less 24 left and right and 18
    /// above and below.</summary>
    private const float AreaWidth = 380f - 48f;

    private const float AreaHeight = 560f - 36f;

    /// <summary>Where the next piece of the page stands below the area's top.</summary>
    private float mfKitY;

    private static UWKitRows.HelpLook KitLook(int piSize, Color pOColour, TextAnchor peAnchor, bool pbBold = false, bool pbWraps = false)
    {
        return new UWKitRows.HelpLook { Size = piSize, Colour = pOColour, Anchor = peAnchor, Bold = pbBold, Wraps = pbWraps };
    }

    /// <summary>
    /// Once a frame after the HUD's parts: the open page on the kit, or the kit's canvas dark - while
    /// the menu is closed, on the layout editor's page, and while a window of the setup bar is open
    /// over it (per user, 2026-10-06: the bar in the in-game menu, so nobody has to go to the main
    /// menu; while one of its windows is open this menu neither draws nor takes clicks).
    /// </summary>
    public void LateUpdate()
    {
        bool lbShown = mePage != PageEnum.Closed && mePage != PageEnum.Layout && !UWSetupMenu.HasOpenPanel;

        if (mOKit == null)
        {
            if (!lbShown)
                return;

            mOKit = new UWKitRows(mOHud.transform, UWUiSorting.ModernGameMenu, UWRowLookEnum.GameMenu);
        }

        if (!lbShown)
        {
            // A name being typed has the keyboard again once the menu shows again (IMGUI's field kept
            // its focus while it was not drawn).
            if (mePage == PageEnum.SaveName && mOKit.FieldHasKeys(NameField))
                mbFocusName = true;

            mOKit.Begin(false, false, false, false);
            return;
        }

        // THE ARROW KEYS (per user 2026-10-12, a fallback for a mouse that cannot click): the first
        // arrow puts the focus on a button, Enter or Space presses it; the bar's own navigation (F10)
        // has the keys while it is on, the name field while it has the keyboard, the debug tools' tab as
        // well (any IMGUI field with the keyboard held them until cleanup 8b.4). A new page
        // starts at its first button.
        mOKit.Begin(true, !UWSetupMenu.KeyNavActive, true, UWDebugTools.IsTyping);

        if (mePage != meKeyNavPage)
        {
            meKeyNavPage = mePage;

            if (mOKit.KeyNav.IsActive)
                mOKit.KeyNav.Activate();
        }

        float lfScale = UWGuiTheme.Scale;
        Rect lOPanel = new Rect((mOKit.Width - 380f) * 0.5f, 90f, 380f, 560f);

        // The menu's ground in the theme in force (cleanup phase 8d; Plain: the overlay's fill).
        mOKit.Ground(lOPanel, UWWindowGroundEnum.Overlay);

        mOAreaAt = new Vector2(lOPanel.x + 24f, lOPanel.y + 18f);
        mfMenuScale = lfScale;
        mfKitY = 0f;
        mOEntryRectsDrawing.Clear();

        switch (mePage)
        {
            case PageEnum.Main: fKitMain(); break;
            case PageEnum.Save: fKitSlots(true); break;
            case PageEnum.SaveName: fKitSaveName(); break;
            case PageEnum.Load: fKitSlots(false); break;
            case PageEnum.Detail: fKitDetail(); break;
        }

        mOKit.End();

        mOEntryRects.Clear();
        mOEntryRects.AddRange(mOEntryRectsDrawing);
    }

    /// <summary>The next piece of the page: pfSpace below the last, pfHeight high, over the area's width
    /// less pfIndent on its left (GUILayout's Space, the piece's Height, a Space before it in a row).</summary>
    private Rect fKitNext(float pfSpace, float pfHeight, float pfIndent = 0f)
    {
        mfKitY += pfSpace;

        Rect lORect = new Rect(mOAreaAt.x + pfIndent, mOAreaAt.y + mfKitY, AreaWidth - pfIndent, pfHeight);

        mfKitY += pfHeight;

        return lORect;
    }

    private void fKitTitle(string psText)
    {
        mOKit.HelpText(fKitNext(0f, 40f), psText, KitLook(UWGuiTheme.GameMenuTitleSize, UWGuiTheme.Accent, TextAnchor.MiddleCenter, true));
    }

    /// <summary>A button at pORect, an entry of the arrow keys and the pad's steps (fKitRecord).</summary>
    private bool fKitButton(Rect pORect, string psText, Texture2D pOPicture = null, bool pbCurrent = false)
    {
        bool lbPressed = mOKit.MenuButton(pORect, psText, pOPicture,
            KitLook(UWGuiTheme.GameMenuButtonSize, pbCurrent ? UWGuiTheme.Accent : UWGuiTheme.Text, TextAnchor.MiddleCenter),
            UWGuiTheme.Panel, UWGuiTheme.Hover, UWGuiTheme.Accent);

        fKitRecord(pORect);

        return lbPressed;
    }

    /// <summary>A button 6 below the last, 38 high (fButton).</summary>
    private bool fKitButton(string psText, Texture2D pOPicture = null)
    {
        return fKitButton(fKitNext(6f, 38f), psText, pOPicture);
    }

    /// <summary>The button just drawn as a screen rectangle (pixels, bottom-left origin), for the pad.</summary>
    private void fKitRecord(Rect pORect)
    {
        mOEntryRectsDrawing.Add(new Rect(pORect.x * mfMenuScale, Screen.height - (pORect.yMax * mfMenuScale),
            pORect.width * mfMenuScale, pORect.height * mfMenuScale));
    }

    /// <summary>One preset under "Edit layout" (fPresetButton): 4 below the last, set in by 28, 30 high,
    /// the one in force in gold. A press chooses it; true for the press.</summary>
    private bool fKitPreset(string psText, UWModernLayout.PresetEnum pePreset, UWModernLayout.PresetEnum peCurrent, bool pbBuilt)
    {
        bool lbEnabled = mOKit.Enabled;

        mOKit.Enabled = lbEnabled && pbBuilt;

        bool lbPressed = fKitButton(fKitNext(4f, 30f, 28f), psText, null, pePreset == peCurrent);

        mOKit.Enabled = lbEnabled;

        if (lbPressed && pePreset != peCurrent)
            UWModernLayout.ChoosePreset(pePreset);

        return lbPressed;
    }

    private void fKitMain()
    {
        fKitTitle(UWText.T("menu.title"));

        if (fKitButton(UWText.T("menu.resume")))
            CloseMenu();

        // Refused in the Void (the original's check_save, UWHudOptions.RefusesToSave); restoring
        // is always allowed there (check_rest).
        if (fKitButton(UWText.T("menu.save_game")) && (mOUi == null || mOUi.Options == null || !mOUi.Options.RefusesToSave()))
            mePage = PageEnum.Save;

        if (fKitButton(UWText.T("menu.restore_game")))
            mePage = PageEnum.Load;

        // Music, sound and the detail level left this menu on 2026-10-06 (per user): the setup
        // bar at the top offers them in full while the menu is open (UWSetupMenu).
        // THE MOUSE'S RIGHT BUTTON AS A GLYPH (per user, 2026-10-08: not the pad's - "Right button"
        // said nothing of which; Kenney's pixel prompts, UWGlyphs), "RMB" if it is missing.
        Texture2D lOMouseRight = UWGlyphs.MouseRight;
        string lsPointerOption = UWUserSettings.ModernPointerHold ? UWText.T("menu.pointer.hold") : UWText.T("menu.pointer.toggle");

        if (lOMouseRight != null ? fKitButton("  " + lsPointerOption, lOMouseRight) : fKitButton(UWText.F("menu.pointer.rmb", lsPointerOption)))
        {
            UWUserSettings.ModernPointerHold = !UWUserSettings.ModernPointerHold;
            UWUserSettings.Save();
        }

        // The mouse look's speed left this menu on 2026-10-08 (per user): the Controls dialog of
        // the setup bar has it as a slider, reachable while the menu is open.

        // THE INTERFACE PRESETS under "Edit layout" as a heading (per user, 2026-10-09): Classic,
        // Classic+, Modern and Customize, the last opening the layout editor (UWModernLayoutEditor,
        // per user 2026-10-04; the UI size lives in its strip) on the own layout, which the other
        // presets leave alone. Classic and Classic+ are planned - greyed until they are built.
        mOKit.HelpText(fKitNext(10f, 24f), UWText.T("menu.edit_layout"),
            KitLook(UWGuiTheme.GameMenuButtonSize, UWGuiTheme.Accent, TextAnchor.MiddleLeft));

        UWModernLayout.PresetEnum lePreset = UWModernLayout.Preset;

        fKitPreset(UWText.T("menu.preset.classic_wide"), UWModernLayout.PresetEnum.Classic, lePreset, true);
        fKitPreset(UWText.T("menu.preset.classic_modular"), UWModernLayout.PresetEnum.ClassicPlus, lePreset, true);
        fKitPreset(UWText.T("menu.preset.modern"), UWModernLayout.PresetEnum.Modern, lePreset, true);

        if (fKitPreset(UWText.T("menu.preset.customize"), UWModernLayout.PresetEnum.Custom, lePreset, true))
            mePage = PageEnum.Layout;

        // The minimap's north up / turning left this menu on 2026-10-09 (per user): the layout
        // editor's inspector has it on the minimap ("North").

        // Quit at the very bottom of the panel, set apart from the settings (per user,
        // 2026-10-06: "das Quit nach unten schieben") - since music, sound and detail left the
        // menu it had moved up with the rest (GUILayout's FlexibleSpace in the IMGUI form).
        if (fKitButton(new Rect(mOAreaAt.x, mOAreaAt.y + AreaHeight - 38f, AreaWidth, 38f), UWText.T("menu.quit")))
            fQuitToMainMenu();
    }

    private void fKitSlots(bool pbSave)
    {
        fKitTitle(pbSave ? UWText.T("menu.save_game") : UWText.T("menu.restore_game"));

        for (int liSlot = 1; liSlot <= UWSavegameSlots.SlotCount; liSlot++)
        {
            string lsName = UWSaveFlow.SlotName(liSlot);

            if (!fKitButton(liSlot + ": " + lsName))
                continue;

            if (pbSave)
            {
                miSaveSlot = liSlot;
                msSaveName = UWSaveFlow.Exists(liSlot) ? lsName : string.Empty;
                mbFocusName = true;
                mePage = PageEnum.SaveName;
            }
            else
            {
                fLoad(liSlot);
            }
        }

        mfKitY += 12f;

        if (fKitButton(UWText.T("menu.back")))
            mePage = PageEnum.Main;
    }

    /// <summary>The save name: its title and line, the field (30 characters, the keyboard at once),
    /// Save and Cancel. Enter saves and Escape goes back to the slots wherever the keyboard is, as the
    /// IMGUI form read them from every key event of the page; the letter grid's Done and Cancel press
    /// those keys (UWOsKeys).</summary>
    private void fKitSaveName()
    {
        fKitTitle(UWText.F("menu.save_slot_title", miSaveSlot));

        string lsLine = UWText.T("menu.save_name");
        UWKitRows.HelpLook lOLine = KitLook(UWGuiTheme.GameMenuTextSize, UWGuiTheme.Text, TextAnchor.MiddleLeft, false, true);

        mOKit.HelpText(fKitNext(0f, mOKit.HelpTextHeight(lsLine, lOLine, AreaWidth)), lsLine, lOLine);

        if (mbFocusName)
        {
            mOKit.FocusField(NameField, msSaveName ?? string.Empty, 30);
            mbFocusName = false;
        }

        msSaveName = mOKit.TextField(fKitNext(0f, 36f), NameField, msSaveName ?? string.Empty, 30);

        if (UWTextEntry.EnterPressed)
        {
            fSave();
            return;
        }

        if (UWBack.Takes(UWBackOwner.ModernSaveName))
        {
            mePage = PageEnum.Save;
            return;
        }

        if (fKitButton(UWText.T("menu.save")))
            fSave();

        if (fKitButton(UWText.T("menu.cancel")))
            mePage = PageEnum.Save;
    }

    /// <summary>
    /// The Detail page: Low, Medium and High as buttons, the one in force marked. CUSTOM (VeryHigh, the
    /// viewer's own values) as in the Graphics window since cleanup 8d.3 (per user, 2026-10-25): a line
    /// in a button's place, no button - lit and marked while it is in force, dim otherwise; no entry of
    /// the arrow keys or the pad's steps. It is set where its values are, under "Remastered effects..."
    /// in the setup bar's Graphics window (until then this page's fourth button set it alone).
    /// </summary>
    private void fKitDetail()
    {
        fKitTitle(UWText.T("menu.detail.title"));

        foreach (UWGraphicsDetail.LevelEnum leLevel in new[]
        {
            UWGraphicsDetail.LevelEnum.Low, UWGraphicsDetail.LevelEnum.Medium,
            UWGraphicsDetail.LevelEnum.High
        })
        {
            string lsMark = leLevel == UWGraphicsDetail.CurrentLevel ? "> " : string.Empty;

            if (fKitButton(lsMark + UWText.T(msDetailIds[(int)leLevel])))
                UWGraphicsDetail.SetLevel(leLevel);
        }

        bool lbCustom = UWGraphicsDetail.CurrentLevel == UWGraphicsDetail.LevelEnum.VeryHigh;

        mOKit.HelpText(fKitNext(6f, 38f), (lbCustom ? "> " : string.Empty) + UWText.T(msDetailIds[(int)UWGraphicsDetail.LevelEnum.VeryHigh]),
            KitLook(UWGuiTheme.GameMenuButtonSize, lbCustom ? UWGuiTheme.Accent : UWGuiTheme.DimText, TextAnchor.MiddleCenter));

        mfKitY += 12f;

        if (fKitButton(UWText.T("menu.back")))
            mePage = PageEnum.Main;
    }
}
