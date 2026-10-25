using System;
using System.Collections.Generic;
using System.Reflection;
using UWDataImport;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// 2026-10-19 (cleanup 5.3): UWClassicLayout, the classic screen's layout table. Every entry
    /// carries its source tag; the table is consistent in itself (the pieces lie on the screen,
    /// the bands and cuts ascend, the derived entries follow their sources); the art builders read
    /// the same numbers (UWHudArt, UWAutomapPainter); with the data, the colours the table names
    /// are the palette's.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckClassicLayout()
        {
            Check.Group("Classic layout (UWClassicLayout - the classic screen's places, sizes and colours)");

            fCheckClassicLayoutTags();
            fCheckClassicLayoutConsistency();
            fCheckClassicLayoutUsers();
        }

        /// <summary>Every public field of every area carries exactly one source tag, with a note;
        /// a user-tuned one starts with its date.</summary>
        private static void fCheckClassicLayoutTags()
        {
            int liFields = 0;
            List<string> lOProblems = new List<string>();

            foreach (Type lOArea in typeof(UWClassicLayout).GetNestedTypes(BindingFlags.Public))
            {
                if (typeof(Attribute).IsAssignableFrom(lOArea))
                    continue;

                foreach (FieldInfo lOField in lOArea.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    liFields++;

                    string lsName = lOArea.Name + "." + lOField.Name;
                    object[] lOTags = lOField.GetCustomAttributes(typeof(UWClassicLayout.SourceAttribute), false);

                    if (lOTags.Length != 1)
                    {
                        lOProblems.Add(lsName + " has " + lOTags.Length + " tags");

                        continue;
                    }

                    UWClassicLayout.SourceAttribute lOTag = (UWClassicLayout.SourceAttribute)lOTags[0];

                    if (string.IsNullOrWhiteSpace(lOTag.Note))
                        lOProblems.Add(lsName + " has an empty note");
                    else if (lOTag is UWClassicLayout.UserTunedAttribute
                        && !(lOTag.Note.StartsWith("20") || lOTag.Note.StartsWith("before 20")))
                        lOProblems.Add(lsName + " is user-tuned without a date");
                }
            }

            if (lOProblems.Count == 0)
                Check.Pass("layout: every entry tagged", liFields + " entries, each with one source and a note");
            else
                Check.Fail("layout: every entry tagged", string.Join("; ", lOProblems));

            Check.Bool("layout: the table has entries", liFields > 100, true);
        }

        private static bool fOnScreen(UWRectInt pORect)
        {
            return pORect.X >= 0 && pORect.Y >= 0 && pORect.Width > 0 && pORect.Height > 0
                && pORect.X + pORect.Width <= UWClassicLayout.Screen.Width
                && pORect.Y + pORect.Height <= UWClassicLayout.Screen.Height;
        }

        private static void fCheckClassicLayoutConsistency()
        {
            // The frame and its hole.
            fExpectInt("layout: the hole is 176 wide", UWClassicLayout.Frame.WindowHoleWidth, 176);
            fExpectInt("layout: ... and 121 high", UWClassicLayout.Frame.WindowHoleHeight, 121);
            fExpectInt("layout: inclusive 177 x 122", UWClassicLayout.Frame.WindowHoleWidthInclusive * 1000
                + UWClassicLayout.Frame.WindowHoleHeightInclusive, 177122);

            UWRectInt lOHole = UWClassicLayout.ClassicWide.Hole;

            Check.Bool("layout: Classic Wide's hole lies in the window hole",
                lOHole.X >= UWClassicLayout.Frame.WindowHoleLeft && lOHole.Y >= UWClassicLayout.Frame.WindowHoleTop
                && lOHole.X + lOHole.Width - 1 <= UWClassicLayout.Frame.WindowHoleRight
                && lOHole.Y + lOHole.Height - 1 <= UWClassicLayout.Frame.WindowHoleBottom, true);
            Check.Bool("layout: the gargoyle's head on the screen", fOnScreen(UWClassicLayout.Frame.GargoyleHead), true);

            // Classic Wide's bands ascend; each band's two cuts ascend and lie on the screen; the
            // view's band cuts inside the hole's black columns.
            Check.Bool("layout: Classic Wide's bands ascend",
                0 < UWClassicLayout.ClassicWide.TopRows && UWClassicLayout.ClassicWide.TopRows < UWClassicLayout.ClassicWide.LedgeTop
                && UWClassicLayout.ClassicWide.LedgeTop < UWClassicLayout.ClassicWide.ScrollTop
                && UWClassicLayout.ClassicWide.ScrollTop < UWClassicLayout.Screen.Height, true);
            fExpectInt("layout: four bands of cuts", UWClassicLayout.ClassicWide.Cuts.GetLength(0) * 10
                + UWClassicLayout.ClassicWide.Cuts.GetLength(1), 42);

            bool lbCuts = true;

            for (int liBand = 0; liBand < UWClassicLayout.ClassicWide.Cuts.GetLength(0); liBand++)
                lbCuts &= 0 < UWClassicLayout.ClassicWide.Cuts[liBand, 0]
                    && UWClassicLayout.ClassicWide.Cuts[liBand, 0] < UWClassicLayout.ClassicWide.Cuts[liBand, 1]
                    && UWClassicLayout.ClassicWide.Cuts[liBand, 1] < UWClassicLayout.Screen.Width;

            Check.Bool("layout: each band's cuts ascend on the screen", lbCuts, true);
            Check.Bool("layout: the view's band cuts within the hole",
                UWClassicLayout.ClassicWide.Cuts[1, 0] > lOHole.X
                && UWClassicLayout.ClassicWide.Cuts[1, 1] <= lOHole.X + lOHole.Width, true);

            // The panel and the page on it.
            Check.Bool("layout: the panel on the screen", fOnScreen(new UWRectInt(UWClassicLayout.Panel.Left,
                UWClassicLayout.Panel.Top, UWClassicLayout.Panel.Width, UWClassicLayout.Panel.Height)), true);
            fExpectInt("layout: the paperdoll page starts at the panel's left", UWClassicLayout.Paperdoll.Page.X, UWClassicLayout.Panel.Left);
            fExpectInt("layout: ... and top", UWClassicLayout.Paperdoll.Page.Y, UWClassicLayout.Panel.Top);
            Check.Bool("layout: the page within the panel",
                UWClassicLayout.Paperdoll.Page.Width <= UWClassicLayout.Panel.Width
                && UWClassicLayout.Paperdoll.Page.Height <= UWClassicLayout.Panel.Height, true);

            // The backpack grid on the panel; the container panel's corner from the prefab values.
            int liGridRight = UWClassicLayout.Backpack.GridLeft
                + (UWClassicLayout.Backpack.Columns * UWClassicLayout.Backpack.SlotSize)
                + ((UWClassicLayout.Backpack.Columns - 1) * UWClassicLayout.Backpack.SpacingX);

            Check.Bool("layout: the backpack grid within the panel",
                UWClassicLayout.Backpack.GridLeft >= UWClassicLayout.Panel.Left
                && liGridRight <= UWClassicLayout.Panel.Left + UWClassicLayout.Panel.Width, true);
            fExpectInt("layout: the backpack holds eight", UWClassicLayout.Backpack.Columns * UWClassicLayout.Backpack.Rows, 8);

            double ldContainerLeft = (UWClassicLayout.Screen.Width / 2.0) + UWClassicLayout.Container.PanelAnchoredX
                - ((UWClassicLayout.Screen.Width + UWClassicLayout.Container.PanelSizeDeltaX) / 2.0);
            double ldContainerTop = (UWClassicLayout.Screen.Height / 2.0) - UWClassicLayout.Container.PanelAnchoredY
                - ((UWClassicLayout.Screen.Height + UWClassicLayout.Container.PanelSizeDeltaY) / 2.0);

            Check.Near("layout: the container panel's left from the prefab values", ldContainerLeft, UWClassicLayout.Container.FrameLeft, 0.01);
            Check.Near("layout: ... and its top", ldContainerTop, UWClassicLayout.Container.FrameTop, 0.01);

            // The rune hollow holds the three runes; the shelf's grid lies on the panel.
            Check.Bool("layout: three 14-pixel runes fit the hollow",
                UWClassicLayout.RuneHollow.RuneLeft > UWClassicLayout.RuneHollow.Left
                && UWClassicLayout.RuneHollow.RuneLeft + (2 * UWClassicLayout.RuneHollow.RunePitch) + UWClassicLayout.RuneShelf.Cell
                    <= UWClassicLayout.RuneHollow.Left + UWClassicLayout.RuneHollow.Width, true);
            Check.Bool("layout: the rune shelf's grid within the panel",
                UWClassicLayout.RuneShelf.SlotLeft + ((UWClassicLayout.RuneShelf.Columns - 1) * UWClassicLayout.RuneShelf.PitchX)
                    + UWClassicLayout.RuneShelf.Cell <= UWClassicLayout.Panel.Width
                && UWClassicLayout.RuneShelf.ClearTop < UWClassicLayout.RuneShelf.ClearBottom
                && UWClassicLayout.RuneShelf.ClearBottom <= UWClassicLayout.Panel.Height, true);

            // The stats page's paging spans the panel's width in its bottom 8 rows.
            fExpectInt("layout: the paging areas meet at 37", UWClassicLayout.Stats.PageBack.X + UWClassicLayout.Stats.PageBack.Width,
                UWClassicLayout.Stats.PageForward.X);
            fExpectInt("layout: ... and end at the panel's edge", UWClassicLayout.Stats.PageForward.X + UWClassicLayout.Stats.PageForward.Width,
                UWClassicLayout.Panel.Width);
            fExpectInt("layout: ... in its bottom 8 rows", UWClassicLayout.Stats.PageBack.Y + UWClassicLayout.Stats.PageBack.Height,
                UWClassicLayout.Panel.Height);

            // The message scroll and its colours; the conversation's answers in the same box.
            Check.Bool("layout: the message box on the screen", fOnScreen(new UWRectInt(UWClassicLayout.MessageLog.Left,
                UWClassicLayout.MessageLog.Top, UWClassicLayout.MessageLog.Width, UWClassicLayout.MessageLog.Height)), true);
            fExpectInt("layout: seven colour codes", UWClassicLayout.MessageLog.ColourIndices.Length, 7);
            fExpectInt("layout: the edges' centre row is the box's middle", (int)(UWClassicLayout.MessageLog.EdgeCentreY * 2),
                (2 * UWClassicLayout.MessageLog.Top) + (int)UWClassicLayout.MessageLog.EdgeHeight);
            fExpectInt("layout: the answers in the message box", UWClassicLayout.Conversation.ChoicesTop * 1000 + UWClassicLayout.Conversation.ChoicesLeft,
                UWClassicLayout.MessageLog.Top * 1000 + UWClassicLayout.MessageLog.Left);

            // The compass's needles; the dragons' pairs.
            fExpectInt("layout: sixteen needles left", UWClassicLayout.Compass.NeedleLeft.Length, 16);
            fExpectInt("layout: ... and top", UWClassicLayout.Compass.NeedleTop.Length, 16);
            Check.Bool("layout: the compass on the ledge band",
                UWClassicLayout.Compass.Top >= UWClassicLayout.ClassicWide.LedgeTop
                && UWClassicLayout.Compass.Top < UWClassicLayout.ClassicWide.ScrollTop, true);
            fExpectInt("layout: two dragons' necks, heads and tails", UWClassicLayout.Dragons.Neck.Length
                + UWClassicLayout.Dragons.Head.Length + UWClassicLayout.Dragons.Tail.Length, 6);
            fExpectInt("layout: two sides by three animations", UWClassicLayout.Dragons.Overlay.Length, 6);

            // The map: the tiles' block on the screen, its top as far down as its bottom is up.
            int liBlock = UWWorldScale.TilesPerAxis * UWClassicLayout.Map.TileSize;

            fExpectInt("layout: the map's tiles end above the bottom by CellBottom",
                UWClassicLayout.Map.TilesTop + liBlock, UWClassicLayout.Screen.Height - UWClassicLayout.Map.CellBottom);
            Check.Bool("layout: ... and fit the width", UWClassicLayout.Map.CellLeft + liBlock <= UWClassicLayout.Screen.Width, true);
            Check.Bool("layout: the map's click areas on the screen",
                fOnScreen(UWClassicLayout.Map.Close) && fOnScreen(UWClassicLayout.Map.PageUp)
                && fOnScreen(UWClassicLayout.Map.PageDown) && fOnScreen(UWClassicLayout.Map.Erase), true);

            // The conversation: the parchment under the header, the trade tables as long as the trade.
            fExpectInt("layout: the parchment starts under the header", UWClassicLayout.Conversation.Parchment.Y,
                UWClassicLayout.Conversation.Header.Y + UWClassicLayout.Conversation.Header.Height);
            Check.Bool("layout: header and parchment on the screen",
                fOnScreen(UWClassicLayout.Conversation.Header) && fOnScreen(UWClassicLayout.Conversation.Parchment), true);
            fExpectInt("layout: the name frame at the header's left", UWClassicLayout.Conversation.NpcNameFrameLeft,
                UWClassicLayout.Conversation.Header.X);
            fExpectInt("layout: four trade slots each", UWClassicLayout.Conversation.NpcTradeSlots.Length
                + UWClassicLayout.Conversation.PlayerTradeSlots.Length, 2 * UWConversationTrade.SlotCount);
            fExpectInt("layout: four trade crosses each", UWClassicLayout.Conversation.NpcTradeCrosses.Length
                + UWClassicLayout.Conversation.PlayerTradeCrosses.Length, 2 * UWConversationTrade.SlotCount);
            fExpectInt("layout: three vine edges", UWClassicLayout.Conversation.EdgeTops.Length, 3);
            fExpectInt("layout: the history's 13 lines of six", UWClassicLayout.Conversation.HistoryHeight / 6, 13);

            // The options' entries fit the panel picture (35 x 108).
            Check.Bool("layout: seven option entries on the 108-row panel",
                UWClassicLayout.Options.EntryTop + ((UWClassicLayout.Options.EntryCount - 1) * UWClassicLayout.Options.EntryPitch) + 14
                    <= UWClassicLayout.Options.PanelTop + 108, true);
            fExpectInt("layout: four main menu buttons", UWClassicLayout.MainMenu.Buttons.Length, 4);
        }

        /// <summary>The engine-free users read the table's numbers (the Unity users name the same
        /// entries; the compile is their check).</summary>
        private static void fCheckClassicLayoutUsers()
        {
            fExpectInt("layout users: the automap's screen", UWAutomapPainter.ScreenWidth * 1000 + UWAutomapPainter.ScreenHeight,
                UWClassicLayout.Screen.Width * 1000 + UWClassicLayout.Screen.Height);
            fExpectInt("layout users: the automap's cell", UWAutomapPainter.TileSize * 100 + UWAutomapPainter.CellLeft * 10 + UWAutomapPainter.CellBottom,
                UWClassicLayout.Map.TileSize * 100 + UWClassicLayout.Map.CellLeft * 10 + UWClassicLayout.Map.CellBottom);

            fExpectInt("layout users: the rune hollow's left", UWHudArt.RuneHollowX, UWClassicLayout.RuneHollow.Left);
            fExpectInt("layout users: the first rune in it", UWHudArt.RuneHollowX + UWHudArt.RuneHollowRuneX, UWClassicLayout.RuneHollow.RuneLeft);
            fExpectInt("layout users: ... and its row", UWHudArt.RuneHollowY + UWHudArt.RuneHollowRuneY, UWClassicLayout.RuneHollow.RuneTop);
            fExpectInt("layout users: the compass disc's top", UWHudArt.CompassDiscY, UWClassicLayout.Compass.Top);
            fExpectInt("layout users: the paperdoll's body", UWHudArt.PaperdollBodyX * 1000 + UWHudArt.PaperdollBodyY,
                UWClassicLayout.Paperdoll.BodyLeft * 1000 + UWClassicLayout.Paperdoll.BodyTop);
            Check.Bool("layout users: the paperdoll page", fSameRect(UWHudArt.PaperdollPage, UWClassicLayout.Paperdoll.Page), true);
            Check.Bool("layout users: the conversation header", fSameRect(UWHudArt.ConversationHeader, UWClassicLayout.Conversation.Header), true);
            Check.Bool("layout users: the conversation parchment", fSameRect(UWHudArt.ConversationParchment, UWClassicLayout.Conversation.Parchment), true);
            Check.Bool("layout users: Classic Wide's hole", fSameRect(UWHudArt.ClassicWideHole, UWClassicLayout.ClassicWide.Hole), true);
            fExpectInt("layout users: Classic Wide's camera", UWHudArt.ClassicWideCameraLeft * 1000 + UWHudArt.ClassicWideCameraRight,
                UWClassicLayout.Frame.WindowHoleLeft * 1000 + UWClassicLayout.Frame.WindowHoleRight);
            fExpectInt("layout users: ... rows", UWHudArt.ClassicWideCameraTop * 1000 + UWHudArt.ClassicWideCameraBottom,
                UWClassicLayout.Frame.WindowHoleTop * 1000 + UWClassicLayout.Frame.WindowHoleBottom);

            int[] liBandRows = { 0, UWClassicLayout.ClassicWide.TopRows, UWClassicLayout.ClassicWide.LedgeTop, UWClassicLayout.ClassicWide.ScrollTop };
            bool lbCuts = true;
            bool lbShift = true;

            for (int liBand = 0; liBand < liBandRows.Length; liBand++)
            {
                int liCut0 = UWClassicLayout.ClassicWide.Cuts[liBand, 0];
                int liCut1 = UWClassicLayout.ClassicWide.Cuts[liBand, 1];

                lbCuts &= UWHudArt.ClassicWideCut(liBandRows[liBand], 0) == liCut0 && UWHudArt.ClassicWideCut(liBandRows[liBand], 1) == liCut1;

                // The shift jumps exactly at the cuts: none left of the first, half between, all right of the second.
                lbShift &= UWHudArt.ClassicWideShiftX(liCut0 - 1, liBandRows[liBand], 100) == 0
                    && UWHudArt.ClassicWideShiftX(liCut0, liBandRows[liBand], 100) == 50
                    && UWHudArt.ClassicWideShiftX(liCut1 - 1, liBandRows[liBand], 100) == 50
                    && UWHudArt.ClassicWideShiftX(liCut1, liBandRows[liBand], 100) == 100;
            }

            Check.Bool("layout users: Classic Wide's cuts by band", lbCuts, true);
            Check.Bool("layout users: Classic Wide's shift jumps at the cuts", lbShift, true);
            fExpectInt("layout users: the compass moves with the ledge's middle",
                UWHudArt.ClassicWideShiftX(UWClassicLayout.Compass.Left, UWClassicLayout.Compass.Top, 100), 50);
        }

        private static bool fSameRect(UWRectInt pOA, UWRectInt pOB)
        {
            return pOA.X == pOB.X && pOA.Y == pOB.Y && pOA.Width == pOB.Width && pOA.Height == pOB.Height;
        }

        /// <summary>With the data: the colours the table names as RGB are the palette's at the
        /// indices the classic screen draws them with.</summary>
        private static void fCheckClassicLayoutColours(DataImport pOData)
        {
            Check.Group("Classic layout colours (UWClassicLayout against palette 0)");

            UWPalette lOPalette = pOData != null && pOData.Palettes != null ? pOData.Palettes.GetPalette(0) : null;

            if (lOPalette == null)
            {
                Check.Fail("layout colours: palette 0", "not loaded");

                return;
            }

            Check.Str("layout colours: the partner's text is CONV.BYT's sample index",
                fHex(lOPalette.GetRGB(UWClassicLayout.Conversation.SampleTextIndex)), fHex(UWClassicLayout.Conversation.NpcText));
            Check.Str("layout colours: ... and the message log's code 0",
                fHex(lOPalette.GetRGB(UWClassicLayout.MessageLog.ColourIndices[0])), fHex(UWClassicLayout.Conversation.NpcText));
            Check.Str("layout colours: the names' index and the modern look's colour",
                fHex(lOPalette.GetRGB(UWClassicLayout.Conversation.NameColourIndex)), fHex(UWClassicLayout.Conversation.NameColour));
        }

        private static string fHex(byte[] pyRgb)
        {
            return pyRgb == null || pyRgb.Length < 3 ? "none" : pyRgb[0].ToString("X2") + pyRgb[1].ToString("X2") + pyRgb[2].ToString("X2");
        }

        private static string fHex(UWColor32 pOColour)
        {
            return pOColour.R.ToString("X2") + pOColour.G.ToString("X2") + pOColour.B.ToString("X2");
        }
    }
}
