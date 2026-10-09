using UnityEngine;
using System.Collections;
using UWDataImport.UWData;
using UWDataImport;

public class UWCharacter : MonoBehaviour, IUWVitalsHost
{
    private enum AttackStatusEnum
    {
        Seathed = 0,
        Seathing = 1,
        Readying = 2,
        Ready = 3,
        PowerUp = 4,
        PoweUpComplete = 5,
        Release = 6,

        /// <summary>Released too early: the wind-up motion runs backwards again without a
        /// blow being struck.</summary>
        PowerDown = 7
    }

    public FilterMode TextureFilterMode = FilterMode.Point;

    public int CharIndex = 9;

    public bool IsMale
    {
        get { return CharIndex < 5 ? true : false; }
    }

    public WeaponTypes CurrentWeaponType = WeaponTypes.RightHandSword;
    public AttackTypes CurrentAttackType = AttackTypes.Slash;

    private bool mIsCombatModeActive;
    /// <summary>
    /// A ranged weapon is NOT held up into view.
    ///
    /// In the original, with a bow or sling in hand you see nothing at all in the
    /// view window - no image, no wind-up motion (per user, 2026-09-10). Ours raised the
    /// axe instead, because a ranged weapon is missing from the melee table and the
    /// display therefore simply kept the previous weapon.
    ///
    /// This is kept up to date continuously while combat mode is active, see
    /// Interaction.fUpdateWeaponType.
    /// </summary>
    public bool HasRangedWeapon { get; set; }

    public bool DrawWWeapon
    {
        get { return !HasRangedWeapon
            && (mIsCombatModeActive || meAttackStatus == AttackStatusEnum.Seathing); }
    }

    private DataImport mOUWData;
    private Interaction mInteraction;
    private UWInventory mInventory;

    private UWWeaponAnimations mOWeapons;
    private AttackStatusEnum meAttackStatus;

    /// <summary>The button was released; the state decides what comes of it.</summary>
    private bool mbReleaseRequested;

    /// <summary>
    /// Reports that the button was released.
    ///
    /// During the wind-up this leads to a retraction from the current frame, afterwards to
    /// the blow. Whether the blow deals damage depends on the minimum charge and is decided
    /// in Interaction - the motion runs in any case.
    /// </summary>
    public void ReleaseAttack()
    {
        mbReleaseRequested = true;
    }

    /// <summary>Weapon to switch to after sheathing.</summary>
    private WeaponTypes mePendingWeapon;

    private bool mbHasPendingWeapon;
    private const int numPowerUpFrames = 4;
    private const int numReleaseFrames = 6;

    /// <summary>How many frames before the end the blow connects. See fGetConnectFrame.</summary>
    private const int DefaultFramesFromEnd = 2;

    /// <summary>
    /// The frame at which the blow connects. It is counted from the END of the animation, whose
    /// length is in the weapon data: an axe chop has four frames, a thrust six (the
    /// coordinate table ends with zero pairs, see UWWeaponAnimation.GetReleaseFrameCount).
    ///
    /// A fixed number therefore does not work - for the axe chop, frame 5 is already past the
    /// end of the motion, the blow would connect too late.
    /// </summary>
    private int fGetConnectFrame()
    {
        int liFromEnd = UnderworldRevisited.UWSettings.Instance != null
            ? UnderworldRevisited.UWSettings.Instance.AttackConnectFramesFromEnd
            : DefaultFramesFromEnd;

        int liLength = mCurrentWeaponAnimation != null
            ? mCurrentWeaponAnimation.GetReleaseFrameCount(CurrentAttackType)
            : numReleaseFrames;

        int liFrame = liLength - liFromEnd;

        return liFrame < 1 ? 1 : liFrame;
    }

    /// <summary>Reports the moment the blow connects. See the attack animation
    /// further below.</summary>
    public System.Action AttackConnected;
    public float CurrentReadyWeaponTime = 0f;
    private int mCurrentWeaponFrameIndex;
    private float mCurrentAttackAnimationDuration;
    private UWTexture mCurrentWeaponFrame;
    private UWWeaponAnimation mCurrentWeaponAnimation;
    private WeaponCoordinate mCurrentWeaponCoodinate;
    private UWPowerGem mPowerGem;
    private float mMaxAttackPowerAnimationTime;
    private UWFlasks mFlasks;

    public Texture2D CurrentWeaponTexture;
    public Texture2D CharTexture;
    public Texture2D HelmetTexture;
    public Texture2D GlovesTexture;
    public Texture2D ChestArmorTexture;
    public Texture2D LegsArmorTexture;
    public Texture2D BootsTexture;

    /// <summary>The armour pictures as the data has them (null when nothing is worn there), for the
    /// modern panel's figure on a back of its own (UWHudArt.ComposePaperdoll).</summary>
    public UWTexture HelmetSource;

    public UWTexture ChestSource;

    public UWTexture GlovesSource;

    public UWTexture LegsSource;

    public UWTexture BootsSource;

    /// <summary>How many rows lower the legs' picture is drawn (UWWearables.GetArmorDrop) - already
    /// moved inside LegsArmorTexture, to be added where LegsSource is drawn.</summary>
    public int LegsDrop;

    public Texture2D PowerGemTeture;
    public Texture2D HealthFlaskTeture;
    public Texture2D ManaFlaskTeture;

    // Fallback starting values of the character. A loaded save game or a newly created
    // character supplies health and mana itself (see Init); these Inspector fields apply
    // only without one. They do NOT come from the template in PLAYER.DAT ("GRONKEY"),
    // because it is incomplete - it has maximum health 0 and maximum mana 188, both
    // obviously unset (see UWPlayerData).
    [Header("Character values")]
    [SerializeField]
    private float mfStartMaxHP = 30f;

    [SerializeField]
    private float mfStartMaxMana = 20f;

    // Separate from the maximum values so that you can start wounded - otherwise the
    // flask is always full and healing (fountains, potions) shows no effect. -1 means
    // "full", which is the normal game start.
    [SerializeField]
    [Tooltip("Hit points at start. -1 means full.")]
    private float mfStartCurrentHP = -1f;

    [SerializeField]
    [Tooltip("Mana at start. -1 means full.")]
    private float mfStartCurrentMana = -1f;

    /// <summary>
    /// The vitals - health, mana, attributes, skills, experience, hunger, poison, the active
    /// spells and the game tick - live engine-free in UWPlayerVitals (P3 of the engine
    /// separation, 2026-09-18). This class hosts them (IUWVitalsHost) and forwards the old
    /// API; the flasks, the paper doll and the weapon stay here.
    /// </summary>
    private UWPlayerVitals mOVitals;

    private UWPlayerVitals fVitals => mOVitals ?? (mOVitals = new UWPlayerVitals(this));

    public float MaxHP => fVitals.MaxHP;

    public float CurrentHP => fVitals.CurrentHP;

    public float MaxMana => fVitals.MaxMana;

    public float CurrentMana => fVitals.CurrentMana;

    /// <summary>The vitals themselves, for the engine-free item use (UWItemUse).</summary>
    public UWPlayerVitals Vitals => fVitals;

    // Attributes and skills. Starting values come from the player data (DataImport.InitialPlayer:
    // a loaded save game, a newly created character, or otherwise PLAYER.DAT, see UWPlayerData).
    // Anyone who needs other values for testing sets them here in the Inspector; with -1 the
    // value from the player data is kept.
    [Header("Attributes (-1 = from PLAYER.DAT)")]
    [SerializeField]
    private int miStartStrength = -1;

    [SerializeField]
    private int miStartDexterity = -1;

    [SerializeField]
    private int miStartIntelligence = -1;

    [SerializeField]
    private int miStartAttack = -1;

    /// <summary>
    /// Level and the two skills that spellcasting depends on - for testing high spell
    /// circles without touching the save game.
    ///
    /// The castable circle is (level + 1) / 2, so a spell from circle seven needs at
    /// least level thirteen. The Mana skill together with Intelligence determines the
    /// mana pool, the Casting skill the success check.
    /// </summary>
    [Header("For testing (-1 = from PLAYER.DAT)")]
    [SerializeField]
    private int miStartLevel = -1;

    [SerializeField]
    private int miStartManaSkill = -1;

    [SerializeField]
    private int miStartCastingSkill = -1;

    public int Strength => fVitals.Strength;

    public int Dexterity => fVitals.Dexterity;

    public int Intelligence => fVitals.Intelligence;

    /// <summary>The character's attack value, a separate field next to the skills in
    /// PLAYER.DAT.</summary>
    public int Attack => fVitals.Attack;

    /// <summary>The Defence SKILL of PLAYER.DAT 0x22 - what the status page shows and what the
    /// save game stores (see UWPlayerVitals.Defence). For the hit check use DefenceCheck.
    /// </summary>
    public int Defence => fVitals.Defence;

    /// <summary>
    /// The value an attacking creature actually rolls against: byte 0x12 of the player's own
    /// creature row, the Defence skill PLUS HALF the skill of the weapon in hand
    /// (PlayerStatusUpdate_ovr133_784, labels 86A-938; see UWPlayerCritterRow.GetDefence).
    ///
    /// The two were the same in the port until 2026-09-20, which is why criticals against the
    /// player were far too frequent.
    /// </summary>
    public int DefenceCheck => UWPlayerCritterRow.GetDefence(fVitals.Defence,
        fVitals.GetSkill(mOStatus.WeaponSkill));

    /// <summary>Which weapon skill the defence takes half of - Unarmed with an empty hand.
    /// </summary>
    public UWPlayerData.Skill DefenceWeaponSkill => mOStatus.WeaponSkill;

    /// <summary>What equipment and active spells add up to - see
    /// UWArmourProtection.</summary>
    private readonly UWArmourProtection.Status mOStatus = new UWArmourProtection.Status();

    /// <summary>How much damage the armour absorbs at this body location.</summary>
    public int GetArmourAt(int piPart)
    {
        return piPart >= 0 && piPart < mOStatus.Armour.Length ? mOStatus.Armour[piPart] : 0;
    }

    /// <summary>How much protection an enchantment gives at this body location - this lowers
    /// the HIT CHANCE, not the damage (see UWArmourProtection).</summary>
    public int GetProtectionAt(int piPart)
    {
        return piPart >= 0 && piPart < mOStatus.Protection.Length ? mOStatus.Protection[piPart] : 0;
    }

    /// <summary>The resistance value of spell class 2. It is already contained in the four
    /// armour values; here it is listed separately only for display.</summary>
    public int DamageResistance => mOStatus.DamageResistance;

    /// <summary>Bit field of the immunities (class 3). Tracked, but has no effect yet -
    /// see UWArmourProtection.</summary>
    public int DamageTypeProof => mOStatus.DamageTypeProof;

    /// <summary>Bit field of the stealth bonuses (class 3): Stealth, Conceal, Invisibility.
    /// </summary>
    public int StealthBonus => mOStatus.StealthBonus;

    /// <summary>The dragon skin boots are worn - then lava does nothing (see
    /// UWPlayerTerrain and UWArmourProtection.Status).</summary>
    public bool HasDragonSkinBoots => mOStatus.HasDragonSkinBoots;

    /// <summary>The crown of maze navigation is worn - see UWMazeNavigation.</summary>
    public bool HasMazeNavigation => mOStatus.HasMazeNavigation;

    /// <summary>How many dice the worn curses roll - see ApplyCurse.
    /// </summary>
    public int CurseDice => mOStatus.CurseDice;

    /// <summary>Value from the previous frame, to detect the EQUIPPING of a cursed
    /// item.</summary>
    private int miPreviousCurseDice;

    /// <summary>Palette colour of the brief flash in the view window. The reference uses
    /// 0xA8 for uw1, 0x30 for uw2.</summary>
    private const int CurseFlashColour = 0xA8;

    private const float CurseFlashSeconds = 0.1f;

    /// <summary>
    /// A cursed piece of equipment strikes - the roll and the floor of three hit points are
    /// in UWPlayerVitals.ApplyCurse. pbFlash: when EQUIPPING nothing flashes, afterwards it
    /// does - that is how the reference distinguishes its two cases. In uw1 there is no
    /// message in either.
    /// </summary>
    public void ApplyCurse(int piDice, bool pbFlash)
    {
        if (fVitals.ApplyCurse(piDice) && pbFlash)
            OnCurseStruck();
    }

    /// <summary>IUWVitalsHost: the curse struck on the tick.</summary>
    public void OnCurseStruck()
    {
        UWGameUI lOUi = UWScene.GameUi;

        if (lOUi != null)
            lOUi.FlashWindowColour(CurseFlashColour, CurseFlashSeconds);
    }

    /// <summary>For maze navigation, which recolours the level geometry.</summary>
    private UWLevelLoader mOLevelLoaderForStatus;

    /// <summary>
    /// Recomputes the whole status - armour, protection, magic light and the
    /// movement abilities, from the worn equipment and the active spells.
    ///
    /// Runs every frame, just as the reference runs its status pass in its loop.
    /// That is cheaper than tracking every place where equipment might
    /// change - and it was the reason why an enchantment on a ring previously had
    /// no effect at all.
    /// </summary>
    public void RefreshStatus()
    {
        if (mOInventory == null)
            mOInventory = UWScene.Inventory;

        // The switch for the original's dead resistance nibble - see
        // UWArmourProtection.ResistBlowsAddsArmour.
        UWArmourProtection.ResistBlowsAddsArmour = UnderworldRevisited.UWSettings.Instance != null
            && UnderworldRevisited.UWSettings.Instance.ResistBlowsAddsArmour;

        UWArmourProtection.Compute(mOInventory, mOUWData, fVitals.ActiveSpells, mOStatus);

        // The stealth bits go straight on to the noise and visibility, as the original's
        // status pass lowers its two bytes (ApplyDefenceStealthBonuses_ovr133_65D).
        fVitals.StealthBonus = mOStatus.StealthBonus;

        if (mOLighting == null)
            mOLighting = UWScene.Lighting;

        if (mOLighting != null)
            mOLighting.MagicBrightness = mOStatus.Brightness;

        if (mOMovement == null)
            mOMovement = UWScene.PlayerMovement;

        if (mOMovement != null)
        {
            mOMovement.MagicalMotionAbilities = mOStatus.MotionAbilities;
            mOMovement.SpellMotionAbilities = mOStatus.SpellMotionAbilities;
        }

        if (mOLevelLoaderForStatus == null)
            mOLevelLoaderForStatus = UWScene.LevelLoader;

        if (mOLevelLoaderForStatus != null)
            mOLevelLoaderForStatus.SetMazeNavigation(mOStatus.HasMazeNavigation);

        // EQUIPPING HURTS IMMEDIATELY. The reference runs its status pass on every
        // inventory change with CastOnEquip set, and the curse strikes then just as it
        // does on the tick - only without the flash. We detect the case by more
        // curse dice being worn than in the frame before.
        if (mOStatus.CurseDice > miPreviousCurseDice)
            ApplyCurse(mOStatus.CurseDice, false);

        miPreviousCurseDice = mOStatus.CurseDice;
    }

    private UWInventory mOInventory;

    public int Experience => fVitals.Experience;

    public int Level => fVitals.Level;

    public int SkillPoints => fVitals.SkillPoints;

    /// <summary>All skill points ever earned - also stored in the save game.</summary>
    public int SkillPointsTotal => fVitals.SkillPointsTotal;

    /// <summary>Records the experience for a slain creature - see UWPlayerVitals. pbStrong is
    /// the creature's word 0x0D bit 10, which scales the reward.</summary>
    public void AwardKillExperience(UWObjectClassProperties.Critter pOCritter, bool pbStrong, int piDungeonLevel)
    {
        fVitals.AwardKillExperience(pOCritter, pbStrong, piDungeonLevel);
    }

    /// <summary>PLAYER.DAT 0xB4: creature blows and missiles do half damage on the easy
    /// difficulty (deviation 47) - see UWPlayerVitals.</summary>
    public bool IsEasyDifficulty => fVitals.IsEasyDifficulty;

    /// <summary>Experience from a conversation (new_player_exp) - see UWPlayerVitals.</summary>
    public void ChangeExperience(int piAmount, int piDungeonLevel)
    {
        fVitals.ChangeExperience(piAmount, piDungeonLevel);
    }

    /// <summary>Sets hunger, hit points, mana and poison as a conversation returns them at
    /// the end - see UWPlayerVitals.</summary>
    public void ApplyConversationValues(int piHunger, int piHp, int piMana, int piPoison)
    {
        fVitals.ApplyConversationValues(piHunger, piHp, piMana, piPoison);
    }

    public int GetSkill(UWPlayerData.Skill peSkill)
    {
        return fVitals.GetSkill(peSkill);
    }

    // ------------------------------------------------- Raising skills

    /// <summary>The numbering of skills in the save game - see UWPlayerVitals.AttackSkillNumber.</summary>
    public const int AttackSkillNumber = UWPlayerVitals.AttackSkillNumber;

    public const int DefenceSkillNumber = UWPlayerVitals.DefenceSkillNumber;

    public const int FirstNamedSkillNumber = UWPlayerVitals.FirstNamedSkillNumber;

    public const int SkillNumberCount = UWPlayerVitals.SkillNumberCount;

    /// <summary>Highest skill value - it does not go beyond this.</summary>
    public const int MaxSkillValue = UWPlayerVitals.MaxSkillValue;

    public int GetSkillByNumber(int piSkillNumber)
    {
        return fVitals.GetSkillByNumber(piSkillNumber);
    }

    /// <summary>Sets a skill to a value - for x_skills in conversations.</summary>
    public void SetSkillByNumber(int piSkillNumber, int piValue)
    {
        fVitals.SetSkillByNumber(piSkillNumber, piValue);
    }

    /// <summary>Raises a skill by one point, often by more - see UWPlayerVitals.TryIncreaseSkill.
    /// Returns false if nothing more is possible.</summary>
    public bool TryIncreaseSkill(int piSkillNumber)
    {
        return fVitals.TryIncreaseSkill(piSkillNumber);
    }

    /// <summary>IUWVitalsHost: better Lore makes the world unidentified again.</summary>
    public void ResetIdentification()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader == null || lOLoader.CurrentLevel == null || lOLoader.UWDataImporter == null)
            return;

        UWLoreCheck.ResetAttempts(lOLoader.CurrentLevel.Masterlist,
            lOLoader.UWDataImporter.CommonObjectProperties);
    }

    /// <summary>Spends one skill point.</summary>
    public void SpendSkillPoint()
    {
        fVitals.SpendSkillPoint();
    }

    /// <summary>
    /// Hands the vitals their start: the player data (save game, created character or
    /// PLAYER.DAT) and the test values from the Inspector (-1 keeps the data).
    /// </summary>
    private void fInitialiseVitals()
    {
        fVitals.LogExperience = UnderworldRevisited.UWSettings.Instance != null
            && UnderworldRevisited.UWSettings.Instance.LogExperience;

        UWPlayerVitals.StartValues lOStart = UWPlayerVitals.StartValues.Default;

        lOStart.Strength = miStartStrength;
        lOStart.Dexterity = miStartDexterity;
        lOStart.Intelligence = miStartIntelligence;
        lOStart.Attack = miStartAttack;
        lOStart.Level = miStartLevel;
        lOStart.ManaSkill = miStartManaSkill;
        lOStart.CastingSkill = miStartCastingSkill;
        lOStart.FallbackMaxHP = mfStartMaxHP;
        lOStart.FallbackMaxMana = mfStartMaxMana;
        lOStart.FallbackCurrentHP = mfStartCurrentHP;
        lOStart.FallbackCurrentMana = mfStartCurrentMana;

        fVitals.Initialise(mOUWData, lOStart);
    }

    /// <summary>A full belly. Zero is starving.</summary>
    public const int FullHunger = UWPlayerVitals.FullHunger;

    /// <summary>How fed the character is, 0 to 255 - HIGH means fed.</summary>
    public int Hunger => fVitals.Hunger;

    /// <summary>How tired the character is, 0 to 255 - HIGH means tired.</summary>
    public int Fatigue => fVitals.Fatigue;

    /// <summary>Five-minute blocks since the last meal - see UWPlayerVitals.ChangeHunger.</summary>
    public int MealHealCounter => fVitals.MealHealCounter;

    /// <summary>PLAYER.DAT 0x3C, see UWPlayerVitals.Counter3C.</summary>
    public int Counter3C => fVitals.Counter3C;

    /// <summary>The value of the game clock, see UWGameClock.</summary>
    public int ClockValue => fVitals.ClockValue;

    /// <summary>How drunk the character is, 0 to 63 - see Drink.</summary>
    public int Intoxication => fVitals.Intoxication;

    /// <summary>Maximum intoxication - six bits in the save game.</summary>
    public const int MaxIntoxication = UWPlayerVitals.MaxIntoxication;

    /// <summary>Drinks something intoxicating and reports what came of it - see
    /// UWPlayerVitals.Drink.</summary>
    public UWPlayerVitals.DrinkResult Drink(int piIntoxication)
    {
        return fVitals.Drink(piIntoxication);
    }

    /// <summary>Lowers intoxication by the given amount, never below zero.</summary>
    public void ReduceIntoxication(int piAmount)
    {
        fVitals.ReduceIntoxication(piAmount);
    }

    /// <summary>How long the mushroom effect still lasts, 0 to 3 - see UWPlayerVitals.</summary>
    public int Hallucination => fVitals.Hallucination;

    public const int MaxHallucination = UWPlayerVitals.MaxHallucination;

    public void AddHallucination()
    {
        fVitals.AddHallucination();
    }

    public void StartHallucination()
    {
        fVitals.StartHallucination();
    }

    /// <summary>How much intoxication a night removes.</summary>
    public const int SleepIntoxicationRelief = UWPlayerVitals.SleepIntoxicationRelief;
    /// <summary>
    /// Changes the weapon visible in the hand. The field alone is not enough - the frame
    /// sequence must be fetched again, otherwise the player keeps striking with the old one.
    /// </summary>
    public void SetWeaponType(WeaponTypes peWeapon, bool pbNothingVisible = false)
    {
        if (mOWeapons == null || peWeapon == (mbHasPendingWeapon ? mePendingWeapon : CurrentWeaponType))
            return;

        // Outside combat mode the weapon can be swapped silently - none is visible
        // anyway. THE SAME HOLDS WITH A RANGED WEAPON IN HAND: nothing is held up into view
        // then (HasRangedWeapon), so there is nothing to sheathe. Laying the bow down in
        // combat mode played the AXE'S sheathing motion, because the weapon type had kept
        // whatever was last in hand while the bow hid it (per user, 2026-09-20).
        if (!mIsCombatModeActive || pbNothingVisible)
        {
            CurrentWeaponType = peWeapon;
            mCurrentWeaponAnimation = mOWeapons.GetWeaponAnimation(peWeapon);

            // NOTHING TO SHEATHE, BUT THE NEW WEAPON IS STILL DRAWN. Laying the bow down in
            // combat mode had the fist there at once; the original plays the readying motion
            // for it (per user, 2026-09-20). Outside combat mode nothing is visible either
            // way, so the motion only starts while it is on.
            if (pbNothingVisible && mIsCombatModeActive)
            {
                meAttackStatus = AttackStatusEnum.Readying;
                CurrentReadyWeaponTime = 0f;
                mCurrentWeaponFrameIndex = 0;
            }

            fUpdateWeapon();

            return;
        }

        // As long as the weapon has not been drawn at all, it is swapped silently. Otherwise
        // on the FIRST entry into combat mode the player would first sheathe the weapon he
        // did not even have in hand yet (per user, 2026-08-30).
        if (meAttackStatus != AttackStatusEnum.Ready && meAttackStatus != AttackStatusEnum.PowerUp
            && meAttackStatus != AttackStatusEnum.PoweUpComplete && meAttackStatus != AttackStatusEnum.Release)
        {
            CurrentWeaponType = peWeapon;
            mCurrentWeaponAnimation = mOWeapons.GetWeaponAnimation(peWeapon);

            fUpdateWeapon();

            return;
        }

        // In combat the switch takes two motions, as observed in the original
        // (user, 2026-08-30): first sheathe the old weapon, then draw the new one. No attack
        // is possible before the draw motion ends.
        mePendingWeapon = peWeapon;
        mbHasPendingWeapon = true;

        meAttackStatus = AttackStatusEnum.Seathing;
        CurrentReadyWeaponTime = 1f;
    }

    /// <summary>Whether a blow can be struck now. Not while sheathing or drawing.</summary>
    public bool CanAttack
    {
        get { return meAttackStatus == AttackStatusEnum.Ready; }
    }

    /// <summary>
    /// Whether the wind-up motion has run through. In the original, exactly this decides
    /// whether releasing becomes a blow: once the animation is through, the blow is struck -
    /// regardless of how far the charge counter got (per user, 2026-09-01). The
    /// minimum charge does not decide WHETHER a blow is struck, only how much
    /// damage comes out of it.
    /// </summary>
    public bool IsWindupComplete
    {
        get { return meAttackStatus == AttackStatusEnum.PoweUpComplete; }
    }

    public void Init(DataImport pOUWData)
    {
        mOUWData = pOUWData;

        // The time since the last tick counts on - see msLastGameTickRealtime.
        mOGameTick.Preload(Time.realtimeSinceStartup - msLastGameTickRealtime);

        mOWeapons = mOUWData.WeaponAnimations;
        mCurrentWeaponAnimation = mOWeapons.GetWeaponAnimation(CurrentWeaponType);
        meAttackStatus = AttackStatusEnum.Seathed;
        fUpdateWeapon();

        // THE BODY TYPE COMES FROM THE SAVE GAME, if one is loaded. Until 2026-09-07
        // only the fixed value from the Inspector was here, and the paper doll therefore
        // ALWAYS showed the same figure - regardless of whom you had loaded (noticed by
        // the user: "Or have we never loaded the right image?" - that was exactly
        // the case).
        //
        // The composition is in the reference (uimanager_paperdoll.SetBody):
        // body type zero to four, plus five for a woman. Both sit in
        // the same byte as the character class (see UWPlayerData).
        UWPlayerData lOBodySource = mOUWData != null ? mOUWData.InitialPlayer : null;

        if (lOBodySource != null && lOBodySource.IsLoaded)
            CharIndex = Mathf.Clamp(lOBodySource.Body, 0, 4) + (lOBodySource.IsFemale ? 5 : 0);

        if (CharIndex > 9 || CharIndex < 0)
            CharIndex = 9;

        UWTexture lOCharTex = mOUWData.Textures.GetTextureByType(UWTexture.TextureTypes.BODIES, CharIndex);
        CharTexture = new Texture2D(lOCharTex.Width, lOCharTex.Height, TextureFormat.ARGB32, false);
        CharTexture.name = "UWCharacter.cs:938";
        CharTexture.filterMode = TextureFilterMode;
        CharTexture.SetPixels32(UWGameUI.fGetTextureInvert(lOCharTex));
        CharTexture.Apply();

        // No manual wiring in the scene needed - same self-creation pattern
        // as UWInventory in Interaction.Awake().
        mInventory = GetComponent<UWInventory>();

        if (mInventory == null)
            mInventory = gameObject.AddComponent<UWInventory>();

        mInventory.InventoryChanged += fRebuildAllArmorSlots;

        fRebuildAllArmorSlots();

        mIsCombatModeActive = false;

        mPowerGem = pOUWData.PowerGem;
        fUpdatePowerGemTeture(mPowerGem.Inactive);

        // After the inventory exists: seeding the active spells runs the status pass, which
        // needs it - the same place the health, mana and spells were set before P3.
        fInitialiseVitals();

        mFlasks = pOUWData.Flasks;
        fUpdateFlasks();

        mInteraction = GetComponentInParent<Interaction>();
    }

    public WeaponCoordinate GetWeaponOffset()
    {
        return mCurrentWeaponCoodinate;
    }

    private void OnDestroy()
    {
        if (mInventory != null)
            mInventory.InventoryChanged -= fRebuildAllArmorSlots;
    }

    private void fRebuildAllArmorSlots()
    {
        fRebuildArmorSlot(UWArmorItemMap.BodySlot.Helmet);
        fRebuildArmorSlot(UWArmorItemMap.BodySlot.Chest);
        fRebuildArmorSlot(UWArmorItemMap.BodySlot.Gloves);
        fRebuildArmorSlot(UWArmorItemMap.BodySlot.Legs);
        fRebuildArmorSlot(UWArmorItemMap.BodySlot.Boots);
    }

    /// <summary>
    /// Maps the quality of an armour piece (0 to 63) to one of the four
    /// paper doll conditions.
    ///
    /// According to the user, the original has its own sprite for each condition (2026-09-01).
    ///
    /// QUARTERS OF THE QUALITY RANGE, quality >> 4: 0-15, 16-31, 32-47, 48-63 (the reference
    /// computes the frame as (quality / 16) * 15 plus the item's class index). Until 2026-09-17
    /// we mapped the six condition-word levels onto the four images, which put a breastplate
    /// at quality 20 on the damaged image; in the original it is worn (per user).
    /// </summary>
    private static UWWearables.ArmorConditions fGetArmorCondition(int piQuality)
    {
        switch (Mathf.Clamp(piQuality >> 4, 0, 3))
        {
            case 0: return UWWearables.ArmorConditions.Damaged;
            case 1: return UWWearables.ArmorConditions.Worn;
            case 2: return UWWearables.ArmorConditions.Servicable;
            default: return UWWearables.ArmorConditions.Excellent;
        }
    }

    /// <summary>
    /// Rebuilds the paper doll texture of a body area from the actually
    /// equipped item (UWInventory) - colour/condition/slot via UWArmorItemMap. If
    /// nothing is equipped, the slot shows no armour (ArmorTypes.None); the
    /// Material/Condition/Type fields in the Inspector are not used.
    /// </summary>
    private void fRebuildArmorSlot(UWArmorItemMap.BodySlot peSlot)
    {
        UWObject lOItem = mInventory != null ? mInventory.GetEquipped(peSlot) : null;

        UWWearables.ArmorMaterials leMaterial;
        UWWearables.ArmorConditions leCondition;
        UWWearables.ArmorTypes leType;

        if (lOItem != null && UWArmorItemMap.TryGet(lOItem.ID, out UWArmorItemMap.Entry lOEntry))
        {
            leMaterial = lOEntry.Material;
            leType = lOEntry.RenderType;

            // The condition comes from the item's quality. An earlier attempt showed
            // wrong pieces; the bug was in GetArmor, which withheld the condition offset
            // from boots and helmets. That note said "fixed since", but GetArmor still tested
            // "> Gloves" until 2026-09-17, when a repaired leather cap kept its old image (per user).
            leCondition = fGetArmorCondition(lOItem.Quality);
        }
        else
        {
            // Nothing equipped = nothing visible, the same for all 5 slots. (The old
            // Inspector preview fields HelmetType/LegsType/... from before this function
            // made the paper doll's legs look inconsistent with the empty slots; they were
            // unused and are gone since 2026-09-18.)
            leType = UWWearables.ArmorTypes.None;
            leMaterial = UWWearables.ArmorMaterials.Leather;
            leCondition = UWWearables.ArmorConditions.Servicable;
        }

        Texture2D lOTexture = fBuildArmorTexture(leMaterial, leCondition, leType);
        int liDrop = leType == UWWearables.ArmorTypes.None ? 0
            : UWWearables.GetArmorDrop(IsMale ? UWWearables.Sex.Male : UWWearables.Sex.Female, leMaterial, leType);

        // The men's plate legs sit two rows too high in their picture (UWWearables.GetArmorDrop):
        // the picture's rows moved down here, for the classic scheme and the modern panel alike.
        if (liDrop > 0)
        {
            Color32[] lOPixels = lOTexture.GetPixels32();
            Color32[] lOMoved = new Color32[lOPixels.Length];
            int liWidth = lOTexture.width;

            for (int liRow = 0; liRow < lOTexture.height - liDrop; liRow++)
                System.Array.Copy(lOPixels, (liRow + liDrop) * liWidth, lOMoved, liRow * liWidth, liWidth);

            lOTexture.SetPixels32(lOMoved);
            lOTexture.Apply();
        }

        UWTexture lOSource = leType == UWWearables.ArmorTypes.None ? null
            : mOUWData.Wearables.GetArmor(IsMale ? UWWearables.Sex.Male : UWWearables.Sex.Female, leMaterial, leCondition, leType);

        switch (peSlot)
        {
            case UWArmorItemMap.BodySlot.Helmet: HelmetTexture = lOTexture; HelmetSource = lOSource; break;
            case UWArmorItemMap.BodySlot.Chest: ChestArmorTexture = lOTexture; ChestSource = lOSource; break;
            case UWArmorItemMap.BodySlot.Gloves: GlovesTexture = lOTexture; GlovesSource = lOSource; break;
            case UWArmorItemMap.BodySlot.Legs: LegsArmorTexture = lOTexture; LegsSource = lOSource; LegsDrop = liDrop; break;
            case UWArmorItemMap.BodySlot.Boots: BootsTexture = lOTexture; BootsSource = lOSource; break;
        }
    }

    private Texture2D fBuildArmorTexture(UWWearables.ArmorMaterials peMaterial, UWWearables.ArmorConditions peCondition, UWWearables.ArmorTypes peType)
    {
        if (peType == UWWearables.ArmorTypes.None)
            return fBuildTransparentPlaceholder();

        UWTexture lOSource = mOUWData.Wearables.GetArmor(IsMale ? UWWearables.Sex.Male : UWWearables.Sex.Female, peMaterial, peCondition, peType);
        Texture2D lOResult = new Texture2D(lOSource.Width, lOSource.Height, TextureFormat.ARGB32, false);
        lOResult.name = "UWCharacter.cs:1090";
        lOResult.SetPixels32(UWGameUI.fGetTextureInvert(lOSource));
        lOResult.filterMode = TextureFilterMode;
        lOResult.Apply();
        return lOResult;
    }

    // GetArmor(..., ArmorTypes.None) previously: returned WeaponAnimations.Black (a
    // real WEAPONS.GR frame), which was only "accidentally" invisible because UWPalette
    // wrongly treated pure black as a transparency marker. Since the palette fix
    // (see UWPalette.cs) real black is opaque again, which made this placeholder
    // show up as a visible black spot on empty armour slots. An empty
    // slot needs no borrowed texture, only real transparency.
    private Texture2D fBuildTransparentPlaceholder()
    {
        Texture2D lOResult = new Texture2D(1, 1, TextureFormat.ARGB32, false);
        lOResult.name = "UWCharacter.cs:1105";
        lOResult.filterMode = TextureFilterMode;
        lOResult.SetPixel(0, 0, new Color32(0, 0, 0, 0));
        lOResult.Apply();
        return lOResult;
    }

    // Use this for initialization
    void Start ()
    {
	
	}
	
    /// <summary>Restores health - the fountain (302) does this in the original
    /// when used (reported by the user).
    ///
    /// Actually works since 2026-08-29 - before that a debug counter in Update() overwrote
    /// every value set on the next one-second tick.</summary>
    public void RestoreVitality()
    {
        fVitals.RestoreVitality();
    }

    /// <summary>The last hit - amount, type and when. FOR DEBUGGING ONLY (F1), see
    /// UWPlayerVitals.LastDamage.</summary>
    public int LastDamage => fVitals.LastDamage;

    public int LastDamageType => fVitals.LastDamageType;

    public float LastDamageTime { get; private set; } = -1f;

    /// <summary>IUWVitalsHost: a hit or a curse was recorded - stamp the time.</summary>
    public void OnDamageRecorded()
    {
        LastDamageTime = Time.time;
    }

    /// <summary>Damage summed since the last call - see UWPlayerVitals.</summary>
    public int TakeDamageThisTick()
    {
        return fVitals.TakeDamageThisTick();
    }

    /// <summary>
    /// Damage to the player, with its type. Subtracts hit points and reports whether he
    /// died from it - see UWPlayerVitals.ApplyDamage. What he wards off comes from the
    /// immunities of the worn equipment and the active spells (DamageTypeProof).
    /// </summary>
    public bool ApplyDamage(int piDamage, int piDamageType = UWDamageTypes.None)
    {
        return fVitals.ApplyDamage(piDamage, piDamageType, DamageTypeProof);
    }

    /// <summary>IUWVitalsHost: a hit shakes the view.</summary>
    public void ShakeOnDamage(int piLevel)
    {
        UWScreenShake.ShakeOnDamage(piLevel);
    }

    /// <summary>IUWVitalsHost: a failed sip shakes the view.</summary>
    public void ShakeUnsteady(int piDuration)
    {
        UWScreenShake lOShake = UWScreenShake.Ensure();

        if (lOShake != null)
            lOShake.Shake(UWScreenShake.LargeChannel, piDuration);
    }

    /// <summary>IUWVitalsHost: a message for the scroll (the level-up line).</summary>
    public void AddMessage(string psMessage)
    {
        if (mInteraction != null)
            mInteraction.AddMessage(psMessage);
    }

    /// <summary>IUWVitalsHost: a creature was slain. A dragon nods (UW.EXE
    /// AwardKillEXP_seg022_1725, before the fanfare); the victory fanfare only if a reward is
    /// due (reference: AwardXPKill).</summary>
    public void OnKillRewarded(bool pbRewarded)
    {
        UWHudDragons.Request(UWHudDragons.NodAnimation);

        if (pbRewarded)
            UWMusic.ChangeTheme(UWMusic.FanfareTheme);
    }

    // ------------------------------------------------- Active spells

    public const int MaxActiveSpells = UWPlayerVitals.MaxActiveSpells;

    /// <summary>The motion spells as a bit field - see UWPlayerVitals.LeapBit.</summary>
    public const int LeapBit = UWPlayerVitals.LeapBit;

    public const int SlowFallBit = UWPlayerVitals.SlowFallBit;

    public const int LevitateBit = UWPlayerVitals.LevitateBit;

    public const int WaterWalkBit = UWPlayerVitals.WaterWalkBit;

    public const int FlyBit = UWPlayerVitals.FlyBit;

    /// <summary>Counts up on every change to the active spells. The UI ties its icons
    /// to it, so that expiry also reaches it.</summary>
    public int ActiveSpellVersion => fVitals.ActiveSpellVersion;

    /// <summary>
    /// Is Freeze Time currently active? Then no critter moves any more (see UWCritter).
    ///
    /// STATIC, because every critter of the level needs this in its own frame update and there
    /// is only one player character - the same pattern as UWConversationScreen.IsAnyOpen.
    /// </summary>
    public static bool TimeIsFrozen { get; private set; }

    /// <summary>Is Telekinesis currently active? Then the hand reaches further - see
    /// Interaction.TryReachTarget.</summary>
    public bool HasTelekinesis => fVitals.HasTelekinesis;

    public System.Collections.Generic.IReadOnlyList<UWActiveSpellEffect> ActiveSpells => fVitals.ActiveSpells;

    /// <summary>Adds a spell to the list. Returns false if three are already running -
    /// then nothing further happens in the original.</summary>
    public bool TryAddActiveSpell(int piMajorClass, int piMinorClass, int piStability)
    {
        return fVitals.TryAddActiveSpell(piMajorClass, piMinorClass, piStability);
    }

    /// <summary>
    /// Applies or withdraws the detached view of Roaming Sight.
    ///
    /// HERE AND ONLY HERE, because all endings converge in the vitals' spell pass: expiry,
    /// clicking the icon away, sleep and loading a save game. So there can be
    /// no state in which the spell is still running and the camera is already back.
    ///
    /// The component sits on THIS object, i.e. on the player camera. The world position
    /// of the view, however, comes from the BODY: UWCharacter itself hangs on the camera and moves
    /// with its pitch motion.
    /// </summary>
    private void fApplyRoamingSight(bool pbActive)
    {
        if (!pbActive)
        {
            if (mORoamingSight != null)
                mORoamingSight.Stop();

            return;
        }

        if (mORoamingSight == null)
            mORoamingSight = GetComponent<UWRoamingSight>();

        if (mORoamingSight == null)
            mORoamingSight = gameObject.AddComponent<UWRoamingSight>();

        CharacterController lOBody = GetComponentInParent<CharacterController>();

        Transform lOFrom = lOBody != null ? lOBody.transform : transform;

        mORoamingSight.Begin(lOFrom.position, lOFrom.eulerAngles.y);
    }

    /// <summary>The feet in zpos steps - the bottom of the body the CharacterController holds,
    /// the height the original keeps in the player object. The rules that count on the tile
    /// grid measure from here (UWReachRules, UWArmourProtection.PickBodyPart).</summary>
    public int GetFeetZPos()
    {
        CharacterController lOBody = GetComponentInParent<CharacterController>();
        float lfFeet = lOBody != null ? lOBody.bounds.min.y : transform.position.y;

        return UWUnits.RoundToInt(lfFeet / UWWorldScale.ZPosStep);
    }

    /// <summary>The body's height in zpos steps, from COMOBJ.DAT for the adventurer (23) - what
    /// the original adds to the feet for the top of the body.</summary>
    public int GetBodyHeightZ()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;
        UWCommonObjectProperties lOCommon = lOLoader != null && lOLoader.UWDataImporter != null
            ? lOLoader.UWDataImporter.CommonObjectProperties : null;
        UWCommonObjectProperties.Entry lOEntry;

        if (lOCommon != null && lOCommon.TryGet(UWObjectMechanics.AdventurerObjectId, out lOEntry))
            return lOEntry.Height;

        return FallbackBodyHeightZ;
    }

    /// <summary>The adventurer's height when the table is not at hand.</summary>
    private const int FallbackBodyHeightZ = 23;

    private UWRoamingSight mORoamingSight;

    /// <summary>Ends a specific active spell. Used by the detached view: if the camera is
    /// removed from outside - on a level change, for instance -, the spell should not
    /// remain in the icon strip without it.</summary>
    public bool EndSpellEffect(int piMajorClass, int piMinorClass)
    {
        return fVitals.EndSpellEffect(piMajorClass, piMinorClass);
    }

    public void CancelActiveSpell(int piIndex)
    {
        fVitals.CancelActiveSpell(piIndex);
    }

    /// <summary>
    /// IUWVitalsHost: the set of active spells changed. Class 11 comes decoded; light,
    /// motion, armour and protection depend not only on spells but also on equipment, and
    /// the same status pass computes both.
    /// </summary>
    public void OnActiveSpellsChanged(bool pbHastened, bool pbRoamingSight, bool pbTimeFrozen, bool pbTelekinesis)
    {
        TimeIsFrozen = pbTimeFrozen;

        fApplyRoamingSight(pbRoamingSight);

        // SPEED (pbHastened) does not make the player walk faster: the original's only readers
        // of its flag (SpeedEnchantment_dseg_5c99_283) are the two frame ticks, which halve the
        // slot units handed to the world - the player and the clock run on at full rate
        // (read 2026-09-24, Todo.md section 0b row 5). That half is UWCritterDriver.PlayerHasSpeed;
        // the estimated 1.5 on the player's walk that stood here counted it twice.
        RefreshStatus();
    }

    private UWLighting mOLighting;

    private UWPlayerMovement mOMovement;

    // ---------- The game tick

    /// <summary>This is how long one point of stability lasts: one player tick. The length
    /// and the evidence for it are in UWPlayerTick.</summary>
    private const float SpellTickSeconds = UWPlayerTick.Seconds;

    private readonly UWTickClock mOGameTick = new UWTickClock(SpellTickSeconds);

    /// <summary>
    /// When the last player tick ran, in real time since the program started - kept across the
    /// scene reload of a restore (static). THE FIRST TICK AFTER LOADING (per user, 2026-09-29: a
    /// torch of quality 2 went out about 15 s earlier in the original): PlayerUpdateTick_seg024_24DC_3A4
    /// adds the whole seconds of the timer since its last call (seg019_710, counted from the
    /// program's start) into a byte and runs the tick once that is above 20. Loaded from the main
    /// menu, the first call finds the last time still at the start - far more than 20 seconds -
    /// and ticks at once; restored during play, the count simply goes on. Ours started every
    /// scene at zero and ticked first after 21 seconds.
    /// </summary>
    private static float msLastGameTickRealtime;

    /// <summary>
    /// The player tick on which everything time-related hangs in the original -
    /// what happens on it is in UWPlayerVitals.RunGameTick. Elapsed time goes in, whole
    /// ticks come out (UWTickClock, P0 of the engine separation): after a long frame
    /// several ticks run instead of one per frame.
    /// </summary>
    private void fTickGameClock()
    {
        int liTicks = mOGameTick.Advance(Time.deltaTime);

        if (liTicks > 0)
            msLastGameTickRealtime = Time.realtimeSinceStartup;

        for (int liTick = 0; liTick < liTicks; liTick++)
        {
            fVitals.RunGameTick(UWDataImport.UWData.UWGameClock.UnitsPerTick, DamageTypeProof);

            // The carried lights burn on the same tick and its counter (PlayerUpdates_seg028_2985_13D
            // calls UpdateInventoryLightSources right after the spells) - until 2026-09-24 on a
            // clock of their own at 25 seconds.
            if (mInventory != null)
                mInventory.Model.BurnTick(1, fVitals.TickInCycle);
        }
    }

    /// <summary>IUWVitalsHost: the curse dice of the worn equipment, for the tick.</summary>
    public int WornCurseDice => mOStatus.CurseDice;

    /// <summary>IUWVitalsHost: the level the player is on, for the orb's mana rule.</summary>
    public int DungeonLevelIndex => UWScene.LevelLoader != null ? UWScene.LevelLoader.CurrentLevelIndex : -1;

    /// <summary>
    /// IUWVitalsHost: the regeneration bits of the worn equipment, for the tick - a Ring of
    /// Regeneration gives one hit point per tick from here. The status pass has already put
    /// them in mOStatus every frame, so no extra timer is needed; see UWWornRegeneration.
    /// </summary>
    public int WornRegeneration => mOStatus.RegenerationBits;

    /// <summary>Subtracts mana and updates the flask. Never below zero.</summary>
    public void SpendMana(int piAmount)
    {
        fVitals.SpendMana(piAmount);
    }

    /// <summary>Damage that leaves at least this many hit points - the leeches (see
    /// UWItemDrag).</summary>
    public void ApplyDamageKeepingMinimum(int piDamage, int piMinimum)
    {
        fVitals.ApplyDamageKeepingMinimum(piDamage, piMinimum, DamageTypeProof);
    }

    /// <summary>Counterpart to ApplyDamage, for instance for healing potions.</summary>
    public void Heal(int piAmount)
    {
        fVitals.Heal(piAmount);
    }

    /// <summary>How strongly the character is poisoned, zero to fifteen - see UWPlayerVitals.</summary>
    public int Poison => fVitals.Poison;

    /// <summary>Poisons the character - a stronger poisoning replaces the current one.</summary>
    public void ApplyPoison(int piStrength)
    {
        fVitals.ApplyPoison(piStrength);
    }

    /// <summary>Advances the game clock by whole hours - see UWSleep.</summary>
    public void AdvanceClockHours(int piHours)
    {
        fVitals.AdvanceClockHours(piHours);
    }

    /// <summary>Advances the game clock by whole minutes - repairing at the anvil.</summary>
    public void AdvanceClockMinutes(int piMinutes)
    {
        fVitals.AdvanceClockMinutes(piMinutes);
    }

    /// <summary>All active spells end - they wear off during sleep.</summary>
    public void EndAllSpells()
    {
        fVitals.EndAllSpells();
    }

    /// <summary>Spell class 10: restore mana - see UWPlayerVitals.RestoreManaFromSpell.</summary>
    public void RestoreManaFromSpell(int piMinorClass)
    {
        fVitals.RestoreManaFromSpell(piMinorClass);
    }

    public void CurePoison()
    {
        fVitals.CurePoison();
    }

    public void ClearFatigue()
    {
        fVitals.ClearFatigue();
    }

    public void ReduceFatigue(int piAmount)
    {
        fVitals.ReduceFatigue(piAmount);
    }

    /// <summary>Changes hunger, up or down. Fed is 255.</summary>
    public void ChangeHunger(int piAmount)
    {
        fVitals.ChangeHunger(piAmount);
    }

    /// <summary>Recovery after a night of sleep - see UWPlayerVitals.RegenerateOnSleep.</summary>
    public void RegenerateOnSleep(int piVitalityFactor, int piMana)
    {
        fVitals.RegenerateOnSleep(piVitalityFactor, piMana);
    }

    /// <summary>Sets hit points to the maximum - Greater Heal.</summary>
    public void HealFully()
    {
        fVitals.HealFully();
    }

    /// <summary>How loud the player currently is, 0 to 15 - this is what critters hear
    /// (UWCritter). The rule is in UWPlayerVitals.Quietness.</summary>
    public int Quietness => fVitals.Quietness;

    /// <summary>How visible the player is, 0 to 15 - the other half of what a creature checks.
    /// The rule is in UWPlayerVitals.Visibility, including Conceal and Invisibility.</summary>
    public int Visibility => fVitals.Visibility;

    /// <summary>Noise from outside: winding up, blow, repair.</summary>
    public void MakeNoise(int piLevel)
    {
        fVitals.MakeNoise(piLevel);
    }

    /// <summary>One step of the easy movement: its noise and its piece of the game clock -
    /// see UWPlayerVitals.ReportEasyMovementStep and UWEasyMovement.</summary>
    public void ReportEasyMovementStep()
    {
        fVitals.ReportEasyMovementStep();
    }

    private UWPlayerMovement mOMovementForNoise;

    private void fTickQuietness()
    {
        if (mOMovementForNoise == null)
            mOMovementForNoise = GetComponentInParent<UWPlayerMovement>();

        fVitals.TickQuietness(Time.deltaTime);
    }

    /// <summary>IUWVitalsHost: the player walks.</summary>
    public bool IsMoving => mOMovementForNoise != null && mOMovementForNoise.IsMoving;

    /// <summary>IUWVitalsHost: how much of the full forward speed is actually being made.</summary>
    public float MovementFraction => mOMovementForNoise != null ? mOMovementForNoise.MomentumFraction : 0f;

    /// <summary>IUWVitalsHost: the player stands in water.</summary>
    public bool IsInLiquid => mOMovementForNoise != null && mOMovementForNoise.IsInLiquid;

    /// <summary>IUWVitalsHost: winding up a blow is loud (reference: CombatStages.Charging).</summary>
    public bool IsChargingAttack => meAttackStatus == AttackStatusEnum.PowerUp || meAttackStatus == AttackStatusEnum.PoweUpComplete;

    /// <summary>IUWVitalsHost: health, mana or poison changed - the flasks show it. Before
    /// Init has handed over the flask pictures there is nothing to draw yet.</summary>
    public void OnVitalsChanged()
    {
        if (mFlasks != null)
            fUpdateFlasks();
    }

    private const int StrikeNoise = UWPlayerVitals.StrikeNoise;
	// Update is called once per frame
	void Update ()
    {
        // Not initialised: no game data was found and the setup screen is up (UWSetupMenu).
        // Every frame threw a NullReferenceException further down (per user, 2026-09-17).
        if (mInteraction == null)
            return;

        // The status pass first: armour, protection, magic light and the
        // movement abilities from equipment and active spells (see RefreshStatus).
        RefreshStatus();

        fTickGameClock();

        fTickQuietness();

        // The flask fill animation needs a redraw every frame while it runs.
        if (mfFlaskFillStart >= 0f)
            fUpdateFlasks();

        fTickFlaskBubbles();

        // Until 2026-08-29 a debug counter ran here that raised the hit points by one every
        // second and reset them to 0 on overflow - only so that the flask
        // in the UI moved. It thereby overwrote every real value, which is why
        // damage and the fountain effect had no effect.

        // A CONVERSATION STOPS THE WEAPON. In the original the conversation is a modal loop and
        // the game loop stands still meanwhile: a combat mode toggled from the weapon hand during
        // the conversation (see UWItemDrag) draws the weapon only once the conversation has ended,
        // the same for sheathing and for a strike (per user on the original, 2026-09-18).
        // Everything from here to the end of Update is the weapon.
        if (UWConversationScreen.IsAnyOpen)
            return;

        if (!mIsCombatModeActive && mInteraction.IsCombatModeActive) //Enter combat mode
        {
            mIsCombatModeActive = true;
            meAttackStatus = AttackStatusEnum.Readying;
            CurrentReadyWeaponTime = 0f;
            mCurrentWeaponFrameIndex = 0;
            fUpdateWeapon();
            fUpdatePowerGemTeture(mPowerGem.Unpowered);

            // Weapon drawn: the "Armed" theme, unless combat music plays (UWMusicSelector.OnWeaponDrawn).
            UWMusic.OnWeaponDrawn();
        }
        else if (mIsCombatModeActive && !mInteraction.IsCombatModeActive) //Exit combat mode
        {
            mIsCombatModeActive = false;
            meAttackStatus = AttackStatusEnum.Seathing;
            CurrentReadyWeaponTime = 1f;
            fUpdatePowerGemTeture(mPowerGem.Inactive);

            UWMusic.OnCombatModeLeft();
        }
        else if (mIsCombatModeActive && meAttackStatus == AttackStatusEnum.Readying) //Readying weapon
        {
            CurrentReadyWeaponTime += Time.deltaTime * 2f;
            if (CurrentReadyWeaponTime >= 1f)
            {
                CurrentReadyWeaponTime = 1f;
                meAttackStatus = AttackStatusEnum.Ready;
            }
        }
        else if (meAttackStatus == AttackStatusEnum.Seathing) //Seathing weapon
        {
            CurrentReadyWeaponTime -= Time.deltaTime * 2f;
            if (CurrentReadyWeaponTime <= 0f)
            {
                CurrentReadyWeaponTime = 0f;

                // Weapon switch in combat: the old weapon is sheathed, now the new one
                // is drawn. Without a pending switch it stays sheathed.
                if (mbHasPendingWeapon)
                {
                    mbHasPendingWeapon = false;

                    CurrentWeaponType = mePendingWeapon;
                    mCurrentWeaponAnimation = mOWeapons.GetWeaponAnimation(mePendingWeapon);

                    meAttackStatus = AttackStatusEnum.Readying;
                    mCurrentWeaponFrameIndex = 0;
                    fUpdateWeapon();
                }
                else
                {
                    meAttackStatus = AttackStatusEnum.Seathed;
                }
            }
        }
        else if (mIsCombatModeActive && (meAttackStatus == AttackStatusEnum.Ready || meAttackStatus == AttackStatusEnum.PowerUp))
        {
            if (mInteraction.AttackCharge > 0f && meAttackStatus == AttackStatusEnum.Ready) //Starting attack
            {
                meAttackStatus = AttackStatusEnum.PowerUp;
                mCurrentWeaponFrameIndex = 0;
                mCurrentAttackAnimationDuration = 0f;
                fUpdateWeapon();
            }
        }

        if (!mIsCombatModeActive) return;

        if (meAttackStatus == AttackStatusEnum.PowerUp)
        {
            // Released during the wind-up: the motion runs back from EXACTLY THIS frame,
            // not from the last one (per user, 2026-08-30).
            if (mbReleaseRequested)
            {
                mbReleaseRequested = false;

                meAttackStatus = AttackStatusEnum.PowerDown;
                mCurrentAttackAnimationDuration = 0f;
            }
            else if (mCurrentWeaponFrameIndex < numPowerUpFrames)
            {
                mCurrentAttackAnimationDuration += Time.deltaTime;

                if (mCurrentAttackAnimationDuration > .125f)
                {
                    mCurrentWeaponFrameIndex++;
                    mCurrentAttackAnimationDuration = 0f;
                    fUpdateWeapon();
                }
            }
            else
            {
                meAttackStatus = AttackStatusEnum.PoweUpComplete;
                mCurrentAttackAnimationDuration = 0f;
                mCurrentWeaponFrameIndex = 0;
            }
        }
        else if(meAttackStatus == AttackStatusEnum.PoweUpComplete)
        {
            if (mInteraction.AttackCharge < 1f)
            {
                // The orb depends solely on the charge counter, not on the minimum charge: the
                // original computes frame = 1 + charge/12 over the full range of 0 to 100.
                // Frame 1 is uncharged, 2 to 8 are the yellow levels, from 9 on green begins.
                //
                // Previously we squeezed the seven yellow frames into the range between
                // minimum charge and full. For the fist (minimum charge 90) that was
                // a tenth of the charge time - the orb practically jumped from red to green
                // (per user, 2026-09-01). In the original yellow is clearly visible, and with
                // this computation it already starts at charge 12.
                //
                // Read from the image data: frames 2 to 8 contain not a single
                // green pixel, their yellow ones grow from 11 to 59. A transition from
                // yellow to green does not occur in them - the change only happens on the
                // jump to the green frames.
                int liCharge = Mathf.RoundToInt(Mathf.Clamp01(mInteraction.AttackCharge) * UWCombat.FullCharge);
                int liGemFrame = UWPowerGem.GetFrame(liCharge);

                // A RANGED WEAPON KNOWS NO YELLOW: in the original the sling jumps straight
                // from red to green, exactly when the targeting cursor appears (per user,
                // 2026-09-10). SINCE 2026-09-22 WE KNOW WHY, and it is not a computation at
                // all - the charge path is skipped for a ranged weapon and the gem is set to
                // frame 9 in one go. See UWPowerGem.RangedFrame.
                if (HasRangedWeapon)
                    liGemFrame = UWPowerGem.GetRangedFrame(mInteraction.IsRangedTargeting);

                if (liGemFrame <= 1)
                    fUpdatePowerGemTeture(mPowerGem.Unpowered);
                else if (liGemFrame - 2 < mPowerGem.Powering.Count)
                    fUpdatePowerGemTeture(mPowerGem.Powering[liGemFrame - 2]);
                else
                    fUpdatePowerGemTeture(mPowerGem.FullyPowered[0]);

                mMaxAttackPowerAnimationTime = 0f;
            }
            else
            {
                mMaxAttackPowerAnimationTime += Time.deltaTime * 5f;
                if (mMaxAttackPowerAnimationTime >= 5f)
                    mMaxAttackPowerAnimationTime = 0f;

                fUpdatePowerGemTeture(mPowerGem.FullyPowered[(int)mMaxAttackPowerAnimationTime]);
            }

            // Once the wind-up motion has run through, a blow is ALWAYS struck - even if
            // the minimum charge has not been reached yet. The blow then just hits nothing
            // (per user, 2026-08-30: with some weapons the orb is still pale red at this
            // point). Whether it deals damage is decided by Interaction via the
            // minimum charge; the motion runs in any case.
            if (mbReleaseRequested)
            {
                mbReleaseRequested = false;

                meAttackStatus = AttackStatusEnum.Release;
                mCurrentWeaponFrameIndex = 0;
                mCurrentAttackAnimationDuration = 0f;

                // The blow is loud (reference: combat_input.AttackTarget).
                MakeNoise(StrikeNoise);

                fUpdateWeapon();
            }
        }

        if (meAttackStatus == AttackStatusEnum.PowerDown)
        {
            // Backwards through the wind-up frames, then ready again. No blow, no
            // hit (per user, 2026-08-30: an attack released too early is not
            // carried out).
            mCurrentAttackAnimationDuration += Time.deltaTime;

            if (mCurrentAttackAnimationDuration > .125f)
            {
                mCurrentAttackAnimationDuration = 0f;

                if (mCurrentWeaponFrameIndex > 0)
                {
                    mCurrentWeaponFrameIndex--;
                    fUpdateWeapon();
                }
                else
                {
                    meAttackStatus = AttackStatusEnum.Ready;
                    fUpdateWeapon();
                    fUpdatePowerGemTeture(mPowerGem.Unpowered);
                }
            }
        }

        if (meAttackStatus == AttackStatusEnum.Release)
        {
            if (mCurrentWeaponFrameIndex < numReleaseFrames)
            {
                mCurrentAttackAnimationDuration += Time.deltaTime;

                if (mCurrentAttackAnimationDuration > .125f)
                {
                    mCurrentWeaponFrameIndex++;
                    mCurrentAttackAnimationDuration = 0f;
                    fUpdateWeapon();

                    // The blow connects: the hit check hangs on THIS frame, not on
                    // releasing the button. In the original you can start a blow out of
                    // range and still hit if you are close enough at the right
                    // moment (per user, 2026-08-30).
                    int liConnectFrame = fGetConnectFrame();

                    if (mCurrentWeaponFrameIndex == liConnectFrame && AttackConnected != null)
                        AttackConnected();
                }
            }
            else
            {
                meAttackStatus = AttackStatusEnum.Ready;
                fUpdateWeapon();
                fUpdatePowerGemTeture(mPowerGem.Unpowered);
            }
        }
    }

    /// <summary>
    /// The weapon image as a texture - each frame converted once and kept.
    ///
    /// LEAK FIXED (per user, 2026-09-13: after long play the screen went dark and the
    /// inventory disappeared, in the log "Resource ID out of range in GetResource ... max is
    /// 1048575"): here every frame change of the weapon created a new texture that was never
    /// released - eight per second in combat. Unity eventually runs out of IDs for
    /// graphics resources, and then no texture can be created any more.
    /// </summary>
    private void fGetCurrentWeaponTexture()
    {
        CurrentWeaponTexture = fGetCachedTexture(mCurrentWeaponFrame);
    }

    /// <summary>Convert a UI texture once and keep it, as with the power orb.
    /// </summary>
    private Texture2D fGetCachedTexture(UWTexture pOTexture)
    {
        if (pOTexture == null)
            return null;

        Texture2D lOCached;

        // A change of the colour help makes every cached picture stale.
        if (miUiColourVersion != UWColourVision.Version)
        {
            foreach (Texture2D lOOld in mOUiTextureCache.Values)
                Destroy(lOOld);

            mOUiTextureCache.Clear();

            miUiColourVersion = UWColourVision.Version;
        }

        if (!mOUiTextureCache.TryGetValue(pOTexture, out lOCached) || lOCached == null)
        {
            lOCached = new Texture2D(pOTexture.Width, pOTexture.Height, TextureFormat.ARGB32, false);
            lOCached.name = "UWCharacter.cs:2051";
            lOCached.filterMode = TextureFilterMode;
            lOCached.SetPixels32(UWGameUI.fGetTextureInvert(pOTexture));
            lOCached.Apply();

            mOUiTextureCache[pOTexture] = lOCached;
        }

        return lOCached;
    }

    private readonly System.Collections.Generic.Dictionary<UWTexture, Texture2D> mOUiTextureCache
        = new System.Collections.Generic.Dictionary<UWTexture, Texture2D>();

    private int miUiColourVersion = -1;

    private void fUpdateWeapon()
    {
        switch (meAttackStatus)
        {
            // PowerDown runs through the same frames as PowerUp, only backwards - without
            // this case it ended up in default and showed the ready frame, so the
            // retraction was not visible (per user, 2026-08-30).
            case AttackStatusEnum.PowerUp:
            case AttackStatusEnum.PowerDown:
                mCurrentWeaponFrame = mCurrentWeaponAnimation.GetPowerupFrame(CurrentAttackType, mCurrentWeaponFrameIndex);
                mCurrentWeaponCoodinate = mCurrentWeaponAnimation.GetPowerupCoord(CurrentAttackType, mCurrentWeaponFrameIndex);
                break;
            case AttackStatusEnum.PoweUpComplete:
                mCurrentWeaponFrame = mCurrentWeaponAnimation.GetPowerupFrame(CurrentAttackType, 3);
                mCurrentWeaponCoodinate = mCurrentWeaponAnimation.GetPowerupCoord(CurrentAttackType, 3);
                break;
            case AttackStatusEnum.Release:
                mCurrentWeaponFrame = mCurrentWeaponAnimation.GetReleaseFrame(CurrentAttackType, mCurrentWeaponFrameIndex);
                mCurrentWeaponCoodinate = mCurrentWeaponAnimation.GetReleaseCoord(CurrentAttackType, mCurrentWeaponFrameIndex);
                break;
            default:
                mCurrentWeaponFrame = mCurrentWeaponAnimation.GetReadyFrame();
                mCurrentWeaponCoodinate = mCurrentWeaponAnimation.GetReadyCoord();
                break;
        }

        fGetCurrentWeaponTexture();
    }

    /// <summary>
    /// Sets the image of the power orb.
    ///
    /// Previously EVERY call created a new texture here, converted the pixels
    /// and called Apply() - and the call is in Update, so it ran every frame. As long as
    /// the orb showed the same image most of the time, this went unnoticed; since it fills over
    /// the whole charge time, it became visible (per user, 2026-09-01: "choppy").
    ///
    /// Now each of the fourteen orb images is converted once and kept, and a
    /// change only happens when the image really changes.
    /// </summary>
    private void fUpdatePowerGemTeture(UWTexture pTexture)
    {
        if (pTexture == null || pTexture == mOCurrentPowerGemSource)
            return;

        mOCurrentPowerGemSource = pTexture;

        Texture2D lOCached;

        if (!mOPowerGemTextures.TryGetValue(pTexture, out lOCached))
        {
            lOCached = new Texture2D(pTexture.Width, pTexture.Height, TextureFormat.ARGB32, false);
            lOCached.name = "UWCharacter.cs:2133";
            lOCached.SetPixels32(UWGameUI.fGetTextureInvert(pTexture));
            lOCached.filterMode = TextureFilterMode;
            lOCached.Apply();

            mOPowerGemTextures[pTexture] = lOCached;
        }

        PowerGemTeture = lOCached;
    }

    private readonly System.Collections.Generic.Dictionary<UWTexture, Texture2D> mOPowerGemTextures
        = new System.Collections.Generic.Dictionary<UWTexture, Texture2D>();

    private UWTexture mOCurrentPowerGemSource;

    /// <summary>
    /// Subtracts hit points without it being a hit - no shaking, no damage type.
    /// For the Ethereal Void (UWVoidEffects), which sets the points directly in UW.EXE.
    /// Never goes below zero.
    /// </summary>
    public void DrainVitality(int piAmount)
    {
        fVitals.DrainVitality(piAmount);
    }

    /// <summary>Levels of a flask picture (UWFlasks.GetFlask).</summary>
    private const float FlaskLevels = 13f;

    /// <summary>Start of the running flask fill animation, below zero for none.</summary>
    private float mfFlaskFillStart = -1f;

    /// <summary>
    /// Lets the health and mana flasks fill up from empty to their current values, one level
    /// per UWSettings.FlaskFillStepSeconds. After the resurrection at the silver tree both
    /// flasks fill slowly in the original, one step every 0.25 s, while vitality and mana are
    /// already nearly full (per user, 2026-09-14) - only the pictures are animated.
    /// </summary>
    public void StartFlaskFill()
    {
        mfFlaskFillStart = Time.time;
        fUpdateFlasks();
    }

    /// <summary>The bubbling of the two flasks (see UWFlaskAnimation) - driven from Update, so it
    /// runs on its own while nothing else changes.</summary>
    private readonly UWFlaskAnimation mOHealthBubbles = new UWFlaskAnimation();

    private readonly UWFlaskAnimation mOManaBubbles = new UWFlaskAnimation();

    /// <summary>
    /// THE LEVEL ITSELF MOVES ONE STEP AT A TIME, an eighth of a second per step, as in the
    /// original (the reference: uimanager_flasks.AnimateFlasks; built 2026-09-17, per user).
    /// Whoever drinks a potion sees the flask run up, not jump. Minus one until the first pass,
    /// so that a loaded character starts at their real level instead of filling up from empty.
    /// </summary>
    private int miShownHealthLevel = -1;

    private int miShownManaLevel = -1;

    private readonly UWTickClock mOFlaskLevelClock = new UWTickClock(UWFlaskAnimation.StepSeconds);

    private void fTickFlaskBubbles()
    {
        int liHealth = MaxHP <= 0f ? 0 : Mathf.Clamp((int)((CurrentHP / MaxHP) * FlaskLevels), 0, (int)FlaskLevels);
        int liMana = MaxMana <= 0f ? 0 : Mathf.Clamp((int)((CurrentMana / MaxMana) * FlaskLevels), 0, (int)FlaskLevels);

        bool lbChanged = false;

        if (miShownHealthLevel < 0 || miShownManaLevel < 0)
        {
            miShownHealthLevel = liHealth;
            miShownManaLevel = liMana;
            lbChanged = true;
        }

        int liSteps = mOFlaskLevelClock.Advance(Time.deltaTime);

        for (int liStep = 0; liStep < liSteps; liStep++)
        {
            if (miShownHealthLevel != liHealth)
            {
                miShownHealthLevel += miShownHealthLevel < liHealth ? 1 : -1;
                lbChanged = true;
            }

            if (miShownManaLevel != liMana)
            {
                miShownManaLevel += miShownManaLevel < liMana ? 1 : -1;
                lbChanged = true;
            }
        }

        // A flask whose level is moving does not bubble - and neither does one during the fill
        // animation of the resurrection (see StartFlaskFill).
        bool lbSettled = mfFlaskFillStart < 0f;

        lbChanged |= mOHealthBubbles.Advance(Time.deltaTime, miShownHealthLevel,
            lbSettled && miShownHealthLevel == liHealth);

        lbChanged |= mOManaBubbles.Advance(Time.deltaTime, miShownManaLevel,
            lbSettled && miShownManaLevel == liMana);

        if (lbChanged)
            fUpdateFlasks();
    }

    /// <summary>The level shown as a fill fraction - half a step into its band, so the rounding
    /// in UWFlasks lands on exactly this level.</summary>
    private float fGetShownFill(int piLevel)
    {
        return (piLevel + 0.5f) / FlaskLevels;
    }

    private void fUpdateFlasks()
    {
        // The level that is currently shown, not the real one - it walks there step by step
        // (see fTickFlaskBubbles).
        float lfHealth = miShownHealthLevel >= 0 ? fGetShownFill(miShownHealthLevel) : (MaxHP <= 0f ? 0f : CurrentHP / MaxHP);
        float lfMana = miShownManaLevel >= 0 ? fGetShownFill(miShownManaLevel) : (MaxMana <= 0f ? 0f : CurrentMana / MaxMana);

        // While the fill animation runs the flasks show at most the animated level (see
        // StartFlaskFill); the real values are set already.
        if (mfFlaskFillStart >= 0f)
        {
            UnderworldRevisited.UWSettings lOSettings = UnderworldRevisited.UWSettings.Instance;
            float lfStep = lOSettings != null ? lOSettings.FlaskFillStepSeconds : 0.25f;
            float lfShown = lfStep <= 0f ? 1f : Mathf.Floor((Time.time - mfFlaskFillStart) / lfStep) / FlaskLevels;

            if (lfShown >= lfHealth && lfShown >= lfMana)
                mfFlaskFillStart = -1f;

            lfHealth = Mathf.Min(lfHealth, lfShown);
            lfMana = Mathf.Min(lfMana, lfShown);
        }

        // From the cache: previously every change created a new texture that was
        // never released (see fGetCurrentWeaponTexture).
        // A POISONED CHARACTER HAS A GREEN FLASK. FLASKS.GR carries the green set, but only the red
        // one was ever used, so a poisoning could not be seen at all (per user on the poison
        // needles of level 3, 2026-09-17: "not poisoned", while the poison was set).
        //
        // WITH THE POISON MARK ON (colour help, see UWColourVision) the green flask is hatched as
        // well - red and green is exactly what a red-green deficiency cannot tell apart.
        bool lbPoisoned = Poison > 0;

        HealthFlaskTeture = fGetFlaskTexture(
            lbPoisoned ? UWFlasks.FlaskTypeEnum.Green : UWFlasks.FlaskTypeEnum.Red, lfHealth,
            lbPoisoned && UWUserSettings.MarkPoison, mOHealthBubbles.Frame);

        // Until 2026-08-29 the mana flask computed with CurrentHP/MaxHP - so both flasks
        // showed the same value. It only went unnoticed because there was no mana at all.
        ManaFlaskTeture = fGetFlaskTexture(UWFlasks.FlaskTypeEnum.Blue, lfMana, false, mOManaBubbles.Frame);
    }

    /// <summary>
    /// A flask picture, by colour, fill level and hatching.
    ///
    /// ITS OWN CACHE, because UWFlasks builds a NEW UWTexture on every call: the cache over the
    /// texture object never found it again and kept a fresh Texture2D per change. The key is
    /// what the picture is made of, and the whole cache is thrown away when the colour help
    /// changes (see UWColourVision).
    /// </summary>
    private Texture2D fGetFlaskTexture(UWFlasks.FlaskTypeEnum peType, float pfFill, bool pbHatch, int piBubbleFrame)
    {
        int liLevel = Mathf.Clamp((int)(pfFill * FlaskLevels), 0, (int)FlaskLevels);
        int liKey = ((int)peType * 1000) + (liLevel * 2) + (pbHatch ? 1 : 0)
            + ((piBubbleFrame + 1) * 10000);

        if (miFlaskColourVersion != UWColourVision.Version)
        {
            foreach (Texture2D lOOld in mOFlaskCache.Values)
                Destroy(lOOld);

            mOFlaskCache.Clear();

            miFlaskColourVersion = UWColourVision.Version;
        }

        Texture2D lOTexture;

        if (mOFlaskCache.TryGetValue(liKey, out lOTexture) && lOTexture != null)
            return lOTexture;

        UWTexture lOFlask = mFlasks.GetFlask(peType, pfFill, pbHatch, piBubbleFrame);

        lOTexture = new Texture2D(lOFlask.Width, lOFlask.Height, TextureFormat.ARGB32, false);
        lOTexture.name = "UWCharacter flask";
        lOTexture.filterMode = TextureFilterMode;
        lOTexture.SetPixels32(UWGameUI.fGetTextureInvert(lOFlask));
        lOTexture.Apply();

        mOFlaskCache[liKey] = lOTexture;

        return lOTexture;
    }

    private readonly System.Collections.Generic.Dictionary<int, Texture2D> mOFlaskCache
        = new System.Collections.Generic.Dictionary<int, Texture2D>();

    private int miFlaskColourVersion = -1;
}
