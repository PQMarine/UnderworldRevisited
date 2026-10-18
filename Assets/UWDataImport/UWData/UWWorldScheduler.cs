using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE GAME'S OWN SEQUENCING (cleanup step 3.4, 2026-10-17): what the world has to do later -
	/// after a number of clock units, once a conversation is over, on every frame of a running
	/// sequence - without a coroutine and without Time.time. (A per-player-tick repeat was built
	/// and removed the same day, per user: the respawn stays in the five-minute block, where its
	/// random draws keep their order.)
	///
	/// WHY: until then the endgame ran as three coroutines on the level loader (the burial polled
	/// the conversation every frame, the void launch waited with WaitForSeconds and kept a static
	/// "launching" flag that a destroyed loader left set), and the trigger spacing compared
	/// Time.time. The user's standing rule (2026-10-17): game logic must never depend on the frame
	/// rate. Everything here runs on the game's own clock, so 60 and 144 frames a second give the
	/// same result, and the scheduler belongs to the session (UWGameSession.Schedule): a new game
	/// or a loaded one drops whatever the previous one had pending, by construction.
	///
	/// THE CLOCK: PIT ticks, 256 a second (UWPlayerTick.PitTicksPerSecond), handed in by the
	/// critter driver once per frame after the creatures and the flying objects ran
	/// (UWCritterDriver), out of the same scaled frame time the creatures' clock is fed from. One
	/// UNIT is the creatures' slot, 16 ticks (UWCritterRules.SlotPitTicks), 1/16 s. Unlike the
	/// creatures' units the scheduler's are NOT halved under the player's Speed and NOT dropped
	/// under Freeze Time, and the easy move's fixed step adds nothing: the coroutines and the
	/// Time.time spacing it replaces ran through all three, and what the player sees stays as it
	/// was. It stands still wherever the game clock holds (menus, death, the conversation) because
	/// the driver then hands in no running frame.
	///
	/// ORDER within one advance: the timed actions by due tick, equal ticks in the order they were
	/// asked for; then the conversation waits; then the per-frame steps. Whatever an action asks
	/// for runs on a later advance at the earliest, even After(0).
	/// </summary>
	public sealed class UWWorldScheduler
	{
		/// <summary>One clock unit in PIT ticks: the creatures' slot (1/16 s).</summary>
		public const int TicksPerUnit = UWCritterRules.SlotPitTicks;

		private struct Timed
		{
			public long Due;

			public long Order;

			public Action Action;
		}

		private sealed class Waiting
		{
			public long Order;

			public bool Armed;

			public Action Action;
		}

		private sealed class Step
		{
			public long Order;

			public Func<bool> Run;
		}

		private readonly List<Timed> mOTimed = new List<Timed>();

		private readonly List<Waiting> mOAfterConversation = new List<Waiting>();

		private readonly List<Step> mOPerFrame = new List<Step>();

		/// <summary>Stamps every request, so that what is asked for during an advance waits for
		/// the next one.</summary>
		private long mlNextOrder;

		/// <summary>The stamp at the start of the running advance.</summary>
		private long mlAdvanceStamp;

		/// <summary>The game time so far, in PIT ticks: only running frames count.</summary>
		public long Now { get; private set; }

		/// <summary>The game time in whole units (Now / TicksPerUnit).</summary>
		public long NowUnits => Now / TicksPerUnit;

		/// <summary>How many one-time actions are still waiting (timed and conversation waits).</summary>
		public int Pending => mOTimed.Count + mOAfterConversation.Count;

		/// <summary>How many per-frame steps are still running.</summary>
		public int RunningSteps => mOPerFrame.Count;

		/// <summary>Runs pAction once, piUnits clock units from now (0: on the next advance).</summary>
		public void After(int piUnits, Action pAction)
		{
			AfterTicks((long)Math.Max(0, piUnits) * TicksPerUnit, pAction);
		}

		/// <summary>Runs pAction once, plTicks PIT ticks from now - for a span that is no whole
		/// number of units.</summary>
		public void AfterTicks(long plTicks, Action pAction)
		{
			if (pAction == null)
				return;

			mOTimed.Add(new Timed { Due = Now + Math.Max(0L, plTicks), Order = mlNextOrder++, Action = pAction });
		}

		/// <summary>
		/// Runs pAction once no conversation is open any more. "NOT YET OPEN" IS HANDLED: the wait
		/// first lets one advance pass, running or not (the coroutine it replaces yielded one frame
		/// before it looked), so a conversation that opens only on the next frame is still waited
		/// for; one that never opens lets the action run on the running advance after that. An open
		/// conversation is no running advance, so the action comes on the first frame the world
		/// runs again - as the coroutine fired on the first frame it found the conversation closed.
		/// </summary>
		public void AfterConversation(Action pAction)
		{
			if (pAction != null)
				mOAfterConversation.Add(new Waiting { Order = mlNextOrder++, Action = pAction });
		}

		/// <summary>
		/// Calls pFStep on every running advance - once a frame while the world runs - until it
		/// returns false. For a sequence whose picture moves every frame (the moongate flight,
		/// UWEndgameSequence); the step reads Now for its timing, so its rule steps stay on the
		/// clock.
		/// </summary>
		public void EveryAdvance(Func<bool> pFStep)
		{
			if (pFStep != null)
				mOPerFrame.Add(new Step { Order = mlNextOrder++, Run = pFStep });
		}

		/// <summary>
		/// One frame of the world: piPitTicks of game time passed. pbWorldRuns is false while the
		/// game clock holds or a conversation is open - then nothing runs and no time passes; only
		/// the conversation waits count the frame (AfterConversation).
		/// </summary>
		public void Advance(int piPitTicks, bool pbWorldRuns)
		{
			mlAdvanceStamp = mlNextOrder;

			if (!pbWorldRuns)
			{
				foreach (Waiting lOWait in mOAfterConversation)
				{
					if (lOWait.Order < mlAdvanceStamp)
						lOWait.Armed = true;
				}

				return;
			}

			Now += Math.Max(0, piPitTicks);

			fRunTimed();
			fRunAfterConversation();
			fRunPerFrame();
		}

		/// <summary>Drops everything pending - nothing the session started runs any more.</summary>
		public void Clear()
		{
			mOTimed.Clear();
			mOAfterConversation.Clear();
			mOPerFrame.Clear();
		}

		/// <summary>The due timed actions, earliest first, equal ticks in the order asked for. Each
		/// leaves the list before it runs, so an action that throws costs only itself.</summary>
		private void fRunTimed()
		{
			while (true)
			{
				int liBest = -1;

				for (int liAt = 0; liAt < mOTimed.Count; liAt++)
				{
					Timed lOEntry = mOTimed[liAt];

					if (lOEntry.Due > Now || lOEntry.Order >= mlAdvanceStamp)
						continue;

					if (liBest < 0 || lOEntry.Due < mOTimed[liBest].Due
						|| (lOEntry.Due == mOTimed[liBest].Due && lOEntry.Order < mOTimed[liBest].Order))
						liBest = liAt;
				}

				if (liBest < 0)
					return;

				Action lAction = mOTimed[liBest].Action;

				mOTimed.RemoveAt(liBest);
				lAction();
			}
		}

		private void fRunAfterConversation()
		{
			Waiting[] lOWaiting = mOAfterConversation.ToArray();

			foreach (Waiting lOWait in lOWaiting)
			{
				if (lOWait.Order >= mlAdvanceStamp)
					continue;

				if (!lOWait.Armed)
				{
					lOWait.Armed = true;
					continue;
				}

				mOAfterConversation.Remove(lOWait);
				lOWait.Action();
			}
		}

		private void fRunPerFrame()
		{
			Step[] lOSteps = mOPerFrame.ToArray();

			foreach (Step lOStep in lOSteps)
			{
				if (lOStep.Order < mlAdvanceStamp && !lOStep.Run())
					mOPerFrame.Remove(lOStep);
			}
		}
	}
}
