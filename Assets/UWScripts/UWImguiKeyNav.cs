using UnityEngine;

/// <summary>
/// THE ARROW KEYS AS A FALLBACK FOR AN IMGUI MENU (per user, 2026-10-12, after the Linux 4K report
/// where the mouse could not click our menus: "the menus should be steerable by the arrow keys as a
/// fallback"). One instance per menu host. The host draws its buttons and toggles through Button and
/// Toggle instead of GUI.Button/GUI.Toggle; they count themselves in drawing order, and one of them
/// holds the keyboard focus: Up or Left steps back, Down or Right on, Enter or Space presses it. The
/// focus is marked with a gold frame. Nothing here goes through the pointer, so it works where the
/// pointer does not (the gamepad stepper, UWPadEntryStepper, moves the real pointer and clicks).
///
/// The host calls Begin first in its drawing pass (after its early returns) and End last. A key is
/// taken only while the navigation is active (Activate, or the first arrow when the host allows
/// that) and no text field holds the keyboard. When a press makes new entries appear (a menu
/// opening), the focus moves to the first of them.
/// </summary>
public sealed class UWImguiKeyNav
{
    private static readonly Color FocusColour = new Color(1f, 0.82f, 0.3f, 1f);

    private const float FocusFrame = 2f;

    private int miFocus = -1;

    private int miCount;

    private int miLastCount;

    private bool mbPress;

    /// <summary>A press happened: the next pass that has more entries puts the focus on the
    /// first new one.</summary>
    private bool mbFollowNew;

    private int miCountAtPress;

    public bool IsActive => miFocus >= 0;

    /// <summary>Puts the focus on the first entry.</summary>
    public void Activate()
    {
        miFocus = 0;
        mbPress = false;
        mbFollowNew = false;
    }

    public void Reset()
    {
        miFocus = -1;
        mbPress = false;
        mbFollowNew = false;
    }

    /// <summary>At the start of the host's drawing pass. pbTakesKeys: the host may react to keys
    /// now; pbArrowActivates: the first arrow switches the navigation on.</summary>
    public void Begin(bool pbTakesKeys, bool pbArrowActivates)
    {
        miCount = 0;
        mbPress = false;

        Event lOEvent = Event.current;

        if (!pbTakesKeys || lOEvent == null || lOEvent.type != EventType.KeyDown || GUIUtility.keyboardControl != 0)
            return;

        int liStep = 0;

        switch (lOEvent.keyCode)
        {
            case KeyCode.UpArrow:
            case KeyCode.LeftArrow:
                liStep = -1;
                break;

            case KeyCode.DownArrow:
            case KeyCode.RightArrow:
                liStep = 1;
                break;

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
            case KeyCode.Space:
                if (miFocus >= 0)
                    mbPress = true;

                return;

            default:
                return;
        }

        if (miFocus < 0)
        {
            if (!pbArrowActivates)
                return;

            miFocus = liStep > 0 ? 0 : Mathf.Max(0, miLastCount - 1);
        }
        else if (miLastCount > 0)
            miFocus = (miFocus + liStep + miLastCount) % miLastCount;

        mbFollowNew = false;
        lOEvent.Use();
    }

    /// <summary>At the end of the host's drawing pass.</summary>
    public void End()
    {
        if (mbFollowNew && miCount > miCountAtPress && Event.current != null && Event.current.type == EventType.Layout)
        {
            mbFollowNew = false;
            miFocus = miCountAtPress;
        }

        if (miFocus >= miCount && miCount > 0 && Event.current != null && Event.current.type == EventType.Layout)
            miFocus = miCount - 1;

        miLastCount = miCount;
        mbPress = false;
    }

    // The controls themselves are UWImguiInput's (the pointer sync and the Linux fallback); this
    // class adds the keyboard focus.
    public bool Button(Rect pORect, string psText)
    {
        return fButton(pORect, UWImguiInput.Button(pORect, psText));
    }

    public bool Button(Rect pORect, string psText, GUIStyle pOStyle)
    {
        return fButton(pORect, UWImguiInput.Button(pORect, psText, pOStyle));
    }

    public bool Button(Rect pORect, GUIContent pOContent)
    {
        return fButton(pORect, UWImguiInput.Button(pORect, pOContent));
    }

    public bool Button(Rect pORect, GUIContent pOContent, GUIStyle pOStyle)
    {
        return fButton(pORect, UWImguiInput.Button(pORect, pOContent, pOStyle));
    }

    public bool Toggle(Rect pORect, bool pbValue, string psText)
    {
        return fToggle(pORect, pbValue, UWImguiInput.Toggle(pORect, pbValue, psText));
    }

    public bool Toggle(Rect pORect, bool pbValue, string psText, GUIStyle pOStyle)
    {
        return fToggle(pORect, pbValue, UWImguiInput.Toggle(pORect, pbValue, psText, pOStyle));
    }

    public bool Toggle(Rect pORect, bool pbValue, GUIContent pOContent)
    {
        return fToggle(pORect, pbValue, UWImguiInput.Toggle(pORect, pbValue, pOContent));
    }

    public bool Toggle(Rect pORect, bool pbValue, GUIContent pOContent, GUIStyle pOStyle)
    {
        return fToggle(pORect, pbValue, UWImguiInput.Toggle(pORect, pbValue, pOContent, pOStyle));
    }

    /// <summary>GUILayout.Button with the focus, and UWImguiInput's pointer sync and Linux fallback.</summary>
    public bool LayoutButton(GUIContent pOContent, GUIStyle pOStyle, params GUILayoutOption[] paOptions)
    {
        bool lbPressed = UWImguiInput.LayoutButton(pOContent, pOStyle, paOptions);
        Rect lORect = Event.current != null && Event.current.type != EventType.Layout ? GUILayoutUtility.GetLastRect() : Rect.zero;

        return fButton(lORect, lbPressed);
    }

    public bool LayoutButton(string psText, GUIStyle pOStyle, params GUILayoutOption[] paOptions)
    {
        return LayoutButton(new GUIContent(psText), pOStyle, paOptions);
    }

    private bool fButton(Rect pORect, bool pbClicked)
    {
        int liIndex = miCount++;

        if (liIndex != miFocus)
            return pbClicked;

        fMark(pORect);

        if (!mbPress)
            return pbClicked;

        mbPress = false;
        mbFollowNew = true;
        miCountAtPress = miLastCount;

        if (Event.current != null)
            Event.current.Use();

        return true;
    }

    private bool fToggle(Rect pORect, bool pbBefore, bool pbAfter)
    {
        return fButton(pORect, false) ? !pbBefore : pbAfter;
    }

    private static void fMark(Rect pORect)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint || pORect.width <= 0f)
            return;

        Color lOBefore = GUI.color;

        GUI.color = FocusColour;
        GUI.DrawTexture(new Rect(pORect.x, pORect.y, pORect.width, FocusFrame), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(pORect.x, pORect.yMax - FocusFrame, pORect.width, FocusFrame), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(pORect.x, pORect.y, FocusFrame, pORect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(pORect.xMax - FocusFrame, pORect.y, FocusFrame, pORect.height), Texture2D.whiteTexture);
        GUI.color = lOBefore;
    }
}
