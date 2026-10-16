using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// IMGUI READS THE POINTER WHERE THE INPUT SYSTEM DOES (per user, 2026-10-12, Linux on a 4K
/// television: after the Display dialog set 1920x1080 in borderless fullscreen, the menu bar, its
/// dialogs and the modern game menu took no click - also after a restart - while everything in the
/// game that reads the Input System, the inventory included, clicked right).
///
/// The Player.log of a click on "Quit" in the middle of the modern game menu: the Input System
/// 959/440 (bottom-left origin, the middle of the 1920-pixel picture: right), IMGUI 479.5/859
/// (half of it: wrong). Unity's X11 player in a downscaled fullscreen viewport ("Setting up
/// downscaled viewport at 1920 x 1080" on a 3840x2160 desktop) hands IMGUI a pointer scaled down
/// once too often. Two earlier attempts the same evening: the first made IMGUI follow the Input
/// System but only BEFORE the hosts set GUI.matrix, the second took IMGUI for the right one.
///
/// So each IMGUI host calls BeginOnGUI first and AfterMatrix after every GUI.matrix it sets: the
/// event's position becomes the Input System's (flipped to IMGUI's top-left origin, carried through
/// the matrix). Where both agree (Windows, a window, the desktop's own resolution) nothing changes.
/// The first presses and releases and the first correction are logged.
/// </summary>
public static class UWImguiInput
{
    /// <summary>A difference of more than this many pixels counts as a disagreement.</summary>
    private const float Tolerance = 1.5f;

    private const int MaxLoggedEvents = 12;

    private static int miLoggedEvents;

    private static bool mbCorrectionLogged;

    /// <summary>The pointer in the rendered picture's pixels, bottom-left origin - the Input
    /// System's, which is right on every platform seen so far.</summary>
    public static Vector2 PointerPosition
    {
        get
        {
            Mouse lOMouse = Mouse.current;

            return lOMouse != null ? lOMouse.position.ReadValue() : Vector2.zero;
        }
    }

    // ------------------------------------------------- Controls with the Linux fallback

    /// <summary>
    /// GUI.Button WITH THE LINUX FALLBACK (per user, 2026-10-14: on a 4K television in borderless
    /// 1920x1080 the modern game menu's and the layout editor's buttons saw no MouseDown or MouseUp
    /// at all - IMGUI did not hand them the click - while the Input System had it right; the bar
    /// and its dialogs got theirs). The pointer is synced first; on the Linux player a release of
    /// the left button over the control in the repaint counts as its press unless an IMGUI MouseUp
    /// reached THIS control in the same frame, so a delivered click never fires twice. Per control,
    /// not per frame (per user the same morning: with one mark for the frame, the bar - whose
    /// clicks IMGUI does deliver - shut the fallback off for the modern game menu below it).
    /// Every IMGUI button and toggle of the game goes through here (UWImguiKeyNav, the layout
    /// editor); the menus move to uGUI later (Todo, cleanup phase 8).
    /// </summary>
    public static bool Button(Rect pORect, GUIContent pOContent, GUIStyle pOStyle)
    {
        Sync();

        EventType leBefore = Event.current != null ? Event.current.type : EventType.Ignore;
        bool lbPressed = pOStyle != null ? GUI.Button(pORect, pOContent, pOStyle) : GUI.Button(pORect, pOContent);

        return AfterControl(pORect, leBefore, lbPressed);
    }

    public static bool Button(Rect pORect, string psText, GUIStyle pOStyle = null)
    {
        return Button(pORect, new GUIContent(psText), pOStyle);
    }

    public static bool Button(Rect pORect, GUIContent pOContent)
    {
        return Button(pORect, pOContent, null);
    }

    /// <summary>GUI.Toggle with the same fallback: the release over it flips it.</summary>
    public static bool Toggle(Rect pORect, bool pbValue, GUIContent pOContent, GUIStyle pOStyle)
    {
        Sync();

        EventType leBefore = Event.current != null ? Event.current.type : EventType.Ignore;
        bool lbNew = pOStyle != null ? GUI.Toggle(pORect, pbValue, pOContent, pOStyle) : GUI.Toggle(pORect, pbValue, pOContent);

        return AfterControl(pORect, leBefore, lbNew != pbValue) ? !pbValue : pbValue;
    }

    public static bool Toggle(Rect pORect, bool pbValue, string psText, GUIStyle pOStyle = null)
    {
        return Toggle(pORect, pbValue, new GUIContent(psText), pOStyle);
    }

    public static bool Toggle(Rect pORect, bool pbValue, GUIContent pOContent)
    {
        return Toggle(pORect, pbValue, pOContent, null);
    }

    /// <summary>
    /// After a control: notes an IMGUI MouseUp it saw (peBefore, the event type before the control
    /// ran) and adds the Linux fallback to its result - also for a layout control, which reads its
    /// rectangle with GUILayoutUtility.GetLastRect afterwards (Rect.zero in the layout pass).
    /// </summary>
    public static bool AfterControl(Rect pORect, EventType peBefore, bool pbPressed)
    {
        if (peBefore == EventType.MouseUp)
        {
            fStartFrame();
            mORectsReleased.Add(pORect);
        }

        return pbPressed || fFallback(pORect, false);
    }

    /// <summary>GUILayout.Button with the pointer sync and the Linux fallback.</summary>
    public static bool LayoutButton(GUIContent pOContent, GUIStyle pOStyle, params GUILayoutOption[] paOptions)
    {
        Sync();

        EventType leBefore = Event.current != null ? Event.current.type : EventType.Ignore;
        bool lbPressed = GUILayout.Button(pOContent, pOStyle, paOptions);
        Rect lORect = Event.current != null && Event.current.type != EventType.Layout ? GUILayoutUtility.GetLastRect() : Rect.zero;

        return AfterControl(lORect, leBefore, lbPressed);
    }

    public static bool LayoutButton(string psText, GUIStyle pOStyle, params GUILayoutOption[] paOptions)
    {
        return LayoutButton(new GUIContent(psText), pOStyle, paOptions);
    }

    /// <summary>
    /// A press of the left button in pORect - for a host's own MouseDown handler (a list row, a
    /// page): IMGUI's MouseDown there, or on the Linux player the Input System's press in the
    /// repaint when IMGUI's did not reach this rectangle this frame (see Button). The caller uses
    /// the event only when it is a MouseDown (Event.current.type).
    /// </summary>
    public static bool PressedIn(Rect pORect)
    {
        Event lOEvent = Event.current;

        if (lOEvent == null || !GUI.enabled)
            return false;

        if (lOEvent.type == EventType.MouseDown && lOEvent.button == 0 && pORect.Contains(lOEvent.mousePosition))
        {
            fStartFrame();
            mORectsPressed.Add(pORect);

            return true;
        }

        return fFallback(pORect, true);
    }

    /// <summary>
    /// The mouse wheel for a GUILayout scroll view, called right after its EndScrollView, on the
    /// Linux player: an IMGUI ScrollWheel the scroll view left unused (it tests its own, wrong
    /// pointer - per user, 2026-10-14: the help's lists did not scroll while the map and the
    /// manual did) moves pOScroll when the synced pointer lies over the scroll view; and when no
    /// IMGUI ScrollWheel reached any host this frame, the Input System's wheel does, in the repaint.
    /// </summary>
    public static Vector2 WheelAfterScrollView(Vector2 pOScroll)
    {
        Event lOEvent = Event.current;
        Mouse lOMouse = Mouse.current;

        if (Application.platform != RuntimePlatform.LinuxPlayer || lOEvent == null)
            return pOScroll;

        if (lOEvent.type == EventType.ScrollWheel)
        {
            Sync();

            if (!GUILayoutUtility.GetLastRect().Contains(lOEvent.mousePosition))
                return pOScroll;

            pOScroll.y = Mathf.Max(0f, pOScroll.y + (lOEvent.delta.y * WheelStep));
            lOEvent.Use();

            return pOScroll;
        }

        if (lOEvent.type != EventType.Repaint || lOMouse == null || miWheelEventFrame == Time.frameCount)
            return pOScroll;

        float lfWheel = lOMouse.scroll.ReadValue().y;

        if (Mathf.Abs(lfWheel) < 0.01f || !GUILayoutUtility.GetLastRect().Contains(lOEvent.mousePosition))
            return pOScroll;

        // IMGUI moves a scroll view 20 units per wheel step. The Input System's value per step is
        // 120 on Windows; what X11 reports was not measured, so a value below 120 counts as one step.
        float lfSteps = Mathf.Max(1f, Mathf.Abs(lfWheel) / 120f);

        pOScroll.y = Mathf.Max(0f, pOScroll.y - (Mathf.Sign(lfWheel) * lfSteps * WheelStep));

        return pOScroll;
    }

    private const float WheelStep = 20f;

    /// <summary>The frame an IMGUI ScrollWheel event last reached a host (BeginOnGUI).</summary>
    private static int miWheelEventFrame = -1;

    /// <summary>The controls (by their rectangle, the same in every pass of a frame) an IMGUI
    /// MouseUp, or a MouseDown for PressedIn, reached this frame.</summary>
    private static readonly System.Collections.Generic.List<Rect> mORectsReleased = new System.Collections.Generic.List<Rect>();

    private static readonly System.Collections.Generic.List<Rect> mORectsPressed = new System.Collections.Generic.List<Rect>();

    private static int miRectsFrame = -1;

    private static void fStartFrame()
    {
        if (miRectsFrame == Time.frameCount)
            return;

        miRectsFrame = Time.frameCount;
        mORectsReleased.Clear();
        mORectsPressed.Clear();
    }

    private static int miLoggedFallbacks;

    /// <summary>The Linux fallback: in the repaint, the Input System's release (pbPress false) or
    /// press of the left button over pORect, unless IMGUI's own event reached it this frame.</summary>
    private static bool fFallback(Rect pORect, bool pbPress)
    {
        Event lOEvent = Event.current;
        Mouse lOMouse = Mouse.current;

        if (Application.platform != RuntimePlatform.LinuxPlayer || lOEvent == null || lOEvent.type != EventType.Repaint
            || lOMouse == null || !GUI.enabled
            || !(pbPress ? lOMouse.leftButton.wasPressedThisFrame : lOMouse.leftButton.wasReleasedThisFrame)
            || !pORect.Contains(lOEvent.mousePosition))
            return false;

        fStartFrame();

        if ((pbPress ? mORectsPressed : mORectsReleased).Contains(pORect))
            return false;

        if (miLoggedFallbacks < MaxLoggedEvents)
        {
            miLoggedFallbacks++;
            Debug.Log(string.Format("UWImguiInput: Input System {0} on rect {1} pointer {2} taken (no IMGUI event for it this frame)",
                pbPress ? "press" : "release", pORect, lOEvent.mousePosition));
        }

        return true;
    }

    /// <summary>Called first in OnGUI, while GUI.matrix is still the identity.</summary>
    public static void BeginOnGUI()
    {
        if (Event.current != null && Event.current.type == EventType.ScrollWheel)
            miWheelEventFrame = Time.frameCount;

        fApply(Matrix4x4.identity, true);
    }

    /// <summary>Called right after the host sets GUI.matrix.</summary>
    public static void AfterMatrix()
    {
        fApply(GUI.matrix, false);
    }

    /// <summary>
    /// Called before a control and after every group, area, scroll view or window begins or ends
    /// (per user, 2026-10-12: the first fix made the bar and most dialogs click, but not "Done"
    /// behind the Display and Controls dialogs' scroll lists, nor the modern game menu inside its
    /// GUILayout area - Unity recomputes the event's position from its own wrong one whenever a
    /// clip is pushed or popped). The Input System's pointer goes through GUIUtility.ScreenToGUIPoint,
    /// i.e. through the matrix and every clip open at this moment.
    /// </summary>
    public static void Sync()
    {
        Event lOEvent = Event.current;
        Mouse lOMouse = Mouse.current;

        if (lOEvent == null || lOMouse == null)
            return;

        EventType leType = lOEvent.type;

        if (!lOEvent.isMouse && leType != EventType.Repaint && leType != EventType.ScrollWheel)
            return;

        Vector2 lORaw = lOMouse.position.ReadValue();
        Vector2 lOWanted = GUIUtility.ScreenToGUIPoint(new Vector2(lORaw.x, Screen.height - lORaw.y));

        if ((lOEvent.mousePosition - lOWanted).sqrMagnitude > Tolerance * Tolerance)
            lOEvent.mousePosition = lOWanted;
    }

    private static void fApply(Matrix4x4 pOMatrix, bool pbLog)
    {
        Event lOEvent = Event.current;
        Mouse lOMouse = Mouse.current;

        if (lOEvent == null || lOMouse == null)
            return;

        EventType leType = lOEvent.type;

        if (!lOEvent.isMouse && leType != EventType.Repaint && leType != EventType.ScrollWheel)
            return;

        Vector2 lORaw = lOMouse.position.ReadValue();
        Vector3 lOScreen = new Vector3(lORaw.x, Screen.height - lORaw.y, 0f);
        Vector2 lOWanted = pOMatrix.inverse.MultiplyPoint3x4(lOScreen);
        Vector2 lOImgui = lOEvent.mousePosition;
        bool lbDiffers = (lOImgui - lOWanted).sqrMagnitude > Tolerance * Tolerance;

        if (pbLog && (leType == EventType.MouseDown || leType == EventType.MouseUp) && miLoggedEvents < MaxLoggedEvents)
        {
            miLoggedEvents++;
            Debug.Log(string.Format("UWImguiInput: {0} at IMGUI {1}, Input System {2}{3}", leType, lOImgui, lOScreen,
                lbDiffers ? " - IMGUI follows the Input System" : string.Empty));
        }

        if (!lbDiffers)
            return;

        if (!mbCorrectionLogged)
        {
            mbCorrectionLogged = true;
            Debug.Log(string.Format("UWImguiInput: IMGUI pointer {0} corrected to the Input System's {1} (screen {2}x{3}, system {4}x{5}, mode {6})",
                lOImgui, lOScreen, Screen.width, Screen.height, Display.main.systemWidth, Display.main.systemHeight,
                Screen.fullScreenMode));
        }

        lOEvent.mousePosition = lOWanted;
    }
}
