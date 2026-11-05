using UnityEngine;
using UWDataImport;
using UWDataImport.UWData;

/// <summary>
/// THE SECOND GAME'S PLAYER ON THE CLOCK (UW2 plan phase 4 step 1): UWPlayerVitals, the first game's rule class, runs
/// here from the character in the record (UWPlayerData.FromSecondGame), its tick on the same clock. The record stays
/// the save's truth: after every tick the numbers go into it, so the flasks, the conversations and the save read
/// them as before, and what a conversation wrote there comes back before the next tick. It is also the player as the
/// creatures meet him (UWCreatureWorld): his body, his armour by the first game's status pass over the
/// inventory, the defender of a blow, and his noise: creatures hear before they see. Death is UWDeathUw2's.
/// </summary>
public sealed class UWVitalsUw2 : MonoBehaviour, IUWVitalsHost
{
    public static UWVitalsUw2 Current { get; private set; }

    private readonly UWTickClock mOClock = new UWTickClock(UWPlayerTick.Seconds);

    private UWPlayerVitals mOVitals;

    private UWPlayerFile mOFile;

    private DataImport mOData;

    private readonly UWArmourProtection.Status mOStatus = new UWArmourProtection.Status();

    private Defender mODefender;

    /// <summary>The player as a blow's defender, his status refreshed first.</summary>
    public IUWAttackDefender Combatant
    {
        get
        {
            RefreshStatus();

            return mODefender ?? (mODefender = new Defender(this));
        }
    }

    /// <summary>What he wears and has cast, as the first game's status pass reads it.</summary>
    public void RefreshStatus()
    {
        UWInventoryModel lOInventory = UWGameScreenUw2.Current != null ? UWGameScreenUw2.Current.Inventory : null;

        if (mOVitals != null)
            UWArmourProtection.Compute(lOInventory, mOData, mOVitals.ActiveSpells, mOStatus);

        // The light of a spell or a worn thing (FixPlayerEquips), the larger counts in UWLighting.
        if (UWScene.Lighting != null)
            UWScene.Lighting.MagicBrightness = mOStatus.Brightness;
    }

    /// <summary>The eye: the camera, as UWCharacter sits on it in the first game.</summary>
    public Transform EyeTransform => UWViewpoint.Current != null ? UWViewpoint.Current.transform : null;

    public int FeetZPos => UWScene.PlayerMovement != null ? UWScene.PlayerMovement.FeetZPos : 0;

    /// <summary>COMOBJ.DAT's height of the adventurer.</summary>
    public int BodyHeightZ => mOData != null && mOData.CommonObjectProperties != null
        && mOData.CommonObjectProperties.TryGet(UWObjectMechanics.AdventurerObjectId, out UWCommonObjectProperties.Entry lOEntry) ? lOEntry.Height : 0;

    /// <summary>The weapon's skill's half counts for the defence (UWPlayerCritterRow).</summary>
    private int fDefence => UWPlayerCritterRow.GetDefence(mOVitals.Defence, mOVitals.GetSkill(mOStatus.WeaponSkill));

    /// <summary>The player as UWAttackResolution asks him (UWPlayerCombatant is the first game's, over UWCharacter).</summary>
    private sealed class Defender : IUWAttackDefender
    {
        private readonly UWVitalsUw2 mOOwner;

        public Defender(UWVitalsUw2 pOOwner)
        {
            mOOwner = pOOwner;
        }

        public UWDefenderKind DefenderKind => UWDefenderKind.Player;

        public int Defence => mOOwner.fDefence;

        public int ProtectionAt(int piPart)
        {
            return piPart >= 0 && piPart < mOOwner.mOStatus.Protection.Length ? mOOwner.mOStatus.Protection[piPart] : 0;
        }

        public int ArmourAt(int piPart)
        {
            return mOOwner.GetArmourAt(piPart);
        }

        public bool IsArmourMetalAt(int piPart)
        {
            UWInventoryModel lOInventory = UWGameScreenUw2.Current != null ? UWGameScreenUw2.Current.Inventory : null;

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

        /// <summary>The piece of the part wears by 2d4 (UWPlayerMelee.WearOnCriticalHit).</summary>
        public void OnCriticalHit(int piPart)
        {
            if (UWAttackUw2.Current != null)
                UWAttackUw2.Current.WearOnCriticalHit(piPart);
        }
    }

    private bool mbSpellsChanged;

    /// <summary>Paralysed (the game's counter); a cast waits for it.</summary>
    public bool IsParalysed => mOFile != null && UWPlayerRecordUw2.Paralysis(mOFile) != 0;

    /// <summary>The vitals, null before the first frame.</summary>
    public UWPlayerVitals Vitals => mOVitals;

    private void Awake()
    {
        Current = this;
    }

    private void OnDestroy()
    {
        if (Current == this)
            Current = null;
    }

    private void Update()
    {
        UWPlayerFile lOFile = UWGameSession.Current != null ? UWGameSession.Current.PlayerFileUw2 : null;
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOFile == null || lOLoader == null || lOLoader.UWDataImporter == null)
            return;

        if (lOFile != mOFile)
            fStart(lOFile, lOLoader.UWDataImporter);

        fTakeFromRecord();

        int liTicks = mOClock.Advance(Time.deltaTime);

        UWInventoryModel lOInventory = UWGameScreenUw2.Current != null ? UWGameScreenUw2.Current.Inventory : null;

        for (int liTick = 0; liTick < liTicks; liTick++)
        {
            mOVitals.RunGameTick(UWDataImport.UWData.UWGameClock.UnitsPerTick, DamageTypeProof);

            // The lights burn on the same tick and its counter (duration_check, PLAYTIME.C:88).
            if (lOInventory != null)
                lOInventory.BurnTick(1, mOVitals.TickInCycle);
        }

        // A spell began or ended, by its time or in the night: the record and the icons follow.
        if (mbSpellsChanged)
        {
            mbSpellsChanged = false;
            UWPlayerRecordUw2.WriteActiveSpells(mOFile, mOVitals.ActiveSpells);

            if (UWGameScreenUw2.Current != null)
                UWGameScreenUw2.Current.ShowRuneState();
        }

        if (liTicks > 0)
        {
            RefreshStatus();
            mOVitals.StealthBonus = mOStatus.StealthBonus;
            WriteToRecord();
        }

        fTickQuietness();
    }

    /// <summary>The world's game time at the last frame, in PIT ticks.</summary>
    private long mlLastNoiseTicks;

    /// <summary>The noise on the game's clock, as UWCharacter's.</summary>
    private void fTickQuietness()
    {
        long llNow = UWGameSession.Current.Schedule.Now;
        int liTicks = llNow >= mlLastNoiseTicks ? (int)System.Math.Min(int.MaxValue, llNow - mlLastNoiseTicks) : 0;

        mlLastNoiseTicks = llNow;
        mOVitals.TickQuietness(liTicks);
    }

    private void fStart(UWPlayerFile pOFile, DataImport pOData)
    {
        mOFile = pOFile;
        mOData = pOData;
        mOVitals = new UWPlayerVitals(this);
        mOVitals.MessageDue += UWGameScreenUw2.Say;

        // At once: a blow lands between the ticks, and the record is what the flasks read and what comes back
        // before the next tick (fTakeFromRecord).
        mOVitals.VitalsChanged += WriteToRecord;
        mOVitals.ExperienceChanged += fExperienceChanged;
        mOVitals.CombatShakeDue += UWScreenShake.ShakeOnDamage;
        mOVitals.UnsteadyShakeDue += fShakeUnsteady;
        mOVitals.DayStepDue += fStepDay;
        mOVitals.StatusPassDue += RefreshStatus;
        mOVitals.ActiveSpellsChanged += (pbHastened, pbRoamingSight, pbTimeFrozen, pbTelekinesis) => mbSpellsChanged = true;
        UWCombatVariantUw2.PlayerArmour = GetArmourAt;
        mOVitals.Initialise(pOData, UWPlayerVitals.StartValues.Default, UWPlayerData.FromSecondGame(pOFile));
    }

    /// <summary>The hour; the schedules that run on it come with phase 4's list item 6.</summary>
    private void fStepDay()
    {
        UWPlayerRecordUw2.StepDayClock(mOFile);
    }

    /// <summary>A failed sip, or waking from the drink.</summary>
    private void fShakeUnsteady(int piDuration)
    {
        UWScreenShake lOShake = UWScreenShake.Ensure();

        if (lOShake != null)
            lOShake.Shake(UWScreenShake.LargeChannel, piDuration);
    }

    /// <summary>Written at once as well (per user, 2026-11-01: a kill gave nothing until the next tick, as the
    /// sum comes after KillRewarded). A level's maxima only then: the record's may have moved in a conversation.</summary>
    private void fExperienceChanged(bool pbLevel)
    {
        if (pbLevel)
            UWPlayerRecordUw2.WriteMaxima(mOFile, mOVitals.MaxHP, mOVitals.MaxMana);

        WriteToRecord();
    }

    /// <summary>A conversation wrote into the record (play_hp and its kin): the vitals take it over.</summary>
    private void fTakeFromRecord()
    {
        int liHunger = mOFile.Record[UWPlayerRecordUw2.HungerOffset];
        int liHp = UWPlayerRecordUw2.Vitality(mOFile);
        int liMana = UWPlayerRecordUw2.Mana(mOFile);
        int liPoison = UWPlayerRecordUw2.Poison(mOFile);

        if (liHunger != mOVitals.Hunger || liHp != mOVitals.CurrentHP || liMana != mOVitals.CurrentMana || liPoison != mOVitals.Poison)
            mOVitals.ApplyConversationValues(liHunger, liHp, liMana, liPoison);

        if (UWPlayerRecordUw2.GameClock(mOFile) != mOVitals.ClockValue)
            mOVitals.TakeClock(UWPlayerRecordUw2.GameClock(mOFile));
    }

    /// <summary>The numbers as they stand into the record (before a save too).</summary>
    public void WriteToRecord()
    {
        if (mOFile == null || mOVitals == null)
            return;

        UWPlayerRecordUw2.WriteVitals(mOFile, mOVitals.Hunger, mOVitals.CurrentHP, mOVitals.CurrentMana, mOVitals.Poison, mOVitals.Fatigue,
            mOVitals.MealHealCounter, mOVitals.Counter3C, mOVitals.ClockValue);
        UWPlayerRecordUw2.WriteDrink(mOFile, mOVitals.Intoxication, mOVitals.Hallucination);
        UWPlayerRecordUw2.WriteActiveSpells(mOFile, mOVitals.ActiveSpells);
        UWPlayerRecordUw2.WriteExperience(mOFile, mOVitals.Experience, mOVitals.Level, mOVitals.SkillPoints, mOVitals.SkillPointsTotal);
    }

    // ------------------------------------------------- IUWVitalsHost

    public int DungeonLevelIndex => UWScene.LevelLoader != null ? UWScene.LevelLoader.CurrentLevelIndex : -1;

    /// <summary>The tile column of the walk's eye, -1 without a level.</summary>
    public int PlayerTileX => UWScene.LevelLoader != null && UWScene.LevelLoader.CurrentLevel != null && UWViewpoint.Current != null
        ? UWScene.LevelLoader.WorldPositionToTile(UWViewpoint.Current.transform.position).X : -1;

    public int WornRegeneration => mOStatus.RegenerationBits;

    public bool IsMoving => UWScene.PlayerMovement != null && UWScene.PlayerMovement.IsMoving;

    public float MovementFraction => UWScene.PlayerMovement != null ? UWScene.PlayerMovement.MomentumFraction : 0f;

    public bool IsInMotionState => false;

    public bool IsChargingAttack => false;

    public int GetArmourAt(int piPart)
    {
        return piPart >= 0 && piPart < mOStatus.Armour.Length ? mOStatus.Armour[piPart] : 0;
    }

    public int DamageTypeProof => mOStatus.DamageTypeProof;

    /// <summary>The Valor spell's bonus to the attack score, 10 + Casting / 5 (uw2 PLAYDATA.C:222).</summary>
    public int Valor => mOStatus.HasValor && mOVitals != null ? ValorBase + (mOVitals.GetSkill(UWPlayerData.Skill.Casting) / ValorCastingDivisor) : 0;

    /// <summary>Poison Weapon's share of the damage in fortieths, Casting + 30 (COMBAT.C:325).</summary>
    public int PoisonWeaponShare => mOStatus.HasPoisonWeapon && mOVitals != null ? mOVitals.GetSkill(UWPlayerData.Skill.Casting) + PoisonWeaponBase : 0;

    private const int ValorBase = 10;

    private const int ValorCastingDivisor = 5;

    private const int PoisonWeaponBase = 30;
}
