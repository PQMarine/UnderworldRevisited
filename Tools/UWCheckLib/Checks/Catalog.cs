namespace UnderworldRevisited.Tools
{
    /// <summary>
    /// The checks of the engine-free layer, one partial file per domain under Checks/, the hosts
    /// and worlds they run on under Fakes/. Called Program while it was the self-check's own
    /// class; the library (cleanup 9.4) gives it its own name.
    /// </summary>
    public static partial class SelfChecks
    {
        /// <summary>
        /// Every group in the order it runs. Four need no data and run first, as they always
        /// did; "Load" reads the installation once; the rest take what it loaded. A group marked
        /// MutatesData changes the loaded world, so the runner reloads the game state after it.
        /// The "original" tag marks groups that pin numbers of UW.EXE, so a mod validator can
        /// skip them.
        /// </summary>
        public static CheckCatalog BuildCatalog()
        {
            CheckCatalog lOCatalog = new CheckCatalog();

            lOCatalog.Add("Constants", pOContext => fCheckConstants(pOContext), false, "meta", "pin");
            lOCatalog.Add("ClickRules", pOContext => fCheckClickRules(), false, "input", "original");
            lOCatalog.Add("CritterBrain", pOContext => fCheckCritterBrain(), false, "brain", "combat", "original");
            lOCatalog.Add("RuleIndex", pOContext => fCheckRuleIndex(), false, "meta");
            lOCatalog.Add("Load", pOContext => fCheckLoad(pOContext), false, "env");
            lOCatalog.Add("Levels", pOContext => fCheckLevels(pOContext.Data), true, "env");
            lOCatalog.Add("Strings", pOContext => fCheckStrings(pOContext.Data), true, "env");
            lOCatalog.Add("Textures", pOContext => fCheckTextures(pOContext.Data), true, "env");
            lOCatalog.Add("Models", pOContext => fCheckModels(pOContext.DataPath), true, "env");
            lOCatalog.Add("Cutscenes", pOContext => fCheckCutscenes(pOContext.DataPath), true, "env");
            lOCatalog.Add("CutsceneRunner", pOContext => fCheckCutsceneRunner(), false, "cutscene", "original");
            lOCatalog.Add("CutsceneRunnerData", pOContext => fCheckCutsceneRunnerData(pOContext), true, "cutscene", "env");
            lOCatalog.Add("Sound", pOContext => fCheckSound(pOContext.Data), true, "env");
            lOCatalog.Add("PlayerStatus", pOContext => fCheckPlayerStatus(pOContext.Data), true, "player", "original");
            lOCatalog.Add("Stealth", pOContext => fCheckStealth(pOContext.Data), true, "player", "original");
            lOCatalog.Add("EasyMovement", pOContext => fCheckEasyMovement(), false, "input", "original");
            lOCatalog.Add("SoundInTheSave", pOContext => fCheckSoundInTheSave(), false, "player", "original");
            lOCatalog.Add("HitVolume", pOContext => fCheckHitVolume(), false, "combat", "original");
            lOCatalog.Add("PowerGem", pOContext => fCheckPowerGem(), false, "hud", "original");
            lOCatalog.Add("Impact", pOContext => fCheckImpact(), false, "player", "original");
            lOCatalog.Add("WaterSwallowsRemains", pOContext => fCheckWaterSwallowsRemains(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("ScrollEdges", pOContext => fCheckScrollEdges(pOContext.Data), true, "hud", "original");
            lOCatalog.Add("StackRules", pOContext => fCheckStackRules(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("Theft", pOContext => fCheckTheft(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("SightLine", pOContext => fCheckSightLine(), false, "path", "original");
            lOCatalog.Add("RespawnDistance", pOContext => fCheckRespawnDistance(pOContext.Data), true, "level", "time", "original");
            lOCatalog.Add("MapReveal", pOContext => fCheckMapReveal(pOContext.Data), true, "level", "original").MutatesData = true;
            lOCatalog.Add("MapPainter", pOContext => fCheckMapPainter(), false, "level", "original");
            lOCatalog.Add("NewCreature", pOContext => fCheckNewCreature(), false, "level", "original");
            lOCatalog.Add("SummonRoll", pOContext => fCheckSummonRoll(pOContext.Data), true, "spell", "original").MutatesData = true;
            lOCatalog.Add("ObjectDamage", pOContext => fCheckObjectDamage(pOContext.DataPath), true, "damage", "original");
            lOCatalog.Add("Moonstone", pOContext => fCheckMoonstone(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("EquipmentWear", pOContext => fCheckEquipmentWear(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("ActiveMobileOrder", pOContext => fCheckActiveMobileOrder(pOContext.Data), true, "level", "original");
            lOCatalog.Add("ConversationBasePointer", pOContext => fCheckConversationBasePointer(), false, "conversation", "original");
            lOCatalog.Add("OriginalRandom", pOContext => fCheckOriginalRandom(), false, "misc", "original");
            lOCatalog.Add("BowlAndStew", pOContext => fCheckBowlAndStew(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("GlowingRock", pOContext => fCheckGlowingRock(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("OneShotChainRemoval", pOContext => fCheckOneShotChainRemoval(pOContext.Data), true, "trap", "original").MutatesData = true;
            lOCatalog.Add("MapRewrite", pOContext => fCheckMapRewrite(pOContext.Data), true, "level", "original").MutatesData = true;
            lOCatalog.Add("PaletteLightMap", pOContext => fCheckPaletteLightMap(pOContext.Data), true, "render");
            lOCatalog.Add("GrimeTint", pOContext => fCheckGrimeTint(pOContext.Data), true, "render");
            lOCatalog.Add("LookupTables", pOContext => fCheckLookupTables(pOContext.Data), true, "render", "pin");
            lOCatalog.Add("TalkRefusal", pOContext => fCheckTalkRefusal(), false, "conversation", "original");
            lOCatalog.Add("ConversationPatches", pOContext => fCheckConversationPatches(pOContext.Data), true, "conversation");
            lOCatalog.Add("ContainsWord", pOContext => fCheckContainsWord(), false, "conversation", "original");
            lOCatalog.Add("ManualAttributes", pOContext => fCheckManualAttributes(pOContext.Data), true, "player", "original");
            lOCatalog.Add("ExplodingBook", pOContext => fCheckExplodingBook(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("OrbManaAside", pOContext => fCheckOrbManaAside(), false, "player", "original");
            lOCatalog.Add("Vitals", pOContext => fCheckVitals(pOContext.Data), true, "player", "original");
            lOCatalog.Add("WeaponMusic", pOContext => fCheckWeaponMusic(), false, "music", "original");
            lOCatalog.Add("ItemClassSpells", pOContext => fCheckItemClassSpells(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("Locks", pOContext => fCheckLocks(), false, "inventory", "trap", "original");
            lOCatalog.Add("Scatter", pOContext => fCheckScatter(), false, "level", "original");
            lOCatalog.Add("ObjectLimit", pOContext => fCheckObjectLimit(pOContext.Data), true, "level", "original");
            lOCatalog.Add("SleepCreatures", pOContext => fCheckSleepCreatures(), false, "sleep", "path", "original");
            lOCatalog.Add("Night", pOContext => fCheckNight(), false, "sleep", "original");
            lOCatalog.Add("DyingCreature", pOContext => fCheckDyingCreatureKept(pOContext.Data), true, "brain", "data", "original").MutatesData = true;
            lOCatalog.Add("BrainRound2", pOContext => fCheckBrainRound2(), false, "brain", "original");
            lOCatalog.Add("NoAttacker", pOContext => fCheckDamageWithoutAttacker(), false, "brain", "trap", "original");
            lOCatalog.Add("AnimationFate", pOContext => fCheckAnimationFate(), false, "world", "original");
            lOCatalog.Add("EndlessDying", pOContext => fCheckEndlessDying(), false, "brain", "original");
            lOCatalog.Add("KinAlarmSaved", pOContext => fCheckKinAlarmSaved(), false, "brain", "save", "original");
            lOCatalog.Add("NightCulling", pOContext => fCheckNightCulling(pOContext.Data), true, "sleep", "level", "original").MutatesData = true;
            lOCatalog.Add("YearlyCheckup", pOContext => fCheckYearlyCheckup(pOContext.Data), true, "brain", "time", "original").MutatesData = true;
            lOCatalog.Add("CreatureTimePass",pOContext => fCheckCreatureTimePass(pOContext.Data), true, "sleep", "level", "original").MutatesData = true;
            lOCatalog.Add("PoisonSpell", pOContext => fCheckPoisonSpell(), false, "spell", "original");
            lOCatalog.Add("DetectMonsters", pOContext => fCheckDetectMonsters(), false, "spell", "original");
            lOCatalog.Add("SpellRules", pOContext => fCheckSpellRules(pOContext.Data), true, "spell", "original");
            lOCatalog.Add("ObjectMotion", pOContext => fCheckObjectMotion(pOContext.Data), true, "motion", "original");
            lOCatalog.Add("Flights", pOContext => fCheckFlights(pOContext), true, "motion", "data", "original");
            lOCatalog.Add("CreatureMotion", pOContext => fCheckCreatureMotion(pOContext.Data), true, "motion", "original");
            lOCatalog.Add("PlayerMotion", pOContext => fCheckPlayerMotion(pOContext.Data), true, "motion", "original");
            lOCatalog.Add("MissileWalkIn", pOContext => fCheckMissileWalkIn(pOContext.Data), true, "motion", "combat", "original");
            lOCatalog.Add("EasyStep", pOContext => fCheckEasyStep(pOContext.Data), true, "motion", "original");
            lOCatalog.Add("PointerScheme", pOContext => fCheckPointerScheme(pOContext.Data), true, "input", "motion", "original");
            lOCatalog.Add("CritterView", pOContext => fCheckCritterView(), false, "brain");
            lOCatalog.Add("PreciseMotion", pOContext => fCheckPreciseMotion(pOContext.Data), true, "motion");
            lOCatalog.Add("MotionStepper", pOContext => fCheckMotionStepper(), false, "motion", "time");
            lOCatalog.Add("MotionStepperWalk", pOContext => fCheckMotionStepperWalk(pOContext.Data), true, "motion", "time");
            lOCatalog.Add("FootstepRules", pOContext => fCheckFootstepRules(), false, "player", "original");
            lOCatalog.Add("ShakeRounds", pOContext => fCheckShakeRounds(), false, "player", "original");
            lOCatalog.Add("SaveRoundTrip", pOContext => fCheckSaveRoundTrip(pOContext), true, "data", "pin").MutatesData = true;
            lOCatalog.Add("Codecs", pOContext => fCheckCodecs(pOContext), true, "data", "original");
            lOCatalog.Add("HudArt", pOContext => fCheckHudArt(pOContext), true, "hud", "pin");
            lOCatalog.Add("HudArtCompose", pOContext => fCheckHudArtCompose(), false, "hud");
            lOCatalog.Add("ScrollPaper", pOContext => fCheckScrollPaper(pOContext), true, "hud", "pin");
            lOCatalog.Add("ClassicLayout", pOContext => fCheckClassicLayout(), false, "hud", "original");
            lOCatalog.Add("ClassicLayoutColours", pOContext => fCheckClassicLayoutColours(pOContext.Data), true, "hud", "original");
            lOCatalog.Add("ClassicGeometry", pOContext => fCheckClassicGeometry(), false, "hud", "input");
            lOCatalog.Add("DragonAnimator", pOContext => fCheckDragonAnimator(), false, "hud", "original");
            lOCatalog.Add("OptionsMenu", pOContext => fCheckOptionsMenu(), false, "hud", "original");
            lOCatalog.Add("Reach", pOContext => fCheckReach(), false, "inventory", "original");
            lOCatalog.Add("ItemApplications", pOContext => fCheckItemApplications(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("LockRules", pOContext => fCheckLockRules(), false, "inventory", "trap", "original");
            lOCatalog.Add("DoorRules", pOContext => fCheckDoorRules(pOContext.Data), true, "trap", "level", "original");
            lOCatalog.Add("InventoryModel", pOContext => fCheckInventoryModel(pOContext.Data), true, "inventory").MutatesData = true;
            lOCatalog.Add("Eating", pOContext => fCheckEating(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("Spill", pOContext => fCheckSpill(pOContext.Data), true, "inventory", "damage", "original").MutatesData = true;
            lOCatalog.Add("Factory", pOContext => fCheckFactory(pOContext), true, "inventory", "level", "original").MutatesData = true;
            lOCatalog.Add("Placement", pOContext => fCheckPlacement(), false, "level", "original");
            lOCatalog.Add("PickupRules", pOContext => fCheckPickupRules(pOContext.Data), true, "inventory", "original").MutatesData = true;
            lOCatalog.Add("ItemTargetRules", pOContext => fCheckItemTargetRules(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("Look", pOContext => fCheckLook(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("WorldObjectSpell", pOContext => fCheckWorldObjectSpell(pOContext.Data), true, "inventory", "spell", "original");
            lOCatalog.Add("WorldEdit", pOContext => fCheckWorldEdit(pOContext), true, "level", "original").MutatesData = true;
            lOCatalog.Add("Traps", pOContext => fCheckTraps(pOContext.Data), true, "trap", "original").MutatesData = true;
            lOCatalog.Add("DispatchCoverage", pOContext => fCheckDispatchCoverage(pOContext.Data), true, "trap", "conversation", "inventory", "original");
            lOCatalog.Add("Session", pOContext => fCheckSession(pOContext), true, "data", "session");
            lOCatalog.Add("SessionRandom", pOContext => fCheckSessionRandom(), false, "misc", "session", "original");
            lOCatalog.Add("BarterOverflow", pOContext => fCheckBarterOverflow(pOContext), true, "conversation", "original");
            lOCatalog.Add("BarterSetup", pOContext => fCheckBarterSetup(pOContext), true, "conversation", "original");
            lOCatalog.Add("BarterHandover", pOContext => fCheckBarterHandover(pOContext), true, "conversation", "original");
            lOCatalog.Add("TradeLootRoll", pOContext => fCheckTradeLootRoll(pOContext), true, "conversation", "inventory", "original");
            lOCatalog.Add("TradeSlotDrop", pOContext => fCheckTradeSlotDrop(pOContext), true, "conversation", "inventory", "original");
            lOCatalog.Add("TradeInventoryFunctions", pOContext => fCheckTradeInventoryFunctions(pOContext), true, "conversation", "inventory", "original");
            lOCatalog.Add("IdentifyName", pOContext => fCheckIdentifyName(pOContext), true, "conversation", "inventory", "original");
            lOCatalog.Add("ClosingPause", pOContext => fCheckClosingPause(pOContext), true, "conversation", "original");
            lOCatalog.Add("ConversationAttitudes", pOContext => fCheckConversationAttitudes(pOContext), true, "conversation", "original").MutatesData = true;
            lOCatalog.Add("RefusedDemand", pOContext => fCheckRefusedDemand(pOContext), true, "conversation", "original");
            lOCatalog.Add("GoalWriteBack", pOContext => fCheckGoalWriteBack(pOContext), true, "conversation", "original");
            lOCatalog.Add("PartnerName", pOContext => fCheckPartnerName(pOContext), true, "conversation", "original");
            lOCatalog.Add("Uw1Builtins", pOContext => fCheckUw1Builtins(), false, "conversation", "original");
            lOCatalog.Add("SaveCodec", pOContext => fCheckSaveCodec(pOContext), true, "data", "pin", "session");
            lOCatalog.Add("Repair", pOContext => fCheckRepair(pOContext.Data), true, "inventory", "original");
            lOCatalog.Add("PlayerWorld", pOContext => fCheckPlayerWorld(pOContext.Data), true, "player", "level", "original");
            lOCatalog.Add("MissileHit", pOContext => fCheckMissileHit(), false, "combat", "damage", "original");
            lOCatalog.Add("World", pOContext => fCheckWorld(), false, "world", "level");
            lOCatalog.Add("Blow", pOContext => fCheckBlow(), false, "world", "combat", "damage");
            lOCatalog.Add("CreatureState", pOContext => fCheckCreatureState(), false, "brain", "save", "damage");
            lOCatalog.Add("DamagePipeline", pOContext => fCheckDamagePipeline(), false, "player", "combat", "damage", "original");
            lOCatalog.Add("AttackResolution", pOContext => fCheckAttackResolution(), false, "combat", "damage", "original");
            lOCatalog.Add("Death", pOContext => fCheckDeath(), false, "player", "original");
            lOCatalog.Add("TerrainRules", pOContext => fCheckTerrainRules(), false, "player", "time", "original");
            lOCatalog.Add("VoidRules", pOContext => fCheckVoidRules(), false, "player", "level", "original");
            lOCatalog.Add("WorldScheduler", pOContext => fCheckWorldScheduler(), false, "time", "session");
            lOCatalog.Add("TriggerPacer", pOContext => fCheckTriggerPacer(), false, "time", "trap");
            lOCatalog.Add("EndgameSequence", pOContext => fCheckEndgameSequence(), false, "time", "level", "original");
            lOCatalog.Add("Story", pOContext => fCheckStory(pOContext.Data), true, "world", "shrine", "original");
            lOCatalog.Add("SpellHost", pOContext => fCheckSpellHost(), false, "world", "spell", "original");
            lOCatalog.Add("AreaWalk", pOContext => fCheckAreaWalk(), false, "world", "spell", "damage", "original");
            lOCatalog.Add("AreaWalkLevel", pOContext => fCheckAreaWalkOnLevel(pOContext.Data), true, "world", "spell", "level", "original");
            lOCatalog.Add("CreatureCasting", pOContext => fCheckCreatureCasting(pOContext.Data), true, "world", "spell", "brain", "original");
            lOCatalog.Add("CreatureActor", pOContext => fCheckCreatureActor(pOContext.Data), true, "world", "brain", "combat", "original").MutatesData = true;
            lOCatalog.Add("CreatureDeath", pOContext => fCheckCreatureDeath(pOContext.Data), true, "world", "brain", "save", "original");
            lOCatalog.Add("CreatureHost", pOContext => fCheckCreatureHost(pOContext.Data), true, "world", "brain", "session");
            lOCatalog.Add("WallSquares", pOContext => fCheckWallSquares(pOContext.Data), true, "world", "spell", "damage", "original");
            lOCatalog.Add("WeaponSwing", pOContext => fCheckWeaponSwing(pOContext.Data), true, "hud", "combat");
            lOCatalog.Add("Paperdoll", pOContext => fCheckPaperdoll(), false, "hud", "inventory");
            lOCatalog.Add("UiTables", pOContext => fCheckUiTables(), false, "hud", "inventory");
            lOCatalog.Add("RuneShelf", pOContext => fCheckRuneShelf(), false, "hud", "spell", "original");
            lOCatalog.Add("UiMessageIds", pOContext => fCheckUiMessageIds(pOContext.Data), true, "hud", "original");
            lOCatalog.Add("FlaskGauge", pOContext => fCheckFlaskGauge(), false, "hud", "player");
            lOCatalog.Add("UploadPixels", pOContext => fCheckUploadPixels(), false, "hud");
            lOCatalog.Add("UploadPixelsData", pOContext => fCheckUploadPixelsData(pOContext.Data), true, "hud");
            lOCatalog.Add("ArtStamp", pOContext => fCheckArtStamp(), false, "hud");
            lOCatalog.Add("AutomapView", pOContext => fCheckAutomapView(), false, "hud", "level");
            lOCatalog.Add("AutomapViewData", pOContext => fCheckAutomapViewData(pOContext.Data), true, "hud", "level");
            lOCatalog.Add("MapNotesModel", pOContext => fCheckMapNotesModel(), false, "hud", "input", "original");
            lOCatalog.Add("UiHelpers", pOContext => fCheckUiHelpers(), false, "hud", "inventory");
            lOCatalog.Add("UiHelpersData", pOContext => fCheckUiHelpersData(pOContext.Data), true, "hud", "inventory");
            lOCatalog.Add("InventoryView", pOContext => fCheckInventoryView(pOContext.Data), true, "hud", "inventory");
            lOCatalog.Add("TextRaster", pOContext => fCheckTextRaster(pOContext.Data), true, "hud");
            lOCatalog.Add("PanelTurn", pOContext => fCheckPanelTurn(), false, "hud");
            lOCatalog.Add("CharacterSheet", pOContext => fCheckCharacterSheet(), false, "hud", "player");
            lOCatalog.Add("CharacterSheetData", pOContext => fCheckCharacterSheetData(pOContext.Data), true, "hud", "player");
            lOCatalog.Add("ConversationView", pOContext => fCheckConversationView(pOContext.Data), true, "hud", "conversation", "original");
            lOCatalog.Add("MessageLogModel", pOContext => fCheckMessageLogModel(), false, "hud", "original");
            lOCatalog.Add("CountInput", pOContext => fCheckCountInput(), false, "hud", "input", "inventory");
            lOCatalog.Add("EasyStepKeys", pOContext => fCheckEasyStepKeys(), false, "input", "settings");
            lOCatalog.Add("InputRepeat", pOContext => fCheckInputRepeat(), false, "input", "hud");
            lOCatalog.Add("PointerMotion", pOContext => fCheckPointerMotion(), false, "input", "settings");
            lOCatalog.Add("ActionTable", pOContext => fCheckActionTable(), false, "input", "settings");
            lOCatalog.Add("InputContexts", pOContext => fCheckInputContexts(), false, "input", "hud");
            lOCatalog.Add("Hotkeys", pOContext => fCheckHotkeys(pOContext), false, "input", "hud");
            lOCatalog.Add("BackChain", pOContext => fCheckBackChain(pOContext), false, "input", "hud");
            lOCatalog.Add("PressOwners", pOContext => fCheckPressOwners(), false, "input", "hud");
            lOCatalog.Add("CursorReturn", pOContext => fCheckCursorReturn(pOContext.Data), true, "hud", "inventory");
            lOCatalog.Add("DragSource", pOContext => fCheckDragSource(pOContext), true, "hud", "inventory", "conversation");
            lOCatalog.Add("YesNoQuestion", pOContext => fCheckYesNoQuestion(), false, "hud", "input");
            lOCatalog.Add("PendingSpell", pOContext => fCheckPendingSpell(), false, "spell", "input", "original");
            lOCatalog.Add("AttackState", pOContext => fCheckAttackState(), false, "combat", "input", "original");
            lOCatalog.Add("UiSchemePolicy", pOContext => fCheckUiSchemePolicy(), false, "hud", "input");
            lOCatalog.Add("AudioMix", pOContext => fCheckAudioMix(pOContext.Data), true, "music", "sound");
            lOCatalog.Add("LightSources", pOContext => fCheckLightSources(), false, "render", "pin");
            lOCatalog.Add("SettingsTable", pOContext => fCheckSettingsTable(), false, "settings", "pin");
            lOCatalog.Add("SettingsRows", pOContext => fCheckSettingsRows(), false, "settings");
            lOCatalog.Add("LayoutPresets", pOContext => fCheckLayoutPresets(), false, "settings", "hud", "pin");
            lOCatalog.Add("HelpTabIds", pOContext => fCheckHelpTabIds(), false, "settings", "pin");
            lOCatalog.Add("TextTable", pOContext => fCheckTextTable(), false, "settings", "text");
            lOCatalog.Add("KitRows", pOContext => fCheckKitRows(), false, "settings", "text");
            lOCatalog.Add("KitTextArea", pOContext => fCheckTextArea(), false, "settings", "text");
            lOCatalog.Add("DebugReport", pOContext => fCheckDebugReport(), false, "hud", "settings");
            lOCatalog.Add("WindowThemes", pOContext => fCheckWindowThemes(pOContext), false, "settings", "pin");

            return lOCatalog;
        }
    }
}
