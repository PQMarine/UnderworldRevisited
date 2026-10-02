using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Free debug camera without collision, toggled via DebugFunctions (key N).
///
/// Replaces the former combination of FlyCam.cs (movement) and MouseLook.cs
/// (looking around) with a single script. MouseLook was removed when the game controls
/// were rebuilt, which left the spectator camera's look control behind as a "Missing Script"
/// - this script closes the gap without reviving the removed legacy
/// code.
///
/// The mouse looks around immediately, without holding a key - unlike the
/// game's original control scheme, where noclip plays no role anyway.
/// </summary>
public class UWNoclipCamera : MonoBehaviour
{
    [Header("Movement (units per second, a tile is 64 wide)")]
    [SerializeField]
    private float mfSpeed = 400f;

    [SerializeField]
    private float mfSprintMultiplier = 3f;

    [Header("Look")]
    [SerializeField]
    private float mfSensitivityX = 5f;

    [SerializeField]
    private float mfSensitivityY = 5f;

    private float mfPitch;
    private UWControls mControls;

    private void Awake()
    {
        mControls = new UWControls();
    }

    private void OnDestroy()
    {
        mControls.Dispose();
    }

    private void OnEnable()
    {
        mControls.Enable();

        fKeepSingleListener();

        mfPitch = transform.localEulerAngles.x;

        // Euler angles are 0..360; for the clamp below this becomes -180..180.
        if (mfPitch > 180f)
            mfPitch -= 360f;
    }

    private void OnDisable()
    {
        mControls.Disable();
    }

    private float mfNextListenerCheck;

    /// <summary>
    /// Exactly ONE active AudioListener while noclip is running.
    ///
    /// Without a listener on this camera everything was silent, and Unity reported "no
    /// audio listeners" every frame - the player and its camera are disabled, and the listener here
    /// is switched off in the scene (per user, 2026-09-13). Simply enabling it, however, gave
    /// "two listeners active" (per user, same day): UWAudioEngine and UWSoundPlayer
    /// create one themselves if they find none, and that one stayed alongside the one from the
    /// scene. Hence: at most one here, and only enabled when no other is active.
    /// It goes off with the object on exit; the player then hears through their own.
    /// </summary>
    private void fKeepSingleListener()
    {
        AudioListener[] lOOwn = GetComponents<AudioListener>();
        AudioListener lOListener = lOOwn.Length > 0 ? lOOwn[0] : gameObject.AddComponent<AudioListener>();

        for (int liAt = 1; liAt < lOOwn.Length; liAt++)
            Destroy(lOOwn[liAt]);

        bool lbOtherActive = false;

        foreach (AudioListener lOOther in FindObjectsByType<AudioListener>())
        {
            if (lOOther.gameObject != gameObject && lOOther.enabled && lOOther.gameObject.activeInHierarchy)
            {
                lbOtherActive = true;
                break;
            }
        }

        lOListener.enabled = !lbOtherActive;
    }

    private void Update()
    {
        // Other scripts may create a listener later - check once per second.
        if (Time.unscaledTime >= mfNextListenerCheck)
        {
            mfNextListenerCheck = Time.unscaledTime + 1f;
            fKeepSingleListener();
        }

        UWControls.NoclipActions lInput = mControls.Noclip;

        Vector2 lOLookDelta = lInput.Look.ReadValue<Vector2>();

        transform.Rotate(0f, lOLookDelta.x * mfSensitivityX, 0f, Space.World);

        mfPitch = Mathf.Clamp(mfPitch - (lOLookDelta.y * mfSensitivityY), -89f, 89f);

        Vector3 lOEuler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(mfPitch, lOEuler.y, 0f);

        Vector2 lOMove = lInput.Move.ReadValue<Vector2>();
        float lfUp = (lInput.Up.IsPressed() ? 1f : 0f) - (lInput.Down.IsPressed() ? 1f : 0f);

        Vector3 lODirection = (transform.forward * lOMove.y) + (transform.right * lOMove.x) + (Vector3.up * lfUp);

        if (lODirection.sqrMagnitude > 1f)
            lODirection.Normalize();

        float lfSpeed = mfSpeed * (lInput.Sprint.IsPressed() ? mfSprintMultiplier : 1f);

        transform.position += lODirection * lfSpeed * Time.deltaTime;
    }
}
