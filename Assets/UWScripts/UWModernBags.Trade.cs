using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE MODERN BAGS IN A CONVERSATION (stage 4, per user on a mockup, 2026-10-04): the trade areas
/// of UWModernConversation are slots of the bags' dragging, as the classic ones are of UWItemDrag:
///
///   - a thing dropped on "Your offer" goes there (a matching stack takes it, a thing already
///     there comes onto the pointer); a container is refused as in the original, the rune bag
///     excepted (UWItemDrag: "You cannot barter a container.");
///   - a LEFT CLICK on a trade slot marks or unmarks the thing for the deal (the original's click
///     on the item or its cross), a LEFT DRAG takes one's own offer back out - and the partner's
///     goods once bought (UWConversationTrade.NpcBought), with the weight checked;
///   - the RIGHT BUTTON on a trade slot looks; one's own things keep their context menu
///     without Use (in a conversation the original uses nothing), its Look answering in the
///     conversation; a container still opens or closes;
///   - nothing goes into the world: outside a slot or a window the thing stays on the pointer.
/// </summary>
public partial class UWModernBags
{
    /// <summary>"You cannot barter a container." (UWItemDrag, string block 1 no. 187 in ours).</summary>
    private const int CannotBarterContainerMessage = UWStringIds.CannotBarterContainerMessage;

    private bool mbTradePress;

    private bool mbTradePressNpc;

    private int miTradePressSlot;

    private Vector2 mOTradePressStart;

    private static UWConversationScreen fConversation()
    {
        UWConversationScreen lOScreen = UWScene.ConversationScreen;

        return lOScreen != null && lOScreen.IsOpen && lOScreen.Session != null ? lOScreen : null;
    }

    private static UWConversationTrade fTrade()
    {
        UWConversationScreen lOScreen = fConversation();

        return lOScreen != null ? lOScreen.Session.Trade : null;
    }

    private static bool fIsTalking()
    {
        return fConversation() != null;
    }

    /// <summary>The trade slot under the pointer, if a modern conversation shows one there.</summary>
    private static bool fTradeSlotAt(Vector2 pOPointer, out bool pbNpc, out int piSlot)
    {
        pbNpc = false;
        piSlot = -1;

        return fTrade() != null && UWModernConversation.Instance != null
            && UWModernConversation.Instance.TryGetTradeSlotAt(pOPointer, out pbNpc, out piSlot);
    }

    /// <summary>A left press on a trade slot: remembered - its release marks, a drag takes.</summary>
    private bool fTradePressed(Vector2 pOPointer)
    {
        if (!fTradeSlotAt(pOPointer, out bool lbNpc, out int liSlot))
            return false;

        mbTradePress = true;
        mbTradePressNpc = lbNpc;
        miTradePressSlot = liSlot;
        mOTradePressStart = pOPointer;

        return true;
    }

    /// <summary>The pressed trade slot, every frame: released in place it marks, moved away it
    /// takes the thing onto the pointer (the release then puts it down, as any drag).</summary>
    private void fUpdateTradePress(UWControls pOControls, Vector2 pOPointer, UWInventory pOInventory)
    {
        if (!mbTradePress)
            return;

        UWConversationTrade lOTrade = fTrade();

        if (lOTrade == null)
        {
            mbTradePress = false;
            return;
        }

        if (pOControls.Player.CursorDrag.WasReleasedThisFrame() || !pOControls.Player.CursorDrag.IsPressed())
        {
            mbTradePress = false;

            if (mbTradePressNpc && lOTrade.NpcGoodsBought && lOTrade.NpcBought[miTradePressSlot]
                && lOTrade.NpcItems[miTradePressSlot] != null)
                fTakeFromTrade(lOTrade, pOInventory, false);
            else if (mbTradePressNpc)
                lOTrade.ToggleNpcSelected(miTradePressSlot);
            else if (lOTrade.PlayerItems[miTradePressSlot] != null)
                lOTrade.TogglePlayerSelected(miTradePressSlot);

            return;
        }

        if (Vector2.Distance(pOPointer, mOTradePressStart) < ClickMoveThreshold)
            return;

        mbTradePress = false;
        fTakeFromTrade(lOTrade, pOInventory, true);
    }

    /// <summary>Takes the pressed slot's thing onto the pointer: one's own offer, or a bought good
    /// (too heavy, it stays there).</summary>
    private void fTakeFromTrade(UWConversationTrade pOTrade, UWInventory pOInventory, bool pbDragged)
    {
        UWObject lOItem = mbTradePressNpc ? pOTrade.NpcItems[miTradePressSlot] : pOTrade.PlayerItems[miTradePressSlot];

        if (lOItem == null || (mbTradePressNpc && (!pOTrade.NpcGoodsBought || !pOTrade.NpcBought[miTradePressSlot])))
            return;

        // The whole stack is taken here, so the whole stack is weighed (UWPickupRules) - on both
        // sides, as UseTradeSlot_ovr097_6E8 weighs (until 2026-10-15 only the partner's).
        UWPickupRules.Result lOCheck = UWPickupRules.CheckFromTrade(lOItem, UWWorldPickup.WholeStack, pOInventory.Model);

        if (!lOCheck.CanTake)
        {
            if (mOInteraction != null && lOCheck.MessageIndex >= 0)
                fShowTalkText(mOInteraction.GetGeneralMessage(lOCheck.MessageIndex));

            return;
        }

        if (mbTradePressNpc)
            pOInventory.BeginDragFromExternal(pOTrade.TakeFromNpcSlot(miTradePressSlot));
        else
            pOInventory.BeginDragFromExternal(pOTrade.TakeFromPlayerSlot(miTradePressSlot));

        // Its way back, should it find no place, is the pack (fReturnCursorItem).
        fForgetOrigin();

        mbPressDrag = pbDragged && pOInventory.CursorItem != null;
        mOPressStart = mOTradePressStart;
        mOPressWindow = null;
        miPressSlot = -1;
        mbPressEquip = false;
    }

    /// <summary>The thing on the pointer put down on a trade slot - true if the pointer was on
    /// one (pbPlaced: whether it went there).</summary>
    private bool fTryPlaceInTrade(Vector2 pOPointer, UWInventory pOInventory, out bool pbPlaced)
    {
        pbPlaced = false;

        if (!fTradeSlotAt(pOPointer, out bool lbNpc, out int liSlot))
            return false;

        // The partner's side takes nothing.
        if (lbNpc)
            return true;

        UWConversationTrade lOTrade = fTrade();
        UWObject lOItem = pOInventory.CursorItem;

        if (lOItem.GetCategory() == UWObject.ObjectCategoryEnum.Containers && lOItem.ID != UWObjectMechanics.RuneBagId)
        {
            if (mOInteraction != null)
                fShowTalkText(mOInteraction.GetGeneralMessage(CannotBarterContainerMessage));

            return true;
        }

        // The slot's own rule (UWConversationTrade.DropOnPlayerSlot): a like pile takes the count,
        // anything else swaps, a container there refuses and the thing stays on the pointer.
        UWObject lOHeld = pOInventory.TakeCursorItem();
        UWObject lOBack = lOTrade.DropOnPlayerSlot(liSlot, lOHeld);

        if (lOBack != null)
            pOInventory.BeginDragFromExternal(lOBack);

        pbPlaced = lOBack != lOHeld;
        return true;
    }

    /// <summary>The right button on a trade slot looks at its thing, as in the original.</summary>
    private bool fTradeLook(Vector2 pOPointer)
    {
        if (!fTradeSlotAt(pOPointer, out bool lbNpc, out int liSlot))
            return false;

        UWConversationScreen lOScreen = fConversation();
        UWConversationTrade lOTrade = lOScreen.Session.Trade;
        UWObject lOItem = lbNpc ? lOTrade.NpcItems[liSlot] : lOTrade.PlayerItems[liSlot];

        if (lOItem == null)
            return true;

        if (lbNpc)
            lOScreen.Session.LookAtPartnerTradeItem(lOItem);
        else
            lOScreen.Session.LookAtOwnTradeItem(lOItem);

        return true;
    }

    /// <summary>A line for the player during a conversation: it replaces the answers for a
    /// moment, as the classic screen shows a look (the messages are hidden meanwhile).</summary>
    private static void fShowTalkText(string psText)
    {
        UWConversationScreen lOScreen = fConversation();

        if (lOScreen != null && !string.IsNullOrEmpty(psText))
            lOScreen.ShowLookText(psText.TrimEnd('\r', '\n'));
    }
}
