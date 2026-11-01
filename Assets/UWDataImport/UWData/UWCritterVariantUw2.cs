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

		/// <summary>Any creature tries the door (PATHFIND.C:1282); level 10's exception needs the level.</summary>
		public bool TriesDoor(int piAttitude)
		{
			return true;
		}

		/// <summary>The four cries by the death kind come with the sound (phase 5).</summary>
		public int DeathCry(UWObjectClassProperties.Critter pORow)
		{
			return -1;
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

		public bool HasSpecialDeath(int piWhoami)
		{
			return false;
		}

		public int SlotOfRecord(int piRaw)
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
					return UWCritterBrain.AnimSpell;
				case SequenceDie:
					return UWCritterBrain.AnimDying;
				default:
					return piRaw >= SequenceFirstAttack && piRaw < SequenceCast ? UWCritterBrain.AnimBash + (piRaw - SequenceFirstAttack) : UWCritterBrain.AnimStanding;
			}
		}

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
