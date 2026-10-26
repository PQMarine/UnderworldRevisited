using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UWDataImport.UWData;
using System.Text;
using UnderworldRevisited.Build;

/// <summary>
/// Look target detection, use/attack via the right mouse button, and combat mode.
///
/// Movement and view direction live elsewhere (UWPlayerMovement, UWPlayerLook,
/// UWControlScheme) - this class only handles interaction with the world.
/// </summary>
public class Interaction : MonoBehaviour, IUWMessageSink
{
    public enum AttackEnum //TODO Relocate to weapon stats class
    {
        Idle = 0,
        Jab = 1,
        Slash = 2,
        Stab = 3,
        Missile = 4
    }

    /// <summary>How far a look reaches - the picker's range (UWWorldPicker).</summary>
    private const float LookRayRange = UWWorldPicker.Range;

    // Original: LOOKING works at any distance (LookRayRange), USING only within reach. Until
    // 2026-09-18 that reach was a distance in world units from the eye to the collider, tuned by
    // hand (48, with the pole 96, with Telekinesis 160). Since the user measured the original to
    // the eighth of a tile it is the original's own check: see UWReachRules, which also holds the
    // numbers (0x90 for the hand, 0x190 for the pole, none with Telekinesis) and the height window.
    [Header("Use (Original)")]
    [SerializeField]
    [Tooltip("On every release, logs which target was detected, whether a drag was detected and how far away it is. Debugging only.")]
    private bool mbLogUseAttempts;

    // Combat range, deliberately separate from mfUseRange: striking and using are
    // different actions in the original, and the reach of a sword swing has nothing to do
    // with the reach for a switch. Same starting value, for tuning
    // in the Inspector.
    [SerializeField]
    [Tooltip("Range of a melee attack (world units, 64 = one tile).")]
    private float mfAttackRange = 48f;

    private UWDataImport.DataImport mOUWDataImporter;
    private UWLevelLoader mLevelLoader;
    private UWGameUI mGameUi;
    private UWCharacter mCharacter;
    private UWSoundPlayer mSoundPlayer;
    private UWConversationController mConversationScreen;
    private UWEyesDisplay mEyes;

    /// <summary>The player's blow from the press to the hit frame - press, kind, ninth, charge and
    /// what the release kept (cleanup step 5.14, 2026-10-22: ten fields here until then).</summary>
    private readonly UWPlayerAttackState mOAttackState = new UWPlayerAttackState();

    /// <summary>The weapon button, the blow, the shot and the hit's pictures (cleanup step 5.14,
    /// 2026-10-22); the surface below forwards.</summary>
    private UWPlayerAttackInput mOAttackInput;

    private UWPlayerMelee mOMelee;

    private UWPlayerMissiles mOMissiles;

    private UWHitFeedback mOHitFeedback;

    private UWControlScheme mControlScheme;

    /// <summary>The message list, the running line and the paging of a long text (cleanup step
    /// 5.13, 2026-10-22); AddMessage and the rest of the surface below forward to it.</summary>
    private UWMessageLog mOLog;

    /// <summary>The mantra and the yes/no question (cleanup step 5.13, 2026-10-22).</summary>
    private UWPromptController mOPrompts;

    /// <summary>The look texts: the look at a thing, the item description, the books, the trap
    /// found on a look (cleanup step 5.13, 2026-10-22); the surface below forwards.</summary>
    private UWLookService mOLook;

    /// <summary>The spell waiting for its target - a projectile's direction or a target spell's
    /// object (cleanup step 5.14, 2026-10-22); the surface below forwards.</summary>
    private UWPendingSpell mOSpell;

    /// <summary>The classic scheme's clicks into the world and the modern scheme's keys and free
    /// pointer (cleanup step 5.14, 2026-10-22); the surface below forwards.</summary>
    private UWClassicWorldGesture mOGesture;

    private UWModernWorldInput mOModernInput;

    /// <summary>Using a thing in the world and talking to it (cleanup step 5.14, 2026-10-22); the
    /// surface below forwards.</summary>
    private UWWorldUse mOWorldUse;

    private UWInventory mInventory;

    private UWControls.PlayerActions mInput;

    /// <summary>The player's input actions, set in Start - for the classes this component ticks.</summary>
    internal UWControls.PlayerActions Input => mInput;

    /// <summary>The kind of the running blow (UWPlayerAttackState.Kind): Idle before the first.</summary>
    public AttackEnum CurrentAttack
    {
        get
        {
            switch (mOAttackState.Kind)
            {
                case AttackTypes.Hack: return AttackEnum.Jab;
                case AttackTypes.Stab: return AttackEnum.Stab;
                case AttackTypes.Slash: return AttackEnum.Slash;
                default: return AttackEnum.Idle;
            }
        }
    }

    /// <summary>The melee range set in the Inspector - for UWPlayerMelee's modern ray.</summary>
    internal float AttackRange => mfAttackRange;

    /// <summary>The eyes at the top edge - for UWHitFeedback.</summary>
    internal UWEyesDisplay Eyes => mEyes;

    /// <summary>The look texts - for UWModernWorldInput's Q over the interface and UWWorldUse's
    /// gravestone.</summary>
    internal UWLookService Look => mOLook;

    /// <summary>The message list - for UWWorldUse's inscription.</summary>
    internal UWMessageLog Log => mOLog;

    /// <summary>The mantra and the yes/no question - for UWWorldUse's shrine.</summary>
    internal UWPromptController Prompts => mOPrompts;

    /// <summary>The conversation screen beside this component - for UWWorldUse.</summary>
    internal UWConversationController ConversationScreen => mConversationScreen;

    /// <summary>The Inspector's switch that logs every use attempt - for UWWorldUse.UseTarget.</summary>
    internal bool LogUseAttempts => mbLogUseAttempts;

    /// <summary>The control scheme above this component - for UWPlayerAttackInput.</summary>
    internal UWControlScheme ControlScheme => mControlScheme;

    /// <summary>The player's inventory beside this component - for UWWorldPickup and
    /// UWItemTargetUse, which act for it.</summary>
    public UWInventory Inventory => mInventory;

    /// <summary>The level loader, the game data and the character this component works with -
    /// for UWItemTargetUse, which acts for it (cleanup step 2.4, 2026-10-12).</summary>
    public UWLevelLoader LevelLoader => mLevelLoader;

    public UWDataImport.DataImport GameData => mOUWDataImporter;

    public UWCharacter Character => mCharacter;

    /// <summary>The game UI beside this component - for UWMessageLog (the wrap and the shown lines
    /// of a paged text) and UWLookService.</summary>
    public UWGameUI GameUi => mGameUi;

    /// <summary>For UWGameUI (Original: message log in the lower frame box, see
    /// fBuildCanvas/MessageLog) - the same list that AddMessage fills (UWMessageLog.Lines).</summary>
    public IReadOnlyList<string> ActiveStrings
    {
        get { return mOLog.Lines; }
    }

    /// <summary>Messages the list keeps (UWMessageLog.HistoryCount).</summary>
    public const int MessageHistoryCount = UWMessageLog.HistoryCount;

    /// <summary>Messages the modern message box shows (UWMessageLog.ModernShownCount).</summary>
    public static int ModernMessageCount => UWMessageLog.ModernShownCount;

    public const int DefaultModernMessageCount = UWMessageLog.DefaultModernShownCount;

    /// <summary>For everything that should play an original sound effect. The mapping
    /// event -> effect is still open, see UWSoundPlayer.</summary>
    public UWSoundPlayer SoundPlayer
    {
        get { return mSoundPlayer; }
    }

    /// <summary>Is the Interact key/mouse button currently held (right mouse button or gamepad south button)?</summary>
    public bool IsInteractHeld
    {
        get { return mInput.Interact.IsPressed(); }
    }

    /// <summary>
    /// Is combat mode active?
    /// </summary>
    public bool IsCombatModeActive { get; set; }

    /// <summary>The command icon that is lit - talk, get or look; None is the default in which
    /// the mouse buttons do what was measured (see the right-button gesture). Fight is not
    /// held here, the drawn weapon is IsCombatModeActive. Rules on UWCommandMode. A MODE CHANGES
    /// ONLY THE RIGHT-BUTTON GESTURE over its kind of target (measured by the user on the
    /// original, 2026-09-19, for talk): the left button keeps walking, the cursor keeps its
    /// look, and over anything else the gesture stays the usual one.</summary>
    public UWCommandMode CommandMode { get; private set; }

    public bool IsCommandModeActive => CommandMode != UWCommandMode.None;

    /// <summary>Look, get and use turn the pointer into the red X (per user on the
    /// original, 2026-09-19); talk does not. Read by UWGameUI for the cursor.</summary>
    public bool IsRedXCursorMode => CommandMode == UWCommandMode.Look
        || CommandMode == UWCommandMode.Get || CommandMode == UWCommandMode.Use;

    /// <summary>Nothing, or an entity that only stands for geometry - the door frame carries a
    /// UWEntityInfo with the wall texture for the look but no object (see
    /// UWObjectSpawner.fSpawnDoorFrames). The modes treat it like a wall (per user on the
    /// original, 2026-09-19: the lintel gets "You cannot use that.", not the reach refusal).</summary>
    internal static bool IsGeometryEntity(UWEntityInfo pOTarget)
    {
        return pOTarget == null || pOTarget.ObjectData == null;
    }

    internal static bool IsCreatureEntity(UWEntityInfo pOTarget)
    {
        return pOTarget != null && pOTarget.ObjectData != null
            && UWObjectIds.IsCreature(pOTarget.ObjectData.ID);
    }

    /// <summary>
    /// A click on a command icon (UWHudCommands). The original's SetInteractionMode: the icon
    /// that is already lit switches the mode off; the fight icon is the weapon toggle and is
    /// refused while swimming (ToggleCombatMode); choosing talk, get or look while the weapon is
    /// drawn puts it away. Options has its own panel and is not a mode here. A gesture of the
    /// right button that is still open is cancelled. Every mode changes the right-button
    /// gesture in the viewport in its own way, see UWClassicWorldGesture.Begin.
    /// </summary>
    public void SetCommandMode(UWCommandMode peMode)
    {
        if (peMode == UWCommandMode.Options)
            return;

        if (peMode == UWCommandMode.Fight)
        {
            ToggleCombatMode();

            return;
        }

        UWCommandMode leNext = UWClickRules.NextMode(CommandMode, peMode);

        if (IsCombatModeActive)
            ToggleCombatMode();

        mOGesture.Cancel();
        CommandMode = leNext;
    }

    /// <summary>
    /// Is attack in progress? (UWPlayerAttackState.IsAttacking)
    /// </summary>
    public bool IsAttacking
    {
        get { return mOAttackState.IsAttacking; }
        set { mOAttackState.IsAttacking = value; }
    }

    /// <summary>
    /// Current attack charge from 0 (none) to 1 (max): play_pow / 100.
    ///
    /// THE CHARGE STARTS AFTER THE WIND-UP, BY THE USER'S DECISION OF 2026-10-12 TO FOLLOW THE
    /// ORIGINAL (combat C1): player_attack (UnderworldExhumed combat/COMBAT.C) sets play_pow to 0
    /// at the swing's start and raises it only while the weapon holds at frame 3, after the
    /// wind-up (adjust_weapon, ui/PANELS.C), by the weapon's speed per step, at most 100. A blow
    /// released right after the wind-up strikes with the weapon's minimum charge. Until then
    /// ours counted from the press, so the moment the wind-up was through a dagger stood at
    /// about half its span already; the timing had been fitted to the user's stopwatch
    /// (2026-08-30, "the time until MinCharge is too long for every weapon"), press to green,
    /// and those measurements still fit: wind-up 0.5 s plus the steps, the rest reaction time.
    /// The steps are UWPlayerAttackState.StepCharge (cleanup step 5.14, 2026-10-22).
    /// </summary>
    public float AttackCharge
    {
        get { return mOAttackState.Charge; }
    }

    /// <summary>The aiming cursor when shooting - the wind-up of a drawn launcher is through
    /// (UWPlayerAttackInput.IsRangedTargeting).</summary>
    public bool IsRangedTargeting
    {
        get { return mOAttackInput != null && mOAttackInput.IsRangedTargeting; }
    }

    private void Awake()
    {
        mGameUi = GetComponent<UWGameUI>();
        mControlScheme = GetComponentInParent<UWControlScheme>();
        mCharacter = GetComponent<UWCharacter>();
        mOLog = new UWMessageLog(this);
        mOPrompts = new UWPromptController(this);
        mOLook = new UWLookService(this, mOLog);
        mOSpell = new UWPendingSpell(this);
        mOGesture = new UWClassicWorldGesture(this);
        mOModernInput = new UWModernWorldInput(this);
        mOWorldUse = new UWWorldUse(this);
        mOHitFeedback = new UWHitFeedback(this);
        mOMissiles = new UWPlayerMissiles(this);
        mOMelee = new UWPlayerMelee(this, mOAttackState, mOHitFeedback);
        mOAttackInput = new UWPlayerAttackInput(this, mOAttackState, mOMelee, mOMissiles);

        if (mCharacter != null)
            mCharacter.Weapon.AttackConnected += mOAttackInput.OnAttackConnected;

        // No manual wiring in the scene needed - creates itself if
        // not present yet (same pattern as UWDebugOverlay in UWLevelLoader).
        mInventory = GetComponent<UWInventory>();

        if (mInventory == null)
            mInventory = gameObject.AddComponent<UWInventory>();

        // Likewise the sound player - creates itself so the effects are available in the
        // scene without manual work. Which effect plays when is not yet
        // decided by this (see UWSoundPlayer).
        mSoundPlayer = GetComponent<UWSoundPlayer>();

        if (mSoundPlayer == null)
            mSoundPlayer = gameObject.AddComponent<UWSoundPlayer>();

        // The intro player is idle until started (it only creates its display
        // then) - it is attached here only so it can be triggered in play mode via its
        // context menu without editing the scene.
        if (GetComponent<UWIntroPlayer>() == null)
            gameObject.AddComponent<UWIntroPlayer>();

        // The eyes at the top edge - they also build their display only on the first hit
        // (see UWEyesDisplay).
        mEyes = GetComponent<UWEyesDisplay>();

        if (mEyes == null)
            mEyes = gameObject.AddComponent<UWEyesDisplay>();

        // The conversation screen likewise creates its UI only on the first conversation
        // and sits idle here until then.
        mConversationScreen = GetComponent<UWConversationController>();

        if (mConversationScreen == null)
            mConversationScreen = gameObject.AddComponent<UWConversationController>();
    }

    private void Start()
    {
        mInput = mControlScheme.Controls.Player;
    }

    private void Update()
    {
        // Jumping into water with the weapon drawn ends combat mode and puts the weapon away:
        // UW.EXE SetPlayerDataOxB9_seg008_B calls PutAwayWeapon_seg024_15C0 when swimming starts
        // (per user on the original, 2026-09-14).
        if (IsCombatModeActive && fIsSwimming())
        {
            IsCombatModeActive = false;
            fResetAttackState();

            // PutAwayWeapon picks a level theme whatever plays (UWMusicSelector.OnCombatModeLeft).
            UWAudioService.PickLevelTheme();
        }

        if (mOUWDataImporter == null)
        {
            if (mLevelLoader == null)
                mLevelLoader = UWScene.LevelLoader;

            if (mLevelLoader != null)
                mOUWDataImporter = mLevelLoader.UWDataImporter;
        }

        // The weapon button's press and the weapon in hand, before the modal gates
        // (UWPlayerAttackInput.UpdatePress, which keeps why).
        mOAttackInput.UpdatePress();

        // THE MANTRA INPUT COMES BEFORE THE MODAL CHECK: it holds the world itself (modal hold
        // "typed input"), so behind the check it would never see its own Enter - the input
        // could not be left (per user, 2026-09-26, right after the hold came in). The same for
        // the swallow of an input a mouse button ended.
        mOPrompts.UpdateTypedInputSwallow();

        if (mOPrompts.UpdateMantra())
            return;

        // Q OVER THE INTERFACE IN A CONVERSATION (per user, 2026-10-14: the bags' menu could look
        // at a thing during a conversation, Q could not): the conversation holds the world, not
        // the modern windows, so Q looks at the thing in the slot under the pointer there too.
        if (UWControlScheme.Ui.Policy.WorldInput == UWWorldInputKind.KeysAndFreePointer && mConversationScreen != null && mConversationScreen.IsOpen)
            mOModernInput.UpdateLookOverUi();

        // While a modal UI panel is open (currently only the Modern inventory),
        // combat/interaction should pause - otherwise e.g. a right click on a
        // backpack slot would also count as attack/use in the 3D world.
        if (mControlScheme != null && mControlScheme.IsWorldInputBlocked)
            return;

        // A running conversation is modal: while it is open, nothing happens in the world.
        if (mConversationScreen != null && mConversationScreen.IsOpen)
            return;

        // The trap question likewise - it waits for yes or no (UWLookService.fTryDetectTrap), and
        // the repair question the same (UWPromptController).
        if (mOPrompts.UpdateYesNo())
            return;

        // A conversation is modal - and so is a long text: if it does not fit into the
        // message box at once, the game waits after each page for a click or a
        // key before continuing (per user, 2026-09-03; UWMessageLog).
        if (mOLog.UpdatePaging())
            return;

        // A displayed window picture is modal (observed by user, 2026-08-29: the
        // mouse cursor disappears as with a full-screen cutscene): the next
        // key press or click only hides it again and triggers nothing else.
        if (mGameUi != null && mGameUi.Pictures.IsWindowPictureVisible)
        {
            // The gamepad too, a stick pushed out as well - walking off hides it. The press is the
            // picture's: the pad's pointer does not click with it (UWPadOwners asks PictureTakesKeys
            // when the frame begins; UWGamepad.ConsumePress here until cleanup step 7.8).
            if (fAnyInputThisFrame() || UWGamepad.AnyPressed(true))
            {
                miPictureHiddenFrame = Time.frameCount;
                mGameUi.Pictures.HideWindowPicture();
            }

            return;
        }

        // DURING USE MODE (UWInventory.UseModeItem) the right click in the 3D view puts the held item
        // onto what is there - read by exactly one class, this one, so that no second reading of
        // the same click can follow (2026-08-27: the look text appeared twice when using a key).
        // Clicks on the inventory during use mode stay with UWItemDrag.
        if (mInventory != null && mInventory.UseModeItem != null)
        {
            mOGesture.UpdateUseModeInWorld();
            return;
        }

        // A spell still waiting for its target intercepts the next click.
        if (mOSpell.UpdateTargeting())
            return;

        // The three arrows under the compass move, a click on the compass itself prints the
        // status - both use the click up. The arrows come first because they lie below the
        // disc and a miss there should not report the status. The arrows also take the HELD
        // button, because holding one repeats the step (UWPlayerInput.HoldEasyMovement).
        // (The compass belongs to the classic frame, which the modern scheme does not show.)
        if (UWControlScheme.Ui.Policy.WorldInput == UWWorldInputKind.RightButtonGesture && (mInput.Interact.IsPressed() || UWMouseButtons.LeftHeld))
        {
            if (mOGesture.TryEasyMovement(UWPointer.Position))
                return;
        }

        if (UWControlScheme.Ui.Policy.WorldInput == UWWorldInputKind.RightButtonGesture && (mInput.Interact.WasPressedThisFrame() || UWMouseButtons.LeftPressed))
        {
            if (mOGesture.TryReportStatus(UWPointer.Position))
                return;
        }

        // THE MODERN SCHEME'S KEYS: E, R, the right button to look, the left to draw.
        if (UWControlScheme.Ui.Policy.WorldInput == UWWorldInputKind.KeysAndFreePointer)
            mOModernInput.UpdateActions();

        // A right click that jumps during a cursor movement is used up (see
        // UWPlayerInput.ConsumesRightClick).
        bool lbRightClickFree = !UWPlayerInput.ConsumesRightClick();

        if (lbRightClickFree && UWControlScheme.Ui.Policy.WorldInput == UWWorldInputKind.RightButtonGesture && mInput.Interact.WasPressedThisFrame())
        {
            // IN COMBAT MODE THE RIGHT BUTTON IS THE WEAPON AND NOTHING ELSE (per user, 2026-09-18):
            // no look, no use - and no striking while sheathing or drawing either, in the original
            // the draw motion must finish first (per user, 2026-08-30). With something on the
            // pointer the click places it (UWItemDrag), it does not look.
            if (IsCombatModeActive)
                mOAttackInput.BeginFromRightPress();
            else if (mInventory == null || mInventory.CursorItem == null)
                mOGesture.Begin();
        }
        else
        {
            // The release that strikes or shoots, the charge while held
            // (UWPlayerAttackInput.UpdateReleaseOrCharge).
            mOAttackInput.UpdateReleaseOrCharge(lbRightClickFree);
        }

        mOGesture.UpdatePointer();

        // The capping of the message list, one line a frame (UWMessageLog.Trim) - here at the
        // end, behind every modal gate above, as it always ran.
        mOLog.Trim();
    }

    // ------------------------------------------------- The right button in the 3D view
    //
    // The classic scheme's clicks into the world are UWClassicWorldGesture's since cleanup step
    // 5.14 (2026-10-22).

    /// <summary>The right button's open gesture ends without acting - the weapon's release takes
    /// the button (UWClassicWorldGesture.CancelPointer).</summary>
    internal void CancelWorldPointer()
    {
        mOGesture.CancelPointer();
    }

    // ------------------------------------------------- The modern scheme's keys
    //
    // R, Q, E, the free pointer on the world and the crosshair's label are UWModernWorldInput's
    // since cleanup step 5.14 (2026-10-22); the surface their callers use stays here and forwards.

    /// <summary>Is the modern pointer free (UWModernWorldInput.IsPointerFree)?</summary>
    internal bool IsPointerFree()
    {
        return mOModernInput.IsPointerFree();
    }

    /// <summary>Do the mouse buttons act in the world (UWModernWorldInput.MouseInWorld)?</summary>
    internal bool MouseInWorld()
    {
        return mOModernInput.MouseInWorld();
    }

    /// <summary>Where E and Q aim in the modern scheme (UWModernWorldInput.AimRay).</summary>
    internal Ray ModernAimRay()
    {
        return mOModernInput.AimRay();
    }

    /// <summary>Whether a thing's menu can open on this spot (UWModernWorldInput).</summary>
    public bool HasWorldThingAt(Vector2 pOPointer)
    {
        return mOModernInput.HasWorldThingAt(pOPointer);
    }

    /// <summary>A left click on a thing in the world with the free pointer: its menu - true if
    /// there was a thing (UWModernWorldInput).</summary>
    public bool TryOpenWorldMenu(Vector2 pOPointer)
    {
        return mOModernInput.TryOpenWorldMenu(pOPointer);
    }

    /// <summary>The world menu's Use and Talk: the direct use of E held (UWModernWorldInput).</summary>
    public void ModernUseThing(UWEntityInfo pOTarget, UWEntityInfo pOPickableBehind)
    {
        mOModernInput.ModernUseThing(pOTarget, pOPickableBehind);
    }

    /// <summary>How far the hold of E has come, 0 to 1, for the HUD; -1 while there is none to
    /// show (UWModernWorldInput).</summary>
    public float ModernUseHoldProgress => mOModernInput != null ? mOModernInput.ModernUseHoldProgress : -1f;

    /// <summary>What lies under the crosshair, measured as the right button's press measures it
    /// (UWClassicWorldGesture.Begin): the frontmost thing or the wall/floor texture, and the pickable thing
    /// behind a blood pool or bone pile. Beyond the sight nothing.</summary>
    internal void AimAtCrosshair(out UWEntityInfo pOFrontmost, out string psChunk, out UWEntityInfo pOPickable)
    {
        AimAt(InteractionRay(), out pOFrontmost, out psChunk, out pOPickable);
    }

    /// <summary>The same along any ray - the free pointer's (ModernAimRay).</summary>
    internal void AimAt(Ray lORay, out UWEntityInfo pOFrontmost, out string psChunk, out UWEntityInfo pOPickable)
    {
        pOFrontmost = null;
        psChunk = null;
        pOPickable = null;

        UWWorldPicker.Measurement lOMeasured = UWWorldPicker.Measure(lORay);

        if (!lOMeasured.HasFrontmost || !IsWithinSight(lOMeasured.FrontmostHit))
            return;

        pOFrontmost = lOMeasured.Frontmost;
        psChunk = pOFrontmost == null ? DescribeChunkHit(lOMeasured.FrontmostHit) : null;
        pOPickable = lOMeasured.Pickable;
    }

    /// <summary>
    /// How far the eye reaches. LOOKING HAS NO RANGE OF ITS OWN in the original (the reference
    /// calls look.LookAt without any reach check) - what limits it is the renderer: the tile band
    /// goes only as deep as the viewing distance of the current light level (SHADES.DAT, see
    /// UWExplorationRules and UWPlayerTerrain.ViewingDistance), and what is not drawn is not
    /// there. It counts in ROWS OF TILES along the view direction snapped to a quadrant, exactly
    /// as the band is built - which is why the boundary falls on the tile edge.
    ///
    /// A WALL FACE BELONGS TO THE OPEN TILE IN FRONT OF IT, not to the solid one behind: the
    /// renderer draws it while working through the open tile, the solid tile itself has no
    /// geometry. That is why the hit point is moved along the surface normal TOWARDS the eye.
    ///
    /// MEASURED (per user in the original, save 4, level 2): standing on tile 34/15 at fine
    /// position 1 and looking east at the wall of the solid tile 38, whose face is drawn on tile
    /// 37 and therefore lies three rows ahead, it reads "You see a rough hewn wall."; one hair
    /// further west the player stands on tile 33, the face is the fourth row and the answer is
    /// "You see nothing.". Carrying no light that is light level 0, viewing distance 3 - and with
    /// the Taper of Sacrifice (brightness 3, viewing distance 4) it is exactly one tile more, as
    /// the user measured.
    /// </summary>
    internal bool IsWithinSight(RaycastHit pOHit)
    {
        // A hair out of the surface, towards the eye: a wall face is drawn on the open tile in
        // front of it, and that is the row the band counts.
        return IsWithinSight(pOHit.point + (pOHit.normal * 0.5f));
    }

    /// <summary>The same for a point that already lies on the tile that is drawn - a creature's
    /// feet, for addressing it (see UseTarget).</summary>
    internal bool IsWithinSight(Vector3 pOSurface)
    {
        if (mOPlayerTerrain == null)
            mOPlayerTerrain = UWScene.PlayerTerrain;

        if (mOPlayerTerrain == null)
            return true;

        int liQuadrant = Mathf.RoundToInt(Mathf.Repeat(transform.eulerAngles.y, 360f) / 90f) & 3;
        UWTilePos lOForward = UWExplorationRules.ForwardOfQuadrant(liQuadrant);

        UWTilePos lOPlayerTile = UWTileQueries.WorldToTile(transform.position.x, transform.position.z);

        UWTilePos lOTargetTile = UWTileQueries.WorldToTile(pOSurface.x, pOSurface.z);

        int liRow = ((lOTargetTile.X - lOPlayerTile.X) * lOForward.X)
            + ((lOTargetTile.Y - lOPlayerTile.Y) * lOForward.Y);

        return liRow <= mOPlayerTerrain.ViewingDistance;
    }

    // ------------------------------------------------- Actions
    //
    // What a gesture resolves to - and what the command icons of the original (Talk, Get, Look,
    // Use) will call once they exist. Each action is complete in itself: whoever calls it needs
    // no knowledge of the others.

    /// <summary>Look: the description of the thing, or of the wall or floor texture
    /// (UWLookService).</summary>
    public void LookAtTarget(UWEntityInfo pOTarget, string psChunkDescription)
    {
        mOLook.LookAtTarget(pOTarget, psChunkDescription);
    }

    /// <summary>Use: door, conversation partner, switch, object effect, inscription - within
    /// reach (UWWorldUse.UseTarget, cleanup step 5.14, 2026-10-22).</summary>
    public void UseTarget(UWEntityInfo pOTarget)
    {
        mOWorldUse.UseTarget(pOTarget);
    }

    /// <summary>Talk: the conversation with the thing, if it has one (UWWorldUse.TalkTo). False for
    /// anything that is not a creature.</summary>
    public bool TalkTo(UWEntityInfo pOTarget)
    {
        return mOWorldUse.TalkTo(pOTarget);
    }

    /// <summary>Talk mode over a thing that is no creature (UWWorldUse.TalkToThing).</summary>
    internal void TalkToThing(UWEntityInfo pOTarget)
    {
        mOWorldUse.TalkToThing(pOTarget);
    }

    /// <summary>For UWItemDrag (Original: plain left click on the hand slot that is
    /// currently the active combat hand, see UWInventory.MainHandSlot) and for the modern
    /// scheme's weapon button - it toggles the combat mode, no more and no less. UWItemDrag
    /// checks itself whether the clicked slot is MainHandSlot at all and holds a weapon
    /// - this method toggles unconditionally.</summary>
    public void ToggleCombatMode()
    {
        // No weapon while swimming: UW.EXE DrawWeapon_seg024_1531 and SetInteractionMode_seg024_24DC_13D5
        // refuse while bit 0 of player byte 0xB8 (swimming) is set (per user on the original,
        // 2026-09-14). Putting the weapon away still works.
        if (!IsCombatModeActive && fIsSwimming())
            return;

        IsCombatModeActive = !IsCombatModeActive;
        fResetAttackState();

        // The weapon is mode 2 of the original and replaces talk, get and look.
        CommandMode = UWCommandMode.None;
    }

    /// <summary>
    /// Forgets a charge and a strike still waiting for its hit frame - whenever the weapon goes
    /// away or comes out. Per user, 2026-10-03 (modern scheme): jumping into water mid-strike put
    /// the weapon away before the hit frame came, the pending strike stayed set, and no strike
    /// started again until the original scheme's press path, which does not look at it, got one
    /// through; putting the weapon away and drawing it again did not help.
    /// </summary>
    private void fResetAttackState()
    {
        mOAttackState.Reset();
    }

    private UWPlayerTerrain mOPlayerTerrain;

    private bool fIsSwimming()
    {
        if (mOPlayerTerrain == null)
            mOPlayerTerrain = UWScene.PlayerTerrain;

        return mOPlayerTerrain != null && mOPlayerTerrain.IsSwimming;
    }

    /// <summary>Any key or mouse button in this frame - for clicking away the
    /// window picture. Deliberately read directly from the devices instead of the Input System actions:
    /// ANY input is wanted here, not a specific binding.</summary>
    private static bool fAnyInputThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            return true;

        if (UWMouseButtons.AnyPressed)
            return true;

        return false;
    }

    /// <summary>A line of the conversation block (7) into the message scroll
    /// (UWWorldUse.AddConversationBlockMessage).</summary>
    internal void AddConversationBlockMessage(int piIndex)
    {
        mOWorldUse.AddConversationBlockMessage(piIndex);
    }

    /// <summary>Which conversation belongs to this creature, or 0 (UWWorldUse.GetConversationSlot).</summary>
    public int GetConversationSlot(UWNpc pONpc)
    {
        return mOWorldUse.GetConversationSlot(pONpc);
    }

    /// <summary>Barrel, chest or nightstand in the world (UWWorldUse.IsWorldContainer).</summary>
    public static bool IsWorldContainer(UWEntityInfo pOTarget)
    {
        return UWWorldUse.IsWorldContainer(pOTarget);
    }

    /// <summary>Using a barrel, chest or nightstand in the world puts its contents openly onto its
    /// tile; smashed or burnt with pbFromDamage (UWWorldUse.SpillWorldContainer).</summary>
    public void SpillWorldContainer(UWEntityInfo pOTarget, bool pbIgnoreLock = false,
        bool pbAnnounceEmpty = true, bool pbFromDamage = false)
    {
        mOWorldUse.SpillWorldContainer(pOTarget, pbIgnoreLock, pbAnnounceEmpty, pbFromDamage);
    }

    /// <summary>The use triggers in a destroyed object's chain, in order
    /// (UWWorldUse.FireDestroyedUseTriggers).</summary>
    public void FireDestroyedUseTriggers(UWObject pOObject)
    {
        mOWorldUse.FireDestroyedUseTriggers(pOObject);
    }

    /// <summary>A general message as text, without adding it to the box right
    /// away - for messages assembled from several pieces (see
    /// UWMiscSpell.fDetectMonsters). Empty if the number does not exist (UWMessageLog).</summary>
    public string GetGeneralMessage(int piIndex)
    {
        return mOLog.GetGeneralMessage(piIndex);
    }

    /// <summary>A missing entry is not an error (UWText gives nothing for it), and nothing
    /// is added for it (UWMessageLog).</summary>
    public void AddGeneralMessage(int piIndex)
    {
        mOLog.AddGeneralMessage(piIndex);
    }

    // ------------------------------------------------- Combat
    //
    // The weapon button, the blow, the shot and the hit's pictures are UWPlayerAttackInput's,
    // UWPlayerMelee's, UWPlayerMissiles' and UWHitFeedback's since cleanup step 5.14 (2026-10-22);
    // the surface their callers use stays here and forwards.

    /// <summary>
    /// The base damage of a missile the player throws BY HAND - see UWPlayerThrow
    /// (UWPlayerMissiles.GetThrownMissileBaseDamage).
    /// </summary>
    public int GetThrownMissileBaseDamage(int piObjectId)
    {
        return mOMissiles.GetThrownMissileBaseDamage(piObjectId);
    }

    /// <summary>The damage type of a class 1 object the player throws
    /// (UWPlayerMissiles.GetThrownMissileDamageType).</summary>
    public int GetThrownMissileDamageType(int piObjectId)
    {
        return mOMissiles.GetThrownMissileDamageType(piObjectId);
    }

    /// <summary>
    /// A critical hit on the player wears the piece the body part picks by 2d4 - called by the
    /// creature's blow (UWPlayerMelee.WearOnCriticalHit).
    /// </summary>
    public void WearOnCriticalHit(int piPart)
    {
        mOMelee.WearOnCriticalHit(piPart);
    }

    /// <summary>The player's missile has hit: an object's flash, a creature's eyes
    /// (UWHitFeedback.ReportSpellHit).</summary>
    public void ReportSpellHit(UWEntityInfo pOTarget, UWDamageable pODamageable, Vector3 pOPoint)
    {
        mOHitFeedback.ReportSpellHit(pOTarget, pODamageable, pOPoint);
    }

    /// <summary>The look message for an item in the inventory or on the floor, with condition
    /// word, quantity, enchantment and owner (UWLookService.DescribeInventoryItem).</summary>
    public string DescribeInventoryItem(UWObject pOItem, int piDetail = UWItemDescriptions.DetailFromLoreCheck)
    {
        return mOLook.DescribeInventoryItem(pOItem, piDetail);
    }

    /// <summary>Looking at an item in the inventory: its look sentence, then SpecialLook's line
    /// - both schemes (UWLookService).</summary>
    public void AddItemLookMessages(UWObject pOItem)
    {
        mOLook.AddItemLookMessages(pOItem);
    }

    /// <summary>Identifies an item permanently and describes it right away - the spell Name
    /// Enchantment. False if the item cannot be identified (UWLookService).</summary>
    public bool IdentifyItem(UWObject pOItem)
    {
        return mOLook.IdentifyItem(pOItem);
    }

    /// <summary>SpecialLook's line for a thing in the world that is looked at with full detail
    /// (UWLookService). Null when there is none.</summary>
    public string GetSpecialLookLine(UWEntityInfo pOTarget)
    {
        return mOLook.GetSpecialLookLine(pOTarget);
    }

    /// <summary>The look sentence for a thing in the world, without the look's triggers and
    /// trap search (UWLookService).</summary>
    public string GetLookDescription(UWEntityInfo pOTarget)
    {
        return mOLook.GetLookDescription(pOTarget);
    }

    /// <summary>Reads a book or scroll; false if the object is neither or has no text
    /// (UWLookService.TryReadBook).</summary>
    public bool TryReadBook(UWObject pOItem, bool pbLooking)
    {
        return mOLook.TryReadBook(pOItem, pbLooking);
    }

    /// <summary>Whether a thing lies within the close look's detail range - player_3dlook gives
    /// a look with detail within 0x48 only (UWReachRules.LookDetailDistanceSquared); for
    /// UWLookService, which shows a window's picture only then.</summary>
    public bool IsWithinLookDetail(UWEntityInfo pOTarget)
    {
        return fReach(pOTarget, UWRules.Current.Inventory.Reach.LookDetailDistanceSquared, false, false) == ReachEnum.Reached;
    }

    /// <summary>For UWItemDrag (Original: use mode, right click in the view window) -
    /// returns the look target under the given screen position, or null (also for
    /// a wall/floor/ceiling hit without its own UWEntityInfo - confirmed by user,
    /// a wall shows no message at all in use mode, unlike normal
    /// right-click look). No category filter (unlike the picker's pickable rule/formerly
    /// TryGetDoorTargetAt) - the caller checks IsDoor itself. The RAY REACHES AS FAR AS LOOKING:
    /// whether the thing can be used from here is decided afterwards on the tile grid
    /// (TryReachTarget), which is also what lets the refusal be spoken at all.</summary>
    public UWEntityInfo TryGetEntityAt(Vector2 pOScreenPos, UWObject pOHeldItem)
    {
        // The same aim as the pointer's own (UWClassicWorldGesture.Begin): by what is drawn - a model's
        // shape, a sprite's pixels -, past spell effects and pure logic volumes (UWWorldPicker).
        // A plain raycast took the first box, and a blood stain beside a boulder got the rock
        // hammer (per user, 2026-10-01).
        return UWWorldPicker.Measure(Camera.main.ScreenPointToRay(pOScreenPos)).Frontmost;
    }

    /// <summary>
    /// Is the target within reach for a use action with the given item on the cursor (null = bare
    /// hand)? Out of reach it says so, exactly as the original does - see UWReachRules for the
    /// rule and its measurement. The pole (UWObjectMechanics.PoleObjectId) doubles distance and
    /// height window, Telekinesis lifts the limit entirely.
    /// </summary>
    public bool TryReachTarget(UWEntityInfo pOTarget, UWObject pOHeldItem,
        int piRefusal = UWReachRules.UnableToUseMessage)
    {
        bool lbUsingPole = pOHeldItem != null && pOHeldItem.ID == UWObjectMechanics.PoleObjectId;

        if (CanReach(pOTarget, UWReachRules.GetDistanceSquared(lbUsingPole, HasTelekinesis), lbUsingPole))
            return true;

        AddGeneralMessage(piRefusal);

        return false;
    }

    /// <summary>Taking something reaches as far as the bare hand and has its own refusal, "That
    /// is too far away to take." (per user on the original, 2026-09-18). The pole does not help
    /// here, Telekinesis does.</summary>
    public bool TryReachForPickup(UWEntityInfo pOTarget)
    {
        int liRefusal = PickupReachRefusal(pOTarget);

        if (liRefusal < 0)
            return true;

        AddGeneralMessage(liRefusal);

        return false;
    }

    /// <summary>The same measurement without the message - for UWWorldPickup, whose refusal
    /// comes from UWPickupRules.</summary>
    public bool CanReachForPickup(UWEntityInfo pOTarget)
    {
        return PickupReachRefusal(pOTarget) < 0;
    }

    /// <summary>
    /// Why a thing cannot be taken, or -1: player_3dget's game_sprint(inrange + 0x5D) - out of
    /// range "That is too far away to take." (94), in range but the terrain between blocks "You
    /// cannot reach that." (95; per user on the original 2026-10-17, the apple across the water
    /// channel on level 1).
    /// </summary>
    public int PickupReachRefusal(UWEntityInfo pOTarget)
    {
        switch (fReach(pOTarget, UWReachRules.GetPickupDistanceSquared(HasTelekinesis), false, true))
        {
            case ReachEnum.Reached:
                return -1;

            case ReachEnum.Blocked:
                return UWReachRules.CannotReachMessage;

            default:
                return UWReachRules.TooFarToTakeMessage;
        }
    }

    /// <summary>Telekinesis lifts the reach - for the use reach of UWPendingSpell too.</summary>
    internal bool HasTelekinesis
    {
        get { return mCharacter != null && mCharacter.Vitals.HasTelekinesis; }
    }

    internal bool CanReach(UWEntityInfo pOTarget, int piDistanceSquared, bool pbUsingPole)
    {
        return fReach(pOTarget, piDistanceSquared, pbUsingPole, true) == ReachEnum.Reached;
    }

    private enum ReachEnum
    {
        Reached,
        OutOfRange,
        Blocked
    }

    /// <summary>InPickRange and, with pbTerrain, BlockingTerrain (UWReachRules): the one
    /// measurement behind use, take, the target mode and a target spell. The close look of
    /// player_3dlook asks the range alone.</summary>
    private ReachEnum fReach(UWEntityInfo pOTarget, int piDistanceSquared, bool pbUsingPole, bool pbTerrain)
    {
        if (pOTarget == null)
            return ReachEnum.OutOfRange;

        int liPlayerX, liPlayerY, liPlayerZ;
        int liTargetX, liTargetY, liTargetZ;

        fGetPlayerGridPosition(out liPlayerX, out liPlayerY, out liPlayerZ);
        fGetTargetGridPosition(pOTarget, out liTargetX, out liTargetY, out liTargetZ);

        if (!UWReachRules.CanReach(liPlayerX, liPlayerY, liPlayerZ, liTargetX, liTargetY, liTargetZ,
            piDistanceSquared, pbUsingPole))
            return ReachEnum.OutOfRange;

        UWLevel lOLevel = mLevelLoader != null ? mLevelLoader.CurrentLevel : null;

        if (!pbTerrain || lOLevel == null)
            return ReachEnum.Reached;

        UWTerrain lOTerrain = mOUWDataImporter != null ? mOUWDataImporter.Terrain : null;

        bool lbBlocked = UWReachRules.IsBlockedByTerrain(liPlayerX >> 3, liPlayerY >> 3, liPlayerZ,
            liTargetX >> 3, liTargetY >> 3, liTargetZ, piDistanceSquared, pbUsingPole,
            (piX, piY) =>
            {
                UWTile lOTile = lOLevel.GetTile(piX, piY);

                return lOTile != null ? (lOTile.FloorHeight >> 4) & 0xF : 0;
            },
            (piX, piY) =>
            {
                UWTile lOTile = lOLevel.GetTile(piX, piY);

                return lOTile != null && lOTerrain != null ? ((int)lOTerrain.GetFloorTerrain(lOTile.TextureFloor) & 0xFF) >> 4 : 0;
            },
            (piX, piY) =>
            {
                UWTile lOTile = lOLevel.GetTile(piX, piY);
                int liBridge = -1;

                if (lOTile != null && lOTile.ObjectsInTile != null)
                {
                    foreach (UWObject lOObject in lOTile.ObjectsInTile)
                    {
                        if (lOObject != null && lOObject.ID == UWObjectIds.Bridge && lOObject.ZPos > liBridge)
                            liBridge = lOObject.ZPos;
                    }
                }

                return liBridge;
            });

        return lbBlocked ? ReachEnum.Blocked : ReachEnum.Reached;
    }

    /// <summary>The player on the tile grid: position in eighths of a tile, feet in zpos steps.
    /// This class sits on the camera, whose ground plan is the body's.</summary>
    private void fGetPlayerGridPosition(out int piX, out int piY, out int piZ)
    {
        Vector3 lOPosition = transform.position;

        piX = UWTileQueries.WorldToEighths(lOPosition.x);
        piY = UWTileQueries.WorldToEighths(lOPosition.z);

        UWCharacter lOCharacter = UWScene.Character;

        piZ = lOCharacter != null
            ? lOCharacter.GetFeetZPos()
            : UWUnits.WorldToZPos(lOPosition.y);
    }

    /// <summary>The target on the tile grid. Its own data is the measure - a wall decal is placed
    /// on the wall face in the world, but the original reckons with the sub-tile spot it is
    /// stored on. Only something without data (there should be none in a use) is read from its
    /// position.</summary>
    private static void fGetTargetGridPosition(UWEntityInfo pOTarget, out int piX, out int piY, out int piZ)
    {
        UWObject lOData = pOTarget.ObjectData;

        if (lOData != null)
        {
            piX = UWReachRules.ToEighths(lOData.TileX, lOData.XPos);
            piY = UWReachRules.ToEighths(lOData.TileY, lOData.YPos);
            piZ = lOData.ZPos;

            return;
        }

        Vector3 lOPosition = pOTarget.transform.position;

        piX = UWTileQueries.WorldToEighths(lOPosition.x);
        piY = UWTileQueries.WorldToEighths(lOPosition.z);
        piZ = UWUnits.WorldToZPos(lOPosition.y);
    }

    // ------------------------------------------------- Shrine and yes/no questions
    //
    // The mantra and the yes/no question are UWPromptController's since cleanup step 5.13
    // (2026-10-22); the surface their callers use stays here and forwards.

    /// <summary>Whether a mantra is currently being typed - then the letters belong to the
    /// shrine, not to the key commands (see UWGameUI, key F).</summary>
    public bool IsChanting => mOPrompts != null && mOPrompts.IsChanting;

    /// <summary>For the modern scheme's mantra box (UWModernQuestion): the typed text so far.</summary>
    public string MantraText => mOPrompts != null ? mOPrompts.MantraText : string.Empty;

    /// <summary>The mantra box's OK (as Enter) or Cancel (as Escape).</summary>
    public void FinishMantra(bool pbCancel)
    {
        mOPrompts.FinishMantra(pbCancel);
    }

    /// <summary>A yes/no question in the message scroll (UWPromptController.AskYesNo) - the anvil's
    /// repair and the trap found on a look ask this way, and so may anything that acts for this
    /// component (UWLookService).</summary>
    public void AskYesNo(string psQuestion, System.Action<bool> pOAnswered)
    {
        mOPrompts.AskYesNo(psQuestion, pOAnswered);
    }

    /// <summary>For the modern scheme's question box (UWModernQuestion): a question is open, and
    /// its text.</summary>
    public bool IsAskingYesNo => mOPrompts != null && mOPrompts.IsAskingYesNo;

    public string YesNoQuestion => mOPrompts != null ? mOPrompts.YesNoQuestion : null;

    /// <summary>The modern question box's OK (true) or Cancel (false).</summary>
    public void AnswerYesNo(bool pbYes)
    {
        mOPrompts.AnswerYesNo(pbYes);
    }

    // ------------------------------------------------- Repair

    // The estimate, the attempt and their messages are in UWRepairRules (P3, 2026-09-18).
    // ------------------------------------------------- Item on item

    /// <summary>The message a spiked door answers everything with - look as well as
    /// use.</summary>
    public void ReportSpikedDoor()
    {
        AddGeneralMessage(UWItemApplications.DoorIsSpikedMessage);
    }

    /// <summary>
    /// The first part of repairing: the estimate and the question (reference:
    /// repair.RepairLogic).
    ///
    /// THE ESTIMATE is a pure calculation, not a check: durability minus skill
    /// plus fifteen, and from that one of five words from "trivial" to "very
    /// difficult". So it reveals nothing the player could not work out
    /// themselves - but it does not use anything up either.
    ///
    /// WHAT CANNOT BE REPAIRED has a durability from 0x80 - read as a
    /// signed number it is then negative. The reference checks exactly
    /// that; 0xFF in our table stands for "indestructible".
    /// </summary>
    public void TryRepairItem(UWObject pOItem)
    {
        if (pOItem == null || mOUWDataImporter == null || IsAskingYesNo)
            return;

        string lsQuestion;

        if (!UWRepairRules.TryGetQuestion(pOItem, mOUWDataImporter,
            mCharacter != null ? mCharacter.Vitals.GetSkill(UWPlayerData.Skill.Repair) : 0, out lsQuestion))
        {
            AddGeneralMessage(UWRepairRules.CannotRepairMessage);

            return;
        }

        // The question works like every yes/no question - see UWPromptController.AskYesNo.
        AskYesNo(lsQuestion, pbYes =>
        {
            if (pbYes)
                fPlayRepairCutscene(pOItem);
        });
    }

    /// <summary>
    /// THE ANVIL SEQUENCE before the attempt: UW.EXE ItemRepair_ovr107_1754 calls
    /// PlayCutscene(0x104) - CS404, played inside the view window - and only then rolls
    /// (ovr107_163C) and prints the result. Its control file holds nothing but four
    /// "chime" commands at frames 4, 13, 22 and 31, the hammer blows, and the end at 36;
    /// UWIntroPlayer plays that sound for the command already. The player locks input
    /// while it runs. Without a player or without the files the attempt follows at once.
    /// </summary>
    private void fPlayRepairCutscene(UWObject pOItem)
    {
        UWIntroPlayer lOPlayer = UWScene.IntroPlayer;

        if (lOPlayer == null)
        {
            fRepair(pOItem);
            return;
        }

        lOPlayer.PlayCutscene(RepairCutscene, () => fRepair(pOItem));
    }

    /// <summary>CS404, the anvil (UW.EXE PlayCutscene 0x104).</summary>
    private const int RepairCutscene = 0x104;

    /// <summary>
    /// The attempt itself (reference: repair.RepairObjectSkillCheck).
    ///
    /// The Repair skill is checked against the item's DURABILITY - the more
    /// durable, the harder to work on.
    ///
    ///   critical success    fully repaired at once
    ///   success             three plus one fifth of the skill added
    ///   failure             nothing
    ///   critical failure    damage, now and then total loss
    ///
    /// The rolls, the fate of a destroyed item and the line are UWRepairRules.Attempt's
    /// (do_repair and repair_item, brought to the original on 2026-10-15).
    /// </summary>
    private void fRepair(UWObject pOItem)
    {
        if (pOItem == null)
            return;

        // Repairing is loud (reference: repair.cs sets PlayerQuietness 0xF).
        if (mCharacter != null)
            mCharacter.Vitals.MakeNoise(UWRepairRules.RepairNoise);

        UWRepairRules.Result lOResult = UWRepairRules.Attempt(pOItem, mOUWDataImporter,
            mCharacter != null ? mCharacter.Vitals.GetSkill(UWPlayerData.Skill.Repair) : 0);

        // A REPAIR TAKES TIME - see UWRepairRules.Attempt for the minutes.
        if (mCharacter != null)
            mCharacter.Vitals.AdvanceClockMinutes(lOResult.Minutes);

        if (!string.IsNullOrEmpty(lOResult.Message))
            AddMessage(lOResult.Message);

        // A destroyed item leaves the inventory as a whole, via RemoveItem, which also finds
        // it inside closed containers. TryConsumeOne counted enchanted armour down instead of
        // removing it: its quantity bit is set and the field holds a special link of 512 or more
        // (the leather cap in SAVE4: 719), so the item stayed on the paper doll (per user,
        // 2026-09-17). TryConsumeOne ignores such values now as well.
        if (lOResult.Destroyed && mInventory != null)
            mInventory.RemoveItem(pOItem);

        if (mInventory != null)
            mInventory.NotifyChanged();
    }

    // ------------------------------------------------- Disarming traps

    // The checks and the messages are in UWTrapDisarming (P3, 2026-09-18).
    /// <summary>
    /// The spell Remove Trap (reference: trapdisarming.TrapDisarmSpell).
    ///
    /// IT DOES NOT ASK AND ROLLS WITH FIXED VALUES: 45 for detecting, 45 for
    /// disarming (obj_spells, UnderworldExhumed SPELLS.C). Against the target 8,
    /// skill_check (SKILLCHK.C) adds 0 to 30 to 37 and always grades it 2: the spell
    /// never fails and never sets the trap off. (Until 2026-10-12 this said it could.)
    ///
    /// Returns false if no trap hangs on the target at all - then the caller reports
    /// "The spell has no discernable effect.".
    /// </summary>
    public bool TryRemoveTrapWithSpell(UWEntityInfo pOTarget)
    {
        if (pOTarget == null || pOTarget.ObjectData == null || mLevelLoader == null
            || mLevelLoader.CurrentLevel == null)
            return false;

        if (!UWTrapDisarming.SpellFinds(pOTarget.ObjectData, mLevelLoader.CurrentLevel.Masterlist))
            return false;

        fDisarmTrap(pOTarget, UWTrapDisarming.SpellTrapSkill);

        return true;
    }

    /// <summary>The disarm attempt with the Traps skill, answered yes to the trap found on a look
    /// (UWLookService).</summary>
    public void DisarmTrap(UWEntityInfo pOTarget)
    {
        fDisarmTrap(pOTarget);
    }

    /// <summary>The disarm attempt - the rule is in UWTrapDisarming.Disarm; a critical failure
    /// sets the trap off on the object's tile.</summary>
    private void fDisarmTrap(UWEntityInfo pOTarget, int piSkill = -1)
    {
        if (pOTarget == null || pOTarget.ObjectData == null || mLevelLoader == null
            || mLevelLoader.CurrentLevel == null)
            return;

        int liSkill = piSkill >= 0
            ? piSkill
            : (mCharacter != null ? mCharacter.Vitals.GetSkill(UWPlayerData.Skill.Traps) : 0);

        UWTrapDisarming.Result lOResult = UWTrapDisarming.Disarm(pOTarget.ObjectData,
            mLevelLoader.CurrentLevel, liSkill, mOUWDataImporter);

        if (!string.IsNullOrEmpty(lOResult.Message))
            AddMessage(lOResult.Message);

        if (lOResult.TrapToFire != null)
            UWTriggerSystem.SetOffDisarmableTrap(lOResult.TrapToFire, lOResult.TriggerToFire, pOTarget.ObjectData,
                pOTarget.ObjectData.TileX, pOTarget.ObjectData.TileY, mLevelLoader, this);
    }

    // ------------------------------------------------- Messages
    //
    // The list, the running line and the paging are UWMessageLog's since cleanup step 5.13
    // (2026-10-22); about 110 callers use this surface, which forwards.

    /// <summary>A long text is waiting for its next page (UWMessageLog).</summary>
    public bool IsWaitingForPage => mOLog.IsWaitingForPage;

    /// <summary>The page-forward input was swallowed this frame - UWItemDrag asks, so the click
    /// that confirms the last page starts no drag (UWMessageLog).</summary>
    public bool PageInputConsumedThisFrame => mOLog.PageInputConsumedThisFrame;

    /// <summary>
    /// THE PAGING TAKES THIS FRAME'S PRESS (cleanup 7, wave 2b, per user 2026-10-25: one Escape closes
    /// only the topmost - Escape paged and opened the modern game menu as well): Update gets to the
    /// paging - no mantra, nothing modal, no conversation, no yes/no question before it - and a page
    /// waits; or the press of this frame paged already. UWBack asks (its Paging row).
    /// </summary>
    internal bool PagingTakesKeys => (fReachesAnyKeyWaits && IsWaitingForPage) || PageInputConsumedThisFrame;

    /// <summary>The window picture takes this frame's press, as the paging (UWBack, its WindowPicture
    /// row): Update gets to it and it is up, or the press of this frame hid it.</summary>
    internal bool PictureTakesKeys => (fReachesAnyKeyWaits && !IsWaitingForPage && mGameUi != null && mGameUi.Pictures.IsWindowPictureVisible)
        || miPictureHiddenFrame == Time.frameCount;

    /// <summary>The frame a press hid the window picture.</summary>
    private int miPictureHiddenFrame = -1;

    /// <summary>Update comes past its modal gates to the paging and the window picture, in its order.</summary>
    private bool fReachesAnyKeyWaits => !IsChanting && !(mControlScheme != null && mControlScheme.IsWorldInputBlocked)
        && !(mConversationScreen != null && mConversationScreen.IsOpen) && !IsAskingYesNo;

    /// <summary>Shows a text page by page below what the box holds; psLeading come before it and
    /// count as printed (UWMessageLog.ShowPagedText).</summary>
    public void ShowPagedText(string psText, params string[] psLeading)
    {
        mOLog.ShowPagedText(psText, psLeading);
    }

    /// <summary>Whether the running question shows a blinking text cursor. A yes/no question
    /// does not (per user from the original, 2026-09-17): its answer is chosen with Y and N,
    /// not typed.</summary>
    public bool ShowsPromptTextCursor
    {
        get { return IsPromptActive && !IsAskingYesNo; }
    }

    public bool IsPromptActive => mOLog.IsPromptActive;

    /// <summary>The running line a prompt keeps replacing (UWMessageLog.SetPromptMessage).</summary>
    public void SetPromptMessage(string psMessage)
    {
        mOLog.SetPromptMessage(psMessage);
    }

    public void EndPromptMessage()
    {
        mOLog.EndPromptMessage();
    }

    public void DropPromptMessage()
    {
        mOLog.DropPromptMessage();
    }

    /// <summary>A composed line for the message scroll; an empty one is dropped (UWMessageLog).</summary>
    public void AddMessage(string psMessage)
    {
        mOLog.AddMessage(psMessage);
    }

    /// <summary>Wipes the box and starts with a blank line - before the compass text
    /// (UWMessageLog.ClearMessages).</summary>
    public void ClearMessages()
    {
        mOLog.ClearMessages();
    }

    /// <summary>Wipes the box without the blank line - the save game messages
    /// (UWMessageLog.ResetMessages).</summary>
    public void ResetMessages()
    {
        mOLog.ResetMessages();
    }

    /// <summary>
    /// The thing under the action bar's crosshair for using an item on it - the middle of the
    /// game camera's view (UWWorldPicker.CrosshairRay), which in Classic Wide is not the middle of
    /// the screen (B18; until 2026-10-12 UWItemDrag measured Screen.width / 2).
    /// </summary>
    public UWEntityInfo TryGetEntityAtCrosshair(UWObject pOHeldItem)
    {
        return UWWorldPicker.Measure(UWWorldPicker.CrosshairRay()).Frontmost;
    }

    // ------------------------------------------------- Spells waiting for their target
    //
    // The waiting projectile and target spell are UWPendingSpell's since cleanup step 5.14
    // (2026-10-22); the surface their callers use stays here and forwards.

    /// <summary>Is a spell waiting for its target - a projectile for the DIRECTION, a target
    /// spell for a clicked OBJECT (UWPendingSpell)?</summary>
    public bool IsSpellTargeting => mOSpell != null && mOSpell.IsTargeting;

    /// <summary>Is a spell waiting for an object instead of a direction? The cursor is
    /// a different one then - see SpellTargetingCursor.</summary>
    public bool IsTargetSpellPending => mOSpell != null && mOSpell.IsTargetSpellPending;

    /// <summary>The pointer while a spell waits - the target pointer or the aiming one
    /// (UWPendingSpell.Cursor), for UWGameUI and UWModernHud.</summary>
    public int SpellTargetingCursor => mOSpell != null ? mOSpell.Cursor : UWHudRunes.SpellTargetCursor;

    /// <summary>Sends a waiting projectile spell off immediately (UWPendingSpell.Fire) - for
    /// wands, whose click already gave the direction.</summary>
    public void FirePendingSpell()
    {
        mOSpell.Fire();
    }

    /// <summary>Sends the queued projectile off from a spell trap's spot
    /// (UWPendingSpell.LaunchFromSpot).</summary>
    public bool LaunchPendingSpellFromSpot(int piTileX, int piTileY, int piXPos, int piYPos, int piZPos, int piHeading)
    {
        return mOSpell.LaunchFromSpot(piTileX, piTileY, piXPos, piYPos, piZPos, piHeading);
    }

    /// <summary>The modern scheme's aim after a cast from runes or an item
    /// (UWPendingSpell.AfterModernCast).</summary>
    public void AfterModernCast()
    {
        mOSpell.AfterModernCast();
    }

    /// <summary>Escape in the modern scheme: a waiting spell is let go (UWPendingSpell.Cancel).</summary>
    public void CancelPendingSpell()
    {
        mOSpell.Cancel();
    }

    /// <summary>Discards a queued projectile without throwing it - for a spell trap whose
    /// projectile could not take off (UWPendingSpell.CancelProjectile).</summary>
    public void CancelPendingProjectile()
    {
        mOSpell.CancelProjectile();
    }

    /// <summary>Starts a conversation with an NPC the player did not click on - a conversation
    /// trap, a critter that comes to talk, Garamon's burial (UWWorldUse.TryTalkToNpcData).</summary>
    public bool TryTalkToNpcData(UWObject pONpcData)
    {
        return mOWorldUse.TryTalkToNpcData(pONpcData);
    }

    /// <summary>Starts a conversation without a counterpart in the world - the talking door
    /// (UWWorldUse.BeginConversation).</summary>
    public bool BeginConversation(int piSlot)
    {
        return mOWorldUse.BeginConversation(piSlot);
    }

    /// <summary>Remembers a projectile spell until the player clicks the direction
    /// (UWPendingSpell.BeginProjectile).</summary>
    public void BeginSpellTargeting(int piProjectileId, int piDamage, int piSpeed, int piManaCost = 0)
    {
        mOSpell.BeginProjectile(piProjectileId, piDamage, piSpeed, piManaCost);
    }

    /// <summary>Remembers a spell of major class 7 or 11 until the player clicks its target
    /// (UWPendingSpell.BeginTarget).</summary>
    public void BeginTargetSpell(int piMajorClass, int piMinorClass)
    {
        mOSpell.BeginTarget(piMajorClass, piMinorClass);
    }

    /// <summary>Is a TARGET spell waiting for its target? A projectile spell does not count - it
    /// flies into the world and has no business in the inventory.</summary>
    public bool IsItemTargetSpellPending => IsTargetSpellPending;

    /// <summary>A click into an inventory while a target spell waits: B17's one rule with the
    /// clicked inventory's policy (UWPendingSpell.ResolveInInventory) - the classic one loses the
    /// spell on no item, the modern bags keep it waiting.</summary>
    public UWPendingSpellState.InventoryClickOutcome ResolveTargetSpellInInventory(UWObject pOItem,
        UWPendingSpellState.InventoryPolicy pePolicy)
    {
        return mOSpell.ResolveInInventory(pOItem, pePolicy);
    }

    /// <summary>
    /// Origin for look and use rays. With Original the mouse cursor
    /// itself points at the target, with Modern - where the cursor is invisible and locked - the
    /// screen centre acts as crosshair.
    /// </summary>
    internal Ray InteractionRay()
    {
        if (UWControlScheme.Ui.Policy.AimSource == UWAimSource.PointerInView)
            return Camera.main.ScreenPointToRay(UWPointer.Position);

        return UWWorldPicker.CrosshairRay();
    }

    /// <summary>
    /// Walls, floors and ceilings are merged into chunk meshes and therefore
    /// no longer have their own UWEntityInfo. Via the hit triangle index the
    /// tile can be found again and the description built from it.
    /// </summary>
    internal string DescribeChunkHit(RaycastHit pOHit)
    {
        if (mOUWDataImporter == null)
            return null;

        UWLevelChunk lOChunk = pOHit.collider.GetComponent<UWLevelChunk>();

        if (lOChunk == null)
            return null;

        int liTileIndex = lOChunk.GetTileIndex(pOHit.triangleIndex);

        if (liTileIndex < 0 || lOChunk.LevelIndex >= mOUWDataImporter.Levels.Count)
            return null;

        UWTile lOTile = mOUWDataImporter.Levels[lOChunk.LevelIndex].TileData[liTileIndex];

        // The surface normal tells whether floor, ceiling or wall was hit.
        float lfUp = Vector3.Dot(pOHit.normal, Vector3.up);

        int liTextureIndex;

        if (lfUp > 0.5f)
            liTextureIndex = 511 - lOTile.TextureFloor;
        else if (lfUp < -0.5f)
            liTextureIndex = 511;
        else
            liTextureIndex = lOTile.TextureWall + 1;

        try
        {
            return mOUWDataImporter.GetTextureDescription(liTextureIndex);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>What the pad's A (E) would do with what the crosshair is on, for the button hint
    /// under it (UWModernWorldInput.GetCrosshairPadVerb).</summary>
    public string GetCrosshairPadVerb()
    {
        return mOModernInput.GetCrosshairPadVerb();
    }

    /// <summary>What the crosshair is on, for the label under it
    /// (UWModernWorldInput.GetCrosshairTargetName).</summary>
    public string GetCrosshairTargetName()
    {
        return mOModernInput.GetCrosshairTargetName();
    }
}
