using System.Collections.Generic;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    public static partial class SelfChecks
    {
        /// <summary>
        /// The path walker of the checks (UWTilePath.IWalker), without a level: every tile open
        /// except the listed walls, and water refused to a land creature and needed by a swimmer -
        /// the water half of UWCritter.CanEnterTile, so the path search can be pinned without a
        /// level. A flier crosses the water. One walker since cleanup 9.1 (2026-10-25), which
        /// merged the water walker of the brain checks and the grid walker of the path and sleep
        /// checks.
        /// </summary>
        private sealed class FakeWalker : UWTilePath.IWalker
        {
            public readonly bool[,] Water = new bool[64, 64];

            /// <summary>Wall tiles as y * 64 + x.</summary>
            public readonly HashSet<int> Walls = new HashSet<int>();

            public bool Swimmer;

            public bool Flier = false;

            public bool CanEnterTile(int piFromX, int piFromY, int piToX, int piToY)
            {
                if (Walls.Contains((piToY * 64) + piToX))
                    return false;

                if (Flier)
                    return true;

                bool lbWater = piToX >= 0 && piToY >= 0 && piToX < 64 && piToY < 64 && Water[piToX, piToY];

                return Swimmer == lbWater;
            }
        }
    }
}
