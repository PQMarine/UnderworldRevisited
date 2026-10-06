// THE ORIGINAL'S DRAW ORDER AS THE DEPTH (the palette path, per user 2026-09-28: "we are only
// patching around here" - the order read the same day in StartRendering_seg017_526,
// seg017_1FDD_DBC and the object sorting of segment 33).
//
// The original has no depth buffer. It paints the view like a painter: ROWS from the back to the
// front, rows across the CARDINAL direction nearest to the view (not across the exact view); in
// each row the columns left outer to inner, right outer to inner, the viewer's column last; in
// each tile the floor, the ceiling, the walls (the face to the right neighbour, the far face, the
// face to the left neighbour), the diagonal wall, then the tile's objects sorted by a coarse key.
// Whatever is painted later covers what was painted before - however far away it really is.
//
// So every sprite writes ITS PLACE IN THAT ORDER as its depth, and the depth
// test does what the painter did. The number is linear: row, column, part of the tile, and a
// fraction inside the part (the true distance, only to keep pieces of one part in order).
//
// Used by UWBillboardPalette (items and creatures) only - the level, doors and 3D models went back
// to the true depth the same day, see UWPainterSpriteVisible. The mesh still carries tile and kind
// per vertex (UWChunkGeometry.TileInfo) and UWPainterLevelDepth stays for a later use. The
// Remastered path keeps the true depth.
#ifndef UW_PAINTER_ORDER_INCLUDED
#define UW_PAINTER_ORDER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// UWWorldScale.TileSize.
#define UW_PAINTER_TILE_SIZE 64.0

// Rows counted from 8 behind the viewer to 31 ahead (the original paints 16). Kept small, like the
// object places, so a part keeps enough depth steps for its pieces.
#define UW_PAINTER_ROW_FIRST -8.0
#define UW_PAINTER_ROW_LAST 31.0
#define UW_PAINTER_ROWS 40.0
#define UW_PAINTER_COLUMNS 33.0

// Parts of a tile: floor 0, ceiling 1, walls 2-4 (right, far, left), diagonal 5, then the object
// places, the largest key first.
#define UW_PAINTER_PART_FLOOR 0.0
#define UW_PAINTER_PART_CEILING 1.0
#define UW_PAINTER_PART_WALL 2.0
#define UW_PAINTER_PART_DIAGONAL 5.0
#define UW_PAINTER_GEOMETRY_PARTS 6.0
#define UW_PAINTER_OBJECT_PLACES 64.0
#define UW_PAINTER_PARTS 70.0

// The kinds UWChunkGeometry writes into UV channel 1 (z); 0 = no level face (a door, a frame).
#define UW_PAINTER_KIND_FLOOR 1.0
#define UW_PAINTER_KIND_CEILING 2.0
#define UW_PAINTER_KIND_WALL 3.0
#define UW_PAINTER_KIND_DIAGONAL 4.0

// "Ahead" (the row step) is the cardinal direction nearest to the view, "right" (the column step)
// a quarter turn clockwise from it (PositionCamera_seg031_396, tables 444 and 446).
void UWPainterAxes(out float2 pAhead, out float2 pRight)
{
    float3 lForward = -UNITY_MATRIX_V[2].xyz;

    pAhead = abs(lForward.x) >= abs(lForward.z) ? float2(lForward.x >= 0.0 ? 1.0 : -1.0, 0.0)
        : float2(0.0, lForward.z >= 0.0 ? 1.0 : -1.0);
    pRight = float2(pAhead.y, -pAhead.x);
}

// The tile a world point lies in (tile i spans i * size - half to i * size + half).
float2 UWPainterTileOf(float2 pWorldXZ)
{
    return floor((pWorldXZ + (UW_PAINTER_TILE_SIZE * 0.5)) / UW_PAINTER_TILE_SIZE);
}

// The place of a part of a tile in the painter's order: painted first gets the smallest number -
// the far rows, then in a row the left columns from the outside in, the right ones from the
// outside in, the middle last.
float UWPainterPlace(float2 pTile, float pfPart)
{
    float2 lAhead;
    float2 lRight;

    UWPainterAxes(lAhead, lRight);

    float2 lOffset = pTile - UWPainterTileOf(_WorldSpaceCameraPos.xz);
    float lfRow = clamp(dot(lOffset, lAhead), UW_PAINTER_ROW_FIRST, UW_PAINTER_ROW_LAST);
    float lfColumn = clamp(dot(lOffset, lRight), -16.0, 16.0);

    float lfRowOrder = UW_PAINTER_ROW_LAST - lfRow;
    float lfColumnOrder = lfColumn == 0.0 ? 32.0 : (lfColumn < 0.0 ? 16.0 + lfColumn : 32.0 - lfColumn);

    return (((lfRowOrder * UW_PAINTER_COLUMNS) + lfColumnOrder) * UW_PAINTER_PARTS)
        + clamp(pfPart, 0.0, UW_PAINTER_PARTS - 1.0);
}

// The depth for a part of a tile. pfFraction 0..1 orders pieces inside the part, larger later.
float UWPainterDepth(float2 pTile, float pfPart, float pfFraction)
{
    float lfPlace = UWPainterPlace(pTile, pfPart) + (saturate(pfFraction) * 0.98);
    float lfDepth = (lfPlace + 1.0) / ((UW_PAINTER_ROWS * UW_PAINTER_COLUMNS * UW_PAINTER_PARTS) + 2.0);

    #if UNITY_REVERSED_Z
    return lfDepth;
    #else
    return 1.0 - lfDepth;
    #endif
}

// Nearer is painted later among pieces of one part: the fraction from the true distance.
float UWPainterNearness(float3 pPositionWS)
{
    return saturate(1.0 - (distance(pPositionWS, _WorldSpaceCameraPos) / 4096.0));
}

// The faces of ONE object (a 3D model, a door, a bridge) among themselves: by their true distance
// around the object's pivot, within two tiles of it - so the front of a model stays in front of
// its back inside the object's one place.
float UWPainterObjectFraction(float3 pPivotWS, float3 pPositionWS)
{
    return saturate(0.5 + ((distance(pPivotWS, _WorldSpaceCameraPos) - distance(pPositionWS, _WorldSpaceCameraPos)) / 128.0));
}

// A face of the level: its tile and kind from UV channel 1, the wall's side from its normal.
float UWPainterLevelDepth(float3 pTileInfo, float3 pNormalWS, float3 pPositionWS)
{
    float lfPart = UW_PAINTER_PART_FLOOR;

    if (pTileInfo.z > UW_PAINTER_KIND_DIAGONAL - 0.5)
        lfPart = UW_PAINTER_PART_DIAGONAL;
    else if (pTileInfo.z > UW_PAINTER_KIND_WALL - 0.5)
    {
        float2 lAhead;
        float2 lRight;

        UWPainterAxes(lAhead, lRight);

        // The normal points into the open tile, away from the neighbour the face stands against.
        float2 lNormal = normalize(pNormalWS.xz + float2(1.0e-5, 0.0));
        float lfSide = dot(lNormal, lRight) < -0.7 ? 0.0 : (dot(lNormal, lRight) > 0.7 ? 2.0 : 1.0);

        lfPart = UW_PAINTER_PART_WALL + lfSide;
    }
    else if (pTileInfo.z > UW_PAINTER_KIND_CEILING - 0.5)
        lfPart = UW_PAINTER_PART_CEILING;

    return UWPainterDepth(pTileInfo.xy, lfPart, 0.0);
}

// An object: its tile from its pivot, and its place among the tile's objects from the original's
// key (seg033_2EEF_3CD) - in eighths of the tile, in the view frame, x across from the left side,
// y ahead from the near edge: the viewer's column 2y, the right side x + y + 1, the left side
// 8 - x + y; the largest first. A BIG object (pfBigRadius, its COMOBJ radius in eighths when
// byte 1 bit 3 is set - creatures) that reaches past the near edge goes with the tile of the
// nearer row, past the inner side with the next inner column, and counts its old place shifted
// by a tile. pfKeyBonus is added to the key (0x20 puts wall writings first).
void UWPainterObjectPlace(float3 pPivotWS, float pfBigRadius, float pfKeyBonus, out float2 pTile, out float pPart)
{
    float2 lAhead;
    float2 lRight;

    UWPainterAxes(lAhead, lRight);

    float2 lTile = UWPainterTileOf(pPivotWS.xz);
    float2 lEyeTile = UWPainterTileOf(_WorldSpaceCameraPos.xz);
    float2 lCorner = (lTile * UW_PAINTER_TILE_SIZE) - (UW_PAINTER_TILE_SIZE * 0.5);
    float2 lLocal = saturate((pPivotWS.xz - lCorner) / UW_PAINTER_TILE_SIZE);

    float lfAcross = dot(lLocal, abs(lRight));
    float lfAheadIn = dot(lLocal, abs(lAhead));

    if (lRight.x + lRight.y < 0.0)
        lfAcross = 1.0 - lfAcross;

    if (lAhead.x + lAhead.y < 0.0)
        lfAheadIn = 1.0 - lfAheadIn;

    float lfX = min(floor(lfAcross * 8.0), 7.0);
    float lfY = min(floor(lfAheadIn * 8.0), 7.0);
    float lfColumn = dot(lTile - lEyeTile, lRight);
    float lfRow = dot(lTile - lEyeTile, lAhead);
    float lfRadius = floor(pfBigRadius + 0.5);

    if (lfRadius > 0.0)
    {
        bool lbNearer = lfY - lfRadius < 0.0;
        float lfReach = lfColumn > 0.0 ? lfX - lfRadius : lfX + lfRadius;
        bool lbInner = lfColumn != 0.0 && (lfReach < 0.0 || lfReach > 7.0);

        if (lbNearer)
        {
            lfRow -= 1.0;
            lfY += 8.0;
        }

        if (lbInner)
        {
            if (lfColumn < 0.0)
            {
                lfColumn += 1.0;
                lfX -= 8.0;
            }
            else
            {
                lfColumn -= 1.0;
                lfX += 8.0;
            }
        }
    }

    float lfKey = (lfColumn == 0.0 ? 2.0 * lfY : (lfColumn > 0.0 ? lfX + lfY + 1.0 : 8.0 - lfX + lfY))
        + pfKeyBonus;

    pPart = UW_PAINTER_GEOMETRY_PARTS + (UW_PAINTER_OBJECT_PLACES - 1.0
        - clamp(lfKey, 0.0, UW_PAINTER_OBJECT_PLACES - 1.0));
    pTile = lEyeTile + (lRight * lfColumn) + (lAhead * lfRow);
}

float UWPainterObjectDepth(float3 pPivotWS, float pfBigRadius, float pfKeyBonus, float pfFraction)
{
    float2 lTile;
    float lfPart;

    UWPainterObjectPlace(pPivotWS, pfBigRadius, pfKeyBonus, lTile, lfPart);

    return UWPainterDepth(lTile, lfPart, pfFraction);
}

// THE SPRITES AGAINST THE GEOMETRY (per user, 2026-09-28: with the level in the painter's order
// water of a lower tile showed through the stone of a farther one - the original clips every tile
// to the screen spans left visible by the nearer ones, and without that clipping the order alone
// paints wrong). So the level, the doors and the 3D models keep their TRUE depth, which is right
// between surfaces, and only the sprites follow the painter: a sprite pixel looks at the surface
// the depth prepass found there (_CameraDepthTexture, the sprites are not in that pass), takes
// the tile it belongs to - half a unit towards the eye, so a wall goes with the open tile it
// bounds, as in the original - and is visible if the painter reaches the sprite's tile no
// earlier than that tile. The sprite's own tile never covers it (its objects come after its
// floor and walls).
float UWPainterTileOrder(float2 pTile)
{
    float2 lAhead;
    float2 lRight;

    UWPainterAxes(lAhead, lRight);

    float2 lOffset = pTile - UWPainterTileOf(_WorldSpaceCameraPos.xz);
    float lfRow = clamp(dot(lOffset, lAhead), UW_PAINTER_ROW_FIRST, UW_PAINTER_ROW_LAST);
    float lfColumn = clamp(dot(lOffset, lRight), -16.0, 16.0);
    float lfColumnOrder = lfColumn == 0.0 ? 32.0 : (lfColumn < 0.0 ? 16.0 + lfColumn : 32.0 - lfColumn);

    return ((UW_PAINTER_ROW_LAST - lfRow) * UW_PAINTER_COLUMNS) + lfColumnOrder;
}

// THE DOOR PARTITION (seg033_2EEF_43B with seg033_2EEF_281): in a tile with a door the original
// paints the objects beyond the door plane first, then the door, then those on the viewer's side -
// and a big creature just behind the door has been handed to the door's tile by then (per user,
// 2026-09-28: creatures showed over doors and lintels). UWOwnTile.ClearDoors fills the planes.
TEXTURE2D(_UWDoorPlanes);
float _UWDoorPlanesReady;

// True when the tile has a door and the point lies beyond its plane, seen from the eye.
bool UWPainterBeyondDoor(float2 pTile, float3 pPointWS)
{
    if (_UWDoorPlanesReady < 0.5 || any(pTile < 0.0) || any(pTile > 63.0))
        return false;

    float4 lDoor = LOAD_TEXTURE2D(_UWDoorPlanes, int2(pTile));

    if (lDoor.r < 0.5)
        return false;

    // g: 1 the door plane lies across X, 0 across Z, 2 a bridge's deck across Y (UWOwnTile).
    float lfPoint = lDoor.g > 1.5 ? pPointWS.y : (lDoor.g > 0.5 ? pPointWS.x : pPointWS.z);
    float lfEye = lDoor.g > 1.5 ? _WorldSpaceCameraPos.y : (lDoor.g > 0.5 ? _WorldSpaceCameraPos.x : _WorldSpaceCameraPos.z);

    return (lfPoint - lDoor.b) * (lfEye - lDoor.b) < 0.0;
}

// A BRIDGE OVER THE SPRITE (per user, 2026-09-29: the Wine of Compassion under the plate of level
// 6, 27/50, still showed a pixel through it with the depth test below - where the plate's edge met
// the wall the depth prepass found a surface beyond the bottle). The plate covers its whole tile,
// so the test is geometric: a sprite on the other side of the deck from the eye is hidden where
// its pixel's ray crosses the deck inside the sprite's tile.
bool UWPainterUnderDeck(float2 pPixel, float3 pPivotWS)
{
    // The pivot's own tile, not the one a big creature is handed to.
    float2 lTile = UWPainterTileOf(pPivotWS.xz);

    if (_UWDoorPlanesReady < 0.5 || any(lTile < 0.0) || any(lTile > 63.0))
        return false;

    float4 lPlane = LOAD_TEXTURE2D(_UWDoorPlanes, int2(lTile));

    if (lPlane.r < 0.5 || lPlane.g < 1.5)
        return false;

    if ((pPivotWS.y - lPlane.b) * (_WorldSpaceCameraPos.y - lPlane.b) >= 0.0)
        return false;

    // Any point of the pixel's ray will do.
    #if UNITY_REVERSED_Z
    float lfAnyDepth = 0.5;
    #else
    float lfAnyDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, 0.5);
    #endif

    float3 lRay = ComputeWorldSpacePosition(pPixel / _ScaledScreenParams.xy, lfAnyDepth, UNITY_MATRIX_I_VP)
        - _WorldSpaceCameraPos;

    if (abs(lRay.y) < 1.0e-5)
        return false;

    float lfAlong = (lPlane.b - _WorldSpaceCameraPos.y) / lRay.y;

    if (lfAlong <= 0.0)
        return false;

    float2 lCross = _WorldSpaceCameraPos.xz + (lRay.xz * lfAlong);

    return all(UWPainterTileOf(lCross) == lTile);
}

// THE TILES THE ORIGINAL DRAWS (per user, 2026-10-06: at a diagonal view things flashed through a
// wall at the edge of the picture). The original paints only the tiles its sweep marks, so a thing
// behind a wall at the edge is not there at all, while the order alone put it over the nearer wall.
// UWSweepMaskDriver writes the mask every frame (UWSweepMask: the original's sweep, run at the
// heading and a quarter turn to either side): 0 = in a cone and not drawn, hide; 1 = drawn or
// undecided, the order decides.
TEXTURE2D(_UWSweepMask);
float _UWSweepMaskReady;

bool UWPainterSweepHides(float2 pTile)
{
    if (_UWSweepMaskReady < 0.5 || any(pTile < 0.0) || any(pTile > 63.0))
        return false;

    return LOAD_TEXTURE2D(_UWSweepMask, int2(pTile)).r < 0.5;
}

// A 3D model stands in the tile (alpha of the plane texture, UWOwnTile.RegisterModelTile).
bool UWPainterTileHasModel(float2 pTile)
{
    if (_UWDoorPlanesReady < 0.5 || any(pTile < 0.0) || any(pTile > 63.0))
        return false;

    return LOAD_TEXTURE2D(_UWDoorPlanes, int2(pTile)).a > 0.5;
}

// THE MODELS OF A TILE IN THE PAINTER'S ORDER (UWOwnTile.RegisterModelTile, per user 2026-10-03:
// the ruby in the gravestone of level 7, 17/34, was cut by the stone). Up to four per tile, each
// 1 + eighth east + 8 * eighth north + 64 * big radius + 512 * place in the tile's chain; r below
// zero when there were more. The original sorts a tile's objects by key, largest first, and keeps
// the chain order on equal keys (the stable bubble sort seg033_2EEF_ED).
TEXTURE2D(_UWModelOrder);

// The pivot of a noted model: the centre of its eighth (the height does not count for the key).
float3 UWPainterModelPivot(float pfModel, float2 pTile)
{
    float lfCode = pfModel - 1.0;
    float lfNorth = floor(fmod(lfCode, 64.0) / 8.0);
    float lfEast = fmod(lfCode, 8.0);
    float2 lCorner = (pTile * UW_PAINTER_TILE_SIZE) - (UW_PAINTER_TILE_SIZE * 0.5);

    return float3(lCorner.x + ((lfEast + 0.5) * UW_PAINTER_TILE_SIZE / 8.0), 0.0,
        lCorner.y + ((lfNorth + 0.5) * UW_PAINTER_TILE_SIZE / 8.0));
}

bool UWPainterModelAfter(float pfModel, float2 pTile, float pfSpritePlace, float pfSpriteChain)
{
    float lfCode = pfModel - 1.0;
    float lfChain = floor(lfCode / 512.0);
    float lfRadius = floor(fmod(lfCode, 512.0) / 64.0);

    float2 lModelTile;
    float lfModelPart;

    UWPainterObjectPlace(UWPainterModelPivot(pfModel, pTile), lfRadius, 0.0, lModelTile, lfModelPart);

    float lfModelPlace = UWPainterPlace(lModelTile, lfModelPart);

    return lfModelPlace > pfSpritePlace || (lfModelPlace == pfSpritePlace && lfChain > pfSpriteChain);
}

// The nearest noted model to a point, within half a tile; 0 when there is none.
float UWPainterNearestModel(float pfModel, float2 pTile, float3 pPointWS, inout float pfBest, float pfCurrent)
{
    if (pfModel < 0.5)
        return pfCurrent;

    float lfDistance = distance(UWPainterModelPivot(pfModel, pTile).xz, pPointWS.xz);

    if (lfDistance >= pfBest)
        return pfCurrent;

    pfBest = lfDistance;

    return pfModel;
}

// Whether the surface behind the sprite's pixel belongs to a model painted after the sprite -
// then it may cover it. The surface goes with the model whose pivot is nearest, within half a
// tile (per user, 2026-10-03: the emerald in the gravestone of 17/35 was still cut, because the
// pillar in the tile's corner is painted after it - with the nearer row, being big - and that
// let the gravestone's face cover the emerald too). Farther from every model it is the tile's
// floor or wall, which the original paints before the objects. True as well when the tile holds
// more models than were noted.
bool UWPainterModelPaintsAfter(float2 pTile, float3 pSurfaceWS, float pfSpritePlace, float pfSpriteChain)
{
    if (any(pTile < 0.0) || any(pTile > 63.0))
        return true;

    float4 lModels = LOAD_TEXTURE2D(_UWModelOrder, int2(pTile));

    if (lModels.r < 0.0)
        return true;

    float lfBest = UW_PAINTER_TILE_SIZE * 0.5;
    float lfModel = 0.0;

    lfModel = UWPainterNearestModel(lModels.r, pTile, pSurfaceWS, lfBest, lfModel);
    lfModel = UWPainterNearestModel(lModels.g, pTile, pSurfaceWS, lfBest, lfModel);
    lfModel = UWPainterNearestModel(lModels.b, pTile, pSurfaceWS, lfBest, lfModel);
    lfModel = UWPainterNearestModel(lModels.a, pTile, pSurfaceWS, lfBest, lfModel);

    return lfModel > 0.5 && UWPainterModelAfter(lfModel, pTile, pfSpritePlace, pfSpriteChain);
}

// pfSpritePart is the sprite's part from UWPainterObjectPlace, pfSpriteChain its place in the
// tile's chain (_ChainIndex).
bool UWPainterSpriteVisible(float2 pPixel, float2 pSpriteTile, float3 pPivotWS, float pfSpritePart, float pfSpriteChain)
{
    if (UWPainterUnderDeck(pPixel, pPivotWS))
        return false;

    float lfDepth = LOAD_TEXTURE2D_X(_CameraDepthTexture, uint2(pPixel)).r;

    // Nothing drawn there (the far plane).
    #if UNITY_REVERSED_Z
    if (lfDepth <= 0.0)
        return true;
    #else
    if (lfDepth >= 1.0)
        return true;
    #endif

    #if !UNITY_REVERSED_Z
    lfDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, lfDepth);
    #endif

    float3 lSurface = ComputeWorldSpacePosition(pPixel / _ScaledScreenParams.xy, lfDepth, UNITY_MATRIX_I_VP);

    float3 lNudged = lSurface + (normalize(_WorldSpaceCameraPos - lSurface) * 0.5);
    float lfSpriteOrder = UWPainterTileOrder(pSpriteTile);
    float lfSurfaceOrder = UWPainterTileOrder(UWPainterTileOf(lNudged.xz));

    // Beyond the door of its tile the sprite comes before the door, its frame and lintel - and
    // in a tile with a 3D model it is sorted among the models (UWOwnTile.RegisterModelTile):
    // then whatever of the tile is nearer than the sprite covers it, but only where that surface
    // belongs to a model painted after the sprite.
    if (lfSpriteOrder == lfSurfaceOrder
        && (UWPainterBeyondDoor(pSpriteTile, pPivotWS)
            || (UWPainterTileHasModel(pSpriteTile)
                && UWPainterModelPaintsAfter(pSpriteTile, lSurface, UWPainterPlace(pSpriteTile, pfSpritePart), pfSpriteChain))))
    {
        float3 lForward = -UNITY_MATRIX_V[2].xyz;

        return dot(lSurface - _WorldSpaceCameraPos, lForward) >= dot(pPivotWS - _WorldSpaceCameraPos, lForward) - 1.0;
    }

    return lfSpriteOrder >= lfSurfaceOrder;
}

// The sprites among themselves still by the painter's order, written into a thin band in front
// of all geometry (a surface nearer than 1.1 times the near plane is never seen). The geometry
// is not compared by depth any more but by UWPainterSpriteVisible.
float UWPainterSpriteBandDepth(float pfPainterDepth)
{
    #if UNITY_REVERSED_Z
    return 0.9 + (pfPainterDepth * 0.1);
    #else
    return pfPainterDepth * 0.1;
    #endif
}

#endif
