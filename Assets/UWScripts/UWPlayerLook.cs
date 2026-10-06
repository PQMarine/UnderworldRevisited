using UnityEngine;

/// <summary>
/// Free mouse look for the modern control scheme.
///
/// Replaces two separate MouseLook instances (one for yaw on the player object,
/// one for pitch on the camera) whose interplay only worked by following a doc-comment
/// instruction ("Set the mouse look to use LookX... LookY..."). Misconfigured,
/// either the body rotates along or the camera does not rotate at all - here there
/// is only one correct wiring: yaw on the own transform, pitch on the
/// assigned camera.
///
/// In the original scheme the cursor movement itself turns (see UWPlayerMovement); tilting
/// happens there only in fixed steps via the keys 1 to 3 (fUpdateClassicPitch).
/// </summary>
public class UWPlayerLook : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Camera that is pitched. Leave empty to look up Camera.main on start.")]
    private Transform mCameraTransform;

    [SerializeField]
    private float mfSensitivityX = 5f;

    [SerializeField]
    private float mfSensitivityY = 5f;

    [Header("Gamepad stick (degrees per second at full deflection)")]
    [SerializeField]
    private float mfStickSensitivityX = 180f;

    [SerializeField]
    private float mfStickSensitivityY = 180f;

    [SerializeField]
    private float mfMinPitch = -60f;

    [SerializeField]
    private float mfMaxPitch = 60f;

    private UWControlScheme mScheme;
    private UWGameUI mOGameUi;
    private UWControls.PlayerActions mInput;

    /// <summary>Classic scheme: the current step, negative is up, positive is
    /// down.</summary>
    private int miClassicSteps;
    private float mfPitch;
    private UWControlScheme.SchemeEnum mePreviousScheme = UWControlScheme.SchemeEnum.Modern;

    public Transform CameraTransform
    {
        get { return mCameraTransform; }
        set { mCameraTransform = value; }
    }

    private void Awake()
    {
        mScheme = GetComponent<UWControlScheme>();
    }

    private void Start()
    {
        if (mCameraTransform == null && Camera.main != null)
            mCameraTransform = Camera.main.transform;

        mInput = mScheme.Controls.Player;

        // Straight ahead after a start or a load (per user, 2026-10-04: the view always pointed up)
        // - the original keeps the pitch in its data segment, not in PLAYER.DAT.
        fResetPitch();
        miQuietFrames = QuietFrames;
    }

    /// <summary>Frames in which the mouse does not turn the view: after the scene starts and after
    /// the pointer locks, the first deltas carry the jump of the pointer being caught.</summary>
    private const int QuietFrames = 3;

    private int miQuietFrames;

    private bool mbWasFree;

    private int miLoadCount = -1;

    private void Update()
    {
        UWControlScheme.SchemeEnum leCurrent = mScheme.Current;

        // When the scheme changes, the view points straight ahead again - the free pitch of the
        // mouse matches none of the fixed steps, and vice versa.
        if (mePreviousScheme != leCurrent)
            fResetPitch();

        mePreviousScheme = leCurrent;

        // A game loaded (or begun): straight ahead, whether or not the scene was rebuilt.
        if (miLoadCount != UWHelpNotes.LoadCount)
        {
            miLoadCount = UWHelpNotes.LoadCount;
            fResetPitch();
            miQuietFrames = QuietFrames;
        }

        // Under a menu or a screen the pointer is out too - the first deltas after it are quiet.
        if (mScheme.IsWorldInputBlocked)
        {
            mbWasFree = true;
            return;
        }

        if (leCurrent != UWControlScheme.SchemeEnum.Modern)
        {
            fUpdateClassicPitch();
            return;
        }

        // The pointer is out for the bags (UWModernBags): the mouse moves it, not the view.
        if (mScheme.IsPointerFree)
        {
            mbWasFree = true;
            return;
        }

        if (mbWasFree)
        {
            mbWasFree = false;
            miQuietFrames = Mathf.Max(miQuietFrames, QuietFrames);
        }

        if (miQuietFrames > 0)
        {
            miQuietFrames--;
            mInput.Look.ReadValue<Vector2>();
            return;
        }

        // The mouse delivers an already elapsed pixel delta (scaled down in the action to the
        // magnitude of the old Input Manager), whereas the stick delivers
        // a held deflection - that is why the latter additionally needs Time.deltaTime.
        Vector2 lOMouseDelta = mInput.Look.ReadValue<Vector2>();
        Vector2 lOStickDelta = mInput.LookStick.ReadValue<Vector2>();

        // The player's own factor on the mouse (UWUserSettings.MouseLookSpeed, the Controls
        // dialog and the modern game menu); the stick keeps its degrees per second.
        float lfSpeed = UWUserSettings.MouseLookSpeed;

        float lfYaw = (lOMouseDelta.x * mfSensitivityX * lfSpeed) + (lOStickDelta.x * mfStickSensitivityX * Time.deltaTime);
        float lfPitchDelta = (lOMouseDelta.y * mfSensitivityY * lfSpeed) + (lOStickDelta.y * mfStickSensitivityY * Time.deltaTime);

        transform.Rotate(0f, lfYaw, 0f);

        mfPitch = Mathf.Clamp(mfPitch - lfPitchDelta, mfMinPitch, mfMaxPitch);

        if (mCameraTransform != null)
            mCameraTransform.localEulerAngles = new Vector3(mfPitch, 0f, 0f);
    }

    /// <summary>
    /// Classic scheme: tilt the view in fixed steps, as in the original - 1 one step
    /// down, 3 one step up, 2 straight again (per user, 2026-09-13: "3 fixed
    /// steps. For now 15 degrees per step", "Only for the classic scheme"). Free
    /// mouse look is reserved for the modern scheme.
    ///
    /// Not while typing - there the digits belong to the text entry, e.g. at "Move
    /// how many?".
    /// </summary>
    private void fUpdateClassicPitch()
    {
        if (mOGameUi == null)
            mOGameUi = GetComponentInChildren<UWGameUI>();

        if (mOGameUi != null && mOGameUi.IsTextEntryActive)
            return;

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;
        float lfStep = lOSettings != null ? lOSettings.ClassicLookStepDegrees : 15f;
        int liMaxSteps = lOSettings != null ? lOSettings.ClassicLookSteps : 3;

        int liSteps = miClassicSteps;

        if (mInput.LookDown.WasPressedThisFrame())
            liSteps++;

        if (mInput.LookUp.WasPressedThisFrame())
            liSteps--;

        if (mInput.LookReset.WasPressedThisFrame())
            liSteps = 0;

        liSteps = Mathf.Clamp(liSteps, -liMaxSteps, liMaxSteps);

        if (liSteps == miClassicSteps && Mathf.Approximately(mfPitch, liSteps * lfStep))
            return;

        miClassicSteps = liSteps;
        mfPitch = liSteps * lfStep;

        fApplyPitchKeepingRoll();
    }

    /// <summary>Sets only the pitch - the roll while swimming (UWPlayerTerrain) lives
    /// on the same camera and is left untouched.</summary>
    private void fApplyPitchKeepingRoll()
    {
        if (mCameraTransform == null)
            return;

        Vector3 lOAngles = mCameraTransform.localEulerAngles;

        mCameraTransform.localEulerAngles = new Vector3(mfPitch, 0f, lOAngles.z);
    }

    private void fResetPitch()
    {
        mfPitch = 0f;
        miClassicSteps = 0;

        fApplyPitchKeepingRoll();
    }
}
