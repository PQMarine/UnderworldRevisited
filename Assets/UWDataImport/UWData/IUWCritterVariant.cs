namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STEPS OF THE CREATURES' RULES WHERE THE GAMES PART (Docs/UW2-PLAN.md section 4.3, phase 0
	/// step 2, 2026-10-28), from Underworld Exhumed's list of differences (docs/UW1-UW2-DIFFERENCES.md,
	/// "AI and critters"). The numbers that differ (the path flood of 32 or 40 steps, the owners'
	/// races, the corpse's chance) are rule set entries; what only one game has (UW1's guards of
	/// Tybal, UW2's held goal 15 and its talkers turning guard) comes with that game's variant or
	/// system.
	/// </summary>
	public interface IUWCritterVariant
	{
		/// <summary>Whether a creature that bumped into a closed door and did not give up on the roll
		/// tries to open it (UW1: only a hostile one, PATHFIND.C:1154; UW2: any, except on level 10,
		/// PATHFIND.C:1282 and :1394).</summary>
		bool TriesDoor(int piAttitude);

		/// <summary>The sound a dying creature cries, or -1 for none (UW1: effect 6 when the row's byte
		/// 8 says so, AI.C:1441; UW2: one of four by the death kind, AI.C:1437).</summary>
		int DeathCry(UWObjectClassProperties.Critter pORow);

		/// <summary>A walking creature's footstep on an odd frame (1 or 3) by its row's category, or -1 for none. UW1:
		/// 1 effects 1 and 2, 2 0x17, 3 5, 4 0x0E, 5 0x0D (53431-53500); UW2 (AI.C:1179): 1 0x5A and 0x5B, 2 0x17, 3 0x27,
		/// 4 0x0E, 5 0x0D, 6 0x2F and 0x30, 7 0x1D.</summary>
		int FootstepSound(int piCategory, int piFrame);

		/// <summary>The combat theme a creature's swing at the player and a blow by the player ask for (UW1 6, UW2 3,
		/// MUSIC_COMBAT; UW2 plan phase 5 part B).</summary>
		int CombatTheme { get; }

		/// <summary>The theme of a blow by the player that leaves the creature below a quarter (hp * 64 / (vitality + 1)
		/// under 16): UW1 5, UW2 2 (MUSIC_FOE_HURT, AI.C:1517).</summary>
		int FoeHurtTheme { get; }

		/// <summary>Whether a combat request leaves the playing theme alone (the combat timer still starts anew). UW1:
		/// theme 6 while 5 to 7 plays (53136-53160); UW2: a swing while 2 to 4 plays (AI.C:1123) - a blow's request
		/// always goes to set_new_music (AI.C:1517-1519).</summary>
		bool KeepsPlayingTheme(int piRequested, int piPlaying, bool pbSwing);

		/// <summary>The brain's animation slot for what the record holds (UW1: the same number; UW2: a
		/// sequence, AI.C). piCastSlot is the record's spell slot (OBJ_CAST): the second game's sequence 6 is a
		/// shot when it is 0 and a cast otherwise (AI.C:1137).</summary>
		int SlotOfRecord(int piRaw, int piCastSlot);

		/// <summary>Whether a whoami's death has a case of its own (UWSpecialDeaths for the first game; the second
		/// game's numbers are other people, its death_check is still to come).</summary>
		bool HasSpecialDeath(int piWhoami);

		/// <summary>The special death itself (the first game's SpecialDeathCases, UWSpecialDeaths; the second game's
		/// death_check, UWSpecialDeathsUw2) for the creature behind pOCreature at this stage. False refuses a death while
		/// dying. pfAfterTalk is non-null when a conversation was opened: what follows it once it is over.</summary>
		bool ApplySpecialDeath(UWCreatureRef pOCreature, UWSpecialDeaths.Stage peStage, IUWWorld pOWorld, out System.Action pfAfterTalk);

		/// <summary>The blood of a creature's table index, -1 for none (build_corpse: 0xD8 + n in the first game,
		/// 0xD9 + n in the second).</summary>
		int BloodObject(int piBloodIndex);

		/// <summary>Whether a corpse stays whatever the roll said (the second game's eighth world).</summary>
		bool CorpseAlways(int piDungeonLevel);

		/// <summary>What the record holds for a slot of the brain.</summary>
		int RecordOfSlot(int piSlot);

		/// <summary>Whether goal 15 is the held goal that counts its target down and then lets go (UW2 AI.C case 15,
		/// Paralyze's sp_hold); the first game only sets the interval.</summary>
		bool CountsDownHeldGoal { get; }
	}
}
