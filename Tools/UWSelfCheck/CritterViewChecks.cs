using System;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE CREATURE PICTURE'S VIEW AND SLOT (NPC_seg032_2DCA_216, 2026-10-06): the uneven table
    /// at dseg 6C2, the facing against the camera's yaw, which views show the target-relative
    /// pages, the slot numbers of the direction sets.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckCritterView()
        {
            Console.WriteLine();
            Console.WriteLine("Creature picture view (NPC_seg032_2DCA_216)");

            int[] liCount = new int[8];

            foreach (int liView in UWCritterAnimations.ViewByAngle)
                liCount[liView]++;

            fExpectInt("view: the table has 32 steps", UWCritterAnimations.ViewByAngle.Length, 32);
            fExpectInt("view: the back takes five steps", liCount[0], 5);
            fExpectInt("view: ... the front five", liCount[4], 5);
            fExpectInt("view: ... a diagonal three", liCount[3], 3);

            fExpectInt("view: facing the way the camera looks shows the back", UWCritterAnimations.GetViewDirection(0, 0), 0);
            fExpectInt("view: facing against it shows the front", UWCritterAnimations.GetViewDirection(4, 0), 4);
            fExpectInt("view: ... still the front with the camera 22.5 degrees off", UWCritterAnimations.GetViewDirection(4, 0x1000), 4);
            fExpectInt("view: ... a diagonal at 33.75 degrees", UWCritterAnimations.GetViewDirection(4, 0x1800), 3);
            fExpectInt("view: ... and the other one the other way", UWCritterAnimations.GetViewDirection(4, 0xE800), 5);
            fExpectInt("view: facing east with the camera north shows the side", UWCritterAnimations.GetViewDirection(2, 0), 2);

            fExpectBool("view: the front shows the combat pages", UWCritterAnimations.ShowsTargetPage(4), true);
            fExpectBool("view: ... and both front diagonals", UWCritterAnimations.ShowsTargetPage(3) && UWCritterAnimations.ShowsTargetPage(5), true);
            fExpectBool("view: ... not the sides", UWCritterAnimations.ShowsTargetPage(2) || UWCritterAnimations.ShowsTargetPage(6), false);
            fExpectBool("view: ... nor the back ones", UWCritterAnimations.ShowsTargetPage(0) || UWCritterAnimations.ShowsTargetPage(1) || UWCritterAnimations.ShowsTargetPage(7), false);

            fExpectInt("view: walking seen from the right-back is slot 0x81", UWCritterAnimations.GetSlot(0x2C, 1), 0x81);
            fExpectInt("view: standing seen from the left is 0x26", UWCritterAnimations.GetSlot(0x20, 6), 0x26);
            fExpectInt("view: backing off seen half from the front is page 7", UWCritterAnimations.GetSlot(7, 3), 7);
            fExpectInt("view: ... from the side the standing picture", UWCritterAnimations.GetSlot(7, 2), 0x22);
            fExpectInt("view: dying is seen from behind too", UWCritterAnimations.GetSlot(0x0C, 0), 0x0C);
            fExpectInt("view: a swing from the front is its page", UWCritterAnimations.GetSlot(2, 4), 2);
        }
    }
}
