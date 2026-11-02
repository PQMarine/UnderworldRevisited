namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FIRST GAME'S COMBAT STEPS (phase 0 step 2, 2026-10-28): what UWPlayerAttack,
	/// UWAttackResolution, UWDamageTypes and UWObjectDamageRules did at these steps until then,
	/// moved here unchanged; those classes ask here now.
	/// </summary>
	public sealed class UWCombatVariantUw1 : IUWCombatVariant
	{
		/// <summary>Only class 0xC enchants a weapon: (minor &amp; 7) + 1, to the damage when bit 3 of
		/// the minor is set, else to the attack score (the wielded weapon's, UWPlayerAttack).</summary>
		public void WeaponEnchantment(int piMajor, int piMinor, out int piAccuracy, out int piDamage)
		{
			piAccuracy = 0;
			piDamage = 0;

			if (piMajor != UWPlayerAttack.WeaponEnchantmentMajorClass)
				return;

			int liAmount = (piMinor & 7) + 1;

			if ((piMinor & UWPlayerAttack.DamageEnchantmentBit) != 0)
				piDamage = liAmount;
			else
				piAccuracy = liAmount;
		}

		/// <summary>No armour test: the resistance alone decides (UWCritterCombat.ShouldPoison).</summary>
		public bool PoisonGetsThrough(in UWAttackerStats pOAttacker, in UWAttackOutcome pOOutcome)
		{
			return true;
		}

		/// <summary>The first game has no doubling for vulnerability.</summary>
		public int Unresisted(int piResistances, int piDamage, int piDamageType)
		{
			return piDamage;
		}

		/// <summary>The door direction bit (almost every trigger and trap carries it) or quality class 3.</summary>
		public bool ShrugsOff(bool pbProtected, int piQualityClass)
		{
			return pbProtected || piQualityClass >= UWObjectDamageRules.ImmuneQualityClass;
		}

		public bool DoorsTakeDamage => false;

		private const int Bow = 0x19;

		private const int Crossbow = 0x1A;

		/// <summary>0xD5 or 0xD6 (the game computes RNG * 2 / 0x8000).</summary>
		public int RollDebris()
		{
			return UWObjectDamageRules.FirstDebrisObjectId + ((UWRandom.Next(0x8000) * UWObjectDamageRules.DebrisVariants) / 0x8000);
		}

		public int DebrisOf(int piItem, int piDamageType, DataImport pOData)
		{
			return RollDebris();
		}

		public int PlayerWeaponKind(int piWeaponId)
		{
			return -1;
		}

		public int MeleeHitSound(int piWeaponKind)
		{
			return UWSoundEffectId.HitCritter;
		}

		public int WallSound(int piWeaponKind)
		{
			return UWSoundEffectId.SpellImpact;
		}

		public bool MissSoundsAtWall => true;

		public int BlockedSound(int piWeaponKind, bool pbArmourMetal)
		{
			return UWCritterCombat.GetBlockedSound(piWeaponKind, pbArmourMetal);
		}

		/// <summary>Every launch plays effect 0xA (PrepareProjectileObject).</summary>
		public int ThrowSound => UWSoundEffectId.Miss;

		public int ShotSound(int piLauncherId)
		{
			return UWSoundEffectId.Miss;
		}

		/// <summary>The two launchers that twang (MissileRelease_seg025_9F: weapon index 9 and 10 in the ranged
		/// table, which starts at 0x10), never the sling, and also when the shot failed for lack of room.</summary>
		public bool Twangs(int piLauncherId, bool pbFired)
		{
			return piLauncherId == Bow || piLauncherId == Crossbow;
		}

		public int NoRoomSound => UWAttackResolution.NoSound;

		public int NoRoomToFireString => 0xFE + 1;

		public bool MissileDamageSounds => true;
	}
}
