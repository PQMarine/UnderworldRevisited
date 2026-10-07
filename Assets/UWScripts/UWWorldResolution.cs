using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// THE WORLD AT THE ORIGINAL'S RESOLUTION (asked for on Reddit, 2026-10-07): the 3D view is drawn
/// with one pixel row per row of the original's 320x200 screen and scaled up with hard pixel
/// edges, while the interface stays at the full resolution. A world pixel is as tall as a pixel
/// of the classic frame on this screen (UWUiFit.Scale screen pixels), so:
///
///   classic scheme  the camera fills the hole of the frame (UWGameUI.fApplyGameCamera), 121 rows
///                   tall - exactly the original's grid, the world's pixels line up with the frame's;
///   modern scheme   the whole screen, 200 rows, the width following the screen's aspect ratio
///                   (as the Doom ports do it).
///
/// A MULTIPLE OF IT (per user, the same day, "bis maximal x4"): 1x to 4x the original's rows,
/// 200 to 800 full screen, chosen with a slider. 4x is locked while it would give a world pixel
/// less than two screen pixels (a window under 1600 rows): below that the pixels no longer read
/// as blocks, only as uneven ones, a column of one screen pixel beside one of two. The chosen
/// value is kept; the locked step gives 3x until the window is tall enough.
///
/// The pixels are square at the row height. With the classic screen at 4:3 the original's
/// columns are 5/6 as wide as that (UWGameUI.HorizontalPixelFactor), so the classic hole has 147
/// columns instead of 176 - URP's render scale is one factor for both axes.
///
/// HOW: URP's own render scale with the nearest-neighbour upscaling filter. Every camera that
/// draws to the screen is scaled, the screen-space overlays of the interface are not, and the
/// camera keeps its pixel rect, so picking (ScreenPointToRay) and every shader that reads
/// _ScaledScreenParams work unchanged. URP clamps the render scale at 0.1: from a window about
/// 2000 pixels tall on, the world gets slightly more rows than the original's.
///
/// POST-PROCESSING IS SWITCHED ON with it: URP applies the nearest-neighbour filter only in its
/// post-processing passes; without them the last copy samples the small picture bilinearly and
/// it looks blurred (per user, 2026-10-07, the first build: "wirkt es aber unscharf"). With no
/// effect of a volume active the post-processing leaves the colours as they are. When the switch
/// goes off the camera gets back what the render mode wants (UWRemasterRenderer turns it on for
/// Remastered and off for the palette path).
///
/// THE PIPELINE ASSET ITSELF IS SET TO NEAREST-NEIGHBOUR (UWRenderPipeline.asset, Upscaling
/// Filter): a build strips the shader variant of the point upscaling (_POINT_SAMPLING in
/// FinalPost.shader) unless an asset in it asks for that filter, and the pass falls back to the
/// bilinear one - the second build was as blurred as the first (per user, the same day). At the
/// full resolution the filter does nothing, URP does not scale below a 5 percent difference.
///
/// The pipeline asset is changed in place; its values from before are put back when this object
/// goes, so a play session in the editor leaves the asset as it was.
/// </summary>
public class UWWorldResolution : MonoBehaviour
{
    private static UWWorldResolution msInstance;

    private static bool mbLoaded;

    private static bool mbEnabled;

    private static int miFactor = 1;

    /// <summary>The highest multiple of the original's resolution.</summary>
    public const int MaxFactor = 4;

    /// <summary>The fewest screen pixels a world pixel may have at 4x.</summary>
    private const float MinScreenPixelsAtMax = 2f;

    private UniversalRenderPipelineAsset mOAsset;

    private float mfOriginalRenderScale;

    private UpscalingFilterSelection meOriginalFilter;

    /// <summary>The switch, kept in the user's settings (UWUserSettings).</summary>
    public static bool Enabled
    {
        get
        {
            fEnsureLoaded();

            return mbEnabled;
        }

        set
        {
            fEnsureLoaded();

            mbEnabled = value;
            UWUserSettings.OriginalWorldResolution = value;
            UWUserSettings.Save();
        }
    }

    /// <summary>The chosen multiple, 1 to MaxFactor, kept in the user's settings.</summary>
    public static int Factor
    {
        get
        {
            fEnsureLoaded();

            return miFactor;
        }

        set
        {
            fEnsureLoaded();

            miFactor = Mathf.Clamp(value, 1, MaxFactor);
            UWUserSettings.OriginalWorldResolutionFactor = miFactor;
            UWUserSettings.Save();
        }
    }

    /// <summary>Whether MaxFactor gives every world pixel at least two screen pixels on this
    /// window.</summary>
    public static bool IsMaxFactorAllowed => UWUiFit.Scale / MaxFactor >= MinScreenPixelsAtMax;

    /// <summary>The multiple in use: the chosen one, the locked step one lower.</summary>
    public static int EffectiveFactor => Mathf.Min(Factor, IsMaxFactorAllowed ? MaxFactor : MaxFactor - 1);

    private static void fEnsureLoaded()
    {
        if (mbLoaded)
            return;

        mbLoaded = true;
        mbEnabled = UWUserSettings.OriginalWorldResolution;
        miFactor = Mathf.Clamp(UWUserSettings.OriginalWorldResolutionFactor, 1, MaxFactor);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void fCreate()
    {
        if (msInstance != null)
            return;

        GameObject lOObject = new GameObject("UW World Resolution");

        DontDestroyOnLoad(lOObject);
        msInstance = lOObject.AddComponent<UWWorldResolution>();
    }

    private void Awake()
    {
        mOAsset = UniversalRenderPipeline.asset;

        if (mOAsset == null)
            return;

        mfOriginalRenderScale = mOAsset.renderScale;
        meOriginalFilter = mOAsset.upscalingFilter;
    }

    private bool mbForcedPostProcessing;

    private void LateUpdate()
    {
        if (mOAsset == null)
            return;

        if (!Enabled)
        {
            fSet(mfOriginalRenderScale, meOriginalFilter);

            if (mbForcedPostProcessing)
            {
                UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

                fSetPostProcessing(lOSettings != null && lOSettings.IsRemasteredActive);
                mbForcedPostProcessing = false;
            }

            return;
        }

        fSet(fTargetScale(), UpscalingFilterSelection.Point);
        fSetPostProcessing(true);
        mbForcedPostProcessing = true;
    }

    /// <summary>The post-processing switch of the player camera and of the remote camera
    /// (Roaming Sight, the camera trap), which draws in its place.</summary>
    private static void fSetPostProcessing(bool pbOn)
    {
        fSetPostProcessing(Camera.main, pbOn);

        if (UWRemoteCamera.Current != null)
            fSetPostProcessing(UWRemoteCamera.Current.ViewCamera, pbOn);
    }

    private static void fSetPostProcessing(Camera pOCamera, bool pbOn)
    {
        if (pOCamera == null)
            return;

        UniversalAdditionalCameraData lOData = pOCamera.GetUniversalAdditionalCameraData();

        if (lOData != null && lOData.renderPostProcessing != pbOn)
            lOData.renderPostProcessing = pbOn;
    }

    /// <summary>The render scale that gives the camera EffectiveFactor rows per original pixel row:
    /// its pixel height in rows of UWUiFit.Scale / EffectiveFactor, rounded, with a quarter row more so that URP's cut to
    /// whole pixels does not lose the last one.</summary>
    private static float fTargetScale()
    {
        float lfRowHeight = UWUiFit.Scale / EffectiveFactor;

        if (lfRowHeight <= 1f)
            return 1f;

        Camera lOCamera = Camera.main;
        float lfPixelHeight = lOCamera != null ? lOCamera.pixelHeight : Screen.height;

        if (lfPixelHeight <= 0f)
            return 1f;

        float lfRows = Mathf.Max(1f, Mathf.Round(lfPixelHeight / lfRowHeight));

        return Mathf.Min(1f, (lfRows + 0.25f) / lfPixelHeight);
    }

    private void fSet(float pfScale, UpscalingFilterSelection peFilter)
    {
        if (!Mathf.Approximately(mOAsset.renderScale, pfScale))
            mOAsset.renderScale = pfScale;

        if (mOAsset.upscalingFilter != peFilter)
            mOAsset.upscalingFilter = peFilter;
    }

    private void OnDestroy()
    {
        if (mOAsset != null)
            fSet(mfOriginalRenderScale, meOriginalFilter);

        if (msInstance == this)
            msInstance = null;
    }
}
