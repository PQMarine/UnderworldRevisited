namespace UWDataImport.UWData
{
	/// <summary>
	/// ONE GAME'S LEV.ARK (Docs/UW2-PLAN.md section 4.2, phase 1 step 2, 2026-10-28): what differs
	/// between the games' level archives - how many levels, which block holds which kind for a level,
	/// how long a level block and a texture map are, and where the second game keeps the animation
	/// overlays and timers (inside the level block, after the part both share). Until then the first
	/// game's numbers were constants of UWLevArkLayout, which keeps what both games share (the tiles,
	/// the objects, the free and active lists, the magic word: UWLevArkLayout.CommonBlockSize bytes).
	///
	/// Evidence (Underworld Exhumed, docs/FORMATS.md "LEV.ARK"): UW1 level.h:39 - map at level - 1,
	/// overlays at + 8, texture map at + 0x11, automap at + 0x1A, notes at + 0x23 (levels 1-based),
	/// 135 blocks; UW2 MAP.C:88, TEXTMAPS.C:73, AUTOMAP.C:186 and :678 - map at level - 1, texture map
	/// at + 0x4F, automap at + 0x9F, notes at + 0xEF, 320 blocks, a level block of 0x7E08 with the
	/// overlays at 0x7C08 and 64 timer words at 0x7D88 (level.h:28-29); texture maps of 0x7A and 0x86
	/// (TEXTMAPS.C:64, UW1 TEXTMAPS.C:118). The first game's 99 note blocks are this port's finding
	/// (2026-09-03, see Uw1MapNoteLevelCount).
	///
	/// The profile names its game's (IUWGameProfile.LevArk); the codecs and the writer take it.
	/// </summary>
	public sealed class UWLevArkFile
	{
		// ------------------------------------------------- The first game

		/// <summary>Levels 1 to 9 are blocks 0 to 8 (LEVARK_MAP).</summary>
		public const int Uw1LevelCount = 9;

		/// <summary>The animation overlays, block 9 + level (LEVARK_ANIM; UWAnimationOverlay).</summary>
		public const int Uw1AnimationFirstBlock = 9;

		/// <summary>The texture maps, block 18 + level (LEVARK_TXM; UWLevel.TextureInfos).</summary>
		public const int Uw1TextureMapFirstBlock = 18;

		/// <summary>The automaps, block 27 + level (LEVARK_AUTOMAP), 0x1000 bytes each. In UW1 the
		/// automap starts at block 27: the 135 blocks are nine levels times fifteen. The 160 from
		/// uw-formats.txt 4.7 applies to Underworld 2, which has 320 blocks.</summary>
		public const int Uw1AutomapFirstBlock = 27;

		/// <summary>The map notes, block 36 + level (LEVARK_NOTES).</summary>
		public const int Uw1MapNoteFirstBlock = 36;

		/// <summary>Ninety-nine note blocks, not only nine: uw-formats.txt calls the later ones
		/// "unused", but the user wrote on levels 10, 46 and 99, saved and reloaded, and the texts
		/// were in exactly these blocks (2026-09-03). 36 plus 99 gives the 135 blocks of the file.</summary>
		public const int Uw1MapNoteLevelCount = 99;

		/// <summary>48 wall and 10 floor entries of 16 bits, then six door bytes (UWLevel.fLoadTextureInfo).</summary>
		public const int Uw1TextureMapSize = 122;

		// ------------------------------------------------- The second game

		public const int Uw2LevelCount = 80;

		public const int Uw2TextureMapFirstBlock = 0x50;

		public const int Uw2AutomapFirstBlock = 0xA0;

		public const int Uw2MapNoteFirstBlock = 0xF0;

		public const int Uw2BlockSize = 0x7E08;

		/// <summary>64 texture numbers in T64.TR, then three words of the six door textures.</summary>
		public const int Uw2TextureMapSize = 0x86;

		/// <summary>The animation overlays inside the level block, right after the magic word.</summary>
		public const int Uw2OverlayOffset = 0x7C08;

		public const int Uw2TimerOffset = 0x7D88;

		public const int Uw2TimerCount = 64;

		/// <summary>The first game's archive.</summary>
		public static readonly UWLevArkFile Uw1 = new UWLevArkFile(Uw1LevelCount, Uw1AnimationFirstBlock, Uw1TextureMapFirstBlock,
			Uw1AutomapFirstBlock, Uw1MapNoteFirstBlock, Uw1MapNoteLevelCount, UWLevArkLayout.CommonBlockSize, Uw1TextureMapSize, -1, -1, 0);

		/// <summary>The second game's archive.</summary>
		public static readonly UWLevArkFile Uw2 = new UWLevArkFile(Uw2LevelCount, -1, Uw2TextureMapFirstBlock,
			Uw2AutomapFirstBlock, Uw2MapNoteFirstBlock, Uw2LevelCount, Uw2BlockSize, Uw2TextureMapSize, Uw2OverlayOffset, Uw2TimerOffset, Uw2TimerCount);

		private readonly int miAnimationFirstBlock;

		private readonly int miTextureMapFirstBlock;

		private readonly int miAutomapFirstBlock;

		private readonly int miMapNoteFirstBlock;

		private UWLevArkFile(int piLevelCount, int piAnimationFirstBlock, int piTextureMapFirstBlock, int piAutomapFirstBlock,
			int piMapNoteFirstBlock, int piMapNoteLevelCount, int piBlockSize, int piTextureMapSize, int piOverlayOffset, int piTimerOffset,
			int piTimerCount)
		{
			LevelCount = piLevelCount;
			miAnimationFirstBlock = piAnimationFirstBlock;
			miTextureMapFirstBlock = piTextureMapFirstBlock;
			miAutomapFirstBlock = piAutomapFirstBlock;
			miMapNoteFirstBlock = piMapNoteFirstBlock;
			MapNoteLevelCount = piMapNoteLevelCount;
			BlockSize = piBlockSize;
			TextureMapSize = piTextureMapSize;
			OverlayOffset = piOverlayOffset;
			TimerOffset = piTimerOffset;
			TimerCount = piTimerCount;
		}

		/// <summary>The levels, each with a map block from block 0.</summary>
		public int LevelCount { get; }

		/// <summary>The levels with a note block (the first game: 99, see Uw1MapNoteLevelCount).</summary>
		public int MapNoteLevelCount { get; }

		/// <summary>The blocks the file declares: the notes are the last kind.</summary>
		public int DeclaredBlockCount => miMapNoteFirstBlock + MapNoteLevelCount;

		/// <summary>A level block's length: the shared part, and the second game's overlays and timers.</summary>
		public int BlockSize { get; }

		public int TextureMapSize { get; }

		/// <summary>Where the animation overlays lie inside a level block; -1 when they are a block of
		/// their own (AnimationBlock).</summary>
		public int OverlayOffset { get; }

		/// <summary>Where the timers lie inside a level block; -1 without them.</summary>
		public int TimerOffset { get; }

		public int TimerCount { get; }

		/// <summary>The first block of the notes - the kinds after the levels' maps lie in this order.</summary>
		public int MapNoteFirstBlock => miMapNoteFirstBlock;

		/// <summary>The block of a kind for a 0-based level index.</summary>
		public int MapBlock(int piLevelIndex)
		{
			return piLevelIndex;
		}

		/// <summary>The overlay block of a level; -1 where the overlays are inside the level block.</summary>
		public int AnimationBlock(int piLevelIndex)
		{
			return miAnimationFirstBlock < 0 ? -1 : miAnimationFirstBlock + piLevelIndex;
		}

		public int TextureMapBlock(int piLevelIndex)
		{
			return miTextureMapFirstBlock + piLevelIndex;
		}

		public int AutomapBlock(int piLevelIndex)
		{
			return miAutomapFirstBlock + piLevelIndex;
		}

		public int MapNoteBlock(int piLevelIndex)
		{
			return miMapNoteFirstBlock + piLevelIndex;
		}

		/// <summary>The level whose map is this block, or -1.</summary>
		public int MapLevelOf(int piBlock)
		{
			return piBlock >= 0 && piBlock < LevelCount ? piBlock : -1;
		}

		/// <summary>The level whose overlay block this is, or -1 (always for the second game).</summary>
		public int AnimationLevelOf(int piBlock)
		{
			return fLevelOf(piBlock, miAnimationFirstBlock, LevelCount);
		}

		public int AutomapLevelOf(int piBlock)
		{
			return fLevelOf(piBlock, miAutomapFirstBlock, LevelCount);
		}

		public int MapNoteLevelOf(int piBlock)
		{
			return fLevelOf(piBlock, miMapNoteFirstBlock, MapNoteLevelCount);
		}

		private static int fLevelOf(int piBlock, int piFirst, int piCount)
		{
			return piFirst >= 0 && piBlock >= piFirst && piBlock < piFirst + piCount ? piBlock - piFirst : -1;
		}
	}
}
