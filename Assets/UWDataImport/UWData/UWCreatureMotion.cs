namespace UWDataImport.UWData
{
	/// <summary>
	/// THE MOTION OF A CREATURE on the engine-free core (stage 2 of the motion rework, read
	/// 2026-10-05): the motion part of one creature update - NPCInitialProcessing_seg007_2488
	/// from the handler choice (52747) to the heading comparison (52990) - with the three motion
	/// callbacks of UW.EXE: the land creature's seg006_1477_431, the flier's seg006_1477_5FE and
	/// the swimmer's seg006_1477_65E. The params come from the record as for a thrown object
	/// (see InitMotionParams_seg029_29EE_3CC in UWMobileObjectMotion), except that a creature
	/// starts at a random spot inside its eighth and gets the step height 8; the write-back is
	/// the creature's tail of ApplyProjectileMotion (see UWMobileObjectMotion.ApplyProjectileMotion):
	/// the eighth and the height, the heading byte, the gravity bit, the vertical speed field,
	/// the speed byte and the contact state - never a fine position.
	///
	/// What the callbacks decide reaches the brain as the StepResult (ICritterHost): stuck
	/// (airborne), drowned, blocked at a water, lava or drop edge, a closed door or another
	/// object hit, the heading the wall turned, the ceiling a flier touched. The brain's own
	/// writes on those (the death animation, the interval) stay with it.
	///
	/// Reading aid: the private notes motion-npc.md.
	/// </summary>
	public sealed class UWCreatureMotion
	{
		/// <summary>The three movers of the critter table (byte 0x0A bit 7 flier, bit 6 swimmer)
		/// with their own params block and handler: land 0x27A8 / 0x285C, flier 0x27D0 / 0x2868,
		/// swimmer 0x2820 / 0x2880.</summary>
		public enum Kind
		{
			Land = 0,

			Flier = 1,

			Swimmer = 2
		}

		/// <summary>The handler blocks (InitVariablesAndDelegates_seg006_1477_28E, 41231): the
		/// result flags ignored, the flags that call the callback, the terrain mask of
		/// GetCollisionHeightState - land {0, 0x1F30, 0x1010}, flier {0x1000, 0x700, 0x80}, swimmer
		/// {0x10, 0x1728, 0x10A8}.</summary>
		private static readonly int[] IgnoreMaskByKind = { 0, 0x1000, 0x10 };

		private static readonly int[] CallbackMaskByKind = { 0x1F30, 0x700, 0x1728 };

		private static readonly int[] TerrainMaskByKind = { 0x1010, 0x80, 0x10A8 };

		/// <summary>COMOBJ byte 8 bit 3: the kind is lava-proof (the stone golem 0x78, the wisp
		/// 0x7B and 0x7C in UW1).</summary>
		public const int LavaProofResistanceBit = 8;

		/// <summary>The lava flag of the result.</summary>
		private const int LavaFlag = 0x20;

		/// <summary>
		/// THE LAVA-PROOF QUIRK (NPCInitialProcessing 52618-52636): for a lava-proof creature the
		/// SELECTED handler block loses the lava flag from its callback mask and gains it in its
		/// ignore mask - on the static block, and nothing ever restores it (the blocks are
		/// initialised once at program start). So from the first update of a lava-proof land
		/// creature within reach of the player on, every land creature walks over a lava edge
		/// without stopping (the burn stays: the contact state is taken before the mask). Kept
		/// for the session as in the original; the self-check resets it.
		/// </summary>
		private static readonly bool[] msLavaFlagDropped = new bool[3];

		public static bool LavaFlagDropped(Kind peKind)
		{
			return msLavaFlagDropped[(int)peKind];
		}

		/// <summary>The handler blocks as at program start.</summary>
		public static void ResetHandlers()
		{
			for (int liAt = 0; liAt < msLavaFlagDropped.Length; liAt++)
				msLavaFlagDropped[liAt] = false;
		}

		/// <summary>The momentum a push hands the struck creature (seg029_29EE_3, loc.speed =
		/// 0xEB): the speed byte 5, one update of 1.8 eighths in the pusher's heading.</summary>
		public const int PushSpeed = 0xEB;

		private readonly IUWMotionWorld mOWorld;

		private readonly UWCommonObjectProperties mOProperties;

		/// <summary>The core of the step (the static calc array 0x2768).</summary>
		public readonly UWMotionCore Core;

		/// <summary>The pre-state's own calc array (seg006_1477_367 works on a stack copy).</summary>
		public readonly UWMotionCore PreState;

		/// <summary>The three params blocks: slide style (+0x17 = 0x80), as InitVariablesAndDelegates
		/// sets them.</summary>
		private readonly UWMotionParams[] mOParamsByKind =
		{
			new UWMotionParams { Style = UWMotionParams.SlideStyle },
			new UWMotionParams { Style = UWMotionParams.SlideStyle },
			new UWMotionParams { Style = UWMotionParams.SlideStyle }
		};

		private readonly UWMotionHandler[] mOHandlersByKind = new UWMotionHandler[3];

		// ------------------------------------------------- per-step scratch (the original's globals)

		private UWCritterRecord mORecord;

		private UWMotionParams mOParams;

		private StepResult mOResult;

		/// <summary>IsNPCActive_2440.</summary>
		private bool mbActive;

		/// <summary>LikelyNPCTileStates_2438: the terrain flags around the creature BEFORE the
		/// step (seg006_1477_367).</summary>
		private int miPreState;

		/// <summary>XHome/YHome_247A/E: the creature's tile while it is written back.</summary>
		private int miHomeX;

		private int miHomeY;

		public UWCreatureMotion(IUWMotionWorld pOWorld, UWCommonObjectProperties pOProperties)
		{
			mOWorld = pOWorld;
			mOProperties = pOProperties;
			Core = new UWMotionCore(pOWorld, pOProperties);
			PreState = new UWMotionCore(pOWorld, pOProperties);

			mOHandlersByKind[(int)Kind.Land] = new UWMotionHandler { Callback = fLandCallback };
			mOHandlersByKind[(int)Kind.Flier] = new UWMotionHandler { Callback = fFlierCallback };
			mOHandlersByKind[(int)Kind.Swimmer] = new UWMotionHandler { Callback = fSwimmerCallback };
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

		/// <summary>The mover of a table row (NPCInitialProcessing 52747-52775): bit 7 before bit 6.</summary>
		public static Kind KindOf(UWObjectClassProperties.Critter pORow)
		{
			if (pORow.IsFlier)
				return Kind.Flier;

			return pORow.IsSwimmer ? Kind.Swimmer : Kind.Land;
		}

		// ------------------------------------------------- One step

		/// <summary>
		/// The physics of one creature update: nothing when the creature stands with speed 0 and
		/// level pitch (the brain's Motion.Moves, see NeedsToMove_seg007_1798_26D4 there), otherwise
		/// one step of dt = interval * 16
		/// ticks on the core with the kind's handler, and the write-back into the record. The
		/// record's tile fields are written; the host relinks the tile list when TileChanged says
		/// so. piIndex is the creature's object index for the collision scans.
		/// </summary>
		public StepResult Step(UWCritterRecord pORecord, int piIndex, Kind peKind)
		{
			mORecord = pORecord;
			mOResult = default(StepResult);
			mbActive = true;

			if (pORecord.IsStanding && pORecord.Speed == 0 && pORecord.Pitch == UWCritterRules.FlierPitchLevel)
				return mOResult;

			UWCommonObjectProperties.Entry lOEntry = fEntry(pORecord.ItemId);
			int liKind = (int)peKind;

			if ((lOEntry.Resistances & LavaProofResistanceBit) != 0)
				msLavaFlagDropped[liKind] = true;

			UWMotionHandler lOHandler = mOHandlersByKind[liKind];

			lOHandler.IgnoreMask = IgnoreMaskByKind[liKind] | (msLavaFlagDropped[liKind] ? LavaFlag : 0);
			lOHandler.CallbackMask = CallbackMaskByKind[liKind] & (msLavaFlagDropped[liKind] ? ~LavaFlag : ~0);
			lOHandler.TerrainMask = TerrainMaskByKind[liKind];

			mOParams = mOParamsByKind[liKind];
			fInitMotionParams(pORecord, piIndex, lOEntry, mOParams);
			mOParams.Dt = (pORecord.Interval & 7) << 4;

			int liHeadingBefore = pORecord.FineHeading;

			miPreState = fPreState(pORecord, piIndex, lOEntry);

			Core.Mover = new UWMotionBody
			{
				Index = piIndex,
				ItemId = pORecord.ItemId,
				XPos = pORecord.FineX,
				YPos = pORecord.FineY,
				ZPos = pORecord.ZPos,
				IsMobile = true,
				IsCreature = true,
				CollidedWithMobile = pORecord.HasPath
			};

			Core.CalculateMotion(mOParams, lOHandler);

			// ONE BIT, TWO MEANINGS (found 2026-10-05 after the user saw goblins follow him into
			// the water in the original, SAVE4's pool): the "collided with a mobile" flag that
			// CollideObjects sets on every mobile mover (byte 0x15 bit 7, seg029_29EE_173 line 99938)
			// is the same bit NPC_Goto stores its path in and the land callback tests as "has a
			// path". A creature that touched the player or another creature therefore walks over
			// the next water, drop or lava edge as if it followed a path - and drowns in the water.
			// The bit lives only while the animation stays walk or stand: the update's path
			// bookkeeping clears it for the combat stance and the back-off (UWCritterBrain, step 5),
			// which is why a creature follows the player into the water exactly when it walks at
			// him from melee contact (per user, the original: "only when the goblin steps forward").
			if (Core.Mover.CollidedWithMobile)
				pORecord.HasPath = true;

			miHomeX = pORecord.TileX;
			miHomeY = pORecord.TileY;
			fApplyMotion(pORecord, mOParams);

			// HasCurrObjHeadingChanged_2449 (52980-52990): byte 9 after the step against before.
			if (pORecord.FineHeading != liHeadingBefore)
				mOResult.HeadingDeflected = true;

			mOResult.Stuck = !mbActive;

			return mOResult;
		}

		/// <summary>The creature's params (InitMotionParams, the class-1 branches, see
		/// UWMobileObjectMotion.InitMotionParams): the position is the record's eighth and coarse
		/// height with a random spot inside - x, y then z from the RNG - the speed times 0x2F, the
		/// step height 8, no ground friction.</summary>
		private void fInitMotionParams(UWCritterRecord pORecord, int piIndex, UWCommonObjectProperties.Entry pOEntry, UWMotionParams p)
		{
			p.Index = piIndex;
			p.Mass = pOEntry.MassTenthStones & 0xFFF;
			p.LowFriction = pOEntry.SlidesFurther ? 1 : 0;
			p.Elasticity = pOEntry.Elasticity & 0xF;
			p.Resistances = pOEntry.Resistances;
			p.Step = 8;
			p.Radius = pOEntry.Radius & 7;
			p.Height = pOEntry.Height;
			p.Heading = (pORecord.FineHeading << 8) & 0xFFFF;
			p.Contact = (1 << (pORecord.TileState & 7)) & 0xFF;
			p.Vz = ((pORecord.Pitch & 0x1F) - 16) << 6;
			p.Gravity = pORecord.Gravity ? -4 : 0;
			p.Hp = pORecord.HitPoints;
			p.X = (pORecord.X << 5) + (mOWorld.Random() & 0x1F);
			p.Y = (pORecord.Y << 5) + (mOWorld.Random() & 0x1F);
			p.Z = (pORecord.ZPos << 3) + (mOWorld.Random() & 7);
			p.Speed = I16(pORecord.Speed * UWCritterRules.MomentumPerSpeedPoint);
			p.Impact = 0;
		}

		/// <summary>seg006_1477_367 (41296): the terrain flags of the 3x3 tiles around the creature
		/// at its record position, step 8, on a calc array of its own - the centre's and the
		/// corners' together. The land callback holds the drop and lava flags of the step
		/// against them: an edge the creature already stands on does not stop it.</summary>
		private int fPreState(UWCritterRecord pORecord, int piIndex, UWCommonObjectProperties.Entry pOEntry)
		{
			PreState.SetCalc(pORecord.X, pORecord.Y, pORecord.ZPos, pOEntry.Radius & 7, pOEntry.Height, piIndex);
			PreState.ProcessMotionTileHeights(8);

			return PreState.Flags | PreState.AllFlags;
		}

		/// <summary>
		/// The write-back of a creature (the mobile class-1 path of ApplyProjectileMotion, see
		/// UWMobileObjectMotion.ApplyProjectileMotion): the tile when it changed (TileChanged for
		/// the host's relink), the eighth and the coarse height; the impact above 0x100 is
		/// DamageObject with type 0 - for a creature its hit points are overwritten with the
		/// params' value right after, so nothing of it lasts (reported, not applied); lava burns
		/// 1 point of plain fire on one update in five; then the heading byte, the gravity bit,
		/// the vertical speed field (16 + vz / 64, clamped 0..31), the speed byte (speed / 0x2F)
		/// and the contact state. No fine position for a creature.
		/// </summary>
		private void fApplyMotion(UWCritterRecord pORecord, UWMotionParams p)
		{
			int liTileX = p.X >> 8;
			int liTileY = p.Y >> 8;

			if (liTileX != miHomeX || liTileY != miHomeY)
			{
				mOResult.TileChanged = true;
				mOResult.OldTileX = miHomeX;
				mOResult.OldTileY = miHomeY;
				miHomeX = liTileX;
				miHomeY = liTileY;
			}

			pORecord.ZPos = (p.Z >> 3) & 0x7F;
			pORecord.FineX = (p.X >> 5) & 7;
			pORecord.FineY = (p.Y >> 5) & 7;

			if ((p.Impact & 0xFFFF) > 0x100)
				mOResult.ImpactDamage = (p.Impact & 0xFFFF) >> 8;

			if ((p.Contact & UWMotionTables.ContactLava) != 0 && mOWorld.Random() % 5 == 0)
				mOResult.LavaBurn = true;

			pORecord.FineHeading = (p.Heading >> 8) & 0xFF;
			pORecord.TileX = miHomeX & 0x3F;
			pORecord.TileY = miHomeY & 0x3F;
			pORecord.Gravity = p.Gravity == -4;

			int liField = (p.Vz / 64) + 16;

			if (liField < 0)
				liField = 0;
			else if (liField > 31)
				liField = 31;

			pORecord.Pitch = liField;
			pORecord.Speed = (p.Speed / UWCritterRules.MomentumPerSpeedPoint) & 0x7F;
			pORecord.TileState = fContactIndex(p.Contact);
		}

		private static int fContactIndex(int piContact)
		{
			int liAt = piContact & 0xFF;

			return liAt < UWMotionTables.ContactStateIndex.Length ? UWMotionTables.ContactStateIndex[liAt] & 7 : 0;
		}

		// ------------------------------------------------- The callbacks

		/// <summary>
		/// The land creature's callback seg006_1477_431 (41379-41611). Airborne (0x1000): gravity
		/// on if it was off, interval 1, a collision, inactive - and the step goes on, so the
		/// creature falls. Water (0x10): water alone under the feet drowns it at once (the splash
		/// and the death animation are the brain's and the host's on Drowned); water with
		/// something else (an object carrying it) stops a creature without a path. A drop (0x800)
		/// or lava (0x20) that the pre-state did not already show stops a creature without a path
		/// and is nothing to one with a path. A wall or step (0x300) is noted and left to the
		/// core's deflection. An object (0x400): a closed door among the overlapping records is
		/// reported as such (seg030_2B26_170C), else the first overlapping record (seg030_2B26_17F5);
		/// either ends the step. True ends the step with the sub-step undone.
		/// </summary>
		private bool fLandCallback(ref int piFlags)
		{
			if ((piFlags & 0x1000) != 0)
			{
				if (mOParams.Gravity == 0)
					mOParams.Gravity = -4;

				mORecord.Interval = UWCritterRules.IntervalStuck;
				mOResult.Collided = true;
				mbActive = false;

				return false;
			}

			if ((piFlags & 0x10) != 0)
			{
				if (UWCritterRules.Drowns(piFlags, false, false))
				{
					mOResult.Collided = true;
					mOResult.Drowned = true;
					mbActive = false;

					return true;
				}

				if (!mORecord.HasPath)
					return fStopHere();
			}

			if ((piFlags & 0x800) != 0 && (miPreState & 0x800) == 0)
				return !mORecord.HasPath && fStopHere();

			if ((piFlags & LavaFlag) != 0 && (miPreState & LavaFlag) == 0)
				return !mORecord.HasPath && fStopHere();

			if ((piFlags & 0x300) != 0)
			{
				mOResult.Collided = true;

				return false;
			}

			if ((piFlags & 0x400) != 0)
			{
				mOResult.Collided = true;

				if (!fReportClosedDoor())
					fReportFirstOverlap();
			}

			return mOResult.Collided && mbActive;
		}

		/// <summary>The flier's callback seg006_1477_5FE (41598-41643): active again, solid or
		/// ceiling (0x200) noted for the core's deflection, the step flag (0x100) sets the
		/// ceiling mark and a vertical speed of 0x80, an object (0x400) is the first overlapping
		/// record. No water, no doors, no interval.</summary>
		private bool fFlierCallback(ref int piFlags)
		{
			mbActive = true;

			if ((piFlags & 0x200) != 0)
			{
				mOResult.Collided = true;

				return false;
			}

			if ((piFlags & 0x100) != 0)
			{
				mOParams.Vz = 0x80;
				mOResult.TouchedCeiling = true;
			}

			if ((piFlags & 0x400) != 0)
			{
				mOResult.Collided = true;
				fReportFirstOverlap();
			}

			return mOResult.Collided && mbActive;
		}

		/// <summary>The swimmer's callback seg006_1477_65E (41644-41671): a wall or step (0x300)
		/// zeroes the velocity and is left to the deflection, an object (0x400) zeroes it and is
		/// the first overlapping record, plain floor (0x8) - the bank - zeroes it and collides.
		/// The water flag never reaches it (the handler ignores 0x10).</summary>
		private bool fSwimmerCallback(ref int piFlags)
		{
			if ((piFlags & 0x300) != 0)
			{
				mOParams.Vx = 0;
				mOParams.Vy = 0;
				mOResult.Collided = true;

				return false;
			}

			if ((piFlags & 0x400) != 0)
			{
				mOParams.Vx = 0;
				mOParams.Vy = 0;
				mOResult.Collided = true;
				fReportFirstOverlap();
			}

			if ((piFlags & 8) != 0)
			{
				mOParams.Vx = 0;
				mOParams.Vy = 0;
				mOResult.Collided = true;
			}

			return mOResult.Collided && mbActive;
		}

		/// <summary>A collision that stops the step where it is: the velocity of this call
		/// zeroed (params +6/+8), RelatedToMotorCollision set, the sub-step undone.</summary>
		private bool fStopHere()
		{
			mOParams.Vx = 0;
			mOParams.Vy = 0;
			mOResult.Collided = true;

			return true;
		}

		/// <summary>seg030_2B26_170C (106351): the first closed door - class 0x14, index below 8 -
		/// among the records that overlap the creature's height, from First on. Reported with
		/// RelatedToColliding_2473.</summary>
		private bool fReportClosedDoor()
		{
			int liFirst = (sbyte)Core.First;
			int liOverlap = Core.Overlap & 0xFF;

			for (int liAt = 0; liAt < liOverlap; liAt++)
			{
				int liIndex = liFirst + liAt;

				if (liIndex < 0 || liIndex >= Core.Records.Length)
					break;

				UWMotionBody lOBody = Core.Records[liIndex].Body;
				int liItemId = lOBody.ItemId & 0x1FF;

				if ((liItemId >> 4) == 0x14 && (liItemId & 0xF) < 8)
				{
					fReportHit(lOBody, true);

					return true;
				}
			}

			return false;
		}

		/// <summary>seg030_2B26_17F5 (106531): the first overlapping record, if any, as the
		/// collision object (CollisionObject_2442 with dseg_246D).</summary>
		private void fReportFirstOverlap()
		{
			int liFirst = (sbyte)Core.First;

			if ((Core.Overlap & 0xFF) == 0 || liFirst < 0 || liFirst >= Core.Records.Length)
				return;

			fReportHit(Core.Records[liFirst].Body, false);
		}

		private void fReportHit(UWMotionBody pOBody, bool pbClosedDoor)
		{
			mOResult.HitObject = true;
			mOResult.HitObjectIndex = pOBody.Index;
			mOResult.HitObjectItemId = pOBody.ItemId & 0x1FF;
			mOResult.HitObjectIsCreature = pOBody.IsCreature;
			mOResult.HitClosedDoor = pbClosedDoor;
		}

		// ------------------------------------------------- The push

		/// <summary>
		/// A creature struck by a mover - the player walking into it, a missile, a thrown thing
		/// (seg029_29EE_3 -> ApplyProjectileMotion on the struck record): the write-back of the
		/// transferred params stores the pusher's heading in byte 9, the speed byte of the push
		/// momentum (0xEB / 0x2F = 5) and the transferred vertical speed; the position stays in
		/// its eighth, the gravity bit as it was. The creature's next update moves it that way
		/// before its mind decides anew. The player's own record is written the same way in the
		/// original, but his motion never reads it: creatures cannot shove the player.
		/// </summary>
		public static void Push(UWCritterRecord pORecord, int piHeading, int piSpeed, int piVz)
		{
			pORecord.FineHeading = (piHeading >> 8) & 0xFF;

			int liField = (piVz / 64) + 16;

			if (liField < 0)
				liField = 0;
			else if (liField > 31)
				liField = 31;

			pORecord.Pitch = liField;
			pORecord.Speed = (piSpeed / UWCritterRules.MomentumPerSpeedPoint) & 0x7F;
		}
	}
}
