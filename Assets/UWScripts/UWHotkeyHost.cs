using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// The Unity side of a hotkey site (cleanup step 7.5, 2026-10-25): what every site answers alike -
/// the scheme, the modifiers held now, an act's press or performance through UWInput, the debug
/// tools' raw F7 and F10, whether a press came from the keyboard. Each site (UWGameUI,
/// UWControlScheme, UWHelpWindow, UWModernPanel, DebugFunctions, UWDebugOverlay, UWDebugTools) adds
/// its gates and handlers and routes its rows of UWHotkeys from its own Update.
/// </summary>
public abstract class UWHotkeyHost : IUWHotkeyHost
{
    /// <summary>The controls the site reads its acts from (the player's, or a debug instance).</summary>
    protected abstract UWControls Controls { get; }

    public bool IsModern => UWControlScheme.IsModern;

    public UWHotkeyMods Mods => HeldMods;

    /// <summary>The modifiers held now, for every site and for the shortcut modifier's rule
    /// (UWControls, cleanup 7 wave 2b).</summary>
    public static UWHotkeyMods HeldMods
    {
        get
        {
            UWHotkeyMods leMods = UWHotkeyMods.None;

            if (UWControls.IsShiftHeld)
                leMods |= UWHotkeyMods.Shift;

            if (UWControls.IsAltHeld)
                leMods |= UWHotkeyMods.Alt;

            if (UWControls.IsCtrlHeld)
                leMods |= UWHotkeyMods.Ctrl;

            if (UWControls.IsRightCtrlHeld)
                leMods |= UWHotkeyMods.RightCtrl;

            return leMods;
        }
    }

    public virtual bool Fired(UWHotkeyRow pORow)
    {
        Keyboard lOKeyboard = Keyboard.current;

        switch (pORow.Trigger)
        {
            case UWHotkeyTrigger.Pressed:
                return UWInput.Pressed(Controls, pORow.Act);

            case UWHotkeyTrigger.Performed:
                return UWInput.Performed(Controls, pORow.Act);

            case UWHotkeyTrigger.Always:
                return true;

            case UWHotkeyTrigger.RawF7:
                return lOKeyboard != null && lOKeyboard.f7Key.wasPressedThisFrame;

            case UWHotkeyTrigger.RawF10:
                return lOKeyboard != null && lOKeyboard.f10Key.wasPressedThisFrame;

            default:
                // The back press is the site's own (UWGameUI).
                return false;
        }
    }

    public bool FromKeyboard(UWAct peAct)
    {
        InputAction lOAction = UWInput.Action(Controls, peAct);

        return lOAction != null && lOAction.activeControl?.device is Keyboard;
    }

    /// <summary>The router asks the gates here: the ones every site answers alike first - the wait for a
    /// key of the Controls window (cleanup 7, wave 2b) -, then the site's own (Gate).</summary>
    bool IUWHotkeyHost.Gate(UWHotkeyGate peGate, UWHotkeyRow pORow)
    {
        if (peGate == UWHotkeyGate.Rebinding)
            return UWKeyBindings.HoldsKeys;

        return Gate(peGate, pORow);
    }

    public abstract bool Gate(UWHotkeyGate peGate, UWHotkeyRow pORow);

    public abstract bool Run(UWHotkeyHandler peHandler);
}
