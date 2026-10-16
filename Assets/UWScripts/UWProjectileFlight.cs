using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// ONE FLYING OBJECT - a thrown thing, an arrow, a spell missile, a trap's bolt, the quake's
/// boulder (stage 1 of the motion rework, 2026-10-05, in place of UWSpellProjectile). Its motion
/// is the original's: a mobile record (UWMobileRecord) stepped by the engine-free core every
/// clock unit through UWProjectileWorld. This component shows it - the picture glides from the
/// last step's position to the new one over the step's interval, as a creature's does - and
/// carries what a hit does in the game: the damage of a missile, the strike on the player,
/// the smashing of Tybal's orb, the impact picture, the detonation's blast.
///
/// The player can still walk into his own missile from behind (per user on the original,
/// 2026-09-05: damage, and it flies on) - until stage 3 puts the player on the core, this
/// stays a touch test of its own (fUpdateBump).
/// </summary>
public class UWProjectileFlight : MonoBehaviour
{
    public UWMobileRecord Record { get; private set; }

    /// <summary>The missile's damage before the target's armour, and its type.</summary>
    public int Damage { get; private set; }

    public int DamageType { get; private set; }

    /// <summary>What a hit or the detonation shows, or -1 (UWRunicMagic.GetProjectileImpactId).</summary>
    public int ImpactId { get; private set; }

    public Interaction Interaction { get; private set; }

    /// <summary>The creature that launched it, or null for the player's and the traps'.</summary>
    public UWCritter Launcher { get; private set; }

    /// <summary>Launched by the player: his impact sound, his hits reported to the eyes, and
    /// the touch test for walking into it.</summary>
    public bool PlayersOwn { get; private set; }

    private UWProjectileWorld mOWorld;

    private Vector3 mOFrom;

    private Vector3 mOTo;

    private float mfSince;

    private float mfInterval;

    private int miShownHeading = -1;

    private bool mbIsModel;

    private float mfTravelled;

    private bool mbTouchingPlayer;

    private bool mbEnded;

    /// <summary>The first missile objects: major class 0, minor class 1 - the only ones that
    /// hurt what they hit (ObjectUse class 0 minor 1 -> seg029_29EE_AD).</summary>
    private const int FirstMissileObjectId = 0x10;

    private const int LastMissileObjectId = 0x1F;

    /// <summary>The touch test of the player's own missile: how far it must have flown first,
    /// and how close the player must come.</summary>
    private const float SelfHitDistance = 32f;

    private const float BumpRadius = 24f;

    private const float BumpKnockback = 120f;

    public void Begin(UWProjectileWorld pOWorld, UWMobileRecord pORecord, int piDamage, int piDamageType, int piImpactId,
        Interaction pOInteraction, UWCritter pOLauncher, bool pbPlayersOwn)
    {
        mOWorld = pOWorld;
        Record = pORecord;
        Damage = piDamage;
        DamageType = piDamageType;
        ImpactId = piImpactId;
        Interaction = pOInteraction;
        Launcher = pOLauncher;
        PlayersOwn = pbPlayersOwn;

        mOFrom = transform.position;
        mOTo = mOFrom;
        mfInterval = fStepSeconds(false);

        UWEntityInfo lOInfo = GetComponent<UWEntityInfo>();

        mbIsModel = lOInfo != null && lOInfo.IsModel;

        // A flying thing has no body of its own: the core tests its path itself.
        foreach (Collider lOCollider in GetComponentsInChildren<Collider>())
            lOCollider.enabled = false;

        fFace();
    }

    /// <summary>The record was stepped: the picture glides to the new position over the step's
    /// interval (the period in clock units of 62.5 ms, twice that under Speed).</summary>
    public void OnStepped(bool pbPlayerHasSpeed)
    {
        mOFrom = transform.position;
        mOTo = UWProjectileWorld.WorldPosition(Record);
        mfSince = 0f;
        mfInterval = fStepSeconds(pbPlayerHasSpeed);
        mfTravelled += Vector3.Distance(mOFrom, mOTo);

        fFace();
    }

    private float fStepSeconds(bool pbPlayerHasSpeed)
    {
        float lfSeconds = (Record.Period & 7) * UWCritterRules.SlotPitTicks / UWCritter.PitTicksPerSecond;

        return pbPlayerHasSpeed ? lfSeconds * 2f : lfSeconds;
    }

    /// <summary>An arrow's model points along its heading; a picture faces the camera by itself.</summary>
    private void fFace()
    {
        if (!mbIsModel || Record.HeadingByte == miShownHeading)
            return;

        miShownHeading = Record.HeadingByte;
        transform.rotation = Quaternion.LookRotation(UWViewpoint.HeadingByteToDirection(Record.HeadingByte));
    }

    private void Update()
    {
        if (mbEnded)
            return;

        mfSince += Time.deltaTime;

        float lfT = mfInterval > 0f ? Mathf.Clamp01(mfSince / mfInterval) : 1f;

        transform.position = Vector3.Lerp(mOFrom, mOTo, lfT);

        fUpdateBump();
    }

    /// <summary>The flight is over: the picture goes.</summary>
    public void End()
    {
        mbEnded = true;
        Destroy(gameObject);
    }

    private bool fIsMissile => Record.ItemId >= FirstMissileObjectId && Record.ItemId <= LastMissileObjectId;

    /// <summary>A fireball or lightning bolt detonates at rest: its picture belongs to that, not
    /// to the hit.</summary>
    private bool fDetonates
    {
        get
        {
            int liCount;
            int liRange;
            int liType;

            return UWObjectDamageRules.TryGetBlast(Record.ItemId, out liCount, out liRange, out liType);
        }
    }

    private int fAttackerIndex => Launcher != null ? Launcher.Index : UWCritterBrain.PlayerIndex;

    private int fHeight => mOWorld != null && mOWorld.Loader != null && mOWorld.Loader.UWDataImporter != null
        && mOWorld.Loader.UWDataImporter.CommonObjectProperties != null
        && mOWorld.Loader.UWDataImporter.CommonObjectProperties.TryGet(Record.ItemId, out UWCommonObjectProperties.Entry lOEntry)
        ? lOEntry.Height : 0;

    // ------------------------------------------------- The hits (MissileAttackHit_seg022_14BF)

    /// <summary>The missile strikes a creature: the target's armour of the part hit, the hit
    /// effect, the eyes for the player's own. The creature stays (false).</summary>
    public bool StrikeCreature(UWCritter pOCritter)
    {
        if (!fIsMissile || pOCritter == null)
            return false;

        UWDamageable lODamageable = pOCritter.GetComponent<UWDamageable>();

        if (lODamageable == null || lODamageable.IsDestroyed)
            return false;

        int liBottom = Record.ZPos;
        int liTop = Record.ZPos + fHeight;
        int liDamage = UWCritterCombat.SubtractArmour(Damage, pOCritter.GetArmourAgainst(liBottom, liTop, true));

        // TWO HIT SOUNDS (2026-10-12; ours played none): missile_thwack plays effect 4 on the
        // struck mobile, then do_damage plays it again louder by the damage before the armour
        // (UnderworldExhumed combat/COMBAT.C).
        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, pOCritter.BodyPosition);
        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, pOCritter.BodyPosition, Damage << 2);

        pOCritter.AnnounceAttacker(fAttackerIndex);
        lODamageable.ApplyDamage(liDamage, DamageType);

        // NO TRACE WHEN THE ARMOUR SWALLOWS IT: do_damage (COMBAT.C) returns before the blood or
        // the flash when the damage after the armour is 0, as the blows already do here. A
        // resistance does not count: it is asked later, in damage_item, so a ghost struck by acid
        // still flashes (per user's question, 2026-10-13). Ours showed it always until then.
        if (liDamage > 0)
            pOCritter.ShowHitEffect(liBottom, liTop, liDamage, true);

        if (PlayersOwn && Interaction != null && liDamage > 0)
            Interaction.ReportSpellHit(pOCritter.GetComponentInParent<UWEntityInfo>(), lODamageable, pOCritter.BodyPosition);

        if (!fDetonates)
            mOWorld.ShowImpact(this, transform.position);

        return false;
    }

    /// <summary>The missile strikes the player: less the armour of the part hit and halved on
    /// the easy difficulty, whoever fired it (see fDamagePlayer).</summary>
    public bool StrikePlayer()
    {
        if (!fIsMissile)
            return false;

        fDamagePlayer();
        mbTouchingPlayer = true;

        if (!fDetonates)
            mOWorld.ShowImpact(this, transform.position);

        return false;
    }

    private void fDamagePlayer()
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
        int liFeet = lOPlayer.GetFeetZPos();
        int liPart = UWArmourProtection.PickBodyPart(liFeet, liFeet + lOPlayer.GetBodyHeightZ(), Record.ZPos, Record.ZPos + fHeight);
        int liDamage = UWCritterCombat.SubtractArmour(Damage, lOPlayer.GetArmourAt(liPart));

        // The shake's level is taken before the easy halving (C5).
        int liShakeDamage = liDamage;

        liDamage = UWCritterCombat.HalveOnEasy(liDamage, lOPlayer.IsEasyDifficulty);

        // Effect 3 twice: missile_thwack's, then do_damage's louder by the damage before the
        // armour (COMBAT.C; ours played the first alone until 2026-10-12).
        UWSoundEffects.PlayAtAvatar(UWSoundEffects.HitPlayer);
        UWSoundEffects.PlayAtAvatar(UWSoundEffects.HitPlayer, Damage << 2);

        if (liDamage > 0)
            lOPlayer.ApplyDamage(liDamage, DamageType);

        // A CREATURE'S MISSILE starts the combat music like its blow: damage_critter (critter/
        // AI.C) finds the shooter in the missile's last_hit and picks MUSIC_DANGER or
        // MUSIC_COMBAT for the player, damage or not (2026-10-12; ours played nothing).
        if (Launcher != null)
            UWMusic.OnPlayerDamaged(lOPlayer.CurrentHP, lOPlayer.MaxHP);

        // A missile with an attacker shakes the view (COMBAT.C missile_thwack -> do_damage, C5,
        // 2026-10-12); one without (fromwho -1) does not.
        if (Launcher != null || PlayersOwn)
            lOPlayer.ShakeForCombatHit(liShakeDamage, liDamage);
    }

    /// <summary>The missile strikes a lying or standing object - a door, a chest: its damage
    /// less the object's armour, the flash and the sound every struck object gets. The orb
    /// rock smashes Tybal's orb; true then, the orb is gone.</summary>
    public bool StrikeObject(UWObject pOObject)
    {
        if (pOObject == null || mOWorld == null || mOWorld.Loader == null)
            return false;

        UWEntityInfo lOEntity;

        mOWorld.Loader.TryGetEntity(pOObject, out lOEntity);

        if (Record.ItemId == UWObjectMechanics.OrbRockObjectId)
        {
            if (pOObject.ID == UWObjectMechanics.TyballOrbObjectId && lOEntity != null && Interaction != null)
            {
                UWItemTargetUse.SmashOrb(Interaction, lOEntity);

                return true;
            }

            return false;
        }

        if (!fIsMissile)
            return false;

        Vector3 lOPoint = lOEntity != null ? lOEntity.transform.position : transform.position;
        UWDamageable lODamageable = lOEntity != null ? lOEntity.GetComponentInParent<UWDamageable>() : null;

        if (lODamageable != null)
            lODamageable.ApplyDamage(UWCritterCombat.SubtractArmour(Damage, lODamageable.Armour), DamageType);

        // A struck object always flashes and sounds (effect 4 at the object), armour or not.
        // THE FLASH AT THE MISSILE'S HEIGHT: missile_thwack sets hitz to the missile's z plus half
        // its height and do_damage puts the flash there - for a door no lower than its own z + 2
        // (COMBAT.C). Ours put it at the object's own position until 2026-10-13 (per user).
        int liHitZ = Record.ZPos + (fHeight / 2);

        if (lOEntity != null && lOEntity.IsDoor && pOObject.ZPos > liHitZ)
            liHitZ = pOObject.ZPos + 2;

        if (Interaction != null && lOEntity != null)
            Interaction.ReportSpellHit(lOEntity, null, new Vector3(lOPoint.x, UWUnits.ZPosToWorld(liHitZ), lOPoint.z));

        // Only do_damage's sound for a static object (missile_thwack plays its own for the player
        // and a mobile), louder by the damage before the armour (since 2026-10-12).
        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, lOPoint, Damage << 2);

        if (!fDetonates)
            mOWorld.ShowImpact(this, transform.position);

        return false;
    }

    /// <summary>DetonateProjectile at rest: the blast on the tile and the explosion's picture.</summary>
    public void Detonate(int piTileX, int piTileY, int piOwner)
    {
        if (mOWorld != null)
            mOWorld.ApplyBlast(this, piTileX, piTileY, piOwner);
    }

    // ------------------------------------------------- The player walking into his own missile

    /// <summary>
    /// Does the player walk into it from behind? Then damage, but the missile flies on. What
    /// counts is the TOUCH, not every frame: whoever stays in contact takes damage once,
    /// whoever breaks away and walks in again takes it again - exactly as the user describes
    /// it in the original. Only his own missile pushes him back; a creature's just hurts.
    /// </summary>
    private void fUpdateBump()
    {
        UWCharacter lOPlayer = UWScene.Character;

        if (!PlayersOwn || !fIsMissile || lOPlayer == null || mfTravelled < SelfHitDistance)
            return;

        bool lbTouching = (lOPlayer.transform.position - transform.position).sqrMagnitude <= BumpRadius * BumpRadius;

        if (lbTouching && !mbTouchingPlayer)
        {
            fDamagePlayer();
            fPushPlayerBack(lOPlayer);
        }

        mbTouchingPlayer = lbTouching;
    }

    private void fPushPlayerBack(UWCharacter pOPlayer)
    {
        UWPlayerMovement lOMovement = UWScene.PlayerMovement;

        if (lOMovement == null)
            return;

        Vector3 lOAway = pOPlayer.transform.position - transform.position;

        lOAway.y = 0f;

        if (lOAway.sqrMagnitude <= 0.0001f)
            lOAway = -UWViewpoint.HeadingByteToDirection(Record.HeadingByte);

        lOMovement.AddKnockback(lOAway.normalized * BumpKnockback);
    }
}
