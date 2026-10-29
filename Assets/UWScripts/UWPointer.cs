using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UWDataImport.UWData;

/// <summary>
/// THE POINTER, IN ONE PLACE (cleanup step 7.7A, 2026-10-25; Docs/CLEANUP-PLAN.md 8.5): where the
/// pointer is (Position, screen pixels with y up), how the mouse moved and turned its wheel this frame
/// (Delta, Scroll), its three buttons (pressed, held, released this frame), whether the system pointer
/// is captured (IsCaptured / IsFree) and who placed the pointer last (Source). Until 7.7A 33 classes
/// read Mouse.current themselves (79 position reads and the buttons beside them); they ask here.
///
/// THE POINTER OF OUR OWN (cleanup step 7.7B, 2026-10-25; the goal decided 2026-10-23, the speed and
/// the capture decided 2026-10-25, per user):
///
///   - IN THE RUNNING GAME the system pointer is locked and hidden (CAPTURED) and the pointer is ours:
///     moved by the mouse's counts times the Pointer speed setting (UWUserSettings.PointerSpeed, 1 =
///     one count per screen pixel, no acceleration), held in the window, drawn by us with the picture
///     UWPointerCursor chooses, at its hotspot (UWPointerDriver). In both schemes and every preset - the
///     HUD, the inventory, the map, the help, a conversation, the classic walk by the pointer, the
///     modern free pointer. The modern look mode stays locked as before; the pointer is hidden there and
///     stands in the middle of the view, where the free pointer appears again.
///   - THE MENUS FREE the system pointer (IsFree): the main menu and the character creation, the modern
///     game menu (the layout editor with it), the classic options panel (the classic game menu, treated
///     like the modern one), the setup bar's windows and the effects screen, the first run - and every
///     scene without a world or a control scheme. There the position is the system's, drawn by Unity
///     with our picture (Cursor.SetCursor, ForceSoftware, as since 2026-10-23).
///   - THE SWITCH: free to captured, ours starts where the system pointer stands; captured to free, the
///     system pointer is put once where ours was (one WarpCursorPosition; where the platform refuses it
///     - Wayland, XWayland - ours takes the system's position after a few frames, UWPointerMotion).
///   - THE FOCUS: lost (Alt+Tab, another window), the capture is let go without a warp; back, the
///     capture comes back with our pointer where it was, and the click that gave the focus back is
///     taken out of the mouse's events (UWFocusClick) - it clicks nothing in the game.
///   - THE PAD (UWGamepadPointer, UWPadEntryStepper, the modern bags' slot of UWModernPointer) moves
///     this pointer (MoveTo); captured that moves ours, free the system pointer by a warp as before.
///     Its buttons and wheel press a MOUSE OF OUR OWN (an Input System device, "UWPadPointer") instead
///     of the system's real buttons (UWOsMouse until this step): every binding to a mouse button takes
///     them as it took the system's clicks - one frame later, as those came -, and the buttons here are
///     the system mouse's and that one's together. No system click any more, so none goes astray.
///   - THE CLASSIC WALK'S CLAMP holds our position in the original's view window (ClampTo, from
///     UWPointerZones) - no warp while captured.
///   - THE POINTER'S VISIBILITY (IsShown) is the game's one switch for "the pointer is to be seen" -
///     what Cursor.visible was to its writers (the drag's icon, the prompts, the map's note, the
///     intro, the scheme's look mode); the Cursor itself is written here only, and only when what it
///     should be changes.
/// </summary>
public static class UWPointer
{
    /// <summary>Who put the pointer where it is: the mouse, the gamepad's pointer (UWGamepadPointer,
    /// UWPadEntryStepper, UWModernPointer's slot) or the classic walk's clamp to the view window
    /// (UWPointerZones).</summary>
    public enum SourceEnum
    {
        Mouse,
        Pad,
        ClassicWarp
    }

    /// <summary>The pad's mouse's name among the Input System's devices.</summary>
    private const string PadMouseName = "UWPadPointer";

    /// <summary>A wheel notch as the pad's mouse reports it - Windows' raw step (UWOsMouse sent the
    /// same); the readers take the sign, the kit's rows one step per 120.</summary>
    private const float WheelNotch = 120f;

    private static readonly UWPointerMotion msOMotion = new UWPointerMotion();

    private static readonly UWFocusClick msOFocusClick = new UWFocusClick();

    /// <summary>The system's mouse: the current one unless that is the pad's.</summary>
    private static Mouse msOSystemMouse;

    /// <summary>The pad's buttons and wheel (made by the driver at the start).</summary>
    private static Mouse msOPadMouse;

    private static ushort msuPadButtons;

    private static SourceEnum mseSource = SourceEnum.Mouse;

    // ------------------------------------------------- Where and how

    /// <summary>A mouse is there - the system's or the pad's. Without one the position is (0, 0) and
    /// no button is down.</summary>
    public static bool HasMouse => fSystemMouse() != null || msOPadMouse != null;

    /// <summary>
    /// The pointer's position in screen pixels, y up: ours while captured (and while the warp at the
    /// release has not arrived), the pad's where it placed the system pointer and was used last (the
    /// system's report lags a warp), else the system pointer's.
    /// </summary>
    public static Vector2 Position
    {
        get
        {
            if (msOMotion.IsCaptured || msOMotion.IsWarpPending)
                return new Vector2(msOMotion.X, msOMotion.Y);

            if (PadOwnsPosition)
                return msOPadPosition;

            Mouse lOMouse = fSystemMouse();

            return lOMouse != null ? lOMouse.position.ReadValue() : Vector2.zero;
        }
    }

    /// <summary>How far the system mouse moved this frame, in its own counts.</summary>
    public static Vector2 Delta
    {
        get
        {
            Mouse lOMouse = fSystemMouse();

            return lOMouse != null ? lOMouse.delta.ReadValue() : Vector2.zero;
        }
    }

    /// <summary>The wheel this frame (y away from the player positive): the system mouse's and the
    /// pad's.</summary>
    public static Vector2 Scroll
    {
        get
        {
            Mouse lOMouse = fSystemMouse();
            Vector2 lOScroll = lOMouse != null ? lOMouse.scroll.ReadValue() : Vector2.zero;

            return msOPadMouse != null ? lOScroll + msOPadMouse.scroll.ReadValue() : lOScroll;
        }
    }

    /// <summary>The system pointer is locked and hidden; ours is the pointer (the running game).</summary>
    public static bool IsCaptured => msOMotion.IsCaptured;

    /// <summary>The system pointer is free - the menus, no game.</summary>
    public static bool IsFree => !msOMotion.IsCaptured;

    /// <summary>Who placed the pointer last.</summary>
    public static SourceEnum Source => mseSource;

    /// <summary>
    /// THE MODERN LOOK MODE (UWControlScheme): the mouse turns the view, the pointer is hidden and
    /// stands in the middle. Locked the system pointer as before; the kit's windows and the pad's
    /// pointer leave it alone (what "Cursor.lockState == Locked" meant to them until 7.7B).
    /// </summary>
    public static bool IsLookLocked
    {
        get => msbLookLocked;
        set
        {
            if (msbLookLocked == value)
                return;

            msbLookLocked = value;

            // Out of the look the pointer appears in the middle of the view, as unlocking the system
            // pointer left it (UWModernPointer.Free).
            if (!value && msOMotion.IsCaptured)
                fPlaceInMiddle();

            fApply();
        }
    }

    private static bool msbLookLocked;

    /// <summary>The pointer is to be seen (what Cursor.visible was to the game): set by whoever hides
    /// it - the drag's icon, a prompt, the map's note, the intro - and shows it again. Applied at once,
    /// written to the Cursor only when its state changes.</summary>
    public static bool IsShown
    {
        get => msbShown;
        set
        {
            if (msbShown == value)
                return;

            msbShown = value;
            fApply();
        }
    }

    private static bool msbShown = true;

    // ------------------------------------------------- The buttons: the system mouse's and the pad's

    public static bool LeftPressed => fPressed(fSystemMouse()?.leftButton) || fPressed(msOPadMouse?.leftButton);

    public static bool LeftHeld => fHeld(fSystemMouse()?.leftButton) || fHeld(msOPadMouse?.leftButton);

    public static bool LeftReleased => fReleased(fSystemMouse()?.leftButton) || fReleased(msOPadMouse?.leftButton);

    public static bool RightPressed => fPressed(fSystemMouse()?.rightButton) || fPressed(msOPadMouse?.rightButton);

    public static bool RightHeld => fHeld(fSystemMouse()?.rightButton) || fHeld(msOPadMouse?.rightButton);

    public static bool RightReleased => fReleased(fSystemMouse()?.rightButton) || fReleased(msOPadMouse?.rightButton);

    public static bool MiddlePressed => fPressed(fSystemMouse()?.middleButton);

    public static bool MiddleHeld => fHeld(fSystemMouse()?.middleButton);

    public static bool MiddleReleased => fReleased(fSystemMouse()?.middleButton);

    /// <summary>Any of the three went down this frame.</summary>
    public static bool AnyPressed => LeftPressed || RightPressed || MiddlePressed;

    /// <summary>Any of the three is down.</summary>
    public static bool AnyHeld => LeftHeld || RightHeld || MiddleHeld;

    /// <summary>
    /// A bound button (UWMouseButtons, which follows the rebinding of the two world buttons): a mouse
    /// button counts on the system mouse and the pad's alike - the binding's path finds the first
    /// mouse's control only - , any other control as it is.
    /// </summary>
    public static bool Pressed(ButtonControl pOControl)
    {
        return fPressed(pOControl) || fPressed(fTwin(pOControl));
    }

    public static bool Held(ButtonControl pOControl)
    {
        return fHeld(pOControl) || fHeld(fTwin(pOControl));
    }

    public static bool Released(ButtonControl pOControl)
    {
        return fReleased(fTwin(pOControl)) || fReleased(pOControl);
    }

    /// <summary>The device is the pad's mouse - its presses are the pad's (UWGamepad: not the player
    /// taking up the mouse).</summary>
    public static bool IsPadMouse(InputDevice pODevice)
    {
        return pODevice != null && pODevice == msOPadMouse;
    }

    private static bool fPressed(ButtonControl pOControl)
    {
        return pOControl != null && pOControl.wasPressedThisFrame;
    }

    private static bool fHeld(ButtonControl pOControl)
    {
        return pOControl != null && pOControl.isPressed;
    }

    private static bool fReleased(ButtonControl pOControl)
    {
        return pOControl != null && pOControl.wasReleasedThisFrame;
    }

    /// <summary>The same button on the other mouse (system and pad), null for none.</summary>
    private static ButtonControl fTwin(ButtonControl pOControl)
    {
        if (pOControl == null || !(pOControl.device is Mouse lOMouse))
            return null;

        Mouse lOOther = lOMouse == msOPadMouse ? fSystemMouse() : msOPadMouse;

        if (lOOther == null || lOOther == lOMouse)
            return null;

        if (pOControl == lOMouse.leftButton)
            return lOOther.leftButton;

        return pOControl == lOMouse.rightButton ? lOOther.rightButton : null;
    }

    /// <summary>The system's mouse: the current one, or the last current one while the pad's mouse
    /// took that place (its events make it current).</summary>
    private static Mouse fSystemMouse()
    {
        Mouse lOCurrent = Mouse.current;

        if (lOCurrent != null && lOCurrent != msOPadMouse)
            msOSystemMouse = lOCurrent;

        if (msOSystemMouse != null && !msOSystemMouse.added)
            msOSystemMouse = null;

        return msOSystemMouse;
    }

    // ------------------------------------------------- The pad

    private static bool msbPadPlaced;

    private static Vector2 msOPadPosition;

    /// <summary>The pad has put the pointer somewhere (PadPosition is meaningful).</summary>
    public static bool PadPlaced => msbPadPlaced;

    /// <summary>Where the pad put the pointer last.</summary>
    public static Vector2 PadPosition => msOPadPosition;

    /// <summary>The pad placed the free system pointer and was used last: its position is the pointer's
    /// - the Input System's own is not to be trusted after a warp (UWGamepadPointer.OwnsPosition until
    /// 7.7B). Captured, ours is the pointer anyway.</summary>
    public static bool PadOwnsPosition => msbPadPlaced && UWGamepad.IsActive && !msOMotion.IsCaptured;

    /// <summary>
    /// PUTS THE POINTER THERE (the pad's stick, its menu stepper, the modern bags' first slot): captured,
    /// ours goes there, held in the window and in the classic walk's view window while that holds it;
    /// free, the system pointer is warped there as the pad did before 7.7B.
    /// </summary>
    public static void MoveTo(Vector2 pOAt, SourceEnum peSource)
    {
        mseSource = peSource;

        if (msOMotion.IsCaptured)
        {
            msOMotion.Place(pOAt.x, pOAt.y, Screen.width, Screen.height);
            fApplyClamp();

            if (peSource == SourceEnum.Pad)
                fNotePad();

            return;
        }

        Mouse lOMouse = fSystemMouse();

        if (lOMouse != null)
            lOMouse.WarpCursorPosition(pOAt);

        if (peSource == SourceEnum.Pad)
            fNotePad(pOAt);

        UWGamepad.NoteSyntheticMouse();
    }

    private static void fNotePad()
    {
        fNotePad(new Vector2(msOMotion.X, msOMotion.Y));
    }

    private static void fNotePad(Vector2 pOAt)
    {
        msOPadPosition = pOAt;
        msbPadPlaced = true;
    }

    /// <summary>The pad's left or right button (UWGamepadPointer): pressed or let go on the pad's own
    /// mouse, which the Input System takes up with its next update, as it took the system clicks.</summary>
    public static void PadButton(bool pbRight, bool pbDown)
    {
        MouseButton leButton = pbRight ? MouseButton.Right : MouseButton.Left;
        ushort luBit = (ushort)(1 << (int)leButton);
        ushort luButtons = pbDown ? (ushort)(msuPadButtons | luBit) : (ushort)(msuPadButtons & ~luBit);

        if (luButtons == msuPadButtons)
            return;

        msuPadButtons = luButtons;
        fQueuePad(0f);
    }

    /// <summary>The pad turns the wheel by notches, positive away from the player (up).</summary>
    public static void PadWheel(int piNotches)
    {
        if (piNotches != 0)
            fQueuePad(piNotches * WheelNotch);
    }

    private static void fQueuePad(float pfScrollY)
    {
        if (msOPadMouse == null || !msOPadMouse.added)
            return;

        MouseState lOState = new MouseState
        {
            position = Position,
            buttons = msuPadButtons,
            scroll = new Vector2(0f, pfScrollY)
        };

        InputSystem.QueueStateEvent(msOPadMouse, lOState);
    }

    // ------------------------------------------------- The classic walk's clamp

    private static int msiClampFrame = -10;

    private static Rect msOClamp;

    /// <summary>
    /// THE CLASSIC WALK BY THE POINTER holds it in the original's view window (UWPointerZones, every
    /// frame of the walk): captured, our position is clamped now and again after every move until a
    /// frame passes without the request; free (not in a running game) the system pointer is warped
    /// back as before 7.7B. Returns the position.
    /// </summary>
    public static Vector2 ClampTo(Rect pOArea)
    {
        msOClamp = pOArea;
        msiClampFrame = Time.frameCount;

        if (msOMotion.IsCaptured)
        {
            fApplyClamp();
            return Position;
        }

        Vector2 lOAt = Position;
        Vector2 lOClamped = new Vector2(Mathf.Clamp(lOAt.x, pOArea.xMin, pOArea.xMax), Mathf.Clamp(lOAt.y, pOArea.yMin, pOArea.yMax));
        Mouse lOMouse = fSystemMouse();

        if (lOMouse != null && (!Mathf.Approximately(lOClamped.x, lOAt.x) || !Mathf.Approximately(lOClamped.y, lOAt.y)))
        {
            lOMouse.WarpCursorPosition(lOClamped);
            mseSource = SourceEnum.ClassicWarp;
        }

        return lOClamped;
    }

    /// <summary>The clamp asked for this frame or the last, applied to our position.</summary>
    private static void fApplyClamp()
    {
        if (Time.frameCount - msiClampFrame > 1)
            return;

        if (msOMotion.Clamp(msOClamp.xMin, msOClamp.yMin, msOClamp.xMax, msOClamp.yMax))
            mseSource = SourceEnum.ClassicWarp;
    }

    // ------------------------------------------------- The picture

    private static Texture2D msOPicture;

    private static Vector2 msOHotspot;

    /// <summary>The picture we draw while captured (UWPointerDriver), null for none.</summary>
    internal static Texture2D Picture => msOPicture;

    /// <summary>Its hotspot, pixels from the top-left corner as Cursor.SetCursor takes it.</summary>
    internal static Vector2 Hotspot => msOHotspot;

    /// <summary>Our pointer is drawn this frame: captured, to be seen, not in the look mode.</summary>
    internal static bool IsDrawn => msOMotion.IsCaptured && msbShown && !msbLookLocked && msOPicture != null;

    /// <summary>
    /// The pointer's picture and hotspot (UWPointerCursor, every frame it shows one): captured we draw
    /// it; free the system pointer gets it, drawn by Unity itself (PointerCursorMode).
    /// </summary>
    public static void SetPicture(Texture2D pOPicture, Vector2 pOHotspot)
    {
        msOPicture = pOPicture;
        msOHotspot = pOHotspot;

        if (!msOMotion.IsCaptured && pOPicture != null && !fSystemArrow)
            Cursor.SetCursor(pOPicture, pOHotspot, PointerCursorMode);
    }

    /// <summary>
    /// PROVISIONAL (per user, 2026-10-29, testing the second game's walk: "show the system mouse pointer
    /// for now so I see where I click"): while the second game runs without its own pointer - its main
    /// menu so far (UW2 plan phase 3 step 5) - the plain system arrow shows, free and always visible.
    /// The walk's classic screen has the game's pointers since phase 3 step 1 (UWGameScreenUw2).
    /// </summary>
    private static bool fSystemArrow => UnderworldRevisited.UWSettings.Game != null && UnderworldRevisited.UWSettings.Game.Game == UWGame.Uw2
        && !UWGameScreenUw2.IsClassicActive;

    /// <summary>Whether the plain arrow has been put in place of a picture (fSystemArrow).</summary>
    private static bool msbArrowWritten;

    /// <summary>
    /// How the free system pointer's picture is drawn: BY UNITY ITSELF, in both schemes (per user,
    /// 2026-10-23). The system limits the size of a hardware pointer, so the system pointer came
    /// smaller than the frame's scale asks for - the quill on the map (2026-09-03) and every pointer;
    /// the original drew its pointer itself too. The price is a frame's delay behind the hand. Until
    /// 2026-10-23 the system drew the pointer (Auto); until 7.7B this stood in UWPointerCursor.
    /// </summary>
    private static CursorMode PointerCursorMode => CursorMode.ForceSoftware;

    // ------------------------------------------------- The capture

    private static bool msbFocused = true;

    /// <summary>The capture was let go with the focus: taken up again, ours comes back where it was
    /// (msOKeptAtFocus - the position follows the system pointer meanwhile).</summary>
    private static bool msbLostToFocus;

    private static Vector2 msOKeptAtFocus;

    private static bool msbWritten;

    private static bool msbWrittenVisible;

    private static CursorLockMode meWrittenLock;

    /// <summary>
    /// THE RUNNING GAME CAPTURES (per user, 2026-10-25): a world, a control scheme, the game's interface,
    /// and none of the menus that free the pointer. The modern look mode is captured anyway.
    /// </summary>
    private static bool fWantsCapture()
    {
        if (!msbFocused || UWControlScheme.Shared == null)
            return false;

        if (msbLookLocked)
            return true;

        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWGameUI lOUi = UWScene.GameUi;

        // The second game's walk (UW2 plan phase 2 step 4): the modern scheme's mouse look captures as
        // in the game; the classic scheme captures for the game's own pointer once its screen is up
        // (phase 3 step 1, UWGameScreenUw2), the system arrow before.
        if (UWWalkUw2.IsWalking)
            return (UWControlScheme.Shared.Current == UWControlScheme.SchemeEnum.Modern || UWGameScreenUw2.IsClassicActive)
                && !UWSetupMenu.HasOpenPanel && !UWSetupMenu.IsEffectsScreenOpen;

        if (lOLoader == null || !lOLoader.HasWorld || lOUi == null)
            return false;

        return !UWScreenUi.IsScreenMenuOpen && !UWModernUi.IsMenuOpen && !lOUi.Classic.IsOptionsOpen
            && !UWSetupMenu.HasOpenPanel && !UWSetupMenu.IsEffectsScreenOpen;
    }

    /// <summary>Takes up or lets go the capture as the game asks, and writes the Cursor's state - only
    /// when it is to change.</summary>
    private static void fApply()
    {
        bool lbCapture = fWantsCapture();

        if (lbCapture && !msOMotion.IsCaptured)
            fCapture();
        else if (!lbCapture && msOMotion.IsCaptured)
            fRelease();

        if (!lbCapture && msbFocused)
            msbLostToFocus = false;

        fWriteCursor();
    }

    private static void fCapture()
    {
        if (msbLostToFocus)
        {
            // Back from Alt+Tab: our pointer where it was.
            msbLostToFocus = false;
            msOMotion.Capture(msOKeptAtFocus.x, msOKeptAtFocus.y, Screen.width, Screen.height);
        }
        else
        {
            Vector2 lOAt = Position;

            msOMotion.Capture(lOAt.x, lOAt.y, Screen.width, Screen.height);
            mseSource = SourceEnum.Mouse;
        }

        if (msbLookLocked)
            fPlaceInMiddle();
    }

    private static void fRelease()
    {
        bool lbWarp = msbFocused;

        msOKeptAtFocus = new Vector2(msOMotion.X, msOMotion.Y);
        msOMotion.Release(lbWarp);
        msbLostToFocus = !msbFocused;

        // The Cursor first, then the warp: the system pointer is free to be put somewhere.
        fWriteCursor();

        Mouse lOMouse = fSystemMouse();
        Vector2 lOAt = new Vector2(msOMotion.X, msOMotion.Y);

        if (lbWarp && lOMouse != null)
            lOMouse.WarpCursorPosition(lOAt);

        // The pad goes on from there (it placed the system pointer before 7.7B too).
        if (msbPadPlaced)
            msOPadPosition = lOAt;

        if (msOPicture != null && !fSystemArrow)
            Cursor.SetCursor(msOPicture, msOHotspot, PointerCursorMode);
    }

    /// <summary>The Cursor's lock and visibility, written when they differ from what was written last.</summary>
    private static void fWriteCursor()
    {
        bool lbCaptured = msOMotion.IsCaptured;
        CursorLockMode leLock = lbCaptured ? CursorLockMode.Locked : CursorLockMode.None;
        bool lbVisible = !lbCaptured && (msbShown || fSystemArrow);

        if (fSystemArrow && !msbArrowWritten)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            msbArrowWritten = true;
        }
        else if (!fSystemArrow)
        {
            // Written again when the arrow comes back (the walk's screen gone, the main menu up).
            msbArrowWritten = false;
        }

        if (!msbWritten || meWrittenLock != leLock)
            Cursor.lockState = leLock;

        if (!msbWritten || msbWrittenVisible != lbVisible)
            Cursor.visible = lbVisible;

        msbWritten = true;
        meWrittenLock = leLock;
        msbWrittenVisible = lbVisible;
    }

    /// <summary>
    /// LINUX ONLY (UWIntroPlayer, per user 2026-10-09): a pointer hidden before the window is fully up
    /// is lost by the player and shows anyway, and writing the same value again does nothing - so while
    /// a sequence plays it is shown and hidden within one frame, twice a second. Only where the system
    /// pointer is to be hidden; the written state stays.
    /// </summary>
    public static void ReassertHidden()
    {
        if (msbWritten && msbWrittenVisible)
            return;

        Cursor.visible = true;
        Cursor.visible = false;
    }

    private static void fPlaceInMiddle()
    {
        msOMotion.Place(Screen.width * 0.5f, Screen.height * 0.5f, Screen.width, Screen.height);
    }

    // ------------------------------------------------- The frame (UWPointerDriver)

    /// <summary>The driver's start: the pad's mouse, the focus and the mouse's events.</summary>
    internal static void Start()
    {
        msbFocused = Application.isFocused;

        Application.focusChanged -= fOnFocus;
        Application.focusChanged += fOnFocus;
        InputSystem.onEvent -= fOnEvent;
        InputSystem.onEvent += fOnEvent;

        fSystemMouse();

        msOPadMouse = InputSystem.GetDevice(PadMouseName) as Mouse;

        if (msOPadMouse == null)
        {
            try
            {
                msOPadMouse = InputSystem.AddDevice<Mouse>(PadMouseName);
            }
            catch (Exception lOError)
            {
                Debug.LogWarning("UWPointer: no mouse for the gamepad's pointer (" + lOError.GetType().Name + ": " + lOError.Message + ").");
            }
        }

        fRestoreCurrentMouse();
    }

    /// <summary>The driver's end (the game quits, the editor leaves play mode): the hooks and the pad's
    /// mouse go, so the editor does not keep a mouse of ours as its current one.</summary>
    internal static void Stop()
    {
        Application.focusChanged -= fOnFocus;
        InputSystem.onEvent -= fOnEvent;

        if (msOPadMouse != null && msOPadMouse.added)
            InputSystem.RemoveDevice(msOPadMouse);

        msOPadMouse = null;
        msuPadButtons = 0;
    }

    /// <summary>
    /// ONCE A FRAME, FIRST (UWUpdateOrder.Pointer): the capture as the game asks; captured, ours moves by
    /// the mouse's counts times the speed and is held in the classic walk's window while that is asked,
    /// or stands in the middle in the look mode; free, it follows the system pointer.
    /// </summary>
    internal static void Tick()
    {
        msOFocusClick.Tick(Time.realtimeSinceStartupAsDouble);
        fRestoreCurrentMouse();
        fApply();

        if (!msOMotion.IsCaptured)
        {
            Mouse lOMouse = fSystemMouse();

            if (lOMouse != null)
            {
                Vector2 lOSystem = lOMouse.position.ReadValue();

                msOMotion.FollowSystem(lOSystem.x, lOSystem.y);
            }

            return;
        }

        if (msbLookLocked)
        {
            fPlaceInMiddle();
            return;
        }

        Vector2 lODelta = Delta;

        if (lODelta.x != 0f || lODelta.y != 0f)
        {
            msOMotion.Move(lODelta.x, lODelta.y, UWUserSettings.PointerSpeed, Screen.width, Screen.height);
            mseSource = SourceEnum.Mouse;
        }
        else
        {
            // A smaller window keeps it inside as well.
            msOMotion.Place(msOMotion.X, msOMotion.Y, Screen.width, Screen.height);
        }

        fApplyClamp();
    }

    /// <summary>The pad's mouse becomes current with its events: the system's is made current again,
    /// so whoever asks Mouse.current keeps meeting the real one.</summary>
    private static void fRestoreCurrentMouse()
    {
        if (msOPadMouse == null || Mouse.current != msOPadMouse)
            return;

        if (msOSystemMouse == null || !msOSystemMouse.added)
        {
            msOSystemMouse = null;

            foreach (InputDevice lODevice in InputSystem.devices)
            {
                if (lODevice is Mouse lOMouse && lOMouse != msOPadMouse)
                {
                    msOSystemMouse = lOMouse;
                    break;
                }
            }
        }

        if (msOSystemMouse != null)
            msOSystemMouse.MakeCurrent();
    }

    private static void fOnFocus(bool pbFocused)
    {
        msbFocused = pbFocused;

        // Back into the running game, the click on the window is not the game's; back into a menu it
        // clicks as any click there.
        if (pbFocused && fWantsCapture())
            msOFocusClick.FocusGained();
        else
            msOFocusClick.FocusLost();

        fApply();
    }

    /// <summary>The click that gives the focus back: its buttons taken out of the system mouse's events
    /// (UWFocusClick).</summary>
    private static void fOnEvent(InputEventPtr pOEvent, InputDevice pODevice)
    {
        if (!msOFocusClick.IsWatching || !(pODevice is Mouse lOMouse) || lOMouse == msOPadMouse)
            return;

        if (!pOEvent.IsA<StateEvent>() && !pOEvent.IsA<DeltaStateEvent>())
            return;

        try
        {
            bool lbDown = fDownIn(lOMouse.leftButton, pOEvent) || fDownIn(lOMouse.rightButton, pOEvent)
                || fDownIn(lOMouse.middleButton, pOEvent);

            if (!msOFocusClick.Swallows(lbDown, Time.realtimeSinceStartupAsDouble))
                return;

            lOMouse.leftButton.WriteValueIntoEvent(0f, pOEvent);
            lOMouse.rightButton.WriteValueIntoEvent(0f, pOEvent);
            lOMouse.middleButton.WriteValueIntoEvent(0f, pOEvent);
        }
        catch (Exception)
        {
            // An event of a format the mouse does not read: left as it is.
        }
    }

    private static bool fDownIn(ButtonControl pOButton, InputEventPtr pOEvent)
    {
        return pOButton.ReadValueFromEvent(pOEvent, out float lfValue) && lfValue >= 0.5f;
    }
}
