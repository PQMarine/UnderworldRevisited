using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE PLAYER'S INPUT, turned into the one command a frame of the original takes (the keyboard
/// poll seg034_2F89_1DA, the pointer scheme seg034_2F89_4E): the walk of both schemes and the pad,
/// the fly keys, the jump (handed to the stepper, which keeps it for the next step), the cursor
/// walk of the original scheme with its right-click jump, and the easy movement's keys and repeat.
/// Out of UWPlayerMovement in cleanup step 4.7 (2026-10-18); the movement owns it and asks it once
/// per frame, in its own Update, so the input and the motion it drives cannot run in either order.
/// The movement keeps what the input asks for: the easy step itself (TryEasyMovement) and the
/// motion abilities.
/// </summary>
public sealed class UWPlayerInput
{
    private readonly UWPlayerMovement mOHost;
    private readonly UWControlScheme mScheme;
    private UWGameUI mGameUi;
    private UWItemDrag mItemDrag;
    private UWControls.PlayerActions mInput;

    public UWPlayerInput(UWPlayerMovement pOHost, UWControlScheme pOScheme)
    {
        mOHost = pOHost;
        mScheme = pOScheme;
        msInstance = this;
    }

    /// <summary>The movement's Start: the game UI and the controls exist then.</summary>
    public void Bind(UWGameUI pOGameUi, UWControls.PlayerActions pOInput)
    {
        mGameUi = pOGameUi;
        mInput = pOInput;
    }

    /// <summary>
    /// Original scheme: is a cursor movement in progress? UWGameUI needs this so that the
    /// brief exit from the tiny game window while dragging, typical for the original, does
    /// not immediately snap back to the centre position.
    /// </summary>
    public bool IsCursorMovementInProgress { get; private set; }

    /// <summary>Did the current left-button press begin inside the 3D viewport?</summary>
    private bool mbCursorDragStartedInViewport;

    /// <summary>Without a world, or while a modal window holds the controls: no cursor walk.</summary>
    public void EndCursorWalk()
    {
        IsCursorMovementInProgress = false;
        mbCursorDragStartedInViewport = false;
    }

    private static UWPlayerInput msInstance;

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
        if (!UWPointer.HasMouse)
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
        return mScheme != null && !UWControlScheme.IsModern
            && mGameUi != null && UWMouseButtons.LeftHeld && mbCursorDragStartedInViewport;
    }

    /// <summary>
    /// THE EASY MOVEMENT on the keyboard: shift with W, S, A, D and X (UWEasyMovement holds
    /// the rule and where it comes from; since decision 18, 2026-10-25, the four are whatever keys
    /// the walk and the turn lie on, UWKeyBindings.ApplyEasySteps) - in the modern scheme its own
    /// modifier instead (UWControls.EasyModifier, Left Alt), Shift walks slowly there. Returns whether the
    /// modifier claims the movement keys this frame - then the ordinary walking stays still,
    /// otherwise one would step and walk at once.
    /// </summary>
    public bool ReadEasyMovement()
    {
        UnityEngine.InputSystem.Keyboard lOKeyboard = UnityEngine.InputSystem.Keyboard.current;
        bool lbModifier = UWControlScheme.IsModern
            ? mInput.EasyModifier.IsPressed()
            : lOKeyboard != null && lOKeyboard.shiftKey.isPressed;

        if (lOKeyboard == null || !lbModifier)
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
    /// keys and by Interaction for the three arrows. The repeat is UWRepeat's since cleanup step
    /// 7.3 (2026-10-25): the interval as the first delay and between the steps, each counted from
    /// the step before, on the game's clock (Time.time, as before).
    /// </summary>
    public void HoldEasyMovement(int piCommand)
    {
        miEasyHoldFrame = Time.frameCount;

        float lfInterval = UWUserSettings.EasyMovementInterval;

        mOEasyRepeat.Delay = lfInterval;
        mOEasyRepeat.Every = lfInterval;

        // The commands run from -2 to 2: shifted, so that none is the repeat's "nothing held".
        if (mOEasyRepeat.Hold(piCommand + EasyCommandShift, Time.time))
            mOHost.TryEasyMovement(piCommand);
    }

    /// <summary>Lets go of the repeat unless someone asked for it in this frame or the one
    /// before - Interaction and the movement update in an order that is not fixed. (Which caller
    /// holds the step is told by the frame; the repeat's timing is the clock's.)</summary>
    private void fReleaseEasyMovementIfIdle()
    {
        if (mOEasyRepeat.Held != 0 && miEasyHoldFrame < Time.frameCount - 1)
            mOEasyRepeat.Reset();
    }

    private const int EasyCommandShift = 10;

    private readonly UWRepeat mOEasyRepeat = new UWRepeat(UWEasyMovement.DefaultRepeatSeconds, UWEasyMovement.DefaultRepeatSeconds,
        UWRepeat.RuleEnum.FromNow);

    private int miEasyHoldFrame = -2;

    /// <summary>The bits that matter here - the same values as in UWCharacter.</summary>
    private const int LevitateBit = UWPlayerVitals.LevitateBit;

    private const int FlyBit = UWPlayerVitals.FlyBit;

    /// <summary>
    /// THE ONE COMMAND OF THE FRAME, as the original's input gives it (MotionCommand_75A with
    /// Walk_763 and TurnInput_765): the jump first (J or Space, the right click of a cursor walk;
    /// with shift the standing jump, command 6 - TWO JUMPS as in the original, per user,
    /// 2026-09-13: "j jumps up and Shift + j jumps forward"), then the fly keys under Levitate or
    /// Fly (the original's E and Q, commands 0xC and 0xD), then the scheme's walk. The jump is not
    /// gated by water or the air here: CalculateMotionFromCommand takes it anywhere, and the
    /// states do the rest (a jump while levitating is a hop that the decay swallows).
    /// </summary>
    public void BuildCommand(UWPlayerMotion pOMotion, bool pbEasyHeld, UWMotionStepper pOStepper)
    {
        bool lbTyping = mGameUi != null && mGameUi.TextEntry.IsTextEntryActive;

        fReadWalkSlowToggle();
        bool lbJumpClick = UWMouseButtons.RightPressed && ConsumesRightClick();

        // A JUMP IS A PRESS, AND A PRESS MUST WAIT FOR A STEP (2026-10-06, per user: "Springen
        // funktioniert nicht zuverlaessig"): with 16-tick steps only every fourth rendered frame
        // at 60 Hz runs a step, and a press in a frame without one was gone by the next. So the
        // jump is kept until the next step takes it (UWMotionStepper), whatever the walk does.
        if (!lbTyping && (mInput.Jump.WasPressedThisFrame() || lbJumpClick || fPadJumps()))
        {
            bool lbShift = UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.shiftKey.isPressed;

            // In the modern scheme Shift is the slow walk (2026-10-21): a jump on the move is the
            // jump, the standing one only from a standstill - the standing jump does nothing while
            // the player moves (UWPlayerMotion.fCommand).
            if (lbShift && mScheme != null && UWControlScheme.IsModern && fMoveInput().sqrMagnitude >= 0.01f)
                lbShift = false;

            pOStepper.PressJump(lbShift ? UWPlayerMotion.Command.StandingJump : UWPlayerMotion.Command.Jump);
        }

        // Shift held: the walk keys belong to the easy movement this frame.
        if (pbEasyHeld)
            return;

        bool lbModern = mScheme != null && UWControlScheme.IsModern;

        if ((mOHost.MagicalMotionAbilities & (LevitateBit | FlyBit)) != 0)
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
    /// a full stick give the run itself, the modern slow walk no more than the original's S
    /// (UWPlayerMotion.WalkSlow).</summary>
    private int fStickWalk(Vector2 pODirection)
    {
        int liWalk = Mathf.Max(1, Mathf.RoundToInt(UWPlayerMotion.WalkRun * Mathf.Clamp01(pODirection.magnitude)));

        return fIsWalkingSlow() ? Mathf.Min(liWalk, UWPlayerMotion.WalkSlow) : liWalk;
    }

    /// <summary>The slow walk switched on by the key (UWUserSettings.WalkSlowToggle).</summary>
    private bool mbWalkSlowOn;

    /// <summary>Another key or a mouse button went down while the slow-walk key was held -
    /// Shift+F2, Shift and a click to split a stack -, or the free pointer was on a window: its
    /// release does not switch.</summary>
    private bool mbWalkSlowChord;

    /// <summary>
    /// THE SLOW-WALK KEY (UWControls.WalkSlow; issue 4, 2026-10-21): held it walks slowly, or with
    /// the toggle setting each release switches the slow walk. The switch waits for the release
    /// and skips it when another key or a mouse button was pressed meanwhile, and when the free
    /// pointer lay on a window at the press or the release (per user, the same day: Shift is the
    /// windows' own there) - whatever key the action is bound to.
    /// </summary>
    private void fReadWalkSlowToggle()
    {
        if (!UWUserSettings.WalkSlowToggle || mScheme == null || !UWControlScheme.IsModern)
        {
            mbWalkSlowOn = UWUserSettings.WalkSlowToggle && mbWalkSlowOn;

            return;
        }

        // The press frame only asks for the pointer: its own key went down then, and the walk
        // keys held from before do not count.
        if (mInput.WalkSlow.WasPressedThisFrame())
            mbWalkSlowChord = fPointerOnWindow();
        else if (mInput.WalkSlow.IsPressed() && !mbWalkSlowChord)
            mbWalkSlowChord = fOtherInputPressed();

        if (mInput.WalkSlow.WasReleasedThisFrame() && !mbWalkSlowChord && !fPointerOnWindow())
            mbWalkSlowOn = !mbWalkSlowOn;
    }

    /// <summary>A key or a mouse button pressed in this frame.</summary>
    private static bool fOtherInputPressed()
    {
        if (UWPointer.HasMouse && (UWPointer.LeftPressed || UWPointer.RightPressed
            || UWPointer.MiddlePressed))
            return true;

        UnityEngine.InputSystem.Keyboard lOKeyboard = UnityEngine.InputSystem.Keyboard.current;

        if (lOKeyboard == null)
            return false;

        foreach (UnityEngine.InputSystem.Controls.KeyControl lOKey in lOKeyboard.allKeys)
        {
            if (lOKey != null && lOKey.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    /// <summary>The free modern pointer lies on one of the windows (UWModernUi.IsOverUi).</summary>
    private static bool fPointerOnWindow()
    {
        return UWModernPointer.IsFree && UWPointer.HasMouse && UWModernUi.IsOverUi(UWPointer.Position);
    }

    /// <summary>The slow walk, of the modern scheme only - the classic one's Shift is the easy movement.</summary>
    private bool fIsWalkingSlow()
    {
        if (mScheme == null || !UWControlScheme.IsModern)
            return false;

        return UWUserSettings.WalkSlowToggle ? mbWalkSlowOn : mInput.WalkSlow.IsPressed();
    }

    /// <summary>
    /// WASD of the modern scheme: forward is the run (Walk 0x70, which gives 822 of the 940),
    /// back the original's back command (0xBC - a fifth of the forward speed, not the half the
    /// controller path had), the strafes the slides (0xEB, a quarter). A DIAGONAL has no
    /// command in the original; the port's Free command walks at the run along the stick's
    /// angle from the view (UWPlayerMotion.FreeOffset). The mouse look turns the transform
    /// itself (UWPlayerLook), the frame takes the yaw from there.
    /// </summary>
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
            // The walk for the slide too: Modern runs it at three quarters of the walk, so the slow
            // walk slows it (per user, 2026-10-28); the core's slides keep their own speed.
            pOMotion.Walk = fStickWalk(lODirection);

            return;
        }

        pOMotion.MotionCommand = UWPlayerMotion.Command.Free;
        pOMotion.Walk = fStickWalk(lODirection);
        pOMotion.FreeOffset = UWViewpoint.DegreesToAngle(Mathf.Atan2(lODirection.x, lODirection.y) * Mathf.Rad2Deg) & 0xFFFF;
    }

    /// <summary>The pointer scheme's walk and turn of the last driving frame - the jump with
    /// both buttons keeps them (seg034_2F89_4E does not clear them there).</summary>
    private int miPointerWalk;

    private int miPointerTurn;

    /// <summary>
    /// The original scheme: with the left button held and the press begun in the viewport the
    /// pointer drives with the original's own values (UWPlayerMotion.PointerCommand, from the
    /// pointer's pixel in the 172 by 113 area, UWPointerZones.CursorAreaX/Y; since 2026-10-06 - the
    /// port scaled the keys' 0x70 and 0x5A by the zone distance before). Both buttons outside
    /// the fight mode jump, on the run and again on every landing while held
    /// (UWPlayerMotion.PointerJumpCommand); in the fight mode they drive on. Without the pointer
    /// the keys: W and S, the turn keys, the strafe keys.
    /// </summary>
    private void fBuildOriginalCoreCommand(UWPlayerMotion pOMotion)
    {
        // Lazy instead of in Start(): UWItemDrag is only created later at runtime via
        // AddComponent (UWLevelLoader.fInitialiseUi), at a point in time that is not
        // guaranteed relative to this Start() - a one-time Start() lookup could
        // therefore permanently cache null.
        if (mItemDrag == null)
            mItemDrag = mOHost.GetComponentInChildren<UWItemDrag>();

        // While UWItemDrag drags an item from the backpack onto the paper doll (same
        // mouse button as the cursor movement), crossing the 3D viewport must not
        // move the player at the same time - otherwise one starts walking while equipping.
        // Only while the button is HELD: an item that merely sticks to the pointer leaves the
        // left button to walking (per user on the original, 2026-09-18).
        bool lbHeldDrag = mItemDrag != null && mItemDrag.IsHeldDrag;

        // While a spell waits for its target, the character KEEPS walking: in the original
        // the left button remains movement (per user, 2026-09-05). A first version
        // blocked walking here.
        //
        // It triggers NOTHING in the process - the spell stays pending, targeting in the
        // viewport is done with the right button only (per user, 2026-09-10; the opposite
        // assumption was in UWPendingSpell.UpdateTargeting until then).
        // ONLY A PRESS THAT STARTED IN THE VIEWPORT moves. A left click on the panels (backpack,
        // runes, compass) used to count as a cursor drag too: the pointer was clamped into the
        // viewport and the player stopped, even while walking with the keyboard (per user,
        // 2026-09-14). Once started, the drag may leave the viewport as before.
        // The left button is read through UWMouseButtons: a click under a held right button is
        // ignored there, everywhere (per user on the original, 2026-09-19).
        if (!lbHeldDrag && mGameUi != null && UWMouseButtons.LeftPressed && UWPointer.HasMouse)
            mbCursorDragStartedInViewport = mGameUi.ClassicScreen.IsScreenPositionInGameArea(UWPointer.Position);

        IsCursorMovementInProgress = !lbHeldDrag && mGameUi != null && UWMouseButtons.LeftHeld && mbCursorDragStartedInViewport;

        if (IsCursorMovementInProgress)
        {
            int liButtons = 1 | (UWMouseButtons.RightHeld ? 2 : 0);

            if (UWPlayerMotion.PointerDrives(liButtons, fIsFightMode()))
            {
                UWPlayerMotion.Command leCommand;

                UWPlayerMotion.PointerCommand(mGameUi.PointerZones.CursorAreaX, mGameUi.PointerZones.CursorAreaY,
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

        // Without the pointer the keys: W and S forward and back, A and D TURN, sideways is on
        // the two keys next to X - that is closer to the original (per user, 2026-09-04).
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
}
