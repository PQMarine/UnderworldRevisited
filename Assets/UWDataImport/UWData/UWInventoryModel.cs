using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Inventory data model: eleven equipment slots, one per UWArmorItemMap.BodySlot
	/// (helmet/chest/gloves/legs/boots, which get a paperdoll graphic in UWCharacter, plus two
	/// rings and four hand places) plus a backpack with 8 fixed places (like the Backpack0-7
	/// slots from the documented PLAYER.DAT format). Taken over from a savegame via
	/// LoadFromPlayerData and handed back for saving via GetSavegameEquipment.
	///
	/// Sits on the same GameObject as Interaction (created from there via AddComponent when
	/// needed, see Interaction.Awake) - no manual wiring in the scene required.
	/// </summary>
	public sealed class UWInventoryModel : IUWEquipment
	{
		private readonly Func<DataImport> mFData;

		private readonly Func<UWLevel> mFLevel;

		/// <param name="pFData">The game data, asked each time (it can be replaced by loading).</param>
		/// <param name="pFLevel">The current level, for resolving container contents.</param>
		public UWInventoryModel(Func<DataImport> pFData, Func<UWLevel> pFLevel)
		{
			mFData = pFData;
			mFLevel = pFLevel;
		}

		private DataImport fData => mFData != null ? mFData() : null;

		private UWLevel fLevel => mFLevel != null ? mFLevel() : null;

		public const int BackpackSize = 8;

		/// <summary>Original: an opened container shows its contents in exactly the same
		/// grid as the backpack (see the UWGameUI.ContainerInventory panel, placed congruent with
		/// the backpack grid) - this is the visible WINDOW SIZE, not the
		/// capacity: a container can hold more contents (from the original level state, see
		/// UWObject.EnsureContentsLoaded), which are then paged through via ContainerScrollOffset.
		/// The real limit is the weight and the item type, see UWContainerCapacity.</summary>
		public const int ContainerSlotCount = 8;

		/// <summary>Why the last attempt to put something into a container failed - read by the
		/// UI to print the matching original message. Fits after a successful attempt.</summary>
		public UWContainerCapacity.ResultEnum LastContainerRejection { get; private set; }
			= UWContainerCapacity.ResultEnum.Fits;

		/// <summary>The container of the last rejection, for the message.</summary>
		public UWObject LastContainerRejectionContainer { get; private set; }

		/// <summary>Type and weight test in one place; remembers the reason for the UI.</summary>
		private bool fContainerAccepts(UWObject pOContainer, UWObject pOItem, UWObject pOReplaced = null)
		{
			UWContainerCapacity.ResultEnum leResult =
				UWContainerCapacity.Check(pOContainer, pOItem, fData, pOReplaced);

			LastContainerRejection = leResult;
			LastContainerRejectionContainer = leResult == UWContainerCapacity.ResultEnum.Fits
				? null : pOContainer;

			return leResult == UWContainerCapacity.ResultEnum.Fits;
		}

		public UWObject[] EquipSlots { get; } = new UWObject[Enum.GetValues(typeof(UWArmorItemMap.BodySlot)).Length];

		public UWObject[] Backpack { get; } = new UWObject[BackpackSize];

		/// <summary>
		/// Which hand slot is the combat hand: the right hand for a right-hander, the left hand
		/// for a left-hander. UW.EXE reads the weapon from inventory slot 8 minus bit 0 of player
		/// byte 0x64 (set = right-handed), see UWPlayerData.IsLeftHanded. Until 2026-09-14 this
		/// was fixed to the right hand. See UWItemDrag.fTryToggleCombatFromClick for the usage.
		/// </summary>
		public UWArmorItemMap.BodySlot MainHandSlot
		{
			get { return IsLeftHanded ? UWArmorItemMap.BodySlot.LeftHandSlot : UWArmorItemMap.BodySlot.RightHandSlot; }
		}

		/// <summary>Handedness of the character, set from the save game or character creation
		/// (UWCharacter.Init).</summary>
		public bool IsLeftHanded { get; set; }

		/// <summary>
		/// The item that currently "sticks to the pointer" via original dragging (UWItemDrag). It is
		/// removed from its origin place IMMEDIATELY when the drag starts (not just hidden visually) -
		/// confirmed against the original: a dragged item is no longer in the backpack/equipped and
		/// therefore has no weight any more. Backpack/EquipSlots are thus the complete truth
		/// during a drag, no special handling in the UI required.
		/// </summary>
		public UWObject CursorItem { get; private set; }

		/// <summary>Original: the use mode (right-click on a usable item, see
		/// UWItemDrag) - unlike CursorItem, the item stays in its place,
		/// only its icon appears at the cursor (see UWItemDrag.fUpdateUseMode). Every use ends
		/// the mode afterwards via CancelUseMode (confirmed by user, 2026-08-27, see
		/// UWItemDrag.fEndUseMode), as does leaving the original control scheme.</summary>
		public UWObject UseModeItem { get; private set; }

		public void EnterUseMode(UWObject pOItem)
		{
			UseModeItem = pOItem;
		}

		public void CancelUseMode()
		{
			UseModeItem = null;
		}

		/// <summary>Original: the currently opened container (bag/chest/...), no matter whether it
		/// is currently in the backpack or still standing in the world - Contents (see UWObject) is
		/// valid independently of the current location of the container object itself. Null
		/// if none is open. See TryOpenContainer/CloseContainer.</summary>
		public UWObject OpenContainer { get; private set; }

		/// <summary>Original: index of the first visible container content in the 8-slot window
		/// (see ContainerSlotCount) - 0 right after opening, changeable via ScrollContainer
		/// as long as there are more contents than the window can show.</summary>
		public int ContainerScrollOffset { get; private set; }

		/// <summary>Fires on every actual change (not on failed
		/// attempts) - UWCharacter and the inventory UI hook in here.</summary>
		public event Action InventoryChanged;

		/// <summary>
		/// Difference between the load the savegame records and our own sum -
		/// captured once on loading.
		///
		/// WHY THERE IS ONE (read 2026-09-25): the original never recomputes the load. PLAYER.DAT
		/// 0x4A is a running total - loaded with the save, raised by the weight of whatever goes
		/// into the inventory (the insert routines of overlays 117, 120 and 121), lowered by the
		/// inventory removal, set to 0 only by Armageddon. The weight of one
		/// object (ovr120_DDD) is the mass times the count for a stack below 512, else the mass
		/// plus everything linked below it (ovr117_FD1) - the same sum as UWInventoryWeight,
		/// which a scan of every save folder confirmed. So whatever changes an object's weight
		/// in place leaves the total behind for good: a wand weighs nothing, its spell object
		/// four tenths, and when an empty wand loses the spell object (MagicChargeUpdate only
		/// unlinks and frees it) the four stay counted - see KeepWeightOf. That is how
		/// Henrietta's original save carries 414 where the objects sum to 404.
		///
		/// Carried along, the offset makes our display the original's: it matches at the
		/// start and follows every movement, because only our sum changes. Saving writes the
		/// total back (UWSavegameWriter), as the original does.
		/// </summary>
		public int WeightOffsetTenthStones { get; private set; }

		/// <summary>An object leaves the inventory without passing the original's removal - its
		/// weight stays in the running total (see WeightOffsetTenthStones).</summary>
		public void KeepWeightOf(UWObject pOObject, UWCommonObjectProperties pOProperties)
		{
			if (pOObject != null && pOProperties != null)
				WeightOffsetTenthStones += UWInventoryWeight.GetItemTenthStones(pOObject, pOProperties);
		}

		/// <summary>An object changes its weight in place (the bowl that becomes the stew): the
		/// running total keeps the weight it had before.</summary>
		public void KeepWeightChange(int piBeforeTenthStones, int piAfterTenthStones)
		{
			WeightOffsetTenthStones += piBeforeTenthStones - piAfterTenthStones;
		}

		/// <summary>The carried load in tenth-stones. What hangs at the pointer does not count -
		/// it is no longer in any place.</summary>
		public int GetCarriedTenthStones(UWCommonObjectProperties pOProperties)
		{
			if (pOProperties == null)
				return 0;

			int liSum = UWInventoryWeight.GetTotalTenthStones(Backpack, pOProperties)
				+ UWInventoryWeight.GetTotalTenthStones(EquipSlots, pOProperties);

			return liSum + WeightOffsetTenthStones;
		}

		/// <summary>
		/// Does an item still fit within the carrying capacity - maximum load from the player data against
		/// the carried load, the same calculation as the display (UWGameUI.fRefreshWeight)?
		/// piCount &gt; 0 weighs only that many pieces of a stack; 0 the whole item including
		/// contents. Without data nothing is refused.
		/// </summary>
		public bool CanCarry(UWObject pOItem, int piCount = 0)
		{
			if (pOItem == null || fData == null
				|| fData.InitialPlayer == null)
				return true;

			UWCommonObjectProperties lOProperties = fData.CommonObjectProperties;

			if (lOProperties == null)
				return true;

			// A CONTAINER FROM THE WORLD weighs with its contents - but those are only resolved on
			// first use. Without this the first pick-up of a full bag counted only the bag, so it could
			// be taken although it was too heavy, and only later tries were refused (per user, bag on
			// level 3, 2026-09-17).
			if (pOItem.Contents == null && pOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers
				&& fLevel != null && fLevel.Masterlist != null)
				pOItem.EnsureContentsLoaded(fLevel.Masterlist);

			int liWeight = UWInventoryWeight.GetItemTenthStones(pOItem, lOProperties);

			if (piCount > 0 && pOItem.HasQuantity && pOItem.Quantity > piCount && pOItem.Quantity < 512)
			{
				UWCommonObjectProperties.Entry lOEntry;

				liWeight = lOProperties.TryGet(pOItem.ID, out lOEntry) ? lOEntry.MassTenthStones * piCount : 0;
			}

			int liRemaining = fData.InitialPlayer.MaxWeight - GetCarriedTenthStones(lOProperties);

			return liWeight <= liRemaining;
		}

		/// <summary>
		/// Takes over equipment and backpack from a loaded savegame.
		///
		/// The order of the eleven equipment places in the savegame differs from that of
		/// our paperdoll, hence the mapping table. The two "shoulder" places of the
		/// original are the circular areas next to the head, for us the off hands.
		/// </summary>
		public void LoadFromPlayerData(UWPlayerData pOPlayer)
		{
			if (pOPlayer == null || !pOPlayer.IsLoaded)
				return;

			IsLeftHanded = pOPlayer.IsLeftHanded;

			for (int liSlot = 0; liSlot < msSavegameSlotMap.Length && liSlot < pOPlayer.Equipment.Length; liSlot++)
			{
				UWObject lOItem = pOPlayer.Equipment[liSlot];

				if (lOItem != null)
					EquipSlots[(int)msSavegameSlotMap[liSlot]] = lOItem;
			}

			for (int liSlot = 0; liSlot < Backpack.Length && liSlot < pOPlayer.Backpack.Length; liSlot++)
				Backpack[liSlot] = pOPlayer.Backpack[liSlot];

			// Capture the reconciliation once, while nothing has been moved yet.
			if (fData != null && pOPlayer.CarriedWeight > 0)
			{
				WeightOffsetTenthStones = 0;
				WeightOffsetTenthStones = pOPlayer.CarriedWeight
					- GetCarriedTenthStones(fData.CommonObjectProperties);
			}

			// Without the signal the paperdoll stays empty until something else touches the
			// inventory - UWCharacter only rebuilds it on InventoryChanged.
			InventoryChanged?.Invoke();

			ApplyCarriedLight();
		}

		/// <summary>Is the item in one of the four hand places?</summary>
		public bool IsInHandSlot(UWObject pOItem)
		{
			if (pOItem == null)
				return false;

			foreach (UWArmorItemMap.BodySlot leSlot in mOHandSlots)
			{
				if (EquipSlots[(int)leSlot] == pOItem)
					return true;
			}

			return false;
		}

		// ------------------------------------------------- Burning down

		/// <summary>
		/// Lets the burning light sources in the four hand places burn down - the original's
		/// UpdateInventoryLightSources_seg028_2985_3B6, read 2026-09-24.
		///
		/// THE RATE is the light table's first byte, which we call the DURATION (lantern 10,
		/// torch 3, candle 12, taper 0), indexed by the object number AND 0xF - for the four lit
		/// ones that is their own entry. Zero never burns down. (Until then this was an open
		/// choice against the reference's class index; the original settles it for the duration.)
		///
		/// THE COUNT IS NOT PER LIGHT but the player tick's own counter, 1 to 24 (piCounter,
		/// UWPlayerVitals.TickInCycle): a point is due when the counter divides by the rate. So a
		/// torch loses eight points per 24 ticks, while the lantern burns at 10 and 20 only and the
		/// candle at 12 and 24 - two each. From 2026-09-05 ours counted each light on its own so
		/// that lighting and dousing between deductions would not keep a torch whole (per user);
		/// the original has that gap, and per user it stays as in the original.
		///
		/// piTicks is 1 for the player tick. Sleep passes its hours times 180 with counter 0,
		/// which makes 1 + hours * 180 / rate points (Sleep_ovr143_D1F).
		///
		/// A light whose quality is not above the points due goes out: quality zero, back to the
		/// unlit object (LightBurnedOut switches the number and the picture).
		/// </summary>
		public void BurnTick(int piTicks, int piCounter)
		{
			bool lbChanged = false;

			foreach (UWArmorItemMap.BodySlot leSlot in mOHandSlots)
			{
				UWObject lOItem = EquipSlots[(int)leSlot];

				if (lOItem == null || !UWObjectMechanics.IsLitLight(lOItem.ID))
					continue;

				int liRate = fGetBurnTicks(lOItem.ID);

				if (liRate <= 0)
					continue;

				int liPoints = piCounter % liRate == 0 ? 1 : 0;

				if (piTicks > 1)
					liPoints += piTicks / liRate;

				if (liPoints == 0)
					continue;

				lbChanged = true;

				if (lOItem.Quality > liPoints)
				{
					lOItem.Quality = (ushort)(lOItem.Quality - liPoints);

					continue;
				}

				lOItem.Quality = 0;

				LightBurnedOut?.Invoke(lOItem);
			}

			if (!lbChanged)
				return;

			ApplyCarriedLight();
			NotifyChanged();
		}

		/// <summary>How many ticks a quality point of this light source lasts. Zero means:
		/// it never burns down.</summary>
		private int fGetBurnTicks(int piObjectId)
		{
			if (fData == null
				|| fData.ObjectProperties == null)
				return 0;

			return fData.ObjectProperties.GetLightDuration(piObjectId);
		}

		/// <summary>A light source has burned down. Whoever knows the image puts it out -
		/// switching the object number along with the image lives in UWItemDrag.</summary>
		public event System.Action<UWObject> LightBurnedOut;

		/// <summary>Tells the UI that something in the inventory has changed. For
		/// changes TO the item itself - a doused lantern changes its image without
		/// changing its place, and would otherwise still look lit.</summary>
		public void NotifyChanged()
		{
			InventoryChanged?.Invoke();
		}

		/// <summary>
		/// Clears away everything the player character carries - equipment, backpack, what hangs
		/// at the pointer and an open container. For Armageddon (see UWMiscSpell), the
		/// only thing in the game that does this.
		///
		/// The items are not dropped but cease to exist - exactly as
		/// the reference describes it (playerdat.ClearInventory).
		/// </summary>
		public void ClearAll()
		{
			for (int liAt = 0; liAt < EquipSlots.Length; liAt++)
				EquipSlots[liAt] = null;

			for (int liAt = 0; liAt < Backpack.Length; liAt++)
				Backpack[liAt] = null;

			CursorItem = null;
			CloseAllContainers();
			ContainerScrollOffset = 0;
			UseModeItem = null;

			// Armageddon, the one caller that empties everything, sets the original's running
			// load to 0 - so nothing of the reconciliation may stay behind.
			WeightOffsetTenthStones = 0;

			ApplyCarriedLight();

			InventoryChanged?.Invoke();
		}

		/// <summary>
		/// Sets the carried light from a burning light source at the pointer or in one of the
		/// four hand places, because only there may a burning one lie; without one the light goes out.
		///
		/// This also takes over a burning light source from the savegame (see
		/// LoadFromPlayerData): whoever leaves their game with a lit lantern finds it burning
		/// again on loading - until 2026-09-05 it stayed dark for us.
		/// </summary>
		public void ApplyCarriedLight()
		{
			CarriedLightChanged?.Invoke(GetCarriedLightObjectId());
		}

		/// <summary>Raised whenever the carried light may have changed, with the object id of the
		/// burning light source that should shine (0 for none). The host turns the light on or off.</summary>
		public event Action<int> CarriedLightChanged;

		/// <summary>
		/// A BURNING LIGHT SOURCE ONLY BURNS IN A HAND PLACE. Put anywhere else in the inventory -
		/// backpack, container, body slot - it goes out and turns back into its unlit number, the
		/// remaining fuel intact (per user on the original, 2026-09-18: "if you put a burning light
		/// source into the inventory it goes out"). On the pointer it keeps burning, a picked-up
		/// burning lantern stays lit (per user, 2026-09-05).
		///
		/// Until 2026-09-18 it was quietly wrong here: a lit torch in the backpack gave no light,
		/// because GetCarriedLightObjectId only looks at the pointer and the hand places, and did
		/// not burn down either, because BurnTick walks the same places - so it kept forever.
		/// </summary>
		public void ExtinguishOutsideHand(UWObject pOItem)
		{
			if (pOItem == null || IsInHandSlot(pOItem))
				return;

			DataImport lOData = mFData != null ? mFData() : null;

			if (UWObjectMechanics.Extinguish(pOItem, lOData != null ? lOData.Textures : null))
				ApplyCarriedLight();
		}

		/// <summary>
		/// Which burning light source gives the carried light: the one at the pointer first - in the
		/// original a picked-up burning lantern stays lit (per user, 2026-09-05) - then the first in a
		/// hand place. 0 if none burns.
		/// </summary>
		public int GetCarriedLightObjectId()
		{
			// THE BRIGHTEST ONE COUNTS, not the first one found: the original walks the four
			// places and keeps the maximum brightness (UW1_asm.asm ovr133_9C9, which then puts it
			// into the upper nibble of player byte 0x63). Taking the first meant that lighting a
			// candle next to a burning lantern DARKENED the room (per user, 2026-09-18).
			UWObjectProperties lOProperties = fData != null ? fData.ObjectProperties : null;

			int liBrightest = 0;
			int liBrightness = -1;

			if (CursorItem != null && UWObjectMechanics.IsLitLight(CursorItem.ID))
			{
				liBrightest = CursorItem.ID;
				liBrightness = lOProperties != null ? lOProperties.GetLightBrightness(CursorItem.ID) : 0;
			}

			foreach (UWArmorItemMap.BodySlot leSlot in mOHandSlots)
			{
				UWObject lOItem = EquipSlots[(int)leSlot];

				if (lOItem == null || !UWObjectMechanics.IsLitLight(lOItem.ID))
					continue;

				int liOwn = lOProperties != null ? lOProperties.GetLightBrightness(lOItem.ID) : 0;

				// Without the table every burning one is worth the same, and then the first found
				// wins, as it did before.
				if (liOwn > liBrightness)
				{
					liBrightest = lOItem.ID;
					liBrightness = liOwn;
				}
			}

			return liBrightest;
		}


		/// <summary>
		/// The equipment in savegame order - the inverse of what
		/// LoadFromPlayerData distributes on loading. For saving (see
		/// UWSavegameWriter).
		/// </summary>
		public UWObject[] GetSavegameEquipment()
		{
			UWObject[] lOResult = new UWObject[msSavegameSlotMap.Length];

			for (int liSlot = 0; liSlot < msSavegameSlotMap.Length; liSlot++)
				lOResult[liSlot] = EquipSlots[(int)msSavegameSlotMap[liSlot]];

			return lOResult;
		}

		/// <summary>Our paperdoll place for the original's slot number (0 helmet ... 4 boots, 5/6
		/// the shoulders, 7/8 the hands, 9/10 the rings), as the slot table in PLAYER.DAT counts.</summary>
		public static UWArmorItemMap.BodySlot FromSavegameSlot(int piSlot)
		{
			return msSavegameSlotMap[System.Math.Max(0, System.Math.Min(msSavegameSlotMap.Length - 1, piSlot))];
		}

		/// <summary>From the place in the savegame to our paperdoll place.</summary>
		private static readonly UWArmorItemMap.BodySlot[] msSavegameSlotMap =
		{
			UWArmorItemMap.BodySlot.Helmet,
			UWArmorItemMap.BodySlot.Chest,
			UWArmorItemMap.BodySlot.Gloves,
			UWArmorItemMap.BodySlot.Legs,
			UWArmorItemMap.BodySlot.Boots,
			UWArmorItemMap.BodySlot.RightOffHandSlot,
			UWArmorItemMap.BodySlot.LeftOffHandSlot,
			UWArmorItemMap.BodySlot.RightHandSlot,
			UWArmorItemMap.BodySlot.LeftHandSlot,
			UWArmorItemMap.BodySlot.RightRing,
			UWArmorItemMap.BodySlot.LeftRing
		};

		/// <summary>
		/// Takes part of a stack to the pointer without touching the stack itself -
		/// its count is only reduced.
		///
		/// This is how the original does it, and there is a visible reason: what hangs at the pointer
		/// weighs nothing. The carried load thus drops immediately on picking up, not only on
		/// dropping (per user, 2026-09-01).
		///
		/// For the WHOLE stack this is the wrong way - then it belongs out of its place like any other
		/// item, see BeginDragFrom*.
		/// </summary>
		public bool TakeFromStack(UWObject pOSource, int piCount)
		{
			if (pOSource == null || piCount < 1 || piCount >= pOSource.Quantity)
				return false;

			CursorItem = pOSource.CloneWithQuantity(piCount);

			if (CursorItem == null)
				return false;

			// The part taken keeps the stack's number, the rest gets a new one (ovr121_A14).
			UWCarriedSlots.OnSplit(pOSource, CursorItem);

			pOSource.Quantity = (ushort)(pOSource.Quantity - piCount);

			InventoryChanged?.Invoke();

			return true;
		}

		/// <summary>
		/// The four equivalent hand places. Only there may a burning light source lie.
		///
		/// The ORDER matters: right shoulder, left shoulder, right hand, left hand -
		/// this is how the original searches for a free place (per user, 2026-09-05). The two
		/// shoulders are for us the circular areas next to the head, i.e. the off-hand places.
		/// </summary>
		private static readonly UWArmorItemMap.BodySlot[] mOHandSlots =
		{
			UWArmorItemMap.BodySlot.RightOffHandSlot,
			UWArmorItemMap.BodySlot.LeftOffHandSlot,
			UWArmorItemMap.BodySlot.RightHandSlot,
			UWArmorItemMap.BodySlot.LeftHandSlot
		};

		/// <summary>Puts the item into the first free hand place. Returns false if
		/// all four are occupied - then the caller reports "Your hands are full."</summary>
		public bool TryPlaceInFreeHandSlot(UWObject pOItem)
		{
			if (pOItem == null)
				return false;

			foreach (UWArmorItemMap.BodySlot leSlot in mOHandSlots)
			{
				if (EquipSlots[(int)leSlot] != null)
					continue;

				EquipSlots[(int)leSlot] = pOItem;

				InventoryChanged?.Invoke();

				return true;
			}

			return false;
		}

		/// <summary>
		/// Takes one piece away from the item - a torch from a stack, for example.
		///
		/// For a stack the quantity drops, otherwise the item disappears from its
		/// place. The search covers backpack, equipment and open container.
		/// </summary>
		public bool TryConsumeOne(UWObject pOItem)
		{
			if (pOItem == null)
				return false;

			// A quantity field of 512 or more is not a count but a special link (an enchantment, a
			// spell), as fGetStackCount in Interaction reads it. Counting it down left enchanted
			// armour destroyed at the anvil on the paper doll and broke its enchantment link
			// (per user, 2026-09-17: the cap in SAVE4 has 719).
			if (pOItem.HasQuantity && pOItem.Quantity > 1 && pOItem.Quantity < 512)
			{
				pOItem.Quantity = (ushort)(pOItem.Quantity - 1);

				InventoryChanged?.Invoke();

				return true;
			}

			for (int liSlot = 0; liSlot < Backpack.Length; liSlot++)
			{
				if (Backpack[liSlot] != pOItem)
					continue;

				Backpack[liSlot] = null;

				InventoryChanged?.Invoke();

				return true;
			}

			for (int liSlot = 0; liSlot < EquipSlots.Length; liSlot++)
			{
				if (EquipSlots[liSlot] != pOItem)
					continue;

				EquipSlots[liSlot] = null;

				InventoryChanged?.Invoke();

				return true;
			}

			if (OpenContainer != null && OpenContainer.Contents != null
				&& OpenContainer.Contents.Remove(pOItem))
			{
				InventoryChanged?.Invoke();

				return true;
			}

			// Also from a closed bag anywhere in the pack - where FindCarried finds ammunition.
			if (fRemoveFromCarriedContents(pOItem))
			{
				InventoryChanged?.Invoke();

				return true;
			}

			return false;
		}

		public UWObject GetEquipped(UWArmorItemMap.BodySlot peSlot)
		{
			return EquipSlots[(int)peSlot];
		}

		/// <summary>
		/// The first carried item that matches, in the original's order (FindObjectInInventory_ovr120_21C,
		/// with its deepest mode as ammunition uses it): the eleven equipment slots in savegame
		/// order, then the backpack slots, then - slot by slot again - inside every container,
		/// depth first. So an arrow in a bag is found; before 2026-09-24 ours looked only at the
		/// backpack and the hands (per user: the original shoots from a bag).
		/// </summary>
		public UWObject FindCarried(Predicate<UWObject> pOMatch)
		{
			foreach (UWArmorItemMap.BodySlot leSlot in msSavegameSlotMap)
			{
				UWObject lOItem = EquipSlots[(int)leSlot];

				if (lOItem != null && pOMatch(lOItem))
					return lOItem;
			}

			foreach (UWObject lOItem in Backpack)
				if (lOItem != null && pOMatch(lOItem))
					return lOItem;

			foreach (UWArmorItemMap.BodySlot leSlot in msSavegameSlotMap)
			{
				UWObject lOFound = fFindInContents(EquipSlots[(int)leSlot], pOMatch);

				if (lOFound != null)
					return lOFound;
			}

			foreach (UWObject lOItem in Backpack)
			{
				UWObject lOFound = fFindInContents(lOItem, pOMatch);

				if (lOFound != null)
					return lOFound;
			}

			return null;
		}

		private static UWObject fFindInContents(UWObject pOContainer, Predicate<UWObject> pOMatch)
		{
			if (pOContainer == null || pOContainer.HasQuantity || pOContainer.Contents == null)
				return null;

			foreach (UWObject lOItem in pOContainer.Contents)
			{
				if (lOItem == null)
					continue;

				if (pOMatch(lOItem))
					return lOItem;

				UWObject lOFound = fFindInContents(lOItem, pOMatch);

				if (lOFound != null)
					return lOFound;
			}

			return null;
		}

		/// <summary>Takes the item out of whichever carried container holds it, at any depth.</summary>
		private bool fRemoveFromCarriedContents(UWObject pOItem)
		{
			foreach (UWObject lOItem in EquipSlots)
				if (fRemoveFromContents(lOItem, pOItem))
					return true;

			foreach (UWObject lOItem in Backpack)
				if (fRemoveFromContents(lOItem, pOItem))
					return true;

			return false;
		}

		private static bool fRemoveFromContents(UWObject pOContainer, UWObject pOItem)
		{
			if (pOContainer == null || pOContainer.HasQuantity || pOContainer.Contents == null)
				return false;

			if (pOContainer.Contents.Remove(pOItem))
				return true;

			foreach (UWObject lOInner in pOContainer.Contents)
				if (fRemoveFromContents(lOInner, pOItem))
					return true;

			return false;
		}

		public bool TryAddToBackpack(UWObject pOItem)
		{
			if (pOItem == null)
				return false;

			for (int i = 0; i < Backpack.Length; i++)
			{
				if (Backpack[i] == null)
				{
					Backpack[i] = pOItem;
					ExtinguishOutsideHand(pOItem);
					InventoryChanged?.Invoke();
					return true;
				}
			}

			return false;
		}

		public void RemoveFromBackpack(int piSlot)
		{
			if (piSlot < 0 || piSlot >= Backpack.Length || Backpack[piSlot] == null)
				return;

			Backpack[piSlot] = null;
			InventoryChanged?.Invoke();
		}

		/// <summary>Takes an equipment piece off the paperdoll without attaching it to the
		/// pointer - for everything that is CONSUMED instead of moved (see
		/// UWItemDrag.fConsumeItem).</summary>
		public void RemoveFromEquip(UWArmorItemMap.BodySlot peSlot)
		{
			if (EquipSlots[(int)peSlot] == null)
				return;

			EquipSlots[(int)peSlot] = null;
			InventoryChanged?.Invoke();
		}

		/// <summary>Takes a piece out of the open container without attaching it to the pointer.
		/// The place is the VISIBLE one, i.e. offset by the scroll offset - exactly as in
		/// BeginDragFromContainer.</summary>
		public void RemoveFromContainer(int piSlot)
		{
			List<UWObject> lOContents = OpenContainer?.Contents;
			int liIndex = ContainerScrollOffset + piSlot;

			if (lOContents == null || piSlot < 0 || liIndex < 0 || liIndex >= lOContents.Count)
				return;

			lOContents.RemoveAt(liIndex);
			fClampContainerScrollOffset();
			InventoryChanged?.Invoke();
		}

		/// <summary>Starts an original drag from the backpack: the item disappears
		/// IMMEDIATELY from the backpack place and is then in CursorItem (see there).</summary>
		public void BeginDragFromBackpack(int piSlot)
		{
			if (piSlot < 0 || piSlot >= Backpack.Length || Backpack[piSlot] == null)
				return;

			CursorItem = Backpack[piSlot];
			Backpack[piSlot] = null;
			InventoryChanged?.Invoke();
		}

		/// <summary>Starts an original drag from an equipment slot (taking off): the
		/// item disappears IMMEDIATELY from the paperdoll and is then in CursorItem.</summary>
		public void BeginDragFromEquip(UWArmorItemMap.BodySlot peSlot)
		{
			UWObject lOItem = EquipSlots[(int)peSlot];

			if (lOItem == null)
				return;

			CursorItem = lOItem;
			EquipSlots[(int)peSlot] = null;
			InventoryChanged?.Invoke();
		}

		/// <summary>
		/// Puts the item from the pointer onto a stack of the same kind (see CanStack).
		///
		/// The QUALITY is averaged in the process, weighted by the counts and rounded down.
		/// Read off the user's savegame (2026-09-05): two torches with 40 and 35 give
		/// a stack of two with 37 - that is 37.5 rounded down.
		/// </summary>
		public bool TryStackInto(UWObject pOTarget)
		{
			if (!CanStack(CursorItem, pOTarget, fData != null ? fData.CommonObjectProperties : null))
				return false;

			int liTargetCount = fGetCount(pOTarget);
			int liCursorCount = fGetCount(CursorItem);
			int liCount = liTargetCount + liCursorCount;

			if (liCount > 0)
			{
				pOTarget.Quality = (ushort)(((pOTarget.Quality * liTargetCount)
					+ (CursorItem.Quality * liCursorCount)) / liCount);
			}

			// A single piece without the quantity bit becomes a counted stack, as UW.EXE does.
			pOTarget.HasQuantity = true;
			pOTarget.Quantity = (ushort)liCount;
			UWCarriedSlots.OnMerged(CursorItem);
			CursorItem = null;

			InventoryChanged?.Invoke();

			return true;
		}

		/// <summary>The condition step zero to five, as the condition word selects it - the same
		/// calculation as in UWCombat.GetConditionStringIndex, only without the group offset.</summary>
		private static int fGetConditionStep(UWObject pOItem)
		{
			// The same calculation as for the condition word, only without the group offset - hence
			// quality type zero. The quality class cannot be looked up here; it
			// only changes whether a thing ALWAYS shows the top word, and then it does so for
			// both sides of the comparison equally.
			return UWCombat.GetConditionStringIndex(0, pOItem.Quality, 0,
				UWObjectMechanics.IsAnyLight(pOItem.ID));
		}

		/// <summary>
		/// Whether two items can be stacked, e.g. the thing at the pointer onto an existing stack.
		///
		/// The two match if they are the same object type, both carry a count
		/// AND their condition word is the same - differently worn things stay separate (checked by the
		/// user in the original, 2026-09-01). Enchanted pieces also stay on their
		/// own, otherwise a stack would waste the enchantment, and keys only stack if they open
		/// the same lock.
		/// </summary>
		public static bool CanStack(UWObject pOFirst, UWObject pOSecond, UWCommonObjectProperties pOProperties = null)
		{
			if (pOFirst == null || pOSecond == null)
				return false;

			if (pOFirst.ID != pOSecond.ID)
				return false;

			// Not the quality has to be equal, but its CONDITION WORD: what has the same name
			// can be stacked (per user on the original, 2026-09-05). Two torches with 40 and 35
			// are both "somewhat used" and go together; the stack then gets the
			// average, see TryStackInto.
			//
			// The six-step level is enough for the comparison - the group depends on the object number, and
			// that has already been checked as equal above.
			if (fGetConditionStep(pOFirst) != fGetConditionStep(pOSecond))
				return false;

			// A PIECE WITHOUT THE QUANTITY BIT stacks too if its kind is stackable (COMOBJ.DAT byte 3,
			// bit 6 clear, UWCommonObjectProperties.StartsWithQuantity - the reference checks the same bit
			// in uimanager_inventory) and it counts as one: two lockpicks, one of them without the bit,
			// stack in the original but did not in ours (per user, 2026-09-17). Without the table only
			// counted pieces stack, as before. A piece without the bit but with a value in the field
			// carries a link there (enchantment, contents) and never stacks.
			if (!fIsCountable(pOFirst, pOProperties) || !fIsCountable(pOSecond, pOProperties))
				return false;

			if (pOFirst.IsEnchanted || pOSecond.IsEnchanted)
				return false;

			// Keys to different locks do not stack (per user on the original,
			// 2026-09-13) - the lock ID is stored in the owner field (UWObjectMechanics.GetKeyId).
			// The reference does not check this.
			if (pOFirst.GetCategory() == UWObject.ObjectCategoryEnum.KeysLockpickLock
				&& UWObjectMechanics.GetKeyId(pOFirst) != UWObjectMechanics.GetKeyId(pOSecond))
				return false;

			// From 512 on the field is no longer a count but a special property.
			return fGetCount(pOFirst) + fGetCount(pOSecond) < 512;
		}

		/// <summary>Pieces in an item: its count with the quantity bit, otherwise one.</summary>
		private static int fGetCount(UWObject pOItem)
		{
			return pOItem.HasQuantity ? pOItem.Quantity : 1;
		}

		/// <summary>Whether an item may take part in a stack - see CanStack.</summary>
		private static bool fIsCountable(UWObject pOItem, UWCommonObjectProperties pOProperties)
		{
			if (pOItem.HasQuantity)
				return true;

			UWCommonObjectProperties.Entry lOEntry;

			return pOProperties != null && pOItem.Quantity == 0
				&& pOProperties.TryGet(pOItem.ID, out lOEntry) && lOEntry.StartsWithQuantity;
		}

		/// <summary>
		/// Combines two items if there is a rule for it - pole and strong
		/// thread into a fishing pole, three key parts into the Key of Infinity.
		///
		/// THE RULES ARE IN CMB.DAT and have long been read in (see
		/// UWObjectCombining); until now they were not used anywhere. In Underworld 1 there are
		/// nine:
		///
		///   lit torch + incense               -> burning incense        (torch stays)
		///   lit torch + corn                  -> popcorn                (torch stays)
		///   strong thread + pole              -> fishing pole
		///   any two of the three keys         -> a two-part key
		///   two-part + third key              -> Key of Infinity
		///
		/// WHICH ONE IS CONSUMED is given by the top bit of the respective entry. At least one
		/// of the two always is; the result takes its place (for stacks, and for when the result
		/// goes to the pointer, see the comments in the body).
		///
		/// If one of the two is KEPT, it is always the one you drag onto - otherwise
		/// you would end up with three items and only two places. For the two rules with
		/// the torch this is exactly right: the torch lies in the backpack, the incense is
		/// dropped onto it.
		/// </summary>
		public bool TryCombineInto(UWObject pOTarget)
		{
			if (CursorItem == null || pOTarget == null || fData == null
				|| fData.ObjectCombining == null)
				return false;

			int liResult;
			bool lbCursorConsumed;
			bool lbTargetConsumed;

			if (!fData.ObjectCombining.TryCombine(CursorItem.ID, pOTarget.ID,
				out liResult, out lbCursorConsumed, out lbTargetConsumed))
				return false;

			// A STACK GIVES UP ONLY ONE PIECE and stays in place afterwards - a handful of corn
			// does not become a single popcorn. The result therefore needs a place
			// that really becomes free, and that is only a consumed SINGLE PIECE. This is checked
			// before any change, so that nothing happens halfway.
			bool lbTargetIsStack = fIsStack(pOTarget);
			bool lbCursorIsStack = fIsStack(CursorItem);

			bool lbTargetFrees = lbTargetConsumed && !lbTargetIsStack;
			bool lbCursorFrees = lbCursorConsumed && !lbCursorIsStack;

			if (!lbTargetFrees && !lbCursorFrees)
				return false;

			if (lbCursorConsumed && lbCursorIsStack)
				CursorItem.Quantity--;

			if (lbTargetConsumed && lbTargetIsStack)
				pOTarget.Quantity--;

			// THE RESULT GOES TO THE POINTER when the pointer item is consumed as a single piece
			// (per user on the original, 2026-09-13). If the one in the slot is consumed as well,
			// the slot becomes free. Until then the result stayed in the slot - and
			// because the same object only got a new number, the display showed the old image.
			// Only if the pointer keeps something (remaining stack) does the result go into the slot.
			UWObject lOResultItem = lbCursorFrees ? CursorItem : pOTarget;

			if (lbCursorFrees && lbTargetFrees)
				RemoveItem(pOTarget);

			UWObjectMechanics.SetObjectId(lOResultItem, liResult,
				fData.Textures);

			// ASSUMPTION: the result is new and therefore unused. Where the original takes the
			// quality from is told neither by the data nor by the reference - which has no
			// combining rules at all. Without this line the fishing pole inherited the thread's condition.
			lOResultItem.Quality = UWObjectMechanics.FullLightQuality;

			InventoryChanged?.Invoke();

			return true;
		}

		/// <summary>More than one piece in a pile.</summary>
		private static bool fIsStack(UWObject pOItem)
		{
			return pOItem.HasQuantity && pOItem.Quantity > 1;
		}

		/// <summary>
		/// Drops CursorItem onto a backpack place. A matching combining rule (TryCombineInto) or
		/// stack (TryStackInto) takes precedence. Otherwise, if the place is occupied, they are swapped -
		/// the item lying there moves into CursorItem and the drag seamlessly continues with it
		/// (confirmed against the original), instead of ending the drag. Every backpack place accepts
		/// every item, so this always succeeds as long as anything hangs at the pointer at all.
		/// </summary>
		public bool DropCursorItemInBackpack(int piSlot)
		{
			if (CursorItem == null || piSlot < 0 || piSlot >= Backpack.Length)
				return false;

			// Combining rules first - only if none matches is the item stacked or
			// swapped (see TryCombineInto).
			if (TryCombineInto(Backpack[piSlot]))
				return true;

			// Like onto like makes a stack instead of a swap.
			if (TryStackInto(Backpack[piSlot]))
				return true;

			UWObject lODisplaced = Backpack[piSlot];
			Backpack[piSlot] = CursorItem;
			CursorItem = lODisplaced;
			ExtinguishOutsideHand(Backpack[piSlot]);
			InventoryChanged?.Invoke();
			return true;
		}

		/// <summary>
		/// Original: the container "opened" icon is itself a valid drop target during an original
		/// drag - puts CursorItem into the first free
		/// backpack place (no specific target place as in DropCursorItemInBackpack, hence
		/// also no swap when occupied - if the backpack is full, it simply fails and
		/// the item keeps sticking to the pointer). The container itself stays open (confirmed by
		/// the user - unlike a simple click without an item at the pointer, which
		/// closes it, see UWItemDrag.fUpdatePending).
		/// </summary>
		public bool DropCursorItemInFirstFreeBackpackSlot()
		{
			if (CursorItem == null)
				return false;

			// A MATCHING STACK IN THE PACK TAKES IT FIRST (same object, same quality - CanStack),
			// as the original does (per user, 2026-09-26: until then it went to a free place
			// beside the stack). A place of its own only when none matches.
			foreach (UWObject lOInPack in Backpack)
			{
				if (lOInPack != null && TryStackInto(lOInPack))
					return true;
			}

			int liFreeSlot = Array.IndexOf(Backpack, null);

			if (liFreeSlot < 0)
				return false;

			Backpack[liFreeSlot] = CursorItem;
			CursorItem = null;
			ExtinguishOutsideHand(Backpack[liFreeSlot]);
			InventoryChanged?.Invoke();
			return true;
		}

		/// <summary>
		/// Drops CursorItem into an equipment slot. Like onto like is stacked first (TryStackInto);
		/// otherwise only if the item type actually fits this slot
		/// per UWArmorItemMap (otherwise false, no change, the item
		/// keeps sticking to the pointer). If the slot is occupied, it is swapped as with the backpack instead of
		/// ending the drag.
		/// </summary>
		public bool DropCursorItemInEquip(UWArmorItemMap.BodySlot peSlot)
		{
			LastContainerRejection = UWContainerCapacity.ResultEnum.Fits;
			LastContainerRejectionContainer = null;

			// The rune bag in a hand or on a shoulder learns a stone as in the pack - the
			// original asks every inventory slot for the bag (see fPutIntoRuneBag).
			UWObject lOThere = EquipSlots[(int)peSlot];

			if (CursorItem != null && lOThere != null && lOThere.ID == UWObjectMechanics.RuneBagId)
				return fPutIntoRuneBag(lOThere);

			if (TryStackInto(EquipSlots[(int)peSlot]))
				return true;

			if (CursorItem == null || !UWArmorItemMap.Fits(CursorItem, peSlot))
				return false;

			UWObject lODisplaced = EquipSlots[(int)peSlot];
			EquipSlots[(int)peSlot] = CursorItem;
			CursorItem = lODisplaced;
			ExtinguishOutsideHand(EquipSlots[(int)peSlot]);
			InventoryChanged?.Invoke();
			return true;
		}

		/// <summary>Everything the character carries: backpack, equipment, and what lies in containers,
		/// as far as their contents are already resolved - for the conversation functions.
		/// </summary>
		public IEnumerable<UWObject> EnumerateAll()
		{
			foreach (UWObject lOItem in Backpack)
			{
				foreach (UWObject lOInner in fEnumerateWithContents(lOItem))
					yield return lOInner;
			}

			foreach (UWObject lOItem in EquipSlots)
			{
				foreach (UWObject lOInner in fEnumerateWithContents(lOItem))
					yield return lOInner;
			}
		}

		private static IEnumerable<UWObject> fEnumerateWithContents(UWObject pOItem)
		{
			if (pOItem == null)
				yield break;

			yield return pOItem;

			if (pOItem.Contents == null)
				yield break;

			foreach (UWObject lOChild in pOItem.Contents)
			{
				foreach (UWObject lOInner in fEnumerateWithContents(lOChild))
					yield return lOInner;
			}
		}

		/// <summary>Takes a thing out of the inventory wherever it lies - backpack, equipment
		/// or container. For give_ptr_npc (see UWConversationTrade).</summary>
		public bool RemoveItem(UWObject pOItem)
		{
			if (pOItem == null)
				return false;

			for (int liSlot = 0; liSlot < Backpack.Length; liSlot++)
			{
				if (Backpack[liSlot] == pOItem)
				{
					Backpack[liSlot] = null;
					InventoryChanged?.Invoke();
					return true;
				}
			}

			for (int liSlot = 0; liSlot < EquipSlots.Length; liSlot++)
			{
				if (EquipSlots[liSlot] == pOItem)
				{
					EquipSlots[liSlot] = null;
					InventoryChanged?.Invoke();
					return true;
				}
			}

			foreach (UWObject lOItem in EnumerateAll())
			{
				if (lOItem.Contents != null && lOItem.Contents.Remove(pOItem))
				{
					InventoryChanged?.Invoke();
					return true;
				}
			}

			return false;
		}

		/// <summary>Attaches a thing to the pointer that comes from elsewhere - from the trade area
		/// of a conversation (see UWConversationTrade). The caller has already taken it
		/// out of there.</summary>
		public void BeginDragFromExternal(UWObject pOItem)
		{
			if (pOItem == null || CursorItem != null)
				return;

			CursorItem = pOItem;
			InventoryChanged?.Invoke();
		}

		/// <summary>Takes the thing off the pointer and returns it - for the trade area. Null
		/// if nothing hangs there.</summary>
		public UWObject TakeCursorItem()
		{
			UWObject lOItem = CursorItem;

			CursorItem = null;

			if (lOItem != null)
				InventoryChanged?.Invoke();

			return lOItem;
		}

		/// <summary>A thing picked up from the world goes straight to the pointer. The host removes it
		/// from the world and then calls NotifyChanged.</summary>
		public void PickUpToCursor(UWObject pOItem)
		{
			if (pOItem == null)
				return;

			// ITS CHAIN COMES ALONG, resolved while the level's list is at hand - a wand's spell,
			// a poisoned potion's trap (see UWObjectMechanics.fFindInResolvedChain).
			if (!pOItem.HasQuantity && pOItem.Quantity != 0 && pOItem.Contents == null
				&& fLevel != null && fLevel.Masterlist != null)
				pOItem.EnsureContentsLoaded(fLevel.Masterlist);

			CursorItem = pOItem;
		}

		/// <summary>The host has placed the pointer item in the world: it leaves the pointer.</summary>
		public void ReleaseCursorItem()
		{
			if (CursorItem == null)
				return;

			CursorItem = null;
			InventoryChanged?.Invoke();
		}

		/// <summary>
		/// Cancels a drag for good (scheme change during dragging, or similar) -
		/// CursorItem returns to its known origin place (which at this point is
		/// guaranteed to still be empty, see BeginDragFrom*), or - if no origin is known
		/// (after a swap, see DropCursorItemIn*) - to the first free
		/// backpack place as a safety net.
		/// </summary>
		public void CancelCursorItem(int? piOriginBackpackSlot, UWArmorItemMap.BodySlot? peOriginEquipSlot)
		{
			if (CursorItem == null)
				return;

			UWObject lOItem = CursorItem;
			CursorItem = null;

			if (piOriginBackpackSlot.HasValue)
			{
				Backpack[piOriginBackpackSlot.Value] = lOItem;
			}
			else if (peOriginEquipSlot.HasValue)
			{
				EquipSlots[(int)peOriginEquipSlot.Value] = lOItem;
			}
			else
			{
				int liFreeSlot = Array.IndexOf(Backpack, null);

				if (liFreeSlot >= 0)
					Backpack[liFreeSlot] = lOItem;
				else
					CursorItem = lOItem; // Backpack full - better kept at the pointer than lost.
			}

			InventoryChanged?.Invoke();
		}

		/// <summary>
		/// Equips an item on a specific slot, regardless of where it comes from.
		/// Only checks whether item and slot match per UWArmorItemMap - does NOT care
		/// where an already equipped item goes (see pODisplaced) or whether the
		/// new item has to be removed from the backpack - the respective caller does that
		/// (see TryAutoEquip for the usual backpack case).
		/// </summary>
		public bool TryEquip(UWObject pOItem, UWArmorItemMap.BodySlot peSlot, out UWObject pODisplaced)
		{
			pODisplaced = null;

			if (pOItem == null || !UWArmorItemMap.Fits(pOItem, peSlot))
				return false;

			pODisplaced = EquipSlots[(int)peSlot];
			EquipSlots[(int)peSlot] = pOItem;
			InventoryChanged?.Invoke();

			return true;
		}

		/// <summary>
		/// Equip via click/auto: finds the matching slot itself, swaps any
		/// already equipped item into the same backpack place (keeps the UI's slot indices
		/// stable), or puts it into the first free backpack place if the item did not come from the backpack.
		/// </summary>
		public bool TryAutoEquip(UWObject pOItem)
		{
			if (pOItem == null || !UWArmorItemMap.TryGetPreferredSlot(pOItem, out UWArmorItemMap.BodySlot leSlot))
				return false;

			int liBackpackIndex = Array.IndexOf(Backpack, pOItem);

			// An item from outside the backpack would push the equipped one into the backpack; with
			// a full backpack that item used to be lost. Refuse instead. Not reachable today: the
			// only caller (UWInventoryUI, modern controls) passes backpack items, which swap in
			// place. The original controls swap the equipped item onto the pointer
			// (DropCursorItemInEquip).
			if (liBackpackIndex < 0 && EquipSlots[(int)leSlot] != null && Array.IndexOf(Backpack, null) < 0)
				return false;

			if (!TryEquip(pOItem, leSlot, out UWObject lODisplaced))
				return false;

			if (liBackpackIndex >= 0)
				Backpack[liBackpackIndex] = lODisplaced;
			else if (lODisplaced != null)
				TryAddToBackpack(lODisplaced);

			InventoryChanged?.Invoke();
			return true;
		}

		/// <summary>Puts an equipped item back into the backpack. Fails (the item
		/// stays equipped) if the backpack is full.</summary>
		public bool TryUnequip(UWArmorItemMap.BodySlot peSlot)
		{
			UWObject lOItem = EquipSlots[(int)peSlot];

			if (lOItem == null || !TryAddToBackpack(lOItem))
				return false;

			EquipSlots[(int)peSlot] = null;
			InventoryChanged?.Invoke();
			return true;
		}

		/// <summary>
		/// Opens a container (original: click on a Containers item in the backpack, or
		/// right-click use on a container in the world, see UWItemDrag/Interaction).
		/// If the same container is already open, it is closed instead (toggle). Resolves the
		/// contents on first opening via the current level masterlist (see
		/// UWObject.EnsureContentsLoaded) - only fails if no level is loaded at all
		/// or the item is not a container.
		/// </summary>
		public bool TryOpenContainer(UWObject pOContainer)
		{
			if (pOContainer == null || pOContainer.GetCategory() != UWObject.ObjectCategoryEnum.Containers)
				return false;

			if (OpenContainer == pOContainer)
			{
				CloseContainer();
				return true;
			}

			if (fLevel == null)
				return false;

			pOContainer.EnsureContentsLoaded(fLevel.Masterlist);

			// A CONTAINER INSIDE A CONTAINER keeps the one it was opened from, so that closing
			// goes back one level instead of all the way out (per user on the original,
			// 2026-09-17). Opening one that is already somewhere in the path - stepping back by
			// hand - cuts the path there rather than stacking it up again.
			int liInPath = mOContainerPath.IndexOf(pOContainer);

			if (liInPath >= 0)
				mOContainerPath.RemoveRange(liInPath, mOContainerPath.Count - liInPath);
			else if (OpenContainer != null && OpenContainer.Contents != null
				&& OpenContainer.Contents.Contains(pOContainer))
				mOContainerPath.Add(OpenContainer);
			else
				mOContainerPath.Clear();

			OpenContainer = pOContainer;
			ContainerScrollOffset = 0;
			InventoryChanged?.Invoke();
			return true;
		}

		/// <summary>The containers this one was opened from, outermost first - see
		/// TryOpenContainer.</summary>
		private readonly List<UWObject> mOContainerPath = new List<UWObject>();

		/// <summary>The container the open one was opened from, or null when it was opened from
		/// the pack - where closing goes back to, and where an item dropped on the container
		/// icon goes (see UWItemDrag).</summary>
		public UWObject ParentContainer
		{
			get { return mOContainerPath.Count > 0 ? mOContainerPath[mOContainerPath.Count - 1] : null; }
		}

		/// <summary>Closes the open container: back into the one it was opened from, or into the
		/// backpack when there is none.</summary>
		public void CloseContainer()
		{
			if (OpenContainer == null)
				return;

			if (mOContainerPath.Count > 0)
			{
				OpenContainer = mOContainerPath[mOContainerPath.Count - 1];
				mOContainerPath.RemoveAt(mOContainerPath.Count - 1);
			}
			else
			{
				OpenContainer = null;
			}

			ContainerScrollOffset = 0;
			InventoryChanged?.Invoke();
		}

		/// <summary>Out of every container at once - for a level change or a new game.</summary>
		public void CloseAllContainers()
		{
			mOContainerPath.Clear();

			if (OpenContainer == null)
				return;

			OpenContainer = null;
			ContainerScrollOffset = 0;
			InventoryChanged?.Invoke();
		}

		/// <summary>
		/// Original: dropping CursorItem directly onto a container item (backpack place)
		/// puts it into its contents instead of displacing the container itself (confirmed by the
		/// user) - the container does not need to be open/have been opened for this
		/// (resolves Contents on demand via the current level masterlist, like
		/// TryOpenContainer). Fails if nothing hangs at the pointer, the target is not a
		/// container, no level is loaded, or the container does not take the item
		/// (UWContainerCapacity, see LastContainerRejection).
		/// </summary>
		public bool DropCursorItemIntoContainerItem(UWObject pOContainer)
		{
			if (CursorItem == null || pOContainer == null
				|| pOContainer.GetCategory() != UWObject.ObjectCategoryEnum.Containers
				|| fLevel == null)
				return false;

			// THE RUNE BAG KEEPS NO OBJECTS: a stone becomes its rune (UWPlayerData.
			// TryLearnRuneStone) and is gone.
			if (pOContainer.ID == UWObjectMechanics.RuneBagId)
				return fPutIntoRuneBag(pOContainer);

			pOContainer.EnsureContentsLoaded(fLevel.Masterlist);

			if (!fContainerAccepts(pOContainer, CursorItem))
				return false;

			// Dropped onto a closed container, the item is stacked automatically if something
			// matching lies inside (checked by the user in the original, 2026-09-01).
			foreach (UWObject lOInside in pOContainer.Contents)
			{
				if (TryStackInto(lOInside))
					return true;
			}

			pOContainer.Contents.Add(CursorItem);
			CursorItem = null;
			ExtinguishOutsideHand(pOContainer.Contents[pOContainer.Contents.Count - 1]);
			InventoryChanged?.Invoke();
			return true;
		}

		/// <summary>The stone at the pointer goes into the rune bag: learned and gone, or refused
		/// with the rune bag's own message for anything that is not a rune stone. THE ORIGINAL
		/// ASKS EVERY INVENTORY SLOT for the bag - pack, hands, shoulders and the slots of an open
		/// container alike (per user on the original, 2026-09-26) -, so all three drops lead
		/// here: DropCursorItemIntoContainerItem, DropCursorItemInEquip, DropCursorItemInContainer.
		/// </summary>
		private bool fPutIntoRuneBag(UWObject pOBag)
		{
			UWPlayerData lOPlayer = fData != null ? fData.InitialPlayer : null;

			if (lOPlayer == null || !lOPlayer.TryLearnRuneStone(CursorItem.ID))
			{
				LastContainerRejection = UWContainerCapacity.ResultEnum.RunesOnly;
				LastContainerRejectionContainer = pOBag;

				return false;
			}

			LastContainerRejection = UWContainerCapacity.ResultEnum.Fits;
			LastContainerRejectionContainer = null;
			CursorItem = null;
			InventoryChanged?.Invoke();

			return true;
		}

		/// <summary>For UWGameUI/UWItemDrag: the item at a specific visible place in the
		/// currently open container (see OpenContainer), taking
		/// ContainerScrollOffset into account - or null if nothing lies there (yet).</summary>
		public UWObject GetContainerItem(int piSlot)
		{
			List<UWObject> lOContents = OpenContainer?.Contents;
			int liIndex = ContainerScrollOffset + piSlot;

			if (lOContents == null || piSlot < 0 || liIndex < 0 || liIndex >= lOContents.Count)
				return null;

			return lOContents[liIndex];
		}

		/// <summary>Starts an original drag from the open container: the item
		/// disappears IMMEDIATELY from its Contents list and is then in CursorItem - exactly
		/// the same behaviour as with backpack/equipment/world, no exception (confirmed by the
		/// user). The list is compact (no gaps) - unlike the backpack, following
		/// places move up one position on removal (hence, after
		/// removal, clamp the scroll offset back if the window end then
		/// lies beyond it).</summary>
		public void BeginDragFromContainer(int piSlot)
		{
			List<UWObject> lOContents = OpenContainer?.Contents;
			int liIndex = ContainerScrollOffset + piSlot;

			if (lOContents == null || piSlot < 0 || liIndex < 0 || liIndex >= lOContents.Count)
				return;

			CursorItem = lOContents[liIndex];
			lOContents.RemoveAt(liIndex);
			fClampContainerScrollOffset();
			InventoryChanged?.Invoke();
		}

		/// <summary>
		/// Drops CursorItem at a visible place in the open container (taking
		/// ContainerScrollOffset into account) - if the click hits an already
		/// occupied place, it is combined, stacked or swapped (as with the backpack); if it hits a free place
		/// (even in the middle of the current page, if it is not quite full), it is appended to the end
		/// of the list. Type and weight are checked first (UWContainerCapacity) - combining is
		/// exempt from it, because it produces no additional object.
		/// </summary>
		public bool DropCursorItemInContainer(int piSlot)
		{
			List<UWObject> lOContents = OpenContainer?.Contents;

			if (CursorItem == null || lOContents == null || piSlot < 0 || piSlot >= ContainerSlotCount)
				return false;

			int liIndex = ContainerScrollOffset + piSlot;

			// A CONTAINER LYING IN THE OPEN ONE takes the item, as one in the backpack does
			// (DropCursorItemIntoContainerItem) - the rune bag learns a stone there too (per user
			// on the original, 2026-09-26). Until 2026-09-28 every other container swapped places
			// with the item instead (per user: the ingredients would not go into the bowl lying in
			// a sack).
			if (liIndex < lOContents.Count && lOContents[liIndex] != null
				&& lOContents[liIndex].GetCategory() == UWObject.ObjectCategoryEnum.Containers)
				return DropCursorItemIntoContainerItem(lOContents[liIndex]);

			if (liIndex < lOContents.Count)
			{
				// Combining works in the container window just as in the backpack. It turns two
				// objects into one and never makes the contents heavier, so it goes before the
				// capacity test.
				if (TryCombineInto(lOContents[liIndex]))
					return true;

				// A SWAP takes the item lying there out again, so its weight does not count -
				// but stacking leaves it inside, so there it does.
				bool lbStacks = CanStack(CursorItem, lOContents[liIndex],
					fData != null ? fData.CommonObjectProperties : null);

				if (!fContainerAccepts(OpenContainer, CursorItem, lbStacks ? null : lOContents[liIndex]))
					return false;

				if (TryStackInto(lOContents[liIndex]))
					return true;

				UWObject lODisplaced = lOContents[liIndex];
				lOContents[liIndex] = CursorItem;
				CursorItem = lODisplaced;
			}
			else
			{
				if (!fContainerAccepts(OpenContainer, CursorItem))
					return false;

				lOContents.Add(CursorItem);
				CursorItem = null;
			}

			ExtinguishOutsideHand(lOContents[liIndex >= 0 && liIndex < lOContents.Count ? liIndex : lOContents.Count - 1]);
			InventoryChanged?.Invoke();
			return true;
		}

		/// <summary>For the DownArrow (decreases ContainerScrollOffset, see
		/// UWItemDrag/ScrollContainer) - named after the direction of the offset, not after the
		/// arrow itself, to avoid exactly the confusion caused by the properties originally
		/// named "CanScrollContainerUp/Down" (the UpArrow
		/// INCREASES the offset, see ScrollContainer - so "Up" referred to neither of the two
		/// here).</summary>
		public bool CanDecreaseContainerScrollOffset => ContainerScrollOffset > 0;

		/// <summary>For the UpArrow (increases ContainerScrollOffset). Original special case (confirmed by
		/// the user): if the currently visible contents fill the window EXACTLY (e.g.
		/// exactly 8 items with ContainerSlotCount=8), you can still scroll one more
		/// time - the result is then a completely empty row. Hence "&lt;=" instead of "&lt;":
		/// on an exact match (offset+ContainerSlotCount==count) the arrow stays active for
		/// ONE more step, only after that (no content left, not even partially)
		/// does it disappear.</summary>
		public bool CanIncreaseContainerScrollOffset
		{
			get
			{
				List<UWObject> lOContents = OpenContainer?.Contents;
				return lOContents != null && ContainerScrollOffset + ContainerSlotCount <= lOContents.Count;
			}
		}

		/// <summary>Original: the scroll arrows on the container panel (BUTTONS.GR 27/28, see
		/// UWGameUI.Init) page the 8-slot window by piDelta places - clamped to the
		/// valid range, so an attempt beyond the end/start simply has no
		/// effect instead of being an error.</summary>
		public void ScrollContainer(int piDelta)
		{
			if (OpenContainer == null)
				return;

			int liOldOffset = ContainerScrollOffset;
			ContainerScrollOffset += piDelta;
			fClampContainerScrollOffset();

			if (ContainerScrollOffset != liOldOffset)
				InventoryChanged?.Invoke();
		}

		/// <summary>
		/// Only a safety net against negative/far out-of-range values - the
		/// actual limit of how far scrolling can go is already enforced by
		/// CanIncreaseContainerScrollOffset (the UpArrow disappears as soon as no new
		/// content would become visible). Hence deliberately clamped GENEROUSLY (last valid
		/// item index, not "count minus window size") - with the tighter formula a
		/// single row step (see ScrollContainer) would wrongly be cut off early and a
		/// partially empty last window (confirmed by the user, see ScrollContainer) would
		/// not be reachable at all.
		/// </summary>
		private void fClampContainerScrollOffset()
		{
			List<UWObject> lOContents = OpenContainer?.Contents;
			int liMaxOffset = lOContents == null ? 0 : Math.Max(0, lOContents.Count - 1);
			ContainerScrollOffset = Math.Max(0, Math.Min(liMaxOffset, ContainerScrollOffset));
		}
	}
}
