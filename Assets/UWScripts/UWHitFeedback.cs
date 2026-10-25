using UnityEngine;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// WHAT A HIT LOOKS LIKE (cleanup step 5.14, 2026-10-22: out of Interaction, which keeps the
/// public surface and forwards): the blood or the flash at the struck thing - where it sits, how
/// it is sorted, when it appears -, the second splat of a critical, the wall's impact picture and
/// sound, and the eyes at the top edge for a creature the player hit, by blow (UWPlayerMelee) or
/// by missile (UWMissiles through ReportSpellHit). The pictures wait for the swing on a
/// coroutine of Interaction's.
/// </summary>
public sealed class UWHitFeedback
{
    private readonly Interaction mOInteraction;

    public UWHitFeedback(Interaction pOInteraction)
    {
        mOInteraction = pOInteraction;
    }

    private UWDataImport.DataImport fData()
    {
        return mOInteraction.GameData;
    }

    private UWLevelLoader fLoader()
    {
        return mOInteraction.LevelLoader;
    }

    private UWEyesDisplay fEyes()
    {
        return mOInteraction.Eyes;
    }

    /// <summary>The blow that met a wall (UWPlayerMelee.fTryWallImpact): effect 7 at the spot and,
    /// after the swing's delay, the impact picture 0x1CB there.</summary>
    public void ShowWallImpact(Vector3 pOAt)
    {
        UWAudioService.PlayEffectAt(UWSoundEffectId.SpellImpact, pOAt);

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        mOInteraction.StartCoroutine(fShowHitEffectDelayed(UWObjectMechanics.FlashEffectObjectId, pOAt,
            lOSettings != null ? lOSettings.HitEffectDelaySeconds : 0.2f,
            lOSettings != null ? lOSettings.HitEffectSeconds : 0.6f));
    }

    /// <summary>Whether a struck creature bleeds (its table's blood bits, Critter.BleedsOnHit).</summary>
    public bool Bleeds(UWEntityInfo pOTarget)
    {
        return pOTarget != null && pOTarget.ObjectData != null && fData() != null
            && fData().ObjectClassProperties != null
            && fData().ObjectClassProperties.TryGetCritter(pOTarget.ObjectData.ID, out UWObjectClassProperties.Critter lOCritter)
            && lOCritter.BleedsOnHit;
    }

    /// <summary>do_damage's second splat on a player critical: (rand &amp; 1) * 5 - 2 more steps of
    /// put_effect's height unit, the creature's height &gt;&gt; 3 (at least 1), in world units.</summary>
    public float SecondSplatOffset(UWEntityInfo pOTarget)
    {
        int liHeight = 0;

        if (pOTarget != null && pOTarget.ObjectData != null && fData() != null && fData().CommonObjectProperties != null
            && fData().CommonObjectProperties.TryGet(pOTarget.ObjectData.ID, out UWCommonObjectProperties.Entry lOEntry))
            liHeight = lOEntry.Height;

        int liUnit = Mathf.Max(1, liHeight >> 3);
        int liSteps = (UWRandom.Next(0, 2) * 5) - 2;

        return liSteps * liUnit * UWWorldScale.ZPosStep;
    }

    /// <summary>
    /// The visible hit: a blood splatter, or an impact for bloodless beings. What
    /// the creature leaves behind is in its table (see
    /// UWObjectMechanics.GetHitEffectObjectId); everything else gets the flash.
    /// pfExtraHeight raises it (world units) - the second splat of a critical.
    /// </summary>
    public void ShowHitEffect(UWEntityInfo pOTarget, Vector3 pOHitPoint, float pfExtraHeight = 0f, int piPart = -1)
    {
        if (pOTarget == null || pOTarget.ObjectData == null || fLoader() == null
            || fData() == null || fData().ObjectClassProperties == null)
            return;

        // A CREATURE bleeds or dusts according to its table entry; EVERYTHING ELSE - door,
        // chest, barrel - gets the flash (animation object 448 + 0x0B, the reference calls it
        // "a flash" and spawns it for object hits, combat.cs). It was missing on every
        // non-creature so far (per user with a screenshot of the original, 2026-09-16).
        UWObjectClassProperties.Critter lOCritter;

        bool lbIsCritter = fData().ObjectClassProperties.TryGetCritter(pOTarget.ObjectData.ID, out lOCritter)
            && lOCritter.Vitality != 0;

        int liEffectId = lbIsCritter
            ? UWObjectMechanics.GetHitEffectObjectId(lOCritter.BloodAndRemains)
            : UWObjectMechanics.FlashEffectObjectId;

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;

        float lfSeconds = lOSettings != null ? lOSettings.HitEffectSeconds : 0.6f;
        float lfDelay = lOSettings != null ? lOSettings.HitEffectDelaySeconds : 0.2f;

        float lfVariation = lOSettings != null ? lOSettings.HitEffectHeightVariation : 8f;

        float lfDepthBias = 0f;
        Vector3 lOAt = lbIsCritter
            ? fVaryHitHeight(pOTarget, pOHitPoint, lfVariation)
            : fGetObjectFlash(pOTarget, fGetDoorFlashPoint(pOTarget, pOHitPoint), out lfDepthBias);

        // A BLOW WITH A KNOWN PART bleeds at that part's height (do_damage's hitz_tab, C3,
        // 2026-10-12): the variation the user saw on the original (2026-08-30) is the random part.
        UWCritter lOStruck = lbIsCritter && piPart >= 0 ? pOTarget.GetComponentInParent<UWCritter>() : null;

        if (lOStruck != null)
            lOAt.y = lOStruck.BodyPosition.y + lOStruck.GetHitEffectHeight(piPart);

        lOAt.y += pfExtraHeight;

        mOInteraction.StartCoroutine(fShowHitEffectDelayed(liEffectId, lOAt, lfDelay, lfSeconds, lfDepthBias));
    }

    /// <summary>
    /// WHERE A STRUCK DOOR FLASHES (AttackerAppliesFinalDamage_seg022_8A5, the branch for class
    /// 0x14): at the DOOR OBJECT'S OWN SPOT, not where the blow or the missile met it, at the
    /// height of the attack but at least two above the door's zpos. Seen by the user on the
    /// original, 2026-09-24: a Magic Missile still left of and above the door set it off, and
    /// the flash sat at the top of the leaf. Anything else keeps the hit point.
    /// </summary>
    private static Vector3 fGetDoorFlashPoint(UWEntityInfo pOTarget, Vector3 pOHitPoint)
    {
        if (pOTarget == null || !pOTarget.IsDoor || pOTarget.ObjectData == null)
            return pOHitPoint;

        UWObject lODoor = pOTarget.ObjectData;
        Vector3 lOSpot = UWViewpoint.SubTileToWorld(lODoor.TileX, lODoor.TileY, lODoor);
        float lfLowest = lOSpot.y + (DoorFlashAbove * UWWorldScale.ZPosStep);

        return new Vector3(lOSpot.x, Mathf.Max(pOHitPoint.y, lfLowest), lOSpot.z);
    }

    /// <summary>The flash sits at least this many zpos above the door's own - see fGetDoorFlashPoint.</summary>
    private const int DoorFlashAbove = 2;

    /// <summary>The flash on a struck sprite (a sack, a box) is sorted this many world units
    /// nearer than the sprite: two billboards on one spot would fight for the pixel.</summary>
    private const float SpriteFlashDepthBias = 4f;

    /// <summary>The flash on a struck model is sorted this far in front of the model's front
    /// towards the camera, in world units: enough that its own width clears a barrel's curve.</summary>
    private const float ModelFlashClearance = 4f;

    /// <summary>
    /// Where the flash appears when an OBJECT is struck - chest, barrel, sack, door - and how
    /// it is sorted.
    ///
    /// THE ORIGINAL puts it at the object's own spot: put_effect (EFFECT.C) copies the
    /// defender's fine position into the effect, AttackerAppliesFinalDamage gives the height
    /// of the blow (COMBAT.C hitz: the attacker's z plus a sixth of his height, a door at
    /// least two above its own z), and the flash, added behind the object in the tile's list,
    /// is drawn over it - over a chest's model too.
    ///
    /// So the flash stands AT THE OBJECT'S SPOT (a sprite's data position, a model's collider
    /// middle) at the height of the blow, and is SORTED IN FRONT of the object by the billboard
    /// shaders' depth bias (UWLevelLoader.SpawnEffectAt): a sprite by a hair, a model by its
    /// extent towards the camera plus ModelFlashClearance, so the mesh does not hide it (per
    /// user, 2026-09-16: "the impact effect is inside the barrel"). Until 2026-10-12 the flash
    /// MOVED instead - 12 units in front of the crosshair's ray hit, then in front of the
    /// model's face - and stood a fifth of a tile out (per user on the original: "closer to the
    /// chest", "too near the camera").
    /// </summary>
    private Vector3 fGetObjectFlash(UWEntityInfo pOTarget, Vector3 pOHitPoint, out float pfDepthBias)
    {
        pfDepthBias = SpriteFlashDepthBias;

        if (pOTarget == null || pOTarget.ObjectData == null)
            return pOHitPoint;

        UWCommonObjectProperties.Entry lOEntry;
        bool lbModel = pOTarget.IsDoor
            || fData() == null || fData().CommonObjectProperties == null
            || !fData().CommonObjectProperties.TryGet(pOTarget.ObjectData.ID, out lOEntry)
            || lOEntry.Is3DModel;

        if (!lbModel)
        {
            UWObject lOObject = pOTarget.ObjectData;
            Vector3 lOSpot = UWObjectSpawner.GetObjectWorldPosition(lOObject.TileX, lOObject.TileY, lOObject);

            return new Vector3(lOSpot.x, pOHitPoint.y, lOSpot.z);
        }

        Collider lOCollider = pOTarget.GetComponentInChildren<Collider>();

        if (lOCollider == null)
            return pOHitPoint;

        Bounds lOBounds = lOCollider.bounds;
        Vector3 lOToCamera = Camera.main != null ? Camera.main.transform.position - lOBounds.center : Vector3.forward;

        lOToCamera.y = 0f;

        if (lOToCamera.sqrMagnitude < 0.0001f)
            lOToCamera = Vector3.forward;

        lOToCamera.Normalize();

        // The collider's extent along the line to the camera: the model's front lies that far
        // before its middle.
        float lfAlongX = Mathf.Abs(lOToCamera.x) > 0.0001f ? lOBounds.extents.x / Mathf.Abs(lOToCamera.x) : float.MaxValue;
        float lfAlongZ = Mathf.Abs(lOToCamera.z) > 0.0001f ? lOBounds.extents.z / Mathf.Abs(lOToCamera.z) : float.MaxValue;

        pfDepthBias = Mathf.Min(lfAlongX, lfAlongZ) + ModelFlashClearance;

        return new Vector3(lOBounds.center.x, pOHitPoint.y, lOBounds.center.z);
    }

    /// <summary>
    /// The spot where the hit effect appears on a CREATURE.
    ///
    /// HORIZONTALLY the figure itself, NOT the point where the ray hit:
    /// a creature's collider is square in plan and thus for a
    /// goblin 68 units deep, so its ray hit lies half a tile in front of the
    /// figure. The splatter therefore floated in front of the enemy and looked too big up close
    /// (per user, 2026-08-30).
    ///
    /// VERTICALLY scattered: in the original the effect sits sometimes higher, sometimes lower, but always
    /// visibly on the enemy - so the random value is then clamped back into the extent of the
    /// target. A splatter next to the figure would no longer be randomness, but a
    /// bug.
    /// </summary>
    private static Vector3 fVaryHitHeight(UWEntityInfo pOTarget, Vector3 pOHitPoint, float pfVariation)
    {
        Vector3 lOPoint = pOHitPoint;

        if (pOTarget != null)
        {
            lOPoint.x = pOTarget.transform.position.x;
            lOPoint.z = pOTarget.transform.position.z;
        }

        UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;
        Collider lOCollider = pOTarget == null ? null : pOTarget.GetComponentInChildren<Collider>();

        // The starting point is the enemy's BODY CENTRE, not the ray hit. That lies
        // at the player's eye height and thus moves with distance and view angle; the
        // centre does not (per user, 2026-08-30).
        if (lOCollider != null)
            lOPoint.y = lOCollider.bounds.center.y;

        lOPoint.y += lOSettings != null ? lOSettings.HitEffectHeightOffset : 0f;

        if (pfVariation > 0f)
            lOPoint.y += Random.Range(-pfVariation, pfVariation);

        if (lOCollider != null)
        {
            // A little away from the edge, so the effect does not stick halfway out of the figure.
            float lfMargin = lOCollider.bounds.size.y * 0.15f;

            lOPoint.y = Mathf.Clamp(lOPoint.y,
                lOCollider.bounds.min.y + lfMargin, lOCollider.bounds.max.y - lfMargin);
        }

        return lOPoint;
    }

    /// <summary>
    /// The effect only appears when the strike visually lands - in the original the
    /// blood appears as soon as the weapon animation has reached the screen centre, not already on
    /// releasing the button (per user, 2026-08-30). The damage is still applied
    /// immediately; only the picture waits.
    /// </summary>
    private System.Collections.IEnumerator fShowHitEffectDelayed(int piObjectId, Vector3 pOHitPoint,
        float pfDelay, float pfSeconds, float pfDepthBias = 0f)
    {
        if (pfDelay > 0f)
            yield return new WaitForSeconds(pfDelay);

        if (fLoader() != null)
            fLoader().SpawnEffectAt(piObjectId, pOHitPoint, pfSeconds, pfDepthBias);
    }
    /// <summary>The player's missile has hit: an object's flash, a creature's eyes at the top
    /// edge as in melee. A CREATURE'S BLOOD comes from UWCritter.ShowMissileHitEffect alone, which the
    /// missile calls first: do_damage (UnderworldExhumed COMBAT.C) puts it once, a second only for
    /// a critical blow, which a missile never is. Until 2026-10-13 ours showed two (reference
    /// audit, finding of the fix agents).</summary>
    public void ReportSpellHit(UWEntityInfo pOTarget, UWDamageable pODamageable, Vector3 pOPoint)
    {
        if (pOTarget != null && !Interaction.IsCreatureEntity(pOTarget))
            ShowHitEffect(pOTarget, pOPoint);

        // Only for a creature - see UWPlayerMelee.fApplyMeleeHit.
        if (fEyes() != null && pODamageable != null && Interaction.IsCreatureEntity(pOTarget))
            ShowEyes(pOTarget, pODamageable);
    }

    /// <summary>The eyes for a creature the player hit: its hit points left against its table
    /// average (UWCombat.GetEyesGroup).</summary>
    public void ShowEyes(UWEntityInfo pOTarget, UWDamageable pODamageable)
    {
        UWCritter lOCritter = pOTarget != null ? pOTarget.GetComponentInParent<UWCritter>() : null;

        fEyes().ShowCondition(fData(), pODamageable.CurrentHealth,
            lOCritter != null ? lOCritter.TableVitality : pODamageable.MaxHealth, fFoeName(pOTarget));
    }

    /// <summary>The bare name of a creature hit, capitalised - for the modern heading strip
    /// (UWModernHud), as the crosshair names it.</summary>
    private string fFoeName(UWEntityInfo pOTarget)
    {
        if (pOTarget == null || pOTarget.ObjectData == null)
            return null;

        return UWText.Capitalised(UWText.ObjectName(fData(), pOTarget.ObjectData.ID, false, false, null));
    }
}
