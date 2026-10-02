using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// A flying spell projectile - Magic Arrow, Electrical Bolt, Fireball (2026-09-04).
///
/// It flies straight in the direction the player faced when casting and strikes the
/// first obstacle. If it hits something vulnerable, it deals the damage from the
/// projectile table of OBJECTS.DAT.
///
/// The check is done ALONG THE PATH: from the last to the new position, not only at the end point.
/// Otherwise a fast projectile flies through a narrow enemy within a single frame.
///
/// The player can be hit by his own projectile in two ways, both checked by the user in the
/// original (2026-09-05):
///
/// - Hit from the FRONT after overtaking it: damage, and the projectile is
///   used up. That is the normal path, he stands as a target on the flight path.
/// - Walking into it from BEHIND: damage, but the projectile keeps flying ON. The player bounces
///   off a little and can repeat this several times.
///
/// The bounce is a short PUSH on the player, not a solid obstacle body - with one of those
/// you could get stuck in narrow corridors. It is deliberately weak so that
/// the player can catch up with the projectile and walk into it again several times.
///
/// It plays its landing sound where it comes down (UWSoundEffects.Landing).
/// </summary>
public class UWSpellProjectile : MonoBehaviour
{
    /// <summary>
    /// From the table value to the flight speed.
    ///
    /// The number comes from two observations by the user in the original (2026-09-05): a
    /// fireball is just below walking speed and can be overtaken, a lightning bolt is
    /// above it and cannot. It applies to EVERY projectile - spells, arrows from traps
    /// and whatever a creature throws.
    ///
    /// Without it a projectile crawls: the table value alone is about a tenth too
    /// small. Exactly that happened to the acid slug, whose projectile did not get the
    /// factor at first (per user, 2026-09-10).
    /// </summary>
    public const float SpeedFactor = 10.7f;

    /// <summary>
    /// How strongly a projectile falls that is not weightless.
    ///
    /// Tuned on the crossbow bolt of the arrow trap (per user, 2026-09-07): it falls visibly
    /// flat along its path and strikes a good twenty units above the floor.
    ///
    /// WHETHER IT FALLS AT ALL is decided by the object itself - see
    /// UWCommonObjectProperties.Entry.IsWeightless. Fireball, lightning bolt and magic missile
    /// fly straight, acid and everything thrown in an arc.
    /// </summary>
    public const float Gravity = 148f;

    private Vector3 mODirection;

    private float mfSpeed;

    private int miDamage;

    private float mfRemainingRange;

    private Interaction mOInteraction;

    private UWLevelLoader mOLevelLoader;

    private Collider mOOwnCollider;

    /// <summary>What the impact is replaced with, or -1 for nothing at all - see
    /// UWRunicMagic.GetProjectileImpactId. The Magic Missile strikes without an effect.
    /// </summary>
    private int miImpactObjectId = -1;

    /// <summary>The missile's own object id (UseObjectRadius), -1 when not given.</summary>
    private int miMissileObjectId = -1;

    /// <summary>Which damage type the projectile deals - see
    /// UWRunicMagic.GetProjectileDamageType. A fireball bounces off a fire elemental,
    /// a Magic Arrow hits everyone.</summary>
    private int miDamageType;

    /// <summary>How long the impact stays in the world.</summary>
    private const float ImpactSeconds = 0.7f;

    /// <summary>What remains lying as a pickable item on impact, or -1 for
    /// nothing at all - see DropOnImpact.</summary>
    private int miDropObjectId = -1;

    /// <summary>Gravity acceleration in world units per second squared, or zero for
    /// a straight path - see MakeBallistic.</summary>
    private float mfGravity;


    /// <summary>
    /// Turns the straight path into a BALLISTIC one: the projectile falls in flight.
    ///
    /// That is how the original does it. A physical projectile carries
    /// gravity_10_Z = -4 there (motion_init.InitMotionParams, set for everything that is not
    /// magical), and every movement round adds
    ///
    ///     unk_a_pitch += gravity_10_Z * speed_12
    ///
    /// onto the pitch (motion_calc, line 89). At Projectile_Speed 1,
    /// speed_12 = 16, which makes -64 per round - and 64 is exactly ONE pitch step. The
    /// new pitch is then written back into the object and clamped to 0..0x1F
    /// (motion.cs: "cx = 0x10 + unk_a_pitch / 0x40"). So a projectile tilts further down
    /// round by round in flight.
    ///
    /// A SPELL projectile does not fall - maybeMagicObjectFlag sets gravity to
    /// zero there. That is why this is here and not in Begin.
    ///
    /// ONLY THE PATH FALLS, NOT THE SPRITE. In the original the bolt stays horizontal
    /// while it sinks (per user, 2026-09-07) - the pitch lives in the
    /// movement calculation there, but a projectile is drawn solely by its
    /// heading. That is why nothing rotates along here; the caller sets the rotation
    /// once on creation, from the horizontal direction.
    /// </summary>
    public void MakeBallistic(float pfGravity)
    {
        mfGravity = pfGravity;
    }

    /// <summary>
    /// Leaves the projectile lying as an item on impact instead of vanishing.
    ///
    /// Original (user, 2026-09-07, arrow trap): on a hit the crossbow bolt hangs
    /// in the air very briefly, falls straight down and then lies on the floor -
    /// it can be picked up. The reference explains this: a projectile there is a completely
    /// normal object in the tile list, not a short-lived effect.
    ///
    /// A SPELL projectile does not do that - a Magic Arrow leaves nothing behind. That is why
    /// it is not in Begin but requested separately.
    /// </summary>
    public void DropOnImpact(int piObjectId)
    {
        miDropObjectId = piObjectId;
    }

    /// <summary>
    /// Like DropOnImpact, but lands THIS item instead of a fresh object with the same number -
    /// for a thrown item (see UWPlayerThrow). UW.EXE turns the thrown item itself into the
    /// projectile and copies quantity, contents, quality, owner and identification over
    /// (DropOrThrowByPlayer_seg025_355), so nothing of it may get lost on landing.
    ///
    /// If its range runs out, it is put down where it is. Only ammunition can break on landing,
    /// as in the original - see fBreaksOnLanding.
    /// </summary>
    public void DropItemOnImpact(UWDataImport.UWData.UWObject pOItem)
    {
        mODropItem = pOItem;
    }

    private UWDataImport.UWData.UWObject mODropItem;

    /// <summary>
    /// A THROWN ITEM THAT IS USED ON WHAT IT HITS. CollideObjects_seg029_29EE_173 hands the
    /// object hit and the thrown one to the object use with the "not from the hand" flag when
    /// the thrown object carries COMOBJ byte 6 bit 1 (UWCommonObjectProperties.UsedWhenThrown).
    /// The case that matters in UW1: the orb rock thrown at Tybal's orb breaks it - per user,
    /// 2026-09-23, "you do not use the rock, you throw it at the orb"; the rock stays where it
    /// falls. Only the orb rock does something here so far; the flag also sits on the
    /// ammunition and a few traps and triggers, whose use on a hit is not read.
    /// </summary>
    private void fUseOnObjectHit(Collider pOCollider)
    {
        if (mODropItem == null || mOInteraction == null || pOCollider == null
            || mODropItem.ID != UWDataImport.UWData.UWObjectMechanics.OrbRockObjectId
            || !fIsUsedWhenThrown(mODropItem.ID))
            return;

        UWEntityInfo lOHit = pOCollider.GetComponentInParent<UWEntityInfo>();

        if (lOHit != null && lOHit.ObjectData != null
            && lOHit.ObjectData.ID == UWDataImport.UWData.UWObjectMechanics.TyballOrbObjectId)
            mOInteraction.SmashOrb(lOHit);
    }

    private bool fIsUsedWhenThrown(int piObjectId)
    {
        UWDataImport.UWData.UWCommonObjectProperties.Entry lOEntry;

        return fTryGetCommon(piObjectId, out lOEntry) && lOEntry.UsedWhenThrown;
    }

    /// <summary>
    /// The projectile hits without doing damage - a thrown item that is not a missile.
    ///
    /// UW.EXE only lets a projectile hurt when it is a missile object, major class 0 minor
    /// class 1, i.e. objects 16 to 31 (reference: use.Use, "a missile projectile" ->
    /// combat.MissileImpact). Everything else bounces off a creature (BounceOtherObject).
    /// </summary>
    public void MakeHarmless()
    {
        mbHarmless = true;
    }

    private bool mbHarmless;

    /// <summary>
    /// The projectile moves like a thrown object of UW.EXE instead of ending at the first
    /// surface (per user, checked in the original, 2026-09-14):
    ///
    /// - FLOOR AND CEILING (DoCollision_seg031_2CFA_D1F, seg031_2CFA_EF0): the vertical speed
    ///   turns round and shrinks to Elasticity / 15, the horizontal loses (15 - Elasticity) / 30
    ///   of itself. Coming DOWN, a rebound below 0x8D ends the vertical motion
    ///   (seg031_2CFA_FA5); the item then SLIDES on with what is left, see fUpdateSliding. Going
    ///   up nothing is zeroed, so it falls back from the ceiling. In WATER it stops at once
    ///   (seg031_2CFA_DDE). If it lands on top of an OBJECT that is not a 3D model, it hops off
    ///   in a random direction instead (MaybeReflection_seg031_2CFA_CC6).
    /// - WALLS (HeadingRelated_seg031_2CFA_A49): the wall is a line in 45 degree steps. The
    ///   vertical speed becomes (speed / 16) * (Elasticity + 1). Of the two directions along the
    ///   wall the one closer to the flight counts; if the flight deviates from it by more than
    ///   0x3000 (67.5 degrees) - nearly head on - it is mirrored at the wall, otherwise it follows
    ///   the wall, turned towards the mirror image by Elasticity / 15. The speed along the ground
    ///   becomes Elasticity / 15 of itself. A door counts as a wall, after taking its damage.
    /// - CREATURES are pushed (BounceOtherObject_seg030_2BB7_8), then the item falls where it hit.
    ///
    /// Elasticity is COMOBJ.DAT, see UWCommonObjectProperties.Entry.Elasticity - a rock has 1 and
    /// drops almost dead, a crossbow bolt 4.
    /// </summary>
    public void EnableBouncing(int piElasticity, bool pbSlidesFurther, int piRadius)
    {
        miElasticity = Mathf.Clamp(piElasticity, 0, 0xF);
        mbSlidesFurther = pbSlidesFurther;

        // The object's own radius from COMOBJ.DAT, in eighth tiles - not the flat twelve of a
        // spell, which is only an estimate of its visible size.
        mfProjectileRadius = Mathf.Max(MinimumItemRadius, piRadius * UnderworldRevisited.Build.UWObjectSpawner.SubTileScale);
    }

    /// <summary>
    /// The collision radius of this object from COMOBJ.DAT, without the bouncing of
    /// EnableBouncing - for a spell projectile and a missile, called after Begin. Every
    /// missile object has radius 1 there, eight units; the twelve of the estimated picture size
    /// made a projectile brush the corridor wall beside its start and end there (per user,
    /// 2026-09-24: "often eaten by the right wall"). It also names the missile, which decides
    /// its explosion (fApplyBlast).
    /// </summary>
    public void UseObjectRadius(int piObjectId)
    {
        UWCommonObjectProperties.Entry lOEntry;

        miMissileObjectId = piObjectId;

        if (fTryGetCommon(piObjectId, out lOEntry))
            mfProjectileRadius = Mathf.Max(MinimumItemRadius, lOEntry.Radius * UnderworldRevisited.Build.UWObjectSpawner.SubTileScale);
    }

    /// <summary>Half the height of a flying missile's sweep: height 0 in COMOBJ.DAT, a point in
    /// height - a unit so the sweep still has a body. See fFindHit.</summary>
    private const float MissileHalfHeight = 1f;

    /// <summary>Radius for an item whose COMOBJ.DAT radius is zero - a sweep needs some size.</summary>
    private const float MinimumItemRadius = 4f;

    /// <summary>
    /// How far the collision sphere sits above the object's position.
    ///
    /// A thrown item is drawn with its pivot at the BOTTOM (UWObjectSpawner, billboard quad), and
    /// UW.EXE takes its zpos - the bottom - for floor contact. With the sphere centred on the
    /// position, every floor bounce happened a radius above the floor (per user, 2026-09-15).
    /// Spells keep the centred sphere.
    /// </summary>
    private float fLift => miElasticity >= 0 ? mfProjectileRadius : 0f;

    /// <summary>The lowest point of the projectile - the pivot for a thrown item.</summary>
    private float fBottom => transform.position.y + fLift - mfProjectileRadius;

    /// <summary>Elasticity for bouncing, or -1 if the projectile ends at the first surface.</summary>
    private int miElasticity = -1;

    private bool mbSlidesFurther;

    /// <summary>
    /// Motion ticks per second of UW.EXE. Not measured directly but derived from the gravity
    /// that was tuned on the arrow trap: gravity takes 64 from the vertical register every tick
    /// (gravity -4 times speed 16), and 64 there is 64 * SpeedFactor / 0x2F world units per
    /// second - so Gravity / that is the tick rate, about ten.
    /// </summary>
    private const float MotionTicksPerSecond = Gravity / (64f * SpeedFactor / 0x2F);

    /// <summary>World units per second of one unit of original momentum.</summary>
    private const float SpeedPerMomentum = SpeedFactor / 0x2F;

    /// <summary>
    /// The rebound below which a falling item stops bouncing - 0x8D in the vertical register,
    /// converted like every other speed.
    /// </summary>
    private const float RestVerticalSpeed = 0x8D * SpeedPerMomentum;

    /// <summary>The hop off an object: vertical 0x8C, momentum at least 0xEB, heading turned by
    /// up to 0x3000 either way in three cases of four (MaybeReflection_seg031_2CFA_CC6).</summary>
    private const int HopPitch = 0x8C;

    private const int HopMomentum = 0xEB;

    /// <summary>What a creature that is hit gets as momentum (BounceOtherObject_seg030_2BB7_8).</summary>
    private const int PushMomentum = 0xEB;

    /// <summary>Whether it currently slides along the ground - see fUpdateSliding.</summary>
    private bool mbSliding;

    /// <summary>Momentum while sliding, in the original's units.</summary>
    private float mfMomentum;

    private float mfTickTimer;

    /// <summary>
    /// Tries to bounce off the surface hit. Returns false if the flight ends there as before -
    /// the player or a creature.
    /// </summary>
    private bool fTryBounce(RaycastHit pOHit)
    {
        if (fIsPlayerBody(pOHit.collider))
            return false;

        fUseOnObjectHit(pOHit.collider);

        UWCritter lOCritter = pOHit.collider.GetComponentInParent<UWCritter>();

        if (lOCritter != null)
        {
            fPushCritter(lOCritter);

            return false;
        }

        // A door takes the hit of a missile, then the item bounces off it like off a wall.
        UWDamageable lODamageable = pOHit.collider.GetComponentInParent<UWDamageable>();

        if (lODamageable != null && !mbHarmless)
        {
            fAnnounceAttacker(lODamageable);
            lODamageable.ApplyDamage(Mathf.Max(0, miDamage - lODamageable.Armour), miDamageType);
        }

        transform.position += mODirection * pOHit.distance;

        if (pOHit.normal.y < -0.5f)
        {
            if (mODirection.y > 0f)
                fBounceVertically();

            return true;
        }

        if (pOHit.normal.y > 0.5f)
        {
            if (mODirection.y <= 0f)
                fLandOn(pOHit.collider);

            return true;
        }

        fBounceOffWall(pOHit.normal);

        return true;
    }

    /// <summary>
    /// A falling item touches something from above. Bounces up again, hops off an object, or
    /// comes to the end of its vertical motion and slides on - see EnableBouncing.
    /// </summary>
    private void fLandOn(Collider pOCollider)
    {
        // WATER DOES NOT BOUNCE. Per the user in the original (2026-09-21) an object that goes
        // under is gone at the FIRST contact with the water, with the splash; it does not hop
        // on the surface first, and it does not slide either. UW.EXE has no bounce there: the
        // landing sets the break chance to 8 of 8 and hands the object to the culling
        // (ObjectHitsFloorTileDestroyTalismans_seg029_C6F, labels C96 to CC8).
        // A TALISMAN IN LAVA THE SAME (per user on the original, 2026-10-02: talismans are gone
        // at once, everything else still slides). C6F, which burns a talisman on level 8 (tile
        // state 2, label CDD), runs only once the motion is zero (ApplyProjectileMotion_seg029_29EE_61A,
        // read the same day) - that a talisman is at rest on its first contact with lava is the
        // user's observation, the motion code's reason for it was not found. Other items slide
        // on and burn meanwhile, see fBurnOnLava.
        if (fIsOverWater() || (fIsOverLava() && mODropItem != null
            && UWDataImport.UWData.UWTalismans.IsTalisman(mODropItem.ID)))
        {
            fComeToRest();

            return;
        }

        if (fBounceVertically())
            return;

        UWEntityInfo lOInfo = pOCollider != null ? pOCollider.GetComponentInParent<UWEntityInfo>() : null;
        UWCommonObjectProperties.Entry lOEntry;

        // On top of an object that is not a 3D model (UnknownFlag3_1): it does not stay there.
        if (lOInfo != null && lOInfo.ObjectData != null && fTryGetCommon(lOInfo.ObjectData.ID, out lOEntry)
            && !lOEntry.Is3DModel)
        {
            fHop();

            return;
        }

        Vector3 lOHorizontal = new Vector3(mODirection.x, 0f, mODirection.z) * mfSpeed;

        fStartSliding(lOHorizontal);
    }

    /// <summary>Floor or ceiling - see EnableBouncing. Returns false if a falling item stops
    /// bouncing; its horizontal speed has then already lost its share.</summary>
    private bool fBounceVertically()
    {
        Vector3 lOVelocity = mODirection * mfSpeed;
        Vector3 lOHorizontal = new Vector3(lOVelocity.x, 0f, lOVelocity.z);

        bool lbFalling = lOVelocity.y <= 0f;
        float lfVertical = -lOVelocity.y * miElasticity / 15f;

        // UW.EXE plays effect 0xF on every such impact (seg031_2CFA_E28). Water never gets
        // here any more - it does not bounce, see fLandOn.
        UWSoundEffects.PlayAt(UWSoundEffects.Landing, transform.position);

        lOHorizontal *= 1f - ((0xF - miElasticity) / 30f);

        if (lbFalling && lfVertical < RestVerticalSpeed)
        {
            fSetVelocity(lOHorizontal);

            return false;
        }

        fSetVelocity(lOHorizontal + (Vector3.up * lfVertical));

        return true;
    }

    /// <summary>The hop off an object - see HopPitch.</summary>
    private void fHop()
    {
        Vector3 lOHorizontal = new Vector3(mODirection.x, 0f, mODirection.z) * mfSpeed;

        float lfSpeed = Mathf.Max(lOHorizontal.magnitude, HopMomentum * SpeedPerMomentum);
        float lfHeading = lOHorizontal.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(lOHorizontal.x, lOHorizontal.z) * Mathf.Rad2Deg
            : 0f;

        if ((Random.Range(0, 0x8000) & 3) != 0)
            lfHeading += Random.Range(0, 0x6000) * 360f / 65536f - 67.5f;

        Vector3 lONew = Quaternion.Euler(0f, lfHeading, 0f) * Vector3.forward * lfSpeed;

        fSetVelocity(lONew + (Vector3.up * (HopPitch * SpeedPerMomentum)));

        mbSliding = false;
    }

    /// <summary>A wall - see EnableBouncing.</summary>
    private void fBounceOffWall(Vector3 pONormal)
    {
        Vector3 lOVelocity = mODirection * mfSpeed;
        Vector3 lOHorizontal = new Vector3(lOVelocity.x, 0f, lOVelocity.z);

        float lfVertical = lOVelocity.y * (miElasticity + 1) / 16f;

        UWSoundEffects.PlayAt(UWSoundEffects.Landing, transform.position);

        Vector3 lONormal = new Vector3(pONormal.x, 0f, pONormal.z);

        if (lOHorizontal.sqrMagnitude < 0.0001f || lONormal.sqrMagnitude < 0.0001f)
        {
            fSetVelocity(Vector3.up * lfVertical);

            return;
        }

        // Headings as in the original: 0 north, clockwise.
        float lfHeading = Mathf.Atan2(lOHorizontal.x, lOHorizontal.z) * Mathf.Rad2Deg;
        float lfWall = (Mathf.Atan2(lONormal.x, lONormal.z) * Mathf.Rad2Deg) + 90f;

        lfWall = Mathf.Round(lfWall / 45f) * 45f;

        float lfDelta = Mathf.DeltaAngle(lfHeading, lfWall);

        if (Mathf.Abs(lfDelta) > 90f)
        {
            lfWall += 180f;
            lfDelta = Mathf.DeltaAngle(lfHeading, lfWall);
        }

        float lfNewHeading = Mathf.Abs(lfDelta) > 67.5f
            ? lfWall + lfDelta
            : lfWall + (lfDelta * miElasticity / 15f);

        float lfSpeed = lOHorizontal.magnitude * miElasticity / 15f;

        Vector3 lONewHorizontal = Quaternion.Euler(0f, lfNewHeading, 0f) * Vector3.forward * lfSpeed;

        fSetVelocity(lONewHorizontal + (Vector3.up * lfVertical));

        if (mbSliding)
            mfMomentum = lfSpeed / SpeedPerMomentum;
    }

    private void fSetVelocity(Vector3 pOVelocity)
    {
        mfSpeed = pOVelocity.magnitude;

        if (mfSpeed > 0f)
            mODirection = pOVelocity / mfSpeed;
    }

    /// <summary>
    /// Pushes a creature that was hit: it gets momentum 0xEB in the flight direction and slides
    /// it off like any object, with its own friction (BounceOtherObject_seg030_2BB7_8). The first
    /// tick moves with the full 0xEB, every later one rebuilds the momentum as in fTickFriction.
    /// Creatures without mass are not moved.
    /// </summary>
    private void fPushCritter(UWCritter pOCritter)
    {
        UWEntityInfo lOInfo = pOCritter.GetComponentInParent<UWEntityInfo>();
        UWCommonObjectProperties.Entry lOEntry;

        if (lOInfo == null || lOInfo.ObjectData == null || !fTryGetCommon(lOInfo.ObjectData.ID, out lOEntry)
            || lOEntry.MassTenthStones == 0)
            return;

        float lfMomentum = PushMomentum;
        float lfDistance = 0f;

        do
        {
            lfDistance += lfMomentum * SpeedPerMomentum / MotionTicksPerSecond;
        }
        while (fTickFriction(ref lfMomentum, lOEntry.SlidesFurther));

        pOCritter.Push(new Vector3(mODirection.x, 0f, mODirection.z), lfDistance);
    }

    /// <summary>
    /// One motion tick of friction on the ground (InitMotionParams_seg029_29EE_3CC): the stored
    /// momentum is momentum / 0x2F, cut to a whole number; at 2 or less (4 or less for
    /// SlidesFurther) the object stops, otherwise its momentum becomes that value times 0x29
    /// (0x2D). Returns false once it has stopped.
    /// </summary>
    private static bool fTickFriction(ref float pfMomentum, bool pbSlidesFurther)
    {
        int liStored = (int)(pfMomentum / 0x2F);

        if (liStored <= 2 + (pbSlidesFurther ? 2 : 0))
        {
            pfMomentum = 0f;

            return false;
        }

        pfMomentum = liStored * (0x29 + (pbSlidesFurther ? 4 : 0));

        return true;
    }

    private void fStartSliding(Vector3 pOHorizontal)
    {
        mfMomentum = pOHorizontal.magnitude / SpeedPerMomentum;
        mfTickTimer = 0f;
        mbSliding = true;

        fSetVelocity(pOHorizontal);
    }

    /// <summary>
    /// Sliding along the ground after the vertical motion has ended. Gravity is off in UW.EXE
    /// then; friction brakes every tick (fTickFriction) until the item comes to rest. Walls still
    /// deflect it. If the ground drops away under it, gravity returns (terrain flag 0x1000,
    /// DoTileCollisionMaybe_seg031_2CFA_179C) and it falls.
    /// </summary>
    private void fUpdateSliding()
    {
        mfTickTimer += fWorldDeltaTime;

        float lfTick = 1f / MotionTicksPerSecond;

        while (mfTickTimer >= lfTick)
        {
            mfTickTimer -= lfTick;

            if (!fTickFriction(ref mfMomentum, mbSlidesFurther) || fBurnOnLava())
            {
                fComeToRest();

                return;
            }
        }

        Vector3 lOFlat = new Vector3(mODirection.x, 0f, mODirection.z);

        if (lOFlat.sqrMagnitude < 0.0001f)
        {
            fComeToRest();

            return;
        }

        lOFlat.Normalize();

        fSetVelocity(lOFlat * (mfMomentum * SpeedPerMomentum));

        float lfStep = mfSpeed * fWorldDeltaTime;

        RaycastHit lOHit;

        if (lfStep > 0f && fFindHit(transform.position, lOFlat, lfStep, out lOHit, true))
        {
            transform.position += lOFlat * lOHit.distance;

            fBounceOffWall(lOHit.normal);

            return;
        }

        transform.position += lOFlat * lfStep;

        // Follow the ground; if there is none within reach any more, it falls.
        RaycastHit lOGround;

        if (fFindGround(transform.position + (Vector3.up * mfProjectileRadius),
                mfProjectileRadius + GroundReach, out lOGround))
        {
            transform.position = new Vector3(transform.position.x, lOGround.point.y,
                transform.position.z);

            return;
        }

        mbSliding = false;
    }

    /// <summary>How far below its resting height the ground may drop before a sliding item
    /// falls instead of following it - a step, not a slope.</summary>
    private const float GroundReach = 4f;

    /// <summary>The item has come to rest: it lands where it is.</summary>
    private void fComeToRest()
    {
        // Resting on an edge with the centre over the lower tile: fDropAtImpact puts it onto the
        // tile that carries it, as the original does (UWLevelLoader.TrySnapOntoCarryingTile).
        fDropAtImpact(transform.position + (mODirection * mfProjectileRadius), true);

        Destroy(gameObject);

        mbAtRest = true;
    }

    private bool mbAtRest;

    private bool fIsOverWater()
    {
        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null)
            return false;

        UWTilePos lOTile = mOLevelLoader.WorldPositionToTile(transform.position);
        UWTile lOAt = mOLevelLoader.CurrentLevel.TileData[(lOTile.Y * UnderworldRevisited.Build.UWLevelMeshBuilder.TilesPerAxis) + lOTile.X];

        return lOAt != null && mOLevelLoader.IsWaterTile(lOAt)
            && fBottom <= lOAt.FloorHeight + 1f;
    }

    /// <summary>
    /// SLIDING OVER LAVA BURNS (ApplyProjectileMotion_seg029_29EE_61A, label 7F4, read
    /// 2026-10-02; per user: "build the damage in"): on every motion tick in which the object is
    /// on lava (terrain bit 4) a roll of 1 in 5 deals it 1 point of plain fire
    /// (DamageObject_seg023_35A with 1 and type 8) - against its resistances and halved per
    /// quality class like any wear (UWObjectDamageRules.Wear), so only a fragile object loses a
    /// point. Ours ticks while sliding; the bounces before are left out. True when the object is
    /// destroyed - it then comes to rest at once, and the lava swallows it with its effect.
    /// What slides across the lava onto stone arrives worn.
    /// </summary>
    private bool fBurnOnLava()
    {
        if (mODropItem == null || !fIsOverLava() || UWRandom.Next(LavaBurnChance) != 0)
            return false;

        UWCommonObjectProperties.Entry lOEntry;

        if (!fTryGetCommon(mODropItem.ID, out lOEntry))
            return false;

        int liDamage = UWDamageTypes.Scale(lOEntry.Resistances, LavaBurnDamage, UWDamageTypes.PlainFire);

        if (!UWObjectDamageRules.Wear(mODropItem.Quality, liDamage, lOEntry.QualityClass, false, out int liQuality))
        {
            mODropItem.Quality = (ushort)liQuality;

            return false;
        }

        mODropItem.Quality = 0;

        return true;
    }

    /// <summary>The 1 in 5 of the lava burn (RNG % 5 == 0).</summary>
    private const int LavaBurnChance = 5;

    private const int LavaBurnDamage = 1;

    /// <summary>On the floor of a lava tile - see fLandOn.</summary>
    private bool fIsOverLava()
    {
        if (mOLevelLoader == null || mOLevelLoader.CurrentLevel == null)
            return false;

        UWTilePos lOTile = mOLevelLoader.WorldPositionToTile(transform.position);
        UWTile lOAt = mOLevelLoader.CurrentLevel.TileData[(lOTile.Y * UnderworldRevisited.Build.UWLevelMeshBuilder.TilesPerAxis) + lOTile.X];

        return lOAt != null && mOLevelLoader.IsLavaTile(lOAt)
            && fBottom <= lOAt.FloorHeight + 1f;
    }

    /// <summary>
    /// Checked by height as well as by the sweep: a sphere that STARTS inside a surface is
    /// ignored by Physics.SphereCast. A throw from a high floor started within reach of the
    /// ceiling and flew straight through it; the same can happen at the floor.
    /// </summary>
    private void fCheckFloorAndCeiling()
    {
        if (miElasticity < 0 || mbAtRest || mbSliding)
            return;

        if (mODirection.y > 0f
            && fBottom + (mfProjectileRadius * 2f) >= UnderworldRevisited.Build.UWLevelMeshBuilder.CeilingHeight)
        {
            fBounceVertically();

            return;
        }

        if (mODirection.y <= 0f && mOLevelLoader != null
            && fBottom < mOLevelLoader.GetFloorHeightAt(transform.position))
        {
            transform.position = new Vector3(transform.position.x,
                mOLevelLoader.GetFloorHeightAt(transform.position) + mfProjectileRadius - fLift, transform.position.z);

            fLandOn(null);
        }
    }

    private bool fTryGetCommon(int piObjectId, out UWCommonObjectProperties.Entry pOEntry)
    {
        pOEntry = default;

        return mOLevelLoader != null && mOLevelLoader.UWDataImporter != null
            && mOLevelLoader.UWDataImporter.CommonObjectProperties != null
            && mOLevelLoader.UWDataImporter.CommonObjectProperties.TryGet(piObjectId, out pOEntry);
    }

    /// <summary>
    /// The nearest surface along a sweep that UW.EXE would collide with.
    ///
    /// ScanForCollisions skips every static object with height 0 in COMOBJ.DAT - items lying on
    /// the floor, wall writings, levers. Here they only have a box for picking up and looking at,
    /// and a projectile flew into it. Trigger volumes do not count either, nor does the launcher.
    /// </summary>
    private bool fFindHit(Vector3 pODirection, float pfDistance, out RaycastHit pOHit)
    {
        return fFindHit(transform.position, pODirection, pfDistance, out pOHit);
    }

    private bool fFindHit(Vector3 pOFrom, Vector3 pODirection, float pfDistance, out RaycastHit pOHit,
        bool pbWallsOnly = false)
    {
        pOHit = default;

        // A DOOR'S BLOCKER COUNTS AS THE DOOR (UWEntityInfo.PartOf, resolved in fTryHit): it fills
        // the closed door's tile, where the original's door object has its collision. Without
        // that a missile ended on its face without a flash, and only from close by - starting
        // inside it - did it reach the leaf (per user, 2026-09-24).
        // A MISSILE IS FLAT (per user, 2026-09-30 with a screenshot: a Magic Missile flying
        // straight at a lurker in its water passed through its picture and only hit when it flew
        // above it). Every missile object has height 0 in COMOBJ.DAT, so the original collides it
        // as a point in height and its radius only across; our sphere reached a radius down as
        // well and ended on the water floor just before the lurker, whose head sits right above
        // it. A flying spell or missile now sweeps a flat box of its radius; a thrown item keeps
        // its sphere, whose bottom is its floor contact.
        RaycastHit[] lOHits = miElasticity < 0
            ? Physics.BoxCastAll(pOFrom, new Vector3(mfProjectileRadius, MissileHalfHeight, mfProjectileRadius),
                pODirection, Quaternion.identity, pfDistance, ~0, QueryTriggerInteraction.Ignore)
            : Physics.SphereCastAll(pOFrom + (Vector3.up * fLift), mfProjectileRadius, pODirection, pfDistance,
                ~0, QueryTriggerInteraction.Ignore);

        bool lbFound = false;

        foreach (RaycastHit lOHit in lOHits)
        {
            // SphereCastAll reports shapes it starts inside with distance zero and no point.
            if (lOHit.distance <= 0f && lOHit.point == Vector3.zero)
            {
                // A BODY THAT STEPPED INTO THE MISSILE IS HIT: in the original the walker's own
                // motion collides with the missile, which then uses itself on it
                // (CollideObjects: the other object's "uses itself" bit). Ours skipped every
                // shape the sweep started inside, so a creature walking into a fireball let it
                // fly through (per user, 2026-09-27: man-sized opponents "mostly hit, not always").
                if (pbWallsOnly || !fIsLivingBody(lOHit.collider) || fIgnoresCollider(lOHit.collider))
                    continue;

                RaycastHit lOInside = lOHit;

                lOInside.point = pOFrom;
                lOInside.normal = -pODirection;
                lOInside.distance = 0f;

                pOHit = lOInside;
                lbFound = true;

                continue;
            }

            if (lbFound && lOHit.distance >= pOHit.distance)
                continue;

            if (pbWallsOnly && Mathf.Abs(lOHit.normal.y) > 0.5f)
                continue;

            if (fIgnoresCollider(lOHit.collider))
                continue;

            pOHit = lOHit;
            lbFound = true;
        }

        return lbFound;
    }

    /// <summary>The ground under a sliding item - a plain ray, because a sphere resting on the
    /// floor already touches it and would not report it.</summary>
    private bool fFindGround(Vector3 pOFrom, float pfDistance, out RaycastHit pOHit)
    {
        pOHit = default;

        RaycastHit[] lOHits = Physics.RaycastAll(pOFrom, Vector3.down, pfDistance, ~0,
            QueryTriggerInteraction.Ignore);

        bool lbFound = false;

        foreach (RaycastHit lOHit in lOHits)
        {
            if (lbFound && lOHit.distance >= pOHit.distance)
                continue;

            if (lOHit.normal.y <= 0.5f || fIgnoresCollider(lOHit.collider)
                || lOHit.collider.GetComponentInParent<UWCritter>() != null
                || fIsPlayerBody(lOHit.collider))
                continue;

            pOHit = lOHit;
            lbFound = true;
        }

        return lbFound;
    }

    /// <summary>
    /// Is this the player's body? Recognised by the CharacterController, which only he has.
    /// UWCharacter sits on the CAMERA, a child of the body, so asking the struck collider for
    /// it in its parents never found him: every missile ended at his body as at a wall and
    /// did no damage (per user, 2026-09-27: the mages' fireballs never hurt in ours, while in
    /// the original they kill quickly). UWCritter recognises him the same way.
    /// </summary>
    private static bool fIsPlayerBody(Collider pOCollider)
    {
        return pOCollider is CharacterController || pOCollider.GetComponentInParent<UWCharacter>() != null;
    }

    /// <summary>The player, or a creature that is not dying.</summary>
    private static bool fIsLivingBody(Collider pOCollider)
    {
        if (fIsPlayerBody(pOCollider))
            return true;

        UWCritter lOCritter = pOCollider.GetComponentInParent<UWCritter>();
        UWDamageable lODamageable = lOCritter != null ? lOCritter.GetComponent<UWDamageable>() : null;

        return lODamageable != null && !lODamageable.IsDestroyed;
    }

    private bool fIgnoresCollider(Collider pOCollider)
    {
        if (mOLauncher != null && pOCollider.transform.IsChildOf(mOLauncher.transform))
            return true;
        if (pOCollider.GetComponentInParent<UWCritter>() != null || fIsPlayerBody(pOCollider))
            return false;

        UWEntityInfo lOInfo = pOCollider.GetComponentInParent<UWEntityInfo>();
        UWCommonObjectProperties.Entry lOEntry;

        return lOInfo != null && lOInfo.ObjectData != null && fTryGetCommon(lOInfo.ObjectData.ID, out lOEntry)
            && lOEntry.Height == 0;
    }

    /// <summary>
    /// Whether a landing projectile breaks (ObjectHitsFloorTile_seg030_2BB7_DDF): with a chance
    /// of ProjectileCulling / 8 it runs the culling test with range 10 plus 0 to 2, and it breaks
    /// when its culling priority plus half of (quantity - 1) does not exceed that range
    /// (ObjectCullingTest). A single arrow thus breaks three times in eight. Not in water - what
    /// is lost there is decided by UWLevelLoader (SinksInLiquid, from the user's tests).
    /// </summary>
    private bool fBreaksOnLanding(int piObjectId, int piQuantity)
    {
        UWCommonObjectProperties.Entry lOEntry;

        if (!fTryGetCommon(piObjectId, out lOEntry) || lOEntry.ProjectileCulling <= 0
            || lOEntry.ProjectileCulling > 8 || fIsOverWater())
            return false;

        if ((Random.Range(0, 0x8000) & 7) >= lOEntry.ProjectileCulling)
            return false;

        int liRange = 10 + Random.Range(0, 3);

        return lOEntry.CullingPriority + (Mathf.Max(0, piQuantity - 1) / 2) <= liRange;
    }

    /// <summary>
    /// Thickness of the projectile. The user estimates it in the original at a quarter to a
    /// half tile (2026-09-05); twelve units of radius are just under three eighths.
    ///
    /// Without it a hair-thin ray flew that narrowly missed slim targets.
    /// </summary>
    [SerializeField]
    private float mfProjectileRadius = 12f;

    /// <summary>
    /// How far the projectile must have flown before it can hit the player.
    ///
    /// It spawns right in front of the camera and would otherwise hit the player's own
    /// body in the same frame. Half a tile of lead is enough to get clear of oneself.
    /// </summary>
    private const float SelfHitDistance = 32f;

    private float mfTravelled;

    /// <summary>How close the player must be for walking into it to count.</summary>
    private const float BumpRadius = 24f;

    private UWCharacter mOPlayer;

    private UWPlayerMovement mOPlayerMovement;

    private bool mbTouchingPlayer;

    /// <summary>
    /// How strong the push is when walking into it, in units per second.
    ///
    /// With 400 damping it pushes about eighteen units, roughly a quarter
    /// tile. At 160 the projectile is slower than walking at 203, so the gap
    /// is closed again in just under half a second.
    /// </summary>
    [SerializeField]
    private float mfBumpKnockback = 120f;

    /// <summary>Starts the flight. The range limits how far the projectile gets before
    /// it vanishes by itself.</summary>
    public void Begin(Vector3 pODirection, float pfSpeed, int piDamage, float pfRange,
        Interaction pOInteraction, UWLevelLoader pOLevelLoader, int piImpactObjectId,
        int piDamageType)
    {
        mOLevelLoader = pOLevelLoader;
        miImpactObjectId = piImpactObjectId;
        miDamageType = piDamageType;

        mODirection = pODirection.sqrMagnitude > 0f ? pODirection.normalized : Vector3.forward;
        mfSpeed = pfSpeed;
        miDamage = piDamage;
        mfRemainingRange = pfRange;
        mOInteraction = pOInteraction;

        mOPlayer = UWScene.Character;
        mOPlayerMovement = UWScene.PlayerMovement;

        // The projectile's own body must not intercept its own ray.
        mOOwnCollider = GetComponentInChildren<Collider>();

        if (mOOwnCollider != null)
            mOOwnCollider.enabled = false;
    }

    /// <summary>
    /// The time a flying object advances by this frame: half of it while the player has Speed.
    /// In the original a missile is a mobile object like a creature and moves by the slot units
    /// the frame tick hands the world, which Speed halves (UWCritterDriver.PlayerHasSpeed,
    /// UWCritterClock); until 2026-09-24 ours flew at full rate while the creatures slowed.
    /// </summary>
    private static float fWorldDeltaTime => UWCritterDriver.PlayerHasSpeed ? Time.deltaTime * 0.5f : Time.deltaTime;

    private void Update()
    {
        if (mbAtRest)
            return;

        fKeepOutOfRock();

        fStep();

        if (!mbAtRest)
            fKeepOutOfRock();
    }

    /// <summary>
    /// The safety net against slipping into the rock - see UWLevelLoader.IsInsideRock.
    ///
    /// The sweep ignores surfaces the sphere already touches, and after a bounce it touches the
    /// wall. That was enough for a thrown item to pass a diagonal wall and fall out of the level
    /// (per user, 2026-09-15). So its centre is checked against the tile data after every step:
    /// in rock, it goes back to the last free position and bounces off the face it came through.
    /// </summary>
    private void fKeepOutOfRock()
    {
        if (miElasticity < 0 || mOLevelLoader == null)
            return;

        Vector3 lONormal;

        if (!mOLevelLoader.IsInsideRock(transform.position, mbHasLastFree ? mOLastFree : transform.position,
                out lONormal))
        {
            mOLastFree = transform.position;
            mbHasLastFree = true;

            return;
        }

        if (!mbHasLastFree)
            return;

        transform.position = mOLastFree;

        if (lONormal.sqrMagnitude > 0f && Vector3.Dot(mODirection, lONormal) < 0f)
            fBounceOffWall(lONormal);
    }

    private Vector3 mOLastFree;

    private bool mbHasLastFree;

    private void fStep()
    {
        if (mbSliding)
        {
            fUpdateSliding();

            return;
        }

        if (mfGravity > 0f)
            fApplyGravity();

        fCheckFloorAndCeiling();

        if (mbAtRest)
            return;

        float lfStep = mfSpeed * fWorldDeltaTime;

        if (lfStep <= 0f)
            return;

        if (fTryHit(lfStep))
            return;

        transform.position += mODirection * lfStep;

        mfTravelled += lfStep;
        mfRemainingRange -= lfStep;

        fUpdateBump();

        if (mfRemainingRange <= 0f)
        {
            if (mODropItem != null)
                fDropAtImpact(transform.position + (mODirection * mfProjectileRadius));

            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Does the player walk into it from behind? Then damage, but the projectile keeps flying.
    ///
    /// What counts is the TOUCH, not every frame: whoever stays in contact takes damage once,
    /// whoever breaks away and walks in again takes it again. That is exactly how the user describes it -
    /// you bounce off and can repeat it.
    /// </summary>
    private void fUpdateBump()
    {
        if (mOPlayer == null || mfTravelled < SelfHitDistance || mbHarmless)
            return;

        // ONLY THE PLAYER'S OWN PROJECTILE PUSHES. Whoever is HIT by a fireball takes
        // damage and sees the screen shake - nothing more (per user, 2026-09-10). The
        // reference confirms this: on the path every hit on the player takes, the only
        // extra is SetScreenShake, no push and no movement
        // (combat.AttackerAppliesFinalDamage, branch "player was the defender").
        //
        // The bounce remains for the case in which it was observed: you walk into
        // your own, slower projectile from behind.
        if (mOLauncher != null)
            return;

        bool lbTouching = (mOPlayer.transform.position - transform.position).sqrMagnitude
            <= BumpRadius * BumpRadius;

        if (lbTouching && !mbTouchingPlayer)
        {
            fDamagePlayer();

            fPushPlayerBack();
        }

        mbTouchingPlayer = lbTouching;
    }

    /// <summary>
    /// The hit on the player - minus his armour.
    ///
    /// A PROJECTILE HIT GOES THROUGH THE SAME CALCULATION AS A SWORD BLOW. The reference
    /// sends both through AttackerAppliesFinalDamage, and there at the end the
    /// toughness of the body part hit is subtracted - for the player character that is its
    /// armour value (see UWArmourProtection). Dice rolling and charge were already applied
    /// at launch, because the thrower's charge is known there.
    ///
    /// ONLY FOR A PROJECTILE FROM ANOTHER HAND: what the player threw himself and
    /// then catches up with hits him unreduced - there is no armour place for that in the
    /// reference, and it would also be hard to explain.
    /// </summary>
    private void fDamagePlayer()
    {
        int liDamage = miDamage;

        if (mOLauncher != null)
        {
            // Heights on the grid: the missile from its bottom to its top, the player from the
            // feet to feet plus height - as the original hands them to PickBodyHitPoint.
            int liBottom = UWUnits.RoundToInt(fBottom / UWWorldScale.ZPosStep);
            int liTop = UWUnits.RoundToInt((fBottom + (2f * mfProjectileRadius)) / UWWorldScale.ZPosStep);
            int liFeet = mOPlayer.GetFeetZPos();

            int liPart = UWArmourProtection.PickBodyPart(liFeet, liFeet + mOPlayer.GetBodyHeightZ(),
                liBottom, liTop);

            liDamage = UWCritterCombat.SubtractArmour(liDamage, mOPlayer.GetArmourAt(liPart));

            // A creature's MISSILE does half damage on the easy difficulty, after the armour
            // (MissileAttackHit into AttackerAppliesFinalDamage, label A28; deviation 47).
            // Its spells take another route in the original and are not halved.
            if (miDamageType == UWDamageTypes.Missile)
                liDamage = UWCritterCombat.HalveOnEasy(liDamage, mOPlayer.IsEasyDifficulty);
        }

        if (liDamage > 0)
            mOPlayer.ApplyDamage(liDamage, miDamageType);
    }

    /// <summary>Pushes the player away from the projectile, horizontally.</summary>
    private void fPushPlayerBack()
    {
        if (mOPlayerMovement == null)
            return;

        Vector3 lOAway = mOPlayer.transform.position - transform.position;

        lOAway.y = 0f;

        // If the player stands exactly inside the projectile, the direction is useless - then push against the flight.
        if (lOAway.sqrMagnitude <= 0.0001f)
            lOAway = -mODirection;

        lOAway.y = 0f;

        mOPlayerMovement.AddKnockback(lOAway.normalized * mfBumpKnockback);
    }

    /// <summary>
    /// The explosion strikes the tile the missile stopped on - every creature there (the one it
    /// has just hit included), the player if he stands there, and the loose objects - each with
    /// a roll of its own: 10d6 fire for the fireball, 6d5 magic for the lightning bolt
    /// (DetonateProjectile_seg044_95A, see UWTileBlast and UWObjectDamageRules).
    ///
    /// Exactly the tile, no radius: the user measured it in the original (2026-09-05) - a
    /// goblin on the same tile is hit even though the ray went into the wall, one next to it is
    /// not. UNTIL 2026-09-27 ours spared the creature hit, dealt the missile's own damage again
    /// instead of the blast dice, and left the player and the loose objects out.
    ///
    /// The tile is taken a little in front of the surface, where the missile came to rest: the
    /// point on a wall itself can round into the wall's tile.
    /// </summary>
    private void fApplyBlast(Vector3 pOPoint, Vector3 pONormal)
    {
        if (mOLevelLoader == null
            || !UWObjectDamageRules.TryGetBlast(miMissileObjectId, out int liCount, out int liRange, out int liType))
            return;

        UWCritter lOShooter = mOLauncher != null ? mOLauncher.GetComponentInParent<UWCritter>() : null;

        UWTileBlast.Strike(mOLevelLoader, mOLevelLoader.WorldPositionToTile(pOPoint + (pONormal * 2f)),
            liCount, liRange, liType, lOShooter != null ? lOShooter.Index : UWCritterBrain.PlayerIndex,
            lOShooter == null ? mOInteraction : null);
    }

    /// <summary>Pulls the path downward and bends the flight direction along with it; the
    /// sprite itself does not rotate - see MakeBallistic.</summary>
    private void fApplyGravity()
    {
        Vector3 lOVelocity = (mODirection * mfSpeed) + (Vector3.down * mfGravity * fWorldDeltaTime);

        mfSpeed = lOVelocity.magnitude;

        if (mfSpeed <= 0f)
            return;

        mODirection = lOVelocity / mfSpeed;
    }

    /// <summary>
    /// Who fired it.
    ///
    /// THE LAUNCHER'S OWN BODY DOES NOT INTERCEPT THE PROJECTILE. For the player there has long
    /// been the initial distance (SelfHitDistance), for a creature there was not - and its
    /// projectile starts right inside it. It showed with the fire elemental: it had
    /// thrown forty-six fireballs, none of which could be seen (per user,
    /// 2026-09-10). Each one had struck its own belly immediately - harmlessly, because a
    /// fire elemental is immune to fire.
    /// </summary>
    public void SetLauncher(GameObject pOLauncher)
    {
        mOLauncher = pOLauncher;
    }

    private GameObject mOLauncher;

    /// <summary>
    /// Tells the target who shot, before the hit - the original carries the launcher in the
    /// projectile's own byte 0x12 and DamageNPC takes it from there (see
    /// UWCritter.AnnounceAttacker). Without a launcher it is the player, which is what a
    /// thrown item or a cast spell of his own is.
    /// </summary>
    private void fAnnounceAttacker(UWDamageable pODamageable)
    {
        UWCritter lOTarget = pODamageable != null
            ? pODamageable.GetComponentInParent<UWCritter>() : null;

        if (lOTarget == null)
            return;

        UWCritter lOShooter = mOLauncher != null
            ? mOLauncher.GetComponentInParent<UWCritter>() : null;

        lOTarget.AnnounceAttacker(lOShooter != null ? lOShooter.Index : UWCritterBrain.PlayerIndex);
    }

    /// <summary>Searches the distance of the next step for a hit.</summary>
    private bool fTryHit(float pfStep)
    {
        RaycastHit lOHit;

        // Skips what UW.EXE does not collide with - see fFindHit.
        if (!fFindHit(mODirection, pfStep, out lOHit))
            return false;

        // Floor, ceiling and walls: bounce instead of ending, if this projectile does that -
        // see EnableBouncing.
        if (miElasticity >= 0 && fTryBounce(lOHit))
            return true;

        // The player stands on the flight path like any other target: it hits him, deals
        // damage and is used up (confirmed by the user in the original, 2026-09-05).
        if (fIsPlayerBody(lOHit.collider))
        {
            // Too close to the start it is the player's own body at launch, not a hit.
            if (mfTravelled < SelfHitDistance)
                return false;

            if (!mbHarmless && mOPlayer != null)
                fDamagePlayer();
        }

        // A DOOR FRAME IS PART OF ITS DOOR (UWEntityInfo.PartOf): the hit goes to the door.
        UWEntityInfo lOHitEntity = lOHit.collider.GetComponentInParent<UWEntityInfo>();
        Vector3 lOHitPoint = lOHit.point;

        if (lOHitEntity != null && lOHitEntity.PartOf != null)
        {
            lOHitEntity = lOHitEntity.PartOf;

            Collider lOWhole = lOHitEntity.GetComponentInChildren<Collider>();

            // Its bounds, not ClosestPoint: the leaf is a concave mesh collider, for which
            // ClosestPoint returns the point unchanged and the flash stayed at the impact.
            if (lOWhole != null)
                lOHitPoint = lOWhole.bounds.ClosestPoint(lOHit.point);
        }

        UWDamageable lODamageable = mbHarmless ? null
            : lOHitEntity != null && lOHitEntity != lOHit.collider.GetComponentInParent<UWEntityInfo>()
                ? lOHitEntity.GetComponentInParent<UWDamageable>()
                : lOHit.collider.GetComponentInParent<UWDamageable>();

        bool lbHitsCreature = lOHit.collider.GetComponentInParent<UWCritter>() != null;

        if (lODamageable != null)
        {
            // THE TARGET'S TOUGHNESS IS SUBTRACTED FOR A PROJECTILE TOO. The reference
            // applies it at the end of the same chain a sword blow goes through
            // (AttackerAppliesFinalDamage, branch "npc has been hit, apply defenses").
            //
            // A HIT THE ARMOUR SWALLOWS WHOLE STILL COUNTS AS A HIT. The reference calls
            // DamageObject with zero then, and the creature turns hostile all the same; only
            // blood and the eyes need real damage (finaldamage != 0). A thrown rock showed it:
            // blood, but the creature stayed calm (per user, 2026-09-14).
            //
            // A CREATURE'S ARMOUR IS THAT OF THE PART HIT: the missile's bottom and top
            // against the body, part + 4, row[part % 4] with the 0xFF fallback and a strong
            // defender's 5/3 (MissileAttackHit label 158B, AttackerAppliesFinalDamage 990;
            // deviation 49). Doors and containers keep the UWDamageable's one value.
            UWCritter lOCritterHit = lOHit.collider.GetComponentInParent<UWCritter>();
            int liArmour = lODamageable.Armour;

            if (lOCritterHit != null)
            {
                int liBottom = UWUnits.RoundToInt(fBottom / UWWorldScale.ZPosStep);
                int liTop = UWUnits.RoundToInt((fBottom + (2f * mfProjectileRadius)) / UWWorldScale.ZPosStep);

                liArmour = lOCritterHit.GetArmourAgainst(liBottom, liTop, true);
            }

            int liDamage = UWCritterCombat.SubtractArmour(miDamage, liArmour);

            fAnnounceAttacker(lODamageable);
            lODamageable.ApplyDamage(liDamage, miDamageType);

            // The blood or the flash of the hit, as the original's missile path does through
            // AttackerAppliesFinalDamage - see UWCritter.ShowHitEffect.
            if (lOCritterHit != null)
                lOCritterHit.ShowHitEffect(UWUnits.RoundToInt(fBottom / UWWorldScale.ZPosStep),
                    UWUnits.RoundToInt((fBottom + (2f * mfProjectileRadius)) / UWWorldScale.ZPosStep),
                    liDamage, true);

            if (mOInteraction != null && liDamage > 0 && lbHitsCreature)
                mOInteraction.ReportSpellHit(lOHit.collider.GetComponentInParent<UWEntityInfo>(),
                    lODamageable, lOHit.point);
        }

        // AN OBJECT STRUCK ALWAYS FLASHES AND SOUNDS - a door, its frame, a chest: the missile's
        // hit (MissileAttackHit_seg022_14BF) plays effect 4 at the object and shows the hit,
        // armour or not; ours showed it only when damage got through, so at a door only now and
        // then (per user on the original, 2026-09-24). No condition eyes for an object, as in the original.
        if (!lbHitsCreature && lOHitEntity != null && lOHitEntity.ObjectData != null)
        {
            if (mOInteraction != null)
                mOInteraction.ReportSpellHit(lOHitEntity, null, lOHitPoint);

            UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, lOHitPoint);
        }

        // THE MISSILE ENDS AT THE OBJECT'S COLLISION, a door's too (its blocker, the original's
        // circle of radius 3). In the original the struck object counts as an obstacle
        // (seg029_29EE_3 returns 4, "object hit"), and the impact force damages the missile
        // itself (ApplyProjectileMotion_seg029_29EE_61A, above 0x100: sound 0x0F, DamageObject),
        // which destroys a spell missile. That it was seen flying on a little (per user on the
        // original, 2026-09-24) is the rest of its coarse motion step, too short to matter here.
        fEndAt(lOHit.point, lOHit.normal);

        return true;
    }

    /// <summary>The end of the flight at a surface: the blast of a detonating projectile, its
    /// impact picture and sound, what it leaves behind.</summary>
    private void fEndAt(Vector3 pOPoint, Vector3 pONormal)
    {
        // A detonating projectile hits everything on its tile, not only what the
        // ray caught - the creature it caught included.
        if (miImpactObjectId >= 0)
            fApplyBlast(pOPoint, pONormal);

        transform.position = pOPoint;

        // INTO THE WATER IT SPLASHES (per user, 2026-09-30: a Magic Missile shot into the water
        // makes a splash with its sound in the original, ours nothing): the missile comes to its
        // end on the floor of a water tile, and ObjectHitsFloorTileDestroyTalismans_seg029_C6F
        // spawns the splash picture for anything landing there - see UWLevelLoader.SplashAt.
        if (mOLevelLoader != null && pONormal.y > 0.5f && fIsOverWater())
            mOLevelLoader.SplashAt(pOPoint);

        // The impact sits a bit in front of the surface hit, otherwise it is stuck in
        // the wall.
        if (mOLevelLoader != null && miImpactObjectId >= 0)
        {
            mOLevelLoader.SpawnEffectAt(miImpactObjectId, pOPoint + (pONormal * 2f), ImpactSeconds);

            // The sound belongs to the picture, and only to the PLAYER'S own impact:
            // SpawnImpactAnimo_seg022_2D2 plays effect 7 right beside the spawn, but only
            // when the attacker is the player (a launcher of ours means a creature shot it).
            if (mOLauncher == null)
                UWSoundEffects.PlayAt(UWSoundEffects.SpellImpact, pOPoint);
        }

        fDropAtImpact(pOPoint);

        Destroy(gameObject);
    }

    /// <summary>
    /// Drops the projectile at the impact point - see DropOnImpact.
    ///
    /// A bit BACK along the flight path: the impact point lies on the
    /// surface hit itself, and behind a wall the tile is solid - UWLevelLoader
    /// drops nothing there. The projectile's own radius is exactly the distance the
    /// ray still had free before it.
    /// </summary>
    private void fDropAtImpact(Vector3 pOPoint, bool pbAtRest = false)
    {
        if (mOLevelLoader == null)
            return;

        // ON AN EDGE onto the tile that carries it, whichever way the item ended - resting after a
        // slide, or dropped after hitting a wall (per user, 2026-09-28: it did not always snap).
        Vector3 lODrop = pOPoint - (mODirection * mfProjectileRadius);

        if (mOLevelLoader.TrySnapOntoCarryingTile(ref lODrop, mfProjectileRadius))
            pOPoint = lODrop + (mODirection * mfProjectileRadius);

        if (mODropItem != null)
        {
            if (fBreaksOnLanding(mODropItem.ID, mODropItem.HasQuantity ? mODropItem.Quantity : 1))
            {
                mODropItem = null;

                return;
            }

            // If the point in front of the wall still lies in a solid tile, the last
            // position of the flight is free for certain - the item must not vanish.
            // A FULL LEVEL is no reason to try again: the original lands the object once, and
            // when even culling finds no slot, the object is not made (UWObjectLimitRules) - a
            // second try here would cull a second round.
            if (!mOLevelLoader.DropThrownItemAt(mODropItem, pOPoint - (mODirection * mfProjectileRadius),
                    UWLevelLoader.HeadingFromDirection(mODirection), pbAtRest)
                && !mOLevelLoader.LastSpawnRefusedForSlot)
                mOLevelLoader.DropThrownItemAt(mODropItem, transform.position,
                    UWLevelLoader.HeadingFromDirection(mODirection), pbAtRest);

            mODropItem = null;

            return;
        }

        if (miDropObjectId < 0 || fBreaksOnLanding(miDropObjectId, 1))
            return;

        mOLevelLoader.DropObjectAt(miDropObjectId, pOPoint - (mODirection * mfProjectileRadius),
            UWLevelLoader.HeadingFromDirection(mODirection));
    }
}
