using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S LEVEL HACKS (UW2 plan G6 part 5, 2026-11-17), do_level_hacks (Underworld Exhumed uw2
	/// game/GAMEWRAP.C:443) with its helpers from event/WORLDEV.C (prison_alarm_check, Killorn_just_crashed,
	/// remove_TK_wand, find_TK_wand_check, genocide(0xFF), destroy_floatskull) and gruesome_door_hack (GAMEWRAP.C:412).
	/// Mode 1 is leaving a level (with that level still in hand), mode 0 arriving.
	///
	/// ALREADY ELSEWHERE: the time passing on leaving (UWLevelExit.LeaveLevel), last_hit cleared on arriving at level 1
	/// (UWDeathRulesUw2.SetPlayerLastHit), the Void's automap (UWAutomapStateUw2.ChangeLevel). Arriving with player->b60_11
	/// set only runs clearobj in the original - a flag ours never sets. The walking music is UWLevelLoader's (phase 5 part A); the effects need no reload in ours; Killorn's crash's screen effect (do_sfx(4, 0), a shake of 0,
	/// and fadeout3d) falls into the level change's own fade.
	/// </summary>
	public static class UWLevelHacksUw2
	{
		public const int LevelsPerWorld = 8;

		public const int PrisonWorld = 1;

		public const int KillornWorld = 2;

		public const int AcademyWorld = 5;

		public const int TombsWorld = 6;

		/// <summary>quests[12] bit 2: the keep is about to crash; 54: it has.</summary>
		public const int KeepFallsQuest = 50;

		public const int KeepCrashedQuest = 54;

		/// <summary>quests[1] bit 3: Praecor Loth is dead.</summary>
		public const int LothDeadQuest = 7;

		/// <summary>prison_alarm_check: race 6 hostile sets quest 60.</summary>
		public const int GoblinRace = 6;

		public const int GoblinAlarmQuest = 60;

		/// <summary>Leaving the Prison Tower's second level fires these two squares' triggers.</summary>
		public static readonly int[][] PrisonLevel2Triggers = { new[] { 0x1F, 0x24 }, new[] { 0x23, 0x22 } };

		/// <summary>The telekinesis wand: ITEM_WAND_9B with the door-direction bit, spell major -1 effect 0x27; put back at
		/// fine 0xFB/0xEB (tile 31/29, 3/3), height 0x50, on the level left.</summary>
		public const int TelekinesisWandId = 0x9B;

		public const int TelekinesisEffect = 0x27;

		public const int WandHomeX8 = 0xFB;

		public const int WandHomeY8 = 0xEB;

		public const int WandHomeZ = 0x50;

		/// <summary>The castle door hack's lock pick (checkLock(0, door, -0x2D)).</summary>
		public const int DoorHackPickSkill = 0x2D;

		public const int FloatingSkullId = 0x13;

		public const int FallenSkullId = 0xC3;

		/// <summary>is_my_race's 0xFF: every undead.</summary>
		public const int UndeadRace = 0xFF;

		public static int WorldOf(int piLevel) => (piLevel - 1) / LevelsPerWorld;

		public static int LevelInWorld(int piLevel) => ((piLevel - 1) % LevelsPerWorld) + 1;

		/// <summary>Leaving: the prison alarm in the Prison Tower.</summary>
		public static bool ChecksPrisonAlarm(int piLevel) => WorldOf(piLevel) == PrisonWorld;

		/// <summary>Leaving: the Prison Tower's level 2 fires two triggers.</summary>
		public static bool FiresPrisonTriggers(int piLevel) => WorldOf(piLevel) == PrisonWorld && LevelInWorld(piLevel) == 2;

		/// <summary>Leaving Killorn's level 1 with quest 50 set: the keep crashes (Killorn_just_crashed(1)).</summary>
		public static bool KeepCrashes(int piLevel, UWPlayerFile pOFile)
		{
			return WorldOf(piLevel) == KillornWorld && LevelInWorld(piLevel) == 1 && UWPlayerRecordUw2.GetQuest(pOFile, KeepFallsQuest) != 0;
		}

		/// <summary>Leaving level 3 of Killorn (the case falls through) or of the Academy: the telekinesis wand goes back.</summary>
		public static bool TakesTelekinesisWand(int piLevel)
		{
			return (WorldOf(piLevel) == KillornWorld || WorldOf(piLevel) == AcademyWorld) && LevelInWorld(piLevel) == 3;
		}

		/// <summary>Arriving in the Tombs with Loth dead: genocide(0xFF).</summary>
		public static bool TombsCleansed(int piLevel, UWPlayerFile pOFile)
		{
			return WorldOf(piLevel) == TombsWorld && UWPlayerRecordUw2.GetQuest(pOFile, LothDeadQuest) != 0;
		}

		/// <summary>
		/// prison_alarm_check: unless quest 60 is set already, every hostile non-loner of race 6 among the active creatures
		/// sets it and moves XC_CHANGED on - once for each, as the original's loop does. Returns how many.
		/// </summary>
		public static int PrisonAlarmCheck(UWLevel pOLevel, UWObjectClassProperties pOProperties, UWPlayerFile pOFile)
		{
			if (pOFile == null || UWPlayerRecordUw2.GetQuest(pOFile, GoblinAlarmQuest) == 1)
				return 0;

			int liFound = 0;

			foreach (UWNpc lONpc in UWScheduleRulesUw2.Select(pOLevel, UWScheduleRulesUw2.AllCritters, pOProperties))
			{
				UWCritterRecord lORecord = new UWCritterRecord(lONpc);

				if (lORecord.Attitude != 0 || UWScheduleRulesUw2.RaceOf(lONpc, pOProperties) != GoblinRace || lORecord.AttitudeLocked)
					continue;

				UWPlayerRecordUw2.SetQuest(pOFile, GoblinAlarmQuest, 1);
				UWPlayerRecordUw2.SetXClock(pOFile, UWScheduleRulesUw2.ChangedClock, UWPlayerRecordUw2.XClock(pOFile, UWScheduleRulesUw2.ChangedClock) + 1);
				liFound++;
			}

			return liFound;
		}

		/// <summary>Killorn_just_crashed(1): quest 54 and XC_CHANGED on (no Sched_SetAllClocks while entering).</summary>
		public static void KeepCrashed(UWPlayerFile pOFile)
		{
			UWPlayerRecordUw2.SetQuest(pOFile, KeepCrashedQuest, 1);
			UWPlayerRecordUw2.SetXClock(pOFile, UWScheduleRulesUw2.ChangedClock, UWPlayerRecordUw2.XClock(pOFile, UWScheduleRulesUw2.ChangedClock) + 1);
		}

		/// <summary>find_TK_wand_check: a wand 0x9B with the door-direction bit whose spell decodes to major -1, effect 0x27.</summary>
		public static bool IsTelekinesisWand(UWObject pOThing, IList<UWObject> pORecords)
		{
			if (pOThing == null || pOThing.ID != TelekinesisWandId || !pOThing.DoorDirection)
				return false;

			return UWEnchantSpellRulesUw2.TryDecode(pOThing, pORecords, out UWObject lOSpell, out int liMajor, out int liEffect, out bool lbCastOnUse)
				&& liMajor == -1 && liEffect == TelekinesisEffect;
		}

		/// <summary>Obj_Check over a chain and the contents below it: the first thing pfMatch takes, null without one.</summary>
		public static UWObject FindIn(IEnumerable<UWObject> pOThings, System.Func<UWObject, bool> pfMatch, System.Func<UWObject, IEnumerable<UWObject>> pfContents)
		{
			if (pOThings == null)
				return null;

			foreach (UWObject lOThing in pOThings)
			{
				if (lOThing == null)
					continue;

				if (pfMatch(lOThing))
					return lOThing;

				UWObject lOInside = FindIn(pfContents(lOThing), pfMatch, pfContents);

				if (lOInside != null)
					return lOInside;
			}

			return null;
		}

		/// <summary>
		/// genocide(0xFF) on the level: every active undead creature dies at once (IUWWorld.KillCreature, instant_kill) and
		/// every floating skull falls (destroy_floatskull: it becomes the plain skull 0xC3, quality 0 where can_place does not
		/// take it). The contents the original frees with the skull stay. Returns the creatures killed and the skulls.
		/// </summary>
		public static int Genocide(IUWWorld pIWorld, UWObjectClassProperties pOProperties, UWCommonObjectProperties pOCommon, out int piSkulls)
		{
			piSkulls = 0;

			UWLevel lOLevel = pIWorld != null ? pIWorld.Level : null;

			if (lOLevel == null || lOLevel.ActiveMobiles == null)
				return 0;

			int liKilled = 0;

			foreach (int liIndex in new List<int>(lOLevel.ActiveMobiles))
			{
				UWObject lOObject = liIndex > 0 && liIndex < lOLevel.Masterlist.Count ? lOLevel.Masterlist[liIndex] : null;

				if (lOObject == null)
					continue;

				if (!UWObjectIds.IsCreature(lOObject.ID))
				{
					if (lOObject.ID == FloatingSkullId && fDestroyFloatSkull(lOObject, pIWorld))
						piSkulls++;

					continue;
				}

				if (fIsUndead(lOObject, pOCommon) && pIWorld.KillCreature(lOObject))
					liKilled++;
			}

			return liKilled;
		}

		private static bool fIsUndead(UWObject pOObject, UWCommonObjectProperties pOCommon)
		{
			return pOCommon != null && pOCommon.TryGet(pOObject.ID, out UWCommonObjectProperties.Entry lOEntry) && UWDamageTypes.IsUndead(lOEntry.Resistances);
		}

		private static bool fDestroyFloatSkull(UWObject pOSkull, IUWWorld pIWorld)
		{
			pOSkull.ID = FallenSkullId;

			if (!pIWorld.CanPlace(FallenSkullId, (pOSkull.TileX << 3) + pOSkull.XPos, (pOSkull.TileY << 3) + pOSkull.YPos, pOSkull.ZPos, true, 0))
				pOSkull.Quality = 0;

			pIWorld.RedrawObject(pOSkull);

			return true;
		}
	}
}
