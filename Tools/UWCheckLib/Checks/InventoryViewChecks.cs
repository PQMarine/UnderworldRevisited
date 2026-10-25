using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.8 (2026-10-20): what the inventory page shows and what a click on its
    /// controls and slots does, given one engine-free home in UWInventoryView for the classic
    /// frame (UWHudInventory, UWItemDrag) and the modern original page and bags
    /// (UWModernInventoryPage, UWModernBags). Pinned with what those classes computed themselves:
    /// the backpack and the container window from the scroll offset on, counts only for more than
    /// one piece (in the hands and on the rings too), the arrows' condition with the empty row
    /// after an exact fit, the open picture, the remaining weight, the spots' meaning and the row
    /// step, the container icon's "one level up" and the drop onto a backpack slot holding a
    /// container.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckInventoryView(DataImport pOData)
        {
            Check.Group("Inventory view (UWInventoryView - cleanup step 5.8)");

            UWInventoryView lOView = new UWInventoryView();

            lOView.Refresh(null, pOData);
            fExpectBool("nothing: no model shows an empty page, no container, no arrows",
                lOView.Backpack[0].Item == null && !lOView.ShowsContainer && lOView.ContainerPicture == null
                && !lOView.ShowsScrollUp && !lOView.ShowsScrollDown, true);
            fExpectInt("nothing: ... and no weight", lOView.RemainingStones, 0);

            UWLevel lOLevel = new UWLevel(0);
            UWInventoryModel lOModel = new UWInventoryModel(() => pOData, () => lOLevel);
            int liStone = UWObjectMechanics.SlingStoneObjectId;
            UWObject lODagger = new UWObject(1);

            // The backpack and its counts: single things get no number.
            lOModel.Backpack[0] = lODagger;
            lOModel.Backpack[2] = fStack(liStone, 3);
            lOModel.Backpack[3] = fStack(liStone, 0);
            lOView.Refresh(lOModel, pOData);
            fExpectBool("backpack: slot 0 shows the dagger", lOView.Backpack[0].Item == lODagger && lOView.PageSlot(0).Item == lODagger, true);
            fExpectBool("backpack: its picture is its icon, else its world picture",
                lOView.Backpack[0].Picture == (lODagger.Icon ?? lODagger.Texture), true);
            fExpectBool("backpack: a single thing shows no count", lOView.Backpack[0].ShowsCount, false);
            fExpectInt("backpack: three stones count 3", lOView.Backpack[2].Count, 3);
            fExpectBool("backpack: ... and show it", lOView.Backpack[2].ShowsCount, true);
            fExpectInt("backpack: a stack placed with 0 is one piece", lOView.Backpack[3].Count, 1);
            fExpectInt("backpack: an empty slot counts 0", lOView.Backpack[1].Count, 0);

            // The hands and rings, in UWPaperdollLayout.IconSlots' order: torches in the hand show
            // their count (per user, 2026-09-05: it was missing in the hand slots).
            lOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.RightHandSlot] = fStack(UWObjectMechanics.TorchObjectId, 2);
            lOModel.EquipSlots[(int)UWArmorItemMap.BodySlot.LeftRing] = new UWObject(1);
            lOView.Refresh(lOModel, pOData);
            fExpectInt("hands: the right hand is the third icon slot", fIconSlotOf(UWArmorItemMap.BodySlot.RightHandSlot), 2);
            fExpectBool("hands: two torches in the right hand show 2",
                lOView.Hands[2].Count == 2 && lOView.Hands[2].ShowsCount, true);
            fExpectBool("hands: the left ring is the first, a single thing without a number",
                lOView.Hands[0].Item != null && !lOView.Hands[0].ShowsCount, true);
            fExpectBool("hands: the empty left hand shows nothing",
                lOView.Hands[fIconSlotOf(UWArmorItemMap.BodySlot.LeftHandSlot)].Item == null, true);

            // The weight circle: capacity left in whole stones, as UWInventoryWeight computes it.
            fExpectInt("weight: the circle shows UWInventoryWeight.RemainingStones", lOView.RemainingStones,
                UWInventoryWeight.RemainingStones(pOData.InitialPlayer, lOModel.GetCarriedTenthStones(pOData.CommonObjectProperties)));

            fCheckInventoryViewPaging(pOData, lOModel, lOView);
            fCheckInventoryViewClicks(pOData, lOModel);
        }

        /// <summary>A container with more than eight things (from the original's level state): the
        /// window from the offset on, the arrows, the row step, and the open picture.</summary>
        private static void fCheckInventoryViewPaging(DataImport pOData, UWInventoryModel pOModel, UWInventoryView pOView)
        {
            UWObject lOSack = fBag(0x80);
            UWObject[] lOThings = new UWObject[9];

            for (int liAt = 0; liAt < lOThings.Length; liAt++)
            {
                lOThings[liAt] = new UWObject(1);
                lOSack.Contents.Add(lOThings[liAt]);
            }

            lOSack.Contents[8] = fStack(UWObjectMechanics.SlingStoneObjectId, 4);
            lOThings[8] = lOSack.Contents[8];

            fExpectBool("paging: the sack opens", pOModel.TryOpenContainer(lOSack), true);
            pOView.Refresh(pOModel, pOData);
            fExpectBool("paging: the page shows the container", pOView.ShowsContainer && pOView.OpenContainer == lOSack, true);
            fExpectBool("paging: ... its first eight things", pOView.PageSlot(0).Item == lOThings[0] && pOView.PageSlot(7).Item == lOThings[7], true);
            fExpectBool("paging: ... the backpack stays readable underneath", pOView.Backpack[2].Count == 3, true);
            fExpectBool("paging: nine things show the up arrow, not the down one",
                pOView.ShowsScrollUp && !pOView.ShowsScrollDown, true);
            fExpectBool("paging: the open picture is the open sack (OBJECTS.GR 129)",
                pOView.ContainerPicture == UWObjectAppearance.OpenContainerPicture(pOData.Textures, lOSack)
                && pOView.ContainerPicture == UWObjectAppearance.Picture(pOData.Textures, 129), true);

            pOModel.ScrollContainer(UWInventoryView.ScrollDeltaOf(UWInventoryView.ControlEnum.ScrollUp));
            pOView.Refresh(pOModel, pOData);
            fExpectInt("paging: the up arrow raises the offset by a row", pOModel.ContainerScrollOffset, 4);
            fExpectBool("paging: ... the second row moves to the first", pOView.PageSlot(0).Item == lOThings[4], true);
            fExpectBool("paging: ... the ninth thing shows in slot 4 with its count",
                pOView.PageSlot(4).Item == lOThings[8] && pOView.PageSlot(4).Count == 4, true);
            fExpectBool("paging: ... the slots behind the end are empty", pOView.PageSlot(5).Item == null, true);
            fExpectBool("paging: ... now only the down arrow shows", !pOView.ShowsScrollUp && pOView.ShowsScrollDown, true);

            pOModel.ScrollContainer(UWInventoryView.ScrollDeltaOf(UWInventoryView.ControlEnum.ScrollDown));
            pOModel.CloseContainer();

            // Exactly eight: the up arrow shows once more, for a step to an empty row.
            lOSack.Contents.RemoveAt(8);
            pOModel.TryOpenContainer(lOSack);
            pOView.Refresh(pOModel, pOData);
            fExpectBool("paging: exactly eight things still show the up arrow", pOView.ShowsScrollUp, true);
            pOModel.ScrollContainer(UWInventoryView.ScrollStep);
            pOView.Refresh(pOModel, pOData);
            fExpectBool("paging: ... one step later only the down arrow", !pOView.ShowsScrollUp && pOView.ShowsScrollDown, true);

            pOModel.CloseContainer();
            pOView.Refresh(pOModel, pOData);
            fExpectBool("paging: closed, the page shows the backpack again",
                !pOView.ShowsContainer && pOView.PageSlot(2).Count == 3 && pOView.ContainerPicture == null
                && !pOView.ShowsScrollUp && !pOView.ShowsScrollDown, true);
            fExpectBool("paging: ... and the container window is empty", pOView.Container[0].Item == null, true);
        }

        /// <summary>What a click on the page's controls and slots does.</summary>
        private static void fCheckInventoryViewClicks(DataImport pOData, UWInventoryModel pOModel)
        {
            // The spots: the left arrow is the up one (it raises the offset), a row per click.
            fExpectBool("controls: the left arrow scrolls up, the right one down, the icon is the icon",
                UWInventoryView.ControlOf(UWClickRules.InventorySpot.ScrollLeft) == UWInventoryView.ControlEnum.ScrollUp
                && UWInventoryView.ControlOf(UWClickRules.InventorySpot.ScrollRight) == UWInventoryView.ControlEnum.ScrollDown
                && UWInventoryView.ControlOf(UWClickRules.InventorySpot.ContainerIcon) == UWInventoryView.ControlEnum.ContainerIcon, true);
            fExpectBool("controls: slots and the body are no control",
                UWInventoryView.ControlOf(UWClickRules.InventorySpot.Backpack0) == UWInventoryView.ControlEnum.None
                && UWInventoryView.ControlOf(UWClickRules.InventorySpot.Helmet) == UWInventoryView.ControlEnum.None
                && UWInventoryView.ControlOf(UWClickRules.InventorySpot.None) == UWInventoryView.ControlEnum.None, true);
            fExpectInt("controls: up pages a row of four", UWInventoryView.ScrollDeltaOf(UWInventoryView.ControlEnum.ScrollUp), 4);
            fExpectInt("controls: down pages back", UWInventoryView.ScrollDeltaOf(UWInventoryView.ControlEnum.ScrollDown), -4);
            fExpectInt("controls: the icon does not scroll", UWInventoryView.ScrollDeltaOf(UWInventoryView.ControlEnum.ContainerIcon), 0);
            fExpectInt("controls: a full pack's icon says message 259", UWInventoryView.NoPlaceMessage, 259);

            // The backpack slots' areas from the original's table, as the modern page rebuilt them.
            bool lbAreas = true;

            for (int liSlot = 0; liSlot < UWInventoryView.SlotCount; liSlot++)
            {
                UWClickRules.Area lOArea = UWClickRules.BackpackArea(liSlot);
                int liX0 = 240 + ((liSlot % 4) * 19);
                int liY0 = liSlot < 4 ? 101 : 83;

                lbAreas &= lOArea.X0 == liX0 && lOArea.Y0 == liY0 && lOArea.X1 == liX0 + 17 && lOArea.Y1 == liY0 + 17
                    && UWClickRules.InventorySpotAt(liX0 + 1, liY0 + 1) == UWClickRules.InventorySpot.Backpack0 + liSlot;
            }

            fExpectBool("areas: the eight backpack areas are the table's, 18 wide in steps of 19", lbAreas, true);
            fExpectBool("areas: no area past the eighth slot", UWClickRules.BackpackArea(8).X1 == 0 && UWClickRules.BackpackArea(-1).X1 == 0, true);

            int liBag = -1;

            for (int liId = 0x80; liId <= 0x8F && liBag < 0; liId++)
            {
                if (liId != UWObjectMechanics.RuneBagId && pOData.ObjectClassProperties != null
                    && pOData.ObjectClassProperties.TryGetContainer(liId, out UWObjectClassProperties.Container lOEntry)
                    && lOEntry.CapacityTenthStones > 0 && lOEntry.ObjectMask == UWObjectClassProperties.ContainerMaskAll)
                    liBag = liId;
            }

            fExpectBool("one level up: a plain container with a capacity", liBag >= 0, true);

            if (liBag < 0)
                return;

            // From a container opened in the pack: the first free backpack place, else no place.
            UWObject lOStone = fStack(UWObjectMechanics.SlingStoneObjectId, 1);

            for (int liSlot = 0; liSlot < pOModel.Backpack.Length; liSlot++)
                pOModel.Backpack[liSlot] = new UWObject(1);

            UWObject lOOuter = fBag(liBag);

            pOModel.TryOpenContainer(lOOuter);
            pOModel.BeginDragFromExternal(lOStone);
            fExpectBool("one level up: a full pack has no place", UWInventoryView.PutOneLevelUp(pOModel) == UWInventoryView.PutOutcomeEnum.NoPlace, true);
            fExpectBool("one level up: ... the stone stays on the pointer", pOModel.CursorItem == lOStone, true);

            pOModel.Backpack[5] = null;
            fExpectBool("one level up: a free place takes it", UWInventoryView.PutOneLevelUp(pOModel) == UWInventoryView.PutOutcomeEnum.Placed, true);
            fExpectBool("one level up: ... there, the container still open",
                pOModel.Backpack[5] == lOStone && pOModel.CursorItem == null && pOModel.OpenContainer == lOOuter, true);

            // From a container opened inside another: into that one.
            UWObject lOInner = fBag(liBag);

            lOOuter.Contents.Add(lOInner);
            pOModel.TryOpenContainer(lOInner);
            fExpectBool("one level up: the inner container's parent is the outer one", pOModel.ParentContainer == lOOuter, true);

            UWObject lOSecond = fStack(UWObjectMechanics.SlingStoneObjectId, 1);

            pOModel.BeginDragFromExternal(lOSecond);
            fExpectBool("one level up: from the inner container into the outer one",
                UWInventoryView.PutOneLevelUp(pOModel) == UWInventoryView.PutOutcomeEnum.Placed
                && lOOuter.Contents.Contains(lOSecond) && !lOInner.Contents.Contains(lOSecond), true);

            pOModel.CloseAllContainers();

            // A drop onto a backpack slot: a container there takes it, anything else swaps.
            UWObject lOThird = fStack(UWObjectMechanics.SlingStoneObjectId, 1);
            UWObject lOSlotBag = fBag(liBag);
            UWObject lODagger = pOModel.Backpack[0];

            pOModel.Backpack[1] = lOSlotBag;
            pOModel.BeginDragFromExternal(lOThird);
            fExpectBool("drop on a slot: a container there takes the thing",
                UWInventoryView.DropOnBackpackSlot(pOModel, 1) == UWInventoryView.PutOutcomeEnum.Placed
                && lOSlotBag.Contents.Contains(lOThird) && pOModel.Backpack[1] == lOSlotBag && pOModel.CursorItem == null, true);

            UWObject lOFourth = fStack(UWObjectMechanics.SlingStoneObjectId, 1);

            pOModel.BeginDragFromExternal(lOFourth);
            fExpectBool("drop on a slot: a dagger there is swapped",
                UWInventoryView.DropOnBackpackSlot(pOModel, 0) == UWInventoryView.PutOutcomeEnum.Placed
                && pOModel.Backpack[0] == lOFourth && pOModel.CursorItem == lODagger, true);

            pOModel.Backpack[2] = new UWObject((ushort)UWObjectMechanics.RuneBagId);
            fExpectBool("drop on a slot: the rune bag refuses a dagger",
                UWInventoryView.DropOnBackpackSlot(pOModel, 2) == UWInventoryView.PutOutcomeEnum.Refused
                && pOModel.LastContainerRejection == UWContainerCapacity.ResultEnum.RunesOnly && pOModel.CursorItem == lODagger, true);

            UWModelReset(pOModel);
        }

        /// <summary>The index of a body slot among UWPaperdollLayout.IconSlots, -1 for none.</summary>
        private static int fIconSlotOf(UWArmorItemMap.BodySlot peSlot)
        {
            for (int liAt = 0; liAt < UWPaperdollLayout.IconSlots.Length; liAt++)
            {
                if (UWPaperdollLayout.IconSlots[liAt].Slot == peSlot)
                    return liAt;
            }

            return -1;
        }
    }
}
