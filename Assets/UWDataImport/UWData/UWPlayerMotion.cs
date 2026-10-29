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
	///
	/// MODERN (the character motor, Docs/MOTOR-PLAN.md M4, 2026-10-28): with Modern set, a frame
	/// keeps everything shared - the commands and their speeds by state, the ramp, the jump's and
	/// the fly commands' vertical speeds, the tile state machine, the fall's damage from the impact,
	/// the triggers and pushes through the world - and hands only the motion to UWCharacterMotor:
	/// the body slides along walls and objects, the view never follows a deflection, back and the
	/// sides run at three quarters of the forward speed (decision 1). The params stay the one state
	/// the host and the save read; a change from outside (a placement, the easy step, a bounce)
	/// is taken over at the next frame.
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

			/// <summary>The second game's ice (contact 8, the floor's terrain class 3; UW2 plan phase 2
			/// step 4 part C). The first game's floors never give it.</summary>
			Ice = 3,

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

		/// <summary>The jump's gravity per tick: -4, -2 under Leap (fJump).</summary>
		public const int JumpGravity = -4;

		public const int LeapGravity = -2;

		/// <summary>MotionWeightRelated_C8 when carrying no more than half the capacity.</summary>
		public const int FullMotionWeight = 0x60;

		/// <summary>dseg_D4: the speed ratio in tenths per state.</summary>
		internal static readonly int[] SpeedRatio = { 10, 3, 5, 10, 1, 7, 2 };

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

		/// <summary>The COMOBJ table, for the motor's objects.</summary>
		private readonly UWCommonObjectProperties mOProperties;

		/// <summary>The character motor of Modern, made at its first frame.</summary>
		private UWCharacterMotor mOMotor;

		/// <summary>The params as Modern's last frame wrote them: a difference at the next frame is a
		/// change from outside, which the motor takes over.</summary>
		private int miModernX = -1;

		private int miModernY;

		private int miModernZ;

		private int miModernXFrac;

		private int miModernYFrac;

		private int miModernZFrac;

		private int miModernVz;

		private int miModernGravity;

		/// <summary>The motion of the character motor in place of the core (Modern; the host sets it
		/// with the precision mode and fine ticks).</summary>
		public bool Modern;

		/// <summary>The motor of Modern (null before its first frame), for the host and the checks.</summary>
		public UWCharacterMotor Motor
		{
			get { return mOMotor; }
		}

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

		/// <summary>The Swimming skill, for the second game's swimming speed (IUWMotionVariant.ScaleSpeed;
		/// UW2 plan phase 2 step 4). The first game's share does not read it.</summary>
		public int SwimmingSkill;

		/// <summary>The 1-based level the player is on, for the second game's falls without damage
		/// (IUWMotionVariant.FallHurts). Set by the host.</summary>
		public int LevelNumber = 1;

		/// <summary>The second game's floor terrain word at a tile (UWTerrainUw2.Word of the floor
		/// texture), for the ice's grip and the current's direction; null reads 0. Set by the host.</summary>
		public System.Func<int, int, int> TerrainWordAt;

		/// <summary>The second game's ice under the feet with grip (frictionless; PHYSICS.C:56).</summary>
		public bool Frictionless { get; private set; }

		/// <summary>The second game's 'slide': the current's or the slope's direction, -1 none. Kept
		/// between frames - a water tile without a current lets the last one go on (PHYSICS.C:440).</summary>
		private int miSlide = -1;

		/// <summary>The heading and speed of the player's own motion before a current was added
		/// (lasth, lasts; -1 none), given back after the step (phys_affect_player, PHYSICS.C:561).</summary>
		private int miOwnHeading;

		private int miOwnSpeed = -1;

		/// <summary>PD[0x4A] and PD[0x4C], for the motion weight.</summary>
		public int CarriedWeight;

		public int MaxWeight;

		/// <summary>PlayerActualForwardSpeed_2496, SlideSpeed_2498, BackwardsSpeed_249A: the base
		/// speeds scaled by the state; at first the rule set's (set in the constructor).</summary>
		public int ForwardSpeed;

		public int SlideSpeed;

		public int BackSpeed;

		/// <summary>MotionWeightRelated_C8: the speed change one frame may make.</summary>
		public int MotionWeight;

		/// <summary>PD[0xB6] bits 0-2.</summary>
		public State CurrentState { get; private set; }

		/// <summary>PD[0xB8] bits 0-4: what the player stands in or hovers by.</summary>
		public int SurfaceBits { get; private set; }

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
				int liWhole = UWRounding.Floor(ldExact);

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

			int liScaled = UWRounding.Truncate(piPerOriginalFrame * fFrameTicks() / OriginalFrameTicks);

			return liScaled < 1 ? 1 : liScaled;
		}

		/// <summary>A per-call factor of the original (four fifths, one half) raised to this
		/// frame's share of the original's frame.</summary>
		private int fDecay(int piValue, double pdFactorPerOriginalFrame)
		{
			if (!Params.Precise)
				return UWRounding.Truncate(piValue * pdFactorPerOriginalFrame);

			double ldFactor = System.Math.Pow(pdFactorPerOriginalFrame, fFrameTicks() / OriginalFrameTicks);

			return UWRounding.HalfToEven(piValue * ldFactor);
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
			// The speeds start at the rule set's of the world's session (motion.*, cleanup phase 10;
			// field initialisers reading UWRules.Current until wave 4).
			UWRulesMotion lORules = pOWorld.Session.Rules.Motion;

			ForwardSpeed = lORules.Speed.Forward;
			SlideSpeed = lORules.Speed.Slide;
			BackSpeed = lORules.Speed.Back;
			MotionWeight = lORules.MotionWeight;

			mOWorld = pOWorld;
			mOProperties = pOProperties;
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

		/// <summary>The feet in zpos steps, the params' height over eight: what phys_affect_player
		/// writes into the player object (UnderworldExhumed PHYSICS.C:401, PN.z >> 3). The one
		/// home of the player's coarse height since cleanup 4.1 (2026-10-18; UWCharacter.GetFeetZPos
		/// read the CharacterController's bounds before).</summary>
		public int FeetZPos
		{
			get { return (Params.Z >> 3) & 0x7F; }
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
					ZPos = FeetZPos,
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

			if (Modern)
			{
				fModernFrame(piDt);

				return true;
			}

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

			// The second game's ice blends from the motion before the input (oldh, olds).
			int liOldHeading = p.Heading & 0xFFFF;
			int liOldSpeed = p.Speed;

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

			int liIceElasticity = -1;

			if (mOWorld.Session.Game.Motion.IceAndCurrents)
				liIceElasticity = fIceAndCurrents(liOldHeading, liOldSpeed);

			p.Dt = piDt;
			p.Elasticity = 5;
			p.Style = (p.Ax | p.Ay | p.Gravity) == 0 ? UWMotionParams.SlideStyle : UWMotionParams.BounceStyle;

			// The second game: on ice no slide along walls, and the ice's own bounce unless falling
			// (PN.flags 0x80 only without motion_state 0x84; PN.bounce 5 when falling there).
			if (CurrentState == State.Ice)
			{
				p.Style = UWMotionParams.BounceStyle;

				if (p.Gravity == 0 && liIceElasticity >= 0)
					p.Elasticity = liIceElasticity;
			}

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

		/// <summary>
		/// THE SECOND GAME'S GROUND (UW2 plan phase 2 step 4 part C; Underworld Exhumed uw2
		/// motion/PHYSICS.C:399-470, set_player_phys_params): ON ICE whose floor terrain bits 3-5 are not 7
		/// (7 has none of ice's effects) the motion after the input is blended from the one before by
		/// munge_vectors, the strength (bits &lt;&lt; 3) + 8 - (old speed / 0x2F &lt;&lt; 2), 0x10 more under the
		/// speed 0x2F, else 8 more when speeding up on the flat and 4 less on a slope; the bounce
		/// becomes 0xF - bits; an ice slope (tile types 6-9) pushes downhill with speed 0x2F. SWIMMING the
		/// same bits less one are a current's direction (a tile without one lets the last go on), added
		/// with speed 0x8D, the sum capped at the run, the player's own motion kept to be given back after
		/// the step. The directions 0..3 are south, north, west and east (0x8000, 0, 0xC000, 0x4000).
		/// Returns the ice's bounce, -1 for none.
		///
		/// THE PRECISION MODE blends per call as well, so the ice's strength is a rate per original frame
		/// (fPerFrame). DEVIATIONS: the original's skid sound and effect on a sharp change (set_effect 0x80,
		/// 4) is not made yet; on flat ice the original's push angle is an uninitialised local - a slope's
		/// direction left from an earlier tile pushes there with what the stack holds; ours pushes with 0.
		/// </summary>
		private int fIceAndCurrents(int piOldHeading, int piOldSpeed)
		{
			UWMotionParams p = Params;
			int liTileX = (p.X >> 8) & 0x3F;
			int liTileY = (p.Y >> 8) & 0x3F;
			int liBits = ((TerrainWordAt != null ? TerrainWordAt(liTileX, liTileY) : 0) & 0x38) >> 3;
			int liAngle = 0;
			int liElasticity = -1;

			mOWorld.TryGetTile(liTileX, liTileY, out int liType, out int liFloorNibble, out int liTerrain);

			if (CurrentState == State.Ice)
			{
				if (liBits != 7)
				{
					Frictionless = true;

					if (liType >= SlopeFirstType && liType <= SlopeLastType)
					{
						miSlide = liType - SlopeFirstType;
						liAngle = IceSlopePush;
					}

					liElasticity = 0xF - liBits;

					int liStrength = (liBits << 3) + 8 - ((piOldSpeed / IceSlopePush) << 2);

					if (piOldSpeed < IceSlopePush)
						liStrength += 0x10;
					else if (p.Speed > piOldSpeed)
						liStrength += miSlide == -1 ? 8 : -4;

					UWMotionVariantUw2.MungeVectors(UWMotionVariantUw2.MungeIce, piOldHeading, piOldSpeed, MotionYaw & 0xFFFF, p.Speed,
						fPerFrame(liStrength), out int liHeading, out int liSpeed);

					MotionYaw = liHeading;
					p.Speed = liSpeed;
				}
				else
				{
					miSlide = -1;
					Frictionless = false;
				}
			}
			else if (CurrentState == State.Swimming)
			{
				int liPrevious = miSlide;

				miSlide = liBits - 1;

				if (miSlide == -1)
					miSlide = liPrevious;

				liAngle = CurrentPush;
			}
			else
			{
				miSlide = -1;
			}

			miOwnSpeed = -1;

			if (miSlide != -1)
			{
				int liDirection = 0;

				if (miSlide <= 3)
				{
					if (miSlide > 1)
						liDirection = 0x4000;

					if ((miSlide & 1) == 0)
						liDirection += 0x8000;
				}

				UWMotionVariantUw2.MungeVectors(UWMotionVariantUw2.MungeAdd, MotionYaw & 0xFFFF, p.Speed, liDirection & 0xFFFF, liAngle, 0x20,
					out int liHeading, out int liSpeed);

				if (liAngle != IceSlopePush)
				{
					miOwnHeading = MotionYaw & 0xFFFF;
					miOwnSpeed = p.Speed;
				}

				if (liSpeed > ForwardSpeed)
					liSpeed = ForwardSpeed;

				MotionYaw = liHeading;
				p.Speed = liSpeed;
			}

			return liElasticity;
		}

		/// <summary>The slopes' tile types (rising north, south, east, west).</summary>
		private const int SlopeFirstType = 6;

		private const int SlopeLastType = 9;

		/// <summary>An ice slope pushes with this speed (and it marks the ice's speed steps).</summary>
		public const int IceSlopePush = 0x2F;

		/// <summary>A current pushes with this speed.</summary>
		public const int CurrentPush = 0x8D;

		/// <summary>(dt * TurnStep) * (TurnInput / 4) / 4, in 16 bits - with FineTicks in 32 bits
		/// and shifted back.</summary>
		private int fTurn(int piDt)
		{
			int liTurnStep = mOWorld.Session.Game.Motion.ScaleTurn(mOWorld.Session.Rules.Motion.TurnStep, (int)CurrentState);

			if (Params.FineTicks)
				return (int)((((long)piDt * liTurnStep * (TurnInput / 4)) / 4) >> 8);

			return I16(I16(I16(piDt * liTurnStep) * (TurnInput / 4)) / 4);
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
					liTarget = fFreeTarget();
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
					liTarget = Modern ? fModernAside(fModernWalk()) : BackSpeed;
					Side = -2;
					miCommandOffset = -0x8000;
					break;

				case Command.SlideLeft:
					liYaw = (CameraYaw - 0x4000) & 0xFFFF;
					liTarget = Modern ? fModernAside(fModernWalk()) : SlideSpeed;
					Side = -1;
					miCommandOffset = -0x4000;
					break;

				case Command.SlideRight:
					liYaw = (CameraYaw + 0x4000) & 0xFFFF;
					liTarget = Modern ? fModernAside(fModernWalk()) : SlideSpeed;
					Side = 1;
					miCommandOffset = 0x4000;
					break;

				case Command.FlyUp:
					Side = 0;
					p.Vz = mOWorld.Session.Rules.Motion.Speed.Fly;
					p.Gravity = 0;
					break;

				case Command.FlyDown:
					Side = 0;
					p.Vz = -mOWorld.Session.Rules.Motion.Speed.Fly;
					p.Gravity = 0;
					break;
			}

			if (lbSetYaw)
				MotionYaw = liYaw & 0xFFFF;

			return liTarget;
		}

		/// <summary>
		/// THE SPEED OF A FREE DIRECTION (per user, 2026-10-09: with the gamepad sideways and backwards
		/// ran at the run's speed - a stick is never exactly sideways or back, so it was always Free,
		/// and Free took the walk's speed in every direction; the keys' S+D did the same). The
		/// original knows only its four directions with their own speeds - forward the walk, sideways
		/// SlideSpeed, back BackSpeed -, so a free direction now takes the speed between them by its
		/// angle: from the walk's straight ahead to SlideSpeed at the side, from there to BackSpeed
		/// straight back. Side and back scale with the walk like forward (a stick's deflection,
		/// UWPlayerMovement.fStickWalk), so a full stick or the keys give the original's values.
		/// Chosen per user ("A - it feels bumpier than the gamepad did, but the game was like that")
		/// over the milder side and back speeds of today's shooters, which would make dodging easier.
		/// </summary>
		private int fFreeTarget()
		{
			int liForward = I16(I16((Walk >> 2) * ForwardSpeed) / 32);
			int liSide = Modern ? fModernAside(Walk) : (int)((long)SlideSpeed * Walk / WalkRun);
			int liBack = Modern ? fModernAside(Walk) : (int)((long)BackSpeed * Walk / WalkRun);
			int liAngle = System.Math.Abs((int)(short)(FreeOffset & 0xFFFF));

			if (liAngle <= 0x4000)
				return liForward + (int)((long)(liSide - liForward) * liAngle / 0x4000);

			return liSide + (int)((long)(liBack - liSide) * (liAngle - 0x4000) / 0x4000);
		}

		/// <summary>The walk value Modern's back and slides scale by: the input's (the modern scheme's
		/// keys and stick, the slow walk lowering it - per user, 2026-10-28: "Shift should count for
		/// sideways and backwards too"), else the run's (the classic scheme's keys and the pointer's
		/// bottom zone send no walk with them, as the original's fixed speeds need none).</summary>
		private int fModernWalk()
		{
			return Walk > 0 ? Walk : WalkRun;
		}

		/// <summary>Modern's speed to the side and back (decision 1, per user 2026-10-27): three
		/// quarters of the walk's forward speed at the walk value piWalk.</summary>
		private int fModernAside(int piWalk)
		{
			return (I16(I16((piWalk >> 2) * ForwardSpeed) / 32) * 3) / 4;
		}

		/// <summary>Case 7 of the command: the take-off.</summary>
		private void fJump()
		{
			UWMotionParams p = Params;
			UWRulesMotion.JumpRules lORules = mOWorld.Session.Rules.Motion.Jump;

			p.Vz = lORules.Speed;

			if (p.Z > 0x280)
			{
				p.Vz = I16(p.Vz * 5) / 6;

				if (p.Z > 0x2C0)
					p.Vz = I16(p.Vz << 1) / 3;
			}

			p.Gravity = (Abilities & AbilityLeap) != 0 ? lORules.LeapGravity : lORules.Gravity;

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

			fApplyTile();

			int liHeading = p.Heading & 0xFFFF;
			bool lbSecondGame = mOWorld.Session.Game.Motion.IceAndCurrents;

			// The second game gives the player's own motion back after a current (lasth, lasts).
			if (lbSecondGame && miOwnSpeed != -1)
			{
				MotionYaw = miOwnHeading;
				p.Speed = miOwnSpeed;
			}

			// The head-on stop; the second game's only on the ground, off the ice, without a current.
			bool lbStops = !lbSecondGame || (p.Gravity == 0 && CurrentState != State.Ice && miOwnSpeed == -1);

			if (p.Impact != 0 && liHeading == (MotionYaw & 0xFFFF) && lbStops)
			{
				p.Speed = 0;
				Last.HeadOnStop = true;
			}

			if (liHeading != (MotionYaw & 0xFFFF))
			{
				MotionYaw = liHeading;
				Last.Deflected = true;

				int liTarget = (liHeading - miCommandOffset) & 0xFFFF;

				// The second game's view follows only while no current or slope pushes (slide == -1).
				if ((p.Style & UWMotionParams.SlideStyle) != 0 && (!lbSecondGame || miSlide == -1))
				{
					int liDiff = I16(CameraYaw - liTarget);
					int liAbs = liDiff < 0 ? -liDiff : liDiff;
					int liFollow = fPerFrame(mOWorld.Session.Game.Motion.FacingFollow);

					if (liAbs < liFollow)
						CameraYaw = liTarget;
					else if ((liDiff & 0xFFFF) < 0x7FFF)
						CameraYaw = (CameraYaw - liFollow) & 0xFFFF;
					else
						CameraYaw = (CameraYaw + liFollow) & 0xFFFF;
				}
			}

			fApplyImpact(p.Vz != 0);
			fProcessTileState(p.Contact, false);
			UpdateNeeded = false;
		}

		/// <summary>The tile relink of ApplyPlayerMotion: the host's, when the tile changed.</summary>
		private void fApplyTile()
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
		}

		/// <summary>The impact block of ApplyPlayerMotion: the value doubled while the vertical speed
		/// stands (pbDoubled), the Acrobat check, the damage and the landing sound.</summary>
		private void fApplyImpact(bool pbDoubled)
		{
			UWMotionParams p = Params;

			if (p.Impact != 0 && p.Elasticity > 0)
			{
				UWRulesMotion.ImpactRules lORules = mOWorld.Session.Rules.Motion.Impact;
				int liValue = (p.Impact & 0xFFFF) >> 8;

				// The second game's world of levels 65 to 72 hurts nobody falling (the value goes to 0
				// before the doubling, as there).
				if (!mOWorld.Session.Game.Motion.FallHurts(LevelNumber))
					liValue = 0;

				if (pbDoubled)
					liValue <<= 1;

				if (UWSkillCheck.IsSuccess(UWSkillCheck.Check(AcrobatSkill, liValue * lORules.AcrobatCheckFactor)))
					liValue = I16(liValue * (lORules.AcrobatFull - AcrobatSkill)) / lORules.AcrobatFull;

				Last.ImpactValue = liValue;

				if (liValue > lORules.DamageThreshold)
					Last.FallDamage = liValue;

				if (liValue > 1 || (p.Contact & UWMotionTables.ContactAirborne) != 0)
				{
					Last.LandingSound = true;
					Last.LandingVolume = (liValue * 4) - 60;
				}

				p.Impact = 0;
			}
		}

		// ------------------------------------------------- Modern: the character motor

		/// <summary>
		/// One frame of Modern. The shared part as the core's frame has it (fInitialCalc: the command,
		/// the ramp, the jump, the fly commands, the ignore mask), then the motor: the velocity along
		/// the motion yaw at the params' speed - the original reads no command in the air, so in the
		/// air the speed stands as it took off -, the vertical speed and gravity, the ledge rule of
		/// the player's callback (below three tenths of the forward speed, not falling), no fall under
		/// levitation or flight. Back into the params: the position with its fractions, the vertical
		/// speed and gravity, the contact state; a landing makes the impact the core's bounce would
		/// (the vertical speed over 15, times 15 less the elasticity), doubled where the core bounces
		/// on (its vertical speed after the bounce at 0x8D or more). Then the tile, the impact, the
		/// tile state - the shared rest of ApplyPlayerMotion; no head-on stop, no deflection.
		/// </summary>
		private void fModernFrame(int piDt)
		{
			UWMotionParams p = Params;

			if (mOMotor == null)
				mOMotor = new UWCharacterMotor(mOWorld, mOProperties) { Height = miHeight };

			fInitialCalc(piDt);
			fModernTakeOver();

			UWCharacterMotor m = mOMotor;
			double lfYaw = (p.Heading & 0xFFFF) * (System.Math.PI * 2.0 / 65536.0);
			double lfTicks = p.FineTicks ? piDt / (double)UWMotionParams.FineTicksPerTick : piDt;
			bool lbFloats = (Abilities & (AbilityLevitate | AbilityFly)) != 0;

			m.Vx = System.Math.Sin(lfYaw) * p.Speed / 65536.0;
			m.Vy = System.Math.Cos(lfYaw) * p.Speed / 65536.0;
			m.NoFall = lbFloats;
			m.HoldAtLedges = !lbFloats && p.Vz == 0 && I16(p.Speed * 10) < I16(ForwardSpeed * 3);

			if (m.Grounded && (p.Vz > 0 || p.Gravity != 0))
				m.Jump(p.Vz, p.Gravity);

			m.Advance(lfTicks);

			// A wall met in the air took the motion into it: the speed and the motion yaw the
			// next frames keep in the air are what is left (the original reads no command there).
			if (System.Math.Abs(m.Vx - (System.Math.Sin(lfYaw) * p.Speed / 65536.0)) > 1.0e-12
				|| System.Math.Abs(m.Vy - (System.Math.Cos(lfYaw) * p.Speed / 65536.0)) > 1.0e-12)
			{
				double lfSpeed = System.Math.Sqrt((m.Vx * m.Vx) + (m.Vy * m.Vy)) * 65536.0;

				p.Speed = UWRounding.Floor(lfSpeed + 0.5);

				if (p.Speed > 0)
					MotionYaw = UWRounding.Floor((System.Math.Atan2(m.Vx, m.Vy) * 65536.0 / (System.Math.PI * 2.0)) + 0.5) & 0xFFFF;
			}

			fModernWriteBack();
			fApplyTile();

			// THE IMPACTS as the core's: a wall met in the air adds the speed it took times
			// (15 - elasticity) / 15 (fTurnAlongWall's bounce; per user 2026-10-28, a jump that
			// bounced off a ledge's face did no damage), doubled while the body is still in the air;
			// a landing the core's bounce (the vertical speed over 15, times 15 less the elasticity),
			// doubled where the core bounces on (its vertical speed after the bounce at 0x8D or more).
			bool lbDoubled = !m.Grounded;

			if (m.WallImpact > 0.0)
				p.Impact = I16(p.Impact + (I16(UWRounding.Floor(m.WallImpact) * (15 - p.Elasticity)) / 15));

			if (m.LandedVz < 0.0)
			{
				int liVz = (int)m.LandedVz;
				int liBounce = -(liVz / 15);

				p.Impact = I16(p.Impact + System.Math.Abs(I16(liBounce * (15 - p.Elasticity))));
				lbDoubled = I16(liBounce * p.Elasticity) >= FlySpeed;
			}

			if (p.Impact != 0)
				fApplyImpact(lbDoubled);

			// The snapshot before the state machine: what it does to the vertical speed and gravity
			// (the levitation's decay, the slow fall's cap, the fall's gravity) is taken over next frame.
			fModernSnapshot();
			fProcessTileState(p.Contact, false);
			UpdateNeeded = false;
		}

		/// <summary>The params' state into the motor where it changed from outside since Modern's
		/// last frame (the first frame, a placement, the easy step, a bounce, the state machine's
		/// gravity and vertical speed).</summary>
		private void fModernTakeOver()
		{
			UWMotionParams p = Params;
			UWCharacterMotor m = mOMotor;

			if (p.X != miModernX || p.Y != miModernY || p.Z != miModernZ
				|| p.XFrac != miModernXFrac || p.YFrac != miModernYFrac || p.ZFrac != miModernZFrac)
			{
				m.Place((p.X + (p.XFrac / 256.0)) / 256.0, (p.Y + (p.YFrac / 256.0)) / 256.0, p.Z + (p.ZFrac / 256.0));

				if ((p.Contact & UWMotionTables.ContactAirborne) != 0 || p.Gravity != 0 || p.Vz != 0)
					m.Jump(p.Vz, p.Gravity);
			}

			if (p.Vz != miModernVz || p.Gravity != miModernGravity)
			{
				if (m.Grounded && (p.Vz > 0 || p.Gravity != 0))
					m.Jump(p.Vz, p.Gravity);
				else
				{
					m.Vz = p.Vz;
					m.Gravity = p.Gravity;
				}
			}
		}

		/// <summary>The motor's state into the params: the position in fine units and their
		/// fractions, the vertical speed and gravity (none on the ground), the contact state - in the
		/// air 0x10, on an object the ground, on a floor the core's own sample at the spot
		/// (ProcessMotionTileHeights, as PlacePlayerInTile takes it: the water edge too), unless that
		/// sample finds no support, then the terrain of the floor the motor stands on.</summary>
		private void fModernWriteBack()
		{
			UWMotionParams p = Params;
			UWCharacterMotor m = mOMotor;
			double lfX = m.X * 256.0;
			double lfY = m.Y * 256.0;
			double lfZ = m.Zf;

			p.X = UWRounding.Floor(lfX) & 0xFFFF;
			p.Y = UWRounding.Floor(lfY) & 0xFFFF;
			p.Z = UWRounding.Floor(lfZ) & 0xFFFF;
			p.XFrac = UWRounding.Floor((lfX - System.Math.Floor(lfX)) * 256.0) & 0xFF;
			p.YFrac = UWRounding.Floor((lfY - System.Math.Floor(lfY)) * 256.0) & 0xFF;
			p.ZFrac = UWRounding.Floor((lfZ - System.Math.Floor(lfZ)) * 256.0) & 0xFF;
			p.Heading = MotionYaw & 0xFFFF;

			if (m.Grounded)
			{
				p.Vz = 0;
				p.VzFrac = 0;
				p.Gravity = 0;

				if (m.OnObject)
					p.Contact = UWMotionTables.ContactGround;
				else
				{
					Core.SetCalc(p.X >> 5, p.Y >> 5, (p.Z >> 3) & 0x7F, miRadius, miHeight, 1);
					Core.ProcessMotionTileHeights(p.Step);

					int liContact = Core.ContactStateOf(Core.Flags | Core.AllFlags);

					p.Contact = (liContact & UWMotionTables.ContactAirborne) != 0 ? 1 << m.GroundTerrain : liContact;
				}
			}
			else
			{
				p.Vz = I16((int)m.Vz);
				p.Gravity = (int)m.Gravity;
				p.Contact = UWMotionTables.ContactAirborne;
			}
		}

		/// <summary>The params as this frame leaves them, to tell an outside change at the next.</summary>
		private void fModernSnapshot()
		{
			UWMotionParams p = Params;

			miModernX = p.X;
			miModernY = p.Y;
			miModernZ = p.Z;
			miModernXFrac = p.XFrac;
			miModernYFrac = p.YFrac;
			miModernZFrac = p.ZFrac;
			miModernVz = p.Vz;
			miModernGravity = p.Gravity;
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

		/// <summary>The state part of ProcessPlayerTileState (labels 56 to D3). The swim counter
		/// PD[0xB9] it sets there - 0x60 entering water, 0x10 at the edge, 0 out of the water - is
		/// UWPlayerTerrainRules' since cleanup step 4.4 (2026-10-17): the core's copy was never read,
		/// the host's is the one the view, the missiles and the save game use.</summary>
		private void fProcessStateChange(int piState)
		{
			State leState = State.Normal;

			PreviousTileState = piState;

			if ((piState & 0x22) != 0)
			{
				if ((Abilities & AbilityWaterWalk) == 0)
				{
					if ((piState & UWMotionTables.ContactWater) != 0)
						Last.EnteredWater = true;

					leState = State.Swimming;
				}
			}
			else if ((piState & UWMotionTables.ContactLava) != 0)
				leState = State.Lava;
			else if ((piState & UWMotionTables.ContactIce) != 0)
				leState = State.Ice;
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

			// The speeds and the ramp are the rule set's (motion.*, cleanup phase 10); the state's
			// share of them is the game's (IUWMotionVariant; the first game: tenths).
			UWRulesMotion lORules = mOWorld.Session.Rules.Motion;
			IUWMotionVariant lIMotion = mOWorld.Session.Game.Motion;

			ForwardSpeed = lIMotion.ScaleSpeed(lORules.Speed, lORules.Speed.Forward, liState, SwimmingSkill);
			SlideSpeed = lIMotion.ScaleSpeed(lORules.Speed, lORules.Speed.Slide, liState, SwimmingSkill);
			BackSpeed = lIMotion.ScaleSpeed(lORules.Speed, lORules.Speed.Back, liState, SwimmingSkill);

			if (MaxWeight != 0 && CarriedWeight * 2 > MaxWeight)
				MotionWeight = lORules.MotionWeight - ((CarriedWeight * lORules.MotionWeight) / (MaxWeight * 2));
			else
				MotionWeight = lORules.MotionWeight;
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

		// ------------------------------------------------- The pointer scheme

		/// <summary>The view's movement area in the original's pixels: dseg_7364 wide, dseg_7280
		/// high (the rectangle seg034_2F89_0 hands on, 172 by 113).</summary>
		public const int PointerAreaWidth = 172;

		public const int PointerAreaHeight = 113;

		/// <summary>The turn input at the far side edge is this times a third of the width over
		/// the width - 0x80 at most, about 127 at the area's real edge.</summary>
		public const int PointerTurnRange = 0x180;

		/// <summary>The walk value at the top edge is this times three fifths of the height over
		/// the height - about 115, a little above the W key's 0x70.</summary>
		public const int PointerWalkRange = 0xC0;

		/// <summary>dseg_768: the commands of the bottom fifth, by thirds of the width - slide
		/// left, back, slide right.</summary>
		public static readonly Command[] PointerBottomCommands = { Command.SlideLeft, Command.Back, Command.SlideRight };

		/// <summary>
		/// Whether the buttons drive the motion (seg034_2F89_4E labels 78 to 98): the left one
		/// alone, or the left one with the right while the fight mode is on (PD[0x5F] bit 1, set
		/// by SetInteractionMode for mode 2). Bit 0 is the left button, bit 1 the right.
		/// </summary>
		public static bool PointerDrives(int piButtons, bool pbFightMode)
		{
			return piButtons == 1 || ((piButtons & 1) != 0 && pbFightMode);
		}

		/// <summary>
		/// THE POINTER SCHEME'S VALUES, seg034_2F89_4E (line 114173) labels 9B to 16A - READ in
		/// full 2026-10-06; the port had scaled the W key's walk and the A/D keys' turn by the
		/// zone distance until then. piX from the area's left, piY from its BOTTOM, in the
		/// original's pixels; all divisions are the original's truncating idiv:
		/// <list type="bullet">
		/// <item>the bottom fifth (y &lt; h/5): the command of PointerBottomCommands by x * 3 / w,
		/// no walk, no turn;</item>
		/// <item>else the walk command (1), with a turn left of the first third
		/// -((w/3 - x) * 0x180) / w, right of the second (x - 2w/3) * 0x180 / w, and a walk above
		/// two fifths (y - 2h/5) * 0xC0 / h. The band between a fifth and two fifths turns
		/// without walking; its middle third stands still.</item>
		/// </list>
		/// </summary>
		public static void PointerCommand(int piX, int piY, int piWidth, int piHeight, out Command peCommand, out int piWalk, out int piTurn)
		{
			piWalk = 0;
			piTurn = 0;

			if (piWidth <= 0 || piHeight <= 0)
			{
				peCommand = Command.None;

				return;
			}

			if (piHeight / 5 > piY)
			{
				int liThird = (piX * 3) / piWidth;

				peCommand = PointerBottomCommands[liThird < 0 ? 0 : liThird > 2 ? 2 : liThird];

				return;
			}

			if (piWidth / 3 > piX)
				piTurn = -((piWidth / 3 - piX) * PointerTurnRange) / piWidth;

			if ((piWidth * 2) / 3 < piX)
				piTurn = ((piX - ((piWidth * 2) / 3)) * PointerTurnRange) / piWidth;

			if ((piHeight * 2) / 5 < piY)
				piWalk = ((piY - ((piHeight * 2) / 5)) * PointerWalkRange) / piHeight;

			peCommand = Command.Walk;
		}

		/// <summary>
		/// Both buttons without the fight mode (labels 172 to 198): the jump, unless the player
		/// is in the air (params +0x25 bit 0x10) or swims (PD[0xB8] = 1), then the walk command.
		/// The walk and turn values are NOT cleared there, so the last frame's carry on - a run
		/// with the left button and the right added jumps on the run, and held, jumps again on
		/// every landing.
		/// </summary>
		public Command PointerJumpCommand
		{
			get { return IsAirborne || SurfaceBits == 1 ? Command.Walk : Command.Jump; }
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
		/// ALSO HERE since 2026-10-06: the step's own trigger loop (labels 4FB to 5C0),
		/// ScanForCollisions around the new spot and every a_move trigger in reach to the world
		/// (IUWMotionWorld.TriggerMove) - after every step; UWMoveTrigger fired on entering before.
		/// The PIT bits written into the player object's frame word are the host's animation
		/// business.
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

			if (Modern)
			{
				// Decision 3 of the motor plan: the easy step asks the motor whether the body can
				// stand at the target; the state there is the motor's ground, or the core's sample.
				if (!fModernFits(liX, liY, lbRun || lbFloat, out liStandZ, out liState))
					return false;
			}
			else if (!Core.CheckIfItemFitsInTile(UWObjectMechanics.AdventurerObjectId, 1, I16(liX) / 0x20, I16(liY) / 0x20,
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

			// Labels 4FB to 5C0: a scan at the new spot, and every a_move trigger in reach is told -
			// after every step, inside reach or not before. The world fires it (TriggerMove).
			Core.Mover = Body;
			Core.SetCalc(I16(p.X) / 0x20, I16(p.Y) / 0x20, (p.Z >> 3) & 0x7F, miRadius, miHeight, 1);
			Core.ScanForCollisions(false, false);
			Core.SortCollisions();

			for (int liAt = (sbyte)Core.First; liAt < (sbyte)Core.First + (Core.Overlap & 0xFF) && liAt < Core.Count; liAt++)
			{
				UWMotionCore.Record lORecord = Core.Records[liAt];

				if (lORecord.Body.ItemId == UWObjectMechanics.MoveTriggerId)
					mOWorld.TriggerMove(1, lORecord.Body.Index, (CurrentTileX + lORecord.Dx) & 0x3F, (CurrentTileY + lORecord.Dy) & 0x3F);
			}

			return true;
		}

		/// <summary>Modern's answer to the easy step: the motor at the player's spot and height asked
		/// whether it can stand at the target (fine piX, piY); the stand height in coarse z and the
		/// contact state there (an object carries as ground; a floor by the core's sample, or its
		/// terrain where the sample finds no support).</summary>
		private bool fModernFits(int piX, int piY, bool pbMayDrop, out int piStandZ, out int piState)
		{
			UWMotionParams p = Params;

			piStandZ = 0;
			piState = UWMotionTables.ContactGround;

			if (mOMotor == null)
				mOMotor = new UWCharacterMotor(mOWorld, mOProperties) { Height = miHeight };

			mOMotor.Place((p.X + (p.XFrac / 256.0)) / 256.0, (p.Y + (p.YFrac / 256.0)) / 256.0, p.Z + (p.ZFrac / 256.0));

			double lfGround;
			bool lbOnObject;
			int liTerrain;

			if (!mOMotor.TryStand((piX & 0xFFFF) / 256.0, (piY & 0xFFFF) / 256.0, pbMayDrop, out lfGround, out lbOnObject, out liTerrain))
				return false;

			piStandZ = UWRounding.Floor(lfGround) >> 3;

			if (lbOnObject)
				return true;

			Core.SetCalc(I16(piX) / 0x20, I16(piY) / 0x20, piStandZ & 0x7F, miRadius, miHeight, 1);
			Core.ProcessMotionTileHeights(p.Step);

			int liContact = Core.ContactStateOf(Core.Flags | Core.AllFlags);

			piState = (liContact & UWMotionTables.ContactAirborne) != 0 ? 1 << liTerrain : liContact;

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
