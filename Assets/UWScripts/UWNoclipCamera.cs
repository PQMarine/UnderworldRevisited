using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UWDataImport.UWData;

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

        if (mOLevelCanvas != null)
            mOLevelCanvas.enabled = false;
    }

    // ------------------------------------------------- The level line (per user, 2026-11-12)

    /// <summary>Block 1's first world name in the second game's strings (by the project's numbering, original 75 + 1).</summary>
    private const int FirstWorldNameString = 76;

    /// <summary>The worlds with a name there: Britannia to the Ethereal Void.</summary>
    private const int NamedWorlds = 9;

    private const int LevelsPerWorld = 8;

    /// <summary>"The Ice Caverns, level 1 (25)"; a world without a name "World 9, level 1 (73)".</summary>
    private static string fSecondGameLevel(UWLevelLoader pOLoader, int piLevel)
    {
        int liWorld = (piLevel - 1) / LevelsPerWorld;
        int liInWorld = ((piLevel - 1) % LevelsPerWorld) + 1;
        string lsWorld = liWorld < NamedWorlds ? UWText.Block(pOLoader.UWDataImporter, 1, FirstWorldNameString + liWorld) : null;

        if (string.IsNullOrEmpty(lsWorld))
            lsWorld = "World " + liWorld;

        return UWText.Capitalised(lsWorld) + ", level " + liInWorld + " (" + piLevel + ")";
    }

    /// <summary>The line's size before the window's scale, and its distance from the top.</summary>
    private const int LevelTextSize = 14;

    private const float LevelTextTop = 44f;

    private static readonly Color LevelTextColour = new Color(1f, 0.85f, 0.3f);

    private Canvas mOLevelCanvas;

    private Text mOLevelText;

    /// <summary>
    /// WHICH LEVEL THE CAMERA IS ON (per user, 2026-11-12: "eine kleine Einblendung zur NoClip Camera, auf welcher Ebene
    /// ich bin"): a gold line at the top centre while noclip runs, "Level 3" in the first game. In the second the world by
    /// its name and the level within it, then the level's number - "The Ice Caverns, level 1 (25)" (per user the same day:
    /// past Britannia the bare numbers say nothing); the names are the game's own, block 1 of its strings from 76
    /// (Britannia, The Prison Tower, Killorn Keep, the Ice Caverns, ... the Ethereal Void), eight levels a world. On the
    /// kit, as the debug labels (UWDebugLabels), the rest English like the debug panel.
    /// </summary>
    private void fShowLevel()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader == null)
            return;

        if (mOLevelCanvas == null)
        {
            mOLevelCanvas = UWUiKit.CreateOverlayCanvas(transform, "Noclip Level Canvas", UWUiSorting.DebugLabels);
            mOLevelText = UWUiKit.CreateText(mOLevelCanvas.transform, "Level", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), TextAnchor.UpperCenter,
                LevelTextColour, null, new UWUiKit.TextStyle { Font = UWUiKit.TextFontEnum.Interface, Effect = UWUiKit.TextEffectEnum.Shadow, KeepsItsFont = true });
            mOLevelText.supportRichText = false;
        }

        if (!mOLevelCanvas.enabled)
            mOLevelCanvas.enabled = true;

        int liLevel = lOLoader.CurrentLevelIndex + 1;
        bool lbSecondGame = UnderworldRevisited.UWSettings.Game != null && UnderworldRevisited.UWSettings.Game.Game == UWGame.Uw2;
        string lsText = lbSecondGame ? fSecondGameLevel(lOLoader, liLevel) : "Level " + liLevel;
        float lfScale = Mathf.Max(1f, UWGuiTheme.Scale);
        int liSize = Mathf.RoundToInt(LevelTextSize * lfScale);

        if (mOLevelText.text != lsText)
            mOLevelText.text = lsText;

        if (mOLevelText.fontSize != liSize)
            mOLevelText.fontSize = liSize;

        Vector2 lOAt = new Vector2(0f, -LevelTextTop * lfScale);
        Vector2 lOSize = new Vector2(320f, 28f) * lfScale;

        if (mOLevelText.rectTransform.anchoredPosition != lOAt)
            mOLevelText.rectTransform.anchoredPosition = lOAt;

        if (mOLevelText.rectTransform.sizeDelta != lOSize)
            mOLevelText.rectTransform.sizeDelta = lOSize;
    }

    private float mfNextListenerCheck;

    /// <summary>
    /// Exactly ONE active AudioListener while noclip is running.
    ///
    /// Without a listener on this camera everything was silent, and Unity reported "no
    /// audio listeners" every frame - the player and its camera are disabled, and the listener here
    /// is switched off in the scene (per user, 2026-09-13). Simply enabling it, however, gave
    /// "two listeners active" (per user, same day): the sound engine and UWSoundPlayer
    /// (UWAudioListenerGuard since 2026-10-24) create one themselves if they find none, and that
    /// one stayed alongside the one from the
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
        // ONLY WHILE NOCLIP FLIES (per user, 2026-11-12: the line stood there all the time): leaving noclip only masks
        // this camera (DebugFunctions.fSetSpectatorView) and leaves its object active, so OnDisable never came. The
        // body's object is off while it flies.
        GameObject lOBody = UWScene.Player;

        if (lOBody == null || !lOBody.activeInHierarchy)
            fShowLevel();
        else if (mOLevelCanvas != null && mOLevelCanvas.enabled)
            mOLevelCanvas.enabled = false;

        // Other scripts may create a listener later - check once per second.
        if (Time.unscaledTime >= mfNextListenerCheck)
        {
            mfNextListenerCheck = Time.unscaledTime + 1f;
            fKeepSingleListener();
        }

        Vector2 lOLookDelta = UWInput.Value<Vector2>(mControls, UWAct.NoclipLook);

        transform.Rotate(0f, lOLookDelta.x * mfSensitivityX, 0f, Space.World);

        mfPitch = Mathf.Clamp(mfPitch - (lOLookDelta.y * mfSensitivityY), -89f, 89f);

        Vector3 lOEuler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(mfPitch, lOEuler.y, 0f);

        Vector2 lOMove = UWInput.Value<Vector2>(mControls, UWAct.NoclipMove);
        float lfUp = (UWInput.Held(mControls, UWAct.NoclipUp) ? 1f : 0f) - (UWInput.Held(mControls, UWAct.NoclipDown) ? 1f : 0f);

        Vector3 lODirection = (transform.forward * lOMove.y) + (transform.right * lOMove.x) + (Vector3.up * lfUp);

        if (lODirection.sqrMagnitude > 1f)
            lODirection.Normalize();

        float lfSpeed = mfSpeed * (UWInput.Held(mControls, UWAct.NoclipSprint) ? mfSprintMultiplier : 1f);

        transform.position += lODirection * lfSpeed * Time.deltaTime;
    }
}
