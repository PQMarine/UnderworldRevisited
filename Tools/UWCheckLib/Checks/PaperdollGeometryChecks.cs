using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE PAPERDOLL'S MEASURES PER GAME (UWPaperdollGeometry; UW2 plan, the modern interface's part U4b step 2,
    /// 2026-11-23): the modern character panel draws and clicks by them on both games. Each game's table must agree
    /// with itself - a place's picture lies where a click finds that place - and the first game's must still be the
    /// numbers the panel had before (UWPaperdollLayout, the widened page).
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckPaperdollGeometry()
        {
            Check.Group("Paperdoll geometry: the page's measures of both games (UWPaperdollGeometry)");

            fCheckPaperdollGame("uw1", UWPaperdollGeometry.Uw1);
            fCheckPaperdollGame("uw2", UWPaperdollGeometry.Uw2);

            UWPaperdollGeometry lOFirst = UWPaperdollGeometry.Uw1;

            Check.Int("uw1: the page as wide as the backpack window", lOFirst.PageWidth, 86);
            Check.Int("uw1: the widening's shift", lOFirst.Shift, 2);
            Check.Int("uw1: the helmet's place", (lOFirst.ArmourX[UWPaperdollGeometry.Helmet] * 1000) + lOFirst.ArmourY[UWPaperdollGeometry.Helmet],
                (UWPaperdollLayout.HelmetX * 1000) + UWPaperdollLayout.HelmetY);
            Check.Int("uw1: the boots' place", (lOFirst.ArmourX[UWPaperdollGeometry.Boots] * 1000) + lOFirst.ArmourY[UWPaperdollGeometry.Boots],
                (UWPaperdollLayout.BootsX * 1000) + UWPaperdollLayout.BootsY);
            Check.Int("uw1: a column left of the widening is the screen's", lOFirst.ColumnToScreen(0), lOFirst.Page.X);

            UWPaperdollGeometry lOSecond = UWPaperdollGeometry.Uw2;

            Check.Int("uw2: nothing widened", lOSecond.PageWidth, lOSecond.Page.Width);
            Check.Int("uw2: a column is the screen's", lOSecond.ColumnToScreen(10), lOSecond.Page.X + 10);
            // ArmorSlots draws leggings, chest, boots, gloves, helmet.
            Check.Int("uw2: the leggings drawn first", lOSecond.ArmourOrder[0], UWPaperdollGeometry.Legs);
            Check.Int("uw2: the helmet drawn last", lOSecond.ArmourOrder[UWPaperdollGeometry.ArmourPieces - 1], UWPaperdollGeometry.Helmet);
        }

        /// <summary>
        /// THE WHOLE PAGE PER GAME (UW2 plan stage U5 step 2, 2026-11-23): the modern original character page
        /// (UWModernInventoryPage) draws and clicks the backpack, the weight and the open bag by these. The first game's
        /// must be the places the page had as constants (UWClickRules' table, the container view of UWHudInventory);
        /// the second game's are InvDisplay's, and every point of its page is the spot FindInventoryHit gives,
        /// written out here as a list of the displays' spots.
        /// </summary>
        private static void fCheckCharacterPageGeometry()
        {
            Check.Group("Character page per game: backpack, weight, open bag (UWPaperdollGeometry - UW2 plan stage U5 step 2)");

            UWPaperdollGeometry lOFirst = UWPaperdollGeometry.Uw1;
            UWPaperdollGeometry lOSecond = UWPaperdollGeometry.Uw2;
            System.Globalization.CultureInfo lOInvariant = System.Globalization.CultureInfo.InvariantCulture;

            Check.Str("uw1: the page at 236/7, the weight's middle at 305/62", lOFirst.PanelLeft + "/" + lOFirst.PanelTop + " "
                + lOFirst.WeightCentreX.ToString(lOInvariant) + "/" + lOFirst.WeightCentreRow.ToString(lOInvariant), "236/7 305/62");
            Check.Str("uw1: the backpack's middles as the page centred them (the odd one a pixel left)",
                lOFirst.BackpackCentreX[0].ToString(lOInvariant) + "/" + lOFirst.BackpackCentreRow[0].ToString(lOInvariant) + " "
                + lOFirst.BackpackCentreX[6].ToString(lOInvariant) + "/" + lOFirst.BackpackCentreRow[6].ToString(lOInvariant), "249/90 286/108");
            Check.Str("uw1: the boxes are the table's areas", fBox(lOFirst.BackpackBoxes[0]) + " " + fBox(lOFirst.BackpackBoxes[7]), "240,81,18,18 297,99,18,18");
            Check.Str("uw1: the container view: the back, the icon, the arrows (8 wide each)",
                lOFirst.BagBackPicture + "@" + lOFirst.BagBackLeft + "/" + lOFirst.BagBackTop + " " + lOFirst.BagIconLeft + "/" + lOFirst.BagIconBottom + " "
                + lOFirst.UpArrowPicture + "," + lOFirst.DownArrowPicture + "@" + lOFirst.ArrowLeft(true, 8, 8) + "," + lOFirst.ArrowLeft(false, 8, 8) + "/" + lOFirst.ArrowBottom,
                "6@236/80 241/80 27,28@295,306/80");

            Check.Str("uw2: the page at 0xEB/7, the weight centred on 0x131 under row 56 in colour 0x88",
                lOSecond.PanelLeft + "/" + lOSecond.PanelTop + " " + lOSecond.WeightCentreX.ToString(lOInvariant) + "/"
                + lOSecond.WeightCentreRow.ToString(lOInvariant) + " " + lOSecond.WeightColourIndex, "235/7 305/59 136");
            Check.Str("uw2: the backpack's middles, InvDisplay 12 and 19 plus half their 16",
                lOSecond.BackpackCentreX[0].ToString(lOInvariant) + "/" + lOSecond.BackpackCentreRow[0].ToString(lOInvariant) + " "
                + lOSecond.BackpackCentreX[7].ToString(lOInvariant) + "/" + lOSecond.BackpackCentreRow[7].ToString(lOInvariant), "247/89 304/107");
            Check.Str("uw2: the boxes are the displays' hit rectangles", fBox(lOSecond.BackpackBoxes[0]) + " " + fBox(lOSecond.BackpackBoxes[7]),
                "238,78,18,18 295,96,18,18");
            Check.Str("uw2: the open bag: INV.GR 6 at 0xEE/0x7A, the bag on display 20, the arrows 21 and 22",
                lOSecond.BagBackPicture + "@" + lOSecond.BagBackLeft + "/" + lOSecond.BagBackTop + " " + lOSecond.BagIconLeft + "/" + lOSecond.BagIconBottom + " "
                + lOSecond.UpArrowPicture + "," + lOSecond.DownArrowPicture + "@" + lOSecond.ArrowLeft(true, 8, 8) + "," + lOSecond.ArrowLeft(false, 8, 8) + "/" + lOSecond.ArrowBottom,
                "6@238/77 239/77 27,28@294,304/76");

            // Every point of both screens: the first game's spot is its table's; the second game's FindInventoryHit's, the
            // displays named by the first game's spots (1 leggings, 2 helmet, 3 chest, 4 gloves, 5 boots, 6 to 11 the
            // shoulders, hands and rings by the slot SlotAt finds, 12 to 19 the backpack, 20 the bag, 21 up, 22 down).
            UWClickRules.InventorySpot[] leDisplays =
            {
                UWClickRules.InventorySpot.None, UWClickRules.InventorySpot.Legs, UWClickRules.InventorySpot.Helmet, UWClickRules.InventorySpot.Chest,
                UWClickRules.InventorySpot.Gloves, UWClickRules.InventorySpot.Boots
            };
            int liFirstWrong = 0;
            int liSecondWrong = 0;
            int liSlotWrong = 0;
            int[] liCount = new int[(int)UWClickRules.InventorySpot.ScrollRight + 1];

            for (int liRow = 0; liRow < 200; liRow++)
            {
                for (int liX = 0; liX < 320; liX++)
                {
                    if (lOFirst.SpotAt(liX, liRow) != UWClickRules.InventorySpotAt(liX, 199 - liRow))
                        liFirstWrong++;

                    UWClickRules.InventorySpot leSpot = lOSecond.SpotAt(liX, liRow);
                    int liY = 199 - liRow;
                    int liHit = 0;

                    for (int liDisplay = 1; liDisplay <= 22 && liHit == 0; liDisplay++)
                    {
                        if (liX >= UWClassicLayoutUw2.Inventory.HitLeft[liDisplay] && liX <= UWClassicLayoutUw2.Inventory.HitRight[liDisplay]
                            && liY >= UWClassicLayoutUw2.Inventory.HitBottom[liDisplay] && liY <= UWClassicLayoutUw2.Inventory.HitTop[liDisplay])
                            liHit = liDisplay;
                    }

                    if (liHit >= 6 && liHit <= 11)
                    {
                        UWArmorItemMap.BodySlot? lePlace = lOSecond.SlotAt(liX, liRow);

                        if (!lePlace.HasValue || UWClickRules.BodySlotOf(leSpot) != lePlace)
                            liSlotWrong++;
                    }
                    else
                    {
                        UWClickRules.InventorySpot leExpected = liHit == 0 ? UWClickRules.InventorySpot.None
                            : liHit < leDisplays.Length ? leDisplays[liHit]
                            : liHit <= 19 ? UWClickRules.InventorySpot.Backpack0 + (liHit - 12)
                            : liHit == 20 ? UWClickRules.InventorySpot.ContainerIcon
                            : liHit == 21 ? UWClickRules.InventorySpot.ScrollLeft
                            : UWClickRules.InventorySpot.ScrollRight;

                        if (leSpot != leExpected)
                            liSecondWrong++;
                    }

                    liCount[(int)leSpot]++;
                }
            }

            Check.Int("uw1: every point of the screen the table's spot", liFirstWrong, 0);
            Check.Int("uw2: every point of the screen FindInventoryHit's display", liSecondWrong, 0);
            Check.Int("uw2: the shoulders, hands and rings the places SlotAt finds", liSlotWrong, 0);
            // The arrows' top row (y 0x85) is the right ring's already, display 11 being found first: 10 x 9, the down
            // arrow's last two columns past the ring's 0x136 keeping it.
            Check.Str("uw2: the backpack's first and last place 18 x 18, the arrows 10 x 10 less the ring's row",
                liCount[(int)UWClickRules.InventorySpot.Backpack0] + " " + liCount[(int)UWClickRules.InventorySpot.Backpack7] + " "
                + liCount[(int)UWClickRules.InventorySpot.ScrollLeft] + " " + liCount[(int)UWClickRules.InventorySpot.ScrollRight], "324 324 90 92");
        }

        private static string fBox(UWRectInt pOBox)
        {
            return pOBox.X + "," + pOBox.Y + "," + pOBox.Width + "," + pOBox.Height;
        }

        private static void fCheckPaperdollGame(string psGame, UWPaperdollGeometry pOGeometry)
        {
            Check.Int(psGame + ": six places with a thing's own picture", pOGeometry.IconSlots.Length, UWPaperdollLayout.IconSlots.Length);

            for (int liSlot = 0; liSlot < pOGeometry.IconSlots.Length; liSlot++)
            {
                UWPaperdollLayout.IconSlot lOSlot = pOGeometry.IconSlots[liSlot];
                UWArmorItemMap.BodySlot? leHit = pOGeometry.SlotAt((int)(lOSlot.X + (lOSlot.Size * 0.5f)), (int)(lOSlot.Y + (lOSlot.Size * 0.5f)));

                Check.Bool(psGame + ": the places in the first game's order, place " + liSlot, lOSlot.Slot == UWPaperdollLayout.IconSlots[liSlot].Slot, true);
                Check.Bool(psGame + ": a click on " + lOSlot.Slot + "'s picture finds it", leHit.HasValue && leHit.Value == lOSlot.Slot, true);
                Check.Bool(psGame + ": " + lOSlot.Slot + " lies on the page", lOSlot.X >= pOGeometry.Page.X && lOSlot.Y >= pOGeometry.Page.Y
                    && lOSlot.X + lOSlot.Size <= pOGeometry.Page.X + pOGeometry.Page.Width && lOSlot.Y + lOSlot.Size <= pOGeometry.Page.Y + pOGeometry.Page.Height, true);
            }

            Check.Bool(psGame + ": the body lies on the page", pOGeometry.BodyX >= pOGeometry.Page.X && pOGeometry.BodyY >= pOGeometry.Page.Y
                && pOGeometry.BodyX < pOGeometry.Page.X + pOGeometry.Page.Width, true);
            Check.Bool(psGame + ": the figure's middle inside the page", pOGeometry.FigureAxisX > pOGeometry.Page.X
                && pOGeometry.FigureAxisX < pOGeometry.Page.X + pOGeometry.Page.Width, true);
            Check.Bool(psGame + ": nothing beside the page is a place", pOGeometry.SlotAt(pOGeometry.Page.X - 20, pOGeometry.Page.Y + 20).HasValue, false);
        }
    }
}
