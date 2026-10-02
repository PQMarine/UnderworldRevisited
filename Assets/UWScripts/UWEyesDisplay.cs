using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.UWData;
using UnderworldRevisited;

/// <summary>
/// The eyes at the top edge of the screen. After a hit they show the condition of the
/// enemy that was hit: green healthy, yellow wounded, red critical.
///
/// EYES.GR contains ten images of 20x3 pixels. By their average colour they split into
/// three groups, and these match the three conditions exactly:
///
///   Image 0-3  green/red ratio 0.64 to 1.14   green
///   Image 4-6  0.58 to 0.61                   yellow
///   Image 7-9  0.45 to 0.34                   red
///
/// Within a group the green share rises - so the images are a small
/// movement, not a colour gradient. This fits the observation in the original (user, 2026-08-30):
/// if the colour stays the same, the image stands still; if it changes, the change is shown as
/// an animation. Therefore on a colour change the frame sequence of the new group
/// plays through once.
///
/// Also observed: the eyes fade in on a hit, stay for about four seconds and
/// fade out again; a new hit resets the timer.
///
/// Like the conversation display, the display builds itself at runtime, so that no
/// change to the UI prefab is needed for it.
/// </summary>
public class UWEyesDisplay : MonoBehaviour
{
    private const int ScreenWidth = 320;

    private const int ScreenHeight = 200;

    private const int FrameCount = 10;

    /// <summary>First and last image of the three groups.</summary>
    private static readonly int[] msGroupFirst = new int[] { 0, 4, 7 };

    private static readonly int[] msGroupLast = new int[] { 3, 6, 9 };

    /// <summary>From which share of health which group applies. Invented - the docs
    /// mention neither thresholds nor this display at all.</summary>
    private const float HealthyThreshold = 2f / 3f;

    private const float WoundedThreshold = 1f / 3f;

    private DataImport mOData;
    private Canvas mOCanvas;
    private RawImage mOImage;

    private readonly Texture2D[] mOFrames = new Texture2D[FrameCount];

    private int miGroup = -1;
    private int miFrame = -1;
    private float mfNextFrameTime;
    private float mfHideAt;
    private float mfAlpha;

    /// <summary>Shows the condition of the enemy that was hit.</summary>
    public void ShowCondition(DataImport pOData, float pfHealthFraction)
    {
        mOData = pOData;

        int liGroup = pfHealthFraction >= HealthyThreshold
            ? 0
            : (pfHealthFraction >= WoundedThreshold ? 1 : 2);

        fEnsureCanvas();

        UWSettings lOSettings = UWSettings.Instance;

        // Colour change: play the frame sequence of the new group from the start. Same colour:
        // leave it where it is.
        if (liGroup != miGroup)
        {
            miGroup = liGroup;
            miFrame = msGroupFirst[liGroup];
            mfNextFrameTime = 0f;
        }
        else if (miFrame < 0)
        {
            miFrame = msGroupLast[liGroup];
        }

        fPlace();

        mfHideAt = Time.time + (lOSettings != null ? lOSettings.EyesSeconds : 4f);

        fShowFrame();
    }

    /// <summary>
    /// Puts the eyes over the frame's top edge. Every frame while they show: the frame moves
    /// under them - with the help window open it slides left and squeezes to 4:3
    /// (UWHelpLayout), and until 2026-09-26 the eyes stayed in the middle then (per user).
    /// </summary>
    private void fPlace()
    {
        UWSettings lOSettings = UWSettings.Instance;
        Vector2 lOEyesAt = lOSettings != null ? lOSettings.EyesPosition : new Vector2(0f, -8f);

        // With DisplayAs4By3 the picture is squeezed sideways, the offset from the centre as well
        // (the image itself through UWPixelAspectFrame).
        lOEyesAt.x *= UWGameUI.HorizontalPixelFactor;
        lOEyesAt.x += UWHelpLayout.FrameShift(UWUiFit.CanvasWidth);

        ((RectTransform)mOImage.transform).anchoredPosition = lOEyesAt + new Vector2(0f, ScreenHeight / 2f);
    }

    private void Update()
    {
        if (mOImage == null || miFrame < 0)
            return;

        fPlace();

        UWSettings lOSettings = UWSettings.Instance;
        float lfFade = lOSettings != null ? lOSettings.EyesFadeSeconds : 0.3f;

        // Advance the frame sequence until the last image of the group is reached.
        if (miGroup >= 0 && miFrame < msGroupLast[miGroup] && Time.time >= mfNextFrameTime)
        {
            float lfRate = lOSettings != null ? lOSettings.EyesFramesPerSecond : 8f;

            mfNextFrameTime = Time.time + (lfRate <= 0f ? 1f : 1f / lfRate);

            miFrame++;

            fShowFrame();
        }

        // Fade in and out.
        float lfTarget = Time.time < mfHideAt ? 1f : 0f;

        if (!Mathf.Approximately(mfAlpha, lfTarget))
        {
            float lfStep = lfFade <= 0f ? 1f : (Time.deltaTime / lfFade);

            mfAlpha = Mathf.MoveTowards(mfAlpha, lfTarget, lfStep);

            fApplyAlpha();
        }
    }

    private void fShowFrame()
    {
        Texture2D lOTexture = fGetFrame(miFrame);

        if (lOTexture == null)
            return;

        mOImage.texture = lOTexture;
        ((RectTransform)mOImage.transform).sizeDelta = new Vector2(lOTexture.width, lOTexture.height);
        mOImage.enabled = true;

        fApplyAlpha();
    }

    private void fApplyAlpha()
    {
        Color lOColour = mOImage.color;
        lOColour.a = mfAlpha;

        mOImage.color = lOColour;
        mOImage.enabled = mfAlpha > 0f;
    }

    private Texture2D fGetFrame(int piFrame)
    {
        if (piFrame < 0 || piFrame >= mOFrames.Length || mOData == null)
            return null;

        if (mOFrames[piFrame] != null)
            return mOFrames[piFrame];

        UWTexture lOSource;

        try
        {
            lOSource = mOData.Textures.GetTextureByType(UWTexture.TextureTypes.EYES, piFrame);
        }
        catch
        {
            return null;
        }

        if (lOSource == null || lOSource.Width <= 0 || lOSource.Height <= 0)
            return null;

        Texture2D lOTexture = new Texture2D(lOSource.Width, lOSource.Height, TextureFormat.RGBA32, false);
        lOTexture.name = "UWEyesDisplay.cs:174";
        lOTexture.filterMode = FilterMode.Point;
        lOTexture.wrapMode = TextureWrapMode.Clamp;

        UWColor32[] lOColours = lOSource.GetUWColor32();
        byte[] lyIndices = lOSource.PaletteIndices;
        Color32[] lOPixels = new Color32[lOSource.Width * lOSource.Height];

        for (int y = 0; y < lOSource.Height; y++)
        {
            // Row 0 of the source is at the top, Unity starts at the bottom.
            int liSourceRow = (lOSource.Height - 1 - y) * lOSource.Width;
            int liTargetRow = y * lOSource.Width;

            for (int x = 0; x < lOSource.Width; x++)
            {
                int liSource = liSourceRow + x;

                if (liSource >= lOColours.Length)
                    continue;

                bool lbTransparent = lyIndices != null && liSource < lyIndices.Length && lyIndices[liSource] == 0;

                lOPixels[liTargetRow + x] = lbTransparent
                    ? new Color32(0, 0, 0, 0)
                    : new Color32(lOColours[liSource].R, lOColours[liSource].G, lOColours[liSource].B, 255);
            }
        }

        lOTexture.SetPixels32(lOPixels);
        lOTexture.Apply(false, false);

        mOFrames[piFrame] = lOTexture;

        return lOTexture;
    }

    private void fEnsureCanvas()
    {
        if (mOCanvas != null)
            return;

        GameObject lOCanvasObject = new GameObject("UW Eyes", typeof(Canvas), typeof(CanvasScaler));
        lOCanvasObject.transform.SetParent(transform, false);

        mOCanvas = lOCanvasObject.GetComponent<Canvas>();
        mOCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Below the conversation display (90), but above the game world.
        mOCanvas.sortingOrder = 80;

        CanvasScaler lOScaler = lOCanvasObject.GetComponent<CanvasScaler>();
        lOScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        lOScaler.referenceResolution = new Vector2(ScreenWidth, ScreenHeight);
        lOScaler.matchWidthOrHeight = 1f;

        // Fitted whole into the window, bars over and under it when narrow (UWUiFit).
        lOCanvasObject.AddComponent<UWFitCanvas>();

        GameObject lOImageObject = new GameObject("Eyes", typeof(RectTransform), typeof(RawImage));
        lOImageObject.transform.SetParent(lOCanvasObject.transform, false);

        lOImageObject.AddComponent<UWPixelAspectFrame>();

        RectTransform lORect = (RectTransform)lOImageObject.transform;
        // At the centre, not at the canvas top: a window narrower than the frame has bars over
        // and under it (UWUiFit), and the eyes belong to the frame's top edge - half its height up.
        lORect.anchorMin = new Vector2(0.5f, 0.5f);
        lORect.anchorMax = new Vector2(0.5f, 0.5f);
        lORect.pivot = new Vector2(0.5f, 1f);

        mOImage = lOImageObject.GetComponent<RawImage>();
        mOImage.raycastTarget = false;
        mOImage.enabled = false;
    }
}
