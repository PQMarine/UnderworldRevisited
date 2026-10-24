namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STRING INDICES MORE THAN ONE SYSTEM PRINTS, in one engine-free place (cleanup phase 1,
	/// 2026-10-11). Until then each of these was declared in two or three files - the carrying
	/// refusal in the classic drag, the modern bags and the interaction alike.
	///
	/// NUMBERING: block 1 is the general message block, block 2 the character words (classes,
	/// skills, attributes); the block 1 indices are in OUR numbering, one above UW.EXE's (see
	/// UWStatusReport). A rule that prints a message only it knows keeps its own private
	/// constant.
	/// </summary>
	public static class UWStringIds
	{
		// ------------------------------------------------- Block 1, general messages

		/// <summary>First of the eight directions from "to the North" on, in the order
		/// GetCardinal_ovr154_7C numbers them - the shrine's direction to the cup and Detect
		/// Monsters use the same eight.</summary>
		public const int FirstDirectionMessage = 37;

		/// <summary>"That is too heavy for you to pick up." - the carrying limit when taking
		/// from the world.</summary>
		public const int TooHeavyToPickUpMessage = 96;

		/// <summary>"It seems to have no effect." (UW.EXE 0x84) - an item used on something it
		/// does nothing to, bones on anything but the grave.</summary>
		public const int NoEffectMessage = 133;

		/// <summary>"No save game there." - a restore from an empty slot, in the classic options
		/// (UWHudOptions) and the modern game menu (UWModernGameMenu) alike; until 2026-10-19 the modern
		/// menu read it from the classic class. The reference gives 161 for uw1, checked against the
		/// printout of the block.</summary>
		public const int NoSaveGameThereMessage = 162;

		/// <summary>"Save Game Failed." (red, colour code 4) - both schemes' save, as
		/// NoSaveGameThereMessage.</summary>
		public const int SaveGameFailedMessage = 165;

		/// <summary>"Save Game Succeeded." (green, colour code 6) - both schemes' save, as
		/// NoSaveGameThereMessage.</summary>
		public const int SaveGameSucceededMessage = 166;

		/// <summary>"You cannot use oil on that." - UseOilOn's refusal (USEITEMS.C, UW.EXE 0xB1).</summary>
		public const int CannotUseOilMessage = 178;

		/// <summary>"You cannot barter a container.  Instead, remove the contents you want to
		/// trade."</summary>
		public const int CannotBarterContainerMessage = 187;

		/// <summary>"The water refreshes you." (UW.EXE 0xED) - drinking water, and every fountain
		/// that is not of the healing class.</summary>
		public const int WaterRefreshesMessage = 238;

		/// <summary>"You can only put runes in the rune bag." - a container refusing an item.</summary>
		public const int RunesOnlyMessage = 248;

		/// <summary>"That item does not fit." - a container refusing an item.</summary>
		public const int DoesNotFitMessage = 249;

		/// <summary>"That is too heavy to take." (UW.EXE 0xFC) - the carrying limit when taking
		/// from a trade.</summary>
		public const int TooHeavyToTakeMessage = 253;

		/// <summary>"There is no place to put that." (UW.EXE 0x102) - the original's one sentence
		/// for a pack without room: ItemFitsSlot (inv/INVPANEL.C) prints it when no backpack slot
		/// is free. Since cleanup step 2.3 (2026-10-12) every "no room" of ours says it: the
		/// classic drop on the container icon, the modern pickup into the pack, taking off a worn
		/// thing and closing the bags with something on the pointer - until then the last three
		/// printed English sentences of their own ("Your pack and bags are full.", "There is no
		/// room for it in your pack.").</summary>
		public const int NoPlaceToPutMessage = 259;

		/// <summary>"The spell has no discernable effect." (UW.EXE 0x10F, printed by
		/// DoorAndTrapSpells_seg038_3307_1560 when a class 11 target spell does not take
		/// effect) - a target spell that hits nothing, a lock spell on a lock it cannot work.</summary>
		public const int SpellNoEffectMessage = 272;

		// ------------------------------------------------- Block 2, character words

		/// <summary>The class names begin here (0x18), one per character class - for the
		/// character panel, the modern stats page, the help window and character creation.</summary>
		public const int ClassNameBase = 24;

		/// <summary>The skill names begin here, one per skill number - for the stats page, the
		/// shrine and character creation alike (declared three times until 2026-09-18).</summary>
		public const int SkillNameBase = 32;
	}
}
