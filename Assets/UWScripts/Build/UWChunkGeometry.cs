using System.Collections.Generic;
using UnityEngine;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// Raw data of a chunk mesh. Deliberately without any reference to UnityEngine.Object, so
    /// that the same build code can run in the editor (baking, preview) and at runtime.
    /// </summary>
    public sealed class UWChunkGeometry
    {
        /// <summary>Edge length of a chunk in tiles.</summary>
        public const int ChunkSizeInTiles = 16;

        /// <summary>Chunks per axis at 64x64 tiles.</summary>
        public const int ChunksPerAxis = UWLevelMeshBuilder.TilesPerAxis / ChunkSizeInTiles;

        public readonly int ChunkX;
        public readonly int ChunkZ;

        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();

        /// <summary>xy = texture coordinate, z = slice in the Texture2DArray.</summary>
        public readonly List<Vector3> TexCoords = new List<Vector3>();

        public readonly List<int> Triangles = new List<int>();

        /// <summary>
        /// Per vertex: the tile the face belongs to (x, z) and its kind (UV channel 1) - what the
        /// palette path needs to paint in the original's order (Assets/UWShaders/UWPainterOrder.hlsl).
        /// A wall belongs to the open tile it bounds, as in the original. Kinds, see the constants.
        /// </summary>
        public readonly List<Vector3> TileInfo = new List<Vector3>();

        public const int KindFloor = 1;

        public const int KindCeiling = 2;

        public const int KindWall = 3;

        public const int KindDiagonal = 4;

        /// <summary>
        /// Per triangle, the tile index it comes from. Without this table, after merging
        /// the tiles into chunks it would no longer be possible to tell which
        /// wall the player is looking at - previously that was one GameObject per face.
        /// </summary>
        public readonly List<int> TriangleToTile = new List<int>();

        public bool IsEmpty
        {
            get { return Triangles.Count == 0; }
        }

        public UWChunkGeometry(int piChunkX, int piChunkZ)
        {
            ChunkX = piChunkX;
            ChunkZ = piChunkZ;
        }

        /// <summary>
        /// Appends a face. Vertices, indices and UVs are taken unchanged from the
        /// build code, only the index offset is added. Normals are derived
        /// from the triangle faces; since no vertices are shared between faces,
        /// this yields the desired hard edges.
        /// </summary>
        public void Append(int piTileIndex, Vector3 pOOrigin, Vector3[] pOVertices, int[] piIndices, Vector2[] pOUVs, int piSlice,
            int piKind)
        {
            int liBase = Positions.Count;

            for (int i = 0; i < pOVertices.Length; i++)
            {
                Positions.Add(pOOrigin + pOVertices[i]);
                TexCoords.Add(new Vector3(pOUVs[i].x, pOUVs[i].y, piSlice));
                TileInfo.Add(new Vector3(piTileIndex % UWLevelMeshBuilder.TilesPerAxis, piTileIndex / UWLevelMeshBuilder.TilesPerAxis, piKind));
                Normals.Add(Vector3.zero);
            }

            for (int i = 0; i < piIndices.Length; i += 3)
            {
                int a = liBase + piIndices[i];
                int b = liBase + piIndices[i + 1];
                int c = liBase + piIndices[i + 2];

                Triangles.Add(a);
                Triangles.Add(b);
                Triangles.Add(c);
                TriangleToTile.Add(piTileIndex);

                Vector3 lONormal = Vector3.Cross(Positions[b] - Positions[a], Positions[c] - Positions[a]);
                Normals[a] += lONormal;
                Normals[b] += lONormal;
                Normals[c] += lONormal;
            }
        }

        /// <summary>Creates the finished mesh. Only here is a UnityEngine.Object created.</summary>
        public Mesh CreateMesh(string psName)
        {
            for (int i = 0; i < Normals.Count; i++)
            {
                Normals[i] = Normals[i].sqrMagnitude > 0f ? Normals[i].normalized : Vector3.up;
            }

            Mesh lOMesh = new Mesh();
            lOMesh.name = psName;

            // 4096 tiles per level do not overflow 16-bit indices, but the chunk size
            // is configurable - better to switch as a precaution than to silently get
            // wrong geometry later.
            if (Positions.Count > 65000)
                lOMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            lOMesh.SetVertices(Positions);
            lOMesh.SetNormals(Normals);
            lOMesh.SetUVs(0, TexCoords);
            lOMesh.SetUVs(1, TileInfo);
            lOMesh.SetTriangles(Triangles, 0);
            lOMesh.RecalculateBounds();

            return lOMesh;
        }
    }
}
