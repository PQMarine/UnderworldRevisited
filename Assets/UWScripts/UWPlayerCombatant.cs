using UWDataImport.UWData;

/// <summary>
/// THE PLAYER AS THE DEFENDER of a blow or a missile (cleanup step 4.9, 2026-10-18): what
/// UWAttackResolution asks of the struck body, answered over UWCharacter - his defence, his
/// enchantment protection and armour of a part, whether the piece on a part is metal (do_miss),
/// and the wear of that piece on a critical hit (frp_check). A creature's blow (UWCritter) and a
/// missile (UWMissiles) hand it in; UWCharacter.Combatant holds one.
/// </summary>
public sealed class UWPlayerCombatant : IUWAttackDefender
{
    private readonly UWCharacter mOCharacter;

    public UWPlayerCombatant(UWCharacter pOCharacter)
    {
        mOCharacter = pOCharacter;
    }

    public UWDefenderKind DefenderKind => UWDefenderKind.Player;

    /// <summary>THE CHECK VALUE IS THE PLAYER'S ROW BYTE 0x12, not his Defence skill: the original
    /// puts the skill plus half the skill of the weapon in hand there
    /// (PlayerStatusUpdate_ovr133_784, labels 86A-938, see UWPlayerCritterRow).</summary>
    public int Defence => mOCharacter.DefenceCheck;

    public int ProtectionAt(int piPart)
    {
        return mOCharacter.GetProtectionAt(piPart);
    }

    /// <summary>His armour of the part - the damage pipeline subtracts it (UWDamageRequest).</summary>
    public int ArmourAt(int piPart)
    {
        return mOCharacter.GetArmourAt(piPart);
    }

    /// <summary>Whether the player's piece on this part is metal for do_miss: the slot of the
    /// part ((part + 1) &amp; 3 of helm, chest, gloves, leggings), not empty, not leather.
    /// (UWCritter.fIsPlayerArmourMetal until cleanup step 4.9, 2026-10-18.)</summary>
    public bool IsArmourMetalAt(int piPart)
    {
        UWInventory lOInventory = UWScene.Inventory;

        if (lOInventory == null)
            return false;

        UWArmorItemMap.BodySlot leSlot;

        switch (piPart)
        {
            case UWArmourProtection.PartBody: leSlot = UWArmorItemMap.BodySlot.Chest; break;
            case UWArmourProtection.PartHands: leSlot = UWArmorItemMap.BodySlot.Gloves; break;
            case UWArmourProtection.PartLegs: leSlot = UWArmorItemMap.BodySlot.Legs; break;
            default: leSlot = UWArmorItemMap.BodySlot.Helmet; break;
        }

        UWObject lOPiece = lOInventory.GetEquipped(leSlot);

        return lOPiece != null && !UWCritterCombat.IsLeatherArmour(lOPiece.ID);
    }

    /// <summary>The piece in the slot of UWCritterCombat.GetCriticalEquipmentSlot wears by 2d4
    /// (UWEquipmentWear, built 2026-09-28 - this said "only objects 0-15, nothing
    /// happens", but the call uses the armour test).</summary>
    public void OnCriticalHit(int piPart)
    {
        Interaction lOWearer = UWScene.Interaction;

        if (lOWearer != null)
            lOWearer.WearOnCriticalHit(piPart);
    }
}
