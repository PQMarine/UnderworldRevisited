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
	/// THE REMAINS: DamageObject_Debris_seg023_D6 opens a door, spills chest and barrel and
	/// class 8 containers, and turns everything else into a pile of debris, 0xD5 or 0xD6, its
	/// contents gone. With FIRE (type bit 8) a pile of debris that is struck again vanishes, and
	/// any other object puts up the smoke (SpawnClass7Object with offset 8) one time in four
	/// before it becomes debris.
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
		public const int FirstDebrisObjectId = 0xD5;

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
		/// Wears an object down. The damage has already been scaled by the resistances; it is
		/// halved once per quality class and taken off the quality, which never drops below 0.
		/// Returns true when the object is destroyed by it.
		/// </summary>
		public static bool Wear(int piQuality, int piDamage, int piQualityClass, bool pbProtected,
			out int piNewQuality)
		{
			piNewQuality = piQuality;

			if (pbProtected || piQualityClass >= ImmuneQualityClass)
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

		/// <summary>How long the smoke stays, in seconds - see AnimationTicksPerSecond.</summary>
		public static float RollSmokeSeconds()
		{
			return UWRandom.RollDice(SmokeDiceCount, SmokeDiceRange) / AnimationTicksPerSecond;
		}
	}
}
