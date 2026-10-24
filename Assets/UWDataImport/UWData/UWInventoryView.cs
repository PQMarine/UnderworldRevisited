namespace UWDataImport.UWData
{
	/// <summary>
	/// THE INVENTORY PAGE AS BOTH SCHEMES SHOW IT (cleanup step 5.8, 2026-10-20): what the eight
	/// slots, the hands and rings, the open container's picture, its scroll arrows and the
	/// weight circle show, and what a click on the page's controls and slots does. Until then the
	/// classic frame (UWHudInventory) and the modern original page (UWModernInventoryPage) each
	/// read UWInventoryModel themselves - the slot loops, the stack count, the arrows' condition,
	/// the open picture and the remaining weight written in both - and the classic drag
	/// (UWItemDrag) and the modern bags (UWModernBags) each wrote the container icon's "one
	/// level up" and the drop onto a backpack slot holding a container.
	///
	/// A renderer keeps its own instance and calls Refresh once a frame before it draws; it
	/// draws what the view says and turns screen points into the page's spots
	/// (UWClickRules.InventorySpotAt). Release-based input, the frame guards, the drag itself and
	/// where a click is allowed at all (the panel turned away, the map open) stay in the Unity
	/// controllers.
	/// </summary>
	public sealed class UWInventoryView
	{
		/// <summary>The page's eight slots - the backpack's places, or the open container's window
		/// on its contents (UWInventoryModel.ContainerSlotCount, the same grid).</summary>
		public const int SlotCount = UWInventoryModel.BackpackSize;

		/// <summary>
		/// A CLICK ON A SCROLL ARROW PAGES ONE WHOLE ROW (confirmed by the user): four places, the
		/// columns of the grid. The up arrow (the left one, UWClickRules.InventorySpot.ScrollLeft)
		/// moves the whole content up - the second row becomes the first and the ones further back
		/// in the container come into view - so it RAISES the offset; the down arrow lowers it.
		/// </summary>
		public const int ScrollStep = UWClassicLayout.Backpack.Columns;

		/// <summary>One slot as it shows: the thing, its picture (its icon, else its world
		/// picture) and its count.</summary>
		public struct Slot
		{
			public UWObject Item;

			public UWTexture Picture;

			/// <summary>The pieces (UWStackRules.CountAtLeastOne), 0 for an empty slot.</summary>
			public int Count;

			/// <summary>Single things get no number - only what is there more than once.</summary>
			public bool ShowsCount
			{
				get { return Count > 1; }
			}
		}

		/// <summary>The eight backpack places, also while a container covers them.</summary>
		public readonly Slot[] Backpack = new Slot[SlotCount];

		/// <summary>The open container's eight places from the scroll offset on; empty while none
		/// is open.</summary>
		public readonly Slot[] Container = new Slot[SlotCount];

		/// <summary>The rings and the four hand places, in the order of
		/// UWPaperdollLayout.IconSlots - a stack can lie there too, such as torches in the hand.</summary>
		public readonly Slot[] Hands = new Slot[UWPaperdollLayout.IconSlots.Length];

		/// <summary>The open container (UWInventoryModel.OpenContainer), null for the backpack.</summary>
		public UWObject OpenContainer { get; private set; }

		/// <summary>Whether the page shows an open container's contents instead of the backpack.</summary>
		public bool ShowsContainer
		{
			get { return OpenContainer != null; }
		}

		/// <summary>The picture above the open container's slots - its open variant
		/// (UWObjectAppearance.OpenContainerPicture), null when none is open or the picture is
		/// missing. It is also the button that closes the container.</summary>
		public UWTexture ContainerPicture { get; private set; }

		/// <summary>
		/// The arrows show only while there is something to page to in their direction. The up
		/// arrow raises the offset, so it shows while more content lies behind the window - also
		/// for one step after a window filled exactly, which then shows an empty row
		/// (UWInventoryModel.CanIncreaseContainerScrollOffset); the down arrow while the offset is
		/// above 0. Containers with more than eight things come from the original's level state.
		/// </summary>
		public bool ShowsScrollUp { get; private set; }

		public bool ShowsScrollDown { get; private set; }

		/// <summary>What the circle below the paper doll shows: the carrying capacity left in whole
		/// stones, never below 0 (UWInventoryWeight.RemainingStones) - computed from the inventory,
		/// so the number follows every move.</summary>
		public int RemainingStones { get; private set; }

		/// <summary>The slot of the page as it shows: the container's while one is open, else the
		/// backpack's.</summary>
		public Slot PageSlot(int piSlot)
		{
			return ShowsContainer ? Container[piSlot] : Backpack[piSlot];
		}

		private UWObject mOPictureFor;

		private UWTextures mOPictureTextures;

		/// <summary>Reads the model; pOData gives the pictures, the player's capacity and the
		/// masses. Nothing without a model.</summary>
		public void Refresh(UWInventoryModel pOModel, DataImport pOData)
		{
			UWObject lOOpen = pOModel != null ? pOModel.OpenContainer : null;

			OpenContainer = lOOpen;

			UWTextures lOTextures = pOData != null ? pOData.Textures : null;

			for (int liSlot = 0; liSlot < SlotCount; liSlot++)
			{
				Backpack[liSlot] = fSlot(pOModel != null && liSlot < pOModel.Backpack.Length ? pOModel.Backpack[liSlot] : null, pOModel, lOTextures);
				Container[liSlot] = fSlot(lOOpen != null ? pOModel.GetContainerItem(liSlot) : null, pOModel, lOTextures);
			}

			for (int liSlot = 0; liSlot < Hands.Length; liSlot++)
				Hands[liSlot] = fSlot(pOModel != null ? pOModel.GetEquipped(UWPaperdollLayout.IconSlots[liSlot].Slot) : null, pOModel, lOTextures);

			// The picture is looked up again only when the container or the pictures change.

			if (lOOpen != mOPictureFor || lOTextures != mOPictureTextures)
			{
				mOPictureFor = lOOpen;
				mOPictureTextures = lOTextures;
				ContainerPicture = UWObjectAppearance.OpenContainerPicture(lOTextures, lOOpen);
			}

			ShowsScrollUp = lOOpen != null && pOModel.CanIncreaseContainerScrollOffset;
			ShowsScrollDown = lOOpen != null && pOModel.CanDecreaseContainerScrollOffset;

			RemainingStones = pOModel != null && pOData != null
				? UWInventoryWeight.RemainingStones(pOData.InitialPlayer, pOModel.GetCarriedTenthStones(pOData.CommonObjectProperties))
				: 0;
		}

		/// <summary>A slot's thing with its picture - an open container's open one, wherever it lies
		/// (UWInventoryModel.IsContainerOpen: a bag opened from the paperdoll shows open there).</summary>
		private static Slot fSlot(UWObject pOItem, UWInventoryModel pOModel, UWTextures pOTextures)
		{
			UWTexture lOPicture = pOItem != null ? pOItem.Icon ?? pOItem.Texture : null;

			if (pOModel != null && pOModel.IsContainerOpen(pOItem))
				lOPicture = UWObjectAppearance.OpenContainerPicture(pOTextures, pOItem) ?? lOPicture;

			return new Slot
			{
				Item = pOItem,
				Picture = lOPicture,
				Count = pOItem != null ? UWStackRules.CountAtLeastOne(pOItem) : 0
			};
		}

		// ------------------------------------------------- What a click does

		/// <summary>The page's controls above the slots, shown only with a container open.</summary>
		public enum ControlEnum
		{
			None,
			ScrollUp,
			ScrollDown,
			ContainerIcon
		}

		/// <summary>The control a spot of the page stands for. Whether it shows - and so takes the
		/// click - is the renderer's (ShowsScrollUp, ShowsScrollDown, ContainerPicture).</summary>
		public static ControlEnum ControlOf(UWClickRules.InventorySpot peSpot)
		{
			switch (peSpot)
			{
				case UWClickRules.InventorySpot.ScrollLeft: return ControlEnum.ScrollUp;
				case UWClickRules.InventorySpot.ScrollRight: return ControlEnum.ScrollDown;
				case UWClickRules.InventorySpot.ContainerIcon: return ControlEnum.ContainerIcon;
				default: return ControlEnum.None;
			}
		}

		/// <summary>How far a click on an arrow moves the container's offset (ScrollStep up, minus
		/// it down), 0 for anything else. The arrows also work with a thing on the pointer: they
		/// only scroll, the thing stays on it.</summary>
		public static int ScrollDeltaOf(ControlEnum peControl)
		{
			switch (peControl)
			{
				case ControlEnum.ScrollUp: return ScrollStep;
				case ControlEnum.ScrollDown: return -ScrollStep;
				default: return 0;
			}
		}

		/// <summary>How putting a thing down on the page went.</summary>
		public enum PutOutcomeEnum
		{
			Placed,

			/// <summary>A container refused it: the reason is UWInventoryModel.LastContainerRejection
			/// (UWItemMessages.ContainerRejection).</summary>
			Refused,

			/// <summary>No place took it (for the container icon, NoPlaceMessage).</summary>
			NoPlace
		}

		/// <summary>"There is no place to put that." - general message 259, ItemFitsSlot's 0x102
		/// (original wording, confirmed by the user), when the container icon finds no free
		/// backpack place.</summary>
		public const int NoPlaceMessage = UWStringIds.NoPlaceToPutMessage;

		/// <summary>
		/// A THING DROPPED ON THE OPEN CONTAINER'S ICON goes ONE LEVEL UP, as closing goes: from a
		/// container opened inside another into that one (per user, 2026-09-30), taking it like a
		/// drop onto the closed container with the same refusals; from one opened in the pack into
		/// the first free backpack place (UWInventoryModel.DropCursorItemInFirstFreeBackpackSlot -
		/// a full pack refuses it, the thing stays on the pointer). The container stays open (per
		/// user - unlike a click without a thing, which closes it).
		/// </summary>
		public static PutOutcomeEnum PutOneLevelUp(UWInventoryModel pOModel)
		{
			UWObject lOParent = pOModel.ParentContainer;

			if (lOParent != null)
				return pOModel.DropCursorItemIntoContainerItem(lOParent) ? PutOutcomeEnum.Placed : PutOutcomeEnum.Refused;

			return pOModel.DropCursorItemInFirstFreeBackpackSlot() ? PutOutcomeEnum.Placed : PutOutcomeEnum.NoPlace;
		}

		/// <summary>
		/// A THING DROPPED ON A BACKPACK PLACE: a container lying there takes it into its contents
		/// instead of being displaced (confirmed by the user) - it need not be open; anything else
		/// is combined, stacked or swapped (UWInventoryModel.DropCursorItemInBackpack, which takes
		/// every thing as long as one hangs on the pointer).
		/// </summary>
		public static PutOutcomeEnum DropOnBackpackSlot(UWInventoryModel pOModel, int piSlot)
		{
			UWObject lOThere = piSlot >= 0 && piSlot < pOModel.Backpack.Length ? pOModel.Backpack[piSlot] : null;

			if (lOThere != null && lOThere.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
				return pOModel.DropCursorItemIntoContainerItem(lOThere) ? PutOutcomeEnum.Placed : PutOutcomeEnum.Refused;

			return pOModel.DropCursorItemInBackpack(piSlot) ? PutOutcomeEnum.Placed : PutOutcomeEnum.NoPlace;
		}
	}
}
