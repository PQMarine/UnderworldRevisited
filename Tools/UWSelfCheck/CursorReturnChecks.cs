using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.15 (2026-10-22): the classic press's source is one UWDragSource (peek, take,
    /// return, consume - the trade's slots too); the thing on the pointer goes back by one UWCursorOrigin and
    /// one rule, UWInventoryModel.ReturnCursorItem - the classic drag's cancel as it was taken
    /// (AsTaken: the backpack place or the paperdoll slot unchecked, else the first free backpack
    /// place, never a container) and the modern bags' way (OrAnywhere: the split stack, the slot or
    /// place while free, the container's place while it is carried, else wherever there is room).
    /// </summary>
    public static partial class Program
    {
        private static void fCheckCursorReturn(DataImport pOData)
        {
            Check.Group("Cursor return (UWInventoryModel.ReturnCursorItem - cleanup step 5.15)");

            UWInventoryModel lOModel = new UWInventoryModel(() => pOData, () => null);
            int liStone = UWObjectMechanics.SlingStoneObjectId;
            UWObject lODagger = new UWObject(1);
            UWObject lOHelmet = new UWObject(44);

            Check.Bool("nothing on the pointer: empty either way",
                lOModel.ReturnCursorItem(UWCursorOrigin.Backpack(0), UWInventoryModel.ReturnEnum.AsTaken)
                && lOModel.ReturnCursorItem(UWCursorOrigin.None, UWInventoryModel.ReturnEnum.OrAnywhere), true);

            // AsTaken - the classic cancel.
            lOModel.Backpack[5] = lODagger;
            lOModel.BeginDragFromBackpack(5);
            Check.Bool("as taken: from a backpack place", lOModel.ReturnCursorItem(UWCursorOrigin.Backpack(5),
                UWInventoryModel.ReturnEnum.AsTaken), true);
            Check.Bool("as taken: ... back in that place", lOModel.Backpack[5] == lODagger && lOModel.CursorItem == null, true);

            lOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.Helmet] = lOHelmet;
            lOModel.BeginDragFromEquip(UWArmorItemMap.BodySlot.Helmet);
            lOModel.ReturnCursorItem(UWCursorOrigin.Equip(UWArmorItemMap.BodySlot.Helmet), UWInventoryModel.ReturnEnum.AsTaken);
            Check.Bool("as taken: from the paperdoll, back onto the head",
                lOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.Helmet] == lOHelmet && lOModel.CursorItem == null, true);
            lOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.Helmet] = null;

            UWObject lOSack = fCursorBag(0x80, new UWObject(0xB4), new UWObject(0xB5));
            UWObject lOApple = lOSack.Contents[0];

            lOModel.Backpack[0] = lOSack;
            lOModel.BeginDragFromContainerItem(lOSack, 0);
            lOModel.ReturnCursorItem(UWCursorOrigin.Container(lOSack, 0), UWInventoryModel.ReturnEnum.AsTaken);
            Check.Bool("as taken: from a container, into the first free backpack place, not the container",
                lOModel.Backpack[1] == lOApple && !lOSack.Contents.Contains(lOApple), true);
            lOModel.Backpack[1] = null;

            lOModel.BeginDragFromExternal(fCursorStack(liStone, 2));
            lOModel.ReturnCursorItem(UWCursorOrigin.None, UWInventoryModel.ReturnEnum.AsTaken);
            Check.Bool("as taken: no origin, the first free backpack place",
                lOModel.Backpack[1] != null && UWStackRules.Count(lOModel.Backpack[1]) == 2, true);

            for (int liSlot = 0; liSlot < lOModel.Backpack.Length; liSlot++)
                lOModel.Backpack[liSlot] = new UWObject(1);

            lOModel.BeginDragFromExternal(lODagger);
            Check.Bool("as taken: a full backpack keeps it on the pointer",
                lOModel.ReturnCursorItem(UWCursorOrigin.None, UWInventoryModel.ReturnEnum.AsTaken), false);
            Check.Bool("as taken: ... still there", lOModel.CursorItem == lODagger, true);
            lOModel.TakeCursorItem();

            for (int liSlot = 0; liSlot < lOModel.Backpack.Length; liSlot++)
                lOModel.Backpack[liSlot] = null;

            // OrAnywhere - the modern bags.
            UWObject lOFive = fCursorStack(liStone, 5);

            lOModel.Backpack[2] = lOFive;
            lOModel.TakeFromStack(lOFive, 2);
            Check.Bool("anywhere: a split part", lOModel.ReturnCursorItem(UWCursorOrigin.Stack(lOFive),
                UWInventoryModel.ReturnEnum.OrAnywhere), true);
            Check.Int("anywhere: ... back onto its stack", UWStackRules.Count(lOFive), 5);

            lOModel.BeginDragFromBackpack(2);
            lOModel.Backpack[2] = lODagger;
            lOModel.ReturnCursorItem(UWCursorOrigin.Backpack(2), UWInventoryModel.ReturnEnum.OrAnywhere);
            Check.Bool("anywhere: its place taken meanwhile, the first free place",
                lOModel.Backpack[0] == lOFive && lOModel.Backpack[2] == lODagger, true);
            lOModel.Backpack[0] = null;
            lOModel.Backpack[2] = null;

            UWObject lOBag = fCursorBag(0x80, new UWObject(0xB4), new UWObject(0xB5), new UWObject(0xB6));
            UWObject lOMiddle = lOBag.Contents[1];

            lOModel.Backpack[4] = lOBag;
            lOModel.BeginDragFromContainerItem(lOBag, 1);
            lOModel.ReturnCursorItem(UWCursorOrigin.Container(lOBag, 1), UWInventoryModel.ReturnEnum.OrAnywhere);
            Check.Bool("anywhere: into its container at its place, the neighbours where they were",
                lOBag.Contents.Count == 3 && lOBag.Contents[1] == lOMiddle && lOModel.CursorItem == null, true);

            lOModel.BeginDragFromContainerItem(lOBag, 1);
            lOModel.Backpack[4] = null;
            lOModel.ReturnCursorItem(UWCursorOrigin.Container(lOBag, 1), UWInventoryModel.ReturnEnum.OrAnywhere);
            Check.Bool("anywhere: its container no longer carried, a free backpack place",
                !lOBag.Contents.Contains(lOMiddle) && lOModel.Backpack[0] == lOMiddle, true);
            lOModel.Backpack[0] = null;

            lOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.Helmet] = lOHelmet;
            lOModel.BeginDragFromEquip(UWArmorItemMap.BodySlot.Helmet);
            lOModel.ReturnCursorItem(UWCursorOrigin.Equip(UWArmorItemMap.BodySlot.Helmet), UWInventoryModel.ReturnEnum.OrAnywhere);
            Check.Bool("anywhere: a piece back onto its free slot",
                lOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.Helmet] == lOHelmet && lOModel.CursorItem == null, true);
            lOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.Helmet] = null;

            Check.Bool("carried: a thing in a carried bag", fCarriedIn(lOModel, lOBag), true);
            Check.Bool("carried: a thing that is not", lOModel.IsCarried(new UWObject(1)), false);
        }

        /// <summary>UWDragSource (cleanup step 5.15): what the classic press hit - peeked, taken
        /// whole or in part, put back and used up by one value, the trade slots too.</summary>
        private static void fCheckDragSource(CheckContext pOContext)
        {
            Check.Group("Drag source (UWDragSource - cleanup step 5.15)");

            UWInventoryModel lOModel = new UWInventoryModel(() => pOContext.Data, () => null);
            int liStone = UWObjectMechanics.SlingStoneObjectId;
            UWObject lOFive = fCursorStack(liStone, 5);
            UWObject lOHelmet = new UWObject(44);
            UWDragSource lOPack = UWDragSource.Backpack(3);
            UWDragSource lOHead = UWDragSource.Equip(UWArmorItemMap.BodySlot.Helmet);

            lOModel.Backpack[3] = lOFive;
            lOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.Helmet] = lOHelmet;

            Check.Bool("kinds: a backpack place holds a thing, the icon, an arrow and a trade click do not",
                lOPack.HoldsThing && UWDragSource.World(new object()).HoldsThing && !UWDragSource.ContainerIcon.HoldsThing
                && !UWDragSource.ScrollArrow(1).HoldsThing && !UWDragSource.TradeClick(() => { }).HoldsThing
                && !UWDragSource.None.HoldsThing, true);
            Check.Bool("kinds: the trade slots are the trade", UWDragSource.PlayerTrade(0).IsTrade && UWDragSource.NpcTrade(0).IsTrade
                && !lOPack.IsTrade, true);
            Check.Bool("kinds: the paperdoll slot", lOHead.IsEquip(UWArmorItemMap.BodySlot.Helmet)
                && !lOHead.IsEquip(UWArmorItemMap.BodySlot.Chest) && !lOPack.IsEquip(UWArmorItemMap.BodySlot.Helmet), true);

            Check.Bool("peek: the backpack place", lOPack.Peek(lOModel, null) == lOFive, true);
            Check.Bool("peek: the paperdoll slot", lOHead.Peek(lOModel, null) == lOHelmet, true);
            Check.Bool("peek: the world's thing is the host's, the icon holds none",
                UWDragSource.World(new object()).Peek(lOModel, null) == null && UWDragSource.ContainerIcon.Peek(lOModel, null) == null, true);
            Check.Bool("peek: a trade slot without a trade is nothing", UWDragSource.PlayerTrade(0).Peek(lOModel, null) == null, true);

            Check.Bool("origin: the backpack place and the paperdoll slot go back as taken",
                lOPack.Origin.Kind == UWCursorOrigin.KindEnum.Backpack && lOPack.Origin.Index == 3
                && lOHead.Origin.Kind == UWCursorOrigin.KindEnum.Equip && lOHead.Origin.Slot == UWArmorItemMap.BodySlot.Helmet, true);
            Check.Bool("origin: a container place, the world and the trade go to the backpack",
                UWDragSource.Container(0).Origin.Kind == UWCursorOrigin.KindEnum.None
                && UWDragSource.World(new object()).Origin.Kind == UWCursorOrigin.KindEnum.None
                && UWDragSource.PlayerTrade(0).Origin.Kind == UWCursorOrigin.KindEnum.None, true);

            Check.Bool("take: two of five", lOPack.TakePart(lOModel, null, 2), true);
            Check.Bool("take: ... two on the pointer, three stay in the place", lOModel.CursorItem != null
                && UWStackRules.Count(lOModel.CursorItem) == 2 && lOModel.Backpack[3] == lOFive && UWStackRules.Count(lOFive) == 3, true);
            lOModel.TakeCursorItem();
            Check.Bool("take: not all of them in part", lOPack.TakePart(lOModel, null, 3), false);

            lOPack.TakeWhole(lOModel, null);
            Check.Bool("take: the whole stack leaves its place", lOModel.CursorItem == lOFive && lOModel.Backpack[3] == null, true);
            lOPack.Return(lOModel, null);
            Check.Bool("return: back in its place", lOModel.CursorItem == null && lOModel.Backpack[3] == lOFive, true);

            lOHead.TakeWhole(lOModel, null);
            lOHead.Return(lOModel, null);
            Check.Bool("return: the helmet back on the head",
                lOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.Helmet] == lOHelmet && lOModel.CursorItem == null, true);

            // The trade's slots (a partner on level 1, as the trade checks make him).
            UWConversationTrade lOTrade = fMakeTrade(pOContext, new[] { 0x80 }, out UWNpc lONpc, out UWConversationSession lOSession);
            UWObject lODagger = new UWObject(1);
            UWDragSource lOOwn = UWDragSource.PlayerTrade(1);

            lOTrade.PlayerItems[1] = lODagger;
            Check.Bool("trade: peek at the player's slot", lOOwn.Peek(lOModel, lOTrade) == lODagger, true);
            lOOwn.TakeWhole(lOModel, lOTrade);
            Check.Bool("trade: taken onto the pointer", lOModel.CursorItem == lODagger && lOTrade.PlayerItems[1] == null, true);
            lOOwn.Return(lOModel, lOTrade);
            Check.Bool("trade: back into its slot while it is free", lOTrade.PlayerItems[1] == lODagger && lOModel.CursorItem == null, true);

            lOOwn.TakeWhole(lOModel, lOTrade);
            lOTrade.PlayerItems[1] = new UWObject(2);
            lOOwn.Return(lOModel, lOTrade);
            Check.Bool("trade: its slot taken meanwhile, the backpack", lOModel.CursorItem == null
                && System.Array.IndexOf(lOModel.Backpack, lODagger) >= 0, true);
            lOModel.RemoveItem(lODagger);

            lOModel.BeginDragFromExternal(lODagger);
            UWDragSource.NpcTrade(2).Return(lOModel, lOTrade);
            Check.Bool("trade: a bought good back onto the partner's side, bought",
                lOTrade.NpcItems[2] == lODagger && lOTrade.NpcBought[2] && lOModel.CursorItem == null, true);

            // Used up from the source.
            lOPack.Consume(lOModel, lOFive);
            Check.Int("consume: a stack gives one", UWStackRules.Count(lOFive), 2);
            lOHead.Consume(lOModel, lOHelmet);
            Check.Bool("consume: a single piece leaves its slot", lOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.Helmet] == null, true);
        }

        private static bool fCarriedIn(UWInventoryModel pOModel, UWObject pOBag)
        {
            pOModel.Backpack[7] = pOBag;

            bool lbCarried = pOModel.IsCarried(pOBag.Contents[0]);

            pOModel.Backpack[7] = null;

            return lbCarried;
        }

        private static UWObject fCursorStack(int piId, int piCount)
        {
            return new UWObject((ushort)piId) { HasQuantity = true, Quantity = (ushort)piCount };
        }

        private static UWObject fCursorBag(int piId, params UWObject[] pOContents)
        {
            UWObject lOBag = fBag(piId);

            lOBag.Contents.AddRange(pOContents);

            return lOBag;
        }
    }
}
