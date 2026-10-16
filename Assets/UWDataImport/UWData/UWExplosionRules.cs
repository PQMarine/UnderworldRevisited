using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The flames around an explosion - fireball_effect (UnderworldExhumed obj/EFFECT.C). A
	/// fireball that strikes becomes the explosion 0x1C2 (mts_doanim), and three to five flames are
	/// scattered around it: each a copy of the explosion as its next or next but one picture
	/// (0x1C3 or 0x1C4), moved by -2 to +2 eighths each way - rerolled until it stays inside the
	/// tile - and by -8 to +7 zpos steps - and each its own short animation: a start picture r of
	/// 0 to 2 and a length of 2 - r + 0..2 ticks (per user, 2026-10-13: with one length for all
	/// they looked too even).
	///
	/// One rule for the fireball's impact (UWProjectileWorld.ShowImpact, per user 2026-10-13: the
	/// original's impact is three animated parts and more, ours was one) and for Flame Wind
	/// (UWAreaSpellRules, since 2026-10-12).
	/// </summary>
	public static class UWExplosionRules
	{
		/// <summary>The explosion a fireball becomes (ITEM_EXPLOSION_1C2).</summary>
		public const int ExplosionObjectId = 0x1C2;

		/// <summary>rand() % 3 + 2 down to 0: three to five flames.</summary>
		public const int FlameCountRange = 3;

		public const int FlameCountBase = 3;

		/// <summary>A flame's height: the explosion's - 8 + (rand() &amp; 0xF).</summary>
		public const int FlameZDrop = 8;

		public const int FlameZSpread = 0x10;

		/// <summary>add_animobj's length for the explosion (mts_doanim) - the lightning's splash
		/// has the same.</summary>
		public const int ExplosionLengthTicks = 4;

		/// <summary>A flame's start picture: rand() % 3.</summary>
		public const int FlameStartRange = 3;

		/// <summary>A flame's length: 2 - start + rand() % 3.</summary>
		public const int FlameLengthBase = 2;

		public const int FlameLengthRange = 3;

		/// <summary>
		/// How long an animation of the given length shows, in seconds: update_animobj (EFFECT.C)
		/// takes the elapsed ticks off the length and ends it only when the rest is below 0, so a
		/// length of n lasts n + 1 ticks, at UWObjectDamageRules.AnimationTicksPerSecond.
		/// </summary>
		public static float LengthToSeconds(int piLengthTicks)
		{
			return (piLengthTicks + 1) / UWObjectDamageRules.AnimationTicksPerSecond;
		}

		/// <summary>The fine offset each way: rand() % 5 - 2.</summary>
		public const int FineOffsetRange = 5;

		public const int FineOffsetBase = 2;

		/// <summary>One flame: its eighth in the tile, its zpos and its picture.</summary>
		public struct Flame
		{
			public int FineX;

			public int FineY;

			public int ZPos;

			public int ObjectId;

			/// <summary>The picture the animation starts on (0 to 2).</summary>
			public int StartFrame;

			/// <summary>add_animobj's length (0 to 4) - see LengthToSeconds.</summary>
			public int LengthTicks;
		}

		/// <summary>The flames of an explosion at the given eighths of its tile and height, in the
		/// original's order of rolls (count, then per flame the picture, x, y, z, start, length).</summary>
		public static List<Flame> ScatterFlames(int piExplosionId, int piFineX, int piFineY, int piZPos)
		{
			List<Flame> lOFlames = new List<Flame>();
			int liCount = UWRandom.Next(FlameCountRange) + FlameCountBase;

			for (int liAt = 0; liAt < liCount; liAt++)
			{
				Flame lOFlame;

				lOFlame.ObjectId = piExplosionId + UWRandom.Next(2) + 1;
				lOFlame.FineX = fScatterFine(piFineX);
				lOFlame.FineY = fScatterFine(piFineY);
				lOFlame.ZPos = piZPos - FlameZDrop + UWRandom.Next(FlameZSpread);
				lOFlame.StartFrame = UWRandom.Next(FlameStartRange);
				lOFlame.LengthTicks = FlameLengthBase - lOFlame.StartFrame + UWRandom.Next(FlameLengthRange);

				lOFlames.Add(lOFlame);
			}

			return lOFlames;
		}

		/// <summary>A fine coordinate moved by rand() % 5 - 2, rerolled until it stays in 0 to 7.</summary>
		private static int fScatterFine(int piFine)
		{
			int liOffset;

			do
				liOffset = UWRandom.Next(FineOffsetRange) - FineOffsetBase;
			while (piFine + liOffset < 0 || piFine + liOffset > 7);

			return piFine + liOffset;
		}
	}
}
