using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// THE CONTROLS DIALOG'S GAMEPAD TAB (per user, 2026-10-07: "a controller menu with the functions
/// bound by the player"): the button names (Xbox, PlayStation, Nintendo - chosen, never detected,
/// UWGamepad.ButtonName), the right stick's look speed and the gamepad's functions, each on a
/// button of the player's choice (UWKeyBindings.PadEntries). The sticks are no buttons: they can
/// be swapped, and the look reversed up and down (per user, the same day: "wanted above all by
/// left-handed players").
/// </summary>
public partial class UWSetupMenu
{
    /// <summary>The Controls dialog shows the gamepad's tab.</summary>
    private bool mbControlsGamepadTab;

    private Vector2 mOGamepadScroll;

    private void fDrawGamepadTab(Rect pODialog, float pfLeft, float pfInner, float pfTop)
    {
        float lfTop = pfTop;

        lfTop += fDrawHint(pfLeft, lfTop, pfInner,
            "Click an entry to change it, then press the new button on the gamepad. Escape on the keyboard keeps the old one. One stick walks, the other looks (in the classic scheme it turns and tilts) - swap them below.") + 12f;

        // THE BUTTON NAMES: chosen here, never detected (per user: a PlayStation controller often
        // reports as an Xbox one through Steam or DS4Windows).
        string[] lsStyles = { "Xbox", "PlayStation", "Nintendo" };
        float lfStyleWidth = (pfInner - 230f) / lsStyles.Length;

        GUI.Label(new Rect(pfLeft, lfTop, 220f, 26f), "Button names", mOListLabel);

        for (int liStyle = 0; liStyle < lsStyles.Length; liStyle++)
        {
            bool lbCurrent = (int)UWGamepad.ButtonStyle == liStyle;

            if (GUI.Button(new Rect(pfLeft + 230f + (liStyle * lfStyleWidth), lfTop, lfStyleWidth, 26f), lsStyles[liStyle],
                lbCurrent ? mOCurrentItem : mOListItem) && !lbCurrent)
            {
                UWUserSettings.GamepadButtonStyle = liStyle;
                UWUserSettings.Save();
            }
        }

        lfTop += 32f;

        // THE STICK'S LOOK SPEED, the same row as the mouse's on the other tab.
        float lfStep = UWUserSettings.MouseLookSpeedStep;
        float lfBefore = UWUserSettings.StickLookSpeed;

        GUI.Label(new Rect(pfLeft, lfTop, 220f, 26f), "Stick look speed", mOListLabel);

        float lfSpeed = GUI.HorizontalSlider(new Rect(pfLeft + 230f, lfTop + 8f, pfInner - 400f, 20f),
            lfBefore, UWUserSettings.MinStickLookSpeed, UWUserSettings.MaxStickLookSpeed);

        lfSpeed = Mathf.Round(lfSpeed / lfStep) * lfStep;

        if (GUI.Button(new Rect(pfLeft + pfInner - 160f, lfTop, 26f, 26f), "<", mOButton))
            lfSpeed = lfBefore - lfStep;

        if (GUI.Button(new Rect(pfLeft + pfInner - 130f, lfTop, 26f, 26f), ">", mOButton))
            lfSpeed = lfBefore + lfStep;

        GUI.Label(new Rect(pfLeft + pfInner - 95f, lfTop, 95f, 26f), Mathf.RoundToInt(lfSpeed * 100f) + " %", mOListLabel);

        if (Mathf.RoundToInt(lfSpeed * 100f) != Mathf.RoundToInt(lfBefore * 100f))
        {
            UWUserSettings.StickLookSpeed = lfSpeed;
            UWUserSettings.Save();
        }

        lfTop += 32f;

        // THE STICKS: swapped for left-handed players, the look reversed up and down.
        // The sticks' presses go with them where the player has not changed them (UWKeyBindings).
        bool lbSwap = GUI.Toggle(new Rect(pfLeft, lfTop, 300f, 26f), UWUserSettings.GamepadSwapSticks,
            "  Swap the sticks (left-handed)", mOToggle);

        if (lbSwap != UWUserSettings.GamepadSwapSticks)
        {
            UWUserSettings.GamepadSwapSticks = lbSwap;
            UWUserSettings.Save();
            UWControls.ApplyKeyBindings();
        }

        bool lbInvert = GUI.Toggle(new Rect(pfLeft + 310f, lfTop, pfInner - 310f, 26f), UWUserSettings.StickInvertY,
            "  Invert looking up and down", mOToggle);

        if (lbInvert != UWUserSettings.StickInvertY)
        {
            UWUserSettings.StickInvertY = lbInvert;
            UWUserSettings.Save();
        }

        lfTop += 32f;

        // THE BUTTON HINTS in the modern interface (per user, 2026-10-08).
        bool lbHints = GUI.Toggle(new Rect(pfLeft, lfTop, pfInner, 26f), UWUserSettings.GamepadButtonHints,
            "  Button hints in the game (crosshair, action bar)", mOToggle);

        if (lbHints != UWUserSettings.GamepadButtonHints)
        {
            UWUserSettings.GamepadButtonHints = lbHints;
            UWUserSettings.Save();
        }

        lfTop += 32f;

        // THE DEADZONES, one per physical stick (per user, 2026-10-08: sooner or later every
        // controller drifts), with the stick's deflection right now beside them - at rest that is
        // the drift, and the deadzone goes a little above it.
        Gamepad lOPad = Gamepad.current;

        lfTop = fDeadzoneRow(pfLeft, pfInner, lfTop, "Left stick deadzone", UWUserSettings.LeftStickDeadzone,
            lOPad != null ? lOPad.leftStick.ReadValue().magnitude : -1f, true);
        lfTop = fDeadzoneRow(pfLeft, pfInner, lfTop, "Right stick deadzone", UWUserSettings.RightStickDeadzone,
            lOPad != null ? lOPad.rightStick.ReadValue().magnitude : -1f, false);

        lfTop += 4f;

        Rect lOListRect = new Rect(pfLeft, lfTop, pfInner, pODialog.yMax - 90f - lfTop);
        const float RowHeight = 36f;
        Rect lOContent = new Rect(0f, 0f, lOListRect.width - 20f, UWKeyBindings.PadEntries.Count * RowHeight);

        mOGamepadScroll = GUI.BeginScrollView(lOListRect, mOGamepadScroll, lOContent);

        for (int liAt = 0; liAt < UWKeyBindings.PadEntries.Count; liAt++)
        {
            UWKeyBindings.Entry lOEntry = UWKeyBindings.PadEntries[liAt];
            float lfRowTop = liAt * RowHeight;

            GUI.Label(new Rect(6f, lfRowTop + 4f, lOContent.width - 220f, 26f), lOEntry.Label, mOListLabel);

            // The button's glyph in the chosen style left of its name (UWGlyphs).
            Texture2D lOGlyph = UWGlyphs.ForPath(UWKeyBindings.GetPath(lOEntry));

            if (lOGlyph != null)
                GUI.DrawTexture(new Rect(lOContent.width - 206f, lfRowTop, 32f, 32f), lOGlyph);

            string lsButton = mOListeningFor == lOEntry ? "Press a button..." : UWKeyBindings.GetKeyName(lOEntry);

            if (GUI.Button(new Rect(lOContent.width - 170f, lfRowTop + 3f, 160f, 26f), lsButton, mOButton)
                && mOListeningFor == null)
                fListenForPad(lOEntry);
        }

        GUI.EndScrollView();

        GUI.Label(new Rect(pfLeft, pODialog.yMax - 82f, pfInner, 22f), msBindingNotice, mODimLabel);

        float lfButtonTop = pODialog.yMax - 46f;

        GUI.enabled = UWKeyBindings.HasGamepadChanges && mOListeningFor == null;

        if (GUI.Button(new Rect(pfLeft, lfButtonTop, 180f, 30f), "Back to default", mOButton))
        {
            UWKeyBindings.ResetGamepad();
            msBindingNotice = string.Empty;
        }

        GUI.enabled = true;

        if (GUI.Button(new Rect(pODialog.xMax - 130f, lfButtonTop, 110f, 30f), "Done", mOButton))
        {
            mbControlsDialogOpen = false;
            mOListeningFor = null;
        }
    }

    /// <summary>A deadzone row: the slider in steps of one per cent, the arrows, the value and the
    /// stick's deflection now. Returns the next row's top.</summary>
    private float fDeadzoneRow(float pfLeft, float pfInner, float pfTop, string psLabel, float pfBefore, float pfNow, bool pbLeft)
    {
        const float Step = 0.01f;

        GUI.Label(new Rect(pfLeft, pfTop, 220f, 26f), psLabel, mOListLabel);

        float lfValue = GUI.HorizontalSlider(new Rect(pfLeft + 230f, pfTop + 8f, pfInner - 470f, 20f),
            pfBefore, 0f, UWUserSettings.MaxStickDeadzone);

        lfValue = Mathf.Round(lfValue / Step) * Step;

        if (GUI.Button(new Rect(pfLeft + pfInner - 230f, pfTop, 26f, 26f), "<", mOButton))
            lfValue = pfBefore - Step;

        if (GUI.Button(new Rect(pfLeft + pfInner - 200f, pfTop, 26f, 26f), ">", mOButton))
            lfValue = pfBefore + Step;

        string lsNow = pfNow < 0f ? "no pad" : "now " + Mathf.RoundToInt(pfNow * 100f) + " %";

        GUI.Label(new Rect(pfLeft + pfInner - 165f, pfTop, 165f, 26f),
            Mathf.RoundToInt(lfValue * 100f) + " %  (" + lsNow + ")", mOListLabel);

        if (Mathf.RoundToInt(lfValue * 100f) != Mathf.RoundToInt(pfBefore * 100f))
        {
            if (pbLeft)
                UWUserSettings.LeftStickDeadzone = lfValue;
            else
                UWUserSettings.RightStickDeadzone = lfValue;

            UWUserSettings.Save();
            UWControls.ApplyKeyBindings();
        }

        return pfTop + 30f;
    }

    /// <summary>Waits for the next gamepad button; Escape keeps the old one. A button already used
    /// elsewhere is accepted, with a note saying where it also sits.</summary>
    private void fListenForPad(UWKeyBindings.Entry pOEntry)
    {
        mOListeningFor = pOEntry;
        msBindingNotice = string.Empty;

        UWKeyBindings.ListenGamepad(psPath =>
        {
            mOListeningFor = null;

            if (psPath == null)
                return;

            UWKeyBindings.Entry lOConflict = UWKeyBindings.FindConflict(pOEntry, psPath);

            UWKeyBindings.Set(pOEntry, psPath);

            msBindingNotice = lOConflict != null
                ? "This button is also used for \"" + lOConflict.Label + "\"."
                : string.Empty;
        });
    }
}
