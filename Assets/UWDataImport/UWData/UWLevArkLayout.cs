namespace UWDataImport.UWData
{
	/// <summary>
	/// THE LAYOUT OF LEV.ARK, in one engine-free place (cleanup phase 2, step "Codecs",
	/// 2026-10-12). Until then the block numbers stood as literals in DataImport (9, 18, 27,
	/// 36), the offsets inside a level block twice under different names (UWLevel for reading,
	/// UWLevelWriter for writing), and the free-list and active-list rules in both. Evidence:
	/// the original's struct LevelBlock and its LEVARK_* block numbers (level.h), Map_Load and
	/// Map_Save (MAP.C) for the three counts before the magic word, Obj_Alloc, Obj_Free,
	/// active_critter and free_critter (OBJECTS.C) for the lists themselves.
	///
	/// THE FILE: a 16-bit block count, then one 32-bit start per block (0 means the block does
	/// not exist), then the blocks. UW1 declares 135 blocks, five kinds of nine levels each -
	/// the level blocks (tiles and objects), the animation overlays, the texture maps, the
	/// automaps and the first nine of the ninety-nine note blocks. The shipped DATA file holds
	/// only the first three kinds; automaps and notes are made when a game is saved.
	///
	/// A LEVEL BLOCK (0x7C08 bytes):
	///
	///   0x0000  64 x 64 tiles, four bytes each
	///   0x4000  256 mobile objects, 27 bytes each (8 like every object, then 19 extra)
	///   0x5B00  768 static objects, 8 bytes each
	///   0x7300  free list of the mobile slots, 16 bits per entry (254 entries, slots 2 to 255)
	///   0x74FC  free list of the static slots, 16 bits per entry (768 entries)
	///   0x7AFC  list of the active mobile objects, one byte per entry (260 entries)
	///   0x7C00  number of active mobile objects
	///   0x7C02  topmost used entry of the mobile free list (-1 means empty)
	///   0x7C04  the same for the static free list
	///   0x7C06  the magic word "uw"
	///
	/// THE FREE LISTS ARE STACKS: Obj_Alloc takes the entry at the top pointer and moves the
	/// pointer down, Obj_Free moves it up and stores the freed slot there. The pointer is
	/// stored as the index of the top entry, so a count is the entries less one (level.h). THE
	/// ACTIVE LIST holds the mobile slots in use in the order they were allocated:
	/// active_critter appends, free_critter moves the last entry into the hole (see
	/// UWLevArkCodec). The port keeps that order because the original's whoami searches walk
	/// it (UWLevel.GetActiveMobileOrder).
	///
	/// File-format constants stay const in code (plan, ground rule 5).
	///
	/// BOTH GAMES (UW2 plan phase 1 step 2, 2026-10-28): the level block's first 0x7C08 bytes have this
	/// layout in both (Underworld Exhumed, docs/FORMATS.md "The level block"); what differs - the
	/// blocks of the file, the level count, the second game's overlays and timers after the magic
	/// word - is UWLevArkFile's, one instance per game.
	/// </summary>
	public static class UWLevArkLayout
	{
		// ------------------------------------------------- Blocks of the file
		//
		// Which block holds which kind, the level count, the level block's and the texture map's
		// length are each game's (UWLevArkFile, UW2 plan phase 1 step 2, 2026-10-28; constants of this
		// class until then). What follows here both games share.

		/// <summary>One automap byte per tile.</summary>
		public const int AutomapSize = TileCount;

		/// <summary>A map note: 50 bytes of text, then X and Y as 16-bit numbers.</summary>
		public const int MapNoteRecordSize = 54;

		public const int MapNoteTextSize = 50;

		public const int MapNoteXOffset = 0x32;

		public const int MapNoteYOffset = 0x34;

		// ------------------------------------------------- A level block

		/// <summary>The part of a level block both games share, ending with the magic word: the
		/// first game's whole block; the second's carries its overlays and timers after it
		/// (UWLevArkFile.BlockSize).</summary>
		public const int CommonBlockSize = 0x7C08;

		public const int TileCount = UWWorldScale.TilesPerAxis * UWWorldScale.TilesPerAxis;

		public const int TileRecordSize = 4;

		/// <summary>The tile map: 0x4000 bytes, so the mobile objects start right after it.</summary>
		public const int TileMapSize = TileCount * TileRecordSize;

		/// <summary>Bits 22-31 of a tile's word: the first object of its chain (UWTile).</summary>
		public const int TileFirstObjectShift = 22;

		public const int MobileBase = 0x4000;

		public const int MobileRecordSize = 27;

		public const int MobileCount = 256;

		public const int StaticBase = 0x5B00;

		public const int StaticRecordSize = 8;

		public const int StaticCount = 768;

		/// <summary>Objects are numbered 0 to 1023: 256 mobile, then 768 static.</summary>
		public const int ObjectCount = MobileCount + StaticCount;

		/// <summary>The two object lists together, 0x3300 bytes from MobileBase.</summary>
		public const int ObjectListSize = (MobileCount * MobileRecordSize) + (StaticCount * StaticRecordSize);

		public const int MobileFreeListOffset = 0x7300;

		/// <summary>Slots 2 to 255 can be free: 254 entries (mobfree[0xFE]).</summary>
		public const int MobileFreeListCapacity = 0xFE;

		public const int StaticFreeListOffset = 0x74FC;

		public const int StaticFreeListCapacity = StaticCount;

		public const int ActiveMobileListOffset = 0x7AFC;

		/// <summary>active[0x104]: four more than the mobile slots, up to the count word.</summary>
		public const int ActiveMobileListCapacity = 0x104;

		public const int ActiveMobileCountOffset = 0x7C00;

		public const int MobileFreeTopOffset = 0x7C02;

		public const int StaticFreeTopOffset = 0x7C04;

		public const int MagicOffset = 0x7C06;

		/// <summary>"uw": Map_Load refuses a block without it.</summary>
		public const int Magic = 0x7577;

		/// <summary>Slot 0 is "no object", slot 1 the player character. Neither is ever
		/// allocated or released; the mobile free list starts at slot 2.</summary>
		public const int NullSlot = 0;

		public const int PlayerSlot = 1;

		public const int FirstFreeMobileSlot = 2;

		/// <summary>The player's record: 27 bytes from the second mobile record (see
		/// UWPlayerData.PlayerObjectOffset for the copy in PLAYER.DAT).</summary>
		public const int PlayerSlotOffset = MobileBase + (PlayerSlot * MobileRecordSize);

		public static bool IsMobileSlot(int piIndex)
		{
			return piIndex >= 0 && piIndex < MobileCount;
		}

		public static bool IsStaticSlot(int piIndex)
		{
			return piIndex >= MobileCount && piIndex < ObjectCount;
		}

		/// <summary>Where an object's record starts inside the block.</summary>
		public static int ObjectOffset(int piIndex)
		{
			return piIndex < MobileCount
				? MobileBase + (piIndex * MobileRecordSize)
				: StaticBase + ((piIndex - MobileCount) * StaticRecordSize);
		}
	}
}
