namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MOTION OF A THROWN OBJECT OR MISSILE, engine-free (read 2026-10-05 for the motion
	/// rework, stage 1): one step of seg006_1477_164 - the params filled from the record
	/// (InitMotionParams_seg029_29EE_3CC, with the ground friction of a sliding object), the shared
	/// core (UWMotionCore), and the write-back (ApplyProjectileMotion_seg029_29EE_61A) with the
	/// impact's self-damage, the lava burn and, once the object has stopped, the SETTLING: the
	/// landing's culling (water takes what is not valuable, a resting arrow breaks with its
	/// chance, a fireball detonates, the talismans burn in the lava of level 8), then the check
	/// where it lies (in a wall or under a ledge it is removed, on water or lava mostly, above the
	/// floor without support it falls again with a nudge, hanging over an edge it slides off).
	/// Also the launch (PrepareProjectileObject_seg025_791's motion part) and the schedule.
	/// Reading aid: the private notes motion-core.md sections 10 and 16, motion-settle.md.
	/// </summary>
	public sealed class UWMobileObjectMotion
	{
		private readonly IUWMotionWorld mOWorld;

		private readonly IUWMobileObjectHost mOHost;

		private readonly UWCommonObjectProperties mOProperties;

		/// <summary>The core of the step (the static calc array 0x2768).</summary>
		public readonly UWMotionCore Core;

		/// <summary>A second one for the settling and the launch, which run on a calc array of
		/// their own on the stack.</summary>
		public readonly UWMotionCore Scratch;

		/// <summary>The one params block of objects and missiles (0x27F8): bounce style.</summary>
		private readonly UWMotionParams mOParams = new UWMotionParams { Style = UWMotionParams.BounceStyle };

		/// <summary>XHome/YHome_247A/E: the object's tile while it is written back.</summary>
		private int miHomeX;

		private int miHomeY;

		/// <summary>dseg_2765/2766 of the settling: resting on a stand-on-able object with the
		/// centre outside its footprint (hangs over its edge), or inside it.</summary>
		private bool mbHangs;

		private bool mbOnObject;

		public UWMobileObjectMotion(IUWMotionWorld pOWorld, IUWMobileObjectHost pOHost, UWCommonObjectProperties pOProperties)
		{
			mOWorld = pOWorld;
			mOHost = pOHost;
			mOProperties = pOProperties;
			Core = new UWMotionCore(pOWorld, pOProperties);
			Scratch = new UWMotionCore(pOWorld, pOProperties);
		}

		private UWCommonObjectProperties.Entry fEntry(int piItemId)
		{
			UWCommonObjectProperties.Entry lOEntry;

			if (mOProperties != null && mOProperties.TryGet(piItemId, out lOEntry))
				return lOEntry;

			return default;
		}

		private static int I16(int piValue)
		{
			return (short)piValue;
		}

		// ------------------------------------------------- The schedule

		/// <summary>Runs the steps the object is due (ProcessMobileObjects' loop) at this clock;
		/// false when the object left the world.</summary>
		public bool RunDueSteps(UWMobileRecord pORecord, UWCritterClock pOClock)
		{
			for (int liGuard = 0; liGuard < 16 && pOClock.IsDue(pORecord.Phase); liGuard++)
			{
				if (!Step(pORecord))
					return false;
			}

			return true;
		}

		// ------------------------------------------------- seg006_1477_164 (line 41077)

		/// <summary>One motion step with dt = period * 16 ticks. False when the object left the
		/// world: removed, or come to rest as a lying object.</summary>
		public bool Step(UWMobileRecord pORecord)
		{
			UWCommonObjectProperties.Entry lOEntry = fEntry(pORecord.ItemId);

			// Its life used up: removed, unless it is valuable - then it lives on with 1.
			if (pORecord.Hp == 0 && lOEntry.QualityClass < 3)
			{
				if (fObjectCulling(pORecord.Object))
				{
					mOHost.Remove(pORecord);

					return false;
				}

				pORecord.Hp = 1;
			}

			UWMotionHandler lOHandler = UWMotionHandler.ForObject(lOEntry.IsWeightless);

			InitMotionParams(pORecord, mOParams);
			mOParams.Dt = (pORecord.Period & 7) << 4;

			Core.Mover = pORecord.ToBody();
			Core.CalculateMotion(mOParams, lOHandler);
			pORecord.CollidedWithMobile = Core.Mover.CollidedWithMobile;

			miHomeX = pORecord.TileX;
			miHomeY = pORecord.TileY;

			bool lbMobile = ApplyProjectileMotion(pORecord, mOParams);

			if (lbMobile)
				pORecord.Phase = (pORecord.Phase + (pORecord.Period & 7)) & 0xF;

			return lbMobile;
		}

		// ------------------------------------------------- InitMotionParams_seg029_29EE_3CC (line 100109)

		/// <summary>The params from the record (a mobile non-creature: the fine position as stored).
		/// A sliding object - no vertical speed, no gravity, not weightless - gets the ground
		/// friction: its stored speed times 41 (45 with the low-friction bit) instead of 47, and
		/// stops at a stored speed of 2 (4).</summary>
		public void InitMotionParams(UWMobileRecord pORecord, UWMotionParams p)
		{
			UWCommonObjectProperties.Entry lOEntry = fEntry(pORecord.ItemId);

			p.Index = pORecord.Index;
			p.Mass = lOEntry.MassTenthStones & 0xFFF;
			p.LowFriction = lOEntry.SlidesFurther ? 1 : 0;
			p.Elasticity = lOEntry.Elasticity & 0xF;
			p.Resistances = lOEntry.Resistances;
			p.Step = 0;
			p.Radius = lOEntry.Radius & 7;
			p.Height = lOEntry.Height;
			p.Heading = (pORecord.HeadingByte << 8) & 0xFFFF;
			p.Contact = (1 << (pORecord.ContactIndex & 7)) & 0xFF;
			p.Vz = ((pORecord.VzField & 0x1F) - 16) << 6;
			p.Gravity = pORecord.GravityBit ? -4 : 0;
			p.Hp = pORecord.Hp;
			p.X = pORecord.FineX;
			p.Y = pORecord.FineY;
			p.Z = pORecord.FineZ;
			p.Speed = pORecord.SpeedByte & 0x7F;

			if (!pORecord.IsCreature && (p.Gravity | p.Vz) == 0 && !lOEntry.IsWeightless)
			{
				if ((p.LowFriction * 2) + 2 >= p.Speed)
					p.Speed = 0;
				else
					p.Speed = (pORecord.SpeedByte & 0x7F) * ((p.LowFriction * 4) + 0x29);
			}
			else
			{
				p.Speed = I16(p.Speed * 0x2F);

				if (pORecord.IsCreature)
					p.Step = 8;
			}

			p.Impact = 0;
		}

		// ------------------------------------------------- ApplyProjectileMotion_seg029_29EE_61A (line 100436)

		/// <summary>The write-back after the step. True while the object stays mobile.</summary>
		public bool ApplyProjectileMotion(UWMobileRecord pORecord, UWMotionParams p)
		{
			int liTileX = p.X >> 8;
			int liTileY = p.Y >> 8;

			if (liTileX != miHomeX || liTileY != miHomeY)
			{
				mOHost.MoveToTile(pORecord, miHomeX, miHomeY, liTileX, liTileY);
				miHomeX = liTileX;
				miHomeY = liTileY;
				pORecord.TileX = liTileX;
				pORecord.TileY = liTileY;
			}

			pORecord.ZPos = (p.Z >> 3) & 0x7F;
			pORecord.XPos = (p.X >> 5) & 7;
			pORecord.YPos = (p.Y >> 5) & 7;
			pORecord.Hp = p.Hp;

			// The impact of this call hurts the object itself - the hp DamageObject sets is then
			// overwritten with the params' again, so only its other effects remain.
			if ((p.Impact & 0xFFFF) > 0x100)
			{
				int liDamage = (p.Impact & 0xFFFF) >> 8;

				if (!pORecord.IsCreature)
					mOHost.PlaySoundAtObject(0x0F, pORecord, I16(p.Mass - 600) / 50);

				if (mOHost.DamageSelf(pORecord, liDamage, 0))
					return false;

				pORecord.Hp = p.Hp;
			}

			// On lava: 1 fire damage on one update in five.
			if ((p.Contact & UWMotionTables.ContactLava) != 0 && mOWorld.Random() % 5 == 0
				&& mOHost.DamageSelf(pORecord, 1, 8))
				return false;

			if (!pORecord.IsCreature && (p.Speed | p.Vz | p.Gravity) == 0)
			{
				// Come to rest.
				pORecord.ContactIndex = fContactIndex(p.Contact);

				if (!fObjectHitsFloorTile(pORecord))
					return false;

				SettleResult leResult = PlacedObjectCollision(pORecord, miHomeX, miHomeY, false);

				if (leResult == SettleResult.Removed)
				{
					mOHost.Remove(pORecord);

					return false;
				}

				if (leResult == SettleResult.Rests)
				{
					mOHost.PlaceAtRest(pORecord, pORecord.Octant & 7, pORecord.Hp & 0x3F);

					return false;
				}

				fNudge(p);
			}

			pORecord.HeadingByte = (p.Heading >> 8) & 0xFF;
			pORecord.TileX = miHomeX & 0x3F;
			pORecord.TileY = miHomeY & 0x3F;
			pORecord.GravityBit = p.Gravity == -4;

			int liField = (p.Vz / 64) + 16;

			if (liField < 0)
				liField = 0;
			else if (liField > 31)
				liField = 31;

			pORecord.VzField = liField;
			pORecord.SpeedByte = (p.Speed / 0x2F) & 0x7F;
			pORecord.ContactIndex = fContactIndex(p.Contact);
			pORecord.FineX = p.X;
			pORecord.FineY = p.Y;
			pORecord.FineZ = p.Z;

			if (pORecord.Object != null)
			{
				pORecord.Object.TileX = pORecord.TileX;
				pORecord.Object.TileY = pORecord.TileY;
				pORecord.Object.XPos = (ushort)pORecord.XPos;
				pORecord.Object.YPos = (ushort)pORecord.YPos;
				pORecord.Object.ZPos = pORecord.ZPos;
			}

			return true;
		}

		private static int fContactIndex(int piContact)
		{
			int liAt = piContact & 0xFF;

			return liAt < UWMotionTables.ContactStateIndex.Length ? UWMotionTables.ContactStateIndex[liAt] & 7 : 0;
		}

		// ------------------------------------------------- ObjectHitsFloorTileDestroyTalismans_seg029_C6F (line 101422)

		/// <summary>
		/// The landing: the object becomes a static one (its quality the hp, its heading the saved
		/// static heading), unless the landing culls it - in water with the chance 8 of 8 (the
		/// splash first), on lava a talisman at the spot of level 8, otherwise with the object's
		/// own chance (an arrow 3 of 8, a bolt 4 of 8), each time against the culling test that
		/// spares what is valuable; on level 9 everything. A fireball or lightning bolt (cull 9)
		/// detonates. False when the object is gone.
		/// </summary>
		private bool fObjectHitsFloorTile(UWMobileRecord pORecord)
		{
			UWCommonObjectProperties.Entry lOEntry = fEntry(pORecord.ItemId);
			bool lbKeep = true;
			int liCull = lOEntry.ProjectileCulling & 0xF;
			int liState = pORecord.ContactIndex;

			if (liState == 1)
			{
				liCull = 8;
				mOHost.Splash(pORecord);
			}
			else if (liState == 2 && liCull == 10 && mOHost.TalismanBurns(pORecord, miHomeX, miHomeY))
			{
				liCull = 8;
				lbKeep = false;
			}

			if (liCull > 0 && liCull <= 8 && (mOWorld.Random() & 7) < liCull && fObjectCulling(pORecord.Object))
				lbKeep = false;

			if (mOHost.DungeonLevel == 9)
				lbKeep = false;

			if (lbKeep)
			{
				// The static copy's heading: the saved one for most, the octant bits for classes 5
				// and 6 and the objects with COMOBJ byte 9 bits 0-1 == 2.
				int liClass = (pORecord.ItemId >> 6) & 7;

				if (liClass != 5 && liClass != 6 && (lOEntry.Byte9 & 3) != 2)
					pORecord.Octant = pORecord.StaticHeading & 7;

				mOHost.OnDemoted(pORecord);
			}

			int liOwner = liCull == 9 ? (pORecord.IsCreature ? 0 : pORecord.Owner) : 0;

			if (liCull == 9 && mOHost.Detonate(pORecord, miHomeX, miHomeY, liOwner) == 0 && lbKeep && fObjectCulling(pORecord.Object))
				lbKeep = false;

			if (!lbKeep)
			{
				mOHost.Remove(pORecord);

				return false;
			}

			return true;
		}

		// ------------------------------------------------- ObjectCulling_seg027_2861_226 (line 95189)

		/// <summary>
		/// The culling test with the range 10: true when the object is to be removed - it is not
		/// protected (word 0 bit 13), not valuable (COMOBJ byte 9 bits 2-5 plus half the extra
		/// count of a stack greater than a range of 10..12), and holds nothing protected. The
		/// second roll of the routine can never save anything at this range, but it is rolled.
		/// </summary>
		private bool fObjectCulling(UWObject pOObject)
		{
			if (pOObject == null)
				return false;

			int liRange = 10 + (int)(((long)mOWorld.Random() * 3) / 0x8000);

			if (fIsProtected(pOObject, liRange))
				return false;

			if (!pOObject.HasQuantity && pOObject.Contents != null)
			{
				foreach (UWObject lOContent in pOObject.Contents)
				{
					if (fIsProtected(lOContent, liRange))
						return false;
				}
			}

			return ((long)mOWorld.Random() * 10) / 0x8000 < liRange;
		}

		/// <summary>The protection of one object: word 0 bit 13, or valuable enough for the range
		/// (UWLiquidCulling.Swallows, the value rule of the liquids).</summary>
		private bool fIsProtected(UWObject pOObject, int piRange)
		{
			if (pOObject.DoorDirection)
				return true;

			int liQuantity = pOObject.HasQuantity && (pOObject.Quantity & 0x200) == 0 ? pOObject.Quantity : 1;

			return !UWLiquidCulling.Swallows(fEntry(pOObject.ID).CullingPriority, liQuantity, piRange);
		}

		// ------------------------------------------------- PlacedObjectCollison_seg029_104D (line 102049)

		public enum SettleResult
		{
			/// <summary>Removed - after the culling test, which spares a valuable object (Rests).</summary>
			Removed,

			/// <summary>Stays as a static object.</summary>
			Rests,

			/// <summary>Made mobile again: it falls or slides on.</summary>
			MovesOn
		}

		/// <summary>
		/// Where a resting object lies: in a wall, under a ledge or inside a solid object it is
		/// removed (the culling test may still spare it); on a water floor the same; on a lava
		/// floor unless it is indestructible or fire-proof; on a ground floor, or on an object it
		/// may rest on, it stays; above the floor without support it is made mobile again. A
		/// weightless object stays where it is without any test.
		/// </summary>
		public SettleResult PlacedObjectCollision(UWMobileRecord pORecord, int piTileX, int piTileY, bool pbScatter)
		{
			UWCommonObjectProperties.Entry lOEntry = fEntry(pORecord.ItemId);
			bool lbRemove = false;
			bool lbRetried = false;
			bool lbMobile = false;

			mbHangs = false;
			mbOnObject = false;

			if (lOEntry.IsWeightless)
				return SettleResult.Rests;

			while (true)
			{
				UWMotionCore c = Scratch;

				c.Mover = pORecord.ToBody();
				c.SetCalc((piTileX * 8) + pORecord.XPos, (piTileY * 8) + pORecord.YPos, pORecord.ZPos,
					lOEntry.Radius & 7, lOEntry.Height, pORecord.Index);

				// The step height is the RADIUS here, not the mover's 0.
				c.ProcessMotionTileHeights(lOEntry.Radius & 7);

				bool lbTight = (c.CentreFloor & 0xFF) + (lOEntry.Radius & 7) >= pORecord.ZPos && !lbRetried;

				c.ScanForCollisions(lbTight, true);
				c.SortCollisions();

				if (c.Overlap == 0 && (sbyte)c.First > 0 && (c.First & 0xFF) <= c.Count)
				{
					// The records just below the feet, highest top first: does it rest on one?
					for (int liJ = c.First - 1; liJ >= 0; liJ--)
					{
						if (c.Records[liJ].Top != pORecord.ZPos)
							break;

						if (!fEntry(c.Records[liJ].Body.ItemId).Is3DModel)
							continue;

						if ((c.Records[liJ].Flags & 0x10) != 0)
						{
							mbOnObject = true;

							break;
						}

						mbHangs = true;
					}
				}

				if (((c.Flags | c.AllFlags) & 0x300) != 0 || c.Overlap != 0)
					lbRemove = true;
				else
				{
					switch (c.Flags & 7)
					{
						case 5:
							lbRemove = true;
							break;
						case 6:
							lbRemove = !UWLiquidCulling.SparedByLava(lOEntry.QualityClass, lOEntry.Resistances);
							break;
						default:
							if ((c.Flags & 8) != 0 || mbOnObject)
								break;

							if (lbTight)
							{
								lbRetried = true;

								continue;
							}

							MakeMobile(pORecord, piTileX, piTileY);
							lbMobile = true;

							if (mbHangs)
							{
								pORecord.SpeedByte = 3;
								pORecord.HeadingByte = (pORecord.HeadingByte + ((mOWorld.Random() % 9) << 4) + 0xC0) & 0xFF;
							}

							if (pbScatter)
							{
								pORecord.SpeedByte = (mOWorld.Random() & 3) + 1;
								pORecord.VzField = ((mOWorld.Random() & 3) + 0xE) & 0x1F;
							}

							break;
					}
				}

				break;
			}

			if (lbRemove)
				return fObjectCulling(pORecord.Object) ? SettleResult.Removed : SettleResult.Rests;

			return lbMobile ? SettleResult.MovesOn : SettleResult.Rests;
		}

		/// <summary>MakeMobile_seg029_29EE_A1F with InitMobileRecord_seg029_29EE_B3A: the object
		/// becomes a mobile record again - due one unit after the clock, period 2, no speed, the
		/// position in the middle of its eighth, hp from the quality (the record's Hp on entry).
		/// Also the host's way to knock a lying object loose (UWProjectileWorld.PushObject).</summary>
		public void MakeMobile(UWMobileRecord pORecord, int piTileX, int piTileY)
		{
			UWCommonObjectProperties.Entry lOEntry = fEntry(pORecord.ItemId);
			int liClass = (pORecord.ItemId >> 6) & 7;
			int liQuality = pORecord.Hp & 0x3F;

			InitMobileRecord(pORecord, piTileX, piTileY);

			// MakeMobile: the hp from the static copy's quality.
			pORecord.Hp = liQuality;

			if (liClass != 5 && (lOEntry.Byte9 & 3) != 2)
				pORecord.StaticHeading = pORecord.Octant & 7;
		}

		/// <summary>InitMobileRecord_seg029_29EE_B3A (line 101261).</summary>
		public void InitMobileRecord(UWMobileRecord pORecord, int piTileX, int piTileY)
		{
			pORecord.HeadingByte = ((pORecord.Octant & 7) << 5) & 0xFF;
			pORecord.FineHeadingBits = 0;
			pORecord.VzField = 16;
			pORecord.GravityBit = !fEntry(pORecord.ItemId).IsWeightless;
			pORecord.TileX = piTileX & 0x3F;
			pORecord.TileY = piTileY & 0x3F;
			pORecord.Phase = (mOHost.ClockPhase + 1) & 0xF;
			pORecord.Period = 2;
			pORecord.SpeedByte = 0;

			if (pORecord.Object != null)
				pORecord.Object.IsHidden = false;

			pORecord.Hp = 0x3F;
			pORecord.ContactIndex = 0;

			if (!pORecord.IsCreature)
			{
				pORecord.FineX = (piTileX << 8) + (pORecord.XPos << 5) + 0xF;
				pORecord.FineY = (piTileY << 8) + (pORecord.YPos << 5) + 0xF;
				pORecord.FineZ = pORecord.ZPos << 3;
				pORecord.Owner = 0;
			}
		}

		/// <summary>seg029_29EE_100B: a re-promoted object gets a push - hanging over an edge it
		/// slides off, turned up to 45 degrees either way at speed 0xBC; otherwise it drops with
		/// gravity and a random small speed.</summary>
		private void fNudge(UWMotionParams p)
		{
			if (mbHangs)
			{
				p.Heading = (p.Heading + (mOWorld.Random() & 0x3FFF) + 0xE000) & 0xFFFF;
				p.Speed = 0xBC;
			}
			else
			{
				p.Speed = ((mOWorld.Random() + 1) & 3) * 0x2F;
				p.Gravity = -4;
			}
		}

		// ------------------------------------------------- The launch (motion-core.md 16)

		/// <summary>What PrepareProjectileObject_seg025_791 needs of the launcher.</summary>
		public struct Launcher
		{
			/// <summary>The launcher's index (1 the player), item id and whether it is a creature.</summary>
			public int Index;

			public bool IsCreature;

			public bool IsPlayer;

			/// <summary>Its tile, eighth and coarse z.</summary>
			public int TileX;

			public int TileY;

			public int XPos;

			public int YPos;

			public int ZPos;

			/// <summary>Word 2 bits 7-9 and byte +0x18 bits 0-4: the octant and the fine heading.</summary>
			public int Octant;

			public int FineHeadingBits;

			/// <summary>COMOBJ: the height (0 for a trap: no height offset, no placement test) and
			/// the radius.</summary>
			public int Height;

			public int Radius;

			/// <summary>PLAYER.DAT 0xB9, the swim counter; above 0x50 the player's missile starts at
			/// the full height less the counter / 8.</summary>
			public int SwimCounter;
		}

		/// <summary>
		/// The motion part of PrepareProjectileObject: the heading from the launcher's facing plus
		/// the aim, the start a few eighths ahead of the launcher and five sixths of its height
		/// up plus two per pitch step, tested for room (seg025_B51) when the launcher has a
		/// height; the vertical speed from the pitch, period 1, the speed byte. The record must
		/// carry its Object and Index; false when there is no room - the record is then unused.
		/// pbHeadingFromLauncher is MissileLauncherHeadingBase's flag (1 for every launcher but a
		/// spell trap and the arrow trap). The hp is left at 0x3F: the caller sets it from the
		/// item's quality for the player's arrows and thrown things (MissileRelease, DropOrThrow).
		/// </summary>
		public bool TryLaunch(UWMobileRecord pORecord, Launcher pOLauncher, bool pbHeadingFromLauncher,
			int piMissileHeading, int piPitch, int piSpeedByte)
		{
			UWCommonObjectProperties.Entry lOEntry = fEntry(pORecord.ItemId);
			int liHeadingBase = pbHeadingFromLauncher ? pOLauncher.FineHeadingBits & 0x1F : 0;

			liHeadingBase += (pOLauncher.Octant & 7) << 5;
			liHeadingBase = (liHeadingBase + piMissileHeading + 0x100) & 0xFF;

			pORecord.XPos = pOLauncher.XPos;
			pORecord.YPos = pOLauncher.YPos;
			pORecord.ZPos = pOLauncher.ZPos;

			InitMobileRecord(pORecord, pOLauncher.TileX, pOLauncher.TileY);

			pORecord.Octant = (liHeadingBase >> 5) & 7;
			pORecord.FineHeadingBits = liHeadingBase & 0x1F;
			pORecord.HeadingByte = liHeadingBase;

			int liHeight = pOLauncher.Height & 0xFF;

			if (liHeight != 0)
			{
				pORecord.ZPos = (pOLauncher.ZPos + ((liHeight * 5) / 6) + (2 * piPitch)) & 0x7F;

				if (pOLauncher.IsPlayer && pOLauncher.SwimCounter > 0x50)
					pORecord.ZPos = (pOLauncher.ZPos + (2 * piPitch) + liHeight - (pOLauncher.SwimCounter >> 3)) & 0x7F;

				if (!fTestPlacement(pORecord, pOLauncher, liHeadingBase))
					return false;
			}

			if (!pORecord.IsCreature)
			{
				pORecord.FineX = (pORecord.TileX << 8) + (pORecord.XPos << 5) + 0xF;
				pORecord.FineY = (pORecord.TileY << 8) + (pORecord.YPos << 5) + 0xF;
				pORecord.FineZ = pORecord.ZPos << 3;

				int liOwner = 0;

				if (pOLauncher.IsCreature)
					liOwner = pOLauncher.Index < 0x100 ? pOLauncher.Index : 0;

				pORecord.Owner = liOwner;
				pORecord.CollidedWithMobile = false;
			}

			pORecord.VzField = (piPitch + 16) & 0x1F;
			pORecord.Period = 1;
			pORecord.SpeedByte = piSpeedByte & 0x7F;

			if (lOEntry.CanHaveOwner && pORecord.Object != null)
				pORecord.Object.Owner = 0;

			if (pORecord.Object != null)
			{
				pORecord.Object.TileX = pORecord.TileX;
				pORecord.Object.TileY = pORecord.TileY;
				pORecord.Object.XPos = (ushort)pORecord.XPos;
				pORecord.Object.YPos = (ushort)pORecord.YPos;
				pORecord.Object.ZPos = pORecord.ZPos;
			}

			return true;
		}

		/// <summary>seg025_B51: the start, radii + 4 eighths ahead of the launcher, must have no
		/// corner floor above the missile and no solid object in its z range.</summary>
		private bool fTestPlacement(UWMobileRecord pORecord, Launcher pOLauncher, int piHeadingBase)
		{
			UWCommonObjectProperties.Entry lOEntry = fEntry(pORecord.ItemId);
			UWMotionCore c = Scratch;
			int liX = (pOLauncher.TileX * 8) + pOLauncher.XPos;
			int liY = (pOLauncher.TileY * 8) + pOLauncher.YPos;

			StepInDirection(piHeadingBase, (pOLauncher.Radius & 7) + (lOEntry.Radius & 7) + 4, ref liX, ref liY);

			c.Mover = pORecord.ToBody();
			c.SetCalc(liX, liY, pORecord.ZPos, lOEntry.Radius & 7, lOEntry.Height, pORecord.Index);
			c.ScanForCollisions(false, true);
			c.ProcessMotionTileHeights(0);

			if (((c.Flags | c.AllFlags) & 0x300) != 0)
				return false;

			if (c.Count != 0)
			{
				c.SortCollisions();

				if (c.Overlap != 0)
					return false;
			}

			pORecord.TileX = (liX >> 3) & 0x3F;
			pORecord.TileY = (liY >> 3) & 0x3F;
			pORecord.XPos = liX & 7;
			pORecord.YPos = liY & 7;

			return true;
		}

		/// <summary>GetCoordinateInDirection_seg041_4D: an eighth-tile position moved a distance
		/// in a heading (1/256 of a turn, 0 = +y): the table entry of the heading's high byte alone
		/// over 0x80, times the distance, over 0x100, truncating, then each axis pushed one further
		/// AWAY from zero.</summary>
		public static void StepInDirection(int piHeading8, int piDistance, ref int piX, ref int piY)
		{
			int liAngle = ((0x140 - piHeading8) & 0xFF) << 8;
			int liSin;
			int liCos;

			UWMotionTables.SinCosCoarse(liAngle, out liSin, out liCos);

			int liOy = I16((liSin / 0x80) * piDistance) / 0x100;
			int liOx = I16((liCos / 0x80) * piDistance) / 0x100;

			if (liOy > 0)
				liOy++;
			else if (liOy < 0)
				liOy--;

			if (liOx > 0)
				liOx++;
			else if (liOx < 0)
				liOx--;

			piY += liOy;
			piX += liOx;
		}
	}
}
