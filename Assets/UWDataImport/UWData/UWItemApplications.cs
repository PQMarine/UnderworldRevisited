namespace UWDataImport.UWData
{
	/// <summary>
	/// Items applied to something: the oil flask on a light source or a piece of wood, the
	/// spike on a door, the rock hammer on a boulder. Engine-free since 2026-09-18 (P3 of the
	/// engine separation), out of Interaction, which keeps the door component, the world
	/// spawns and the inventory.
	/// </summary>
	public static class UWItemApplications
	{
		// String block 1, our numbering.
		private const int OnlyClosedDoorsMessage = 129;

		private const int DoorNowSpikedMessage = 130;

		/// <summary>The message a spiked door answers everything with - look as well as use.</summary>
		public const int DoorIsSpikedMessage = 132;

		private const int NoEffectAtAllMessage = 133;

		private const int RockBreaksMessage = 136;

		private const int OilLanternMessage = 179;

		private const int OilTorchMessage = 183;

		private const int OilOnWoodMessage = 182;

		/// <summary>How many sling stones the smallest boulder yields - the reference
		/// rolls three plus zero to five.</summary>
		private const int SlingStoneMinimum = 3;

		private const int SlingStoneSpread = 6;

		/// <summary>The outcome of an application.</summary>
		public struct Result
		{
			/// <summary>False when the item does nothing on that target at all.</summary>
			public bool Handled;

			/// <summary>A line of string block 1, or -1 for none.</summary>
			public int MessageIndex;

			/// <summary>The applied item (flask, spike) is used up.</summary>
			public bool ItemUsed;

			/// <summary>The target changed (the inventory display follows).</summary>
			public bool TargetChanged;
		}

		/// <summary>
		/// The oil flask: turns a piece of wood into a torch, or refills torch and
		/// lantern.
		///
		/// It does not work ON A BURNING light source - there is a separate warning for that.
		/// An unlit one gets 32 fuller, at most up to 63; if it is already full,
		/// it says so.
		///
		/// The flask itself is used up by each of these applications.
		///
		/// DEVIATION FROM THE REFERENCE, BUT DELIBERATE: it picks the message group with
		/// "item_id == 0x90 or 94" - the second number is decimal and makes no sense,
		/// obviously the lit lantern 0x94 is meant. With its version one would get
		/// the sentence about the torch when putting oil on a lit lantern.
		/// </summary>
		public static Result ApplyOil(UWObject pOTarget, DataImport pOData)
		{
			Result lOResult = new Result { MessageIndex = -1 };

			if (pOTarget == null || pOData == null)
				return lOResult;

			int liId = pOTarget.ID;

			if (liId >= UWObjectMechanics.FirstWoodObjectId && liId <= UWObjectMechanics.LastWoodObjectId)
			{
				UWObjectMechanics.SetObjectId(pOTarget, UWObjectMechanics.TorchObjectId, pOData.Textures);

				pOTarget.Quality = UWObjectMechanics.NewTorchQuality;

				lOResult.Handled = true;
				lOResult.MessageIndex = OilOnWoodMessage;
				lOResult.ItemUsed = true;
				lOResult.TargetChanged = true;

				return lOResult;
			}

			bool lbLantern = liId == UWObjectMechanics.LanternObjectId
				|| liId == UWObjectMechanics.LanternObjectId + UWObjectMechanics.LitLightOffset;

			int liFirstMessage = lbLantern ? OilLanternMessage : OilTorchMessage;

			// If it is burning, oil is a bad idea - and the flask stays intact.
			if (UWObjectMechanics.IsLitLight(liId))
			{
				lOResult.Handled = true;
				lOResult.MessageIndex = liFirstMessage;

				return lOResult;
			}

			if (liId != UWObjectMechanics.LanternObjectId && liId != UWObjectMechanics.TorchObjectId)
				return lOResult;

			lOResult.Handled = true;

			if (pOTarget.Quality >= UWObjectMechanics.FullLightQuality)
			{
				lOResult.MessageIndex = liFirstMessage + 2;

				return lOResult;
			}

			int liFilled = pOTarget.Quality + UWObjectMechanics.OilFlaskFill;

			pOTarget.Quality = (ushort)(liFilled > UWObjectMechanics.FullLightQuality
				? UWObjectMechanics.FullLightQuality
				: liFilled);

			lOResult.MessageIndex = liFirstMessage + 1;
			lOResult.ItemUsed = true;
			lOResult.TargetChanged = true;

			return lOResult;
		}

		/// <summary>
		/// The spike wedges a closed door: the door's owner field carries the spike from now
		/// on (UWObjectMechanics.FreshDoorSpikeOwner). An open one cannot be wedged.
		/// </summary>
		public static Result SpikeDoor(UWObject pODoor, bool pbDoorIsClosed)
		{
			Result lOResult = new Result { MessageIndex = -1 };

			if (pODoor == null)
				return lOResult;

			lOResult.Handled = true;

			if (!pbDoorIsClosed)
			{
				lOResult.MessageIndex = OnlyClosedDoorsMessage;

				return lOResult;
			}

			pODoor.Owner = UWObjectMechanics.FreshDoorSpikeOwner;

			lOResult.MessageIndex = DoorNowSpikedMessage;
			lOResult.ItemUsed = true;
			lOResult.TargetChanged = true;

			return lOResult;
		}

		/// <summary>What the rock hammer turns a boulder into.</summary>
		public struct RockResult
		{
			/// <summary>A line of string block 1.</summary>
			public int MessageIndex;

			/// <summary>The boulder goes and this object takes its place - zero for nothing.</summary>
			public int SpawnObjectId;

			/// <summary>How many of them, zero for a single piece without count.</summary>
			public int SpawnQuantity;
		}

		/// <summary>
		/// The rock hammer: breaks a boulder into the next smaller one.
		///
		/// The four boulders 339 to 342 are gone through in order; the smallest becomes
		/// three to eight sling stones. Everything else it answers with "It seems to have no
		/// effect." - so the hammer can be applied to any world object, it just does
		/// nothing anywhere else. Freshly broken is fresh - the caller spawns with
		/// UWSummonSpellRules.NewObjectQuality, the same reasoning as for conjured food.
		///
		/// FITS WITH TREMOR: the spell drops exactly these boulders from the ceiling (see
		/// UWMiscSpellRules), and the hammer turns them into ammunition.
		/// </summary>
		/// <summary>String block 1: "It seems to have no effect."</summary>
		private const int NoEffectMessage = 133;

		/// <summary>String block 1: "The orb is destroyed!"</summary>
		private const int OrbDestroyedMessage = 134;

		/// <summary>The class-7 picture the smashed orb leaves: offset 4 with a duration of 5
		/// (TybalsOrb_seg040_A12 pushes 4 and 5 into SpawnClass7Object).</summary>
		public const int OrbEffectObjectId = 0x1C0 + 4;

		/// <summary>
		/// The orb rock on something (TybalsOrb_seg040_A12).
		///
		/// On TYBALL'S ORB it says "The orb is destroyed!", leaves a class-7 picture where
		/// the orb stood, and both the orb and the rock are gone. On anything else it only
		/// says that it seems to have no effect - the rock survives that.
		/// </summary>
		public static Result UseOrbRock(UWObject pOTarget)
		{
			Result lOResult = new Result();

			if (pOTarget == null || pOTarget.ID != UWObjectMechanics.TyballOrbObjectId)
			{
				lOResult.Handled = true;
				lOResult.MessageIndex = NoEffectMessage;

				return lOResult;
			}

			lOResult.Handled = true;
			lOResult.MessageIndex = OrbDestroyedMessage;
			lOResult.ItemUsed = true;
			lOResult.TargetChanged = true;

			return lOResult;
		}

		public static RockResult BreakRock(int piObjectId)
		{
			RockResult lOResult = new RockResult();

			if (piObjectId < UWObjectMechanics.FirstBoulderObjectId
				|| piObjectId > UWObjectMechanics.LastBoulderObjectId)
			{
				lOResult.MessageIndex = NoEffectAtAllMessage;

				return lOResult;
			}

			lOResult.MessageIndex = RockBreaksMessage;

			if (piObjectId >= UWObjectMechanics.LastBoulderObjectId)
			{
				lOResult.SpawnObjectId = UWObjectMechanics.SlingStoneObjectId;
				lOResult.SpawnQuantity = SlingStoneMinimum + UWRandom.Next(SlingStoneSpread);
			}
			else
			{
				lOResult.SpawnObjectId = piObjectId + 1;
			}

			return lOResult;
		}

		/// <summary>
		/// Takes durability off the spike of a spiked, closed door. Returns true if the strike
		/// is used up by that - then the door itself takes nothing. The reference redirects
		/// damage on a spiked, closed door to the durability of the SPIKE instead of the door's
		/// (damage.cs, branch "damage to spiked closed doors"). Once the spike is gone, the door
		/// is free again - and only the next strike hits it again.
		/// </summary>
		public static bool TryDamageDoorSpike(UWObject pODoor, bool pbDoorIsClosed, int piDamage)
		{
			if (pODoor == null || !pbDoorIsClosed || !UWObjectMechanics.IsDoorSpiked(pODoor))
				return false;

			int liLeft = UWObjectMechanics.GetDoorSpikeStrength(pODoor) - piDamage;

			UWObjectMechanics.SetDoorSpikeStrength(pODoor, liLeft);

			return true;
		}
	}
}
