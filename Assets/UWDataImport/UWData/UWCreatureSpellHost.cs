using System;

namespace UWDataImport.UWData
{
	/// <summary>
	/// A CREATURE'S CAST (cleanup step 4.10, 2026-10-18; B2 and B3 of Docs/CLEANUP-PLAN.md): the
	/// spell host a creature hands UWSpellCasting.CastSpell, so its spells take the one effect path
	/// the player's runes, wands and the spell traps take (UWSpellCasting.ApplyEffect), as the
	/// original's do take one: the mind's cast sequence calls cast(spells[i], critter, 0) at frame 4
	/// (critter_ai, UnderworldExhumed AI.C), and cast hands it to do_spell (SPELLS.C), the dispatcher
	/// for every caster. Until then UWCritter dispatched a creature's spell itself - the missile,
	/// the area spell and Summon Monster each by a path of its own - and only the last two refused
	/// in the Void.
	///
	/// WHAT do_spell DOES FOR A CREATURE, and so this host:
	///   - the refusal first: an anti-magic square under the creature, or level 9 (IsMagicBlocked,
	///     UWEndgameRules.IsMagicBlocked at the caster's tile) - nothing a creature casts works in
	///     the Void (the Exhumed audit's row 30: its missile spells flew there until 2026-10-18);
	///   - classes 0 to 3, the lasting spells, are the player's only (who == ThePlayer), and so are
	///     class 7 (nail_1area), 10 (restore_mana) and 11 (xt_spells): HasPlayer is false;
	///   - class 4 heals the target, and a creature's cast has none (cast passes 0);
	///   - class 5 releases the missile at once (release_missile -> spell_fire, a critter firing
	///     straight ahead): BeginProjectileSpell hands it to the creature's launcher;
	///   - class 6 strikes the area four squares along the creature's heading (nail_area ->
	///     gronk_area with the caster's index as the source), measured from the creature
	///     (UWCastContext.CasterSpot and CasterHeading) - B3: until 2026-10-11 the area's targets
	///     were sorted by the player's distance even when a creature cast, and until 2026-10-18 the
	///     creature worked out the centre itself;
	///   - class 8 creates (creat_spell): Summon Monster from the creature's fine position along its
	///     heading, hostile and sent at the player (UWSummonSpellRules.SummonByCreature).
	///
	/// THE SPELLS CREATURES HOLD (OBJECTS.DAT's creature table, bytes 0x2A to 0x2C, read
	/// 2026-10-18): 0x02 Magic Arrow, 0x0F Electrical Bolt, 0x18 Fireball, 0x31 Acid (class 5);
	/// 0x1F Sheet Lightning, 0x2D Flame Wind (class 6); 0x26 Summon Monster (class 8); 0x08 and
	/// 0x13, heals (class 4); 0x00 Light (class 0, the third spell of 0x66); 0x2B Tremor and 0x32
	/// (class 11). None holds Create Food, the Rune of Warding, Reveal, Confusion, a class 13 spell
	/// (spells[] has none, and a creature casts only from spells[]) or Levitate and Fly (whose
	/// phys_bounce_up do_spell would give the creature) - this host builds none of them: their
	/// members do nothing here.
	///
	/// The comments of the paths it replaces stay with UWCritter.OnSpellCast.
	///
	/// PER CAST, engine-free: the world, the cast's value and the creature's missile launcher; the
	/// self-check builds it on its fake world.
	/// </summary>
	public sealed class UWCreatureSpellHost : IUWSpellHost
	{
		private readonly Action<int> mOReleaseMissile;

		/// <param name="pIWorld">The world the creature is in.</param>
		/// <param name="pOCast">The creature, its spot and heading (UWCastContext.Creature).</param>
		/// <param name="pOReleaseMissile">Releases the spell missile of this object id from the
		/// creature (spell_fire); null to release none.</param>
		public UWCreatureSpellHost(IUWWorld pIWorld, UWCastContext pOCast, Action<int> pOReleaseMissile)
		{
			World = pIWorld;
			Cast = pOCast;
			mOReleaseMissile = pOReleaseMissile;
		}

		public IUWWorld World { get; }

		public UWCastContext Cast { get; }

		/// <summary>The world's data - the creature's loader's, which the old paths took.</summary>
		public DataImport Data => World != null ? World.Data : null;

		/// <summary>do_spell's refusal at the creature's own square: its anti-magic bit, or the Void
		/// (UWEndgameRules.IsMagicBlocked, the one home of the rule).</summary>
		public bool IsMagicBlocked => World != null
			&& UWEndgameRules.IsMagicBlocked(World.Level, World.LevelIndex, Cast.CasterSpot.Tile);

		// ------------------------------------------------- The caster: none of the player's

		public bool HasPlayer => false;

		public int PlayerLevel => 0;

		public int PlayerMana => 0;

		public int PlayerCastingSkill => 0;

		public void SpendMana(int piAmount)
		{
		}

		public double RealSeconds => 0d;

		public void RestoreManaFromSpell(int piMinorClass)
		{
		}

		public void Heal(int piAmount)
		{
		}

		public void HealFully()
		{
		}

		public bool TryAddActiveSpell(int piMajorClass, int piMinorClass, int piStability)
		{
			return false;
		}

		public void BouncePlayerUp()
		{
		}

		public void BeginRoamingSightWithoutSpell()
		{
		}

		public void CurePoison()
		{
		}

		public void StartHallucination()
		{
		}

		// ------------------------------------------------- Sounds and icons: the player's cast only

		/// <summary>Silent: player_cast plays the cast sound after do_spell (RUNES.C), the mind's
		/// cast plays none.</summary>
		public void PlaySpellSound()
		{
		}

		public void PlaySpellFailureSound()
		{
		}

		public void RefreshSpellIcons()
		{
		}

		// ------------------------------------------------- The missile

		/// <summary>release_missile: a creature's missile goes off at once, straight ahead at the
		/// pitch its mind worked out (spell_fire). The damage and speed are the creature launcher's
		/// own (the table's damage rolled with charge 0x80, as before); a creature pays no mana.</summary>
		public void BeginProjectileSpell(int piProjectileId, int piDamage, int piSpeed, int piManaCost)
		{
			mOReleaseMissile?.Invoke(piProjectileId);
		}

		public void BeginTargetSpell(int piMajorClass, int piMinorClass)
		{
		}

		/// <summary>Nothing waits: BeginProjectileSpell released it.</summary>
		public void FirePendingSpell()
		{
		}

		// ------------------------------------------------- What the player's cast alone does

		public void Armageddon()
		{
		}

		public void FireBullfrog(int piMode)
		{
		}

		public bool FireLookTrigger(UWObject pOObject, int piSearchSkill)
		{
			return false;
		}
	}
}
