using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// How the classic 320x200 screen is fitted into the window (per user, 2026-09-26, with a
/// screenshot of a window dragged narrow): as large as it fits WHOLE. Until then every classic
/// canvas followed the height alone, so a window narrower than the frame cut its right part
/// off. Now a wide window keeps the height and gets bars left and right as before, a narrow one
/// (or a monitor turned upright) keeps the width and gets bars at the top and the bottom.
///
/// The frame's width in canvas units follows the pixel proportion (UWGameUI.
/// HorizontalPixelFactor: 320 with square pixels, 266.7 at 4:3), so the switch between the two
/// ways of fitting lies exactly where the squeezed frame fills the window.
///
/// The canvases do it through UWFitCanvas; the few places that turn canvas units into screen
/// pixels themselves (UWGameUI, the compass) take Scale and BottomMarginPixels from here.
/// </summary>
public static class UWUiFit
{
    public const float FrameHeight = 200f;

    public const float FrameWidthUnits = 320f;

    /// <summary>The frame's width in canvas units at the current pixel proportion.</summary>
    public static float FrameWidth => (FrameWidthUnits + UWClassicWide.Extra) * UWGameUI.HorizontalPixelFactor;

    /// <summary>The window is narrower than the frame: fit to the width, bars top and bottom.
    /// </summary>
    public static bool IsNarrow => Screen.height > 0 && Screen.width < Screen.height * (FrameWidth / FrameHeight);

    /// <summary>Screen pixels per canvas unit.</summary>
    public static float Scale
    {
        get
        {
            if (Screen.height <= 0)
                return 1f;

            return IsNarrow ? Screen.width / FrameWidth : Screen.height / FrameHeight;
        }
    }

    /// <summary>The canvas' size in its units, straight from the window - the same as the
    /// scaled canvas rect, but valid in this frame already.</summary>
    public static float CanvasWidth => Screen.width / Scale;

    public static float CanvasHeight => Screen.height / Scale;

    /// <summary>The height of the bar under (and over) the frame, in screen pixels; zero
    /// unless the window is narrow.</summary>
    public static float BottomMarginPixels => Mathf.Max(0f, (Screen.height - (FrameHeight * Scale)) / 2f);

    /// <summary>Sets a classic canvas' scaler to the fitting above - both ways of the
    /// ScaleWithScreenSize mode give exactly Scale: the height against 200, or the width
    /// against the frame's width.</summary>
    public static void Apply(CanvasScaler pOScaler)
    {
        if (pOScaler == null)
            return;

        pOScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        pOScaler.referenceResolution = new Vector2(FrameWidth, FrameHeight);
        pOScaler.matchWidthOrHeight = IsNarrow ? 0f : 1f;
    }
}
