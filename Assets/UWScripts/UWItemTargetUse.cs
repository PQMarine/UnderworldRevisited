using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// AN ITEM APPLIED TO SOMETHING - the one applier of UWItemTargetRules (cleanup step 2.4,
/// 2026-10-12). Until then the dispatch was written in UWItemDrag (fTryUseItemOnTarget, the
/// world three-liner "reach - use - refusal" three times, the anvil and oil branches twice) and
/// the actions lived in Interaction; now the classic use mode, the modern bags' use mode and
/// the action bar's tools all come here, and the actions with them.
///
/// THE REFUSALS are each tool's own, as the original's (UWItemTargetRules, decided 2026-10-15),
/// on a carried thing as well; the generic "The ... cannot be used on that."
/// (UWItemMessages.CannotBeUsedOnThat) is left for an item that asks for no target at all. If
/// the pointer hits no thing at all (wall, floor, ceiling), nothing happens - not
/// even a refusal (confirmed by user for pole AND key). Picking up via the use mode is
/// deliberately not supported - in the original the pole cannot pick anything up (checked by
/// user).
/// </summary>
public static class UWItemTargetUse
{
    /// <summary>
    /// The use mode's application in both schemes: the held item onto the carried thing
    /// (pOCarriedTarget) or onto what lies under a screen point in the world (pOWorldPoint).
    /// The anvil and the oil flask act only on a carried thing; every other item acts on the
    /// world and answers a carried thing with its own refusal (UWItemTargetRules.OnTarget,
    /// 2026-10-15). Returns true when the use mode stays (UWItemTargetRules.KeepsUseMode: the
    /// pole on a carried thing); the caller ends it otherwise.
    /// </summary>
    public static bool Apply(Interaction pOInteraction, UWObject pOItem, UWObject pOCarriedTarget, Vector2? pOWorldPoint)
    {
        if (pOInteraction == null || pOItem == null)
            return false;

        if (pOCarriedTarget != null || UWItemTargetRules.TakesCarriedTarget(pOItem.ID))
            return OnCarried(pOInteraction, pOItem, pOCarriedTarget);

        if (pOWorldPoint.HasValue)
            OnWorld(pOInteraction, pOItem, pOInteraction.TryGetEntityAt(pOWorldPoint.Value, pOItem));

        return false;
    }

    /// <summary>
    /// The action bar's tools act on THE CROSSHAIR'S TARGET (decided per user, 2026-10-03): the
    /// use mode's world branch with the crosshair for the pointer. Out of reach the reach rules
    /// refuse; on nothing at all nothing happens, as there. Not while a spell waits for its
    /// target.
    /// </summary>
    public static void OnCrosshair(Interaction pOInteraction, UWObject pOItem)
    {
        if (pOInteraction == null || pOItem == null || pOInteraction.IsSpellTargeting)
            return;

        OnWorld(pOInteraction, pOItem, pOInteraction.TryGetEntityAtCrosshair(pOItem));
    }

    /// <summary>
    /// The held item onto a thing in the world. Out of reach the refusal of the reach rules
    /// comes first - it beats the one about the wrong target; the pole's reach counts for the
    /// pole (TryReachTarget). IT IS THE TARGET MODE'S "You cannot reach that." (mous_in_3d,
    /// UnderworldExhumed INTERACT.C: game_sprint 0x5E, as for a target spell); until 2026-10-13
    /// ours printed the plain use's "You are unable to use that from here." (reference audit,
    /// finding of the fix agents).
    /// </summary>
    public static void OnWorld(Interaction pOInteraction, UWObject pOItem, UWEntityInfo pOTarget)
    {
        if (pOInteraction == null || pOItem == null || pOTarget == null)
            return;

        if (!pOInteraction.TryReachTarget(pOTarget, pOItem, UWReachRules.CannotReachMessage))
            return;

        // THE POLE ON ITS TARGET is a second status pass (UsePoleOn, USEITEMS.C: FixPlayerEquips
        // before it looks at the target), switch or not (2026-10-13).
        if (pOItem.ID == UWObjectMechanics.PoleObjectId)
            UWScene.Character?.OnStatusPassDue();

        int liTargetId = pOTarget.ObjectData != null ? pOTarget.ObjectData.ID : -1;
        UWItemTargetRules.Action leAction = UWItemTargetRules.OnTarget(pOItem,
            UWItemTargetRules.SortOf(liTargetId, pOTarget.IsDoor));

        if (!fApplyToWorld(pOInteraction, pOItem, pOTarget, leAction))
            fRefuse(pOInteraction, pOItem);
    }

    /// <summary>
    /// The held item onto a carried thing: the anvil repairs it (a question first), the oil
    /// flask fills or makes a torch; a world tool answers with its own refusal, or a key or the
    /// lockpick works a lock the thing holds (UWItemTargetRules.OnTarget). Nothing without a
    /// target. Returns true when the use mode stays (the pole).
    /// </summary>
    public static bool OnCarried(Interaction pOInteraction, UWObject pOItem, UWObject pOTarget)
    {
        if (pOInteraction == null || pOItem == null || pOTarget == null)
            return false;

        UWItemTargetRules.Action leAction = UWItemTargetRules.OnTarget(pOItem, UWItemTargetRules.TargetSort.Carried);

        switch (leAction)
        {
            case UWItemTargetRules.Action.Repair:
                pOInteraction.TryRepairItem(pOTarget);
                break;

            // ANYTHING ELSE: "You cannot use oil on that." - UseOilOn's own refusal (per user
            // 2026-10-12, as the original; until then "The oil flask cannot be used on that.").
            case UWItemTargetRules.Action.Oil:
                if (!fTryOilItem(pOInteraction, pOTarget, pOItem))
                    pOInteraction.AddGeneralMessage(UWStringIds.CannotUseOilMessage);

                break;

            case UWItemTargetRules.Action.NoEffect:
                pOInteraction.AddGeneralMessage(UWStringIds.NoEffectMessage);
                break;

            // THE POLE: UsePoleOn makes its status pass (FixPlayerEquips) and refuses anything but
            // a switch; the pole stays on the pointer.
            case UWItemTargetRules.Action.PoleOnCarried:
                UWScene.Character?.OnStatusPassDue();
                pOInteraction.AddGeneralMessage(UWItemTargetRules.PoleCannotBeUsedMessage);
                break;

            case UWItemTargetRules.Action.Spike:
                pOInteraction.AddGeneralMessage(UWItemApplications.SpikeDoor(pOTarget, false).MessageIndex);
                break;

            case UWItemTargetRules.Action.PickLock:
                fPickLock(pOInteraction, null, pOTarget, UWLockRules.FindLoadedLock(pOTarget));
                break;

            case UWItemTargetRules.Action.ToggleLock:
                fToggleLock(pOInteraction, null, pOTarget, UWLockRules.FindLoadedLock(pOTarget), false, pOItem);
                break;
        }

        return UWItemTargetRules.KeepsUseMode(leAction);
    }

    private static void fRefuse(Interaction pOInteraction, UWObject pOItem)
    {
        pOInteraction.AddMessage(UWItemMessages.CannotBeUsedOnThat(pOInteraction.GameData, pOItem.ID));
    }

    /// <summary>Carries the action out on a world target. False when the combination does not
    /// work at all - the caller then refuses.</summary>
    private static bool fApplyToWorld(Interaction pOInteraction, UWObject pOItem, UWEntityInfo pOTarget,
        UWItemTargetRules.Action peAction)
    {
        switch (peAction)
        {
            case UWItemTargetRules.Action.Ignore:
                return true;

            case UWItemTargetRules.Action.FireSwitch:
                // The pole: tap a switch from the pole's reach - the same effect as a normal use
                // without item. The item itself plays no role, all that counts is that something
                // reaches the switch at all. Only a switch of class 0x17 (UsePoleOn: OBJ_CLASS ==
                // CLASS_SWITCH; until 2026-10-15 anything with a use trigger, a door too);
                // anything else gets UsePoleOn's own 0x9E. A switch gets 0x9D before it is used,
                // whatever the use does.
                if (pOTarget.ObjectData == null
                    || pOTarget.ObjectData.GetCategory() != UWObject.ObjectCategoryEnum.Switches)
                {
                    pOInteraction.AddGeneralMessage(UWItemTargetRules.PoleCannotBeUsedMessage);
                    return true;
                }

                pOInteraction.AddGeneralMessage(UWItemTargetRules.PoleTriggersSwitchMessage);
                UWTriggerSystem.TryFireSwitch(pOTarget.ObjectData, pOInteraction.LevelLoader, pOInteraction);
                return true;

            case UWItemTargetRules.Action.PickLock:
                fTryPickLock(pOInteraction, pOTarget, pOItem);
                return true;

            case UWItemTargetRules.Action.KeyOfInfinity:
                return fTryKeyOfInfinity(pOInteraction, pOTarget, pOItem);

            // Bones ask for a target and report themselves - at the gravestone they are buried,
            // Garamon's bones at his grave set the endgame in motion (see UWEndgame).
            case UWItemTargetRules.Action.Bones:
                return UWEndgame.UseBones(pOItem, pOTarget, pOInteraction, pOInteraction.Inventory,
                    UWScene.LevelLoader);

            case UWItemTargetRules.Action.Spike:
                return fTrySpikeDoor(pOInteraction, pOTarget, pOItem);

            case UWItemTargetRules.Action.BreakRock:
                return fTryBreakRock(pOInteraction, pOTarget);

            case UWItemTargetRules.Action.OrbRock:
                return fTryUseOrbRock(pOInteraction, pOTarget, pOItem);

            case UWItemTargetRules.Action.ToggleLock:
                fTryToggleDoorLockWithKey(pOInteraction, pOTarget, pOItem);
                return true;

            default:
                return false;
        }
    }

    // ------------------------------------------------- The actions

    // The messages and the rules of spike, oil flask and rock hammer are in
    // UWItemApplications (P3, 2026-09-18).

    /// <summary>
    /// The spike: spikes a closed door.
    ///
    /// It is used up in the process. An OPEN door cannot be spiked - there is
    /// a separate message for that. What the spike does is described in
    /// UWObjectMechanics.IsDoorSpiked: the door no longer opens, and whoever wants to
    /// break it open hits the spike first.
    ///
    /// Anything but a door gets the same "You can only spike closed doors." (SpikeDoor_seg040_662;
    /// until 2026-10-15 "The spike cannot be used on that.").
    /// </summary>
    private static bool fTrySpikeDoor(Interaction pOInteraction, UWEntityInfo pOTarget, UWObject pOSpike)
    {
        if (pOTarget == null || pOTarget.ObjectData == null)
            return false;

        UWDoor lODoor = pOTarget.IsDoor ? pOTarget.GetComponentInParent<UWDoor>() : null;

        UWItemApplications.Result lOResult = UWItemApplications.SpikeDoor(pOTarget.ObjectData,
            pOTarget.IsDoor && (lODoor == null || lODoor.IsClosed));

        if (lOResult.MessageIndex >= 0)
            pOInteraction.AddGeneralMessage(lOResult.MessageIndex);

        if (lOResult.ItemUsed && pOInteraction.Inventory != null)
            pOInteraction.Inventory.TryConsumeOne(pOSpike);

        return true;
    }

    /// <summary>
    /// The orb rock on Tyball's orb (TybalsOrb_seg040_A12): the orb is destroyed, a class-7
    /// picture stays where it stood, and the rock goes with it. On anything else the rock only
    /// says that it seems to have no effect, and it survives that.
    /// </summary>
    private static bool fTryUseOrbRock(Interaction pOInteraction, UWEntityInfo pOTarget, UWObject pORock)
    {
        if (pOTarget == null || pOTarget.ObjectData == null || pOInteraction.LevelLoader == null)
            return false;

        UWItemApplications.Result lOResult = UWItemApplications.UseOrbRock(pOTarget.ObjectData);

        if (lOResult.TargetChanged)
            fBreakOrb(pOInteraction.LevelLoader, pOTarget);

        if (lOResult.ItemUsed && pOInteraction.Inventory != null && pORock != null)
            pOInteraction.Inventory.RemoveItem(pORock);

        pOInteraction.AddGeneralMessage(lOResult.MessageIndex);

        return true;
    }

    /// <summary>
    /// The orb rock THROWN at Tybal's orb (UWProjectileFlight, the collision's object use):
    /// the same breaking as from the hand, but the rock is not used up - it lies where it fell,
    /// as in the original (per user, 2026-09-23). A throw at anything else says nothing.
    /// </summary>
    public static void SmashOrb(Interaction pOInteraction, UWEntityInfo pOOrb)
    {
        if (pOInteraction == null || pOOrb == null || pOOrb.ObjectData == null || pOInteraction.LevelLoader == null
            || UWGameFlags.OrbDestroyed)
            return;

        UWItemApplications.Result lOResult = UWItemApplications.UseOrbRock(pOOrb.ObjectData);

        if (!lOResult.TargetChanged)
            return;

        fBreakOrb(pOInteraction.LevelLoader, pOOrb);
        pOInteraction.AddGeneralMessage(lOResult.MessageIndex);
    }

    /// <summary>The orb goes, its picture stays for a moment, and the bit and the maximum mana
    /// follow (UWTybalOrbRules).</summary>
    private static void fBreakOrb(UWLevelLoader pOLevelLoader, UWEntityInfo pOOrb)
    {
        // ONCE ONLY. The orb's body goes at the end of the frame, and a thrown rock sweeps it
        // again in the next frames - without this guard Tybal was halved two or three times
        // over and fell to one weak blow (per user, 2026-09-24).
        if (UWGameFlags.OrbDestroyed)
            return;

        Vector3 lOWhere = pOOrb.transform.position;

        pOLevelLoader.RemoveObjectFromWorld(pOOrb.ObjectData);
        pOLevelLoader.SpawnEffectAt(UWItemApplications.OrbEffectObjectId, lOWhere,
            UWLevelLoader.SplashEffectSeconds);

        if (UWScene.Character != null)
            UWTybalOrbRules.OnOrbDestroyed(UWScene.Character.Vitals);

        // And Tybal, wherever he stands on this level, loses half his strength for good.
        UWCritter[] lOCritters = UWScene.FindCritters();

        for (int liAt = 0; lOCritters != null && liAt < lOCritters.Length; liAt++)
        {
            if (lOCritters[liAt] != null && lOCritters[liAt].IsTybal)
                lOCritters[liAt].WeakenByBrokenOrb();
        }
    }

    private static bool fTryBreakRock(Interaction pOInteraction, UWEntityInfo pOTarget)
    {
        UWLevelLoader lOLevelLoader = pOInteraction.LevelLoader;

        if (pOTarget == null || pOTarget.ObjectData == null || lOLevelLoader == null)
            return false;

        UWObject lOBoulder = pOTarget.ObjectData;
        UWItemApplications.RockResult lOResult = UWItemApplications.BreakRock(lOBoulder.ID);

        if (lOResult.Broken)
        {
            UWTilePos lOTile = new UWTilePos(lOBoulder.TileX, lOBoulder.TileY);
            Vector3 lOSpot = pOTarget.transform.position;

            // THE PIECES ARE THE BOULDER'S COPIES: UseRockHammerOn (UnderworldExhumed USEITEMS.C,
            // read 2026-10-12 and 2026-10-15) makes each with CreateObj and then copies the
            // boulder's whole record over it before setting the smaller id, so they carry the
            // boulder's quality, owner and flags (and a sling-stone pile its 3 to 8), and put_at
            // scatters each by 6 eighths around the boulder's spot. Until 2026-10-12 ours gave
            // them 0x3F, "freshly broken is fresh"; until 2026-10-15 one piece in the tile centre.
            for (int liAt = 0; liAt < lOResult.Pieces.Length; liAt++)
            {
                UWObject lOPiece = UWObject.Clone(lOBoulder);

                lOPiece.Link = 0;
                UWObjectMechanics.SetObjectId(lOPiece, lOResult.Pieces[liAt].ObjectId, pOInteraction.GameData?.Textures);

                if (lOResult.Pieces[liAt].Quantity > 0)
                {
                    lOPiece.HasQuantity = true;
                    lOPiece.Quantity = (ushort)lOResult.Pieces[liAt].Quantity;
                }

                // EACH PIECE MUST FIT AS put_at TESTS IT, with the boulder still lying there:
                // drop_around_place asks can_place against every solid object (the boulder, the
                // pieces put down before, the player), and a piece that finds no spot in 24 tries
                // goes to the boulder's spot unless the culling test takes it. The original removes
                // the boulder only after the loop (Obj_Punt). Per user on the original 2026-10-15:
                // often no piece at all, depending on the room; ours, with only the tile test and
                // the boulder already gone, left all of them (A35 of audit round 2).
                lOLevelLoader.SpawnScatteredAround(lOPiece, lOSpot, UWItemApplications.RockPieceScatter, lOTile,
                    pbFitsLikeCanPlace: true, piZ: lOBoulder.ZPos);
            }

            lOLevelLoader.RemoveObjectFromWorld(lOBoulder);
        }

        pOInteraction.AddGeneralMessage(lOResult.MessageIndex);

        return true;
    }

    /// <summary>
    /// The oil flask: turns a piece of wood into a torch, or refills torch and
    /// lantern.
    ///
    /// It does not work ON A BURNING light source - there is a separate warning for that.
    /// An unlit one gets 32 fuller, at most up to 63; if it is already full,
    /// it says so.
    ///
    /// The flask itself is used up by each of these applications.
    ///
    /// DEVIATION FROM THE REFERENCE, BUT DELIBERATE: it picks the message group with
    /// "item_id == 0x90 or 94" - the second number is decimal and makes no sense,
    /// obviously the lit lantern 0x94 is meant. With its version one would get
    /// the sentence about the torch when putting oil on a lit lantern.
    /// </summary>
    private static bool fTryOilItem(Interaction pOInteraction, UWObject pOTarget, UWObject pOFlask)
    {
        UWInventory lOInventory = pOInteraction.Inventory;
        DataImport lOData = pOInteraction.GameData;

        if (pOTarget == null || lOInventory == null || lOData == null)
            return false;

        UWItemApplications.Result lOResult = UWItemApplications.ApplyOil(pOTarget, lOData);

        if (!lOResult.Handled)
            return false;

        if (lOResult.MessageIndex >= 0)
            pOInteraction.AddGeneralMessage(lOResult.MessageIndex);

        if (lOResult.ItemUsed)
            lOInventory.TryConsumeOne(pOFlask);

        if (lOResult.TargetChanged)
            lOInventory.NotifyChanged();

        return true;
    }

    /// <summary>
    /// The Key of Infinity (seg040_352B_AFF, USEITEMS.C, read 2026-10-12): it works only on a wall
    /// panel (0x16E) whose texture is of the terrain type "textured door" (TERRAIN.DAT 0x0B,
    /// w64_types) - there the key is used up and the panel's open triggers fire (checkTrap kind 7);
    /// on anything else "It seems to have no effect.". Per user 2026-10-12, as the original; until
    /// then it fired an open trigger on whatever it touched and was kept. Always true: the original
    /// answers every target itself, the generic refusal never follows.
    /// </summary>
    private static bool fTryKeyOfInfinity(Interaction pOInteraction, UWEntityInfo pOTarget, UWObject pOKey)
    {
        UWObject lOTarget = pOTarget != null ? pOTarget.ObjectData : null;
        DataImport lOData = pOInteraction.GameData;

        bool lbDoorPanel = lOTarget != null && lOTarget.ID == UWObjectIds.WallPanel && lOTarget.Texture != null
            && lOData != null && lOData.Terrain != null && lOData.Terrain.IsLoaded
            && lOData.Terrain.GetWallTerrain(lOTarget.Texture.Index) == UWTerrain.TerrainType.TexturedDoor;

        if (!lbDoorPanel)
        {
            pOInteraction.AddGeneralMessage(UWStringIds.NoEffectMessage);
            return true;
        }

        if (pOInteraction.Inventory != null)
            pOInteraction.Inventory.TryConsumeOne(pOKey);

        UWTriggerSystem.TryFireOpenTrigger(lOTarget, pOInteraction.LevelLoader, pOInteraction, UWTrapRules.Setter.Player);

        return true;
    }

    /// <summary>
    /// Pick a lock with the lockpick - the rule is UWLockRules.PickLock, after the original
    /// (read 2026-09-25): the Picklock skill against three times the lock difficulty, the door
    /// stays shut, the pick is never lost, and a lock without its keep bit is gone afterwards.
    /// </summary>
    private static void fTryPickLock(Interaction pOInteraction, UWEntityInfo pOTarget, UWObject pOLockpick)
    {
        UWLevelLoader lOLevelLoader = pOInteraction.LevelLoader;

        if (pOTarget == null || pOLockpick == null || lOLevelLoader == null
            || lOLevelLoader.CurrentLevel == null)
            return;

        // A BARREL OR CHEST is picked like a door: its lock sits in its own chain (see
        // Interaction.SpillWorldContainer), it just has no UWDoor component (per user,
        // 2026-09-16: the lockpick could not be applied to the chest at all). One lookup for key,
        // pick and spell (UWDoor.ResolveLockTarget, 2026-10-12).
        // ANY OTHER THING is looked into the same way: without a lock it answers "There is no lock
        // on that." (checkLock result 1; 2026-10-15).
        UWDoor.ResolveLockTarget(pOTarget, lOLevelLoader.CurrentLevel,
            out UWLockState lODoorLock, out UWObject lOContainerLock);

        fPickLock(pOInteraction, lODoorLock, lOContainerLock != null ? pOTarget.ObjectData : null, lOContainerLock);
    }

    /// <summary>The lockpick on a resolved lock - a door's, or the one a thing holds in its chain,
    /// in the world or carried. The checks and the messages are in UWLockRules (P3, 2026-09-18).</summary>
    private static void fPickLock(Interaction pOInteraction, UWLockState pODoorLock, UWObject pOContainer,
        UWObject pOContainerLock)
    {
        UWLockRules.PickResult lOResult = UWLockRules.PickLock(pODoorLock, pOContainer, pOContainerLock,
            pOInteraction.Character != null ? pOInteraction.Character.GetSkill(UWPlayerData.Skill.Picklock) : 0);

        if (lOResult.Unlocked)
            UWSoundEffects.PlayAtAvatar(UWSoundEffects.Lockpick);

        pOInteraction.AddGeneralMessage(lOResult.MessageIndex);
    }

    /// <summary>The key on a door, a barrel or a chest. Toggles ONLY the lock state (unlocks a
    /// locked door, locks an unlocked one again - confirmed by user) and deliberately does NOT
    /// start the door moving - unlocking does not open it. Messages are the real original
    /// strings (confirmed by user, 2026-08-27) - unlike a click without a door target (e.g. a
    /// wall) every case HERE (including a failure) shows a message, not only a success.</summary>
    private static void fTryToggleDoorLockWithKey(Interaction pOInteraction, UWEntityInfo pOTarget, UWObject pOKey)
    {
        if (pOTarget == null || pOKey == null || pOKey.GetCategory() != UWObject.ObjectCategoryEnum.KeysLockpickLock)
            return;

        UWLevelLoader lOLevelLoader = pOInteraction.LevelLoader;

        if (lOLevelLoader == null || lOLevelLoader.CurrentLevel == null)
            return;

        // A BARREL OR CHEST has no UWDoor, its lock hangs in its own chain - exactly as with
        // the lockpick (see fTryPickLock). Without this the key worked on doors only.
        UWDoor.ResolveLockTarget(pOTarget, lOLevelLoader.CurrentLevel,
            out UWLockState lODoorLock, out UWObject lOContainerLock);

        // An open door answers "That is already open." as in the original (UseKey, read
        // 2026-09-25); any other thing without a lock "There is no lock on that." (2026-10-15).
        UWDoor lODoor = pOTarget.GetComponentInParent<UWDoor>();

        fToggleLock(pOInteraction, lODoorLock, lOContainerLock != null ? pOTarget.ObjectData : null, lOContainerLock,
            lODoor != null && !lODoor.IsClosed, pOKey);
    }

    /// <summary>The key on a resolved lock, in the world or carried. The rule and the messages
    /// are in UWLockRules (P3, 2026-09-18).</summary>
    private static void fToggleLock(Interaction pOInteraction, UWLockState pODoorLock, UWObject pOContainer,
        UWObject pOContainerLock, bool pbTargetIsOpen, UWObject pOKey)
    {
        int liMessage = UWLockRules.ToggleWithKey(pODoorLock, pOContainer, pOContainerLock, pbTargetIsOpen, pOKey);

        if (liMessage >= 0)
            pOInteraction.AddGeneralMessage(liMessage);
    }
}
