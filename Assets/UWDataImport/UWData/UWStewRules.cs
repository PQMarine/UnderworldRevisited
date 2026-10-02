namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ROTWORM STEW (RotwormStew_ovr099_1B2, read whole 2026-09-28 on the user's find that
	/// the dead rotworm would not go into a bowl, "needed for rotworm stew too"). It is made by
	/// USING THE RECIPE - a readable that is not enchanted, carries no map piece (word 0 bit 10)
	/// and whose text link is 0x100 or higher (UseReadable in seg040 hands those to this routine
	/// instead of showing a text; LOOKING at it still reads the text, per user on the original).
	/// UW1 places one such scroll: 0x13E with link 0x301, carried by the goblin on level 1,
	/// 4/56 (scanned 2026-09-28).
	///
	///   - the first bowl (0x8E) in the inventory, else "You need a bowl to mix the ingredients.";
	///   - every object in the bowl must be one of the three of the recipe in UW.EXE (dseg E28:
	///     the dead rotworm 0xD9, the mushroom 0xB8, the flask of port 0xBE), and each of the
	///     three must be there at least once - more of one is fine; else "The bowl does not
	///     contain the correct ingredients." (an empty bowl too);
	///   - then the bowl is closed if it is the open container, its contents freed, and the bowl
	///     itself becomes the stew (0x11B, the rest of word 0 kept): "You mix the ingredients
	///     into a stew."
	///
	/// THE CARRIED WEIGHT: the contents are freed without the inventory removal and the bowl
	/// changes its id in place, so the original's running total keeps what it had
	/// (UWInventoryModel.WeightOffsetTenthStones).
	/// </summary>
	public static class UWStewRules
	{
		public const int BowlObjectId = 0x8E;

		public const int RotwormStewObjectId = 0x11B;

		private static readonly int[] msRecipe = { 0xD9, 0xB8, 0xBE };

		/// <summary>The text link from which on a readable is the recipe.</summary>
		private const int RecipeLinkFirst = 0x100;

		/// <summary>Word 0 bit 10 - a map piece, which plays its cutscene instead.</summary>
		private const int MapPieceFlag = 0x2;

		/// <summary>Block 1 lines, our numbering (the original's plus one).</summary>
		public const int WrongIngredientsMessage = 149;

		public const int MixedMessage = 150;

		public const int NoBowlMessage = 151;

		/// <summary>Is this readable the recipe (see the class comment)?</summary>
		public static bool IsRecipe(UWObject pOItem)
		{
			return pOItem != null
				&& pOItem.GetCategory() == UWObject.ObjectCategoryEnum.BooksAndScrolls
				&& !pOItem.IsEnchanted
				&& (pOItem.Flags & MapPieceFlag) == 0
				&& (pOItem.Quantity & 0x1FF) >= RecipeLinkFirst;
		}

		/// <summary>Mixes the stew if the bowl holds the right things. Returns the block 1 line to
		/// print.</summary>
		public static int TryMix(UWInventoryModel pOInventory, DataImport pOData)
		{
			UWCommonObjectProperties lOProperties = pOData != null ? pOData.CommonObjectProperties : null;

			UWObject lOBowl = null;

			if (pOInventory != null)
			{
				foreach (UWObject lOItem in pOInventory.EnumerateAll())
				{
					if (lOItem != null && lOItem.ID == BowlObjectId)
					{
						lOBowl = lOItem;
						break;
					}
				}
			}

			if (lOBowl == null)
				return NoBowlMessage;

			int[] liFound = new int[msRecipe.Length];

			if (lOBowl.Contents != null)
			{
				foreach (UWObject lOIngredient in lOBowl.Contents)
				{
					int liAt = lOIngredient != null ? System.Array.IndexOf(msRecipe, (int)lOIngredient.ID) : -1;

					if (liAt < 0)
						return WrongIngredientsMessage;

					liFound[liAt]++;
				}
			}

			foreach (int liCount in liFound)
			{
				if (liCount == 0)
					return WrongIngredientsMessage;
			}

			if (pOInventory.OpenContainer == lOBowl)
				pOInventory.CloseContainer();

			int liBefore = lOProperties != null ? UWInventoryWeight.GetItemTenthStones(lOBowl, lOProperties) : 0;

			lOBowl.Contents.Clear();
			// Number, icon and texture together - the stew kept the bowl's picture until then (per
			// user, 2026-09-28).
			UWObjectMechanics.SetObjectId(lOBowl, RotwormStewObjectId, pOData != null ? pOData.Textures : null);

			int liAfter = lOProperties != null ? UWInventoryWeight.GetItemTenthStones(lOBowl, lOProperties) : 0;

			pOInventory.KeepWeightChange(liBefore, liAfter);
			pOInventory.NotifyChanged();

			return MixedMessage;
		}
	}
}
