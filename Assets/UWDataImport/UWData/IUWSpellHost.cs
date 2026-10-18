namespace UWDataImport.UWData
{
	/// <summary>
	/// What the spell rules (UWSpellCasting, UWMiscSpellRules, UWAreaSpellRules,
	/// UWSummonSpellRules, UWItemSpellRules) need from ONE CAST: who casts, the caster's numbers,
	/// the pending projectile and target cursors, the active spells and the spell icons - and the
	/// world as this cast sees it. The Unity side is UWSpellHost, built per cast (UWHudRunes for
	/// the player, UWCritter for a creature's area spell or summoning).
	///
	/// THE WORLD (cleanup step 3.6, 2026-10-17): the level, the player's tile and view, the
	/// messages, sounds, shake and effect pictures, the travel, the objects a spell makes, the
	/// creatures Detect Monsters finds and the targets of an area spell are IUWWorld's; World is
	/// the world with this cast's presentation (its lines go to the Interaction the cast was given,
	/// silent without one). Until then this interface had about fifty members, most of them the
	/// world's, and the area targets were numbered entries on the host, valid between a collection
	/// and its use, with the creature caster as a mutable property (AreaCaster). Now the targets
	/// are a list the rule holds (UWAreaTarget) and the caster a value (UWCastContext).
	///
	/// The target spells that go onto a CLICKED creature or item (UWTargetSpell, the target
	/// half of UWMiscSpell) stay on the Unity side: they are nothing but component calls.
	/// </summary>
	public interface IUWSpellHost
	{
		/// <summary>The world with this cast's presentation; never null while a level is loaded
		/// (a cast without a level loader has none).</summary>
		IUWWorld World { get; }

		/// <summary>Who casts: the player, or the creature whose area spell this is.</summary>
		UWCastContext Cast { get; }

		/// <summary>The data the caster was handed (kept as given, not the world's).</summary>
		DataImport Data { get; }

		/// <summary>The void, or a tile with the no-magic bit under the caster.</summary>
		bool IsMagicBlocked { get; }

		// ------------------------------------------------- The caster

		bool HasPlayer { get; }

		int PlayerLevel { get; }

		/// <summary>Mana in whole points, ROUNDED DOWN (the cost check) - the conversation's
		/// PlayerMana rounds to the nearest; two conventions on purpose (cleanup step 3.1).</summary>
		int PlayerMana { get; }

		int PlayerCastingSkill { get; }

		void SpendMana(int piAmount);

		/// <summary>Real seconds since some start, for the pause between two casts.</summary>
		double RealSeconds { get; }

		void RestoreManaFromSpell(int piMinorClass);

		void Heal(int piAmount);

		void HealFully();

		/// <summary>False when all three effect slots are taken.</summary>
		bool TryAddActiveSpell(int piMajorClass, int piMinorClass, int piStability);

		/// <summary>The small hop of Levitate and Fly (phys_bounce_up): on the ground the player
		/// gets a little upward speed.</summary>
		void BouncePlayerUp();

		/// <summary>Roaming Sight cast with no free slot: the view detaches all the same, with no
		/// active spell to end it (see UWRoamingSight).</summary>
		void BeginRoamingSightWithoutSpell();

		void CurePoison();

		/// <summary>The hallucination counter of the player to its maximum - see
		/// UWItemSpellRules.</summary>
		void StartHallucination();

		// ------------------------------------------------- Sounds and icons

		void PlaySpellSound();

		void PlaySpellFailureSound();

		/// <summary>The three spell icons in the HUD show the active effects anew.</summary>
		void RefreshSpellIcons();

		// ------------------------------------------------- Pending spells

		/// <summary>A projectile waits for the click that gives its direction. piManaCost is paid only
		/// when it is released (0 for a wand or a trap).</summary>
		void BeginProjectileSpell(int piProjectileId, int piDamage, int piSpeed, int piManaCost);

		/// <summary>A spell of class 7 or 11 waits for the click that names its target.</summary>
		void BeginTargetSpell(int piMajorClass, int piMinorClass);

		/// <summary>Sends the pending projectile off at once.</summary>
		void FirePendingSpell();

		// ------------------------------------------------- What goes through this cast's channel

		/// <summary>Armageddon: inventory, runes, rune shelf and the whole level are cleared,
		/// the character's status refreshed.</summary>
		void Armageddon();

		/// <summary>One action of the bullfrog puzzle, as its a_do traps fire it (mode 4 is the
		/// reset) - see UWItemSpellRules. The firing's messages go to this cast's channel.</summary>
		void FireBullfrog(int piMode);

		/// <summary>Fires the look trigger of an object, as Reveal does it - with this cast's
		/// channel.</summary>
		bool FireLookTrigger(UWObject pOObject, int piSearchSkill);
	}

	/// <summary>
	/// WHO CASTS (cleanup step 3.6, 2026-10-17): the player, or a creature whose area spell runs
	/// through the same code with it as the caster (UW.EXE does so, read 2026-09-27). The blasts
	/// of the cast are charged to the caster: a creature's damage goes to that creature, so the
	/// victim's damage reaction turns on the caster and no kin alarm is raised as if the player had
	/// struck; the gargoyle eyes stay for the player's own hits. A value the cast carries - until
	/// then the mutable UWSpellHost.AreaCaster, set after the host was built.
	/// </summary>
	public readonly struct UWCastContext
	{
		/// <summary>Whether a creature casts.</summary>
		public readonly bool ByCreature;

		/// <summary>The casting creature's index in the level's mobile list; 0 for the player.</summary>
		public readonly int CreatureIndex;

		private UWCastContext(bool pbByCreature, int piCreatureIndex)
		{
			ByCreature = pbByCreature;
			CreatureIndex = piCreatureIndex;
		}

		/// <summary>The player casts (runes, a wand, a trap's spell for him).</summary>
		public static UWCastContext ThePlayer => new UWCastContext(false, 0);

		/// <summary>A creature casts.</summary>
		public static UWCastContext Creature(int piCreatureIndex)
		{
			return new UWCastContext(true, piCreatureIndex);
		}

		/// <summary>Whom the cast's blasts are charged to: the creature's index, else the player's
		/// (UWCritterBrain.PlayerIndex).</summary>
		public int AttackerIndex => ByCreature ? CreatureIndex : UWCritterBrain.PlayerIndex;
	}

	/// <summary>
	/// ONE THING AN AREA SPELL MAY STRIKE (cleanup step 3.6, 2026-10-17): its tile and whether it
	/// is a creature, with the scene's own handle to act on it (IUWWorld.ConfuseAreaTarget and the
	/// two after it). The world collects them (IUWWorld.CollectAreaTargets) and the rule holds the
	/// list for the cast - until then the spell host kept them as numbered entries, valid between a
	/// collection and its use.
	/// </summary>
	public readonly struct UWAreaTarget
	{
		/// <summary>The tile it belongs to.</summary>
		public readonly UWTilePos Tile;

		/// <summary>A creature with its object - the only kind the rules act on.</summary>
		public readonly bool IsCreature;

		/// <summary>The scene's handle (opaque to the rules).</summary>
		public readonly object Body;

		public UWAreaTarget(UWTilePos pOTile, bool pbIsCreature, object pOBody)
		{
			Tile = pOTile;
			IsCreature = pbIsCreature;
			Body = pOBody;
		}
	}
}
