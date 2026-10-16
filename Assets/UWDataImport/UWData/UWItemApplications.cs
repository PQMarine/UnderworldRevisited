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


		private const int RockBreaksMessage = 136;

		private const int OilLanternMessage = 179;

		private const int OilTorchMessage = 183;

		private const int OilOnWoodMessage = 182;

		/// <summary>How many sling stones a piece below the small boulder holds: UseRockHammerOn
		/// rolls three plus rand % 6.</summary>
		private const int SlingStoneMinimum = 3;

		private const int SlingStoneSpread = 6;

		/// <summary>How far the pieces scatter around the boulder's spot, in eighths of a tile
		/// (UseRockHammerOn: put_at with range 6).</summary>
		public const int RockPieceScatter = 6;

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
				// THE TORCH KEEPS THE WOOD'S QUALITY: UseOilOn (UnderworldExhumed obj/USEITEMS.C,
				// read 2026-10-15) only changes the item id. Until 2026-10-15 ours set 40, so a
				// worn piece of wood made a torch that burnt longer than in the original.
				UWObjectMechanics.SetObjectId(pOTarget, UWObjectMechanics.TorchObjectId, pOData.Textures);

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
		/// on (UWObjectMechanics.FreshDoorSpikeOwner). An open door, and anything else in the
		/// world or carried, gets "You can only spike closed doors." (the spike's second half in
		/// USEITEMS.C, named in UWItemTargetRules, takes the closed door ids 0x140..0x147 only;
		/// pbDoorIsClosed false, 2026-10-15).
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

		/// <summary>One piece of a broken boulder.</summary>
		public struct RockPiece
		{
			/// <summary>The piece's object id: a smaller boulder or the sling stone.</summary>
			public int ObjectId;

			/// <summary>The count of a sling-stone pile, zero for a boulder (which keeps the broken
			/// boulder's own fields).</summary>
			public int Quantity;
		}

		/// <summary>What the rock hammer turns a boulder into.</summary>
		public struct RockResult
		{
			/// <summary>A line of string block 1.</summary>
			public int MessageIndex;

			/// <summary>True when the boulder breaks (and goes), even when no piece is left.</summary>
			public bool Broken;

			/// <summary>The pieces that take its place - empty for none.</summary>
			public RockPiece[] Pieces;
		}

		/// <summary>String block 1: "It seems to have no effect." (until 2026-10-11 declared
		/// twice here, as NoEffectMessage and NoEffectAtAllMessage).</summary>
		private const int NoEffectMessage = UWStringIds.NoEffectMessage;

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

		/// <summary>
		/// The rock hammer on a boulder (UseRockHammerOn, UnderworldExhumed obj/USEITEMS.C:724-763,
		/// read 2026-10-15): the boulder breaks into rand(0..1) + (0x155 - id) + 1 pieces, each one or
		/// two sizes smaller (rand(0..1) + 1); a piece below the small boulder is a pile of three to
		/// eight sling stones. So the large boulder 0x153 gives three or four pieces, 0x154 two or
		/// three, 0x155 one or two, and the small boulder 0x156 none or one: half the time it is
		/// simply gone. Everything else it answers with "It seems to have no effect." The pieces are
		/// copies of the boulder made by the caller (UWItemTargetUse), scattered by RockPieceScatter.
		/// Until 2026-10-15 ours made one piece of the next size, and the small boulder always gave
		/// stones (reference audit round 2).
		///
		/// FITS WITH TREMOR: the spell drops exactly these boulders from the ceiling (see
		/// UWMiscSpellRules), and the hammer turns them into ammunition.
		/// </summary>
		public static RockResult BreakRock(int piObjectId)
		{
			RockResult lOResult = new RockResult { Pieces = new RockPiece[0] };

			if (!UWObjectIds.IsBoulder(piObjectId))
			{
				lOResult.MessageIndex = NoEffectMessage;

				return lOResult;
			}

			lOResult.MessageIndex = RockBreaksMessage;
			lOResult.Broken = true;

			int liCount = UWRandom.Next(2) + (UWObjectIds.MediumBoulder - piObjectId) + 1;

			if (liCount <= 0)
				return lOResult;

			lOResult.Pieces = new RockPiece[liCount];

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				int liPiece = piObjectId + UWRandom.Next(2) + 1;

				if (liPiece > UWObjectIds.SmallBoulder)
				{
					lOResult.Pieces[liAt].ObjectId = UWObjectMechanics.SlingStoneObjectId;
					lOResult.Pieces[liAt].Quantity = SlingStoneMinimum + UWRandom.Next(SlingStoneSpread);
				}
				else
				{
					lOResult.Pieces[liAt].ObjectId = liPiece;
				}
			}

			return lOResult;
		}

		/// <summary>
		/// Takes durability off the spike of a spiked, closed door. Returns true if the strike
		/// is used up by that - then the door itself takes nothing. damage_object
		/// (UnderworldExhumed combat/DAMAGE.C) sends the damage of a closed door 0x140-0x147 whose
		/// owner bit 0 is set to the spike while its strength (owner &gt;&gt; 1) is above 0. piDamage
		/// is what is left AFTER the quality class shift (UWDamageable, 2026-10-12; until then the
		/// full blow). A spike worn to 0 keeps the bit and takes nothing: the blow goes to the door.
		/// </summary>
		public static bool TryDamageDoorSpike(UWObject pODoor, bool pbDoorIsClosed, int piDamage)
		{
			if (pODoor == null || !pbDoorIsClosed || !UWObjectMechanics.IsDoorSpiked(pODoor)
				|| UWObjectMechanics.GetDoorSpikeStrength(pODoor) <= 0)
				return false;

			int liLeft = UWObjectMechanics.GetDoorSpikeStrength(pODoor) - piDamage;

			UWObjectMechanics.SetDoorSpikeStrength(pODoor, liLeft);

			return true;
		}
	}
}
