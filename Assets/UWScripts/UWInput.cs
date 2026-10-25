using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UWDataImport.UWData;

/// <summary>
/// THE INPUT FACADE (cleanup step 7.4, 2026-10-25; section 8.5 of the cleanup plan): the game asks
/// for an action by its UWAct - Pressed, Held, Released, Performed, Value - instead of reaching into
/// a UWControls instance's struct fields. A THIN WRAPPER over the actions UWControls builds: the same
/// Input System actions, bindings and rebinds (UWKeyBindings), the same frame semantics
/// (WasPressedThisFrame and its kin); nothing is read differently.
///
/// The Player map's acts are read from the player's controls (UWControlScheme.SharedControls). The
/// four UWControls instances stay (each for its reason): UWControlScheme's is the player's, switched
/// off while the classic options or the debug tools are open (UWHudOptions, UWDebugTools);
/// DebugFunctions' and UWDebugOverlay's carry the debug keys, which must work while the player's are
/// off; UWNoclipCamera's lives with the noclip camera, enabled only while it flies. Those three read
/// their own instance through the overloads that take one.
/// </summary>
public static class UWInput
{
    /// <summary>The player's controls, null before the scene has a control scheme component.</summary>
    public static UWControls Controls => UWControlScheme.SharedControls;

    /// <summary>
    /// THE INPUT CONTEXTS IN FORCE NOW (UWInputContext; what each does is UWInputContexts'): the world
    /// at the bottom; UiModal and Conversation from the control scheme's holds (UWControlScheme.
    /// HeldContexts); TextEntry while the typing lock holds (UWControls.IsTextEntryActive, which
    /// UWGameUI puts once a frame from UWTextEntryState and the bags' menu and split box at once);
    /// ScreenMenu over the main menu and the character creation; Cutscene while a sequence plays or
    /// ended in this frame; PadPointer while the pad's pointer has its buttons (PadPointerContext).
    /// Asked anew each time, as the tests it replaces were.
    /// </summary>
    public static UWInputContext Contexts
    {
        get
        {
            UWControlScheme lOScheme = UWControlScheme.Shared;
            UWInputContext leSet = lOScheme != null ? lOScheme.HeldContexts : UWInputContext.World;

            if (UWControls.IsTextEntryActive)
                leSet |= UWInputContext.TextEntry;

            if (UWScreenUi.IsScreenMenuOpen)
                leSet |= UWInputContext.ScreenMenu;

            if (UWScene.IntroPlayer != null && UWScene.IntroPlayer.HoldsKeys)
                leSet |= UWInputContext.Cutscene;

            return leSet | PadPointerContext;
        }
    }

    /// <summary>The PadPointer context alone (cleanup step 7.8): the pad's pointer has its buttons
    /// (UWGamepadPointer.ClicksActive, decided once a frame before every other script; the rule is
    /// UWPressOwners.PadPointerContext).</summary>
    public static UWInputContext PadPointerContext => UWGamepadPointer.ClicksActive ? UWInputContext.PadPointer : UWInputContext.None;

    /// <summary>
    /// THE PAD'S POINTER TAKES THIS PRESS of an action (cleanup step 7.8; UWGamepadPointer.Takes until
    /// then): the PadPointer context holds, the press comes from the pad, and its control is one of the
    /// pointer's buttons (PadClick, PadContext as bound) - the action leaves it to the pointer
    /// (UWInputContexts.PadPointerTakes).
    /// </summary>
    public static bool PadPointerTakes(InputAction pOAction)
    {
        if (pOAction == null)
            return false;

        bool lbFromPad = UWGamepad.IsFromGamepad(pOAction);

        return UWInputContexts.PadPointerTakes(UWInputContext.World | PadPointerContext, lbFromPad,
            lbFromPad && UWGamepadPointer.IsPointerButton(pOAction.activeControl));
    }

    /// <summary>Whether a context is in force now.</summary>
    public static bool In(UWInputContext peContext)
    {
        return (Contexts & peContext) != 0;
    }

    /// <summary>The player's action of an act, null without controls.</summary>
    public static InputAction Action(UWAct peAct)
    {
        return Action(Controls, peAct);
    }

    /// <summary>An instance's action of an act, null without the instance.</summary>
    public static InputAction Action(UWControls pOControls, UWAct peAct)
    {
        return pOControls != null ? pOControls[peAct] : null;
    }

    /// <summary>Pressed in this frame (InputAction.WasPressedThisFrame).</summary>
    public static bool Pressed(UWAct peAct)
    {
        return Pressed(Controls, peAct);
    }

    public static bool Pressed(UWControls pOControls, UWAct peAct)
    {
        InputAction lOAction = Action(pOControls, peAct);

        return lOAction != null && lOAction.WasPressedThisFrame();
    }

    private static readonly UWPressHold msOPressHold = new UWPressHold();

    /// <summary>
    /// A CONTEXT CHANGE TOOK THIS PRESS (cleanup step 7.8; UWModernPointer.SwitchedFrame until then): the
    /// right button that freed or locked the modern pointer (UWModernPointer.Tick) uses nothing else in
    /// that frame - the press is held for the rest of it (UWPressHold); its readers ask PressedUnheld.
    /// </summary>
    public static void HoldPress(UWAct peAct)
    {
        msOPressHold.Hold(peAct, UnityEngine.Time.frameCount);
    }

    /// <summary>Pressed in this frame, and not held by a context change (HoldPress).</summary>
    public static bool PressedUnheld(UWControls pOControls, UWAct peAct)
    {
        return Pressed(pOControls, peAct) && !msOPressHold.Holds(peAct, UnityEngine.Time.frameCount);
    }

    /// <summary>Performed in this frame (InputAction.WasPerformedThisFrame) - for an action with an
    /// interaction, such as the pad's View button tapped for the map and held for the help.</summary>
    public static bool Performed(UWAct peAct)
    {
        return Performed(Controls, peAct);
    }

    public static bool Performed(UWControls pOControls, UWAct peAct)
    {
        InputAction lOAction = Action(pOControls, peAct);

        return lOAction != null && lOAction.WasPerformedThisFrame();
    }

    /// <summary>Released in this frame (InputAction.WasReleasedThisFrame).</summary>
    public static bool Released(UWAct peAct)
    {
        return Released(Controls, peAct);
    }

    public static bool Released(UWControls pOControls, UWAct peAct)
    {
        InputAction lOAction = Action(pOControls, peAct);

        return lOAction != null && lOAction.WasReleasedThisFrame();
    }

    /// <summary>Held down now (InputAction.IsPressed).</summary>
    public static bool Held(UWAct peAct)
    {
        return Held(Controls, peAct);
    }

    public static bool Held(UWControls pOControls, UWAct peAct)
    {
        InputAction lOAction = Action(pOControls, peAct);

        return lOAction != null && lOAction.IsPressed();
    }

    /// <summary>
    /// THE PAD'S BUTTON OF AN ACT'S GAMEPAD BINDING, as it is in force (a rebind included,
    /// UWBindingInfo.PadPath; psBuiltIn where none is known), pressed this frame - read on the pad
    /// itself, not through the action (cleanup step 7.6, 2026-10-25). For the places that read the
    /// pad's buttons by their place until then (the count prompt, the split box, the classic yes/no,
    /// the screens before the game): they follow a rebind of that binding now, and still work where the
    /// player's actions are off (the typing lock leaves the pad's actions on, the classic options'
    /// panel switches every action off) or not yet there.
    /// </summary>
    public static bool PadPressed(UWAct peAct, int piBinding, string psBuiltIn)
    {
        Gamepad lOPad = Gamepad.current;

        if (lOPad == null)
            return false;

        string lsPath = UWBindingInfo.PadPath(UWActionTable.NameOf(peAct), piBinding) ?? psBuiltIn;

        if (string.IsNullOrEmpty(lsPath) || !lsPath.StartsWith(GamepadPrefix))
            return false;

        ButtonControl lOButton = lOPad.TryGetChildControl(lsPath.Substring(GamepadPrefix.Length)) as ButtonControl;

        return lOButton != null && lOButton.wasPressedThisFrame;
    }

    private const string GamepadPrefix = "<Gamepad>/";

    /// <summary>The value now (InputAction.ReadValue) - an axis as float, a stick or the walk keys as
    /// a Vector2; the default without controls.</summary>
    public static T Value<T>(UWAct peAct) where T : struct
    {
        return Value<T>(Controls, peAct);
    }

    public static T Value<T>(UWControls pOControls, UWAct peAct) where T : struct
    {
        InputAction lOAction = Action(pOControls, peAct);

        return lOAction != null ? lOAction.ReadValue<T>() : default(T);
    }
}
