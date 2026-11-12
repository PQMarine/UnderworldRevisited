using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S SPELLS ON A CLICKED THING (UW2 plan G1 part D2, 2026-11-12): obj_spells' cases on the thing the target
/// mode picked (UWAttackUw2), in the world (pOEntity its node) or in the inventory (pbCarried, no node). The rules that need
/// no engine are UWObjectSpellRulesUw2's; this side holds the calls on the level, the doors, the look and the player.
///
/// THE MANA, as obj_spells: paid at the end, waived only where the spell found nothing to work on - Dispel Rune on what is
/// no rune, Mending on what cannot be mended, Gate Travel on what is no moonstone. Remove Trap, Name Enchantment, Open and
/// Detect Trap cost it whatever they find.
///
/// NOT YET (G3, the traps): a trap the disarm sets off says so and does not fire, and Open does not set off the lock's
/// trap (checkTrap 0xB); the second game's traps do not go off at all so far.
/// </summary>
public static class UWObjectSpellUw2
{
    /// <summary>Runs the spell; true when its mana is to be paid.</summary>
    public static bool Cast(int piMinor, UWObject pOTarget, UWEntityInfo pOEntity, bool pbCarried, UWAttackUw2 pOAttack, IUWSpellHost pIHost)
    {
        if (pOTarget == null || pOAttack == null || pIHost == null)
            return false;

        DataImport lOData = pIHost.Data;
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWLevel lOLevel = lOLoader != null ? lOLoader.CurrentLevel : null;
        int liDungeonLevel = lOLoader != null ? lOLoader.CurrentLevelIndex + 1 : 1;

        switch (piMinor)
        {
            case UWObjectSpellRulesUw2.DispelRune:
                if (pOTarget.ID != UWObjectSpellRulesUw2.FlameRuneId && pOTarget.ID != UWObjectSpellRulesUw2.StasisRuneId)
                {
                    pOAttack.AddGeneralMessage(UWObjectSpellRulesUw2.NotARuneString);

                    return false;
                }

                pIHost.World.Remove(pOTarget);

                return true;

            case UWObjectSpellRulesUw2.Mending:
                if (!UWObjectSpellRulesUw2.Mendable(pOTarget.ID))
                {
                    pOAttack.AddGeneralMessage(UWObjectSpellRulesUw2.NoNoticeableEffectString);

                    return false;
                }

                pOAttack.AddMessage(UWText.General(lOData, UWObjectSpellRulesUw2.RepairsString) + UWText.ObjectName(lOData, pOTarget.ID)
                    + UWText.General(lOData, UWObjectSpellRulesUw2.PeriodString, "."));
                pOTarget.Quality = UWObjectSpellRulesUw2.MendedQuality;

                // FixPlayerEquips and editchng(0x200): what is worn counts anew and the panel redraws (the armour's picture
                // follows its quality).
                if (UWGameScreenUw2.Current != null)
                    UWGameScreenUw2.Current.InventoryChanged();
                else if (UWVitalsUw2.Current != null)
                    UWVitalsUw2.Current.RefreshStatus();

                return true;

            case UWObjectSpellRulesUw2.RemoveTrap:
                fRemoveTrap(pOTarget, lOLevel, liDungeonLevel, pOAttack, lOData);

                return true;

            case UWObjectSpellRulesUw2.NameEnchantment:
                fNameEnchantment(pOTarget, pOEntity, pOAttack);

                return true;

            case UWObjectSpellRulesUw2.Open:
                fOpen(pOTarget, pOEntity, pbCarried, lOLevel, pOAttack);

                return true;

            case UWObjectSpellRulesUw2.DetectTrap:
                pOAttack.AddGeneralMessage(fDetectTrap(pOTarget, lOLevel, liDungeonLevel)
                    ? UWObjectSpellRulesUw2.DetectedTrapString : UWObjectSpellRulesUw2.NoTrapsString);

                return true;

            case UWObjectSpellRulesUw2.Enchantment:
                fEnchant(pOTarget, pbCarried, lOLevel, pOAttack, pIHost);

                return true;

            case UWObjectSpellRulesUw2.GateTravel:
                return fGateTravel(pOTarget, pbCarried, lOLevel, liDungeonLevel, pOAttack, pIHost);

            default:
                return false;
        }
    }

    /// <summary>DetectedTrap(target, 45): a trap there and the check against 10 and twice the world passed.</summary>
    private static bool fDetectTrap(UWObject pOTarget, UWLevel pOLevel, int piDungeonLevel)
    {
        return pOLevel != null && UWObjectSpellRulesUw2.TryFindTrap(pOTarget, pOLevel.Masterlist, out _, out _)
            && UWSkillCheck.IsSuccess(UWSkillCheck.Check(UWObjectSpellRulesUw2.SpellSkill, UWObjectSpellRulesUw2.DetectTarget(piDungeonLevel)));
    }

    /// <summary>obj_spells' case 10: a trap found as Detect Trap finds it is disarmed with 45 against 8 and the world - "The
    /// trap on the chest was successfully disarmed." and the trap gone from the thing; a fumble says it set the trap off;
    /// else "Unable to defuse trap.". Found nothing: no word.</summary>
    private static void fRemoveTrap(UWObject pOTarget, UWLevel pOLevel, int piDungeonLevel, UWAttackUw2 pOAttack, DataImport pOData)
    {
        if (!fDetectTrap(pOTarget, pOLevel, piDungeonLevel)
            || !UWObjectSpellRulesUw2.TryFindTrap(pOTarget, pOLevel.Masterlist, out UWObject lOTrigger, out UWObject lOTrap))
            return;

        UWSkillCheck.ResultEnum leResult = UWSkillCheck.Check(UWObjectSpellRulesUw2.SpellSkill, UWObjectSpellRulesUw2.DisarmTarget(piDungeonLevel));
        string lsTrap = UWObjectSpellRulesUw2.TrapName(pOData, lOTrap);

        if (UWSkillCheck.IsSuccess(leResult))
        {
            pOAttack.AddMessage("The " + lsTrap + " on the " + UWText.ObjectName(pOData, pOTarget.ID)
                + UWText.General(pOData, UWObjectSpellRulesUw2.DisarmedString));

            // The thing's chain no longer leads to the trap: what the first game's disarm clears (UWTrapDisarming). The
            // original deletes only the trap from the trigger's square (delete_trap); with the second game's traps (G3)
            // neither fires again.
            UWTrapChainRemoval.ClearSpecialChain(pOTarget, pOLevel);

            return;
        }

        if (leResult == UWSkillCheck.ResultEnum.CriticalFailure)
        {
            pOAttack.AddMessage(UWText.General(pOData, UWObjectSpellRulesUw2.SetOffString) + lsTrap
                + UWText.General(pOData, UWObjectSpellRulesUw2.PeriodString, "."));

            return;
        }

        pOAttack.AddGeneralMessage(UWObjectSpellRulesUw2.UnableToDefuseString);
    }

    /// <summary>LookAt(target, 3) and the identified mark: an item that can be identified is named whole, anything else
    /// lookable is described (UW1's Name Enchantment, UWMiscSpell).</summary>
    private static void fNameEnchantment(UWObject pOTarget, UWEntityInfo pOEntity, UWAttackUw2 pOAttack)
    {
        UWLookService lOLook = pOAttack.Look;

        if (lOLook == null || lOLook.IdentifyItem(pOTarget))
            return;

        if (pOEntity == null)
        {
            lOLook.AddItemLookMessages(pOTarget);

            return;
        }

        string lsLook = lOLook.GetLookDescription(pOEntity);

        if (!string.IsNullOrEmpty(lsLook))
            pOAttack.AddMessage(lsLook);

        string lsSpecial = lOLook.GetSpecialLookLine(pOEntity);

        if (!string.IsNullOrEmpty(lsSpecial))
            pOAttack.AddMessage(lsSpecial);
    }

    /// <summary>checkLock(player, target, -45): a door's lock or the lock in a chest's chain, picked with 45 - "The spell
    /// unlocks the lock." when it came off, "The spell has no discernable effect." otherwise.</summary>
    private static void fOpen(UWObject pOTarget, UWEntityInfo pOEntity, bool pbCarried, UWLevel pOLevel, UWAttackUw2 pOAttack)
    {
        UWLockState lODoorLock = null;
        UWObject lOContainerLock;

        if (pbCarried || pOEntity == null)
            lOContainerLock = UWLockRules.FindLoadedLock(pOTarget);
        else
            UWDoor.ResolveLockTarget(pOEntity, pOLevel, out lODoorLock, out lOContainerLock);

        bool lbUnlocked = UWLockRules.Unlock(lODoorLock, lOContainerLock != null ? pOTarget : null, lOContainerLock, false,
            -UWObjectSpellRulesUw2.SpellSkill) == UWLockRules.Outcome.Unlocked;

        pOAttack.AddGeneralMessage(lbUnlocked ? UWObjectSpellRulesUw2.UnlocksString : UWObjectSpellRulesUw2.NoDiscernableEffectString);
    }

    /// <summary>
    /// obj_spells' case 15 on a moonstone: with one left on another level the player goes there, to that stone (as the
    /// class 11 Gate Travel finds it, UWExtraSpellRulesUw2); with the placed ones on this level to the other moonstone in
    /// the tiles, else to the clicked one's square; none placed: "The moonstone is not available.". As the first game's,
    /// ours stays when the far level holds no stone (Docs/DEVIATIONS.md).
    /// </summary>
    private static bool fGateTravel(UWObject pOTarget, bool pbCarried, UWLevel pOLevel, int piDungeonLevel, UWAttackUw2 pOAttack, IUWSpellHost pIHost)
    {
        if (pOTarget.ID != UWObjectSpellRulesUw2.MoonstoneId)
        {
            pOAttack.AddGeneralMessage(UWObjectSpellRulesUw2.NotAMoonstoneString);

            return false;
        }

        IUWSpellHostUw2 lIPlayer = pIHost as IUWSpellHostUw2;

        if (lIPlayer == null || lIPlayer.Record == null)
            return true;

        UWObjectSpellRulesUw2.GateEnum leGate = UWObjectSpellRulesUw2.DecideGate(UWPlayerRecordUw2.Moonstone(lIPlayer.Record, 0),
            UWPlayerRecordUw2.Moonstone(lIPlayer.Record, 1), piDungeonLevel, out int liLevel);

        if (leGate == UWObjectSpellRulesUw2.GateEnum.NotAvailable)
        {
            pOAttack.AddGeneralMessage(UWObjectSpellRulesUw2.MoonstoneNotAvailableString);

            return true;
        }

        if (leGate == UWObjectSpellRulesUw2.GateEnum.OtherLevel)
        {
            UWObject lOStone = pIHost.Data != null && pIHost.Data.Levels != null && liLevel <= pIHost.Data.Levels.Count && pIHost.Data.Levels[liLevel - 1] != null
                ? UWMoonstoneRules.FindHolderInLevel(pIHost.Data.Levels[liLevel - 1]) : null;

            if (lOStone == null)
                pOAttack.AddGeneralMessage(UWObjectSpellRulesUw2.MoonstoneNotAvailableString);
            else
                lIPlayer.MovePlayer(liLevel, new UWTilePos(lOStone.TileX, lOStone.TileY));

            return true;
        }

        UWObject lOOther = fOtherMoonstoneInTiles(pOLevel, pOTarget);

        if (lOOther != null)
            lIPlayer.MovePlayer(piDungeonLevel, new UWTilePos(lOOther.TileX, lOOther.TileY));
        else if (!pbCarried)
            lIPlayer.MovePlayer(piDungeonLevel, new UWTilePos(pOTarget.TileX, pOTarget.TileY));

        return true;
    }

    /// <summary>
    /// obj_spells' case 14, sp_enchant (UWEnchantSpellRulesUw2, part D2b): "You have enchanted the ...", "You cannot enchant
    /// that.", or the blast - "Your attempt to enchant the ... destroys it in a blaze of fire!", the explosion with its
    /// flames and 10d6 of magic and fire on the thing's square (the player's when carried, sp_enchant takes his square);
    /// a blown charge leaves the thing as its debris; a destroyed thing leaves its debris too (carried: "Your ... was
    /// destroyed." and the debris by the player, as DamageInventory with debris; in the world damage_item's remove_object
    /// turns it into its debris in place - a weapon its broken weapon, else a pile; until 2026-11-12 ours removed it).
    /// </summary>
    private static void fEnchant(UWObject pOTarget, bool pbCarried, UWLevel pOLevel, UWAttackUw2 pOAttack, IUWSpellHost pIHost)
    {
        DataImport lOData = pIHost.Data;
        UWObjectClassProperties lOProperties = lOData != null ? lOData.ObjectClassProperties : null;
        string lsName = UWText.ObjectName(lOData, pOTarget.ID);
        UWEnchantSpellRulesUw2.OutcomeEnum leOutcome = UWEnchantSpellRulesUw2.Enchant(pOTarget, pbCarried || pOLevel == null ? null : pOLevel.Masterlist,
            pIHost.PlayerLevel, pIHost.PlayerCastingSkill, lOProperties, out UWObject lOSpellGone);
        UWGameScreenUw2 lOScreen = UWGameScreenUw2.Current;

        if (leOutcome == UWEnchantSpellRulesUw2.OutcomeEnum.CannotEnchant)
        {
            pOAttack.AddGeneralMessage(UWEnchantSpellRulesUw2.CannotEnchantString);

            return;
        }

        if (leOutcome == UWEnchantSpellRulesUw2.OutcomeEnum.Enchanted)
        {
            pOAttack.AddMessage(UWText.General(lOData, UWEnchantSpellRulesUw2.EnchantedString) + UWText.ObjectName(lOData, pOTarget.ID)
                + UWText.General(lOData, UWObjectSpellRulesUw2.PeriodString, "."));

            if (pbCarried && lOScreen != null)
                lOScreen.InventoryChanged();

            return;
        }

        pOAttack.AddMessage(UWText.General(lOData, UWEnchantSpellRulesUw2.AttemptString) + lsName + UWText.General(lOData, UWEnchantSpellRulesUw2.BlazeString));

        UWTilePos lOSquare;
        int liZPos;

        if (pbCarried || pOLevel == null)
        {
            Camera lOCamera = UWViewpoint.Current;
            Vector3 lOFeet = lOCamera != null ? UWPlayerThrow.GetFeet(lOCamera.transform) : Vector3.zero;

            lOSquare = new UWTilePos(UWTileQueries.WorldToEighths(lOFeet.x) >> 3, UWTileQueries.WorldToEighths(lOFeet.z) >> 3);
            liZPos = UWUnits.WorldToZPos(lOFeet.y);
        }
        else
        {
            lOSquare = new UWTilePos(pOTarget.TileX, pOTarget.TileY);
            liZPos = pOTarget.ZPos;
        }

        fBlast(lOSquare, liZPos, pIHost);

        if (leOutcome == UWEnchantSpellRulesUw2.OutcomeEnum.Blown)
        {
            // The spell object out of the thing's chain, the thing its debris (SET_ITEM).
            if (lOSpellGone != null)
            {
                if (pOTarget.Contents != null)
                    pOTarget.Contents.Remove(lOSpellGone);
                else
                    UWTrapChainRemoval.RemoveTrapFromChain(pOTarget, lOSpellGone, pOLevel);
            }

            if (pbCarried)
            {
                if (lOScreen != null)
                    lOScreen.InventoryChanged();
            }
            else
            {
                int liDebris = pOTarget.ID;

                pIHost.World.Remove(pOTarget);
                pIHost.World.SpawnNewObject(liDebris, lOSquare, 1, UWWorldEdit.Arrival.Created);
            }

            return;
        }

        // Destroyed outright.
        if (pbCarried)
        {
            if (lOScreen != null && lOScreen.Inventory != null)
                lOScreen.Inventory.RemoveItem(pOTarget);

            pOAttack.AddMessage("Your " + lsName + (lsName.EndsWith("s") ? " were" : " was") + " destroyed.");
            pIHost.World.SpawnNewObject(UWEnchantSpellRulesUw2.DebrisOf(pOTarget.ID, lOProperties), lOSquare, 1, UWWorldEdit.Arrival.Created);

            if (lOScreen != null)
                lOScreen.InventoryChanged();
        }
        else
        {
            // remove_object: the thing becomes its debris where it lay (the blast may have done it already, then
            // the entity is gone and there is nothing left to do).
            UWLevelLoader lOLoader = UWScene.LevelLoader;

            if (lOLoader != null && lOLoader.TryGetEntity(pOTarget, out UWEntityInfo lOEntity))
                UWDebris.ReplaceWithDebris(lOLoader, lOEntity, pOTarget, new UWTilePos(pOTarget.TileX, pOTarget.TileY),
                    lOEntity.transform.position);
            else
                pIHost.World.Remove(pOTarget);
        }
    }

    /// <summary>sp_enchant_faildestroymess's blast: the explosion 0x1C2 with its flames (fireball_effect) and damage_square's
    /// 10d6 of magic and fire on the square.</summary>
    private static void fBlast(UWTilePos pOSquare, int piZPos, IUWSpellHost pIHost)
    {
        const int Fine = 3;
        IUWPresentation lIPresentation = pIHost.World.Presentation;

        lIPresentation.ShowEffect(UWEnchantSpellRulesUw2.ExplosionId, new UWSpot(pOSquare, Fine, Fine, piZPos),
            UWExplosionRules.LengthToSeconds(UWExplosionRules.ExplosionLengthTicks));

        foreach (UWExplosionRules.Flame lOFlame in UWExplosionRules.ScatterFlames(UWEnchantSpellRulesUw2.ExplosionId, Fine, Fine, piZPos))
            lIPresentation.ShowEffect(lOFlame.ObjectId, new UWSpot(pOSquare, lOFlame.FineX, lOFlame.FineY, lOFlame.ZPos),
                UWExplosionRules.LengthToSeconds(lOFlame.LengthTicks), lOFlame.StartFrame);

        pIHost.World.StrikeTile(pOSquare, UWEnchantSpellRulesUw2.BlastDice, UWEnchantSpellRulesUw2.BlastSides,
            UWEnchantSpellRulesUw2.BlastDamageType, pIHost.Cast.AttackerIndex);
    }

    /// <summary>Obj_FindInMap's moonstone: the first in the level's tiles that is not the clicked one.</summary>
    private static UWObject fOtherMoonstoneInTiles(UWLevel pOLevel, UWObject pOClicked)
    {
        if (pOLevel == null || pOLevel.TileData == null)
            return null;

        foreach (UWTile lOTile in pOLevel.TileData)
        {
            if (lOTile == null || lOTile.ObjectsInTile == null)
                continue;

            foreach (UWObject lOObject in lOTile.ObjectsInTile)
            {
                if (lOObject != null && lOObject != pOClicked && lOObject.ID == UWObjectSpellRulesUw2.MoonstoneId)
                    return lOObject;
            }
        }

        return null;
    }
}
