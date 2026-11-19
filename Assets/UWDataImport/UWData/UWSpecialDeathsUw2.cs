using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S PLOT DEATHS (UW2 plan G6 part 2, 2026-11-16), from Underworld Exhumed's uw2 event/WORLDEV.C:
	/// death_check (:703), stop_and_talk (:913), call_out_the_guards (:931) and genocide (:1686).
	///
	/// death_check runs twice, as the first game's SpecialDeathCases does (UWSpecialDeaths): with mode 0 before a creature
	/// with a whoami starts to die (go_into_dying_sequence, critter/AI.C:1419), where false keeps it alive, and with mode 1
	/// for every creature at the end of its dying sequence (critter_ai, AI.C:1109), right before its body goes. instant_kill
	/// (a schedule's kill, genocide) asks both and ignores the answer.
	///
	/// WHAT IT DOES, in the original's order: mode 1 on a creature of race 0xB turns every other of the race hostile; a
	/// bloodworm killed on level 4 counts in quest byte 7 (up to 200); the castle's people (whoami 0x81 to 0x8F, Syria 0xA8,
	/// the guard 0x95, loners not) cannot die - they keep a third of their table's hit points less one and the guards are
	/// called out to their home, except Lady Tory once the castle plot reached 8; then the named ones by whoami (see the
	/// constants); at the end the pit record's best goes into X clock 14.
	///
	/// THE PITS (G6 part 4, 2026-11-17): a listed pit fighter's death leaves the fight (remove_opponent) and counts in the
	/// pit record (UWArenaRulesUw2 for the rest of the arena). NOT HERE: the original's TimeStop = 0 and FixPlayerEquips in
	/// stop_and_talk have no counterpart wired for the second game.
	///
	/// A TALK: the original's TalkTo is modal, ours opens the conversation and returns. What the original does after
	/// TalkTo comes back is handed out as an action for the end of the conversation (Patterson's castle clock, Mors Gotha's
	/// quest), and a talk in mode 1 holds the body until then (UWCreatureActor).
	/// </summary>
	public static class UWSpecialDeathsUw2
	{
		// ------------------------------------------------- Who (the names from WORLDEV.C's notes)

		public const int MystellWhoAmI = 0x2C;

		public const int AltaraWhoAmI = 0x2D;

		public const int MokpoWhoAmI = 0x48;

		public const int BliySkupWhoAmI = 0x98;

		/// <summary>The guard that turns into a hordling the first time he would die.</summary>
		public const int HordlingGuardWhoAmI = 0x4B;

		public const int FreemisWhoAmI = 0x0B;

		public const int ZariaWhoAmI = 0x62;

		public const int DorstagWhoAmI = 0x63;

		public const int BlogWhoAmI = 0x65;

		public const int BishopWhoAmI = 6;

		public const int RelkWhoAmI = 0x31;

		public const int UmbriaWhoAmI = 0x1F;

		public const int LothWhoAmI = 0x20;

		public const int GothaWhoAmI = 0x2F;

		public const int ListenerWhoAmI = 0x91;

		public const int KrilnerWhoAmI = 0x64;

		public const int FissifWhoAmI = 0x80;

		public const int PattersonWhoAmI = UWScheduleRulesUw2.PattersonWhoAmI;

		public const int NelsonWhoAmI = UWScheduleRulesUw2.NelsonWhoAmI;

		public const int ToryWhoAmI = UWScheduleRulesUw2.ToryWhoAmI;

		/// <summary>The one whose second death sets quest 50 and shakes the screen.</summary>
		public const int CountedWhoAmI = 0x3A;

		/// <summary>case 3: dies without the pit record's check.</summary>
		public const int PlainWhoAmI = 3;

		/// <summary>The castle guard that cannot die beside the castle's people.</summary>
		public const int CastleGuardWhoAmI = 0x95;

		// ------------------------------------------------- What (uw2 include/items.h, player.h)

		public const int HostileRace = 0xB;

		public const int LichRace = 0x17;

		public const int BloodwormId = 0x4E;

		public const int HordlingId = 0x5E;

		public const int JewelledDaggerId = 0x0A;

		public const int BrokenDaggerId = 0xC7;

		public const int WormLevel = 4;

		public const int WormCountLimit = 0xC8;

		/// <summary>The quest numbers of quest_bytes[n]: 128 + n (QB_PIT_RECORD 1, QB_WORMS_KILLED 7, the counter 6).</summary>
		public const int PitRecordQuest = 0x80 + 1;

		public const int WormsKilledQuest = 0x80 + 7;

		public const int CountedQuest = 0x80 + 6;

		/// <summary>XC_PIT_KILLS: the best pit record.</summary>
		public const int PitKillsClock = 14;

		public const int HordlingHitPoints = 0x5C;

		public const int AttackGoal = 5;

		public const int GoHomeGoal = 1;

		/// <summary>The guards' post: the scheduled triggers in this square (Obj_FindInMapSquare(MAJOR_TRAP, 2, 0xC, ...)).</summary>
		public const int GuardPostX = 0xF;

		public const int GuardPostY = 0x1D;

		/// <summary>Where Nelson calls them to, and the square his home must lie in (castle plot 0xB on).</summary>
		public const int NelsonCallX = 0x12;

		public const int NelsonCallY = 0x28;

		/// <summary>Killorn Keep's world (Mors Gotha talks there instead of dying).</summary>
		public const int KillornWorld = 2;

		/// <summary>The wrong blow's flag (using_altaras_dagger, combat/COMBAT.C:625): set while the player's strike with the
		/// jewelled dagger lands, null otherwise; calling it breaks the dagger - out of the hand, a broken dagger (0xC7) put
		/// down beside the player (near_mob_put_at(ThePlayer, dagger, 6, 0)). UWPlayerMelee sets it around the blow.</summary>
		public static Action AltarasDaggerStrike;

		/// <summary>
		/// death_check(obj, mode) for the creature behind pOCreature. True lets the death go on (mode 1's answer is not
		/// asked by anyone). pfAfterTalk is non-null when a conversation was opened: run it once the conversation is over
		/// (what follows TalkTo in the original). Without a host or a record nothing happens and the death goes on.
		/// </summary>
		public static bool Check(UWCreatureRef pOCreature, int piMode, IUWScheduleHost pIHost, out Action pfAfterTalk)
		{
			pfAfterTalk = null;

			UWCritterRecord lORecord = pOCreature.Record;
			UWNpc lONpc = lORecord != null ? lORecord.Npc : null;
			UWPlayerFile lOFile = pIHost != null ? pIHost.Record : null;

			if (lONpc == null || lOFile == null || pIHost.World == null)
				return true;

			bool lbMode = piMode != 0;
			IUWWorld lOWorld = pIHost.World;
			int liWhoAmI = lONpc.NPCwhoami;
			UWLevel lOLevel = lOWorld.Level;

			if (lbMode && UWScheduleRulesUw2.RaceOf(lONpc, pIHost.Properties) == HostileRace)
			{
				foreach (UWNpc lOKin in UWScheduleRulesUw2.Select(lOLevel, UWScheduleRulesUw2.ByRace | (HostileRace << 8), pIHost.Properties))
					UWScheduleRulesUw2.GronkAttitude(lOKin, 0);
			}

			if (lbMode && lONpc.ID == BloodwormId && pIHost.LevelNumber == WormLevel
				&& UWPlayerRecordUw2.GetQuest(lOFile, WormsKilledQuest) < WormCountLimit)
				fAddQuestByte(lOFile, WormsKilledQuest, 1);

			// A pit fighter's death (G6 part 4): remove_opponent, and one that was listed counts in the pit record; quest 24
			// unless it was Krilner.
			bool lbPit = lbMode && UWPlayerRecordUw2.IsInPits(lOFile) && UWPlayerRecordUw2.RemovePitOpponent(lOFile, pOCreature.Index);

			if (lbPit)
			{
				fAddQuestByte(lOFile, PitRecordQuest, 1);

				if (liWhoAmI != KrilnerWhoAmI)
					UWPlayerRecordUw2.SetQuest(lOFile, 24, 1);
			}

			if (!lbMode && IsCastlePerson(liWhoAmI) && !lORecord.AttitudeLocked)
			{
				if (liWhoAmI == ToryWhoAmI && fCastle(lOFile) >= 8)
					return true;

				lORecord.HitPoints = (fAverageHits(lONpc, pIHost.Properties) / 3 - 1) & 0xFF;
				CallOutTheGuards(lORecord.HomeX, lORecord.HomeY, pIHost);

				return false;
			}

			switch (liWhoAmI)
			{
				case AltaraWhoAmI:
					UWPlayerRecordUw2.SetQuest(lOFile, 69, piMode);
					break;

				case MokpoWhoAmI:
					if (lbMode)
					{
						UWPlayerRecordUw2.SetQuest(lOFile, 53, 1);
						fChanged(lOFile);
					}
					break;

				case MystellWhoAmI:
					if (lbMode)
					{
						UWPlayerRecordUw2.SetQuest(lOFile, 66, 1);
						fChanged(lOFile);
					}
					break;

				case BliySkupWhoAmI:
					if (lbMode)
					{
						fFireTriggerAt(0x3A, 0x12, pIHost);
						fFireTriggerAt(0x3C, 0x12, pIHost);
						UWPlayerRecordUw2.SetQuest(lOFile, 122, 1);
					}
					break;

				case HordlingGuardWhoAmI:
					if (!lbMode && lONpc.ID != HordlingId)
					{
						lONpc.ID = HordlingId;
						lORecord.HitPoints = HordlingHitPoints;
						fSetGoal(lORecord, AttackGoal);
						lOWorld.RedrawObject(lONpc);

						return false;
					}
					break;

				case FreemisWhoAmI:
					UWPlayerRecordUw2.SetQuest(lOFile, 10, 1);
					break;

				case ZariaWhoAmI:
					UWPlayerRecordUw2.SetQuest(lOFile, 25, 1);

					if (lbPit)
						fAddQuestByte(lOFile, PitRecordQuest, 3);
					break;

				case DorstagWhoAmI:
					if (lbMode)
					{
						UWPlayerRecordUw2.SetQuest(lOFile, 121, 1);

						if (UWPlayerRecordUw2.GetQuest(lOFile, 23) == 0)
							fAddQuestByte(lOFile, PitRecordQuest, 6);
					}
					break;

				case BlogWhoAmI:
					if (lbMode)
					{
						UWPlayerRecordUw2.SetQuest(lOFile, 65, 1);
						fChanged(lOFile);

						if (UWPlayerRecordUw2.GetQuest(lOFile, 22) != 0 && UWPlayerRecordUw2.GetQuest(lOFile, 23) == 0)
							UWPlayerRecordUw2.SetQuest(lOFile, 22, 0);

						if (UWPlayerRecordUw2.GetQuest(lOFile, 23) != 0)
							fAddQuestByte(lOFile, PitRecordQuest, 6);
					}
					break;

				case BishopWhoAmI:
					if (lbMode)
						UWPlayerRecordUw2.SetQuest(lOFile, 4, 1);
					break;

				case RelkWhoAmI:
					if (lbMode)
						UWPlayerRecordUw2.SetQuest(lOFile, 123, 1);
					break;

				case UmbriaWhoAmI:
					if (lbMode)
					{
						// set_numbered_variable(0xF3, 2, 1) and player_did_bad(0x15): a crime against the golems' race.
						UWPlayerRecordUw2.SetNumbered(lOFile, 0xF3, 1);

						if (pIHost.Traps != null)
							pIHost.Traps.AngerRaceNearPlayer(0x15, UWCritterRules.TheftAreaBack, UWCritterRules.TheftAreaForward);
					}
					break;

				case LothWhoAmI:
					if (lbMode)
					{
						Genocide(LichRace, pIHost);
						UWPlayerRecordUw2.SetQuest(lOFile, 7, 1);
						fFireTriggerAt(1, 2, pIHost);
					}
					break;

				case GothaWhoAmI:
					if (!lbMode)
					{
						if ((pIHost.LevelNumber - 1) / UWScheduleRulesUw2.LevelsPerWorld == KillornWorld)
						{
							if (UWPlayerRecordUw2.GetQuest(lOFile, 54) == 0)
								pfAfterTalk = StopAndTalk(pOCreature, pIHost, null);
							else
								lOWorld.Remove(lONpc);
						}
						else
						{
							pfAfterTalk = StopAndTalk(pOCreature, pIHost, () =>
							{
								UWPlayerRecordUw2.SetQuest(lOFile, 64, 1);
								fChanged(lOFile);
							});

							return true;
						}

						return false;
					}
					break;

				case ListenerWhoAmI:
					if (!lbMode)
					{
						Action lfBreak = AltarasDaggerStrike;

						if (lfBreak == null)
						{
							lORecord.HitPoints = 1;
							fSetGoal(lORecord, AttackGoal);

							return false;
						}

						lfBreak();
						UWPlayerRecordUw2.SetXClock(lOFile, UWScheduleRulesUw2.CastleClock, fCastle(lOFile) + 1);
						UWPlayerRecordUw2.SetQuest(lOFile, 11, 1);
					}
					break;

				case KrilnerWhoAmI:
				case FissifWhoAmI:
					if (liWhoAmI == KrilnerWhoAmI)
					{
						if (lbPit)
							fAddQuestByte(lOFile, PitRecordQuest, -1);

						if (UWPlayerRecordUw2.GetQuest(lOFile, 28) != 0)
							return true;
					}

					if (liWhoAmI == FissifWhoAmI && UWPlayerRecordUw2.GetQuest(lOFile, 119) != 0)
						return true;

					if (!lbMode)
					{
						// The terrain bits of byte 0x0A mark "has yielded once", as the first game's golem's do.
						if (lORecord.TileState != 0)
							return true;

						lORecord.TileState = 1;

						if (lOWorld.Player != null && lOWorld.Player.Vitals != null)
							lOWorld.Player.Vitals.ChangeExperience(0x32, pIHost.LevelNumber);

						pfAfterTalk = StopAndTalk(pOCreature, pIHost, null);

						return false;
					}
					break;

				case PattersonWhoAmI:
					if (lbMode)
					{
						int liOld = fCastle(lOFile);

						pfAfterTalk = StopAndTalk(pOCreature, pIHost, () =>
						{
							int liNew = UWPlayerRecordUw2.XClock(lOFile, UWPlayerRecordUw2.DjinnClock) >= 6 ? 0xE : 0xD;

							UWPlayerRecordUw2.SetXClock(lOFile, UWScheduleRulesUw2.CastleClock, liNew < liOld ? liOld : liNew);
						});
					}
					return true;

				case NelsonWhoAmI:
					if (lbMode)
					{
						if (fCastle(lOFile) >= 0xB && lORecord.HomeX >= 0x14 && lORecord.HomeY >= 0x25
							&& lORecord.HomeX <= 0x16 && lORecord.HomeY <= 0x27)
							CallOutTheGuards(NelsonCallX, NelsonCallY, pIHost);

						return true;
					}
					break;

				case ToryWhoAmI:
					return !(lbMode && fCastle(lOFile) < 8);

				case CountedWhoAmI:
					if (lbMode)
					{
						fAddQuestByte(lOFile, CountedQuest, 1);

						if (UWPlayerRecordUw2.GetQuest(lOFile, CountedQuest) >= 2)
						{
							UWPlayerRecordUw2.SetQuest(lOFile, 50, 1);

							// do_sfx(4, 0x2C): the screen shakes by 0x58.
							if (lOWorld.Presentation != null)
								lOWorld.Presentation.ShakeScreen(0x2C << 1);

							UWPlayerRecordUw2.SetQuest(lOFile, CountedQuest, 0x18);
						}
					}
					return true;

				case PlainWhoAmI:
					return true;
			}

			int liRecord = UWPlayerRecordUw2.GetQuest(lOFile, PitRecordQuest);

			if (liRecord > UWPlayerRecordUw2.XClock(lOFile, PitKillsClock))
				UWPlayerRecordUw2.SetXClock(lOFile, PitKillsClock, liRecord);

			return true;
		}

		/// <summary>The castle's people who cannot die (whoami 0x81 to 0x8F, Syria 0xA8, the guard 0x95).</summary>
		public static bool IsCastlePerson(int piWhoAmI)
		{
			return (piWhoAmI >= UWScheduleRulesUw2.FirstCastleWhoAmI && piWhoAmI <= UWScheduleRulesUw2.LastCastleWhoAmI)
				|| piWhoAmI == UWScheduleRulesUw2.SyriaWhoAmI || piWhoAmI == CastleGuardWhoAmI;
		}

		/// <summary>
		/// stop_and_talk: no attacker, the player out of the fight, sequence 0 frame 0, attitude 2, goal 8, the talk, the
		/// no-heal bit cleared. Returns the action for the conversation's end: pfThen, or an empty one, so the caller
		/// knows a talk was opened.
		/// </summary>
		public static Action StopAndTalk(UWCreatureRef pOCreature, IUWScheduleHost pIHost, Action pfThen)
		{
			UWCritterRecord lORecord = pOCreature.Record;
			IUWWorld lOWorld = pIHost.World;

			lORecord.Attacker = 0;

			if (lOWorld.Player != null)
				lOWorld.Player.LeaveCombatMode();

			lORecord.RawSequence = 0;
			lORecord.Frame = 0;
			lORecord.Attitude = 2;
			fSetGoal(lORecord, 8);
			lOWorld.TalkTo(pOCreature);
			lORecord.NoLevelExitHealing = false;

			return pfThen ?? (() => { });
		}

		/// <summary>
		/// call_out_the_guards(x, y): from the first scheduled trigger (trap minor class 2, in-class 0xC) on the guards' post
		/// square on through the rest of its list, each one's target's link - a creation trap's guard template - gets the
		/// home x/y, goal 1 (go home), the temporary bit and the powerful bit, and the trigger goes off (UseTrigger with
		/// mode -1, by nobody). The original's player_looking test here has an empty body; it is left out. Returns how
		/// many fired.
		/// </summary>
		public static int CallOutTheGuards(int piHomeX, int piHomeY, IUWScheduleHost pIHost)
		{
			UWLevel lOLevel = pIHost.World != null ? pIHost.World.Level : null;
			UWTile lOTile = lOLevel != null ? lOLevel.GetTile(GuardPostX, GuardPostY) : null;

			if (lOTile == null || lOTile.ObjectsInTile == null || pIHost.Traps == null)
				return 0;

			List<UWObject> lOList = new List<UWObject>(lOTile.ObjectsInTile);
			int liFirst = lOList.FindIndex(o => o != null && UWTrapRulesUw2.IsTrigger(o.ID)
				&& (o.ID & 0xF) == UWScheduleRulesUw2.ScheduledTriggerInClass);
			int liFired = 0;

			if (liFirst < 0)
				return 0;

			for (int liAt = liFirst; liAt < lOList.Count; liAt++)
			{
				UWObject lOTrigger = lOList[liAt];

				if (lOTrigger == null)
					continue;

				UWObject lOTarget = UWObjectMechanics.GetLinkedObject(lOTrigger, lOLevel.Masterlist);

				if (lOTarget != null && UWObjectMechanics.GetLinkedObject(lOTarget, lOLevel.Masterlist) is UWNpc lOGuard)
				{
					UWCritterRecord lOGuardRecord = new UWCritterRecord(lOGuard);

					lOGuard.Quality = (ushort)((lOGuard.Quality & ~0x3F) | (piHomeX & 0x3F));
					lOGuard.Owner = (ushort)((lOGuard.Owner & ~0x3F) | (piHomeY & 0x3F));
					fSetGoal(lOGuardRecord, GoHomeGoal);
					lOGuard.NPCSpawned = true;
					lOGuardRecord.IsStrong = true;
				}

				// Only a trigger is used: the post's square holds nothing else in the shipped data.
				if (UWTrapRulesUw2.IsTrigger(lOTrigger.ID))
				{
					UWTrapRulesUw2.UseTrigger(null, lOTrigger, UWTrapRulesUw2.ChainMode, pIHost.Traps);
					liFired++;
				}
			}

			return liFired;
		}

		/// <summary>genocide(race): every active creature of the race dies at once (instant_kill), loners too.</summary>
		public static int Genocide(int piRace, IUWScheduleHost pIHost)
		{
			UWLevel lOLevel = pIHost.World != null ? pIHost.World.Level : null;
			int liKilled = 0;

			foreach (UWNpc lONpc in UWScheduleRulesUw2.Select(lOLevel, UWScheduleRulesUw2.AllCritters, pIHost.Properties))
			{
				if (UWScheduleRulesUw2.RaceOf(lONpc, pIHost.Properties) == piRace && pIHost.World.KillCreature(lONpc))
					liKilled++;
			}

			return liKilled;
		}

		private static void fFireTriggerAt(int piX, int piY, IUWScheduleHost pIHost)
		{
			if (pIHost.Traps != null)
				UWTrapRulesUw2.FireTriggerAt(piX, piY, pIHost.Traps);
		}

		/// <summary>SET_GOAL: the goal alone (stop_and_talk, the hordling, the Listener).</summary>
		private static void fSetGoal(UWCritterRecord pORecord, int piGoal)
		{
			pORecord.Goal = piGoal & UWNpcRecordCodec.GoalMask;
		}

		private static int fCastle(UWPlayerFile pOFile)
		{
			return UWPlayerRecordUw2.XClock(pOFile, UWScheduleRulesUw2.CastleClock);
		}

		/// <summary>XC_CHANGED one on, so the world rules (block 15) look again.</summary>
		private static void fChanged(UWPlayerFile pOFile)
		{
			UWPlayerRecordUw2.SetXClock(pOFile, UWScheduleRulesUw2.ChangedClock, UWPlayerRecordUw2.XClock(pOFile, UWScheduleRulesUw2.ChangedClock) + 1);
		}

		/// <summary>quest_bytes[n] += piBy, a byte that wraps.</summary>
		private static void fAddQuestByte(UWPlayerFile pOFile, int piQuest, int piBy)
		{
			UWPlayerRecordUw2.SetQuest(pOFile, piQuest, (UWPlayerRecordUw2.GetQuest(pOFile, piQuest) + piBy) & 0xFF);
		}

		/// <summary>Creature[].avghit, the table's average hit points (CRIT.DAT byte 4); 0 without a row.</summary>
		private static int fAverageHits(UWNpc pONpc, UWObjectClassProperties pOProperties)
		{
			return pOProperties != null && pOProperties.TryGetCritter(pONpc.ID, out UWObjectClassProperties.Critter lOCritter) ? lOCritter.Vitality : 0;
		}
	}
}
