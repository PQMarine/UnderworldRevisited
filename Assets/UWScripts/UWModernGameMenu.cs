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
        mOKeyNav.Reset();
        mePage = PageEnum.Closed;
        ReleaseHolds();
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

    // ------------------------------------------------- Drawing the menu (IMGUI, as the help window)

    private static readonly Color msBackground = new Color(0.08f, 0.08f, 0.09f, 0.96f);

    private static readonly Color msButton = new Color(0.14f, 0.14f, 0.16f, 1f);

    private static readonly Color msButtonHover = new Color(0.22f, 0.22f, 0.26f, 1f);

    /// <summary>The reference height the menu is laid out for; it scales with the screen.</summary>
    private const float MenuReferenceHeight = 720f;

    private GUIStyle mOTitleStyle;

    private GUIStyle mOButtonStyle;
    private GUIStyle mOCurrentPresetStyle;
    private GUIStyle mOSectionStyle;

    private GUIStyle mOLabelStyle;

    private GUIStyle mOFieldStyle;

    private Texture2D mOBackgroundTexture;

    private Texture2D mOButtonTexture;

    private Texture2D mOButtonHoverTexture;

    private Texture2D mOFieldTexture;

    private static Texture2D fTexture(Color pOColour)
    {
        Texture2D lOTexture = new Texture2D(1, 1);
        lOTexture.SetPixel(0, 0, pOColour);
        lOTexture.Apply();

        return lOTexture;
    }

    private void fEnsureStyles()
    {
        if (mOTitleStyle != null)
            return;

        mOBackgroundTexture = fTexture(msBackground);
        mOButtonTexture = fTexture(msButton);
        mOButtonHoverTexture = fTexture(msButtonHover);
        mOFieldTexture = fTexture(new Color(0.05f, 0.05f, 0.06f, 1f));

        Font lOFont = UWInterfaceFont.ModernOrInterface;

        mOTitleStyle = new GUIStyle { font = lOFont, fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        mOTitleStyle.normal.textColor = UWModernPalette.Accent;

        mOLabelStyle = new GUIStyle { font = lOFont, fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = true };
        mOLabelStyle.normal.textColor = UWModernPalette.HudText;


        mOButtonStyle = new GUIStyle { font = lOFont, fontSize = 16, alignment = TextAnchor.MiddleCenter };
        mOButtonStyle.normal.background = mOButtonTexture;
        mOButtonStyle.normal.textColor = UWModernPalette.HudText;
        mOButtonStyle.hover.background = mOButtonHoverTexture;
        mOButtonStyle.hover.textColor = UWModernPalette.Accent;
        mOButtonStyle.active.background = mOButtonHoverTexture;
        mOButtonStyle.active.textColor = UWModernPalette.Accent;

        mOCurrentPresetStyle = new GUIStyle(mOButtonStyle);
        mOCurrentPresetStyle.normal.textColor = UWModernPalette.Accent;

        mOSectionStyle = new GUIStyle { font = lOFont, fontSize = 16, alignment = TextAnchor.MiddleLeft };
        mOSectionStyle.normal.textColor = UWModernPalette.Accent;

        mOFieldStyle = new GUIStyle { font = lOFont, fontSize = 16, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 0, 0) };
        mOFieldStyle.normal.background = mOFieldTexture;
        mOFieldStyle.normal.textColor = UWModernPalette.HudText;
        mOFieldStyle.focused.background = mOFieldTexture;
        mOFieldStyle.focused.textColor = UWModernPalette.HudText;
    }

    public void OnGUI()
    {
        UWImguiInput.BeginOnGUI();

        if (mePage == PageEnum.Closed || mePage == PageEnum.Layout)
            return;

        // The setup bar's menus and dialogs lie over this menu while it is open (per user,
        // 2026-10-06: the bar in the in-game menu, so nobody has to go to the main menu); while
        // one of them is open this menu neither draws nor takes clicks.
        if (UWSetupMenu.HasOpenPanel)
            return;

        fEnsureStyles();

        float lfScale = Screen.height / MenuReferenceHeight;
        Matrix4x4 lOMatrix = GUI.matrix;

        GUI.matrix = Matrix4x4.Scale(new Vector3(lfScale, lfScale, 1f));
        UWImguiInput.AfterMatrix();

        float lfWidth = Screen.width / lfScale;
        Rect lOPanel = new Rect((lfWidth - 380f) * 0.5f, 90f, 380f, 560f);

        GUI.DrawTexture(lOPanel, mOBackgroundTexture);
        GUILayout.BeginArea(new Rect(lOPanel.x + 24f, lOPanel.y + 18f, lOPanel.width - 48f, lOPanel.height - 36f));
        UWImguiInput.Sync();

        // The buttons' places for the gamepad (fRecordEntry), gathered in the repaint.
        mOAreaAt = new Vector2(lOPanel.x + 24f, lOPanel.y + 18f);
        mfMenuScale = lfScale;

        if (Event.current.type == EventType.Repaint)
            mOEntryRectsDrawing.Clear();

        // THE ARROW KEYS (UWImguiKeyNav, per user 2026-10-12, a fallback for a mouse that cannot
        // click): the first arrow puts the focus on a button, Enter or Space presses it; the bar's
        // own navigation (F10) has the keys while it is on. A new page starts at its first button.
        if (mePage != meKeyNavPage)
        {
            meKeyNavPage = mePage;

            if (mOKeyNav.IsActive)
                mOKeyNav.Activate();
        }

        mOKeyNav.Begin(!UWSetupMenu.KeyNavActive, true);

        switch (mePage)
        {
            case PageEnum.Main: fDrawMain(); break;
            case PageEnum.Save: fDrawSlots(true); break;
            case PageEnum.SaveName: fDrawSaveName(); break;
            case PageEnum.Load: fDrawSlots(false); break;
            case PageEnum.Detail: fDrawDetail(); break;
        }

        mOKeyNav.End();

        GUILayout.EndArea();
        UWImguiInput.Sync();
        GUI.matrix = lOMatrix;
        UWImguiInput.AfterMatrix();

        if (Event.current.type == EventType.Repaint)
        {
            mOEntryRects.Clear();
            mOEntryRects.AddRange(mOEntryRectsDrawing);
        }
    }

    private bool fButton(string psText)
    {
        return fButton(new GUIContent(psText));
    }

    /// <summary>One preset under "Edit layout": indented and lower than the other buttons, the one
    /// in force in gold. A press chooses it; true for the press (Customize then opens the editor
    /// even when it is already in force).</summary>
    private bool fPresetButton(string psText, UWModernLayout.PresetEnum pePreset, UWModernLayout.PresetEnum peCurrent, bool pbBuilt)
    {
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.Space(28f);

        bool lbEnabled = GUI.enabled;

        GUI.enabled = lbEnabled && pbBuilt;

        bool lbPressed = mOKeyNav.LayoutButton(psText, pePreset == peCurrent ? mOCurrentPresetStyle : mOButtonStyle, GUILayout.Height(30f));

        fRecordEntry();
        GUI.enabled = lbEnabled;
        GUILayout.EndHorizontal();

        if (lbPressed && pePreset != peCurrent)
            UWModernLayout.ChoosePreset(pePreset);

        return lbPressed;
    }

    private bool fButton(GUIContent pOContent)
    {
        GUILayout.Space(6f);

        bool lbPressed = mOKeyNav.LayoutButton(pOContent, mOButtonStyle, GUILayout.Height(38f));

        fRecordEntry();

        return lbPressed;
    }

    // ------------------------------------------------- The gamepad in the menu

    /// <summary>The gamepad steps through the buttons (UWPadEntryStepper, per user 2026-10-08:
    /// as in the context menu).</summary>
    private readonly UWPadEntryStepper mOMenuStepper = new UWPadEntryStepper();

    /// <summary>The arrow keys over the menu's buttons (UWImguiKeyNav) and the page they were
    /// counted on.</summary>
    private readonly UWImguiKeyNav mOKeyNav = new UWImguiKeyNav();

    private PageEnum meKeyNavPage = PageEnum.Closed;

    /// <summary>The buttons' screen rectangles of the last drawn frame, and those being drawn.</summary>
    private readonly List<Rect> mOEntryRects = new List<Rect>();

    private readonly List<Rect> mOEntryRectsDrawing = new List<Rect>();

    /// <summary>The menu's area and scale while it draws, for the buttons' screen places.</summary>
    private Vector2 mOAreaAt;

    private float mfMenuScale = 1f;

    private PageEnum meSteppedPage = PageEnum.Closed;

    /// <summary>The button just laid out, as a screen rectangle (pixels, bottom-left origin) - in
    /// the repaint only, where GUILayout knows its place.</summary>
    private void fRecordEntry()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        Rect lORect = GUILayoutUtility.GetLastRect();
        float lfX = (mOAreaAt.x + lORect.x) * mfMenuScale;
        float lfTop = (mOAreaAt.y + lORect.y) * mfMenuScale;

        mOEntryRectsDrawing.Add(new Rect(lfX, Screen.height - lfTop - (lORect.height * mfMenuScale),
            lORect.width * mfMenuScale, lORect.height * mfMenuScale));
    }

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

    private void fDrawMain()
    {
        GUILayout.Label("Game menu", mOTitleStyle, GUILayout.Height(40f));

        if (fButton("Resume"))
            CloseMenu();

        // Refused in the Void (the original's check_save, UWHudOptions.RefusesToSave); restoring
        // is always allowed there (check_rest).
        if (fButton("Save game") && (mOUi == null || mOUi.Options == null || !mOUi.Options.RefusesToSave()))
            mePage = PageEnum.Save;

        if (fButton("Restore game"))
            mePage = PageEnum.Load;

        // Music, sound and the detail level left this menu on 2026-10-06 (per user): the setup
        // bar at the top offers them in full while the menu is open (UWSetupMenu).
        // THE MOUSE'S RIGHT BUTTON AS A GLYPH (per user, 2026-10-08: not the pad's - "Right button"
        // said nothing of which; Kenney's pixel prompts, UWGlyphs), "RMB" if it is missing.
        Texture2D lOMouseRight = UWGlyphs.MouseRight;
        string lsPointerOption = UWUserSettings.ModernPointerHold ? "hold to look around" : "toggles the pointer";

        if (fButton(lOMouseRight != null ? new GUIContent("  " + lsPointerOption, lOMouseRight) : new GUIContent("RMB: " + lsPointerOption)))
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
        GUILayout.Space(10f);
        GUILayout.Label("Edit layout", mOSectionStyle, GUILayout.Height(24f));

        UWModernLayout.PresetEnum lePreset = UWModernLayout.Preset;

        fPresetButton("Classic Wide", UWModernLayout.PresetEnum.Classic, lePreset, true);
        fPresetButton("Classic Modular", UWModernLayout.PresetEnum.ClassicPlus, lePreset, true);
        fPresetButton("Modern", UWModernLayout.PresetEnum.Modern, lePreset, true);

        if (fPresetButton("Customize", UWModernLayout.PresetEnum.Custom, lePreset, true))
            mePage = PageEnum.Layout;

        // The minimap's north up / turning left this menu on 2026-10-09 (per user): the layout
        // editor's inspector has it on the minimap ("North").

        // Quit at the very bottom of the panel, set apart from the settings (per user,
        // 2026-10-06: "das Quit nach unten schieben") - since music, sound and detail left the
        // menu it had moved up with the rest.
        GUILayout.FlexibleSpace();

        if (fButton("Quit to the main menu"))
            fQuitToMainMenu();
    }

    private void fDrawSlots(bool pbSave)
    {
        GUILayout.Label(pbSave ? "Save game" : "Restore game", mOTitleStyle, GUILayout.Height(40f));

        for (int liSlot = 1; liSlot <= UWSavegameSlots.SlotCount; liSlot++)
        {
            string lsName = UWSaveFlow.SlotName(liSlot);

            if (!fButton(liSlot + ": " + lsName))
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

        GUILayout.Space(12f);

        if (fButton("Back"))
            mePage = PageEnum.Main;
    }

    private void fDrawSaveName()
    {
        GUILayout.Label("Save game " + miSaveSlot, mOTitleStyle, GUILayout.Height(40f));
        GUILayout.Label("Name of the save game:", mOLabelStyle);

        Event lOEvent = Event.current;
        bool lbEnter = lOEvent.type == EventType.KeyDown
            && (lOEvent.keyCode == KeyCode.Return || lOEvent.keyCode == KeyCode.KeypadEnter);
        bool lbEscape = lOEvent.type == EventType.KeyDown && lOEvent.keyCode == KeyCode.Escape;

        GUI.SetNextControlName("UWModernSaveName");
        msSaveName = GUILayout.TextField(msSaveName ?? string.Empty, 30, mOFieldStyle, GUILayout.Height(36f));

        if (mbFocusName)
        {
            GUI.FocusControl("UWModernSaveName");
            mbFocusName = false;
        }

        if (lbEnter)
        {
            lOEvent.Use();
            fSave();
            return;
        }

        if (lbEscape)
        {
            lOEvent.Use();
            mePage = PageEnum.Save;
            return;
        }

        if (fButton("Save"))
            fSave();

        if (fButton("Cancel"))
            mePage = PageEnum.Save;
    }

    private void fDrawDetail()
    {
        GUILayout.Label("Detail", mOTitleStyle, GUILayout.Height(40f));

        foreach (UWGraphicsDetail.LevelEnum leLevel in new[]
        {
            UWGraphicsDetail.LevelEnum.Low, UWGraphicsDetail.LevelEnum.Medium,
            UWGraphicsDetail.LevelEnum.High, UWGraphicsDetail.LevelEnum.VeryHigh
        })
        {
            string lsMark = leLevel == UWGraphicsDetail.CurrentLevel ? "> " : string.Empty;

            if (fButton(lsMark + leLevel))
                UWGraphicsDetail.SetLevel(leLevel);
        }

        GUILayout.Space(12f);

        if (fButton("Back"))
            mePage = PageEnum.Main;
    }
}
