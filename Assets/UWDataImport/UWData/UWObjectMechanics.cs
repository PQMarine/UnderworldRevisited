using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Interprets already parsed UWObject fields (Owner/Quantity/Flags/Quality) for
	/// doors/locks/keys/triggers - according to uw-formats.txt (vividos/
	/// UnderworldAdventures) these are not separate binary fields but additional
	/// meanings of the existing generic word fields, just like the
	/// container sp_link (see UWObject.Contents/EnsureContentsLoaded).
	/// </summary>
	public static class UWObjectMechanics
	{
		/// <summary>a_rune bag (143, category Containers). It is an exception: clicking
		/// it opens NO container window but turns the panel to the rune shelf
		/// (per user, 2026-09-03). The ID comes from string block 4, entry 144 - with the
		/// project's usual offset of one that is object 143.</summary>
		public const int RuneBagId = 143;

		/// <summary>The player's object number WHILE PLAYING: 127, "an adventurer" - height 23,
		/// radius 2 in COMOBJ.DAT. On disk the player carries 63 (UWTileQueries.PlayerObjectId),
		/// whose entry has height zero; UW.EXE sets 127 while the game runs and 63 only for saving
		/// (reference: SaveGame.Save, "Set the player back to being an adventurer").</summary>
		public const int AdventurerObjectId = 127;

		/// <summary>
		/// Books and scrolls (304-319, category BooksAndScrolls) do not carry their text
		/// themselves: the quantity field points into string block 3, index equal to quantity minus
		/// 0x200.
		///
		/// Cross-checked against all books and scrolls in the game (2026-09-03, see
		/// the removed tool UWBookDump): block 3 holds 512 strings, 32 of which belong to each level - and every
		/// index found falls into the block of its own level.
		///
		/// On top of that comes the project's usual offset of one, just like with the wall writings
		/// (see GetWritingTextIndex): the calculation is quantity minus 511.
		///
		/// Evidence: index 0 is empty, index 1 holds "The pages are blank." - so the counting is
		/// one-based. Without the offset several scrolls from the user's save game
		/// land on empty entries (0, 8, 32), with it on real texts. In addition there was the
		/// test in the original: a scroll with quantity 619 shows the text of index 108 there
		/// (per user, 2026-09-03).
		///
		/// Some scrolls still point to an empty string. Those are the picture parchments
		/// and the map - they have no text but a presentation of their own.
		/// </summary>
		public const int BookStringOffset = 0x200 - 1;

		/// <summary>a_map (315). An exception among the scrolls: it does not show its
		/// entry from block 3 (that would be "The pages are blank.") but a fixed
		/// message, and a click in the original opens the overview map (per user).
		/// </summary>
		public const int MapObjectId = 315;

		/// <summary>"Enscribed upon the scroll is your map." in string block 1. The spelling mistake
		/// is in the original.</summary>
		public const int MapMessageIndex = 152;

		/// <summary>Book or scroll? According to string block 4, 304-311 are books and 312-319
		/// scrolls, among them 315 the map. Only decides the reading hint.
		/// </summary>
		public static bool IsBookId(int piId)
		{
			return piId >= 304 && piId <= 311;
		}

		public static int GetBookStringIndex(UWObject pOBook)
		{
			return pOBook == null ? -1 : pOBook.Quantity - BookStringOffset;
		}

		/// <summary>a_door trap - opens/closes the door on the target tile of the
		/// firing trigger (see UWTriggerSystem).</summary>
		public const int DoorTrapId = 0x0188;

		/// <summary>a_text string trap - prints a message when fired (see
		/// GetTextTrapStringIndex).</summary>
		public const int TextStringTrapId = 0x0190;

		/// <summary>some_writing (358, 0x0166) - wall writing/plaque/sign/rune. Shows its text
		/// directly on look AND on use, not "You see some writing."</summary>
		public const int WritingObjectId = 358;

		/// <summary>Index of the actual writing text in string block 8. The field
		/// "quantity" holds no quantity here but a string number with an offset of
		/// 512; together with the project's usual +1 on string access (see
		/// GetTextTrapStringIndex) that gives quantity - 511.
		///
		/// Checked on all nine writings on level 1 (2026-08-28): 576 -> "We attacked the
		/// entrance..." (confirmed by the user in the original), 577 -> fountain text, 580 -> Silver
		/// Sapling, 581 -> "Keep Out", 582-584 -> the three mantras. All nine land on
		/// real texts, none on an empty entry.</summary>
		public static int GetWritingTextIndex(UWObject pOWriting)
		{
			return pOWriting.Quantity - 511;
		}

		/// <summary>Index of the introduction in string block 8 - depending on appearance "The
		/// writing reads: ", "The plaque reads: ", "The sign reads: ", "An ancient rune: "
		/// or "The runes read: " (block 8, 370-384). Selected via the flags field,
		/// the same field that also determines the TMOBJ graphic (see
		/// UWLevel.load_object_list) - fitting, since both describe the same appearance.
		///
		/// Evidence: the writing on tile 30/2 has flags 8 and shows "The writing
		/// reads:" in the original (per user), which gives index 378 here. The fountain text on 30/61 has
		/// flags 0 and therefore gets "The plaque reads: ".</summary>
		public static int GetWritingPrefixIndex(UWObject pOWriting)
		{
			return 370 + pOWriting.Flags;
		}

		/// <summary>a_use trigger - attached to an object and fires its trap when the
		/// player USES the object. This is how both switches and the double door on
		/// level 1 (tiles 31/1 and 32/1) are attached to their traps.</summary>
		public const int UseTriggerId = 0x01A2;

		/// <summary>a_look trigger - the same, but on LOOKING at the object. On level 1
		/// the orb (tile 58/13) is attached to its text trap this way: right-click look shows
		/// first "You see an orb.", then the crystal ball vision.
		///
		/// So the trigger type decides WHICH interaction fires the chain - which is why
		/// no special cases per object are needed (confirmed per user and data,
		/// 2026-08-28; a first attempt instead fired traps on THE SAME tile,
		/// which only happened to fit for the orb).</summary>
		public const int LookTriggerId = 0x01A3;

		/// <summary>String index of an a_text string trap in block 9. According to uw-formats.txt
		/// "(64*level + owner)" - with a 0-based level, so level 1 occupies 0-63.
		///
		/// The ADDITIONAL +1 is not part of the original formula but compensates for the
		/// known offset of this project on string access: the parsed blocks
		/// sit one entry higher than the original numbering. That is exactly why
		/// this project also always calls GetObjectDescription with "ObjectId + 1" (see
		/// UWObjectSpawner.fSpawnBillboard and UWArmorItemMap) - block 9 behaves like
		/// block 4.
		///
		/// Established per user in the original (2026-08-28), both level 1 traps:
		/// - tile 58/13, owner 1 -> index 2: the crystal ball vision with the green path
		///   to Britannia, fired by the orb on the same tile.
		/// - tile 34/1, owner 3 -> index 4: "The doors are securely locked.", the same
		///   text the user saw when using the double door sprite.
		/// A previous version calculated with a 1-based level and without the offset and
		/// thus landed on 65 and 67 respectively - a plausible but wrong reading in which a
		/// trap seemed to point to an empty string.
		///
		/// piLevelIndex is the project's usual 0-based level index (see
		/// UWLevelLoader.CurrentLevelIndex) and thus matches the original formula directly.</summary>
		public static int GetTextTrapStringIndex(UWObject pOTrap, int piLevelIndex)
		{
			return (64 * piLevelIndex) + pOTrap.Owner + 1;
		}

		/// <summary>a_pole (216, 0x00D8) - verified live in the original per user
		/// (2026-08-28): in use mode the pole extends the reach instead of having
		/// an effect of its own. On level 1 exactly that solves a puzzle - the
		/// switch on tile (55,49) is behind a portcullis and can only be reached from the neighbouring tile
		/// (56,49) with the pole (see Interaction.GetUseRangeFor). It is in
		/// the category SceneryAndJunk, so it cannot be recognised by category - hence this
		/// ID constant.</summary>
		public const int PoleObjectId = 216;

		/// <summary>The anvil - repairs are done at it (see Interaction.TryRepairItem).
		/// </summary>
		public const int AnvilObjectId = 215;

		/// <summary>a_do trap [uw1] - multi-purpose trap, "quality" selects the action. According to
		/// uw-formats.txt only partially documented (action 0x2a = start conversation,
		/// 0x18 = "bullfrog puzzle", action 0x03 itself WITHOUT description in the docs).
		/// Verified live in the original per user (2026-08-28): action 3 together with an
		/// a_lever (ID 353) raises its own tile by "zpos" per use and advances the
		/// lever position (0-7, visible via TMOBJ_4..TMOBJ_11) in a cycle - see
		/// UWTriggerSystem/UWHeightLever.</summary>
		public const int DoTrapId = 0x0183;

		/// <summary>Action of the a_do trap (its "quality" field) for the height levers.</summary>
		public const int DoTrapRaiseTileAction = 3;

		/// <summary>a_do trap 2: the remote camera. Two instances, levels 2 and 4. Location
		/// and heading are in the trap's OWN fields (xpos, ypos, zpos and
		/// heading), not in those of the trigger.</summary>
		public const int DoTrapCameraAction = 2;

		// ------------------------------------------------- further a_do trap actions
		//
		// ALL REACHABLE a_do traps in uw1, counted across all nine levels (2026-09-08):
		//
		//   quality  3  height lever        4x level 1
		//   quality  5  trespass            2x level 1
		//   quality  2  camera              levels 2 and 4
		//   quality 24  bullfrog            4x level 4
		//   quality 40  emerald puzzle      level 6
		//   quality 42  talking door        level 6, only reachable via the door chain
		//   quality 50  conversation        level 7
		//   quality 63  endgame sequence    level 9
		//
		// quality 62 (earthquake) has NO reachable instance: object slot 261 holds
		// byte-identical free-slot garbage on seven levels, which happens to read as such a trap.
		// Deliberately not built for that reason.

		/// <summary>a_do trap 5: trespass. Owner is the race that feels
		/// disturbed.</summary>
		public const int DoTrapTrespassAction = 5;

		/// <summary>a_do trap 24: the bullfrog puzzle on level 4. Owner is the
		/// sub-action.</summary>
		public const int DoTrapBullfrogAction = 24;

		/// <summary>a_do trap 40: the emerald puzzle on level 6.</summary>
		public const int DoTrapEmeraldPuzzleAction = 40;

		/// <summary>a_do trap 42: the talking door on level 6.</summary>
		public const int DoTrapConversationAction = 42;

		/// <summary>a_do trap 50: starts the conversation with the troll guard on level 7.
		/// </summary>
		public const int DoTrapStartConversationAction = 50;

		/// <summary>a_do trap 63: the endgame sequence on level 9.</summary>
		public const int DoTrapEndgameAction = 63;

		/// <summary>a_do trap 41: the exploding book, see UWExplodingBook (see
		/// ExplodingBook_ovr107_1259).</summary>
		public const int DoTrapExplodingBookAction = 41;

		/// <summary>The orb rock, which is good for one thing: smashing Tyball's orb
		/// (TybalsOrb_seg040_A12).</summary>
		public const int OrbRockObjectId = 0x112;

		/// <summary>Tyball's orb, the thing the rock is for.</summary>
		public const int TyballOrbObjectId = 0x117;

		/// <summary>a_do trap 57: Arial is freed (ArialTalking_ovr107_1373) - cutscene 3 and
		/// nothing else.</summary>
		public const int DoTrapArialAction = 57;

		/// <summary>a_do traps 60 to 62: the quake (QuakeTrap_seg008_DE7). Quality MINUS 59 is a
		/// bit mask - bit 0 shakes the screen with the intensity from the trap's owner field,
		/// bit 1 bounces the player - so 60 shakes, 61 bounces, 62 does both.</summary>
		public const int DoTrapQuakeFirstAction = 60;

		public const int DoTrapQuakeLastAction = 62;

		/// <summary>What the quake's quality is measured from - see DoTrapQuakeFirstAction.
		/// </summary>
		public const int DoTrapQuakeBase = 59;


		/// <summary>The emerald - four of them solve the puzzle on level 6.</summary>
		public const int EmeraldObjectId = 167;

		/// <summary>How far the four pedestals of the emerald puzzle lie from the trigger tile,
		/// in tiles - on both axes, in both directions.</summary>
		public const int EmeraldPuzzleOffset = 4;

		/// <summary>Conversation slot of the talking door. The trap does NOT carry it in a
		/// field - all its fields are zero; the number is hard-coded in uw.exe.</summary>
		public const int TalkingDoorConversationSlot = 25;

		/// <summary>Object slot of the conversation partner of a_do trap 50 - on level 7 the
		/// troll guard (id 111, whoami 216) on tile 26/56. Also a fixed value in
		/// uw.exe, not a field of the trap.</summary>
		public const int ConversationTrapNpcIndex = 251;

		/// <summary>Cutscene of the endgame sequence - CUTS\CS001, file names in octal.</summary>
		public const int EndgameCutscene = 1;

		/// <summary>The bullfrog field lies on level 4 (zero-based 3) and spans 48 to 55 on both
		/// axes.</summary>
		public const int BullfrogLevelIndex = 3;

		public const int BullfrogGridOrigin = 48;

		public const int BullfrogGridSize = 8;

		/// <summary>Game variables of the bullfrog puzzle: 24 the X choice, 25 the Y choice, 26 the
		/// remaining attempts. A fresh game starts with 53 attempts - the only
		/// game variable the original initialises to a non-zero value at all.</summary>
		public const int BullfrogVarX = 24;

		public const int BullfrogVarY = 25;

		public const int BullfrogVarRetries = 26;

		public const int BullfrogRetriesAtStart = 53;

		public const int BullfrogRetriesOnReset = 0x3F;

		/// <summary>Base height (position 0) of a height lever target tile - confirmed live
		/// in the original per user (position 0 -&gt; 32, position 1 -&gt; 48, position 2 -&gt;
		/// 64, ...), NOT the static height the target tile already has in the level data
		/// (that is completely ignored/overwritten). The user himself: "I don't think
		/// this is the right formula, but that is how the original behaves." Only verified in
		/// one place (level 1, target tiles around 43-49/44) - whether 32 is a real
		/// engine constant for this a_do trap action or only happens to fit here is
		/// unconfirmed.</summary>
		public const int DoTrapRaiseTileBaseHeight = 32;

		/// <summary>a_lock (0x010F, last element of KeysLockpickLock). Doors resolve their lock
		/// via Contents/EnsureContentsLoaded, not via ID comparison; the constant only
		/// keeps the lock itself out of the key search (see FindMatchingKey).</summary>
		public const int LockObjectId = 0x010F;

		/// <summary>
		/// The lockpick (0x0101). It sits right among the keys - object 256 is a
		/// key, 257 the lockpick, 258 a key again - and can therefore
		/// only be recognised by its number, not by the object class.
		/// </summary>
		public const int LockpickObjectId = 257;

		/// <summary>
		/// Quest items occupy their own ID range (uw-formats.txt 6:
		/// "0110-011f Quest items"). For the question of what is lost in water and lava,
		/// however, this range is NOT the rule: the user has seen items outside of it
		/// lying in the original, and with the resilient sphere one inside it
		/// that sinks. What counts is SinksInLiquid from COMOBJ.DAT.
		/// </summary>
		public const int QuestItemFirstId = 0x0110;

		public const int QuestItemLastId = 0x011F;

		public static bool IsQuestItem(int piObjectId)
		{
			return piObjectId >= QuestItemFirstId && piObjectId <= QuestItemLastId;
		}

		/// <summary>
		/// Bit 0 of the "owner" field: the door is spiked.
		///
		/// THIS USED TO SAY BIT 1, following uw-formats.txt. That is wrong, and the reference
		/// shows it in two places independently: it checks "owner &amp; 1" when
		/// looking at a door (door.LookAt) and, when striking a spiked
		/// door, uses "owner >> 1" as the DURABILITY OF THE SPIKE (damage.cs). The spike itself
		/// sets owner to 63 - that is exactly bit 0 plus the highest possible durability
		/// 31. With bit 1 as the marker this calculation would not work out.
		/// </summary>
		public static bool IsDoorSpiked(UWObject pODoor)
		{
			return (pODoor.Owner & 0x1) != 0;
		}

		/// <summary>How much the spike can still take - the upper five bits of the
		/// "owner" field. Whoever strikes a spiked door hits the spike, not
		/// the door.</summary>
		public static int GetDoorSpikeStrength(UWObject pODoor)
		{
			return pODoor.Owner >> 1;
		}

		/// <summary>Sets the durability of the spike. At zero it is broken and the door is
		/// free again.</summary>
		public static void SetDoorSpikeStrength(UWObject pODoor, int piStrength)
		{
			if (piStrength <= 0)
			{
				pODoor.Owner = 0;

				return;
			}

			pODoor.Owner = (ushort)(0x1 | ((piStrength > 31 ? 31 : piStrength) << 1));
		}

		/// <summary>What a freshly placed spike can take - the reference simply writes
		/// 63 into the field, i.e. marker plus full durability.</summary>
		public const int FreshDoorSpikeOwner = 63;

		/// <summary>Bit 0 of the lock object's "flags" field (= absolute bit 9 of the raw word -
		/// our already masked flags nibble starts there at bit 0): locked (1)
		/// or open (0).</summary>
		public static bool IsLockLocked(UWObject pOLock)
		{
			return (pOLock.Flags & 0x1) != 0;
		}

		/// <summary>Locks or unlocks a lock object directly. A DOOR keeps its state in
		/// UWDoorLock and only writes it back when saving; a barrel or chest has no such
		/// component, so key and lockpick change its lock object itself.</summary>
		public static void SetLockLocked(UWObject pOLock, bool pbLocked)
		{
			if (pOLock == null)
				return;

			pOLock.Flags = (ushort)(pbLocked ? pOLock.Flags | 0x1 : pOLock.Flags & ~0x1);
		}

		/// <summary>Bit 1 of the lock object's "flags" field. The docs called it "use once"; the
		/// original reads it the other way round (read 2026-09-25, see UWLockRules.Unlock): SET,
		/// the lock stays when it is opened and can be locked again; CLEAR, it is taken off and
		/// gone. 81 of the 84 locks in the nine levels carry it.</summary>
		public static bool IsLockKeptWhenOpened(UWObject pOLock)
		{
			return pOLock != null && (pOLock.Flags & 0x2) != 0;
		}

		/// <summary>Lower 6 bits of the lock object's "quantity/special link" field = lock ID.
		/// Must match GetKeyId of a key.</summary>
		public static int GetLockId(UWObject pOLock)
		{
			return pOLock.Quantity & 0x3F;
		}

		/// <summary>"owner" field of a key object = key ID.</summary>
		public static int GetKeyId(UWObject pOKey)
		{
			return pOKey.Owner;
		}

		/// <summary>sp_link target of an object directly via the "quantity/special link" field,
		/// WITHOUT the HasQuantity filter of UWObject.EnsureContentsLoaded/Contents. A live test
		/// showed: for switch/trigger objects HasQuantity is not reliably false,
		/// even though the field is an sp_link there too (not a real quantity field) - a trigger
		/// with a real, valid trap link wrongly yielded an empty chain via
		/// EnsureContentsLoaded. Mirrors the already working
		/// teleport trigger scan in UWLevelLoader.fSpawnTransitions, which ignores HasQuantity for the same
		/// reason. Only the ONE directly linked successor, no
		/// link chain (as in the teleport trap model) - should multiply chained
		/// traps prove necessary, this would still have to be extended here.</summary>
		public static UWObject GetLinkedObject(UWObject pOSource, List<UWObject> pOMasterlist)
		{
			int liIndex = pOSource.Quantity;

			if (liIndex <= 0 || liIndex >= pOMasterlist.Count)
				return null;

			return pOMasterlist[liIndex];
		}

		/// <summary>Searches recursively (also in containers, nested to any depth) for
		/// a key whose key ID matches the given lock ID. Only for
		/// Modern (automatic check on an attempt to open, see Interaction.
		/// fTryUseDoor) - Original instead requires explicitly applying a specific
		/// key to the door in use mode (see Interaction.TryToggleDoorLockWithKey), no search
		/// needed.</summary>
		public static UWObject FindMatchingKey(IEnumerable<UWObject> pOItems, int piLockId, List<UWObject> pOMasterlist)
		{
			if (pOItems == null)
				return null;

			foreach (UWObject lOItem in pOItems)
			{
				if (lOItem == null)
					continue;

				if (lOItem.GetCategory() == UWObject.ObjectCategoryEnum.KeysLockpickLock && lOItem.ID != LockObjectId)
				{
					if (GetKeyId(lOItem) == piLockId)
						return lOItem;
				}
				else if (lOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
				{
					lOItem.EnsureContentsLoaded(pOMasterlist);

					UWObject lOFound = FindMatchingKey(lOItem.Contents, piLockId, pOMasterlist);

					if (lOFound != null)
						return lOFound;
				}
			}

			return null;
		}

		/// <summary>Target tile coordinates of a trigger (Quality/Owner), analogous to the already
		/// verified teleport trap pattern in UWLevelLoader (there the TRAP itself carries
		/// Quality/Owner as target X/Y). For the door trap the target coordinates, according to
		/// the wording of the docs, are on the TRIGGER instead - not yet verified live, can be
		/// corrected by testing.</summary>
		public static (int X, int Y) GetTriggerTargetTile(UWObject pOTrigger)
		{
			return (pOTrigger.Quality, pOTrigger.Owner);
		}



		/// <summary>Blood splatter on a hit: "some_blood", the first class-7 animation
		/// (0x1C0 = 448; SpawnClass7Object_seg044_A33 adds 0x1C0 to the offset, and
		/// AttackerAppliesFinalDamage spawns the blood with offset 0, labels B53-B75).
		/// Verified against the disassembly 2026-09-20 (deviation 51).</summary>
		public const int BloodEffectObjectId = 0x1C0;

		/// <summary>Hit without blood: the same object as the flash on a door or a barrel,
		/// class-7 offset 0x0B (AttackerAppliesFinalDamage label C03). Kept under its old name
		/// for the callers.</summary>
		public const int DustEffectObjectId = FlashEffectObjectId;

		/// <summary>
		/// Which effect appears on a hit, from table byte 8 of the creature: bits 3-4 nonzero
		/// bleed (the blood splat), zero flash (AttackerAppliesFinalDamage labels B49-C17,
		/// "shr 3; and 3"). Until 2026-09-20 the low nibble was tested; on the game's data
		/// only bit 3 ever varies, so the result was the same.
		///
		/// Code 0 is carried by skeleton, ghost, earth golem, shadow beast and reaper - all
		/// creatures without blood; rotworm, slug, bat, rat and spider bleed.
		/// </summary>
		public static int GetHitEffectObjectId(int piBloodAndRemains)
		{
			return ((piBloodAndRemains >> 3) & 3) == 0 ? FlashEffectObjectId : BloodEffectObjectId;
		}
		/// <summary>The flash that appears when a weapon hits something that is not a creature
		/// (door, chest, barrel): animation object 0x1C0 + 0x0B, as the original spawns it
		/// for object hits (AttackerAppliesFinalDamage label C03).</summary>
		public const int FlashEffectObjectId = 0x1C0 + 0x0B;

		/// <summary>
		/// The splash on water: class-7 offset 6, so object 0x1C6 = 454, string block 4 entry
		/// 454 "a_splash". The original spawns it in exactly two places, and both push the 6
		/// together with an animation duration of 3:
		///
		///   - a land creature that ends its step in deep water (seg006_1477_476, asm line
		///     41433), which is the one splash of a DROWNING;
		///   - a flying object that comes to rest on a water tile
		///     (ObjectHitsFloorTileDestroyTalismans_seg029_C6F, labels C96 to CC8: the object's
		///     byte 0x0A bits 4-6, the tile state, equal to 1), which is the splash of
		///     everything that FALLS in.
		///
		/// Nothing that is merely PLACED on a water tile gets one, which is why the user
		/// counted exactly one splash at a drowning although the loot went into the water as
		/// well (2026-09-20).
		/// </summary>
		public const int SplashEffectObjectId = 0x1C0 + 6;

		/// <summary>The 3D-model containers standing in the world (major class 5, minor
		/// class 1): barrel, chest, nightstand. Using them spills their contents onto the
		/// tile instead of opening a container panel.</summary>
		public const int BarrelObjectId = 0x015B;

		public const int ChestObjectId = 0x015D;

		public const int NightstandObjectId = 0x015E;

		/// <summary>The bridge (356), the moongate (346) and the force field (365) - further fixed
		/// things in the world that several systems recognise by number.</summary>
		public const int BridgeObjectId = 0x0164;

		public const int MoongateObjectId = 0x015A;

		public const int ForceFieldObjectId = 365;

		/// <summary>The moonstone (294) - the Gate Travel spell and the save game know it.</summary>
		public const int MoonstoneObjectId = 0x126;

		/// <summary>Ammunition starts at 16: the crossbow bolt is 17, the arrow 18.</summary>
		public const int FirstAmmunitionObjectId = 0x10;

		public const int CrossbowBoltObjectId = 17;

		public const int ArrowObjectId = 18;

		/// <summary>Creatures are the objects 64 to 127.</summary>
		public const int FirstCritterObjectId = 0x40;

		public const int LastCritterObjectId = 0x7F;

		/// <summary>The quality field has six bits.</summary>
		public const int MaxQuality = 63;

		/// <summary>Major class of doors (5) and the render type of objects drawn by view
		/// direction (2, COMOBJ.DAT) - the lore check and the trade screen tell them apart.</summary>
		public const int DoorMajorClass = 5;

		public const int DirectionalRenderType = 2;

		/// <summary>Barrel, chest or nightstand - see Interaction.SpillWorldContainer.</summary>
		public static bool IsWorldContainer(int piObjectId)
		{
			return piObjectId == BarrelObjectId || piObjectId == ChestObjectId
				|| piObjectId == NightstandObjectId;
		}

		/// <summary>The base of the "fluids" objects a dead creature leaves: 0xD8 + (table byte
		/// 8 &gt;&gt; 5 &amp; 7), so 217 "a_dead rotworm" for index 1 (DropNPCRemains_seg006_5,
		/// 40801-40872). See GetRemainsObjectId.</summary>
		public const int FirstRemainsObjectId = 0xD8;

		/// <summary>The base of the corpse objects: 0xC0 + (table byte 0x0A &gt;&gt; 2 &amp; 7),
		/// placed with 7 in 16 (DropNPCRemains 41019-41050). Index 0 means none.</summary>
		public const int FirstCorpseObjectId = 0xC0;

		/// <summary>
		/// What remains of a creature, from table byte 8: 0xD8 + (byte &gt;&gt; 5 &amp; 7)
		/// (DropNPCRemains_seg006_5, 40872 "add ax, 0D8h"), -1 for index 0. The seven objects
		/// behind 0xD8:
		///
		///   217 a_dead rotworm            222 a_blood stain
		///   218 some_rubble               223 a_blood stain
		///   219 a_pile of wood chips
		///   220 a_pile of bones
		///   221 a_blood stain
		///
		/// The earlier reading "216 + (bits 4-7) / 2" from uw-formats.txt gives the same
		/// numbers; the formula now follows the disassembly (deviation 51).
		/// </summary>
		public static int GetRemainsObjectId(int piBloodAndRemains)
		{
			int liIndex = (piBloodAndRemains >> 5) & 7;

			return liIndex == 0 ? -1 : FirstRemainsObjectId + liIndex;
		}
		/// <summary>Fountain, the basin - according to the user, using it refreshes the player.</summary>
		public const int FountainObjectId = 302;

		/// <summary>Fountain, the animated water. A fountain consists of TWO objects on
		/// the same tile: the basin (302) and the water (457), which sits one height step
		/// above it and is therefore hit when looking straight ahead (dump of all five
		/// fountains on level 1, 2026-08-30). Both must have the same effect, otherwise
		/// nothing happens depending on the viewing angle.</summary>
		public const int FountainWaterObjectId = 457;

		/// <summary>Cauldron - using it only reports that it is empty.</summary>
		public const int CauldronObjectId = 303;

		/// <summary>"The waters of the fountain renew your strength." (string block 1) - only from a
		/// fountain whose enchantment is of the healing class (Fountain_seg040_352B_1816, 0xF9 there).</summary>
		public const int FountainMessageIndex = 250;

		/// <summary>"The water refreshes you." - every other fountain, enchanted or not (0xED).</summary>
		public const int FountainRefreshMessageIndex = 238;

		/// <summary>The healing class: the fountain says "renew your strength" for it.</summary>
		public const int FountainHealMajorClass = 4;

		/// <summary>
		/// THE ENCHANTMENT AS CLASSES, GetItemEnchantment_seg040_352B_257C (read 2026-10-08 for the
		/// fountain, per user on the original: not every fountain heals, and the one on level 1 not
		/// fully): traps and triggers carry none; otherwise the spell object as FindSpellObject finds
		/// it. One linked with no charge left is not found four times in ten (the same roll as the
		/// wand's, UWItemUse). Its field read in two forms: with the spell format flag the item
		/// classes (13 and up, -1 for a zero upper part - found, but nothing to cast), without it
		/// the major class in bits 4-8 and the minor in bits 0-3 - the form the traps' classes have.
		/// False when there is no enchantment.
		/// </summary>
		public static bool TryGetEnchantmentClass(UWObject pOObject, IList<UWObject> pORecords, out int piMajorClass, out int piMinorClass)
		{
			piMajorClass = -1;
			piMinorClass = 0;

			if (pOObject == null || ((pOObject.ID >> 6) & 7) == 6)
				return false;

			UWObject lOSpell = FindSpellObject(pOObject, pORecords);

			if (lOSpell == null)
				return false;

			if (lOSpell != pOObject && (lOSpell.Quality & 0x3F) == 0 && UWRandom.Next(10) < 4)
				return false;

			int liRaw = lOSpell.Quantity & 0x1FF;

			if ((lOSpell.Flags & SpellFormatFlag) != 0)
			{
				int liUpper = liRaw >> 6;

				piMajorClass = liUpper == 0 ? -1 : liUpper + FirstItemSpellMajorClass - 1;
				piMinorClass = liRaw & 0x3F;

				return true;
			}

			piMajorClass = liRaw >> 4;
			piMinorClass = liRaw & 0xF;

			return true;
		}

		/// <summary>"The cauldron is empty." (string block 1, confirmed verbatim
		/// per user).</summary>
		public const int CauldronMessageIndex = 275;

		/// <summary>a_move trigger: lies in the tile itself and fires on entering.</summary>
		public const int MoveTriggerId = 0x01A0;

		/// <summary>a_pick up trigger: fires when the player picks up the object it is
		/// attached to. This is how the headless appears on level 3: the bag on 49/52 carries it, and
		/// behind it is a create trap on 44/55.</summary>
		public const int PickUpTriggerId = 0x01A1;

		/// <summary>a_create object trap - creates an object from the template the
		/// sp_link points to. According to uw-formats.txt only if a random number between 0 and
		/// 0x3F is GREATER than "quality".</summary>
		public const int CreateObjectTrapId = 0x0187;

		/// <summary>a_change terrain trap - turns a solid tile into floor and
		/// vice versa. This is how the secret passages work (see UWTriggerSystem).</summary>
		public const int ChangeTerrainTrapId = 0x0185;

		/// <summary>a_damage trap - damage or poison on whoever fires it.</summary>
		public const int DamageTrapId = 0x0180;

		/// <summary>an_arrow trap - fires a projectile.</summary>
		public const int ArrowTrapId = 0x0182;

		/// <summary>
		/// Which ammunition an arrow trap fires.
		///
		/// The object number is split over two fields: the lower five bits in "owner", the
		/// upper ones in "quality". Cross-checked on all seven arrow traps in the game - each
		/// gives a sensible object: twice a boulder (340), a skull (195),
		/// a bone (196) and twice a crossbow bolt (17).
		/// </summary>
		public static int GetArrowTrapAmmunitionId(UWObject pOTrap)
		{
			return pOTrap.Owner | (pOTrap.Quality << 5);
		}

		/// <summary>A damage trap with "owner" set poisons instead of doing
		/// damage. Of 26 damage traps in the game, two do this.</summary>
		public static bool IsPoisonDamageTrap(UWObject pOTrap)
		{
			return pOTrap.Owner != 0;
		}

		/// <summary>a_delete object trap - removes an object from the world. Usually the
		/// fake wall, after the terrain trap has opened the tile behind it.</summary>
		public const int DeleteObjectTrapId = 0x018B;

		/// <summary>a_set variable trap. Changes a game variable (see UWGameVariables)
		/// or, for variable number zero, a quest flag. The number is in ZPos, the
		/// operation in Heading, the right operand in Quality/Owner/YPos - see
		/// GetVariableOperand.</summary>
		public const int SetVariableTrapId = 0x018D;

		/// <summary>a_check variable trap. Compares one or more game variables and
		/// branches the trap chain accordingly.</summary>
		public const int CheckVariableTrapId = 0x018E;

		/// <summary>a_spelltrap. Casts a spell: Quality is the major class, Owner the
		/// minor class (the reference: a_spell_trap.Activate). In uw1 there are two - on level 5
		/// at 10/45 a projectile spell (5/3) and on level 7 at 55/6 a
		/// cutscene (14/3).</summary>
		public const int SpellTrapId = 0x0186;

		/// <summary>a_pit trap. Toggles a tile between height zero and a
		/// preset height - see GetPitTrapHeight.</summary>
		public const int PitTrapId = 0x0184;

		/// <summary>
		/// a_ward trap. Does not occur in any level of the shipped data - it is only created
		/// during play when the player places a ward rune (spell class 8, minor class 3,
		/// see UWSummonSpell).
		///
		/// "quality" is the class of creature it responds to; 0x3F means "to
		/// any". The ward rune sets it with 0x3F.
		/// </summary>
		public const int WardTrapId = 0x0189;

		/// <summary>The "any creature" marker in the quality field of the a_ward trap.</summary>
		public const int WardTrapAnyClass = 0x3F;

		/// <summary>an_inventory trap. Checks whether the player carries a specific item
		/// - see GetInventoryTrapItemId.</summary>
		public const int InventoryTrapId = 0x018C;

		/// <summary>
		/// Which item an an_inventory trap looks for. Quality forms the upper, Owner
		/// the lower digits (the reference: an_inventory_trap.Activate).
		///
		/// The only trap of this kind in uw1 is on level 7 at 21/41 and carries
		/// quality 8 and owner 17 - together 273, a crystal splinter.
		/// </summary>
		public static int GetInventoryTrapItemId(UWObject pOTrap)
		{
			return ((pOTrap.Quality & 0x3F) << 5) | (pOTrap.Owner & 0x3F);
		}

		/// <summary>
		/// To which height an a_pit trap raises its tile, in height steps.
		///
		/// It is NOT in ZPos but in YPos and XPos combined (the reference:
		/// a_pit_trap.Activate). In the only instance in uw1 - level 3 at 6/4 - both are
		/// zero, so the trap raises to zero and thus does nothing visible.
		/// </summary>
		public static int GetPitTrapHeight(UWObject pOTrap)
		{
			return (pOTrap.YPos << 3) + pOTrap.XPos;
		}

		/// <summary>an_open trigger. Fires when a door is opened.
		///
		/// IT HANGS OFF THE DOOR CHAIN, not in the tile list: the door points via sp_link
		/// to its a_lock, and that one's link leads on to the trigger. Counted across all nine
		/// levels there are six of them, on levels 1, 3, 7 and 8 - not two,
		/// as Todo.md first claimed.</summary>
		public const int OpenTriggerId = 0x01A5;

		/// <summary>
		/// The right operand of a variable trap: Quality and Owner together form the
		/// upper digits, YPos the lower three.
		///
		/// Both variable traps use the same composition - for one it is the
		/// value used in the calculation, for the other the target value of the comparison.
		/// </summary>
		public static int GetVariableOperand(UWObject pOTrap)
		{
			return ((((int)pOTrap.Quality << 5) | pOTrap.Owner) << 3) | pOTrap.YPos;
		}

		/// <summary>"quality" of an a_door trap: 1 open only, 2 close only,
		/// 3 toggle (uw-formats.txt 6.1.2).</summary>
		public const int DoorTrapOpenAction = 1;

		public const int DoorTrapCloseAction = 2;

		public const int DoorTrapToggleAction = 3;

		/// <summary>Cutscene with the window images - uw-formats.txt literally calls them
		/// "look" graphics for windows to abyss volcano core.</summary>
		public const string WindowCutsceneFile = "CS400.N01";

		/// <summary>The picture parchment. A scroll without text shows this
		/// plan in the view window instead of letters - labelled LOCKED, TRAP and TELEPORT.
		///
		/// Found by searching the CS4xx files and recognised by the user from a screenshot of
		/// the original (2026-09-03). The file has three images, the second
		/// equals the first - image 0 is shown.
		///
		/// ASSUMPTION: that EVERY textless scroll shows this one image. Only the one
		/// case from the user's save game is established. If there were several different images, something
		/// on the object would have to tell them apart - its flags would be the first suspect,
		/// this scroll carries a 2 there instead of the usual 0.</summary>
		public const string PictureScrollCutsceneFile = "CS410.N01";

		/// <summary>The two wall textures that show a window opening. Wall decorations
		/// (object 366/367) take their texture via the owner field from the level's wall texture list,
		/// so their meaning lies solely in the texture. On all nine levels
		/// the eleven windows use only these two indices (level 3 uses 127, all others
		/// 142).
		///
		/// ONLY A FALLBACK NOW: TERRAIN.DAT assigns every wall texture a terrain type,
		/// and exactly these two are listed there as Window - see UWTerrain.IsWindowWall, which
		/// UWObjectSpawner prefers. The list here only applies when the file
		/// is missing.</summary>
		public static bool IsWindowTexture(int piTextureIndex)
		{
			return piTextureIndex == 127 || piTextureIndex == 142;
		}

		/// <summary>Frame in WindowCutsceneFile for a level: the deeper, the more lava
		/// is visible below the window. Windows exist on levels 1 to 7, which are
		/// frames 0 to 6 - frames 0 and 1 are identical, from frame 2 the image becomes
		/// increasingly red. Frame 7 is not an image of its own but the usual
		/// animator delta back to frame 0 for the loop.</summary>
		public static int GetWindowFrame(int piLevelIndex)
		{
			if (piLevelIndex < 0)
				return 0;

			return piLevelIndex > 6 ? 6 : piLevelIndex;
		}

		/// <summary>The object that carries a spell: id 288, decomposed 4-2-0. A
		/// wand links to it instead of carrying the spell itself.</summary>
		public const int SpellObjectId = 288;

		/// <summary>Bit 2 of the flags. It decides how the link is to be read - a rune spell
		/// number with the bit set, major and minor class without it (see
		/// UWArmourProtection.TryGetWearableEnchantment, which reads the other form).</summary>
		public const int SpellFormatFlag = 0x4;

		/// <summary>
		/// Which rune spell an item casts, or -1.
		///
		/// A wand does NOT carry the spell itself. It has no quantity, and its
		/// link field points to a separate object of type 288 - decomposed 4-2-0, exactly
		/// what the reference searches for in the chain. That object's link carries the spell, its
		/// quality the charges.
		///
		/// How the link is to be read is given by bit 2 of the flags:
		///
		/// - SET: major class is (link &amp; 0x1FF) >> 6. If it is zero, the
		///   lower six bits simply hold the number of a rune spell. Otherwise it would be a
		///   class from 12 upwards, which we do not have.
		/// - NOT SET: the form of weapon enchantments, (link &amp; 0x1FF) >> 4 and
		///   (link &amp; 0xF). Not a castable spell.
		///
		/// Cross-checked against the user's save game (2026-09-05): his wand links to a
		/// spell object with flags 12 and link 538. Bit 2 is set, 538 &amp; 0x1FF gives 26,
		/// of which >> 6 is zero - so rune spell 26, which is Name Enchantment. That is exactly the
		/// wand he has in his inventory. The enchanted dagger next to it has flags 8, where the bit
		/// is not set, and the other form applies.
		/// </summary>
		public static int GetObjectSpellIndex(UWObject pOObject, IList<UWObject> pORecords,
			out int piCharges)
		{
			piCharges = 0;

			UWObject lOSpell = FindSpellObject(pOObject, pORecords);

			if (lOSpell == null)
				return -1;

			if ((lOSpell.Flags & SpellFormatFlag) == 0)
				return -1;

			int liRaw = lOSpell.Quantity & 0x1FF;

			if ((liRaw >> 6) != 0)
				return -1;

			piCharges = lOSpell.Quality;

			return liRaw & 0x3F;
		}

		/// <summary>Item spells start at this major class: in the spell form a non-zero
		/// (link &amp; 0x1FF) >> 6 is added to 12, as the original's enchantment lookup does
		/// (see UWItemUse.fTryCastFromItem).</summary>
		public const int FirstItemSpellMajorClass = 13;

		/// <summary>
		/// The other half of GetObjectSpellIndex: a spell only items carry, given by its major
		/// class (13 and up) and minor class instead of a rune spell number. False when the item
		/// carries none or a rune spell.
		///
		/// Of the real items in the nine levels (scanned 2026-09-25) only two such spells occur:
		/// 13/5 Hallucination on five red potions, a green potion and a scroll, and 13/3 on the
		/// wand of the Frog on level 4 at 46/47. What they do is UWItemSpellRules.
		/// </summary>
		public static bool TryGetItemClassSpell(UWObject pOObject, IList<UWObject> pORecords,
			out int piMajorClass, out int piMinorClass)
		{
			piMajorClass = -1;
			piMinorClass = 0;

			UWObject lOSpell = FindSpellObject(pOObject, pORecords);

			if (lOSpell == null || (lOSpell.Flags & SpellFormatFlag) == 0)
				return false;

			int liRaw = lOSpell.Quantity & 0x1FF;

			if ((liRaw >> 6) == 0)
				return false;

			piMajorClass = (liRaw >> 6) + FirstItemSpellMajorClass - 1;
			piMinorClass = liRaw & 0x3F;

			return true;
		}

		/// <summary>
		/// Finds the spell object. There are THREE cases, and the third was missing until 2026-09-09:
		///
		///   1. The item IS the spell object (type 288).
		///   2. It has NO quantity and links to a chain - the spell object lies there.
		///      This is how a wand is attached to its spell.
		///   3. It has a quantity AND is enchanted - then it carries the spell ITSELF, and
		///      the quantity field is not a quantity at all but the spell id.
		///
		/// CASE 3 IS THE POTION. SAVE3 holds six of them, all with the quantity bit, all with the
		/// enchanted bit, all with flags 12 - the same value as the spell object on the user's
		/// wand. Their quantity field holds 533, 534, 541, 542, 548 and 551; read via
		/// GetObjectSpellIndex that gives rune spells 21, 22, 29, 30, 36 and 39.
		///
		/// Without this case a potion had no effect - and worse: code that took its quantity field
		/// for a count and decremented it silently changed its spell.
		/// Hence IsStackable alongside.
		///
		/// Doors are excluded, as in the reference: for them the same bit means
		/// something else.
		/// </summary>
		public static UWObject FindSpellObject(UWObject pOObject, IList<UWObject> pORecords)
		{
			if (pOObject == null)
				return null;

			if (pOObject.ID == SpellObjectId)
				return pOObject;

			if (pOObject.HasQuantity)
			{
				return pOObject.IsEnchanted
					&& pOObject.GetCategory() != UWObject.ObjectCategoryEnum.Doors
					? pOObject : null;
			}

			if (pOObject.Quantity == 0)
				return null;

			// A RESOLVED CHAIN comes first - see fFindInResolvedChain.
			if (pOObject.Contents != null)
				return fFindInResolvedChain(pOObject, SpellObjectId);

			if (pORecords == null)
				return null;

			int liAt = pOObject.Quantity;

			// The chain is finite, but a broken file could point in a circle.
			for (int liStep = 0; liStep < pORecords.Count && liAt > 0 && liAt < pORecords.Count; liStep++)
			{
				UWObject lOAt = pORecords[liAt];

				if (lOAt == null)
					return null;

				if (lOAt.ID == SpellObjectId)
					return lOAt;

				liAt = lOAt.Link;
			}

			return null;
		}

		/// <summary>
		/// Is the quantity field really a count - so may one subtract from it?
		///
		/// TWO REASONS WHY NOT. Without the quantity bit it is a link to an object chain.
		/// And if the item is ENCHANTED, it carries the spell id instead of a number
		/// (see FindSpellObject) - which is exactly why the potions' values in SAVE3 are
		/// above five hundred. uw-formats.txt 4.3 gives the same threshold, and
		/// UWInventoryWeight uses it: from 512 the field is a special property.
		/// </summary>
		public static bool IsStackable(UWObject pOObject)
		{
			return pOObject != null && pOObject.HasQuantity && !pOObject.IsEnchanted
				&& pOObject.Quantity < SpecialPropertyThreshold;
		}

		/// <summary>From here on the quantity field is a special property, not a count.
		/// </summary>
		public const int SpecialPropertyThreshold = 512;

		/// <summary>
		/// Finds a damage trap in an item's chain - the same search as
		/// FindSpellObject, only for a different object number.
		///
		/// NEEDED FOR POISONED POTIONS: if no spell is attached to a potion, the
		/// reference searches here instead (potion.QuaffPotion). The trap's quality is the
		/// base damage, its owner field says whether it is poison.
		/// </summary>
		/// <summary>
		/// THE CHAIN OF A CARRIED ITEM, already resolved into its Contents: an item from the save
		/// has it resolved against the save's records (UWPlayerData), one picked up from the floor
		/// against the level's (UWInventoryModel.PickUpToCursor). Its links point into one of the
		/// two lists, and the callers only know the save's - the poisoned potion on level 2,
		/// 14/51, picked up and drunk, found no trap there and did not poison (per user,
		/// 2026-09-30).
		/// </summary>
		private static UWObject fFindInResolvedChain(UWObject pOObject, int piObjectId)
		{
			foreach (UWObject lOAt in pOObject.Contents)
			{
				if (lOAt != null && lOAt.ID == piObjectId)
					return lOAt;
			}

			return null;
		}

		public static UWObject FindDamageTrapInChain(UWObject pOObject, IList<UWObject> pORecords)
		{
			if (pOObject == null || pOObject.HasQuantity || pOObject.Quantity == 0)
				return null;

			if (pOObject.Contents != null)
				return fFindInResolvedChain(pOObject, DamageTrapId);

			if (pORecords == null)
				return null;

			int liAt = pOObject.Quantity;

			for (int liStep = 0; liStep < pORecords.Count && liAt > 0 && liAt < pORecords.Count; liStep++)
			{
				UWObject lOAt = pORecords[liAt];

				if (lOAt == null)
					return null;

				if (lOAt.ID == DamageTrapId)
					return lOAt;

				liAt = lOAt.Link;
			}

			return null;
		}

		/// <summary>
		/// The three trap types that can be disarmed: damage, teleport, arrow.
		/// The reference checks for this whether the lower six bits of the object number are at most
		/// two (trapdisarming.DisarmTrap) - that matches exactly 384, 385 and 386.
		/// </summary>
		public const int LastDisarmableTrapIndex = 2;

		/// <summary>
		/// Searches an item's chain for a trap that can be
		/// disarmed - as the reference does on looking and on disarming
		/// (trapdisarming).
		///
		/// THE CHAIN CAN HAVE TWO FORMS: either the trap hangs directly off the
		/// item, or a TRIGGER hangs off it and the trap off that. Both
		/// are in the same major class 6 and differ in the minor class: from
		/// two it is a trigger (objects from 416), below that a trap (384 to 415).
		///
		/// pOTrigger stays null when the trap hangs directly.
		///
		/// READ IN UW.EXE 2026-09-30 (SearchForTrap_ovr143_1829, DefuseTrap_sub_8E27D): nothing
		/// with the quantity bit, nothing with an empty special link; the first object of major
		/// class 6 in the chain under the special link; a trigger leads on through ITS special
		/// link (word +6, GetNextOrLinkedObject), not its next - until then ours followed the
		/// trigger's next link.
		/// </summary>
		public static bool TryFindDisarmableTrap(UWObject pOObject, IList<UWObject> pORecords,
			out UWObject pOTrap, out UWObject pOTrigger)
		{
			pOTrap = null;
			pOTrigger = null;

			if (pOObject == null || pORecords == null || pOObject.HasQuantity || pOObject.Quantity == 0)
				return false;

			int liAt = pOObject.Quantity;

			for (int liStep = 0; liStep < pORecords.Count && liAt > 0 && liAt < pORecords.Count; liStep++)
			{
				UWObject lOAt = pORecords[liAt];

				if (lOAt == null)
					return false;

				if ((lOAt.ID >> 6) == TrapMajorClass)
				{
					UWObject lOTrap = lOAt;

					if (((lOAt.ID >> 4) & 0x3) >= 2)
					{
						// A trigger: the trap hangs off ITS special link.
						if (lOAt.Quantity <= 0 || lOAt.Quantity >= pORecords.Count)
							return false;

						pOTrigger = lOAt;
						lOTrap = pORecords[lOAt.Quantity];
					}

					if (lOTrap == null || (lOTrap.ID & 0x3F) > LastDisarmableTrapIndex)
						return false;

					pOTrap = lOTrap;

					return true;
				}

				liAt = lOAt.Link;
			}

			return false;
		}

		/// <summary>Major class of traps and triggers.</summary>
		public const int TrapMajorClass = 6;

		/// <summary>
		/// Light sources: 144 to 147 unlit, 148 to 151 lit.
		///
		/// Read from the names in string block 4: lantern, torch, candle, taper, then
		/// the same four as "lit". So lighting simply means adding four.
		/// </summary>
		public const int FirstUnlitLightId = 144;

		public const int LastUnlitLightId = 147;

		public const int LitLightOffset = 4;

		public static bool IsUnlitLight(int piObjectId)
		{
			return piObjectId >= FirstUnlitLightId && piObjectId <= LastUnlitLightId;
		}

		public static bool IsLitLight(int piObjectId)
		{
			return piObjectId >= FirstUnlitLightId + LitLightOffset
				&& piObjectId <= LastUnlitLightId + LitLightOffset;
		}

		/// <summary>A light source, lit or not - the eight numbers 144 to 151.
		/// The reference writes "(object number &amp; 0x1F8) equals 0x90" for this, which matches exactly
		/// the same eight. Needed for the condition word, see
		/// UWCombat.GetConditionStringIndex.</summary>
		public static bool IsAnyLight(int piObjectId)
		{
			return (piObjectId & 0x1F8) == FirstUnlitLightId;
		}

		/// <summary>The lantern - first of the four light sources.</summary>
		public const int LanternObjectId = 144;

		/// <summary>The torch.</summary>
		public const int TorchObjectId = 145;

		/// <summary>Two object numbers are both called "a_piece of wood". A piece of wood
		/// and an oil flask make a torch.</summary>
		public const int FirstWoodObjectId = 204;

		public const int LastWoodObjectId = 205;

		/// <summary>The spike. It can be used to spike a closed door.</summary>
		public const int SpikeObjectId = 295;

		/// <summary>The rock hammer. It smashes boulders.</summary>
		public const int RockHammerObjectId = 296;

		/// <summary>The oil flask.</summary>
		public const int OilFlaskObjectId = 301;

		/// <summary>The Key of Infinity, assembled from the three
		/// key parts (see UWObjectCombining).</summary>
		public const int KeyOfInfinityObjectId = 231;

		/// <summary>The four boulders, from largest to smallest. The rock hammer turns
		/// each into the next smaller one; the smallest becomes sling stones.</summary>
		public const int FirstBoulderObjectId = 339;

		public const int LastBoulderObjectId = 342;

		/// <summary>The sling stone - what remains of the smallest boulder.</summary>
		public const int SlingStoneObjectId = 16;

		/// <summary>Maximum fill of a light source. Oil adds 32, more than that
		/// does not fit.</summary>
		public const int FullLightQuality = 63;

		/// <summary>This much oil fits in a flask.</summary>
		public const int OilFlaskFill = 32;

		/// <summary>What a freshly soaked torch starts with.</summary>
		public const int NewTorchQuality = 40;

		/// <summary>
		/// Sets an item's object number and fetches its image and texture again.
		///
		/// Both depend on the number and would otherwise stay the old ones - a piece of wood that
		/// becomes a torch kept showing the image of the wood. Lives here and not in the
		/// UI because by now several places repurpose an object: lighting,
		/// breaking, oil flask, rock hammer and combining from CMB.DAT.
		/// </summary>
		public static void SetObjectId(UWObject pOItem, int piObjectId, UWTextures pOTextures)
		{
			if (pOItem == null)
				return;

			pOItem.ID = (ushort)piObjectId;

			if (pOTextures == null)
				return;

			try
			{
				UWTexture lOTexture = pOTextures.GetTextureByType(
					UWTexture.TextureTypes.OBJECTS, piObjectId);

				pOItem.Icon = lOTexture;
				pOItem.Texture = lOTexture;
			}
			catch
			{
				// Without an image the old one stays - no reason to revert the number.
			}
		}

		/// <summary>
		/// A burning light source goes out: its number drops back by LitLightOffset to the unlit
		/// counterpart and the image follows (SetObjectId); the remaining fuel stays in the quality.
		/// False if it was not burning. ONE mechanism for five events - burned out, put into the
		/// inventory outside a hand, put out by the use command, put down, landed after a throw.
		/// Until 2026-09-18 two of them carried a hand copy of SetObjectId (first pass of the
		/// duplicate audit, Todo 11.2 item 19).
		/// </summary>
		public static bool Extinguish(UWObject pOItem, UWTextures pOTextures)
		{
			if (pOItem == null || !IsLitLight(pOItem.ID))
				return false;

			SetObjectId(pOItem, pOItem.ID - LitLightOffset, pOTextures);

			return true;
		}
	}
}
