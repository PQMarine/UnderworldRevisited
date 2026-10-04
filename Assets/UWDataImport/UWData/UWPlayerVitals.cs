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
	/// doll, the weapon animation and the camera stay there.
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

			public float FallbackMaxHP;

			public float FallbackMaxMana;

			/// <summary>Hit points at start without a save game, minus one for full.</summary>
			public float FallbackCurrentHP;

			public float FallbackCurrentMana;

			public static StartValues Default => new StartValues
			{
				Strength = -1, Dexterity = -1, Intelligence = -1, Attack = -1, Level = -1,
				ManaSkill = -1, CastingSkill = -1,
				FallbackMaxHP = 30f, FallbackMaxMana = 20f, FallbackCurrentHP = -1f, FallbackCurrentMana = -1f
			};
		}

		private readonly IUWVitalsHost mIHost;

		private DataImport mOData;

		public UWPlayerVitals(IUWVitalsHost pIHost)
		{
			mIHost = pIHost;
		}

		// ------------------------------------------------- Health and mana

		public float MaxHP { get; private set; }

		public float CurrentHP { get; private set; }

		public float MaxMana { get; private set; }

		public float CurrentMana { get; private set; }

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
			IsEasyDifficulty = lOPlayer != null && lOPlayer.IsEasyDifficulty;

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
			ClockValue = lbLoaded ? lOPlayer.ClockValue : 0;
			Intoxication = lbLoaded ? lOPlayer.Intoxication : 0;
			Hallucination = lbLoaded ? lOPlayer.Hallucination : 0;

			MaxHP = pOStart.FallbackMaxHP;
			MaxMana = pOStart.FallbackMaxMana;
			CurrentHP = pOStart.FallbackCurrentHP < 0f ? MaxHP : Math.Min(pOStart.FallbackCurrentHP, MaxHP);
			CurrentMana = pOStart.FallbackCurrentMana < 0f ? MaxMana : Math.Min(pOStart.FallbackCurrentMana, MaxMana);

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
			if (mIHost != null)
				mIHost.OnVitalsChanged();
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

			if (mIHost != null)
				mIHost.OnKillRewarded(liReward > 0);

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

			if (mIHost != null)
				mIHost.AddMessage(fGetLevelUpMessage());
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

			if (mIHost != null)
				mIHost.AddMessage(fGetLevelUpMessage());
		}

		/// <summary>Sets hunger, hit points, mana and poison as a conversation returns them at
		/// the end (play_hunger, play_hp, play_mana, play_poison - UW.EXE
		/// RetrieveImportedVariablesAfterConversation). Only changed values are
		/// written, so that fractional parts are not lost.</summary>
		public void ApplyConversationValues(int piHunger, int piHp, int piMana, int piPoison)
		{
			if (piHunger != Hunger)
				Hunger = Math.Clamp(piHunger & 0xFF, 0, FullHunger);

			if (piHp != UWUnits.RoundToInt(CurrentHP))
				CurrentHP = Math.Clamp(piHp & 0xFF, 0f, MaxHP);

			if (piMana != UWUnits.RoundToInt(CurrentMana))
				CurrentMana = Math.Clamp(piMana & 0xFF, 0f, MaxMana);

			if (piPoison != Poison)
				Poison = Math.Clamp(piPoison & 0xF, 0, MaxPoison);

			fVitalsChanged();
		}

		/// <summary>Maximum health and mana after a level-up.</summary>
		private void fApplyLevel()
		{
			MaxHP = UWExperience.GetMaximumHealth(Strength, mOExperience.Level);

			if (CurrentHP > MaxHP)
				CurrentHP = MaxHP;

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
		private static readonly int[] msSkillRollRange = { 0x19, 0x28, 0x0A };

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
		/// BUT ONLY FOR DEXTERITY AND INTELLIGENCE: the reference checks for
		/// "attribute not equal to zero", and zero is also the number of Strength. The
		/// combat skills therefore get nothing, even though their cap does come from
		/// Strength. This looks like an oversight in the original and has been
		/// kept as is.
		/// </summary>
		public bool TryIncreaseSkill(int piSkillNumber)
		{
			if (piSkillNumber < 0 || piSkillNumber >= SkillNumberCount)
				return false;

			int liAttribute = fGetGoverningAttribute(piSkillNumber);
			int liAttributeValue = fGetAttributeValue(liAttribute);
			int liValue = GetSkillByNumber(piSkillNumber);

			if ((liAttributeValue << 1) < liValue || liValue >= MaxSkillValue)
				return false;

			liValue++;

			if (liAttribute != 0)
			{
				if (liAttributeValue / 2 > liValue)
					liValue++;

				if (liValue < liAttributeValue
					&& UWRandom.Next(msSkillRollRange[liAttribute]) < liAttributeValue - liValue)
					liValue++;
			}

			if (liValue > MaxSkillValue)
				liValue = MaxSkillValue;

			fSetSkillByNumber(piSkillNumber, liValue);

			fRefreshDerivedValues();

			// BETTER LORE MAKES THE WORLD UNIDENTIFIED AGAIN. Without this it achieved nothing
			// at all: the check is rolled only once per item (see UWLoreCheck).
			if (piSkillNumber == LoreSkillNumber && mIHost != null)
				mIHost.ResetIdentification();

			return true;
		}

		/// <summary>Lore in the save game numbering - see AttackSkillNumber.
		/// </summary>
		private const int LoreSkillNumber = FirstNamedSkillNumber + (int)UWPlayerData.Skill.Lore;

		/// <summary>Spends one skill point.</summary>
		public void SpendSkillPoint()
		{
			if (mOExperience.SkillPoints > 0)
				mOExperience.SkillPoints--;
		}

		/// <summary>Sets the maximum and the current mana directly - for Tybal's orb, which
		/// drains both in its lair and gives them back (UWTybalOrbRules). The current value is
		/// kept at or below the maximum.</summary>
		public void SetMana(float pfMaximum, float pfCurrent)
		{
			MaxMana = Math.Max(0f, pfMaximum);
			CurrentMana = Math.Clamp(pfCurrent, 0f, MaxMana);

			fVitalsChanged();
		}

		/// <summary>The health and mana limits depend on skills and attributes and
		/// must be recomputed after a raise.</summary>
		private void fRefreshDerivedValues()
		{
			fApplyMaximumMana();

			fVitalsChanged();
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

		/// <summary>How drunk the character is, 0 to 63. Drops by one on the minute tick and by
		/// sixteen during sleep - see Drink.</summary>
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
		private const int DrinkHealAmount = 2;

		/// <summary>Base duration of the shaking after a failed sip.</summary>
		private const int DrinkShakeBase = 0xA;

		/// <summary>
		/// Drinks something intoxicating and reports what came of it.
		///
		/// The reference's sequence (food.DrinkLiquid): intoxication rises by the magnitude of
		/// the negative nutrition value - water one, port eight -, and then STRENGTH is checked
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
				CurrentHP = Math.Min(MaxHP, CurrentHP + DrinkHealAmount);

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

			if (mIHost != null)
				mIHost.ShakeUnsteady(Intoxication / 6);

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
		/// How long the mushroom effect still lasts, 0 to 3. Drops by one on the minute tick.
		///
		/// WHAT IS SEEN while it runs is UWHallucinationState - one of three effects, rolled once
		/// (the palette one is built, see UWPaletteRenderToggle). The
		/// counter still runs: it is stored in the save game, and the visuals can be hooked up
		/// to it later without anything else needing to change. The mushroom raises it by one,
		/// the Hallucination spell of some potions and a scroll sets it to the maximum.
		/// </summary>
		public int Hallucination { get; private set; }

		/// <summary>Maximum value of the mushroom effect - two bits in the save game.</summary>
		public const int MaxHallucination = 3;

		public void AddHallucination()
		{
			Hallucination = Math.Min(MaxHallucination, Hallucination + 1);
		}

		/// <summary>Item spell 13/5 writes 3 into the two bits - see UWItemSpellRules.</summary>
		public void StartHallucination()
		{
			Hallucination = MaxHallucination;
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
			if (Poison < ToadstoolPoison)
				Poison = ToadstoolPoison;
			else if (Poison < ToadstoolPoisonLimit)
				Poison += 2;
			else
				return;

			fVitalsChanged();
		}

		private const int ToadstoolPoison = 4;

		private const int ToadstoolPoisonLimit = 0xD;

		/// <summary>The shaking after waking from a drink: 10 plus intoxication / 6.</summary>
		public void ShakeAfterPassingOut()
		{
			if (mIHost != null)
				mIHost.ShakeUnsteady(DrinkShakeBase + (Intoxication / 6));
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

		/// <summary>From the reference: the shake level is the damage divided by four, capped at
		/// three.</summary>
		private const int DamageLevelDivisor = 4;

		private const int MaxDamageLevel = 3;

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

		/// <summary>
		/// Damage to the player, with its type. Subtracts hit points and reports whether he
		/// died from it. This class deliberately does not decide what happens on death.
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
		/// starvation, drowning and the damage trap.
		/// </summary>
		public bool ApplyDamage(int piDamage, int piDamageType, int piDamageTypeProof)
		{
			piDamage = UWDamageTypes.Scale(piDamageTypeProof, piDamage, piDamageType);

			if (piDamage > 0)
			{
				LastDamage = piDamage;
				LastDamageType = piDamageType;
				miDamageThisTick += piDamage;

				if (mIHost != null)
					mIHost.OnDamageRecorded();

				CurrentHP -= piDamage;

				if (CurrentHP < 0f)
					CurrentHP = 0f;

				fVitalsChanged();

				// A hit shakes the view. The LEVEL is the damage divided by four, at most
				// three - that is how the reference computes it (combat.cs lines 577-580), and the
				// duration follows from it. A graze therefore does not shake at all.
				if (mIHost != null)
					mIHost.ShakeOnDamage(Math.Min(piDamage / DamageLevelDivisor, MaxDamageLevel));
			}

			return CurrentHP <= 0f;
		}

		/// <summary>A curse brings nobody below this limit - that is how the reference has it.
		/// </summary>
		private const int CurseMinimumHealth = 3;

		/// <summary>The curse rolls eight-sided dice.</summary>
		private const int CurseDieSides = 8;

		/// <summary>
		/// A cursed piece of equipment strikes (reference:
		/// spellcasting_class_9.CastClass9_Curse). Returns whether it struck at all.
		///
		/// THE ROLL IS "subclass times d8": the reference computes
		/// DiceRoll(subclass, 8), which is subclass plus that many rolls from zero to
		/// seven. For the cursed crowns with subclass 5 that is five to forty.
		///
		/// IT CANNOT KILL. If fewer than three hit points would remain, it is set to exactly
		/// three - you waste away, but do not die of it.
		///
		/// NO RESISTANCE AND NO SHAKING: the reference subtracts the value straight from the
		/// hit points, without going through the damage types. The hit is still
		/// recorded so that it shows up in the F1 display. Whether the view flashes is the
		/// host's decision (when EQUIPPING nothing flashes, afterwards it does).
		/// </summary>
		public bool ApplyCurse(int piDice)
		{
			if (piDice <= 0)
				return false;

			int liDamage = piDice;

			for (int liAt = 0; liAt < piDice; liAt++)
				liDamage += UWRandom.Next(CurseDieSides);

			LastDamage = liDamage;
			LastDamageType = UWDamageTypes.None;

			if (mIHost != null)
				mIHost.OnDamageRecorded();

			CurrentHP = CurrentHP - liDamage < CurseMinimumHealth
				? CurseMinimumHealth
				: CurrentHP - liDamage;

			fVitalsChanged();

			return true;
		}

		/// <summary>Subtracts mana and updates the flask. Never below zero.</summary>
		public void SpendMana(int piAmount)
		{
			CurrentMana -= piAmount;

			if (CurrentMana < 0f)
				CurrentMana = 0f;

			fVitalsChanged();
		}

		/// <summary>
		/// Damage that leaves at least this many hit points - the leeches bite hard but never kill
		/// (the reference: leech.LeechDamage, see UWItemDrag).
		/// </summary>
		public void ApplyDamageKeepingMinimum(int piDamage, int piMinimum, int piDamageTypeProof)
		{
			int liAllowed = Math.Max(0, (int)Math.Floor(CurrentHP) - piMinimum);

			if (liAllowed <= 0 || piDamage <= 0)
				return;

			ApplyDamage(Math.Min(piDamage, liAllowed), UWDamageTypes.None, piDamageTypeProof);
		}

		/// <summary>Counterpart to ApplyDamage, for instance for healing potions.</summary>
		public void Heal(int piAmount)
		{
			if (piAmount <= 0)
				return;

			CurrentHP += piAmount;

			if (CurrentHP > MaxHP)
				CurrentHP = MaxHP;

			fVitalsChanged();
		}

		/// <summary>
		/// Subtracts hit points without it being a hit - no shaking, no damage type.
		/// For the Ethereal Void (UWVoidEffects), which sets the points directly in UW.EXE.
		/// Never goes below zero.
		/// </summary>
		public void DrainVitality(int piAmount)
		{
			if (piAmount <= 0)
				return;

			CurrentHP = Math.Max(0f, CurrentHP - piAmount);

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
		/// </summary>
		public void ApplyPoison(int piStrength)
		{
			if (piStrength <= Poison)
				return;

			Poison = piStrength > MaxPoison ? MaxPoison : piStrength;

			fVitalsChanged();
		}

		/// <summary>Highest value a poisoning can take.</summary>
		private const int MaxPoison = 0xF;

		/// <summary>Three ticks are the original's minute. Poison and mana regeneration hang
		/// on this - that is how the reference counts in its player loop. The length comes from
		/// UWPlayerTick, so three ticks are sixty-three real seconds.</summary>
		private const int MinuteTicks = UWPlayerTick.MinuteTicks;

		private int miPoisonTickCounter;

		/// <summary>
		/// The poison takes effect: damage equal to the current value, and the value drops by one.
		///
		/// Called on every tick of RunGameTick, but counts its own minute
		/// (MinuteTicks), so it takes effect once per minute.
		/// </summary>
		private void fTickPoison(int piDamageTypeProof)
		{
			if (Poison <= 0)
				return;

			miPoisonTickCounter++;

			if (miPoisonTickCounter < MinuteTicks)
				return;

			miPoisonTickCounter = 0;

			// POISON DAMAGE IS POISON: the reference gives type 0x10 for it (playerdatloop, in the
			// minute block). Poison Resistance thereby nullifies the poison damage - the
			// poison itself still runs its course.
			ApplyDamage(Poison, UWDamageTypes.Poison, piDamageTypeProof);

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

			UWActiveSpellEffect lOSpell = mOActiveSpells[piIndex];

			// LEVITATE AND FLY TURN INTO SLOW FALL, keeping what is left of their stability - as
			// when they wear off, so one does not plummet out of the air (per user, 2026-10-04; the
			// reference's spell icon click does the same, uimanager_spells).
			if (lOSpell.MajorClass == MotionMajor
				&& ((lOSpell.MinorClass & 0x3F) == LevitateMinor || (lOSpell.MinorClass & 0x3F) == FlyMinor))
			{
				lOSpell.MinorClass = SlowFallMinor;
				mOActiveSpells[piIndex] = lOSpell;
			}
			else
			{
				mOActiveSpells.RemoveAt(piIndex);
			}

			fApplyActiveSpells();
		}

		/// <summary>All active spells end - they wear off during sleep.</summary>
		public void EndAllSpells()
		{
			mOActiveSpells.Clear();

			// Clearing alone is not enough: speed, light and the motion modes hang on
			// fApplyActiveSpells and would otherwise remain, although no spell is running any more.
			fApplyActiveSpells();
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

			if (mIHost != null)
				mIHost.OnActiveSpellsChanged(lbHastened, lbRoaming, TimeIsFrozen, HasTelekinesis);
		}

		/// <summary>
		/// Counts down the duration of the spells.
		///
		/// At one it ends - except for Levitate and Fly: those turn into Slow Fall,
		/// so you do not plummet out of the air. The reference does exactly the same.
		/// </summary>
		private void fTickActiveSpells()
		{
			if (mOActiveSpells.Count == 0)
				return;

			bool lbChanged = false;

			for (int liSpell = mOActiveSpells.Count - 1; liSpell >= 0; liSpell--)
			{
				UWActiveSpellEffect lOSpell = mOActiveSpells[liSpell];

				lOSpell.Stability--;

				if (lOSpell.Stability > 1)
				{
					mOActiveSpells[liSpell] = lOSpell;

					continue;
				}

				if (lOSpell.MajorClass == MotionMajor
					&& (lOSpell.MinorClass == LevitateMinor || lOSpell.MinorClass == FlyMinor))
				{
					lOSpell.MinorClass = SlowFallMinor;

					mOActiveSpells[liSpell] = lOSpell;
				}
				else
				{
					mOActiveSpells.RemoveAt(liSpell);
				}

				lbChanged = true;
			}

			if (lbChanged)
				fApplyActiveSpells();
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

			fTickActiveSpells();

			// Poison counts its minute tick itself (see fTickPoison), so it is
			// still called on every tick as before.
			fTickPoison(piDamageTypeProof);

			// Cursed equipment strikes on exactly this tick - the reference attaches the
			// curse to its status pass, and that runs in the same timed block there.
			if (mIHost != null && ApplyCurse(mIHost.WornCurseDice))
				mIHost.OnCurseStruck();

			// A Ring of Regeneration gives its point on this tick and no other - the reference
			// puts the block directly after its status pass (PlayerUpdates_seg028_2985_13D,
			// label 213), before the minute and five-minute blocks below.
			if (mIHost != null)
				fTickWornRegeneration(mIHost.WornRegeneration);

			// THE MUSHROOM WEARS OFF ON EVERY TICK, before any counter is looked at
			// (PlayerUpdates_seg028_2985_13D label 1CD). It sat on the minute tick until
			// 2026-09-22, three times too slow.
			if (Hallucination > 0)
				Hallucination--;

			if (miTickCounter % MinuteTicks == 0)
				fRegenerateMana();

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
		/// </summary>
		private void fTickWornRegeneration(int piBits)
		{
			if (piBits == 0)
				return;

			bool lbChanged = false;

			if ((piBits & UWWornRegeneration.HealthBit) != 0 && CurrentHP < MaxHP && CurrentHP > 0f)
			{
				CurrentHP = Math.Min(MaxHP, CurrentHP + UWWornRegeneration.Amount);
				lbChanged = true;
			}

			if ((piBits & UWWornRegeneration.ManaBit) != 0 && CurrentMana < MaxMana)
			{
				CurrentMana = Math.Min(MaxMana, CurrentMana + UWWornRegeneration.Amount);
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
			Hunger = Math.Max(0, Hunger - 3 - UWRandom.Next(4));

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

			// On STRENGTH, which is an attribute and not a skill (the player's creature row
			// byte 5, see UWPlayerCritterRow).
			int liCheck = fGetCheckMagnitude(Strength, NaturalHealingDifficulty);

			if (liCheck <= 0 || CurrentHP >= MaxHP || CurrentHP <= 0)
				return;

			CurrentHP = Math.Min(MaxHP, CurrentHP + 1);

			fVitalsChanged();
		}

		/// <summary>Every minute some mana back if the check of the Mana skill against 10
		/// succeeds: as many points as the check's result, one or two (label 26E, the
		/// negative branch of the change routine adds the amount as it is).
		/// </summary>
		private void fRegenerateMana()
		{
			int liCheck = fGetCheckMagnitude(GetSkill(UWPlayerData.Skill.Mana), ManaRegenerationDifficulty);

			if (liCheck <= 0 || CurrentMana >= MaxMana)
				return;

			CurrentMana = Math.Min(MaxMana, CurrentMana + liCheck);

			fVitalsChanged();
		}

		/// <summary>The difficulty of the minute's mana check (push 0x0A before the call).</summary>
		private const int ManaRegenerationDifficulty = 10;

		/// <summary>The difficulty of the five minutes' healing check (push 0x0F).</summary>
		private const int NaturalHealingDifficulty = 15;

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
		/// The reference computes the amount as 1 + (MaxMana * (subclass + roll 0 to 3)) / 16
		/// (playerdatloop.ManaRegenChange). For subclass 3 that is a fifth to a
		/// third of the pool, for 9 a good half to three quarters.
		///
		/// THE REFERENCE ITSELF DOUBTS THIS FORMULA ("may be wrong and is restoring too much
		/// mana"). We adopt it anyway because it is the only source - if it is less in the
		/// original, that will show up in testing.
		/// </summary>
		public void RestoreManaFromSpell(int piMinorClass)
		{
			int liIncrease = 1 + (int)((MaxMana * (piMinorClass + UWRandom.Next(4))) / 16f);

			CurrentMana = Math.Min(MaxMana, CurrentMana + liIncrease);

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
				int liHeal = Math.Min(MaxMealHeal, MealHealCounter / 8);

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
		private const int MaxMealHeal = 8;

		/// <summary>
		/// Recovery after a night of sleep.
		///
		/// Health uses the same formula as the five-minute tick - with bonus
		/// and die, see fTickFiveMinutes -, mana is a flat amount. That is exactly how
		/// the reference separates it (HPRegenerationChange with a positive, ManaRegenChange with
		/// a negative value).
		///
		/// A health factor of zero means no healing: whoever sleeps without a bedroll or
		/// hungry only wakes up rested.
		/// </summary>
		public void RegenerateOnSleep(int piVitalityFactor, int piMana)
		{
			if (piVitalityFactor > 0 && CurrentHP > 0)
				CurrentHP = Math.Min(MaxHP,
					CurrentHP + 1 + (float)Math.Floor((UWRandom.Next(4) + piVitalityFactor) * MaxHP / 16f));

			if (piMana > 0)
				CurrentMana = Math.Min(MaxMana, CurrentMana + piMana);

			fVitalsChanged();
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
		/// minus five, and water adds four on top of either. The base is NoiseBase, 13 minus
		/// Sneak / 3 and what the Stealth spell takes off it. If the value is above the stored one,
		/// it applies immediately; below it the stored one only drops by one every eight ticks.
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
				return UWArmourProtection.ApplyToNoise(
					UWCritterRules.PlayerNoiseBase
						- (GetSkill(UWPlayerData.Skill.Sneak) / UWCritterRules.PlayerNoiseSneakDivisor),
					StealthBonus);
			}
		}

		/// <summary>How visible the player is, 0 to 15: 15 minus Sneak / 5, minus what Conceal
		/// and Invisibility take off. This is what a creature's sight check reads.</summary>
		public int Visibility
		{
			get
			{
				int liBase = UWArmourProtection.ApplyToVisibility(
					UWCritterRules.PlayerVisibilityBase
						- (GetSkill(UWPlayerData.Skill.Sneak) / UWCritterRules.PlayerVisibilitySneakDivisor),
					StealthBonus);

				return Math.Clamp(liBase, 0, MaxQuietness);
			}
		}

		/// <summary>
		/// Calls per second of the noise rule. THE ORIGINAL CALLS IT ONCE PER FRAME:
		/// ApplyPlayerSneakScore_seg034_2F89_898 sits in GameObjectLoop_seg034_2F89_518, the loop
		/// that also moves the creatures' slot clock (read 2026-09-23). Up at once to the target,
		/// down by one only when the counter dseg_5c99_783 is zero, the counter counting every call
		/// mod 8 - which is exactly TickQuietness. So the decay is one step per eight FRAMES, and
		/// the frame rate of the original depends on the machine (the loop waits at least four PIT
		/// ticks and a retrace, up to 64 frames a second, fewer when the drawing takes longer; in
		/// DOSBox the cycles decide). There is no fixed rate to take over; twelve stands in for
		/// it, the same assumption as for the void effects (UWSettings.VoidEffectTicksPerSecond).
		/// Until 2026-09-23 the comments said "once per 8 player ticks" - that was wrong.
		/// </summary>
		private const float QuietnessTicksPerSecond = 12f;

		private float mfQuietnessTicks;

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

			return (int)(UWCritterRules.PlayerNoiseSpeedFactor * pfMovementFraction);
		}

		/// <summary>
		/// One step of the easy movement: it is as loud as the flat four of
		/// ApplyPlayerSneakScore_seg034_2F89_898 on top of the base, water adds its four as
		/// everywhere, and the game clock gains UWEasyMovement.ClockAdvance. The original
		/// substitutes the four because it clears the momentum for this step, so the usual
		/// speed summand would be zero. Quieter than what is already stored does not count,
		/// exactly as in the decay below.
		/// </summary>
		public void ReportEasyMovementStep()
		{
			int liNow = NoiseBase + UWCritterRules.PlayerNoiseEasyMove;

			if (mIHost != null && mIHost.IsInLiquid)
				liNow += UWCritterRules.PlayerNoiseWater;

			liNow = Math.Clamp(liNow, 0, MaxQuietness);

			if (liNow >= Quietness)
				Quietness = liNow;

			ClockValue += UWEasyMovement.ClockAdvance;
		}

		/// <summary>The noise decay, driven with the elapsed time of the frame.</summary>
		public void TickQuietness(float pfElapsedSeconds)
		{
			mfQuietnessTicks += pfElapsedSeconds * QuietnessTicksPerSecond;

			bool lbMoving = mIHost != null && mIHost.IsMoving;
			bool lbInLiquid = mIHost != null && mIHost.IsInLiquid;
			float lfFraction = mIHost != null ? mIHost.MovementFraction : 0f;

			while (mfQuietnessTicks >= 1f)
			{
				mfQuietnessTicks -= 1f;

				int liNow = 0;

				if (lbMoving)
					liNow = NoiseBase + GetMovingNoise(lfFraction) - UWCritterRules.PlayerNoiseMovingOffset;

				if (lbInLiquid)
					liNow += UWCritterRules.PlayerNoiseWater;

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
