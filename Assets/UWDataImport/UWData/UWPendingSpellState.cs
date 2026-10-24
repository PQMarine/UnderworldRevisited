namespace UWDataImport.UWData
{
	/// <summary>
	/// A SPELL WAITING FOR ITS TARGET (cleanup step 5.14, 2026-10-22: the fields were Interaction's
	/// until then). A cast projectile waits for the click that gives its DIRECTION, a target spell
	/// for the clicked OBJECT - major class 7 targets creatures (UWTargetSpell), 11 items and doors
	/// (UWMiscSpell). The original sets its own mouse cursor for both and acts on the next click.
	///
	/// This is the state and the rules on it: what waits, the mana a released projectile pays,
	/// Escape letting the spell go, and what a click into the inventory does to a waiting target
	/// spell - B17, DECIDED 2026-10-22 per user: both schemes keep their behaviour, so the one rule
	/// carries a per-scheme policy (InventoryPolicy) instead of the two code paths of UWItemDrag and
	/// UWModernBags. The aim, the reach, the launch and the cast are the Unity side's
	/// (UWPendingSpell); nothing here reads the engine.
	/// </summary>
	public sealed class UWPendingSpellState
	{
		/// <summary>Nothing waits.</summary>
		public const int None = -1;

		/// <summary>How a click into the inventory treats a waiting target spell (B17).</summary>
		public enum InventoryPolicy
		{
			/// <summary>
			/// THE ORIGINAL'S (per user, 2026-09-10), the classic scheme: a click into the inventory -
			/// with any button - consumes the spell, even if it hits nothing valid, and the click
			/// still does what it otherwise does (UWItemDrag).
			/// </summary>
			Classic,

			/// <summary>The modern bags (UWModernBags): only a click on an item takes the spell;
			/// any other click leaves it waiting.</summary>
			Modern
		}

		/// <summary>What a click into the inventory did to the waiting target spell.</summary>
		public enum InventoryClickOutcome
		{
			/// <summary>No target spell waits; the click is the inventory's alone.</summary>
			NotWaiting,

			/// <summary>The spell is gone onto the clicked item - a class 11 spell casts on it,
			/// any other is used up without a word.</summary>
			CastOnItem,

			/// <summary>No item under the click and the spell is gone, silently (Classic).</summary>
			Lost,

			/// <summary>No item under the click and the spell keeps waiting (Modern).</summary>
			KeepsWaiting
		}

		/// <summary>
		/// The projectile object of a cast projectile spell that waits for the click that gives
		/// the direction, or None.
		///
		/// The original sets its own mouse cursor for this and only releases the projectile on the
		/// next click. For us the number of the projectile object and its damage are kept
		/// ready until the click.
		/// </summary>
		public int ProjectileId { get; private set; } = None;

		/// <summary>Its base damage, rolled at the hit (cleanup step 4.9b, 2026-10-18).</summary>
		public int ProjectileBaseDamage { get; private set; }

		/// <summary>The ammunition table's speed byte of the waiting projectile (RangedAmmoType).</summary>
		public int ProjectileSpeedByte { get; private set; }

		/// <summary>The mana a rune-cast projectile costs, paid only when it is released; when
		/// there is no room nothing is paid - the original's order, see UWPendingSpell.fLaunchSpellProjectile.
		/// Zero for a wand or a trap.</summary>
		public int ManaCost { get; private set; }

		/// <summary>A class 7 spell waits for the object that gets clicked.
		/// None means none.</summary>
		public int TargetMinor { get; private set; } = None;

		/// <summary>Which major class the waiting target spell belongs to - 7 or 11, see
		/// UWPendingSpell.fApplyTargetSpell.</summary>
		public int TargetMajor { get; private set; } = None;

		/// <summary>Is a spell currently running and waiting for its target? This applies to both
		/// kinds: a projectile waits for the DIRECTION, a class 7 spell for a
		/// clicked OBJECT.</summary>
		public bool IsTargeting => ProjectileId >= 0 || TargetMinor >= 0;

		/// <summary>Is a spell waiting for an object instead of a direction? The cursor is
		/// a different one then - see UWPendingSpell.Cursor.</summary>
		public bool IsTargetSpellPending => TargetMinor >= 0;

		/// <summary>
		/// Remembers the projectile until the player clicks the direction (the table value is the
		/// base for the roll at the hit - see UWPendingSpell.BeginProjectile).
		/// </summary>
		public void BeginProjectile(int piProjectileId, int piBaseDamage, int piSpeedByte, int piManaCost)
		{
			ProjectileId = piProjectileId;
			ManaCost = piManaCost;
			ProjectileBaseDamage = piBaseDamage;
			ProjectileSpeedByte = piSpeedByte;
		}

		/// <summary>Remembers a target spell until the player clicks its target.</summary>
		public void BeginTarget(int piMajorClass, int piMinorClass)
		{
			TargetMajor = piMajorClass;
			TargetMinor = piMinorClass;
		}

		/// <summary>The projectile no longer waits - it flew, found no room, or a spell trap's
		/// could not take off. Its mana stays as it is (paid by TakeManaCost on a launch).</summary>
		public void ForgetProjectile()
		{
			ProjectileId = None;
		}

		/// <summary>Released: the mana to pay now, and none left to pay (see ManaCost).</summary>
		public int TakeManaCost()
		{
			int liCost = ManaCost;

			ManaCost = 0;

			return liCost;
		}

		/// <summary>Escape in the modern scheme: a waiting spell is let go (a projectile's mana was not
		/// paid yet, a target spell's was).</summary>
		public void Cancel()
		{
			ProjectileId = None;
			ManaCost = 0;
			TargetMinor = None;
			TargetMajor = None;
		}

		/// <summary>The waiting target spell is spent: its classes, and nothing waits any more.
		/// False when none waited.</summary>
		public bool TakeTarget(out int piMinor, out int piMajor)
		{
			piMinor = TargetMinor;
			piMajor = TargetMajor;

			if (TargetMinor < 0)
				return false;

			TargetMinor = None;
			TargetMajor = None;

			return true;
		}

		/// <summary>
		/// B17, THE ONE RULE: what a click into the inventory does to a waiting target spell. On an
		/// item both schemes give it the spell; on no item the classic scheme loses it, as the
		/// original does, and the modern one keeps it waiting. A projectile spell has no business in
		/// the inventory - it does not count here.
		/// </summary>
		public static InventoryClickOutcome DecideInventoryClick(InventoryPolicy pePolicy, bool pbTargetSpellWaits,
			bool pbItemClicked)
		{
			if (!pbTargetSpellWaits)
				return InventoryClickOutcome.NotWaiting;

			if (pbItemClicked)
				return InventoryClickOutcome.CastOnItem;

			return pePolicy == InventoryPolicy.Classic ? InventoryClickOutcome.Lost : InventoryClickOutcome.KeepsWaiting;
		}

		/// <summary>DecideInventoryClick applied to this state: CastOnItem and Lost spend the target
		/// spell (its classes come out for the cast), the others leave it as it is.</summary>
		public InventoryClickOutcome ResolveInventoryClick(InventoryPolicy pePolicy, bool pbItemClicked,
			out int piMinor, out int piMajor)
		{
			InventoryClickOutcome leOutcome = DecideInventoryClick(pePolicy, IsTargetSpellPending, pbItemClicked);

			piMinor = None;
			piMajor = None;

			if (leOutcome == InventoryClickOutcome.CastOnItem || leOutcome == InventoryClickOutcome.Lost)
				TakeTarget(out piMinor, out piMajor);

			return leOutcome;
		}
	}
}
