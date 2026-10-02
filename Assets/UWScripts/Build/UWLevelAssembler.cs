using UnityEngine;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// Assembles the actual GameObjects from the computed chunk data.
    /// Used both by editor baking and at runtime, so that both
    /// paths are guaranteed to produce the same result.
    /// </summary>
    public static class UWLevelAssembler
    {
        public const string DungeonShaderName = "UW/Dungeon";

        /// <summary>Counterpart for the palette renderer. Expects an array of
        /// palette indices from UWTextureArrayBuilder.BuildIndexed.</summary>
        public const string PaletteDungeonShaderName = "UW/DungeonPalette";

        /// <summary>
        /// The material for floor, ceiling and walls.
        ///
        /// Both render paths differ here only in shader and texture name - the
        /// slice layout is the same, so the same meshes fit both. See
        /// UWSettings.RenderModeEnum for everything else that has to follow along when switching.
        /// </summary>
        public static Material CreateMaterial(Texture2DArray pOTextures, string psName,
            UWSettings.RenderModeEnum peMode = UWSettings.RenderModeEnum.Remastered)
        {
            bool lbPalette = peMode == UWSettings.RenderModeEnum.Palette;
            string lsShaderName = lbPalette ? PaletteDungeonShaderName : DungeonShaderName;

            Shader lOShader = Shader.Find(lsShaderName);

            if (lOShader == null)
            {
                Debug.LogError("Shader " + lsShaderName + " not found - geometry stays unpainted.");
                return null;
            }

            Material lOMaterial = new Material(lOShader);
            lOMaterial.name = psName;
            lOMaterial.SetTexture(lbPalette ? "_IndexArray" : "_TexArray", pOTextures);

            return lOMaterial;
        }

        /// <summary>
        /// Builds the chunk objects under a shared root object.
        /// </summary>
        public static GameObject CreateLevelRoot(string psName, UWChunkGeometry[] pOChunks, Material pOMaterial, int piLevelIndex, bool pbAddColliders)
        {
            GameObject lORoot = new GameObject(psName);

            for (int i = 0; i < pOChunks.Length; i++)
            {
                UWChunkGeometry lOChunk = pOChunks[i];

                if (lOChunk.IsEmpty)
                    continue;

                string lsChunkName = string.Format("Chunk {0}_{1}", lOChunk.ChunkX, lOChunk.ChunkZ);

                GameObject lOObject = new GameObject(lsChunkName);
                lOObject.transform.SetParent(lORoot.transform, false);

                Mesh lOMesh = lOChunk.CreateMesh(lsChunkName);

                lOObject.AddComponent<MeshFilter>().sharedMesh = lOMesh;
                lOObject.AddComponent<MeshRenderer>().sharedMaterial = pOMaterial;

                if (pbAddColliders)
                    lOObject.AddComponent<MeshCollider>().sharedMesh = lOMesh;

                lOObject.AddComponent<UWLevelChunk>().Initialise(piLevelIndex, lOChunk.TriangleToTile.ToArray());
            }

            return lORoot;
        }

        /// <summary>Sums triangles over all chunks, for log output.</summary>
        public static int CountTriangles(UWChunkGeometry[] pOChunks)
        {
            int liCount = 0;

            for (int i = 0; i < pOChunks.Length; i++)
                liCount += pOChunks[i].TriangleToTile.Count;

            return liCount;
        }
    }
}
