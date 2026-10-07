// THE PALETTE RENDERER'S OWN EFFECTS (UWPaletteEffects.cs, per user 2026-10-07: "alle
// Grafikeffekte die Sinn machen im Palette Renderer"). Not the original's - it has none of them -
// and each one is off unless the player turns it on.
//
// ONLY THE SHADE LEVEL MOVES. The palette path has no colours to compute with: the textures hold
// indices, the light picks one of sixteen levels, the colour table gives the colour
// (UWPaletteLookup.hlsl). An effect therefore adds or takes away LEVELS before that lookup, and
// UWShadeIndex dithers between the two neighbouring levels - every pixel stays a colour of the
// palette. What computes with finished colours (bloom, specular highlights, tone mapping) has no
// place here.
//
// The result goes into the extra level the shaders already pass (the 3D models' vertex darkness
// uses it too): positive darkens, negative brightens.
#ifndef UW_PALETTE_EFFECTS_INCLUDED
#define UW_PALETTE_EFFECTS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"
#include "UWOwnTile.hlsl"
#include "UWGrime.hlsl"

// AMBIENT OCCLUSION: URP's screen-space occlusion (the renderer's feature, computed from the
// depth prepass the palette path draws anyway), turned into levels - this many at full
// occlusion. 0 = off; then the feature does not run and its texture is not read.
float _UWPaletteAO;

// LIGHT SOURCES (UWPaletteLightMap): a map of the level, four texels a tile, holding the shade
// level the sources give (times 16, 255 none) - each one falls off by the player's own SHADES.DAT
// table and walls shadow it.
//
// HOW FAR: first only within the player's own view distance (_UWCutoffDistance; per user the same
// day, a source should not let one see further than SHADES.DAT allows) - but then a fire near the
// edge of that distance lost its own surroundings and its glow lay on the floor in front of the
// player ("wirkt immernoch unnatuerlich", per user with a screenshot). Now the limit is the reach
// of the original's view sweep, 16 tiles (UWRenderSweep.Rows): within it the sweep mask shows the
// sprites properly (UWSweepMask), so a fire and its glow belong together; beyond it the mask hides
// the sprites, so the light runs out there too. A deliberate deviation: lit places beyond the
// player's own light show, which the original draws black (and does not put on the map).
#define UW_LIGHT_SOURCE_REACH_TILES 16.0
TEXTURE2D(_UWPaletteLightMap);
SAMPLER(sampler_UWPaletteLightMap);
float _UWPaletteLights;

// The level the sources give at a point; pNormalWS moves the sample off a wall into the open
// tile in front of it (a wall lies on the border of a solid tile, whose texels stay dark).
float UWPaletteLightCap(float3 pPositionWS, float3 pNormalWS)
{
    if (_UWPaletteLights < 0.5 || _UWTileSpacing <= 0.0)
        return 15.0;

    float lfDistance = distance(pPositionWS.xz, _WorldSpaceCameraPos.xz);
    float lfReach = UW_LIGHT_SOURCE_REACH_TILES * _UWTileSpacing;

    if (lfDistance > lfReach)
        return 15.0;

    float2 lPoint = pPositionWS.xz + (pNormalWS.xz * (_UWTileSpacing / 8.0));
    float2 lUV = ((lPoint / _UWTileSpacing) + 0.5) / 64.0;
    float lfLevel = SAMPLE_TEXTURE2D(_UWPaletteLightMap, sampler_UWPaletteLightMap, lUV).r * (255.0 / 16.0);

    // A SOFT EDGE (per user, the same day, with a screenshot: a lit floor ending in a hard curve
    // looked odd): over the last tile and a half the sources' level runs out to the darkest.
    float lfFade = saturate((lfDistance - (lfReach - (1.5 * _UWTileSpacing))) / (1.5 * _UWTileSpacing));

    return lerp(lfLevel, 15.0, lfFade);
}

// A flat surface's normal from the screen derivatives, turned towards the eye.
float3 UWFacingNormal(float3 pPositionWS)
{
    float3 lNormal = normalize(cross(ddy(pPositionWS), ddx(pPositionWS)));

    return dot(lNormal, _WorldSpaceCameraPos - pPositionWS) < 0.0 ? -lNormal : lNormal;
}

// The grime's levels: its amount times the slider's steps.
float UWGrimeLevel(float3 pPositionWS, float3 pNormalWS)
{
    return _UWPaletteGrime * UWGrimeAmount(pPositionWS, pNormalWS);
}

// GROUND SHADOWS (stage 3): darker levels under the creatures and items standing in the world as
// sprites - the same ones the Remastered mode gives a blob (UWRemasterGroundShadows): not the
// light sources, nothing in flight. Up to UW_GROUND_SHADOWS of them nearest the eye, each
// x, floor y, z and radius (UWPaletteEffects.cs); a floor point takes the darkest of them that
// stand on its height. Snapped to the texel grid like the grime, so the blob looks painted.
#define UW_GROUND_SHADOWS 64

float4 _UWPaletteShadows[UW_GROUND_SHADOWS];
float _UWPaletteShadowCount;
float _UWPaletteShadowStrength;

float UWGroundShadowLevel(float3 pPositionWS, float3 pNormalWS)
{
    if (_UWPaletteShadowStrength <= 0.0 || _UWPaletteShadowCount < 0.5 || pNormalWS.y < 0.7)
        return 0.0;

    float lfTexel = _UWTileSpacing / 64.0;
    float2 lPoint = (floor(pPositionWS.xz / lfTexel) + 0.5) * lfTexel;
    float lfShadow = 0.0;
    int liCount = (int)min(_UWPaletteShadowCount, (float)UW_GROUND_SHADOWS);

    for (int liAt = 0; liAt < liCount; liAt++)
    {
        float4 lCaster = _UWPaletteShadows[liAt];

        if (abs(pPositionWS.y - lCaster.y) > 6.0)
            continue;

        float lfRatio = distance(lPoint, lCaster.xz) / max(lCaster.w, 0.001);

        lfShadow = max(lfShadow, saturate((1.0 - lfRatio) * 2.0));
    }

    return _UWPaletteShadowStrength * lfShadow;
}

// RELIEF AND HOLLOWS (stage 4): the Remastered mode's normal and height array
// (UWTextureArrayBuilder.BuildNormals: RG the normal in tangent space, A the height from the
// detected joints), built for the palette path only while one of the two is on.
//   relief   the bumped normal against the flat one, lit from a torch held a quarter tile above
//            the eye: a face turned away from it gets darker, one turned towards it lighter, so
//            the stones' upper edges catch the light (_UWPaletteRelief steps at full turn).
//   hollows  what lies lower than its surroundings (eight by eight texels) - the joints - gets
//            darker, as the Remastered cavity does (_UWPaletteCavity steps).
// Not on water or lava floors (the tile map's liquid channel).
TEXTURE2D_ARRAY(_UWPaletteNormalArray);
SAMPLER(sampler_UWPaletteNormalArray);
float _UWPaletteRelief;
float _UWPaletteCavity;

// The tangent frame from the screen derivatives, as UW/Dungeon builds it (UWBuildTangentFrame,
// with its handedness fix).
float3x3 UWPaletteTangentFrame(float3 pNormalWS, float3 pPositionWS, float2 pUv, float3 pDdxPos,
    float3 pDdyPos, float2 pDdxUv, float2 pDdyUv)
{
    float3 lPerpY = cross(pDdyPos, pNormalWS);
    float3 lPerpX = cross(pNormalWS, pDdxPos);
    float3 lTangent = (lPerpY * pDdxUv.x) + (lPerpX * pDdyUv.x);
    float3 lBitangent = (lPerpY * pDdxUv.y) + (lPerpX * pDdyUv.y);
    float lfScale = rsqrt(max(1e-8, max(dot(lTangent, lTangent), dot(lBitangent, lBitangent))));

    lfScale *= dot(cross(pDdxPos, pDdyPos), pNormalWS) < 0.0 ? -1.0 : 1.0;

    return float3x3(lTangent * lfScale, lBitangent * lfScale, pNormalWS);
}

// DEPTH (stage 4b, per user the same day: "Oder wirkt es nur zusammen mit dem Parallax-Effekt
// aus Remastered gut?"): the Remastered parallax occlusion mapping (UW/Dungeon, UWParallaxUv) on
// the palette path. It only moves the texture coordinate - another texel of the same texture is
// read, no new colour arises - so it keeps to the palette. Always snapped to whole texels and the
// height read from a coarse mipmap (the Remastered "snap"), so the hard pixels jump texel by texel
// instead of swimming; the height in the Remastered number of layers. With it a SELF SHADOW in
// levels: from the point a ray runs over the height map towards the torch above the eye, and
// what rises above it darkens the point (UW/Dungeon, UWSelfShadow).
//   _UWPaletteDepth        the slider, 0 off; the parallax depth is the Remastered one times
//                          slider / 3, the self shadow that many levels at full shadow.
//   _UWPaletteParallax     x the Remastered depth, y layers, z steps, w sign (UWPaletteEffects.cs)
float _UWPaletteDepth;
float4 _UWPaletteParallax;

// NO SNAP TO WHOLE TEXELS (per user, 2026-10-07: "Ohne sieht es immer besser aus"): the shift is
// free and the height read texel by texel. A snapped variant (rounded shift, the height from a
// coarse mipmap) was tried and taken out, in both render modes.

float UWPaletteSampleDepth(float2 pUv, float pfSlice)
{
    float lfHeight = SAMPLE_TEXTURE2D_ARRAY_LOD(_UWPaletteNormalArray, sampler_UWPaletteNormalArray, pUv, pfSlice, 0.0).a;
    float lfLevels = max(1.0, _UWPaletteParallax.y);

    return 1.0 - (floor(lfHeight * lfLevels) / lfLevels);
}

// The shifted texture coordinate; pNormalWS faces the eye. Not on water or lava floors.
float2 UWPaletteParallaxUv(float2 pUv, float pfSlice, float3 pPositionWS, float3 pNormalWS)
{
    float3 lDdxPos = ddx(pPositionWS);
    float3 lDdyPos = ddy(pPositionWS);
    float2 lDdxUv = ddx(pUv);
    float2 lDdyUv = ddy(pUv);

    if (_UWPaletteDepth <= 0.0)
        return pUv;

    if (pNormalWS.y > 0.7 && _UWFloorHeightsReady > 0.5
        && UWGrimeTile(int2(round(pPositionWS.xz / _UWTileSpacing))).b > 0.5)
        return pUv;

    float3x3 lFrame = UWPaletteTangentFrame(pNormalWS, pPositionWS, pUv, lDdxPos, lDdyPos, lDdxUv, lDdyUv);
    float3 lViewTS = mul(lFrame, normalize(_WorldSpaceCameraPos - pPositionWS));
    float lfDepth = _UWPaletteParallax.x * (_UWPaletteDepth / 3.0) * smoothstep(0.15, 0.6, abs(lViewTS.z));

    if (lfDepth <= 0.0001)
        return pUv;

    float lfSteps = max(4.0, _UWPaletteParallax.z * lerp(1.5, 0.6, saturate(abs(lViewTS.z))));
    float lfStep = 1.0 / lfSteps;
    float2 lOffset = (lViewTS.xy / max(0.35, abs(lViewTS.z))) * lfDepth * lfStep * _UWPaletteParallax.w;
    float lfRayDepth = 0.0;
    float2 lAt = pUv;
    float lfMapDepth = UWPaletteSampleDepth(lAt, pfSlice);

    [loop]
    for (int liStep = 0; liStep < 48; liStep++)
    {
        if (lfRayDepth >= lfMapDepth || (float)liStep >= lfSteps)
            break;

        lAt -= lOffset;
        lfRayDepth += lfStep;
        lfMapDepth = UWPaletteSampleDepth(lAt, pfSlice);
    }

    return lAt;
}

// The self shadow in levels at an (already shifted) coordinate, the Remastered reach, depth and
// softness (_UWPaletteSelfShadow: x depth, y reach, z softness).
float4 _UWPaletteSelfShadow;

float UWPaletteSelfShadowLevel(float2 pUv, float pfSlice, float3 pPositionWS, float3 pNormalWS)
{
    float3 lDdxPos = ddx(pPositionWS);
    float3 lDdyPos = ddy(pPositionWS);
    float2 lDdxUv = ddx(pUv);
    float2 lDdyUv = ddy(pUv);

    if (_UWPaletteDepth <= 0.0)
        return 0.0;

    float3x3 lFrame = UWPaletteTangentFrame(pNormalWS, pPositionWS, pUv, lDdxPos, lDdyPos, lDdxUv, lDdyUv);
    float3 lLightTS = mul(lFrame, normalize(_WorldSpaceCameraPos + float3(0.0, _UWTileSpacing * 0.25, 0.0) - pPositionWS));
    float lfFlat = length(lLightTS.xy);

    if (lLightTS.z <= 0.001 || lfFlat < 0.0001)
        return 0.0;

    float2 lDirection = lLightTS.xy / lfFlat;
    float lfRise = (lLightTS.z / lfFlat) / max(_UWPaletteSelfShadow.x, 0.0001);
    float lfStart = SAMPLE_TEXTURE2D_ARRAY_GRAD(_UWPaletteNormalArray, sampler_UWPaletteNormalArray, pUv, pfSlice,
        lDdxUv, lDdyUv).a;
    float lfDistance = min(_UWPaletteSelfShadow.y, (1.0 - lfStart) / max(lfRise, 0.0001));

    if (lfDistance <= 0.0)
        return 0.0;

    float lfOcclusion = 0.0;

    [loop]
    for (int liStep = 1; liStep <= 8; liStep++)
    {
        float lfT = liStep / 8.0;
        float lfAlong = lfDistance * lfT;
        float lfHeight = SAMPLE_TEXTURE2D_ARRAY_GRAD(_UWPaletteNormalArray, sampler_UWPaletteNormalArray,
            pUv + (lDirection * lfAlong), pfSlice, lDdxUv, lDdyUv).a;

        lfOcclusion = max(lfOcclusion, (lfHeight - (lfStart + (lfAlong * lfRise))) * (1.0 - (lfT * 0.5)));
    }

    return _UWPaletteDepth * saturate(lfOcclusion * _UWPaletteSelfShadow.z);
}

float UWReliefLevel(float3 pPositionWS, float3 pNormalWS, float2 pUv, float pfSlice)
{
    // Derivatives first, while every pixel of the quad still runs the same code.
    float3 lDdxPos = ddx(pPositionWS);
    float3 lDdyPos = ddy(pPositionWS);
    float2 lDdxUv = ddx(pUv);
    float2 lDdyUv = ddy(pUv);

    if (_UWPaletteRelief <= 0.0 && _UWPaletteCavity <= 0.0)
        return 0.0;

    if (pNormalWS.y > 0.7 && _UWFloorHeightsReady > 0.5
        && UWGrimeTile(int2(round(pPositionWS.xz / _UWTileSpacing))).b > 0.5)
        return 0.0;

    half4 lPacked = SAMPLE_TEXTURE2D_ARRAY_GRAD(_UWPaletteNormalArray, sampler_UWPaletteNormalArray, pUv, pfSlice,
        lDdxUv, lDdyUv);
    float lfLevel = 0.0;

    if (_UWPaletteRelief > 0.0)
    {
        float3 lTangentNormal;

        lTangentNormal.xy = (lPacked.rg * 2.0) - 1.0;
        lTangentNormal.z = sqrt(saturate(1.0 - dot(lTangentNormal.xy, lTangentNormal.xy)));

        float3 lBumped = normalize(mul(lTangentNormal,
            UWPaletteTangentFrame(pNormalWS, pPositionWS, pUv, lDdxPos, lDdyPos, lDdxUv, lDdyUv)));
        float3 lToLight = normalize(_WorldSpaceCameraPos + float3(0.0, _UWTileSpacing * 0.25, 0.0) - pPositionWS);

        lfLevel += _UWPaletteRelief * (dot(pNormalWS, lToLight) - dot(lBumped, lToLight)) * 4.0;
    }

    if (_UWPaletteCavity > 0.0)
    {
        float lfAround = SAMPLE_TEXTURE2D_ARRAY_GRAD(_UWPaletteNormalArray, sampler_UWPaletteNormalArray, pUv, pfSlice,
            lDdxUv * 8.0, lDdyUv * 8.0).a;

        lfLevel += _UWPaletteCavity * saturate((lfAround - lPacked.a) * 3.0);
    }

    return lfLevel;
}

// GLOW (stage 5): what burns or shines by itself keeps its full colour - level 0, no darkening
// by distance - within the light sources' reach (UW_LIGHT_SOURCE_REACH_TILES):
//   lava      the palette's lava indices (16-23, the rotating ones) on floors and walls;
//   sprites   the light sources' pictures (_UWGlowSprite on their renderers, UWPaletteEffects.cs),
//             only their bright pixels - the flame, not the torch's handle: a pixel whose colour at
//             full light has a channel above UW_GLOW_THRESHOLD, as the Remastered sprite glow
//             takes the bright ones (UW/Billboard, _GlowThreshold).
// Spell missiles glow already (_Glow, per user 2026-10-04).
float _UWPaletteGlow;

#define UW_GLOW_THRESHOLD 0.6

bool UWGlowInReach(float3 pPositionWS)
{
    return _UWPaletteGlow > 0.5 && _UWTileSpacing > 0.0
        && distance(pPositionWS.xz, _WorldSpaceCameraPos.xz) < UW_LIGHT_SOURCE_REACH_TILES * _UWTileSpacing;
}

// A lava pixel's level cap: 0 while glowing, else none (15).
float UWLavaGlowCap(float4 pRawTexel, float3 pPositionWS)
{
    float lfIndex = UWToIndex(pRawTexel.r);

    return lfIndex >= 16.0 && lfIndex <= 23.0 && UWGlowInReach(pPositionWS) ? 0.0 : 15.0;
}

float UWPaletteEffectLevel(float4 pPositionCS)
{
    float lfLevel = 0.0;

    if (_UWPaletteAO > 0.0)
    {
        float lfOcclusion = 1.0 - saturate(SampleAmbientOcclusion(GetNormalizedScreenSpaceUV(pPositionCS)));

        lfLevel += lfOcclusion * _UWPaletteAO;
    }

    return lfLevel;
}

#endif
