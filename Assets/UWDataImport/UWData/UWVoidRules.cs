namespace UWDataImport.UWData
{
	/// <summary>
	/// The effects of the Ethereal Void on level 9 (per user, 2026-09-13: "Do the effects
	/// on level 9"), engine-free since cleanup step 4.5 (2026-10-17; UWVoidEffects drew them itself
	/// and counted its ticks on Time.deltaTime until then - now on the game's PIT ticks).
	///
	/// FOLLOWING UW.EXE (UW1_asm.asm, EtherealVoidSpecialEffects_seg008_150, called from
	/// the player update tick, see UWPlayerTick): on every game tick on level 9, with a chance
	/// of 1 in 32 (RNG and 0x1F equal to zero):
	///   Flash         palette colour 0xB5 in the view window
	///   Life drain    above 100 points 0 to 5, above 50 then 0 to 3, above 20 with chance 1 in 4
	///                 0 to 2; otherwise, as long as more than 1 remains, with chance 1 in 8 exactly one
	///   Quake         with chance 11 in 12, large channel, duration 15 plus 0 to 29
	///   Compass       jumps to a random one of the 16 directions
	/// The reference (etherealvoid.cs) ALWAYS quakes and, above 20 points, subtracts nothing
	/// when the roll fails - there it deviates from the disassembly; UW.EXE is followed.
	///
	/// THE TICK RATE is not established: how often the game tick ran in the original depends on the
	/// machine, and the reference itself notes it as unresolved. It was therefore kept in UWSettings
	/// (VoidEffectTicksPerSecond, 32); since cleanup phase 10 (2026-10-26) it is the rule set's
	/// vitals.void.ticksPerSecond, TicksPerSecond its default.
	/// </summary>
	public sealed class UWVoidRules
	{
		/// <summary>What one game tick in the Void brings.</summary>
		public struct Effect
		{
			/// <summary>The hit points the Void takes (UWDamageRequest.VoidDrain), 0 for none.</summary>
			public int Drain;

			/// <summary>The quake's length on the large channel, 0 for none.</summary>
			public int QuakeTicks;

			/// <summary>The compass's new direction, 0 to 15.</summary>
			public int CompassStep;
		}

		/// <summary>The flash of an effect.</summary>
		public const int FlashColour = 0xB5;

		public const float FlashSeconds = 0.08f;

		public const int CompassSteps = 16;

		/// <summary>The Void's game ticks per second - about one effect a second (per user,
		/// 2026-09-14); see the class.</summary>
		public const int TicksPerSecond = 32;

		/// <summary>The game ticks not yet run, in fractions.</summary>
		private double mdTicks;

		/// <summary>
		/// How many of the Void's game ticks piPitTicks of game time bring at pfTicksPerSecond
		/// (vitals.void.ticksPerSecond); the fraction carries over. Nothing at a rate of
		/// zero or below.
		/// </summary>
		public int Advance(int piPitTicks, float pfTicksPerSecond)
		{
			if (pfTicksPerSecond <= 0f || piPitTicks <= 0)
				return 0;

			mdTicks += piPitTicks * (double)pfTicksPerSecond / UWPlayerTick.PitTicksPerSecond;

			int liTicks = UWRounding.Floor(mdTicks);

			mdTicks -= liTicks;

			return liTicks;
		}

		/// <summary>Off level 9 or while the view rests: the count starts afresh.</summary>
		public void Reset()
		{
			mdTicks = 0;
		}

		/// <summary>
		/// One game tick in the Void: the one-in-32 roll, then - when it comes - the drain from
		/// piHitPoints, the quake and the compass, drawn from UWRandom in UW.EXE's order. False
		/// when nothing happens this tick.
		/// </summary>
		public static bool Tick(int piHitPoints, out Effect pOEffect)
		{
			pOEffect = default;

			if ((UWRandom.Next(0x8000) & 0x1F) != 0)
				return false;

			int liNew = piHitPoints;

			if (piHitPoints > 0x64)
				liNew -= UWRandom.Next(6);
			else if (piHitPoints > 0x32)
				liNew -= UWRandom.Next(4);
			else if (piHitPoints > 0x14 && (UWRandom.Next(0x8000) & 3) < 1)
				liNew -= UWRandom.Next(3);
			else if (piHitPoints > 1 && (UWRandom.Next(0x8000) & 7) == 0)
				liNew -= 1;

			pOEffect.Drain = piHitPoints - liNew;

			if (UWRandom.Next(12) != 0)
				pOEffect.QuakeTicks = 0xF + UWRandom.Next(0x1E);

			pOEffect.CompassStep = UWRandom.Next(CompassSteps);

			return true;
		}
	}
}
