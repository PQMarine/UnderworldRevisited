namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SLEEP RULES of the rule set (sleep.*; cleanup phase 10, 2026-10-25): the hours of a
	/// night, what it heals and costs, an interrupted night, the doors that shut and who keeps the
	/// sleeper awake (UWSleepRules, UWPlayerVitals.RegenerateOnSleep). Each default is the const of
	/// the class that applies it; see UWRules.
	/// </summary>
	public sealed class UWRulesSleep
	{
		public readonly HoursRules Hours = new HoursRules();

		public readonly RecoveryRules Recovery = new RecoveryRules();

		public readonly HungerRules Hunger = new HungerRules();

		public readonly ReliefRules Relief = new ReliefRules();

		public readonly AmbushRules Ambush = new AmbushRules();

		[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.StarvingDamage),
			"Damage a night on an empty stomach does (and it heals nothing).")]
		public int StarvingDamage = UWSleepRules.StarvingDamage;

		[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.DoorClosesInTen),
			"The chance in ten that an open door of the level shuts in the night.")]
		public int DoorClosesInTen = UWSleepRules.DoorClosesInTen;

		[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.HostileCheckTileRadius),
			"Tiles around the player searched for a hostile creature before sleeping.", Minimum = 0)]
		public int HostileCheckTileRadius = UWSleepRules.HostileCheckTileRadius;

		/// <summary>sleep.hours.*: firstBase + 0..firstRoll-1 hours before the ambush test, totalBase
		/// + 0..totalRoll-1 in all, woundedExtraBase + 0..woundedExtraRoll-1 more below woundedBelow
		/// hit points.</summary>
		public sealed class HoursRules
		{
			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.FirstHoursBase),
				"Hours slept before an ambush can come, before the roll.")]
			public int FirstBase = UWSleepRules.FirstHoursBase;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.FirstHoursRoll),
				"Hours before an ambush: plus 0 to this minus one.", Minimum = 1)]
			public int FirstRoll = UWSleepRules.FirstHoursRoll;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.TotalHoursBase),
				"Hours of an undisturbed night in all, before the roll.")]
			public int TotalBase = UWSleepRules.TotalHoursBase;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.TotalHoursRoll),
				"Hours of a night: plus 0 to this minus one.", Minimum = 1)]
			public int TotalRoll = UWSleepRules.TotalHoursRoll;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.WoundedBelowHitPoints),
				"Below this many hit points the night is longer.")]
			public int WoundedBelow = UWSleepRules.WoundedBelowHitPoints;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.WoundedExtraHoursBase),
				"Extra hours for the wounded, before the roll.")]
			public int WoundedExtraBase = UWSleepRules.WoundedExtraHoursBase;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.WoundedExtraHoursRoll),
				"Extra hours for the wounded: plus 0 to this minus one.", Minimum = 1)]
			public int WoundedExtraRoll = UWSleepRules.WoundedExtraHoursRoll;
		}

		/// <summary>sleep.recovery.*: regen = min(most, base + fatigue / 2); the hit points get a share
		/// of regen + regen * comfort - 1 sixteenths, the mana flatMana and then such a share. Comfort
		/// is the bedroll AND a hunger above wellFedHunger.</summary>
		public sealed class RecoveryRules
		{
			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.RecoveryBase),
				"A night's recovery before the fatigue's half.")]
			public int Base = UWSleepRules.RecoveryBase;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.RecoveryMost),
				"A night's recovery at most.")]
			public int Most = UWSleepRules.RecoveryMost;

			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.SleepFlatMana),
				"Mana every night gives before its share.")]
			public int FlatMana = UWPlayerVitals.SleepFlatMana;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.WellFedHunger),
				"Above this hunger (255 full) a night in the bedroll is comfortable.")]
			public int WellFedHunger = UWSleepRules.WellFedHunger;
		}

		/// <summary>sleep.hunger.*: what a night takes off the hunger, base + 0..roll-1.</summary>
		public sealed class HungerRules
		{
			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.NightHungerBase),
				"Hunger a full night costs, before the roll.")]
			public int NightBase = UWSleepRules.NightHungerBase;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.NightHungerRoll),
				"Hunger a full night costs: plus 0 to this minus one.", Minimum = 1)]
			public int NightRoll = UWSleepRules.NightHungerRoll;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.InterruptedHungerBase),
				"Hunger an interrupted night costs, before the roll.")]
			public int InterruptedBase = UWSleepRules.InterruptedHungerBase;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.InterruptedHungerRoll),
				"Hunger an interrupted night costs: plus 0 to this minus one.", Minimum = 1)]
			public int InterruptedRoll = UWSleepRules.InterruptedHungerRoll;
		}

		/// <summary>sleep.relief.*: what a night takes off the intoxication and an interrupted one
		/// off the fatigue (a full one clears it).</summary>
		public sealed class ReliefRules
		{
			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.SleepIntoxicationRelief),
				"Intoxication a full night removes.")]
			public int Intoxication = UWPlayerVitals.SleepIntoxicationRelief;

			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.InterruptedSleepIntoxicationRelief),
				"Intoxication an interrupted night removes.")]
			public int InterruptedIntoxication = UWPlayerVitals.InterruptedSleepIntoxicationRelief;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.InterruptedFatigueRelief),
				"Fatigue an interrupted night removes.")]
			public int InterruptedFatigue = UWSleepRules.InterruptedFatigueRelief;
		}

		/// <summary>sleep.ambush.*: the creatures that can come upon the sleeper (UWSleepRules.TryAmbush).</summary>
		public sealed class AmbushRules
		{
			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.AmbushTileRadius),
				"Tiles around the sleeper searched for an ambusher.", Minimum = 0)]
			public int TileRadius = UWSleepRules.AmbushTileRadius;

			[UWRule(typeof(UWSleepRules), nameof(UWSleepRules.AmbushMinSteps),
				"An ambusher needs a path of at least this many steps (and is set down two before the sleeper).",
				Minimum = 2)]
			public int MinSteps = UWSleepRules.AmbushMinSteps;
		}
	}
}
