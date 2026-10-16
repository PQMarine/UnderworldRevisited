using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ONE WAY A NEW OBJECT COMES INTO BEING during play, after CreateObj (UnderworldExhumed
	/// MAPADDR.C, read 2026-10-12): quality 40, no owner, no next link, z 0, heading 0, fine
	/// position 3/3 (near the middle of a tile), the flags and the id bits (enchanted, door
	/// direction, invisible) clear. An item whose COMOBJ stack class is 0 or 2 starts as a stack
	/// of one (the quantity bit with a link of 1); any other has a link of 0 and no quantity bit.
	///
	/// What a caller does AFTERWARDS stays the caller's: creat_spell (SPELLS.C) sets 0x3F on
	/// the conjured food and runs creature_obj_init on a summoned creature
	/// (UWCritterRecord.InitialiseAsNew); go_fish's fish gets 0x3F (USEITEMS.C UseMagic);
	/// plant_seed sets the tree's tenacious bit (SKILLS.C); npc_inv_create gives the barter's
	/// item 0x3F (BARTER.C); add_animobj writes the first frame into the owner field
	/// (UWLevelLoader.fBuildObject). The creation mark (UWObject.MarkSpawned) is the caller's
	/// too. The picture comes from UWObjectAppearance.
	///
	/// Until 2026-10-12 this was written at sixteen places with three qualities (0x28, 0x3F and
	/// an unset 0) and two quantity rules (cleanup plan section 4.1, "fresh object creation").
	/// </summary>
	public static class UWObjectFactory
	{
		/// <summary>What CreateObj writes into the quality field of every new object: 40.</summary>
		public const int DefaultQuality = 0x28;

		/// <summary>The fine position CreateObj gives: 3/3, near the middle of the tile.</summary>
		public const int DefaultFinePosition = 3;

		/// <summary>A quantity or quality argument that asks for CreateObj's own value.</summary>
		public const int Unset = -1;

		/// <summary>
		/// A new object of this kind. piQuantity Unset leaves the stack rule to COMOBJ, as
		/// CreateObj does; above 0 makes it a stack of that many (the quantity bit set, as a
		/// caller that knows its count sets it); 0 gives no count and no bit (what the sites
		/// meant by 0 before the factory; the logic objects of the ward rune ask for it).
		/// piQuality 0 to 63 is written as given; Unset gives 40. A creature id gives a UWNpc,
		/// the mobile record, with its nineteen bytes zero (CreateObj takes a creature from the
		/// mobile list and does not initialise it).
		/// </summary>
		public static UWObject Create(int piObjectId, UWCommonObjectProperties pOCommon, int piQuantity = Unset,
			int piQuality = Unset)
		{
			UWObject lOObject = UWObjectIds.IsCreature(piObjectId)
				? new UWNpc((ushort)piObjectId)
				: new UWObject((ushort)piObjectId);

			lOObject.Quality = (ushort)(piQuality >= 0 ? piQuality & UWObjectIds.MaxQuality : DefaultQuality);
			lOObject.Owner = 0;
			lOObject.Link = 0;
			lOObject.ZPos = 0;
			lOObject.Heading = 0;
			lOObject.XPos = DefaultFinePosition;
			lOObject.YPos = DefaultFinePosition;
			lOObject.Flags = 0;
			lOObject.IsEnchanted = false;
			lOObject.DoorDirection = false;
			lOObject.IsHidden = false;

			if (piQuantity > 0)
			{
				lOObject.HasQuantity = true;
				lOObject.Quantity = (ushort)piQuantity;
			}
			else if (piQuantity == Unset && StartsWithQuantity(pOCommon, piObjectId))
			{
				lOObject.HasQuantity = true;
				lOObject.Quantity = 1;
			}
			else
			{
				lOObject.HasQuantity = false;
				lOObject.Quantity = 0;
			}

			return lOObject;
		}

		/// <summary>Create with the picture attached - for a caller that has the game data at
		/// hand. Null when the data has no picture for the id (the sites that built their
		/// object by hand gave up then; the spawner skips an object without one).</summary>
		public static UWObject Create(int piObjectId, DataImport pOData, int piQuantity = Unset, int piQuality = Unset)
		{
			if (pOData == null)
				return null;

			UWObject lOObject = Create(piObjectId, pOData.CommonObjectProperties, piQuantity, piQuality);

			return UWObjectAppearance.Apply(lOObject, pOData.Textures, null) ? lOObject : null;
		}

		/// <summary>CreateObj's stack rule: COMOBJ stack class 0 or 2 starts as a stack of one.
		/// An id the table does not know starts with none.</summary>
		public static bool StartsWithQuantity(UWCommonObjectProperties pOCommon, int piObjectId)
		{
			UWCommonObjectProperties.Entry lOEntry;

			return pOCommon != null && pOCommon.TryGet(piObjectId, out lOEntry) && lOEntry.StartsWithQuantity;
		}

		/// <summary>CreateObj's count for a new object of this kind: 1 for a stack class of 0
		/// or 2, else 0 (the loot and the barter pass it on as a quantity argument).</summary>
		public static int NewQuantity(UWCommonObjectProperties pOCommon, int piObjectId)
		{
			return StartsWithQuantity(pOCommon, piObjectId) ? 1 : 0;
		}
	}
}
