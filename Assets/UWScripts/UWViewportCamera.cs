using UnityEngine;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE VIEW CAMERA'S RECTANGLE, ASPECT AND FIELD OF VIEW (cleanup 5.18, 2026-10-23: out of
/// UWGameUI, as it was): where on the screen the world is rendered and at what angle, set anew
/// every frame by UWGameUI.Update, BEFORE the screen shake - UWScreenShake runs last in
/// LateUpdate (UWUpdateOrder.Shake) and stays the last writer of the camera's rotation; nothing
/// here touches it.
///
///   - THE CLASSIC SCHEME: the hole in the frame (UWClassicScreen's game area, the original's
///     view window of 172 x 113 at 52/19 widened to the hole's 176 x 121 - UWClassicLayout.Frame)
///     at the CRT's pixel proportion, the original's own vertical angle over the hole's rows;
///   - CLASSIC WIDE (UWModernClassicFrame): the widened hole, the same vertical angle;
///   - THE MODERN SCHEME: the whole screen with the camera's own angle from the Inspector.
///   The Graphics menu's view angle (UWViewAngle) overrides each scheme's own, at Original it is
///   that scheme's.
/// </summary>
public sealed class UWViewportCamera
{
    public UWViewportCamera(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    private readonly UWGameUI mOUi;

    private Camera mOCamera;

    private float mfDefaultFov;

    /// <summary>The camera the UI drives - UWGameUI sits on the same GameObject as Camera.main
    /// (see UWLevelLoader.fInitialiseUi). mfDefaultFov remembers the Modern value set in the
    /// Inspector, before the original scheme overwrites it (see Apply).</summary>
    internal void Attach(Camera pOCamera)
    {
        mOCamera = pOCamera;

        if (mOCamera != null)
            mfDefaultFov = mOCamera.fieldOfView;
    }

    /// <summary>
    /// Original scheme: the camera renders only into the small 3D viewport
    /// (Camera.rect, normalised to Screen.width/height) instead of onto the full screen with
    /// the UI simply on top - exactly as in the original, which likewise rendered only this window.
    /// A simple "full screen + UI laid over it" shows in the hole only
    /// the narrow central cutout of the full camera FOV, so near floor objects
    /// (e.g. a skull directly in front of the feet) stay invisible, although in the original they
    /// were visible at the same position - CONFIRMED per user comparison with a real
    /// DOSBox screenshot. Modern: full area, the camera's own FOV.
    ///
    /// THE ANGLE IS THE ORIGINAL'S (read 2026-10-22, UWClassicLayout.Frame.ViewZoom and the
    /// entries around it): the tangent of the half angle at the top edge of its 113-row window
    /// is its zoom, 0.7629 (74.7 degrees over those rows); the camera's area has
    /// WindowHoleHeight rows, so the tangent is scaled by that ratio and the focal length per
    /// row stays the original's. Unity's Camera.fieldOfView is vertical, the horizontal angle
    /// follows from the aspect (at the corrected one about 89 degrees across our 176 columns,
    /// the original's 88.1 across its 172). Until this reading the angle was an Inspector value,
    /// 90 horizontal, tuned by eye - and the code's 75 default was UnderworldGodot's vertical one.
    ///
    /// What counts here is NOT the real (square) pixel aspect ratio of the 176x121 hole,
    /// but one emulated via UWClassicScreen.VgaPixelAspectCorrection: classic 320x200 VGA mode
    /// had, on the 4:3 CRT monitor the original was designed for, NO
    /// square pixels (320x200 raw = 8:5 = 1.6, but displayed at 4:3 = 1.333 -> each
    /// pixel row effectively 20% taller than wide, pixel aspect ratio 5:6) - CONFIRMED per
    /// GOG forum discussion (see chat answer). The original rendering (and the
    /// uncorrected DOSBox screenshot it is compared against) is therefore itself slightly
    /// squashed vertically - no combination of FOV/camera height could fix that, because the
    /// aspect ratio problem was a different source of error than position/view angle.
    /// Camera.aspect is therefore deliberately overwritten with the corrected value (only for the
    /// projection - Camera.rect stays exactly the real pixel hole), Modern has to lift the override
    /// again via ResetAspect().
    /// </summary>
    internal void Apply(bool pbOriginalScheme)
    {
        if (mOCamera == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        // The projection follows the field of view again; the classic view shifts it below.
        mOCamera.ResetProjectionMatrix();

        if (!pbOriginalScheme)
        {
            // CLASSIC WIDE (UWModernClassicFrame): the classic scheme's area, widened. The hole is
            // shown at the CRT's proportion already, so its raw aspect needs no correction; the
            // vertical field of view is the original's over the area's rows, so a wider hole
            // shows more to the sides and the same up and down.
            if (UWModernClassicFrame.IsActive)
            {
                Rect lOArea = UWModernClassicFrame.CameraRect;

                mOCamera.rect = new Rect(lOArea.x / Screen.width, lOArea.y / Screen.height, lOArea.width / Screen.width, lOArea.height / Screen.height);
                mOCamera.ResetAspect();

                // The view angle set in the Graphics menu, if any (UWViewAngle).
                mOCamera.fieldOfView = UWViewAngle.Vertical(mOCamera.aspect,
                    fOriginalVerticalFov(UWHudArt.ClassicWideCameraBottom - UWHudArt.ClassicWideCameraTop));
                return;
            }

            mOCamera.rect = new Rect(0f, 0f, 1f, 1f);
            mOCamera.ResetAspect();
            mOCamera.fieldOfView = UWViewAngle.Vertical(mOCamera.aspect, mfDefaultFov);
            return;
        }

        UWClassicScreen lOScreen = mOUi.ClassicScreen;

        mOCamera.rect = new Rect(
            lOScreen.GameAreaLeft / Screen.width,
            lOScreen.GameAreaBottom / Screen.height,
            (lOScreen.GameAreaRight - lOScreen.GameAreaLeft) / Screen.width,
            (lOScreen.GameAreaTop - lOScreen.GameAreaBottom) / Screen.height);

        // ResetAspect() BEFORE reading: a manual aspect override (see below) otherwise
        // persists across frames and from the second frame on Camera.aspect would
        // already return the corrected value from the last pass instead of the
        // real raw aspect ratio computed from Camera.rect - the correction
        // would otherwise add up frame by frame.
        // At 4:3 (UWDisplayAspect) the hole itself is already shown at the CRT proportion, so its raw
        // aspect is the corrected one and must not be corrected a second time.
        mOCamera.ResetAspect();
        // General form for every factor in between, as the help window blends it: 1 corrects by
        // 5/6, 5/6 by nothing.
        float lfCorrectedAspect = mOCamera.aspect * UWClassicScreen.VgaPixelAspectCorrection / UWClassicScreen.HorizontalPixelFactor;
        mOCamera.aspect = lfCorrectedAspect;

        // The vertical field of view is the original's over the hole's rows: a hole widened by
        // UWClassicWide shows more to the sides, the same up and down.
        mOCamera.fieldOfView = UWViewAngle.Vertical(lfCorrectedAspect, fOriginalVerticalFov(UWClassicLayout.Frame.WindowHoleHeight));

        // THE ORIGINAL'S PROJECTION CENTRE (UWClassicLayout.Frame.ProjectionCentreColumn/Row) instead of
        // the hole's middle: an off-centre projection, the content moved by the difference in the
        // hole's columns and rows (OpenGL convention: ndc = p * pos / -z - p[i, 2]).
        int liColumns = UWClassicLayout.Frame.WindowHoleWidth + UWClassicWide.Extra;
        int liRows = UWClassicLayout.Frame.WindowHoleHeight;
        float lfHoleColumn = UWClassicLayout.Frame.WindowHoleLeft + (liColumns * 0.5f);
        float lfHoleRow = UWClassicLayout.Frame.WindowHoleTop + (liRows * 0.5f);
        float lfRight = UWClassicLayout.Frame.ProjectionCentreColumn + (UWClassicWide.Extra * 0.5f) - lfHoleColumn;
        float lfDown = UWClassicLayout.Frame.ProjectionCentreRow - lfHoleRow;

        Matrix4x4 lOProjection = mOCamera.projectionMatrix;

        lOProjection[0, 2] -= 2f * lfRight / liColumns;
        lOProjection[1, 2] += 2f * lfDown / liRows;
        mOCamera.projectionMatrix = lOProjection;
    }

    /// <summary>The original's vertical angle for a camera area of this many original rows: its
    /// tangent at the top edge of the 113-row window (UWClassicLayout.Frame.VerticalViewTangent)
    /// scaled by the rows, so one row subtends what it does in the original.</summary>
    private static float fOriginalVerticalFov(int piRows)
    {
        float lfTangent = UWClassicLayout.Frame.VerticalViewTangent * piRows / UWClassicLayout.Frame.ViewWindowHeight;

        return 2f * Mathf.Atan(lfTangent) * Mathf.Rad2Deg;
    }
}
