using UnityEngine;
using UnityEngine.UI;
using UWDataImport.Presentation;
using UWDataImport.UWData;

/// <summary>
/// THE STONE SHELF, A DECO PANEL (per user, 2026-10-09: "the stone shelf freely placed and
/// sized, without a function - one can put the power gem or the flasks on it"): the original's
/// ledge at the width chosen in the inspector (UWUserSettings.ModernStoneShelfWidth), placed and
/// sized like any part, drawn under all of them; by default bottom left above the shelf. Out of
/// UWModernHud since cleanup 5.16 (2026-10-22).
/// </summary>
public class UWModernStoneShelf : UWModernPart
{
    /// <summary>The stone shelf deco panel (UWModernLayout.IsStoneShelfShown), its picture and the
    /// width it was built at.</summary>
    private RawImage mOStoneShelf;

    private readonly UWVersionedTexture mOStoneShelfTexture = new UWVersionedTexture();

    /// <summary>The stone shelf's default width in its own pixels: as wide as the flasks' shelf.</summary>
    public const int StoneShelfDefaultWidth = 81;

    /// <summary>The stone shelf's width as set, in its own pixels.</summary>
    public static int StoneShelfWidth => UWModernClassicPlus.IsActive ? UWModernClassicPlus.StoneShelfWidth
        : UWUserSettings.ModernStoneShelfWidth > 0 ? UWUserSettings.ModernStoneShelfWidth : StoneShelfDefaultWidth;

    /// <summary>The widest stone shelf: the whole screen at its size (per user, 2026-10-10: "one
    /// should be able to pull the shelf across the whole screen"; before, 320).</summary>
    public static int StoneShelfMaxWidth => Mathf.Max(UWDataImport.Presentation.UWHudArt.StoneShelfMinWidth,
        Mathf.CeilToInt(Screen.width / Mathf.Max(0.01f, UWModernUi.PixelScale * UWModernLayout.Scale(UWModernLayout.ElementEnum.StoneShelf) * UWModernHudArt.PixelAspectX)));

    public UWModernStoneShelf(UWModernHud pOHud) : base(pOHud)
    {
    }

    public override void Build(Transform pORoot)
    {
        // The stone shelf deco panel, under everything else, so things can stand on it.
        mOStoneShelf = UWUiKit.CreateRawImage(pORoot, "Stone shelf", new Vector2(0f, 0f), new Vector2(0f, 0f));
        UWPixelArtUI.Apply(mOStoneShelf);
    }

    protected override void fUpdate()
    {
        if (!UWModernLayout.IsStoneShelfShown)
        {
            mOStoneShelf.enabled = false;
            return;
        }

        int liWidth = Mathf.Clamp(StoneShelfWidth, UWDataImport.Presentation.UWHudArt.StoneShelfMinWidth, StoneShelfMaxWidth);
        UWArtStamp lOStamp = UWVersionedTexture.Colours(liWidth);

        if (!mOStoneShelfTexture.IsCurrent(lOStamp))
            mOStoneShelfTexture.Set(lOStamp, UWModernHudArt.BuildStoneShelf(mOUi.mOUWData.Textures, liWidth, mOUi.TextureFilterMode));

        Texture2D lOStoneShelf = mOStoneShelfTexture.Texture;
        float liScale = fScaleOf(UWModernLayout.ElementEnum.StoneShelf);
        float lfWidth = lOStoneShelf.width * liScale * UWModernHudArt.PixelAspectX;
        float lfHeight = lOStoneShelf.height * liScale;
        Rect lOPlaced = fPlaceAndReport(UWModernLayout.ElementEnum.StoneShelf,
            new Rect(UWModernVitals.ShelfMargin * miPixelScale, Screen.height * 0.35f, lfWidth, lfHeight));

        mOStoneShelf.texture = lOStoneShelf;
        mOStoneShelf.enabled = true;
        ((RectTransform)mOStoneShelf.transform).anchoredPosition = lOPlaced.position;
        ((RectTransform)mOStoneShelf.transform).sizeDelta = lOPlaced.size;
    }
}
