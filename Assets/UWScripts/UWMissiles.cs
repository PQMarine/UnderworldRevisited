using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE MISSILES: what the flying objects do in the game (cleanup step 3.5, 2026-10-17, split out
/// of UWProjectileWorld). The launches - the player's shot, throw and spell, a creature's
/// missile, a trap's bolt, the quake's boulder - and a thing knocked loose; a damage trap's
/// strike on a thing in flight, with the debris flying on; the hits on a creature, the player
/// and an object (MissileAttackHit_seg022_14BF), the detonation's blast and the impact picture;
/// and the player walking into his own missile. The motion is UWMotionHost's: it keeps the
/// flights, steps their records on the world clock and calls back here when one strikes,
/// detonates or comes to rest. The numbers of a hit are UWMissileHitRules'; the one place a
/// missile's damage lands on its target is fApplyMissileDamage.
/// </summary>
public sealed class UWMissiles
{
    /// <summary>The boulder of the quake: on sub-position 3/3 of its tile, just below the
    /// ceiling (QuakeSpell_seg038_3307_1211).</summary>
    private const int BoulderSpawnZPos = 0x6E;

    private const int BoulderSubTile = 3;

    /// <summary>How long a missile's impact picture stays: mts_doanim starts the explosion and the
    /// lightning's splash with the length 4, which lasts five ticks (UWExplosionRules.
    /// LengthToSeconds) - 1.25 s; ours held 0.7 s until 2026-10-13.</summary>
    private static readonly float ImpactSeconds = UWExplosionRules.LengthToSeconds(UWExplosionRules.ExplosionLengthTicks);

    /// <summary>The touch test of the player's own missile: how far it must have flown first,
    /// and how close the player must come.</summary>
    private const float SelfHitDistance = 32f;

    private const float BumpRadius = 24f;

    private readonly UWMotionHost mOHost;

    public UWMissiles(UWMotionHost pOHost)
    {
        mOHost = pOHost;
    }

    /// <summary>The missiles of the level object's motion host, made when it is missing.</summary>
    public static UWMissiles Ensure(UWLevelLoader pOLoader)
    {
        UWMotionHost lOHost = UWMotionHost.Ensure(pOLoader);

        return lOHost != null ? lOHost.Missiles : null;
    }

    private UWLevelLoader fLoader => mOHost != null ? mOHost.Loader : null;

    // ------------------------------------------------- The launches

    /// <summary>The heading (1/256 of a turn, 0 north) and the pitch of a missile flying along
    /// a direction at this speed byte: the pitch is the vertical part in units of 64 against the
    /// horizontal speed byte * 0x2F (UWPlayerThrow.GetMissileDirection the other way round).
    /// The heading ROUNDS and is not UWHeadings.DegreesToHeadingByte on purpose: that one
    /// truncates the player's yaw as the original shifts its facing, while the original derives
    /// these headings from its arctangent table, not from the player (decision 23 of
    /// Docs/CLEANUP-PLAN.md, 2026-10-12).</summary>
    public static void AimFromDirection(Vector3 pODirection, int piSpeedByte, out int piHeading, out int piPitch)
    {
        float lfHorizontal = Mathf.Sqrt((pODirection.x * pODirection.x) + (pODirection.z * pODirection.z));

        piHeading = lfHorizontal > 0.0001f
            ? Mathf.RoundToInt(Mathf.Atan2(pODirection.x, pODirection.z) * Mathf.Rad2Deg * 256f / 360f) & 0xFF
            : 0;

        if (lfHorizontal > 0.0001f)
            piPitch = Mathf.RoundToInt(pODirection.y / lfHorizontal * piSpeedByte * 0x2F / 64f);
        else
            piPitch = pODirection.y > 0f ? 15 : -16;

        piPitch = Mathf.Clamp(piPitch, -16, 15);
    }

    /// <summary>
    /// The player's missile: a shot, a throw (pOItem is the thrown thing itself) or a spell
    /// projectile, at an absolute heading and a pitch (UWPlayerThrow.GetMissileAim), with the
    /// speed byte of the ammunition table (0x0F for a throw). Null when there is no room in
    /// front of the player. piHp is the item's quality for a thrown thing or an arrow, 0x3F
    /// for a spell.
    /// </summary>
    public UWProjectileFlight LaunchFromPlayer(UWObject pOItem, int piItemId, int piHeading, int piPitch, int piSpeedByte,
        int piBaseDamage, int piDamageType, int piImpactId, Interaction pOInteraction, int piHp)
    {
        Camera lOCamera = Camera.main;

        if (lOCamera == null || mOHost.ObjectMotion == null)
            return null;

        Vector3 lOFeet = UWPlayerThrow.GetFeet(lOCamera.transform);
        int liX8 = UWViewpoint.WorldToOriginalX(lOFeet.x) >> 5;
        int liY8 = UWViewpoint.WorldToOriginalY(lOFeet.z) >> 5;
        UWCommonObjectProperties.Entry lOPlayer = mOHost.Entry(UWObjectMechanics.AdventurerObjectId);
        UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

        UWMobileObjectMotion.Launcher lOLauncher = new UWMobileObjectMotion.Launcher
        {
            Index = 1,
            IsCreature = true,
            IsPlayer = true,
            TileX = liX8 >> 3,
            TileY = liY8 >> 3,
            XPos = liX8 & 7,
            YPos = liY8 & 7,
            ZPos = UWViewpoint.WorldToOriginalZ(lOFeet.y) >> 3,
            Octant = (piHeading >> 5) & 7,
            FineHeadingBits = piHeading & 0x1F,
            Height = lOPlayer.Height,
            Radius = lOPlayer.Radius,
            SwimCounter = lOTerrain != null ? lOTerrain.SwimCounter : 0
        };

        return fLaunch(pOItem, piItemId, lOLauncher, 0, piPitch, piSpeedByte, piBaseDamage, piDamageType, piImpactId,
            pOInteraction, null, piHp);
    }

    /// <summary>A creature's missile or spell projectile along its facing, at the pitch of its
    /// aim (GetPitchToGTarg). Null when the start has no room - the original makes no missile then.</summary>
    public UWProjectileFlight LaunchFromCreature(UWCritter pOCritter, int piItemId, int piPitch, int piBaseDamage,
        int piDamageType, int piImpactId)
    {
        if (pOCritter == null || pOCritter.Record == null || mOHost.ObjectMotion == null)
            return null;

        UWCritterRecord lORecord = pOCritter.Record;
        UWCommonObjectProperties.Entry lOOwn = mOHost.Entry(lORecord.ItemId);

        UWMobileObjectMotion.Launcher lOLauncher = new UWMobileObjectMotion.Launcher
        {
            Index = pOCritter.Index,
            IsCreature = true,
            TileX = lORecord.TileX,
            TileY = lORecord.TileY,
            XPos = lORecord.FineX,
            YPos = lORecord.FineY,
            ZPos = lORecord.ZPos,
            Octant = lORecord.FacingEighth & 7,
            FineHeadingBits = lORecord.FullFacing & 0x1F,
            Height = lOOwn.Height,
            Radius = lOOwn.Radius
        };

        return fLaunch(null, piItemId, lOLauncher, 0, piPitch, piSpeedByte: fSpeedByte(piItemId), piBaseDamage: piBaseDamage,
            piDamageType: piDamageType, piImpactId: piImpactId, pOInteraction: null, pOCritter: pOCritter, piHp: 0x3F);
    }

    /// <summary>A missile from a spot without a body of its own - the arrow trap, the spell
    /// trap: launched at the spot itself, no height offset, no placement test (a launcher of
    /// height 0).</summary>
    public UWProjectileFlight LaunchFromSpot(int piItemId, int piTileX, int piTileY, int piXPos, int piYPos, int piZPos,
        int piHeading, int piPitch, int piSpeedByte, int piBaseDamage, int piDamageType, int piImpactId, Interaction pOInteraction)
    {
        if (mOHost.ObjectMotion == null)
            return null;

        UWMobileObjectMotion.Launcher lOLauncher = new UWMobileObjectMotion.Launcher
        {
            Index = 0,
            TileX = piTileX,
            TileY = piTileY,
            XPos = piXPos,
            YPos = piYPos,
            ZPos = piZPos,
            Octant = (piHeading >> 5) & 7,
            FineHeadingBits = piHeading & 0x1F,
            Height = 0,
            Radius = 0
        };

        return fLaunch(null, piItemId, lOLauncher, 0, piPitch, piSpeedByte, piBaseDamage, piDamageType, piImpactId,
            pOInteraction, null, 0x3F);
    }

    /// <summary>The ammunition table's speed byte of a missile.</summary>
    private int fSpeedByte(int piItemId)
    {
        UWLevelLoader lOLoader = fLoader;

        return lOLoader != null && lOLoader.UWDataImporter != null && lOLoader.UWDataImporter.ObjectProperties != null
            ? lOLoader.UWDataImporter.ObjectProperties.GetRangedSpeed(piItemId)
            : 0;
    }

    /// <summary>A missile made for the flight (an arrow shot, a spell, a trap's bolt): a fresh
    /// object with its picture - the spawner bails out silently for one without (per user,
    /// 2026-10-05: shot arrows vanished where they fell) - and a stack of one when its kind
    /// counts (PrepareNewObjectProps_seg035_3E, as a landed projectile has it).</summary>
    private UWObject fNewObject(int piItemId)
    {
        UWLevelLoader lOLoader = fLoader;
        UWDataImport.DataImport lOData = lOLoader != null ? lOLoader.UWDataImporter : null;

        // CreateObj's fresh object (UWObjectFactory): the stack rule, quality 40 and the
        // picture. Until 2026-10-12 a missile made here had quality 0.
        UWObject lOObject = UWObjectFactory.Create(piItemId, lOData != null ? lOData.CommonObjectProperties : null);

        if (lOData != null)
            UWObjectAppearance.Apply(lOObject, lOData.Textures, null);

        return lOObject;
    }

    private UWProjectileFlight fLaunch(UWObject pOItem, int piItemId, UWMobileObjectMotion.Launcher pOLauncher, int piMissileHeading,
        int piPitch, int piSpeedByte, int piBaseDamage, int piDamageType, int piImpactId, Interaction pOInteraction,
        UWCritter pOCritter, int piHp)
    {
        UWMobileRecord lORecord = new UWMobileRecord
        {
            Object = pOItem ?? fNewObject(piItemId),
            Index = mOHost.NewFlightIndex()
        };

        if (!mOHost.ObjectMotion.TryLaunch(lORecord, pOLauncher, true, piMissileHeading, piPitch, piSpeedByte))
            return null;

        lORecord.Hp = piHp;

        // The thing lies in its launch direction when it comes to rest (per user, 2026-09-07:
        // arrows and bolts lie in the facing).
        lORecord.StaticHeading = lORecord.Octant;

        return fSpawnFlight(lORecord, piBaseDamage, piDamageType, piImpactId, pOInteraction, pOCritter, pOInteraction != null && pOCritter == null);
    }

    /// <summary>The quake's boulder: a piece of debris with a random small speed and schedule
    /// (QuakeSpell_seg038_3307_1211), falling from just below the ceiling.</summary>
    public UWProjectileFlight DropBoulder(int piItemId, int piTileX, int piTileY)
    {
        if (mOHost.ObjectMotion == null)
            return null;

        UWCritterClock lOClock = UWScene.CritterClock;

        UWMobileRecord lORecord = new UWMobileRecord
        {
            Object = fNewObject(piItemId),
            Index = mOHost.NewFlightIndex(),
            TileX = piTileX,
            TileY = piTileY,
            XPos = BoulderSubTile,
            YPos = BoulderSubTile,
            ZPos = BoulderSpawnZPos,
            FineX = (piTileX << 8) + (BoulderSubTile << 5) + 0xF,
            FineY = (piTileY << 8) + (BoulderSubTile << 5) + 0xF,
            FineZ = BoulderSpawnZPos << 3,
            HeadingByte = mOHost.Random() & 0xFF,
            SpeedByte = (mOHost.Random() & 3) + 2,
            GravityBit = !mOHost.Entry(piItemId).IsWeightless,
            VzField = 16,
            Period = (mOHost.Random() % 3) + 1,
            Phase = ((lOClock != null ? lOClock.Phase : 0) + (mOHost.Random() & 3)) & 0xF,
            Hp = 0x3F
        };

        lORecord.Octant = lORecord.HeadingByte >> 5;
        lORecord.StaticHeading = lORecord.Octant;

        return fSpawnFlight(lORecord, UWAttackResolution.NoMissileDamage, UWDamageTypes.Physical, -1, null, null, false);
    }

    /// <summary>A lying object knocked loose by a collision (UWMotionHost.PushObject): its record,
    /// already made mobile, flies as a thrown thing does - no damage, no launch sound.</summary>
    public UWProjectileFlight KnockLoose(UWMobileRecord pORecord)
    {
        return fSpawnFlight(pORecord, UWAttackResolution.NoMissileDamage, UWDamageTypes.Physical, -1, null, null, false, false);
    }

    private UWProjectileFlight fSpawnFlight(UWMobileRecord pORecord, int piBaseDamage, int piDamageType, int piImpactId,
        Interaction pOInteraction, UWCritter pOCritter, bool pbPlayersOwn, bool pbLaunchSound = true)
    {
        Vector3 lOAt = UWMotionHost.WorldPosition(pORecord);
        GameObject lOSpawned = fLoader.SpawnProjectile(pORecord.ItemId, lOAt, UWViewpoint.HeadingByteToDirection(pORecord.HeadingByte));

        if (lOSpawned == null)
            return null;

        UWProjectileFlight lOFlight = lOSpawned.AddComponent<UWProjectileFlight>();

        lOFlight.Begin(this, pORecord, piBaseDamage, piDamageType, piImpactId, pOInteraction, pOCritter, pbPlayersOwn);
        mOHost.AddFlight(lOFlight);

        // The launch sound of every missile (PrepareProjectileObject, effect 0x0A); a thing
        // knocked loose makes none.
        if (pbLaunchSound)
            UWSoundEffects.PlayAt(UWSoundEffects.Miss, lOAt);

        return lOFlight;
    }

    // ------------------------------------------------- A damage trap strikes a thing in flight

    /// <summary>
    /// A damage trap struck a thing in flight that set it off (DamageTrap_ovr107_CB2 ->
    /// DamageObject, type 4). In the original the damage comes off the flying record's byte 8
    /// inside the motion step, and ApplyProjectileMotion writes the hp it loaded before the step
    /// back over it at the end - so the wear is undone (per user on the original, 2026-10-06: a
    /// wand thrown ten times over the level 3 needles kept its quality 40). Only a hit that
    /// DESTROYS has an effect, as DamageObjectAndDoors then hands it to DamageObject_Debris: the
    /// thing becomes a pile of debris. So here: word 0 bit 13 protects, the resistances of COMOBJ
    /// byte 8 scale it, the quality class halves it (UWObjectDamageRules.Wear) - and only when
    /// that reaches 0 the thing becomes debris that flies on. A container is left alone
    /// (the original spills it, EmptyContainer; a thrown bag with its things is not destroyed
    /// here), and so is a thing that came to rest in the same frame. The type is the damage
    /// trap's 4, handed in by the blow since cleanup step 3.3 (2026-10-17: IUWWorld.Damage on
    /// UWDamageTarget.InFlight).
    /// </summary>
    public void StrikeThing(int piIndex, int piDamage, int piDamageType)
    {
        UWProjectileFlight lOFlight = mOHost.FlightAt(piIndex);

        if (lOFlight == null || lOFlight.Record == null || piDamage <= 0)
            return;

        UWMobileRecord lORecord = lOFlight.Record;

        if (UWObjectDamageRules.SpillsWhenDestroyed(lORecord.ItemId))
            return;

        UWCommonObjectProperties.Entry lOEntry = mOHost.Entry(lORecord.ItemId);
        int liScaled = UWDamageTypes.Scale(lOEntry.Resistances, piDamage, piDamageType);
        bool lbProtected = lORecord.Object != null && lORecord.Object.DoorDirection;
        int liUnused;

        if (!UWObjectDamageRules.Wear(lORecord.Hp, liScaled, lOEntry.QualityClass, lbProtected, out liUnused))
            return;

        UWObjectDamageRules.Remains lORemains = UWObjectDamageRules.DecideRemains(lORecord.ItemId, piDamageType);

        mOHost.EndFlight(lORecord);

        UWLevelLoader lOLoader = fLoader;

        if (lORemains == UWObjectDamageRules.Remains.Gone || lOLoader == null)
            return;

        // THE DEBRIS FLIES ON (per user on the original, 2026-10-06: "the debris flies on with the
        // same momentum"). DamageObject_Debris_seg023_D6 (READ the same evening), for a thing that
        // is no door, chest, barrel or container and not struck by fire: unless word 0 bit 15
        // (a quantity) is set, the chain behind its link - contents, enchantment - is cleared
        // (ClearObjectChain); then the id bits 0-8 become 0xD5 or 0xD6, its flags and every other
        // field stay; only a STATIC object goes through PlacedObjectCollison, a flying one moves
        // on as it was, and the routine answers "not gone". So the same object gets the new id,
        // the record keeps its place, heading, speed, vertical speed and hp (its quality at rest),
        // and the picture is made anew.
        int liDebris = UWObjectDamageRules.RollDebrisObjectId();
        UWObject lOObject = lORecord.Object;

        if (lOObject == null)
            lORecord.Object = fNewObject(liDebris);
        else
        {
            if (!lOObject.HasQuantity)
            {
                lOObject.Link = 0;

                if (lOObject.Contents != null)
                    lOObject.Contents.Clear();
            }

            lOObject.ID = (ushort)liDebris;

            if (lOLoader.UWDataImporter != null)
                UWObjectAppearance.Apply(lOObject, lOLoader.UWDataImporter.Textures, null);
        }

        fSpawnFlight(lORecord, lOFlight.BaseDamage, lOFlight.DamageType, lOFlight.ImpactId, lOFlight.Interaction,
            lOFlight.Launcher, lOFlight.PlayersOwn, false);
    }

    // ------------------------------------------------- The hits (MissileAttackHit_seg022_14BF)

    private int fAttackerIndex(UWProjectileFlight pOFlight)
    {
        return pOFlight.Launcher != null ? pOFlight.Launcher.Index : UWCritterBrain.PlayerIndex;
    }

    /// <summary>The missile's COMOBJ height, the top of its box over its z.</summary>
    private int fHeight(UWProjectileFlight pOFlight)
    {
        return mOHost.Entry(pOFlight.Record.ItemId).Height;
    }

    /// <summary>
    /// THE MISSILE'S DAMAGE ON ITS TARGET: the one place a missile hurts what it struck - a
    /// creature's or an object's UWDamageable, or the player (cleanup step 3.5, 2026-10-17). The
    /// armour is already off (UWAttackResolution.Missile; the player's and the easy halving are the
    /// damage pipeline's); the callers keep their order around it. Since step 3.3 (the same day) it
    /// is World.Damage with a missile's blow, which charges the launcher to a struck creature (with
    /// the damage since cleanup step 4.8, announced before it until then, as StrikeCreature did
    /// itself before step 3.3; fAttackerIndex: the player for a missile without a launching
    /// creature). The outcome's blow (UWAttackOutcome.Blow) since cleanup step 4.9b (2026-10-18).
    /// </summary>
    private void fApplyMissileDamage(UWDamageTarget pOTarget, in UWAttackOutcome pOOutcome)
    {
        UWWorld lOWorld = UWWorld.Of(fLoader);

        if (lOWorld != null)
            lOWorld.Damage(pOTarget, pOOutcome.Blow);
    }

    /// <summary>The missile's hit on a defender as a blow's (missile_thwack -> do_damage):
    /// UWAttackResolution.Missile with the flight's base, type and launcher - the dice ROLLED HERE,
    /// at the hit, after the part was picked, as do_damage rolls them (since cleanup step 4.9b,
    /// 2026-10-18, by the user's decision to follow the original; at the launch until then).</summary>
    private UWAttackOutcome fResolveHit(UWProjectileFlight pOFlight, IUWAttackDefender pIDefender, int piPart)
    {
        return UWAttackResolution.Missile(pOFlight.BaseDamage, pOFlight.DamageType, fAttackerIndex(pOFlight), pIDefender, piPart,
            pOFlight.Launcher != null, pOFlight.PlayersOwn);
    }

    /// <summary>The missile strikes a creature: the target's armour of the part hit, the hit
    /// effect, the eyes for the player's own. The creature stays (false).</summary>
    public bool StrikeCreature(UWProjectileFlight pOFlight, UWCritter pOCritter)
    {
        if (!UWMissileHitRules.HurtsWhatItHits(pOFlight.Record.ItemId) || pOCritter == null)
            return false;

        UWDamageable lODamageable = pOCritter.GetComponent<UWDamageable>();

        if (lODamageable == null || lODamageable.IsDestroyed)
            return false;

        int liBottom = pOFlight.Record.ZPos;
        int liTop = pOFlight.Record.ZPos + fHeight(pOFlight);

        // THE PART PICKED ONCE (missile_thwack, UnderworldExhumed combat/COMBAT.C:772): its armour
        // (the creature as the defender of UWAttackResolution.Missile) and the blood's height both
        // take it - since cleanup step 4.9b (2026-10-18, by the user's decision to follow the
        // original); until then the armour and the effect each picked their own.
        int liPart = pOCritter.PickMissilePart(liBottom, liTop);
        UWAttackOutcome lOOutcome = fResolveHit(pOFlight, pOCritter, liPart);

        // TWO HIT SOUNDS (2026-10-12; ours played none): missile_thwack plays effect 4 on the
        // struck mobile, then do_damage plays it again louder by the damage before the armour
        // (UnderworldExhumed combat/COMBAT.C).
        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, pOCritter.BodyPosition);
        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, pOCritter.BodyPosition, lOOutcome.HitVolume);

        fApplyMissileDamage(UWWorld.TargetOf(pOCritter, lODamageable), lOOutcome);

        // NO TRACE WHEN THE ARMOUR SWALLOWS IT: do_damage (COMBAT.C) returns before the blood or
        // the flash when the damage after the armour is 0, as the blows already do here. A
        // resistance does not count: it is asked later, in damage_item, so a ghost struck by acid
        // still flashes (per user's question, 2026-10-13). Ours showed it always until then.
        // (UWAttackOutcome.LeavesTrace, the rule of UWMissileHitRules.LeavesTrace.)
        if (lOOutcome.LeavesTrace)
            pOCritter.ShowMissileHitEffect(liPart, lOOutcome.Damage, (liBottom + liTop) / 2);

        if (pOFlight.PlayersOwn && pOFlight.Interaction != null && lOOutcome.LeavesTrace)
            pOFlight.Interaction.ReportSpellHit(pOCritter.GetComponentInParent<UWEntityInfo>(), lODamageable, pOCritter.BodyPosition);

        if (!UWMissileHitRules.Detonates(pOFlight.Record.ItemId))
            fShowImpact(pOFlight, pOFlight.transform.position);

        return false;
    }

    /// <summary>The missile strikes the player: less the armour of the part hit and halved on
    /// the easy difficulty, whoever fired it (see fDamagePlayer).</summary>
    public bool StrikePlayer(UWProjectileFlight pOFlight)
    {
        if (!UWMissileHitRules.HurtsWhatItHits(pOFlight.Record.ItemId))
            return false;

        fDamagePlayer(pOFlight);
        pOFlight.TouchingPlayer = true;

        if (!UWMissileHitRules.Detonates(pOFlight.Record.ItemId))
            fShowImpact(pOFlight, pOFlight.transform.position);

        return false;
    }

    private void fDamagePlayer(UWProjectileFlight pOFlight)
    {
        UWCharacter lOPlayer = UWScene.Character;

        if (lOPlayer == null)
            return;

        // EVERY MISSILE HIT ON THE PLAYER goes missile_newhit -> missile_thwack -> do_damage
        // (UnderworldExhumed motion/OBJPHYS.C, combat/COMBAT.C): the armour of the part hit
        // comes off (the player is Creature[63] there) and the easy difficulty halves what is
        // left, whoever fired it and whatever its type. Until 2026-10-12 ours halved only a
        // creature's arrow, bolt or stone - a mage's fireball or lightning bolt hit an easy
        // player in full - and the player's own rebounding missile skipped armour and halving.
        int liPart = UWMissileHitRules.PickPlayerBodyPart(lOPlayer.GetFeetZPos(), lOPlayer.GetBodyHeightZ(),
            pOFlight.Record.ZPos, fHeight(pOFlight));

        // THE HIT AS A BLOW'S (missile_thwack -> do_damage): UWAttackResolution.Missile with the
        // player as the defender (cleanup step 4.9, 2026-10-18) - no check; the dice of the flight's
        // base rolled here, after the part (since cleanup step 4.9b, the same day; at the launch until
        // then); the volume from the damage before the armour, the request below.
        UWAttackOutcome lOOutcome = fResolveHit(pOFlight, lOPlayer.Combatant, liPart);

        // Effect 3 twice: missile_thwack's, then do_damage's louder by the damage before the
        // armour (COMBAT.C; ours played the first alone until 2026-10-12).
        UWSoundEffects.PlayAtAvatar(UWSoundEffects.HitPlayer);
        UWSoundEffects.PlayAtAvatar(UWSoundEffects.HitPlayer, lOOutcome.HitVolume);

        // THE REST IS THE DAMAGE PIPELINE'S since cleanup step 4.2 (2026-10-17; the request is
        // UWDamageRequest.Missile): the armour of the part and the easy halving (nothing left,
        // nothing further); A CREATURE'S MISSILE starts the combat music like its blow -
        // damage_critter (critter/AI.C) finds the shooter in the missile's last_hit and picks
        // MUSIC_DANGER or MUSIC_COMBAT for the player, damage or not (2026-10-12; ours played
        // nothing); a missile with an attacker shakes the view (COMBAT.C missile_thwack ->
        // do_damage, C5, 2026-10-12), one without (fromwho -1) does not, the shake's level taken
        // before the easy halving. (The outcome's blow carries UWDamageRequest.Missile.)
        fApplyMissileDamage(UWDamageTarget.ThePlayer, lOOutcome);
    }

    /// <summary>The missile strikes a lying or standing object - a door, a chest: its damage
    /// whole, the flash and the sound every struck object gets. The orb rock smashes Tybal's orb;
    /// true then, the orb is gone. THE HIT AS A BLOW'S on a thing since cleanup step 4.9b
    /// (2026-10-18; UWAttackResolution.Missile, UWAttackResolution.ObjectDefender): no part, no
    /// armour - do_damage subtracts armour for creatures only, and the UWDamageable of a thing
    /// holds 0 (UWObjectSpawner.fAttachHealth), which ours subtracted until then.</summary>
    public bool StrikeObject(UWProjectileFlight pOFlight, UWObject pOObject)
    {
        UWLevelLoader lOLoader = fLoader;

        if (pOObject == null || lOLoader == null)
            return false;

        UWEntityInfo lOEntity;

        lOLoader.TryGetEntity(pOObject, out lOEntity);

        if (UWMissileHitRules.IsOrbRock(pOFlight.Record.ItemId))
        {
            if (UWMissileHitRules.SmashesOrb(pOFlight.Record.ItemId, pOObject.ID) && lOEntity != null && pOFlight.Interaction != null)
            {
                UWItemTargetUse.SmashOrb(pOFlight.Interaction, lOEntity);

                return true;
            }

            return false;
        }

        if (!UWMissileHitRules.HurtsWhatItHits(pOFlight.Record.ItemId))
            return false;

        Vector3 lOPoint = lOEntity != null ? lOEntity.transform.position : pOFlight.transform.position;
        UWDamageable lODamageable = lOEntity != null ? lOEntity.GetComponentInParent<UWDamageable>() : null;
        UWAttackOutcome lOOutcome = fResolveHit(pOFlight, UWAttackResolution.ObjectDefender, UWAttackResolution.NoPart);

        if (lODamageable != null)
            fApplyMissileDamage(UWWorld.TargetOf(lODamageable), lOOutcome);

        // A struck object always flashes and sounds (effect 4 at the object), armour or not.
        // THE FLASH AT THE MISSILE'S HEIGHT: missile_thwack sets hitz to the missile's z plus half
        // its height and do_damage puts the flash there - for a door no lower than its own z + 2
        // (COMBAT.C). Ours put it at the object's own position until 2026-10-13 (per user).
        int liHitZ = UWMissileHitRules.ObjectFlashZ(pOFlight.Record.ZPos, fHeight(pOFlight), lOEntity != null && lOEntity.IsDoor,
            pOObject.ZPos);

        if (pOFlight.Interaction != null && lOEntity != null)
            pOFlight.Interaction.ReportSpellHit(lOEntity, null, new Vector3(lOPoint.x, UWUnits.ZPosToWorld(liHitZ), lOPoint.z));

        // Only do_damage's sound for a static object (missile_thwack plays its own for the player
        // and a mobile), louder by the damage before the armour (since 2026-10-12).
        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, lOPoint, lOOutcome.HitVolume);

        if (!UWMissileHitRules.Detonates(pOFlight.Record.ItemId))
            fShowImpact(pOFlight, pOFlight.transform.position);

        return false;
    }

    // ------------------------------------------------- The blast and the impact

    /// <summary>DetonateProjectile at rest: the blast of a detonating missile on the tile, its
    /// picture and its sound.</summary>
    public void ApplyBlast(UWProjectileFlight pOFlight, int piTileX, int piTileY, int piOwner)
    {
        UWLevelLoader lOLoader = fLoader;

        if (lOLoader == null)
            return;

        int liCount;
        int liRange;
        int liType;

        if (UWObjectDamageRules.TryGetBlast(pOFlight.Record.ItemId, out liCount, out liRange, out liType))
        {
            int liAttacker = piOwner == 1 ? UWCritterBrain.PlayerIndex : piOwner > 1 ? piOwner : UWBlow.NoBlastAttacker;

            UWTileBlast.Strike(lOLoader, new UWTilePos(piTileX, piTileY), liCount, liRange, liType, liAttacker);
        }

        fShowImpact(pOFlight, UWMotionHost.WorldPosition(pOFlight.Record));
    }

    /// <summary>The impact picture of a missile that has one, and the player's own impact sound
    /// (SpawnImpactAnimo_seg022_2D2 plays effect 7 only for the player's).</summary>
    private void fShowImpact(UWProjectileFlight pOFlight, Vector3 pOAt)
    {
        UWLevelLoader lOLoader = fLoader;

        if (lOLoader == null || pOFlight.ImpactId < 0)
            return;

        lOLoader.SpawnEffectAt(pOFlight.ImpactId, pOAt + (Vector3.up * 2f), ImpactSeconds);

        // THE FIREBALL'S FLAMES: mts_doanim turns the fireball into the explosion and adds
        // fireball_effect's three to five flames around it, in the struck tile (per user on the
        // original, 2026-10-13: the impact is several animated parts; ours showed the one).
        if (pOFlight.ImpactId == UWExplosionRules.ExplosionObjectId && pOFlight.Record != null)
        {
            UWMobileRecord lORecord = pOFlight.Record;

            foreach (UWExplosionRules.Flame lOFlame in UWExplosionRules.ScatterFlames(pOFlight.ImpactId,
                lORecord.XPos & 7, lORecord.YPos & 7, lORecord.ZPos))
            {
                lOLoader.SpawnEffectAt(lOFlame.ObjectId, new Vector3(
                    UWUnits.SubTileToWorldAxis(lORecord.TileX, lOFlame.FineX),
                    UWUnits.ZPosToWorld(lOFlame.ZPos),
                    UWUnits.SubTileToWorldAxis(lORecord.TileY, lOFlame.FineY)),
                    UWExplosionRules.LengthToSeconds(lOFlame.LengthTicks), 0f, lOFlame.StartFrame);
            }
        }

        if (pOFlight.PlayersOwn)
            UWSoundEffects.PlayAt(UWSoundEffects.SpellImpact, pOAt);
    }

    // ------------------------------------------------- The player walking into his own missile

    /// <summary>
    /// The player can still walk into his own missile from behind (per user on the original,
    /// 2026-09-05: damage, and it flies on). The motion core does not do it for him: his body
    /// meeting a flight is an obstacle there (seg029_29EE_3 hands a flight's index to PushObject,
    /// which moves nothing) - so this stays a touch test of its own, run from the flight's
    /// Update after its picture moved (re-checked in cleanup step 3.5, 2026-10-17, and again in
    /// step 4.1, 2026-10-18, when the core became the only path: it still deals no damage).
    ///
    /// Does the player walk into it from behind? Then damage, but the missile flies on. What
    /// counts is the TOUCH, not every frame: whoever stays in contact takes damage once,
    /// whoever breaks away and walks in again takes it again - exactly as the user describes
    /// it in the original. THE BOUNCE (in the original the player bounces off a little but can
    /// catch up with the missile again, per user 2026-09-05) is the core's: the collision stops
    /// or deflects him. The push back this test gave the CharacterController path
    /// (UWPlayerMovement.AddKnockback) did nothing on the core and went in cleanup 4.1.
    /// </summary>
    public void UpdateSelfBump(UWProjectileFlight pOFlight)
    {
        UWCharacter lOPlayer = UWScene.Character;

        if (!pOFlight.PlayersOwn || !UWMissileHitRules.HurtsWhatItHits(pOFlight.Record.ItemId) || lOPlayer == null
            || pOFlight.Travelled < SelfHitDistance)
            return;

        bool lbTouching = (lOPlayer.transform.position - pOFlight.transform.position).sqrMagnitude <= BumpRadius * BumpRadius;

        if (lbTouching && !pOFlight.TouchingPlayer)
            fDamagePlayer(pOFlight);

        pOFlight.TouchingPlayer = lbTouching;
    }
}
