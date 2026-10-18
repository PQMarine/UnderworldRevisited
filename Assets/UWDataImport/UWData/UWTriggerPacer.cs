using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UWDataImport.UWData
{
	/// <summary>
	/// How often one move trigger fires at most while a mover stays in its reach (cleanup step 3.4,
	/// 2026-10-17; until then two Time.time dictionaries in UWProjectileWorld, UWMotionHost since
	/// step 3.5).
	///
	/// From PlayerTriggerSpacingSeconds: once per 16 PIT ticks, the original's frame at the
	/// user's DOSBox reference. The original fires per scan, so its rate follows its
	/// frame rate; ours runs the motion once per rendered frame in Smooth mode, and at 144 frames a
	/// second a damage trigger would hurt nine times as often.
	///
	/// ON THE GAME'S CLOCK NOW (the user's rule of 2026-10-17: game logic never depends on the
	/// frame rate): a trigger fires again once the scheduler's clock (UWWorldScheduler.Now) has
	/// crossed into a new unit of 16 ticks since its last firing - once per original frame. The
	/// Time.time test "at least 1/16 s since" overshot by up to a frame at every firing, so the
	/// rate it allowed depended on the frame rate (every fourth frame at 60 fps, 15 a second;
	/// every ninth or tenth at 144 fps); per unit it is 16 a second at any frame rate above 16.
	/// The clock is the session's game time, which runs under Freeze Time and Speed as Time.time
	/// did and stands still with the game clock.
	/// </summary>
	public sealed class UWTriggerPacer
	{
		/// <summary>The spacing: one clock unit, 16 PIT ticks (16/256 s).</summary>
		public const int SpacingTicks = UWWorldScheduler.TicksPerUnit;

		private struct Key
		{
			public object Trigger;

			public int Mover;
		}

		private sealed class KeyComparer : IEqualityComparer<Key>
		{
			public bool Equals(Key pA, Key pB)
			{
				return ReferenceEquals(pA.Trigger, pB.Trigger) && pA.Mover == pB.Mover;
			}

			public int GetHashCode(Key pKey)
			{
				return (RuntimeHelpers.GetHashCode(pKey.Trigger) * 397) ^ pKey.Mover;
			}
		}

		private readonly Dictionary<Key, long> mOFiredAt = new Dictionary<Key, long>(new KeyComparer());

		private readonly int miForgetAbove;

		/// <param name="piForgetAbove">BeginBatch forgets every firing once more than this many
		/// are remembered (64 for the player's triggers, 256 for the others', as before).</param>
		public UWTriggerPacer(int piForgetAbove)
		{
			miForgetAbove = piForgetAbove;
		}

		/// <summary>How many firings are remembered.</summary>
		public int Count => mOFiredAt.Count;

		/// <summary>At the start of a batch of queued triggers: keeps the memory small.</summary>
		public void BeginBatch()
		{
			if (mOFiredAt.Count > miForgetAbove)
				mOFiredAt.Clear();
		}

		/// <summary>May this mover's trigger fire at plNow (PIT ticks)? Yes when it never fired,
		/// when the clock crossed into a new unit since, or when the clock started afresh.</summary>
		public bool IsReady(object pOTrigger, int piMover, long plNow)
		{
			long llLast;

			if (pOTrigger == null || !mOFiredAt.TryGetValue(new Key { Trigger = pOTrigger, Mover = piMover }, out llLast))
				return true;

			return IsSpaced(llLast, plNow);
		}

		/// <summary>Remembers that the trigger fired at plNow.</summary>
		public void MarkFired(object pOTrigger, int piMover, long plNow)
		{
			if (pOTrigger != null)
				mOFiredAt[new Key { Trigger = pOTrigger, Mover = piMover }] = plNow;
		}

		/// <summary>The spacing rule alone: a later unit than the last firing's, or a clock that
		/// went back (a new session starts at zero).</summary>
		public static bool IsSpaced(long plLast, long plNow)
		{
			return plNow < plLast || (plNow / SpacingTicks) != (plLast / SpacingTicks);
		}
	}
}
