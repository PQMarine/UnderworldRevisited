using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// The running world as the rules and their hosts see it, engine-free. PHASE A (cleanup step
	/// 3.1, 2026-10-17): the data, the level and its tiles, the travel between levels, and the way
	/// to the presentation and to the player's body. The creature search (FindCreatures) came with
	/// step 3.2, the blows (Damage) with step 3.3; the doors, the objects, a found creature's acts
	/// and the level's own time with step 3.6 (section 7.2 of Docs/CLEANUP-PLAN.md).
	///
	/// WHY: nine host interfaces repeated these members with their own bodies (section 4.5) - the
	/// level alone in four index conventions. Since step 3.6 (2026-10-17) the rules take the world
	/// themselves where a host only forwarded to it (the sleep, the shrine, the special deaths), and
	/// the hosts that remain keep only what belongs to one firing, one cast or one conversation.
	/// The one implementation is UWWorld on the level object; the self-check has FakeWorld.
	/// </summary>
	public interface IUWWorld
	{
		/// <summary>The loaded installation; null before it is loaded.</summary>
		DataImport Data { get; }

		/// <summary>The level the player is on; null without data.</summary>
		UWLevel Level { get; }

		/// <summary>The dungeon level, 0-based. For the other conventions the hosts use, see
		/// UWWorldLevels.</summary>
		int LevelIndex { get; }

		int LevelCount { get; }

		/// <summary>The tile questions of the current level (UWTileQueries, rebuilt with the
		/// level).</summary>
		UWTileQueries Tiles { get; }

		/// <summary>Messages, sounds, effects, shake, flash, cutscenes.</summary>
		IUWPresentation Presentation { get; }

		/// <summary>The player's body: where it stands and looks.</summary>
		IUWPlayerAvatar Player { get; }

		/// <summary>The plain move to a level and tile - stairs, a moongate, a spell: the arrival
		/// search (new_player_pos) decides where the player lands, and a move right after another is
		/// refused for a moment.</summary>
		void TravelTo(int piLevelIndex, int piTileX, int piTileY);

		/// <summary>A teleport trap's move (do_teleport): on the same level the target must have room
		/// by the first search before anyone moves, else nothing happens; then TravelTo. The trap
		/// host has always used this one, the spell host TravelTo - kept apart on purpose.</summary>
		void TeleportTo(int piLevelIndex, int piTileX, int piTileY);

		/// <summary>
		/// THE CREATURE SEARCH (cleanup step 3.2, 2026-10-17): clears pOInto, adds every creature of
		/// the level that passes the filter and stands in the area by the area's tile measure
		/// (UWCreatureSearch.Matches), and returns how many. The order is the level's list of active
		/// creatures, the one the creature driver walks (UWCritter.Active: the order they came into
		/// the scene); a caller that needs the original's order sorts, as the sleep does.
		///
		/// WHY: the hosts that needed creatures around a tile each searched the whole scene
		/// (FindObjectsByType) with tests of their own; the scene search promised no order and walked
		/// every object of the scene per call.
		/// </summary>
		int FindCreatures(UWTileArea pOArea, UWCreatureFilter peFilter, List<UWCreatureRef> pOInto);

		/// <summary>
		/// A blow lands on the player, a creature, an object or a thing in flight (cleanup step 3.3,
		/// 2026-10-17): every damage site of the objects and creatures, and the hosts' damage and
		/// curse on the player, end here (UWBlowRouting). The site keeps its own amount, sounds,
		/// pictures and messages around the call; the blow names the attacker a struck creature
		/// records (UWBlow.Attacker; how it learned it was UWBlowAttribution until cleanup step 4.8).
		/// </summary>
		void Damage(UWDamageTarget pOTarget, UWBlow pOBlow);

		// ------------------------------------------------- Doors (cleanup step 3.6)

		/// <summary>The doors of the level: the night closes them, the door trap and gronk_door move
		/// them.</summary>
		IUWDoors Doors { get; }

		// ------------------------------------------------- Objects (cleanup step 3.6)

		/// <summary>An object leaves the world, display included (UWWorldEdit.Remove: out of its
		/// tile list, its slot back). False when it was not in the world or there is no level.</summary>
		bool Remove(UWObject pOObject);

		/// <summary>
		/// A thing the game hands the player (UWInventoryModel.GiveToPlayer): the pointer when it is
		/// free, else the backpack, else the floor at his feet; pbPreferBags puts it away first, as
		/// the modern scheme does. False only when nothing could be given at all - no item, no
		/// inventory, or no level to lay it in.
		/// </summary>
		bool GiveToHand(UWObject pOItem, bool pbPreferBags);

		/// <summary>Puts an object into a tile of the level, display included, at the fine
		/// position and height it carries - a creation trap's copy of its template, the
		/// conversation's place_object (UWLevelLoader.SpawnObjectInTileAtTemplatePosition). False
		/// when it could not be placed.</summary>
		bool Spawn(UWObject pOObject, UWTilePos pOTile);

		/// <summary>A new object of this kind (CreateObj: quality 40) in the centre of a tile - the
		/// Vas rune of the emerald puzzle, the moongate in the volcano. piQuantity 0 gives the stack
		/// rule of its class; above 0 it is the count, or for the moongate its link field (0x2C0,
		/// its colour), with the quantity bit set. peArrival: where in the tile's list (the stone
		/// at the head, the moongate at the tail - UWWorldEdit.Arrival). False without data.</summary>
		bool SpawnNewObject(int piObjectId, UWTilePos pOTile, int piQuantity, UWWorldEdit.Arrival peArrival);

		/// <summary>place_object with a negative x: the item goes down at the player's feet
		/// (put_at at his tile, stay_centered, range 6, never culled). Nothing without a view to find
		/// the feet by.</summary>
		void DropAtPlayer(UWObject pOItem);

		/// <summary>Puts an item down around the player (PlaceObjectAtNPC_seg026_12E5 with the
		/// player object and radius 5) - what the player still has laid out when a conversation
		/// ends (end_barter_ovr095_3C2).</summary>
		void DropAroundPlayer(UWObject pOItem);

		/// <summary>Puts an item down around a creature (PlaceObjectAtNPC_seg026_12E5 with radius
		/// 5) - bought goods the player left on the partner's side when a conversation ends
		/// (end_barter_ovr095_3C2). False when the culling test took it (near_mob_put_at's
		/// failure).</summary>
		bool DropAtCreature(UWObject pOItem, UWNpc pONpc);

		/// <summary>Puts an object down at a world point (east and north in world units), on the
		/// floor of its tile, as a dropped object - the summoned food and creature.</summary>
		bool DropObjectAt(UWObject pOObject, float pfWorldX, float pfWorldZ);

		/// <summary>The Rune of Warding's objects on a tile (its trigger and trap); false when there
		/// is no room.</summary>
		bool SpawnWardRune(int piTileX, int piTileY);

		/// <summary>A boulder appears below the ceiling of the tile and falls on the motion core -
		/// a piece of Tremor's debris. False when it could not be made.</summary>
		bool DropBoulder(int piObjectId, int piTileX, int piTileY);

		/// <summary>Whether a creature of this kind fits on the spot (east and north in world
		/// units): nothing's body - a creature, the player, a solid object - overlaps its footprint
		/// within its height (the original's fit test, see UWSummonSpellRules).</summary>
		bool CreatureFitsAt(int piObjectId, float pfWorldX, float pfWorldZ);

		// ------------------------------------------------- Area spells (cleanup step 3.6)

		// Which creatures an area spell reaches is the rules' walk over the squares' lists
		// (UWAreaWalk, cleanup step 4.0, 2026-10-18); the world only acts on the creature it is
		// handed - its level object from a tile list. The physics collection (CollectAreaTargets)
		// is gone.

		/// <summary>Confusion on the creature with this level object: Missed when it has no body to
		/// take it, Spent when resisted, Shown when it took.</summary>
		UWTargetSpellRules.Result ConfuseCreature(UWObject pOCreature);

		/// <summary>A class 7 spell on the creature with this level object (UWTargetSpell).</summary>
		UWTargetSpellRules.Result CastTargetSpellOnCreature(UWObject pOCreature, int piMinorClass);

		/// <summary>An effect picture at the middle of that creature's body for pfSeconds.</summary>
		void ShowEffectAtCreature(UWObject pOCreature, int piEffectObjectId, float pfSeconds);

		/// <summary>Every thing on the tile takes a roll of its own - the player when he stands there,
		/// then the tile's list in its order: creatures, doors and chests, the loose objects
		/// (UWTileBlast over UWAreaWalk.DamageSquare, see UWObjectDamageRules) - charged to
		/// piAttacker (a creature's index or the player's).</summary>
		void StrikeTile(UWTilePos pOTile, int piDiceCount, int piDiceRange, int piDamageType, int piAttacker);

		// ------------------------------------------------- A found creature (cleanup step 3.6)

		/// <summary>The creature starts its conversation with the player (its own talk: nothing while
		/// a conversation is open). The golem and the nameless one talk instead of dying.</summary>
		void TalkTo(UWCreatureRef pOCreature);

		/// <summary>Critter table byte 0x1C, upper nibble (UWObjectClassProperties.Critter.TravelRange);
		/// 0 without a table row or a creature behind the reference.</summary>
		int CreatureTravelRange(UWCreatureRef pOCreature);

		/// <summary>The creature's flood_path with range 0 from its tile and height to pOTo at
		/// piToHeight (floor levels), both ends included - the sleep ambush's path. pOPath gets the
		/// tiles; false without a way.</summary>
		bool TryGetCreaturePath(UWCreatureRef pOCreature, UWTilePos pOTo, int piToHeight, List<UWTilePos> pOPath);

		/// <summary>Sets the creature down on this sub-tile spot (0..7 each) of the tile; false on a
		/// solid tile or the wrong ground for its kind.</summary>
		bool PlaceCreature(UWCreatureRef pOCreature, UWTilePos pOTile, int piSubX, int piSubY);

		// ------------------------------------------------- The level's own time (cleanup step 3.6)

		/// <summary>One respawn pass over the level's creation traps (UWRespawnRules.Run); with
		/// pbAnyDistance the distance test is off, as the night has it. Returns how many fired.</summary>
		int RunRespawn(bool pbAnyDistance);

		/// <summary>Time passes for the level's creatures (UWLevelExit.PassTime): healing, home
		/// posts, the race moods, temporary creatures gone - the scene follows at once.</summary>
		void LetTimePassForCreatures();

		/// <summary>The tidying of far objects (Obj_GarbageCollect, UWWorldEdit.CollectGarbage): up
		/// to piLimit objects on tiles more than 10 - piRange from the player. Returns how many
		/// went; 0 without a level.</summary>
		int CollectGarbage(int piRange, int piLimit);
	}


	/// <summary>
	/// A WORLD WITH A PRESENTATION OF ITS OWN (cleanup step 3.6, 2026-10-17): everything goes to the
	/// world inside, only the presentation is the one handed in - the night's lines to the
	/// Interaction the sleep was given, a trap firing's or a cast's lines to its own channel or the
	/// silent scope. This is how the call state of one firing, one cast or one night travels with
	/// the world without the long-lived world keeping it (section 7.2 of Docs/CLEANUP-PLAN.md).
	/// </summary>
	public sealed class UWScopedWorld : IUWWorld
	{
		private readonly IUWWorld mOInner;

		private readonly IUWPresentation mOPresentation;

		/// <summary>pOInner with pOPresentation; pOInner itself when the presentation is its
		/// own.</summary>
		public static IUWWorld With(IUWWorld pOInner, IUWPresentation pOPresentation)
		{
			if (pOInner == null || ReferenceEquals(pOInner.Presentation, pOPresentation))
				return pOInner;

			return new UWScopedWorld(pOInner, pOPresentation);
		}

		private UWScopedWorld(IUWWorld pOInner, IUWPresentation pOPresentation)
		{
			mOInner = pOInner is UWScopedWorld lOScoped ? lOScoped.mOInner : pOInner;
			mOPresentation = pOPresentation;
		}

		public DataImport Data => mOInner.Data;

		public UWLevel Level => mOInner.Level;

		public int LevelIndex => mOInner.LevelIndex;

		public int LevelCount => mOInner.LevelCount;

		public UWTileQueries Tiles => mOInner.Tiles;

		public IUWPresentation Presentation => mOPresentation;

		public IUWPlayerAvatar Player => mOInner.Player;

		public IUWDoors Doors => mOInner.Doors;

		public void TravelTo(int piLevelIndex, int piTileX, int piTileY)
		{
			mOInner.TravelTo(piLevelIndex, piTileX, piTileY);
		}

		public void TeleportTo(int piLevelIndex, int piTileX, int piTileY)
		{
			mOInner.TeleportTo(piLevelIndex, piTileX, piTileY);
		}

		public int FindCreatures(UWTileArea pOArea, UWCreatureFilter peFilter, List<UWCreatureRef> pOInto)
		{
			return mOInner.FindCreatures(pOArea, peFilter, pOInto);
		}

		public void Damage(UWDamageTarget pOTarget, UWBlow pOBlow)
		{
			mOInner.Damage(pOTarget, pOBlow);
		}

		public bool Remove(UWObject pOObject)
		{
			return mOInner.Remove(pOObject);
		}

		public bool GiveToHand(UWObject pOItem, bool pbPreferBags)
		{
			return mOInner.GiveToHand(pOItem, pbPreferBags);
		}

		public void TalkTo(UWCreatureRef pOCreature)
		{
			mOInner.TalkTo(pOCreature);
		}

		public bool Spawn(UWObject pOObject, UWTilePos pOTile)
		{
			return mOInner.Spawn(pOObject, pOTile);
		}

		public bool SpawnNewObject(int piObjectId, UWTilePos pOTile, int piQuantity, UWWorldEdit.Arrival peArrival)
		{
			return mOInner.SpawnNewObject(piObjectId, pOTile, piQuantity, peArrival);
		}

		public void DropAtPlayer(UWObject pOItem)
		{
			mOInner.DropAtPlayer(pOItem);
		}

		public void DropAroundPlayer(UWObject pOItem)
		{
			mOInner.DropAroundPlayer(pOItem);
		}

		public bool DropAtCreature(UWObject pOItem, UWNpc pONpc)
		{
			return mOInner.DropAtCreature(pOItem, pONpc);
		}

		public bool DropObjectAt(UWObject pOObject, float pfWorldX, float pfWorldZ)
		{
			return mOInner.DropObjectAt(pOObject, pfWorldX, pfWorldZ);
		}

		public bool SpawnWardRune(int piTileX, int piTileY)
		{
			return mOInner.SpawnWardRune(piTileX, piTileY);
		}

		public bool DropBoulder(int piObjectId, int piTileX, int piTileY)
		{
			return mOInner.DropBoulder(piObjectId, piTileX, piTileY);
		}

		public bool CreatureFitsAt(int piObjectId, float pfWorldX, float pfWorldZ)
		{
			return mOInner.CreatureFitsAt(piObjectId, pfWorldX, pfWorldZ);
		}

		public UWTargetSpellRules.Result ConfuseCreature(UWObject pOCreature)
		{
			return mOInner.ConfuseCreature(pOCreature);
		}

		public UWTargetSpellRules.Result CastTargetSpellOnCreature(UWObject pOCreature, int piMinorClass)
		{
			return mOInner.CastTargetSpellOnCreature(pOCreature, piMinorClass);
		}

		public void ShowEffectAtCreature(UWObject pOCreature, int piEffectObjectId, float pfSeconds)
		{
			mOInner.ShowEffectAtCreature(pOCreature, piEffectObjectId, pfSeconds);
		}

		public void StrikeTile(UWTilePos pOTile, int piDiceCount, int piDiceRange, int piDamageType, int piAttacker)
		{
			mOInner.StrikeTile(pOTile, piDiceCount, piDiceRange, piDamageType, piAttacker);
		}

		public int CreatureTravelRange(UWCreatureRef pOCreature)
		{
			return mOInner.CreatureTravelRange(pOCreature);
		}

		public bool TryGetCreaturePath(UWCreatureRef pOCreature, UWTilePos pOTo, int piToHeight, List<UWTilePos> pOPath)
		{
			return mOInner.TryGetCreaturePath(pOCreature, pOTo, piToHeight, pOPath);
		}

		public bool PlaceCreature(UWCreatureRef pOCreature, UWTilePos pOTile, int piSubX, int piSubY)
		{
			return mOInner.PlaceCreature(pOCreature, pOTile, piSubX, piSubY);
		}

		public int RunRespawn(bool pbAnyDistance)
		{
			return mOInner.RunRespawn(pbAnyDistance);
		}

		public void LetTimePassForCreatures()
		{
			mOInner.LetTimePassForCreatures();
		}

		public int CollectGarbage(int piRange, int piLimit)
		{
			return mOInner.CollectGarbage(piRange, piLimit);
		}
	}

	/// <summary>
	/// THE FOUR LEVEL CONVENTIONS of the hosts in one place (cleanup step 3.1, 2026-10-17). Each host
	/// answered the dungeon level its own way, and the rules behind it rely on that answer, so they
	/// stay as they were - only their arithmetic lives here now:
	///
	///   - the trap rules (UWTrapRules.LevelIndexOf; IUWTrapHost.CurrentLevelIndex until cleanup
	///     step 3.6): 0-based, 0 without a world (IndexOr 0);
	///   - IUWVitalsHost.DungeonLevelIndex: 0-based, -1 without a world (IndexOr -1);
	///   - IUWConversationHost.CurrentLevelNumber: 1-based, 1 without a world (NumberOr 1) - the
	///     conversations' dungeon_level;
	///   - the shrine's quest mantras (UWShrineRules; UWShrineRules.IHost.DungeonLevel until cleanup
	///     step 3.6): 1-based, 0 without a world (NumberOr 0) - as PLAYER.DAT 0x5C keeps it.
	///
	/// "Without a world" means no level object in the scene (a test, the main menu before a game).
	/// </summary>
	public static class UWWorldLevels
	{
		/// <summary>The 0-based level index, or piWithoutWorld.</summary>
		public static int IndexOr(IUWWorld pOWorld, int piWithoutWorld)
		{
			return pOWorld != null ? pOWorld.LevelIndex : piWithoutWorld;
		}

		/// <summary>The 1-based level number, or piWithoutWorld.</summary>
		public static int NumberOr(IUWWorld pOWorld, int piWithoutWorld)
		{
			return pOWorld != null ? pOWorld.LevelIndex + 1 : piWithoutWorld;
		}
	}
}
