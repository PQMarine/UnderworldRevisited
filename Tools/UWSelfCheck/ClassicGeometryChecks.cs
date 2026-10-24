using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// Cleanup step 5.18 (2026-10-23): the classic screen's fit into the window (UWClassicGeometry)
    /// left UWGameUI. Pinned with what UWGameUI computed: the original's point of a screen point
    /// with its row counted from the bottom (199 - r), the 3D view's rectangle at the hole's
    /// flood-filled edges (50/226 and 183/62 bottom up), the window picture's corner (52, 19 for
    /// the 172 x 112 block), a picture's screen rectangle, the fractions in the view, and the
    /// widened frame's columns mapped back to the original's (the inserted ones belong to no
    /// area).
    /// </summary>
    public static partial class Program
    {
        private static void fCheckClassicGeometry()
        {
            Check.Group("Classic geometry (UWClassicGeometry - cleanup step 5.18)");

            // Square pixels, four screen pixels per original one, no bars.
            UWClassicGeometry lOPlain = new UWClassicGeometry(4f, 1f, 0f, 0f, 0);

            Check.Near("geometry: a column is as wide as a row at square pixels", lOPlain.ScaleX, 4f, 0.0001);
            fExpectInt("geometry: the frame's columns", lOPlain.FrameColumns, 320);
            fExpectBool("point: the bottom-left screen pixel is column 0, row 0 from the bottom",
                lOPlain.TryGetOriginalPoint(0f, 0f, out int liX, out int liY) && liX == 0 && liY == 0, true);
            fExpectBool("point: the top-right pixel is column 319, 199 from the bottom",
                lOPlain.TryGetOriginalPoint(1279.5f, 799.5f, out liX, out liY) && liX == 319 && liY == 199, true);
            fExpectBool("point: the row r from the top is y = 199 - r (row 17, the hole's top edge)",
                lOPlain.TryGetOriginalPoint(400f, (199 - 17) * 4f + 2f, out liX, out liY) && liY == 182, true);
            fExpectBool("point: right of the frame is outside", lOPlain.TryGetOriginalPoint(1280f, 10f, out liX, out liY), false);
            fExpectBool("point: above the frame is outside", lOPlain.TryGetOriginalPoint(10f, 800f, out liX, out liY), false);
            fExpectBool("point: left of the frame is outside", lOPlain.TryGetOriginalPoint(-0.5f, 10f, out liX, out liY), false);

            // The original's areas: both ends included, the position relative to the corner.
            UWClickRules.Area lOArea = new UWClickRules.Area(10, 20, 30, 40);

            fExpectBool("area: the corner itself, at (0, 0) of the area",
                lOPlain.IsPointerInOriginalArea(10 * 4f, 20 * 4f, lOArea, out liX, out liY) && liX == 0 && liY == 0, true);
            fExpectBool("area: the far corner is still inside (both ends included)",
                lOPlain.IsPointerInOriginalArea(30 * 4f + 3f, 40 * 4f + 3f, lOArea, out liX, out liY) && liX == 20 && liY == 20, true);
            fExpectBool("area: one column further is outside", lOPlain.IsPointerInOriginalArea(31 * 4f, 20 * 4f, lOArea, out liX, out liY), false);

            // The 3D view's rectangle: the hole's flood-filled edges, bottom up 183 and 62.
            Check.Near("view: left edge at column 50", lOPlain.GameAreaLeft, 50 * 4f, 0.0001);
            Check.Near("view: right edge at column 226", lOPlain.GameAreaRight, 226 * 4f, 0.0001);
            Check.Near("view: top edge at row 183 from the bottom", lOPlain.GameAreaTop, 183 * 4f, 0.0001);
            Check.Near("view: bottom edge at row 62 from the bottom", lOPlain.GameAreaBottom, 62 * 4f, 0.0001);
            fExpectBool("view: a point inside", lOPlain.IsInGameArea(400f, 400f), true);
            fExpectBool("view: the edge is outside for the strict test", lOPlain.IsInGameArea(200f, 400f), false);
            fExpectBool("view: ... but inside for the pointer zones (per user, 2026-09-15)", lOPlain.IsInGameAreaOrOnBorder(200f, 400f), true);
            fExpectBool("view: beyond the edge is outside for both", lOPlain.IsInGameAreaOrOnBorder(199f, 400f) || lOPlain.IsInGameArea(199f, 400f), false);
            Check.Near("view: the bottom edge is fraction 0", lOPlain.VerticalFractionInGameArea(62 * 4f), 0f, 0.0001);
            Check.Near("view: the top edge is fraction 1", lOPlain.VerticalFractionInGameArea(183 * 4f), 1f, 0.0001);
            Check.Near("view: the middle column is fraction 0.5", lOPlain.HorizontalFractionInGameArea(138 * 4f), 0.5f, 0.0001);

            // The pointer scheme's movement area: the view window of place_3d_view (52/19,
            // 172 x 113), bottom up 181 and 68 (per user from the original, 2026-10-23).
            Check.Near("movement area: left edge at column 52", lOPlain.MovementAreaLeft, 52 * 4f, 0.0001);
            Check.Near("movement area: right edge at column 224", lOPlain.MovementAreaRight, 224 * 4f, 0.0001);
            Check.Near("movement area: top edge at row 181 from the bottom", lOPlain.MovementAreaTop, 181 * 4f, 0.0001);
            Check.Near("movement area: bottom edge at row 68 from the bottom, seven rows above the hole's", lOPlain.MovementAreaBottom, 68 * 4f, 0.0001);
            fExpectBool("movement area: the border is inside for the pointer zones", lOPlain.IsInMovementAreaOrOnBorder(52 * 4f, 68 * 4f), true);
            fExpectBool("movement area: the hole's bottom rows are outside", lOPlain.IsInMovementAreaOrOnBorder(400f, 65 * 4f), false);
            fExpectBool("movement area: widened with the frame", new UWClassicGeometry(4f, 1f, 0f, 0f, 40).MovementAreaRight > lOPlain.MovementAreaRight, true);
            Check.Near("view: beyond the right edge clamps to 1", lOPlain.HorizontalFractionInGameArea(5000f), 1f, 0.0001);
            Check.Near("view: left of the left edge clamps to 0", lOPlain.HorizontalFractionInGameArea(-5f), 0f, 0.0001);

            // The window picture's corner: the 172 x 112 block centred in the hole's inclusive
            // 177 columns, two rows below the hole's top (user, 2026-08-29).
            (float X, float Y) lOCorner = UWClassicGeometry.ViewportPictureCorner(172f, 112f);

            Check.Near("picture: the window picture's column", lOCorner.X, 52f, 0.0001);
            Check.Near("picture: the window picture's row", lOCorner.Y, 19f, 0.0001);
            Check.Near("picture: an odd width rounds its centring down", UWClassicGeometry.ViewportPictureCorner(173f, 112f).X, 52f, 0.0001);

            // A picture's screen rectangle: bottom up, scaled, from the corner given top down.
            (float X, float Y, float Width, float Height) lORect = lOPlain.UiPictureScreenRect(52f, 19f, 172f, 112f);

            Check.Near("picture: screen x", lORect.X, 52 * 4f, 0.0001);
            Check.Near("picture: screen y from the bottom (200 - 19 - 112 rows)", lORect.Y, 69 * 4f, 0.0001);
            Check.Near("picture: screen width", lORect.Width, 172 * 4f, 0.0001);
            Check.Near("picture: screen height", lORect.Height, 112 * 4f, 0.0001);

            // At 4:3 with bars: a column is 5/6 of a row, the bars shift everything.
            UWClassicGeometry lOBars = new UWClassicGeometry(3f, 5f / 6f, 100f, 30f, 0);

            Check.Near("bars: a column at 4:3 is five sixths of a row", lOBars.ScaleX, 2.5f, 0.0001);
            fExpectBool("bars: the bar itself is outside the frame", lOBars.TryGetOriginalPoint(99f, 40f, out liX, out liY), false);
            fExpectBool("bars: the frame starts after the bar, column 0 row 0",
                lOBars.TryGetOriginalPoint(100f, 30f, out liX, out liY) && liX == 0 && liY == 0, true);
            fExpectBool("bars: column 50 lies 125 pixels into the frame",
                lOBars.TryGetOriginalPoint(100f + 125f, 30f + 62 * 3f, out liX, out liY) && liX == 50 && liY == 62, true);
            Check.Near("bars: the view's left edge follows the bar", lOBars.GameAreaLeft, 100f + 50 * 2.5f, 0.0001);
            Check.Near("bars: the view's bottom edge follows the bottom bar", lOBars.GameAreaBottom, 30f + 62 * 3f, 0.0001);
            Check.Near("bars: a picture's screen x follows the bar", lOBars.UiPictureScreenRect(52f, 19f, 172f, 112f).X, 100f + 52 * 2.5f, 0.0001);

            // Classic Wide: 100 columns put in, 50 at each cut of a band; the inserted columns belong
            // to no area, the right part maps back by the whole widening.
            UWClassicGeometry lOWide = new UWClassicGeometry(1f, 1f, 0f, 0f, 100);

            fExpectInt("wide: the frame's columns", lOWide.FrameColumns, 420);
            Check.Near("wide: the view's right edge moves by the widening", lOWide.GameAreaRight, 326f, 0.0001);
            fExpectInt("wide: a column before the view band's left cut (53) stays", lOWide.OriginalColumn(52, 60), 52);
            fExpectInt("wide: the first inserted column belongs to no area", lOWide.OriginalColumn(53, 60), -1);
            fExpectInt("wide: the last of the left insert belongs to no area", lOWide.OriginalColumn(102, 60), -1);
            fExpectInt("wide: the view's columns map back by half the widening", lOWide.OriginalColumn(103, 60), 53);
            fExpectInt("wide: the column before the right cut (223) maps back by half", lOWide.OriginalColumn(272, 60), 222);
            fExpectInt("wide: the right insert belongs to no area", lOWide.OriginalColumn(273, 60), -1);
            fExpectInt("wide: the last inserted column belongs to no area", lOWide.OriginalColumn(322, 60), -1);
            fExpectInt("wide: the right part maps back by the whole widening", lOWide.OriginalColumn(323, 60), 223);
            fExpectInt("wide: the last column is the original's last", lOWide.OriginalColumn(419, 60), 319);
            fExpectInt("wide: the top bar's cuts (78, 198) differ from the view's", lOWide.OriginalColumn(78, 5), -1);
            fExpectInt("wide: the top bar's column 77 stays", lOWide.OriginalColumn(77, 5), 77);
            fExpectBool("wide: a click into the inserted columns is no point of the original",
                lOWide.TryGetOriginalPoint(60.5f, 199 - 60 + 0.5f, out liX, out liY), false);
            fExpectBool("wide: a click right of the insert maps back",
                lOWide.TryGetOriginalPoint(323.5f, 199 - 60 + 0.5f, out liX, out liY) && liX == 223 && liY == 139, true);
            fExpectBool("wide: the widened frame's last column is inside",
                lOWide.TryGetOriginalPoint(419.5f, 0.5f, out liX, out liY) && liX == 319, true);
            fExpectBool("wide: beyond the widened frame is outside", lOWide.TryGetOriginalPoint(420f, 0.5f, out liX, out liY), false);
            Check.Near("wide: the viewport picture moves with the hole's middle (half the widening)",
                lOWide.ViewportPictureScreenRect(172f, 112f).X, 52f + 50f, 0.0001);
            Check.Near("plain: the viewport picture stays at its column without widening",
                lOPlain.ViewportPictureScreenRect(172f, 112f).X, 52 * 4f, 0.0001);

            // The pointer zones' borders as UWGameUI wrote them until cleanup 5.18 (57f / 172f and
            // the rest), now UWClassicLayout.PointerZones - the same floats.
            fExpectBool("zones: the left border is 57 of 172 columns", UWClassicLayout.PointerZones.LeftBorder == 57f / 172f, true);
            fExpectBool("zones: the right border is 115 of 172 columns", UWClassicLayout.PointerZones.RightBorder == 115f / 172f, true);
            fExpectBool("zones: the strafe band is the bottom 22 of 113 rows", UWClassicLayout.PointerZones.StrafeBorder == 1f - (22f / 113f), true);
            fExpectBool("zones: walking starts above 46 of 113 rows", UWClassicLayout.PointerZones.WalkBorder == 1f - (46f / 113f), true);
        }
    }
}
