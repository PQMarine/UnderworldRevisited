using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The named creatures whose death is not an ordinary one
	/// (SpecialDeathCases_ovr107_149F). Seven whoami numbers have a case there, and the
	/// routine runs at TWO moments: `ProcessDeath_seg007_1798_3577` asks it with mode 0
	/// BEFORE the death and lets its answer veto the whole thing, while
	/// `NPCInitialProcessing_seg007_2488` asks with mode 1 for a creature that is already
	/// dead, which is where the consequences belong.
	///
	/// WHAT EACH ONE DOES
	///
	///   0x0B  Thorlson (conversation 11), cut content: he is TALKED TO instead of
	///         dying, gets sixty hit points back, and the death is refused.
	///   0x16  the golem of level 6, and this one is in the game: the first time he is beaten
	///         down he gives 500 experience, then he stops fighting, stands, talks - and
	///         does not die either; his hit points stay at 0 until his script sets them.
	///         The "first time" is remembered in his own record, in the tile state bits of
	///         byte 0x0A, which the original misuses as a marker.
	///   0x18  the prisoner: quest flag 6, friend of the lizard folk.
	///   0x1B  Garamon: the talismans can no longer be destroyed (PLAYER.DAT 0x62 bit 2).
	///   0x6E  the gazer: quest flag 4.
	///   0x8E  Rodric: quest flag 11.
	///   0xE7  Tyball: see UWSpecialDeaths.TyballAllies - the cutscene, the dream bit, his
	///         allies and the traps of his prison.
	///
	/// Engine-free; what has to touch the world goes through IUWWorld, what belongs to the creature
	/// through its record (UWCreatureRef), the story bits through UWGameFlags. Until cleanup step
	/// 3.6 (2026-10-17) a host of its own (UWSpecialDeaths.IHost, nine members, implemented on
	/// UWCritter) did all of it.
	/// </summary>
	public static class UWSpecialDeaths
	{
		/// <summary>When the question is asked - the original's mode argument.</summary>
		public enum Stage
		{
			/// <summary>Mode 0: the creature is about to die and may refuse.</summary>
			Dying,

			/// <summary>Mode 1: it is dead, and the consequences follow.</summary>
			Dead
		}

		public const int TalkerWhoami = 0x0B;

		public const int MetalGolemWhoami = 0x16;

		public const int PrisonerWhoami = 0x18;

		public const int GaramonWhoami = 0x1B;

		public const int GazerWhoami = 0x6E;

		public const int RodricWhoami = 0x8E;

		public const int TyballWhoami = 0xE7;

		/// <summary>What the golem is worth, once (EXPChange with 0x1F4).</summary>
		private const int GolemExperience = 500;

		/// <summary>The hit points the nameless one gets back (byte 8 = 0x3C). The golem gets
		/// none: he stays at 0, so his script sees "beaten" (npc_hp below 10), hands over the
		/// Shield of Valor and sets his hit points to 125 itself (per user, 2026-09-28: with 60 our
		/// golem took the "I would give thee a chance to live" branch instead).</summary>
		private const int SurvivorHitPoints = 60;

		private const int PrisonerQuestFlag = 6;

		private const int GazerQuestFlag = 4;

		private const int RodricQuestFlag = 11;

		/// <summary>The dying Tyball.</summary>
		private const int TyballCutscene = 2;

		/// <summary>Which bit of the dream flag Tyball's death sets (word 0x6E bit 2; bit 3 is
		/// Garamon's burial, see UWSleepRules).</summary>
		private const int TyballDreamBit = 4;

		/// <summary>Tyball's allies, by whoami, out of the table beside TyballDeath_ovr107_13D1
		/// (TybalAlliesToRemove_dseg_5c99_12C0). The original walks it from index nine down to
		/// one, so the table's first entry, 0xDE, is NOT among them.</summary>
		public static readonly int[] TyballAllies =
			{ 0xD1, 0xDB, 0xD2, 0xDC, 0xD5, 0xD8, 0xD4, 0xD3, 0xDD };

		/// <summary>The tile of his prison, whose move triggers go with him (UWQuestPlaces).</summary>
		private const int TyballPrisonTileX = UWQuestPlaces.TyballPrisonTileX;

		private const int TyballPrisonTileY = UWQuestPlaces.TyballPrisonTileY;

		/// <summary>The move trigger that locked the player in.</summary>
		private const int MoveTriggerObjectId = UWObjectMechanics.MoveTriggerId;

		/// <summary>
		/// The special case of this whoami, if there is one. Returns FALSE when the creature
		/// does not die after all - only the nameless one and the golem do that, and only
		/// while dying. pOCreature is the dying or dead creature itself (its record carries the
		/// hit points, the stand-down and the golem's marker). Without a world nothing special
		/// happens, as without a host until cleanup step 3.6 (2026-10-17).
		/// </summary>
		public static bool Apply(int piWhoami, Stage peStage, IUWWorld pOWorld, UWCreatureRef pOCreature)
		{
			if (pOWorld == null)
				return true;

			if (peStage == Stage.Dying)
				return fApplyWhileDying(piWhoami, pOWorld, pOCreature);

			fApplyWhenDead(piWhoami, pOWorld);

			return true;
		}

		/// <summary>Whether this whoami has a case at all - the host can spare itself the work
		/// for everyone else.</summary>
		public static bool HasSpecialDeath(int piWhoami)
		{
			return piWhoami == TalkerWhoami || piWhoami == MetalGolemWhoami
				|| piWhoami == PrisonerWhoami || piWhoami == GaramonWhoami
				|| piWhoami == GazerWhoami || piWhoami == RodricWhoami
				|| piWhoami == TyballWhoami;
		}

		private static bool fApplyWhileDying(int piWhoami, IUWWorld pOWorld, UWCreatureRef pOCreature)
		{
			UWCritterRecord lORecord = pOCreature.Record;

			if (piWhoami == TalkerWhoami)
			{
				pOWorld.TalkTo(pOCreature);

				// Byte 8, the hit points.
				if (lORecord != null)
					lORecord.HitPoints = SurvivorHitPoints;

				return false;
			}

			if (piWhoami != MetalGolemWhoami)
				return true;

			// The reward only the first time; the marker is his own tile state.
			if (fTryMarkRewarded(lORecord))
				fAwardExperience(pOWorld, GolemExperience);

			fStandDown(pOWorld, lORecord);
			pOWorld.TalkTo(pOCreature);

			return false;
		}

		/// <summary>The golem's marker: the original misuses the tile state bits of byte 0x0A for it,
		/// and so do we - they are recomputed by the physics anyway. True the first time, false
		/// afterwards.</summary>
		private static bool fTryMarkRewarded(UWCritterRecord pORecord)
		{
			if (pORecord == null || pORecord.TileState != 0)
				return false;

			pORecord.TileState = 1;

			return true;
		}

		/// <summary>EXPChange with the given tenths, as the golem's reward - the dungeon level is
		/// what the player stands on, exactly as with a kill.</summary>
		private static void fAwardExperience(IUWWorld pOWorld, int piExperience)
		{
			UWPlayerVitals lOVitals = pOWorld.Player.Vitals;

			if (lOVitals != null)
				lOVitals.ChangeExperience(piExperience, pOWorld.LevelIndex + 1);
		}

		/// <summary>Ends the fight and sets the creature standing: no attacker (byte 0x12),
		/// EndCombat, animation 0x20, frame 0 - the hit points stay as they are.</summary>
		private static void fStandDown(IUWWorld pOWorld, UWCritterRecord pORecord)
		{
			if (pORecord != null)
			{
				pORecord.Attacker = 0;
				pORecord.Animation = UWCritterBrain.AnimStanding;
				pORecord.Frame = 0;
			}

			pOWorld.Player.LeaveCombatMode();
		}

		private static void fApplyWhenDead(int piWhoami, IUWWorld pOWorld)
		{
			switch (piWhoami)
			{
				case PrisonerWhoami:
					UWQuestFlags.Set(PrisonerQuestFlag, 1);
					return;

				case GazerWhoami:
					UWQuestFlags.Set(GazerQuestFlag, 1);
					return;

				case RodricWhoami:
					UWQuestFlags.Set(RodricQuestFlag, 1);
					return;

				case GaramonWhoami:
					// PLAYER.DAT 0x62 bit 2 - see UWPlayerData.TalismansDestroyable.
					UWGameFlags.TalismansDestroyable = false;
					return;

				case TyballWhoami:
					fApplyTyball(pOWorld);
					return;
			}
		}

		/// <summary>TyballDeath_ovr107_13D1: the cutscene, the dream bit, his allies, and the
		/// move triggers of the prison he locked the player into.</summary>
		private static void fApplyTyball(IUWWorld pOWorld)
		{
			pOWorld.Presentation.PlayCutscene(TyballCutscene);

			UWQuestFlags.Set(UWPlayerData.DreamQuestFlag,
				UWQuestFlags.Get(UWPlayerData.DreamQuestFlag) | TyballDreamBit);

			RemoveCreaturesByWhoami(pOWorld, TyballAllies);
			RemoveObjectsInTile(pOWorld, TyballPrisonTileX, TyballPrisonTileY, MoveTriggerObjectId);
		}

		/// <summary>
		/// Removes, for each of these whoami numbers, the FIRST creature that carries it in the
		/// level's list of allocated mobiles (UWLevel.GetActiveMobileOrder; see
		/// RunFunctionOnWhoAmIList_seg038_3307_D4E, which stops at the first match; per user,
		/// 2026-09-29, the original removed nine of fourteen).
		///
		/// ONE CREATURE PER ENTRY (RunFunctionOnWhoAmIList_seg038_3307_D4E with 0 for "go on": it
		/// stops at the first match in the active mobile list). Per user, 2026-09-29: the original
		/// removed nine of the fourteen - one of the three 209 guards, one of the two 219 and one of
		/// the three 220 - while ours removed all. The first is the first in the level's list of
		/// allocated mobiles (UWLevel.GetActiveMobileOrder, the file's list at 0x7AFC): the
		/// original's #222 before #223 and #200 is exactly that order.
		///
		/// A snapshot from the world's search (cleanup step 3.2, 2026-10-17; before, a scene search):
		/// the removals below do not disturb it. A dead one (its body destroyed) is passed over. On
		/// the special-death host until cleanup step 3.6 (the same day).
		/// </summary>
		public static void RemoveCreaturesByWhoami(IUWWorld pOWorld, int[] piWhoamis)
		{
			if (piWhoamis == null)
				return;

			UWLevel lOLevel = pOWorld.Level;
			List<UWCreatureRef> lOCreatures = new List<UWCreatureRef>();

			pOWorld.FindCreatures(UWTileArea.WholeLevel, UWCreatureFilter.Any, lOCreatures);

			foreach (int liWhoami in piWhoamis)
			{
				UWNpc lOFirst = null;
				int liFirstOrder = int.MaxValue;

				foreach (UWCreatureRef lOCreature in lOCreatures)
				{
					UWNpc lONpc = lOCreature.Record != null ? lOCreature.Record.Npc : null;

					if (lONpc == null || lONpc.NPCwhoami != liWhoami || lOCreature.IsDead)
						continue;

					int liOrder = lOLevel != null ? lOLevel.GetActiveMobileOrder(lOCreature.Index) : int.MaxValue;

					if (lOFirst == null || liOrder < liFirstOrder)
					{
						lOFirst = lONpc;
						liFirstOrder = liOrder;
					}
				}

				if (lOFirst != null)
					pOWorld.Remove(lOFirst);
			}
		}

		/// <summary>Removes every object of that id from one tile (walked over a copy of the tile's
		/// list).</summary>
		public static void RemoveObjectsInTile(IUWWorld pOWorld, int piTileX, int piTileY, int piObjectId)
		{
			UWLevel lOLevel = pOWorld.Level;

			if (lOLevel == null || lOLevel.TileData == null)
				return;

			UWTile lOTile = lOLevel.GetTile(piTileX, piTileY);

			if (lOTile == null || lOTile.ObjectsInTile == null)
				return;

			foreach (UWObject lOObject in new List<UWObject>(lOTile.ObjectsInTile))
			{
				if (lOObject != null && lOObject.ID == piObjectId)
					pOWorld.Remove(lOObject);
			}
		}
	}
}
