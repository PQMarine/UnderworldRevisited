using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// What the spell rules (UWSpellCasting, UWMiscSpellRules, UWAreaSpellRules,
	/// UWSummonSpellRules) need from the game: the caster's numbers, where he stands and
	/// looks, messages and sounds, the pending projectile and target cursors, the world, and
	/// the creatures in an area as numbered targets. The Unity side is UWSpellHost, built by
	/// UWGameUI per cast.
	///
	/// The target spells that go onto a CLICKED creature or item (UWTargetSpell, the target
	/// half of UWMiscSpell) stay on the Unity side: they are nothing but component calls.
	/// </summary>
	public interface IUWSpellHost
	{
		DataImport Data { get; }

		UWLevel CurrentLevel { get; }

		/// <summary>The void, or a tile with the no-magic bit under the caster.</summary>
		bool IsMagicBlocked { get; }

		// ------------------------------------------------- The caster

		bool HasPlayer { get; }

		int PlayerLevel { get; }

		/// <summary>Mana in whole points.</summary>
		int PlayerMana { get; }

		int PlayerCastingSkill { get; }

		UWTilePos PlayerTile { get; }

		/// <summary>The view direction, flat and normalised, east and north.</summary>
		void GetViewDirection(out float pfX, out float pfZ);

		/// <summary>The view yaw in degrees, 0 north, clockwise.</summary>
		float ViewYawDegrees { get; }

		/// <summary>Where the eye is, east and north in world units.</summary>
		void GetEyePosition(out float pfX, out float pfZ);

		void SpendMana(int piAmount);

		/// <summary>A backfired spell on the player: piDice d8 off the hit points, never below
		/// three (none at all at three or fewer), the view flashing (backfire, SPELLS.C).</summary>
		void Backfire(int piDice);

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

		// ------------------------------------------------- Messages and sounds

		void AddMessage(string psMessage);

		/// <summary>A line of string block 1, our numbering.</summary>
		void AddGeneralMessage(int piIndex);

		/// <summary>A line of string block 1 as text, for messages assembled from pieces.</summary>
		string GetGeneralMessage(int piIndex);

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

		// ------------------------------------------------- The world

		void TravelTo(int piLevelIndex, int piTileX, int piTileY);

		bool SpawnObjectById(int piObjectId, int piTileX, int piTileY, int piQuantity, int piQuality);

		/// <summary>Puts an object down on a world spot, on the floor of its tile.</summary>
		bool SpawnObjectAt(UWObject pOObject, float pfWorldX, float pfWorldZ);

		/// <summary>Whether a creature of this kind fits on the spot: nothing's body - a creature,
		/// the player, a solid object - overlaps its footprint within its height
		/// (the original's fit test, see UWSummonSpellRules).</summary>
		bool CreatureFitsAt(int piObjectId, float pfWorldX, float pfWorldZ);

		bool SpawnWardRune(int piTileX, int piTileY);

		/// <summary>A boulder appears below the ceiling of the tile and falls.</summary>
		bool DropBoulder(int piObjectId, int piTileX, int piTileY);

		void ShakeScreen(int piDuration);

		/// <summary>A sound at the caster - the Tremor's rumble, see
		/// UWMiscSpellRules.TremorSound.</summary>
		void PlaySoundAtPlayer(int piEffect);

		/// <summary>Armageddon: inventory, runes, rune shelf and the whole level are cleared,
		/// the character's status refreshed.</summary>
		void Armageddon();

		/// <summary>The hallucination counter of the player to its maximum - see
		/// UWItemSpellRules.</summary>
		void StartHallucination();

		/// <summary>One action of the bullfrog puzzle, as its a_do traps fire it (mode 4 is the
		/// reset) - see UWItemSpellRules.</summary>
		void FireBullfrog(int piMode);

		/// <summary>All living creatures on the level, each with its tile (the record's, word 0x16)
		/// and its object id - for Detect Monsters, which asks the kind's stealth.</summary>
		IReadOnlyList<CreatureSighting> Creatures { get; }

		/// <summary>Fires the look trigger of an object, as Reveal does it.</summary>
		bool FireLookTrigger(UWObject pOObject, int piSearchSkill);

		/// <summary>An effect picture stands in a tile for a moment, at a fine spot (eighths, 0 to
		/// 7) and a height in zpos steps - for pfSeconds when given (else the host's usual time),
		/// its animation starting on piStartFrame.</summary>
		void SpawnEffectInTile(int piTileX, int piTileY, int piFineX, int piFineY, int piZPos, int piEffectObjectId,
			float pfSeconds = -1f, int piStartFrame = 0);

		// ------------------------------------------------- Targets in an area

		/// <summary>Collects what can be hit in and around an area and returns how many.
		/// The targets are then addressed by index until the next collection.</summary>
		int CollectAreaTargets(UWTilePos pOCentre, int piRadiusTiles);

		UWTilePos AreaTargetTile(int piIndex);

		bool AreaTargetIsCreature(int piIndex);

		/// <summary>Every thing on the tile takes a roll of its own - creatures, the player when he
		/// stands there, doors and chests, the loose objects (see UWObjectDamageRules) - charged
		/// to the caster, with the hit report for the player's own spell.</summary>
		void DamageObjectsInTile(int piTileX, int piTileY, int piDiceCount, int piDiceRange, int piDamageType);

		/// <summary>Confusion on that creature: Missed, Spent when resisted, Shown when it took.</summary>
		UWTargetSpellRules.Result ConfuseAreaTarget(int piIndex);

		/// <summary>A class 7 spell on that creature (UWTargetSpell).</summary>
		UWTargetSpellRules.Result CastTargetSpellOnAreaTarget(int piIndex, int piMinorClass);

		void SpawnEffectAtAreaTarget(int piIndex, int piEffectObjectId);
	}

	/// <summary>A creature as Detect Monsters sees it: where it stands and what kind it is.</summary>
	public struct CreatureSighting
	{
		public UWTilePos Tile;

		public int ObjectId;

		public CreatureSighting(UWTilePos pOTile, int piObjectId)
		{
			Tile = pOTile;
			ObjectId = piObjectId;
		}
	}
}
