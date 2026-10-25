using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE HOST OF ONE ITEM USE (cleanup step 5.15, 2026-10-22: out of UWItemDrag, which was the host
/// of every use and learned where the used piece came from through three side fields - mOUsedItem,
/// mbConsumeFromCursor and mbConsumeAnywhere - set before and cleared after each call). Each use
/// now gets a host of its own that knows the piece and where it is taken from (ConsumptionEnum);
/// what all uses share - the components and the wand's cooldown - is Services, built once by
/// UWItemDrag.
///
/// Everything that takes effect immediately on click instead of waiting for a target - the rules
/// are in UWItemUse (P3 of the engine separation, 2026-09-18); this class is its host and knows
/// where the used piece came from.
/// </summary>
public sealed class UWItemUseHost : IUWItemUseHost
{
    /// <summary>Where the used piece is taken from when the rules use it up (ConsumeUsedItem).</summary>
    public enum ConsumptionEnum
    {
        /// <summary>Nothing carried is used: the incense block burnt where it lies in the world
        /// (UWWorldUse), whose redrawing is the caller's.</summary>
        Nothing,

        /// <summary>From the classic click's source slot (UWDragSource.Consume).</summary>
        FromSource,

        /// <summary>Used off the CURSOR (the drop on the head): the piece hangs on the pointer and
        /// no longer in the slot it came from, so taking it out of that slot would delete whatever
        /// moved there in the meantime.</summary>
        FromCursor,

        /// <summary>Used from the modern scheme (UWCarriedItemUse): no click source is remembered,
        /// the piece leaves from wherever it lies.</summary>
        Anywhere
    }

    /// <summary>What every use shares: the components on the camera and the wand's cooldown.</summary>
    public sealed class Services
    {
        public readonly Component Owner;

        public readonly UWInventory Inventory;

        public readonly UWGameUI GameUi;

        public readonly Interaction Interaction;

        /// <summary>The game data, known from UWItemDrag.Start.</summary>
        public DataImport Data;

        /// <summary>For this long a wand cannot be used again after a use.
        /// It applies GLOBALLY and not per wand: after a use a
        /// second wand is also briefly dead. A RUNE spell is exempt and works immediately - it
        /// goes through UWGameUI.fCastSelectedRunes and never passes here (all per
        /// user, checked in the original, 2026-09-05).
        ///
        /// THE LENGTH IS THE ORIGINAL'S since 2026-09-24: CastSpellFromObject_seg040_1F04 sets its
        /// timer to the game clock plus 0x2FD, and the clock runs at 256 units a second
        /// (UWPlayerTick.PitTicksPerSecond), so just under three seconds; ours was an estimated two.</summary>
        private const float WandCooldownSeconds = WandCooldownClockUnits / (float)UWPlayerTick.PitTicksPerSecond;

        private const int WandCooldownClockUnits = 0x2FD;

        private float mfNextWandUseTime;

        public Services(Component pOOwner, UWInventory pOInventory, UWGameUI pOGameUi, Interaction pOInteraction)
        {
            Owner = pOOwner;
            Inventory = pOInventory;
            GameUi = pOGameUi;
            Interaction = pOInteraction;
        }

        public bool IsWandCoolingDown => Time.time < mfNextWandUseTime;

        public void StartWandCooldown()
        {
            mfNextWandUseTime = Time.time + WandCooldownSeconds;
        }

        /// <summary>The item use's presentation (cleanup step 3.1, 2026-10-17): its lines go to this
        /// component's Interaction, or nowhere without one, as the two message members did; the flash,
        /// the cutscene and the refusal sound go through it too.</summary>
        public IUWPresentation Presentation => UWPresentation.For(UWScene.World, Interaction);

        /// <summary>Goes to sleep - see UWSleep. The bedroll stays in the
        /// backpack, it is not consumed.</summary>
        public bool Sleep(bool pbBedroll)
        {
            if (Interaction == null)
                return false;

            UWSleep.Sleep(Data, UWScene.Character, Interaction,
                UWScene.LevelLoader, false, pbBedroll);

            return true;
        }
    }

    private readonly Services mOServices;

    /// <summary>The item currently being used, for ConsumeUsedItem.</summary>
    private readonly UWObject mOUsedItem;

    private readonly ConsumptionEnum meConsumption;

    private readonly UWDragSource mOSource;

    /// <param name="pOSource">The classic click's source (FromSource only).</param>
    public UWItemUseHost(Services pOServices, UWObject pOItem, ConsumptionEnum peConsumption, UWDragSource pOSource)
    {
        mOServices = pOServices;
        mOUsedItem = pOItem;
        meConsumption = peConsumption;
        mOSource = pOSource;
    }

    private UWInventory fInventory => mOServices.Inventory;

    private Interaction fInteraction => mOServices.Interaction;

    // ------------------------------------------------- IUWItemUseHost

    public void ConsumeUsedItem()
    {
        if (mOUsedItem == null || meConsumption == ConsumptionEnum.Nothing)
            return;

        // Used from the modern scheme (UWCarriedItemUse): no click source is remembered, the piece
        // leaves from wherever it lies.
        if (meConsumption == ConsumptionEnum.Anywhere)
        {
            if (UWStackRules.TryTakeOne(mOUsedItem))
            {
                fInventory.NotifyChanged();

                return;
            }

            fInventory.RemoveItem(mOUsedItem);

            return;
        }

        // Used off the cursor (the drop on the head): the piece hangs on the pointer and no
        // longer in the slot it came from, so taking it out of that slot would delete whatever
        // moved there in the meantime.
        if (meConsumption == ConsumptionEnum.FromCursor)
        {
            if (UWStackRules.TryTakeOne(mOUsedItem))
                return;

            fInventory.TakeCursorItem();

            return;
        }

        mOSource.Consume(fInventory.Model, mOUsedItem);
    }

    public void AddGeneralMessage(int piIndex)
    {
        mOServices.Presentation.AddGeneralMessage(piIndex);
    }

    public void AddMessage(string psMessage)
    {
        mOServices.Presentation.AddMessage(psMessage);
    }

    /// <summary>The exploding book's curse on the player, through the world since cleanup step 3.3
    /// (2026-10-17).</summary>
    public void CursePlayer(int piDice)
    {
        UWWorld lOWorld = UWScene.World;

        if (lOWorld != null)
            lOWorld.Damage(UWDamageTarget.ThePlayer, UWBlow.ToPlayer(UWDamageRequest.Curse(piDice)));
    }

    public int PlayerDamageTypeProof => UWScene.Character != null ? UWScene.Character.DamageTypeProof : 0;

    public void FlashWindow(int piPaletteColour, float pfSeconds)
    {
        mOServices.Presentation.FlashWindow(piPaletteColour, pfSeconds);
    }

    public void PlayCutscene(int piCutscene)
    {
        mOServices.Presentation.PlayCutscene(piCutscene);
    }

    /// <summary>Do NOT fire immediately: on the click the pointer is over the inventory, not in
    /// the view window. A projectile therefore waits for the target click as with rune casting -
    /// exactly as the original does it (per user, 2026-09-05).</summary>
    public bool CastSpellFromObject(int piSpellIndex)
    {
        return mOServices.GameUi != null && mOServices.GameUi.Runes.CastSpellFromObject(piSpellIndex, false);
    }

    public bool CastSpellByClass(int piMajorClass, int piMinorClass)
    {
        return mOServices.GameUi != null && mOServices.GameUi.Runes.CastSpellByClass(piMajorClass, piMinorClass);
    }

    public bool IsWandCoolingDown => mOServices.IsWandCoolingDown;

    public void StartWandCooldown()
    {
        mOServices.StartWandCooldown();
    }

    public void PlayMagicItemRefused()
    {
        mOServices.Presentation.PlaySoundAtPlayer(UWSoundEffectId.MagicItemRefused);
    }

    /// <summary>IUWItemUseHost: the bedroll used - a night in it.</summary>
    public bool TrySleep()
    {
        return mOServices.Sleep(true);
    }

    public void SleepPassedOut()
    {
        if (fInteraction == null)
            return;

        // PASSED OUT, not gone to bed: the original asks nothing then - not the ground, not
        // the enemies - and lets the surface do its work instead (see UWSleepRules.Sleep).
        UWSleep.Sleep(mOServices.Data, UWScene.Character, fInteraction,
            UWScene.LevelLoader, true);
    }

    public bool TryPlantSeed()
    {
        return UWSilverTree.TryPlant(UWScene.LevelLoader, mOServices.Owner.transform, fInteraction);
    }

    public bool IsInstrument(int piObjectId)
    {
        return UWInstrumentPlayer.IsInstrument(piObjectId);
    }

    public bool PlayInstrument(UWObject pOItem)
    {
        return UWInstrumentPlayer.Begin(fInteraction != null ? fInteraction.gameObject : mOServices.Owner.gameObject, pOItem);
    }

    public bool TryFish()
    {
        return UWFishing.TryFish(UWScene.LevelLoader, mOServices.Owner.transform, fInventory, fInteraction);
    }
}
