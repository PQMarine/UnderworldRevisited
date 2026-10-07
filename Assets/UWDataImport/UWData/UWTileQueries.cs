using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// Questions about the tiles of a level that the rules ask: which tile a point lies in, how
	/// high the floor is there, whether it is water or lava, whether an item sinks, how far an item
	/// has to keep from edges and steps, and how far objects reach. Engine-free since 2026-09-17
	/// (P2 of the engine separation); UWLevelLoader keeps one per level and forwards.
	///
	/// All positions are in our world units on the horizontal axes X (east) and Z (north).
	/// </summary>
	public sealed class UWTileQueries
	{
		private readonly UWLevel mOLevel;
		private readonly DataImport mOData;

		/// <summary>The player object; its radius is part of every trigger reach.</summary>
		public const int PlayerObjectId = 63;

		/// <summary>From this much higher floor at the neighbour an edge counts as a step, in world
		/// units - smaller than any real height step, larger than rounding errors.</summary>
		private const float StepEdgeTolerance = 0.5f;

		/// <summary>Nothing in the game is nested deeper than this; beyond it a container counts as
		/// unsinkable, so that nothing runs in circles.</summary>
		private const int MaxContainerDepth = 8;

		public UWTileQueries(UWLevel pOLevel, DataImport pOData)
		{
			mOLevel = pOLevel;
			mOData = pOData;
		}

		public UWLevel Level => mOLevel;

		/// <summary>The tile at these coordinates, null outside the map or without data.</summary>
		public UWTile GetTile(int piTileX, int piTileZ)
		{
			if (mOLevel == null || mOLevel.TileData == null
				|| piTileX < 0 || piTileZ < 0
				|| piTileX >= UWWorldScale.TilesPerAxis || piTileZ >= UWWorldScale.TilesPerAxis)
				return null;

			return mOLevel.TileData[(piTileZ * UWWorldScale.TilesPerAxis) + piTileX];
		}

		/// <summary>
		/// Which tile a world point lies in, clamped to the map. The tile centre lies exactly at
		/// tile * TileSize, so this is the position divided by the tile width, rounded (halves to
		/// even, as Mathf.RoundToInt did). Adding half a tile first put the centre on the rounding
		/// boundary (user, 2026-08-29).
		/// </summary>
		public static UWTilePos WorldToTile(float pfWorldX, float pfWorldZ)
		{
			return new UWTilePos(fClampTile(UWUnits.RoundToInt(pfWorldX / UWWorldScale.TileSize)),
				fClampTile(UWUnits.RoundToInt(pfWorldZ / UWWorldScale.TileSize)));
		}

		private static int fClampTile(int piTile)
		{
			return piTile < 0 ? 0 : (piTile > UWWorldScale.TilesPerAxis - 1 ? UWWorldScale.TilesPerAxis - 1 : piTile);
		}

		/// <summary>Which of the eight fixed spots (0..7) within the tile is closest to a world
		/// coordinate - the inverse of the object placement (sub-tile step, offset and the 0.1 draw
		/// offset of the spawner).</summary>
		public static ushort WorldToSubTile(float pfWorldCoordinate, int piTileIndex)
		{
			float lfWithinTile = pfWorldCoordinate - (piTileIndex * UWWorldScale.TileSize) + UWWorldScale.TileHalfSize
				- 0.1f - UWWorldScale.SubTileOffset;

			int liSpot = UWUnits.RoundToInt(lfWithinTile / UWWorldScale.SubTileStep);

			return (ushort)(liSpot < 0 ? 0 : (liSpot > 7 ? 7 : liSpot));
		}

		/// <summary>A world coordinate in eighths of a tile - tile times eight plus the sub-tile
		/// spot, the unit the original measures reach and closeness in (UWReachRules,
		/// UWCritterRules). One axis at a time: x for X, z for Y.</summary>
		public static int WorldToEighths(float pfWorldCoordinate)
		{
			int liTile = fClampTile(UWUnits.RoundToInt(pfWorldCoordinate / UWWorldScale.TileSize));

			return UWReachRules.ToEighths(liTile, WorldToSubTile(pfWorldCoordinate, liTile));
		}

		/// <summary>The inverse of WorldToEighths: the world coordinate of an eighth's spot.</summary>
		public static float EighthsToWorld(int piEighths)
		{
			return ((piEighths >> 3) * UWWorldScale.TileSize) - UWWorldScale.TileHalfSize
				+ ((piEighths & 7) * UWWorldScale.SubTileStep) + UWWorldScale.SubTileOffset;
		}

		/// <summary>Whether the culling test (the liquids' range 0x0A, rolled anew) takes this
		/// object - what UWScatterRules does with an object no spot was found for.</summary>
		public bool Culls(int piObjectId, int piQuantity)
		{
			return fSinksInLiquid(piObjectId, piQuantity, UWLiquidCulling.RollRange());
		}

		/// <summary>Whether this tile holds water: its floor texture is water in TERRAIN.DAT
		/// (uw-formats.txt 4.9, value 0x0010; in UW1 floor textures 16 and 17).</summary>
		public bool IsWaterTile(UWTile pOTile)
		{
			return pOTile != null && mOData != null && mOData.Terrain != null
				&& mOData.Terrain.IsWaterFloor(pOTile.TextureFloor);
		}

		/// <summary>Whether this tile carries lava - TERRAIN.DAT value 0x0020, in UW1 the floor
		/// textures 23 to 25 on levels 6 to 9.</summary>
		public bool IsLavaTile(UWTile pOTile)
		{
			return pOTile != null && mOData != null && mOData.Terrain != null
				&& mOData.Terrain.IsLavaFloor(pOTile.TextureFloor);
		}

		/// <summary>Floor that can swallow a dropped item.</summary>
		public bool IsDestructiveTile(UWTile pOTile)
		{
			return IsWaterTile(pOTile) || IsLavaTile(pOTile);
		}

		/// <summary>The first and last wall picture object (0x16D-0x16F, "special tmap obj").</summary>
		public const int FirstWallPictureId = 0x16D;

		public const int LastWallPictureId = 0x16F;

		/// <summary>
		/// Whether the map marks this tile as a stair (automap display 0xC). In the original the
		/// object renderer sets the marker (dseg_54F = 3) while it draws a wall picture 0x16D-0x16F
		/// whose wall texture is stairs up or down in TERRAIN.DAT (3 or 4, table 720C) - nothing
		/// else does (render type 3, minor class 2, see render-order.md). Until 2026-10-07 the port
		/// took a move trigger with a teleport to another level for a stair, so the water hole on
		/// level 1 at 47/53 (a trigger into level 2, no picture) showed as black and red specks:
		/// the stair steps added to the water's palette entries (per user that day).
		/// A hidden picture is not drawn and marks nothing.
		/// </summary>
		public bool HasStairMarker(UWTile pOTile)
		{
			if (pOTile == null || pOTile.ObjectsInTile == null || mOData == null || mOData.Terrain == null)
				return false;

			foreach (UWObject lOObject in pOTile.ObjectsInTile)
			{
				if (lOObject == null || lOObject.IsHidden || lOObject.Texture == null
					|| lOObject.ID < FirstWallPictureId || lOObject.ID > LastWallPictureId)
					continue;

				UWTerrain.TerrainType leTerrain = mOData.Terrain.GetWallTerrain(lOObject.Texture.Index);

				if (leTerrain == UWTerrain.TerrainType.StairsUp || leTerrain == UWTerrain.TerrainType.StairsDown)
					return true;
			}

			return false;
		}

		/// <summary>Whether this kind of item sinks in water or lava, with its OWN roll of the
		/// culling range (UWLiquidCulling). If COMOBJ.DAT does not know the object, it stays -
		/// better one item too many in the world than one lost for good.</summary>
		public bool SinksInLiquid(int piObjectId)
		{
			return fSinksInLiquid(piObjectId, 1, UWLiquidCulling.RollRange());
		}

		/// <summary>One object against an already rolled range - see UWLiquidCulling.</summary>
		private bool fSinksInLiquid(int piObjectId, int piQuantity, int piRange)
		{
			UWCommonObjectProperties.Entry lOEntry;

			return mOData != null && mOData.CommonObjectProperties != null
				&& mOData.CommonObjectProperties.TryGet(piObjectId, out lOEntry)
				&& UWLiquidCulling.Swallows(lOEntry.CullingPriority, piQuantity, piRange);
		}

		/// <summary>Whether lava spares this object before any culling (UWLiquidCulling.SparedByLava):
		/// quality class 3 or fire-resistant. An unknown object is spared too.</summary>
		public bool SparedByLava(int piObjectId)
		{
			UWCommonObjectProperties.Entry lOEntry;

			return mOData == null || mOData.CommonObjectProperties == null
				|| !mOData.CommonObjectProperties.TryGet(piObjectId, out lOEntry)
				|| UWLiquidCulling.SparedByLava(lOEntry.QualityClass, lOEntry.Resistances);
		}

		/// <summary>Whether the floor of this tile takes this object: lava first spares what
		/// SparedByLava says, then both liquids cull (SinksInLiquid). A THROWN object that comes to
		/// rest in water is tested twice, each with its own roll - the landing and the collision
		/// after it (UWLiquidCulling.ThrownIntoWaterTests). In lava the landing's test belongs to
		/// the break chance, which the projectile rolls itself.</summary>
		public bool TileSwallows(UWTile pOTile, UWObject pOObject, bool pbThrown = false)
		{
			if (pOObject == null || !IsDestructiveTile(pOTile))
				return false;

			if (IsLavaTile(pOTile))
				return !SparedByLava(pOObject.ID) && SinksInLiquid(pOObject);

			int liTests = pbThrown ? UWLiquidCulling.ThrownIntoWaterTests : 1;

			for (int liTest = 0; liTest < liTests; liTest++)
			{
				if (SinksInLiquid(pOObject))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Like SinksInLiquid(id), but with contents: a container only sinks if everything in it
		/// sinks too, containers inside included. Otherwise a bag with a talisman was lost in the
		/// lava; in the original the bag stays (per user, 2026-09-14) - and the original does the
		/// same, its test walks the contents chain (ObjectCullingTest_seg027_2861_1A6 through
		/// RunCodeOnObjectChain_seg027_117).
		///
		/// THE RANGE IS ROLLED ONCE for the whole landing and handed down, as the original keeps
		/// it in GlobalCullingRange for the whole test.
		/// </summary>
		public bool SinksInLiquid(UWObject pOObject, int piDepth = 0)
		{
			return fSinksInLiquid(pOObject, UWLiquidCulling.RollRange(), piDepth);
		}

		private bool fSinksInLiquid(UWObject pOObject, int piRange, int piDepth)
		{
			if (pOObject == null
				|| !fSinksInLiquid(pOObject.ID, pOObject.HasQuantity ? pOObject.Quantity : 1, piRange))
				return false;

			if (piDepth > MaxContainerDepth)
				return false;

			if (pOObject.Contents == null && mOLevel != null && mOLevel.Masterlist != null
				&& pOObject.GetCategory() == UWObject.ObjectCategoryEnum.Containers)
				pOObject.EnsureContentsLoaded(mOLevel.Masterlist);

			if (pOObject.Contents == null)
				return true;

			foreach (UWObject lOItem in pOObject.Contents)
			{
				if (lOItem != null && !fSinksInLiquid(lOItem, piRange, piDepth + 1))
					return false;
			}

			return true;
		}

		/// <summary>Floor height under a world point, interpolated on a slope; zero outside the level
		/// or without a tile.</summary>
		public float FloorHeightAt(float pfWorldX, float pfWorldZ)
		{
			if (mOLevel == null || mOLevel.TileData == null)
				return 0f;

			UWTilePos lOTile = WorldToTile(pfWorldX, pfWorldZ);
			UWTile lOAt = GetTile(lOTile.X, lOTile.Y);

			return lOAt == null ? 0f : FloorHeightAt(lOAt, pfWorldX, pfWorldZ, lOTile.X, lOTile.Y);
		}

		/// <summary>
		/// Floor height at a world point within a given tile - on a slope linearly between
		/// FloorHeight (low edge) and FloorHeight + Slope (high edge) across the tile, as the floor
		/// mesh does. A thrown item placed at the flat FloorHeight would otherwise stick in a slope
		/// (confirmed from the original).
		/// </summary>
		public static float FloorHeightAt(UWTile pOTile, float pfWorldX, float pfWorldZ, int piTileX, int piTileZ)
		{
			if (pOTile.Slope == 0)
				return pOTile.FloorHeight;

			float lfLocalX = Math.Clamp((pfWorldX - UWUnits.TileToWorldAxis(piTileX)) / UWWorldScale.TileSize, 0f, 1f);
			float lfLocalZ = Math.Clamp((pfWorldZ - UWUnits.TileToWorldAxis(piTileZ)) / UWWorldScale.TileSize, 0f, 1f);

			switch (pOTile.TileType)
			{
				case UWTile.TileTypeEnum.slope_n: return pOTile.FloorHeight + (pOTile.Slope * lfLocalZ);
				case UWTile.TileTypeEnum.slope_s: return pOTile.FloorHeight + (pOTile.Slope * (1f - lfLocalZ));
				case UWTile.TileTypeEnum.slope_e: return pOTile.FloorHeight + (pOTile.Slope * lfLocalX);
				case UWTile.TileTypeEnum.slope_w: return pOTile.FloorHeight + (pOTile.Slope * (1f - lfLocalX));
				default: return pOTile.FloorHeight;
			}
		}

		/// <summary>Whether a sub-tile spot (0..7 each) lies on the open half of its tile - every
		/// spot of an open tile or a slope, none of a solid one, and on a diagonal the side named
		/// by its type (y counts north). The same halves UWLevelLoader.GetRandomPositionInTile
		/// scatters into.</summary>
		public static bool IsSubTileInOpenHalf(UWTile.TileTypeEnum peType, int piX, int piY)
		{
			switch (peType)
			{
				case UWTile.TileTypeEnum.solid: return false;
				case UWTile.TileTypeEnum.diagonal_ne: return piY >= 7 - piX;
				case UWTile.TileTypeEnum.diagonal_se: return piY < piX;
				case UWTile.TileTypeEnum.diagonal_nw: return piY >= piX;
				case UWTile.TileTypeEnum.diagonal_sw: return piY < 8 - piX;
				default: return true;
			}
		}

		/// <summary>Slopes and diagonals: floors on which an item cannot simply lie flat.</summary>
		public static bool IsUnevenFloor(UWTile.TileTypeEnum peType)
		{
			switch (peType)
			{
				case UWTile.TileTypeEnum.slope_n:
				case UWTile.TileTypeEnum.slope_e:
				case UWTile.TileTypeEnum.slope_s:
				case UWTile.TileTypeEnum.slope_w:
				case UWTile.TileTypeEnum.diagonal_se:
				case UWTile.TileTypeEnum.diagonal_sw:
				case UWTile.TileTypeEnum.diagonal_ne:
				case UWTile.TileTypeEnum.diagonal_nw:
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Pulls a sub-tile spot (0..7) away from blocked edges by the item radius in eighths: a
		/// solid neighbour, and with pbIncludeSteps also a neighbour whose floor is higher at the
		/// shared edge (a ramp that joins flush does not count). If a corner reaches into a blocked
		/// diagonal neighbour, the spot moves aside on the X axis, like the original does for
		/// creatures.
		/// </summary>
		public void KeepClearOfEdges(int piTileX, int piTileZ, ref int piX, ref int piY, int piRadius, bool pbIncludeSteps)
		{
			if (mOLevel == null || mOLevel.TileData == null)
				return;

			if (IsEdgeBlocked(piTileX, piTileZ, -1, 0, pbIncludeSteps))
				piX = Math.Max(piX, piRadius);

			if (IsEdgeBlocked(piTileX, piTileZ, 1, 0, pbIncludeSteps))
				piX = Math.Min(piX, 7 - piRadius);

			if (IsEdgeBlocked(piTileX, piTileZ, 0, -1, pbIncludeSteps))
				piY = Math.Max(piY, piRadius);

			if (IsEdgeBlocked(piTileX, piTileZ, 0, 1, pbIncludeSteps))
				piY = Math.Min(piY, 7 - piRadius);

			for (int liCornerY = -1; liCornerY <= 1; liCornerY += 2)
			{
				bool lbReachesY = liCornerY < 0 ? piY - piRadius < 0 : piY + piRadius > 7;

				if (!lbReachesY)
					continue;

				if (piX - piRadius < 0 && IsEdgeBlocked(piTileX, piTileZ, -1, liCornerY, pbIncludeSteps))
					piX = piRadius;

				if (piX + piRadius > 7 && IsEdgeBlocked(piTileX, piTileZ, 1, liCornerY, pbIncludeSteps))
					piX = 7 - piRadius;
			}

			piX = piX < 0 ? 0 : (piX > 7 ? 7 : piX);
			piY = piY < 0 ? 0 : (piY > 7 ? 7 : piY);
		}

		/// <summary>Whether something is in the way behind the edge (or corner) in direction
		/// piDx/piDz - see KeepClearOfEdges.</summary>
		public bool IsEdgeBlocked(int piTileX, int piTileZ, int piDx, int piDz, bool pbIncludeSteps)
		{
			int liNeighbourX = piTileX + piDx;
			int liNeighbourZ = piTileZ + piDz;

			if (liNeighbourX < 0 || liNeighbourZ < 0
				|| liNeighbourX >= UWWorldScale.TilesPerAxis || liNeighbourZ >= UWWorldScale.TilesPerAxis)
				return true;

			UWTile lONeighbour = mOLevel.TileData[(liNeighbourZ * UWWorldScale.TilesPerAxis) + liNeighbourX];

			if (lONeighbour == null || lONeighbour.TileType == UWTile.TileTypeEnum.solid)
				return true;

			if (!pbIncludeSteps)
				return false;

			UWTile lOOwn = mOLevel.TileData[(piTileZ * UWWorldScale.TilesPerAxis) + piTileX];

			if (lOOwn == null)
				return false;

			// The point on the shared edge (or corner), measured in both tiles.
			float lfEdgeX = UWUnits.TileToWorldAxis(piTileX)
				+ (piDx < 0 ? 0f : piDx > 0 ? UWWorldScale.TileSize : UWWorldScale.TileHalfSize);
			float lfEdgeZ = UWUnits.TileToWorldAxis(piTileZ)
				+ (piDz < 0 ? 0f : piDz > 0 ? UWWorldScale.TileSize : UWWorldScale.TileHalfSize);

			float lfNeighbourFloor = FloorHeightAt(lONeighbour, lfEdgeX, lfEdgeZ, liNeighbourX, liNeighbourZ);
			float lfOwnFloor = FloorHeightAt(lOOwn, lfEdgeX, lfEdgeZ, piTileX, piTileZ);

			return lfNeighbourFloor > lfOwnFloor + StepEdgeTolerance;
		}

		/// <summary>The game data the queries read.</summary>
		public DataImport Data => mOData;

		/// <summary>
		/// Removes an object from its tile list WITHOUT touching any display - for a creature that
		/// dies: its death animation may still run, but in the data it is gone. Otherwise it would
		/// be back on re-entering the level and in the save game.
		/// </summary>
		public bool ForgetObjectData(UWObject pOObject)
		{
			UWTile lOTile = FindTileHolding(pOObject);

			return lOTile != null && lOTile.ObjectsInTile.Remove(pOObject);
		}

		/// <summary>
		/// Moves an object into the list of another tile - when a creature has walked on. If it
		/// stays on its tile, the order stays as well. Whatever is in no list is not re-inserted.
		/// </summary>
		public void MoveObjectData(UWObject pOObject, int piTileX, int piTileZ)
		{
			if (pOObject == null || GetTile(piTileX, piTileZ) == null)
				return;

			UWTile lOCurrent = FindTileHolding(pOObject);
			UWTile lOTarget = GetTile(piTileX, piTileZ);

			if (lOCurrent == null)
				return;

			if (lOCurrent != lOTarget)
			{
				lOCurrent.ObjectsInTile.Remove(pOObject);
				lOTarget.ObjectsInTile.Add(pOObject);
			}

			pOObject.TileX = piTileX;
			pOObject.TileY = piTileZ;
		}

		/// <summary>The tile in whose list an object is. First the one it records itself, then all -
		/// a creature may have walked on without its data knowing.</summary>
		public UWTile FindTileHolding(UWObject pOObject)
		{
			if (pOObject == null || mOLevel == null || mOLevel.TileData == null)
				return null;

			UWTile lOOwn = GetTile(pOObject.TileX, pOObject.TileY);

			if (lOOwn != null && lOOwn.ObjectsInTile != null && lOOwn.ObjectsInTile.Contains(pOObject))
				return lOOwn;

			foreach (UWTile lOTile in mOLevel.TileData)
			{
				if (lOTile != null && lOTile.ObjectsInTile != null && lOTile.ObjectsInTile.Contains(pOObject))
					return lOTile;
			}

			return null;
		}

		/// <summary>Radius of an object in eighths of a tile from COMOBJ.DAT, 0 if unknown.</summary>
		public int ObjectRadius(int piObjectId)
		{
			UWCommonObjectProperties.Entry lOEntry;

			if (mOData == null || mOData.CommonObjectProperties == null
				|| !mOData.CommonObjectProperties.TryGet(piObjectId, out lOEntry))
				return 0;

			return lOEntry.Radius;
		}

		/// <summary>
		/// How far a trigger reaches, per axis in world units: its own radius plus that of the
		/// player, in eighths. For the a_move trigger (2 + 2) * 8 = 32, half a tile in each
		/// direction. Without an entry half a tile - better too generous than a trigger that never
		/// fires.
		/// </summary>
		public float TriggerReach(int piObjectId)
		{
			int liRadius = ObjectRadius(piObjectId);

			if (liRadius <= 0)
				return UWWorldScale.TileHalfSize;

			return (liRadius + ObjectRadius(PlayerObjectId)) * UWWorldScale.SubTileStep;
		}

		/// <summary>
		/// THE ORIGINAL'S CONTACT TEST, in eighth cells (see ScanForCollisions_seg026_BF6 in UWMotionCore, and
		/// see CreateColllisionRecord_seg026_A60 in UWMotionCore): the trigger's box is its
		/// cell +- its radius, the player's his cell +- (his radius - 1) - a creature mover is taken
		/// one eighth narrower - and they touch when the boxes overlap on both axes, i.e. when the
		/// cells differ by at most radius + player radius - 1 per axis: 3 for the a_move trigger.
		/// The world reach above (4 eighths) is the generous pre-filter the volume uses; the
		/// test itself counts cells (2026-10-06).
		/// </summary>
		public int TriggerReachCells(int piObjectId)
		{
			int liRadius = ObjectRadius(piObjectId);

			if (liRadius <= 0)
				return 4;

			int liPlayer = ObjectRadius(PlayerObjectId);

			return liRadius + (liPlayer > 0 ? liPlayer - 1 : 0);
		}
	}
}
