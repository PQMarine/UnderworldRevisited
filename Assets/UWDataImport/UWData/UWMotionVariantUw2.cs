namespace UWDataImport.UWData
{
	/// <summary>
	/// THE SECOND GAME'S MOTION (Docs/UW2-PLAN.md phase 2 step 4, 2026-10-29), as Underworld Exhumed's
	/// matched sources have it (docs/UW1-UW2-DIFFERENCES.md, "Movement and physics"):
	///
	/// - newFPS (uw2 motion/PHYSICS.C:748-770): the speeds are the full ones times a share out of 20 -
	///   on foot 20, swimming Swimming / 2 + 4, lava 14, ice 20, levitating 1, flying 14, slow falling
	///   4. It also sets a turn rate (pTurn, the same share on the ground) that nothing reads: the turn
	///   is PlayerTurn in every state (PHYSICS.C:402, 671), as in the first game. Until 2026-11-14 ours
	///   turned with pTurn, in water at 6/20 and on lava at 14/20 (per user: turning in water too slow).
	/// - The view swings towards the motion by up to 0x600 a step (PHYSICS.C:591-596).
	/// - No fall damage on levels 65 to 72, (PlayerLevel - 1) / 8 == 8 (PHYSICS.C:604).
	/// - Lava spares nobody (PLAYMOVE.C:520); an impact hurts above 0x180, a creature a quarter, an
	///   object of fewer than 0x20 hit points four times (OBJPHYS.C:299-305).
	///
	/// Ice, currents and paralysis are the second game's own and come with the rest of step 4.
	/// The shares are this variant's, not the rule set's TenthsByState (the first game's tenths).
	/// </summary>
	public sealed class UWMotionVariantUw2 : IUWMotionVariant
	{
		/// <summary>newFPS's ratios by state, out of 20.</summary>
		public static readonly int[] TwentiethsByState = { 20, 6, 14, 20, 1, 14, 4 };

		public const int ShareDivisor = 20;

		/// <summary>The swimming state's index.</summary>
		public const int SwimmingState = 1;

		/// <summary>The swimming share: Swimming / 2 + this.</summary>
		public const int SwimmingShareBase = 4;

		public const int FacingFollowStep = 0x600;

		public const int ImpactThreshold = 0x180;

		/// <summary>An object with fewer hit points takes the impact four times.</summary>
		public const int FragileHitPoints = 0x20;

		/// <summary>The world without fall damage: (level - 1) / 8 == 8.</summary>
		public const int NoFallDamageWorld = 8;

		public const int LevelsPerWorld = 8;

		public int ScaleSpeed(UWRulesMotion.SpeedRules pORules, int piBase, int piState, int piSwimmingSkill)
		{
			int liShare = piState == SwimmingState
				? (piSwimmingSkill / 2) + SwimmingShareBase
				: TwentiethsByState[piState];

			return piBase * liShare / ShareDivisor;
		}

		/// <summary>The full turn in every state: newFPS's pTurn is never read (see the class comment).</summary>
		public int ScaleTurn(int piTurnStep, int piState)
		{
			return piTurnStep;
		}

		public int FacingFollow => FacingFollowStep;

		public bool FallHurts(int piLevel)
		{
			return (piLevel - 1) / LevelsPerWorld != NoFallDamageWorld;
		}

		public bool IceAndCurrents => true;

		public bool PutDownSplashes => true;

		public bool IceHoldsPlacedObjects => true;

		public bool NothingRestsInVoid => false;

		/// <summary>The fireball do_objhit lets strike again (ITEM_FIREBALL_1D).</summary>
		public const int RepeatingFireballId = 0x1D;

		/// <summary>The resilient sphere do_objhit lets strike again (ITEM_RESILIENT_SPHERE_13F).</summary>
		public const int ResilientSphereId = 0x13F;

		public bool StrikesMobilesAgain(int piItemId) => piItemId == RepeatingFireballId || piItemId == ResilientSphereId;

		/// <summary>The light splash below this mass, the middle one below MediumSplashMass, else the first game's.</summary>
		public const int LightSplashMass = 20;

		public const int MediumSplashMass = 100;

		public int WaterLandingSound(int piMass, out int piVolume)
		{
			piVolume = 0;

			return piMass < LightSplashMass ? 0x18 : piMass < MediumSplashMass ? 0x19 : 5;
		}

		/// <summary>Water only (per user, 2026-11-06: with Bouncing one hopped on snow in the original, ours
		/// stopped dead there - the first game's bit test took the ice, terrain 3, for water).</summary>
		public bool LandsInWater(int piFlags)
		{
			return (piFlags & 3) == 1;
		}

		/// <summary>munge_vectors' kinds: ice moves the first motion towards the second, a current or a
		/// slope adds them.</summary>
		public const int MungeIce = 2;

		public const int MungeAdd = 3;

		/// <summary>The strength that moves the whole way (strength / 0x40).</summary>
		public const int MungeFull = 0x40;

		/// <summary>
		/// munge_vectors (Underworld Exhumed uw2 motion/PHYSICS.C:297): two motions as heading and speed,
		/// each turned into a vector by the table sine and cosine (cFstSinCos, the table entry without
		/// interpolation - UWMotionTables.SinCosCoarse) divided by 0x800 and times the speed; ice moves the
		/// first towards the second by strength / 0x40 (at most 0x40), a current or slope adds them. The
		/// result back through the square root of the sum of the squares of a sixteenth of each (1 and
		/// below is 0) and, when it moves, the arctangent of the vector scaled to 0x7FFF; standing keeps
		/// the first heading. A strength of 0 or less returns the first motion.
		/// </summary>
		public static void MungeVectors(int piKind, int piHeading, int piSpeed, int piHeading2, int piSpeed2, int piStrength,
			out int piOutHeading, out int piOutSpeed)
		{
			if (piStrength <= 0)
			{
				piOutSpeed = piSpeed;
				piOutHeading = piHeading;

				return;
			}

			if (piStrength > MungeFull)
				piStrength = MungeFull;

			UWMotionTables.SinCosCoarse(piHeading, out int liX1, out int liY1);
			UWMotionTables.SinCosCoarse(piHeading2, out int liX2, out int liY2);

			liX1 = (short)((short)(liX1 / 0x800) * piSpeed);
			liY1 = (short)((short)(liY1 / 0x800) * piSpeed);
			liX2 = (short)((short)(liX2 / 0x800) * piSpeed2);
			liY2 = (short)((short)(liY2 / 0x800) * piSpeed2);

			int liVx;
			int liVy;

			if (piKind == MungeIce)
			{
				liVx = (short)(liX1 + (int)((long)(short)(liX2 - liX1) * piStrength / MungeFull));
				liVy = (short)(liY1 + (int)((long)(short)(liY2 - liY1) * piStrength / MungeFull));
			}
			else
			{
				liVx = (short)(liX2 + liX1);
				liVy = (short)(liY2 + liY1);
			}

			long llX = liVx / 16;
			long llY = liVy / 16;

			piOutSpeed = UWVanillaMath.Sqrt((int)((llX * llX) + (llY * llY)));

			if (piOutSpeed <= 1)
				piOutSpeed = 0;

			if (piOutSpeed != 0)
			{
				llX = llX * 0x7FFF / piOutSpeed;
				llY = llY * 0x7FFF / piOutSpeed;
				piOutHeading = UWVanillaMath.Atan2((short)llX, (short)llY) & 0xFFFF;
			}
			else
			{
				piOutHeading = piHeading;
			}
		}

		public bool LavaSpares(bool pbDragonSkinBoots)
		{
			return false;
		}

		public int ImpactDamage(int piImpact, bool pbCreature, int piHitPoints)
		{
			int liImpact = piImpact & 0xFFFF;

			if (liImpact <= ImpactThreshold)
				return 0;

			int liDamage = liImpact >> 8;

			if (pbCreature)
				return liDamage / 4;

			return piHitPoints < FragileHitPoints ? liDamage * 4 : liDamage;
		}

		/// <summary>No write-back after the impact (OBJPHYS.C:295-312): the wear stays. Until 2026-11-07
		/// ours undid it as in the first game.</summary>
		public bool ImpactWearUndone => false;
	}
}
