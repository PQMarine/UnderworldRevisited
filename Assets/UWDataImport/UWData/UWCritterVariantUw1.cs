namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FIRST GAME'S CREATURE STEPS (phase 0 step 2, 2026-10-28): what UWCritterBrain did at these
	/// steps until then, moved here unchanged.
	/// </summary>
	public sealed class UWCritterVariantUw1 : IUWCritterVariant
	{
		/// <summary>Only a hostile creature tries a door it bumped into; any other stands blocked.</summary>
		public bool TriesDoor(int piAttitude)
		{
			return piAttitude == UWNpc.AttitudeHostile;
		}

		public bool MayOpenDoorsOn(int piDungeonLevel) => true;

		public int FootstepSound(int piCategory, int piFrame)
		{
			switch (piCategory)
			{
				case 1: return piFrame == 1 ? 1 : 2;
				case 2: return 0x17;
				case 3: return 5;
				case 4: return 0x0E;
				case 5: return 0x0D;
				default: return -1;
			}
		}

		public int CombatTheme => UWCritterRules.CombatTheme;

		public int FoeHurtTheme => UWCritterRules.HurtCombatTheme;

		public bool KeepsPlayingTheme(int piRequested, int piPlaying, bool pbSwing)
		{
			return piRequested == UWCritterRules.CombatTheme
				&& piPlaying >= UWMusicSelector.FirstCombatTheme && piPlaying <= UWMusicSelector.LastCombatTheme;
		}

		/// <summary>The death sound when the row's byte 8 low three bits are 1.</summary>
		public int DeathCry(UWObjectClassProperties.Critter pORow)
		{
			return (pORow.RowByte(8) & 7) == 1 ? UWCritterRules.DeathSound : -1;
		}

		public int BloodObject(int piBloodIndex)
		{
			return UWCritterCombat.GetBloodObjectId(piBloodIndex);
		}

		public bool CorpseAlways(int piDungeonLevel)
		{
			return false;
		}

		public bool HasSpecialDeath(int piWhoami)
		{
			return UWSpecialDeaths.HasSpecialDeath(piWhoami);
		}

		/// <summary>SpecialDeathCases: its talks refuse the death, nothing follows them.</summary>
		public bool ApplySpecialDeath(UWCreatureRef pOCreature, UWSpecialDeaths.Stage peStage, IUWWorld pOWorld, out System.Action pfAfterTalk)
		{
			pfAfterTalk = null;

			int liWhoami = pOCreature.Record != null && pOCreature.Record.Npc != null ? pOCreature.Record.Npc.NPCwhoami : 0;

			return UWSpecialDeaths.Apply(liWhoami, peStage, pOWorld, pOCreature);
		}

		public int SlotOfRecord(int piRaw, int piCastSlot)
		{
			return piRaw;
		}

		public int RecordOfSlot(int piSlot)
		{
			return piSlot;
		}

		public bool CountsDownHeldGoal => false;
	}
}
