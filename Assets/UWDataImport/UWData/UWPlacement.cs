using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Where an object goes down in a tile (cleanup step 2.11, 2026-10-12): the spot of an
	/// eighth, the roll of a spot within a tile, the fit test of the original's scatter and the
	/// radius an object keeps from walls and containers. Until then UWLevelLoader held three
	/// copies of the eighth formula (+3.75; +3.85 with the spawner's draw offset in the snap out
	/// of rock; +1.0 in the drop in front of the player), the "open half of a diagonal" rule
	/// twice and the COMOBJ radius clamp three times. Every roll here is on UWRandom, as the loot
	/// scatter has been since 2026-10-11, so that a run can be repeated.
	///
	/// THE SPOT OF AN EIGHTH is the original's: a resting object's exact position is
	/// (tile &lt;&lt; 8) + (xpos &lt;&lt; 5) + 0xF - mob_init and the missile launch write it that
	/// way (OBJPHYS.C, MISSILE.C), set_phys_data reads it back with (pos &gt;&gt; 5) &amp; 7 - so
	/// 0xF of the 32 units of a step, 3.75 of our 8 (UWWorldScale.SubTileOffset). One formula,
	/// UWUnits.SubTileToWorldAxis, for everything that is placed: a creature's remains, a
	/// spilled barrel, a barter drop, the item put down in front of the player (decision 22 of
	/// Docs/CLEANUP-PLAN.md, 2026-10-12: until then one unit into the step).
	/// </summary>
	public static class UWPlacement
	{
		/// <summary>The least radius an object keeps from a blocked edge, in eighths: even a coin
		/// (COMOBJ radius 0) stays one eighth off a wall, as a thrown item does.</summary>
		public const int MinRadius = 1;

		/// <summary>The most an object keeps from an edge: three eighths, so that a large thing
		/// still finds a spot in a one-tile corridor (7 - 3 leaves 1 to 4).</summary>
		public const int MaxRadius = 3;

		/// <summary>World units a floor may lie above the point and still count as not higher -
		/// rounding between a body's height and the floor's (the scatter's fit test).</summary>
		public const float FloorHeightTolerance = 1f;

		/// <summary>A COMOBJ radius as the distance kept from edges, MinRadius to MaxRadius.</summary>
		public static int ClampRadius(int piRadius)
		{
			return piRadius < MinRadius ? MinRadius : (piRadius > MaxRadius ? MaxRadius : piRadius);
		}

		/// <summary>The clamped radius of an object kind; MinRadius for an unknown kind or no
		/// kind at all (piObjectId below zero).</summary>
		public static int PlacementRadius(UWTileQueries pOQueries, int piObjectId)
		{
			return ClampRadius(pOQueries != null && piObjectId >= 0 ? pOQueries.ObjectRadius(piObjectId) : 0);
		}

		/// <summary>The tile part of the original's item-fit test, which runs the whole motion
		/// scan (see CheckIfItemFitsInTile_seg026_1008 in UWMotionCore), for one eighth cell
		/// (tile times 8 plus the spot, per axis): inside the level, not solid, in the open half
		/// of a diagonal, and a floor no higher than the point plus FloorHeightTolerance (a step
		/// up refuses it). See UWScatterRules for what the original tests beyond the tile.</summary>
		public static bool FitsAtEighths(UWLevel pOLevel, int piX, int piY, float pfPointHeight)
		{
			int liTileX = piX >> 3;
			int liTileY = piY >> 3;

			// Off the map there is no tile, and nothing fits.
			UWTile lOTile = pOLevel != null ? pOLevel.GetTile(liTileX, liTileY) : null;

			if (lOTile == null || !UWTileQueries.IsSubTileInOpenHalf(lOTile.TileType, piX & 7, piY & 7))
				return false;

			float lfFloor = UWTileQueries.FloorHeightAt(lOTile, UWUnits.EighthsToWorldAxis(piX),
				UWUnits.EighthsToWorldAxis(piY), liTileX, liTileY);

			return lfFloor <= pfPointHeight + FloorHeightTolerance;
		}

		/// <summary>
		/// THE OBJECT'S FOOTPRINT, not only its point (per user, 2026-09-28: "the loot lies too
		/// close to walls at times"): the original's fit test runs the motion test with the
		/// object's own radius (COMOBJ, in eighths of a tile), so a wall or a higher floor within
		/// that radius refuses the spot. Every eighth of the square the radius spans has to pass
		/// FitsAtEighths; whether the original draws the line one eighth nearer or further is
		/// not read.
		/// </summary>
		public static bool FootprintFits(UWLevel pOLevel, int piX, int piY, int piRadius, float pfPointHeight)
		{
			for (int liDy = -piRadius; liDy <= piRadius; liDy++)
			{
				for (int liDx = -piRadius; liDx <= piRadius; liDx++)
				{
					if (!FitsAtEighths(pOLevel, piX + liDx, piY + liDy, pfPointHeight))
						return false;
				}
			}

			return true;
		}

		/// <summary>
		/// The original's scatter around a point in eighths (UWScatterRules.TryPickSpot: up to 24
		/// rolls within the radius, the first that fits) with the tile fit of FootprintFits for
		/// an object of this radius, and an optional further condition of the host (the
		/// clearance from world containers a spilled barrel keeps). False when none of the rolls
		/// fits - piX and piY are then the point itself, and the caller lets the culling test
		/// decide (UWTileQueries.Culls).
		/// </summary>
		public static bool TryScatter(UWTileQueries pOQueries, int piCentreX, int piCentreY, int piRadiusEighths,
			int piObjectRadius, float pfPointHeight, Func<int, int, bool> pbAlsoFits, out int piX, out int piY,
			bool pbStayCentered = false)
		{
			UWLevel lOLevel = pOQueries != null ? pOQueries.Level : null;

			return UWScatterRules.TryPickSpot(piCentreX, piCentreY, piRadiusEighths,
				(piAtX, piAtY) => FootprintFits(lOLevel, piAtX, piAtY, piObjectRadius, pfPointHeight)
					&& (pbAlsoFits == null || pbAlsoFits(piAtX, piAtY)),
				out piX, out piY, pbStayCentered);
		}
	}
}
