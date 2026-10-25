using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The inventory model (UWInventoryModel): carrying, splitting, consuming, putting away, the
    /// nested bags and combining - moved out of InventoryRuleChecks unchanged.
    /// </summary>
    public static partial class SelfChecks
    {
        private static UWObject fStack(int piId, int piCount)
        {
            return new UWObject((ushort)piId) { HasQuantity = true, Quantity = (ushort)piCount };
        }

        /// <summary>UWInventoryModel: carrying with a count, splitting a stack to the pointer,
        /// consuming one piece, removing, and the modern put-away.</summary>
        private static void fCheckInventoryModel(DataImport pOData)
        {
            Check.Group("Inventory model (CanCarry, TakeFromStack, TryConsumeOne, RemoveItem, DropCursorItemAnywhere)");

            UWCommonObjectProperties lOProperties = pOData.CommonObjectProperties;

            if (lOProperties == null || pOData.InitialPlayer == null)
            {
                Check.Fail("inventory model", "no COMOBJ.DAT or no player");

                return;
            }

            // The template PLAYER.DAT carries no capacity (0x4C is 0 there); a character with 400
            // tenth-stones stands in for the check. The runner reloads the game state afterwards.
            byte[] lyPlain = (byte[])pOData.InitialPlayer.PlainBytes.Clone();

            UWBinary.Write16(lyPlain, 0x4C, 400);
            pOData.ReplaceInitialPlayer(UWPlayerData.FromPlain(lyPlain));

            UWInventoryModel lOModel = new UWInventoryModel(() => pOData, () => null);
            int liStone = UWObjectMechanics.SlingStoneObjectId;
            int liCapacity = pOData.InitialPlayer.MaxWeight - lOModel.GetCarriedTenthStones(lOProperties);
            UWCommonObjectProperties.Entry lOEntry;

            Check.Bool("carry: the template player can carry something", liCapacity > 0, true);
            Check.Bool("carry: the sling stone has a mass", lOProperties.TryGet(liStone, out lOEntry) && lOEntry.MassTenthStones > 0, true);

            int liMass = lOEntry.MassTenthStones;
            int liTooMany = (liCapacity / liMass) + 2;
            UWObject lOHeap = fStack(liStone, liTooMany);

            Check.Bool("carry: a heap heavier than the capacity is refused", lOModel.CanCarry(lOHeap), false);
            Check.Bool("carry: one stone of it is not", lOModel.CanCarry(lOHeap, 1), true);
            Check.Bool("carry: a count above the stack weighs the whole stack",
                lOModel.CanCarry(lOHeap, liTooMany + 5), false);
            Check.Bool("carry: exactly the capacity fits", lOModel.CanCarry(fStack(liStone, liCapacity / liMass)), true);
            Check.Bool("carry: nothing is refused without an item", lOModel.CanCarry(null), true);

            // ItemWeight (INVDATA.C): mass times the raw count, so a count of 0 weighs nothing
            // (C10, decided 2026-10-15); a special link weighs one piece.
            Check.Int("weight: a stack of three stones weighs three", UWInventoryWeight.GetItemTenthStones(fStack(liStone, 3),
                lOProperties), 3 * liMass);
            Check.Int("weight: a stack with a count of 0 weighs nothing", UWInventoryWeight.GetItemTenthStones(fStack(liStone, 0),
                lOProperties), 0);
            Check.Int("weight: a special link weighs one piece", UWInventoryWeight.GetItemTenthStones(
                fStack(liStone, UWStackRules.SpecialPropertyThreshold + 5), lOProperties), liMass);

            // Splitting: part of the stack goes to the pointer, the stack keeps the rest.
            UWObject lOFive = fStack(liStone, 5);
            lOModel.Backpack[0] = lOFive;

            Check.Bool("split: two of five go to the pointer", lOModel.TakeFromStack(lOFive, 2), true);
            Check.Int("split: ... the pointer holds two", lOModel.CursorItem != null ? UWStackRules.Count(lOModel.CursorItem) : -1, 2);
            Check.Int("split: ... three stay", UWStackRules.Count(lOFive), 3);
            Check.Bool("split: ... the stack stays in its place", lOModel.Backpack[0] == lOFive, true);
            lOModel.TakeCursorItem();
            Check.Bool("split: the whole stack does not split", lOModel.TakeFromStack(lOFive, 3), false);
            Check.Bool("split: nor more than it has", lOModel.TakeFromStack(lOFive, 4), false);
            Check.Bool("split: nor a count of zero", lOModel.TakeFromStack(lOFive, 0), false);

            UWObject lOEnchanted = new UWObject(0x20) { Quantity = 719 };
            Check.Bool("split: a special property is one piece and never splits", lOModel.TakeFromStack(lOEnchanted, 1), false);

            // Consuming: a stack loses one, a single piece leaves its place, a special property goes whole.
            Check.Bool("consume: one of three", lOModel.TryConsumeOne(lOFive), true);
            Check.Int("consume: ... two stay", UWStackRules.Count(lOFive), 2);
            lOFive.Quantity = 1;
            Check.Bool("consume: the last piece leaves the backpack", lOModel.TryConsumeOne(lOFive), true);
            Check.Bool("consume: ... slot empty", lOModel.Backpack[0] == null, true);
            lOModel.Backpack[1] = lOEnchanted;
            Check.Bool("consume: enchanted armour goes whole", lOModel.TryConsumeOne(lOEnchanted), true);
            Check.Bool("consume: ... slot empty, the link field untouched", lOModel.Backpack[1] == null && lOEnchanted.Quantity == 719, true);
            Check.Bool("consume: nothing happens to a thing that is not carried", lOModel.TryConsumeOne(new UWObject(1)), false);

            UWObject lODagger = new UWObject(1);
            lOModel.Backpack[2] = lODagger;
            Check.Bool("remove: a backpack item", lOModel.RemoveItem(lODagger), true);
            Check.Bool("remove: ... gone", lOModel.Backpack[2] == null, true);
            Check.Bool("remove: not twice", lOModel.RemoveItem(lODagger), false);

            // The modern put-away: onto a matching stack, else a free place.
            UWModelReset(lOModel);
            lOModel.Backpack[3] = fStack(liStone, 3);
            lOModel.BeginDragFromExternal(fStack(liStone, 2));
            Check.Bool("put away: two stones join the three", lOModel.DropCursorItemAnywhere(), true);
            Check.Int("put away: ... five", UWStackRules.Count(lOModel.Backpack[3]), 5);
            Check.Bool("put away: ... the pointer is empty", lOModel.CursorItem == null, true);

            lOModel.Backpack[3].Quantity = (ushort)UWStackRules.MaxStackCount;
            lOModel.BeginDragFromExternal(fStack(liStone, 2));
            Check.Bool("put away: a full stack of 511 does not take two more", lOModel.DropCursorItemAnywhere(), true);
            Check.Int("put away: ... the stack stays at 511", UWStackRules.Count(lOModel.Backpack[3]), UWStackRules.MaxStackCount);
            Check.Bool("put away: ... the two lie in the first free place", lOModel.Backpack[0] != null && UWStackRules.Count(lOModel.Backpack[0]) == 2, true);

            UWModelReset(lOModel);

            for (int liSlot = 0; liSlot < lOModel.Backpack.Length; liSlot++)
                lOModel.Backpack[liSlot] = new UWObject(1);

            Check.Bool("put away: a full backpack without bags takes nothing", lOModel.TryStoreAnywhere(new UWObject(2)), false);
            Check.Bool("put away: ... and the pointer is empty again", lOModel.CursorItem == null, true);
            Check.Bool("put away: a free place takes the thing", fFreeSlot(lOModel, 4) && lOModel.TryStoreAnywhere(lODagger), true);
            Check.Bool("put away: ... in that place", lOModel.Backpack[4] == lODagger, true);

            fCheckCombining(pOData, lOModel);
            fCheckDropOnEquippedBag(lOModel);
            fCheckNestedBags(pOData, lOModel);
        }

        /// <summary>ItemFitsSlot's loop over the open bags (A43, 2026-10-15): a box inside a full
        /// sack takes nothing more, though the box itself has room - "The box is too full.".</summary>
        private static void fCheckNestedBags(DataImport pOData, UWInventoryModel pOModel)
        {
            int liOuter = -1;
            int liInner = -1;
            int liOuterCapacity = 0;

            for (int liId = 0x80; liId <= 0x8F; liId++)
            {
                if (liId == UWObjectMechanics.RuneBagId || pOData.ObjectClassProperties == null
                    || !pOData.ObjectClassProperties.TryGetContainer(liId, out UWObjectClassProperties.Container lOEntry))
                    continue;

                if (liOuter < 0 && lOEntry.CapacityTenthStones > 0 && lOEntry.ObjectMask == UWObjectClassProperties.ContainerMaskAll)
                {
                    liOuter = liId;
                    liOuterCapacity = lOEntry.CapacityTenthStones;
                }
                else if (liInner < 0 && liOuter >= 0 && lOEntry.ObjectMask == UWObjectClassProperties.ContainerMaskAll)
                {
                    liInner = liId;
                }
            }

            UWCommonObjectProperties lOProperties = pOData.CommonObjectProperties;
            int liStone = UWObjectMechanics.SlingStoneObjectId;
            int liStoneMass = lOProperties != null && lOProperties.TryGet(liStone, out UWCommonObjectProperties.Entry lOStone)
                ? lOStone.MassTenthStones : 0;

            fExpectBool("nested bags: two plain containers and a stone with a mass", liOuter >= 0 && liInner >= 0 && liStoneMass > 0, true);

            if (liOuter < 0 || liInner < 0 || liStoneMass <= 0)
                return;

            UWModelReset(pOModel);

            UWObject lOInner = fBag(liInner);
            int liInnerWeight = UWInventoryWeight.GetItemTenthStones(lOInner, lOProperties);
            int liFill = (liOuterCapacity - liInnerWeight) / liStoneMass;
            UWObject lOOuter = fBag(liOuter);

            lOOuter.Contents.Add(lOInner);
            lOOuter.Contents.Add(fStack(liStone, liFill));

            pOModel.Backpack[0] = lOOuter;
            pOModel.BeginDragFromExternal(fStack(liStone, 1));

            Check.Bool("nested bags: the full sack refuses a stone dropped into the box inside it",
                pOModel.DropCursorItemInContainerAt(lOInner, 0), false);
            Check.Bool("nested bags: ... naming the box", pOModel.LastContainerRejection == UWContainerCapacity.ResultEnum.TooHeavy
                && pOModel.LastContainerRejectionContainer == lOInner, true);
            Check.Bool("nested bags: ... dropped onto the box in the sack's slot too",
                pOModel.DropCursorItemInContainerAt(lOOuter, 0), false);

            lOOuter.Contents.RemoveAt(1);

            Check.Bool("nested bags: with the sack emptied the box takes it", pOModel.DropCursorItemInContainerAt(lOInner, 0), true);

            UWModelReset(pOModel);
        }

        /// <summary>AddToOccupiedSlot (A36, 2026-10-15): a bag on a shoulder or in a hand takes
        /// what is dropped onto it; putting a piece ON (the modern equip) swaps instead.</summary>
        private static void fCheckDropOnEquippedBag(UWInventoryModel pOModel)
        {
            UWModelReset(pOModel);

            UWObject lOSack = fBag(0x80);
            UWObject lOApple = new UWObject(0xB4);

            pOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.RightOffHandSlot] = lOSack;
            pOModel.BeginDragFromExternal(lOApple);

            Check.Bool("bag on a shoulder: the apple goes in",
                pOModel.DropCursorItemInEquip(UWArmorItemMap.BodySlot.RightOffHandSlot), true);
            Check.Bool("bag on a shoulder: ... the sack stays, the pointer is empty, the apple inside",
                pOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.RightOffHandSlot] == lOSack && pOModel.CursorItem == null
                && lOSack.Contents.Contains(lOApple), true);

            UWObject lOTorch = new UWObject((ushort)UWObjectMechanics.TorchObjectId);

            pOModel.BeginDragFromExternal(lOTorch);
            pOModel.DropCursorItemInEquip(UWArmorItemMap.BodySlot.RightOffHandSlot, false);

            Check.Bool("bag on a shoulder: putting a piece on swaps it", pOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.RightOffHandSlot]
                == lOTorch && pOModel.CursorItem == lOSack, true);

            pOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.RightOffHandSlot] = null;
            UWModelReset(pOModel);

            // The open bag's icon when it is the outermost (A44): a full pack refuses at once, even
            // with a matching stack in it; with a place free a stack in a hand takes it too.
            int liStone = UWObjectMechanics.SlingStoneObjectId;
            UWObject lOPackStones = fStack(liStone, 3);

            for (int liSlot = 0; liSlot < pOModel.Backpack.Length; liSlot++)
                pOModel.Backpack[liSlot] = new UWObject(1);

            pOModel.Backpack[0] = lOPackStones;
            pOModel.BeginDragFromExternal(fStack(liStone, 2));

            Check.Bool("bag icon: a full pack refuses, matching stack or not", pOModel.DropCursorItemInFirstFreeBackpackSlot(), false);
            Check.Int("bag icon: ... the stack keeps its three", UWStackRules.Count(lOPackStones), 3);

            pOModel.Backpack[5] = null;
            pOModel.Backpack[0] = null;

            UWObject lOHandStones = fStack(liStone, 4);

            pOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.LeftOffHandSlot] = lOHandStones;

            Check.Bool("bag icon: with a place free the stack in a hand takes them", pOModel.DropCursorItemInFirstFreeBackpackSlot(), true);
            Check.Int("bag icon: ... six in the hand", UWStackRules.Count(lOHandStones), 6);

            pOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.LeftOffHandSlot] = null;
            UWModelReset(pOModel);
        }

        /// <summary>AddToOccupiedSlot's combining (A37, 2026-10-15): a NEW object of quality 40, the
        /// pointer item's place taken by it when that is used up, a stack or a thing holding
        /// something refused.</summary>
        private static void fCheckCombining(DataImport pOData, UWInventoryModel pOModel)
        {
            UWObjectCombining.Rule lOBoth = default;
            UWObjectCombining.Rule lOKeeps = default;
            bool lbBoth = false;
            bool lbKeeps = false;

            foreach (UWObjectCombining.Rule lORule in pOData.ObjectCombining.Rules)
            {
                if (!lbBoth && lORule.FirstIsConsumed && lORule.SecondIsConsumed)
                {
                    lOBoth = lORule;
                    lbBoth = true;
                }

                if (!lbKeeps && lORule.FirstIsConsumed != lORule.SecondIsConsumed)
                {
                    lOKeeps = lORule;
                    lbKeeps = true;
                }
            }

            Check.Bool("combine: CMB.DAT has a rule using up both and one keeping one", lbBoth && lbKeeps, true);

            if (!lbBoth || !lbKeeps)
                return;

            UWModelReset(pOModel);

            UWObject lOFirst = new UWObject((ushort)lOBoth.FirstObjectId) { Quality = 9 };
            pOModel.Backpack[1] = new UWObject((ushort)lOBoth.SecondObjectId) { Quality = 9 };
            pOModel.BeginDragFromExternal(lOFirst);

            Check.Bool("combine: both used up", pOModel.DropCursorItemInBackpack(1), true);
            Check.Bool("combine: ... the result is a new object on the pointer", pOModel.CursorItem != null
                && pOModel.CursorItem != lOFirst && pOModel.CursorItem.ID == lOBoth.ResultObjectId, true);
            Check.Int("combine: ... of CreateObj's quality 40", pOModel.CursorItem != null ? pOModel.CursorItem.Quality : -1, 40);
            Check.Bool("combine: ... and the place is empty", pOModel.Backpack[1] == null, true);

            // The rule keeping one side: the kept one at the pointer, the used-up one in the place,
            // so the result takes that place.
            UWModelReset(pOModel);

            int liKept = lOKeeps.FirstIsConsumed ? lOKeeps.SecondObjectId : lOKeeps.FirstObjectId;
            int liUsed = lOKeeps.FirstIsConsumed ? lOKeeps.FirstObjectId : lOKeeps.SecondObjectId;
            UWObject lOKeptItem = new UWObject((ushort)liKept);

            pOModel.Backpack[2] = new UWObject((ushort)liUsed);
            pOModel.BeginDragFromExternal(lOKeptItem);

            Check.Bool("combine: one kept", pOModel.DropCursorItemInBackpack(2), true);
            Check.Bool("combine: ... the kept one stays at the pointer", pOModel.CursorItem == lOKeptItem, true);
            Check.Bool("combine: ... the result takes the used-up one's place", pOModel.Backpack[2] != null
                && pOModel.Backpack[2].ID == lOKeeps.ResultObjectId, true);

            // A stack of two is refused (ObjsBeCombinable) - the drop swaps instead.
            UWModelReset(pOModel);

            UWObject lOStack = new UWObject((ushort)liUsed) { HasQuantity = true, Quantity = 2 };
            UWObject lOKept = new UWObject((ushort)liKept);

            pOModel.Backpack[2] = lOStack;
            pOModel.BeginDragFromExternal(lOKept);
            pOModel.DropCursorItemInBackpack(2);

            Check.Bool("combine: a stack of two is refused, the two swap", pOModel.Backpack[2] == lOKept
                && pOModel.CursorItem == lOStack && UWStackRules.Count(lOStack) == 2, true);

            UWModelReset(pOModel);
        }

        private static void UWModelReset(UWInventoryModel pOModel)
        {
            for (int liSlot = 0; liSlot < pOModel.Backpack.Length; liSlot++)
                pOModel.Backpack[liSlot] = null;

            pOModel.TakeCursorItem();
        }

        private static bool fFreeSlot(UWInventoryModel pOModel, int piSlot)
        {
            pOModel.Backpack[piSlot] = null;

            return true;
        }
    }
}
