using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The remote camera: a view that is not attached to the character's head.
///
/// Two things need it - a_do trap 2 (the camera jumps to the trap while the
/// player holds the look, see fUpdateRelease) and the spell Roaming Sight (the view flies across the
/// level while the character stands still).
///
/// A SECOND CAMERA, NOT THE MOVED PLAYER CAMERA. UWCharacter sits on the player camera
/// (see UWLevelLoader.fInitialiseUi), and a good dozen places measure
/// against Camera.main when they mean "where is the character": the creatures use it to find their
/// target (UWCritter.fEnsurePlayer), the trespass trap its radius, the
/// trap projectile launch its direction, the area spell its view axis. If the
/// player camera flew away, the creatures would chase the camera for minutes during
/// Roaming Sight. This way Camera.main stays with the body, and only the IMAGE moves.
///
/// CLAIM STACK instead of a single state: the camera trap can be triggered during Roaming
/// Sight. The trap's claim has the higher number, lays itself on top,
/// and after the release the view falls back to the remembered flight pose - not to the
/// player. The original cannot do this: there both share the same six global
/// values, and at the end the trap unconditionally reattaches to the character.
/// </summary>
public sealed class UWRemoteCamera : MonoBehaviour
{
    /// <summary>The trap's claim lies above that of the spell.</summary>
    public const int TrapPriority = 20;

    public const int SpellPriority = 10;

    public static UWRemoteCamera Current { get; private set; }

    /// <summary>The camera itself. It deliberately carries NO MainCamera tag and no
    /// audio listener - both stay with the character.</summary>
    public Camera ViewCamera { get; private set; }

    /// <summary>Is there any claim at all?</summary>
    public bool IsActive
    {
        get { return mOClaims.Count > 0; }
    }

    /// <summary>Finds the camera or creates it. Called from UWLevelLoader once the
    /// UI is up - the same pattern as the other self-created components.</summary>
    public static UWRemoteCamera Ensure()
    {
        if (Current != null)
            return Current;

        UWRemoteCamera lOFound = FindAnyObjectByType<UWRemoteCamera>();

        if (lOFound != null)
        {
            Current = lOFound;

            return lOFound;
        }

        GameObject lOHost = new GameObject("UWRemoteCamera");

        return lOHost.AddComponent<UWRemoteCamera>();
    }

    private struct Claim
    {
        public int Id;

        public int Priority;

        public Vector3 Position;

        public float Yaw;

        public float Pitch;

        /// <summary>Roll around the view axis, in degrees - so far only for the pull into the moongate.</summary>
        public float Roll;

        /// <summary>The trap view: it lasts only while the player holds the button or key
        /// that looked at the trigger (see fUpdateRelease).</summary>
        public bool EndOnRelease;

        public System.Action OnEnd;
    }

    /// <summary>Ordered by priority from bottom to top; the last entry renders.
    /// </summary>
    private readonly List<Claim> mOClaims = new List<Claim>();

    private int miNextClaimId = 1;

    private UWControlScheme mOScheme;

    private UWLevelLoader mOLevelLoader;

    private int miLevelAtStart = -1;

    // The six values of the topmost claim in ORIGINAL UNITS. Only for reading along in the
    // Inspector; the world pose is what is tracked. They are here because this way the camera trap
    // can be checked directly against the reference (there DoCameraX/Y/Z/H/Pitch/Roll).
    [Header("Read-only: the topmost claim in original units")]
    [SerializeField]
    private short miDoCameraX;

    [SerializeField]
    private short miDoCameraY;

    [SerializeField]
    private short miDoCameraZ;

    [SerializeField]
    private short miDoCameraH;

    [SerializeField]
    private short miDoCameraPitch;

    [SerializeField]
    private short miDoCameraRoll;

    private void Awake()
    {
        Current = this;

        ViewCamera = gameObject.GetComponent<Camera>();

        if (ViewCamera == null)
            ViewCamera = gameObject.AddComponent<Camera>();

        Camera lOMain = Camera.main;

        if (lOMain != null)
        {
            ViewCamera.CopyFrom(lOMain);
            ViewCamera.depth = lOMain.depth + 1;
        }

        // CopyFrom does NOT copy the URP additional data. Without these lines URP creates it at
        // runtime with default values - it works, but the intent would be stated nowhere.
        UniversalAdditionalCameraData lOData = gameObject.GetComponent<UniversalAdditionalCameraData>();

        if (lOData == null)
            lOData = gameObject.AddComponent<UniversalAdditionalCameraData>();

        lOData.renderType = CameraRenderType.Base;

        // The tag belongs to the player camera. Camera.main must keep finding that one.
        gameObject.tag = "Untagged";

        ViewCamera.enabled = false;
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    /// <summary>Releases the lock when the scene is torn down - otherwise the
    /// controls would stay locked.</summary>
    private void OnDisable()
    {
        EndAll();
    }

    /// <summary>
    /// Registers a claim on the view and returns its number. Numbers start at one, so
    /// zero can stand for "no claim"; a claim is never rejected. The number is never
    /// reused, so an expired claim cannot
    /// accidentally operate someone else's.
    /// </summary>
    public int Begin(int piPriority, Vector3 pOPosition, float pfYaw, float pfPitch,
        bool pbEndOnRelease, System.Action pOOnEnd)
    {
        Claim lOClaim = new Claim
        {
            Id = miNextClaimId++,
            Priority = piPriority,
            Position = pOPosition,
            Yaw = pfYaw,
            Pitch = pfPitch,
            EndOnRelease = pbEndOnRelease,
            OnEnd = pOOnEnd
        };

        int liAt = mOClaims.Count;

        while (liAt > 0 && mOClaims[liAt - 1].Priority > piPriority)
            liAt--;

        mOClaims.Insert(liAt, lOClaim);

        fEnsureRefs();

        miLevelAtStart = mOLevelLoader != null ? mOLevelLoader.CurrentLevelIndex : -1;

        return lOClaim.Id;
    }

    /// <summary>Is this claim on top, i.e. currently rendering?</summary>
    public bool IsClaimActive(int piClaim)
    {
        return mOClaims.Count > 0 && mOClaims[mOClaims.Count - 1].Id == piClaim;
    }

    /// <summary>Is this claim still on the stack - possibly displaced?</summary>
    public bool HasClaim(int piClaim)
    {
        return fFind(piClaim) >= 0;
    }

    public bool SetPose(int piClaim, Vector3 pOPosition, float pfYaw, float pfPitch, float pfRoll = 0f)
    {
        int liAt = fFind(piClaim);

        if (liAt < 0)
            return false;

        Claim lOClaim = mOClaims[liAt];

        lOClaim.Position = pOPosition;
        lOClaim.Yaw = pfYaw;
        lOClaim.Pitch = pfPitch;
        lOClaim.Roll = pfRoll;

        mOClaims[liAt] = lOClaim;

        return true;
    }

    public bool GetPose(int piClaim, out Vector3 pOPosition, out float pfYaw, out float pfPitch)
    {
        int liAt = fFind(piClaim);

        if (liAt < 0)
        {
            pOPosition = Vector3.zero;
            pfYaw = 0f;
            pfPitch = 0f;

            return false;
        }

        pOPosition = mOClaims[liAt].Position;
        pfYaw = mOClaims[liAt].Yaw;
        pfPitch = mOClaims[liAt].Pitch;

        return true;
    }

    public void End(int piClaim)
    {
        int liAt = fFind(piClaim);

        if (liAt < 0)
            return;

        System.Action lOOnEnd = mOClaims[liAt].OnEnd;

        mOClaims.RemoveAt(liAt);

        // After removal another claim is on top again: it does not need to re-arm,
        // since it did not trigger anything.
        if (lOOnEnd != null)
            lOOnEnd();
    }


    public void EndAll()
    {
        while (mOClaims.Count > 0)
            End(mOClaims[mOClaims.Count - 1].Id);
    }

    private int fFind(int piClaim)
    {
        for (int liAt = 0; liAt < mOClaims.Count; liAt++)
        {
            if (mOClaims[liAt].Id == piClaim)
                return liAt;
        }

        return -1;
    }

    private void fEnsureRefs()
    {
        if (mOScheme == null)
            mOScheme = UWScene.ControlScheme;

        if (mOLevelLoader == null)
            mOLevelLoader = UWScene.LevelLoader;
    }

    /// <summary>
    /// LATE, not in Update: UWGameUI.fApplyGameCamera sets the viewport rect and field of view
    /// of the player camera in Update, and there is no fixed script execution order in the
    /// project. The only reliable thing is that all Updates run before all LateUpdates.
    /// </summary>
    private void LateUpdate()
    {
        fEnsureRefs();

        if (mOClaims.Count == 0)
        {
            if (ViewCamera != null)
                ViewCamera.enabled = false;

            if (mOScheme != null)
                mOScheme.IsRemoteCameraActive = false;

            return;
        }

        // A level change ends every view - the tile below then belongs to
        // a different level. The reference cleans up at the same point.
        if (mOLevelLoader != null && miLevelAtStart >= 0
            && mOLevelLoader.CurrentLevelIndex != miLevelAtStart)
        {
            EndAll();

            return;
        }

        if (mOScheme != null)
            mOScheme.IsRemoteCameraActive = true;

        fUpdateRelease();

        if (mOClaims.Count == 0)
            return;

        Claim lOTop = mOClaims[mOClaims.Count - 1];

        transform.SetPositionAndRotation(lOTop.Position,
            Quaternion.Euler(lOTop.Pitch, lOTop.Yaw, lOTop.Roll));

        fMirrorOriginalValues(lOTop);

        if (ViewCamera == null)
            return;

        // The viewport comes ready-made from the player camera: it already holds the hole
        // in the original scheme, the stretched aspect ratio of the VGA display and the
        // field of view derived back from it. So the remote image sits correctly by itself
        // and follows every scheme change and every window size.
        Camera lOMain = Camera.main;

        if (lOMain != null)
        {
            ViewCamera.rect = lOMain.rect;
            ViewCamera.aspect = lOMain.aspect;
            ViewCamera.fieldOfView = lOMain.fieldOfView;
        }

        ViewCamera.enabled = true;
    }

    /// <summary>
    /// THE TRAP VIEW LASTS WHILE THE LOOK IS HELD (per user on the original, 2026-09-24): the
    /// camera picture shows only as long as the right mouse button that looked at the orb is
    /// held, and it ends on the release. Meanwhile the WORLD RUNS ON, around the player as at
    /// the camera's spot - the slugs in front of the moonstone move. DoTrapCamera_ovr114_4FE
    /// waits for that release (ClickEventRelated_seg013_1CC9_367 with 1) and does not halt the
    /// game loop; a first reading here took it for a modal wait and froze the world
    /// (2026-09-24, reverted the same day). Until then ours showed the view until the NEXT
    /// press of any key. Any held key or button keeps it, so it works in every scheme.
    /// </summary>
    private void fUpdateRelease()
    {
        Claim lOTop = mOClaims[mOClaims.Count - 1];

        if (!lOTop.EndOnRelease)
            return;

        bool lbAnyHeld = (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
            || (Mouse.current != null
                && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed
                    || Mouse.current.middleButton.isPressed));

        if (!lbAnyHeld)
            End(lOTop.Id);
    }

    private void fMirrorOriginalValues(Claim pOClaim)
    {
        miDoCameraX = (short)UWViewpoint.WorldToOriginalX(pOClaim.Position.x);
        miDoCameraY = (short)UWViewpoint.WorldToOriginalY(pOClaim.Position.z);
        miDoCameraZ = (short)UWViewpoint.WorldToOriginalZ(pOClaim.Position.y);
        miDoCameraH = UWViewpoint.DegreesToAngle(pOClaim.Yaw);
        miDoCameraPitch = (short)-UWViewpoint.DegreesToAngle(pOClaim.Pitch);
        miDoCameraRoll = UWViewpoint.DegreesToAngle(pOClaim.Roll);
    }
}
