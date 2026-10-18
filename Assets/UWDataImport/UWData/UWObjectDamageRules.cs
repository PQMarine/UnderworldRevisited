namespace UWDataImport.UWData
{
	/// <summary>
	/// What damage does to an OBJECT that is not a creature - read 2026-09-27 after the user saw
	/// a small animated smoke cloud over the remains of summoned creatures that the mages'
	/// fireballs had killed (SAVE2, level 4, tile 4/56: smoke 0x1C8 over a pile of debris 0xD5
	/// with quality 0, the blood stains beside it worn down from 40 to 11 and 14).
	///
	/// THE BLAST: DetonateProjectile_seg044_95A turns a stopped fireball (0x14) into the
	/// explosion 0x1C2 and a lightning bolt (0x15) into 0x1C5 (tables AB0/AB4) and then strikes
	/// every object in the list of the missile's tile, each with a roll of its own from the
	/// tables 9D3/9D5/9D7: 10d6 of type 0x0B for the fireball (the same as Flame Wind), 6d5 of
	/// type 3 for the lightning bolt (the same as Sheet Lightning). Nothing is spared - the
	/// creature the missile has just hit is in that list too, and so is the player, whom the
	/// game keeps in the list of his tile while playing. A creature and the player take it
	/// without armour, only the resistances count.
	///
	/// THE WEAR: DamageObjectAndDoors_seg023_3E7 leaves an object alone when word 0 bit 13 is
	/// set (almost every trigger and trap carries it, see UWObject.DoorDirection) or its
	/// quality class (COMOBJ byte 6 bits 2-3) is 3; otherwise it halves the damage once per
	/// class and takes it off the quality. At 0 the object is destroyed.
	///
	/// THE REMAINS: DamageObject_Debris_seg023_D6 (remove_object of DAMAGE.C, read in the
	/// decompilation 2026-10-12) has four branches. A DOOR swings open and loses its locks and
	/// stays. A CHEST OR BARREL loses its lock, is used (UseCont, so its contents spill, see
	/// UWSpillRules) and becomes a pile of debris. A BAG (class 8) is asked the culling test
	/// with the range 10 (Obj_Elem_Fate, UWObjectLimitRules.CullingTest): spared, it is LEFT AS
	/// IT IS, worn to quality 0; otherwise it spills (DumpTheBag) and becomes debris. EVERYTHING
	/// ELSE loses its chain and becomes debris, 0xD5 or 0xD6 - and only here, with FIRE (type
	/// bit 8), a pile of debris that is struck again vanishes and any other object puts up the
	/// smoke (put_effect 8, 6d10) one time in four first. The object BECOMES the debris: the
	/// id changes, the quality stays at the 0 the wear left (DebrisQuality).
	/// </summary>
	public static class UWObjectDamageRules
	{
		/// <summary>The fireball and the lightning bolt, the two missiles that explode.</summary>
		public const int FireballObjectId = 0x14;

		public const int LightningBoltObjectId = 0x15;

		public const int FireBlastDiceCount = 10;

		public const int FireBlastDiceRange = 6;

		public const int LightningBlastDiceCount = 6;

		public const int LightningBlastDiceRange = 5;

		/// <summary>"a_pile of debris", the first of the two remains objects.</summary>
		public const int FirstDebrisObjectId = UWObjectIds.FirstDebris;

		public const int DebrisVariants = 2;

		/// <summary>"some_smoke", animation object 0x1C0 + 8.</summary>
		public const int SmokeObjectId = 0x1C8;

		/// <summary>The smoke's duration as the game hands it over, DiceRoll(6, 10).</summary>
		public const int SmokeDiceCount = 6;

		public const int SmokeDiceRange = 10;

		/// <summary>
		/// The animation table counts the duration down by what the frame loop hands it
		/// (the countdown seg044_6F9, fed by the frame loop): the PIT timer shifted right by 6, one unit per 64
		/// PIT ticks - four a second at the game's 256 (UWPlayerTick.PitTicksPerSecond), the same
		/// four a second the user measured for the animated objects. The smoke thus stays 1.5 to
		/// 15 seconds, 8 on average. (Guessed at twelve a second until the user did not see the
		/// smoke at all, 2026-09-27.)
		/// </summary>
		public const float AnimationTicksPerSecond = UWPlayerTick.PitTicksPerSecond / 64f;

		/// <summary>The quality class that takes no damage at all.</summary>
		public const int ImmuneQualityClass = 3;

		/// <summary>Containers are major class 0x08 of the object ids (0x80-0x8F).</summary>
		private const int ContainerIdClass = 0x08;

		/// <summary>The dice of the blast a missile sets off when it stops, or false for every
		/// missile that does not explode.</summary>
		public static bool TryGetBlast(int piMissileObjectId, out int piDiceCount, out int piDiceRange,
			out int piDamageType)
		{
			switch (piMissileObjectId)
			{
				case FireballObjectId:
					piDiceCount = FireBlastDiceCount;
					piDiceRange = FireBlastDiceRange;
					piDamageType = UWDamageTypes.Fire;
					return true;

				case LightningBoltObjectId:
					piDiceCount = LightningBlastDiceCount;
					piDiceRange = LightningBlastDiceRange;
					piDamageType = UWDamageTypes.Magic;
					return true;

				default:
					piDiceCount = 0;
					piDiceRange = 0;
					piDamageType = UWDamageTypes.None;
					return false;
			}
		}

		/// <summary>
		/// DAMAGE_OBJECT'S FIRST TEST (UnderworldExhumed combat/DAMAGE.C): an object whose word 0
		/// bit 13 is set (pbProtected, UWObject.DoorDirection) or whose quality class is 3 takes
		/// nothing, whatever the blow - the one home of that condition since 2026-10-18 (cleanup
		/// 4.0 follow-up: blasts reach the wall squares now, where the traps and triggers lie, and
		/// every one of them carries the bit). Wear, UWHealth.IsIndestructible and the spawner's
		/// door and container health ask here.
		/// </summary>
		public static bool ShrugsOff(bool pbProtected, int piQualityClass)
		{
			return pbProtected || piQualityClass >= ImmuneQualityClass;
		}

		/// <summary>
		/// Wears an object down. The damage has already been scaled by the resistances; it is
		/// halved once per quality class and taken off the quality, which never drops below 0.
		/// Returns true when the object is destroyed by it.
		/// </summary>
		public static bool Wear(int piQuality, int piDamage, int piQualityClass, bool pbProtected,
			out int piNewQuality)
		{
			piNewQuality = piQuality;

			if (ShrugsOff(pbProtected, piQualityClass))
				return false;

			int liDamage = piDamage >> piQualityClass;

			if (liDamage <= 0)
				return false;

			piNewQuality = piQuality - liDamage;

			if (piNewQuality > 0)
				return false;

			piNewQuality = 0;

			return true;
		}

		public static bool IsDebris(int piObjectId)
		{
			return piObjectId >= FirstDebrisObjectId && piObjectId < FirstDebrisObjectId + DebrisVariants;
		}

		/// <summary>A bag, a box, a pouch: its contents land on the tile before it becomes
		/// debris.</summary>
		public static bool SpillsWhenDestroyed(int piObjectId)
		{
			return (piObjectId >> 4) == ContainerIdClass;
		}

		/// <summary>
		/// A chest (0x15D) or barrel (0x15B): remove_object loses one lock and USES it (UseCont),
		/// so its contents spill. The nightstand (0x15E), though it holds things too, falls into
		/// the last branch with everything else: its chain is freed and the contents are gone
		/// (UnderworldExhumed combat/DAMAGE.C; until 2026-10-12 ours spilled it like a chest).
		/// </summary>
		public static bool IsUsedWhenDestroyed(int piObjectId)
		{
			return piObjectId == UWObjectMechanics.ChestObjectId || piObjectId == UWObjectMechanics.BarrelObjectId;
		}

		public enum Remains
		{
			/// <summary>The object becomes a pile of debris.</summary>
			Debris,

			/// <summary>Smoke rises, then the object becomes a pile of debris.</summary>
			DebrisWithSmoke,

			/// <summary>A pile of debris that fire struck again: gone.</summary>
			Gone
		}

		/// <summary>What is left of a destroyed object. Rolls the smoke (RNG &amp; 3 == 0) only
		/// for fire.</summary>
		public static Remains DecideRemains(int piObjectId, int piDamageType)
		{
			if ((piDamageType & UWDamageTypes.PlainFire) == 0)
				return Remains.Debris;

			if (IsDebris(piObjectId))
				return Remains.Gone;

			return (UWRandom.Next(0x8000) & 3) == 0 ? Remains.DebrisWithSmoke : Remains.Debris;
		}

		/// <summary>0xD5 or 0xD6 (the game computes RNG * 2 / 0x8000).</summary>
		public static int RollDebrisObjectId()
		{
			return FirstDebrisObjectId + ((UWRandom.Next(0x8000) * DebrisVariants) / 0x8000);
		}

		/// <summary>The quality of a fresh pile of debris: the destroyed object keeps its
		/// quality when its id changes (remove_object's SET_ITEM), and damage_object has just
		/// worn that to 0. One value for the blast and the smashed container since 2026-10-12
		/// (cleanup 2.6; until then 0 and "unchanged", which a fresh object makes 0 as well).</summary>
		public const int DebrisQuality = 0;

		/// <summary>The range remove_object hands the culling test for a destroyed bag -
		/// Obj_Elem_Fate(10, obj), the liquids' ten.</summary>
		public const int BagCullingRangeArgument = UWLiquidCulling.RangeBase;

		/// <summary>
		/// Whether a destroyed bag, box or pouch is LEFT AS IT IS: remove_object asks the
		/// culling test (UWObjectLimitRules.CullingTest, range 10 plus 0 to 2) and leaves the
		/// bag alone, worn to quality 0 and with its contents, when the test spares it - a bag
		/// with word 0 bit 13, or valuable enough itself, or holding something that is. Only a
		/// bag the test condemns spills and becomes debris. Until 2026-10-12 ours spilled every
		/// destroyed bag (Todo "Smoke rolled for a bag under fire", the open point).
		/// </summary>
		public static bool DestroyedBagIsSpared(UWObject pOBag, UWCommonObjectProperties pOProperties,
			System.Collections.Generic.List<UWObject> pOMaster)
		{
			return !UWObjectLimitRules.CullingTest(pOBag, BagCullingRangeArgument, pOProperties, pOMaster,
				fRandomDraw);
		}

		private static int fRandomDraw()
		{
			return UWRandom.Next(UWOriginalRandom.Range);
		}

		/// <summary>How long the smoke stays, in seconds - see AnimationTicksPerSecond.</summary>
		public static float RollSmokeSeconds()
		{
			return UWRandom.RollDice(SmokeDiceCount, SmokeDiceRange) / AnimationTicksPerSecond;
		}
	}
}
