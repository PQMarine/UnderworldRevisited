using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S DEATH (player_is_dead, uw2 game/SKILLS.C:802): a ninth of the experience goes, and outside
	/// Britannia (above the eighth level) the player wakes by the blackrock gem on level 5 (do_gem, :667), near
	/// 28/40; in Britannia it is the end (real_death). Jail, for one killed by a castle guard on level 1, and waking
	/// from the Ethereal Void come with those systems. The numbers at the gem are do_resurrect's, UWDeathRules'.
	/// </summary>
	public static class UWDeathRulesUw2
	{
		public enum OutcomeEnum
		{
			Gem,

			Final
		}

		/// <summary>The last level of Britannia, the first world.</summary>
		public const int LastBritanniaLevel = 8;

		/// <summary>The blackrock gem's level and the place do_gem moves the player from.</summary>
		public const int GemLevel = 5;

		public const int GemTileX = 0x1C;

		public const int GemTileY = 0x28;

		/// <summary>move_along(rand(), 8): how far from that place.</summary>
		public const int GemDistance = 8;

		/// <summary>"You regain awareness in the cavern containing the pulsating blackrock gem."</summary>
		public const int RegainString = 0x169;

		private const int ExperienceLossDivisor = 9;

		/// <summary>ITEM_BLOOD_STAIN_DF, left where the player wakes.</summary>
		public const int WakingStain = 0xDF;

		public static OutcomeEnum Decide(int piLevel)
		{
			return piLevel > LastBritanniaLevel ? OutcomeEnum.Gem : OutcomeEnum.Final;
		}

		public static int ExperienceLoss(int piExperience)
		{
			return piExperience / ExperienceLossDivisor;
		}

		/// <summary>move_along (sys/UTIL.C:35): the point piDistance from the tile at the heading, of 256 to the circle
		/// and counted back from 0x140; a step that moves at all gains one.</summary>
		public static void MoveAlong(int piHeading, int piDistance, ref int piX, ref int piY)
		{
			double lfAngle = ((0x140 - piHeading) & 0xFF) * Math.PI * 2.0 / 256.0;

			piY += fStep(Math.Sin(lfAngle), piDistance);
			piX += fStep(Math.Cos(lfAngle), piDistance);
		}

		private static int fStep(double pfUnit, int piDistance)
		{
			int liStep = ((int)(pfUnit * 0x7FFF) / 0x80 * piDistance) / 0x100;

			return liStep > 0 ? liStep + 1 : liStep < 0 ? liStep - 1 : 0;
		}
	}
}
