// WHAT STANDS IN A TILE IS NOT COVERED BY THAT TILE'S OWN FLOOR, CEILING OR WALLS
// (keyword _UW_OWN_TILE).
//
// The original paints tile by tile from the back to the front, and whatever stands in a tile is
// painted after that tile. So the tile's own floor, ceiling and walls never cover it - only what
// belongs to a nearer tile does. Seen so far, all per user against the original:
//
//   the lurker     its image hangs twelve of its nineteen rows below its ground point, ripple
//                  included, and all of it shows above the water (2026-09-17; since then every
//                  creature has the rule, Drog's feet sank into the floor)
//   the shrine     level 3, 28/44: two units in its floor and, 55 units tall in 48 of room, into
//                  the ceiling - in full in the original (2026-09-22)
//   a goblin       attacking at a wall, now and then an arm went into the wall (2026-09-22)
//   thrown items   do not vanish into the wall in the original (2026-09-22)
//
// With a depth buffer: take the box of the tile the object stands in - its four sides, the
// ground below and the ceiling above. A pixel outside that box, seen through it, takes the depth
// of the point where its line of sight LEAVES the box. So it wins against the box's own faces and
// still loses against everything nearer, which lies in front of that point.
//
// Until 2026-09-22 this only knew the plane at the ground point (then UWBelowGroundPlane.hlsl),
// and a second file tried the floor and ceiling for 3D models on its own. Both are this one now.
//
// A 3D MODEL has a front and a back on the same line of sight, and both leave the box at the
// same point - pulled to the same depth, they fought, and the back showed through the front
// (per user, 2026-09-22). So a model only pulls the side that faces the viewer; the back keeps
// its own depth, lies behind the plane and stays hidden. A sprite has a single side.
//
// NOT HANDLED BY THE BOX: a diagonal wall of the own tile lies inside it and still covers a
// model; sprites use the plane rule below, which leaves it behind them.
#ifndef UW_OWN_TILE_INCLUDED
#define UW_OWN_TILE_INCLUDED

// Set once by UWLevelLoader (UWWorldScale.TileSize and CeilingHeight). While they are zero the
// box is only the plane at the ground, the old rule.
float _UWOwnTileSize;
float _UWOwnTileCeilingY;

// The floor height of every tile (UWOwnTile.SetFloorHeights), 64 x 64, and whether it is set.
TEXTURE2D(_UWFloorHeights);
float _UWFloorHeightsReady;

// The box is drawn in by this much on every side, so its faces do not fight with the surfaces
// they stand for - the lift the lurker always had above its water plane.
#define UW_OWN_TILE_INSET 0.25

float UWOwnTileDeviceDepth(float3 pPositionWS)
{
    float4 lClip = TransformWorldToHClip(pPositionWS);
    float lfDepth = lClip.z / lClip.w;

    #if !UNITY_REVERSED_Z
    // OpenGL style clip space runs from -1 to 1, the depth output from 0 to 1.
    lfDepth = lfDepth * 0.5 + 0.5;
    #endif

    return lfDepth;
}

// pPositionWS the fragment, pPivotWS the point the object stands on (its tile is the pivot's
// tile), pfGroundY the bottom of the box - a creature's or item's own ground point, a model's tile
// floor. pfDepth is the fragment's own depth (SV_POSITION.z); the result goes to SV_Depth.
float UWOwnTileDepth(float3 pPositionWS, float3 pPivotWS, float pfGroundY, float pfDepth)
{
    float3 lEye = _WorldSpaceCameraPos;
    float3 lMin;
    float3 lMax;

    if (_UWOwnTileSize > 0.0)
    {
        float lfHalf = _UWOwnTileSize * 0.5;
        float2 lCorner = (floor((pPivotWS.xz + lfHalf) / _UWOwnTileSize) * _UWOwnTileSize) - lfHalf;

        lMin = float3(lCorner.x, pfGroundY, lCorner.y) + UW_OWN_TILE_INSET;
        lMax = float3(lCorner.x + _UWOwnTileSize, _UWOwnTileCeilingY, lCorner.y + _UWOwnTileSize)
            - UW_OWN_TILE_INSET;
    }
    else
    {
        lMin = float3(-1.0e6, pfGroundY + UW_OWN_TILE_INSET, -1.0e6);
        lMax = float3(1.0e6, 1.0e6, 1.0e6);
    }

    if (all(pPositionWS >= lMin) && all(pPositionWS <= lMax))
        return pfDepth;

    // The line of sight from the eye (t = 0) to the fragment (t = 1): where does it leave the box?
    float3 lRay = pPositionWS - lEye;
    float3 lSafe = lRay;
    lSafe.x = abs(lSafe.x) > 1.0e-5 ? lSafe.x : 1.0e-5;
    lSafe.y = abs(lSafe.y) > 1.0e-5 ? lSafe.y : 1.0e-5;
    lSafe.z = abs(lSafe.z) > 1.0e-5 ? lSafe.z : 1.0e-5;

    float3 lT1 = (lMin - lEye) / lSafe;
    float3 lT2 = (lMax - lEye) / lSafe;
    float3 lNear = min(lT1, lT2);
    float3 lFar = max(lT1, lT2);
    float lfEnter = max(max(lNear.x, lNear.y), lNear.z);
    float lfLeave = min(min(lFar.x, lFar.y), lFar.z);

    // The line misses the box, or the fragment lies before it: nothing of the tile's own is in
    // front of it.
    if (lfLeave < lfEnter || lfLeave <= 0.0 || lfLeave >= 1.0)
        return pfDepth;

    return UWOwnTileDeviceDepth(lEye + (lRay * lfLeave));
}

// SPRITES IN THE ORIGINAL'S DRAW ORDER (per user, 2026-09-28; the order read the same day in
// StartRendering_seg017_526, seg017_1FDD_DBC and the object sorting of segment 33). The original
// has no depth buffer: it walks ROWS from the back to the front - rows across the CARDINAL
// direction nearest to the view, not across the exact view - and in each row the columns from the
// outside in, the viewer's column last; each tile paints floor, ceiling, walls, then its objects.
// So an object is covered by nothing of its own tile and of the tiles painted before it, and by
// everything painted after.
//
// With a depth buffer: a sprite gets ONE depth for all its pixels, the near edge of its tile's row
// (the inset inside it) plus a small step per the original's sort key. What that gives, all per
// user the same day: a creature reaching into a neighbour tile is not covered by it; a spider
// before a shield, a lizardman before an ash heap behind him, a spider before a headless; no sprite
// stabs through another (one flat depth each); a mushroom on a lower tile beside the viewer stays
// under the edge of the viewer's own tile.
//
//   - A BIG object (a creature: pfBigRadius, its COMOBJ radius) whose radius reaches past the near
//     edge of its tile is painted with the tile of the NEARER row, one reaching past the inner side
//     (towards the viewer's column) with the next INNER column - both, the diagonal one.
//   - Inside a tile the original sorts by a coarse key in eighths of the tile, in the view frame:
//     x across, y ahead from the near edge; the viewer's column 2y, the right side x + y + 1, the
//     left side 8 - x + y; the largest first. A deferred object counts its old place, shifted by a
//     tile. Ties keep the chain order there; here the exact distance breaks them.
//   - The viewer's own tile is painted last: a sprite in it is measured from the eye. A sprite in
//     another tile of the viewer's row (the row's near edge lies at or behind the eye) keeps the box
//     rule above, so the viewer's own tile still covers it.
//   - A pivot beside or behind the eye is the nearest of all in the viewer's own tile; in another
//     tile it keeps the box rule.
//   - Between tiles of one row the column order decides (left outer to inner, right outer to
//     inner, the viewer's column last), counted three columns out; the key only inside a tile.
//
// NOT MODELLED: the original's hole for a big object in the viewer's row that pokes towards him
// (deferred to a row that never comes - not drawn); the partition around doors and bridges;
// animations 0x1C0-0x1FF one key step nearer.
#define UW_OWN_TILE_SQUEEZE 0.1
#define UW_OWN_TILE_KEY_STEPS 32.0

// How far below its own tile's floor a pivot counts as under it - see UWOwnTileSpriteDepth.
#define UW_BELOW_FLOOR_TOLERANCE 4.0

// A solid tile in _UWFloorHeights: UWOwnTile.SetFloorHeights writes the ceiling height (256)
// for it, an open floor is at most 15 steps of 16.
#define UW_SOLID_TILE_FLOOR 250.0

// BEHIND ROCK (per user, 2026-10-03, Remastered only, level 1 at 27/15 looking south-east: bones
// lying on 28/13 showed through the rock 28/14 - "sprites in general", the palette mode right).
// The plane rule below puts a sprite at the near edge of its tile's row, and a wall INSIDE that
// row - here the north face of 28/14, which runs along the row - lies behind that plane, so the
// sprite won. The original paints that wall with the open tile in front of it (its right wall),
// after the sprite's tile, and covers it; the palette path follows the order of the surface
// behind each pixel (UWPainterSpriteVisible). Here the pixel's line of sight is walked through
// the tile grid: if it crosses a solid tile before it reaches the sprite's own tile (or the tile
// the pixel lies in), the rock covers the pixel.
//
// DIAGONALS AND STEPS TOO (per user, 2026-10-03, "do the diagonals and steps in Remastered"): the
// same holds for a diagonal's wall and for the face of a higher floor inside the row. So the walk
// keeps where the line enters and leaves each tile, and the piece in between is covered when one
// of its ends lies in a diagonal's closed half (the open half is a triangle, so both ends inside
// it means the whole piece is), or below the tile's floor (the lower end of a straight piece is one
// of its ends). Slopes are left out of the floor test - their floor is not flat - and so is the
// ceiling, which is the same height everywhere.

// How far the line may dip below a floor or into a diagonal's closed half before it counts.
#define UW_STEP_TOLERANCE 1.0
#define UW_DIAGONAL_TOLERANCE 0.01

// The halves of a diagonal tile, u east and v north from its south-west corner (0 to 1): type 2
// open to the south-east, 3 south-west, 4 north-east, 5 north-west (UWTile.TileTypeEnum).
bool UWOwnTileInOpenHalf(float pfType, float2 pUV)
{
    if (pfType < 2.5)
        return pUV.y < pUV.x + UW_DIAGONAL_TOLERANCE;

    if (pfType < 3.5)
        return pUV.x + pUV.y < 1.0 + UW_DIAGONAL_TOLERANCE;

    if (pfType < 4.5)
        return pUV.x + pUV.y > 1.0 - UW_DIAGONAL_TOLERANCE;

    return pUV.y > pUV.x - UW_DIAGONAL_TOLERANCE;
}

// Whether the piece of the line from pfEnter to pfLeave (fractions of the way from the eye to the
// pixel) is covered inside this tile. pTile is the texel: floor height and tile type.
bool UWOwnTilePieceCovered(float2 pCell, float2 pTile, float3 pPositionWS, float pfEnter, float pfLeave)
{
    float3 lA = lerp(_WorldSpaceCameraPos, pPositionWS, pfEnter);
    float3 lB = lerp(_WorldSpaceCameraPos, pPositionWS, pfLeave);
    float lfType = round(pTile.g);

    if (lfType < 5.5 && min(lA.y, lB.y) < pTile.r - UW_STEP_TOLERANCE)
        return true;

    if (lfType > 1.5 && lfType < 5.5)
    {
        float2 lCorner = (pCell * _UWOwnTileSize) - (_UWOwnTileSize * 0.5);

        if (!UWOwnTileInOpenHalf(lfType, (lA.xz - lCorner) / _UWOwnTileSize)
            || !UWOwnTileInOpenHalf(lfType, (lB.xz - lCorner) / _UWOwnTileSize))
            return true;
    }

    return false;
}

bool UWOwnTileBehindRock(float3 pPositionWS, float3 pPivotWS)
{
    if (_UWFloorHeightsReady < 0.5 || _UWOwnTileSize <= 0.0)
        return false;

    float lfSize = _UWOwnTileSize;
    float lfHalf = lfSize * 0.5;
    float2 lFrom = _WorldSpaceCameraPos.xz;
    float2 lDir = pPositionWS.xz - lFrom;
    float2 lCell = floor((lFrom + lfHalf) / lfSize);
    float2 lEnd = floor((pPositionWS.xz + lfHalf) / lfSize);
    float2 lOwn = floor((pPivotWS.xz + lfHalf) / lfSize);
    float2 lStep = float2(lDir.x >= 0.0 ? 1.0 : -1.0, lDir.y >= 0.0 ? 1.0 : -1.0);
    float2 lSafe = float2(abs(lDir.x) > 1.0e-5 ? lDir.x : 1.0e-5, abs(lDir.y) > 1.0e-5 ? lDir.y : 1.0e-5);

    // Tile i spans i * size - half to i * size + half; the next border in the step's direction.
    float2 lNext = (((lCell + (lStep * 0.5)) * lfSize) - lFrom) / lSafe;
    float2 lDelta = abs(lfSize / lSafe);
    float lfEnter = 0.0;

    [loop]
    for (int liAt = 0; liAt < 32; liAt++)
    {
        if (all(lCell == lOwn) || all(lCell == lEnd))
            return false;

        float lfLeave = saturate(min(lNext.x, lNext.y));

        // The eye's own tile does not count, should the camera stand in a wall.
        if (liAt > 0)
        {
            int2 lTexel = int2(lCell);

            if (any(lTexel < int2(0, 0)) || any(lTexel > int2(63, 63)))
                return false;

            float2 lTile = LOAD_TEXTURE2D(_UWFloorHeights, lTexel).rg;

            if (lTile.r >= UW_SOLID_TILE_FLOOR)
                return true;

            if (UWOwnTilePieceCovered(lCell, lTile, pPositionWS, lfEnter, lfLeave))
                return true;
        }

        lfEnter = lfLeave;

        if (lNext.x < lNext.y)
        {
            lCell.x += lStep.x;
            lNext.x += lDelta.x;
        }
        else
        {
            lCell.y += lStep.y;
            lNext.y += lDelta.y;
        }
    }

    return false;
}

float UWOwnTileSpriteDepth(float3 pPositionWS, float3 pPivotWS, float pfGroundY, float pfDepth, float pfBigRadius)
{
    if (_UWOwnTileSize <= 0.0)
        return UWOwnTileDepth(pPositionWS, pPivotWS, pfGroundY, pfDepth);

    float3 lEye = _WorldSpaceCameraPos;
    float3 lForward = -UNITY_MATRIX_V[2].xyz;

    if (abs(lForward.x) < 1.0e-4 && abs(lForward.z) < 1.0e-4)
        return UWOwnTileDepth(pPositionWS, pPivotWS, pfGroundY, pfDepth);

    // The cardinal direction nearest to the view: "ahead" (the row step), and "right" (the column
    // step) a quarter turn clockwise from it.
    float2 lAhead = abs(lForward.x) >= abs(lForward.z) ? float2(sign(lForward.x), 0.0) : float2(0.0, sign(lForward.z));
    float2 lRight = float2(lAhead.y, -lAhead.x);

    float lfSize = _UWOwnTileSize;
    float lfHalf = lfSize * 0.5;
    float2 lTile = floor((pPivotWS.xz + lfHalf) / lfSize);
    float2 lEyeTile = floor((lEye.xz + lfHalf) / lfSize);
    // UNDER ITS OWN TILE'S FLOOR (per user, 2026-09-28: a thrown mushroom falling past a ledge,
    // its centre still over the higher tile, showed over that tile's floor; in the original it is
    // covered): the original never has an object below its tile's floor, only our physics passes
    // through that state - the sprite keeps its true depth, and the floor covers it.
    if (_UWFloorHeightsReady > 0.5)
    {
        int2 lTexel = clamp(int2(lTile), int2(0, 0), int2(63, 63));

        if (pPivotWS.y < LOAD_TEXTURE2D(_UWFloorHeights, lTexel).r - UW_BELOW_FLOOR_TOLERANCE)
            return pfDepth;
    }

    float2 lCorner = (lTile * lfSize) - lfHalf;

    // The pivot in eighths of its tile), in the view frame: x from the left side, y from the near edge.
    float2 lLocal = saturate((pPivotWS.xz - lCorner) / lfSize);
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

    // A big object reaching out of its tile goes with the nearer row and / or the inner column.
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

    float lfKey = lfColumn == 0.0 ? 2.0 * lfY : (lfColumn > 0.0 ? lfX + lfY + 1.0 : 8.0 - lfX + lfY);

    // The near edge of the (target) tile along "ahead", the inset inside it.
    float2 lTarget = lEyeTile + (lRight * lfColumn) + (lAhead * lfRow);
    float2 lTargetMin = (lTarget * lfSize) - lfHalf + UW_OWN_TILE_INSET - lEye.xz;
    float2 lTargetMax = (lTarget * lfSize) + lfHalf - UW_OWN_TILE_INSET - lEye.xz;
    float lfFront = min(dot(lTargetMin, lAhead), dot(lTargetMax, lAhead));

    float3 lPivotRay = pPivotWS - lEye;
    float lfPivot = dot(lPivotRay.xz, lAhead);

    // A pivot beside or behind the eye: the nearest of all only IN the viewer's own tile (the
    // spider standing on him); beside him in another tile the box rule, so his own tile covers
    // it (per user, same day: a mushroom on the lower tile beside him still showed over his floor).
    if (lfPivot <= 1.0)
    {
        if (any(abs(lTarget - lEyeTile) > 0.5))
            return UWOwnTileDepth(pPositionWS, pPivotWS, pfGroundY, pfDepth);

        return UWOwnTileDeviceDepth(lEye + (lForward * (_ProjectionParams.y * 2.0)));
    }

    if (lfFront <= 0.0)
    {
        if (any(abs(lTarget - lEyeTile) > 0.5))
            return UWOwnTileDepth(pPositionWS, pPivotWS, pfGroundY, pfDepth);

        lfFront = 0.0;
    }

    // THE COLUMN ORDER INSIDE A ROW comes before the key (per user, same day: turning, an item
    // further back slid in front now and then - two tiles of one row were compared by their keys,
    // which are built differently left, right and in the middle). The original paints the left
    // columns outer to inner, then the right ones outer to inner, the viewer's column last; the
    // later a tile, the nearer. Counted up to three columns out, which is where sprites of
    // neighbouring columns can still overlap.
    float lfOut = min(abs(lfColumn), 3.0);
    float lfPainted = lfColumn == 0.0 ? 6.0 : (lfColumn < 0.0 ? 3.0 - lfOut : 6.0 - lfOut);
    float lfSlot = ((6.0 - lfPainted) * UW_OWN_TILE_KEY_STEPS) + lfKey + 1.0;

    float lfStep = lfSize * UW_OWN_TILE_SQUEEZE / (7.0 * UW_OWN_TILE_KEY_STEPS);
    float lfTarget = lfFront + (lfSlot * lfStep) + (max(lfPivot - lfFront, 0.0) * 0.0001);

    // EVERY PIXEL ON THE SAME PLANE: the vertical plane lfTarget ahead of the eye along "ahead",
    // met by this pixel's own line of sight (per user, same day: leather armour and boots of one
    // tile swapped places when the view turned a little - the depth was taken along each sprite's
    // own pivot ray, and two rays side by side turn by different amounts). Two sprites at one pixel
    // now compare by lfTarget alone, whatever the angle; the original keeps the boots in front.
    float3 lRay = pPositionWS - lEye;
    float lfAlong = dot(lRay.xz, lAhead);

    if (lfAlong <= 1.0e-3)
        return pfDepth;

    return UWOwnTileDeviceDepth(lEye + (lRay * (lfTarget / lfAlong)));
}

#endif
