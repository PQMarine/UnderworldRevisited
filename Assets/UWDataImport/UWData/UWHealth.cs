namespace UWDataImport.UWData
{
	/// <summary>
	/// WHERE A CREATURE'S HIT POINTS LIVE: its record's byte 8 (UWCritterRecord.HitPoints), the one
	/// store since cleanup step 4.8 (2026-10-18; section 7.4 of Docs/CLEANUP-PLAN.md). Until then
	/// UWHealth counted its own copy beside the record, kept in step at six places, and the save took
	/// the copy clamped to at least 1 - so a golem whose death was refused saved with 1 hit point
	/// although his record held 0 (B23).
	/// </summary>
	public interface IUWHitPointStore
	{
		int HitPoints { get; set; }
	}

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

		/// <summary>A creature's record, which holds the hit points instead of miCurrentHealth (see
		/// KeepHitPointsIn); null for a door, a barrel or a chest.</summary>
		private IUWHitPointStore mIStore;

		/// <summary>With a store: the death is committed (see MarkDestroyed).</summary>
		private bool mbDestroyed;

		public int MaxHealth => miMaxHealth;

		public int CurrentHealth => mIStore != null ? mIStore.HitPoints : miCurrentHealth;

		/// <summary>
		/// THE HIT POINTS GO TO THE CREATURE'S RECORD from here on (cleanup step 4.8, 2026-10-18; see
		/// IUWHitPointStore): CurrentHealth reads it, every write goes to it, and destroyed no longer
		/// means "no hit points" but the death committed (MarkDestroyed) - a creature whose death
		/// was refused lives on with 0 hit points, as the golem does until his script sets 125
		/// (UWSpecialDeaths), and still takes blows. MaxHealth and InitialHealth stay as Initialise
		/// set them.
		/// </summary>
		public void KeepHitPointsIn(IUWHitPointStore pIStore)
		{
			mIStore = pIStore;
		}

		/// <summary>Whether the hit points live in a record (KeepHitPointsIn).</summary>
		public bool HasStore => mIStore != null;

		/// <summary>The death is committed: with a store the flag, else the hit points go to 0 - the
		/// creature that drowned or loaded dying, and the one a blow killed (cleanup step 4.8;
		/// UWCritter drained its copy with a blow of its own until then).</summary>
		public void MarkDestroyed()
		{
			if (mIStore != null)
				mbDestroyed = true;
			else
				miCurrentHealth = 0;
		}

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

		public bool IsDestroyed => mIStore != null ? mbDestroyed : miCurrentHealth <= 0;

		/// <summary>Fraction of the remaining hit points, 0 to 1.</summary>
		public float HealthFraction => miMaxHealth <= 0 ? 0f : CurrentHealth / (float)miMaxHealth;

		/// <summary>How many points it was created with. Saving writes hit points back only if they
		/// differ from this or the data itself carried some.</summary>
		public int InitialHealth => miInitialHealth;

		/// <summary>Quality class 3: takes no damage at all. It still has hit points, because the
		/// look message reads its condition from them.</summary>
		public bool IsIndestructible => UWObjectDamageRules.ShrugsOff(IsProtected, QualityClass);

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
			return TakeScaledDamage(ScaleDamage(piDamage, piDamageType));
		}

		/// <summary>
		/// What of a blow reaches the thing: the resistances first (check_res in damage_item),
		/// then halved once per quality class (damage_object, DAMAGE.C). Critters carry class 0.
		/// Rolls for a magic resistance, so it is asked once per blow; ApplyDamage does both
		/// halves, UWDamageable splits them for the spike of a door (2026-10-12).
		/// </summary>
		public int ScaleDamage(int piDamage, int piDamageType = UWDamageTypes.None)
		{
			piDamage = UWDamageTypes.Scale(Resistances, piDamage, piDamageType);
			piDamage >>= QualityClass;

			return piDamage < 0 ? 0 : piDamage;
		}

		/// <summary>Takes damage already scaled by ScaleDamage off the hit points and reports
		/// whether the target was destroyed by it. Not for a creature with a store: its mind takes
		/// the blow (DamageNPC, UWCritterBrain.OnDamaged) and commits the death.</summary>
		public bool TakeScaledDamage(int piDamage)
		{
			int liHealth = CurrentHealth - (piDamage < 0 ? 0 : piDamage);

			fSetHealth(liHealth > 0 ? liHealth : 0);

			return liHealth <= 0;
		}

		/// <summary>Sets the hit points outright, 1 up to the maximum - for rules that change them
		/// without a blow. Not for a kill.</summary>
		public void SetCurrentHealth(int piHealth)
		{
			fSetHealth(System.Math.Max(1, System.Math.Min(miMaxHealth, piHealth)));
		}

		private void fSetHealth(int piHealth)
		{
			if (mIStore != null)
				mIStore.HitPoints = piHealth;
			else
				miCurrentHealth = piHealth;
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
			return UWCombat.GetConditionStringIndex(QualityType, CurrentHealth, QualityClass);
		}
	}
}
