namespace UWDataImport.UWData
{
	/// <summary>
	/// The execution of a creature's blow, missile, poison, death and remains - the arithmetic
	/// of NPCExecuteAttack_seg022_15DE (83437), CalculateAttackResults_seg022_230E_6B9 (80688),
	/// AttackerAppliesFinalDamage_seg022_8A5 (81050), MissileAttackHit_seg022_14BF (83234),
	/// AwardKillEXP_seg022_1725 (83677) and DropNPCRemains_seg006_5 (40801), without any engine
	/// dependency (deviation rows 45-51 of Docs/AI/creature-ai.md, spec 8.3-8.7).
	///
	/// The host (UWCritter) does what needs the world - the reach scan for the defender, the
	/// heights for the body part, the sounds and the effects. The chain below runs in
	/// UWAttackResolution since cleanup step 4.9 (2026-10-18), for the creature's blow, the
	/// player's melee and a missile on the player alike, in the original's order:
	///
	///   score, damage      GetAttackScore, GetAttackDamage, the strong bonuses
	///   to-hit             GetHitScore into UWSkillCheck.GetResult (2 crit, 1 hit, 0 miss, -1)
	///   critical           GetCriticalMultiplier on the base damage, before the dice
	///   dice               RollDamage: D = max(2, base), (D / 6)d6 + 1d(D % 6)
	///   charge, flank      ScaleByCharge: rolled * charge / 128 + flank
	///   sound              with THIS value, before the armour (label 938)
	///   armour             GetCreatureArmour or the player's slot, SubtractArmour
	///   difficulty         HalveOnEasy for the player as defender (label A28)
	///   aftermath          GetEffectSize, GetHitZ, GetHitEffectObjectId
	///
	/// Every roll a self-check wants to script is a parameter; the overloads without one
	/// draw from UWRandom.
	/// </summary>
	public static class UWCritterCombat
	{
		/// <summary>The sound of a blocked blow, metal on metal (do_miss).</summary>
		public const int BlockedMetalSound = 7;

		/// <summary>The sound of every other blocked blow (do_miss).</summary>
		public const int BlockedSound = 8;

		/// <summary>The kind that counts as metal, for a weapon and for armour.</summary>
		public const int MetalKind = 1;

		/// <summary>The player's own creature row (the adventurer, item 0x7F): do_miss takes the
		/// weapon kind of HIS blocked blows from this row, whatever he wields.</summary>
		public const int PlayerCritterObjectId = UWObjectIds.Adventurer;

		/// <summary>
		/// THE SOUND OF A BLOCKED BLOW (do_miss, UnderworldExhumed COMBAT.C, read 2026-10-11; the
		/// Exhumed audit's row 14): a blow that reached its target but failed the check plays at
		/// the TARGET - effect 7 when a metal weapon meets metal armour, else 8. The weapon kind is
		/// the attacker's row (the player's always his own row, 0x7F); the armour kind, for the
		/// player as target the piece on the struck part (none or leather is not metal), for a
		/// creature as target the ATTACKER's row - a slip of the original, kept. A swing at nothing
		/// stays effect 0xA at the attacker. Until 2026-10-11 ours played 0xA for both.
		/// </summary>
		public static int GetBlockedSound(int piWeaponKind, bool pbArmourMetal)
		{
			return piWeaponKind == MetalKind && pbArmourMetal ? BlockedMetalSound : BlockedSound;
		}

		/// <summary>The leather pieces, which do_miss does not count as metal: vest, leggings,
		/// gloves, boots, cap.</summary>
		public static bool IsLeatherArmour(int piObjectId)
		{
			return piObjectId == 0x20 || piObjectId == 0x23 || piObjectId == 0x26 || piObjectId == 0x29 || piObjectId == 0x2C;
		}

		// ------------------------------------------------- NPCExecuteAttack (83437)

		/// <summary>Row byte 0x13 + 3i for attack i.</summary>
		private const int AttackToHitOffset = 0x13;

		/// <summary>Row byte 0x14 + 3i for attack i.</summary>
		private const int AttackDamageOffset = 0x14;

		/// <summary>Row byte 5, a fifth of it goes into the damage.</summary>
		private const int StrengthOffset = 5;

		public const int AttackStride = 3;

		/// <summary>Three attacks per row; an attack number outside 0..2 is held to that range.</summary>
		private const int AttackCount = 3;

		/// <summary>AttackScore = (signed) row[0x13 + 3i] + (signed) row[0x11] &gt;&gt; 1 (labels
		/// 1656-1679: cbw, sar 1 - a negative base rounds towards minus infinity).</summary>
		public static int GetAttackScore(UWObjectClassProperties.Critter pORow, int piAttack)
		{
			int liToHit = (sbyte)pORow.RowByte(AttackToHitOffset + (AttackStride * UWMath.Clamp(piAttack, 0, AttackCount - 1)));

			return liToHit + (pORow.AttackScoreBase >> 1);
		}

		/// <summary>AttackDamage = row[0x14 + 3i] + row[5] / 5 (labels 1629-1648), both
		/// unsigned.</summary>
		public static int GetAttackDamage(UWObjectClassProperties.Critter pORow, int piAttack)
		{
			return pORow.RowByte(AttackDamageOffset + (AttackStride * UWMath.Clamp(piAttack, 0, AttackCount - 1)))
				+ (pORow.RowByte(StrengthOffset) / 5);
		}

		/// <summary>A strong individual (word 0x0D bit 10) adds 7 + RNG(6) to the score
		/// (label 1679).</summary>
		public static int GetStrongScoreBonus(int piRoll6)
		{
			UWRulesCritter lORules = UWRules.Current.Critter;

			return lORules.Strong.ToHitBase + fClampRoll(piRoll6, lORules.Strong.ToHitRoll);
		}

		public static int GetStrongScoreBonus()
		{
			return GetStrongScoreBonus(UWRandom.Next(UWRules.Current.Critter.Strong.ToHitRoll));
		}

		/// <summary>... and 4 + RNG(12) to the damage (label 1679).</summary>
		public static int GetStrongDamageBonus(int piRoll12)
		{
			UWRulesCritter lORules = UWRules.Current.Critter;

			return lORules.Strong.DamageBase + fClampRoll(piRoll12, lORules.Strong.DamageRoll);
		}

		public static int GetStrongDamageBonus()
		{
			return GetStrongDamageBonus(UWRandom.Next(UWRules.Current.Critter.Strong.DamageRoll));
		}

		// ------------------------------------------------- CalculateAttackResults (80688)

		/// <summary>
		/// The value SkillCheck_seg037_32E6_C (123242) bands: score - protection + flank -
		/// defence + RNG(31). The protection is the player's enchantment of the part hit
		/// (label 76F, zero for a creature defender), the defence the defender's row byte 0x12
		/// (label 797). Hand the result to UWSkillCheck.GetResult: above 28 a critical hit,
		/// above 15 a hit, above 2 a miss, else a critical miss.
		/// </summary>
		public static int GetHitScore(int piAttackScore, int piFlank, int piProtection, int piDefence, int piRoll31)
		{
			return piAttackScore - piProtection + piFlank - piDefence
				+ fClampRoll(piRoll31, UWRules.Current.Combat.SkillCheck.RollRange);
		}

		/// <summary>The roll's range is the rule set's (combat.skillCheck.rollRange; UWCritterRules.
		/// SkillCheckRoll is its default).</summary>
		public static UWSkillCheck.ResultEnum ResolveHit(int piAttackScore, int piFlank, int piProtection, int piDefence)
		{
			return UWSkillCheck.GetResult(GetHitScore(piAttackScore, piFlank, piProtection, piDefence,
				UWRandom.Next(UWRules.Current.Combat.SkillCheck.RollRange)));
		}

		/// <summary>A critical hit multiplies the base damage BEFORE the dice by
		/// ((RNG &amp; 31) + 48) &gt;&gt; 5: 1 for a low nibble below 16, else 2 (label 7AE).</summary>
		public static int GetCriticalMultiplier(int piRng)
		{
			return ((piRng & 31) + 48) >> 5;
		}

		public static int GetCriticalMultiplier()
		{
			return GetCriticalMultiplier(UWRandom.Next(32));
		}

		/// <summary>The red flash the player sees on a critical hit against him (label 7CB,
		/// j_FlashColour_ovr114_4D6).</summary>
		public const int CriticalFlashColour = 0xB8;

		/// <summary>
		/// The inventory slot of the player's piece a critical hit against him damages (labels
		/// 7D3-81B, the original's slot numbering: 0 helmet, 1 chest, 2 gloves, 3 legs, 4
		/// boots, 7/8 the hands): (part + 1) &amp; 3; 3 becomes boots 1 in 5; 1 and 2 become the
		/// OFF hand, 7 + the handedness bit (bit 0 of PLAYER.DAT 0x64, set for right-handed).
		/// The routine behind it is ovr120_C0F with MODE 1, the armour test (the call pushes 1, 1,
		/// 4 - corrected 2026-09-28, this said mode 0 and "armour never suffers"): helmet, leggings
		/// and boots take it, the shield hand only with a shield in it - see UWEquipmentWear.
		/// </summary>
		public static int GetCriticalEquipmentSlot(int piPart, bool pbRightHanded, int piRoll5)
		{
			int liSlot = (piPart + 1) & 3;

			if (liSlot == 3)
				return fClampRoll(piRoll5, 5) == 0 ? 4 : 3;

			if (liSlot == 1 || liSlot == 2)
				return 7 + (pbRightHanded ? 1 : 0);

			return 0;
		}

		// ------------------------------------------------- AttackerAppliesFinalDamage (81050)

		/// <summary>The dice: D = max(2, base) (label 8C9), (D / 6) six-sided dice plus one
		/// die of D % 6 sides (labels 8FC, 910, DiceRoll_seg041_11E: count + sum of RNG(sides)).
		/// Bounds: D / 6 + (D % 6 &gt; 0 ? 1 : 0) .. D.</summary>
		public static int RollDamage(int piAttackDamage)
		{
			int liMinimum = UWRules.Current.Combat.Damage.MinimumBase;
			int liBase = piAttackDamage < liMinimum ? liMinimum : piAttackDamage;

			return UWCombat.RollDamage(liBase);
		}

		/// <summary>The lowest and highest value RollDamage can return for a base value - for
		/// the self-check and the F1 overlay.</summary>
		public static void GetDamageBounds(int piAttackDamage, out int piMinimum, out int piMaximum)
		{
			int liMinimumBase = UWRules.Current.Combat.Damage.MinimumBase;
			int liBase = piAttackDamage < liMinimumBase ? liMinimumBase : piAttackDamage;

			piMinimum = (liBase / 6) + (liBase % 6 != 0 ? 1 : 0);
			piMaximum = liBase;
		}

		/// <summary>rolled * charge &gt;&gt; 7 plus the flanking bonus (labels 921-930). The
		/// charge is the table VALUE (UWCritterRules.ChargeTable, 50..255), 0x80 for a
		/// missile.</summary>
		public static int ScaleByCharge(int piRolled, int piCharge, int piFlank)
		{
			return ((piRolled * (piCharge & 0xFF)) >> 7) + piFlank;
		}

		/// <summary>
		/// The armour of a creature defender (labels 990-A02): row byte [part % 4], a byte of
		/// 0xFF falls back to byte 0 (Critter.ArmourOfPart); a strong defender's is a * 5 / 3.
		/// The player's armour is his equipment (UWCharacter.GetArmourAt) and is never scaled.
		/// </summary>
		public static int GetCreatureArmour(UWObjectClassProperties.Critter pORow, int piPart, bool pbStrongDefender)
		{
			UWRulesCritter lORules = UWRules.Current.Critter;

			int liArmour = pORow.ArmourOfPart(piPart);

			return pbStrongDefender
				? liArmour * lORules.Strong.ArmourNumerator / lORules.Strong.ArmourDenominator
				: liArmour;
		}

		/// <summary>a &gt;= damage leaves 0, else damage - a (labels A04-A10). No minimum
		/// damage.</summary>
		public static int SubtractArmour(int piDamage, int piArmour)
		{
			return piArmour >= piDamage ? 0 : piDamage - piArmour;
		}

		/// <summary>The easy difficulty (PLAYER.DAT 0xB4 != 0) halves the damage to the player
		/// (label A28, sar 1) - after the armour, before DamageObject. The halving can be switched
		/// off in the rule set (critter.easyHalvesDamage, cleanup phase 10); the shift stays.</summary>
		public static int HalveOnEasy(int piDamage, bool pbEasy)
		{
			return pbEasy && UWRules.Current.Critter.EasyHalvesDamage ? piDamage >> 1 : piDamage;
		}

		/// <summary>The original halves on easy - the rule's default.</summary>
		public const bool EasyHalvesDamage = true;

		/// <summary>di = min(3, damage / 4) (labels A15-A25): the screen shake level for the
		/// player and the start frame of the blood or flash animation.</summary>
		public static int GetEffectSize(int piDamage)
		{
			int liSize = piDamage / 4;

			return liSize > 3 ? 3 : liSize;
		}

		/// <summary>HitZ (dseg 0x246, label B5B): the height of the effect on the defender's
		/// body per part, in eighths of its COMOBJ height (SpawnClass7Object 142838 multiplies
		/// it by height &gt;&gt; 3): body 5, hands 3, legs 1, head 7, a missile part (4) 0.</summary>
		public static readonly int[] HitZ = { 5, 3, 1, 7, 0 };

		public static int GetHitZ(int piPart)
		{
			return piPart >= 0 && piPart < HitZ.Length ? HitZ[piPart] : 0;
		}

		/// <summary>The effect a damaging hit leaves on a creature (labels B49-C17): table byte
		/// 8 bits 3-4 nonzero the blood splat, class 7 offset 0 (object 0x1C0 = 448, "some_blood";
		/// the spec's "offset 1" was the animation duration argument next to it), else the
		/// impact flash, class 7 offset 0x0B (459). Both start at frame GetEffectSize.</summary>
		public static int GetHitEffectObjectId(UWObjectClassProperties.Critter pORow)
		{
			return pORow.BleedsOnHit ? UWObjectMechanics.BloodEffectObjectId : UWObjectMechanics.FlashEffectObjectId;
		}

		// ------------------------------------------------- MissileAttackHit (83234)

		/// <summary>A missile's body part is PickBodyHitPoint + 4 (label 158B): the same
		/// armour byte (part % 4), HitZ 0 and no enchantment protection.</summary>
		public static int GetMissileBodyPart(int piPart)
		{
			return (piPart & 3) + MissilePartOffset;
		}

		/// <summary>
		/// THE BLOW'S PROBE IN HEIGHT (resolve_attack, UnderworldExhumed combat/COMBAT.C): its
		/// bottom is the attacker's z plus height * (ninth / 3) / 3, its height (reach * 2 + 1) * 4.
		/// pickloc picks the defender's part against THIS, not the attacker's body (C3, by the
		/// user's decision of 2026-10-12). The player adds his pitch (Interaction).
		/// </summary>
		public static void GetBlowProbe(int piAttackerFeet, int piAttackerHeight, int piNinth, int piReach,
			out int piBottom, out int piTop)
		{
			piBottom = piAttackerFeet + (piAttackerHeight * (piNinth / 3) / 3);
			piTop = piBottom + (((piReach * 2) + 1) * 4);
		}

		/// <summary>A creature's blow: wsize 2 (critter_attack) and a ninth of rand() % 9 (AI.C), so
		/// the probe sits at a random third of its body.</summary>
		public const int CritterBlowReach = 2;

		public const int CritterBlowNinths = 9;

		/// <summary>The probe of one creature blow, the ninth rolled on UWRandom.</summary>
		public static void RollCritterBlowProbe(int piFeet, int piHeight, out int piBottom, out int piTop)
		{
			GetBlowProbe(piFeet, piHeight, UWRandom.Next(0, CritterBlowNinths), CritterBlowReach, out piBottom, out piTop);
		}

		/// <summary>What missile_thwack adds to pickloc's part.</summary>
		public const int MissilePartOffset = 4;

		/// <summary>
		/// The part whose height the blood or flash takes: do_damage (UnderworldExhumed
		/// combat/COMBAT.C) sets hitloc &amp;= 4 when the row's armour byte of the part is 0xFF -
		/// part 0, the body, for a blow; the missile's own height (4) for a missile.
		/// </summary>
		public static int GetEffectPart(UWObjectClassProperties.Critter pORow, int piPart)
		{
			return pORow.RowByte(piPart & 3) == NoArmourByte ? piPart & MissilePartOffset : piPart;
		}

		/// <summary>An armour byte of 0xFF: the part has none of its own and falls back to byte 0.</summary>
		public const int NoArmourByte = 0xFF;

		/// <summary>The damage of a creature's missile: the ammunition table's byte 0 through
		/// the dice with charge 0x80 and flank 0 - no skill roll, the physics decides the hit.
		/// Every missile's dice, rolled at its hit since cleanup step 4.9b (2026-10-18;
		/// UWAttackResolution.RollMissile).</summary>
		public static int RollMissileDamage(int piAmmoDamage)
		{
			return ScaleByCharge(RollDamage(piAmmoDamage), UWRules.Current.Critter.Missile.Charge, 0);
		}

		// ------------------------------------------------- Poison (NPCExecuteAttack 16A4-16EC)

		/// <summary>
		/// After a hit on the player (even one the armour swallowed): his poison nibble
		/// (PLAYER.DAT 0x5F bits 2-5) must be below the row's byte 0x0F and
		/// ScaleDamageAgainstObject(player, 1, 0x10) nonzero - a Poison Resistance rules it
		/// out. No roll, no armour test.
		/// </summary>
		public static bool ShouldPoison(int piCurrentNibble, int piPoisonByte, int piDamageTypeProof)
		{
			if (piCurrentNibble >= piPoisonByte)
				return false;

			return UWDamageTypes.Scale(piDamageTypeProof, 1, UWCritterRules.PoisonResistanceType) != 0;
		}

		/// <summary>What is written: byte 0x0F &amp; 0xF (label 16E1).</summary>
		public static int GetPoisonNibble(int piPoisonByte)
		{
			return piPoisonByte & 0xF;
		}

		// ------------------------------------------------- AwardKillEXP (83677)

		/// <summary>
		/// exp = 4 * base + 2d(base) (row word 0x28, DiceRoll(2, base) = 2 + two RNG(base)),
		/// times (24 + RNG(24)) / 16 for a strong individual; then EXPChange_seg037_48 as it
		/// is, with no halving. piStrongRoll below 0 means not strong.
		/// </summary>
		public static int GetKillExperience(int piBase, int piTwoDice, int piStrongRoll)
		{
			UWRulesCritter lORules = UWRules.Current.Critter;

			if (piBase <= 0)
				return 0;

			int liExperience = (UWRules.Current.Experience.Kill.Factor * piBase) + piTwoDice;

			if (piStrongRoll >= 0)
				liExperience = liExperience * (lORules.Strong.ExperienceBase + fClampRoll(piStrongRoll, lORules.Strong.ExperienceRoll))
					/ lORules.Strong.ExperienceDivisor;

			return liExperience;
		}

		public static int GetKillExperience(int piBase, bool pbStrong)
		{
			if (piBase <= 0)
				return 0;

			return GetKillExperience(piBase, UWRandom.RollDice(2, piBase),
				pbStrong ? UWRandom.Next(UWRules.Current.Critter.Strong.ExperienceRoll) : -1);
		}

		// ------------------------------------------------- DropNPCRemains (40801)

		/// <summary>The blood object 0xD8 + (table byte 8 &gt;&gt; 5 &amp; 7) at the creature's
		/// position, when nonzero (40872). -1 for none.</summary>
		public static int GetBloodObjectId(int piBloodIndex)
		{
			return piBloodIndex <= 0 ? -1 : UWObjectMechanics.FirstRemainsObjectId + piBloodIndex;
		}

		/// <summary>The corpse 0xC0 + (table byte 0x0A &gt;&gt; 2 &amp; 7) when nonzero AND
		/// RNG(16) &lt; 7 (41019-41024).</summary>
		public static bool ShouldDropCorpse(int piCorpseIndex, int piRoll16)
		{
			UWRulesCritter lORules = UWRules.Current.Critter;

			return piCorpseIndex > 0 && fClampRoll(piRoll16, lORules.Corpse.Roll) < lORules.Corpse.Chance;
		}

		public static int GetCorpseObjectId(int piCorpseIndex)
		{
			return piCorpseIndex <= 0 ? -1 : UWObjectMechanics.FirstCorpseObjectId + piCorpseIndex;
		}

		/// <summary>The corpse's OWNER carries the kind: item id &amp; 0x3F into word 3 bits 0-5
		/// (41041-41050; build_corpse in PATHFIND.C, remains->ol.f.owner = OBJ_INMAJOR(obj)) -
		/// BonesLook names the creature from it ("It looks to be that of a goblin."). The quality
		/// stays CreateObj's 40. Until 2026-10-13 ours wrote the kind into the quality and left the
		/// owner 0, so the bones of a kill said nothing in the inventory.</summary>
		public static int GetCorpseOwner(int piCritterItemId)
		{
			return piCritterItemId & 0x3F;
		}

		// ------------------------------------------------- helpers

		/// <summary>A scripted roll outside 0..n-1 is taken modulo n, as the fake host does.</summary>
		private static int fClampRoll(int piRoll, int piN)
		{
			if (piN <= 0)
				return 0;

			int liRoll = piRoll % piN;

			return liRoll < 0 ? liRoll + piN : liRoll;
		}
	}
}
