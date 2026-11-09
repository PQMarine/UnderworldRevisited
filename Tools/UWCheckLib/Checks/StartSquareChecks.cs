using System.IO;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE NEW-GAME SQUARE FROM THE PROGRAM (per user, 2026-11-09, for the editor's randomizer): the
    /// player's own UW.EXE holds player_setup(0x20, 2, 1) once (beside a call of the same shape to another
    /// routine, told apart by the target) and gives 32/2; a copy with the pushes
    /// changed gives the new square, one with the call twice none; a new character starts at the
    /// program's square, and a rule set that changed story.start wins.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckStartSquare(DataImport pOData)
        {
            Check.Group("New game: the start square from the program (UWStartSquare)");

            string lsRoot = Path.GetDirectoryName(pOData.GameData.DataPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            byte[] lyProgram = File.ReadAllBytes(Path.Combine(lsRoot, "UW.EXE"));

            Check.Str("start: the player's UW.EXE says 32/2",
                UWStartSquare.TryRead(lyProgram, out int liX, out int liY) + " " + liX + "/" + liY, "True 32/2");

            // A moved start, as the editor writes it: the y and x pushes changed.
            byte[] lyMoved = (byte[])lyProgram.Clone();
            int liAt = fFindStartCall(lyMoved);

            lyMoved[liAt + 3] = 40;
            lyMoved[liAt + 5] = 12;

            Check.Str("start: pushes changed to 12/40 read as 12/40",
                UWStartSquare.TryRead(lyMoved, out liX, out liY) + " " + liX + "/" + liY, "True 12/40");

            // The call twice: no telling which, none.
            byte[] lyTwice = new byte[lyProgram.Length + 14];

            System.Array.Copy(lyProgram, lyTwice, lyProgram.Length);
            System.Array.Copy(lyProgram, liAt, lyTwice, lyProgram.Length, 14);
            Check.Bool("start: the call twice gives none", UWStartSquare.TryRead(lyTwice, out _, out _), false);

            // A new character: the program's square, the rule set's where it was changed.
            UWCharacterGeneration lOGeneration = new UWCharacterGeneration(pOData.Strings, pOData.MiscDataFiles)
            {
                ProgramStartX = 12,
                ProgramStartY = 40
            };
            UWPlayerData.NewCharacter lOCharacter = lOGeneration.Result();

            Check.Str("start: a new character starts at the program's square", lOCharacter.StartTileX + "/" + lOCharacter.StartTileY, "12/40");

            // The rule set's story.start changed (on the session's rules, put back afterwards).
            UWRulesStory.StartRules lOStart = UWRules.Current.Story.Start;
            int liOldX = lOStart.TileX;
            int liOldY = lOStart.TileY;

            try
            {
                lOStart.TileX = 20;
                lOStart.TileY = 21;

                UWPlayerData.NewCharacter lORuled = lOGeneration.Result();

                Check.Str("start: a rule set that changed story.start wins", lORuled.StartTileX + "/" + lORuled.StartTileY, "20/21");
            }
            finally
            {
                lOStart.TileX = liOldX;
                lOStart.TileY = liOldY;
            }
        }

        private static int fFindStartCall(byte[] pyProgram)
        {
            for (int i = 0; i + 14 <= pyProgram.Length; i++)
            {
                if (pyProgram[i] == 0x6A && pyProgram[i + 1] == 0x01 && pyProgram[i + 2] == 0x6A && pyProgram[i + 4] == 0x6A
                    && pyProgram[i + 6] == 0x9A && pyProgram[i + 7] == 0xD1 && pyProgram[i + 8] == 0x06
                    && pyProgram[i + 11] == 0x83 && pyProgram[i + 12] == 0xC4 && pyProgram[i + 13] == 0x06)
                    return i;
            }

            return -1;
        }
    }
}
