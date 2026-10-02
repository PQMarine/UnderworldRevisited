using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The creature mind of UW.EXE, engine-free: one creature's update in the order of
	/// NPCInitialProcessing_seg007_2488 (Docs/AI/creature-ai.md 1.4) on its record
	/// (UWCritterRecord), its table row and what the host tells it about the world
	/// (ICritterHost). Everything the original decides on the tile grid is decided here:
	/// perception, temper, the goal state machine, the heading and speed of the next step,
	/// the animation frame, the interval. The physics - the step itself, collisions, doors'
	/// mechanics, the blow's reach scan, projectiles - stays with the host, which applies the
	/// Motion this returns and reports the outcome as a StepResult.
	///
	/// ORDER OF ONE UPDATE (spec 1.4): the step of the PREVIOUS decision has already been
	/// applied by the host; Update then takes the snapshot, culls by distance, releases a
	/// path an attack animation made obsolete, evaluates the step (stuck, drowned), runs the
	/// animation frame machine (a running swing, shot, spell or death ends the update without
	/// goal logic), otherwise NPCBehaviours: kin alarm, damage reaction, target guard, the goal
	/// routine, the turn limiter; then reschedules. So the movement a goal implies happens at
	/// the start of the NEXT due update, as in the original.
	///
	/// Every rule cites its routine and a line of UW1_asm.asm; the constants live in
	/// UWCritterRules. Where the port keeps a physics substitute (path shape, clearance
	/// probes, the goal 3 tile check) the host decides, and this only asks.
	/// </summary>
	public sealed class UWCritterBrain
	{
		/// <summary>
		/// What the body does before the next update: the step along the fine heading with
		/// the momentum of speed * 0x2F over interval * 16 PIT ticks (InitMotionParams
		/// 100347-100380, CalculateMotionTopLevel 41703), the picture (facing, slot, frame),
		/// the pitch of a flier, and when the next update is due. Immutable; the record holds
		/// the same values.
		/// </summary>
		public readonly struct Motion
		{
			/// <summary>False when the creature stands with speed 0 and level pitch
			/// (NeedsToMove_seg007_1798_26D4): no physics this interval.</summary>
			public readonly bool Moves;

			/// <summary>Byte 9, 0..255, 0 = +Y, clockwise, 64 = +X.</summary>
			public readonly int FineHeading;

			/// <summary>Word 2 bits 7-9: the picture's direction.</summary>
			public readonly int FacingEighth;

			/// <summary>Byte 0x13 bits 0-6.</summary>
			public readonly int Speed;

			/// <summary>Momentum times ticks: speed * 0x2F * interval * 16, in the stepper's
			/// sub-units of which 0x2000 make one eighth of a tile (spec 7.2). The absolute
			/// scale is to be confirmed in game; the ratio speed / 20 of the player's full
			/// walk per interval is the robust statement.</summary>
			public readonly int StepSubUnits;

			/// <summary>The same as eighths of a tile.</summary>
			public float StepEighths => StepSubUnits / (float)UWCritterRules.SubUnitsPerEighth;

			/// <summary>Byte 0x14 bits 3-7, 16 level; the vertical component is (pitch - 16) * 64.</summary>
			public readonly int Pitch;

			/// <summary>Byte 0x13 bit 7.</summary>
			public readonly bool Gravity;

			/// <summary>Byte 0x15 bits 0-5: the animation slot.</summary>
			public readonly int Animation;

			/// <summary>Word 0x0B bits 12-15: the frame to show.</summary>
			public readonly int Frame;

			/// <summary>Byte 0x14 bits 0-2: slots until the next update.</summary>
			public readonly int Interval;

			/// <summary>Byte 0x0A bits 0-3 after the reschedule.</summary>
			public readonly int DueSlot;

			/// <summary>The update returned at the distance cull: nothing but DueSlot is new.</summary>
			public readonly bool Culled;

			/// <summary>The object was freed at the end of the death animation; the host must
			/// drop it and never reschedule it.</summary>
			public readonly bool Removed;

			public Motion(UWCritterRecord pORecord, bool pbCulled, bool pbRemoved)
			{
				Moves = !(pORecord.IsStanding && pORecord.Speed == 0 && pORecord.Pitch == UWCritterRules.FlierPitchLevel);
				FineHeading = pORecord.FineHeading;
				FacingEighth = pORecord.FacingEighth;
				Speed = pORecord.Speed;
				StepSubUnits = pORecord.Speed * UWCritterRules.MomentumPerSpeedPoint
					* pORecord.Interval * UWCritterRules.StepTicksPerInterval;
				Pitch = pORecord.Pitch;
				Gravity = pORecord.Gravity;
				Animation = pORecord.Animation;
				Frame = pORecord.Frame;
				Interval = pORecord.Interval;
				DueSlot = pORecord.DueSlot;
				Culled = pbCulled;
				Removed = pbRemoved;
			}
		}

		/// <summary>The search results of SearchForGoalTarget_seg007_1798_1CEB.</summary>
		public const int SearchFound = 0;

		public const int SearchLost = 1;

		public const int SearchUnchanged = 2;

		/// <summary>The animation slots the original's routines write (spec 2.1, byte 0x15).</summary>
		public const int AnimCombatIdle = 0x00;

		public const int AnimBash = 0x01;

		public const int AnimMissile = 0x05;

		public const int AnimBackOff = 0x07;

		public const int AnimDying = 0x0C;

		public const int AnimSpell = 0x0D;

		public const int AnimStanding = 0x20;

		public const int AnimWalking = 0x2C;

		/// <summary>The player's object index and item id.</summary>
		public const int PlayerIndex = 1;

		public const int PlayerItemId = 0x7F;

		private readonly UWCritterRecord mORecord;

		private readonly UWObjectClassProperties.Critter mORow;

		private readonly int miIndex;

		public UWCritterRecord Record => mORecord;

		public int Index => miIndex;

		// ------------------------------------------------- per-update scratch

		private ICritterHost mIHost;

		private StepResult mOStep;

		/// <summary>IsNPCActive_dseg_5c99_2440: the step succeeded (not stuck, not drowning).</summary>
		private bool mbActive;

		/// <summary>RelatedToMotorCollision_2452, cleared by the flier's door duck.</summary>
		private bool mbCollided;

		/// <summary>FlyingPitchingRelated_244A: a flier ducked under a door this update.</summary>
		private bool mbFlierDucked;

		/// <summary>The snapshot of step 7: fine heading, full facing and speed after the physics.</summary>
		private int miFineHeadingBefore;

		private int miFullFacingBefore;

		private int miSpeedBefore;

		/// <summary>GetDistancesToGTarg_seg007_326B: the target and the vectors to it.</summary>
		private CritterTarget mOTarget;

		private bool mbTargetFound;

		private int miDx;

		private int miDy;

		private int miTileDist2;

		private int miEighthDist2;

		/// <summary>Guard against the mutual recursion of NPCWanderUpdate and StandStillGoal
		/// running away on a scripted RNG that keeps answering 1.</summary>
		private int miWanderDepth;

		private bool mbRemoved;

		public UWCritterBrain(UWCritterRecord pORecord, UWObjectClassProperties.Critter pORow, int piIndex)
		{
			mORecord = pORecord;
			mORow = pORow;
			miIndex = piIndex;
		}

		private int fRandom(int piN)
		{
			return piN <= 1 ? 0 : mIHost.Random(piN);
		}

		private bool fIsFlier => mORow.IsFlier;

		private int fWanderSpeed => mORow.WanderSpeed & 0x7F;

		private int fPursuitSpeed => mORow.MovementSpeed & 0x7F;

		// ------------------------------------------------- the update

		/// <summary>
		/// One creature update (NPCInitialProcessing_seg007_2488, 52586-53399). The host has
		/// applied the previous Motion and filled LastStep. Returns the Motion for the next
		/// interval; Culled and Removed say when it carries nothing.
		/// </summary>
		public Motion Update(ICritterHost pIHost)
		{
			mIHost = pIHost;
			mOStep = pIHost.LastStep;
			mbRemoved = false;
			mbTargetFound = false;
			miWanderDepth = 0;

			// 2. The distance cull (52640-52720, label seg007_1798_25BC): far from the player AND
			// from the view (UWCritterRules.IsFarFromBoth) and not following, the creature is
			// frozen and looked at again half a second later.
			CritterTarget lOPlayer = pIHost.GetObject(PlayerIndex);

			if (lOPlayer.Exists && mORecord.Goal != 3)
			{
				UWTilePos lOView = pIHost.ViewTile;

				if (UWCritterRules.IsFarFromBoth(mORecord.TileX - lOPlayer.TileX, mORecord.TileY - lOPlayer.TileY,
					mORecord.TileX - lOView.X, mORecord.TileY - lOView.Y))
				{
					mORecord.DueSlot = UWCritterClock.FarSlot(mORecord.DueSlot);

					return new Motion(mORecord, true, false);
				}
			}

			// 4. Per-update globals (52787).
			mbActive = true;
			mbFlierDucked = false;
			mbCollided = mOStep.Collided;

			// 5. Path bookkeeping (52800-52825): an animation that is neither walk nor stand
			// drops the path.
			if (mORecord.Animation != AnimWalking && mORecord.Animation != AnimStanding && mORecord.HasPath)
				mORecord.HasPath = false;

			// 6. The step's verdict (seg006_1477_431, 41381-41460): stuck sets interval 1 and
			// clears IsNPCActive; drowning sets the death animation at frame 3.
			if (mOStep.Stuck)
			{
				mbActive = false;
				mORecord.Interval = UWCritterRules.IntervalStuck;
			}

			// DROWNING (seg006_1477_476, 41417-41460): the land creature's motion callback
			// clears IsNPCActive, spawns the splash and writes byte 0x15 bits 0-5 = 0x0C
			// (41445-41448), word 0x0B bits 12-15 = 3 (41450-41453) and byte 0x14 bits 0-2 = 1
			// (41455-41458) straight into the object. Those writes happen DURING the physics,
			// which is step 6 of the SAME update - so the animation machine of THIS update
			// (step 9) already finds animation 0x0C at frame 3 and removes the creature
			// (53018-53090). NOTHING OF THE DEATH PICTURE IS EVER DRAWN: the user watched for
			// it in the original and saw only the splash (2026-09-20). We therefore do the
			// bookkeeping here and fall through to the animation machine instead of waiting an
			// interval. Already dying is skipped - a second drowning report must not restart
			// the death.
			if (mOStep.Drowned && mORecord.Animation != AnimDying)
			{
				mbActive = false;
				mORecord.Animation = AnimDying;
				mORecord.Frame = UWCritterRules.DeathRemovalFrame;
				mORecord.Interval = UWCritterRules.IntervalStuck;
				mORecord.HitPoints = 0;
				pIHost.OnDrowned();
			}

			// 7. The snapshot after the physics (52900-52985).
			miFineHeadingBefore = mORecord.FineHeading;
			miFullFacingBefore = mORecord.FullFacing;
			miSpeedBefore = mORecord.Speed;

			// 8, 9. Goals 3 and 11 skip the animation machine (label seg007_1798_2849).
			if (mORecord.Goal == 3 || mORecord.Goal == 11)
				fBehaviours();
			else
				fAnimationMachine();

			if (mbRemoved)
				return new Motion(mORecord, false, true);

			// 10. Reschedule (53373).
			mORecord.DueSlot = UWCritterClock.NextSlot(mORecord.DueSlot, mORecord.Interval);

			return new Motion(mORecord, false, false);
		}

		/// <summary>The animation frame machine (52990-53355): each attack branch ends the
		/// update without the goal routine.</summary>
		private void fAnimationMachine()
		{
			int liAnim = mORecord.Animation;
			int liFrame = mORecord.Frame;

			if (liAnim == AnimDying)
			{
				// Frame 3: SpecialDeathCases(obj, 1), unlink, loot, remains, inventory, free the
				// object (label seg007_1798_2875, 53030-53090).
				if (liFrame == UWCritterRules.DeathRemovalFrame)
				{
					mIHost.OnDeathFinished();
					mbRemoved = true;

					return;
				}

				mORecord.Frame = (liFrame + 1) & 0xF;

				return;
			}

			if (liAnim >= 1 && liAnim <= 3)
			{
				// Frame 0 against the player starts the combat music (53136-53160); frame 4 is
				// the blow (seg007_1798_29CF, seg007_1798_2A29, 53040-53080). The swing-started
				// event fires here and not in ChooseMeleeAttackToMake, because a swing rolled
				// at 65..100 squared eighths is overwritten by NPC_Goto's walk in the same
				// update (spec 6.5) and only one that reaches this machine is real.
				if (liFrame == 0)
				{
					mIHost.OnSwingStarted(liAnim - 1);

					if (mORecord.GTarg == PlayerIndex)
						mIHost.PlayMusic(UWCritterRules.CombatTheme);
				}

				if (liFrame == UWCritterRules.MeleeHitFrame)
				{
					fExecuteAttack(liAnim - 1);
					mORecord.Animation = AnimCombatIdle;
					mORecord.Frame = 0;
					mORecord.ChargeIndex = 0;
				}
				else
					mORecord.Frame = (liFrame + 1) & 0xF;

				return;
			}

			if (liAnim == AnimSpell && mORecord.SpellSlot != 0)
			{
				// Frame 4: pitch to the target, SpellTrapWandCast(table byte 0x29 + slot)
				// (seg007_1798_2AAD, 53276).
				if (liFrame == UWCritterRules.MeleeHitFrame)
				{
					fGetDistancesToGTarg();

					int liPitch = fPitchToTarget(0x1E, false);

					mIHost.OnSpellCast(mORow.SpellOfSlot(mORecord.SpellSlot), liPitch);
					mORecord.Animation = AnimCombatIdle;
					mORecord.Frame = 0;
					mORecord.SpellSlot = 0;
				}
				else
					mORecord.Frame = (liFrame + 1) & 0xF;

				return;
			}

			if (liAnim == AnimMissile)
			{
				// Frame 4: NPCMissileLaunch with the ammunition of table byte 0x20 bits 1-4
				// (seg007_1798_2B63).
				if (liFrame == UWCritterRules.MeleeHitFrame)
				{
					fGetDistancesToGTarg();

					int liAmmo = mORow.AmmunitionIndex;
					int liPitch = fPitchToTarget(mIHost.MissileVelocity(liAmmo), true);

					mIHost.OnMissileLaunched(liAmmo, liPitch);
					mORecord.Animation = AnimCombatIdle;
					mORecord.Frame = 0;
				}
				else
					mORecord.Frame = (liFrame + 1) & 0xF;

				return;
			}

			fBehaviours();
		}

		/// <summary>NPCExecuteAttack_seg022_15DE at frame 4 (83437): the host runs the reach
		/// scan and the skill check; this hands it the swing type RNG(9), the charge table
		/// value and the flanking bonus of the two facings (CalcFlankingBonus, 81871).</summary>
		private void fExecuteAttack(int piAttack)
		{
			fGetDistancesToGTarg();

			int liCharge = UWCritterRules.ChargeTable[mORecord.ChargeIndex & 0xF];
			int liSwing = fRandom(UWCritterRules.SwingTypeRoll);
			int liFlank = mbTargetFound
				? UWCritterRules.GetFlankingBonus(mORecord.FacingEighth, mOTarget.FacingEighth)
				: 0;

			mIHost.OnBlow(mORecord.GTarg, piAttack, liCharge, liSwing, liFlank);
		}

		// ------------------------------------------------- NPCBehaviours_seg007_1798_2C4A (53405-54064)

		private void fBehaviours()
		{
			mORecord.NewDestination = false;
			mORecord.IsStanding = false;

			if (mORecord.Goal != 11)
			{
				fFootsteps();

				// Passive kinds (table byte 0x0A bit 1) skip the alarm and the reaction (53508-53516).
				if (!mORow.IsPassive)
				{
					fKinAlarm();
					fDamageReaction();
				}
			}

			fSwitchGoals();
			fTurnLimiter();
		}

		/// <summary>The footstep sound of a walking creature on odd frames, by the category of
		/// table byte 0x10 (53431-53500).</summary>
		private void fFootsteps()
		{
			if (mORecord.Animation != AnimWalking || (mORecord.Frame & 1) == 0)
				return;

			switch (mORow.FootstepCategory)
			{
				case 1:
					mIHost.PlaySound(mORecord.Frame == 1 ? 1 : 2);
					break;

				case 2:
					mIHost.PlaySound(0x17);
					break;

				case 3:
					mIHost.PlaySound(5);
					break;

				case 4:
					mIHost.PlaySound(0x0E);
					break;

				case 5:
					mIHost.PlaySound(0x0D);
					break;
			}
		}

		/// <summary>The kin alarm (53514-53600, spec 4.2): same kind or an ally, within the
		/// hearing range in Manhattan tiles, within 0x200 PIT ticks of the player's blow.</summary>
		private void fKinAlarm()
		{
			UWCritterAlarm lOAlarm = mIHost.Alarm;

			if (lOAlarm == null)
				return;

			bool lbAlly = mORecord.IsAlly;
			bool lbKin = !lbAlly && miIndex != lOAlarm.Index && mORow.GeneralType == lOAlarm.Kind
				&& !mORecord.AttitudeLocked;

			if (!lbKin && !lbAlly)
				return;

			if (!lOAlarm.IsFresh(mIHost.Clock))
				return;

			int liManhattan = Math.Abs(mORecord.TileX - lOAlarm.TileX) + Math.Abs(mORecord.TileY - lOAlarm.TileY);

			if (liManhattan >= mORow.NoiseRange)
				return;

			mORecord.Attitude = UWNpc.AttitudeHostile;
			mORecord.TargetConfirmed = true;

			if (mORecord.Goal != 6 && mORecord.Goal != 9)
			{
				fSetNewGoal(5, lbAlly ? lOAlarm.Index : PlayerIndex);
				mORecord.SetDestination(lOAlarm.TileX, lOAlarm.TileY, lOAlarm.FloorLevel);
			}
		}

		/// <summary>
		/// The damage reaction (53603-53866, spec 4.3, decision 3): gated on byte 0x12; only
		/// the player's blows and those of or against allies provoke. A far hit that cannot be
		/// answered with magic sets the relentless bit and goal 5 without a roll; the bits 5
		/// and 4 decide the next hits; a close hit rolls the morale check once.
		/// </summary>
		private void fDamageReaction()
		{
			int liAttacker = mORecord.Attacker;

			if (liAttacker == 0)
				return;

			bool lbReact = liAttacker == PlayerIndex || mORecord.IsAlly;

			if (!lbReact)
				lbReact = mIHost.GetObject(liAttacker).IsAlly;

			if (!lbReact)
				return;

			if (mORecord.GTarg != liAttacker)
				mORecord.GTarg = liAttacker;

			// A dead attacker ends the reaction (GetDistancesToGTarg returns 0, jump to 3093).
			if (!fGetDistancesToGTarg())
			{
				mORecord.Attacker = 0;
				mORecord.DamageTaken = 0;

				return;
			}

			if (liAttacker == PlayerIndex)
			{
				mORecord.Attitude = UWNpc.AttitudeHostile;
				mORecord.SetDestination(mOTarget.TileX, mOTarget.TileY, mOTarget.FloorLevel);
				mORecord.TargetConfirmed = true;
			}

			int liGoal;

			// Label seg007_1798_2F8C, 53775-53830.
			if (miTileDist2 > UWCritterRules.FollowNearTilesSquared
				&& (!mORow.IsCaster || mIHost.IsMagicBlockedAt(mORecord.TileX, mORecord.TileY)))
			{
				mORecord.Relentless = true;
				liGoal = 5;
			}
			else if (mORecord.Relentless)
				liGoal = 5;
			else if (mORecord.MadeStand)
				liGoal = 9;
			else
				liGoal = fMoraleCheck() ? 6 : 5;

			fSetNewGoal(liGoal, liAttacker);
			mORecord.Attacker = 0;
			mORecord.DamageTaken = 0;
		}

		/// <summary>seg007_1798_3383 (54196-54277): RNG(4) is rolled only when the early exits
		/// did not decide, so the RNG stream stays the original's.</summary>
		private bool fMoraleCheck()
		{
			int liVitality = mORow.Vitality;
			int liHp = mORecord.HitPoints;
			int liMorale = mORow.Morale;
			int liDamage = mORecord.DamageTaken;
			bool lbResult;

			if (UWCritterRules.MoraleCheckDecided(liVitality, liHp, liDamage, out lbResult))
				return lbResult;

			return UWCritterRules.MoraleCheck(liVitality, liHp, liMorale, liDamage, fRandom(UWCritterRules.MoraleRoll));
		}

		/// <summary>The goal switch (SwitchGoals_seg007_1798_3247, 54066) with the target
		/// validity guard for goals 3, 5, 6, 9 (53880-53960, 54108).</summary>
		private void fSwitchGoals()
		{
			int liGoal = mORecord.Goal;

			if (liGoal == 3 || liGoal == 5 || liGoal == 6 || liGoal == 9)
			{
				if (!mbTargetFound && !fGetDistancesToGTarg())
				{
					fResetGoal();

					return;
				}
			}

			switch (liGoal)
			{
				case 0:
				case 4:
				case 7:
					fStandStillGoal();
					break;

				case 1:
					fGoto(mORecord.HomeX, mORecord.HomeY, mIHost.FloorLevelAt(mORecord.HomeX, mORecord.HomeY));
					break;

				case 2:
					fWanderUpdate();
					break;

				case 3:
					fGoal3();
					break;

				case 5:
					fGoal5();
					break;

				case 6:
					fGoal6();
					break;

				case 8:
					fGoal8();
					break;

				case 9:
					fGoal9();
					break;

				case 10:
					fGoal10();
					break;

				case 11:
					fGoal11();
					break;

				case 12:
					fGoal12();
					break;

				default:
					// Goals 13-15: interval 7 and nothing else (54050-54056).
					mORecord.Interval = UWCritterRules.IntervalUnknownGoal;
					break;
			}
		}

		// ------------------------------------------------- goal writers with a trace

		private void fSetNewGoal(int piGoal, int piGTarg)
		{
			int liOld = mORecord.Goal;

			mORecord.SetNewGoal(piGoal, piGTarg);
			mIHost.OnGoalChanged(liOld, piGoal, piGTarg);
		}

		private void fResetGoal()
		{
			int liOld = mORecord.Goal;

			mORecord.ResetGoal();
			mIHost.OnGoalChanged(liOld, mORecord.Goal, mORecord.GTarg);
		}

		// ------------------------------------------------- GetDistancesToGTarg_seg007_326B (54088-54190)

		/// <summary>The target and the vectors to it in eighths and tiles. False (the
		/// original's 0) when the target does not exist or has no hit points.</summary>
		private bool fGetDistancesToGTarg()
		{
			// DEVIATION 63 (see Docs/AI/creature-ai.md): a follower (goal 3) without a target
			// follows the player. The level data of the Void gives the Slasher of Veils goal 3
			// and target 0; GetDistancesToGTarg_seg007_326B does not check it - LookupObjectByIndex
			// returns a null pointer and the original reads its target out of the interrupt
			// table at 0000:0000 - yet in the original he follows the player (per user,
			// 2026-09-14 and 2026-09-29). Ours took target 0 as none and let him wander.
			int liTarget = mORecord.GTarg == 0 && mORecord.Goal == 3 ? PlayerIndex : mORecord.GTarg;

			mOTarget = mIHost.GetObject(liTarget);
			mbTargetFound = mOTarget.Exists && mOTarget.HitPoints != 0;

			if (!mbTargetFound)
				return false;

			miDx = mOTarget.X - mORecord.X;
			miDy = mOTarget.Y - mORecord.Y;
			miEighthDist2 = (miDx * miDx) + (miDy * miDy);

			int liTx = mOTarget.TileX - mORecord.TileX;
			int liTy = mOTarget.TileY - mORecord.TileY;

			miTileDist2 = (liTx * liTx) + (liTy * liTy);

			return true;
		}

		private bool fOnTargetTile => mOTarget.TileX == mORecord.TileX && mOTarget.TileY == mORecord.TileY;

		private int fFloorDifference => Math.Abs(mOTarget.FloorLevel - mORecord.FloorLevel);

		// ------------------------------------------------- Perception (spec 3)

		/// <summary>
		/// SearchForGoalTarget_seg007_1798_1CEB (51655-51914): heard within hear2 / 4 (strict),
		/// seen within see^2 when the target's eighth is the facing or a neighbour and the
		/// sight line is clear, unchanged within hear2 * 4, else lost. The target's tile goes
		/// out; bit 0 is written on seen and lost only.
		///
		/// THE TWO NIBBLES the host hands in are the original's row byte 0x1D: loudness low,
		/// visibility high, both from UWPlayerVitals. Complete since 2026-09-21 - the speed
		/// term while moving, the stealth spells on both bases
		/// (ApplyDefenceStealthBonuses_ovr133_65D) and the visibility 15 - Sneak / 5; since the
		/// same day also the easy-move base of four for an EASY MOVEMENT step - the three arrows
		/// under the compass (UWPlayerVitals.ReportEasyMovementStep). The decay runs as in the
		/// original, one step per eight calls; only the call rate is ours - the original calls it
		/// once per frame of its game loop, whose rate depends on the machine (deviation 24, see
		/// UWPlayerVitals.QuietnessTicksPerSecond).
		///
		/// THE LIGHT LEVEL does not belong here: it goes from PLAYER.DAT byte 0x63 to the
		/// renderer's shading table and nowhere near this check (read 2026-09-21).
		/// </summary>
		private int fSearchForTarget(out int piTx, out int piTy)
		{
			piTx = mOTarget.TileX;
			piTy = mOTarget.TileY;

			int liDx = mOTarget.TileX - mORecord.TileX;
			int liDy = mOTarget.TileY - mORecord.TileY;
			int liD2 = (liDx * liDx) + (liDy * liDy);

			int liHear = mOTarget.Loudness * mORow.NoiseRange / 16;
			int liHear2 = liHear * liHear;

			if (liHear2 / UWCritterRules.HearingDivisor > liD2)
				return SearchFound;

			int liSee = mOTarget.Visibility * mORow.SightRange / 16;

			if (liD2 <= liSee * liSee)
			{
				int liRelative = (UWCritterRules.GetVectorHeading(liDx, liDy) - mORecord.FacingEighth + 8) % 8;

				if ((liRelative == 0 || liRelative == 1 || liRelative == 7)
					&& mIHost.HasLineOfSight(mORecord.X, mORecord.Y, mORecord.ZPos + mIHost.OwnObjectHeight,
						mOTarget.X, mOTarget.Y, mOTarget.ZPos + mOTarget.ObjectHeight))
				{
					mORecord.TargetConfirmed = true;

					return SearchFound;
				}
			}

			if (liHear2 * UWCritterRules.LostMultiplier > liD2)
				return SearchUnchanged;

			mORecord.TargetConfirmed = false;

			return SearchLost;
		}

		/// <summary>TurnTowardsTarget_seg007_1798_1F1A (51920-52010): argument 0 turns the
		/// facing eighth one step the shorter way and reports facing; argument 1 is the fine
		/// turn of shots and spells (seg007_1798_2123).</summary>
		private bool fTurnTowardsTarget(bool pbFine)
		{
			if (pbFine)
				return fFineTurnTowards(miDx, miDy);

			int liRelative = (UWCritterRules.GetVectorHeading(miDx, miDy) - mORecord.FacingEighth + 8) % 8;

			if (liRelative == 0)
				return true;

			mORecord.FacingEighth = (mORecord.FacingEighth + (liRelative <= 4 ? 1 : -1)) & 7;

			return false;
		}

		/// <summary>seg007_1798_2123 (52174-52376): the full facing snaps to the exact heading
		/// of the vector when within 0x20 of it (and reports facing), else turns 0x20 the
		/// shorter way. A zero vector counts as facing.</summary>
		private bool fFineTurnTowards(int piDx, int piDy)
		{
			if (piDx == 0 && piDy == 0)
				return true;

			int liCurrent = mORecord.FullFacing;
			int liWanted = FineHeadingOf(piDx, piDy);
			int liD = (liWanted - liCurrent) & 0xFF;

			if (liD < UWCritterRules.TurnLimitPerUpdate || liD > 0x100 - UWCritterRules.TurnLimitPerUpdate)
			{
				mORecord.SetFullFacing(liWanted);

				return true;
			}

			mORecord.SetFullFacing((liCurrent + (liD < 0x80 ? UWCritterRules.TurnLimitPerUpdate : -UWCritterRules.TurnLimitPerUpdate)) & 0xFF);

			return false;
		}

		/// <summary>The fine heading of a vector, 0..255 with 0 = +Y and 64 = +X - the
		/// original's table arctangent (seg019_EFB) replaced by Math.Atan2, rounded.</summary>
		public static int FineHeadingOf(int piDx, int piDy)
		{
			return (int)Math.Round(Math.Atan2(piDx, piDy) * 128.0 / Math.PI) & 0xFF;
		}

		/// <summary>ReactToPlayerPresence_seg007_1AF6 (51383-51507) at the end of a wander
		/// step: standing or facing an armed player, it looks at him within 0x90 squared eighths.</summary>
		private void fReactToPlayerPresence()
		{
			CritterTarget lOPlayer = mIHost.GetObject(PlayerIndex);

			if (mORecord.Speed != 0 && !lOPlayer.WeaponDrawn)
				return;

			mORecord.GTarg = PlayerIndex;

			if (!fGetDistancesToGTarg())
				return;

			if (miEighthDist2 >= UWCritterRules.ReactToPlayerDistanceSquared)
				return;

			int liHeading = UWCritterRules.GetVectorHeading(miDx, miDy);

			mORecord.Speed = 0;
			mORecord.Animation = AnimStanding;
			mORecord.Interval = UWCritterRules.IntervalStanding;

			if (fRandom(2) != 0)
				mORecord.AdvancePhase();

			mORecord.SetFacing(liHeading);
		}

		/// <summary>
		/// MaybeVectorsToPlayer_seg007_1798_181D (50985-51156): a heading is bent away from
		/// the player when he is within the distance (squared compare, strict). Within 90
		/// degrees of "away" it is kept; between 0x40 and 0x60 from it the result is away -
		/// 0x20, up to 0x80 the heading + 0x20, up to 0xA0 away + 0x20, else the heading - 0x20.
		/// </summary>
		private int fMaybeVectorsToPlayer(int piHeading, int piDistance)
		{
			CritterTarget lOPlayer = mIHost.GetObject(PlayerIndex);

			if (!lOPlayer.Exists)
				return piHeading;

			int liDx = lOPlayer.X - mORecord.X;
			int liDy = lOPlayer.Y - mORecord.Y;
			int liD2 = (liDx * liDx) + (liDy * liDy);

			if (piDistance * piDistance <= liD2)
				return piHeading;

			int liAway = ((UWCritterRules.GetVectorHeading(liDx, liDy) + 4) & 7) << 5;
			int liD = (liAway + 0x100 - piHeading) & 0xFF;

			if (liD < 0x40 || liD > 0xC0)
				return piHeading;

			if (liD < 0x60)
				return (liAway - 0x20) & 0xFF;

			if (liD < 0x80)
				return (piHeading + 0x20) & 0xFF;

			if (liD <= 0xA0)
				return (liAway + 0x20) & 0xFF;

			return (piHeading - 0x20) & 0xFF;
		}

		// ------------------------------------------------- Goals 0, 4, 7, 8 (spec 6.1)

		/// <summary>StandStillGoal_seg007_1798_6B0 (48640-48864): a hostile one turns towards
		/// noises and searches with its initiative; then the pose of its goal.</summary>
		private void fStandStillGoal()
		{
			if (!mbActive)
				return;

			if (mORecord.Attitude == UWNpc.AttitudeHostile)
			{
				mORecord.GTarg = PlayerIndex;

				if (fGetDistancesToGTarg())
				{
					if (mORecord.TargetConfirmed)
					{
						fSetNewGoal(5, PlayerIndex);

						return;
					}

					if (mORecord.HeardSomething)
					{
						// Note the two rolls differ: <= for the turn, < for the search (48696, 48713).
						if (fRandom(UWCritterRules.InitiativeRoll) <= mORow.Initiative)
							fTurnTowardsTarget(false);
						else
							mORecord.HeardSomething = false;
					}

					if (fRandom(UWCritterRules.InitiativeRoll) < mORow.Initiative)
					{
						int liTx, liTy;
						int liResult = fSearchForTarget(out liTx, out liTy);

						if (liResult == SearchFound)
						{
							mORecord.TargetConfirmed = true;
							mORecord.SetDestination(liTx, liTy, mOTarget.FloorLevel);
							fSetNewGoal(5, PlayerIndex);

							return;
						}

						if (liResult == SearchUnchanged)
						{
							mORecord.HeardSomething = true;

							// Walk over and look (48760-48790).
							if (fRandom(UWCritterRules.UnchangedCoin) == 0)
							{
								fGoto(liTx, liTy, mOTarget.FloorLevel);

								return;
							}
						}
					}
				}
			}

			switch (mORecord.Goal)
			{
				case 2:
					fWanderUpdate();
					break;

				case 4:
					fGoal8();
					break;

				default:
					fStandPose();
					break;
			}
		}

		/// <summary>The stand pose: interval 6, speed 0, animation 0x20, standing bit, frame++
		/// with probability 1/2 (48130-48214, 48819-48836).</summary>
		private void fStandPose()
		{
			mORecord.Interval = UWCritterRules.IntervalStanding;
			mORecord.Speed = 0;
			mORecord.Animation = AnimStanding;
			mORecord.IsStanding = true;

			if (fRandom(2) != 0)
				mORecord.AdvancePhase();
		}

		/// <summary>Goal8_seg007_1798_5DF (48522-48634) for goals 4 and 8: hostile becomes 4;
		/// beyond the travel range it walks home, else it wanders.</summary>
		private void fGoal8()
		{
			if (!mbActive)
				return;

			if (mORecord.Attitude == UWNpc.AttitudeHostile && mORecord.Goal != 4)
			{
				fSetNewGoal(4, PlayerIndex);

				return;
			}

			int liDx = mORecord.HomeX - mORecord.TileX;
			int liDy = mORecord.HomeY - mORecord.TileY;
			int liRange = mORow.TravelRange;

			if ((liDx * liDx) + (liDy * liDy) > UWCritterRules.HomeRangeFactor * liRange * liRange)
				fGoto(mORecord.HomeX, mORecord.HomeY, mIHost.FloorLevelAt(mORecord.HomeX, mORecord.HomeY));
			else
				fWanderUpdate();
		}

		// ------------------------------------------------- Goal 2 (spec 6.3)

		/// <summary>NPCWanderUpdate_seg007_1798_1 (47653-48272): stop-and-go by restlessness,
		/// switched only at frame 3; a hostile one runs StandStillGoal on half its updates.</summary>
		private void fWanderUpdate()
		{
			if (mORecord.HasPath)
				mORecord.HasPath = false;

			if (!mbActive)
			{
				mORecord.Interval = UWCritterRules.IntervalStuck;

				return;
			}

			// [47781] The mutual recursion with StandStillGoal is the original's; the depth
			// guard only stops a scripted RNG that never answers 0.
			if (mORecord.Attitude == UWNpc.AttitudeHostile && miWanderDepth < 8
				&& fRandom(UWCritterRules.HostileStandChance) == 1)
			{
				miWanderDepth++;
				fStandStillGoal();

				return;
			}

			if (fIsFlier)
			{
				// [47817-47868]
				int liFloor = mIHost.FloorLevelAt(mORecord.TileX, mORecord.TileY);

				if (liFloor > 14)
					mORecord.Pitch = UWCritterRules.FlierPitchDown + fRandom(3);
				else if (mORecord.FloorLevel < liFloor + 2)
					mORecord.Pitch = UWCritterRules.FlierPitchLevel + fRandom(3);
				else
					mORecord.Pitch = UWCritterRules.FlierPitchDown + fRandom(5);
			}

			int liRestlessness = mORow.Restlessness;

			if (mORecord.Animation == AnimStanding)
			{
				if (fRandom(UWCritterRules.RestlessnessRoll) < liRestlessness && mORecord.Frame == 3)
					mORecord.Animation = AnimWalking;
			}
			else
			{
				if (!(liRestlessness >= fRandom(UWCritterRules.RestlessnessRoll)) && mORecord.Frame == 3)
					mORecord.Animation = AnimStanding;
			}

			bool lbWalking = mORecord.Animation != AnimStanding;

			if (lbWalking)
			{
				// A bump with unchanged heading: a quarter turn, no step, no frame advance
				// (47988-48000).
				if (mbCollided && !mOStep.HeadingDeflected)
				{
					mORecord.FineHeading = (mORecord.FineHeading
						+ (fRandom(2) != 0 ? UWCritterRules.QuarterTurn : -UWCritterRules.QuarterTurn)) & 0xFF;
					mORecord.SetFullFacing(mORecord.FineHeading);
					mORecord.Speed = 0;

					return;
				}

				if (fRandom(UWCritterRules.WanderDeflectRoll) < liRestlessness + UWCritterRules.WanderDeflectBonus)
					mORecord.FineHeading = (mORecord.FineHeading + fRandom(2 * UWCritterRules.DeflectionHalfRange)
						- UWCritterRules.DeflectionHalfRange) & 0xFF;

				if (!mOStep.HeadingDeflected)
					mORecord.FineHeading = fMaybeVectorsToPlayer(mORecord.FineHeading, UWCritterRules.WanderAvoidPlayerEighths);
			}
			else
			{
				if (fRandom(UWCritterRules.IdleTurnRoll) < liRestlessness)
					mORecord.FineHeading = (mORecord.FineHeading + fRandom(2 * UWCritterRules.DeflectionHalfRange)
						- UWCritterRules.DeflectionHalfRange) & 0xFF;
			}

			// THE PICTURE LOOKS WHERE THE FEET GO: every path of the wander step ends at label
			// seg007_1798_2E9 (48144-48182), which writes the fine heading AND the full facing
			// (eighth and residual) from it, deflected or not. Without this the wanderer walked
			// sideways (per user, 2026-09-20, first test of the new brain).
			mORecord.SetFullFacing(mORecord.FineHeading);

			if (!lbWalking)
				fStandPose();
			else
			{
				// [48233-48249]
				mORecord.IsStanding = false;
				mORecord.Speed = fWanderSpeed;
				mORecord.Interval = UWCritterRules.IntervalWalking;
				mORecord.AdvancePhase();
			}

			fReactToPlayerPresence();
		}

		// ------------------------------------------------- Goal 3 (spec 6.4)

		/// <summary>seg007_1798_3E4 (48278-48516): the follower swings for show within 2
		/// tiles^2, teleports beyond 64 to four tiles short of the target, walks between.</summary>
		private void fGoal3()
		{
			if (miTileDist2 <= UWCritterRules.FollowNearTilesSquared)
			{
				if (!mbActive)
					return;

				mORecord.Animation = AnimBash;
				mORecord.AdvancePhase();
				mORecord.Speed = 0;
				mORecord.SetFacing(UWCritterRules.GetVectorHeading(miDx, miDy));
				mORecord.Interval = UWCritterRules.IntervalWalking;

				return;
			}

			if (miTileDist2 > UWCritterRules.FollowTeleportTilesSquared)
			{
				// [48458-48471] four tiles short of the target on the line towards the follower.
				int liRoot = IntSqrt(miTileDist2);
				int liX = mOTarget.TileX + ((mORecord.TileX - mOTarget.TileX) * UWCritterRules.FollowTeleportShortTiles / liRoot);
				int liY = mOTarget.TileY + ((mORecord.TileY - mOTarget.TileY) * UWCritterRules.FollowTeleportShortTiles / liRoot);

				if (mIHost.Teleport(liX, liY))
				{
					mORecord.TileX = liX;
					mORecord.TileY = liY;
					mORecord.FineX = 4;
					mORecord.FineY = 4;
					mORecord.ZPos = mIHost.FloorLevelAt(liX, liY) << 3;
				}

				return;
			}

			if (mbActive)
				fGoto(mOTarget.TileX, mOTarget.TileY, mOTarget.FloorLevel);
		}

		// ------------------------------------------------- Goal 5 (spec 6.5)

		/// <summary>NPC_Goal5_Attack_seg007_1798_891 (48870-49198): melee, missile or magic,
		/// the leash, then AttackGoalSearchForTarget.</summary>
		private void fGoal5()
		{
			if (!mbActive)
				return;

			int liStandoff = UWCritterRules.CasterStandTiles;

			if (mIHost.TybalOrbStands && mORow.GeneralType == UWCritterRules.OrbMageKind)
				liStandoff = UWCritterRules.OrbMageStandTiles;

			int liD2 = miEighthDist2;
			int liHomeDx = mORecord.HomeX - mORecord.TileX;
			int liHomeDy = mORecord.HomeY - mORecord.TileY;
			int liD2Home = (liHomeDx * liHomeDx) + (liHomeDy * liHomeDy);

			if (mORecord.GTarg == PlayerIndex)
				mORecord.Attitude = UWNpc.AttitudeHostile;

			bool lbStarted = false;

			if ((liD2 < UWCritterRules.MeleeDistanceSquared || fOnTargetTile)
				&& (fFloorDifference < UWCritterRules.MeleeHeightLevels || fIsFlier))
			{
				fChooseMeleeAttack(liD2);
			}
			else if (mORow.SpellPower > 0)
			{
				if (!fTryMagicAttack() && mORow.IsCaster)
					lbStarted = fStartMagicAttack();
			}
			else if (mORow.HasMissileWeapon)
				lbStarted = fTryMissile();

			if (lbStarted)
			{
				// Sight and facing were given: an attack runs, or the creature stands this
				// update (49045-49097).
				int liAnim = mORecord.Animation;

				if (liAnim == AnimMissile || liAnim == AnimSpell || liAnim == AnimBash)
					return;

				mORecord.Animation = AnimCombatIdle;
				mORecord.Interval = UWCritterRules.IntervalWalking;
				mORecord.AdvancePhase();
				mORecord.Speed = 0;

				return;
			}

			// The leash (49129-49172).
			int liRange = mORow.TravelRange;

			if (liD2 > UWCritterRules.LeashTargetDistanceSquared && mORecord.BackupGoal == 4 && !mORecord.Relentless
				&& liD2Home > UWCritterRules.LeashRangeFactor * liRange * liRange)
			{
				mORecord.TargetConfirmed = false;
				mORecord.HeardSomething = false;
				fSetNewGoal(4, 0);

				return;
			}

			fAttackGoalSearchForTarget(mOTarget.TileX, mOTarget.TileY, mORow.IsCaster ? liStandoff : 1);
		}

		/// <summary>AttackGoalSearchForTarget_seg007_E5D (49597-49843): the 1-in-8 re-search
		/// after a tile change, the stand test of the range, the walk with NPC_Goto and the
		/// reset when the walk went blocked.</summary>
		private void fAttackGoalSearchForTarget(int piTx, int piTy, int piRange)
		{
			if (mORecord.DestinationX != piTx || mORecord.DestinationY != piTy)
			{
				if (fRandom(UWCritterRules.ReSearchChance) == 0)
				{
					int liResult = fSearchForTarget(out piTx, out piTy);

					if (liResult == SearchLost)
					{
						mORecord.TargetConfirmed = false;
						mORecord.HeardSomething = false;
						fResetGoal();

						return;
					}

					if (liResult == SearchUnchanged && fRandom(UWCritterRules.UnchangedCoin) == 0)
					{
						mORecord.TargetConfirmed = false;
						mORecord.HeardSomething = true;
						fResetGoal();

						return;
					}

					mORecord.SetDestination(piTx, piTy, mOTarget.FloorLevel);
				}
			}

			// [49715-49735]
			if (mORecord.TileX == piTx && mORecord.TileY == piTy && fFloorDifference < UWCritterRules.MeleeHeightLevels)
				return;

			bool lbMove = false;

			// [49740-49775] strict: equality stands.
			if (piRange > 1 && piRange * piRange < miTileDist2)
				lbMove = true;
			else if (piRange * piRange * 64 < miEighthDist2)
				lbMove = true;
			else if (piRange > 1)
				return;
			else if (fFloorDifference < UWCritterRules.MeleeHeightLevels)
				return;
			else
				lbMove = true;

			if (!lbMove)
				return;

			fGoto(piTx, piTy, mOTarget.FloorLevel);

			// [49800-49815]
			if (mORecord.Blocked)
			{
				fResetGoal();
				mORecord.HeardSomething = false;
			}
		}

		// ------------------------------------------------- Goal 6 (spec 6.6)

		/// <summary>NPCGoal6_seg007_1798_13FA (50483-50979): backs away facing the target when
		/// close, runs when far; the stand roll and a bump close by lead to goal 9. Also called
		/// as a routine from goal 9, where the goal stays 9.</summary>
		private void fGoal6()
		{
			if (!mbActive)
				return;

			int liHeading = UWCritterRules.GetVectorHeading(miDx, miDy);
			int liDz = mOTarget.ZPos - mORecord.ZPos;

			if (fIsFlier)
				mORecord.Pitch = (mORecord.ZPos > 0x6E ? 13 : 15) + fRandom(5);

			if (miTileDist2 <= UWCritterRules.WithdrawNearTilesSquared && Math.Abs(liDz) < UWCritterRules.WithdrawNearHeight)
			{
				// [50569-50606] the roll comes first, then the bump.
				bool lbStand = fRandom(UWCritterRules.StandRoll) < (mORow.Morale >> 3);

				if (lbStand || (mbCollided && !mOStep.HeadingDeflected))
				{
					mORecord.MadeStand = true;
					fSetNewGoal(9, mORecord.GTarg);

					return;
				}

				// [50607-50660] walks away, looks at the target.
				mORecord.FineHeading = ((liHeading + 4) & 7) << 5;
				mORecord.SetFacing(liHeading);
				mORecord.Animation = AnimBackOff;
				mORecord.AdvancePhase();
				mORecord.Speed = (fWanderSpeed + 1) / 2;

				return;
			}

			if (mbCollided && !mOStep.HeadingDeflected)
			{
				// Far and cornered (50661-50700).
				if (miTileDist2 < UWCritterRules.CorneredTilesSquared)
				{
					if (mORecord.Goal == 9)
					{
						mORecord.SetFacing(liHeading);
						mORecord.Speed = 0;
						mORecord.Animation = AnimCombatIdle;
						mORecord.Interval = UWCritterRules.IntervalWalking;
						mORecord.AdvancePhase();

						return;
					}

					mORecord.MadeStand = true;
					fSetNewGoal(9, mORecord.GTarg);

					return;
				}

				// A quarter turn to either side (label seg007_1798_1692).
				int liSide = fRandom(2) != 0 ? 2 : -2;

				mORecord.FineHeading = ((((liHeading + liSide) & 7) << 5) + fRandom(32)) & 0xFF;
			}
			else
			{
				// [label seg007_1798_16D2]
				if (fTryMagicAttack())
					return;

				if (fRandom(UWCritterRules.WanderDeflectRoll) < mORow.Restlessness + UWCritterRules.WanderDeflectBonus)
					mORecord.FineHeading = (mORecord.FineHeading + fRandom(2 * UWCritterRules.DeflectionHalfRange)
						- UWCritterRules.DeflectionHalfRange) & 0xFF;

				if (!mOStep.HeadingDeflected)
					mORecord.FineHeading = fMaybeVectorsToPlayer(mORecord.FineHeading, UWCritterRules.WithdrawAvoidPlayerEighths);
			}

			// Both branches meet at label seg007_1798_1758 (50905-50918): the full facing follows
			// the new heading, as in the wander step.
			mORecord.SetFullFacing(mORecord.FineHeading);

			// [50936-50973]
			mORecord.Speed = miTileDist2 < UWCritterRules.FleeFastTilesSquared ? fPursuitSpeed : fWanderSpeed;
			mORecord.Animation = AnimWalking;
			mORecord.AdvancePhase();
			mORecord.Interval = UWCritterRules.IntervalWalking;
		}

		// ------------------------------------------------- Goal 9 (spec 6.7)

		/// <summary>NPCGoal9_seg007_1798_12C6 (50284-50477): strikes below 0x90 without a floor
		/// test, holds within 2 tiles, else shoots, casts or runs the withdraw routine.</summary>
		private void fGoal9()
		{
			if (!mbActive)
				return;

			if (miEighthDist2 < UWCritterRules.StandMeleeReachSquared || fOnTargetTile)
			{
				fChooseMeleeAttack(miEighthDist2);

				return;
			}

			if (miTileDist2 > UWCritterRules.HoldGroundTilesSquared)
			{
				if (fTryMagicAttack())
					return;

				if (mORow.SpellPower > 0)
					fStartMagicAttack();
				else if (mORow.HasMissileWeapon)
					fTryMissile();
				else
					fGoal6();

				return;
			}

			// [50379-50451]
			mORecord.SetFacing(UWCritterRules.GetVectorHeading(miDx, miDy));
			mORecord.Speed = 0;
			mORecord.Animation = AnimCombatIdle;
			mORecord.Interval = UWCritterRules.IntervalWalking;
			mORecord.AdvancePhase();
		}

		// ------------------------------------------------- Goal 10 (spec 6.8)

		/// <summary>GoalTalkto_seg007_1798_1937 (51162-51377): faces the player within 0x190
		/// squared eighths and talks within 0x90 when he looks back. No attitude check.</summary>
		private void fGoal10()
		{
			if (!mbActive)
				return;

			mORecord.GTarg = PlayerIndex;

			if (!fGetDistancesToGTarg())
			{
				fStandPose();

				return;
			}

			int liTx, liTy;
			int liResult = fSearchForTarget(out liTx, out liTy);

			if (liResult == SearchLost || miEighthDist2 >= UWCritterRules.TurnToTargetDistanceSquared)
			{
				fStandPose();

				return;
			}

			int liHeading = UWCritterRules.GetVectorHeading(miDx, miDy);

			mORecord.SetFacing(liHeading);
			mORecord.FineHeading = liHeading << 5;
			mORecord.Speed = 0;
			mORecord.Animation = AnimStanding;
			mORecord.Interval = UWCritterRules.IntervalStanding;

			if (miEighthDist2 < UWCritterRules.TalkDistanceSquared)
			{
				int liRelative = (mOTarget.FacingEighth + 8 - liHeading) & 7;

				if (liRelative >= 3 && liRelative <= 5)
					mIHost.OnTalk();
			}
		}

		// ------------------------------------------------- Goal 11 (spec 6.9)

		/// <summary>Ethereal_seg007_1798_318D (53968-54030): random heading, speed 0 or 1,
		/// random height, standing bit; no reactions.</summary>
		private void fGoal11()
		{
			mORecord.Interval = UWCritterRules.IntervalWalking;
			mORecord.Speed = fRandom(2);
			mORecord.FineHeading = fRandom(256);
			mORecord.Pitch = 15 + fRandom(3);
			mORecord.AdvancePhase();
			mORecord.IsStanding = true;
		}

		// ------------------------------------------------- Goal 12 (spec 6.10)

		/// <summary>NPCGoalC_seg007_1798_1BF4 (51513-51649): hostile becomes 4; else walks to
		/// the home post and stands there.</summary>
		private void fGoal12()
		{
			if (!mbActive)
				return;

			if (mORecord.Attitude == UWNpc.AttitudeHostile)
			{
				fSetNewGoal(4, PlayerIndex);

				return;
			}

			if (mORecord.TileX != mORecord.HomeX || mORecord.TileY != mORecord.HomeY)
				fGoto(mORecord.HomeX, mORecord.HomeY, mIHost.FloorLevelAt(mORecord.HomeX, mORecord.HomeY));
			else
				fStandPose();
		}

		// ------------------------------------------------- NPC_Goto_seg006_1477_29A1 (46652-47363, spec 7.1)

		/// <summary>
		/// The walk to a TILE: arrival by tile equality; the collision rules and the blocked
		/// bit; the heading from the straight tile line, the host's path or the wander step
		/// while blocked; then the walk block that writes animation 0x2C, the speed of the
		/// goal and interval 4 unconditionally (label seg006_1477_2FB7).
		/// </summary>
		private void fGoto(int piX, int piY, int piZ)
		{
			mORecord.SetDestination(piX, piY, piZ);

			if (mORecord.NewDestination && mORecord.HasPath)
				mORecord.HasPath = false;

			// Arrival (46700-46745).
			if (mORecord.TileX == piX && mORecord.TileY == piY)
			{
				mORecord.HasPath = false;

				if (mORecord.Goal == 1)
					fSetNewGoal(8, 0);
				else if (mbActive)
				{
					mORecord.Speed = 0;
					mORecord.IsStanding = true;
					mORecord.Animation = AnimStanding;
				}

				return;
			}

			if (!mbActive)
			{
				mORecord.Interval = UWCritterRules.IntervalStuck;

				return;
			}

			bool lbSkipStraight = false;

			// The collision rules (46841-46960).
			if (mbCollided && !mOStep.HeadingDeflected && !mORecord.Blocked)
			{
				if (mOStep.HitObject)
				{
					int liItemId = mOStep.HitObjectItemId;

					if (mOStep.HitClosedDoor)
					{
						mORecord.Animation = AnimStanding;

						// The roll comes before the attitude test (46870).
						bool lbGiveUp = fRandom(UWCritterRules.DoorTryChance) == 0;

						if (lbGiveUp || mORecord.Attitude != UWNpc.AttitudeHostile)
							mORecord.Blocked = true;
						else
							fTryOpenDoor(mOStep.HitObjectIndex, liItemId);
					}
					else if (mOStep.HitObjectIsCreature && liItemId != PlayerItemId && mORecord.Goal == 5
						&& mIHost.GetObject(mOStep.HitObjectIndex).Goal == 5)
					{
						// Two attackers bumping: nothing (46880-46925).
					}
					else if ((liItemId >> 4) == 0x14 && (liItemId & 0xF) >= 8 && fIsFlier)
					{
						// An open door and a flier: duck under it (46930).
						mORecord.Pitch = UWCritterRules.FlierPitchDown;
						mbCollided = false;
						mbFlierDucked = true;
					}
					else
						mORecord.Blocked = true;
				}

				if (mbCollided)
				{
					mORecord.HasPath = false;
					mORecord.StraightLineKnown = false;
					lbSkipStraight = true;
				}
			}

			if (mORecord.HasPath)
			{
				fTurnTowardsPath(piX, piY);
			}
			else if (mORecord.StraightLineKnown && !mORecord.NewDestination)
			{
				fHeadStraightTo(piX, piY);

				if (fIsFlier)
					fFlierPitchTowards(piX, piY);
			}
			else if (mORecord.Blocked && !mORecord.NewDestination)
			{
				// [47000-47020] one in eight clears the bit; the wander step runs either way.
				if (fRandom(UWCritterRules.BlockedClearChance) == 0)
					mORecord.Blocked = false;

				fWanderUpdate();

				return;
			}
			else
			{
				if (!lbSkipStraight && mIHost.IsStraightLineClear(piX, piY))
				{
					// [47030-47050]
					mORecord.StraightLineKnown = true;
					fHeadStraightTo(piX, piY);
					mORecord.Blocked = false;
					mORecord.HasPath = false;
				}
				else
				{
					int liNextX, liNextY;
					int liBudget = UWCritterRules.GetPathRangeBudget(mORecord.Attitude, mORow.Vitality,
						mORecord.HitPoints, mORow.Morale, mORecord.ExcludedFromPathBudget);

					if (mIHost.TryGetNextPathTile(piX, piY, liBudget, mORow.DoorSkill != 0, out liNextX, out liNextY))
					{
						// [47060-47261]
						mORecord.Blocked = false;
						mORecord.HasPath = true;
						fAimAtPathTile(liNextX, liNextY);
					}
					else
					{
						mORecord.Blocked = true;
						mORecord.StraightLineKnown = false;
						fWanderUpdate();

						return;
					}
				}
			}

			// The walk block (label seg006_1477_2FB7, 47293-47355).
			mORecord.IsStanding = false;
			mORecord.Animation = AnimWalking;
			mORecord.Speed = mbFlierDucked ? 0 : (mORecord.Goal == 5 ? fPursuitSpeed : fWanderSpeed);
			mORecord.AdvancePhase();
			mORecord.Interval = UWCritterRules.IntervalWalking;
		}

		/// <summary>The eight-way heading of the tile difference (46906, 47040): byte 9, the
		/// facing and a cleared residual.</summary>
		private void fHeadStraightTo(int piX, int piY)
		{
			int liHeading = UWCritterRules.GetVectorHeading(piX - mORecord.TileX, piY - mORecord.TileY);

			mORecord.FineHeading = liHeading << 5;
			mORecord.SetFacing(liHeading);
		}

		/// <summary>TurnTowardsPath_seg006_1477_2504 (46064) over the host's path: the next tile
		/// towards the destination, or the end of the path (release).</summary>
		private void fTurnTowardsPath(int piX, int piY)
		{
			int liNextX, liNextY;
			int liBudget = UWCritterRules.GetPathRangeBudget(mORecord.Attitude, mORow.Vitality,
				mORecord.HitPoints, mORow.Morale, mORecord.ExcludedFromPathBudget);

			if (mIHost.TryGetNextPathTile(piX, piY, liBudget, mORow.DoorSkill != 0, out liNextX, out liNextY))
				fAimAtPathTile(liNextX, liNextY);
			else
				mORecord.HasPath = false;
		}

		/// <summary>The aim point of a normal path step (46170-46230): tile * 8 + 4 on the axis
		/// where the tile column equals the creature's, + 7 when the path tile lies below, + 0
		/// when above; the heading by GetVectorHeading of aim minus position.</summary>
		private void fAimAtPathTile(int piNextX, int piNextY)
		{
			int liAimX = (piNextX << 3) + fAimOffset(piNextX, mORecord.TileX);
			int liAimY = (piNextY << 3) + fAimOffset(piNextY, mORecord.TileY);
			int liHeading = UWCritterRules.GetVectorHeading(liAimX - mORecord.X, liAimY - mORecord.Y);

			mORecord.FineHeading = liHeading << 5;
			mORecord.SetFacing(liHeading);

			if (fIsFlier)
				fFlierPitchTowards(piNextX, piNextY);
		}

		private static int fAimOffset(int piPathTile, int piOwnTile)
		{
			if (piPathTile == piOwnTile)
				return UWCritterRules.PathAimCentre;

			return piPathTile < piOwnTile ? UWCritterRules.PathAimBelow : UWCritterRules.PathAimAbove;
		}

		/// <summary>seg006_1477_3061 (47369-47440): a flier's pitch towards 20 + 8 * the
		/// destination floor, capped at 0x78; skipped in the update it ducked under a door.</summary>
		private void fFlierPitchTowards(int piX, int piY)
		{
			if (mbFlierDucked)
				return;

			int liTarget = UWCritterRules.FlierTargetHeightBase + (mIHost.FloorLevelAt(piX, piY) << 3);

			if (liTarget > UWCritterRules.FlierTargetHeightMax)
				liTarget = UWCritterRules.FlierTargetHeightMax;

			int liZ = mORecord.ZPos;

			if (mOStep.TouchedCeiling && liZ < UWCritterRules.FlierTargetHeightMax)
				mORecord.Pitch = UWCritterRules.FlierPitchUp;
			else if (liZ < liTarget - 8)
				mORecord.Pitch = UWCritterRules.FlierPitchUp;
			else if (liZ > UWCritterRules.FlierTargetHeightMax || liZ > liTarget + 8)
				mORecord.Pitch = UWCritterRules.FlierPitchDown;
			else
				mORecord.Pitch = 15 + fRandom(3);
		}

		/// <summary>NPCTryToOpenDoor_seg006_1477_3123 (47474): portcullises never; with door
		/// skill the door is used, a door still closed is picked (RNG(2) != 0) or bashed
		/// (RNG(4) == 0 with RNG(table byte 0x14) damage).</summary>
		private void fTryOpenDoor(int piDoorIndex, int piItemId)
		{
			if ((piItemId & 7) == 7)
				return;

			int liSkill = mORow.DoorSkill;
			bool lbOpen = false;

			if (liSkill != 0)
				lbOpen = mIHost.UseDoor(piDoorIndex);

			if (lbOpen)
				return;

			if (liSkill != 0 && fRandom(UWCritterRules.LockpickChance) != 0)
				mIHost.PickDoor(piDoorIndex, liSkill);
			else if (fRandom(UWCritterRules.BashChance) == 0)
				mIHost.BashDoor(piDoorIndex, fRandom(mORow.DoorBashDie));
		}

		// ------------------------------------------------- Combat (spec 8)

		/// <summary>
		/// ChooseMeleeAttackToMake_seg007_1798_AFF (49204-49591): faces the target, the
		/// footwork by distance (back off or side-step below 7 eighths, circle by dexterity up
		/// to 9, walk in beyond), a flier's pitch, and within 10 eighths the 1-in-4 roll that
		/// starts a swing - or raises the charge index.
		/// </summary>
		private void fChooseMeleeAttack(int piD2)
		{
			int liFacing = UWCritterRules.GetVectorHeading(miDx, miDy);

			mORecord.SetFacing(liFacing);
			mORecord.FineHeading = liFacing << 5;
			mORecord.IsStanding = false;

			if (piD2 < UWCritterRules.MeleeBackOffDistanceSquared)
			{
				// [49256-49318]
				if (fRandom(UWCritterRules.SideStepChance) == 0)
				{
					int liSide = fRandom(2) != 0 ? 2 : -2;

					mORecord.FineHeading = ((liFacing + liSide) & 7) << 5;
					mORecord.Animation = AnimCombatIdle;
					mORecord.Speed = fWanderSpeed * UWCritterRules.SideStepSpeedNumerator / UWCritterRules.SideStepSpeedDenominator;
				}
				else
				{
					mORecord.FineHeading = ((liFacing + 4) & 7) << 5;
					mORecord.Animation = AnimBackOff;
					mORecord.Speed = UWCritterRules.BackOffSpeed;
				}
			}
			else if (piD2 <= UWCritterRules.MeleeCircleDistanceSquared)
			{
				// [49349-49363]
				if (fRandom(64) < mORow.Dexterity)
				{
					mORecord.FineHeading = fRandom(8) << 5;
					mORecord.Animation = AnimCombatIdle;
					mORecord.Speed = UWCritterRules.CircleSpeed;
				}
				else
				{
					mORecord.Animation = AnimCombatIdle;
					mORecord.Speed = 0;
				}
			}
			else
			{
				mORecord.Animation = AnimWalking;
				mORecord.Speed = UWCritterRules.WalkInSpeed;
			}

			if (fIsFlier)
			{
				// [label seg007_1798_CD8] the target's zpos + 14 against the own zpos.
				int liDiff = mOTarget.ZPos + 14 - mORecord.ZPos;

				if (liDiff > 1)
					mORecord.Pitch = UWCritterRules.FlierPitchUp;
				else if (liDiff < -1)
					mORecord.Pitch = UWCritterRules.FlierPitchDown;
				else
					mORecord.Pitch = 15 + fRandom(3);
			}

			if (piD2 <= UWCritterRules.MeleeDistanceSquared)
			{
				// [label seg007_1798_D51, 49462]
				if (fRandom(UWCritterRules.MeleeStartChance) == 0)
				{
					int liRoll = fRandom(100);
					int liAttack = 0;

					while (liAttack < 2 && mORow.Attacks[liAttack].Probability <= liRoll)
					{
						liRoll -= mORow.Attacks[liAttack].Probability;
						liAttack++;
					}

					mORecord.Animation = liAttack + 1;
					mORecord.Interval = UWCritterRules.IntervalWalking;
					mORecord.Frame = 0;
				}
				else
				{
					if (mORecord.ChargeIndex < UWCritterRules.ChargeIndexMax)
						mORecord.ChargeIndex++;

					mORecord.Interval = UWCritterRules.IntervalWalking;
					mORecord.AdvancePhase();
				}
			}
			else
			{
				mORecord.Interval = UWCritterRules.IntervalWalking;
				mORecord.AdvancePhase();
			}
		}

		/// <summary>TryToDoMagicAttack_seg007_1798_FF2 (49849-49952): the third spell, rolled
		/// without range or sight; not on a no-magic tile, not Tybal while the orb stands.</summary>
		private bool fTryMagicAttack()
		{
			if (mORow.Spell3 == -1)
				return false;

			if (fRandom(UWCritterRules.Spell3Roll) >= mORow.SpellPower)
				return false;

			if (mIHost.IsMagicBlockedAt(mORecord.TileX, mORecord.TileY))
				return false;

			if (mORow.GeneralType == UWCritterRules.OrbMageKind && mIHost.TybalOrbStands)
				return false;

			mORecord.Speed = 0;
			mORecord.Animation = AnimSpell;
			mORecord.SpellSlot = UWCritterRules.SpellSlotThird;
			mORecord.Frame = 0;

			return true;
		}

		/// <summary>NPCStartMagicAttack_seg007_1798_109F (49958-50169): spells 1 and 2 within 8
		/// tiles with sight and facing; returns true whenever sight and facing were given.</summary>
		private bool fStartMagicAttack()
		{
			if (mIHost.IsMagicBlockedAt(mORecord.TileX, mORecord.TileY))
				return false;

			if (mORow.GeneralType == UWCritterRules.OrbMageKind && mIHost.TybalOrbStands)
				return false;

			if (miTileDist2 >= UWCritterRules.CasterSpellTilesSquared)
				return false;

			if (!fHasSightOfTarget())
				return false;

			if (!fTurnTowardsTarget(true))
				return false;

			if (fRandom(UWCritterRules.Spell12Roll) < mORow.SpellPower)
			{
				mORecord.Animation = AnimSpell;
				mORecord.SpellSlot = fRandom(UWCritterRules.PrimarySpellRoll) < UWCritterRules.PrimarySpellChance ? 1 : 2;
				mORecord.Speed = 0;
				mORecord.Frame = 0;
			}

			return true;
		}

		/// <summary>seg007_1798_11FB (50175-50278): the missile within 4 tiles with sight and
		/// facing, started by RNG(0xC0) &lt;= dexterity; true whenever sight and facing were given.</summary>
		private bool fTryMissile()
		{
			if (miTileDist2 >= UWCritterRules.MissileTilesSquared)
				return false;

			if (!fHasSightOfTarget())
				return false;

			if (!fTurnTowardsTarget(true))
				return false;

			if (fRandom(UWCritterRules.MissileRoll) <= mORow.Dexterity)
			{
				mORecord.Animation = AnimMissile;
				mORecord.Speed = 0;
				mORecord.Frame = 0;
			}

			return true;
		}

		private bool fHasSightOfTarget()
		{
			return mIHost.HasLineOfSight(mORecord.X, mORecord.Y, mORecord.ZPos + mIHost.OwnObjectHeight,
				mOTarget.X, mOTarget.Y, mOTarget.ZPos + mOTarget.ObjectHeight);
		}

		/// <summary>GetPitchToGTarg_seg007_1798_22C1 (52382): dz * 4 / dist clamped to +-15
		/// (+-15 at distance 0), plus dist * 3 / velocity for a missile's drop.</summary>
		private int fPitchToTarget(int piVelocity, bool pbGravity)
		{
			if (!mbTargetFound)
				return 0;

			int liDz = mOTarget.ZPos - mORecord.ZPos;
			int liDist = IntSqrt(miEighthDist2);

			if (liDist == 0)
				return liDz > 0 ? 15 : -15;

			int liPitch = liDz * 4 / liDist;

			if (liPitch > 15)
				liPitch = 15;
			else if (liPitch < -15)
				liPitch = -15;

			if (pbGravity && piVelocity != 0)
				liPitch += liDist * 3 / piVelocity;

			return liPitch;
		}

		// ------------------------------------------------- The turn limiter (seg007_1798_1FDC, 52016-52168)

		/// <summary>
		/// After the goal routine: the facing turns at most 45 degrees per update towards what
		/// the goal wanted; a deflected heading is kept as the physics left it; a moving
		/// creature whose new heading differs by 45..90 degrees is clamped, by 90 degrees or
		/// more stopped for one update.
		/// </summary>
		private void fTurnLimiter()
		{
			int liWanted = mORecord.FullFacing;
			int liBefore = miFullFacingBefore;
			int liDiff = (liWanted - liBefore) & 0xFF;

			if (liDiff >= UWCritterRules.TurnLimitPerUpdate && liDiff <= 0x100 - UWCritterRules.TurnLimitPerUpdate)
				liWanted = (liBefore + (liDiff < 0x80 ? UWCritterRules.TurnLimitPerUpdate : -UWCritterRules.TurnLimitPerUpdate)) & 0xFF;

			mORecord.SetFullFacing(liWanted);

			if (mOStep.HeadingDeflected)
			{
				// [52085, label seg007_1798_2116] the physics' heading stands, speed untouched.
				mORecord.FineHeading = miFineHeadingBefore;

				return;
			}

			if (miSpeedBefore <= 1 || mORecord.Speed <= 1)
				return;

			int liD = (mORecord.FineHeading - miFineHeadingBefore) & 0xFF;

			if (liD < UWCritterRules.TurnLimitPerUpdate || liD > 0x100 - UWCritterRules.TurnLimitPerUpdate)
				return;

			if (liD < UWCritterRules.ReversalStopThreshold)
				mORecord.FineHeading = (miFineHeadingBefore + UWCritterRules.TurnLimitPerUpdate) & 0xFF;
			else if (liD > 0x100 - UWCritterRules.ReversalStopThreshold)
				mORecord.FineHeading = (miFineHeadingBefore - UWCritterRules.TurnLimitPerUpdate) & 0xFF;
			else
			{
				// [label seg007_1798_2108] a reversal stops the creature for one update.
				mORecord.Speed = 0;
				mORecord.FineHeading = miFineHeadingBefore;
			}
		}

		// ------------------------------------------------- DamageNPC_seg007_1798_3622 (54563-54802)

		/// <summary>
		/// What DamageNPC does to the record and the alarm: adds the damage to byte 0x11,
		/// stores the attacker in byte 0x12 (1 the player, a projectile's launcher, 0 none),
		/// writes the kin alarm for the player's blows, kills or subtracts, and switches the
		/// music after a blow by the player. The mind reacts on its next update (the damage
		/// reaction). A miss is a call with 0 damage: it still records the attacker.
		/// </summary>
		public void OnDamaged(ICritterHost pIHost, int piAttackerIndex, int piDamage)
		{
			mIHost = pIHost;

			mORecord.DamageTaken = (mORecord.DamageTaken + piDamage) & 0xFF;

			if (piAttackerIndex != 0)
				mORecord.Attacker = piAttackerIndex;

			if (piAttackerIndex == PlayerIndex && !mORecord.AttitudeLocked && pIHost.Alarm != null)
				pIHost.Alarm.Record(miIndex, mORow.GeneralType, mORecord.TileX, mORecord.TileY, mORecord.FloorLevel, pIHost.Clock);

			if (mORecord.HitPoints <= piDamage)
			{
				mORecord.HitPoints = 0;

				if (fDeath())
				{
					pIHost.OnDeathStarted(piAttackerIndex == PlayerIndex);

					return;
				}
			}
			else
				mORecord.HitPoints -= piDamage;

			if (piAttackerIndex == PlayerIndex)
			{
				int liHealth = mORecord.HitPoints * 64 / (mORow.Vitality + 1);

				pIHost.PlayMusic(liHealth < UWCritterRules.HurtThemeThreshold
					? UWCritterRules.HurtCombatTheme
					: UWCritterRules.CombatTheme);
			}
		}

		/// <summary>Death_seg007_1798_35CB (54504) and ProcessDeath_seg007_1798_3577 (54452):
		/// unless already dying, a whoami lets the script veto; else animation 0x0C, frame 0,
		/// interval 4, hp 0, and the death sound for kinds with table byte 8 &amp; 7 == 1.</summary>
		private bool fDeath()
		{
			if (mORecord.Animation == AnimDying)
				return false;

			if (mORecord.WhoAmI != 0 && mIHost.VetoDeath())
				return false;

			mORecord.Animation = AnimDying;
			mORecord.Frame = 0;
			mORecord.Interval = UWCritterRules.IntervalWalking;
			mORecord.HitPoints = 0;

			if ((mORow.RowByte(8) & 7) == 1)
				mIHost.PlaySound(UWCritterRules.DeathSound);

			return true;
		}

		// ------------------------------------------------- helpers

		/// <summary>The original's GetSquareRoot_seg019_F3F: the integer root, rounded down.</summary>
		public static int IntSqrt(int piValue)
		{
			if (piValue <= 0)
				return 0;

			int liRoot = (int)Math.Sqrt(piValue);

			while (liRoot * liRoot > piValue)
				liRoot--;

			while ((liRoot + 1) * (liRoot + 1) <= piValue)
				liRoot++;

			return liRoot;
		}
	}
}
