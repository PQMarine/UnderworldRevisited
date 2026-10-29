namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STEPS OF COMBAT WHERE THE GAMES PART (Docs/UW2-PLAN.md section 4.3, phase 0 step 2,
	/// 2026-10-28), from Underworld Exhumed's list of differences (docs/UW1-UW2-DIFFERENCES.md,
	/// "Combat"). The rules keep their flow and ask here; what only the second game has (Valor's
	/// bonus, Poison Weapon, the special weapons, the combat sounds by the weapon's kind) comes with
	/// its variant in phase 4. The numbers that differ are rule set entries (UWRulesCombat).
	/// </summary>
	public interface IUWCombatVariant
	{
		/// <summary>What a weapon's enchantment of this class adds to the attack score and to the
		/// damage (UW1: class 0xC, (minor &amp; 7) + 1 to one of them by bit 3, COMBAT.C:594; UW2: by
		/// the effect's range, and special weapons from 8, COMBAT.C:641).</summary>
		void WeaponEnchantment(int piMajor, int piMinor, out int piAccuracy, out int piDamage);

		/// <summary>Whether a creature's poisonous hit gets past the player's armour (UW1: always, the
		/// resistance alone decides, COMBAT.C:808; UW2: a roll against twice the armour first,
		/// COMBAT.C:911).</summary>
		bool PoisonGetsThrough(in UWAttackerStats pOAttacker, in UWAttackOutcome pOOutcome);

		/// <summary>The damage of a type the target resists not at all (check_res; UW1: as it is,
		/// DAMAGE.C:166; UW2: fire on a creature resisting cold, or cold on one resisting fire but not
		/// cold, doubled up to 255, DAMAGE.C:225).</summary>
		int Unresisted(int piResistances, int piDamage, int piDamageType);

		/// <summary>damage_object's first test: whether an object takes nothing at all (UW1: the door
		/// direction bit or quality class 3, DAMAGE.C:213; UW2: doors can be damaged, DAMAGE.C:265).</summary>
		bool ShrugsOff(bool pbProtected, int piQualityClass);

		/// <summary>The debris a destroyed object becomes (UW1: one of two at random, DAMAGE.C:140;
		/// UW2: debris_type and the broken weapons, DAMAGE.C:117 and :174).</summary>
		int RollDebris();
	}
}
