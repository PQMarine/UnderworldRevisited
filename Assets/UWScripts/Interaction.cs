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
public class Interaction : MonoBehaviour
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

    /// <summary>String block 1: "You see nothing." - the look at what is not there.</summary>
    private const int NothingToSeeMessage = 155;

    /// <summary>Radius of the fallback check for close range, see fFindModernMeleeTarget.</summary>
    private const float CloseCombatRadius = 12f;

    /// <summary>Above this height in the view window lies the upper third, below
    /// LowerThirdFraction the lower one. 0 is the bottom.</summary>
    private const float UpperThirdFraction = 2f / 3f;

    private const float LowerThirdFraction = 1f / 3f;

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
    private UWConversationScreen mConversationScreen;
    private UWEyesDisplay mEyes;

    /// <summary>A strike is running and waiting for its hit frame.</summary>
    private bool mbAttackPending;

    /// <summary>The current right-button press began in the viewport - only such a press charges
    /// the weapon (see Update).</summary>
    private bool mbRightPressInViewport;

    /// <summary>The weapon button's press came from the gamepad's trigger (UWGamepad): it strikes
    /// at the crosshair whatever the mouse pointer lies on.</summary>
    private bool mbAttackFromPad;

    private float mfPendingCharge;

    private AttackEnum mePendingAttack;

    /// <summary>The clicked ninth of the view, 1 to 9 from the bottom left (player_attack's swing,
    /// resolve_attack's towhere) - chosen with the attack kind (fChooseAttackKind), kept for the
    /// hit frame like the kind.</summary>
    private int miCurrentNinth = MiddleNinth;

    private int miPendingNinth = MiddleNinth;

    /// <summary>The middle ninth, 5 - the slash in the middle column.</summary>
    private const int MiddleNinth = 5;

    private UWControlScheme mControlScheme;
    private List<string> mOActiveStrings;
    /// <summary>The right button in the 3D view: pressed once, read once - see UWWorldPointer.</summary>
    private readonly UWWorldPointer mOPointer = new UWWorldPointer();
    private UWInventory mInventory;
    private AttackEnum mCurrentAttack;

    /// <summary>The charge of the blow, 0 to 100 - player_attack's play_pow (see fUpdateCharge).</summary>
    private int miPlayPow;

    /// <summary>Seconds gathered towards the next charge step; below 0 until the first frame
    /// after the wind-up (player_attack's elapsed of -1).</summary>
    private float mfChargeElapsed = -1f;

    /// <summary>What one charge step adds - the melee table's speed of the weapon in hand.</summary>
    private int miChargeSpeed;

    private UWControls.PlayerActions mInput;

    public AttackEnum CurrentAttack
    {
        get { return mCurrentAttack; }
    }

    /// <summary>The player's inventory beside this component - for UWWorldPickup and
    /// UWItemTargetUse, which act for it.</summary>
    public UWInventory Inventory => mInventory;

    /// <summary>The level loader, the game data and the character this component works with -
    /// for UWItemTargetUse, which acts for it (cleanup step 2.4, 2026-10-12).</summary>
    public UWLevelLoader LevelLoader => mLevelLoader;

    public UWDataImport.DataImport GameData => mOUWDataImporter;

    public UWCharacter Character => mCharacter;

    /// <summary>For UWGameUI (Original: message log in the lower frame box, see
    /// fBuildCanvas/MessageLog) - the same list that AddMessage fills.</summary>
    public IReadOnlyList<string> ActiveStrings
    {
        get { return mOActiveStrings; }
    }

    /// <summary>Messages the list keeps: as many as the modern scroll's tallest setting
    /// (UWModernScroll.MaxLines).</summary>
    public const int MessageHistoryCount = UWModernScroll.MaxLines;

    /// <summary>Messages the modern message box shows: the original's five, or the layout editor's
    /// Lines (UWUserSettings.ModernMessageLines; issue 4, 2026-10-21), as few as the scroll's
    /// fewest and as many as the list keeps.</summary>
    public static int ModernMessageCount => Mathf.Clamp(
        UWUserSettings.ModernMessageLines > 0 ? UWUserSettings.ModernMessageLines : DefaultModernMessageCount,
        UWModernScroll.MinLines, MessageHistoryCount);

    public const int DefaultModernMessageCount = 5;

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

    /// <summary>The open right-button gesture - what the press decided it to be (UWClickRules).</summary>
    private UWClickRules.GestureKind meGesture;

    /// <summary>Nothing, or an entity that only stands for geometry - the door frame carries a
    /// UWEntityInfo with the wall texture for the look but no object (see
    /// UWObjectSpawner.fSpawnDoorFrames). The modes treat it like a wall (per user on the
    /// original, 2026-09-19: the lintel gets "You cannot use that.", not the reach refusal).</summary>
    private static bool fIsGeometry(UWEntityInfo pOTarget)
    {
        return pOTarget == null || pOTarget.ObjectData == null;
    }

    private static bool fIsCreature(UWEntityInfo pOTarget)
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
    /// gesture in the viewport in its own way, see fBeginWorldPointer.
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

        mOPointer.Cancel();
        meGesture = UWClickRules.GestureKind.None;
        CommandMode = leNext;
    }

    /// <summary>
    /// Is attack in progress?
    /// </summary>
    public bool IsAttacking { get; set; }

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
    /// </summary>
    public float AttackCharge
    {
        get { return miPlayPow / (float)UWCombat.FullCharge; }
    }

    /// <summary>
    /// The aiming cursor when shooting.
    ///
    /// A LAUNCHER HAS NO CHARGE, BY THE USER'S DECISION OF 2026-10-12 TO FOLLOW THE ORIGINAL
    /// (combat C2): in fire mode player_attack (COMBAT.C) waits for the same wind-up as any
    /// weapon; when the weapon reaches its hold frame (weap_frame 3) the gem jumps to frame 9 and
    /// the crosshair appears, and only a release after that shoots - released earlier, nothing
    /// flies. The user's "about half a second, bow and sling alike" (2026-09-10) is that
    /// wind-up. Until then ours took the reference's reading - the crosshair at the masked melee
    /// entry's minimum charge, 0.05 to 0.08 s after the press - so arrows went off at the
    /// clicking speed.
    /// </summary>
    public bool IsRangedTargeting
    {
        get
        {
            return IsCombatModeActive && IsAttacking && mCharacter != null && mCharacter.Weapon.HasRangedWeapon
                && mCharacter.Weapon.IsWindupComplete;
        }
    }

    /// <summary>
    /// player_attack's charge while the button is held: nothing during the wind-up; on the
    /// first frame after it the clock starts (elapsed -1 to 0), then every
    /// AttackChargeStepSeconds the weapon's speed is added, capped at 100. A launcher never
    /// charges (fire mode returns before this).
    /// </summary>
    private void fUpdateCharge(float pfDeltaSeconds)
    {
        if (!IsAttacking || (mCharacter != null && (!mCharacter.Weapon.IsWindupComplete || mCharacter.Weapon.HasRangedWeapon)))
            return;

        if (mfChargeElapsed < 0f)
        {
            mfChargeElapsed = 0f;

            return;
        }

        float lfStep = UnderworldRevisited.UWSettings.Instance != null
            ? UnderworldRevisited.UWSettings.Instance.AttackChargeStepSeconds
            : DefaultChargeStepSeconds;

        if (lfStep <= 0f)
            lfStep = DefaultChargeStepSeconds;

        mfChargeElapsed += pfDeltaSeconds;

        while (mfChargeElapsed > lfStep)
        {
            miPlayPow = Mathf.Min(UWCombat.FullCharge, miPlayPow + miChargeSpeed);
            mfChargeElapsed -= lfStep;
        }
    }

    /// <summary>One charge step without a settings asset - see UWSettings.AttackChargeStepSeconds.</summary>
    private const float DefaultChargeStepSeconds = 0.072f;

    private void Awake()
    {
        mGameUi = GetComponent<UWGameUI>();
        mControlScheme = GetComponentInParent<UWControlScheme>();
        mCharacter = GetComponent<UWCharacter>();

        if (mCharacter != null)
            mCharacter.Weapon.AttackConnected += fOnAttackConnected;

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
        mConversationScreen = GetComponent<UWConversationScreen>();

        if (mConversationScreen == null)
            mConversationScreen = gameObject.AddComponent<UWConversationScreen>();
    }

    private void Start()
    {
        mOActiveStrings = new List<string>();
        mCurrentAttack = AttackEnum.Idle;
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
            UWMusic.PickLevelTheme();
        }

        if (mOUWDataImporter == null)
        {
            if (mLevelLoader == null)
                mLevelLoader = UWScene.LevelLoader;

            if (mLevelLoader != null)
                mOUWDataImporter = mLevelLoader.UWDataImporter;
        }

        // Pressing again during the strike animation starts the next strike afterwards
        // (per user, 2026-08-30). Instead of keeping a queue, it simply
        // starts as soon as the weapon is ready again and the button is still held.
        // THIS BLOCK COMES BEFORE THE MODAL CHECK further down and therefore needs it
        // itself: without it, a still-held Interact button during a
        // detached camera still triggered a strike including the weapon animation.
        // WHERE THE RIGHT BUTTON WENT DOWN decides for the whole time it is held (per user on the
        // original, 2026-09-19): a press over the UI never charges the weapon, not even while
        // the button stays down and the weapon becomes ready. Read on the press frame, so the
        // held-button path below and the press path further down agree.
        // THE WEAPON BUTTON: the right one in the original, the left one in the modern scheme
        // (concept per user, 2026-10-03), where the right button looks.
        InputAction lOAttackButton = fGetAttackButton();

        if (lOAttackButton.WasPressedThisFrame())
        {
            // While the gamepad's pointer drives, its trigger is the left mouse button
            // (UWGamepadPointer.Takes): the strike goes where the pointer is, as with the mouse.
            mbAttackFromPad = UWGamepad.IsFromGamepad(lOAttackButton) && !UWGamepadPointer.Takes(lOAttackButton);

            // The modern scheme: on the world, not on a window of its UI (a drag from a bag onto
            // the world must not strike).
            mbRightPressInViewport = UWControlScheme.IsModern ? mbAttackFromPad || fMouseInWorld()
                : mGameUi == null || mGameUi.IsScreenPositionInGameArea(Mouse.current.position.ReadValue());
        }

        if (IsCombatModeActive && !IsAttacking && !mbAttackPending && mbRightPressInViewport
            && mCharacter != null && mCharacter.Weapon.CanAttack && lOAttackButton.IsPressed() && !UWPlayerInput.ConsumesRightClick()
            && (mControlScheme == null || !mControlScheme.IsWorldInputBlocked) && (mbAttackFromPad || fMouseInWorld()))
            fBeginAttack();

        // The weapon in hand follows the equipment. This is checked continuously because the
        // weapon can be changed in the original even in the middle of combat mode - then first the
        // old one is sheathed and then the new one drawn (see
        // UWWeaponSwing.SetWeaponType). The call is cheap: it only does something when the
        // weapon has actually changed.
        if (IsCombatModeActive)
            fUpdateWeaponType();

        // THE MANTRA INPUT COMES BEFORE THE MODAL CHECK: it holds the world itself (modal hold
        // "typed input"), so behind the check it would never see its own Enter - the input
        // could not be left (per user, 2026-09-26, right after the hold came in). The same for
        // the swallow of an input a mouse button ended.
        fUpdateTypedInputSwallow();

        if (fUpdateMantraPrompt())
            return;

        // Q OVER THE INTERFACE IN A CONVERSATION (per user, 2026-10-14: the bags' menu could look
        // at a thing during a conversation, Q could not): the conversation holds the world, not
        // the modern windows, so Q looks at the thing in the slot under the pointer there too.
        if (UWControlScheme.IsModern && mConversationScreen != null && mConversationScreen.IsOpen)
            fUpdateModernLookOverUi();

        // While a modal UI panel is open (currently only the Modern inventory),
        // combat/interaction should pause - otherwise e.g. a right click on a
        // backpack slot would also count as attack/use in the 3D world.
        if (mControlScheme != null && mControlScheme.IsWorldInputBlocked)
            return;

        // A running conversation is modal: while it is open, nothing happens in the world.
        if (mConversationScreen != null && mConversationScreen.IsOpen)
            return;

        // The trap question likewise - it waits for yes or no (see fTryDetectTrap), and
        // the repair question the same.
        if (fUpdateYesNoPrompt())
            return;

        // A conversation is modal - and so is a long text: if it does not fit into the
        // message box at once, the game waits after each page for a click or a
        // key before continuing (per user, 2026-09-03).
        if (fUpdatePagedText())
            return;

        // A displayed window picture is modal (observed by user, 2026-08-29: the
        // mouse cursor disappears as with a full-screen cutscene): the next
        // key press or click only hides it again and triggers nothing else.
        if (mGameUi != null && mGameUi.IsWindowPictureVisible)
        {
            // The gamepad too, a stick pushed out as well - walking off hides it.
            if (fAnyInputThisFrame() || UWGamepad.AnyPressed(true))
            {
                UWGamepad.ConsumePress();
                mGameUi.HideWindowPicture();
            }

            return;
        }

        // DURING USE MODE (UWInventory.UseModeItem) the right click in the 3D view puts the held item
        // onto what is there - read by exactly one class, this one, so that no second reading of
        // the same click can follow (2026-08-27: the look text appeared twice when using a key).
        // Clicks on the inventory during use mode stay with UWItemDrag.
        if (mInventory != null && mInventory.UseModeItem != null)
        {
            fUpdateUseModeInWorld();
            return;
        }

        // A spell still waiting for its target intercepts the next click.
        if (fUpdateSpellTargeting())
            return;

        // The three arrows under the compass move, a click on the compass itself prints the
        // status - both use the click up. The arrows come first because they lie below the
        // disc and a miss there should not report the status. The arrows also take the HELD
        // button, because holding one repeats the step (UWPlayerInput.HoldEasyMovement).
        // (The compass belongs to the classic frame, which the modern scheme does not show.)
        if (!UWControlScheme.IsModern && (mInput.Interact.IsPressed() || UWMouseButtons.LeftHeld))
        {
            if (fTryEasyMovement(Mouse.current.position.ReadValue()))
                return;
        }

        if (!UWControlScheme.IsModern && (mInput.Interact.WasPressedThisFrame() || UWMouseButtons.LeftPressed))
        {
            if (fTryReportStatus(Mouse.current.position.ReadValue()))
                return;
        }

        // THE MODERN SCHEME'S KEYS: E, R, the right button to look, the left to draw.
        if (UWControlScheme.IsModern)
            fUpdateModernActions();

        // A right click that jumps during a cursor movement is used up (see
        // UWPlayerInput.ConsumesRightClick).
        bool lbRightClickFree = !UWPlayerInput.ConsumesRightClick();

        if (lbRightClickFree && !UWControlScheme.IsModern && mInput.Interact.WasPressedThisFrame())
        {
            // IN COMBAT MODE THE RIGHT BUTTON IS THE WEAPON AND NOTHING ELSE (per user, 2026-09-18):
            // no look, no use - and no striking while sheathing or drawing either, in the original
            // the draw motion must finish first (per user, 2026-08-30). With something on the
            // pointer the click places it (UWItemDrag), it does not look.
            if (IsCombatModeActive)
            {
                // THE WEAPON CHARGES ONLY FROM A PRESS IN THE VIEWPORT (per user on the original,
                // 2026-09-19, correcting the first reading that combat mode locked everything):
                // over the UI the right button does what it always does there, in combat mode too.
                if (mbRightPressInViewport && (mCharacter == null || mCharacter.Weapon.CanAttack))
                    fBeginAttack();
            }
            else if (mInventory == null || mInventory.CursorItem == null)
                fBeginWorldPointer();
        }
        else if (lbRightClickFree && lOAttackButton.WasReleasedThisFrame() && IsCombatModeActive && IsAttacking)
        {
            // Releasing starts the strike animation; whether it hits is only decided
            // when it reaches its hit frame (see UWWeaponSwing.AttackConnected).
            // The charge and the attack type must therefore be kept until then -
            // both are reset just below.
            // The charge counter is passed on unchanged; the conversion to the
            // weapon's damage factor only happens on the hit (UWCombat.MapCharge).
            // Previously this additionally counted from the minimum charge here - that counted the
            // factor twice.
            mfPendingCharge = AttackCharge;
            mePendingAttack = mCurrentAttack;
            miPendingNinth = miCurrentNinth;

            // Whether the release becomes a strike depends solely on the windup:
            // if it has completed, the strike happens, otherwise it plays backwards
            // and the weapon returns to ready (checked by user in the original,
            // 2026-09-01). The minimum charge does NOT decide this - it is the
            // lower bound of the damage factor. A strike from a barely charged weapon
            // does happen, but does little.
            mbAttackPending = mCharacter == null || mCharacter.Weapon.IsWindupComplete;

            // A LAUNCHER SHOOTS ON RELEASE, not at a hit frame: PlayerCombat_seg022_11BA calls
            // MissileRelease_seg025_9F the moment the button comes up, with the pointer in the
            // view window (seg013_1CC9_115) - twang, launch sound and the no-room message come at
            // once (per user on the original, 2026-09-24; ours came a little late, with the
            // strike animation). But only once the aiming cursor is up (IsRangedTargeting, the
            // same state that switches the cursor there: the wind-up is through); released
            // earlier, nothing is shot (per user on the original; COMBAT.C player_attack).
            if (fHoldsLauncher())
            {
                bool lbDrawn = IsRangedTargeting;

                mbAttackPending = false;

                if (lbDrawn)
                    fResolveAttack();
            }

            // The character only learns THAT the button was released. Whether that becomes a retraction
            // or a strike depends on its windup state; whether the
            // strike hits is decided by mbAttackPending here.
            if (mCharacter != null)
                mCharacter.Weapon.ReleaseAttack();

            IsAttacking = false;
            fClearCharge();
            mOPointer.Cancel();
        }
        else if (lOAttackButton.IsPressed())
        {
            fUpdateCharge(Time.deltaTime);
        }
        else if (!lOAttackButton.IsPressed())
        {
            if (IsAttacking)
            {
                fClearCharge();
                IsAttacking = false;
            }
        }

        fUpdateWorldPointer();

        // Original: the log in the lower frame box shows the last 5 messages, new ones
        // appended at the bottom, old ones dropped at the top (see UWGameUI.fRefreshMessageLog for the
        // actual Original display - here only the capping of the list itself). The list keeps
        // MessageHistoryCount, as many as the modern interface's scroll can be made tall
        // (per user, 2026-10-09: eight lines showed only five); the classic box keeps the newest
        // that fit, the modern message box the newest ModernMessageCount.
        if (mOActiveStrings.Count > MessageHistoryCount)
        {
            mOActiveStrings.RemoveAt(0);

            // A running input line moves up with the rest. Until 2026-09-26 its index stayed,
            // so with a full scroll it pointed past the end, the next refresh appended a new
            // line and the old one stayed behind - "Move how many? 1" repeated line after line
            // while a stack was dragged (per user with a screenshot).
            if (miPromptLine >= 0)
                miPromptLine--;

            // The message scroll moves on: a dragon scrolls along (UW.EXE seg043_37F0_1CA).
            UWHudDragons.Request(UWHudDragons.ScrollAnimation);
        }
    }

    // ------------------------------------------------- The right button in the 3D view

    /// <summary>
    /// The press: what lies under the pointer is measured ONCE - the frontmost thing (or the
    /// wall or floor texture), and the pickable thing behind a blood pool or bone pile (one
    /// UWWorldPicker measurement). Until 2026-09-18 UWItemDrag ran its own raycast and its own click
    /// threshold on the same button, and the two disagreed on small movements.
    /// </summary>
    private void fBeginWorldPointer()
    {
        Vector2 lOMousePos = Mouse.current.position.ReadValue();
        UWWorldPointer.Aim lOAim = new UWWorldPointer.Aim { PressPosition = lOMousePos };

        // With Original, outside the small 3D view window (e.g. right click on the backpack or
        // paper doll panel, see UWItemDrag) no world raycast - the UI does not block the physics
        // query, a raycast from there would hit invisible geometry behind the UI.
        bool lbOutsideGameArea = !UWControlScheme.IsModern
            && mGameUi != null && !mGameUi.IsScreenPositionInGameArea(lOMousePos);

        // BEYOND THE SIGHT NOTHING IS THERE: the original draws only as deep as the light allows,
        // and what is not drawn cannot be aimed at - neither looked at nor used (see
        // fIsWithinSight). The aim then stays empty and the look answers "You see nothing.".
        if (!lbOutsideGameArea)
        {
            UWWorldPicker.Measurement lOMeasured = UWWorldPicker.Measure(fGetInteractionRay());

            if (lOMeasured.HasFrontmost && fIsWithinSight(lOMeasured.FrontmostHit))
            {
                lOAim.Frontmost = lOMeasured.Frontmost;
                lOAim.ChunkDescription = lOAim.Frontmost == null ? fDescribeChunkHit(lOMeasured.FrontmostHit) : null;
                lOAim.Pickable = lOMeasured.Pickable;
            }
        }

        mOPointer.Begin(lOAim);

        // WHAT THE PRESS DOES is a rule, not code here: UWClickRules knows the mode, the kind of
        // target and whether the press was in the viewport, and says what to do and whether the
        // gesture stays open (measured by the user on the original, 2026-09-18 and 19).
        fApply(UWClickRules.OnPress(CommandMode, fClassify(lOAim), !lbOutsideGameArea), lOAim);
    }

    /// <summary>The kind of thing under the pointer, as UWClickRules tells them apart. A
    /// pickable thing counts as such even behind a blood pool or a bone pile.</summary>
    private static UWClickRules.TargetKind fClassify(UWWorldPointer.Aim pOAim)
    {
        if (pOAim.Pickable != null)
            return UWClickRules.TargetKind.Pickable;

        if (pOAim.Frontmost == null)
            return pOAim.ChunkDescription != null ? UWClickRules.TargetKind.Geometry : UWClickRules.TargetKind.Nothing;

        if (fIsGeometry(pOAim.Frontmost))
            return UWClickRules.TargetKind.Geometry;

        return fIsCreature(pOAim.Frontmost) ? UWClickRules.TargetKind.Creature : UWClickRules.TargetKind.Thing;
    }

    /// <summary>Carries out what UWClickRules decided, on the aim of the press. The actions are
    /// the public ones of this class - the command icons of the original call the same.</summary>
    private void fApply(UWClickRules.Decision pODecision, UWWorldPointer.Aim pOAim)
    {
        switch (pODecision.Action)
        {
            case UWClickRules.ActionKind.Look:
                LookAtTarget(pOAim.Frontmost, pOAim.ChunkDescription);
                break;

            case UWClickRules.ActionKind.Message:
                AddGeneralMessage(pODecision.MessageIndex);
                break;

            case UWClickRules.ActionKind.ConversationMessage:
                fAddConversationBlockMessage(pODecision.MessageIndex);
                break;

            case UWClickRules.ActionKind.TakeToPointer:
            case UWClickRules.ActionKind.BeginDrag:
                // Both put the thing onto the pointer within the hand's reach ("That is too far
                // away to take." otherwise); the drag of the default state additionally marks the
                // gesture as used up, so the release does not use the thing as well.
                if (pOAim.Pickable != null && UWScene.ItemDrag != null)
                {
                    if (!TryReachForPickup(pOAim.Pickable))
                        mOPointer.DragConsumed = true;
                    else if (UWScene.ItemDrag.TryBeginWorldDrag(pOAim.Pickable))
                        mOPointer.DragConsumed = true;
                }
                break;

            case UWClickRules.ActionKind.Talk:
                TalkTo(pOAim.Frontmost);
                break;

            case UWClickRules.ActionKind.TalkToThing:
                fTalkToThing(pOAim.Frontmost ?? pOAim.Pickable);
                break;

            case UWClickRules.ActionKind.Use:
                UseTarget(pOAim.Frontmost);
                break;
        }

        meGesture = pODecision.Gesture;

        if (meGesture == UWClickRules.GestureKind.None)
            mOPointer.Cancel();
    }

    /// <summary>
    /// The gesture after the press: the drag threshold and the release, both answered by
    /// UWClickRules (which knows the gesture the press opened). The threshold decides (checked
    /// by user in the original, 2026-09-18): from the sixth pixel a pickable thing follows the
    /// pointer at once, anything else is used ON THE RELEASE after the threshold was crossed;
    /// within the threshold one may jitter. A remembered creature or thing (talk and use mode)
    /// is acted on by any release.
    /// </summary>
    private void fUpdateWorldPointer()
    {
        if (!mOPointer.IsPressed)
            return;

        // Combat mode switched on, or something put on the pointer, while the button was held:
        // the gesture is over, the release must not use anything.
        if (IsCombatModeActive || (mInventory != null && mInventory.CursorItem != null))
        {
            mOPointer.Cancel();
            meGesture = UWClickRules.GestureKind.None;
            return;
        }

        UWWorldPointer.Aim lOAim = mOPointer.Current;

        UWWorldPointer.EventKind leEvent = mOPointer.Poll(Mouse.current.position.ReadValue(),
            mInput.Interact.WasReleasedThisFrame());

        // Modern has no dragging, its pickup sits in UseTarget.
        if (leEvent == UWWorldPointer.EventKind.DragStarted && !UWControlScheme.IsModern)
            fApply(UWClickRules.OnDragThreshold(meGesture, fClassify(lOAim)), lOAim);

        // A drag that starts and ends in the same frame is a release after a drag too.
        bool lbReleased = leEvent == UWWorldPointer.EventKind.ReleasedAsClick
            || leEvent == UWWorldPointer.EventKind.ReleasedAfterDrag
            || (leEvent == UWWorldPointer.EventKind.DragStarted && !mOPointer.IsPressed);

        if (lbReleased)
        {
            fApply(UWClickRules.OnRelease(meGesture, leEvent != UWWorldPointer.EventKind.ReleasedAsClick,
                mOPointer.DragConsumed), lOAim);
        }
    }

    /// <summary>The button that charges and releases the weapon: the right one in the original,
    /// the left one in the modern scheme - or the gamepad's right trigger (ModernAttack).</summary>
    private InputAction fGetAttackButton()
    {
        return !UWControlScheme.IsModern ? mInput.Interact : mInput.ModernAttack;
    }

    // ------------------------------------------------- The modern scheme's keys

    /// <summary>Is the modern pointer free (UWModernPointer)?</summary>
    private bool fIsPointerFree()
    {
        return mControlScheme != null && mControlScheme.IsPointerFree;
    }

    /// <summary>Do the mouse buttons act in the world? With the pointer locked, and - when the
    /// right button is held to look around and the pointer is free otherwise - also on the world
    /// outside the modern UI's windows (UWModernPointer.WorldTakesClicks); with the weapon drawn
    /// the free pointer on the world strikes too (per user, 2026-10-04 - it did nothing).</summary>
    private bool fMouseInWorld()
    {
        return !fIsPointerFree() || UWModernPointer.WorldTakesClicks()
            || (IsCombatModeActive && UWModernPointer.FreePointerOnWorld());
    }

    /// <summary>
    /// THE MODERN SCHEME (concept per user, 2026-10-03): R draws or puts away the weapon (the
    /// left button only charges and strikes with it drawn - it drew too until the same day), Q
    /// looks at what the crosshair is on (the right button switches the pointer,
    /// UWModernPointer). E TAPPED does the default - what the classic right-button drag does
    /// (fModernUse); E HELD uses directly, even what could be picked up - a sack is emptied
    /// instead of taken (per user, the same day). The tap acts on the release, the hold once
    /// ModernUseHoldSeconds have passed. The rules are the original's; only the way to them is
    /// new. Nothing here while something hangs on the pointer.
    /// </summary>
    private void fUpdateModernActions()
    {
        if (mInventory != null && mInventory.CursorItem != null)
        {
            mfModernUseHeld = -1f;
            return;
        }

        // Only R draws the weapon: the left button no longer does (per user, 2026-10-03), so
        // outside combat it is free; drawn, it charges and strikes (Update, fMouseInWorld).
        // The gamepad's right trigger draws the weapon as well (UWGamepad): held on, it charges
        // once the weapon is ready (Update). The mouse button does not draw.
        bool lbPadDraws = !IsCombatModeActive && mInput.ModernAttack.WasPressedThisFrame()
            && UWGamepad.IsFromGamepad(mInput.ModernAttack) && !UWGamepadPointer.Takes(mInput.ModernAttack);

        if (mInput.ModernReady.WasPressedThisFrame() || lbPadDraws)
            ToggleCombatMode();

        // THE FREE POINTER OVER THE INTERFACE (per user, 2026-10-07): Q looks at the thing in the
        // slot under the pointer - character panel, bags, action bar -, E tapped uses it there
        // and E held does nothing (fUpdateModernUseKey); until then both fell back to what the
        // crosshair aimed at in the world.
        bool lbOverUi = fIsPointerFree() && Mouse.current != null
            && UWModernPointer.IsOverUi(Mouse.current.position.ReadValue());

        // Q looks (per user, 2026-10-03) - the right button switches the pointer
        // (UWModernPointer).
        // The gamepad's pointer takes its X as the right button (UWGamepadPointer.Takes).
        if (mInput.ModernLook.WasPressedThisFrame() && !UWGamepadPointer.Takes(mInput.ModernLook))
        {
            if (lbOverUi)
                fLookAtUi(Mouse.current.position.ReadValue());
            else
            {
                fAimAtModern(out UWEntityInfo lOFrontmost, out string lsChunk, out UWEntityInfo _);
                LookAtTarget(lOFrontmost, lsChunk);
            }
        }

        fUpdateModernUseKey(lbOverUi);

        fUpdateModernPointerWorld();
    }

    // ------------------------------------------------- The free pointer in the world

    /// <summary>
    /// THE FREE POINTER ON THE WORLD (per user, 2026-10-04, the MMO way): with the weapon put away,
    /// a left CLICK on a thing opens its MENU (TryOpenWorldMenu - on nothing, a wall or the floor
    /// nothing happens), held and DRAGGED it TAKES the thing onto the pointer (the bags open and it
    /// is put down where the button is let go - UWModernBags.BeginWorldDrag). The right button
    /// stays the pointer's everywhere outside the windows (a right click for the menu made it
    /// depend on what lay under the pointer). E and Q act under the free pointer as well
    /// (fAimAtModern). Not over a window of the modern UI, not with a thing on the pointer, not in
    /// the use mode, the split box or the menu.
    /// </summary>
    private void fUpdateModernPointerWorld()
    {
        Mouse lOMouse = Mouse.current;

        if (lOMouse == null || Camera.main == null || !fIsPointerFree() || IsCombatModeActive
            || UWModernBags.IsMenuOpen || UWModernBags.IsSplitting || UWModernQuestion.IsBlocking
            || (UWModernBags.Instance != null && UWModernBags.Instance.IsUsing))
        {
            mbWorldPress = false;
            return;
        }

        Vector2 lOPointer = lOMouse.position.ReadValue();

        if (UWMouseButtons.LeftPressed)
        {
            mbWorldPress = !UWModernPointer.IsOverUi(lOPointer);

            if (mbWorldPress)
            {
                mOWorldPressAt = lOPointer;
                fAimAt(Camera.main.ScreenPointToRay(lOPointer), out mOWorldPressFront, out _, out mOWorldPressPickable);
            }

            return;
        }

        if (!mbWorldPress)
            return;

        bool lbMoved = Vector2.Distance(lOPointer, mOWorldPressAt) >= WorldDragPixels;

        if (UWMouseButtons.LeftHeld && lbMoved)
        {
            mbWorldPress = false;

            UWEntityInfo lOTake = mOWorldPressFront != null && mOWorldPressFront.CanBePickedUp ? mOWorldPressFront : mOWorldPressPickable;

            if (lOTake != null && UWWorldPickup.Take(this, lOTake, UWWorldPickup.WholeStack, UWWorldPickup.Destination.Pointer)
                && UWModernBags.Instance != null)
                UWModernBags.Instance.BeginWorldDrag(mOWorldPressAt);

            return;
        }

        if (!UWMouseButtons.LeftHeld)
        {
            mbWorldPress = false;

            if (!lbMoved)
                TryOpenWorldMenu(mOWorldPressAt);
        }
    }

    /// <summary>Screen pixels the pointer moves before a press on the world becomes a drag.</summary>
    private const float WorldDragPixels = 6f;

    private bool mbWorldPress;

    private Vector2 mOWorldPressAt;

    private UWEntityInfo mOWorldPressFront;

    private UWEntityInfo mOWorldPressPickable;

    /// <summary>Where E and Q aim in the modern scheme: under the free pointer when it is on the
    /// world, else at the crosshair (per user, 2026-10-04).</summary>
    private Ray fGetModernAimRay()
    {
        if (fIsPointerFree() && Mouse.current != null && Camera.main != null)
        {
            Vector2 lOPointer = Mouse.current.position.ReadValue();

            if (!UWModernPointer.IsOverUi(lOPointer))
                return Camera.main.ScreenPointToRay(lOPointer);
        }

        return fGetInteractionRay();
    }

    private void fAimAtModern(out UWEntityInfo pOFrontmost, out string psChunk, out UWEntityInfo pOPickable)
    {
        fAimAt(fGetModernAimRay(), out pOFrontmost, out psChunk, out pOPickable);
    }

    /// <summary>The thing a menu would be for under a screen point: the frontmost thing, or the
    /// pickable one behind geometry; not a wall panel (a piece of wall).</summary>
    private UWEntityInfo fWorldThingAt(Vector2 pOPointer, out UWEntityInfo pOPickableBehind)
    {
        pOPickableBehind = null;

        if (Camera.main == null)
            return null;

        fAimAt(Camera.main.ScreenPointToRay(pOPointer), out UWEntityInfo lOFront, out string _, out UWEntityInfo lOPickable);

        UWEntityInfo lOTarget = !fIsGeometry(lOFront) ? lOFront : lOPickable;

        if (fIsGeometry(lOTarget) || lOTarget.ObjectData.ID == WallPanelObjectId || lOTarget.ObjectData.ID == WallPanelObjectId + 1)
            return null;

        pOPickableBehind = lOTarget == lOFront ? lOPickable : null;
        return lOTarget;
    }

    /// <summary>Whether a thing's menu can open on this spot.</summary>
    public bool HasWorldThingAt(Vector2 pOPointer)
    {
        return UWControlScheme.IsModern && (mInventory == null || mInventory.CursorItem == null) && !IsSpellTargeting
            && (UWModernBags.Instance == null || !UWModernBags.Instance.IsUsing)
            && fWorldThingAt(pOPointer, out UWEntityInfo _) != null;
    }

    /// <summary>A left click on a thing in the world with the free pointer: its menu
    /// (UWModernBags.OpenWorldMenu) - true if there was a thing.</summary>
    public bool TryOpenWorldMenu(Vector2 pOPointer)
    {
        if (!HasWorldThingAt(pOPointer) || UWModernBags.Instance == null)
            return false;

        UWEntityInfo lOTarget = fWorldThingAt(pOPointer, out UWEntityInfo lOPickable);
        bool lbCreature = fIsCreature(lOTarget);

        UWModernBags.Instance.OpenWorldMenu(lOTarget, lOPickable, lbCreature, lOTarget.CanBePickedUp && !lbCreature, pOPointer);
        return true;
    }

    /// <summary>The world menu's Use and Talk: the direct use of E held (fModernUseOn).</summary>
    public void ModernUseThing(UWEntityInfo pOTarget, UWEntityInfo pOPickableBehind)
    {
        fModernUseOn(true, pOTarget, pOPickableBehind);
    }

    /// <summary>How long E has to be held for the direct use.</summary>
    private const float ModernUseHoldSeconds = 0.4f;

    /// <summary>How long E has been held, -1 when it is not (or the press was taken elsewhere).</summary>
    private float mfModernUseHeld = -1f;

    private bool mbModernUseHoldFired;

    /// <summary>How far the hold of E has come, 0 to 1, for the HUD; -1 while there is none to
    /// show (not held, just tapped, or already fired).</summary>
    public float ModernUseHoldProgress => mfModernUseHeld >= 0.1f && !mbModernUseHoldFired && !mbModernUseOnUi
        ? Mathf.Clamp01(mfModernUseHeld / ModernUseHoldSeconds) : -1f;

    /// <summary>E went down with the free pointer over the interface: its tap uses the slot there,
    /// its hold does nothing.</summary>
    private bool mbModernUseOnUi;

    /// <summary>E tapped over the interface: the slot under the pointer is used, or nothing.</summary>
    private static void fUseAtUi(Vector2 pOPointer)
    {
        if (UWModernBags.Instance != null && UWModernBags.Instance.TryUseAt(pOPointer))
            return;

        if (UWModernActionBar.Instance != null)
            UWModernActionBar.Instance.TryUseAt(pOPointer);
    }

    /// <summary>Q over the interface alone, for a conversation, which holds the world: the free
    /// pointer over a window, nothing on the pointer.</summary>
    private void fUpdateModernLookOverUi()
    {
        if ((mInventory != null && mInventory.CursorItem != null) || Mouse.current == null
            || !mInput.ModernLook.WasPressedThisFrame() || UWGamepadPointer.Takes(mInput.ModernLook))
            return;

        Vector2 lOPointer = Mouse.current.position.ReadValue();

        if (UWModernPointer.IsOverUi(lOPointer))
            fLookAtUi(lOPointer);
    }

    /// <summary>Q over the interface: the thing in the slot under the pointer, or nothing.</summary>
    private static void fLookAtUi(Vector2 pOPointer)
    {
        if (UWModernBags.Instance != null && UWModernBags.Instance.TryLookAt(pOPointer))
            return;

        if (UWModernActionBar.Instance != null)
            UWModernActionBar.Instance.TryLookAt(pOPointer);
    }

    private void fUpdateModernUseKey(bool pbOverUi)
    {
        InputAction lOKey = mInput.ModernUse;

        // The gamepad's pointer takes its A as the click (UWGamepadPointer.Takes).
        if (lOKey.WasPressedThisFrame() && UWGamepadPointer.Takes(lOKey))
        {
            mfModernUseHeld = -1f;
            return;
        }

        if (lOKey.WasPressedThisFrame())
        {
            mfModernUseHeld = 0f;
            mbModernUseHoldFired = false;
            mbModernUseOnUi = pbOverUi;
            return;
        }

        if (mfModernUseHeld < 0f)
            return;

        // Begun on the world and the pointer went onto a window: that press is gone.
        if (!mbModernUseOnUi && pbOverUi)
        {
            mfModernUseHeld = -1f;
            return;
        }

        if (lOKey.IsPressed())
        {
            mfModernUseHeld += Time.unscaledDeltaTime;

            if (!mbModernUseHoldFired && mfModernUseHeld >= ModernUseHoldSeconds)
            {
                // Held over the interface: nothing (per user, 2026-10-07).
                mbModernUseHoldFired = true;

                if (!mbModernUseOnUi)
                    fModernUse(true);
            }

            return;
        }

        // Released (or lost while a panel held the world): a tap that did not reach the hold.
        if (lOKey.WasReleasedThisFrame() && !mbModernUseHoldFired)
        {
            if (!mbModernUseOnUi)
                fModernUse(false);
            else if (pbOverUi)
                fUseAtUi(Mouse.current.position.ReadValue());
        }

        mfModernUseHeld = -1f;
    }

    /// <summary>What lies under the crosshair, measured as the right button's press measures it
    /// (fBeginWorldPointer): the frontmost thing or the wall/floor texture, and the pickable thing
    /// behind a blood pool or bone pile. Beyond the sight nothing.</summary>
    private void fAimAtCrosshair(out UWEntityInfo pOFrontmost, out string psChunk, out UWEntityInfo pOPickable)
    {
        fAimAt(fGetInteractionRay(), out pOFrontmost, out psChunk, out pOPickable);
    }

    /// <summary>The same along any ray - the free pointer's (fGetModernAimRay).</summary>
    private void fAimAt(Ray lORay, out UWEntityInfo pOFrontmost, out string psChunk, out UWEntityInfo pOPickable)
    {
        pOFrontmost = null;
        psChunk = null;
        pOPickable = null;

        UWWorldPicker.Measurement lOMeasured = UWWorldPicker.Measure(lORay);

        if (!lOMeasured.HasFrontmost || !fIsWithinSight(lOMeasured.FrontmostHit))
            return;

        pOFrontmost = lOMeasured.Frontmost;
        psChunk = pOFrontmost == null ? fDescribeChunkHit(lOMeasured.FrontmostHit) : null;
        pOPickable = lOMeasured.Pickable;
    }

    /// <summary>
    /// The context key does WHAT THE CLASSIC RIGHT-BUTTON DRAG DOES (per user, 2026-10-03; the
    /// default gesture of UWClickRules): over something pickable the drag takes it - here
    /// straight into the backpack, as there is no pointer to put it on, with the reach of the
    /// hand ("That is too far away to take.") -; otherwise the release uses what was under the
    /// pointer: a creature is talked to, a door, switch, lever or container is used
    /// (UseTarget). Besides, a shrine takes the mantra and the chained princess answers as
    /// talking to her does (fTalkToThing). On a wall, the floor or nothing: "You cannot use
    /// that.", as the original's use mode says there.
    ///
    /// pbDirect (E held): no taking - the thing is used as the original's use mode uses it, and
    /// what has no use stays where it is.
    /// </summary>
    private void fModernUse(bool pbDirect)
    {
        fAimAtModern(out UWEntityInfo lOFrontmost, out string _, out UWEntityInfo lOPickable);
        fModernUseOn(pbDirect, lOFrontmost, lOPickable);
    }

    private void fModernUseOn(bool pbDirect, UWEntityInfo lOFrontmost, UWEntityInfo lOPickable)
    {
        UWEntityInfo lOTake = lOFrontmost != null && lOFrontmost.CanBePickedUp ? lOFrontmost : lOPickable;

        if (!pbDirect && lOTake != null && !fIsCreature(lOTake))
        {
            UWWorldPickup.Take(this, lOTake, UWWorldPickup.WholeStack, UWWorldPickup.Destination.Pack);

            return;
        }

        if (fIsGeometry(lOFrontmost))
        {
            // Geometry in front with something pickable behind it (direct use only - the default
            // took it above): that thing.
            if (lOPickable != null)
                UseTarget(lOPickable);
            else
                AddGeneralMessage(UWClickRules.CannotUseThatMessage);

            return;
        }

        int liObjectId = lOFrontmost != null && lOFrontmost.ObjectData != null ? lOFrontmost.ObjectData.ID : -1;
        UWClickRules.ThingTalkAnswer leThing = UWClickRules.TalkToThing(liObjectId,
            lOFrontmost != null && lOFrontmost.IsChainedPrincess);

        if (!fIsCreature(lOFrontmost) && leThing != UWClickRules.ThingTalkAnswer.CannotTalk)
        {
            fTalkToThing(lOFrontmost);
            return;
        }

        UseTarget(lOFrontmost);
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
    private bool fIsWithinSight(RaycastHit pOHit)
    {
        // A hair out of the surface, towards the eye: a wall face is drawn on the open tile in
        // front of it, and that is the row the band counts.
        return fIsWithinSight(pOHit.point + (pOHit.normal * 0.5f));
    }

    /// <summary>The same for a point that already lies on the tile that is drawn - a creature's
    /// feet, for addressing it (see UseTarget).</summary>
    private bool fIsWithinSight(Vector3 pOSurface)
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

    /// <summary>Use mode: the right button in the view window puts the held item onto what is
    /// there, UWItemDrag applies it (and keeps the clicks on the inventory - the anvil and the
    /// oil flask go the other way round). Outside the view window nothing happens here.</summary>
    private void fUpdateUseModeInWorld()
    {
        if (UWPlayerInput.ConsumesRightClick() || !mInput.Interact.WasPressedThisFrame() || UWScene.ItemDrag == null)
            return;

        Vector2 lOMousePos = Mouse.current.position.ReadValue();

        if (mGameUi != null && !mGameUi.IsScreenPositionInGameArea(lOMousePos))
            return;

        UWScene.ItemDrag.ApplyUseModeItemInWorld(lOMousePos);
    }

    // ------------------------------------------------- Actions
    //
    // What a gesture resolves to - and what the command icons of the original (Talk, Get, Look,
    // Use) will call once they exist. Each action is complete in itself: whoever calls it needs
    // no knowledge of the others.

    /// <summary>Look: the description of the thing, or of the wall or floor texture.</summary>
    public void LookAtTarget(UWEntityInfo pOTarget, string psChunkDescription)
    {
        if (pOTarget != null)
        {
            // UWObjectDescriptionFormatter builds the "You see ..." message from the raw
            // original string (see there for the "_"/"&" structure) - applies only to
            // object/door descriptions (string block 4), not to the chunk description (wall/floor
            // texture, string block 10) - that one gets the same sentence frame below.
            // Wall inscriptions already carry their finished wording and must not be embedded
            // in "You see ..." (see UWEntityInfo). A gravestone shows its inscription and its
            // picture instead of the description (reference look.cs -> gravestone.Use).
            if (!fTryShowGravestone(pOTarget))
            {
                mOActiveStrings.Add(fGetLookDescription(pOTarget));

                // SpecialLook (LOOK.C) after the look line: a closed door with its spike bit adds
                // "The door is spiked." (string 0x83) - per user on the original, 2026-10-13, the
                // door's own sentence comes first.
                string lsSpecial = fGetSpecialLookLine(pOTarget);

                if (lsSpecial != null)
                    mOActiveStrings.Add(lsSpecial);
            }

            // If an a_look trigger hangs on the look target, its trap text belongs directly
            // after the look message - for the orb on tile 58/13 first "You see an orb.", then
            // the crystal ball vision (checked by user in the original, 2026-08-28). The orb can
            // NOT be used, the chain deliberately hangs on looking. Deliberately NOT limited to
            // mfUseRange: looking has no range limit in the original, and the text belongs to
            // looking. A HIDDEN trigger additionally requires a Search skill check - the
            // difficulty is stored in the trigger itself (see UWTriggerSystem).
            UWTriggerSystem.TryFireLookTrigger(pOTarget.ObjectData, mLevelLoader, this,
                mCharacter != null ? mCharacter.Vitals.GetSkill(UWPlayerData.Skill.Search) : 0);

            // And a trap is spotted in the process, if one notices it - see fTryDetectTrap.
            fTryDetectTrap(pOTarget);

            // A window additionally shows a picture in the view window - the deeper the level,
            // the more lava (uw-formats.txt on CS400.N01: "look" graphics for windows to abyss
            // volcano core). ONLY FROM CLOSE BY: RectLook shows it for a look with detail, which
            // player_3dlook gives within 0x48 only (UWReachRules.LookDetailDistanceSquared,
            // 2026-10-15; until then from any distance).
            if (pOTarget.IsWindow && mGameUi != null && mLevelLoader != null
                && fReach(pOTarget, UWReachRules.LookDetailDistanceSquared, false, false) == ReachEnum.Reached)
                mGameUi.ShowWindowPicture(mLevelLoader.CurrentLevelIndex);
        }
        else if (!string.IsNullOrEmpty(psChunkDescription))
        {
            // WITH "You see": walls and floors report like this in the original too (per user,
            // 2026-09-14: "You see" was missing for the floor). The texture names in block 10
            // already carry their article ("a stone wall", "nothing").
            mOActiveStrings.Add(UWObjectDescriptionFormatter.FormatLookMessage(psChunkDescription));
        }
        else
        {
            // NOTHING THERE: too dark to see that far, or the eye goes past everything. The
            // original has its own sentence for it (per user, 2026-09-18), it does not stay silent.
            AddGeneralMessage(NothingToSeeMessage);
        }
    }

    /// <summary>
    /// Use: door, conversation partner, switch, object effect, inscription. The pickup is not
    /// here: Original drags, Modern takes the thing before it comes this far (fModernUseOn).
    /// Only within reach (see UWReachRules) - looking still works at any distance. Out of reach
    /// the original answers "You are unable to use that from here." (per user, 2026-09-18;
    /// until then we silently did nothing).
    /// </summary>
    public void UseTarget(UWEntityInfo pOTarget)
    {
        if (mbLogUseAttempts)
        {
            Debug.Log(string.Format("[Use] Target={0} Id={1} IsDoor={2}",
                pOTarget == null ? "none" : pOTarget.name,
                pOTarget == null || pOTarget.ObjectData == null ? -1 : pOTarget.ObjectData.ID,
                pOTarget != null && pOTarget.IsDoor));
        }

        // ADDRESSING A CREATURE HAS NO REACH OF ITS OWN (per user on the original, 2026-09-19):
        // it works as far as the eye sees, like the look command, not the twelve eighths of
        // the hand. Beyond the light's band the creature is not drawn and cannot be clicked at
        // all, so out of sight simply nothing happens - no refusal.
        bool lbCreature = fIsCreature(pOTarget);

        if (lbCreature)
        {
            if (!fIsWithinSight(pOTarget.transform.position))
                return;
        }
        else if (pOTarget != null && !TryReachTarget(pOTarget, null))
        {
            return;
        }

        if (pOTarget?.IsDoor == true)
        {
            fTryUseDoor(pOTarget);

            // A DOOR DOES NOT SWALLOW ITS a_use trigger. The reference calls the trigger
            // unconditionally after the door handling (use.cs). For us this went unnoticed so far
            // because of 216 reachable doors in the whole game EXACTLY ONE has an a_use trigger in
            // its chain - the talking door on level 6, tile 4/2 (see
            // UWObjectMechanics.DoTrapConversationAction).
            UWTriggerSystem.TryFireSwitch(pOTarget.ObjectData, mLevelLoader, this);
        }
        // An NPC with its own conversation slot is talked to instead of used. The slot is
        // stored on the object (npc_whoami); 0 means "none" and applies to all animals - on
        // level 1 exactly the three outcasts and one goblin have their own slot.
        else if (TalkTo(pOTarget))
        {
        }
        // A barrel, chest or nightstand fires its use trigger while spilling (see
        // SpillWorldContainer) - its first chain link must not swallow the use here
        // (UWWorldUseRules.UseTriggerFiresFirst).
        else if (pOTarget != null
            && UWWorldUseRules.UseTriggerFiresFirst(pOTarget.ObjectData.ID, pOTarget.ObjectData.GetCategory())
            && UWTriggerSystem.TryFireSwitch(pOTarget.ObjectData, mLevelLoader, this))
        {
            // Original: a used switch shows no message of its own, the door swinging shut is
            // the only feedback.
        }
        else if (fTryUseObjectEffect(pOTarget))
        {
            // The object's own effect (fountain, cauldron) - the message comes from there.
        }
        // A wall inscription shows its wording on USE too, not only on look (confirmed by
        // user, 2026-08-28).
        else if (pOTarget != null && pOTarget.ShowDescriptionVerbatim)
        {
            mOActiveStrings.Add(pOTarget.Description);
        }

        if (!lbCreature && pOTarget != null && !pOTarget.IsDoor)
            fCastThingSpell(pOTarget.ObjectData);
    }

    /// <summary>
    /// THE LAST STEP OF A USE FROM THE WORLD: a thing carrying a charged spell casts it with
    /// itself as the caster and spends a charge (UseObj's checkSpell with how 0, UnderworldExhumed
    /// obj/USEITEMS.C; UWItemSpellRules.TryCastFromWorldObject, 2026-10-15) - the sceptre on
    /// level 5 at 59/54 and the ruby on level 6 at 14/3. Not after food, a readable or a wand,
    /// which UseObj leaves to their own handlers.
    /// </summary>
    private void fCastThingSpell(UWObject pOThing)
    {
        if (pOThing == null || mLevelLoader == null || mLevelLoader.CurrentLevel == null)
            return;

        UWObject.ObjectCategoryEnum leCategory = pOThing.GetCategory();

        if (leCategory == UWObject.ObjectCategoryEnum.Comestibles || leCategory == UWObject.ObjectCategoryEnum.BooksAndScrolls)
            return;

        if (UWItemSpellRules.TryCastFromWorldObject(pOThing, mLevelLoader.CurrentLevel.Masterlist, true,
            out int liMajor, out int liMinor))
            UWTriggerSystem.CastSpellFromThing(pOThing, liMajor, liMinor, mLevelLoader, this);
    }

    /// <summary>Talk: the conversation with the thing, if it has one ("You get no response."
    /// for creatures without). False for anything that is not a creature.</summary>
    public bool TalkTo(UWEntityInfo pOTarget)
    {
        return fTryTalk(pOTarget);
    }

    /// <summary>Talk mode over a thing that is no creature - the shrine, Arial on her wall, or
    /// the refusal (UWClickRules.TalkToThing).</summary>
    private void fTalkToThing(UWEntityInfo pOTarget)
    {
        int liObjectId = pOTarget != null && pOTarget.ObjectData != null ? pOTarget.ObjectData.ID : -1;

        switch (UWClickRules.TalkToThing(liObjectId, pOTarget != null && pOTarget.IsChainedPrincess))
        {
            case UWClickRules.ThingTalkAnswer.ChantMantra:
                fBeginMantra();
                break;

            case UWClickRules.ThingTalkAnswer.NoReactionFromPrincess:
                AddGeneralMessage(UWClickRules.NoReactionFromPrincessMessage);
                break;

            case UWClickRules.ThingTalkAnswer.CannotTalk:
                fAddConversationBlockMessage(UWClickRules.CannotTalkToThatLine);
                break;
        }
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
        fClearCharge();
        IsAttacking = false;
        mbAttackPending = false;
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

    /// <summary>
    /// Talks to an NPC if there is a conversation for it.
    ///
    /// The slot is stored in the object itself (npc_whoami). With 0 the original takes the
    /// generic conversation of its kind: slot 256 + (object number - 64) (reference:
    /// conversationinitialisation.GetConversationNumber). Until 2026-09-13, 0 meant
    /// "none" for us - the wisp on level 4 (0x7A, conversation 314) stayed silent, in the original it
    /// talks (per user). If there is no conversation for the kind (animals), the caller continues with
    /// looking.
    /// </summary>
    private bool fTryTalk(UWEntityInfo pOTarget)
    {
        if (pOTarget == null || mConversationScreen == null || mOUWDataImporter == null)
            return false;

        UWNpc lONpc = pOTarget.ObjectData as UWNpc;
        int liSlot = GetConversationSlot(lONpc);

        if (fRefusesTalk(lONpc, liSlot))
            return true;

        return liSlot > 0 && mConversationScreen.Begin(mOUWDataImporter, lONpc, liSlot);
    }

    /// <summary>
    /// TALKTO'S REFUSALS (TalkTo_ovr100_0, CONVERSE.C), the one home for every way a conversation
    /// with a creature starts - the player's click and, since 2026-10-15, a conversation trap or a
    /// critter that comes to talk (TryTalkToNpcData; gronkify_talkto_player and crit_talk call
    /// TalkTo too, ours skipped the refusal there). True when it refuses, with "You get no
    /// response." (block 7, entry 1 in game):
    ///   - FIGHTING OR FLEEING THE PLAYER, OR HOSTILE, it does not answer (UWClickRules.
    ///     CreatureAnswers; per user, 2026-10-01: Oradinar, sent off by his own conversation,
    ///     cannot be talked to in the original);
    ///   - NO CONVERSATION: none for the slot, its code empty, or whoami 255 - the Slasher of
    ///     Veils and the animals (per user in the original, 2026-09-14).
    /// </summary>
    private bool fRefusesTalk(UWNpc pONpc, int piSlot)
    {
        if (pONpc == null || !UWObjectIds.IsCreature(pONpc.ID))
            return false;

        if (piSlot > 0 && pONpc.NPCwhoami != NoResponseWhoami && !fHasEmptyConversation(piSlot)
            && UWClickRules.CreatureAnswers(pONpc.NPCwhoami, pONpc.NPCGoal, pONpc.NPCGTarg, pONpc.NPCAttitude, pONpc.NPCIsAlly))
            return false;

        fAddNoResponse();

        return true;
    }

    /// <summary>whoami 255: this creature never talks (reference).</summary>
    private const int NoResponseWhoami = 255;

    /// <summary>"You get no response." - block 7, entry 1 in game, for us as usual one
    /// higher.</summary>
    private const int NoResponseStringIndex = 1 + 1;

    private bool fHasEmptyConversation(int piSlot)
    {
        UWConversations.Conversation lOConversation = mOUWDataImporter.Conversations.GetConversation(piSlot);

        return lOConversation == null || lOConversation.Code == null || lOConversation.Code.Length == 0;
    }

    private void fAddNoResponse()
    {
        fAddConversationBlockMessage(NoResponseStringIndex);
    }

    /// <summary>A line of the conversation block (7) into the message scroll - "You get no
    /// response.", "You cannot talk to that!".</summary>
    private void fAddConversationBlockMessage(int piIndex)
    {
        try
        {
            string lsMessage = mOUWDataImporter.Strings.Blocks[UWConversations.PartnerNameStringBlock].Strings[piIndex];

            if (!string.IsNullOrWhiteSpace(lsMessage))
                mOActiveStrings.Add(lsMessage.TrimEnd('\r', '\n'));
        }
        catch
        {
            // a missing entry is not an error
        }
    }

    /// <summary>First slot of the generic conversations - one per creature kind.</summary>
    private const int GenericConversationBase = 256;

    /// <summary>Which conversation belongs to this creature, or 0 - see fTryTalk.</summary>
    public int GetConversationSlot(UWNpc pONpc)
    {
        if (pONpc == null || mOUWDataImporter == null || mOUWDataImporter.Conversations == null)
            return 0;

        int liSlot = pONpc.NPCwhoami != 0
            ? pONpc.NPCwhoami
            : GenericConversationBase + (pONpc.ID - UWObjectClassProperties.CritterFirstId);

        if (pONpc.NPCwhoami == 0 && (pONpc.ID < UWObjectClassProperties.CritterFirstId || pONpc.ID > 127))
            return 0;

        return liSlot < mOUWDataImporter.Conversations.SlotCount
            && mOUWDataImporter.Conversations.GetConversation(liSlot) != null ? liSlot : 0;
    }
    /// <summary>Inscription and picture of a gravestone (reference gravestone.DisplayGrave):
    /// string block 8, picture from CS401.N01 via GRAVE.DAT. Returns false if it is not a
    /// gravestone with a grave number.</summary>
    private bool fTryShowGravestone(UWEntityInfo pOTarget)
    {
        int liGrave = pOTarget != null ? UWEndgame.GetGraveNumber(pOTarget.ObjectData) : -1;

        if (liGrave < 0 || mOUWDataImporter == null)
            return false;

        string lsText = UWEndgame.GetGraveText(mOUWDataImporter, liGrave);

        if (!string.IsNullOrEmpty(lsText))
            mOActiveStrings.Add(lsText);

        if (mGameUi != null)
            mGameUi.ShowGravePicture(UWEndgame.GetGraveFrame(mOUWDataImporter, liGrave));

        return true;
    }

    /// <summary>
    /// THE FOUNTAIN, Fountain_seg040_352B_1816 (read 2026-10-08; per user on the original: not
    /// every fountain heals - "The water refreshes you." and nothing else - and the one on level 1
    /// not fully; until then every fountain healed fully): its enchantment (UWObjectMechanics.
    /// TryGetEnchantmentClass) is cast on the user as a spell trap casts, and only the healing
    /// class says "The waters of the fountain renew your strength."; the healing class's minor
    /// class decides how much (UWSpellCasting). The water above the basin uses the fountain
    /// linked to it (seg040_352B_2A0).
    /// </summary>
    private void fUseFountain(UWObject pOObject)
    {
        List<UWObject> lOMaster = mLevelLoader != null && mLevelLoader.CurrentLevel != null ? mLevelLoader.CurrentLevel.Masterlist : null;
        UWObject lOFountain = pOObject != null && pOObject.ID == UWObjectMechanics.FountainWaterObjectId
            ? fLinkedFountain(pOObject, lOMaster)
            : pOObject;

        if (lOFountain != null && UWObjectMechanics.TryGetEnchantmentClass(lOFountain, lOMaster, out int liMajor, out int liMinor)
            && liMajor >= 0)
        {
            UWGameUI lOUi = UWScene.GameUi;

            if (lOUi != null)
                lOUi.CastSpellByClass(liMajor, liMinor);

            AddGeneralMessage(liMajor == UWObjectMechanics.FountainHealMajorClass
                ? UWObjectMechanics.FountainMessageIndex
                : UWObjectMechanics.FountainRefreshMessageIndex);

            return;
        }

        AddGeneralMessage(UWObjectMechanics.FountainRefreshMessageIndex);
    }

    /// <summary>The fountain the water above it links to - its contents link, else the next one.</summary>
    private static UWObject fLinkedFountain(UWObject pOWater, List<UWObject> pOMaster)
    {
        if (pOMaster == null)
            return null;

        int liContents = pOWater.HasQuantity ? 0 : pOWater.Quantity;

        foreach (int liAt in new[] { liContents, (int)pOWater.Link })
        {
            if (liAt > 0 && liAt < pOMaster.Count && pOMaster[liAt] != null && pOMaster[liAt].ID == UWObjectMechanics.FountainObjectId)
                return pOMaster[liAt];
        }

        return null;
    }

    /// <summary>Objects with their own effect when used that are neither door nor switch - which
    /// thing has which effect is UWWorldUseRules (cleanup step 2.4, 2026-10-12), here it is
    /// carried out. False when the thing has none (or the gravestone no grave number, or the
    /// silver tree was not taken) - then UseTarget goes on.</summary>
    private bool fTryUseObjectEffect(UWEntityInfo pOTarget)
    {
        if (pOTarget == null || pOTarget.ObjectData == null || mOUWDataImporter == null)
            return false;

        switch (UWWorldUseRules.Classify(pOTarget.ObjectData.ID, pOTarget.ObjectData.GetCategory()))
        {
            // Using a gravestone shows the same as looking (reference use.cs, case 5).
            case UWWorldUseRules.Effect.Gravestone:
                return fTryShowGravestone(pOTarget);

            case UWWorldUseRules.Effect.Fountain:
                fUseFountain(pOTarget.ObjectData);
                return true;

            // Reaching for the silver tree takes it back as a seed (see UWSilverTree).
            case UWWorldUseRules.Effect.SilverTree:
                return UWSilverTree.TryTakeTree(mLevelLoader, pOTarget.ObjectData, mInventory, this);

            case UWWorldUseRules.Effect.Cauldron:
                AddGeneralMessage(UWObjectMechanics.CauldronMessageIndex);
                return true;

            case UWWorldUseRules.Effect.Shrine:
                fBeginMantra();
                return true;

            // The anvil asks WHAT to work on - answered with a click in the backpack (see
            // UWItemTargetUse); the pole lying on the floor asks the same and is then applied in
            // the world, from where it lies.
            case UWWorldUseRules.Effect.UseModeFromWorld:
                fEnterUseModeFromWorld(pOTarget);
                return true;

            // Barrel, chest and the containers of major class 2 spill (the nightstand does
            // nothing, see UWWorldUseRules).
            case UWWorldUseRules.Effect.Spill:
                SpillWorldContainer(pOTarget);
                return true;

            case UWWorldUseRules.Effect.LightsOnlyEquipped:
                AddGeneralMessage(UWWorldUseRules.LightsOnlyEquippedMessage);
                return true;

            // The glowing rock joins the carried one, as walking over it does (UWTouchGather).
            case UWWorldUseRules.Effect.GlowingRock:
                if (mInventory != null && mLevelLoader != null
                    && UWGlowingRockRules.TryGather(pOTarget.ObjectData, mInventory.Model, mLevelLoader.CurrentLevel))
                {
                    mLevelLoader.RemoveObjectFromWorld(pOTarget.ObjectData);
                    mInventory.Model.NotifyChanged();
                }

                return true;

            case UWWorldUseRules.Effect.BurningIncense:
                fBurnIncenseInWorld(pOTarget.ObjectData);
                return true;

            default:
                return false;
        }
    }

    /// <summary>Burning incense on the floor: the vision plays and the block becomes debris where
    /// it lies (UWItemUse.BurnIncense) - the record keeps its place, its picture is put down anew,
    /// and its place in the tile's list too (UseUnique's SET_ITEM; UWWorldEdit.Arrival.InPlace,
    /// 2026-10-18).</summary>
    private void fBurnIncenseInWorld(UWObject pOIncense)
    {
        UWItemDrag lOHost = UWScene.ItemDrag;

        if (pOIncense == null || lOHost == null || mLevelLoader == null)
            return;

        int liTileX = pOIncense.TileX;
        int liTileY = pOIncense.TileY;

        int liListIndex = mLevelLoader.WorldEdit != null ? mLevelLoader.WorldEdit.ListIndexOf(pOIncense) : -1;

        UWItemUse.BurnIncense(pOIncense, mOUWDataImporter, null, lOHost);

        if (mLevelLoader.RemoveObjectFromWorld(pOIncense))
            mLevelLoader.PlaceRestingObject(pOIncense, liTileX, liTileY, UWWorldEdit.Arrival.InPlace, liListIndex);
    }

    /// <summary>A thing in the world enters use mode as if it had been clicked in the pack:
    /// its icon hangs on the pointer and the game asks "Use ... on what?" - the prompt and its
    /// wording are UWItemDrag's (fReportUsePrompt), the answer UWItemDrag.fTryApplyUseModeItem.</summary>
    private void fEnterUseModeFromWorld(UWEntityInfo pOTarget)
    {
        if (mInventory == null)
            return;

        UWItemDrag lOItemDrag = UWScene.ItemDrag;

        if (lOItemDrag != null)
            lOItemDrag.BeginUseModeFromWorld(pOTarget.ObjectData);
        else
            mInventory.EnterUseMode(pOTarget.ObjectData);
    }

    /// <summary>Barrel, chest or nightstand in the world (see SpillWorldContainer): the 3D-model
    /// containers of major class 5, minor class 1 (barrel 0x15B, chest 0x15D, nightstand 0x15E,
    /// which does nothing when used - UWWorldUseRules).</summary>
    public static bool IsWorldContainer(UWEntityInfo pOTarget)
    {
        int liId = pOTarget != null && pOTarget.ObjectData != null ? pOTarget.ObjectData.ID : -1;
        return UWObjectMechanics.IsWorldContainer(liId);
    }

    /// <summary>
    /// Using a barrel, chest or nightstand in the world puts its contents openly onto its tile
    /// instead of opening a container panel (per user on the original, 2026-09-15). The
    /// original's UseCont -> DumpTheBag -> drop_link_chain (USEITEMS.C, TREASURE.C): a locked
    /// container reports "The ... is locked"; otherwise the chain goes down around the
    /// container, 6 eighths each way, which can reach the next tile (read 2026-09-25, see
    /// UWScatterRules; until then ours kept to the tile) and NOT onto the containers standing
    /// there (per user, 2026-09-16; a THROWN item still may). The whole chain goes down, lock and
    /// triggers too, and a used container fires no use trigger (decision 25, per user on the
    /// original 2026-10-12; UWSpillRules, UWSpill). A smashed or burnt container comes through
    /// here with pbFromDamage and no message of its own: damage_object sets off the use triggers
    /// first, before the spill. A smashed chest or barrel (UWContainerDamage) then loses ONE lock
    /// (remove_object's remove_lock(obj, 0), the lock freed, not spilled) and is used like a
    /// player's use, so a second, locked lock still says "The ... is locked." and keeps the
    /// contents (2026-10-12; until then every lock was ignored and the lock went down with the
    /// rest). A bag destroyed by fire (UWTileBlast) ignores the lock (DumpTheBag).
    /// </summary>
    public void SpillWorldContainer(UWEntityInfo pOTarget, bool pbIgnoreLock = false,
        bool pbAnnounceEmpty = true, bool pbFromDamage = false)
    {
        if (mLevelLoader == null || mLevelLoader.CurrentLevel == null)
            return;

        UWObject lOContainer = pOTarget.ObjectData;
        lOContainer.EnsureContentsLoaded(mLevelLoader.CurrentLevel.Masterlist);

        // Smashed or burnt: the use triggers fire while the chain is whole (damage_object's
        // checkTrap kind 4 before remove_object), whether a lock then holds the contents or not.
        if (pbFromDamage)
        {
            FireDestroyedUseTriggers(lOContainer);

            if (UWObjectDamageRules.IsUsedWhenDestroyed(lOContainer.ID))
                UWSpillRules.TakeFirstLock(lOContainer.Contents);
        }

        if (!pbIgnoreLock && UWSpillRules.FindLockedLock(lOContainer.Contents) != null)
        {
            AddMessage(UWItemMessages.IsLocked(mOUWDataImporter, lOContainer.ID));
            return;
        }

        UWTilePos lOTile = mLevelLoader.WorldPositionToTile(pOTarget.transform.position);
        int liSpilled = UWSpill.Apply(mLevelLoader, lOContainer, UWSpillRules.Partition(lOContainer.Contents),
            pOTarget.transform.position, lOTile, true);

        // NOTHING CAME OUT: the original then says so. UW.EXE builds the sentence from three
        // pieces (DumpTheBag: "The ", the bare item name, " is empty.") and only when nothing
        // was spilled and the player did the using - which is why smashing a container stays
        // silent (per user, 2026-09-16: using an empty chest or barrel prints something in the
        // original, ours printed nothing).
        if (UWSpillRules.AnnouncesEmpty(liSpilled, pbAnnounceEmpty))
            AddMessage(UWItemMessages.IsEmpty(mOUWDataImporter, lOContainer.ID));
    }

    /// <summary>The use triggers in a destroyed object's chain, in order - damage_object's
    /// checkTrap kind 4 for every static object it destroys (DAMAGE.C), before remove_object
    /// spills or frees the chain.</summary>
    public void FireDestroyedUseTriggers(UWObject pOObject)
    {
        if (pOObject == null || mLevelLoader == null || mLevelLoader.CurrentLevel == null)
            return;

        pOObject.EnsureContentsLoaded(mLevelLoader.CurrentLevel.Masterlist);

        foreach (UWObject lOTrigger in UWSpillRules.UseTriggers(pOObject.Contents))
            UWTriggerSystem.FireTraps(lOTrigger, pOObject, mLevelLoader, this);
    }

    /// <summary>A general message as text, without adding it to the box right
    /// away - for messages assembled from several pieces (see
    /// UWMiscSpell.fDetectMonsters). Empty if the number does not exist.</summary>
    public string GetGeneralMessage(int piIndex)
    {
        return UWText.General(mOUWDataImporter, piIndex);
    }

    /// <summary>A missing entry is not an error (UWText gives nothing for it), and nothing
    /// is added for it.</summary>
    public void AddGeneralMessage(int piIndex)
    {
        string lsMessage = UWText.General(mOUWDataImporter, piIndex);

        if (!string.IsNullOrWhiteSpace(lsMessage))
            mOActiveStrings.Add(lsMessage);
    }

    /// <summary>The strike animation has reached its hit frame - now it is checked whether
    /// something is in range.</summary>
    private void fOnAttackConnected()
    {
        if (!mbAttackPending)
            return;

        mbAttackPending = false;

        fResolveAttack();
    }

    /// <summary>
    /// Evaluates a strike: what is in range, what is used to strike, how much
    /// damage it does.
    ///
    /// The weapon comes from the main hand (UWInventory.MainHandSlot); if it is empty, the
    /// fist is used - the melee table lists it as a full weapon, so no
    /// special case is needed. The damage formula is in UWCombat, including a note on what
    /// of it comes from the data and what is invented.
    /// </summary>
    private void fResolveAttack()
    {
        if (mOUWDataImporter == null || mePendingAttack == AttackEnum.Idle)
            return;

        // WITH BOW, CROSSBOW OR SLING you shoot instead of strike - and that
        // works completely differently: no ray at a target, but a projectile that flies.
        if (fTryShoot())
            return;

        bool lbLog = UnderworldRevisited.UWSettings.Instance != null && UnderworldRevisited.UWSettings.Instance.LogAttacks;

        Collider lOHitCollider;
        Vector3 lOHitPoint;
        float lfHitDistance;

        mbBlowStruckNothing = false;

        // CLASSIC SCHEME: as in the original a hit area in front of the character, not the
        // ray to the mouse cursor - see fFindClassicMeleeTarget.
        bool lbFound = !UWControlScheme.IsModern
            ? fFindClassicMeleeTarget(lbLog, out lOHitCollider, out lOHitPoint, out lfHitDistance)
            : fFindModernMeleeTarget(lbLog, out lOHitCollider, out lOHitPoint, out lfHitDistance);

        // A SWING INTO THE AIR still makes the miss sound (effect 0x0A, the same as throwing):
        // the reference plays it at the attacker when the hit check finds no target ("swing
        // and a miss", combat.cs ExecuteAttack/CombatMissImpactSound). Noticed missing by the
        // user, 2026-09-15.
        if (!lbFound)
        {
            if (!mbBlowStruckNothing)
                fTryWallImpact();

            UWSoundEffects.PlayAtAvatar(UWSoundEffects.Miss);
            return;
        }

        UWEntityInfo lOTarget = lOHitCollider.GetComponentInParent<UWEntityInfo>();

        // A door's frame or blocker is the door (UWEntityInfo.PartOf) - for the modern scheme's
        // ray too; the classic search resolves it already.
        if (lOTarget != null && lOTarget.PartOf != null)
        {
            lOTarget = lOTarget.PartOf;

            Collider lOWhole = lOTarget.GetComponentInChildren<Collider>();

            if (lOWhole != null)
                lOHitPoint = lOWhole.bounds.ClosestPoint(lOHitPoint);
        }

        UWDamageable lODamageable = lOTarget == null ? null : lOTarget.GetComponentInParent<UWDamageable>();

        if (lODamageable == null || !lODamageable.TakesBlows)
        {
            if (lOTarget == null)
                fTryWallImpact();

            UWSoundEffects.PlayAtAvatar(UWSoundEffects.Miss);
            return;
        }

        if (lbLog)
            Debug.Log(string.Format("[Attack] hit: {0} at {1:0.0}, object={2}, damageable={3}.",
                lOHitCollider.name, lfHitDistance,
                lOTarget == null || lOTarget.ObjectData == null ? -1 : lOTarget.ObjectData.ID,
                lODamageable != null));

        fApplyMeleeHit(lOTarget, lODamageable, lOHitPoint);
    }

    /// <summary>Modern scheme: the ray from the camera through the screen centre, as before.
    /// </summary>
    private bool fFindModernMeleeTarget(bool pbLog, out Collider pOCollider, out Vector3 pOPoint, out float pfDistance)
    {
        pOCollider = null;
        pOPoint = Vector3.zero;
        pfDistance = 0f;

        RaycastHit lOHit;

        bool lbHit = Physics.Raycast(fGetInteractionRay(), out lOHit, mfAttackRange);

        // If the enemy stands very close, the camera point lies INSIDE its
        // collider - for a creature that is square in plan and therefore
        // 68 units deep, so it reaches 34 units forward. A ray that starts inside a
        // body reports no hit, and the strike went nowhere (per user,
        // 2026-08-30). So on a miss, check once more with a sphere.
        if (!lbHit)
            lbHit = Physics.SphereCast(fGetInteractionRay(), CloseCombatRadius, out lOHit, mfAttackRange);

        if (!lbHit)
        {
            if (pbLog)
            {
                // Check again without a range limit: so the log shows what lies THERE and how
                // far away it is. That distinguishes "nothing hit" from "just too far".
                if (Physics.Raycast(fGetInteractionRay(), out RaycastHit lOFar, LookRayRange))
                    Debug.Log(string.Format("[Attack] nothing within range {0}. Nearest target: {1} at {2:0.0}.",
                        mfAttackRange, lOFar.collider.name, lOFar.distance));
                else
                    Debug.Log(string.Format("[Attack] nothing hit, not even in the distance."));
            }

            return false;
        }

        pOCollider = lOHit.collider;
        pOPoint = lOHit.point;
        pfDistance = lOHit.distance;

        return true;
    }

    /// <summary>Object 127 is the avatar; its height from COMOBJ.DAT determines the
    /// strike height.</summary>
    private const int AvatarObjectId = 127;

    /// <summary>Height of the avatar in original units, in case COMOBJ.DAT is missing.</summary>
    private const int FallbackAvatarHeight = 32;

    /// <summary>
    /// THE BLOW'S PROBE as resolve_attack builds it: a box (radius + 1) eighths of a tile to each
    /// side, its height from the weapon's radius, at the height of the swing (the low, middle or
    /// high third of the avatar by the kind of blow, raised or lowered by the view's pitch),
    /// centred (radius + 3) eighths ahead along the facing. The radius is the weapon's from
    /// COMOBJ.DAT, the fist's without one. Shared by the target search and the wall's impact.
    /// </summary>
    /// <summary>
    /// Where the blow's picture sits in height (world y), written by fGetMeleeProbe for the
    /// strike it serves: the original's hitz (COMBAT.C, AttackerAppliesFinalDamage) is the
    /// probe's z - the attacker's feet plus the swing's share of his height plus his pitch -
    /// plus a sixth of his height, so a stab flashes low, a bash high. Until 2026-10-12 the
    /// flash on an object took the height of the probe's closest point, the probe's middle,
    /// and sat too high (per user on the original: "a little lower").
    /// </summary>
    private float mfMeleeHitY;

    /// <summary>The probe's bottom above the player's feet and its height, in zpos - written by
    /// fGetMeleeProbe; the body part is picked against them (resolve_attack hands pickloc the
    /// probe, not the attacker's body - C3, 2026-10-12).</summary>
    private int miMeleeProbeOffsetZ;

    private int miMeleeProbeHeightZ;

    /// <summary>The attacker's eighth cell and the probe's centre after move_along, in eighths -
    /// written by fGetMeleeProbe for the classic target search.</summary>
    private UWTilePos mOMeleeFrom;

    private UWTilePos mOMeleeAt;

    /// <summary>The classic search met objects in its probe but struck none of them: resolve_attack
    /// then returns without asking the terrain, so no wall impact either.</summary>
    private bool mbBlowStruckNothing;

    private void fGetMeleeProbe(out Transform pOBody, out Vector3 pOForward, out Vector3 pOCentre,
        out Vector3 pOHalfExtents, out Quaternion pORotation, out int piRadius)
    {
        CharacterController lOController = GetComponentInParent<CharacterController>();
        Transform lOBody = lOController != null ? lOController.transform : transform;

        pOBody = lOBody;

        // Weapon radius from COMOBJ.DAT; without a melee weapon the fist's - a torch, a wand or
        // anything else in the hand reaches as far as the fist (GetPlayerWeapon, UnderworldExhumed
        // combat/COMBAT.C, sets wsize from ITEM_FIST for anything outside class 0; until
        // 2026-10-12 ours took the held object's own radius).
        UWObject lOWeapon = mInventory == null ? null : mInventory.GetEquipped(mInventory.MainHandSlot);
        int liWeaponId = lOWeapon == null || lOWeapon.ID > UWPlayerCritterRow.MeleeLastId ? UWCombat.FistObjectId : lOWeapon.ID;
        int liRadius = 0;
        int liAvatarHeight = FallbackAvatarHeight;
        UWCommonObjectProperties.Entry lOEntry;

        if (mOUWDataImporter.CommonObjectProperties != null)
        {
            if (mOUWDataImporter.CommonObjectProperties.TryGet(liWeaponId, out lOEntry))
                liRadius = lOEntry.Radius;

            if (mOUWDataImporter.CommonObjectProperties.TryGet(AvatarObjectId, out lOEntry) && lOEntry.Height > 0)
                liAvatarHeight = lOEntry.Height;
        }

        // towhere is the clicked ninth, 1 to 9 (miPendingNinth; the middle column 2, 5, 8 by kind
        // until 2026-10-12, see fChooseAttackKind): towhere / 3 gives 0 for 1 and 2, 1 for 3 to 5,
        // 2 for 6 to 8 and 3 for 9 - a bash in the top right corner aims above the head.
        int liSwingHeight = miPendingNinth;

        float lfFeetY = lOController != null
            ? lOBody.position.y + lOController.center.y - (lOController.height * 0.5f)
            : lOBody.position.y;

        // View pitch in degrees, positive downwards (Unity) - in the original a positive
        // value raises the area, hence the inverted sign.
        float lfPitchDegrees = 0f;

        if (Camera.main != null)
        {
            lfPitchDegrees = Camera.main.transform.localEulerAngles.x;

            if (lfPitchDegrees > 180f)
                lfPitchDegrees -= 360f;
        }

        float lfPitchUnits = -lfPitchDegrees * (UWWorldScale.FullCircleUnits / 360f);

        float lfBottomUnits = (liAvatarHeight * (liSwingHeight / 3) / 3) + (lfPitchUnits / 0x200);
        float lfHeightUnits = ((liRadius * 2) + 1) * 4;

        miMeleeProbeOffsetZ = (liAvatarHeight * (liSwingHeight / 3) / 3) + (int)(lfPitchUnits / 0x200);
        miMeleeProbeHeightZ = (int)lfHeightUnits;

        Vector3 lOForward = new Vector3(lOBody.forward.x, 0f, lOBody.forward.z);

        if (lOForward.sqrMagnitude < 0.0001f)
            lOForward = Vector3.forward;
        else
            lOForward.Normalize();

        float lfBottom = lfFeetY + (lfBottomUnits * UWWorldScale.ZPosStep);
        float lfHeight = lfHeightUnits * UWWorldScale.ZPosStep;

        // The probe's radius is wsize + 1 eighths, and the motion core's boxes run from x - radius
        // to x + radius INCLUSIVE in eighth cells (UWMotionCore.fCreateRecord), so the square
        // reaches half a cell past the radius around the centre cell (2026-10-14; until then ours
        // stopped at the radius).
        float lfHalfWidth = (liRadius + 1.5f) * UWWorldScale.SubTileStep;

        // The height of the blow's picture (COMBAT.C hitz): the probe's bottom plus a sixth
        // of the attacker's height - see mfMeleeHitY.
        mfMeleeHitY = lfBottom + ((liAvatarHeight / 6) * UWWorldScale.ZPosStep);

        // THE CENTRE AS THE ORIGINAL PUTS IT (resolve_attack, UnderworldExhumed COMBAT.C): the
        // attacker's eighth cell moved wsize + 3 along the 256-step heading by move_along, which
        // truncates each axis and adds one away from zero (UWAreaSpellRules.MoveAlong) - one eighth
        // farther ahead than a plain distance (per user 2026-10-14: our reach felt shorter).
        Vector3 lOCentre = new Vector3(lOBody.position.x, lfBottom + (lfHeight * 0.5f), lOBody.position.z)
            + (lOForward * ((liRadius + 3) * UWWorldScale.SubTileStep));

        if (mLevelLoader != null)
        {
            UWTilePos lOTile = mLevelLoader.WorldPositionToTile(lOBody.position);
            UWTilePos lOFrom = new UWTilePos((lOTile.X << 3) + UWTileQueries.WorldToEighthFloor(lOBody.position.x),
                (lOTile.Y << 3) + UWTileQueries.WorldToEighthFloor(lOBody.position.z));
            UWTilePos lOAt = UWAreaSpellRules.MoveAlong(lOFrom, UWPlayerThrow.GetPlayerHeading(lOBody), liRadius + 3);

            mOMeleeFrom = lOFrom;
            mOMeleeAt = lOAt;
            lOCentre = new Vector3(UWUnits.EighthsToWorldAxis(lOAt.X), lOCentre.y, UWUnits.EighthsToWorldAxis(lOAt.Y));
        }

        Vector3 lOHalfExtents = new Vector3(lfHalfWidth, lfHeight * 0.5f, lfHalfWidth);

        // THE PROBE IS SQUARE TO THE MAP, not turned with the view: ObjectCheck tests the motion
        // core's boxes, the mover's from x - radius to x + radius in eighths and every record's
        // the same way (UWMotionCore.fCreateRecord) - all aligned to the tile grid. Facing a
        // diagonal, the square's corner reaches farther than a turned box; per user on the
        // original 2026-10-14 (audit B5) a goblin diagonally round a corner is hit from close
        // by, which ours, with the turned box, never reached (it struck the wall).
        Quaternion lORotation = Quaternion.identity;

        pOForward = lOForward;
        pOCentre = lOCentre;
        pOHalfExtents = lOHalfExtents;
        pORotation = lORotation;
        piRadius = liRadius;
    }

    /// <summary>
    /// THE BLOW THAT MEETS A WALL (find_wall_coll, UnderworldExhumed COMBAT.C, read 2026-10-11;
    /// the Exhumed audit's row 14): when the swing found no object, resolve_attack asks whether
    /// its probe touches a wall; if so, find_wall_coll walks from the attacker along the facing in
    /// steps of an eighth of a tile, (radius + 3) + 1 of them, at the probe's height, and at the
    /// first step inside the wall puts the impact picture 0x1CB and, for the player's blow,
    /// plays effect 7 - before the miss. Where the walk does not reach the wall (a wall touched
    /// only at the probe's side) there is neither. Ours does the same with the same probe
    /// (fGetMeleeProbe) and the walk as one level ray at its height (fTryFindWallInReach). Until
    /// 2026-10-11 ours played the miss alone; the first version that day looked along at eye
    /// height and missed the low walls (per user: "not at every wall").
    /// </summary>
    private void fTryWallImpact()
    {
        if (!fTryFindWallInReach(out Vector3 lOAt))
            return;

        UWSoundEffects.PlayAt(UWSoundEffects.SpellImpact, lOAt);

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        StartCoroutine(fShowHitEffectDelayed(UWObjectMechanics.FlashEffectObjectId, lOAt,
            lOSettings != null ? lOSettings.HitEffectDelaySeconds : 0.2f,
            lOSettings != null ? lOSettings.HitEffectSeconds : 0.6f));
    }

    /// <summary>
    /// Where the walk of find_wall_coll first enters the wall, or false: a level ray (radius
    /// + 4) eighths long from the attacker along the facing at the probe's height - the walk's
    /// steps of an eighth as one ray. The terrain the original tests is the walls (its hit bits
    /// 0x300), so only a collider without an entity counts whose face stands upright, not the
    /// floor or the ceiling a low or high blow brushes.
    /// </summary>
    private bool fTryFindWallInReach(out Vector3 pOAt)
    {
        pOAt = Vector3.zero;

        fGetMeleeProbe(out Transform lOBody, out Vector3 lOForward, out Vector3 lOCentre, out Vector3 lOUnusedExtents,
            out Quaternion lOUnusedRotation, out int liRadius);

        Vector3 lOOrigin = new Vector3(lOBody.position.x, lOCentre.y, lOBody.position.z);
        RaycastHit[] lOHits = Physics.RaycastAll(lOOrigin, lOForward, (liRadius + 4) * UWWorldScale.SubTileStep, ~0,
            QueryTriggerInteraction.Ignore);

        // RaycastAll comes in no order - the nearest first.
        System.Array.Sort(lOHits, (pOLeft, pORight) => pOLeft.distance.CompareTo(pORight.distance));

        foreach (RaycastHit lOHit in lOHits)
        {
            if (lOHit.collider == null || lOHit.collider.transform.IsChildOf(lOBody))
                continue;

            if (lOHit.collider.GetComponentInParent<UWEntityInfo>() != null || Mathf.Abs(lOHit.normal.y) > 0.5f)
                return false;

            pOAt = lOHit.point - (lOForward * UWWorldScale.SubTileStep * 0.5f);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Melee in the classic scheme AS IN THE ORIGINAL (per user, 2026-09-13: "rebuild
    /// melee in the classic scheme as in the original"; reason: lurkers and flying
    /// enemies can be fought in the original by looking down or up).
    ///
    /// REFERENCE combat.checkAttackHit: no ray to the mouse cursor, but an area in front of
    /// the character in its view direction.
    ///   Centre: weapon radius plus three eighths of a tile ahead.
    ///   Width:  weapon radius plus one, in eighths.
    ///   Bottom: foot height plus avatar height times (height value divided by 3) divided by 3, both
    ///           integer. Height value per attack type 2, 5 or 8, plus the view pitch
    ///           divided by 0x200.
    ///   Height: (weapon radius times 2 plus 1) times 4.
    /// The nearest thing in the area is hit.
    ///
    /// UNITS (UWViewpoint): horizontally one eighth of a tile = 8 world units, vertically one
    /// unit = 2 world units (UWWorldScale.ZPosStep), angle 65536 per full circle.
    /// A view step of 15 degrees thus raises or lowers the area by a good 5 units.
    ///
    /// ATTACK TYPE: the stab is lowest, the side slash in the middle, the
    /// overhead strike highest - matching the mouse thirds and animation (per user,
    /// 2026-09-13: "stab is the lowest"). Height values thus stab 2, slash 5, overhead 8.
    /// The reference contradicts itself here (comments on the height value versus its
    /// input mapping); a first version followed the input mapping and put the stab
    /// highest.
    ///
    /// NO WALL TEST: resolve_attack (UnderworldExhumed COMBAT.C) asks only what its probe touches
    /// (ObjectCheck first, the terrain only when no object is there) - no line from the attacker
    /// to the target. Per user on the original 2026-10-14 (audit B5): close to a corner, a goblin
    /// diagonally behind the wall's edge is hit; ours cast a ray to the hit point and struck the
    /// wall instead, until that day.
    /// </summary>
    private bool fFindClassicMeleeTarget(bool pbLog, out Collider pOCollider, out Vector3 pOPoint, out float pfDistance)
    {
        pOCollider = null;
        pOPoint = Vector3.zero;
        pfDistance = 0f;

        fGetMeleeProbe(out Transform lOBody, out Vector3 lOForward, out Vector3 lOCentre, out Vector3 lOHalfExtents,
            out Quaternion lORotation, out int liRadius);

        UWMotionHost lOWorld = UWMotionHost.Ensure(mLevelLoader);

        if (lOWorld == null || mCharacter == null)
            return false;

        // THE TARGET FROM THE LEVEL DATA'S BOXES (UWPlayerAttack.PickBlowTarget): the probe of
        // radius weapon + 1, its bottom the swing's height above the feet, the nearest object in
        // its height struck. Until 2026-10-14 ours asked the drawn colliders, which are narrower
        // than the original's boxes - a door's box is radius 3 around its position - and struck
        // the wall where the original strikes the door's frame (audit B5).
        int liProbeBottom = mCharacter.GetFeetZPos() + miMeleeProbeOffsetZ;
        int liFound = lOWorld.FindBlowTarget(mOMeleeAt.X, mOMeleeAt.Y, liProbeBottom, liRadius + 1, miMeleeProbeHeightZ,
            mOMeleeFrom.X, mOMeleeFrom.Y, out UWObject lOObject, out UWCritter lOCritter);

        mbBlowStruckNothing = liFound != UWPlayerAttack.BlowFoundNothing;

        UWEntityInfo lOEntity = null;

        if (lOCritter != null)
            lOEntity = lOCritter.GetComponentInParent<UWEntityInfo>();
        else if (lOObject != null && mLevelLoader != null)
            mLevelLoader.TryGetEntity(lOObject, out lOEntity);

        Collider lOStruck = lOCritter != null ? lOCritter.GetComponentInChildren<Collider>()
            : lOEntity != null ? lOEntity.GetComponentInChildren<Collider>() : null;

        if (pbLog)
            Debug.Log(string.Format("[Attack] probe at eighths {0}/{1}, z {2}, radius {3}, height {4}: {5}.",
                mOMeleeAt.X, mOMeleeAt.Y, liProbeBottom, liRadius + 1, miMeleeProbeHeightZ,
                liFound == UWPlayerAttack.BlowFoundNothing ? "no object"
                    : liFound == UWPlayerAttack.BlowStruckNothing ? "objects, none in the blow's height"
                    : lOStruck != null ? "target " + lOStruck.name : "a target without a body"));

        if (lOStruck == null)
            return false;

        // The flash on the body's bounds, nearest the probe's centre (the door leaf is a concave
        // mesh collider, for which ClosestPoint hands the point back unchanged).
        Vector3 lOPoint = lOStruck.bounds.ClosestPoint(lOCentre);
        Vector3 lOFlat = lOPoint - lOBody.position;

        lOFlat.y = 0f;

        pOCollider = lOStruck;
        pOPoint = lOPoint;
        pfDistance = lOFlat.magnitude;

        return true;
    }

    /// <summary>Check, damage and feedback of a melee hit - the same for both
    /// schemes.</summary>
    private void fApplyMeleeHit(UWEntityInfo lOTarget, UWDamageable lODamageable, Vector3 pOHitPoint)
    {
        bool lbLog = UnderworldRevisited.UWSettings.Instance != null && UnderworldRevisited.UWSettings.Instance.LogAttacks;

        if (lODamageable == null || !lODamageable.TakesBlows)
            return;

        UWObject lOWeapon = mInventory == null ? null : mInventory.GetEquipped(mInventory.MainHandSlot);
        int liWeaponId = lOWeapon == null ? UWCombat.FistObjectId : lOWeapon.ID;

        // A CREATURE'S ARMOUR IS THAT OF THE PART HIT: PickBodyHitPoint over the two bodies,
        // then row[part % 4] with the 0xFF fallback and a strong defender's 5/3
        // (AttackerAppliesFinalDamage labels 990-A02, deviation 49) - the creature as the
        // defender of UWAttackResolution since cleanup step 4.9 (2026-10-18). A thing - a door, a
        // chest - takes no armour (do_damage subtracts it for creatures only; the UWDamageable's
        // one value of a thing is 0 from the spawner anyway).
        UWCritter lOCritterTarget = lOTarget != null ? lOTarget.GetComponentInParent<UWCritter>() : null;
        IUWAttackDefender lIDefender = lOCritterTarget != null ? lOCritterTarget : UWAttackResolution.ObjectDefender;
        int liFlank = 0;

        // The blow's probe (fGetMeleeProbe; the modern scheme's ray finds its target without it,
        // so it is measured here for both): its height decides the part and the picture's place.
        fGetMeleeProbe(out Transform lOUnusedBody, out Vector3 lOUnusedForward, out Vector3 lOUnusedCentre,
            out Vector3 lOUnusedExtents, out Quaternion lOUnusedRotation, out int liUnusedRadius);

        int liPart = UWAttackResolution.NoPart;

        if (lOCritterTarget != null && mCharacter != null)
        {
            // THE PART IS PICKED AGAINST THE BLOW'S PROBE, by the user's decision of 2026-10-12
            // to follow the original (combat C3): resolve_attack hands pickloc the probe -
            // the ninth's third of the body, the pitch, the weapon's reach in height - not the
            // attacker's body. A stab mostly meets the legs, an overhead bash often the head.
            // Until then ours took the player's whole body, so the kind of blow made no difference.
            int liProbeBottom = mCharacter.GetFeetZPos() + miMeleeProbeOffsetZ;

            liPart = lOCritterTarget.PickPartHitBy(liProbeBottom, liProbeBottom + miMeleeProbeHeightZ);

            // CalcFlankingBonus_seg022_230E_D48 from both headings in eighths, as a creature's
            // blow has it (UWCritterBrain): 0 face to face, 4 from behind.
            liFlank = UWCritterRules.GetFlankingBonus(
                UWPlayerThrow.GetPlayerHeading(mCharacter.transform) >> 5, lOCritterTarget.FacingEighth);
        }
        else if (mCharacter != null && lOTarget != null && lOTarget.ObjectData != null)
        {
            // compute_hitangle takes any target's heading - a door or a bench struck from its
            // back takes more (COMBAT.C, 2026-10-14).
            liFlank = UWCritterRules.GetFlankingBonus(
                UWPlayerThrow.GetPlayerHeading(mCharacter.transform) >> 5, lOTarget.ObjectData.Heading);
        }

        // The wielded weapon's Accuracy or Damage enchantment (DoPlayerWeapon).
        UWPlayerAttack.GetWeaponEnchantment(lOWeapon,
            mOUWDataImporter != null && mOUWDataImporter.InitialPlayer != null ? mOUWDataImporter.InitialPlayer.InventoryRecords : null,
            out int liEnchantAccuracy, out int liEnchantDamage);

        // THE WEAPON WEARS (UWEquipmentWear, per user 2026-09-28: the dagger lost quality twice
        // in the original, never in ours): a blow at a door on its roll, a critical failure
        // against a creature that does not spare it - in frp_check, between the check and the
        // dice, since cleanup step 4.9b (2026-10-18, by the user's decision to follow the
        // original; UWAttackResolution.PlayerBlow calls fWearWeapon). Worn here after the dice
        // until then.
        int liTargetId = lOTarget != null && lOTarget.ObjectData != null ? lOTarget.ObjectData.ID : -1;

        // The numbers - hit check and damage - are in UWPlayerAttack (P3 of the engine
        // separation, 2026-09-18). The linear charge fraction (0 to 1) kept on release, see
        // mfPendingCharge.
        UWPlayerAttack.MeleeResult lOResult = UWPlayerAttack.ResolveMelee(mOUWDataImporter, liWeaponId,
            fGetAttackKind(mePendingAttack), mfPendingCharge,
            mCharacter != null ? mCharacter.Vitals.Strength : 0,
            mCharacter != null ? mCharacter.Vitals.Attack : 0,
            mCharacter != null ? mCharacter.Vitals.Dexterity : 0,
            mCharacter != null ? mCharacter.Vitals.GetSkill(UWPlayerData.Skill.Unarmed) : 0,
            mCharacter != null ? (System.Func<UWPlayerData.Skill, int>)mCharacter.Vitals.GetSkill : null,
            lIDefender, liPart,
            mCharacter != null && mCharacter.Vitals.IsEasyDifficulty, liFlank, liEnchantAccuracy, liEnchantDamage,
            liTargetId, lOCritterTarget != null && lOCritterTarget.WearsWeaponOnCriticalFailure, fWearWeapon);

        if (lOResult.NoWeapon)
            return;

        if (lbLog)
            Debug.Log(string.Format("[Attack] hit check {0} + flank {3} against {1}: {2}, damage {4} ({5} before armour {6}).",
                lOResult.Accuracy, lIDefender.Defence, lOResult.Check, lOResult.Flank,
                lOResult.Damage, lOResult.DamageBeforeArmour,
                lOCritterTarget != null ? lIDefender.ArmourAt(liPart) : 0));

        if (!lOResult.Hit)
        {
            // Blocked. No damage, no hit effect, no eyes display - only the sound, at the target:
            // 7 or 8 by the player's own row's weapon and armour kinds (do_miss reads both from
            // the attacker's row; UWCritterCombat.GetBlockedSound). The creature still notices
            // the strike: the reference calls DamageObject with zero damage, and it
            // turns hostile and attacks (per user on the original, 2026-09-12). The row's kinds
            // are read by UWPlayerAttack.ResolveMelee, the sound chosen by UWAttackResolution
            // (cleanup step 4.9, 2026-10-18).
            UWSoundEffects.PlayAt(lOResult.Outcome.BlockedSound, pOHitPoint);

            // The miss lands through the world as the player's blow with 0 (cleanup step 4.8,
            // 2026-10-18; UWCritter.OnMissedByPlayer told the mind past the body until then).
            UWCritter lOMissedCritter = lOTarget != null ? lOTarget.GetComponentInParent<UWCritter>() : null;
            UWWorld lOMissWorld = mLevelLoader != null ? UWWorld.Of(mLevelLoader) : UWScene.World;

            if (lOMissedCritter != null && lOMissWorld != null)
                lOMissWorld.Damage(UWWorld.TargetOf(lOMissedCritter, lOMissedCritter.Damageable), lOResult.Outcome.Blow);

            return;
        }

        int liDamage = lOResult.Damage;

        // The picture at the original's height (mfMeleeHitY, worked out by the probe above); a
        // creature's blood or flash at the height of the part hit (liPart, C3).
        Vector3 lOFlashPoint = new Vector3(pOHitPoint.x, mfMeleeHitY, pOHitPoint.z);

        // A SPIKED DOOR'S SPIKE takes the blow inside ApplyDamage (UWDamageable.TakesBlowFirst,
        // after the quality class shift, as damage_object does). Until 2026-10-12 the player's
        // blow went to the spike here first, in full and without the hit sound; do_damage plays
        // the sound and the flash for a spiked door as for any other.

        // The player's own strike is bash damage, just like a creature's (reference:
        // combat.AttackerAppliesFinalDamage with four). Of the creatures in uw1 only
        // object 124 resists this type, the one with resistance byte 0x7F. Through the world
        // since cleanup step 3.3 (2026-10-17): the creature is not told, its damage event charges
        // the player (UWBlow.PlayerBlow).
        UWWorld lOWorld = mLevelLoader != null ? UWWorld.Of(mLevelLoader) : UWScene.World;

        if (lOWorld != null)
            lOWorld.Damage(lOCritterTarget != null ? UWWorld.TargetOf(lOCritterTarget, lODamageable) : UWWorld.TargetOf(lODamageable),
                lOResult.Outcome.Blow);

        // The hit sound at the target, louder by the damage BEFORE the armour: the original
        // plays it at label 938 of AttackerAppliesFinalDamage_seg022_8A5 and only subtracts
        // the armour at labels A04-A10, so a blow the armour swallows is still heard at full
        // volume. We had the volume from the damage after armour until 2026-09-22. The
        // creature's own blows (UWCritter) had it right already.
        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, pOHitPoint, lOResult.Outcome.HitVolume);

        // Armour that swallows the whole blow leaves no visible trace: UW.EXE
        // (AttackerAppliesFinalDamage, seg022_230E_A6B) skips the blood or flash animation and
        // the eyes when the damage after armour is 0. DamageObject is still called, so the
        // creature notices the blow. This is why the Slasher of Veils (toughness 20) almost
        // never bleeds (per user, 2026-09-14).
        if (liDamage != 0)
        {
            fShowHitEffect(lOTarget, lOFlashPoint, 0f, liPart);

            // A CRITICAL THAT DRAWS BLOOD SPLASHES TWICE (do_damage, UnderworldExhumed
            // combat/COMBAT.C, for the player as attacker): the second splat 2 below or 3 above
            // the first, in eighths of the creature's height (2026-10-12; ours had one).
            if (lOResult.Check == UWSkillCheck.ResultEnum.CriticalSuccess && fBleeds(lOTarget))
                fShowHitEffect(lOTarget, lOFlashPoint, fSecondSplatOffset(lOTarget), liPart);

            // The eyes at the top show the condition of the CREATURE just hit - not of a door, a
            // chest or a barrel (per user on the original, 2026-09-24).
            if (mEyes != null && fIsCreature(lOTarget))
                fShowEyes(lOTarget, lODamageable);
        }

        // NO LINE IN THE LOG: AttackerAppliesFinalDamage_seg022_8A5 prints nothing for a blow on
        // a door (per user on the original, 2026-09-27); ours wrote "The door is ..." after
        // every one until then. The condition stays in the look message.
    }

    // ------------------------------------------------- Player ranged combat

    /// <summary>First ammunition object - sling stone, bolt, arrow. The weapon names
    /// its ammunition type as a number counted from here.</summary>
    private const int FirstAmmunitionObjectId = UWObjectMechanics.FirstAmmunitionObjectId;

    /// <summary>Weapon type of physical projectiles. Whatever carries it is ammunition and
    /// not a weapon.</summary>
    private const int AmmunitionWeaponType = 0xC0;

    /// <summary>
    /// The message when out of ammunition.
    ///
    /// IT IS NOT IN STRINGS.PAK, BUT IN UW.EXE. The reference names
    /// string block 1, number 207 for it - for us that holds "You flip the switch.", and the whole
    /// block has no such sentence. I therefore searched the game binary itself
    /// and found the wording there (2026-09-10): "Sorry, you have no " is stored as raw
    /// text in the executable, the item name is appended at runtime.
    ///
    /// The user checked both in the original: "Sorry, you have no arrows." with the
    /// bow, "Sorry, you have no sling stones." with the sling. The full stop at the end
    /// comes from there.
    /// </summary>
    private const string NoAmmunitionMessage = "Sorry, you have no ";

    /// <summary>
    /// The shot with bow, crossbow or sling.
    ///
    /// WHICH AMMUNITION belongs to it is told by the weapon itself: its weapon type in the
    /// projectile table is the ammunition number, counted from the sling stone. The
    /// sling names 0, the crossbow 1, the bow 2 - so sling stone, bolt, arrow.
    /// Creatures use the same mapping for their thrown projectiles, and the loot of the
    /// sling goblins confirms it (see UWCritter).
    ///
    /// THE DAMAGE runs through the same chain as any other hit, with one addition
    /// that only exists here: the MISSILE skill stretches or squeezes it before
    /// the roll (reference: combat.MissileImpact). It enters as (skill times eight
    /// plus 192) divided by 256 - so at skill 0 three quarters remain, at 8 it becomes
    /// one and a half times. A check against ten subtracts another half from that or
    /// adds three quarters.
    ///
    /// Returns false if no shot happens at all - then the caller strikes as
    /// before.
    /// </summary>
    private bool fTryShoot()
    {
        if (mInventory == null || mOUWDataImporter == null
            || mOUWDataImporter.ObjectProperties == null)
            return false;

        UWObject lOWeapon = mInventory.GetEquipped(mInventory.MainHandSlot);

        if (lOWeapon == null)
            return false;

        int liAmmunitionId;

        if (!fTryGetAmmunitionId(lOWeapon.ID, out liAmmunitionId))
            return false;

        UWObject lOAmmunition = fFindAmmunition(liAmmunitionId);

        if (lOAmmunition == null)
        {
            // No shot, but no strike either: nobody strikes with a bow
            // in hand.
            AddMessage(NoAmmunitionMessage + fGetPluralName(liAmmunitionId) + ".");

            return true;
        }

        // MissileRelease_seg025_9F: the ammunition is taken only when the missile could be
        // placed; blocked, it stays in the pack.
        if (fLaunchPlayerMissile(liAmmunitionId))
            mInventory.TryConsumeOne(lOAmmunition);

        // The twang: bow and crossbow only, never the sling - and also when the shot failed
        // for lack of room (both paths of MissileRelease_seg025_9F end in it).
        if (lOWeapon.ID == BowObjectId || lOWeapon.ID == CrossbowObjectId)
            UWSoundEffects.PlayAtAvatar(UWSoundEffects.BowTwang);

        return true;
    }

    /// <summary>The two launchers that twang (MissileRelease_seg025_9F: weapon index 9 and 10
    /// in the ranged table, which starts at 0x10).</summary>
    private const int BowObjectId = 0x19;
    private const int CrossbowObjectId = 0x1A;

    /// <summary>"You need more space to fire that weapon." (block 1).</summary>
    private const int NoRoomForWeaponMessage = 255;

    /// <summary>Whether a bow, crossbow or sling is in the main hand.</summary>
    private bool fHoldsLauncher()
    {
        if (mInventory == null || mOUWDataImporter == null || mOUWDataImporter.ObjectProperties == null)
            return false;

        UWObject lOWeapon = mInventory.GetEquipped(mInventory.MainHandSlot);

        return lOWeapon != null && fTryGetAmmunitionId(lOWeapon.ID, out _);
    }

    /// <summary>
    /// Which ammunition belongs to this weapon. Returns false if it is not a
    /// ranged weapon at all.
    ///
    /// The projectile table covers objects 0x10 to 0x1F. Everything in it that is not
    /// ammunition itself (weapon type 0xC0) and names a known ammunition type is a
    /// ranged weapon.
    /// </summary>
    private bool fTryGetAmmunitionId(int piWeaponId, out int piAmmunitionId)
    {
        piAmmunitionId = -1;

        if (piWeaponId < FirstAmmunitionObjectId || piWeaponId > FirstAmmunitionObjectId + 0xF)
            return false;

        int liType = mOUWDataImporter.ObjectProperties.GetRangedType(piWeaponId);

        if (liType == AmmunitionWeaponType || liType > 0xF)
            return false;

        piAmmunitionId = FirstAmmunitionObjectId + liType;

        return true;
    }

    /// <summary>
    /// The name of the ammunition in plural, without article - "arrows", "sling stones".
    ///
    /// The original simply appends an s to the bare name instead of taking the plural form from
    /// the string block (reference: GetSimpleObjectNameUW plus "s"). For the three
    /// ammunition types the result is the same.
    /// </summary>
    private string fGetPluralName(int piObjectId)
    {
        return UWText.ObjectName(mOUWDataImporter, piObjectId) + "s";
    }

    /// <summary>Matching ammunition anywhere on the player, bags included, in the original's
    /// order (CheckForAmmo_seg022_E78) - see UWInventoryModel.FindCarried.</summary>
    private UWObject fFindAmmunition(int piAmmunitionId)
    {
        return mInventory.Model.FindCarried(lOItem => lOItem.ID == piAmmunitionId);
    }

    /// <summary>
    /// Fires one piece of ammunition. False when there is no room for it in front of the player -
    /// then "You need more space to fire that weapon." (MissileRelease_seg025_9F), which in the
    /// original comes all the time with a creature close in front (per user, 2026-09-24). The
    /// arrow is a mobile record on the motion core since 2026-10-05 (UWMissiles): it
    /// starts radii + 4 eighths ahead, falls, bounces and lies where it comes to rest.
    /// </summary>
    private bool fLaunchPlayerMissile(int piAmmunitionId)
    {
        if (mLevelLoader == null || Camera.main == null)
            return false;

        int liSpeedByte = mOUWDataImporter.ObjectProperties.GetRangedSpeed(piAmmunitionId);
        int liHeading;
        int liPitch;

        // The original's aim from the pointer in the view window; the modern scheme shoots where
        // the free pointer stands on the world, else along the crosshair (per user, 2026-10-04).
        if (!UWControlScheme.IsModern && mGameUi != null)
        {
            UWPlayerThrow.GetPointerInView(mGameUi, Mouse.current.position.ReadValue(), out int liX, out int liY);
            UWPlayerThrow.GetMissileAim(liX, liY, Camera.main.transform, out liHeading, out liPitch);
        }
        else
            UWMissiles.AimFromDirection(fGetModernAimRay().direction, liSpeedByte, out liHeading, out liPitch);

        // A missile hit always counts with charge 0x80, however far the bow was drawn
        // (reference: combat.MissileAttackHit, "PlayerAttackCharge = 0x80"). The arrow's life
        // is the ammunition's quality (MissileRelease copies it from the piece). The flight carries
        // the base; its dice are rolled at the hit (since cleanup step 4.9b, 2026-10-18).
        UWObject lOAmmunition = fFindAmmunition(piAmmunitionId);

        UWProjectileFlight lOFlight = UWMissiles.Ensure(mLevelLoader).LaunchFromPlayer(null, piAmmunitionId,
            liHeading, liPitch, liSpeedByte, fGetMissileBaseDamage(piAmmunitionId),
            UWDamageTypes.Missile, -1, this, lOAmmunition != null ? lOAmmunition.Quality : 0x3F);

        if (lOFlight == null)
        {
            AddGeneralMessage(NoRoomForWeaponMessage);

            return false;
        }

        return true;
    }

    /// <summary>The base damage of a fired projectile: the Missile skill stretches or squeezes it -
    /// see UWPlayerAttack.GetMissileBaseDamage (the dice at the hit since cleanup step 4.9b,
    /// 2026-10-18).</summary>
    private int fGetMissileBaseDamage(int piAmmunitionId)
    {
        return UWPlayerAttack.GetMissileBaseDamage(mOUWDataImporter, piAmmunitionId,
            mCharacter != null ? mCharacter.Vitals.GetSkill(UWPlayerData.Skill.Missile) : 0);
    }

    /// <summary>
    /// The base damage of a missile the player throws BY HAND - see UWPlayerThrow.
    ///
    /// Charge 0x80 at the hit, like every missile hit (missile_thwack), where the dice are rolled
    /// since cleanup step 4.9b (2026-10-18). The Missile skill scales only ammunition
    /// (UWPlayerAttack.GetThrownMissileBaseDamage, 2026-10-12).
    /// </summary>
    public int GetThrownMissileBaseDamage(int piObjectId)
    {
        if (mOUWDataImporter == null || mOUWDataImporter.ObjectProperties == null)
            return UWAttackResolution.NoMissileDamage;

        return UWPlayerAttack.GetThrownMissileBaseDamage(mOUWDataImporter, piObjectId,
            mCharacter != null ? mCharacter.Vitals.GetSkill(UWPlayerData.Skill.Missile) : 0);
    }

    /// <summary>The damage type of a class 1 object the player throws: -ammo of its missile
    /// table row (UWPlayerAttack.GetMissileDamageType) - 0x40 for ammunition.</summary>
    public int GetThrownMissileDamageType(int piObjectId)
    {
        return UWPlayerAttack.GetMissileDamageType(mOUWDataImporter, piObjectId);
    }

    /// <summary>
    /// Begins a strike: the wind-up starts, the charge waits for it (fUpdateCharge, by the
    /// weapon's speed, fGetChargeSpeed).
    /// </summary>
    private void fBeginAttack()
    {
        fChooseAttackKind();

        fClearCharge();
        miChargeSpeed = fGetChargeSpeed();
        IsAttacking = true;
    }

    /// <summary>play_pow 0 and the step clock stopped (player_attack at the swing's start).</summary>
    private void fClearCharge()
    {
        miPlayPow = 0;
        mfChargeElapsed = -1f;
    }

    /// <summary>
    /// What one charge step adds for the weapon in hand: the melee table's speed (wd->speed in
    /// player_attack, COMBAT.C), the fist's for anything that is not a melee weapon
    /// (GetPlayerWeapon). A heavy weapon charges slowly to a high maximum, a light one quickly
    /// to a low one:
    ///
    ///   a_dagger        speed 30  MaxCharge 145   -> short
    ///   a_longsword     speed 15  MaxCharge 190
    ///   a_battle axe    speed  5  MaxCharge 220   -> long
    ///
    /// THE TIMING FOLLOWS THE ORIGINAL BY THE USER'S DECISION OF 2026-10-12 (combat C1, C2): the
    /// charge starts at 0 after the wind-up (four frames of 0.125 s) and gains the speed every
    /// UWSettings.AttackChargeStepSeconds; a launcher does not charge at all. Until then a fixed
    /// base of 0.73 s plus the steps was counted from the press, fitted to the user's stopwatch
    /// (2026-08-30, press to green: dagger 0.9, fist 1.2, Sword of Justice 1.4, broadsword 1.4 -
    /// 1.5, battle axe 2.05 s). The step of 0.072 s from that fit stays: wind-up 0.5 s plus the
    /// steps to frame 9 meets the same measurements within a reaction time (dagger 0.79, battle
    /// axe 1.94). The original's step is 0x10 ticks of its game clock, 0.0625 s if that is the
    /// 1/256 s clock (not settled).
    /// </summary>
    private int fGetChargeSpeed()
    {
        UWObject lOWeapon = mInventory == null ? null : mInventory.GetEquipped(mInventory.MainHandSlot);
        int liWeaponId = lOWeapon == null || lOWeapon.ID > UWPlayerCritterRow.MeleeLastId ? UWCombat.FistObjectId : lOWeapon.ID;

        UWObjectClassProperties.MeleeWeapon lOMelee;

        if (mOUWDataImporter == null || mOUWDataImporter.ObjectClassProperties == null
            || !mOUWDataImporter.ObjectClassProperties.TryGetMeleeWeapon(liWeaponId, out lOMelee)
            || lOMelee.AttackSpeed <= 0)
            return FallbackChargeSpeed;

        return lOMelee.AttackSpeed;
    }

    /// <summary>The fist's speed, for a weapon table that cannot be read.</summary>
    private const int FallbackChargeSpeed = 15;

    /// <summary>
    /// Chooses the attack type from the pointer position in the view window: upper third
    /// overhead strike, lower third stab, in between the sideways slash (per user,
    /// 2026-08-30). The same division into thirds that movement also follows.
    ///
    /// Both are set: the type for the damage calculation (mCurrentAttack, see
    /// fGetAttackKind) and the one for the weapon animation (UWWeaponSwing.AttackType).
    ///
    /// Unarmed there is only ONE animation - the weapon data list the same coordinates for the fist
    /// for all three types. The damage values of the melee table do differ there
    /// though (slash 2, bash 4, stab 3), so the
    /// type is evaluated with bare hands too. Whether the original does the same is open.
    /// </summary>
    private void fChooseAttackKind()
    {
        // THE MODERN SCHEME takes the view's height for the pointer's third (per user, 2026-10-03,
        // after trying the mouse movement while winding up - the view turned with it and the kind
        // came out unreliably): looking up an overhead bash, down a thrust, straight a slash,
        // fixed when the button goes down, as in the original.
        //
        // WITH THE POINTER FREE the original's thirds again, of the screen - the modern view fills
        // it (per user, 2026-10-04): upper third an overhead bash, lower a thrust, between a slash.
        if (UWControlScheme.IsModern && fIsPointerFree() && Mouse.current != null)
        {
            float lfAt = Mouse.current.position.ReadValue().y / Mathf.Max(1f, Screen.height);

            if (lfAt >= UpperThirdFraction)
                fSetAttackKind(AttackTypes.Hack);
            else if (lfAt <= LowerThirdFraction)
                fSetAttackKind(AttackTypes.Stab);
            else
                fSetAttackKind(AttackTypes.Slash);

            return;
        }

        if (UWControlScheme.IsModern)
        {
            float lfPitch = fGetViewPitchUp();

            if (lfPitch >= ModernSwingPitch)
                fSetAttackKind(AttackTypes.Hack);
            else if (lfPitch <= -ModernSwingPitch)
                fSetAttackKind(AttackTypes.Stab);
            else
                fSetAttackKind(AttackTypes.Slash);

            return;
        }

        float lfFraction = mGameUi != null
            ? mGameUi.GetVerticalFractionInGameArea(Mouse.current.position.ReadValue())
            : 0.5f;

        if (lfFraction >= UpperThirdFraction)
            fSetAttackKind(AttackTypes.Hack);
        else if (lfFraction <= LowerThirdFraction)
            fSetAttackKind(AttackTypes.Stab);
        else
            fSetAttackKind(AttackTypes.Slash);

        // THE COLUMN COUNTS TOO (by the user's decision of 2026-10-12 to follow the original,
        // combat C6): the original's swing is the clicked ninth, 1 to 9 from the bottom left, and
        // resolve_attack raises the blow by towhere / 3 thirds of the body - so the right column
        // (3, 6, 9) sits a third higher than the rest of its row. Until then ours took the middle
        // column for every row. The modern scheme keeps the middle column (fSetAttackKind).
        float lfAcross = mGameUi != null
            ? mGameUi.GetHorizontalFractionInGameArea(Mouse.current.position.ReadValue())
            : 0.5f;
        int liColumn = lfAcross >= UpperThirdFraction ? 2 : lfAcross <= LowerThirdFraction ? 0 : 1;

        miCurrentNinth = (fGetSwingRow() * 3) + liColumn + 1;
    }

    /// <summary>The row of the ninths for the current kind: 0 the stab (bottom), 1 the slash, 2
    /// the overhead bash (top) - swing_kind of COMBAT.C read backwards.</summary>
    private int fGetSwingRow()
    {
        switch (mCurrentAttack)
        {
            case AttackEnum.Stab: return 0;
            case AttackEnum.Slash: return 1;
            default: return 2;
        }
    }

    /// <summary>Sets both the kind for the damage (mCurrentAttack) and the one for the weapon
    /// animation (UWWeaponSwing.AttackType, read every frame, so a change while the weapon
    /// winds up shows at once).</summary>
    private void fSetAttackKind(AttackTypes peKind)
    {
        switch (peKind)
        {
            case AttackTypes.Hack: mCurrentAttack = AttackEnum.Jab; break;
            case AttackTypes.Stab: mCurrentAttack = AttackEnum.Stab; break;
            default: mCurrentAttack = AttackEnum.Slash; break;
        }

        if (mCharacter != null)
            mCharacter.Weapon.AttackType = peKind;

        // The middle column of the kind's row; the classic scheme sets the clicked column after.
        miCurrentNinth = (fGetSwingRow() * 3) + 2;
    }

    /// <summary>How far the view has to look up or down, in degrees, for a bash or a thrust
    /// (modern scheme).</summary>
    private const float ModernSwingPitch = 12f;

    /// <summary>The camera's pitch in degrees, positive looking up.</summary>
    private float fGetViewPitchUp()
    {
        Camera lOCamera = Camera.main;

        return lOCamera != null ? -Mathf.DeltaAngle(0f, lOCamera.transform.eulerAngles.x) : 0f;
    }

    /// <summary>
    /// Takes the weapon from the main hand into the display. Called every frame while
    /// combat mode is active, because the weapon can be changed in the middle of combat
    /// mode (see Update and UWWeaponSwing.SetWeaponType); it only does something when the
    /// weapon has actually changed.
    ///
    /// Which animation belongs to which weapon is given by the SkillType field of the
    /// melee table - the same source as for the skill.
    /// </summary>
    private void fUpdateWeaponType()
    {
        if (mCharacter == null || mOUWDataImporter == null)
            return;

        UWObject lOWeapon = mInventory == null ? null : mInventory.GetEquipped(mInventory.MainHandSlot);
        int liWeaponId = lOWeapon == null ? UWCombat.FistObjectId : lOWeapon.ID;

        // WITH BOW OR SLING NOTHING IS VISIBLE - see UWWeaponSwing.HasRangedWeapon.
        int liAmmunitionId;

        // What was in view a moment ago decides whether the change needs the two motions -
        // see UWWeaponSwing.SetWeaponType. With a bow in hand nothing was.
        bool lbNothingWasVisible = mCharacter.Weapon.HasRangedWeapon;

        mCharacter.Weapon.HasRangedWeapon = fTryGetAmmunitionId(liWeaponId, out liAmmunitionId);

        if (mCharacter.Weapon.HasRangedWeapon)
            return;

        UWObjectClassProperties.MeleeWeapon lOMelee;
        WeaponTypes leType = WeaponTypes.RightHandFist;

        if (mOUWDataImporter.ObjectClassProperties.TryGetMeleeWeapon(liWeaponId, out lOMelee))
            leType = UWCombat.GetWeaponTypeForWeapon(lOMelee, mInventory != null && mInventory.IsLeftHanded);
        else if (mInventory != null && mInventory.IsLeftHanded)
            leType = WeaponTypes.LeftHandFist;

        // AN EMPTY HAND IS THE FIST, not whatever was held before. Without this the weapon
        // type simply kept its last value, which is how an axe could turn up in a hand that
        // held none (per user, 2026-09-20).
        mCharacter.Weapon.SetWeaponType(leType, lbNothingWasVisible);
    }
    private static UWCombat.AttackKind fGetAttackKind(AttackEnum peAttack)
    {
        switch (peAttack)
        {
            case AttackEnum.Slash:
                return UWCombat.AttackKind.Slash;

            case AttackEnum.Stab:
                return UWCombat.AttackKind.Stab;

            default:
                // Jab corresponds to the bash attack of the original table.
                return UWCombat.AttackKind.Bash;
        }
    }

    /// <summary>
    /// A critical hit on the player wears the piece the body part picks by 2d4 (UWEquipmentWear,
    /// built 2026-09-28) - called by the creature's blow (UWCritter.fStrikePlayer).
    /// </summary>
    public void WearOnCriticalHit(int piPart)
    {
        if (mInventory == null)
            return;

        int liSlot = UWCritterCombat.GetCriticalEquipmentSlot(piPart, !mInventory.IsLeftHanded, UWRandom.Next(0, 5));

        fWearEquipment(UWInventoryModel.FromSavegameSlot(liSlot),
            UWRandom.RollDice(UWEquipmentWear.CriticalHitDiceCount, UWEquipmentWear.CriticalHitDiceRange), false);
    }

    /// <summary>The weapon hand wears by count d range - only a melee weapon does.</summary>
    private void fWearWeapon(int piDiceCount, int piDiceRange)
    {
        if (mInventory != null)
            fWearEquipment(mInventory.MainHandSlot, UWRandom.RollDice(piDiceCount, piDiceRange), true);
    }

    /// <summary>
    /// ovr120_C0F: the piece in the slot takes the damage, the message goes to the scroll, and a
    /// destroyed piece leaves the inventory and lies as a pile of debris at the player's feet.
    /// </summary>
    private void fWearEquipment(UWArmorItemMap.BodySlot peSlot, int piDamage, bool pbWeaponTest)
    {
        if (mInventory == null || mOUWDataImporter == null)
            return;

        UWObject lOItem = mInventory.GetEquipped(peSlot);

        if (pbWeaponTest ? !UWEquipmentWear.IsWearableWeapon(lOItem) : !UWEquipmentWear.IsWearableArmour(lOItem, peSlot))
            return;

        UWEquipmentWear.Outcome leOutcome = UWEquipmentWear.Wear(lOItem, piDamage, mOUWDataImporter, out string lsMessage);

        if (leOutcome == UWEquipmentWear.Outcome.None)
            return;

        AddMessage(lsMessage);

        if (leOutcome == UWEquipmentWear.Outcome.Destroyed)
        {
            mInventory.RemoveItem(lOItem);

            if (mLevelLoader != null && mCharacter != null)
            {
                Vector3 lOAt = mCharacter.transform.position;
                UWTilePos lOTile = UWTileQueries.WorldToTile(lOAt.x, lOAt.z);

                // A NEW pile from CreateObj (INVDATA.C: junk = CreateObj(debris), nothing set on
                // it afterwards), so CreateObj's quality 40 - until 2026-10-12 this gave 0, as
                // the smashed world object's debris rightly does (UWContainerDamage). Put down
                // with near_mob_put_at, so at the tail of the tile's list (2026-10-18).
                mLevelLoader.SpawnObjectById(UWObjectDamageRules.RollDebrisObjectId(), lOTile.X, lOTile.Y, 0, -1,
                    new Vector3(lOAt.x, 0f, lOAt.z), UWWorldEdit.Arrival.PutDown);
            }
        }

        mInventory.NotifyChanged();

        if (mCharacter != null)
            mCharacter.RefreshStatus();
    }

    /// <summary>Whether a struck creature bleeds (its table's blood bits, Critter.BleedsOnHit).</summary>
    private bool fBleeds(UWEntityInfo pOTarget)
    {
        return pOTarget != null && pOTarget.ObjectData != null && mOUWDataImporter != null
            && mOUWDataImporter.ObjectClassProperties != null
            && mOUWDataImporter.ObjectClassProperties.TryGetCritter(pOTarget.ObjectData.ID, out UWObjectClassProperties.Critter lOCritter)
            && lOCritter.BleedsOnHit;
    }

    /// <summary>do_damage's second splat on a player critical: (rand &amp; 1) * 5 - 2 more steps of
    /// put_effect's height unit, the creature's height &gt;&gt; 3 (at least 1), in world units.</summary>
    private float fSecondSplatOffset(UWEntityInfo pOTarget)
    {
        int liHeight = 0;

        if (pOTarget != null && pOTarget.ObjectData != null && mOUWDataImporter != null && mOUWDataImporter.CommonObjectProperties != null
            && mOUWDataImporter.CommonObjectProperties.TryGet(pOTarget.ObjectData.ID, out UWCommonObjectProperties.Entry lOEntry))
            liHeight = lOEntry.Height;

        int liUnit = Mathf.Max(1, liHeight >> 3);
        int liSteps = (UWRandom.Next(0, 2) * 5) - 2;

        return liSteps * liUnit * UWWorldScale.ZPosStep;
    }

    /// <summary>
    /// The visible hit: a blood splatter, or an impact for bloodless beings. What
    /// the creature leaves behind is in its table (see
    /// UWObjectMechanics.GetHitEffectObjectId); everything else gets the flash.
    /// pfExtraHeight raises it (world units) - the second splat of a critical.
    /// </summary>
    private void fShowHitEffect(UWEntityInfo pOTarget, Vector3 pOHitPoint, float pfExtraHeight = 0f, int piPart = -1)
    {
        if (pOTarget == null || pOTarget.ObjectData == null || mLevelLoader == null
            || mOUWDataImporter == null || mOUWDataImporter.ObjectClassProperties == null)
            return;

        // A CREATURE bleeds or dusts according to its table entry; EVERYTHING ELSE - door,
        // chest, barrel - gets the flash (animation object 448 + 0x0B, the reference calls it
        // "a flash" and spawns it for object hits, combat.cs). It was missing on every
        // non-creature so far (per user with a screenshot of the original, 2026-09-16).
        UWObjectClassProperties.Critter lOCritter;

        bool lbIsCritter = mOUWDataImporter.ObjectClassProperties.TryGetCritter(pOTarget.ObjectData.ID, out lOCritter)
            && lOCritter.Vitality != 0;

        int liEffectId = lbIsCritter
            ? UWObjectMechanics.GetHitEffectObjectId(lOCritter.BloodAndRemains)
            : UWObjectMechanics.FlashEffectObjectId;

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        float lfSeconds = lOSettings != null ? lOSettings.HitEffectSeconds : 0.6f;
        float lfDelay = lOSettings != null ? lOSettings.HitEffectDelaySeconds : 0.2f;

        float lfVariation = lOSettings != null ? lOSettings.HitEffectHeightVariation : 8f;

        float lfDepthBias = 0f;
        Vector3 lOAt = lbIsCritter
            ? fVaryHitHeight(pOTarget, pOHitPoint, lfVariation)
            : fGetObjectFlash(pOTarget, fGetDoorFlashPoint(pOTarget, pOHitPoint), out lfDepthBias);

        // A BLOW WITH A KNOWN PART bleeds at that part's height (do_damage's hitz_tab, C3,
        // 2026-10-12): the variation the user saw on the original (2026-08-30) is the random part.
        UWCritter lOStruck = lbIsCritter && piPart >= 0 ? pOTarget.GetComponentInParent<UWCritter>() : null;

        if (lOStruck != null)
            lOAt.y = lOStruck.BodyPosition.y + lOStruck.GetHitEffectHeight(piPart);

        lOAt.y += pfExtraHeight;

        StartCoroutine(fShowHitEffectDelayed(liEffectId, lOAt, lfDelay, lfSeconds, lfDepthBias));
    }

    /// <summary>
    /// WHERE A STRUCK DOOR FLASHES (AttackerAppliesFinalDamage_seg022_8A5, the branch for class
    /// 0x14): at the DOOR OBJECT'S OWN SPOT, not where the blow or the missile met it, at the
    /// height of the attack but at least two above the door's zpos. Seen by the user on the
    /// original, 2026-09-24: a Magic Missile still left of and above the door set it off, and
    /// the flash sat at the top of the leaf. Anything else keeps the hit point.
    /// </summary>
    private static Vector3 fGetDoorFlashPoint(UWEntityInfo pOTarget, Vector3 pOHitPoint)
    {
        if (pOTarget == null || !pOTarget.IsDoor || pOTarget.ObjectData == null)
            return pOHitPoint;

        UWObject lODoor = pOTarget.ObjectData;
        Vector3 lOSpot = UWViewpoint.SubTileToWorld(lODoor.TileX, lODoor.TileY, lODoor);
        float lfLowest = lOSpot.y + (DoorFlashAbove * UWWorldScale.ZPosStep);

        return new Vector3(lOSpot.x, Mathf.Max(pOHitPoint.y, lfLowest), lOSpot.z);
    }

    /// <summary>The flash sits at least this many zpos above the door's own - see fGetDoorFlashPoint.</summary>
    private const int DoorFlashAbove = 2;

    /// <summary>The flash on a struck sprite (a sack, a box) is sorted this many world units
    /// nearer than the sprite: two billboards on one spot would fight for the pixel.</summary>
    private const float SpriteFlashDepthBias = 4f;

    /// <summary>The flash on a struck model is sorted this far in front of the model's front
    /// towards the camera, in world units: enough that its own width clears a barrel's curve.</summary>
    private const float ModelFlashClearance = 4f;

    /// <summary>
    /// Where the flash appears when an OBJECT is struck - chest, barrel, sack, door - and how
    /// it is sorted.
    ///
    /// THE ORIGINAL puts it at the object's own spot: put_effect (EFFECT.C) copies the
    /// defender's fine position into the effect, AttackerAppliesFinalDamage gives the height
    /// of the blow (COMBAT.C hitz: the attacker's z plus a sixth of his height, a door at
    /// least two above its own z), and the flash, added behind the object in the tile's list,
    /// is drawn over it - over a chest's model too.
    ///
    /// So the flash stands AT THE OBJECT'S SPOT (a sprite's data position, a model's collider
    /// middle) at the height of the blow, and is SORTED IN FRONT of the object by the billboard
    /// shaders' depth bias (UWLevelLoader.SpawnEffectAt): a sprite by a hair, a model by its
    /// extent towards the camera plus ModelFlashClearance, so the mesh does not hide it (per
    /// user, 2026-09-16: "the impact effect is inside the barrel"). Until 2026-10-12 the flash
    /// MOVED instead - 12 units in front of the crosshair's ray hit, then in front of the
    /// model's face - and stood a fifth of a tile out (per user on the original: "closer to the
    /// chest", "too near the camera").
    /// </summary>
    private Vector3 fGetObjectFlash(UWEntityInfo pOTarget, Vector3 pOHitPoint, out float pfDepthBias)
    {
        pfDepthBias = SpriteFlashDepthBias;

        if (pOTarget == null || pOTarget.ObjectData == null)
            return pOHitPoint;

        UWCommonObjectProperties.Entry lOEntry;
        bool lbModel = pOTarget.IsDoor
            || mOUWDataImporter == null || mOUWDataImporter.CommonObjectProperties == null
            || !mOUWDataImporter.CommonObjectProperties.TryGet(pOTarget.ObjectData.ID, out lOEntry)
            || lOEntry.Is3DModel;

        if (!lbModel)
        {
            UWObject lOObject = pOTarget.ObjectData;
            Vector3 lOSpot = UWObjectSpawner.GetObjectWorldPosition(lOObject.TileX, lOObject.TileY, lOObject);

            return new Vector3(lOSpot.x, pOHitPoint.y, lOSpot.z);
        }

        Collider lOCollider = pOTarget.GetComponentInChildren<Collider>();

        if (lOCollider == null)
            return pOHitPoint;

        Bounds lOBounds = lOCollider.bounds;
        Vector3 lOToCamera = Camera.main != null ? Camera.main.transform.position - lOBounds.center : Vector3.forward;

        lOToCamera.y = 0f;

        if (lOToCamera.sqrMagnitude < 0.0001f)
            lOToCamera = Vector3.forward;

        lOToCamera.Normalize();

        // The collider's extent along the line to the camera: the model's front lies that far
        // before its middle.
        float lfAlongX = Mathf.Abs(lOToCamera.x) > 0.0001f ? lOBounds.extents.x / Mathf.Abs(lOToCamera.x) : float.MaxValue;
        float lfAlongZ = Mathf.Abs(lOToCamera.z) > 0.0001f ? lOBounds.extents.z / Mathf.Abs(lOToCamera.z) : float.MaxValue;

        pfDepthBias = Mathf.Min(lfAlongX, lfAlongZ) + ModelFlashClearance;

        return new Vector3(lOBounds.center.x, pOHitPoint.y, lOBounds.center.z);
    }

    /// <summary>
    /// The spot where the hit effect appears on a CREATURE.
    ///
    /// HORIZONTALLY the figure itself, NOT the point where the ray hit:
    /// a creature's collider is square in plan and thus for a
    /// goblin 68 units deep, so its ray hit lies half a tile in front of the
    /// figure. The splatter therefore floated in front of the enemy and looked too big up close
    /// (per user, 2026-08-30).
    ///
    /// VERTICALLY scattered: in the original the effect sits sometimes higher, sometimes lower, but always
    /// visibly on the enemy - so the random value is then clamped back into the extent of the
    /// target. A splatter next to the figure would no longer be randomness, but a
    /// bug.
    /// </summary>
    private static Vector3 fVaryHitHeight(UWEntityInfo pOTarget, Vector3 pOHitPoint, float pfVariation)
    {
        Vector3 lOPoint = pOHitPoint;

        if (pOTarget != null)
        {
            lOPoint.x = pOTarget.transform.position.x;
            lOPoint.z = pOTarget.transform.position.z;
        }

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;
        Collider lOCollider = pOTarget == null ? null : pOTarget.GetComponentInChildren<Collider>();

        // The starting point is the enemy's BODY CENTRE, not the ray hit. That lies
        // at the player's eye height and thus moves with distance and view angle; the
        // centre does not (per user, 2026-08-30).
        if (lOCollider != null)
            lOPoint.y = lOCollider.bounds.center.y;

        lOPoint.y += lOSettings != null ? lOSettings.HitEffectHeightOffset : 0f;

        if (pfVariation > 0f)
            lOPoint.y += Random.Range(-pfVariation, pfVariation);

        if (lOCollider != null)
        {
            // A little away from the edge, so the effect does not stick halfway out of the figure.
            float lfMargin = lOCollider.bounds.size.y * 0.15f;

            lOPoint.y = Mathf.Clamp(lOPoint.y,
                lOCollider.bounds.min.y + lfMargin, lOCollider.bounds.max.y - lfMargin);
        }

        return lOPoint;
    }

    /// <summary>
    /// The effect only appears when the strike visually lands - in the original the
    /// blood appears as soon as the weapon animation has reached the screen centre, not already on
    /// releasing the button (per user, 2026-08-30). The damage is still applied
    /// immediately; only the picture waits.
    /// </summary>
    private System.Collections.IEnumerator fShowHitEffectDelayed(int piObjectId, Vector3 pOHitPoint,
        float pfDelay, float pfSeconds, float pfDepthBias = 0f)
    {
        if (pfDelay > 0f)
            yield return new WaitForSeconds(pfDelay);

        if (mLevelLoader != null)
            mLevelLoader.SpawnEffectAt(piObjectId, pOHitPoint, pfSeconds, pfDepthBias);
    }

    /// <summary>
    /// A click on one of the three arrows under the compass: the easy movement
    /// (UWEasyMovement, UWHudCompass.TryGetEasyMovementCommand). Both mouse buttons do it, as
    /// with the compass itself - the original's region handler does not distinguish them
    /// either. Returns true if the click is used up.
    /// </summary>
    private bool fTryEasyMovement(Vector2 pOScreenPos)
    {
        if (mGameUi == null || !mGameUi.TryGetEasyMovementCommand(pOScreenPos, out int liCommand))
            return false;

        UWPlayerMovement lOMovement = UWScene.PlayerMovement;

        if (lOMovement != null)
            lOMovement.Input.HoldEasyMovement(liCommand);

        return true;
    }

    /// <summary>
    /// A click on the compass prints the character's status - hunger, fatigue,
    /// level, day and time of day (see UWStatusReport).
    ///
    /// BOTH MOUSE BUTTONS do the same. The reference does not distinguish them
    /// (uimanager_compass._on_compass_click only checks THAT a button was pressed).
    ///
    /// Returns true if the click is used up - then it must not trigger anything else.
    /// </summary>
    private bool fTryReportStatus(Vector2 pOScreenPos)
    {
        if (mGameUi == null || !mGameUi.IsScreenPositionOnCompass(pOScreenPos))
            return false;

        mGameUi.ReportStatus();

        return true;
    }

    /// <summary>
    /// Look message of a target. Doors get their condition inserted - checked by
    /// user in the original (2026-08-27): "Right-click look shows the condition of the
    /// door. Sturdy, badly damaged, broken."
    /// </summary>
    private string fGetLookDescription(UWEntityInfo pOTarget)
    {
        // Wall inscriptions already carry their finished wording (see UWEntityInfo).
        if (pOTarget.ShowDescriptionVerbatim)
            return pOTarget.Description;

        // AN ITEM ON THE FLOOR is described like one in the backpack. This shows with
        // stacks: the original counts them in the world too ("13 arrows" instead of "an
        // arrow", per user, 2026-09-07). With the same message the condition word and
        // enchantment come along - the same information as in the inventory, which should
        // be no different from there.
        //
        // Only for pickable things: creatures, doors and fixed scenery do carry
        // object data too, but no quantity and no item condition.
        if (pOTarget.CanBePickedUp && pOTarget.ObjectData != null)
        {
            string lsItemMessage = DescribeInventoryItem(pOTarget.ObjectData, WorldLookDetail);

            if (!string.IsNullOrEmpty(lsItemMessage))
                return lsItemMessage;
        }

        string lsRaw = pOTarget.Description;

        // A CREATURE WITH ATTITUDE: "You see a mellow outcast named Bragit." (per user
        // in the original, 2026-09-12). The word comes from string block 5 from 96 (hostile, upset,
        // mellow, friendly), the article follows it, the name is in block 7 and
        // appears immediately, without having to have talked - as in the reference
        // (npc.RegularNPCDescription).
        // Only for a creature that is not an item (the wording is in UWItemDescriptions).
        string lsCreature = pOTarget.CanBePickedUp
            ? null
            : UWItemDescriptions.DescribeCreature(pOTarget.ObjectData as UWNpc, lsRaw, mOUWDataImporter);

        if (lsCreature != null)
            return lsCreature;

        if (pOTarget.IsDoor)
        {
            // A SPIKED DOOR is described like any door; "The door is spiked." follows as a line of
            // its own (fGetSpecialLookLine). Until 2026-10-13 ours said only that, after the
            // reference's door.LookAt, which leaves before the description (per user on the
            // original: the door's sentence first, then the spike).

            // OPEN OR CLOSED comes from the door itself, not from the text frozen at spawn time:
            // the object id only changes when the world is captured, the leaf swings long before.
            // Block 4 names the closed doors "a_door", the open ones "an_open door" - the door on
            // level 2, tile 61/6 said "open door" whether open or not (per user, 2026-09-18).
            IUsableDoor lOMover = pOTarget.GetComponentInParent<IUsableDoor>();

            // A DOOR ON ITS WAY IS "a moving door": the original swaps it for object 0x1CF
            // while it swings or the grate runs (OpenDoor_seg040_352B_21E6 through
            // seg040_352B_20CD), and looking at that names only it - no condition, no owner
            // (per user, 2026-09-27; ours named the door as it stood before).
            if (lOMover != null && lOMover.IsMoving)
            {
                string lsMoving = fGetObjectName(MovingDoorObjectId);

                if (!string.IsNullOrEmpty(lsMoving))
                    return UWItemDescriptions.FormatLookMessageWithOwner(lsMoving, null, mOUWDataImporter);
            }

            string lsStateName = lOMover != null && pOTarget.ObjectData != null
                ? fGetDoorName(pOTarget.ObjectData.ID, lOMover.IsClosed) : null;

            if (!string.IsNullOrEmpty(lsStateName))
                lsRaw = lsStateName;

            UWDamageable lODamageable = pOTarget.GetComponentInParent<UWDamageable>();
            string lsCondition = lODamageable == null ? null : fGetConditionWord(lODamageable);

            if (!string.IsNullOrEmpty(lsCondition))
                lsRaw = UWItemDescriptions.InsertCondition(lsRaw, lsCondition);
        }

        // WHO OWNS IT belongs on fixed scenery too: the original says "You see a sturdy chest
        // belonging to a green goblin." for barrels and chests (per user with a screenshot,
        // 2026-09-16). Items on the floor get it through DescribeInventoryItem above.
        return UWItemDescriptions.FormatLookMessageWithOwner(lsRaw, pOTarget.ObjectData, mOUWDataImporter);
    }

    /// <summary>
    /// SpecialLook (LOOK.C), the line after the look sentence: for a closed door at rest whose
    /// owner bit 0 is set, "The door is spiked." (string 0x83); for a key or bones the line of
    /// UWItemDescriptions.SpecialLookLine. Null when there is none.
    /// </summary>
    private string fGetSpecialLookLine(UWEntityInfo pOTarget)
    {
        if (pOTarget == null || pOTarget.ObjectData == null)
            return null;

        // A key, bones: UWItemDescriptions.SpecialLookLine - but in the world only with detail.
        // player_3dlook (INTERACT.C) passes LookAt a lore of 1 only for a thing that cannot be
        // picked up and lies in reach, else 0, and KeyLook and BonesLook print only above 0.
        // Every key and every skull or bone can be picked up, so in the world neither says
        // more; the inventory look does (per user on the original, 2026-10-13: the key's line
        // only in the inventory). The spiked door's line asks for no detail.
        if (!pOTarget.IsDoor)
            return pOTarget.CanBePickedUp ? null
                : UWItemDescriptions.SpecialLookLine(pOTarget.ObjectData, mOUWDataImporter);

        if (!UWObjectMechanics.IsDoorSpiked(pOTarget.ObjectData))
            return null;

        IUsableDoor lOMover = pOTarget.GetComponentInParent<IUsableDoor>();

        if (lOMover != null && (!lOMover.IsClosed || lOMover.IsMoving))
            return null;

        return GetGeneralMessage(UWItemApplications.DoorIsSpikedMessage);
    }

    /// <summary>The block 4 name of a door in its current state: the closed doors are 320 to
    /// 327, the same doors open lie eight higher (see UWWorldCapture). Null without data.</summary>
    private string fGetDoorName(int piObjectId, bool pbClosed)
    {
        const int liOpenOffset = UWWorldCapture.FirstOpenDoorId - UWWorldCapture.FirstClosedDoorId;

        if (mOUWDataImporter == null || piObjectId < UWWorldCapture.FirstClosedDoorId || piObjectId > UWWorldCapture.LastOpenDoorId)
            return null;

        int liClosedId = piObjectId >= UWWorldCapture.FirstOpenDoorId ? piObjectId - liOpenOffset : piObjectId;

        return UWText.RawObjectName(mOUWDataImporter, pbClosed ? liClosedId : liClosedId + liOpenOffset, null);
    }

    /// <summary>"a_moving door", the class-7 object a door becomes while it moves.</summary>
    private const int MovingDoorObjectId = 0x1CF;

    /// <summary>The raw block 4 name of an object, or null without data.</summary>
    private string fGetObjectName(int piObjectId)
    {
        return UWText.RawObjectName(mOUWDataImporter, piObjectId, null);
    }

    /// <summary>Condition word from string block 5 - broken, badly damaged, damaged, sturdy,
    /// massive - for a door or another thing with health (see UWItemDescriptions).</summary>
    private string fGetConditionWord(UWDamageable pODamageable)
    {
        return UWItemDescriptions.GetConditionWord(pODamageable.GetConditionStringIndex(), mOUWDataImporter);
    }

    /// <summary>
    /// The look message for an item in the inventory or on the floor, with condition word,
    /// quantity, enchantment and owner - the wording is in UWItemDescriptions (P3 of the
    /// engine separation, 2026-09-18).
    /// </summary>
    public string DescribeInventoryItem(UWObject pOItem, int piDetail = UWItemDescriptions.DetailFromLoreCheck)
    {
        if (pOItem == null || mOUWDataImporter == null)
            return null;

        return UWItemDescriptions.DescribeItem(pOItem, mOUWDataImporter,
            mCharacter != null ? mCharacter.Vitals.GetSkill(UWPlayerData.Skill.Lore) : 0,
            fGetItemObjectList(), piDetail);
    }

    /// <summary>The detail level of a look at an item in the world: always 1, no Lore roll
    /// (Look_seg024_24DC_D20, see UWItemDescriptions.DetailFromLoreCheck).</summary>
    private const int WorldLookDetail = 1;

    /// <summary>Looking at an item in the inventory: its look sentence, then SpecialLook's line
    /// (UWItemDescriptions.SpecialLookLine) - both schemes.</summary>
    public void AddItemLookMessages(UWObject pOItem)
    {
        AddMessage(DescribeInventoryItem(pOItem));

        string lsSpecial = UWItemDescriptions.SpecialLookLine(pOItem, mOUWDataImporter);

        if (lsSpecial != null)
            AddMessage(lsSpecial);
    }

    /// <summary>
    /// Identifies an item permanently and describes it right away - the spell Name
    /// Enchantment (see UWLoreCheck.Identify and UWMiscSpell).
    ///
    /// Returns false if the item cannot be identified; then the
    /// caller reports "The spell has no discernable effect.".
    /// </summary>
    public bool IdentifyItem(UWObject pOItem)
    {
        if (pOItem == null || mOUWDataImporter == null)
            return false;

        if (!UWLoreCheck.Identify(pOItem, mOUWDataImporter.CommonObjectProperties))
            return false;

        // LookAt(target, 3) (obj_spells, UnderworldExhumed combat/SPELLS.C): the look sentence and
        // SpecialLook's line at full detail - a book or scroll is read, a key or bones say their
        // line, in the world as carried (2026-10-15; until then the sentence alone, reference
        // audit round 2, A50).
        if (!TryReadBook(pOItem, true))
            AddItemLookMessages(pOItem);

        return true;
    }

    /// <summary>SpecialLook's line for a thing in the world that is looked at with full detail
    /// (Name Enchantment on a door: "The door is spiked."). Null when there is none.</summary>
    public string GetSpecialLookLine(UWEntityInfo pOTarget)
    {
        return fGetSpecialLookLine(pOTarget);
    }

    /// <summary>The object list that holds inventory items - needed to find the linked
    /// spell object from a wand. If the game state comes from a save file,
    /// it is that file's record list, otherwise the level's.</summary>
    private System.Collections.Generic.List<UWObject> fGetItemObjectList()
    {
        return UWItemDescriptions.ItemObjectList(mOUWDataImporter,
            mLevelLoader != null ? mLevelLoader.CurrentLevel : null);
    }

    /// <summary>Confirmed by original test (see UWDoor): a locked door cannot
    /// be opened manually, but can be closed at any time (the lock check only applies to
    /// opening). A switch/trigger/trap (UWTriggerSystem) bypasses this check completely
    /// and does not change the lock state either - both confirmed original behaviour.
    /// The decision is UWDoorRules.Use (2026-10-12), shared with the creatures' door use.
    ///
    /// Key usage deliberately differs per scheme. Modern has no dragging,
    /// hence an automatic backpack check (incl. containers, recursive - a key
    /// did NOT work in the first test when it lay in a container instead of directly in the
    /// backpack, see UWObjectMechanics.FindMatchingKey) - if it finds a matching
    /// key, the door is unlocked AND opened in one step.
    ///
    /// Original has NO automatic check - dragging the key onto the door was
    /// tried and explicitly confirmed as wrong by the user ("does not work in the
    /// original"). The real mechanism is the general use mode (see
    /// UWInventory.UseModeItem/UWItemDrag.fUpdateUseMode): right click on the key in the
    /// backpack attaches its icon to the cursor (item stays in place), right click on the door
    /// in the view window applies it - see UWItemTargetUse. This unlocks (or
    /// locks again) ONLY the lock state, it does NOT automatically open the door (confirmed
    /// by user) - unlike the Modern path here.</summary>
    private void fTryUseDoor(UWEntityInfo pOTarget)
    {
        UWDoor lODoor = pOTarget.GetComponentInParent<UWDoor>();

        if (lODoor == null)
            return;

        // The modern scheme's automatic key: asked only when it can matter (a closed, locked
        // door), so the backpack is not searched for every click on a door.
        bool lbKeyAtHand = UWControlScheme.IsModern && lODoor.IsClosed && lODoor.IsLocked
            && mInventory != null && mLevelLoader != null && mLevelLoader.CurrentLevel != null
            && UWObjectMechanics.FindMatchingKey(mInventory.Backpack, lODoor.LockId, mLevelLoader.CurrentLevel.Masterlist) != null;

        switch (UWDoorRules.Use(lODoor.IsClosed, lODoor.IsSpiked, lODoor.IsLocked, true, lbKeyAtHand))
        {
            case UWDoorRules.UseOutcome.Locked:
                mOActiveStrings.Add("The door is locked");
                return;

            case UWDoorRules.UseOutcome.Spiked:
                // Not for the player any more (2026-10-12: UseRect lets him open a spiked door,
                // OpenDoor takes the spike with it - the spike keeps creatures out); kept for
                // completeness.
                ReportSpikedDoor();
                return;

            case UWDoorRules.UseOutcome.UnlockAndOpen:
                lODoor.Unlock();
                break;
        }

        // The open trigger (an_open in the door's chain) is UWDoor.Toggle's, fired on OPENING.
        lODoor.Toggle(UWTrapRules.Setter.Player);
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
        // The same aim as the pointer's own (fBeginWorldPointer): by what is drawn - a model's
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

        if (fCanReach(pOTarget, UWReachRules.GetDistanceSquared(lbUsingPole, fHasTelekinesis), lbUsingPole))
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
        switch (fReach(pOTarget, UWReachRules.GetPickupDistanceSquared(fHasTelekinesis), false, true))
        {
            case ReachEnum.Reached:
                return -1;

            case ReachEnum.Blocked:
                return UWReachRules.CannotReachMessage;

            default:
                return UWReachRules.TooFarToTakeMessage;
        }
    }

    private bool fHasTelekinesis
    {
        get { return mCharacter != null && mCharacter.Vitals.HasTelekinesis; }
    }

    private bool fCanReach(UWEntityInfo pOTarget, int piDistanceSquared, bool pbUsingPole)
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

    // ------------------------------------------------- Shrine

    private bool mbChanting;

    /// <summary>Whether a mantra is currently being typed - then the letters belong to the
    /// shrine, not to the key commands (see UWGameUI, key F).</summary>
    public bool IsChanting
    {
        get { return mbChanting; }
    }

    private string msMantra = string.Empty;

    /// <summary>
    /// The shrine asks for the mantra. Typing works as for a map note - the
    /// characters come via onTextInput, backspace and enter are handled here.
    ///
    /// AS THE ORIGINAL'S INPUT LINE (ProcessTypedInput_seg043_37F0_A5E, read 2026-09-26; per
    /// user on the original the same day: the pointer is gone and a mouse button ends it):
    /// the input holds everything - the pointer is hidden, the world stands still and no
    /// click reaches the game - and it ends on Enter, Escape or any mouse button. A mouse
    /// button takes the typed text as Enter does (the shrine does not look at how it ended);
    /// Escape puts "-" behind the prompt and chants the empty text, which is "That is not a
    /// mantra.". At most ten characters (the shrine's call passes 0x0A).
    /// </summary>
    private void fBeginMantra()
    {
        if (mbChanting)
            return;

        mbChanting = true;
        msMantra = string.Empty;

        if (Keyboard.current != null)
            Keyboard.current.onTextInput += fOnMantraInput;

        fHoldForTypedInput(true);
        SetPromptMessage(UWShrine.Prompt);
    }

    /// <summary>The longest mantra that can be typed.</summary>
    private const int MantraMaxLength = 10;

    /// <summary>The modal hold of a typed input (UWControlScheme.HoldUiModal).</summary>
    private const string TypedInputHold = "typed input";

    /// <summary>Set when an input ended by a mouse button: the hold and the mouse block stay
    /// until every button is up, so neither the release nor the held button acts in the game.
    /// </summary>
    private bool mbTypedInputSwallow;

    private void fHoldForTypedInput(bool pbHold)
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;

        if (lOScheme != null)
            lOScheme.SetUiModal(TypedInputHold, pbHold);

        UWMouseButtons.IsBlockedByPrompt = pbHold;

        // The modern scheme's box shows the pointer (UWModernQuestion) - hidden here it stayed
        // invisible under it (as with the yes/no questions).
        if (!UWControlScheme.IsModern)
            Cursor.visible = !pbHold;
    }

    /// <summary>For the modern scheme's mantra box (UWModernQuestion): the typed text so far.</summary>
    public string MantraText => msMantra;

    /// <summary>The mantra box's OK (as Enter) or Cancel (as Escape).</summary>
    public void FinishMantra(bool pbCancel)
    {
        if (mbChanting)
            fFinishMantra(pbCancel, false);
    }

    /// <summary>Lifts the hold of an input that a mouse button ended, once no button is held.
    /// </summary>
    private void fUpdateTypedInputSwallow()
    {
        if (!mbTypedInputSwallow)
            return;

        Mouse lOMouse = Mouse.current;

        if (lOMouse != null && (lOMouse.leftButton.isPressed || lOMouse.rightButton.isPressed
            || lOMouse.middleButton.isPressed))
            return;

        mbTypedInputSwallow = false;
        fHoldForTypedInput(false);
    }

    private void fOnMantraInput(char pcCharacter)
    {
        if (!mbChanting || pcCharacter < ' ' || pcCharacter > '~' || msMantra.Length >= MantraMaxLength)
            return;

        msMantra += pcCharacter;

        SetPromptMessage(UWShrine.Prompt + msMantra);
    }

    private bool fUpdateMantraPrompt()
    {
        if (!mbChanting)
            return false;

        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard == null)
            return true;

        if (lOKeyboard.backspaceKey.wasPressedThisFrame && msMantra.Length > 0)
        {
            msMantra = msMantra.Substring(0, msMantra.Length - 1);

            SetPromptMessage(UWShrine.Prompt + msMantra);
        }

        // Where no text events arrive (Linux), the keys themselves (UWTypedKeys).
        foreach (char lcChar in UWTypedKeys.ReadTyped())
            fOnMantraInput(lcChar);

        // Read straight from the mouse: UWMouseButtons is blocked for the game meanwhile.
        Mouse lOMouse = Mouse.current;

        // The modern scheme's box takes the mouse: OK and Cancel (UWModernQuestion).
        bool lbByMouse = !UWControlScheme.IsModern && lOMouse != null && (lOMouse.leftButton.wasPressedThisFrame
            || lOMouse.rightButton.wasPressedThisFrame || lOMouse.middleButton.wasPressedThisFrame);

        bool lbDone = lOKeyboard.enterKey.wasPressedThisFrame
            || lOKeyboard.numpadEnterKey.wasPressedThisFrame || lbByMouse;

        bool lbCancel = lOKeyboard.escapeKey.wasPressedThisFrame && !lbDone;

        if (!lbDone && !lbCancel)
            return true;

        fFinishMantra(lbCancel, lbByMouse);

        return true;
    }

    private void fFinishMantra(bool lbCancel, bool lbByMouse)
    {
        mbChanting = false;

        if (Keyboard.current != null)
            Keyboard.current.onTextInput -= fOnMantraInput;

        // THE MODERN SCHEME: cancelling (Cancel, the right button, Escape) leaves no trace - the
        // prompt line goes from the messages and nothing is chanted (per user, 2026-10-04); the
        // classic scheme keeps the original's dash and "That is not a mantra."
        if (lbCancel && UWControlScheme.IsModern)
        {
            if (miPromptLine >= 0 && miPromptLine < mOActiveStrings.Count)
                mOActiveStrings.RemoveAt(miPromptLine);

            miPromptLine = -1;
            msMantra = string.Empty;
            fHoldForTypedInput(false);
            return;
        }

        // Escape: the original writes a dash for the answer and chants the empty text.
        if (lbCancel)
        {
            msMantra = string.Empty;
            SetPromptMessage(UWShrine.Prompt + "-");
        }

        EndPromptMessage();

        if (lbByMouse)
            mbTypedInputSwallow = true;
        else
            fHoldForTypedInput(false);

        UWShrine.Chant(msMantra, mCharacter, this, mOUWDataImporter);

        msMantra = string.Empty;
    }

    // ------------------------------------------------- Repair

    // The estimate, the attempt and their messages are in UWRepairRules (P3, 2026-09-18).
    // ------------------------------------------------- Yes/no questions

    /// <summary>
    /// A YES/NO QUESTION IN THE MESSAGE SCROLL - repairing and the trap found on a look ask the
    /// same way (per user from the original, 2026-09-17 on repairing, 2026-09-30: "it must work
    /// exactly like there" for the trap): the pointer disappears while the question stands;
    /// "Yes" is preset; Y (and J) and N only switch the shown answer, Enter confirms it, Escape
    /// answers no; a RIGHT click confirms "Yes", a LEFT click turns it into "No" and cancels -
    /// in the modern scheme a box with OK and Cancel takes the mouse (UWModernQuestion).
    /// Not in the frame the question was asked, whose click chose the item.
    /// </summary>
    private bool mbAskingYesNo;

    /// <summary>The question hid the pointer (the classic scheme only), so the answer shows it again.</summary>
    private bool mbYesNoHidCursor;

    /// <summary>Frame in which the question was asked.</summary>
    private int miYesNoAskedFrame = -1;

    /// <summary>The question without its answer, so a cancel can show "No".</summary>
    private string msYesNoQuestion;

    /// <summary>Whether the pointer was visible before the question hid it (the Modern scheme hides
    /// it anyway).</summary>
    private bool mbYesNoCursorWasVisible = true;

    /// <summary>The answer currently shown, confirmed with Enter.</summary>
    private bool mbYesNoAnswer = true;

    /// <summary>What happens with the answer.</summary>
    private System.Action<bool> mOYesNoAnswered;

    private void fAskYesNo(string psQuestion, System.Action<bool> pOAnswered)
    {
        mbAskingYesNo = true;
        miYesNoAskedFrame = Time.frameCount;
        mOYesNoAnswered = pOAnswered;

        // The modern scheme's box shows the pointer itself (UWModernQuestion, under a modal hold);
        // hidden here and shown again by the hold in the same moment, Windows kept it invisible
        // until it left the window (per user, 2026-10-04).
        mbYesNoHidCursor = !UWControlScheme.IsModern;

        if (mbYesNoHidCursor)
        {
            mbYesNoCursorWasVisible = Cursor.visible;
            Cursor.visible = false;
        }

        msYesNoQuestion = psQuestion;
        mbYesNoAnswer = true;
        SetPromptMessage(msYesNoQuestion + "Yes");
    }

    /// <summary>Returns true while the question is running; the rest of the flow is paused
    /// then.</summary>
    private bool fUpdateYesNoPrompt()
    {
        if (!mbAskingYesNo)
            return false;

        Keyboard lOKeyboard = Keyboard.current;

        if (lOKeyboard == null)
            return true;

        if (lOKeyboard.yKey.wasPressedThisFrame || lOKeyboard.jKey.wasPressedThisFrame)
            fShowYesNoAnswer(true);
        else if (lOKeyboard.nKey.wasPressedThisFrame)
            fShowYesNoAnswer(false);

        bool lbYes = false;
        bool lbNo = lOKeyboard.escapeKey.wasPressedThisFrame;

        // THE GAMEPAD in the classic scheme (per user, 2026-10-07): A yes, B no - the pad's pointer
        // stands aside meanwhile (UWGamepadPointer). The modern box is clicked with the pointer.
        Gamepad lOPad = Gamepad.current;

        if (lOPad != null && Time.frameCount != miYesNoAskedFrame && !UWControlScheme.IsModern)
        {
            lbYes |= lOPad.buttonSouth.wasPressedThisFrame;
            lbNo |= lOPad.buttonEast.wasPressedThisFrame;
        }

        if (lOKeyboard.enterKey.wasPressedThisFrame || lOKeyboard.numpadEnterKey.wasPressedThisFrame)
        {
            lbYes = mbYesNoAnswer;
            lbNo = !mbYesNoAnswer;
        }

        // THE MODERN SCHEME answers with the mouse in its own box (UWModernQuestion, per user
        // 2026-10-04: OK and Cancel, every question there alike; a right click both answered and
        // switched the pointer).
        if (Mouse.current != null && Time.frameCount != miYesNoAskedFrame && !UWControlScheme.IsModern)
        {
            lbYes |= UWMouseButtons.RightPressed;
            lbNo |= UWMouseButtons.LeftPressed;
        }

        if (!lbYes && !lbNo)
            return true;

        if (lbYes && lbNo)
            lbYes = false;

        fFinishYesNo(lbYes);

        return true;
    }

    /// <summary>For the modern scheme's question box (UWModernQuestion): a question is open, and
    /// its text.</summary>
    public bool IsAskingYesNo => mbAskingYesNo;

    public string YesNoQuestion => msYesNoQuestion;

    /// <summary>The modern question box's OK (true) or Cancel (false).</summary>
    public void AnswerYesNo(bool pbYes)
    {
        if (mbAskingYesNo)
            fFinishYesNo(pbYes);
    }

    private void fFinishYesNo(bool lbYes)
    {
        mbAskingYesNo = false;

        if (mbYesNoHidCursor)
            Cursor.visible = mbYesNoCursorWasVisible;

        // The answer stays in the log: a cancel shows "No".
        SetPromptMessage(msYesNoQuestion + (lbYes ? "Yes" : "No"));
        EndPromptMessage();

        System.Action<bool> lOAnswered = mOYesNoAnswered;
        mOYesNoAnswered = null;

        if (lOAnswered != null)
            lOAnswered(lbYes);
    }

    private void fShowYesNoAnswer(bool pbYes)
    {
        mbYesNoAnswer = pbYes;
        SetPromptMessage(msYesNoQuestion + (pbYes ? "Yes" : "No"));
    }

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
        if (pOItem == null || mOUWDataImporter == null || mbAskingYesNo)
            return;

        string lsQuestion;

        if (!UWRepairRules.TryGetQuestion(pOItem, mOUWDataImporter,
            mCharacter != null ? mCharacter.Vitals.GetSkill(UWPlayerData.Skill.Repair) : 0, out lsQuestion))
        {
            AddGeneralMessage(UWRepairRules.CannotRepairMessage);

            return;
        }

        // The question works like every yes/no question - see fAskYesNo.
        fAskYesNo(lsQuestion, pbYes =>
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
    /// Is a trap noticed when looking?
    ///
    /// THE CHECK IS SEARCH against eight (reference: trapdisarming.DetectTrapTrigger and
    /// DoTrapSkillCheck). If you spot it, the original asks whether you want to disarm it
    /// - with a pre-typed "Yes".
    ///
    /// Detection happens ANEW EACH TIME. Whoever notices nothing can look again; there is no
    /// memory list.
    ///
    /// ONLY WITH THE LOOK ICON LIT (Look_seg024_24DC_D20 calls SearchForTrap_ovr143_1829 only
    /// while the mode word dseg 268C is 3; the plain right-click look, mode 0, looks and then
    /// may pick up instead). Per user, 2026-09-30: the poisoned potion on level 2, 14/51, asked
    /// the trap question now and then in ours, never in 20 right-click looks in the original.
    ///
    /// THE MODERN SCHEME has no look icon: its look key (Q) and the context menu's Look do
    /// nothing but look, the deliberate look of the icon, so they search as it does (per user,
    /// 2026-10-13: the question never came there).
    /// </summary>
    private void fTryDetectTrap(UWEntityInfo pOTarget)
    {
        if (pOTarget == null || pOTarget.ObjectData == null || mbAskingYesNo
            || (CommandMode != UWCommandMode.Look && !UWControlScheme.IsModern)
            || mLevelLoader == null || mLevelLoader.CurrentLevel == null)
            return;

        if (!UWTrapDisarming.Detect(pOTarget.ObjectData, mLevelLoader.CurrentLevel.Masterlist,
            mCharacter != null ? mCharacter.Vitals.GetSkill(UWPlayerData.Skill.Search) : 0))
            return;

        // Asked like every yes/no question (per user, 2026-09-30) - see fAskYesNo.
        fAskYesNo(GetGeneralMessage(UWTrapDisarming.TrapFoundMessage) + " ", pbYes =>
        {
            if (pbYes)
                fDisarmTrap(pOTarget);
        });
    }

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

    /// <summary>Line in the message box that keeps changing - for the quantity question
    /// when dragging. -1 means: none at the moment.</summary>
    private int miPromptLine = -1;

    /// <summary>
    /// What is still pending of a long text.
    ///
    /// The original shows a text that does not fit into the box at once in steps
    /// and waits for a confirmation in between (per user, 2026-09-03).
    /// </summary>
    private readonly List<string> mOPendingPageLines = new List<string>();

    public bool IsWaitingForPage
    {
        get { return mOPendingPageLines.Count > 0; }
    }

    /// <summary>The frame in which the page-forward input was swallowed. UWItemDrag
    /// queries this because the order of the Update calls is not fixed: otherwise
    /// the same click that confirms the last page would immediately start a drag.</summary>
    private int miPageInputFrame = -1;

    public bool PageInputConsumedThisFrame
    {
        get { return miPageInputFrame == Time.frameCount; }
    }

    /// <summary>
    /// Shows a text page by page, BELOW what the box already holds: the box is not cleared
    /// (per user on the original, 2026-09-25, reading a scroll - ours cleared it until then),
    /// the old lines count as printed and scroll up as the text comes in.
    ///
    /// psLeading are lines that come BEFORE the text and count as already printed - they
    /// do not count as a pending page. With a short text they therefore stay, with
    /// a long one the text pushes them out right away, without a confirmation being
    /// needed (observed by user on the original with throttled CPU speed,
    /// 2026-09-03).
    /// </summary>
    public void ShowPagedText(string psText, params string[] psLeading)
    {
        mOPendingPageLines.Clear();
        mOPrintedPageLines.Clear();

        if (miPromptLine >= 0 && miPromptLine < mOActiveStrings.Count)
            mOActiveStrings.RemoveAt(miPromptLine);

        mOPrintedPageLines.AddRange(mOActiveStrings);
        miPromptLine = -1;

        if (mGameUi == null || string.IsNullOrEmpty(psText))
            return;

        if (psLeading != null)
        {
            foreach (string lsLeading in psLeading)
            {
                if (!string.IsNullOrEmpty(lsLeading))
                    mOPrintedPageLines.AddRange(mGameUi.WrapLogText(UWFontRenderer.CleanText(lsLeading)));
            }
        }

        mOPendingPageLines.AddRange(mGameUi.WrapLogText(UWFontRenderer.CleanText(psText)));

        fShowNextPage();
    }

    /// <summary>
    /// Prints the next page.
    ///
    /// The log SCROLLS in the process, it is not cleared: per page up to four new
    /// lines are added, and the last ones are always shown. If more is pending, the
    /// fifth line stays free for the [MORE] hint, so only four are visible.
    ///
    /// Until 2026-09-03 the box was cleared instead and only the rest shown - this made
    /// the text slide two lines too far up. In the original, after confirming, the
    /// third line is at the very top again (measured by user on the Lakshi letter).
    /// </summary>
    private void fShowNextPage()
    {
        int liCapacity = mGameUi != null ? mGameUi.LogLineCount : 5;
        int liPerPage = Mathf.Max(1, liCapacity - 1);

        for (int liAt = 0; liAt < liPerPage && mOPendingPageLines.Count > 0; liAt++)
        {
            mOPrintedPageLines.Add(mOPendingPageLines[0]);
            mOPendingPageLines.RemoveAt(0);
        }

        // Paging moves the message scroll too (UW.EXE seg043_37F0_8EB).
        if (mOPrintedPageLines.Count > liCapacity)
            UWHudDragons.Request(UWHudDragons.ScrollAnimation);

        int liShow = IsWaitingForPage ? liCapacity - 1 : liCapacity;

        mOActiveStrings.Clear();

        for (int liAt = Mathf.Max(0, mOPrintedPageLines.Count - liShow);
            liAt < mOPrintedPageLines.Count; liAt++)
        {
            mOActiveStrings.Add(mOPrintedPageLines[liAt]);
        }

        fUpdatePagedTimeScale();
    }

    /// <summary>What has already been printed of this text - the scroll from which the box
    /// shows its last lines.</summary>
    private readonly List<string> mOPrintedPageLines = new List<string>();

    /// <summary>
    /// While the game waits for paging, time stands still: in the original
    /// the water animation stops and the creatures do not move (per user,
    /// 2026-09-03).
    ///
    /// A named hold on UWGameClock, so that another pause is not overwritten.
    /// </summary>
    private void fUpdatePagedTimeScale()
    {
        bool lbWaiting = IsWaitingForPage;

        if (lbWaiting == mbPagingHoldsTime)
            return;

        mbPagingHoldsTime = lbWaiting;

        if (lbWaiting)
            UWGameClock.Hold(UWGameClock.PagingHold);
        else
            UWGameClock.Release(UWGameClock.PagingHold);
    }

    private bool mbPagingHoldsTime;

    /// <summary>While pages are still pending, nothing happens in the world - every key and every
    /// click only pages forward.</summary>
    private bool fUpdatePagedText()
    {
        if (!IsWaitingForPage)
            return false;

        Mouse lOMouse = Mouse.current;
        Keyboard lOKeyboard = Keyboard.current;

        bool lbConfirm = (lOMouse != null
                && (UWMouseButtons.LeftPressed || UWMouseButtons.RightPressed))
            || (lOKeyboard != null && lOKeyboard.anyKey.wasPressedThisFrame);

        // Any gamepad button as well (UWGamepad).
        if (!lbConfirm && UWGamepad.AnyPressed(false))
        {
            UWGamepad.ConsumePress();
            lbConfirm = true;
        }

        if (lbConfirm)
        {
            miPageInputFrame = Time.frameCount;
            fShowNextPage();
        }

        return true;
    }

    /// <summary>
    /// The line before the text: "You read the " + the item's name + "...", for books and
    /// scrolls alike (UseReadable_seg040_352B_19A8 and Read_ovr122_586, which put the item name
    /// between two strings of UW.EXE; "UNNAMED" when it has none). CONFIRMED per user on the
    /// original 2026-09-25: a right click gives "You see a ragged scroll.", "You read the
    /// scroll...", then the text; a left click the last two only. Until then ours said "The
    /// scroll reads:" and "The book reads:" - lines that exist nowhere in UW1.
    /// </summary>
    private string fReadHeader(UWObject pOItem)
    {
        string lsName = UWText.ObjectName(mOUWDataImporter, pOItem.ID);

        return ReadPrefix + (string.IsNullOrEmpty(lsName) ? UnnamedItem : lsName) + ReadSuffix;
    }

    private const string ReadPrefix = "You read the ";

    private const string ReadSuffix = "...";

    private const string UnnamedItem = "UNNAMED";

    /// <summary>
    /// Reads a book or scroll. Returns false if the object is neither or
    /// has no text - then the caller should carry on as before.
    ///
    /// Without text are the picture parchments and the map. In the original they have their own
    /// display, which we do not have yet.
    ///
    /// Three parts are printed: the look message, the reading hint and the text. With
    /// short texts all three are in the box at once, with long ones the first
    /// two scroll out (per user on the original).
    /// </summary>
    public bool TryReadBook(UWObject pOItem, bool pbLooking)
    {
        if (pOItem == null || mOUWDataImporter == null
            || pOItem.GetCategory() != UWObject.ObjectCategoryEnum.BooksAndScrolls)
            return false;

        // An ENCHANTED scroll is a spell scroll and has no text: its quantity field
        // carries the spell, not a string pointer. In the original it only shows the
        // look message "You see a scroll of ..." (per user, 2026-09-03).
        //
        // Read from the user's save game: the four spell scrolls all carry the bit,
        // the pure text scrolls none. Without this check one of them produced a
        // plausible-looking but wrong text.
        if (pOItem.IsEnchanted)
            return false;

        string lsLook = string.Empty;

        try
        {
            lsLook = DescribeInventoryItem(pOItem);
        }
        catch
        {
            // A missing string entry is not an error - then the line is dropped.
        }

        // THE RECIPE is not read but used: with a bowl of the right things it becomes the
        // rotworm stew (UWStewRules, per user 2026-09-28). Only USING it mixes - a right click
        // reads its text as with every scroll (per user on the original, the same day).
        if (!pbLooking && UWStewRules.IsRecipe(pOItem))
        {
            int liLine = UWStewRules.TryMix(mInventory != null ? mInventory.Model : null, mOUWDataImporter);

            AddGeneralMessage(liLine);

            return true;
        }

        // The map has its own sentence and NO reading hint before it (per user on the
        // original: only the look message and "Enscribed upon the scroll is your map.").
        // Its entry in block 3 would be "The pages are blank." and thus wrong.
        if (pOItem.ID == UWObjectMechanics.MapObjectId)
        {
            ShowPagedText(mOUWDataImporter.GetGeneralMessage(UWObjectMechanics.MapMessageIndex),
                pbLooking ? lsLook : null);

            return true;
        }

        // THE MAP PIECE plays its cutscene instead of letters, a plan in the view window as a
        // window shows (per user with a screenshot from the original, 2026-09-03): the flag
        // decides, not an empty text (UWObjectMechanics.GetBookCutscene, 2026-10-15).
        //
        // The look message belongs ONLY to the right click. On left click the
        // original only shows the picture and writes nothing to the log.
        int liCutscene = UWObjectMechanics.GetBookCutscene(pOItem);

        if (liCutscene >= 0)
        {
            if (mGameUi == null || !mGameUi.ShowScrollPicture(liCutscene))
                return false;

            if (pbLooking && !string.IsNullOrEmpty(lsLook))
                AddMessage(lsLook);

            return true;
        }

        string lsText = mOUWDataImporter.GetBookText(UWObjectMechanics.GetBookStringIndex(pOItem));

        // A TEXT NUMBER FROM 0x100 ON (the recipe, looked at) stands alone, without "You read
        // the ..." (BookLook, obj/LOOK.C, 2026-10-15; until then ours put the header before it).
        string lsPrefix = UWObjectMechanics.ReadsWithHeader(pOItem) ? fReadHeader(pOItem) : null;

        // An empty text still gets its header, as UseBook prints it (none such is known).
        if (string.IsNullOrEmpty(lsText) || string.IsNullOrEmpty(lsText.Trim()))
        {
            if (pbLooking && !string.IsNullOrEmpty(lsLook))
                AddMessage(lsLook);

            if (!string.IsNullOrEmpty(lsPrefix))
                AddMessage(lsPrefix);

            return true;
        }

        // The look message belongs only to the right click. The reading hint and the text
        // appear with both buttons (per user, 2026-09-03).
        if (pbLooking)
            ShowPagedText(lsText, lsLook, lsPrefix);
        else
            ShowPagedText(lsText, lsPrefix);

        return true;
    }

    /// <summary>Whether the running question shows a blinking text cursor. A yes/no question
    /// does not (per user from the original, 2026-09-17): its answer is chosen with Y and N,
    /// not typed.</summary>
    public bool ShowsPromptTextCursor
    {
        get { return IsPromptActive && !mbAskingYesNo; }
    }

    public bool IsPromptActive
    {
        get { return miPromptLine >= 0; }
    }

    /// <summary>Sets the running line, or creates it. Unlike AddMessage this does not stack
    /// but replaces - otherwise the box would be full after typing three times.</summary>
    public void SetPromptMessage(string psMessage)
    {
        if (miPromptLine >= 0 && miPromptLine < mOActiveStrings.Count)
        {
            mOActiveStrings[miPromptLine] = psMessage;
            return;
        }

        mOActiveStrings.Add(psMessage);
        miPromptLine = mOActiveStrings.Count - 1;
    }

    /// <summary>Ends the running line. The text stays, it is just no longer
    /// replaced from now on.</summary>
    public void EndPromptMessage()
    {
        miPromptLine = -1;
    }

    public void AddMessage(string psMessage)
    {
        if (!string.IsNullOrEmpty(psMessage))
            mOActiveStrings.Add(psMessage);
    }

    /// <summary>
    /// Wipes the message box and starts with a blank line.
    ///
    /// That is what the original does before the compass text (uimanager_compass: first scroll.Clear,
    /// then a line break). The point is that nothing from before remains - the
    /// user noticed it at the same place (2026-09-07).
    ///
    /// The blank line is a SPACE, not an empty string: AddMessage deliberately drops empty
    /// messages, and the display would otherwise have nothing to draw.
    /// </summary>
    public void ClearMessages()
    {
        mOActiveStrings.Clear();
        mOActiveStrings.Add(" ");
    }

    /// <summary>Wipes the box WITHOUT the blank line: the original's save game messages
    /// ("Restoring Game ...", the name prompt, "Save Game Succeeded.") appear in the
    /// top line (per user, 2026-09-12).</summary>
    public void ResetMessages()
    {
        mOActiveStrings.Clear();
        miPromptLine = -1;
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

    // ------------------------------------------------- Spell projectiles

    /// <summary>
    /// A cast projectile spell waits for the click that gives the direction.
    ///
    /// The original sets its own mouse cursor for this and only releases the projectile on the
    /// next click. For us the number of the projectile object and its damage are kept
    /// ready until the click.
    /// </summary>
    private int miPendingProjectileId = -1;

    /// <summary>Its base damage, rolled at the hit (cleanup step 4.9b, 2026-10-18).</summary>
    private int miPendingProjectileBaseDamage;

    /// <summary>The ammunition table's speed byte of the waiting projectile (RangedAmmoType).</summary>
    private int miPendingProjectileSpeedByte;

    /// <summary>The mana a rune-cast projectile costs, paid only when it is released - the
    /// original keeps it in SpellManaCost and subtracts it after PrepareProjectileObject
    /// succeeded (Class5ProjectileSpells_seg038_4EE); when there is no room nothing is paid.
    /// Zero for a wand or a trap.</summary>
    private int miPendingManaCost;

    /// <summary>A class 7 spell waits for the object that gets clicked.
    /// -1 means none.</summary>
    private int miPendingTargetSpell = -1;

    /// <summary>Which major class the waiting target spell belongs to - 7 or 11, see
    /// fApplyTargetSpell.</summary>
    private int miPendingTargetSpellMajor = -1;

    /// <summary>Is a spell currently running and waiting for its target? This applies to both
    /// kinds: a projectile waits for the DIRECTION, a class 7 spell for a
    /// clicked OBJECT.</summary>
    public bool IsSpellTargeting => miPendingProjectileId >= 0 || miPendingTargetSpell >= 0;

    /// <summary>Is a spell waiting for an object instead of a direction? The cursor is
    /// a different one then - see UWGameUI.</summary>
    public bool IsTargetSpellPending => miPendingTargetSpell >= 0;

    /// <summary>Message when the projectile has nowhere to go. String block 1.</summary>
    private const int NoRoomForSpellMessage = 256;

    /// <summary>Sends a waiting projectile spell off immediately, in the direction the
    /// pointer is currently pointing. For wands: the click that applied the wand
    /// already gives the direction.</summary>
    public void FirePendingSpell()
    {
        if (!IsSpellTargeting)
            return;

        fLaunchSpellProjectile();

        miPendingProjectileId = -1;
    }

    /// <summary>
    /// Sends the queued projectile off from a trap's spot - the a_spelltrap, which has no click
    /// that could give it the direction (see UWTriggerSystem.FireSpellTrap). spell_fire for an
    /// object caster (UnderworldExhumed MISSILE.C): from the map square the trigger names, at
    /// the trap's fine position and height, with missile_arc and missile_try 0 - flat, along
    /// the heading given (OBJ_HEADING(trap) &lt;&lt; 5); a launcher of height 0, so no offset
    /// and no placement test. Until 2026-10-12 the caller aimed it at the camera.
    /// </summary>
    public bool LaunchPendingSpellFromSpot(int piTileX, int piTileY, int piXPos, int piYPos, int piZPos, int piHeading)
    {
        if (miPendingProjectileId < 0 || mLevelLoader == null)
            return false;

        int liProjectile = miPendingProjectileId;

        miPendingProjectileId = -1;

        return UWMissiles.Ensure(mLevelLoader).LaunchFromSpot(liProjectile, piTileX, piTileY, piXPos, piYPos,
            piZPos, piHeading, 0, miPendingProjectileSpeedByte, miPendingProjectileBaseDamage,
            UWRunicMagic.GetProjectileDamageType(liProjectile, fObjectProperties), UWRunicMagic.GetProjectileImpactId(liProjectile)) != null;
    }

    /// <summary>The OBJECTS.DAT tables, for a spell projectile's damage type.</summary>
    private UWObjectProperties fObjectProperties => mOUWDataImporter != null ? mOUWDataImporter.ObjectProperties : null;

    /// <summary>The modern scheme's aim after a cast from runes or an item (UWHudRunes.fAfterModernCast,
    /// UWCarriedItemUse): a projectile with the pointer locked flies at once towards the
    /// crosshair; free, or a target spell, waits for the left button.</summary>
    public void AfterModernCast()
    {
        if (!UWControlScheme.IsModern || !IsSpellTargeting)
            return;

        if (IsTargetSpellPending || fIsPointerFree())
            AddMessage("Choose the target with the left button.");
        else
            FirePendingSpell();
    }

    /// <summary>Escape in the modern scheme: a waiting spell is let go (a projectile's mana was not
    /// paid yet, a target spell's was).</summary>
    public void CancelPendingSpell()
    {
        miPendingProjectileId = -1;
        miPendingManaCost = 0;
        miPendingTargetSpell = -1;
        miPendingTargetSpellMajor = -1;
    }

    /// <summary>Discards a queued projectile without throwing it - for a spell trap
    /// whose projectile could not take off. Otherwise the player would be left with it as an aiming cursor.
    /// </summary>
    public void CancelPendingProjectile()
    {
        miPendingProjectileId = -1;
    }

    /// <summary>Starts a conversation with an NPC the player did not click on -
    /// for the a_do trap 50, which knows its conversation partner via its object slot
    /// (see UWTriggerSystem.fFireConversationTrap), a critter that comes to talk and
    /// Garamon's burial. TalkTo's refusals apply as to a click (fRefusesTalk).</summary>
    public bool TryTalkToNpcData(UWObject pONpcData)
    {
        UWNpc lONpc = pONpcData as UWNpc;

        if (mConversationScreen == null || UWConversationScreen.IsAnyOpen)
            return false;

        int liSlot = GetConversationSlot(lONpc);

        if (fRefusesTalk(lONpc, liSlot) || liSlot <= 0)
            return false;

        return mConversationScreen.Begin(mOUWDataImporter, lONpc, liSlot);
    }

    /// <summary>Starts a conversation without a counterpart in the world - for the talking door
    /// (a_do trap 42), whose conversation slot is hard-wired and belongs to no
    /// NPC.</summary>
    public bool BeginConversation(int piSlot)
    {
        return mConversationScreen != null && mOUWDataImporter != null
            && !UWConversationScreen.IsAnyOpen
            && mConversationScreen.Begin(mOUWDataImporter, null, piSlot);
    }

    /// <summary>
    /// Remembers the spell until the player clicks the direction.
    ///
    /// THE TABLE VALUE IS THE BASE VALUE FOR THE ROLL, not the damage - for the fireball
    /// twenty-eight, so fifteen on average. The reference sends a projectile hit
    /// through the same calculation as a sword swing (combat.MissileImpact ->
    /// AttackerAppliesFinalDamage), and that starts with the roll. For creature
    /// projectiles this has been in place since 2026-09-10, here is the other half.
    ///
    /// WITH NEUTRAL CHARGE: for a creature it is its strike charge, for the player
    /// the reference takes the value of his power gem at this point - but that belongs to
    /// melee and, when casting, holds whatever the last swing left behind. That
    /// is evidently arbitrary; we calculate with the plain value.
    ///
    /// THE ROLL AT THE HIT since cleanup step 4.9b (2026-10-18, by the user's decision to follow
    /// the original: missile_thwack -> do_damage rolls it, UWAttackResolution.Missile, with charge
    /// 0x80): the table value is kept as the flight's base; it was rolled here until then.
    /// </summary>
    public void BeginSpellTargeting(int piProjectileId, int piDamage, int piSpeed, int piManaCost = 0)
    {
        miPendingProjectileId = piProjectileId;
        miPendingManaCost = piManaCost;
        miPendingProjectileBaseDamage = piDamage;
        miPendingProjectileSpeedByte = piSpeed;
    }

    /// <summary>Remembers a spell until the player clicks its target. This exists in
    /// two major classes: 7 targets creatures (see UWTargetSpell), 11 items
    /// and doors (see UWMiscSpell).</summary>
    public void BeginTargetSpell(int piMajorClass, int piMinorClass)
    {
        miPendingTargetSpellMajor = piMajorClass;
        miPendingTargetSpell = piMinorClass;
    }

    /// <summary>Message when the spell hits nothing: string block 1, our 272, "The spell has no
    /// discernable effect." Until 2026-09-14 this was 143, which is "You cannot repair that."
    /// (UW.EXE ItemRepair_ovr107_1754 prints it as 0x8E); a "Nothing happens." does not exist in
    /// block 1. 272 is what UW.EXE DoorAndTrapSpells_seg038_3307_1560 prints (0x10F) when a
    /// class 11 target spell does not take effect.</summary>
    private const int NothingHappensMessage = UWStringIds.SpellNoEffectMessage;

    /// <summary>
    /// The waiting target spell onto what lies under a screen point - the pointer, or in the
    /// modern scheme the crosshair while the pointer is locked.
    ///
    /// AS THE ORIGINAL'S TARGET MODE (mous_in_3d, GIM_TARGET, UnderworldExhumed INTERACT.C, read
    /// 2026-10-12): a click on no object does nothing and the spell keeps waiting; an object out
    /// of reach - the use reach, InPickRange with PickDist, which Telekinesis lifts - gives "You
    /// cannot reach that." and the spell keeps waiting; only an object in reach takes the spell
    /// (obj_spells), which then prints its own outcome. Until 2026-10-12 every click used the
    /// spell up, from as far as one could look, with "The spell has no discernable effect." for
    /// a miss. The original's BlockingTerrain test is not built here, as on the use path.
    /// </summary>
    private void fApplyTargetSpell(Vector2 pOAt)
    {
        int liSpell = miPendingTargetSpell;
        int liMajor = miPendingTargetSpellMajor;

        // The pickable thing first - behind a blood pool if need be -, else whatever the look
        // hits: a creature, a door (UWWorldPicker.Measurement.SpellTarget).
        UWEntityInfo lOTarget = Camera.main != null
            ? UWWorldPicker.Measure(Camera.main.ScreenPointToRay(pOAt)).SpellTarget
            : null;

        if (lOTarget == null)
            return;

        if (!fCanReach(lOTarget, UWReachRules.GetDistanceSquared(false, fHasTelekinesis), false))
        {
            AddGeneralMessage(UWMiscSpellRules.TargetOutOfReachMessage);

            return;
        }

        miPendingTargetSpell = -1;
        miPendingTargetSpellMajor = -1;

        if (liMajor == UWMiscSpell.MajorClass)
            UWMiscSpell.CastOnTarget(liSpell, lOTarget, this, mLevelLoader);
        else if (UWTargetSpell.Cast(liSpell, lOTarget) == UWTargetSpellRules.Result.Missed)
            AddGeneralMessage(NothingHappensMessage);
    }

    /// <summary>The look sentence for a thing in the world, without the look's triggers and
    /// trap search - for Name Enchantment on something that cannot be identified (see
    /// UWMiscSpell).</summary>
    public string GetLookDescription(UWEntityInfo pOTarget)
    {
        return pOTarget != null ? fGetLookDescription(pOTarget) : null;
    }

    /// <summary>Is a TARGET spell waiting for its target? A projectile spell does not count - it
    /// flies into the world and has no business in the inventory.</summary>
    public bool IsItemTargetSpellPending => miPendingTargetSpell >= 0;

    /// <summary>
    /// Fires a waiting target spell on an item in the inventory.
    ///
    /// A TARGET SPELL ALSO WORKS ON THE BACKPACK (per user, 2026-09-10): Name Enchantment
    /// identifies an item you carry - you do not have to drop it first.
    ///
    /// THE SPELL IS GONE AFTERWARDS IN ANY CASE, even if the click hit nothing valid
    /// - that is how the original behaves with both mouse buttons (per user,
    /// 2026-09-10). Only in the view window does it keep waiting, because there the left button
    /// drives movement.
    ///
    /// IF IT MISSES, NOTHING IS SHOWN - no "The spell has no discernable effect." as when missing in
    /// the world (per user, 2026-09-10). What you read on a hit is the
    /// description the spell itself outputs (see UWLoreCheck and IdentifyItem) -
    /// with both buttons, including the left one, which otherwise does not look in the inventory.
    /// ONCE, not twice: the normal look is not added on top (checked in the original,
    /// per user, 2026-09-10).
    ///
    /// This is called from UWItemDrag, not from here: there the click afterwards
    /// still has its usual effect, and the original does both - the container closes
    /// AND the spell is used up.
    /// </summary>
    public void ResolveTargetSpellOnItem(UWObject pOItem)
    {
        if (miPendingTargetSpell < 0)
            return;

        int liSpell = miPendingTargetSpell;
        int liMajor = miPendingTargetSpellMajor;

        miPendingTargetSpell = -1;
        miPendingTargetSpellMajor = -1;

        if (pOItem == null || liMajor != UWMiscSpell.MajorClass)
            return;

        UWMiscSpell.CastOnItem(liSpell, pOItem, this);
    }

    /// <summary>Intercepts the click that gives the direction. Returns true as long as the
    /// spell claims the click for itself.</summary>
    private bool fUpdateSpellTargeting()
    {
        if (!IsSpellTargeting)
            return false;

        bool lbRightButton = mInput.Interact.WasPressedThisFrame();

        if (!lbRightButton && !UWMouseButtons.LeftPressed)
            return true;

        // THE MODERN SCHEME (per user, 2026-10-04): the right button switches the pointer there,
        // so the LEFT button gives the target - through the crosshair, or under the free pointer;
        // on a window of the modern UI the window has the click (a bag slot takes an item target,
        // UWModernBags). See UWHudRunes.fAfterModernCast.
        if (UWControlScheme.IsModern)
        {
            if (!UWMouseButtons.LeftPressed)
                return true;

            bool lbFree = fIsPointerFree() && Mouse.current != null;
            Vector2 lOAt = lbFree ? Mouse.current.position.ReadValue() : UWModernClassicFrame.ViewCentre;

            if (lbFree && UWModernPointer.IsOverUi(lOAt))
                return true;

            if (miPendingTargetSpell >= 0)
            {
                fApplyTargetSpell(lOAt);
                return true;
            }

            // A projectile flies towards the click (UWHudRunes.fAfterModernCast: it waits only
            // when cast with the pointer free).
            fLaunchSpellProjectile(Camera.main != null ? Camera.main.ScreenPointToRay(lOAt) : (Ray?)null);
            miPendingProjectileId = -1;
            return true;
        }

        // Outside the view window you do not point into the world. The click on the
        // backpack is evaluated by UWItemDrag (fTryResolveTargetSpellInInventory) - there, because
        // it can also still have its usual effect there.
        if (!UWControlScheme.IsModern
            && mGameUi != null && !mGameUi.IsScreenPositionInGameArea(Mouse.current.position.ReadValue()))
            return true;

        // IN THE VIEW WINDOW ONLY THE RIGHT BUTTON AIMS. With the left one you keep walking, and
        // the waiting spell keeps waiting - this applies to target spells as well as
        // projectile spells (per user on the original, 2026-09-10).
        //
        // THIS REPLACES THE ASSUMPTION OF 2026-09-05 that the left button fires the shot
        // in addition to walking. The walking itself was already correct and stays as it
        // is (see UWPlayerInput.IsCursorMovementInProgress).
        if (!lbRightButton)
            return true;

        if (miPendingTargetSpell >= 0)
        {
            fApplyTargetSpell(Mouse.current.position.ReadValue());

            return true;
        }

        fLaunchSpellProjectile();

        miPendingProjectileId = -1;

        return true;
    }

    /// <summary>Sends the waiting projectile off - along pOAim when given (the modern scheme's
    /// click under the free pointer), else along the interaction ray. The original's aim from
    /// the pointer in the view window (InitPlayerProjectilesValues_seg025_D) in the original
    /// scheme; the start radii + 4 eighths ahead of the feet, five sixths of the height up - and
    /// no room there means no spell (per user, 2026-09-24: in a narrow corridor one has to aim
    /// nearer the middle in the original). On the motion core since 2026-10-05.</summary>
    private void fLaunchSpellProjectile(Ray? pOAim = null)
    {
        if (mLevelLoader == null || Camera.main == null)
            return;

        int liHeading;
        int liPitch;

        if (!UWControlScheme.IsModern && mGameUi != null)
        {
            UWPlayerThrow.GetPointerInView(mGameUi, Mouse.current.position.ReadValue(), out int liX, out int liY);
            UWPlayerThrow.GetMissileAim(liX, liY, Camera.main.transform, out liHeading, out liPitch);
        }
        else
        {
            Ray lORay = pOAim ?? fGetInteractionRay();

            UWMissiles.AimFromDirection(lORay.direction, miPendingProjectileSpeedByte, out liHeading, out liPitch);
        }

        UWProjectileFlight lOFlight = UWMissiles.Ensure(mLevelLoader).LaunchFromPlayer(null, miPendingProjectileId,
            liHeading, liPitch, miPendingProjectileSpeedByte, miPendingProjectileBaseDamage,
            UWRunicMagic.GetProjectileDamageType(miPendingProjectileId, fObjectProperties), UWRunicMagic.GetProjectileImpactId(miPendingProjectileId),
            this, 0x3F);

        if (lOFlight == null)
        {
            AddGeneralMessage(NoRoomForSpellMessage);

            return;
        }

        // Released: now the rune spell's mana is paid (see miPendingManaCost).
        if (miPendingManaCost > 0 && mCharacter != null)
            mCharacter.Vitals.SpendMana(miPendingManaCost);

        miPendingManaCost = 0;
    }

    /// <summary>The player's missile has hit: an object's flash, a creature's eyes at the top
    /// edge as in melee. A CREATURE'S BLOOD comes from UWCritter.ShowMissileHitEffect alone, which the
    /// missile calls first: do_damage (UnderworldExhumed COMBAT.C) puts it once, a second only for
    /// a critical blow, which a missile never is. Until 2026-10-13 ours showed two (reference
    /// audit, finding of the fix agents).</summary>
    public void ReportSpellHit(UWEntityInfo pOTarget, UWDamageable pODamageable, Vector3 pOPoint)
    {
        if (pOTarget != null && !fIsCreature(pOTarget))
            fShowHitEffect(pOTarget, pOPoint);

        // Only for a creature - see fApplyMeleeHit.
        if (mEyes != null && pODamageable != null && fIsCreature(pOTarget))
            fShowEyes(pOTarget, pODamageable);
    }

    /// <summary>The eyes for a creature the player hit: its hit points left against its table
    /// average (UWCombat.GetEyesGroup).</summary>
    private void fShowEyes(UWEntityInfo pOTarget, UWDamageable pODamageable)
    {
        UWCritter lOCritter = pOTarget != null ? pOTarget.GetComponentInParent<UWCritter>() : null;

        mEyes.ShowCondition(mOUWDataImporter, pODamageable.CurrentHealth,
            lOCritter != null ? lOCritter.TableVitality : pODamageable.MaxHealth, fFoeName(pOTarget));
    }

    /// <summary>The bare name of a creature hit, capitalised - for the modern heading strip
    /// (UWModernHud), as the crosshair names it.</summary>
    private string fFoeName(UWEntityInfo pOTarget)
    {
        if (pOTarget == null || pOTarget.ObjectData == null)
            return null;

        return UWText.Capitalised(UWText.ObjectName(mOUWDataImporter, pOTarget.ObjectData.ID, false, false, null));
    }

    /// <summary>
    /// Origin for look and use rays. With Original the mouse cursor
    /// itself points at the target, with Modern - where the cursor is invisible and locked - the
    /// screen centre acts as crosshair.
    /// </summary>
    private Ray fGetInteractionRay()
    {
        if (!UWControlScheme.IsModern)
            return Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        return UWWorldPicker.CrosshairRay();
    }

    /// <summary>
    /// Walls, floors and ceilings are merged into chunk meshes and therefore
    /// no longer have their own UWEntityInfo. Via the hit triangle index the
    /// tile can be found again and the description built from it.
    /// </summary>
    private string fDescribeChunkHit(RaycastHit pOHit)
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

    /// <summary>The first of the two wall panels, 0x16E and 0x16F.</summary>
    private const int WallPanelObjectId = 0x16E;

    /// <summary>
    /// WHAT THE PAD'S A (E) DOES with what the crosshair is on, for the button hint under it
    /// (UWModernHud, per user 2026-10-08): fModernUseOn's tap without acting - "Pick up" what can be
    /// taken, "Talk" to a creature or a thing that answers, "Use" anything else - and null where
    /// the tap does nothing (a wall, the floor, beyond the sight).
    /// </summary>
    public string GetCrosshairPadVerb()
    {
        if (mOUWDataImporter == null)
            return null;

        fAimAtCrosshair(out UWEntityInfo lOFrontmost, out string _, out UWEntityInfo lOPickable);

        UWEntityInfo lOTake = lOFrontmost != null && lOFrontmost.CanBePickedUp ? lOFrontmost : lOPickable;

        if (lOTake != null && !fIsCreature(lOTake))
            return "Pick up";

        if (fIsGeometry(lOFrontmost))
            return null;

        if (fIsCreature(lOFrontmost))
            return "Talk";

        int liObjectId = lOFrontmost.ObjectData != null ? lOFrontmost.ObjectData.ID : -1;

        return UWClickRules.TalkToThing(liObjectId, lOFrontmost.IsChainedPrincess) != UWClickRules.ThingTalkAnswer.CannotTalk
            ? "Talk" : "Use";
    }

    /// <summary>
    /// WHAT THE CROSSHAIR IS ON, for the label under it (UWModernHud): the thing's bare
    /// name out of string block 4 ("goblin", "door"), plural for a stack. Without the look's
    /// side effects (identifying, trap search, look triggers); nothing for walls, floors,
    /// wall panels, geometry and what lies beyond the sight.
    /// </summary>
    public string GetCrosshairTargetName()
    {
        if (mOUWDataImporter == null)
            return null;

        fAimAtCrosshair(out UWEntityInfo lOFrontmost, out string _, out UWEntityInfo lOPickable);

        UWEntityInfo lOTarget = fIsGeometry(lOFrontmost) ? lOPickable : lOFrontmost;

        if (fIsGeometry(lOTarget))
            return null;

        // A WALL PANEL (0x16E/0x16F, "special tmap obj") is a piece of wall - the look describes
        // its wall texture; it gets no name, as a wall gets none (per user, 2026-10-03).
        if (lOTarget.ObjectData.ID == WallPanelObjectId || lOTarget.ObjectData.ID == WallPanelObjectId + 1)
            return null;

        // Plural for a real stack only (UWStackRules.IsStack): until 2026-10-11 any quantity
        // field above 1 chose the plural, so a special property named an enchanted piece as many.
        UWObject lOObject = lOTarget.ObjectData;

        return UWText.ObjectName(mOUWDataImporter, lOObject.ID, UWStackRules.IsStack(lOObject), false, null);
    }
}
