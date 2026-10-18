using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The camera flight into the moongate at the end (cleanup step 3.4, 2026-10-17: the pure
	/// maths of UWEndgame.fLaunchIntoVoid, which UWEndgameSequence now steps on the game's clock).
	///
	/// The camera flight to the moongate: 64 steps (reference 0.1 s each, for us
	/// UWSettings.MoongateStepSeconds). Per step 0x800 angle units of roll around the view axis
	/// (reference PositionPlayerCamera, roll = steps &lt;&lt; 0xB) and 0xCCB along the circular path
	/// around the gate, the view stays aimed at the gate.
	///
	/// THE CAMERA CIRCLES THE GATE (reference PositionPlayerCamera, MoongateSucking): the
	/// angle grows by 0xCCB per step, the distance shrinks linearly from the starting distance
	/// to zero, the view always points at the gate, plus the roll and a sinking by two
	/// fine height units per step. Because the view is locked to the gate, it looks as if
	/// the gate rotates - as in the original (per user, 2026-09-14; the gates on level 9 do
	/// not rotate, so the model itself does not either). The direction of rotation is not confirmed.
	///
	/// Positions are world units with Y up (the Unity side's frame), angles degrees, yaw clockwise
	/// from north (+Z); the arithmetic is Unity's Mathf in single precision, as before.
	/// </summary>
	public readonly struct UWMoongateFlight
	{
		/// <summary>The number of steps.</summary>
		public const int Steps = 0x40;

		/// <summary>The reference's wait per step, used when no setting gives one.</summary>
		public const float DefaultStepSeconds = 0.1f;

		/// <summary>Roll per step in degrees.</summary>
		public const float RollPerStep = 0x800 * 360f / UWWorldScale.FullCircleUnits;

		/// <summary>Angle along the circular path around the gate per step (reference MoveCameraToVoid: 0xCCB).
		/// </summary>
		public const float YawPerStep = 0xCCB * 360f / UWWorldScale.FullCircleUnits;

		/// <summary>Sinking per step: two fine height units (reference, steps &lt;&lt; 1), for
		/// us 0.25 world units each (UWWorldScale.UnitsPerOriginalUnit).</summary>
		public const float SinkPerStep = 2f * UWWorldScale.UnitsPerOriginalUnit;

		/// <summary>Mathf.Rad2Deg and Mathf.Deg2Rad, so the poses come out as they did.</summary>
		private const float Rad2Deg = 57.29578f;

		private const float Deg2Rad = 0.0174532924f;

		/// <summary>One pose of the camera.</summary>
		public readonly struct Pose
		{
			public readonly UWVector3 Position;

			public readonly float Yaw;

			public readonly float Roll;

			public Pose(UWVector3 pOPosition, float pfYaw, float pfRoll)
			{
				Position = pOPosition;
				Yaw = pfYaw;
				Roll = pfRoll;
			}
		}

		/// <summary>The gate the camera circles, at the starting camera's height.</summary>
		public readonly UWVector3 Gate;

		/// <summary>The starting distance from the gate, shrinking to zero.</summary>
		public readonly float Radius;

		/// <summary>The starting angle around the gate, degrees.</summary>
		public readonly float StartAngle;

		/// <summary>The camera's position at the start.</summary>
		public readonly UWVector3 Start;

		private UWMoongateFlight(UWVector3 pOStart, UWVector3 pOGate, float pfRadius, float pfStartAngle)
		{
			Start = pOStart;
			Gate = pOGate;
			Radius = pfRadius;
			StartAngle = pfStartAngle;
		}

		/// <summary>
		/// The flight from where the camera is now to the gate in the centre of a tile. A camera
		/// standing on the gate itself keeps its heading (turned round, as the path looks back at
		/// the gate).
		/// </summary>
		public static UWMoongateFlight From(UWVector3 pOCamera, float pfCameraYaw, int piGateTileX, int piGateTileY)
		{
			UWVector3 lOGate = new UWVector3(UWUnits.TileCentreToWorldAxis(piGateTileX), pOCamera.Y,
				UWUnits.TileCentreToWorldAxis(piGateTileY));
			float lfX = pOCamera.X - lOGate.X;
			float lfY = pOCamera.Y - lOGate.Y;
			float lfZ = pOCamera.Z - lOGate.Z;
			float lfRadius = (float)Math.Sqrt((lfX * lfX) + (lfY * lfY) + (lfZ * lfZ));
			float lfStartAngle = lfRadius > 0.0001f
				? (float)Math.Atan2(lfX, lfZ) * Rad2Deg
				: pfCameraYaw + 180f;

			return new UWMoongateFlight(pOCamera, lOGate, lfRadius, lfStartAngle);
		}

		/// <summary>The view the camera is handed at the start, before step 0.</summary>
		public Pose StartPose => new Pose(Start, StartAngle + 180f, 0f);

		/// <summary>
		/// The pose of one step, 0 to Steps - 1. Like UW.EXE: draw first, then count on - step 0 is
		/// still at the starting position.
		/// </summary>
		public Pose PoseAt(int piStep)
		{
			float lfAngle = StartAngle + (piStep * YawPerStep);
			float lfDistance = Radius * (Steps - piStep) / Steps;
			float lfRadians = lfAngle * Deg2Rad;
			UWVector3 lOPosition = new UWVector3(
				Gate.X + ((float)Math.Sin(lfRadians) * lfDistance),
				Gate.Y - (piStep * SinkPerStep),
				Gate.Z + ((float)Math.Cos(lfRadians) * lfDistance));

			return new Pose(lOPosition, lfAngle + 180f, piStep * RollPerStep);
		}

		/// <summary>
		/// Which step is due after plElapsedTicks PIT ticks of game time (or piFrames running frames
		/// when the step length is 0, "one step per frame, as in the original"). Steps, or more,
		/// means the flight is over. The step k is due from k step lengths on, counted from the
		/// launch - not from the previous step, as WaitForSeconds did, which added up to a frame per
		/// step at every frame rate differently.
		/// </summary>
		public static int StepAt(float pfStepSeconds, long plElapsedTicks, int piFrames)
		{
			if (pfStepSeconds <= 0f)
				return piFrames;

			double ldStepTicks = pfStepSeconds * (double)UWPlayerTick.PitTicksPerSecond;

			return (int)Math.Min(int.MaxValue, Math.Floor(plElapsedTicks / ldStepTicks));
		}
	}
}
