using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S COMBAT STEPS (UW2 plan phase 4), from Underworld Exhumed's uw2 combat/COMBAT.C and DAMAGE.C.
	/// Valor, Poison Weapon, the special weapons' extra effects and the sounds by the weapon's kind come with the
	/// player's blows.
	/// </summary>
	public sealed class UWCombatVariantUw2 : IUWCombatVariant
	{
		private const int WeaponEnchantmentClass = 0xC;

		private const int LowEnchantments = 4;

		private const int PlainEnchantments = 8;

		/// <summary>check_res' fire and cold bits, in the resist byte and in a damage type.</summary>
		private const int Fire = 8;

		private const int Cold = 0x20;

		private const int MostDamage = 0xFF;

		private const int LastDoubled = 0x7F;

		/// <summary>debris_type's pile of debris, ITEM_PILE_OF_DEBRIS_D6.</summary>
		private const int DebrisPile = 0xD6;

		/// <summary>The armour on a part of the player (UWVitalsUw2): the poisonous blow's roll reads it.</summary>
		public static Func<int, int> PlayerArmour;

		/// <summary>An effect below 4 adds 2e + 1 to the damage, from 4 to 7 2e - 7 to the attack score; 8 and up
		/// are the special weapons, their tables with the player's blows (COMBAT.C:641).</summary>
		public void WeaponEnchantment(int piMajor, int piMinor, out int piAccuracy, out int piDamage)
		{
			piAccuracy = 0;
			piDamage = 0;

			if (piMajor != WeaponEnchantmentClass || piMinor >= PlainEnchantments)
				return;

			if (piMinor < LowEnchantments)
				piDamage = (piMinor * 2) + 1;
			else
				piAccuracy = (piMinor * 2) - 7;
		}

		/// <summary>rand() % (poison + 6) must beat twice the armour at the hit (COMBAT.C:911).</summary>
		public bool PoisonGetsThrough(in UWAttackerStats pOAttacker, in UWAttackOutcome pOOutcome)
		{
			int liArmour = PlayerArmour != null ? PlayerArmour(pOOutcome.Part) : 0;

			return UWRandom.Next(pOAttacker.PoisonByte + 6) > liArmour * 2;
		}

		/// <summary>Fire on a creature resisting cold, or cold on one resisting fire but not cold, doubled up to
		/// 255 (DAMAGE.C:225).</summary>
		public int Unresisted(int piResistances, int piDamage, int piDamageType)
		{
			bool lbFireOnCold = (piDamageType & Fire) != 0 && (piResistances & Cold) != 0;
			bool lbColdOnFire = (piDamageType & Cold) != 0 && (piResistances & Fire) != 0 && (piResistances & (Fire | Cold)) != (Fire | Cold);

			if (!lbFireOnCold && !lbColdOnFire)
				return piDamage;

			return piDamage < LastDoubled ? piDamage << 1 : MostDamage;
		}

		/// <summary>Doors can be damaged (DAMAGE.C:265): only the quality class protects.</summary>
		public bool ShrugsOff(bool pbProtected, int piQualityClass)
		{
			return piQualityClass >= UWObjectDamageRules.ImmuneQualityClass;
		}

		/// <summary>The pile; the broken weapons and the wood chips need the item.</summary>
		public int RollDebris()
		{
			return DebrisPile;
		}
	}
}
