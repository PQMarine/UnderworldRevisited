using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// What a creature search asks of a creature besides its tile (cleanup step 3.2, 2026-10-17).
	/// Flags: several together must all hold. Each is the test one of the old scene scans made.
	/// </summary>
	[Flags]
	public enum UWCreatureFilter
	{
		/// <summary>Every creature, dead or alive, with or without its object.</summary>
		Any = 0,

		/// <summary>Has a record and is not dead (the body not destroyed) - the sleep's and Detect
		/// Monsters' test.</summary>
		Living = 1,

		/// <summary>Carries its level object - the theft and trespass search skipped one
		/// without.</summary>
		HasObject = 2,

		/// <summary>Created during play (UWObject.IsSpawned, which a save keeps for a creature) - the
		/// creation trap's crowding count. Implies HasObject.</summary>
		Spawned = 4
	}

	/// <summary>
	/// A creature as a search hands it out, engine-free (cleanup step 3.2, 2026-10-17): its level
	/// object, its record, the tile its body stands on and whether it is dead. A snapshot taken at
	/// the search; the object and the record are the live ones.
	/// </summary>
	public readonly struct UWCreatureRef
	{
		/// <summary>The creature's object in the level (the entity's ObjectData); null without.</summary>
		public readonly UWObject Object;

		/// <summary>The record the brain and the motion core work on; null before the creature is
		/// set up.</summary>
		public readonly UWCritterRecord Record;

		/// <summary>The tile the body's world point lies in (UWCreatureTile.Body).</summary>
		public readonly UWTilePos BodyTile;

		/// <summary>The body is destroyed: dying or dead.</summary>
		public readonly bool IsDead;

		/// <summary>The creature's index in the level's mobile list (its masterlist slot, taken when
		/// it was set up) - who it is to the damage attribution and the level's order of active
		/// mobiles (UWLevel.GetActiveMobileOrder). 0 when not known. Since cleanup step 3.6
		/// (2026-10-17), for the special deaths' removal of Tybal's allies.</summary>
		public readonly int Index;

		public UWCreatureRef(UWObject pOObject, UWCritterRecord pORecord, UWTilePos pOBodyTile, bool pbIsDead,
			int piIndex = 0)
		{
			Object = pOObject;
			Record = pORecord;
			BodyTile = pOBodyTile;
			IsDead = pbIsDead;
			Index = piIndex;
		}

		/// <summary>The record's tile (UWCreatureTile.Record); Nowhere's tile without a record.</summary>
		public UWTilePos RecordTile => Record != null ? new UWTilePos(Record.TileX, Record.TileY) : UWSpot.Nowhere.Tile;

		/// <summary>The creature's object id, 0 without an object (Detect Monsters' old answer).</summary>
		public int ObjectId => Object != null ? Object.ID : 0;
	}

	/// <summary>
	/// THE ONE TEST of a creature search (cleanup step 3.2, 2026-10-17): IUWWorld.FindCreatures
	/// (UWWorld over the scene's active creatures, the self-check's FakeWorld over its list) asks
	/// it for every creature, so the area and the filter mean the same everywhere.
	///
	/// WHY: four hosts searched the scene for creatures with a scan of their own (the theft and
	/// trespass witnesses, the creation trap's crowding, the sleep, Detect Monsters), each with
	/// its own tile measure and its own tests (section 7.2 of Docs/CLEANUP-PLAN.md).
	/// </summary>
	public static class UWCreatureSearch
	{
		/// <summary>Whether the creature passes the filter and stands in the area by the area's
		/// measure. A creature without a record never lies in an area measured by the record,
		/// but the whole level holds it.</summary>
		public static bool Matches(in UWCreatureRef pOCreature, UWTileArea pOArea, UWCreatureFilter peFilter)
		{
			if ((peFilter & UWCreatureFilter.Living) != 0 && (pOCreature.Record == null || pOCreature.IsDead))
				return false;

			if ((peFilter & (UWCreatureFilter.HasObject | UWCreatureFilter.Spawned)) != 0 && pOCreature.Object == null)
				return false;

			if ((peFilter & UWCreatureFilter.Spawned) != 0 && !pOCreature.Object.IsSpawned)
				return false;

			if (pOArea.IsWholeLevel)
				return true;

			if (pOArea.Measure == UWCreatureTile.Record)
				return pOCreature.Record != null && pOArea.Contains(pOCreature.RecordTile);

			return pOArea.Contains(pOCreature.BodyTile);
		}

		/// <summary>Clears pOInto and adds every creature of pOAll that matches, in pOAll's order;
		/// returns how many.</summary>
		public static int Collect(IReadOnlyList<UWCreatureRef> pOAll, UWTileArea pOArea, UWCreatureFilter peFilter,
			List<UWCreatureRef> pOInto)
		{
			pOInto.Clear();

			for (int liAt = 0; pOAll != null && liAt < pOAll.Count; liAt++)
			{
				UWCreatureRef lOCreature = pOAll[liAt];

				if (Matches(lOCreature, pOArea, peFilter))
					pOInto.Add(lOCreature);
			}

			return pOInto.Count;
		}
	}
}
