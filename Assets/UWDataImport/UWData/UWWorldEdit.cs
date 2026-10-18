using System;
using System.Collections.Generic;

namespace UWDataImport.UWData
{
	/// <summary>
	/// THE ONE PLACE WHERE THE OBJECT LISTS OF A LEVEL CHANGE DURING PLAY (cleanup step 2.12,
	/// 2026-10-12): an object put into a tile (Place), taken out (Remove), dropped from the data
	/// while its picture lives on (Forget), moved into another tile's list (Move), the whole
	/// level emptied (WipeAll). Until then the tile lists were changed in ten places - six in
	/// UWLevelLoader, three in UWTileQueries, one in UWInventory, whose pickup left the entity
	/// registry stale (cleanup plan section 4.4).
	///
	/// Each change raises an event, and the Unity side (UWWorldView) builds or destroys the
	/// picture: ObjectPlaced after an object joined a tile list, ObjectRemoved after it left
	/// the world, ObjectMoved after it changed tiles, LevelCleared after WipeAll. Forget raises
	/// nothing - a dying creature keeps its picture for the death animation.
	///
	/// THE SLOTS: the original's Obj_Alloc (OBJECTS.C) pops a record from the level's free list
	/// and garbage collects first when the list is empty (Obj_GarbageCollect, range 3, five
	/// mobile or ten static objects); Obj_Free pushes it back. Ours does not hand out static
	/// numbers while playing - the slots are counted (UWObjectLimitRules), and UWLevelWriter
	/// numbers new objects on saving. A creature takes its mobile slot at once
	/// (UWLevel.TryPlaceInFreeMobileSlot) and gives it back when it leaves the world for good
	/// (UWLevel.ReleaseMobileSlot), as the whoami lists need its number. An effect holds a
	/// static slot while it lives and never culls (TryTakeEffectSlot, decision 8); WipeAll
	/// empties the whole store as Map_ObjFix does.
	///
	/// One instance per level; UWLevelLoader builds it with the level and hands in the two
	/// things the rules cannot know: how many objects the player carries (they hold static
	/// slots of this level, see UWObjectLimitRules.MakeRoomForInventory) and the player's tile
	/// (the culling's centre).
	/// </summary>
	public sealed class UWWorldEdit
	{
		/// <summary>What Place did with an object.</summary>
		public enum PlaceResult
		{
			/// <summary>Not placed: no tile, a solid tile, or no slot even after culling.</summary>
			Refused,

			/// <summary>In the tile's list.</summary>
			Placed,

			/// <summary>Placed and at once taken by water or lava (UWTileQueries.TileSwallows).</summary>
			Swallowed
		}

		public UWWorldEdit(UWTileQueries pOQueries)
		{
			Queries = pOQueries;
		}

		public UWTileQueries Queries { get; }

		public UWLevel Level => Queries != null ? Queries.Level : null;

		/// <summary>How many objects the player carries, with the contents of containers - they
		/// count against the level's static slots. Null counts none.</summary>
		public Func<int> CarriedObjectCount { get; set; }

		/// <summary>The player's tile, the centre the culling keeps its distance from. Null
		/// stands far off the map, so every tile qualifies.</summary>
		public Func<UWTilePos> PlayerTile { get; set; }

		/// <summary>Where the notes on a full level go (the loader's log); null drops them.</summary>
		public Action<string> Log { get; set; }

		/// <summary>An object joined the list of tile (x, y) - in the list already, its record
		/// placed.</summary>
		public event Action<UWObject, int, int> ObjectPlaced;

		/// <summary>An object left the world for good (or was asked to - the view may hold a
		/// picture of something the lists no longer had).</summary>
		public event Action<UWObject> ObjectRemoved;

		/// <summary>An object moved from the list of tile (x, y) to that of tile (x, y).</summary>
		public event Action<UWObject, int, int, int, int> ObjectMoved;

		/// <summary>WipeAll emptied every tile list.</summary>
		public event Action LevelCleared;

		/// <summary>Where the culling centres when nobody tells (UWLevelLoader's fallback before
		/// 2026-10-12): far off the map.</summary>
		private static readonly UWTilePos NoPlayerTile = new UWTilePos(-100, -100);

		// ------------------------------------------------- Slots

		/// <summary>
		/// GetFreeObject for an object about to join the level (UWObjectLimitRules): an object
		/// already in a tile list keeps its slot; otherwise, if its kind has no free slot, far
		/// objects are culled - range 3, five at most for a creature, ten for anything else - and
		/// the object is refused when that freed nothing. Culling frees the slots of whatever it
		/// takes, so three creatures culled for a static slot leave the static list empty and the
		/// object is refused all the same - as in the original (Obj_Alloc, OBJECTS.C).
		/// </summary>
		public bool ReserveSlot(UWObject pOObject)
		{
			if (pOObject == null || Level == null || Queries.FindTileHolding(pOObject) != null)
				return true;

			bool lbMobile = UWObjectLimitRules.NeedsMobileSlot(pOObject);

			if (UWObjectLimitRules.HasFreeSlot(Level, lbMobile, fHeldStaticSlots()))
				return true;

			UWTilePos lOPlayer = fPlayerTile();

			int liCulled = UWObjectLimitRules.Cull(Level, lOPlayer.X, lOPlayer.Y, UWObjectLimitRules.CullRange,
				lbMobile ? UWObjectLimitRules.MobileCullLimit : UWObjectLimitRules.StaticCullLimit,
				fCullsForSpace, fRemoveCulled);

			bool lbFree = UWObjectLimitRules.HasFreeSlot(Level, lbMobile, fHeldStaticSlots());

			Log?.Invoke(string.Format("Level {0} full ({1} slots): culled {2}, {3}", Level.LevelNumber,
				lbMobile ? "mobile" : "static", liCulled, lbFree ? "slot free" : "object " + pOObject.ID + " refused"));

			return lbFree;
		}

		/// <summary>
		/// The inventory arrives in the level (UWObjectLimitRules.MakeRoomForInventory): if level
		/// and inventory together exceed the static slots, far objects are culled in steps of ten
		/// until they fit. Returns how many objects went.
		/// </summary>
		public int MakeRoomForInventory(int piCarriedObjects, int piPlayerTileX, int piPlayerTileY)
		{
			if (Level == null || piCarriedObjects <= 0)
				return 0;

			return UWObjectLimitRules.MakeRoomForInventory(Level, piPlayerTileX, piPlayerTileY, piCarriedObjects,
				fCullsForSpace, fRemoveCulled);
		}

		/// <summary>The static slots held outside the tile lists: the carried objects and the
		/// effects running.</summary>
		private int fHeldStaticSlots()
		{
			return (CarriedObjectCount != null ? CarriedObjectCount() : 0) + mOEffectSlots.Count;
		}

		// ------------------------------------------------- Effects

		/// <summary>The slots the running effects hold, in the order they were taken: the token the
		/// picture keeps, and the static slot popped for it (0 when the in-memory free list had
		/// none to give - the count above is what decides).</summary>
		private readonly List<KeyValuePair<int, int>> mOEffectSlots = new List<KeyValuePair<int, int>>();

		private int miLastEffectToken;

		/// <summary>How many effects hold a slot now.</summary>
		public int EffectSlotsHeld => mOEffectSlots.Count;

		/// <summary>
		/// AN EFFECT TAKES A STATIC SLOT WHILE IT LIVES - decision 8 of Docs/CLEANUP-PLAN.md
		/// (2026-10-12): the original's put_effect (EFFECT.C) makes the blood, dust, sparks and
		/// spell pictures with CreateObj(cls + FIRST_ANIMOBJ, 0), a static record from the free
		/// list (MAPADDR.C), and makes no effect when it gets none; toast_animobj frees it when
		/// its frames run out. Ours takes the top of the level's static free list for it and
		/// counts it against the static slots (fHeldStaticSlots), and gives both back in
		/// ReleaseEffectSlot. Per the decision an effect never culls (Obj_Alloc would garbage
		/// collect first; ours shows nothing instead) and is never a candidate for the culling:
		/// it lies in no tile list, the animation list does not hold it, and so no save ever
		/// reaches it. False, and no token, when the level has no static slot free.
		///
		/// Until 2026-10-12 an effect went the way of a dropped item: the slot reservation with
		/// its culling (a blood splat in a full level could cull far objects), the tile list for
		/// the spawn, and the liquid's roll - over water or lava the splat was swallowed at once.
		/// put_effect has no such test; it appends the effect to the tile at the given height.
		/// </summary>
		public bool TryTakeEffectSlot(out int piToken)
		{
			piToken = 0;

			if (Level == null || !UWObjectLimitRules.HasFreeSlot(Level, false, fHeldStaticSlots()))
				return false;

			piToken = ++miLastEffectToken;
			mOEffectSlots.Add(new KeyValuePair<int, int>(piToken, Level.TakeStaticSlot()));

			return true;
		}

		/// <summary>The effect ended (its picture is gone): its slot goes back onto the top of the
		/// static free list, as Obj_Free does. A token given back already is ignored.</summary>
		public void ReleaseEffectSlot(int piToken)
		{
			int liAt = mOEffectSlots.FindIndex(pOEntry => pOEntry.Key == piToken);

			if (liAt < 0)
				return;

			int liSlot = mOEffectSlots[liAt].Value;

			mOEffectSlots.RemoveAt(liAt);

			if (liSlot > 0 && Level != null)
				Level.ReturnStaticSlot(liSlot);
		}

		/// <summary>Every running effect's slot back, the last taken first, so the free list is
		/// what it was before them - when the level is left (UWLevelChangeRules) or torn down.</summary>
		public void ReleaseAllEffectSlots()
		{
			for (int liAt = mOEffectSlots.Count - 1; liAt >= 0; liAt--)
				ReleaseEffectSlot(mOEffectSlots[liAt].Key);
		}

		private UWTilePos fPlayerTile()
		{
			return PlayerTile != null ? PlayerTile() : NoPlayerTile;
		}

		/// <summary>
		/// Obj_GarbageCollect (UnderworldExhumed OBJECTS.C) with its own range and count: tiles
		/// more than 10 - range from the player (taxicab), row by row, each object through the
		/// culling test with that range, until piLimit went. The night calls it with range 1 and
		/// 20 (player_sleep, SKILLS.C; UWSleepRules), the slot reservation above with range 3.
		/// Returns how many objects went; their pictures go with them (ObjectRemoved).
		/// </summary>
		public int CollectGarbage(int piRange, int piLimit)
		{
			if (Level == null)
				return 0;

			UWTilePos lOPlayer = fPlayerTile();

			return UWObjectLimitRules.Cull(Level, lOPlayer.X, lOPlayer.Y, piRange, piLimit,
				pOCandidate => UWObjectLimitRules.Culls(pOCandidate, piRange,
					Queries.Data != null ? Queries.Data.CommonObjectProperties : null, Level.Masterlist),
				fRemoveCulled);
		}

		/// <summary>The culling test with the range GetFreeObject uses.</summary>
		private bool fCullsForSpace(UWObject pOCandidate)
		{
			return UWObjectLimitRules.Culls(pOCandidate, UWObjectLimitRules.CullRange,
				Queries.Data != null ? Queries.Data.CommonObjectProperties : null, Level.Masterlist);
		}

		private void fRemoveCulled(UWObject pOCulled, int piTileX, int piTileY)
		{
			pOCulled.TileX = piTileX;
			pOCulled.TileY = piTileY;
			Remove(pOCulled);
		}

		// ------------------------------------------------- Place, Remove, Forget, Move

		/// <summary>
		/// Puts an object into the list of tile (x, y) at the given fine position and height: a
		/// slot first (ReserveSlot, culling if the level is full), a creature's mobile slot, the
		/// record's place, the tile list (appended), the animation list for an animated object,
		/// then ObjectPlaced. The record is left untouched when the object is refused.
		///
		/// In water and lava only what actually sinks is lost - see
		/// UWCommonObjectProperties.Entry.SinksInLiquid. The roll comes before the object joins
		/// the list, the entry is still added first and removed afterwards, so the picture is
		/// built against the same tile list as usual. pbCanSink false: a flying projectile does
		/// not sink, even if it starts above lava - it does not lie there, it only flies over it
		/// (without the exception the tile swallowed the fire elemental's fireball the moment it
		/// was thrown, per user, 2026-09-10); nothing sinks that lies above the floor - on a
		/// bridge over the water it stays dry. LAVA SPARES quality class 3 and fire-resistant
		/// objects before it culls (keys, incense, strong thread - Tybal died on lava in his lair
		/// and took them with him, per user 2026-09-24); see UWLiquidCulling.SparedByLava. A
		/// THROWN object lands on the motion core instead (UWMotionHost, UWLiquidCulling) -
		/// the talisman in the volcano's lava and the splash of a landing are decided there.
		///
		/// NO SPLASH HERE: the splash belongs to what FALLS into the water, not to what is put
		/// down there (user's measurement of 2026-09-20: a drowning gave exactly ONE splash, the
		/// creature's own, although what it left behind went into the water as well).
		/// </summary>
		public PlaceResult Place(UWObject pOObject, int piTileX, int piTileY, int piXPos, int piYPos, int piZPos,
			bool pbCanSink)
		{
			UWTile lOTile = Level != null ? Level.GetTile(piTileX, piTileY) : null;

			if (pOObject == null || lOTile == null || lOTile.TileType == UWTile.TileTypeEnum.solid)
				return PlaceResult.Refused;

			// A SLOT FOR IT, as GetFreeObject hands one out - culling far objects when the level is
			// full, and refusing the object when even that frees nothing (UWObjectLimitRules).
			if (!ReserveSlot(pOObject))
				return PlaceResult.Refused;

			// A creature made at runtime (a trap's copy, a summoning) takes its index now, as the
			// original's GetFreeObject hands it out - see UWLevel.TryPlaceInFreeMobileSlot.
			if (pOObject is UWNpc && Level.Masterlist != null && Level.Masterlist.IndexOf(pOObject) < 0)
				Level.TryPlaceInFreeMobileSlot(pOObject, out int _);

			pOObject.TileX = piTileX;
			pOObject.TileY = piTileY;
			pOObject.XPos = (ushort)piXPos;
			pOObject.YPos = (ushort)piYPos;
			pOObject.ZPos = piZPos;

			// A CREATURE NEVER SINKS ON PLACEMENT: drop_around_place (COLLIDE.C) settles only a static
			// object through obj_deal, where water and lava take it; a mobile one just gets its home
			// tile, and creat_spell adds a summoned one directly. A land creature in the water is the
			// motion's business (drowning). Until 2026-10-12 ours rolled the liquid culling for a
			// creature too - a trap's copy over water could vanish and kept its mobile slot.
			bool lbSinks = pbCanSink && !(pOObject is UWNpc) && Queries.TileSwallows(lOTile, pOObject);

			lOTile.ObjectsInTile.Add(pOObject);

			// An animated object placed in a tile goes onto the level's animation list, as
			// add_animobj does in the original (UWAnimationOverlay).
			if (UWObjectIds.IsAnimated(pOObject.ID))
				Level.Animations.Add(pOObject, UWAnimationOverlay.Forever, piTileX, piTileY);

			ObjectPlaced?.Invoke(pOObject, piTileX, piTileY);

			if (!lbSinks)
				return PlaceResult.Placed;

			// Out of the lists again; its picture goes with ObjectRemoved (never a creature, see
			// above).
			lOTile.ObjectsInTile.Remove(pOObject);
			Level.Animations.Remove(pOObject);
			ObjectRemoved?.Invoke(pOObject);

			return PlaceResult.Swallowed;
		}

		/// <summary>
		/// OBJ_ALLOC(0) WITH ITS NUMBER, for a static record another record must name at once: the
		/// trap a spell-laid trigger links to (cast_trap_spell, TRIGGER.C, 2026-10-12). The slot is
		/// reserved as for any placed object (ReserveSlot, culling a full level); the number is the
		/// top of the level's static free list, and the object takes that place in the Masterlist,
		/// where UWLevelWriter keeps it and the trap chain finds it (UWObjectMechanics.
		/// GetLinkedObject). A number whose Masterlist record still lies in a tile list was stale on
		/// the free list and is passed over. 0 when no number was free.
		/// </summary>
		public int TakeStaticNumber(UWObject pOObject)
		{
			if (pOObject == null || Level == null || Level.Masterlist == null || !ReserveSlot(pOObject))
				return 0;

			List<UWObject> lOMaster = Level.Masterlist;

			for (int liIndex = Level.TakeStaticSlot(); liIndex > 0; liIndex = Level.TakeStaticSlot())
			{
				if (liIndex < lOMaster.Count && lOMaster[liIndex] != null && Queries.FindTileHolding(lOMaster[liIndex]) != null)
					continue;

				while (lOMaster.Count <= liIndex)
					lOMaster.Add(null);

				lOMaster[liIndex] = pOObject;

				return liIndex;
			}

			return 0;
		}

		/// <summary>Obj_Free for a number from TakeStaticNumber whose object never joined a list.</summary>
		public void ReturnStaticNumber(int piIndex)
		{
			if (Level != null && piIndex > 0)
				Level.ReturnStaticSlot(piIndex);
		}

		/// <summary>
		/// OBJ_ADD: the object at the HEAD of the tile's list (Obj_AddEnd is the tail, which Place
		/// does), its record's place left as it is - for a trap or a trigger a spell lays
		/// (cast_trap_spell). Any tile, a solid one too: the original makes no tile test there. No
		/// slot test (TakeStaticNumber did it), no sinking, no animation list. Raises ObjectPlaced.
		/// </summary>
		public bool AddAtHead(UWObject pOObject, int piTileX, int piTileY)
		{
			UWTile lOTile = Level != null ? Level.GetTile(piTileX, piTileY) : null;

			if (pOObject == null || lOTile == null || lOTile.ObjectsInTile == null)
				return false;

			pOObject.TileX = piTileX;
			pOObject.TileY = piTileY;

			lOTile.ObjectsInTile.Insert(0, pOObject);

			ObjectPlaced?.Invoke(pOObject, piTileX, piTileY);

			return true;
		}

		/// <summary>
		/// Removes an object from the world for good - from the list of the tile its record
		/// names, from the animation list, and (ObjectRemoved) its picture: a pickup, the
		/// a_delete object trap, the culling. A creature gone for good gives its slot back
		/// (UWLevel.ReleaseMobileSlot). Only creatures: an item that leaves the world here may
		/// live on elsewhere - in the inventory, in an NPC's after a conversation - and the
		/// original counts carried things against the level's slots anyway.
		///
		/// pbHeldElsewhere: the picture still held the object although its tile list did not
		/// (the caller asks the view) - it counts as removed then, as it did when the loader
		/// did this itself. False for an object off the map.
		/// </summary>
		public bool Remove(UWObject pOObject, bool pbHeldElsewhere = false)
		{
			if (pOObject == null || Level == null || Level.TileData == null
				|| !new UWTilePos(pOObject.TileX, pOObject.TileY).IsOnMap)
				return false;

			UWTile lOTile = Level.GetTile(pOObject.TileX, pOObject.TileY);

			bool lbRemoved = lOTile != null && lOTile.ObjectsInTile.Remove(pOObject);

			Level.Animations.Remove(pOObject);

			ObjectRemoved?.Invoke(pOObject);

			lbRemoved |= pbHeldElsewhere;

			if (lbRemoved && pOObject is UWNpc)
				Level.ReleaseMobileSlot(pOObject);

			return lbRemoved;
		}

		/// <summary>
		/// Removes an object from its tile's list WITHOUT touching the picture - for a creature
		/// that dies: its death animation is still running, but in the data it is already gone.
		/// Otherwise it would be back on re-entering the level and in the save game. A dead
		/// creature gives its slot back (UWLevel.ReleaseMobileSlot, AddToFreeObjectsList).
		/// </summary>
		public bool Forget(UWObject pOObject)
		{
			bool lbForgotten = Unlink(Queries, pOObject);

			if (lbForgotten && pOObject is UWNpc && Level != null)
				Level.ReleaseMobileSlot(pOObject);

			return lbForgotten;
		}

		/// <summary>
		/// Moves an object into the list of another tile - a creature that walked on. If it
		/// stays on its tile, the order stays as well. Whatever is in no list is not
		/// re-inserted. The picture moves by itself (the creature's own motion); ObjectMoved
		/// tells who wants to know.
		/// </summary>
		public void Move(UWObject pOObject, int piTileX, int piTileY)
		{
			UWTile lOFrom;

			if (fRelink(Queries, pOObject, piTileX, piTileY, out lOFrom) && (lOFrom.X != piTileX || lOFrom.Z != piTileY))
				ObjectMoved?.Invoke(pOObject, lOFrom.X, lOFrom.Z, piTileX, piTileY);
		}

		/// <summary>The data half of Forget, for the callers that work on UWTileQueries alone
		/// (UWLevelExit, UWWorldCapture): out of the list of whichever tile holds it.</summary>
		public static bool Unlink(UWTileQueries pOQueries, UWObject pOObject)
		{
			UWTile lOTile = pOQueries != null ? pOQueries.FindTileHolding(pOObject) : null;

			return lOTile != null && lOTile.ObjectsInTile.Remove(pOObject);
		}

		/// <summary>The data half of Move, for the callers that work on UWTileQueries alone: false
		/// when the object is in no list or the target is no tile.</summary>
		public static bool Relink(UWTileQueries pOQueries, UWObject pOObject, int piTileX, int piTileY)
		{
			return fRelink(pOQueries, pOObject, piTileX, piTileY, out UWTile _);
		}

		private static bool fRelink(UWTileQueries pOQueries, UWObject pOObject, int piTileX, int piTileY, out UWTile pOFrom)
		{
			pOFrom = null;

			if (pOQueries == null || pOObject == null || pOQueries.GetTile(piTileX, piTileY) == null)
				return false;

			UWTile lOCurrent = pOQueries.FindTileHolding(pOObject);
			UWTile lOTarget = pOQueries.GetTile(piTileX, piTileY);

			if (lOCurrent == null)
				return false;

			if (lOCurrent != lOTarget)
			{
				lOCurrent.ObjectsInTile.Remove(pOObject);
				lOTarget.ObjectsInTile.Add(pOObject);
			}

			pOObject.TileX = piTileX;
			pOObject.TileY = piTileY;
			pOFrom = lOCurrent;

			return true;
		}

		// ------------------------------------------------- The whole level

		/// <summary>
		/// Armageddon: every tile list emptied - items, creatures, doors, triggers, everything -
		/// then LevelCleared for the pictures. The original's clearobj (UWEDIT.C) runs
		/// Map_ObjFix (OBJECTS.C), which empties every tile and refills the free lists with every
		/// slot (UWLevel.ResetObjectStore). Until 2026-10-12 ours emptied the tile lists only, so
		/// the slots of the wiped creatures stayed taken and in the active list (cleanup plan
		/// section 4.4: "Armageddon skips ReleaseMobileSlot").
		/// </summary>
		public void WipeAll()
		{
			for (int liAt = 0; Level != null && Level.TileData != null && liAt < Level.TileData.Length; liAt++)
			{
				UWTile lOTile = Level.TileData[liAt];

				if (lOTile != null && lOTile.ObjectsInTile != null)
					lOTile.ObjectsInTile.Clear();
			}

			if (Level != null)
				Level.ResetObjectStore();

			LevelCleared?.Invoke();
		}
	}
}
