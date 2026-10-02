using System.Collections.Generic;

namespace UWDataImport.UWData
{
    /// <summary>
    /// Lightweight replacement for UnityEngine.Vector3 - this assembly deliberately stays
    /// engine-independent (see UWDataImport.asmdef, noEngineReferences).
    /// </summary>
    public struct UWVector3
    {
        public float X;
        public float Y;
        public float Z;

        public UWVector3(float pfX, float pfY, float pfZ)
        {
            X = pfX;
            Y = pfY;
            Z = pfZ;
        }

        public static UWVector3 operator +(UWVector3 pA, UWVector3 pB)
        {
            return new UWVector3(pA.X + pB.X, pA.Y + pB.Y, pA.Z + pB.Z);
        }

        public override string ToString()
        {
            return string.Format("({0:F3}, {1:F3}, {2:F3})", X, Y, Z);
        }
    }

    /// <summary>Lightweight replacement for UnityEngine.Vector2, see UWVector3.</summary>
    public struct UWVector2
    {
        public float X;
        public float Y;

        public UWVector2(float pfX, float pfY)
        {
            X = pfX;
            Y = pfY;
        }
    }

    /// <summary>
    /// A 3D model interpreted from the original bytecode (door frame, bridge,
    /// bench, chest, shrine/ankh, ...). See UW3DModelImport for the parser.
    /// </summary>
    public class UW3DModel
    {
        public List<UWVector3> Vertices = new List<UWVector3>();

        public List<UW3DModelFace> Faces = new List<UW3DModelFace>();

        /// <summary>Size of the model's cull box (node 78: twice its half extents), in tiles,
        /// order X, height, Y. Diagnostics only.</summary>
        public UWVector3 Extents;

        /// <summary>Centre vertex of the model's cull box (node 78), in tiles, order X, height,
        /// Y. Not subtracted from the vertices. Diagnostics only.</summary>
        public UWVector3 Center;

        /// <summary>Opcodes at which execution stopped because they are not supported.
        /// Empty if everything was recognised. Diagnostics only.</summary>
        public List<int> UnknownNodeIds = new List<int>();

        /// <summary>
        /// Dense vertex indices (keys in Vertices) that according to node 008C ("vertex
        /// variable height") should reach up to the ceiling - pillars and door frames that
        /// adapt to the respective room height in the original. Which ceiling height that actually
        /// is only becomes known at runtime (current tile), not in this engine-independent
        /// parser - so here they are only marked, not yet stretched.
        /// </summary>
        public System.Collections.Generic.HashSet<int> CeilingVertexIndices = new System.Collections.Generic.HashSet<int>();

        /// <summary>
        /// Dense vertex indices (keys in Vertices) with a brightness value from node
        /// 00D4 ("define vertex shading values") - 0 = darkest, 255 = brightest level (direction
        /// experimental, see UWObjectSpawner.fSpawn3DModel). Only effective on faces for
        /// which UW3DModelFace.IsGouraud is set (node 00D6 active when the bytecode defined the
        /// face) - otherwise the face stays single-coloured (ColorIndex).
        /// </summary>
        public System.Collections.Generic.Dictionary<int, byte> VertexDarkValues = new System.Collections.Generic.Dictionary<int, byte>();
    }

    public class UW3DModelFace
    {
        public List<int> VertexIndices = new List<int>();

        /// <summary>UV per vertex, same order as VertexIndices. Null if untextured.</summary>
        public List<UWVector2> Uvs;

        /// <summary>Texture number according to the model data, -1 if none (single-coloured face).</summary>
        public int TextureNumber = -1;

        /// <summary>
        /// Palette index that was last set by a colour node (0014/00BC/00BE/00D4/0016) during
        /// parsing when this face was defined - see UW3DModelImport (colour records from UW.EXE).
        /// Only relevant for faces without Uvs (single-coloured); unused for textured faces.
        /// </summary>
        public int ColorIndex;

        /// <summary>
        /// Only valid (not -1) if IsGouraud is set: the "other" table slot in
        /// the UW3DModelImport colour record of this model (slot+1, with wraparound) - the
        /// counter colour between which and ColorIndex dithering should happen per vertex
        /// brightness (VertexDarkValues). The original seems to use real dithering between two
        /// palette colours instead of a smooth brightness gradient on one colour -
        /// confirmed on the chest (0xE6 wood brown <-> 0x68 blue grey).
        /// </summary>
        public int AltColorIndex = -1;

        /// <summary>Set if node 00D6 ("introduce Gouraud shaded face") was active when
        /// this face was defined - then according to UWFileInfo.txt it should show a brightness
        /// gradient across the vertices (see UW3DModel.VertexDarkValues) instead of a
        /// single face colour.</summary>
        public bool IsGouraud;

        /// <summary>Shade level of a flat face (opcode BC): UW.EXE draws the colour through its
        /// shade table at this level (plus the global light), 0 = unchanged. Always 0 on
        /// Gouraud faces, whose shades come per vertex (UW3DModel.VertexDarkValues).</summary>
        public int ShadeLevel;
    }
}
