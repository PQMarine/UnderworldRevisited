// GRIME ALONG THE WALLS, shared by both render modes (moved out of UWPaletteEffects.hlsl the same
// day the Remastered mode got it, per user 2026-10-07: "Mach grime fuer Remastered"). The palette
// path turns its amount into shade levels and dithers the dirt's palette colour in
// (UWPaletteEffects.hlsl, UWPaletteLookup.hlsl); UW/Dungeon darkens the albedo towards the tone
// (_UWGrimeToneColour). One slider and one tone for both (_UWPaletteGrime, UWPaletteEffects.cs).
// The tile size from _UWOwnTileSize, which the level loader sets in both modes.
#ifndef UW_GRIME_INCLUDED
#define UW_GRIME_INCLUDED

#include "UWOwnTile.hlsl"

#define UW_GRIME_TILE _UWOwnTileSize

// GRIME ALONG THE WALLS (per user, 2026-10-07: "als sehe es so aus als ob sich dort Dreck
// angesammelt hat, um den Uebergang zu kaschieren"): where a wall or a step up meets the floor, a
// band of darker levels on the floor and a narrower one up the wall, its width wandering along the
// edge so that it reads as dirt piled up, not as a shadow. Taken from the tile map
// (_UWFloorHeights: floor height, solid as the ceiling, and the tile type), not from the picture,
// so it neither flickers nor depends on the view. Snapped to the textures' texel grid (64 a
// tile), so it looks painted in. Open tiles and slopes (their floor rising 16 units across the
// tile, as UWTileQueries.FloorHeightAt has it; per user the same day: "bei Schraegen funktioniert
// es noch nicht") and diagonal tiles, whose floor takes the band along the diagonal wall too
// (per user: "an diagonalen Waenden ist grime nur an der Wand zu sehen"). Never on water or lava (per user: "bei Wasser und
// fluessiger Lava sollte man es weglassen") - the tile map's third channel names the liquid.
float _UWPaletteGrime;

float UWGrimeHash(float2 pPoint)
{
    return frac(sin(dot(pPoint, float2(127.1, 311.7))) * 43758.5453);
}

// A value noise along the edge, a new value every four texels, smoothly between.
float UWGrimeNoise(float pfAlong, float pfSeed)
{
    float lfCell = pfAlong / 4.0;
    float lfAt = floor(lfCell);
    float lfMix = frac(lfCell);

    lfMix = lfMix * lfMix * (3.0 - (2.0 * lfMix));

    return lerp(UWGrimeHash(float2(lfAt, pfSeed)), UWGrimeHash(float2(lfAt + 1.0, pfSeed)), lfMix);
}

// A tile's floor height (solid: the ceiling), type and liquid (1 water, 2 lava); off the map solid.
float3 UWGrimeTile(int2 pTile)
{
    if (any(pTile < int2(0, 0)) || any(pTile > int2(63, 63)))
        return float3(256.0, 0.0, 0.0);

    return LOAD_TEXTURE2D(_UWFloorHeights, pTile).rgb;
}

// The floor height at a point of a tile, pLocal from -0.5 to 0.5 across it (x east, y north):
// a slope rises 16 units from its low side to its high one (types 6-9: north, south, east, west).
float UWGrimeFloorAt(float3 pTile, float2 pLocal)
{
    if (pTile.r >= UW_SOLID_TILE_FLOOR)
        return 256.0;

    float2 lAcross = saturate(pLocal + 0.5);
    int liType = (int)round(pTile.g);

    if (liType == 6)
        return pTile.r + (16.0 * lAcross.y);

    if (liType == 7)
        return pTile.r + (16.0 * (1.0 - lAcross.y));

    if (liType == 8)
        return pTile.r + (16.0 * lAcross.x);

    if (liType == 9)
        return pTile.r + (16.0 * (1.0 - lAcross.x));

    return pTile.r;
}

// How much grime lies at a point of a wall, floor or ceiling, 0 to 1; pNormalWS faces the eye.
// The levels are this times _UWPaletteGrime, the dirt's colour is dithered in by it (UWGrimeTintMix).
float UWGrimeAmount(float3 pPositionWS, float3 pNormalWS)
{
    if (_UWPaletteGrime <= 0.0 || _UWFloorHeightsReady < 0.5 || UW_GRIME_TILE <= 0.0)
        return 0.0;

    float lfTexel = UW_GRIME_TILE / 64.0;
    float3 lPoint = (floor(pPositionWS / lfTexel) + 0.5) * lfTexel;
    float lfDistance = 1.0e6;
    float lfAlong = 0.0;
    float lfSeed = 0.0;

    if (pNormalWS.y > 0.7)
    {
        // A floor: the nearest side whose neighbour is solid or a step up, or a diagonal wall.
        int2 lTile = int2(round(lPoint.xz / UW_GRIME_TILE));
        float3 lOwn = UWGrimeTile(lTile);
        int liType = (int)round(lOwn.g);
        float2 lLocal = (lPoint.xz / UW_GRIME_TILE) - float2(lTile);

        // Open tiles and slopes, not on water or lava, and only the floor itself (not a bridge or
        // the top of a model lying on it).
        if (liType < 1 || liType > 9 || lOwn.b > 0.5
            || abs(pPositionWS.y - UWGrimeFloorAt(lOwn, lLocal)) > 2.0)
            return 0.0;

        // A DIAGONAL TILE: the distance to its diagonal wall, on the open half (UWTileQueries.
        // IsSubTileInOpenHalf: se y < x, nw y >= x, ne x + y >= 1, sw x + y < 1, y north), along
        // the wall for the noise; the wall's own texels find the same tile and line (below).
        if (liType >= 2 && liType <= 5)
        {
            float2 lAcross = lLocal + 0.5;
            float lfToWall = liType == 2 ? lAcross.x - lAcross.y
                : liType == 5 ? lAcross.y - lAcross.x
                : liType == 4 ? (lAcross.x + lAcross.y) - 1.0
                : 1.0 - (lAcross.x + lAcross.y);
            bool lbRising = liType == 2 || liType == 5;

            lfDistance = max(lfToWall, 0.0) * 0.70710678 * UW_GRIME_TILE;
            lfAlong = dot(lPoint.xz, lbRising ? float2(0.70710678, 0.70710678) : float2(0.70710678, -0.70710678));
            lfSeed = 1000.0 + (float)((lTile.y * 64) + lTile.x);
        }

        int2 lSides[4] = { int2(1, 0), int2(-1, 0), int2(0, 1), int2(0, -1) };

        for (int liSide = 0; liSide < 4; liSide++)
        {
            // The floor on both sides of the shared edge, at this point's place along it.
            float2 lSide = float2(lSides[liSide]);
            float2 lOnEdge = (lLocal * abs(lSide.yx)) + (0.5 * lSide);
            float lfOwnEdge = UWGrimeFloorAt(lOwn, lOnEdge);
            float lfNeighbourEdge = UWGrimeFloorAt(UWGrimeTile(lTile + lSides[liSide]), lOnEdge - lSide);

            if (lfNeighbourEdge < lfOwnEdge + 8.0)
                continue;

            float lfToEdge = (0.5 - dot(lLocal, float2(lSides[liSide]))) * UW_GRIME_TILE;

            if (lfToEdge < lfDistance)
            {
                lfDistance = lfToEdge;
                lfAlong = lSides[liSide].x != 0 ? lPoint.z : lPoint.x;
                lfSeed = round(((lSides[liSide].x != 0 ? lTile.x : lTile.y) + (0.5 * (lSides[liSide].x + lSides[liSide].y))) * 2.0);
            }
        }
    }
    else if (abs(pNormalWS.y) < 0.3)
    {
        // A wall: its height above the floor of the tile in front of it, counted double, so the
        // band up the wall is half as wide as the one on the floor.
        float2 lFrontPoint = (lPoint.xz + (pNormalWS.xz * (UW_GRIME_TILE * 0.25))) / UW_GRIME_TILE;
        int2 lFront = int2(round(lFrontPoint));
        float3 lOwn = UWGrimeTile(lFront);

        if (lOwn.r >= UW_SOLID_TILE_FLOOR || lOwn.b > 0.5)
            return 0.0;

        // The floor in front at the wall's foot (a slope's height where it meets the wall).
        float lfFoot = UWGrimeFloorAt(lOwn, lFrontPoint - (pNormalWS.xz * 0.25) - float2(lFront));

        if (lPoint.y < lfFoot)
            return 0.0;

        lfDistance = (lPoint.y - lfFoot) * 2.0;

        if (abs(pNormalWS.x) > 0.3 && abs(pNormalWS.z) > 0.3)
        {
            // A diagonal wall: along it as its floor does, seeded by the same tile.
            bool lbRising = (pNormalWS.x * pNormalWS.z) < 0.0;

            lfAlong = dot(lPoint.xz, lbRising ? float2(0.70710678, 0.70710678) : float2(0.70710678, -0.70710678));
            lfSeed = 1000.0 + (float)((lFront.y * 64) + lFront.x);
        }
        else
        {
            lfAlong = abs(pNormalWS.x) > 0.5 ? lPoint.z : lPoint.x;
            lfSeed = round((abs(pNormalWS.x) > 0.5 ? pPositionWS.x : pPositionWS.z) / UW_GRIME_TILE * 2.0);
        }
    }
    else
    {
        return 0.0;
    }

    float lfWidth = (UW_GRIME_TILE / 10.0) * (0.3 + (1.2 * UWGrimeNoise(floor(lfAlong / lfTexel), lfSeed)));
    float lfGrit = 0.65 + (0.35 * UWGrimeHash(lPoint.xz + lPoint.y));

    return saturate(1.0 - (lfDistance / lfWidth)) * lfGrit;
}

#endif
