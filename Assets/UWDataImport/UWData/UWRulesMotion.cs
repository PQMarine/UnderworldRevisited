namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MOTION RULES of the rule set (motion.*; cleanup phase 10 wave 2, 2026-10-26): the
	/// player's motion core as the original has it - the base speeds and their share per motion
	/// state, the turn step, the ramp, flying, the jump and what an impact does (UWPlayerMotion,
	/// UWImpactRules). Each default is the const of the class that applies it; see UWRules. Speeds
	/// are in 1/65536 tile per PIT tick, as the core counts them. The Smooth mode's own tuning (its
	/// ramp frame, the jump's Euler offset, the fine ticks) is not a rule and stays with the motion.
	/// </summary>
	public sealed class UWRulesMotion
	{
		public readonly SpeedRules Speed = new SpeedRules();

		[UWRule(typeof(UWPlayerMotion), nameof(UWPlayerMotion.TurnStep),
			"How fast the turn keys turn the view: (ticks * this) * (turn input / 4) / 4 per frame.", Minimum = 0)]
		public int TurnStep = UWPlayerMotion.TurnStep;

		[UWRule(typeof(UWPlayerMotion), nameof(UWPlayerMotion.FullMotionWeight),
			"The speed change one frame may make (the start and stop ramp), less above half the carrying capacity.",
			Minimum = 1)]
		public int MotionWeight = UWPlayerMotion.FullMotionWeight;

		public readonly JumpRules Jump = new JumpRules();

		public readonly ImpactRules Impact = new ImpactRules();

		/// <summary>motion.speed.*: forward, slide and back are the base speeds; each motion state
		/// takes tenthsByState tenths of them; fly is the vertical speed of flying up and down.</summary>
		public sealed class SpeedRules
		{
			/// <summary>THE RUN'S 16-BIT CEILING (2026-10-26, per user: with 1880 W stood still while
			/// the walk, the slide and the back went): the run's target is I16((Walk >> 2) * speed)
			/// as in the original, and Walk >> 2 is at most 0x70 >> 2 = 28, so 32767 / 28 = 1170 is
			/// the most it holds - with the state's tenths at most 10.</summary>
			public const int MaxForward = 32767 / (UWPlayerMotion.WalkRun >> 2);

			[UWRule(typeof(UWPlayerMotion), nameof(UWPlayerMotion.BaseForwardSpeed),
				"The full forward speed of the run, before the motion state's share.", Minimum = 0,
				Maximum = MaxForward)]
			public int Forward = UWPlayerMotion.BaseForwardSpeed;

			[UWRule(typeof(UWPlayerMotion), nameof(UWPlayerMotion.BaseSlideSpeed),
				"The sideways speed, before the motion state's share.", Minimum = 0)]
			public int Slide = UWPlayerMotion.BaseSlideSpeed;

			[UWRule(typeof(UWPlayerMotion), nameof(UWPlayerMotion.BaseBackSpeed),
				"The backwards speed, before the motion state's share.", Minimum = 0)]
			public int Back = UWPlayerMotion.BaseBackSpeed;

			[UWRule(typeof(UWPlayerMotion), nameof(UWPlayerMotion.SpeedRatio),
				"Tenths of the speeds by motion state: normal, swimming, lava, (unused), levitating, flying, slow falling (7 numbers).",
				Minimum = 0, Maximum = 10)]
			public int[] TenthsByState = (int[])UWPlayerMotion.SpeedRatio.Clone();

			[UWRule(typeof(UWPlayerMotion), nameof(UWPlayerMotion.FlySpeed),
				"The vertical speed of flying up or down.", Minimum = 0)]
			public int Fly = UWPlayerMotion.FlySpeed;
		}

		/// <summary>motion.jump.*: the take-off's vertical speed (cut near the ceiling of the map's
		/// height range) and the gravity of the jump, lighter under Leap.</summary>
		public sealed class JumpRules
		{
			[UWRule(typeof(UWPlayerMotion), nameof(UWPlayerMotion.JumpSpeed),
				"The vertical speed a jump takes off with.", Minimum = 0)]
			public int Speed = UWPlayerMotion.JumpSpeed;

			[UWRule(typeof(UWPlayerMotion), nameof(UWPlayerMotion.JumpGravity),
				"The gravity of a jump, per tick (negative pulls down).")]
			public int Gravity = UWPlayerMotion.JumpGravity;

			[UWRule(typeof(UWPlayerMotion), nameof(UWPlayerMotion.LeapGravity),
				"The gravity of a jump under the Leap spell.")]
			public int LeapGravity = UWPlayerMotion.LeapGravity;
		}

		/// <summary>motion.impact.*: a wall or the floor hit hard - the value is the momentum lost
		/// / 256, doubled while falling; a passed Acrobat check against value * acrobatCheckFactor
		/// keeps (acrobatFull - Acrobat) / acrobatFull of it; above damageThreshold it is damage.</summary>
		public sealed class ImpactRules
		{
			[UWRule(typeof(UWImpactRules), nameof(UWImpactRules.AcrobatCheckFactor),
				"The Acrobat check's target is the impact value times this.")]
			public int AcrobatCheckFactor = UWImpactRules.AcrobatCheckFactor;

			[UWRule(typeof(UWImpactRules), nameof(UWImpactRules.AcrobatFull),
				"An Acrobat of this much takes the whole impact away on a passed check.", Minimum = 1)]
			public int AcrobatFull = UWImpactRules.AcrobatFull;

			[UWRule(typeof(UWImpactRules), nameof(UWImpactRules.DamageThreshold),
				"An impact value above this hurts the player by that value.")]
			public int DamageThreshold = UWImpactRules.DamageThreshold;
		}
	}
}
