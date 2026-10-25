using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UWDataImport.UWData;

/// <summary>
/// Single source of truth for which control scheme is currently active, and
/// owner of the shared UWControls instance (Player action map) for the player.
///
/// Original: movement via the mouse pointer position in the game window, as in the original from
/// 1992 - the pointer is visible and free, held with the left mouse button. Modern:
/// free mouse look plus WASD/gamepad stick, as usual in today's games - the pointer
/// is invisible and locked to the centre of the screen (UWPointer.IsLookLocked).
///
/// Other scripts ask exclusively here (Current, Controls), instead of
/// interpreting the pointer's visibility or the state of individual movement/look scripts themselves
/// or creating their own UWControls instance for the same player.
/// </summary>
public class UWControlScheme : MonoBehaviour
{
    public enum SchemeEnum
    {
        Original = 0,
        Modern = 1
    }

    [SerializeField]
    private SchemeEnum meDefaultScheme = SchemeEnum.Modern;

    private UWControls mControls;

    public UWControls Controls
    {
        get { return mControls; }
    }

    public SchemeEnum Current { get; private set; }

    /// <summary>The one component of the scene (First Person Controller), from Awake to
    /// OnDestroy - it stays known while the player object is switched off (noclip), which
    /// UWScene.ControlScheme would not find then.</summary>
    private static UWControlScheme msInstance;

    /// <summary>
    /// THE ONE SCHEME TEST (2026-10-11). Until then fourteen classes asked their own cached
    /// component or UWScene.ControlScheme, each with its own null rule; every one of them
    /// read the same Current. Null-safe: without a scheme component - the main menu, a scene
    /// before the player exists - it is the Original scheme, as every caller assumed.
    ///
    /// The last 24 tests of the form "scheme.Current == SchemeEnum.X" came here on 2026-10-19
    /// (cleanup 5.1). Where a caller held its own reference it keeps that reference's null
    /// check: UWScene.ControlScheme is null while the player object is off (noclip), and the
    /// caller's answer for that case stays as it was. Not a scheme test and left alone: the
    /// change detection of UWPlayerLook and the scheme selector of UWSetupMenu.
    /// </summary>
    public static bool IsModern => msInstance != null && msInstance.Current == SchemeEnum.Modern;

    /// <summary>The player's controls (the one component's, from Awake to OnDestroy - known while the
    /// player object is off, as msInstance is); null without a scheme component. What UWInput reads.</summary>
    public static UWControls SharedControls => msInstance != null ? msInstance.mControls : null;

    /// <summary>The one component, null without one - known while the player object is off, unlike
    /// UWScene.ControlScheme.</summary>
    public static UWControlScheme Shared => msInstance;

    /// <summary>
    /// THE INTERFACE THE GAME LOGIC TALKS TO (cleanup 5.19, 2026-10-23): UWClassicUiScheme or
    /// UWModernUiScheme, swapped in Apply with the scheme - the game logic asks it what the
    /// interface in force does instead of testing IsModern and branching (IUWUiScheme). The same
    /// null rule as IsModern: without a scheme component it is the classic one.
    /// </summary>
    public static IUWUiScheme Ui => msInstance != null ? msInstance.mOUi : UWClassicUiScheme.Instance;

    private IUWUiScheme mOUi = UWClassicUiScheme.Instance;

    private readonly HashSet<string> mOUiModalHolds = new HashSet<string>();

    /// <summary>While a modal UI panel is open (e.g. the Modern inventory - with
    /// Original the system pointer is always free anyway), the pointer must be visible/unlocked
    /// regardless of the control scheme, and look/interaction scripts should
    /// pause. Single source of truth for this, just as for the scheme itself.
    ///
    /// Since 2026-09-18 a set of named holds instead of one boolean: the backpack closing no
    /// longer frees the controls that the options panel or a cutscene still hold.</summary>
    public bool IsUIModalOpen
    {
        get { return mOUiModalHolds.Count > 0; }
    }

    /// <summary>
    /// A HOLD THAT DOES NOT CHANGE LEAVES THE POINTER ALONE (2026-10-22): the cursor state is
    /// applied only when a hold is really added or taken away. Until then every call applied it,
    /// and UWModernPointer.Tick releases its pointer hold in EVERY frame of the classic scheme -
    /// so the classic scheme showed the system pointer again each frame, over the yes/no question
    /// (per user: it came back on Y), the count prompt and the map note (the morning's fix in
    /// UWGameUI.LateUpdate, gone again), whatever had hidden it.
    /// </summary>
    public void HoldUiModal(string psReason)
    {
        if (mOUiModalHolds.Add(psReason))
            fApplyCursorState();
    }

    public void ReleaseUiModal(string psReason)
    {
        if (mOUiModalHolds.Remove(psReason))
            fApplyCursorState();
    }

    public void SetUiModal(string psReason, bool pbOpen)
    {
        if (pbOpen)
            HoldUiModal(psReason);
        else
            ReleaseUiModal(psReason);
    }

    private readonly HashSet<string> mOPointerHolds = new HashSet<string>();

    /// <summary>
    /// INPUT HELD WITHOUT TOUCHING THE POINTER (per user, 2026-10-04: asleep, the modern scheme's
    /// pointer came free): the world's input stops as under a modal hold, but the pointer stays
    /// as it is - a modal hold shows it. Named holds as with HoldUiModal.
    /// </summary>
    private readonly HashSet<string> mOInputHolds = new HashSet<string>();

    public bool IsInputHeld => mOInputHolds.Count > 0;

    public void HoldInput(string psReason)
    {
        mOInputHolds.Add(psReason);
    }

    public void ReleaseInput(string psReason)
    {
        mOInputHolds.Remove(psReason);
    }

    /// <summary>
    /// THE FREE POINTER of the modern scheme's bags (UWModernBags, per user 2026-10-03: I or B
    /// frees the pointer, walking goes on). Unlike a modal hold it stops only what the mouse
    /// does in the world - the mouse look and the buttons' attack and look -, not movement and
    /// not the keys. Named holds as with HoldUiModal.
    /// </summary>
    public bool IsPointerFree
    {
        get { return mOPointerHolds.Count > 0; }
    }

    public void HoldPointer(string psReason)
    {
        if (mOPointerHolds.Add(psReason))
            fApplyCursorState();
    }

    public void ReleasePointer(string psReason)
    {
        if (mOPointerHolds.Remove(psReason))
            fApplyCursorState();
    }

    /// <summary>
    /// Set by UWRemoteCamera while the view is detached.
    ///
    /// A SEPARATE FIELD, NOT IsUIModalOpen. Two reasons: its setter releases the
    /// system pointer, and while the camera flies it should stay locked. (Until 2026-09-18 the
    /// second reason was that it was a plain boolean five classes set - now it is a set of
    /// named holds, see HoldUiModal.)
    /// </summary>
    public bool IsRemoteCameraActive { get; set; }

    /// <summary>
    /// Set by UWConversationController while a conversation is running.
    ///
    /// A SEPARATE FIELD, NOT IsUIModalOpen: the conversation halts look, movement and the
    /// world, but NOT the inventory - in the original the paperdoll stays usable during the
    /// conversation, it is needed for trading (per user, 2026-09-11).
    /// </summary>
    public bool IsConversationOpen
    {
        get { return mbIsConversationOpen; }
        set
        {
            mbIsConversationOpen = value;
            fApplyCursorState();
        }
    }

    private bool mbIsConversationOpen;

    /// <summary>The input contexts this component's holds put on the stack (cleanup step 7.4,
    /// 2026-10-25): UiModal for a modal hold, the detached view or an input hold, Conversation while
    /// one runs - over the World.</summary>
    public UWInputContext HeldContexts
    {
        get
        {
            UWInputContext leSet = UWInputContext.World;

            if (IsUIModalOpen || IsRemoteCameraActive || IsInputHeld)
                leSet |= UWInputContext.UiModal;

            if (mbIsConversationOpen)
                leSet |= UWInputContext.Conversation;

            return leSet;
        }
    }

    /// <summary>World input is paused: either a display lies on top, the view
    /// is detached, or a conversation is running. Look, movement and use query this
    /// (UWInputContexts.BlocksWorld).
    /// </summary>
    public bool IsWorldInputBlocked
    {
        get { return UWInputContexts.BlocksWorld(HeldContexts); }
    }

    /// <summary>Inventory input is paused - like the world, just not during a conversation (see
    /// IsConversationOpen). UWItemDrag queries this (UWInputContexts.BlocksInventory).</summary>
    public bool IsInventoryInputBlocked
    {
        get { return UWInputContexts.BlocksInventory(HeldContexts); }
    }

    private void Awake()
    {
        msInstance = this;
        mControls = new UWControls();
        mControls.Enable();
    }

    private void OnDestroy()
    {
        if (msInstance == this)
            msInstance = null;

        mControls.Dispose();
    }

    /// <summary>
    /// THE LAST CHOSEN SCHEME (per user, 2026-10-04): Shift+F2 stores it in the user settings, and
    /// it is chosen again where the scene starts - a new game, a loaded one (the scene is rebuilt)
    /// - and when the player comes back from noclip (the player object was switched off; its
    /// pointer state is applied anew). Not chosen yet: the scene's default.
    /// </summary>
    private void Start()
    {
        mbStarted = true;
        Apply(fRemembered());
    }

    private bool mbStarted;

    private void OnEnable()
    {
        if (mbStarted)
            Apply(fRemembered());
    }

    private SchemeEnum fRemembered()
    {
        int liScheme = UWUserSettings.ControlScheme;

        return liScheme == (int)SchemeEnum.Original || liScheme == (int)SchemeEnum.Modern ? (SchemeEnum)liScheme : meDefaultScheme;
    }

    private void Update()
    {
        // On the keyboard SHIFT+F2 since 2026-09-26: plain F2 is the original's talk key (UWHotkeys,
        // ShiftOnKeyboard; cleanup step 7.5).
        if (mOKeys == null)
            mOKeys = new SchemeKeys(this);

        UWHotkeys.Route(UWHotkeySite.Scheme, mOKeys);
    }

    private SchemeKeys mOKeys;

    /// <summary>The scheme's hotkey (UWHotkeys): Shift+F2 switches it.</summary>
    private sealed class SchemeKeys : UWHotkeyHost
    {
        private readonly UWControlScheme mOScheme;

        public SchemeKeys(UWControlScheme pOScheme)
        {
            mOScheme = pOScheme;
        }

        protected override UWControls Controls => mOScheme.mControls;

        public override bool Gate(UWHotkeyGate peGate, UWHotkeyRow pORow)
        {
            return false;
        }

        public override bool Run(UWHotkeyHandler peHandler)
        {
            if (peHandler != UWHotkeyHandler.SwitchScheme)
                return false;

            mOScheme.Choose(mOScheme.Current == SchemeEnum.Original ? SchemeEnum.Modern : SchemeEnum.Original);
            return true;
        }
    }

    /// <summary>The player's choice of a scheme - Shift+F2 or the Controls dialog of the menu bar
    /// (UWSetupMenu) - applied and kept for the next start.</summary>
    public void Choose(SchemeEnum peScheme)
    {
        Apply(peScheme);
        UWUserSettings.ControlScheme = (int)Current;
        UWUserSettings.Save();
    }

    public void Apply(SchemeEnum peScheme)
    {
        SchemeEnum leBefore = Current;

        Current = peScheme;

        // The interface the game logic talks to follows the scheme (Ui) - before the menus are
        // handed over, which the new scheme's interface sees as before.
        mOUi = peScheme == SchemeEnum.Modern ? (IUWUiScheme)UWModernUiScheme.Instance : UWClassicUiScheme.Instance;

        if (leBefore != peScheme && mbStarted)
            fHandOverMenus(leBefore);

        // The easy steps lie on the turn keys in the classic scheme and on the keys beside the walk
        // in the modern one (decision 18): put anew - an unchanged step is left alone.
        UWControls.ApplyEasySteps();

        fApplyCursorState();
    }

    /// <summary>
    /// A MENU SURVIVES THE SCHEME CHANGE (per user, 2026-10-12: with the classic options up -
    /// the way to the menu bar - the Controls dialog switched to Modern; the panel vanished,
    /// its holds on time and input stayed, and nothing could be done). The old scheme's menu
    /// closes and the new scheme's opens in its place, so the player still stands in a menu
    /// and can close it.
    /// </summary>
    private static void fHandOverMenus(SchemeEnum peFrom)
    {
        UWGameUI lOUi = UWScene.GameUi;
        UWHudOptions lOOptions = lOUi != null ? lOUi.Options : null;
        UWModernHud lOModern = UWModernHud.Instance;

        if (peFrom == SchemeEnum.Original)
        {
            if (lOOptions == null || !lOOptions.IsOpen)
                return;

            lOOptions.CloseForSchemeChange();

            if (lOModern != null)
                lOModern.Menu.OpenMenu(UWModernGameMenu.PageEnum.Main);
        }
        else
        {
            if (lOModern == null || !lOModern.IsOpen)
                return;

            lOModern.Menu.CloseMenu();

            if (lOOptions != null)
                lOOptions.OpenForSchemeChange();
        }
    }

    /// <summary>
    /// THE LOOK MODE AND THE POINTER'S VISIBILITY, through UWPointer since cleanup step 7.7B
    /// (2026-10-25; Cursor.visible and Cursor.lockState here until then): the modern scheme without a
    /// modal hold, a conversation or its free pointer turns the view with the mouse and hides the
    /// pointer; otherwise the pointer shows - in the game it is our own, captured (UWPointer), in the
    /// menus the system's.
    /// </summary>
    private void fApplyCursorState()
    {
        bool lbLook = Current == SchemeEnum.Modern && !IsUIModalOpen && !mbIsConversationOpen && !IsPointerFree;

        // With Original the pointer always stays visible and free anyway, as in the original
        // from 1992 - UWPointerZones only clamps it to the small view window during a held cursor
        // movement. With Modern and an open UI panel (IsUIModalOpen) the same applies temporarily.
        UWPointer.IsLookLocked = lbLook;
        UWPointer.IsShown = !lbLook;
    }
}
