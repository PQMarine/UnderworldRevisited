using UnityEngine;
using UWDataImport.UWData;

namespace UnderworldRevisited.Build
{
    /// <summary>
    /// Unity side of the relief heights for the Remastered render mode: turns the pixels of a
    /// texture slice into brightness values and hands them to UWHeightMap, where the joint
    /// detection lives engine-free since 2026-09-17 (P1 of the engine separation).
    /// </summary>
    public static class UWHeightMapBuilder
    {
        /// <param name="pyIndices">The palette indices of the same pixels (see
        /// UWTextureArrayBuilder.GetSliceIndices), for detecting water and lava. May be null.</param>
        /// <param name="piWall">The wall texture's number (its slice), -1 for a floor: the second game's walls without dark
        /// joints have them looked for as the lighter (UWHeightMap.LightJointWallsUw2).</param>
        public static UWHeightMap.Result Build(Color32[] pOPixels, byte[] pyIndices, int piWall = -1)
        {
            int liCount = UWHeightMap.Size * UWHeightMap.Size;
            float[] lfLuminance = new float[liCount];

            for (int i = 0; i < liCount && i < pOPixels.Length; i++)
                lfLuminance[i] = ((pOPixels[i].r * 0.299f) + (pOPixels[i].g * 0.587f) + (pOPixels[i].b * 0.114f)) / 255f;

            // The second game's joints are fainter (UWHeightMap.FaintJointStoneLimit).
            bool lbSecondGame = UWGameSession.Current.Game.Game == UWGame.Uw2;
            bool lbLightJoints = lbSecondGame && piWall >= 0 && System.Array.IndexOf(UWHeightMap.LightJointWallsUw2, piWall) >= 0;

            return UWHeightMap.Build(lfLuminance, pyIndices, lbSecondGame, lbLightJoints);
        }
    }
}
