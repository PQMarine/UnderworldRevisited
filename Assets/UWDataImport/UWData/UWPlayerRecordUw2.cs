namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S PLAYER RECORD AS THE WALK USES IT (Docs/UW2-PLAN.md phase 2 step 6, 2026-10-29):
	/// the fields of PLAYER.DAT's deciphered record (UWPlayerFile.Record) the walk reads and writes, at the
	/// offsets of Underworld Exhumed's struct Player (uw2 include/player.h:20) - which agree with the first
	/// game's up to 0x5D: the name from 0, the 20 skills from 0x21 (Swimming the last, 0x34), and where the
	/// game was saved, saved_x and saved_y (PN's x and y: the tile in the high byte, the fine position in the
	/// low), saved_z (PN's z, the first game's ZPosition), saved_facing and saved_level (1-based) from 0x54.
	/// The rest of the record and the workspace are kept as read: what they hold is the second game's
	/// character, which comes with its rules and interface.
	/// </summary>
	public static class UWPlayerRecordUw2
	{
		public const int SkillsOffset = 0x21;

		public const int SwimmingSkill = 19;

		public const int PositionXOffset = 0x54;

		public const int PositionYOffset = 0x56;

		public const int ZPositionOffset = 0x58;

		public const int HeadingOffset = 0x5A;

		public const int LevelOffset = 0x5C;

		/// <summary>The name a walk begun without a save is written with.</summary>
		public const string WalkerName = "Avatar";

		public static int PositionX(UWPlayerFile pOFile) => UWBinary.ReadU16(pOFile.Record, PositionXOffset);

		public static int PositionY(UWPlayerFile pOFile) => UWBinary.ReadU16(pOFile.Record, PositionYOffset);

		public static int ZPosition(UWPlayerFile pOFile) => UWBinary.ReadU16(pOFile.Record, ZPositionOffset);

		public static int Heading(UWPlayerFile pOFile) => UWBinary.ReadU16(pOFile.Record, HeadingOffset);

		/// <summary>The 1-based level the game was saved on.</summary>
		public static int Level(UWPlayerFile pOFile) => UWBinary.ReadU16(pOFile.Record, LevelOffset);

		public static int Skill(UWPlayerFile pOFile, int piSkill) => pOFile.Record[SkillsOffset + piSkill];

		// ------------------------------------------------- The character on the screen (phase 3 step 2)

		/// <summary>The attributes, then vitality, mana and their maxima, the level, the weight carried and
		/// the most, the experience in tenths, the skill points (struct Player 0x1E to 0x52, as the first
		/// game's).</summary>
		public const int StrengthOffset = 0x1E;

		public const int DexterityOffset = 0x1F;

		public const int IntelligenceOffset = 0x20;

		public const int VitalityOffset = 0x35;

		public const int MaxVitalityOffset = 0x36;

		public const int ManaOffset = 0x37;

		public const int MaxManaOffset = 0x38;

		public const int CharacterLevelOffset = 0x3D;

		public const int WeightOffset = 0x4A;

		public const int MaxWeightOffset = 0x4C;

		public const int ExperienceOffset = 0x4E;

		public const int SkillPointsOffset = 0x52;

		/// <summary>The word of drawn (bit 0) and poison (bits 1-4).</summary>
		public const int StateWordOffset = 0x60;

		/// <summary>The byte of lefty (bit 0), female (bit 1), body (bits 2-4) and class (bits 5-7) - the
		/// first game's byte 0x63 one place on.</summary>
		public const int BodyByteOffset = 0x65;

		/// <summary>The player object's hit points in the workspace: the vitality the game keeps on loading
		/// (getPlayerInvCopy overwrites the record's, Underworld Exhumed docs/FORMATS.md "PLAYER.DAT").</summary>
		public const int WorkspaceHitPointsOffset = 8;

		public static int Strength(UWPlayerFile pOFile) => pOFile.Record[StrengthOffset];

		public static int Dexterity(UWPlayerFile pOFile) => pOFile.Record[DexterityOffset];

		public static int Intelligence(UWPlayerFile pOFile) => pOFile.Record[IntelligenceOffset];

		/// <summary>The vitality: the workspace's player object's, else the record's.</summary>
		public static int Vitality(UWPlayerFile pOFile) => pOFile.Workspace.Length > WorkspaceHitPointsOffset
			? pOFile.Workspace[WorkspaceHitPointsOffset] : pOFile.Record[VitalityOffset];

		public static int MaxVitality(UWPlayerFile pOFile) => pOFile.Record[MaxVitalityOffset];

		public static int Mana(UWPlayerFile pOFile) => pOFile.Record[ManaOffset];

		public static int MaxMana(UWPlayerFile pOFile) => pOFile.Record[MaxManaOffset];

		public static int CharacterLevel(UWPlayerFile pOFile) => pOFile.Record[CharacterLevelOffset];

		public static int Weight(UWPlayerFile pOFile) => UWBinary.ReadU16(pOFile.Record, WeightOffset);

		public static int MaxWeight(UWPlayerFile pOFile) => UWBinary.ReadU16(pOFile.Record, MaxWeightOffset);

		/// <summary>The weight the character can still carry in tenths of a stone (displayEnc).</summary>
		public static int WeightLeft(UWPlayerFile pOFile) => MaxWeight(pOFile) - Weight(pOFile);

		/// <summary>The experience in tenths.</summary>
		public static long Experience(UWPlayerFile pOFile) => (long)UWBinary.ReadU16(pOFile.Record, ExperienceOffset)
			| ((long)UWBinary.ReadU16(pOFile.Record, ExperienceOffset + 2) << 16);

		public static int SkillPoints(UWPlayerFile pOFile) => pOFile.Record[SkillPointsOffset];

		/// <summary>The rune bag (a bit per rune, rune r at bit 7 - (r &amp; 7) of byte r / 8), the shelf's
		/// three runes (0x18 none) and the active spells (three words; their count in bits 5-8 of the word
		/// at 0x60), as the first game's record holds them; the shelf's count is bits 1-2 of the word at 0x61
		/// (UE include/player.h:20, combat/RUNES.C add_rune).</summary>
		public const int RuneBagOffset = 0x44;

		public const int ShelfOffset = 0x47;

		public const int ActiveSpellsOffset = 0x3E;

		public const int RuneCount = 0x18;

		public const int NoRune = 0x18;

		public static bool HasRune(UWPlayerFile pOFile, int piRune) => piRune >= 0 && piRune < RuneCount
			&& ((pOFile.Record[RuneBagOffset + (piRune >> 3)] >> (7 - (piRune & 7))) & 1) != 0;

		public static void SetRune(UWPlayerFile pOFile, int piRune)
		{
			if (piRune >= 0 && piRune < RuneCount)
				pOFile.Record[RuneBagOffset + (piRune >> 3)] |= (byte)(1 << (7 - (piRune & 7)));
		}

		public static int ShelfRune(UWPlayerFile pOFile, int piPlace) => pOFile.Record[ShelfOffset + piPlace];

		/// <summary>The shelf's three runes and their count (word 0x61 bits 1-2).</summary>
		public static void WriteShelf(UWPlayerFile pOFile, int[] piRunes, int piCount)
		{
			for (int liAt = 0; liAt < 3; liAt++)
				pOFile.Record[ShelfOffset + liAt] = (byte)(liAt < piRunes.Length ? piRunes[liAt] : NoRune);

			int liWord = UWBinary.ReadU16(pOFile.Record, StateWordOffset + 1);

			UWBinary.Write16(pOFile.Record, StateWordOffset + 1, (liWord & ~0x6) | ((piCount & 3) << 1));
		}

		public static int ShelfCount(UWPlayerFile pOFile) => (UWBinary.ReadU16(pOFile.Record, StateWordOffset + 1) >> 1) & 3;

		public static int ActiveSpellCount(UWPlayerFile pOFile) => (UWBinary.ReadU16(pOFile.Record, StateWordOffset) >> 5) & 0xF;

		public static int ActiveSpell(UWPlayerFile pOFile, int piIndex) => UWBinary.ReadU16(pOFile.Record, ActiveSpellsOffset + (2 * piIndex));

		/// <summary>The poison, 0 to 15 (bits 1-4 of the word at 0x60).</summary>
		public static int Poison(UWPlayerFile pOFile) => (UWBinary.ReadU16(pOFile.Record, StateWordOffset) >> 1) & 0xF;

		public static bool IsPoisoned(UWPlayerFile pOFile) => Poison(pOFile) != 0;

		public static bool IsLeftHanded(UWPlayerFile pOFile) => (pOFile.Record[BodyByteOffset] & 1) != 0;

		public static bool IsFemale(UWPlayerFile pOFile) => (pOFile.Record[BodyByteOffset] & 2) != 0;

		public static int Body(UWPlayerFile pOFile) => (pOFile.Record[BodyByteOffset] >> 2) & 7;

		public static int CharacterClass(UWPlayerFile pOFile) => (pOFile.Record[BodyByteOffset] >> 5) & 7;

		/// <summary>
		/// Where the object in an Inventory[] slot lies in the workspace, or -1 for an empty slot: the slot
		/// holds the saved object's number in bits 6-15, object n at 0x5B + 8n (numbered from 1).
		/// </summary>
		public static int SlotObjectOffset(UWPlayerFile pOFile, int piSlot)
		{
			int liNumber = pOFile.Slot(piSlot);
			int liAt = UWPlayerFile.WorkspaceHeaderSize + (UWPlayerFile.SavedObjectSize * liNumber);

			return liNumber > 0 && liAt + UWPlayerFile.SavedObjectSize <= pOFile.Workspace.Length ? liAt : -1;
		}

		/// <summary>The item id in a slot, -1 for an empty one.</summary>
		public static int SlotItemId(UWPlayerFile pOFile, int piSlot)
		{
			int liAt = SlotObjectOffset(pOFile, piSlot);

			return liAt >= 0 ? UWObjectRecordCodec.ReadId(pOFile.Workspace, liAt) : -1;
		}

		/// <summary>The quality (word 2, bits 0-5) of the object in a slot.</summary>
		public static int SlotQuality(UWPlayerFile pOFile, int piSlot)
		{
			int liAt = SlotObjectOffset(pOFile, piSlot);

			return liAt >= 0 ? UWBinary.ReadU16(pOFile.Workspace, liAt + 4) & 0x3F : 0;
		}

		/// <summary>The count a slot's stack shows: the quantity of an is_quant object below 0x200 (above it
		/// the link is special), else 1 (displayInventoryArray).</summary>
		public static int SlotCount(UWPlayerFile pOFile, int piSlot)
		{
			int liAt = SlotObjectOffset(pOFile, piSlot);

			if (liAt < 0 || !UWObjectRecordCodec.ReadIsQuantity(pOFile.Workspace, liAt))
				return 1;

			int liQuantity = UWObjectRecordCodec.ReadQuantityOrLink(pOFile.Workspace, liAt);

			return liQuantity < 0x200 && liQuantity > 1 ? liQuantity : 1;
		}

		/// <summary>Where the game is saved: the fine positions, the fine z, the 16-bit facing, the level.</summary>
		public static void WritePosition(UWPlayerFile pOFile, int piPositionX, int piPositionY, int piZPosition, int piHeading, int piLevel)
		{
			UWBinary.Write16(pOFile.Record, PositionXOffset, piPositionX & 0xFFFF);
			UWBinary.Write16(pOFile.Record, PositionYOffset, piPositionY & 0xFFFF);
			UWBinary.Write16(pOFile.Record, ZPositionOffset, piZPosition & 0xFFFF);
			UWBinary.Write16(pOFile.Record, HeadingOffset, piHeading & 0xFFFF);
			UWBinary.Write16(pOFile.Record, LevelOffset, piLevel);
		}

		/// <summary>
		/// A record for a walk begun without a save: the name (WalkerName), its key, everything else zero,
		/// no saved objects - enough for the walk to come back to, NOT a character the original could play
		/// (no vitality, no skills; it would come in dead).
		/// </summary>
		public static UWPlayerFile NewWalker()
		{
			byte[] lyRecord = new byte[UWPlayerFile.Uw2RecordSize];

			for (int liAt = 0; liAt < WalkerName.Length; liAt++)
				lyRecord[liAt] = (byte)WalkerName[liAt];

			return UWPlayerFile.Create(UWGame.Uw2, lyRecord, 1, new byte[UWPlayerFile.WorkspaceHeaderSize]);
		}

		// ------------------------------------------------- A new character (UW2 plan phase 3 step 5)

		/// <summary>init_char's fixed values (Underworld Exhumed uw2 game/CHARGEN.C:73-129) and the fields
		/// they go to (include/player.h:20-106).</summary>
		public const int HungerOffset = 0x39;

		public const int FatigueOffset = 0x3A;

		public const int FoodHealOffset = 0x3B;

		public const int SkillPointsEarnedOffset = 0x53;

		public const int MoonstonesOffset = 0x5E;

		/// <summary>Word 0x62's bit 4: the automap is on (player->automap).</summary>
		public const int AutomapByteOffset = 0x62;

		public const int AutomapBit = 0x10;

		public const int QuestBytesF6Offset = 0xF6;

		public const int MapScrapOffset = 0xF8;

		public const int EasyOffset = 0x301;

		/// <summary>Word 0x302: sound bits 0-1, music 2-3, detail 4-7.</summary>
		public const int DetailByteOffset = 0x302;

		public const int DetailShift = 4;

		public const int GameClockOffset = 0x369;

		public const int NewHunger = 0xC0;

		public const int NewFatigue = 0x30;

		public const int NewFoodHeal = 0x30;

		public const int NewDetail = 3;

		public const int NewGameClock = 0x465000;

		public static readonly int[] NewMoonstones = { 3, 0x2D };

		public const int NewQuestByteF6 = 8;

		public const int NewMapScrap = 1;

		/// <summary>The carrying limit: strength times 13 plus 300 (SKILLS.C:81 player_compute).</summary>
		public const int WeightPerStrength = 13;

		public const int BaseWeight = 300;

		/// <summary>The player object, a mobile object of 27 bytes at the workspace's start.</summary>
		public const int PlayerObjectSize = UWLevArkLayout.MobileRecordSize;

		/// <summary>InitPlayerRec's player object (Underworld Exhumed uw2 game/PLAYER.C:91-104): the adventurer
		/// (items.h ITEM_ADVENTURER 0x7F) with the door-direction bit 13 set, whoami 0xFD at 0x1A, no links.
		/// player_setup (motion/PHYSICS.C:521-527) adds the home tile at 0x16 (x bits 10-15, y 4-9), the fine
		/// position 3/3 in word 2 (y bits 10-12, x 13-15) and the sequence 1 at 0x15. The DOS saves' workspaces
		/// begin 7F 20 and end FD (measured).</summary>
		public const int PlayerItemId = 0x7F;

		public const int PlayerDoorDirBit = 0x2000;

		public const int PlayerWhoami = 0xFD;

		public const int PlayerWhoamiOffset = 0x1A;

		public const int PlayerPosOffset = 2;

		public const int PlayerFine = 3;

		public const int PlayerSequenceOffset = 0x15;

		public const int PlayerHomeOffset = 0x16;

		/// <summary>
		/// THE NEW CHARACTER'S PLAYER.DAT (CHARGEN.C create_player): the record as init_char(1) blanks it and
		/// gen_char fills it, and a workspace with the player object and nothing carried.
		///
		/// The record: the name (29 characters and a zero), the attributes and skills chosen, vitality and
		/// mana at their maxima (roll_stats sets the hit points to avghit, the "Yes" runs player_compute(1),
		/// which fills the mana), level 1, one skill point, hunger 0xC0, fatigue and food_heal 0x30, an empty
		/// shelf, the carrying limit strength * 13 + 300, the moonstones on levels 3 and 0x2D, the automap on,
		/// map_scrap 1, bF6 8, the difficulty, detail 3, the game clock at 0x465000; the X clocks all zero -
		/// init_char sets the time of day to 15 and then clears all sixteen (CHARGEN.C:86 and :116). The byte
		/// 0x65: lefty (the second game asks Right, Left and stores 1 - choice, CHARGEN.C:591 - the first
		/// answer's bit, which UWCharacterGeneration's IsLeftHanded is), female, body, class. No position or
		/// level: the walk starts where a new game does (player_setup at 0x13/0x30 on level 1).
		///
		/// The workspace: the player object as InitPlayerRec and player_setup make it (PlayerItemId and the
		/// rest, its home the start tile, its z left 0 - the walk places the player by the floor, and the
		/// original takes PN from the record's position when it restores), its hit points (byte 8) at the
		/// maximum; then the cursor object and the slots, zero.
		/// </summary>
		public static UWPlayerFile NewCharacter(UWPlayerData.NewCharacter pOCharacter)
		{
			byte[] lyRecord = new byte[UWPlayerFile.Uw2RecordSize];
			string lsName = pOCharacter.Name ?? string.Empty;

			for (int liAt = 0; liAt < UWPlayerData.MaxNameLength && liAt < lsName.Length; liAt++)
			{
				char lcChar = lsName[liAt];

				lyRecord[liAt] = (byte)(UWTextLine.IsStorable(lcChar) ? lcChar : '_');
			}

			lyRecord[StrengthOffset] = (byte)pOCharacter.Strength;
			lyRecord[DexterityOffset] = (byte)pOCharacter.Dexterity;
			lyRecord[IntelligenceOffset] = (byte)pOCharacter.Intelligence;
			lyRecord[SkillsOffset] = (byte)pOCharacter.Attack;
			lyRecord[SkillsOffset + 1] = (byte)pOCharacter.Defense;

			for (int liAt = 0; pOCharacter.Skills != null && liAt < pOCharacter.Skills.Length && liAt + 2 < UWCharacterGeneration.SkillNumberCount; liAt++)
				lyRecord[SkillsOffset + 2 + liAt] = (byte)pOCharacter.Skills[liAt];

			lyRecord[VitalityOffset] = (byte)pOCharacter.MaxVitality;
			lyRecord[MaxVitalityOffset] = (byte)pOCharacter.MaxVitality;
			lyRecord[ManaOffset] = (byte)pOCharacter.MaxMana;
			lyRecord[MaxManaOffset] = (byte)pOCharacter.MaxMana;
			lyRecord[HungerOffset] = NewHunger;
			lyRecord[FatigueOffset] = NewFatigue;
			lyRecord[FoodHealOffset] = NewFoodHeal;
			lyRecord[CharacterLevelOffset] = 1;

			for (int liAt = 0; liAt < 3; liAt++)
				lyRecord[ShelfOffset + liAt] = NoRune;

			UWBinary.Write16(lyRecord, MaxWeightOffset, (pOCharacter.Strength * WeightPerStrength) + BaseWeight);
			lyRecord[SkillPointsOffset] = 1;
			lyRecord[MoonstonesOffset] = (byte)NewMoonstones[0];
			lyRecord[MoonstonesOffset + 1] = (byte)NewMoonstones[1];
			lyRecord[AutomapByteOffset] = AutomapBit;
			lyRecord[BodyByteOffset] = (byte)((pOCharacter.IsLeftHanded ? 1 : 0) | (pOCharacter.IsFemale ? 2 : 0)
				| ((pOCharacter.Body & 7) << 2) | ((pOCharacter.CharacterClass & 7) << 5));
			lyRecord[QuestBytesF6Offset] = NewQuestByteF6;
			lyRecord[MapScrapOffset] = NewMapScrap;
			lyRecord[EasyOffset] = (byte)pOCharacter.Difficulty;
			lyRecord[DetailByteOffset] = NewDetail << DetailShift;
			UWBinary.Write32(lyRecord, GameClockOffset, NewGameClock);

			byte[] lyWorkspace = new byte[UWPlayerFile.WorkspaceHeaderSize];

			UWBinary.Write16(lyWorkspace, 0, PlayerItemId | PlayerDoorDirBit);
			UWBinary.Write16(lyWorkspace, PlayerPosOffset, (PlayerFine << 10) | (PlayerFine << 13));
			lyWorkspace[WorkspaceHitPointsOffset] = (byte)pOCharacter.MaxVitality;
			lyWorkspace[PlayerSequenceOffset] = 1;
			UWBinary.Write16(lyWorkspace, PlayerHomeOffset, (UWGameProfileUw2.NewGameTileX << 10) | (UWGameProfileUw2.NewGameTileY << 4));
			lyWorkspace[PlayerWhoamiOffset] = PlayerWhoami;

			return UWPlayerFile.Create(UWGame.Uw2, lyRecord, 1, lyWorkspace);
		}
	}
}
