using UnityEngine;
using UnityEngine.UI;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE VIEW'S PICTURES (per user, 2026-10-04): a window's look into the depths, a gravestone,
/// a picture scroll - the original shows them in its view window, which the modern scheme
/// hides; here they lie in a leather frame in the middle of the screen, at the UI's size, over
/// everything. Any key or click hides them, as in the original (Interaction). Out of UWModernHud
/// since cleanup 5.16 (2026-10-22): the picture comes from UWGameUI.CurrentWindowPicture, its
/// canvas is made on the HUD's the first time one shows.
/// </summary>
public class UWModernViewPicture : UWModernPart
{
    private Canvas mOPictureCanvas;

    private RawImage mOPictureFrame;

    private Image mOPicture;

    private readonly UWVersionedTexture mOPictureFrameTexture = new UWVersionedTexture();

    private const int PictureBorder = 3;

    /// <summary>The HUD's canvas, the picture canvas's parent.</summary>
    private Transform mORoot;

    public UWModernViewPicture(UWModernHud pOHud) : base(pOHud)
    {
    }

    /// <summary>Nothing yet: the picture's canvas is made when the first picture shows.</summary>
    public override void Build(Transform pORoot)
    {
        mORoot = pORoot;
    }

    protected override void fUpdate()
    {
        Sprite lOSprite = mOUi != null ? mOUi.Pictures.CurrentWindowPicture : null;

        if (lOSprite == null)
        {
            if (mOPictureCanvas != null)
                mOPictureCanvas.enabled = false;

            return;
        }

        if (mOPictureCanvas == null)
        {
            mOPictureCanvas = UWUiKit.CreateLayerCanvas(mORoot, "View picture", UWUiSorting.ModernViewPicture);
            UWUiKit.Stretch((RectTransform)mOPictureCanvas.transform);

            mOPictureFrame = UWUiKit.CreateRawImage(mOPictureCanvas.transform, "Leather", Vector2.zero, Vector2.zero);
            UWPixelArtUI.Apply(mOPictureFrame);
            mOPictureFrame.enabled = true;
            mOPicture = UWUiKit.CreateImage(mOPictureCanvas.transform, "Picture", Vector2.zero, Vector2.zero, Color.white);
        }

        mOPictureCanvas.enabled = true;

        float liScale = miPixelScale;
        Vector2 lOSize = lOSprite.rect.size;
        int liFrameWidth = Mathf.RoundToInt(lOSize.x * UWModernHudArt.PixelAspectX) + UWModernHudArt.LeatherLeft + UWModernHudArt.LeatherRight + (2 * PictureBorder);
        int liFrameHeight = Mathf.RoundToInt(lOSize.y) + UWModernHudArt.LeatherTop + UWModernHudArt.LeatherBottom + (2 * PictureBorder);

        UWArtStamp lOFrameStamp = UWVersionedTexture.Colours(liFrameWidth, liFrameHeight);

        if (!mOPictureFrameTexture.IsCurrent(lOFrameStamp))
        {
            mOPictureFrame.texture = mOPictureFrameTexture.Set(lOFrameStamp,
                UWModernHudArt.BuildLeather(mOUi.mOUWData.Textures, liFrameWidth, liFrameHeight, mOUi.TextureFilterMode));
        }

        float lfWidth = liFrameWidth * liScale;
        float lfHeight = liFrameHeight * liScale;
        float lfX = Mathf.Round((Screen.width - lfWidth) * 0.5f);
        float lfY = Mathf.Round((Screen.height * 0.55f) - (lfHeight * 0.5f));
        RectTransform lOFrame = (RectTransform)mOPictureFrame.transform;

        lOFrame.anchoredPosition = new Vector2(lfX, lfY);
        lOFrame.sizeDelta = new Vector2(lfWidth, lfHeight);

        mOPicture.sprite = lOSprite;

        RectTransform lOPicture = (RectTransform)mOPicture.transform;

        lOPicture.anchoredPosition = new Vector2(lfX + ((UWModernHudArt.LeatherLeft + PictureBorder) * liScale),
            lfY + ((UWModernHudArt.LeatherBottom + PictureBorder) * liScale));
        lOPicture.sizeDelta = new Vector2(lOSize.x * UWModernHudArt.PixelAspectX, lOSize.y) * liScale;
    }
}
