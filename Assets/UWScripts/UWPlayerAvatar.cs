using UnityEngine;
using UWDataImport.UWData;

/// <summary>
/// The Unity side of IUWPlayerAvatar (cleanup step 3.1, 2026-10-17): where the player's body
/// stands and looks, read from the view on every call. Owned by UWWorld (see there why it is no
/// component).
///
/// THE VIEW is Camera.main, else the character's transform - the spell host's rule, the widest of
/// the old ones. Both are the same object while playing: UWCharacter sits on the Main Camera of
/// the First Person Controller, and the remote camera and Roaming Sight move only a second camera
/// that carries no MainCamera tag (UWRemoteCamera). So the hosts that asked Camera.main alone (the
/// trap host, the conversation drops) or the character alone (the spell and sleep hosts) or the
/// controller (the shrine; the camera is its child at local x/z 0, only its height changes) get
/// the same tile and cell as before. What they did NOT share is the height, kept apart as
/// FloorSpot and FeetSpot.
/// </summary>
public sealed class UWPlayerAvatar : IUWPlayerAvatar
{
    private readonly UWWorld mOWorld;

    internal UWPlayerAvatar(UWWorld pOWorld)
    {
        mOWorld = pOWorld;
    }

    /// <summary>Camera.main, else the character's transform, else null.</summary>
    private static Transform fView
    {
        get
        {
            Camera lOCamera = Camera.main;

            if (lOCamera != null)
                return lOCamera.transform;

            UWCharacter lOCharacter = UWScene.Character;

            return lOCharacter != null ? lOCharacter.transform : null;
        }
    }

    public bool HasBody => fView != null;

    /// <summary>The cell under the eye at the floor's zpos there, clamped - the trespass trap's
    /// formula (UWTrapHost.AngerRaceNearPlayer, the cell since decision 21 of
    /// Docs/CLEANUP-PLAN.md).</summary>
    public UWSpot FloorSpot
    {
        get
        {
            Transform lOView = fView;

            if (lOView == null || mOWorld == null)
                return UWSpot.Nowhere;

            Vector3 lOAt = lOView.position;

            return UWWorld.SpotAt(lOAt, mOWorld.FloorZPosAt(lOAt));
        }
    }

    /// <summary>The cell of the feet (the bottom of the CharacterController, UWPlayerThrow.GetFeet)
    /// at the feet's zpos: the original's units of the height shifted by three, unclamped - the
    /// sleep host's formula (its PlayerFloorLevel, since 2026-10-17 the sleeper's height in
    /// UWSleepRules, is this spot's FloorLevel).</summary>
    public UWSpot FeetSpot
    {
        get
        {
            if (!TryGetFeetPoint(out Vector3 lOFeet))
                return UWSpot.Nowhere;

            return UWWorld.SpotAt(lOFeet, UWUnits.WorldHeightToOriginal(lOFeet.y) >> 3);
        }
    }

    /// <summary>The feet as a world point - for the drops around the player, which scatter from the
    /// exact point and not from a cell. False without a view.</summary>
    public bool TryGetFeetPoint(out Vector3 pOFeet)
    {
        Transform lOView = fView;

        pOFeet = lOView != null ? UWPlayerThrow.GetFeet(lOView) : Vector3.zero;

        return lOView != null;
    }

    public void GetEyePosition(out float pfX, out float pfZ)
    {
        Transform lOView = fView;

        pfX = lOView != null ? lOView.position.x : 0f;
        pfZ = lOView != null ? lOView.position.z : 0f;
    }

    public void GetViewDirection(out float pfX, out float pfZ)
    {
        Transform lOView = fView;
        Vector3 lOFlat = lOView != null ? new Vector3(lOView.forward.x, 0f, lOView.forward.z) : Vector3.zero;

        if (lOFlat.sqrMagnitude <= 0.0001f)
            lOFlat = Vector3.forward;

        lOFlat.Normalize();

        pfX = lOFlat.x;
        pfZ = lOFlat.z;
    }

    public float ViewYawDegrees
    {
        get
        {
            Transform lOView = fView;

            return lOView != null ? lOView.eulerAngles.y : 0f;
        }
    }

    // ------------------------------------------------- The motion state (cleanup step 3.6)

    /// <summary>The movement on the character's parent: UWCharacter sits on the Main Camera, a
    /// child of the controller with UWPlayerMovement - GetComponent found nothing until 2026-10-13.
    /// Moved from the sleep's host (2026-10-17), which asked the character it was handed - the
    /// scene's one character.</summary>
    private static UWPlayerMovement fMovement()
    {
        UWCharacter lOCharacter = UWScene.Character;

        return lOCharacter != null ? lOCharacter.GetComponentInParent<UWPlayerMovement>() : null;
    }

    /// <summary>
    /// The original's MS_SWIM: the motion core's swimming state, which it takes in water and
    /// at the water's edge unless the player walks on water (ProcessPlayerTileState). Without
    /// the core, the terrain's swimming. Until 2026-10-15 the water contact alone, so the shore
    /// counted as dry (audit round 2, A31). The sleep host's IsInWater until 2026-10-17.
    /// </summary>
    public bool IsSwimming
    {
        get
        {
            UWPlayerMovement lOMovement = fMovement();

            if (lOMovement != null && lOMovement.UsesMotionCore)
                return lOMovement.Motion.CurrentState == UWPlayerMotion.State.Swimming;

            UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

            return lOTerrain != null && lOTerrain.IsSwimming;
        }
    }

    /// <summary>The original's MS_LAVA: the core's lava state - standing on it, not floating
    /// above it.</summary>
    public bool IsOnLava
    {
        get
        {
            UWPlayerMovement lOMovement = fMovement();

            if (lOMovement != null && lOMovement.UsesMotionCore)
                return lOMovement.Motion.CurrentState == UWPlayerMotion.State.Lava;

            UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

            return lOTerrain != null && lOTerrain.IsOnLava;
        }
    }

    public bool IsOnGround
    {
        get
        {
            // See fMovement: the movement sits on the parent (until 2026-10-13 never found,
            // so the sleeper always counted as standing).
            UWPlayerMovement lOMovement = fMovement();

            return lOMovement == null || (!lOMovement.IsJumping && lOMovement.IsWalkingOnGround);
        }
    }

    // ------------------------------------------------- What he carries (cleanup step 3.6)

    public UWInventoryModel Inventory
    {
        get
        {
            UWInventory lOInventory = UWScene.Inventory;

            return lOInventory != null ? lOInventory.Model : null;
        }
    }

    public UWPlayerVitals Vitals
    {
        get
        {
            UWCharacter lOCharacter = UWScene.Character;

            return lOCharacter != null ? lOCharacter.Vitals : null;
        }
    }

    /// <summary>UWCharacter.DamageTypeProof (the status pass), 0 without a character - the trap
    /// host's PlayerDamageTypeProof until cleanup step 3.6 (2026-10-17).</summary>
    public int DamageTypeProof
    {
        get
        {
            UWCharacter lOCharacter = UWScene.Character;

            return lOCharacter != null ? lOCharacter.DamageTypeProof : 0;
        }
    }

    /// <summary>The quake trap's second bit (the trap host's BouncePlayer until cleanup step 3.6,
    /// 2026-10-17). The movement sits on the character's parent (until 2026-10-13 never found
    /// here, so the quake never threw the player up).</summary>
    public void BounceUp(int piIntensity)
    {
        UWPlayerMovement lOMovement = fMovement();

        if (lOMovement != null)
            lOMovement.BounceUp(piIntensity);
    }

    /// <summary>The golem's stand-down (the special deaths' host until cleanup step 3.6,
    /// 2026-10-17): the scene's Interaction leaves combat mode.</summary>
    public void LeaveCombatMode()
    {
        Interaction lOInteraction = UWScene.Interaction;

        if (lOInteraction != null)
            lOInteraction.IsCombatModeActive = false;
    }
}
