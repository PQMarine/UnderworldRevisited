namespace UWDataImport.UWData
{
	/// <summary>
	/// The numbers of the player's own blows and shots: the hit check and the damage of a
	/// melee strike, the damage of a missile. Engine-free since 2026-09-18 (P3 of the engine
	/// separation), out of Interaction, which keeps finding the target, the sounds, the
	/// effects and the eyes.
	/// </summary>
	public static class UWPlayerAttack
	{
		/// <summary>Target value of the Missile check when shooting - the reference checks against ten.
		/// </summary>
		private const int MissileSkillTarget = 0xA;

		/// <summary>The outcome of a melee strike against one target.</summary>
		public struct MeleeResult
		{
			/// <summary>No melee entry for the weapon, and none for the fist either - the strike
			/// fizzles.</summary>
			public bool NoWeapon;

			/// <summary>What the hit check was rolled with, for the log.</summary>
			public int Accuracy;

			public UWSkillCheck.ResultEnum Check;

			public bool Hit;

			/// <summary>The damage after armour; zero leaves no visible trace.</summary>
			public int Damage;

			/// <summary>The damage BEFORE armour - what the hit sound is played with, see
			/// UWCombat.ComputeDamage.</summary>
			public int DamageBeforeArmour;

			/// <summary>The flanking bonus the blow carried (0 to 4).</summary>
			public int Flank;
		}

		/// <summary>
		/// A melee strike: the hit check and the damage.
		///
		/// Strength goes into the base value - according to the docs the weapon's table value is
		/// only a bonus on top (see UWCombat). Weapon skill belongs in the hit check, not in
		/// the damage.
		///
		/// Our charge bar runs from 0 to 1; the original maps it onto the span
		/// between the weapon's MinCharge and MaxCharge and multiplies the damage by it.
		///
		/// HIT CHECK. Accuracy is made up of weapon skill, half the
		/// attack value and one seventh of dexterity; the target value is the
		/// creature's defence value. This explains the user's observation that
		/// a mage misses very often, while a fighter almost always hits -
		/// skill and attack only go in here, not into the damage.
		/// </summary>
		/// <param name="piWeaponId">The wielded weapon, or UWCombat.FistObjectId.</param>
		/// <summary>
		/// What the EASY difficulty gives the player on his attack score
		/// (CalculateAttackScorePlayer_seg022_FF6: seven points when PLAYER.DAT byte 0xB4 is
		/// not zero). Measured 2026-09-21: a character created on Standard has 0 there, one on
		/// Easy has 1 - the user made one save of each. Seven is a lot on a score that usually
		/// runs around twenty to thirty.
		///
		/// The other half of the difficulty was already built: on easy the player takes half
		/// damage (UWCritterCombat.HalveOnEasy).
		/// </summary>
		public const int EasyDifficultyAttackBonus = 7;

		public static MeleeResult ResolveMelee(DataImport pOData, int piWeaponId, UWCombat.AttackKind peKind,
			float pfChargeFraction, int piStrength, int piAttack, int piDexterity, int piUnarmedSkill,
			System.Func<UWPlayerData.Skill, int> pFGetSkill, int piTargetDefence, int piTargetArmour,
			bool pbEasyDifficulty = false, int piFlank = 0)
		{
			MeleeResult lOResult = new MeleeResult();

			if (pOData == null || pOData.ObjectClassProperties == null)
			{
				lOResult.NoWeapon = true;
				return lOResult;
			}

			if (!pOData.ObjectClassProperties.TryGetMeleeWeapon(piWeaponId, out UWObjectClassProperties.MeleeWeapon lOMelee))
			{
				// No melee entry - then strike with the fist instead of letting the strike
				// fizzle out without effect.
				if (!pOData.ObjectClassProperties.TryGetMeleeWeapon(UWCombat.FistObjectId, out lOMelee))
				{
					lOResult.NoWeapon = true;
					return lOResult;
				}
			}

			bool lbUnarmed = lOMelee.SkillType == UWCombat.UnarmedSkillType;

			int liCharge = UWCombat.MapCharge(lOMelee.MinCharge, lOMelee.MaxCharge, pfChargeFraction);

			int liWeaponSkill = pFGetSkill != null ? pFGetSkill(UWCombat.GetSkillForWeapon(lOMelee)) : 0;

			lOResult.Accuracy = liWeaponSkill + (piAttack / 2) + (piDexterity / 7);

			if (pbEasyDifficulty)
				lOResult.Accuracy += EasyDifficultyAttackBonus;

			// THE FLANKING BONUS (UWCritterRules.GetFlankingBonus, 0 from the front, 4 from
			// behind) goes into the check, not into the score itself (as UWCritterCombat.GetHitScore
			// adds it for a creature), and again into the damage (per user, 2026-09-28: the golem took
			// far more blows in ours than in the original, and ours hit less often).
			lOResult.Flank = piFlank;
			lOResult.Check = UWSkillCheck.Check(lOResult.Accuracy + piFlank, piTargetDefence);

			if (!UWSkillCheck.IsSuccess(lOResult.Check))
				return lOResult;

			lOResult.Hit = true;

			lOResult.Damage = UWCombat.ComputeDamage(
				UWCombat.GetWeaponDamage(lOMelee, peKind),
				lbUnarmed, piStrength, piUnarmedSkill, liCharge, piTargetArmour,
				out int liBeforeArmour,
				lOResult.Check == UWSkillCheck.ResultEnum.CriticalSuccess, piFlank);

			lOResult.DamageBeforeArmour = liBeforeArmour;

			return lOResult;
		}

		/// <summary>
		/// The damage of a missile - shot or thrown.
		///
		/// It runs through the same chain as any other hit, with one addition
		/// that only exists here: the MISSILE skill stretches or squeezes it before
		/// the roll (reference: combat.MissileImpact). It enters as (skill times eight
		/// plus 192) divided by 256 - so at skill 0 three quarters remain, at 8 it becomes
		/// one and a half times. A check against ten subtracts another half from that or
		/// adds three quarters.
		/// </summary>
		public static int GetMissileDamage(DataImport pOData, int piAmmunitionId, int piCharge, int piMissileSkill)
		{
			if (pOData == null || pOData.ObjectProperties == null)
				return 0;

			int liBase = pOData.ObjectProperties.GetRangedDamage(piAmmunitionId);

			int liFactor = (piMissileSkill << 3) + 0xC0;

			switch (UWSkillCheck.Check(piMissileSkill, MissileSkillTarget))
			{
				case UWSkillCheck.ResultEnum.CriticalFailure:
					liFactor -= 0x80;
					break;

				case UWSkillCheck.ResultEnum.CriticalSuccess:
					liFactor += 0xC0;
					break;
			}

			if (liFactor < 0)
				liFactor = 0;

			return UWCombat.ComputeDamage((liBase * liFactor) >> 8, false, 0, 0, piCharge, 0);
		}
	}
}
