using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN HUD'S FRAMES, cut from the original graphics (look decided per user on mockups,
/// 2026-10-03, see Todo "Modern control scheme"):
///
///   - the LEATHER of the bags and the action bar: INV.GR image 6, the original's 85 x 41
///     container panel, stretched to any size from its calm parts only - its highlight streak
///     and dark patch made a tiling of the whole centre uneven;
///   - the SLOT CIRCLE: the top right circle of the backpack panel in MAIN.BYT, whose fill is
///     closest to that leather, cut round;
///   - the STONE SHELF the flasks stand on, from MAIN.BYT, widened between the flasks so the
///     power gem fits, with the gem's own pedestal from the frame below it.
///
/// Everything is composed from the palette colours (rows top down, as the files store them),
/// keyed there, and only turned into a texture at the end, with the colour help applied as
/// UWGameUI.fGetTextureInvert does. Callers rebuild when UWColourVision.Version changes.
/// </summary>
public static class UWModernHudArt
{
    // ------------------------------------------------- The leather (INV.GR 6)

    /// <summary>The panel's frame: 4 columns on the left, 3 on the right, 3 rows on top, 4 at
    /// the bottom - after the two fully transparent columns at the panel's right edge (83 and 84,
    /// LeatherTransparentRight) are cut off; with them the windows showed a see-through strip on
    /// the right (per user, 2026-10-03).</summary>
    public const int LeatherLeft = 4;

    public const int LeatherRight = 3;

    private const int LeatherTransparentRight = 2;

    public const int LeatherTop = 3;

    public const int LeatherBottom = 4;

    private const int LeatherImage = 6;

    /// <summary>The most even part of the leather, 40 x 12, no highlight and no dark patch.</summary>
    private static readonly RectInt msCalmCentre = new RectInt(28, 23, 40, 12);

    /// <summary>The stretch of the top and bottom edge with the least variation.</summary>
    private const int CalmEdgeX0 = 36;

    private const int CalmEdgeX1 = 50;

    /// <summary>The same for the left and right edge.</summary>
    private const int CalmEdgeY0 = 14;

    private const int CalmEdgeY1 = 26;

    // ------------------------------------------------- The slot circle (MAIN.BYT)

    /// <summary>The top right circle of the backpack panel: its fill (106,61,17) is closest to
    /// the leather (92,52,13); the top left one lies in the panel's highlight. 17 x 17 - the
    /// 18th column is already the panel's frame.</summary>
    private static readonly RectInt msSlotCircle = new RectInt(298, 81, 17, 17);

    private const float SlotCircleRadius = 9f;

    public const int SlotSize = 17;

    // ------------------------------------------------- The shelf (MAIN.BYT)

    private const int ShelfTop = 125;

    private const int ShelfBottom = 166;

    /// <summary>The shelf with the vitality flask (drawn at 248,125), up to the mana flask.</summary>
    private const int ShelfLeftX0 = 237;

    private const int ShelfLeftX1 = 272;

    /// <summary>The shelf with the mana flask (drawn at 284,125).</summary>
    private const int ShelfRightX0 = 284;

    private const int ShelfRightX1 = 318;

    /// <summary>The flask the left part holds, from its left edge.</summary>
    private const int HealthFlaskX = 248 - ShelfLeftX0;

    /// <summary>The rows of the 12 columns between the flasks that carry the chain ring.</summary>
    private const int ChainRows = 15;

    /// <summary>Where MAIN.BYT draws the power gem; its pedestal is the 6 rows below it.</summary>
    private const int GemX = 4;

    private const int GemY = 139;

    private const int GemWidth = 31;

    private const int GemHeight = 12;

    private const int PedestalRows = 6;

    /// <summary>Free columns on each side of the gem in the widened middle.</summary>
    private const int GemMargin = 3;

    /// <summary>The gem's pedestal ends 2 rows below the flask pictures (chosen per user from
    /// three heights).</summary>
    private const int GemBottom = 35;

    /// <summary>Above this row the shelf's back edge was dithered against the black: the loose
    /// dark dots left there are dirt without it.</summary>
    private const int ShelfDitherRows = 26;

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

    // ------------------------------------------------- Pixel work

    /// <summary>A picture as the files hold it: rows top down, palette colours.</summary>
    private sealed class Picture
    {
        public readonly int Width;

        public readonly int Height;

        public readonly UWColor32[] Pixels;

        public Picture(int piWidth, int piHeight)
        {
            Width = piWidth;
            Height = piHeight;
            Pixels = new UWColor32[piWidth * piHeight];
        }

        public static Picture From(UWTexture pOTexture)
        {
            Picture lOPicture = new Picture(pOTexture.Width, pOTexture.Height);
            UWColor32[] lyColours = pOTexture.GetUWColor32();

            System.Array.Copy(lyColours, lOPicture.Pixels, Mathf.Min(lyColours.Length, lOPicture.Pixels.Length));

            return lOPicture;
        }

        public UWColor32 Get(int piX, int piY)
        {
            return Pixels[(piY * Width) + piX];
        }

        public void Set(int piX, int piY, UWColor32 pOColour)
        {
            Pixels[(piY * Width) + piX] = pOColour;
        }

        public Picture Crop(RectInt pORect)
        {
            Picture lOOut = new Picture(pORect.width, pORect.height);

            for (int y = 0; y < pORect.height; y++)
            {
                for (int x = 0; x < pORect.width; x++)
                    lOOut.Set(x, y, Get(pORect.x + x, pORect.y + y));
            }

            return lOOut;
        }

        /// <summary>Copies every pixel, the transparent ones too.</summary>
        public void Paste(Picture pOSource, int piX, int piY)
        {
            for (int y = 0; y < pOSource.Height; y++)
            {
                for (int x = 0; x < pOSource.Width; x++)
                {
                    int liX = piX + x;
                    int liY = piY + y;

                    if (liX >= 0 && liY >= 0 && liX < Width && liY < Height)
                        Set(liX, liY, pOSource.Get(x, y));
                }
            }
        }

        /// <summary>Copies only the visible pixels.</summary>
        public void Overlay(Picture pOSource, int piX, int piY)
        {
            for (int y = 0; y < pOSource.Height; y++)
            {
                for (int x = 0; x < pOSource.Width; x++)
                {
                    UWColor32 lOColour = pOSource.Get(x, y);
                    int liX = piX + x;
                    int liY = piY + y;

                    if (lOColour.A != 0 && liX >= 0 && liY >= 0 && liX < Width && liY < Height)
                        Set(liX, liY, lOColour);
                }
            }
        }

        public Texture2D ToTexture(FilterMode peFilter, string psName)
        {
            Color32[] lyOut = new Color32[Pixels.Length];
            int liAt = 0;

            for (int y = Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < Width; x++)
                {
                    UWColor32 lOColour = Get(x, y);

                    lyOut[liAt++] = lOColour.A == 0
                        ? new Color32(0, 0, 0, 0)
                        : UWColourVision.Apply(new Color32(lOColour.R, lOColour.G, lOColour.B, lOColour.A));
                }
            }

            Texture2D lOTexture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            lOTexture.name = psName;
            lOTexture.filterMode = peFilter;
            lOTexture.wrapMode = TextureWrapMode.Clamp;
            lOTexture.SetPixels32(lyOut);
            lOTexture.Apply();

            return lOTexture;
        }
    }

    /// <summary>Tiles a piece with every second copy flipped, so no seam repeats.</summary>
    private static Picture fMirrorTile(Picture pOSource, int piWidth, int piHeight, bool pbFlipX, bool pbFlipY)
    {
        Picture lOOut = new Picture(Mathf.Max(0, piWidth), Mathf.Max(0, piHeight));

        for (int y = 0; y < lOOut.Height; y++)
        {
            int liTileY = y / pOSource.Height;
            int liInY = y % pOSource.Height;

            if (pbFlipY && (liTileY & 1) != 0)
                liInY = pOSource.Height - 1 - liInY;

            for (int x = 0; x < lOOut.Width; x++)
            {
                int liTileX = x / pOSource.Width;
                int liInX = x % pOSource.Width;

                if (pbFlipX && (liTileX & 1) != 0)
                    liInX = pOSource.Width - 1 - liInX;

                lOOut.Set(x, y, pOSource.Get(liInX, liInY));
            }
        }

        return lOOut;
    }

    /// <summary>The original's backdrop behind the flasks and the gem: transparent, black, and
    /// the dark blue-grey (28,28,36) the panels dither with.</summary>
    private static bool fIsBackdrop(UWColor32 pOColour)
    {
        return pOColour.A == 0 || (pOColour.R == pOColour.G && pOColour.R <= 28 && pOColour.B - pOColour.R <= 8);
    }

    /// <summary>Which pixels are backdrop reached from the picture's border - the rest stays.</summary>
    private static bool[] fFloodBackdrop(Picture pOPicture)
    {
        bool[] lbBackdrop = new bool[pOPicture.Pixels.Length];
        Stack<Vector2Int> lOTodo = new Stack<Vector2Int>();

        for (int x = 0; x < pOPicture.Width; x++)
        {
            lOTodo.Push(new Vector2Int(x, 0));
            lOTodo.Push(new Vector2Int(x, pOPicture.Height - 1));
        }

        for (int y = 0; y < pOPicture.Height; y++)
        {
            lOTodo.Push(new Vector2Int(0, y));
            lOTodo.Push(new Vector2Int(pOPicture.Width - 1, y));
        }

        while (lOTodo.Count > 0)
        {
            Vector2Int lOAt = lOTodo.Pop();

            if (lOAt.x < 0 || lOAt.y < 0 || lOAt.x >= pOPicture.Width || lOAt.y >= pOPicture.Height)
                continue;

            int liIndex = (lOAt.y * pOPicture.Width) + lOAt.x;

            if (lbBackdrop[liIndex] || !fIsBackdrop(pOPicture.Pixels[liIndex]))
                continue;

            lbBackdrop[liIndex] = true;

            lOTodo.Push(new Vector2Int(lOAt.x + 1, lOAt.y));
            lOTodo.Push(new Vector2Int(lOAt.x - 1, lOAt.y));
            lOTodo.Push(new Vector2Int(lOAt.x, lOAt.y + 1));
            lOTodo.Push(new Vector2Int(lOAt.x, lOAt.y - 1));
        }

        return lbBackdrop;
    }

    private static void fClear(Picture pOPicture, bool[] pbClear)
    {
        for (int liAt = 0; liAt < pOPicture.Pixels.Length; liAt++)
        {
            if (pbClear[liAt])
                pOPicture.Pixels[liAt] = new UWColor32(0, 0, 0, 0);
        }
    }

    private static Picture fMain(UWTextures pOTextures)
    {
        return Picture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.MAIN, 0));
    }

    // ------------------------------------------------- The pictures

    /// <summary>The leather at any size of at least the frame, in original pixels.</summary>
    /// <summary>The leather at any size, its inside the clouds (fClouds); piVariant picks another
    /// part of the cloud field, so neighbouring panels do not start alike.</summary>
    public static Texture2D BuildLeather(UWTextures pOTextures, int piWidth, int piHeight, FilterMode peFilter, int piVariant = 0)
    {
        return fLeather(pOTextures, piWidth, piHeight, piVariant).ToTexture(peFilter, "UWModernHudArt leather");
    }

    /// <summary>A tab of leather, framed on the top, left and bottom and OPEN on the right. Laid
    /// over a panel's left frame (LeatherLeft columns wider than it shows), its top and bottom
    /// frame lines turn into the panel's frame and its leather runs on into the panel's - the
    /// handle is one piece with the panel (per user, 2026-10-03: beside the panel it looked cut
    /// off).</summary>
    public static Texture2D BuildLeatherTab(UWTextures pOTextures, int piWidth, int piHeight, FilterMode peFilter)
    {
        Picture lOFull = fLeather(pOTextures, piWidth + LeatherRight + LeatherLeft, piHeight, 0);

        return lOFull.Crop(new RectInt(0, 0, piWidth, piHeight)).ToTexture(peFilter, "UWModernHudArt tab");
    }

    /// <summary>
    /// The original's inventory page above the backpack - MAIN.BYT x 236 to 317, rows 7 to 81:
    /// the leather with the circles of the shoulders, hands and rings, on which the paperdoll
    /// stands (the character panel of the modern scheme, look per user on a mockup, 2026-10-03).
    /// WIDENED BY FOUR to the backpack window's 86 (per user: then it docks onto it): the two
    /// leather columns inside each border are doubled (PaperdollWidenLeft/Right), so everything
    /// on the page sits PaperdollShift further right.
    /// </summary>
    public static Texture2D BuildPaperdollPage(UWTextures pOTextures, FilterMode peFilter)
    {
        Picture lOPage = fMain(pOTextures).Crop(PaperdollPage);
        Picture lOOut = new Picture(PaperdollPageWidth, lOPage.Height);

        for (int x = 0; x < PaperdollPageWidth; x++)
        {
            int liColumn = PageColumnToMain(x) - PaperdollPage.x;

            for (int y = 0; y < lOPage.Height; y++)
                lOOut.Set(x, y, lOPage.Get(liColumn, y));
        }

        return lOOut.ToTexture(peFilter, "UWModernHudArt paperdoll");
    }

    /// <summary>The page's place in MAIN.BYT (rows from the top).</summary>
    public static readonly RectInt PaperdollPage = new RectInt(236, 7, 82, 75);

    /// <summary>The widened page: as wide as the backpack window.</summary>
    public const int PaperdollPageWidth = 86;

    /// <summary>Page columns from which a column is doubled: the leather just inside the left
    /// border (MAIN x 239 and 240) and inside the right one (x 310 and 311).</summary>
    private const int PaperdollWidenLeft = 3;

    private const int PaperdollWidenRight = 74;

    /// <summary>How far the left doubling moves what lies on the page.</summary>
    public const int PaperdollShift = 2;

    /// <summary>The MAIN.BYT column a column of the widened page shows.</summary>
    public static int PageColumnToMain(int piColumn)
    {
        int liColumn;

        if (piColumn < PaperdollWidenLeft + 2)
            liColumn = piColumn;
        else if (piColumn < PaperdollWidenLeft + 4)
            liColumn = piColumn - 2;
        else if (piColumn < PaperdollWidenRight + 4)
            liColumn = piColumn - PaperdollShift;
        else
            liColumn = piColumn - 4;

        return PaperdollPage.x + Mathf.Clamp(liColumn, 0, PaperdollPage.width - 1);
    }

    /// <summary>
    /// THE CLOUD LEATHER (per user, 2026-10-04): the mirrored tiling of the calm centre showed as a
    /// regular pattern on the larger panels, and the leather's stripes do not tile without seams
    /// either - so the inside is drawn anew as soft clouds, as the original's leather has them,
    /// from its own colours (fCloudColours) with a little grain; the frame stays the original's.
    /// Variant C of three offline renders per user: five shades, a dark one among them, as the
    /// light text wants a darker ground; per user the same day for every modern background.
    ///
    /// ONE FIXED FIELD, anchored at the inside's top left: every value comes from its coordinates
    /// alone (fHash), so a panel that grows or shrinks shows more or less of the same clouds - a
    /// seed per size drew it anew at every change (per user, the same day: it looked odd).
    /// </summary>
    // ------------------------------------------------- The heading strip

    /// <summary>
    /// THE HEADING STRIP (per user on mockups, 2026-10-04): the modern scheme's heading on a strip
    /// of leather at the top centre, the gargoyle's head of the original's frame at the left in its
    /// own colours (burned into the leather it looked like a graphics fault in so few pixels, per
    /// user the same day), its eyes lit by the original's EYES.GR after a hit, the foe's name at
    /// the right. Closed it is only as wide as the heading; while the eyes are lit it opens
    /// as wide as the brand and the foe's name need, the same to both sides (UWModernHud.fUpdateStrip;
    /// a fixed 150 left too much room, per user 2026-10-04). The clouds are anchored at the strip's
    /// centre and the brand at the open strip's left edge, so both stand still while it opens.
    /// </summary>
    public const int StripHeight = 24;

    public const int StripClosedWidth = 44;

    /// <summary>The open strip's least half width: the brand at the left with its margin, the
    /// heading's half and a gap.</summary>
    public const int StripMinOpenHalf = 46;

    /// <summary>The brand's distance from the open strip's left edge, and its top.</summary>
    public const int BrandMargin = 6;

    public const int BrandTop = 3;

    public const int BrandWidth = 24;

    /// <summary>The original's eyes (EYES.GR, 20 x 3) on the brand: the slits lie in rows 4 and 5
    /// of the head, the image's left edge two columns in.</summary>
    public const int EyesLeftInBrand = 2;

    public const int EyesTopInBrand = 3;

    /// <summary>The gargoyle's head in MAIN.BYT, its eyes at 130-133 and 142-145, rows 4 and 5.</summary>
    private static readonly RectInt msGargoyleHead = new RectInt(126, 0, 24, 18);

    /// <summary>The brand's left edge in a strip of this width that opens to piOpenHalf.</summary>
    public static int BrandLeft(int piWidth, int piOpenHalf)
    {
        return (piWidth / 2) - piOpenHalf + BrandMargin;
    }

    public static Texture2D BuildHeadingStrip(UWTextures pOTextures, int piWidth, int piOpenHalf, FilterMode peFilter)
    {
        Picture lOStrip = fLeather(pOTextures, piWidth, StripHeight, 4, -(piWidth / 2));

        fPasteGargoyle(pOTextures, lOStrip, BrandLeft(piWidth, piOpenHalf), BrandTop);

        return lOStrip.ToTexture(peFilter, "UWModernHudArt heading strip");
    }

    /// <summary>
    /// The head's own pixels in msGargoyleHead (X), without what belongs to the frame: the dark
    /// and blue-grey band beside the jaw, the frame's wooden bar below it except the chin, the
    /// corners (per user, 2026-10-04: dark patches beside the teeth). Worked out by a flood from
    /// the crop's sides and bottom through dark and blue pixels, checked on a render.
    /// </summary>
    private static readonly string[] msGargoyleMask =
    {
        ".XXXXXXXXXXXXXXXXXXXXXX.",
        ".XXXXXXXXXXXXXXXXXXXXXX.",
        "XXXXXXXXXXXXXXXXXXXXXXXX",
        "XXXXXXXXXXXXXXXXXXXXXXXX",
        "XXXXXXXXXXXXXXXXXXXXXXXX",
        "XXXXXXXXXXXXXXXXXXXXXXXX",
        "XXXXXXXXXXXXXXXXXXXXXXXX",
        ".XXXXXXXXXXXXXXXXXXXXXX.",
        ".XXXXXXXXXXXXXXXXXXXXXX.",
        "..XXXXXXXXXXXXXXXXXXXX..",
        "..XXXXXXXXXXXXXXXXXXXX..",
        "..XXXXXXXXXXXXXXXXXXXX..",
        "...XXXXXXXXXXXXXXXXXX...",
        "...XXXXXXXXXXXXXXXXXX...",
        "...XXXXXXXXXXXXXXXXXX...",
        "..XXXXXXXXXXXXXXXXXXXX..",
        "........XXXXXXXX........",
        "........................"
    };

    /// <summary>Lays the gargoyle's head onto the strip's inside at the given top left - its own
    /// pixels (msGargoyleMask). Not on the closed strip: there its edge showed a sliver of it
    /// (per user, 2026-10-04).</summary>
    private static void fPasteGargoyle(UWTextures pOTextures, Picture pOStrip, int piLeft, int piTop)
    {
        if (pOStrip.Width <= StripClosedWidth)
            return;

        Picture lOHead = fMain(pOTextures).Crop(msGargoyleHead);
        int liInsideRight = pOStrip.Width - LeatherRight;
        int liInsideBottom = pOStrip.Height - LeatherBottom;

        for (int y = 0; y < lOHead.Height; y++)
        {
            for (int x = 0; x < lOHead.Width; x++)
            {
                UWColor32 lOColour = lOHead.Get(x, y);
                int liAtX = piLeft + x;
                int liAtY = piTop + y;

                if (lOColour.A == 0 || fLuma(lOColour) > 240f || msGargoyleMask[y][x] != 'X'
                    || liAtX < LeatherLeft || liAtY < LeatherTop || liAtX >= liInsideRight || liAtY >= liInsideBottom)
                    continue;

                pOStrip.Set(liAtX, liAtY, lOColour);
            }
        }
    }

    /// <summary>The cloud octaves: cell size in original pixels and weight.</summary>
    private static readonly float[] mfCloudCells = { 24f, 10f, 4f };

    private static readonly float[] mfCloudWeights = { 0.55f, 0.3f, 0.15f };

    /// <summary>The noise range spread over the shades, and the grain.</summary>
    private const float CloudLow = 0.2f;

    private const float CloudHigh = 0.8f;

    private const float CloudGrain = 0.08f;

    /// <summary>The shades, darkest first: the calm centre's colours, and below them the most
    /// frequent darker colour of the leather's inside (not the near-black of the scratches).</summary>
    private static List<UWColor32> fCloudColours(Picture pOSource)
    {
        List<UWColor32> lOShades = new List<UWColor32>();
        Picture lOCalm = pOSource.Crop(msCalmCentre);

        foreach (UWColor32 lOColour in lOCalm.Pixels)
        {
            if (!lOShades.Exists(lOOther => fSame(lOOther, lOColour)))
                lOShades.Add(lOColour);
        }

        lOShades.Sort((lOA, lOB) => fLuma(lOA).CompareTo(fLuma(lOB)));

        if (lOShades.Count == 0)
            return lOShades;

        float lfDarkest = fLuma(lOShades[0]);
        Dictionary<int, int> lOCounts = new Dictionary<int, int>();
        UWColor32 lODark = lOShades[0];
        int liBest = 0;

        for (int y = LeatherTop; y < pOSource.Height - LeatherBottom; y++)
        {
            for (int x = LeatherLeft; x < pOSource.Width - LeatherRight; x++)
            {
                UWColor32 lOColour = pOSource.Get(x, y);
                float lfLuma = fLuma(lOColour);

                if (lOColour.A == 0 || lfLuma >= lfDarkest || lfLuma < lfDarkest * 0.6f)
                    continue;

                int liKey = (lOColour.R << 16) | (lOColour.G << 8) | lOColour.B;

                lOCounts.TryGetValue(liKey, out int liCount);
                lOCounts[liKey] = ++liCount;

                if (liCount > liBest)
                {
                    liBest = liCount;
                    lODark = lOColour;
                }
            }
        }

        if (liBest > 0)
            lOShades.Insert(0, lODark);

        return lOShades;
    }

    private static bool fSame(UWColor32 pOA, UWColor32 pOB)
    {
        return pOA.R == pOB.R && pOA.G == pOB.G && pOA.B == pOB.B && pOA.A == pOB.A;
    }

    private static float fLuma(UWColor32 pOColour)
    {
        return (0.299f * pOColour.R) + (0.587f * pOColour.G) + (0.114f * pOColour.B);
    }

    /// <summary>Soft clouds of the leather's shades - see the cloud leather above.</summary>
    private static Picture fClouds(Picture pOSource, int piWidth, int piHeight, int piVariant, int piShiftX = 0)
    {
        Picture lOOut = new Picture(Mathf.Max(0, piWidth), Mathf.Max(0, piHeight));
        List<UWColor32> lOShades = fCloudColours(pOSource);

        if (lOShades.Count == 0)
            return lOOut;

        // Another variant: another part of the same field, far away.
        int liOffsetX = (piVariant * 977) + piShiftX;
        int liOffsetY = piVariant * 613;

        for (int y = 0; y < lOOut.Height; y++)
        {
            for (int x = 0; x < lOOut.Width; x++)
            {
                int liX = x + liOffsetX;
                int liY = y + liOffsetY;
                float lfValue = 0f;

                for (int liOctave = 0; liOctave < mfCloudCells.Length; liOctave++)
                    lfValue += mfCloudWeights[liOctave] * fSmoothNoise(liOctave, liX / mfCloudCells[liOctave], liY / mfCloudCells[liOctave]);

                lfValue += (fHash(liX, liY, 99) - 0.5f) * CloudGrain;
                lfValue = Mathf.Clamp((lfValue - CloudLow) / (CloudHigh - CloudLow), 0f, 0.999f);

                lOOut.Set(x, y, lOShades[(int)(lfValue * lOShades.Count)]);
            }
        }

        return lOOut;
    }

    /// <summary>A value from 0 to 1 that depends on the point and the layer alone.</summary>
    private static float fHash(int piX, int piY, int piLayer)
    {
        unchecked
        {
            uint luHash = ((uint)piX * 73856093u) ^ ((uint)piY * 19349663u) ^ ((uint)piLayer * 83492791u);

            luHash ^= luHash >> 13;
            luHash *= 0x5bd1e995u;
            luHash ^= luHash >> 15;

            return (luHash & 0xFFFFFF) / 16777216f;
        }
    }

    private static float fSmoothNoise(int piLayer, float pfX, float pfY)
    {
        int liX = Mathf.FloorToInt(pfX);
        int liY = Mathf.FloorToInt(pfY);
        float lfX = fSmoothStep(pfX - liX);
        float lfY = fSmoothStep(pfY - liY);
        float lfTop = Mathf.Lerp(fHash(liX, liY, piLayer), fHash(liX + 1, liY, piLayer), lfX);
        float lfBottom = Mathf.Lerp(fHash(liX, liY + 1, piLayer), fHash(liX + 1, liY + 1, piLayer), lfX);

        return Mathf.Lerp(lfTop, lfBottom, lfY);
    }

    private static float fSmoothStep(float pfT)
    {
        return pfT * pfT * (3f - (2f * pfT));
    }

    private static Picture fLeather(UWTextures pOTextures, int piWidth, int piHeight, int piVariant, int piShiftX = 0)
    {
        Picture lOPanel = Picture.From(pOTextures.GetTextureByType(UWTexture.TextureTypes.INV, LeatherImage));
        Picture lOSource = lOPanel.Crop(new RectInt(0, 0, lOPanel.Width - LeatherTransparentRight, lOPanel.Height));
        int liSourceWidth = lOSource.Width;
        int liSourceHeight = lOSource.Height;

        piWidth = Mathf.Max(piWidth, LeatherLeft + LeatherRight + 1);
        piHeight = Mathf.Max(piHeight, LeatherTop + LeatherBottom + 1);

        int liInnerWidth = piWidth - LeatherLeft - LeatherRight;
        int liInnerHeight = piHeight - LeatherTop - LeatherBottom;

        Picture lOOut = new Picture(piWidth, piHeight);

        lOOut.Paste(fClouds(lOSource, liInnerWidth, liInnerHeight, piVariant, piShiftX), LeatherLeft, LeatherTop);

        RectInt lOTop = new RectInt(CalmEdgeX0, 0, CalmEdgeX1 - CalmEdgeX0, LeatherTop);
        RectInt lOBottom = new RectInt(CalmEdgeX0, liSourceHeight - LeatherBottom, CalmEdgeX1 - CalmEdgeX0, LeatherBottom);
        RectInt lOLeft = new RectInt(0, CalmEdgeY0, LeatherLeft, CalmEdgeY1 - CalmEdgeY0);
        RectInt lORight = new RectInt(liSourceWidth - LeatherRight, CalmEdgeY0, LeatherRight, CalmEdgeY1 - CalmEdgeY0);

        lOOut.Paste(fMirrorTile(lOSource.Crop(lOTop), liInnerWidth, LeatherTop, true, false), LeatherLeft, 0);
        lOOut.Paste(fMirrorTile(lOSource.Crop(lOBottom), liInnerWidth, LeatherBottom, true, false), LeatherLeft, piHeight - LeatherBottom);
        lOOut.Paste(fMirrorTile(lOSource.Crop(lOLeft), LeatherLeft, liInnerHeight, false, true), 0, LeatherTop);
        lOOut.Paste(fMirrorTile(lOSource.Crop(lORight), LeatherRight, liInnerHeight, false, true), piWidth - LeatherRight, LeatherTop);

        lOOut.Paste(lOSource.Crop(new RectInt(0, 0, LeatherLeft, LeatherTop)), 0, 0);
        lOOut.Paste(lOSource.Crop(new RectInt(liSourceWidth - LeatherRight, 0, LeatherRight, LeatherTop)), piWidth - LeatherRight, 0);
        lOOut.Paste(lOSource.Crop(new RectInt(0, liSourceHeight - LeatherBottom, LeatherLeft, LeatherBottom)), 0, piHeight - LeatherBottom);
        lOOut.Paste(lOSource.Crop(new RectInt(liSourceWidth - LeatherRight, liSourceHeight - LeatherBottom, LeatherRight, LeatherBottom)),
            piWidth - LeatherRight, piHeight - LeatherBottom);

        return lOOut;
    }

    /// <summary>One slot circle, 17 x 17, the corners outside the circle transparent so the
    /// leather shows there.</summary>
    public static Texture2D BuildSlotCircle(UWTextures pOTextures, FilterMode peFilter)
    {
        Picture lOCircle = fMain(pOTextures).Crop(msSlotCircle);
        float lfCentre = (SlotSize - 1) * 0.5f;

        for (int y = 0; y < lOCircle.Height; y++)
        {
            for (int x = 0; x < lOCircle.Width; x++)
            {
                float lfX = x - lfCentre;
                float lfY = y - lfCentre;

                if ((lfX * lfX) + (lfY * lfY) > SlotCircleRadius * SlotCircleRadius)
                    lOCircle.Set(x, y, new UWColor32(0, 0, 0, 0));
            }
        }

        return lOCircle.ToTexture(peFilter, "UWModernHudArt slot");
    }

    /// <summary>
    /// The stone shelf, without the flasks and the gem themselves (they change; they are laid
    /// on top, keyed with FlaskMask and GemMask) but with the gem's pedestal: the shelf of
    /// MAIN.BYT split between the flasks and widened there with the 12 columns between them,
    /// the chain ring cut off; the black backdrop, the frame's wood at the left end and the
    /// loose dither dots of the back edge keyed out.
    /// </summary>
    public static Texture2D BuildShelf(UWTextures pOTextures, FilterMode peFilter, out ShelfLayout pOLayout)
    {
        Picture lOMain = fMain(pOTextures);
        int liRows = ShelfBottom - ShelfTop;

        Picture lOLeft = lOMain.Crop(new RectInt(ShelfLeftX0, ShelfTop, ShelfLeftX1 - ShelfLeftX0, liRows));
        Picture lORight = lOMain.Crop(new RectInt(ShelfRightX0, ShelfTop, ShelfRightX1 - ShelfRightX0, liRows));
        Picture lOMiddleSource = lOMain.Crop(new RectInt(ShelfLeftX1, ShelfTop, ShelfRightX0 - ShelfLeftX1, liRows));

        for (int y = 0; y < ChainRows; y++)
        {
            for (int x = 0; x < lOMiddleSource.Width; x++)
                lOMiddleSource.Set(x, y, new UWColor32(0, 0, 0, 0));
        }

        int liMiddleWidth = GemWidth + (2 * GemMargin);
        Picture lOMiddle = fMirrorTile(lOMiddleSource, liMiddleWidth, liRows, true, false);

        Picture lOShelf = new Picture(lOLeft.Width + liMiddleWidth + lORight.Width, liRows);
        lOShelf.Paste(lOLeft, 0, 0);
        lOShelf.Paste(lOMiddle, lOLeft.Width, 0);
        lOShelf.Paste(lORight, lOLeft.Width + liMiddleWidth, 0);

        fClear(lOShelf, fFloodBackdrop(lOShelf));

        for (int y = 0; y < lOShelf.Height; y++)
        {
            for (int x = 0; x < lOShelf.Width; x++)
            {
                UWColor32 lOColour = lOShelf.Get(x, y);

                if (lOColour.A == 0)
                    continue;

                bool lbWood = lOColour.R > lOColour.G + 30;
                bool lbDitherDot = y < ShelfDitherRows && lOColour.R + lOColour.G + lOColour.B < 150;

                if (lbWood || lbDitherDot)
                    lOShelf.Set(x, y, new UWColor32(0, 0, 0, 0));
            }
        }

        int liGemX = lOLeft.Width + GemMargin;
        int liGemY = GemBottom - PedestalRows - GemHeight;

        lOShelf.Overlay(lOMain.Crop(new RectInt(GemX, GemY + GemHeight, GemWidth, PedestalRows)), liGemX, liGemY + GemHeight);

        pOLayout = new ShelfLayout
        {
            HealthFlask = new Vector2Int(HealthFlaskX, 0),
            ManaFlask = new Vector2Int(lOLeft.Width + liMiddleWidth, 0),
            Gem = new Vector2Int(liGemX, liGemY),
            Width = lOShelf.Width,
            Height = lOShelf.Height
        };

        return lOShelf.ToTexture(peFilter, "UWModernHudArt shelf");
    }

    /// <summary>
    /// Which pixels of a flask picture to keep: not the backdrop reached from its border.
    /// Taken from the empty and the full flask together - the fill and the bubbles only ever
    /// paint inside the glass, so every flask picture shares this outline. Rows top down.
    /// </summary>
    public static bool[] BuildFlaskMask(UWFlasks pOFlasks)
    {
        return fKeepMask(new[]
        {
            pOFlasks.GetFlask(UWFlasks.FlaskTypeEnum.Red, 0f),
            pOFlasks.GetFlask(UWFlasks.FlaskTypeEnum.Red, 1f)
        });
    }

    /// <summary>The same for the power gem, over all its fourteen pictures: a pixel goes only
    /// where every one of them shows backdrop.</summary>
    public static bool[] BuildGemMask(UWPowerGem pOGem)
    {
        List<UWTexture> lOAll = new List<UWTexture> { pOGem.Inactive, pOGem.Unpowered };

        lOAll.AddRange(pOGem.Powering);
        lOAll.AddRange(pOGem.FullyPowered);

        return fKeepMask(lOAll);
    }

    private static bool[] fKeepMask(IEnumerable<UWTexture> pOPictures)
    {
        bool[] lbKeep = null;

        foreach (UWTexture lOTexture in pOPictures)
        {
            if (lOTexture == null)
                continue;

            bool[] lbBackdrop = fFloodBackdrop(Picture.From(lOTexture));

            if (lbKeep == null)
                lbKeep = new bool[lbBackdrop.Length];

            for (int liAt = 0; liAt < lbKeep.Length && liAt < lbBackdrop.Length; liAt++)
                lbKeep[liAt] |= !lbBackdrop[liAt];
        }

        return lbKeep;
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
