using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN HUD'S FRAMES as Unity textures. The pictures themselves - the leather, the slot
/// circle, the stone shelf, the heading strip with the gargoyle, the paperdoll page, the keep
/// masks of the flasks and the gem - are composed engine-free in UWHudArt (moved there
/// 2026-10-05, per user: reusable for UW2 or another engine); here they only become textures,
/// with the colour help applied as UWGameUI.fGetTextureInvert does. Callers rebuild when
/// UWColourVision.Version changes.
/// </summary>
public static class UWModernHudArt
{
    public const int LeatherLeft = UWHudArt.LeatherLeft;

    public const int LeatherRight = UWHudArt.LeatherRight;

    public const int LeatherTop = UWHudArt.LeatherTop;

    public const int LeatherBottom = UWHudArt.LeatherBottom;

    public const int SlotSize = UWHudArt.SlotSize;

    public const int PaperdollPageWidth = UWHudArt.PaperdollPageWidth;

    public const int PaperdollShift = UWHudArt.PaperdollShift;

    public const int StripHeight = UWHudArt.StripHeight;

    public const int StripClosedWidth = UWHudArt.StripClosedWidth;

    public const int StripMinOpenHalf = UWHudArt.StripMinOpenHalf;

    public const int BrandTop = UWHudArt.BrandTop;

    public const int EyesLeftInBrand = UWHudArt.EyesLeftInBrand;

    public const int EyesTopInBrand = UWHudArt.EyesTopInBrand;

    /// <summary>The paperdoll page's place in MAIN.BYT (rows from the top).</summary>
    public static readonly RectInt PaperdollPage = new RectInt(UWHudArt.PaperdollPage.X, UWHudArt.PaperdollPage.Y,
        UWHudArt.PaperdollPage.Width, UWHudArt.PaperdollPage.Height);

    /// <summary>Where the shelf picture puts the flasks and the gem, top-left corners in its
    /// own pixels, rows counted from the top.</summary>
    public struct ShelfLayout
    {
        public Vector2Int HealthFlask;

        public Vector2Int ManaFlask;

        public Vector2Int Gem;

        public int Width;

        public int Height;
    }

    public static int PageColumnToMain(int piColumn)
    {
        return UWHudArt.PageColumnToMain(piColumn);
    }

    public static int BrandLeft(int piWidth, int piOpenHalf)
    {
        return UWHudArt.BrandLeft(piWidth, piOpenHalf);
    }

    public static Texture2D BuildLeather(UWTextures pOTextures, int piWidth, int piHeight, FilterMode peFilter, int piVariant = 0)
    {
        return fToTexture(UWHudArt.BuildLeather(pOTextures, piWidth, piHeight, piVariant), peFilter, "UWModernHudArt leather");
    }

    public static Texture2D BuildLeatherTab(UWTextures pOTextures, int piWidth, int piHeight, FilterMode peFilter)
    {
        return fToTexture(UWHudArt.BuildLeatherTab(pOTextures, piWidth, piHeight), peFilter, "UWModernHudArt tab");
    }

    public static Texture2D BuildPaperdollPage(UWTextures pOTextures, FilterMode peFilter)
    {
        return fToTexture(UWHudArt.BuildPaperdollPage(pOTextures), peFilter, "UWModernHudArt paperdoll");
    }

    public static Texture2D BuildHeadingStrip(UWTextures pOTextures, int piWidth, int piOpenHalf, FilterMode peFilter)
    {
        return fToTexture(UWHudArt.BuildHeadingStrip(pOTextures, piWidth, piOpenHalf), peFilter, "UWModernHudArt heading strip");
    }

    public static Texture2D BuildSlotCircle(UWTextures pOTextures, FilterMode peFilter)
    {
        return fToTexture(UWHudArt.BuildSlotCircle(pOTextures), peFilter, "UWModernHudArt slot");
    }

    public static Texture2D BuildShelf(UWTextures pOTextures, FilterMode peFilter, out ShelfLayout pOLayout)
    {
        UWPicture lOShelf = UWHudArt.BuildShelf(pOTextures, out UWHudArt.ShelfLayout lOLayout);

        pOLayout = new ShelfLayout
        {
            HealthFlask = new Vector2Int(lOLayout.HealthFlaskX, lOLayout.HealthFlaskY),
            ManaFlask = new Vector2Int(lOLayout.ManaFlaskX, lOLayout.ManaFlaskY),
            Gem = new Vector2Int(lOLayout.GemX, lOLayout.GemY),
            Width = lOLayout.Width,
            Height = lOLayout.Height
        };

        return fToTexture(lOShelf, peFilter, "UWModernHudArt shelf");
    }

    public static bool[] BuildFlaskMask(UWFlasks pOFlasks)
    {
        return UWHudArt.BuildFlaskMask(pOFlasks);
    }

    public static bool[] BuildGemMask(UWPowerGem pOGem)
    {
        return UWHudArt.BuildGemMask(pOGem);
    }

    /// <summary>A picture (rows top down) as a Unity texture (rows bottom up), the colour help
    /// applied.</summary>
    private static Texture2D fToTexture(UWPicture pOPicture, FilterMode peFilter, string psName)
    {
        Color32[] lyOut = new Color32[pOPicture.Pixels.Length];
        int liAt = 0;

        for (int y = pOPicture.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < pOPicture.Width; x++)
            {
                UWColor32 lOColour = pOPicture.Get(x, y);

                lyOut[liAt++] = lOColour.A == 0
                    ? new Color32(0, 0, 0, 0)
                    : UWColourVision.Apply(new Color32(lOColour.R, lOColour.G, lOColour.B, lOColour.A));
            }
        }

        Texture2D lOTexture = new Texture2D(pOPicture.Width, pOPicture.Height, TextureFormat.RGBA32, false);
        lOTexture.name = psName;
        lOTexture.filterMode = peFilter;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(lyOut);
        lOTexture.Apply();

        return lOTexture;
    }

    /// <summary>A copy of a Unity texture (rows bottom up, as UWGameUI.fGetTextureInvert makes
    /// them) with the pixels outside a top-down keep mask transparent.</summary>
    public static Texture2D ApplyMask(Texture2D pOSource, bool[] pbKeepTopDown)
    {
        Color32[] lyPixels = pOSource.GetPixels32();
        int liWidth = pOSource.width;
        int liHeight = pOSource.height;

        if (pbKeepTopDown != null && pbKeepTopDown.Length == lyPixels.Length)
        {
            for (int y = 0; y < liHeight; y++)
            {
                int liMaskRow = (liHeight - 1 - y) * liWidth;

                for (int x = 0; x < liWidth; x++)
                {
                    if (!pbKeepTopDown[liMaskRow + x])
                        lyPixels[(y * liWidth) + x] = new Color32(0, 0, 0, 0);
                }
            }
        }

        Texture2D lOTexture = new Texture2D(liWidth, liHeight, TextureFormat.RGBA32, false);
        lOTexture.name = "UWModernHudArt keyed";
        lOTexture.filterMode = pOSource.filterMode;
        lOTexture.wrapMode = TextureWrapMode.Clamp;
        lOTexture.SetPixels32(lyPixels);
        lOTexture.Apply();

        return lOTexture;
    }
}
