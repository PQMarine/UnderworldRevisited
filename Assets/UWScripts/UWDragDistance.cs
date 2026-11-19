using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// WHEN A HELD BUTTON BECOMES A DRAG, on the classic screen as the original measures it (2026-11-18, per user: asked
/// whether the second game had the first's "dead zone", which both had as six SCREEN pixels): mouse_dragged (uw1 and
/// uw2 ui/MOUSE.C) moves its own pointer inside the 320 x 200 picture and calls it a drag once |dx| + |dy| is above 6
/// of those pixels (UWClickRules.IsDrag). An original pixel here is UWUiFit.Scale screen pixels tall and that times the
/// pixel factor wide, so on a large window the zone is several times what six screen pixels were. The modern scheme
/// has no such picture and keeps its six screen pixels.
/// </summary>
public static class UWDragDistance
{
    /// <summary>The modern scheme's threshold in screen pixels (the classic one's until 2026-11-18).</summary>
    public const float ModernPixels = 6f;

    /// <summary>Whether the pointer has gone far enough from where the button went down to drag.</summary>
    public static bool Crossed(Vector2 pOFrom, Vector2 pOTo)
    {
        if (UWControlScheme.IsModern)
            return Vector2.Distance(pOFrom, pOTo) >= ModernPixels;

        float lfRow = Mathf.Max(0.0001f, UWUiFit.Scale);
        float lfColumn = Mathf.Max(0.0001f, lfRow * UWClassicScreen.HorizontalPixelFactor);

        return UWClickRules.IsDrag((pOTo.x - pOFrom.x) / lfColumn, (pOTo.y - pOFrom.y) / lfRow);
    }
}
