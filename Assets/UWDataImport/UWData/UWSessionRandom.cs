using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE RANDOM SOURCE OF ONE SESSION (cleanup step 2.8, 2026-10-14; UWGameSession.Random): the
	/// one seeded generator every rule draws from - combat, loot, the creature brain
	/// (ICritterHost.Random), the motion core (IUWMotionWorld.Random), the conversation's random
	/// (UWConversationVM.IHost.Random) and everything that calls UWRandom. Before, UWRandom kept a
	/// static System.Random seeded from the clock, shared by every game of the process and not
	/// reproducible.
	///
	/// THE GENERATOR IS THE ORIGINAL'S (UWOriginalRandom: Borland's rand, the 32-bit seed times
	/// 0x015A4E35 plus 1, 0 to 0x7FFF from its upper word; the decompilation's game has this one
	/// rand for every roll and seeds it only around the barter). The sequence of a running game is
	/// still not the original's - that depends on every roll the original makes, in its order - but
	/// the generator is now a few lines any port can mirror exactly, where System.Random's algorithm
	/// belongs to the .NET runtime. Ranges are taken the way the original takes them: rand() % n,
	/// which for a power of two is its rand() &amp; (n - 1).
	///
	/// The session's seed decides the whole sequence: the game seeds a new session from the clock
	/// (as the barter reseeds from the wall time), a test or a replay passes its own (Reseed). A test
	/// may also put a scripted source in front (Use); the generator then waits underneath and
	/// continues where it was when the script is taken away again.
	/// </summary>
	public sealed class UWSessionRandom : IUWRandom
	{
		private UWOriginalRandom mOGenerator;

		private IUWRandom mIScript;

		/// <summary>The seed the generator was last started with; the generator takes its lower
		/// word, as the original's InitRNG does.</summary>
		public int Seed { get; private set; }

		public UWSessionRandom(int piSeed)
		{
			Reseed(piSeed);
		}

		/// <summary>A seed from the clock, for a game that should not repeat the previous one.</summary>
		public static int ClockSeed()
		{
			return Environment.TickCount;
		}

		/// <summary>Starts the generator anew from piSeed and drops a scripted source.</summary>
		public void Reseed(int piSeed)
		{
			Seed = piSeed;
			mOGenerator = new UWOriginalRandom(piSeed);
			mIScript = null;
		}

		/// <summary>Draws from pIScript until it is taken away again (null, or this generator
		/// itself, takes it away).</summary>
		public void Use(IUWRandom pIScript)
		{
			mIScript = ReferenceEquals(pIScript, this) ? null : pIScript;
		}

		/// <summary>What the draws come from: the scripted source if there is one, else this.</summary>
		public IUWRandom Active
		{
			get { return mIScript ?? this; }
		}

		/// <summary>0 up to (excluding) piMaxExclusive: rand() % n. Every call is one roll, as it
		/// was with System.Random (a range of 0 or 1 rolls too and gives 0); a range beyond the
		/// original's 0x8000 takes a second roll for 15 more bits.</summary>
		public int Next(int piMaxExclusive)
		{
			if (mIScript != null)
				return mIScript.Next(piMaxExclusive);

			if (piMaxExclusive < 0)
				throw new ArgumentOutOfRangeException(nameof(piMaxExclusive));

			int liRoll = mOGenerator.Next();

			if (piMaxExclusive <= UWOriginalRandom.Range)
				return piMaxExclusive == 0 ? 0 : liRoll % piMaxExclusive;

			long llWide = ((long)liRoll << 15) | (long)mOGenerator.Next();

			return (int)(llWide % piMaxExclusive);
		}

		/// <summary>piMin up to (excluding) piMaxExclusive; an empty range gives piMin.</summary>
		public int Next(int piMin, int piMaxExclusive)
		{
			if (mIScript != null)
				return mIScript.Next(piMin, piMaxExclusive);

			if (piMin > piMaxExclusive)
				throw new ArgumentOutOfRangeException(nameof(piMin));

			return piMin + Next(piMaxExclusive - piMin);
		}

		/// <summary>0 up to (excluding) 1: one draw over the original's range.</summary>
		public double NextDouble()
		{
			if (mIScript != null)
				return mIScript.NextDouble();

			return mOGenerator.Next() / (double)UWOriginalRandom.Range;
		}
	}
}
