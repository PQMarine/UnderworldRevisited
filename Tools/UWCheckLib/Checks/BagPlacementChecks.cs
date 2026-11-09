using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE BAG WINDOWS AND A HIGH CHARACTER PANEL (per user, 2026-11-08, with a screenshot): the
    /// backpack bottom right, the panel open above it. The first bag window fits between them and goes
    /// above the backpack; the next would lie on the panel, so it starts a new column AT THE BOTTOM,
    /// left of the first - until then it went on beside the panel at the height reached. A column that
    /// only grazes the panel's edge moves the little way left and goes on.
    /// </summary>
    public static partial class SelfChecks
    {
        private static void fCheckBagPlacement()
        {
            Check.Group("Bags: a new column at the bottom where the panel blocks the way up (UWBagPlacement)");

            const float Scale = 3f;
            const int WindowWidth = 90;
            float lfPackWidth = WindowWidth * Scale;
            float lfMargin = 4f * Scale;
            UWRect lOPack = new UWRect(1920f - lfMargin - lfPackWidth, lfMargin, lfPackWidth, 64f * Scale);

            UWBagPlacement.Result lOResult = UWBagPlacement.Place(new UWBagPlacement.Input
            {
                ScreenWidth = 1920f,
                ScreenHeight = 1080f,
                Scale = Scale,
                PixelScale = Scale,
                Pack = lOPack,
                PackPlaced = false,
                PackWindowHeight = 58,
                WindowWidth = WindowWidth,
                BagHeights = new[] { 50, 90, 60, 90 },
                // The open panel at the right edge, from well above the backpack to the top.
                Panel = new UWRect(1920f - (110f * Scale), 600f, 110f * Scale, 1080f - 600f - (14f * Scale)),
                Minimap = new UWRect(0f, 0f, 0f, 0f),
                ActionBar = new UWRect(0f, 0f, 0f, 0f),
                ScreenMargin = 4,
                WindowGap = 4,
                LoadBarRows = 2
            });

            UWBagPlacement.Spot[] lOSpots = lOResult.Bags;

            Check.Str("bags: the backpack stays bottom right (the panel is above it)",
                lOResult.Pack.X + " " + lOResult.Pack.Y, lOPack.X + " " + lOPack.Y);
            Check.Bool("bags: the first window above the backpack, in its column, below the panel",
                lOSpots[0].Right == lOPack.XMax && lOSpots[0].Bottom > lOPack.YMax && lOSpots[0].Bottom + (50f * Scale) <= 600f, true);
            Check.Bool("bags: the second starts a new column at the bottom, left of the first",
                lOSpots[1].Right < lOPack.XMax - lfPackWidth + 1f && lOSpots[1].Bottom == lOPack.Y, true);
            Check.Bool("bags: the third goes above the second in its column",
                lOSpots[2].Right == lOSpots[1].Right && lOSpots[2].Bottom > lOSpots[1].Bottom, true);
            // The new column only grazes the panel's edge: the fourth moves the little way left of it
            // and stays on the column (no third column at the bottom).
            Check.Str("bags: the fourth, grazing the panel, moves just left of it above the third",
                lOSpots[3].Right + " " + (lOSpots[3].Bottom > lOSpots[2].Bottom), (1920f - (110f * Scale) - lfMargin) + " True");

            // The help tab: the panel wide and down to the bottom pushes the backpack left onto the
            // action bar - it climbs over the bar.
            UWRect lOBar = new UWRect(900f, lfMargin, 700f, 30f * Scale);
            UWBagPlacement.Result lOHelp = UWBagPlacement.Place(new UWBagPlacement.Input
            {
                ScreenWidth = 1920f,
                ScreenHeight = 1080f,
                Scale = Scale,
                PixelScale = Scale,
                Pack = lOPack,
                PackPlaced = false,
                PackWindowHeight = 58,
                WindowWidth = WindowWidth,
                BagHeights = new int[0],
                Panel = new UWRect(1300f, lfMargin, 620f, 1080f - (18f * Scale)),
                Minimap = new UWRect(0f, 0f, 0f, 0f),
                ActionBar = lOBar,
                ScreenMargin = 4,
                WindowGap = 4,
                LoadBarRows = 2
            });

            Check.Str("bags: pushed onto the action bar the backpack climbs over it (left of the panel)",
                (lOHelp.Pack.XMax == 1300f - lfMargin) + " " + lOHelp.Pack.Y, "True " + (lOBar.YMax + lfMargin));

            // The same with the backpack already beside the panel (the bags' view keeps it left of a
            // panel at its height): no push here, it climbs over the bar all the same.
            UWBagPlacement.Input lOBeside = new UWBagPlacement.Input
            {
                ScreenWidth = 1920f, ScreenHeight = 1080f, Scale = Scale, PixelScale = Scale,
                Pack = new UWRect(1300f - lfMargin - lfPackWidth, lfMargin, lfPackWidth, 64f * Scale),
                PackWindowHeight = 58, WindowWidth = WindowWidth, BagHeights = new int[0],
                Panel = new UWRect(1300f, lfMargin, 620f, 1080f - (18f * Scale)),
                ActionBar = lOBar, ScreenMargin = 4, WindowGap = 4, LoadBarRows = 2
            };

            Check.Str("bags: already beside the panel and on the bar, it climbs over it too",
                UWBagPlacement.Place(lOBeside).Pack.Y.ToString(), (lOBar.YMax + lfMargin).ToString());

            // A bar moved up the screen: the backpack under it stays down, and a column beside it
            // starts at the bottom, not above the bar.
            lOBeside.ActionBar = new UWRect(900f, 900f, 700f, 30f * Scale);
            lOBeside.BagHeights = new[] { 300, 50 };

            UWBagPlacement.Result lOHigh = UWBagPlacement.Place(lOBeside);

            Check.Str("bags: a bar moved up leaves the backpack and the columns' start alone",
                lOHigh.Pack.Y + " " + (lOHigh.Bags[1].Bottom < 900f), lfMargin + " True");
        }
    }
}
