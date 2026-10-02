using UWDataImport.UWData;

/// <summary>
/// Spell class 11 on the Unity side. The casting itself is engine-free since 2026-09-18
/// (UWMiscSpellRules, P3 of the engine separation); what stays here are the three spells
/// that go onto a CLICKED target - Unlock, Name Enchantment, Remove Trap - because they are
/// nothing but calls on the door lock and the Interaction. The constants the character and
/// the camera read are forwarded so their call sites stay as they were.
/// </summary>
public static class UWMiscSpell
{
    /// <summary>See UWMiscSpellRules.FreezeTimeEffectMinor.</summary>
    public const int FreezeTimeEffectMinor = UWMiscSpellRules.FreezeTimeEffectMinor;

    public const int RoamingSightEffectMinor = UWMiscSpellRules.RoamingSightEffectMinor;

    public const int SpeedEffectMinor = UWMiscSpellRules.SpeedEffectMinor;

    public const int TelekinesisEffectMinor = UWMiscSpellRules.TelekinesisEffectMinor;

    /// <summary>Spell class 11.</summary>
    public const int MajorClass = UWMiscSpellRules.MajorClass;

    // ------------------------------------------------- Class 11 target spells

    /// <summary>
    /// A class 11 spell that goes onto a clicked target (see
    /// Interaction.fApplyTargetSpell): Unlock, Name Enchantment and Remove Trap.
    ///
    /// Returns false if the click hit nothing - the caller then reports "Nothing
    /// happens.". For Unlock, if it hit something, this branch outputs its own message, just
    /// like the reference: it says either "The spell unlocks the lock." or "That is not
    /// locked.", never both and never nothing.
    /// </summary>
    public static bool CastOnTarget(int piMinorClass, UWEntityInfo pOTarget,
        Interaction pOInteraction, UWLevelLoader pOLevelLoader)
    {
        if (pOTarget == null || pOInteraction == null)
            return false;

        switch (piMinorClass)
        {
            case UWMiscSpellRules.UnlockSpell:
                return fUnlock(pOTarget, pOInteraction, pOLevelLoader);

            case UWMiscSpellRules.NameEnchantmentSpell:
                return fNameEnchantment(pOTarget, pOInteraction);

            case UWMiscSpellRules.RemoveTrapSpell:
                return pOInteraction.TryRemoveTrapWithSpell(pOTarget);

            default:
                return false;
        }
    }

    /// <summary>
    /// A class 11 target spell on an item in the inventory - see
    /// Interaction.ResolveTargetSpellOnItem.
    ///
    /// So far exactly one: Name Enchantment. Unlock needs a door and finds nothing
    /// matching in the backpack.
    /// </summary>
    public static bool CastOnItem(int piMinorClass, UWObject pOItem, Interaction pOInteraction)
    {
        if (pOItem == null || pOInteraction == null)
            return false;

        return piMinorClass == UWMiscSpellRules.NameEnchantmentSpell && pOInteraction.IdentifyItem(pOItem);
    }

    /// <summary>
    /// Name Enchantment: the item is identified from now on, and the description comes
    /// along right away (see UWLoreCheck).
    ///
    /// This applies in the world as in the backpack - the spell explicitly also works on
    /// carried items (per user, 2026-09-10). CastOnItem takes the path there.
    /// </summary>
    private static bool fNameEnchantment(UWEntityInfo pOTarget, Interaction pOInteraction)
    {
        return pOTarget.ObjectData != null && pOInteraction.IdentifyItem(pOTarget.ObjectData);
    }

    /// <summary>
    /// Unlock: picks the lock of whatever is pointed at, a door or a barrel or chest, with a
    /// skill of 45 (UWLockRules.CastUnlock, read 2026-09-25 - the original's Unlock case hands
    /// UnlockDoor 0xFFD3). So difficulty 15 withstands it and a hard lock can resist; "The
    /// spell unlocks the lock." or "The spell has no discernable effect.". Until then ours
    /// always unlocked, only doors, and answered a failure with "That is not locked.".
    ///
    /// NO OPENING. Unlocking does not open the door - the same already applies to the
    /// key (see Interaction.TryToggleDoorLockWithKey, confirmed by user).
    /// </summary>
    private static bool fUnlock(UWEntityInfo pOTarget, Interaction pOInteraction,
        UWLevelLoader pOLevelLoader)
    {
        UWDoorLock lOLock = pOTarget.GetComponentInParent<UWDoorLock>();
        UWObject lOContainerLock = null;

        if (pOLevelLoader != null && pOLevelLoader.CurrentLevel != null)
        {
            if (lOLock != null)
                lOLock.EnsureResolved(pOTarget.ObjectData, pOLevelLoader.CurrentLevel.Masterlist);
            else
                lOContainerLock = UWLockRules.FindContainerLock(pOTarget.ObjectData, pOLevelLoader.CurrentLevel.Masterlist);
        }

        pOInteraction.AddGeneralMessage(UWLockRules.CastUnlock(lOLock != null ? lOLock.State : null,
            lOContainerLock != null ? pOTarget.ObjectData : null, lOContainerLock));

        return true;
    }
}
