namespace UWDataImport.UWData
{
	/// <summary>
	/// What the trap rules (UWTrapRules) need from ONE FIRING: the world as the firing sees it
	/// (its message channel, which may be silent), who set it off, and the engine work that
	/// belongs to a firing - the switch that was used, the terrain change, the triggering creature
	/// or thing, the traps that are nothing but engine work (the arrow's flight, a spell, the
	/// remote camera, the trespass search over the creature bodies) and the conversation it may
	/// start. The Unity side is UWTrapHost inside UWTriggerSystem, built per firing.
	///
	/// THE WORLD (cleanup step 3.6, 2026-10-17): the level and its data, the player (his tile,
	/// inventory, numbers, the damage, curse and poison that reach him), the messages, the
	/// cutscenes, the quake's shake and bounce, the objects a trap makes or removes, the creation
	/// trap's crowding check, the teleport and the door of a door trap are IUWWorld's. World is the
	/// world with this firing's presentation - the silent scope for a firing without a message
	/// channel (the respawn). Until then this interface had 37 members, most of them the world's.
	/// </summary>
	public interface IUWTrapHost
	{
		/// <summary>The world with this firing's presentation; never null while a level is loaded.</summary>
		IUWWorld World { get; }

		/// <summary>Who set the firing off.</summary>
		UWTrapFiring Firing { get; }

		/// <summary>Test switch: a create-object trap always fires, whatever its chance.</summary>
		bool AlwaysFireCreateObjectTraps { get; }

		// ------------------------------------------------- Switches and levers

		/// <summary>Advances an eight-position lever by one, if the used object is one.</summary>
		void AdvanceLever(UWObject pOSwitchObject);

		/// <summary>Flips the pressed image of a switch.</summary>
		void ToggleSwitchVisual(UWObject pOSwitchObject);

		// ------------------------------------------------- The terrain change

		void RebuildGeometry();

		/// <summary>A tile's height change moved this object's z (UWTrapRules.SetTileHeight): its
		/// picture follows, by the difference to piOldZPos.</summary>
		void OnObjectHeightChanged(UWObject pOObject, int piOldZPos);

		/// <summary>The creatures on a tile whose height changed go with the floor by
		/// change_terrain's rule (UWTrapRules.SetTileHeight): on a rising tile those below the new
		/// floor are lifted onto it, on a sinking tile those standing on the old floor go down.
		/// Ours keeps the creatures in their live records, not in the tile list.</summary>
		void MoveCreaturesWithFloor(int piTileX, int piTileY, int piOldZPos, int piNewZPos);

		/// <summary>Objects inside a tile that was solid until now are drawn late.</summary>
		void SpawnPendingObjectsInTile(int piTileX, int piTileY);

		// ------------------------------------------------- The one who set it off

		/// <summary>A damage trap's value on the triggering thing: destroyed when the hit wears it
		/// out, else untouched (the original's motion step writes its hp back).</summary>
		void StrikeTriggeringThing(int piDamage);

		/// <summary>A damage trap's quality on the triggering creature, with no attacker (whack_thing
		/// hands damage_item none: no alarm, no experience).</summary>
		void DamageTriggeringCreature(int piDamage);

		/// <summary>Hurts the triggering creature with no attacker, as a damage trap does, and
		/// tells the player in which direction it happened.</summary>
		void ApplyWardDamage(int piDamage, int piWardMessage, int piFirstDirectionMessage);

		// ------------------------------------------------- Traps that are engine work

		bool FireArrowTrap(UWObject pOTrap, int piTileX, int piTileY);

		bool FireSpellTrap(UWObject pOTrap, int piTileX, int piTileY, int piMajorClass, int piMinorClass);

		/// <summary>The view jumps to the trap's spot on that tile until a key is pressed.</summary>
		bool FireCameraTrap(UWObject pOTrap, int piTileX, int piTileY);

		/// <summary>Trespassing: every creature near the player that minds the owner and sees
		/// him loses goodwill and says so by its new attitude (UWCritterRules.MindsTheft - the same
		/// routine as a theft in the original). Returns how many were angered.</summary>
		int AngerRaceNearPlayer(int piOwner, int piTilesBack, int piTilesForward);

		// ------------------------------------------------- Through this firing's channel

		bool BeginConversation(int piSlot);

		bool TalkTo(UWObject pONpcData);

		bool PlayVictory();
	}

	/// <summary>
	/// WHO SET A FIRING OFF (cleanup step 3.6, 2026-10-17): the player, a creature (the rune of
	/// warding, and since 2026-10-06 every move trigger a creature sets off) or a thing - thrown,
	/// shot, knocked loose. A value the firing carries; until then two members of the trap host
	/// (HasTriggeringCreature, HasTriggeringThing) and the door trap's own derivation of the opener.
	/// </summary>
	public readonly struct UWTrapFiring
	{
		/// <summary>A creature stepped on the trigger.</summary>
		public readonly bool ByCreature;

		/// <summary>A thing in flight set the trigger off.</summary>
		public readonly bool ByThing;

		public UWTrapFiring(bool pbByCreature, bool pbByThing)
		{
			ByCreature = pbByCreature;
			ByThing = pbByThing;
		}

		/// <summary>The player set it off.</summary>
		public static UWTrapFiring ByPlayer => new UWTrapFiring(false, false);

		/// <summary>OpenDoor(CharacterThatTriggeredTrap, door): whoever set the chain off opens a door
		/// it moves - the creature, else the thing, else the player.</summary>
		public UWTrapRules.Setter Opener => ByCreature ? UWTrapRules.Setter.Creature
			: ByThing ? UWTrapRules.Setter.Thing
			: UWTrapRules.Setter.Player;
	}
}
