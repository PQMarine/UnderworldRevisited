using UnityEngine;
using UnityEngine.UI;
using UWDataImport.Presentation;
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
/// of the modern HUD (UWModernUi.PixelScale) and the element's own size, placed by
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
///
/// PER GAME (UW2 plan stage U6 step 1, 2026-11-23; UWCompassGeometry): the second game's disc is the middle recess of
/// its marble ledge, the compass laid on it whole, no movement arrows; the power gem lies in the compass's middle as on
/// that game's frame - this element draws it there (the live picture, IUWModernArt.PowerGem) while it is not apart
/// (UWModernLayout.IsGemInCompass), and the vitals leave it out.
/// </summary>
public class UWModernCompass : IUWModernPart
{
    private RawImage mOImage;

    /// <summary>The pictures by step, back and look, each built for the colours (UWArtCache).</summary>
    private readonly UWArtCache<int> mOSteps = new UWArtCache<int>();

    /// <summary>The room around the cross when it has a back.</summary>
    private const int Pad = 5;

    /// <summary>The stone disc without the cross (IsCompassDisc), built once per colour help and
    /// outline.</summary>
    private readonly UWVersionedTexture mODisc = new UWVersionedTexture();

    /// <summary>The room around the picture the current one has (Pad with a back, else 0).</summary>
    private int miShownPad;

    private Texture2D mOShown;

    /// <summary>The power gem in the compass (the second game), over the compass.</summary>
    private RawImage mOGem;

    /// <summary>Where it was drawn on the screen (bottom-left origin); empty while hidden.</summary>
    public Rect ScreenRect { get; private set; }

    public UWModernCompass()
    {
    }

    public void Build(Transform pORoot)
    {
        mOImage = UWUiKit.CreateRawImage(pORoot, "Compass", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        UWPixelArtUI.Apply(mOImage);
        mOGem = UWUiKit.CreateRawImage(pORoot, "Power gem in the compass", new Vector2(0f, 0f), new Vector2(0f, 0f));
        UWPixelArtUI.Apply(mOGem);
        mOGem.enabled = false;
    }

    /// <summary>Once a frame while the modern HUD shows; pfPixelScale is UWModernUi.PixelScale.</summary>
    public void Update(float pfPixelScale)
    {
        ScreenRect = Rect.zero;

        if (mOImage == null)
            return;

        if (!UWModernLayout.IsCompassShown || UWModernUi.Session == null || UWModernUi.Data == null)
        {
            mOImage.enabled = false;
            mOGem.enabled = false;
            return;
        }

        int liStep = UWModernUi.Session.World.CompassStep;
        UWModernBacks.Back lOBack = UWModernBacks.Get(UWModernLayout.ElementEnum.Compass);
        bool lbDisc = UWModernLayout.IsCompassDisc;
        bool lbWhole = UWModernClassicFrame.IsActive;
        int liKey = (((((lOBack.Key * 100) + liStep) * 4) + (lbDisc ? 1 : 0) + (lbDisc && UWModernClassicPlus.CompassOutline ? 2 : 0)) * 2)
            + (lbWhole ? 1 : 0);

        // The colour help changes the pictures (UWColourVision): built anew then.
        UWArtStamp lOStamp = UWVersionedTexture.Colours();

        if (!mOSteps.TryGet(liKey, lOStamp, out Texture2D lOTexture))
            lOTexture = mOSteps.Set(liKey, lOStamp, fBuild(liStep, lOBack, lbDisc));

        if (lOTexture == null)
        {
            mOImage.enabled = false;
            mOGem.enabled = false;
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
        mOShown = lOTexture;
        miShownPad = lOBack.Shape == UWBackdropArt.ShapeEnum.None ? 0 : Pad;

        fPlaceGem(lOPlaced, lfScale, lbDisc);
    }

    /// <summary>The power gem at its place in the compass (UWCompassGeometry.GemOnCross; on the disc moved by where the
    /// compass lies on it), the back's room in, at the compass's scale - while it lies in the compass.</summary>
    private void fPlaceGem(Rect pOPlaced, float pfScale, bool pbDisc)
    {
        Texture2D lOGem = UWModernLayout.IsGemInCompass ? UWModernUi.Session.Art.PowerGem : null;

        mOGem.enabled = lOGem != null;

        if (lOGem == null)
            return;

        UWTextures lOTextures = UWModernUi.Data.Textures;
        UWCompassGeometry lOAt = UWCompassGeometry.Of(lOTextures);
        int liDiscX = 0;
        int liDiscY = 0;

        if (pbDisc)
            lOAt.CrossOnDisc(lOTextures, out liDiscX, out liDiscY);

        lOAt.GemOnCross(lOTextures, out int liGemX, out int liGemY);

        float lfAspect = pfScale * UWModernHudArt.PixelAspectX;
        float lfX = pOPlaced.x + ((miShownPad + liDiscX + liGemX) * lfAspect);
        float lfTop = pOPlaced.yMax - ((miShownPad + liDiscY + liGemY) * pfScale);

        mOGem.texture = lOGem;

        RectTransform lORect = (RectTransform)mOGem.transform;

        lORect.anchoredPosition = new Vector2(Mathf.Round(lfX), Mathf.Round(lfTop - (lOGem.height * pfScale)));
        lORect.sizeDelta = new Vector2(lOGem.width * lfAspect, lOGem.height * pfScale);
    }

    /// <summary>
    /// The movement arrow on the stone disc under a screen point, -1 for none or without the
    /// disc - the original's click areas (UWHudArt.CompassDiscArrowAt), so the arrows work as on
    /// the classic frame (UWModernHudInput.fUpdateMoveArrowClicks).
    /// </summary>
    public int ArrowAt(Vector2 pOPointer)
    {
        if (!UWModernLayout.IsCompassDisc || mOShown == null || ScreenRect.width <= 0f || !ScreenRect.Contains(pOPointer)
            || !UWCompassGeometry.Of(UWModernUi.Data.Textures).HasArrows)
            return -1;

        float lfX = (pOPointer.x - ScreenRect.x) / ScreenRect.width * mOShown.width;
        float lfY = (ScreenRect.yMax - pOPointer.y) / ScreenRect.height * mOShown.height;

        return UWHudArt.CompassDiscArrowAt(Mathf.FloorToInt(lfX) - miShownPad, Mathf.FloorToInt(lfY) - miShownPad);
    }

    /// <summary>The freed cross - on the stone disc with the arrows (pbDisc), the cross's box
    /// lying at its place on the pedestal (MAIN.BYT 112/131) - and on its generated back when it
    /// has one.</summary>
    private Texture2D fBuild(int piStep, UWModernBacks.Back pOBack, bool pbDisc)
    {
        // CLASSIC WIDE: the original's disc and needle pictures whole, as the classic scheme lays
        // them over the frame - they cover the needle baked into it (per user's screenshots,
        // 2026-10-10: the freed cross left the baked one showing); no back.
        if (UWModernClassicFrame.IsActive)
            return UWModernUi.Session.Art.Compass(piStep, false);

        // On the second game's disc the compass whole: its face covers the cross baked into the recess.
        Texture2D lOCross = UWModernUi.Session.Art.Compass(piStep, !pbDisc || UWCompassGeometry.Of(UWModernUi.Data.Textures).IsFreedOnDisc);

        if (lOCross != null && pbDisc)
            lOCross = fOnDisc(lOCross);

        if (lOCross == null || pOBack.Shape == UWBackdropArt.ShapeEnum.None)
            return lOCross;

        int liWidth = lOCross.width + (2 * Pad);
        int liHeight = lOCross.height + (2 * Pad);
        Texture2D lOTexture = UWModernHudArt.BuildBackdrop(UWModernUi.Data.Textures, liWidth, liHeight, pOBack, UWModernUi.Filter);
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

    /// <summary>The cross laid on the stone disc; the cross's texture goes.</summary>
    private Texture2D fOnDisc(Texture2D pOCross)
    {
        bool lbOutline = UWModernClassicPlus.CompassOutline;
        UWArtStamp lODiscStamp = UWVersionedTexture.Colours(lbOutline ? 1 : 0);

        if (!mODisc.IsCurrent(lODiscStamp))
            mODisc.Set(lODiscStamp, UWModernHudArt.BuildCompassDisc(UWModernUi.Data.Textures, lbOutline, UWModernUi.Filter));

        Texture2D lODisc = mODisc.Texture;
        int liWidth = lODisc.width;
        int liHeight = lODisc.height;
        UWCompassGeometry.Of(UWModernUi.Data.Textures).CrossOnDisc(UWModernUi.Data.Textures, out int liLeft, out int liTop);
        Color32[] lOPixels = lODisc.GetPixels32();
        Color32[] lOCrossPixels = pOCross.GetPixels32();

        // Rows bottom up in both: the cross's top row lies liTop rows under the disc's top.
        for (int liY = 0; liY < pOCross.height; liY++)
        {
            int liRow = liHeight - liTop - pOCross.height + liY;

            for (int liX = 0; liX < pOCross.width; liX++)
            {
                Color32 lOPixel = lOCrossPixels[(liY * pOCross.width) + liX];
                int liColumn = liLeft + liX;

                if (lOPixel.a > 0 && liRow >= 0 && liRow < liHeight && liColumn >= 0 && liColumn < liWidth)
                    lOPixels[(liRow * liWidth) + liColumn] = lOPixel;
            }
        }

        Texture2D lOTexture = new Texture2D(liWidth, liHeight, TextureFormat.RGBA32, false);
        lOTexture.name = "UWModernCompass disc";
        lOTexture.filterMode = UWModernUi.Filter;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(lOPixels);
        lOTexture.Apply(false, false);
        Object.Destroy(pOCross);

        return lOTexture;
    }

    public void Hide()
    {
        ScreenRect = Rect.zero;

        if (mOImage != null)
            mOImage.enabled = false;

        if (mOGem != null)
            mOGem.enabled = false;
    }
}
