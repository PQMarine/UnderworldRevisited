using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S CLASS 0x12 THINGS ON THE UNITY SIDE (G4 rest part 2, 2026-11-16): leeches, the instruments, the
/// fishing pole, the oil flask and the rock hammer used from the pack (UseMagic, uw2 obj/USEITEMS.C:934). The rules and
/// lines are UWToolRulesUw2's; here the pointer, the pack and the pieces in the world.
/// </summary>
public static class UWToolUseUw2
{
    /// <summary>Whether UseMagic takes this carried thing (the bedroll stays the sleep's).</summary>
    public static bool Handles(int piId)
    {
        return piId == UWToolRulesUw2.Leeches || piId == UWToolRulesUw2.Mandolin || piId == UWToolRulesUw2.Flute
            || piId == UWToolRulesUw2.FishingPole || piId == UWToolRulesUw2.OilFlask || piId == UWToolRulesUw2.RockHammer
            || piId == UWToolRulesUw2.StopperedBottle || piId == UWToolRulesUw2.Horn || UWToolRulesUw2.IsKeyGem(piId)
            || UWToolRulesUw2.IsBones(piId) || piId == UWToolRulesUw2.Anvil || piId == UWToolRulesUw2.Pole;
    }

    /// <summary>
    /// UseUtil in the world (how 0): the anvil and the pole ask for their target there too, the anvil standing where it
    /// is and the clicked thing in the pack repaired (uses part 2, 2026-11-18). False for anything else.
    /// </summary>
    public static bool UseInWorld(UWObject pOItem, DataImport pOData, UWInventoryModel pOInventory)
    {
        if (pOItem == null || (pOItem.ID != UWToolRulesUw2.Anvil && pOItem.ID != UWToolRulesUw2.Pole))
            return false;

        Use(pOItem, pOData, pOInventory, null);

        return true;
    }

    /// <summary>UseMagic from the pack. pOHost carries the instrument player.</summary>
    public static void Use(UWObject pOItem, DataImport pOData, UWInventoryModel pOInventory, GameObject pOHost)
    {
        switch (pOItem.ID)
        {
            case UWToolRulesUw2.Leeches:
                fLeeches(pOItem, pOData, pOInventory);
                break;

            case UWToolRulesUw2.Mandolin:
            case UWToolRulesUw2.Flute:
                UWInstrumentPlayer.BeginSecondGame(pOHost, pOItem, piLine => fSay(pOData, piLine));
                break;

            case UWToolRulesUw2.FishingPole:
                fFish(pOData, pOInventory);
                break;

            case UWToolRulesUw2.OilFlask:
                fBeginUseOn(pOItem, pOData, (pOTarget, pOEntity, pbCarried) => fOil(pOItem, pOTarget, pbCarried, pOData, pOInventory));
                break;

            case UWToolRulesUw2.RockHammer:
                fBeginUseOn(pOItem, pOData, (pOTarget, pOEntity, pbCarried) => fRockHammer(pOTarget, pOEntity, pbCarried, pOData));
                break;

            // UseUnique (part 3): the stoppered bottle, the horn, the key gems.
            case UWToolRulesUw2.StopperedBottle:
                fSay(pOData, UWToolRulesUw2.StopperString);
                break;

            case UWToolRulesUw2.Horn:
                if (UWToolRulesUw2.HornSounds(UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null))
                    UWInstrumentPlayer.BeginSecondGame(pOHost, UWToolRulesUw2.HornInstrument, piLine => fSay(pOData, piLine), UWToolRulesUw2.HornPlayedString);
                else
                    fSay(pOData, UWToolRulesUw2.HornFailsString);
                break;

            // UseUtil (uses part 2): the anvil repairs a carried thing, the pole works a switch out of reach.
            case UWToolRulesUw2.Anvil:
                fBeginUseOn(pOItem, pOData, (pOTarget, pOEntity, pbCarried) => fAnvil(pOTarget, pbCarried, pOData, pOInventory));
                break;

            case UWToolRulesUw2.Pole:
                fBeginUseOn(pOItem, pOData, (pOTarget, pOEntity, pbCarried) => fPole(pOTarget, pOEntity, pOData));
                break;

            default:
                if (UWToolRulesUw2.IsKeyGem(pOItem.ID))
                    fBeginUseOn(pOItem, pOData, (pOTarget, pOEntity, pbCarried) => fKeyGem(pOItem, pOTarget, pbCarried, pOData, pOInventory));
                else if (UWToolRulesUw2.IsBones(pOItem.ID))
                    fBeginUseOn(pOItem, pOData, (pOTarget, pOEntity, pbCarried) => fBones(pOItem, pOTarget, pOData, pOInventory));
                break;
        }
    }

    private static void fSay(DataImport pOData, int piLine)
    {
        UWGameScreenUw2.Say(UWText.Block(pOData, 1, piLine, string.Empty));
    }

    /// <summary>UseThing: "Use &lt;name&gt; on what?" and the waiting pointer.</summary>
    private static void fBeginUseOn(UWObject pOItem, DataImport pOData, System.Action<UWObject, UWEntityInfo, bool> pOActor)
    {
        UWAttackUw2 lOAttack = UWAttackUw2.Current;

        if (lOAttack == null)
            return;

        lOAttack.AddMessage(UWItemMessages.UseOnWhat(pOData, pOItem.ID));
        lOAttack.BeginUseOn(pOItem, pOActor);
    }

    /// <summary>One of the pack's things used up (using_punt): one off a stack, else the thing.</summary>
    public static void UseUp(UWObject pOItem, UWInventoryModel pOInventory)
    {
        if (pOInventory == null)
            return;

        if (!UWStackRules.TryTakeOne(pOItem))
            pOInventory.RemoveItem(pOItem);

        pOInventory.NotifyChanged();
    }

    /// <summary>Leeches: the line when poisoned, the poison gone, backfire(2), used up.</summary>
    private static void fLeeches(UWObject pOItem, DataImport pOData, UWInventoryModel pOInventory)
    {
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;

        if (lOPlayer == null || lOPlayer.Vitals == null)
            return;

        if (lOPlayer.Vitals.Poison > 0)
            fSay(pOData, UWToolRulesUw2.LeechesString);

        lOPlayer.Vitals.CurePoison();
        UWDamagePipeline.Apply(lOPlayer.Vitals, UWDamageRequest.Leeches());
        lOPlayer.WriteToRecord();
        UseUp(pOItem, pOInventory);
    }

    /// <summary>
    /// go_fish: the water 11 eighths ahead (UWFishing.CanFishAhead), the bite by the Track skill (one roll of 0 to 4 at
    /// most (Track + 7) / 8), the fish if it can be carried, onto the empty pointer (place_new; with the pointer busy
    /// the fish is lost, as there). The pole stays.
    /// </summary>
    private static void fFish(DataImport pOData, UWInventoryModel pOInventory)
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;
        Transform lOViewer = Camera.main != null ? Camera.main.transform : null;

        if (lOLoader == null || lOPlayer == null || lOPlayer.Vitals == null || pOInventory == null)
            return;

        if (!UWFishing.CanFishAhead(lOLoader, lOViewer))
        {
            fSay(pOData, UWToolRulesUw2.FishNotThereString);

            return;
        }

        if (!UWGameSession.Current.Game.Traps.Bites(lOPlayer.Vitals.GetSkill(UWPlayerData.Skill.Track)))
        {
            fSay(pOData, UWToolRulesUw2.FishNoLuckString);

            return;
        }

        UWObject lOFish = UWFishing.CreateFish(lOLoader);

        if (lOFish == null || !pOInventory.CanCarry(lOFish))
        {
            fSay(pOData, UWToolRulesUw2.FishGetsAwayString);

            return;
        }

        fSay(pOData, UWToolRulesUw2.FishCaughtString);

        if (pOInventory.CursorItem == null)
            pOInventory.PickUpToCursor(lOFish);
    }

    /// <summary>UseOilOn: a carried thing only; one taken off a stack onto the pointer before it changes.</summary>
    private static void fOil(UWObject pOFlask, UWObject pOTarget, bool pbCarried, DataImport pOData, UWInventoryModel pOInventory)
    {
        if (pOTarget == null || !pbCarried || pOInventory == null)
            return;

        UWToolRulesUw2.OilResult lOResult = UWToolRulesUw2.Oil(pOTarget.ID, pOTarget.Quality);
        UWObject lOChanged = pOTarget;

        if (lOResult.Changes && UWStackRules.IsStack(pOTarget) && pOInventory.CursorItem == null)
        {
            lOChanged = pOTarget.CloneWithQuantity(1);
            UWStackRules.TryTakeOne(pOTarget);
            pOInventory.PickUpToCursor(lOChanged);
        }

        fSay(pOData, lOResult.Message);

        if (lOResult.FlaskUsed)
            UseUp(pOFlask, pOInventory);

        if (!lOResult.Changes)
            return;

        lOChanged.ID = (ushort)lOResult.NewId;
        lOChanged.Quality = (ushort)lOResult.NewQuality;
        UWObjectAppearance.Apply(lOChanged, pOData.Textures, null);
        pOInventory.NotifyChanged();
    }

    /// <summary>UseBonesOn: on a gravestone the bones find their rest and are used up, on anything else no effect.</summary>
    private static void fBones(UWObject pOBones, UWObject pOTarget, DataImport pOData, UWInventoryModel pOInventory)
    {
        int liLine = UWToolRulesUw2.BonesOn(pOTarget != null ? pOTarget.ID : 0, out bool lbUsedUp);

        fSay(pOData, liLine);

        if (lbUsedUp)
            UseUp(pOBones, pOInventory);
    }

    /// <summary>UsePoleOn: a switch is worked as a use in the world (UseObj with how 0), with its line; anything else
    /// cannot be.</summary>
    private static void fPole(UWObject pOTarget, UWEntityInfo pOEntity, DataImport pOData)
    {
        if (pOTarget == null || pOEntity == null || !UWToolRulesUw2.IsSwitch(pOTarget.ID) || UWWalkUw2View.Current == null)
        {
            fSay(pOData, UWToolRulesUw2.PoleCannotString);

            return;
        }

        fSay(pOData, UWToolRulesUw2.PoleTriggersString);
        UWWalkUw2View.Current.UseThing(pOEntity);
    }

    /// <summary>
    /// UseAnvilOn and repair_item (uw2 event/WORLDEV.C:1047, the first game's rules - UWRepairRules with the second
    /// game's lines): a carried thing only; the estimate and "Make an attempt?", then the attempt - loud (noise 0xF), the
    /// time passed (pass_time, the schedules catching up), the line, a destroyed thing gone from the pack.
    /// </summary>
    private static void fAnvil(UWObject pOTarget, bool pbCarried, DataImport pOData, UWInventoryModel pOInventory)
    {
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;

        if (pOTarget == null || !pbCarried || lOPlayer == null || lOPlayer.Vitals == null || UWGameScreenUw2.Current == null)
            return;

        int liSkill = lOPlayer.Vitals.GetSkill(UWPlayerData.Skill.Repair);

        if (!UWRepairRules.TryGetQuestion(pOTarget, pOData, liSkill, UWGame.Uw2, out string lsQuestion))
        {
            fSay(pOData, UWRepairRules.Uw2CannotRepairMessage);

            return;
        }

        UWGameScreenUw2.Current.AskYesNo(lsQuestion, pbYes =>
        {
            if (pbYes)
                fRepair(pOTarget, pOData, pOInventory, liSkill);
        });
    }

    private static void fRepair(UWObject pOTarget, DataImport pOData, UWInventoryModel pOInventory, int piSkill)
    {
        UWVitalsUw2 lOPlayer = UWVitalsUw2.Current;
        UWPlayerFile lOFile = UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;

        if (lOPlayer == null || lOPlayer.Vitals == null)
            return;

        UWRepairRules.Result lOResult = UWRepairRules.Attempt(pOTarget, pOData, piSkill, UWGame.Uw2);

        lOPlayer.Vitals.MakeNoise(UWRepairRules.RepairNoise);

        // pass_time(time * 60): the record first (the day clock is read from it), the vitals take the new clock, the
        // schedules catch up - as the jail's night (UWDeathUw2).
        if (lOFile != null && lOResult.Minutes > 0)
        {
            lOPlayer.WriteToRecord();
            UWDeathRulesUw2.PassTime(lOFile, lOResult.Minutes * 60);
            lOPlayer.Vitals.TakeClock(UWPlayerRecordUw2.GameClock(lOFile));
            UWScheduleHostUw2.RunAll();
        }

        if (!string.IsNullOrEmpty(lOResult.Message))
            UWGameScreenUw2.Say(lOResult.Message);

        if (lOResult.Destroyed && pOInventory != null)
            pOInventory.RemoveItem(pOTarget);

        if (pOInventory != null)
            pOInventory.NotifyChanged();
    }

    /// <summary>UseKeyGem: a treated key gem on the large blackrock gem fuses with its lines and sounds and is used up.</summary>
    private static void fKeyGem(UWObject pOKeyGem, UWObject pOTarget, bool pbCarried, DataImport pOData, UWInventoryModel pOInventory)
    {
        UWToolRulesUw2.KeyGemResult lOResult = UWToolRulesUw2.UseKeyGem(pOKeyGem, pOTarget != null ? pOTarget.ID : 0, pbCarried,
            UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null);

        // do_sfx(4, ...): the tremor, before the lines (per user, 2026-11-16: the view did not shake).
        if (lOResult.Fused)
        {
            UWScreenShake lOShake = UWScreenShake.Ensure();

            if (lOShake != null)
                lOShake.Shake(UWScreenShake.LargeChannel, lOResult.ShakeRounds);
        }

        foreach (int liLine in lOResult.Lines)
            fSay(pOData, liLine);

        if (!lOResult.Fused)
            return;

        UWAudioService.PlayEffect(UWToolRulesUw2.KeyGemSound, UWToolRulesUw2.KeyGemSoundVolume);
        UWAudioService.PlayEffect(lOResult.AllUsed ? UWToolRulesUw2.KeyGemAllSound : UWToolRulesUw2.KeyGemAfterSound,
            UWToolRulesUw2.KeyGemAfterSoundVolume);
        UseUp(pOKeyGem, pOInventory);
    }

    /// <summary>UseRockHammerOn: a boulder in the world breaks; the boulder goes first, the new pieces around its spot.</summary>
    private static void fRockHammer(UWObject pOTarget, UWEntityInfo pOEntity, bool pbCarried, DataImport pOData)
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (pOTarget == null || pbCarried || pOEntity == null || lOLoader == null)
            return;

        List<UWToolRulesUw2.RockPiece> lOPieces = UWToolRulesUw2.BreakRock(pOTarget.ID, out bool lbBroken);

        if (!lbBroken)
        {
            fSay(pOData, UWToolRulesUw2.NoEffectString);

            return;
        }

        fSay(pOData, UWToolRulesUw2.RockBreaksString);

        UWTilePos lOTile = new UWTilePos(pOTarget.TileX, pOTarget.TileY);
        Vector3 lOSpot = pOEntity.transform.position;
        int liZ = pOTarget.ZPos;

        lOLoader.RemoveObjectFromWorld(pOTarget);

        foreach (UWToolRulesUw2.RockPiece lOPiece in lOPieces)
        {
            UWObject lORock = UWObjectFactory.Create(lOPiece.ObjectId, pOData, lOPiece.Quantity > 0 ? lOPiece.Quantity : UWObjectFactory.Unset);

            if (lORock == null)
                break;

            lORock.WasSpawned = true;
            lOLoader.SpawnScatteredAround(lORock, lOSpot, UWToolRulesUw2.PieceRange, lOTile, pbFitsLikeCanPlace: true, piZ: liZ);
        }
    }
}
