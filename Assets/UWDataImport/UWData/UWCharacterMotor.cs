using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE CHARACTER MOTOR (Docs/MOTOR-PLAN.md; M1, 2026-10-27): the player's modern motion, of its
	/// own and engine-free. Two attempts on the original core were not satisfactory - patching the
	/// core's slide, and steering in front of it with walls as half-planes - see the Todo's row
	/// "True modern movement". This motor shares nothing with the core's eighth cells: the player
	/// is a ROUND body (a circle in plan, per user), the walls are exact segments, and a step is a
	/// collide and slide.
	///
	/// THE GEOMETRY (fBuild): only the edges between rock and free ground are walls - the sides
	/// of solid tiles and of floors too high to step onto that face free ground, the diagonals, and
	/// the closed sides of diagonal tiles that face free ground. Walls on one line with one facing
	/// are merged into one segment, so sliding along a wall of many tiles meets no seam (a seam's
	/// corner would deflect the slide by a hair, the "ghost collision"). A segment's ends are
	/// corners the circle can meet, so it rounds them.
	///
	/// THE STEP (fStretch): the circle travels the step's displacement; at the first contact - the
	/// exact time of impact against a segment, offset by the radius, or against a corner, a circle
	/// of the radius - it stops there, keeps the part of the rest along the wall and travels on, up
	/// to MaxPasses times; a part that runs into the wall met before is a corner and stops. A body
	/// found too close to a wall (a start, rounding) is pushed out first.
	///
	/// HEIGHTS (M2, 2026-10-27): a frame (Advance) moves the body by its velocity in sub-steps; off
	/// the ground the feet follow the arc exactly - the core's vertical speed is Vz / 256 fine z a
	/// tick and its gravity is per tick (the original's constants: a jump takes off with JumpSpeed
	/// less the precision mode's JumpEulerOffset, a fall has gravity -4), so a jump's height and
	/// reach are the precision mode's within a percent. The GROUND is the highest floor the body
	/// stands over within the step height - on a slope the slope's height at the centre, unless
	/// another floor stands more than the radius (two coarse z) above it, as the core snaps a player on a slope (UWMotionCore.fSlopeZ).
	/// On the ground the feet follow it up and down by up to a level; a deeper drop makes the body
	/// fall, unless HoldAtLedges (the caller's rule: below three tenths of the forward speed) turns
	/// the edge into a wall for the CENTRE, so a slow player stands at the edge half over it. In the
	/// air a floor reached by the feet is landed on (LandedVz keeps the speed for the fall's damage),
	/// rising as falling - one within the step height above the feet too, as the core rises to it -,
	/// and the head stops at the ceiling (coarse 0x80 less the height). Heights are compared in fine z:
	/// a floor more than the step height above the feet is a wall.
	///
	/// OBJECTS (M3, 2026-10-27): the bodies of the tiles around, as the core's scan takes them
	/// (ScanForCollisions, fCreateRecord): a box of the COMOBJ radius in eighths around the object's
	/// eighth (radius 4 the whole tile, a creature's one eighth smaller), from its z up by its
	/// height; switches and things of height 0 are passed. The player meets them with a radius of
	/// one eighth (the core shrinks a creature mover's box for objects), so ObjectPad keeps the
	/// centre that far from a box. A SOLID box is a wall where its height range overlaps the body's,
	/// unless its top is at or below the feet, or it is a 3D model (a barrel, a bridge) whose top
	/// is within the step height - those carry the body like a floor. A solid object met is pushed
	/// (PushObject, as fTransferMomentum); the head stops under a solid object above. A body that
	/// overlaps a move trigger (or another object of class 6) fires it, one that overlaps an object
	/// used when struck has it used on the body - looked at when the centre enters a new eighth
	/// cell, as the core scans, once a frame, after the frame. Doors need no rule of their own: a closed door's box stands on the floor, an open
	/// one is raised by 0x18 (UWMotionHost's open rise) above the head.
	///
	/// Units: tiles (x east, y north), z in the params' fine z (a floor nibble is 0x40), the step
	/// height and the body's height in coarse z (8 is one floor level, the core's p.Step), time in
	/// PIT ticks, the vertical speed and gravity in the core's units.
	/// </summary>
	public sealed class UWCharacterMotor
	{
		/// <summary>The body's radius in tiles: the core's box reaches two eighths from the centre,
		/// so doorways and corridors fit as before.</summary>
		public const double DefaultRadius = 0.25;

		/// <summary>The gap kept to every wall beyond the radius, in tiles.</summary>
		public const double Skin = 1.0 / 512.0;

		/// <summary>How many contacts one step resolves.</summary>
		public const int MaxPasses = 4;

		/// <summary>The longest stretch moved in one go, in tiles; a longer step is cut.</summary>
		public const double MaxStretch = 0.25;

		/// <summary>A motion whose part into a wall is under this share of its length grazes the
		/// wall and passes: a body sliding along a wall or round a corner would else meet it again
		/// by a rounding error, and the corner rule would stop it (the sweep's halts, 2026-10-27).</summary>
		public const double Grazing = 1.0e-6;

		/// <summary>The player's step height in coarse z (the core's p.Step).</summary>
		public const int DefaultStep = 8;

		/// <summary>The player's height in coarse z (the core's default for the adventurer).</summary>
		public const int DefaultHeight = 23;

		/// <summary>The ceiling of the world in coarse z (SetCollisionTarget: 0x80 less the height).</summary>
		public const int CeilingZ = 0x80;

		/// <summary>The gravity of a fall, per tick (the core's -4 when the ground drops away).</summary>
		public const int FallGravity = -4;

		/// <summary>The fine z a sub-step moves up or down at most, so a landing is met within it.</summary>
		public const double MaxRise = 8.0;

		/// <summary>Rising, a floor less than this many fine z above the feet is a wall (the core's
		/// radius of two coarse z: its rising target takes a floor only beyond it).</summary>
		public const int RisingBand = 16;

		/// <summary>On a slope the slope's height at the centre holds unless another floor stands more
		/// than this many fine z above it (the core's radius of two coarse z).</summary>
		public const int SlopeHold = 16;

		/// <summary>How far the centre keeps from an object's box, in tiles: the core's mover box for
		/// objects reaches one eighth from a creature's centre (ScanForCollisions shrinks it by one).</summary>
		public const double ObjectPad = 1.0 / 8.0;

		/// <summary>The class of the triggers in the item id (bits 6-8): a body meeting one fires it.</summary>
		public const int TriggerClass = 6;

		/// <summary>The speed a solid object met is pushed with (fTransferMomentum).</summary>
		public const int PushSpeed = 0xEB;

		private struct Segment
		{
			public double Ax;

			public double Ay;

			public double Bx;

			public double By;

			/// <summary>The unit normal towards the free side.</summary>
			public double Nx;

			public double Ny;

			/// <summary>How far the centre keeps from it: the radius for rock, nothing for a ledge
			/// (the centre stops at the edge), ObjectPad for an object.</summary>
			public double Pad;

			/// <summary>The object whose box side it is (-1: rock or a ledge).</summary>
			public int Owner;
		}

		/// <summary>An object as the motor meets it: its box in tiles, its height range in coarse z.</summary>
		private struct Box
		{
			public double X0;

			public double Y0;

			public double X1;

			public double Y1;

			public int Bottom;

			public int Top;

			public bool Solid;

			public bool Model;

			public bool Trigger;

			public bool UsedOnMover;

			public int Index;

			public int TileX;

			public int TileY;

			public int Mass;
		}

		private readonly IUWMotionWorld mOWorld;

		private readonly UWCommonObjectProperties mOProperties;

		private readonly List<Segment> mOSegments = new List<Segment>(64);

		private readonly List<Segment> mOMerged = new List<Segment>(64);

		private readonly List<Box> mOBoxes = new List<Box>(32);

		private readonly List<UWMotionBody> mOBodies = new List<UWMotionBody>(16);

		/// <summary>The objects met this frame: pushed (solid, met as a wall) and touched (triggers,
		/// objects used on the body), by index, in order, with their tiles.</summary>
		private readonly List<Box> mOPushed = new List<Box>(4);

		private readonly List<Box> mOTouched = new List<Box>(4);

		/// <summary>A motor for the tiles alone (no objects).</summary>
		public UWCharacterMotor(IUWMotionWorld pOWorld) : this(pOWorld, null)
		{
		}

		/// <summary>A motor for the tiles and the objects of the world, by their COMOBJ entries.</summary>
		public UWCharacterMotor(IUWMotionWorld pOWorld, UWCommonObjectProperties pOProperties)
		{
			mOWorld = pOWorld;
			mOProperties = pOProperties;
		}

		/// <summary>The body's own object index, skipped among the objects (the player is 1).</summary>
		public int SelfIndex = 1;

		/// <summary>The centre in tiles.</summary>
		public double X;

		public double Y;

		/// <summary>The feet in the params' fine z, exact; Z is its whole part.</summary>
		public double Zf;

		public int Z
		{
			get { return (int)Math.Floor(Zf + 1.0e-9); }
		}

		/// <summary>The velocity in tiles a tick; the caller sets it on the ground, in the air it is
		/// kept (the original reads no command in the air) - less what a wall met in the air took.</summary>
		public double Vx;

		public double Vy;

		/// <summary>The vertical speed and the gravity in the core's units (Vz / 256 fine z a tick).</summary>
		public double Vz;

		public double Gravity;

		/// <summary>On the ground (no vertical motion).</summary>
		public bool Grounded = true;

		/// <summary>The edges of drops deeper than the step height are walls for the centre.</summary>
		public bool HoldAtLedges;

		/// <summary>Leaving the ground does not start a fall (levitation and flight: the core's
		/// ignore mask 0x1000); the body hovers, gravity 0.</summary>
		public bool NoFall;

		/// <summary>What the body stands on, as of the last settle on the ground: an object (a bridge,
		/// a barrel), or a floor and its terrain class (0 ground, 1 water, 2 lava, 3 the fourth).</summary>
		public bool OnObject { get; private set; }

		public int GroundTerrain { get; private set; }

		/// <summary>The vertical speed of a landing in the last frame (0: none).</summary>
		public double LandedVz { get; private set; }

		/// <summary>The head met the ceiling in the last frame.</summary>
		public bool HitCeiling { get; private set; }

		/// <summary>The speed the body had at each wall met in the air in the last frame, summed, in
		/// the core's speed units (1/65536 tile a tick). The caller makes the impact of it the core
		/// makes of a wall in the air (fTurnAlongWall's bounce, whatever the angle).</summary>
		public double WallImpact { get; private set; }

		/// <summary>A step of the last frame was undone: it took a body holding at ledges off the
		/// ground (one already past an edge, after a landing, where no ledge wall stands), or it
		/// stepped up into a solid object above the head (the core: no headroom).</summary>
		public bool StepHeld { get; private set; }

		public double Radius = DefaultRadius;

		public int Step = DefaultStep;

		public int Height = DefaultHeight;

		/// <summary>The last step met a wall.</summary>
		public bool Touched { get; private set; }

		/// <summary>The normals of the walls and corners the last step met, as x, y pairs.</summary>
		public readonly List<double> Contacts = new List<double>(16);

		/// <summary>Placed standing at a height, at rest.</summary>
		public void Place(double pfX, double pfY, double pfZ)
		{
			X = pfX;
			Y = pfY;
			Zf = pfZ;
			fRest();
			fMarkCell();
		}

		/// <summary>Placed on the floor of the tile under the centre, then on the highest floor the
		/// body stands over.</summary>
		public void PlaceOnFloor(double pfX, double pfY)
		{
			int liType;
			int liFloor;
			int liTerrain;

			X = pfX;
			Y = pfY;

			if (mOWorld.TryGetTile((int)Math.Floor(pfX), (int)Math.Floor(pfY), out liType, out liFloor, out liTerrain))
				Zf = (liFloor & 0xF) * 0x40;

			fRest();
			fSettle();
			fMarkCell();
		}

		/// <summary>The eighth cell of the centre now, so a trigger it stands in is not met before it
		/// moves on (a placement, a teleport).</summary>
		private void fMarkCell()
		{
			miCellX = (int)Math.Floor(X * 8.0);
			miCellY = (int)Math.Floor(Y * 8.0);
		}

		private void fRest()
		{
			Grounded = true;
			Vz = 0.0;
			Gravity = 0.0;
		}

		/// <summary>The take-off: the vertical speed and the gravity (the caller's jump rules).</summary>
		public void Jump(double pfVz, double pfGravity)
		{
			Grounded = false;
			Vz = pfVz;
			Gravity = pfGravity;
		}

		/// <summary>One step of (pfDx, pfDy) tiles on the ground: collide and slide; true when a wall
		/// was met.</summary>
		public bool Move(double pfDx, double pfDy)
		{
			fBegin();

			double lfLength = Math.Sqrt((pfDx * pfDx) + (pfDy * pfDy));
			int liStretches = Math.Max(1, (int)Math.Ceiling(lfLength / MaxStretch));

			for (int liAt = 0; liAt < liStretches; liAt++)
			{
				fStretchHeld(pfDx / liStretches, pfDy / liStretches);
				fSettle();
				fTouch();
			}

			fEnd(pfDx, pfDy);

			return Touched;
		}

		/// <summary>One frame of pfTicks: the velocity and, off the ground, the arc, in sub-steps short
		/// enough that no wall is passed and no landing missed; true when a wall was met.</summary>
		public bool Advance(double pfTicks)
		{
			fBegin();

			if (pfTicks <= 0.0)
				return false;

			double lfLength = Math.Sqrt((Vx * Vx) + (Vy * Vy)) * pfTicks;
			double lfRise = Grounded ? 0.0 : ((Math.Abs(Vz) * pfTicks) + (0.5 * Math.Abs(Gravity) * pfTicks * pfTicks)) / 256.0;
			int liSteps = Math.Max(1, Math.Max((int)Math.Ceiling(lfLength / MaxStretch), (int)Math.Ceiling(lfRise / MaxRise)));
			double lfDt = pfTicks / liSteps;

			for (int liAt = 0; liAt < liSteps; liAt++)
			{
				if (!Grounded)
				{
					double lfZ = Zf + (((Vz * lfDt) + (0.5 * Gravity * lfDt * lfDt)) / 256.0);
					double lfTop = Math.Min((CeilingZ - Height) * 8.0, fHeadroom());

					Vz += Gravity * lfDt;

					// The head stops rising at the ceiling; a body already above it (on a high floor)
					// is not pulled down.
					if (lfZ > lfTop && lfZ > Zf)
					{
						lfZ = Math.Max(lfTop, Zf);
						HitCeiling = true;

						if (Vz > 0.0)
							Vz = 0.0;
					}

					mfFeetBefore = Zf;
					Zf = lfZ;
					fSettle();
					mfFeetBefore = double.NegativeInfinity;
				}

				fStretchHeld(Vx * lfDt, Vy * lfDt);
				fSettle();
				fTouch();
			}

			fEnd(Vx, Vy);

			return Touched;
		}

		private void fBegin()
		{
			Touched = false;
			Contacts.Clear();
			LandedVz = 0.0;
			HitCeiling = false;
			WallImpact = 0.0;
			StepHeld = false;
			mOPushed.Clear();
			mOTouched.Clear();
		}

		/// <summary>After the frame: the solid objects met are pushed along the motion (the
		/// heading of (pfDx, pfDy), 0 north clockwise in 1/65536 of a turn), the triggers touched fire
		/// and the objects used when struck are used on the body.</summary>
		private void fEnd(double pfDx, double pfDy)
		{
			int liHeading = ((int)Math.Round(Math.Atan2(pfDx, pfDy) * 65536.0 / (Math.PI * 2.0))) & 0xFFFF;

			foreach (Box lOBox in mOPushed)
			{
				if (lOBox.Mass != 0)
					mOWorld.PushObject(lOBox.Index, liHeading, PushSpeed, 0, lOBox.TileX, lOBox.TileY);
			}

			foreach (Box lOBox in mOTouched)
			{
				if (lOBox.UsedOnMover)
					mOWorld.UseOnMover(lOBox.Index, SelfIndex, lOBox.TileX, lOBox.TileY);
				else
					mOWorld.TriggerMove(SelfIndex, lOBox.Index, lOBox.TileX, lOBox.TileY);
			}
		}

		/// <summary>The triggers and objects used when struck that the body overlaps, each once a
		/// frame - looked at only when the centre has entered a new eighth cell, as the core scans
		/// on a cell change (fDoTileCollisionMaybe): a trigger the body stands in, after a teleport,
		/// fires once it moves on within it, not as it steps out.</summary>
		private void fTouch()
		{
			int liCellX = (int)Math.Floor(X * 8.0);
			int liCellY = (int)Math.Floor(Y * 8.0);

			if (mOProperties == null || (liCellX == miCellX && liCellY == miCellY))
				return;

			miCellX = liCellX;
			miCellY = liCellY;

			fCollect(X - 1.0, Y - 1.0, X + 1.0, Y + 1.0);

			foreach (Box lOBox in mOBoxes)
			{
				if ((!lOBox.Trigger && !lOBox.UsedOnMover) || !fInHeight(lOBox) || fBoxDistance(lOBox) >= ObjectPad)
					continue;

				bool lbKnown = false;

				foreach (Box lOMet in mOTouched)
					lbKnown |= lOMet.Index == lOBox.Index;

				if (!lbKnown)
					mOTouched.Add(lOBox);
			}
		}

		/// <summary>The highest the feet may rise under the solid objects above the head the body
		/// stands under (their bottom less the height), in fine z.</summary>
		private double fHeadroom()
		{
			double lfTop = double.MaxValue;

			if (mOProperties == null)
				return lfTop;

			fCollect(X - 1.0, Y - 1.0, X + 1.0, Y + 1.0);

			foreach (Box lOBox in mOBoxes)
			{
				if (lOBox.Solid && lOBox.Bottom * 8.0 >= Zf + (Height * 8) - 1.0e-9 && fBoxDistance(lOBox) < ObjectPad)
					lfTop = Math.Min(lfTop, (lOBox.Bottom - Height) * 8.0);
			}

			return lfTop;
		}

		/// <summary>A stretch on the ground or in the air; on the ground the body stays where it was
		/// when the stretch would take a body holding at ledges off the ground (the player's
		/// callback undoes such a sub-step) or step it up with its head into a solid object above
		/// (GetCollisionHeightState: no headroom under a solid object).</summary>
		private void fStretchHeld(double pfDx, double pfDy)
		{
			double lfX = X;
			double lfY = Y;

			fStretch(pfDx, pfDy);

			if (!Grounded)
				return;

			double lfGround = fGround();

			if ((HoldAtLedges && lfGround < Zf - (Step * 8) - 1.0e-9) || (lfGround > Zf + 1.0e-9 && fNoHeadroom(lfGround)))
			{
				X = lfX;
				Y = lfY;
				StepHeld = true;
			}
		}

		/// <summary>Whether feet at fine z pfFeet would put the head into a solid object now above
		/// the head (fGround has just collected the objects around).</summary>
		private bool fNoHeadroom(double pfFeet)
		{
			foreach (Box lOBox in mOBoxes)
			{
				double lfBottom = lOBox.Bottom * 8.0;

				if (lOBox.Solid && lfBottom >= Zf + (Height * 8) - 1.0e-9 && lfBottom < pfFeet + (Height * 8) - 1.0e-9
					&& fBoxDistance(lOBox) < ObjectPad)
					return true;
			}

			return false;
		}

		/// <summary>The feet against the ground under the body: on the ground they follow it up and
		/// down by up to a level, a deeper drop starts a fall; in the air a floor reached is landed on.</summary>
		private void fSettle()
		{
			double lfGround = fGround();

			if (Grounded)
			{
				if (lfGround >= Zf - (Step * 8) - 1.0e-9)
				{
					Zf = lfGround;
					OnObject = mbGroundObject;
					GroundTerrain = miGroundTerrain;
				}
				else
				{
					Grounded = false;
					Vz = 0.0;
					Gravity = NoFall ? 0.0 : FallGravity;
				}

				return;
			}

			if (lfGround < Zf)
				return;

			// Rising onto a floor ends the jump there as falling onto one does (the core's target
			// when rising under a higher floor); lifting the feet and rising on gained a step a time.
			Zf = lfGround;
			LandedVz = Math.Min(0.0, Vz);
			OnObject = mbGroundObject;
			GroundTerrain = miGroundTerrain;
			fRest();
		}

		/// <summary>The body's distance to the nearest wall or corner beyond its radius, in tiles
		/// (negative: it overlaps one); the checks' measure of "inside".</summary>
		public double Clearance()
		{
			fBuild(X - Radius - 1.0, Y - Radius - 1.0, X + Radius + 1.0, Y + Radius + 1.0);

			double lfBest = double.MaxValue;

			foreach (Segment lOSeg in mOMerged)
			{
				double lfTx = lOSeg.Bx - lOSeg.Ax;
				double lfTy = lOSeg.By - lOSeg.Ay;
				double lfLen2 = (lfTx * lfTx) + (lfTy * lfTy);
				double lfAlong = Math.Max(0.0, Math.Min(1.0, (((X - lOSeg.Ax) * lfTx) + ((Y - lOSeg.Ay) * lfTy)) / lfLen2));
				double lfDx = X - (lOSeg.Ax + (lfTx * lfAlong));
				double lfDy = Y - (lOSeg.Ay + (lfTy * lfAlong));
				double lfDist = Math.Sqrt((lfDx * lfDx) + (lfDy * lfDy));

				if (lOSeg.Pad == 0.0 || lOSeg.Owner >= 0)
					continue;

				// On the wall's back side within the pad the body reaches into the rock or the box;
				// farther behind it the wall is another room's side.
				double lfSide = (lOSeg.Nx * (X - lOSeg.Ax)) + (lOSeg.Ny * (Y - lOSeg.Ay));

				if (lfSide < 0.0 && lfAlong > 0.0 && lfAlong < 1.0)
				{
					if (lfSide < -lOSeg.Pad)
						continue;

					lfDist = lfSide;
				}

				lfBest = Math.Min(lfBest, lfDist - lOSeg.Pad);
			}

			// The objects by their boxes: inside one counts by how deep.
			foreach (Box lOBox in mOBoxes)
			{
				if (!fBlocks(lOBox))
					continue;

				double lfDist = fBoxDistance(lOBox);

				if (lfDist == 0.0)
					lfDist = -Math.Min(Math.Min(X - lOBox.X0, lOBox.X1 - X), Math.Min(Y - lOBox.Y0, lOBox.Y1 - Y));

				lfBest = Math.Min(lfBest, lfDist - ObjectPad);
			}

			return lfBest;
		}

		// ------------------------------------------------- One stretch

		private void fStretch(double pfDx, double pfDy)
		{
			double lfReach = Math.Sqrt((pfDx * pfDx) + (pfDy * pfDy)) + Radius + 1.0;

			fBuild(X - lfReach, Y - lfReach, X + lfReach, Y + lfReach);
			fPushOut();

			double lfMx = pfDx;
			double lfMy = pfDy;
			double lfPrevNx = 0.0;
			double lfPrevNy = 0.0;
			bool lbHadPrev = false;

			for (int liPass = 0; liPass < MaxPasses; liPass++)
			{
				if ((lfMx * lfMx) + (lfMy * lfMy) < 1.0e-14)
					break;

				double lfT;
				double lfNx;
				double lfNy;

				if (!fFirstContact(lfMx, lfMy, out lfT, out lfNx, out lfNy))
				{
					X += lfMx;
					Y += lfMy;

					break;
				}

				Touched = true;
				Contacts.Add(lfNx);
				Contacts.Add(lfNy);

				// IN THE AIR a wall takes the velocity into it for good (per user, 2026-10-28: a jump
				// that met a ledge's face too low was set onto the ledge as it rose, where the
				// original bounces off; kept, the motion into the face mantled the body up). The
				// body slides down along the face instead.
				if (!Grounded)
				{
					double lfVn = (Vx * lfNx) + (Vy * lfNy);

					if (lfVn < 0.0)
					{
						// The impact is the whole speed at the hit, as the core's bounce counts it
						// whatever the angle (fTurnAlongWall: Impact += Speed * (15 - e) / 15).
						WallImpact += Math.Sqrt((Vx * Vx) + (Vy * Vy)) * 65536.0;
						Vx -= lfNx * lfVn;
						Vy -= lfNy * lfVn;

						// What two walls leave of it in a corner is a rounding residue, no motion.
						if ((Vx * Vx) + (Vy * Vy) < 1.0e-18)
						{
							Vx = 0.0;
							Vy = 0.0;
						}
					}
				}

				if (miContactOwner >= 0)
					fPushed(miContactOwner);
				X += lfMx * lfT;
				Y += lfMy * lfT;
				lfMx *= 1.0 - lfT;
				lfMy *= 1.0 - lfT;

				// The rest along the wall; a part running into the wall met before is a corner.
				double lfInto = (lfMx * lfNx) + (lfMy * lfNy);

				if (lfInto < 0.0)
				{
					lfMx -= lfNx * lfInto;
					lfMy -= lfNy * lfInto;
				}

				if (lbHadPrev && (lfMx * lfPrevNx) + (lfMy * lfPrevNy) < -Grazing * Math.Sqrt((lfMx * lfMx) + (lfMy * lfMy)))
				{
					lfMx = 0.0;
					lfMy = 0.0;
				}

				lfPrevNx = lfNx;
				lfPrevNy = lfNy;
				lbHadPrev = true;
			}
		}

		/// <summary>The earliest contact of the centre's path (lfMx, lfMy) with a wall or a corner:
		/// the fraction of the path and the contact normal. Only contacts the path runs into count.</summary>
		private bool fFirstContact(double pfMx, double pfMy, out double pfT, out double pfNx, out double pfNy)
		{
			double lfBest = double.MaxValue;
			double lfLength = Math.Sqrt((pfMx * pfMx) + (pfMy * pfMy));

			pfT = 0.0;
			pfNx = 0.0;
			pfNy = 0.0;
			miContactOwner = -1;

			foreach (Segment lOSeg in mOMerged)
			{
				double lfR = lOSeg.Pad + Skin;

				// The face: the line moved out by the radius, within the segment's length.
				double lfInto = (lOSeg.Nx * pfMx) + (lOSeg.Ny * pfMy);

				if (lfInto < -Grazing * lfLength)
				{
					double lfDist = (lOSeg.Nx * (X - lOSeg.Ax)) + (lOSeg.Ny * (Y - lOSeg.Ay));
					double lfT = (lfDist - lfR) / -lfInto;

					if (lfT < 0.0)
						lfT = 0.0;

					if (lfT <= 1.0 && lfT < lfBest && lfDist > -lfR)
					{
						double lfHx = X + (pfMx * lfT);
						double lfHy = Y + (pfMy * lfT);
						double lfTx = lOSeg.Bx - lOSeg.Ax;
						double lfTy = lOSeg.By - lOSeg.Ay;
						double lfAlong = ((lfHx - lOSeg.Ax) * lfTx) + ((lfHy - lOSeg.Ay) * lfTy);
						double lfLen2 = (lfTx * lfTx) + (lfTy * lfTy);

						if (lfAlong >= 0.0 && lfAlong <= lfLen2)
						{
							lfBest = lfT;
							pfNx = lOSeg.Nx;
							pfNy = lOSeg.Ny;
							miContactOwner = lOSeg.Owner;
						}
					}
				}

				double lfBefore = lfBest;

				fCorner(lOSeg.Ax, lOSeg.Ay, pfMx, pfMy, lfR, ref lfBest, ref pfNx, ref pfNy);
				fCorner(lOSeg.Bx, lOSeg.By, pfMx, pfMy, lfR, ref lfBest, ref pfNx, ref pfNy);

				if (lfBest != lfBefore)
					miContactOwner = lOSeg.Owner;
			}

			if (lfBest == double.MaxValue)
				return false;

			pfT = lfBest;

			return true;
		}

		/// <summary>The eighth cell the centre was in at the last look for triggers.</summary>
		private int miCellX = int.MinValue;

		private int miCellY = int.MinValue;

		/// <summary>The object of the contact fFirstContact found (-1: rock).</summary>
		private int miContactOwner = -1;

		/// <summary>A solid object met as a wall, once a frame.</summary>
		private void fPushed(int piIndex)
		{
			foreach (Box lOMet in mOPushed)
			{
				if (lOMet.Index == piIndex)
					return;
			}

			foreach (Box lOBox in mOBoxes)
			{
				if (lOBox.Index == piIndex)
				{
					mOPushed.Add(lOBox);

					return;
				}
			}
		}

		/// <summary>A corner as a circle of the radius around it: the path's entry, or at once when the
		/// centre is inside it and moving further in.</summary>
		private void fCorner(double pfCx, double pfCy, double pfMx, double pfMy, double pfR,
			ref double pfBest, ref double pfNx, ref double pfNy)
		{
			double lfFx = X - pfCx;
			double lfFy = Y - pfCy;
			double lfA = (pfMx * pfMx) + (pfMy * pfMy);
			double lfB = (lfFx * pfMx) + (lfFy * pfMy);

			double lfC = (lfFx * lfFx) + (lfFy * lfFy) - (pfR * pfR);

			// Moving away from the corner, along it, or not at all.
			if (lfA < 1.0e-18 || lfB >= -Grazing * Math.Sqrt(lfA * (lfC + (pfR * pfR))))
				return;

			double lfT;

			if (lfC <= 0.0)
				lfT = 0.0;
			else
			{
				double lfDisc = (lfB * lfB) - (lfA * lfC);

				if (lfDisc < 0.0)
					return;

				lfT = (-lfB - Math.Sqrt(lfDisc)) / lfA;

				if (lfT < 0.0)
					lfT = 0.0;
			}

			if (lfT > 1.0 || lfT >= pfBest)
				return;

			double lfHx = X + (pfMx * lfT) - pfCx;
			double lfHy = Y + (pfMy * lfT) - pfCy;
			double lfLen = Math.Sqrt((lfHx * lfHx) + (lfHy * lfHy));

			if (lfLen < 1.0e-12)
				return;

			pfBest = lfT;
			pfNx = lfHx / lfLen;
			pfNy = lfHy / lfLen;
		}

		/// <summary>A body closer to a wall or a corner than the radius is pushed out along its normal;
		/// one inside an object's box (an object come to lie on it, a start in it) the shortest way out.</summary>
		private void fPushOut()
		{
			for (int liRound = 0; liRound < 3; liRound++)
			{
				bool lbMoved = fPushOutOfBoxes();

				foreach (Segment lOSeg in mOMerged)
				{
					double lfR = lOSeg.Pad + Skin;
					double lfTx = lOSeg.Bx - lOSeg.Ax;
					double lfTy = lOSeg.By - lOSeg.Ay;
					double lfLen2 = (lfTx * lfTx) + (lfTy * lfTy);
					double lfAlong = ((X - lOSeg.Ax) * lfTx) + ((Y - lOSeg.Ay) * lfTy);

					if (lfAlong < 0.0 || lfAlong > lfLen2)
						continue;

					double lfDist = (lOSeg.Nx * (X - lOSeg.Ax)) + (lOSeg.Ny * (Y - lOSeg.Ay));

					// Behind the wall by more than the radius: another room's side, not this one's.
					if (lfDist < lfR - 1.0e-12 && lfDist > -lfR)
					{
						X += lOSeg.Nx * (lfR - lfDist);
						Y += lOSeg.Ny * (lfR - lfDist);
						lbMoved = true;
					}
				}

				if (!lbMoved)
					break;
			}
		}

		/// <summary>Out of every blocking box the centre is nearer than ObjectPad: from inside by the
		/// shortest side, from outside along the line from the nearest point; true when it moved.</summary>
		private bool fPushOutOfBoxes()
		{
			bool lbMoved = false;
			double lfR = ObjectPad + Skin;

			foreach (Box lOBox in mOBoxes)
			{
				if (!fBlocks(lOBox))
					continue;

				double lfDist = fBoxDistance(lOBox);

				if (lfDist >= lfR - 1.0e-12)
					continue;

				if (lfDist > 1.0e-12)
				{
					double lfPx = Math.Max(lOBox.X0, Math.Min(lOBox.X1, X));
					double lfPy = Math.Max(lOBox.Y0, Math.Min(lOBox.Y1, Y));

					X = lfPx + ((X - lfPx) * lfR / lfDist);
					Y = lfPy + ((Y - lfPy) * lfR / lfDist);
				}
				else
				{
					double lfWest = X - lOBox.X0;
					double lfEast = lOBox.X1 - X;
					double lfSouth = Y - lOBox.Y0;
					double lfNorth = lOBox.Y1 - Y;
					double lfLeast = Math.Min(Math.Min(lfWest, lfEast), Math.Min(lfSouth, lfNorth));

					if (lfLeast == lfWest)
						X = lOBox.X0 - lfR;
					else if (lfLeast == lfEast)
						X = lOBox.X1 + lfR;
					else if (lfLeast == lfSouth)
						Y = lOBox.Y0 - lfR;
					else
						Y = lOBox.Y1 + lfR;
				}

				lbMoved = true;
			}

			return lbMoved;
		}

		/// <summary>The ground under the body in fine z: the highest floor it stands over among those
		/// within the step height (not the floor under the centre: a body half over a ledge still
		/// stands on it, else stepping down would turn the ledge into a wall the body is in); on a
		/// slope the slope's height at the centre unless another floor is more than SlopeHold above.
		/// Minus infinity over nothing.</summary>
		private double fGround()
		{
			double lfBest = double.NegativeInfinity;
			double lfSlope = double.NegativeInfinity;
			int liSlopeTerrain = 0;

			mbGroundObject = false;
			miGroundTerrain = 0;
			int liX0 = (int)Math.Floor(X - Radius);
			int liY0 = (int)Math.Floor(Y - Radius);
			int liX1 = (int)Math.Floor(X + Radius);
			int liY1 = (int)Math.Floor(Y + Radius);
			int liCx = (int)Math.Floor(X);
			int liCy = (int)Math.Floor(Y);

			for (int liTy = liY0; liTy <= liY1; liTy++)
			{
				for (int liTx = liX0; liTx <= liX1; liTx++)
				{
					// The ground the body is over catches it: the air's wall rule is for walls only (a
					// slope rising under a falling body let it fall through, the sweep 2026-10-28).
					int liKind = fKind(liTx, liTy, false, false);

					if (liKind == Blocked || fOpenDistance(liTx, liTy, liKind) >= Radius - 1.0e-9)
						continue;

					// The floor at the tile's point nearest the centre.
					double lfU = Math.Max(0.0, Math.Min(0.999999, X - liTx));
					double lfV = Math.Max(0.0, Math.Min(0.999999, Y - liTy));
					double lfFloor = fFloorAt(liTx, liTy, liKind, lfU, lfV);
					int liTerrain = fTerrain(liTx, liTy);

					if (liTx == liCx && liTy == liCy && liKind >= 6)
					{
						lfSlope = lfFloor;
						liSlopeTerrain = liTerrain;
					}

					if (lfFloor > lfBest)
					{
						lfBest = lfFloor;
						miGroundTerrain = liTerrain;
					}
				}
			}

			// The objects that carry the body: solid tops at or below the feet, 3D models within the
			// step height above them. An object's top as high as the best floor wins the tie, as the
			// core's target takes a record at the floor's height: a bridge level with the floor before
			// it carries the body from its first step on (per user, 2026-10-28: on a bridge over water
			// the body swam where its edge still touched the floor, the centre over the water).
			if (mOProperties != null)
			{
				fCollect(X - 1.0, Y - 1.0, X + 1.0, Y + 1.0);

				foreach (Box lOBox in mOBoxes)
				{
					if (fCarries(lOBox) && fBoxDistance(lOBox) < ObjectPad && lOBox.Top * 8.0 >= lfBest - 1.0e-9)
					{
						lfBest = lOBox.Top * 8.0;
						mbGroundObject = true;
					}
				}
			}

			if (lfSlope != double.NegativeInfinity && lfBest <= lfSlope + SlopeHold)
			{
				mbGroundObject = false;
				miGroundTerrain = liSlopeTerrain;

				return lfSlope;
			}

			return lfBest;
		}

		/// <summary>What the last fGround found the ground to be.</summary>
		private bool mbGroundObject;

		private int miGroundTerrain;

		/// <summary>A tile's terrain class (the world's: 0 ground, 1 water, 2 lava, 3 the fourth).</summary>
		private int fTerrain(int piTx, int piTy)
		{
			int liType;
			int liFloor;
			int liTerrain;

			return mOWorld.TryGetTile(piTx, piTy, out liType, out liFloor, out liTerrain) ? liTerrain & 3 : 0;
		}

		/// <summary>
		/// Whether the body could stand at (pfX, pfY) from its height now (the easy step's question,
		/// decision 3 of the plan: the classic scheme's step asks the motor): no wall or object
		/// within its reach there, ground within the step height above and - unless pbMayDrop - at
		/// most the step height below. The ground's height, and whether an object carries it there.
		/// The body itself does not move.
		/// </summary>
		public bool TryStand(double pfX, double pfY, bool pbMayDrop, out double pfGround, out bool pbOnObject, out int piTerrain)
		{
			double lfX = X;
			double lfY = Y;

			X = pfX;
			Y = pfY;
			pfGround = fGround();
			pbOnObject = mbGroundObject;
			piTerrain = miGroundTerrain;

			bool lbFits = Clearance() >= 0.0 && pfGround != double.NegativeInfinity && pfGround <= Zf + (Step * 8) + 1.0e-9
				&& (pbMayDrop || pfGround >= Zf - (Step * 8) - 1.0e-9);

			X = lfX;
			Y = lfY;

			return lbFits;
		}

		// ------------------------------------------------- The objects

		/// <summary>The objects of the tiles around a region as boxes (ScanForCollisions and
		/// fCreateRecord): not the body itself, no switch (the player is a creature mover), no
		/// lying thing of height 0; a creature's box one eighth smaller.</summary>
		private void fCollect(double pfX0, double pfY0, double pfX1, double pfY1)
		{
			mOBoxes.Clear();

			if (mOProperties == null)
				return;

			int liX0 = (int)Math.Floor(pfX0) - 1;
			int liY0 = (int)Math.Floor(pfY0) - 1;
			int liX1 = (int)Math.Floor(pfX1) + 1;
			int liY1 = (int)Math.Floor(pfY1) + 1;

			for (int liTy = liY0; liTy <= liY1; liTy++)
			{
				for (int liTx = liX0; liTx <= liX1; liTx++)
				{
					mOBodies.Clear();
					mOWorld.GetBodiesInTile(liTx, liTy, mOBodies);

					foreach (UWMotionBody lOBody in mOBodies)
					{
						UWCommonObjectProperties.Entry lOEntry;

						if (lOBody.Index == SelfIndex || !mOProperties.TryGet(lOBody.ItemId, out lOEntry))
							continue;

						if (lOEntry.IsSwitch || (lOEntry.Height == 0 && !lOBody.IsMobile))
							continue;

						int liR = lOEntry.Radius & 7;
						Box lOBox = new Box
						{
							Bottom = lOBody.ZPos & 0x7F,
							Solid = lOEntry.IsSolid,
							Model = lOEntry.Is3DModel,
							Trigger = ((lOBody.ItemId >> 6) & 7) == TriggerClass,
							UsedOnMover = lOEntry.UsedWhenThrown,
							Index = lOBody.Index,
							TileX = liTx & 0x3F,
							TileY = liTy & 0x3F,
							Mass = lOEntry.MassTenthStones & 0xFFF
						};

						lOBox.Top = lOBox.Bottom + lOEntry.Height + (lOEntry.Height == 0 ? 1 : 0);

						if (liR == 4)
						{
							lOBox.X0 = liTx;
							lOBox.Y0 = liTy;
							lOBox.X1 = liTx + 1;
							lOBox.Y1 = liTy + 1;
						}
						else
						{
							if (lOBody.IsCreature && liR > 0)
								liR--;

							double lfCx = liTx + ((lOBody.XPos + 0.5) / 8.0);
							double lfCy = liTy + ((lOBody.YPos + 0.5) / 8.0);
							double lfHalf = (liR + 0.5) / 8.0;

							lOBox.X0 = lfCx - lfHalf;
							lOBox.Y0 = lfCy - lfHalf;
							lOBox.X1 = lfCx + lfHalf;
							lOBox.Y1 = lfCy + lfHalf;
						}

						mOBoxes.Add(lOBox);
					}
				}
			}
		}

		/// <summary>Whether a box's height range overlaps the body's.</summary>
		private bool fInHeight(Box pOBox)
		{
			return (pOBox.Top * 8.0) > Zf + 1.0e-9 && (pOBox.Bottom * 8.0) < Zf + (Height * 8) - 1.0e-9;
		}

		/// <summary>Whether a solid box carries the body: its top at or below the feet (or below where
		/// they were before this sub-step's fall, so a top fallen past is landed on), or a 3D model's
		/// within the step height above them - unless the head would go through the ceiling there.</summary>
		private bool fCarries(Box pOBox)
		{
			if (!pOBox.Solid)
				return false;

			double lfTop = pOBox.Top * 8.0;

			if (lfTop <= Math.Max(Zf, mfFeetBefore) + 1.0e-9)
				return true;

			// A 3D model's top is stepped onto from the ground (or hovering), not in a jump or a fall:
			// the core's target in the air is the record's for its head or its top below.
			return pOBox.Model && (Grounded || Vz == 0.0) && lfTop <= Zf + (Step * 8) + 1.0e-9 && !fHeadThrough(lfTop);
		}

		/// <summary>Whether standing at fine z pfFeet would put the head through the ceiling, for a
		/// step up from the feet (the core refuses such a step: the head through the ceiling).</summary>
		private bool fHeadThrough(double pfFeet)
		{
			return pfFeet > Zf + 1.0e-9 && pfFeet + (Height * 8) > (CeilingZ * 8) + 1.0e-9;
		}

		/// <summary>The feet before the sub-step's fall (minus infinity outside a fall).</summary>
		private double mfFeetBefore = double.NegativeInfinity;

		/// <summary>Whether a box is a wall for the body now: solid, in its height range, not carrying it.</summary>
		private bool fBlocks(Box pOBox)
		{
			return pOBox.Solid && fInHeight(pOBox) && !fCarries(pOBox);
		}

		/// <summary>The centre's distance to a box (0 inside).</summary>
		private double fBoxDistance(Box pOBox)
		{
			double lfDx = Math.Max(0.0, Math.Max(pOBox.X0 - X, X - pOBox.X1));
			double lfDy = Math.Max(0.0, Math.Max(pOBox.Y0 - Y, Y - pOBox.Y1));

			return Math.Sqrt((lfDx * lfDx) + (lfDy * lfDy));
		}

		/// <summary>The sides of the boxes that are walls now, facing out, each its own segment.</summary>
		private void fAddBoxWalls()
		{
			foreach (Box lOBox in mOBoxes)
			{
				if (!fBlocks(lOBox))
					continue;

				mOMerged.Add(new Segment { Ax = lOBox.X0, Ay = lOBox.Y1, Bx = lOBox.X1, By = lOBox.Y1, Ny = 1.0, Pad = ObjectPad, Owner = lOBox.Index });
				mOMerged.Add(new Segment { Ax = lOBox.X1, Ay = lOBox.Y0, Bx = lOBox.X1, By = lOBox.Y1, Nx = 1.0, Pad = ObjectPad, Owner = lOBox.Index });
				mOMerged.Add(new Segment { Ax = lOBox.X0, Ay = lOBox.Y0, Bx = lOBox.X1, By = lOBox.Y0, Ny = -1.0, Pad = ObjectPad, Owner = lOBox.Index });
				mOMerged.Add(new Segment { Ax = lOBox.X0, Ay = lOBox.Y0, Bx = lOBox.X0, By = lOBox.Y1, Nx = -1.0, Pad = ObjectPad, Owner = lOBox.Index });
			}
		}

		/// <summary>A tile's floor in fine z at (pfU, pfV) inside it: the floor nibble * 0x40, on a slope
		/// plus the fine position across it &gt;&gt; 2 (as UWMotionCore.fSlopeZ; 6 rises to the north, 7 to the
		/// south, 8 to the east, 9 to the west).</summary>
		private double fFloorAt(int piTx, int piTy, int piKind, double pfU, double pfV)
		{
			int liType;
			int liFloor;
			int liTerrain;

			mOWorld.TryGetTile(piTx, piTy, out liType, out liFloor, out liTerrain);

			int liAcross;

			switch (piKind)
			{
				case 6: liAcross = (int)(pfV * 256.0); break;
				case 7: liAcross = 0xFF - (int)(pfV * 256.0); break;
				case 8: liAcross = (int)(pfU * 256.0); break;
				case 9: liAcross = 0xFF - (int)(pfU * 256.0); break;
				default: liAcross = 0; break;
			}

			return ((liFloor & 0xF) * 0x40) + (liAcross >> 2);
		}

		/// <summary>The centre's distance to a tile's open ground: the square, or a diagonal's open
		/// triangle.</summary>
		private double fOpenDistance(int piTx, int piTy, int piType)
		{
			double lfU = X - piTx;
			double lfV = Y - piTy;

			if (piType < 2 || piType > 5)
			{
				double lfDu = Math.Max(0.0, Math.Max(-lfU, lfU - 1.0));
				double lfDv = Math.Max(0.0, Math.Max(-lfV, lfV - 1.0));

				return Math.Sqrt((lfDu * lfDu) + (lfDv * lfDv));
			}

			double[] lfCorners = OpenTriangles[piType - 2];

			bool lbInside = true;
			double lfBest = double.MaxValue;
			double lfTurn = 0.0;

			for (int liAt = 0; liAt < 3; liAt++)
			{
				double lfAx = lfCorners[liAt * 2];
				double lfAy = lfCorners[(liAt * 2) + 1];
				double lfBx = lfCorners[((liAt + 1) % 3) * 2];
				double lfBy = lfCorners[(((liAt + 1) % 3) * 2) + 1];
				double lfCross = ((lfBx - lfAx) * (lfV - lfAy)) - ((lfBy - lfAy) * (lfU - lfAx));

				if (liAt == 0)
					lfTurn = lfCross;
				else if ((lfCross < 0.0) != (lfTurn < 0.0) && Math.Abs(lfCross) > 1.0e-12)
					lbInside = false;

				double lfTx = lfBx - lfAx;
				double lfTy = lfBy - lfAy;
				double lfAlong = Math.Max(0.0, Math.Min(1.0, (((lfU - lfAx) * lfTx) + ((lfV - lfAy) * lfTy)) / ((lfTx * lfTx) + (lfTy * lfTy))));
				double lfDx = lfU - (lfAx + (lfTx * lfAlong));
				double lfDy = lfV - (lfAy + (lfTy * lfAlong));

				lfBest = Math.Min(lfBest, Math.Sqrt((lfDx * lfDx) + (lfDy * lfDy)));
			}

			return lbInside ? 0.0 : lfBest;
		}

		// ------------------------------------------------- The geometry

		/// <summary>The open triangle's corners of the diagonal types 2 to 5 (2 south-east, 3 south-west,
		/// 4 north-east, 5 north-west), as x, y pairs inside the tile.</summary>
		private static readonly double[][] OpenTriangles =
		{
			new[] { 0.0, 0.0, 1.0, 0.0, 1.0, 1.0 },
			new[] { 0.0, 0.0, 1.0, 0.0, 0.0, 1.0 },
			new[] { 1.0, 0.0, 1.0, 1.0, 0.0, 1.0 },
			new[] { 0.0, 0.0, 1.0, 1.0, 0.0, 1.0 }
		};

		/// <summary>The merge's marks, kept between the frames (no allocation per step).</summary>
		private bool[] mbUsed = new bool[64];

		/// <summary>The sides by number: 0 north (+y), 1 east (+x), 2 south, 3 west.</summary>
		private static readonly int[] SideDx = { 0, 1, 0, -1 };

		private static readonly int[] SideDy = { 1, 0, -1, 0 };

		private const int Blocked = 0;

		/// <summary>Ground too low to stand on while the body holds at ledges.</summary>
		private const int Ledge = -1;

		/// <summary>A tile for the walls: Blocked for rock and for a floor above the feet's reach; with
		/// pbLedges, Ledge for one more than the step height below the feet of a body on the ground
		/// that holds at ledges; else its type (1 open, 2-5 diagonals, 6-9 slopes). A slope's height
		/// is taken at its point nearest the centre, as the core samples the floor near the body - by
		/// its high end, a staircase of slopes made the slope above a wall the body stood in.</summary>
		private int fKind(int piTx, int piTy, bool pbLedges, bool pbAir = true)
		{
			int liType;
			int liFloor;
			int liTerrain;

			if (!mOWorld.TryGetTile(piTx, piTy, out liType, out liFloor, out liTerrain))
				return Blocked;

			liType &= 0xF;

			double lfTop = (liFloor & 0xF) * 0x40;

			if (liType >= 6)
				lfTop = fFloorAt(piTx, piTy, liType, Math.Max(0.0, Math.Min(0.999999, X - piTx)), Math.Max(0.0, Math.Min(0.999999, Y - piTy)));

			if (liType == Blocked || lfTop > Zf + (Step * 8) + 1.0e-9 || fHeadThrough(lfTop))
				return Blocked;

			// IN THE AIR a floor above the feet is stepped onto only RISING and more than the core's
			// radius of two coarse z below its top (SetCollisionTarget: rising, the floor is the
			// target only beyond the radius; falling, the target is no floor's to step onto, so
			// GetCollisionHeightState makes it a wall). A jump that meets a ledge falling, or rising
			// with the feet just under the top, bounces off its face instead of being set onto it
			// (per user, 2026-10-28, comparing with the original; SAVE3 level 1, 11/25 to 11/28).
			// A floor the feet fell past in this sub-step is the one they land on, not a wall.
			if (pbAir && !Grounded && lfTop > Math.Max(Zf, mfFeetBefore) + 1.0e-9 && (Vz < 0.0 || (Vz > 0.0 && lfTop < Zf + RisingBand)))
				return Blocked;

			if (pbLedges && HoldAtLedges && Grounded && lfTop < Zf - (Step * 8) - 1.0e-9)
				return Ledge;

			return liType;
		}

		private int fKind(int piTx, int piTy)
		{
			return fKind(piTx, piTy, true);
		}

		/// <summary>Whether a diagonal's side lies on its open half: 2 open to the south-east, 3
		/// south-west, 4 north-east, 5 north-west.</summary>
		private static bool fSideOpen(int piType, int piSide)
		{
			switch (piType)
			{
				case 2: return piSide == 2 || piSide == 1;
				case 3: return piSide == 2 || piSide == 3;
				case 4: return piSide == 0 || piSide == 1;
				default: return piSide == 0 || piSide == 3;
			}
		}

		/// <summary>Whether this side of a tile borders free ground (a ledge's low ground too: rock
		/// beside it is still rock).</summary>
		private bool fSideFree(int piTx, int piTy, int piSide)
		{
			int liKind = fKind(piTx, piTy, false);

			if (liKind == Blocked)
				return false;

			return liKind < 2 || liKind > 5 || fSideOpen(liKind, piSide);
		}

		/// <summary>Whether this side of a tile is open ground of its kind (not a diagonal's closed side).</summary>
		private static bool fOpenSide(int piKind, int piSide)
		{
			return piKind < 2 || piKind > 5 || fSideOpen(piKind, piSide);
		}

		private void fBuild(double pfX0, double pfY0, double pfX1, double pfY1)
		{
			mOSegments.Clear();

			int liX0 = (int)Math.Floor(pfX0);
			int liY0 = (int)Math.Floor(pfY0);
			int liX1 = (int)Math.Floor(pfX1);
			int liY1 = (int)Math.Floor(pfY1);

			for (int liTy = liY0; liTy <= liY1; liTy++)
			{
				for (int liTx = liX0; liTx <= liX1; liTx++)
				{
					// The rock by the tile's own type; whether it is a ledge only adds the ledge's walls.
					int liKind = fKind(liTx, liTy, false);

					if (liKind == Blocked)
					{
						for (int liSide = 0; liSide < 4; liSide++)
						{
							if (fSideFree(liTx + SideDx[liSide], liTy + SideDy[liSide], (liSide + 2) & 3))
								fAddSide(liTx, liTy, liSide, false);
						}

						continue;
					}

					// A ledge: the open sides of standing ground that face low ground's open sides,
					// walls for the centre facing back into the standing ground.
					if (fKind(liTx, liTy) != Ledge)
					{
						for (int liSide = 0; liSide < 4; liSide++)
						{
							int liNx = liTx + SideDx[liSide];
							int liNy = liTy + SideDy[liSide];

							if (fKind(liNx, liNy) == Ledge && fOpenSide(liKind, liSide) && fOpenSide(fKind(liNx, liNy, false), (liSide + 2) & 3))
								fAddSide(liTx, liTy, liSide, true);
						}
					}

					if (liKind < 2 || liKind > 5)
						continue;

					fAddDiagonal(liTx, liTy, liKind);

					for (int liSide = 0; liSide < 4; liSide++)
					{
						if (!fSideOpen(liKind, liSide) && fSideFree(liTx + SideDx[liSide], liTy + SideDy[liSide], (liSide + 2) & 3))
							fAddSide(liTx, liTy, liSide, false);
					}
				}
			}

			fMerge();
			fCollect(pfX0, pfY0, pfX1, pfY1);
			fAddBoxWalls();
		}

		/// <summary>A tile's side as a wall facing out of the tile (rock), or into it for the centre
		/// (a ledge).</summary>
		private void fAddSide(int piTx, int piTy, int piSide, bool pbLedge)
		{
			Segment lOSeg = new Segment { Pad = pbLedge ? 0.0 : Radius, Owner = -1 };

			switch (piSide)
			{
				case 0: lOSeg.Ax = piTx; lOSeg.Ay = piTy + 1; lOSeg.Bx = piTx + 1; lOSeg.By = piTy + 1; lOSeg.Ny = 1.0; break;
				case 1: lOSeg.Ax = piTx + 1; lOSeg.Ay = piTy; lOSeg.Bx = piTx + 1; lOSeg.By = piTy + 1; lOSeg.Nx = 1.0; break;
				case 2: lOSeg.Ax = piTx; lOSeg.Ay = piTy; lOSeg.Bx = piTx + 1; lOSeg.By = piTy; lOSeg.Ny = -1.0; break;
				default: lOSeg.Ax = piTx; lOSeg.Ay = piTy; lOSeg.Bx = piTx; lOSeg.By = piTy + 1; lOSeg.Nx = -1.0; break;
			}

			if (pbLedge)
			{
				lOSeg.Nx = -lOSeg.Nx;
				lOSeg.Ny = -lOSeg.Ny;
			}

			mOSegments.Add(lOSeg);
		}

		/// <summary>A diagonal facing its open half.</summary>
		private void fAddDiagonal(int piTx, int piTy, int piType)
		{
			const double lfRoot = 0.70710678118654752;
			Segment lOSeg = new Segment { Pad = Radius, Owner = -1 };

			if (piType == 2 || piType == 5)
			{
				lOSeg.Ax = piTx;
				lOSeg.Ay = piTy;
				lOSeg.Bx = piTx + 1;
				lOSeg.By = piTy + 1;
				lOSeg.Nx = piType == 2 ? lfRoot : -lfRoot;
				lOSeg.Ny = piType == 2 ? -lfRoot : lfRoot;
			}
			else
			{
				lOSeg.Ax = piTx + 1;
				lOSeg.Ay = piTy;
				lOSeg.Bx = piTx;
				lOSeg.By = piTy + 1;
				lOSeg.Nx = piType == 4 ? lfRoot : -lfRoot;
				lOSeg.Ny = piType == 4 ? lfRoot : -lfRoot;
			}

			mOSegments.Add(lOSeg);
		}

		/// <summary>Walls on one line facing one way, touching or overlapping, become one segment.</summary>
		private void fMerge()
		{
			mOMerged.Clear();

			if (mbUsed.Length < mOSegments.Count)
				mbUsed = new bool[mOSegments.Count * 2];

			bool[] lbUsed = mbUsed;

			Array.Clear(lbUsed, 0, mOSegments.Count);

			for (int liAt = 0; liAt < mOSegments.Count; liAt++)
			{
				if (lbUsed[liAt])
					continue;

				Segment lOSeg = mOSegments[liAt];
				double lfTx = -lOSeg.Ny;
				double lfTy = lOSeg.Nx;
				double lfLine = (lOSeg.Nx * lOSeg.Ax) + (lOSeg.Ny * lOSeg.Ay);
				double lfFrom = Math.Min((lfTx * lOSeg.Ax) + (lfTy * lOSeg.Ay), (lfTx * lOSeg.Bx) + (lfTy * lOSeg.By));
				double lfTo = Math.Max((lfTx * lOSeg.Ax) + (lfTy * lOSeg.Ay), (lfTx * lOSeg.Bx) + (lfTy * lOSeg.By));

				lbUsed[liAt] = true;

				bool lbGrew = true;

				while (lbGrew)
				{
					lbGrew = false;

					for (int liOther = 0; liOther < mOSegments.Count; liOther++)
					{
						if (lbUsed[liOther])
							continue;

						Segment lOOther = mOSegments[liOther];

						if (Math.Abs(lOOther.Nx - lOSeg.Nx) > 1.0e-9 || Math.Abs(lOOther.Ny - lOSeg.Ny) > 1.0e-9 || lOOther.Pad != lOSeg.Pad)
							continue;

						if (Math.Abs((lOOther.Nx * lOOther.Ax) + (lOOther.Ny * lOOther.Ay) - lfLine) > 1.0e-9)
							continue;

						double lfA = (lfTx * lOOther.Ax) + (lfTy * lOOther.Ay);
						double lfB = (lfTx * lOOther.Bx) + (lfTy * lOOther.By);
						double lfOtherFrom = Math.Min(lfA, lfB);
						double lfOtherTo = Math.Max(lfA, lfB);

						if (lfOtherFrom > lfTo + 1.0e-9 || lfOtherTo < lfFrom - 1.0e-9)
							continue;

						lfFrom = Math.Min(lfFrom, lfOtherFrom);
						lfTo = Math.Max(lfTo, lfOtherTo);
						lbUsed[liOther] = true;
						lbGrew = true;
					}
				}

				// Back from the line and the tangent extent to the two ends.
				double lfPx = lOSeg.Nx * lfLine;
				double lfPy = lOSeg.Ny * lfLine;

				mOMerged.Add(new Segment
				{
					Ax = lfPx + (lfTx * lfFrom),
					Ay = lfPy + (lfTy * lfFrom),
					Bx = lfPx + (lfTx * lfTo),
					By = lfPy + (lfTy * lfTo),
					Nx = lOSeg.Nx,
					Ny = lOSeg.Ny,
					Pad = lOSeg.Pad,
					Owner = -1
				});
			}
		}
	}
}
