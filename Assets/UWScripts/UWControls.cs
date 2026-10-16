using UnityEngine.InputSystem;

/// <summary>
/// All input action maps of the game: Player (movement/look/interaction),
/// Noclip (free debug camera) and Debug (developer tools).
///
/// Builds the actions directly in C# instead of via a generated .inputactions asset.
/// Reason: the code generation of the Input System importer runs via an
/// AssetPostprocessor that, on the very first editor start after package installation, did not
/// reliably finish before script compilation (no asset, no generated
/// wrapper class, compile errors) - reproducible even after a clean
/// Library rebuild. The code path is independent of the asset import and needs no
/// second attempt.
/// </summary>
public class UWControls : System.IDisposable
{
    public readonly struct PlayerActions
    {
        public readonly InputAction Move;
        public readonly InputAction Look;
        public readonly InputAction LookStick;
        public readonly InputAction CursorDrag;
        public readonly InputAction Interact;
        public readonly InputAction ToggleCombat;
        public readonly InputAction ToggleScheme;
        public readonly InputAction ToggleInventory;

        /// <summary>Height while hovering and flying: E up, Q down - as in the
        /// original.</summary>
        public readonly InputAction HoverHeight;

        /// <summary>Turning via the keyboard: A left, D right. Only in the original scheme -
        /// in the modern one the mouse turns, and there A/D stays strafing.</summary>
        public readonly InputAction Turn;

        /// <summary>Strafing via the keyboard, on the two keys next to the X.
        /// Likewise only in the original scheme.</summary>
        public readonly InputAction Strafe;

        /// <summary>Jumping: J as in the original, plus the space bar (per user,
        /// 2026-09-13).</summary>
        public readonly InputAction Jump;

        /// <summary>Classic scheme: look one step down (1), straight (2), one
        /// step up (3) - as in the original (per user, 2026-09-13).</summary>
        public readonly InputAction LookDown;

        public readonly InputAction LookReset;

        public readonly InputAction LookUp;

        /// <summary>The overview map, if the character carries one - our own key, the original
        /// has none (per user, 2026-09-17).</summary>
        public readonly InputAction ToggleMap;

        /// <summary>Between the original font and a modern one (see UWTextLabel).</summary>
        public readonly InputAction ToggleFont;

        /// <summary>The help window beside the picture (UWHelpWindow) - our own key, Tab since
        /// 2026-09-26 (F1 went back to the original's options); ignored while Alt is held, so
        /// Alt+Tab to another program does not open it (per user).</summary>
        public readonly InputAction ToggleHelp;

        /// <summary>THE ORIGINAL'S FUNCTION KEYS (key table of InitPlayerAndRegisterKeys, read
        /// 2026-09-26): F1 to F6 the command column's steps - options, talk, get, look, fight,
        /// use -, F7 turns the panel, F8 casts the runes on the shelf, F9 uses the track skill
        /// (since 2026-09-27), F10 makes camp. Ignored with Shift, which our own F-keys take, and with
        /// Alt (Alt+F4) and Ctrl (the original's option shortcuts).</summary>
        public readonly InputAction KeyOptions;
        public readonly InputAction KeyTalk;
        public readonly InputAction KeyGet;
        public readonly InputAction KeyLook;
        public readonly InputAction KeyFight;
        public readonly InputAction KeyUse;
        public readonly InputAction KeyPanel;
        public readonly InputAction KeyCast;
        public readonly InputAction KeyTrack;
        public readonly InputAction KeyCamp;

        /// <summary>The letters of the original's Ctrl shortcuts - S, R, M, F, D, Q, bound by
        /// the character as the font and map keys are; UWGameUI takes them only with Ctrl held
        /// (UWHudOptions.ChooseByShortcut). Not in the menu bar: they are the original's.</summary>
        public readonly InputAction OptSave;
        public readonly InputAction OptRestore;
        public readonly InputAction OptMusic;
        public readonly InputAction OptSound;
        public readonly InputAction OptDetail;
        public readonly InputAction OptQuit;

        /// <summary>
        /// The easy movement on the same WASD keys, held together with SHIFT - one step
        /// forward, one back, one turn of 45 degrees each way (UWEasyMovement). The shift is
        /// asked for in UWPlayerMovement, not bound here, so that the actions stay ordinary
        /// buttons. W may step off a ledge, X is the same step without it.
        /// </summary>
        public readonly InputAction EasyForward;

        public readonly InputAction EasyWalkForward;

        public readonly InputAction EasyBack;

        public readonly InputAction EasyTurnLeft;

        public readonly InputAction EasyTurnRight;

        /// <summary>THE MODERN SCHEME'S OWN KEYS (concept per user, 2026-10-03): E does what fits
        /// the thing under the crosshair - talk, use, pick up -, R draws or puts away the weapon
        /// (the left button draws it too), Space and the left Ctrl rise and sink while flying (E is taken), and
        /// Escape opens the game menu. Read only in the modern scheme.</summary>
        public readonly InputAction ModernUse;

        public readonly InputAction ModernReady;

        public readonly InputAction ModernHover;

        public readonly InputAction Menu;

        /// <summary>B: the modern scheme's bag windows (UWModernBags); C (ToggleInventory) is its character panel.
        /// The debug brightness gave B up for it and went to F11 (per user, 2026-10-03).</summary>
        public readonly InputAction ModernBags;

        /// <summary>Q: look at the crosshair's target in the modern scheme (the right button switches
        /// the pointer there, UWModernPointer). In the original scheme Q sinks while flying.</summary>
        public readonly InputAction ModernLook;

        /// <summary>The key left of X (Y on a German keyboard, Z on an English one - bound by its
        /// place, not its letter, per user 2026-10-04): the modern rune panel (UWModernRunePanel).</summary>
        public readonly InputAction ModernRunes;

        /// <summary>1 to 9 and 0: the modern scheme's action bar (UWModernActionBar). In the
        /// original scheme 1 to 3 look down, straight and up - read only there.</summary>
        public readonly InputAction[] ModernSlots;

        /// <summary>The modern scheme's weapon button: the left mouse button and the gamepad's
        /// right trigger (UWGamepad) - CursorDrag stays the mouse's for the windows.</summary>
        public readonly InputAction ModernAttack;

        /// <summary>The gamepad's B (UWGamepad): closes what is open.</summary>
        public readonly InputAction PadBack;

        /// <summary>The gamepad's Y: jumps.</summary>
        public readonly InputAction PadJump;

        /// <summary>The gamepad's left trigger: tapped casts the hollow, held the rune panel.</summary>
        public readonly InputAction PadCast;

        /// <summary>The gamepad's shoulders pick a slot of the action bar, up on the d-pad uses it.</summary>
        public readonly InputAction PadSlotPrevious;

        public readonly InputAction PadSlotNext;

        public readonly InputAction PadSlotUse;

        /// <summary>THE GAMEPAD'S POINTER (UWGamepadPointer): R3 switches it, A clicks, X is the right
        /// button (the wheel is the walk stick over a window, UWGamepadPointer) - read only while it drives.</summary>
        public readonly InputAction PadPointer;

        public readonly InputAction PadClick;

        public readonly InputAction PadContext;

        internal PlayerActions(InputActionMap pMap)
        {
            Move = pMap["Move"];
            Look = pMap["Look"];
            LookStick = pMap["LookStick"];
            CursorDrag = pMap["CursorDrag"];
            Interact = pMap["Interact"];
            ToggleCombat = pMap["ToggleCombat"];
            ToggleScheme = pMap["ToggleScheme"];
            ToggleInventory = pMap["ToggleInventory"];
            HoverHeight = pMap["HoverHeight"];
            Turn = pMap["Turn"];
            Strafe = pMap["Strafe"];
            Jump = pMap["Jump"];
            LookDown = pMap["LookDown"];
            LookReset = pMap["LookReset"];
            LookUp = pMap["LookUp"];
            ToggleMap = pMap["ToggleMap"];
            ToggleFont = pMap["ToggleFont"];
            ToggleHelp = pMap["ToggleHelp"];
            KeyOptions = pMap["KeyOptions"];
            KeyTalk = pMap["KeyTalk"];
            KeyGet = pMap["KeyGet"];
            KeyLook = pMap["KeyLook"];
            KeyFight = pMap["KeyFight"];
            KeyUse = pMap["KeyUse"];
            KeyPanel = pMap["KeyPanel"];
            KeyCast = pMap["KeyCast"];
            KeyTrack = pMap["KeyTrack"];
            KeyCamp = pMap["KeyCamp"];
            OptSave = pMap["OptSave"];
            OptRestore = pMap["OptRestore"];
            OptMusic = pMap["OptMusic"];
            OptSound = pMap["OptSound"];
            OptDetail = pMap["OptDetail"];
            OptQuit = pMap["OptQuit"];
            EasyForward = pMap["EasyForward"];
            EasyWalkForward = pMap["EasyWalkForward"];
            EasyBack = pMap["EasyBack"];
            EasyTurnLeft = pMap["EasyTurnLeft"];
            EasyTurnRight = pMap["EasyTurnRight"];
            ModernUse = pMap["ModernUse"];
            ModernReady = pMap["ModernReady"];
            ModernHover = pMap["ModernHover"];
            Menu = pMap["Menu"];
            ModernBags = pMap["ModernBags"];
            ModernLook = pMap["ModernLook"];
            ModernRunes = pMap["ModernRunes"];
            ModernAttack = pMap["ModernAttack"];
            PadBack = pMap["PadBack"];
            PadJump = pMap["PadJump"];
            PadCast = pMap["PadCast"];
            PadSlotPrevious = pMap["PadSlotPrevious"];
            PadSlotNext = pMap["PadSlotNext"];
            PadSlotUse = pMap["PadSlotUse"];
            PadPointer = pMap["PadPointer"];
            PadClick = pMap["PadClick"];
            PadContext = pMap["PadContext"];

            ModernSlots = new InputAction[ModernSlotCount];

            for (int liSlot = 0; liSlot < ModernSlotCount; liSlot++)
                ModernSlots[liSlot] = pMap["ModernSlot" + (liSlot + 1)];
        }
    }

    public readonly struct NoclipActions
    {
        public readonly InputAction Move;
        public readonly InputAction Look;
        public readonly InputAction Up;
        public readonly InputAction Down;
        public readonly InputAction Sprint;

        internal NoclipActions(InputActionMap pMap)
        {
            Move = pMap["Move"];
            Look = pMap["Look"];
            Up = pMap["Up"];
            Down = pMap["Down"];
            Sprint = pMap["Sprint"];
        }
    }

    public readonly struct DebugActions
    {
        public readonly InputAction ToggleOverlay;
        public readonly InputAction TeleportDebug;
        public readonly InputAction ToggleSpectator;
        public readonly InputAction NextLevel;
        public readonly InputAction PrevLevel;
        public readonly InputAction ToggleBrightness;

        internal DebugActions(InputActionMap pMap)
        {
            ToggleOverlay = pMap["ToggleOverlay"];
            TeleportDebug = pMap["TeleportDebug"];
            ToggleSpectator = pMap["ToggleSpectator"];
            NextLevel = pMap["NextLevel"];
            PrevLevel = pMap["PrevLevel"];
            ToggleBrightness = pMap["ToggleBrightness"];
        }
    }

    // Mouse deltas come raw in pixels per frame; this scaling brings them to the
    // order of magnitude the old Input Manager delivered at Sensitivity 0.1, so that the
    // already tuned mfSensitivityX/Y values in UWPlayerLook/UWNoclipCamera still
    // fit.
    private const string MouseDeltaScale = "scaleVector2(x=0.1,y=0.1)";

    private readonly InputActionMap mPlayerMap;
    private readonly InputActionMap mNoclipMap;
    private readonly InputActionMap mDebugMap;

    /// <summary>All live instances - several scripts keep their own (controls,
    /// noclip, debug), and the key lock while typing must reach all of them.</summary>
    private static readonly System.Collections.Generic.List<UWControls> msInstances
        = new System.Collections.Generic.List<UWControls>();

    private bool mbEnabled;

    /// <summary>
    /// Is text being typed right now - mantra, map note, savegame name, input line,
    /// debug input? Then all actions bound to letter and special keys stay silent
    /// (per user, 2026-09-13: "during text input while chanting a mantra, switch off the
    /// special keys. B N F and whatever else comes along"). Otherwise a
    /// typed A turned the character, and an N switched to noclip. Mouse and clicks remain.
    ///
    /// Set once per frame by UWGameUI. Keys that are polled directly outside this
    /// class (F6, F7, F10) check this value themselves.
    /// </summary>
    public static bool IsTextEntryActive { get; private set; }

    /// <summary>The modifiers as they are held right now - our own F-keys want Shift, the
    /// original's want neither Shift nor Alt, Tab ignores Alt (Alt+Tab), and the original's
    /// Ctrl+letter shortcuts are not ours.</summary>
    public static bool IsShiftHeld => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

    public static bool IsAltHeld => Keyboard.current != null && Keyboard.current.altKey.isPressed;

    public static bool IsCtrlHeld => Keyboard.current != null && Keyboard.current.ctrlKey.isPressed;

    /// <summary>The right Ctrl alone - the modern scheme's Ctrl shortcuts, its left Ctrl sinks.</summary>
    public static bool IsRightCtrlHeld => Keyboard.current != null && Keyboard.current.rightCtrlKey.isPressed;

    public static void SetTextEntryActive(bool pbActive)
    {
        if (IsTextEntryActive == pbActive)
            return;

        IsTextEntryActive = pbActive;

        foreach (UWControls lOControls in msInstances)
            lOControls.fApplyTextEntry();
    }

    /// <summary>The actions that stay silent while typing: everything tied to the keyboard.
    /// The debug map lies entirely on keys.</summary>
    private void fApplyTextEntry()
    {
        if (!mbEnabled)
            return;

        InputAction[] lOKeyboardActions =
        {
            mPlayerMap["Move"], mPlayerMap["ToggleScheme"], mPlayerMap["ToggleInventory"],
            mPlayerMap["HoverHeight"], mPlayerMap["Turn"], mPlayerMap["Strafe"], mPlayerMap["Jump"],
            mPlayerMap["LookDown"], mPlayerMap["LookReset"], mPlayerMap["LookUp"],
            mPlayerMap["ToggleMap"], mPlayerMap["ToggleFont"],
            mPlayerMap["KeyOptions"], mPlayerMap["KeyTalk"], mPlayerMap["KeyGet"], mPlayerMap["KeyLook"],
            mPlayerMap["KeyFight"], mPlayerMap["KeyUse"], mPlayerMap["KeyPanel"], mPlayerMap["KeyCast"],
            mPlayerMap["KeyTrack"], mPlayerMap["KeyCamp"],
            mPlayerMap["OptSave"], mPlayerMap["OptRestore"], mPlayerMap["OptMusic"],
            mPlayerMap["OptSound"], mPlayerMap["OptDetail"], mPlayerMap["OptQuit"],
            mPlayerMap["EasyForward"], mPlayerMap["EasyWalkForward"], mPlayerMap["EasyBack"],
            mPlayerMap["EasyTurnLeft"], mPlayerMap["EasyTurnRight"],
            mPlayerMap["ModernUse"], mPlayerMap["ModernReady"], mPlayerMap["ModernHover"], mPlayerMap["Menu"],
            mPlayerMap["ModernBags"], mPlayerMap["ModernLook"], mPlayerMap["ModernRunes"],
            mPlayerMap["ModernSlot1"], mPlayerMap["ModernSlot2"], mPlayerMap["ModernSlot3"], mPlayerMap["ModernSlot4"],
            mPlayerMap["ModernSlot5"], mPlayerMap["ModernSlot6"], mPlayerMap["ModernSlot7"], mPlayerMap["ModernSlot8"],
            mPlayerMap["ModernSlot9"], mPlayerMap["ModernSlot10"],
            mNoclipMap["Move"], mNoclipMap["Up"], mNoclipMap["Down"], mNoclipMap["Sprint"]
        };

        foreach (InputAction lOAction in lOKeyboardActions)
        {
            if (IsTextEntryActive)
                lOAction.Disable();
            else
                lOAction.Enable();
        }

        if (IsTextEntryActive)
            mDebugMap.Disable();
        else
            mDebugMap.Enable();
    }

    /// <summary>The slots of the modern action bar, on the keys 1 to 0.</summary>
    public const int ModernSlotCount = 10;

    public PlayerActions Player { get; }
    public NoclipActions Noclip { get; }
    public DebugActions Debug { get; }

    public UWControls()
    {
        mPlayerMap = fBuildPlayerMap();
        mNoclipMap = fBuildNoclipMap();
        mDebugMap = fBuildDebugMap();

        Player = new PlayerActions(mPlayerMap);
        Noclip = new NoclipActions(mNoclipMap);
        Debug = new DebugActions(mDebugMap);

        msInstances.Add(this);

        // The keys the player changed in the menu bar (see UWKeyBindings).
        UWKeyBindings.ApplyTo(this);
    }

    /// <summary>Any live instance - for reading the built-in binding of an action
    /// (UWKeyBindings).</summary>
    public static UWControls AnyInstance => msInstances.Count > 0 ? msInstances[0] : null;

    /// <summary>Puts changed keys onto every live instance.</summary>
    public static void ApplyKeyBindings()
    {
        foreach (UWControls lOControls in msInstances)
            UWKeyBindings.ApplyTo(lOControls);
    }

    /// <summary>An action by map and name, or null.</summary>
    public InputAction FindAction(string psMap, string psAction)
    {
        InputActionMap lOMap = psMap == "Noclip" ? mNoclipMap : (psMap == "Debug" ? mDebugMap : mPlayerMap);

        return lOMap.FindAction(psAction);
    }

    public void Enable()
    {
        mPlayerMap.Enable();
        mNoclipMap.Enable();
        mDebugMap.Enable();
        mbEnabled = true;

        // Enabling a map also re-enables the locked actions.
        if (IsTextEntryActive)
            fApplyTextEntry();
    }

    public void Disable()
    {
        mPlayerMap.Disable();
        mNoclipMap.Disable();
        mDebugMap.Disable();
        mbEnabled = false;
    }

    public void Dispose()
    {
        msInstances.Remove(this);

        mPlayerMap.Dispose();
        mNoclipMap.Dispose();
        mDebugMap.Dispose();
    }

    private static InputActionMap fBuildPlayerMap()
    {
        InputActionMap lMap = new InputActionMap("Player");

        InputAction lMove = lMap.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
        lMove.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        lMove.AddBinding("<Gamepad>/leftStick");

        lMap.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2")
            .AddBinding("<Mouse>/delta").WithProcessor(MouseDeltaScale);

        lMap.AddAction("LookStick", InputActionType.Value, expectedControlLayout: "Vector2")
            .AddBinding("<Gamepad>/rightStick");

        lMap.AddAction("CursorDrag", InputActionType.Button)
            .AddBinding("<Mouse>/leftButton");

        // E and Q are OUT here: in the original they belong to hover height (see
        // HoverHeight further down). Use still works via right click, combat mode
        // via a click on the weapon on the paper doll - both the original's way.
        // They may come back in the modern scheme (per user, 2026-09-03).
        // THE GAMEPAD LEFT THESE (UWGamepad, 2026-10-07): A on Interact freed the modern pointer,
        // X drew the weapon, View switched the scheme. ToggleCombat has no binding left.
        InputAction lInteract = lMap.AddAction("Interact", InputActionType.Button);
        lInteract.AddBinding("<Mouse>/rightButton");

        lMap.AddAction("ToggleCombat", InputActionType.Button);

        InputAction lToggleScheme = lMap.AddAction("ToggleScheme", InputActionType.Button);
        lToggleScheme.AddBinding("<Keyboard>/f2");

        // C since 2026-10-03: the modern scheme's character panel with the bags (UWModernBags) -
        // per user, after I; the original scheme reads it nowhere.
        InputAction lToggleInventory = lMap.AddAction("ToggleInventory", InputActionType.Button);
        lToggleInventory.AddBinding("<Keyboard>/#(c)");
        // The pad's d-pad down since 2026-10-07 (per user: Y jumps instead).
        lToggleInventory.AddBinding("<Gamepad>/dpad/down");

        // Height while hovering. Q and E come from the original (per user, 2026-09-03);
        // combat mode and interaction gave up these keys for it.
        InputAction lHoverHeight = lMap.AddAction("HoverHeight", InputActionType.Value, expectedControlLayout: "Axis");
        lHoverHeight.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/q")
            .With("Positive", "<Keyboard>/e");

        // In the original scheme A and D turn, strafing goes via the two keys next to
        // the X (per user, 2026-09-04). Move keeps A/D because the modern scheme uses them to
        // strafe as usual; in the original scheme Move's x axis is
        // ignored there.
        InputAction lTurn = lMap.AddAction("Turn", InputActionType.Value, expectedControlLayout: "Axis");
        lTurn.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/a")
            .With("Positive", "<Keyboard>/d");

        // The hash notation binds to the CHARACTER the key produces, not to
        // its position on a US keyboard - otherwise the key would be somewhere else on a German
        // keyboard.
        InputAction lStrafe = lMap.AddAction("Strafe", InputActionType.Value, expectedControlLayout: "Axis");
        lStrafe.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/#(y)")
            .With("Positive", "<Keyboard>/#(c)");

        // Jumping. J is the original's key; the hash notation binds to the
        // character, as with Strafe above.
        InputAction lJump = lMap.AddAction("Jump", InputActionType.Button);
        lJump.AddBinding("<Keyboard>/space");
        lJump.AddBinding("<Keyboard>/#(j)");

        // Look pitch in the classic scheme: the number keys above the letters.
        lMap.AddAction("LookDown", InputActionType.Button).AddBinding("<Keyboard>/1");
        lMap.AddAction("LookReset", InputActionType.Button).AddBinding("<Keyboard>/2");
        lMap.AddAction("LookUp", InputActionType.Button).AddBinding("<Keyboard>/3");

        // Our own keys: the map (only with a map in the pack) and the font switch. The hash
        // notation binds to the character, as with Strafe above.
        // THE PAD'S VIEW BUTTON (per user, 2026-10-07): tapped the map, held the help - two
        // interactions on the one button, so the readers ask WasPerformedThisFrame (a key
        // without an interaction performs on its press as before).
        InputAction lToggleMap = lMap.AddAction("ToggleMap", InputActionType.Button);
        lToggleMap.AddBinding("<Keyboard>/#(m)");
        lToggleMap.AddBinding("<Gamepad>/select").WithInteraction("tap(duration=0.5)");

        lMap.AddAction("ToggleFont", InputActionType.Button).AddBinding("<Keyboard>/#(f)");

        InputAction lToggleHelp = lMap.AddAction("ToggleHelp", InputActionType.Button);
        lToggleHelp.AddBinding("<Keyboard>/tab");
        lToggleHelp.AddBinding("<Gamepad>/select").WithInteraction("hold(duration=0.5)");

        // The original's function keys (see KeyOptions).
        lMap.AddAction("KeyOptions", InputActionType.Button).AddBinding("<Keyboard>/f1");
        lMap.AddAction("KeyTalk", InputActionType.Button).AddBinding("<Keyboard>/f2");
        lMap.AddAction("KeyGet", InputActionType.Button).AddBinding("<Keyboard>/f3");
        lMap.AddAction("KeyLook", InputActionType.Button).AddBinding("<Keyboard>/f4");
        lMap.AddAction("KeyFight", InputActionType.Button).AddBinding("<Keyboard>/f5");
        lMap.AddAction("KeyUse", InputActionType.Button).AddBinding("<Keyboard>/f6");
        lMap.AddAction("KeyPanel", InputActionType.Button).AddBinding("<Keyboard>/f7");
        lMap.AddAction("KeyCast", InputActionType.Button).AddBinding("<Keyboard>/f8");
        lMap.AddAction("KeyTrack", InputActionType.Button).AddBinding("<Keyboard>/f9");
        lMap.AddAction("KeyCamp", InputActionType.Button).AddBinding("<Keyboard>/f10");

        // The letters of the original's Ctrl shortcuts (see OptSave).
        lMap.AddAction("OptSave", InputActionType.Button).AddBinding("<Keyboard>/#(s)");
        lMap.AddAction("OptRestore", InputActionType.Button).AddBinding("<Keyboard>/#(r)");
        lMap.AddAction("OptMusic", InputActionType.Button).AddBinding("<Keyboard>/#(m)");
        lMap.AddAction("OptSound", InputActionType.Button).AddBinding("<Keyboard>/#(f)");
        lMap.AddAction("OptDetail", InputActionType.Button).AddBinding("<Keyboard>/#(d)");
        lMap.AddAction("OptQuit", InputActionType.Button).AddBinding("<Keyboard>/#(q)");

        // The easy movement (UWEasyMovement). The original hangs it on the UPPERCASE codes of
        // A, D, S, W and X, which is its shift; here the keys are bound plain and the shift is
        // checked in UWPlayerMovement, because the same keys still have to walk without it.
        // W STEPS OFF A LEDGE, X DOES NOT - that is the original's difference between its W
        // and its S (per user, who found it by trying, 2026-09-21). Our S is the modern
        // backward key, so the careful step went to the X that was left over.
        lMap.AddAction("EasyForward", InputActionType.Button).AddBinding("<Keyboard>/w");
        lMap.AddAction("EasyWalkForward", InputActionType.Button).AddBinding("<Keyboard>/#(x)");
        lMap.AddAction("EasyBack", InputActionType.Button).AddBinding("<Keyboard>/s");
        lMap.AddAction("EasyTurnLeft", InputActionType.Button).AddBinding("<Keyboard>/a");
        lMap.AddAction("EasyTurnRight", InputActionType.Button).AddBinding("<Keyboard>/d");

        // The modern scheme's own keys (see ModernUse). The hash notation binds to the character,
        // as with Strafe above.
        InputAction lModernUse = lMap.AddAction("ModernUse", InputActionType.Button);
        lModernUse.AddBinding("<Keyboard>/#(e)");
        lModernUse.AddBinding("<Gamepad>/buttonSouth");

        InputAction lModernReady = lMap.AddAction("ModernReady", InputActionType.Button);
        lModernReady.AddBinding("<Keyboard>/#(r)");
        lModernReady.AddBinding("<Gamepad>/leftStickPress");

        // The LEFT CTRL sinks since 2026-10-04 (per user; V from 2026-10-03, C before). The
        // original's Ctrl shortcuts take the right Ctrl then in the modern scheme (UWGameUI).
        InputAction lModernHover = lMap.AddAction("ModernHover", InputActionType.Value, expectedControlLayout: "Axis");
        lModernHover.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/leftCtrl")
            .With("Positive", "<Keyboard>/space");
        lModernHover.AddCompositeBinding("1DAxis")
            .With("Negative", "<Gamepad>/dpad/left")
            .With("Positive", "<Gamepad>/dpad/right");

        InputAction lMenu = lMap.AddAction("Menu", InputActionType.Button);
        lMenu.AddBinding("<Keyboard>/escape");
        lMenu.AddBinding("<Gamepad>/start");

        lMap.AddAction("ModernBags", InputActionType.Button).AddBinding("<Keyboard>/#(b)");

        InputAction lModernLook = lMap.AddAction("ModernLook", InputActionType.Button);
        lModernLook.AddBinding("<Keyboard>/#(q)");
        lModernLook.AddBinding("<Gamepad>/buttonWest");

        lMap.AddAction("ModernRunes", InputActionType.Button).AddBinding("<Keyboard>/z");

        // THE GAMEPAD'S OWN (UWGamepad, per user 2026-10-07).
        InputAction lModernAttack = lMap.AddAction("ModernAttack", InputActionType.Button);
        lModernAttack.AddBinding("<Mouse>/leftButton");
        lModernAttack.AddBinding("<Gamepad>/rightTrigger");

        // B only goes back, Y jumps (per user, 2026-10-07; B jumped while nothing was open).
        lMap.AddAction("PadBack", InputActionType.Button).AddBinding("<Gamepad>/buttonEast");
        lMap.AddAction("PadJump", InputActionType.Button).AddBinding("<Gamepad>/buttonNorth");
        lMap.AddAction("PadCast", InputActionType.Button).AddBinding("<Gamepad>/leftTrigger");
        lMap.AddAction("PadSlotPrevious", InputActionType.Button).AddBinding("<Gamepad>/leftShoulder");
        lMap.AddAction("PadSlotNext", InputActionType.Button).AddBinding("<Gamepad>/rightShoulder");
        lMap.AddAction("PadSlotUse", InputActionType.Button).AddBinding("<Gamepad>/dpad/up");

        lMap.AddAction("PadPointer", InputActionType.Button).AddBinding("<Gamepad>/rightStickPress");
        // RT clicks as well: holding A while the same thumb steers the stick does not work for a
        // drag (per user, 2026-10-07) - the index finger holds RT instead, as on a mouse, where it
        // is the left button too (RT's strike then follows the pointer, Interaction). RB clicked
        // for a while the same day and went back to the action bar (per user: six click buttons
        // for two mouse buttons, and the bar was gone with the pointer free).
        InputAction lPadClick = lMap.AddAction("PadClick", InputActionType.Button);
        lPadClick.AddBinding("<Gamepad>/buttonSouth");
        lPadClick.AddBinding("<Gamepad>/rightTrigger");
        // LT is the right button as well, mirroring RT: X with the stick under the same thumb could
        // not drag (per user, 2026-10-07). LB, likewise for a while, is the action bar's again.
        InputAction lPadContext = lMap.AddAction("PadContext", InputActionType.Button);
        lPadContext.AddBinding("<Gamepad>/buttonWest");
        lPadContext.AddBinding("<Gamepad>/leftTrigger");

        for (int liSlot = 1; liSlot <= ModernSlotCount; liSlot++)
            lMap.AddAction("ModernSlot" + liSlot, InputActionType.Button).AddBinding("<Keyboard>/" + (liSlot % 10));

        return lMap;
    }

    private static InputActionMap fBuildNoclipMap()
    {
        InputActionMap lMap = new InputActionMap("Noclip");

        InputAction lMove = lMap.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
        lMove.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        lMap.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2")
            .AddBinding("<Mouse>/delta").WithProcessor(MouseDeltaScale);

        lMap.AddAction("Up", InputActionType.Button).AddBinding("<Keyboard>/space");
        lMap.AddAction("Down", InputActionType.Button).AddBinding("<Keyboard>/leftCtrl");
        lMap.AddAction("Sprint", InputActionType.Button).AddBinding("<Keyboard>/leftShift");

        return lMap;
    }

    private static InputActionMap fBuildDebugMap()
    {
        InputActionMap lMap = new InputActionMap("Debug");

        // Shift+F1 since 2026-09-25: plain F1 opens the help window (per user, 2026-09-24).
        lMap.AddAction("ToggleOverlay", InputActionType.Button).AddCompositeBinding("OneModifier")
            .With("Modifier", "<Keyboard>/shift").With("Binding", "<Keyboard>/f1");
        lMap.AddAction("TeleportDebug", InputActionType.Button).AddBinding("<Keyboard>/p");
        lMap.AddAction("ToggleSpectator", InputActionType.Button).AddBinding("<Keyboard>/n");
        lMap.AddAction("NextLevel", InputActionType.Button).AddBinding("<Keyboard>/pageUp");
        lMap.AddAction("PrevLevel", InputActionType.Button).AddBinding("<Keyboard>/pageDown");
        // F11 since 2026-10-03: B opens the bags in the modern scheme (per user).
        lMap.AddAction("ToggleBrightness", InputActionType.Button).AddBinding("<Keyboard>/f11");

        return lMap;
    }
}
