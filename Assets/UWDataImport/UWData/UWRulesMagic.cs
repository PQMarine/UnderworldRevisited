namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MAGIC RULES of the rule set (magic.*; cleanup phase 10, 2026-10-25): what a rune spell
	/// costs and takes to cast, how long a lasting one runs, the pause between two casts, the dice of
	/// the damaging spells and of a backfire. Each default is the const of the class that applies it;
	/// see UWRules. A die here is rolled as the game's dice always are (UWRandom.RollDice): each of
	/// "dice" dice counts 1 to "sides".
	/// </summary>
	public sealed class UWRulesMagic
	{
		[UWRule(typeof(UWRunicMagic), nameof(UWRunicMagic.ManaPerCircle),
			"Mana a rune spell costs per circle (circle 1 costs this, circle 8 eight times it).", Minimum = 0)]
		public int ManaPerCircle = UWRunicMagic.ManaPerCircle;

		public readonly CastingCheckRules CastingCheck = new CastingCheckRules();

		public readonly CastDelayRules CastDelay = new CastDelayRules();

		public readonly DurationRules Duration = new DurationRules();

		public readonly BackfireRules Backfire = new BackfireRules();

		public readonly FlameWindRules FlameWind = new FlameWindRules();

		public readonly SheetLightningRules SheetLightning = new SheetLightningRules();

		public readonly AreaHitsRules AreaHits = new AreaHitsRules();

		public readonly PoisonRules Poison = new PoisonRules();

		public readonly TremorRules Tremor = new TremorRules();

		/// <summary>magic.castingCheck.*: the cast's skill check is Casting + bonus against
		/// perCircle * circle (UWSpellCasting.CastRunes).</summary>
		public sealed class CastingCheckRules
		{
			[UWRule(typeof(UWSpellCasting), nameof(UWSpellCasting.CastingCheckBonus),
				"Added to the Casting skill for the casting check.")]
			public int Bonus = UWSpellCasting.CastingCheckBonus;

			[UWRule(typeof(UWSpellCasting), nameof(UWSpellCasting.CastingCheckPerCircle),
				"The casting check's target per circle of the spell.")]
			public int PerCircle = UWSpellCasting.CastingCheckPerCircle;
		}

		/// <summary>magic.castDelay.*: after a cast the next one waits (2 * circle - level) * perStep
		/// + base clock units, 256 a second, the whole kept in a byte (UWSpellCasting.CastRunes).</summary>
		public sealed class CastDelayRules
		{
			[UWRule(typeof(UWSpellCasting), nameof(UWSpellCasting.CastDelayBase),
				"The pause after a cast, in 1/256 seconds, before the circle's share.")]
			public int Base = UWSpellCasting.CastDelayBase;

			[UWRule(typeof(UWSpellCasting), nameof(UWSpellCasting.CastDelayPerStep),
				"The pause's share per step of (2 * circle - level).")]
			public int PerStep = UWSpellCasting.CastDelayPerStep;
		}

		/// <summary>magic.duration.*: how many player ticks (21 seconds each) a lasting spell runs, by
		/// its stability class (UWSpellCasting.RollStability).</summary>
		public sealed class DurationRules
		{
			[UWRule(typeof(UWSpellCasting), nameof(UWSpellCasting.LongStabilityDice),
				"Long spells (Light, Night Vision, ...): this many dice.", Minimum = 0)]
			public int LongDice = UWSpellCasting.LongStabilityDice;

			[UWRule(typeof(UWSpellCasting), nameof(UWSpellCasting.LongStabilitySides),
				"Long spells: the sides of each die.", Minimum = 0)]
			public int LongSides = UWSpellCasting.LongStabilitySides;

			[UWRule(typeof(UWSpellCasting), nameof(UWSpellCasting.LongStabilityBonus),
				"Long spells: ticks added to the dice.")]
			public int LongBonus = UWSpellCasting.LongStabilityBonus;

			[UWRule(typeof(UWSpellCasting), nameof(UWSpellCasting.MediumStabilityDice),
				"Medium spells (Thick Skin, ...): this many dice.", Minimum = 0)]
			public int MediumDice = UWSpellCasting.MediumStabilityDice;

			[UWRule(typeof(UWSpellCasting), nameof(UWSpellCasting.MediumStabilitySides),
				"Medium spells: the sides of each die.", Minimum = 0)]
			public int MediumSides = UWSpellCasting.MediumStabilitySides;

			[UWRule(typeof(UWSpellCasting), nameof(UWSpellCasting.MediumStabilityBonus),
				"Medium spells: ticks added to the dice.")]
			public int MediumBonus = UWSpellCasting.MediumStabilityBonus;

			[UWRule(typeof(UWSpellCasting), nameof(UWSpellCasting.ShortStabilityDice),
				"Short spells: this many dice.", Minimum = 0)]
			public int ShortDice = UWSpellCasting.ShortStabilityDice;

			[UWRule(typeof(UWSpellCasting), nameof(UWSpellCasting.ShortStabilitySides),
				"Short spells: the sides of each die.", Minimum = 0)]
			public int ShortSides = UWSpellCasting.ShortStabilitySides;
		}

		/// <summary>magic.backfire.*: a critically failed cast rolls its dice of this many sides off
		/// the caster's hit points, never below the floor (UWDamagePipeline).</summary>
		public sealed class BackfireRules
		{
			[UWRule(typeof(UWDamagePipeline), nameof(UWDamagePipeline.BackfireDieSides),
				"The sides of each backfire die.", Minimum = 0)]
			public int DieSides = UWDamagePipeline.BackfireDieSides;

			[UWRule(typeof(UWDamagePipeline), nameof(UWDamagePipeline.BackfireFloor),
				"A backfire brings nobody below this many hit points, and does nothing at or below it.")]
			public int Floor = UWDamagePipeline.BackfireFloor;
		}

		/// <summary>magic.flameWind.*: the Flame Wind's damage, rolled anew for every object on a
		/// tile of its cross (UWAreaSpellRules).</summary>
		public sealed class FlameWindRules
		{
			[UWRule(typeof(UWAreaSpellRules), nameof(UWAreaSpellRules.FlameDiceCount),
				"Flame Wind damage: this many dice.", Minimum = 0)]
			public int Dice = UWAreaSpellRules.FlameDiceCount;

			[UWRule(typeof(UWAreaSpellRules), nameof(UWAreaSpellRules.FlameDiceRange),
				"Flame Wind damage: the sides of each die.", Minimum = 0)]
			public int Sides = UWAreaSpellRules.FlameDiceRange;
		}

		/// <summary>magic.sheetLightning.*: the Sheet Lightning's damage per object struck
		/// (UWAreaSpellRules).</summary>
		public sealed class SheetLightningRules
		{
			[UWRule(typeof(UWAreaSpellRules), nameof(UWAreaSpellRules.LightningDiceCount),
				"Sheet Lightning damage: this many dice.", Minimum = 0)]
			public int Dice = UWAreaSpellRules.LightningDiceCount;

			[UWRule(typeof(UWAreaSpellRules), nameof(UWAreaSpellRules.LightningDiceRange),
				"Sheet Lightning damage: the sides of each die.", Minimum = 0)]
			public int Sides = UWAreaSpellRules.LightningDiceRange;
		}

		/// <summary>magic.areaHits.*: how many objects an area spell (Reveal, Sheet Lightning,
		/// Confusion, Flame Wind) reaches at most (UWAreaSpellRules.CastAt).</summary>
		public sealed class AreaHitsRules
		{
			[UWRule(typeof(UWAreaSpellRules), nameof(UWAreaSpellRules.HitDiceCount),
				"Objects an area spell reaches: this many dice.", Minimum = 0)]
			public int Dice = UWAreaSpellRules.HitDiceCount;

			[UWRule(typeof(UWAreaSpellRules), nameof(UWAreaSpellRules.HitDiceRange),
				"Objects an area spell reaches: the sides of each die.", Minimum = 0)]
			public int Sides = UWAreaSpellRules.HitDiceRange;
		}

		/// <summary>magic.poison.*: the Poison spell's damage before the resistances
		/// (UWTargetSpellRules).</summary>
		public sealed class PoisonRules
		{
			[UWRule(typeof(UWTargetSpellRules), nameof(UWTargetSpellRules.PoisonDiceCount),
				"Poison spell damage: this many dice.", Minimum = 0)]
			public int Dice = UWTargetSpellRules.PoisonDiceCount;

			[UWRule(typeof(UWTargetSpellRules), nameof(UWTargetSpellRules.PoisonDieRange),
				"Poison spell damage: the sides of each die.", Minimum = 0)]
			public int Sides = UWTargetSpellRules.PoisonDieRange;
		}

		/// <summary>magic.tremor.*: the most boulders a Tremor drops (UWMiscSpellRules).</summary>
		public sealed class TremorRules
		{
			[UWRule(typeof(UWMiscSpellRules), nameof(UWMiscSpellRules.TremorDiceCount),
				"Tremor boulders: this many dice.", Minimum = 0)]
			public int Dice = UWMiscSpellRules.TremorDiceCount;

			[UWRule(typeof(UWMiscSpellRules), nameof(UWMiscSpellRules.TremorDieSides),
				"Tremor boulders: the sides of each die.", Minimum = 0)]
			public int Sides = UWMiscSpellRules.TremorDieSides;
		}
	}
}
