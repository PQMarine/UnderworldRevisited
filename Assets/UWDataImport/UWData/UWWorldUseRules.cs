namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT USING A THING IN THE WORLD DOES when it is neither a door nor a creature with
	/// something to say nor a switch - the object's own effect (cleanup step 2.4, 2026-10-12;
	/// until then the ids sat in Interaction.fTryUseObjectEffect's switch). Interaction.UseTarget
	/// asks in this order: the door, the conversation, the use trigger (UseTriggerFiresFirst),
	/// the effect from here, the wording of an inscription.
	///
	/// THE ORIGINAL (obj/USEITEMS.C UseObj, UnderworldExhumed, read 2026-10-12) dispatches on the
	/// class: a container used from the world is emptied (UseCont -> DumpTheBag), a light source
	/// used from the world answers 0x7B (UseLight), the anvil and the pole ask what to use them
	/// on (UseUtil -> UseThing), the fountain casts its spell and the cauldron prints 0x112
	/// (UseMagic), the fountain's water uses the fountain linked to it, the silver tree becomes a
	/// seed in the hand (the animated class 0xA). The gravestone shows its inscription as looking
	/// does (the reference's use.cs, case 5) and the shrine takes the mantra.
	///
	/// NOT HERE, as in the original (per user, 2026-09-19): the bedroll on the floor and the
	/// nightstand do nothing - sleeping is the bedroll used from the inventory (UWItemUse; until
	/// that day a case put the player to sleep from the world), and the nightstand stays a world
	/// container only for the use trigger rule and the loot placement. What using a container
	/// does (the spill) is UWSpillRules; whether
	/// that changes is the open decision 25 of the cleanup plan, not this table.
	/// </summary>
	public static class UWWorldUseRules
	{
		/// <summary>The thing's own effect when used from the world.</summary>
		public enum Effect
		{
			/// <summary>None of its own - the inscription's wording, if it has one, else nothing.</summary>
			None,

			/// <summary>The gravestone: its inscription and picture (false when it has no grave
			/// number - then the use goes on as for any thing).</summary>
			Gravestone,

			/// <summary>The fountain or its water: the fountain's spell and message.</summary>
			Fountain,

			/// <summary>The silver tree: taken back as a seed (UWSilverTreeRules).</summary>
			SilverTree,

			/// <summary>The cauldron: "The cauldron is empty." - always (checked by user in the
			/// original; the string block has no second message).</summary>
			Cauldron,

			/// <summary>The shrine: the mantra prompt.</summary>
			Shrine,

			/// <summary>The anvil and the pole lying on the floor: the use mode, as from the pack
			/// (per user on the original, 2026-09-19, "strangely, normal" for the pole).</summary>
			UseModeFromWorld,

			/// <summary>A barrel, a chest, or a container of major class 2 (0x80-0x8F: sacks,
			/// boxes, the urn): emptied onto the floor (UseCont -> DumpTheBag; in the listing's
			/// names ObjectUse_seg040 case 2, minor class 0 -> UseDoorOrContainer_seg040_352B_2494
			/// -> EmptyContainer when not used from the inventory). Per user, 2026-09-30: the urn
			/// on level 2, 24/36, empties its six spikes in the original; ours did nothing.</summary>
			Spill,

			/// <summary>A light source: "Lights may only be used if equipped." (UseLight's 0x7B
			/// for a use from the world; per user on the original, 2026-09-19).</summary>
			LightsOnlyEquipped,

			/// <summary>A glowing rock: gathered into the carried one as walking over it does
			/// (UseMagic with how 0 runs the touch's branch; UWGlowingRockRules.TryGather) -
			/// nothing when none is carried (2026-10-15).</summary>
			GlowingRock,

			/// <summary>Burning incense: its vision plays and it becomes debris, as from the pack
			/// (UseUnique's incense case does not ask how; UWItemUse.BurnIncense, 2026-10-15).</summary>
			BurningIncense
		}

		/// <summary>String block 1: "Lights may only be used if equipped." (UW.EXE 0x7B).</summary>
		public const int LightsOnlyEquippedMessage = 124;

		/// <summary>The thing's own effect by its id and category.</summary>
		public static Effect Classify(int piObjectId, UWObject.ObjectCategoryEnum peCategory)
		{
			switch (piObjectId)
			{
				case UWEndgameRules.GravestoneObjectId:
					return Effect.Gravestone;

				case UWObjectMechanics.FountainObjectId:
				case UWObjectMechanics.FountainWaterObjectId:
					return Effect.Fountain;

				case UWSilverTreeRules.TreeObjectId:
					return Effect.SilverTree;

				case UWObjectMechanics.CauldronObjectId:
					return Effect.Cauldron;

				case UWShrineRules.ShrineObjectId:
					return Effect.Shrine;

				case UWObjectMechanics.AnvilObjectId:
				case UWObjectMechanics.PoleObjectId:
					return Effect.UseModeFromWorld;

				// The nightstand is a world container too, but used it does nothing.
				case UWObjectMechanics.BarrelObjectId:
				case UWObjectMechanics.ChestObjectId:
					return Effect.Spill;

				// Used from the world as from the pack (UseMagic, UseUnique; until 2026-10-15
				// neither did anything here, reference audit round 2, A48).
				case UWGlowingRockRules.GlowingRockId:
					return Effect.GlowingRock;

				case UWItemUse.BurningIncenseObjectId:
					return Effect.BurningIncense;
			}

			if (peCategory == UWObject.ObjectCategoryEnum.Containers)
				return Effect.Spill;

			if (UWObjectMechanics.IsUnlitLight(piObjectId) || UWObjectMechanics.IsLitLight(piObjectId))
				return Effect.LightsOnlyEquipped;

			return Effect.None;
		}

		/// <summary>
		/// Whether a thing's use trigger is fired BEFORE its effect, as a switch's: everything
		/// but a container. A barrel, chest or nightstand (UWObjectMechanics.IsWorldContainer)
		/// and any container fire their use trigger while spilling (UWSpillRules), so the first
		/// link of the chain must not swallow the use here.
		/// </summary>
		public static bool UseTriggerFiresFirst(int piObjectId, UWObject.ObjectCategoryEnum peCategory)
		{
			return !UWObjectMechanics.IsWorldContainer(piObjectId) && peCategory != UWObject.ObjectCategoryEnum.Containers;
		}
	}
}
