using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// THE GAMEPAD'S POINTER (stage 2 of the gamepad, per user 2026-10-07 - "as Assassin's Creed does in
/// its menus"): the look stick moves the system pointer, A clicks (held: drags; RT too, for the
/// index finger to hold while the thumb steers - per user, the same day; LT likewise for X), X is the right
/// button, the WALK STICK turns the wheel while the pointer is over a window or a window holds
/// the world (per user, 2026-10-08: the d-pad keeps its work in both modes - it scrolled until
/// then; StickScrolls, UWPlayerMovement.fMoveInput stops the walk meanwhile). The pad drives the REAL pointer and presses
/// the real buttons (UWOsMouse), so every window works as with the mouse, IMGUI ones included.
///
/// WHEN: R3 switches it on and off (the modern scheme frees or locks its pointer with it,
/// UWModernPointer); on its own while a window holds the world - the game menu, the options, the
/// map, a conversation, the character creation, the manual (UWControlScheme.IsUIModalOpen,
/// IsConversationOpen) - and with no scheme at all. While it drives, the stick does not turn the
/// view (UWPlayerLook, UWPlayerMovement.fClassicTurn), and the gamepad buttons it uses do not do
/// their other work (Takes: A's use, X's look, the d-pad's slot and map).
/// </summary>
// LATE IN THE FRAME: what waits for any key takes a pad press first (UWGamepad.ConsumePress), so
// the pointer does not click with it as well.
[DefaultExecutionOrder(1000)]
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

    private Vector2 mOAt;

    private static bool msbPlaced;

    private static Vector2 msOPosition;

    /// <summary>The pad placed the pointer and was used last: its position is the pointer's. The
    /// Input System's own is not to be trusted after a warp (UWGameUI.fCursorMovement asks this
    /// during the classic walk by the pointer).</summary>
    public static bool OwnsPosition => msbPlaced && UWGamepad.IsActive;

    public static Vector2 Position => msOPosition;

    /// <summary>The pad has put the pointer somewhere (Position is meaningful).</summary>
    public static bool HasPlaced => msbPlaced;

    private static bool msbPlacePending;

    /// <summary>Someone put the system pointer there (UWModernPointer.Free): the pad goes on from
    /// it - the Input System may still report the place before.</summary>
    public static void PlaceAt(Vector2 pOAt)
    {
        msOPosition = pOAt;
        msbPlacePending = true;
        msbPlaced = true;
    }

    private Vector2 mOLastWarp = new Vector2(-1000f, -1000f);

    private bool mbLeftDown;

    private bool mbRightDown;

    private float mfWheelHeld;

    private float mfWheelNext;

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

    private static void fRefresh()
    {
        if (msiDrivingFrame == Time.frameCount)
            return;

        msiDrivingFrame = Time.frameCount;
        msbDriving = fComputeDriving(out bool lbFree);

        UWControlScheme lOScheme = UWScene.ControlScheme;

        msbClicks = msbDriving || (lbFree && lOScheme != null && lOScheme.Current == UWControlScheme.SchemeEnum.Original);
        msbStickScrolls = msbDriving && fIsOverWindow(lOScheme);
    }

    private static bool fIsOverWindow(UWControlScheme pOScheme)
    {
        // An open context menu has the stick for its entries, wherever it stands (UWModernBags).
        if (fIsHeldByWindow(pOScheme) || UWModernBags.IsMenuOpen || UWModernBags.IsSplitting || UWPadEntryStepper.IsActive)
            return true;

        Vector2 lOAt = msbPlaced ? msOPosition : (Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);

        if (pOScheme.Current == UWControlScheme.SchemeEnum.Modern)
            return UWModernPointer.IsOverUi(lOAt);

        UWGameUI lOUi = UWScene.GameUi;

        return lOUi != null && !lOUi.IsScreenPositionInGameArea(lOAt);
    }

    private static bool fIsHeldByWindow(UWControlScheme pOScheme)
    {
        return pOScheme == null || pOScheme.IsUIModalOpen || pOScheme.IsConversationOpen;
    }

    /// <summary>pbFree: nothing else has the pad's buttons (the grid, a classic prompt), the
    /// pointer is not locked.</summary>
    private static bool fComputeDriving(out bool pbFree)
    {
        pbFree = false;

        // NOT BY Cursor.visible: the classic drag hides the system pointer behind the thing's
        // icon (UWItemDrag.fBeginActualDrag), and taking that for "off" let the button go at once
        // - the thing fell back, and in a container the neighbour seemed to be taken (per user,
        // 2026-10-07). The letter grid has the pad while it is up (UWLetterGrid), the classic
        // count and yes/no prompts read it themselves (UWItemDrag, Interaction).
        if (Cursor.lockState == CursorLockMode.Locked || UWLetterGrid.IsShown)
            return false;

        if (UWScene.ItemDrag != null && UWScene.ItemDrag.IsAskingCount)
            return false;

        Interaction lOInteraction = UWScene.Interaction;
        UWControlScheme lOScheme = UWScene.ControlScheme;

        if (lOInteraction != null && lOInteraction.IsAskingYesNo
            && (lOScheme == null || lOScheme.Current == UWControlScheme.SchemeEnum.Original))
            return false;

        pbFree = true;

        // THE MODERN SCHEME'S FREE POINTER IS THE PAD'S as well, however it was freed (per user,
        // 2026-10-07: with the pointer freed, RT, RB, LT and LB did their game work instead).
        bool lbModernFree = lOScheme != null && lOScheme.Current == UWControlScheme.SchemeEnum.Modern && lOScheme.IsPointerFree;

        return IsOn || lbModernFree || fIsHeldByWindow(lOScheme);
    }

    /// <summary>Whether a gamepad press of this action is the pointer's - a click or the right button
    /// while they work (ClicksActive), the wheel while it drives -, so the action leaves it alone.</summary>
    public static bool Takes(InputAction pOAction)
    {
        if (!ClicksActive || !UWGamepad.IsFromGamepad(pOAction))
            return false;

        UWControls lOControls = fControls();

        if (lOControls == null)
            return false;

        InputControl lOControl = pOAction.activeControl;
        UWControls.PlayerActions lOPlayer = lOControls.Player;

        return fBoundTo(lOPlayer.PadClick, lOControl) || fBoundTo(lOPlayer.PadContext, lOControl);
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
        bool lbModern = lOScheme != null && lOScheme.Current == UWControlScheme.SchemeEnum.Modern;

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
            mfWheelHeld = -1f;
        }

        fButton(lOPlayer.PadClick, UWOsMouse.ButtonEnum.Left, ref mbLeftDown);
        fButton(lOPlayer.PadContext, UWOsMouse.ButtonEnum.Right, ref mbRightDown);
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
        Mouse lOMouse = Mouse.current;

        if (lOMouse == null || pOStick.sqrMagnitude < 0.0001f)
            return;

        // THE CLASSIC WALK BY THE POINTER (a left button held in the view): UWGameUI holds the
        // pointer inside the view with a warp of its own, and the Input System keeps reporting
        // pushed-out positions after a warp - taken for the real mouse, they reset the pad's
        // pointer to the edge every frame, and it stuck to the view's top and left edges until
        // the button was let go (per user, 2026-10-07). There the pad's own position counts,
        // kept inside the view.
        UWGameUI lOUi = UWScene.GameUi;
        bool lbHeldInView = lOUi != null && UWScene.PlayerMovement != null && UWScene.PlayerMovement.IsCursorMovementInProgress;

        // The real mouse moved it since: go on from there.
        Vector2 lOMousePosition = lOMouse.position.ReadValue();

        if (msbPlacePending)
        {
            msbPlacePending = false;
            mOAt = msOPosition;
        }
        else if (!lbHeldInView && (lOMousePosition - mOLastWarp).sqrMagnitude > 4f)
        {
            mOAt = lOMousePosition;
        }

        // Faster the further out, so small moves stay fine (the deflection once more).
        float lfSpeed = ScreensPerSecond * Screen.height * UWUserSettings.StickLookSpeed * pOStick.magnitude;

        mOAt += pOStick * (lfSpeed * Time.unscaledDeltaTime);
        mOAt.x = Mathf.Clamp(mOAt.x, 0f, Screen.width - 1f);
        mOAt.y = Mathf.Clamp(mOAt.y, 0f, Screen.height - 1f);

        if (lbHeldInView)
        {
            Rect lOView = lOUi.GameAreaRect;

            mOAt.x = Mathf.Clamp(mOAt.x, lOView.xMin, lOView.xMax);
            mOAt.y = Mathf.Clamp(mOAt.y, lOView.yMin, lOView.yMax);
        }

        lOMouse.WarpCursorPosition(mOAt);
        mOLastWarp = mOAt;
        msOPosition = mOAt;
        msbPlaced = true;
        UWGamepad.NoteSyntheticMouse();
    }

    private static void fButton(InputAction pOAction, UWOsMouse.ButtonEnum peButton, ref bool pbDown)
    {
        // A press something waiting for any key took (UWGamepad.ConsumePress) clicks nothing.
        if (!pbDown && pOAction.WasPressedThisFrame() && UWGamepad.IsFromGamepad(pOAction) && !UWGamepad.IsPressConsumed)
        {
            UWOsMouse.Button(peButton, true);
            UWGamepad.NoteSyntheticMouse();
            pbDown = true;

            if (peButton == UWOsMouse.ButtonEnum.Right)
                msfRightSentAt = Time.unscaledTime;
        }
        else if (pbDown && !pOAction.IsPressed())
        {
            UWOsMouse.Button(peButton, false);
            UWGamepad.NoteSyntheticMouse();
            pbDown = false;

            if (peButton == UWOsMouse.ButtonEnum.Right)
                msfRightSentAt = Time.unscaledTime;
        }

        if (peButton == UWOsMouse.ButtonEnum.Right)
            msbRightHeld = pbDown;
    }

    /// <summary>The walk stick up and down over a window: a notch on the push, then repeating,
    /// faster the further out (StickScrolls).</summary>
    private void fWheel(UWControls.PlayerActions pOPlayer)
    {
        // Not under an open context menu: there the stick steps through the entries (UWModernBags).
        float lfStick = StickScrolls && !UWModernBags.IsMenuOpen && !UWModernBags.IsSplitting && !UWPadEntryStepper.IsActive
            && UWGamepad.IsFromGamepad(pOPlayer.Move)
            ? pOPlayer.Move.ReadValue<Vector2>().y : 0f;

        if (Mathf.Abs(lfStick) < 0.35f)
        {
            mfWheelHeld = -1f;
            return;
        }

        int liDirection = lfStick > 0f ? 1 : -1;

        if (mfWheelHeld < 0f)
        {
            mfWheelHeld = 0f;
            mfWheelNext = WheelDelaySeconds;
            UWOsMouse.Wheel(liDirection);
            UWGamepad.NoteSyntheticMouse();
            return;
        }

        mfWheelHeld += Time.unscaledDeltaTime;

        if (mfWheelHeld < mfWheelNext)
            return;

        mfWheelNext += Mathf.Lerp(0.22f, WheelRepeatSeconds, Mathf.InverseLerp(0.35f, 1f, Mathf.Abs(lfStick)));
        UWOsMouse.Wheel(liDirection);
        UWGamepad.NoteSyntheticMouse();
    }

    /// <summary>A button still down when the pointer stops driving goes up.</summary>
    private void fLetGo()
    {
        if (mbLeftDown)
            UWOsMouse.Button(UWOsMouse.ButtonEnum.Left, false);

        if (mbRightDown)
        {
            UWOsMouse.Button(UWOsMouse.ButtonEnum.Right, false);
            msfRightSentAt = Time.unscaledTime;
        }

        mbLeftDown = false;
        mbRightDown = false;
        msbRightHeld = false;
        mfWheelHeld = -1f;
    }

    private void OnDisable()
    {
        fLetGo();
    }
}
