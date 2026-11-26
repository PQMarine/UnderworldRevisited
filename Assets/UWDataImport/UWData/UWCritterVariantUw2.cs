namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S CREATURE STEPS (UW2 plan phase 4), from Underworld Exhumed's uw2 critter/AI.C and PATHFIND.C.
	/// The brain is the first game's and writes its animation slots; the second game's record holds a sequence
	/// instead (0 stand, 1 walk, 2 combat stance, 3 to 5 the attacks, 6 cast or shoot, 7 die), so the two are put into
	/// each other at the record.
	/// </summary>
	public sealed class UWCritterVariantUw2 : IUWCritterVariant
	{
		private const int FirstBloodObjectId = 0xD9;

		private const int LevelsPerWorld = 8;

		private const int CorpseWorld = 7;

		private const int SequenceStand = 0;

		private const int SequenceWalk = 1;

		private const int SequenceCombat = 2;

		private const int SequenceFirstAttack = 3;

		private const int SequenceCast = 6;

		private const int SequenceDie = 7;

		/// <summary>Any creature tries the door (PATHFIND.C:1282); level 10's exception is MayOpenDoorsOn.</summary>
		public bool TriesDoor(int piAttitude)
		{
			return true;
		}

		/// <summary>The Prison Tower's second level (10): no creature opens a door (PATHFIND.C:1394).</summary>
		public const int NoDoorsLevel = 10;

		public bool MayOpenDoorsOn(int piDungeonLevel) => piDungeonLevel != NoDoorsLevel;

		/// <summary>crit_die (AI.C:1437, UW2 plan phase 5 part B): the death kind, the row's byte 8 low three bits - 0
		/// silent, 2, 3 and 4 the effects 0x22, 0x23 and 0x24, 1 and the rest effect 6. The original reads the kind of
		/// the creature last damaged (victim, set in damage_critter), which is the dying one on every blow's path.</summary>
		public int DeathCry(UWObjectClassProperties.Critter pORow)
		{
			switch (pORow.DeathSound)
			{
				case 0:
					return -1;

				case 2:
					return 0x22;

				case 3:
					return 0x23;

				case 4:
					return 0x24;

				default:
					return UWCritterRules.DeathSound;
			}
		}

		/// <summary>critter_ai's step sound (AI.C:1179, UW2 plan phase 5 part B follow-up): per user 2026-11-17 the
		/// lurkers in the gem room's moat splashed loud in ours - the first game's table gave category 3 effect 5, the
		/// second game's heavy water landing as a sample, where the second game plays 0x27 on the AdLib.</summary>
		public int FootstepSound(int piCategory, int piFrame)
		{
			switch (piCategory)
			{
				case 1: return piFrame == 1 ? UWSoundsDat.AliasOfFirst : piFrame == 3 ? UWSoundsDat.AliasOfSecond : -1;
				case 2: return 0x17;
				case 3: return 0x27;
				case 4: return 0x0E;
				case 5: return 0x0D;
				case 6: return piFrame == 1 ? 0x2F : piFrame == 3 ? 0x30 : -1;
				case 7: return piFrame == 1 || piFrame == 3 ? 0x1D : -1;
				default: return -1;
			}
		}

		public int CombatTheme => UWMusicSelectorUw2.CombatTheme;

		public int FoeHurtTheme => UWMusicSelectorUw2.FoeHurtTheme;

		public bool KeepsPlayingTheme(int piRequested, int piPlaying, bool pbSwing)
		{
			return pbSwing && UWMusicSelectorUw2.IsCombat(piPlaying);
		}

		/// <summary>uw2 critter/PATHFIND.C:151.</summary>
		public int BloodObject(int piBloodIndex)
		{
			return piBloodIndex <= 0 ? -1 : FirstBloodObjectId + piBloodIndex;
		}

		/// <summary>uw2 critter/PATHFIND.C:161, the roll is drawn all the same.</summary>
		public bool CorpseAlways(int piDungeonLevel)
		{
			return (piDungeonLevel - 1) / LevelsPerWorld == CorpseWorld;
		}

		/// <summary>death_check is asked for every creature (G6 part 2): mode 1 at the end of every death, mode 0 for one
		/// with a whoami (the brain's own test, go_into_dying_sequence).</summary>
		public bool HasSpecialDeath(int piWhoami)
		{
			return true;
		}

		/// <summary>death_check (UWSpecialDeathsUw2) through the world's schedule host; without one the death goes on.</summary>
		public bool ApplySpecialDeath(UWCreatureRef pOCreature, UWSpecialDeaths.Stage peStage, IUWWorld pOWorld, out System.Action pfAfterTalk)
		{
			pfAfterTalk = null;

			return pOWorld == null || UWSpecialDeathsUw2.Check(pOCreature, peStage == UWSpecialDeaths.Stage.Dead ? 1 : 0, pOWorld.Schedules, out pfAfterTalk);
		}

		/// <summary>
		/// SEQUENCE 6 IS A SHOT OR A CAST BY OBJ_CAST (AI.C:1136-1144: 0 fires arms[0]'s missile, else casts that
		/// spell slot). Until 2026-11-14 ours read it back as a cast always, so a fighter that began to shoot was a
		/// caster without a spell on its next update and began again - it stood in the pose and never fired (per user,
		/// test 19.9).
		/// </summary>
		public int SlotOfRecord(int piRaw, int piCastSlot)
		{
			switch (piRaw)
			{
				case SequenceStand:
					return UWCritterBrain.AnimStanding;
				case SequenceWalk:
					return UWCritterBrain.AnimWalking;
				case SequenceCombat:
					return UWCritterBrain.AnimCombatIdle;
				case SequenceCast:
					return piCastSlot != 0 ? UWCritterBrain.AnimSpell : UWCritterBrain.AnimMissile;
				case SequenceDie:
					return UWCritterBrain.AnimDying;
				default:
					return piRaw >= SequenceFirstAttack && piRaw < SequenceCast ? UWCritterBrain.AnimBash + (piRaw - SequenceFirstAttack) : UWCritterBrain.AnimStanding;
			}
		}

		/// <summary>AI.C case 15: speed 0, the gtarg one less a step, at 0 the goal is let go.</summary>
		public bool CountsDownHeldGoal => true;

		public int RecordOfSlot(int piSlot)
		{
			switch (piSlot)
			{
				case UWCritterBrain.AnimStanding:
					return SequenceStand;
				case UWCritterBrain.AnimWalking:
					return SequenceWalk;
				case UWCritterBrain.AnimCombatIdle:
				case UWCritterBrain.AnimBackOff:
					return SequenceCombat;
				case UWCritterBrain.AnimMissile:
				case UWCritterBrain.AnimSpell:
					return SequenceCast;
				case UWCritterBrain.AnimDying:
					return SequenceDie;
				default:
					return piSlot >= UWCritterBrain.AnimBash && piSlot < UWCritterBrain.AnimMissile
						? SequenceFirstAttack + ((piSlot - UWCritterBrain.AnimBash) % (SequenceCast - SequenceFirstAttack)) : SequenceStand;
			}
		}
	}
}
