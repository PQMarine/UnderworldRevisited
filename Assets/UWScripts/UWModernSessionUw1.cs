using System.Collections.Generic;
using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE FIRST GAME BEHIND THE MODERN INTERFACE'S ENTRY (Docs/UW2-PLAN.md, "The modern interface for UW2: one
/// interface, two hosts", stage U1, 2026-11-23): IUWModernSession over the scene's UWGameUI, UWCharacter and
/// Interaction. Every member forwards to what the windows read directly until then, so the first game shows
/// and does what it did - the vitals from UWCharacter.Vitals, the flasks from its UWFlaskView, the gem and the
/// weapon from its UWWeaponView with UWGameUI's sway, the active spells' texts and the flasks' words from
/// UWHudRunes, the lines from Interaction's message log. Since stage U2 the inventory too: the model of the
/// scene's UWInventory, and what reaches past it through UWCarriedItemUse, UWItemTargetUse, UWItemDrag,
/// UWPlayerThrow and Interaction, as the bags called them.
///
/// Made by UWGameUI when its scene starts (its Start, before it adds the modern windows; by UWModernHud until
/// stage U3 part D) and set on UWModernUi.Session.
/// </summary>
public sealed class UWModernSessionUw1 : IUWModernSession, IUWModernCharacter, IUWModernInventory, IUWModernMagic, IUWModernMessages,
    IUWModernWorld, IUWModernArt
{
    private readonly UWGameUI mOUi;

    private readonly UWCharacter mOCharacter;

    private readonly Interaction mOInteraction;

    public UWModernSessionUw1(UWGameUI pOUi, UWCharacter pOCharacter, Interaction pOInteraction)
    {
        mOUi = pOUi;
        mOCharacter = pOCharacter;
        mOInteraction = pOInteraction;
    }

    // ------------------------------------------------- IUWModernSession

    public DataImport Data => mOUi.mOUWData;

    public FilterMode Filter => mOUi.TextureFilterMode;

    public UWGameUI TextUi => mOUi;

    public IUWModernCharacter Character => this;

    public IUWModernInventory Inventory => this;

    public IUWModernMagic Magic => this;

    public IUWModernMessages Messages => this;

    public IUWModernWorld World => this;

    public IUWModernArt Art => this;

    private IUWModernConversationHost mOConversation;

    public IUWModernConversationHost Conversation => mOConversation ?? (mOConversation = new UWModernConversationHostUw1(mOUi));

    /// <summary>The first game has every part and every preset - they were made from its art - but the spells' hollow, a
    /// hollow of the second game's frame (UW2 plan stage U6 step 2c; per user at the test of 202: the editor offered it on
    /// the first game, and Classic Modular cut its frame at the second game's places).</summary>
    public bool Has(UWModernPartEnum pePart)
    {
        return pePart != UWModernPartEnum.SpellHollow;
    }

    public bool Supports(UWModernLayout.PresetEnum pePreset)
    {
        return true;
    }

    // ------------------------------------------------- IUWModernCharacter

    public int HitPoints => mOCharacter.Vitals.CurrentHP;

    public int MaxHitPoints => mOCharacter.Vitals.MaxHP;

    public int Mana => mOCharacter.Vitals.CurrentMana;

    public int MaxMana => mOCharacter.Vitals.MaxMana;

    /// <summary>The classic path itself (UWHudRunes.DescribeFlask), which Classic Wide uses too.</summary>
    public void DescribeFlask(UWClickRules.FlaskPart pePart)
    {
        if (mOUi.Runes != null)
            mOUi.Runes.DescribeFlask(pePart);
    }

    public void ReportStatus()
    {
        mOUi.Actions.ReportStatus();
    }

    public bool IsLoaded => mOUi.mOUWData != null && mOUi.mOUWData.InitialPlayer != null;

    public int MaxLoad => IsLoaded ? mOUi.mOUWData.InitialPlayer.MaxWeight : 0;

    public UWCharacterSheet BindSheet(UWCharacterSheet pOSheet)
    {
        return pOSheet.Bind(mOUi.mOUWData.Strings, mOUi.mOUWData.InitialPlayer, mOCharacter.Vitals);
    }

    // ------------------------------------------------- IUWModernInventory

    /// <summary>The scene's UWInventory owns the model (UWGameUI.mOInventory, on the same object as Interaction).</summary>
    public UWInventoryModel Model => mOUi.mOInventory != null ? mOUi.mOInventory.Model : null;

    public bool CanUse => UWScene.ItemDrag != null;

    /// <summary>Opening a container sets nothing off on the first game.</summary>
    public void ContainerOpened(UWObject pOContainer)
    {
    }

    public bool Use(UWObject pOItem, UWCarriedItemUse.Context peContext)
    {
        return UWCarriedItemUse.Use(UWScene.ItemDrag, pOItem, peContext);
    }

    public bool UseOn(UWObject pOItem, UWObject pOCarriedTarget)
    {
        return UWItemTargetUse.Apply(mOInteraction, pOItem, pOCarriedTarget, null);
    }

    public void UseOnWorld(UWObject pOItem, Vector2 pOScreenPoint)
    {
        UWItemTargetUse.Apply(mOInteraction, pOItem, null, pOScreenPoint);
    }

    public void SayUsePrompt(UWObject pOItem)
    {
        UWItemDrag lODrag = UWScene.ItemDrag;

        if (lODrag != null)
            lODrag.ReportUsePrompt(pOItem);
    }

    /// <summary>As on the classic page (UWItemDrag.TryConsumeCursorItemOnHead).</summary>
    public bool TryConsumePointerItem()
    {
        UWItemDrag lODrag = UWScene.ItemDrag;

        return lODrag != null && lODrag.TryConsumeCursorItemOnHead();
    }

    /// <summary>The classic view's drop or throw at the pointer's place in the full-screen view (UWPlayerThrow).</summary>
    public bool DropOrThrowPointerItem(Vector2 pOScreenPoint)
    {
        UWPlayerThrow.GetPointerInFullScreenView(pOScreenPoint, out int liX, out int liY);

        Camera lOCamera = Camera.main;

        return lOCamera != null && UWPlayerThrow.TryDropOrThrowAtView(liX, liY, lOCamera.transform, mOUi.mOInventory, UWScene.LevelLoader,
            mOInteraction);
    }

    public string Describe(UWObject pOItem)
    {
        return mOInteraction.DescribeInventoryItem(pOItem);
    }

    public bool TryRead(UWObject pOItem, bool pbLooking)
    {
        return mOInteraction.TryReadBook(pOItem, pbLooking);
    }

    public void SayLook(UWObject pOItem)
    {
        mOInteraction.AddItemLookMessages(pOItem);
    }

    public bool IsItemSpellPending => mOInteraction.IsItemTargetSpellPending;

    public UWPendingSpellState.InventoryClickOutcome CastPendingSpellOn(UWObject pOItem)
    {
        return mOInteraction.ResolveTargetSpellInInventory(pOItem, UWPendingSpellState.InventoryPolicy.Modern);
    }

    // ------------------------------------------------- IUWModernMagic

    public int ActiveSpellVersion => mOCharacter.Vitals.ActiveSpellVersion;

    public int ActiveSpellCount => mOCharacter.Vitals.ActiveSpells.Count;

    /// <summary>The icon the classic shelf shows for the effect (UWRunicMagic.GetIconIndex), none from NoIcon on.</summary>
    public int ActiveSpellPicture(int piSlot)
    {
        IReadOnlyList<UWActiveSpellEffect> lOSpells = mOCharacter.Vitals.ActiveSpells;

        if (piSlot < 0 || piSlot >= lOSpells.Count)
            return -1;

        int liIcon = UWRunicMagic.GetIconIndex(lOSpells[piSlot].MajorClass, lOSpells[piSlot].MinorClass);

        return liIcon < UWRunicMagic.NoIcon ? liIcon : -1;
    }

    public string ActiveSpellText(int piSlot)
    {
        return mOUi.Runes != null ? mOUi.Runes.ActiveSpellText(piSlot) : string.Empty;
    }

    public void DescribeActiveSpell(int piSlot)
    {
        if (mOUi.Runes != null)
            mOUi.Runes.DescribeActiveSpell(piSlot);
    }

    public void CancelActiveSpell(int piSlot)
    {
        if (mOUi.Runes != null)
            mOUi.Runes.CancelActiveSpell(piSlot);
    }

    /// <summary>The classic rune page (UWHudRunes) holds the shelf and its rules.</summary>
    public bool IsReady => mOUi.Runes != null;

    public bool IsHeldByAnotherInput => mOUi.Runes != null && mOUi.Runes.InAnotherInputMode;

    public int RuneCount => UWPlayerData.RuneCount;

    public int RuneObject(int piRune)
    {
        return UWPlayerData.FirstRuneObjectId + piRune;
    }

    /// <summary>The bag of the game's character (the player data's runes), null without the game's data.</summary>
    private UWPlayerData fBag()
    {
        return mOUi.mOUWData != null ? mOUi.mOUWData.InitialPlayer : null;
    }

    public bool HasRune(int piRune)
    {
        UWPlayerData lOBag = fBag();

        return lOBag != null && lOBag.HasRune((UWPlayerData.Rune)piRune);
    }

    public bool HasRunes(int piSequence)
    {
        return UWRuneShelfModel.HasRunes(piSequence, fBag());
    }

    /// <summary>The character's level halved, rounded up (UWSpellCasting.CircleOf).</summary>
    public int Circle => UWSpellCasting.CircleOf(mOCharacter.Vitals.Level);

    /// <summary>UWSpellCasting.CanCast with the character's bag, level and whole mana; without the game's data a
    /// rune counts as missing.</summary>
    public UWSpellCasting.CastCheckEnum CastCheck(int piSequence)
    {
        UWPlayerData lOBag = fBag();

        if (lOBag == null)
            return UWSpellCasting.CastCheckEnum.RuneMissing;

        return UWSpellCasting.CanCast(piSequence, mOCharacter.Vitals.Level, mOCharacter.Vitals.CurrentMana, lOBag);
    }

    public IReadOnlyList<int> Shelf => mOUi.Runes != null ? mOUi.Runes.Shelf.Selected : null;

    public void AddToShelf(int piRune)
    {
        if (mOUi.Runes != null)
            mOUi.Runes.AddToShelf(piRune);
    }

    public void PutOnShelf(IReadOnlyList<int> pORunes)
    {
        if (mOUi.Runes != null)
            mOUi.Runes.PutOnShelf(pORunes);
    }

    public void ClearShelf()
    {
        if (mOUi.Runes != null)
            mOUi.Runes.ClearRuneShelf();
    }

    public void LookAtRune(int piRune)
    {
        if (mOUi.Runes != null)
            mOUi.Runes.LookAtRune(piRune);
    }

    public void CastShelf()
    {
        mOUi.Actions.CastShelf();
    }

    public void CastSequence(int piSequence)
    {
        mOUi.Actions.CastSequence(piSequence);
    }

    /// <summary>
    /// The spell's own picture in SPELLS.GR (only the lasting effects have one), a projectile's missile, else null -
    /// UWModernRunePanel.SpellIcon's rule until part U4c step 2 of the UW2 plan, the same code. The table's minor class
    /// carries the duration in its upper bits; the picture goes by the effect alone (Light: 0x0E + 3 = 17, the torch).
    /// SPELLS.GR has only 21 pictures. A projectile shows its missile (per user, 2026-10-04): the fireball, the
    /// lightning bolt, the magic missile from OBJECTS.GR.
    /// </summary>
    public UWTexture SpellPicture(int piSequence)
    {
        DataImport lOData = mOUi.mOUWData;

        if (lOData == null || !UWRunicMagic.TryGetSpell(piSequence, out UWRunicMagic.Spell lOSpell))
            return null;

        if (lOSpell.MajorClass == UWSpellCasting.ProjectileMajorClass)
        {
            int liMissile = UWRunicMagic.GetProjectileId(UWRunicMagic.GetEffectMinor(lOSpell.MinorClass));

            return liMissile >= 0 ? lOData.Textures.GetTextureByType(UWTexture.TextureTypes.OBJECTS, liMissile) : null;
        }

        int liIcon = UWRunicMagic.GetIconIndex(lOSpell.MajorClass, fEffectMinor(lOSpell));
        List<UWTexture> lOIcons = lOData.Textures.GetTexturesByType(UWTexture.TextureTypes.SPELLS);

        return liIcon < UWRunicMagic.NoIcon && lOIcons != null && liIcon >= 0 && liIcon < lOIcons.Count ? lOIcons[liIcon] : null;
    }

    /// <summary>
    /// The minor class the spell's lasting effect shows its picture by. Class 11's minor class in the table is the
    /// spell's own number (UWMiscSpellRules), not an effect: only four of them last and have a picture (Speed, Roaming
    /// Sight, Telekinesis, Freeze Time, by the effect they add); the rest have none (per user, 2026-10-04: Remove Trap,
    /// Open, Cure Poison ... showed wrong ones).
    /// </summary>
    private static int fEffectMinor(UWRunicMagic.Spell pOSpell)
    {
        int liMinor = UWRunicMagic.GetEffectMinor(pOSpell.MinorClass);

        if (pOSpell.MajorClass != UWMiscSpellRules.MajorClass)
            return liMinor;

        switch (liMinor)
        {
            case UWMiscSpellRules.SpeedSpell: return UWMiscSpellRules.SpeedEffectMinor;
            case UWMiscSpellRules.RoamingSightSpell: return UWMiscSpellRules.RoamingSightEffectMinor;
            case UWMiscSpellRules.TelekinesisSpell: return UWMiscSpellRules.TelekinesisEffectMinor;
            case UWMiscSpellRules.FreezeTimeSpell: return UWMiscSpellRules.FreezeTimeEffectMinor;
            default: return UWRunicMagic.NoIcon;
        }
    }

    /// <summary>The hidden spells learned in the game (GitHub issue 19, UWKnowledge): all of them with "Show every
    /// mantra and spell" or outside the first game, as the rune panel worked it out itself until stage U3.</summary>
    public int ShownHiddenSpells
    {
        get
        {
            int[] laHidden = UWKnowledgeSources.HiddenSpells;
            int liAll = (1 << laHidden.Length) - 1;

            if (UWUserSettings.KnowledgeShowAll || UWGameSession.Current == null || UWGameSession.Current.Game.Game != UWGame.Uw1)
                return liAll;

            UWPlayerData lOBag = fBag();
            int liShown = 0;

            for (int liAt = 0; liAt < laHidden.Length; liAt++)
            {
                if (UWKnowledge.Current.SpellState(laHidden[liAt], lOBag, out UWKnowledge.Entry _) == UWKnowledge.SpellStateEnum.Known)
                    liShown |= 1 << liAt;
            }

            return liShown;
        }
    }

    // ------------------------------------------------- IUWModernMessages

    public IReadOnlyList<string> Lines => mOInteraction.ActiveStrings;

    public IReadOnlyList<float> LineTimes => mOInteraction.ActiveStringTimes;

    public long LinesSaid => mOInteraction.ActiveStringsSaid;

    public long LinesCleared => mOInteraction.ActiveStringsCleared;

    public bool IsWaitingForPage => mOInteraction.IsWaitingForPage;

    public int PromptLine => mOInteraction.PromptLineIndex;

    public bool ShowsPromptCursor => mOInteraction.ShowsPromptTextCursor;

    public bool IsAskingYesNo => mOInteraction.IsAskingYesNo;

    public string YesNoQuestion => mOInteraction.YesNoQuestion;

    public void AnswerYesNo(bool pbYes)
    {
        mOInteraction.AnswerYesNo(pbYes);
    }

    public bool IsChanting => mOInteraction.IsChanting;

    public string MantraPrompt => UWShrine.Prompt;

    public string MantraText => mOInteraction.MantraText;

    public void FinishMantra(bool pbCancel)
    {
        mOInteraction.FinishMantra(pbCancel);
    }

    public bool IsPromptActive => mOInteraction.IsPromptActive;

    public void Say(string psMessage)
    {
        mOInteraction.AddMessage(psMessage);
    }

    public void SayGeneral(int piIndex)
    {
        mOInteraction.AddGeneralMessage(piIndex);
    }

    public string General(int piIndex)
    {
        return mOInteraction.GetGeneralMessage(piIndex);
    }

    // ------------------------------------------------- IUWModernWorld

    public bool IsMapOpen => mOUi.Map.IsMapVisible;

    public bool IsConversationOpen => UWConversationController.IsAnyOpen;

    public string TargetName => mOInteraction.GetCrosshairTargetName();

    public bool TryGetTargetLock(out int piKeyId)
    {
        return mOInteraction.GetCrosshairLock(out piKeyId);
    }

    public string TargetPadVerb => mOInteraction.GetCrosshairPadVerb();

    public float UseHoldProgress => mOInteraction.ModernUseHoldProgress;

    public bool IsFightMode => mOInteraction.IsCombatModeActive;

    public void ToggleFightMode()
    {
        mOInteraction.ToggleCombatMode();
    }

    public bool IsSpellAiming => mOInteraction.IsSpellTargeting;

    public void CancelSpellAim()
    {
        mOInteraction.CancelPendingSpell();
    }

    public void UseThing(UWEntityInfo pOTarget, UWEntityInfo pOPickableBehind)
    {
        mOInteraction.ModernUseThing(pOTarget, pOPickableBehind);
    }

    public void LookAt(UWEntityInfo pOTarget)
    {
        mOInteraction.LookAtTarget(pOTarget, null);
    }

    public bool TakeToPointer(UWEntityInfo pOTarget)
    {
        return UWWorldPickup.Take(mOInteraction, pOTarget, UWWorldPickup.WholeStack, UWWorldPickup.Destination.Pointer);
    }

    public bool CarriesMap => mOUi.IsCarryingMap;

    public bool OpenMap()
    {
        return mOUi.OpenBigMap();
    }

    public IUWMinimapPictures Automap => mOUi.AutomapPictures;

    public int CompassStep => mOUi.Compass.CurrentStep;

    /// <summary>The eyes of the frame's top edge (UWEyesDisplay, the one the hits light through Interaction), as the
    /// heading strip read them itself until part U4d step 2 of the UW2 plan.</summary>
    public UWModernFoeEyes FoeEyes
    {
        get
        {
            UWEyesDisplay lOEyes = mOInteraction != null ? mOInteraction.Eyes : null;

            if (lOEyes == null)
                return default(UWModernFoeEyes);

            return new UWModernFoeEyes
            {
                IsLit = lOEyes.IsLit,
                Picture = lOEyes.CurrentFrame,
                Alpha = lOEyes.Alpha,
                Name = lOEyes.TargetName,
                Group = Mathf.Clamp(lOEyes.Group, 0, 2),
                TopInHead = UWModernHudArt.EyesTopInBrand
            };
        }
    }

    public void Track()
    {
        mOUi.Actions.Track();
    }

    public void Camp()
    {
        mOUi.Actions.Camp();
    }

    public bool RefusesMenu()
    {
        return mOUi.Options != null && mOUi.Options.RefusesToOpen();
    }

    public bool RefusesToSave()
    {
        return mOUi.Options != null && mOUi.Options.RefusesToSave();
    }

    public void ShowPointerForBags(bool pbShow)
    {
        mOUi.PointerCursor.ShowSystemPointerForBags(pbShow);
    }

    public bool IsMouseInWorld => mOInteraction.MouseInWorld();

    public bool IsPointerFree => mOInteraction.IsPointerFree();

    /// <summary>The first game holds its windows by its own means (a waiting spell, the help).</summary>
    public bool HoldsPanels => false;

    public bool CanCamp => UWGameActions.CanCamp;

    public bool OffersCamp => true;

    public bool OffersTrack => true;

    public string SaveSlotName(int piSlot)
    {
        return UWSaveFlow.SlotName(piSlot);
    }

    public bool HasSave(int piSlot)
    {
        return UWSaveFlow.Exists(piSlot);
    }

    public string WriteSave(int piSlot, string psName)
    {
        return UWSaveFlow.Write(piSlot, psName);
    }

    public void ReportSave(string psError)
    {
        UWSaveFlow.ReportSave(mOInteraction, mOUi.mOUWData, psError, false);
    }

    public void ReportNoSave()
    {
        UWSaveFlow.ReportNoSaveGame(mOInteraction);
    }

    public void LoadSave(int piSlot)
    {
        UWSaveFlow.Restore(mOInteraction, piSlot);
    }

    public void QuitToMainMenu()
    {
        UWSaveFlow.QuitToMainMenu();
    }

    // ------------------------------------------------- IUWModernArt

    /// <summary>The first game's shelf, stand and masks, cut from its frame (UWModernHudArt), as the vitals built
    /// them themselves until stage U4.</summary>
    public UWModernVitalsArt BuildVitalsArt(bool pbGemApart)
    {
        DataImport lOData = mOUi.mOUWData;
        UWModernVitalsArt lOArt = new UWModernVitalsArt();

        lOArt.Shelf = UWModernHudArt.BuildShelf(lOData.Textures, mOUi.TextureFilterMode, out lOArt.Layout, pbGemApart);
        lOArt.GemStand = UWModernHudArt.BuildGemStand(lOData.Textures, mOUi.TextureFilterMode, out lOArt.GemOnStand);
        lOArt.FlaskMask = UWModernHudArt.BuildFlaskMask(lOData.Flasks);
        lOArt.FreeFlaskMask = UWModernHudArt.BuildFreeFlaskMask(lOData.Flasks);
        lOArt.GemMask = UWModernHudArt.BuildGemMask(lOData.PowerGem);

        return lOArt;
    }

    public Texture2D Crosshair(bool pbLocked)
    {
        return mOUi.PointerCursor.CrosshairPicture(mOInteraction, pbLocked);
    }

    public Texture2D Compass(int piStep, bool pbFreed)
    {
        return mOUi.Compass.BuildComposite(piStep, mOUi.TextureFilterMode, pbFreed);
    }

    public Sprite ViewPicture => mOUi.Pictures.CurrentWindowPicture;

    public Sprite DragonSprite(int piIndex)
    {
        return mOUi.Classic.GetDragonSprite(piIndex);
    }

    public int FigureBody => mOCharacter.CharIndex;

    public int FigureVersion => mOCharacter.Paperdoll.LookVersion;

    public int FigureLegsDrop => mOCharacter.Paperdoll.LegsDrop;

    public UWTexture FigureSource(UWModernFigurePiece pePiece)
    {
        UWPaperdollView lOFigure = mOCharacter.Paperdoll;

        switch (pePiece)
        {
            case UWModernFigurePiece.Helmet: return lOFigure.HelmetSource;
            case UWModernFigurePiece.Gloves: return lOFigure.GlovesSource;
            case UWModernFigurePiece.Legs: return lOFigure.LegsSource;
            case UWModernFigurePiece.Chest: return lOFigure.ChestSource;
            case UWModernFigurePiece.Boots: return lOFigure.BootsSource;
            default: return null;
        }
    }

    public Texture2D FigureTexture(UWModernFigurePiece pePiece)
    {
        UWPaperdollView lOFigure = mOCharacter.Paperdoll;

        switch (pePiece)
        {
            case UWModernFigurePiece.Body: return lOFigure.CharTexture;
            case UWModernFigurePiece.Helmet: return lOFigure.HelmetTexture;
            case UWModernFigurePiece.Gloves: return lOFigure.GlovesTexture;
            case UWModernFigurePiece.Legs: return lOFigure.LegsArmorTexture;
            case UWModernFigurePiece.Chest: return lOFigure.ChestArmorTexture;
            case UWModernFigurePiece.Boots: return lOFigure.BootsTexture;
            default: return null;
        }
    }

    public Texture2D HealthFlask => mOCharacter.Flasks.HealthFlaskTeture;

    public Texture2D ManaFlask => mOCharacter.Flasks.ManaFlaskTeture;

    public Texture2D PowerGem => mOCharacter.Weapon.PowerGemTeture;

    public bool TryGetWeapon(out UWModernWeaponFrame pOFrame)
    {
        pOFrame = default;

        UWWeaponView lOWeapon = mOCharacter.Weapon;
        Texture2D lOTexture = lOWeapon.CurrentWeaponTexture;

        if (!lOWeapon.DrawWWeapon || lOTexture == null)
            return false;

        WeaponCoordinate lOOffset = lOWeapon.GetWeaponOffset();

        pOFrame.Texture = lOTexture;
        // The first game's view as the weapon's frames are measured in it: 172 columns, 112 rows (the weapon
        // view's own constants until stage U4 part A step 3).
        pOFrame.ViewWidth = 172f;
        pOFrame.ViewHeight = 112f;
        pOFrame.Size = lOWeapon.CurrentWeaponSize;
        pOFrame.OffsetX = lOOffset.X;
        pOFrame.OffsetY = lOOffset.Y;
        pOFrame.Sway = mOUi.WeaponSway.Offset();
        pOFrame.Ready = lOWeapon.CurrentReadyWeaponTime;
        pOFrame.Extended = lOWeapon.CurrentWeaponExtended;
        pOFrame.ExtendedLeft = lOWeapon.CurrentWeaponExtendedLeft;
        pOFrame.ExtendedTop = lOWeapon.CurrentWeaponExtendedTop;
        pOFrame.ExtendedSize = lOWeapon.CurrentWeaponExtendedSize;

        return true;
    }
}
