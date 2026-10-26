namespace UWDataImport.UWData
{
	/// <summary>
	/// What the stepper runs: one frame of the player's motion. The host (UWPlayerMovement) sets
	/// the command, runs UWPlayerMotion.Frame and acts on the frame's result; the self-check
	/// records the calls instead.
	/// </summary>
	public interface IUWMotionStepTarget
	{
		/// <summary>Original mode, before each whole step: the host keeps the state the view is
		/// interpolated from (the fine position and the camera yaw before the step).</summary>
		void BeforeStep();

		/// <summary>One frame of the motion: piDt PIT ticks in the Original mode, 1/256 ticks
		/// (UWMotionParams.FineTicksPerTick) in the Smooth mode, under peCommand - the held walk,
		/// or the pending jump in its place for this one frame.</summary>
		void RunFrame(int piDt, UWPlayerMotion.Command peCommand);
	}

	/// <summary>
	/// HOW MANY MOTION FRAMES A RENDERED FRAME RUNS (cleanup step 4.7, 2026-10-18): the tick
	/// accumulator and the two loops of the motion switch (UWUserSettings.MotionSmooth), engine-free
	/// out of UWPlayerMovement.fUpdateOnCore so that the self-check can drive them with any frame
	/// length. The arithmetic is the host's of 2026-10-06 unchanged, operation for operation in
	/// float, so the steps fall on the same frames as before.
	///
	/// The rendered frame's time becomes PIT ticks (256 a second, UWPlayerTick) and is added to the
	/// remainder, CAPPED at MaxFrameTicks plus one step: as the original caps its frame at 0x40 ticks
	/// (UWPlayerMotion.MaxFrameTicks), a long frame does not become a long step and the rest is
	/// dropped. A held clock (no time) runs nothing.
	///
	/// ORIGINAL runs whole steps of the chosen frame length (UWUserSettings.OriginalFrameTicks: 4, 8,
	/// 12 or 16 ticks) while the remainder holds one, the view drawn between the last two states
	/// (Alpha). The game rules therefore see the same steps at 30, 60 or 144 frames a second; only
	/// the picture follows the monitor (Docs/DEVIATIONS.md, Rules: no rule hangs on the frame rate).
	///
	/// SMOOTH (the precision mode's second stage, FineTicks, 2026-10-06) runs ONE frame per
	/// rendered frame with the time it took in 1/256 ticks; the remainder below a 1/256 tick carries
	/// over, and past MaxFrameTicks the frame is cut and the remainder dropped.
	///
	/// THE PENDING JUMP: a jump is a press, and a press must wait for a step (2026-10-06, per user:
	/// "Springen funktioniert nicht zuverlaessig") - with 16-tick steps only every fourth rendered
	/// frame at 60 Hz runs one, and a press in a frame without one was gone by the next. So the
	/// jump is kept here until the next frame that runs takes it, in place of the held walk for
	/// that one frame; the walk goes on after it.
	/// </summary>
	public sealed class UWMotionStepper
	{
		/// <summary>The PIT ticks not yet handed to a frame.</summary>
		public float Remainder { get; private set; }

		/// <summary>A jump pressed but not yet taken by a frame (None without one).</summary>
		public UWPlayerMotion.Command PendingJump { get; private set; } = UWPlayerMotion.Command.None;

		/// <summary>The input pressed a jump (Jump or StandingJump): the next frame that runs takes
		/// it. A second press before then replaces the first.</summary>
		public void PressJump(UWPlayerMotion.Command peJump)
		{
			PendingJump = peJump;
		}

		/// <summary>A modal window holds the controls, or there is no world: the press is dropped.</summary>
		public void DropPendingJump()
		{
			PendingJump = UWPlayerMotion.Command.None;
		}

		/// <summary>The motion was placed (a level entry, a load, a teleport): no time is owed.</summary>
		public void ResetTime()
		{
			Remainder = 0f;
		}

		/// <summary>The step length of the mode: Smooth's step (only the cap uses it there, see the
		/// class), clamped to 1..MaxFrameTicks; Original's frame length as chosen.</summary>
		public static int StepTicks(bool pbSmooth, int piSmoothStepTicks, int piOriginalFrameTicks)
		{
			if (!pbSmooth)
				return piOriginalFrameTicks;

			if (piSmoothStepTicks < 1)
				return 1;

			return piSmoothStepTicks > UWPlayerMotion.MaxFrameTicks ? UWPlayerMotion.MaxFrameTicks : piSmoothStepTicks;
		}

		/// <summary>
		/// One rendered frame: AddTime, then Run. Returns how many frames ran.
		/// </summary>
		public int Advance(float pfSeconds, bool pbSmooth, int piStepTicks, UWPlayerMotion.Command peHeld, IUWMotionStepTarget pOTarget)
		{
			AddTime(pfSeconds, piStepTicks);

			return Run(pbSmooth, piStepTicks, peHeld, pOTarget);
		}

		/// <summary>The rendered frame's time as PIT ticks onto the remainder, capped (see the
		/// class). The host adds it before it reads the input, as it did before the split: a
		/// placement in between (ResetTime) then drops this frame's time too.</summary>
		public void AddTime(float pfSeconds, int piStepTicks)
		{
			Remainder += pfSeconds * UWPlayerTick.PitTicksPerSecond;

			if (Remainder > UWPlayerMotion.MaxFrameTicks + piStepTicks)
				Remainder = UWPlayerMotion.MaxFrameTicks + piStepTicks;
		}

		/// <summary>Runs the mode's frames on pOTarget, with peHeld as the command of every frame but
		/// the one that takes the pending jump. Returns how many frames ran.</summary>
		public int Run(bool pbSmooth, int piStepTicks, UWPlayerMotion.Command peHeld, IUWMotionStepTarget pOTarget)
		{
			return pbSmooth ? fRunSmooth(peHeld, pOTarget) : fRunOriginal(piStepTicks, peHeld, pOTarget);
		}

		/// <summary>Original's view: how far between the state before the last step (0) and the
		/// current one (1) the picture is drawn - the remainder over the step.</summary>
		public float Alpha(int piStepTicks)
		{
			return Remainder / piStepTicks;
		}

		private int fRunSmooth(UWPlayerMotion.Command peHeld, IUWMotionStepTarget pOTarget)
		{
			int liFine = UWRounding.Floor(Remainder * UWMotionParams.FineTicksPerTick);
			int liMostFine = UWPlayerMotion.MaxFrameTicks * UWMotionParams.FineTicksPerTick;

			if (liFine > liMostFine)
			{
				liFine = liMostFine;
				Remainder = 0f;
			}
			else
				Remainder -= liFine / (float)UWMotionParams.FineTicksPerTick;

			if (liFine <= 0)
				return 0;

			pOTarget.RunFrame(liFine, fTakeCommand(peHeld));

			return 1;
		}

		private int fRunOriginal(int piStepTicks, UWPlayerMotion.Command peHeld, IUWMotionStepTarget pOTarget)
		{
			int liFrames = 0;

			while (piStepTicks > 0 && Remainder >= piStepTicks)
			{
				Remainder -= piStepTicks;

				pOTarget.BeforeStep();
				pOTarget.RunFrame(piStepTicks, fTakeCommand(peHeld));
				liFrames++;
			}

			return liFrames;
		}

		/// <summary>The pending jump takes this one frame in place of the walk, then the walk goes on.</summary>
		private UWPlayerMotion.Command fTakeCommand(UWPlayerMotion.Command peHeld)
		{
			if (PendingJump == UWPlayerMotion.Command.None)
				return peHeld;

			UWPlayerMotion.Command leJump = PendingJump;

			PendingJump = UWPlayerMotion.Command.None;

			return leJump;
		}
	}
}
