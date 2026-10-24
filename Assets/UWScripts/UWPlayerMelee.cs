using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// THE PLAYER'S BLOW AT ITS HIT FRAME (cleanup step 5.14, 2026-10-22: out of Interaction, which
/// keeps the public surface and forwards): the target - the classic scheme's box probe on the
/// level data (UWPlayerAttack.PickBlowTarget through UWMotionHost.FindBlowTarget), the modern
/// scheme's ray through the crosshair -, the wall a blow meets, the hit check and damage
/// (UWPlayerAttack.ResolveMelee over UWAttackResolution, whose random draws keep the original's
/// order), the door bash with its spike (UWDamageable.TakesBlowFirst), and the wear of the weapon
/// and of the armour a critical hit on the player picks. The pictures and the eyes are
/// UWHitFeedback's; what the release kept (charge, kind, ninth) is UWPlayerAttackState's.
/// </summary>
public sealed class UWPlayerMelee
{
    private readonly Interaction mOInteraction;

    private readonly UWPlayerAttackState mOState;

    private readonly UWHitFeedback mOFeedback;

    public UWPlayerMelee(Interaction pOInteraction, UWPlayerAttackState pOState, UWHitFeedback pOFeedback)
    {
        mOInteraction = pOInteraction;
        mOState = pOState;
        mOFeedback = pOFeedback;
    }

    private UWDataImport.DataImport fData()
    {
        return mOInteraction.GameData;
    }

    private UWLevelLoader fLoader()
    {
        return mOInteraction.LevelLoader;
    }

    private UWCharacter fCharacter()
    {
        return mOInteraction.Character;
    }

    private UWInventory fInventory()
    {
        return mOInteraction.Inventory;
    }

    private UWEyesDisplay fEyes()
    {
        return mOInteraction.Eyes;
    }

    /// <summary>Radius of the fallback check for close range, see fFindModernMeleeTarget.</summary>
    private const float CloseCombatRadius = 12f;

    /// <summary>
    /// Evaluates a strike: what is in range, what is used to strike, how much
    /// damage it does.
    ///
    /// The weapon comes from the main hand (UWInventory.MainHandSlot); if it is empty, the
    /// fist is used - the melee table lists it as a full weapon, so no
    /// special case is needed. The damage formula is in UWCombat, including a note on what
    /// of it comes from the data and what is invented.
    /// </summary>
    public void Strike()
    {
        bool lbLog = UnderworldRevisited.UWSettings.Instance != null && UnderworldRevisited.UWSettings.Instance.LogAttacks;

        Collider lOHitCollider;
        Vector3 lOHitPoint;
        float lfHitDistance;

        mbBlowStruckNothing = false;

        // CLASSIC SCHEME: as in the original a hit area in front of the character, not the
        // ray to the mouse cursor - see fFindClassicMeleeTarget.
        bool lbFound = UWControlScheme.Ui.Policy.MeleeByHitArea
            ? fFindClassicMeleeTarget(lbLog, out lOHitCollider, out lOHitPoint, out lfHitDistance)
            : fFindModernMeleeTarget(lbLog, out lOHitCollider, out lOHitPoint, out lfHitDistance);

        // A SWING INTO THE AIR still makes the miss sound (effect 0x0A, the same as throwing):
        // the reference plays it at the attacker when the hit check finds no target ("swing
        // and a miss", combat.cs ExecuteAttack/CombatMissImpactSound). Noticed missing by the
        // user, 2026-09-15.
        if (!lbFound)
        {
            if (!mbBlowStruckNothing)
                fTryWallImpact();

            UWSoundEffects.PlayAtAvatar(UWSoundEffects.Miss);
            return;
        }

        UWEntityInfo lOTarget = lOHitCollider.GetComponentInParent<UWEntityInfo>();

        // A door's frame or blocker is the door (UWEntityInfo.PartOf) - for the modern scheme's
        // ray too; the classic search resolves it already.
        if (lOTarget != null && lOTarget.PartOf != null)
        {
            lOTarget = lOTarget.PartOf;

            Collider lOWhole = lOTarget.GetComponentInChildren<Collider>();

            if (lOWhole != null)
                lOHitPoint = lOWhole.bounds.ClosestPoint(lOHitPoint);
        }

        UWDamageable lODamageable = lOTarget == null ? null : lOTarget.GetComponentInParent<UWDamageable>();

        if (lODamageable == null || !lODamageable.TakesBlows)
        {
            if (lOTarget == null)
                fTryWallImpact();

            UWSoundEffects.PlayAtAvatar(UWSoundEffects.Miss);
            return;
        }

        if (lbLog)
            Debug.Log(string.Format("[Attack] hit: {0} at {1:0.0}, object={2}, damageable={3}.",
                lOHitCollider.name, lfHitDistance,
                lOTarget == null || lOTarget.ObjectData == null ? -1 : lOTarget.ObjectData.ID,
                lODamageable != null));

        fApplyMeleeHit(lOTarget, lODamageable, lOHitPoint);
    }

    /// <summary>Modern scheme: the ray from the camera through the screen centre, as before.
    /// </summary>
    private bool fFindModernMeleeTarget(bool pbLog, out Collider pOCollider, out Vector3 pOPoint, out float pfDistance)
    {
        pOCollider = null;
        pOPoint = Vector3.zero;
        pfDistance = 0f;

        RaycastHit lOHit;

        bool lbHit = Physics.Raycast(mOInteraction.InteractionRay(), out lOHit, mOInteraction.AttackRange);

        // If the enemy stands very close, the camera point lies INSIDE its
        // collider - for a creature that is square in plan and therefore
        // 68 units deep, so it reaches 34 units forward. A ray that starts inside a
        // body reports no hit, and the strike went nowhere (per user,
        // 2026-08-30). So on a miss, check once more with a sphere.
        if (!lbHit)
            lbHit = Physics.SphereCast(mOInteraction.InteractionRay(), CloseCombatRadius, out lOHit, mOInteraction.AttackRange);

        if (!lbHit)
        {
            if (pbLog)
            {
                // Check again without a range limit: so the log shows what lies THERE and how
                // far away it is. That distinguishes "nothing hit" from "just too far".
                if (Physics.Raycast(mOInteraction.InteractionRay(), out RaycastHit lOFar, UWWorldPicker.Range))
                    Debug.Log(string.Format("[Attack] nothing within range {0}. Nearest target: {1} at {2:0.0}.",
                        mOInteraction.AttackRange, lOFar.collider.name, lOFar.distance));
                else
                    Debug.Log(string.Format("[Attack] nothing hit, not even in the distance."));
            }

            return false;
        }

        pOCollider = lOHit.collider;
        pOPoint = lOHit.point;
        pfDistance = lOHit.distance;

        return true;
    }

    /// <summary>Object 127 is the avatar; its height from COMOBJ.DAT determines the
    /// strike height.</summary>
    private const int AvatarObjectId = 127;

    /// <summary>Height of the avatar in original units, in case COMOBJ.DAT is missing.</summary>
    private const int FallbackAvatarHeight = 32;

    /// <summary>
    /// THE BLOW'S PROBE as resolve_attack builds it: a box (radius + 1) eighths of a tile to each
    /// side, its height from the weapon's radius, at the height of the swing (the low, middle or
    /// high third of the avatar by the kind of blow, raised or lowered by the view's pitch),
    /// centred (radius + 3) eighths ahead along the facing. The radius is the weapon's from
    /// COMOBJ.DAT, the fist's without one. Shared by the target search and the wall's impact.
    /// </summary>
    /// <summary>
    /// Where the blow's picture sits in height (world y), written by fGetMeleeProbe for the
    /// strike it serves: the original's hitz (COMBAT.C, AttackerAppliesFinalDamage) is the
    /// probe's z - the attacker's feet plus the swing's share of his height plus his pitch -
    /// plus a sixth of his height, so a stab flashes low, a bash high. Until 2026-10-12 the
    /// flash on an object took the height of the probe's closest point, the probe's middle,
    /// and sat too high (per user on the original: "a little lower").
    /// </summary>
    private float mfMeleeHitY;

    /// <summary>The probe's bottom above the player's feet and its height, in zpos - written by
    /// fGetMeleeProbe; the body part is picked against them (resolve_attack hands pickloc the
    /// probe, not the attacker's body - C3, 2026-10-12).</summary>
    private int miMeleeProbeOffsetZ;

    private int miMeleeProbeHeightZ;

    /// <summary>The attacker's eighth cell and the probe's centre after move_along, in eighths -
    /// written by fGetMeleeProbe for the classic target search.</summary>
    private UWTilePos mOMeleeFrom;

    private UWTilePos mOMeleeAt;

    /// <summary>The classic search met objects in its probe but struck none of them: resolve_attack
    /// then returns without asking the terrain, so no wall impact either.</summary>
    private bool mbBlowStruckNothing;

    private void fGetMeleeProbe(out Transform pOBody, out Vector3 pOForward, out Vector3 pOCentre,
        out Vector3 pOHalfExtents, out Quaternion pORotation, out int piRadius)
    {
        CharacterController lOController = mOInteraction.GetComponentInParent<CharacterController>();
        Transform lOBody = lOController != null ? lOController.transform : mOInteraction.transform;

        pOBody = lOBody;

        // Weapon radius from COMOBJ.DAT; without a melee weapon the fist's - a torch, a wand or
        // anything else in the hand reaches as far as the fist (GetPlayerWeapon, UnderworldExhumed
        // combat/COMBAT.C, sets wsize from ITEM_FIST for anything outside class 0; until
        // 2026-10-12 ours took the held object's own radius).
        UWObject lOWeapon = fInventory() == null ? null : fInventory().GetEquipped(fInventory().MainHandSlot);
        int liWeaponId = lOWeapon == null || lOWeapon.ID > UWPlayerCritterRow.MeleeLastId ? UWCombat.FistObjectId : lOWeapon.ID;
        int liRadius = 0;
        int liAvatarHeight = FallbackAvatarHeight;
        UWCommonObjectProperties.Entry lOEntry;

        if (fData().CommonObjectProperties != null)
        {
            if (fData().CommonObjectProperties.TryGet(liWeaponId, out lOEntry))
                liRadius = lOEntry.Radius;

            if (fData().CommonObjectProperties.TryGet(AvatarObjectId, out lOEntry) && lOEntry.Height > 0)
                liAvatarHeight = lOEntry.Height;
        }

        // towhere is the clicked ninth, 1 to 9 (mOState.PendingNinth; the middle column 2, 5, 8 by kind
        // until 2026-10-12, see fChooseAttackKind): towhere / 3 gives 0 for 1 and 2, 1 for 3 to 5,
        // 2 for 6 to 8 and 3 for 9 - a bash in the top right corner aims above the head.
        int liSwingHeight = mOState.PendingNinth;

        float lfFeetY = lOController != null
            ? lOBody.position.y + lOController.center.y - (lOController.height * 0.5f)
            : lOBody.position.y;

        // View pitch in degrees, positive downwards (Unity) - in the original a positive
        // value raises the area, hence the inverted sign.
        float lfPitchDegrees = 0f;

        if (Camera.main != null)
        {
            lfPitchDegrees = Camera.main.transform.localEulerAngles.x;

            if (lfPitchDegrees > 180f)
                lfPitchDegrees -= 360f;
        }

        float lfPitchUnits = -lfPitchDegrees * (UWWorldScale.FullCircleUnits / 360f);

        float lfBottomUnits = (liAvatarHeight * (liSwingHeight / 3) / 3) + (lfPitchUnits / 0x200);
        float lfHeightUnits = ((liRadius * 2) + 1) * 4;

        miMeleeProbeOffsetZ = (liAvatarHeight * (liSwingHeight / 3) / 3) + (int)(lfPitchUnits / 0x200);
        miMeleeProbeHeightZ = (int)lfHeightUnits;

        Vector3 lOForward = new Vector3(lOBody.forward.x, 0f, lOBody.forward.z);

        if (lOForward.sqrMagnitude < 0.0001f)
            lOForward = Vector3.forward;
        else
            lOForward.Normalize();

        float lfBottom = lfFeetY + (lfBottomUnits * UWWorldScale.ZPosStep);
        float lfHeight = lfHeightUnits * UWWorldScale.ZPosStep;

        // The probe's radius is wsize + 1 eighths, and the motion core's boxes run from x - radius
        // to x + radius INCLUSIVE in eighth cells (UWMotionCore.fCreateRecord), so the square
        // reaches half a cell past the radius around the centre cell (2026-10-14; until then ours
        // stopped at the radius).
        float lfHalfWidth = (liRadius + 1.5f) * UWWorldScale.SubTileStep;

        // The height of the blow's picture (COMBAT.C hitz): the probe's bottom plus a sixth
        // of the attacker's height - see mfMeleeHitY.
        mfMeleeHitY = lfBottom + ((liAvatarHeight / 6) * UWWorldScale.ZPosStep);

        // THE CENTRE AS THE ORIGINAL PUTS IT (resolve_attack, UnderworldExhumed COMBAT.C): the
        // attacker's eighth cell moved wsize + 3 along the 256-step heading by move_along, which
        // truncates each axis and adds one away from zero (UWAreaSpellRules.MoveAlong) - one eighth
        // farther ahead than a plain distance (per user 2026-10-14: our reach felt shorter).
        Vector3 lOCentre = new Vector3(lOBody.position.x, lfBottom + (lfHeight * 0.5f), lOBody.position.z)
            + (lOForward * ((liRadius + 3) * UWWorldScale.SubTileStep));

        if (fLoader() != null)
        {
            UWTilePos lOTile = fLoader().WorldPositionToTile(lOBody.position);
            UWTilePos lOFrom = new UWTilePos((lOTile.X << 3) + UWTileQueries.WorldToEighthFloor(lOBody.position.x),
                (lOTile.Y << 3) + UWTileQueries.WorldToEighthFloor(lOBody.position.z));
            UWTilePos lOAt = UWAreaSpellRules.MoveAlong(lOFrom, UWPlayerThrow.GetPlayerHeading(lOBody), liRadius + 3);

            mOMeleeFrom = lOFrom;
            mOMeleeAt = lOAt;
            lOCentre = new Vector3(UWUnits.EighthsToWorldAxis(lOAt.X), lOCentre.y, UWUnits.EighthsToWorldAxis(lOAt.Y));
        }

        Vector3 lOHalfExtents = new Vector3(lfHalfWidth, lfHeight * 0.5f, lfHalfWidth);

        // THE PROBE IS SQUARE TO THE MAP, not turned with the view: ObjectCheck tests the motion
        // core's boxes, the mover's from x - radius to x + radius in eighths and every record's
        // the same way (UWMotionCore.fCreateRecord) - all aligned to the tile grid. Facing a
        // diagonal, the square's corner reaches farther than a turned box; per user on the
        // original 2026-10-14 (audit B5) a goblin diagonally round a corner is hit from close
        // by, which ours, with the turned box, never reached (it struck the wall).
        Quaternion lORotation = Quaternion.identity;

        pOForward = lOForward;
        pOCentre = lOCentre;
        pOHalfExtents = lOHalfExtents;
        pORotation = lORotation;
        piRadius = liRadius;
    }

    /// <summary>
    /// THE BLOW THAT MEETS A WALL (find_wall_coll, UnderworldExhumed COMBAT.C, read 2026-10-11;
    /// the Exhumed audit's row 14): when the swing found no object, resolve_attack asks whether
    /// its probe touches a wall; if so, find_wall_coll walks from the attacker along the facing in
    /// steps of an eighth of a tile, (radius + 3) + 1 of them, at the probe's height, and at the
    /// first step inside the wall puts the impact picture 0x1CB and, for the player's blow,
    /// plays effect 7 - before the miss. Where the walk does not reach the wall (a wall touched
    /// only at the probe's side) there is neither. Ours does the same with the same probe
    /// (fGetMeleeProbe) and the walk as one level ray at its height (fTryFindWallInReach). Until
    /// 2026-10-11 ours played the miss alone; the first version that day looked along at eye
    /// height and missed the low walls (per user: "not at every wall").
    /// </summary>
    private void fTryWallImpact()
    {
        if (!fTryFindWallInReach(out Vector3 lOAt))
            return;

        // The sound and the delayed picture (UWHitFeedback.ShowWallImpact).
        mOFeedback.ShowWallImpact(lOAt);
    }

    /// <summary>
    /// Where the walk of find_wall_coll first enters the wall, or false: a level ray (radius
    /// + 4) eighths long from the attacker along the facing at the probe's height - the walk's
    /// steps of an eighth as one ray. The terrain the original tests is the walls (its hit bits
    /// 0x300), so only a collider without an entity counts whose face stands upright, not the
    /// floor or the ceiling a low or high blow brushes.
    /// </summary>
    private bool fTryFindWallInReach(out Vector3 pOAt)
    {
        pOAt = Vector3.zero;

        fGetMeleeProbe(out Transform lOBody, out Vector3 lOForward, out Vector3 lOCentre, out Vector3 lOUnusedExtents,
            out Quaternion lOUnusedRotation, out int liRadius);

        Vector3 lOOrigin = new Vector3(lOBody.position.x, lOCentre.y, lOBody.position.z);
        RaycastHit[] lOHits = Physics.RaycastAll(lOOrigin, lOForward, (liRadius + 4) * UWWorldScale.SubTileStep, ~0,
            QueryTriggerInteraction.Ignore);

        // RaycastAll comes in no order - the nearest first.
        System.Array.Sort(lOHits, (pOLeft, pORight) => pOLeft.distance.CompareTo(pORight.distance));

        foreach (RaycastHit lOHit in lOHits)
        {
            if (lOHit.collider == null || lOHit.collider.transform.IsChildOf(lOBody))
                continue;

            if (lOHit.collider.GetComponentInParent<UWEntityInfo>() != null || Mathf.Abs(lOHit.normal.y) > 0.5f)
                return false;

            pOAt = lOHit.point - (lOForward * UWWorldScale.SubTileStep * 0.5f);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Melee in the classic scheme AS IN THE ORIGINAL (per user, 2026-09-13: "rebuild
    /// melee in the classic scheme as in the original"; reason: lurkers and flying
    /// enemies can be fought in the original by looking down or up).
    ///
    /// REFERENCE combat.checkAttackHit: no ray to the mouse cursor, but an area in front of
    /// the character in its view direction.
    ///   Centre: weapon radius plus three eighths of a tile ahead.
    ///   Width:  weapon radius plus one, in eighths.
    ///   Bottom: foot height plus avatar height times (height value divided by 3) divided by 3, both
    ///           integer. Height value per attack type 2, 5 or 8, plus the view pitch
    ///           divided by 0x200.
    ///   Height: (weapon radius times 2 plus 1) times 4.
    /// The nearest thing in the area is hit.
    ///
    /// UNITS (UWViewpoint): horizontally one eighth of a tile = 8 world units, vertically one
    /// unit = 2 world units (UWWorldScale.ZPosStep), angle 65536 per full circle.
    /// A view step of 15 degrees thus raises or lowers the area by a good 5 units.
    ///
    /// ATTACK TYPE: the stab is lowest, the side slash in the middle, the
    /// overhead strike highest - matching the mouse thirds and animation (per user,
    /// 2026-09-13: "stab is the lowest"). Height values thus stab 2, slash 5, overhead 8.
    /// The reference contradicts itself here (comments on the height value versus its
    /// input mapping); a first version followed the input mapping and put the stab
    /// highest.
    ///
    /// NO WALL TEST: resolve_attack (UnderworldExhumed COMBAT.C) asks only what its probe touches
    /// (ObjectCheck first, the terrain only when no object is there) - no line from the attacker
    /// to the target. Per user on the original 2026-10-14 (audit B5): close to a corner, a goblin
    /// diagonally behind the wall's edge is hit; ours cast a ray to the hit point and struck the
    /// wall instead, until that day.
    /// </summary>
    private bool fFindClassicMeleeTarget(bool pbLog, out Collider pOCollider, out Vector3 pOPoint, out float pfDistance)
    {
        pOCollider = null;
        pOPoint = Vector3.zero;
        pfDistance = 0f;

        fGetMeleeProbe(out Transform lOBody, out Vector3 lOForward, out Vector3 lOCentre, out Vector3 lOHalfExtents,
            out Quaternion lORotation, out int liRadius);

        UWMotionHost lOWorld = UWMotionHost.Ensure(fLoader());

        if (lOWorld == null || fCharacter() == null)
            return false;

        // THE TARGET FROM THE LEVEL DATA'S BOXES (UWPlayerAttack.PickBlowTarget): the probe of
        // radius weapon + 1, its bottom the swing's height above the feet, the nearest object in
        // its height struck. Until 2026-10-14 ours asked the drawn colliders, which are narrower
        // than the original's boxes - a door's box is radius 3 around its position - and struck
        // the wall where the original strikes the door's frame (audit B5).
        int liProbeBottom = fCharacter().GetFeetZPos() + miMeleeProbeOffsetZ;
        int liFound = lOWorld.FindBlowTarget(mOMeleeAt.X, mOMeleeAt.Y, liProbeBottom, liRadius + 1, miMeleeProbeHeightZ,
            mOMeleeFrom.X, mOMeleeFrom.Y, out UWObject lOObject, out UWCritter lOCritter);

        mbBlowStruckNothing = liFound != UWPlayerAttack.BlowFoundNothing;

        UWEntityInfo lOEntity = null;

        if (lOCritter != null)
            lOEntity = lOCritter.GetComponentInParent<UWEntityInfo>();
        else if (lOObject != null && fLoader() != null)
            fLoader().TryGetEntity(lOObject, out lOEntity);

        Collider lOStruck = lOCritter != null ? lOCritter.GetComponentInChildren<Collider>()
            : lOEntity != null ? lOEntity.GetComponentInChildren<Collider>() : null;

        if (pbLog)
            Debug.Log(string.Format("[Attack] probe at eighths {0}/{1}, z {2}, radius {3}, height {4}: {5}.",
                mOMeleeAt.X, mOMeleeAt.Y, liProbeBottom, liRadius + 1, miMeleeProbeHeightZ,
                liFound == UWPlayerAttack.BlowFoundNothing ? "no object"
                    : liFound == UWPlayerAttack.BlowStruckNothing ? "objects, none in the blow's height"
                    : lOStruck != null ? "target " + lOStruck.name : "a target without a body"));

        if (lOStruck == null)
            return false;

        // The flash on the body's bounds, nearest the probe's centre (the door leaf is a concave
        // mesh collider, for which ClosestPoint hands the point back unchanged).
        Vector3 lOPoint = lOStruck.bounds.ClosestPoint(lOCentre);
        Vector3 lOFlat = lOPoint - lOBody.position;

        lOFlat.y = 0f;

        pOCollider = lOStruck;
        pOPoint = lOPoint;
        pfDistance = lOFlat.magnitude;

        return true;
    }

    /// <summary>Check, damage and feedback of a melee hit - the same for both
    /// schemes.</summary>
    private void fApplyMeleeHit(UWEntityInfo lOTarget, UWDamageable lODamageable, Vector3 pOHitPoint)
    {
        bool lbLog = UnderworldRevisited.UWSettings.Instance != null && UnderworldRevisited.UWSettings.Instance.LogAttacks;

        if (lODamageable == null || !lODamageable.TakesBlows)
            return;

        UWObject lOWeapon = fInventory() == null ? null : fInventory().GetEquipped(fInventory().MainHandSlot);
        int liWeaponId = lOWeapon == null ? UWCombat.FistObjectId : lOWeapon.ID;

        // A CREATURE'S ARMOUR IS THAT OF THE PART HIT: PickBodyHitPoint over the two bodies,
        // then row[part % 4] with the 0xFF fallback and a strong defender's 5/3
        // (AttackerAppliesFinalDamage labels 990-A02, deviation 49) - the creature as the
        // defender of UWAttackResolution since cleanup step 4.9 (2026-10-18). A thing - a door, a
        // chest - takes no armour (do_damage subtracts it for creatures only; the UWDamageable's
        // one value of a thing is 0 from the spawner anyway).
        UWCritter lOCritterTarget = lOTarget != null ? lOTarget.GetComponentInParent<UWCritter>() : null;
        IUWAttackDefender lIDefender = lOCritterTarget != null ? lOCritterTarget : UWAttackResolution.ObjectDefender;
        int liFlank = 0;

        // The blow's probe (fGetMeleeProbe; the modern scheme's ray finds its target without it,
        // so it is measured here for both): its height decides the part and the picture's place.
        fGetMeleeProbe(out Transform lOUnusedBody, out Vector3 lOUnusedForward, out Vector3 lOUnusedCentre,
            out Vector3 lOUnusedExtents, out Quaternion lOUnusedRotation, out int liUnusedRadius);

        int liPart = UWAttackResolution.NoPart;

        if (lOCritterTarget != null && fCharacter() != null)
        {
            // THE PART IS PICKED AGAINST THE BLOW'S PROBE, by the user's decision of 2026-10-12
            // to follow the original (combat C3): resolve_attack hands pickloc the probe -
            // the ninth's third of the body, the pitch, the weapon's reach in height - not the
            // attacker's body. A stab mostly meets the legs, an overhead bash often the head.
            // Until then ours took the player's whole body, so the kind of blow made no difference.
            int liProbeBottom = fCharacter().GetFeetZPos() + miMeleeProbeOffsetZ;

            liPart = lOCritterTarget.PickPartHitBy(liProbeBottom, liProbeBottom + miMeleeProbeHeightZ);

            // CalcFlankingBonus_seg022_230E_D48 from both headings in eighths, as a creature's
            // blow has it (UWCritterBrain): 0 face to face, 4 from behind.
            liFlank = UWCritterRules.GetFlankingBonus(
                UWPlayerThrow.GetPlayerHeading(fCharacter().transform) >> 5, lOCritterTarget.FacingEighth);
        }
        else if (fCharacter() != null && lOTarget != null && lOTarget.ObjectData != null)
        {
            // compute_hitangle takes any target's heading - a door or a bench struck from its
            // back takes more (COMBAT.C, 2026-10-14).
            liFlank = UWCritterRules.GetFlankingBonus(
                UWPlayerThrow.GetPlayerHeading(fCharacter().transform) >> 5, lOTarget.ObjectData.Heading);
        }

        // The wielded weapon's Accuracy or Damage enchantment (DoPlayerWeapon).
        UWPlayerAttack.GetWeaponEnchantment(lOWeapon,
            fData() != null && fData().InitialPlayer != null ? fData().InitialPlayer.InventoryRecords : null,
            out int liEnchantAccuracy, out int liEnchantDamage);

        // THE WEAPON WEARS (UWEquipmentWear, per user 2026-09-28: the dagger lost quality twice
        // in the original, never in ours): a blow at a door on its roll, a critical failure
        // against a creature that does not spare it - in frp_check, between the check and the
        // dice, since cleanup step 4.9b (2026-10-18, by the user's decision to follow the
        // original; UWAttackResolution.PlayerBlow calls fWearWeapon). Worn here after the dice
        // until then.
        int liTargetId = lOTarget != null && lOTarget.ObjectData != null ? lOTarget.ObjectData.ID : -1;

        // The numbers - hit check and damage - are in UWPlayerAttack (P3 of the engine
        // separation, 2026-09-18). The linear charge fraction (0 to 1) kept on release, see
        // mOState.PendingCharge.
        UWPlayerAttack.MeleeResult lOResult = UWPlayerAttack.ResolveMelee(fData(), liWeaponId,
            UWPlayerAttackState.CombatKindOf(mOState.PendingKind), mOState.PendingCharge,
            fCharacter() != null ? fCharacter().Vitals.Strength : 0,
            fCharacter() != null ? fCharacter().Vitals.Attack : 0,
            fCharacter() != null ? fCharacter().Vitals.Dexterity : 0,
            fCharacter() != null ? fCharacter().Vitals.GetSkill(UWPlayerData.Skill.Unarmed) : 0,
            fCharacter() != null ? (System.Func<UWPlayerData.Skill, int>)fCharacter().Vitals.GetSkill : null,
            lIDefender, liPart,
            fCharacter() != null && fCharacter().Vitals.IsEasyDifficulty, liFlank, liEnchantAccuracy, liEnchantDamage,
            liTargetId, lOCritterTarget != null && lOCritterTarget.WearsWeaponOnCriticalFailure, fWearWeapon);

        if (lOResult.NoWeapon)
            return;

        if (lbLog)
            Debug.Log(string.Format("[Attack] hit check {0} + flank {3} against {1}: {2}, damage {4} ({5} before armour {6}).",
                lOResult.Accuracy, lIDefender.Defence, lOResult.Check, lOResult.Flank,
                lOResult.Damage, lOResult.DamageBeforeArmour,
                lOCritterTarget != null ? lIDefender.ArmourAt(liPart) : 0));

        if (!lOResult.Hit)
        {
            // Blocked. No damage, no hit effect, no eyes display - only the sound, at the target:
            // 7 or 8 by the player's own row's weapon and armour kinds (do_miss reads both from
            // the attacker's row; UWCritterCombat.GetBlockedSound). The creature still notices
            // the strike: the reference calls DamageObject with zero damage, and it
            // turns hostile and attacks (per user on the original, 2026-09-12). The row's kinds
            // are read by UWPlayerAttack.ResolveMelee, the sound chosen by UWAttackResolution
            // (cleanup step 4.9, 2026-10-18).
            UWSoundEffects.PlayAt(lOResult.Outcome.BlockedSound, pOHitPoint);

            // The miss lands through the world as the player's blow with 0 (cleanup step 4.8,
            // 2026-10-18; UWCritter.OnMissedByPlayer told the mind past the body until then).
            UWCritter lOMissedCritter = lOTarget != null ? lOTarget.GetComponentInParent<UWCritter>() : null;
            UWWorld lOMissWorld = fLoader() != null ? UWWorld.Of(fLoader()) : UWScene.World;

            if (lOMissedCritter != null && lOMissWorld != null)
                lOMissWorld.Damage(UWWorld.TargetOf(lOMissedCritter, lOMissedCritter.Damageable), lOResult.Outcome.Blow);

            return;
        }

        int liDamage = lOResult.Damage;

        // The picture at the original's height (mfMeleeHitY, worked out by the probe above); a
        // creature's blood or flash at the height of the part hit (liPart, C3).
        Vector3 lOFlashPoint = new Vector3(pOHitPoint.x, mfMeleeHitY, pOHitPoint.z);

        // A SPIKED DOOR'S SPIKE takes the blow inside ApplyDamage (UWDamageable.TakesBlowFirst,
        // after the quality class shift, as damage_object does). Until 2026-10-12 the player's
        // blow went to the spike here first, in full and without the hit sound; do_damage plays
        // the sound and the flash for a spiked door as for any other.

        // The player's own strike is bash damage, just like a creature's (reference:
        // combat.AttackerAppliesFinalDamage with four). Of the creatures in uw1 only
        // object 124 resists this type, the one with resistance byte 0x7F. Through the world
        // since cleanup step 3.3 (2026-10-17): the creature is not told, its damage event charges
        // the player (UWBlow.PlayerBlow).
        UWWorld lOWorld = fLoader() != null ? UWWorld.Of(fLoader()) : UWScene.World;

        if (lOWorld != null)
            lOWorld.Damage(lOCritterTarget != null ? UWWorld.TargetOf(lOCritterTarget, lODamageable) : UWWorld.TargetOf(lODamageable),
                lOResult.Outcome.Blow);

        // The hit sound at the target, louder by the damage BEFORE the armour: the original
        // plays it at label 938 of AttackerAppliesFinalDamage_seg022_8A5 and only subtracts
        // the armour at labels A04-A10, so a blow the armour swallows is still heard at full
        // volume. We had the volume from the damage after armour until 2026-09-22. The
        // creature's own blows (UWCritter) had it right already.
        UWSoundEffects.PlayAt(UWSoundEffects.HitCritter, pOHitPoint, lOResult.Outcome.HitVolume);

        // Armour that swallows the whole blow leaves no visible trace: UW.EXE
        // (AttackerAppliesFinalDamage, seg022_230E_A6B) skips the blood or flash animation and
        // the eyes when the damage after armour is 0. DamageObject is still called, so the
        // creature notices the blow. This is why the Slasher of Veils (toughness 20) almost
        // never bleeds (per user, 2026-09-14).
        if (liDamage != 0)
        {
            mOFeedback.ShowHitEffect(lOTarget, lOFlashPoint, 0f, liPart);

            // A CRITICAL THAT DRAWS BLOOD SPLASHES TWICE (do_damage, UnderworldExhumed
            // combat/COMBAT.C, for the player as attacker): the second splat 2 below or 3 above
            // the first, in eighths of the creature's height (2026-10-12; ours had one).
            if (lOResult.Check == UWSkillCheck.ResultEnum.CriticalSuccess && mOFeedback.Bleeds(lOTarget))
                mOFeedback.ShowHitEffect(lOTarget, lOFlashPoint, mOFeedback.SecondSplatOffset(lOTarget), liPart);

            // The eyes at the top show the condition of the CREATURE just hit - not of a door, a
            // chest or a barrel (per user on the original, 2026-09-24).
            if (fEyes() != null && Interaction.IsCreatureEntity(lOTarget))
                mOFeedback.ShowEyes(lOTarget, lODamageable);
        }

        // NO LINE IN THE LOG: AttackerAppliesFinalDamage_seg022_8A5 prints nothing for a blow on
        // a door (per user on the original, 2026-09-27); ours wrote "The door is ..." after
        // every one until then. The condition stays in the look message.
    }

    /// <summary>
    /// A critical hit on the player wears the piece the body part picks by 2d4 (UWEquipmentWear,
    /// built 2026-09-28) - called by the creature's blow (UWCritter.fStrikePlayer).
    /// </summary>
    public void WearOnCriticalHit(int piPart)
    {
        if (fInventory() == null)
            return;

        int liSlot = UWCritterCombat.GetCriticalEquipmentSlot(piPart, !fInventory().IsLeftHanded, UWRandom.Next(0, 5));

        fWearEquipment(UWInventoryModel.FromSavegameSlot(liSlot),
            UWRandom.RollDice(UWEquipmentWear.CriticalHitDiceCount, UWEquipmentWear.CriticalHitDiceRange), false);
    }

    /// <summary>The weapon hand wears by count d range - only a melee weapon does.</summary>
    private void fWearWeapon(int piDiceCount, int piDiceRange)
    {
        if (fInventory() != null)
            fWearEquipment(fInventory().MainHandSlot, UWRandom.RollDice(piDiceCount, piDiceRange), true);
    }

    /// <summary>
    /// ovr120_C0F: the piece in the slot takes the damage, the message goes to the scroll, and a
    /// destroyed piece leaves the inventory and lies as a pile of debris at the player's feet.
    /// </summary>
    private void fWearEquipment(UWArmorItemMap.BodySlot peSlot, int piDamage, bool pbWeaponTest)
    {
        if (fInventory() == null || fData() == null)
            return;

        UWObject lOItem = fInventory().GetEquipped(peSlot);

        if (pbWeaponTest ? !UWEquipmentWear.IsWearableWeapon(lOItem) : !UWEquipmentWear.IsWearableArmour(lOItem, peSlot))
            return;

        UWEquipmentWear.Outcome leOutcome = UWEquipmentWear.Wear(lOItem, piDamage, fData(), out string lsMessage);

        if (leOutcome == UWEquipmentWear.Outcome.None)
            return;

        mOInteraction.AddMessage(lsMessage);

        if (leOutcome == UWEquipmentWear.Outcome.Destroyed)
        {
            fInventory().RemoveItem(lOItem);

            if (fLoader() != null && fCharacter() != null)
            {
                Vector3 lOAt = fCharacter().transform.position;
                UWTilePos lOTile = UWTileQueries.WorldToTile(lOAt.x, lOAt.z);

                // A NEW pile from CreateObj (INVDATA.C: junk = CreateObj(debris), nothing set on
                // it afterwards), so CreateObj's quality 40 - until 2026-10-12 this gave 0, as
                // the smashed world object's debris rightly does (UWContainerDamage). Put down
                // with near_mob_put_at, so at the tail of the tile's list (2026-10-18).
                fLoader().SpawnObjectById(UWObjectDamageRules.RollDebrisObjectId(), lOTile.X, lOTile.Y, 0, -1,
                    new Vector3(lOAt.x, 0f, lOAt.z), UWWorldEdit.Arrival.PutDown);
            }
        }

        fInventory().NotifyChanged();

        if (fCharacter() != null)
            fCharacter().RefreshStatus();
    }
}
