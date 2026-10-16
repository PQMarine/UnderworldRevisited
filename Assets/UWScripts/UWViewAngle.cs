using UnityEngine;

/// <summary>
/// THE VIEW ANGLE (per user, 2026-10-10: the widened view stretches at its edges - "a slider";
/// one for both schemes, per user the same day on the question): the HORIZONTAL angle of the 3D
/// view as one sees it, 60 to 120 degrees, kept in UWUserSettings.ViewAngle. At 0, "Original",
/// every scheme keeps its own: the classic hole and Classic Wide the original's vertical angle
/// (75 degrees across its 4:3 hole, so a widened hole shows more to the sides, Hor+), the modern
/// scheme the camera's own. A set angle is turned into the vertical one Unity's camera takes, from
/// the view's aspect (UWGameUI.fApplyGameCamera).
/// </summary>
public static class UWViewAngle
{
    public const float Min = 60f;

    public const float Max = 120f;

    /// <summary>The angle set, or 0 for the scheme's own.</summary>
    public static float Horizontal
    {
        get
        {
            float lfAngle = UWUserSettings.ViewAngle;

            return lfAngle <= 0f ? 0f : Mathf.Clamp(lfAngle, Min, Max);
        }
        set
        {
            UWUserSettings.ViewAngle = value <= 0f ? 0f : Mathf.Clamp(value, Min, Max);
            UWUserSettings.Save();
        }
    }

    public static bool IsOriginal => Horizontal <= 0f;

    /// <summary>The vertical angle for a view of this aspect (width over height, as shown): the one
    /// giving the angle set across it, or pfOwnVertical with none set.</summary>
    public static float Vertical(float pfAspect, float pfOwnVertical)
    {
        if (IsOriginal || pfAspect <= 0f)
            return pfOwnVertical;

        return 2f * Mathf.Atan(Mathf.Tan(Horizontal * Mathf.Deg2Rad / 2f) / pfAspect) * Mathf.Rad2Deg;
    }

    /// <summary>The horizontal angle a camera shows right now (for the slider at Original).</summary>
    public static float HorizontalOf(Camera pOCamera)
    {
        if (pOCamera == null)
            return 75f;

        return 2f * Mathf.Atan(Mathf.Tan(pOCamera.fieldOfView * Mathf.Deg2Rad / 2f) * pOCamera.aspect) * Mathf.Rad2Deg;
    }
}
