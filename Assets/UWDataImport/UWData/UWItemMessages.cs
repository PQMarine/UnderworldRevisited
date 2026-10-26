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
	///
	/// SINCE TRANSLATION STEP 3A (2026-10-26) the frames are UWText ids (msg.item.*,
	/// UWTextEnglish.Messages) and the name is the grammar's (UWText.TheObjectName: English the bare
	/// name, German with the definite article in the frame's case), the line written as the language
	/// writes a sentence (UWText.Sentence) - English byte for byte as before (the look catalog's
	/// message family, Pins/look-strings.txt). The read header, the taste, the missing ammunition,
	/// "Use ... on what?" and the theft line came here from the Unity side and UWItemUse the same day.
	/// </summary>
	public static class UWItemMessages
	{
		/// <summary>What stands in for a name the strings do not have. The original writes
		/// "UNNAMED" for a container (UseCont); ours keeps the word the refusal messages used.</summary>
		public static string UnnamedItem => UWText.T("msg.item.unnamed");

		/// <summary>
		/// "The pole cannot be used on that." for any item. The original has the sentence only
		/// for the pole (block 1, 0x9E in its numbering, UsePoleOn) and answers any other failed
		/// use with message 0x84; the port says it with the item's own name.
		/// </summary>
		public static string CannotBeUsedOnThat(DataImport pOData, int piObjectId)
		{
			return UWText.Sentence(pOData, "msg.item.cannot_use_on", fName(pOData, piObjectId));
		}

		/// <summary>"The map case is too full." (per user on the original, 2026-09-18).</summary>
		public static string IsTooFull(DataImport pOData, int piObjectId)
		{
			return UWText.Sentence(pOData, "msg.item.too_full", fName(pOData, piObjectId));
		}

		/// <summary>"The chest is locked." - with the full stop of the original's UseCont
		/// (" is locked.\n"); until 2026-10-11 ours left it out.</summary>
		public static string IsLocked(DataImport pOData, int piObjectId)
		{
			return UWText.Sentence(pOData, "msg.item.locked", fName(pOData, piObjectId));
		}

		/// <summary>"The barrel is empty." - only when nothing came out and the player asked
		/// (DumpTheBag with to_player; per user, 2026-09-16).</summary>
		public static string IsEmpty(DataImport pOData, int piObjectId)
		{
			return UWText.Sentence(pOData, "msg.item.empty", fName(pOData, piObjectId));
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

		// ------------------------------------------------- The other sentences around a name
		// (translation step 3A, 2026-10-26: moved here from the Unity side and from UWItemUse, so
		// that the look catalog records them engine-free - UWLookCatalog's message family)

		/// <summary>"Use torch on what?" - the original's sentence, assembled by UW.EXE from pieces
		/// (UWClassicUseMode); the name in the accusative.</summary>
		public static string UseOnWhat(DataImport pOData, int piObjectId)
		{
			return UWText.Sentence(pOData, "prompt.use_on_what",
				UWText.TheObject(pOData, piObjectId, UWGrammarCase.Accusative, UnnamedItem));
		}

		/// <summary>
		/// The line before a book's or scroll's text: "You read the " + the item's name + "...", for
		/// books and scrolls alike (UseReadable_seg040_352B_19A8 and Read_ovr122_586, which put the
		/// item name between two strings of UW.EXE; "UNNAMED" when it has none). CONFIRMED per user
		/// on the original 2026-09-25 (UWLookService.TryReadBook). The name in the accusative.
		/// </summary>
		public static string ReadHeader(DataImport pOData, int piObjectId)
		{
			return UWText.Sentence(pOData, "msg.item.read",
				UWText.TheObject(pOData, piObjectId, UWGrammarCase.Accusative, UWText.T("msg.item.read_unnamed")));
		}

		/// <summary>"That apple tasted great." - "That " (UW.EXE), the bare name (nominative) and one
		/// of the five taste words of block 1 from FirstTasteMessage (UWItemUse, food without a
		/// message of its own; piStep 0 to 4).</summary>
		public static string Taste(DataImport pOData, int piObjectId, int piStep)
		{
			return UWText.Sentence(pOData, "msg.item.taste", UWText.TheObject(pOData, piObjectId, UWGrammarCase.Nominative),
				UWText.Fragment(UWText.General(pOData, FirstTasteMessage + piStep)));
		}

		/// <summary>" tasted putrid." (block 1); the next four up to " tasted great.".</summary>
		public const int FirstTasteMessage = 173;

		/// <summary>
		/// "Sorry, you have no arrows." - a launcher whose ammunition is missing (UWPlayerMissiles):
		/// the plural the original makes by adding an "s" to the bare name instead of taking the
		/// plural of the string block (reference: GetSimpleObjectNameUW plus "s"; for the three
		/// ammunition types the result is the same) - UWText.ObjectNameAddedPlural, the language's
		/// plural in a language with a grammar of its own.
		///
		/// IT IS NOT IN STRINGS.PAK, BUT IN UW.EXE. The reference names string block 1, number 207
		/// for it - for us that holds "You flip the switch.", and the whole block has no such
		/// sentence. The game binary has the wording (2026-09-10): "Sorry, you have no " is stored as
		/// raw text in the executable, the item name is appended at runtime. The user checked both
		/// in the original: "Sorry, you have no arrows." with the bow, "Sorry, you have no sling
		/// stones." with the sling. The full stop at the end comes from there.
		/// </summary>
		public static string NoAmmunition(DataImport pOData, int piAmmunitionId)
		{
			return UWText.Sentence(pOData, "msg.item.no_ammunition", UWText.ObjectAddedPlural(pOData, piAmmunitionId));
		}

		/// <summary>"a goblin is angered by your action." - the creature's name with its article
		/// (nominative) and the theft line of its attitude (UWTriggerSystem, a theft that angers the
		/// owners: UWCritterRules.TheftMessageBase plus the attitude; per user 2026-09-16). English
		/// keeps the original's small first letter.</summary>
		public static string Angered(DataImport pOData, int piCreatureId, int piAttitude)
		{
			return UWText.Sentence(pOData, "msg.item.angered", UWText.ObjectPhrase(pOData, piCreatureId, false, true),
				UWText.Fragment(UWText.General(pOData, UWCritterRules.TheftMessageBase + piAttitude)));
		}

		/// <summary>A container's or a tool's name in the nominative, "item" when it has none.</summary>
		private static UWNamePhrase fName(DataImport pOData, int piObjectId)
		{
			return UWText.TheObject(pOData, piObjectId, UWGrammarCase.Nominative, UnnamedItem);
		}
	}
}
