using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE CLASSIC DRAG ON A CONVERSATION'S TRADE (cleanup step 5.15, 2026-10-22: out of UWItemDrag):
/// the trade areas as sources of a press (TryPress), the player's own area as a place to put
/// things (TryPlace), and what the parchment shows instead of the message box behind it - the
/// barter refusals and the look. The trade itself is UWConversationTrade; UWDragSource takes from
/// and returns to its slots.
/// </summary>
public sealed class UWClassicTradeDrag
{
    /// <summary>What a drop on the trade areas did (TryPlace).</summary>
    public enum PlaceEnum
    {
        /// <summary>Not on a trade area, or no trade running - the other places decide.</summary>
        NotHere,

        /// <summary>The slot took it (maybe swapping another thing onto the pointer).</summary>
        Placed,

        /// <summary>On a trade area, but it found no place there - the thing stays on the pointer.</summary>
        Refused
    }

    private readonly Component mOOwner;

    /// <summary>Looked up on the first use of Trade, as before (see Screen).</summary>
    private UWConversationController mOConversationScreen;

    public UWClassicTradeDrag(Component pOOwner)
    {
        mOOwner = pOOwner;
    }

    /// <summary>The conversation screen once Trade has looked it up - null before that, as the
    /// inline fields of UWItemDrag were (a press asks Trade before it acts).</summary>
    public UWConversationController Screen => mOConversationScreen;

    /// <summary>The conversation is open (known once Trade has looked the screen up).</summary>
    public bool IsConversationOpen => mOConversationScreen != null && mOConversationScreen.IsOpen;

    /// <summary>The left click that cuts a look text short in a conversation is used up - no
    /// answer, no cross, no slot (per user on the original, 2026-09-18).</summary>
    public bool ConsumesLeftClick => IsConversationOpen && mOConversationScreen.ConsumesLeftClick;

    /// <summary>The trade of the running conversation, otherwise null.</summary>
    public UWConversationTrade Trade
    {
        get
        {
            if (mOConversationScreen == null)
                mOConversationScreen = mOOwner.GetComponent<UWConversationController>();

            if (mOConversationScreen == null || !mOConversationScreen.IsOpen || mOConversationScreen.Session == null)
                return null;

            return mOConversationScreen.Session.Trade;
        }
    }

    /// <summary>
    /// A press on the trade areas of the conversation. Returns true if the press is handled
    /// there - the action itself waits in pOSource (UWDragSource.TradeClick) for the release, as
    /// everything does during a conversation (per user on the original, 2026-09-18; until then the
    /// cross and the look acted on the press); a press on an empty slot of the player's own area
    /// is handled with nothing (UWDragSource.None). Instead returns the trade slot as pOSource if
    /// a drag from the player's own area or of a bought good begins (then it continues like any
    /// other source).
    ///
    /// UseTradeSlot_ovr097_6E8 (BARTER.C): the slot picks up the item (a left drag), marks it (a
    /// left click) or describes it (right); the partner's items can only be marked and looked at
    /// until they are bought. The crosses beside the slots are only marks (see
    /// UWConversationController.GetPlayerTradeSlotAt).
    /// </summary>
    public bool TryPress(bool pbRightButton, Vector2 pOMousePos, out UWDragSource pOSource)
    {
        pOSource = UWDragSource.None;

        UWConversationTrade lOTrade = Trade;

        if (lOTrade == null)
            return false;

        int liSlot = mOConversationScreen.GetPlayerTradeSlotAt(pOMousePos);

        if (liSlot >= 0)
        {
            int liChosen = liSlot;

            if (lOTrade.PlayerItems[liSlot] == null)
                return true;

            if (pbRightButton)
            {
                pOSource = UWDragSource.TradeClick(() => mOConversationScreen.Session.LookAtOwnTradeItem(lOTrade.PlayerItems[liChosen]));
                return true;
            }

            pOSource = UWDragSource.PlayerTrade(liSlot);
            return false;
        }

        liSlot = mOConversationScreen.GetNpcTradeSlotAt(pOMousePos);

        if (liSlot < 0)
            return false;

        int liNpcSlot = liSlot;

        if (pbRightButton && lOTrade.NpcItems[liSlot] != null)
            pOSource = UWDragSource.TradeClick(() => mOConversationScreen.Session.LookAtPartnerTradeItem(lOTrade.NpcItems[liNpcSlot]));
        else if (lOTrade.NpcGoodsBought && lOTrade.NpcBought[liSlot] && lOTrade.NpcItems[liSlot] != null)
        {
            // A bought good: a source like any other - dragged, or taken by a click.
            pOSource = UWDragSource.NpcTrade(liSlot);
            return false;
        }
        else
            pOSource = UWDragSource.TradeClick(() => lOTrade.ToggleNpcSelected(liNpcSlot));

        return true;
    }

    /// <summary>
    /// The player's own trade area in a conversation takes the item from the pointer; if one is
    /// already there, that one goes to the pointer (swap, as with the backpack). The partner's
    /// area accepts nothing.
    /// </summary>
    public PlaceEnum TryPlace(UWInventory pOInventory, Vector2 pOMousePos)
    {
        UWConversationTrade lOTrade = Trade;

        if (lOTrade == null)
            return PlaceEnum.NotHere;

        int liSlot = mOConversationScreen.GetPlayerTradeSlotAt(pOMousePos);

        if (liSlot >= 0)
        {
            // The slot's own rule (UWConversationTrade.DropOnPlayerSlot, CombineToSlot_ovr097_D30):
            // a like pile takes the count (per user, 2026-09-29: the original stacks there),
            // anything else swaps, a container there refuses - then the thing stays on the
            // pointer, as a drop that found no place.
            UWObject lOHeld = pOInventory.TakeCursorItem();
            UWObject lOBack = lOTrade.DropOnPlayerSlot(liSlot, lOHeld);

            if (lOBack != null)
                pOInventory.BeginDragFromExternal(lOBack);

            return lOBack != lOHeld ? PlaceEnum.Placed : PlaceEnum.Refused;
        }

        if (mOConversationScreen.GetNpcTradeSlotAt(pOMousePos) >= 0)
            return PlaceEnum.Refused;

        return PlaceEnum.NotHere;
    }

    /// <summary>
    /// IN A CONVERSATION A CONTAINER STAYS WHERE IT IS: "You cannot barter a container.
    /// Instead, remove the contents you want to trade." for a moment on the parchment, the
    /// drag never starts (per user on the original, 2026-09-18; the reference refuses the
    /// pickup the same way, uimanager_inventory). Only from the inventory - a container that
    /// already lies in the trade area can be taken back. THE RUNE BAG IS EXEMPT: UW.EXE
    /// (ovr121_241 at ovr121_3A5, read 2026-10-01) refuses major class 2 minor 0 in the
    /// conversation's mouse mode 4 only when the index is not 0xF - 0x8F is the rune bag,
    /// which Dr. Owl wants to see (conversation 14, show_inv for 143); ours refused it too
    /// (per user, 2026-10-01). True when it refused.
    /// </summary>
    public bool RefusesContainer(UWObject pOPending, UWDragSource pOSource, DataImport pOData)
    {
        if (pOPending == null || pOSource.IsTrade
            || pOPending.GetCategory() != UWObject.ObjectCategoryEnum.Containers
            || pOPending.ID == UWObjectMechanics.RuneBagId
            || !IsConversationOpen || pOData == null)
            return false;

        mOConversationScreen.ShowLookText(UWText.General(pOData, UWStringIds.CannotBarterContainerMessage));
        return true;
    }

    /// <summary>
    /// A trade good refused for its weight (UWItemDrag.fTakeFromSource) - its message
    /// ON THE PARCHMENT, where the player looks (B16 of the cleanup plan, 2026-10-12):
    /// the original prints 0xFC with game_sprint while the barter is up
    /// (UseTradeSlot_ovr097_6E8), where the player reads it; ours wrote it into the
    /// message box, which the parchment covers, so the refusal was never seen. The
    /// modern trade shows it there as well (UWModernBagTrade.fTakeFromTrade), as the barter
    /// refusal of a container above does.
    /// </summary>
    public void ShowRefusal(int piMessageIndex, Interaction pOInteraction, DataImport pOData)
    {
        if (piMessageIndex >= 0 && IsConversationOpen && pOData != null)
            mOConversationScreen.ShowLookText(UWText.General(pOData, piMessageIndex));
        else if (pOInteraction != null && piMessageIndex >= 0)
            pOInteraction.AddGeneralMessage(piMessageIndex);
    }

    /// <summary>
    /// IN A CONVERSATION the look goes onto the parchment: the description replaces the
    /// answers for two seconds, as for an item in the trade area (per user on the original,
    /// 2026-09-18). The message box behind the parchment is not visible there. False outside a
    /// conversation.
    /// </summary>
    public bool TryLook(UWObject pOItem)
    {
        if (Trade != null || (mOConversationScreen != null && mOConversationScreen.IsOpen && mOConversationScreen.Session != null))
        {
            mOConversationScreen.Session.LookAt(pOItem);
            return true;
        }

        return false;
    }
}
