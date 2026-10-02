namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PLAYER IS A CREATURE ROW LIKE ANY OTHER. UW.EXE keeps him as row 63 of the
	/// in-memory creature table (PlayerCritterData_dseg_7272 = Critters_0_dseg_4A52 +
	/// (playerObject.word0 &amp; 0x3F) * 0x30, set in ovr134_152, line 383401). In OBJECTS.DAT
	/// that row is all zeros - the game FILLS it at runtime, and
	/// PlayerStatusUpdate_ovr133_784 (382211-383111) is what fills it.
	///
	/// This matters because an attacking creature does not ask the player object anything
	/// special: CalculateAttackResults_seg022_230E_6B9 (80688) takes the DEFENDER'S ROW and
	/// rolls SkillCheck_seg037_32E6_C(score + flank, row[0x12]) (label 797). Row byte 0x12 is
	/// therefore the player's defence value in every blow, and row bytes 0-3 his armour per
	/// body part (AttackerAppliesFinalDamage_seg022_8A5, label 990).
	///
	/// This class holds the three pieces of arithmetic that were missing from the port; the
	/// walk over the equipment itself lives in UWArmourProtection, which fills the same row.
	///
	///   row[0x12]  = PLAYER.DAT[0x22] (the Defence skill, label 86A)
	///              + PLAYER.DAT[0x21 + var_6] / 2 (label 921-938), where var_6 names the
	///                skill of the weapon in the WEAPON HAND: 2 unarmed, 3 sword, 4 axe,
	///                5 mace - see GetDefenceWeaponSkill.
	///
	///   row[0-3]   = per armour piece ((protection * quality) / 64) + 1 - see GetArmourValue
	///                (ovr133_72D, 382128-382205), NOT the raw table byte
	///              + the same value of a SHIELD in the off hand onto parts 0 and 1
	///                (labels 7EB-867)
	///              + the enchantments, see UWArmourProtection.
	///
	/// WHEN THE ORIGINAL RUNS IT: not per frame. It is called from 26 places - after character
	/// generation (InitPlayer_ovr098_0, 326408), on every change to the inventory
	/// (ovr120_57 365544, RemoveObjectFromInventory_ovr120_5D0, ClickOnInventorySlot_ovr117_0,
	/// ovr121_48), when a spell is cast or expires (CastSpells_seg038_3307_78 124292,
	/// MajorSpellClassB_seg038_1645, PlayerStatusEffectSpells_seg028_589 99297), on eating,
	/// lighting, repairing, sleeping, resurrecting and on a level change
	/// (SetCameraAtObject_ovr134_CA8 385912). Our port recomputes the same values every frame
	/// instead (UWCharacter.RefreshStatus), which is the same result for more work.
	/// </summary>
	public static class UWPlayerCritterRow
	{
		/// <summary>Row byte 0x12 - the check value of the attacker's skill check.</summary>
		public const int DefenceRowByte = 0x12;

		/// <summary>Object ids 0x00-0x0F are the melee weapons, 0x10-0x1F the ranged ones,
		/// 0x20-0x3F the wearables. ovr133_72D tests exactly these bit fields.</summary>
		public const int MeleeLastId = 0x0F;

		/// <summary>The shields, the last five wearables. PlayerStatusUpdate takes the object in
		/// the off hand only when its id lies in this range (labels 80E-840: bits 6-8 clear,
		/// bits 4-5 both set, low nibble 0x0B to 0x0F).</summary>
		public const int ShieldFirstId = 0x3B;

		public const int ShieldLastId = 0x3F;

		/// <summary>An object's quality sits in six bits, so a full piece is 63 of 64.</summary>
		public const int MaxQuality = 0x3F;

		/// <summary>Skill type 3 sword, 4 axe, 5 mace in the melee table; the value is clamped
		/// into this band before it is used as a skill index (labels 8E3-8FB).</summary>
		public const int FirstWeaponSkillType = 3;

		public const int LastWeaponSkillType = 5;

		/// <summary>PLAYER.DAT 0x21 is Attack, 0x22 Defence, 0x23 the first named skill
		/// (Unarmed). The original indexes [0x21 + var_6], so var_6 2 is our Skill.Unarmed 0.
		/// </summary>
		public const int SkillTypeToSkillIndex = 2;

		/// <summary>
		/// What one worn piece adds to its body part - ovr133_72D (382128-382205).
		///
		/// THE QUALITY SCALES THE PROTECTION, and a worn-out piece still gives 1: the routine
		/// returns ((tableProtection * (quality &amp; 0x3F)) &gt;&gt; 6) + 1. A weapon (id below
		/// 0x20) returns 0 - and without the +1, which is why an empty hand is not worth a
		/// point.
		///
		/// The port used the raw table byte until 2026-09-20, so a fresh chain coif gave 3
		/// instead of ((3 * 40) / 64) + 1 = 2, and a ruined one 3 instead of 1.
		/// </summary>
		public static int GetArmourValue(UWObject pOItem, DataImport pOData)
		{
			if (pOItem == null || pOData == null || pOData.ObjectClassProperties == null)
				return 0;

			// A weapon has no armour value at all - the original leaves before the +1.
			if (pOItem.ID <= MeleeLastId)
				return 0;

			UWObjectClassProperties.Wearable lOWearable;

			if (!pOData.ObjectClassProperties.TryGetWearable(pOItem.ID, out lOWearable))
				return 0;

			return ScaleArmourValue(lOWearable.Protection, pOItem.Quality);
		}

		/// <summary>The arithmetic of ovr133_72D on its own, so that a self-check can pin it
		/// without a loaded OBJECTS.DAT.</summary>
		public static int ScaleArmourValue(int piTableProtection, int piQuality)
		{
			return ((piTableProtection * (piQuality & MaxQuality)) >> 6) + 1;
		}

		/// <summary>A shield in the meaning of PlayerStatusUpdate (labels 80E-840).</summary>
		public static bool IsShield(int piObjectId)
		{
			return piObjectId >= ShieldFirstId && piObjectId <= ShieldLastId;
		}

		/// <summary>
		/// WHICH SKILL HALF GOES INTO THE DEFENCE - PlayerStatusUpdate labels 887-92D.
		///
		/// The original reads the object in the WEAPON HAND (inventory slot 8 minus the
		/// handedness bit of PLAYER.DAT 0x64) and asks the melee table for its skill type,
		/// clamped to 3..5. Everything else - an empty hand, a shield, a bow or a sling - keeps
		/// the default var_6 = 2, the Unarmed skill. A ranged weapon therefore does NOT give
		/// its Missile skill to the defence.
		/// </summary>
		public static UWPlayerData.Skill GetDefenceWeaponSkill(UWObject pOWeapon, DataImport pOData)
		{
			if (pOWeapon == null || pOData == null || pOData.ObjectClassProperties == null)
				return UWPlayerData.Skill.Unarmed;

			if (pOWeapon.ID < UWObjectClassProperties.MeleeFirstId || pOWeapon.ID > MeleeLastId)
				return UWPlayerData.Skill.Unarmed;

			UWObjectClassProperties.MeleeWeapon lOWeapon;

			if (!pOData.ObjectClassProperties.TryGetMeleeWeapon(pOWeapon.ID, out lOWeapon))
				return UWPlayerData.Skill.Unarmed;

			int liType = lOWeapon.SkillType;

			// The clamp is the original's and is not cosmetic: the fist carries skill type 6 and
			// would become Mace here. It cannot be held, so the case never arises in the game.
			if (liType < FirstWeaponSkillType)
				liType = FirstWeaponSkillType;
			else if (liType > LastWeaponSkillType)
				liType = LastWeaponSkillType;

			return (UWPlayerData.Skill)(liType - SkillTypeToSkillIndex);
		}

		/// <summary>
		/// Row byte 0x12: the Defence skill plus HALF the weapon skill (labels 86A-938).
		///
		/// THIS IS THE WHOLE POINT OF THE ROUTINE for combat. The port used the bare Defence
		/// skill, so a character with Defence 30 and Sword 25 defended with 30 instead of 42.
		/// Measured in the original on 2026-09-20: a reaper (attack score 30) never scored a
		/// critical against Defence 30 - impossible only once the check is at least 32, which
		/// the weapon-skill half supplies.
		/// </summary>
		public static int GetDefence(int piDefenceSkill, int piWeaponSkillValue)
		{
			return piDefenceSkill + (piWeaponSkillValue / 2);
		}
	}
}
