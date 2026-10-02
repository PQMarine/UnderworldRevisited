namespace UWDataImport.UWData
{
	/// <summary>
	/// Hit points of something that can take damage - critters, doors, barrels and chests - and
	/// the rules for taking damage. Engine-free since 2026-09-17 (P2 of the engine separation);
	/// UWDamageable on the Unity side holds one and raises its events.
	///
	/// The hit points come from the game data, not from an estimate: for critters the vitality from
	/// the critter table in OBJECTS.DAT, for doors the quality of the object, which in the original
	/// also carries the displayed condition (see UWCombat.GetConditionStringIndex).
	/// </summary>
	public sealed class UWHealth
	{
		private int miMaxHealth = 1;
		private int miCurrentHealth = 1;
		private int miInitialHealth = 1;

		public int MaxHealth => miMaxHealth;

		public int CurrentHealth => miCurrentHealth;

		/// <summary>Armour: subtracted from the damage. For critters taken from the table.</summary>
		public int Armour { get; private set; }

		/// <summary>Defence value from the critter table. Target value of the hit roll - the higher,
		/// the harder to hit.</summary>
		public int Defence { get; private set; }

		/// <summary>Quality type from COMOBJ.DAT byte 10 - it selects the group of six condition
		/// words, see UWCombat.GetConditionStringIndex.</summary>
		public int QualityType { get; private set; }

		/// <summary>
		/// Quality CLASS from COMOBJ.DAT byte 6 (bits 2-3), not to be confused with the quality TYPE.
		/// It is how sturdy a thing is built: every point halves the damage (reference:
		/// damage.DamageOtherObjectTypes, basedamage shifted right by the quality class). A chest is
		/// class 2 and therefore takes a quarter of every blow, a barrel class 1 and half of it -
		/// which is why they hold out so much longer in the original than their 40 quality points
		/// suggest (per user, 2026-09-16). Class 3 cannot be damaged at all.
		/// </summary>
		public int QualityClass { get; private set; }

		/// <summary>Resistance byte from comobj.dat - see UWDamageTypes.</summary>
		public int Resistances { get; private set; }

		public bool IsDestroyed => miCurrentHealth <= 0;

		/// <summary>Fraction of the remaining hit points, 0 to 1.</summary>
		public float HealthFraction => miMaxHealth <= 0 ? 0f : miCurrentHealth / (float)miMaxHealth;

		/// <summary>How many points it was created with. Saving writes hit points back only if they
		/// differ from this or the data itself carried some.</summary>
		public int InitialHealth => miInitialHealth;

		/// <summary>Quality class 3: takes no damage at all. It still has hit points, because the
		/// look message reads its condition from them.</summary>
		public bool IsIndestructible => QualityClass == UWCombat.AlwaysBestQualityClass || IsProtected;

		/// <summary>
		/// WORD 0 BIT 13 PROTECTS (read 2026-09-27; per user: "the protection bit on doors"):
		/// DamageObjectAndDoors, which every blow of the player and of a creature and every blast
		/// runs through for an object that is not a creature, returns at once when that bit is set
		/// - no quality lost, nothing destroyed. In LEV.ARK 48 of 201 doors carry it, wooden ones of
		/// every class among them, and a few barrels and chests; ours could break them. It is
		/// the same bit that turns a door's swing (UWObject.DoorDirection). The condition the look
		/// message names still follows the class and the hit points, as for class 3.
		/// </summary>
		public bool IsProtected { get; set; }

		/// <summary>Whether the target is undead. For Smite Undead and Repel Undead.</summary>
		public bool IsUndead => UWDamageTypes.IsUndead(Resistances);

		/// <summary>Whether a blow can land at all: not yet destroyed and not indestructible.</summary>
		public bool CanTakeDamage => !IsDestroyed && !IsIndestructible;

		/// <summary>Sets hit points, armour, quality type, defence and resistances. piCurrentHealth:
		/// how many points it starts with - for critters those from the object data (npc_hp). Full
		/// if not given.</summary>
		public void Initialise(int piMaxHealth, int piArmour, int piQualityType, int piDefence = 0, int piResistances = 0,
			int piCurrentHealth = -1, int piQualityClass = 0)
		{
			QualityClass = piQualityClass < 0 ? 0 : piQualityClass > 3 ? 3 : piQualityClass;
			miMaxHealth = piMaxHealth < 1 ? 1 : piMaxHealth;
			miCurrentHealth = piCurrentHealth > 0 ? (piCurrentHealth < miMaxHealth ? piCurrentHealth : miMaxHealth) : miMaxHealth;
			miInitialHealth = miCurrentHealth;
			Armour = piArmour;
			QualityType = piQualityType;
			Defence = piDefence;
			Resistances = piResistances;
		}

		/// <summary>
		/// Subtracts damage and reports whether the target was destroyed by it. The caller checks
		/// CanTakeDamage first.
		///
		/// The DAMAGE TYPE decides whether the blow lands at all: a fire elemental is immune to fire,
		/// a ghost to poison (see UWDamageTypes). If not given, nothing is checked.
		/// </summary>
		public bool ApplyDamage(int piDamage, int piDamageType = UWDamageTypes.None)
		{
			piDamage = UWDamageTypes.Scale(Resistances, piDamage, piDamageType);

			// How sturdily the thing is built halves the blow per class. Critters carry class 0.
			piDamage >>= QualityClass;

			miCurrentHealth -= piDamage < 0 ? 0 : piDamage;

			if (miCurrentHealth > 0)
				return false;

			miCurrentHealth = 0;

			return true;
		}

		/// <summary>Sets the hit points outright, 1 up to the maximum - for rules that change them
		/// without a blow, as breaking Tybal's orb halves his (UWTybalOrbRules). Not for a kill.
		/// </summary>
		public void SetCurrentHealth(int piHealth)
		{
			miCurrentHealth = System.Math.Max(1, System.Math.Min(miMaxHealth, piHealth));
		}

		/// <summary>
		/// Index of the condition word in string block 5. For the look message of a door.
		///
		/// Uses the CURRENT hit points, not the fraction: for a door hit points and quality are the
		/// same number. The fraction turned every undamaged door into one with quality 63 and thus
		/// massive, although the savegame holds about 40 and the original reports sturdy (per user,
		/// 2026-09-04).
		///
		/// WITH THE QUALITY CLASS: class 3 - the metal door, the portcullis - always gets the top
		/// word, "massive", whatever its hit points (the reference's look.GetDescriptionString does
		/// the same). Without it the metal door on level 2, tile 61/6 read "sturdy" at quality 40
		/// where the original says "massive" (per user, 2026-09-18).
		/// </summary>
		public int GetConditionStringIndex()
		{
			return UWCombat.GetConditionStringIndex(QualityType, miCurrentHealth, QualityClass);
		}
	}
}
