using System;

namespace UWDataImport.UWData
{
	/// <summary>A source of random numbers for the rules. See UWRandom.</summary>
	public interface IUWRandom
	{
		/// <summary>0 up to (excluding) piMaxExclusive.</summary>
		int Next(int piMaxExclusive);

		/// <summary>piMin up to (excluding) piMaxExclusive.</summary>
		int Next(int piMin, int piMaxExclusive);

		/// <summary>0 up to (excluding) 1.</summary>
		double NextDouble();
	}

	/// <summary>
	/// THE ONE random source of the engine-free rules (P0 of the engine separation, 2026-09-17).
	/// Before, combat, loot, damage types, experience, skill checks and character creation each
	/// kept their own System.Random, so no run could be repeated.
	///
	/// The host may replace it: a test driver or a later comparison with a C++ port sets a seeded
	/// source with UseSeed or its own implementation with Use. The game keeps the default, a
	/// System.Random seeded from the clock, which behaves exactly like the separate ones did.
	///
	/// NOT the generator of UW.EXE for the running game - its sequence depends on every roll the
	/// original makes, in its order. Where the original SEEDS its generator with a fixed value
	/// and rolls a known sequence from it (the barter), UWOriginalRandom gives the same numbers.
	/// </summary>
	public static class UWRandom
	{
		private sealed class SystemRandomSource : IUWRandom
		{
			private readonly Random mORandom;

			public SystemRandomSource(Random pORandom)
			{
				mORandom = pORandom;
			}

			public int Next(int piMaxExclusive)
			{
				return mORandom.Next(piMaxExclusive);
			}

			public int Next(int piMin, int piMaxExclusive)
			{
				return mORandom.Next(piMin, piMaxExclusive);
			}

			public double NextDouble()
			{
				return mORandom.NextDouble();
			}
		}

		private static IUWRandom msSource = new SystemRandomSource(new Random());

		/// <summary>The source the rules draw from.</summary>
		public static IUWRandom Source
		{
			get { return msSource; }
		}

		/// <summary>Replaces the source; null restores an unseeded one.</summary>
		public static void Use(IUWRandom pISource)
		{
			msSource = pISource ?? new SystemRandomSource(new Random());
		}

		/// <summary>A repeatable source: the same seed gives the same sequence.</summary>
		public static void UseSeed(int piSeed)
		{
			msSource = new SystemRandomSource(new Random(piSeed));
		}

		public static int Next(int piMaxExclusive)
		{
			return msSource.Next(piMaxExclusive);
		}

		public static int Next(int piMin, int piMaxExclusive)
		{
			return msSource.Next(piMin, piMaxExclusive);
		}

		public static double NextDouble()
		{
			return msSource.NextDouble();
		}

		/// <summary>The game's usual dice: count plus count rolls of 0 to range - 1. A range of
		/// zero or less adds nothing.</summary>
		public static int RollDice(int piCount, int piRange)
		{
			int liTotal = piCount;

			if (piRange <= 0)
				return liTotal;

			for (int liAt = 0; liAt < piCount; liAt++)
				liTotal += Next(piRange);

			return liTotal;
		}
	}

	/// <summary>
	/// THE GENERATOR OF UW.EXE (read 2026-09-29): RNG_seg005_DE7 is Borland's rand - the 32-bit
	/// seed times 0x015A4E35 plus 1, the result its upper word masked to 0 to 0x7FFF - and
	/// InitRNG_seg005_10B2_DD6 sets the seed's lower word and clears the upper one. Only for the
	/// places that seed it with a known value and then roll right away: the barter seeds with the
	/// partner's and each item's object number, so those rolls come out as in the original.
	/// </summary>
	public sealed class UWOriginalRandom
	{
		/// <summary>The largest result plus one - the original divides by it for a fraction.</summary>
		public const int Range = 0x8000;

		private uint muSeed;

		/// <summary>InitRNG: the seed as a 16-bit word.</summary>
		public UWOriginalRandom(int piSeed)
		{
			muSeed = (ushort)piSeed;
		}

		/// <summary>RNG: 0 to 0x7FFF.</summary>
		public int Next()
		{
			muSeed = unchecked((muSeed * 0x015A4E35u) + 1u);

			return (int)((muSeed >> 16) & 0x7FFF);
		}

		/// <summary>
		/// The barter's noise (ovr095_17B9, see UWConversationTrade.fRandomOffset): the value
		/// moved by a percentage between piLower and piUpper, the fraction RNG * (upper - lower) /
		/// 0x8000 in 32 bits; the product with the value is cut to 16 bits (CWD after the IMUL)
		/// before it is divided by 100 towards zero.
		/// </summary>
		public int Noise(int piValue, int piLower, int piUpper)
		{
			int liPercent = (short)(piLower + (int)(((long)Next() * (piUpper - piLower)) / Range));

			return (short)(piValue + ((short)(piValue * liPercent) / 100));
		}
	}
}
