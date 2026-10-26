using UnityEngine;
using UnityEngine.UI;
using UWDataImport;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE RUNE HOLLOW AS AN ELEMENT (Classic+, per user 2026-10-10: "the hollow with the
/// prepared runes"): the frame's recess (UWHudArt.BuildRuneHollow) with the prepared runes at
/// the frame's places in it, placed by UWModernLayout - by default right of the compass, under
/// the heading without it. A click casts as on the frame (UWModernHudInput.fUpdateRuneHollowClicks).
/// Out of UWModernHud since cleanup 5.16 (2026-10-22); ScreenRect is where it was drawn.
/// </summary>
public class UWModernRuneHollow : UWModernPart
{
    /// <summary>The hollow with the prepared runes (UWModernLayout.IsRuneHollowShown): its picture,
    /// the three runes in it, the rune icons (palette indices, UWIconPalette), where it was drawn.</summary>
    private RawImage mORuneHollow;

    private readonly UWVersionedTexture mORuneHollowArt = new UWVersionedTexture();

    private readonly RawImage[] mORuneHollowRunes = new RawImage[UWRuneShelfModel.Capacity];

    /// <summary>The compass and the heading it stands beside or under by default.</summary>
    private readonly UWModernCompass mOCompass;

    private readonly UWModernHeadingStrip mOHeading;

    public UWModernRuneHollow(UWModernHud pOHud, UWModernCompass pOCompass, UWModernHeadingStrip pOHeading) : base(pOHud)
    {
        mOCompass = pOCompass;
        mOHeading = pOHeading;
    }

    public override void Build(Transform pORoot)
    {
        mORuneHollow = UWUiKit.CreateRawImage(pORoot, "Rune hollow", new Vector2(0f, 0f), new Vector2(0f, 0f));
        UWPixelArtUI.Apply(mORuneHollow);

        for (int liAt = 0; liAt < mORuneHollowRunes.Length; liAt++)
        {
            mORuneHollowRunes[liAt] = UWUiKit.CreateRawImage(pORoot, "Prepared rune", new Vector2(0f, 0f), new Vector2(0f, 0f));
            UWIconPalette.Apply(mORuneHollowRunes[liAt]);
        }
    }

    protected override void fUpdate()
    {
        ScreenRect = Rect.zero;

        if (!UWModernLayout.IsRuneHollowShown)
        {
            mORuneHollow.enabled = false;

            foreach (RawImage lORune in mORuneHollowRunes)
                lORune.enabled = false;

            return;
        }

        bool lbOutline = UWModernClassicPlus.RuneHollowOutline;

        UWArtStamp lOHollowStamp = UWVersionedTexture.Colours(lbOutline ? 1 : 0);

        if (!mORuneHollowArt.IsCurrent(lOHollowStamp))
            mORuneHollowArt.Set(lOHollowStamp, UWModernHudArt.BuildRuneHollow(mOUi.mOUWData.Textures, lbOutline, mOUi.TextureFilterMode));

        Texture2D lOHollow = mORuneHollowArt.Texture;

        // The outline puts the hollow a pixel in.
        int liPad = lbOutline ? 1 : 0;

        float lfScale = fScaleOf(UWModernLayout.ElementEnum.RuneHollow);
        float lfAspect = lfScale * UWModernHudArt.PixelAspectX;
        float lfWidth = lOHollow.width * lfAspect;
        float lfHeight = lOHollow.height * lfScale;
        bool lbCompass = mOCompass != null && mOCompass.ScreenRect.width > 0f;
        Rect lOHeadingRect = mOHeading.ScreenRect;
        Rect lODefault = lbCompass
            ? new Rect(mOCompass.ScreenRect.xMax + (2f * miPixelScale), mOCompass.ScreenRect.center.y - (lfHeight * 0.5f), lfWidth, lfHeight)
            : new Rect(lOHeadingRect.center.x - (lfWidth * 0.5f), lOHeadingRect.y - (2f * miPixelScale) - lfHeight, lfWidth, lfHeight);
        Rect lOPlaced = fPlaceAndReport(UWModernLayout.ElementEnum.RuneHollow, lODefault);

        mORuneHollow.texture = lOHollow;
        mORuneHollow.enabled = true;
        ((RectTransform)mORuneHollow.transform).anchoredPosition = lOPlaced.position;
        ((RectTransform)mORuneHollow.transform).sizeDelta = lOPlaced.size;
        ScreenRect = lOPlaced;

        System.Collections.Generic.IReadOnlyList<int> lOShelf = mOUi.Runes != null ? mOUi.Runes.Shelf.Selected : null;

        for (int liAt = 0; liAt < mORuneHollowRunes.Length; liAt++)
        {
            RawImage lOImage = mORuneHollowRunes[liAt];
            int liRune = lOShelf != null && liAt < lOShelf.Count ? lOShelf[liAt] : -1;
            Texture2D lOIcon = fRuneIcon(liRune);

            lOImage.enabled = lOIcon != null;

            if (lOIcon == null)
                continue;

            float lfIconHeight = lOIcon.height * lfScale;

            lOImage.texture = lOIcon;
            ((RectTransform)lOImage.transform).anchoredPosition = new Vector2(
                lOPlaced.x + ((liPad + UWHudArt.RuneHollowRuneX + (liAt * UWHudArt.RuneHollowPitch)) * lfAspect),
                lOPlaced.yMax - ((liPad + UWHudArt.RuneHollowRuneY) * lfScale) - lfIconHeight);
            ((RectTransform)lOImage.transform).sizeDelta = new Vector2(lOIcon.width * lfAspect, lfIconHeight);
        }
    }

    /// <summary>A rune's icon (OBJECTS.GR from the first rune on), shared (UWIconCache).</summary>
    private Texture2D fRuneIcon(int piRune)
    {
        if (piRune < 0 || piRune >= UWPlayerData.RuneCount)
            return null;

        return UWIconCache.Texture(mOUi.mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, UWPlayerData.FirstRuneObjectId + piRune),
            mOUi.TextureFilterMode);
    }
}
