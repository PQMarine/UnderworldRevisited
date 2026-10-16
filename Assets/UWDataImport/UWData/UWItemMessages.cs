namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SENTENCES BUILT AROUND AN ITEM NAME. UW.EXE assembles these from pieces instead of
	/// taking a finished string from STRINGS.PAK: "The ", the bare name, " is locked." (UseCont,
	/// UnderworldExhumed USEITEMS.C), " is empty." (DumpTheBag), " is too full." (INVPANEL.C,
	/// the bag that refuses a thing). Until 2026-10-11 the port spelled them at eight places
	/// in UWItemDrag, UWModernBags and Interaction, with two name fallbacks ("item" and empty,
	/// which printed "The  is too full."); here each is one function, and a missing name is
	/// "item" everywhere.
	/// </summary>
	public static class UWItemMessages
	{
		/// <summary>What stands in for a name the strings do not have. The original writes
		/// "UNNAMED" for a container (UseCont); ours keeps the word the refusal messages used.</summary>
		/// <summary>What stands in for a name the strings do not have.</summary>
		public const string UnnamedItem = "item";

		/// <summary>
		/// "The pole cannot be used on that." for any item. The original has the sentence only
		/// for the pole (block 1, 0x9E in its numbering, UsePoleOn) and answers any other failed
		/// use with message 0x84; the port says it with the item's own name.
		/// </summary>
		public static string CannotBeUsedOnThat(DataImport pOData, int piObjectId)
		{
			return "The " + fName(pOData, piObjectId) + " cannot be used on that.";
		}

		/// <summary>"The map case is too full." (per user on the original, 2026-09-18).</summary>
		public static string IsTooFull(DataImport pOData, int piObjectId)
		{
			return "The " + fName(pOData, piObjectId) + " is too full.";
		}

		/// <summary>"The chest is locked." - with the full stop of the original's UseCont
		/// (" is locked.\n"); until 2026-10-11 ours left it out.</summary>
		public static string IsLocked(DataImport pOData, int piObjectId)
		{
			return "The " + fName(pOData, piObjectId) + " is locked.";
		}

		/// <summary>"The barrel is empty." - only when nothing came out and the player asked
		/// (DumpTheBag with to_player; per user, 2026-09-16).</summary>
		public static string IsEmpty(DataImport pOData, int piObjectId)
		{
			return "The " + fName(pOData, piObjectId) + " is empty.";
		}

		/// <summary>
		/// Why a container refused an item (UWContainerCapacity): the rune bag's own message,
		/// "That item does not fit.", or "The map case is too full." with the container's name.
		/// Null when it fit, or when a too-full result comes without the container.
		/// </summary>
		public static string ContainerRejection(UWContainerCapacity.ResultEnum peResult, UWObject pOContainer,
			DataImport pOData)
		{
			switch (peResult)
			{
				case UWContainerCapacity.ResultEnum.RunesOnly:
					return UWText.General(pOData, UWStringIds.RunesOnlyMessage);

				case UWContainerCapacity.ResultEnum.WrongType:
					return UWText.General(pOData, UWStringIds.DoesNotFitMessage);

				case UWContainerCapacity.ResultEnum.TooHeavy:
					return pOContainer != null ? IsTooFull(pOData, pOContainer.ID) : null;

				default:
					return null;
			}
		}

		private static string fName(DataImport pOData, int piObjectId)
		{
			return UWText.ObjectName(pOData, piObjectId, false, false, UnnamedItem);
		}
	}
}
