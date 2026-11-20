using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE AID "WALK THROUGH PEACEFUL PEOPLE" (per user 2026-11-02 as an idea, planned and decided 2026-11-19): in the
	/// second game's castle, Britannia's first level, peaceful people stand in the passages and the original makes the
	/// player wait or walk around them. With the aid (Gameplay, Aids; off by default, both schemes, every motion mode)
	/// the player and a creature that is not turned against him pass through each other there.
	///
	/// WHO IS PEACEFUL: the conversation's own test, UWClickRules.IsAgainstThePlayer - not hostile, not attacking,
	/// withdrawing from or making a stand against the player; an ally counts as peaceful.
	///
	/// WHERE: only UW2's level levels.passPeacefulUw2 of the rule set (0-based, the castle by default), so a mod can
	/// repoint it. Nothing changes in the first game.
	///
	/// HOW (the host, UWMotionHost): a pass test on the cores that MOVE the player or a creature
	/// (UWMotionCore.PassesThrough, UWCharacterMotor.PassesThrough) - never on the blow's core, the doors' or the fit
	/// tests, so a peaceful one can still be struck and a door still meets him. Null, the cores are the original's.
	/// </summary>
	public static class UWCreaturePass
	{
		/// <summary>Where the setting is read (the Unity layer's settings; the self-check its own); null reads off.</summary>
		public static Func<bool> EnabledSource { get; set; }

		public static bool Enabled => EnabledSource != null && EnabledSource();

		/// <summary>The castle, Britannia's first level of the second game (0-based); levels.passPeacefulUw2.</summary>
		public const int LevelUw2 = 0;

		/// <summary>Whether the aid works on this level of this game: the second game's level of the rule set.</summary>
		public static bool WorksOn(UWGame peGame, int piLevel, int piRuleLevel)
		{
			return peGame == UWGame.Uw2 && piLevel == piRuleLevel;
		}

		/// <summary>Whether a creature lets the player through: not turned against him (UWClickRules).</summary>
		public static bool IsPeaceful(int piGoal, int piGTarg, int piAttitude, bool pbAlly)
		{
			return !UWClickRules.IsAgainstThePlayer(piGoal, piGTarg, piAttitude, pbAlly);
		}

		/// <summary>
		/// Whether the player and a creature overlap, in eighths of a tile (the cores' boxes: a radius of r reaches r + 1/2
		/// eighths from the centre, a creature's box one eighth smaller): such a pair keeps passing until it is apart, so
		/// nobody sticks when a creature turns hostile in the middle of a passage or the aid is switched off.
		/// </summary>
		public static bool Overlap(int piPlayerX8, int piPlayerY8, int piPlayerRadius, int piCreatureX8, int piCreatureY8,
			int piCreatureRadius)
		{
			int liReach = (piPlayerRadius & 7) + Math.Max((piCreatureRadius & 7) - 1, 0);

			return Math.Abs(piPlayerX8 - piCreatureX8) <= liReach && Math.Abs(piPlayerY8 - piCreatureY8) <= liReach;
		}
	}
}
