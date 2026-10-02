namespace UWDataImport.UWData
{
	/// <summary>
	/// Whether a container takes an item: the TYPE test (rune bag, quiver, map case, bowl) and
	/// the WEIGHT test against the capacity from OBJECTS.DAT
	/// (UWObjectClassProperties.Container).
	///
	/// Until 2026-09-18 neither test existed - every container took everything in any amount
	/// (per user: "Fuellstand von Behaeltern pruefen wir garnicht"). The visible eight
	/// places are only the window (see UWInventoryModel.ContainerSlotCount), never the limit;
	/// the limit is the weight.
	/// </summary>
	public static class UWContainerCapacity
	{
		public enum ResultEnum
		{
			Fits,

			/// <summary>Wrong type for a restricted container.</summary>
			WrongType,

			/// <summary>Wrong type, and the container is the rune bag - it has its own
			/// message in the original.</summary>
			RunesOnly,

			/// <summary>Right type, but the capacity would be exceeded.</summary>
			TooHeavy
		}

		/// <summary>Type class of the mask minus UWObjectClassProperties.ContainerMaskFirstClass.</summary>
		private const int ClassRunes = 0;

		private const int ClassAmmunition = 1;

		private const int ClassScrolls = 2;

		private const int ClassEdibles = 3;

		/// <summary>The rune bag, for its own message.</summary>
		private const int RuneBagObjectId = 143;

		/// <param name="pOReplaced">An item that leaves the container in the same move (swapping
		/// places) - its weight does not count against the capacity.</param>
		public static ResultEnum Check(UWObject pOContainer, UWObject pOItem, DataImport pOData,
			UWObject pOReplaced = null)
		{
			if (pOContainer == null || pOItem == null || pOData == null)
				return ResultEnum.Fits;

			UWObjectClassProperties.Container lOEntry;

			// Unknown container (data not loaded, or an id outside the table): let it through
			// rather than block the player.
			if (pOData.ObjectClassProperties == null
				|| !pOData.ObjectClassProperties.TryGetContainer(pOContainer.ID, out lOEntry))
				return ResultEnum.Fits;

			// WEIGHT FIRST: a sack too heavy for the map case gives "The map case is too
			// full.", not the type message (per user on the original, 2026-09-18).
			if (!fFitsWeight(pOContainer, pOItem, pOReplaced, lOEntry.CapacityTenthStones,
				pOData.CommonObjectProperties))
				return ResultEnum.TooHeavy;

			if (!fMatchesMask(lOEntry.ObjectMask, pOItem.ID))
			{
				return pOContainer.ID == RuneBagObjectId ? ResultEnum.RunesOnly : ResultEnum.WrongType;
			}

			return ResultEnum.Fits;
		}

		/// <summary>Short form for callers that only want a yes or no.</summary>
		public static bool Fits(UWObject pOContainer, UWObject pOItem, DataImport pOData)
		{
			return Check(pOContainer, pOItem, pOData) == ResultEnum.Fits;
		}

		/// <summary>
		/// The type classes behind the mask, AS THE ORIGINAL TESTS THEM (the inventory drop routine
		/// of overlay 121 that also feeds the paperdoll, labels 1003 to 10CD, read 2026-09-28 on the
		/// user's find that the dead rotworm would not go into a bowl). The object classes are the usual uw division of the object id: major =
		/// id >> 6, minor = (id >> 4) &amp; 3, index = id &amp; 0xF.
		///
		///   0x200 runes         major 3 and minor 3, or minor 2 with index above 7 (0xE8-0xFF)
		///   0x201 ammunition    major 0, minor 1, index below 3 - sling stone, bolt and arrow;
		///                       not the plain stone 0x13 (ours took it until then)
		///   0x202 scrolls       major 4, minor 3, index 8 and up - the scrolls 0x138-0x13F, not
		///                       the books 0x130-0x137 (ours took the books until then)
		///   0x203 edibles       major 2, minor 3 (0xB0-0xBF), and six more by id: the candle
		///                       0x92, the plants 0xCE and 0xCF, the leeches 0x125, the rotworm
		///                       stew 0x11B and the dead rotworm 0xD9 (ours had none of the six)
		///   anything higher     fits nothing (ours had a key class 0x204, which no uw1
		///                       container carries)
		/// </summary>
		private static bool fMatchesMask(int piMask, int piObjectId)
		{
			if (piMask == UWObjectClassProperties.ContainerMaskAll)
				return true;

			// Below the class range the mask would be a single object id. No uw1 container
			// uses that, but the original would compare it directly.
			if (piMask < UWObjectClassProperties.ContainerMaskFirstClass)
				return piObjectId == piMask;

			int liMajor = piObjectId >> 6;
			int liMinor = (piObjectId >> 4) & 0x3;
			int liIndex = piObjectId & 0xF;

			switch (piMask - UWObjectClassProperties.ContainerMaskFirstClass)
			{
			case ClassRunes:
				return liMajor == 3 && (liMinor == 3 || (liMinor == 2 && liIndex > 7));

			case ClassAmmunition:
				return liMajor == 0 && liMinor == 1 && liIndex < 3;

			case ClassScrolls:
				return liMajor == 4 && liMinor == 3 && liIndex >= 8;

			case ClassEdibles:
				return (liMajor == 2 && liMinor == 3) || System.Array.IndexOf(msBowlExtras, piObjectId) >= 0;
			}

			return false;
		}

		/// <summary>What else goes into a bowl besides the food class - see fMatchesMask.</summary>
		private static readonly int[] msBowlExtras = { 0xCE, 0xCF, 0x92, 0x125, 0x11B, 0xD9 };

		/// <summary>
		/// The capacity counts the contents WITHOUT the container itself, and the item to be
		/// added with everything inside it. The test is "over the capacity". CAPACITY 0 MEANS NO
		/// LIMIT (the same routine as the masks: a zero capacity skips the comparison, read
		/// 2026-09-28) - quiver and rune bag carry 0. Ours took it for a limit of nothing until
		/// then, so every arrow that weighs a tenth was "too heavy" for the quiver.
		/// </summary>
		private static bool fFitsWeight(UWObject pOContainer, UWObject pOItem, UWObject pOReplaced,
			int piCapacityTenthStones, UWCommonObjectProperties pOProperties)
		{
			if (pOProperties == null || piCapacityTenthStones == 0)
				return true;

			int liInside = pOContainer.Contents != null
				? UWInventoryWeight.GetTotalTenthStones(pOContainer.Contents, pOProperties) : 0;

			if (pOReplaced != null)
				liInside -= UWInventoryWeight.GetItemTenthStones(pOReplaced, pOProperties);

			int liAdded = UWInventoryWeight.GetItemTenthStones(pOItem, pOProperties);

			return liInside + liAdded <= piCapacityTenthStones;
		}
	}
}
