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

			/// <summary>The whole of it as UWAttackResolution answered (cleanup step 4.9, 2026-10-18):
			/// the blocked sound of a miss, the hit volume, the blow that lands.</summary>
			public UWAttackOutcome Outcome;
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

		/// <summary>The major class of the weapon enchantments, Accuracy and Damage (DoPlayerWeapon).</summary>
		public const int WeaponEnchantmentMajorClass = 0xC;

		/// <summary>The minor's bit that makes it a damage enchantment; without it it is accuracy.</summary>
		private const int DamageEnchantmentBit = 8;

		/// <summary>
		/// THE WIELDED WEAPON'S ENCHANTMENT (DoPlayerWeapon, UnderworldExhumed COMBAT.C, read
		/// 2026-10-11; the Exhumed audit's row 5): an enchantment of major class 0xC in the spell
		/// field's plain form (decode_obj_spell without the format flag) adds (minor & 7) + 1 - to
		/// the damage when bit 3 of the minor is set, else to the attack score. Until 2026-10-11
		/// ours ignored it, and an enchanted weapon struck like a plain one.
		/// </summary>
		public static void GetWeaponEnchantment(UWObject pOWeapon, System.Collections.Generic.IList<UWObject> pORecords,
			out int piAccuracy, out int piDamage)
		{
			piAccuracy = 0;
			piDamage = 0;

			if (pOWeapon == null || !UWObjectMechanics.TryGetEnchantmentClass(pOWeapon, pORecords, out int liMajor, out int liMinor)
				|| liMajor != WeaponEnchantmentMajorClass)
				return;

			int liAmount = (liMinor & 7) + 1;

			if ((liMinor & DamageEnchantmentBit) != 0)
				piDamage = liAmount;
			else
				piAccuracy = liAmount;
		}

		/// <param name="piEnchantAccuracy">The weapon's accuracy enchantment (GetWeaponEnchantment).</param>
		/// <param name="piEnchantDamage">The weapon's damage enchantment, on the base damage.</param>
		/// <param name="pIDefender">The struck creature, or UWAttackResolution.ObjectDefender for a
		/// thing (null is taken as a thing).</param>
		/// <param name="piPart">The creature's part the blow's probe picked (pickloc), or
		/// UWAttackResolution.NoPart.</param>
		/// <param name="piTargetObjectId">The struck thing's object id - a door may wear the weapon.</param>
		/// <param name="pbTargetWearsWeapon">The struck creature does not spare a weapon on a bad miss.</param>
		/// <param name="pFWearWeapon">Rolls the dice (count, range) and wears the weapon in the hand;
		/// called between the check and the dice (UWAttackResolution.PlayerBlow), none without it.</param>
		public static MeleeResult ResolveMelee(DataImport pOData, int piWeaponId, UWCombat.AttackKind peKind,
			float pfChargeFraction, int piStrength, int piAttack, int piDexterity, int piUnarmedSkill,
			System.Func<UWPlayerData.Skill, int> pFGetSkill, IUWAttackDefender pIDefender, int piPart,
			bool pbEasyDifficulty = false, int piFlank = 0, int piEnchantAccuracy = 0, int piEnchantDamage = 0,
			int piTargetObjectId = -1, bool pbTargetWearsWeapon = false, System.Action<int, int> pFWearWeapon = null)
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

			lOResult.Accuracy += piEnchantAccuracy;

			// THE FLANKING BONUS (UWCritterRules.GetFlankingBonus, 0 from the front, 4 from
			// behind) goes into the check, not into the score itself (as UWCritterCombat.GetHitScore
			// adds it for a creature), and again into the damage (per user, 2026-09-28: the golem took
			// far more blows in ours than in the original, and ours hit less often).
			lOResult.Flank = piFlank;

			// A BLOW AT A THING ALWAYS LANDS: frp_check (UnderworldExhumed COMBAT.C) answers "hit"
			// for anything that is no creature before any roll - no miss, no critical; the angle
			// bonus still goes into the damage (do_damage). Per user on the original 2026-10-14:
			// a Level 1 character with Attack and Unarmed 0 hit a door with every blow. Until then
			// ours rolled against the thing's defence 0 and could miss with the blocked sound.
			// (UWAttackResolution.Check since cleanup step 4.9, 2026-10-18.)
			//
			// THE CHECK AND THE DAMAGE are the creatures' since cleanup step 4.9 (2026-10-18):
			// do_attack is one routine for every attacker (UWAttackResolution). The weapon's
			// damage, the strength and the enchantment make the base before the minimum
			// (UWCombat.GetRawBaseDamage); the blocked sound takes the kinds of his own row, 0x7F
			// (do_miss), which Interaction read itself until then.
			UWObjectClassProperties.Critter lOOwnRow;

			pOData.ObjectClassProperties.TryGetCritter(UWCritterCombat.PlayerCritterObjectId, out lOOwnRow);

			UWAttackerStats lOAttacker = new UWAttackerStats(UWCritterBrain.PlayerIndex, lOResult.Accuracy,
				UWCombat.GetRawBaseDamage(UWCombat.GetWeaponDamage(lOMelee, peKind), lbUnarmed, piStrength, piUnarmedSkill,
					piEnchantDamage),
				liCharge, piFlank, lOOwnRow.WeaponKind, lOOwnRow.ArmourKind == UWCritterCombat.MetalKind);

			// THE WEAPON WEARS IN frp_check, between the check and the dice (UWAttackResolution.
			// PlayerBlow, cleanup step 4.9b, 2026-10-18, by the user's decision to follow the
			// original; Interaction wore it after this call until then).
			UWAttackOutcome lOOutcome = UWAttackResolution.PlayerBlow(lOAttacker,
				pIDefender ?? UWAttackResolution.ObjectDefender, piPart, piTargetObjectId, pbTargetWearsWeapon, pFWearWeapon);

			lOResult.Outcome = lOOutcome;
			lOResult.Check = lOOutcome.Result;
			lOResult.Hit = lOOutcome.Hit;
			lOResult.Damage = lOOutcome.Damage;
			lOResult.DamageBeforeArmour = lOOutcome.Rolled;

			return lOResult;
		}

		/// <summary>PickBlowTarget: no object in the probe at all - the terrain decides
		/// (resolve_attack's TerrainCheck and find_wall_coll).</summary>
		public const int BlowFoundNothing = -2;

		/// <summary>PickBlowTarget: objects in the probe, none struck (all below or above the blow,
		/// or skipped) - resolve_attack returns without the wall test.</summary>
		public const int BlowStruckNothing = -1;

		/// <summary>
		/// WHAT A BLOW STRIKES, as resolve_attack and set_hitobj (UnderworldExhumed combat/COMBAT.C)
		/// decide it - from the objects' boxes in the level data, not from the drawn bodies. The core
		/// holds the blow's probe (SetCalc: the centre in eighths, z, radius weapon + 1, height,
		/// index 1; Mover the player, a creature, so the probe and every creature's box shrink by
		/// one and switches are skipped). ObjectCheck(0, 1) is ScanForCollisions(false, true), the
		/// touchable objects; process_objlist is SortCollisions. Of those in the blow's height the
		/// one whose position lies nearest the attacker's is struck - traps and the attacker never,
		/// an ally only as the last record when nothing came before. Returns the record index or
		/// BlowFoundNothing / BlowStruckNothing. Per user on the original 2026-10-14 (audit B5): from
		/// one spot the door's frame is struck - a door's box is radius 3 around its position and
		/// reaches far into the next tile - and closer by the goblin round the corner.
		/// </summary>
		public static int PickBlowTarget(UWMotionCore pOCore, int piAttackerX8, int piAttackerY8,
			System.Func<UWMotionBody, bool> pFIsAlly)
		{
			pOCore.ScanForCollisions(false, true);

			if (pOCore.Count == 0)
				return BlowFoundNothing;

			pOCore.SortCollisions();

			int liLast = pOCore.First + pOCore.Overlap;
			int liFound = BlowStruckNothing;
			long llBest = 100000L;
			int liTileX = pOCore.CalcX >> 3;
			int liTileY = pOCore.CalcY >> 3;

			for (int liAt = pOCore.First; liAt < liLast; liAt++)
			{
				UWMotionCore.Record lORecord = pOCore.Records[liAt];
				UWMotionBody lOBody = lORecord.Body;

				if ((lOBody.ItemId >> 6) == UWObjectMechanics.TrapMajorClass || lOBody.Index == pOCore.CalcIndex)
					continue;

				if (lOBody.IsMobile && pFIsAlly != null && pFIsAlly(lOBody) && (liLast - 1 != liAt || llBest != 100000L))
					continue;

				int liDx = piAttackerX8 - ((((liTileX + lORecord.Dx) & 0x3F) << 3) + lOBody.XPos);
				int liDy = piAttackerY8 - ((((liTileY + lORecord.Dy) & 0x3F) << 3) + lOBody.YPos);
				long llDistance = ((long)liDx * liDx) + ((long)liDy * liDy);

				if (llDistance < llBest)
				{
					llBest = llDistance;
					liFound = liAt;
				}
			}

			return liFound;
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
		///
		/// THE BASE ONLY since cleanup step 4.9b (2026-10-18, by the user's decision to follow the
		/// original): the dice are rolled at the hit (UWAttackResolution.Missile, missile_thwack ->
		/// do_damage), with charge 0x80 there; until then this rolled them at the launch. The skill
		/// check stays at the launch - the original makes it in missile_newhit at the hit as well
		/// (UnderworldExhumed motion/OBJPHYS.C:114), but the user's decision keeps the base as the
		/// launch computes it. Without data NoMissileDamage (0 at the hit, as before).
		/// </summary>
		public static int GetMissileBaseDamage(DataImport pOData, int piAmmunitionId, int piMissileSkill)
		{
			if (pOData == null || pOData.ObjectProperties == null)
				return UWAttackResolution.NoMissileDamage;

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

			return (liBase * liFactor) >> 8;
		}

		/// <summary>The ammunition byte of the missile table (OBJECTS.DAT 0x82 + 3n + 2, signed)
		/// that marks a piece of ammunition: -64.</summary>
		public const int AmmunitionMarker = -64;

		/// <summary>The missile table's signed ammunition byte of a class 1 object (0x10-0x1F).</summary>
		public static int GetMissileAmmoByte(DataImport pOData, int piObjectId)
		{
			if (pOData == null || pOData.ObjectProperties == null)
				return AmmunitionMarker;

			return (sbyte)pOData.ObjectProperties.GetRangedType(piObjectId);
		}

		/// <summary>The damage type a class 1 object hits with: -ammo as a byte (missile_newhit,
		/// UnderworldExhumed motion/OBJPHYS.C) - 0x40 for ammunition, the launcher's own odd
		/// value for a thrown bow, sling or crossbow.</summary>
		public static int GetMissileDamageType(DataImport pOData, int piObjectId)
		{
			return (-GetMissileAmmoByte(pOData, piObjectId)) & 0xFF;
		}

		/// <summary>
		/// The damage of a class 1 object the PLAYER THROWS by hand. missile_newhit
		/// (UnderworldExhumed motion/OBJPHYS.C) scales by the Missile skill only when the table
		/// marks the object as ammunition (ammo byte -64); a thrown bow, sling or crossbow does
		/// its table damage unscaled (2026-10-12; until then every 0x10-0x1F was scaled). The base
		/// only since cleanup step 4.9b (2026-10-18): the dice are rolled at the hit, see
		/// GetMissileBaseDamage.
		/// </summary>
		public static int GetThrownMissileBaseDamage(DataImport pOData, int piObjectId, int piMissileSkill)
		{
			if (pOData == null || pOData.ObjectProperties == null)
				return UWAttackResolution.NoMissileDamage;

			if (GetMissileAmmoByte(pOData, piObjectId) == AmmunitionMarker)
				return GetMissileBaseDamage(pOData, piObjectId, piMissileSkill);

			return pOData.ObjectProperties.GetRangedDamage(piObjectId);
		}
	}
}
