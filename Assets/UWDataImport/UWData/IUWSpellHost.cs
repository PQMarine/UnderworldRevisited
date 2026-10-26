namespace UWDataImport.UWData
{
	/// <summary>
	/// What the spell rules (UWSpellCasting, UWMiscSpellRules, UWAreaSpellRules,
	/// UWSummonSpellRules, UWItemSpellRules) need from ONE CAST: who casts, the caster's numbers,
	/// the pending projectile and target cursors, the active spells and the spell icons - and the
	/// world as this cast sees it. The player's is UWSpellHost on the Unity side, built per cast
	/// by UWHudRunes; a creature's is the engine-free UWCreatureSpellHost, built per cast by
	/// UWCritter (since cleanup step 4.10, 2026-10-18; a UWSpellHost with the creature as the
	/// area caster until then).
	///
	/// THE WORLD (cleanup step 3.6, 2026-10-17): the level, the player's tile and view, the
	/// messages, sounds, shake and effect pictures, the travel, the objects a spell makes, the
	/// creatures Detect Monsters finds and the targets of an area spell are IUWWorld's; World is
	/// the world with this cast's presentation (its lines go to the Interaction the cast was given,
	/// silent without one). Until then this interface had about fifty members, most of them the
	/// world's, and the area targets were numbered entries on the host, valid between a collection
	/// and its use, with the creature caster as a mutable property (AreaCaster). Now the caster is
	/// a value (UWCastContext), and since cleanup step 4.0 (2026-10-18) the rule walks the squares'
	/// object lists for the targets itself (UWAreaWalk; a list of physics hits, UWAreaTarget, in
	/// between).
	///
	/// The target spells that go onto a CLICKED creature or item (UWTargetSpell, the target
	/// half of UWMiscSpell) stay on the Unity side: they are nothing but component calls.
	/// </summary>
	public interface IUWSpellHost
	{
		/// <summary>The world with this cast's presentation; never null while a level is loaded
		/// (a cast without a level loader has none).</summary>
		IUWWorld World { get; }

		/// <summary>Who casts: the player, or the creature whose spell this is.</summary>
		UWCastContext Cast { get; }

		/// <summary>The data the caster was handed (kept as given, not the world's).</summary>
		DataImport Data { get; }

		/// <summary>The void, or a tile with the no-magic bit under the caster
		/// (UWEndgameRules.IsMagicBlocked) - do_spell's one refusal, which UWSpellCasting.ApplyEffect
		/// asks for every caster.</summary>
		bool IsMagicBlocked { get; }

		// ------------------------------------------------- The caster

		/// <summary>The player casts and has a body: the effects that are his only (do_spell's
		/// who == ThePlayer) take place. False for a creature's cast.</summary>
		bool HasPlayer { get; }

		int PlayerLevel { get; }

		/// <summary>Mana in whole points, ROUNDED DOWN (the cost check) - the conversation's
		/// PlayerMana rounds to the nearest; two conventions on purpose (cleanup step 3.1).</summary>
		int PlayerMana { get; }

		int PlayerCastingSkill { get; }

		void SpendMana(int piAmount);

		/// <summary>The game's running time in PIT ticks, 256 a second (UWWorldScheduler.Now), for
		/// the pause between two casts: the original's game clock, which stands while the game
		/// holds (a conversation, a menu). Real seconds until cleanup phase 10 (2026-10-26).</summary>
		long GameTicks { get; }

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
		/// when it is released (0 for a wand or a trap). A creature's is released at once
		/// (do_spell: release_missile for any caster but the player).</summary>
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
	/// WHO CASTS (cleanup step 3.6, 2026-10-17): the player, or a creature whose spell runs
	/// through the same code with it as the caster (UW.EXE does so, read 2026-09-27). The blasts
	/// of the cast are charged to the caster: a creature's damage goes to that creature, so the
	/// victim's damage reaction turns on the caster and no kin alarm is raised as if the player had
	/// struck; the gargoyle eyes stay for the player's own hits. A value the cast carries - until
	/// then the mutable UWSpellHost.AreaCaster, set after the host was built.
	///
	/// WHERE THE CREATURE STANDS AND LOOKS (cleanup step 4.10, 2026-10-18): its record's spot and
	/// 256-step heading, from which the rules measure the cast - gronk_area moves from the
	/// caster's square along its heading, creat_spell from its fine position (SPELLS.C) - and at
	/// which do_spell tests for magic. Until then the creature worked out the area's centre and
	/// the summoning's spot itself and handed the rules the results.
	/// </summary>
	public readonly struct UWCastContext
	{
		/// <summary>Whether a creature casts.</summary>
		public readonly bool ByCreature;

		/// <summary>The casting creature's index in the level's mobile list; 0 for the player.</summary>
		public readonly int CreatureIndex;

		/// <summary>The casting creature's spot from its record (tile, eighths, zpos); Nowhere for
		/// the player, whose spot is his avatar's.</summary>
		public readonly UWSpot CasterSpot;

		/// <summary>The casting creature's heading, 0 to 255 (0 north, clockwise:
		/// UWCritterRecord.FullFacing); 0 for the player, whose view is his avatar's.</summary>
		public readonly int CasterHeading;

		private UWCastContext(bool pbByCreature, int piCreatureIndex, UWSpot pOSpot, int piHeading)
		{
			ByCreature = pbByCreature;
			CreatureIndex = piCreatureIndex;
			CasterSpot = pOSpot;
			CasterHeading = piHeading;
		}

		/// <summary>The player casts (runes, a wand, a trap's spell for him).</summary>
		public static UWCastContext ThePlayer => new UWCastContext(false, 0, UWSpot.Nowhere, 0);

		/// <summary>A creature casts, standing on pOSpot and looking along piHeading.</summary>
		public static UWCastContext Creature(int piCreatureIndex, UWSpot pOSpot, int piHeading)
		{
			return new UWCastContext(true, piCreatureIndex, pOSpot, piHeading & 0xFF);
		}

		/// <summary>Whom the cast's blasts are charged to: the creature's index, else the player's
		/// (UWCritterBrain.PlayerIndex).</summary>
		public int AttackerIndex => ByCreature ? CreatureIndex : UWCritterBrain.PlayerIndex;
	}
}
