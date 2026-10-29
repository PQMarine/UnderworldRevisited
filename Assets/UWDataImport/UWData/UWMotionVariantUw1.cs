namespace UWDataImport.UWData
{
	/// <summary>
	/// THE FIRST GAME'S MOTION STEPS (phase 0 step 2, 2026-10-28): what UWPlayerMotion,
	/// UWPlayerTerrainRules, UWMobileObjectMotion and UWCreatureMotion did at these steps until
	/// then, moved here unchanged.
	/// </summary>
	public sealed class UWMotionVariantUw1 : IUWMotionVariant
	{
		/// <summary>An impact above this hurts (its high byte is the damage).</summary>
		public const int ImpactThreshold = 0x100;

		public int ScaleSpeed(UWRulesMotion.SpeedRules pORules, int piBase, int piState, int piSwimmingSkill)
		{
			return piBase * pORules.TenthsByState[piState] / 10;
		}

		/// <summary>The port's turn step in every state, as UW.EXE's motion was read (UW2 plan phase 2
		/// step 4 left it so).</summary>
		public int ScaleTurn(int piTurnStep, int piState)
		{
			return piTurnStep;
		}

		public const int FacingFollowStep = 0x400;

		public int FacingFollow => FacingFollowStep;

		public bool FallHurts(int piLevel)
		{
			return true;
		}

		public bool IceAndCurrents => false;

		/// <summary>The dragon skin boots keep out the lava, and only the lava.</summary>
		public bool LavaSpares(bool pbDragonSkinBoots)
		{
			return pbDragonSkinBoots;
		}

		/// <summary>The same for a creature and an object.</summary>
		public int ImpactDamage(int piImpact, bool pbCreature, int piHitPoints)
		{
			int liImpact = piImpact & 0xFFFF;

			return liImpact > ImpactThreshold ? liImpact >> 8 : 0;
		}
	}
}
