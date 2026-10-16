namespace UWDataImport.UWData
{
	/// <summary>
	/// WHICH ITEM GOES ONTO WHICH TARGET AND WHAT IT DOES THERE - the one table behind the use
	/// mode of both schemes, the action bar's tools and the modern bags' use (cleanup step 2.4,
	/// 2026-10-12; until then the item ids were known in four places of UWItemDrag and
	/// Interaction - fIsUsableItem, NeedsWorldTarget, fTryUseItemOnTarget and the anvil and oil
	/// branches written twice). The Unity side carries the action out (UWItemTargetUse).
	///
	/// THE ORIGINAL (obj/USEITEMS.C, UnderworldExhumed, read 2026-10-12): UseObj dispatches on
	/// the object's class; an item that needs a second object goes through UseThing ("Use
	/// &lt;name&gt; on what?") and the second half is called with the object clicked next -
	/// UsePoleOn (a switch is used from afar, message 0x9D), UseLockpickOn and UseKeyOn
	/// (checkLock), seg040_352B_AFF (the Key of Infinity), UseBonesOn, SpikeDoor_seg040_662,
	/// UseRockHammerOn (a target in the world only), TybalsOrb_seg040_A12 (the orb rock),
	/// UseOilOn and UseAnvilOn (a carried target only). The keys, the lockpick, the spike, the
	/// rock hammer, the orb rock and the oil flask ask from the pack; the anvil and the pole
	/// ask from the world as well (UseUtil).
	///
	/// THE REFUSALS ARE THE ORIGINAL'S, ON CARRIED TARGETS AS WELL (decided by the user,
	/// 2026-10-15, reference audit round 2 C4): mous_in_inv (ui/INTERACT.C) calls the same second
	/// half with the carried thing that mous_in_3d calls with a world thing, so each tool answers
	/// a wrong target in its own words wherever it lies - the spike "You can only spike closed
	/// doors.", the lockpick and the keys "There is no lock on that." (checkLock finds no lock in
	/// the thing's chain), the bones, the orb rock and the Key of Infinity "It seems to have no
	/// effect.", the pole "The pole cannot be used on that."; the rock hammer says nothing to a
	/// carried thing. The pole alone stays in the use mode after a carried target: its second
	/// half never clears the pointer, and the inventory click does not either (KeepsUseMode).
	/// Until 2026-10-15 ours said "The &lt;item&gt; cannot be used on that." and ignored carried
	/// targets.
	/// </summary>
	public static class UWItemTargetRules
	{
		/// <summary>What a carried item asks for when it is used.</summary>
		public enum TargetKind
		{
			/// <summary>Nothing - it takes effect at once (UWItemUse) or has no use.</summary>
			None,

			/// <summary>A thing in the world (the use mode's click into the view).</summary>
			World,

			/// <summary>A carried thing (the oil flask; the anvil, used from the world).</summary>
			Carried
		}

		/// <summary>What a target is, as far as the items tell targets apart.</summary>
		public enum TargetSort
		{
			/// <summary>Any other thing in the world.</summary>
			Thing,

			/// <summary>A door (UWEntityInfo.IsDoor).</summary>
			Door,

			/// <summary>A barrel, chest or nightstand (UWObjectMechanics.IsWorldContainer): its
			/// lock hangs in its own chain, so key and lockpick work on it like on a door.</summary>
			WorldContainer,

			/// <summary>A thing in the inventory.</summary>
			Carried
		}

		/// <summary>What the item does to the target.</summary>
		public enum Action
		{
			/// <summary>Nothing, and nothing is said - a world tool clicked onto the inventory,
			/// the oil flask or the anvil clicked into the view.</summary>
			Ignore,

			/// <summary>"The ... cannot be used on that." - an item that asks for no target at all;
			/// every tool answers a wrong target in its own words.</summary>
			NotApplicable,

			FireSwitch,
			PickLock,
			KeyOfInfinity,
			Bones,
			Spike,
			BreakRock,
			OrbRock,
			ToggleLock,
			Repair,
			Oil,

			/// <summary>"It seems to have no effect." (0x84): bones, orb rock and Key of Infinity on a
			/// carried thing.</summary>
			NoEffect,

			/// <summary>The pole on a carried thing: "The pole cannot be used on that.", a status
			/// pass, and the use mode stays (UsePoleOn).</summary>
			PoleOnCarried
		}

		/// <summary>String block 1: "Using the pole you trigger the switch." (UW.EXE 0x9D,
		/// UsePoleOn) - "switch" whatever was hit (per user on the original, 2026-08-28: on a
		/// button it still said "switch").</summary>
		public const int PoleTriggersSwitchMessage = 158;

		/// <summary>String block 1: "The pole cannot be used on that." (UW.EXE 0x9E, UsePoleOn) -
		/// the pole on anything but a switch, in the world or carried.</summary>
		public const int PoleCannotBeUsedMessage = 159;

		/// <summary>
		/// What a CARRIED item asks for when it is clicked for use: the use mode's target, or
		/// nothing. Pole, spike, rock hammer, orb rock, Key of Infinity and bones ask for a thing
		/// in the world, the oil flask for a carried thing; an item that carries a spell never
		/// asks (UW.EXE casts it right away, see UWItemUse; until 2026-09-14 a refused cast put the
		/// item into use mode); the keys and the lockpick ask for a world thing, the lock object
		/// does not. Food and light sources take effect at once (UWItemUse.TryUseImmediately) and
		/// are not here.
		/// </summary>
		public static TargetKind Classify(UWObject pOItem, DataImport pOData)
		{
			if (pOItem == null)
				return TargetKind.None;

			int liId = pOItem.ID;

			if (liId == UWObjectMechanics.OilFlaskObjectId)
				return TargetKind.Carried;

			if (liId == UWObjectMechanics.PoleObjectId
				|| liId == UWObjectMechanics.SpikeObjectId
				|| liId == UWObjectMechanics.RockHammerObjectId
				|| liId == UWObjectMechanics.OrbRockObjectId
				|| liId == UWObjectMechanics.KeyOfInfinityObjectId
				|| UWEndgameRules.IsBones(liId))
				return TargetKind.World;

			if (UWItemUse.SpellOf(pOItem, pOData).Exists)
				return TargetKind.None;

			return pOItem.GetCategory() == UWObject.ObjectCategoryEnum.KeysLockpickLock
				&& liId != UWObjectMechanics.LockObjectId
				? TargetKind.World
				: TargetKind.None;
		}

		/// <summary>Whether an item IN THE USE MODE takes a carried target: the oil flask and
		/// the anvil (UseOilOn and UseAnvilOn return at once without "other", the carried
		/// target); every other item takes a world target.</summary>
		public static bool TakesCarriedTarget(int piItemId)
		{
			return piItemId == UWObjectMechanics.OilFlaskObjectId || piItemId == UWObjectMechanics.AnvilObjectId;
		}

		/// <summary>The sort of a thing in the world.</summary>
		public static TargetSort SortOf(int piTargetId, bool pbIsDoor)
		{
			if (pbIsDoor)
				return TargetSort.Door;

			return UWObjectMechanics.IsWorldContainer(piTargetId) ? TargetSort.WorldContainer : TargetSort.Thing;
		}

		/// <summary>
		/// What the item does to a target of that sort. Every item has its OWN allowed targets -
		/// no shared "tap anything" case (checked by user in the original, 2026-08-28). The order
		/// matters where the classes overlap: the lockpick looks like a key (the same class) and
		/// comes first; the Key of Infinity is in another class and comes before the keys; the
		/// rock hammer and the orb rock accept any world target and say themselves that nothing
		/// happens.
		/// </summary>
		public static Action OnTarget(UWObject pOItem, TargetSort peTarget)
		{
			if (pOItem == null)
				return Action.Ignore;

			int liId = pOItem.ID;

			// THE ANVIL AND THE OIL FLASK GO ONTO A CARRIED THING, not onto the world (the
			// anvil: a world object onto an item, the reference's anvil.Use and anvil.UseOn).
			if (liId == UWObjectMechanics.AnvilObjectId)
				return peTarget == TargetSort.Carried ? Action.Repair : Action.Ignore;

			if (liId == UWObjectMechanics.OilFlaskObjectId)
				return peTarget == TargetSort.Carried ? Action.Oil : Action.Ignore;

			// Every other item is a world tool; on a carried thing its own second half answers
			// (mous_in_inv calls ObjectActor with the carried thing, 2026-10-15).
			bool lbCarried = peTarget == TargetSort.Carried;

			if (liId == UWObjectMechanics.PoleObjectId)
				return lbCarried ? Action.PoleOnCarried : Action.FireSwitch;

			// THE LOCKPICK AND THE KEYS look for a lock in whatever they touch (checkLock walks the
			// thing's chain); without one they say "There is no lock on that.". The lockpick looks
			// like a key (the same class) and comes first.
			if (liId == UWObjectMechanics.LockpickObjectId)
				return Action.PickLock;

			if (liId == UWObjectMechanics.KeyOfInfinityObjectId)
				return lbCarried ? Action.NoEffect : Action.KeyOfInfinity;

			if (UWEndgameRules.IsBones(liId))
				return lbCarried ? Action.NoEffect : Action.Bones;

			// The spike answers anything but a closed door, carried or not, itself.
			if (liId == UWObjectMechanics.SpikeObjectId)
				return Action.Spike;

			// The rock hammer returns at once on a carried thing (UseRockHammerOn: "other").
			if (liId == UWObjectMechanics.RockHammerObjectId)
				return lbCarried ? Action.Ignore : Action.BreakRock;

			if (liId == UWObjectMechanics.OrbRockObjectId)
				return lbCarried ? Action.NoEffect : Action.OrbRock;

			if (pOItem.GetCategory() == UWObject.ObjectCategoryEnum.KeysLockpickLock
				&& liId != UWObjectMechanics.LockObjectId)
				return Action.ToggleLock;

			return lbCarried ? Action.Ignore : Action.NotApplicable;
		}

		/// <summary>Whether the use mode stays after the action: only the pole on a carried thing
		/// (UsePoleOn clears neither the pointer nor the input mode, and mous_in_inv does not
		/// either, unlike mous_in_3d after a world target).</summary>
		public static bool KeepsUseMode(Action peAction)
		{
			return peAction == Action.PoleOnCarried;
		}
	}
}
