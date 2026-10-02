using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// What the original does when the player LEAVES a level (the reference:
	/// LevelChangeEventsUW1 in the ExitLevelMode branch calls uwObject.ResetMobileObjects, which
	/// in turn calls npc.ReturnNPCToHomeTile for every creature).
	///
	/// It is the answer to an old observation by the user: a dorg that was pushed away stays
	/// where it is, and is only back in its place after a LEVEL CHANGE
	/// (2026-09-05). So the clean-up is not part of loading, but of leaving.
	///
	/// Five things happen to every creature of the level:
	///
	///   1. Creatures created during play (summoned, from a trap) disappear.
	///   2. Allies are no longer allies, and the combat markers are dropped (in our port those
	///      only live at runtime anyway and are recreated on build).
	///   3. Anyone who does not currently want to talk (goal 10) turns in a random direction.
	///   4. Anyone below the average vitality of their kind gets half of it
	///      added - that is why wounded enemies are back in shape after an excursion.
	///   5. Anyone not standing on their home tile is moved there.
	///
	/// Afterwards the original balances the attitude per KIND: every hostile creature of a kind
	/// subtracts one step, every friendly one adds one, and the result is applied to all
	/// creatures of that kind. So whoever antagonises the goblins has to deal with
	/// all goblins on the next visit.
	///
	/// TWO DEVIATIONS FROM THE REFERENCE, both deliberate:
	///
	/// In ReturnNPCToHomeTile the reference moves the creature to the tile from npc_xhome
	/// (word 0x16). That is the tile it is already standing on - the step would have
	/// no effect, and the function is not called "ToHomeTile" for nothing. We take the home
	/// from Quality and Owner, the same fields that wander goal 8 heads for, and thereby
	/// agree with the observation of the original.
	///
	/// SCD.ARK, which the reference reads for schedules, DOES NOT EXIST in uw1 at all (neither in
	/// C:\UW\DATA nor in any of the user's save games, and the reference only calls it under
	/// _RES == GAME_UW2). Scheduled events are an innovation of uw2.
	/// </summary>
	public static class UWLevelExit
	{
		/// <summary>The kind of a creature is in byte 0x09 of the creature table
		/// (GenericNameIndex) - the same field the group alarm uses to find others of its
		/// kind. Being a byte, it allows at most 256 kinds.</summary>
		private const int GeneralTypeCount = 256;

		private const int MaxAttitude = UWNpc.MaxAttitude;

		private const int HostileAttitude = UWNpc.AttitudeHostile;

		private const int FriendlyAttitude = UWNpc.AttitudeFriendly;

		/// <summary>Goal 10: the creature wants to talk to the player and keeps its
		/// facing direction.</summary>
		private const int GoalWantToTalk = UWNpc.GoalWantToTalk;

		private const int MaxHitPoints = UWNpc.MaxHitPoints;

		/// <summary>
		/// Cleans up the level the player is currently leaving. Afterwards the level is
		/// torn down; we therefore work on the DATA, not on the game objects.
		/// </summary>
		public static void LeaveLevel(UWTileQueries pOLoader)
		{
			if (pOLoader == null || pOLoader.Level == null || pOLoader.Level.TileData == null)
				return;

			List<UWNpc> lONpcs = fCollectNpcs(pOLoader);

			int[] liMood = new int[GeneralTypeCount];
			int liSentHome = 0;
			int liRemoved = 0;

			foreach (UWNpc lONpc in lONpcs)
			{
				if (lONpc.NPCSpawned)
				{
					pOLoader.ForgetObjectData(lONpc);
					liRemoved++;
					continue;
				}

				lONpc.NPCIsAlly = false;

				if (lONpc.NPCGoal != GoalWantToTalk)
					lONpc.Heading = (ushort)UWRandom.Next(0, 8); // reference npc.cs: obj.heading = Rng & 7

				fHeal(pOLoader, lONpc);
				fCountMood(pOLoader, lONpc, liMood);

				if (fSendHome(pOLoader, lONpc))
					liSentHome++;
			}

			fSpreadMood(pOLoader, lONpcs, liMood);

			if (liSentHome > 0 || liRemoved > 0)
				UWLog.Info(string.Format("Level left: {0} creatures sent home, {1} removed.",
					liSentHome, liRemoved));
		}

		/// <summary>All creatures of the level, as a separate list - the tile lists change
		/// during the clean-up.</summary>
		private static List<UWNpc> fCollectNpcs(UWTileQueries pOLoader)
		{
			List<UWNpc> lOFound = new List<UWNpc>();

			foreach (UWTile lOTile in pOLoader.Level.TileData)
			{
				if (lOTile == null || lOTile.ObjectsInTile == null)
					continue;

				foreach (UWObject lOObject in lOTile.ObjectsInTile)
				{
					UWNpc lONpc = lOObject as UWNpc;

					if (lONpc != null)
						lOFound.Add(lONpc);
				}
			}

			return lOFound;
		}

		/// <summary>Half the average vitality of the kind on top, as long as the creature
		/// is below it (the reference: avghit is byte 0x04 of the creature table, in our port
		/// Vitality).</summary>
		private static void fHeal(UWTileQueries pOLoader, UWNpc pONpc)
		{
			if (pONpc.NPCNoHealing)
				return;

			UWObjectClassProperties.Critter lOStats;

			if (!fTryGetStats(pOLoader, pONpc, out lOStats) || lOStats.Vitality == 0)
				return;

			if (pONpc.HitPoints >= lOStats.Vitality)
				return;

			pONpc.HitPoints = (byte)System.Math.Min(MaxHitPoints, pONpc.HitPoints + (lOStats.Vitality / 2));
			pONpc.NPC_HP = pONpc.HitPoints;
		}

		/// <summary>Counts how the kind feels about the player: every hostile creature one step
		/// down, every friendly one up.</summary>
		private static void fCountMood(UWTileQueries pOLoader, UWNpc pONpc, int[] piMood)
		{
			if (pONpc.NPCAttitudeLocked)
				return;

			int liType;

			if (!fTryGetGeneralType(pOLoader, pONpc, out liType))
				return;

			if (pONpc.NPCAttitude == HostileAttitude)
				piMood[liType]--;
			else if (pONpc.NPCAttitude == FriendlyAttitude)
				piMood[liType]++;
		}

		/// <summary>Writes the counted mood into every creature of the kind.</summary>
		private static void fSpreadMood(UWTileQueries pOLoader, List<UWNpc> pONpcs, int[] piMood)
		{
			foreach (UWNpc lONpc in pONpcs)
			{
				if (lONpc.NPCAttitudeLocked || lONpc.NPCSpawned)
					continue;

				int liType;

				if (!fTryGetGeneralType(pOLoader, lONpc, out liType) || piMood[liType] == 0)
					continue;

				lONpc.NPCAttitude = (byte)System.Math.Max(0, System.Math.Min(MaxAttitude, lONpc.NPCAttitude + piMood[liType]));
			}
		}

		/// <summary>
		/// Moves a creature to its home tile (Quality and Owner).
		///
		/// WHERE IN THE TILE follows the original since 2026-09-24 (CalmNPCS_ovr104_115, the
		/// same spot routine as the sleep ambush, UWSleepRules.TryGetAmbushSpot): 4/4 on an open
		/// tile, a corner of the open half on a diagonal, and IF IT DOES NOT FIT there the
		/// creature stays where it is. A flier is set halfway between the floor and the top of
		/// the level ((floor * 8 + 0x80) / 2 in zpos), everyone else onto the floor. Until then
		/// ours put everyone on 3/3 (the reference's moveNPCToTile) and never refused.
		/// THE FIT is the original's item-fits-in-tile test; ours asks the open half of the tile
		/// and whether another creature in the home tile stands closer than both radii.
		/// </summary>
		private static bool fSendHome(UWTileQueries pOLoader, UWNpc pONpc)
		{
			int liHomeX = pONpc.Quality;
			int liHomeY = pONpc.Owner;
			int liSize = UWWorldScale.TilesPerAxis;

			if (liHomeX < 0 || liHomeX >= liSize || liHomeY < 0 || liHomeY >= liSize)
				return false;

			if (pONpc.TileX == liHomeX && pONpc.TileY == liHomeY)
				return false;

			UWTile lOHome = pOLoader.Level.TileData[(liHomeY * liSize) + liHomeX];

			if (lOHome == null)
				return false;

			if (!UWSleepRules.TryGetAmbushSpot(lOHome.TileType, out int liSubX, out int liSubY)
				|| !UWTileQueries.IsSubTileInOpenHalf(lOHome.TileType, liSubX, liSubY)
				|| fIsSpotTaken(pOLoader, lOHome, pONpc, liSubX, liSubY))
				return false;

			pOLoader.MoveObjectData(pONpc, liHomeX, liHomeY);

			int liFloorZ = System.Math.Max(0, System.Math.Min(127,
				UWUnits.RoundToInt(lOHome.FloorHeight / UWWorldScale.ZPosStep)));

			UWObjectClassProperties.Critter lOStats;

			if (fTryGetStats(pOLoader, pONpc, out lOStats) && lOStats.IsFlier)
				liFloorZ = (liFloorZ + FlierHeightTop) / 2;

			pONpc.XPos = (ushort)liSubX;
			pONpc.YPos = (ushort)liSubY;
			pONpc.ZPos = (ushort)liFloorZ;

			// The word at 0x16 is the current tile and must match the tile list, otherwise
			// the creature appears twice in the original (see UWWorldSync.fCaptureCritter).
			pONpc.NPCXHome = (byte)liHomeX;
			pONpc.NPCYHome = (byte)liHomeY;

			return true;
		}

		/// <summary>The zpos a flier's height is averaged with - the top of the level.</summary>
		private const int FlierHeightTop = 0x80;

		/// <summary>Another creature in the home tile closer to the spot than both COMOBJ radii
		/// together, in eighths.</summary>
		private static bool fIsSpotTaken(UWTileQueries pOLoader, UWTile pOHome, UWNpc pONpc, int piSubX, int piSubY)
		{
			if (pOHome.ObjectsInTile == null)
				return false;

			int liOwn = fGetRadius(pOLoader, pONpc.ID);

			foreach (UWObject lOObject in pOHome.ObjectsInTile)
			{
				UWNpc lOOther = lOObject as UWNpc;

				if (lOOther == null || lOOther == pONpc)
					continue;

				int liReach = liOwn + fGetRadius(pOLoader, lOOther.ID);
				int liDx = lOOther.XPos - piSubX;
				int liDy = lOOther.YPos - piSubY;

				if ((liDx * liDx) + (liDy * liDy) < liReach * liReach)
					return true;
			}

			return false;
		}

		private static int fGetRadius(UWTileQueries pOLoader, int piObjectId)
		{
			UWCommonObjectProperties.Entry lOEntry;

			return pOLoader.Data != null && pOLoader.Data.CommonObjectProperties != null
				&& pOLoader.Data.CommonObjectProperties.TryGet(piObjectId, out lOEntry)
				? lOEntry.Radius
				: 0;
		}

		private static bool fTryGetStats(UWTileQueries pOLoader, UWNpc pONpc,
			out UWObjectClassProperties.Critter pOStats)
		{
			pOStats = default;

			return pOLoader.Data != null
				&& pOLoader.Data.ObjectClassProperties != null
				&& pOLoader.Data.ObjectClassProperties.TryGetCritter(pONpc.ID, out pOStats);
		}

		private static bool fTryGetGeneralType(UWTileQueries pOLoader, UWNpc pONpc, out int piType)
		{
			UWObjectClassProperties.Critter lOStats;

			piType = 0;

			if (!fTryGetStats(pOLoader, pONpc, out lOStats))
				return false;

			piType = lOStats.GenericNameIndex;

			return piType >= 0 && piType < GeneralTypeCount;
		}
	}
}
