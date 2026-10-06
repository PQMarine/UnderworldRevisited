namespace UWDataImport.UWData
{
	/// <summary>
	/// What the trap rules (UWTrapRules) need from the game around them. One host per firing:
	/// it carries the level, the message channel of that firing (which may be silent) and,
	/// for the rune of warding, the creature that stepped on the trigger. The Unity side is
	/// UWTrapHost inside UWTriggerSystem.
	///
	/// The traps that are nothing but engine work - the arrow's flight, a spell, the remote
	/// camera, the trespass search over the creature bodies - stay whole on the host; the
	/// rules only decide when they fire and record them.
	/// </summary>
	public interface IUWTrapHost
	{
		DataImport Data { get; }

		UWLevel CurrentLevel { get; }

		/// <summary>The dungeon level, 0-based.</summary>
		int CurrentLevelIndex { get; }

		int LevelCount { get; }

		/// <summary>Test switch: a create-object trap always fires, whatever its chance.</summary>
		bool AlwaysFireCreateObjectTraps { get; }

		/// <summary>Is a creature that was created during play (UWObject.WasSpawned) standing
		/// within piTilesBack below and piTilesForward above this tile on both axes? The
		/// crowding check of a creation trap - see UWTrapRules.fFireCreateObjectTrap.</summary>
		bool IsSpawnedCreatureNear(int piTileX, int piTileY, int piTilesBack, int piTilesForward);

		// ------------------------------------------------- Switches and levers

		/// <summary>Advances an eight-position lever by one, if the used object is one.</summary>
		void AdvanceLever(UWObject pOSwitchObject);

		/// <summary>Flips the pressed image of a switch.</summary>
		void ToggleSwitchVisual(UWObject pOSwitchObject);

		/// <summary>The tile height an eight-position lever stands for, if the used object is
		/// one (a_do trap "raise tile").</summary>
		bool TryGetLeverTileHeight(UWObject pOSwitchObject, int piStepSize, out int piHeight);

		// ------------------------------------------------- The world

		void RebuildGeometry();

		/// <summary>Objects inside a tile that was solid until now are drawn late.</summary>
		void SpawnPendingObjectsInTile(int piTileX, int piTileY);

		bool SpawnObjectInTileAtTemplatePosition(UWObject pOCopy, int piTileX, int piTileY);

		bool SpawnObjectById(int piObjectId, int piTileX, int piTileY);

		bool RemoveObjectFromWorld(UWObject pOObject);

		void TravelTo(int piLevelIndex, int piTileX, int piTileY);

		/// <summary>The door side of an a_door trap: the lock is removed if there is one,
		/// otherwise made from the template; then the door acts (1 open, 2 close, anything
		/// else toggles).</summary>
		void FireDoorTrap(UWObject pODoor, int piAction, UWObject pOLockTemplate);

		// ------------------------------------------------- The player

		bool HasPlayer { get; }

		/// <summary>The player's inventory, or null without one.</summary>
		UWInventoryModel Inventory { get; }

		void DamagePlayer(int piDamage);

		void PoisonPlayer(int piStrength);

		int PlayerCastingSkill { get; }

		// ------------------------------------------------- Messages

		/// <summary>False when this firing has no message channel - the messages are then
		/// dropped, and traps that only talk do nothing.</summary>
		bool HasMessages { get; }

		void AddMessage(string psMessage);

		/// <summary>A line of string block 1, our numbering.</summary>
		void AddGeneralMessage(int piIndex);

		// ------------------------------------------------- Traps that are engine work

		bool FireArrowTrap(UWObject pOTrap, int piTileX, int piTileY);

		bool FireSpellTrap(UWObject pOTrap, int piTileX, int piTileY, int piMajorClass, int piMinorClass);

		/// <summary>The view jumps to the trap's spot on that tile until a key is pressed.</summary>
		bool FireCameraTrap(UWObject pOTrap, int piTileX, int piTileY);

		/// <summary>Trespassing: every creature near the player that minds the owner and sees
		/// him loses goodwill and says so by its new attitude (UWCritterRules.MindsTheft - the same
		/// routine as a theft in the original). Returns how many were angered.</summary>
		int AngerRaceNearPlayer(int piOwner, int piTilesBack, int piTilesForward);

		/// <summary>The creature that stepped on the trigger, if it was one (rune of warding, and
		/// since 2026-10-06 every move trigger a creature sets off).</summary>
		bool HasTriggeringCreature { get; }

		/// <summary>A thing - thrown, shot, knocked loose - set the trigger off.</summary>
		bool HasTriggeringThing { get; }

		/// <summary>A damage trap's value on the triggering thing: destroyed when the hit wears it
		/// out, else untouched (the original's motion step writes its hp back).</summary>
		void StrikeTriggeringThing(int piDamage);

		/// <summary>A damage trap's quality on the triggering creature.</summary>
		void DamageTriggeringCreature(int piDamage);

		/// <summary>Hurts the triggering creature and tells the player in which direction it
		/// happened.</summary>
		void ApplyWardDamage(int piDamage, int piWardMessage, int piFirstDirectionMessage);

		bool BeginConversation(int piSlot);

		bool TalkTo(UWObject pONpcData);

		bool PlayVictory();

		/// <summary>The class 9 curse on the player: dice plus dice eight-sided, never below
		/// three hit points (UWPlayerVitals.ApplyCurse). The exploding book uses three dice.
		/// </summary>
		void CursePlayer(int piDice);

		/// <summary>Plays a cutscene - the freeing of Arial is cutscene 3.</summary>
		void PlayCutscene(int piCutscene);

		/// <summary>Shakes the screen, the quake trap's first bit - always 0x1E on the large channel;
		/// the original ignores the intensity, the trap's
		/// owner field.</summary>
		void ShakeScreen(int piIntensity);

		/// <summary>Throws the player up, the quake trap's second bit.</summary>
		void BouncePlayer(int piIntensity);
	}
}
