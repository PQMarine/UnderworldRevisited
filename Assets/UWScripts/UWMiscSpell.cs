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
    /// A class 11 spell that goes onto a clicked target in reach (see
    /// UWPendingSpell.fApplyTargetSpell): Unlock, Name Enchantment and Remove Trap.
    ///
    /// EACH SPEAKS FOR ITSELF, as obj_spells (UnderworldExhumed SPELLS.C, read 2026-10-12):
    /// Unlock says "The spell unlocks the lock." or "The spell has no discernable effect.",
    /// Name Enchantment is a look, Remove Trap on a thing without a trap says nothing, and any
    /// other minor (Strengthen Door) does nothing at all. Until 2026-10-12 a miss of Remove Trap
    /// or Name Enchantment printed "The spell has no discernable effect.".
    /// </summary>
    public static void CastOnTarget(int piMinorClass, UWEntityInfo pOTarget,
        Interaction pOInteraction, UWLevelLoader pOLevelLoader)
    {
        if (pOTarget == null || pOInteraction == null)
            return;

        switch (piMinorClass)
        {
            case UWMiscSpellRules.UnlockSpell:
                fUnlock(pOTarget, pOInteraction, pOLevelLoader);
                break;

            case UWMiscSpellRules.NameEnchantmentSpell:
                fNameEnchantment(pOTarget, pOInteraction);
                break;

            case UWMiscSpellRules.RemoveTrapSpell:
                pOInteraction.TryRemoveTrapWithSpell(pOTarget);
                break;
        }
    }

    /// <summary>
    /// A class 11 target spell on an item in the inventory - see
    /// UWPendingSpell.ResolveInInventory.
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
    ///
    /// WHAT CANNOT BE IDENTIFIED (a creature, a door, a directional thing) is simply looked at:
    /// the original's case is LookAt with the identify level, which describes anything
    /// lookable (obj_spells and LookAt, UnderworldExhumed SPELLS.C and LOOK.C, read 2026-10-12).
    /// </summary>
    private static void fNameEnchantment(UWEntityInfo pOTarget, Interaction pOInteraction)
    {
        if (pOTarget.ObjectData != null && pOInteraction.IdentifyItem(pOTarget.ObjectData))
            return;

        string lsLook = pOInteraction.GetLookDescription(pOTarget);

        if (!string.IsNullOrEmpty(lsLook))
            pOInteraction.AddMessage(lsLook);

        // And SpecialLook's line, which LookAt prints after the sentence (a spiked door,
        // 2026-10-15).
        string lsSpecial = pOInteraction.GetSpecialLookLine(pOTarget);

        if (!string.IsNullOrEmpty(lsSpecial))
            pOInteraction.AddMessage(lsSpecial);
    }

    /// <summary>
    /// Unlock: picks the lock of whatever is pointed at, a door or a barrel or chest, with a
    /// skill of 45 (UWLockRules.CastUnlock, read 2026-09-25 - the original's Unlock case hands
    /// UnlockDoor 0xFFD3). So difficulty 15 withstands it and a hard lock can resist; "The
    /// spell unlocks the lock." or "The spell has no discernable effect.". Until then ours
    /// always unlocked, only doors, and answered a failure with "That is not locked.".
    ///
    /// NO OPENING. Unlocking does not open the door - the same already applies to the
    /// key (see UWItemTargetUse, confirmed by user).
    /// </summary>
    private static bool fUnlock(UWEntityInfo pOTarget, Interaction pOInteraction,
        UWLevelLoader pOLevelLoader)
    {
        // A door's lock, else a chest's or barrel's - the one lookup the key, the pick and the
        // spell share (UWDoor.ResolveLockTarget, 2026-10-12).
        UWDoor.ResolveLockTarget(pOTarget, pOLevelLoader != null ? pOLevelLoader.CurrentLevel : null,
            out UWLockState lODoorLock, out UWObject lOContainerLock);

        pOInteraction.AddGeneralMessage(UWLockRules.CastUnlock(lODoorLock,
            lOContainerLock != null ? pOTarget.ObjectData : null, lOContainerLock));

        return true;
    }
}
