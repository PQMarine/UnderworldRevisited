using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT A CLASSIC PRESS HIT AND A DRAG TAKES FROM (cleanup step 5.15, 2026-10-22): one value
	/// instead of the nine fields UWItemDrag reset in five places, each time a different subset of
	/// them - a paperdoll slot, a backpack place, a place of the open container, a trade slot of
	/// either side, the world thing, the container icon, a scroll arrow and the trade click waiting
	/// for its release. One source at a time: a new press replaces it, and so does every end.
	///
	/// What a source holds is read (Peek), taken onto the pointer (TakePart, TakeWhole), put back
	/// (Return) or used up (Consume) here, against the inventory model and the running trade - one
	/// order for all of them, which UWItemDrag used to write out four times. The world thing is the
	/// host's: its picture is an engine object (Entity, opaque here), and taking it goes through
	/// the world (UWWorldPickup).
	/// </summary>
	public readonly struct UWDragSource
	{
		public enum KindEnum
		{
			None,

			/// <summary>A backpack place (Index).</summary>
			Backpack,

			/// <summary>A visible place of the open container (Index) - its panel lies exactly on
			/// top of the backpack.</summary>
			Container,

			/// <summary>A paperdoll slot (Slot) - the EMPTY weapon hand counts too, unarmed combat
			/// starts there.</summary>
			Equip,

			/// <summary>A slot of the player's trade area in a conversation (Index, see
			/// UWConversationTrade).</summary>
			PlayerTrade,

			/// <summary>A bought good on the partner's side (Index, see UWConversationTrade.NpcBought).
			/// Dragged and asked "Move how many?" like the player's own trade slots (per user on the
			/// original, 2026-09-29).</summary>
			NpcTrade,

			/// <summary>A thing in the 3D world (Entity, the host's picture of it).</summary>
			World,

			/// <summary>The open container's icon: a click closes it, there is nothing to drag.</summary>
			ContainerIcon,

			/// <summary>A scroll arrow of the open container (Index is the scroll step): a click
			/// scrolls, there is nothing to drag.</summary>
			ScrollArrow,

			/// <summary>A press on the trade areas that acts on its release (Click) - the cross, the
			/// mark or the look - as everything does during a conversation.</summary>
			TradeClick
		}

		public readonly KindEnum Kind;

		/// <summary>The backpack place, the container place, the trade slot, or the scroll step.</summary>
		public readonly int Index;

		public readonly UWArmorItemMap.BodySlot Slot;

		/// <summary>The world thing's picture (World) - the host's object, opaque here.</summary>
		public readonly object Entity;

		/// <summary>What the press does on its release (TradeClick).</summary>
		public readonly Action Click;

		private UWDragSource(KindEnum peKind, int piIndex, UWArmorItemMap.BodySlot peSlot, object pOEntity, Action pAClick)
		{
			Kind = peKind;
			Index = piIndex;
			Slot = peSlot;
			Entity = pOEntity;
			Click = pAClick;
		}

		public static UWDragSource None => new UWDragSource(KindEnum.None, -1, default, null, null);

		public static UWDragSource Backpack(int piSlot) => new UWDragSource(KindEnum.Backpack, piSlot, default, null, null);

		public static UWDragSource Container(int piSlot) => new UWDragSource(KindEnum.Container, piSlot, default, null, null);

		public static UWDragSource Equip(UWArmorItemMap.BodySlot peSlot) => new UWDragSource(KindEnum.Equip, -1, peSlot, null, null);

		public static UWDragSource PlayerTrade(int piSlot) => new UWDragSource(KindEnum.PlayerTrade, piSlot, default, null, null);

		public static UWDragSource NpcTrade(int piSlot) => new UWDragSource(KindEnum.NpcTrade, piSlot, default, null, null);

		public static UWDragSource World(object pOEntity) => new UWDragSource(KindEnum.World, -1, default, pOEntity, null);

		public static UWDragSource ContainerIcon => new UWDragSource(KindEnum.ContainerIcon, -1, default, null, null);

		public static UWDragSource ScrollArrow(int piDelta) => new UWDragSource(KindEnum.ScrollArrow, piDelta, default, null, null);

		public static UWDragSource TradeClick(Action pAClick) => new UWDragSource(KindEnum.TradeClick, -1, default, null, pAClick);

		/// <summary>A thing lies at the source - not the container icon, a scroll arrow, a trade
		/// click or nothing.</summary>
		public bool HoldsThing => Kind >= KindEnum.Backpack && Kind <= KindEnum.World;

		/// <summary>A slot of either side of the trade.</summary>
		public bool IsTrade => Kind == KindEnum.PlayerTrade || Kind == KindEnum.NpcTrade;

		/// <summary>This paperdoll slot.</summary>
		public bool IsEquip(UWArmorItemMap.BodySlot peSlot)
		{
			return Kind == KindEnum.Equip && Slot == peSlot;
		}

		/// <summary>
		/// The thing at the source, still in its place - null for nothing, an empty slot, or the
		/// world (whose thing the host reads from its picture).
		/// </summary>
		public UWObject Peek(UWInventoryModel pOModel, UWConversationTrade pOTrade)
		{
			switch (Kind)
			{
				case KindEnum.PlayerTrade:
					return pOTrade != null ? pOTrade.PlayerItems[Index] : null;

				case KindEnum.NpcTrade:
					return pOTrade != null ? pOTrade.NpcItems[Index] : null;

				case KindEnum.Container:
					return pOModel.GetContainerItem(Index);

				case KindEnum.Backpack:
					return pOModel.Backpack[Index];

				case KindEnum.Equip:
					return pOModel.GetEquipped(Slot);

				default:
					return null;
			}
		}

		/// <summary>
		/// Part of a stack onto the pointer: piCount pieces are counted off, the stack stays where
		/// it is (UWInventoryModel.TakeFromStack), and the trade hears of its slot. False when the
		/// stack does not split so - then the caller takes it whole.
		/// </summary>
		public bool TakePart(UWInventoryModel pOModel, UWConversationTrade pOTrade, int piCount)
		{
			if (!pOModel.TakeFromStack(Peek(pOModel, pOTrade), piCount))
				return false;

			if (Kind == KindEnum.NpcTrade && pOTrade != null)
				pOTrade.NpcSlotChanged(Index);
			else if (Kind == KindEnum.PlayerTrade && pOTrade != null)
				pOTrade.PlayerSlotPartTaken(Index);

			return true;
		}

		/// <summary>The whole thing at the source onto the pointer - it leaves its place at once.</summary>
		public void TakeWhole(UWInventoryModel pOModel, UWConversationTrade pOTrade)
		{
			switch (Kind)
			{
				case KindEnum.PlayerTrade:
					if (pOTrade != null)
						pOModel.BeginDragFromExternal(pOTrade.TakeFromPlayerSlot(Index));

					break;

				case KindEnum.NpcTrade:
					if (pOTrade != null)
						pOModel.BeginDragFromExternal(pOTrade.TakeFromNpcSlot(Index));

					break;

				case KindEnum.Container:
					pOModel.BeginDragFromContainer(Index);
					break;

				case KindEnum.Backpack:
					pOModel.BeginDragFromBackpack(Index);
					break;

				case KindEnum.Equip:
					pOModel.BeginDragFromEquip(Slot);
					break;
			}
		}

		/// <summary>Where the thing on the pointer goes back to when the classic drag is cancelled:
		/// its backpack place or paperdoll slot; from anywhere else (a container, the world, the
		/// trade) the backpack (UWInventoryModel.ReturnEnum.AsTaken).</summary>
		public UWCursorOrigin Origin
		{
			get
			{
				if (Kind == KindEnum.Backpack)
					return UWCursorOrigin.Backpack(Index);

				return Kind == KindEnum.Equip ? UWCursorOrigin.Equip(Slot) : UWCursorOrigin.None;
			}
		}

		/// <summary>
		/// The classic drag's cancel puts the thing on the pointer back. Dragged from the trade area:
		/// back there, as long as the slot is still free; taken from the partner's side: back onto
		/// its place there. Then the model's way back as taken (UWInventoryModel.ReturnCursorItem) -
		/// a no-op if the thing was already placed.
		/// </summary>
		public void Return(UWInventoryModel pOModel, UWConversationTrade pOTrade)
		{
			if (Kind == KindEnum.PlayerTrade && pOTrade != null && pOModel.CursorItem != null
				&& pOTrade.PlayerItems[Index] == null)
				pOTrade.PutInPlayerSlot(Index, pOModel.TakeCursorItem());

			if (Kind == KindEnum.NpcTrade && pOTrade != null && pOModel.CursorItem != null)
				pOTrade.ReturnToNpcSlot(Index, pOModel.TakeCursorItem());

			pOModel.ReturnCursorItem(Origin, UWInventoryModel.ReturnEnum.AsTaken);
		}

		/// <summary>
		/// Removes the consumed piece from the game - for a stack only one of them.
		///
		/// Where it came from is this source, the same one Peek fetched it from - backpack, open
		/// container or paperdoll (a world source never gets here: UWItemDrag.TryBeginWorldDrag
		/// takes the thing at once and never leaves a pending click). The order is the same as
		/// there, so that both refer to the same piece.
		/// </summary>
		public void Consume(UWInventoryModel pOModel, UWObject pOItem)
		{
			// ONLY DECREMENT A REAL STACK. On an enchanted piece the same field carries the
			// enchantment id from 512 on - counting it down would silently change its spell
			// (UWStackRules).
			if (UWStackRules.TryTakeOne(pOItem))
				return;

			if (Kind == KindEnum.Container)
				pOModel.RemoveFromContainer(Index);
			else if (Kind == KindEnum.Backpack)
				pOModel.RemoveFromBackpack(Index);
			else if (Kind == KindEnum.Equip)
				pOModel.RemoveFromEquip(Slot);
		}
	}
}
