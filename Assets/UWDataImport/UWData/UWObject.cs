using System.Collections.Generic;

namespace UWDataImport.UWData
{
	public class UWObject
	{
		public enum ObjectCategoryEnum
		{
			Unknown = -1,
			WeaponsAndMissiles,
			ArmorAndClothing,
			Monsters,
			Containers,
			LightSources,
			Wands,
			Treasure,
			Comestibles,
			SceneryAndJunk,
			RunesAndKeyOfInfinityParts,
			KeysLockpickLock,
			QuestItems,
			InventoryItemsMiscStuff,
			BooksAndScrolls,
			Doors,
			Furniture,
			PillarSomeDecalsForceFieldSpecialTmapObj,
			Switches,
			Traps,
			Triggers,
			ExplosionsSplatsFountainSilverTreeMovingThings
		}

		public enum Headings
		{
			South,
			SouthWest,
			West,
			NorthWest,
			North,
			NorthEast,
			East,
			SouthEast
		}

		public ushort ID;

		public ushort Link;

		public ushort Quality;

		public ushort Owner;

		public ushort Quantity;

		/// <summary>The flags, bits 9-12 of the record's first word: FOUR bits, as the original's
		/// OBJ_FLAGS reads and SET_FLAGS writes them (object.h), with the enchanted bit as the top
		/// one. The single truth since 2026-10-12 (decision 4 of the cleanup plan, B13): until
		/// then the writers put three bits here and a separate IsEnchanted bool into bit 12,
		/// which re-added a stale bit after a four-bit write had cleared it. See
		/// UWObjectRecordCodec.</summary>
		public ushort Flags;

		/// <summary>Bit 3 of Flags, bit 12 of the word (ID_ENCHANT) - a view on Flags, not a
		/// second store.</summary>
		public bool IsEnchanted
		{
			get { return (Flags & UWObjectRecordCodec.EnchantedFlag) != 0; }
			set
			{
				Flags = (ushort)(value
					? Flags | UWObjectRecordCodec.EnchantedFlag
					: Flags & ~UWObjectRecordCodec.EnchantedFlag);
			}
		}

		public bool DoorDirection;

		public bool IsHidden;

		public bool HasQuantity;

		public int ZPos;

		public ushort Heading;

		public ushort YPos;

		public ushort XPos;

		public int TileX;

		public int TileY;

		/// <summary>
		/// Brought into the game only at runtime by a spawner trap, not loaded from the
		/// object list.
		///
		/// The original keeps a bit on the object for this and checks it during the regular
		/// refill: if a creature spawned this way is already roaming near the player,
		/// no further one is added (see UWCreatureRespawner). Without that the
		/// monsters pile up at a spot where one stays for a longer time.
		/// </summary>
		public bool WasSpawned;

		/// <summary>Created during play: the port's WasSpawned, or for a creature the original's bit
		/// (UWNpc.NPCSpawned, word 0x0D bit 8), which is what the save carries. Until 2026-10-11 the
		/// respawn's crowding check asked WasSpawned alone, which no save keeps: after a load the
		/// trap put a second creature onto the first (per user).</summary>
		public bool IsSpawned
		{
			get { return WasSpawned || (this is UWNpc lONpc && lONpc.NPCSpawned); }
		}

		/// <summary>Marks an object made during play; a creature gets the original's bit too, so
		/// the mark survives the save and the level exit removes it (UWLevelExit).</summary>
		public void MarkSpawned()
		{
			WasSpawned = true;

			if (this is UWNpc lONpc)
				lONpc.NPCSpawned = true;
		}

		/// <summary>
		/// The object's number in the current level's object list while it is carried, 0 while
		/// it has none (see UWCarriedSlots). In the original the inventory lives in the level's
		/// static slots, and the barter seeds each item's appraisal with that number.
		/// </summary>
		public int SlotIndex;

		public UWTexture Texture { get; set; }

		public UWTexture Icon { get; set; }

		public Headings HeadingEnum => (Headings)Heading;

		public UWObject(ushort piItem_id)
		{
			ID = piItem_id;
			if (ID != 320)
			{
			}
		}

		public UWObject GetNextObjectInLine(List<UWObject> pOMasterObjectList)
		{
			if (Link == 0)
			{
				return null;
			}
			return pOMasterObjectList[Link];
		}

		/// <summary>Container contents (only relevant for Containers, but usable in general).
		/// According to uw-formats.txt, word 0006 (already parsed here as Owner/Quantity,
		/// see UWLevel.load_object_list) is not a quantity field when HasQuantity=false, but a
		/// "sp_link": index of the FIRST contained object. Its neighbours in the same
		/// container are linked together via the normal link chain (GetNextObjectInLine) -
		/// the same chaining mechanism as in UWTile.LoadObjects, only anchored at a different
		/// starting point (the container object itself instead of a tile).</summary>
		public List<UWObject> Contents { get; private set; }

		/// <summary>
		/// A copy of the same item with a different quantity - for splitting a
		/// stack while dragging. Container contents are NOT copied along: a stack is never
		/// a container, and split contents would exist twice.
		/// </summary>
		public UWObject CloneWithQuantity(int piQuantity)
		{
			UWObject lOCopy = new UWObject(ID)
			{
				Flags = Flags,
				DoorDirection = DoorDirection,
				IsHidden = IsHidden,
				HasQuantity = HasQuantity,
				ZPos = ZPos,
				Heading = Heading,
				YPos = YPos,
				XPos = XPos,
				Quality = Quality,
				Link = 0,
				Owner = Owner,
				Quantity = (ushort)piQuantity,
				Texture = Texture,
				Icon = Icon
			};

			return lOCopy;
		}

		/// <summary>Resolves Contents once (in-memory cache) - after that Contents is the
		/// sole truth for UI mutations (dragging in/out); Link/Quantity of the
		/// individual objects are not updated here. When saving, the chain is rebuilt from
		/// Contents (see UWLevelWriter).</summary>
		public void EnsureContentsLoaded(List<UWObject> pOMasterObjectList)
		{
			if (Contents != null)
			{
				return;
			}

			Contents = new List<UWObject>();

			if (HasQuantity || Quantity == 0)
			{
				return;
			}

			// A chain that points back into itself would run forever - seen while dumping all
			// containers of the nine levels (2026-09-16, out of memory). The original walks the
			// same links, so such a chain can only come from damaged data or from an object
			// whose sp_link is not a container link at all. UWObjectChain stops at the first
			// object seen twice (2026-10-11; before that a HashSet in a loop here).
			foreach (UWObject lOItem in UWObjectChain.Walk(this, pOMasterObjectList, pOMasterObjectList.Count))
				Contents.Add(lOItem);
		}

		/// <summary>
		/// A copy of the object as the create-object trap makes one (UnderworldExhumed TRIGGER.C
		/// TRAP_CREATE_OBJECT, read 2026-10-12): the record copied field by field, and of what the
		/// template holds (a container's contents, a creature's inventory, a wand's spell) only
		/// the FIRST item, copied as well, with its own next link and its own contents cut. The
		/// copy's chain is its own: a thing the created container holds is not the one the
		/// template holds.
		///
		/// pOMasterlist resolves a template whose contents are not loaded yet; without it (the
		/// door trap's lock, which holds nothing) a template with unresolved contents keeps its
		/// fields as they are. Until 2026-10-12 the copy shared the template's contents list and,
		/// when that was not loaded yet, resolved the template's chain for itself later: the
		/// created creature carried and dropped the template's very items.
		/// </summary>
		public static UWObject Clone(UWObject pOObject, List<UWObject> pOMasterlist = null)
		{
			if (pOObject == null)
				return null;

			UWObject lOCopy = pOObject.MemberwiseClone() as UWObject;

			if (lOCopy == null)
				return null;

			// The copy owns its nineteen bytes; until 2026-10-11 it shared the template's, so a
			// bit set on one (the spawned mark, the AI flags) changed the other.
			if (pOObject is UWNpc lONpc && lOCopy is UWNpc lONpcCopy && lONpc.RawNpcBytes != null)
				lONpcCopy.RawNpcBytes = (byte[])lONpc.RawNpcBytes.Clone();

			if (pOObject.HasQuantity)
				return lOCopy;

			if (pOObject.Contents == null && pOObject.Quantity != 0 && pOMasterlist != null)
				pOObject.EnsureContentsLoaded(pOMasterlist);

			if (pOObject.Contents == null)
				return lOCopy;

			lOCopy.Contents = new List<UWObject>();
			lOCopy.Quantity = 0;

			UWObject lOFirst = pOObject.Contents.Count > 0 ? pOObject.Contents[0] : null;

			if (lOFirst == null)
				return lOCopy;

			UWObject lOFirstCopy = lOFirst.MemberwiseClone() as UWObject;

			if (lOFirstCopy == null)
				return lOCopy;

			if (lOFirst is UWNpc lOFirstNpc && lOFirstCopy is UWNpc lOFirstNpcCopy && lOFirstNpc.RawNpcBytes != null)
				lOFirstNpcCopy.RawNpcBytes = (byte[])lOFirstNpc.RawNpcBytes.Clone();

			lOFirstCopy.Link = 0;

			if (!lOFirstCopy.HasQuantity)
			{
				lOFirstCopy.Quantity = 0;
				lOFirstCopy.Contents = new List<UWObject>();
			}

			lOCopy.Contents.Add(lOFirstCopy);

			return lOCopy;
		}

		public override string ToString()
		{
			return $"ID: {ID}, Link: {Link}";
		}

		public ObjectCategoryEnum GetCategory()
		{
			if (ID <= 31)
			{
				return ObjectCategoryEnum.WeaponsAndMissiles;
			}
			if (ID >= 32 && ID <= 63)
			{
				return ObjectCategoryEnum.ArmorAndClothing;
			}
			if (ID >= 64 && ID <= 127)
			{
				return ObjectCategoryEnum.Monsters;
			}
			if (ID >= 128 && ID <= 143)
			{
				return ObjectCategoryEnum.Containers;
			}
			if (ID >= 144 && ID <= 151)
			{
				return ObjectCategoryEnum.LightSources;
			}
			if (ID >= 152 && ID <= 159)
			{
				return ObjectCategoryEnum.Wands;
			}
			if (ID >= 160 && ID <= 175)
			{
				return ObjectCategoryEnum.Treasure;
			}
			if (ID >= 176 && ID <= 191)
			{
				return ObjectCategoryEnum.Comestibles;
			}
			if (ID >= 192 && ID <= 223)
			{
				return ObjectCategoryEnum.SceneryAndJunk;
			}
			if (ID >= 224 && ID <= 255)
			{
				return ObjectCategoryEnum.RunesAndKeyOfInfinityParts;
			}
			if (ID >= 256 && ID <= 271)
			{
				return ObjectCategoryEnum.KeysLockpickLock;
			}
			if (ID >= 272 && ID <= 287)
			{
				return ObjectCategoryEnum.QuestItems;
			}
			if (ID >= 288 && ID <= 303)
			{
				return ObjectCategoryEnum.InventoryItemsMiscStuff;
			}
			if (ID >= 304 && ID <= 319)
			{
				return ObjectCategoryEnum.BooksAndScrolls;
			}
			if (ID >= 320 && ID <= 335)
			{
				return ObjectCategoryEnum.Doors;
			}
			if (ID >= 336 && ID <= 351)
			{
				return ObjectCategoryEnum.Furniture;
			}
			if (ID >= 352 && ID <= 367)
			{
				return ObjectCategoryEnum.PillarSomeDecalsForceFieldSpecialTmapObj;
			}
			if (ID >= 368 && ID <= 383)
			{
				return ObjectCategoryEnum.Switches;
			}
			if (ID >= 384 && ID <= 415)
			{
				return ObjectCategoryEnum.Traps;
			}
			if (ID >= 416 && ID <= 447)
			{
				return ObjectCategoryEnum.Triggers;
			}
			if (ID >= 448 && ID <= 463)
			{
				return ObjectCategoryEnum.ExplosionsSplatsFountainSilverTreeMovingThings;
			}
			return ObjectCategoryEnum.Unknown;
		}
	}
}
