using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Feeds the palette path's sprites the tiles the original would draw from the current eye
/// (UWSweepMask): a 64 by 64 mask as the global texture _UWSweepMask, read by UWPainterOrder.hlsl
/// (UWPainterSweepHides) in UWBillboardPalette. Off - the texture marked not ready and taken away
/// (Clear) - outside the palette path, where the true depth needs no help. A child of the palette
/// path (UWPaletteRenderPath), ticked only while it is in force, since cleanup step 6.5
/// (2026-10-24); until then a component of its own, kept across scene loads, that asked the
/// render mode every frame.
///
/// BUILT ONLY WHEN SOMETHING IT READS CHANGED (B35, 2026-10-11; completed in cleanup step 6.3,
/// 2026-10-24): the eye's tile position, its heading and the level, and the tiles themselves -
/// the sweep reads nothing but their types, and those change only through a geometry rebuild
/// (UWLevelLoader.GeometryRevision). A standing eye costs nothing; until this step it still built
/// the mask twice a second in case a trap had changed the tiles around it.
/// </summary>
public sealed class UWSweepMaskDriver
{
    private readonly UWSweepMask mOMask = new UWSweepMask();

    private Texture2D mOTexture;

    private bool mbApplied;

    private UWLevel mOBuiltLevel;

    private int miBuiltX;

    private int miBuiltY;

    private int miBuiltHeading;

    private int miBuiltRevision;

    /// <summary>Every frame while the palette path is in force (UWPaletteRenderPath.LateTick).</summary>
    public void Tick()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        Camera lOCamera = UWViewpoint.Current;

        if (lOLoader == null || lOLoader.CurrentLevel == null || lOCamera == null)
        {
            UWShaderGlobals.SetFloat(UWShaderIds.SweepMaskReady, 0f);
            mbApplied = false;

            return;
        }

        Vector3 lOEye = lOCamera.transform.position;
        int liX = UWViewpoint.WorldToOriginalX(lOEye.x);
        int liY = UWViewpoint.WorldToOriginalY(lOEye.z);
        int liHeading = UWViewpoint.DegreesToAngle(Mathf.Repeat(lOCamera.transform.eulerAngles.y, 360f)) & 0xFFFF;

        // Only after the eye moved or the tiles changed, as the class comment says (until
        // 2026-10-11 every frame: three sweeps, 4225 arc tangents and a texture upload; until
        // 2026-10-24 also every half second for a standing eye, as a trap can change the tiles
        // around it - the geometry revision says when one did).
        int liRevision = lOLoader.GeometryRevision;

        if (mbApplied && lOLoader.CurrentLevel == mOBuiltLevel && liX == miBuiltX && liY == miBuiltY
            && liHeading == miBuiltHeading && liRevision == miBuiltRevision)
            return;

        mOMask.Build(lOLoader.CurrentLevel, liX, liY, liHeading);
        mOBuiltLevel = lOLoader.CurrentLevel;
        miBuiltX = liX;
        miBuiltY = liY;
        miBuiltHeading = liHeading;
        miBuiltRevision = liRevision;
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

        UWShaderGlobals.SetTexture(UWShaderIds.SweepMask, mOTexture);
        UWShaderGlobals.SetFloat(UWShaderIds.SweepMaskReady, 1f);
    }

    /// <summary>The palette path goes: not ready, and the mask taken away; built again from the
    /// first frame it is back.</summary>
    public void Clear()
    {
        UWShaderGlobals.SetFloat(UWShaderIds.SweepMaskReady, 0f);
        UWShaderGlobals.ClearTexture(UWShaderIds.SweepMask);
        mbApplied = false;
    }

    /// <summary>The palette path goes with its scene.</summary>
    public void Dispose()
    {
        if (mOTexture != null)
        {
            UWShaderGlobals.Invalidate(mOTexture);
            Object.Destroy(mOTexture);
        }

        mOTexture = null;
        mbApplied = false;
    }
}
