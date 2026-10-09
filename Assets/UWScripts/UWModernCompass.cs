using UnityEngine;
using UnityEngine.UI;
using UWDataImport.UWData;

/// <summary>
/// THE ORIGINAL'S COMPASS IN THE MODERN INTERFACE (per user, 2026-10-09: the first of the
/// original's pieces freed as elements, from a Reddit wish; its own element, placed freely).
///
/// The classic frame's compass (UWHudCompass) is a disc of four background pictures and sixteen
/// needles from COMPASS.GR, each at its own measured place. Here disc and needle of a step are
/// composed into ONE picture per step, over the box all of them take together, so the element
/// keeps one size whatever the heading; the step is the classic compass's own
/// (UWHudCompass.CurrentStep), frozen in the void as there. Scaled by whole pixels like the rest
/// of the modern HUD (UWModernHud.PixelScale) and the element's own size, placed by
/// UWModernLayout - by default top centre under the heading strip. A click reports the status,
/// as the classic compass and the heading do.
///
/// Shown under Customize when switched on in the layout editor (UWModernLayout.IsCompassShown);
/// the heading's text then gives way to it.
///
/// ITS BACK (per user, the same day, after the cut ones were dropped): a GENERATED one
/// (UWModernBacks, UWBackdropArt) - none by default, else an oval or a rectangle in leather,
/// marble, stone or bronze with clouds or one of three wall textures and an optional border, the room of Pad around
/// the cross.
///
/// ALWAYS FREED (per user, 2026-10-09): the cross without the disc's stone
/// (UWHudCompass.BuildComposite pbFreed, UWHudArt.CompassCrossMask). Backs were tried the same
/// day - an oval or rectangle of the frame's slate with a border, the original disc - and taken
/// out again: the original's compass lay embedded in the frame, and lifted out of it every back
/// looked stuck on; the free cross looked best.
/// </summary>
public class UWModernCompass
{
    private readonly UWGameUI mOUi;

    private RawImage mOImage;

    private readonly System.Collections.Generic.Dictionary<int, Texture2D> mOSteps = new System.Collections.Generic.Dictionary<int, Texture2D>();

    /// <summary>The room around the cross when it has a back.</summary>
    private const int Pad = 5;

    private int miArtVersion = -1;

    /// <summary>Where it was drawn on the screen (bottom-left origin); empty while hidden.</summary>
    public Rect ScreenRect { get; private set; }

    public UWModernCompass(UWGameUI pOUi)
    {
        mOUi = pOUi;
    }

    public void Build(Transform pORoot)
    {
        GameObject lOObject = new GameObject("Compass", typeof(RectTransform), typeof(RawImage));
        lOObject.transform.SetParent(pORoot, false);

        RectTransform lORect = (RectTransform)lOObject.transform;
        lORect.anchorMin = new Vector2(0.5f, 1f);
        lORect.anchorMax = new Vector2(0.5f, 1f);
        lORect.pivot = new Vector2(0.5f, 1f);

        mOImage = lOObject.GetComponent<RawImage>();
        mOImage.raycastTarget = false;
        mOImage.enabled = false;
        UWPixelArtUI.Apply(mOImage);
    }

    /// <summary>Once a frame while the modern HUD shows; pfPixelScale is UWModernHud.PixelScale.</summary>
    public void Update(float pfPixelScale)
    {
        ScreenRect = Rect.zero;

        if (mOImage == null)
            return;

        if (!UWModernLayout.IsCompassShown || mOUi == null || mOUi.mOUWData == null)
        {
            mOImage.enabled = false;
            return;
        }

        // The colour help changes the pictures (UWColourVision): built anew then.
        if (miArtVersion != UWColourVision.Version)
        {
            foreach (Texture2D lOOld in mOSteps.Values)
            {
                if (lOOld != null)
                    Object.Destroy(lOOld);
            }

            mOSteps.Clear();

            miArtVersion = UWColourVision.Version;
        }

        int liStep = mOUi.Compass.CurrentStep;
        UWModernBacks.Back lOBack = UWModernBacks.Get(UWModernLayout.ElementEnum.Compass);
        int liKey = (lOBack.Key * 100) + liStep;

        if (!mOSteps.TryGetValue(liKey, out Texture2D lOTexture) || lOTexture == null)
        {
            lOTexture = fBuild(liStep, lOBack);
            mOSteps[liKey] = lOTexture;
        }

        if (lOTexture == null)
        {
            mOImage.enabled = false;
            return;
        }

        float lfScale = pfPixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.Compass);
        float lfWidth = lOTexture.width * lfScale * UWModernHudArt.PixelAspectX;
        float lfHeight = lOTexture.height * lfScale;
        float lfTop = (4f + UWModernHudArt.StripHeight + 2f) * pfPixelScale;

        Rect lOPlaced = UWModernLayout.Place(UWModernLayout.ElementEnum.Compass,
            new Rect((Screen.width - lfWidth) * 0.5f, Screen.height - lfTop - lfHeight, lfWidth, lfHeight));

        UWModernLayout.Report(UWModernLayout.ElementEnum.Compass, lOPlaced);

        mOImage.texture = lOTexture;
        mOImage.enabled = true;
        ((RectTransform)mOImage.transform).anchoredPosition = new Vector2(Mathf.Round(lOPlaced.center.x - (Screen.width * 0.5f)),
            -(Screen.height - lOPlaced.yMax));
        ((RectTransform)mOImage.transform).sizeDelta = new Vector2(lfWidth, lfHeight);

        ScreenRect = lOPlaced;
    }

    /// <summary>The freed cross, on its generated back when it has one.</summary>
    private Texture2D fBuild(int piStep, UWModernBacks.Back pOBack)
    {
        Texture2D lOCross = mOUi.Compass.BuildComposite(piStep, mOUi.TextureFilterMode, true);

        if (lOCross == null || pOBack.Shape == UWBackdropArt.ShapeEnum.None)
            return lOCross;

        int liWidth = lOCross.width + (2 * Pad);
        int liHeight = lOCross.height + (2 * Pad);
        Texture2D lOTexture = UWModernHudArt.BuildBackdrop(mOUi.mOUWData.Textures, liWidth, liHeight, pOBack, mOUi.TextureFilterMode);
        Color32[] lOPixels = lOTexture.GetPixels32();
        Color32[] lOCrossPixels = lOCross.GetPixels32();

        for (int liY = 0; liY < lOCross.height; liY++)
        {
            for (int liX = 0; liX < lOCross.width; liX++)
            {
                Color32 lOPixel = lOCrossPixels[(liY * lOCross.width) + liX];

                if (lOPixel.a > 0)
                    lOPixels[((liY + Pad) * liWidth) + liX + Pad] = lOPixel;
            }
        }

        lOTexture.SetPixels32(lOPixels);
        lOTexture.Apply(false, false);
        lOTexture.name = "UWModernCompass back " + pOBack.Key;
        Object.Destroy(lOCross);

        return lOTexture;
    }

    public void Hide()
    {
        ScreenRect = Rect.zero;

        if (mOImage != null)
            mOImage.enabled = false;
    }
}
