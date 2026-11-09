namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE A NEW GAME STARTS, AS THE PROGRAM SAYS (per user, 2026-11-09, for the randomizer of Karl
	/// Clinckspoor's Ultima Underworld editor, whose "Mark as start" writes it there): the original's main
	/// menu calls player_setup(0x20, 2, 1) after making the character (Exhumed ui/MAINMENU.C), compiled as
	///
	///   6A 01        push 1      (how)
	///   6A yy        push y      (2)
	///   6A xx        push x      (0x20)
	///   9A D1 06 48 00  call far player_setup (0048:06D1)
	///   83 C4 06     add sp, 6
	///
	/// The square is read from those pushes - only where the pattern stands exactly once in the program,
	/// else none. The call's target is part of the pattern: the GOG program holds a second call of the same
	/// shape (push 1, 0x40, 0x40, call far 00C0:0005 at 0x7F967) to another routine; a randomizer changes
	/// the pushes, never the target. Ours started every new character at the rule set's story.start (32/2) until then; a
	/// rule set that changes story.start still wins over the program (UWCharacterGeneration). The first
	/// game only: the second keeps its start in its profile (UWGameProfileUw2).
	/// </summary>
	public static class UWStartSquare
	{
		/// <summary>The square of the first game's unchanged program.</summary>
		public const int OriginalX = 0x20;

		public const int OriginalY = 2;

		/// <summary>player_setup's far address as the call holds it (offset 06D1, segment 0048).</summary>
		private static readonly byte[] msPlayerSetup = { 0xD1, 0x06, 0x48, 0x00 };

		/// <summary>The square from the program's call, false where the call is not found or not unique.</summary>
		public static bool TryRead(byte[] pyProgram, out int piX, out int piY)
		{
			piX = 0;
			piY = 0;

			if (pyProgram == null)
				return false;

			int liFound = -1;

			for (int i = 0; i + 14 <= pyProgram.Length; i++)
			{
				if (pyProgram[i] != 0x6A || pyProgram[i + 1] != 0x01 || pyProgram[i + 2] != 0x6A || pyProgram[i + 4] != 0x6A
					|| pyProgram[i + 6] != 0x9A || pyProgram[i + 11] != 0x83 || pyProgram[i + 12] != 0xC4 || pyProgram[i + 13] != 0x06
					|| pyProgram[i + 7] != msPlayerSetup[0] || pyProgram[i + 8] != msPlayerSetup[1]
					|| pyProgram[i + 9] != msPlayerSetup[2] || pyProgram[i + 10] != msPlayerSetup[3])
					continue;

				// A second call of the same shape: no telling which is the start.
				if (liFound >= 0)
					return false;

				liFound = i;
			}

			if (liFound < 0)
				return false;

			piY = pyProgram[liFound + 3];
			piX = pyProgram[liFound + 5];

			// A level is 64 by 64 squares; a push of more is no square.
			return piX < 64 && piY < 64;
		}
	}
}
