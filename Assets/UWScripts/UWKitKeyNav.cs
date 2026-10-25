using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE ARROW KEYS ON THE KIT (cleanup 8b.1, 2026-10-24): UWImguiKeyNav's fallback for a mouse that
/// cannot click (per user, 2026-10-12: "the menus should be steerable by the arrow keys as a
/// fallback", after the Linux 4K report where the mouse could not click our menus), for the windows
/// drawn on the kit (UWKitRows) - the same rules. The entries count themselves in drawing order; once
/// the navigation is on (Activate, or the first arrow where the host allows that) Up or Left steps
/// back, Down or Right on, both round the end; Enter, the keypad's Enter or Space presses the entry
/// with the focus, which wears the same gold frame; a press that makes new entries appear (a window
/// opening) puts the focus on the first of them. Nothing here goes through the pointer.
///
/// THE KEYS come from the Input System - the kit sees no IMGUI events - and REPEAT while held, as the
/// system's key repeat did for IMGUI's KeyDown: once on the press, again after RepeatDelay, then every
/// RepeatEvery (ours: the system's own rate is not readable from here). The repeat is UWRepeat's since
/// cleanup step 7.3 (2026-10-25), with this class's numbers and rule (AccumulateOrNow).
///
/// The hand-over to entries still drawn with IMGUI (FollowerFocus, FollowerPress and
/// UWImguiKeyNav.BeginFollowing, while phase 8b moved the windows one by one) went with UWImguiKeyNav
/// in cleanup 8b.4: every window steps on the kit.
/// </summary>
public sealed class UWKitKeyNav
{
    /// <summary>The focus's gold frame (UWImguiKeyNav's until cleanup 8b.4).</summary>
    public static readonly Color FocusColour = new Color(1f, 0.82f, 0.3f, 1f);

    public const float FocusFrame = 2f;

    /// <summary>A held arrow steps again after this (seconds)...</summary>
    private const float RepeatDelay = 0.4f;

    /// <summary>...and then every this.</summary>
    private const float RepeatEvery = 0.06f;

    private int miFocus = -1;

    /// <summary>The kit's entries counted in this frame so far.</summary>
    private int miCount;

    /// <summary>The entries of the last finished frame: the steps go round it.</summary>
    private int miLastTotal;

    private bool mbPress;

    /// <summary>A press happened: the next frame that has more entries puts the focus on the first
    /// new one.</summary>
    private bool mbFollowNew;

    private int miCountAtPress;

    /// <summary>The direction held (-1 back, 1 on, 0 none) and when it steps again: the next repeat
    /// RepeatEvery after the last one's due time, from now where that is already past.</summary>
    private readonly UWRepeat mORepeat = new UWRepeat(RepeatDelay, RepeatEvery, UWRepeat.RuleEnum.AccumulateOrNow);

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

    /// <summary>At the start of the host's frame. pbTakesKeys: the host may react to keys now (not
    /// while text is typed); pbArrowActivates: the first arrow switches the navigation on.</summary>
    public void Begin(bool pbTakesKeys, bool pbArrowActivates)
    {
        miCount = 0;
        mbPress = false;

        Keyboard lOKeyboard = Keyboard.current;

        if (!pbTakesKeys || lOKeyboard == null)
        {
            mORepeat.Reset();
            return;
        }

        int liStep = fStep(lOKeyboard);

        if (liStep != 0)
        {
            if (miFocus < 0)
            {
                if (pbArrowActivates)
                    miFocus = liStep > 0 ? 0 : Mathf.Max(0, miLastTotal - 1);
            }
            else if (miLastTotal > 0)
                miFocus = (miFocus + liStep + miLastTotal) % miLastTotal;

            mbFollowNew = false;
        }

        if (miFocus >= 0 && (lOKeyboard.enterKey.wasPressedThisFrame || lOKeyboard.numpadEnterKey.wasPressedThisFrame
            || lOKeyboard.spaceKey.wasPressedThisFrame))
            mbPress = true;
    }

    /// <summary>The arrows: -1, 1 or 0 this frame, with the repeat of a held one.</summary>
    private int fStep(Keyboard pOKeyboard)
    {
        bool lbBackDown = pOKeyboard.upArrowKey.wasPressedThisFrame || pOKeyboard.leftArrowKey.wasPressedThisFrame;
        bool lbOnDown = pOKeyboard.downArrowKey.wasPressedThisFrame || pOKeyboard.rightArrowKey.wasPressedThisFrame;
        float lfNow = Time.unscaledTime;

        if (lbBackDown || lbOnDown)
        {
            int liPressed = lbOnDown ? 1 : -1;

            mORepeat.Tick(liPressed, true, true, lfNow);

            return liPressed;
        }

        int liHeld = mORepeat.Held;

        if (liHeld == 0)
            return 0;

        bool lbStillDown = liHeld < 0
            ? pOKeyboard.upArrowKey.isPressed || pOKeyboard.leftArrowKey.isPressed
            : pOKeyboard.downArrowKey.isPressed || pOKeyboard.rightArrowKey.isPressed;

        return mORepeat.Tick(liHeld, false, lbStillDown, lfNow) ? liHeld : 0;
    }

    /// <summary>A kit entry in drawing order: pbFocused when it has the focus (the host frames it);
    /// true when a key pressed it.</summary>
    public bool Entry(out bool pbFocused)
    {
        int liIndex = miCount++;

        pbFocused = liIndex == miFocus;

        if (!pbFocused || !mbPress)
            return false;

        mbPress = false;
        mbFollowNew = true;
        miCountAtPress = miLastTotal;

        return true;
    }

    /// <summary>At the end of the host's frame.</summary>
    public void End()
    {
        int liTotal = miCount;

        // A press with the focus beyond this frame's entries (they became fewer): the next frame that
        // has more puts the focus on the first new one, as a press that opened something (this was
        // the hand-over to the IMGUI entries until cleanup 8b.4; kept as it acted).
        if (mbPress && miFocus >= miCount)
        {
            mbFollowNew = true;
            miCountAtPress = miLastTotal;
        }

        mbPress = false;

        if (mbFollowNew && liTotal > miCountAtPress)
        {
            mbFollowNew = false;
            miFocus = miCountAtPress;
        }

        if (miFocus >= liTotal && liTotal > 0)
            miFocus = liTotal - 1;

        miLastTotal = liTotal;
    }
}
