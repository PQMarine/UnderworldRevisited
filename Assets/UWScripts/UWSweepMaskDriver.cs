using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Feeds the palette path's sprites the tiles the original would draw from the current eye
/// (UWSweepMask): every frame after the camera moved, a 64 by 64 mask as the global texture
/// _UWSweepMask, read by UWPainterOrder.hlsl (UWPainterSweepHides) in UWBillboardPalette. Off - the
/// texture marked not ready - outside the palette path, where the true depth needs no help.
/// Created once and kept across scene loads.
/// </summary>
public class UWSweepMaskDriver : MonoBehaviour
{
    private static UWSweepMaskDriver msInstance;

    private static readonly int msMaskId = Shader.PropertyToID("_UWSweepMask");

    private static readonly int msMaskReadyId = Shader.PropertyToID("_UWSweepMaskReady");

    private readonly UWSweepMask mOMask = new UWSweepMask();

    private Texture2D mOTexture;

    /// <summary>How long a mask built for a standing eye is kept before it is built again.</summary>
    private const float StandingRebuildSeconds = 0.5f;

    private bool mbApplied;

    private UWLevel mOBuiltLevel;

    private int miBuiltX;

    private int miBuiltY;

    private int miBuiltHeading;

    private float mfBuiltAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void fCreate()
    {
        if (msInstance != null)
            return;

        GameObject lOObject = new GameObject("UW Sweep Mask");

        DontDestroyOnLoad(lOObject);
        msInstance = lOObject.AddComponent<UWSweepMaskDriver>();
    }

    private void LateUpdate()
    {
        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        Camera lOCamera = UWViewpoint.Current;

        if (lOSettings == null || lOSettings.IsRemasteredActive || lOLoader == null || lOLoader.CurrentLevel == null
            || lOCamera == null)
        {
            Shader.SetGlobalFloat(msMaskReadyId, 0f);
            mbApplied = false;

            return;
        }

        Vector3 lOEye = lOCamera.transform.position;
        int liX = UWViewpoint.WorldToOriginalX(lOEye.x);
        int liY = UWViewpoint.WorldToOriginalY(lOEye.z);
        int liHeading = UWViewpoint.DegreesToAngle(Mathf.Repeat(lOCamera.transform.eulerAngles.y, 360f)) & 0xFFFF;

        // Only after the eye moved, as the class comment says (until 2026-10-11 every frame:
        // three sweeps, 4225 arc tangents and a texture upload). A standing eye still gets a
        // fresh mask twice a second, because a trap can change the tiles around it.
        bool lbSame = mbApplied && lOLoader.CurrentLevel == mOBuiltLevel && liX == miBuiltX && liY == miBuiltY
            && liHeading == miBuiltHeading;

        if (lbSame && Time.unscaledTime - mfBuiltAt < StandingRebuildSeconds)
            return;

        mOMask.Build(lOLoader.CurrentLevel, liX, liY, liHeading);
        mOBuiltLevel = lOLoader.CurrentLevel;
        miBuiltX = liX;
        miBuiltY = liY;
        miBuiltHeading = liHeading;
        mfBuiltAt = Time.unscaledTime;
        mbApplied = true;

        if (mOTexture == null)
        {
            mOTexture = new Texture2D(UWSweepMask.Size, UWSweepMask.Size, TextureFormat.R8, false, true);
            mOTexture.name = "UW Sweep Mask";
            mOTexture.filterMode = FilterMode.Point;
            mOTexture.wrapMode = TextureWrapMode.Clamp;
        }

        mOTexture.SetPixelData(mOMask.Mask, 0);
        mOTexture.Apply(false, false);

        Shader.SetGlobalTexture(msMaskId, mOTexture);
        Shader.SetGlobalFloat(msMaskReadyId, 1f);
    }

    private void OnDestroy()
    {
        if (msInstance == this)
            msInstance = null;

        if (mOTexture != null)
            Destroy(mOTexture);
    }
}
