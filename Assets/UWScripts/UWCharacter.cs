using UnityEngine;
using System.Collections;
using UWDataImport.UWData;
using UWDataImport;

// Before the movement: the frame moves with this frame's abilities (UWUpdateOrder, cleanup 4.7).
[DefaultExecutionOrder(UWUpdateOrder.Character)]
public class UWCharacter : MonoBehaviour, IUWVitalsHost
{
    public FilterMode TextureFilterMode = FilterMode.Point;

    public int CharIndex = 9;

    public bool IsMale
    {
        get { return CharIndex < 5 ? true : false; }
    }

    /// <summary>The weapon in the hand when the game starts, until Interaction sets the real one
    /// (the scene has the axe). The weapon state itself is UWWeaponView's since cleanup step 4.6
    /// (2026-10-18); this was the public CurrentWeaponType until then.</summary>
    [SerializeField]
    [UnityEngine.Serialization.FormerlySerializedAs("CurrentWeaponType")]
    private WeaponTypes meStartWeaponType = WeaponTypes.RightHandSword;

    /// <summary>
    /// The weapon hand and the power gem in the view window (UWWeaponView, its swing engine-free
    /// in UWWeaponSwing) - part of this class until cleanup step 4.6 (2026-10-18).
    /// </summary>
    public UWWeaponView Weapon => mOWeapon;

    private readonly UWWeaponView mOWeapon = new UWWeaponView();

    /// <summary>
    /// The paper doll - the body and the five armour slots (UWPaperdollView, the pictures chosen by
    /// UWPaperdollRules) - part of this class until cleanup step 4.6 (2026-10-18).
    /// </summary>
    public UWPaperdollView Paperdoll => mOPaperdoll;

    private readonly UWPaperdollView mOPaperdoll = new UWPaperdollView();

    private DataImport mOUWData;
    private Interaction mInteraction;
    private UWInventory mInventory;

    /// <summary>
    /// The health and mana flasks (UWFlaskView, the shown levels and the bubbling in the engine-free
    /// UWFlaskGauge) - part of this class until cleanup step 4.6 (2026-10-18).
    /// </summary>
    public UWFlaskView Flasks => mOFlasks;

    private readonly UWFlaskView mOFlasks = new UWFlaskView();

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
    /// separation, 2026-09-18). This class hosts them (IUWVitalsHost); the callers read and change
    /// them through Vitals since cleanup step 4.6 (2026-10-18), which took out the forwards of the
    /// old API, and the flasks, the paper doll and the weapon into views of their own.
    /// </summary>
    private UWPlayerVitals mOVitals;

    private UWPlayerVitals fVitals => mOVitals ?? (mOVitals = fCreateVitals());

    /// <summary>The vitals with this character listening to everything they tell: the callbacks
    /// of IUWVitalsHost until cleanup step 3.6 (2026-10-17), events on UWPlayerVitals since, each
    /// going to the method that implemented the callback.</summary>
    private UWPlayerVitals fCreateVitals()
    {
        UWPlayerVitals lOVitals = new UWPlayerVitals(this);

        lOVitals.VitalsChanged += OnVitalsChanged;
        lOVitals.DamageRecorded += OnDamageRecorded;
        lOVitals.CombatShakeDue += ShakeOnDamage;
        lOVitals.Damaged += fOnDamaged;
        lOVitals.SwimCheckDue += fOnSwimCheckDue;
        lOVitals.UnsteadyShakeDue += ShakeUnsteady;
        lOVitals.MessageDue += AddMessage;
        lOVitals.KillRewarded += OnKillRewarded;
        lOVitals.StatusPassDue += OnStatusPassDue;
        lOVitals.FiveMinuteBlockDue += OnFiveMinuteBlock;
        lOVitals.IdentificationReset += ResetIdentification;
        lOVitals.ActiveSpellsChanged += OnActiveSpellsChanged;

        return lOVitals;
    }

    /// <summary>The vitals themselves - health, mana, attributes, skills, experience, hunger, poison,
    /// the clock and the active spells, for every reader (the HUDs, the save writer, the
    /// conversations, the spells, the engine-free item use).</summary>
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

    /// <summary>The player as the defender of a blow or a missile (UWAttackResolution, cleanup
    /// step 4.9, 2026-10-18).</summary>
    public UWPlayerCombatant Combatant => mOCombatant ??= new UWPlayerCombatant(this);

    private UWPlayerCombatant mOCombatant;

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

    /// <summary>How many dice the worn curses roll - see UWDamageRequest.Curse.
    /// </summary>
    public int CurseDice => mOStatus.CurseDice;

    /// <summary>
    /// Damage to the player: the request through the damage pipeline (UWDamagePipeline, cleanup
    /// step 4.2, 2026-10-17) - the world's player end (UWWorld.Bodies) and the character's own
    /// sites (the curse of the status pass) come here. What the player sees and hears of it
    /// follows in OnDamaged.
    /// </summary>
    public UWDamageOutcome Damage(in UWDamageRequest pORequest)
    {
        return UWDamagePipeline.Apply(fVitals, pORequest);
    }

    /// <summary>The vitals' SwimCheckDue, on the player tick: the swim counter's check and the
    /// drowning (UWPlayerTerrain.RunSwimCheck; cleanup step 4.4, 2026-10-17).</summary>
    private void fOnSwimCheckDue()
    {
        UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

        if (lOTerrain != null)
            lOTerrain.RunSwimCheck();
    }

    /// <summary>
    /// The vitals' Damaged, once per request: the view flashes when it landed (the critical
    /// blow's red, drowning's blue, and 0xA8 when a curse, a backfire or the leeches struck -
    /// backfire flashes for the player whoever called it, UnderworldExhumed SPELLS.C; until
    /// 2026-10-12 equipping a cursed item did not flash, as the reference has it; in uw1 there is
    /// no message), and the combat music hears of a creature's blow or missile, or of his own
    /// missile (UWMusic, hit or not; his own since 2026-10-19, UWDamageMusic). Each site flashed and told the music itself until cleanup step 4.2 (2026-10-17).
    /// </summary>
    private void fOnDamaged(UWDamageRequest pORequest, UWDamageOutcome pOOutcome)
    {
        if (pOOutcome.Landed && pORequest.FlashColour != UWDamageRequest.NoFlash)
            fPresentation.FlashWindow(pORequest.FlashColour, pORequest.FlashSeconds);

        if (pORequest.TellsMusic)
            UWMusic.OnPlayerDamaged(fVitals.CurrentHP, fVitals.MaxHP, pORequest.Music == UWDamageMusic.Player);
    }

    /// <summary>What the vitals show and say (cleanup step 3.1, 2026-10-17): the lines go to this
    /// character's Interaction, or nowhere without one, as AddMessage did; the curse flash and the
    /// unsteady shake go through it too.</summary>
    private IUWPresentation fPresentation => UWPresentation.For(UWScene.World, mInteraction);

    /// <summary>For maze navigation, which recolours the level geometry.</summary>
    private UWLevelLoader mOLevelLoaderForStatus;

    /// <summary>
    /// Recomputes the whole status - armour, protection, magic light and the
    /// movement abilities, from the worn equipment and the active spells.
    ///
    /// Ran every frame until cleanup step 4.6 (2026-10-18), just as the reference runs its status
    /// pass in its loop - cheaper than tracking every place where equipment might change, and
    /// the reason why an enchantment on a ring previously had no effect at all. SINCE 4.6 IT RUNS
    /// ON CHANGE: what it reads changes only with the inventory (every InventoryChanged, the quiet
    /// ones too: equipping, wear, repair, a load) and with the active spells
    /// (OnActiveSpellsChanged), and the status pass and the callers that need it fresh (a
    /// creature's blow, the debug overlay) call it themselves. mbStatusStale carries the change to
    /// the next Update, where the per-frame call stood.
    /// </summary>
    public void RefreshStatus()
    {
        if (mOInventory == null)
            mOInventory = UWScene.Inventory;

        // The engine-free model is the authority on what is worn (IUWEquipment, which the
        // inventory implemented by forwarding to it, is gone since cleanup step 3.6, 2026-10-17).
        UWArmourProtection.Compute(mOInventory != null ? mOInventory.Model : null, mOUWData, fVitals.ActiveSpells,
            mOStatus);

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
            mOMovement.MagicalMotionAbilities = mOStatus.MotionAbilities;

        if (mOLevelLoaderForStatus == null)
            mOLevelLoaderForStatus = UWScene.LevelLoader;

        if (mOLevelLoaderForStatus != null)
            mOLevelLoaderForStatus.SetMazeNavigation(mOStatus.HasMazeNavigation);

        // Stale until everyone who hears of the status has been found: the per-frame pass reached
        // a light, a movement or a loader that came later on the next frame.
        mbStatusStale = mOLighting == null || mOMovement == null || mOLevelLoaderForStatus == null;
    }

    /// <summary>Something the status is made of changed since the last RefreshStatus - see there.
    /// </summary>
    private bool mbStatusStale = true;

    /// <summary>
    /// The switch for the original's dead resistance nibble - see
    /// UWArmourProtection.ResistBlowsAddsArmour. ONCE PER GAME since cleanup step 4.6
    /// (2026-10-18): the session that keeps it is new with every start (UWLevelLoader), and Init
    /// follows; until then every status refresh, i.e. every frame, wrote it again.
    /// </summary>
    private static void fApplyRuleSwitches()
    {
        UWArmourProtection.ResistBlowsAddsArmour = UnderworldRevisited.UWSettings.Instance != null
            && UnderworldRevisited.UWSettings.Instance.ResistBlowsAddsArmour;
    }

    /// <summary>A status pass of the original is due - see fRunStatusPass.</summary>
    private bool mbStatusPassDue;

    /// <summary>The vitals' StatusPassDue: a spell began or ended, the mushroom, sleep, a mantra - the
    /// original runs FixPlayerEquips now.</summary>
    public void OnStatusPassDue()
    {
        mbStatusPassDue = true;
    }

    /// <summary>An inventory change: every one except opening, closing and scrolling a container
    /// and a light that only burned down a point is a status pass in the original (pick up,
    /// drop, equip, move, use up - UWInventoryModel.IsQuietChange).</summary>
    private void fOnInventoryChangedForStatusPass()
    {
        // Every change may touch what is worn - the quiet ones are no status pass of the
        // original, but the per-frame refresh saw them too (cleanup step 4.6).
        mbStatusStale = true;

        if (mInventory != null && !mInventory.Model.IsQuietChange)
            mbStatusPassDue = true;
    }

    /// <summary>
    /// THE CURSE STRIKES ON THE ORIGINAL'S STATUS PASS (UnderworldExhumed PLAYDATA.C
    /// FixPlayerEquips -> player_affected_by class 9 -> SPELLS.C backfire, per user's decision of
    /// 2026-10-12): whenever the original runs it - equipping, picking up, dropping, any move in
    /// the inventory, using something up, a spell beginning or ending, a light going out, the
    /// mushroom, sleep, a mantra, a level change or load -, and then every time, with the 0xA8
    /// flash. Standing still with a cursed crown costs nothing. Until 2026-10-12 ours struck on
    /// every player tick (21 s) and on equipping only.
    ///
    /// ONE STRIKE PER FRAME: everything that asked for a pass since the last frame strikes once.
    /// The original calls the pass once per routine; a single drag of ours can raise several
    /// inventory changes in one frame, which must not strike several times.
    ///
    /// The worn curses strike TOGETHER with their summed dice (UWArmourProtection.Status.CurseDice):
    /// the original rolls each cursed item on its own, but backfire rolls before it looks at the
    /// hit points and leaves at least three, so the dice in the same order give the same hit
    /// points either way - only the flash would come twice.
    /// </summary>
    private void fRunStatusPass()
    {
        if (!mbStatusPassDue)
            return;

        mbStatusPassDue = false;

        RefreshStatus();

        Damage(UWDamageRequest.Curse(mOStatus.CurseDice));
    }

    private UWInventory mOInventory;

    // ------------------------------------------------- What the vitals tell

    /// <summary>The vitals' IdentificationReset: better Lore makes the world unidentified again.</summary>
    public void ResetIdentification()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader == null || lOLoader.CurrentLevel == null || lOLoader.UWDataImporter == null)
            return;

        UWLoreCheck.ResetAttempts(lOLoader.CurrentLevel.Masterlist,
            lOLoader.UWDataImporter.CommonObjectProperties);
    }

    /// <summary>The vitals' FiveMinuteBlockDue: the level's share of the five-minute block, in duration_check's
    /// order - the wandering monsters' one-in-four roll (DoWanderingMonsters(1), the respawner
    /// with its distance test), then the creatures' checkup (UWLevelExit.YearlyCheckup,
    /// yearly_checkup).</summary>
    public void OnFiveMinuteBlock()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader == null || lOLoader.CurrentLevel == null)
            return;

        if (UWRespawnRules.RollOpportunity())
            UWTriggerSystem.RespawnCreatures(lOLoader, false);

        UWLevelExit.YearlyCheckup(lOLoader.TileQueries);
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

    public void Init(DataImport pOUWData)
    {
        mOUWData = pOUWData;

        // Before the first status refresh (seeding the spells below runs one).
        fApplyRuleSwitches();

        // The time since the last tick counts on - see msLastGameTickRealtime.
        mOGameTick.Preload(Time.realtimeSinceStartup - msLastGameTickRealtime);

        // The weapon sheathed and the gem inactive (UWWeaponView, cleanup step 4.6); the blow's
        // noise goes to the vitals.
        mOWeapon.Init(mOUWData, meStartWeaponType, TextureFilterMode);
        mOWeapon.Struck -= fOnStruck;
        mOWeapon.Struck += fOnStruck;

        // The body picture from the save game, if one is loaded (UWPaperdollRules.GetBodyIndex);
        // the Inspector's value otherwise.
        CharIndex = UWPaperdollRules.GetBodyIndex(mOUWData != null ? mOUWData.InitialPlayer : null, CharIndex);

        // No manual wiring in the scene needed - same self-creation pattern
        // as UWInventory in Interaction.Awake().
        mInventory = GetComponent<UWInventory>();

        if (mInventory == null)
            mInventory = gameObject.AddComponent<UWInventory>();

        // The paper doll follows the inventory (UWPaperdollView, cleanup step 4.6).
        mOPaperdoll.Init(mOUWData, CharIndex, TextureFilterMode, mInventory);

        mInventory.InventoryChanged += fOnInventoryChangedForStatusPass;

        // After the inventory exists: seeding the active spells runs the status pass, which
        // needs it - the same place the health, mana and spells were set before P3.
        fInitialiseVitals();

        mOFlasks.Init(pOUWData.Flasks, TextureFilterMode, fVitals);

        mInteraction = GetComponentInParent<Interaction>();
    }

    /// <summary>The weapon's blow is loud (reference: combat_input.AttackTarget).</summary>
    private void fOnStruck()
    {
        fVitals.MakeNoise(StrikeNoise);
    }

    private void OnDestroy()
    {
        mOPaperdoll.Detach();
        mOPaperdoll.Release();
    }

    public float LastDamageTime { get; private set; } = -1f;

    /// <summary>The vitals' DamageRecorded: a hit or a curse was recorded - stamp the time.</summary>
    public void OnDamageRecorded()
    {
        LastDamageTime = Time.time;
    }

    /// <summary>The level of the last combat shake (0 to 3) and when it came, for the F1
    /// overlay - so a test can see whether a hit shook and how hard (2026-10-19: the user saw no
    /// shake from a trap's arrow and the overlay showed no level).</summary>
    public int LastShakeLevel { get; private set; } = -1;

    public float LastShakeTime { get; private set; } = -1f;

    /// <summary>The vitals' CombatShakeDue: a blow or a missile shakes the view.</summary>
    public void ShakeOnDamage(int piLevel)
    {
        LastShakeLevel = piLevel;
        LastShakeTime = Time.time;
        UWScreenShake.ShakeOnDamage(piLevel);
    }

    /// <summary>The vitals' UnsteadyShakeDue: a failed sip shakes the view.</summary>
    public void ShakeUnsteady(int piDuration)
    {
        fPresentation.ShakeScreen(piDuration);
    }

    /// <summary>The vitals' MessageDue: a message for the scroll (the level-up line).</summary>
    public void AddMessage(string psMessage)
    {
        fPresentation.AddMessage(psMessage);
    }

    /// <summary>The vitals' KillRewarded: a creature was slain. A dragon nods (UW.EXE
    /// AwardKillEXP_seg022_1725, before the fanfare); the victory fanfare only if a reward is
    /// due (reference: AwardXPKill).</summary>
    public void OnKillRewarded(bool pbRewarded)
    {
        UWHudDragons.Request(UWHudDragons.NodAnimation);

        if (pbRewarded)
            UWMusic.ChangeTheme(UWMusic.FanfareTheme);
    }

    // ------------------------------------------------- Active spells

    /// <summary>
    /// Is Freeze Time currently active? Then no critter moves any more (see UWCritter).
    ///
    /// STATIC, because every critter of the level needs this in its own frame update and there
    /// is only one player character - the same pattern as UWConversationController.IsAnyOpen.
    /// </summary>
    public static bool TimeIsFrozen { get; private set; }

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

    /// <summary>The feet in zpos steps - the height the original keeps in the player object,
    /// from the motion core since cleanup 4.1 (2026-10-18, UWPlayerMovement.FeetZPos: the params'
    /// height over eight, as phys_affect_player writes it); until then the bottom of the
    /// CharacterController's bounds, rounded. The rules that count on the tile grid measure from
    /// here (UWReachRules, UWArmourProtection.PickBodyPart).</summary>
    public int GetFeetZPos()
    {
        UWPlayerMovement lOMovement = UWScene.PlayerMovement;

        return lOMovement != null ? lOMovement.FeetZPos : UWUnits.WorldToZPos(transform.position.y);
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

    /// <summary>
    /// The vitals' ActiveSpellsChanged: the set of active spells changed. Class 11 comes decoded; light,
    /// motion, armour and protection depend not only on spells but also on equipment, and
    /// the same status pass computes both.
    /// </summary>
    public void OnActiveSpellsChanged(bool pbHastened, bool pbRoamingSight, bool pbTimeFrozen, bool pbTelekinesis)
    {
        TimeIsFrozen = pbTimeFrozen;

        // Roaming Sight among the active spells is the original's WizEye: the view is lit at
        // level 6 (UWLighting.RoamingSightLight, FixPlayerEquips; 2026-10-15).
        if (mOLighting == null)
            mOLighting = UWScene.Lighting;

        if (mOLighting != null)
            mOLighting.RoamingSightLight = pbRoamingSight;

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

    /// <summary>IUWVitalsHost: the level the player is on, for the orb's mana rule - 0-based, -1
    /// without a world (the vitals' convention, UWWorldLevels).</summary>
    public int DungeonLevelIndex => UWWorldLevels.IndexOr(UWScene.World, -1);

    /// <summary>
    /// IUWVitalsHost: the regeneration bits of the worn equipment, for the tick - a Ring of
    /// Regeneration gives one hit point per tick from here. The status refresh keeps them in
    /// mOStatus (every frame until cleanup step 4.6, on change since), so no extra timer is
    /// needed; see UWWornRegeneration.
    /// </summary>
    public int WornRegeneration => mOStatus.RegenerationBits;

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

    /// <summary>
    /// IUWVitalsHost: the original's motion_state is not zero - swimming (not water walking),
    /// on lava, floating with Levitate or Fly, or the view still shaking (the combat shake or
    /// the tremor). PLAYMOVE.C set_sound adds four for any of them; until 2026-10-12 ours only
    /// for the liquid.
    /// </summary>
    public bool IsInMotionState
    {
        get
        {
            UWPlayerTerrain lOTerrain = UWScene.PlayerTerrain;

            if (lOTerrain != null && (lOTerrain.IsSwimming || lOTerrain.IsOnLava))
                return true;

            if (mOMovementForNoise != null && mOMovementForNoise.IsHovering)
                return true;

            if (mOShakeForNoise == null)
                mOShakeForNoise = UWScreenShake.Ensure();

            return mOShakeForNoise != null && mOShakeForNoise.IsShaking;
        }
    }

    private UWScreenShake mOShakeForNoise;

    /// <summary>IUWVitalsHost: winding up a blow is loud (reference: CombatStages.Charging).</summary>
    public bool IsChargingAttack => mOWeapon.IsChargingAttack;

    /// <summary>The vitals' VitalsChanged: health, mana or poison changed - the flasks show it. Before
    /// Init has handed over the flask pictures there is nothing to draw yet.</summary>
    public void OnVitalsChanged()
    {
        mOFlasks.Refresh();
    }

    private const int StrikeNoise = UWPlayerVitals.StrikeNoise;
	// Update is called once per frame
	void Update ()
    {
        // Not initialised: no game data was found and the setup screen is up (UWSetupMenu).
        // Every frame threw a NullReferenceException further down (per user, 2026-09-17).
        if (mInteraction == null)
            return;

        // The status first: armour, protection, magic light and the movement abilities from
        // equipment and active spells - when something they are made of changed since the last
        // refresh (every frame until cleanup step 4.6, see RefreshStatus).
        if (mbStatusStale)
            RefreshStatus();

        fTickGameClock();

        // After the tick: a spell that ended or a light that went out asks for a pass too.
        fRunStatusPass();

        fTickQuietness();

        // The flasks: the fill animation's redraw, the levels' steps and the bubbling
        // (UWFlaskView, cleanup step 4.6).
        mOFlasks.Tick(Time.deltaTime);

        // Until 2026-08-29 a debug counter ran here that raised the hit points by one every
        // second and reset them to 0 on overflow - only so that the flask
        // in the UI moved. It thereby overwrote every real value, which is why
        // damage and the fountain effect had no effect.

        // A CONVERSATION STOPS THE WEAPON. In the original the conversation is a modal loop and
        // the game loop stands still meanwhile: a combat mode toggled from the weapon hand during
        // the conversation (see UWItemDrag) draws the weapon only once the conversation has ended,
        // the same for sheathing and for a strike (per user on the original, 2026-09-18).
        // Everything from here to the end of Update is the weapon.
        if (UWConversationController.IsAnyOpen)
            return;

        // The weapon hand and the gem (UWWeaponView and its engine-free UWWeaponSwing since
        // cleanup step 4.6, 2026-10-18; the state machine stood here until then).
        mOWeapon.Tick(Time.deltaTime, mInteraction);
    }
}
