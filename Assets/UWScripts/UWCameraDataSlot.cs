using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP's additional data of one camera slot - the player camera, the remote view - looked up again
/// only when the slot's camera is another one (cleanup step 6.3, 2026-10-24). The camera flags are
/// written every frame, and until this step every writer asked the camera for its component every
/// frame (the palette toggle twice, the world resolution twice).
/// </summary>
public sealed class UWCameraDataSlot
{
    private Camera mOCamera;

    private UniversalAdditionalCameraData mOData;

    /// <summary>The data of this camera, null for none (a destroyed camera reads as none).</summary>
    public UniversalAdditionalCameraData Get(Camera pOCamera)
    {
        if (pOCamera == null)
            return null;

        if (!ReferenceEquals(pOCamera, mOCamera) || mOData == null)
        {
            mOCamera = pOCamera;
            mOData = pOCamera.GetComponent<UniversalAdditionalCameraData>();
        }

        return mOData;
    }
}
