using System;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE POINTER SCHEME'S VALUES (seg034_2F89_4E, 2026-10-06): UWPlayerMotion.PointerCommand
    /// over the 172 by 113 area - the bottom fifth's three commands, the turn at the side edges,
    /// the walk at the top, the still middle - and which buttons drive or jump.
    /// </summary>
    public static partial class Program
    {
        private static void fExpectPointer(string psName, int piX, int piY, UWPlayerMotion.Command peCommand, int piWalk, int piTurn)
        {
            UWPlayerMotion.Command leCommand;
            int liWalk;
            int liTurn;

            UWPlayerMotion.PointerCommand(piX, piY, UWPlayerMotion.PointerAreaWidth, UWPlayerMotion.PointerAreaHeight,
                out leCommand, out liWalk, out liTurn);

            fExpectInt(psName + ": command", (int)leCommand, (int)peCommand);
            fExpectInt(psName + ": walk", liWalk, piWalk);
            fExpectInt(psName + ": turn", liTurn, piTurn);
        }

        private static void fCheckPointerScheme(DataImport pOData)
        {
            Console.WriteLine();
            Console.WriteLine("Pointer scheme (seg034_2F89_4E)");

            UWPlayerMotion.Command leSlide = UWPlayerMotion.Command.SlideLeft;
            UWPlayerMotion.Command leBack = UWPlayerMotion.Command.Back;
            UWPlayerMotion.Command leRight = UWPlayerMotion.Command.SlideRight;
            UWPlayerMotion.Command leWalk = UWPlayerMotion.Command.Walk;

            fExpectPointer("pointer: bottom fifth, left third slides left", 10, 5, leSlide, 0, 0);
            fExpectPointer("pointer: ... the middle goes back", 86, 21, leBack, 0, 0);
            fExpectPointer("pointer: ... the right third slides right", 171, 0, leRight, 0, 0);
            fExpectPointer("pointer: the middle of the turn band stands still", 86, 30, leWalk, 0, 0);
            fExpectPointer("pointer: the left edge of the turn band: -(57 * 384) / 172", 0, 22, leWalk, 0, -127);
            fExpectPointer("pointer: ... the right edge: (171 - 114) * 384 / 172", 171, 45, leWalk, 0, 127);
            fExpectPointer("pointer: ... the first pixel past the right third", 115, 40, leWalk, 0, 2);
            fExpectPointer("pointer: the top middle runs: (112 - 45) * 192 / 113", 86, 112, leWalk, 113, 0);
            fExpectPointer("pointer: ... the first pixel above two fifths", 86, 46, leWalk, 1, 0);
            fExpectPointer("pointer: top left walks and turns", 0, 112, leWalk, 113, -127);
            fExpectPointer("pointer: mid right", 140, 80, leWalk, 59, 58);

            fExpectBool("pointer: the left button alone drives", UWPlayerMotion.PointerDrives(1, false), true);
            fExpectBool("pointer: both buttons do not, outside the fight mode", UWPlayerMotion.PointerDrives(3, false), false);
            fExpectBool("pointer: ... but do in it", UWPlayerMotion.PointerDrives(3, true), true);
            fExpectBool("pointer: the right one alone never drives", UWPlayerMotion.PointerDrives(2, true), false);

            if (pOData == null || pOData.CommonObjectProperties == null)
                return;

            FakeMotionWorld lOWorld = new FakeMotionWorld();
            UWPlayerMotion lOPlayer = fMakePlayer(lOWorld, pOData, 8, 8);

            fExpectInt("pointer: both buttons on the ground jump", (int)lOPlayer.PointerJumpCommand, (int)UWPlayerMotion.Command.Jump);
            lOPlayer.MotionCommand = lOPlayer.PointerJumpCommand;
            lOPlayer.Frame(16);
            fExpectInt("pointer: ... in the air they walk", (int)lOPlayer.PointerJumpCommand, (int)UWPlayerMotion.Command.Walk);
        }
    }
}
