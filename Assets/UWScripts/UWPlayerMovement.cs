using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The player's movement: the facade over the engine-free motion core (UWPlayerMotion, since the
/// motion rework of 2026-10-05/06). It has the input of both schemes turned into the original's
/// frame command (UWPlayerInput), runs the frames (UWMotionStepper), writes the position and the
/// yaw back to the transform and plays the footsteps. The input and the stepper are its own parts
/// since cleanup step 4.7 (2026-10-18), driven from its one Update.
///
/// Replaces the 2014 combination of CharacterMotorC (a generic, 650-line physics motor
/// with slope curves, moving platforms and jump physics - none of which occurs in a
/// dungeon with fixed tile heights) and FPSInputControllerC. Both ran independently of
/// each other every frame and wrote the same value (motor.inputMoveDirection); depending
/// on script execution order, one script could overwrite the other, which could make the
/// original controls unusable.
/// Here there is exactly one movement per frame from exactly one source. The
/// CharacterController's own movement path, which the core replaced, went in cleanup step 4.1
/// (2026-10-18); the controller stays as the body other systems probe.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DefaultExecutionOrder(UWUpdateOrder.Movement)]
public class UWPlayerMovement : MonoBehaviour, IUWMotionStepTarget
{
    // Original and Modern feel differently fast at the same numeric value -
    // Original moves in fixed steps per cursor zone, Modern glides freely with WASD.
    // Hence separately adjustable instead of one shared pair of values.
    // Only the two forward speeds are left since cleanup 4.1 (2026-10-18): the head bob, the
    // weapon's jitter and the footsteps measure against them (NormalForwardSpeed); the motion
    // core walks at the original's own speeds.

    /// <summary>Measured on the original: ten tiles in 3.15 seconds, i.e. 203 units per
    /// second at a tile width of 64 units (per user, 2026-09-04, DOSBox Staging at a
    /// fixed 30000 cycles). Before that this was 230 - about 13 percent too fast, because
    /// it had been tuned against the original at cycles=max.</summary>
    [Header("Original scheme: speed in units per second (a tile is 64 wide)")]
    [SerializeField]
    private float mfOriginalForwardSpeed = 203f;

    [Header("Modern scheme: speed in units per second")]
    [SerializeField]
    private float mfModernForwardSpeed = 200f;

    private CharacterController mController;
    private UWControlScheme mScheme;

    /// <summary>The input of both schemes turned into the frame's command (UWPlayerInput, its own
    /// class since cleanup step 4.7, 2026-10-18).</summary>
    private UWPlayerInput mOInput;

    /// <summary>The player's input: the cursor walk, the easy movement's repeat, the jump click.</summary>
    public UWPlayerInput Input => mOInput;

    /// <summary>
    /// The active motion spells as a bit field, set by UWCharacter. See the
    /// constants there.
    /// </summary>
    public int MagicalMotionAbilities { get; set; }

    /// <summary>What the movement sees - read-only in the Inspector. Added 2026-09-24 for the slow
    /// walk after Tybal's death cutscene (per user; Todo "SLOW AFTER TYBAL'S DEATH"); the hover and
    /// speed factor fields of the controller path went with it in cleanup 4.1 (2026-10-18).</summary>
    [Header("Read only")]
    [SerializeField]
    private int miDebugMagicalMotionAbilities;

    [SerializeField]
    private bool mbDebugWorldInputBlocked;

    [SerializeField]
    private bool mbDebugGrounded;

    private void fUpdateDebugFields()
    {
        miDebugMagicalMotionAbilities = MagicalMotionAbilities;
        mbDebugWorldInputBlocked = mScheme != null && mScheme.IsWorldInputBlocked;
        mbDebugGrounded = fIsOnGround();
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
        if (piIntensity <= 0 || Motion == null)
            return;

        Motion.Bounce(piIntensity);
    }

    /// <summary>
    /// LEVITATE AND FLY GIVE A SMALL HOP when cast (do_spell calls phys_bounce_up for class 1
    /// minors 3 and 5, UnderworldExhumed SPELLS.C and PHYSICS.C, read 2026-10-12): on the ground
    /// the vertical speed becomes 0x8D, gravity off in any case - UWPlayerMotion.StopFalling,
    /// the same routine. That is the lift-off of a CAST spell while standing (per user in the
    /// original, 2026-09-03); a ring does not come here - it lets one walk normally and only
    /// carries once the ground is gone (per user, 2026-09-09). Without a world there is no
    /// motion and nothing to hop.
    /// </summary>
    public void SpellHop()
    {
        if (Motion != null)
            Motion.StopFalling();
    }

    /// <summary>In water: the core's contact state is water (2) - the swim state, whatever the
    /// height on a water slope; the water-edge state 0x20 is not (UWPlayerTerrain's view and sound
    /// read it). READ FROM THE CORE since cleanup step 4.7 (2026-10-18): until then UWPlayerTerrain
    /// copied the contact into IsInLiquid and IsOnLava here in its Update, before or after the
    /// frame's steps, so the landing's splash saw the contact of before the landing.</summary>
    public bool IsInWater => Motion != null && Motion.Params.Contact == UWMotionTables.ContactWater;

    /// <summary>In liquid, water or lava: the head bob, the landing's splash and the footsteps
    /// read it.</summary>
    public bool IsInLiquid => IsInWater || IsOnLava;

    /// <summary>Standing on lava: the core's contact state is lava (4). Lava counts as liquid, but
    /// it is ground for the head bob and lands with the ordinary sound - the original lets the
    /// player jump onto and off lava (per user, 2026-09-14).</summary>
    public bool IsOnLava => Motion != null && Motion.Params.Contact == UWMotionTables.ContactLava;

    /// <summary>Is the character currently moving horizontally? For creature
    /// perception: whoever stands still makes no noise (see UWCritter).</summary>
    public bool IsMoving => Motion != null && Motion.Params.Speed > 0;

    /// <summary>The distance actually travelled: the footsteps, the momentum, the measured speed
    /// (UWPlayerFootsteps, its own part since cleanup step 4.7, 2026-10-18).</summary>
    private readonly UWPlayerFootsteps mOFootsteps = new UWPlayerFootsteps();

    /// <summary>How fast the character really moved, smoothed - the head bob and the weapon's
    /// jitter read it (UWPlayerFootsteps.SmoothedHorizontalSpeed).</summary>
    public float SmoothedHorizontalSpeed => mOFootsteps.SmoothedHorizontalSpeed;

    /// <summary>How much of the full forward speed is actually being made, 0 to 1 - the noise
    /// reads it (UWPlayerFootsteps.MomentumFraction).</summary>
    public float MomentumFraction => mOFootsteps.MomentumFraction;

    /// <summary>The full forward speed of the current scheme with the load and terrain factor
    /// - what the head bob measures the current speed against.</summary>
    public float FullForwardSpeed => Motion != null
        ? NormalForwardSpeed * Motion.ForwardSpeed / UWPlayerMotion.BaseForwardSpeed
        : NormalForwardSpeed;

    /// <summary>The forward speed of the current scheme on normal ground - the original's base
    /// speed of 0x3AC, which the weapon jitter's thresholds are counted against.</summary>
    public float NormalForwardSpeed =>
        mScheme != null && UWControlScheme.IsModern ? mfModernForwardSpeed : mfOriginalForwardSpeed;

    /// <summary>Measures the speed after the frame's move (UWPlayerFootsteps.MeasureSpeed).</summary>
    private void LateUpdate()
    {
        mOFootsteps.MeasureSpeed(transform.position, NormalForwardSpeed);
    }

    /// <summary>On the ground and not hovering or swimming - the head bob runs only then, as
    /// the original skips it while the player is off the ground. LAVA IS GROUND: one walks on
    /// it, and the original's bob (PlayerMotion_seg034_2F89_604) asks only for the off-ground
    /// bit; until 2026-10-02 every liquid tile stopped the bob and the weapon's jitter (per
    /// user: "on lava the head bob is missing").</summary>
    public bool IsWalkingOnGround => fIsOnGround() && !fIsHovering()
        && (!IsInLiquid || IsOnLava);

    /// <summary>Floating in the air with Levitate or Fly - the original's motion_state bit 0x08
    /// (PHYSICS.C parse_player_terr), which makes the player louder (UWCharacter.IsInMotionState).
    /// </summary>
    public bool IsHovering => fIsHovering();

    private void Awake()
    {
        mController = GetComponent<CharacterController>();
        mScheme = GetComponent<UWControlScheme>();
        mOInput = new UWPlayerInput(this, mScheme);
    }

    private void Start()
    {
        mOInput.Bind(GetComponentInChildren<UWGameUI>(), mScheme.Controls.Player);
    }

    private void Update()
    {
        fUpdateDebugFields();

        UWPlayerMotion lOMotion = fEnsureMotion();

        // NO WORLD, NO MOTION: the main menu at launch and the frames before the loader's Start
        // have no floor to stand on, so the player stands still - no falling, no footsteps
        // (cleanup 4.1, 2026-10-18). Until then the CharacterController path ran there, its
        // gravity held only by the stopped game clock.
        if (lOMotion == null)
        {
            fStandStill();

            return;
        }

        fUpdateOnCore(lOMotion);
    }

    /// <summary>Without a world: no cursor walk, no pending jump, no momentum.</summary>
    private void fStandStill()
    {
        mOInput.EndCursorWalk();
        mOStepper.DropPendingJump();
        mOFootsteps.Stand(transform.position);
    }

    /// <summary>
    /// One command of the easy movement, from the keys or from the three arrows under the
    /// compass (UWHudCompass). A turn snaps onto the 45 degree line when the view is not on
    /// one; a step carries half a tile forward or a quarter back, and does nothing at all when
    /// something is in the way - the original's CheckIfItemFitsInTile is likewise all or
    /// nothing.
    ///
    /// It is the original's step since 2026-10-06 (UWPlayerMotion.EasyStep: the target,
    /// CheckIfItemFitsInTile, the floor-kind rule, the drop) and its gate
    /// (UWPlayerMotion.MayEasyMove). The CharacterController path's sweep of the capsule along
    /// the step went in cleanup 4.1 (2026-10-18).
    /// </summary>
    public bool TryEasyMovement(int piCommand)
    {
        if (mController == null || !mController.enabled)
            return false;

        if (mScheme != null && mScheme.IsWorldInputBlocked)
            return false;

        // The original's gate: no turn and no step while gravity acts or the speed has reached
        // the motion weight (UWPlayerMotion.MayEasyMove). Without a world nothing moves.
        if (Motion == null || !Motion.MayEasyMove)
            return false;

        if (UWEasyMovement.IsTurn(piCommand))
        {
            int liYaw = UWHeadings.DegreesToAngleUnits(transform.eulerAngles.y);

            liYaw = UWEasyMovement.Turn(liYaw, piCommand);

            Vector3 lOAngles = transform.eulerAngles;

            lOAngles.y = liYaw * 360f / UWEasyMovement.Circle;
            transform.eulerAngles = lOAngles;

            Motion.CameraYaw = liYaw & 0xFFFF;
            Motion.MotionYaw = Motion.CameraYaw;
            fSnapInterpolation(Motion);
            miLastWrittenYaw = Motion.CameraYaw;
            Motion.EndEasyTurn();

            fReportEasyMovementStep();

            return true;
        }

        int liDistance = UWEasyMovement.GetStepDistance(piCommand);

        if (liDistance <= 0)
            return false;

        // The original's step on the params; the transform follows.
        if (!Motion.EasyStep(piCommand, fTransformYawToAngle()))
            return false;

        fSnapInterpolation(Motion);
        fApplyMotionToTransform(Motion);
        fReportEasyMovementStep();

        return true;
    }

    /// <summary>The noise and the clock of one step - both are the character's business -
    /// and the world's 64 ticks of it (seg034_2F89_334: every step and turn gives the mobile
    /// objects four units, halved under Speed; UWCritterDriver.StepWorld, since 2026-10-05).</summary>
    private void fReportEasyMovementStep()
    {
        UWCharacter lOCharacter = UWScene.Character;

        if (lOCharacter != null)
            lOCharacter.Vitals.ReportEasyMovementStep();

        if (UWCritterDriver.Instance != null)
            UWCritterDriver.Instance.StepWorld();
    }

    /// <summary>
    /// Landing in water SPLASHES instead of thudding: effect 5, the same splash a thrown
    /// object makes (UWMotionHost.Splash) - not effect 0, which is the rushing one hears
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
        return Motion != null && (Motion.CurrentState == UWPlayerMotion.State.Levitating
            || Motion.CurrentState == UWPlayerMotion.State.Flying);
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
    /// This component is the FACADE: it has the input of both schemes turned into the one command
    /// a frame of the original takes (keyboard poll seg034_2F89_1DA, pointer scheme seg034_2F89_4E;
    /// UWPlayerInput since cleanup 4.7), feeds the frame's PIT ticks (UWMotionStepper), writes the
    /// position and the camera yaw back, and acts on
    /// the frame's result - the damage, the landing sound. What the controller path did on its
    /// own - hover oscillation, knockback, slope keep, fall grace - has no part here; that path
    /// went in cleanup 4.1 (2026-10-18).
    ///
    /// The motion is made when the level's world exists.
    /// </summary>
    public UWPlayerMotion Motion { get; private set; }

    /// <summary>Whether a motion exists - false only without a world (UWLevelLoader.HasWorld):
    /// since cleanup 4.1 (2026-10-18) the core is the one path, the Inspector switch to the
    /// CharacterController path is gone.</summary>
    public bool UsesMotionCore => Motion != null;

    /// <summary>The world the motion was built on - a new level object gets a new motion.</summary>
    private UWMotionHost mOMotionWorld;

    /// <summary>How many motion frames a rendered frame runs, the tick remainder and the pending
    /// jump (UWMotionStepper, engine-free since cleanup step 4.7, 2026-10-18).</summary>
    private readonly UWMotionStepper mOStepper = new UWMotionStepper();

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
    [Header("Motion core (stage 3 of the motion rework)")]
    [SerializeField]
    [Tooltip("Without effect since the second stage of the precision mode: Smooth runs one frame per rendered frame with the frame's time in 1/256 ticks (FineTicks), cut at MaxFrameTicks whatever this says; Original takes the frame length of the Motion dialog's 'Original fps' (UWMotionStepper).")]
    [Range(1, 32)]
    private int miCoreStepTicks = 4;

    /// <summary>The fine positions of the two interpolation states with their carried fraction
    /// (UWPlayerMotion.FineX), for a view even to a 1/256 fine unit.</summary>
    private float mfPrevFineX;

    private float mfPrevFineY;

    private float mfPrevFineZ;

    /// <summary>The yaw before the last step, for the interpolation.</summary>
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

        if (lOLoader == null || lOLoader.UWDataImporter == null || !lOLoader.HasWorld)
            return Motion;

        UWMotionHost lOWorld = UWMotionHost.Ensure(lOLoader);

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
    /// One frame on the core. The frame's ticks are the elapsed PIT ticks, capped at 0x40
    /// as the original's loop caps them (a long frame does not become a long step); a held
    /// clock (deltaTime 0) runs nothing, and the easy step gives the world its 64 ticks on its
    /// own (fReportEasyMovementStep).
    /// </summary>
    private void fUpdateOnCore(UWPlayerMotion lOMotion)
    {
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
        int liStep = UWMotionStepper.StepTicks(lbSmooth, miCoreStepTicks, UWUserSettings.OriginalFrameTicks);

        lOMotion.Precise = lbSmooth;
        lOMotion.FineTicks = lbSmooth;
        lOMotion.RampFrameTicks = UWUserSettings.SmoothRampTicks;

        // A long frame does not become a long step: as the original caps its frame at 0x40
        // ticks, at most that many are simulated here and the rest is dropped (UWMotionStepper).
        mOStepper.AddTime(Time.deltaTime, liStep);

        bool lbBlocked = mScheme != null && mScheme.IsWorldInputBlocked;

        // While a modal window is open - conversation, full-screen map - the character
        // stands still. Otherwise, in the original scheme, the pointer position keeps
        // driving the movement although the pointer is needed to operate the window
        // (per user, 2026-09-03). GRAVITY STILL RUNS: the frames go on without a command, so
        // the character does not hang in the air while a window holds the controls (per user,
        // 2026-09-11, then about the CharacterController path).
        //
        // A cursor walk ends here too, otherwise the clamp of the pointer to the viewport
        // (UWPointerZones) outlived a conversation an NPC started while the left
        // button was down, and the pointer could not reach the parchment (per user,
        // 2026-09-19, with the goal-10 outcast).
        if (lbBlocked)
        {
            mOInput.EndCursorWalk();
            mOStepper.DropPendingJump();
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

        bool lbEasy = !lbBlocked && mOInput.ReadEasyMovement();

        lOMotion.MotionCommand = UWPlayerMotion.Command.None;
        lOMotion.Walk = 0;
        lOMotion.TurnInput = 0;
        lOMotion.FreeOffset = 0;

        // The jump has the right of way over the easy movement: shift is both the easy
        // movement's key and the standing jump's (shift J), and with shift down the walk's
        // keys stay with the easy steps (per user, 2026-10-06: "shift+jump funktioniert nicht").
        if (!lbBlocked)
            mOInput.BuildCommand(lOMotion, lbEasy, mOStepper);

        // THE STEPS (UWMotionStepper): Smooth one frame with the time this frame took, Original
        // whole steps of its frame length, the pending jump in place of the walk for one frame.
        mOStepper.Run(lbSmooth, liStep, lOMotion.MotionCommand, this);

        if (lbSmooth)
        {
            // SMOOTH, THE SECOND STAGE (FineTicks, 2026-10-06): ONE call with the time this frame
            // took, in 1/256 tick, and the current state drawn - no steps, no interpolation, no
            // lag. The remainder below a 1/256 tick carries over.
            fSnapInterpolation(lOMotion);
            fApplyMotionToTransform(lOMotion);
        }
        else
        {
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
                fApplyMotionToTransform(lOMotion, mOStepper.Alpha(liStep));
        }

        // The move triggers the steps met (UWMotionHost.TriggerMove): fired now, after the
        // frame, so that a teleport is not overwritten by the step.
        UWMotionHost lOTriggerWorld = UWScene.LevelLoader != null ? UWMotionHost.Ensure(UWScene.LevelLoader) : null;

        if (lOTriggerWorld != null)
            lOTriggerWorld.FirePendingPlayerTriggers();

        miDebugCoreSpeed = lOMotion.Params.Speed;
        msDebugCoreState = lOMotion.CurrentState.ToString();
        miDebugCoreContact = lOMotion.Params.Contact;
        miDebugCoreZ = lOMotion.Params.Z;
        miDebugCoreVz = lOMotion.Params.Vz;

        fUpdateFootsteps();
    }

    /// <summary>The frame's footsteps (UWPlayerFootsteps.Step): none without a world, in water,
    /// in the air or hovering.</summary>
    private void fUpdateFootsteps()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        bool lbOnFoot = !(mController == null || IsInLiquid || fIsHovering() || !fIsOnGround()
            || (lOLoader != null && !lOLoader.HasWorld));

        mOFootsteps.Step(transform.position, Mathf.Max(mfModernForwardSpeed, mfOriginalForwardSpeed), lbOnFoot);
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
            pOMotion.AcrobatSkill = lOCharacter.Vitals.GetSkill(UWPlayerData.Skill.Acrobat);

        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWDataImport.DataImport lOData = lOLoader != null ? lOLoader.UWDataImporter : null;
        UWInventory lOInventory = UWScene.Inventory;

        if (lOData != null && lOData.InitialPlayer != null && lOInventory != null)
        {
            pOMotion.MaxWeight = lOData.InitialPlayer.MaxWeight;
            pOMotion.CarriedWeight = Mathf.Max(0, lOInventory.GetCarriedTenthStones(lOData.CommonObjectProperties));
        }
    }

    /// <summary>Original mode, before each whole step: the state the view is drawn from.</summary>
    void IUWMotionStepTarget.BeforeStep()
    {
        mfPrevFineX = Motion.FineX;
        mfPrevFineY = Motion.FineY;
        mfPrevFineZ = Motion.FineZ;
        miPrevYaw = Motion.CameraYaw;
    }

    /// <summary>One frame of the motion under the stepper's command (the held walk, or the pending
    /// jump in its place), its result acted on; the held command stands again afterwards.</summary>
    void IUWMotionStepTarget.RunFrame(int piDt, UWPlayerMotion.Command peCommand)
    {
        UWPlayerMotion.Command leHeld = Motion.MotionCommand;

        Motion.MotionCommand = peCommand;

        if (Motion.Frame(piDt))
            fApplyCoreResult(Motion);

        Motion.MotionCommand = leHeld;
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
                lOCharacter.Damage(UWDamageRequest.Fall(lOResult.FallDamage));
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

        transform.position = lOFeet + (Vector3.up * FootOffset);
        transform.rotation = Quaternion.Euler(0f, UWViewpoint.AngleToDegrees(liYaw), 0f);
        miLastWrittenYaw = liYaw;
        mOLastWrittenPosition = transform.position;
        mbHasLastWrittenPosition = true;
    }

    /// <summary>Back from noclip (the player object was inactive and was put at the spectator
    /// camera's spot) or any other re-activation: the motion takes the transform's spot.</summary>
    private void OnEnable()
    {
        if (Motion != null)
            fPlaceMotionFromTransform();
    }

    /// <summary>Both interpolation states onto the current one: after a placement, a teleport,
    /// an easy step.</summary>
    private void fSnapInterpolation(UWPlayerMotion pOMotion)
    {
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
    public float CoreFeetHeight => Motion != null ? Motion.FineZ * UWWorldScale.UnitsPerOriginalUnit : transform.position.y - FootOffset;

    /// <summary>The feet in zpos steps: the params' height over eight, what phys_affect_player
    /// writes into the player object (UnderworldExhumed PHYSICS.C:401, PN.z >> 3) - the height
    /// the rules that count on the tile grid measure from (UWCharacter.GetFeetZPos, since cleanup
    /// 4.1, 2026-10-18). Without a world the transform's feet.</summary>
    public int FeetZPos => Motion != null ? Motion.FeetZPos : UWUnits.WorldToZPos(transform.position.y - FootOffset);

    /// <summary>How far the transform's origin sits above the feet - the controller's bottom,
    /// plus its skin. THE ONE HOME since cleanup 4.1 (2026-10-18): the motion's write-back, its
    /// placement and the loader's placement on the floor (UWLevelLoader.fPlacePlayerOnFloor, which
    /// had a copy) all use it.</summary>
    public float FootOffset
    {
        get
        {
            CharacterController lOController = mController != null ? mController : GetComponent<CharacterController>();

            if (lOController == null)
                return 0f;

            float lfBottom = lOController.center.y - Mathf.Max(lOController.height * 0.5f, lOController.radius);

            return (-lfBottom * transform.lossyScale.y) + lOController.skinWidth;
        }
    }

    /// <summary>
    /// The transform's yaw in the original's units, ROUNDED (since 2026-10-10): the transform
    /// gives back an angle written as 45 degrees as 44.99998, and the truncating
    /// UWViewpoint.DegreesToAngle made that 8191 instead of 8192 - the look's delta then pulled
    /// the camera yaw one unit below the eighth, and the easy turn to the right (which snaps up
    /// to the next eighth) landed on the same eighth again: the compass disc's right arrow did
    /// nothing after a few clicks until the pointer was locked and freed (per user).
    /// </summary>
    private int fTransformYawToAngle()
    {
        return UWHeadings.DegreesToAngleUnits(transform.eulerAngles.y);
    }

    /// <summary>
    /// The loader placed the transform (a level entry, a loaded save, a teleport): the motion
    /// takes the spot - PlaceAt with the feet's fine position and height and the transform's
    /// yaw, the reset of PlacePlayerInTile_seg008_6D1 - and the transform snaps onto the params.
    /// </summary>
    public void PlaceFromTransform()
    {
        if (fEnsureMotion() == null)
            return;

        fPlaceMotionFromTransform();
    }

    private void fPlaceMotionFromTransform()
    {
        if (Motion == null)
            return;

        Vector3 lOFeet = transform.position - (Vector3.up * FootOffset);
        int liX = Mathf.Clamp(UWViewpoint.WorldToOriginalX(lOFeet.x), 0, 0x3FFF);
        int liY = Mathf.Clamp(UWViewpoint.WorldToOriginalY(lOFeet.z), 0, 0x3FFF);
        int liZ = Mathf.Clamp(UWViewpoint.WorldToOriginalZ(lOFeet.y), 0, 0x3FF);

        Motion.PlaceAt(liX, liY, liZ, fTransformYawToAngle());
        mOStepper.ResetTime();
        fSnapInterpolation(Motion);
        fApplyMotionToTransform(Motion);
    }

    /// <summary>On the ground: the core's contact state. Without a world the player stands.
    /// </summary>
    private bool fIsOnGround()
    {
        return Motion == null || !Motion.IsAirborne;
    }
}
