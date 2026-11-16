using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// KEYS AND LOCKPICKS IN THE SECOND GAME (UW2 plan G4 part A, 2026-11-13): UseKey (uw2 obj/OBJUSE.C) asks "Use &lt;name&gt;
/// on what?" and waits on the pointer (UseThing, UWAttackUw2.BeginUseOn); the next click hands the thing to UseKeyOn or
/// UseLockpickOn (obj/USEITEMS.C). The rules are the shared lock rules (UWLockRules.ToggleWithKey, PickLockSecondGame),
/// the lock found as the first game finds it (UWDoor.ResolveLockTarget in the world, UWLockRules.FindLoadedLock carried).
/// An unlock sets off the thing's unlock triggers (checkTrap 0xB) before the line, as checkLock does.
/// </summary>
public static class UWLockUseUw2
{
    /// <summary>The lock itself (ITEM_LOCK): UseKey leaves it alone.</summary>
    private const int FirstKeysId = 0x100;

    /// <summary>play_effect_here(0x13) on a picked lock.</summary>
    private const int PickedSound = 0x13;

    /// <summary>A key or a lockpick, the things UseKey sends to a lock (0x100 to 0x10E).</summary>
    public static bool IsKeyOrLockpick(UWObject pOItem)
    {
        return pOItem != null && pOItem.ID >= FirstKeysId && pOItem.ID < UWObjectMechanics.LockObjectId;
    }

    /// <summary>UseKey from the inventory: the line and the waiting pointer. False without the attack host.</summary>
    public static bool Begin(UWObject pOItem, DataImport pOData, UWInventoryModel pOInventory)
    {
        UWAttackUw2 lOAttack = UWAttackUw2.Current;

        if (lOAttack == null || !IsKeyOrLockpick(pOItem))
            return false;

        lOAttack.AddMessage(UWItemMessages.UseOnWhat(pOData, pOItem.ID));

        if (pOItem.ID == UWObjectMechanics.LockpickObjectId)
            lOAttack.BeginUseOn(pOItem, (pOTarget, pOEntity, pbCarried) => fPick(pOItem, pOTarget, pOEntity, pbCarried, pOInventory));
        else
            lOAttack.BeginUseOn(pOItem, (pOTarget, pOEntity, pbCarried) => fKey(pOItem, pOTarget, pOEntity, pbCarried));

        return true;
    }

    /// <summary>The lock of the thing: a door's, or the lock object in a thing's chain (world or carried).</summary>
    private static void fResolve(UWObject pOTarget, UWEntityInfo pOEntity, bool pbCarried, out UWLockState pODoorLock,
        out UWObject pOContainerLock, out UWDoor pODoor)
    {
        pODoorLock = null;
        pODoor = null;

        if (pbCarried || pOEntity == null)
        {
            pOContainerLock = UWLockRules.FindLoadedLock(pOTarget);

            return;
        }

        UWLevelLoader lOLoader = UWScene.LevelLoader;

        UWDoor.ResolveLockTarget(pOEntity, lOLoader != null ? lOLoader.CurrentLevel : null, out pODoorLock, out pOContainerLock);
        pODoor = pOEntity.GetComponentInParent<UWDoor>();
    }

    /// <summary>UseKeyOn: checkLock with the key's owner, game_sprint(result + 2).</summary>
    private static void fKey(UWObject pOKey, UWObject pOTarget, UWEntityInfo pOEntity, bool pbCarried)
    {
        UWAttackUw2 lOAttack = UWAttackUw2.Current;

        if (lOAttack == null || pOTarget == null)
            return;

        fResolve(pOTarget, pOEntity, pbCarried, out UWLockState lODoorLock, out UWObject lOContainerLock, out UWDoor lODoor);

        bool lbOpen = lODoor != null ? !lODoor.IsClosed : UWLockRules.IsOpenForKeySecondGame(pOTarget);
        int liMessage = UWLockRules.ToggleWithKey(lODoorLock, lOContainerLock != null ? pOTarget : null, lOContainerLock, lbOpen, pOKey);

        if (liMessage < 0)
            return;

        if (liMessage == UWLockRules.FirstKeyMessage + (int)UWLockRules.Outcome.Unlocked)
            fUnlockTriggers(pOTarget);

        lOAttack.AddGeneralMessage(liMessage);
    }

    /// <summary>UseLockpickOn: Picklock + 1 against the lock, a fumble breaks the pick unless Dexterity saves it.</summary>
    private static void fPick(UWObject pOPick, UWObject pOTarget, UWEntityInfo pOEntity, bool pbCarried, UWInventoryModel pOInventory)
    {
        UWAttackUw2 lOAttack = UWAttackUw2.Current;
        UWPlayerVitals lOVitals = UWVitalsUw2.Current != null ? UWVitalsUw2.Current.Vitals : null;

        if (lOAttack == null || pOTarget == null || lOVitals == null)
            return;

        fResolve(pOTarget, pOEntity, pbCarried, out UWLockState lODoorLock, out UWObject lOContainerLock, out UWDoor _);

        UWLockRules.PickResult lOResult = UWLockRules.PickLockSecondGame(lODoorLock, lOContainerLock != null ? pOTarget : null,
            lOContainerLock, lOVitals.GetSkill(UWPlayerData.Skill.Picklock), lOVitals.Dexterity);

        if (lOResult.Unlocked)
        {
            fUnlockTriggers(pOTarget);
            UWAudioService.PlayEffect(PickedSound);
        }

        // using_punt(ObjectActing, 1, 1): the broken pick is used up, one off a stack.
        if (lOResult.Broke && pOInventory != null)
        {
            if (UWStackRules.TryTakeOne(pOPick))
                pOInventory.NotifyChanged();
            else
                pOInventory.RemoveItem(pOPick);
        }

        lOAttack.AddGeneralMessage(lOResult.MessageIndex);
    }

    /// <summary>checkLock's checkTrap(who, door, 0xB) on an unlock.</summary>
    private static void fUnlockTriggers(UWObject pOTarget)
    {
        UWTriggerSystem.CheckTrapUw2(pOTarget, UWTrapRulesUw2.UnlockMode, pOTarget.TileX, pOTarget.TileY, UWScene.LevelLoader);
    }
}
