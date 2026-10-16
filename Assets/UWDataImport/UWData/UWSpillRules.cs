using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// What leaves a container when it is emptied onto the floor - the one partition behind using
	/// a barrel, chest, urn or bag in the world, smashing one, destroying a bag by fire and the pack
	/// a dead creature drops (cleanup step 2.6, 2026-10-12; until then Interaction.
	/// SpillWorldContainer and UWCritterRemains each kept the loop).
	///
	/// THE ORIGINAL (combat/DAMAGE.C, obj/USEITEMS.C, obj/TREASURE.C, read 2026-10-12): UseObj
	/// hands a container of major class 0x0A minor 0 to UseCont, which first asks checkLock with
	/// no key - a lock object whose locked bit is set refuses with "The ... is locked." - and then
	/// DumpTheBag (EmptyContainer_seg040_352B_23EC), which is drop_link_chain: EVERY object of the
	/// chain - things, the lock, the triggers - goes down with put_at, 6 eighths around the
	/// container's spot, and the chain is cut. "The ... is empty." is printed only when the chain
	/// was empty and the player did the using. UseObj sets off the use trigger of what was used
	/// (checkTrap, kind 4) AFTER the class handler, when the chain is already empty, so a used
	/// container fires nothing. A smashed or burnt container is different: damage_object calls
	/// checkTrap kind 4 BEFORE remove_object empties it, so there the use trigger fires first.
	/// A dead creature spills the same way (drop_some_objects after build_corpse).
	///
	/// DECISION 25 (per user, 2026-10-12, tested on the original): the pouch on level 3 at 49/52
	/// holds a pick-up trigger that sends a headless; used in the world without picking it up,
	/// the pouch spills, and picking up the empty pouch afterwards sends nothing in the original -
	/// the trigger lies on the floor. Ours kept the lock and the triggers with the container
	/// (the reference's reading) and the headless came. Now everything goes down, as there.
	/// </summary>
	public static class UWSpillRules
	{
		/// <summary>The chain in its order; a null link is skipped. Everything goes down - see the
		/// class comment.</summary>
		public static List<UWObject> Partition(IEnumerable<UWObject> pOContents)
		{
			List<UWObject> lOSpilled = new List<UWObject>();

			if (pOContents == null)
				return lOSpilled;

			foreach (UWObject lOItem in pOContents)
			{
				if (lOItem != null)
					lOSpilled.Add(lOItem);
			}

			return lOSpilled;
		}

		/// <summary>The use triggers of a container's chain, in order - what damage_object sets
		/// off (checkTrap kind 4) before a smashed or burnt container spills.</summary>
		public static List<UWObject> UseTriggers(IEnumerable<UWObject> pOContents)
		{
			List<UWObject> lOTriggers = new List<UWObject>();

			if (pOContents == null)
				return lOTriggers;

			foreach (UWObject lOItem in pOContents)
			{
				if (lOItem != null && lOItem.ID == UWObjectMechanics.UseTriggerId)
					lOTriggers.Add(lOItem);
			}

			return lOTriggers;
		}

		/// <summary>
		/// The lock that refuses the use (checkLock without a key): THE FIRST lock object of the
		/// chain when its locked bit is set, else null - the container opens. checkLock
		/// (UnderworldExhumed USEITEMS.C) asks Obj_InList for the first lock and answers "not
		/// locked" when that one is open, whatever lies behind it; until 2026-10-14 ours refused
		/// on any locked lock of the chain (reference audit, finding of the fix agents). A smashed
		/// chest's second lock is that first lock then (TakeFirstLock).
		/// </summary>
		public static UWObject FindLockedLock(IEnumerable<UWObject> pOContents)
		{
			if (pOContents == null)
				return null;

			foreach (UWObject lOItem in pOContents)
			{
				if (lOItem != null && lOItem.ID == UWObjectMechanics.LockObjectId)
					return UWObjectMechanics.IsLockLocked(lOItem) ? lOItem : null;
			}

			return null;
		}

		/// <summary>
		/// remove_lock(obj, 0) (UnderworldExhumed combat/DAMAGE.C) for a smashed chest or barrel:
		/// the FIRST lock object of the chain is taken out and freed, whether locked or not; a
		/// second one stays. Returns it, or null without a lock.
		/// </summary>
		public static UWObject TakeFirstLock(List<UWObject> pOContents)
		{
			if (pOContents == null)
				return null;

			for (int liAt = 0; liAt < pOContents.Count; liAt++)
			{
				UWObject lOItem = pOContents[liAt];

				if (lOItem != null && lOItem.ID == UWObjectMechanics.LockObjectId)
				{
					pOContents.RemoveAt(liAt);

					return lOItem;
				}
			}

			return null;
		}

		/// <summary>Whether "The ... is empty." is due: the chain was empty (drop_link_chain counts
		/// a lock or trigger too), and the use was the player's own (DumpTheBag's to_player;
		/// smashing stays silent).</summary>
		public static bool AnnouncesEmpty(int piSpilled, bool pbPlayerUsed)
		{
			return piSpilled == 0 && pbPlayerUsed;
		}
	}
}
