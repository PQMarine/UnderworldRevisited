using System.Collections.Generic;
using UnityEngine;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// The Unity side of a cast: what the engine-free spell rules (UWSpellCasting and the
/// class rules next to it) ask for through IUWSpellHost, read from UWCharacter, Interaction,
/// UWLevelLoader, the camera and the HUD. Built by UWGameUI per cast.
///
/// The targets of an area spell are collected here with the physics (the sphere deliberately
/// reaches too far: the cross of Flame Wind extends one tile beyond the edge, and the corners
/// of a square area lie further away than its edges - the rules filter by tile number anyway)
/// and handed to the rules as numbered entries.
/// </summary>
public sealed class UWSpellHost : IUWSpellHost
{
    /// <summary>This long an effect stays in the world - as with a projectile impact.</summary>
    private const float EffectSeconds = 0.7f;

    /// <summary>The height the boulders of Tremor come from. The reference sets zpos 0x6E; our
    /// world units are twice as fine (UWObjectSpawner.HeightScale), the ceiling is
    /// at 256.</summary>
    private const int BoulderSpawnZPos = 0x6E;

    /// <summary>Where in the tile a Tremor boulder appears, both axes.</summary>
    private const int BoulderSubTile = 3;

    /// <summary>How far a boulder falls at most before it disappears on its own.
    /// From the ceiling to the lowest floor is about 250 units.</summary>
    private const float BoulderFallRange = 512f;

    private struct Target
    {
        public UWTilePos Tile;

        public UWDamageable Damageable;

        public UWCritter Critter;

        public UWEntityInfo Entity;

        public Vector3 Centre;
    }

    private readonly UWCharacter mOCharacter;

    private readonly Interaction mOInteraction;

    private readonly UWLevelLoader mOLoader;

    private readonly UWDataImport.DataImport mOData;

    private readonly UWGameUI mOUi;

    private readonly List<Target> mOTargets = new List<Target>();

    private readonly HashSet<UWDamageable> mOSeen = new HashSet<UWDamageable>();

    public UWSpellHost(UWCharacter pOCharacter, Interaction pOInteraction, UWLevelLoader pOLoader,
        UWDataImport.DataImport pOData, UWGameUI pOUi)
    {
        mOCharacter = pOCharacter;
        mOInteraction = pOInteraction;
        mOLoader = pOLoader;
        mOData = pOData;
        mOUi = pOUi;
    }

    public UWDataImport.DataImport Data => mOData;

    public UWLevel CurrentLevel => mOLoader != null ? mOLoader.CurrentLevel : null;

    public int CurrentLevelIndex => mOLoader != null ? mOLoader.CurrentLevelIndex : 0;

    public bool IsMagicBlocked => mOCharacter != null && mOLoader != null
        && UWEndgame.IsMagicBlockedAt(mOLoader, mOCharacter.transform.position);

    // ------------------------------------------------- The caster

    public bool HasPlayer => mOCharacter != null;

    public int PlayerLevel => mOCharacter != null ? mOCharacter.Level : 0;

    public int PlayerMana => mOCharacter != null ? Mathf.FloorToInt(mOCharacter.CurrentMana) : 0;

    public int PlayerCastingSkill => mOCharacter != null ? mOCharacter.GetSkill(UWPlayerData.Skill.Casting) : 0;

    public int PlayerHunger => mOCharacter != null ? mOCharacter.Hunger : 0;

    public UWTilePos PlayerTile => mOLoader != null && mOCharacter != null
        ? mOLoader.WorldPositionToTile(mOCharacter.transform.position)
        : new UWTilePos(0, 0);

    /// <summary>The camera, or the body when there is none - both share the yaw.</summary>
    private Transform fViewTransform
    {
        get
        {
            Camera lOCamera = Camera.main;

            return lOCamera != null ? lOCamera.transform : (mOCharacter != null ? mOCharacter.transform : null);
        }
    }

    public void GetViewDirection(out float pfX, out float pfZ)
    {
        Transform lOView = fViewTransform;
        Vector3 lOFlat = lOView != null ? new Vector3(lOView.forward.x, 0f, lOView.forward.z) : Vector3.zero;

        if (lOFlat.sqrMagnitude <= 0.0001f)
            lOFlat = Vector3.forward;

        lOFlat.Normalize();

        pfX = lOFlat.x;
        pfZ = lOFlat.z;
    }

    public float ViewYawDegrees => fViewTransform != null ? fViewTransform.eulerAngles.y : 0f;

    public void GetEyePosition(out float pfX, out float pfZ)
    {
        Transform lOView = fViewTransform;

        pfX = lOView != null ? lOView.position.x : 0f;
        pfZ = lOView != null ? lOView.position.z : 0f;
    }

    public void SpendMana(int piAmount)
    {
        if (mOCharacter != null)
            mOCharacter.SpendMana(piAmount);
    }

    public void RestoreManaFromSpell(int piMinorClass)
    {
        if (mOCharacter != null)
            mOCharacter.RestoreManaFromSpell(piMinorClass);
    }

    public void Heal(int piAmount)
    {
        if (mOCharacter != null)
            mOCharacter.Heal(piAmount);
    }

    public void HealFully()
    {
        if (mOCharacter != null)
            mOCharacter.HealFully();
    }

    public bool TryAddActiveSpell(int piMajorClass, int piMinorClass, int piStability)
    {
        return mOCharacter != null && mOCharacter.TryAddActiveSpell(piMajorClass, piMinorClass, piStability);
    }

    public void CurePoison()
    {
        if (mOCharacter != null)
            mOCharacter.CurePoison();
    }

    public void ChangeHunger(int piAmount)
    {
        if (mOCharacter != null)
            mOCharacter.ChangeHunger(piAmount);
    }

    // ------------------------------------------------- Messages and sounds

    public void AddMessage(string psMessage)
    {
        if (mOInteraction != null)
            mOInteraction.AddMessage(psMessage);
    }

    public void AddGeneralMessage(int piIndex)
    {
        if (mOInteraction != null)
            mOInteraction.AddGeneralMessage(piIndex);
    }

    public string GetGeneralMessage(int piIndex)
    {
        return mOInteraction != null ? mOInteraction.GetGeneralMessage(piIndex) : string.Empty;
    }

    public void PlaySpellSound()
    {
        UWSoundEffects.PlayAtAvatar(UWSoundEffects.Spell);
    }

    public void PlaySpellFailureSound()
    {
        UWSoundEffects.PlayAtAvatar(UWSoundEffects.SpellFailure);
    }

    public void RefreshSpellIcons()
    {
        if (mOUi != null)
            mOUi.RefreshSpellIcons();
    }

    // ------------------------------------------------- Pending spells

    public void BeginProjectileSpell(int piProjectileId, int piDamage, int piSpeed, int piManaCost)
    {
        if (mOInteraction != null)
            mOInteraction.BeginSpellTargeting(piProjectileId, piDamage, piSpeed, piManaCost);
    }

    public void BeginTargetSpell(int piMajorClass, int piMinorClass)
    {
        if (mOInteraction != null)
            mOInteraction.BeginTargetSpell(piMajorClass, piMinorClass);
    }

    public void FirePendingSpell()
    {
        if (mOInteraction != null)
            mOInteraction.FirePendingSpell();
    }

    // ------------------------------------------------- The world

    public void TravelTo(int piLevelIndex, int piTileX, int piTileY)
    {
        if (mOLoader != null)
            mOLoader.TravelTo(piLevelIndex, piTileX, piTileY);
    }

    public bool SpawnObjectById(int piObjectId, int piTileX, int piTileY, int piQuantity, int piQuality)
    {
        return mOLoader != null && mOLoader.SpawnObjectById(piObjectId, piTileX, piTileY, piQuantity, piQuality);
    }

    public bool SpawnObjectInTile(UWObject pOObject, int piTileX, int piTileY)
    {
        return mOLoader != null && mOLoader.SpawnObjectInTile(pOObject, piTileX, piTileY);
    }

    public bool SpawnObjectAt(UWObject pOObject, float pfWorldX, float pfWorldZ)
    {
        return mOLoader != null && mOLoader.SpawnDroppedObject(pOObject, new Vector3(pfWorldX, 0f, pfWorldZ), false);
    }

    /// <summary>
    /// CheckIfItemFitsInTile_seg026_1008 for a summoned creature: its COMOBJ footprint (radius
    /// in eighths around the spot) from the floor up to its COMOBJ height; blocked by a living
    /// creature, the player's body, or an object with a COMOBJ height (a barrel, a door) -
    /// the same records ScanForCollisions gathers. Items lying on the floor (height 0) do not
    /// count, as they do not in the original's scan.
    /// </summary>
    public bool CreatureFitsAt(int piObjectId, float pfWorldX, float pfWorldZ)
    {
        if (mOLoader == null || mOLoader.UWDataImporter == null)
            return true;

        UWCommonObjectProperties lOCommon = mOLoader.UWDataImporter.CommonObjectProperties;
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

        lOFloor.y = mOLoader.GetFloorHeightAt(lOFloor);

        Collider[] lOHits = Physics.OverlapBox(lOFloor + (Vector3.up * lfHalfHeight),
            new Vector3(lfHalfWidth, lfHalfHeight, lfHalfWidth), Quaternion.identity, Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        foreach (Collider lOCollider in lOHits)
        {
            if (lOCollider.GetComponentInParent<UWSpellProjectile>() != null)
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

    public bool SpawnWardRune(int piTileX, int piTileY)
    {
        return mOLoader != null && mOLoader.SpawnWardRune(piTileX, piTileY);
    }

    /// <summary>Places a boulder below the ceiling and lets it fall.</summary>
    public bool DropBoulder(int piObjectId, int piTileX, int piTileY)
    {
        if (mOLoader == null)
            return false;

        // On sub-position 3/3 of the tile, as QuakeSpell_seg038_3307_1211 moves it there
        // (tile * 8 + 3); ours used the tile's middle until 2026-09-27.
        Vector3 lOAt = new Vector3(
            UWUnits.SubTileToWorldAxis(piTileX, BoulderSubTile),
            BoulderSpawnZPos * UWObjectSpawner.HeightScale,
            UWUnits.SubTileToWorldAxis(piTileY, BoulderSubTile));

        GameObject lOBoulder = mOLoader.SpawnFlyingObject(piObjectId, lOAt);

        if (lOBoulder == null)
            return false;

        UWSpellProjectile lOFall = lOBoulder.AddComponent<UWSpellProjectile>();

        // Without Interaction: there is no hit message, because there is no damage.
        lOFall.Begin(Vector3.down, 0f, 0, BoulderFallRange, null, mOLoader, -1, UWDamageTypes.Physical);

        lOFall.MakeBallistic(UWSpellProjectile.Gravity);
        lOFall.DropOnImpact(piObjectId);

        return true;
    }

    public void ShakeScreen(int piDuration)
    {
        UWScreenShake lOShake = UWScreenShake.Ensure();

        if (lOShake != null)
            lOShake.Shake(UWScreenShake.LargeChannel, piDuration);
    }

    /// <summary>The Tremor rumbles while it shakes - see UWMiscSpellRules.TremorSound.</summary>
    public void PlaySoundAtPlayer(int piEffect)
    {
        UWSoundEffects.PlayAtAvatar(piEffect);
    }

    /// <summary>Armageddon: the inventory, the rune shelf and the whole level are cleared;
    /// without equipment, armour, light and movement abilities are no longer correct, so the
    /// status is refreshed. The rune bag in the player data is cleared by the rules.</summary>
    public void Armageddon()
    {
        if (mOLoader == null)
            return;

        UWInventory lOInventory = UWScene.Inventory;

        if (lOInventory != null)
            lOInventory.ClearAll();

        if (mOUi != null)
            mOUi.ClearRuneShelf();

        mOLoader.ApplyArmageddon();

        if (mOCharacter != null)
            mOCharacter.RefreshStatus();
    }

    public void StartHallucination()
    {
        if (mOCharacter != null)
            mOCharacter.StartHallucination();
    }

    public void FireBullfrog(int piMode)
    {
        UWTriggerSystem.FireBullfrog(piMode, mOLoader, mOInteraction);
    }

    public IReadOnlyList<CreatureSighting> Creatures
    {
        get
        {
            List<CreatureSighting> lOSightings = new List<CreatureSighting>();

            if (mOLoader == null)
                return lOSightings;

            UWCritter[] lOCritters = UWScene.FindCritters();

            for (int liAt = 0; lOCritters != null && liAt < lOCritters.Length; liAt++)
            {
                UWCritter lOCritter = lOCritters[liAt];

                if (lOCritter == null || lOCritter.IsDead || lOCritter.Record == null)
                    continue;

                UWEntityInfo lOEntity = lOCritter.GetComponent<UWEntityInfo>();
                int liObjectId = lOEntity != null && lOEntity.ObjectData != null ? lOEntity.ObjectData.ID : 0;

                lOSightings.Add(new CreatureSighting(
                    new UWTilePos(lOCritter.Record.TileX, lOCritter.Record.TileY), liObjectId));
            }

            return lOSightings;
        }
    }

    public bool FireLookTrigger(UWObject pOObject, int piSearchSkill)
    {
        return UWTriggerSystem.TryFireLookTrigger(pOObject, mOLoader, mOInteraction, piSearchSkill);
    }

    /// <summary>The centre of a tile at floor height - that is where the effect stands. Sprites have
    /// their pivot at the bottom edge, so they stand on the floor instead of in it.</summary>
    public void SpawnEffectOnTileFloor(int piTileX, int piTileY, int piEffectObjectId)
    {
        if (mOLoader == null || mOLoader.CurrentLevel == null || mOLoader.CurrentLevel.TileData == null
            || piTileX < 0 || piTileY < 0 || piTileX >= UWLevelMeshBuilder.TilesPerAxis
            || piTileY >= UWLevelMeshBuilder.TilesPerAxis)
            return;

        UWTile lOTile = mOLoader.CurrentLevel.TileData[(piTileY * UWLevelMeshBuilder.TilesPerAxis) + piTileX];

        mOLoader.SpawnEffectAt(piEffectObjectId, new Vector3(
            piTileX * UWLevelMeshBuilder.TileSpacing,
            lOTile != null ? lOTile.FloorHeight : 0f,
            piTileY * UWLevelMeshBuilder.TileSpacing), EffectSeconds);
    }

    // ------------------------------------------------- Targets in an area

    /// <summary>Collects everything damageable in and around the area, once per spell. Which
    /// tile an entry belongs to is decided by rounding the world position, just like on a
    /// projectile impact (see UWSpellProjectile.fApplyBlast).</summary>
    public int CollectAreaTargets(UWTilePos pOCentre, int piRadiusTiles)
    {
        mOTargets.Clear();
        mOSeen.Clear();

        if (mOLoader == null || mOLoader.CurrentLevel == null || mOLoader.CurrentLevel.TileData == null)
            return 0;

        UWTile lOCentreTile = null;

        if (pOCentre.X >= 0 && pOCentre.Y >= 0 && pOCentre.X < UWLevelMeshBuilder.TilesPerAxis
            && pOCentre.Y < UWLevelMeshBuilder.TilesPerAxis)
            lOCentreTile = mOLoader.CurrentLevel.TileData[(pOCentre.Y * UWLevelMeshBuilder.TilesPerAxis) + pOCentre.X];

        Vector3 lOCentre = new Vector3(
            pOCentre.X * UWLevelMeshBuilder.TileSpacing,
            lOCentreTile != null ? lOCentreTile.FloorHeight : 0f,
            pOCentre.Y * UWLevelMeshBuilder.TileSpacing);

        float lfRadius = (piRadiusTiles + 2) * UWLevelMeshBuilder.TileSpacing * 1.5f;

        Collider[] lOHits = Physics.OverlapSphere(lOCentre, lfRadius);

        if (lOHits == null)
            return 0;

        foreach (Collider lOCollider in lOHits)
        {
            UWDamageable lODamageable = lOCollider.GetComponentInParent<UWDamageable>();

            if (lODamageable == null || !mOSeen.Add(lODamageable))
                continue;

            Target lOTarget = new Target();

            lOTarget.Damageable = lODamageable;
            lOTarget.Critter = lODamageable.GetComponent<UWCritter>();
            lOTarget.Entity = lOCollider.GetComponentInParent<UWEntityInfo>();
            lOTarget.Centre = lOCollider.bounds.center;
            lOTarget.Tile = mOLoader.WorldPositionToTile(lODamageable.transform.position);

            mOTargets.Add(lOTarget);
        }

        return mOTargets.Count;
    }

    public UWTilePos AreaTargetTile(int piIndex)
    {
        return mOTargets[piIndex].Tile;
    }

    public bool AreaTargetIsCreature(int piIndex)
    {
        return mOTargets[piIndex].Critter != null && mOTargets[piIndex].Entity != null;
    }

    /// <summary>Squared horizontal distance from the caster - the height plays no role, and
    /// the square is enough for sorting.</summary>
    public float AreaTargetDistanceSquared(int piIndex)
    {
        Vector3 lOFrom = mOCharacter != null ? mOCharacter.transform.position : Vector3.zero;
        Vector3 lOAt = mOTargets[piIndex].Centre;

        float lfX = lOAt.x - lOFrom.x;
        float lfZ = lOAt.z - lOFrom.z;

        return (lfX * lfX) + (lfZ * lfZ);
    }

    /// <summary>
    /// A CREATURE CASTING (2026-09-27): set for a creature's area spell, which UW.EXE runs
    /// through the same code with the creature as caster. Its damage is then charged to that
    /// creature (DamageObjectsInTile passes the caster as the source), so the victim's damage
    /// reaction turns on the caster and no kin alarm is raised as if the player had struck;
    /// the gargoyle eyes stay for the player's own hits.
    /// </summary>
    public UWCritter AreaCaster { get; set; }

    /// <summary>
    /// UWTileBlast on the tile. The player's own spell now strikes him too when his tile is in
    /// the area (per user, 2026-09-27, and PlacePlayerInTile keeping him in the tile list), and
    /// the loose objects there are worn down. The player's hits are charged to him as the
    /// projectile's blast charges them (AnnounceAttacker with the player's index).
    /// </summary>
    public void DamageObjectsInTile(int piTileX, int piTileY, int piDiceCount, int piDiceRange, int piDamageType)
    {
        UWTileBlast.Strike(mOLoader, new UWTilePos(piTileX, piTileY), piDiceCount, piDiceRange, piDamageType,
            AreaCaster != null ? AreaCaster.Index : UWCritterBrain.PlayerIndex,
            AreaCaster == null ? mOInteraction : null);
    }

    public bool ConfuseAreaTarget(int piIndex)
    {
        return mOTargets[piIndex].Critter != null && mOTargets[piIndex].Critter.Confuse();
    }

    public bool CastTargetSpellOnAreaTarget(int piIndex, int piMinorClass)
    {
        return UWTargetSpell.Cast(piMinorClass, mOTargets[piIndex].Entity);
    }

    public void SpawnEffectAtAreaTarget(int piIndex, int piEffectObjectId)
    {
        if (mOLoader != null)
            mOLoader.SpawnEffectAt(piEffectObjectId, mOTargets[piIndex].Centre, EffectSeconds);
    }
}
