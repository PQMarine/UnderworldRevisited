namespace UWDataImport.UWData
{
	/// <summary>
	/// WHAT A CREATURE ASKS OF THE WORLD (cleanup step 4.11, 2026-10-18; section 7.4 of
	/// Docs/CLEANUP-PLAN.md): the level's part that its engine-free actor (UWCreatureActor) cannot
	/// answer from the data alone - the motion engines, the other creatures' actors, the player's
	/// body as the mind sees it and as a blow's defender, the melee defender search, the missile
	/// launch, the talk, the music. Everything the rules already have a world member for - the
	/// level and its tiles, the data, the doors, the blows (Damage), the presentation, the player's
	/// vitals - the actor takes from World (IUWWorld) and is not repeated here.
	///
	/// ONE PER LEVEL: the Unity implementation is UWCreatureWorld, owned by the level's UWWorld
	/// (UWWorld.Creatures); the self-check has a fake on FakeWorld. It keeps no state of one
	/// creature: each member is handed the actor it is about.
	/// </summary>
	public interface ICreatureWorld
	{
		/// <summary>The world of the level: data, level, tiles, doors, blows, presentation, the
		/// player's avatar.</summary>
		IUWWorld World { get; }

		// ------------------------------------------------- The motion engines (the level's)

		/// <summary>The creature motion core of the level (UWCreatureMotion on the level's motion
		/// world); null without one.</summary>
		UWCreatureMotion Motion { get; }

		/// <summary>The level's shared path search (flood_path, beeline).</summary>
		UWTileRoute Route { get; }

		/// <summary>The level's shared triple test; null without one (no route then either).</summary>
		UWTileTraverse Traverse { get; }

		/// <summary>A lying object in the motion core's collision scan carries a transient index of
		/// the motion world (4096 and up, UWMotionHost.FirstStaticIndex): true for such an index,
		/// with its level object (null when it is gone); false for a master-list index.</summary>
		bool TryGetStaticObject(int piIndex, out UWObject pOObject);

		/// <summary>The creature's object changed its tile: relinked into the new tile's list.</summary>
		void MoveObjectData(UWNpc pONpc, int piTileX, int piTileY);

		// ------------------------------------------------- The other creatures

		/// <summary>The actor of the creature this level object is (its entity's creature); null for
		/// an object that is no creature's or a creature without an actor.</summary>
		UWCreatureActor ActorOf(UWObject pOObject);

		// ------------------------------------------------- The player

		/// <summary>The player's body is there (UWCharacter in the scene).</summary>
		bool HasPlayer { get; }

		/// <summary>The player as the mind sees him: position in eighths, feet, height, facing,
		/// loudness and visibility, hit points, the drawn weapon. Exists false without a player.</summary>
		CritterTarget SnapshotPlayer();

		/// <summary>The tile the view is drawn from - see ICritterHost.ViewTile.</summary>
		UWTilePos ViewTile { get; }

		/// <summary>The player as a blow's defender, his status refreshed first: the defender
		/// (UWPlayerCombatant), his feet and his body's height in zpos. False without a player.</summary>
		bool TryGetPlayerAsDefender(out IUWAttackDefender pIDefender, out int piFeetZ, out int piBodyHeightZ);

		// ------------------------------------------------- Melee (CheckForAttackHit)

		/// <summary>The defender of this creature's blow: the nearest thing its reach probe meets
		/// along its full facing - the player, a creature, a door or another object with hit
		/// points, or the world - or nothing (see UWCreatureWorld.FindMeleeDefender).</summary>
		UWMeleeContact FindMeleeDefender(UWCreatureActor pOAttacker);

		/// <summary>The door of this door object as the object a creature bashes; Kind Nothing when
		/// it has no body to take the bash.</summary>
		UWMeleeContact DoorContact(UWObject pODoorData);

		/// <summary>The pictures of a blow on the player: on a hit sound 3 at the avatar with the
		/// outcome's volume, on a miss the blocked sound at his body.</summary>
		void ShowBlowOnPlayer(in UWAttackOutcome pOOutcome);

		/// <summary>The impact flash and sound 4 with this volume where the blow met the object (a
		/// door's at its own height, towards the viewer).</summary>
		void ShowBlowOnObject(UWCreatureActor pOAttacker, in UWMeleeContact pOContact, int piVolume);

		/// <summary>The impact flash and sound where the blow met the world (the miss sound is the
		/// attacker's view's).</summary>
		void ShowBlowOnWorld(UWCreatureActor pOAttacker, in UWMeleeContact pOContact);

		/// <summary>Sound 4 at the bashed door with this volume.</summary>
		void ShowDoorBashed(in UWMeleeContact pOContact, int piVolume);

		// ------------------------------------------------- Missiles

		/// <summary>A missile of this object id from the creature along its facing at this pitch, on
		/// the motion core (UWMissiles.LaunchFromCreature). False when the start has no room - the
		/// original makes no missile then.</summary>
		bool LaunchMissile(UWCreatureActor pOLauncher, int piObjectId, int piPitch, int piBaseDamage, int piDamageType,
			int piImpactId);

		// ------------------------------------------------- Talk and music

		/// <summary>A conversation can start now: none is open and the interaction is there.</summary>
		bool CanTalk { get; }

		/// <summary>The creature's conversation with the player (Interaction.TryTalkToNpcData).</summary>
		void Talk(UWNpc pONpc);

		/// <summary>The theme playing now (UWMusic.CurrentTheme).</summary>
		int MusicTheme { get; }

		/// <summary>A blow was struck: the combat music timer starts anew (UWMusic.MarkCombat).</summary>
		void MarkCombatMusic();
	}

	/// <summary>What a creature's blow met (UWMeleeContact.Kind).</summary>
	public enum UWMeleeContactKind
	{
		/// <summary>Nothing in reach.</summary>
		Nothing,

		Player,

		Creature,

		/// <summary>A door, a chest, a barrel - a thing with hit points that is no creature.</summary>
		Object,

		/// <summary>A wall, a floor step, an object without hit points.</summary>
		World
	}

	/// <summary>
	/// The defender a creature's blow met (cleanup step 4.11, 2026-10-18): what the actor needs to
	/// resolve the blow, and the world's own note of where (Place, for its pictures only).
	/// </summary>
	public readonly struct UWMeleeContact
	{
		public readonly UWMeleeContactKind Kind;

		/// <summary>Kind Creature: the defender's actor (null for a creature without one).</summary>
		public readonly UWCreatureActor Creature;

		/// <summary>Kind Object: the thing as the target of the blow.</summary>
		public readonly UWDamageTarget Target;

		/// <summary>Kind Object: a blow lands on it (it has hit points, unbroken or a broken door).</summary>
		public readonly bool TakesBlows;

		/// <summary>Kind Object: its name for the trace (null without a body); Kind World: where.</summary>
		public readonly string Name;

		/// <summary>The world's note of where the blow met it; only the world reads it.</summary>
		public readonly object Place;

		public UWMeleeContact(UWMeleeContactKind peKind, UWCreatureActor pOCreature, UWDamageTarget pOTarget, bool pbTakesBlows,
			string psName, object pOPlace)
		{
			Kind = peKind;
			Creature = pOCreature;
			Target = pOTarget;
			TakesBlows = pbTakesBlows;
			Name = psName;
			Place = pOPlace;
		}

		public static readonly UWMeleeContact Nothing = new UWMeleeContact(UWMeleeContactKind.Nothing, null, default, false,
			null, null);

		public static UWMeleeContact Player()
		{
			return new UWMeleeContact(UWMeleeContactKind.Player, null, default, false, null, null);
		}

		public static UWMeleeContact OfCreature(UWCreatureActor pODefender, object pOPlace = null)
		{
			return new UWMeleeContact(UWMeleeContactKind.Creature, pODefender, default, false, null, pOPlace);
		}

		public static UWMeleeContact OfObject(UWDamageTarget pOTarget, bool pbTakesBlows, string psName, object pOPlace)
		{
			return new UWMeleeContact(UWMeleeContactKind.Object, null, pOTarget, pbTakesBlows, psName, pOPlace);
		}

		/// <summary>The world in the way; psName says where, for the trace.</summary>
		public static UWMeleeContact OfWorld(string psName, object pOPlace)
		{
			return new UWMeleeContact(UWMeleeContactKind.World, null, default, false, psName, pOPlace);
		}
	}
}
