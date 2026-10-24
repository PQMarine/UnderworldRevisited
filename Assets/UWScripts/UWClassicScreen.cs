using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE CLASSIC SCREEN'S FIT INTO THE WINDOW, the Unity side (cleanup 5.18, 2026-10-23: out of
/// UWGameUI): the pixel scale, the letterbox bars and the help window's slide, and every
/// conversion between the original's 320 x 200 pixels and the window's - the arithmetic is
/// UWClassicGeometry's (engine-free), built here from the window for each question. The pointer
/// zones (UWPointerZones) measure the 3D view's rectangle here, the camera (UWViewportCamera)
/// renders into it, the classic HUD parts ask where the pointer is in the original's terms
/// (IsPointerInOriginalArea), the pictures and the cutscenes where a picture goes.
///
/// The pointer zone logic stays in real screen pixels, since it evaluates the real mouse position -
/// hence Scale and the margins in pixels, not canvas units.
/// </summary>
public sealed class UWClassicScreen
{
    public UWClassicScreen(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    private readonly UWGameUI mOUi;

    /// <summary>Pixel aspect ratio 5:6 of the 320x200 VGA mode on a 4:3 monitor - see
    /// UWViewportCamera.</summary>
    public const float VgaPixelAspectCorrection = UWClassicLayout.Screen.PixelAspect;

    /// <summary>
    /// Horizontal size of one original pixel relative to its height. 1 shows square pixels;
    /// on the classic screen (always at 4:3 since 2026-10-10, UWDisplayAspect) it is 5/6, so the 320x200 picture appears at 4:3 like on
    /// the CRT the game was made for and like DOSBox shows it - for comparing our screenshots
    /// with DOSBox ones (per user, 2026-09-17). The whole GameFrame is squeezed sideways and
    /// every conversion between original pixels and screen pixels takes the factor on X.
    /// </summary>
    public static float HorizontalPixelFactor
    {
        get
        {
            // Blended to 4:3 while the help window is open (UWHelpLayout).
            return UWHelpLayout.PixelFactor(UWDisplayAspect.Enabled ? VgaPixelAspectCorrection : 1f);
        }
    }

    // Pointer zones, by contrast, work in real screen pixels, since they evaluate the real
    // mouse position (WarpCursorPosition, zone classification).
    internal float scale
    {
        // Fitted whole into the window (UWUiFit): by the height, or by the width when narrow.
        get { return UWUiFit.Scale; }
    }

    internal float scaleX
    {
        get { return scale * HorizontalPixelFactor; }
    }

    /// <summary>
    /// GameFrame sits centred in the canvas (anchorMin/Max 0.5/0.5, fixed 320x200 size -
    /// see UWGameUI.fBuildCanvas) - on a screen wider than the 320:200 reference
    /// aspect ratio this creates a margin left/right (BackdropLeft/Right,
    /// see UWGameUI.Update()). Without this margin, the game area (and with it camera rect,
    /// pointer zones, world raycast gating) would on such a screen be shifted to the left relative to the
    /// actually visible viewport.
    /// </summary>
    internal float fGetLetterboxMarginRefUnits()
    {
        if (mOUi.mCanvasRoot == null || mOUi.mGameFrame == null)
            return 0f;

        // From the window itself, not from the canvas rect: that one follows the scaler, which may
        // run after this in the frame - while a window is dragged the bars lagged a frame behind.
        float lfCanvasWidth = UWUiFit.CanvasWidth;

        // The frame slides left while the help window is open (UWHelpLayout).
        return Mathf.Max(0f, ((lfCanvasWidth - (mOUi.mGameFrame.sizeDelta.x * HorizontalPixelFactor)) / 2f)
            + UWHelpLayout.FrameShift(lfCanvasWidth));
    }

    internal float fGetLetterboxMarginPixels()
    {
        return fGetLetterboxMarginRefUnits() * scale;
    }

    /// <summary>The fit of this moment: the scale, the pixel proportion, the bars and the widening,
    /// for the engine-free arithmetic.</summary>
    public UWClassicGeometry Current => new UWClassicGeometry(scale, HorizontalPixelFactor, fGetLetterboxMarginPixels(),
        UWUiFit.BottomMarginPixels, UWClassicWide.Extra);

    /// <summary>A screen point in the original's own terms (UWClassicGeometry.TryGetOriginalPoint):
    /// the column from the left, the row COUNTED FROM THE BOTTOM, 0 to 199. False outside the
    /// frame.</summary>
    public bool TryGetOriginalPoint(Vector2 pOScreen, out int piX, out int piY)
    {
        return Current.TryGetOriginalPoint(pOScreen.x, pOScreen.y, out piX, out piY);
    }

    /// <summary>Is the pointer inside an area of the original's table - both ends included, as
    /// the dispatcher tests (UWClickRules)? The position relative to the corner comes back,
    /// as the original hands it to the area's handler.</summary>
    public bool IsPointerInOriginalArea(Vector2 pOScreen, UWClickRules.Area pOArea, out int piX, out int piY)
    {
        return Current.IsPointerInOriginalArea(pOScreen.x, pOScreen.y, pOArea, out piX, out piY);
    }

    /// <summary>
    /// Screen rectangle (pixels, origin bottom left) of a picture of the given size in original
    /// pixels, placed in the viewport exactly like the window and gravestone pictures:
    /// horizontally centred in the hole, WindowContentInsetTop below its top edge
    /// (UWClassicGeometry.ViewportPictureCorner). Used by UWIntroPlayer for the viewport cutscenes.
    /// </summary>
    public Rect GetViewportPictureScreenRect(Vector2 pOSize)
    {
        return fRect(Current.ViewportPictureScreenRect(pOSize.x, pOSize.y));
    }

    /// <summary>Top-left corner (original UI pixels, top down) of a picture of the given size
    /// placed like the window pictures - see GetViewportPictureScreenRect.</summary>
    public static Vector2 GetViewportPictureCorner(Vector2 pOSize)
    {
        (float X, float Y) lOCorner = UWClassicGeometry.ViewportPictureCorner(pOSize.x, pOSize.y);

        return new Vector2(lOCorner.X, lOCorner.Y);
    }

    /// <summary>Screen rectangle (pixels, origin bottom left) of a picture whose top-left
    /// corner lies at the given original UI position (top down).</summary>
    public Rect GetUiPictureScreenRect(Vector2 pOCorner, Vector2 pOSize)
    {
        return fRect(Current.UiPictureScreenRect(pOCorner.x, pOCorner.y, pOSize.x, pOSize.y));
    }

    private static Rect fRect((float X, float Y, float Width, float Height) pORect)
    {
        return new Rect(pORect.X, pORect.Y, pORect.Width, pORect.Height);
    }

    // ------------------------------------------------- The 3D view's rectangle

    /// <summary>The hole's edges on the screen (pixels, bottom-left origin), the widening
    /// included - UWClassicGeometry.GameArea*.</summary>
    internal float GameAreaLeft => Current.GameAreaLeft;

    internal float GameAreaRight => Current.GameAreaRight;

    internal float GameAreaTop => Current.GameAreaTop;

    internal float GameAreaBottom => Current.GameAreaBottom;

    /// <summary>The 3D view's rectangle on the screen (pixels, bottom-left origin) - the gamepad's
    /// pointer keeps inside it during a held cursor movement (UWGamepadPointer).</summary>
    public Rect GameAreaRect
    {
        get
        {
            UWClassicGeometry lOGeometry = Current;

            return Rect.MinMaxRect(lOGeometry.GameAreaLeft, lOGeometry.GameAreaBottom, lOGeometry.GameAreaRight, lOGeometry.GameAreaTop);
        }
    }

    /// <summary>The pointer scheme's movement area on the screen (pixels, bottom-left origin):
    /// the original's view window, 172 x 113 at 52/19, which keeps the pointer while the button
    /// is held (UWPointerZones, the gamepad's pointer likewise) - UWClassicGeometry.MovementArea*.
    /// </summary>
    public Rect MovementAreaRect
    {
        get
        {
            UWClassicGeometry lOGeometry = Current;

            return Rect.MinMaxRect(lOGeometry.MovementAreaLeft, lOGeometry.MovementAreaBottom, lOGeometry.MovementAreaRight, lOGeometry.MovementAreaTop);
        }
    }

    /// <summary>For Interaction.cs: is a screen position inside the small
    /// 3D viewport (original scheme)? A world raycast from outside (e.g. over the
    /// paper doll/backpack panel) would otherwise hit invisible geometry behind the UI, since
    /// the UI does not block the physics query (no EventSystem/GraphicRaycaster).</summary>
    public bool IsScreenPositionInGameArea(Vector2 pOScreenPos)
    {
        return Current.IsInGameArea(pOScreenPos.x, pOScreenPos.y);
    }

    /// <summary>For UWItemDrag (original: drop vs. throw on release in the
    /// 3D viewport) - 0 at the bottom, 1 at the top edge of the viewport. Only meaningful
    /// if IsScreenPositionInGameArea was already true for the same position.</summary>
    public float GetVerticalFractionInGameArea(Vector2 pOScreenPos)
    {
        return Current.VerticalFractionInGameArea(pOScreenPos.y);
    }

    /// <summary>0 at the left, 1 at the right edge of the viewport. Counterpart to
    /// GetVerticalFractionInGameArea - needed for the target direction of
    /// spell projectiles, which the original computes from the horizontal pointer position.</summary>
    public float GetHorizontalFractionInGameArea(Vector2 pOScreenPos)
    {
        return Current.HorizontalFractionInGameArea(pOScreenPos.x);
    }

    /// <summary>The classic message scroll's top edge on the screen (pixels, bottom-left origin) -
    /// the gamepad's letter grid stays above it, where the input line rolls (UWLetterGrid).</summary>
    public float MessageLogTopScreenY => GetUiPictureScreenRect(new Vector2(0f, UWHudMessageLog.messageLogTop), Vector2.zero).y;
}
