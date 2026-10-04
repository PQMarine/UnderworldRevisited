using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;
using UnderworldRevisited.Build;

/// <summary>
/// Dropping and throwing the item at the pointer into the 3D view, following UW.EXE
/// (DropOrThrowByPlayer_seg025_355, 2026-09-14).
///
/// THE ORDER IN UW.EXE:
///
/// 1. The pointer position in the view window decides (InitPlayerProjectilesValues_seg025_D):
///    from row 37 of 113, counted from the bottom, it is a THROW, below that a DROP. That is
///    the lower third the user had observed.
/// 2. A throw turns the item into a projectile (PrepareProjectileObject_seg025_791). If that
///    fails - the start point in front of the player is blocked - the item is DROPPED
///    instead, not refused.
/// 3. A drop goes in the player's own facing, not towards the pointer.
///
/// All positions are computed in the original's units - eighth tiles for x/y, zpos for the
/// height - and only converted to world units at the end (UWViewpoint). That keeps the
/// rounding of the original, which works on the eighth grid.
///
/// In flight a thrown item moves like an object of UW.EXE - bouncing, sliding, hopping off
/// objects, pushing creatures, breaking - see UWSpellProjectile.EnableBouncing.
/// </summary>
public static class UWPlayerThrow
{
    /// <summary>Pointer rows from the bottom of the view window up to which it drops - UW.EXE
    /// compares with 0x25 (seg025_9B).</summary>
    public const int ThrowThresholdRow = 0x25;

    /// <summary>Size of the view window in the original's pointer coordinates, after
    /// clamping: X 0 to 0xAC, Y 0 to 0x71 (InitPlayerProjectilesValues_seg025_D).</summary>
    private const int ViewMaxX = 0xAC;

    private const int ViewMaxY = 0x71;

    /// <summary>
    /// Offsets UW.EXE subtracts from the screen pointer: 0x34 horizontally, 0x43 vertically
    /// (counted from the bottom of the 320x200 screen).
    ///
    /// Our view hole was measured at X 50-225 and Y 17-137 from the top (UWGameUI), i.e. 176
    /// by 121 pixels. Its left edge is therefore X = 50 - 52 = -2, its bottom edge
    /// Y = (200 - 137) - 67 = -4. Both clamp to zero, so the corners of the hole are the
    /// corners of the original's window.
    /// </summary>
    private const int HoleWidthPixels = 176;

    private const int HoleHeightPixels = 121;

    private const int HoleLeftInWindow = -2;

    private const int HoleBottomInWindow = -4;

    /// <summary>The ammunition type UW.EXE gives every thrown item (seg025_3B3) - it is the
    /// momentum factor, see ThrowSpeed.</summary>
    private const int ThrownAmmoType = 0xF;

    /// <summary>
    /// Horizontal speed of a thrown item.
    ///
    /// The ammunition type IS the speed byte of the projectile table (see
    /// UWObjectProperties.GetRangedSpeed), and the momentum is that value times 0x2F
    /// (motion_init.InitMotionParams). A thrown item therefore flies exactly as fast as a
    /// sling stone, whatever it is - weight plays no part at launch.
    /// </summary>
    private const float ThrowSpeed = ThrownAmmoType * UWSpellProjectile.SpeedFactor;

    /// <summary>
    /// Vertical speed of one pitch step.
    ///
    /// The pitch goes in as (Projectile_Pitch - 16) * 64 into the same register the momentum
    /// goes into (unk_a_pitch against momentum_14), and both run in the same units - 4
    /// original units per world unit on every axis. So one step is 64 / 0x2F of the speed
    /// factor.
    /// </summary>
    private const float SpeedPerPitchStep = 64f * UWSpellProjectile.SpeedFactor / 0x2F;

    /// <summary>How far a thrown item flies before it is put down where it is, if nothing
    /// stopped it.</summary>
    private const float ThrowRange = 2000f;

    /// <summary>Missile objects - major class 0, minor class 1 - are the only ones that hurt
    /// what they hit, see UWSpellProjectile.MakeHarmless.</summary>
    private const int FirstMissileObjectId = 0x10;

    private const int LastMissileObjectId = 0x1F;

    /// <summary>The player's object number while playing, see UWObjectMechanics.AdventurerObjectId
    /// - with the saved 63 the start height branch of PrepareProjectileObject would be skipped and
    /// every throw would start at the feet.</summary>
    private const int RuntimePlayerObjectId = UWObjectMechanics.AdventurerObjectId;

    private const int CrossbowBoltObjectId = UWObjectMechanics.CrossbowBoltObjectId;

    private const int ArrowObjectId = UWObjectMechanics.ArrowObjectId;

    /// <summary>The original message when nothing fits in front of the player - string 0xFD
    /// of block 1 (seg025_711).</summary>
    public const string NoSpaceMessage = "There is no space to drop that.";

    /// <summary>
    /// Drops or throws the item at the pointer. Returns false if it stays at the pointer.
    /// </summary>
    public static bool TryDropOrThrow(Vector2 pOMousePos, Transform pOCamera, UWInventory pOInventory,
        UWLevelLoader pOLevelLoader, UWGameUI pOGameUi, Interaction pOInteraction)
    {
        if (pOInventory == null || pOInventory.CursorItem == null || pOLevelLoader == null
            || pOGameUi == null || pOCamera == null)
            return false;

        int liX;
        int liY;

        GetPointerInView(pOGameUi, pOMousePos, out liX, out liY);

        return TryDropOrThrowAtView(liX, liY, pOCamera, pOInventory, pOLevelLoader, pOInteraction);
    }

    /// <summary>
    /// TryDropOrThrow with the pointer already in the view window's coordinates (see
    /// GetPointerInView) - for the modern scheme, whose whole screen is the view and which maps
    /// the pointer onto the window itself (UWModernBags).
    /// </summary>
    public static bool TryDropOrThrowAtView(int piX, int piY, Transform pOCamera, UWInventory pOInventory,
        UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        if (pOInventory == null || pOInventory.CursorItem == null || pOLevelLoader == null || pOCamera == null)
            return false;

        UWObject lOItem = pOInventory.CursorItem;

        if (piY >= ThrowThresholdRow
            && fTryThrow(lOItem, piX, piY, pOCamera, pOLevelLoader, pOInteraction))
        {
            pOInventory.TakeCursorItem();

            // It no longer lights the player's way, even while it still burns in flight.
            pOInventory.ApplyCarriedLight();

            fNoteMoonstone(lOItem, pOLevelLoader);

            return true;
        }

        if (!fTryDrop(lOItem, pOCamera, pOInventory, pOLevelLoader, pOInteraction))
            return false;

        fNoteMoonstone(lOItem, pOLevelLoader);

        return true;
    }

    /// <summary>The moonstone, or what holds it, went into the world on this level - Gate Travel
    /// goes here now (UWMoonstoneRules; in the original right after DropOrThrowByPlayer).</summary>
    private static void fNoteMoonstone(UWObject pOItem, UWLevelLoader pOLevelLoader)
    {
        UWMoonstoneRules.OnPutIntoWorld(pOItem, pOLevelLoader.CurrentLevelIndex + 1);
    }

    /// <summary>
    /// The pointer in the original's view window coordinates: X 0 to 172 from the left,
    /// Y 0 to 113 from the BOTTOM.
    /// </summary>
    public static void GetPointerInView(UWGameUI pOGameUi, Vector2 pOMousePos, out int piX, out int piY)
    {
        int liPixelX = Mathf.Min(Mathf.FloorToInt(pOGameUi.GetHorizontalFractionInGameArea(pOMousePos) * HoleWidthPixels),
            HoleWidthPixels - 1);
        int liPixelY = Mathf.Min(Mathf.FloorToInt(pOGameUi.GetVerticalFractionInGameArea(pOMousePos) * HoleHeightPixels),
            HoleHeightPixels - 1);

        piX = Mathf.Clamp(liPixelX + HoleLeftInWindow, 0, ViewMaxX);
        piY = Mathf.Clamp(liPixelY + HoleBottomInWindow, 0, ViewMaxY);
    }

    /// <summary>
    /// The pointer in the view window's coordinates when the whole screen is the view (modern
    /// scheme): the screen's width and height stretched over the window's X 0 to 0xAC and Y 0 to
    /// 0x71 from the bottom.
    /// </summary>
    public static void GetPointerInFullScreenView(Vector2 pOMousePos, out int piX, out int piY)
    {
        float lfX = Screen.width > 0 ? Mathf.Clamp01(pOMousePos.x / Screen.width) : 0.5f;
        float lfY = Screen.height > 0 ? Mathf.Clamp01(pOMousePos.y / Screen.height) : 0.5f;

        piX = Mathf.Clamp(Mathf.FloorToInt(lfX * (ViewMaxX + 1)), 0, ViewMaxX);
        piY = Mathf.Clamp(Mathf.FloorToInt(lfY * (ViewMaxY + 1)), 0, ViewMaxY);
    }

    /// <summary>
    /// Where a missile of the player goes, from the pointer in view window coordinates
    /// (GetPointerInView) - the throw and the spell projectile alike: the original computes
    /// both in the same routine (InitPlayerProjectilesValues_seg025_D, called from
    /// DropOrThrowByPlayer_seg025_355 and ProjectileSpell_seg025_2B1).
    /// piHeading is absolute, in 256ths of the circle, 0 north clockwise; piPitch is the
    /// missile pitch, positive up, clamped to the five-bit field.
    /// </summary>
    public static void GetMissileAim(int piX, int piY, Transform pOCamera, out int piHeading, out int piPitch)
    {
        // Direction relative to the player, in 256ths of the circle:
        //     1 + ((X - 86) * 5) / 13
        // integer division truncating towards zero, like idiv. Clockwise is positive, the same
        // as the heading itself (GetCoordinateInDirection turns 0x40 - heading into a maths angle).
        int liMissileHeading = 1 + (((piX - 0x56) * 5) / 0xD);

        // Pitch: camera pitch / 0x300 plus (Y - 56) / 6, positive is UP. The camera pitch is a
        // 16 bit angle there (UWViewpoint.PitchToDegrees has the sign).
        piPitch = (fGetOriginalCameraPitch(pOCamera) / 0x300) + ((piY - 0x38) / 6);

        // Projectile_Pitch is a five bit field with 16 as level. UW.EXE would wrap a value
        // outside it; our camera looks further up and down than the original's, so a wrap
        // would throw backwards through the floor - clamped instead.
        piPitch = Mathf.Clamp(piPitch, -16, 15);

        piHeading = (GetPlayerHeading(pOCamera) + liMissileHeading + 0x100) & 0xFF;
    }

    /// <summary>
    /// The flight direction of a missile of this speed (world units per second) at this aim:
    /// the heading as the horizontal part at full speed, the pitch as the vertical part,
    /// SpeedPerPitchStep per step - the pitch goes in against the momentum, so the same pitch
    /// climbs steeper for a slow missile than for a fast one.
    /// </summary>
    public static Vector3 GetMissileDirection(int piHeading, int piPitch, float pfSpeed)
    {
        Vector3 lOHorizontal = Quaternion.Euler(0f, piHeading * 360f / 256f, 0f) * Vector3.forward;
        Vector3 lOVelocity = (lOHorizontal * pfSpeed) + (Vector3.up * (piPitch * SpeedPerPitchStep));

        return lOVelocity.sqrMagnitude > 0.0001f ? lOVelocity.normalized : lOHorizontal;
    }

    /// <summary>
    /// Where a missile of the player starts, and whether it can - PrepareProjectileObject_seg025_791
    /// for the throw and the spell projectile alike. False when the spot is blocked: the throw
    /// then drops the item, a spell answers "There is not enough room to release that spell."
    /// Since 2026-09-24 the spell starts here too; before, it started a fixed distance in front of
    /// the camera without any check, and the user saw it cast where the original refuses (aiming
    /// far to the side in a narrow corridor) and come less from the right.
    /// </summary>
    public static bool TryGetMissileStart(Transform pOCamera, UWLevelLoader pOLevelLoader, int piMissileId,
        int piHeading, int piPitch, out Vector3 pOStart)
    {
        pOStart = Vector3.zero;

        DataImport lOData = pOLevelLoader != null ? pOLevelLoader.UWDataImporter : null;

        if (lOData == null || pOCamera == null)
            return false;

        Vector3 lOFeet = GetFeet(pOCamera);

        int liPlayerRadius;
        int liPlayerHeight;
        int liItemRadius;
        int liItemHeight;

        fGetSize(lOData, RuntimePlayerObjectId, out liPlayerRadius, out liPlayerHeight);
        fGetSize(lOData, piMissileId, out liItemRadius, out liItemHeight);

        // Start: the player's eighth-tile position, moved forward by both radii plus four
        // (motion_projectile.PlaceProjectileInWorld), then placed in the middle of its
        // sub-step - PrepareProjectileObject adds 0xF to (xpos << 5).
        int liEighthX = UWViewpoint.WorldToOriginalX(lOFeet.x) >> 5;
        int liEighthY = UWViewpoint.WorldToOriginalY(lOFeet.z) >> 5;

        StepInDirection(piHeading, liPlayerRadius + liItemRadius + 4, ref liEighthX, ref liEighthY);

        // Height: the player's zpos, plus five sixths of his height and two per pitch step.
        // Without a height of the launcher UW.EXE takes its zpos unchanged.
        int liZPos = UWViewpoint.WorldToOriginalZ(lOFeet.y) >> 3;

        if (liPlayerHeight != 0)
            liZPos += (((liPlayerHeight * 5) & 0xFF) / 6) + (piPitch << 1);

        pOStart = UWViewpoint.OriginalToWorld((liEighthX << 5) + 0xF, (liEighthY << 5) + 0xF, liZPos << 3);

        return fIsStartFree(pOCamera, pOStart, pOLevelLoader);
    }

    /// <summary>
    /// The throw. Returns false if the start point in front of the player is blocked - then
    /// UW.EXE drops the item instead.
    /// </summary>
    private static bool fTryThrow(UWObject pOItem, int piX, int piY, Transform pOCamera,
        UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        DataImport lOData = pOLevelLoader.UWDataImporter;

        if (lOData == null)
            return false;

        GetMissileAim(piX, piY, pOCamera, out int liHeading, out int liMissilePitch);

        if (!TryGetMissileStart(pOCamera, pOLevelLoader, pOItem.ID, liHeading, liMissilePitch, out Vector3 lOStart))
            return false;

        float lfYaw = liHeading * 360f / 256f;
        Vector3 lOHorizontal = Quaternion.Euler(0f, lfYaw, 0f) * Vector3.forward;
        Vector3 lOVelocity = (lOHorizontal * ThrowSpeed) + (Vector3.up * (liMissilePitch * SpeedPerPitchStep));

        GameObject lOFlying = pOLevelLoader.SpawnProjectile(pOItem.ID, lOStart, lOVelocity);
        if (lOFlying == null)
            return false;

        // UW1 plays effect 0xA when the projectile is placed (PrepareProjectileObject).
        UWSoundEffects.PlayAt(UWSoundEffects.Miss, lOStart);

        bool lbMissile = pOItem.ID >= FirstMissileObjectId && pOItem.ID <= LastMissileObjectId;

        UWSpellProjectile lOFlight = lOFlying.AddComponent<UWSpellProjectile>();

        lOFlight.Begin(lOVelocity.normalized, lOVelocity.magnitude,
            lbMissile && pOInteraction != null ? pOInteraction.GetThrownMissileDamage(pOItem.ID) : 0,
            ThrowRange, pOInteraction, pOLevelLoader, -1, UWDamageTypes.Missile);

        // Gravity -4 for everything that is not weightless (motion_init.InitMotionParams).
        if (!fIsWeightless(lOData, pOItem.ID))
            lOFlight.MakeBallistic(UWSpellProjectile.Gravity);

        if (!lbMissile)
            lOFlight.MakeHarmless();

        UWCommonObjectProperties.Entry lOEntry;

        if (lOData.CommonObjectProperties != null && lOData.CommonObjectProperties.TryGet(pOItem.ID, out lOEntry))
            lOFlight.EnableBouncing(lOEntry.Elasticity, lOEntry.SlidesFurther, lOEntry.Radius);
        else
            lOFlight.EnableBouncing(0, false, 0);

        lOFlight.DropItemOnImpact(pOItem);

        return true;
    }

    /// <summary>
    /// The drop, in the player's own facing.
    ///
    /// UW.EXE first tries (player radius + item radius + 1) eighth tiles ahead. Only if the item
    /// FITS there does it move three eighths further and test again, and it is the second test
    /// that counts (seg025_5CC to seg025_636). The item has to fit at both spots.
    /// </summary>
    private static bool fTryDrop(UWObject pOItem, Transform pOCamera, UWInventory pOInventory,
        UWLevelLoader pOLevelLoader, Interaction pOInteraction)
    {
        DataImport lOData = pOLevelLoader.UWDataImporter;

        if (lOData == null)
            return false;

        Vector3 lOFeet = GetFeet(pOCamera);
        int liHeading = GetPlayerHeading(pOCamera);

        int liPlayerRadius;
        int liPlayerHeight;
        int liItemRadius;
        int liItemHeight;

        fGetSize(lOData, RuntimePlayerObjectId, out liPlayerRadius, out liPlayerHeight);
        fGetSize(lOData, pOItem.ID, out liItemRadius, out liItemHeight);

        int liDistance = liPlayerRadius + liItemRadius + 1;

        int liEighthX = UWViewpoint.WorldToOriginalX(lOFeet.x) >> 5;
        int liEighthY = UWViewpoint.WorldToOriginalY(lOFeet.z) >> 5;

        StepInDirection(liHeading, liDistance, ref liEighthX, ref liEighthY);

        bool lbFits = fFitsAt(liEighthX, liEighthY, liItemRadius, liDistance, lOFeet, pOCamera, pOLevelLoader);

        if (lbFits)
        {
            StepInDirection(liHeading, 3, ref liEighthX, ref liEighthY);

            lbFits = fFitsAt(liEighthX, liEighthY, liItemRadius, liDistance, lOFeet, pOCamera, pOLevelLoader);
        }

        if (!lbFits)
        {
            pOInteraction?.AddMessage(NoSpaceMessage);

            UWSoundEffects.PlayAtAvatar(UWSoundEffects.Landing);

            return false;
        }

        // A burning light goes out when it is put down - only after it is sure to fit, otherwise
        // it would stay at the pointer doused.
        if (UWObjectMechanics.Extinguish(pOItem, lOData.Textures))
            pOInventory.ApplyCarriedLight();

        // Arrows and bolts lie in the facing (per user, 2026-09-07) - see SpawnDroppedObject.
        return pOInventory.DropCursorItemInWorld(fEighthToWorld(liEighthX, liEighthY, pOCamera.position.y),
            true, null, UWLevelLoader.HeadingFromDirection(pOCamera.forward));
    }

    /// <summary>
    /// Whether the item fits at this eighth-tile spot.
    ///
    /// APPROXIMATION of CheckIfItemFitsInTile_seg026_1008, which runs the whole collision scan.
    /// Checked here: the tile is open and level (dropping on a slope does not work, per the
    /// earlier test in the original), no wall lies between the player and the spot plus the item's
    /// radius, and the floor there is not higher than the player's feet plus the tolerance UW.EXE
    /// passes along (the same distance, in zpos steps).
    /// </summary>
    private static bool fFitsAt(int piEighthX, int piEighthY, int piItemRadius, int piTolerance,
        Vector3 pOFeet, Transform pOCamera, UWLevelLoader pOLevelLoader)
    {
        Vector3 lOSpot = fEighthToWorld(piEighthX, piEighthY, pOCamera.position.y);

        if (!pOLevelLoader.CanDropAt(lOSpot, true))
            return false;

        if (pOLevelLoader.GetFloorHeightAt(lOSpot) > pOFeet.y + (piTolerance * UWObjectSpawner.HeightScale))
            return false;

        if (fIsBlockedByObject(lOSpot, pOFeet.y, piItemRadius, pOLevelLoader))
            return false;

        Vector3 lOFrom = pOCamera.position;
        Vector3 lOTo = new Vector3(lOSpot.x, lOFrom.y, lOSpot.z);
        Vector3 lOLine = lOTo - lOFrom;
        float lfLength = lOLine.magnitude + (piItemRadius * UWObjectSpawner.SubTileScale);

        if (lOLine.sqrMagnitude < 0.0001f)
            return true;

        return !Physics.Raycast(lOFrom, lOLine.normalized, lfLength, fGetMaskWithoutPlayer(pOCamera),
            QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Whether an object stands in the way at the spot - the collision part of
    /// CheckIfItemFitsInTile_seg026_1008 (reference: TestIfObjectFitsInTile).
    ///
    /// Collisions are found like in ScanForCollisions: a creature always counts, a lying object
    /// only with a height in COMOBJ.DAT, and they touch as a BOX per axis, |dx| and |dy| at most
    /// the sum of both radii (CreateCollisonRecord_Seg028_2941_A78). If what is hit is a 3D
    /// model (flags bit 1), the item may be put on it; anything else refuses.
    /// </summary>
    private static bool fIsBlockedByObject(Vector3 pOSpot, float pfFeet, int piItemRadius, UWLevelLoader pOLevelLoader)
    {
        DataImport lOData = pOLevelLoader.UWDataImporter;

        if (lOData == null || lOData.CommonObjectProperties == null)
            return false;

        Collider[] lOColliders = Physics.OverlapSphere(new Vector3(pOSpot.x, pfFeet, pOSpot.z),
            UWLevelMeshBuilder.TileSpacing, ~0, QueryTriggerInteraction.Ignore);

        System.Collections.Generic.HashSet<UWEntityInfo> lODone = new System.Collections.Generic.HashSet<UWEntityInfo>();

        foreach (Collider lOCollider in lOColliders)
        {
            UWEntityInfo lOInfo = lOCollider.GetComponentInParent<UWEntityInfo>();

            if (lOInfo == null || lOInfo.ObjectData == null || !lODone.Add(lOInfo))
                continue;

            UWCommonObjectProperties.Entry lOEntry;

            if (!lOData.CommonObjectProperties.TryGet(lOInfo.ObjectData.ID, out lOEntry))
                continue;

            bool lbCritter = lOCollider.GetComponentInParent<UWCritter>() != null;

            if ((!lbCritter && lOEntry.Height == 0) || lOEntry.Is3DModel)
                continue;

            Bounds lOBounds = lOCollider.bounds;

            // Only what reaches into the height the item is put down at.
            if (lOBounds.max.y < pfFeet || lOBounds.min.y > pfFeet + UWLevelMeshBuilder.TileHalfSize)
                continue;

            float lfReach = (piItemRadius + lOEntry.Radius) * UWObjectSpawner.SubTileScale;

            if (Mathf.Abs(lOBounds.center.x - pOSpot.x) <= lfReach && Mathf.Abs(lOBounds.center.z - pOSpot.z) <= lfReach)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the projectile can be placed - approximation of PlaceProjectileInWorld: its tile
    /// must be open and nothing may stand between the player's eyes and the start point.
    /// </summary>
    private static bool fIsStartFree(Transform pOCamera, Vector3 pOStart, UWLevelLoader pOLevelLoader)
    {
        if (!pOLevelLoader.CanDropAt(pOStart, false))
            return false;

        if (pOStart.y <= pOLevelLoader.GetFloorHeightAt(pOStart))
            return false;

        // Also by the tile data - a start behind a diagonal wall, or a path that cuts through
        // the solid half of one, would otherwise put the item into the rock.
        Vector3 lONormal;
        Vector3 lOFeet = GetFeet(pOCamera);

        for (int liSample = 1; liSample <= 4; liSample++)
        {
            Vector3 lOAt = Vector3.Lerp(new Vector3(lOFeet.x, pOStart.y, lOFeet.z), pOStart, liSample / 4f);

            if (pOLevelLoader.IsInsideRock(lOAt, lOFeet, out lONormal))
                return false;
        }

        return !Physics.Linecast(pOCamera.position, pOStart, fGetMaskWithoutPlayer(pOCamera),
            QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// Moves an eighth-tile position by a distance in a heading - GetCoordinateInDirection_seg041_4D.
    ///
    /// The quirk is kept: the table value is divided by 0x80, times the distance, divided by
    /// 0x100 - which loses a little - and then pushed one further AWAY from zero on each axis
    /// that moved at all.
    /// </summary>
    public static void StepInDirection(int piHeading, int piDistance, ref int piX, ref int piY)
    {
        int liAngle = (0x140 - piHeading) & 0xFF;

        int liX = ((fSine(liAngle + 0x40) / 0x80) * piDistance) / 0x100;
        int liY = ((fSine(liAngle) / 0x80) * piDistance) / 0x100;

        if (liX > 0)
            liX++;
        else if (liX < 0)
            liX--;

        if (liY > 0)
            liY++;
        else if (liY < 0)
            liY--;

        piX += liX;
        piY += liY;
    }

    /// <summary>The heading lookup table of the original: a sine over 256 steps, scaled to
    /// 0x7FFF (reference: motion_lookuptables, 0, 804, 1608, ...).</summary>
    private static int fSine(int piStep)
    {
        return Mathf.RoundToInt(Mathf.Sin((piStep & 0xFF) * Mathf.PI * 2f / 256f) * 0x7FFF);
    }

    /// <summary>The player's heading in 256ths of the circle, 0 is north, clockwise - the
    /// object's heading bits times 32 plus its fine heading (seg025_57C).</summary>
    public static int GetPlayerHeading(Transform pOCamera)
    {
        return Mathf.FloorToInt(Mathf.Repeat(pOCamera.eulerAngles.y, 360f) * 256f / 360f) & 0xFF;
    }

    /// <summary>The camera pitch as the original's 16 bit angle, positive is up.</summary>
    private static int fGetOriginalCameraPitch(Transform pOCamera)
    {
        float lfPitch = pOCamera.eulerAngles.x;

        if (lfPitch > 180f)
            lfPitch -= 360f;

        return -(int)(lfPitch * UWViewpoint.FullCircleUnits / 360f);
    }

    /// <summary>Where the player's feet are - the zpos of the player object.</summary>
    public static Vector3 GetFeet(Transform pOCamera)
    {
        CharacterController lOBody = pOCamera.GetComponentInParent<CharacterController>();

        if (lOBody == null)
            return pOCamera.position;

        Bounds lOBounds = lOBody.bounds;

        return new Vector3(lOBounds.center.x, lOBounds.min.y, lOBounds.center.z);
    }

    /// <summary>An eighth-tile position as a world point. One unit into the sub-step, so that
    /// the tile and sub-tile rounding of the level loader lands on exactly this spot and not
    /// on the neighbouring tile at xpos 0.</summary>
    private static Vector3 fEighthToWorld(int piEighthX, int piEighthY, float pfHeight)
    {
        Vector3 lOCorner = UWViewpoint.OriginalToWorld(piEighthX << 5, piEighthY << 5, 0);

        return new Vector3(lOCorner.x + 1f, pfHeight, lOCorner.z + 1f);
    }

    private static int fGetMaskWithoutPlayer(Transform pOCamera)
    {
        CharacterController lOBody = pOCamera.GetComponentInParent<CharacterController>();

        int liMask = ~(1 << pOCamera.gameObject.layer);

        if (lOBody != null)
            liMask &= ~(1 << lOBody.gameObject.layer);

        return liMask;
    }

    private static void fGetSize(DataImport pOData, int piObjectId, out int piRadius, out int piHeight)
    {
        UWCommonObjectProperties.Entry lOEntry;

        if (pOData.CommonObjectProperties != null && pOData.CommonObjectProperties.TryGet(piObjectId, out lOEntry))
        {
            piRadius = lOEntry.Radius;
            piHeight = lOEntry.Height;

            return;
        }

        piRadius = 0;
        piHeight = 0;
    }

    private static bool fIsWeightless(DataImport pOData, int piObjectId)
    {
        return pOData.CommonObjectProperties != null && pOData.CommonObjectProperties.IsWeightless(piObjectId);
    }
}
