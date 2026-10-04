// THE HALLUCINATION'S PICTURE (UWHallucinationState) for the shaders that draw the 3D view:
// the scrambled texture mapper of the walls, floors and ceilings (both render paths), and the
// three effects in Remastered (UW/Dungeon, UW/Billboard, UW/Decal).
//
// IN THE PALETTE PATH the effects fall out of the lookup chain itself (UWPaletteLookup.hlsl): the
// colour table is built from the hallucination's palette, the light table is swapped. REMASTERED
// draws colours and lights them with URP, so there the three were missing (per user,
// 2026-09-27). Every Remastered surface knows its palette index, though - the walls from the
// index array UWRemasterRenderer sets, the sprites and decals from the index atlas of their
// atlas (_IndexTex), the model faces from their vertex - and with it:
//
//   0  the scrambled mapper: the same texel scrambling as UW/DungeonPalette, on the colour, the
//      normals and the index alike;
//   1  the other palette: the index's colour in the hallucination's palette (_UWRemasterPalettes,
//      raw), which then is lit as usual;
//   2  the light table: the surface is drawn as the palette path draws it - its index through
//      the effect table at the shade level of its distance, unlit. The effect blackens nearly
//      everything; torchlight on top would only light the black.
//
// RAW COLOURS: Remastered puts the colour help on in its post-processing (UWRemasterRenderer),
// so its tables are built without it (UWShadePalette, pbColourHelp false).
//
// THE GHOSTS (XFER.DAT, audit row 12) are applied here as well: in Remastered they were drawn with
// one fixed blend fitted over the whole palette (UWTransparencyTables.TryGetBlend), which the
// lighting darkened further - nearly invisible (per user, 2026-09-27). Now the translucent pixel
// takes the background's nearest palette index and replaces it through its table, as the
// palette path does (UWBillboardPalette). Remastered's lit colours are no palette colours; the
// nearest one keeps their brightness, and the table's result is taken as it is.

#ifndef UW_HALLUCINATION_INCLUDED
#define UW_HALLUCINATION_INCLUDED

#include "UWPaletteLookup.hlsl"

// THE HALLUCINATION'S SCRAMBLED TEXTURE MAPPER (UWHallucinationState.ScrambleEffect,
// seg031_99): the original forms a texel index from the row position times the
// width - its fraction in the low bits - and the column, through a mask that
// normally keeps only the row bits (0x3E0 for 32, 0xFC0 for 64 pixels). While the
// effect runs the mask is random, the fraction leaks into the column, and the
// surface turns into swirling bands of its own colours (per user with screenshots of
// the original, 2026-09-27). x = the mask of the 64 pixel walls, y = of the 32 pixel
// floors and ceilings, z = on. The slices after walls and floors are colour swatches.
float4 _UWScramble;
float _UWWallSlices;
float _UWFloorSlices;

bool UWScrambleActive()
{
    return _UWScramble.z > 0.5;
}

float2 UWScrambleUV(float3 pOUV)
{
    if (_UWScramble.z < 0.5 || pOUV.z >= _UWWallSlices + _UWFloorSlices)
        return pOUV.xy;

    bool lbWall = pOUV.z < _UWWallSlices;
    uint liSize = lbWall ? 64u : 32u;
    uint liMask = (uint)(lbWall ? _UWScramble.x : _UWScramble.y);

    // The slice holds the texture upside down (UWTextureArrayBuilder): row 0 of the
    // original is at the top, v = 1.
    float2 lOIn = frac(pOUV.xy);
    uint liColumn = min((uint)(lOIn.x * liSize), liSize - 1u);
    uint liRowTimesSize = min((uint)((1.0 - lOIn.y) * liSize * liSize), liSize * liSize - 1u);

    uint liTexel = ((liRowTimesSize & liMask) + liColumn) & (liSize * liSize - 1u);

    float lfColumn = (float)(liTexel % liSize);
    float lfRow = (float)(liTexel / liSize);

    return float2((lfColumn + 0.5) / liSize, 1.0 - ((lfRow + 0.5) / liSize));
}

// The part of the hallucination's light table: 2 walls, 1 a floor slice below the eye
// (ceilings share the floor slices), 0 anything else.
float UWDungeonSurfacePart(float pfSlice, float pfHeightWS)
{
    return pfSlice < _UWWallSlices ? 2.0
        : ((pfSlice < _UWWallSlices + _UWFloorSlices && pfHeightWS < _WorldSpaceCameraPos.y) ? 1.0 : 0.0);
}

// ------------------------------------------------------------------ Remastered
//
// Set per frame by UWPaletteRenderToggle. _UWHallucinationPalette is the row of the palette
// effect, 0 while it is off (palette 0 is the ordinary one); _UWRemasterXfer is on while the
// raw XFER lookups are ready and no other palette is on screen (the cube knows palette 0 only).
// Sampled with URP's shared point sampler, to spare these shaders sampler slots.
TEXTURE2D(_UWRemasterPalettes);
TEXTURE3D(_UWRemasterInverseLookup);
TEXTURE2D(_UWRemasterXferColours);

float _UWHallucinationPalette;
float _UWRemasterXfer;

#define UW_RAW_PALETTES 8.0

bool UWRemasterPaletteSwapped()
{
    return _UWHallucinationPalette > 0.5;
}

bool UWRemasterLightEffect()
{
    return _UWLightEffect > 0.5;
}

// The colour of an index (0 to 255, as stored) in the hallucination's palette, raw. Rotated
// like the palette path's, so water and lava keep moving.
half3 UWRemasterPaletteColour(float pfIndex)
{
    float2 lOUV = UWTableUV(UWRotateIndex(pfIndex), _UWHallucinationPalette, UW_PALETTE_SIZE, UW_RAW_PALETTES);

    return SAMPLE_TEXTURE2D(_UWRemasterPalettes, sampler_PointClamp, lOUV).rgb;
}

// Lava and fire (16 to 23) and water (48 to 63) travel through the palette (UWPaletteRotation).
bool UWIsRotatingIndex(float pfIndex)
{
    return (pfIndex > 15.5 && pfIndex < 23.5) || (pfIndex > 47.5 && pfIndex < 63.5);
}

// An index as the light table effect shows it at this spot: its shade level from the distance
// in the plane (with pfExtraLevel on top, the model faces' own darkening), dithered like the
// palette path's, the colour from the effect table's part pfPart.
half3 UWRemasterLightEffectColour(float pfIndex, float pfPart, float3 pOPositionWS,
    float pfExtraLevel, float2 pOScreenPosition)
{
    UWRawColours = 1.0;
    UWSurfacePart = pfPart;

    return UWShadeIndex(UWRotateIndex(pfIndex), UWTileDistance(pOPositionWS, _WorldSpaceCameraPos),
        pfExtraLevel, pOScreenPosition).rgb;
}

// A translucent marker under the light table effect: LIGHT.DAT keeps 248 to 255 at every level,
// so the original looks the marker up first - the effect's table sends it to black or to one of
// its dots, and the ghost vanishes, as in UWBillboardPalette.
half3 UWRemasterXferUnderLightEffect(float pfTable, float3 pOPivotWS, float2 pOScreenPosition)
{
    UWRawColours = 1.0;
    UWSurfacePart = 0.0;

    return UWShadeIndex(UW_XFER_FIRST_MARKER + pfTable, UWTileDistance(pOPivotWS, _WorldSpaceCameraPos),
        0.0, pOScreenPosition).rgb;
}

bool UWRemasterXferReady()
{
    return _UWRemasterXfer > 0.5;
}

// The table a texel of an index atlas asks for, or -1 for an ordinary pixel (the green channel
// holds the table plus one, see UWObjectAtlasBuilder and UWCritterAtlasBuilder).
float UWRemasterXferTable(float4 pOIndexTexel)
{
    return UWToIndex(pOIndexTexel.g) - 1.0;
}

// The background's nearest palette index through the table, raw.
half3 UWRemasterXferColour(float pfTable, half3 pOBackground)
{
    float3 lOCell = floor(saturate(pOBackground) * (UW_INVERSE_SIZE - 1.0) + 0.5);
    float lfIndex = UWToIndex(SAMPLE_TEXTURE3D(_UWRemasterInverseLookup, sampler_PointClamp,
        (lOCell + 0.5) / UW_INVERSE_SIZE).r);

    return SAMPLE_TEXTURE2D(_UWRemasterXferColours, sampler_PointClamp,
        UWTableUV(lfIndex, pfTable, UW_PALETTE_SIZE, UW_XFER_TABLES)).rgb;
}

#endif
