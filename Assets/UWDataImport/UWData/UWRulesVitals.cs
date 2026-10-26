namespace UWDataImport.UWData
{
	/// <summary>
	/// THE VITALS RULES of the rule set (vitals.*; cleanup phase 10, 2026-10-25): hunger, the
	/// healing and mana checks of the passing time, what a meal heals, drinking, the toadstool and
	/// the worn regeneration - the awake body (UWPlayerVitals) - and since wave 2 (2026-10-26) what
	/// the ground does to it: lava, drowning, the Void. The night's numbers are UWRulesSleep's.
	/// Each default is the const of the class that applies it; see UWRules.
	/// </summary>
	public sealed class UWRulesVitals
	{
		public readonly HungerRules Hunger = new HungerRules();

		public readonly HealingRules Healing = new HealingRules();

		public readonly ManaRegenerationRules ManaRegeneration = new ManaRegenerationRules();

		public readonly MealRules Meal = new MealRules();

		public readonly DrinkRules Drink = new DrinkRules();

		public readonly ToadstoolRules Toadstool = new ToadstoolRules();

		public readonly WornRegenerationRules WornRegeneration = new WornRegenerationRules();

		public readonly LavaRules Lava = new LavaRules();

		public readonly SwimRules Swim = new SwimRules();

		public readonly VoidRules Void = new VoidRules();

		/// <summary>vitals.hunger.*: every five minutes of game time (24 player ticks) the hunger
		/// drops by perBlockBase plus 0 to perBlockRoll - 1; 255 is full, 0 starving.</summary>
		public sealed class HungerRules
		{
			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.HungerPerBlockBase),
				"Hunger lost every five minutes before the roll.")]
			public int PerBlockBase = UWPlayerVitals.HungerPerBlockBase;

			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.HungerPerBlockRoll),
				"Hunger lost every five minutes: plus 0 to this minus one.", Minimum = 1)]
			public int PerBlockRoll = UWPlayerVitals.HungerPerBlockRoll;
		}

		/// <summary>vitals.healing.*: every five minutes one hit point comes back when Strength passes
		/// a skill check against this.</summary>
		public sealed class HealingRules
		{
			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.NaturalHealingDifficulty),
				"The target of the five minutes' healing check on Strength.")]
			public int Difficulty = UWPlayerVitals.NaturalHealingDifficulty;
		}

		/// <summary>vitals.manaRegeneration.*: every minute (3 player ticks) the Mana skill is checked
		/// against this, and a pass gives one or two points.</summary>
		public sealed class ManaRegenerationRules
		{
			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.ManaRegenerationDifficulty),
				"The target of the minute's mana check on the Mana skill.")]
			public int Difficulty = UWPlayerVitals.ManaRegenerationDifficulty;
		}

		/// <summary>vitals.meal.*: a meal heals one point per blocksPerPoint five-minute blocks since
		/// the last one, at most maxHeal.</summary>
		public sealed class MealRules
		{
			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.MaxMealHeal),
				"The most hit points a meal heals.")]
			public int MaxHeal = UWPlayerVitals.MaxMealHeal;

			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.MealHealBlocksPerPoint),
				"A meal heals one point per this many five-minute blocks since the last.", Minimum = 1)]
			public int BlocksPerPoint = UWPlayerVitals.MealHealBlocksPerPoint;
		}

		/// <summary>vitals.drink.*: a sip of something intoxicating checks Strength against the
		/// intoxication (UWPlayerVitals.Drink).</summary>
		public sealed class DrinkRules
		{
			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.DrinkHealAmount),
				"Hit points a critically successful sip gives.")]
			public int HealAmount = UWPlayerVitals.DrinkHealAmount;

			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.DrinkShakeBase),
				"The shaking after waking from passing out, before intoxication / 6.")]
			public int ShakeBase = UWPlayerVitals.DrinkShakeBase;
		}

		/// <summary>vitals.toadstool.*: the toadstool raises a poison below poison to it, one below
		/// poisonLimit by two.</summary>
		public sealed class ToadstoolRules
		{
			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.ToadstoolPoison),
				"A weaker poison becomes this when a toadstool is eaten.")]
			public int Poison = UWPlayerVitals.ToadstoolPoison;

			[UWRule(typeof(UWPlayerVitals), nameof(UWPlayerVitals.ToadstoolPoisonLimit),
				"A poison below this rises by two when a toadstool is eaten.")]
			public int PoisonLimit = UWPlayerVitals.ToadstoolPoisonLimit;
		}

		/// <summary>vitals.wornRegeneration.*: what a worn Regeneration or Mana Regeneration gives
		/// every player tick (UWWornRegeneration).</summary>
		public sealed class WornRegenerationRules
		{
			[UWRule(typeof(UWWornRegeneration), nameof(UWWornRegeneration.Amount),
				"Points a worn Regeneration or Mana Regeneration gives every player tick (21 seconds).")]
			public int Amount = UWWornRegeneration.Amount;
		}

		/// <summary>vitals.lava.*: on lava a roll comes every rollTicks PIT ticks (256 a second) and
		/// one in chanceInN hits for damage points of fire (UWPlayerTerrainRules.TickLava; wave 2,
		/// 2026-10-26, UWSettings.LavaDamage* until then).</summary>
		public sealed class LavaRules
		{
			[UWRule(typeof(UWPlayerTerrainRules), nameof(UWPlayerTerrainRules.LavaRollTicks),
				"PIT ticks (256 a second) between two lava rolls; the original rolls once per frame.", Minimum = 1)]
			public int RollTicks = UWPlayerTerrainRules.LavaRollTicks;

			[UWRule(typeof(UWPlayerTerrainRules), nameof(UWPlayerTerrainRules.LavaHitOneIn),
				"A lava roll hits with one chance in this.", Minimum = 1)]
			public int ChanceInN = UWPlayerTerrainRules.LavaHitOneIn;

			[UWRule(typeof(UWPlayerTerrainRules), nameof(UWPlayerTerrainRules.LavaHitDamage),
				"Fire damage of one lava hit.", Minimum = 0)]
			public int Damage = UWPlayerTerrainRules.LavaHitDamage;
		}

		/// <summary>vitals.swim.*: the swim counter (0x60 on entering the water) rises on a failed
		/// swim check while below riseBelow, and above drownAbove a second check drowns
		/// (UWPlayerTerrainRules.SwimCheck).</summary>
		public sealed class SwimRules
		{
			[UWRule(typeof(UWPlayerTerrainRules), nameof(UWPlayerTerrainRules.SwimCounterRiseLimit),
				"A failed swim check sinks the swimmer further only while the swim counter is below this.")]
			public int RiseBelow = UWPlayerTerrainRules.SwimCounterRiseLimit;

			[UWRule(typeof(UWPlayerTerrainRules), nameof(UWPlayerTerrainRules.SwimCounterDrownThreshold),
				"Above this swim counter a second check can drown the swimmer.")]
			public int DrownAbove = UWPlayerTerrainRules.SwimCounterDrownThreshold;
		}

		/// <summary>vitals.void.*: the Ethereal Void's game ticks per second, each with its one in 32
		/// for an effect (UWVoidRules; wave 2, 2026-10-26, UWSettings.VoidEffectTicksPerSecond until
		/// then).</summary>
		public sealed class VoidRules
		{
			[UWRule(typeof(UWVoidRules), nameof(UWVoidRules.TicksPerSecond),
				"The Void's game ticks per second, each with one chance in 32 for an effect; 0 for none.", Minimum = 0)]
			public int TicksPerSecond = UWVoidRules.TicksPerSecond;
		}
	}
}
