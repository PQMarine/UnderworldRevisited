namespace UWDataImport.UWData
{
	/// <summary>
	/// WHETHER A THING CAN BE TAKEN, and the message when it cannot - the one rule behind taking
	/// something from the world (the classic drag, the modern E, the world menu's Pick up, the
	/// free pointer's drag) and taking a bought good from the trade (cleanup step 2.3,
	/// 2026-10-12; until then Interaction.TakeOntoPointer, Interaction.fTryPickUp and
	/// UWItemDrag.fTakeFromSource each had the checks, and the pack-full refusal was an English
	/// literal). What happens once the answer is yes - the theft, the moonstone, the removal
	/// from the world - is UWWorldPickup on the Unity side.
	///
	/// THE ORIGINAL (ui/INTERACT.C player_3dget, UnderworldExhumed, read 2026-10-12): an object
	/// is "releaseable" when its COMOBJ entry says pickable and it is no mobile object (a
	/// creature); it must be in reach (InPickRange) - out of reach "That is too far away to
	/// take." -; a stack asks "Move how many?" (AskHowMany) and is CUT to that count; then
	/// EncumCheck weighs the cut part against the free capacity - "That is too heavy for you to
	/// pick up." (0x5F) and the cut is undone -; only then the moonstone field, the owners
	/// (player_grabbed) and the removal from the tile (release_3d). Taking from the trade
	/// (conv/BARTER.C UseTradeSlot_ovr097_6E8) is the same AskHowMany and EncumCheck with its
	/// own sentence, "That is too heavy to take." (0xFC).
	///
	/// DECISION 14 OF THE CLEANUP PLAN, decided from the source above: THE WEIGHT CHECK IS
	/// COUNT-AWARE. AskHowMany cuts the stack down to the count before EncumCheck sees it, so
	/// the original weighs exactly what goes onto the pointer (inv/INVDATA.C ItemWeight: count
	/// times mass for a stack). The classic drag's check of the chosen count is therefore the
	/// original's; the modern scheme takes a stack whole and weighs it whole, which is the same
	/// rule for what it takes. Nothing changed by the decision.
	/// </summary>
	public static class UWPickupRules
	{
		/// <summary>What the check came to.</summary>
		public enum Outcome
		{
			/// <summary>It can be taken.</summary>
			Take,

			/// <summary>A creature, or nothing at all - refused without a word (the original
			/// never calls a creature releaseable).</summary>
			NotPickable,

			/// <summary>Beyond the hand's reach (UWReachRules).</summary>
			TooFar,

			/// <summary>Over the carrying capacity.</summary>
			TooHeavy
		}

		/// <summary>The answer and the general message (block 1, our numbering) to print, -1 for
		/// none.</summary>
		public readonly struct Result
		{
			public readonly Outcome Outcome;

			public readonly int MessageIndex;

			public Result(Outcome peOutcome, int piMessageIndex)
			{
				Outcome = peOutcome;
				MessageIndex = piMessageIndex;
			}

			public bool CanTake => Outcome == Outcome.Take;
		}

		/// <summary>
		/// Can piCount pieces of a thing in the world be taken? In the original's order: the
		/// creature test, the reach (pbReachOk, measured by the caller on the tile grid - see
		/// UWReachRules.CanReach; the classic drag measures it before the count prompt, as
		/// player_3dget does), the weight of what is taken (piCount pieces of a stack, 0 or the
		/// whole count for all of it; a container with its contents).
		/// </summary>
		/// piReachRefusal: what a failed reach says - out of range "That is too far away to take.",
		/// blocked by the terrain between "You cannot reach that." (player_3dget, inrange + 0x5D).
		public static Result Check(UWObject pOItem, int piCount, bool pbReachOk, UWInventoryModel pOInventory,
			int piReachRefusal = UWReachRules.TooFarToTakeMessage)
		{
			if (pOItem == null || UWObjectIds.IsCreature(pOItem.ID))
				return new Result(Outcome.NotPickable, -1);

			if (!pbReachOk)
				return new Result(Outcome.TooFar, piReachRefusal);

			if (pOInventory != null && !pOInventory.CanCarry(pOItem, piCount))
				return new Result(Outcome.TooHeavy, UWStringIds.TooHeavyToPickUpMessage);

			return new Result(Outcome.Take, -1);
		}

		/// <summary>
		/// Can piCount pieces of a bought good be taken from the partner's side of the trade?
		/// Only the weight counts there, with the trade's own sentence (UseTradeSlot_ovr097_6E8,
		/// 0xFC: "That is too heavy to take.").
		/// </summary>
		public static Result CheckFromTrade(UWObject pOItem, int piCount, UWInventoryModel pOInventory)
		{
			if (pOItem == null)
				return new Result(Outcome.NotPickable, -1);

			if (pOInventory != null && !pOInventory.CanCarry(pOItem, piCount))
				return new Result(Outcome.TooHeavy, UWStringIds.TooHeavyToTakeMessage);

			return new Result(Outcome.Take, -1);
		}
	}
}
