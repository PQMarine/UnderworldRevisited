using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE WORLD (cleanup step 3.1, 2026-10-17): the one Unity implementation of IUWWorld, on the level
/// object beside UWLevelLoader. Phase A answers the data, the level and its tiles, the travel, and
/// hands out the presentation (UWPresentation) and the player's body (UWPlayerAvatar). The hosts
/// that repeated these members - UWTrapHost, UWSpellHost, UWSleep's host, UWConversationHost,
/// UWShrine's host, UWItemDrag, UWCharacter and the special-death host - delegate here. Since
/// step 3.2 (2026-10-17) it also searches the creatures (FindCreatures, FindCritters) for every
/// host that used to search the scene: the theft and trespass witnesses, the creation trap's
/// crowding, the sleep, Detect Monsters, Tybal's orb, the special deaths and the debug tools. Since
/// step 3.3 (the same day) every blow lands here too (Damage, UWBlow). Since step 3.6 (the same
/// day) it answers what the sleep's, the shrine's and the special deaths' hosts answered - the
/// doors, the removal of an object and the gift to the hand, a found creature's talk, path and
/// place, the level's own time - and those hosts are gone.
///
/// LONG-LIVED: one per level object, for the whole game. What belongs to one firing, one cast or
/// one conversation (the triggering creature, the message channel of a firing, the area targets)
/// stays on the per-call hosts and never moves in here (section 11 of Docs/CLEANUP-PLAN.md).
///
/// The presentation and the avatar are plain objects owned here rather than components of their
/// own: neither keeps state of its own, both only read scene services (UWScene, Camera.main) on
/// every call, so they need no lifetime and no place in the scene, and every host reaches all
/// three through one entry point.
/// </summary>
public sealed class UWWorld : MonoBehaviour, IUWWorld
{
    private UWLevelLoader mOLoader;

    private UWPlayerAvatar mOPlayer;

    private UWPresentation mOPresentation;

    /// <summary>The world on the loader's object, made when it is missing (as
    /// UWMotionHost.Ensure does); null without a loader.</summary>
    public static UWWorld Of(UWLevelLoader pOLoader)
    {
        if (pOLoader == null)
            return null;

        UWWorld lOWorld = pOLoader.GetComponent<UWWorld>();

        if (lOWorld == null)
            lOWorld = pOLoader.gameObject.AddComponent<UWWorld>();

        return lOWorld;
    }

    /// <summary>The level loader beside this component.</summary>
    public UWLevelLoader Loader
    {
        get
        {
            if (mOLoader == null)
                mOLoader = GetComponent<UWLevelLoader>();

            return mOLoader;
        }
    }

    public DataImport Data => Loader != null ? Loader.UWDataImporter : null;

    public UWLevel Level => Loader != null ? Loader.CurrentLevel : null;

    public int LevelIndex => Loader != null ? Loader.CurrentLevelIndex : 0;

    public int LevelCount => Loader != null ? Loader.LevelCount : 0;

    public UWTileQueries Tiles => Loader != null ? Loader.TileQueries : null;

    /// <summary>The world's presentation, its lines going to the scene's Interaction. A host
    /// that carries a message channel of its own takes UWPresentation.For(world, channel).</summary>
    public IUWPresentation Presentation => mOPresentation ?? (mOPresentation = new UWPresentation(this, null, true));

    public IUWPlayerAvatar Player => Avatar;

    /// <summary>The body with its Unity-only members (the world points of the eye and the
    /// feet).</summary>
    public UWPlayerAvatar Avatar => mOPlayer ?? (mOPlayer = new UWPlayerAvatar(this));

    public void TravelTo(int piLevelIndex, int piTileX, int piTileY)
    {
        if (Loader != null)
            Loader.TravelTo(piLevelIndex, piTileX, piTileY);
    }

    public void TeleportTo(int piLevelIndex, int piTileX, int piTileY)
    {
        if (Loader != null)
            Loader.TeleportTo(piLevelIndex, piTileX, piTileY);
    }

    // ------------------------------------------------- Creatures

    public int FindCreatures(UWTileArea pOArea, UWCreatureFilter peFilter, List<UWCreatureRef> pOInto)
    {
        pOInto.Clear();

        IReadOnlyList<UWCritter> lOActive = UWCritter.Active;

        for (int liAt = 0; liAt < lOActive.Count; liAt++)
        {
            UWCritter lOCritter = lOActive[liAt];

            if (lOCritter == null)
                continue;

            UWCreatureRef lOCreature = RefOf(lOCritter);

            if (UWCreatureSearch.Matches(lOCreature, pOArea, peFilter))
                pOInto.Add(lOCreature);
        }

        return pOInto.Count;
    }

    /// <summary>
    /// FindCreatures for the Unity hosts that act on the creature's component (its sight, its
    /// anger, its path, its place): the same walk and the same test, handing out the UWCritter.
    /// Clears pOInto and returns how many.
    ///
    /// THE WALK IS UWCritter.Active (cleanup step 3.2, 2026-10-17), the list OnEnable and OnDisable
    /// keep - the same set as the scene search it replaces (FindObjectsByType without inactive
    /// objects; nothing disables a UWCritter component on an active object), in the order the
    /// creatures came into the scene instead of no promised order. pOInto is a snapshot: a caller
    /// may remove a creature while it walks the result, as the special deaths do.
    /// </summary>
    public int FindCritters(UWTileArea pOArea, UWCreatureFilter peFilter, List<UWCritter> pOInto)
    {
        pOInto.Clear();

        IReadOnlyList<UWCritter> lOActive = UWCritter.Active;

        for (int liAt = 0; liAt < lOActive.Count; liAt++)
        {
            UWCritter lOCritter = lOActive[liAt];

            if (lOCritter != null && UWCreatureSearch.Matches(RefOf(lOCritter), pOArea, peFilter))
                pOInto.Add(lOCritter);
        }

        return pOInto.Count;
    }

    /// <summary>A creature as the engine-free search sees it. The object is its entity's
    /// (UWEntityInfo on the creature's own object, UWObjectSpawner - the theft search's
    /// GetComponentInChildren found that same one first); the body tile is the transform's, by
    /// the loader's WorldPositionToTile arithmetic (UWTileQueries.WorldToTile).</summary>
    public static UWCreatureRef RefOf(UWCritter pOCritter)
    {
        UWEntityInfo lOEntity = pOCritter.GetComponent<UWEntityInfo>();
        Vector3 lOBody = pOCritter.transform.position;

        return new UWCreatureRef(lOEntity != null ? lOEntity.ObjectData : null, pOCritter.Record,
            UWTileQueries.WorldToTile(lOBody.x, lOBody.z), pOCritter.IsDead, pOCritter.Index);
    }

    // ------------------------------------------------- Damage (cleanup step 3.3)

    private Bodies mOBodies;

    /// <summary>
    /// A blow lands (IUWWorld.Damage): the one way every object and creature damage site, the
    /// missiles, the blasts, the target spells and the trap, sleep, spell and item-use hosts hurt
    /// what they strike since cleanup step 3.3 (2026-10-17). The landing itself is the engine-free
    /// UWBlowRouting; the bodies below only carry it out.
    /// </summary>
    public void Damage(UWDamageTarget pOTarget, UWBlow pOBlow)
    {
        UWBlowRouting.Land(mOBodies ?? (mOBodies = new Bodies(this)), pOTarget, pOBlow);
    }

    /// <summary>A site's body that is missing: the site found none, so nothing is looked up for it
    /// either - the blow falls on nothing, as the site's own call on a null body did.</summary>
    private static readonly object NoBody = new object();

    /// <summary>A creature as the target of a blow, with the body the site holds (its
    /// UWDamageable).</summary>
    public static UWDamageTarget TargetOf(UWCritter pOCritter, UWDamageable pOBody)
    {
        return UWDamageTarget.Creature(pOCritter != null ? pOCritter.Index : 0, (object)pOBody ?? NoBody);
    }

    /// <summary>A door, a chest or another object with hit points as the target of a blow, with the
    /// body the site holds.</summary>
    public static UWDamageTarget TargetOf(UWDamageable pOBody)
    {
        return UWDamageTarget.ObjectOf(null, (object)pOBody ?? NoBody);
    }

    /// <summary>
    /// The bodies of the Unity scene behind UWBlowRouting: each member does exactly what the
    /// damage sites did themselves until 2026-10-17. A target without a body is looked up - a
    /// creature by its index (UWCritter.AtIndex), an object through its entity.
    /// </summary>
    private sealed class Bodies : IUWBlowBodies
    {
        private readonly UWWorld mOWorld;

        public Bodies(UWWorld pOWorld)
        {
            mOWorld = pOWorld;
        }

        private UWDamageable fBodyOf(UWDamageTarget pOTarget)
        {
            if (pOTarget.Body is UWDamageable lOBody)
                return lOBody;

            if (pOTarget.Body == NoBody)
                return null;

            if (pOTarget.Kind == UWDamageTargetKind.Creature)
            {
                // The registry by index (cleanup step 4.8, 2026-10-18; a walk of Active until then).
                UWCritter lOCritter = UWCritter.AtIndex(pOTarget.Index);

                return lOCritter != null ? lOCritter.Damageable : null;
            }

            UWLevelLoader lOLoader = mOWorld.Loader;

            if (pOTarget.Object != null && lOLoader != null && lOLoader.TryGetEntity(pOTarget.Object, out UWEntityInfo lOEntity)
                && lOEntity != null)
                return lOEntity.GetComponentInParent<UWDamageable>();

            return null;
        }

        public bool CreatureTakesBlows(UWDamageTarget pOTarget)
        {
            UWDamageable lOBody = fBodyOf(pOTarget);

            return lOBody != null && !lOBody.IsDestroyed;
        }

        public void DamageBody(UWDamageTarget pOTarget, int piDamage, int piDamageType, int piAttacker)
        {
            UWDamageable lOBody = fBodyOf(pOTarget);

            if (lOBody != null)
                lOBody.ApplyDamage(piDamage, piDamageType, piAttacker);
        }

        /// <summary>The player's end: the request through the damage pipeline (cleanup step 4.2,
        /// 2026-10-17).</summary>
        public void DamagePlayer(UWDamageRequest pORequest)
        {
            UWCharacter lOPlayer = UWScene.Character;

            if (lOPlayer != null)
                lOPlayer.Damage(pORequest);
        }

        public void StrikeThingInFlight(int piFlightIndex, int piDamage, int piDamageType)
        {
            UWMissiles lOMissiles = UWMissiles.Ensure(mOWorld.Loader);

            if (lOMissiles != null)
                lOMissiles.StrikeThing(piFlightIndex, piDamage, piDamageType);
        }
    }

    // ------------------------------------------------- Doors (cleanup step 3.6)

    private DoorList mODoors;

    /// <summary>The level's doors: UWDoor's registry (every UWDoor that woke, in that order; a
    /// destroyed one leaves it in OnDestroy).</summary>
    public IUWDoors Doors => mODoors ?? (mODoors = new DoorList(this));

    private sealed class DoorList : IUWDoors
    {
        private readonly UWWorld mOWorld;

        public DoorList(UWWorld pOWorld)
        {
            mOWorld = pOWorld;
        }

        public IReadOnlyList<IUWDoor> All => UWDoor.All;

        /// <summary>The door component on the door object's entity or above it - the lookup the
        /// trap host's FireDoorTrap and gronk_door (UWTriggerSystem.TrySetDoorState) made each
        /// until cleanup step 3.6 (2026-10-17). Null without a level or an entity.</summary>
        public IUWDoor Find(UWObject pODoorData)
        {
            UWLevelLoader lOLoader = mOWorld.Loader;

            if (lOLoader == null || lOLoader.CurrentLevel == null
                || !lOLoader.TryGetEntity(pODoorData, out UWEntityInfo lOEntity))
                return null;

            UWDoor lODoor = lOEntity.GetComponentInParent<UWDoor>();

            return lODoor != null ? lODoor : null;
        }
    }

    // ------------------------------------------------- Objects (cleanup step 3.6)

    /// <summary>UWLevelLoader.RemoveObjectFromWorld - what the special deaths' host, the
    /// conversation's remove_talker and the trap host called on the loader each.</summary>
    public bool Remove(UWObject pOObject)
    {
        return Loader != null && pOObject != null && Loader.RemoveObjectFromWorld(pOObject);
    }

    /// <summary>UWInventory.GiveToPlayer with its floor case presented (the shrine's host until
    /// cleanup step 3.6, 2026-10-17); false without an inventory.</summary>
    public bool GiveToHand(UWObject pOItem, bool pbPreferBags)
    {
        UWInventory lOInventory = UWScene.Inventory;

        if (lOInventory == null || lOInventory.Model == null)
            return false;

        return lOInventory.GiveToPlayer(pOItem, pbPreferBags);
    }

    /// <summary>UWLevelLoader.SpawnObjectInTileAtTemplatePosition (the object's own fine spot and
    /// height) - the creation trap's copy and the conversation's place_object, each on its host
    /// until cleanup step 3.6 (2026-10-17).</summary>
    public bool Spawn(UWObject pOObject, UWTilePos pOTile)
    {
        return Loader != null && Loader.SpawnObjectInTileAtTemplatePosition(pOObject, pOTile.X, pOTile.Y);
    }

    /// <summary>UWLevelLoader.SpawnObjectById with CreateObj's own quality and the given count or
    /// link (the trap host's SpawnObjectById with 0, and the endgame host's SpawnObject with the
    /// moongate's link, until cleanup step 3.6, 2026-10-17).</summary>
    public bool SpawnNewObject(int piObjectId, UWTilePos pOTile, int piQuantity, UWWorldEdit.Arrival peArrival)
    {
        return Loader != null && Loader.SpawnObjectById(piObjectId, pOTile.X, pOTile.Y, peArrival, piQuantity);
    }

    /// <summary>
    /// place_object with a negative x (BABLHACK.C): put_at at the player's tile with its fine
    /// position 0/0, drop_around_place's stay_centered set (that point is tried first), range 6
    /// and nocull - the object is never culled. Until 2026-10-12 a random spot of the player's
    /// tile, a roll the original never makes. On the conversation host until cleanup step 3.6
    /// (2026-10-17).
    /// </summary>
    public void DropAtPlayer(UWObject pOItem)
    {
        UWLevelLoader lOLoader = Loader;

        // Nothing without a view to find the feet by (the avatar's TryGetFeetPoint).
        if (lOLoader == null || pOItem == null || !Avatar.TryGetFeetPoint(out Vector3 lOFeet))
            return;

        UWTilePos lOTile = lOLoader.WorldPositionToTile(lOFeet);
        Vector3 lOCorner = new Vector3(UWUnits.EighthsToWorldAxis(lOTile.X << 3), lOFeet.y,
            UWUnits.EighthsToWorldAxis(lOTile.Y << 3));

        lOLoader.SpawnScatteredAround(pOItem, lOCorner, PlaceObjectRange, lOTile, false, true, true);
    }

    /// <summary>place_object's range at the player's feet, in eighths.</summary>
    private const int PlaceObjectRange = 6;

    public void DropAroundPlayer(UWObject pOItem)
    {
        UWLevelLoader lOLoader = Loader;

        if (lOLoader == null || pOItem == null || !Avatar.TryGetFeetPoint(out Vector3 lOFeet))
            return;

        // No second try when it returns false: then the culling test took it, as in the original.
        lOLoader.SpawnScatteredAround(pOItem, lOFeet, BarterDropRadius, lOLoader.WorldPositionToTile(lOFeet));
    }

    /// <summary>Radius of end_barter's PlaceObjectAtNPC, in eighths of a tile.</summary>
    private const int BarterDropRadius = 5;

    /// <summary>
    /// near_mob_put_at(talking_to, item, 5, cull): around the partner's own spot, radius 5, the
    /// culling test deciding when no spot fits. False when the culling test took it (the 3 of
    /// npc_barter_give). Until 2026-10-12 a failure went on to the player's tile, which the
    /// original never does. The conversation host's DropAtNpc until cleanup step 3.6 (2026-10-17).
    /// </summary>
    public bool DropAtCreature(UWObject pOItem, UWNpc pONpc)
    {
        UWLevelLoader lOLoader = Loader;

        if (lOLoader == null || pOItem == null || pONpc == null)
            return false;

        UWEntityInfo lOEntity;
        Vector3 lOCentre = lOLoader.TryGetEntity(pONpc, out lOEntity) && lOEntity != null
            ? lOEntity.transform.position
            : UnderworldRevisited.Build.UWObjectSpawner.GetObjectWorldPosition(pONpc.TileX, pONpc.TileY, pONpc);

        return lOLoader.SpawnScatteredAround(pOItem, lOCentre, BarterDropRadius, lOLoader.WorldPositionToTile(lOCentre));
    }

    /// <summary>UWLevelLoader.SpawnDroppedObject at the point on the floor of its tile, not
    /// requiring a flat floor (the spell host's SpawnObjectAt until cleanup step 3.6, 2026-10-17).
    /// At the head of the tile's list: creat_spell Obj_Adds what it conjures (2026-10-18).</summary>
    public bool DropObjectAt(UWObject pOObject, float pfWorldX, float pfWorldZ)
    {
        return Loader != null && Loader.SpawnDroppedObject(pOObject, new Vector3(pfWorldX, 0f, pfWorldZ), false,
            UWWorldEdit.Arrival.Created);
    }

    public bool SpawnWardRune(int piTileX, int piTileY)
    {
        return Loader != null && Loader.SpawnWardRune(piTileX, piTileY);
    }

    /// <summary>Places a boulder below the ceiling and lets it fall - a piece of the quake's
    /// debris on the motion core (UWMissiles.DropBoulder, since 2026-10-05).</summary>
    public bool DropBoulder(int piObjectId, int piTileX, int piTileY)
    {
        return Loader != null && UWMissiles.Ensure(Loader).DropBoulder(piObjectId, piTileX, piTileY) != null;
    }

    /// <summary>
    /// CheckIfItemFitsInTile_seg026_1008 for a summoned creature: its COMOBJ footprint (radius
    /// in eighths around the spot) from the floor up to its COMOBJ height; blocked by a living
    /// creature, the player's body, or an object with a COMOBJ height (a barrel, a door) -
    /// the same records ScanForCollisions gathers. Items lying on the floor (height 0) do not
    /// count, as they do not in the original's scan. Moved from the spell host (cleanup step 3.6,
    /// 2026-10-17); the physics query stays as it was.
    /// </summary>
    public bool CreatureFitsAt(int piObjectId, float pfWorldX, float pfWorldZ)
    {
        UWLevelLoader lOLoader = Loader;

        if (lOLoader == null || lOLoader.UWDataImporter == null)
            return true;

        UWCommonObjectProperties lOCommon = lOLoader.UWDataImporter.CommonObjectProperties;
        int liRadius = 1;
        int liHeight = 1;

        if (lOCommon != null && lOCommon.TryGet(piObjectId, out UWCommonObjectProperties.Entry lOOwn))
        {
            liRadius = Mathf.Max(1, (int)lOOwn.Radius);
            liHeight = Mathf.Max(1, (int)lOOwn.Height);
        }

        float lfHalfWidth = liRadius * UWWorldScale.SubTileStep;
        float lfHalfHeight = liHeight * UWWorldScale.ZPosStep * 0.5f;
        Vector3 lOFloor = new Vector3(pfWorldX, 0f, pfWorldZ);

        lOFloor.y = lOLoader.GetFloorHeightAt(lOFloor);

        Collider[] lOHits = Physics.OverlapBox(lOFloor + (Vector3.up * lfHalfHeight),
            new Vector3(lfHalfWidth, lfHalfHeight, lfHalfWidth), Quaternion.identity, Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        foreach (Collider lOCollider in lOHits)
        {
            if (lOCollider.GetComponentInParent<UWProjectileFlight>() != null)
                continue;

            if (lOCollider is CharacterController)
                return false;

            UWCritter lOCritter = lOCollider.GetComponentInParent<UWCritter>();

            if (lOCritter != null)
            {
                UWDamageable lODamageable = lOCritter.GetComponent<UWDamageable>();

                if (lODamageable != null && !lODamageable.IsDestroyed)
                    return false;

                continue;
            }

            UWEntityInfo lOEntity = lOCollider.GetComponentInParent<UWEntityInfo>();

            if (lOEntity != null && lOEntity.ObjectData != null && lOCommon != null
                && lOCommon.TryGet(lOEntity.ObjectData.ID, out UWCommonObjectProperties.Entry lOEntry)
                && lOEntry.Height > 0)
                return false;
        }

        return true;
    }

    // ------------------------------------------------- Area spells (cleanup step 3.6)

    /// <summary>
    /// The creature behind a level object an area spell's walk hands over (UWAreaWalk over the
    /// squares' lists): the entity the loader made for it. Since cleanup step 4.0 (2026-10-18,
    /// decision 11 of Docs/CLEANUP-PLAN.md) the rules choose the creatures by the tile lists, as
    /// process_area does; until then this world collected the area targets with the physics
    /// (CollectAreaTargets: everything damageable within (radius + 2) * 1.5 tiles of the centre,
    /// each on the tile its transform stood on).
    /// </summary>
    private UWEntityInfo fEntityOf(UWObject pOCreature)
    {
        return Loader != null && pOCreature != null && Loader.TryGetEntity(pOCreature, out UWEntityInfo lOEntity)
            ? lOEntity
            : null;
    }

    public UWTargetSpellRules.Result ConfuseCreature(UWObject pOCreature)
    {
        UWEntityInfo lOEntity = fEntityOf(pOCreature);
        UWCritter lOCritter = lOEntity != null ? lOEntity.GetComponentInParent<UWCritter>() : null;

        return lOCritter != null ? lOCritter.Confuse() : UWTargetSpellRules.Result.Missed;
    }

    public UWTargetSpellRules.Result CastTargetSpellOnCreature(UWObject pOCreature, int piMinorClass)
    {
        return UWTargetSpell.Cast(piMinorClass, fEntityOf(pOCreature));
    }

    /// <summary>The picture at the middle of the creature's collider, where it stood on the
    /// collider the physics found until 2026-10-18; at the entity's own point without one.</summary>
    public void ShowEffectAtCreature(UWObject pOCreature, int piEffectObjectId, float pfSeconds)
    {
        UWEntityInfo lOEntity = fEntityOf(pOCreature);

        if (Loader == null || lOEntity == null)
            return;

        Collider lOCollider = lOEntity.GetComponentInChildren<Collider>();

        Loader.SpawnEffectAt(piEffectObjectId,
            lOCollider != null ? lOCollider.bounds.center : lOEntity.transform.position, pfSeconds);
    }

    /// <summary>UWTileBlast on the tile - the area spells' DamageObjectsInTile.</summary>
    public void StrikeTile(UWTilePos pOTile, int piDiceCount, int piDiceRange, int piDamageType, int piAttacker)
    {
        UWTileBlast.Strike(Loader, pOTile, piDiceCount, piDiceRange, piDamageType, piAttacker);
    }

    // ------------------------------------------------- A found creature (cleanup step 3.6)

    /// <summary>The creature's own talk (ICritterHost.OnTalk: nothing while a conversation is
    /// open, else Interaction.TryTalkToNpcData) - the special deaths' TalkToThisCreature until
    /// cleanup step 3.6 (2026-10-17).</summary>
    public void TalkTo(UWCreatureRef pOCreature)
    {
        UWCritter lOCritter = CritterOf(pOCreature);

        if (lOCritter != null)
            ((ICritterHost)lOCritter).OnTalk();
    }

    /// <summary>The creature behind a reference: the active creature that carries its record (the
    /// record is the creature's own, made once per body). Null when none does any more.</summary>
    public static UWCritter CritterOf(UWCreatureRef pOCreature)
    {
        if (pOCreature.Record == null)
            return null;

        // The registry by index first (cleanup step 4.8, 2026-10-18); a creature without an index
        // (no data) is still walked for.
        UWCritter lOByIndex = UWCritter.AtIndex(pOCreature.Index);

        if (lOByIndex != null && lOByIndex.Record == pOCreature.Record)
            return lOByIndex;

        IReadOnlyList<UWCritter> lOActive = UWCritter.Active;

        for (int liAt = 0; liAt < lOActive.Count; liAt++)
        {
            UWCritter lOCritter = lOActive[liAt];

            if (lOCritter != null && lOCritter.Record == pOCreature.Record)
                return lOCritter;
        }

        return null;
    }

    public int CreatureTravelRange(UWCreatureRef pOCreature)
    {
        UWCritter lOCritter = CritterOf(pOCreature);

        return lOCritter != null ? lOCritter.TravelRange : 0;
    }

    public bool TryGetCreaturePath(UWCreatureRef pOCreature, UWTilePos pOTo, int piToHeight, List<UWTilePos> pOPath)
    {
        UWCritter lOCritter = CritterOf(pOCreature);

        return lOCritter != null && lOCritter.TryGetPathTo(pOTo, piToHeight, pOPath);
    }

    public bool PlaceCreature(UWCreatureRef pOCreature, UWTilePos pOTile, int piSubX, int piSubY)
    {
        UWCritter lOCritter = CritterOf(pOCreature);

        return lOCritter != null && lOCritter.PlaceAtSpot(pOTile, piSubX, piSubY);
    }

    // ------------------------------------------------- The level's own time (cleanup step 3.6)

    /// <summary>The respawn pass on the level (UWTriggerSystem.RespawnCreatures; a component on
    /// the level loader's object until 2026-10-17).</summary>
    public int RunRespawn(bool pbAnyDistance)
    {
        return UWTriggerSystem.RespawnCreatures(Loader, pbAnyDistance);
    }

    /// <summary>The level-exit pass on the living level (UWLevelExit.PassTime, 2026-10-15): the
    /// creatures stay in the scene, so their bodies follow what the pass wrote. Moved from the
    /// sleep's host (cleanup step 3.6, 2026-10-17).</summary>
    public void LetTimePassForCreatures()
    {
        UWLevelLoader lOLoader = Loader;

        if (lOLoader == null || lOLoader.CurrentLevel == null)
            return;

        int liSentHome = UWLevelExit.PassTime(lOLoader.WorldEdit, new NightCreatureHost(lOLoader), out int liRemoved);

        if (liSentHome > 0 || liRemoved > 0)
            Debug.Log(string.Format("The night passed: {0} creatures sent home, {1} removed.", liSentHome, liRemoved));
    }

    public int CollectGarbage(int piRange, int piLimit)
    {
        UWLevelLoader lOLoader = Loader;

        return lOLoader != null && lOLoader.CurrentLevel != null
            ? lOLoader.WorldEdit.CollectGarbage(piRange, piLimit)
            : 0;
    }

    /// <summary>What the night's time pass asks of the scene: a temporary creature vanishes at
    /// once - inactive before the respawner looks for crowding, as the original frees it before
    /// DoWanderingMonsters - and a changed creature's body and health follow its record.</summary>
    private sealed class NightCreatureHost : IUWCreatureTimeHost
    {
        private readonly UWLevelLoader mOLoader;

        public NightCreatureHost(UWLevelLoader pOLoader)
        {
            mOLoader = pOLoader;
        }

        public void RemoveCreature(UWNpc pONpc)
        {
            if (mOLoader.TryGetEntity(pONpc, out UWEntityInfo lOEntity) && lOEntity != null)
                lOEntity.gameObject.SetActive(false);

            UWLevelExit.RemoveFromLevel(mOLoader.WorldEdit, pONpc);
        }

        public void CreatureChanged(UWNpc pONpc)
        {
            if (!mOLoader.TryGetEntity(pONpc, out UWEntityInfo lOEntity) || lOEntity == null)
                return;

            UWCritter lOCritter = lOEntity.GetComponent<UWCritter>();

            if (lOCritter != null)
                lOCritter.FollowRecord();
        }
    }

    // ------------------------------------------------- Spots and world points (Unity only)

    /// <summary>
    /// A world point as a spot: the tile it lies in (UWTileQueries.WorldToTile, clamped to the
    /// map) and the eighth CELL it lies in (UWTileQueries.WorldToEighthFloor, the original's
    /// (pos &gt;&gt; 5) &amp; 7 - decision 21 of Docs/CLEANUP-PLAN.md), at the given zpos.
    /// </summary>
    public static UWSpot SpotAt(Vector3 pOPoint, int piZPos)
    {
        return new UWSpot(UWTileQueries.WorldToTile(pOPoint.x, pOPoint.z),
            UWTileQueries.WorldToEighthFloor(pOPoint.x), UWTileQueries.WorldToEighthFloor(pOPoint.z), piZPos);
    }

    /// <summary>A spot as a world point: its eighth on each axis (UWUnits.SubTileToWorldAxis) at
    /// its zpos.</summary>
    public static Vector3 PointOf(UWSpot pOSpot)
    {
        return new Vector3(UWUnits.SubTileToWorldAxis(pOSpot.Tile.X, pOSpot.FineX), UWUnits.ZPosToWorld(pOSpot.ZPos),
            UWUnits.SubTileToWorldAxis(pOSpot.Tile.Y, pOSpot.FineY));
    }

    /// <summary>The zpos of the floor under a world point, clamped to 0..127.</summary>
    public int FloorZPosAt(Vector3 pOPoint)
    {
        return Loader != null ? Mathf.Clamp(UWUnits.WorldToZPos(Loader.GetFloorHeightAt(pOPoint)), 0, 127) : 0;
    }
}
