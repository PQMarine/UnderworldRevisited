namespace UWDataImport.UWData
{
	/// <summary>
	/// Rules of the class 7 spells cast on a creature (UWTargetSpell is the Unity side).
	///
	/// POISON, read 2026-09-24 (Todo.md section 0b, row 4): Poison_seg038_3307_834 shows its
	/// effect picture and hands DamageObject_seg023_35A five dice of four (the dice roll
	/// with count 5 and range 4, so 5 to 20) of damage type 0x13 - poison AND magic. That is
	/// ALL: DamageObject scales the damage against the resistances (ScaleDamageAgainstObject,
	/// see UWDamageTypes.Scale - the magic bits roll against the magic resistance first, then
	/// the poison bit bounces off a poison-proof creature) and passes the rest to the creature
	/// as an ordinary hit. A creature carries NO lasting poison; the audit had assumed it did.
	/// Until 2026-09-24 ours dealt a flat 10 of pure poison, so the magic resistance was never
	/// asked.
	/// </summary>
	public static class UWTargetSpellRules
	{
		public const int PoisonDiceCount = 5;

		public const int PoisonDieRange = 4;

		/// <summary>Poison and magic together.</summary>
		public const int PoisonDamageType = UWDamageTypes.Poison | UWDamageTypes.Magic;

		/// <summary>The damage before the resistances, 5 to 20.</summary>
		public static int RollPoisonDamage()
		{
			return UWRandom.RollDice(PoisonDiceCount, PoisonDieRange);
		}
	}
}
