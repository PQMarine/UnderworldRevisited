using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SHARED MOTION CORE OF UW.EXE - CalculateMotion_seg030_B and everything under it,
	/// transcribed 2026-10-05 for the motion rework (stage 1: objects and missiles; the creatures
	/// and the player follow on the same core). Instruction-faithful: the 16-bit arithmetic, the
	/// truncating divisions and the rounding quirks are kept where the original has them, because
	/// they shape how a thrown object flies, bounces and comes to rest.
	///
	/// One motion step: the heading and speed give a velocity, the time budget (dt in PIT ticks)
	/// is walked in SUB-STEPS of one eighth of a tile along the dominant axis (a DDA, the minor
	/// axis and the height carried as fractions). Before the step and whenever a sub-step enters a
	/// new eighth cell, the terrain is sampled at the mover's centre and the four corners of its
	/// box (ProcessMotionTileHeights) and the objects around it are collected into up to nine
	/// collision records (ScanForCollisions, SortCollisions). The vertical motion stops at a
	/// target (SetCollisionTarget: the floor, an object top, the ceiling, a head target) with a
	/// bounce (DoCollision); a wall or an object in the way undoes the sub-step and deflects
	/// (Deflect, TurnAlongWall: slide for the player and NPCs, bounce for objects); after every
	/// deflection or bounce the remaining time is re-integrated with the new velocity (Retime).
	/// The step rule (GetCollisionHeightState) lets a mover with a step height climb a floor or an
	/// object top within reach at once. Sixteen collisions freeze the mover.
	///
	/// The state the original keeps in its data segment (the calc array 0x2768, the stepper
	/// globals 0x410.., the terrain scratch, the collision table) lives in this instance; the
	/// world (tiles, the objects of a tile, the RNG, the side effects of a hit) is the host's,
	/// through IUWMotionWorld. Reading aid: the private notes, motion-core.md sections 7-9.
	/// </summary>
	public sealed class UWMotionCore
	{
		/// <summary>One collision record (6 bytes at CollisionRecords_dseg_26C6).</summary>
		public struct Record
		{
			/// <summary>+0: zpos + COMOBJ height as a byte (+1 for a height of 0).</summary>
			public int Top;

			/// <summary>+1: zpos.</summary>
			public int Bottom;

			/// <summary>+2 bits 0-5: 9 at creation, 0x10 the mover's centre lies inside the
			/// footprint, 0x20 the pair was handled by CollideObjects.</summary>
			public int Flags;

			/// <summary>+4: the object's tile relative to the mover's.</summary>
			public int Dx;

			public int Dy;

			/// <summary>The object itself (the original looks it up by the index in +2).</summary>
			public UWMotionBody Body;
		}

		private struct Corner
		{
			/// <summary>t = 4 + dx + 3 * dy into the 3x3 around the mover's tile.</summary>
			public int T;

			/// <summary>The eighth inside that tile, 0..7.</summary>
			public int X;

			public int Y;

			public int Flags;
		}

		private const int MaxRecords = 9;

		private const int NotLoaded = 0x1111;

		private readonly IUWMotionWorld mOWorld;

		private readonly UWCommonObjectProperties mOProperties;

		private UWMotionParams mOParams;

		private UWMotionHandler mOHandler;

		public UWMotionCore(IUWMotionWorld pOWorld, UWCommonObjectProperties pOProperties)
		{
			mOWorld = pOWorld;
			mOProperties = pOProperties;
		}

		// ------------------------------------------------- The mover

		/// <summary>The mover as the collision scan and CollideObjects need it; the caller sets it
		/// before a step and reads CollidedWithMobile back after it (byte +0x15 bit 7).</summary>
		public UWMotionBody Mover;

		// ------------------------------------------------- The calc array (0x2768)

		/// <summary>+0, +2: the position in eighths (tile * 8 + eighth); +4: the coarse z.</summary>
		public int CalcX;

		public int CalcY;

		public int CalcZ;

		/// <summary>+6 (276E): the heading during the step; deflections change it.</summary>
		public int CalcHeading;

		public int CalcRadius;

		public int CalcHeight;

		public int CalcIndex;

		/// <summary>+0xC (2774): the terrain flags of the centre; +0xE (2776): OR of the centre and
		/// the four corners.</summary>
		public int Flags;

		public int AllFlags;

		/// <summary>+0x10 (2778): the centre floor in coarse z, 0x80 solid; +0x11 (2779): the
		/// highest corner floor, 0x80 when a corner is blocked.</summary>
		public int CentreFloor;

		public int MaxFloor;

		/// <summary>+0x12 (277A): the wall normal octant 0..7, 9 none; +0x13 (277B): the second
		/// octant, which nothing reads.</summary>
		public int WallOctant;

		public int SecondOctant;

		/// <summary>+0x14 (277C): the records; +0x15 (277D): how many of them, from First on, overlap
		/// the mover's z range; +0x16 (277E): the first record whose top is above the feet.</summary>
		public int Count;

		public int Overlap;

		public int First;

		/// <summary>+0x17 (277F): the deflection cooldown.</summary>
		public int Cooldown;

		public readonly Record[] Records = new Record[MaxRecords];

		// ------------------------------------------------- The terrain scratch (seg026)

		/// <summary>26F6: the attr words of the 3x3 tiles, NotLoaded until read.</summary>
		private readonly int[] miAttr = new int[9];

		/// <summary>270A + 5 * i: the four corners, 271E: the centre.</summary>
		private readonly Corner[] mOCorners = new Corner[5];

		/// <summary>2724: the mover's tile.</summary>
		private int miTileX;

		private int miTileY;

		/// <summary>272A low byte: no corner reached the diagonal edge test.</summary>
		private bool mbNoDiagonalEdge;

		/// <summary>272B, 26C2: the mover's eighth inside its tile; 26C0/26C1/26C3/26C4: the scan box.</summary>
		private int miCx;

		private int miCy;

		private int miXMin;

		private int miXMax;

		private int miYMin;

		private int miYMax;

		private readonly List<UWMotionBody> mOBodies = new List<UWMotionBody>();

		/// <summary>The x/y pairs of the translation, indexed by axis (0 x, 1 y).</summary>
		private readonly int[] miFrac = new int[2];

		private readonly int[] miCell = new int[2];

		// ------------------------------------------------- The stepper globals (0x410..)

		/// <summary>410/412/414: the fractions of x, y (0..0x1FFF, 0x2000 = one eighth) and z
		/// (0..0x7FF, 0x800 = one coarse z).</summary>
		private int miFracX;

		private int miFracY;

		private int miZFrac;

		/// <summary>416/418: the per-step increments of x and y.</summary>
		private readonly int[] miStep = new int[2];

		/// <summary>41A: the z direction, +-0x800 or 0.</summary>
		private int miZDir;

		/// <summary>41C/41E: the dominant and the minor axis, 0 = x, 1 = y.</summary>
		private int miDominant;

		private int miMinor;

		/// <summary>420: whole eighth-steps in this call; 422: the remainder; 424: ticks per
		/// eighth-step; 426: steps done.</summary>
		private int miWhole;

		private int miRemainder;

		private int miPerStep;

		private int miDone;

		/// <summary>428: the record the vertical motion will meet, -1 floor or ceiling; 429: its
		/// item id; 42B: the coarse z where the vertical motion stops; 42F: the lowest bottom - 1 of
		/// the solid records above the feet, 0x7F none; 431: the vertical rate per eighth-step.</summary>
		private int miCollisionIndex;

		private int miCollisionItemId;

		private int miCollisionZ;

		private int miLowestAbove;

		private int miRate;

		/// <summary>2848: GetCollisionHeightState changed or tried to change z; 284C low: the
		/// collision z came from the floor rule; 284D: the previous contact state.</summary>
		private bool mbLanded;

		private bool mbTargetFromFloor;

		private int miPreviousContact;

		/// <summary>2761/2762: the tile of the last hit (CollideObjects).</summary>
		public int HitTileX;

		public int HitTileY;

		// ------------------------------------------------- Helpers

		private static int I16(int piValue)
		{
			return (short)piValue;
		}

		/// <summary>The original's 16-bit abs: neg if negative, so -0x8000 stays -0x8000.</summary>
		private static int Abs16(int piValue)
		{
			short lsValue = (short)piValue;

			return lsValue < 0 ? (short)(-lsValue) : lsValue;
		}

		private UWCommonObjectProperties.Entry fEntry(int piItemId)
		{
			UWCommonObjectProperties.Entry lOEntry;

			if (mOProperties != null && mOProperties.TryGet(piItemId, out lOEntry))
				return lOEntry;

			return default;
		}

		private static bool fIsCreatureClass(int piItemId)
		{
			return ((piItemId >> 6) & 7) == 1;
		}

		// ------------------------------------------------- CalculateMotion_seg030_B (line 102606)

		/// <summary>
		/// One motion step of the mover: the params go in with the position, heading, speed,
		/// vertical speed, gravity and the time budget Dt, and come out with the new position,
		/// heading, speeds, contact state and impact. Mover must be set before.
		/// </summary>
		public void CalculateMotion(UWMotionParams pOParams, UWMotionHandler pOHandler)
		{
			mOParams = pOParams;
			mOHandler = pOHandler;
			miPreviousContact = pOParams.Contact;
			Cooldown = 0;

			if (!fInitialMotionCalc(true, true))
				return;

			for (int liIteration = 0; miDone < miWhole + 1;)
			{
				// Sixteen collisions: the mover freezes, and the position of this call is not stored.
				if (liIteration++ == 16)
				{
					pOParams.Gravity = 0;
					pOParams.Vz = 0;
					pOParams.Speed = 0;

					return;
				}

				if (fProcessCollisions(1) != 0)
					fDoTileCollisionMaybe();
			}

			fStoreNewXYZH();
		}

		// ------------------------------------------------- InitalMotionCalc_seg030_410 (line 103373)

		private bool fInitialMotionCalc(bool pbResetPosition, bool pbScan)
		{
			UWMotionParams p = mOParams;
			int liSin;
			int liCos;

			UWMotionTables.SinCos(p.Heading, out liSin, out liCos);

			// LXMUL and a 32-bit sar: the product's floor, then the low word.
			p.Vx = I16((liSin * p.Speed) >> 15);
			p.Vy = I16((liCos * p.Speed) >> 15);

			if (p.FineTicks)
			{
				// Dt in 1/256 tick: the products shifted back, the gravity's fraction carried.
				p.Vx = I16(p.Vx + (int)(((long)p.Ax * p.Dt) >> 8));
				p.Vy = I16(p.Vy + (int)(((long)p.Ay * p.Dt) >> 8));

				long liGravity = ((long)p.Gravity * p.Dt) + p.VzFrac;
				long liWhole = liGravity >> 8;

				p.Vz = I16(p.Vz + (int)liWhole);
				p.VzFrac = (int)(liGravity - (liWhole << 8));
			}
			else
			{
				p.Vx = I16(p.Vx + I16(p.Ax * p.Dt));
				p.Vy = I16(p.Vy + I16(p.Ay * p.Dt));
				p.Vz = I16(p.Vz + I16(p.Gravity * p.Dt));
			}

			if ((p.Vx | p.Vy | p.Vz) == 0)
				return false;

			if (pbResetPosition)
				fLoadCalc();

			miDominant = Abs16(p.Vx) > Abs16(p.Vy) ? 0 : 1;
			miMinor = (miDominant + 1) % 2;

			int liVDominant = miDominant == 0 ? p.Vx : p.Vy;
			int liVMinor = miDominant == 0 ? p.Vy : p.Vx;

			miStep[miDominant] = liVDominant > 0 ? 0x2000 : -0x2000;

			if (liVDominant != 0)
			{
				// The product is CUT to 16 bits (cwd), the quotient shifted in 16 bits.
				miStep[miMinor] = I16((I16((miStep[miDominant] / 0x100) * liVMinor) / liVDominant) << 8);

				if (p.FineTicks)
				{
					// The fine budget: |v| * dt / 256 in 32 bits, the ticks per eighth-step in 1/256.
					long liFine = ((long)Abs16(liVDominant) * p.Dt) >> 8;

					miWhole = (int)(liFine >> 13);
					miRemainder = (int)(liFine & 0x1FFF);
					miPerStep = (int)(0x200000L / Abs16(liVDominant));
				}
				else
				{
					// v * dt in 16 bits, LOW WORD ONLY: the wrap of the fast movers (motion-core.md 9).
					int liT = I16(liVDominant * p.Dt);

					miWhole = Abs16(liT) >> 13;
					miRemainder = Abs16(liT) & 0x1FFF;
					miPerStep = Abs16(0x2000 / liVDominant);
				}
			}
			else
			{
				miStep[miMinor] = 1;
				miStep[miDominant] = 1;
				miWhole = 0;
				miRemainder = 0;
				miPerStep = p.Dt;
			}

			miDone = 0;

			if ((CalcIndex == 1 || p.Vz != 0) && pbScan)
			{
				ProcessMotionTileHeights(p.Step);
				ScanForCollisions(false, false);
			}

			if (p.Vz == 0)
			{
				miZDir = 0;

				return true;
			}

			miZDir = p.Vz > 0 ? 0x800 : -0x800;

			SetCollisionTarget();

			if (liVDominant != 0)
			{
				// |vz| * 256 / |v_dom|, all 32-bit, truncating.
				long liQuotient = ((long)p.Vz * (miStep[miDominant] / 0x2000) * 0x100)
					/ ((long)liVDominant * (miZDir / 0x800));

				if (liQuotient >= -0x8000 && liQuotient <= 0x7FFF)
				{
					miRate = (int)liQuotient;

					return true;
				}

				// Steep (|vz| > 128 |v_dom|): the remainder is dropped.
				miRemainder = 0;
			}

			if (p.FineTicks)
			{
				long liSteep = (((long)(p.Vz >> 1) * p.Dt) >> 8) >> 5;

				miRate = (int)System.Math.Min(0x7FFF, System.Math.Abs(liSteep));
			}
			else
				miRate = Abs16(I16(I16((p.Vz >> 1) * p.Dt) >> 5));

			return true;
		}

		// ------------------------------------------------- seg030_2B26_91 and StoreNewXYZH_seg030_2B26_7FE

		private void fLoadCalc()
		{
			UWMotionParams p = mOParams;

			CalcHeading = p.Heading & 0xFFFF;
			CalcRadius = p.Radius;
			CalcHeight = p.Height;
			CalcIndex = p.Index;
			CalcX = p.X >> 5;
			CalcY = p.Y >> 5;
			CalcZ = p.Z >> 3;
			miFracX = (p.X & 0x1F) << 8;
			miFracY = (p.Y & 0x1F) << 8;
			miZFrac = (p.Z & 7) << 8;

			// The precision mode (UWMotionParams.Precise): the sub-fine fraction carried from the
			// last call comes back in.
			if (p.Precise)
			{
				miFracX |= p.XFrac & 0xFF;
				miFracY |= p.YFrac & 0xFF;
				miZFrac |= p.ZFrac & 0xFF;
			}
		}

		/// <summary>The calc array from a position in eighths and a coarse z, for the callers that
		/// run the terrain sampling and the scan on their own (the settling, the launch).</summary>
		public void SetCalc(int piX8, int piY8, int piZ, int piRadius, int piHeight, int piIndex)
		{
			CalcX = piX8;
			CalcY = piY8;
			CalcZ = piZ;
			CalcRadius = piRadius;
			CalcHeight = piHeight;
			CalcIndex = piIndex;
		}

		private void fStoreNewXYZH()
		{
			UWMotionParams p = mOParams;

			p.X = I16(CalcX << 5) + (miFracX >> 8);
			p.Y = I16(CalcY << 5) + (miFracY >> 8);
			p.Z = I16(CalcZ << 3) + (miZFrac >> 8);

			// The precision mode keeps what the original drops here: the fraction below one fine
			// unit, for the next call.
			p.XFrac = p.Precise ? miFracX & 0xFF : 0;
			p.YFrac = p.Precise ? miFracY & 0xFF : 0;
			p.ZFrac = p.Precise ? miZFrac & 0xFF : 0;

			// The player on a slope snaps to its surface (seg026_1A5).
			if ((Flags & 0x2000) != 0 && Abs16(CalcZ - (CentreFloor & 0xFF)) <= (p.Radius & 0xFF)
				&& CalcIndex == 1 && p.Vz == 0)
			{
				p.Z = fSlopeZ(p.X, p.Y);
				p.ZFrac = 0;
			}

			p.Heading = CalcHeading & 0xFFFF;
		}

		/// <summary>seg026_1A5: the fine z on a slope - the fine position across the slope (0..255,
		/// mirrored as 0xFF - v for types 7 and 9) &gt;&gt; 2 plus the floor height * 64. READ in
		/// full 2026-10-05 (stage 3); the player alone uses it.</summary>
		private int fSlopeZ(int piX, int piY)
		{
			int liType = miAttr[4] & 0xF;
			int liHeight = (miAttr[4] & 0xF0) >> 4;
			int liAcross;

			switch (liType)
			{
				case 6: liAcross = piY & 0xFF; break;
				case 7: liAcross = 0xFF - (piY & 0xFF); break;
				case 8: liAcross = piX & 0xFF; break;
				case 9: liAcross = 0xFF - (piX & 0xFF); break;
				default: liAcross = 0; break;
			}

			return (liAcross >> 2) + (liHeight * 64);
		}

		// ------------------------------------------------- Retime, Stop, State

		private void fRetime(bool pbScan)
		{
			UWMotionParams p = mOParams;

			if (p.FineTicks)
				p.Dt = (int)System.Math.Max(0L, p.Dt - ((long)miPerStep * miDone));
			else
				p.Dt = I16(p.Dt - I16(miPerStep * miDone));

			if (p.Dt > 0 && fInitialMotionCalc(false, pbScan))
				return;

			miDone = miWhole + 1;
		}

		private void fStop()
		{
			UWMotionParams p = mOParams;

			p.Vy = 0;
			p.Ay = 0;
			p.Vx = 0;
			p.Ax = 0;
			p.Vz = 0;
			p.Gravity = 0;
			p.Speed = 0;
			miDone = miWhole + 1;
		}

		/// <summary>seg030_2B26_11F2: the contact state from the result flags - for the callers
		/// that place a mover and need its state (PlacePlayerInTile, UWPlayerMotion).</summary>
		public int ContactStateOf(int piFlags)
		{
			return fState(piFlags);
		}

		/// <summary>seg030_2B26_11F2: the contact state from the result flags.</summary>
		private int fState(int piFlags)
		{
			if ((piFlags & 0x1000) != 0)
				return UWMotionTables.ContactAirborne;

			if ((piFlags & 4) != 0)
			{
				// The player at a water edge: on the floor with the water bit but not the water kind.
				if (CalcIndex == 1 && (piFlags & 3) == 1 && (piFlags & 0xF8) != (piFlags & 0x90))
					return 0x20;

				return 1 << (piFlags & 3);
			}

			if ((piFlags & 0x88) != 0)
				return UWMotionTables.ContactGround;

			if ((piFlags & 0x10) != 0)
				return UWMotionTables.ContactWater;

			if ((piFlags & 0x20) != 0)
				return UWMotionTables.ContactLava;

			return UWMotionTables.ContactTerrain3;
		}

		// ------------------------------------------------- ProcessCollisions_seg030_115F (line 105453)

		private int fProcessCollisions(int piDirection)
		{
			bool lbRerun = false;

			if (piDirection == -1)
			{
				// Undo the last sub-step: rescan and retarget at the CURRENT cell.
				ScanForCollisions(false, false);
				SetCollisionTarget();
				mOParams.Contact = miPreviousContact;

				if (mbLanded)
					lbRerun = true;
			}
			else
				Cooldown--;

			int liResult = mOParams.Vz != 0 ? fMaybeGravityZ(0, piDirection) : fTranslateXY(0, piDirection);

			if (lbRerun)
			{
				int liFlags = GetCollisionHeightState();

				miPreviousContact = mOParams.Contact;
				mOParams.Contact = fState(liFlags);
			}

			return liResult;
		}

		// ------------------------------------------------- LikelyTranslateXY_seg030_8A4 (line 104045)

		private int fTranslateXY(int piReturn, int piDirection)
		{
			int[] liFrac = miFrac;
			int[] liCalc = miCell;

			liFrac[0] = miFracX;
			liFrac[1] = miFracY;
			liCalc[0] = CalcX;
			liCalc[1] = CalcY;

			if (miWhole + (piDirection == -1 ? 1 : 0) > miDone)
			{
				// A FULL eighth-step: the dominant axis moves a cell, the minor carries its fraction.
				liFrac[miMinor] = I16(piDirection == 1 ? liFrac[miMinor] + miStep[miMinor] : liFrac[miMinor] - miStep[miMinor]);

				if (I16(miStep[miDominant] * piDirection) > 0)
					liCalc[miDominant]++;
				else
					liCalc[miDominant]--;

				if ((liFrac[miMinor] & 0xE000) != 0)
				{
					if (liFrac[miMinor] > 0)
						liCalc[miMinor]++;
					else
						liCalc[miMinor]--;

					liFrac[miMinor] &= 0x1FFF;
				}

				miDone += piDirection;
				piReturn = 1;
			}
			else
			{
				// The PARTIAL step at the end: the remainder along the dominant axis, its share on the minor.
				// The original's product in whole fine units; the precision mode takes the full
				// remainder (the same quotient without the two truncations).
				int liD = mOParams.Precise
					? (int)(((long)miStep[miMinor] * miRemainder) >> 13)
					: I16((miStep[miMinor] >> 5) * (miRemainder >> 8));

				liFrac[miMinor] = I16(piDirection == 1 ? liFrac[miMinor] + liD : liFrac[miMinor] - liD);

				if (I16(miStep[miDominant] * piDirection) > 0)
					liFrac[miDominant] = I16(liFrac[miDominant] + miRemainder);
				else
					liFrac[miDominant] = I16(liFrac[miDominant] - miRemainder);

				for (int liAxis = 0; liAxis < 2; liAxis++)
				{
					if ((liFrac[liAxis] & 0xE000) == 0)
						continue;

					piReturn = 1;

					if (liFrac[liAxis] > 0)
						liCalc[liAxis]++;
					else
						liCalc[liAxis]--;

					liFrac[liAxis] &= 0x1FFF;
				}

				miDone += piDirection;
			}

			miFracX = liFrac[0];
			miFracY = liFrac[1];
			CalcX = liCalc[0];
			CalcY = liCalc[1];

			return piReturn;
		}

		// ------------------------------------------------- MAYBEGRAVITYZ_seg030_FDF (line 105192)

		private int fMaybeGravityZ(int piReturn, int piDirection)
		{
			UWMotionParams p = mOParams;
			long liDz;

			if (miWhole + (piDirection == -1 ? 1 : 0) > miDone)
			{
				// A full step: rate << 5 in 16 bits - it WRAPS for a rate of 0x400 and up.
				int liR = I16(miRate << 5);

				liDz = I16(piDirection * miZDir) > 0 ? liR : I16(-liR);
			}
			else
			{
				int liT = I16(I16(piDirection * (miZDir / 0x800)) * miRate);

				if (miRemainder != 0)
					liDz = ((long)miRemainder * liT) / 0x100;
				else
					liDz = 0x40 * (long)liT;
			}

			liDz += I16(miZFrac);

			// The rounding of a negative dz: an exact multiple of 0x800 goes one coarse z too far.
			int liN = liDz >= 0
				? I16((int)(liDz / 0x800))
				: -I16((int)(System.Math.Abs(liDz) / 0x800) + 1);

			miZFrac = I16((int)liDz) & 0x7FF;

			if (piDirection == -1)
				CalcZ += liN;
			else if (liN > 0)
			{
				p.Contact = UWMotionTables.ContactAirborne;

				if (CalcZ + liN > miCollisionZ)
				{
					fDoCollision();

					return 0;
				}

				CalcZ += liN;
			}
			else if (liN < 0)
			{
				p.Contact = UWMotionTables.ContactAirborne;

				if (CalcZ + liN < miCollisionZ)
				{
					fDoCollision();

					return 0;
				}

				CalcZ += liN;
			}

			return fTranslateXY(piReturn, piDirection);
		}

		// ------------------------------------------------- DoTileCollisionMaybe_seg030_2B26_1640 (line 106199)

		private void fDoTileCollisionMaybe()
		{
			UWMotionHandler h = mOHandler;
			bool lbHit = false;
			int liFlags = GetCollisionHeightState();

			miPreviousContact = mOParams.Contact;
			mOParams.Contact = fState(liFlags);

			if ((liFlags & 0xC000) != 0)
			{
				fProcessCollisions(-1);

				if ((liFlags & 0x4000) != 0)
					fStop();
				else
					miDone = miWhole + 1;

				return;
			}

			liFlags &= ~h.IgnoreMask;

			if (liFlags == 0)
				return;

			if ((liFlags & h.CallbackMask) != 0 && h.Callback != null && h.Callback(ref liFlags))
			{
				fProcessCollisions(-1);
				miDone = miWhole + 1;

				return;
			}

			if ((liFlags & 0x700) != 0)
			{
				fDeflect((liFlags & 0x400) == 0);
				lbHit = true;
			}

			// Start falling - outside the wall test, so it also runs after a deflection.
			if ((liFlags & 0x1000) != 0 && mOParams.Gravity == 0)
			{
				mOParams.Gravity = -4;
				fRetime(lbHit);
			}
		}

		// ------------------------------------------------- Deflect seg030_2B26_BDF, TurnAlongWall seg030_2B26_A47

		private void fDeflect(bool pbTerrain)
		{
			if ((sbyte)Cooldown > 0)
			{
				fProcessCollisions(-1);
				miDone = miWhole + 1;

				return;
			}

			int liOctant = 9;

			if (pbTerrain)
			{
				fWallOctant();
				liOctant = WallOctant & 0xFF;
			}

			// An object hit, or no normal: a wall along the dominant axis.
			if (liOctant == 9)
				liOctant = miDominant * 2;

			fProcessCollisions(-1);

			if (fTurnAlongWall(UWMotionTables.WallHeading[liOctant]))
			{
				fRetime(true);
				Cooldown = 2;
			}
			else
				miDone = miWhole + 1;
		}

		private bool fTurnAlongWall(int piWall)
		{
			UWMotionParams p = mOParams;
			int liE = p.Elasticity;

			if ((p.Style & UWMotionParams.NoDeflectStyle) != 0)
				return false;

			if (p.Vz != 0)
				p.Vz = I16((p.Vz / 16) * (liE + 1));

			int liWall = piWall;
			int liD = I16(liWall - CalcHeading);

			// The nearer of the two directions along the wall.
			if (liD > 0x4000 || liD < -0x4000)
			{
				liWall = (liWall + 0x8000) & 0xFFFF;
				liD = I16(liD + 0x8000);
			}

			if ((p.Style & UWMotionParams.SlideStyle) != 0)
			{
				// SLIDE: head-on within 22.5 degrees of the normal stops the mover.
				if (Abs16(liD) > 0x3000 && Abs16(liD) < 0x5000)
				{
					p.Impact = I16(p.Impact + p.Speed);

					return false;
				}

				if (CalcHeading != liWall)
					CalcHeading = liWall;
				else
				{
					// Already parallel: a jump to an eighth corner, the side by chance.
					int liA = (liWall & 0x4000) != 0 ? 1 : 0;
					int liB = 1;

					if (mOWorld.Random() % 2 != 0)
					{
						liA = 1 - liA;
						liB = 1 - liB;
					}

					miFracX = liA * 0x1F00;
					miFracY = liB * 0x1F00;
				}
			}
			else
			{
				// BOUNCE: head-on it mirrors, glancing it turns mostly along the wall.
				if (Abs16(liD) > 0x3000 && Abs16(liD) < 0x5000)
					CalcHeading = (liWall + liD) & 0xFFFF;
				else
					CalcHeading = (liWall + I16((liD / 15) * liE)) & 0xFFFF;

				p.Impact = I16(p.Impact + (I16(p.Speed * (15 - liE)) / 15));
				p.Speed = I16(p.Speed * liE) / 15;
			}

			p.Heading = CalcHeading & 0xFFFF;

			return true;
		}

		// ------------------------------------------------- Hop seg030_2B26_C49 (line 104639)

		/// <summary>An object landing on a ledge corner or on something it cannot rest on hops off,
		/// up to 67.5 degrees either way. It writes the params heading only: the calc heading is
		/// copied back at the end of the call, so the stored heading is the pre-hop one.</summary>
		private void fHop()
		{
			UWMotionParams p = mOParams;

			p.Vz = 0xEB;
			p.Gravity = -4;

			if (p.Speed < 0xEB)
				p.Speed = 0xEB;

			p.Contact = UWMotionTables.ContactAirborne;
			p.Heading = (p.Heading - 0x3000 + (mOWorld.Random() % 0x6000)) & 0xFFFF;
		}

		// ------------------------------------------------- DoCollision_seg030_2B26_C93 (line 104680)

		private void fDoCollision()
		{
			UWMotionParams p = mOParams;
			int liMass = fEntry(Mover.ItemId).MassTenthStones & 0xFFF;
			int liE = p.Elasticity;

			if (miRate > 4)
			{
				if (p.FineTicks)
				{
					// The same time in fine ticks, in 32 bits.
					long liUsed = ((long)System.Math.Abs(CalcZ - miCollisionZ) * miPerStep) << 4;

					p.Dt = (int)System.Math.Max(0L, p.Dt - (liUsed / (miRate / 4)));
				}
				else
				{
					// The time used up to the impact, in 16 bits - it can wrap (motion-core.md 9, item 6).
					int liT = I16(Abs16(CalcZ - miCollisionZ) * miPerStep);

					liT = I16(liT << 4);
					p.Dt = I16(p.Dt - (liT / (miRate / 4)));
				}
			}
			else
				p.Dt = 0;

			CalcZ = miCollisionZ;
			miZFrac = 0;

			// Falling into water stops the mover dead.
			if (miCollisionIndex == -1 && (Flags & 1) != 0
				&& (CentreFloor & 0xFF) + (CalcRadius & 0xFF) >= CalcZ && p.Vz < 0)
			{
				fStop();
				p.Contact = UWMotionTables.ContactWater;
				mOWorld.PlaySoundAt(5, p.X >> 5, p.Y >> 5, I16(liMass - 600) / 50);

				return;
			}

			int liVolume = (Abs16(p.Vz) / 10) + (I16(liMass - 600) / 50) - 40;

			mOWorld.PlaySoundAt(0x0F, p.X >> 5, p.Y >> 5, liVolume);

			int liResult = CollideObjects(miCollisionIndex, CalcIndex);

			if ((liResult & 0x18) != 0)
			{
				if ((liResult & 0x10) != 0)
					fStop();
				else
					miDone = miWhole + 1;

				return;
			}

			if ((liResult & 4) != 0)
			{
				// BOUNCE: the vertical speed turns round at e / 15, the horizontal loses (15 - e) / 30.
				bool lbFalling = p.Vz <= 0;

				p.Vz = -(p.Vz / 15);
				p.Impact = Abs16(I16(p.Vz * (15 - liE)));
				p.Vz = I16(p.Vz * liE);

				if (liE != 0)
					p.Speed = I16(p.Speed - (I16((15 - liE) * p.Speed) / 30));
				else
					p.Speed = 0;

				if (lbFalling && p.Vz < 0x8D)
				{
					// LANDED: the reversal stop.
					p.Vz = 0;
					p.Gravity = 0;

					if (miCollisionIndex != -1)
					{
						if (fEntry(Records[miCollisionIndex].Body.ItemId).Is3DModel)
							p.Contact = UWMotionTables.ContactGround;
						else if (Mover.IsCreature)
							p.Contact = UWMotionTables.ContactGround;
						else
							fHop();
					}
					else if ((CentreFloor & 0xFF) + (CalcRadius & 0xFF) >= CalcZ)
						p.Contact = 1 << (Flags & 3);
					else if (!Mover.IsCreature)
						fHop();
					else
						p.Contact = (AllFlags & 0x10) != 0 ? UWMotionTables.ContactWater
							: (AllFlags & 0x20) != 0 ? UWMotionTables.ContactLava : UWMotionTables.ContactGround;
				}
			}
			else if (miCollisionIndex != -1)
			{
				// Not solid, or a trigger's result: the record goes, the last one takes its place.
				Count--;
				Records[miCollisionIndex] = Records[Count];
			}

			fRetime(false);
		}

		// ------------------------------------------------- CollideObjects_seg029_29EE_173 (line 99682)

		/// <summary>
		/// The mover meets a record (or the floor and ceiling, piEntry -1): the pair is handled once
		/// per scan, the struck object may use itself on the mover or fire as a trigger, a missile
		/// uses itself on what it struck, and a solid object takes the mover's momentum. Returns 2
		/// nothing, 4 an obstacle, 8 end the step, 0x10 stop the mover.
		/// </summary>
		public int CollideObjects(int piEntry, int piMoverIndex)
		{
			if (piEntry != -1)
			{
				if ((Records[piEntry].Flags & 0x20) != 0)
					return 2;

				Records[piEntry].Flags |= 0x20;
			}

			int liMoverTileX = CalcX >> 3;
			int liMoverTileY = CalcY >> 3;
			bool lbOther = piEntry != -1;
			UWMotionBody lOOther = lbOther ? Records[piEntry].Body : default;
			bool lbOtherSolid = true;

			if (lbOther)
			{
				HitTileX = (liMoverTileX + Records[piEntry].Dx) & 0x3F;
				HitTileY = (liMoverTileY + Records[piEntry].Dy) & 0x3F;
				lbOtherSolid = fEntry(lOOther.ItemId).IsSolid;

				// Mobile against mobile: a non-creature mover collides with a mobile only once.
				if (Mover.IsMobile && lOOther.IsMobile)
				{
					if (!Mover.IsCreature && Mover.CollidedWithMobile)
						return 2;

					Mover.CollidedWithMobile = true;
				}

				if (fEntry(lOOther.ItemId).UsedWhenThrown)
					mOWorld.UseOnMover(lOOther.Index, piMoverIndex, HitTileX, HitTileY);
				else if (((lOOther.ItemId >> 6) & 7) == 6)
					return mOWorld.TriggerMove(piMoverIndex, lOOther.Index, HitTileX, HitTileY);
			}

			if (!lbOtherSolid)
				return 2;

			if (fEntry(Mover.ItemId).UsedWhenThrown && lbOther
				&& mOWorld.MissileHits(piMoverIndex, lOOther.Index, liMoverTileX, liMoverTileY))
				return 0x10;

			return fTransferMomentum(lbOther, lOOther);
		}

		/// <summary>seg029_29EE_3: the struck object gets the mover's heading, speed 0xEB and a
		/// share of its vertical speed by the masses - in 16 bits, so a heavy mover falling fast
		/// onto a light object can send it UP (motion-core.md 9, item 8). Always 4: an obstacle.</summary>
		private int fTransferMomentum(bool pbOther, UWMotionBody pOOther)
		{
			if (!pbOther)
				return 4;

			int liOtherMass = fEntry(pOOther.ItemId).MassTenthStones & 0xFFF;

			if (liOtherMass != 0)
			{
				int liK = I16(mOParams.Mass << 6) / liOtherMass;

				if (liK > 0x80)
					liK = 0x80;

				int liVz = I16(mOParams.Vz * liK) / 64;

				mOWorld.PushObject(pOOther.Index, mOParams.Heading & 0xFFFF, 0xEB, liVz, HitTileX, HitTileY);
			}

			return 4;
		}

		// ------------------------------------------------- SetCollisionTarget_seg030_2B26_10C (line 102861)

		/// <summary>Where the vertical motion stops: rising, the first record above the head or the
		/// ceiling (a corner floor above z + radius instead); at rest, the floor within step or
		/// the highest solid record top in reach; falling, the highest floor sample or the highest
		/// record top below the feet.</summary>
		public void SetCollisionTarget()
		{
			UWMotionParams p = mOParams;

			mbTargetFromFloor = true;
			SortCollisions();
			miCollisionIndex = -1;
			miLowestAbove = 0x7F;

			if (p.Vz > 0)
			{
				int liK = (sbyte)First + Overlap;

				if (Count != 0 && liK < Count && (sbyte)First >= 0)
				{
					miCollisionZ = I16(Records[liK].Bottom - p.Height);
					miCollisionIndex = liK;
				}
				else
					miCollisionZ = 0x80 - p.Height;

				mbTargetFromFloor = false;

				if (CalcZ + p.Radius < MaxFloor)
				{
					miCollisionZ = MaxFloor;
					mbTargetFromFloor = true;
				}
			}
			else if (p.Vz == 0)
			{
				miCollisionZ = CalcZ + p.Step >= MaxFloor ? MaxFloor : CentreFloor;

				bool lbBelowOnly = Overlap == 0 && (sbyte)First > 0 && (First & 0xFF) <= Count;

				for (int liAt = 0; liAt < Count; liAt++)
				{
					if (!fEntry(Records[liAt].Body.ItemId).IsSolid)
						continue;

					if (liAt >= (sbyte)First)
					{
						if (Records[liAt].Bottom - 1 < miLowestAbove)
							miLowestAbove = Records[liAt].Bottom - 1;

						if (liAt < First + Overlap && Records[liAt].Top > miCollisionZ
							&& Records[liAt].Top < 0x80 - p.Height)
						{
							miCollisionIndex = liAt;
							miCollisionZ = Records[liAt].Top;
						}
					}
					else if (lbBelowOnly && Records[liAt].Top >= miCollisionZ)
					{
						// The original's three tests here all end up taking the record.
						miCollisionZ = Records[liAt].Top;
						miCollisionIndex = liAt;
					}
				}
			}
			else
			{
				miCollisionZ = MaxFloor;

				if ((sbyte)First > 0 && (First & 0xFF) <= Count && Records[First - 1].Top > miCollisionZ)
				{
					miCollisionZ = Records[First - 1].Top;
					miCollisionIndex = First - 1;
				}

				mbTargetFromFloor = false;
			}

			if (miCollisionIndex != -1)
				miCollisionItemId = Records[miCollisionIndex].Body.ItemId;
		}

		// ------------------------------------------------- GetCollisionHeightState_seg030_2B26_1259 (line 105641)

		/// <summary>The record's top for the step rule; with index -1 the original reads the scan
		/// box bytes before the records (the stale path of a land NPC over water).</summary>
		private int fRecordTop(int piIndex)
		{
			return piIndex == -1 ? miXMin & 0xFF : Records[piIndex].Top;
		}

		private int fRecordFlags(int piIndex)
		{
			return piIndex == -1 ? miCy & 0xFF : Records[piIndex].Flags;
		}

		/// <summary>
		/// The terrain and the objects at the current cell as result flags: 0x3 terrain, 4 on the
		/// floor, 8/0x10/0x20/0x40 the floor kind, 0x80 standing on an object, 0x100 a wall (a floor
		/// too high to step onto), 0x200 solid, 0x400 an object in the way, 0x800 a drop-off, 0x1000
		/// no support, 0x2000 a slope, 0x4000 stop, 0x8000 end the step. A mover whose step height
		/// reaches the target takes that height at once.
		/// </summary>
		public int GetCollisionHeightState()
		{
			UWMotionParams p = mOParams;
			UWMotionHandler h = mOHandler;
			bool lbOk = false;
			bool lbMayStandOnObject = (h.TerrainMask & 0x80) == 0;

			mbLanded = false;
			ProcessMotionTileHeights(p.Step);
			ScanForCollisions(false, false);
			SetCollisionTarget();

			int liFlags = Flags | AllFlags;
			bool lbMayFollowFloor = (liFlags & h.TerrainMask) == 0;

			if ((Count & 0xFF) != 0)
			{
				for (int liAt = (sbyte)First; liAt < (sbyte)First + (Overlap & 0xFF); liAt++)
				{
					int liResult = CollideObjects(liAt, CalcIndex);

					if ((liResult & 4) != 0)
						liFlags |= 0x400;

					if ((liResult & 0x18) != 0)
						return liFlags | ((liResult & 0x10) != 0 ? 0x4000 : 0x8000);
				}
			}

			if (CalcZ == miCollisionZ)
			{
				// Already at the target.
				if (miCollisionIndex != -1 && fEntry(miCollisionItemId).Is3DModel && lbMayStandOnObject)
					liFlags = (liFlags | 0x80) & ~4;
			}
			else
			{
				if (miCollisionIndex == -1 && lbMayFollowFloor)
				{
					lbOk = Abs16(CalcZ - miCollisionZ) <= p.Step
						|| (p.Vz == 0 && (AllFlags & 0x800) == 0 && (Flags & 4) != 0);
				}
				else if (lbMayStandOnObject)
				{
					lbOk = fEntry(miCollisionItemId).Is3DModel;

					if (lbOk)
					{
						lbOk = Abs16(CalcZ - fRecordTop(miCollisionIndex)) <= p.Step;

						if (lbOk)
							liFlags |= 0x80;
					}
				}

				lbOk = lbOk && mbTargetFromFloor;

				if (lbOk && miCollisionZ >= miLowestAbove)
					liFlags &= ~0x100;
				else if (!lbOk && MaxFloor > CalcZ)
					liFlags |= 0x100;

				if (lbOk && miCollisionZ + p.Height > 0x7F)
				{
					// The head through the ceiling.
					lbOk = false;
					mbLanded = true;
					liFlags |= 0x200;
				}
				else if (lbOk && miCollisionIndex == -1 && miCollisionZ + p.Height > miLowestAbove)
				{
					// No headroom under a solid object: reported as an object hit.
					lbOk = false;
					mbLanded = true;
					liFlags |= 0x400;
				}

				if (lbOk && (liFlags & 0x400) == 0 && miCollisionIndex != -1
					&& (!fEntry(miCollisionItemId).Is3DModel || !lbMayStandOnObject))
					lbOk = false;

				if (lbOk)
				{
					// The step: take the target height now, up or down.
					mbLanded = true;
					CalcZ = miCollisionZ;

					if (Abs16(miCollisionZ - CentreFloor) <= p.Radius)
						liFlags |= 4;
					else
						liFlags &= ~4;
				}
			}

			if (mbLanded && lbOk)
			{
				// The second pass after a snap - over the records from 0, not from First (the
				// original's bug, motion-core.md 9 question 12; never reached by objects).
				SortCollisions();
				liFlags &= ~0x400;

				for (int liAt = 0; liAt < (Overlap & 0xFF); liAt++)
				{
					if ((CollideObjects(liAt, CalcIndex) & 4) != 0)
						liFlags |= 0x400;
				}
			}

			if ((liFlags & 0x80) != 0 && lbMayStandOnObject
				&& ((fRecordFlags(miCollisionIndex) & 0x3F & 0x10) != 0 || (Flags & 4) != 0))
				liFlags &= ~0x800;

			if ((AllFlags & 0x100) != 0 && lbMayFollowFloor && p.Vz == 0 && CalcZ + p.Step >= MaxFloor)
			{
				// The second step: the highest corner is in reach from the new height.
				liFlags &= ~0x100;
				CalcZ = MaxFloor;

				if (CalcZ == CentreFloor)
					liFlags |= 4;
				else
					liFlags &= ~4;
			}

			if ((liFlags & 0xFC) == 0)
				liFlags |= 0x1000;

			if ((liFlags & 0x80) == 0 && CalcZ - p.Radius > MaxFloor)
				liFlags |= 0x1000;

			return liFlags;
		}

		// ------------------------------------------------- CheckIfItemFitsInTile_seg026_1008 (line 93903)

		/// <summary>
		/// Whether an object of the given kind fits at a spot - READ in full 2026-10-06 for the
		/// player's easy step; the summoning, the dropping and the putting down call it too.
		/// The object's box (COMOBJ radius and height) at the eighth position and coarse z:
		/// <list type="number">
		/// <item>a top above 127 does not fit, unless the height is 0x80;</item>
		/// <item>the terrain (ProcessMotionTileHeights with piStep as the step allowance): a wall
		/// or a floor more than piStep above (0x300) does not fit;</item>
		/// <item>the height to stand at: the highest corner floor if z plus piStep reaches it,
		/// else the centre floor; the state: the centre's floor kind (1 &lt;&lt; terrain) if z
		/// lies at most max(radius, piStep) above the centre floor, else airborne (0x10);</item>
		/// <item>the solid objects around (ScanForCollisions, SortCollisions): any that reaches
		/// into the box's z range does not fit; of those below, the highest top above the floor
		/// becomes the height to stand at - and that object must be one to stand on (COMOBJ byte
		/// 3 bit 1, decoded as Is3DModel: tables, chests, barrels), else no fit; standing on it
		/// is ground (1);</item>
		/// <item>without pbMayDrop, a drop-off (0x800) whose ground lies more than piStep below z
		/// does not fit - the ledge rule of the easy step.</item>
		/// </list>
		/// Mover must be set before (the scan asks whether the mover is a creature). The calc
		/// array is this core's own, so the caller's step must be finished.
		/// </summary>
		public bool CheckIfItemFitsInTile(int piItemId, int piIndex, int piX8, int piY8, int piZ, bool pbMayDrop, int piStep,
			out int piStandZ, out int piState)
		{
			UWCommonObjectProperties.Entry lOEntry = fEntry(piItemId);

			piStandZ = 0;
			piState = 0;

			SetCalc(piX8, piY8, piZ, lOEntry.Radius & 7, lOEntry.Height, piIndex);

			// Labels 1077 to 108A: the top under the ceiling of the world.
			if (lOEntry.Height != 0x80 && lOEntry.Height + piZ > 0x7F)
				return false;

			ProcessMotionTileHeights(piStep);

			if (((Flags | AllFlags) & 0x300) != 0)
				return false;

			// Labels 10B1 to 10D4: the highest corner if it can be stepped onto, else the centre.
			int liCentre = CentreFloor & 0xFF;
			int liHighest = MaxFloor & 0xFF;

			piStandZ = piZ + piStep >= liHighest ? liHighest : liCentre;

			// Labels 10D9 to 1122: on the ground within the larger of radius and allowance.
			int liReach = System.Math.Max(CalcRadius & 0xFF, piStep & 0xFF);

			piState = piZ <= liCentre + liReach ? 1 << (Flags & 3) : UWMotionTables.ContactAirborne;

			// Labels 1122 to 1139: the tight scan only for a static object on the ground.
			ScanForCollisions(piState != UWMotionTables.ContactAirborne && piIndex >= 0x100, true);

			if ((Count & 0xFF) != 0)
			{
				SortCollisions();

				if ((Overlap & 0xFF) != 0)
					return false;

				int liStoodOn = -1;

				for (int liAt = 0; liAt < (sbyte)First; liAt++)
				{
					if ((Records[liAt].Top & 0xFF) > piStandZ)
					{
						liStoodOn = liAt;
						piStandZ = Records[liAt].Top & 0xFF;
					}
				}

				if (liStoodOn > -1)
				{
					if (!fEntry(Records[liStoodOn].Body.ItemId).Is3DModel)
						return false;

					piState = UWMotionTables.ContactGround;
				}
			}

			// Labels 11ED to 121A: the ledge.
			if (!pbMayDrop && ((Flags | AllFlags) & 0x800) != 0 && piZ - piStep > piStandZ)
				return false;

			return true;
		}

		// ------------------------------------------------- ProcessMotionTileHeights_seg026_379 (line 91828)

		/// <summary>The attr word of a tile: type, floor height and terrain as the original packs
		/// them (tile byte 0 plus the TERRAIN.DAT floor value shifted); off the map solid.</summary>
		private int fAttr(int piTileX, int piTileY)
		{
			int liType;
			int liFloor;
			int liTerrain;

			if (!mOWorld.TryGetTile(piTileX, piTileY, out liType, out liFloor, out liTerrain))
				return 0;

			return (liType & 0xF) | ((liFloor & 0xF) << 4) | ((liTerrain & 3) << 8);
		}

		private int fCornerAttr(int piT)
		{
			if (miAttr[piT] == NotLoaded)
				miAttr[piT] = fAttr(miTileX + ((piT % 3) - 1), miTileY + ((piT / 3) - 1));

			return miAttr[piT];
		}

		/// <summary>
		/// Samples the terrain at the mover's centre and at the four corners of its box (radius
		/// in eighths) into Flags, AllFlags, CentreFloor and MaxFloor: per sample the floor height
		/// of its tile type (solid 0x80, the diagonals' line itself solid, the slopes rising per
		/// eighth), then a wall when it lies more than piStep above the mover, a drop-off when more
		/// than piStep below, the floor kind otherwise. A corner inside a diagonal tile whose box
		/// edge leaves through the closed side counts as solid.
		/// </summary>
		public void ProcessMotionTileHeights(int piStep)
		{
			for (int liAt = 0; liAt < 9; liAt++)
				miAttr[liAt] = NotLoaded;

			miTileX = CalcX >> 3;
			miTileY = CalcY >> 3;

			int liFx = CalcX & 7;
			int liFy = CalcY & 7;
			int liT = 4;

			mOCorners[4] = new Corner { T = 4, X = liFx, Y = liFy };
			miAttr[4] = fAttr(miTileX, miTileY);

			fSampleCentre(piStep);

			AllFlags = Flags;
			MaxFloor = CentreFloor;

			if (CalcRadius == 0)
				return;

			int liR = CalcRadius;

			liFy -= liR;
			while (liFy < 0) { liT -= 3; liFy += 8; }
			liFx -= liR;
			while (liFx < 0) { liT -= 1; liFx += 8; }
			mOCorners[0] = new Corner { T = liT, X = liFx, Y = liFy };
			liFx += 2 * liR;
			while (liFx > 7) { liT += 1; liFx -= 8; }
			mOCorners[1] = new Corner { T = liT, X = liFx, Y = liFy };
			liFy += 2 * liR;
			while (liFy > 7) { liT += 3; liFy -= 8; }
			mOCorners[2] = new Corner { T = liT, X = liFx, Y = liFy };
			liFx -= 2 * liR;
			while (liFx < 0) { liT -= 1; liFx += 8; }
			mOCorners[3] = new Corner { T = liT, X = liFx, Y = liFy };

			for (int liAt = 0; liAt < 4; liAt++)
				fCornerAttr(mOCorners[liAt].T);

			mbNoDiagonalEdge = true;

			for (int liAt = 0; liAt < 4; liAt++)
			{
				if (fSampleCorner(liAt, piStep))
					continue;

				if ((mOCorners[liAt].Flags & 0x300) != 0)
					continue;

				int liType = miAttr[mOCorners[liAt].T] & 0xF;
				int liTraverse = liType < UWMotionTables.TileTraverseFlags.Length ? UWMotionTables.TileTraverseFlags[liType] : 0;

				for (int liK = 0; liK < 2; liK++)
				{
					int liJ = (liAt + (2 * liK) + 1) & 3;

					if (mOCorners[liJ].T != mOCorners[liAt].T && (liTraverse & UWMotionTables.EdgeBits[liAt + liK]) != 0)
					{
						mOCorners[liAt].Flags = 0x200;
						MaxFloor = 0x80;

						break;
					}
				}

				mbNoDiagonalEdge = false;
			}

			AllFlags |= mOCorners[0].Flags | mOCorners[1].Flags | mOCorners[2].Flags | mOCorners[3].Flags;
		}

		/// <summary>seg026_2C4: the centre sample into Flags and CentreFloor.</summary>
		private void fSampleCentre(int piStep)
		{
			bool lbDiagonal;
			int liTerrain = (miAttr[4] & 0x300) >> 8;

			Flags = liTerrain;
			CentreFloor = fFloorAt(4, out lbDiagonal);

			if (CentreFloor == 0x80)
				Flags |= 0x200;
			else if (CalcZ + piStep < CentreFloor)
				Flags |= 0x100;
			else if (CalcZ - piStep > CentreFloor)
				Flags |= 0x800;
			else
				Flags |= 4 | (8 << liTerrain);

			if ((miAttr[4] & 0xF) >= 6)
				Flags |= 0x2000;
		}

		/// <summary>seg026_20D: a corner sample; true when the corner is NOT in a diagonal tile.</summary>
		private bool fSampleCorner(int piCorner, int piStep)
		{
			bool lbDiagonal;
			int liH = fFloorAt(piCorner, out lbDiagonal);
			int liFlags;

			if (liH == 0x80)
				liFlags = 0x200;
			else if (CalcZ + piStep < liH)
				liFlags = 0x100;
			else if (CalcZ - piStep > liH)
				liFlags = 0x800;
			else
				liFlags = 8 << ((miAttr[mOCorners[piCorner].T] & 0x300) >> 8);

			mOCorners[piCorner].Flags = liFlags;

			if ((MaxFloor & 0xFF) < (liH & 0xFF))
				MaxFloor = liH;

			return !lbDiagonal;
		}

		/// <summary>seg026_4: the floor in coarse z at a sample, by the tile type; 0x80 solid.</summary>
		private int fFloorAt(int piSample, out bool pbDiagonal)
		{
			int liAttr = miAttr[mOCorners[piSample].T];
			int liH = (liAttr & 0xF0) >> 1;
			int liX = mOCorners[piSample].X;
			int liY = mOCorners[piSample].Y;

			pbDiagonal = false;

			switch (liAttr & 0xF)
			{
				case 0: liH = 0x80; break;
				case 1: break;
				case 2: if (liY >= liX) liH = 0x80; pbDiagonal = true; break;
				case 3: if (liX + liY >= 7) liH = 0x80; pbDiagonal = true; break;
				case 4: if (liX + liY <= 7) liH = 0x80; pbDiagonal = true; break;
				case 5: if (liY <= liX) liH = 0x80; pbDiagonal = true; break;
				case 6: liH += liY & 7; break;
				case 7: liH += 7 - (liY & 7); break;
				case 8: liH += liX & 7; break;
				case 9: liH += 7 - (liX & 7); break;
				default: break;
			}

			return liH & 0xFF;
		}

		// ------------------------------------------------- seg026_7F6: the wall octant (line 92461)

		private void fWallOctant()
		{
			int liNb = 0;
			int liNo = 0;
			int liBx = 0;
			int liBy = 0;
			int liOx = 0;
			int liOy = 0;

			for (int liAt = 0; liAt < 4; liAt++)
			{
				if ((mOCorners[liAt].Flags & 0xF8) == 0)
				{
					liOx += UWMotionTables.CornerSign[liAt];
					liOy += UWMotionTables.CornerSign[(liAt + 3) & 3];
					liNo++;
				}

				if ((mOCorners[liAt].Flags & 0x300) != 0)
				{
					liBx += UWMotionTables.CornerSign[liAt];
					liBy += UWMotionTables.CornerSign[(liAt + 3) & 3];
					liNb++;
				}
			}

			if (liNb == 0)
				WallOctant = 9;
			else
			{
				WallOctant = UWMotionTables.OctantBySum[((liBx / liNb) * 3) + (liBy / liNb) + 4];

				if (liNb == 1 && (WallOctant % 2) != 0 && mbNoDiagonalEdge)
				{
					// One blocked corner, a diagonal octant: refined by the heading and the corner.
					int liRel = (9 - WallOctant) & 7;
					int liHq = (CalcHeading & 0xFFFF) >> 13;

					switch ((liHq - liRel) & 7)
					{
						case 0:
						case 1:
							WallOctant = 9;
							break;
						case 2:
						case 3:
							WallOctant = (WallOctant + 1) & 7;
							break;
						case 4:
						case 5:
						{
							int liK = (WallOctant - 1) >> 1;
							int liA;
							int liB;

							WallOctant--;

							switch (liK)
							{
								case 0: liA = mOCorners[liK].X; liB = mOCorners[liK].Y; break;
								case 1: liA = mOCorners[liK].X; liB = 8 - mOCorners[liK].Y; break;
								case 2: liA = mOCorners[liK].Y; liB = mOCorners[liK].X; break;
								default: liA = 8 - mOCorners[liK].X; liB = mOCorners[liK].Y; break;
							}

							if ((liA & 0xFF) < (liB & 0xFF))
								WallOctant = (WallOctant + 2) & 7;

							if (liA == liB)
								WallOctant++;

							break;
						}
						default:
							WallOctant = (WallOctant + 7) & 7;
							break;
					}
				}
			}

			if (liNo == 1 || liNo == 2)
				SecondOctant = UWMotionTables.OctantBySum[((liOx / -liNo) * 3) + (liOy / -liNo) + 4];
			else
				SecondOctant = 9;
		}

		// ------------------------------------------------- ScanForCollisions_seg026_BF6 (line 93164)

		/// <summary>
		/// Collects the objects whose box touches the mover's into the records: the tiles the
		/// mover's box reaches into, their object lists in order, up to nine records (later objects
		/// are dropped), 64 objects in one tile end the whole scan. Skipped: the mover itself,
		/// switches for a creature mover, static objects of height 0, mobile non-creatures that
		/// have collided with a mobile already; with pbSolidOnly everything that is not solid.
		/// </summary>
		public void ScanForCollisions(bool pbTight, bool pbSolidOnly)
		{
			bool lbMoverIsCreature = CalcIndex != 0 && Mover.IsCreature;

			miTileX = CalcX >> 3;
			miTileY = CalcY >> 3;
			Count = 0;
			miCx = CalcX & 7;
			miCy = CalcY & 7;

			int liR;

			if (pbTight && CalcHeight == 0)
				liR = 0;
			else
			{
				liR = CalcRadius;

				if (lbMoverIsCreature && liR > 0)
					liR--;
			}

			miXMax = miCx + liR;
			miYMax = miCy + liR;
			miXMin = miCx - liR;
			miYMin = miCy - liR;

			int liTx0 = (miXMin - 11) / 8;
			int liTy0 = (miYMin - 11) / 8;
			int liTx1 = (miXMax + 4) / 8;
			int liTy1 = (miYMax + 4) / 8;

			for (int liDx = liTx0; liDx <= liTx1; liDx++)
			{
				for (int liDy = liTy0; liDy <= liTy1; liDy++)
				{
					mOBodies.Clear();
					mOWorld.GetBodiesInTile(miTileX + liDx, miTileY + liDy, mOBodies);

					int liN = 0;

					for (; liN < mOBodies.Count && liN < 0x40; liN++)
					{
						UWMotionBody lOBody = mOBodies[liN];

						if (lOBody.Index == CalcIndex)
							continue;

						UWCommonObjectProperties.Entry lOEntry = fEntry(lOBody.ItemId);

						if (lbMoverIsCreature && lOEntry.IsSwitch)
							continue;

						if (lOEntry.Height == 0 && !lOBody.IsMobile)
							continue;

						if (lOBody.IsMobile && !lOBody.IsCreature && lOBody.CollidedWithMobile)
							continue;

						if (pbSolidOnly && !lOEntry.IsSolid)
							continue;

						fCreateRecord(lOBody, lOEntry, liDx, liDy, lbMoverIsCreature);
					}

					if (liN == 0x40)
						return;
				}
			}
		}

		/// <summary>CreateColllisionRecord_seg026_A60 (line 92924).</summary>
		private void fCreateRecord(UWMotionBody pOBody, UWCommonObjectProperties.Entry pOEntry, int piDx, int piDy, bool pbShrinkNpc)
		{
			if ((Count & 0xFF) > 8)
				return;

			int liX0;
			int liY0;
			int liX1;
			int liY1;

			if ((pOEntry.Radius & 7) == 4)
			{
				// Radius 4: the whole tile.
				liX0 = piDx << 3;
				liX1 = liX0 + 7;
				liY0 = piDy << 3;
				liY1 = liY0 + 7;
			}
			else
			{
				liX0 = (piDx << 3) + pOBody.XPos;
				liY0 = (piDy << 3) + pOBody.YPos;

				int liR = pOEntry.Radius & 7;

				if (pOBody.IsCreature && liR > 0 && pbShrinkNpc)
					liR--;

				liX1 = liX0 + liR;
				liY1 = liY0 + liR;
				liX0 -= liR;
				liY0 -= liR;
			}

			if (liX1 < miXMin || liX0 > miXMax || liY1 < miYMin || liY0 > miYMax)
				return;

			Record lORecord = new Record
			{
				Bottom = pOBody.ZPos & 0x7F,
				Flags = 9,
				Dx = piDx,
				Dy = piDy,
				Body = pOBody
			};

			lORecord.Top = (lORecord.Bottom + pOEntry.Height) & 0xFF;

			if (pOEntry.Height == 0)
				lORecord.Top = (lORecord.Top + 1) & 0xFF;

			if (miCx >= liX0 && miCx <= liX1 && miCy >= liY0 && miCy <= liY1)
				lORecord.Flags |= 0x10;

			Records[Count++] = lORecord;
		}

		// ------------------------------------------------- SortCollisions_seg026_EBF (line 93651)

		/// <summary>The records below the feet by top, lowest first (First = the first one whose top
		/// is above the feet), the rest by bottom; Overlap = how many from First on reach into the
		/// mover's z range.</summary>
		public void SortCollisions()
		{
			int liFlat = CalcHeight == 0 ? 1 : 0;
			int liFirst = 0;

			while ((Count & 0xFF) > (liFirst & 0xFF))
			{
				for (int liJ = Count - 2; liJ >= liFirst; liJ--)
				{
					if ((Records[liJ].Top & 0xFF) > (Records[liJ + 1].Top & 0xFF))
						fSwap(liJ);
				}

				if (I16(Records[liFirst].Top) > CalcZ)
					break;

				liFirst++;
			}

			for (int liAt = liFirst; (Count & 0xFF) > (liAt & 0xFF); liAt++)
			{
				for (int liJ = Count - 2; liJ >= liAt; liJ--)
				{
					if ((Records[liJ].Bottom & 0xFF) > (Records[liJ + 1].Bottom & 0xFF))
						fSwap(liJ);
				}
			}

			First = liFirst;
			Overlap = 0;

			while (liFirst + Overlap < Count && CalcZ + CalcHeight + liFlat > Records[liFirst + Overlap].Bottom)
				Overlap++;
		}

		private void fSwap(int piJ)
		{
			Record lOTemp = Records[piJ];

			Records[piJ] = Records[piJ + 1];
			Records[piJ + 1] = lOTemp;
		}
	}
}
