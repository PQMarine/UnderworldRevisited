namespace UWDataImport.UWData
{
	/// <summary>
	/// THE EASY MOVEMENT - the three arrows under the compass and, in the original, Shift with
	/// A, D, S, W and X. One click turns in 45 degree steps or steps forward half a tile at
	/// once, the way the old dungeon crawlers move. The user uses it constantly, among other
	/// things because it works in water and makes the Swimming skill largely unnecessary
	/// (2026-09-21).
	///
	/// WHERE IT COMES FROM: one handler, seg034_2F89_334, takes a single number. Five keys
	/// carry it (RegisterEventHandler_seg010_105, 383602-383700) and three screen rectangles
	/// under the compass (RegisterEvent_seg010_83, 383700-383745). The registered key codes
	/// are the UPPERCASE letters - 0x41, 0x44, 0x53, 0x57, 0x58 - so the shift is part of it;
	/// the lowercase codes are not on this handler at all.
	///
	/// The step itself is seg008_1B2A_216 (55427-55876). It does nothing while
	/// Player_MotionArray_unk_10 is set or the momentum has reached MotionWeightRelated, so it
	/// never fights ordinary running. Afterwards the caller clears the momentum, advances the
	/// game clock by ClockAdvance and reports the step to the loop, which is where the noise
	/// base of four comes from (UWCritterRules.PlayerNoiseEasyMove).
	///
	/// THE KEYS ARE OURS, not the original's (per user, 2026-09-21: "I would keep it at WASD
	/// because that is the industry standard by now"). W steps forward and may step off a
	/// ledge, S steps back, A and D turn, and X steps forward WITHOUT the ledge - which is
	/// exactly what the original's S did. In the original the same column meant run forward,
	/// walk forward and walk backward.
	/// </summary>
	public static class UWEasyMovement
	{
		/// <summary>Turn left by an eighth of the circle (the original's -1, key A).</summary>
		public const int CommandTurnLeft = -1;

		/// <summary>Turn right by an eighth (the original's +1, key D).</summary>
		public const int CommandTurnRight = 1;

		/// <summary>Step forward (the original's 0, key S and the middle arrow).</summary>
		public const int CommandStepForward = 0;

		/// <summary>
		/// Step forward AND OFF A LEDGE if there is one (the original's +2, key W). It goes
		/// the same distance as CommandStepForward; the difference is the flag - see
		/// AllowsDrop.
		/// </summary>
		public const int CommandRunForward = 2;

		/// <summary>Step backward (the original's -2, key X).</summary>
		public const int CommandStepBack = -2;

		/// <summary>The yaw is a full 16 bit circle, 0 north, clockwise.</summary>
		public const int Circle = 0x10000;

		/// <summary>An eighth of it - 45 degrees, the turn of one click.</summary>
		public const int EighthStep = Circle / 8;

		/// <summary>The units of PLAYER.DAT 0x54 and 0x56, the player's position: the upper
		/// byte is the tile, the lower one the fraction, so a tile is 0x100.</summary>
		public const int UnitsPerTile = 0x100;

		/// <summary>Half a tile forward (seg008_1B2A_216 label 253, the distance handed to
		/// GetCoordinateInDirection, see seg041_4D in UWMobileObjectMotion.StepInDirection).</summary>
		public const int ForwardDistance = 0x80;

		/// <summary>A quarter tile backward (label 266, with the yaw turned by half a circle).
		/// </summary>
		public const int BackwardDistance = 0x40;

		/// <summary>What one step adds to the game clock (seg034_2F89_334 label 38D). In the
		/// units of UWGameClock, so a two hundred and fortieth of a game minute.</summary>
		public const int ClockAdvance = 0x40;

		/// <summary>The motion abilities that let the step through the tile check even without
		/// ground - Levitate and Fly, bits 2 and 4 (seg008_1B2A_216 label 2EB tests 0x14).
		/// This is why the user can use it while levitating.</summary>
		public const int FloatingAbilities = 0x14;

		/// <summary>
		/// WHAT THE FLAG IS FOR: it lets one step OFF A LEDGE. The user found it by trying,
		/// 2026-09-21 - "with Shift W you can fall down, with Shift S you cannot" - and the
		/// routine says the same. CheckIfItemFitsInTile_seg026_1008 (labels 11ED to 121A)
		/// takes it as its second to last argument: is it set, the tile fits and that is the
		/// end of it. Is it zero and the motion result carries bit 0x800, the target's z minus
		/// the last argument, which is 8 here, is held against the ground, and a bigger gap
		/// refuses the step altogether.
		///
		/// So the flag is not about speed. Levitate and Fly set it for the same reason a step
		/// with W sets it: whoever floats needs no ground under the next tile.
		/// </summary>
		public static bool AllowsDrop(int piCommand)
		{
			return piCommand == CommandRunForward;
		}

		/// <summary>How far the ground may lie below the step without the flag - the 8 that
		/// seg008_1B2A_216 hands CheckIfItemFitsInTile as its last argument, in the zpos units
		/// of the player object, of which a floor height step is worth eight.</summary>
		public const int DropAllowanceZPos = 8;

		/// <summary>
		/// HOLDING A KEY OR AN ARROW repeats the command, which the original does too (per
		/// user, 2026-09-21). How fast is OURS, not the original's: there it is too fast to aim
		/// with, one overshoots easily, and whether that is the game or the DOSBox cycles is
		/// not settled - so it became a setting (UWUserSettings.EasyMovementInterval, the
		/// slider under Controls).
		///
		/// A quarter of a second after trying it out the same day; half a second, the first
		/// guess, was slow. The range ends there too, because everything above was of no use.
		/// </summary>
		public const float DefaultRepeatSeconds = 0.25f;

		public const float MinRepeatSeconds = 0.1f;

		public const float MaxRepeatSeconds = 0.5f;

		/// <summary>What the two arrows beside the slider move it by, and the grid the slider
		/// itself snaps to - ten milliseconds, so a round value can actually be hit.</summary>
		public const float RepeatStepSeconds = 0.01f;

		public static bool IsTurn(int piCommand)
		{
			return piCommand == CommandTurnLeft || piCommand == CommandTurnRight;
		}

		public static bool IsBackward(int piCommand)
		{
			return piCommand == CommandStepBack;
		}

		/// <summary>How far this command carries, in the units of UnitsPerTile; zero for a
		/// turn.</summary>
		public static int GetStepDistance(int piCommand)
		{
			if (piCommand == CommandStepBack)
				return BackwardDistance;

			if (piCommand == CommandStepForward || piCommand == CommandRunForward)
				return ForwardDistance;

			return 0;
		}

		/// <summary>
		/// The new yaw after a turn (seg008_1B2A_216 label 278).
		///
		/// IF THE VIEW IS NOT ON A 45 DEGREE LINE - anything in the low 13 bits - the click
		/// does not turn but SNAPS: to the right onto the next line, to the left back onto the
		/// one just passed. The user described exactly this before it was read
		/// ("if you were not looking at a 45 degree angle the first click put you on the next
		/// 45"). Only from a line does it turn by a full eighth.
		/// </summary>
		public static int Turn(int piYaw, int piCommand)
		{
			piYaw &= Circle - 1;

			if ((piYaw & (EighthStep - 1)) != 0)
			{
				int liFloor = piYaw & ~(EighthStep - 1) & (Circle - 1);

				return (piCommand > 0 ? liFloor + EighthStep : liFloor) & (Circle - 1);
			}

			return (piYaw + (piCommand * EighthStep)) & (Circle - 1);
		}

		/// <summary>The direction the step goes in: backwards is the yaw turned by half a
		/// circle (label 266 adds 0x8000).</summary>
		public static int GetStepYaw(int piYaw, int piCommand)
		{
			if (IsBackward(piCommand))
				piYaw += Circle / 2;

			return piYaw & (Circle - 1);
		}
	}
}
