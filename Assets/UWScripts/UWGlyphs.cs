using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// INPUT GLYPHS: Kenney's "Input Prompts Pixel" (CC0, ThirdParty/Kenney), 16 by 16 pixel art that
/// suits the game (per user, 2026-10-08). The whole sheet is kept as PNG bytes in Resources/UWGlyphs
/// and cut here, so no texture import can blur or compress it; a glyph is drawn enlarged by whole
/// pixels. THE PLAYSTATION SHAPES come in two halves on two tiles each (likely for Sony's mark) and
/// are laid over each other here, as the sheet means them.
/// </summary>
public static class UWGlyphs
{
    private const string SheetName = "UWGlyphs/KenneyInputPromptsPixel";

    private const int TileSize = 16;

    /// <summary>The sheet's tiles per row (34 by 24, no spacing in the packed sheet).</summary>
    private const int SheetColumns = 34;

    /// <summary>The mouse with its right button lit.</summary>
    public const int MouseRightTile = 78;

    /// <summary>The sticks with all four directions, and pressed (the L and R on them suit every style).</summary>
    public const int LeftStickTile = 219;

    public const int RightStickTile = 287;

    /// <summary>The d-pad up and down, left and right together.</summary>
    public const int DpadVerticalTile = 40;

    public const int DpadHorizontalTile = 39;

    /// <summary>The orange double arrows: left-right and four ways (the layout editor's pointers,
    /// per user 2026-10-10).</summary>
    public const int ArrowLeftRightTile = 611;

    public const int ArrowFourWaysTile = 679;

    /// <summary>The controls by their place on the pad, as UWGamepad names them.</summary>
    private static readonly string[] msControls =
    {
        "buttonSouth", "buttonEast", "buttonWest", "buttonNorth", "leftShoulder", "rightShoulder",
        "leftTrigger", "rightTrigger", "leftStickPress", "rightStickPress", "start", "select",
        "dpad/up", "dpad/down", "dpad/left", "dpad/right"
    };

    /// <summary>Their tiles per style (UWGamepad.ButtonStyleEnum), in the order above; several tiles
    /// are laid over each other. Nintendo's face buttons are swapped against the place.</summary>
    private static readonly int[][][] msTiles =
    {
        // Xbox: the coloured A B X Y, LB RB LT RT, the stick presses, Menu and View, the d-pad.
        new[]
        {
            new[] { 4 }, new[] { 5 }, new[] { 6 }, new[] { 7 }, new[] { 553 }, new[] { 554 },
            new[] { 555 }, new[] { 556 }, new[] { 220 }, new[] { 288 }, new[] { 617 }, new[] { 616 },
            new[] { 35 }, new[] { 37 }, new[] { 38 }, new[] { 36 }
        },
        // PlayStation: cross, circle, square, triangle in halves; L1 R1 L2 R2; Options, Share.
        new[]
        {
            new[] { 567, 568 }, new[] { 563, 564 }, new[] { 565, 566 }, new[] { 561, 562 }, new[] { 631 }, new[] { 632 },
            new[] { 629 }, new[] { 630 }, new[] { 220 }, new[] { 288 }, new[] { 617 }, new[] { 618 },
            new[] { 35 }, new[] { 37 }, new[] { 38 }, new[] { 36 }
        },
        // Nintendo: B A Y X by place; L R ZL ZR; + and -.
        new[]
        {
            new[] { 14 }, new[] { 13 }, new[] { 16 }, new[] { 15 }, new[] { 627 }, new[] { 628 },
            new[] { 625 }, new[] { 626 }, new[] { 220 }, new[] { 288 }, new[] { 685 }, new[] { 684 },
            new[] { 35 }, new[] { 37 }, new[] { 38 }, new[] { 36 }
        }
    };

    private static Texture2D msSheet;

    private static bool msbSheetTried;

    private static readonly Dictionary<string, Texture2D> msCache = new Dictionary<string, Texture2D>();

    /// <summary>The mouse's right button (the game menu's pointer option).</summary>
    public static Texture2D MouseRight => Tiles(MouseRightTile);

    /// <summary>A binding path's glyph in the chosen button style ("&lt;Gamepad&gt;/buttonSouth" is
    /// A, the cross or B), null for a control without one.</summary>
    public static Texture2D ForPath(string psPath)
    {
        if (string.IsNullOrEmpty(psPath))
            return null;

        const string Prefix = "<Gamepad>/";
        string lsControl = psPath.StartsWith(Prefix) ? psPath.Substring(Prefix.Length) : psPath;
        int liAt = System.Array.IndexOf(msControls, lsControl);

        return liAt < 0 ? null : Tiles(msTiles[(int)UWGamepad.ButtonStyle][liAt]);
    }

    /// <summary>The binding path of a gamepad entry as it is now (UWKeyBindings.PadEntries), or null.</summary>
    public static string PadPath(string psAction, int piBinding)
    {
        foreach (UWKeyBindings.Entry lOEntry in UWKeyBindings.PadEntries)
        {
            if (lOEntry.Action == psAction && lOEntry.Binding == piBinding)
                return UWKeyBindings.GetPath(lOEntry);
        }

        return null;
    }

    /// <summary>The glyph of a gamepad entry's button as it is bound now.</summary>
    public static Texture2D ForEntry(string psAction, int piBinding)
    {
        return ForPath(PadPath(psAction, piBinding));
    }

    /// <summary>Tiles laid over each other, twice their size with sharp pixels; null if the sheet
    /// is missing.</summary>
    public static Texture2D Tiles(params int[] piTiles)
    {
        if (piTiles == null || piTiles.Length == 0)
            return null;

        string lsKey = string.Join(",", piTiles);

        if (msCache.TryGetValue(lsKey, out Texture2D lOCached) && lOCached != null)
            return lOCached;

        Texture2D lOGlyph = fBuild(piTiles, lsKey, false, 2);

        if (lOGlyph != null)
            msCache[lsKey] = lOGlyph;

        return lOGlyph;
    }

    /// <summary>A tile as a mouse pointer at piScale times its size (as the game's own pointers,
    /// UWGameUI), readable as Cursor.SetCursor needs it; null if the sheet is missing.</summary>
    public static Texture2D Pointer(int piTile, int piScale)
    {
        string lsKey = "pointer " + piTile + " x" + piScale;

        if (msCache.TryGetValue(lsKey, out Texture2D lOCached) && lOCached != null)
            return lOCached;

        Texture2D lOPointer = fBuild(new[] { piTile }, lsKey, true, Mathf.Max(1, piScale));

        if (lOPointer != null)
            msCache[lsKey] = lOPointer;

        return lOPointer;
    }

    private static Texture2D fBuild(int[] piTiles, string psKey, bool pbReadable, int piScale)
    {
        Texture2D lOSheet = fSheet();

        if (lOSheet == null)
            return null;

        int liSize = TileSize * piScale;
        Color32[] lySheet = lOSheet.GetPixels32();
        Color32[] lyGlyph = new Color32[liSize * liSize];

        foreach (int liTile in piTiles)
        {
            // The sheet is read bottom up; its tiles are numbered from the top left.
            int liLeft = (liTile % SheetColumns) * TileSize;
            int liBottom = lOSheet.height - ((liTile / SheetColumns) + 1) * TileSize;

            for (int y = 0; y < liSize; y++)
            {
                for (int x = 0; x < liSize; x++)
                {
                    Color32 lOPixel = lySheet[((liBottom + (y / piScale)) * lOSheet.width) + liLeft + (x / piScale)];

                    if (lOPixel.a > 0)
                        lyGlyph[(y * liSize) + x] = lOPixel;
                }
            }
        }

        Texture2D lOGlyph = new Texture2D(liSize, liSize, TextureFormat.RGBA32, false)
        {
            name = "UWGlyphs " + psKey,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        lOGlyph.SetPixels32(lyGlyph);
        lOGlyph.Apply(false, !pbReadable);

        return lOGlyph;
    }

    private static Texture2D fSheet()
    {
        if (msSheet != null || msbSheetTried)
            return msSheet;

        msbSheetTried = true;

        TextAsset lOBytes = Resources.Load<TextAsset>(SheetName);

        if (lOBytes == null)
            return null;

        Texture2D lOSheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!lOSheet.LoadImage(lOBytes.bytes))
        {
            Object.Destroy(lOSheet);
            return null;
        }

        msSheet = lOSheet;

        return msSheet;
    }
}
