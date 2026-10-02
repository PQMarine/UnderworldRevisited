// CORE OF THE PALETTE RENDERER. Included by the palette shaders UW/DungeonPalette,
// UW/ModelPalette, UW/DecalPalette and UW/BillboardPalette. The palette path is the default;
// F6 switches back to the URP lighting for comparison (see UWPaletteRenderToggle).
//
// The shared lookup chain of all palette shaders. It replaces URP lighting
// completely - there is no point light, no normal vector, no shadow casting.
// The original knows none of that: the brightness of a pixel depends only on the
// player's light level and the distance to the camera.
//
// Steps per pixel:
//
//   1. Read the PALETTE INDEX from the source texture (red channel, 0 to 255) and the opacity
//      (alpha channel).
//   2. Look up in the rotation table the index that takes its place in this rotation
//      step - this is how water and lava move.
//   3. Convert the distance IN THE PLANE to tiles and fetch the shade level from the
//      shade table.
//   4. Fetch the final colour from the colour table at (index, level).
//
// For an explanation of the tables see UWShadePalette.
//
// DISTANCE IN THE PLANE ONLY
//
// The original is a tile renderer: it shades by the distance between TILES, height
// does not enter into it. Measuring in 3D space instead adds the eye height -
// with a tile width of 64 units and an eye height of about one tile, that pushes a
// floor point right in front of the feet out to one and a half tiles and the top edge of a
// wall by a whole one. That is exactly what was visible: the image was about one tile too
// dark everywhere (per user, compared with the original, 2026-09-05).
//
// ALL textures here must be point filtered, and all except the colour table linear -
// otherwise a gamma corrected in-between colour comes out instead of an index.
//
// AT THE SCREEN'S RESOLUTION, NOT 320 BY 200 - A DELIBERATE DEVIATION (per user, 2026-09-27;
// Todo.md section 8, "Far objects are drawn fine, not coarse"): the original draws into a 320
// by 200 frame, so far sprites turn into a few coarse blocks there; this renderer keeps the
// colours, the shading and the dithering cell of the original's pixel, but not its resolution.

#ifndef UW_PALETTE_LOOKUP_INCLUDED
#define UW_PALETTE_LOOKUP_INCLUDED

// The world's colour table (UWShadePalette.WorldColourTableProperty): _UWColourTable, or at
// Night Vision the one built from MONO.DAT. The interface reads _UWColourTable itself.
TEXTURE2D(_UWWorldColourTable);
SAMPLER(sampler_UWWorldColourTable);

TEXTURE2D(_UWShadeTable);
SAMPLER(sampler_UWShadeTable);

TEXTURE2D(_UWRotationTable);
SAMPLER(sampler_UWRotationTable);

// Set per frame by UWPaletteRenderToggle (via UWShadePalette.ApplyGlobals). _UWLightLevel is
// the light level 0 to 7, _UWRotationStep the current step of the palette rotation,
// _UWRotationStepCount the number of rotation steps, _UWTileSpacing the edge length
// of a tile in world units and _UWCutoffDistance the view distance at this light level.
float _UWLightLevel;
float _UWRotationStep;
float _UWRotationStepCount;
float _UWTileSpacing;
float _UWCutoffDistance;

// 1 = in-between levels between two tile distances, 0 = the level of the whole tile.
float _UWInterpolateLevels;

// 1 = the in-between level is dithered, 0 = rounded.
float _UWDither;

// 0 = checkerboard of 2 by 2, 1 = ordered matrix of 4 by 4.
float _UWDitherPattern;

// How many screen pixels wide one cell of the pattern is - one ORIGINAL PIXEL.
float _UWDitherScale;

// Everything at shade level zero - the original colours, without any darkening. For
// testing only: this also shows what would otherwise lie in the dark.
float _UWFullBright;

#define UW_PALETTE_SIZE     256.0
#define UW_SHADE_LEVELS      16.0
#define UW_DISTANCE_COUNT    16.0
#define UW_LIGHT_LEVELS       8.0

// The texel centre, so that point filtering does not land on the border between two
// entries.
float2 UWTableUV(float pfColumn, float pfRow, float pfWidth, float pfHeight)
{
    return float2((pfColumn + 0.5) / pfWidth, (pfRow + 0.5) / pfHeight);
}

// Turn a channel value 0..1 back into the integer 0..255.
float UWToIndex(float pfChannel)
{
    return floor((pfChannel * 255.0) + 0.5);
}

// Step 2: the rotated index.
float UWRotateIndex(float pfIndex)
{
    float2 lOUV = UWTableUV(pfIndex, _UWRotationStep, UW_PALETTE_SIZE, _UWRotationStepCount);

    return UWToIndex(SAMPLE_TEXTURE2D(_UWRotationTable, sampler_UWRotationTable, lOUV).r);
}

// The shade level at a whole tile distance.
float UWShadeLevelAt(float pfDistance)
{
    float2 lOUV = UWTableUV(pfDistance, _UWLightLevel, UW_DISTANCE_COUNT, UW_LIGHT_LEVELS);

    return UWToIndex(SAMPLE_TEXTURE2D(_UWShadeTable, sampler_UWShadeTable, lOUV).r);
}

// Step 3: the shade level at an arbitrary tile distance.
//
// The table only knows whole tile distances, and its jumps are large - at light level
// 4 roughly 0, 4, 9, 14. Used raw, this produces hard-edged rings around the player.
// In the original the transitions on the floor are softer, so it also uses the
// in-between levels.
//
// What is interpolated is the LEVEL, not the colour. The difference matters: a
// mixed colour is not in the palette and would be impossible in the original, an
// in-between level is not.
float UWShadeLevel(float pfTileDistance)
{
    float lfDistance = clamp(pfTileDistance, 0.0, UW_DISTANCE_COUNT - 1.0);
    float lfLow = floor(lfDistance);

    float lfLevel = UWShadeLevelAt(lfLow);

    if (_UWInterpolateLevels > 0.5)
    {
        float lfHigh = min(lfLow + 1.0, UW_DISTANCE_COUNT - 1.0);

        lfLevel = lerp(lfLevel, UWShadeLevelAt(lfHigh), frac(lfDistance));
    }

    // Deliberately NOT rounded - UWShadeIndex decides that, by dithering or by
    // rounding.
    return clamp(lfLevel, 0.0, UW_SHADE_LEVELS - 1.0);
}

// The dither threshold for a pixel. The value lies between 0 and 1 and depends only
// on the position of the pixel on the SCREEN.
//
// TWO PATTERNS to choose from:
//
//   Checkerboard two thresholds, 0.25 and 0.75. This gives exactly ONE in-between level:
//                below a quarter entirely the lower level, above three quarters entirely the
//                upper one, in between half and half. The user took a close-up
//                of the original and sees a clean checkerboard pattern there, finer than
//                texels (2026-09-05) - that matches this and not a finer
//                matrix.
//
//   Matrix       sixteen thresholds over 4 by 4. Gives softer gradations, but looks
//                grainy instead of checkerboard-like. For comparison.
//
// That the dithering depends on the SCREEN position and not on the surface is shown by
// the same close-up: the pattern there is finer than a texel. If it were baked into
// the texture or computed per texel, that could not be the case.
//
// ONE CELL IS ONE ORIGINAL PIXEL, not one screen pixel. The original draws at
// 320 by 200; here one of its pixels covers a multiple of that depending on the window
// size. A pattern of single screen pixels would be too fine by that factor and
// would just flicker when moving.
//
// The user measured cells of 4x4, 5x5, 2x3 and 1x4 pixels in the original, plus
// skipped pixels (2026-09-05). That is not the game but scaling by a
// non-integer factor: at about 5.4x, nearest neighbour repeats some source pixels
// five times, some six times, and drops some entirely. In the game itself the pattern is
// regular.
float UWDitherThreshold(float2 pOScreenPosition)
{
    float2 lOCell = floor(pOScreenPosition / max(_UWDitherScale, 1.0));

    if (_UWDitherPattern < 0.5)
    {
        float lfCheck = fmod(lOCell.x + lOCell.y, 2.0);

        return lfCheck < 0.5 ? 0.25 : 0.75;
    }

    const float lfMatrix[16] =
    {
         0.0,  8.0,  2.0, 10.0,
        12.0,  4.0, 14.0,  6.0,
         3.0, 11.0,  1.0,  9.0,
        15.0,  7.0, 13.0,  5.0
    };

    int liX = (int)fmod(lOCell.x, 4.0);
    int liY = (int)fmod(lOCell.y, 4.0);

    return (lfMatrix[(liY * 4) + liX] + 0.5) / 16.0;
}

// Step 4: the colour for index and level.
//
// pfExtraLevel is an additional darkening that does not come from distance. It
// is needed for the flat-coloured faces of the 3D models: chests and the like carry
// their own brightness value per vertex (node 00D4, see UW3DModel.VertexDarkValues),
// which varies across the face and gives it its contour.
//
// ADDED, not replaced: a chest in the distance is darker than one at your feet, but its
// contour stays the same. Both are darkenings and add up. The user
// observed in the original that the shading of these faces looks dithered exactly
// like that of the walls (2026-09-05) - so it is the same calculation, just with an
// additional term, and not a separate colouring.
//
// DITHER INSTEAD OF ROUNDING. An 8-bit renderer cannot paint an in-between brightness, it has
// only the sixteen levels from LIGHT.DAT. To land in between anyway, the
// original spreads the two neighbouring levels across the surface in a checkerboard. This explains two
// observations of the original at once (per user, 2026-09-05): the soft transitions
// on the floor and the rougher look of the textures - it is one effect, not two.
//
// The threshold depends on the position on the SCREEN, not on the surface. The pattern
// is thus fixed in the image and shifts when you move - just like in a
// software renderer that sets it while drawing the scanline.
//
// Which pattern exactly is decided by _UWDitherPattern - see UWDitherThreshold.
//
// THE HALLUCINATION'S SECOND LIGHT TABLE (UWHallucinationState.LightTableEffect): while it
// runs, the colour of an index at a level comes from _UWLightEffectTable instead - what the
// original's copy of UW.EXE's segment 51 over LIGHT.DAT maps it to (UWShadePalette.
// BuildLightTableEffect, an approximation): bands of dots by distance. Every sprite, wall and
// model face drawn here takes part, as in the original; the interface does not pass here, as
// it does not pass the original's light table. The table has three parts - everything else,
// floors, walls -, which a shader picks by setting UWSurfacePart before shading (the dungeon
// shader does; UWHallucinationState.OtherPart, FloorPart, WallPart).
TEXTURE2D(_UWLightEffectTable);
SAMPLER(sampler_UWLightEffectTable);

// The same table in raw colours, for Remastered (UWHallucination.hlsl), which puts the colour
// help on in its post-processing; a shader picks it by setting UWRawColours before shading.
// Sampled with URP's shared point sampler, to spare the URP shaders a sampler slot.
TEXTURE2D(_UWRemasterLightEffect);

float _UWLightEffect;

static float UWSurfacePart = 0.0;

static float UWRawColours = 0.0;

half4 UWShadeLookup(float pfIndex, float pfLevel)
{
    float2 lOUV = UWTableUV(pfIndex, pfLevel, UW_PALETTE_SIZE, UW_SHADE_LEVELS);

    // One result and one return: returns in nested branches made the compiler warn about an
    // uninitialised value.
    half4 lOColour = SAMPLE_TEXTURE2D(_UWWorldColourTable, sampler_UWWorldColourTable, lOUV);

    if (_UWLightEffect > 0.5)
    {
        float2 lOEffectUV = UWTableUV(pfIndex, pfLevel + (UWSurfacePart * UW_SHADE_LEVELS),
            UW_PALETTE_SIZE, 3.0 * UW_SHADE_LEVELS);

        lOColour = UWRawColours > 0.5
            ? SAMPLE_TEXTURE2D(_UWRemasterLightEffect, sampler_PointClamp, lOEffectUV)
            : SAMPLE_TEXTURE2D(_UWLightEffectTable, sampler_UWLightEffectTable, lOEffectUV);
    }

    return lOColour;
}

half4 UWShadeIndex(float pfIndex, float pfTileDistance, float pfExtraLevel, float2 pOScreenPosition)
{
    // FULL BRIGHT: level zero, i.e. the pure palette colours. Distance, vertex darkness
    // and dithering all drop out together - dithering only happens between two levels anyway.
    if (_UWFullBright > 0.5)
        return UWShadeLookup(pfIndex, 0.0);

    float lfLevel = UWShadeLevel(pfTileDistance) + pfExtraLevel;

    if (_UWDither > 0.5)
    {
        lfLevel = floor(lfLevel) + step(UWDitherThreshold(pOScreenPosition), frac(lfLevel));
    }
    else
    {
        lfLevel = floor(lfLevel + 0.5);
    }

    lfLevel = clamp(lfLevel, 0.0, UW_SHADE_LEVELS - 1.0);

    return UWShadeLookup(pfIndex, lfLevel);
}

// The distance used for shading: straight-line distance in the PLANE, ignoring height difference.
float UWTileDistance(float3 pOPositionWS, float3 pOCameraWS)
{
    return distance(pOPositionWS.xz, pOCameraWS.xz) / max(_UWTileSpacing, 0.0001);
}

// The whole chain in one call, with an additional darkening per vertex (UWPaletteColour
// is the same without it).
//
// THE OPACITY COMES FROM THE SOURCE TEXTURE, not from the colour table. Reason: which
// pixel is transparent is decided differently depending on the source, and the
// colour table only knows the index.
//
//   Walls and floors   always opaque.
//   Object sprites   RAW INDEX ZERO is transparent, i.e. the value before
//                     conversion through the auxiliary palette (see UWObjectAtlasBuilder,
//                     lbIndexZero). After conversion this would no longer be recognisable,
//                     because auxiliary index 0 points to a perfectly ordinary main index.
//   Critter sprites   the pixel whose auxiliary palette entry points to MAIN index 0
//                     (see UWCritterAtlasBuilder, TransparentIndex).
//   Icons and text   the palette's transparency marker, a very dark blue
//                     (see UWPalette).
//   Mist and ghosts   partial opacity from XFER.DAT (see UWTransparencyTables).
//
// Whoever builds the index texture therefore puts the opacity into the alpha channel by
// their own rule, and here the same applies to all.
half4 UWPaletteColourExtra(float4 pORawTexel, float pfExtraLevel, float3 pOPositionWS,
    float3 pOCameraWS, float2 pOScreenPosition)
{
    float lfIndex = UWRotateIndex(UWToIndex(pORawTexel.r));

    half4 lOColour = UWShadeIndex(lfIndex, UWTileDistance(pOPositionWS, pOCameraWS),
        pfExtraLevel, pOScreenPosition);

    return half4(lOColour.rgb, pORawTexel.a);
}

// For faces whose palette index is not in a texture but attached as a number to the
// vertex - the flat-coloured faces of the 3D models. Within a face the
// index is the same everywhere, so interpolation between the vertices does no harm;
// the DARKENING on the other hand varies, and is meant to.
half4 UWPaletteColourIndexed(float pfIndex, float pfExtraLevel, float3 pOPositionWS,
    float3 pOCameraWS, float2 pOScreenPosition)
{
    float lfIndex = UWRotateIndex(floor(pfIndex + 0.5));

    half4 lOColour = UWShadeIndex(lfIndex, UWTileDistance(pOPositionWS, pOCameraWS),
        pfExtraLevel, pOScreenPosition);

    return half4(lOColour.rgb, 1.0h);
}

// XFER.DAT AS THE ORIGINAL APPLIES IT (audit row 12, 2026-09-27). The fog cloud, the smoke
// and the ghosts are made of indices that do not paint a colour but replace the pixel BEHIND
// them through a table (UWTransparencyTables). The original's frame holds indices; ours holds
// colours - but in this renderer every one of them is a palette colour, so the cube
// _UWInverseLookup (six bits a channel) turns it back into its index, and _UWXferColours holds
// for every table (row) and background index (column) the colour the table makes of it. No
// shading on top: the background is already shaded, and so is the table's result.
//
// The index atlas marks such a pixel in its GREEN channel: the table number plus one (see
// UWObjectAtlasBuilder and UWCritterAtlasBuilder); its red and alpha keep the old fixed blend,
// which is used while _UWXferReady is off (the hallucination's palette).
TEXTURE3D(_UWInverseLookup);
SAMPLER(sampler_UWInverseLookup);

TEXTURE2D(_UWXferColours);
SAMPLER(sampler_UWXferColours);

float _UWXferReady;

#define UW_INVERSE_SIZE   64.0
#define UW_XFER_TABLES     6.0

// The palette index that asks for table 0 (UWTransparencyTables.FadeToShadowIndex, 0xFB); the
// tables follow in ascending order.
#define UW_XFER_FIRST_MARKER 251.0

// The table a texel asks for, or -1 for an ordinary pixel.
float UWXferTable(float4 pORawTexel)
{
    return _UWXferReady > 0.5 ? UWToIndex(pORawTexel.g) - 1.0 : -1.0;
}

half3 UWXferColour(float pfTable, half3 pOBackground)
{
    float3 lOCell = floor(saturate(pOBackground) * (UW_INVERSE_SIZE - 1.0) + 0.5);
    float lfIndex = UWToIndex(SAMPLE_TEXTURE3D(_UWInverseLookup, sampler_UWInverseLookup,
        (lOCell + 0.5) / UW_INVERSE_SIZE).r);

    return SAMPLE_TEXTURE2D(_UWXferColours, sampler_UWXferColours,
        UWTableUV(lfIndex, pfTable, UW_PALETTE_SIZE, UW_XFER_TABLES)).rgb;
}

// The usual path: index from the texture, no additional darkening.
half4 UWPaletteColour(float4 pORawTexel, float3 pOPositionWS, float3 pOCameraWS,
    float2 pOScreenPosition)
{
    return UWPaletteColourExtra(pORawTexel, 0.0, pOPositionWS, pOCameraWS, pOScreenPosition);
}

#endif
