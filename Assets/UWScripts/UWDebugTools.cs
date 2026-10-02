using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// Helpers for testing, all for the workshop only (2026-09-06).
///
/// JUMP (F7): an input line into which "level,X,Y" is typed - "3,45,13" takes you
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
/// SPELL SELECTION (F10): a searchable list of all spells that casts one without circle,
/// mana and check - see fDrawSpellBox.
///
/// Drawing uses IMGUI instead of marker bodies in the world: no materials,
/// no render path, nothing that could get in the way of the palette renderer - and the name
/// comes along right away.
/// </summary>
public class UWDebugTools : MonoBehaviour
{
    /// <summary>Invisible objects up to this distance are still labelled.</summary>
    [SerializeField]
    private float mfLabelRange = 1500f;

    private UWLevelLoader mOLoader;

    private UWPaletteRenderToggle mOPalette;

    private UWControlScheme mOScheme;

    private bool mbJumpOpen;

    private static UWDebugTools msInstance;

    /// <summary>Is one of the input lines open? Then the special keys stay silent, see
    /// UWControls.IsTextEntryActive.</summary>
    public static bool IsTyping => msInstance != null && (msInstance.mbJumpOpen || msInstance.mbSpellOpen);

    private string msJumpText = string.Empty;

    private GUIStyle mOStyle;

    private GUIStyle mOLabelStyle;

    private const string JumpControlName = "UWJump";

    public void Init(UWLevelLoader pOLoader)
    {
        msInstance = this;
        mOLoader = pOLoader;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // While typing in another input, F7 and F10 stay silent (UWControls.IsTextEntryActive);
        // they still close their own lines.
        bool lbOtherTyping = UWControls.IsTextEntryActive && !mbJumpOpen && !mbSpellOpen;

        // Shift+F7 and Shift+F10 since 2026-09-26: plain F7 turns the panel and F10 makes camp,
        // as in the original.
        if (Keyboard.current.f7Key.wasPressedThisFrame && UWControls.IsShiftHeld && !lbOtherTyping)
        {
            fSetModal(!mbJumpOpen, ref mbJumpOpen);
        }

        if (Keyboard.current.f10Key.wasPressedThisFrame && UWControls.IsShiftHeld && !lbOtherTyping)
        {
            mbJumpOpen = false;
            fSetModal(!mbSpellOpen, ref mbSpellOpen);
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (mbSpellOpen)
                fSetModal(false, ref mbSpellOpen);

            if (mbJumpOpen)
                fSetModal(false, ref mbJumpOpen);
        }
    }

    /// <summary>
    /// Opens or closes the input line.
    ///
    /// All controls are switched off and on again in the process. Merely releasing the
    /// pointer is not enough: movement reads its actions directly, the character
    /// would keep walking while typing.
    /// </summary>
    private void fSetJumpOpen(bool pbOpen)
    {
        fSetModal(pbOpen, ref mbJumpOpen);
    }

    private void fSetModal(bool pbOpen, ref bool pbFlag)
    {
        pbFlag = pbOpen;

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

    private void OnGUI()
    {
        if (mOLoader == null)
            return;

        fEnsureStyles();

        if (fIsFullBright())
            fDrawInvisibleObjects();

        if (mbJumpOpen)
            fDrawJumpBox();

        if (mbSpellOpen)
            fDrawSpellBox();
    }

    private bool mbSpellOpen;

    private Vector2 mOSpellScroll;

    private string msSpellFilter = string.Empty;

    /// <summary>
    /// Spell selection for testing (F10).
    ///
    /// Casting goes through UWGameUI.CastSpellFromObject - the same path a
    /// wand takes: WITHOUT spell circle, without mana and without skill check. The
    /// reference calls it CastRunicSpellWithoutRules.
    ///
    /// A projectile spell is NOT sent off immediately but, as with rune
    /// casting, waits for the click that gives the direction; a class 7 spell waits for
    /// its target. Otherwise neither could be tested.
    /// </summary>
    private void fDrawSpellBox()
    {
        if (mOLoader.UWDataImporter == null)
            return;

        float lfWidth = 420f;
        float lfHeight = 420f;

        Rect lORect = new Rect((Screen.width - lfWidth) * 0.5f, 40f, lfWidth, lfHeight);

        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        GUI.DrawTexture(lORect, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(lORect.x + 12f, lORect.y + 8f, lORect.width - 24f, lORect.height - 16f));

        GUILayout.Label("Cast spell - without circle, mana and check. [Esc] closes.");

        GUILayout.BeginHorizontal();
        GUILayout.Label("Search:", GUILayout.Width(50f));
        msSpellFilter = GUILayout.TextField(msSpellFilter);
        GUILayout.EndHorizontal();

        mOSpellScroll = GUILayout.BeginScrollView(mOSpellScroll);

        for (int liAt = 0; liAt < UWRunicMagic.AllSpells.Count; liAt++)
        {
            string lsLabel = fDescribeSpell(liAt);

            if (!string.IsNullOrEmpty(msSpellFilter)
                && lsLabel.IndexOf(msSpellFilter, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (!GUILayout.Button(lsLabel, GUILayout.Height(22f)))
                continue;

            fSetModal(false, ref mbSpellOpen);

            UWGameUI lOUi = mOLoader.GetComponent<UWGameUI>();

            if (lOUi == null)
                lOUi = UWScene.GameUi;

            if (lOUi != null)
                lOUi.CastSpellFromObject(liAt, false);

            break;
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    /// <summary>Number, name, class and rune sequence - so the spell can be found again
    /// even when the name is missing.</summary>
    private string fDescribeSpell(int piIndex)
    {
        UWRunicMagic.Spell lOSpell = UWRunicMagic.AllSpells[piIndex];

        string lsName = UWRunicMagic.GetName(lOSpell, mOLoader.UWDataImporter.Strings);

        if (string.IsNullOrWhiteSpace(lsName))
            lsName = "(no name)";

        return string.Format("{0,2}  {1,-28} Class {2}-{3:X}  Circle {4}  {5}",
            piIndex, lsName, lOSpell.MajorClass, lOSpell.MinorClass & 0xF, lOSpell.Level,
            UWRunicMagic.DescribeRunes(lOSpell.RuneSequence));
    }

    private bool fIsFullBright()
    {
        if (mOPalette == null)
            mOPalette = mOLoader.GetComponent<UWPaletteRenderToggle>();

        return mOPalette != null && mOPalette.FullBright;
    }

    private void fEnsureStyles()
    {
        if (mOStyle != null)
            return;

        mOStyle = new GUIStyle(GUI.skin.textField);
        mOStyle.fontSize = 18;

        mOLabelStyle = new GUIStyle(GUI.skin.label);
        mOLabelStyle.fontSize = 11;
        mOLabelStyle.alignment = TextAnchor.MiddleCenter;
        mOLabelStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
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

                Vector2 lOAt = new Vector2(lOScreen.x, Screen.height - lOScreen.y);

                fDrawTargetLine(lOCamera, lOObject, lOAt);

                Rect lORect = new Rect(lOAt.x - 90f, lOAt.y - 16f, 180f, 32f);

                GUI.Label(lORect, lsText, mOLabelStyle);
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

        if (liTileX < 0 || liTileX >= UWLevelMeshBuilder.TilesPerAxis
            || liTileY < 0 || liTileY >= UWLevelMeshBuilder.TilesPerAxis)
            return;

        UWTile lOTarget = mOLoader.CurrentLevel.TileData[(liTileY * UWLevelMeshBuilder.TilesPerAxis) + liTileX];

        if (lOTarget == null)
            return;

        Vector3 lOWorld = new Vector3(
            liTileX * UWLevelMeshBuilder.TileSpacing,
            lOTarget.FloorHeight + UWLevelMeshBuilder.TileHalfSize,
            liTileY * UWLevelMeshBuilder.TileSpacing);

        Vector3 lOScreen = pOCamera.WorldToScreenPoint(lOWorld);

        if (lOScreen.z <= 0f)
            return;

        fDrawLine(pOFrom, new Vector2(lOScreen.x, Screen.height - lOScreen.y));
    }

    /// <summary>
    /// A line in the UI.
    ///
    /// IMGUI can only draw rectangles, so a thin strip is rotated. The
    /// detour via GL drawing would depend on the render pipeline; this works
    /// the same everywhere.
    /// </summary>
    private void fDrawLine(Vector2 pOFrom, Vector2 pOTo)
    {
        Vector2 lODelta = pOTo - pOFrom;
        float lfLength = lODelta.magnitude;

        if (lfLength < 1f)
            return;

        Matrix4x4 lOPrevious = GUI.matrix;
        Color lOColour = GUI.color;

        GUI.color = new Color(1f, 0.85f, 0.3f, 0.5f);

        GUIUtility.RotateAroundPivot(Mathf.Atan2(lODelta.y, lODelta.x) * Mathf.Rad2Deg, pOFrom);
        GUI.DrawTexture(new Rect(pOFrom.x, pOFrom.y - 1f, lfLength, 2f), Texture2D.whiteTexture);

        GUI.matrix = lOPrevious;
        GUI.color = lOColour;
    }

    /// <summary>In noclip mode the player camera is switched off - then the spectator
    /// camera draws. Same approach as in UWDebugOverlay.</summary>
    private static Camera fGetActiveCamera()
    {
        return UWScene.ActiveCamera;
    }

    private void fDrawJumpBox()
    {
        Rect lORect = new Rect((Screen.width * 0.5f) - 180f, 60f, 360f, 34f);

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(lORect.x - 8f, lORect.y - 26f, lORect.width + 16f, lORect.height + 34f),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(lORect.x, lORect.y - 24f, lORect.width, 22f),
            "Jump:  level,X,Y     [Enter] go   [Esc] cancel");

        // The input MUST be caught before the field is drawn - afterwards the
        // field has already consumed the event.
        if (Event.current.type == EventType.KeyDown
            && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
        {
            fJump(msJumpText);

            Event.current.Use();

            return;
        }

        GUI.SetNextControlName(JumpControlName);

        msJumpText = GUI.TextField(lORect, msJumpText, 24, mOStyle);

        GUI.FocusControl(JumpControlName);
    }

    /// <summary>
    /// "3,45,13" means level 3, tile 45/13. Two numbers without a level ("45,13") jump
    /// within the current level.
    ///
    /// Separators are comma, semicolon, slash and space - depending on how
    /// you happened to note down the tile.
    /// </summary>
    private void fJump(string psText)
    {
        fSetJumpOpen(false);

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
            || liX < 0 || liX >= UWLevelMeshBuilder.TilesPerAxis
            || liY < 0 || liY >= UWLevelMeshBuilder.TilesPerAxis)
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

        UWTile lOTile = mOLoader.CurrentLevel.TileData[(piTileY * UWLevelMeshBuilder.TilesPerAxis) + piTileX];

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
            piTileX * UWLevelMeshBuilder.TileSpacing,
            lOTile.FloorHeight + UWLevelMeshBuilder.TileHalfSize,
            piTileY * UWLevelMeshBuilder.TileSpacing));
    }

}
