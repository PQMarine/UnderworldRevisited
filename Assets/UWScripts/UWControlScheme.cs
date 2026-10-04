using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Single source of truth for which control scheme is currently active, and
/// owner of the shared UWControls instance (Player action map) for the player.
///
/// Original: movement via the mouse pointer position in the game window, as in the original from
/// 1992 - the pointer is visible and free, held with the left mouse button. Modern:
/// free mouse look plus WASD/gamepad stick, as usual in today's games - the pointer
/// is invisible and locked to the centre of the screen.
///
/// Other scripts ask exclusively here (Current, Controls), instead of
/// interpreting Cursor.visible or the state of individual movement/look scripts themselves
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

    public void HoldUiModal(string psReason)
    {
        mOUiModalHolds.Add(psReason);
        fApplyCursorState();
    }

    public void ReleaseUiModal(string psReason)
    {
        mOUiModalHolds.Remove(psReason);
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
        mOPointerHolds.Add(psReason);
        fApplyCursorState();
    }

    public void ReleasePointer(string psReason)
    {
        mOPointerHolds.Remove(psReason);
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
    /// Set by UWConversationScreen while a conversation is running.
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

    /// <summary>World input is paused: either a display lies on top, the view
    /// is detached, or a conversation is running. Look, movement and use query this.
    /// </summary>
    public bool IsWorldInputBlocked
    {
        get { return IsUIModalOpen || IsRemoteCameraActive || mbIsConversationOpen || IsInputHeld; }
    }

    /// <summary>Inventory input is paused - like the world, just not during a conversation (see
    /// IsConversationOpen). UWItemDrag queries this.</summary>
    public bool IsInventoryInputBlocked
    {
        get { return IsUIModalOpen || IsRemoteCameraActive || IsInputHeld; }
    }

    private void Awake()
    {
        mControls = new UWControls();
        mControls.Enable();
    }

    private void OnDestroy()
    {
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
        // On the keyboard SHIFT+F2 since 2026-09-26: plain F2 is the original's talk key.
        InputAction lOToggle = mControls.Player.ToggleScheme;

        if (lOToggle.WasPressedThisFrame()
            && (!(lOToggle.activeControl?.device is Keyboard) || UWControls.IsShiftHeld))
        {
            Apply(Current == SchemeEnum.Original ? SchemeEnum.Modern : SchemeEnum.Original);
            UWUserSettings.ControlScheme = (int)Current;
            UWUserSettings.Save();
        }
    }

    public void Apply(SchemeEnum peScheme)
    {
        Current = peScheme;
        fApplyCursorState();
    }

    private void fApplyCursorState()
    {
        if (Current == SchemeEnum.Modern && !IsUIModalOpen && !mbIsConversationOpen && !IsPointerFree)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            // With Original the real system pointer always stays visible and free anyway,
            // as in the original from 1992 - UWGameUI only actively clamps it to the small view
            // window during a held cursor movement. With Modern and an open
            // UI panel (IsUIModalOpen) the same applies temporarily.
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
}
