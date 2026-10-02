namespace UWDataImport.UWData
{
	/// <summary>
	/// What using an item (UWItemUse) needs from the game: taking the used piece out of
	/// wherever it was dragged from, messages, the view flash, a cutscene, casting the spell
	/// of a wand, sleeping, planting, playing, fishing and the carried light. The Unity side
	/// is UWItemDrag, which knows where the item came from.
	/// </summary>
	public interface IUWItemUseHost
	{
		/// <summary>Removes the consumed piece from where it was taken - for a stack only one
		/// of them.</summary>
		void ConsumeUsedItem();

		/// <summary>A line of string block 1, our numbering.</summary>
		void AddGeneralMessage(int piIndex);

		/// <summary>A composed line for the message scroll - "That fish tasted great.".</summary>
		void AddMessage(string psMessage);

		/// <summary>The class 9 curse on the player, with the flash (see IUWTrapHost.CursePlayer).
		/// </summary>
		void CursePlayer(int piDice);

		/// <summary>A brief flash of the view window in a palette colour.</summary>
		void FlashWindow(int piPaletteColour, float pfSeconds);

		void PlayCutscene(int piCutscene);

		/// <summary>Casts the spell of an item without the rules of rune casting. False if the
		/// spell does not exist.</summary>
		bool CastSpellFromObject(int piSpellIndex);

		/// <summary>The same for a spell only items carry, given by its classes (see
		/// UWItemSpellRules).</summary>
		bool CastSpellByClass(int piMajorClass, int piMinorClass);

		/// <summary>A wand is briefly dead after a use - the cooldown applies globally.</summary>
		bool IsWandCoolingDown { get; }

		void StartWandCooldown();

		/// <summary>The sound of a magic item refused on cooldown (UW.EXE plays 0x15).</summary>
		void PlayMagicItemRefused();

		/// <summary>The bedroll: sleep, with everything it brings. False if sleeping is not
		/// possible right now.</summary>
		bool TrySleep();

		/// <summary>Passed out from drink: sleep without a bedroll and without a dream.</summary>
		void SleepPassedOut();

		/// <summary>The silver seed is planted ahead; true when it is used up.</summary>
		bool TryPlantSeed();

		bool IsInstrument(int piObjectId);

		bool PlayInstrument(UWObject pOItem);

		/// <summary>The fishing pole fishes in the water ahead.</summary>
		bool TryFish();
	}
}
