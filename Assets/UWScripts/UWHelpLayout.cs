using UnityEngine;

/// <summary>
/// Where the help window goes and what it does to the picture (decided per user, 2026-09-25):
/// while the help is open the classic 320x200 frame is shown at 4:3 and SLIDES TO THE LEFT, and
/// the help takes the freed width on the right, at most MaxWidthPerHeight of the screen height
/// wide; on a very wide screen frame and help stand together as one centred block. A screen
/// without room beside the 4:3 frame (a real 4:3 or 5:4 one) gets the help laid OVER the right
/// part of the picture instead.
///
/// Everything here is in the canvas units of the classic UI (the frame is 320 x 200 of them, the
/// canvas is 200 high and as wide as the screen's aspect gives), so it is independent of the
/// resolution. Opening and closing blend over SlideSeconds; the frame's squeeze and slide follow
/// Blend, so both run as one movement.
/// </summary>
public static class UWHelpLayout
{
    /// <summary>How long the frame takes to slide, in seconds.</summary>
    public const float SlideSeconds = 0.25f;

    /// <summary>The help's maximum width as a share of the screen height (about 650 px at 1080;
    /// per user, 2026-09-25).</summary>
    public const float MaxWidthPerHeight = 0.6f;

    /// <summary>Below this width beside the frame (canvas units, about 215 px at 1080) the help
    /// is laid over the picture instead.</summary>
    public const float MinSideWidth = 40f;

    /// <summary>Width of the help when it lies over the picture, as a share of the canvas.</summary>
    public const float OverlayShare = 0.45f;

    private const float CanvasHeight = 200f;

    private const float FrameWidth = 320f;

    /// <summary>The 4:3 pixel width - UWGameUI.VgaPixelAspectCorrection.</summary>
    private const float FourByThreeFactor = 5f / 6f;

    /// <summary>Whether the help is (being) opened.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>0 closed, 1 open - moves towards IsOpen over SlideSeconds.</summary>
    public static float Blend { get; private set; }

    public static void SetOpen(bool pbOpen)
    {
        IsOpen = pbOpen;
    }

    /// <summary>Open without the slide - when a game is loaded and the help was left open.
    /// </summary>
    public static void OpenAtOnce()
    {
        IsOpen = true;
        mfLinear = 1f;
        Blend = 1f;
    }

    /// <summary>Closed without the slide - when a game is loaded.</summary>
    public static void CloseAtOnce()
    {
        IsOpen = false;
        mfLinear = 0f;
        Blend = 0f;
    }

    /// <summary>Moves Blend towards IsOpen; eased so the slide starts and ends softly.</summary>
    public static void Tick(float pfDeltaSeconds)
    {
        float lfStep = SlideSeconds > 0f ? pfDeltaSeconds / SlideSeconds : 1f;

        mfLinear = Mathf.Clamp01(mfLinear + (IsOpen ? lfStep : -lfStep));
        Blend = Mathf.SmoothStep(0f, 1f, mfLinear);
    }

    private static float mfLinear;

    /// <summary>The horizontal pixel factor of the frame: the setting's, blended to 4:3.</summary>
    public static float PixelFactor(float pfSettingFactor)
    {
        return Mathf.Lerp(pfSettingFactor, FourByThreeFactor, Blend);
    }

    /// <summary>Whether the help goes beside the picture on a canvas of this width.</summary>
    public static bool FitsBeside(float pfCanvasWidth)
    {
        return pfCanvasWidth - (FrameWidth * FourByThreeFactor) >= MinSideWidth;
    }

    /// <summary>
    /// The layout when fully open: how far the frame's centre moves from the canvas centre, and
    /// where the help lies (left edge and width, from the canvas' left edge).
    /// </summary>
    public static void OpenLayout(float pfCanvasWidth, out float pfFrameShift, out float pfHelpLeft,
        out float pfHelpWidth)
    {
        float lfMaxWidth = MaxWidthPerHeight * CanvasHeight;

        if (!FitsBeside(pfCanvasWidth))
        {
            pfFrameShift = 0f;
            pfHelpWidth = Mathf.Min(lfMaxWidth, pfCanvasWidth * OverlayShare);
            pfHelpLeft = pfCanvasWidth - pfHelpWidth;

            return;
        }

        // The classic frame as wide as it is now (UWClassicWide: narrowed to leave the help room).
        float lfFrame = (FrameWidth + UWClassicWide.Extra) * FourByThreeFactor;

        pfHelpWidth = Mathf.Min(pfCanvasWidth - lfFrame, lfMaxWidth);

        float lfBlockLeft = (pfCanvasWidth - (lfFrame + pfHelpWidth)) / 2f;

        pfFrameShift = lfBlockLeft + (lfFrame / 2f) - (pfCanvasWidth / 2f);
        pfHelpLeft = lfBlockLeft + lfFrame;
    }

    /// <summary>The frame's right edge right now (canvas units from the left) - the help lies
    /// BEHIND the frame while it slides (per user, 2026-09-25), so it is cut off there.</summary>
    public static float FrameRightEdge(float pfCanvasWidth, float pfPixelFactor)
    {
        return (pfCanvasWidth / 2f) + FrameShift(pfCanvasWidth) + ((FrameWidth + UWClassicWide.Extra) * pfPixelFactor / 2f);
    }

    /// <summary>The frame's shift right now (canvas units, negative is left).</summary>
    public static float FrameShift(float pfCanvasWidth)
    {
        if (Blend <= 0f)
            return 0f;

        OpenLayout(pfCanvasWidth, out float lfShift, out float _, out float _);

        return lfShift * Blend;
    }
}
