using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ONE walk along an object chain (cleanup phase 1, 2026-10-11). Objects hang together
	/// through their "next" link (UWObject.Link, word +4 bits 6-15): the objects of a tile, the
	/// contents of a container, the triggers and traps behind a switch, the inventory behind
	/// the player object. The chain starts at an index - a tile's first object, or a container's
	/// "sp_link" (UWObject.Quantity when HasQuantity is clear) - and ends at link 0.
	///
	/// Before this class seven loops walked the same links, each with its own guard against a
	/// damaged file whose links run in a circle: a HashSet, a fixed 1024, the record count.
	/// Here every walk stops at a null record, at a link of 0 or beyond the list, after
	/// piMaxSteps objects and, unless the caller says otherwise, at the first object seen twice.
	/// A revisit cannot be hidden from a caller that only SEARCHES the chain (the result is the
	/// same object or none); a caller that COLLECTS passes pbStopOnRevisit false where it used
	/// to collect the circle up to its limit, so its list stays what it was.
	/// </summary>
	public static class UWObjectChain
	{
		/// <summary>
		/// The objects from record piFirst along the next links. Records at or beyond piCount
		/// are out of bounds (piCount is at most the list's count - the save game may declare
		/// fewer inventory records than the list holds). Yields nothing for piFirst 0.
		/// </summary>
		public static IEnumerable<UWObject> WalkFrom(int piFirst, IList<UWObject> pORecords, int piCount,
			int piMaxSteps, bool pbStopOnRevisit = true)
		{
			if (pORecords == null)
				yield break;

			if (piCount > pORecords.Count)
				piCount = pORecords.Count;

			HashSet<UWObject> lOSeen = pbStopOnRevisit ? new HashSet<UWObject>() : null;
			int liAt = piFirst;

			for (int liStep = 0; liStep < piMaxSteps && liAt > 0 && liAt < piCount; liStep++)
			{
				UWObject lOAt = pORecords[liAt];

				if (lOAt == null)
					yield break;

				if (lOSeen != null && !lOSeen.Add(lOAt))
					yield break;

				yield return lOAt;

				liAt = lOAt.Link;
			}
		}

		/// <summary>
		/// The chain behind an object's special link - a container's contents, a switch's
		/// triggers, a wand's spell. The CALLER has checked that the quantity field is a link
		/// (HasQuantity clear): with the quantity bit set the field is a count or a spell id,
		/// never a record index.
		/// </summary>
		public static IEnumerable<UWObject> Walk(UWObject pOOwner, IList<UWObject> pORecords, int piMaxSteps,
			bool pbStopOnRevisit = true)
		{
			if (pOOwner == null || pORecords == null)
				return System.Array.Empty<UWObject>();

			return WalkFrom(pOOwner.Quantity, pORecords, pORecords.Count, piMaxSteps, pbStopOnRevisit);
		}
	}
}
