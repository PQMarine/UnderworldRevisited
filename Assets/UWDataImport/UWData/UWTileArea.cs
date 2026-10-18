namespace UWDataImport.UWData
{
	/// <summary>
	/// Which tile a creature counts as standing on in a search (cleanup step 3.2, 2026-10-17).
	///
	/// WHY TWO: the record keeps the tile the motion core moved the creature to, the body (the
	/// picture's transform) trails it by up to a step (UWCritterRemains). The searches decided by
	/// different ones before FindCreatures, and each keeps its own: the theft and trespass
	/// witnesses and the creation trap's crowding count asked the body's tile, the sleep and
	/// Detect Monsters the record's (section 11 of Docs/CLEANUP-PLAN.md: moving everything to
	/// records changes who counts at tile edges, a separate, checked step).
	/// </summary>
	public enum UWCreatureTile
	{
		/// <summary>The record's tile (UWCritterRecord.TileX/TileY), where the original keeps it.</summary>
		Record,

		/// <summary>The tile the body's world point lies in (UWTileQueries.WorldToTile of the
		/// transform).</summary>
		Body
	}

	/// <summary>
	/// A box of tiles for a creature search, with the tile measure it is meant for (cleanup step
	/// 3.2, 2026-10-17). Every area our creature searches use is such a box around a centre, or the
	/// whole level: the theft and trespass witnesses (seven back, eight forward), the creation
	/// trap's crowding (the trap's own back and forward), the sleep's gronk_area (radius back,
	/// radius + 1 forward, UWSleepRules.AreaAround). None measures Manhattan or Euclidean distance;
	/// a reach that does (the ambush's WithinAmbushReach, Detect Monsters' range) stays in its rule
	/// and runs over what the search found.
	/// </summary>
	public readonly struct UWTileArea
	{
		/// <summary>Every creature of the level, whatever its tile - also one without a record.</summary>
		public static readonly UWTileArea WholeLevel =
			new UWTileArea(int.MinValue, int.MinValue, int.MaxValue, int.MaxValue, UWCreatureTile.Record, true);

		/// <summary>The westmost tile column inside, inclusive.</summary>
		public readonly int MinX;

		/// <summary>The southmost tile row inside, inclusive.</summary>
		public readonly int MinY;

		/// <summary>The eastmost tile column inside, inclusive.</summary>
		public readonly int MaxX;

		/// <summary>The northmost tile row inside, inclusive.</summary>
		public readonly int MaxY;

		/// <summary>Which of a creature's tiles has to lie inside.</summary>
		public readonly UWCreatureTile Measure;

		/// <summary>No tile test at all (WholeLevel).</summary>
		public readonly bool IsWholeLevel;

		private UWTileArea(int piMinX, int piMinY, int piMaxX, int piMaxY, UWCreatureTile peMeasure, bool pbWholeLevel)
		{
			MinX = piMinX;
			MinY = piMinY;
			MaxX = piMaxX;
			MaxY = piMaxY;
			Measure = peMeasure;
			IsWholeLevel = pbWholeLevel;
		}

		/// <summary>The tiles from piTilesBack below to piTilesForward above the centre, on both axes
		/// - the old searches' "delta &gt;= -back and delta &lt;= forward" on x and y.</summary>
		public static UWTileArea Around(UWTilePos pOCentre, int piTilesBack, int piTilesForward, UWCreatureTile peMeasure)
		{
			return new UWTileArea(pOCentre.X - piTilesBack, pOCentre.Y - piTilesBack,
				pOCentre.X + piTilesForward, pOCentre.Y + piTilesForward, peMeasure, false);
		}

		/// <summary>Whether the tile lies inside; always for the whole level.</summary>
		public bool Contains(UWTilePos pOTile)
		{
			return IsWholeLevel
				|| (pOTile.X >= MinX && pOTile.X <= MaxX && pOTile.Y >= MinY && pOTile.Y <= MaxY);
		}

		public override string ToString()
		{
			return IsWholeLevel ? "whole level" : MinX + "/" + MinY + ".." + MaxX + "/" + MaxY + " by " + Measure;
		}
	}
}
