using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// REMASTERED'S RELIEF FROM THE JOINTS (UWHeightMap): the faint joints of the second game (per user 2026-11-20, the
    /// castle's bricks). A made-up wall of bricks 32 pixels wide and 8 high, its upright joints dark and two pixels wide, its level joints
    /// faint: at MortarContrast only the upright ones are found and each column of bricks is one "stone"; with faint
    /// joints asked for the search goes on until the bricks come apart. The first game's call is the plain one.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckHeightMap()
        {
            Check.Group("Remastered relief: joints (UWHeightMap)");

            int liSize = UWHeightMap.Size;
            float[] lfLuminance = new float[liSize * liSize];

            for (int y = 0; y < liSize; y++)
            {
                for (int x = 0; x < liSize; x++)
                {
                    float lfValue = 0.6f;

                    if ((y % 8) == 0)
                        lfValue = 0.48f;

                    if ((x % 32) < 2)
                        lfValue = 0.35f;

                    lfLuminance[(y * liSize) + x] = lfValue;
                }
            }

            UWHeightMap.Result lOPlain = UWHeightMap.Build(lfLuminance, null);
            UWHeightMap.Result lOFirst = UWHeightMap.Build(lfLuminance, null, false);
            UWHeightMap.Result lOFaint = UWHeightMap.Build(lfLuminance, null, true);

            Check.Bool("relief: the plain call is the first game's (no faint joints)",
                UWHeightMap.LargestStoneShare(lOPlain.Mortar) == UWHeightMap.LargestStoneShare(lOFirst.Mortar), true);
            Check.Bool("relief: at the plain contrast a column of bricks is one stone (over the limit)",
                lOFirst.HasMortar && UWHeightMap.LargestStoneShare(lOFirst.Mortar) > UWHeightMap.FaintJointStoneLimit, true);
            Check.Bool("relief: with faint joints the bricks come apart (each under the limit)",
                lOFaint.HasMortar && UWHeightMap.LargestStoneShare(lOFaint.Mortar) <= UWHeightMap.FaintJointStoneLimit, true);
            Check.Bool("relief: ... a level joint is a joint then",
                lOFaint.Mortar[(8 * liSize) + 16] && !lOFirst.Mortar[(8 * liSize) + 16], true);

            // A wall with light joints (LightJointWallsUw2, the second game's 143): the made-up wall turned round - light
            // joints, a dark brick among the bricks - finds the joints and leaves the dark brick a stone.
            float[] lfLight = new float[liSize * liSize];

            for (int y = 0; y < liSize; y++)
            {
                for (int x = 0; x < liSize; x++)
                {
                    float lfValue = (x >= 4 && x < 28 && y >= 2 && y < 7) ? 0.15f : 0.4f;

                    if ((y % 8) == 0 || (x % 32) < 2)
                        lfValue = 0.7f;

                    lfLight[(y * liSize) + x] = lfValue;
                }
            }

            UWHeightMap.Result lODark = UWHeightMap.Build(lfLight, null, true, false);
            UWHeightMap.Result lOLight = UWHeightMap.Build(lfLight, null, true, true);

            Check.Bool("relief: with dark joints looked for, the light lines are no joints", lODark.Mortar[(8 * liSize) + 16], false);
            Check.Bool("relief: with light joints, the light lines are the joints and the dark brick is a stone",
                lOLight.HasMortar && lOLight.Mortar[(8 * liSize) + 16] && !lOLight.Mortar[(4 * liSize) + 10], true);
            Check.Str("relief: the second game's walls with light joints", string.Join(",", UWHeightMap.LightJointWallsUw2), "143");
        }
    }
}
