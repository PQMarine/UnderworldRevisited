namespace UWDataImport.UWData
{
	/// <summary>
	/// THE OBJECT NUMBERS SEVERAL SYSTEMS KNOW, in one engine-free place, with the range tests
	/// on them (cleanup phase 1, 2026-10-11). Until then the same numbers were declared in up to
	/// four files each - the bedroll in three, the creature range in nine forms - and every copy
	/// carried its own explanation.
	///
	/// NUMBERING: an object number is nine bits, 0 to 511, and its name stands in string block 4
	/// at the number PLUS ONE (the project's usual offset, see UWObjectMechanics.RuneBagId).
	/// The number splits into a major class (bits 6-8, Major), a minor class (bits 4-5, Minor)
	/// and an index (bits 0-3), which is how COMOBJ.DAT and the original's class tests read it.
	///
	/// UWObjectMechanics keeps its public names (hundreds of callers); where both have a name
	/// for the same number, UWObjectMechanics' is an alias of this one. Numbers only one rule
	/// uses stay with that rule (the fishing pole with UWItemUse, the gravestone with
	/// UWEndgameRules).
	/// </summary>
	public static class UWObjectIds
	{
		// ------------------------------------------------- Classes

		/// <summary>Major class of the creatures (1): the objects 64 to 127.</summary>
		public const int CreatureMajorClass = 1;

		/// <summary>The major class, bits 6 to 8 of the number - 0 weapons, 1 creatures,
		/// 2 containers and lights, 3 runes and keys, 4 quest and inventory items, 5 doors and
		/// furniture, 6 traps and triggers, 7 effects.</summary>
		public static int Major(int piObjectId)
		{
			return (piObjectId >> 6) & 7;
		}

		/// <summary>The minor class, bits 4 and 5 of the number.</summary>
		public static int Minor(int piObjectId)
		{
			return (piObjectId >> 4) & 3;
		}

		// ------------------------------------------------- Player and creatures

		/// <summary>The player's object number WHILE PLAYING: 127, "an adventurer" - height 23,
		/// radius 2 in COMOBJ.DAT. On disk the player carries 63 (UWTileQueries.PlayerObjectId),
		/// whose entry has height zero; UW.EXE sets 127 while the game runs and 63 only for saving
		/// (reference: SaveGame.Save, "Set the player back to being an adventurer").</summary>
		public const int Adventurer = 127;

		/// <summary>Creatures are the objects 64 to 127, major class 1.</summary>
		public const int FirstCreature = 0x40;

		public const int LastCreature = 0x7F;

		/// <summary>A creature: the original's test is "major class equals 1"
		/// (UWMotionCore.fIsCreatureClass mirrors it bit for bit).</summary>
		public static bool IsCreature(int piObjectId)
		{
			return piObjectId >= FirstCreature && piObjectId <= LastCreature;
		}

		// ------------------------------------------------- Containers and lights

		/// <summary>The containers, 128 "a_sack" to 143 "a_rune bag".</summary>
		public const int FirstContainer = 0x80;

		/// <summary>Last container with its own open picture: sack to gold coffer, 128 to 139.
		/// For these, bit 0 of the object number means "open" (the reference's container.Use);
		/// urn, quiver and bowl (140 to 142) have none and show themselves.</summary>
		public const int LastContainerWithOpenPicture = 139;

		/// <summary>a_rune bag (143, category Containers). It is an exception: clicking
		/// it opens NO container window but turns the panel to the rune shelf
		/// (per user, 2026-09-03).</summary>
		public const int RuneBag = 143;

		/// <summary>Whether an open container of this number shows a picture of its own: the
		/// number with bit 0 set (OpenPicture). "| 1" and not "+ 1": an already open number
		/// would otherwise jump to the next container (UWHudInventory, 2026-09-11).</summary>
		public static bool HasOpenPicture(int piObjectId)
		{
			return piObjectId >= FirstContainer && piObjectId <= LastContainerWithOpenPicture;
		}

		/// <summary>The picture of an open container: its open variant where it has one, else
		/// itself.</summary>
		public static int OpenPicture(int piObjectId)
		{
			return HasOpenPicture(piObjectId) ? piObjectId | 0x1 : piObjectId;
		}

		/// <summary>
		/// Light sources: 144 to 147 unlit, 148 to 151 lit.
		///
		/// Read from the names in string block 4: lantern, torch, candle, taper, then
		/// the same four as "lit". So lighting simply means adding four.
		/// </summary>
		public const int FirstUnlitLight = 144;

		public const int LastUnlitLight = 147;

		public const int LitLightOffset = 4;

		public const int Lantern = 144;

		public const int Torch = 145;

		public const int Candle = 146;

		public const int Taper = 147;

		/// <summary>The lit taper, the last of the four lit lights and of all eight.</summary>
		public const int LitTaper = LastUnlitLight + LitLightOffset;

		/// <summary>A light source, lit or not - the eight numbers 144 to 151 (the reference
		/// writes "(object number &amp; 0x1F8) equals 0x90", the same eight).</summary>
		public static bool IsLight(int piObjectId)
		{
			return piObjectId >= FirstUnlitLight && piObjectId <= LitTaper;
		}

		// ------------------------------------------------- Treasure, food, remains

		/// <summary>a_shiny cup (174), the Cup of Wonder.</summary>
		public const int CupOfWonder = 174;

		/// <summary>First food, 0xB0 "a_piece of meat": the comestibles 176 to 191. The food
		/// table of OBJECTS.DAT and the loot and Create Food rolls count from here.</summary>
		public const int FirstFood = 0xB0;

		/// <summary>Bones 194 to 198 - "some_bones" and the named skulls; the burial and the
		/// character's remains use them (UWEndgameRules, UWDeath).</summary>
		public const int FirstBones = 0xC2;

		public const int LastBones = 0xC6;

		public static bool IsBones(int piObjectId)
		{
			return piObjectId >= FirstBones && piObjectId <= LastBones;
		}

		/// <summary>"a_pile of debris" (213), the first of the two remains a smashed container
		/// leaves.</summary>
		public const int FirstDebris = 0xD5;

		// ------------------------------------------------- Keys, quest and inventory items

		/// <summary>The Key of Truth (0xE1), spawned into the hand at the shrine.</summary>
		public const int KeyOfTruth = 0xE1;

		/// <summary>The rotworm stew (0x11B, "a_bowl of wormy stew").</summary>
		public const int RotwormStew = 0x11B;

		/// <summary>The spell object (288) that carries an enchantment for its owner.</summary>
		public const int SpellObject = 288;

		/// <summary>The bedroll (289). From the reference's class calculation: major class 4,
		/// minor class 2, index 1 gives (4 &lt;&lt; 6) | (2 &lt;&lt; 4) | 1.</summary>
		public const int Bedroll = 289;

		/// <summary>The silver seed (0x122), planted into a silver tree.</summary>
		public const int SilverSeed = 0x122;

		/// <summary>The moonstone (294) - the Gate Travel spell and the save game know it.</summary>
		public const int Moonstone = 0x126;

		/// <summary>The glowing rock (0x129, "a glowing rock"; the disassembly calls it zanium).
		/// The light tables of the Remastered renderer and of the palette effects list it by
		/// number as well.</summary>
		public const int GlowingRock = 0x129;

		// ------------------------------------------------- Doors

		/// <summary>The sixteen door numbers 0x140 to 0x14F, major class 5 minor class 0: 320 to
		/// 327 closed, 328 to 335 the same eight open (the reference: door.ToggleDoor). Six
		/// doors, then the portcullis (326, open 334) and the secret door (327, open 335), which
		/// shows the tile's wall texture instead of a door picture.</summary>
		public const int FirstDoor = 0x140;

		public const int FirstClosedDoor = 0x140;

		public const int LastClosedDoor = 0x147;

		public const int Portcullis = 0x146;

		public const int SecretDoor = 0x147;

		public const int FirstOpenDoor = 0x148;

		public const int LastOpenDoor = 0x14F;

		public const int OpenPortcullis = 0x14E;

		public const int OpenSecretDoor = 0x14F;

		public const int LastDoor = 0x14F;

		/// <summary>The eight closed doors become the eight open ones by adding this.</summary>
		public const int OpenDoorOffset = FirstOpenDoor - FirstClosedDoor;

		public static bool IsDoor(int piObjectId)
		{
			return piObjectId >= FirstDoor && piObjectId <= LastDoor;
		}

		public static bool IsClosedDoor(int piObjectId)
		{
			return piObjectId >= FirstClosedDoor && piObjectId <= LastClosedDoor;
		}

		public static bool IsOpenDoor(int piObjectId)
		{
			return piObjectId >= FirstOpenDoor && piObjectId <= LastOpenDoor;
		}

		/// <summary>The secret door, closed or open.</summary>
		public static bool IsSecretDoor(int piObjectId)
		{
			return piObjectId == SecretDoor || piObjectId == OpenSecretDoor;
		}

		/// <summary>The portcullis, closed or open.</summary>
		public static bool IsPortcullis(int piObjectId)
		{
			return piObjectId == Portcullis || piObjectId == OpenPortcullis;
		}

		// ------------------------------------------------- Furniture and scenery

		/// <summary>The four boulders, from largest to smallest, 0x153 to 0x156. The rock hammer
		/// turns each into the next smaller one; the smallest becomes sling stones. The two
		/// largest share the large boulder model; Tremor drops the last three.</summary>
		public const int FirstBoulder = 0x153;

		public const int LargeBoulderA = 0x153;

		public const int LargeBoulderB = 0x154;

		public const int MediumBoulder = 0x155;

		public const int SmallBoulder = 0x156;

		public const int LastBoulder = 0x156;

		public static bool IsBoulder(int piObjectId)
		{
			return piObjectId >= FirstBoulder && piObjectId <= LastBoulder;
		}

		/// <summary>The bridge (356) - a floor one can stand on, and nothing can be taken through
		/// it.</summary>
		public const int Bridge = 0x164;

		/// <summary>The wall panel with a texture of the level's wall list in its owner field (ITEM_TMAP_C):
		/// drains, banners, the princess, the textured doors.</summary>
		public const int WallPanel = 0x16E;

		// ------------------------------------------------- Traps and triggers

		/// <summary>Major class 6: the traps 0x180 to 0x19F, then the triggers 0x1A0 to 0x1BF
		/// (UWTrapRules). The original's chain removal tests only the first sixteen triggers,
		/// (number &amp; 0x1F0) equals 0x1A0 - UWTrapChainRemoval keeps that mirror.</summary>
		public const int FirstTrap = 0x180;

		public const int LastTrap = 0x19F;

		public const int FirstTrigger = 0x1A0;

		public const int LastTrigger = 0x1BF;

		public static bool IsTrap(int piObjectId)
		{
			return piObjectId >= FirstTrap && piObjectId <= LastTrap;
		}

		public static bool IsTrigger(int piObjectId)
		{
			return piObjectId >= FirstTrigger && piObjectId <= LastTrigger;
		}

		/// <summary>The sixteen animated objects (class 7, ANIMO.GR): blood, smoke, flames, the
		/// fountain's water, the silver tree ... 0x1C0 to 0x1CF.</summary>
		public const int FirstAnimated = 0x1C0;

		public const int LastAnimated = 0x1CF;

		public static bool IsAnimated(int piObjectId)
		{
			return piObjectId >= FirstAnimated && piObjectId <= LastAnimated;
		}

		// ------------------------------------------------- Moving things

		/// <summary>a_silver tree (0x1CA), grown from the seed; until 2026-10-11 one site held
		/// 459, its string index.</summary>
		public const int SilverTree = 0x1CA;

		// ------------------------------------------------- Fields

		/// <summary>The quality field has six bits, so a full piece is 63 of 64 - and a light
		/// source holds no more fuel than this (UWObjectMechanics.FullLightQuality). The same
		/// number with another meaning keeps its own name: the "any creature" marker of the ward
		/// trap (UWObjectMechanics.WardTrapAnyClass) and the owner of a fresh door spike
		/// (UWObjectMechanics.FreshDoorSpikeOwner).</summary>
		public const int MaxQuality = 0x3F;
	}
}
