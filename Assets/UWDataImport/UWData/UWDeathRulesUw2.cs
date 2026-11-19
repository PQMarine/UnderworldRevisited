using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S DEATH (player_is_dead, uw2 game/SKILLS.C:802): a ninth of the experience goes, and outside
	/// Britannia (above the eighth level) the player wakes by the blackrock gem on level 5 (do_gem, :667), near
	/// 28/40; in Britannia it is the end (real_death). The numbers at the gem are do_resurrect's, UWDeathRules'.
	///
	/// G6 PART 3 (2026-11-16): killed on level 1 by one of the castle's people (UWSpecialDeathsUw2.IsCastlePerson, not a
	/// loner), the player wakes in jail instead (put_player_in_jail, event/WORLDEV.C:2283); a fight in the pits lost, the
	/// pit fighters stand down before the death goes on. Who killed him is the player object's last_hit, which
	/// damage_critter writes (critter/AI.C:1493) and arriving on level 1 clears (do_level_hacks). Waking from the Ethereal
	/// Void (punt_void) comes with the Void, which ours does not send the player into yet.
	/// </summary>
	public static class UWDeathRulesUw2
	{
		public enum OutcomeEnum
		{
			Gem,

			Final,

			Jail
		}

		/// <summary>The last level of Britannia, the first world.</summary>
		public const int LastBritanniaLevel = 8;

		/// <summary>The blackrock gem's level and the place do_gem moves the player from.</summary>
		public const int GemLevel = 5;

		public const int GemTileX = 0x1C;

		public const int GemTileY = 0x28;

		/// <summary>move_along(rand(), 8): how far from that place.</summary>
		public const int GemDistance = 8;

		/// <summary>"You regain awareness in the cavern containing the pulsating blackrock gem."</summary>
		public const int RegainString = 0x169;

		private const int ExperienceLossDivisor = 9;

		/// <summary>ITEM_BLOOD_STAIN_DF, left where the player wakes.</summary>
		public const int WakingStain = 0xDF;

		public static OutcomeEnum Decide(int piLevel)
		{
			return piLevel > LastBritanniaLevel ? OutcomeEnum.Gem : OutcomeEnum.Final;
		}

		/// <summary>player_is_dead's order: jail first (level 1, the last one to hit a castle person not a loner), then the
		/// gem or the end.</summary>
		public static OutcomeEnum Decide(int piLevel, UWNpc pOKiller)
		{
			return JailsKiller(piLevel, pOKiller) ? OutcomeEnum.Jail : Decide(piLevel);
		}

		// ------------------------------------------------- Jail (G6 part 3)

		/// <summary>The cell: level 1, 0x2A/0x26 (and 0x2B beside it, do_teleport's test).</summary>
		public const int JailLevel = 1;

		public const int JailTileX = 0x2A;

		public const int JailTileY = 0x26;

		public const int JailCellLastX = 0x2B;

		/// <summary>The castle guards' race (0x1C), calmed: attitude 2, goal 1 with target 0.</summary>
		public const int GuardRace = 0x1C;

		public const int CalmAttitude = 2;

		public const int GoHomeGoal = 1;

		/// <summary>Quest 112: in jail; 124: escaped.</summary>
		public const int JailedQuest = 112;

		public const int EscapedQuest = 124;

		/// <summary>Lord British (0x8E) taken outside the cell: to 0x2A/0x22, home 0x28/0x27, goal 1.</summary>
		public const int BritishWhoAmI = 0x8E;

		public const int BritishTileX = 0x2A;

		public const int BritishTileY = 0x22;

		public const int BritishHomeX = 0x28;

		public const int BritishHomeY = 0x27;

		/// <summary>The cell's trigger (fire_trigger_at(0x27, 0x25)).</summary>
		public const int JailTriggerX = 0x27;

		public const int JailTriggerY = 0x25;

		/// <summary>The player object's last_hit in the workspace (byte 0x12 of the object at its start).</summary>
		public const int PlayerLastHitOffset = 0x12;

		/// <summary>Whether this killer sends the player to jail: on level 1, one of the castle's people, no loner.</summary>
		public static bool JailsKiller(int piLevel, UWNpc pOKiller)
		{
			return piLevel == JailLevel && pOKiller != null && UWSpecialDeathsUw2.IsCastlePerson(pOKiller.NPCwhoami)
				&& !new UWCritterRecord(pOKiller).AttitudeLocked;
		}

		/// <summary>The time put_player_in_jail passes, in seconds: (40 - the clock's minute modulo 20) minutes less 60 to
		/// 149 seconds (piRoll is rand()).</summary>
		public static int JailSeconds(int piGameClock, int piRoll)
		{
			int liHour = (int)((uint)piGameClock / 0x3C00 % 0x14);

			return ((0x28 - liHour) * 0x3C) - ((piRoll % 0x5A) + 0x3C);
		}

		/// <summary>pass_time (WORLDEV.C:162): the game clock on by the seconds (in 1/256 s), the day clock from it. The
		/// schedules' catching up is the caller's.</summary>
		public static void PassTime(UWPlayerFile pOFile, int piSeconds)
		{
			if (pOFile == null)
				return;

			UWBinary.Write32(pOFile.Record, UWPlayerRecordUw2.GameClockOffset, UWPlayerRecordUw2.GameClock(pOFile) + (piSeconds << 8));
			UWPlayerRecordUw2.SetDayClockFromClock(pOFile);
		}

		/// <summary>The player object's last_hit: who struck him last (a mobile index), 0 for nobody.</summary>
		public static int PlayerLastHit(UWPlayerFile pOFile)
		{
			return pOFile != null && pOFile.Workspace != null && pOFile.Workspace.Length > PlayerLastHitOffset ? pOFile.Workspace[PlayerLastHitOffset] : 0;
		}

		/// <summary>damage_critter on the player: a known attacker is remembered (the caller passes no 0); arriving on
		/// level 1 writes 0.</summary>
		public static void SetPlayerLastHit(UWPlayerFile pOFile, int piWho)
		{
			if (pOFile != null && pOFile.Workspace != null && pOFile.Workspace.Length > PlayerLastHitOffset)
				pOFile.Workspace[PlayerLastHitOffset] = (byte)piWho;
		}

		/// <summary>The jail's world part: the guards' race calmed (attitude 2, then goal 1 with target 0, loners passed by)
		/// and quest 112 set. Lord British, the trigger and the time are the caller's.</summary>
		public static void CalmTheGuards(UWLevel pOLevel, UWObjectClassProperties pOProperties, UWPlayerFile pOFile)
		{
			List<UWNpc> lOGuards = UWScheduleRulesUw2.Select(pOLevel, UWScheduleRulesUw2.ByRace | (GuardRace << 8), pOProperties);

			foreach (UWNpc lOGuard in lOGuards)
				UWScheduleRulesUw2.GronkAttitude(lOGuard, CalmAttitude);

			foreach (UWNpc lOGuard in lOGuards)
				UWScheduleRulesUw2.GronkChangeGoal(lOGuard, GoHomeGoal, 0);

			UWPlayerRecordUw2.SetQuest(pOFile, JailedQuest, 1);
		}

		/// <summary>Lord British, null without him.</summary>
		public static UWNpc FindBritish(UWLevel pOLevel)
		{
			return UWTrapRules.FindActiveByWhoAmI(pOLevel, BritishWhoAmI);
		}

		/// <summary>Lord British outside the cell after the move: his home and goal 1.</summary>
		public static void SendBritishHome(UWNpc pOBritish)
		{
			if (pOBritish == null)
				return;

			UWCritterRecord lORecord = new UWCritterRecord(pOBritish);

			pOBritish.Quality = (ushort)((pOBritish.Quality & ~0x3F) | BritishHomeX);
			pOBritish.Owner = (ushort)((pOBritish.Owner & ~0x3F) | BritishHomeY);
			lORecord.Goal = GoHomeGoal;
		}

		/// <summary>do_teleport's jail test: with quest 112 set, a teleport of the player anywhere but the cell (level 0 or
		/// 1, y 0x26, x 0x2A or 0x2B) is an escape - quest 112 cleared, 124 set. True when it was one.</summary>
		public static bool CheckEscape(UWPlayerFile pOFile, int piDestLevel, int piX, int piY)
		{
			if (UWPlayerRecordUw2.GetQuest(pOFile, JailedQuest) == 0)
				return false;

			if ((piDestLevel == 0 || piDestLevel == JailLevel) && piY == JailTileY && piX >= JailTileX && piX <= JailCellLastX)
				return false;

			UWPlayerRecordUw2.SetQuest(pOFile, JailedQuest, 0);
			UWPlayerRecordUw2.SetQuest(pOFile, EscapedQuest, 1);

			return true;
		}

		// ------------------------------------------------- The pits lost (G6 part 3)

		/// <summary>player_is_dead in the pits: every pit fighter stands down - attitude 1, no last hit, goal 1 - before
		/// UWPlayerRecordUw2.ClearPits empties the list. Returns how many were found in the level.</summary>
		public static int PitFightersStandDown(UWLevel pOLevel, UWPlayerFile pOFile)
		{
			if (pOLevel == null || pOFile == null || !UWPlayerRecordUw2.IsInPits(pOFile))
				return 0;

			int liFound = 0;

			for (int liAt = 0; liAt < UWPlayerRecordUw2.PitFighterCount; liAt++)
			{
				int liIndex = UWPlayerRecordUw2.PitFighter(pOFile, liAt);

				if (liIndex <= 0 || liIndex >= pOLevel.Masterlist.Count || !(pOLevel.Masterlist[liIndex] is UWNpc lOFighter))
					continue;

				UWCritterRecord lORecord = new UWCritterRecord(lOFighter);

				lORecord.Attitude = 1;
				lORecord.Attacker = 0;
				lORecord.Goal = GoHomeGoal;
				liFound++;
			}

			return liFound;
		}

		public static int ExperienceLoss(int piExperience)
		{
			return piExperience / ExperienceLossDivisor;
		}

		/// <summary>move_along (sys/UTIL.C:35): the point piDistance from the tile at the heading, of 256 to the circle
		/// and counted back from 0x140; a step that moves at all gains one.</summary>
		public static void MoveAlong(int piHeading, int piDistance, ref int piX, ref int piY)
		{
			double lfAngle = ((0x140 - piHeading) & 0xFF) * Math.PI * 2.0 / 256.0;

			piY += fStep(Math.Sin(lfAngle), piDistance);
			piX += fStep(Math.Cos(lfAngle), piDistance);
		}

		private static int fStep(double pfUnit, int piDistance)
		{
			int liStep = ((int)(pfUnit * 0x7FFF) / 0x80 * piDistance) / 0x100;

			return liStep > 0 ? liStep + 1 : liStep < 0 ? liStep - 1 : 0;
		}
	}
}
