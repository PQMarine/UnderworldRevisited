using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// THE CONTROLS DIALOG'S GAMEPAD TAB (per user, 2026-10-07: "a controller menu with the functions
/// bound by the player"): the button names (Xbox, PlayStation, Nintendo - chosen, never detected,
/// UWBindingInfo.ButtonName), the right stick's look speed and the gamepad's functions, each on a
/// button of the player's choice (UWKeyBindings.PadEntries). The sticks are no buttons: they can
/// be swapped, and the look reversed up and down (per user, the same day: "wanted above all by
/// left-handed players").
/// </summary>
public partial class UWSetupMenu
{
    /// <summary>The Controls dialog shows the gamepad's tab.</summary>
    private bool mbControlsGamepadTab;

    private Vector2 mOGamepadScroll;

    /// <summary>A choice row of the wide dialogs (the button names here, the palette presets): the
    /// label 220 wide, the choices from 230 over the row less 230.</summary>
    private static readonly UWChoiceColumns WideChoiceColumns = new UWChoiceColumns(0f, 220f, 230f, 230f);

    /// <summary>A deadzone row: the label 220 wide, the slider from 230 over the row less 470, the
    /// arrows 230 and the value with the stick's deflection 165 from the right edge.</summary>
    private static readonly UWSliderColumns DeadzoneColumns = new UWSliderColumns(0f, 220f, 230f, 470f, 230f, 165f, 165f);

    private void fDrawGamepadTab(Rect pODialog, float pfLeft, float pfInner, float pfTop)
    {
        // The hint, the button names, the stick's speed, the sticks, the hints and the deadzones
        // (fBuildGamepadPage), then the gamepad's functions down to the dialog's bottom.
        float lfTop = mOPassPages.Draw(mOGamepadPage, pfLeft, pfTop, pfInner);

        Rect lOListRect = new Rect(pfLeft, lfTop, pfInner, pODialog.yMax - 90f - lfTop);
        const float RowHeight = 36f;
        Rect lOContent = new Rect(0f, 0f, lOListRect.width - 20f, UWKeyBindings.PadEntries.Count * RowHeight);

        mOGamepadScroll = mOPassRows.BeginScroll(lOListRect, mOGamepadScroll, lOContent);

        for (int liAt = 0; liAt < UWKeyBindings.PadEntries.Count; liAt++)
        {
            UWKeyBindings.Entry lOEntry = UWKeyBindings.PadEntries[liAt];

            // The button's glyph in the chosen style left of its name (UWGlyphs).
            Texture2D lOGlyph = UWGlyphs.ForPath(UWBindingInfo.Path(lOEntry));
            string lsButton = mOListeningFor == lOEntry ? UWText.T("setup.gamepad.press_button") : UWBindingInfo.Name(lOEntry);

            if (mOPassRows.Rebind(new Rect(0f, liAt * RowHeight, lOContent.width, RowHeight), lOEntry.Label, lsButton, lOGlyph, true)
                && mOListeningFor == null)
                fListenForPad(lOEntry);
        }

        mOPassRows.EndScroll();

        mOPassRows.Info(new Rect(pfLeft, pODialog.yMax - 82f, pfInner, 22f), msBindingNotice, UWRowInfoEnum.Dim);

        float lfButtonTop = pODialog.yMax - 46f;

        mOPassRows.Enabled = UWKeyBindings.HasGamepadChanges && mOListeningFor == null;

        // As wide as its text on the kit (cleanup 8b.2).
        string lsDefault = UWText.T("setup.controls.back_to_default");

        if (mOPassRows.Action(new Rect(pfLeft, lfButtonTop, mOPassRows.FitWidth(lsDefault, 180f), 30f), lsDefault))
        {
            UWKeyBindings.ResetGamepad();
            msBindingNotice = string.Empty;
        }

        mOPassRows.Enabled = true;

        if (mOPassRows.Done(pODialog))
        {
            meOpenDialog = DialogEnum.None;
            mOListeningFor = null;
        }
    }

    /// <summary>A deadzone row: the slider in steps of one per cent (the setting's step), the
    /// arrows, the value and the stick's deflection now ("no pad" without one). A change is stored,
    /// saved and the bindings put anew (the entry's OnChange).</summary>
    private static UWSetupRow fDeadzoneRow(UWFloatSettingDef pODef, bool pbLeft)
    {
        return new UWSetupRow
        {
            Kind = UWSetupRow.KindEnum.Slider, Id = pODef.LabelId, Def = pODef, SliderColumns = DeadzoneColumns, Advance = 30f,
            FormatWith = pfValue => Mathf.RoundToInt(pfValue * 100f) + " %  (" + fStickNow(pbLeft) + ")"
        };
    }

    /// <summary>The stick's deflection right now, or "no pad".</summary>
    private static string fStickNow(bool pbLeft)
    {
        Gamepad lOPad = Gamepad.current;
        float lfNow = lOPad != null ? (pbLeft ? lOPad.leftStick.ReadValue() : lOPad.rightStick.ReadValue()).magnitude : -1f;

        return lfNow < 0f ? UWText.T("setup.gamepad.no_pad") : UWText.F("setup.gamepad.stick_now", Mathf.RoundToInt(lfNow * 100f));
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
                ? UWText.F("setup.gamepad.conflict_button", lOConflict.Label)

                : string.Empty;
        });
    }
}
