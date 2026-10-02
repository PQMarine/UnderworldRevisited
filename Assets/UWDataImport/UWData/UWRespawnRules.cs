using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The periodic respawning of monsters (2026-09-06). Engine-free since 2026-09-18 (P3 of
	/// the engine separation), out of UWCreatureRespawner, which keeps the timer and looks at
	/// the creature bodies.
	///
	/// In the original there are two kinds of creation traps (a_create object trap, 0x0187), and
	/// the trap's FLAGS FIELD separates them. Cross-checked against all 72 creation traps in the game that
	/// lie in a tile - the pattern has no exception:
	///
	///   Flags non-zero        quality always 0, and a trigger ALWAYS points at it (usually an
	///                         a_move trigger on a neighbouring tile, sometimes a pickup,
	///                         use or look trigger). These are one-time ambushes,
	///                         and they run through UWTrapRules.
	///   Flags zero            quality 18 to 60, and a trigger NEVER points at it. Exactly
	///                         these are what the respawn handles.
	///
	/// THE PROCEDURE OF THE ORIGINAL, read 2026-09-23 - it was written from the reference
	/// until then, and that had the distance the wrong way round:
	///
	///   PlayerUpdates_seg028_2985_13D, in the five-minute block (24 player ticks), rolls
	///   RNG &amp; 3 == 0 - one in four - and calls TriggerCreateObjectTrap_ovr153_1517 with 1;
	///   Sleep_ovr143_D1F calls it with 0, right after resetting the mobile objects.
	///
	///   TriggerCreateObjectTrap walks the WHOLE map for creation traps with a zero flags field,
	///   marks the template creature (bit 8 of word 0x0D - our UWObject.WasSpawned on the
	///   copy) and fires the trap unless CheckIfWithin8TilesFromObject_ovr153_14C4 says the
	///   player is NEAR: fewer than eight tiles away on BOTH axes. So a creature comes only
	///   OUT OF SIGHT - at least eight tiles away on one axis - and when sleeping, where the
	///   argument 0 switches the test off, every such trap fires.
	///
	/// Until 2026-09-23 ours fired the traps INSIDE the square, as the reference does, and held
	/// the whole pass back while a respawned creature walked within four tiles of the player.
	/// That second check belongs to the trap itself in the original and looks at the trap's
	/// template, not at the player - see UWTrapRules.fFireCreateObjectTrap.
	/// </summary>
	public static class UWRespawnRules
	{
		/// <summary>Twenty-four player ticks - the same tick that spell durations run down
		/// with, so the length is taken from UWPlayerTick instead of being written out again:
		/// 504 seconds. It was thirty until 2026-09-22, when the count was read out properly
		/// (see UWPlayerTick.FiveMinuteTicks).</summary>
		public const float IntervalSeconds = UWPlayerTick.FiveMinuteTicks * UWPlayerTick.Seconds;

		/// <summary>One out of this many opportunities is taken.</summary>
		private const int Chance = 4;

		/// <summary>A trap fires only this far from the player on at least one axis - fewer
		/// tiles on both is "near" (CheckIfWithin8TilesFromObject_ovr153_14C4).</summary>
		public const int PlayerNearTiles = 8;

		/// <summary>The roll at the end of an interval: one in four.</summary>
		public static bool RollOpportunity()
		{
			return UWRandom.Next(Chance) == 0;
		}

		/// <summary>
		/// One pass of TriggerCreateObjectTrap_ovr153_1517: fires every respawning creation trap
		/// on the level that is not near the player - or every one at all, when sleeping.
		/// </summary>
		/// <param name="pbAnyDistance">The sleeping call, the original's argument 0: no
		/// distance test.</param>
		/// <param name="pFFireTrap">Fires a creation trap on its tile; true when it ran.</param>
		/// <returns>How many traps were fired.</returns>
		public static int Run(UWLevel pOLevel, UWTilePos pOPlayerTile, bool pbAnyDistance,
			Func<UWObject, int, int, bool> pFFireTrap)
		{
			if (pOLevel == null || pOLevel.TileData == null || pFFireTrap == null)
				return 0;

			int liCount = 0;

			for (int liX = 0; liX < UWWorldScale.TilesPerAxis; liX++)
			{
				for (int liY = 0; liY < UWWorldScale.TilesPerAxis; liY++)
				{
					if (!pbAnyDistance && IsNearPlayer(pOPlayerTile, liX, liY))
						continue;

					UWTile lOTile = pOLevel.TileData[(liY * UWWorldScale.TilesPerAxis) + liX];

					if (lOTile == null || lOTile.ObjectsInTile == null)
						continue;

					// Over a copy: firing puts a creature into the tile list.
					UWObject[] lOObjects = lOTile.ObjectsInTile.ToArray();

					foreach (UWObject lOObject in lOObjects)
					{
						if (lOObject == null || lOObject.ID != UWObjectMechanics.CreateObjectTrapId)
							continue;

						// With the flags field set, the trap hangs off a trigger and is not
						// respawned.
						if (lOObject.Flags != 0)
							continue;

						if (pFFireTrap(lOObject, liX, liY))
							liCount++;
					}
				}
			}

			return liCount;
		}

		/// <summary>CheckIfWithin8TilesFromObject_ovr153_14C4: near is fewer than
		/// PlayerNearTiles on BOTH axes.</summary>
		public static bool IsNearPlayer(UWTilePos pOPlayerTile, int piTileX, int piTileY)
		{
			return Math.Abs(pOPlayerTile.X - piTileX) < PlayerNearTiles
				&& Math.Abs(pOPlayerTile.Y - piTileY) < PlayerNearTiles;
		}

	}
}
