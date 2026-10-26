namespace UWDataImport.UWData
{
	/// <summary>
	/// THE CREATURE RULES of the rule set (critter.*; cleanup phase 10 wave 2, 2026-10-26): the
	/// creature AI's numbers of UW.EXE (UWCritterRules, Docs/AI/creature-ai.md section 9) that are
	/// odds, distances and coefficients - a strong individual's bonuses, the swing-charge table,
	/// the melee distances, the senses, the wander step, doors, withdrawing, the leash, following,
	/// talking, spells and missiles - plus what a creature's blow does on easy, the remains, and
	/// the player's loudness the creatures hear. Each default is the const of the class that applies
	/// it; see UWRules. The clock (slots and intervals), the turn limits, the path search and the
	/// motion encoding (pitches, hops, aim points) stay const: they are the engine, not the rules.
	/// "Squared" distances are in eighths of a tile or in whole tiles as each meaning says.
	/// </summary>
	public sealed class UWRulesCritter
	{
		public readonly StrongRules Strong = new StrongRules();

		[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.ChargeTable),
			"A creature's damage multiplier over 128 by its swing charge 0 to 15, raised by each failed start roll (16 numbers).",
			Minimum = 0)]
		public int[] ChargeTable = (int[])UWCritterRules.ChargeTable.Clone();

		[UWRule(typeof(UWCritterCombat), nameof(UWCritterCombat.EasyHalvesDamage),
			"On the easy difficulty the damage of a blow or missile on the player is halved.")]
		public bool EasyHalvesDamage = UWCritterCombat.EasyHalvesDamage;

		public readonly CorpseRules Corpse = new CorpseRules();

		public readonly MeleeRules Melee = new MeleeRules();

		public readonly SenseRules Sense = new SenseRules();

		public readonly WanderRules Wander = new WanderRules();

		public readonly DoorRules Door = new DoorRules();

		public readonly WithdrawRules Withdraw = new WithdrawRules();

		public readonly LeashRules Leash = new LeashRules();

		public readonly FollowRules Follow = new FollowRules();

		public readonly TalkRules Talk = new TalkRules();

		public readonly SpellRules Spell = new SpellRules();

		public readonly MissileRules Missile = new MissileRules();

		public readonly NoiseRules Noise = new NoiseRules();

		/// <summary>critter.strong.*: a strong individual (word 0x0D bit 10) adds toHitBase + 0..toHitRoll-1
		/// to its score and damageBase + 0..damageRoll-1 to its damage, defends with armour * numerator
		/// / denominator, and is worth experience * (experienceBase + 0..experienceRoll-1) /
		/// experienceDivisor (UWCritterCombat).</summary>
		public sealed class StrongRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.StrongToHitBase),
				"A strong creature's bonus to its attack score, before the roll.")]
			public int ToHitBase = UWCritterRules.StrongToHitBase;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.StrongToHitRoll),
				"A strong creature's attack score bonus: plus 0 to this minus one.", Minimum = 1)]
			public int ToHitRoll = UWCritterRules.StrongToHitRoll;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.StrongDamageBase),
				"A strong creature's bonus to its damage, before the roll.")]
			public int DamageBase = UWCritterRules.StrongDamageBase;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.StrongDamageRoll),
				"A strong creature's damage bonus: plus 0 to this minus one.", Minimum = 1)]
			public int DamageRoll = UWCritterRules.StrongDamageRoll;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.StrongArmourNumerator),
				"A strong defender's armour times this, divided by armourDenominator.")]
			public int ArmourNumerator = UWCritterRules.StrongArmourNumerator;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.StrongArmourDenominator),
				"A strong defender's armour: see armourNumerator.", Minimum = 1)]
			public int ArmourDenominator = UWCritterRules.StrongArmourDenominator;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.StrongExperienceBase),
				"A strong creature's kill experience is times this plus the roll, over experienceDivisor.")]
			public int ExperienceBase = UWCritterRules.StrongExperienceBase;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.StrongExperienceRoll),
				"A strong creature's experience factor: plus 0 to this minus one.", Minimum = 1)]
			public int ExperienceRoll = UWCritterRules.StrongExperienceRoll;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.StrongExperienceDivisor),
				"A strong creature's experience factor is divided by this.", Minimum = 1)]
			public int ExperienceDivisor = UWCritterRules.StrongExperienceDivisor;
		}

		/// <summary>critter.corpse.*: the corpse a dead creature leaves comes with chance in roll
		/// (UWCritterCombat.ShouldDropCorpse).</summary>
		public sealed class CorpseRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.CorpseChance),
				"A dead creature leaves its corpse when a roll of 0 to roll minus one is below this.")]
			public int Chance = UWCritterRules.CorpseChance;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.CorpseRoll),
				"The corpse's roll: 0 to this minus one.", Minimum = 1)]
			public int Roll = UWCritterRules.CorpseRoll;
		}

		/// <summary>critter.melee.*: goal 5's melee branch, its footwork and goal 9's stand (squared
		/// distances in eighths of a tile unless the name says tiles).</summary>
		public sealed class MeleeRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.MeleeDistanceSquared),
				"Goal 5 fights hand to hand below this squared distance in eighths (ten eighths).")]
			public int DistanceSquared = UWCritterRules.MeleeDistanceSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.MeleeHeightLevels),
				"Hand to hand needs a floor difference below this many height levels (fliers need none).")]
			public int HeightLevels = UWCritterRules.MeleeHeightLevels;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.MeleeStartChance),
				"In reach a creature starts a swing with one chance in this per update.", Minimum = 1)]
			public int StartChance = UWCritterRules.MeleeStartChance;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.MeleeBackOffDistanceSquared),
				"Closer than this squared distance in eighths the creature side-steps or backs off.")]
			public int BackOffDistanceSquared = UWCritterRules.MeleeBackOffDistanceSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.SideStepChance),
				"Too close, one chance in this side-steps instead of backing off.", Minimum = 1)]
			public int SideStepChance = UWCritterRules.SideStepChance;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.MeleeCircleDistanceSquared),
				"Up to this squared distance in eighths the creature circles or stands; beyond it walks in.")]
			public int CircleDistanceSquared = UWCritterRules.MeleeCircleDistanceSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.StandMeleeReachSquared),
				"Goal 9 (stand and fight) strikes below this squared distance in eighths.")]
			public int StandReachSquared = UWCritterRules.StandMeleeReachSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.HoldGroundTilesSquared),
				"Goal 9 faces and holds within this squared distance in tiles.")]
			public int HoldGroundTilesSquared = UWCritterRules.HoldGroundTilesSquared;
		}

		/// <summary>critter.sense.*: hearing, losing a target, the far freeze, the reaction to the
		/// player, the standing rolls and the morale roll (UWCritterBrain).</summary>
		public sealed class SenseRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.HearingDivisor),
				"A target is heard when the hearing range squared divided by this exceeds the squared distance.",
				Minimum = 1)]
			public int HearingDivisor = UWCritterRules.HearingDivisor;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.LostMultiplier),
				"A target is lost at a squared distance of the hearing range squared times this.")]
			public int LostMultiplier = UWCritterRules.LostMultiplier;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.FarTileDistanceSquared),
				"Beyond this squared tile distance from the player and the view a creature is frozen.")]
			public int FarTilesSquared = UWCritterRules.FarTileDistanceSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.ReactToPlayerDistanceSquared),
				"A wandering creature faces a player closer than this squared distance in eighths.")]
			public int ReactToPlayerDistanceSquared = UWCritterRules.ReactToPlayerDistanceSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.ReSearchChance),
				"Goal 5 searches its moved target again with one chance in this per update.", Minimum = 1)]
			public int ReSearchChance = UWCritterRules.ReSearchChance;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.UnchangedCoin),
				"On an unchanged search one chance in this keeps the target or goes to look.", Minimum = 1)]
			public int UnchangedCoin = UWCritterRules.UnchangedCoin;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.InitiativeRoll),
				"A standing creature turns and searches by a roll of 0 to this minus one against its initiative.",
				Minimum = 1)]
			public int InitiativeRoll = UWCritterRules.InitiativeRoll;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.MoraleRoll),
				"The morale check adds a roll of 0 to this minus one to the hit point term.", Minimum = 1)]
			public int MoraleRoll = UWCritterRules.MoraleRoll;
		}

		/// <summary>critter.wander.*: the wander step - keep walking or stand by the restlessness,
		/// deflect the heading, bend away from the player, the hostile's stand, the blocked bit
		/// (UWCritterBrain).</summary>
		public sealed class WanderRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.RestlessnessRoll),
				"A walking creature keeps walking by a roll of 0 to this minus one against its restlessness.",
				Minimum = 1)]
			public int RestlessnessRoll = UWCritterRules.RestlessnessRoll;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.WanderDeflectRoll),
				"A walking (or withdrawing) creature deflects when a roll of 0 to this minus one is below restlessness + deflectBonus.",
				Minimum = 1)]
			public int DeflectRoll = UWCritterRules.WanderDeflectRoll;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.WanderDeflectBonus),
				"Added to the restlessness for the deflection roll.")]
			public int DeflectBonus = UWCritterRules.WanderDeflectBonus;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.IdleTurnRoll),
				"A standing creature turns when a roll of 0 to this minus one is below its restlessness.",
				Minimum = 1)]
			public int IdleTurnRoll = UWCritterRules.IdleTurnRoll;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.WanderAvoidPlayerEighths),
				"The wander heading bends away from a player within this many eighths of a tile.")]
			public int AvoidPlayerEighths = UWCritterRules.WanderAvoidPlayerEighths;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.HostileStandChance),
				"A hostile creature stands instead of wandering in one update of this many.", Minimum = 1)]
			public int HostileStandChance = UWCritterRules.HostileStandChance;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.BlockedClearChance),
				"A blocked creature resumes its approach with one chance in this per update.", Minimum = 1)]
			public int BlockedClearChance = UWCritterRules.BlockedClearChance;
		}

		/// <summary>critter.door.*: a hostile creature at a closed door (UWCritterBrain).</summary>
		public sealed class DoorRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.DoorTryChance),
				"A hostile creature gives up on a closed door with one chance in this, else tries it.", Minimum = 1)]
			public int TryChance = UWCritterRules.DoorTryChance;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.LockpickChance),
				"With door skill the lock is picked unless a roll of 0 to this minus one is zero.", Minimum = 1)]
			public int LockpickChance = UWCritterRules.LockpickChance;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.BashChance),
				"Otherwise one chance in this bashes the door.", Minimum = 1)]
			public int BashChance = UWCritterRules.BashChance;
		}

		/// <summary>critter.withdraw.*: goal 6, the creature that flees (UWCritterBrain).</summary>
		public sealed class WithdrawRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.WithdrawNearTilesSquared),
				"A fleeing creature this close (squared tiles) may turn and stand.")]
			public int NearTilesSquared = UWCritterRules.WithdrawNearTilesSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.WithdrawNearHeight),
				"... and within this height difference in zpos.")]
			public int NearHeight = UWCritterRules.WithdrawNearHeight;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.StandRoll),
				"Near, it stands when a roll of 0 to this minus one is below its morale / 8.", Minimum = 1)]
			public int StandRoll = UWCritterRules.StandRoll;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.CorneredTilesSquared),
				"A fleeing creature blocked within this squared tile distance is cornered and fights.")]
			public int CorneredTilesSquared = UWCritterRules.CorneredTilesSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.FleeFastTilesSquared),
				"Below this squared tile distance it flees at its pursuit speed, beyond at its wander speed.")]
			public int FleeFastTilesSquared = UWCritterRules.FleeFastTilesSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.WithdrawAvoidPlayerEighths),
				"A fleeing creature's heading bends away from a player within this many eighths.")]
			public int AvoidPlayerEighths = UWCritterRules.WithdrawAvoidPlayerEighths;
		}

		/// <summary>critter.leash.*: how far goal 5 strays from home, and when goal 8 walks back.</summary>
		public sealed class LeashRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.LeashTargetDistanceSquared),
				"The leash of goal 5 holds only beyond this squared distance in eighths to the target.")]
			public int TargetDistanceSquared = UWCritterRules.LeashTargetDistanceSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.LeashRangeFactor),
				"... when the squared tile distance from home exceeds this times the travel range squared.")]
			public int RangeFactor = UWCritterRules.LeashRangeFactor;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.HomeRangeFactor),
				"Goal 8 walks home beyond this times the travel range squared (squared tiles).")]
			public int HomeRangeFactor = UWCritterRules.HomeRangeFactor;
		}

		/// <summary>critter.follow.*: goal 3, the follower.</summary>
		public sealed class FollowRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.FollowNearTilesSquared),
				"A follower within this squared tile distance stays and swings for show.")]
			public int NearTilesSquared = UWCritterRules.FollowNearTilesSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.FollowTeleportTilesSquared),
				"A follower beyond this squared tile distance is set down near the target.")]
			public int TeleportTilesSquared = UWCritterRules.FollowTeleportTilesSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.FollowTeleportShortTiles),
				"... this many tiles short of the target.")]
			public int TeleportShortTiles = UWCritterRules.FollowTeleportShortTiles;
		}

		/// <summary>critter.talk.*: goal 10, the creature that wants to talk (UWCritterBrain).</summary>
		public sealed class TalkRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.TurnToTargetDistanceSquared),
				"Within this squared distance in eighths the creature turns to the player.")]
			public int TurnDistanceSquared = UWCritterRules.TurnToTargetDistanceSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.TalkDistanceSquared),
				"Within this one it speaks, if the player faces it.")]
			public int DistanceSquared = UWCritterRules.TalkDistanceSquared;
		}

		/// <summary>critter.spell.*: a casting creature's stand-off and its rolls against its
		/// spell power (UWCritterBrain).</summary>
		public sealed class SpellRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.CasterStandTiles),
				"A magic user stops closing within this many tiles and casts from there.")]
			public int StandTiles = UWCritterRules.CasterStandTiles;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.OrbMageStandTiles),
				"The orb level's mages' stand-off in tiles while the orb stands.")]
			public int OrbMageStandTiles = UWCritterRules.OrbMageStandTiles;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.Spell3Roll),
				"The third spell is cast when a roll of 0 to this minus one is below the spell power.", Minimum = 1)]
			public int ThirdRoll = UWCritterRules.Spell3Roll;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.CasterSpellTilesSquared),
				"Spells 1 and 2 need a squared tile distance below this.")]
			public int NearTilesSquared = UWCritterRules.CasterSpellTilesSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.Spell12Roll),
				"Spell 1 or 2 is cast when a roll of 0 to this minus one is below the spell power.", Minimum = 1)]
			public int FirstSecondRoll = UWCritterRules.Spell12Roll;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.PrimarySpellRoll),
				"Spell 1 rather than 2: a roll of 0 to this minus one below primaryChance.", Minimum = 1)]
			public int PrimaryRoll = UWCritterRules.PrimarySpellRoll;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.PrimarySpellChance),
				"Spell 1 rather than 2: see primaryRoll.")]
			public int PrimaryChance = UWCritterRules.PrimarySpellChance;
		}

		/// <summary>critter.missile.*: a creature's missile (UWCritterBrain) and the damage
		/// charge of every missile's hit (UWCritterCombat.RollMissileDamage).</summary>
		public sealed class MissileRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.MissileTilesSquared),
				"A creature shoots only below this squared tile distance.")]
			public int TilesSquared = UWCritterRules.MissileTilesSquared;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.MissileRoll),
				"It shoots when a roll of 0 to this minus one is at most its dexterity.", Minimum = 1)]
			public int DexterityRoll = UWCritterRules.MissileRoll;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.MissileCharge),
				"A missile's damage is scaled by this over 128, as a blow's by its charge.", Minimum = 0)]
			public int Charge = UWCritterRules.MissileCharge;
		}

		/// <summary>critter.noise.*: how loud and how visible the player is to the creatures
		/// (UWPlayerVitals).</summary>
		public sealed class NoiseRules
		{
			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.PlayerNoiseBase),
				"The player's noise at rest: this minus Sneak / sneakDivisor.")]
			public int Base = UWCritterRules.PlayerNoiseBase;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.PlayerNoiseSneakDivisor),
				"The noise takes the Sneak skill divided by this off.", Minimum = 1)]
			public int SneakDivisor = UWCritterRules.PlayerNoiseSneakDivisor;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.PlayerVisibilityBase),
				"The player's visibility: this minus Sneak / visibilitySneakDivisor (0 to 15).")]
			public int VisibilityBase = UWCritterRules.PlayerVisibilityBase;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.PlayerVisibilitySneakDivisor),
				"The visibility takes the Sneak skill divided by this off.", Minimum = 1)]
			public int VisibilitySneakDivisor = UWCritterRules.PlayerVisibilitySneakDivisor;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.PlayerNoiseSpeedFactor),
				"Moving adds this times the share of the full speed being made.")]
			public int SpeedFactor = UWCritterRules.PlayerNoiseSpeedFactor;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.PlayerNoiseMovingOffset),
				"... and takes this off again, so creeping is quieter than standing.")]
			public int MovingOffset = UWCritterRules.PlayerNoiseMovingOffset;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.PlayerNoiseWater),
				"Noise added in any motion state (swimming, levitating, flying ...).")]
			public int MotionState = UWCritterRules.PlayerNoiseWater;

			[UWRule(typeof(UWCritterRules), nameof(UWCritterRules.PlayerNoiseEasyMove),
				"Noise one step of the easy movement adds to the base.")]
			public int EasyMove = UWCritterRules.PlayerNoiseEasyMove;
		}
	}
}
