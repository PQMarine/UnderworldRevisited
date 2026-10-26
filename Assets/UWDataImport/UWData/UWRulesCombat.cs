namespace UWDataImport.UWData
{
	/// <summary>
	/// THE COMBAT RULES of the rule set (combat.*; cleanup phase 10, 2026-10-25): the skill check
	/// every roll of the game is banded by, the player's attack score and the base damage of a blow,
	/// the charge step of his swing and the Resist Blows switch (both UWSettings until wave 2,
	/// 2026-10-26). Each default is the const of the class that applies it; see UWRules.
	/// </summary>
	public sealed class UWRulesCombat
	{
		public readonly SkillCheckRules SkillCheck = new SkillCheckRules();

		public readonly PlayerRules Player = new PlayerRules();

		public readonly DamageRules Damage = new DamageRules();

		[UWRule(typeof(UWArmourProtection), nameof(UWArmourProtection.ResistBlowsAddsArmourOriginal),
			"Resist Blows adds its value to the armour of all four body parts (the original writes it where nothing reads it).")]
		public bool ResistBlowsAddsArmour = UWArmourProtection.ResistBlowsAddsArmourOriginal;

		/// <summary>combat.skillCheck.*: score = skill - target + a roll of 0 to rollRange - 1,
		/// banded (UWSkillCheck) - hitting, casting, picking locks, healing, haggling.</summary>
		public sealed class SkillCheckRules
		{
			[UWRule(typeof(UWSkillCheck), nameof(UWSkillCheck.RollRange),
				"The skill check's die: the roll is 0 to this minus one.", Minimum = 1)]
			public int RollRange = UWSkillCheck.RollRange;

			[UWRule(typeof(UWSkillCheck), nameof(UWSkillCheck.CriticalFailureAtMost),
				"A score up to this is a critical failure (a backfire, a broken lock pick).")]
			public int CriticalFailureAtMost = UWSkillCheck.CriticalFailureAtMost;

			[UWRule(typeof(UWSkillCheck), nameof(UWSkillCheck.FailureAtMost),
				"A score up to this is a failure; above it the check passes.")]
			public int FailureAtMost = UWSkillCheck.FailureAtMost;

			[UWRule(typeof(UWSkillCheck), nameof(UWSkillCheck.SuccessAtMost),
				"A score up to this is a success; above it a critical success.")]
			public int SuccessAtMost = UWSkillCheck.SuccessAtMost;
		}

		/// <summary>combat.player.*: the player's attack score and his missiles (UWPlayerAttack).</summary>
		public sealed class PlayerRules
		{
			[UWRule(typeof(UWPlayerAttack), nameof(UWPlayerAttack.AttackScoreDivisor),
				"The attack score takes the Attack skill divided by this.", Minimum = 1)]
			public int AttackDivisor = UWPlayerAttack.AttackScoreDivisor;

			[UWRule(typeof(UWPlayerAttack), nameof(UWPlayerAttack.DexterityScoreDivisor),
				"The attack score takes Dexterity divided by this.", Minimum = 1)]
			public int DexterityDivisor = UWPlayerAttack.DexterityScoreDivisor;

			[UWRule(typeof(UWPlayerAttack), nameof(UWPlayerAttack.EasyDifficultyAttackBonus),
				"What the easy difficulty adds to the player's attack score.")]
			public int EasyAttackBonus = UWPlayerAttack.EasyDifficultyAttackBonus;

			[UWRule(typeof(UWPlayerAttack), nameof(UWPlayerAttack.MissileSkillTarget),
				"The target of the Missile check when shooting.")]
			public int MissileTarget = UWPlayerAttack.MissileSkillTarget;

			[UWRule(typeof(UWPlayerAttackState), nameof(UWPlayerAttackState.ChargeStepMilliseconds),
				"Milliseconds per charge step of a held swing after the wind-up; each adds the weapon's speed, up to 100.",
				Minimum = 1)]
			public int ChargeStepMilliseconds = UWPlayerAttackState.ChargeStepMilliseconds;
		}

		/// <summary>combat.damage.*: the base value a blow's dice are rolled from (UWCombat,
		/// UWCritterCombat).</summary>
		public sealed class DamageRules
		{
			[UWRule(typeof(UWCombat), nameof(UWCombat.MinimumBaseDamage),
				"A base below this is raised to it before the dice.")]
			public int MinimumBase = UWCombat.MinimumBaseDamage;

			[UWRule(typeof(UWCombat), nameof(UWCombat.UnarmedBaseDamage),
				"Unarmed base: this plus strength / unarmedStrengthDivisor plus a share of the Unarmed skill.")]
			public int UnarmedBase = UWCombat.UnarmedBaseDamage;

			[UWRule(typeof(UWCombat), nameof(UWCombat.UnarmedStrengthDivisor),
				"Unarmed base: strength divided by this.", Minimum = 1)]
			public int UnarmedStrengthDivisor = UWCombat.UnarmedStrengthDivisor;

			[UWRule(typeof(UWCombat), nameof(UWCombat.UnarmedSkillNumerator),
				"Unarmed base: the Unarmed skill times this, divided by unarmedSkillDenominator.")]
			public int UnarmedSkillNumerator = UWCombat.UnarmedSkillNumerator;

			[UWRule(typeof(UWCombat), nameof(UWCombat.UnarmedSkillDenominator),
				"Unarmed base: see unarmedSkillNumerator.", Minimum = 1)]
			public int UnarmedSkillDenominator = UWCombat.UnarmedSkillDenominator;

			[UWRule(typeof(UWCombat), nameof(UWCombat.ArmedStrengthDivisor),
				"Armed base: strength divided by this, plus the weapon's value.", Minimum = 1)]
			public int ArmedStrengthDivisor = UWCombat.ArmedStrengthDivisor;
		}
	}
}
