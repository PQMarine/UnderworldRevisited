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

    public static Texture2D BuildHeadingStrip(UWTextures pOTextures, int piWidth, int piOpenHalf, FilterMode peFilter,
        int piClosedWidth = UWHudArt.StripClosedWidth)
    {
        return fToTexture(UWHudArt.BuildHeadingStrip(pOTextures, piWidth, piOpenHalf, piClosedWidth), peFilter, "UWModernHudArt heading strip");
    }

    public static Texture2D BuildHeadingStripWithBrandAt(UWTextures pOTextures, int piWidth, int piBrandLeft, FilterMode peFilter)
    {
        return fToTexture(UWHudArt.BuildHeadingStripWithBrandAt(pOTextures, piWidth, piBrandLeft), peFilter, "UWModernHudArt heading strip");
    }

    public static Texture2D BuildSlotCircle(UWTextures pOTextures, FilterMode peFilter)
    {
        return fToTexture(UWHudArt.BuildSlotCircle(pOTextures), peFilter, "UWModernHudArt slot");
    }

    public static Texture2D BuildShelf(UWTextures pOTextures, FilterMode peFilter, out ShelfLayout pOLayout, bool pbGemApart = false)
    {
        UWPicture lOShelf = UWHudArt.BuildShelf(pOTextures, out UWHudArt.ShelfLayout lOLayout, pbGemApart);

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

    /// <summary>The stone shelf deco panel at a width in its own pixels (UWHudArt.BuildStoneShelf).</summary>
    public static Texture2D BuildStoneShelf(UWTextures pOTextures, int piWidth, FilterMode peFilter)
    {
        return fToTexture(UWHudArt.BuildStoneShelf(pOTextures, piWidth), peFilter, "UWModernHudArt stone shelf");
    }

    /// <summary>The power gem's own stand (UWHudArt.BuildGemStand); pOGem is where the gem lies on it.</summary>
    /// <summary>A picture of one of the game's .GR files as it is; null without it.</summary>
    public static Texture2D BuildPicture(UWTextures pOTextures, UWTexture.TextureTypes peType, int piIndex, FilterMode peFilter)
    {
        UWTexture lOPicture = pOTextures.GetTextureByType(peType, piIndex);

        return lOPicture != null ? fToTexture(UWPicture.From(lOPicture), peFilter, "UWModernHudArt " + peType + " " + piIndex) : null;
    }

    /// <summary>A page of the original's right panel (PANELS.GR: 0 inventory, 1 runes, 2 stats);
    /// null without it.</summary>
    public static Texture2D BuildPanelPage(UWTextures pOTextures, int piPage, FilterMode peFilter)
    {
        UWTexture lOPage = pOTextures.GetTextureByType(UWTexture.TextureTypes.PANELS, piPage);

        return lOPage != null ? fToTexture(UWPicture.From(lOPage), peFilter, "UWModernHudArt panel page " + piPage) : null;
    }

    /// <summary>The original's conversation top, its sample names and portraits gone (UWHudArt.BuildConversationHeader).</summary>
    /// <summary>The classic frame widened by piExtra columns (UWHudArt.BuildClassicWideFrame), and
    /// what was built (its picture and cuts) for the parts laid on it.</summary>
    public static Texture2D BuildClassicWideFrame(UWTextures pOTextures, int piExtra, FilterMode peFilter, out UWHudArt.ClassicWideFrame pOFrame)
    {
        pOFrame = UWHudArt.BuildClassicWideFrame(pOTextures, piExtra);

        return fToTexture(pOFrame.Picture, peFilter, "UWModernHudArt classic wide " + piExtra);
    }

    public static Texture2D BuildConversationHeader(UWTextures pOTextures, FilterMode peFilter, bool pbOutline = false)
    {
        return fToTexture(UWHudArt.PadAndOutline(UWHudArt.BuildConversationHeader(pOTextures), pbOutline), peFilter, "UWModernHudArt conversation top");
    }

    /// <summary>The original's conversation parchment at a height (UWHudArt.BuildConversationParchment).</summary>
    public static Texture2D BuildConversationParchment(UWTextures pOTextures, int piRows, FilterMode peFilter, bool pbKnobRollers = false,
        bool pbOutline = false)
    {
        return fToTexture(UWHudArt.PadAndOutline(UWHudArt.BuildConversationParchment(pOTextures, piRows, pbKnobRollers), pbOutline), peFilter,
            "UWModernHudArt conversation parchment " + piRows);
    }

    /// <summary>The rune hollow cut out of the frame (UWHudArt.BuildRuneHollow).</summary>
    public static Texture2D BuildRuneHollow(UWTextures pOTextures, bool pbOutline, FilterMode peFilter)
    {
        return fToTexture(UWHudArt.BuildRuneHollow(pOTextures, pbOutline), peFilter, "UWModernHudArt rune hollow");
    }

    /// <summary>The compass's stone disc without the cross (UWHudArt.BuildCompassDisc).</summary>
    public static Texture2D BuildCompassDisc(UWTextures pOTextures, bool pbOutline, FilterMode peFilter)
    {
        return fToTexture(UWHudArt.BuildCompassDisc(pOTextures, pbOutline), peFilter, "UWModernHudArt compass disc");
    }

    public static Texture2D BuildGemStand(UWTextures pOTextures, FilterMode peFilter, out Vector2Int pOGem)
    {
        UWPicture lOStand = UWHudArt.BuildGemStand(pOTextures, out int liX, out int liY);

        pOGem = new Vector2Int(liX, liY);

        return fToTexture(lOStand, peFilter, "UWModernHudArt gem stand");
    }

    public static bool[] BuildFlaskMask(UWFlasks pOFlasks)
    {
        return UWHudArt.BuildFlaskMask(pOFlasks);
    }

    /// <summary>The flask standing free, without the shelf's stone (UWHudArt.BuildFreeFlaskMask).</summary>
    public static bool[] BuildFreeFlaskMask(UWFlasks pOFlasks)
    {
        return UWHudArt.BuildFreeFlaskMask(pOFlasks);
    }

    public static bool[] BuildGemMask(UWPowerGem pOGem)
    {
        return UWHudArt.BuildGemMask(pOGem);
    }

    /// <summary>
    /// THE ORIGINAL'S PIXEL PROPORTION (per user, 2026-10-09): its pictures were drawn for a screen
    /// whose pixels are 1.2 times taller than wide (the classic scheme's 4:3 fix, UWDisplayAspect);
    /// with square pixels the paperdoll looked "chubbier" and a shield "too wide". The modern scheme
    /// keeps the heights and draws the original's figure and things this much as wide - the same
    /// proportion without changing any part's height (per user: "the same result if we make it 20 %
    /// narrower"). Leather, frames and generated backs are not affected.
    /// </summary>
    public const float PixelAspectX = 1f / 1.2f;

    /// <summary>A part's generated back (UWBackdropArt, UWModernBacks).</summary>
    public static Texture2D BuildBackdrop(UWTextures pOTextures, int piWidth, int piHeight, UWModernBacks.Back pOBack, FilterMode peFilter,
        int piVariant = 0)
    {
        return fToTexture(UWBackdropArt.Build(pOTextures, piWidth, piHeight, pOBack.Shape, pOBack.Colour, pOBack.Pattern, pOBack.Border,
            piVariant), peFilter, "UWModernHudArt back");
    }

    /// <summary>A leather part's leather - or its back in the leather's place, a rectangle, when it
    /// has one (UWModernBacks.IsLeatherPart).</summary>
    public static Texture2D BuildPartLeather(UWModernLayout.ElementEnum peElement, UWTextures pOTextures, int piWidth, int piHeight,
        FilterMode peFilter, int piVariant = 0)
    {
        UWModernBacks.Back lOBack = UWModernBacks.Get(peElement);

        if (lOBack.Shape == UWBackdropArt.ShapeEnum.None)
            return BuildLeather(pOTextures, piWidth, piHeight, peFilter, piVariant);

        lOBack.Shape = UWBackdropArt.ShapeEnum.Rect;

        return BuildBackdrop(pOTextures, piWidth, piHeight, lOBack, peFilter, piVariant);
    }

    /// <summary>A leather part's tab, open on the right (BuildLeatherTab) - or its back's: the
    /// rectangle made wider and its right part cut off, so the border stays open there.</summary>
    public static Texture2D BuildPartLeatherTab(UWModernLayout.ElementEnum peElement, UWTextures pOTextures, int piWidth, int piHeight,
        FilterMode peFilter)
    {
        UWModernBacks.Back lOBack = UWModernBacks.Get(peElement);

        if (lOBack.Shape == UWBackdropArt.ShapeEnum.None)
            return BuildLeatherTab(pOTextures, piWidth, piHeight, peFilter);

        UWPicture lOWide = UWBackdropArt.Build(pOTextures, piWidth + 4, piHeight, UWBackdropArt.ShapeEnum.Rect, lOBack.Colour, lOBack.Pattern,
            lOBack.Border);

        return fToTexture(lOWide.Crop(new UWRectInt(0, 0, piWidth, piHeight)), peFilter, "UWModernHudArt back tab");
    }

    /// <summary>A leather part's slot circle - or, on a back of its own, the ring alone carved into
    /// the back, the slot a light shadow (UWHudArt.BuildSlotRing; per user, 2026-10-09: the circles
    /// showed squares of leather on a back).</summary>
    public static Texture2D BuildPartSlot(UWModernLayout.ElementEnum peElement, UWTextures pOTextures, FilterMode peFilter)
    {
        UWModernBacks.Back lOBack = UWModernBacks.Get(peElement);

        if (lOBack.Shape == UWBackdropArt.ShapeEnum.None)
            return BuildSlotCircle(pOTextures, peFilter);

        return fToTexture(UWHudArt.BuildSlotRing(pOTextures, UWBackdropArt.Ramp(pOTextures, lOBack.Colour)), peFilter, "UWModernHudArt slot ring");
    }

    /// <summary>The character panel's page - or, on a back of its own, only its circles carved into
    /// the back, without the weight's stone (UWHudArt.BuildPaperdollMarks).</summary>
    public static Texture2D BuildPartPaperdollPage(UWModernLayout.ElementEnum peElement, UWTextures pOTextures, FilterMode peFilter)
    {
        UWModernBacks.Back lOBack = UWModernBacks.Get(peElement);

        if (lOBack.Shape == UWBackdropArt.ShapeEnum.None)
            return BuildPaperdollPage(pOTextures, peFilter);

        return fToTexture(UWHudArt.BuildPaperdollMarks(pOTextures, UWBackdropArt.Ramp(pOTextures, lOBack.Colour)), peFilter,
            "UWModernHudArt paperdoll marks");
    }

    /// <summary>The paperdoll's figure - the body (BODIES.GR index) and the armour in the classic
    /// order (helmet, gloves, legs, chest, boots) with their places on MAIN.BYT - without the page,
    /// for a back of the character panel's own (UWHudArt.ComposePaperdoll); pOOrigin is where it
    /// lies on MAIN.BYT.</summary>
    public static Texture2D BuildFreeFigure(UWTextures pOTextures, int piBody, UWTexture[] pOArmour, Vector2Int[] pOAt, FilterMode peFilter,
        out Vector2Int pOOrigin)
    {
        var lOPictures = new System.Collections.Generic.List<UWPicture>();
        var lOAt = new System.Collections.Generic.List<(int X, int Y)>();

        for (int liAt = 0; liAt < pOArmour.Length; liAt++)
        {
            lOPictures.Add(pOArmour[liAt] != null ? UWPicture.From(pOArmour[liAt]) : null);
            lOAt.Add((pOAt[liAt].x, pOAt[liAt].y));
        }

        UWPicture lOFigure = UWHudArt.ComposePaperdoll(pOTextures, piBody, lOPictures, lOAt, out int liX, out int liY);

        pOOrigin = new Vector2Int(liX, liY);

        return fToTexture(lOFigure, peFilter, "UWModernHudArt figure");
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
