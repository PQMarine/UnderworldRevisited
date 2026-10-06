namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MOTION PARAMS BLOCK of UW.EXE (0x28 bytes, five static copies: player 0x2780, land
	/// NPC 0x27A8, flier 0x27D0, objects and missiles 0x27F8, swimmer 0x2820), read 2026-10-05
	/// for the motion rework. One per mover; the player's persists between frames, the others
	/// are filled from the object record before every step (UWMobileObjectMotion.InitMotionParams)
	/// and written back after it.
	///
	/// Units: X, Y fine (tile * 256 + eighth * 32 + 0..31), Z fine (coarse z * 8 + 0..7), the
	/// velocities in 1/65536 tile per PIT tick (0x2000 per call of dt ticks = one eighth of a
	/// tile), Heading 16 bits with 0x10000 a full turn, 0 = +y, 0x4000 = +x. Everything is kept
	/// as int but used as the original's int16 where it wraps.
	/// </summary>
	public sealed class UWMotionParams
	{
		/// <summary>+0x00, +0x02, +0x04: the position.</summary>
		public int X;

		public int Y;

		public int Z;

		/// <summary>+0x06, +0x08: the velocity of this call, from the heading and the speed.</summary>
		public int Vx;

		public int Vy;

		/// <summary>+0x0A: the vertical velocity; gravity * dt is added in every InitalMotionCalc.</summary>
		public int Vz;

		/// <summary>+0x0C, +0x0E: external acceleration (UW2 currents); only ever 0 in UW1.</summary>
		public int Ax;

		public int Ay;

		/// <summary>+0x10: 0, -4 (normal) or -2 (the player's leap).</summary>
		public int Gravity;

		/// <summary>+0x12: the time budget of this step in PIT ticks; used up by the sub-steps.</summary>
		public int Dt;

		/// <summary>+0x14: the horizontal speed along the heading.</summary>
		public int Speed;

		/// <summary>+0x16: elasticity 0..15 (COMOBJ word +6 bits 5-8; the player 5).</summary>
		public int Elasticity;

		/// <summary>+0x17: the collision style - 0x80 slides along walls and stops head-on (player,
		/// NPCs), 0 bounces (objects), 0x40 never deflects.</summary>
		public int Style;

		/// <summary>+0x18: the mass (COMOBJ word +1 &gt;&gt; 4).</summary>
		public int Mass;

		/// <summary>+0x1A: COMOBJ byte 6 bit 4, the low ground friction.</summary>
		public int LowFriction;

		/// <summary>+0x1B: the hit points (mobile) or quality (static), carried through.</summary>
		public int Hp;

		/// <summary>+0x1C: COMOBJ byte 8, the resistances.</summary>
		public int Resistances;

		/// <summary>+0x1E: the heading.</summary>
		public int Heading;

		/// <summary>+0x20: the object index (the player is 1).</summary>
		public int Index;

		/// <summary>+0x22: the radius in eighths (COMOBJ byte 1 &amp; 7).</summary>
		public int Radius;

		/// <summary>+0x23: the height in coarse z (COMOBJ byte 0).</summary>
		public int Height;

		/// <summary>+0x24: the step height in coarse z - 8 for the player and NPCs, 0 for objects.</summary>
		public int Step;

		/// <summary>+0x25: the contact state, one bit (UWMotionTables.Contact...).</summary>
		public int Contact;

		/// <summary>+0x26: the impact of this call, |dv| from bounces and wall hits; above 0x100
		/// the mover hurts itself by impact &gt;&gt; 8.</summary>
		public int Impact;

		/// <summary>
		/// THE PRECISION MODE of the port (2026-10-06, for the Smooth motion of a player who gets
		/// motion sick; the Opus analysis smooth-float-analysis-2026-10-06 in the private notes):
		/// the original's core drops the fraction below one fine unit at the end of every call
		/// (StoreNewXYZH), so the slow motions stall at few ticks a call - the DOSBox cycles=max
		/// water bug. With this flag the core carries that fraction in XFrac/YFrac/ZFrac between
		/// the calls and gives the partial step's minor axis its full share, so small steps add
		/// up exactly. Off for everything that is the original: the Original motion mode, the
		/// creatures, the objects and missiles.
		/// </summary>
		public bool Precise;

		/// <summary>The carried sub-fine fraction of X, Y (0..255 of one fine unit) and Z (0..255
		/// of one fine z), only with Precise.</summary>
		public int XFrac;

		public int YFrac;

		public int ZFrac;

		/// <summary>
		/// FINE TICKS (the second stage of the precision mode, 2026-10-06): Dt is counted in 1/256
		/// of a PIT tick, so a call can take exactly the time a rendered frame took (deltaTime *
		/// 256 * 256) and the view needs no interpolation. The core's products with Dt run in 32
		/// bits with a shift of 8 then, and the gravity keeps its fraction in VzFrac between the
		/// calls. Only with Precise; the original's wraps stay on the plain path.
		/// </summary>
		public bool FineTicks;

		/// <summary>The fraction of the vertical speed below one unit that the gravity's product
		/// with a fine Dt left over (0..255), only with FineTicks.</summary>
		public int VzFrac;

		/// <summary>1/256 tick: what Dt counts in with FineTicks.</summary>
		public const int FineTicksPerTick = 256;

		public const int SlideStyle = 0x80;

		public const int BounceStyle = 0;

		public const int NoDeflectStyle = 0x40;
	}

	/// <summary>
	/// A MOTION HANDLER of UW.EXE (12 bytes): which result flags of a sub-step are ignored,
	/// which call the mover's callback, and the terrain mask of GetCollisionHeightState.
	/// Objects and missiles: 0x2874 - ignore 0x1000 for a weightless object, no callback.
	/// </summary>
	public sealed class UWMotionHandler
	{
		/// <summary>The callback: gets the flags by reference and may change them; true ends the
		/// step after the sub-step is undone.</summary>
		public delegate bool CallbackDelegate(ref int piFlags);

		/// <summary>+0: the result flags that are ignored.</summary>
		public int IgnoreMask;

		/// <summary>+2: the result flags that call the callback.</summary>
		public int CallbackMask;

		/// <summary>+4: GetCollisionHeightState - bit 0x80: the mover cannot stand on objects (fliers,
		/// swimmers); the floor-kind bits: the mover does not follow the floor there (land NPCs over
		/// water, swimmers over ground).</summary>
		public int TerrainMask;

		public CallbackDelegate Callback;

		/// <summary>The handler of objects and missiles (0x2874): the ignore mask alone changes, per
		/// object (COMOBJ byte 3 bit 3, weightless: 0x1000).</summary>
		public static UWMotionHandler ForObject(bool pbWeightless)
		{
			return new UWMotionHandler { IgnoreMask = pbWeightless ? 0x1000 : 0 };
		}
	}
}
