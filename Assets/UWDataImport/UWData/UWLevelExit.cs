using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// What the time pass needs from whoever holds the level's creatures. On a level change the
	/// level is torn down afterwards and only the data counts (LeaveLevel); after a night the
	/// creatures stay in the scene, and their bodies follow what the pass wrote (UWSleep).
	/// </summary>
	public interface IUWCreatureTimeHost
	{
		/// <summary>A temporary creature leaves the level for good: out of its tile list, its
		/// slot back, its picture gone at once.</summary>
		void RemoveCreature(UWNpc pONpc);

		/// <summary>The pass rewrote this creature's record (tile, fine position, height, facing,
		/// hit points): the body and its health follow it.</summary>
		void CreatureChanged(UWNpc pONpc);
	}

	/// <summary>
	/// TIME PASSES FOR A LEVEL'S CREATURES (update_all_critters_whilst_player_snoozes with up_crit,
	/// UnderworldExhumed CRITTIME.C). The original runs it when the player LEAVES a level
	/// (do_level_hacks, GAMEWRAP.C, mode 1) and after every UNDISTURBED NIGHT (player_sleep,
	/// SKILLS.C, right before DoWanderingMonsters(0)); ours ran it on the level change only until
	/// 2026-10-15 (reference audit round 2, creatures D2).
	///
	/// It is the answer to an old observation by the user: a dorg that was pushed away stays
	/// where it is, and is only back in its place after a LEVEL CHANGE (2026-09-05).
	///
	/// For every creature of the level, in up_crit's order:
	///
	///   1. A temporary creature (word 0x0D bit 8: summoned, from a trap) is removed.
	///   2. The fed and ally bits and byte 0x19 bits 0, 1, 4 and 5 (target confirmed, heard
	///      something, made a stand, relentless) are cleared.
	///   3. It faces a random eighth - every creature, the one waiting to talk (goal 10) too.
	///   4. Below its kind's average hit points it heals HALF WAY to the average, never above.
	///   5. Unless a loner, it counts towards its race's mood: hostile -1, friendly +1.
	///   6. Away from its home post (quality and owner) it is set down there if it fits.
	///
	/// Afterwards every creature that is not a loner shifts its attitude by its race's net count,
	/// clamped to 0..3. So whoever antagonises the goblins has to deal with all goblins.
	///
	/// THE PASS WRITES THE CREATURE RECORD (UWCritterRecord), the bytes the creature's mind reads
	/// and the save takes as its base. Until 2026-10-15 it wrote the parsed UWNpc fields only, so
	/// on a level re-entered without saving the mood, the ally reset and the moved tile had no
	/// effect until a save and load (reference audit round 2, creatures D5).
	///
	/// NOT HERE: up_mob, what the pass does to a mobile object that is not a creature (a missile
	/// or a throw in flight is deleted or dropped). Ours keeps flying things outside the tile lists.
	///
	/// SCD.ARK, which the reference reads for schedules, DOES NOT EXIST in uw1 at all; scheduled
	/// events are an innovation of uw2.
	/// </summary>
	public static class UWLevelExit
	{
		/// <summary>The race of a creature is byte 0x09 of the creature table (GenericNameIndex),
		/// the same field the kin alarm uses. Being a byte, it allows at most 256.</summary>
		private const int RaceCount = 256;

		private const int MaxAttitude = UWNpc.MaxAttitude;

		private const int HostileAttitude = UWNpc.AttitudeHostile;

		private const int FriendlyAttitude = UWNpc.AttitudeFriendly;

		/// <summary>The facing eighths a creature may be turned to (rand() % 8).</summary>
		private const int FacingEighths = 8;

		/// <summary>
		/// Cleans up the level the player is leaving, in its data: the level is torn down
		/// afterwards, so nothing has to follow in the scene.
		/// </summary>
		public static void LeaveLevel(UWWorldEdit pOEdit)
		{
			int liRemoved;
			int liSentHome = PassTime(pOEdit, new DataOnlyHost(pOEdit), out liRemoved);

			if (liSentHome > 0 || liRemoved > 0)
				UWLog.Info(string.Format("Level left: {0} creatures sent home, {1} removed.", liSentHome, liRemoved));
		}

		/// <summary>
		/// The pass over every creature of the level - see the class comment. Returns how many
		/// were sent home; piRemoved how many temporary creatures went.
		/// </summary>
		public static int PassTime(UWWorldEdit pOEdit, IUWCreatureTimeHost pIHost, out int piRemoved)
		{
			piRemoved = 0;

			UWTileQueries lOQueries = pOEdit != null ? pOEdit.Queries : null;

			if (lOQueries == null || lOQueries.Level == null || lOQueries.Level.TileData == null || pIHost == null)
				return 0;

			List<UWNpc> lONpcs = fCollectNpcs(lOQueries);
			List<UWCritterRecord> lOKept = new List<UWCritterRecord>();

			int[] liMood = new int[RaceCount];
			int liSentHome = 0;

			foreach (UWNpc lONpc in lONpcs)
			{
				if (lONpc.NPCSpawned)
				{
					pIHost.RemoveCreature(lONpc);
					piRemoved++;
					continue;
				}

				UWCritterRecord lORecord = new UWCritterRecord(lONpc);

				lOKept.Add(lORecord);

				ClearTransientBits(lORecord);
				lORecord.FacingEighth = UWRandom.Next(FacingEighths);

				fHeal(lOQueries, lORecord);
				fCountMood(lOQueries, lORecord, liMood);

				if (fSendHome(pOEdit, lORecord))
					liSentHome++;

				pIHost.CreatureChanged(lONpc);
			}

			fSpreadMood(lOQueries, lOKept, liMood);

			return liSentHome;
		}

		/// <summary>The ally bit survives the checkup when the roll of four comes up this.</summary>
		private const int AllyKeptRoll = 1;

		/// <summary>
		/// THE FIVE-MINUTE CHECKUP (yearly_checkup, UnderworldExhumed CRITTIME.C, called from
		/// duration_check every 24th player tick, PLAYTIME.C): for every creature of the level the
		/// fed bit becomes a coin (rand() % 2), and the ally bit is cleared unless rand() % 4 is 1 -
		/// an ally (the Ally spell, a conversation) usually stops being one within a block or two
		/// and acts by its attitude again. Ours had no counterpart until 2026-10-15: an ally stayed
		/// one until the player left the level, and the fed bit never changed (audit round 2,
		/// creatures D7). Written into the record, which the mind reads at once.
		/// </summary>
		public static void YearlyCheckup(UWTileQueries pOQueries)
		{
			if (pOQueries == null || pOQueries.Level == null || pOQueries.Level.TileData == null)
				return;

			foreach (UWNpc lONpc in fCollectNpcs(pOQueries))
			{
				UWCritterRecord lORecord = new UWCritterRecord(lONpc);

				lORecord.Fed = UWRandom.Next(2) == 1;

				if (UWRandom.Next(4) != AllyKeptRoll)
					lORecord.IsAlly = false;
			}
		}

		/// <summary>up_crit's first writes: fed, ally and byte 0x19 bits 4, 5, 0 and 1.</summary>
		public static void ClearTransientBits(UWCritterRecord pORecord)
		{
			pORecord.Fed = false;
			pORecord.IsAlly = false;
			pORecord.MadeStand = false;
			pORecord.Relentless = false;
			pORecord.TargetConfirmed = false;
			pORecord.HeardSomething = false;
		}

		/// <summary>
		/// up_crit's healing: below the kind's average (avghit, table byte 0x04 - our Vitality)
		/// the hit points go half way to it, (hp + avghit) / 2, never above it. Ours added half
		/// the average on top until 2026-10-15, which lifted a scratched creature above its kind's
		/// average (avghit 30, hp 28: the original 29, ours 43; reference audit round 2,
		/// creatures D4).
		/// </summary>
		public static int HealedHitPoints(int piHitPoints, int piAverage)
		{
			return piHitPoints < piAverage ? (piHitPoints + piAverage) / 2 : piHitPoints;
		}

		/// <summary>The mood shift up_crit counts for one creature: -1 hostile, +1 friendly, 0
		/// otherwise.</summary>
		public static int MoodOf(int piAttitude)
		{
			if (piAttitude == HostileAttitude)
				return -1;

			return piAttitude == FriendlyAttitude ? 1 : 0;
		}

		/// <summary>The attitude after the race's net count, clamped to 0..3.</summary>
		public static int ShiftedAttitude(int piAttitude, int piRaceCount)
		{
			return System.Math.Max(0, System.Math.Min(MaxAttitude, piAttitude + piRaceCount));
		}

		/// <summary>All creatures of the level, as a separate list - the tile lists change
		/// during the pass.</summary>
		private static List<UWNpc> fCollectNpcs(UWTileQueries pOQueries)
		{
			List<UWNpc> lOFound = new List<UWNpc>();

			foreach (UWTile lOTile in pOQueries.Level.TileData)
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

		private static void fHeal(UWTileQueries pOQueries, UWCritterRecord pORecord)
		{
			if (pORecord.NoLevelExitHealing)
				return;

			UWObjectClassProperties.Critter lOStats;

			if (!fTryGetStats(pOQueries, pORecord.Npc, out lOStats))
				return;

			int liHealed = HealedHitPoints(pORecord.HitPoints, lOStats.Vitality);

			if (liHealed == pORecord.HitPoints)
				return;

			pORecord.HitPoints = liHealed;
			pORecord.Npc.NPC_HP = (byte)liHealed;
		}

		private static void fCountMood(UWTileQueries pOQueries, UWCritterRecord pORecord, int[] piMood)
		{
			if (pORecord.AttitudeLocked)
				return;

			int liRace;

			if (fTryGetRace(pOQueries, pORecord.Npc, out liRace))
				piMood[liRace] += MoodOf(pORecord.Attitude);
		}

		private static void fSpreadMood(UWTileQueries pOQueries, List<UWCritterRecord> pORecords, int[] piMood)
		{
			foreach (UWCritterRecord lORecord in pORecords)
			{
				int liRace;

				if (lORecord.AttitudeLocked || !fTryGetRace(pOQueries, lORecord.Npc, out liRace) || piMood[liRace] == 0)
					continue;

				lORecord.Attitude = ShiftedAttitude(lORecord.Attitude, piMood[liRace]);
			}
		}

		/// <summary>
		/// Moves a creature to its home post (quality and owner), up_crit's last part.
		///
		/// WHERE IN THE TILE follows the original since 2026-09-24 (the same spot routine as the
		/// sleep ambush, UWSleepRules.TryGetAmbushSpot): 4/4 on an open tile, a corner of the open
		/// half on a diagonal, and IF IT DOES NOT FIT there the creature stays where it is. A flier
		/// is set halfway between the floor and the top of the level ((floor * 8 + 0x80) / 2 in
		/// zpos), everyone else onto the floor.
		/// THE FIT is the original's can_place (COLLIDE.C); ours asks the open half of the tile and
		/// whether another creature in the home tile stands closer than both radii.
		/// The current tile is the record's word 0x16, which the original compares with the post.
		/// </summary>
		private static bool fSendHome(UWWorldEdit pOEdit, UWCritterRecord pORecord)
		{
			UWTileQueries lOQueries = pOEdit.Queries;
			int liHomeX = pORecord.HomeX;
			int liHomeY = pORecord.HomeY;

			if (pORecord.TileX == liHomeX && pORecord.TileY == liHomeY)
				return false;

			UWTile lOHome = lOQueries.Level.GetTile(liHomeX, liHomeY);

			if (lOHome == null)
				return false;

			if (!UWSleepRules.TryGetAmbushSpot(lOHome.TileType, out int liSubX, out int liSubY)
				|| !UWTileQueries.IsSubTileInOpenHalf(lOHome.TileType, liSubX, liSubY)
				|| fIsSpotTaken(lOQueries, lOHome, pORecord.Npc, liSubX, liSubY))
				return false;

			int liFloorZ = System.Math.Max(0, System.Math.Min(127,
				UWUnits.RoundToInt(lOHome.FloorHeight / UWWorldScale.ZPosStep)));

			UWObjectClassProperties.Critter lOStats;

			if (fTryGetStats(lOQueries, pORecord.Npc, out lOStats) && lOStats.IsFlier)
				liFloorZ = (liFloorZ + FlierHeightTop) / 2;

			pOEdit.Move(pORecord.Npc, liHomeX, liHomeY);

			// The word at 0x16 is the current tile and must match the tile list, otherwise
			// the creature appears twice in the original (see UWWorldSync.fCaptureCritter).
			pORecord.TileX = liHomeX;
			pORecord.TileY = liHomeY;
			pORecord.FineX = liSubX;
			pORecord.FineY = liSubY;
			pORecord.ZPos = liFloorZ;

			return true;
		}

		/// <summary>The zpos a flier's height is averaged with - the top of the level.</summary>
		private const int FlierHeightTop = 0x80;

		/// <summary>Another creature in the home tile closer to the spot than both COMOBJ radii
		/// together, in eighths.</summary>
		private static bool fIsSpotTaken(UWTileQueries pOQueries, UWTile pOHome, UWNpc pONpc, int piSubX, int piSubY)
		{
			if (pOHome.ObjectsInTile == null)
				return false;

			int liOwn = pOQueries.ObjectRadius(pONpc.ID);

			foreach (UWObject lOObject in pOHome.ObjectsInTile)
			{
				UWNpc lOOther = lOObject as UWNpc;

				if (lOOther == null || lOOther == pONpc)
					continue;

				int liReach = liOwn + pOQueries.ObjectRadius(lOOther.ID);
				int liDx = lOOther.XPos - piSubX;
				int liDy = lOOther.YPos - piSubY;

				if ((liDx * liDx) + (liDy * liDy) < liReach * liReach)
					return true;
			}

			return false;
		}

		private static bool fTryGetStats(UWTileQueries pOQueries, UWNpc pONpc,
			out UWObjectClassProperties.Critter pOStats)
		{
			pOStats = default;

			return pOQueries.Data != null
				&& pOQueries.Data.ObjectClassProperties != null
				&& pOQueries.Data.ObjectClassProperties.TryGetCritter(pONpc.ID, out pOStats);
		}

		private static bool fTryGetRace(UWTileQueries pOQueries, UWNpc pONpc, out int piRace)
		{
			UWObjectClassProperties.Critter lOStats;

			piRace = 0;

			if (!fTryGetStats(pOQueries, pONpc, out lOStats))
				return false;

			piRace = lOStats.GenericNameIndex;

			return piRace >= 0 && piRace < RaceCount;
		}

		/// <summary>The level change's host: the level is torn down afterwards, so a removed
		/// creature only leaves the data (its slot comes back) and nothing has to follow.</summary>
		private sealed class DataOnlyHost : IUWCreatureTimeHost
		{
			private readonly UWWorldEdit mOEdit;

			public DataOnlyHost(UWWorldEdit pOEdit)
			{
				mOEdit = pOEdit;
			}

			public void RemoveCreature(UWNpc pONpc)
			{
				RemoveFromLevel(mOEdit, pONpc);
			}

			public void CreatureChanged(UWNpc pONpc)
			{
			}
		}

		/// <summary>Out of whichever tile list holds it, its slot back and ObjectRemoved raised
		/// (Obj_FreeLinkChain in up_crit). UWWorldEdit.Remove looks in the tile the object
		/// records, so that is set from the list first.</summary>
		public static void RemoveFromLevel(UWWorldEdit pOEdit, UWNpc pONpc)
		{
			if (pOEdit == null || pONpc == null)
				return;

			UWTile lOHolding = pOEdit.Queries != null ? pOEdit.Queries.FindTileHolding(pONpc) : null;

			if (lOHolding != null)
			{
				pONpc.TileX = lOHolding.X;
				pONpc.TileY = lOHolding.Z;
			}

			pOEdit.Remove(pONpc);
		}
	}
}
