using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE GAMEPAD'S POINTER (stage 2 of the gamepad, per user 2026-10-07 - "as Assassin's Creed does in
/// its menus"): the look stick moves the system pointer, A clicks (held: drags; RT too, for the
/// index finger to hold while the thumb steers - per user, the same day; LT likewise for X), X is the right
/// button, the WALK STICK turns the wheel while the pointer is over a window or a window holds
/// the world (per user, 2026-10-08: the d-pad keeps its work in both modes - it scrolled until
/// then; StickScrolls, UWPlayerInput.fMoveInput stops the walk meanwhile). The pad drives THE POINTER
/// (UWPointer.MoveTo: in the game our own, in the menus the system's by a warp) and presses its own
/// mouse's buttons and wheel (UWPointer.PadButton, PadWheel - since cleanup step 7.7B; the system's
/// real ones through UWOsMouse until then, for the IMGUI windows, which are gone), so every window
/// works as with the mouse.
///
/// WHEN: R3 switches it on and off (the modern scheme frees or locks its pointer with it,
/// UWModernPointer); on its own while a window holds the world - the game menu, the options, the
/// map, a conversation, the character creation, the manual (UWControlScheme.IsUIModalOpen,
/// IsConversationOpen) - and with no scheme at all. While it drives, the stick does not turn the
/// view (UWPlayerLook, UWPlayerInput.fClassicTurn), and the gamepad buttons it uses do not do
/// their other work: the PadPointer context (UWInput.PadPointerTakes; Takes here until cleanup step
/// 7.8) - A's use, X's look, RT's strike, LT's cast, as the pointer's buttons are bound.
/// </summary>
// LATE IN THE FRAME: what waits for a pad press owns it before the pointer (UWPadOwners, the owners of
// UWPressOwners; UWGamepad.ConsumePress until cleanup step 7.8), so the pointer does not click with it
// as well.
[DefaultExecutionOrder(UWUpdateOrder.GamepadPointer)]
public class UWGamepadPointer : MonoBehaviour
{
    /// <summary>Switched on by R3.</summary>
    public static bool IsOn { get; private set; }

    private static int msiDrivingFrame = -1;

    private static bool msbDriving;

    /// <summary>The pointer at full deflection crosses this many screen heights a second.</summary>
    private const float ScreensPerSecond = 1.1f;

    /// <summary>The wheel repeats while the d-pad is held: the first repeat after this, then every
    /// WheelRepeatSeconds.</summary>
    private const float WheelDelaySeconds = 0.35f;

    private const float WheelRepeatSeconds = 0.08f;

    // Where the pad put the pointer and whether its position counts (OwnsPosition, Position, HasPlaced,
    // PlaceAt) went to UWPointer with cleanup step 7.7B: PadOwnsPosition, PadPosition, PadPlaced, MoveTo.

    private bool mbLeftDown;

    private bool mbRightDown;

    /// <summary>The wheel's repeat (UWRepeat since cleanup step 7.3, 2026-10-25; the time held and the
    /// next due time here until then): each repeat Every after the last one's due time.</summary>
    private readonly UWRepeat mOWheelRepeat = new UWRepeat(WheelDelaySeconds, WheelRepeatSeconds, UWRepeat.RuleEnum.Accumulate);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void fCreate()
    {
        GameObject lOObject = new GameObject("UWGamepadPointer");
        DontDestroyOnLoad(lOObject);
        lOObject.AddComponent<UWGamepadPointer>();
    }

    private static bool msbClicks;

    private static bool msbStickScrolls;

    /// <summary>The walk stick turns the wheel instead of walking this frame: the pointer drives and
    /// lies on a window - the modern interface's, the classic one outside the view - or a window
    /// holds the world (the menu, a conversation).</summary>
    public static bool StickScrolls
    {
        get
        {
            fRefresh();
            return msbStickScrolls;
        }
    }

    /// <summary>The stick moves the pointer this frame (worked out once a frame).</summary>
    public static bool IsDriving
    {
        get
        {
            fRefresh();
            return msbDriving;
        }
    }

    /// <summary>
    /// The pad's mouse buttons work this frame: while the pointer drives, and IN THE CLASSIC SCHEME
    /// always - its pointer is free anyway, so A, RB and RT are the left button and X, LB and LT
    /// the right one there even with the stick turning the view (per user, 2026-10-07). The wheel
    /// only while it drives.
    /// </summary>
    public static bool ClicksActive
    {
        get
        {
            fRefresh();
            return msbClicks;
        }
    }

    /// <summary>
    /// THE FRAME'S DECISION, before anyone asks (cleanup step 7.8; UWPointerDriver, the first script of
    /// the frame): whether the pointer drives, has its buttons (the PadPointer context) and scrolls -
    /// once a frame, the same for every reader. Until then the first reader decided, wherever it ran
    /// (UWPlayerLook in the game). The pointer's own Update decides again after R3, as before.
    /// </summary>
    public static void DecideFrame()
    {
        fRefresh();
    }

    private static void fRefresh()
    {
        if (msiDrivingFrame == Time.frameCount)
            return;

        msiDrivingFrame = Time.frameCount;
        msbDriving = fComputeDriving(out bool lbFree);

        UWControlScheme lOScheme = UWScene.ControlScheme;

        msbClicks = UWPressOwners.PadPointerContext(msbDriving, lbFree, lOScheme != null, UWControlScheme.IsModern)
            == UWInputContext.PadPointer;
        msbStickScrolls = msbDriving && fIsOverWindow(lOScheme);
    }

    private static bool fIsOverWindow(UWControlScheme pOScheme)
    {
        // An open context menu has the stick for its entries, wherever it stands (UWModernBags).
        if (fIsHeldByWindow(pOScheme) || UWModernBags.IsMenuOpen || UWModernBags.IsSplitting || UWPadEntryStepper.IsActive)
            return true;

        Vector2 lOAt = UWPointer.Position;

        // A window of the modern UI, or outside the classic view window (IUWUiScheme).
        return UWControlScheme.Ui.IsPointerOverInterface(lOAt);
    }

    private static bool fIsHeldByWindow(UWControlScheme pOScheme)
    {
        return pOScheme == null || pOScheme.IsUIModalOpen || pOScheme.IsConversationOpen;
    }

    /// <summary>pbFree: nothing else has the pad's buttons (the grid, a classic prompt), the
    /// pointer is not locked. The rule is UWPressOwners.PadPointerDrives since cleanup step 7.8; the
    /// states are read here.</summary>
    private static bool fComputeDriving(out bool pbFree)
    {
        // NOT BY THE POINTER'S VISIBILITY: the classic drag hides the pointer behind the thing's
        // icon (UWDragIcon.Hold, from UWItemDrag.fBeginActualDrag), and taking that for "off" let the button go at once
        // - the thing fell back, and in a container the neighbour seemed to be taken (per user,
        // 2026-10-07). The letter grid has the pad while it is up (UWLetterGrid), the classic
        // count and yes/no prompts read it themselves (UWItemDrag, Interaction). The modern look mode
        // (the system pointer locked until cleanup step 7.7B; in the game it is captured always since).
        Interaction lOInteraction = UWScene.Interaction;
        UWControlScheme lOScheme = UWScene.ControlScheme;
        bool lbYesNoOnPad = lOInteraction != null && lOInteraction.IsAskingYesNo
            && (lOScheme == null || UWControlScheme.Ui.Policy.PadButtonsAnswerYesNo);

        // THE MODERN SCHEME'S FREE POINTER IS THE PAD'S as well, however it was freed (per user,
        // 2026-10-07: with the pointer freed, RT, RB, LT and LB did their game work instead).
        bool lbModernFree = UWControlScheme.Ui.IsPointerUnlocked;

        return UWPressOwners.PadPointerDrives(UWPointer.IsLookLocked, UWLetterGrid.IsShown,
            UWScene.ItemDrag != null && UWScene.ItemDrag.IsAskingCount, lbYesNoOnPad, IsOn, lbModernFree,
            fIsHeldByWindow(lOScheme), out pbFree);
    }

    /// <summary>Whether a control is one of the pointer's buttons - bound to its click or its right
    /// button (PadClick, PadContext). In the PadPointer context such a pad press is the pointer's
    /// (UWInput.PadPointerTakes; Takes here until cleanup step 7.8).</summary>
    public static bool IsPointerButton(InputControl pOControl)
    {
        UWControls lOControls = fControls();

        if (lOControls == null || pOControl == null)
            return false;

        UWControls.PlayerActions lOPlayer = lOControls.Player;

        return fBoundTo(lOPlayer.PadClick, pOControl) || fBoundTo(lOPlayer.PadContext, pOControl);
    }

    private static bool fBoundTo(InputAction pOAction, InputControl pOControl)
    {
        foreach (InputControl lOControl in pOAction.controls)
        {
            if (lOControl == pOControl)
                return true;
        }

        return false;
    }

    private static UWControls fControls()
    {
        UWControlScheme lOScheme = UWScene.ControlScheme;

        return lOScheme != null && lOScheme.Controls != null ? lOScheme.Controls : UWControls.AnyInstance;
    }

    private void Update()
    {
        UWControls lOControls = fControls();

        if (lOControls == null)
            return;

        UWControls.PlayerActions lOPlayer = lOControls.Player;
        UWControlScheme lOScheme = UWScene.ControlScheme;
        bool lbModern = lOScheme != null && UWControlScheme.IsModern;

        // The modern pointer locked some other way (the right mouse button): R3's state goes.
        if (IsOn && lbModern && !lOScheme.IsPointerFree && !fIsHeldByWindow(lOScheme))
            IsOn = false;

        if (lOPlayer.PadPointer.WasPressedThisFrame() && !fIsHeldByWindow(lOScheme))
            fToggle(lOScheme, lbModern);

        // Worked out again after R3.
        msiDrivingFrame = -1;

        if (!ClicksActive)
        {
            fLetGo();
            return;
        }

        if (IsDriving)
        {
            fMove(lOPlayer.LookStick.ReadValue<Vector2>());
            fWheel(lOPlayer);
        }
        else
        {
            mOWheelRepeat.Reset();
        }

        fButton(lOPlayer.PadClick, false, ref mbLeftDown);
        fButton(lOPlayer.PadContext, true, ref mbRightDown);
    }

    /// <summary>R3: in the modern scheme its pointer frees or locks with it, whoever freed it
    /// (locking puts back what hangs on it, or stays free if there is no room - UWModernPointer.Lock);
    /// in the classic one the pointer is always free, only the stick changes over.</summary>
    private static void fToggle(UWControlScheme pOScheme, bool pbModern)
    {
        if (pbModern)
        {
            if (!pOScheme.IsPointerFree)
            {
                UWModernPointer.Free(true);
                UWModernPointer.PadHoldsLock = false;
                IsOn = true;
            }
            else if (UWModernPointer.Lock())
            {
                UWModernPointer.PadHoldsLock = true;
                IsOn = false;
            }

            return;
        }

        IsOn = !IsOn;
    }

    private static float msfRightSentAt = -10f;

    private static bool msbRightHeld;

    /// <summary>The right mouse button now down or just pressed is the pad's: the modern pointer
    /// does not switch on it (UWModernPointer.Tick) - R3 does that for the pad.</summary>
    public static bool IsPadRightButton => msbRightHeld || Time.unscaledTime - msfRightSentAt < 0.25f;

    private void fMove(Vector2 pOStick)
    {
        if (!UWPointer.HasMouse || pOStick.sqrMagnitude < 0.0001f)
            return;

        // From where the pointer is (UWPointer.Position: ours in the game, where the mouse moves it too;
        // in the menus the pad's own place while it was used last, else the system pointer's).
        Vector2 lOAt = UWPointer.Position;

        // Faster the further out, so small moves stay fine (the deflection once more).
        float lfSpeed = ScreensPerSecond * Screen.height * UWUserSettings.StickLookSpeed * pOStick.magnitude;

        lOAt += pOStick * (lfSpeed * Time.unscaledDeltaTime);
        lOAt.x = Mathf.Clamp(lOAt.x, 0f, Screen.width - 1f);
        lOAt.y = Mathf.Clamp(lOAt.y, 0f, Screen.height - 1f);

        // THE CLASSIC WALK BY THE POINTER (a left button held in the view): the pointer stays inside the
        // original's view window (2026-10-23, with UWPointerZones). Until cleanup step 7.7B the pad kept
        // its own position for it, since the Input System kept reporting the pushed-out positions of
        // UWGameUI's warps and the pointer stuck to the view's top and left edges (per user, 2026-10-07);
        // our own pointer has one position, which UWPointer.MoveTo holds in the window as well.
        UWGameUI lOUi = UWScene.GameUi;

        if (lOUi != null && UWScene.PlayerMovement != null && UWScene.PlayerMovement.Input.IsCursorMovementInProgress)
        {
            Rect lOView = lOUi.ClassicScreen.MovementAreaRect;

            lOAt.x = Mathf.Clamp(lOAt.x, lOView.xMin, lOView.xMax);
            lOAt.y = Mathf.Clamp(lOAt.y, lOView.yMin, lOView.yMax);
        }

        UWPointer.MoveTo(lOAt, UWPointer.SourceEnum.Pad);
    }

    /// <summary>A pad button that clicks (pbRight: the right mouse button) - on the pad's own mouse
    /// (UWPointer.PadButton; the system's real button through UWOsMouse until cleanup step 7.7B).</summary>
    private static void fButton(InputAction pOAction, bool pbRight, ref bool pbDown)
    {
        // A press a place waiting for a pad press owns clicks nothing (UWPadOwners, the owners of
        // UWPressOwners; UWGamepad.ConsumePress until cleanup step 7.8).
        if (!pbDown && pOAction.WasPressedThisFrame() && UWGamepad.IsFromGamepad(pOAction) && UWPadOwners.PointerClicks)
        {
            UWPointer.PadButton(pbRight, true);
            pbDown = true;

            if (pbRight)
                msfRightSentAt = Time.unscaledTime;
        }
        else if (pbDown && !pOAction.IsPressed())
        {
            UWPointer.PadButton(pbRight, false);
            pbDown = false;

            if (pbRight)
                msfRightSentAt = Time.unscaledTime;
        }

        if (pbRight)
            msbRightHeld = pbDown;
    }

    /// <summary>The walk stick up and down over a window: a notch on the push, then repeating,
    /// faster the further out (StickScrolls).</summary>
    private void fWheel(UWControls.PlayerActions pOPlayer)
    {
        // Not under an open context menu: there the stick steps through the entries (UWModernBags).
        float lfStick = StickScrolls && !UWModernBags.IsMenuOpen && !UWModernBags.IsSplitting
            && (!UWPadEntryStepper.IsActive || UWPadEntryStepper.StickYields)
            && UWGamepad.IsFromGamepad(pOPlayer.Move)
            ? pOPlayer.Move.ReadValue<Vector2>().y : 0f;

        if (Mathf.Abs(lfStick) < 0.35f)
        {
            mOWheelRepeat.Reset();
            return;
        }

        int liDirection = lfStick > 0f ? 1 : -1;

        // A notch on the push, the first repeat WheelDelaySeconds later, then the faster the further
        // out (the stick as it is at the repeat). One key for both directions: a flip without the
        // dead zone between goes on with the repeat, as it did.
        mOWheelRepeat.Every = Mathf.Lerp(0.22f, WheelRepeatSeconds, Mathf.InverseLerp(0.35f, 1f, Mathf.Abs(lfStick)));

        if (!mOWheelRepeat.Tick(1, mOWheelRepeat.Held == 0, true, Time.unscaledTime))
            return;

        UWPointer.PadWheel(liDirection);
    }

    /// <summary>A button still down when the pointer stops driving goes up.</summary>
    private void fLetGo()
    {
        if (mbLeftDown)
            UWPointer.PadButton(false, false);

        if (mbRightDown)
        {
            UWPointer.PadButton(true, false);
            msfRightSentAt = Time.unscaledTime;
        }

        mbLeftDown = false;
        mbRightDown = false;
        msbRightHeld = false;
        mOWheelRepeat.Reset();
    }

    private void OnDisable()
    {
        fLetGo();
    }
}
