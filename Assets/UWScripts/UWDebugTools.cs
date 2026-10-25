using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// Helpers for testing, all for the workshop only (2026-09-06).
///
/// JUMP (Shift+F7): an input line into which "level,X,Y" is typed - "3,45,13" takes you
/// to level 3, tile 45/13. While it is open, all game controls are
/// disabled; otherwise the character would start walking while typing and the digits would end up in
/// some actions.
///
/// INVISIBLE OBJECTS: traps and triggers (objects 384 to 447) get no GameObject at all
/// when the level is built - they are pure logic markers (see UWObjectSpawner). So you cannot
/// tell where they lie. In FULL-BRIGHT mode they are shown as labels at their
/// position - the same switch that also brightens the old render path
/// (key B, see UWLighting.ToggleDebugBrightness).
///
/// SPELL SELECTION (Shift+F10): a searchable list of all spells that casts one without circle,
/// mana and check - see DrawTab.
///
/// ON THE KIT (cleanup 8c, 2026-10-25): the jump line and the spell list are the TOOLS TAB of the
/// debug panel (UWDebugPanel, beside the F1 report), with their fields on the kit's text field
/// (UWKitRows on UWTextEntry and UWTypedKeys, so they type on Linux as well); Shift+F7 opens the tab
/// with the jump line typing, Shift+F10 with the search, the same key again or Escape closes it. The
/// labels in the world are uGUI texts and lines on a canvas of their own (UWDebugLabels) - no
/// materials, no render path, nothing in the palette renderer's way, as IMGUI's were. Until then two
/// IMGUI boxes and OnGUI.
/// </summary>
public class UWDebugTools : MonoBehaviour
{
    /// <summary>Invisible objects up to this distance are still labelled.</summary>
    [SerializeField]
    private float mfLabelRange = 1500f;

    private UWLevelLoader mOLoader;

    private UWRenderPathHost mOPalette;

    private UWControlScheme mOScheme;

    /// <summary>The tools' tab is open (one flag since cleanup 8c; the jump and the spell box had one
    /// each, and closing one released the hold the other still needed).</summary>
    private bool mbOpen;

    private static UWDebugTools msInstance;

    /// <summary>The tools of the scene, null without one.</summary>
    public static UWDebugTools Instance => msInstance;

    /// <summary>Is the tools' tab open? Then the special keys stay silent, see
    /// UWControls.IsTextEntryActive.</summary>
    public static bool IsTyping => msInstance != null && msInstance.mbOpen;

    /// <summary>The tools' tab is open (the panel shows it).</summary>
    public static bool IsOpen => IsTyping;

    private enum FieldEnum
    {
        None,
        Jump,
        Search
    }

    /// <summary>The key that opened the tab (its second press closes it again), and the field that
    /// takes the keyboard when the tab is drawn next.</summary>
    private FieldEnum meOpenedFor;

    private FieldEnum meFocusPending;

    private string msJumpText = string.Empty;

    private const string JumpField = "UWJump";

    private const string SearchField = "UWSpellSearch";

    /// <summary>The jump line's length, as the IMGUI field's.</summary>
    private const int JumpMaxLength = 24;

    private UWDebugLabels mOLabels;

    public void Init(UWLevelLoader pOLoader)
    {
        msInstance = this;
        mOLoader = pOLoader;
    }

    private void OnDestroy()
    {
        if (msInstance == this)
            msInstance = null;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // While typing in another input, Shift+F7 and Shift+F10 stay silent
        // (UWControls.IsTextEntryActive); they still close their own tab.
        // Shift+F7 and Shift+F10 since 2026-09-26: plain F7 turns the panel and F10 makes camp,
        // as in the original. Read off the keys themselves - the debug map's actions fall silent
        // while the tab is open. UWHotkeys' rows since cleanup step 7.5.
        if (mOKeys == null)
            mOKeys = new ToolKeys(this);

        UWHotkeys.Route(UWHotkeySite.DebugTools, mOKeys);

        if (mbOpen && UWBack.Takes(UWBackOwner.DebugTools))
            Close();
    }

    private ToolKeys mOKeys;

    /// <summary>The tools' hotkeys (UWHotkeys): Shift+F7 the jump, Shift+F10 the spell search.</summary>
    private sealed class ToolKeys : UWHotkeyHost
    {
        private readonly UWDebugTools mOTools;

        public ToolKeys(UWDebugTools pOTools)
        {
            mOTools = pOTools;
        }

        protected override UWControls Controls => mOTools.mOScheme != null ? mOTools.mOScheme.Controls : null;

        public override bool Gate(UWHotkeyGate peGate, UWHotkeyRow pORow)
        {
            return peGate == UWHotkeyGate.OtherTyping && UWControls.IsTextEntryActive && !mOTools.mbOpen;
        }

        public override bool Run(UWHotkeyHandler peHandler)
        {
            if (peHandler == UWHotkeyHandler.ToggleDebugJump)
                mOTools.fToggle(FieldEnum.Jump);
            else if (peHandler == UWHotkeyHandler.ToggleDebugSearch)
                mOTools.fToggle(FieldEnum.Search);
            else
                return false;

            return true;
        }
    }

    /// <summary>The tab's key: opens the tab with its field typing; pressed again it closes the tab,
    /// the other key's press moves the typing to its field (until cleanup 8c Shift+F10 closed the jump
    /// box and opened the spells, Shift+F7 opened the jump box beside the spells).</summary>
    private void fToggle(FieldEnum peField)
    {
        if (mbOpen && meOpenedFor == peField)
        {
            Close();

            return;
        }

        if (!mbOpen)
            fSetOpen(true);

        meOpenedFor = peField;
        meFocusPending = peField;
    }

    /// <summary>Opens the tab without a field typing (the panel's Tools tab).</summary>
    public void Open()
    {
        if (mbOpen)
            return;

        fSetOpen(true);
        meOpenedFor = FieldEnum.None;
        meFocusPending = FieldEnum.None;
    }

    /// <summary>Closes the tab (Escape, its key again, a jump, a cast, the panel's Report tab).</summary>
    public void Close()
    {
        if (mbOpen)
            fSetOpen(false);
    }

    /// <summary>
    /// Opens or closes the tab.
    ///
    /// All controls are switched off and on again in the process. Merely releasing the
    /// pointer is not enough: movement reads its actions directly, the character
    /// would keep walking while typing.
    /// </summary>
    private void fSetOpen(bool pbOpen)
    {
        mbOpen = pbOpen;
        meOpenedFor = FieldEnum.None;
        meFocusPending = FieldEnum.None;

        if (pbOpen)
            msJumpText = string.Empty;

        if (mOScheme == null)
            mOScheme = UWScene.ControlScheme;

        if (mOScheme == null)
            return;

        mOScheme.SetUiModal("debug tools", pbOpen);

        if (pbOpen)
            mOScheme.Controls.Disable();
        else
            mOScheme.Controls.Enable();
    }

    private void LateUpdate()
    {
        bool lbLabels = mOLoader != null && fIsFullBright();

        if (mOLabels == null)
        {
            if (!lbLabels)
                return;

            mOLabels = new UWDebugLabels(transform);
        }

        mOLabels.Begin(lbLabels);

        if (!lbLabels)
            return;

        fDrawInvisibleObjects();
        mOLabels.End();
    }

    // ------------------------------------------------- The tools' tab

    private Vector2 mOSpellScroll;

    private string msSpellFilter = string.Empty;

    /// <summary>Each spell's line (fDescribeSpell), made once: the search runs over them.</summary>
    private string[] msSpellLabels;

    private readonly List<int> mOShownSpells = new List<int>();

    private const float FieldHeight = 26f;

    private const float SpellRowHeight = 20f;

    /// <summary>
    /// The tools' tab in the debug panel's area (UWDebugPanel, cleanup 8c): the jump line, then the
    /// spell selection - the search field and the list.
    ///
    /// Casting goes through UWGameUI.CastSpellFromObject - the same path a
    /// wand takes: WITHOUT spell circle, without mana and without skill check. The
    /// reference calls it CastRunicSpellWithoutRules.
    ///
    /// A projectile spell is NOT sent off immediately but, as with rune
    /// casting, waits for the click that gives the direction; a class 7 spell waits for
    /// its target. Otherwise neither could be tested.
    /// </summary>
    public void DrawTab(UWKitRows pOKit, Rect pOArea)
    {
        if (mOLoader == null || mOLoader.UWDataImporter == null)
            return;

        UWKitRows.HelpLook lOText = new UWKitRows.HelpLook { Size = UWDebugPanel.TextSize, Colour = UWGuiTheme.Text, Anchor = TextAnchor.MiddleLeft };
        UWKitRows.HelpLook lODim = lOText;

        lODim.Colour = UWGuiTheme.DimText;

        float lfX = pOArea.x;
        float lfY = pOArea.y;

        // The jump line: "level,X,Y" or "X,Y" on this level.
        string lsJump = "Jump:  level,X,Y";
        float lfLabel = pOKit.HelpTextWidth("Search:", lOText) + 12f;

        lfLabel = Mathf.Max(lfLabel, pOKit.HelpTextWidth(lsJump, lOText) + 12f);
        pOKit.HelpText(new Rect(lfX, lfY, lfLabel, FieldHeight), lsJump, lOText);

        if (meFocusPending == FieldEnum.Jump)
            pOKit.FocusField(JumpField, msJumpText, JumpMaxLength);

        msJumpText = pOKit.TextField(new Rect(lfX + lfLabel, lfY, 160f, FieldHeight), JumpField, msJumpText, JumpMaxLength);

        bool lbGo = pOKit.Action(new Rect(lfX + lfLabel + 168f, lfY, 50f, FieldHeight), "Go");

        pOKit.HelpText(new Rect(lfX + lfLabel + 228f, lfY, Mathf.Max(0f, pOArea.width - lfLabel - 228f), FieldHeight), "[Enter] go   [Esc] cancel", lODim);

        if (lbGo || pOKit.EnterIn(JumpField))
        {
            fJump(msJumpText);

            return;
        }

        lfY += FieldHeight + 12f;

        // The spell selection.
        pOKit.HelpText(new Rect(lfX, lfY, pOArea.width, 18f), "Cast spell - without circle, mana and check. [Esc] closes.", lODim);
        lfY += 22f;

        pOKit.HelpText(new Rect(lfX, lfY, lfLabel, FieldHeight), "Search:", lOText);

        if (meFocusPending == FieldEnum.Search)
            pOKit.FocusField(SearchField, msSpellFilter);

        meFocusPending = FieldEnum.None;
        msSpellFilter = pOKit.TextField(new Rect(lfX + lfLabel, lfY, Mathf.Min(300f, pOArea.width - lfLabel), FieldHeight), SearchField, msSpellFilter);
        lfY += FieldHeight + 8f;

        fFilterSpells();

        Rect lOView = new Rect(lfX, lfY, pOArea.width, Mathf.Max(SpellRowHeight, pOArea.yMax - lfY));
        float lfContent = mOShownSpells.Count * SpellRowHeight;
        float lfRowWidth = lOView.width - (lfContent > lOView.height ? UWKitRows.ScrollBarRoom + 2f : 0f);
        int liCast = -1;

        mOSpellScroll = pOKit.BeginScroll(lOView, mOSpellScroll, new Rect(0f, 0f, lOView.width, lfContent));

        for (int liRow = 0; liRow < mOShownSpells.Count; liRow++)
        {
            float lfRowY = liRow * SpellRowHeight;

            // Only the rows in sight are drawn.
            if (lfRowY + SpellRowHeight < mOSpellScroll.y || lfRowY > mOSpellScroll.y + lOView.height)
                continue;

            if (fDrawSpellRow(pOKit, new Rect(0f, lfRowY, lfRowWidth, SpellRowHeight - 1f), mOShownSpells[liRow], lOText, lODim))
                liCast = mOShownSpells[liRow];
        }

        pOKit.EndScroll();

        if (liCast < 0)
            return;

        Close();

        UWGameUI lOUi = mOLoader.GetComponent<UWGameUI>();

        if (lOUi == null)
            lOUi = UWScene.GameUi;

        if (lOUi != null)
            lOUi.Runes.CastSpellFromObject(liCast, false);
    }

    /// <summary>The spells whose line holds the search text (any case), in their order.</summary>
    private void fFilterSpells()
    {
        if (msSpellLabels == null || msSpellLabels.Length != UWRunicMagic.AllSpells.Count)
        {
            msSpellLabels = new string[UWRunicMagic.AllSpells.Count];

            for (int liAt = 0; liAt < msSpellLabels.Length; liAt++)
                msSpellLabels[liAt] = fDescribeSpell(liAt);
        }

        mOShownSpells.Clear();

        for (int liAt = 0; liAt < msSpellLabels.Length; liAt++)
        {
            if (!string.IsNullOrEmpty(msSpellFilter)
                && msSpellLabels[liAt].IndexOf(msSpellFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            mOShownSpells.Add(liAt);
        }
    }

    /// <summary>A spell's row: lit under the pointer, cast on the release over it; its number, name,
    /// class, circle and runes in columns.</summary>
    private bool fDrawSpellRow(UWKitRows pOKit, Rect pORect, int piSpell, UWKitRows.HelpLook pOText, UWKitRows.HelpLook pODim)
    {
        UWRunicMagic.Spell lOSpell = UWRunicMagic.AllSpells[piSpell];
        bool lbClicked = pOKit.HelpButton(pORect, string.Empty, pOText, new Color(0f, 0f, 0f, 0f), UWGuiTheme.Hover, UWGuiTheme.Text);
        float lfX = pORect.x + 6f;

        pOKit.HelpText(new Rect(lfX, pORect.y, 28f, pORect.height), piSpell.ToString(), pODim);
        pOKit.HelpText(new Rect(lfX + 30f, pORect.y, 210f, pORect.height), fSpellName(lOSpell), pOText);
        pOKit.HelpText(new Rect(lfX + 244f, pORect.y, 80f, pORect.height),
            string.Format("Class {0}-{1:X}", lOSpell.MajorClass, lOSpell.MinorClass & 0xF), pODim);
        pOKit.HelpText(new Rect(lfX + 326f, pORect.y, 64f, pORect.height), string.Format("Circle {0}", lOSpell.Level), pODim);
        pOKit.HelpText(new Rect(lfX + 392f, pORect.y, Mathf.Max(0f, pORect.width - 398f), pORect.height),
            UWRunicMagic.DescribeRunes(lOSpell.RuneSequence), pOText);

        return lbClicked;
    }

    private string fSpellName(UWRunicMagic.Spell pOSpell)
    {
        string lsName = UWRunicMagic.GetName(pOSpell, mOLoader.UWDataImporter.Strings);

        return string.IsNullOrWhiteSpace(lsName) ? "(no name)" : lsName;
    }

    /// <summary>Number, name, class and rune sequence - so the spell can be found again
    /// even when the name is missing.</summary>
    private string fDescribeSpell(int piIndex)
    {
        UWRunicMagic.Spell lOSpell = UWRunicMagic.AllSpells[piIndex];

        return string.Format("{0,2}  {1,-28} Class {2}-{3:X}  Circle {4}  {5}",
            piIndex, fSpellName(lOSpell), lOSpell.MajorClass, lOSpell.MinorClass & 0xF, lOSpell.Level,
            UWRunicMagic.DescribeRunes(lOSpell.RuneSequence));
    }

    // ------------------------------------------------- Invisible objects

    private bool fIsFullBright()
    {
        if (mOPalette == null)
            mOPalette = mOLoader.GetComponent<UWRenderPathHost>();

        return mOPalette != null && mOPalette.FullBright;
    }

    /// <summary>
    /// Labels every trap and trigger object of the level at its position.
    ///
    /// The position is the exact one: tile corner plus sub-tile position, height from ZPos - i.e.
    /// the same point the trigger zone is attached to (see
    /// UWLevelLoader.fCreateTriggerVolume). For a move trigger you can see
    /// immediately that it does not sit in the tile centre.
    /// </summary>
    private void fDrawInvisibleObjects()
    {
        if (mOLoader.CurrentLevel == null || mOLoader.CurrentLevel.TileData == null)
            return;

        // NOT Camera.main: in noclip mode the player camera is switched off, and
        // Camera.main then returns null - the labels would be missing exactly where you
        // need them most (per user, 2026-09-06).
        Camera lOCamera = fGetActiveCamera();

        if (lOCamera == null)
            return;

        foreach (UWTile lOTile in mOLoader.CurrentLevel.TileData)
        {
            if (lOTile == null || lOTile.ObjectsInTile == null)
                continue;

            foreach (UWObject lOObject in lOTile.ObjectsInTile)
            {
                if (lOObject == null || lOObject.ID < FirstLogicObjectId || lOObject.ID > LastLogicObjectId)
                    continue;

                Vector3 lOWorld = UWViewpoint.SubTileToWorld(lOTile.X, lOTile.Z, lOObject);

                if (Vector3.Distance(lOCamera.transform.position, lOWorld) > mfLabelRange)
                    continue;

                Vector3 lOScreen = lOCamera.WorldToScreenPoint(lOWorld);

                // Behind the camera the point comes out mirrored.
                if (lOScreen.z <= 0f)
                    continue;

                string lsText = string.Format("{0}\n{1}/{2}",
                    UWTrapLog.GetName(mOLoader != null ? mOLoader.UWDataImporter : null, lOObject.ID), lOTile.X, lOTile.Z);

                Vector2 lOAt = new Vector2(lOScreen.x, lOScreen.y);

                fDrawTargetLine(lOCamera, lOObject, lOAt);

                mOLabels.Label(lOAt, lsText);
            }
        }
    }

    /// <summary>Traps and triggers - major class 6.</summary>
    private const int FirstLogicObjectId = 384;

    private const int LastLogicObjectId = 447;

    /// <summary>From here on they are triggers, below that traps. Only triggers have a
    /// target tile.</summary>
    private const int FirstTriggerId = UWTrapRules.FirstTriggerId;

    /// <summary>
    /// Draws a line from the trigger to its TARGET TILE.
    ///
    /// With 189 move triggers on one level this is the fastest way to see what
    /// belongs to what - especially since trigger and trap almost always lie on different tiles.
    /// Without the line you would have to compare the label numbers by hand.
    ///
    /// Only for TRIGGERS: a trap has no target tile of its own, the trigger supplies it.
    /// </summary>
    private void fDrawTargetLine(Camera pOCamera, UWObject pOTrigger, Vector2 pOFrom)
    {
        if (pOTrigger.ID < FirstTriggerId)
            return;

        (int liTileX, int liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

        UWTile lOTarget = mOLoader.CurrentLevel.GetTile(liTileX, liTileY);

        if (lOTarget == null)
            return;

        Vector3 lOWorld = new Vector3(
            UWUnits.TileCentreToWorldAxis(liTileX),
            lOTarget.FloorHeight + UWWorldScale.TileHalfSize,
            UWUnits.TileCentreToWorldAxis(liTileY));

        Vector3 lOScreen = pOCamera.WorldToScreenPoint(lOWorld);

        if (lOScreen.z <= 0f)
            return;

        mOLabels.Line(pOFrom, new Vector2(lOScreen.x, lOScreen.y));
    }

    /// <summary>In noclip mode the player camera is switched off - then the spectator
    /// camera draws. Same approach as in UWDebugOverlay.</summary>
    private static Camera fGetActiveCamera()
    {
        return UWScene.ActiveCamera;
    }

    // ------------------------------------------------- The jump

    /// <summary>
    /// "3,45,13" means level 3, tile 45/13. Two numbers without a level ("45,13") jump
    /// within the current level.
    ///
    /// Separators are comma, semicolon, slash and space - depending on how
    /// you happened to note down the tile.
    /// </summary>
    private void fJump(string psText)
    {
        Close();

        if (string.IsNullOrEmpty(psText))
            return;

        string[] lsParts = psText.Split(new char[] { ',', ';', '/', ' ' },
            System.StringSplitOptions.RemoveEmptyEntries);

        int liLevel;
        int liX;
        int liY;

        if (lsParts.Length >= 3)
        {
            if (!int.TryParse(lsParts[0], out liLevel) || !int.TryParse(lsParts[1], out liX)
                || !int.TryParse(lsParts[2], out liY))
                return;

            liLevel--;
        }
        else if (lsParts.Length == 2)
        {
            if (!int.TryParse(lsParts[0], out liX) || !int.TryParse(lsParts[1], out liY))
                return;

            liLevel = mOLoader.CurrentLevelIndex;
        }
        else
        {
            return;
        }

        if (liLevel < 0 || liLevel >= mOLoader.LevelCount
            || liX < 0 || liX >= UWWorldScale.TilesPerAxis
            || liY < 0 || liY >= UWWorldScale.TilesPerAxis)
        {
            Debug.LogWarning(string.Format("Jump: {0}/{1} on level {2} is out of range.",
                liX, liY, liLevel + 1));

            return;
        }

        Debug.Log(string.Format("Jump to level {0}, tile {1}/{2}.", liLevel + 1, liX, liY));

        mOLoader.TravelTo(liLevel, liX, liY);

        fEnterSpectatorIfSolid(liX, liY);
    }

    /// <summary>
    /// If the target tile is SOLID, noclip mode is switched on.
    ///
    /// Otherwise the character stands inside rock: it cannot move, and nothing can be
    /// seen either. But precisely when checking what is inside a wall - a secret door, say -
    /// that is exactly where you want to go.
    ///
    /// Checked ONLY AFTER THE JUMP: before that CurrentLevel still points to the old level.
    /// </summary>
    private void fEnterSpectatorIfSolid(int piTileX, int piTileY)
    {
        if (mOLoader.CurrentLevel == null || mOLoader.CurrentLevel.TileData == null)
            return;

        UWTile lOTile = mOLoader.CurrentLevel.GetTile(piTileX, piTileY);

        if (lOTile == null || lOTile.TileType != UWTile.TileTypeEnum.solid)
            return;

        DebugFunctions lODebug = FindAnyObjectByType<DebugFunctions>();

        if (lODebug == null)
        {
            Debug.LogWarning("Jump: target tile is solid, but there is no DebugFunctions for noclip mode.");

            return;
        }

        Debug.Log("Jump: target tile is solid - noclip on.");

        lODebug.EnterSpectator(new Vector3(
            UWUnits.TileCentreToWorldAxis(piTileX),
            lOTile.FloorHeight + UWWorldScale.TileHalfSize,
            UWUnits.TileCentreToWorldAxis(piTileY)));
    }
}
