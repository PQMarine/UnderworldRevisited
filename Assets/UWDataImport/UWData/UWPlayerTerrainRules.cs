namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT THE GROUND DOES TO THE PLAYER: the swim counter with its drowning, and the lava's rolls
	/// (cleanup step 4.4, 2026-10-17). Engine-free out of UWPlayerTerrain, which drove both on clocks
	/// of its own - the drowning on a 21-second UWTickClock out of phase with the player tick, the
	/// lava on Time.time. Now the drowning runs ON the player tick, where the original has it, and
	/// the lava counts the game's PIT ticks (UWWorldScheduler.Now), so no rule hangs on the frame
	/// rate or the motion mode (Docs/DEVIATIONS.md, Rules; the user's decision of 2026-10-17).
	///
	/// ONE SWIM COUNTER: the motion core kept a write-only copy (UWPlayerMotion.SwimCounter) beside
	/// the one the view, the missiles and the save game read; it is gone.
	///
	/// THE SWIM COUNTER of the original, PLAYER.DAT 0xB9 (per user, 2026-09-17: "the view really
	/// sinks", checked in the original). Replaces the guessed exhaustion (fixed time, then damage
	/// every two seconds) that stood in UWPlayerTerrain before.
	///
	/// UW.EXE:
	///   - Entering water sets it to 0x60 (SetPlayerDataOxB9, whose state part is the motion
	///     core's, UWPlayerMotion), leaving it to 0.
	///   - The camera sits 0xA4 above the body minus the counter (seg034_2F89_971), so it is
	///     the counter that lowers the view in the water at all. One unit is an eighth of a zpos
	///     step, a quarter of our world unit.
	///   - In the periodic player update (seg028_2985_24C), while the counter is above 0x50,
	///     SwimSkillCheck_seg028_4AE runs: swimming skill against (carried weight * 32) / carrying
	///     capacity. On a failure it rises by (3 - result) four-sided dice - three on a failure,
	///     four on a critical one - but only while it is still below 0x8C.
	///   - Above 0x78 a second check follows; everything but a critical success costs
	///     (2 - result) + 2 sided dice twice in hit points with a flash in palette colour 0xC6 (blue in game).
	/// So the view sinks with every failed check, and a heavily loaded, unpractised swimmer
	/// drowns sooner.
	/// </summary>
	public sealed class UWPlayerTerrainRules
	{
		/// <summary>The swim counter PD[0xB9] on entering water (SetPlayerDataOxB9; water_set,
		/// UnderworldExhumed motion/PHYSICS.C). Moved from UWPlayerMotion 2026-10-17.</summary>
		public const int SwimCounterOnEntering = 0x60;

		/// <summary>While the counter is above this, the periodic player update
		/// (seg028_2985_24C) runs the swim checks, and one hangs deeper in the water than the
		/// feet position says (UWReachRules; until 2026-10-11 declared there and on the Unity
		/// side as well). Moved from UWPlayerMotion 2026-10-17.</summary>
		public const int SwimCounterCheckThreshold = 0x50;

		/// <summary>A failed check raises the counter only while it is below this.</summary>
		public const int SwimCounterRiseLimit = 0x8C;

		/// <summary>Above this the second check may drown.</summary>
		public const int SwimCounterDrownThreshold = 0x78;

		/// <summary>The lava rolls at most this far behind: after a pause on the lava the backlog
		/// is dropped (a quarter second, as UWPlayerTerrain had it on Time.time).</summary>
		public const int LavaBacklogTicks = UWPlayerTick.PitTicksPerSecond / 4;

		/// <summary>THE LAVA'S NUMBERS (see TickLava), the defaults of the rule set's vitals.lava.*
		/// (cleanup phase 10, 2026-10-26; UWSettings.LavaDamageInterval, LavaDamageChance and
		/// LavaDamage until then): a roll every 1/32 s, 8 PIT ticks - UW.EXE rolls once per game frame
		/// (see TickLava), and 1/32 s is the game loop rate the Void's effects assume too (per user,
		/// 2026-09-14; the earlier 0.5 s did far too little damage); one in five hits; a hit does one
		/// point of fire.</summary>
		public const int LavaRollTicks = UWPlayerTick.PitTicksPerSecond / 32;

		public const int LavaHitOneIn = 5;

		public const int LavaHitDamage = 1;

		/// <summary>PLAYER.DAT 0xB9 - the view's dip, the missiles' launch height, the save game.</summary>
		public int SwimCounter { get; private set; }

		private bool mbWasSwimming;

		/// <summary>The game time so far, in PIT ticks, as handed to TickLava.</summary>
		private long mlLavaNow;

		/// <summary>When the next lava roll is due; meaningful while mbLavaRunning.</summary>
		private long mlNextLavaRoll;

		private bool mbLavaRunning;

		/// <summary>Takes the counter from a loaded save game, so a character saved while swimming
		/// keeps its sunken view and does not start over at 0x60.</summary>
		public void LoadSwimCounter(int piCounter)
		{
			SwimCounter = piCounter & 0xFF;
			mbWasSwimming = SwimCounter > 0;
		}

		/// <summary>Whether the player swims now (the core's contact is water, no Water Walk):
		/// entering sets the counter to 0x60, leaving to 0 - the original's water_set and
		/// parse_player_terr; the water's edge (state 0x20) swims not, as the original keeps its
		/// counter at 0 there.</summary>
		public void UpdateSwimming(bool pbSwimming)
		{
			if (pbSwimming == mbWasSwimming)
				return;

			SwimCounter = pbSwimming ? SwimCounterOnEntering : 0;
			mbWasSwimming = pbSwimming;
		}

		/// <summary>
		/// The swim check on the player tick (PLAYTIME.C duration_check: "if swim_count > 0x50
		/// sink_sink_sink", after the regeneration and before the minute block): piSkill is the
		/// Swimming skill, piLoad the carried weight * 32 / the capacity. Returns the drowning
		/// damage, 0 for none - the caller lands it as UWDamageRequest.Drowning with its flash.
		/// The draws as the original's: the check, the counter's dice on a failure, the second
		/// check above 0x78, the damage's dice.
		/// </summary>
		public int SwimCheck(int piSkill, int piLoad)
		{
			if (SwimCounter <= SwimCounterCheckThreshold)
				return 0;

			// The rise limit and the drowning threshold are the rule set's (vitals.swim.*, cleanup
			// phase 10); the consts above are their defaults.
			UWRulesVitals.SwimRules lORules = UWRules.Current.Vitals.Swim;
			UWSkillCheck.ResultEnum leResult = UWSkillCheck.Check(piSkill, piLoad);

			if (!UWSkillCheck.IsSuccess(leResult) && SwimCounter < lORules.RiseBelow)
				SwimCounter = (SwimCounter + UWRandom.RollDice(3 - (int)leResult, 4)) & 0xFF;

			if (SwimCounter <= lORules.DrownAbove)
				return 0;

			int liSeverity = 2 - (int)UWSkillCheck.Check(piSkill, piLoad);

			if (liSeverity == 0)
				return 0;

			return UWRandom.RollDice(2, liSeverity + 2);
		}

		/// <summary>
		/// THE LAVA, piPitTicks of game time later: how many of its rolls hit. In the original a roll
		/// is made per game frame (PLAYMOVE.C parse_effect: rand() % 5 == 0, unless the dragon skin
		/// boots are worn) - faster on a faster machine. Ours rolls at a fixed rate, every
		/// piIntervalTicks (vitals.lava.rollTicks, LavaRollTicks: 1/32 s = 8 ticks, about what the original
		/// reaches in DOSBox: the user measured 69 of 78 hit points in 10 s in both, 2026-10-17), with
		/// the chance pdChance (one in five). The first roll comes on the step onto the lava, a
		/// backlog of more than a quarter second is dropped, and off the lava (or in the boots)
		/// nothing is due. Each hit is one UWDamageRequest.Lava of the caller's.
		/// </summary>
		public int TickLava(int piPitTicks, bool pbOnLava, bool pbDragonSkinBoots, int piIntervalTicks, double pdChance)
		{
			mlLavaNow += System.Math.Max(0, piPitTicks);

			// THE DRAGON SKIN BOOTS KEEP OUT THE LAVA - and only the lava. The reference checks
			// their flag right before the lava damage (motion_player) and nowhere else;
			// a fireball still hits. Consistent with that, the boots themselves carry the
			// fire resistance in comobj.dat.
			if (!pbOnLava || pbDragonSkinBoots)
			{
				mbLavaRunning = false;

				return 0;
			}

			if (!mbLavaRunning || mlNextLavaRoll < mlLavaNow - LavaBacklogTicks)
				mlNextLavaRoll = mlLavaNow;

			mbLavaRunning = true;

			int liInterval = System.Math.Max(1, piIntervalTicks);
			int liHits = 0;

			while (mlLavaNow >= mlNextLavaRoll)
			{
				mlNextLavaRoll += liInterval;

				// In the original a roll is made per game frame and only in one of five a single
				// point is subtracted. So lava hurts, but does not kill at the first step.
				if (UWRandom.NextDouble() < pdChance)
					liHits++;
			}

			return liHits;
		}

		/// <summary>An interval in seconds as PIT ticks (at least one) - what UWSettings.LavaDamageInterval
		/// was converted with until cleanup phase 10.</summary>
		public static int SecondsToTicks(float pfSeconds)
		{
			return System.Math.Max(1, UWRounding.HalfToEven(pfSeconds * UWPlayerTick.PitTicksPerSecond));
		}
	}
}
