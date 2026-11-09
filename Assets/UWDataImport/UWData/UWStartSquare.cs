namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE A NEW GAME STARTS, AS THE PROGRAM SAYS (per user, 2026-11-09, for the randomizer of the
	/// Underworld Editor (Avalonia), whose "Mark as start" writes it there): the original's main
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

		/// <summary>
		/// THE HEIGHT A NEW CHARACTER STANDS AT (per user, 2026-11-09: with the editor's start moved the character fell
		/// from the original start's height): player_setup puts the player on the square's floor, z = hgt_val[height]
		/// - 0x40 a step, but 0 for the heights 14 and 15 (a possible bug of the original, Exhumed motion-findings;
		/// no open square of the shipped levels has them, kept as the original) - and half a step more on a slope
		/// (tile_walls & 0x20, the types 6 to 9). In eighths of a z step, as PLAYER.DAT's 0x58 holds it; the
		/// original's 32/2 (height 12, open) gives 0x300, the fixed value until then.
		/// </summary>
		public static int HeightOnTile(int piFloorHeight, int piTileType)
		{
			int liZ = piFloorHeight >= 0 && piFloorHeight <= 13 ? piFloorHeight * 0x40 : 0;

			if (piTileType >= 6 && piTileType <= 9)
				liZ += 0x20;

			return liZ;
		}

		/// <summary>The first level's square in a LEV.ARK as the program reads it: its floor height and type (block 0,
		/// 64 by 64 words of 32 bits, the type in bits 0 to 3, the height in 4 to 7); false where the file is short.</summary>
		public static bool TryReadFirstLevelTile(byte[] pyLevArk, int piX, int piY, out int piFloorHeight, out int piTileType)
		{
			piFloorHeight = 0;
			piTileType = 0;

			if (pyLevArk == null || pyLevArk.Length < 6 || piX < 0 || piY < 0 || piX >= 64 || piY >= 64)
				return false;

			long liBlock = pyLevArk[2] | (pyLevArk[3] << 8) | (pyLevArk[4] << 16) | ((long)pyLevArk[5] << 24);
			long liAt = liBlock + (((piY * 64) + piX) * 4);

			if (liAt + 4 > pyLevArk.Length)
				return false;

			piTileType = pyLevArk[liAt] & 0x0F;
			piFloorHeight = (pyLevArk[liAt] >> 4) & 0x0F;

			return true;
		}
	}
}
