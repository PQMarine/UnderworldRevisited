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
	/// Engine-free; what has to touch the world goes through IUWSpecialDeathHost.
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

		/// <summary>What a special death needs from the world.</summary>
		public interface IHost
		{
			/// <summary>Starts the conversation with this creature.</summary>
			void TalkToThisCreature();

			/// <summary>Ends the fight and sets the creature standing: no attacker (byte 0x12),
			/// EndCombat, animation 0x20, frame 0. The hit points stay as they are.</summary>
			void StandDown();

			/// <summary>Byte 8, the hit points.</summary>
			void SetHitPoints(int piHitPoints);

			/// <summary>The tile state bits of byte 0x0A as the golem's marker: true the
			/// first time, false afterwards.</summary>
			bool TryMarkRewarded();

			/// <summary>EXPChange with the given tenths.</summary>
			void AwardExperience(int piExperience);

			void PlayCutscene(int piCutscene);

			/// <summary>Removes, for each of these whoami numbers, the FIRST creature that carries
			/// it in the level's list of allocated mobiles (UWLevel.GetActiveMobileOrder;
			/// see RunFunctionOnWhoAmIList_seg038_3307_D4E, which stops at the first match; per user,
			/// 2026-09-29, the original removed nine of fourteen).</summary>
			void RemoveCreaturesByWhoami(int[] piWhoamis);

			/// <summary>Removes every object of that id from one tile.</summary>
			void RemoveObjectsInTile(int piTileX, int piTileY, int piObjectId);

			/// <summary>PLAYER.DAT 0x62 bit 2 - see UWPlayerData.TalismansDestroyable.</summary>
			void SetTalismansDestroyable(bool pbValue);
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

		/// <summary>The tile of his prison, whose move triggers go with him.</summary>
		private const int TyballPrisonTileX = 0x17;

		private const int TyballPrisonTileY = 0x38;

		/// <summary>The move trigger that locked the player in.</summary>
		private const int MoveTriggerObjectId = 0x1A0;

		/// <summary>
		/// The special case of this whoami, if there is one. Returns FALSE when the creature
		/// does not die after all - only the nameless one and the golem do that, and only
		/// while dying.
		/// </summary>
		public static bool Apply(int piWhoami, Stage peStage, IHost pIHost)
		{
			if (pIHost == null)
				return true;

			if (peStage == Stage.Dying)
				return fApplyWhileDying(piWhoami, pIHost);

			fApplyWhenDead(piWhoami, pIHost);

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

		private static bool fApplyWhileDying(int piWhoami, IHost pIHost)
		{
			if (piWhoami == TalkerWhoami)
			{
				pIHost.TalkToThisCreature();
				pIHost.SetHitPoints(SurvivorHitPoints);

				return false;
			}

			if (piWhoami != MetalGolemWhoami)
				return true;

			// The reward only the first time; the marker is his own tile state.
			if (pIHost.TryMarkRewarded())
				pIHost.AwardExperience(GolemExperience);

			pIHost.StandDown();
			pIHost.TalkToThisCreature();

			return false;
		}

		private static void fApplyWhenDead(int piWhoami, IHost pIHost)
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
					pIHost.SetTalismansDestroyable(false);
					return;

				case TyballWhoami:
					fApplyTyball(pIHost);
					return;
			}
		}

		/// <summary>TyballDeath_ovr107_13D1: the cutscene, the dream bit, his allies, and the
		/// move triggers of the prison he locked the player into.</summary>
		private static void fApplyTyball(IHost pIHost)
		{
			pIHost.PlayCutscene(TyballCutscene);

			UWQuestFlags.Set(UWPlayerData.DreamQuestFlag,
				UWQuestFlags.Get(UWPlayerData.DreamQuestFlag) | TyballDreamBit);

			pIHost.RemoveCreaturesByWhoami(TyballAllies);
			pIHost.RemoveObjectsInTile(TyballPrisonTileX, TyballPrisonTileY, MoveTriggerObjectId);
		}
	}
}
