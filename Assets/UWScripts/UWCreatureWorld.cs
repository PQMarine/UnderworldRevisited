using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE CREATURES' SIDE OF THE WORLD (cleanup step 4.11, 2026-10-18): the Unity implementation of
/// ICreatureWorld, one per level, owned by the level's UWWorld (UWWorld.Creatures) like its
/// presentation and avatar. It answers what the engine-free UWCreatureActor cannot take from the
/// data: the level's motion engines (UWMotionHost), the actor of a level object, the player's body
/// as the mind sees it and as a blow's defender, the melee defender search (physics probes), the
/// pictures and sounds of a blow, the missile launch (UWMissiles), the talk and the music.
///
/// The members moved here from UWCritter with their comments; they read the scene on every call
/// and keep no state, so the one object serves every creature of the level.
/// </summary>
public sealed class UWCreatureWorld : ICreatureWorld
{
    // ------------------------------------------------- Constants (from UWCritter)

    /// <summary>Layer of the sprites - the same number as in UWObjectSpawner.SpriteLayer. It is
    /// left out when searching for the floor and for a line of sight.</summary>
    private const int SpriteLayer = 8;

    /// <summary>
    /// What the blow probe of fScanForDefender may meet: EVERY layer. Until 2026-09-27 the
    /// sprite layer was left out, so that items lying about would not catch a blow - but the
    /// creatures' bodies are on that layer too (UWObjectSpawner gives every object's collider
    /// the sprite layer), so a creature's blow could never find another creature: "nothing in
    /// reach" with the reaper right in front of a summoned feral troll, and the reaper never
    /// turned on it (per user). The probe now filters instead: on the sprite layer only a
    /// living creature counts, as defender and as obstacle alike.
    /// </summary>
    private const int SweepLayerMask = Physics.AllLayers;

    /// <summary>How far in front of the surface a blow's impact sits, in world units.</summary>
    private const float BlowEffectSetback = 4f;

    /// <summary>COMOBJ radius of every door, open or closed, portcullis included (0x140-0x14F).</summary>
    private const int DoorRadiusEighths = 3;

    /// <summary>An open door object stands this much above its floor, in zpos (UWWorldCapture,
    /// the original's data).</summary>
    private const int OpenDoorRaiseZ = 24;

    /// <summary>The impact on a door sits this much above the door object, in zpos.</summary>
    private const int DoorImpactAboveZ = 2;

    /// <summary>World units the impact on an object is moved towards the viewer - 2 per user,
    /// 2026-09-29 (4, the setback on the world, was tried first).</summary>
    private const float ObjectImpactSetback = 2f;

    private readonly UWWorld mOWorld;

    public UWCreatureWorld(UWWorld pOWorld)
    {
        mOWorld = pOWorld;
    }

    public IUWWorld World => mOWorld;

    private UWLevelLoader fLoader => mOWorld != null ? mOWorld.Loader : null;

    // ------------------------------------------------- The motion engines

    private UWMotionHost fMotionHost()
    {
        UWLevelLoader lOLoader = fLoader;

        return lOLoader != null ? UWMotionHost.Ensure(lOLoader) : null;
    }

    public UWCreatureMotion Motion
    {
        get
        {
            UWMotionHost lOHost = fMotionHost();

            return lOHost != null ? lOHost.CreatureMotion : null;
        }
    }

    public UWTileRoute Route
    {
        get
        {
            UWMotionHost lOHost = fMotionHost();

            return lOHost != null ? lOHost.TileRoute : null;
        }
    }

    public UWTileTraverse Traverse
    {
        get
        {
            UWMotionHost lOHost = fMotionHost();

            return lOHost != null ? lOHost.TileTraverse : null;
        }
    }

    public bool TryGetStaticObject(int piIndex, out UWObject pOObject)
    {
        pOObject = null;

        if (!UWMotionHost.IsStaticIndex(piIndex))
            return false;

        UWMotionHost lOHost = fMotionHost();

        pOObject = lOHost != null ? lOHost.StaticObjectAt(piIndex) : null;

        return true;
    }

    public void MoveObjectData(UWNpc pONpc, int piTileX, int piTileY)
    {
        UWLevelLoader lOLoader = fLoader;

        if (lOLoader != null && pONpc != null)
            lOLoader.MoveObjectData(pONpc, piTileX, piTileY);
    }

    // ------------------------------------------------- The other creatures

    /// <summary>The creature component on the object's entity or above it - the lookup UWCritter
    /// made for a snapshot (fCritterOf) until cleanup step 4.11.</summary>
    public UWCreatureActor ActorOf(UWObject pOObject)
    {
        UWLevelLoader lOLoader = fLoader;
        UWEntityInfo lOEntity;

        if (lOLoader == null || !lOLoader.TryGetEntity(pOObject, out lOEntity) || lOEntity == null)
            return null;

        UWCritter lOCritter = lOEntity.GetComponentInParent<UWCritter>();

        return lOCritter != null ? lOCritter.Actor : null;
    }

    // ------------------------------------------------- The player

    public bool HasPlayer => UWScene.Character != null;

    /// <summary>The player as the brain sees him: position in eighths from the camera's
    /// horizontal position, feet in zpos, the facing eighth from the yaw, loudness and
    /// visibility nibbles as UWPlayerVitals keeps them (deviation 24), the drawn weapon from
    /// the interaction's combat mode (PlayerData 0x5F bit 1 in the original).</summary>
    public CritterTarget SnapshotPlayer()
    {
        CritterTarget lOTarget = new CritterTarget();
        UWCharacter lOPlayer = UWScene.Character;

        if (lOPlayer == null)
            return lOTarget;

        Transform lOPlayerTransform = lOPlayer.transform;
        Vector3 lOAt = lOPlayerTransform.position;
        UWTilePos lOTile = UWTileQueries.WorldToTile(lOAt.x, lOAt.z);

        lOTarget.Exists = true;
        lOTarget.Index = UWCritterBrain.PlayerIndex;
        lOTarget.IsPlayer = true;
        lOTarget.IsCreature = true;
        lOTarget.ItemId = UWCritterBrain.PlayerItemId;
        lOTarget.X = UWTileQueries.WorldToEighths(lOAt.x);
        lOTarget.Y = UWTileQueries.WorldToEighths(lOAt.z);
        lOTarget.ZPos = Mathf.Clamp(lOPlayer.GetFeetZPos(), 0, 0x7F);
        lOTarget.TileX = lOTile.X;
        lOTarget.TileY = lOTile.Y;
        lOTarget.ObjectHeight = lOPlayer.GetBodyHeightZ();
        lOTarget.FacingEighth = UWPlayerThrow.GetPlayerHeading(lOPlayerTransform) >> 5;
        lOTarget.Loudness = lOPlayer.Vitals.Quietness;
        lOTarget.Visibility = lOPlayer.Vitals.Visibility;
        lOTarget.HitPoints = lOPlayer.Vitals.CurrentHP > 0f ? Mathf.Max(1, (int)lOPlayer.Vitals.CurrentHP) : 0;
        lOTarget.Goal = 0;
        lOTarget.IsAlly = false;
        lOTarget.Kind = 0;

        Interaction lOInteraction = UWScene.Interaction;

        lOTarget.WeaponDrawn = lOInteraction != null && lOInteraction.IsCombatModeActive;

        return lOTarget;
    }

    /// <summary>The remote camera's tile while it shows (camera trap, Roaming Sight), else the
    /// player's - see ICritterHost.ViewTile.</summary>
    public UWTilePos ViewTile
    {
        get
        {
            UWRemoteCamera lORemote = UWRemoteCamera.Current;

            if (lORemote != null && lORemote.IsActive && lORemote.ViewCamera != null)
            {
                Vector3 lOEye = lORemote.ViewCamera.transform.position;

                return UWTileQueries.WorldToTile(lOEye.x, lOEye.z);
            }

            UWCharacter lOPlayer = UWScene.Character;

            if (lOPlayer == null)
                return new UWTilePos(-100, -100);

            Vector3 lOAt = lOPlayer.transform.position;

            return UWTileQueries.WorldToTile(lOAt.x, lOAt.z);
        }
    }

    /// <summary>The player's status refreshed (his defence and protection of the part are the
    /// status pass's), then his combatant (UWCharacter.Combatant), his feet and his body's
    /// height in zpos - what the creature's blow on him reads.</summary>
    public bool TryGetPlayerAsDefender(out IUWAttackDefender pIDefender, out int piFeetZ, out int piBodyHeightZ)
    {
        UWCharacter lOPlayer = UWScene.Character;

        pIDefender = null;
        piFeetZ = 0;
        piBodyHeightZ = 0;

        if (lOPlayer == null)
            return false;

        lOPlayer.RefreshStatus();

        pIDefender = lOPlayer.Combatant;
        piFeetZ = lOPlayer.GetFeetZPos();
        piBodyHeightZ = lOPlayer.GetBodyHeightZ();

        return true;
    }

    // ------------------------------------------------- Melee

    /// <summary>Where a blow met an object: its body and the point (for the impact).</summary>
    private sealed class ObjectPlace
    {
        public readonly UWDamageable Body;

        public readonly Vector3 At;

        public ObjectPlace(UWDamageable pOBody, Vector3 pOAt)
        {
            Body = pOBody;
            At = pOAt;
        }
    }

    private static UWMeleeContact fObjectContact(UWDamageable pOBody, Vector3 pOAt)
    {
        return UWMeleeContact.OfObject(UWWorld.TargetOf(pOBody), pOBody != null && pOBody.TakesBlows,
            pOBody != null ? pOBody.name : null, new ObjectPlace(pOBody, pOAt));
    }

    /// <summary>
    /// The defender of a creature's blow (the front of UWCritter.OnBlow until cleanup step 4.11):
    /// the capsule probe (fScanForDefender), then a door nearer than what it met, then the player,
    /// a creature, an object with hit points, or the world.
    /// </summary>
    public UWMeleeContact FindMeleeDefender(UWCreatureActor pOAttacker)
    {
        UWCritter lOAttacker = pOAttacker != null ? pOAttacker.View as UWCritter : null;

        if (lOAttacker == null)
            return UWMeleeContact.Nothing;

        Vector3 lOBody = lOAttacker.BodyPosition;

        RaycastHit lOHit;
        UWCritter lODefender;
        bool lbPlayer;

        bool lbFound = fScanForDefender(lOAttacker, pOAttacker, out lOHit, out lODefender, out lbPlayer);

        // A DOOR NEARER THAN THAT IS THE DEFENDER (per user, 2026-09-29, SAVE3 of the original: a
        // goblin standing in the frame of an OPEN door strikes the door, not the player behind
        // it). The door is an object of COMOBJ radius 3 in its tile, open or closed; the open
        // door's colliders sit raised above the head (see UWObjectSpawner, OpenDoorRaise), so
        // the physics probe never sees it - the door is tested in the plane.
        float lfDefenderDistance = float.MaxValue;

        if (lbFound)
        {
            UWCharacter lOPlayer = UWScene.Character;
            Vector3 lODefenderAt = lbPlayer && lOPlayer != null ? lOPlayer.transform.position
                : (lODefender != null ? lODefender.BodyPosition : lOHit.point);

            lfDefenderDistance = fFlatDistance(lOBody, lODefenderAt);
        }

        if (fTryDoorInReach(pOAttacker, lOBody, lfDefenderDistance, out UWDoor lODoor))
            return fObjectContact(lODoor.Damageable, lODoor.Centre);

        if (!lbFound)
            return UWMeleeContact.Nothing;

        if (lbPlayer)
            return UWMeleeContact.Player();

        if (lODefender != null)
            return UWMeleeContact.OfCreature(lODefender.Actor);

        // AN OBJECT IN THE WAY IS THE DEFENDER (per user, 2026-09-29: a goblin striking at the
        // player through an open door knocked the door from quality 40 to 33 in the original,
        // ours never touched it). The nearest collision is the defender whatever it is;
        // CalculateAttackResults_seg022_230E_6B9 rolls no check against an object that is not a
        // creature, and AttackerAppliesFinalDamage_seg022_8A5 subtracts armour only for
        // creatures - the rolled damage, charge and flank go into DamageObject with type 4.
        UWDamageable lOObject = lOHit.collider != null ? lOHit.collider.GetComponentInParent<UWDamageable>() : null;

        // A frame post or a lintel belongs to its door (UWEntityInfo.PartOf, as for missiles).
        UWEntityInfo lOHitInfo = lOHit.collider != null ? lOHit.collider.GetComponentInParent<UWEntityInfo>() : null;

        if (lOObject == null && lOHitInfo != null && lOHitInfo.PartOf != null)
            lOObject = lOHitInfo.PartOf.GetComponent<UWDamageable>();

        if (lOObject != null && lOObject.TakesBlows && lOObject.GetComponentInParent<UWCritter>() == null)
            return fObjectContact(lOObject, lOHit.point);

        return UWMeleeContact.OfWorld(lOHit.point.ToString(), lOHit);
    }

    /// <summary>
    /// The nearest door the blow's probe meets in the plane: the probe of radius 3 eighths
    /// moved 5 eighths along the facing from the creature's centre (CheckForAttackHit_seg022_466)
    /// against the door object of radius 3 at its centre, on the creature's floor. Nearer than
    /// pfLimit (the distance of what the physics probe found), else none.
    /// </summary>
    private bool fTryDoorInReach(UWCreatureActor pOAttacker, Vector3 pOBody, float pfLimit, out UWDoor pODoor)
    {
        pODoor = null;

        UWLevelLoader lOLoader = fLoader;
        Vector3 lODirection = UWViewpoint.HeadingByteToDirection(pOAttacker.Record.FullFacing);
        float lfReach = UWCritterRules.AttackReachEighths * UWWorldScale.SubTileStep;
        float lfTouch = (UWCritterRules.AttackReachRadius + DoorRadiusEighths) * UWWorldScale.SubTileStep;
        float lfBest = pfLimit;

        foreach (UWDoor lOCandidate in UWDoor.All)
        {
            // The door object stands at height 0 - its mesh carries the floor - so its floor is
            // the tile's.
            if (lOCandidate == null || lOLoader == null
                || Mathf.Abs(lOLoader.GetFloorHeightAt(lOCandidate.Centre) - pOBody.y) > UWWorldScale.TileSize)
                continue;

            Vector3 lOOffset = lOCandidate.Centre - pOBody;

            lOOffset.y = 0f;

            float lfAlong = Mathf.Clamp(Vector3.Dot(lOOffset, lODirection), 0f, lfReach);

            if ((lOOffset - (lODirection * lfAlong)).magnitude > lfTouch)
                continue;

            float lfDistance = lOOffset.magnitude;

            if (lfDistance >= lfBest)
                continue;

            lfBest = lfDistance;
            pODoor = lOCandidate;
        }

        return pODoor != null;
    }

    private static float fFlatDistance(Vector3 pOA, Vector3 pOB)
    {
        return new Vector2(pOA.x - pOB.x, pOA.z - pOB.z).magnitude;
    }

    /// <summary>
    /// The reach scan as a capsule probe: from the creature's feet to its COMOBJ height,
    /// radius 3 eighths, cast 5 eighths along the fine heading (UWCritterRules.AttackReach*,
    /// one eighth = UWWorldScale.SubTileStep world units), sprites and triggers ignored, the
    /// creature's own colliders skipped. True with the nearest hit: the player, a creature,
    /// or the world (both out parameters null). The original's height band per swing type
    /// (attacker zpos + height * (swingType / 3) / 3, label 4CE) is replaced by the whole
    /// body: what the band chose is what PickBodyPart chooses from the heights below.
    /// </summary>
    private bool fScanForDefender(UWCritter pOAttacker, UWCreatureActor pOActor, out RaycastHit pOHit, out UWCritter pODefender,
        out bool pbPlayer)
    {
        pOHit = default;
        pODefender = null;
        pbPlayer = false;

        int liOwnRadius;
        int liOwnHeight;
        pOActor.GetOwnSize(out liOwnRadius, out liOwnHeight);

        Vector3 lOBody = pOAttacker.BodyPosition;
        float lfRadius = UWCritterRules.AttackReachRadius * UWWorldScale.SubTileStep;
        float lfHeight = Mathf.Max(liOwnHeight * UWWorldScale.ZPosStep, 2f * lfRadius);

        // THE BLOW GOES WHERE THE CREATURE LOOKS, not where its feet carry it. The original
        // hands GetCoordinateInDirection the FULL FACING - word 2 bits 7-9 times 32 plus byte
        // 0x18 bits 0-4 - not the movement heading of byte 9 (CheckForAttackHit_seg022_466,
        // labels 581-5B3). The two differ exactly in melee: a creature that is too close backs
        // off, so its feet point AWAY from the player while it faces and strikes him. We
        // searched along the feet and found nothing, which is why a reaper standing right next
        // to the player reported "nothing in reach" on every swing (per user, 2026-09-20).
        Vector3 lODirection = UWViewpoint.HeadingByteToDirection(pOActor.Record.FullFacing);
        Vector3 lOFeet = lOBody + (Vector3.up * lfRadius);
        Vector3 lOTop = lOBody + (Vector3.up * (lfHeight - lfRadius));

        // A BODY THAT ALREADY TOUCHES THE PROBE IS THE DEFENDER, and it has to be looked for
        // separately: Unity's sweeps do not report a collider that overlaps the capsule at its
        // start. The original moves a body of this radius FROM the attacker's centre
        // (GetCoordinateInDirection, then ScanForCollisions_seg026_BF6), so whoever stands
        // against a WIDE creature collides on the first step and sorts first. Without this a
        // reaper never reached the player while the original hit him from the same spot (per
        // user, 2026-09-20: "nothing in reach" where the original lands its blows).
        Collider[] lOTouching = Physics.OverlapCapsule(lOFeet, lOTop, lfRadius,
            SweepLayerMask, QueryTriggerInteraction.Ignore);

        float lfNearestCentre = float.MaxValue;
        bool lbTouched = false;

        foreach (Collider lOCandidate in lOTouching)
        {
            UWCritter lOTouchingCritter;
            bool lbTouchingPlayer;

            if (!fIsDefenderCollider(pOAttacker, lOCandidate, out lOTouchingCritter, out lbTouchingPlayer))
                continue;

            float lfCentre = Vector3.Distance(lOBody, lOCandidate.bounds.center);

            if (lbTouched && lfCentre >= lfNearestCentre)
                continue;

            lfNearestCentre = lfCentre;
            pODefender = lOTouchingCritter;
            pbPlayer = lbTouchingPlayer;
            lbTouched = true;
        }

        if (lbTouched)
            return true;

        RaycastHit[] lOHits = Physics.CapsuleCastAll(lOFeet, lOTop, lfRadius, lODirection,
            UWCritterRules.AttackReachEighths * UWWorldScale.SubTileStep, SweepLayerMask,
            QueryTriggerInteraction.Ignore);

        bool lbFound = false;
        bool lbFoundBody = false;
        float lfNearest = float.MaxValue;

        foreach (RaycastHit lOCandidate in lOHits)
        {
            if (lOCandidate.collider == null || lOCandidate.distance > lfNearest)
                continue;

            // A projectile or an effect in flight is neither a defender nor an obstacle
            // (UWProjectileFlight skips them the same way).
            if (lOCandidate.collider.GetComponentInParent<UWProjectileFlight>() != null)
                continue;

            UWCritter lOCritter;
            bool lbPlayerBody;
            bool lbBody = fIsDefenderCollider(pOAttacker, lOCandidate.collider, out lOCritter, out lbPlayerBody);

            // A creature that is removed (a dying one is a defender since cleanup step 4.12b), this
            // creature itself, or something in flight is no defender - and no obstacle either
            // (fIsDefenderCollider).
            if (lOCritter != null && !lbBody)
                continue;

            // An item on the floor (sprite layer, no body) is neither defender nor obstacle.
            if (!lbBody && lOCandidate.collider.gameObject.layer == SpriteLayer)
                continue;

            // THE FLOOR IS NOT IN THE WAY. The capsule starts at the feet, so the floor (and
            // on a slope the tile the creature stands on) overlaps its start and reports
            // distance 0 with no point - and won every blow against the player, who stood a
            // few units further ("hits the world" on every strike, never any damage, per user
            // 2026-09-20). The original's scan starts inside the attacker's own body, never
            // inside a wall or a floor: an overlap at the start and any floor or ceiling
            // (a mostly vertical normal) count for nothing unless they are a body.
            if (!lbBody && (lOCandidate.distance <= 0f || Mathf.Abs(lOCandidate.normal.y) > 0.5f))
                continue;

            // A collider that overlaps the capsule's start reports distance 0 for a body and a
            // wall alike; on that tie the body is the defender, as the original's scan finds
            // the creature before the tile behind it.
            if (lOCandidate.distance == lfNearest && (lbFoundBody || !lbBody))
                continue;

            lfNearest = lOCandidate.distance;
            pOHit = lOCandidate;
            pODefender = lOCritter;
            pbPlayer = lbPlayerBody;
            lbFound = true;
            lbFoundBody = lbBody;
        }

        return lbFound;
    }

    /// <summary>
    /// Is this collider a body the blow can land on - a living or dying creature other than the attacker,
    /// or the player? False for walls, floors, doors and anything in flight.
    /// </summary>
    private static bool fIsDefenderCollider(UWCritter pOAttacker, Collider pOCollider, out UWCritter pOCritter, out bool pbPlayer)
    {
        pOCritter = pOCollider != null ? pOCollider.GetComponentInParent<UWCritter>() : null;
        pbPlayer = false;

        if (pOCollider == null || pOCritter == pOAttacker)
            return false;

        if (pOCollider.GetComponentInParent<UWProjectileFlight>() != null)
            return false;

        // A DYING CREATURE IS A DEFENDER (cleanup step 4.12b, 2026-10-19, the user's decision "as
        // the original"): resolve_attack's probe and set_hitobj (UnderworldExhumed
        // combat/COMBAT.C:133-184, 232-272) find every creature in the tile lists, and a dying one
        // stays there until frame 3 (critter/AI.C:1140). Only one the death machine has removed is
        // gone. Until then a creature whose body was destroyed - dying - was neither defender nor
        // obstacle. A creature without an actor keeps the body's flag. The body's one rule since
        // cleanup step 4.12c (2026-10-19, UWDamageable.TakesBlows - the actor's TakesBlows); the same
        // test written out here until then.
        if (pOCritter != null)
            return pOCritter.Damageable != null && pOCritter.Damageable.TakesBlows;

        pbPlayer = fIsPlayerCollider(pOCollider);

        return pbPlayer;
    }

    /// <summary>Is this the player's own body? Recognised by the CharacterController, which
    /// only he has - NOT by UWCharacter, which sits on the CAMERA (2026-09-16).</summary>
    private static bool fIsPlayerCollider(Collider pOCollider)
    {
        return pOCollider != null && pOCollider.GetComponent<CharacterController>() != null;
    }

    public UWMeleeContact DoorContact(UWObject pODoorData)
    {
        UWLevelLoader lOLoader = fLoader;
        UWEntityInfo lOEntity;

        if (pODoorData == null || lOLoader == null || !lOLoader.TryGetEntity(pODoorData, out lOEntity) || lOEntity == null)
            return UWMeleeContact.Nothing;

        UWDoor lODoor = lOEntity.GetComponentInParent<UWDoor>();
        UWDamageable lODamageable = lODoor != null ? lODoor.Damageable : null;

        if (lODamageable == null)
            return UWMeleeContact.Nothing;

        return UWMeleeContact.OfObject(UWWorld.TargetOf(lODamageable), lODamageable.TakesBlows, lODamageable.name,
            lOEntity.transform.position);
    }

    /// <summary>Sound 3 at the avatar with the volume of the damage before the armour, or the
    /// blocked sound (7 or 8) at the player's body.</summary>
    public void ShowBlowOnPlayer(in UWAttackOutcome pOOutcome)
    {
        if (pOOutcome.Hit)
        {
            UWAudioService.PlayEffect(UWSoundEffectId.HitPlayer, pOOutcome.HitVolume);

            return;
        }

        UWCharacter lOPlayer = UWScene.Character;

        if (lOPlayer != null)
            UWAudioService.PlayEffectAt(pOOutcome.BlockedSound, lOPlayer.transform.position);
    }

    /// <summary>
    /// THE IMPACT AND THE SOUND AT THE HEIGHT OF THE BLOW (per user, 2026-09-29: the door was
    /// hit without either). The original plays sound 4 at the object with volume damage * 4
    /// and puts the impact animation 0x1CB where the blow met it, at the blow's height and at
    /// least 2 above the door's own base. A door object stands at height 0 - its mesh carries
    /// the floor - so the point takes the creature's eye height and moves a little towards it
    /// to be seen in front of the leaf.
    /// </summary>
    public void ShowBlowOnObject(UWCreatureActor pOAttacker, in UWMeleeContact pOContact, int piVolume)
    {
        ObjectPlace lOPlace = pOContact.Place as ObjectPlace;
        UWCritter lOAttacker = pOAttacker != null ? pOAttacker.View as UWCritter : null;

        if (lOPlace == null || lOAttacker == null)
            return;

        UWLevelLoader lOLoader = fLoader;
        Vector3 lOEye = lOAttacker.EyePosition;
        Vector3 lOAt = new Vector3(lOPlace.At.x, lOEye.y, lOPlace.At.z);

        // A DOOR lifts the impact to its own height + 2 (AttackerAppliesFinalDamage, door
        // branch before the impact): an open door object stands 24 above its floor, so the flash
        // shows at the underside of the lintel (per user, 2026-09-29, in the original).
        UWDoor lODoor = lOPlace.Body != null ? lOPlace.Body.GetComponent<UWDoor>() : null;

        if (lODoor != null && lOLoader != null)
        {
            int liDoorZ = (!lODoor.IsClosed ? OpenDoorRaiseZ : 0) + DoorImpactAboveZ;

            lOAt.y = Mathf.Max(lOAt.y, lOLoader.GetFloorHeightAt(lOPlace.At) + (liDoorZ * UWWorldScale.ZPosStep));
        }

        // TOWARDS THE VIEWER, not the attacker (per user, 2026-09-29: in the original the
        // impact on a door is always seen, even with the goblin on the far side of the lintel -
        // as if the player had struck the door himself). Moved to the attacker's side it hid
        // behind the leaf and the lintel.
        Camera lOCamera = Camera.main;
        Vector3 lOViewer = lOCamera != null ? lOCamera.transform.position : lOEye;
        Vector3 lOTowards = new Vector3(lOViewer.x - lOAt.x, 0f, lOViewer.z - lOAt.z);

        if (lOTowards.sqrMagnitude > 0.01f)
            lOAt += lOTowards.normalized * Mathf.Min(ObjectImpactSetback, lOTowards.magnitude);

        if (lOLoader != null)
            lOLoader.SpawnEffectAt(UWObjectMechanics.FlashEffectObjectId, lOAt, UWCritter.BlowEffectSeconds);

        UWAudioService.PlayEffectAt(UWSoundEffectId.HitCritter, lOAt, piVolume);
    }

    /// <summary>The impact flash and hit sound where the blow met the world. A collider that
    /// overlapped the capsule's start has no point (Unity reports zero); the impact then sits
    /// one radius ahead of the creature.</summary>
    public void ShowBlowOnWorld(UWCreatureActor pOAttacker, in UWMeleeContact pOContact)
    {
        UWLevelLoader lOLoader = fLoader;
        UWCritter lOAttacker = pOAttacker != null ? pOAttacker.View as UWCritter : null;

        if (lOLoader == null || lOAttacker == null || !(pOContact.Place is RaycastHit lOHit))
            return;

        Vector3 lOAt = lOHit.distance > 0f
            ? lOHit.point + (lOHit.normal * BlowEffectSetback)
            : lOAttacker.EyePosition + (UWViewpoint.HeadingByteToDirection(pOAttacker.Record.FullFacing)
                * (UWCritterRules.AttackReachRadius * UWWorldScale.SubTileStep));

        lOLoader.SpawnEffectAt(UWObjectMechanics.FlashEffectObjectId, lOAt, UWCritter.BlowEffectSeconds);
        UWAudioService.PlayEffectAt(UWSoundEffectId.HitCritter, lOAt);
    }

    public void ShowDoorBashed(in UWMeleeContact pOContact, int piVolume)
    {
        if (pOContact.Place is Vector3 lOAt)
            UWAudioService.PlayEffectAt(UWSoundEffectId.HitCritter, lOAt, piVolume);
    }

    // ------------------------------------------------- Missiles

    public bool LaunchMissile(UWCreatureActor pOLauncher, int piObjectId, int piPitch, int piBaseDamage, int piDamageType,
        int piImpactId)
    {
        UWLevelLoader lOLoader = fLoader;
        UWCritter lOCritter = pOLauncher != null ? pOLauncher.View as UWCritter : null;

        if (lOLoader == null || lOCritter == null)
            return false;

        return UWMissiles.Ensure(lOLoader).LaunchFromCreature(lOCritter, piObjectId, piPitch, piBaseDamage, piDamageType,
            piImpactId) != null;
    }

    // ------------------------------------------------- Talk and music

    public bool CanTalk => !UWConversationController.IsAnyOpen && UWScene.Interaction != null;

    public void Talk(UWNpc pONpc)
    {
        Interaction lOInteraction = UWScene.Interaction;

        if (lOInteraction != null)
            lOInteraction.TryTalkToNpcData(pONpc);
    }

    public int MusicTheme => UWAudioService.CurrentTheme;

    public void MarkCombatMusic()
    {
        UWAudioService.MarkCombat();
    }
}
