using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The player character's numbers and the rules that move them: health and mana,
	/// attributes and skills, experience and level, hunger, fatigue, the game clock,
	/// intoxication, the mushroom, poison, the active spells with their expiry, the
	/// twenty-one second tick everything time-related hangs on (UWPlayerTick), and how loud
	/// the player is.
	///
	/// Engine-free since 2026-09-18 (P3 of the engine separation), out of UWCharacter, which
	/// hosts one of these (IUWVitalsHost) and forwards its old API - the flasks, the paper
	/// doll, the weapon animation and the camera stay there. Since cleanup step 3.6
	/// (2026-10-17) what the vitals TELL the game are events (VitalsChanged, the shakes, the
	/// messages, the status pass, ...); IUWVitalsHost keeps only what they ASK.
	/// </summary>
	public sealed class UWPlayerVitals
	{
		/// <summary>Test values from the Inspector; minus one keeps the value from the player
		/// data. The health and mana fallbacks apply only without a loaded save game.</summary>
		public struct StartValues
		{
			public int Strength;

			public int Dexterity;

			public int Intelligence;

			public int Attack;

			public int Level;

			public int ManaSkill;

			public int CastingSkill;

			public int FallbackMaxHP;

			public int FallbackMaxMana;

			/// <summary>Hit points at start without a save game, minus one for full.</summary>
			public int FallbackCurrentHP;

			public int FallbackCurrentMana;

			/// <summary>Plays on the easy difficulty whatever the player data says - for the self-check of
			/// the damage pipeline's halving (cleanup step 4.2, 2026-10-17); false keeps the data's.</summary>
			public bool EasyDifficulty;

			public static StartValues Default => new StartValues
			{
				Strength = -1, Dexterity = -1, Intelligence = -1, Attack = -1, Level = -1,
				ManaSkill = -1, CastingSkill = -1,
				FallbackMaxHP = 30, FallbackMaxMana = 20, FallbackCurrentHP = -1, FallbackCurrentMana = -1
			};
		}

		private readonly IUWVitalsHost mIHost;

		private DataImport mOData;

		/// <param name="pIHost">What the vitals ask of the game around them (the dungeon level, the
		/// worn regeneration, the movement); may be null - then those answer as for a player who
		/// stands still nowhere, and the Lore attempt records nothing.</param>
		public UWPlayerVitals(IUWVitalsHost pIHost)
		{
			mIHost = pIHost;
		}

		/// <summary>The host, for the damage pipeline's armour and proof (null without one).</summary>
		internal IUWVitalsHost Host => mIHost;

		// ------------------------------------------------- What the vitals tell (cleanup step 3.6)
		//
		// WHY EVENTS: until 2026-10-17 these were callbacks on IUWVitalsHost, which only UWCharacter
		// implemented, so every headless user (the self-check, the sleep, the traps) had to write a
		// host with ten empty bodies. Now the game subscribes to what it shows - UWCharacter to all of
		// them - and a check subscribes to what it counts. They are raised at exactly the moments the
		// callbacks were called; nobody subscribed is what no host was.

		/// <summary>Health, mana or poison changed - the flasks show it.</summary>
		public event Action VitalsChanged;

		/// <summary>A hit or a curse was just recorded in LastDamage - the host stamps the time.</summary>
		public event Action DamageRecorded;

		/// <summary>A blow or a missile shakes the view (UWDamageRequest.Shakes): the combat shake is
		/// set to the level handed (0 to 3) times five, so level 0 ends a running one.</summary>
		public event Action<int> CombatShakeDue;

		/// <summary>A damage request went through UWDamagePipeline - once per request, after the hit
		/// points and the shake: the host flashes the view (UWDamageRequest.FlashColour, when it
		/// landed) and tells the combat music (TellsMusic). Since cleanup step 4.2 (2026-10-17);
		/// each site flashed and told the music itself until then.</summary>
		public event Action<UWDamageRequest, UWDamageOutcome> Damaged;

		/// <summary>
		/// The hit points crossed to zero - from a hit, the Void's drain or a conversation's play_hp.
		/// Raised once per crossing, after the flasks were told; what follows is UWDeathRules'
		/// (UWDeath). Since cleanup step 4.3 (2026-10-17): UWDeath polled the hit points every frame
		/// until then.
		/// </summary>
		public event Action Died;

		/// <summary>A failed sip or the waking after passing out: the view shakes for this many ticks
		/// on the large channel.</summary>
		public event Action<int> UnsteadyShakeDue;

		/// <summary>A line for the message scroll - the level-up.</summary>
		public event Action<string> MessageDue;

		/// <summary>A creature was slain: the dragon nods, and if a reward is due (true) the fanfare
		/// plays. Raised before the experience changes.</summary>
		public event Action<bool> KillRewarded;

		/// <summary>
		/// The original would run its status pass now (UnderworldExhumed PLAYDATA.C
		/// FixPlayerEquips): a spell began or ended, the mushroom began or wore off, the active
		/// spells were cleared for sleep, a mantra was chanted. That pass is where a worn cursed
		/// item strikes (class 9, backfire) - the host runs it (UWCharacter). Since 2026-10-12;
		/// until then the curse struck on every player tick (OnCurseStruck, WornCurseDice).
		/// </summary>
		public event Action StatusPassDue;

		/// <summary>
		/// The level's share of the five-minute block (duration_check, UnderworldExhumed
		/// PLAYTIME.C, every 24th player tick): the wandering monsters' one-in-four roll
		/// (UWRespawnRules.RollOpportunity) and the creatures' checkup (UWLevelExit.YearlyCheckup).
		/// Raised after hunger and intoxication and before the strength roll, in the original's
		/// order of draws. Since 2026-10-15.
		/// </summary>
		public event Action FiveMinuteBlockDue;

		/// <summary>The player tick's swim check is due (PLAYTIME.C duration_check runs sink_sink_sink
		/// after the regeneration, before the minute block): the host runs UWPlayerTerrainRules.SwimCheck,
		/// which only acts while the swim counter is above 0x50. Since cleanup step 4.4 (2026-10-17);
		/// the check ran on a clock of its own until then.</summary>
		public event Action SwimCheckDue;

		/// <summary>Lore rose (or was tried): every item in the world may be identified anew.</summary>
		public event Action IdentificationReset;

		/// <summary>What class 11 contributes to the active spells, decoded.</summary>
		public delegate void ActiveSpellsHandler(bool pbHastened, bool pbRoamingSight, bool pbTimeFrozen,
			bool pbTelekinesis);

		/// <summary>
		/// The set of active spells changed. What class 11 contributes is handed over decoded;
		/// everything that can also sit on equipment is recomputed by the host's status pass
		/// afterwards.
		/// </summary>
		public event ActiveSpellsHandler ActiveSpellsChanged;

		// ------------------------------------------------- Health and mana

		// WHOLE NUMBERS, as the original keeps them (cleanup phase 10, wave 3, 2026-10-26): the
		// current and maximum vitality and mana are bytes in PLAYER.DAT (0x35 to 0x38) and in the
		// original's player record, and every rule that moves them adds or takes whole points. They
		// were floats until then, written back through a rounding (half to even) on saving - the
		// same numbers, as nothing ever made a fraction, but a C++ mirror had to guess why.

		public int MaxHP { get; private set; }

		public int CurrentHP { get; private set; }

		public int MaxMana { get; private set; }

		public int CurrentMana { get; private set; }

		// ------------------------------------------------- Attributes

		public int Strength { get; private set; }

		public int Dexterity { get; private set; }

		public int Intelligence { get; private set; }

		/// <summary>The character's attack value, a separate field next to the skills in
		/// PLAYER.DAT.</summary>
		public int Attack { get; private set; }

		/// <summary>
		/// The defence value - the target value against which an attacking critter rolls its
		/// hit check.
		///
		/// In the reference it sits in a hidden place: its critter table supplies an
		/// opponent's defence value, and for object number 127 - the adventurer, i.e. the
		/// player character itself - it returns exactly this field instead of a table value
		/// (critterobjectdat.defence).
		/// </summary>
		public int Defence { get; private set; }

		/// <summary>PLAYER.DAT 0xB4 nonzero: creature blows and missiles do half damage to the
		/// player (UWCritterCombat.HalveOnEasy, deviation 47). Without a save game: standard.</summary>
		public bool IsEasyDifficulty { get; private set; }

		private readonly int[] miSkills = new int[UWPlayerData.SkillCount];

		/// <summary>Experience, level and skill points - the mutable part.</summary>
		private UWExperience.State mOExperience;

		public int Experience => mOExperience.Experience;

		public int Level => mOExperience.Level;

		public int SkillPoints => mOExperience.SkillPoints;

		/// <summary>All skill points ever earned - also stored in the save game.</summary>
		public int SkillPointsTotal => mOExperience.SkillPointsTotal;

		/// <summary>Whether kill rewards are written to the log (UWSettings.LogExperience).</summary>
		public bool LogExperience { get; set; }

		/// <summary>
		/// Takes attributes, skills, health, mana, the counters and the active spells from the
		/// player data (save game, created character or PLAYER.DAT), unless preset for a test.
		///
		/// From a loaded save game, health and mana come directly from the file; the fallbacks
		/// apply only when none is loaded. Until 2026-09-03 the status page therefore always
		/// showed the default values instead of the real ones (reported by the user: 33/97 and
		/// 8/15 in the original). The health and mana fallbacks do NOT come from the template
		/// in PLAYER.DAT ("GRONKEY"), because it is incomplete - it has maximum health 0 and
		/// maximum mana 188, both obviously unset (see UWPlayerData).
		/// </summary>
		public void Initialise(DataImport pOData, StartValues pOStart)
		{
			mOData = pOData;

			UWPlayerData lOPlayer = pOData != null ? pOData.InitialPlayer : null;

			Strength = pOStart.Strength >= 0 ? pOStart.Strength : (lOPlayer != null ? lOPlayer.Strength : 10);
			Dexterity = pOStart.Dexterity >= 0 ? pOStart.Dexterity : (lOPlayer != null ? lOPlayer.Dexterity : 10);
			Intelligence = pOStart.Intelligence >= 0 ? pOStart.Intelligence : (lOPlayer != null ? lOPlayer.Intelligence : 10);
			Attack = pOStart.Attack >= 0 ? pOStart.Attack : (lOPlayer != null ? lOPlayer.Attack : 10);
			Defence = lOPlayer != null ? lOPlayer.Defense : 0;
			IsEasyDifficulty = pOStart.EasyDifficulty || (lOPlayer != null && lOPlayer.IsEasyDifficulty);

			for (int liSkill = 0; liSkill < miSkills.Length; liSkill++)
				miSkills[liSkill] = lOPlayer != null ? lOPlayer.GetSkill((UWPlayerData.Skill)liSkill) : 0;

			if (pOStart.ManaSkill >= 0)
				miSkills[(int)UWPlayerData.Skill.Mana] = pOStart.ManaSkill;

			if (pOStart.CastingSkill >= 0)
				miSkills[(int)UWPlayerData.Skill.Casting] = pOStart.CastingSkill;

			mOExperience = new UWExperience.State
			{
				Experience = lOPlayer != null ? (int)lOPlayer.Experience : 0,
				Level = lOPlayer != null && lOPlayer.Level > 0 ? lOPlayer.Level : 1,
				SkillPoints = lOPlayer != null ? lOPlayer.AvailableSkillPoints : 0,
				SkillPointsTotal = lOPlayer != null ? lOPlayer.TotalSkillPoints : 0
			};

			if (pOStart.Level > 0)
				mOExperience.Level = pOStart.Level;

			// Hunger, fatigue and time of day come from the save game. Without one the
			// character starts fed, rested and on the morning of the first day.
			bool lbLoaded = lOPlayer != null && lOPlayer.IsLoaded;

			Hunger = lbLoaded ? lOPlayer.Hunger : FullHunger;
			Fatigue = lbLoaded ? lOPlayer.Fatigue : 0;
			MealHealCounter = lbLoaded ? lOPlayer.MealHealCounter : 0;
			Counter3C = lbLoaded ? lOPlayer.Counter3C : 0;

			for (int liAt = 0; liAt < myLoreByLevel.Length; liAt++)
				myLoreByLevel[liAt] = lbLoaded ? lOPlayer.LoreByLevel[liAt] : (byte)0;
			ClockValue = lbLoaded ? lOPlayer.ClockValue : 0;
			Intoxication = lbLoaded ? lOPlayer.Intoxication : 0;
			Hallucination = lbLoaded ? lOPlayer.Hallucination : 0;

			// THE POISON COMES FROM THE SAVE as well (read_player_data loads the whole record,
			// UnderworldExhumed PLAYDATA.C); until 2026-10-12 loading cured it.
			Poison = lbLoaded ? Math.Clamp(lOPlayer.Poison, 0, MaxPoison) : 0;

			MaxHP = pOStart.FallbackMaxHP;
			MaxMana = pOStart.FallbackMaxMana;
			CurrentHP = pOStart.FallbackCurrentHP < 0 ? MaxHP : Math.Min(pOStart.FallbackCurrentHP, MaxHP);
			CurrentMana = pOStart.FallbackCurrentMana < 0 ? MaxMana : Math.Min(pOStart.FallbackCurrentMana, MaxMana);

			if (lbLoaded && lOPlayer.MaxVitality > 0)
			{
				MaxHP = lOPlayer.MaxVitality;
				CurrentHP = lOPlayer.CurrentVitality;
				MaxMana = lOPlayer.MaxMana;
				CurrentMana = lOPlayer.CurrentMana;
			}

			fSeedActiveSpells(lbLoaded ? lOPlayer : null);

			// The test value beats the save game. Otherwise a loaded character would always have
			// the mana from the file, and a high spell circle could not be tested.
			if (pOStart.ManaSkill >= 0)
			{
				MaxMana = UWExperience.GetMaximumMana(GetSkill(UWPlayerData.Skill.Mana), Intelligence);
				CurrentMana = MaxMana;
			}

			fVitalsChanged();
		}

		private void fVitalsChanged()
		{
			VitalsChanged?.Invoke();
		}

		// ------------------------------------------------- Experience

		/// <summary>
		/// Records the experience for a slain creature.
		///
		/// The level is factored in: whoever is deep down gets less for weak opponents.
		/// If the level rises, the original reports "You have attained experience level N" and
		/// recomputes the maximum health.
		/// </summary>
		public void AwardKillExperience(UWObjectClassProperties.Critter pOCritter, bool pbStrong, int piDungeonLevel)
		{
			// A dragon nods (UW.EXE AwardKillEXP_seg022_1725, before the fanfare). The reward
			// is 4 * base + 2d(base), a strong individual's times (24..47) / 16, and goes to
			// EXPChange unhalved (deviation 50 adopted 2026-09-20).
			int liReward = UWExperience.GetKillReward(pOCritter, pbStrong);

			KillRewarded?.Invoke(liReward > 0);

			if (liReward <= 0)
				return;

			int liBefore = mOExperience.Experience;

			int liLevels = UWExperience.Change(ref mOExperience, liReward, piDungeonLevel);

			if (LogExperience)
				UWLog.Info(string.Format("[Experience] +{0} (now {1}), level {2}, skill points {3}.",
					mOExperience.Experience - liBefore, mOExperience.Experience,
					mOExperience.Level, mOExperience.SkillPoints));

			if (liLevels <= 0)
				return;

			fApplyLevel();

			MessageDue?.Invoke(fGetLevelUpMessage());
		}

		/// <summary>Experience from a conversation (new_player_exp) - without the halving of
		/// kill experience, as in UW.EXE EXPChange_seg037_48. Negative subtracts.</summary>
		public void ChangeExperience(int piAmount, int piDungeonLevel)
		{
			if (piAmount == 0)
				return;

			int liLevels = UWExperience.Change(ref mOExperience, piAmount, piDungeonLevel);

			if (liLevels <= 0)
				return;

			fApplyLevel();

			MessageDue?.Invoke(fGetLevelUpMessage());
		}

		/// <summary>Sets hunger, hit points, mana and poison as a conversation returns them at
		/// the end (play_hunger, play_hp, play_mana, play_poison - UW.EXE
		/// RetrieveImportedVariablesAfterConversation). Only changed values are written: the
		/// original writes them back as they are, so an unchanged value above the maximum stays
		/// (the guard kept a fraction until the vitals became whole numbers in cleanup phase 10;
		/// with whole numbers it is that plain write-back).
		///
		/// NOT CUT TO THE MAXIMUM (2026-10-26, per user, found by cleanup phase 10 wave 3): the
		/// original stores what the conversation returns into its fields as they are - hunger,
		/// play_mana and the hit points bytes, poison a 4-bit field (CONVVARS.C, player.h) - so a
		/// conversation may lift them above the maximum; until then ours clamped each to its
		/// maximum. Only the field widths remain.</summary>
		public void ApplyConversationValues(int piHunger, int piHp, int piMana, int piPoison)
		{
			if (piHunger != Hunger)
				Hunger = piHunger & 0xFF;

			bool lbWasAlive = CurrentHP > 0;

			if (piHp != CurrentHP)
				CurrentHP = piHp & 0xFF;

			if (piMana != CurrentMana)
				CurrentMana = piMana & 0xFF;

			if (piPoison != Poison)
				Poison = piPoison & 0xF;

			fVitalsChanged();

			// A conversation can set the hit points to zero (play_hp): that is a death as well.
			if (lbWasAlive && CurrentHP <= 0)
				Died?.Invoke();
		}

		/// <summary>
		/// The derived maxima, as player_compute(0) sets them (UnderworldExhumed SKILLS.C) after a
		/// level-up (advance) and a shrine mantra (mantra_advance): the maximum vitality
		/// 30 + level * strength / 5 and the maximum mana. A loaded game keeps the maximum of
		/// PLAYER.DAT 0x36 until then (PLAYDATA.C), as ours does.
		///
		/// THE CURRENT VITALITY IS NOT CUT to a lower maximum: player_compute does not touch it,
		/// and the flask draws a value above the maximum as full (113 of 90 in an original save,
		/// 2026-09-29). Until 2026-10-13 ours cut it at a level-up, and a mantra left the maximum
		/// vitality alone (reference audit, finding of the fix agents).
		/// </summary>
		private void fApplyLevel()
		{
			MaxHP = UWExperience.GetMaximumHealth(Strength, mOExperience.Level);

			fApplyMaximumMana();

			fVitalsChanged();
		}

		/// <summary>The maximum mana recomputed - in Tybal's lair into the orb's backup instead
		/// (see UWTybalOrbRules.KeepsMaxManaAside).</summary>
		private void fApplyMaximumMana()
		{
			int liMaximum = UWExperience.GetMaximumMana(GetSkill(UWPlayerData.Skill.Mana), Intelligence);

			if (mIHost != null && UWTybalOrbRules.KeepsMaxManaAside(mIHost.DungeonLevelIndex))
				UWGameFlags.OrbManaBackup = liMaximum;
			else
				MaxMana = liMaximum;
		}

		private string fGetLevelUpMessage()
		{
			try
			{
				// String block 1, number 148 - the text ends with a space and without the number,
				// the game appends it from a two-character buffer: a space and the digit below
				// level 10, two digits from 10 on - so "level  2" with two spaces, and NO FULL
				// STOP (read 2026-09-26 in the level-up routine of overlay 143 and its buffer
				// " 0\n" in UW.EXE; ours wrote "level 2." until then).
				string lsText = mOData.Strings.Blocks[1].Strings[148].TrimEnd('\r', '\n');

				return lsText + fLevelDigits(mOExperience.Level);
			}
			catch
			{
				return "You have attained experience level " + fLevelDigits(mOExperience.Level);
			}
		}

		private static string fLevelDigits(int piLevel)
		{
			return piLevel < 10 ? " " + piLevel : piLevel.ToString();
		}

		public int GetSkill(UWPlayerData.Skill peSkill)
		{
			int liIndex = (int)peSkill;

			return liIndex >= 0 && liIndex < miSkills.Length ? miSkills[liIndex] : 0;
		}

		// ------------------------------------------------- Raising skills

		/// <summary>
		/// THE NUMBERING OF SKILLS IN THE SAVE GAME starts two places before ours:
		/// zero is Attack, one Defence, and only from two on come the eighteen that
		/// our enum lists. The shrine counts the same way (see UWShrine), which is why
		/// the methods here use this numbering.
		/// </summary>
		public const int AttackSkillNumber = 0;

		public const int DefenceSkillNumber = 1;

		public const int FirstNamedSkillNumber = 2;

		public const int SkillNumberCount = FirstNamedSkillNumber + UWPlayerData.SkillCount;

		/// <summary>Highest skill value - it does not go beyond this.</summary>
		public const int MaxSkillValue = UWPlayerData.MaxSkillValue;

		public int GetSkillByNumber(int piSkillNumber)
		{
			if (piSkillNumber == AttackSkillNumber)
				return Attack;

			if (piSkillNumber == DefenceSkillNumber)
				return Defence;

			int liIndex = piSkillNumber - FirstNamedSkillNumber;

			return liIndex >= 0 && liIndex < miSkills.Length ? miSkills[liIndex] : 0;
		}

		/// <summary>Sets a skill to a value - for x_skills in conversations (see
		/// UWConversationSession). Between zero and the maximum.</summary>
		public void SetSkillByNumber(int piSkillNumber, int piValue)
		{
			if (piSkillNumber < 0 || piSkillNumber >= SkillNumberCount)
				return;

			fSetSkillByNumber(piSkillNumber, Math.Clamp(piValue, 0, MaxSkillValue));
		}

		private void fSetSkillByNumber(int piSkillNumber, int piValue)
		{
			if (piSkillNumber == AttackSkillNumber)
			{
				Attack = piValue;

				return;
			}

			if (piSkillNumber == DefenceSkillNumber)
			{
				Defence = piValue;

				return;
			}

			int liIndex = piSkillNumber - FirstNamedSkillNumber;

			if (liIndex >= 0 && liIndex < miSkills.Length)
				miSkills[liIndex] = piValue;
		}

		/// <summary>
		/// Which attribute governs a skill: Strength the combat skills, Intelligence
		/// the three magical ones, Dexterity the rest (reference:
		/// playerdatskills.GetGoverningAttribute).
		/// </summary>
		private static int fGetGoverningAttribute(int piSkillNumber)
		{
			if (piSkillNumber < 7)
				return 0;

			return piSkillNumber < 10 ? 2 : 1;
		}

		private int fGetAttributeValue(int piAttribute)
		{
			switch (piAttribute)
			{
				case 1: return Dexterity;
				case 2: return Intelligence;
				default: return Strength;
			}
		}

		/// <summary>Range of the bonus roll, per attribute - taken from the reference.
		/// </summary>
		internal static readonly int[] msSkillRollRange = { 0x19, 0x28, 0x0A };

		/// <summary>
		/// Raises a skill by one point, often by more (reference:
		/// playerdatskills.IncreaseSkill). Returns false if nothing more is possible.
		///
		/// TWO CAPS: twice the governing attribute, and thirty as a hard limit.
		///
		/// TWO BONUSES on top when the skill lags far behind its attribute:
		/// below half of its VALUE there is one free point, and below the value another one with
		/// a probability that grows with the gap. UW.EXE SkillGain_ovr143_271 compares against
		/// the attribute score (di = PlayerCritterData[5 + attribute]) in both checks. The
		/// reference compares against the attribute NUMBER (1 or 2) instead, so its free point
		/// never applied; we copied that until 2026-09-14. After the bonuses UW.EXE clamps the
		/// skill to 30 again (ovr143_384).
		///
		/// THE FREE POINT NOT FOR STRENGTH: UW.EXE checks "attribute number not zero" for it, and
		/// zero is Strength's, so the combat skills never get it. The CHANCE POINT stands outside
		/// that check and comes for every skill, Strength's with the range 25 (UnderworldExhumed
		/// SKILLS.C get_skill, 2026-10-11). Until then both sat inside the check, as the reference
		/// has them, and the combat skills never got the chance point either.
		/// </summary>
		public bool TryIncreaseSkill(int piSkillNumber)
		{
			if (piSkillNumber < 0 || piSkillNumber >= SkillNumberCount)
				return false;

			int liAttribute = fGetGoverningAttribute(piSkillNumber);
			int liAttributeValue = fGetAttributeValue(liAttribute);
			int liValue = GetSkillByNumber(piSkillNumber);

			if ((liAttributeValue << 1) < liValue || liValue >= MaxSkillValue)
			{
				fAfterLoreAttempt(piSkillNumber);

				return false;
			}

			liValue++;

			if (liAttribute != 0 && liAttributeValue / 2 > liValue)
				liValue++;

			// The roll's range is the rule set's (experience.skillRaise.chanceRange, cleanup phase 10).
			if (liValue < liAttributeValue
				&& UWRandom.Next(UWRules.Current.Experience.SkillRaise.ChanceRange[liAttribute]) < liAttributeValue - liValue)
				liValue++;

			if (liValue > MaxSkillValue)
				liValue = MaxSkillValue;

			fSetSkillByNumber(piSkillNumber, liValue);

			// NO MAXIMUM IS RECOMPUTED HERE (get_skill does not call player_compute): a trainer's
			// raise leaves the mana flask's maximum until the next level-up or shrine mantra
			// (RecomputeMaxima). Until 2026-10-12 every raise recomputed it at once.
			fVitalsChanged();

			fAfterLoreAttempt(piSkillNumber);

			return true;
		}

		/// <summary>
		/// BETTER LORE MAKES THE WORLD UNIDENTIFIED AGAIN. Without this it achieved nothing at all:
		/// the check is rolled only once per item (see UWLoreCheck).
		///
		/// ON EVERY ATTEMPT AT LORE, raised or not: get_skill clears the lore tries for SKILL_LORE
		/// whatever its result (UnderworldExhumed SKILLS.C). Until 2026-10-12 ours reset only after
		/// a successful raise.
		/// </summary>
		private void fAfterLoreAttempt(int piSkillNumber)
		{
			if (piSkillNumber != LoreSkillNumber || mIHost == null)
				return;

			IdentificationReset?.Invoke();

			// And the Lore skill is noted for this level (UWPlayerData.LoreByLevel): get_skill
			// writes player->lore[PlayerLevel] up to level 8.
			int liLevel = mIHost.DungeonLevelIndex + 1;

			if (liLevel >= 0 && liLevel < myLoreByLevel.Length)
				myLoreByLevel[liLevel] = (byte)GetSkill(UWPlayerData.Skill.Lore);
		}

		private readonly byte[] myLoreByLevel = new byte[UWPlayerData.LoreByLevelCount];

		/// <summary>PLAYER.DAT 0xC2 on, by level number - see UWPlayerData.LoreByLevel. A copy.</summary>
		public byte[] LoreByLevel => (byte[])myLoreByLevel.Clone();

		/// <summary>Lore in the save game numbering - see AttackSkillNumber.
		/// </summary>
		private const int LoreSkillNumber = FirstNamedSkillNumber + (int)UWPlayerData.Skill.Lore;

		/// <summary>Mana in the save game numbering - see AttackSkillNumber.
		/// </summary>
		public const int ManaSkillNumber = FirstNamedSkillNumber + (int)UWPlayerData.Skill.Mana;

		/// <summary>Spends one skill point.</summary>
		public void SpendSkillPoint()
		{
			if (mOExperience.SkillPoints > 0)
				mOExperience.SkillPoints--;
		}

		/// <summary>Sets the maximum and the current mana directly - for Tybal's orb, which
		/// drains both in its lair and gives them back (UWTybalOrbRules). The current value is
		/// kept at or below the maximum.</summary>
		public void SetMana(int piMaximum, int piCurrent)
		{
			MaxMana = Math.Max(0, piMaximum);
			CurrentMana = Math.Clamp(piCurrent, 0, MaxMana);

			fVitalsChanged();
		}

		/// <summary>The maximum vitality and mana, as player_compute(0) sets them after a shrine
		/// mantra (UnderworldExhumed SKILLS.C mantra_advance) - the mana in Tybal's lair into the
		/// orb's backup. The current values stay as they are (see fApplyLevel).</summary>
		public void RecomputeMaxima()
		{
			fApplyLevel();
		}

		// ------------------------------------------------- Hunger, fatigue, clock, drink

		/// <summary>A full belly. Zero is starving.</summary>
		public const int FullHunger = 0xFF;

		/// <summary>How fed the character is, 0 to 255 - HIGH means fed. Drops on the
		/// five-minute tick, see fTickFiveMinutes.</summary>
		public int Hunger { get; private set; }

		/// <summary>How tired the character is, 0 to 255 - HIGH means tired. Rises on the
		/// five-minute tick.</summary>
		public int Fatigue { get; private set; }

		/// <summary>Five-minute blocks since the last meal, 0 to 255 (PLAYER.DAT 0x3B) - see
		/// ChangeHunger.</summary>
		public int MealHealCounter { get; private set; }

		/// <summary>PLAYER.DAT 0x3C, counted like fatigue and read nowhere we know of.</summary>
		public int Counter3C { get; private set; }

		/// <summary>The value of the game clock, see UWGameClock.</summary>
		public int ClockValue { get; private set; }

		/// <summary>How drunk the character is, 0 to 63. Drops by one in the five-minute block
		/// (fTickFiveMinutes; this said "the minute tick" until 2026-10-12) and by 32 or 16 during
		/// sleep - see Drink and SleepIntoxicationRelief.</summary>
		public int Intoxication { get; private set; }

		/// <summary>Maximum intoxication - six bits in the save game.</summary>
		public const int MaxIntoxication = 0x3F;

		/// <summary>What a sip has caused - see Drink.</summary>
		public enum DrinkResult
		{
			Nothing,
			Refreshing,
			Unsteady,
			PassedOut
		}

		/// <summary>On a critical success the sip even does some good.</summary>
		internal const int DrinkHealAmount = 2;

		/// <summary>Base duration of the shaking after a failed sip.</summary>
		internal const int DrinkShakeBase = 0xA;

		/// <summary>
		/// Drinks something intoxicating and reports what came of it.
		///
		/// The reference's sequence (food.DrinkLiquid): intoxication rises by the magnitude of
		/// the negative nutrition value - port eight; water (-1) is no drink, the caller leaves it
		/// out as the original does (USEITEMS.C, UWItemUse) -, and then STRENGTH is checked
		/// against the NEW intoxication. The drunker, the harder the check:
		///
		///   critical success  a message, plus two points of health
		///   success           nothing
		///   failure           the view shakes
		///   critical failure  the character passes out - sleep with negative duration - and
		///                     the view shakes afterwards
		///
		/// The shake lasts intoxication / 6 after a failure, and 10 more after waking from passing out, on the large channel (see
		/// UWScreenShake).
		/// </summary>
		public DrinkResult Drink(int piIntoxication)
		{
			Intoxication = Math.Clamp(Intoxication + piIntoxication, 0, MaxIntoxication);

			int liCheck = (int)UWSkillCheck.Check(Strength, Intoxication);

			if (liCheck >= (int)UWSkillCheck.ResultEnum.CriticalSuccess)
			{
				CurrentHP = Math.Min(MaxHP, CurrentHP + UWRules.Current.Vitals.Drink.HealAmount);

				fVitalsChanged();

				return DrinkResult.Refreshing;
			}

			if (liCheck > 0)
				return DrinkResult.Nothing;

			// Passing out shakes only AFTER the sleep, with ten more (ShakeAfterPassingOut); a
			// plain failure shakes intoxication / 6 and no base (the eating routine, labels 12EA and
			// 12C0, read 2026-09-24 - until then both got the base, before the sleep).
			if (liCheck < 0)
				return DrinkResult.PassedOut;

			UnsteadyShakeDue?.Invoke(Intoxication / 6);

			return DrinkResult.Unsteady;
		}

		/// <summary>Lowers intoxication by the given amount, never below zero. Sleep passes
		/// SleepIntoxicationRelief or InterruptedSleepIntoxicationRelief.
		/// </summary>
		public void ReduceIntoxication(int piAmount)
		{
			Intoxication = Math.Max(0, Intoxication - piAmount);
		}

		/// <summary>
		/// How long the mushroom effect still lasts, 0 to 3. Drops by one on every tick (see
		/// RunGameTick; this said "the minute tick" until 2026-10-12).
		///
		/// WHAT IS SEEN while it runs is UWHallucinationState - one of three effects, rolled once
		/// (the palette one is built, see UWPaletteCommon). The
		/// counter still runs: it is stored in the save game, and the visuals can be hooked up
		/// to it later without anything else needing to change. The mushroom raises it by one,
		/// the Hallucination spell of some potions and a scroll sets it to the maximum.
		/// </summary>
		public int Hallucination { get; private set; }

		/// <summary>Maximum value of the mushroom effect - two bits in the save game.</summary>
		public const int MaxHallucination = 3;

		/// <summary>The mushroom eaten (USEITEMS.C UseFood, which then runs FixPlayerEquips).</summary>
		public void AddHallucination()
		{
			Hallucination = Math.Min(MaxHallucination, Hallucination + 1);

			RequestStatusPass();
		}

		/// <summary>Item spell 13/5 writes 3 into the two bits - see UWItemSpellRules (SPELLS.C
		/// do_spell, then FixPlayerEquips).</summary>
		public void StartHallucination()
		{
			Hallucination = MaxHallucination;

			RequestStatusPass();
		}

		/// <summary>Sleeping clears the two bits - see UWSleepRules.</summary>
		public void EndHallucination()
		{
			Hallucination = 0;
		}

		/// <summary>Adds mana as it is, capped at the maximum - the negative branch of the
		/// original's change routine (ManaChange_seg038_2B6), which the mushroom uses.</summary>
		public void AddMana(int piAmount)
		{
			if (piAmount <= 0)
				return;

			CurrentMana = Math.Min(MaxMana, CurrentMana + piAmount);

			fVitalsChanged();
		}

		/// <summary>The toadstool (see UWItemUse.fEatLikeOriginal, after its line): a poison below four becomes
		/// four, one below thirteen rises by two, a stronger one stays.</summary>
		public void PoisonFromToadstool()
		{
			// The two values are the rule set's (vitals.toadstool.*, cleanup phase 10).
			UWRulesVitals.ToadstoolRules lORules = UWRules.Current.Vitals.Toadstool;

			if (Poison < lORules.Poison)
				Poison = lORules.Poison;
			else if (Poison < lORules.PoisonLimit)
				Poison += 2;
			else
				return;

			fVitalsChanged();
		}

		internal const int ToadstoolPoison = 4;

		internal const int ToadstoolPoisonLimit = 0xD;

		/// <summary>The shaking after waking from a drink: 10 plus intoxication / 6.</summary>
		public void ShakeAfterPassingOut()
		{
			UnsteadyShakeDue?.Invoke(UWRules.Current.Vitals.Drink.ShakeBase + (Intoxication / 6));
		}

		/// <summary>How much intoxication a night removes: 32 after a full night, 16 after an
		/// interrupted one, cleared when less is left (Sleep_ovr143_D1F, labels 102F and EB3, read
		/// 2026-09-27). Ours took the reference's 16 after a full night and nothing after an
		/// interrupted one until then.</summary>
		public const int SleepIntoxicationRelief = 0x20;

		public const int InterruptedSleepIntoxicationRelief = 0x10;

		// ------------------------------------------------- Damage

		public void RestoreVitality()
		{
			CurrentHP = MaxHP;
			fVitalsChanged();
		}

		/// <summary>
		/// The last hit - amount and type. FOR DEBUGGING ONLY (F1): from the hit points alone
		/// you cannot see WHERE the damage came from; with the fire elemental exactly that was
		/// the obstacle (per user, 2026-09-10). The host stamps the time (OnDamageRecorded).
		/// </summary>
		public int LastDamage { get; private set; }

		public int LastDamageType { get; private set; }

		/// <summary>Damage summed since the last TakeDamageThisTick - UW.EXE keeps it in byte 11h of
		/// the player object and clears it in PlayerUpdateTick after the dragon check.</summary>
		private int miDamageThisTick;

		public int TakeDamageThisTick()
		{
			int liDamage = miDamageThisTick;
			miDamageThisTick = 0;
			return liDamage;
		}

		// WHAT THE DAMAGE PIPELINE DOES TO THE NUMBERS (cleanup step 4.2, 2026-10-17): every damage
		// to the player is a UWDamageRequest through UWDamagePipeline.Apply, which decides the
		// armour, the halving and the proof and then lands the result through one of the three
		// helpers below. Until then they were the public ApplyDamage, ApplyCurse/ApplyBackfire and
		// DrainVitality, each site choosing one.

		/// <summary>
		/// Damage to the player, with its type, after the proof (UWDamagePipeline): subtracts hit
		/// points and reports whether they crossed to zero with it. This class deliberately does not
		/// decide what happens on death.
		///
		/// THE TYPE DECIDES WHETHER IT ARRIVES AT ALL. The player has no entry in the
		/// resistance table (see UWDamageTypes) - what he wards off comes solely from
		/// the class 3 spells and enchantments recorded in the host's DamageTypeProof.
		/// Flameproof absorbs lava and fireballs completely, Poison Resistance the poison breath,
		/// magic protection rolls dice.
		///
		/// It is ALL OR NOTHING. uw1 knows no reduction - either the blow lands
		/// in full or not at all.
		///
		/// Without a type it stays as before: no resistance, full damage. That is the
		/// right fallback for everything that also goes through unfiltered in the original -
		/// starvation, drowning, the sleeper's water and lava.
		///
		/// NO SHAKE HERE: only a blow or a missile shakes the view (UWDamageRequest.Shakes).
		/// Until 2026-10-12 every damage did - poison, falls, lava, traps, drowning.
		/// </summary>
		internal bool TakeHit(int piDamage, int piDamageType)
		{
			LastDamage = piDamage;
			LastDamageType = piDamageType;
			miDamageThisTick += piDamage;

			DamageRecorded?.Invoke();

			return fLowerHitPoints(CurrentHP - piDamage);
		}

		/// <summary>
		/// What a curse and a backfire share (one helper since 2026-10-11, before that the same
		/// lines twice): the hit is recorded without a damage type - no resistance, but F1 shows
		/// it - and taken straight off the hit points, which stay at piFloor at the least. The dice
		/// are rolled by the caller (UWDamagePipeline), so the order of the draws is unchanged.
		///
		/// NO RESISTANCE AND NO SHAKING: the value comes straight off the hit points, without
		/// the damage types. The hit is still recorded so that it shows up in the F1 display.
		/// </summary>
		internal void TakeBackfire(int piDamage, int piFloor)
		{
			LastDamage = piDamage;
			LastDamageType = UWDamageTypes.None;

			DamageRecorded?.Invoke();

			fLowerHitPoints(CurrentHP - piDamage < piFloor ? piFloor : CurrentHP - piDamage);
		}

		/// <summary>
		/// Subtracts hit points without it being a hit - no shaking, no damage type, no record.
		/// For the Ethereal Void (UWVoidEffects), which sets the points directly in UW.EXE.
		/// Never goes below zero.
		/// </summary>
		internal bool TakeDrain(int piAmount)
		{
			return fLowerHitPoints(Math.Max(0, CurrentHP - piAmount));
		}

		/// <summary>The one place the damage sets the hit points: never below zero, the flasks told,
		/// and Died raised when they cross to zero. Returns whether they did.</summary>
		private bool fLowerHitPoints(int piHitPoints)
		{
			bool lbWasAlive = CurrentHP > 0;

			CurrentHP = piHitPoints < 0 ? 0 : piHitPoints;

			fVitalsChanged();

			if (!lbWasAlive || CurrentHP > 0)
				return false;

			Died?.Invoke();

			return true;
		}

		/// <summary>The pipeline's combat shake (CombatShakeDue).</summary>
		internal void TellCombatShake(int piLevel)
		{
			CombatShakeDue?.Invoke(piLevel);
		}

		/// <summary>The pipeline's word after each request (Damaged).</summary>
		internal void TellDamaged(in UWDamageRequest pORequest, in UWDamageOutcome pOOutcome)
		{
			Damaged?.Invoke(pORequest, pOOutcome);
		}

		/// <summary>Subtracts mana and updates the flask. Never below zero.</summary>
		public void SpendMana(int piAmount)
		{
			CurrentMana -= piAmount;

			if (CurrentMana < 0)
				CurrentMana = 0;

			fVitalsChanged();
		}

		/// <summary>Counterpart to the damage (UWDamagePipeline), for instance for healing potions.</summary>
		public void Heal(int piAmount)
		{
			if (piAmount <= 0)
				return;

			CurrentHP += piAmount;

			if (CurrentHP > MaxHP)
				CurrentHP = MaxHP;

			fVitalsChanged();
		}

		// ------------------------------------------------- Poison

		/// <summary>
		/// How strongly the character is poisoned, zero to fifteen.
		///
		/// The original keeps a single value: it is both the damage due the
		/// next time and the counter of how often it will still happen. With each
		/// bout it drops by one - a poisoning of five therefore hurts 5, then 4, then 3
		/// and is over after five bouts.
		/// </summary>
		public int Poison { get; private set; }

		/// <summary>
		/// Poisons the character. A STRONGER poisoning replaces the current one, a
		/// weaker one changes nothing - that is what the original does instead of adding.
		///
		/// THE FIELD HAS FOUR BITS (player.h, poison): whack_thing compares the full strength and
		/// stores it into the bit field, so a strength above 15 keeps its low four bits. Until
		/// 2026-10-12 ours clamped it to 15; no shipped trap or potion is that strong.
		/// </summary>
		public void ApplyPoison(int piStrength)
		{
			if (piStrength <= Poison)
				return;

			Poison = piStrength & MaxPoison;

			fVitalsChanged();
		}

		/// <summary>Highest value a poisoning can take.</summary>
		private const int MaxPoison = 0xF;

		/// <summary>Three ticks are the original's minute. Poison and mana regeneration hang
		/// on this - that is how the reference counts in its player loop. The length comes from
		/// UWPlayerTick, so three ticks are sixty-three real seconds.</summary>
		private const int MinuteTicks = UWPlayerTick.MinuteTicks;

		/// <summary>
		/// The poison takes effect: damage equal to the current value, and the value drops by one.
		///
		/// ON THE GLOBAL MINUTE TICK, the same one the mana roll uses (UnderworldExhumed PLAYTIME.C
		/// duration_check: plyregen[1] % 3, poison first), so the first damage comes one to three
		/// ticks after the poisoning. Until 2026-10-12 the poison counted its own three ticks from
		/// the poisoning and always hurt first after exactly three.
		/// </summary>
		private void fTickPoison(int piDamageTypeProof)
		{
			if (Poison <= 0)
				return;

			// POISON DAMAGE IS POISON: the reference gives type 0x10 for it (playerdatloop, in the
			// minute block). Poison Resistance thereby nullifies the poison damage - the
			// poison itself still runs its course. Through the damage pipeline since cleanup step 4.2
			// (2026-10-17), against the proof the tick was handed.
			UWDamagePipeline.Apply(this, UWDamageRequest.PoisonTick(Poison, piDamageTypeProof));

			Poison--;

			fVitalsChanged();
		}

		public void CurePoison()
		{
			Poison = 0;
			fVitalsChanged();
		}

		// ------------------------------------------------- Active spells

		public const int MaxActiveSpells = UWPlayerData.MaxActiveSpells;

		/// <summary>
		/// The motion spells as a bit field, one bit per subclass: bit zero is Leap,
		/// one Slow Fall, two Levitate, three Water Walk, four Fly. The reference
		/// sets it the same way, namely as one shifted left by subclass minus one.
		/// </summary>
		public const int LeapBit = 0x01;

		public const int SlowFallBit = 0x02;

		public const int LevitateBit = 0x04;

		public const int WaterWalkBit = 0x08;

		public const int FlyBit = 0x10;

		/// <summary>Counts up on every change to the active spells. The UI ties its icons
		/// to it, so that expiry also reaches it.</summary>
		public int ActiveSpellVersion { get; private set; }

		/// <summary>Is Freeze Time currently active? Then no critter moves any more.</summary>
		public bool TimeIsFrozen { get; private set; }

		/// <summary>Is Telekinesis currently active? Then the hand reaches further - see
		/// Interaction.GetUseRangeFor.</summary>
		public bool HasTelekinesis { get; private set; }

		private readonly List<UWActiveSpellEffect> mOActiveSpells = new List<UWActiveSpellEffect>();

		public IReadOnlyList<UWActiveSpellEffect> ActiveSpells
		{
			get { return mOActiveSpells; }
		}

		/// <summary>
		/// Takes over the active spells from the save game (see
		/// UWPlayerData.ActiveSpells for the layout of the three slots).
		///
		/// Until 2026-09-08 a loaded character always started without active spells. In SAVE1,
		/// however, one is running: class 0, level 3, stability 28 - a light spell that was then
		/// missing with us. It now keeps running and keeps counting down, since the
		/// stability is stored in the save game.
		///
		/// TryAddActiveSpell takes at most three; the save game holds no more anyway.
		/// </summary>
		private void fSeedActiveSpells(UWPlayerData pOSaved)
		{
			mOActiveSpells.Clear();

			if (pOSaved != null && pOSaved.IsLoaded)
			{
				foreach (UWPlayerData.ActiveSpell lOSpell in pOSaved.ActiveSpells)
					TryAddActiveSpell(lOSpell.MajorClass, lOSpell.MinorClass, lOSpell.Stability);
			}

			// Compute once even without a save game: otherwise a new start after a
			// loaded game would keep its light and speed (see EndAllSpells, same bug).
			fApplyActiveSpells();
		}

		/// <summary>Adds a spell to the list. Returns false if three are already running -
		/// then nothing further happens in the original.</summary>
		public bool TryAddActiveSpell(int piMajorClass, int piMinorClass, int piStability)
		{
			if (mOActiveSpells.Count >= MaxActiveSpells)
				return false;

			mOActiveSpells.Add(new UWActiveSpellEffect
			{
				MajorClass = piMajorClass,
				MinorClass = piMinorClass,
				Stability = piStability
			});

			fApplyActiveSpells();

			// set_curmagic ends with FixPlayerEquips: a cast spell is a status pass.
			RequestStatusPass();

			return true;
		}

		/// <summary>Ends a specific active spell. Used by the detached view: if the camera is
		/// removed from outside - on a level change, for instance -, the spell should not
		/// remain in the icon strip without it.</summary>
		public bool EndSpellEffect(int piMajorClass, int piMinorClass)
		{
			for (int liAt = 0; liAt < mOActiveSpells.Count; liAt++)
			{
				if (mOActiveSpells[liAt].MajorClass != piMajorClass
					|| mOActiveSpells[liAt].MinorClass != piMinorClass)
					continue;

				CancelActiveSpell(liAt);

				return true;
			}

			return false;
		}

		public void CancelActiveSpell(int piIndex)
		{
			if (piIndex < 0 || piIndex >= mOActiveSpells.Count)
				return;

			// THE ICON CLICK IS THE TICK'S DISPEL (try_clear -> dispel_spell, UnderworldExhumed RUNES.C,
			// PLAYTIME.C): Levitate and Fly become Slow Fall WITH STABILITY ONE, "nearly done", ending
			// at the next tick. Until 2026-10-14 the click kept what was left of their stability (per
			// user on the original: the Slow Fall of a cancelled fresh Fly is "nearly done", ours kept
			// Fly's state - audit group B3); one rule for both now (fDispel).
			fDispel(piIndex);

			fApplyActiveSpells();

			// try_clear runs FixPlayerEquips after the dispel.
			RequestStatusPass();
		}

		/// <summary>All active spells end - they wear off during sleep.</summary>
		public void EndAllSpells()
		{
			mOActiveSpells.Clear();

			// Clearing alone is not enough: speed, light and the motion modes hang on
			// fApplyActiveSpells and would otherwise remain, although no spell is running any more.
			fApplyActiveSpells();

			// Sleep and the resurrection run FixPlayerEquips (player_sleep, do_resurrect).
			RequestStatusPass();
		}

		/// <summary>
		/// The original would run its status pass, FixPlayerEquips, now - the host does it,
		/// and that is where a worn curse strikes (the StatusPassDue event). Called by the
		/// rules that call FixPlayerEquips in the original: a spell begins or ends, the mushroom
		/// begins or wears off, sleep, a mantra (UWShrineRules).
		/// </summary>
		public void RequestStatusPass()
		{
			StatusPassDue?.Invoke();
		}

		/// <summary>
		/// Applies the effect of the active spells.
		///
		/// ONLY major class 11 IS DECODED HERE - Freeze Time, Roaming Sight, Haste,
		/// Telekinesis -, because those exist exclusively as cast spells.
		///
		/// Everything that can also be attached to a piece of equipment - light (0), motion (1),
		/// resistance (2), bonuses (3), protection (0xC) -, is computed by the host's status pass
		/// together with the worn equipment, just as the reference sends both through the same
		/// case distinction.
		/// </summary>
		private void fApplyActiveSpells()
		{
			ActiveSpellVersion++;

			bool lbHastened = false;
			bool lbRoaming = false;

			TimeIsFrozen = false;
			HasTelekinesis = false;

			foreach (UWActiveSpellEffect lOSpell in mOActiveSpells)
			{
				if (lOSpell.MajorClass != UWMiscSpellRules.MajorClass)
					continue;

				// THE SUBCLASSES OF CLASS 11 ARE DIFFERENT HERE than when casting. The
				// reference notes the change at every single place ("Note the change
				// in mapping here") - see UWMiscSpellRules for the mapping.
				if (lOSpell.MinorClass == UWMiscSpellRules.SpeedEffectMinor)
					lbHastened = true;

				if (lOSpell.MinorClass == UWMiscSpellRules.FreezeTimeEffectMinor)
					TimeIsFrozen = true;

				if (lOSpell.MinorClass == UWMiscSpellRules.TelekinesisEffectMinor)
					HasTelekinesis = true;

				if (lOSpell.MinorClass == UWMiscSpellRules.RoamingSightEffectMinor)
					lbRoaming = true;
			}

			ActiveSpellsChanged?.Invoke(lbHastened, lbRoaming, TimeIsFrozen, HasTelekinesis);
		}

		/// <summary>
		/// Counts down the duration of the spells (UnderworldExhumed PLAYTIME.C duration_check).
		///
		/// A spell whose stability IS one when the tick comes ends (fDispel), any other loses a
		/// step - so a spell of stability S lasts S ticks, and it holds two and one for a whole
		/// tick each, which is when its icon says "is nearly done" (UWHudRunes). Levitate and
		/// Fly do not end but turn into Slow Fall for one more tick, so you do not plummet out
		/// of the air.
		///
		/// UNTIL 2026-10-12 the step came first and the spell ended at one or below, S - 1 ticks,
		/// and "is nearly done" practically never showed.
		///
		/// The slots are walked upwards as the original walks them: when a spell ends the last
		/// one moves into its slot and is looked at in the same pass.
		/// </summary>
		private bool fTickActiveSpells()
		{
			if (mOActiveSpells.Count == 0)
				return false;

			bool lbChanged = false;

			for (int liSpell = 0; liSpell < mOActiveSpells.Count; liSpell++)
			{
				UWActiveSpellEffect lOSpell = mOActiveSpells[liSpell];

				if (lOSpell.Stability == 1)
				{
					if (fDispel(liSpell))
						liSpell--;

					lbChanged = true;

					continue;
				}

				// A stored zero wraps to 255, as the high byte of the original's word does.
				lOSpell.Stability = (lOSpell.Stability - 1) & 0xFF;
				mOActiveSpells[liSpell] = lOSpell;
			}

			if (lbChanged)
				fApplyActiveSpells();

			return lbChanged;
		}

		/// <summary>
		/// Ends the active spell in slot piIndex (UnderworldExhumed PLAYTIME.C dispel_spell, used by
		/// the tick and by the icon click). Levitate and Fly become Slow Fall with stability one
		/// and keep their slot; any other spell is removed and the LAST spell moves into the freed
		/// slot (until 2026-10-12 ours shifted the rest down, so with three spells the icons came
		/// out B, C where the original shows C, B). Returns whether the slot now holds another
		/// spell that still has to be looked at.
		/// </summary>
		private bool fDispel(int piIndex)
		{
			UWActiveSpellEffect lOSpell = mOActiveSpells[piIndex];

			if (lOSpell.MajorClass == MotionMajor
				&& ((lOSpell.MinorClass & 0x3F) == LevitateMinor || (lOSpell.MinorClass & 0x3F) == FlyMinor))
			{
				lOSpell.MinorClass = SlowFallMinor;
				lOSpell.Stability = 1;
				mOActiveSpells[piIndex] = lOSpell;

				return false;
			}

			fRemoveSlot(piIndex);

			return piIndex < mOActiveSpells.Count;
		}

		/// <summary>Removes a slot the original's way: the last spell moves into it.</summary>
		private void fRemoveSlot(int piIndex)
		{
			int liLast = mOActiveSpells.Count - 1;

			if (piIndex < liLast)
				mOActiveSpells[piIndex] = mOActiveSpells[liLast];

			mOActiveSpells.RemoveAt(liLast);
		}

		/// <summary>Major class and subclasses of the motion spells.</summary>
		private const int MotionMajor = 1;

		private const int SlowFallMinor = 2;

		private const int LevitateMinor = 3;

		private const int FlyMinor = 5;

		// ------------------------------------------------- The game tick

		/// <summary>Every thirtieth tick is the original's five minutes - ten and a half real
		/// minutes, on that label see UWRespawnRules.</summary>
		private const int FiveMinuteTicks = UWPlayerTick.FiveMinuteTicks;

		private int miTickCounter;

		/// <summary>
		/// Where the last tick stood in the five-minute cycle, 1 to 24 - the original's own
		/// counter (dseg_5c99_3F9 in PlayerUpdates_seg028_2985_13D), which it resets after the
		/// five-minute block. Ours keeps counting, which is the same for the divisors 3 and 24,
		/// but not for the light sources' rates of 10 or 12 (UWInventoryModel.BurnTick) - they
		/// need the value as the original has it.
		/// </summary>
		public int TickInCycle => miTickCounter <= 0 ? 0 : ((miTickCounter - 1) % FiveMinuteTicks) + 1;

		/// <summary>
		/// One player tick - the tick on which everything time-related hangs in the original,
		/// in the same staggering (playerdatloop.PlayerTimedLoop): every tick twenty-one
		/// seconds (UWPlayerTick), every third the minute, every twenty-fourth the five minutes. The host's clock
		/// (UWTickClock) decides how many of these a frame brings.
		/// </summary>
		/// <param name="piClockUnitsPerTick">How far the game clock advances per tick.</param>
		/// <param name="piDamageTypeProof">The immunities of the moment, for the poison.</param>
		public void RunGameTick(int piClockUnitsPerTick, int piDamageTypeProof)
		{
			ClockValue += piClockUnitsPerTick;

			miTickCounter++;

			bool lbStatusChanged = fTickActiveSpells();

			// THE MUSHROOM WEARS OFF ON EVERY TICK, before any counter is looked at
			// (PlayerUpdates_seg028_2985_13D label 1CD). It sat on the minute tick until
			// 2026-09-22, three times too slow. Wearing off counts as a change (duration_check).
			if (Hallucination > 0 && --Hallucination == 0)
				lbStatusChanged = true;

			// A SPELL ENDED OR THE MUSHROOM WORE OFF: the original runs its status pass
			// (duration_check -> FixPlayerEquips; a light going out is the host's, see
			// UWCharacter). That pass is where a worn curse strikes - NOT every tick, as ours did
			// until 2026-10-12 (the user's decision of that day: the original's timing).
			if (lbStatusChanged)
				RequestStatusPass();

			// A Ring of Regeneration gives its point on this tick and no other - the reference
			// puts the block directly after its status pass (PlayerUpdates_seg028_2985_13D,
			// label 213), before the minute and five-minute blocks below.
			if (mIHost != null)
				fTickWornRegeneration(mIHost.WornRegeneration);

			// The swim check, between the regeneration and the minute (duration_check).
			SwimCheckDue?.Invoke();

			// The minute: the poison, then the mana roll (duration_check, plyregen[1] % 3).
			if (miTickCounter % MinuteTicks == 0)
			{
				fTickPoison(piDamageTypeProof);
				fRegenerateMana();
			}

			if (miTickCounter % FiveMinuteTicks == 0)
				fTickFiveMinutes();
		}

		/// <summary>
		/// What the worn Regeneration and Mana Regeneration enchantments give on this tick:
		/// one point each, capped at the maximum, with no roll and no skill check - see
		/// UWWornRegeneration for the whole rule and where it is read from.
		///
		/// The reference calls its change routines unconditionally and lets them cap; the two
		/// "below maximum" tests here are that cap, written so that the flasks are only
		/// redrawn when something really moved.
		///
		/// ONE GUARD IS OURS: at zero hit points nothing is regenerated. The reference gets
		/// away without it because its tick kills the player a few lines earlier
		/// (PlayerUpdateTick_seg024_24DC_3A4, label 479) and never reaches the block with a
		/// corpse; our death runs on the host side and the tick can still come in.
		/// REVISITED in cleanup step 4.3 (2026-10-17), and kept: the vitals now raise Died on the
		/// crossing, but UWDeath acts on its next frame, and a long frame runs several ticks.
		/// </summary>
		private void fTickWornRegeneration(int piBits)
		{
			if (piBits == 0)
				return;

			bool lbChanged = false;

			// The point is the rule set's (vitals.wornRegeneration.amount, cleanup phase 10).
			int liAmount = UWRules.Current.Vitals.WornRegeneration.Amount;

			if ((piBits & UWWornRegeneration.HealthBit) != 0 && CurrentHP < MaxHP && CurrentHP > 0)
			{
				CurrentHP = Math.Min(MaxHP, CurrentHP + liAmount);
				lbChanged = true;
			}

			if ((piBits & UWWornRegeneration.ManaBit) != 0 && CurrentMana < MaxMana)
			{
				CurrentMana = Math.Min(MaxMana, CurrentMana + liAmount);
				lbChanged = true;
			}

			if (lbChanged)
				fVitalsChanged();
		}

		/// <summary>
		/// What happens every five minutes (PlayerUpdates_seg028_2985_13D, labels 2F4 to 3AD,
		/// read 2026-09-24): hunger drops by three to six, intoxication by one, fatigue rises by
		/// one up to 255, and ONE hit point comes back when a check of Strength against 15
		/// succeeds.
		///
		/// UNTIL 2026-09-24 the healing was the reference's: Strength against 10 and
		/// 1 + ((die(0..3) + check) * MaxHP) / 16 - a sixteenth to a third of the maximum per
		/// block. That formula belongs to the sleep (RegenerateOnSleep, the positive branch of
		/// the original's change routine); awake the block passes -1 to it, which is one point.
		/// </summary>
		private void fTickFiveMinutes()
		{
			// The numbers of this block are the rule set's (vitals.*, cleanup phase 10); the consts
			// are their defaults.
			UWRulesVitals lORules = UWRules.Current.Vitals;

			Hunger = Math.Max(0, Hunger - lORules.Hunger.PerBlockBase - UWRandom.Next(lORules.Hunger.PerBlockRoll));

			// THREE BYTES COUNT UP TOGETHER, each up to 255 (label 363): fatigue 0x3A, the meal
			// heal counter 0x3B and 0x3C.
			Fatigue = Math.Min(0xFF, Fatigue + 1);
			MealHealCounter = Math.Min(0xFF, MealHealCounter + 1);
			Counter3C = Math.Min(0xFF, Counter3C + 1);

			// INTOXICATION BELONGS HERE, not on the minute tick where it sat until 2026-09-22 -
			// the original steps it down one in this block (label 31F), eight times slower than
			// we had it.
			if (Intoxication > 0)
				Intoxication--;

			// THE LEVEL'S SHARE comes here, between the drunkenness and the fatigue in the
			// original's block (duration_check, PLAYTIME.C): the creatures' checkup. Ours had none
			// until 2026-10-15.
			FiveMinuteBlockDue?.Invoke();

			// On STRENGTH, which is an attribute and not a skill (the player's creature row
			// byte 5, see UWPlayerCritterRow).
			int liCheck = fGetCheckMagnitude(Strength, lORules.Healing.Difficulty);

			if (liCheck <= 0 || CurrentHP >= MaxHP || CurrentHP <= 0)
				return;

			CurrentHP = Math.Min(MaxHP, CurrentHP + 1);

			fVitalsChanged();
		}

		/// <summary>What the five minutes take off the hunger: three plus 0 to 3.</summary>
		public const int HungerPerBlockBase = 3;

		public const int HungerPerBlockRoll = 4;

		/// <summary>Every minute some mana back if the check of the Mana skill against 10
		/// succeeds: as many points as the check's result, one or two (label 26E, the
		/// negative branch of the change routine adds the amount as it is).
		/// </summary>
		private void fRegenerateMana()
		{
			int liCheck = fGetCheckMagnitude(GetSkill(UWPlayerData.Skill.Mana), UWRules.Current.Vitals.ManaRegeneration.Difficulty);

			if (liCheck <= 0 || CurrentMana >= MaxMana)
				return;

			CurrentMana = Math.Min(MaxMana, CurrentMana + liCheck);

			fVitalsChanged();
		}

		/// <summary>The difficulty of the minute's mana check (push 0x0A before the call).</summary>
		internal const int ManaRegenerationDifficulty = 10;

		/// <summary>The difficulty of the five minutes' healing check (push 0x0F).</summary>
		internal const int NaturalHealingDifficulty = 15;

		/// <summary>
		/// How WELL a check went, as a number: 0 on failure, otherwise 1 or 2.
		///
		/// THE SAME AS THE ORIGINAL (settled 2026-09-24): SkillCheck_seg037_32E6_C returns exactly
		/// -1, 0, 1 or 2, the four bands of UWSkillCheck. The reference had called it a finer
		/// number, and this was listed as a deviation until then.
		/// </summary>
		private static int fGetCheckMagnitude(int piValue, int piDifficulty)
		{
			return Math.Max(0, (int)UWSkillCheck.Check(piValue, piDifficulty));
		}

		// ------------------------------------------------- Clock, sleep, mana

		/// <summary>
		/// Advances the game clock by whole hours. Like the methods below, part of what
		/// sleeping changes about the character - see UWSleep.
		/// </summary>
		public void AdvanceClockHours(int piHours)
		{
			if (piHours > 0)
				ClockValue += piHours * UWGameClock.UnitsPerHour;
		}

		/// <summary>Advances the game clock by whole minutes - repairing at the anvil (see
		/// Interaction.fRepair).</summary>
		public void AdvanceClockMinutes(int piMinutes)
		{
			if (piMinutes > 0)
				ClockValue += piMinutes * UWGameClock.UnitsPerMinute;
		}

		/// <summary>
		/// Spell class 10: restore mana. The two spells of this class have no
		/// rune sequence - they exist only in items.
		///
		/// The amount is 1 + (MaxMana * (subclass + roll 0 to 3)) / 16, exactly as restore_mana
		/// (UnderworldExhumed SPELLS.C) adds it. For subclass 3 that is a fifth to a third of the
		/// pool, for 9 a good half to three quarters. The reference doubted this formula; the
		/// original settles it (2026-10-12).
		/// </summary>
		public void RestoreManaFromSpell(int piMinorClass)
		{
			// The same share restore_mana adds (fRestoreAmount). Until 2026-10-11 this line divided
			// in float (/ 16f); the maximum is always a whole number, so the results were identical.
			CurrentMana = Math.Min(MaxMana, CurrentMana + fRestoreAmount(MaxMana, piMinorClass));

			fVitalsChanged();
		}

		public void ClearFatigue()
		{
			Fatigue = 0;
		}

		public void ReduceFatigue(int piAmount)
		{
			Fatigue = Math.Max(0, Fatigue - piAmount);
		}

		/// <summary>
		/// Changes hunger, up or down. Fed is 255 - ChangeHunger_ovr143_1107, read 2026-09-24.
		///
		/// Beyond 255 nothing changes and false comes back (the eater answers "too full"). Below
		/// zero it stops at zero.
		///
		/// A MEAL HEALS: whenever the change is positive, the player gets back an eighth of the
		/// five-minute blocks since the last meal, at most eight hit points, and the count starts
		/// again. Whoever eats after an hour gets one point, after five hours eight. Until
		/// 2026-09-24 ours knew nothing of it, and byte 0x3B was "unknown".
		/// </summary>
		public bool ChangeHunger(int piAmount)
		{
			int liHunger = Hunger + piAmount;

			if (liHunger > FullHunger)
				return false;

			Hunger = Math.Max(0, liHunger);

			if (piAmount > 0)
			{
				// The two numbers are the rule set's (vitals.meal.*, cleanup phase 10).
				UWRulesVitals.MealRules lORules = UWRules.Current.Vitals.Meal;
				int liHeal = Math.Min(lORules.MaxHeal, MealHealCounter / lORules.BlocksPerPoint);

				MealHealCounter = 0;

				if (liHeal > 0 && CurrentHP > 0)
				{
					CurrentHP = Math.Min(MaxHP, CurrentHP + liHeal);

					fVitalsChanged();
				}
			}

			return true;
		}

		/// <summary>The most a meal heals.</summary>
		internal const int MaxMealHeal = 8;

		/// <summary>A meal heals one point per this many five-minute blocks since the last.</summary>
		public const int MealHealBlocksPerPoint = 8;

		/// <summary>
		/// Recovery after a night of sleep (player_sleep, UnderworldExhumed SKILLS.C, with SPELLS.C's
		/// restore_hp and restore_mana, read 2026-10-11; the Exhumed audit's row 6): the hit points
		/// gain max * (piHitPointAmount + 0..3) / 16 + 1, the mana first a flat six and then
		/// max * (piManaAmount + 0..3) / 16 + 1, both capped. The amounts are at least one, so a
		/// night always heals.
		///
		/// UNTIL 2026-10-11 the hit points healed only with the food factor and the mana got one flat
		/// sum - the reference's reading; a night without the bedroll or a full belly healed nothing.
		/// </summary>
		public void RegenerateOnSleep(int piHitPointAmount, int piManaAmount)
		{
			if (CurrentHP > 0)
				CurrentHP = Math.Min(MaxHP, CurrentHP + fRestoreShare(MaxHP, piHitPointAmount));

			CurrentMana = Math.Min(MaxMana, CurrentMana + UWRules.Current.Sleep.Recovery.FlatMana);
			CurrentMana = Math.Min(MaxMana, CurrentMana + fRestoreShare(MaxMana, piManaAmount));

			fVitalsChanged();
		}

		/// <summary>What restore_mana(-6) adds before the share.</summary>
		internal const int SleepFlatMana = 6;

		/// <summary>restore_hp / restore_mana with a positive amount: (max * (amount + RNG & 3)) >> 4,
		/// plus one.</summary>
		private static int fRestoreShare(int piMax, int piAmount)
		{
			if (piAmount <= 0)
				return -piAmount;

			return fRestoreAmount(piMax, piAmount);
		}

		/// <summary>(max * (amount + RNG & 3)) >> 4, plus one: the share of the maximum that
		/// restore_hp, restore_mana and the mana spell of class 10 add. One draw of 0 to 3.</summary>
		private static int fRestoreAmount(int piMax, int piAmount)
		{
			return ((piMax * (piAmount + UWRandom.Next(4))) >> 4) + 1;
		}

		/// <summary>Sets hit points to the maximum - Greater Heal.</summary>
		public void HealFully()
		{
			CurrentHP = MaxHP;

			fVitalsChanged();
		}

		// ------------------------------------------------- Noise

		/// <summary>
		/// How loud the player currently is, 0 to 15 - this is what critters hear (UWCritter).
		///
		/// FOLLOWING ApplyPlayerSneakScore_seg034_2F89_898 (115605-115709): standing is 0 whatever
		/// the base is, walking is the base plus momentum times ten divided by the forward speed
		/// minus five, and the motion state adds four on top of either (swimming, lava, floating
		/// with Levitate or Fly, a shaking view - IUWVitalsHost.IsInMotionState; only water until
		/// 2026-10-12). The base is NoiseBase, 13 minus Sneak / 3 and what the Stealth spell takes
		/// off it. If the value is above the stored one, it applies immediately; below it the stored one only drops by one every eight ticks.
		/// Winding up sets 10, the blow and repairing 15.
		///
		/// THE SPEED TERM CAME IN 2026-09-21 with the stealth check. Before that walking was a
		/// flat base minus five, which made Stealth silence even a running player - in the
		/// original he is still heard at five, because the base it removes is only one of the
		/// two summands.
		///
		/// Previously a standing player was silent immediately - and whoever fights stands. The wolf spiders
		/// on level 4 at 2-5/44-46 therefore did not join the fight with the reaper, in the original
		/// all of them did (per user, SAVE1 compared with SAVE4, 2026-09-13).
		/// </summary>
		public int Quietness { get; private set; }

		/// <summary>Noise from outside: winding up, blow, repair. Sets the value as the
		/// reference does.</summary>
		public void MakeNoise(int piLevel)
		{
			Quietness = Math.Clamp(piLevel, 0, MaxQuietness);
		}

		private const int MaxQuietness = 0xF;

		/// <summary>
		/// The stealth bit field of the active spells and worn enchantments, written by the
		/// status pass (UWCharacter.RefreshStatus from UWArmourProtection.Status.StealthBonus).
		/// The original keeps the two bases in dseg_5c99_1AFE and 1AFF and lets the status pass
		/// lower them there; we keep the bit field and lower the base when it is read, which
		/// comes to the same because nothing else writes those two bytes.
		/// </summary>
		public int StealthBonus { get; set; }

		/// <summary>How loud the player is without moving: 13 minus Sneak / 3, minus what the
		/// Stealth spell takes off.</summary>
		public int NoiseBase
		{
			get
			{
				UWRulesCritter.NoiseRules lORules = UWRules.Current.Critter.Noise;

				return UWArmourProtection.ApplyToNoise(
					lORules.Base - (GetSkill(UWPlayerData.Skill.Sneak) / lORules.SneakDivisor),
					StealthBonus);
			}
		}

		/// <summary>How visible the player is, 0 to 15: 15 minus Sneak / 5, minus what Conceal
		/// and Invisibility take off. This is what a creature's sight check reads.</summary>
		public int Visibility
		{
			get
			{
				UWRulesCritter.NoiseRules lORules = UWRules.Current.Critter.Noise;
				int liBase = UWArmourProtection.ApplyToVisibility(
					lORules.VisibilityBase - (GetSkill(UWPlayerData.Skill.Sneak) / lORules.VisibilitySneakDivisor),
					StealthBonus);

				return Math.Clamp(liBase, 0, MaxQuietness);
			}
		}

		/// <summary>
		/// PIT ticks of game time per call of the noise rule. THE ORIGINAL CALLS IT ONCE PER FRAME:
		/// ApplyPlayerSneakScore_seg034_2F89_898 sits in GameObjectLoop_seg034_2F89_518, the loop
		/// that also moves the creatures' slot clock (read 2026-09-23). Up at once to the target,
		/// down by one only when the counter dseg_5c99_783 is zero, the counter counting every call
		/// mod 8 - which is exactly TickQuietness. So the decay is one step per eight FRAMES, and
		/// the frame rate of the original depends on the machine (the loop waits at least four PIT
		/// ticks and a retrace, up to 64 frames a second, fewer when the drawing takes longer; in
		/// DOSBox the cycles decide). Until 2026-09-23 the comments said "once per 8 player ticks" -
		/// that was wrong.
		///
		/// ON THE GAME'S CLOCK AT THE ORIGINAL'S NOMINAL FRAME RATE since cleanup phase 10
		/// (2026-10-26): 32 calls a second, one per 8 PIT ticks of running game time - the rate
		/// every per-frame rule of the original runs at here (the lava's roll,
		/// UWPlayerTerrainRules.LavaRollTicks; the Void, UWVoidRules.TicksPerSecond), about what
		/// the original reaches in DOSBox (the lava measured 2026-09-14; Docs/DEVIATIONS.md). Until
		/// then twelve calls a second from the frame's elapsed seconds, a guess, so a stopped walk
		/// took 2.7 times as long to die down (one step per two thirds of a second, now one per
		/// quarter), and the decay ran on through a conversation or a menu.
		/// </summary>
		internal const int QuietnessCallTicks = UWPlayerTick.PitTicksPerSecond / 32;

		/// <summary>PIT ticks handed in and not yet used for a call.</summary>
		private int miQuietnessTicks;

		private int miQuietnessCooldown;

		/// <summary>Winding up is loud (reference: CombatStages.Charging).</summary>
		public const int ChargingNoise = 0xA;

		public const int StrikeNoise = 0xF;

		/// <summary>
		/// What the movement adds: momentum times ten divided by the forward speed, an integer
		/// division, so 0 to 10 (ApplyPlayerSneakScore_seg034_2F89_898, label 8B5). Together
		/// with the five it subtracts afterwards this is why a creeping player is quieter than
		/// his own base and a running one louder.
		/// </summary>
		public static int GetMovingNoise(float pfMovementFraction)
		{
			if (pfMovementFraction <= 0f)
				return 0;

			if (pfMovementFraction > 1f)
				pfMovementFraction = 1f;

			// The original divides (an integer division, towards zero); ours has the quotient as a
			// float and truncates it the same way.
			return UWRounding.Truncate(UWRules.Current.Critter.Noise.SpeedFactor * pfMovementFraction);
		}

		/// <summary>
		/// One step of the easy movement: it is as loud as the flat four of
		/// ApplyPlayerSneakScore_seg034_2F89_898 on top of the base, the motion state adds its four as
		/// everywhere, and the game clock gains UWEasyMovement.ClockAdvance. The original
		/// substitutes the four because it clears the momentum for this step, so the usual
		/// speed summand would be zero. Quieter than what is already stored does not count,
		/// exactly as in the decay below.
		/// </summary>
		public void ReportEasyMovementStep()
		{
			UWRulesCritter lORules = UWRules.Current.Critter;

			int liNow = NoiseBase + lORules.Noise.EasyMove;

			if (mIHost != null && mIHost.IsInMotionState)
				liNow += lORules.Noise.MotionState;

			liNow = Math.Clamp(liNow, 0, MaxQuietness);

			if (liNow >= Quietness)
				Quietness = liNow;

			ClockValue += UWEasyMovement.ClockAdvance;
		}

		/// <summary>The noise rule, driven with the PIT ticks of game time since the last call (the
		/// host's share of UWWorldScheduler.Now): one call per QuietnessCallTicks.</summary>
		public void TickQuietness(int piPitTicks)
		{
			UWRulesCritter lORules = UWRules.Current.Critter;

			if (piPitTicks > 0)
				miQuietnessTicks += piPitTicks;

			bool lbMoving = mIHost != null && mIHost.IsMoving;
			bool lbInMotionState = mIHost != null && mIHost.IsInMotionState;
			float lfFraction = mIHost != null ? mIHost.MovementFraction : 0f;

			while (miQuietnessTicks >= QuietnessCallTicks)
			{
				miQuietnessTicks -= QuietnessCallTicks;

				int liNow = 0;

				if (lbMoving)
					liNow = NoiseBase + GetMovingNoise(lfFraction) - lORules.Noise.MovingOffset;

				if (lbInMotionState)
					liNow += lORules.Noise.MotionState;

				liNow = Math.Clamp(liNow, 0, MaxQuietness);

				if (liNow >= Quietness)
					Quietness = liNow;
				else if (miQuietnessCooldown == 0)
					Quietness--;

				miQuietnessCooldown = (miQuietnessCooldown + 1) % 8;
			}

			if (mIHost != null && mIHost.IsChargingAttack)
				MakeNoise(ChargingNoise);
		}
	}
}
