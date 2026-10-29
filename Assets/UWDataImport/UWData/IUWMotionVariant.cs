namespace UWDataImport.UWData
{
	/// <summary>
	/// THE STEPS OF THE MOTION RULES WHERE THE GAMES PART (Docs/UW2-PLAN.md section 4.3, phase 0
	/// step 2, 2026-10-28), from Underworld Exhumed's list of differences (docs/UW1-UW2-DIFFERENCES.md,
	/// "Movement and physics"). The numbers that differ (the turn rate's divisor, the view's swing of
	/// 0x400 or 0x600 a step, a bounced mover's upward speed) are rule set entries; what only the
	/// second game has (no fall damage in world 8, paralysis, ice and currents) comes with its variant
	/// in phase 4, and the first game's talismans in the lava stay its own.
	/// </summary>
	public interface IUWMotionVariant
	{
		/// <summary>A base speed scaled by the motion state's share (UW1: the rule set's tenths,
		/// PHYSICS.C:582; UW2: twentieths, the swimming share from the Swimming skill,
		/// PHYSICS.C:748-760).</summary>
		int ScaleSpeed(UWRulesMotion.SpeedRules pORules, int piBase, int piState, int piSwimmingSkill);

		/// <summary>The turn rate in a motion state (UW1: the rule set's turn step as it is; UW2: the
		/// state's twentieths below levitation, PHYSICS.C:767; UW2 plan phase 2 step 4).</summary>
		int ScaleTurn(int piTurnStep, int piState);

		/// <summary>How far the view swings towards the motion a step while sliding (UW1 0x400,
		/// PHYSICS.C:411; UW2 0x600, PHYSICS.C:591).</summary>
		int FacingFollow { get; }

		/// <summary>Whether a fall hurts on this 1-based level (UW1: always; UW2: not on 65 to 72,
		/// PHYSICS.C:604).</summary>
		bool FallHurts(int piLevel);

		/// <summary>Whether the ground state rules of the second game run: ice blends the motion, a
		/// current pushes the swimmer (UW2 plan phase 2 step 4, part C; PHYSICS.C:399-470, :561, :578).</summary>
		bool IceAndCurrents { get; }

		/// <summary>Whether the lava spares the player this time (UW1: with the dragon skin boots
		/// on, PLAYMOVE.C:505; UW2: never, PLAYMOVE.C:520).</summary>
		bool LavaSpares(bool pbDragonSkinBoots);

		/// <summary>The damage a moving object's impact does to itself, 0 for none (UW1: above 0x100,
		/// the impact's high byte, OBJPHYS.C:305; UW2: above 0x180, a creature a quarter, an object
		/// of fewer than 0x20 hit points four times, OBJPHYS.C:299-305).</summary>
		int ImpactDamage(int piImpact, bool pbCreature, int piHitPoints);
	}
}
