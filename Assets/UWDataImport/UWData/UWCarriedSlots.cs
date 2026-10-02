using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE NUMBERS OF THE CARRIED OBJECTS (read 2026-09-29, per user's decision: "rebuild the slot
	/// allocation"). In the original the inventory lives in the current level's static slots, and
	/// some rules seed with an object's number - the barter's appraisal of every item
	/// (ovr095_16B1). Ours kept the inventory outside the level's list, so a carried item had no
	/// number and its appraisal came out random (per user: the Gray Goblin took an offer in ours
	/// that the original refused).
	///
	///   RestoreGame_ovr140_44B -> ovr118_639 -> ovr118_573: the player object's chain goes into
	///     the level by ovr118_3CB - each object takes GetFreeObject(static), the top of the
	///     level's static free list, then its contents (a link in word 3 without the quantity bit)
	///     the same way, then the next in the chain. The object in the hand comes last.
	///   Leaving a level (ovr140_92, ovr140_12D, both through the same restore afterwards) frees
	///     them first by ClearObjectList_ovr118_4BE: an object's contents, then the rest of its
	///     chain, then the object itself goes back onto the free list.
	///   Splitting a stack ("Move how many?", ovr121_A14): the part taken KEEPS the number, the
	///     rest left behind is a new object from GetFreeObject.
	///
	/// NOT FOLLOWED: objects used up, destroyed or created in the inventory otherwise (they would
	/// free or take a slot in the original), and the place in the chain an item picked up during
	/// play gets - on a level change ours takes the chain in the order the save game writer uses.
	/// Right after loading the numbers are exact; the longer the play, the more they can drift.
	/// </summary>
	public static class UWCarriedSlots
	{
		/// <summary>The level the carried objects live in - set by the level loader.</summary>
		public static UWLevel Level { get; set; }

		/// <summary>An object's number for the rules that seed with it: its carried number, or
		/// its place in the level's object list, or 0.</summary>
		public static int NumberOf(UWObject pOObject)
		{
			if (pOObject == null)
				return 0;

			if (pOObject.SlotIndex > 0)
				return pOObject.SlotIndex;

			int liIndex = Level != null && Level.Masterlist != null ? Level.Masterlist.IndexOf(pOObject) : -1;

			return liIndex > 0 ? liIndex : 0;
		}

		/// <summary>ovr118_3CB: the chain into the level, each object before its contents.</summary>
		public static void TakeSlots(UWLevel pOLevel, IList<UWObject> pOChain)
		{
			if (pOLevel == null || pOChain == null)
				return;

			foreach (UWObject lOObject in pOChain)
			{
				if (lOObject == null)
					continue;

				lOObject.SlotIndex = pOLevel.TakeStaticSlot();

				if (fHasContents(lOObject))
					TakeSlots(pOLevel, lOObject.Contents);
			}
		}

		/// <summary>ClearObjectList_ovr118_4BE: the chain out of the level - contents first, then
		/// the rest of the chain, the object itself last.</summary>
		public static void ReturnSlots(UWLevel pOLevel, IList<UWObject> pOChain)
		{
			if (pOLevel == null || pOChain == null)
				return;

			fReturnFrom(pOLevel, pOChain, 0);
		}

		private static void fReturnFrom(UWLevel pOLevel, IList<UWObject> pOChain, int piAt)
		{
			if (piAt >= pOChain.Count)
				return;

			UWObject lOObject = pOChain[piAt];

			if (lOObject != null && fHasContents(lOObject))
				fReturnFrom(pOLevel, lOObject.Contents, 0);

			fReturnFrom(pOLevel, pOChain, piAt + 1);

			if (lOObject != null && lOObject.SlotIndex > 0)
			{
				pOLevel.ReturnStaticSlot(lOObject.SlotIndex);
				lOObject.SlotIndex = 0;
			}
		}

		/// <summary>ovr121_A14: pOTaken leaves pOSource with part of the stack. The taken part
		/// keeps the stack's number, what stays behind gets a new one.</summary>
		public static void OnSplit(UWObject pOSource, UWObject pOTaken)
		{
			if (pOSource == null || pOTaken == null)
				return;

			pOTaken.SlotIndex = NumberOf(pOSource);
			pOSource.SlotIndex = Level != null ? Level.TakeStaticSlot() : 0;
		}

		/// <summary>A carried stack joins another: the one that disappears frees its slot.</summary>
		public static void OnMerged(UWObject pOGone)
		{
			if (pOGone == null || pOGone.SlotIndex <= 0)
				return;

			if (Level != null)
				Level.ReturnStaticSlot(pOGone.SlotIndex);

			pOGone.SlotIndex = 0;
		}

		/// <summary>The recursion of ovr118_3CB and ClearObjectList: no quantity bit and a link in
		/// word 3 - a container's contents, a wand's spell object.</summary>
		private static bool fHasContents(UWObject pOObject)
		{
			return !pOObject.HasQuantity && pOObject.Contents != null && pOObject.Contents.Count > 0;
		}
	}
}
