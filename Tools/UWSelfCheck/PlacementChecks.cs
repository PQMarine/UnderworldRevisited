using System;
using UWDataImport.UWData;

namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// THE PLACEMENT OF AN OBJECT INTO A TILE (cleanup step 2.11, 2026-10-12): the one eighth-spot
    /// formula against the original's numbers (the +0xF of mob_init in OBJPHYS.C), the roll of a
    /// spot within a tile on the open half of a diagonal, the radius clamp, the scatter's tile
    /// fit, and the conversions the step decided (Docs/CLEANUP-PLAN.md, decisions 20 to 24):
    /// the floor from world to tile and to the eighth cell, the heading byte by truncation, the
    /// compass step, the rounded 16-bit angle. Needs no game data: the maps are synthetic.
    /// </summary>
    public static partial class Program
    {
        private static void fCheckPlacement()
        {
            Check.Group("Placement (UWPlacement, the eighth spot of the original)");

            fCheckEighthSpot();
            fCheckSpotRolls();
            fCheckScatterFit();
            fCheckFloorConversions();
            fCheckHeadingByte();
        }

        /// <summary>Decision 23: the 256-step heading from the yaw by truncation, the original's
        /// PlayerFacing &gt;&gt; 8 (PHYSICS.C), for the throw and the compass alike; then the
        /// compass step by the original's (v + 8 &amp; 0xFF) &gt;&gt; 4 (INTERACT.C).</summary>
        private static void fCheckHeadingByte()
        {
            float lfStep = 360f / UWHeadings.HeadingBytesPerTurn;

            Check.Int("heading: north is 0", UWHeadings.DegreesToHeadingByte(0f), 0);
            Check.Int("heading: east is 64", UWHeadings.DegreesToHeadingByte(90f), 64);
            Check.Int("heading: exactly one step is 1", UWHeadings.DegreesToHeadingByte(lfStep), 1);
            Check.Int("heading: a hair short of one step truncates to 0", UWHeadings.DegreesToHeadingByte(lfStep - 0.01f), 0);
            Check.Int("heading: ... where a rounding would say 1", UWUnits.RoundToInt((lfStep - 0.01f) / lfStep), 1);
            Check.Int("heading: the transform's 44.99998 for 45 degrees is step 31", UWHeadings.DegreesToHeadingByte(44.99998f), 31);
            Check.Int("heading: the last step before north", UWHeadings.DegreesToHeadingByte(359.99f), 255);
            Check.Int("heading: a full turn is north again", UWHeadings.DegreesToHeadingByte(360f), 0);
            Check.Int("heading: a negative yaw wraps", UWHeadings.DegreesToHeadingByte(-90f), 192);
            Check.Int("heading: the original's shift of the 16-bit facing agrees",
                UWHeadings.DegreesToHeadingByte(123.4f), ((UWUnits.DegreesToAngle(123.4f) & 0xFFFF) >> 8) & 0xFF);

            Check.Int("compass: north is step 0", UWHeadings.HeadingByteToCompassStep(0), 0);
            Check.Int("compass: heading 7 still points north", UWHeadings.HeadingByteToCompassStep(7), 0);
            Check.Int("compass: heading 8 is the first step past it", UWHeadings.HeadingByteToCompassStep(8), 1);
            Check.Int("compass: east is step 4", UWHeadings.HeadingByteToCompassStep(64), 4);
            Check.Int("compass: heading 247 is the last step", UWHeadings.HeadingByteToCompassStep(247), 15);
            Check.Int("compass: heading 248 wraps to north", UWHeadings.HeadingByteToCompassStep(248), 0);
            Check.Int("compass: ... as does 255", UWHeadings.HeadingByteToCompassStep(255), 0);
            Check.Int("compass: the camera at 11.25 degrees (step 8 exactly) turns the needle",
                UWHeadings.HeadingByteToCompassStep(UWHeadings.DegreesToHeadingByte(11.25f)), 1);
            Check.Int("compass: ... a hair before it does not",
                UWHeadings.HeadingByteToCompassStep(UWHeadings.DegreesToHeadingByte(11.24f)), 0);

            // Decision 24: the 16-bit angle the creature animator reads the camera with rounds
            // like the player's movement, so the transform's 44.99998 is the eighth itself.
            Check.Int("angle: the transform's 44.99998 rounds to the eighth 8192", UWHeadings.DegreesToAngleUnits(44.99998f), 8192);
            Check.Int("angle: ... which the view table takes as the fourth 32nd", UWHeadings.DegreesToAngleUnits(44.99998f) >> 11, 4);
            Check.Int("angle: a floor would have stayed a unit short", (int)Math.Floor(44.99998f * 65536f / 360f), 8191);
            Check.Int("angle: a creature facing the camera, seen with the camera turned 45 degrees, shows a front diagonal",
                UWCritterAnimations.GetViewDirection(4, UWHeadings.DegreesToAngleUnits(44.99998f)), 3);
        }

        /// <summary>Decision 20: world to tile is the floor, the original's pos &gt;&gt; 8 - a
        /// point on a tile boundary belongs to the tile it starts, where a rounding (halves to
        /// even) had sent every second boundary to the tile before.</summary>
        private static void fCheckFloorConversions()
        {
            float lfTile = UWWorldScale.TileSize;
            float lfHalf = UWWorldScale.TileHalfSize;

            Check.Int("tile: the map's corner is tile 0", UWUnits.WorldAxisToTile(-lfHalf), 0);
            Check.Int("tile: just before it is off the map", UWUnits.WorldAxisToTile(-lfHalf - 0.01f), -1);
            Check.Int("tile: the boundary of tiles 0 and 1 belongs to tile 1", UWUnits.WorldAxisToTile(lfHalf), 1);
            Check.Int("tile: ... a hair before it to tile 0", UWUnits.WorldAxisToTile(lfHalf - 0.01f), 0);
            Check.Int("tile: the boundary of tiles 1 and 2 belongs to tile 2 as well", UWUnits.WorldAxisToTile(lfTile + lfHalf), 2);
            Check.Int("tile: a tile centre is its tile", UWUnits.WorldAxisToTile(5 * lfTile), 5);
            Check.Int("tile: the original's shift agrees on a boundary",
                UWUnits.WorldAxisToTile(lfHalf), UWUnits.WorldAxisToOriginal(lfHalf) >> 8);
            Check.Int("tile: ... and inside a tile",
                UWUnits.WorldAxisToTile((5 * lfTile) + 17f), UWUnits.WorldAxisToOriginal((5 * lfTile) + 17f) >> 8);

            UWTilePos lOAt = UWTileQueries.WorldToTile(lfHalf, (2 * lfTile) + lfHalf);

            Check.Int("tile: WorldToTile floors both axes", (lOAt.X * 100) + lOAt.Y, 103);

            lOAt = UWTileQueries.WorldToTile(-500f, 9000f);

            Check.Int("tile: ... and clamps to the map", (lOAt.X * 100) + lOAt.Y, 63);

            // Decision 21: the cell a point lies in (the original's (pos >> 5) & 7) against the
            // nearest stored spot - they part at the cell boundaries.
            float lfStep = UWWorldScale.SubTileStep;
            float lfCorner = UWUnits.TileToWorldAxis(5);

            Check.Int("cell: the tile's corner is cell 0", UWTileQueries.WorldToEighthFloor(lfCorner), 0);
            Check.Int("cell: just short of one step in is still cell 0", UWTileQueries.WorldToEighthFloor(lfCorner + lfStep - 0.01f), 0);
            Check.Int("cell: ... where the nearest stored spot is already 1",
                UWTileQueries.WorldToSubTile(lfCorner + lfStep - 0.01f, 5), 1);
            Check.Int("cell: one step in is cell 1", UWTileQueries.WorldToEighthFloor(lfCorner + lfStep), 1);
            Check.Int("cell: the last unit of the tile is cell 7", UWTileQueries.WorldToEighthFloor(lfCorner + lfTile - 0.01f), 7);
            Check.Int("cell: the next tile's corner is cell 0 again", UWTileQueries.WorldToEighthFloor(lfCorner + lfTile), 0);
            Check.Int("cell: the original's shift agrees on a stored spot",
                UWTileQueries.WorldToEighthFloor(UWUnits.SubTileToWorldAxis(5, 6)), (UWUnits.WorldAxisToOriginal(UWUnits.SubTileToWorldAxis(5, 6)) >> 5) & 7);
            Check.Int("cell: a stored spot is its own cell", UWTileQueries.WorldToEighthFloor(UWUnits.SubTileToWorldAxis(5, 6)), 6);
        }

        /// <summary>The spot of an eighth: (tile &lt;&lt; 8) + (xpos &lt;&lt; 5) + 0xF in the
        /// original's units, 3.75 of ours into the step - and its inverses.</summary>
        private static void fCheckEighthSpot()
        {
            Check.Near("spot: tile 5, eighth 3 is the original's 0x50F in our units",
                UWUnits.SubTileToWorldAxis(5, 3), UWUnits.OriginalToWorldAxis((5 << 8) + (3 << 5) + 0xF), 0.0);
            Check.Near("spot: ... 315.75 world units", UWUnits.SubTileToWorldAxis(5, 3), 315.75, 0.0);
            Check.Near("spot: eighths 43 is the same spot", UWUnits.EighthsToWorldAxis(43), 315.75, 0.0);
            Check.Near("spot: eighth 0 of tile 0 lies 3.75 past the map's corner", UWUnits.EighthsToWorldAxis(0), -28.25, 0.0);
            Check.Near("spot: eighth 7 stays 4.25 short of the tile edge",
                UWUnits.SubTileToWorldAxis(0, 7), UWUnits.TileToWorldAxis(1) - 4.25, 0.0);

            bool lbRoundTrip = true;

            for (int liEighths = 0; liEighths < 512; liEighths++)
            {
                if (UWTileQueries.WorldToEighths(UWUnits.EighthsToWorldAxis(liEighths)) != liEighths)
                    lbRoundTrip = false;
            }

            Check.Bool("spot: every spot of the map reads back as its eighths", lbRoundTrip, true);
            Check.Int("spot: the stored sub-tile reads back", UWTileQueries.WorldToSubTile(UWUnits.SubTileToWorldAxis(5, 3), 5), 3);
            Check.Int("spot: ... with the spawner's 0.1 draw offset on top too",
                UWTileQueries.WorldToSubTile(UWUnits.SubTileToWorldAxis(5, 7) + 0.1f, 5), 7);
            Check.Int("spot: ... and 0.1 short of it", UWTileQueries.WorldToSubTile(UWUnits.SubTileToWorldAxis(5, 0) - 0.1f, 5), 0);

            Check.Int("radius: a coin keeps one eighth", UWPlacement.ClampRadius(0), 1);
            Check.Int("radius: two stay two", UWPlacement.ClampRadius(2), 2);
            Check.Int("radius: nothing keeps more than three", UWPlacement.ClampRadius(9), 3);
            Check.Int("radius: no kind at all is one", UWPlacement.PlacementRadius(null, -1), 1);
        }

        /// <summary>drop_around_place's stay_centered: the centre is the first try, and it is taken
        /// when it fits; without it the 24 tries roll around the centre (2026-10-12: the random roll
        /// of a spot within a tile, the reference's, is gone - the original never makes it).</summary>
        private static void fCheckSpotRolls()
        {
            int liX;
            int liY;

            bool lbFound = UWScatterRules.TryPickSpot(85, 85, 6, (piAtX, piAtY) => true, out liX, out liY, true);

            Check.Bool("stay_centered: the centre is tried first and taken", lbFound && liX == 85 && liY == 85, true);

            bool lbAway = UWScatterRules.TryPickSpot(85, 85, 6, (piAtX, piAtY) => piAtX != 85 || piAtY != 85,
                out liX, out liY, true);

            Check.Bool("stay_centered: a refused centre goes on to the rolls", lbAway && (liX != 85 || liY != 85), true);

            bool lbNone = UWScatterRules.TryPickSpot(85, 85, 6, (piAtX, piAtY) => false, out liX, out liY, true);

            Check.Bool("no spot fits: false, and the centre is handed back (put_at's own point)",
                !lbNone && liX == 85 && liY == 85, true);
        }

        /// <summary>The scatter's tile fit: open, not solid, on the map, no higher floor; the
        /// footprint of a radius; and the scatter itself finding a fitting spot.</summary>
        private static void fCheckScatterFit()
        {
            UWLevel lOLevel = new UWLevel(0) { TileData = fSightMap(UWTile.TileTypeEnum.open, 2) };
            UWTileQueries lOQueries = new UWTileQueries(lOLevel, null);
            float lfFloor = 2 << 4;

            Check.Bool("fit: an open cell at the floor's height fits", UWPlacement.FitsAtEighths(lOLevel, (11 << 3) + 4, (10 << 3) + 4, lfFloor), true);
            Check.Bool("fit: a solid tile refuses", UWPlacement.FitsAtEighths(lOLevel, (11 << 3) + 4, (11 << 3) + 4, lfFloor), false);
            Check.Bool("fit: off the map refuses", UWPlacement.FitsAtEighths(lOLevel, -1, (10 << 3) + 4, lfFloor), false);
            Check.Bool("fit: a floor within the tolerance above the point still fits",
                UWPlacement.FitsAtEighths(lOLevel, (11 << 3) + 4, (10 << 3) + 4, lfFloor - UWPlacement.FloorHeightTolerance), true);
            Check.Bool("fit: ... a hair more refuses it",
                UWPlacement.FitsAtEighths(lOLevel, (11 << 3) + 4, (10 << 3) + 4, lfFloor - UWPlacement.FloorHeightTolerance - 0.1f), false);
            Check.Bool("fit: the open half of a diagonal fits",
                UWPlacement.FitsAtEighths(new UWLevel(0) { TileData = fSightMap(UWTile.TileTypeEnum.diagonal_ne, 2) }, (11 << 3) + 7, (10 << 3) + 7, lfFloor), true);
            Check.Bool("fit: ... its solid half not",
                UWPlacement.FitsAtEighths(new UWLevel(0) { TileData = fSightMap(UWTile.TileTypeEnum.diagonal_ne, 2) }, (11 << 3) + 0, (10 << 3) + 0, lfFloor), false);

            Check.Bool("footprint: the last cell before the wall fits a radius of 0",
                UWPlacement.FootprintFits(lOLevel, (11 << 3) + 4, (10 << 3) + 0, 0, lfFloor), true);
            Check.Bool("footprint: ... a radius of 1 reaches into the wall",
                UWPlacement.FootprintFits(lOLevel, (11 << 3) + 4, (10 << 3) + 0, 1, lfFloor), false);
            Check.Bool("footprint: one cell in, a radius of 1 fits",
                UWPlacement.FootprintFits(lOLevel, (11 << 3) + 4, (10 << 3) + 1, 1, lfFloor), true);
            Check.Bool("footprint: the corridor's open end takes a radius of 1 at the tile edge",
                UWPlacement.FootprintFits(lOLevel, (11 << 3) + 0, (10 << 3) + 4, 1, lfFloor), true);

            bool lbFound = UWPlacement.TryScatter(lOQueries, (11 << 3) + 4, (10 << 3) + 4, UWScatterRules.LootRadius, 1, lfFloor, null,
                out int liX, out int liY);

            Check.Bool("scatter: the loot finds a spot in the corridor", lbFound, true);
            Check.Bool("scatter: ... which fits its footprint", UWPlacement.FootprintFits(lOLevel, liX, liY, 1, lfFloor), true);
            Check.Bool("scatter: ... in a corridor tile", (liY >> 3) == 10 && (liX >> 3) >= 10 && (liX >> 3) <= 12, true);
            Check.Bool("scatter: the host's condition can refuse every spot",
                UWPlacement.TryScatter(lOQueries, (11 << 3) + 4, (10 << 3) + 4, UWScatterRules.LootRadius, 1, lfFloor, (piX, piY) => false, out liX, out liY), false);
            Check.Int("scatter: ... and the point itself comes back", (liX * 1000) + liY, (((11 << 3) + 4) * 1000) + (10 << 3) + 4);
            Check.Bool("scatter: in solid rock nothing fits",
                UWPlacement.TryScatter(lOQueries, (30 << 3) + 4, (30 << 3) + 4, UWScatterRules.LootRadius, 0, lfFloor, null, out liX, out liY), false);
        }
    }
}
