using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UWDataImport.UWData;
using UWDataImport;
using System.Collections.Generic;

/// <summary>
/// The classic original HUD's owner and the game's interface root: the canvas with the frame
/// and its bars, Init/Start/Update, the keys both schemes route here, and one object per concern
/// (cleanup 5.17 and 5.18, 2026-10-23): the classic HUD parts (UWClassicHud: Runes, Map, Panel,
/// Compass, Inventory, Options, Pictures, MessageLog, Conversation, Commands, the dragons, the
/// paper doll and the flasks), the classic screen's fit (UWClassicScreen), the view camera
/// (UWViewportCamera), the pointer zones (UWPointerZones), the pointer's picture
/// (UWPointerCursor), the typing locks (UWTextEntryState), the weapon sway (UWWeaponSway), the
/// game actions (UWGameActions), the save, load and quit flows (UWSaveFlow).
///
/// Runs on a UGUI canvas built at runtime (Image components under a
/// CanvasScaler with a fixed reference resolution of 320x200 - exactly the original resolution of
/// UW1) instead of OnGUI/IMGUI. Every original pixel coordinate is thereby taken over unchanged
/// as a RectTransform position, without the earlier manual
/// scale/liSizeToFill calculation - the CanvasScaler scales and centres automatically.
/// IMGUI, on the other hand, remained impractical for everything still to come (inventory drag&drop,
/// rune shelf): no editor visibility, no hover/drag support.
///
/// The pointer zone logic (UWPointerZones) independently stays in real
/// screen pixels, since it evaluates the real mouse position - see UWClassicScreen.
///
/// TAKEN APART on 2026-09-18 (7,633 lines were one class): one object per section -
/// UWHudInventory, UWHudMessageLog, UWHudPanel, UWHudRunes, UWHudMap, UWHudPictures,
/// UWHudCompass, UWHudOptions, UWHudConversation - each created on first use through its
/// accessor (UWClassicHud's since cleanup 5.18; the accessors here forward). The sections reach
/// the shared services through their owner (mOUi: game data, frame, character, interaction,
/// inventory, filter mode) and one another through the accessors; the Inspector fields stay on
/// this MonoBehaviour so that scene values survive. The rest of the game calls the sections
/// directly (mOUi.Map, mOUi.Runes, ...) since cleanup 5.18 - the forwards went.
/// </summary>
public class UWGameUI : MonoBehaviour
{
    internal UWCharacter mCharacter;
    internal UWControlScheme mControlSchemeRef;

    internal DataImport mOUWData;

    private UWClassicScreen mOClassicScreen;

    /// <summary>The classic screen's fit into the window: the pixel scale, the bars, the help
    /// window's slide, every conversion between the original's pixels and the window's, the 3D
    /// view's rectangle (UWClassicScreen over the engine-free UWClassicGeometry, cleanup 5.18,
    /// 2026-10-23).</summary>
    public UWClassicScreen ClassicScreen
    {
        get { return mOClassicScreen ?? (mOClassicScreen = new UWClassicScreen(this)); }
    }

    internal Texture2D mOMainTexture;

    private bool lbIsInitialized;

    private UWPointerCursor mOPointerCursor;

    /// <summary>The pointer's picture: the original's pointers, which one shows, the scaled copies,
    /// the bags' hiding of the system pointer (UWPointerCursor, cleanup 5.17, 2026-10-23).</summary>
    internal UWPointerCursor PointerCursor
    {
        get { return mOPointerCursor ?? (mOPointerCursor = new UWPointerCursor(this)); }
    }

    private UWGameActions mOGameActions;

    /// <summary>One entry per game action the keys and buttons of both schemes start (camp, track,
    /// cast, status; UWGameActions, cleanup 5.17, 2026-10-23).</summary>
    internal UWGameActions Actions
    {
        get { return mOGameActions ?? (mOGameActions = new UWGameActions(this)); }
    }

    public Texture2D UWBackground;

    public FilterMode TextureFilterMode = FilterMode.Point;

    private UWViewportCamera mOViewport;

    /// <summary>The view camera's rectangle, aspect and field of view per scheme (UWViewportCamera,
    /// cleanup 5.18, 2026-10-23), written every Update before the screen shake.</summary>
    internal UWViewportCamera Viewport
    {
        get { return mOViewport ?? (mOViewport = new UWViewportCamera(this)); }
    }

    private UWPointerZones mOPointerZones;

    /// <summary>The classic scheme's pointer-driven movement: the zones of the view that mirror
    /// UW.EXE, the clamp of the pointer to the view during a held movement, the one position
    /// everyone draws the pointer at (UWPointerZones, cleanup 5.18, 2026-10-23).</summary>
    public UWPointerZones PointerZones
    {
        get { return mOPointerZones ?? (mOPointerZones = new UWPointerZones(this)); }
    }

    private UWTextEntryState mOTextEntry;

    /// <summary>Who has the keys while text is typed - the one query for every lock (UWTextEntryState,
    /// cleanup 5.18, 2026-10-23).</summary>
    public UWTextEntryState TextEntry
    {
        get { return mOTextEntry ?? (mOTextEntry = new UWTextEntryState(this)); }
    }

    private UWWeaponSway mOWeaponSway;

    /// <summary>The weapon picture's sway with the movement, one state for the classic frame and
    /// the modern weapon view (UWWeaponSway, cleanup 5.18, 2026-10-23).</summary>
    internal UWWeaponSway WeaponSway
    {
        get { return mOWeaponSway ?? (mOWeaponSway = new UWWeaponSway(this)); }
    }

    private UWClassicHud mOClassic;

    /// <summary>The classic HUD's parts, their creation and update order, the paper doll, the
    /// dragons and the frame's picture for the width (UWClassicHud, cleanup 5.18, 2026-10-23).</summary>
    public UWClassicHud Classic
    {
        get { return mOClassic ?? (mOClassic = new UWClassicHud(this)); }
    }

    /// <summary>The interaction on this object, found on first use - the sections and the text
    /// entry state ask here before Interaction has introduced itself.</summary>
    internal Interaction fFindInteraction()
    {
        if (mOInteraction == null)
            mOInteraction = GetComponent<Interaction>();

        return mOInteraction;
    }

    /// <summary>Is a projectile spell currently waiting for its target? As long as it is, the left
    /// button does not drive movement but fires the shot.</summary>
    public bool IsSpellTargeting
    {
        get { return mOInteraction != null && mOInteraction.IsSpellTargeting; }
    }

    // Canvas hierarchy (built once, see fBuildCanvas()).
    internal GameObject mCanvasRoot;
    internal RectTransform mGameFrame;
    private RectTransform mBackdropLeftRect;
    private RectTransform mBackdropRightRect;

    /// <summary>The bars over and under the frame when the window is narrower than it (UWUiFit).
    /// </summary>
    private RectTransform mBackdropTopRect;

    private RectTransform mBackdropBottomRect;
    internal Image mBackgroundImage;
    internal Image mMainFrameImage;

    internal Interaction mOInteraction;

    internal UWInventory mOInventory;

    public void Init(DataImport pOUWData)
    {
        mOUWData = pOUWData;

        Runes.fLoadSelectedRunes();
        var lOUWMain = mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.MAIN, 0);
        mOMainTexture = UWTextureFactory.FromTexture(lOUWMain, TextureFilterMode, "UWGameUI main screen");

        PointerCursor.Load(mOUWData, TextureFilterMode);

        PointerZones.ResetLastPositionToViewCentre();

        if (mCanvasRoot == null)
            fInstantiateCanvas();

        mBackgroundImage.sprite = fToSprite(UWBackground);
        mMainFrameImage.sprite = fToSprite(mOMainTexture);

        Inventory.LoadSprites();
        MessageLog.LoadSprites();

        lbIsInitialized = true;

        // ONLY here: the runes from the save game have been in the list since Init, but their
        // pictures only exist once the interface is built. Without this they were
        // there but could not be seen (per user, 2026-09-05).
        Runes.fRefreshSelectedRunes();
    }

	void Start ()
    {
        mCharacter = GetComponentInParent<UWCharacter>();
        mControlSchemeRef = GetComponentInParent<UWControlScheme>();
        PointerZones.Attach(GetComponentInParent<UWPlayerMovement>());
        lbIsInitialized = false;

        // No manual wiring in the scene needed - same self-creation pattern
        // as UWInventory in Interaction.Awake().
        mOInventory = GetComponent<UWInventory>();

        if (mOInventory == null)
            mOInventory = gameObject.AddComponent<UWInventory>();

        // The help window (F1), the same self-creation.
        if (GetComponent<UWHelpWindow>() == null)
            gameObject.AddComponent<UWHelpWindow>();

        Inventory.mBackpackItemsShown = new UWObject[UWHudInventory.BackpackSlotCount];
        Inventory.mBackpackSourcesShown = new UWTexture[UWHudInventory.BackpackSlotCount];
        Inventory.mEquipIconItemsShown = new UWObject[UWHudInventory.ArmorHitZoneSlots.Length - UWHudInventory.ArmorSlotCount];
        Inventory.mEquipIconSourcesShown = new UWTexture[UWHudInventory.ArmorHitZoneSlots.Length - UWHudInventory.ArmorSlotCount];
        Inventory.mContainerItemsShown = new UWObject[UWHudInventory.ContainerSlotCount];
        Inventory.mContainerSourcesShown = new UWTexture[UWHudInventory.ContainerSlotCount];

        // UWGameUI sits on the same GameObject as Camera.main (see UWLevelLoader.
        // fInitialiseUi) - the camera is UWViewportCamera's from here on.
        Viewport.Attach(GetComponent<Camera>());

        // The modern scheme's HUD and game menu - idle in the original scheme.
        if (GetComponent<UWModernHud>() == null)
            gameObject.AddComponent<UWModernHud>();

        if (GetComponent<UWModernBags>() == null)
            gameObject.AddComponent<UWModernBags>();

        if (GetComponent<UWModernPanel>() == null)
            gameObject.AddComponent<UWModernPanel>();

        if (GetComponent<UWModernMinimap>() == null)
            gameObject.AddComponent<UWModernMinimap>();

        if (GetComponent<UWModernActionBar>() == null)
            gameObject.AddComponent<UWModernActionBar>();

        if (GetComponent<UWModernRunePanel>() == null)
            gameObject.AddComponent<UWModernRunePanel>();

        if (GetComponent<UWModernQuestion>() == null)
            gameObject.AddComponent<UWModernQuestion>();

        if (GetComponent<UWModernConversation>() == null)
            gameObject.AddComponent<UWModernConversation>();

        if (GetComponent<UWModernLayoutEditor>() == null)
            gameObject.AddComponent<UWModernLayoutEditor>();
    }

	// Update is called once per frame
	void Update ()
    {
        if (!lbIsInitialized)
            return;

        // The key lock while typing, for all actions - see UWControls.IsTextEntryActive.
        UWControls.SetTextEntryActive(TextEntry.LocksKeys);

        fCheckFontToggle();

        Runes.fRefreshSpellIconsIfChanged();

        // In Modern the 3D view should fill the whole screen instead of disappearing behind the
        // small original window with black bars. The original window
        // including paper doll belongs to the original scheme, not to the modern controls.
        //
        // THE BIG MAP is the exception (per user, 2026-10-04): in the original it fills the whole
        // screen, and it lives on the classic frame - opened in the modern scheme the frame shows
        // under it, 4:3 with dark bars, and everything works as there (notes, eraser, levels,
        // the quill); the modern UI hides meanwhile (UWModernHud.fIsCovered).
        bool lbShowClassicUi = mControlSchemeRef == null || !UWControlScheme.Ui.Policy.ClassicScreenOnlyUnderMap
            || Map.IsMapVisible;

        // THE CLASSIC FRAME WIDENED (UWClassicWide): not over the conversation frame or the big
        // map; with the help window only as wide as leaves it room.
        UWClassicWide.Update(mControlSchemeRef == null || !UWControlScheme.Ui.Policy.ClassicScreenOnlyUnderMap,
            Conversation.mbConversationFrame || Map.IsMapVisible);

        mCanvasRoot.SetActive(lbShowClassicUi);

        if (!lbShowClassicUi)
        {
            Viewport.Apply(false);

            // The modern scheme's pointer: the cross, the target pointer, the layout editor's
            // arrows (UWPointerCursor).
            PointerCursor.ApplyModern(mOInteraction);

            return;
        }

        Viewport.Apply(true);

        // Bar width depends on the aspect ratio - the CanvasScaler already delivers the
        // actual canvas width in the same reference units as GameFrame.
        mGameFrame.localScale = new Vector3(UWClassicScreen.HorizontalPixelFactor, 1f, 1f);
        mGameFrame.sizeDelta = new Vector2(UWUiFit.FrameWidthUnits + UWClassicWide.Extra, UWUiFit.FrameHeight);
        Classic.ApplyWideFrame();

        // The frame slides left while the help window is open (UWHelpLayout): the left bar
        // shrinks by what the right one gains.
        float lfCanvasWidth = UWUiFit.CanvasWidth;
        float lfShift = UWHelpLayout.FrameShift(lfCanvasWidth);
        float lfMarginWidth = ClassicScreen.fGetLetterboxMarginRefUnits();
        float lfRightMargin = Mathf.Max(0f, lfCanvasWidth - lfMarginWidth - (mGameFrame.sizeDelta.x * UWClassicScreen.HorizontalPixelFactor));

        mGameFrame.anchoredPosition = new Vector2(lfShift, 0f);
        mBackdropLeftRect.sizeDelta = new Vector2(lfMarginWidth, mBackdropLeftRect.sizeDelta.y);
        mBackdropRightRect.sizeDelta = new Vector2(lfRightMargin, mBackdropRightRect.sizeDelta.y);

        // A window narrower than the frame fits it by the width (UWUiFit): bars at the top and
        // the bottom instead.
        float lfVerticalMargin = Mathf.Max(0f, (UWUiFit.CanvasHeight - UWUiFit.FrameHeight) / 2f);

        mBackdropTopRect.sizeDelta = new Vector2(mBackdropTopRect.sizeDelta.x, lfVerticalMargin);
        mBackdropBottomRect.sizeDelta = new Vector2(mBackdropBottomRect.sizeDelta.x, lfVerticalMargin);

        Classic.Update();

        PointerZones.Update();

        // The pointer's picture: the movement arrows, the red X, the cross, the quill and the
        // eraser, the target pointer (UWPointerCursor).
        PointerCursor.ApplyClassic(mOInteraction);
	}

    /// <summary>
    /// Builds the classic UI canvas in code (fBuildCanvas) and adds the parts built at
    /// runtime (compass, dragons, options menu: UWClassicHud.BuildAfterCanvas, with the history
    /// of the prefab this replaced).
    /// </summary>
    private void fInstantiateCanvas()
    {
        fBuildCanvas();
        mCanvasRoot.name = "Classic UI Canvas";

        Classic.BuildAfterCanvas();
    }

    /// <summary>
    /// Builds the canvas hierarchy once. The order of the children matches the
    /// drawing order of the earlier OnGUI: weapon behind the frame picture (so that in the
    /// original window it shines through its transparent centre, but is cut off at the edge by the
    /// frame), frame behind the paper doll.
    /// </summary>
    private void fBuildCanvas()
    {
        // Fitted whole into the window - by the height, by the width when narrow (UWUiFit).
        mCanvasRoot = UWUiKit.CreateClassicCanvas(transform, "Classic UI Canvas", UWUiSorting.Classic).gameObject;

        RectTransform lOCanvasRect = (RectTransform)mCanvasRoot.transform;

        // Two narrow bars left/right of the 320 wide frame, not one full
        // area - otherwise the actual 3D picture behind the transparent centre
        // of MainFrame would be covered. Width is tracked in Update() from the actual
        // canvas width (in reference units), since it depends on the aspect
        // ratio.
        Image lOBackdropLeft = UWUiKit.CreateFrameImage(lOCanvasRect, "BackdropLeft", 0f, 0f, 0f, 0f, Color.black);
        mBackdropLeftRect = (RectTransform)lOBackdropLeft.transform;
        mBackdropLeftRect.anchorMin = new Vector2(0f, 0f);
        mBackdropLeftRect.anchorMax = new Vector2(0f, 1f);
        mBackdropLeftRect.pivot = new Vector2(0f, 0.5f);
        mBackdropLeftRect.anchoredPosition = Vector2.zero;
        mBackdropLeftRect.offsetMin = new Vector2(0f, 0f);
        mBackdropLeftRect.offsetMax = new Vector2(0f, 0f);

        Image lOBackdropRight = UWUiKit.CreateFrameImage(lOCanvasRect, "BackdropRight", 0f, 0f, 0f, 0f, Color.black);
        mBackdropRightRect = (RectTransform)lOBackdropRight.transform;
        mBackdropRightRect.anchorMin = new Vector2(1f, 0f);
        mBackdropRightRect.anchorMax = new Vector2(1f, 1f);
        mBackdropRightRect.pivot = new Vector2(1f, 0.5f);
        mBackdropRightRect.anchoredPosition = Vector2.zero;
        mBackdropRightRect.offsetMin = new Vector2(0f, 0f);
        mBackdropRightRect.offsetMax = new Vector2(0f, 0f);

        mBackdropTopRect = fCreateBar("BackdropTop", lOCanvasRect, 1f);
        mBackdropBottomRect = fCreateBar("BackdropBottom", lOCanvasRect, 0f);

        GameObject lOFrameObj = new GameObject("GameFrame", typeof(RectTransform));
        lOFrameObj.transform.SetParent(lOCanvasRect, false);
        mGameFrame = (RectTransform)lOFrameObj.transform;
        mGameFrame.anchorMin = new Vector2(0.5f, 0.5f);
        mGameFrame.anchorMax = new Vector2(0.5f, 0.5f);
        mGameFrame.pivot = new Vector2(0.5f, 0.5f);
        mGameFrame.sizeDelta = new Vector2(320f, 200f);
        mGameFrame.anchoredPosition = Vector2.zero;

        Classic.BuildWeapon();

        mBackgroundImage = UWUiKit.CreateFrameImage(mGameFrame, "Background", 0f, 0f, 320f, 200f, Color.white);
        UWUiKit.Stretch(mBackgroundImage.rectTransform);

        Classic.BuildWindowPicture();

        mMainFrameImage = UWUiKit.CreateFrameImage(mGameFrame, "MainFrame", 0f, 0f, 320f, 200f, Color.white);
        UWUiKit.Stretch(mMainFrameImage.rectTransform);

        Classic.BuildPaperdollAndPanels();
    }

    /// <summary>
    /// The two keys of our own: F switches all fonts between original and modern (see
    /// UWTextLabel), M opens the map. Both come from the Player map since 2026-09-17, so they can
    /// be changed in the menu bar (UWKeyBindings). Not while typing anywhere - map note, save
    /// game name, mantra, input line - there the letters belong to the text.
    /// </summary>
    private void fCheckFontToggle()
    {
        UWControls lOControls = mControlSchemeRef != null ? mControlSchemeRef.Controls : null;

        if (lOControls == null)
            return;

        if (mOInteraction == null)
            mOInteraction = GetComponent<Interaction>();

        // In the main menu and in creation the keys stay completely silent (per user,
        // 2026-09-11), and while a sequence plays - or the key that ended it is still down in
        // this frame (UWIntroPlayer.HoldsKeys, 2026-10-13).
        if (TextEntry.IsTextEntryActive || UWScreenUi.IsScreenMenuOpen
            || (UWScene.IntroPlayer != null && UWScene.IntroPlayer.HoldsKeys))
            return;

        // Ctrl+F and Ctrl+M are the original's option shortcuts (sound and music, with Ctrl+S,
        // R, D and Q; per user in the original, 2026-09-26: it is Ctrl, Alt does nothing) - not
        // ours.
        bool lbCtrl = UWControls.IsCtrlHeld;

        if (!lbCtrl && lOControls.Player.ToggleFont.WasPressedThisFrame())
            UWTextLabel.UseModernFont = !UWTextLabel.UseModernFont;

        // THE MODERN SCHEME has no classic frame: the options, the map and the command icons live
        // on it, and opened there they were invisible and held the game (found 2026-10-03). There
        // F1, Escape and the Ctrl shortcuts open the modern game menu (UWModernGameMenu); the map, the
        // command keys and the rune shelf wait for their modern counterparts; F9 and F10 stay.
        if (mControlSchemeRef != null && UWControlScheme.IsModern)
        {
            fCheckModernKeys(lOControls);
            return;
        }

        // Not in a conversation: using the map is locked there, so the key (our addition) is too.
        // The gamepad's pointer takes the d-pad as its wheel - except to close the map again.
        if (!lbCtrl && lOControls.Player.ToggleMap.WasPerformedThisFrame() && !UWConversationController.IsAnyOpen
            && (Map.IsMapVisible || !UWGamepadPointer.Takes(lOControls.Player.ToggleMap)))
            fToggleMapByKey();

        fCheckOptionShortcuts(lOControls);
        fCheckOriginalKeys(lOControls);
    }

    /// <summary>The keys of the modern scheme that UWGameUI routes - see fCheckFontToggle.</summary>
    private void fCheckModernKeys(UWControls pOControls)
    {
        UWModernHud lOHud = UWModernHud.Instance;

        if (lOHud == null || UWConversationController.IsAnyOpen || UWRoamingSight.IsAnyRunning
            || UWRoamingSight.EndedByEscapeThisFrame || mOInteraction == null)
            return;

        UWControls.PlayerActions lOPlayer = pOControls.Player;

        // The big map open: M or Escape close it (writing a note, the keys are the note's).
        if (Map.IsMapVisible)
        {
            // The gamepad's B closes it too (UWGamepad).
            if (!UWControls.IsCtrlHeld && (lOPlayer.ToggleMap.WasPerformedThisFrame() || lOPlayer.Menu.WasPressedThisFrame()
                || (lOPlayer.PadBack.WasPressedThisFrame() && !UWLetterGrid.IsShown)))
                Map.HideMap();

            return;
        }

        // THE RIGHT CTRL ONLY: the left one sinks in the modern scheme (per user, 2026-10-04) -
        // held while walking back it would open the save page with S, with R the restore page.
        if (UWControls.IsRightCtrlHeld && !UWControls.IsShiftHeld && !UWControls.IsAltHeld)
        {
            if (lOPlayer.OptSave.WasPressedThisFrame())
                lOHud.Menu.OpenMenu(UWModernGameMenu.PageEnum.Save);
            else if (lOPlayer.OptRestore.WasPressedThisFrame())
                lOHud.Menu.OpenMenu(UWModernGameMenu.PageEnum.Load);
            else if (lOPlayer.OptMusic.WasPressedThisFrame() || lOPlayer.OptSound.WasPressedThisFrame()
                || lOPlayer.OptDetail.WasPressedThisFrame() || lOPlayer.OptQuit.WasPressedThisFrame())
                lOHud.Menu.OpenMenu(UWModernGameMenu.PageEnum.Main);

            return;
        }

        if (UWControls.IsShiftHeld || UWControls.IsAltHeld)
            return;

        // Escape first lets a waiting spell go (UWHudRunes.fAfterModernCast), then closes the bags
        // (UWModernBags), the character panel (UWModernPanel; a pinned one only leaves its Help
        // tab) and the rune panel (UWModernRunePanel); the menu comes after. The order is
        // UWModernUi's closable chain since 2026-10-22, one list for Escape and PadBackCloses.

        // The gamepad's B does what Escape does while something is open (PadBackCloses);
        // otherwise it jumps (UWPlayerInput).
        bool lbBack = lOPlayer.Menu.WasPressedThisFrame()
            || (lOPlayer.PadBack.WasPressedThisFrame() && !UWLetterGrid.IsShown && !UWModernBags.IsMenuOpen && !UWModernBags.IsSplitting
                && PadBackCloses());

        if (lbBack && !lOHud.IsOpen && UWModernUi.CloseNext())
        {
            // Something of the chain was closed; the menu waits for the next press.
        }
        else if (lOPlayer.KeyOptions.WasPressedThisFrame() || lbBack)
            lOHud.Menu.ToggleMenu();
        else if (lOHud.IsOpen)
            return;
        else if (lOPlayer.KeyTrack.WasPressedThisFrame())
            Actions.Track();
        else if (lOPlayer.KeyCamp.WasPressedThisFrame() && UWGameActions.CanCamp)
            Actions.Camp();
        else if (lOPlayer.ToggleMap.WasPerformedThisFrame() && !UWGamepadPointer.Takes(lOPlayer.ToggleMap))
            fToggleMapByKey();
    }

    /// <summary>The modern minimap's click (UWModernMinimap): the big map, with the map carried.</summary>
    public bool OpenBigMap()
    {
        if (!IsCarryingMap || Map.IsMapVisible)
            return false;

        fToggleMapByKey();
        return Map.IsMapVisible;
    }

    /// <summary>
    /// The original's Ctrl shortcuts (built 2026-09-26, see UWHudOptions.ChooseByShortcut):
    /// Ctrl+S save, Ctrl+R restore, Ctrl+M music, Ctrl+F sound, Ctrl+D detail, Ctrl+Q quit.
    /// Normal play only, as the original registers them. Partway through an action the options
    /// refuse to open (UWHudOptions.RefusesToOpen); on level 9 they open, only the Save entry
    /// refuses (RefusesToSave).
    /// </summary>
    private void fCheckOptionShortcuts(UWControls pOControls)
    {
        if (!UWControls.IsCtrlHeld || UWControls.IsShiftHeld || UWControls.IsAltHeld)
            return;

        // Not while the view roams: the options are locked then, as in the original.
        if (UWConversationController.IsAnyOpen || Map.IsMapVisible || Options.IsOpen || mOInteraction == null
            || UWRoamingSight.IsAnyRunning)
            return;

        UWControls.PlayerActions lOPlayer = pOControls.Player;
        int liEntry = lOPlayer.OptSave.WasPressedThisFrame() ? UWOptionsMenuModel.TopSave
            : lOPlayer.OptRestore.WasPressedThisFrame() ? UWOptionsMenuModel.TopRestore
            : lOPlayer.OptMusic.WasPressedThisFrame() ? UWOptionsMenuModel.TopMusic
            : lOPlayer.OptSound.WasPressedThisFrame() ? UWOptionsMenuModel.TopSound
            : lOPlayer.OptDetail.WasPressedThisFrame() ? UWOptionsMenuModel.TopDetail
            : lOPlayer.OptQuit.WasPressedThisFrame() ? UWOptionsMenuModel.TopQuit
            : -1;

        if (liEntry < 0)
            return;

        // "Partway" and "between worlds" are refused by the options themselves.
        Options.ChooseByShortcut(liEntry);
    }

    private int miPadBackFrame = -1;

    private bool mbPadBackCloses;

    /// <summary>
    /// The gamepad's B (UWGamepad): whether it closes something - the menu, a waiting spell, the
    /// use mode, the bags, a closable panel - in the modern scheme. Worked out once a frame, before
    /// anything closes, so the jump (UWPlayerInput) sees the same answer whichever runs first.
    /// </summary>
    public bool PadBackCloses()
    {
        if (miPadBackFrame == Time.frameCount)
            return mbPadBackCloses;

        miPadBackFrame = Time.frameCount;

        // The menu, the big map, or a link of UWModernUi's closable chain (the waiting spell, the
        // bags' use mode, the bags, the closable panels) - the same list Escape closes from
        // (UWModernUiScheme.PadBackCloses; nothing in the classic scheme).
        mbPadBackCloses = mControlSchemeRef != null && UWControlScheme.Ui.PadBackCloses;

        return mbPadBackCloses;
    }

    /// <summary>
    /// THE ORIGINAL'S FUNCTION KEYS (built 2026-09-26, see UWControls.KeyOptions): F1 options,
    /// F2 to F6 the command modes as a click on their icon, F7 turns the panel, F8 casts, F9
    /// tracks (UWMiscSpellRules.Track, since 2026-09-27), F10 makes camp. Not with Shift (our own keys) nor Alt (Alt+F4) nor Ctrl; not in a
    /// conversation - the original registers them for its normal mode only -, not over the
    /// map and not while the options panel is up.
    /// </summary>
    private void fCheckOriginalKeys(UWControls pOControls)
    {
        if (UWControls.IsShiftHeld || UWControls.IsAltHeld || UWControls.IsCtrlHeld)
            return;

        if (UWConversationController.IsAnyOpen || Map.IsMapVisible || mOInteraction == null)
            return;

        UWControls.PlayerActions lOPlayer = pOControls.Player;

        // WHILE THE VIEW ROAMS only F7 turns the panel - in the original too, as does the chain
        // (per user, 2026-09-27: F1 to F6 and F8 are refused there). F9 and F10 are refused as a
        // DELIBERATE DEVIATION (decided per user the same day): the original still tracks and
        // makes camp, and sleeping leaves the spell gone from the bar but the view stuck.
        if (UWRoamingSight.IsAnyRunning)
        {
            if (lOPlayer.KeyPanel.WasPressedThisFrame())
                Panel.TogglePanelSide();

            return;
        }

        if (lOPlayer.KeyOptions.WasPressedThisFrame())
        {
            Options.OpenByKey();
            return;
        }

        if (Options.IsOpen)
            return;

        if (lOPlayer.KeyTalk.WasPressedThisFrame())
            mOInteraction.SetCommandMode(UWCommandMode.Talk);
        else if (lOPlayer.KeyGet.WasPressedThisFrame())
            mOInteraction.SetCommandMode(UWCommandMode.Get);
        else if (lOPlayer.KeyLook.WasPressedThisFrame())
            mOInteraction.SetCommandMode(UWCommandMode.Look);
        else if (lOPlayer.KeyFight.WasPressedThisFrame())
            mOInteraction.SetCommandMode(UWCommandMode.Fight);
        else if (lOPlayer.KeyUse.WasPressedThisFrame())
            mOInteraction.SetCommandMode(UWCommandMode.Use);
        else if (lOPlayer.KeyPanel.WasPressedThisFrame())
            Panel.TogglePanelSide();
        else if (lOPlayer.KeyCast.WasPressedThisFrame())
            Actions.CastShelf();
        else if (lOPlayer.KeyTrack.WasPressedThisFrame())
            Actions.Track();
        else if (lOPlayer.KeyCamp.WasPressedThisFrame() && UWGameActions.CanCamp)
            Actions.Camp();
    }

    /// <summary>
    /// The map key (M by default): closes the map when it is up, otherwise opens it - but only
    /// if the character really carries a map (object 315), just as it can otherwise only be
    /// opened by using one (per user, 2026-09-17). The original has no key for it.
    /// </summary>
    private void fToggleMapByKey()
    {
        if (Map.mMapBackgroundImage != null && Map.mMapBackgroundImage.gameObject.activeSelf)
        {
            Map.HideMap();

            return;
        }

        if (IsCarryingMap)
            Map.ShowMap(Map.CurrentMapLevelIndex);
    }

    /// <summary>Whether the character carries the map (object 315) anywhere - in a slot or
    /// inside a container. The map key and the help window's map tab ask here.</summary>
    internal bool IsCarryingMap
    {
        get
        {
            UWInventory lOInventory = GetComponent<UWInventory>();

            if (lOInventory == null)
                return false;

            foreach (UWObject lOItem in lOInventory.EnumerateAll())
            {
                if (lOItem != null && lOItem.ID == UWObjectMechanics.MapObjectId)
                    return true;
            }

            return false;
        }
    }

    internal UWControlScheme mOControlScheme;

    /// <summary>A black bar across the whole width at the top (1) or the bottom (0) of the
    /// canvas; its height is set every frame in Update.</summary>
    private static RectTransform fCreateBar(string psName, RectTransform pOParent, float pfEdge)
    {
        RectTransform lORect = (RectTransform)UWUiKit.CreateFrameImage(pOParent, psName, 0f, 0f, 0f, 0f, Color.black).transform;

        lORect.anchorMin = new Vector2(0f, pfEdge);
        lORect.anchorMax = new Vector2(1f, pfEdge);
        lORect.pivot = new Vector2(0.5f, pfEdge);
        lORect.anchoredPosition = Vector2.zero;
        lORect.sizeDelta = Vector2.zero;

        return lORect;
    }

    private void OnDestroy()
    {
        if (mOClassic != null)
            mOClassic.Release();

        if (mOPointerCursor != null)
        {
            mOPointerCursor.ReleaseScaledCursors();
            mOPointerCursor.ReleaseHeldPointer();
        }
    }

    internal static Sprite fToSprite(Texture2D pOTexture)
    {
        if (pOTexture == null)
            return null;

        return Sprite.Create(pOTexture, new Rect(0f, 0f, pOTexture.width, pOTexture.height), new Vector2(0.5f, 0.5f), 1f);
    }

    // ------------------------------------------------- The classic HUD's parts (UWClassicHud)

    /// <summary>The sections, UWClassicHud's: one accessor each, forwarding, so the sections and the
    /// rest of the game keep writing mOUi.Runes, mOUi.Map and so on.</summary>
    internal UWHudCompass Compass => Classic.Compass;

    internal UWHudConversation Conversation => Classic.Conversation;

    internal UWHudMessageLog MessageLog => Classic.MessageLog;

    internal UWHudOptions Options => Classic.Options;

    internal UWHudCommands Commands => Classic.Commands;

    internal UWHudPictures Pictures => Classic.Pictures;

    internal UWHudMap Map => Classic.Map;

    internal UWAutomapPictures AutomapPictures => Classic.AutomapPictures;

    internal UWHudInventory Inventory => Classic.Inventory;

    internal UWHudRunes Runes => Classic.Runes;

    internal UWHudPanel Panel => Classic.Panel;

    /// <summary>
    /// The lines of the message scroll IN USE NOW, which a long text pages by (decision 17, DECIDED
    /// 2026-10-20 per user; UWMessageLogModel.ShowNextPage): the classic box's five, the modern
    /// scroll's paper (UWModernScroll.Rows, three to nine), the modern message box's five
    /// (Interaction.ModernMessageCount). Asked for every page, so Shift+F2 in the middle of a text
    /// pages the rest by the new scheme's scroll. Until then the classic five held for both (B28).
    /// The scheme in force answers (IUWUiScheme.ShownLogLines since cleanup 5.19).
    /// </summary>
    public int ShownLogLines
    {
        get
        {
            if (mControlSchemeRef == null)
                return MessageLog.LogLineCount;

            return UWControlScheme.Ui.ShownLogLines;
        }
    }
}
