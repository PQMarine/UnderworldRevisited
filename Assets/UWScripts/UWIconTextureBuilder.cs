using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// Builds icon textures for inventory displays (backpack slots, drag icon, containers,
/// scroll arrows) from a UWTexture. Previously an additional 1px black border was drawn
/// here around every opaque pixel (outline fill), because the original icons visibly
/// have an opaque black border - but that actually already came from the raw data itself
/// and was only swallowed by a bug in the alpha detection
/// (UWPalette wrongly took "red channel == 0" as the only
/// transparency marker, which also made real black outline pixels transparent - see the
/// UWPalette.cs constructor, now corrected to the actual marker: dark blue,
/// red=green=0 and blue&gt;0). With a correct alpha source every icon already carries its
/// border itself - the additional outline fill would only thicken it needlessly (confirmed
/// by user) and has therefore been removed; now just a plain 1:1 conversion.
/// </summary>
public static class UWIconTextureBuilder
{
    /// <summary>
    /// Builds the icon texture - PALETTE INDICES in the red channel, opacity in the alpha channel.
    ///
    /// The colour is only produced in the shader UW/IconPalette, via the same
    /// lookup textures as the world. That way the palette rotation also cycles here -
    /// flames on torches, candles and the light spell icon flicker without having to
    /// pre-bake all steps for every icon (UWCycledIcon used to do that, and
    /// only in two of ten places).
    ///
    /// AUXILIARY PALETTE: object sprites from OBJECTS.GR are 4-bit and go through an
    /// auxiliary palette, so their pixels hold values from 0 to 15. They first have to be
    /// mapped back to the main palette (see UWTexture.GetMainPaletteIndices),
    /// otherwise the shader shows completely wrong colours.
    ///
    /// The opacity still comes from the resolved colour: the palette's transparency marker
    /// is a very dark blue, and whether a pixel carries it cannot be told from the
    /// index alone (see UWPalette).
    ///
    /// LINEAR, not sRGB - otherwise Unity applies a gamma curve to the index. In the
    /// gamma colour space the project runs in, this has no effect; it only matters if
    /// the project is ever switched to linear.
    ///
    /// Since 2026-10-19 (cleanup step 5.4) the pixels are made engine-free
    /// (UWUploadPixels.IconIndices) and uploaded by UWTextureFactory; every icon shown is made
    /// once and shared by UWIconCache, which owns it.
    /// </summary>
    public static Texture2D Build(UWTexture pOSource, FilterMode peFilterMode)
    {
        return UWTextureFactory.FromIndices(pOSource, peFilterMode, "UWIconTextureBuilder icon");
    }
}
