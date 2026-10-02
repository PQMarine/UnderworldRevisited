namespace UWDataImport.UWData
{
	/// <summary>
	/// Hit effect and condition description - the arithmetic side of combat, without
	/// any Unity dependency.
	///
	/// WHAT COMES FROM THE DATA: the damage values of the weapons per attack kind, the vitality
	/// and armour of the creatures (both UWObjectClassProperties) and the condition words
	/// (string block 5).
	///
	/// THE FORMULA that turns these into a hit is not in uw-formats.txt: it describes the
	/// tables, but nowhere how the original calculates. It is bundled in one place so that it
	/// can be replaced. The first version here was invented; base value, critical factor,
	/// dice roll and charge now follow the reference (combat.cs), see the members below.
	/// </summary>
	public static class UWCombat
	{
		/// <summary>The original's three attack kinds. The table in OBJECTS.DAT lists
		/// exactly these three damage values for every weapon.</summary>
		public enum AttackKind
		{
			Slash,
			Bash,
			Stab
		}

		/// <summary>Skill type of the fist in the melee table (3 sword, 4 axe,
		/// 5 mace, 6 unarmed). Safer than comparing the object id, because a
		/// weapon without a table entry falls back to the fist.</summary>
		public const int UnarmedSkillType = 6;

		/// <summary>Object id of the fist - the table lists it as a full weapon, so
		/// there is no special case for unarmed attacks.</summary>
		public const int FistObjectId = 15;

		/// <summary>At this charge value the dice roll stays unchanged - in the original the damage is
		/// multiplied by the charge and divided by 128.</summary>
		public const int NeutralCharge = 128;

		/// <summary>
		/// What a CREATURE strikes with. It does not wind up like the player, but has a
		/// counter that rises with every exchange of blows - the table comes from the reference
		/// (combat_globals.NPCSwingCharges).
		///
		/// A CREATURE THAT HAS JUST STARTED ATTACKING STRIKES WEAKLY: the counter starts at zero,
		/// that is 0x32 of 128, so thirty-nine percent of the rolled damage. Only
		/// one that keeps at it longer reaches the 0xFF at the end - almost double.
		///
		/// WE WERE MISSING THIS ENTIRELY. We gave every creature NeutralCharge, so a full
		/// 128 from the start - the first hits were thus about two and a half times as hard as
		/// in the original. It was noticed because the user's character in the original
		/// gets no screen shake from melee hits even WITHOUT armour, but in ours it
		/// did (2026-09-09) - the shake kicks in from four points of damage.
		/// </summary>
		public static readonly int[] NpcSwingCharges =
		{
			0x32, 0x3C, 0x46, 0x50, 0x5A, 0x64, 0x6E, 0x78,
			0x82, 0x8C, 0x9B, 0xAA, 0xB9, 0xCD, 0xE6, 0xFF
		};

		/// <summary>The charge value for a counter state, with range limits.</summary>
		public static int GetNpcCharge(int piIndex)
		{
			if (piIndex < 0)
				piIndex = 0;

			if (piIndex >= NpcSwingCharges.Length)
				piIndex = NpcSwingCharges.Length - 1;

			return NpcSwingCharges[piIndex];
		}

		/// <summary>Highest state of the counter.</summary>
		public const int MaxNpcSwingChargeIndex = 0xF;

		/// <summary>Maximum of the charge counter while winding up. MinCharge also lies on this
		/// scale, the threshold from which a strike is possible at all.</summary>
		public const int FullCharge = 100;

		/// <summary>Below this base value it is rounded up before rolling.</summary>
		public const int MinimumBaseDamage = 2;

		/// <summary>Damage value of a weapon for an attack kind, directly from the
		/// melee table.</summary>
		public static int GetWeaponDamage(UWObjectClassProperties.MeleeWeapon pOWeapon, AttackKind peKind)
		{
			switch (peKind)
			{
				case AttackKind.Slash:
					return pOWeapon.SlashDamage;

				case AttackKind.Bash:
					return pOWeapon.BashDamage;

				default:
					return pOWeapon.StabDamage;
			}
		}

		/// <summary>
		/// Which weapon animation belongs to a melee weapon. The same source as for the
		/// skill: the SkillType field of the melee table.
		/// </summary>
		public static WeaponTypes GetWeaponTypeForWeapon(UWObjectClassProperties.MeleeWeapon pOWeapon, bool pbLeftHanded = false)
		{
			WeaponTypes leType;

			switch (pOWeapon.SkillType)
			{
				case 3: leType = WeaponTypes.RightHandSword; break;
				case 4: leType = WeaponTypes.RightHandAxe; break;
				case 5: leType = WeaponTypes.RightHandMace; break;
				default: leType = WeaponTypes.RightHandFist; break;
			}

			// A left-hander uses the second half of WEAPONS.GR, the same four weapons in the left
			// hand (the reference adds 28 animations for isLefty, combat.WeaponAnimHandednessOffset).
			return pbLeftHanded ? leType + (WeaponTypes.LeftHandSword - WeaponTypes.RightHandSword) : leType;
		}

		/// <summary>
		/// Which skill belongs to a melee weapon. The weapon says so itself: according to
		/// uw-formats.txt the SkillType field of the melee table holds 3 for sword,
		/// 4 for axe, 5 for mace and 6 for unarmed.
		/// </summary>
		public static UWPlayerData.Skill GetSkillForWeapon(UWObjectClassProperties.MeleeWeapon pOWeapon)
		{
			switch (pOWeapon.SkillType)
			{
				case 3: return UWPlayerData.Skill.Sword;
				case 4: return UWPlayerData.Skill.Axe;
				case 5: return UWPlayerData.Skill.Mace;
				default: return UWPlayerData.Skill.Unarmed;
			}
		}

		/// <summary>
		/// Damage of a hit.
		///
		/// WHAT COMES FROM THE DATA: the value from the weapon table is explicitly called
		/// "damage modifier for Slash/Bash/Stab attack" there - a BONUS, not damage. And the
		/// armour is subtracted ("armor: amount of damage subtracted from attacks"). Both
		/// are stated like this in uw-formats.txt.
		///
		/// WHAT THE BONUS IS ADDED TO is now the reference's base value (see GetBaseDamage).
		/// An earlier version fitted it to two observations in the original
		/// (user, 2026-08-30, mage with strength 17, unarmed):
		///
		///   Rat    (8 vitality, armour 0)  falls after ONE blow
		///   Goblin (27 vitality, armour 2) falls after FIVE blows
		///
		/// On top of that, a fighter with strength 25 felled the same goblin in THREE blows.
		///
		/// With base value = strength / 2.8 both worked out:
		///   Mage     17/2.8 = 6.1 + 2 =  8 damage; rat falls at once,
		///                                          goblin 27/(8-2) = 4.5 -> 5 blows
		///   Fighter  25/2.8 = 8.9 + 2 = 11 damage; goblin 27/(11-2) = 3 -> 3 blows
		///
		/// (STRENGTH + ATTACK) / 4 FITTED EQUALLY WELL: the mage has attack 6, the
		/// fighter 10, and that gives the same 8 and 11. The two formulas could not be told
		/// apart with the numbers at hand.
		///
		/// The WEAPON SKILL does NOT go into the damage. A mage with low
		/// unarmed skill still reaches the 8, so it cannot have a large share there;
		/// but he missed very often, which points to a hit roll. That is where
		/// the skill belongs (the hit check before this call, see UWSkillCheck).
		///
		/// The first approach treated the modifier as the whole damage. That gave a
		/// fist with 2 damage against a goblin armour of 2 - so the minimum damage 1 and
		/// 27 blows per goblin.
		/// </summary>
		public static int ComputeDamage(int piWeaponDamage, bool pbUnarmed, int piStrength,
			int piUnarmedSkill, int piCharge, int piTargetArmour, bool pbCritical = false)
		{
			return ComputeDamage(piWeaponDamage, pbUnarmed, piStrength, piUnarmedSkill, piCharge,
				piTargetArmour, out int liUnused, pbCritical);
		}

		/// <summary>
		/// The same, and it also hands out the damage BEFORE the armour. That is what the hit
		/// SOUND is played with: AttackerAppliesFinalDamage_seg022_8A5 plays effect 3 or 4 with
		/// the volume offset at label 938, four lines before it subtracts the armour at labels
		/// A04 to A10. So a blow the armour swallows entirely is still heard at full volume.
		/// </summary>
		public static int ComputeDamage(int piWeaponDamage, bool pbUnarmed, int piStrength,
			int piUnarmedSkill, int piCharge, int piTargetArmour, out int piBeforeArmour,
			bool pbCritical = false, int piFlank = 0)
		{
			int liBase = GetBaseDamage(piWeaponDamage, pbUnarmed, piStrength, piUnarmedSkill);

			// A critical hit multiplies the base value before rolling. The
			// factor is 1 or 2 - (48 + (RNG & 31)) divided by 32 (the critical branch of the
			// attack results; the reference rolled 0 to 29), so in half the cases
			// double damage and otherwise no bonus at all.
			if (pbCritical)
				liBase *= (48 + UWRandom.Next(0, 32)) >> 5;

			int liRolled = RollDamage(liBase);

			// The FLANKING BONUS is added before the armour (AttackerAppliesFinalDamage_seg022_8A5):
			// ExecuteAttack computes it for every melee blow, the player's too; a
			// missile has none.
			piBeforeArmour = ((liRolled * piCharge) >> 7) + piFlank;

			// Armour is subtracted and can swallow the hit completely. In the original
			// there is no minimum damage for this.
			return piBeforeArmour <= piTargetArmour ? 0 : piBeforeArmour - piTargetArmour;
		}

		/// <summary>
		/// The base value from which the roll is made.
		///
		/// Unarmed, the unarmed skill counts, armed it does not - there the
		/// weapon carries the main part and strength only a one-ninth bonus (the reference:
		/// combat.cs). Unarmed: 4 + strength / 6 + two fifths of the unarmed skill. That
		/// explains the user's observation that a mage with skill 0 knocks down an opponent just
		/// as well as a fighter: the weapon skill lies in the hit roll, not here.
		/// </summary>
		public static int GetBaseDamage(int piWeaponDamage, bool pbUnarmed, int piStrength, int piUnarmedSkill)
		{
			int liBase = pbUnarmed
				? 4 + (piStrength / 6) + ((piUnarmedSkill * 2) / 5)
				: (piStrength / 9) + piWeaponDamage;

			return liBase < MinimumBaseDamage ? MinimumBaseDamage : liBase;
		}

		/// <summary>
		/// The dice roll over the base value: as many six-sided dice as six fits
		/// into it, plus one die for the remainder. Base value 8 thus becomes 1d6 + 1d2.
		///
		/// That is the spread the user took for a hidden crit chance while testing
		/// - it is simply dice. Critical hits do exist, but only on a critical hit check
		/// (see ComputeDamage).
		/// </summary>
		public static int RollDamage(int piBaseDamage)
		{
			int liDice = piBaseDamage / 6;
			int liRest = piBaseDamage % 6;

			int liTotal = 0;

			for (int liRoll = 0; liRoll < liDice; liRoll++)
				liTotal += UWRandom.Next(1, 7);

			if (liRest != 0)
				liTotal += UWRandom.Next(1, liRest + 1);

			return liTotal;
		}

		/// <summary>
		/// Converts the charge bar to the weapon's damage factor.
		///
		/// While winding up, the original counts a value from 0 to 100 and on striking
		/// maps it onto the range between the weapon's MinCharge and MaxCharge. Only
		/// this value multiplies the damage. MinCharge is thus two things: the threshold
		/// from which a strike happens at all, and the lower edge of the damage factor.
		/// </summary>
		public static int MapCharge(int piMinCharge, int piMaxCharge, float pfChargeFraction)
		{
			float lfFraction = pfChargeFraction < 0f ? 0f : (pfChargeFraction > 1f ? 1f : pfChargeFraction);

			return piMinCharge + (int)((piMaxCharge - piMinCharge) * lfFraction);
		}

		/// <summary>
		/// Index in string block 5 for the condition of an object.
		///
		/// The block is built in groups of six, ascending from broken to intact.
		///
		/// Which group applies is given by the QUALITY TYPE from byte 10 of COMOBJ.DAT - not the
		/// quality class from byte 6, as stated here until 2026-09-01. Examples verified
		/// against the data: weapons and shields have type 1 and thus ruined, badly
		/// worn, worn, servicable, excellent. Doors have type 0 and thus broken, badly
		/// damaged, damaged, sturdy, massive - exactly the words the user saw on the door
		/// in the original. A torch has type 10 and burns down from unused to burned
		/// out, a sack has type 15 and thus no condition at all.
		///
		/// With the class, door 321 would have ended up at ruined and worn, and the torch too.
		///
		/// HOW THE QUALITY MAPS ONTO THE SIX STEPS was stated here until 2026-09-10 as a
		/// linear assumption (quality times six divided by 64). That was wrong. The reference
		/// calculates with the upper nibble (look.GetDescriptionString):
		///
		///     Quality 0          step 0
		///     Quality 1          step 1 - except for a light source, which stays at 0
		///     Quality 2 and up   step 1 + (quality >> 4), so 1 to 4
		///
		/// Nothing ordinary reaches the fifth step: it is reserved for quality class 3,
		/// which always shows the top word. In most groups slots four and five hold
		/// the same words anyway ("full" and "full"), which is why this is
		/// hardly noticeable.
		///
		/// NOTICED ON A LANTERN: Henrietta's burning lantern in SAVE3 has quality
		/// 21. The original calls it "half full", our linear calculation turned it into
		/// "nearly empty" (per user, 2026-09-10). With the reference's calculation it is
		/// 1 + (21 >> 4) = 2, and slot 2 of the lantern group is exactly "half full".
		///
		/// THE EXCEPTION FOR LIGHT SOURCES is checked by the reference with "(object number &amp; 0x1F8)
		/// equals 0x90" - that is exactly the eight numbers 144 to 151, the four
		/// light sources unlit and lit. A torch with its last remainder
		/// is thus called "burned out" and not "nearly spent".
		/// </summary>
		/// <param name="piQualityClass">From byte 6 of COMOBJ.DAT. Class 3 always shows the
		/// top word.</param>
		/// <param name="pbIsLightSource">Object number 144 to 151 - see above.</param>
		public static int GetConditionStringIndex(int piQualityType, int piQuality,
			int piQualityClass = 0, bool pbIsLightSource = false)
		{
			int liStep = 0;

			if (piQuality == 1)
			{
				if (!pbIsLightSource)
					liStep = 1;
			}
			else if (piQuality > 1)
			{
				liStep = piQualityClass == AlwaysBestQualityClass ? 5 : 1 + (piQuality >> 4);
			}

			if (liStep > 5)
				liStep = 5;

			return (piQualityType * 6) + liStep;
		}

		/// <summary>This quality class always shows the top condition word. In uw1
		/// 142 of the 461 objects carry it.</summary>
		public const int AlwaysBestQualityClass = 3;

		/// <summary>Quality level 0 to 63 from a health fraction - the inverse of
		/// GetConditionStringIndex, so that a damaged door reports its condition.</summary>
		public static int GetQualityFromHealthFraction(float pfFraction)
		{
			float lfClamped = pfFraction < 0f ? 0f : (pfFraction > 1f ? 1f : pfFraction);

			return (int)(lfClamped * 63f);
		}
	}
}
