using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE HELP WINDOW (feature wish of the user, 2026-09-24; layout decided 2026-09-25): Tab (F1 until 2026-09-26) opens
/// it beside the picture - the classic frame goes to 4:3 and slides left (UWHelpLayout), the help
/// takes the freed width. Seven tabs: the character's STATS, a live cut-out of the MAP (both added
/// 2026-09-26), free NOTES of the player (kept per save game, UWHelpNotes), the known SPELLS, the
/// known MANTRAS, the game's MANUAL (the GOG PDF, rendered by UWManual, with a reading mode
/// over the whole picture) and the CONTROLS of the scheme in force (UWHelpControls, 2026-10-04).
///
/// THE LOOK is modern and readable (per user), the same as the setup bar: IMGUI with the built-in
/// font, because the scene has no EventSystem on purpose (see UWItemDrag) and IMGUI brings the
/// text area and the scroll list along.
///
/// Not in a screen menu: those are centred canvases of their own that do not follow the frame, so
/// the help closes when one opens. A CONVERSATION keeps it (per user, 2026-09-26: it closed when a
/// conversation started): the conversation's frame slides with the game frame
/// (UWConversationScreen.fFollowHelpLayout) and leaves the keys to the help while it has them.
/// </summary>
public class UWHelpWindow : MonoBehaviour
{
    /// <summary>The help's own reference height, as the setup bar's.</summary>
    private const float ReferenceHeight = 720f;

    private const float CanvasHeight = 200f;

    private const string NotesControl = "UWHelpNotes";

    /// <summary>Room below each spell or mantra entry.</summary>
    private const float EntrySpacing = 8f;

    private static readonly Color BackgroundColour = new Color(0.08f, 0.08f, 0.09f, 0.96f);

    private static readonly Color TabColour = new Color(0.14f, 0.14f, 0.16f, 1f);

    private static readonly Color HoverColour = new Color(0.22f, 0.22f, 0.26f, 1f);

    private static readonly Color FieldColour = new Color(0.05f, 0.05f, 0.06f, 1f);

    private static readonly Color TextColour = new Color(0.86f, 0.86f, 0.84f, 1f);

    private static readonly Color DimTextColour = new Color(0.55f, 0.55f, 0.55f, 1f);

    private static readonly Color AccentColour = new Color(0.80f, 0.64f, 0.34f, 1f);

    /// <summary>AccentColour for rich text.</summary>
    private const string AccentHex = "CCA357";

    private enum TabEnum
    {
        Stats,
        Map,
        Notes,
        Spells,
        Mantras,
        Manual,
        Controls
    }

    private static readonly string[] msTabNames = { "Stats", "Map", "Notes", "Spells", "Mantras", "Manual", "Controls" };

    /// <summary>Seven tabs do not fit in one row beside the 4:3 frame - two rows of four (of three
    /// until the Controls tab came, 2026-10-04).</summary>
    private const int TabsPerRow = 4;

    /// <summary>Whether the player types in the notes - then the game's keys stay silent (see
    /// UWGameUI.IsTextEntryActive).</summary>
    public static bool IsTyping { get; private set; }

    private TabEnum meTab = TabEnum.Spells;

    /// <summary>
    /// THE HELP KEEPS ITS STATE (per user, 2026-10-01): open or closed, the tab and the map
    /// tab's zoom are stored in the settings (UWUserSettings) and come back with the next game
    /// and the next start. Only the player's own Tab counts as the wish - a screen menu that
    /// pushes the help aside does not change it, and once the menu is gone (and no intro or
    /// cutscene plays) the help comes back.
    /// </summary>
    private static bool msbReopenPending;

    /// <summary>Called by the level loader instead of closing the help: open at once when the
    /// player left it open and nothing covers the picture, otherwise closed and reopened later.
    /// </summary>
    public static void RestoreAfterLoad()
    {
        if (UWUserSettings.HelpOpen && !fIsCovered())
        {
            UWHelpLayout.OpenAtOnce();
            msbReopenPending = false;
        }
        else
        {
            UWHelpLayout.CloseAtOnce();
            msbReopenPending = UWUserSettings.HelpOpen;
        }
    }

    /// <summary>A screen menu, the intro or a cutscene takes the picture.</summary>
    private static bool fIsCovered()
    {
        UWIntroPlayer lOIntro = UWScene.IntroPlayer;

        return UWScreenUi.IsScreenMenuOpen || (lOIntro != null && lOIntro.IsPlaying);
    }

    private void Awake()
    {
        int liTab = UWUserSettings.HelpTab;

        if (liTab >= 0 && liTab < msTabNames.Length)
            meTab = (TabEnum)liTab;

        miMapSpan = Mathf.Clamp(UWUserSettings.HelpMapSpan, MinMapSpan, MaxMapSpan);
    }

    private Vector2 mOScroll;

    /// <summary>The panels with what the manual does not name - closed until asked for.</summary>
    private bool mbShowMoreSpells;

    private bool mbShowMoreMantras;

    private readonly Dictionary<Color, Texture2D> mOTextures = new Dictionary<Color, Texture2D>();

    private Font mOFont;

    private GUIStyle mOText;

    private GUIStyle mODimText;

    private GUIStyle mOHeading;

    private GUIStyle mOSubHeading;

    private GUIStyle mOFoldout;

    private GUIStyle mOTab;

    private GUIStyle mOCurrentTab;

    private GUIStyle mONotes;

    private GUIStyle mOCentred;

    /// <summary>A stat row's label and value (fStatRow) - one line each, no wrapping.</summary>
    private GUIStyle mOLabel;

    private GUIStyle mOValue;

    /// <summary>A rune picture in the spell list: no padding, a little room between them.</summary>
    private GUIStyle mOIcon;

    private GUIStyle mOArrow;

    // ------------------------------------------------- The manual

    /// <summary>The name of the modal hold while reading (UWControlScheme.HoldUiModal).</summary>
    private const string ReadingHold = "help reading";

    private static readonly Color ReadingShadeColour = new Color(0.03f, 0.03f, 0.04f, 0.94f);

    /// <summary>The page shown, from zero - kept while the game runs.</summary>
    private static int msPage;

    /// <summary>
    /// THE READING MODE (per user, 2026-09-26): a click on the page lays the manual over the
    /// whole picture at screen height, two pages side by side like the open booklet where the
    /// width allows. Beside the frame the page is only good for leafing through below about
    /// 1080 lines (probe render the same day). While it is open the game hears neither keys
    /// nor clicks.
    /// </summary>
    public static bool IsReading { get; private set; }

    /// <summary>The help has the keyboard - typing in the notes or reading the manual (see
    /// UWGameUI.IsTextEntryActive).</summary>
    public static bool BlocksGameKeys => IsTyping || IsReading;

    /// <summary>Where the help lies on the screen (pixels, top left), for keeping the game's
    /// clicks off it.</summary>
    private Rect mOPanelOnScreen;

    /// <summary>The side view's page, drawn after the layout at whole pixels.</summary>
    private Texture2D mOPendingPage;

    private Rect mOPendingPageRect;

    /// <summary>Scale and screen origin of the layout area, in the scaled GUI units.</summary>
    private float mfScale = 1f;

    private Vector2 mOAreaOrigin;

    private void Update()
    {
        UWHelpLayout.Tick(Time.unscaledDeltaTime);

        // A screen menu takes the whole picture - the help steps aside. A conversation does not.
        // A CUTSCENE TOO (per user, 2026-10-02: the help still covered a cutscene): it steps
        // aside without changing the wish and comes back afterwards.
        if (UWHelpLayout.IsOpen && fIsCovered())
        {
            fSetOpen(false, false);
            msbReopenPending = UWUserSettings.HelpOpen;
        }
        else if (msbReopenPending && !fIsCovered())
        {
            msbReopenPending = false;

            if (UWUserSettings.HelpOpen && !UWHelpLayout.IsOpen)
                fSetOpen(true, false);
        }

        // The game's buttons stay off the help: while reading everywhere, otherwise over the
        // panel (it lies over the picture on 4:3 screens and in the modern scheme).
        fUpdateSwallow();

        UWMouseButtons.IsBlockedByHelp = IsReading || msbSwallowUntilReleased
            || (UWHelpLayout.Blend > 0f && fIsMouseOverPanel());

        if (BlocksGameKeys)
            return;

        UWControlScheme lOScheme = UWScene.ControlScheme;
        UWControls lOControls = lOScheme != null ? lOScheme.Controls : null;

        // TAB, NOT WITH ALT (per user, 2026-09-26): Alt+Tab switches to another program and must
        // not open or close anything on the way. Nor with Shift - Shift+Tab is left alone.
        // In the modern scheme the character panel takes Tab and opens on its Help tab
        // (UWModernPanel, per user 2026-10-03); the help is drawn inside it.
        bool lbModern = lOScheme != null && lOScheme.Current != UWControlScheme.SchemeEnum.Original
            && UWModernPanel.Instance != null;

        // Not under a setup-menu panel either - the Game-folder dialog took Tab as help
        // (Linux first run, 2026-10-06).
        if (lOControls != null && !lbModern && !UWControls.IsShiftHeld && !UWControls.IsAltHeld && !UWControls.IsTextEntryActive
            && !UWSetupMenu.HasOpenPanel
            && lOControls.Player.ToggleHelp.WasPerformedThisFrame()
            && !fIsCovered())
            fSetOpen(!UWHelpLayout.IsOpen, true);
    }

    /// <summary>Turns to the map tab - for a click on the modern scheme's minimap
    /// (UWModernMinimap), which opens the panel's Help tab on it.</summary>
    internal void ShowMapTab()
    {
        meTab = TabEnum.Map;
        mOScroll = Vector2.zero;

        if (UWUserSettings.HelpTab != (int)TabEnum.Map)
        {
            UWUserSettings.HelpTab = (int)TabEnum.Map;
            UWUserSettings.Save();
        }
    }

    /// <summary>The modern character panel opens or closes the help with its Help tab - the
    /// player's own choice, stored as the wish like Tab.</summary>
    internal void SetOpenFromPanel(bool pbOpen)
    {
        if (UWHelpLayout.IsOpen != pbOpen || UWUserSettings.HelpOpen != pbOpen)
            fSetOpen(pbOpen, true);
    }

    /// <summary>pbByPlayer: the player's own Tab - stored as the wish.</summary>
    private void fSetOpen(bool pbOpen, bool pbByPlayer)
    {
        UWHelpLayout.SetOpen(pbOpen);

        if (pbByPlayer && UWUserSettings.HelpOpen != pbOpen)
        {
            UWUserSettings.HelpOpen = pbOpen;
            msbReopenPending = false;
            UWUserSettings.Save();
        }

        if (!pbOpen)
        {
            IsTyping = false;
            fSetReading(false);
        }
    }

    /// <summary>The reading mode was closed by a click whose buttons are still down: the game
    /// stays shut out (mouse and modal hold) until every button is up, so the release does
    /// not use, drop or walk anything (per user, 2026-09-26).</summary>
    private static bool msbSwallowUntilReleased;

    private static void fSetReading(bool pbReading, bool pbByClick = false)
    {
        if (IsReading == pbReading)
            return;

        IsReading = pbReading;

        if (!pbReading && pbByClick)
        {
            msbSwallowUntilReleased = true;
            return;
        }

        fSetReadingHold(pbReading);
    }

    private static void fSetReadingHold(bool pbHold)
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;

        if (lOScheme != null)
            lOScheme.SetUiModal(ReadingHold, pbHold);
    }

    /// <summary>Ends the swallowing once no mouse button is held any more.</summary>
    private static void fUpdateSwallow()
    {
        if (!msbSwallowUntilReleased)
            return;

        Mouse lOMouse = Mouse.current;

        if (lOMouse != null && (lOMouse.leftButton.isPressed || lOMouse.rightButton.isPressed
            || lOMouse.middleButton.isPressed))
            return;

        msbSwallowUntilReleased = false;

        if (!IsReading)
            fSetReadingHold(false);
    }

    private bool fIsMouseOverPanel()
    {
        Mouse lOMouse = Mouse.current;

        if (lOMouse == null)
            return false;

        Vector2 lOAt = lOMouse.position.ReadValue();

        return mOPanelOnScreen.Contains(new Vector2(lOAt.x, Screen.height - lOAt.y));
    }

    private void OnDisable()
    {
        IsTyping = false;
        fSetReading(false);

        if (msbSwallowUntilReleased)
        {
            msbSwallowUntilReleased = false;
            fSetReadingHold(false);
        }

        UWMouseButtons.IsBlockedByHelp = false;
    }

    // ------------------------------------------------- Drawing

    private void OnGUI()
    {
        if (UWHelpLayout.Blend <= 0f || Screen.height <= 0)
        {
            IsTyping = false;
            mOPanelOnScreen = Rect.zero;
            fSetReading(false);
            return;
        }

        fEnsureStyles();

        float lfUnit = Screen.height / CanvasHeight;
        float lfCanvasWidth = Screen.width / lfUnit;

        UWHelpLayout.OpenLayout(lfCanvasWidth, out float _, out float lfLeft, out float lfWidth);

        // In the modern scheme there is no classic frame to slide: the help lies over the picture.
        UWControlScheme lOScheme = UWScene.ControlScheme;
        bool lbBesideFrame = UWHelpLayout.FitsBeside(lfCanvasWidth);

        // THE MODERN CHARACTER PANEL holds the help on its Help tab (UWModernPanel): drawn into
        // the panel's inner area, and not at all while the panel is closed or still sliding.
        float lfScale = Screen.height / ReferenceHeight;
        float lfAreaTop = 0f;
        float lfAreaHeight = ReferenceHeight;

        if (lOScheme != null && lOScheme.Current != UWControlScheme.SchemeEnum.Original)
        {
            lfWidth = Mathf.Min(UWHelpLayout.MaxWidthPerHeight * CanvasHeight, lfCanvasWidth * UWHelpLayout.OverlayShare);
            lfLeft = lfCanvasWidth - lfWidth;
            lbBesideFrame = false;

            if (UWModernPanel.Instance != null)
            {
                if (!UWModernPanel.Instance.TryGetHelpArea(out Rect lOInPanel))
                {
                    IsTyping = false;
                    mOPanelOnScreen = Rect.zero;
                    fSetReading(false);
                    return;
                }

                lfLeft = lOInPanel.x / lfUnit;
                lfWidth = lOInPanel.width / lfUnit;
                lfAreaTop = (Screen.height - lOInPanel.yMax) / lfScale;
                lfAreaHeight = lOInPanel.height / lfScale;
            }
        }

        // Beside the sliding frame the help lies BEHIND it: cut off at the frame's right edge,
        // so the frame uncovers it while sliding left (per user, 2026-09-25). IMGUI is drawn over
        // every canvas, hence the clip instead of a draw order.
        float lfClipLeft = lfLeft;

        if (lbBesideFrame)
            lfClipLeft = Mathf.Clamp(UWHelpLayout.FrameRightEdge(lfCanvasWidth, UWGameUI.HorizontalPixelFactor),
                lfLeft, lfLeft + lfWidth);

        GUI.matrix = Matrix4x4.Scale(new Vector3(lfScale, lfScale, 1f));

        Color lOWas = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, UWHelpLayout.Blend);

        float lfClipX = lfClipLeft * lfUnit / lfScale;
        float lfAreaX = lfLeft * lfUnit / lfScale;
        float lfAreaWidth = lfWidth * lfUnit / lfScale;

        mfScale = lfScale;
        mOAreaOrigin = new Vector2(lfAreaX + 12f, lfAreaTop + 12f);
        mOPanelOnScreen = new Rect(lfClipX * lfScale, lfAreaTop * lfScale, (lfAreaX + lfAreaWidth - lfClipX) * lfScale,
            lfAreaHeight * lfScale);
        mOPendingPage = null;
        mbPendingMap = false;

        // While reading, the overlay has the mouse - the panel below must not react to it.
        bool lbWasEnabled = GUI.enabled;

        if (IsReading)
            GUI.enabled = false;

        GUI.BeginGroup(new Rect(lfClipX, lfAreaTop, lfAreaX + lfAreaWidth - lfClipX, lfAreaHeight));

        Rect lOArea = new Rect(lfAreaX - lfClipX, 0f, lfAreaWidth, lfAreaHeight);

        GUI.DrawTexture(lOArea, fTexture(BackgroundColour));

        GUILayout.BeginArea(new Rect(lOArea.x + 12f, lOArea.y + 12f, lOArea.width - 24f, lOArea.height - 24f));

        fDrawTabs();

        GUILayout.Space(10f);

        if (meTab == TabEnum.Notes)
            fDrawNotes();
        else if (meTab == TabEnum.Manual)
        {
            fDrawManual();
            IsTyping = false;
        }
        else if (meTab == TabEnum.Map)
        {
            fDrawMap();
            IsTyping = false;
        }
        else
        {
            mOScroll = GUILayout.BeginScrollView(mOScroll);

            switch (meTab)
            {
                case TabEnum.Stats: fDrawStats(); break;
                case TabEnum.Spells: fDrawSpells(); break;
                case TabEnum.Mantras: fDrawMantras(); break;
                case TabEnum.Controls: fDrawControls(); break;
            }

            GUILayout.EndScrollView();
            IsTyping = false;
        }

        GUILayout.EndArea();

        GUI.EndGroup();

        GUI.enabled = lbWasEnabled;

        // The page at whole screen pixels, so its texels meet the pixels one to one - and
        // clipped like the panel, so it too lies behind the sliding frame.
        if (mOPendingPage != null && Event.current.type == EventType.Repaint)
        {
            GUI.matrix = Matrix4x4.identity;

            float lfClipPixels = lfClipX * lfScale;

            GUI.BeginGroup(new Rect(lfClipPixels, 0f, mOPanelOnScreen.xMax - lfClipPixels, Screen.height));
            GUI.DrawTexture(new Rect(mOPendingPageRect.x - lfClipPixels, mOPendingPageRect.y,
                mOPendingPageRect.width, mOPendingPageRect.height), mOPendingPage);
            GUI.EndGroup();
        }

        // The map as well, at whole screen pixels per map pixel.
        if (mbPendingMap && Event.current.type == EventType.Repaint)
            fDrawPendingMap(lfClipX * lfScale);

        if (IsReading)
            fDrawReading();

        GUI.color = lOWas;
    }

    private void fDrawTabs()
    {
        for (int liRowStart = 0; liRowStart < msTabNames.Length; liRowStart += TabsPerRow)
        {
            GUILayout.BeginHorizontal();

            for (int liTab = liRowStart; liTab < Mathf.Min(liRowStart + TabsPerRow, msTabNames.Length); liTab++)
            {
                if (GUILayout.Button(msTabNames[liTab], (int)meTab == liTab ? mOCurrentTab : mOTab,
                    GUILayout.Height(28f), GUILayout.Width(1f), GUILayout.ExpandWidth(true)))
                {
                    meTab = (TabEnum)liTab;
                    mOScroll = Vector2.zero;
                    GUI.FocusControl(null);

                    if (UWUserSettings.HelpTab != liTab)
                    {
                        UWUserSettings.HelpTab = liTab;
                        UWUserSettings.Save();
                    }
                }
            }

            GUILayout.EndHorizontal();
        }
    }

    private void fDrawNotes()
    {
        GUILayout.Label("Your own notes, kept with the save game.", mODimText);
        GUILayout.Space(6f);

        // Tab closes the help while typing too - the game's keys are silent then. Taken BEFORE
        // the text area, which would otherwise type a tab into the notes; with Alt it is the
        // switch to another program and is left alone.
        Event lOEvent = Event.current;
        bool lbTyping = GUI.GetNameOfFocusedControl() == NotesControl;

        if (lbTyping && lOEvent.type == EventType.KeyDown && !lOEvent.alt && !lOEvent.shift
            && (lOEvent.keyCode == KeyCode.Tab || lOEvent.character == '\t'))
        {
            lOEvent.Use();

            if (lOEvent.keyCode == KeyCode.Tab)
            {
                GUI.FocusControl(null);
                fSetOpen(false, true);
            }
        }

        mOScroll = GUILayout.BeginScrollView(mOScroll);

        GUI.SetNextControlName(NotesControl);
        UWHelpNotes.Notes = GUILayout.TextArea(UWHelpNotes.Notes ?? string.Empty, mONotes, GUILayout.ExpandHeight(true));

        GUILayout.EndScrollView();

        IsTyping = GUI.GetNameOfFocusedControl() == NotesControl;
    }

    private void fDrawSpells()
    {
        DataImport lOData = fData();

        GUILayout.Label("Spells", mOHeading);
        GUILayout.Label("Put the runes on the shelf from the rune bag, then click the shelf to cast. "
            + "A spell costs three mana per circle, and your level halved (rounded up) must reach its circle. "
            + "Click a spell here to lay its runes into the hollow; grey runes are not in your bag yet.",
            mODimText);

        fDrawSpellList(lOData, true);

        GUILayout.Space(16f);

        if (GUILayout.Button(mbShowMoreSpells ? "Hide the spells the manual does not name"
                : "Show the spells the manual does not name", mOFoldout))
            mbShowMoreSpells = !mbShowMoreSpells;

        if (mbShowMoreSpells)
            fDrawSpellList(lOData, false);
    }

    /// <summary>
    /// The rune spells circle by circle - the ones the manual names, or the others.
    ///
    /// RUNE ICONS AND A CLICK TO EQUIP (feature wish of the user, 2026-09-27): each spell shows
    /// its runes as the game's rune pictures, grey where the rune is not in the bag yet (per
    /// user). A left click on the row lays the runes into the casting hollow, replacing what
    /// lay there - only when all of them are in the bag, as the shelf itself only offers what
    /// the bag holds. The spell lying in the hollow right now is marked.
    /// </summary>
    private void fDrawSpellList(DataImport pOData, bool pbManual)
    {
        int liCircle = 0;
        UWPlayerData lOPlayer = pOData != null ? pOData.InitialPlayer : null;
        UWGameUI lOUi = UWScene.GameUi;
        IReadOnlyList<int> lOShelf = lOUi != null ? lOUi.Runes.SelectedRunes : null;

        foreach (UWRunicMagic.Spell lOSpell in UWRunicMagic.AllSpells)
        {
            if (lOSpell.RuneSequence == UWRunicMagic.EmptySequence)
                continue;

            bool lbNamed = UWHelpContent.ManualSpellNotes.TryGetValue(lOSpell.Index, out string lsNote);

            if (lbNamed != pbManual)
                continue;

            if (lOSpell.Level != liCircle)
            {
                liCircle = lOSpell.Level;

                GUILayout.Space(10f);
                GUILayout.Label(string.Format("Circle {0}  -  {1} mana", liCircle, lOSpell.ManaCost), mOSubHeading);
            }

            string lsName = pOData != null ? UWRunicMagic.GetName(lOSpell, pOData.Strings) : string.Empty;
            List<int> lORunes = fRunesOf(lOSpell.RuneSequence);
            bool lbAllInBag = lOPlayer != null;

            foreach (int liRune in lORunes)
                lbAllInBag &= lOPlayer != null && lOPlayer.HasRune((UWPlayerData.Rune)liRune);

            bool lbOnShelf = fIsOnShelf(lOShelf, lORunes);

            GUILayout.BeginHorizontal();

            foreach (int liRune in lORunes)
            {
                bool lbInBag = lOPlayer != null && lOPlayer.HasRune((UWPlayerData.Rune)liRune);
                Texture2D lOIcon = fRuneIcon(pOData, liRune, !lbInBag);

                // Drawn into a fixed rect: a label would not scale the 16 pixel picture up.
                Rect lOIconRect = GUILayoutUtility.GetRect(RuneIconSize, RuneIconSize, mOIcon,
                    GUILayout.Width(RuneIconSize), GUILayout.Height(RuneIconSize));

                if (lOIcon != null && Event.current.type == EventType.Repaint)
                    GUI.DrawTexture(lOIconRect, lOIcon, ScaleMode.ScaleToFit);
            }

            GUILayout.Space(8f);
            GUILayout.Label(string.Format("<b>{0}</b>   <color=#{1}>{2}</color>{3}",
                string.IsNullOrEmpty(lsName) ? "?" : lsName, AccentHex,
                UWRunicMagic.DescribeRunes(lOSpell.RuneSequence),
                lbOnShelf ? "   <i>(in the hollow)</i>" : string.Empty), mOText);

            GUILayout.EndHorizontal();

            Rect lORow = GUILayoutUtility.GetLastRect();
            Event lOEvent = Event.current;

            if (lbAllInBag && lOUi != null && GUI.enabled && lOEvent.type == EventType.MouseDown && lOEvent.button == 0
                && lORow.Contains(lOEvent.mousePosition))
            {
                lOUi.Runes.PutOnShelf(lORunes);
                lOEvent.Use();
            }

            if (lbNamed)
                GUILayout.Label(lsNote, mODimText);

            // Room between the spells, so each stands apart (per user, 2026-09-26).
            GUILayout.Space(EntrySpacing);
        }
    }

    /// <summary>The runes of a sequence, first to last, empty slots left out.</summary>
    private static List<int> fRunesOf(int piSequence)
    {
        List<int> lORunes = new List<int>(3);

        for (int liShift = 10; liShift >= 0; liShift -= 5)
        {
            int liRune = (piSequence >> liShift) & 0x1F;

            if (liRune < UWRunicMagic.EmptyRune)
                lORunes.Add(liRune);
        }

        return lORunes;
    }

    private static bool fIsOnShelf(IReadOnlyList<int> pOShelf, List<int> pORunes)
    {
        if (pOShelf == null || pOShelf.Count != pORunes.Count)
            return false;

        for (int liAt = 0; liAt < pORunes.Count; liAt++)
        {
            if (pOShelf[liAt] != pORunes[liAt])
                return false;
        }

        return true;
    }

    /// <summary>How large a rune picture stands in the list (help units, 720 lines).</summary>
    private const float RuneIconSize = 22f;

    private readonly Texture2D[] mORuneIcons = new Texture2D[UWRunicMagic.EmptyRune];

    private readonly Texture2D[] mOGreyRuneIcons = new Texture2D[UWRunicMagic.EmptyRune];

    /// <summary>A rune's picture (the rune stone object, 0xE8 + rune) in real colours - the
    /// panel's icon textures carry palette indices for a shader IMGUI does not have. Grey:
    /// desaturated and dimmed, for a rune not in the bag.</summary>
    private Texture2D fRuneIcon(DataImport pOData, int piRune, bool pbGrey)
    {
        if (pOData == null || piRune < 0 || piRune >= mORuneIcons.Length)
            return null;

        Texture2D[] lOCache = pbGrey ? mOGreyRuneIcons : mORuneIcons;

        if (lOCache[piRune] != null)
            return lOCache[piRune];

        UWTexture lOSource = pOData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS,
            UWPlayerData.FirstRuneObjectId + piRune);

        if (lOSource == null)
            return null;

        Color32[] lOPixels = UWGameUI.fGetTextureInvert(lOSource);

        if (pbGrey)
        {
            for (int liAt = 0; liAt < lOPixels.Length; liAt++)
            {
                Color32 lOPixel = lOPixels[liAt];
                byte lyGrey = (byte)((((lOPixel.r * 30) + (lOPixel.g * 59) + (lOPixel.b * 11)) / 100) * 55 / 100);

                lOPixels[liAt] = new Color32(lyGrey, lyGrey, lyGrey, lOPixel.a);
            }
        }

        Texture2D lOTexture = new Texture2D(lOSource.Width, lOSource.Height, TextureFormat.RGBA32, false);
        lOTexture.name = "UWHelpWindow rune " + piRune + (pbGrey ? " grey" : string.Empty);
        lOTexture.filterMode = FilterMode.Point;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(lOPixels);
        lOTexture.Apply(false, false);

        lOCache[piRune] = lOTexture;

        return lOTexture;
    }

    private void fDrawMantras()
    {
        DataImport lOData = fData();

        GUILayout.Label("Mantras", mOHeading);
        GUILayout.Label("Talk to an ankh shrine and type a mantra. It raises your skills only when you "
            + "have earned a skill point.", mODimText);

        GUILayout.Space(10f);

        foreach (KeyValuePair<int, string> lOMantra in UWHelpContent.ManualMantras)
        {
            GUILayout.Label("<b>" + UWShrineRules.GetMantra(lOMantra.Key, lOData) + "</b>", mOText);
            GUILayout.Label(lOMantra.Value + " (" + fGroupSkills(lOMantra.Key, lOData) + ")", mODimText);
            GUILayout.Space(EntrySpacing);
        }

        GUILayout.Space(16f);

        if (GUILayout.Button(mbShowMoreMantras ? "Hide the mantras the manual does not name"
                : "Show the mantras the manual does not name", mOFoldout))
            mbShowMoreMantras = !mbShowMoreMantras;

        if (!mbShowMoreMantras)
            return;

        GUILayout.Space(10f);
        GUILayout.Label("One skill each, raised greatly", mOSubHeading);

        for (int liMantra = 0; liMantra < UWShrineRules.FirstSpecialMantra; liMantra++)
            GUILayout.Label(string.Format("<b>{0}</b>   <color=#{1}>{2}</color>",
                UWShrineRules.GetMantra(liMantra, lOData), AccentHex, UWShrineRules.GetSkillName(liMantra, lOData)),
                mOText);

        GUILayout.Space(10f);
        GUILayout.Label("Quests", mOSubHeading);

        GUILayout.Label("<b>" + UWShrineRules.GetMantra(UWShrineRules.CupOfWonderMantra, lOData) + "</b>", mOText);
        GUILayout.Label(UWHelpContent.CupOfWonderNote, mODimText);
        GUILayout.Space(EntrySpacing);
        GUILayout.Label("<b>" + UWShrineRules.GetMantra(UWShrineRules.KeyOfTruthMantra, lOData) + "</b>", mOText);
        GUILayout.Label(UWHelpContent.KeyOfTruthNote, mODimText);
    }

    /// <summary>The controls of the scheme in force (UWHelpControls): per section a heading, a dim
    /// line and the keys in the accent colour beside what they do.</summary>
    private void fDrawControls()
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;
        bool lbModern = lOScheme != null && lOScheme.Current == UWControlScheme.SchemeEnum.Modern;

        GUILayout.Label(lbModern ? "Modern controls" : "Classic controls", mOHeading);

        foreach (UWHelpControls.Section lOSection in UWHelpControls.Build(lbModern))
        {
            GUILayout.Space(10f);
            GUILayout.Label(lOSection.Title, mOSubHeading);

            if (!string.IsNullOrEmpty(lOSection.Intro))
                GUILayout.Label(lOSection.Intro, mODimText);

            foreach (UWHelpControls.Row lORow in lOSection.Rows)
            {
                GUILayout.BeginHorizontal();

                // GAMEPAD GLYPHS in the keys' column (UWHelpControls.AddPad, UWGlyphs), glyphs and text
                // centred on each other (per user, 2026-10-08: the text stood at the glyphs' top).
                if (lORow.Glyphs != null)
                {
                    if (mOTextMiddle == null)
                        mOTextMiddle = new GUIStyle(mOText) { alignment = TextAnchor.MiddleLeft };

                    GUILayout.BeginVertical(GUILayout.Width(ControlsKeyWidth));
                    GUILayout.FlexibleSpace();
                    GUILayout.BeginHorizontal();

                    foreach (Texture2D lOGlyph in lORow.Glyphs)
                        GUILayout.Label(lOGlyph, GUIStyle.none, GUILayout.Width(ControlsGlyphSize), GUILayout.Height(ControlsGlyphSize));

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndVertical();

                    GUILayout.Label(lORow.Text, mOTextMiddle, GUILayout.MinHeight(ControlsGlyphSize));
                }
                else
                {
                    GUILayout.Label("<color=#" + AccentHex + "><b>" + lORow.Keys + "</b></color>", mOText, GUILayout.Width(ControlsKeyWidth));
                    GUILayout.Label(lORow.Text, mOText);
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(3f);
            }
        }
    }

    /// <summary>The keys' column of the Controls tab.</summary>
    private const float ControlsKeyWidth = 130f;

    /// <summary>A gamepad glyph there: Kenney's 16 pixels twice over (UWGlyphs).</summary>
    private const float ControlsGlyphSize = 32f;

    /// <summary>The text beside gamepad glyphs, centred on them.</summary>
    private GUIStyle mOTextMiddle;

    /// <summary>"Attack, Defense, ..." - the skills a group mantra draws from.</summary>
    private static string fGroupSkills(int piMantra, DataImport pOData)
    {
        if (!UWShrineRules.TryGetGroup(piMantra, out int liFirst, out int liCount, out int _))
            return string.Empty;

        List<string> lONames = new List<string>();

        for (int liSkill = liFirst; liSkill < liFirst + liCount; liSkill++)
            lONames.Add(UWShrineRules.GetSkillName(liSkill, pOData).Trim());

        return string.Join(", ", lONames);
    }

    // ------------------------------------------------- Stats

    /// <summary>The panel with the values the original never shows as numbers - closed until
    /// asked for (per user, 2026-09-26).</summary>
    private bool mbShowHiddenStats;

    /// <summary>
    /// THE STATS TAB (feature wish of the user, 2026-09-26): what the character panel shows, in
    /// readable type and all twenty skills at once, live while the help is open. Below it,
    /// folded away, the values the game keeps but only hints at: food, fatigue, poison, the
    /// unspent skill points, the exact experience and the next level's threshold, the load.
    /// </summary>
    private void fDrawStats()
    {
        UWCharacter lOCharacter = UWScene.Character;
        UWGameUI lOUi = UWScene.GameUi;
        DataImport lOData = fData();

        if (lOCharacter == null || lOData == null)
        {
            GUILayout.Label("Stats", mOHeading);
            GUILayout.Label("No character is loaded.", mODimText);

            return;
        }

        UWPlayerData lOPlayer = lOData.InitialPlayer;

        GUILayout.Label(lOPlayer != null && !string.IsNullOrEmpty(lOPlayer.Name) ? lOPlayer.Name : "Avatar", mOHeading);
        GUILayout.Label(string.Format("Level {0} {1}", lOCharacter.Level, fClassName(lOData, lOPlayer)), mOText);

        string lsCondition = UWStatusReport.GetConditionLine(lOData, lOCharacter.Hunger, lOCharacter.Fatigue);

        if (!string.IsNullOrEmpty(lsCondition))
            GUILayout.Label(lsCondition.Trim(), mODimText);

        GUILayout.Space(10f);
        GUILayout.Label("Attributes", mOSubHeading);

        fStatRow("Strength", lOCharacter.Strength.ToString());
        fStatRow("Dexterity", lOCharacter.Dexterity.ToString());
        fStatRow("Intelligence", lOCharacter.Intelligence.ToString());
        fStatRow("Vitality", Mathf.RoundToInt(lOCharacter.CurrentHP) + " / " + Mathf.RoundToInt(lOCharacter.MaxHP));
        fStatRow("Mana", Mathf.RoundToInt(lOCharacter.CurrentMana) + " / " + Mathf.RoundToInt(lOCharacter.MaxMana));
        fStatRow("Experience", (lOCharacter.Experience / 10).ToString());

        int liCarried = -1;

        if (lOUi != null && lOUi.mOInventory != null)
        {
            liCarried = lOUi.mOInventory.GetCarriedTenthStones(lOData.CommonObjectProperties);

            // What the circle below the paper doll shows: the capacity left, in whole stones.
            int liLeft = Mathf.Max(0, (lOPlayer != null ? lOPlayer.MaxWeight : 0) - liCarried);

            fStatRow("Can still carry", (liLeft / 10) + " stones");
        }

        GUILayout.Space(10f);
        GUILayout.Label("Skills", mOSubHeading);

        // Attack and Defense first, then the eighteen skills - the order of the panel's list
        // (UWHudPanel.fGetSkillEntry).
        for (int liEntry = 0; liEntry < 2 + (int)UWPlayerData.Skill.Swimming + 1; liEntry++)
        {
            string lsValue;

            if (liEntry == 0)
                lsValue = lOCharacter.Attack.ToString();
            else if (liEntry == 1)
                lsValue = lOCharacter.Defence.ToString();
            else
                lsValue = lOCharacter.GetSkill((UWPlayerData.Skill)(liEntry - 2)).ToString();

            fStatRow(fSkillName(lOData, liEntry), lsValue);
        }

        GUILayout.Space(16f);

        if (GUILayout.Button(mbShowHiddenStats ? "Hide the hidden values" : "Show the hidden values", mOFoldout))
            mbShowHiddenStats = !mbShowHiddenStats;

        if (!mbShowHiddenStats)
            return;

        GUILayout.Space(10f);
        GUILayout.Label("The game keeps these, but shows them only as words or not at all.", mODimText);
        GUILayout.Space(4f);

        fStatRow("Food", lOCharacter.Hunger + " / " + UWCharacter.FullHunger);
        GUILayout.Label("255 is full; the status line changes every 30.", mODimText);
        fStatRow("Fatigue", lOCharacter.Fatigue + " / 255");
        GUILayout.Label("0 is rested; the status line changes every 23.", mODimText);
        fStatRow("Poison", lOCharacter.Poison + " / 15");
        fStatRow("Hallucination", lOCharacter.Hallucination + " / " + UWCharacter.MaxHallucination
            + fHallucinationEffectText());
        GUILayout.Label("A mushroom adds one, the potion sets three; one wears off every 21 seconds.",
            mODimText);
        fStatRow("Unspent skill points", lOCharacter.SkillPoints.ToString());
        GUILayout.Label("A mantra at a shrine spends them. One more every 3000 experience and every level.",
            mODimText);

        fStatRow("Exact experience", fTenths(lOCharacter.Experience));

        int liNext = UWExperience.GetNextLevelExperience(lOCharacter.Level);

        if (liNext < 0)
            fStatRow("Next level", "none, this is the highest");
        else
            fStatRow("Next level at", string.Format("{0} ({1} to go)", liNext / 10,
                fTenths(Mathf.Max(0, liNext - lOCharacter.Experience))));

        if (liCarried >= 0 && lOPlayer != null)
            fStatRow("Load", fTenths(liCarried) + " of " + fTenths(lOPlayer.MaxWeight) + " stones");

        fDrawWornMagic(lOData, lOUi != null ? lOUi.mOInventory : null);
    }

    /// <summary>The worn equipment's own effects, without cast spells - see fDrawWornMagic.</summary>
    private readonly UWArmourProtection.Status mOWornStatus = new UWArmourProtection.Status();

    /// <summary>
    /// WHAT THE WORN MAGIC DOES (per user, 2026-10-02: "show the effect of the equipped magic
    /// items in the hidden values"). First every enchanted item on the paper doll with the name
    /// of its enchantment - whether identified or not -, then the sum of them as the status pass
    /// counts it (UWArmourProtection.Compute without the active spells): only the five armour
    /// pieces, the two rings and a shield cast theirs; a weapon's acts only when it strikes.
    /// </summary>
    private void fDrawWornMagic(DataImport pOData, UWInventory pOInventory)
    {
        GUILayout.Space(10f);
        GUILayout.Label("Worn magic", mOSubHeading);

        if (pOInventory == null)
        {
            GUILayout.Label("No inventory.", mODimText);
            return;
        }

        bool lbLeftHanded = pOInventory.IsLeftHanded;
        UWArmorItemMap.BodySlot leWeaponSlot = UWArmourProtection.GetWeaponSlot(lbLeftHanded);
        UWArmorItemMap.BodySlot leShieldSlot = UWArmourProtection.GetShieldSlot(lbLeftHanded);
        bool lbAny = false;

        foreach (UWArmorItemMap.BodySlot leSlot in msWornMagicSlots)
        {
            UWObject lOItem = pOInventory.GetEquipped(leSlot);

            if (lOItem == null)
                continue;

            UWEnchantment.Result lOEnchantment = UWEnchantment.Get(lOItem, null, pOData.Strings);

            if (!lOEnchantment.HasEnchantment)
                continue;

            string lsEffect = lOEnchantment.IsCursed ? "Curse" : lOEnchantment.Name;

            if (leSlot == leWeaponSlot)
                lsEffect += " (when striking)";
            else if (leSlot == leShieldSlot && !UWPlayerCritterRow.IsShield(lOItem.ID))
                lsEffect += " (not in effect here)";

            // The slot alone, not the item's name - it saves the room (per user, 2026-10-02).
            fStatRow(fSlotName(leSlot, leWeaponSlot, leShieldSlot), lsEffect);
            lbAny = true;
        }

        if (!lbAny)
        {
            GUILayout.Label("Nothing worn is enchanted.", mODimText);
            return;
        }

        UWArmourProtection.Compute(pOInventory, pOData, null, mOWornStatus);

        GUILayout.Space(6f);
        GUILayout.Label("Together, without cast spells:", mODimText);

        int[] liProtection = mOWornStatus.Protection;

        if (liProtection[UWArmourProtection.PartBody] + liProtection[UWArmourProtection.PartHands]
            + liProtection[UWArmourProtection.PartLegs] + liProtection[UWArmourProtection.PartHead] > 0)
            fStatRow("Protection", string.Format("head {0}, body {1}, hands {2}, legs {3}",
                liProtection[UWArmourProtection.PartHead], liProtection[UWArmourProtection.PartBody],
                liProtection[UWArmourProtection.PartHands], liProtection[UWArmourProtection.PartLegs]));

        if (mOWornStatus.DamageResistance > 0)
            fStatRow("Resistance", fSpellName(pOData, ResistanceClass, mOWornStatus.DamageResistance));

        if (mOWornStatus.Brightness > 0)
            fStatRow("Light", fSpellName(pOData, LightClass, mOWornStatus.Brightness));

        fBitRow(pOData, "Movement", MotionClass, 1, 5, mOWornStatus.MotionAbilities, piMinor => 1 << (piMinor - 1));
        fBitRow(pOData, "Stealth", BonusClass, 2, 4, mOWornStatus.StealthBonus, piMinor => 1 << (piMinor - 1));
        fBitRow(pOData, "Immunity", BonusClass, 5, 9, mOWornStatus.DamageTypeProof, piMinor => msProofBitOfMinor[piMinor - 5]);
        fBitRow(pOData, "Regeneration", UWWornRegeneration.MajorClass, 0xE, 0xF, mOWornStatus.RegenerationBits,
            piMinor => UWWornRegeneration.GetBits(UWWornRegeneration.MajorClass, piMinor));

        if (mOWornStatus.CurseDice > 0)
            fStatRow("Curse", mOWornStatus.CurseDice + " dice of eight, now and then");

        if (mOWornStatus.HasMazeNavigation)
            fStatRow("Maze navigation", "shows the way in Tybal's maze");

        if (mOWornStatus.HasDragonSkinBoots)
            fStatRow("Dragon skin boots", "no harm from lava");
    }

    /// <summary>The slots that can carry worn magic: the five armour pieces, the rings and the
    /// two front hands.</summary>
    private static readonly UWArmorItemMap.BodySlot[] msWornMagicSlots =
    {
        UWArmorItemMap.BodySlot.Helmet, UWArmorItemMap.BodySlot.Chest, UWArmorItemMap.BodySlot.Gloves,
        UWArmorItemMap.BodySlot.Legs, UWArmorItemMap.BodySlot.Boots, UWArmorItemMap.BodySlot.LeftRing,
        UWArmorItemMap.BodySlot.RightRing, UWArmorItemMap.BodySlot.RightHandSlot, UWArmorItemMap.BodySlot.LeftHandSlot
    };

    // The enchantment classes the summary names (UWArmourProtection.fApplyEnchantment).
    private const int LightClass = 0;

    private const int MotionClass = 1;

    private const int ResistanceClass = 2;

    private const int BonusClass = 3;

    /// <summary>The immunities' bits, minor class 5 to 9 (UWArmourProtection.msProofBits).</summary>
    private static readonly int[] msProofBitOfMinor = { 0x40, 0x08, 0x10, 0x01, 0x02 };

    /// <summary>One row naming every enchantment of a class whose bit is set.</summary>
    private void fBitRow(DataImport pOData, string psLabel, int piMajor, int piFirstMinor, int piLastMinor,
        int piBits, System.Func<int, int> pFBitOf)
    {
        List<string> lONames = new List<string>();

        for (int liMinor = piFirstMinor; liMinor <= piLastMinor; liMinor++)
        {
            if ((piBits & pFBitOf(liMinor)) != 0)
                lONames.Add(fSpellName(pOData, piMajor, liMinor));
        }

        if (lONames.Count > 0)
            fStatRow(psLabel, string.Join(", ", lONames));
    }

    /// <summary>A spell name from string block 6, (major &lt;&lt; 4) | minor plus the project's one.</summary>
    private static string fSpellName(DataImport pOData, int piMajor, int piMinor)
    {
        try
        {
            return pOData.Strings.Blocks[UWEnchantment.SpellStringBlock].Strings[((piMajor << 4) | piMinor) + 1].Trim();
        }
        catch
        {
            return "?";
        }
    }

    private static string fSlotName(UWArmorItemMap.BodySlot peSlot, UWArmorItemMap.BodySlot peWeaponSlot,
        UWArmorItemMap.BodySlot peShieldSlot)
    {
        if (peSlot == peWeaponSlot)
            return "Weapon hand";

        if (peSlot == peShieldSlot)
            return "Shield hand";

        switch (peSlot)
        {
            case UWArmorItemMap.BodySlot.LeftRing: return "Left ring";
            case UWArmorItemMap.BodySlot.RightRing: return "Right ring";
            default: return peSlot.ToString();
        }
    }

    /// <summary>Which of the three pictures the hallucination shows (UWHallucinationState), or
    /// nothing while none runs.</summary>
    private static string fHallucinationEffectText()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWPaletteRenderToggle lOToggle = lOLoader != null ? lOLoader.GetComponent<UWPaletteRenderToggle>() : null;

        if (lOToggle == null)
            return string.Empty;

        switch (lOToggle.HallucinationEffect)
        {
            case UWDataImport.UWData.UWHallucinationState.ScrambleEffect:
                return ", scrambled textures";

            case UWDataImport.UWData.UWHallucinationState.PaletteEffect:
                return ", palette " + lOToggle.HallucinationPalette;

            case UWDataImport.UWData.UWHallucinationState.LightTableEffect:
                return ", dark light table";

            default:
                return string.Empty;
        }
    }

    /// <summary>A label on the left, its value on the right.</summary>
    private void fStatRow(string psLabel, string psValue)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(psLabel, mOLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label("<color=#" + AccentHex + ">" + psValue + "</color>", mOValue);
        GUILayout.EndHorizontal();
    }

    /// <summary>A value kept in tenths, with its decimal place.</summary>
    private static string fTenths(int piTenths)
    {
        return (piTenths / 10) + "." + Mathf.Abs(piTenths % 10);
    }

    internal static string fClassName(DataImport pOData, UWPlayerData pOPlayer)
    {
        if (pOPlayer == null)
            return string.Empty;

        try
        {
            // Block 2 from 0x18 on, as on the character panel (UWHudPanel.fGetClassName).
            return fTitleCase(pOData.Strings.Blocks[2].Strings[0x18 + pOPlayer.CharacterClass]);
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string fSkillName(DataImport pOData, int piEntry)
    {
        try
        {
            return fTitleCase(pOData.Strings.Blocks[2].Strings[UWPlayerData.SkillNameStringIndex + piEntry]);
        }
        catch
        {
            return "?";
        }
    }

    private static string fTitleCase(string psText)
    {
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            (psText ?? string.Empty).Trim().ToLowerInvariant());
    }

    // ------------------------------------------------- Map

    /// <summary>How many tiles the map tab shows across; the wheel changes it.</summary>
    private int miMapSpan = 20;

    private const int MapSpanStep = 4;

    private const int MinMapSpan = 8;

    private const int MaxMapSpan = 64;

    /// <summary>How often the tiles are redrawn while standing still - the automap also fills in
    /// from what is seen, not only where one walks.</summary>
    private const float MapRefreshSeconds = 0.5f;

    private bool mbPendingMap;

    private Rect mOPendingMapRect;

    private Texture2D mOMapTiles;

    private Vector2 mOMapBuiltAt = new Vector2(-1f, -1f);

    private float mfMapBuiltTime = -1f;

    /// <summary>
    /// THE MAP TAB (feature wish of the user, 2026-09-26): a cut-out of the map around the
    /// character, in the original's look - the parchment, the discovered tiles, the notes -, kept
    /// up to date while the help is open. Only with the map carried, as the real one. Notes are
    /// only shown: they are written and erased on the real map alone (per user).
    /// </summary>
    private void fDrawMap()
    {
        UWGameUI lOUi = UWScene.GameUi;
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOUi == null || lOLoader == null)
        {
            GUILayout.Label("Map", mOHeading);
            GUILayout.Label("No level is loaded.", mODimText);

            return;
        }

        if (!lOUi.IsCarryingMap)
        {
            GUILayout.Label("Map", mOHeading);
            GUILayout.Label("You carry no map. Pick it up again to see it here.", mODimText);

            return;
        }

        GUILayout.Label(string.Format("Level {0}. The wheel zooms; notes are written on the map itself.",
            lOLoader.CurrentLevelIndex + 1), mODimText);
        GUILayout.Space(6f);

        Rect lOSpace = GUILayoutUtility.GetRect(16f, 16f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        Event lOEvent = Event.current;

        if (lOEvent.type == EventType.Layout || lOSpace.width < 4f || lOSpace.height < 4f)
            return;

        if (lOEvent.type == EventType.Repaint)
        {
            float lfLeft = Mathf.Round((mOAreaOrigin.x + lOSpace.x) * mfScale);
            float lfTop = Mathf.Round((mOAreaOrigin.y + lOSpace.y) * mfScale);

            mOPendingMapRect = new Rect(lfLeft, lfTop,
                Mathf.Round((mOAreaOrigin.x + lOSpace.xMax) * mfScale) - lfLeft,
                Mathf.Round((mOAreaOrigin.y + lOSpace.yMax) * mfScale) - lfTop);
            mbPendingMap = true;
        }
        else if (GUI.enabled && lOEvent.type == EventType.ScrollWheel && lOSpace.Contains(lOEvent.mousePosition))
        {
            miMapSpan = Mathf.Clamp(miMapSpan + (lOEvent.delta.y > 0f ? MapSpanStep : -MapSpanStep), MinMapSpan, MaxMapSpan);
            lOEvent.Use();

            if (UWUserSettings.HelpMapSpan != miMapSpan)
            {
                UWUserSettings.HelpMapSpan = miMapSpan;
                UWUserSettings.Save();
            }
        }
    }

    /// <summary>The map cut-out at whole screen pixels per map pixel, centred on the character,
    /// clipped to its space and to the panel's left edge (it too lies behind the sliding frame).
    /// </summary>
    private void fDrawPendingMap(float pfClipPixels)
    {
        UWGameUI lOUi = UWScene.GameUi;

        if (lOUi == null)
            return;

        UWHudMap lOMap = lOUi.Map;

        if (!lOMap.TryGetMinimapPlayerPixel(out Vector2 lOPlayer))
            return;

        if (mOMapTiles == null || lOPlayer != mOMapBuiltAt || Time.unscaledTime - mfMapBuiltTime >= MapRefreshSeconds)
        {
            mOMapTiles = lOMap.BuildMinimapTiles();
            mOMapBuiltAt = lOPlayer;
            mfMapBuiltTime = Time.unscaledTime;
        }

        Texture2D lOParchment = lOMap.GetMinimapParchment();

        float lfLeft = Mathf.Max(mOPendingMapRect.x, pfClipPixels);
        Rect lOClip = new Rect(lfLeft, mOPendingMapRect.y, mOPendingMapRect.xMax - lfLeft, mOPendingMapRect.height);

        if (lOClip.width <= 0f || lOClip.height <= 0f)
            return;

        int liScale = Mathf.Max(1, Mathf.RoundToInt(mOPendingMapRect.width / (miMapSpan * UWHudMap.TilePixels)));

        GUI.matrix = Matrix4x4.identity;
        GUI.BeginGroup(lOClip);

        GUI.DrawTexture(new Rect(0f, 0f, lOClip.width, lOClip.height), fTexture(FieldColour));

        float lfOriginX = Mathf.Round(mOPendingMapRect.center.x - lfLeft - (lOPlayer.x * liScale));
        float lfOriginY = Mathf.Round((mOPendingMapRect.height / 2f) - (lOPlayer.y * liScale));

        if (lOParchment != null)
            GUI.DrawTexture(new Rect(lfOriginX, lfOriginY, lOParchment.width * liScale, lOParchment.height * liScale),
                lOParchment);

        if (mOMapTiles != null)
        {
            Vector2 lOTiles = UWHudMap.TilesTopLeft;
            float lfSize = UWHudMap.TilesPerAxis * UWHudMap.TilePixels * liScale;

            GUI.DrawTexture(new Rect(lfOriginX + (lOTiles.x * liScale), lfOriginY + (lOTiles.y * liScale), lfSize, lfSize),
                mOMapTiles);
        }

        foreach (UWHudMap.MinimapNote lONote in lOMap.GetMinimapNotes())
        {
            GUI.DrawTexture(new Rect(lfOriginX + (lONote.TopLeft.x * liScale), lfOriginY + (lONote.TopLeft.y * liScale),
                lONote.Texture.width * liScale, lONote.Texture.height * liScale), lONote.Texture);
        }

        GUI.EndGroup();
    }

    private static DataImport fData()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        return lOLoader != null ? lOLoader.UWDataImporter : null;
    }

    private void fDrawManual()
    {
        if (!UWManual.EnsureOpen())
        {
            GUILayout.Label("Manual", mOHeading);
            GUILayout.Label(UWManual.Problem ?? "The manual cannot be shown.", mODimText);

            return;
        }

        int liCount = UWManual.PageCount;

        msPage = Mathf.Clamp(msPage, 0, Mathf.Max(0, liCount - 1));

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("<", mOFoldout, GUILayout.Width(44f), GUILayout.Height(28f)))
            fTurn(-1);

        GUILayout.Label(string.Format("Page {0} of {1}", msPage + 1, liCount), mOCentred, GUILayout.Height(28f),
            GUILayout.ExpandWidth(true));

        if (GUILayout.Button(">", mOFoldout, GUILayout.Width(44f), GUILayout.Height(28f)))
            fTurn(1);

        GUILayout.EndHorizontal();

        GUILayout.Label("Click the page to read it large.", mODimText);
        GUILayout.Space(6f);

        Rect lOSpace = GUILayoutUtility.GetRect(16f, 16f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        Event lOEvent = Event.current;

        if (lOEvent.type == EventType.Layout || lOSpace.width < 4f || lOSpace.height < 4f)
            return;

        // The page as large as the space allows, placed on whole screen pixels.
        Vector2 lOSize = UWManual.GetPageSize(msPage);
        float lfLeft = (mOAreaOrigin.x + lOSpace.x) * mfScale;
        float lfTop = (mOAreaOrigin.y + lOSpace.y) * mfScale;
        float lfRoomWidth = lOSpace.width * mfScale;
        float lfRoomHeight = lOSpace.height * mfScale;

        int liWidth = Mathf.FloorToInt(Mathf.Min(lfRoomWidth, lfRoomHeight * lOSize.x / lOSize.y));
        int liHeight = Mathf.RoundToInt(liWidth * lOSize.y / lOSize.x);

        if (liWidth < 16 || liHeight < 16)
            return;

        Rect lOOnScreen = new Rect(Mathf.Round(lfLeft + ((lfRoomWidth - liWidth) / 2f)), Mathf.Round(lfTop), liWidth, liHeight);
        Rect lOLocal = new Rect((lOOnScreen.x / mfScale) - mOAreaOrigin.x, (lOOnScreen.y / mfScale) - mOAreaOrigin.y,
            liWidth / mfScale, liHeight / mfScale);

        if (lOEvent.type == EventType.Repaint)
        {
            mOPendingPage = UWManual.GetPage(msPage, liWidth, liHeight);
            mOPendingPageRect = lOOnScreen;
        }
        else if (GUI.enabled && lOEvent.type == EventType.MouseDown && lOEvent.button == 0
            && lOLocal.Contains(lOEvent.mousePosition))
        {
            fSetReading(true);
            lOEvent.Use();
        }
        else if (GUI.enabled && lOEvent.type == EventType.ScrollWheel && lOSpace.Contains(lOEvent.mousePosition))
        {
            fTurn(lOEvent.delta.y > 0f ? 1 : -1);
            lOEvent.Use();
        }
    }

    private static void fTurn(int piPages)
    {
        msPage = Mathf.Clamp(msPage + piPages, 0, Mathf.Max(0, UWManual.PageCount - 1));
    }

    /// <summary>
    /// The manual over the whole picture. Page one is the cover and stands alone on the
    /// right; after it the even pages lie left and the odd ones right, as in the printed
    /// booklet. Two pages when both fit at screen height, one otherwise.
    /// </summary>
    private void fDrawReading()
    {
        int liCount = UWManual.PageCount;

        if (liCount <= 0)
        {
            fSetReading(false);
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

        GUI.matrix = Matrix4x4.identity;
        GUI.DrawTexture(new Rect(0f, 0f, lfWidth, lfHeight), fTexture(ReadingShadeColour));

        float lfX = Mathf.Round((lfWidth - lfShown) / 2f);
        float lfY = Mathf.Round(lfMargin + ((lfHeight - (2f * lfMargin) - lfBar - lfPageHeight) / 2f));
        Event lOEvent = Event.current;
        bool lbClickedPage = false;

        foreach (int liPage in new[] { liLeft, liRight })
        {
            if (liPage < 0)
                continue;

            int liPageWidth = Mathf.RoundToInt(fPageWidth(liPage, lfPageHeight));
            int liPageHeight = Mathf.RoundToInt(lfPageHeight);
            Rect lORect = new Rect(lfX, lfY, liPageWidth, liPageHeight);

            if (lOEvent.type == EventType.Repaint)
            {
                Texture2D lOPage = UWManual.GetPage(liPage, liPageWidth, liPageHeight);

                if (lOPage != null)
                    GUI.DrawTexture(lORect, lOPage);
            }
            else if (lOEvent.type == EventType.MouseDown && lORect.Contains(lOEvent.mousePosition))
                lbClickedPage = true;

            lfX += liPageWidth;
        }

        int liStep = lbSpread ? 2 : 1;

        // Arrows, page number and hint in the help's own scale.
        float lfScale = Screen.height / ReferenceHeight;
        float lfUnitsWide = lfWidth / lfScale;

        GUI.matrix = Matrix4x4.Scale(new Vector3(lfScale, lfScale, 1f));

        float lfArrowWidth = lfArrowZone / lfScale;
        float lfArrowTop = (ReferenceHeight / 2f) - 40f;

        if (msPage > 0 && GUI.Button(new Rect(lfMargin / lfScale, lfArrowTop, lfArrowWidth, 80f), "<", mOArrow))
            fTurn(-liStep);

        if (fLastShown(liLeft, liRight) < liCount - 1
            && GUI.Button(new Rect(lfUnitsWide - (lfMargin / lfScale) - lfArrowWidth, lfArrowTop, lfArrowWidth, 80f), ">", mOArrow))
            fTurn(liStep);

        string lsPages = liRight >= 0 && liLeft >= 0
            ? string.Format("Pages {0} and {1} of {2}", liLeft + 1, liRight + 1, liCount)
            : string.Format("Page {0} of {1}", Mathf.Max(liLeft, liRight) + 1, liCount);

        GUI.Label(new Rect(0f, ReferenceHeight - (lfBar / lfScale) - (lfMargin / lfScale), lfUnitsWide, lfBar / lfScale),
            lsPages + "      Arrow keys or the wheel turn the pages, Esc or a click on the page closes", mOCentred);

        // The keys and the wheel - the game hears none of them while reading.
        if (lOEvent.type == EventType.KeyDown)
        {
            switch (lOEvent.keyCode)
            {
                case KeyCode.LeftArrow:
                case KeyCode.PageUp:
                    fTurn(-liStep);
                    lOEvent.Use();
                    break;

                case KeyCode.RightArrow:
                case KeyCode.PageDown:
                case KeyCode.Space:
                    fTurn(liStep);
                    lOEvent.Use();
                    break;

                case KeyCode.Home:
                    msPage = 0;
                    lOEvent.Use();
                    break;

                case KeyCode.End:
                    msPage = liCount - 1;
                    lOEvent.Use();
                    break;

                case KeyCode.Escape:
                    fSetReading(false);
                    lOEvent.Use();
                    break;

                case KeyCode.Tab:
                    // Not with Alt: that is the switch to another program.
                    if (!lOEvent.alt)
                    {
                        fSetReading(false);
                        lOEvent.Use();
                    }

                    break;
            }
        }
        else if (lOEvent.type == EventType.ScrollWheel)
        {
            fTurn(lOEvent.delta.y > 0f ? liStep : -liStep);
            lOEvent.Use();
        }
        else if (lbClickedPage && lOEvent.type == EventType.MouseDown)
        {
            // The click that closes is the help's: the game gets neither its release nor the
            // button held down after it (per user, 2026-09-26).
            fSetReading(false, true);
            lOEvent.Use();
        }
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

    // ------------------------------------------------- Styles

    private void fEnsureStyles()
    {
        if (mOText != null)
            return;

        mOFont = UWInterfaceFont.Font;

        mOText = new GUIStyle { font = mOFont, fontSize = 15, wordWrap = true, richText = true };
        mOText.normal.textColor = TextColour;

        mODimText = new GUIStyle(mOText) { fontSize = 13 };
        mODimText.normal.textColor = DimTextColour;

        mOHeading = new GUIStyle(mOText) { fontSize = 20, fontStyle = FontStyle.Bold };
        mOHeading.normal.textColor = AccentColour;

        mOSubHeading = new GUIStyle(mOText) { fontSize = 16, fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 6, 2) };
        mOSubHeading.normal.textColor = AccentColour;

        mOTab = new GUIStyle(mOText) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = false };
        mOTab.normal.background = fTexture(TabColour);
        mOTab.hover.background = fTexture(HoverColour);
        mOTab.hover.textColor = TextColour;

        mOCurrentTab = new GUIStyle(mOTab);
        mOCurrentTab.normal.textColor = AccentColour;
        mOCurrentTab.hover.textColor = AccentColour;
        mOCurrentTab.normal.background = fTexture(HoverColour);

        mOFoldout = new GUIStyle(mOTab) { fontSize = 13, padding = new RectOffset(10, 10, 6, 6) };

        mOCentred = new GUIStyle(mOText) { alignment = TextAnchor.MiddleCenter, wordWrap = false };

        mOLabel = new GUIStyle(mOText) { wordWrap = false };

        mOValue = new GUIStyle(mOText) { wordWrap = false, alignment = TextAnchor.UpperRight };

        mOIcon = new GUIStyle { padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 3, 0, 0) };

        mOArrow = new GUIStyle(mOTab) { fontSize = 28 };

        mONotes = new GUIStyle(mOText) { padding = new RectOffset(8, 8, 8, 8), richText = false };
        mONotes.normal.background = fTexture(FieldColour);
        mONotes.focused.background = fTexture(FieldColour);
        mONotes.focused.textColor = TextColour;
        mONotes.hover.textColor = TextColour;
    }

    private Texture2D fTexture(Color pOColour)
    {
        if (mOTextures.TryGetValue(pOColour, out Texture2D lOTexture) && lOTexture != null)
            return lOTexture;

        lOTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        lOTexture.name = "UWHelpWindow";
        lOTexture.SetPixel(0, 0, pOColour);
        lOTexture.Apply();
        mOTextures[pOColour] = lOTexture;

        return lOTexture;
    }
}
