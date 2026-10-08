using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Moves the player via Unity's built-in CharacterController.
///
/// Replaces the 2014 combination of CharacterMotorC (a generic, 650-line physics motor
/// with slope curves, moving platforms and jump physics - none of which occurs in a
/// dungeon with fixed tile heights) and FPSInputControllerC. Both ran independently of
/// each other every frame and wrote the same value (motor.inputMoveDirection); depending
/// on script execution order, one script could overwrite the other, which could make the
/// original controls unusable.
/// Here there is exactly one movement vector per frame from exactly one source.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class UWPlayerMovement : MonoBehaviour
{
    // Original and Modern feel differently fast at the same numeric value -
    // Original moves in fixed steps per cursor zone, Modern glides freely with WASD.
    // Hence separately adjustable instead of one shared pair of values.

    /// <summary>Measured on the original: ten tiles in 3.15 seconds, i.e. 203 units per
    /// second at a tile width of 64 units (per user, 2026-09-04, DOSBox Staging at a
    /// fixed 30000 cycles). Before that this was 230 - about 13 percent too fast, because
    /// it had been tuned against the original at cycles=max.</summary>
    [Header("Original scheme: speed in units per second (a tile is 64 wide)")]
    [SerializeField]
    private float mfOriginalForwardSpeed = 203f;

    [SerializeField]
    private float mfOriginalBackwardSpeed = 101.5f;

    /// <summary>Sideways is half speed, like backwards (per user on the original,
    /// 2026-09-04). Until then this held the full speed and the cursor path halved it
    /// again by itself - but the keyboard path did not. Now the value lives here and
    /// applies everywhere.</summary>
    [SerializeField]
    private float mfOriginalStrafeSpeed = 101.5f;

    [Header("Modern scheme: speed in units per second")]
    [SerializeField]
    private float mfModernForwardSpeed = 200f;

    [SerializeField]
    private float mfModernBackwardSpeed = 100f;

    [SerializeField]
    private float mfModernStrafeSpeed = 200f;

    [SerializeField]
    [Tooltip("How quickly the speed approaches the target value. High = responsive.")]
    private float mfAcceleration = 2200f;

    [Header("Original scheme: turning, degrees per second")]
    [SerializeField]
    private float mfTurnSpeed = 130f;

    [SerializeField]
    private float mfSlightTurnSpeed = 130f;

    [Header("Gravity")]
    [SerializeField]
    private float mfGravity = 800f;

    /// <summary>
    /// Jump height. The reference gives the jump a fixed initial momentum (0x263) against
    /// a fixed gravity (4); the conversion into world units is not documented there.
    /// The user remembers 48 units (2026-09-13).
    ///
    /// ACROBATICS PLAYS NO ROLE IN THE HEIGHT: in the reference's jump routine
    /// (motion_player, CalculateMotionFromCommand, commands 6 and 7) the skill does not
    /// appear, only in fall damage.
    /// </summary>
    [Header("Jumping")]
    [SerializeField]
    [Tooltip("How high a jump carries, in world units (the player is 40 tall, a step 16). 48 from the user's memory of the original.")]
    private float mfJumpHeight = 48f;

    [SerializeField]
    [Tooltip("Gravity during a jump under the Leap spell, as a factor. The reference halves it (2 instead of 4) - the jump becomes twice as high and far.")]
    private float mfLeapGravityFactor = 0.5f;

    [SerializeField]
    [Tooltip("When jumping with Shift from a standstill, the player moves forward with this share of the forward speed. The reference uses half.")]
    private float mfStandingJumpForwardFactor = 0.5f;

    /// <summary>A jump is in progress - from takeoff to landing, even if it continues
    /// down an edge afterwards.</summary>
    private bool mbJumping;

    /// <summary>The horizontal velocity of the jump in world coordinates. In the air it
    /// can no longer be changed, only the view direction.</summary>
    private Vector3 mOJumpVelocity;

    /// <summary>Time elapsed since takeoff - in the takeoff frame the controller still
    /// reports ground contact.</summary>
    private float mfJumpTime;

    /// <summary>Below this much time since takeoff, ground contact does not count as a landing.
    /// </summary>
    private const float JumpLandingGrace = 0.08f;

    /// <summary>Is the character currently jumping?</summary>
    public bool IsJumping => mbJumping;

    [SerializeField]
    private float mfGroundedStickForce = 8f;

    private CharacterController mController;
    private UWControlScheme mScheme;
    private UWGameUI mGameUi;
    private UWItemDrag mItemDrag;
    private UWControls.PlayerActions mInput;
    /// <summary>LOCAL to the player's rotation, not the world - x is sideways, z is forward.
    /// Whoever holds it against anything from the world turns it with TransformDirection
    /// first; forgetting that cost a day on the wall impact (2026-09-22).</summary>
    private Vector3 mCurrentHorizontalVelocity;
    private float mfVerticalVelocity;

    /// <summary>
    /// Original scheme: is a cursor movement in progress? UWGameUI needs this so that the
    /// brief exit from the tiny game window while dragging, typical for the original, does
    /// not immediately snap back to the centre position.
    /// </summary>
    public bool IsCursorMovementInProgress { get; private set; }

    /// <summary>Did the current left-button press begin inside the 3D viewport?</summary>
    private bool mbCursorDragStartedInViewport;

    private static UWPlayerMovement msInstance;

    private static int miJumpClickFrame = -1;

    private static bool mbJumpClickLatched;

    /// <summary>
    /// Original scheme: a right click while moving with the held left button makes the player
    /// jump, and that click does nothing else - no look, no use, no pickup, no attack (per user,
    /// 2026-09-14). True from the press until the right button is up again. Evaluated at most
    /// once per frame, whichever component asks first, so all consumers agree.
    /// </summary>
    public static bool ConsumesRightClick()
    {
        UnityEngine.InputSystem.Mouse lOMouse = UnityEngine.InputSystem.Mouse.current;

        if (lOMouse == null)
            return false;

        if (miJumpClickFrame != Time.frameCount)
        {
            miJumpClickFrame = Time.frameCount;

            // In the fight mode both buttons drive on and the right one attacks (seg034_2F89_4E,
            // UWPlayerMotion.PointerDrives) - the click is not the jump's then.
            if (UWMouseButtons.RightPressed)
                mbJumpClickLatched = msInstance != null && msInstance.fIsCursorMoving() && !fIsFightMode();
            else if (!UWMouseButtons.RightHeld && !UWMouseButtons.RightReleased)
                mbJumpClickLatched = false;
        }

        return mbJumpClickLatched;
    }

    /// <summary>The fight mode of the original scheme (the drawn weapon, PD[0x5F] bit 1).</summary>
    private static bool fIsFightMode()
    {
        Interaction lOInteraction = UWScene.Interaction;

        return lOInteraction != null && lOInteraction.IsCombatModeActive;
    }

    /// <summary>Left button held with a press that started in the viewport, original scheme.</summary>
    private bool fIsCursorMoving()
    {
        return mScheme != null && mScheme.Current == UWControlScheme.SchemeEnum.Original
            && mGameUi != null && UWMouseButtons.LeftHeld && mbCursorDragStartedInViewport;
    }

    /// <summary>
    /// Factor on the walking speed. Swimming and lava lower it, see
    /// UWPlayerTerrain - the movement itself does not need to know about it.
    /// </summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>
    /// The active motion spells as a bit field, set by UWCharacter. See the
    /// constants there.
    /// </summary>
    public int MagicalMotionAbilities { get; set; }

    /// <summary>The part of it that comes from a cast spell - see
    /// UWArmourProtection.Status.SpellMotionAbilities and fUpdateHover.</summary>
    public int SpellMotionAbilities { get; set; }

    /// <summary>What currently scales the walking speed - read-only in the Inspector. Added
    /// 2026-09-24: after Tybal's death cutscene, started while levitating, the player stayed
    /// slow until the next load, on lava or not (per user); the code shows no factor that
    /// could stick, so these show which one does.</summary>
    [Header("Read only")]
    [SerializeField]
    private float mfDebugSpeedMultiplier;

    [SerializeField]
    private float mfDebugHoverSpeedFactor;

    [SerializeField]
    private bool mbDebugHovering;

    [SerializeField]
    private bool mbDebugHoverLifting;

    [SerializeField]
    private bool mbDebugHoverFromJump;

    [SerializeField]
    private int miDebugMagicalMotionAbilities;

    [SerializeField]
    private int miDebugSpellMotionAbilities;

    [SerializeField]
    private bool mbDebugWorldInputBlocked;

    [SerializeField]
    private bool mbDebugGrounded;

    /// <summary>The speed the input asks for, before any factor - with the original scheme it
    /// depends on where the pointer stands in the view.</summary>
    [SerializeField]
    private float mfDebugInputSpeed;

    private void fUpdateDebugFields()
    {
        mfDebugSpeedMultiplier = SpeedMultiplier;
        mfDebugHoverSpeedFactor = fGetHoverSpeedFactor();
        mbDebugHovering = mbHovering;
        mbDebugHoverLifting = mbHoverLifting;
        mbDebugHoverFromJump = mbHoverFromJump;
        miDebugMagicalMotionAbilities = MagicalMotionAbilities;
        miDebugSpellMotionAbilities = SpellMotionAbilities;
        mbDebugWorldInputBlocked = mScheme != null && mScheme.IsWorldInputBlocked;
        mbDebugGrounded = mController != null && mController.isGrounded;
    }

    /// <summary>
    /// How high the feet are above the tile floor, set by UWPlayerTerrain.
    ///
    /// The initial value means "unknown": without UWPlayerTerrain in the picture, hovering
    /// should behave as before.
    /// </summary>
    public float HeightAboveFloor { get; set; } = float.MaxValue;

    /// <summary>
    /// How much floor below the feet still counts as floor while a hover spell is
    /// active.
    ///
    /// ONE STEP. The user observed on the original that the hover effect does NOT kick in
    /// at a sixteen-unit step or on descending slopes (2026-09-09) - the player walks
    /// down as usual. Sixteen is exactly one height step of the original
    /// (see UWTile.HeightLevel), and the same sixteen is the height of a slope.
    ///
    /// Without this, a single frame without ground contact was enough - and there are
    /// many of those on every staircase and every slope.
    /// </summary>
    [SerializeField]
    [Tooltip("Up to this depth below the feet, floor still counts as floor. 16 = one step.")]
    private float mfHoverGroundClearance = UWTile.HeightLevel;

    /// <summary>Maximum sink speed under Slow Fall. Halved after the user compared it
    /// against the original (2026-09-03).</summary>
    [SerializeField]
    private float mfSlowFallSpeed = 30f;

    /// <summary>
    /// Speed while levitating, as a factor on the walking speed.
    ///
    /// In the original the player barely moves - about as fast as swimming against the
    /// current (per user, 2026-09-03).
    /// </summary>
    [SerializeField]
    private float mfLevitateSpeedFactor = 0.2f;

    /// <summary>Speed while flying, as a factor on the walking speed. Confirmed on the
    /// original: it is exactly the normal walking speed (per user, 2026-09-04).</summary>
    [SerializeField]
    private float mfFlySpeedFactor = 1f;

    /// <summary>How far the height oscillates up and down while hovering (per user,
    /// estimated on the original: four up, four down, 2026-09-03).</summary>
    [SerializeField]
    private float mfHoverAmplitude = 4f;

    /// <summary>Duration of one full oscillation. Matched against the original by the user
    /// (2026-09-03): first two seconds, then 1.7 - one second fits.</summary>
    [SerializeField]
    private float mfHoverPeriodSeconds = 1f;

    /// <summary>How far the spell lifts off the ground when cast while standing. Must be
    /// larger than the oscillation, otherwise the player touches down again at the bottom
    /// (per user: at least five).</summary>
    [SerializeField]
    private float mfHoverLiftOff = 5f;

    /// <summary>How quickly Q and E adjust the hover height.</summary>
    [SerializeField]
    private float mfHoverClimbSpeed = 40f;

    /// <summary>Maximum speed at which the character follows its target height. Well above
    /// what the oscillation requires - so lift-off feels immediate.</summary>
    [SerializeField]
    private float mfHoverTrackSpeed = 300f;

    /// <summary>The height around which the oscillation happens.</summary>
    private float mfHoverBaseY;

    private float mfHoverPhase;

    private bool mbHovering;

    /// <summary>Currently lifting off the ground. Only during this does the player hover
    /// although ground below the feet is still reported.</summary>
    private bool mbHoverLifting;

    /// <summary>The hover began at the top of a jump. It then lasts until the feet touch
    /// ground again - the apex of a jump from level ground is lower than the clearance
    /// fIsAirborne asks for, so without this flag the hover would end in the next frame.</summary>
    private bool mbHoverFromJump;

    private bool mbHoverSpellWasActive;

    /// <summary>
    /// A short push from outside, in world coordinates. Decays by itself.
    ///
    /// Needed when walking into one's own spell projectile: in the original the player
    /// bounces off a little but can catch up with the projectile again (per user, 2026-09-05).
    /// Hence a push instead of a solid obstacle - on a solid body the player could get
    /// stuck in narrow corridors.
    /// </summary>
    private Vector3 mOKnockback;

    /// <summary>How quickly a push decays, in units per second squared.</summary>
    [SerializeField]
    private float mfKnockbackDamping = 400f;

    public void AddKnockback(Vector3 pOWorldVelocity)
    {
        // On the core the collision itself deflects or stops the player (CollideObjects).
        if (UsesMotionCore)
            return;

        mOKnockback += pOWorldVelocity;
    }

    /// <summary>
    /// The quake throws the player up. READ 2026-09-25 (BouncePlayer_seg008_D9E, whole): it
    /// writes intensity * 0x2F / 4 into the same vertical motion value a jump starts with
    /// 0x263, and halves both horizontal speeds. So the take-off is that fraction of a jump's
    /// take-off. No quake trap in the nine levels is reachable (the only quality 62 record is
    /// an unlinked leftover), so this only keeps the rule; until 2026-09-25 the speed was an
    /// estimate per intensity point.
    /// </summary>
    public void BounceUp(int piIntensity)
    {
        if (piIntensity <= 0)
            return;

        if (UsesMotionCore)
        {
            Motion.Bounce(piIntensity);

            return;
        }

        float lfJumpTakeOff = Mathf.Sqrt(2f * mfGravity * mfJumpHeight);
        float lfTakeOff = lfJumpTakeOff * ((piIntensity * BounceMotionPerIntensity / BounceMotionDivisor) / (float)JumpMotionValue);

        mfVerticalVelocity = Mathf.Max(mfVerticalVelocity, lfTakeOff);
        mCurrentHorizontalVelocity *= 0.5f;
        mOJumpVelocity *= 0.5f;
        mbFalling = true;
    }

    /// <summary>BouncePlayer: intensity * 0x2F / 4 (integer), against the jump's 0x263.</summary>
    private const int BounceMotionPerIntensity = 0x2F;

    private const int BounceMotionDivisor = 4;

    private const int JumpMotionValue = 0x263;

    /// <summary>In liquid the fall does not apply - swimming stays controllable.</summary>
    public bool IsInLiquid { get; set; }

    /// <summary>Standing on lava. Lava counts as liquid for falling, but jumping stays
    /// possible there - the original lets the player jump onto and off lava (per user,
    /// 2026-09-14).</summary>
    public bool IsOnLava { get; set; }

    /// <summary>Is the character currently moving horizontally? For creature
    /// perception: whoever stands still makes no noise (see UWCritter).</summary>
    public bool IsMoving => UsesMotionCore ? Motion.Params.Speed > 0 : mCurrentHorizontalVelocity.sqrMagnitude > 1f;

    /// <summary>
    /// How fast the character REALLY moved over the ground in the last frame - measured from
    /// the position, not the wanted velocity, so that walking into a wall counts as standing.
    /// For the head bob and the weapon's jitter (UWHeadBobRules). The original uses its motion
    /// speed there, which it does not clear against a north or east wall - a slip of its own we
    /// do not copy (per user, 2026-09-23).
    /// </summary>
    public float ActualHorizontalSpeed { get; private set; }

    /// <summary>
    /// ActualHorizontalSpeed smoothed over about a tenth of a second - what the head bob and the
    /// weapon read. Measured from frame to frame the speed wobbles with the physics steps, and
    /// the bob's factor wobbled with it; the user saw the view grow shakier than with the
    /// wanted velocity (2026-09-23). The original's motion speed changes smoothly.
    /// </summary>
    public float SmoothedHorizontalSpeed { get; private set; }

    /// <summary>The time constant of that smoothing, in seconds.</summary>
    private const float SpeedSmoothingSeconds = 0.1f;

    private Vector3 mOLastFramePosition;

    private bool mbHasLastFramePosition;

    /// <summary>The full forward speed of the current scheme with the load and terrain factor
    /// - what the head bob measures the current speed against.</summary>
    public float FullForwardSpeed => UsesMotionCore
        ? NormalForwardSpeed * Motion.ForwardSpeed / UWPlayerMotion.BaseForwardSpeed
        : NormalForwardSpeed * Mathf.Max(0f, SpeedMultiplier);

    /// <summary>The forward speed of the current scheme on normal ground - the original's base
    /// speed of 0x3AC, which the weapon jitter's thresholds are counted against.</summary>
    public float NormalForwardSpeed =>
        mScheme != null && mScheme.Current == UWControlScheme.SchemeEnum.Modern ? mfModernForwardSpeed : mfOriginalForwardSpeed;

    /// <summary>Measures ActualHorizontalSpeed after the frame's move. A jump in position
    /// (teleport, level change) reads as one fast frame; the readers clamp it.</summary>
    private void LateUpdate()
    {
        Vector3 lOPosition = transform.position;

        if (mbHasLastFramePosition && Time.deltaTime > 0f)
        {
            Vector3 lOMoved = lOPosition - mOLastFramePosition;
            lOMoved.y = 0f;

            ActualHorizontalSpeed = lOMoved.magnitude / Time.deltaTime;
        }
        else if (Time.deltaTime > 0f)
            ActualHorizontalSpeed = 0f;

        mOLastFramePosition = lOPosition;
        mbHasLastFramePosition = true;

        if (Time.deltaTime > 0f)
        {
            // A jump in position (teleport, level change) must not kick the smoothed value.
            float lfRaw = Mathf.Min(ActualHorizontalSpeed, NormalForwardSpeed * 2f);

            SmoothedHorizontalSpeed = Mathf.Lerp(SmoothedHorizontalSpeed, lfRaw,
                1f - Mathf.Exp(-Time.deltaTime / SpeedSmoothingSeconds));
        }
    }

    /// <summary>On the ground and not hovering or swimming - the head bob runs only then, as
    /// the original skips it while the player is off the ground. LAVA IS GROUND: one walks on
    /// it, and the original's bob (PlayerMotion_seg034_2F89_604) asks only for the off-ground
    /// bit; until 2026-10-02 every liquid tile stopped the bob and the weapon's jitter (per
    /// user: "on lava the head bob is missing").</summary>
    public bool IsWalkingOnGround => fIsOnGround() && !fIsHovering()
        && (!IsInLiquid || IsOnLava);

    /// <summary>
    /// How long the character may be without ground before counting as falling.
    ///
    /// CharacterController.isGrounded drops out for single frames during normal walking.
    /// Without this grace period, Slow Fall already removed the momentum then (per user,
    /// 2026-09-03).
    /// </summary>
    [SerializeField]
    private float mfFallGraceSeconds = 0.15f;

    /// <summary>The direction in which the fall began, in world coordinates.</summary>
    private Vector3 mOFallVelocity;

    private bool mbFalling;

    /// <summary>The downward speed of the touchdown, kept from fGetVerticalVelocity because the
    /// stick force wipes it before fApplyFall reports the landing.</summary>
    private float mfImpactSpeed;

    private float mfAirborneTime;

    /// <summary>
    /// Direction of the current in world coordinates, or zero vector if none applies.
    /// Set by UWPlayerTerrain.
    /// </summary>
    public Vector3 FlowDirection { get; set; }

    /// <summary>How strongly the current changes the speed. 0.5 means: one and a half
    /// times as fast with the current, half as fast against it.</summary>
    public float FlowStrength { get; set; }

    /// <summary>
    /// After a teleport (level change, start) no residual vertical velocity from the old
    /// position may remain - otherwise the player briefly falls harder than the new
    /// floor height warrants.
    ///
    /// However, CharacterController.isGrounded only reflects the real ground contact at
    /// the new position after an actual Move() call - directly after a teleport it still
    /// shows the old (not grounded) state. Without the small probing move,
    /// fGetVerticalVelocity() wrongly takes this for "in the air" and applies real
    /// gravity for one frame instead of grounding immediately - that was the actual
    /// reason for the minimal falling that resetting the velocity alone did not
    /// fix.
    /// </summary>
    public void ResetVerticalVelocity()
    {
        mfVerticalVelocity = 0f;
        mbJumping = false;

        if (mController != null && mController.enabled)
            mController.Move(Vector3.down * 0.1f);
    }

    private void Awake()
    {
        mController = GetComponent<CharacterController>();
        msInstance = this;
        mScheme = GetComponent<UWControlScheme>();
    }

    private void Start()
    {
        mGameUi = GetComponentInChildren<UWGameUI>();
        mInput = mScheme.Controls.Player;
    }

    private void Update()
    {
        fUpdateDebugFields();

        if (fUpdateOnCore())
            return;

        // While a modal window is open - conversation, full-screen map - the character
        // stands still. Otherwise, in the original scheme, the pointer position keeps
        // driving the movement although the pointer is needed to operate the window
        // (per user, 2026-09-03).
        //
        // GRAVITY STILL RUNS. Until 2026-09-11 Update returned here entirely, and so the
        // character hung in the air as long as anything held the block: at startup the
        // loader places it above the floor while logos, intro or menu block the controls
        // (per user: "we have had problems with the character spawning in the air from
        // the very beginning"). If time stands still ([MORE], menu), deltaTime is zero
        // and nothing falls either.
        if (mScheme != null && mScheme.IsWorldInputBlocked)
        {
            mCurrentHorizontalVelocity = Vector3.zero;

            // A cursor walk ends here too, otherwise the clamp of the pointer to the viewport
            // (UWGameUI.fCursorMovement) outlived a conversation an NPC started while the left
            // button was down, and the pointer could not reach the parchment (per user,
            // 2026-09-19, with the goal-10 outcast).
            IsCursorMovementInProgress = false;
            mbCursorDragStartedInViewport = false;
            // A window in mid-jump ends the jump - the character then simply falls.
            mbJumping = false;

            if (mController != null && mController.enabled && !fIsHovering())
                mController.Move(new Vector3(0f, fGetVerticalVelocity(), 0f) * Time.deltaTime);

            return;
        }

        fUpdateHover();

        // SHIFT TURNS WASD INTO THE EASY MOVEMENT. It has to come before the ordinary
        // velocity, because while shift is held the same keys must not also walk.
        bool lbEasy = fUpdateEasyMovement();

        Vector3 lOTargetLocalVelocity = lbEasy
            ? Vector3.zero
            : mScheme.Current == UWControlScheme.SchemeEnum.Modern
                ? fGetModernVelocity()
                : fGetOriginalVelocity();

        mfDebugInputSpeed = lOTargetLocalVelocity.magnitude;

        lOTargetLocalVelocity *= Mathf.Max(0f, SpeedMultiplier) * fGetHoverSpeedFactor();

        mCurrentHorizontalVelocity = Vector3.MoveTowards(mCurrentHorizontalVelocity, lOTargetLocalVelocity, mfAcceleration * Time.deltaTime);

        // THE SPEED SPELL DOES NOT TOUCH THE PLAYER: it halves the rest of the world (see
        // UWCritterDriver.PlayerHasSpeed).
        Vector3 lOWorldVelocity = fApplyFlow(transform.TransformDirection(mCurrentHorizontalVelocity));
        // The right click during a cursor movement jumps too (see ConsumesRightClick).
        bool lbJumpClick = UWMouseButtons.RightPressed && ConsumesRightClick();

        if (mInput.Jump.WasPressedThisFrame() || lbJumpClick || fPadJumps())
            fTryJump(lOWorldVelocity, UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.shiftKey.isPressed);

        lOWorldVelocity.y = fGetVerticalVelocity();

        // During a jump the horizontal direction is fixed since takeoff - like in a fall,
        // which the jump includes at its end.
        if (mbJumping)
        {
            lOWorldVelocity.x = mOJumpVelocity.x;
            lOWorldVelocity.z = mOJumpVelocity.z;
        }
        else
            lOWorldVelocity = fApplyFall(lOWorldVelocity);

        // AFTER the fall: that freezes the horizontal direction, but a push should also
        // take effect in the air.
        lOWorldVelocity += mOKnockback;

        mOKnockback = Vector3.MoveTowards(mOKnockback, Vector3.zero,
            mfKnockbackDamping * Time.deltaTime);

        bool lbWasGrounded = mController.isGrounded;

        mController.Move(lOWorldVelocity * Time.deltaTime);

        fKeepOnSlope(lbWasGrounded, lOWorldVelocity);

        fUpdateFootsteps();
    }

    /// <summary>
    /// DOWNHILL THE FEET STAY ON THE GROUND (per user, 2026-09-30: under Slow Fall walking down
    /// a slope stuttered, and a jump could not be started while going down one). The
    /// CharacterController moves along its velocity and only then looks for ground; going
    /// down a slope faster than the stick force pulls it lifts off for a frame, isGrounded
    /// drops, and with it the jump and - under the spell - the pace. The original's motion is
    /// tile based and never leaves a slope. So a step that started on the ground and ends just
    /// above it is set down onto it: no further than the step's own horizontal distance (a
    /// slope of one in one) and never more than one floor height step, as fSettleAfterEasyStep
    /// allows - a real ledge is still a fall.
    /// </summary>
    private void fKeepOnSlope(bool pbWasGrounded, Vector3 pOWorldVelocity)
    {
        if (!pbWasGrounded || mController.isGrounded || mbJumping || fIsHovering() || IsInLiquid
            || pOWorldVelocity.y > 0f)
            return;

        float lfHorizontal = new Vector2(pOWorldVelocity.x, pOWorldVelocity.z).magnitude * Time.deltaTime;
        float lfReach = Mathf.Min(UWEasyMovement.DropAllowanceZPos * UWWorldScale.ZPosStep, lfHorizontal + SlopeSnapMargin);

        Vector3 lOFoot = transform.TransformPoint(mController.center)
            - (Vector3.up * (mController.height * 0.5f))
            + (Vector3.up * SettleProbeLift);

        if (!Physics.Raycast(lOFoot, Vector3.down, out RaycastHit lOHit, SettleProbeLift + lfReach,
            ~0, QueryTriggerInteraction.Ignore))
            return;

        float lfDrop = lOHit.distance - SettleProbeLift;

        if (lfDrop > 0f)
            mController.Move(Vector3.down * (lfDrop + mController.skinWidth));
    }

    /// <summary>What the ground probe of fKeepOnSlope reaches beyond the step's horizontal
    /// distance - rounding and the controller's skin. Judged by eye.</summary>
    private const float SlopeSnapMargin = 0.5f;

    /// <summary>
    /// The current does NOT act as thrust but as a factor on one's own movement:
    /// whoever stands still stays put, and whoever moves makes faster progress with the
    /// current and slower progress against it (checked by the user in the original, 2026-09-01).
    ///
    /// A first attempt pushed the player permanently instead - then one drifts off
    /// even without input, which the original does not do.
    /// </summary>
    private Vector3 fApplyFlow(Vector3 pOWorldVelocity)
    {
        if (FlowStrength <= 0f || FlowDirection.sqrMagnitude <= 0f)
            return pOWorldVelocity;

        Vector3 lOHorizontal = new Vector3(pOWorldVelocity.x, 0f, pOWorldVelocity.z);

        if (lOHorizontal.sqrMagnitude <= 0.0001f)
            return pOWorldVelocity;

        float lfAlignment = Vector3.Dot(lOHorizontal.normalized, FlowDirection.normalized);

        // Clamped from below so that one still makes progress against the current instead of getting stuck.
        float lfFactor = Mathf.Max(MinimumFlowFactor, 1f + (FlowStrength * lfAlignment));

        return new Vector3(pOWorldVelocity.x * lfFactor, pOWorldVelocity.y, pOWorldVelocity.z * lfFactor);
    }

    /// <summary>Lower limit of the current factor. Against the current one becomes slow, but
    /// never unable to move.</summary>
    private const float MinimumFlowFactor = 0.15f;

    /// <summary>How far a critter gives way per shove.</summary>
    [SerializeField]
    private float mfCritterPushDistance = 12f;

    /// <summary>How strongly the player is knocked back in the process.</summary>
    [SerializeField]
    private float mfCritterPushKnockback = 80f;

    /// <summary>For this long it does not work again, so the shove does not turn into
    /// continuous pushing. Half a second, estimated on the original (per user, 2026-09-05).</summary>
    [SerializeField]
    private float mfCritterPushCooldown = 0.5f;

    private float mfNextCritterPushTime;

    /// <summary>
    /// Walking into a critter gives it a SHOVE and the player bounces back a little -
    /// as with one's own projectile, not as continuous pushing (per user on the original,
    /// 2026-09-05).
    ///
    /// The critter itself checks whether it can stand at the target spot at all - this
    /// also removes shoving into water, which the original does not allow (per user, 2026-09-05).
    ///
    /// The reverse does not work: NPCs cannot shove the player. That follows naturally,
    /// because shoving hangs off the player controller's collision and creatures set
    /// their position directly.
    /// </summary>
    private void OnControllerColliderHit(ControllerColliderHit pOHit)
    {
        if (pOHit == null || pOHit.collider == null)
            return;

        fReportWallBump(pOHit);

        if (Time.time < mfNextCritterPushTime)
            return;

        UWCritter lOCritter = pOHit.collider.GetComponentInParent<UWCritter>();

        if (lOCritter == null)
            return;

        mfNextCritterPushTime = Time.time + mfCritterPushCooldown;

        // The push momentum of the original's momentum transfer (seg029_29EE_3: speed 0xEB),
        // the creature's record takes the heading and the speed byte 5 (UWCreatureMotion.Push).
        lOCritter.Push(UWCritter.DirectionToHeading(pOHit.moveDirection), UWDataImport.UWData.UWCreatureMotion.PushSpeed, 0);

        Vector3 lOBack = -pOHit.moveDirection;

        lOBack.y = 0f;

        if (lOBack.sqrMagnitude > 0.0001f)
            AddKnockback(lOBack.normalized * mfCritterPushKnockback);
    }

    /// <summary>
    /// COMING DOWN, through the same block as a wall (UWImpactRules) since 2026-09-22, when the
    /// user asked for it after the wall impact worked: "do the fall the same way". Before, the
    /// volume was the fall speed over eight and there was no fall damage at all.
    ///
    /// THE ONE THING THAT IS OURS is what turns a fall speed into the original's momentum. Its
    /// vertical field is fed by seg030_2B26 label E2D with a formula we could not reconcile
    /// with the numbers, so the scale is anchored on the one vertical value that IS known: a
    /// jump takes off with UWImpactRules.JumpMomentum, and our jump takes off at the speed
    /// mfJumpHeight and mfGravity give it. That anchor also settles the case that must not go
    /// wrong - a jump on the spot comes down at its take-off speed, which is worth two points,
    /// so it SOUNDS and does NOT hurt, as in the original. A tile's drop is worth two as well,
    /// two tiles three, three tiles four, and from there it hurts.
    ///
    /// NO DOUBLING here: the block doubles while the pitch field is set (label B10), but a jump
    /// on the spot would then cost four points and hurt, which it does not. When that doubling
    /// applies is not settled, so it stays off in both call sites.
    ///
    /// AND THE ACROBAT SKILL DOES NOT SILENCE A LANDING. In the original the block cuts the
    /// value with that skill before both the damage and the sound, and taking that literally
    /// left Henrietta - Acrobat 25, so a sixth of it - with nothing at all: a jump is worth two
    /// points, and two sixths is zero (per user, 2026-09-22: "now nothing sounds on a jump or a
    /// fall any more"). In the original she hears both. The cut there does not bite because the
    /// vertical way into the accumulator is far larger than we make it - it has no division by
    /// fifteen (seg030_2B26 label E2D) - and we could not pin that scale down.
    ///
    /// SO IT IS CALIBRATED ON WHAT THE USER MEASURED instead: the DAMAGE goes by the cut value,
    /// which is what the skill is for and why she takes none; the SOUND does not, because she
    /// hears every landing. The one place the cut still decides a sound is a wall ON THE GROUND,
    /// which is exactly the case she could hardly ever provoke.
    /// </summary>
    private void fReportLanding(float pfSpeed)
    {
        float lfTakeOff = Mathf.Sqrt(2f * mfGravity * mfJumpHeight);

        if (lfTakeOff <= 0f)
            return;

        int liMomentum = Mathf.RoundToInt(UWImpactRules.JumpMomentum * pfSpeed / lfTakeOff);

        // ONE SOUND, NOT TWO. In the original one bounces off the floor and so hears the impact a
        // second time, a little quieter (per user, 2026-09-22, tried in the original). Until the
        // same day a second, weaker blow followed here after 0.18 s to imitate that - but our
        // CharacterController does not bounce, and a second sound without the bounce it belongs
        // to felt out of place (per user, tried with a fresh character). It comes back together
        // with the bounce itself, when the player's motion leaves the Unity physics (Todo, P6).
        fReportImpact(UWImpactRules.GetImpactValue(liMomentum, false), true);
    }

    /// <summary>
    /// The second half of the impact block, the part that is the same whichever way one hit
    /// something: the Acrobat check cuts the value down, above three it hurts, above one it
    /// sounds - or below that over liquid, which is the splash.
    /// </summary>
    private int fReportImpact(int piValue, bool pbAlwaysSounds = false)
    {
        if (piValue <= 0)
            return 0;

        UWCharacter lOCharacter = UWScene.Character;

        if (lOCharacter != null)
        {
            int liAcrobat = lOCharacter.GetSkill(UWPlayerData.Skill.Acrobat);
            bool lbPassed = UWSkillCheck.IsSuccess(
                UWSkillCheck.Check(liAcrobat, UWImpactRules.GetAcrobatTarget(piValue)));

            int liAfter = UWImpactRules.ApplyAcrobat(piValue, liAcrobat, lbPassed);

            if (UWImpactRules.CausesDamage(liAfter))
                lOCharacter.ApplyDamage(liAfter, UWDamageTypes.Physical);

            // A bounce in the air is heard whatever the skill - see fReportWallBump. The
            // damage still goes by the cut value.
            if (!pbAlwaysSounds)
                piValue = liAfter;
        }

        if (!UWImpactRules.PlaysSound(piValue, IsInLiquid))
            return 0;

        UWSoundEffects.PlayAtAvatar(fLandingSound(), UWImpactRules.GetVolume(piValue));

        return piValue;
    }

    /// <summary>
    /// RUNNING INTO A WALL SOUNDS LIKE A LANDING - because in the original it IS a landing.
    /// The user noticed it in 2026-09-11, could not provoke it reliably afterwards and asked
    /// for the rule on 2026-09-22; it is in UWImpactRules, read out that day.
    ///
    /// The whole block is the one of ApplyPlayerMotion_seg008_90D and makes no difference
    /// between a floor and a wall: the collision hands it the momentum it took away, that over
    /// 256 is the impact value, an ACROBAT check may cut it down, above 1 it sounds and the
    /// volume is the value times four less sixty.
    ///
    /// OUR SUBSTITUTE is the momentum: the original carries it in the motion array we do not
    /// run, so it is taken from the speed into the wall against the full forward speed, times
    /// the 0x3AC of a full run - the same conversion the footsteps use. Everything after that
    /// is the original's arithmetic. The pause is ours, so that leaning on a wall stays quiet.
    /// </summary>
    private void fReportWallBump(ControllerColliderHit pOHit)
    {
        if (Time.time < mfNextWallBumpTime || Mathf.Abs(pOHit.normal.y) > WallNormalTolerance)
            return;

        // mCurrentHorizontalVelocity IS LOCAL to the player's rotation, the hit normal is not.
        // Without turning it into the world first the two only agreed while facing north, and
        // that is exactly what the user heard (2026-09-22) - the sound came only there. The
        // mistake was in the first build of this method.
        Vector3 lOWorld = transform.TransformDirection(mCurrentHorizontalVelocity);
        Vector3 lOFlat = new Vector3(lOWorld.x, 0f, lOWorld.z);
        float lfInto = -Vector3.Dot(lOFlat, new Vector3(pOHit.normal.x, 0f, pOHit.normal.z).normalized);
        float lfFullSpeed = Mathf.Max(mfModernForwardSpeed, mfOriginalForwardSpeed);

        if (lfInto <= 0f || lfFullSpeed <= 0f)
            return;

        int liMomentum = Mathf.RoundToInt(UWImpactRules.RunMomentum * lfInto / lfFullSpeed);
        bool lbAirborne = !mController.isGrounded && !fIsHovering() && !IsInLiquid;
        int liValue = UWImpactRules.GetImpactValue(liMomentum, false);

        if (liValue <= 0)
            return;

        mfNextWallBumpTime = Time.time + WallBumpCooldownSeconds;

        fReportImpact(liValue, lbAirborne);

        // BENT INTO SHAPE, like the second blow of a landing: in the original one BOUNCES off
        // a wall touched while falling, which is why the sound comes there every time and why a
        // wall behind gives a second one (per user, 2026-09-22). Our controller only stops, so
        // the push is put back by hand, and only in the air - on the ground running into a wall
        // must not throw one back.
        if (!lbAirborne)
            return;

        Vector3 lOAway = new Vector3(pOHit.normal.x, 0f, pOHit.normal.z);

        if (lOAway.sqrMagnitude > 0.0001f)
            AddKnockback(lOAway.normalized * (lfInto * AirborneWallRebound));
    }

    /// <summary>How much of the speed into a wall comes back out of it while falling. Judged by
    /// eye - the original's physics is bouncier than ours (per user, 2026-09-22).</summary>
    private const float AirborneWallRebound = 0.6f;

    /// <summary>
    /// THE EASY MOVEMENT on the keyboard: shift with W, S, A, D and X (UWEasyMovement holds
    /// the rule and where it comes from). Returns whether shift claims the movement keys this
    /// frame - then the ordinary walking stays still, otherwise one would step and walk at
    /// once.
    /// </summary>
    private bool fUpdateEasyMovement()
    {
        UnityEngine.InputSystem.Keyboard lOKeyboard = UnityEngine.InputSystem.Keyboard.current;

        if (lOKeyboard == null || !lOKeyboard.shiftKey.isPressed)
        {
            fReleaseEasyMovementIfIdle();

            return false;
        }

        if (mInput.EasyTurnLeft.IsPressed())
            HoldEasyMovement(UWEasyMovement.CommandTurnLeft);
        else if (mInput.EasyTurnRight.IsPressed())
            HoldEasyMovement(UWEasyMovement.CommandTurnRight);
        else if (mInput.EasyForward.IsPressed())
            HoldEasyMovement(UWEasyMovement.CommandRunForward);
        else if (mInput.EasyWalkForward.IsPressed())
            HoldEasyMovement(UWEasyMovement.CommandStepForward);
        else if (mInput.EasyBack.IsPressed())
            HoldEasyMovement(UWEasyMovement.CommandStepBack);
        else
            fReleaseEasyMovementIfIdle();

        return true;
    }

    /// <summary>
    /// A HELD KEY OR ARROW REPEATS, as in the original (per user, 2026-09-21). The first call
    /// steps at once, every further one when the interval has passed; a different command
    /// starts over. How long the interval is comes from the settings
    /// (UWUserSettings.EasyMovementInterval) - the original's own pace is too fast to aim
    /// with, see UWEasyMovement.DefaultRepeatSeconds.
    ///
    /// Called every frame while the key or the mouse button is down, by the movement for the
    /// keys and by Interaction for the three arrows.
    /// </summary>
    public void HoldEasyMovement(int piCommand)
    {
        miEasyHoldFrame = Time.frameCount;

        if (!mbEasyHeld || miEasyHeldCommand != piCommand)
        {
            mbEasyHeld = true;
            miEasyHeldCommand = piCommand;
            mfNextEasyRepeat = Time.time + UWUserSettings.EasyMovementInterval;

            TryEasyMovement(piCommand);

            return;
        }

        if (Time.time < mfNextEasyRepeat)
            return;

        mfNextEasyRepeat = Time.time + UWUserSettings.EasyMovementInterval;

        TryEasyMovement(piCommand);
    }

    /// <summary>Lets go of the repeat unless someone asked for it in this frame or the one
    /// before - Interaction and the movement update in an order that is not fixed.</summary>
    private void fReleaseEasyMovementIfIdle()
    {
        if (mbEasyHeld && miEasyHoldFrame < Time.frameCount - 1)
            mbEasyHeld = false;
    }

    private bool mbEasyHeld;

    private int miEasyHeldCommand;

    private int miEasyHoldFrame = -2;

    private float mfNextEasyRepeat;

    /// <summary>
    /// One command of the easy movement, from the keys or from the three arrows under the
    /// compass (UWHudCompass). A turn snaps onto the 45 degree line when the view is not on
    /// one; a step carries half a tile forward or a quarter back, and does nothing at all when
    /// something is in the way - the original's CheckIfItemFitsInTile is likewise all or
    /// nothing.
    ///
    /// ON THE MOTION CORE it is the original's step since 2026-10-06 (UWPlayerMotion.EasyStep:
    /// the target, CheckIfItemFitsInTile, the floor-kind rule, the drop) and its gate
    /// (UWPlayerMotion.MayEasyMove). THE OLD PATH, the CharacterController's, still sweeps the
    /// character's own capsule along the step and probes the ground after it, with the
    /// original's drop rule (UWEasyMovement.AllowsDrop); it refuses while jumping.
    /// </summary>
    public bool TryEasyMovement(int piCommand)
    {
        if (mController == null || !mController.enabled || mbJumping)
            return false;

        if (mScheme != null && mScheme.IsWorldInputBlocked)
            return false;

        // ON THE CORE the original's gate: no turn and no step while gravity acts or the speed
        // has reached the motion weight (UWPlayerMotion.MayEasyMove).
        if (UsesMotionCore && !Motion.MayEasyMove)
            return false;

        if (UWEasyMovement.IsTurn(piCommand))
        {
            float lfYaw = Mathf.Repeat(transform.eulerAngles.y, 360f);
            int liYaw = Mathf.RoundToInt(lfYaw * UWEasyMovement.Circle / 360f) & (UWEasyMovement.Circle - 1);

            liYaw = UWEasyMovement.Turn(liYaw, piCommand);

            Vector3 lOAngles = transform.eulerAngles;

            lOAngles.y = liYaw * 360f / UWEasyMovement.Circle;
            transform.eulerAngles = lOAngles;

            if (UsesMotionCore)
            {
                Motion.CameraYaw = liYaw & 0xFFFF;
                Motion.MotionYaw = Motion.CameraYaw;
                fSnapInterpolation(Motion);
                miLastWrittenYaw = Motion.CameraYaw;
                Motion.EndEasyTurn();
            }

            fReportEasyMovementStep();

            return true;
        }

        int liDistance = UWEasyMovement.GetStepDistance(piCommand);

        if (liDistance <= 0)
            return false;

        if (UsesMotionCore)
        {
            // The original's step on the params; the transform follows.
            if (!Motion.EasyStep(piCommand, fTransformYawToAngle()))
                return false;

            fSnapInterpolation(Motion);
            fApplyMotionToTransform(Motion);
            fReportEasyMovementStep();

            return true;
        }

        float lfStep = liDistance * UnderworldRevisited.Build.UWLevelMeshBuilder.TileSpacing
            / UWEasyMovement.UnitsPerTile;

        Vector3 lOFlat = new Vector3(transform.forward.x, 0f, transform.forward.z);

        if (lOFlat.sqrMagnitude < 0.0001f)
            return false;

        lOFlat.Normalize();

        if (UWEasyMovement.IsBackward(piCommand))
            lOFlat = -lOFlat;

        if (!fIsEasyStepFree(lOFlat, lfStep))
            return false;

        if (!fMayEasyStepDrop(piCommand) && fIsEasyStepOverADrop(lOFlat, lfStep))
            return false;

        bool lbWasGrounded = mController.isGrounded;

        mController.Move(lOFlat * lfStep);
        fSettleAfterEasyStep(lbWasGrounded);
        fReportEasyMovementStep();

        return true;
    }

    /// <summary>
    /// Whether the character's capsule gets through the step without touching anything - the
    /// substitute for CheckIfItemFitsInTile.
    ///
    /// THE PROBE STARTS A STEP HEIGHT ABOVE THE FOOT, otherwise a slope going UP counts as a
    /// wall and the step is refused on it (per user, 2026-09-21: "only slopes upward do not
    /// work"). The CharacterController climbs anything within its own step offset by itself,
    /// so what it can walk over must not block the probe either.
    /// </summary>
    private bool fIsEasyStepFree(Vector3 pODirection, float pfDistance)
    {
        float lfRadius = mController.radius;
        float lfHalf = Mathf.Max(lfRadius, mController.height * 0.5f) - lfRadius;
        Vector3 lOCentre = transform.TransformPoint(mController.center);
        Vector3 lOTop = lOCentre + (Vector3.up * lfHalf);
        Vector3 lOBottom = lOCentre - (Vector3.up * lfHalf) + (Vector3.up * mController.stepOffset);

        if (lOBottom.y > lOTop.y)
            lOBottom = lOTop;

        return !Physics.CapsuleCast(lOBottom, lOTop, lfRadius * EasyStepClearance, pODirection,
            out RaycastHit lOUnused, pfDistance, ~0, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// A step is instant, so it has to END on the ground. Without this the character stood in
    /// the air over a slope going DOWN and sank the rest of the way under gravity, which looks
    /// like falling (per user, 2026-09-21).
    ///
    /// ONLY AS FAR AS THE STEP WAS ALLOWED TO DROP: the ground is taken if it lies within
    /// UWEasyMovement.DropAllowanceZPos, one floor height step. Anything deeper is a real
    /// ledge, and there falling is the right answer - that is what the W command is for.
    /// Whoever was already in the air, hovering or swimming keeps falling or floating.
    /// </summary>
    private void fSettleAfterEasyStep(bool pbWasGrounded)
    {
        if (!pbWasGrounded || fIsHovering() || IsInLiquid)
            return;

        float lfAllowance = UWEasyMovement.DropAllowanceZPos * UWWorldScale.ZPosStep;
        Vector3 lOFoot = transform.TransformPoint(mController.center)
            - (Vector3.up * (mController.height * 0.5f))
            + (Vector3.up * SettleProbeLift);

        if (!Physics.Raycast(lOFoot, Vector3.down, out RaycastHit lOHit, SettleProbeLift + lfAllowance,
            ~0, QueryTriggerInteraction.Ignore))
            return;

        float lfDrop = lOHit.distance - SettleProbeLift;

        if (lfDrop > 0f)
            mController.Move(Vector3.down * lfDrop);
    }

    /// <summary>How far above the foot the ground probe of fSettleAfterEasyStep starts, so it
    /// does not begin inside the floor it is standing on.</summary>
    private const float SettleProbeLift = 1f;

    /// <summary>The capsule is probed a little thinner than it is, so that a step does not
    /// fail on a wall one is already leaning against. Judged by eye.</summary>
    private const float EasyStepClearance = 0.95f;

    /// <summary>
    /// Whether this step may go off a ledge: the original's flag, which the W command sets and
    /// Levitate or Fly set as well (UWEasyMovement.AllowsDrop and FloatingAbilities). While
    /// already hovering or falling there is nothing to refuse either.
    /// </summary>
    private bool fMayEasyStepDrop(int piCommand)
    {
        if (UWEasyMovement.AllowsDrop(piCommand) || fIsHovering() || !fIsOnGround())
            return true;

        int liAbilities = MagicalMotionAbilities | SpellMotionAbilities;

        return (liAbilities & UWEasyMovement.FloatingAbilities) != 0;
    }

    /// <summary>
    /// Whether the ground after the step lies further below than the original allows without
    /// the flag - eight zpos units, which is one floor height step
    /// (UWEasyMovement.DropAllowanceZPos). Nothing found under the foot at all counts as a
    /// drop.
    /// </summary>
    private bool fIsEasyStepOverADrop(Vector3 pODirection, float pfDistance)
    {
        float lfAllowance = UWEasyMovement.DropAllowanceZPos * UWWorldScale.ZPosStep;
        Vector3 lOFoot = transform.TransformPoint(mController.center)
            - (Vector3.up * (mController.height * 0.5f))
            + (pODirection * pfDistance);

        // A little above the foot, so the ray starts clear of the floor it is standing on.
        float lfLift = mController.stepOffset + lfAllowance;

        return !Physics.Raycast(lOFoot + (Vector3.up * lfLift), Vector3.down, out RaycastHit lOUnused,
            lfLift + lfAllowance, ~0, QueryTriggerInteraction.Ignore);
    }

    /// <summary>The noise and the clock of one step - both are the character's business -
    /// and the world's 64 ticks of it (seg034_2F89_334: every step and turn gives the mobile
    /// objects four units, halved under Speed; UWCritterDriver.StepWorld, since 2026-10-05).</summary>
    private void fReportEasyMovementStep()
    {
        UWCharacter lOCharacter = UWScene.Character;

        if (lOCharacter != null)
            lOCharacter.ReportEasyMovementStep();

        if (UWCritterDriver.Instance != null)
            UWCritterDriver.Instance.StepWorld();
    }

    /// <summary>How flat a surface has to be to count as a wall - see fReportWallBump.</summary>
    private const float WallNormalTolerance = 0.5f;

    /// <summary>The pause between two of them, so that pressing against a wall stays quiet.
    /// </summary>
    private const float WallBumpCooldownSeconds = 0.6f;

    private float mfNextWallBumpTime;

    /// <summary>
    /// Whoever falls can no longer steer - only turn (per user on the original,
    /// 2026-09-03).
    ///
    /// Therefore the WORLD direction at the start of the fall is recorded and kept until
    /// landing. The usual calculation rotates the direction along with the character,
    /// because it keeps the velocity in the character's own coordinates - that would let
    /// one still change course in the air.
    ///
    /// On touchdown the direction is converted back so there is no jolt.
    ///
    /// Falling means: without ground under the feet for a short grace period, and moving downwards.
    ///
    /// A minimum SPEED for this was a mistake - Slow Fall caps at thirty, every sensible
    /// threshold was above that, and so of all things one never counted as falling under
    /// the spell. Therefore the TIME without ground is measured.
    ///
    /// Levitate and Fly are exempt and stay controllable. In liquid the fall does not
    /// apply either.
    /// </summary>
    private Vector3 fApplyFall(Vector3 pOWorldVelocity)
    {
        if (IsInLiquid || fIsHovering() || mController.isGrounded)
            mfAirborneTime = 0f;
        else
            mfAirborneTime += Time.deltaTime;

        bool lbFalling = mfAirborneTime > mfFallGraceSeconds && mfVerticalVelocity < 0f;

        if (lbFalling)
        {
            if (!mbFalling)
            {
                mbFalling = true;
                mOFallVelocity = new Vector3(pOWorldVelocity.x, 0f, pOWorldVelocity.z);
            }

            // Slow Fall removes all momentum - one sinks vertically (per user on the
            // original, 2026-09-03). The reference halves it step by step, which
            // amounts to the same thing.
            if ((MagicalMotionAbilities & SlowFallBit) != 0)
                mOFallVelocity = Vector3.zero;

            return new Vector3(mOFallVelocity.x, pOWorldVelocity.y, mOFallVelocity.z);
        }

        if (mbFalling)
        {
            mbFalling = false;
            mCurrentHorizontalVelocity = transform.InverseTransformDirection(mOFallVelocity);

            fReportLanding(mfImpactSpeed > 0f ? mfImpactSpeed : Mathf.Abs(mfVerticalVelocity));
            mfImpactSpeed = 0f;
        }

        return pOWorldVelocity;
    }

    // ------------------------------------------------------------------
    // Footsteps
    // ------------------------------------------------------------------

    /// <summary>When the next step sounds.</summary>
    private float mfNextStepTime;

    private bool mbStepToggle;

    /// <summary>From this speed in tiles per second on, footsteps play.</summary>
    private const float StepSpeedThreshold = 0.3f;

    /// <summary>The original's momentum at full run (motion_player, 0x3AC).</summary>
    private const int FullMomentum = 0x3AC;

    /// <summary>
    /// How much of the full forward speed is actually being made, 0 to 1 - the original's
    /// Player_MotionArray_unk_14 over its cap, which is PlayerActualForwardSpeed.
    /// Measured from the distance covered, exactly like the footsteps beside it, so turning
    /// on the spot counts as standing. The noise reads it (UWPlayerVitals.TickQuietness);
    /// it is kept up to date even in water and in the air, where the footsteps stay silent.
    /// </summary>
    public float MomentumFraction { get; private set; }

    /// <summary>The original's PIT counter runs at 256 Hz (reference: main.cs).</summary>
    private const float PitTicksPerSecond = 256f;

    /// <summary>Where the character stood in the previous frame - for the distance actually
    /// travelled.</summary>
    private Vector3 mOLastStepPosition;

    /// <summary>
    /// Footsteps alternating between the two sounds, using the original's formula
    /// (reference: playerdatloop, uw1 branch): the interval is 0x40 plus 0x1770 divided by
    /// (1 plus momentum divided by 4) PIT ticks, at most 0xC8 - at full run 89 ticks, i.e.
    /// 0.35 s, at half 0.45 s, when sneaking 0.78 s. Louder with momentum: 0xF plus momentum
    /// divided by 32 as note offset. Our momentum comes from the distance ACTUALLY
    /// travelled, not the target velocity - whoever turns on the spot makes no footsteps
    /// (per user, 2026-09-11). Nothing in water or in the air.
    /// </summary>
    private void fUpdateFootsteps()
    {
        Vector3 lOPosition = transform.position;
        Vector3 lODelta = lOPosition - mOLastStepPosition;
        mOLastStepPosition = lOPosition;

        float lfFullSpeed = Mathf.Max(mfModernForwardSpeed, mfOriginalForwardSpeed)
            / UnderworldRevisited.Build.UWLevelMeshBuilder.TileSpacing;
        float lfSpeed = Time.deltaTime > 0f
            ? new Vector3(lODelta.x, 0f, lODelta.z).magnitude / Time.deltaTime
                / UnderworldRevisited.Build.UWLevelMeshBuilder.TileSpacing
            : 0f;

        MomentumFraction = lfFullSpeed > 0f ? Mathf.Clamp01(lfSpeed / lfFullSpeed) : 0f;

        if (mController == null || IsInLiquid || fIsHovering() || !fIsOnGround() || Time.deltaTime <= 0f)
        {
            mfNextStepTime = Time.time + 0.2f;
            return;
        }

        if (lfSpeed < StepSpeedThreshold)
        {
            mfNextStepTime = Time.time + 0.2f;
            return;
        }

        if (Time.time < mfNextStepTime)
            return;

        int liMomentum = Mathf.Clamp(Mathf.RoundToInt(FullMomentum * lfSpeed / lfFullSpeed), 0, FullMomentum);

        UWSoundEffects.PlayAtAvatar(mbStepToggle ? UWSoundEffects.FootstepA : UWSoundEffects.FootstepB,
            0xF + (liMomentum >> 5));
        mbStepToggle = !mbStepToggle;

        int liTicks = Mathf.Min(0xC8, 0x40 + (0x1770 / (1 + (liMomentum >> 2))));

        mfNextStepTime = Time.time + (liTicks / PitTicksPerSecond);
    }

    /// <summary>
    /// The vertical component, including the motion spells.
    ///
    /// All three take effect ONLY without ground under the feet (per user on the original,
    /// 2026-09-03) - see fUpdateHover, which tracks the hover state.
    ///
    /// While hovering the character does not fall but follows a target height: the
    /// hover height plus a sine oscillation. The reference sets gravity to zero for this
    /// and damps whatever vertical velocity remains; the oscillation itself is not in
    /// there, it was read off the original.
    ///
    /// Slow Fall only limits sinking.
    /// </summary>
    private float fGetVerticalVelocity()
    {
        if (mbJumping)
            return fGetJumpVerticalVelocity();

        if (fIsHovering())
        {
            float lfTargetY = mfHoverBaseY + (Mathf.Sin(mfHoverPhase) * mfHoverAmplitude);
            float lfStep = (lfTargetY - transform.position.y) / Mathf.Max(Time.deltaTime, 0.0001f);

            mfVerticalVelocity = Mathf.Clamp(lfStep, -mfHoverTrackSpeed, mfHoverTrackSpeed);

            return mfVerticalVelocity;
        }

        if (mController.isGrounded)
        {
            // THE SPEED OF THE TOUCHDOWN HAS TO BE KEPT HERE. This runs before fApplyFall,
            // which is where the landing is reported, so by then the stick force below has
            // long wiped the fall speed - the landing was reported with almost nothing and
            // stayed silent (per user, 2026-09-22: "in the jump it works, in a fall it does
            // not"). The jump has its own path and was never affected.
            if (mfVerticalVelocity < -mfGroundedStickForce)
                mfImpactSpeed = -mfVerticalVelocity;

            mfVerticalVelocity = -mfGroundedStickForce;
            return mfVerticalVelocity;
        }

        mfVerticalVelocity -= mfGravity * Time.deltaTime;

        // SLOW FALL ONLY ONCE IT IS A FALL (per user, 2026-09-30: walking down a slope under the
        // spell stuttered, "as if one were not on the ground"). Going downhill the controller
        // lifts off the slope for a frame or two; capped at the spell's thirty the character
        // could not follow the slope down, stayed off the ground past the grace period, counted as
        // falling - and Slow Fall took the momentum away. Within the grace period gravity pulls as
        // usual, so the feet find the slope again; a real fall is capped from then on.
        if ((MagicalMotionAbilities & SlowFallBit) != 0 && mfAirborneTime > mfFallGraceSeconds
            && mfVerticalVelocity < -mfSlowFallSpeed)
            mfVerticalVelocity = -mfSlowFallSpeed;

        return mfVerticalVelocity;
    }

    /// <summary>
    /// Takes off if possible (per user, 2026-09-13: "implement jumping. With
    /// Space and, as in the original, with J").
    ///
    /// TWO JUMPS as in the original (per user, 2026-09-13: "j jumps up and
    /// Shift + j jumps forward"), in the reference commands 7 and 6 (motion_player):
    /// J or Space jumps up and keeps only the momentum one already has -
    /// so from a standstill it jumps on the spot. With Shift, half the forward speed is
    /// added as momentum from a standstill; whoever is already moving keeps theirs. Jumps
    /// only start from the ground, and in the air the direction can no longer be changed.
    ///
    /// Not while levitating or flying, not in water and not while typing -
    /// there the J belongs to the text.
    /// </summary>
    private void fTryJump(Vector3 pOWorldVelocity, bool pbForward)
    {
        if (mbJumping || (IsInLiquid && !IsOnLava) || fIsHovering() || !mController.isGrounded || mfJumpHeight <= 0f)
            return;

        if (mGameUi != null && mGameUi.IsTextEntryActive)
            return;

        Vector3 lOHorizontal = new Vector3(pOWorldVelocity.x, 0f, pOWorldVelocity.z);

        if (pbForward && lOHorizontal.sqrMagnitude < 1f)
        {
            float lfForward = mScheme != null && mScheme.Current == UWControlScheme.SchemeEnum.Modern
                ? mfModernForwardSpeed
                : mfOriginalForwardSpeed;

            Vector3 lOForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

            lOHorizontal = lOForward * (lfForward * mfStandingJumpForwardFactor * Mathf.Max(0f, SpeedMultiplier));
        }

        mOJumpVelocity = lOHorizontal;
        mfJumpTime = 0f;
        mbJumping = true;

        // The takeoff velocity for the desired height under NORMAL gravity.
        // Under Leap gravity is weaker afterwards - the jump becomes higher, as in
        // the reference, where only gravity changes.
        mfVerticalVelocity = Mathf.Sqrt(2f * mfGravity * mfJumpHeight);
    }

    /// <summary>The vertical component during a jump: gravity (weaker under Leap), the ascent
    /// ends at the ceiling, and the jump ends on touchdown.</summary>
    private float fGetJumpVerticalVelocity()
    {
        mfJumpTime += Time.deltaTime;

        if (mfJumpTime > JumpLandingGrace && mController.isGrounded && mfVerticalVelocity <= 0f)
        {
            fLand();
            mfVerticalVelocity = -mfGroundedStickForce;

            return mfVerticalVelocity;
        }

        if (mfVerticalVelocity > 0f && (mController.collisionFlags & CollisionFlags.Above) != 0)
            mfVerticalVelocity = 0f;

        float lfGravity = (MagicalMotionAbilities & UWCharacter.LeapBit) != 0
            ? mfGravity * mfLeapGravityFactor
            : mfGravity;

        mfVerticalVelocity -= lfGravity * Time.deltaTime;

        if ((MagicalMotionAbilities & SlowFallBit) != 0 && mfVerticalVelocity < -mfSlowFallSpeed)
            mfVerticalVelocity = -mfSlowFallSpeed;

        return mfVerticalVelocity;
    }

    /// <summary>End of the jump: the momentum passes into normal movement without a jolt, and
    /// the landing goes through the impact block like any other (UWImpactRules) - since
    /// 2026-09-22 this no longer has a formula of its own.</summary>
    private void fLand()
    {
        float lfSpeed = Mathf.Abs(mfVerticalVelocity);

        mbJumping = false;
        mbFalling = false;
        mfAirborneTime = 0f;
        mCurrentHorizontalVelocity = transform.InverseTransformDirection(mOJumpVelocity);

        fReportLanding(lfSpeed);
    }

    /// <summary>
    /// Landing in water SPLASHES instead of thudding: effect 5, the same splash a thrown
    /// object makes (UWLevelLoader.fSplashAt) - not effect 0, which is the rushing one hears
    /// while in the water.
    ///
    /// THE DECISION SITS HERE, at the landing, and nowhere else: walking into water is silent
    /// in the original, jumping in splashes (per user, 2026-09-21), and UW.EXE decides it in
    /// the same place - the landing block of ApplyPlayerMotion_seg008_90D (labels B6C to B95)
    /// is the only thing that makes a sound after a fall. Lava keeps the ordinary landing.
    /// </summary>
    private int fLandingSound()
    {
        return IsInLiquid && !IsOnLava ? UWSoundEffects.Splash : UWSoundEffects.Landing;
    }

    /// <summary>Is the character currently levitating or flying?</summary>
    private bool fIsHovering()
    {
        if (UsesMotionCore)
            return Motion.CurrentState == UWPlayerMotion.State.Levitating || Motion.CurrentState == UWPlayerMotion.State.Flying;

        return mbHovering;
    }

    /// <summary>
    /// Updates the hover state.
    ///
    /// On the ground one does NOT hover - whoever touches down stands again (per user on the original).
    /// Whoever CASTS the spell lifts off immediately while standing: otherwise the oscillation
    /// would put them right back on the ground. The same applies when standing on the ground
    /// and wanting to go up with E.
    ///
    /// A RING DOES NOT LIFT OFF. It only carries once there really is no ground left (per
    /// user, 2026-09-09) - otherwise one would hover with it constantly, right after every
    /// level change. Therefore only SpellMotionAbilities counts for lift-off.
    ///
    /// "NO GROUND LEFT" does not mean "one frame without ground contact": on a staircase and
    /// a slope the contact keeps dropping out briefly. What counts is the distance to the
    /// tile floor - see mfHoverGroundClearance.
    ///
    /// Q and E adjust the height around which the oscillation happens.
    /// </summary>
    private void fUpdateHover()
    {
        bool lbSpell = (MagicalMotionAbilities & (LevitateBit | FlyBit)) != 0;
        bool lbCastSpell = (SpellMotionAbilities & (LevitateBit | FlyBit)) != 0;
        // In the modern scheme E is the context key, so Space and the left Ctrl rise and sink there
        // (V until 2026-10-04, C until 2026-10-03); the classic scheme keeps the original's keys.
        float lfInput = mScheme != null && mScheme.Current == UWControlScheme.SchemeEnum.Modern
            ? mInput.ModernHover.ReadValue<float>()
            : mInput.HoverHeight.ReadValue<float>();

        if (lbCastSpell && !mbHoverSpellWasActive && mController.isGrounded)
            fBeginLiftOff();

        mbHoverSpellWasActive = lbCastSpell;

        if (!lbSpell)
        {
            mbHovering = false;
            mbHoverLifting = false;
            mbHoverFromJump = false;

            return;
        }

        // A JUMP TURNS INTO HOVERING at its top (per user, 2026-09-18: with the Ring of
        // Levitation the effect comes on when jumping in the original, in ours it did not).
        // UW.EXE seg008_1B2A: in the air with Levitate or Fly gravity is 0 and the vertical
        // speed is damped to four fifths per tick, so the jump rises a little further and
        // then stays up. Here the jump hovers once it is higher than the walking clearance of
        // fIsAirborne (one and a half steps; per user on the original, 2026-09-18: the hover starts
        // earlier than at the apex, "probably your estimated one and a half"), or at its apex if
        // it never gets that high (a low ceiling).
        if (mbJumping && (mfVerticalVelocity <= 0f
            || HeightAboveFloor > mfHoverGroundClearance + (UWTile.HeightLevel * 0.5f)))
        {
            mbJumping = false;
            mbFalling = false;
            mfAirborneTime = 0f;
            mCurrentHorizontalVelocity = transform.InverseTransformDirection(mOJumpVelocity);
            mbHoverFromJump = true;
        }

        if (mbHoverFromJump && mController.isGrounded)
            mbHoverFromJump = false;

        bool lbAirborne = fIsAirborne() || mbHoverFromJump;

        // Lifted off: from now on the only thing that counts is that there is no ground left.
        if (mbHoverLifting && lbAirborne)
            mbHoverLifting = false;

        // TOUCHING DOWN ENDS THE LOW HOVER TOO (per user, 2026-09-27, reproduced: hovering, and
        // Q held again at the lower turning point of the bob). The low hover after a lift-off
        // never gets "airborne" - lift-off 5 and the bob's 4 stay below the clearance -, so
        // mbHoverLifting alone carried it and only ended with the spell; Q pushed the character
        // onto the floor and it hovered on there at the levitation speed (0.2), with the ring
        // until the next load - the slow walk after Tybal's death cutscene, Todo "SLOW AFTER
        // TYBAL'S DEATH". Whoever touches down stands again (per user on the original); a
        // lift-off itself is not cut short, its target lies above the feet.
        if (mbHoverLifting && mController.isGrounded
            && mfHoverBaseY + (Mathf.Sin(mfHoverPhase) * mfHoverAmplitude) <= transform.position.y)
            mbHoverLifting = false;

        if (!lbAirborne && !mbHoverLifting && lfInput > 0f)
            fBeginLiftOff();

        bool lbHovering = mbHoverLifting || lbAirborne;

        // From falling into hovering: continue oscillating from the current height.
        if (lbHovering && !mbHovering && !mbHoverLifting)
        {
            mfHoverPhase = 0f;
            mfHoverBaseY = transform.position.y;
        }

        mbHovering = lbHovering;

        if (!mbHovering)
            return;

        mfHoverBaseY += lfInput * mfHoverClimbSpeed * Time.deltaTime;

        if (mfHoverPeriodSeconds > 0f)
            mfHoverPhase += (2f * Mathf.PI / mfHoverPeriodSeconds) * Time.deltaTime;
    }

    /// <summary>
    /// Is there really no ground under the feet any more?
    ///
    /// The CharacterController's ground contact alone is not enough - it drops out for
    /// single frames while walking, and constantly on a staircase or a slope. Therefore
    /// the distance to the tile floor, tracked by UWPlayerTerrain, counts as well.
    /// </summary>
    private bool fIsAirborne()
    {
        if (mController.isGrounded)
            return false;

        // WALKING DOWN A STEP IS NOT A FALL. At the moment the lower tile is under the feet,
        // they are one full step plus the controller's skin above its floor - a hair more than
        // mfHoverGroundClearance, and the hover effect flashed on for an instant (per user,
        // 2026-09-18: in the original it does not on a stair step, only on a jump). Outside a
        // jump half a step of margin is added: a single step never reaches it, a drop of two
        // steps (more than one can climb) does.
        float lfClearance = mbJumping
            ? mfHoverGroundClearance
            : mfHoverGroundClearance + (UWTile.HeightLevel * 0.5f);

        return HeightAboveFloor > lfClearance;
    }

    /// <summary>Sets the hover height above the ground and lets the character rise
    /// there.</summary>
    private void fBeginLiftOff()
    {
        mfHoverPhase = 0f;
        mfHoverBaseY = transform.position.y + mfHoverLiftOff;
        mbHoverLifting = true;
    }

    /// <summary>The speed in the air. Fly beats Levitate if both are active.</summary>
    private float fGetHoverSpeedFactor()
    {
        if (!fIsHovering())
            return 1f;

        return (MagicalMotionAbilities & FlyBit) != 0
            ? mfFlySpeedFactor
            : mfLevitateSpeedFactor;
    }

    /// <summary>The bits that matter here - the same values as in UWCharacter.</summary>
    private const int SlowFallBit = UWCharacter.SlowFallBit;

    private const int LevitateBit = UWCharacter.LevitateBit;

    private const int FlyBit = UWCharacter.FlyBit;

    /// <summary>WASD/gamepad stick relative to one's own view direction, as in any modern game.</summary>
    private Vector3 fGetModernVelocity()
    {
        Vector2 lODirection = fMoveInput();

        if (lODirection.sqrMagnitude > 1f)
            lODirection.Normalize();

        float lfForwardSpeed = lODirection.y >= 0f ? mfModernForwardSpeed : mfModernBackwardSpeed;

        return new Vector3(lODirection.x * mfModernStrafeSpeed, 0f, lODirection.y * lfForwardSpeed);
    }

    /// <summary>
    /// Original scheme: the mouse pointer position in the game window determines direction and
    /// turning while the left mouse button is held - exactly as in the 1992 original. Turning
    /// rotates the player transform directly; there is no free mouse look in this scheme.
    /// </summary>
    private Vector3 fGetOriginalVelocity()
    {
        // Lazy instead of in Start(): UWItemDrag is only created later at runtime via
        // AddComponent (UWLevelLoader.fInitialiseUi), at a point in time that is not
        // guaranteed relative to this Start() - a one-time Start() lookup could
        // therefore permanently cache null.
        if (mItemDrag == null)
            mItemDrag = GetComponentInChildren<UWItemDrag>();

        // While UWItemDrag drags an item from the backpack onto the paper doll (same
        // mouse button as the cursor movement), crossing the 3D viewport must not
        // move the player at the same time - otherwise one starts walking while equipping.
        // Only while the button is HELD: an item that merely sticks to the pointer leaves the
        // left button to walking (per user on the original, 2026-09-18).
        if (mItemDrag != null && mItemDrag.IsHeldDrag)
        {
            IsCursorMovementInProgress = false;
            return fGetOriginalKeyboardVelocity();
        }

        // While a spell waits for its target, the character KEEPS walking: in the original
        // the left button remains movement (per user, 2026-09-05). A first version
        // blocked walking here.
        //
        // It triggers NOTHING in the process - the spell stays pending, targeting in the
        // viewport is done with the right button only (per user, 2026-09-10; the opposite
        // assumption was in Interaction.fUpdateSpellTargeting until then).
        // ONLY A PRESS THAT STARTED IN THE VIEWPORT moves. A left click on the panels (backpack,
        // runes, compass) used to count as a cursor drag too: the pointer was clamped into the
        // viewport and the player stopped, even while walking with the keyboard (per user,
        // 2026-09-14). Once started, the drag may leave the viewport as before.
        // The left button is read through UWMouseButtons: a click under a held right button is
        // ignored there, everywhere (per user on the original, 2026-09-19).
        if (mGameUi != null && UWMouseButtons.LeftPressed && UnityEngine.InputSystem.Mouse.current != null)
            mbCursorDragStartedInViewport = mGameUi.IsScreenPositionInGameArea(UnityEngine.InputSystem.Mouse.current.position.ReadValue());

        IsCursorMovementInProgress = mGameUi != null && UWMouseButtons.LeftHeld && mbCursorDragStartedInViewport;

        // With the left mouse button held, the cursor takes precedence (including turning).
        // Otherwise WASD keeps moving in pure translation, as was always possible in the
        // original alongside cursor control - just never both at once,
        // and without WASD turning by itself: turning stays a cursor matter in this scheme.
        if (!IsCursorMovementInProgress)
            return fGetOriginalKeyboardVelocity();

        // Speed and turn rate are analogue: the farther the pointer is from the zone
        // boundary, the stronger the response (mGameUi.CursorWalkFactor/CursorRotateFactor,
        // 0..1) - as in the original, instead of fixed values per zone. Strafing is the
        // exception: it always uses the full mfOriginalStrafeSpeed.
        switch (mGameUi.CursorMovement)
        {
            case UWCursors.CursorEnum.Forward:
                return new Vector3(0f, 0f, mfOriginalForwardSpeed * mGameUi.CursorWalkFactor);

            case UWCursors.CursorEnum.Backward:
                return new Vector3(0f, 0f, -mfOriginalBackwardSpeed * mGameUi.CursorWalkFactor);

            case UWCursors.CursorEnum.StrafeLeft:
                return new Vector3(-mfOriginalStrafeSpeed, 0f, 0f);

            case UWCursors.CursorEnum.StrafeRight:
                return new Vector3(mfOriginalStrafeSpeed, 0f, 0f);

            case UWCursors.CursorEnum.TurnLeft:
                transform.Rotate(0f, -mfTurnSpeed * mGameUi.CursorRotateFactor * Time.deltaTime, 0f);
                return Vector3.zero;

            case UWCursors.CursorEnum.TurnRight:
                transform.Rotate(0f, mfTurnSpeed * mGameUi.CursorRotateFactor * Time.deltaTime, 0f);
                return Vector3.zero;

            // Walking at the same speed as straight ahead: UW.EXE uses one command for the whole
            // forward band (seg034_2F89_4E), the halved speed was our own (removed per user,
            // 2026-09-15).
            case UWCursors.CursorEnum.SlightLeft:
                transform.Rotate(0f, -mfSlightTurnSpeed * mGameUi.CursorRotateFactor * Time.deltaTime, 0f);
                return new Vector3(0f, 0f, mfOriginalForwardSpeed * mGameUi.CursorWalkFactor);

            case UWCursors.CursorEnum.SlightRight:
                transform.Rotate(0f, mfSlightTurnSpeed * mGameUi.CursorRotateFactor * Time.deltaTime, 0f);
                return new Vector3(0f, 0f, mfOriginalForwardSpeed * mGameUi.CursorWalkFactor);

            default:
                IsCursorMovementInProgress = false;
                return Vector3.zero;
        }
    }

    /// <summary>
    /// Keyboard fallback in the original scheme while no cursor drag is active.
    ///
    /// W and S forward and back, A and D TURN, sideways is on the two keys
    /// next to X - that is closer to the original (per user, 2026-09-04). The x axis of
    /// Move stays unused here; it belongs to the modern scheme, where A and D move
    /// sideways.
    /// </summary>
    private Vector3 fGetOriginalKeyboardVelocity()
    {
        float lfTurn = fClassicTurn();

        if (lfTurn != 0f)
            transform.Rotate(0f, lfTurn * mfTurnSpeed * Time.deltaTime, 0f);

        float lfForward = fMoveInput().y;
        float lfStrafe = fClassicStrafe();

        float lfForwardSpeed = lfForward >= 0f ? mfOriginalForwardSpeed : mfOriginalBackwardSpeed;

        return new Vector3(lfStrafe * mfOriginalStrafeSpeed, 0f, lfForward * lfForwardSpeed);
    }

    // ------------------------------------------------------------------
    // The motion core (stage 3 of the motion rework, 2026-10-05)
    // ------------------------------------------------------------------

    /// <summary>
    /// THE PLAYER ON THE ENGINE-FREE CORE: since stage 3 of the motion rework his motion is
    /// UW.EXE's own (UWPlayerMotion on UWMotionCore) - the params block that persists between
    /// frames, the commands of CalculateMotionFromCommand, the acceleration by the motion
    /// weight, the states with their speeds (swimming three tenths, lava a half, levitation a
    /// tenth, flying seven tenths, slow fall two tenths), the ledge that holds a creeping
    /// player and lets a running one off, the jump of 0x263 with the height cuts, the fall
    /// damage through the Acrobat check, the slide along walls with the camera following. The
    /// CharacterController stays as the body other systems probe and as the trigger volumes'
    /// visitor, but it no longer moves: the transform is set from the params every frame.
    ///
    /// This component is the FACADE: it turns the input of both schemes into the one command
    /// a frame of the original takes (keyboard poll seg034_2F89_1DA, pointer scheme seg034_2F89_4E),
    /// feeds the frame's PIT ticks, writes the position and the camera yaw back, and acts on
    /// the frame's result - the damage, the landing sound. What the controller path did on its
    /// own - hover oscillation, knockback, slope keep, fall grace - has no part here; the old
    /// path stays switchable off for comparison.
    /// </summary>
    [Header("Motion core (stage 3 of the motion rework)")]
    [SerializeField]
    [Tooltip("The player's motion runs on the engine-free core of UW.EXE (UWPlayerMotion) instead of the CharacterController. Off: the controller path of before stage 3.")]
    private bool mbUseMotionCore = true;

    /// <summary>The engine-free motion, made when the level's world exists.</summary>
    public UWPlayerMotion Motion { get; private set; }

    /// <summary>Whether the frames run on the core right now.</summary>
    public bool UsesMotionCore => mbUseMotionCore && Motion != null;

    /// <summary>The world the motion was built on - a new level object gets a new motion.</summary>
    private UWProjectileWorld mOMotionWorld;

    /// <summary>The PIT ticks not yet handed to the frame.</summary>
    private float mfCoreTickRemainder;

    /// <summary>
    /// THE SIMULATION STEP (2026-10-06, per user after the first test: "the movements and the
    /// looking around are not as smooth any more"). The original runs one motion frame per
    /// rendered frame with the ticks that passed - 13 to 17 of them at its 15 to 20 frames a
    /// second. Doing the same at 60 or 144 frames a second gives 4 or 5 ticks a frame, an
    /// integer: every other frame moves and turns a fifth more than its neighbour, and the
    /// positions and the yaw are whole units on top. So the frame runs in STEPS OF THIS MANY
    /// TICKS, and the camera is drawn between the last two states (the creatures do the same
    /// with their pictures). 16 would be the original's own frame length at its usual pace,
    /// with the acceleration (0x60 a frame, ten frames to the run) and the turn (7.25 degrees a
    /// frame at the key) at the original's speeds - but the stop then felt late (per user,
    /// 2026-10-06: 'das Abbremsen ist schon deutlich weiter'): nine steps of decay plus up to a
    /// step until the release is seen plus a step of interpolation. 8 halves all three; the
    /// run is reached in 0.28 s. The mouse look is NOT stepped: its delta goes onto both states
    /// at once.
    /// </summary>
    [SerializeField]
    [Tooltip("Unused since the second stage of the precision mode: Smooth runs one frame per rendered frame with the frame's time in 1/256 ticks (FineTicks), Original the frame length of the Motion dialog's 'Original fps'. Kept for the self-check's stepping only.")]
    [Range(1, 32)]
    private int miCoreStepTicks = 4;

    /// <summary>The fine positions of the two interpolation states with their carried fraction
    /// (UWPlayerMotion.FineX), for a view even to a 1/256 fine unit.</summary>
    private float mfPrevFineX;

    private float mfPrevFineY;

    private float mfPrevFineZ;

    /// <summary>The state before the last step, for the interpolation.</summary>
    private int miPrevX;

    private int miPrevY;

    private int miPrevZ;

    private int miPrevYaw;

    /// <summary>The yaw the transform was last written with, so the look's own turning since
    /// then can be told from ours.</summary>
    private int miLastWrittenYaw;

    /// <summary>The position the transform was last written with: anyone else moving the
    /// transform (noclip's return, the debug jump, a cutscene) is noticed and the motion takes
    /// the new spot (per user, 2026-10-06: leaving noclip put the player back where he had
    /// left - the params still held that spot).</summary>
    private Vector3 mOLastWrittenPosition;

    private bool mbHasLastWrittenPosition;

    private int miLastCoreAbilities = -1;

    /// <summary>A jump pressed but not yet taken by a step (see fBuildCoreCommand).</summary>
    private UWPlayerMotion.Command mePendingJump = UWPlayerMotion.Command.None;

    [SerializeField]
    private int miDebugCoreSpeed;

    [SerializeField]
    private string msDebugCoreState;

    [SerializeField]
    private int miDebugCoreContact;

    [SerializeField]
    private int miDebugCoreZ;

    [SerializeField]
    private int miDebugCoreVz;

    private UWPlayerMotion fEnsureMotion()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader == null || lOLoader.UWDataImporter == null)
            return Motion;

        UWProjectileWorld lOWorld = UWProjectileWorld.Ensure(lOLoader);

        if (lOWorld == null)
            return Motion;

        if (Motion == null || mOMotionWorld != lOWorld)
        {
            mOMotionWorld = lOWorld;
            Motion = new UWPlayerMotion(lOWorld, lOLoader.UWDataImporter.CommonObjectProperties);
            fPlaceMotionFromTransform();
        }

        return Motion;
    }

    /// <summary>
    /// One frame on the core. False when the core is off or the level is not there yet - the
    /// controller path runs then. The frame's ticks are the elapsed PIT ticks, capped at 0x40
    /// as the original's loop caps them (a long frame does not become a long step); a held
    /// clock (deltaTime 0) runs nothing, and the easy step gives the world its 64 ticks on its
    /// own (fReportEasyMovementStep).
    /// </summary>
    private bool fUpdateOnCore()
    {
        if (!mbUseMotionCore)
            return false;

        UWPlayerMotion lOMotion = fEnsureMotion();

        if (lOMotion == null)
            return false;

        // Moved from outside since our last write: the motion takes the transform's spot.
        if (mbHasLastWrittenPosition && (transform.position - mOLastWrittenPosition).sqrMagnitude > 0.01f)
            fPlaceMotionFromTransform();

        // THE SWITCH (UWUserSettings.MotionSmooth, the Graphics menu). ORIGINAL runs the
        // original's frame of 16 ticks, step by step, the view interpolated between the steps -
        // UW.EXE at the fixed DOSBox cycles the user measures against (13-17 ticks a frame). Not one frame per rendered frame: the core drops the fraction below a fine unit
        // every call, so at the 1-5 ticks a frame of a fast machine the slow motions stall - the
        // DOSBox cycles=max water bug, which the user recognised (2026-10-06, the analysis in the
        // private notes). SMOOTH runs the precision mode (UWPlayerMotion.Precise: the fraction
        // carried, the per-call rules as rates per original frame) in small steps and
        // interpolates the view, with the fraction, so the view is even and the speeds exact.
        bool lbSmooth = UWUserSettings.MotionSmooth;
        // The Original's frame length is the player's choice (UWUserSettings.OriginalFrameTicks:
        // 4, 8, 12 or 16 ticks; the user matched 8 = 32 fps against DOSBox at 30000 cycles).
        int liStep = lbSmooth ? Mathf.Clamp(miCoreStepTicks, 1, UWPlayerMotion.MaxFrameTicks) : UWUserSettings.OriginalFrameTicks;

        lOMotion.Precise = lbSmooth;
        lOMotion.FineTicks = lbSmooth;
        lOMotion.RampFrameTicks = UWUserSettings.SmoothRampTicks;
        mfCoreTickRemainder += Time.deltaTime * PitTicksPerSecond;

        // A long frame does not become a long step: as the original caps its frame at 0x40
        // ticks, at most that many are simulated here and the rest is dropped.
        if (mfCoreTickRemainder > UWPlayerMotion.MaxFrameTicks + liStep)
            mfCoreTickRemainder = UWPlayerMotion.MaxFrameTicks + liStep;

        bool lbBlocked = mScheme != null && mScheme.IsWorldInputBlocked;

        if (lbBlocked)
        {
            IsCursorMovementInProgress = false;
            mbCursorDragStartedInViewport = false;
            mePendingJump = UWPlayerMotion.Command.None;
        }

        fSyncMotionFromCharacter(lOMotion);

        // THE LOOK'S TURNING SINCE OUR LAST WRITE (the modern scheme's mouse look rotates the
        // transform itself, the loader's heading too) goes onto both states at once - the mouse
        // must not wait for a step - while the simulated turn (the turn input, the deflection
        // follow) is stepped and interpolated like the position.
        int liLookDelta = (short)(fTransformYawToAngle() - miLastWrittenYaw);

        if (liLookDelta != 0)
        {
            lOMotion.CameraYaw = (lOMotion.CameraYaw + liLookDelta) & 0xFFFF;
            miPrevYaw = (miPrevYaw + liLookDelta) & 0xFFFF;
        }

        bool lbEasy = !lbBlocked && fUpdateEasyMovement();

        lOMotion.MotionCommand = UWPlayerMotion.Command.None;
        lOMotion.Walk = 0;
        lOMotion.TurnInput = 0;
        lOMotion.FreeOffset = 0;

        // The jump has the right of way over the easy movement: shift is both the easy
        // movement's key and the standing jump's (shift J), and with shift down the walk's
        // keys stay with the easy steps (per user, 2026-10-06: "shift+jump funktioniert nicht").
        if (!lbBlocked)
            fBuildCoreCommand(lOMotion, lbEasy);

        if (lbSmooth)
        {
            // SMOOTH, THE SECOND STAGE (FineTicks, 2026-10-06): ONE call with the time this frame
            // took, in 1/256 tick, and the current state drawn - no steps, no interpolation, no
            // lag. The remainder below a 1/256 tick carries over.
            int liFine = Mathf.FloorToInt(mfCoreTickRemainder * UWMotionParams.FineTicksPerTick);
            int liMostFine = UWPlayerMotion.MaxFrameTicks * UWMotionParams.FineTicksPerTick;

            if (liFine > liMostFine)
            {
                liFine = liMostFine;
                mfCoreTickRemainder = 0f;
            }
            else
                mfCoreTickRemainder -= liFine / (float)UWMotionParams.FineTicksPerTick;

            if (liFine > 0)
            {
                UWPlayerMotion.Command leHeld = lOMotion.MotionCommand;

                if (mePendingJump != UWPlayerMotion.Command.None)
                {
                    lOMotion.MotionCommand = mePendingJump;
                    mePendingJump = UWPlayerMotion.Command.None;
                }

                if (lOMotion.Frame(liFine))
                    fApplyCoreResult(lOMotion);

                lOMotion.MotionCommand = leHeld;
            }

            fSnapInterpolation(lOMotion);
            fApplyMotionToTransform(lOMotion);
        }
        else
        {
            while (liStep > 0 && mfCoreTickRemainder >= liStep)
            {
                mfCoreTickRemainder -= liStep;

                miPrevX = lOMotion.Params.X;
                miPrevY = lOMotion.Params.Y;
                miPrevZ = lOMotion.Params.Z;
                mfPrevFineX = lOMotion.FineX;
                mfPrevFineY = lOMotion.FineY;
                mfPrevFineZ = lOMotion.FineZ;
                miPrevYaw = lOMotion.CameraYaw;

                // The pending jump takes this one step in place of the walk, then the walk goes on.
                UWPlayerMotion.Command leHeld = lOMotion.MotionCommand;

                if (mePendingJump != UWPlayerMotion.Command.None)
                {
                    lOMotion.MotionCommand = mePendingJump;
                    mePendingJump = UWPlayerMotion.Command.None;
                }

                if (lOMotion.Frame(liStep))
                    fApplyCoreResult(lOMotion);

                lOMotion.MotionCommand = leHeld;
            }

            // THE PICTURE of Original (UWUserSettings.MotionViewStepped, the Motion dialog's row
            // "Picture"): Even draws the view between the steps at the monitor's rate; Stepped
            // sets it once per step - with 16 ticks the 15-20 pictures a second of the original
            // on DOSBox, which the user first called "ruckelt sehr stark" and then wanted as a
            // choice for everyone.
            if (UWUserSettings.MotionViewStepped)
            {
                fSnapInterpolation(lOMotion);
                fApplyMotionToTransform(lOMotion);
            }
            else
                fApplyMotionToTransform(lOMotion, mfCoreTickRemainder / liStep);
        }

        // The move triggers the steps met (UWProjectileWorld.TriggerMove): fired now, after the
        // frame, so that a teleport is not overwritten by the step.
        UWProjectileWorld lOTriggerWorld = UWScene.LevelLoader != null ? UWProjectileWorld.Ensure(UWScene.LevelLoader) : null;

        if (lOTriggerWorld != null)
            lOTriggerWorld.FirePendingPlayerTriggers();

        mfDebugInputSpeed = lOMotion.Params.Speed;
        miDebugCoreSpeed = lOMotion.Params.Speed;
        msDebugCoreState = lOMotion.CurrentState.ToString();
        miDebugCoreContact = lOMotion.Params.Contact;
        miDebugCoreZ = lOMotion.Params.Z;
        miDebugCoreVz = lOMotion.Params.Vz;

        fUpdateFootsteps();

        return true;
    }

    /// <summary>The character's values the frame reads: the abilities (a change re-applies the
    /// state, seg008_1B2A_D85), the Acrobat skill for the fall, the load for the motion weight.</summary>
    private void fSyncMotionFromCharacter(UWPlayerMotion pOMotion)
    {
        pOMotion.Abilities = MagicalMotionAbilities;

        if (MagicalMotionAbilities != miLastCoreAbilities)
        {
            if (miLastCoreAbilities >= 0)
                pOMotion.ReapplyState();

            miLastCoreAbilities = MagicalMotionAbilities;
        }

        UWCharacter lOCharacter = UWScene.Character;

        if (lOCharacter != null)
            pOMotion.AcrobatSkill = lOCharacter.GetSkill(UWPlayerData.Skill.Acrobat);

        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWDataImport.DataImport lOData = lOLoader != null ? lOLoader.UWDataImporter : null;
        UWInventory lOInventory = UWScene.Inventory;

        if (lOData != null && lOData.InitialPlayer != null && lOInventory != null)
        {
            pOMotion.MaxWeight = lOData.InitialPlayer.MaxWeight;
            pOMotion.CarriedWeight = Mathf.Max(0, lOInventory.GetCarriedTenthStones(lOData.CommonObjectProperties));
        }
    }

    /// <summary>
    /// THE ONE COMMAND OF THE FRAME, as the original's input gives it (MotionCommand_75A with
    /// Walk_763 and TurnInput_765): the jump first (J or Space, the right click of a cursor walk;
    /// with shift the standing jump, command 6), then the fly keys under Levitate or Fly (the
    /// original's E and Q, commands 0xC and 0xD), then the scheme's walk. The jump is not
    /// gated by water or the air here: CalculateMotionFromCommand takes it anywhere, and the
    /// states do the rest (a jump while levitating is a hop that the decay swallows).
    /// </summary>
    private void fBuildCoreCommand(UWPlayerMotion pOMotion, bool pbEasyHeld)
    {
        bool lbTyping = mGameUi != null && mGameUi.IsTextEntryActive;
        bool lbJumpClick = UWMouseButtons.RightPressed && ConsumesRightClick();

        // A JUMP IS A PRESS, AND A PRESS MUST WAIT FOR A STEP (2026-10-06, per user: "Springen
        // funktioniert nicht zuverlaessig"): with 16-tick steps only every fourth rendered frame
        // at 60 Hz runs a step, and a press in a frame without one was gone by the next. So the
        // jump is kept until the next step takes it (fUpdateOnCore), whatever the walk does.
        if (!lbTyping && (mInput.Jump.WasPressedThisFrame() || lbJumpClick || fPadJumps()))
        {
            bool lbShift = UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.shiftKey.isPressed;

            mePendingJump = lbShift ? UWPlayerMotion.Command.StandingJump : UWPlayerMotion.Command.Jump;
        }

        // Shift held: the walk keys belong to the easy movement this frame.
        if (pbEasyHeld)
            return;

        bool lbModern = mScheme != null && mScheme.Current == UWControlScheme.SchemeEnum.Modern;

        if ((MagicalMotionAbilities & (LevitateBit | FlyBit)) != 0)
        {
            float lfHover = lbModern ? mInput.ModernHover.ReadValue<float>() : mInput.HoverHeight.ReadValue<float>();

            if (lfHover > 0.01f)
            {
                pOMotion.MotionCommand = UWPlayerMotion.Command.FlyUp;

                return;
            }

            if (lfHover < -0.01f)
            {
                pOMotion.MotionCommand = UWPlayerMotion.Command.FlyDown;

                return;
            }
        }

        if (lbModern)
            fBuildModernCoreCommand(pOMotion);
        else
            fBuildOriginalCoreCommand(pOMotion);
    }

    /// <summary>
    /// WASD of the modern scheme: forward is the run (Walk 0x70, which gives 822 of the 940),
    /// back the original's back command (0xBC - a fifth of the forward speed, not the half the
    /// controller path had), the strafes the slides (0xEB, a quarter). A DIAGONAL has no
    /// command in the original; the port's Free command walks at the run along the stick's
    /// angle from the view (UWPlayerMotion.FreeOffset). The mouse look turns the transform
    /// itself (UWPlayerLook), the frame takes the yaw from there.
    /// </summary>
    /// <summary>
    /// THE GAMEPAD IN THE CLASSIC SCHEME (per user, 2026-10-07: only forward and back worked there),
    /// the usual two sticks as in the modern scheme: the RIGHT stick sideways TURNS, as A and D do
    /// there (up and down it tilts the view a step, UWPlayerLook) - the left one turning felt odd
    /// (per user, the same day).
    /// </summary>
    private float fClassicTurn()
    {
        // Not while the stick moves the gamepad's pointer (UWGamepadPointer).
        float lfStick = UWGamepadPointer.IsDriving ? 0f : mInput.LookStick.ReadValue<Vector2>().x;

        return Mathf.Clamp(mInput.Turn.ReadValue<float>() + lfStick, -1f, 1f);
    }

    /// <summary>The strafe keys plus the LEFT stick pushed well sideways - Move's x axis counts only
    /// while the stick drives it, so a key the player bound for the modern strafe does not slide
    /// here, and only past half, so walking forward a little askew does not slide either (the
    /// original's slide takes the place of the walk).</summary>
    private float fClassicStrafe()
    {
        float lfStick = UWGamepad.IsFromGamepad(mInput.Move) ? fMoveInput().x : 0f;

        return Mathf.Clamp(mInput.Strafe.ReadValue<float>() + (Mathf.Abs(lfStick) >= 0.5f ? Mathf.Sign(lfStick) : 0f), -1f, 1f);
    }

    /// <summary>Move's value - without the gamepad's stick while it turns the wheel over a window
    /// (UWGamepadPointer.StickScrolls); the keys still walk.</summary>
    private Vector2 fMoveInput()
    {
        if (UWGamepadPointer.StickScrolls && UWGamepad.IsFromGamepad(mInput.Move))
            return Vector2.zero;

        return mInput.Move.ReadValue<Vector2>();
    }

    /// <summary>The gamepad's Y jumps (per user, 2026-10-07 - B did while nothing was open to close).</summary>
    private bool fPadJumps()
    {
        return !UWControls.IsTextEntryActive && mInput.PadJump.WasPressedThisFrame();
    }

    /// <summary>The walk for a stick: the run scaled by its deflection (UWGamepad) - the keys and
    /// a full stick give the run itself.</summary>
    private static int fStickWalk(Vector2 pODirection)
    {
        return Mathf.Max(1, Mathf.RoundToInt(UWPlayerMotion.WalkRun * Mathf.Clamp01(pODirection.magnitude)));
    }

    private void fBuildModernCoreCommand(UWPlayerMotion pOMotion)
    {
        Vector2 lODirection = fMoveInput();

        if (lODirection.sqrMagnitude < 0.01f)
            return;

        bool lbSide = Mathf.Abs(lODirection.x) > 0.01f;
        bool lbAlong = Mathf.Abs(lODirection.y) > 0.01f;

        if (lbAlong && !lbSide)
        {
            pOMotion.MotionCommand = lODirection.y > 0f ? UWPlayerMotion.Command.Walk : UWPlayerMotion.Command.Back;
            pOMotion.Walk = fStickWalk(lODirection);

            return;
        }

        if (lbSide && !lbAlong)
        {
            pOMotion.MotionCommand = lODirection.x > 0f ? UWPlayerMotion.Command.SlideRight : UWPlayerMotion.Command.SlideLeft;

            return;
        }

        pOMotion.MotionCommand = UWPlayerMotion.Command.Free;
        pOMotion.Walk = fStickWalk(lODirection);
        pOMotion.FreeOffset = UWViewpoint.DegreesToAngle(Mathf.Atan2(lODirection.x, lODirection.y) * Mathf.Rad2Deg) & 0xFFFF;
    }

    /// <summary>
    /// The original scheme: with the left button held and the press begun in the viewport the
    /// pointer drives with the original's own values (UWPlayerMotion.PointerCommand, from the
    /// pointer's pixel in the 172 by 113 area, UWGameUI.CursorAreaX/Y; since 2026-10-06 - the
    /// port scaled the keys' 0x70 and 0x5A by the zone distance before). Both buttons outside
    /// the fight mode jump, on the run and again on every landing while held
    /// (UWPlayerMotion.PointerJumpCommand); in the fight mode they drive on. Without the pointer
    /// the keys: W and S, the turn keys, the strafe keys.
    /// </summary>
    /// <summary>The pointer scheme's walk and turn of the last driving frame - the jump with
    /// both buttons keeps them (seg034_2F89_4E does not clear them there).</summary>
    private int miPointerWalk;

    private int miPointerTurn;

    private void fBuildOriginalCoreCommand(UWPlayerMotion pOMotion)
    {
        if (mItemDrag == null)
            mItemDrag = GetComponentInChildren<UWItemDrag>();

        bool lbHeldDrag = mItemDrag != null && mItemDrag.IsHeldDrag;

        if (!lbHeldDrag && mGameUi != null && UWMouseButtons.LeftPressed && UnityEngine.InputSystem.Mouse.current != null)
            mbCursorDragStartedInViewport = mGameUi.IsScreenPositionInGameArea(UnityEngine.InputSystem.Mouse.current.position.ReadValue());

        IsCursorMovementInProgress = !lbHeldDrag && mGameUi != null && UWMouseButtons.LeftHeld && mbCursorDragStartedInViewport;

        if (IsCursorMovementInProgress)
        {
            int liButtons = 1 | (UWMouseButtons.RightHeld ? 2 : 0);

            if (UWPlayerMotion.PointerDrives(liButtons, fIsFightMode()))
            {
                UWPlayerMotion.Command leCommand;

                UWPlayerMotion.PointerCommand(mGameUi.CursorAreaX, mGameUi.CursorAreaY,
                    UWPlayerMotion.PointerAreaWidth, UWPlayerMotion.PointerAreaHeight,
                    out leCommand, out miPointerWalk, out miPointerTurn);

                pOMotion.MotionCommand = leCommand;
                pOMotion.Walk = miPointerWalk;
                pOMotion.TurnInput = miPointerTurn;

                return;
            }

            // Both buttons, no fight mode: the jump, with the last walk and turn carrying on.
            pOMotion.MotionCommand = pOMotion.PointerJumpCommand;
            pOMotion.Walk = miPointerWalk;
            pOMotion.TurnInput = miPointerTurn;

            return;
        }

        float lfTurn = fClassicTurn();
        float lfForward = fMoveInput().y;
        float lfStrafe = fClassicStrafe();

        if (Mathf.Abs(lfStrafe) > 0.01f)
        {
            pOMotion.MotionCommand = lfStrafe > 0f ? UWPlayerMotion.Command.SlideRight : UWPlayerMotion.Command.SlideLeft;

            return;
        }

        if (lfForward < -0.01f)
        {
            pOMotion.MotionCommand = UWPlayerMotion.Command.Back;

            return;
        }

        if (lfForward > 0.01f || Mathf.Abs(lfTurn) > 0.01f)
        {
            pOMotion.MotionCommand = UWPlayerMotion.Command.Walk;
            pOMotion.Walk = lfForward > 0.01f ? fStickWalk(new Vector2(0f, lfForward)) : 0;
            pOMotion.TurnInput = Mathf.RoundToInt(Mathf.Clamp(lfTurn, -1f, 1f) * UWPlayerMotion.TurnInputStep);
        }
    }

    /// <summary>What the frame's result asks of the game: the fall damage (the impact block of
    /// ApplyPlayerMotion, the value after the Acrobat check above three) and the landing sound
    /// at value * 4 - 60 - the splash over water, as before (fLandingSound).</summary>
    private void fApplyCoreResult(UWPlayerMotion pOMotion)
    {
        UWPlayerMotion.FrameResult lOResult = pOMotion.Last;

        if (lOResult.FallDamage > 0)
        {
            UWCharacter lOCharacter = UWScene.Character;

            if (lOCharacter != null)
                lOCharacter.ApplyDamage(lOResult.FallDamage, UWDamageTypes.Physical);
        }

        if (lOResult.LandingSound)
            UWSoundEffects.PlayAtAvatar(fLandingSound(), UWImpactRules.GetVolume(lOResult.ImpactValue));
    }

    /// <summary>The transform from the params: the feet at the fine position and height, the
    /// body a foot offset above them (as UWLevelLoader.fPlacePlayerOnFloor sets it), the yaw
    /// from the camera yaw - drawn pfAlpha of the way from the state before the last step to
    /// the current one (1 = the current state, as after a placement).</summary>
    private void fApplyMotionToTransform(UWPlayerMotion pOMotion, float pfAlpha = 1f)
    {
        pfAlpha = Mathf.Clamp01(pfAlpha);

        // The fine positions with their fraction (0 without the precision mode), as world points.
        Vector3 lOFeet = Vector3.Lerp(
            fFineToWorld(mfPrevFineX, mfPrevFineY, mfPrevFineZ),
            fFineToWorld(pOMotion.FineX, pOMotion.FineY, pOMotion.FineZ),
            pfAlpha);

        int liYaw = (miPrevYaw + Mathf.RoundToInt((short)(pOMotion.CameraYaw - miPrevYaw) * pfAlpha)) & 0xFFFF;

        transform.position = lOFeet + (Vector3.up * fFootOffset());
        transform.rotation = Quaternion.Euler(0f, UWViewpoint.AngleToDegrees(liYaw), 0f);
        miLastWrittenYaw = liYaw;
        mOLastWrittenPosition = transform.position;
        mbHasLastWrittenPosition = true;
    }

    /// <summary>Back from noclip (the player object was inactive and was put at the spectator
    /// camera's spot) or any other re-activation: the motion takes the transform's spot.</summary>
    private void OnEnable()
    {
        if (mbUseMotionCore && Motion != null)
            fPlaceMotionFromTransform();
    }

    /// <summary>Both interpolation states onto the current one: after a placement, a teleport,
    /// an easy step.</summary>
    private void fSnapInterpolation(UWPlayerMotion pOMotion)
    {
        miPrevX = pOMotion.Params.X;
        miPrevY = pOMotion.Params.Y;
        miPrevZ = pOMotion.Params.Z;
        mfPrevFineX = pOMotion.FineX;
        mfPrevFineY = pOMotion.FineY;
        mfPrevFineZ = pOMotion.FineZ;
        miPrevYaw = pOMotion.CameraYaw;
    }

    /// <summary>A fine position with its fraction as a world point (UWUnits' scale, the half tile
    /// of the axes, no rounding).</summary>
    private static Vector3 fFineToWorld(float pfX, float pfY, float pfZ)
    {
        return new Vector3(
            (pfX * UWWorldScale.UnitsPerOriginalUnit) - UWWorldScale.TileHalfSize,
            pfZ * UWWorldScale.UnitsPerOriginalUnit,
            (pfY * UWWorldScale.UnitsPerOriginalUnit) - UWWorldScale.TileHalfSize);
    }

    /// <summary>The feet's world height on the core: the params' fine z (with its fraction) as
    /// a world height - the floor the player stands or swims on, slope included.</summary>
    public float CoreFeetHeight => UsesMotionCore ? Motion.FineZ * UWWorldScale.UnitsPerOriginalUnit : transform.position.y - fFootOffset();

    /// <summary>How far the transform's origin sits above the feet - the controller's bottom,
    /// plus its skin.</summary>
    private float fFootOffset()
    {
        if (mController == null)
            return 0f;

        float lfBottom = mController.center.y - Mathf.Max(mController.height * 0.5f, mController.radius);

        return (-lfBottom * transform.lossyScale.y) + mController.skinWidth;
    }

    private int fTransformYawToAngle()
    {
        return UWViewpoint.DegreesToAngle(Mathf.Repeat(transform.eulerAngles.y, 360f)) & 0xFFFF;
    }

    /// <summary>
    /// The loader placed the transform (a level entry, a loaded save, a teleport): the motion
    /// takes the spot - PlaceAt with the feet's fine position and height and the transform's
    /// yaw, the reset of PlacePlayerInTile_seg008_6D1 - and the transform snaps onto the params.
    /// </summary>
    public void PlaceFromTransform()
    {
        if (!mbUseMotionCore || fEnsureMotion() == null)
            return;

        fPlaceMotionFromTransform();
    }

    private void fPlaceMotionFromTransform()
    {
        if (Motion == null)
            return;

        Vector3 lOFeet = transform.position - (Vector3.up * fFootOffset());
        int liX = Mathf.Clamp(UWViewpoint.WorldToOriginalX(lOFeet.x), 0, 0x3FFF);
        int liY = Mathf.Clamp(UWViewpoint.WorldToOriginalY(lOFeet.z), 0, 0x3FFF);
        int liZ = Mathf.Clamp(UWViewpoint.WorldToOriginalZ(lOFeet.y), 0, 0x3FF);

        Motion.PlaceAt(liX, liY, liZ, fTransformYawToAngle());
        mfCoreTickRemainder = 0f;
        fSnapInterpolation(Motion);
        fApplyMotionToTransform(Motion);
    }

    /// <summary>On the ground: the core's contact state, or the controller's contact.</summary>
    private bool fIsOnGround()
    {
        if (UsesMotionCore)
            return !Motion.IsAirborne;

        return mController != null && mController.isGrounded;
    }
}
