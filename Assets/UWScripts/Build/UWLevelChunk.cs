using UnityEngine;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// Sits on a merged chunk mesh and restores the link from the hit
    /// triangle to the tile.
    ///
    /// Previously every face was its own GameObject with its own UWEntityInfo, so a
    /// raycast returned the tile directly. After merging, the raycast only returns
    /// a triangle index within the chunk - this table translates it back.
    /// </summary>
    public sealed class UWLevelChunk : MonoBehaviour
    {
        [SerializeField]
        private int miLevelIndex;

        [SerializeField]
        private int[] miTriangleToTile;

        public int LevelIndex
        {
            get { return miLevelIndex; }
        }

        public void Initialise(int piLevelIndex, int[] piTriangleToTile)
        {
            miLevelIndex = piLevelIndex;
            miTriangleToTile = piTriangleToTile;
        }

        /// <summary>
        /// Returns the tile index for the hit triangle, or -1 if the index does not
        /// fit. The value comes from RaycastHit.triangleIndex.
        /// </summary>
        public int GetTileIndex(int piTriangleIndex)
        {
            if (miTriangleToTile == null || piTriangleIndex < 0 || piTriangleIndex >= miTriangleToTile.Length)
                return -1;

            return miTriangleToTile[piTriangleIndex];
        }
    }
}
