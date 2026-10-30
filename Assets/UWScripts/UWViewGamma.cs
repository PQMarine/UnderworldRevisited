using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UWDataImport.UWData;

/// <summary>
/// THE 3D VIEW'S GAMMA (issue 9 on GitHub, 2026-10-30, per user: a slider in the Graphics window
/// that works in both render modes; the curve and its limits are UWViewGammaCurve's). Above Low the
/// Remastered mode lights the level with the player's light falling off with distance, which
/// leaves more of it dark than the palette mode's light table does - the issue asked for a way to
/// see the small objects in it.
///
/// ONE WAY FOR BOTH MODES: a global Volume of its own that holds only URP's lift, gamma and gain,
/// switched on while the value is not 1. Everything the world's cameras draw goes through it - the
/// palette renderer's colours, its effects and ghosts, the Remastered lighting - and nothing the
/// interface draws: that lies on screen-space canvases above the camera, as for the colour help's
/// Night Vision grey. While it is on the cameras need their post-processing (ForcesPostProcessing,
/// written by UWRenderPathHost, the one writer of the camera's flags), as for the world at the
/// original's resolution. At 1 the Volume is off and the palette mode's picture is the one of
/// before, pixel for pixel.
///
/// URP's gamma is a power on the linear value, 1 / (1 + w) for a neutral colour and the offset w
/// (ColorUtils.PrepareLiftGammaGain), so w = g - 1 gives the power 1 / g of the curve.
/// </summary>
public static class UWViewGamma
{
    /// <summary>Above Remastered's Volume (UWRemasterRenderPath, 1000); the two hold different
    /// effects, so the order only matters for the blend of equal ones, of which there are none.</summary>
    private const float VolumePriority = 1001f;

    private static Volume mOVolume;

    private static LiftGammaGain mOLiftGammaGain;

    /// <summary>The value in force, within the curve's limits.</summary>
    public static float Value
    {
        get { return UWViewGammaCurve.Clamp(UWUserSettings.ViewGamma); }
        set
        {
            UWUserSettings.ViewGamma = UWViewGammaCurve.Clamp(value);
            UWUserSettings.Save();
        }
    }

    public static bool IsNeutral => UWViewGammaCurve.IsNeutral(Value);

    /// <summary>Whether the cameras need their post-processing for the gamma now.</summary>
    public static bool ForcesPostProcessing => !IsNeutral && mOLiftGammaGain != null;

    /// <summary>Each frame from UWRenderPathHost: the Volume under the host (it goes with the
    /// scene), on with the value in force or off at 1.</summary>
    public static void Tick(Transform pOHost)
    {
        if (mOVolume == null && pOHost != null)
            fCreate(pOHost);

        if (mOVolume == null)
            return;

        bool lbOn = !IsNeutral;

        if (mOVolume.gameObject.activeSelf != lbOn)
            mOVolume.gameObject.SetActive(lbOn);

        if (lbOn)
            mOLiftGammaGain.gamma.value = new Vector4(1f, 1f, 1f, Value - 1f);
    }

    private static void fCreate(Transform pOHost)
    {
        GameObject lOHolder = new GameObject("UW View Gamma");

        lOHolder.transform.SetParent(pOHost, false);

        mOVolume = lOHolder.AddComponent<Volume>();
        mOVolume.isGlobal = true;
        mOVolume.priority = VolumePriority;
        mOVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        mOVolume.profile.name = "UW View Gamma";

        // With true every parameter is marked overridden; lift and gain keep their neutral values.
        mOLiftGammaGain = mOVolume.profile.Add<LiftGammaGain>(true);
    }

    /// <summary>The scene goes: the holder goes with the host, the profile is destroyed here.</summary>
    public static void Dispose()
    {
        if (mOVolume != null && mOVolume.profile != null)
            Object.Destroy(mOVolume.profile);

        mOVolume = null;
        mOLiftGammaGain = null;
    }
}
