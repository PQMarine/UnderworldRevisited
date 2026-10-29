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

		/// <summary>0xD5 or 0xD6 (the game computes RNG * 2 / 0x8000).</summary>
		public int RollDebris()
		{
			return UWObjectDamageRules.FirstDebrisObjectId + ((UWRandom.Next(0x8000) * UWObjectDamageRules.DebrisVariants) / 0x8000);
		}
	}
}
