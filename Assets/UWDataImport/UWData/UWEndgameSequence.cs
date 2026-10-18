namespace UWDataImport.UWData
{
	/// <summary>
	/// The last talisman is destroyed - sequence per UW.EXE:
	///   on impact           "A rending sound fills the air.", the counter (quest 36) at 0
	///                       (UWEndgameRules.DropTalismanInLava; the impact is not this class's)
	///   EndGameFunctions    run every frame while the counter is 0 (check_victory,
	///                       UWEndgameRules.AllTalismansGone; since 2026-10-18, the impact
	///                       launched until then): moongate in the centre of the level (link 0x2C0),
	///                       "The Slasher of Veils is dragged from this world.",
	///                       LaunchPlayerAtMoongate_ovr134_D52: 64 steps, ONE FRAME PER STEP
	///                       (renderrelated_seg031_351, no delay - the speed depended on the
	///                       machine speed), then remove the gate, teleport to level 9
	///                       27/23, the counter to 0xFF, "You are sucked through the moongate . . . . ."
	/// Camera per step following PositionCameraAtObject_seg034_2F89_B99, see UWMoongateFlight.
	///
	/// ENGINE-FREE ON THE GAME'S CLOCK SINCE 2026-10-17 (cleanup step 3.4): until then the coroutine
	/// UWEndgame.fLaunchIntoVoid on the level loader, waiting with WaitForSeconds and guarded by a
	/// static flag that only a new game cleared - a loader destroyed mid-flight left it set. Now one
	/// sequence per session (UWGameSession.Endgame) steps on the session's scheduler: the messages,
	/// the gate and the start of the flight at once, as before; then one pose per step length of
	/// game time (or per running frame with the step length 0); then the end of the camera, the
	/// journey and the last message.
	///
	/// THE WORLD (cleanup step 3.6, 2026-10-17): the messages, the gate and the journey go to the
	/// world (IUWWorld: its presentation, SpawnNewObject, TravelTo); IHost keeps the view the flight
	/// takes over, whose claim belongs to this flight. Until then IHost carried all seven.
	/// </summary>
	public sealed class UWEndgameSequence
	{
		/// <summary>The view the flight takes over (the Unity side, UWEndgame's remote camera) -
		/// per launch, as it holds the flight's claim on the view.</summary>
		public interface IHost
		{
			/// <summary>Where the view is now and its heading; false without a camera (the flight
			/// then runs its time without a picture).</summary>
			bool TryGetCamera(out UWVector3 pOPosition, out float pfYaw);

			/// <summary>Takes the view over for the flight, at its start.</summary>
			void BeginCamera(UWMoongateFlight.Pose pOPose);

			void SetCameraPose(UWMoongateFlight.Pose pOPose);

			void EndCamera();
		}

		/// <summary>String block 1, our export one higher than UW.EXE's numbers (see UWEndgame:
		/// UW.EXE calls 0x116, 0x117 and 0x118).</summary>
		public const int RendingSoundMessage = 0x116 + 1;      // A rending sound fills the air.

		private const int SlasherDraggedMessage = 0x117 + 1;   // The Slasher of Veils is dragged from this world.

		private const int SuckedThroughMessage = 0x118 + 1;    // You are sucked through the moongate . . . . .

		/// <summary>Link field of the moongate in the volcano: 0x2C0, colour 192.</summary>
		public const int MoongateLink = 0x2C0;

		/// <summary>Is the launch running? The former static UWEndgame.mbLaunching: a second
		/// launch while one runs does nothing.</summary>
		public bool IsLaunching { get; private set; }

		private IHost mIHost;

		private IUWWorld mOWorld;

		private UWWorldScheduler mOSchedule;

		private UWMoongateFlight mOFlight;

		private bool mbHasCamera;

		private float mfStepSeconds;

		private bool mbStarted;

		private long mlStartTick;

		private int miFrames;

		private int miShownStep;

		/// <summary>
		/// The journey, started by the victory check when the talisman counter is 0 (the impact of
		/// the last talisman only plays the rending sound since 2026-10-18): the message, the gate
		/// and the start of the flight at once; the steps follow on pOSchedule. pfStepSeconds is
		/// the wait per step (0: one step per running frame). False when a launch is already running.
		/// </summary>
		public bool Launch(IUWWorld pOWorld, IHost pIHost, UWWorldScheduler pOSchedule, float pfStepSeconds)
		{
			if (IsLaunching || pOWorld == null || pIHost == null || pOSchedule == null)
				return false;

			IsLaunching = true;
			mOWorld = pOWorld;
			mIHost = pIHost;
			mOSchedule = pOSchedule;
			mfStepSeconds = pfStepSeconds;
			mbStarted = false;
			miFrames = 0;

			// Link 0x2C0 as in UW.EXE (EndGameFunctions: word 3 = ... | 0xB000, quantity bit set) -
			// for the moongate that is the colour, see UWObjectSpawner.fGetModelFaceColor. Without it
			// the gate stayed pink (per user, 2026-09-14).
			// Obj_AddEnd (SKILLS.C): at the tail of the tile's list (2026-10-18).
			mOWorld.SpawnNewObject(UWObjectMechanics.MoongateObjectId,
				new UWTilePos(UWEndgameRules.VolcanoCentre, UWEndgameRules.VolcanoCentre), MoongateLink,
				UWWorldEdit.Arrival.PutDown);

			// The scroll is looked up at every message (the world's presentation): the journey
			// rebuilds the level before the last one.
			mOWorld.Presentation.AddGeneralMessage(SlasherDraggedMessage);

			UWVector3 lOCamera;
			float lfYaw;

			mbHasCamera = mIHost.TryGetCamera(out lOCamera, out lfYaw);
			mOFlight = UWMoongateFlight.From(mbHasCamera ? lOCamera : new UWVector3(0f, 0f, 0f), lfYaw,
				UWEndgameRules.VolcanoCentre, UWEndgameRules.VolcanoCentre);

			if (mbHasCamera)
			{
				mIHost.BeginCamera(mOFlight.StartPose);
				mIHost.SetCameraPose(mOFlight.PoseAt(0));
			}

			miShownStep = 0;
			mOSchedule.EveryAdvance(fStep);

			return true;
		}

		/// <summary>
		/// One running frame. THE FIRST ADVANCE IS THE LAUNCH'S OWN FRAME: the impact runs inside the
		/// critter driver, before the scheduler advances in the same frame, so step 0 stays up for
		/// its full length and with the step length 0 every step gets one frame, as the coroutine
		/// had it.
		/// </summary>
		private bool fStep()
		{
			if (!mbStarted)
			{
				mbStarted = true;
				mlStartTick = mOSchedule.Now;
			}
			else
				miFrames++;

			int liStep = UWMoongateFlight.StepAt(mfStepSeconds, mOSchedule.Now - mlStartTick, miFrames);

			if (liStep >= UWMoongateFlight.Steps)
			{
				fFinish();

				return false;
			}

			if (liStep != miShownStep && mbHasCamera)
				mIHost.SetCameraPose(mOFlight.PoseAt(liStep));

			miShownStep = liStep;

			return true;
		}

		private void fFinish()
		{
			if (mbHasCamera)
				mIHost.EndCamera();

			IsLaunching = false;

			// check_victory: the teleport, then the counter to 0xFF, then the journey - a save
			// during the flight keeps the 0 and starts it again on loading.
			UWQuestFlags.Set(UWEndgameRules.TalismanQuestFlag, UWEndgameRules.TalismansAfterVictory);
			mOWorld.TravelTo(UWEndgameRules.VoidLevelIndex, UWEndgameRules.VoidTileX, UWEndgameRules.VoidTileY);
			mOWorld.Presentation.AddGeneralMessage(SuckedThroughMessage);
			mIHost = null;
			mOWorld = null;
		}
	}
}
