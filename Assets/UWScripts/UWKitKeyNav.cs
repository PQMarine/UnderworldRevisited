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
///
/// SLIDERS BY THE KEYS (cleanup 8d.4, per user 2026-10-25: the arrows only moved the focus, so no
/// slider could be changed without the mouse): a slider is an entry of its own (SliderEntry), and with
/// the focus on it Left and Right move its value by its row's step (UWKeyNavRules) with the same repeat,
/// while Up and Down step as before. Whether a held Left or Right moves the slider or the focus is
/// decided on its press - on a slider it slides, elsewhere it steps as before and goes on stepping past
/// a slider it reaches while held, so a held key never starts changing a value it was not pressed on.
/// Left and Right on any other entry step the focus as they always did (the choices of a row are
/// entries of their own in drawing order, so they step from choice to choice).
/// </summary>
public sealed class UWKitKeyNav
{
    /// <summary>The focus's frame, its width; its colour is the theme's (UWGuiTheme.Palette.Focus since
    /// cleanup phase 8d, Plain's the gold this class kept, UWImguiKeyNav's until cleanup 8b.4).</summary>
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

    /// <summary>The arrow held (one of the Key values below, 0 none) and when it acts again: the next
    /// repeat RepeatEvery after the last one's due time, from now where that is already past.</summary>
    private readonly UWRepeat mORepeat = new UWRepeat(RepeatDelay, RepeatEvery, UWRepeat.RuleEnum.AccumulateOrNow);

    // The arrows as the repeat holds them (cleanup 8d.4: each its own, since Left and Right may slide
    // where Up and Down step; until then one direction for Up or Left and one for Down or Right).
    private const int KeyUp = 1;

    private const int KeyDown = 2;

    private const int KeyLeft = 3;

    private const int KeyRight = 4;

    /// <summary>Left (-1) or Right (1) moves the slider with the focus this frame; its SliderEntry
    /// takes it.</summary>
    private int miSlide;

    /// <summary>The held Left or Right moves the slider with the focus, not the focus (decided on its
    /// press: the focus was on a slider).</summary>
    private bool mbHeldSlides;

    /// <summary>The entry with the focus is a slider: in the last finished frame, and in this one so far.</summary>
    private bool mbFocusOnSlider;

    private bool mbFocusOnSliderNow;

    public bool IsActive => miFocus >= 0;

    /// <summary>The entry with the focus in drawing order, -1 while the navigation is off (the kit
    /// shows it whole in a scrolled area when it changes, cleanup 8d.4).</summary>
    public int Focus => miFocus;

    /// <summary>Left or Right moves the slider with the focus this frame (read before the entries).</summary>
    public bool Slides => miSlide != 0;

    /// <summary>Puts the focus on the first entry.</summary>
    public void Activate()
    {
        miFocus = 0;
        mbPress = false;
        mbFollowNew = false;
        mbFocusOnSlider = false;
    }

    public void Reset()
    {
        miFocus = -1;
        mbPress = false;
        mbFollowNew = false;
        mbFocusOnSlider = false;
    }

    /// <summary>At the start of the host's frame. pbTakesKeys: the host may react to keys now (not
    /// while text is typed); pbArrowActivates: the first arrow switches the navigation on.</summary>
    public void Begin(bool pbTakesKeys, bool pbArrowActivates)
    {
        miCount = 0;
        mbPress = false;
        miSlide = 0;
        mbFocusOnSliderNow = false;

        Keyboard lOKeyboard = Keyboard.current;

        if (!pbTakesKeys || lOKeyboard == null)
        {
            mORepeat.Reset();
            return;
        }

        int liKey = fArrow(lOKeyboard, out bool lbPressed);

        if (liKey != 0)
        {
            bool lbAcross = liKey == KeyLeft || liKey == KeyRight;
            int liStep = liKey == KeyUp || liKey == KeyLeft ? -1 : 1;

            if (lbPressed)
                mbHeldSlides = lbAcross && miFocus >= 0 && mbFocusOnSlider;

            if (lbAcross && mbHeldSlides)
                miSlide = liStep;
            else
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
        }

        if (miFocus >= 0 && (lOKeyboard.enterKey.wasPressedThisFrame || lOKeyboard.numpadEnterKey.wasPressedThisFrame
            || lOKeyboard.spaceKey.wasPressedThisFrame))
            mbPress = true;
    }

    /// <summary>The arrow that acts this frame (a Key value, 0 for none), with the repeat of a held
    /// one; pbPressed when it went down now. Pressed together, Down wins, then Right, Up and Left (Down
    /// or Right won until cleanup 8d.4, when the arrows had two directions between them).</summary>
    private int fArrow(Keyboard pOKeyboard, out bool pbPressed)
    {
        float lfNow = Time.unscaledTime;
        int liPressed = pOKeyboard.downArrowKey.wasPressedThisFrame ? KeyDown
            : pOKeyboard.rightArrowKey.wasPressedThisFrame ? KeyRight
            : pOKeyboard.upArrowKey.wasPressedThisFrame ? KeyUp
            : pOKeyboard.leftArrowKey.wasPressedThisFrame ? KeyLeft
            : 0;

        pbPressed = liPressed != 0;

        if (pbPressed)
        {
            mORepeat.Tick(liPressed, true, true, lfNow);

            return liPressed;
        }

        int liHeld = mORepeat.Held;

        if (liHeld == 0)
            return 0;

        return mORepeat.Tick(liHeld, false, fIsDown(pOKeyboard, liHeld), lfNow) ? liHeld : 0;
    }

    private static bool fIsDown(Keyboard pOKeyboard, int piKey)
    {
        switch (piKey)
        {
            case KeyUp: return pOKeyboard.upArrowKey.isPressed;
            case KeyDown: return pOKeyboard.downArrowKey.isPressed;
            case KeyLeft: return pOKeyboard.leftArrowKey.isPressed;
            default: return pOKeyboard.rightArrowKey.isPressed;
        }
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

    /// <summary>
    /// A slider in drawing order (cleanup 8d.4): an entry like the others, pbFocused when it has the
    /// focus (the host frames it); returns -1 or 1 when Left or Right moves it this frame, else 0 (the
    /// host moves the value by its step, UWKeyNavRules.SliderKeyValue). Enter or Space on it press
    /// nothing.
    /// </summary>
    public int SliderEntry(out bool pbFocused)
    {
        int liIndex = miCount++;

        pbFocused = liIndex == miFocus;

        if (!pbFocused)
            return 0;

        mbFocusOnSliderNow = true;
        mbPress = false;

        int liSlide = miSlide;

        miSlide = 0;

        return liSlide;
    }

    /// <summary>At the end of the host's frame.</summary>
    public void End()
    {
        int liTotal = miCount;
        int liFocusDrawn = miFocus;

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

        // The next frame's Left and Right slide only where the focus stays on the slider drawn now.
        mbFocusOnSlider = mbFocusOnSliderNow && miFocus == liFocusDrawn;
        miLastTotal = liTotal;
    }
}
