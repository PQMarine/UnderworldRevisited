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

    /// <summary>The second game's player tick, 20 seconds (UWPlayerVariantUw2.TickLength; uw2 ui/INTERACT.C:116-118). Until
    /// 2026-11-26 the first game's 21 (duplication audit G1).</summary>
    private readonly UWTickClock mOClock = new UWTickClock(UWPlayerVariantUw2.TickLength);

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
        {
            UWArmourProtection.Compute(lOInventory, mOData, mOVitals.ActiveSpells, mOStatus);

            // The stealth of the worn things at once with the pass, as UWCharacter.RefreshStatus sets it (duplication audit
            // G4, 2026-11-26: here it came only with the next tick).
            mOVitals.StealthBonus = mOStatus.StealthBonus;
        }

        // THE MOTION SPELLS IN THE SESSION (per user, 2026-11-06: Bouncing did nothing; magic stage 1 left "the motion
        // spells' effect on the walk" open): Leap, Slow Fall, Levitate, Water Walk, Fly and Bouncing, as UWCharacter
        // hands them on in the first game, which stands idle here.
        if (UWScene.PlayerMovement != null)
            UWScene.PlayerMovement.MagicalMotionAbilities = mOStatus.MotionAbilities;

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

    /// <summary>The active spells hold Roaming Sight (the vitals' ActiveSpellsChanged).</summary>
    private bool mbRoaming;

    private UWRoamingSight mORoamingSight;

    /// <summary>Paralysed (the game's counter); a cast waits for it.</summary>
    public bool IsParalysed => mOFile != null && UWPlayerRecordUw2.Paralysis(mOFile) != 0;

    /// <summary>
    /// THE SECOND GAME'S PARALYSIS (part E2, the tym rune): do_player_input gives no speed (PHYSICS.C:662), the view
    /// (mous_in_3d) and the right panel (mous_in_panel) take no click, fight cannot be chosen and no rune cast begins
    /// (RUNES.C:174). For the shared walk, the attack and the screen; false on the first game.
    /// </summary>
    public static bool PlayerParalysed => Current != null && Current.IsParalysed;

    /// <summary>sleepbits and in_void (G6, the Void): the panel, the inventory and the fight are shut.</summary>
    public static bool PlayerAsleepInVoid => UWGameSession.Current != null && UWVoidRulesUw2.IsAsleepInVoid(UWGameSession.Current.PlayerFileUw2);

    /// <summary>Freeze Time is on (TimeStop): Killorn's countdown holds.</summary>
    private bool mbTimeFrozen;

    /// <summary>The real seconds toward the next step down of the paralysis.</summary>
    private float mfParalysisSeconds;

    /// <summary>
    /// The frame loop takes the counter down by one whenever the clock's second changes (INTERACT.C:111-121: the
    /// game_clock >> 8, 256 units a real second) - so 4 to 19 seconds for the tym rune. Not while the options panel
    /// stands: the original's clock does not run there.
    /// </summary>
    private void fTickParalysis()
    {
        int liLeft = UWPlayerRecordUw2.Paralysis(mOFile);

        if (liLeft <= 0 || UWGameScreenUw2.IsOptionsOpen)
        {
            if (liLeft <= 0)
                mfParalysisSeconds = 0f;

            return;
        }

        mfParalysisSeconds += Time.deltaTime;

        while (mfParalysisSeconds >= 1f && liLeft > 0)
        {
            mfParalysisSeconds -= 1f;
            liLeft--;
        }

        UWPlayerRecordUw2.SetParalysis(mOFile, liLeft);
    }

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
        fTickParalysis();

        int liTicks = mOClock.Advance(Time.deltaTime);

        UWInventoryModel lOInventory = UWGameScreenUw2.Current != null ? UWGameScreenUw2.Current.Inventory : null;

        // THE STATUS AT ONCE AFTER A LOAD (per user, 2026-11-07: the debug overlay showed no armour until a piece was
        // moved): as soon as the screen has the inventory, not at the first tick - worn light and motion from the
        // start. RefreshStatus only, without the curse's strike.
        if (!mbFirstStatusDone && lOInventory != null)
        {
            mbFirstStatusDone = true;
            RefreshStatus();
        }

        for (int liTick = 0; liTick < liTicks; liTick++)
        {
            // The game clock goes on at real time (256 units a second): one tick is twenty seconds of it here.
            mOVitals.RunGameTick(UWPlayerVariantUw2.TickLength * UWPlayerTick.PitTicksPerSecond, DamageTypeProof);

            // The lights burn on the same tick and its counter (duration_check, PLAYTIME.C:88).
            if (lOInventory != null)
                lOInventory.BurnTick(1, mOVitals.TickInCycle);
        }

        fRunStatusPass();

        // A spell began or ended, by its time or in the night: the record and the icons follow.
        if (mbSpellsChanged)
        {
            mbSpellsChanged = false;
            UWPlayerRecordUw2.WriteActiveSpells(mOFile, mOVitals.ActiveSpells);

            // The view follows the effect, as UWCharacter.fApplyRoamingSight on the first game: every ending - its
            // time, its icon, the night, a load - goes through here.
            if (mbRoaming)
                BeginRoamingSight(false);
            else if (mORoamingSight != null)
                mORoamingSight.Stop();

            // WizEye lights the view at level 6 (uw2 game/PLAYDATA.C:413, FixPlayerEquips), as UWCharacter's
            // OnActiveSpellsChanged does on the first game (duplication audit G2, 2026-11-26: the second game's view
            // stayed at the player's own light).
            if (UWScene.Lighting != null)
                UWScene.Lighting.RoamingSightLight = mbRoaming;

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
        mbFirstStatusDone = false;
        mOVitals.MessageDue += UWGameScreenUw2.Say;

        // At once: a blow lands between the ticks, and the record is what the flasks read and what comes back
        // before the next tick (fTakeFromRecord).
        mOVitals.VitalsChanged += WriteToRecord;
        mOVitals.ExperienceChanged += fExperienceChanged;
        mOVitals.CombatShakeDue += UWScreenShake.ShakeOnDamage;
        mOVitals.UnsteadyShakeDue += fShakeUnsteady;
        mOVitals.DayStepDue += fStepDay;
        mOVitals.StatusPassDue += RequestStatusPass;

        // The view's flash of a landed request (a creature's critical, the curse, the leech, drowning), as UWCharacter
        // shows it in the first game (per user, 2026-11-17: no red flash when hit), and the combat music (phase 5 part B).
        mOVitals.Damaged += fOnDamaged;

        // A creature the player killed: the victory theme (player_killed_a, COMBAT.C:940; UW2 plan phase 5 part B).
        mOVitals.KillRewarded += fOnKill;

        // G3 part H5: the level's share of the 30-tick block (duration_check, uw2 PLAYTIME.C:139) - the wandering
        // monsters' one-in-four roll (DoWanderingMonsters(1), out of the player's reach) and yearly_checkup.
        mOVitals.FiveMinuteBlockDue += fOnFiveMinuteBlock;

        // G2: the swimming check on the player tick, the first game's (UWPlayerTerrain.RunSwimCheck).
        mOVitals.SwimCheckDue += () =>
        {
            if (UWScene.PlayerTerrain != null)
                UWScene.PlayerTerrain.RunSwimCheck();

            // duration_check's next two (G6): Killorn's countdown and the Void's sleep.
            fKillornCountdown();

            if (UWVoidRulesUw2.TickSleep(mOFile))
                UWSleepUw2.WakeFromVoid();
        };
        // Lore rose: the level's lore tries are cleared, as on the first game (uw2 game/SKILLS.C:180-184; duplication
        // audit G3, 2026-11-26 - it was never subscribed here).
        mOVitals.IdentificationReset += UWCharacter.ResetWorldIdentification;

        mOVitals.ActiveSpellsChanged += (pbHastened, pbRoamingSight, pbTimeFrozen, pbTelekinesis) =>
        {
            mbSpellsChanged = true;
            mbRoaming = pbRoamingSight;
            mbTimeFrozen = pbTimeFrozen;
        };
        UWCombatVariantUw2.PlayerArmour = GetArmourAt;
        mOVitals.Initialise(pOData, UWPlayerVitals.StartValues.Default, UWPlayerData.FromSecondGame(pOFile));
    }

    /// <summary>The second game's block: the wandering monsters' roll, then the creatures' checkup (the first game's
    /// UWCharacter.OnFiveMinuteBlock, in duration_check's order).</summary>
    private void fOnFiveMinuteBlock()
    {
        UWLevelLoader lOLoader = UWScene.LevelLoader;

        if (lOLoader == null || lOLoader.CurrentLevel == null)
            return;

        if (UWRespawnRules.RollOpportunity())
            UWTriggerSystem.RespawnCreatures(lOLoader, false);

        UWLevelExit.YearlyCheckup(lOLoader.TileQueries);
    }

    /// <summary>
    /// ROAMING SIGHT (UW2 plan G1 part D3): xt_spells' home_cam(0) and attach_eye(-1) - the view rises from the
    /// player's spot and facing (UWRoamingSight, the first game's), the body stays. pbWithoutSpell: cast with all
    /// slots taken, nothing in the spells ends it. Escape (modern) and a level change end the effect here.
    /// </summary>
    public void BeginRoamingSight(bool pbWithoutSpell)
    {
        if (mORoamingSight == null)
        {
            mORoamingSight = gameObject.GetComponent<UWRoamingSight>();

            if (mORoamingSight == null)
                mORoamingSight = gameObject.AddComponent<UWRoamingSight>();

            mORoamingSight.SpellEnder = () =>
            {
                if (mOVitals != null)
                    mOVitals.EndSpellEffect(UWMiscSpellRules.MajorClass, UWMiscSpellRules.RoamingSightEffectMinor);
            };
            mORoamingSight.PointerCell = fRoamingPointerCell;
        }

        Transform lOBody = UWScene.Player != null ? UWScene.Player.transform : null;
        Camera lOView = Camera.main;
        Vector3 lOFrom = lOBody != null ? lOBody.position : lOView != null ? lOView.transform.position : Vector3.zero;
        float lfYaw = lOView != null ? lOView.transform.eulerAngles.y : 0f;

        mORoamingSight.Begin(lOFrom, lfYaw, pbWithoutSpell);
    }

    /// <summary>move_cam(0)'s thirds: the left button held in the classic view window (mous_in_3d's player_mous_move).</summary>
    private static bool fRoamingPointerCell(out int piColumn, out int piRow)
    {
        piColumn = 1;
        piRow = 1;

        UWGameScreenUw2 lOScreen = UWGameScreenUw2.Current;

        if (lOScreen == null || !UWPointer.HasMouse || !UWPointer.LeftHeld || !lOScreen.IsInViewWindow(UWPointer.Position))
            return false;

        piColumn = Mathf.Clamp(Mathf.FloorToInt(lOScreen.ViewAcrossOf(UWPointer.Position) * 3f), 0, 2);
        piRow = Mathf.Clamp(Mathf.FloorToInt((1f - lOScreen.ViewHeightOf(UWPointer.Position)) * 3f), 0, 2);

        return true;
    }

    private void fOnDamaged(UWDamageRequest pORequest, UWDamageOutcome pOOutcome)
    {
        // damage_critter on the player by someone (a creature's blow, a miss's 0 included, or a missile, his own too):
        // the danger or combat theme - not when it killed him (crit_die returns first, AI.C:1505).
        if (pORequest.TellsMusic && !pOOutcome.Died)
            UWAudioService.Uw2PlayerDamaged(mOVitals.CurrentHP, mOVitals.MaxHP);

        if (!pOOutcome.Landed || pORequest.FlashColour == UWDamageRequest.NoFlash)
            return;

        UWWorld lOWorld = UWWorld.Of(UWScene.LevelLoader);

        if (lOWorld != null)
            lOWorld.Presentation.FlashWindow(pORequest.FlashColour, pORequest.FlashSeconds);
    }

    /// <summary>player_killed_a: the victory theme for every creature the player kills, reward or not.</summary>
    private void fOnKill(bool pbRewarded)
    {
        UWAudioService.Uw2Request(UWMusicSelectorUw2.VictoryTheme);
    }

    /// <summary>Killorn's countdown (UWVoidRulesUw2.KillornCountdown): the shake each check, at the end the crash, the
    /// schedules and 0xFF to the player.</summary>
    private void fKillornCountdown()
    {
        UWVoidRulesUw2.CountdownEnum leStep = UWVoidRulesUw2.KillornCountdown(mOFile, mbTimeFrozen);

        if (leStep == UWVoidRulesUw2.CountdownEnum.None)
            return;

        UWScreenShake lOShake = UWScreenShake.Ensure();

        if (lOShake != null)
            lOShake.Shake(UWScreenShake.LargeChannel, UWVoidRulesUw2.CountdownShake);

        if (leStep != UWVoidRulesUw2.CountdownEnum.Crash)
            return;

        Debug.Log("[Session] Killorn Keep crashes.");
        UWScheduleHostUw2.RunAll();

        UWWorld lOWorld = UWWorld.Of(UWScene.LevelLoader);

        if (lOWorld != null)
            lOWorld.Damage(UWDamageTarget.ThePlayer, UWBlow.ToPlayer(UWDamageRequest.Plain(UWVoidRulesUw2.CrashDamage, UWDamageTypes.None)));
    }

    /// <summary>The hour: X clock 0 one step on, and block 0 of the schedules run up to it (PLAYTIME.C:155, G6 part 1).</summary>
    private void fStepDay()
    {
        UWPlayerRecordUw2.StepDayClock(mOFile);
        UWScheduleHostUw2.StepDay();
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

    /// <summary>A skill raised in a conversation (x_skills, G5 part 1): the twenty skills, the maxima player_compute gave
    /// and the skill points into the record, which the save, the panel and the next conversation read.</summary>
    public void WriteSkillsToRecord()
    {
        if (mOFile == null || mOVitals == null)
            return;

        UWPlayerRecordUw2.WriteSkills(mOFile, mOVitals.GetSkillByNumber);
        UWPlayerRecordUw2.WriteMaxima(mOFile, mOVitals.MaxHP, mOVitals.MaxMana);
        WriteToRecord();
    }

    // ------------------------------------------------- IUWVitalsHost

    public int DungeonLevelIndex => UWScene.LevelLoader != null ? UWScene.LevelLoader.CurrentLevelIndex : -1;

    /// <summary>The tile column of the session's eye, -1 without a level.</summary>
    public int PlayerTileX => UWScene.LevelLoader != null && UWScene.LevelLoader.CurrentLevel != null && UWViewpoint.Current != null
        ? UWScene.LevelLoader.WorldPositionToTile(UWViewpoint.Current.transform.position).X : -1;

    public int WornRegeneration => mOStatus.RegenerationBits;

    public bool IsMoving => UWScene.PlayerMovement != null && UWScene.PlayerMovement.IsMoving;

    public float MovementFraction => UWScene.PlayerMovement != null ? UWScene.PlayerMovement.MomentumFraction : 0f;

    /// <summary>
    /// The original's motion_state is not zero (PLAYMOVE.C set_sound adds four for it): swimming, on lava, floating
    /// with Levitate or Fly, or the view shaking - as UWCharacter.IsInMotionState, from the movement's own contact
    /// (G0 of the 2026-11-06 sweep: always false, the player too quiet).
    /// </summary>
    public bool IsInMotionState
    {
        get
        {
            UWPlayerMovement lOMovement = UWScene.PlayerMovement;

            if (lOMovement != null && (lOMovement.IsOnLava || lOMovement.IsHovering
                || (lOMovement.IsInWater && (lOMovement.MagicalMotionAbilities & UWPlayerVitals.WaterWalkBit) == 0)))
                return true;

            UWScreenShake lOShake = UWScreenShake.Ensure();

            return lOShake != null && lOShake.IsShaking;
        }
    }

    /// <summary>The status computed once since the save was taken up (Update).</summary>
    private bool mbFirstStatusDone;

    /// <summary>A status pass of the original is due (the vitals' StatusPassDue, an inventory change).</summary>
    private bool mbStatusPassDue;

    /// <summary>The original runs FixPlayerEquips now: at the frame's end, once however often it was asked.</summary>
    public void RequestStatusPass()
    {
        mbStatusPassDue = true;
    }

    /// <summary>
    /// THE STATUS PASS AND THE WORN CURSE (G0 of the 2026-11-06 sweep): as UWCharacter.fRunStatusPass, the status
    /// anew and the worn curses' summed dice through the damage pipeline (player_affected_by class 9 to backfire,
    /// uw2 PLAYDATA.C); until then the curse dice were worked out and never dealt.
    /// </summary>
    private void fRunStatusPass()
    {
        if (!mbStatusPassDue)
            return;

        mbStatusPassDue = false;
        RefreshStatus();

        if (mOStatus.CurseDice > 0)
            UWDamagePipeline.Apply(mOVitals, UWDamageRequest.Curse(mOStatus.CurseDice));
    }

    public bool IsChargingAttack => false;

    public int GetArmourAt(int piPart)
    {
        return piPart >= 0 && piPart < mOStatus.Armour.Length ? mOStatus.Armour[piPart] : 0;
    }

    public int DamageTypeProof => mOStatus.DamageTypeProof;

    /// <summary>The status as the last status pass left it, for the debug overlay (UWDebugOverlay): read as it
    /// is, not computed anew, so the overlay shows whether a pass ran.</summary>
    public UWArmourProtection.Status WornStatus => mOStatus;

    /// <summary>What a creature rolls against (UWPlayerCritterRow.GetDefence), as UWCharacter.DefenceCheck.</summary>
    public int DefenceCheck => fDefence;

    /// <summary>The Valor spell's bonus to the attack score, 10 + Casting / 5 (uw2 PLAYDATA.C:222).</summary>
    public int Valor => mOStatus.HasValor && mOVitals != null ? ValorBase + (mOVitals.GetSkill(UWPlayerData.Skill.Casting) / ValorCastingDivisor) : 0;

    /// <summary>Poison Weapon's share of the damage in fortieths, Casting + 30 (COMBAT.C:325).</summary>
    public int PoisonWeaponShare => mOStatus.HasPoisonWeapon && mOVitals != null ? mOVitals.GetSkill(UWPlayerData.Skill.Casting) + PoisonWeaponBase : 0;

    private const int ValorBase = 10;

    private const int ValorCastingDivisor = 5;

    private const int PoisonWeaponBase = 30;
}
