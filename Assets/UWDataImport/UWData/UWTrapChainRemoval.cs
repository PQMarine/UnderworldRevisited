using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// What the original removes after a ONE-SHOT trigger has run its trap (flags bit 1 clear).
	/// READ IN UW.EXE 2026-10-01 after the user's report (lever on level 3, 52/13, SAVE2 ours
	/// against SAVE3 the original): the original takes the WHOLE chain behind the trap out of the
	/// level - the use trigger on the lever, the three change terrain traps, the text trap and the
	/// two move triggers in between all on the static free list, every special link 0, the traps out
	/// of their tiles; ours, built after the reference, freed part of it and left the trap on 44/8
	/// in its tile, still linked to its move trigger.
	///
	/// Trigger_ovr153_3B, after RunTrap_ovr153_24A, whatever the trap did: trigger flags bit 1
	/// (w0 0x400) clear and its special link set -> ovr153_E83(target tile's object list, trap).
	///
	///   ovr153_E83 (list, trap)  count = trap flags (w0 bits 9-12). While it is above zero, every
	///       tile of the level in turn is scanned by ovr153_DC2. Then the trap is looked up in
	///       the list; found, RemoveFromObjectChain_seg027_2861_831 clears its own special chain
	///       (ClearObjectChain, unless it carries the quantity bit), unlinks it and frees it.
	///   ovr153_DC2 (list)  every trigger (0x1A0-0x1AF) in the list whose special link is the
	///       trap is unlinked, freed, its special link cleared, and the count goes down; every
	///       object without the quantity bit is searched down its special chain the same way -
	///       so a trigger hanging on a lever, a door or a trap is found too.
	///   ClearObjectChain_seg027_772 (chain)  its first object: a trap goes through ovr153_E83 with
	///       the chain as the list, a trigger through ovr153_135C, and that is all for this chain;
	///       anything else clears the rest of the chain behind it, then its own special chain,
	///       and is unlinked and freed.
	///   ovr153_135C (list, trigger)  the trigger's trap carries flags 1: ovr153_E83 on the
	///       trigger's target tile and that trap. Otherwise the trap's flags count down by one
	///       and the trigger alone is unlinked and freed.
	///
	/// The flags of a trap are thus a count of the triggers still pointing at it.
	/// </summary>
	public static class UWTrapChainRemoval
	{
		/// <summary>Triggers 0x1A0-0x1AF: (id &amp; 0x1F0) is 0x1A0, as ovr153_DC2 tests it.</summary>
		private const int TriggerIdMask = 0x1F0;

		private const int TriggerIdBase = 0x1A0;

		/// <summary>Minor class 0 and 1 of major class 6 are traps (0x180-0x19F), 2 and 3 triggers.</summary>
		private const int FirstTriggerMinorClass = 2;

		private const int FlagsMask = 0xF;

		/// <summary>Against a chain that runs in a circle.</summary>
		private const int MaxDepth = 64;

		/// <summary>The cleanup of Trigger_ovr153_3B for a one-shot trigger that has run.</summary>
		public static void AfterOneShotTrigger(UWObject pOTrigger, UWObject pOTrap, UWLevel pOLevel)
		{
			if (pOTrigger == null || pOTrap == null || pOLevel == null || pOLevel.TileData == null
				|| pOTrigger.Quantity == 0)
				return;

			(int liTileX, int liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);
			UWTile lOTile = pOLevel.GetTile(liTileX, liTileY);

			fRemoveTrap(new ObjectList(lOTile), pOTrap, pOLevel, 0);
		}

		/// <summary>ClearObjectChain_seg027_772 on an object's special link - what a successful
		/// disarm does to the item (DefuseTrap_sub_8E27D).</summary>
		public static void ClearSpecialChain(UWObject pOOwner, UWLevel pOLevel)
		{
			if (pOOwner == null || pOLevel == null || pOLevel.TileData == null)
				return;

			fClearChain(new ObjectList(pOOwner), pOLevel, 0);
		}

		/// <summary>ovr153_E83 with an object's special chain as the list (stub153_66) - what the
		/// original does to a trap a bumbled disarm has set off.</summary>
		public static void RemoveTrapFromChain(UWObject pOOwner, UWObject pOTrap, UWLevel pOLevel)
		{
			if (pOOwner == null || pOTrap == null || pOLevel == null || pOLevel.TileData == null)
				return;

			fRemoveTrap(new ObjectList(pOOwner), pOTrap, pOLevel, 0);
		}

		/// <summary>An object list of the original: a tile's, or an object's special chain.</summary>
		private sealed class ObjectList
		{
			private readonly UWTile mOTile;

			private readonly UWObject mOOwner;

			public ObjectList(UWTile pOTile)
			{
				mOTile = pOTile;
			}

			public ObjectList(UWObject pOOwner)
			{
				mOOwner = pOOwner;
			}

			public List<UWObject> Members(List<UWObject> pOMasterlist)
			{
				if (mOTile != null)
					return mOTile.ObjectsInTile != null ? new List<UWObject>(mOTile.ObjectsInTile) : new List<UWObject>();

				return fChainMembers(mOOwner, pOMasterlist);
			}

			public bool Contains(UWObject pOObject, List<UWObject> pOMasterlist)
			{
				return Members(pOMasterlist).Contains(pOObject);
			}

			public void Remove(UWObject pOObject, List<UWObject> pOMasterlist)
			{
				if (mOTile != null)
				{
					mOTile.ObjectsInTile?.Remove(pOObject);
					return;
				}

				fUnlinkFromChain(mOOwner, pOObject, pOMasterlist);
			}
		}

		/// <summary>ovr153_E83.</summary>
		private static void fRemoveTrap(ObjectList pOList, UWObject pOTrap, UWLevel pOLevel, int piDepth)
		{
			if (pOTrap == null || piDepth > MaxDepth)
				return;

			List<UWObject> lOMasterlist = pOLevel.Masterlist;
			int liCount = pOTrap.Flags & FlagsMask;
			int liTrapIndex = lOMasterlist.IndexOf(pOTrap);

			if (liCount > 0 && liTrapIndex > 0)
			{
				foreach (UWTile lOTile in pOLevel.TileData)
				{
					if (liCount <= 0)
						break;

					if (lOTile != null && lOTile.ObjectsInTile != null && lOTile.ObjectsInTile.Count > 0)
						fRemoveTriggersIn(new ObjectList(lOTile), liTrapIndex, ref liCount, pOLevel, 0);
				}
			}

			if (!pOList.Contains(pOTrap, lOMasterlist))
				return;

			if (!pOTrap.HasQuantity && pOTrap.Quantity != 0)
				fClearChain(new ObjectList(pOTrap), pOLevel, piDepth + 1);

			pOList.Remove(pOTrap, lOMasterlist);
			fFree(pOTrap, pOLevel);
		}

		/// <summary>ovr153_DC2.</summary>
		private static void fRemoveTriggersIn(ObjectList pOList, int piTrapIndex, ref int piCount, UWLevel pOLevel, int piDepth)
		{
			if (piDepth > MaxDepth)
				return;

			List<UWObject> lOMasterlist = pOLevel.Masterlist;

			foreach (UWObject lOAt in pOList.Members(lOMasterlist))
			{
				if (lOAt == null)
					continue;

				if ((lOAt.ID & TriggerIdMask) == TriggerIdBase && lOAt.Quantity == piTrapIndex)
				{
					pOList.Remove(lOAt, lOMasterlist);
					fFree(lOAt, pOLevel);
					lOAt.Quantity = 0;
					piCount--;
				}

				if (!lOAt.HasQuantity && lOAt.Quantity > 0 && lOAt.Quantity < lOMasterlist.Count)
					fRemoveTriggersIn(new ObjectList(lOAt), piTrapIndex, ref piCount, pOLevel, piDepth + 1);
			}
		}

		/// <summary>ClearObjectChain_seg027_772 on the whole chain: its members from the first on.</summary>
		private static void fClearChain(ObjectList pOList, UWLevel pOLevel, int piDepth)
		{
			fClearFrom(pOList, pOList.Members(pOLevel.Masterlist), 0, pOLevel, piDepth);
		}

		/// <summary>ClearObjectChain_seg027_772 on the part of a chain from member piAt on: the
		/// original's recursion on the next link of a member is the rest of the same list.</summary>
		private static void fClearFrom(ObjectList pOList, List<UWObject> pOMembers, int piAt, UWLevel pOLevel, int piDepth)
		{
			if (piAt >= pOMembers.Count || piDepth > MaxDepth)
				return;

			UWObject lOObject = pOMembers[piAt];

			if (lOObject == null)
				return;

			if ((lOObject.ID >> 6) == UWObjectMechanics.TrapMajorClass)
			{
				if (((lOObject.ID >> 4) & 0x3) < FirstTriggerMinorClass)
					fRemoveTrap(pOList, lOObject, pOLevel, piDepth + 1);
				else
					fRemoveChainTrigger(pOList, lOObject, pOLevel, piDepth + 1);

				return;
			}

			fClearFrom(pOList, pOMembers, piAt + 1, pOLevel, piDepth + 1);

			if (!lOObject.HasQuantity && lOObject.Quantity != 0)
				fClearChain(new ObjectList(lOObject), pOLevel, piDepth + 1);

			pOList.Remove(lOObject, pOLevel.Masterlist);
			fFree(lOObject, pOLevel);
		}

		/// <summary>ovr153_135C.</summary>
		private static void fRemoveChainTrigger(ObjectList pOList, UWObject pOTrigger, UWLevel pOLevel, int piDepth)
		{
			UWObject lOTrap = UWObjectMechanics.GetLinkedObject(pOTrigger, pOLevel.Masterlist);

			if (lOTrap != null && (lOTrap.Flags & FlagsMask) == 1)
			{
				(int liTileX, int liTileY) = UWObjectMechanics.GetTriggerTargetTile(pOTrigger);

				fRemoveTrap(new ObjectList(pOLevel.GetTile(liTileX, liTileY)), lOTrap, pOLevel, piDepth + 1);

				return;
			}

			if (lOTrap != null)
				lOTrap.Flags = (ushort)(((lOTrap.Flags & FlagsMask) - 1) & FlagsMask);

			pOList.Remove(pOTrigger, pOLevel.Masterlist);
			fFree(pOTrigger, pOLevel);
		}

		/// <summary>AddToFreeObjectsList_seg027_4DD: the static slot back onto the free list.</summary>
		private static void fFree(UWObject pOObject, UWLevel pOLevel)
		{
			int liIndex = pOLevel.Masterlist.IndexOf(pOObject);

			if (liIndex > 0)
				pOLevel.ReturnStaticSlot(liIndex);
		}

		/// <summary>The members of an object's special chain: its resolved contents where they are
		/// loaded (they are the truth then, UWObject.Contents), else the link fields.</summary>
		private static List<UWObject> fChainMembers(UWObject pOOwner, List<UWObject> pOMasterlist)
		{
			List<UWObject> lOMembers = new List<UWObject>();

			if (pOOwner == null)
				return lOMembers;

			if (pOOwner.Contents != null)
			{
				lOMembers.AddRange(pOOwner.Contents);
				return lOMembers;
			}

			int liAt = pOOwner.Quantity;

			for (int liStep = 0; liStep < pOMasterlist.Count && liAt > 0 && liAt < pOMasterlist.Count; liStep++)
			{
				UWObject lOAt = pOMasterlist[liAt];

				if (lOAt == null || lOMembers.Contains(lOAt))
					break;

				lOMembers.Add(lOAt);
				liAt = lOAt.Link;
			}

			return lOMembers;
		}

		/// <summary>RemoveObjectFromLinkedList_seg027_659 on an object's special chain: the link
		/// fields mended around it, the resolved contents kept in step.</summary>
		private static void fUnlinkFromChain(UWObject pOOwner, UWObject pOObject, List<UWObject> pOMasterlist)
		{
			if (pOOwner == null || pOObject == null)
				return;

			pOOwner.Contents?.Remove(pOObject);

			int liIndex = pOMasterlist.IndexOf(pOObject);

			if (liIndex <= 0)
				return;

			if (pOOwner.Quantity == liIndex)
			{
				pOOwner.Quantity = pOObject.Link;
				pOObject.Link = 0;
				return;
			}

			int liAt = pOOwner.Quantity;

			for (int liStep = 0; liStep < pOMasterlist.Count && liAt > 0 && liAt < pOMasterlist.Count; liStep++)
			{
				UWObject lOAt = pOMasterlist[liAt];

				if (lOAt == null)
					return;

				if (lOAt.Link == liIndex)
				{
					lOAt.Link = pOObject.Link;
					pOObject.Link = 0;
					return;
				}

				liAt = lOAt.Link;
			}
		}
	}
}
