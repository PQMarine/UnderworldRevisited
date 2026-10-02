using UnityEngine;
using UWDataImport.UWData;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// Builds the static architecture of a level - floor, ceiling and
    /// walls - from its tile data as a handful of chunk meshes.
    ///
    /// Previously every face got its own GameObject with its own mesh, its own
    /// material, its own texture and its own MeshCollider; with around 1800 walkable tiles
    /// that meant roughly 6000 to 10000 objects per level. Here 16 chunks remain.
    ///
    /// The geometry rules - slopes, diagonals, wall heights at transitions - were
    /// deliberately taken over verbatim from the old construction code. The only change
    /// is where the triangles are written to.
    /// </summary>
    public sealed class UWLevelMeshBuilder
    {
        public const int TilesPerAxis = UWDataImport.UWData.UWWorldScale.TilesPerAxis;

        /// <summary>Half the edge length of a tile in world coordinates.</summary>
        public const float TileHalfSize = UWDataImport.UWData.UWWorldScale.TileHalfSize;

        /// <summary>Distance between two tile centres.</summary>
        public const float TileSpacing = UWDataImport.UWData.UWWorldScale.TileSize;

        /// <summary>Ceiling height in world coordinates.</summary>
        public const float CeilingHeight = UWDataImport.UWData.UWWorldScale.CeilingHeight;

        /// <summary>First slice with floor textures in the shared Texture2DArray.</summary>
        private readonly int miFloorSliceOffset;

        private readonly UWLevel mOLevel;
        private UWChunkGeometry[] mOChunks;

        private int miCurrentTileIndex;
        private int miAdjacentTileIndex;

        private UWTile CurrentTile
        {
            get { return mOLevel.TileData[miCurrentTileIndex]; }
        }

        private UWTile AdjacentTile
        {
            get { return mOLevel.TileData[miAdjacentTileIndex]; }
        }

        private UWTile NorthTile
        {
            get { return fTile(CurrentTile.NorthTile); }
        }

        private UWTile EastTile
        {
            get { return fTile(CurrentTile.EastTile); }
        }

        private UWTile SouthTile
        {
            get { return fTile(CurrentTile.SouthTile); }
        }

        private UWTile WestTile
        {
            get { return fTile(CurrentTile.WestTile); }
        }

        private UWLevelMeshBuilder(UWLevel pOLevel, int piFloorSliceOffset)
        {
            mOLevel = pOLevel;
            miFloorSliceOffset = piFloorSliceOffset;
        }

        /// <param name="piFloorSliceOffset">First slice with floor textures, see UWTextureArrayBuilder.</param>
        public static UWChunkGeometry[] Build(UWLevel pOLevel, int piFloorSliceOffset)
        {
            return new UWLevelMeshBuilder(pOLevel, piFloorSliceOffset).fBuild();
        }

        /// <summary>
        /// Neighbour access with range check. At the level edge the neighbour indices run out
        /// of the array; in the original this went unchecked, because the edge tiles are solid
        /// and the cases never occurred.
        /// </summary>
        private UWTile fTile(int piTileIndex)
        {
            if (piTileIndex < 0 || piTileIndex >= mOLevel.TileData.Length)
                return CurrentTile;

            return mOLevel.TileData[piTileIndex];
        }

        private UWChunkGeometry[] fBuild()
        {
            int liChunksPerAxis = TilesPerAxis / UWChunkGeometry.ChunkSizeInTiles;
            mOChunks = new UWChunkGeometry[liChunksPerAxis * liChunksPerAxis];

            for (int z = 0; z < liChunksPerAxis; z++)
            {
                for (int x = 0; x < liChunksPerAxis; x++)
                    mOChunks[z * liChunksPerAxis + x] = new UWChunkGeometry(x, z);
            }

            for (int z = 0; z < TilesPerAxis; z++)
            {
                for (int x = 0; x < TilesPerAxis; x++)
                {
                    miCurrentTileIndex = (z * TilesPerAxis) + x;

                    if (CurrentTile.TileType == UWTile.TileTypeEnum.solid)
                        continue;

                    fAddTile(x, z);
                }
            }

            return mOChunks;
        }

        private UWChunkGeometry fGetChunk(int xPos, int zPos)
        {
            int liChunksPerAxis = TilesPerAxis / UWChunkGeometry.ChunkSizeInTiles;
            int liChunkX = xPos / UWChunkGeometry.ChunkSizeInTiles;
            int liChunkZ = zPos / UWChunkGeometry.ChunkSizeInTiles;

            return mOChunks[liChunkZ * liChunksPerAxis + liChunkX];
        }

        private void fAddTile(int xPos, int zPos)
        {
            fAddFloorAndCeiling(xPos, zPos);

            float lfTop = float.NaN;
            float lfBottom = float.NaN;

            for (int side = 0; side < 4; side++)
            {
                if (CurrentTile.AdjacentTileIndices[side] >= 0 && CurrentTile.AdjacentTileIndices[side] < 4096)
                {
                    miAdjacentTileIndex = CurrentTile.AdjacentTileIndices[side];

                    fGetHeights(side, ref lfTop, ref lfBottom);

                    if (lfTop < lfBottom)
                        continue;

                    fAddStraightWall(xPos, zPos, side, lfTop, lfBottom);
                }
            }

            // Outside the side loop: the diagonal wall does not depend on the neighbours,
            // and a tile whose four sides are all skipped (map edge,
            // or lfTop < lfBottom) would otherwise never get it.
            fAddDiagonalWall(xPos, zPos);
        }

        private void fGetHeights(int side, ref float pfTop, ref float pfBottom)
        {
            pfTop = CeilingHeight;
            pfBottom = CurrentTile.FloorHeight;

            if (AdjacentTile.TileType != UWTile.TileTypeEnum.solid && AdjacentTile.FloorHeight + AdjacentTile.Slope > CurrentTile.FloorHeight)
            {
                // A solid neighbour is already excluded by the condition above; the test used
                // to be repeated here and could never be true (removed 2026-09-16).
                if ((AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_ne && (side == 0 || side == 1))
                    || (AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_nw && (side == 0 || side == 3))
                    || (AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_se && (side == 2 || side == 1))
                    || (AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_sw && (side == 2 || side == 3))
                    || ((CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_ne || CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_nw) && (AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_se || AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_sw))
                    )
                    return;

                if (CurrentTile.TileType == UWTile.TileTypeEnum.open &&
                    ((WestTile.TileType == UWTile.TileTypeEnum.slope_e && side == 3) ||
                    (SouthTile.TileType == UWTile.TileTypeEnum.slope_n && side == 2) ||
                    (NorthTile.TileType == UWTile.TileTypeEnum.slope_s && side == 0) ||
                    (EastTile.TileType == UWTile.TileTypeEnum.slope_w && side == 1)))
                {
                    pfTop = AdjacentTile.FloorHeight + AdjacentTile.Slope;
                }
                else
                    pfTop = AdjacentTile.FloorHeight;
            }
        }

        /// <summary>
        /// Floor and ceiling of a tile.
        ///
        /// THE FLOOR UVS ARE FLIPPED IN V (V=0 at the +z edge, V=1 at the -z edge). The ceiling
        /// uses the same V direction and differs only by a mirror in U. Reason: the textures are
        /// row-flipped when baked, because row 0 of the source is at the top and Unity starts
        /// at the bottom (see UWTextureArrayBuilder.fGetPixels). For walls that is right, for
        /// floors it turns the texture upside down.
        ///
        /// VERIFIED on floor texture 39 on tile 7/52 of level 3 (noticed by the user,
        /// 2026-09-05): it shows a shoreline and carries its water colours exclusively
        /// in image rows 16 to 31, i.e. at the bottom. The tile's surroundings slope down towards
        /// LARGER y - that is where the water is - while towards smaller y rock
        /// rises. Without the flip the water half ended up on the rock side.
        ///
        /// Most floor textures do not show this, they are too symmetrical.
        /// </summary>
        private void fAddFloorAndCeiling(int xPos, int zPos)
        {
            Vector3[] verticesFloor = null;
            int[] indicesFloor = null;
            Vector2[] uvsFloor = null;

            switch (CurrentTile.TileType)
            {
                case UWTile.TileTypeEnum.diagonal_se:
                    verticesFloor = new Vector3[3];
                    verticesFloor[0] = fCreateVertex(1, 1, CurrentTile.FloorHeight);
                    verticesFloor[1] = fCreateVertex(1, -1, CurrentTile.FloorHeight);
                    verticesFloor[2] = fCreateVertex(-1, -1, CurrentTile.FloorHeight);
                    indicesFloor = new int[] { 0, 1, 2 };
                    uvsFloor = new Vector2[] { new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                    break;
                case UWTile.TileTypeEnum.diagonal_sw:
                    verticesFloor = new Vector3[3];
                    verticesFloor[0] = fCreateVertex(-1, 1, CurrentTile.FloorHeight);
                    verticesFloor[1] = fCreateVertex(1, -1, CurrentTile.FloorHeight);
                    verticesFloor[2] = fCreateVertex(-1, -1, CurrentTile.FloorHeight);
                    indicesFloor = new int[] { 0, 1, 2 };
                    uvsFloor = new Vector2[] { new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1) };
                    break;
                case UWTile.TileTypeEnum.diagonal_ne:
                    verticesFloor = new Vector3[3];
                    verticesFloor[0] = fCreateVertex(-1, 1, CurrentTile.FloorHeight);
                    verticesFloor[1] = fCreateVertex(1, 1, CurrentTile.FloorHeight);
                    verticesFloor[2] = fCreateVertex(1, -1, CurrentTile.FloorHeight);
                    indicesFloor = new int[] { 0, 1, 2 };
                    uvsFloor = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1) };
                    break;
                case UWTile.TileTypeEnum.diagonal_nw:
                    verticesFloor = new Vector3[3];
                    verticesFloor[0] = fCreateVertex(-1, 1, CurrentTile.FloorHeight);
                    verticesFloor[1] = fCreateVertex(1, 1, CurrentTile.FloorHeight);
                    verticesFloor[2] = fCreateVertex(-1, -1, CurrentTile.FloorHeight);
                    indicesFloor = new int[] { 0, 1, 2 };
                    uvsFloor = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) };
                    break;
                case UWTile.TileTypeEnum.slope_n:
                    verticesFloor = new Vector3[4];
                    verticesFloor[0] = fCreateVertex(-1, 1, CurrentTile.FloorHeight + CurrentTile.Slope);
                    verticesFloor[1] = fCreateVertex(1, 1, CurrentTile.FloorHeight + CurrentTile.Slope);
                    verticesFloor[2] = fCreateVertex(1, -1, CurrentTile.FloorHeight);
                    verticesFloor[3] = fCreateVertex(-1, -1, CurrentTile.FloorHeight);
                    indicesFloor = new int[] { 0, 1, 2, 2, 3, 0 };
                    uvsFloor = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                    break;
                case UWTile.TileTypeEnum.slope_e:
                    verticesFloor = new Vector3[4];
                    verticesFloor[0] = fCreateVertex(-1, 1, CurrentTile.FloorHeight);
                    verticesFloor[1] = fCreateVertex(1, 1, CurrentTile.FloorHeight + CurrentTile.Slope);
                    verticesFloor[2] = fCreateVertex(1, -1, CurrentTile.FloorHeight + CurrentTile.Slope);
                    verticesFloor[3] = fCreateVertex(-1, -1, CurrentTile.FloorHeight);
                    indicesFloor = new int[] { 0, 1, 2, 2, 3, 0 };
                    uvsFloor = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                    break;
                case UWTile.TileTypeEnum.slope_s:
                    verticesFloor = new Vector3[4];
                    verticesFloor[0] = fCreateVertex(-1, 1, CurrentTile.FloorHeight);
                    verticesFloor[1] = fCreateVertex(1, 1, CurrentTile.FloorHeight);
                    verticesFloor[2] = fCreateVertex(1, -1, CurrentTile.FloorHeight + CurrentTile.Slope);
                    verticesFloor[3] = fCreateVertex(-1, -1, CurrentTile.FloorHeight + CurrentTile.Slope);
                    indicesFloor = new int[] { 0, 1, 2, 2, 3, 0 };
                    uvsFloor = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                    break;
                case UWTile.TileTypeEnum.slope_w:
                    verticesFloor = new Vector3[4];
                    verticesFloor[0] = fCreateVertex(-1, 1, CurrentTile.FloorHeight + CurrentTile.Slope);
                    verticesFloor[1] = fCreateVertex(1, 1, CurrentTile.FloorHeight);
                    verticesFloor[2] = fCreateVertex(1, -1, CurrentTile.FloorHeight);
                    verticesFloor[3] = fCreateVertex(-1, -1, CurrentTile.FloorHeight + CurrentTile.Slope);
                    indicesFloor = new int[] { 0, 1, 2, 2, 3, 0 };
                    uvsFloor = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                    break;
                default:
                    verticesFloor = new Vector3[4];
                    verticesFloor[0] = fCreateVertex(-1, 1, CurrentTile.FloorHeight);
                    verticesFloor[1] = fCreateVertex(1, 1, CurrentTile.FloorHeight);
                    verticesFloor[2] = fCreateVertex(1, -1, CurrentTile.FloorHeight);
                    verticesFloor[3] = fCreateVertex(-1, -1, CurrentTile.FloorHeight);
                    indicesFloor = new int[] { 0, 1, 2, 2, 3, 0 };
                    uvsFloor = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                    break;
            }

            Vector3 lOOrigin = fGetTileOrigin(xPos, zPos);
            UWChunkGeometry lOChunk = fGetChunk(xPos, zPos);

            if (verticesFloor != null && indicesFloor != null)
                lOChunk.Append(miCurrentTileIndex, lOOrigin, verticesFloor, indicesFloor, uvsFloor, miFloorSliceOffset + CurrentTile.TextureFloor,
                    UWChunkGeometry.KindFloor);

            Vector3[] verticesCeiling = new Vector3[4];
            int[] indicesCeiling = new int[] { 3, 1, 0, 3, 2, 1 };
            Vector2[] uvsCeiling = new Vector2[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) };
            verticesCeiling[0] = fCreateVertex(-1, 1, CeilingHeight);
            verticesCeiling[1] = fCreateVertex(1, 1, CeilingHeight);
            verticesCeiling[2] = fCreateVertex(1, -1, CeilingHeight);
            verticesCeiling[3] = fCreateVertex(-1, -1, CeilingHeight);

            lOChunk.Append(miCurrentTileIndex, lOOrigin, verticesCeiling, indicesCeiling, uvsCeiling, miFloorSliceOffset + CurrentTile.TextureCeiling,
                UWChunkGeometry.KindCeiling);
        }

        private void fAddStraightWall(int xPos, int zPos, int side, float top, float bottom)
        {
            Vector3[] vertices = null;
            int[] indices = null;
            Vector2[] uvs = null;

            if (AdjacentTile.FloorHeight + AdjacentTile.Slope > CurrentTile.FloorHeight || AdjacentTile.TileType == UWTile.TileTypeEnum.solid
                || (AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_ne && (side == 0 || side == 1))
                || (AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_nw && (side == 0 || side == 3))
                || (AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_se && (side == 2 || side == 1))
                || (AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_sw && (side == 2 || side == 3)))
            {
                if (CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_ne && (side == 2 || side == 3))
                    return;

                if (CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_se && (side == 0 || side == 3))
                    return;

                if (CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_nw && (side == 1 || side == 2))
                    return;

                if (CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_sw && (side == 0 || side == 1))
                    return;

                float vScale = 1f;

                // THE WALL FOLLOWS BOTH FLOORS CORNER BY CORNER (2026-09-17). Until then the height
                // came from a handful of fixed cases (an open tile next to one particular slope,
                // a straight side next to a slope running along it), so a slope next to a slope
                // running the other way got a flat wall of zero height and left a hole: on level 2
                // at 58/14 (rising north) next to 59/14 (rising west) one looked through the floor
                // into black (per user with a screenshot). Now the floor heights of this tile and
                // of the neighbour are compared at the two corners of the shared edge; where the
                // neighbour is higher, the wall fills exactly the space in between - a quad when
                // it is higher at both corners, a triangle up to the crossing point when only at
                // one. A solid neighbour, and a diagonal one whose closed half lies on this edge,
                // still get the full wall up to "top" as before.
                int liCornerX0, liCornerZ0, liCornerX1, liCornerZ1, liStepX, liStepZ;
                float lfU0, lfU1;

                switch (side)
                {
                    case 0:
                        liCornerX0 = -1; liCornerZ0 = 1; liCornerX1 = 1; liCornerZ1 = 1; liStepX = 0; liStepZ = 1;
                        lfU0 = 0f; lfU1 = 1f;
                        indices = new int[] { 2, 1, 0, 0, 3, 2 };
                        break;
                    case 1:
                        liCornerX0 = 1; liCornerZ0 = 1; liCornerX1 = 1; liCornerZ1 = -1; liStepX = 1; liStepZ = 0;
                        lfU0 = 0f; lfU1 = 1f;
                        indices = new int[] { 2, 1, 0, 0, 3, 2 };
                        break;
                    case 2:
                        liCornerX0 = -1; liCornerZ0 = -1; liCornerX1 = 1; liCornerZ1 = -1; liStepX = 0; liStepZ = -1;
                        lfU0 = 1f; lfU1 = 0f;
                        indices = new int[] { 0, 1, 2, 2, 3, 0 };
                        break;
                    default:
                        liCornerX0 = -1; liCornerZ0 = -1; liCornerX1 = -1; liCornerZ1 = 1; liStepX = -1; liStepZ = 0;
                        lfU0 = 0f; lfU1 = 1f;
                        indices = new int[] { 1, 0, 3, 3, 2, 1 };
                        break;
                }

                bool lbFullWall = AdjacentTile.TileType == UWTile.TileTypeEnum.solid
                    || (AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_ne && (side == 0 || side == 1))
                    || (AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_nw && (side == 0 || side == 3))
                    || (AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_se && (side == 2 || side == 1))
                    || (AdjacentTile.TileType == UWTile.TileTypeEnum.diagonal_sw && (side == 2 || side == 3))
                    || top >= CeilingHeight;

                float lfBottom0 = fCornerHeight(CurrentTile, liCornerX0, liCornerZ0);
                float lfBottom1 = fCornerHeight(CurrentTile, liCornerX1, liCornerZ1);

                float lfTop0 = lbFullWall ? top : fCornerHeight(AdjacentTile, liCornerX0 - (2 * liStepX), liCornerZ0 - (2 * liStepZ));
                float lfTop1 = lbFullWall ? top : fCornerHeight(AdjacentTile, liCornerX1 - (2 * liStepX), liCornerZ1 - (2 * liStepZ));

                float lfRise0 = lfTop0 - lfBottom0;
                float lfRise1 = lfTop1 - lfBottom1;

                if (lfRise0 <= 0f && lfRise1 <= 0f)
                    return;

                // v0/v1 on the floor at corner 0/1, v2 above v1, v3 above v0 - the order and
                // winding of the former quads per side.
                Vector3 lOBottom0 = fCreateVertex(liCornerX0, liCornerZ0, lfBottom0);
                Vector3 lOBottom1 = fCreateVertex(liCornerX1, liCornerZ1, lfBottom1);
                Vector3 lOTop0 = fCreateVertex(liCornerX0, liCornerZ0, lfTop0);
                Vector3 lOTop1 = fCreateVertex(liCornerX1, liCornerZ1, lfTop1);

                vertices = new Vector3[] { lOBottom0, lOBottom1, lOTop1, lOTop0 };
                uvs = new Vector2[] { new Vector2(lfU0, 0f), new Vector2(lfU1, 0f), new Vector2(lfU1, 0f), new Vector2(lfU0, 0f) };

                // Higher at one corner only: the other corner collapses onto the point where the
                // two floors cross, which turns the quad into a triangle.
                if (lfRise0 <= 0f || lfRise1 <= 0f)
                {
                    float lfT = lfRise0 / (lfRise0 - lfRise1);
                    Vector3 lOCross = Vector3.Lerp(lOBottom0, lOBottom1, lfT);
                    Vector2 lOCrossUv = new Vector2(Mathf.Lerp(lfU0, lfU1, lfT), 0f);

                    if (lfRise0 <= 0f)
                    {
                        vertices[0] = lOCross;
                        vertices[3] = lOCross;
                        uvs[0] = lOCrossUv;
                        uvs[3] = lOCrossUv;
                    }
                    else
                    {
                        vertices[1] = lOCross;
                        vertices[2] = lOCross;
                        uvs[1] = lOCrossUv;
                        uvs[2] = lOCrossUv;
                    }
                }

                if (vertices == null || indices == null || uvs == null)
                    return;

                // The top edge may be sloped; we want its highest point.
                float lfHighest = CurrentTile.FloorHeight;

                for (int i = 0; i < vertices.Length; i++)
                    lfHighest = Mathf.Max(lfHighest, vertices[i].y);

                // Where two floors meet flush, a wall without height results.
                // It draws nothing and only costs vertices and triangles.
                if (lfHighest <= CurrentTile.FloorHeight + 0.001f)
                    return;

                // V COMES FROM THE ABSOLUTE HEIGHT, not from the position within the wall piece.
                //
                // The original computes the vertical texture position from the world height of
                // the edge: in eighth-tiles that is uv = edge * 0.125, so in our units
                // simply height / 64 - one texture repeat per tile width. The
                // anchor is thus fixed in space and not at the floor of the respective tile.
                //
                // Until 2026-09-06 we computed from CurrentTile.FloorHeight, so every wall
                // started with the bottom edge of the image at itself. On level floor that
                // goes unnoticed, but at every step it shows: the pattern jumped by the step height.
                // The user noticed it at the secret wall on level 3 (48/55) - there
                // the fake wall blended SEAMLESSLY into the wall behind it in our version,
                // while in the original the transition is visible. The fake wall is an
                // object and sits at its own height; only the wall was wrong. The
                // tile has floor height 5, i.e. 80 world units, and 80 mod 64 = 16 pixels -
                // exactly the offset the user had estimated.
                //
                // Whether the original counts from the bottom or from the ceiling does not matter:
                // the ceiling is at 256 units, i.e. exactly four full repeats.
                //
                // V=0 is the BOTTOM EDGE OF THE IMAGE and grows upwards. The textures are stored
                // row-flipped in the Texture2DArray, because Unity's SetPixels32 stores from bottom
                // to top, while the game data delivers row 0 = top.
                //
                // The sloped wall triangles previously used the reverse mapping
                // from the quads (bottom edge 1 instead of 0) and were therefore upside down.
                // Going through the world height removes the difference.
                //
                // OPEN, deliberately left as is: on SLOPED FLOORS the pattern does not fit
                // yet. The world height is right there, but the texture runs along the
                // slope instead of vertically, and the seam to the adjoining wall sits
                // askew. The user looked at it and decided to leave it:
                // "in the original it looks silly anyway" (2026-09-06).
                for (int i = 0; i < uvs.Length; i++)
                {
                    uvs[i].x = uvs[i].x * vScale;
                    // A STEP (a wall that ends below the ceiling, up to a higher floor) STARTS THE
                    // PICTURE AT ITS OWN TOP EDGE (per user on the original, 2026-09-29: the sides
                    // of the Ring of Humility's pedestal on level 5, 16/59, floor 112 to 144, sat
                    // wrong in ours while every other wall fitted). With the absolute height the
                    // step showed v 1.75 to 2.25, the image's top rows at the bottom and a seam in
                    // the middle. For a wall up to the ceiling both rules are the same - the
                    // ceiling lies on a whole repeat - which is why the secret wall of 2026-09-06
                    // fitted either way.
                    if (lbFullWall)
                        uvs[i].y = vertices[i].y / 64f;
                    else
                        uvs[i].y = 1f + ((vertices[i].y - (i == 0 || i == 3 ? lfTop0 : lfTop1)) / 64f);
                }

                fGetChunk(xPos, zPos).Append(miCurrentTileIndex, fGetTileOrigin(xPos, zPos), vertices, indices, uvs, CurrentTile.TextureWall,
                    UWChunkGeometry.KindWall);
            }
        }

        private void fAddDiagonalWall(int xPos, int zPos)
        {
            float top = CeilingHeight;
            float bottom = CurrentTile.FloorHeight;

            Vector3[] vertices = null;
            int[] indices = null;
            Vector2[] uvs = null;
            // A diagonal tile always has a solid half, so its diagonal wall must
            // ALWAYS be drawn - regardless of what lies next to it.
            //
            // Previously this depended on a list of neighbour cases (neighbour solid,
            // neighbour higher, or one of twelve diagonal pairs). The list grew twice
            // because walls were missing at specific spots - and was still
            // incomplete: on level 3 the diagonal tile (41,51) has four open
            // neighbours of the same floor height, not a single case matched, the wall was missing
            // (reported by the user, 2026-08-30). The neighbours are simply irrelevant
            // for this face: the vertices come from the tile itself alone,
            // and so do the heights (bottom is the tile's own floor height, top the
            // ceiling height; fGetHeights is not involved).
            if (CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_ne
                || CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_nw
                || CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_se
                || CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_sw)
            {
                top = CeilingHeight;
                float vScale = 1f;

                vertices = new Vector3[4];

                if (CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_ne)
                {
                    vertices[0] = fCreateVertex(-1, 1, bottom);
                    vertices[1] = fCreateVertex(1, -1, bottom);
                    vertices[2] = fCreateVertex(1, -1, top);
                    vertices[3] = fCreateVertex(-1, 1, top);
                    indices = new int[] { 0, 1, 2, 2, 3, 0 };
                    uvs = new Vector2[] { new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0) };
                }
                else if (CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_nw)
                {
                    vertices[0] = fCreateVertex(1, 1, bottom);
                    vertices[1] = fCreateVertex(-1, -1, bottom);
                    vertices[2] = fCreateVertex(-1, -1, top);
                    vertices[3] = fCreateVertex(1, 1, top);
                    indices = new int[] { 0, 3, 2, 2, 1, 0 };
                    uvs = new Vector2[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
                }
                else if (CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_se)
                {
                    vertices[0] = fCreateVertex(1, 1, bottom);
                    vertices[1] = fCreateVertex(-1, -1, bottom);
                    vertices[2] = fCreateVertex(-1, -1, top);
                    vertices[3] = fCreateVertex(1, 1, top);
                    indices = new int[] { 0, 1, 2, 2, 3, 0 };
                    uvs = new Vector2[] { new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0) };
                }
                else if (CurrentTile.TileType == UWTile.TileTypeEnum.diagonal_sw)
                {
                    vertices[0] = fCreateVertex(-1, 1, bottom);
                    vertices[1] = fCreateVertex(1, -1, bottom);
                    vertices[2] = fCreateVertex(1, -1, top);
                    vertices[3] = fCreateVertex(-1, 1, top);
                    indices = new int[] { 2, 1, 0, 0, 3, 2 };
                    uvs = new Vector2[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
                }

                if (vertices == null || indices == null || uvs == null)
                    return;

                // V COMES FROM THE ABSOLUTE HEIGHT, not from the position within the wall piece.
                //
                // The original computes the vertical texture position from the world height of
                // the edge: in eighth-tiles that is uv = edge * 0.125, so in our units
                // simply height / 64 - one texture repeat per tile width. The
                // anchor is thus fixed in space and not at the floor of the respective tile.
                //
                // Until 2026-09-06 we computed from CurrentTile.FloorHeight, so every wall
                // started with the bottom edge of the image at itself. On level floor that
                // goes unnoticed, but at every step it shows: the pattern jumped by the step height.
                // The user noticed it at the secret wall on level 3 (48/55) - there
                // the fake wall blended SEAMLESSLY into the wall behind it in our version,
                // while in the original the transition is visible. The fake wall is an
                // object and sits at its own height; only the wall was wrong. The
                // tile has floor height 5, i.e. 80 world units, and 80 mod 64 = 16 pixels -
                // exactly the offset the user had estimated.
                //
                // Whether the original counts from the bottom or from the ceiling does not matter:
                // the ceiling is at 256 units, i.e. exactly four full repeats.
                //
                // V=0 is the BOTTOM EDGE OF THE IMAGE and grows upwards. The textures are stored
                // row-flipped in the Texture2DArray, because Unity's SetPixels32 stores from bottom
                // to top, while the game data delivers row 0 = top.
                //
                // The sloped wall triangles previously used the reverse mapping
                // from the quads (bottom edge 1 instead of 0) and were therefore upside down.
                // Going through the world height removes the difference.
                //
                // OPEN, deliberately left as is: on SLOPED FLOORS the pattern does not fit
                // yet. The world height is right there, but the texture runs along the
                // slope instead of vertically, and the seam to the adjoining wall sits
                // askew. The user looked at it and decided to leave it:
                // "in the original it looks silly anyway" (2026-09-06).
                for (int i = 0; i < uvs.Length; i++)
                {
                    uvs[i].x = uvs[i].x * vScale;
                    uvs[i].y = vertices[i].y / 64f;
                }

                fGetChunk(xPos, zPos).Append(miCurrentTileIndex, fGetTileOrigin(xPos, zPos), vertices, indices, uvs, CurrentTile.TextureWall,
                    UWChunkGeometry.KindDiagonal);
            }
        }

        private static Vector3 fGetTileOrigin(int xPos, int zPos)
        {
            return new Vector3(xPos * TileSpacing, 0f, zPos * TileSpacing);
        }

        /// <summary>Floor height of a tile at one of its corners (x and z -1 or 1): a slope is
        /// Slope higher along its rising edge, every other floor is flat.</summary>
        private static float fCornerHeight(UWTile pOTile, int piX, int piZ)
        {
            float lfFloor = pOTile.FloorHeight;
            float lfHigh = lfFloor + pOTile.Slope;

            switch (pOTile.TileType)
            {
                case UWTile.TileTypeEnum.slope_n: return piZ > 0 ? lfHigh : lfFloor;
                case UWTile.TileTypeEnum.slope_s: return piZ < 0 ? lfHigh : lfFloor;
                case UWTile.TileTypeEnum.slope_e: return piX > 0 ? lfHigh : lfFloor;
                case UWTile.TileTypeEnum.slope_w: return piX < 0 ? lfHigh : lfFloor;
                default: return lfFloor;
            }
        }

        private static Vector3 fCreateVertex(int x, int z, float height)
        {
            return new Vector3(x * TileHalfSize, height, z * TileHalfSize);
        }
    }
}
