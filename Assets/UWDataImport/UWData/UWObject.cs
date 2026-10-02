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

		public ushort Flags;

		public bool IsEnchanted;

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
				IsEnchanted = IsEnchanted,
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

			UWObject lOCurrent = pOMasterObjectList[Quantity];

			// A chain that points back into itself would run forever - seen while dumping all
			// containers of the nine levels (2026-09-16, out of memory). The original walks the
			// same links, so such a chain can only come from damaged data or from an object
			// whose sp_link is not a container link at all.
			System.Collections.Generic.HashSet<UWObject> lOSeen = new System.Collections.Generic.HashSet<UWObject>();

			while (lOCurrent != null && lOSeen.Add(lOCurrent))
			{
				Contents.Add(lOCurrent);
				lOCurrent = lOCurrent.GetNextObjectInLine(pOMasterObjectList);
			}
		}

		public static UWObject Clone(UWObject pOObject)
		{
			if (pOObject is UWNpc)
			{
				return pOObject.MemberwiseClone() as UWNpc;
			}
			return pOObject.MemberwiseClone() as UWObject;
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
