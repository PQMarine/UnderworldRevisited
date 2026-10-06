namespace UWDataImport.UWData
{
	/// <summary>
	/// THE PLAYER'S MOTION on the engine-free core (stage 3 of the motion rework, read
	/// 2026-10-05): PlayerMotion_seg034_2F89_604 and what it calls - PlayerMotionInitialCalc_seg008_5E3
	/// with CalculateMotionFromCommand_seg008_BEC, the shared CalculateMotion (UWMotionCore) with the
	/// player's handler 0x284E and his callback seg008_1B2A_BB3, ApplyPlayerMotion_seg008_90D,
	/// ProcessPlayerTileState_seg008_1B2A_3D with UpdateMotionStateAndSwimming_seg008_E0C and
	/// SetPlayerDataOxB9_seg008_B, PlacePlayerInTile_seg008_6D1, StopFalling_seg008_D5B and
	/// BouncePlayer_seg008_D9E. Instruction-faithful in the 16-bit arithmetic.
	///
	/// The params block 0x2780 PERSISTS between frames - the player's position, velocity,
	/// gravity and speed live in it, nothing is read back from his object record. A frame is
	/// one call of Frame with the elapsed PIT ticks (capped at 0x40 by the caller, as the
	/// original's loop does) and the command the input produced; the host reads the position
	/// and the camera yaw from the params afterwards and acts on the FrameResult: the fall
	/// damage and its sound, the water entered, the state and its speeds.
	///
	/// What stays with the host: the input mapping (the keyboard poll seg034_2F89_1DA and the
	/// pointer scheme, see seg034_2F89_4E in UWGameClock), WalkOnSurfaceTypes_seg034_93D (the head bob, the lava
	/// damage of one point on one frame in five, the shakes), the swim checks of the periodic
	/// update, and the easy step (see seg008_1B2A_216 in UWEasyMovement). Reading aid: the private notes motion-player.md.
	/// </summary>
	public sealed class UWPlayerMotion
	{
		/// <summary>MotionCommand_75A: the jump table of CalculateMotionFromCommand. 2-5 and 0xB do
		/// nothing. Free is the port's own entry for the modern scheme's diagonal walk (see
		/// FreeOffset) - the original knows only the four directions.</summary>
		public enum Command
		{
			None = 0,

			Walk = 1,

			StandingJump = 6,

			Jump = 7,

			Back = 8,

			SlideLeft = 9,

			SlideRight = 0xA,

			FlyUp = 0xC,

			FlyDown = 0xD,

			Free = 0x10
		}

		/// <summary>PD[0xB6] bits 0-2, the motion state UpdateMotionStateAndSwimming scales the
		/// speeds by. 3 is unused.</summary>
		public enum State
		{
			Normal = 0,

			Swimming = 1,

			Lava = 2,

			Levitating = 4,

			Flying = 5,

			SlowFalling = 6
		}

		// ------------------------------------------------- The constants of the data segment

		/// <summary>dseg CE / CC / CA: the base speeds, in 1/65536 tile per PIT tick.</summary>
		public const int BaseForwardSpeed = 0x3AC;

		public const int BaseSlideSpeed = 0xEB;

		public const int BaseBackSpeed = 0xBC;

		/// <summary>MotionTurnStep_75C: the only turn step the commands read. (UpdateMotionStateAndSwimming
		/// writes a state-scaled copy to 2492, which nothing reads.)</summary>
		public const int TurnStep = 0xF;

		/// <summary>The frame's tick budget is capped here by the loop (motion-timing.md 2).</summary>
		public const int MaxFrameTicks = 0x40;

		/// <summary>PlayerMotionWalk_763 of the keyboard: W runs, S walks (seg034_2F89_1DA).</summary>
		public const int WalkRun = 0x70;

		public const int WalkSlow = 0x32;

		/// <summary>PlayerMotionTurnHeading_765 of the keyboard: A and D.</summary>
		public const int TurnInputStep = 0x5A;

		/// <summary>MagicMotionAbilities_D2 - the same bits as UWPlayerVitals.LeapBit and the rest.</summary>
		public const int AbilityLeap = 1;

		public const int AbilitySlowFall = 2;

		public const int AbilityLevitate = 4;

		public const int AbilityWaterWalk = 8;

		public const int AbilityFly = 0x10;

		/// <summary>The jump's vertical speed (0x263), the fly commands' (0x8D).</summary>
		public const int JumpSpeed = 0x263;

		public const int FlySpeed = 0x8D;

		/// <summary>MotionWeightRelated_C8 when carrying no more than half the capacity.</summary>
		public const int FullMotionWeight = 0x60;

		/// <summary>The swim counter PD[0xB9] on entering water (SetPlayerDataOxB9).</summary>
		public const int SwimCounterOnEntering = 0x60;

		/// <summary>dseg_D4: the speed ratio in tenths per state.</summary>
		private static readonly int[] SpeedRatio = { 10, 3, 5, 10, 1, 7, 2 };

		/// <summary>dseg_DB: the low bits of PD[0xB8] per state (swim 1, lava 2, levitate and fly
		/// 8, slow fall and normal 0).</summary>
		private static readonly int[] SurfaceBitsByState = { 0, 1, 2, 4, 8, 8, 0 };

		/// <summary>dseg_1992: the fine z PlacePlayerInTile gives a floor height - h * 0x40 for h 0
		/// to 13 and 0 for 14 and 15 (read byte by byte 2026-10-05).</summary>
		public static readonly int[] PlaceZByFloor =
		{
			0x000, 0x040, 0x080, 0x0C0, 0x100, 0x140, 0x180, 0x1C0,
			0x200, 0x240, 0x280, 0x2C0, 0x300, 0x340, 0x000, 0x000
		};

		// ------------------------------------------------- The state

		private readonly IUWMotionWorld mOWorld;

		/// <summary>The params block 0x2780.</summary>
		public readonly UWMotionParams Params;

		/// <summary>The core of the step (the static calc array 0x2768, shared with the creatures
		/// in the original; the port gives the player his own instance).</summary>
		public readonly UWMotionCore Core;

		/// <summary>The handler 0x284E: callback mask 0x1100, the ignore mask set per frame.</summary>
		private readonly UWMotionHandler mOHandler;

		/// <summary>The player's COMOBJ size (0x7F): radius byte 1 &amp; 7, height byte 0.</summary>
		private readonly int miRadius;

		private readonly int miHeight;

		/// <summary>PlayerCameraYaw_727A and PlayerMotionYaw_727C, 16 bits, 0 = +y, 0x4000 = +x.
		/// The host writes CameraYaw when its own look turns the view.</summary>
		public int CameraYaw;

		public int MotionYaw;

		/// <summary>dseg_D0: the side of the last command (-2 back, -1 left, 1 right, 0 forward);
		/// the camera follows a deflection by heading - (Side &lt;&lt; 14).</summary>
		public int Side;

		/// <summary>MagicMotionAbilities_D2, set by the host from the active spells and rings.</summary>
		public int Abilities;

		/// <summary>PD[0x32], for the fall's check.</summary>
		public int AcrobatSkill;

		/// <summary>PD[0x4A] and PD[0x4C], for the motion weight.</summary>
		public int CarriedWeight;

		public int MaxWeight;

		/// <summary>PlayerActualForwardSpeed_2496, SlideSpeed_2498, BackwardsSpeed_249A: the base
		/// speeds scaled by the state.</summary>
		public int ForwardSpeed = BaseForwardSpeed;

		public int SlideSpeed = BaseSlideSpeed;

		public int BackSpeed = BaseBackSpeed;

		/// <summary>MotionWeightRelated_C8: the speed change one frame may make.</summary>
		public int MotionWeight = FullMotionWeight;

		/// <summary>PD[0xB6] bits 0-2.</summary>
		public State CurrentState { get; private set; }

		/// <summary>PD[0xB8] bits 0-4: what the player stands in or hovers by.</summary>
		public int SurfaceBits { get; private set; }

		/// <summary>PD[0xB9]: 0x60 on entering water, 0 out of it; the periodic swim checks of the
		/// host raise it.</summary>
		public int SwimCounter;

		/// <summary>PreviousTileState_24A0.</summary>
		public int PreviousTileState;

		/// <summary>CurrentTileOffset_2494 as a tile, -1 before the first placement.</summary>
		public int CurrentTileX = -1;

		public int CurrentTileY = -1;

		/// <summary>PlayerMotionUpdateNeeded_D3: a frame runs even without a command or motion.</summary>
		public bool UpdateNeeded;

		/// <summary>
		/// THE PRECISION MODE (UWMotionParams.Precise, the host's Smooth motion, 2026-10-06): the
		/// core carries the sub-fine fraction, and the rules the original applies PER CALL -
		/// the ramp of MotionWeight, the levitation decay of four fifths, the slow fall's halving,
		/// the camera follow of 0x400 at a wall - become rates per OriginalFrameTicks, so that
		/// small steps add up to the original's frame of 16 ticks. The jump's take-off loses the
		/// half frame of gravity the original's Euler step never integrates (JumpEulerOffset), so
		/// the continuous arc peaks where the 16-tick one does. Off: the original, call by call.
		/// </summary>
		public bool Precise
		{
			get { return Params.Precise; }
			set { Params.Precise = value; }
		}

		/// <summary>UW.EXE's frame at its usual pace, the unit of the per-call rules.</summary>
		public const int OriginalFrameTicks = 16;

		/// <summary>
		/// THE RAMP'S FRAME in the precision mode (2026-10-06, per user: the long start and stop
		/// "wirken dagegen" for a player who gets motion sick): the speed moves by MotionWeight per
		/// this many ticks - 16 is the original at 16 frames a second (0.56 s to the run), 8 the
		/// original on the user's DOSBox at 32 (0.31 s), 4 a fast machine (0.16 s), 0 at once.
		/// Only the ramp; the camera follow, the levitation decay and the slow fall stay per
		/// OriginalFrameTicks. The host sets it from UWUserSettings.
		/// </summary>
		public int RampFrameTicks = OriginalFrameTicks;

		/// <summary>What the take-off loses in the precision mode so that the continuous arc peaks
		/// where the original's 16-tick arc does. The Euler half step alone would be g * 16 / 2 =
		/// 32, but the original's arc also loses a fine z to the dropped fraction on every call,
		/// so 16 is what the self-check measured as the match (apex 290 fine z from floor 128).</summary>
		public const int JumpEulerOffset = 16;

		/// <summary>The frame's tick budget as given, before the core consumes Params.Dt (in 1/256
		/// tick with FineTicks).</summary>
		private int miFrameDt = OriginalFrameTicks;

		/// <summary>
		/// FINE TICKS (UWMotionParams.FineTicks, the second stage of the precision mode): Frame
		/// takes its Dt in 1/256 tick, so the host calls it once per rendered frame with the time
		/// that frame took and draws the current state - no interpolation, no lag. The per-frame
		/// rules below scale by the frame's real tick count then; the ramp keeps its fraction
		/// between the frames.
		/// </summary>
		public bool FineTicks
		{
			get { return Params.FineTicks; }
			set { Params.FineTicks = value; }
		}

		/// <summary>The frame's ticks as a number, whole or fine.</summary>
		private double fFrameTicks()
		{
			return Params.FineTicks ? miFrameDt / (double)UWMotionParams.FineTicksPerTick : miFrameDt;
		}

		/// <summary>The ramp's fraction below one unit carried between the frames (FineTicks).</summary>
		private double mdRampCarry;

		/// <summary>The fine position with the carried fraction, for a view that is even to a
		/// 1/256 of a fine unit.</summary>
		public float FineX
		{
			get { return Params.X + (Params.XFrac / 256f); }
		}

		public float FineY
		{
			get { return Params.Y + (Params.YFrac / 256f); }
		}

		public float FineZ
		{
			get { return Params.Z + (Params.ZFrac / 256f); }
		}

		/// <summary>The speed change this call may make: MotionWeight per call in the original, per
		/// RampFrameTicks in the precision mode (0 = all of it at once).</summary>
		private int fRampWeight()
		{
			if (!Params.Precise)
				return MotionWeight;

			if (RampFrameTicks <= 0)
				return 0x7FFF;

			if (Params.FineTicks)
			{
				double ldExact = (MotionWeight * fFrameTicks() / RampFrameTicks) + mdRampCarry;
				int liWhole = (int)System.Math.Floor(ldExact);

				mdRampCarry = ldExact - liWhole;

				return liWhole < 1 ? 1 : liWhole;
			}

			int liScaled = (MotionWeight * miFrameDt) / RampFrameTicks;

			return liScaled < 1 ? 1 : liScaled;
		}

		/// <summary>A per-call amount of the original scaled to this frame's ticks (at least 1).</summary>
		private int fPerFrame(int piPerOriginalFrame)
		{
			if (!Params.Precise)
				return piPerOriginalFrame;

			int liScaled = (int)(piPerOriginalFrame * fFrameTicks() / OriginalFrameTicks);

			return liScaled < 1 ? 1 : liScaled;
		}

		/// <summary>A per-call factor of the original (four fifths, one half) raised to this
		/// frame's share of the original's frame.</summary>
		private int fDecay(int piValue, double pdFactorPerOriginalFrame)
		{
			if (!Params.Precise)
				return (int)(piValue * pdFactorPerOriginalFrame);

			double ldFactor = System.Math.Pow(pdFactorPerOriginalFrame, fFrameTicks() / OriginalFrameTicks);

			return (int)System.Math.Round(piValue * ldFactor);
		}

		// ------------------------------------------------- The input of the frame

		public Command MotionCommand;

		/// <summary>PlayerMotionWalk_763: 0..0x70; the target speed of Walk is (Walk &gt;&gt; 2) *
		/// ForwardSpeed / 32, so 0x70 gives 822 of the 940.</summary>
		public int Walk;

		/// <summary>PlayerMotionTurnHeading_765: +-0x5A from the keys; the camera turns by
		/// (dt * TurnStep) * (TurnInput / 4) / 4 per frame.</summary>
		public int TurnInput;

		/// <summary>The Free command's heading offset from the camera yaw (the port's diagonal walk
		/// of the modern scheme); the camera follows a deflection by heading - FreeOffset.</summary>
		public int FreeOffset;

		/// <summary>The offset of the running command, which the camera follow uses (Side &lt;&lt; 14
		/// for the original's commands, FreeOffset for Free).</summary>
		private int miCommandOffset;

		/// <summary>What one frame did that the host has to act on.</summary>
		public struct FrameResult
		{
			/// <summary>The frame ran a step (something moved or a command stood).</summary>
			public bool Ran;

			public bool TileChanged;

			public int OldTileX;

			public int OldTileY;

			/// <summary>A head-on wall hit zeroed the speed.</summary>
			public bool HeadOnStop;

			/// <summary>The core deflected the heading and the camera followed.</summary>
			public bool Deflected;

			/// <summary>The impact value after the Acrobat check (impact &gt;&gt; 8, doubled while
			/// the vertical speed stands), 0 without an impact.</summary>
			public int ImpactValue;

			/// <summary>Above 3: DamageObject(player, value, type 0).</summary>
			public int FallDamage;

			/// <summary>The landing sound 0x0F plays: the value above 1, or still airborne after the
			/// impact (a wall touched in the air).</summary>
			public bool LandingSound;

			/// <summary>Its volume argument, value * 4 - 60.</summary>
			public int LandingVolume;

			/// <summary>The motion state changed this frame.</summary>
			public bool StateChanged;

			/// <summary>Water entered: the swim counter set to 0x60 and the weapon put away
			/// (PutAwayWeapon, see seg024_15C0 in UWMusicSelector).</summary>
			public bool EnteredWater;
		}

		public FrameResult Last;

		public UWPlayerMotion(IUWMotionWorld pOWorld, UWCommonObjectProperties pOProperties)
		{
			mOWorld = pOWorld;
			Core = new UWMotionCore(pOWorld, pOProperties);
			Params = new UWMotionParams { Index = 1, Step = 8, Elasticity = 5, Style = UWMotionParams.SlideStyle };
			mOHandler = new UWMotionHandler { CallbackMask = 0x1100, Callback = fCallback };

			UWCommonObjectProperties.Entry lOEntry;

			if (pOProperties != null && pOProperties.TryGet(UWObjectMechanics.AdventurerObjectId, out lOEntry))
			{
				miRadius = lOEntry.Radius & 7;
				miHeight = lOEntry.Height;
			}
			else
			{
				miRadius = 2;
				miHeight = 23;
			}

			Params.Radius = miRadius;
			Params.Height = miHeight;
		}

		private static int I16(int piValue)
		{
			return (short)piValue;
		}

		/// <summary>The player as the collision scan sees him: index 1, item 0x7F, at the params'
		/// eighth and coarse height.</summary>
		public UWMotionBody Body
		{
			get
			{
				return new UWMotionBody
				{
					Index = 1,
					ItemId = UWObjectMechanics.AdventurerObjectId,
					XPos = (Params.X >> 5) & 7,
					YPos = (Params.Y >> 5) & 7,
					ZPos = (Params.Z >> 3) & 0x7F,
					IsMobile = true,
					IsCreature = true,
					Direction = (CameraYaw >> 13) & 7
				};
			}
		}

		public int TileX
		{
			get { return (Params.X >> 8) & 0x3F; }
		}

		public int TileY
		{
			get { return (Params.Y >> 8) & 0x3F; }
		}

		/// <summary>Params +0x25 bit 0x10: off the ground.</summary>
		public bool IsAirborne
		{
			get { return (Params.Contact & UWMotionTables.ContactAirborne) != 0; }
		}

		// ------------------------------------------------- The frame

		/// <summary>
		/// One frame of PlayerMotion_seg034_2F89_604: nothing when no command stands and the
		/// player neither moves, falls nor needs an update (the gate in GameObjectLoop, 115030);
		/// otherwise the initial calc, the core's step and the write-back. piDt is the frame's
		/// PIT ticks, which the caller caps at MaxFrameTicks.
		/// </summary>
		public bool Frame(int piDt)
		{
			Last = default(FrameResult);

			UWMotionParams p = Params;

			if (MotionCommand == Command.None && p.Speed == 0 && p.Vz == 0 && p.Gravity == 0
				&& p.Ax == 0 && p.Ay == 0 && !UpdateNeeded)
				return false;

			if (piDt <= 0)
				return false;

			Last.Ran = true;
			p.Radius = miRadius;
			p.Height = miHeight;
			miFrameDt = piDt;

			fInitialCalc(piDt);

			Core.Mover = Body;
			Core.CalculateMotion(p, mOHandler);

			fApplyMotion();

			return true;
		}

		/// <summary>
		/// PlayerMotionInitialCalc_seg008_5E3 (55882). On the ground (gravity 0) the command gives
		/// a target speed and the speed moves towards it by at most MotionWeight PER CALL (not per
		/// tick - the acceleration is per frame), clamped to the forward speed and zero. In the air
		/// no command is read; only the camera turns with the turn input. Then the frame's budget,
		/// the elasticity 5, the slide style on the ground (0x80 when no acceleration and no
		/// gravity), the motion yaw snapped to the camera when standing, the handler's ignore mask
		/// (0x1000 with levitate or fly: he never falls, and slides), the impact cleared.
		/// </summary>
		private void fInitialCalc(int piDt)
		{
			UWMotionParams p = Params;
			int liTarget = 0;

			if (p.Gravity == 0)
			{
				liTarget = fCommand(piDt);

				if (p.Gravity == 0)
				{
					int liDiff = I16(liTarget - p.Speed);
					int liAbs = liDiff < 0 ? -liDiff : liDiff;
					int liWeight = fRampWeight();

					if (liAbs > liWeight)
						liDiff = I16((liDiff > 0 ? 1 : -1) * liWeight);

					p.Speed = I16(p.Speed + liDiff);

					if (p.Speed > ForwardSpeed)
						p.Speed = ForwardSpeed;
					else if (p.Speed < 0)
						p.Speed = 0;
				}
			}

			if (p.Gravity != 0)
				CameraYaw = (CameraYaw + fTurn(piDt)) & 0xFFFF;

			p.Dt = piDt;
			p.Elasticity = 5;
			p.Style = (p.Ax | p.Ay | p.Gravity) == 0 ? UWMotionParams.SlideStyle : UWMotionParams.BounceStyle;

			if (p.Speed == 0)
				MotionYaw = CameraYaw;

			p.Heading = MotionYaw & 0xFFFF;
			mOHandler.IgnoreMask = 0;

			if ((Abilities & (AbilityLevitate | AbilityFly)) != 0)
			{
				mOHandler.IgnoreMask = 0x1000;
				p.Style = UWMotionParams.SlideStyle;
			}

			p.Impact = 0;
		}

		/// <summary>(dt * TurnStep) * (TurnInput / 4) / 4, in 16 bits - with FineTicks in 32 bits
		/// and shifted back.</summary>
		private int fTurn(int piDt)
		{
			if (Params.FineTicks)
				return (int)((((long)piDt * TurnStep * (TurnInput / 4)) / 4) >> 8);

			return I16(I16(I16(piDt * TurnStep) * (TurnInput / 4)) / 4);
		}

		/// <summary>
		/// CalculateMotionFromCommand_seg008_BEC (56597): the target speed of the command, the
		/// motion yaw from the camera yaw with the command's offset (back 0x8000, the slides
		/// +-0x4000), the side. Walk turns the camera by the turn input first and targets
		/// (Walk &gt;&gt; 2) * ForwardSpeed / 32. The standing jump (6) needs a player at rest - no
		/// vertical speed, no gravity, no speed - and then runs forward at half speed into the
		/// jump (7): vertical speed 0x263, cut to 5/6 above a fine z of 0x280 and then to 2/3 of
		/// that above 0x2C0, gravity -2 under Leap, -4 otherwise; the jump and the stop leave the
		/// motion yaw untouched. Fly up and down (0xC, 0xD) set +-0x8D and no gravity.
		/// </summary>
		private int fCommand(int piDt)
		{
			UWMotionParams p = Params;
			int liYaw = CameraYaw;
			int liTarget = 0;
			bool lbSetYaw = true;

			miCommandOffset = 0;

			switch (MotionCommand)
			{
				case Command.None:
					liTarget = 0;
					lbSetYaw = false;
					break;

				case Command.Walk:
					CameraYaw = (CameraYaw + fTurn(piDt)) & 0xFFFF;
					liYaw = CameraYaw;
					MotionYaw = CameraYaw;
					liTarget = I16(I16((Walk >> 2) * ForwardSpeed) / 32);
					Side = 0;
					break;

				case Command.Free:
					CameraYaw = (CameraYaw + fTurn(piDt)) & 0xFFFF;
					liYaw = (CameraYaw + FreeOffset) & 0xFFFF;
					liTarget = I16(I16((Walk >> 2) * ForwardSpeed) / 32);
					Side = 0;
					miCommandOffset = FreeOffset;
					break;

				case Command.StandingJump:
					if (p.Vz != 0 || p.Gravity != 0 || p.Speed != 0)
						break;

					MotionYaw = CameraYaw;
					liTarget = ForwardSpeed / 2;
					p.Speed = liTarget;
					Side = 0;
					fJump();
					lbSetYaw = false;
					break;

				case Command.Jump:
					fJump();
					lbSetYaw = false;
					break;

				case Command.Back:
					liYaw = (CameraYaw - 0x8000) & 0xFFFF;
					liTarget = BackSpeed;
					Side = -2;
					miCommandOffset = -0x8000;
					break;

				case Command.SlideLeft:
					liYaw = (CameraYaw - 0x4000) & 0xFFFF;
					liTarget = SlideSpeed;
					Side = -1;
					miCommandOffset = -0x4000;
					break;

				case Command.SlideRight:
					liYaw = (CameraYaw + 0x4000) & 0xFFFF;
					liTarget = SlideSpeed;
					Side = 1;
					miCommandOffset = 0x4000;
					break;

				case Command.FlyUp:
					Side = 0;
					p.Vz = FlySpeed;
					p.Gravity = 0;
					break;

				case Command.FlyDown:
					Side = 0;
					p.Vz = -FlySpeed;
					p.Gravity = 0;
					break;
			}

			if (lbSetYaw)
				MotionYaw = liYaw & 0xFFFF;

			return liTarget;
		}

		/// <summary>Case 7 of the command: the take-off.</summary>
		private void fJump()
		{
			UWMotionParams p = Params;

			p.Vz = JumpSpeed;

			if (p.Z > 0x280)
			{
				p.Vz = I16(p.Vz * 5) / 6;

				if (p.Z > 0x2C0)
					p.Vz = I16(p.Vz << 1) / 3;
			}

			p.Gravity = (Abilities & AbilityLeap) != 0 ? -2 : -4;

			// The precision mode integrates the arc continuously; without the half frame of
			// gravity the original never adds at the take-off, the apex stays the original's.
			if (p.Precise)
				p.Vz -= JumpEulerOffset;
		}

		/// <summary>
		/// The player's callback seg008_1B2A_BB3 (56557): off the ground (0x1000) with no vertical
		/// speed and a speed below three tenths of the forward speed the velocity is zeroed and
		/// the sub-step undone - a slow player holds at a ledge; a fast one walks off it and the
		/// core sets his gravity.
		/// </summary>
		private bool fCallback(ref int piFlags)
		{
			UWMotionParams p = Params;

			if ((piFlags & 0x1000) != 0 && p.Vz == 0 && I16(p.Speed * 10) < I16(ForwardSpeed * 3))
			{
				p.Vx = 0;
				p.Vy = 0;

				return true;
			}

			return false;
		}

		/// <summary>
		/// ApplyPlayerMotion_seg008_90D (56239): the tile relink when the tile changed (the host's,
		/// TileChanged), the head-on stop (an impact with the heading still the motion yaw zeroes
		/// the speed), the deflection (the heading changed: the motion yaw takes it, and while
		/// sliding the camera turns towards heading - the command's offset by at most 0x400 per
		/// frame), the impact block (impact &gt;&gt; 8, doubled while the vertical speed stands, an
		/// Acrobat check against twice the value cuts it by (30 - skill) / 30, above 3 it hurts,
		/// above 1 - or still airborne - it sounds at value * 4 - 60), then ProcessPlayerTileState
		/// with the new contact state and UpdateNeeded cleared.
		/// </summary>
		private void fApplyMotion()
		{
			UWMotionParams p = Params;
			int liTileX = (p.X >> 8) & 0x3F;
			int liTileY = (p.Y >> 8) & 0x3F;

			if (liTileX != CurrentTileX || liTileY != CurrentTileY)
			{
				Last.TileChanged = true;
				Last.OldTileX = CurrentTileX;
				Last.OldTileY = CurrentTileY;
				CurrentTileX = liTileX;
				CurrentTileY = liTileY;
			}

			int liHeading = p.Heading & 0xFFFF;

			if (p.Impact != 0 && liHeading == (MotionYaw & 0xFFFF))
			{
				p.Speed = 0;
				Last.HeadOnStop = true;
			}

			if (liHeading != (MotionYaw & 0xFFFF))
			{
				MotionYaw = liHeading;
				Last.Deflected = true;

				int liTarget = (liHeading - miCommandOffset) & 0xFFFF;

				if ((p.Style & UWMotionParams.SlideStyle) != 0)
				{
					int liDiff = I16(CameraYaw - liTarget);
					int liAbs = liDiff < 0 ? -liDiff : liDiff;
					int liFollow = fPerFrame(0x400);

					if (liAbs < liFollow)
						CameraYaw = liTarget;
					else if ((liDiff & 0xFFFF) < 0x7FFF)
						CameraYaw = (CameraYaw - liFollow) & 0xFFFF;
					else
						CameraYaw = (CameraYaw + liFollow) & 0xFFFF;
				}
			}

			if (p.Impact != 0 && p.Elasticity > 0)
			{
				int liValue = (p.Impact & 0xFFFF) >> 8;

				if (p.Vz != 0)
					liValue <<= 1;

				if (UWSkillCheck.IsSuccess(UWSkillCheck.Check(AcrobatSkill, liValue * 2)))
					liValue = I16(liValue * (30 - AcrobatSkill)) / 30;

				Last.ImpactValue = liValue;

				if (liValue > 3)
					Last.FallDamage = liValue;

				if (liValue > 1 || (p.Contact & UWMotionTables.ContactAirborne) != 0)
				{
					Last.LandingSound = true;
					Last.LandingVolume = (liValue * 4) - 60;
				}

				p.Impact = 0;
			}

			fProcessTileState(p.Contact, false);
			UpdateNeeded = false;
		}

		// ------------------------------------------------- The state machine

		/// <summary>
		/// ProcessPlayerTileState_seg008_1B2A_3D (55080). TWO PARTS: the state part runs on a changed
		/// contact state (or forced) - water or the player's water-edge state (0x22) make him swim
		/// unless he walks on water (SetPlayerDataOxB9: the counter 0x60 and the weapon put away
		/// when the water kind itself is under him, 0x10 at the edge, which the clearing below
		/// undoes), lava is state 2, and off the ground the abilities decide: levitate 4, fly 5,
		/// slow fall 6; then the speeds, and out of water the counter 0. THE AIRBORNE PART RUNS ON
		/// EVERY CALL - the early exit of an unchanged state jumps to it (label D8), not past it
		/// (found 2026-10-06 when the user hovered on after letting go of E, while the original
		/// stops): off the ground with levitate or fly gravity is 0 and the vertical speed decays
		/// to four fifths a frame (zero at ten or below) - so a tap of E rises and stops; otherwise
		/// gravity -4 if none, and under slow fall a vertical speed at or below -0x5E is set to
		/// -0x5E and the speed halved (zero below 0x14), frame after frame - the fall is capped and
		/// the momentum gone within a few frames, as the user saw in the original (2026-09-03).
		/// </summary>
		private void fProcessTileState(int piState, bool pbForce)
		{
			UWMotionParams p = Params;

			if (piState != PreviousTileState || pbForce)
				fProcessStateChange(piState);

			// Label D8: every call.
			if ((piState & UWMotionTables.ContactAirborne) != 0)
			{
				if ((Abilities & (AbilityLevitate | AbilityFly)) != 0)
				{
					p.Gravity = 0;

					int liAbs = p.Vz < 0 ? -p.Vz : p.Vz;

					p.Vz = liAbs <= 10 ? 0 : p.Precise ? fDecay(p.Vz, 0.8) : I16(p.Vz * 4) / 5;
				}
				else
				{
					if (p.Gravity == 0)
						p.Gravity = -4;

					if ((Abilities & AbilitySlowFall) != 0 && p.Vz <= -0x5E)
					{
						p.Vz = -0x5E;
						p.Speed = p.Speed > 0x14 ? (p.Precise ? fDecay(p.Speed, 0.5) : p.Speed / 2) : 0;
					}
				}
			}
		}

		/// <summary>The state part of ProcessPlayerTileState (labels 56 to D3).</summary>
		private void fProcessStateChange(int piState)
		{
			bool lbInWater = false;
			State leState = State.Normal;

			PreviousTileState = piState;

			if ((piState & 0x22) != 0)
			{
				if ((Abilities & AbilityWaterWalk) == 0)
				{
					if ((piState & UWMotionTables.ContactWater) != 0)
					{
						lbInWater = true;
						SwimCounter = SwimCounterOnEntering;
						Last.EnteredWater = true;
					}
					else
						SwimCounter = 0x10;

					leState = State.Swimming;
				}
			}
			else if ((piState & UWMotionTables.ContactLava) != 0)
				leState = State.Lava;
			else if ((piState & UWMotionTables.ContactAirborne) != 0)
			{
				if ((Abilities & AbilityLevitate) != 0)
					leState = State.Levitating;
				else if ((Abilities & AbilityFly) != 0)
					leState = State.Flying;
				else if ((Abilities & AbilitySlowFall) != 0)
					leState = State.SlowFalling;
			}

			fUpdateMotionState(leState);

			if (!lbInWater)
				SwimCounter = 0;
		}

		/// <summary>
		/// UpdateMotionStateAndSwimming_seg008_E0C (56930): PD[0xB8]'s low bits and PD[0xB6]'s state,
		/// the three speeds as base * ratio / 10, and the motion weight: 0x60, or less when the
		/// carried load exceeds half the capacity - 0x60 - carried * 0x60 / (capacity * 2).
		/// </summary>
		private void fUpdateMotionState(State peState)
		{
			int liState = (int)peState;

			if (CurrentState != peState)
				Last.StateChanged = true;

			SurfaceBits = SurfaceBitsByState[liState];
			CurrentState = peState;

			ForwardSpeed = BaseForwardSpeed * SpeedRatio[liState] / 10;
			SlideSpeed = BaseSlideSpeed * SpeedRatio[liState] / 10;
			BackSpeed = BaseBackSpeed * SpeedRatio[liState] / 10;

			if (MaxWeight != 0 && CarriedWeight * 2 > MaxWeight)
				MotionWeight = FullMotionWeight - ((CarriedWeight * FullMotionWeight) / (MaxWeight * 2));
			else
				MotionWeight = FullMotionWeight;
		}

		/// <summary>seg008_1B2A_D85: the state re-applied (an ability spell ended or began) and the
		/// next frame forced.</summary>
		public void ReapplyState()
		{
			Last = default(FrameResult);
			fProcessTileState(Params.Contact, true);
			UpdateNeeded = true;
		}

		// ------------------------------------------------- Placement

		/// <summary>
		/// PlacePlayerInTile_seg008_6D1 (55985): the handler, everything zeroed, the tile's centre
		/// (tile * 256 + 0x80), the fine z from the table by the floor height plus 0x20 on a slope,
		/// the terrain sampled at the centre eighth (tile * 8 + 3) with step 8 for the contact state,
		/// the state applied, the next frame forced. The host links his object into the tile.
		/// </summary>
		public void PlaceInTile(int piTileX, int piTileY)
		{
			int liType;
			int liFloor;
			int liTerrain;

			if (!mOWorld.TryGetTile(piTileX, piTileY, out liType, out liFloor, out liTerrain))
			{
				liType = 1;
				liFloor = 0;
			}

			int liZ = PlaceZByFloor[liFloor & 0xF];

			if (liType >= 0 && liType < UWMotionTables.TileTraverseFlags.Length
				&& (UWMotionTables.TileTraverseFlags[liType] & 0x20) != 0)
				liZ += 0x20;

			fPlace((piTileX << 8) + 0x80, (piTileY << 8) + 0x80, liZ);
		}

		/// <summary>The loaded save's position (PLAYER.DAT 0x54, 0x56, 0x58 as fine x, y and z) and
		/// heading (0x5A): the same reset as PlaceInTile, at the exact spot.</summary>
		public void PlaceAt(int piFineX, int piFineY, int piFineZ, int piHeading)
		{
			CameraYaw = piHeading & 0xFFFF;
			MotionYaw = CameraYaw;
			fPlace(piFineX, piFineY, piFineZ);
		}

		private void fPlace(int piX, int piY, int piZ)
		{
			UWMotionParams p = Params;

			Last = default(FrameResult);
			mOHandler.IgnoreMask = 0;
			p.Speed = 0;
			p.Gravity = 0;
			p.Ax = 0;
			p.Ay = 0;
			p.Vx = 0;
			p.Vy = 0;
			p.Vz = 0;
			p.Impact = 0;
			p.X = piX & 0xFFFF;
			p.Y = piY & 0xFFFF;
			p.Z = piZ & 0xFFFF;
			p.XFrac = 0;
			p.YFrac = 0;
			p.ZFrac = 0;
			p.VzFrac = 0;
			mdRampCarry = 0;
			p.Step = 8;
			p.Index = 1;
			p.Heading = MotionYaw & 0xFFFF;
			CurrentTileX = (piX >> 8) & 0x3F;
			CurrentTileY = (piY >> 8) & 0x3F;
			MotionCommand = Command.None;

			Core.SetCalc(p.X >> 5, p.Y >> 5, p.Z >> 3, miRadius, miHeight, 1);
			Core.ProcessMotionTileHeights(8);
			p.Contact = Core.ContactStateOf(Core.Flags | Core.AllFlags);

			fProcessTileState(p.Contact, false);
			UpdateNeeded = true;
		}

		// ------------------------------------------------- The easy movement

		/// <summary>The gate of the easy movement (labels 21D and 228, see seg008_1B2A_216 in UWEasyMovement): nothing,
		/// not even a turn, while gravity acts or the speed has reached the motion weight. A
		/// rising or sinking levitation (gravity 0) may step.</summary>
		public bool MayEasyMove
		{
			get { return Params.Gravity == 0 && Params.Speed < MotionWeight; }
		}

		/// <summary>After a turn of the easy movement: the caller of the step (seg034_2F89_334)
		/// clears the speed, as after a step.</summary>
		public void EndEasyTurn()
		{
			Params.Speed = 0;
		}

		/// <summary>
		/// THE EASY STEP of the original, seg008_1B2A_216 (line 55427) from label 253 on - READ
		/// in full 2026-10-06; until then the port swept its own capsule and nudged the params.
		/// <list type="number">
		/// <item>The target: half a tile (0x80 fine) along the camera's yaw, the yaw as a signed
		/// byte heading (idiv by 0x100); backwards a quarter tile (0x40) along the yaw plus half a
		/// circle (shr 8). The offset is GetCoordinateInDirection's, coarse table and all.</item>
		/// <item>The test: CheckIfItemFitsInTile for the adventurer (0x7F, index 1) at the
		/// target's eighth and the player's coarse z, with the step allowance 8; the may-drop flag
		/// is the W command's (the original's +2) or Levitate/Fly (abilities 0x14). No fit, no
		/// step.</item>
		/// <item>Without the W command the floor kind must not change: a target that is neither
		/// ground (1) nor the previous tile state is refused - so a plain step does not go from
		/// the shore into water, but goes from water onto the shore and on in water - unless it
		/// is the air (0x10) and the player floats.</item>
		/// <item>The move: the new fine position (fractions dropped), the tile; the height of
		/// the fit when the player neither runs nor floats or the new height lies at most 8 below
		/// him, else he keeps his height and, unless he floats, gravity -4 starts the fall.</item>
		/// <item>ProcessPlayerTileState with the fit's state; the caller clears the speed.</item>
		/// </list>
		/// NOT HERE: the step's own move-trigger loop (labels 562 to 5C0: ScanForCollisions
		/// around the new spot, Trigger on every a_move trigger in reach). The host's
		/// UWMoveTrigger tests the same reach on the transform the step moves, and firing here as
		/// well would fire twice. The PIT bits written into the player object's frame word are
		/// the host's animation business.
		/// </summary>
		public bool EasyStep(int piCommand, int piCameraYaw)
		{
			UWMotionParams p = Params;

			if (!MayEasyMove)
				return false;

			int liDistance;
			int liHeading;
			bool lbRun = false;

			if (piCommand == UWEasyMovement.CommandStepBack)
			{
				liDistance = UWEasyMovement.BackwardDistance;
				liHeading = ((piCameraYaw + 0x8000) & 0xFFFF) >> 8;
			}
			else if (piCommand == UWEasyMovement.CommandStepForward || piCommand == UWEasyMovement.CommandRunForward)
			{
				lbRun = UWEasyMovement.AllowsDrop(piCommand);
				liDistance = UWEasyMovement.ForwardDistance;
				liHeading = I16(piCameraYaw) / 0x100;
			}
			else
				return false;

			bool lbFloat = (Abilities & UWEasyMovement.FloatingAbilities) != 0;
			int liX = p.X;
			int liY = p.Y;

			UWMobileObjectMotion.StepInDirection(liHeading, liDistance, ref liX, ref liY);

			int liStandZ;
			int liState;

			Core.Mover = Body;

			if (!Core.CheckIfItemFitsInTile(UWObjectMechanics.AdventurerObjectId, 1, I16(liX) / 0x20, I16(liY) / 0x20,
				(p.Z >> 3) & 0x7F, lbRun || lbFloat, UWEasyMovement.DropAllowanceZPos, out liStandZ, out liState))
				return false;

			// Labels 34F to 376.
			if (!lbRun && liState != UWMotionTables.ContactGround && liState != PreviousTileState
				&& !(liState == UWMotionTables.ContactAirborne && lbFloat))
				return false;

			p.X = liX & 0xFFFF;
			p.Y = liY & 0xFFFF;
			p.XFrac = 0;
			p.YFrac = 0;
			CurrentTileX = (p.X >> 8) & 0x3F;
			CurrentTileY = (p.Y >> 8) & 0x3F;

			// Labels 471 to 4C7: the drop allowance is a literal 8 here as well.
			if ((!lbRun && !lbFloat) || (I16(p.Z) >> 3) - UWEasyMovement.DropAllowanceZPos <= liStandZ)
			{
				p.Z = liStandZ << 3;
				p.ZFrac = 0;
			}
			else if (p.Gravity == 0 && !lbFloat)
				p.Gravity = -4;

			// The port's host reads the floor kind from the contact (swimming, lava); the original
			// leaves it to the next frame.
			p.Contact = liState;

			fProcessTileState(liState, false);
			p.Speed = 0;

			return true;
		}

		// ------------------------------------------------- Interventions

		/// <summary>StopFalling_seg008_D5B for the player's object: on the ground the vertical speed
		/// is 0x8D (a small hop), gravity off.</summary>
		public void StopFalling()
		{
			if (!IsAirborne)
				Params.Vz = FlySpeed;

			Params.Gravity = 0;
		}

		/// <summary>BouncePlayer_seg008_D9E (the quake trap): gravity -2 unless already -4, the
		/// vertical speed intensity * 0x2F / 4, the velocity of the frame halved.</summary>
		public void Bounce(int piIntensity)
		{
			UWMotionParams p = Params;

			if (p.Gravity != -4)
				p.Gravity = -2;

			p.Vz = I16(piIntensity * 0x2F) / 4;
			p.Vx /= 2;
			p.Vy /= 2;
		}
	}
}
