using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
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
    private const int ScreenHeight = 200;

    private const int FrameCount = 10;

    /// <summary>First and last image of the three groups.</summary>
    private static readonly int[] msGroupFirst = new int[] { 0, 4, 7 };

    private static readonly int[] msGroupLast = new int[] { 3, 6, 9 };

    private DataImport mOData;
    private Canvas mOCanvas;
    private RawImage mOImage;

    /// <summary>The frames, each built for the colour help (UWArtCache) - see fGetFrame.</summary>
    private readonly UWArtCache<int> mOFrames = new UWArtCache<int>();

    private int miGroup = -1;
    private int miFrame = -1;
    private float mfNextFrameTime;
    private float mfHideAt;
    private float mfAlpha;

    // --- For the modern scheme's heading strip (UWModernHeadingStrip.fUpdateStrip), which shows the eyes
    //     in its burned gargoyle instead of this image over the classic frame.

    /// <summary>The eyes are lit, or still fading.</summary>
    public bool IsLit => miFrame >= 0 && (mfAlpha > 0f || Time.time < mfHideAt);

    public float Alpha => mfAlpha;

    /// <summary>0 healthy, 1 wounded, 2 critical.</summary>
    public int Group => miGroup;

    /// <summary>The name of the creature hit, or null.</summary>
    public string TargetName { get; private set; }

    public Texture2D CurrentFrame => miFrame >= 0 ? fGetFrame(miFrame) : null;

    /// <summary>Shows the condition of the enemy that was hit: hit points left against the
    /// creature table's average (UWCombat.GetEyesGroup, do_damage in COMBAT.C). The thresholds
    /// were called invented here until 2026-10-12; they are the original's thirds, and the
    /// measure is the table average, not the creature's own maximum.</summary>
    public void ShowCondition(DataImport pOData, int piHitPoints, int piAverageHitPoints, string psName = null)
    {
        mOData = pOData;
        TargetName = psName;

        int liGroup = UWCombat.GetEyesGroup(piHitPoints, piAverageHitPoints);

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

        // At 4:3 (UWDisplayAspect) the picture is squeezed sideways, the offset from the centre as well
        // (the image itself through UWPixelAspectFrame).
        lOEyesAt.x *= UWClassicScreen.HorizontalPixelFactor;
        lOEyesAt.x += UWHelpLayout.FrameShift(UWUiFit.CanvasWidth);

        ((RectTransform)mOImage.transform).anchoredPosition = lOEyesAt + new Vector2(0f, ScreenHeight / 2f);
    }

    private void Update()
    {
        if (mOImage == null || miFrame < 0)
            return;

        // The colour help changed: the frames are built anew (fGetFrame), the shown one too.
        if (mOImage.texture != fGetFrame(miFrame))
            fShowFrame();

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

        // The modern scheme shows them in its heading strip.
        if (UWControlScheme.Ui.Policy.EyesInHeadingStrip)
            mOImage.enabled = false;
        else if (mfAlpha > 0f && !mOImage.enabled)
            fApplyAlpha();
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
        mOImage.enabled = mfAlpha > 0f && !UWControlScheme.Ui.Policy.EyesInHeadingStrip;
    }

    /// <summary>
    /// One frame of EYES.GR, index 0 transparent (UWUploadPixels.FromIndexZeroKeyed).
    ///
    /// THE COLOUR HELP (decision 6 of the cleanup plan, B30, per user 2026-10-19): the eyes
    /// follow UWColourVision like every other picture, the frames are built anew when it changes.
    /// Until then they were drawn in the palette's colours alone, with the filter on they did not
    /// match the frame around them. With the filter off nothing changes.
    /// </summary>
    private Texture2D fGetFrame(int piFrame)
    {
        if (piFrame < 0 || piFrame >= FrameCount || mOData == null)
            return null;

        UWArtStamp lOStamp = UWVersionedTexture.Colours();

        if (mOFrames.TryGet(piFrame, lOStamp, out Texture2D lOFrame))
            return lOFrame;

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

        return mOFrames.Set(piFrame, lOStamp, UWTextureFactory.Upload(UWUploadPixels.FromIndexZeroKeyed(lOSource, UWColourVision.Filter),
            lOSource.Width, lOSource.Height, FilterMode.Point, TextureWrapMode.Clamp, "UWEyesDisplay eyes " + piFrame));
    }

    private void fEnsureCanvas()
    {
        if (mOCanvas != null)
            return;

        // Below the conversation display (90), but above the game world. Fitted whole into the
        // window, bars over and under it when narrow (UWUiFit).
        mOCanvas = UWUiKit.CreateClassicCanvas(transform, "UW Eyes", UWUiSorting.Eyes);

        // At the centre, not at the canvas top: a window narrower than the frame has bars over
        // and under it (UWUiFit), and the eyes belong to the frame's top edge - half its height up.
        mOImage = UWUiKit.CreateRawImage(mOCanvas.transform, "Eyes", UWUiKit.Centre, new Vector2(0.5f, 1f));
        mOImage.gameObject.AddComponent<UWPixelAspectFrame>();
    }
}
